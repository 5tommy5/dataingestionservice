# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build
dotnet build

# Run (API only)
dotnet run --project DataIngestionService.Api

# Run with Docker (full stack: API + PostgreSQL + Redis)
docker compose up

# Run tests
dotnet test

# Run a single test
dotnet test --filter "FullyQualifiedName~TestClassName.MethodName"

# EF Core migrations (DbContext lives in Infrastructure)
dotnet ef migrations add <MigrationName> --project DataIngestionService.Infrastructure --startup-project DataIngestionService.Api
dotnet ef database update --project DataIngestionService.Infrastructure --startup-project DataIngestionService.Api
```

## Architecture

This is a .NET 8 Web API service for ingesting, validating, deduplicating, and querying customer transaction data, structured as **Clean Architecture**. The stack is:

- **API**: ASP.NET Core 8 (`[ApiController]` controllers — no minimal APIs)
- **Database**: PostgreSQL via EF Core 8 with `EFCore.BulkExtensions` for bulk inserts
- **Cache**: Redis (`IDistributedCache` / StackExchange.Redis) — used only for `GET /stats/summary` with 60s TTL
- **Validation**: FluentValidation + `ISOCurrencies` NuGet — validators defined in the Application layer; currency checked against real ISO 4217 data, not just a regex
- **Testing**: xUnit + Moq
- **Container**: Docker + docker-compose (three services: `api`, `postgres`, `redis`)

### Projects

```
DataIngestionService.Domain          — entities, domain exceptions; no external deps
DataIngestionService.Application     — use cases, DTOs, repository/cache interfaces, FluentValidation validators
DataIngestionService.Infrastructure  — EF Core DbContext, repository implementations, Redis cache, migrations
DataIngestionService.Api             — minimal API endpoints, DI registration, startup
DataIngestionService.Tests           — xUnit test project
```

Dependency rule: `Api` → `Application` ← `Infrastructure`; `Domain` has no outward dependencies.

### Layering

```
API (input parsing, HTTP mapping)
    ↓
Application (use cases; FluentValidation; cache logic for stats)
    ↓          ↓ (stats/summary only)
Repository   Redis Cache       ← interfaces defined in Application, implemented in Infrastructure
    ↓
PostgreSQL
```

### Key design decisions

- **Clean Architecture**: domain and application logic have zero dependency on EF Core, Redis, or ASP.NET. Interfaces for `ITransactionRepository` and `IStatsCache` are declared in Application; Infrastructure implements them.
- **FluentValidation**: `TransactionRequestValidator : AbstractValidator<TransactionRequest>` lives in Application. Currency rule uses the `ISOCurrencies` NuGet to check against the real ISO 4217 table rather than a regex. Registered in DI and invoked explicitly by use cases (not auto-wired to model binding).
- **Deduplication** via `idempotency_key` (SHA-256 of `customer_id|transaction_datetime|amount|currency|source_channel`), stored as UNIQUE — duplicates silently skipped via `ON CONFLICT DO NOTHING`.
- **Batch ingestion** streams CSV line-by-line and bulk-inserts in chunks of 15,000 rows per DB transaction.
- **Stats cache** is stale-by-TTL (not invalidated on ingest) — 60s TTL is acceptable per spec.
- **No auth, no message queue** — intentionally excluded per PRD.
- Errors surface as structured `ProblemDetails` responses.

### Endpoints

| Method | Path | Description |
|--------|------|-------------|
| POST | `/ingest/batch` | `IngestController` — multipart CSV upload; returns accepted/rejected counts |
| POST | `/ingest/transaction` | `IngestController` — single JSON transaction; returns 201/400/409 |
| GET | `/customers/{id}/transactions` | `QueryController` — paginated, filterable transaction list |
| GET | `/stats/summary` | `QueryController` — aggregate stats (Redis-cached); use case returns `Task` |

### Data model

`transactions` table: `id` (UUID PK), `customer_id` (indexed), `transaction_date` (TIMESTAMP), `amount` (DECIMAL 18,4), `currency` (CHAR 3), `source_channel`, `idempotency_key` (UNIQUE), `created_at`.

### Validation rules (enforced via FluentValidation)

- `customer_id`: required, non-empty
- `transaction_date`: required, valid datetime, not in the future
- `amount`: required, > 0, max 2 decimal places
- `currency`: required, valid ISO 4217 (3 uppercase letters)
- `source_channel`: required, non-empty
