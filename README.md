# ConversionReporter

A scalable microservice that processes user actions (views/payments) and calculates conversion rates for e-commerce.

## Architecture

Vertical Slice Architecture — each feature is a self-contained slice owning its command/query, handler, validator, and transport entry point. Cross-cutting concerns live in `Common/`.

```
src/
└── ConversionReporter/
    ├── Common/
    │   ├── Abstractions/
    │   └── Behaviors/
    ├── Domain/
    │   ├── Actions/
    │   └── Reports/
    ├── Features/
    │   ├── Actions/
    │   │   └── Commands/
    │   │       └── RegisterAction/
    │   └── Reports/
    │       ├── Commands/
    │       │   ├── CancelReport/
    │       │   ├── CountRatio/
    │       │   └── CreateReport/
    │       └── Queries/
    │           └── GetReport/
    └── Infrastructure/
        ├── Caching/
        ├── Messaging/
        │   ├── Kafka/
        │   └── Outbox/
        └── Persistence/
            └── Configurations/
```

### CQRS

Commands and queries are dispatched through MediatR:

- **Commands** (`RegisterActionCommand`, `CreateReportCommand`, `CountRatioCommand`, `CancelReportCommand`) mutate state, return `ErrorOr<Success>` or a typed response, and flow through Kafka topics.
- **Queries** (`GetReportQuery`) implement the `IQuery` marker, bypass the transactional pipeline, and read from a Redis-backed cache before falling through to Postgres.

MediatR pipeline order:

1. `ValidationBehavior` — FluentValidation, throws on failure.
2. `IdempotencyBehavior` — short-circuits duplicate `IIdempotentCommand` requests via Redis key.
3. `TransactionBehavior` — skips queries, commits via `SaveChangesAsync` only on non-error result.

Write paths persist domain changes and outbox messages in the same EF Core transaction. `OutboxWorker` polls `outbox_messages` every 5 seconds and publishes to Kafka.

### Slice structure

```
Features/Reports/Commands/CreateReport/
    CreateReportCommand.cs
    CreateReportHandler.cs
    CreateReportValidator.cs
    CreateReportConsumer.cs
    CreateReportResponse.cs
```

EF Core configurations live in `Infrastructure/Persistence/Configurations/` — pure persistence mapping, no business logic.

### Reliability patterns

| Concern            | Mechanism                                                       |
|--------------------|-----------------------------------------------------------------|
| Duplicate commands | Redis idempotency keys, 7-day TTL                               |
| Event publishing   | Transactional Outbox + dedicated worker                         |
| Read latency       | Redis read-through cache, invalidated on `Cancel`               |
| Consumer offsets   | `EnableAutoCommit = false`, manual commit after handler success |

## Tech stack

| Layer          | Technology                                           |
|----------------|------------------------------------------------------|
| Runtime        | .NET 10                                              |
| Language       | C# 14                                                |
| API            | gRPC (`Grpc.AspNetCore` 2.64)                        |
| Mediator       | MediatR 12                                           |
| Validation     | FluentValidation 11                                  |
| Result type    | ErrorOr 2.0                                          |
| Database       | PostgreSQL via EF Core 10 + Npgsql                   |
| Cache          | Redis via `StackExchange.Redis` 3.0                  |
| Message broker | Apache Kafka via `Confluent.Kafka` 2.14              |
| Testing        | xUnit, NSubstitute, FluentAssertions, Testcontainers |

## Domain flow

```
Kafka: actions              ──▶ RegisterActionConsumer ──▶ RegisterActionHandler
Kafka: reports.create       ──▶ CreateReportConsumer   ──▶ CreateReportHandler
Kafka: reports.count-ratio  ──▶ CountRatioConsumer     ──▶ CountRatioHandler
Kafka: reports.cancel       ──▶ CancelReportConsumer   ──▶ CancelReportHandler

gRPC:  Reports/GetReport    ──▶ GetReportHandler       ──▶ Redis | Postgres
```

## Running locally

Prerequisites: .NET 10 SDK, Docker.

```bash
docker compose up -d postgres redis kafka

dotnet ef database update --project src/ConversionReporter

dotnet run --project src/ConversionReporter
```

`appsettings.json`:

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

## gRPC API

```proto
syntax = "proto3";

package report;
option csharp_namespace = "ConversionReporter.Grpc";

service Reports {
    rpc GetReport (GetReportRequest) returns (GetReportResponse);
}

message GetReportRequest { string report_id = 1; }

message GetReportResponse {
    string id = 1;
    string item_id = 2;
    string start_date = 3;
    string end_date = 4;
    string status = 5;
    double ratio = 6;
}
```

Status codes: `OK`, `INVALID_ARGUMENT` (bad GUID), `NOT_FOUND`.

```bash
grpcurl -plaintext \
    -d '{"report_id": "5f4e8b1a-9c3d-4a7e-b8f2-1d6c5e9a0b3f"}' \
    localhost:5001 \
    report.Reports/GetReport
```

## Kafka commands

| Topic                 | Command                 | Payload fields                                                  |
|-----------------------|-------------------------|-----------------------------------------------------------------|
| `actions`             | `RegisterActionCommand` | `ItemId`, `ActionType` (`View` \| `Payment`), `IdempotencyKey` |
| `reports.create`      | `CreateReportCommand`   | `ItemId`, `StartDate`, `EndDate`, `IdempotencyKey`              |
| `reports.count-ratio` | `CountRatioCommand`     | `ReportId`, `IdempotencyKey`                                    |
| `reports.cancel`      | `CancelReportCommand`   | `ReportId`                                                      |

```bash
echo '{"ItemId":"8a1b3c4d-5e6f-7081-92a3-b4c5d6e7f809","ActionType":"View","IdempotencyKey":"f1e2d3c4-b5a6-9788-6655-443322110099"}' \
  | kafka-console-producer --bootstrap-server localhost:9092 --topic actions
```

## Testing

```bash
dotnet test tests/ConversionReporter.Tests
dotnet test tests/ConversionReporter.IntegrationTests
```

Integration tests spin up Postgres, Redis, and Kafka via Testcontainers.