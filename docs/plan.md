# Implementation Plan: Data Ingestion Service (Clean Architecture)

## Project structure

```
DataIngestionService.Domain/
DataIngestionService.Application/
DataIngestionService.Infrastructure/
DataIngestionService.Api/
DataIngestionService.Tests/
```

Dependency rule: `Api` → `Application` ← `Infrastructure`; `Domain` has no outward dependencies.

---

## Subtasks

### 1. Solution & project scaffold
Create four class-library projects (`Domain`, `Application`, `Infrastructure`) plus the existing `Api` project and an xUnit `Tests` project. Add project references to enforce the dependency rule. Add NuGet packages per layer:

- **Application**: `FluentValidation`, `ISOCurrencies` (ISO 4217 validation)
- **Infrastructure**: `Npgsql.EntityFrameworkCore.PostgreSQL`, `EFCore.BulkExtensions`, `Microsoft.Extensions.Caching.StackExchangeRedis`
- **Tests**: `xunit`, `Moq`, `Microsoft.AspNetCore.Mvc.Testing`, `FluentValidation.TestHelper`

---

### 2. Domain layer — `Transaction` entity & domain exceptions
In `DataIngestionService.Domain`:
- `Transaction` entity: `Id`, `CustomerId`, `TransactionDate`, `Amount`, `Currency`, `SourceChannel`, `IdempotencyKey`, `CreatedAt`
- `DuplicateTransactionException` — thrown when a single-insert hits the unique constraint
- No EF Core, no FluentValidation, no framework references here

---

### 3. Application layer — interfaces & DTOs
In `DataIngestionService.Application`:
- `ITransactionRepository` — `InsertAsync`, `BulkInsertAsync`, `GetByCustomerIdAsync`, `GetStatsAsync`
- `IStatsCache` — `GetAsync(key)`, `SetAsync(key, value, ttl)`
- Request/response DTOs: `TransactionRequest`, `BatchIngestResponse` (`accepted`, `rejected`, `errors`), `CustomerTransactionsResponse`, `StatsSummaryResponse`
- `ValidationError` record (`field`, `reason`) used in batch error lists

---

### 4. Application layer — FluentValidation validator
In `DataIngestionService.Application`:
- `TransactionRequestValidator : AbstractValidator<TransactionRequest>` with rules:
  - `CustomerId`: `NotEmpty()`
  - `TransactionDate`: `NotEmpty()`, `LessThanOrEqualTo(DateTime.UtcNow.Date)`
  - `Amount`: `GreaterThan(0)`, custom rule asserting max 2 decimal places
  - `Currency`: `NotEmpty()`, custom rule using `ISOCurrencies` (e.g. `CurrencyCodesResolver.TryGetCurrencyCode(value, out _)`) to assert the value is a valid ISO 4217 code
  - `SourceChannel`: `NotEmpty()`
- Register via `services.AddValidatorsFromAssembly(typeof(TransactionRequestValidator).Assembly)`

---

### 5. Application layer — use cases
Four use-case service classes in `DataIngestionService.Application`:

- **`IngestTransactionUseCase`**: compute `IdempotencyKey` (SHA-256 of `customerId|date|amount|currency|sourceChannel`), validate via `IValidator<TransactionRequest>`, call `ITransactionRepository.InsertAsync`, catch `DuplicateTransactionException` → 409
- **`IngestBatchUseCase`**: stream CSV line-by-line, parse + validate each row, collect `{row, reason}` errors, bulk-insert valid rows in chunks of 5000 via `ITransactionRepository.BulkInsertAsync`, return `BatchIngestResponse`
- **`GetCustomerTransactionsUseCase`**: delegate to `ITransactionRepository.GetByCustomerIdAsync` with pagination/filter params
- **`GetStatsSummaryUseCase`**: returns `Task<StatsSummaryResponse>` — checks `IStatsCache` first; on miss calls `ITransactionRepository.GetStatsAsync` and writes to `IStatsCache` with 60 s TTL (Redis I/O is always async so `ValueTask` offers no benefit here)

---

### 6. Infrastructure — EF Core `DbContext` & entity configuration
In `DataIngestionService.Infrastructure`:
- `AppDbContext : DbContext` with `DbSet<Transaction>`
- `TransactionConfiguration : IEntityTypeConfiguration<Transaction>`:
  - `IdempotencyKey` → UNIQUE index
  - `CustomerId` → index
  - `Amount` → `decimal(18,4)`
  - `Currency` → `char(3)`

---

### 7. Infrastructure — migration & startup
Run initial migration:
```bash
dotnet ef migrations add InitialCreate \
  --project DataIngestionService.Infrastructure \
  --startup-project DataIngestionService.Api
```
Apply migrations automatically on startup in `Program.cs` via `app.Services.GetRequiredService<AppDbContext>().Database.MigrateAsync()`.

---

### 8. Infrastructure — repository & cache implementations
In `DataIngestionService.Infrastructure`:
- `TransactionRepository : ITransactionRepository` — implements all four methods; `BulkInsertAsync` uses `EFCore.BulkExtensions` with `ON CONFLICT DO NOTHING`; `InsertAsync` catches `DbUpdateException` for unique-key violations and throws `DuplicateTransactionException`
- `RedisStatsCache : IStatsCache` — wraps `IDistributedCache`; serializes/deserializes with `System.Text.Json`

---

### 9. Docker Compose wiring
Update `compose.yaml` with three services: `api`, `postgres`, `redis`. Add healthchecks on `postgres` and `redis`; `api` depends on both. Pass connection string and Redis URL as environment variables. Redis runs with default config (no persistence).

---

### 10. API — endpoints & DI registration
In `DataIngestionService.Api` (`Program.cs`):
- Register `AppDbContext`, `ITransactionRepository`, `IStatsCache`, all four use cases, and FluentValidation validators in DI
- Two `[ApiController]` controllers:
  - `IngestController` — `POST /ingest/batch`, `POST /ingest/transaction`
  - `QueryController` — `GET /customers/{id}/transactions`, `GET /stats/summary`
- Controllers only parse input and map use-case results/exceptions to `IActionResult` responses (`201 Created`, `400 ProblemDetails`, `409 ProblemDetails`).
- `GetStatsSummary` action awaits `Task<StatsSummaryResponse>` from `GetStatsSummaryUseCase`.

---

### 11. Unit tests — validator & use cases
In `DataIngestionService.Tests`:
- `TransactionRequestValidatorTests` using `FluentValidation.TestHelper` — one test per rule (missing, empty, future date, negative amount, >2 dp, bad currency format)
- `IngestTransactionUseCaseTests` (mocked `ITransactionRepository`): happy path → 201, validation failure → 400, duplicate → 409
- `IngestBatchUseCaseTests`: all-valid CSV, mixed CSV (error list with correct row numbers), malformed line treated as validation error
- `GetStatsSummaryUseCaseTests`: cache hit → repo never called; cache miss → repo called and result cached

---

### 12. Integration tests — controller happy path & error cases
Using `WebApplicationFactory<Program>` with a real test PostgreSQL instance:
- `POST /ingest/transaction` → 201, 400, 409
- `POST /ingest/batch` → 200 with correct `accepted`/`rejected` counts
- `GET /customers/{id}/transactions` → pagination and filter params applied correctly
- `GET /stats/summary` → 200 with correct shape; second call served from Redis cache
