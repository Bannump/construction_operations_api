# Construction Operations API

A controller-based ASP.NET Core 8 API for construction project budgets, labor and expenses. Uses EF Core 8 and SQL Server 2022, with decimal arithmetic, async persistence, service interfaces, automated tests and Docker Compose.

## Architecture

```text
HTTP controllers → Application services and rules → Store interfaces
                                                   ↑
                                         EF Core / SQL Server
```

- `src/ConstructionOps.Api`: REST controllers, dependency injection, OpenAPI, exception handling and structured JSON logs.
- `src/ConstructionOps.Application`: entities, DTOs, business rules and service/store interfaces. No dependency on ASP.NET or EF Core.
- `src/ConstructionOps.Infrastructure`: EF mappings, stores, migrations and opt-in seed data.
- `tests/ConstructionOps.UnitTests`: isolated service and business rule tests.
- `tests/ConstructionOps.IntegrationTests`: WebApplicationFactory HTTP tests and a separate SQL Server suite.

Controllers delegate work to services. Services validate business inputs and call stores; stores use async EF Core methods and pass cancellation tokens. DTOs keep persistence navigation properties out of API responses. A single DbContext per request is the unit of work. No generic repository, messaging or distributed services are needed.

Budget queries project SQL aggregates without loading labor/expense collections. The cross-project report materializes one aggregate row per project, then uses LINQ to sum those rows; this deliberately simple implementation suits a portfolio-sized workload. For very large portfolios, move the final rollup into a SQL aggregate. SQL Server `rowversion` prevents concurrent status writes from silently overwriting each other; conflicts return 409.

## Local setup

Requirements: .NET 8 SDK (8.0.4xx), Docker Engine/Desktop with Compose v2, and an x86-64 host capable of running SQL Server 2022 with at least 2 GB available memory. SQL Server's Linux image is not natively supported on ARM; use a supported x86-64 SQL Server instance when needed.

### Run both services with Docker

```bash
cp .env.example .env
# Edit .env and replace the password. Do not commit .env.
docker compose up --build -d
docker compose logs api
curl --fail http://localhost:8080/health
```

The API listens on port 8080. Open `/swagger` on that port for the interactive OpenAPI UI, or `/swagger/v1/swagger.json` for the document. Compose waits for SQL Server's query-based health check before starting the API. The API applies migrations and seeds a sample project on startup in this local configuration. Data persists in `sql-data`.

```bash
docker compose down         # Stop services; preserve data
# docker compose down -v    # Optional: permanently delete local database data
```

### Run the API with the .NET SDK

Start SQL Server via Compose, then supply the connection string to the process. The following are Bash commands, run from the repository root:

```bash
docker compose up -d sqlserver
# .env is trusted local configuration that you created above.
set -a; source .env; set +a
export ConnectionStrings__Operations="Server=localhost,1433;Database=ConstructionOps;User Id=sa;Password=${SQLSERVER_SA_PASSWORD};Encrypt=True;TrustServerCertificate=True"
dotnet restore
dotnet tool restore
dotnet ef database update --project src/ConstructionOps.Infrastructure --startup-project src/ConstructionOps.Api
ASPNETCORE_ENVIRONMENT=Development Database__MigrateOnStartup=true Database__Seed=true \
  dotnet run --project src/ConstructionOps.Api --urls http://localhost:8080
```

PowerShell users can set variables with `$env:ConnectionStrings__Operations = '...'` and `$env:ASPNETCORE_ENVIRONMENT = 'Development'`. Use the same connection-string and startup flags as above, then run the `dotnet` commands individually.

Normal SDK startup does not migrate or seed unless explicitly enabled. The connection string is required at runtime; there is no committed database password. For a managed deployment, apply reviewed migrations separately and disable startup migration/seeding. `TrustServerCertificate=True` is for the local SQL Server certificate; configure a trusted certificate for a deployment.

### Migrations

The initial migration and model snapshot are committed. Schema includes SQL Server foreign keys with restricted deletion, indexes on project status/start date and cost project/date, an expense category index, decimal precision, check constraints and project rowversion.

```bash
dotnet tool restore
# ConnectionStrings__Operations should be set, as above.
dotnet ef migrations add YourMigration --project src/ConstructionOps.Infrastructure --startup-project src/ConstructionOps.Api
dotnet ef database update --project src/ConstructionOps.Infrastructure --startup-project src/ConstructionOps.Api
dotnet ef migrations script --idempotent --project src/ConstructionOps.Infrastructure --startup-project src/ConstructionOps.Api
```

Seed data is opt-in (`Database__Seed=true` with `Database__MigrateOnStartup=true`), inserted atomically and safe to repeat in a single-instance local startup. It creates `Riverside Bridge` with budget 250,000, labor cost 340 and expenses 4,500. Its ID is `11111111-1111-1111-1111-111111111111`.

## API

| Method | Path | Behavior |
|---|---|---|
| POST | `/api/projects` | Create a Planned project; 201 and Location |
| GET | `/api/projects/{id}` | Project details |
| GET | `/api/projects?status=Active&page=1&pageSize=20` | Filtered, paginated list; max page size 100 |
| PATCH | `/api/projects/{id}/status` | Update status |
| POST | `/api/projects/{id}/labor` | Add labor; 201 with calculated cost |
| POST | `/api/projects/{id}/expenses` | Add categorized expense; 201 |
| GET | `/api/projects/{id}/budget` | Budget, labor, expenses, total spent, remaining |
| GET | `/api/reports/financials?status=Active` | Cross-project totals; status is optional |
| GET | `/health` | Database connectivity; 200 or 503 |

