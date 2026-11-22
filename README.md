# Chubb APAC Claims Backend

Backend foundation for the Chubb APAC claims assessment. The architecture and initial SQL Server data model are documented in [the architecture brief](docs/Claims_Backend_Architecture_and_SQL_Server_Data_Model.md).

## Prerequisites

- .NET 10 SDK
- SQL Server for local development (SQL Server Developer or Express, or a local SQL Server instance)
- A Kafka-compatible broker when the transactional outbox publisher is enabled

## Local configuration

Copy `src/Chubb.Claims.Api/appsettings.example.json` to `src/Chubb.Claims.Api/appsettings.local.json` and replace the placeholders. This local file is loaded by the API and ignored by Git. Keep credentials and signing keys there, in user secrets, or in environment variables; never commit them. The example contains placeholders only.

The API reads `ConnectionStrings:ClaimsDatabase` for SQL Server and `Claims:Kafka:BootstrapServers` for Kafka publisher configuration. Kafka publishing is disabled when bootstrap servers are absent or left as a placeholder.

## JWT authentication and authorization

The API requires `Jwt:Issuer`, `Jwt:Audience`, and either `Jwt:Authority` (for trusted metadata and signing keys) or `Jwt:SigningKey` (a symmetric key of at least 32 UTF-8 bytes). `Jwt:ClockSkewSeconds` is optional and defaults to 60 (allowed range 0–300). JWT bearer validation checks issuer, audience, signature, and a required unexpired `exp`; missing required configuration stops API startup. Supply values through user secrets, environment variables such as `Jwt__SigningKey`, or the deployment's approved secret store. Never put real keys in tracked settings. The example file uses placeholders only.

The API policies are `Claimant`, `ClaimsOfficer`, `Staff` (officer or manager), and `Manager`. The application also checks the signed stable `sub` subject, role, team, and market scopes for each use case. Claimants can access only claims whose stored claimant subject matches `sub`. Staff must carry `team`/`teams` and `market`/`markets` claims whose values grant the relevant scope. Manager reports pass both scopes to the query store.

No identity provider is selected or token issuer included. A future external provider must issue signed access tokens for this API's configured audience, with the configured issuer, a stable non-reassignable subject in `sub`, and role values `Claimant`, `ClaimsOfficer`, or `Manager`. Staff tokens must also carry authorized team and market scope claims. Validate the actual provider's role mapping and scope lifecycle before deployment; do not trust client-supplied headers or request fields as identity. The test authentication handler is compiled only into the test project.

## SQL Server persistence

Infrastructure uses EF Core 10 with Microsoft SQL Server. The initial EF migration creates exactly 20 assessment tables: the 19 claim/reference/workflow tables plus `OutboxMessages`. `IdempotencyRecords` is not included because submission idempotency has not been implemented. Policy administration and persisted reporting tables are outside this scope; `Claims.PolicyNumber` remains an external reference. Claim documents contain metadata and an object storage key only.

For local development, Docker Compose starts a SQL Server Developer instance. Set a strong local password in the shell; do not put it in a tracked file:

```powershell
$env:MSSQL_SA_PASSWORD = Read-Host "Enter a strong local SQL Server SA password"
docker compose up -d sqlserver
```

Create the local `Claims` database separately in that SQL Server instance, then configure the API connection in the current shell:

```powershell
$env:ConnectionStrings__ClaimsDatabase = "Server=localhost,1433;Database=Claims;User Id=sa;Password=$env:MSSQL_SA_PASSWORD;Encrypt=True;TrustServerCertificate=True"
```

On provisioned SQL Server, set `ConnectionStrings__ClaimsDatabase` with that server's host, existing database name, approved authentication mode, encryption, and trust settings through environment variables, user secrets, or an approved secret store. Do not put credentials in `appsettings.json` or commit them.

The migration changes schema inside an existing database. It does not provision a SQL Server instance or a database resource. Two scripts generated from the EF migration are checked in under `database/scripts/`:

- `001-initial-claims-schema.sql` applies the migration from an empty database.
- `001-initial-claims-schema-idempotent.sql` checks EF's migration history and is suitable for a separately provisioned SQL Server database.

