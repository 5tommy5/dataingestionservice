# DataIngestionService

A .NET 8 Web API for ingesting, validating, deduplicating, and querying customer transaction data.

## Quick Start

```bash
# Full stack (API + PostgreSQL + Redis)
docker compose up

# API only
dotnet run --project DataIngestionService.Api

# Tests
dotnet test
```

## Endpoints

| Method | Path | Description |
|--------|------|-------------|
| POST | `/ingest/batch` | Multipart CSV upload |
| POST | `/ingest/transaction` | Single JSON transaction |
| GET | `/customers/{id}/transactions` | Paginated transaction list |
| GET | `/stats/summary` | Aggregate stats (Redis-cached, 60s TTL) |

See [docs/prd.md](docs/prd.md) and [docs/plan.md](docs/plan.md) for full requirements and implementation plan.

---

## AI Usage

AI (Claude) was used in the following ways during this project:

- **PRD drafting** — initial product requirements document generated from a brief description of the service goals
- **Implementation plan** — high-level plan and task breakdown generated from the PRD
- **Review and corrections** — both the PRD and plan were reviewed manually; notable corrections made included: enforcing Clean Architecture layering (no EF Core / Redis leaking into Application/Domain), scoping Redis cache only to the stats summary endpoint, replacing regex-based currency validation with real ISO 4217 data via the `ISOCurrencies` NuGet package, and wiring FluentValidation explicitly in use cases rather than relying on model binding auto-validation
