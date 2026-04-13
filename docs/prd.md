# PRD: Data Ingestion Service

## Overview

A .NET 8 Web API service for ingesting, validating, deduplicating, and querying customer transaction data. Supports both bulk CSV upload and real-time single-transaction ingestion.

---

## Stack

| Layer | Choice | Reason |
| Framework | .NET 8 Web API | Required |
| Architecture | Clean Architecture | Clear separation of domain/application/infrastructure; domain has zero external deps |
| Database | PostgreSQL | Reliable, great for bulk inserts, native JSON support for stats queries |
| ORM | EF Core 8 | Migrations, typed queries, bulk insert via EFCore.BulkExtensions |
| Cache | Redis | In-memory cache for expensive aggregate queries (`/stats/summary`) |
| Validation | FluentValidation + ISOCurrencies | Expressive, testable validators; ISO 4217 currency check via third-party NuGet; defined in Application layer |
| Containerization | Docker + docker-compose | Single `docker-compose up` to run everything |
| Testing | xUnit + Moq | Standard .NET test stack |

---

## Data Model

### `transactions` table

```
id              UUID        PK
customer_id     VARCHAR     indexed
transaction_date TIMESTAMP
amount          DECIMAL(18,4)
currency        CHAR(3)
source_channel  VARCHAR
idempotency_key VARCHAR     UNIQUE (for dedup)
created_at      TIMESTAMP
```

**Deduplication key**: `idempotency_key` = hash/composite of `(customer_id + transaction_date + amount + currency + source_channel)`. Duplicate = same key already exists.

---

## Endpoints

### POST `/ingest/batch`
- Accepts `multipart/form-data` CSV upload
- Streams file line-by-line to avoid loading 100K rows into memory at once
- Validates each row (see Validation below)
- Bulk-inserts valid rows using `EFCore.BulkExtensions` (single roundtrip)
- Skips duplicates silently (ON CONFLICT DO NOTHING)
- Returns:
```json
{
  "accepted": 98000,
  "rejected": 2000,
  "errors": [
    { "row": 42, "reason": "Invalid currency code: XYZ" }
  ]
}
```

### POST `/ingest/transaction`
- Accepts JSON body
- Same validation rules as batch
- Returns `201 Created` with saved transaction, or `400`/`409` on error/duplicate

### GET `/customers/:id/transactions`
- Paginated (`page`, `pageSize` query params)
- Filterable by `dateFrom`, `dateTo`, `currency`, `sourceChannel`
- Returns transactions array + total count

### GET `/stats/summary`
- Returns aggregate stats:
```json
{
  "totalTransactions": 150000,
  "totalCustomers": 4200,
  "totalVolumeByCurrency": { "USD": 1200000.00, "EUR": 340000.00 },
  "avgDailyTransactionAmount": 87.50,
  "topSourceChannels": [{ "channel": "web", "count": 90000 }],
  "transactionsLast24h": 1200
}
```
- `avgDailyTransactionAmount`: average of total transaction amounts summed per day, over the last 30 days
- Response is cached in Redis with a **60-second TTL**
- Cache key: `stats:summary` (global, not per-user)
- On cache miss: runs full aggregate query against PostgreSQL, then writes result to Redis
- Cache is **not** invalidated on ingest — stale-by-TTL is acceptable; stats are not expected to be real-time

---

## Validation Rules

| Field | Rule |
|---|---|
| `customer_id` | Required, non-empty |
| `transaction_date` | Required, valid datetime, not in the future |
| `amount` | Required, > 0, max 2 decimal places |
| `currency` | Required, valid ISO 4217 code — validated via `ISOCurrencies` NuGet (not just regex) |
| `source_channel` | Required, non-empty |

---

## Architecture

**Clean Architecture** — four projects with a strict inward dependency rule:

```
DataIngestionService.Domain          (no external deps)
DataIngestionService.Application     (depends on Domain only)
DataIngestionService.Infrastructure  (depends on Application + Domain; owns EF Core & Redis)
DataIngestionService.Api             (depends on Application; wires DI and HTTP)
```

```
HTTP Request
    │
    ▼
API Layer (thin — input parsing, HTTP mapping only)
    │
    ▼
Application Layer (use cases, FluentValidation, orchestration)
    │           │
    │           ▼ (stats/summary only)
    │         IStatsCache → Redis (Infrastructure)
    │           │ miss
    ▼           ▼
ITransactionRepository → EF Core (Infrastructure) → PostgreSQL
```

- `ITransactionRepository` and `IStatsCache` are interfaces declared in Application; implemented in Infrastructure — Application never references EF Core or Redis directly.
- Validators (`TransactionRequestValidator`) are `AbstractValidator<T>` classes in Application, registered in DI and called explicitly by use cases.
- Batch processing runs in a single DB transaction per chunk (5000 rows) to balance performance and atomicity.
- All errors surface as structured `ProblemDetails` responses.
- `IDistributedCache` is wrapped by `IStatsCache` in Infrastructure; cache logic lives in the Application use case, not the controller.

---

## Docker Setup

- `docker-compose.yml` defines three services: `api`, `postgres`, and `redis`
- `api` depends on `postgres` and `redis` with healthchecks
- DB migrations run automatically on startup via `dotnet ef database update`
- Environment config via `.env` or `docker-compose` environment block
- Redis runs with default config (no persistence needed — cache only)

---

## Testing Scope

- Validation logic (unit)
- Deduplication logic (unit)
- Batch CSV parsing edge cases (unit)
- Stats aggregation (unit)
- Controller integration tests for happy path + error cases

---

## Trade-offs & Out of Scope

- **No auth** — not required per spec
- **No message queue** — RabbitMQ/Kafka intentionally excluded. The `POST /ingest/batch` contract returns a synchronous per-row error report; making this async would require a polling endpoint (`GET /ingest/jobs/:id`) and significantly more complexity. The real bottleneck is PostgreSQL throughput, not message passing, so a queue would add operational overhead without improving throughput. Revisit if the requirement shifts to fire-and-forget with async status.
- **No retry mechanism** — partial batch failures are reported but not retried
- **Stats cached via Redis** — 60-second TTL; stale-by-TTL is acceptable since stats are not expected to be real-time