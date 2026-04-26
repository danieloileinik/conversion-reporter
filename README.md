# conversion-reporter
A scalable microservice that processes user actions (views/payments) and calculates conversion rates for e-commerce.

## Architecture

The service follows Clean Architecture with Onion layering and strict dependency direction — outer layers depend on inner layers, never the reverse.

```
Domain
  └── Application (depends on Domain)
        └── Infrastructure (depends on Application)
              └── Presentation (depends on Application)
```

### Layers

**Domain** contains the core business logic: the `Report` aggregate, `ConversionRatio` value object with validation, `Action` entity, and domain error definitions. No external dependencies.

**Application** contains CQRS handlers, pipeline behaviors, and repository abstractions. Uses MediatR for in-process messaging. Pipeline behaviors handle cross-cutting concerns: idempotency checks, input validation via FluentValidation, and database transactions via Unit of Work.

**Application.Contracts** is a separate project containing commands, queries, and response DTOs. This is the public contract of the application layer, referenced by the Presentation layer without pulling in handler implementations.

**Infrastructure.Persistence** implements repository interfaces using Entity Framework Core with PostgreSQL. Includes Outbox pattern implementation for reliable event publishing and EF configurations that keep domain models free of data annotations.

**Infrastructure.Caching** provides Redis-backed idempotency key storage and a decorator over the report repository that adds a cache layer transparently using the Decorator pattern via Scrutor.

**Infrastructure.Messaging** contains a Kafka producer via the Outbox worker — a background service that polls unpublished outbox messages and publishes them to Kafka — and a Kafka consumer that receives incoming action registration events from other services.

**Presentation.Grpc** exposes the service over gRPC. Contains the proto definition and the service implementation that maps between transport types and application contracts.

## Tech Stack

- .NET 10
- Entity Framework Core with Npgsql (PostgreSQL)
- MediatR for CQRS pipeline
- FluentValidation
- StackExchange.Redis
- Confluent.Kafka
- Grpc.AspNetCore
- Scrutor for decorator registration
- ErrorOr for result types
- xUnit, FluentAssertions, NSubstitute, Testcontainers

## CQRS

All operations go through the MediatR pipeline. Commands mutate state; queries read state without opening a transaction.

Commands: `CreateReportCommand`, `CountRatioCommand`, `CancelReportCommand`

Queries: `GetReportQuery`

Pipeline execution order for commands:

```
IdempotencyBehavior -> ValidationBehavior -> TransactionBehavior -> Handler
```

Idempotency check runs first to avoid unnecessary work. Validation runs before opening a transaction. The transaction wraps the handler and commits on success.

## Outbox Pattern

Handlers never publish to Kafka directly. Instead, they write an `OutboxMessage` record to the database in the same transaction as the domain change. This guarantees atomicity — either both the domain change and the event are persisted, or neither is.

The `OutboxWorker` background service polls the `outbox_messages` table every 5 seconds, publishes pending messages to Kafka, and marks them as processed.

```
Handler
  writes Report + OutboxMessage in one transaction

OutboxWorker (every 5s)
  reads unpublished messages
  publishes to Kafka topic
  marks as processed
```

## Getting Started

### Prerequisites

- Docker and Docker Compose
- .NET 10 SDK (for local development without Docker)

### Running locally

```bash
git clone <repository>
cd ConversionReporter
cp .env.example .env.production
# fill in any required values in .env.production
docker compose up --build
```

The gRPC service will be available on port 8080.

### Running tests

Unit tests use NSubstitute mocks and do not require any infrastructure:

```bash
dotnet test tests/ConversionReporter.Tests
```

Integration tests use Testcontainers to spin up real PostgreSQL and Redis instances in Docker. Docker must be running:

```bash
dotnet test tests/ConversionReporter.IntegrationTests
```

### Applying migrations

Migrations are applied automatically on startup via `MigrateAsync`. For production deployments, run migrations as a separate step before starting the application:

```bash
dotnet ef database update \
  --project src/Infrastructure/ConversionReporter.Infrastructure.Persistence \
  --startup-project src/Presentation/ConversionReporter.Presentation.Grpc
```

## Configuration

Copy `.env.example` to `.env.production` and fill in the values.

| Variable | Description |
|---|---|
| `POSTGRES_DB` | Database name |
| `POSTGRES_USER` | PostgreSQL user |
| `POSTGRES_PASSWORD` | PostgreSQL password |
| `ConnectionStrings__Postgres` | Full Npgsql connection string |
| `ConnectionStrings__Redis` | Redis connection string |
| `Kafka__BootstrapServers` | Kafka broker address |
| `CLUSTER_ID` | Kafka KRaft cluster ID (base64 string) |

## Sending a Kafka message

The service consumes `RegisterActionCommand` messages from the `actions` topic. Publish a message with the following JSON structure:

```json
{
  "ItemId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "ActionType": "View",
  "IdempotencyKey": "7c9e6679-7425-40de-944b-e07fc1f90ae7"
}
```

`ActionType` accepts `View` or `Payment`. The `IdempotencyKey` ensures the message is processed exactly once — sending the same key twice will result in only one action being registered.

Using the Kafka CLI:

```bash
docker exec -it conversionreporter-kafka-1 \
  kafka-console-producer \
  --bootstrap-server localhost:9092 \
  --topic actions
```

Then paste the JSON and press Enter.

## Calling the gRPC method

The service exposes one gRPC method: `GetReport`.

Proto definition:

```protobuf
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
  optional double ratio = 6;
}
```

Using grpcurl:

```bash
grpcurl -plaintext \
  -d '{"report_id": "3fa85f64-5717-4562-b3fc-2c963f66afa6"}' \
  localhost:8080 \
  reports.Reports/GetReport
```

Possible status values in the response: `Processing`, `Done`, `Canceled`.

The `ratio` field is omitted when the report has not yet been calculated (status is `Processing`).

## Project Structure

```
src/
  Domain/
    ConversionReporter.Domain/
  Application/
    ConversionReporter.Application/
    ConversionReporter.Application.Contracts/
  Infrastructure/
    ConversionReporter.Infrastructure.Persistence/
    ConversionReporter.Infrastructure.Caching/
    ConversionReporter.Infrastructure.Messaging/
  Presentation/
    ConversionReporter.Presentation.Grpc/
tests/
  ConversionReporter.Tests/
  ConversionReporter.IntegrationTests/
```