Apply the idempotent script with `sqlcmd -S <server> -d <existing-database> -E -i database/scripts/001-initial-claims-schema-idempotent.sql` for integrated authentication, or use the authentication options approved for that server. To regenerate scripts after an EF migration, set `CLAIMS_DATABASE_CONNECTION` in the current shell, then run `dotnet tool restore` and `dotnet tool run dotnet-ef migrations script 0 --project src/Chubb.Claims.Infrastructure --startup-project src/Chubb.Claims.Infrastructure --output database/scripts/001-initial-claims-schema.sql` followed by the same command with `--idempotent` and the idempotent output path. The tool manifest pins EF tooling, and the design-time factory reads only `CLAIMS_DATABASE_CONNECTION`.

Before claim submission is enabled in a deployment, provision the approved `Markets` reference rows and the relevant teams/staff metadata. Market codes, currencies, time zones, and market rules are business inputs and are deliberately not seeded here. Claims, workflow history, activities, and outbox messages are saved in one SQL transaction with rowversion concurrency.

## Build and run

```powershell
dotnet restore .\Chubb.Claims.slnx
dotnet build .\Chubb.Claims.slnx --no-restore
dotnet test .\Chubb.Claims.slnx --no-build
dotnet run --project .\src\Chubb.Claims.Api
```

The API exposes `GET /health`. In Development it also serves the generated OpenAPI document at `/openapi/v1.json`.
# Kafka transactional outbox (demo)

When `Claims:Kafka:BootstrapServers` is configured, the Infrastructure hosted publisher leases pending SQL Server outbox rows, sends each event to its configured topic, then marks it published after Kafka acknowledges the send. Local topics default to `claims.submitted.v1`, `claims.assigned.v1`, `claims.information-requested.v1`, `claims.status-changed.v1`, and `claims.decision-recorded.v1`; override them with `Claims:Kafka:Topics` configuration. The standalone local Compose broker listens at `localhost:9092`; start it with `docker compose -f compose.kafka.yaml up -d` and set `Claims__Kafka__BootstrapServers=localhost:9092`.

This is at-least-once delivery. A process can stop after Kafka accepts an event but before SQL records the acknowledgement, so consumers must deduplicate by the envelope's stable `EventId`. No distributed transaction or exactly-once guarantee is used. Failed sends increment `AttemptCount`, record `LastError`, clear the lease, and retry with bounded exponential backoff. Expired leases are eligible for recovery by the next publisher. Event payloads are allowlisted and exclude claim narratives, contact details, document content, credentials, and internal notes. These topic names and the local broker settings are demo defaults.

## Manager reporting dashboard (demo)

The read-only manager endpoints are `GET /api/v1/manager/workload`, `/performance?fromUtc=...&toUtc=...`, and `/exposure`. The Manager authorization policy protects these endpoints, and application handlers also require a Manager role plus non-empty team and market scopes. Queries use SQL projections over the claims database; they do not change claims or own a second copy of claim data.

The dashboard defines current claims grouped by market, team, assignee, and status; it reports the count with no assigned officer. It includes `Submitted`, `InReview`, `Assessment`, `AwaitingInformation`, and `Approved`; it excludes `Rejected` and `Settled`. Claims with no current team assignment appear under a null team for managers scoped to that market. Ageing is elapsed UTC days since submission in demo bands 0-7, 8-30, 31-90, and 90+; these are reporting thresholds, not service targets.

Performance reports decision and completed-settlement counts and elapsed days from submission, within a selected UTC interval whose start is inclusive and end exclusive. Decision counts include final decisions recorded in the interval. Settlement counts include completed settlements in the interval. Durations are operational workflow measurements, not SLA commitments.

Reserve exposure is an operational estimate based on the current reserve estimate for claims in the included statuses. It counts claims with no reserve separately, and groups reserve totals by market and currency. Currency totals are never combined. Estimates are not booked liabilities; the dashboard applies no FX rates, coverage rules, or SLA targets. Status and ageing choices above are demo assumptions.