Enums use strings in JSON. Expense categories: `Materials`, `Equipment`, `Permits`, `Subcontractors`, `Other`.

```bash
curl -i -X POST http://localhost:8080/api/projects \
  -H 'Content-Type: application/json' \
  -d '{"name":"North Bridge","approvedBudget":150000.00,"startDate":"2026-01-01","endDate":"2026-12-31"}'

# Copy the returned id into this variable:
PROJECT_ID=11111111-1111-1111-1111-111111111111
curl http://localhost:8080/api/projects/$PROJECT_ID
curl -X PATCH http://localhost:8080/api/projects/$PROJECT_ID/status \
  -H 'Content-Type: application/json' -d '{"status":"Active"}'
curl -X POST http://localhost:8080/api/projects/$PROJECT_ID/labor \
  -H 'Content-Type: application/json' \
  -d '{"workerName":"Jordan Lee","hours":7.5,"hourlyRate":40.00,"workDate":"2026-02-01"}'
curl -X POST http://localhost:8080/api/projects/$PROJECT_ID/expenses \
  -H 'Content-Type: application/json' \
  -d '{"description":"Concrete delivery","amount":2500.00,"category":"Materials","incurredDate":"2026-02-01"}'
curl http://localhost:8080/api/projects/$PROJECT_ID/budget
curl http://localhost:8080/api/reports/financials
```

For the newly created 150,000-budget project after those entries:

```json
{
  "projectId": "<returned-id>",
  "name": "North Bridge",
  "approvedBudget": 150000.00,
  "laborCost": 300.00,
  "expenses": 2500.00,
  "totalSpent": 2800.00,
  "remainingBudget": 147200.00
}
```

Replace the example ID with the newly returned ID to reproduce that result. The seeded project starts Active, so attempting to set it Active again returns 409.

## Business rules and errors

- New projects start `Planned`. Allowed transitions: `Planned → Active`, `Planned → Cancelled`, `Active → Completed`, `Active → Cancelled`. Terminal states and repeated transitions return 409.
- Names/descriptions must be nonblank and within length limits. All request fields are required.
- Budget, hourly rate and expenses must be nonnegative with at most two decimal places, up to 999,999,999,999.99. Hours must be greater than zero and at most 24, with up to two decimal places.
- Dates are ISO calendar dates (`YYYY-MM-DD`). End date cannot precede start date. Cost entries must fall within the project's inclusive date range. Dates are not converted between time zones.
- Labor cost is `hours × hourlyRate`, rounded per entry to two decimal places, midpoint away from zero. All financial calculations use `decimal`. Reports sum the rounded entries. Empty projects and empty reports return zero costs.
- Spending can exceed the approved budget; remaining budget becomes negative. Costs can be recorded in any project status to support late accounting entries; dates must still fall within the project range. No cumulative daily-hours rule is implied.
- Invalid inputs return 400, missing projects 404, conflicting status writes 409, and unexpected errors 500 with generic details. Error bodies use Problem Details and include a trace ID. Malformed GUID paths do not match the route and return 404; the all-zero GUID returns 400.

Logs include operation/project identifiers without logging request bodies. This portfolio API intentionally has no authentication or authorization; add identity, access controls and an appropriate deployment configuration before exposing business data publicly.

## Testing

From the repository root:

```bash
dotnet restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
```

The default run executes unit tests and HTTP integration tests using EF Core's in-memory provider. SQL Server tests are **reported as skipped** unless `TEST_SQLSERVER_CONNECTION_STRING` is supplied. The in-memory tests do not establish relational constraints, SQL translation, precision or rowversion behavior.

To run all tests including SQL Server:

```bash
docker compose up -d --wait sqlserver
set -a; source .env; set +a
export TEST_SQLSERVER_CONNECTION_STRING="Server=localhost,1433;User Id=sa;Password=${SQLSERVER_SA_PASSWORD};Encrypt=True;TrustServerCertificate=True"
dotnet test --configuration Release
# Or select only SQL Server tests:
dotnet test --configuration Release --filter 'Category=SqlServer'
```

Use a disposable SQL Server instance. Each SQL test creates a uniquely named `ConstructionOps_Test_<guid>` database, applies real migrations, and deletes its database on disposal. The login needs permission to create/drop these databases. A configured but unreachable database fails tests; it does not silently skip them. Abruptly terminated runs can leave test databases to remove manually.

Coverage includes transitions, validation, per-entry rounding, overspending, API creation/retrieval, cost/report workflows, HTTP error contracts and filtering. The six SQL Server tests cover the full HTTP financial workflow, empty reports, foreign keys, check constraints, rowversion conflicts, migrations, repeatable seed data, indexes and decimal scale. GitHub Actions builds Release and runs all tests against a SQL Server service, preserving TRX results.

### Validation in the implementation workspace

Validated in this workspace:

- Release build: passed with zero warnings and zero errors.
- 39 unit tests: passed.
- 11 HTTP integration tests and one SQL Server query translation/model consistency check: passed.
- Six SQL Server execution tests: skipped; not verified.
- Compose configuration and idempotent migration script generation: passed.

 SQL Server container validation was blocked here: `docker pull mcr.microsoft.com/mssql/server:2022-latest` returned `Forbidden` for image layer downloads, which redirect to `centralus.data.mcr.microsoft.com`. The SQL Server tests and Compose startup must be run on your local Docker host (or CI) before treating database-specific behavior as verified. No cloud connection or publication is required for local development.
