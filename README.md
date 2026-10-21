# ConversionReporter

A .NET 10 service that records views and payments, calculates report ratios in PostgreSQL, and exposes reports over gRPC. Commands arrive through Kafka; Redis caches report snapshots.

## Report behavior

- The ratio is **views / payments**: 100 views and 10 payments produce `10.0`.
- Actions use their registration time in UTC. The report includes actions for its item between `StartDate` and `EndDate`, including both boundaries.
- Creating a report sets `Processing`. Send a separate calculation command to complete it; there is no built-in scheduler.
- Calculation requires at least one payment. Otherwise it returns a retryable error and leaves the report unchanged. The Kafka consumer retries without a limit, delaying later commands in the same partition until a payment in the report period is registered.
- Successful calculation sets `Done`. Repeating it preserves the original ratio and produces no additional event. Create a new report for a new calculation.
- Cancellation sets `Canceled` and is terminal. It can cancel a completed report while preserving its ratio. Repeated cancellation has no further effect. Calculating a canceled report sends the command to dead-letter.

## Run locally

Requirements: Docker with Compose. Run these commands from the repository root:

```bash
cp .env.example .env.local
# Review .env.local before starting the services.
docker compose up -d --wait postgres redis kafka
docker compose build conversionreporter
docker compose run --rm --no-deps --entrypoint ./efbundle conversionreporter
docker compose run --rm --no-deps --publish 5177:8080 \
  --env ASPNETCORE_URLS=http://+:8080 conversionreporter
```

The last command starts the application in the foreground and exposes gRPC at `localhost:5177`. Migrations run through `efbundle`; starting the application alone does not apply them.

The supplied environment values target the Compose network: `postgres`, `redis`, and `kafka` are container hostnames. Kafka advertises `kafka:9092`, so the examples below run Kafka tools inside its container. Running the application on the host requires host-specific connection strings and a Kafka listener advertised to the host. Compose reads `.env.local`; `dotnet run` does not load it automatically.

## End-to-end example

