# ConversionReporter

A scalable microservice that processes user actions (views/payments) and calculates conversion rates for e-commerce.
## Architecture

The solution is split into four layers following Clean Architecture, with the dependency rule pointing inward toward the domain.

```
src/
├── Domain/                                  Pure domain model, no dependencies
│   └── ConversionReporter.Domain
├── Application/                             Use cases, contracts, MediatR pipeline
│   ├── ConversionReporter.Application
│   └── ConversionReporter.Application.Contracts
├── Infrastructure/                          External concerns
│   ├── ConversionReporter.Infrastructure.Persistence    EF Core + Postgres
│   ├── ConversionReporter.Infrastructure.Caching        Redis
│   └── ConversionReporter.Infrastructure.Messaging      Kafka producer/consumers
└── Presentation/
    └── ConversionReporter.Presentation.Grpc             gRPC entry point
```

### CQRS

Commands and queries are dispatched through MediatR and separated by intent:

- **Commands** (`RegisterActionCommand`, `CreateReportCommand`, `CountRatioCommand`, `CancelReportCommand`) mutate state, return `ErrorOr<Success>` or a typed response, and flow through Kafka topics.
- **Queries** (`GetReportQuery`) implement the `IQuery` marker, bypass the transactional pipeline, and read from a Redis-backed cache before falling through to Postgres.

The MediatR pipeline composes three behaviors in this order:

1. `ValidationBehavior` runs FluentValidation rules and throws on failure.
2. `IdempotencyBehavior` short-circuits duplicate `IIdempotentCommand` requests by checking a Redis key.
3. `TransactionBehavior` skips queries, otherwise commits the `IUnitOfWork` only when the handler returns a non-error result.

Write paths persist domain changes and outbox messages in the same EF Core transaction. The `OutboxWorker` background service polls `outbox_messages` every 5 seconds and publishes to Kafka, guaranteeing at-least-once delivery without distributed transactions.

### Reliability patterns

| Concern              | Mechanism                                                           |
|----------------------|---------------------------------------------------------------------|
| Duplicate commands   | Redis idempotency keys, 7-day TTL                                   |
| Event publishing     | Transactional Outbox + dedicated worker                             |
| Read latency         | Redis read-through cache, invalidated on `Cancel`                   |
| Consumer offsets     | `EnableAutoCommit = false`, manual commit after handler success     |

## Tech stack

| Layer          | Technology                                            |
|----------------|-------------------------------------------------------|
| Runtime        | .NET 10                                               |
| Language       | C# 14 (primary constructors, collection expressions)  |
| API            | gRPC (`Grpc.AspNetCore` 2.64)                         |
| Mediator       | MediatR 14                                            |
| Validation     | FluentValidation 12                                   |
| Result type    | ErrorOr 2.0                                           |
| Database       | PostgreSQL via EF Core 10 + Npgsql                    |
| Cache          | Redis via `StackExchange.Redis` 3.0                   |
| Message broker | Apache Kafka via `Confluent.Kafka` 2.14               |
| Testing        | xUnit, NSubstitute, FluentAssertions, Testcontainers  |

## Domain flow

```
Kafka topic: actions               ──▶ RegisterActionConsumer ──▶ RegisterActionCommandHandler
Kafka topic: reports.create        ──▶ CreateReportConsumer   ──▶ CreateReportCommandHandler
Kafka topic: reports.count-ratio   ──▶ CountRatioConsumer     ──▶ CountRatioCommandHandler
Kafka topic: reports.cancel        ──▶ CancelReportConsumer   ──▶ CancelReportCommandHandler

gRPC: ReportService.GetReport      ──▶ GetReportQueryHandler  ──▶ Redis cache | Postgres
```

`Report` is an aggregate with three states: `Processing`, `Done`, `Canceled`. Ratio computation is a domain operation on the aggregate guarded by `ConversionRatio.Create`, which rejects zero or negative payment counts.

## Running locally

Prerequisites: .NET 10 SDK, Docker (for Postgres, Redis, Kafka).

```bash
docker compose up -d postgres redis kafka

dotnet ef database update \
    --project src/Infrastructure/ConversionReporter.Infrastructure.Persistence \
    --startup-project src/Presentation/ConversionReporter.Presentation.Grpc

dotnet run --project src/Presentation/ConversionReporter.Presentation.Grpc
```

Configuration keys expected in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Database=conversion_reporter;Username=postgres;Password=postgres",
    "Redis": "localhost:6379"
  },
  "Kafka": {
    "BootstrapServers": "localhost:9092",
    "GroupId": "conversion-reporter"
  }
}
```

## Calling the gRPC API

The only synchronous entry point is `ReportService.GetReport`, which retrieves a report by id. The proto contract:

```proto
syntax = "proto3";

package conversion_reporter;
option csharp_namespace = "ConversionReporter.Grpc";

service Reports {
    rpc GetReport (GetReportRequest) returns (GetReportResponse);
}

message GetReportRequest {
    string report_id = 1;
}

message GetReportResponse {
    string id = 1;
    string item_id = 2;
    string start_date = 3;
    string end_date = 4;
    string status = 5;
    double ratio = 6;
}
```

### Sample C# client

```csharp
using Grpc.Net.Client;
using ConversionReporter.Grpc;

using var channel = GrpcChannel.ForAddress("https://localhost:5001");
var client = new Reports.ReportsClient(channel);

var response = await client.GetReportAsync(new GetReportRequest
{
    ReportId = "5f4e8b1a-9c3d-4a7e-b8f2-1d6c5e9a0b3f"
});

Console.WriteLine($"Status: {response.Status}, Ratio: {response.Ratio}");
```

### Sample grpcurl call

```bash
grpcurl -plaintext \
    -d '{"report_id": "5f4e8b1a-9c3d-4a7e-b8f2-1d6c5e9a0b3f"}' \
    localhost:5001 \
    conversion_reporter.Reports/GetReport
```

Status codes returned:

- `OK` — report found.
- `INVALID_ARGUMENT` — `report_id` is not a valid GUID.
- `NOT_FOUND` — no report with the given id.

## Sending commands through Kafka

All write operations are dispatched as JSON-serialized command payloads on dedicated topics. The message key is conventionally the aggregate id; the value is the command body.

### Topic map

| Topic                  | Command                  | Payload fields                                              |
|------------------------|--------------------------|-------------------------------------------------------------|
| `actions`              | `RegisterActionCommand`  | `ItemId`, `ActionType` (`View` \| `Payment`), `IdempotencyKey` |
| `reports.create`       | `CreateReportCommand`    | `ItemId`, `StartDate`, `EndDate`, `IdempotencyKey`          |
| `reports.count-ratio`  | `CountRatioCommand`      | `ReportId`, `IdempotencyKey`                                |
| `reports.cancel`       | `CancelReportCommand`    | `ReportId`                                                  |

### Sample C# producer

```csharp
using System.Text.Json;
using Confluent.Kafka;

var config = new ProducerConfig { BootstrapServers = "localhost:9092" };
using var producer = new ProducerBuilder<string, string>(config).Build();

var command = new
{
    ItemId = Guid.NewGuid(),
    ActionType = "View",
    IdempotencyKey = Guid.NewGuid()
};

await producer.ProduceAsync("actions", new Message<string, string>
{
    Key = command.ItemId.ToString(),
    Value = JsonSerializer.Serialize(command)
});
```

### Sample payloads

Register an action:

```json
{
  "ItemId": "8a1b3c4d-5e6f-7081-92a3-b4c5d6e7f809",
  "ActionType": "Payment",
  "IdempotencyKey": "f1e2d3c4-b5a6-9788-6655-443322110099"
}
```

Create a report:

```json
{
  "ItemId": "8a1b3c4d-5e6f-7081-92a3-b4c5d6e7f809",
  "StartDate": "2026-04-01T00:00:00Z",
  "EndDate":   "2026-04-30T23:59:59Z",
  "IdempotencyKey": "a7b6c5d4-e3f2-1100-aabb-ccddeeff0011"
}
```

Trigger ratio computation:

```json
{
  "ReportId": "5f4e8b1a-9c3d-4a7e-b8f2-1d6c5e9a0b3f",
  "IdempotencyKey": "0011aabb-ccdd-eeff-2233-445566778899"
}
```

### Producing via kafka-console-producer

```bash
echo '{"ItemId":"8a1b3c4d-5e6f-7081-92a3-b4c5d6e7f809","ActionType":"View","IdempotencyKey":"f1e2d3c4-b5a6-9788-6655-443322110099"}' \
  | kafka-console-producer \
      --bootstrap-server localhost:9092 \
      --topic actions
```

## End-to-end example

1. Producer sends `RegisterActionCommand` messages to `actions` as users browse and pay.
2. Producer sends `CreateReportCommand` to `reports.create` with the desired window. Handler persists the `Report` (status `Processing`) and writes a `ReportCreated` outbox row.
3. `OutboxWorker` publishes `ReportCreated` to its topic; downstream services react.
4. Producer sends `CountRatioCommand` to `reports.count-ratio`. Handler loads actions in range, computes `views / payments`, transitions the report to `Done`, and emits `RatioCounted`.
5. Client calls `Reports/GetReport` over gRPC. First call hits Postgres and warms the Redis cache; subsequent calls return from cache until `Cancel` invalidates it.

## Testing

```bash
dotnet test tests/ConversionReporter.Tests                 # unit tests
dotnet test tests/ConversionReporter.IntegrationTests      # Testcontainers-backed
```

Integration tests spin up Postgres, Redis, and Kafka via Testcontainers and exercise the real handlers, repositories, consumers, and the gRPC service.