Keep the application running and open another terminal at the repository root. This example also requires [grpcurl](https://github.com/fullstorydev/grpcurl) on the host. Use the sample item only for this example; choose a new item ID and new idempotency keys for an independent run.

1. Register one view and one payment. Both messages use the same Kafka key so they reach the same partition and the view is processed first:

   ```bash
   printf '%s\n' \
     'e58b37a1-a2ca-45f4-89b5-921701925eaa|{"ItemId":"e58b37a1-a2ca-45f4-89b5-921701925eaa","ActionType":"View","IdempotencyKey":"1e528c9a-777d-4273-b856-9238a46af198"}' \
     'e58b37a1-a2ca-45f4-89b5-921701925eaa|{"ItemId":"e58b37a1-a2ca-45f4-89b5-921701925eaa","ActionType":"Payment","IdempotencyKey":"c113b859-74d3-483c-a361-105249dbcf04"}' \
     | docker compose exec -T kafka kafka-console-producer \
         --bootstrap-server kafka:9092 --topic actions \
         --property parse.key=true --property 'key.separator=|'
   ```

2. Create a report with a period that includes the actions' registration time:

   ```bash
   printf '%s\n' '{"ItemId":"e58b37a1-a2ca-45f4-89b5-921701925eaa","StartDate":"2000-01-01T00:00:00Z","EndDate":"2100-01-01T00:00:00Z","IdempotencyKey":"ec5c1de7-c29c-445a-b849-2e34f8669a97"}' \
     | docker compose exec -T kafka kafka-console-producer \
         --bootstrap-server kafka:9092 --topic reports.create
   ```

3. Read creation events, find the event with the sample `ItemId`, and copy its `Id`. Stop the consumer with Ctrl+C:

   ```bash
   docker compose exec kafka kafka-console-consumer \
     --bootstrap-server kafka:9092 --topic ReportCreated --from-beginning
   ```

4. Substitute that ID below and request calculation:

   ```bash
   report_id='<Id from ReportCreated>'
   printf '{"ReportId":"%s","IdempotencyKey":"7102763b-311b-4a20-84e0-283a83e637ae"}\n' "$report_id" \
     | docker compose exec -T kafka kafka-console-producer \
         --bootstrap-server kafka:9092 --topic reports.count-ratio
   ```

   Commands on different topics are processed independently. If calculation arrives before the payment is registered, it retries automatically.

5. Read the report from the same terminal:

   ```bash
   grpcurl -plaintext \
     -import-path src/ConversionReporter.Contracts.Grpc \
     -proto report.proto \
     -d "{\"report_id\":\"$report_id\"}" \
     localhost:5177 report.Report/GetReport
   ```

   If the status is still `Processing`, repeat the read. The expected result is `status: "Done"` and `ratio: 1` for one view and one payment.

## Kafka interface

### Commands

Send JSON objects with these fields. IDs must be nonempty GUIDs; dates must be UTC timestamps, with `StartDate < EndDate`.

| Topic | Fields |
| --- | --- |
| `actions` | `ItemId`, `ActionType` (`View` or `Payment`), `IdempotencyKey` |
| `reports.create` | `ItemId`, `StartDate`, `EndDate`, `IdempotencyKey` |
| `reports.count-ratio` | `ReportId`, `IdempotencyKey` |
| `reports.cancel` | `ReportId` |

For example, register a view:

```bash
printf '%s\n' '{"ItemId":"8a1b3c4d-5e6f-7081-92a3-b4c5d6e7f809","ActionType":"View","IdempotencyKey":"f1e2d3c4-b5a6-9788-6655-443322110099"}' \
  | docker compose exec -T kafka kafka-console-producer \
      --bootstrap-server kafka:9092 --topic actions
```

Reuse an idempotency key only when retrying the same command. A new action or report needs a new key. Successful results are stored by command type and key in `processed_commands`; replaying a successful command emits no additional event. Failed results do not reserve a key.

### Events

| Topic | JSON fields |
| --- | --- |
| `ReportCreated` | `Id`, `ItemId`, `StartDate`, `EndDate` |
| `RatioCounted` | `ReportId`, `Ratio` |

Use the ID from `ReportCreated` for calculation, cancellation, and gRPC reads. To inspect creation events:

```bash
docker compose exec kafka kafka-console-consumer \
  --bootstrap-server kafka:9092 --topic ReportCreated --from-beginning
```

Event keys contain the report ID. The `event-id` header contains the stable outbox message ID. Delivery is **at least once**: consumers must deduplicate that ID. For each report, the outbox publishes earlier events before later ones; a retryable delivery failure blocks that report's later events. Consumers of different topics can still observe events in a different order. Quarantined events stop blocking publication; see [Outbox recovery](#outbox-recovery).

### Failures and retries

| Outcome | Consumer behavior |
| --- | --- |
| Success | Commit the source offset after processing |
| Invalid JSON, command validation failure, or business conflict (such as calculating a canceled report) | Publish to `<source-topic>.dead-letter`, then commit |
| Other business errors, database failures, or failed delivery | Retry from the same offset |

A retryable failure holds up later messages in its partition. Examples include calculating a report with no payments or calculating/canceling an unknown report. There is no command retry limit. Dead-letter messages include `Topic`, `Partition`, `Offset`, `Payload`, and `Error`; if dead-letter delivery fails, the source offset remains uncommitted.

## gRPC interface

The [Protobuf contract](src/ConversionReporter.Contracts.Grpc/report.proto) defines `report.Report/GetReport`. Server reflection is not enabled, so pass the contract to [grpcurl](https://github.com/fullstorydev/grpcurl):

```bash
grpcurl -plaintext \
  -import-path src/ConversionReporter.Contracts.Grpc \
  -proto report.proto \
  -d '{"report_id":"<report-id>"}' \
  localhost:5177 report.Report/GetReport
```

Replace `<report-id>` with an ID from `ReportCreated`. The response contains `id`, `item_id`, UTC `start_date` and `end_date`, `status`, and `ratio`. An uncalculated ratio is returned as `0`; use `status` to distinguish it from a calculated zero.

Expected status codes: `OK`, `INVALID_ARGUMENT` for a malformed GUID, and `NOT_FOUND` for an unknown report.

## Configuration

Connection settings are shown in [.env.example](.env.example); image tags and local infrastructure settings live in [docker-compose.yml](docker-compose.yml). Package versions are defined in the project files.

Optional settings under `Outbox`:

| Setting | Default | Purpose |
| --- | --- | --- |
| `BatchSize` | `100` | Maximum records claimed per batch |
| `MaxParallelism` | `8` | Concurrent publications per worker |
| `PollInterval` | `00:00:01` | Delay when no records are ready or a batch fails |
| `RetryDelay` | `00:00:01` | Initial delivery retry delay |
| `MaxRetryDelay` | `00:05:00` | Maximum delivery retry delay |
| `MaxAttempts` | `0` | Unlimited retries; a positive value enables quarantine |

Environment variables use double underscores, for example `Outbox__BatchSize=200`. With a positive attempt limit, exhausted records stay in `outbox_messages` with `failed_at` set and stop blocking later events.

### Outbox recovery

After fixing delivery, replay a selected unprocessed record by setting `failed_at = NULL`, `attempt_count = 0`, and `next_attempt_at = now()`. There is no replay API. Later events may already have been published while the record was quarantined, so replay can deliver an older event after newer ones.

## Architecture and consistency

Features use vertical slices: each command or query keeps its handler, validation, and transport entry point together. Shared concerns live in `Common/`; persistence, caching, and messaging live in `Infrastructure/`.

| Location | Purpose |
| --- | --- |
| [Features](src/ConversionReporter/Features) | Action registration, report commands, and report reads |
| [Domain](src/ConversionReporter/Domain) | Report state transitions and ratio rules |
| [Common](src/ConversionReporter/Common) | MediatR validation, transaction, and idempotency behaviors |
| [Infrastructure](src/ConversionReporter/Infrastructure) | EF Core mappings/migrations, Redis, Kafka, and outbox |
| [Contracts](src/ConversionReporter.Contracts.Events) | Published event payloads |

The command pipeline runs **validation → transaction → idempotency → handler**. PostgreSQL advisory locks serialize duplicate keys; business changes, saved command results, and outbox records commit together. Report versions detect concurrent updates. Kafka retries concurrency failures against current state.

Reads check the report version in PostgreSQL before using a matching Redis snapshot. A delayed cache fill cannot replace a newer version. Each cache entry expires after one hour; reads still require PostgreSQL and Redis availability.

Outbox workers claim rows with `FOR UPDATE SKIP LOCKED` and publish independent reports in parallel. An earlier retryable event blocks later events for the same report. Failed deliveries use exponential backoff; successful retries clear the error. Ready batches drain without a polling pause. Automatic cleanup of processed outbox records and idempotency records is not implemented.

## Tests

Requires the .NET 10 SDK; integration tests also require Docker.

```bash
dotnet test tests/ConversionReporter.Tests
dotnet test tests/ConversionReporter.IntegrationTests
```

Integration tests share one PostgreSQL, Redis, and KRaft Kafka fixture. Each test clears database/cache state and truncates Kafka topic records. The suite covers concurrent commands, rollback and replay, cache races, outbox claims/retries, period boundaries, and gRPC calls through the generated client.
