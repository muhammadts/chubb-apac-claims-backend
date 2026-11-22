# Chubb APAC Claims Backend

Backend foundation for the Chubb APAC claims assessment. The architecture and initial SQL Server data model are documented in [the architecture brief](docs/Claims_Backend_Architecture_and_SQL_Server_Data_Model.md).

## Prerequisites

- .NET 10 SDK
- SQL Server for local development (SQL Server Developer or Express, or a local SQL Server instance)
- A Kafka-compatible broker for later outbox publisher work; this foundation installs the client package but does not connect to a broker

## Local configuration

Copy `src/Chubb.Claims.Api/appsettings.example.json` to `src/Chubb.Claims.Api/appsettings.local.json` and replace the placeholders. This local file is loaded by the API and ignored by Git. Keep credentials and signing keys there, in user secrets, or in environment variables; never commit them. The example contains placeholders only.

The API reads `ConnectionStrings:ClaimsDatabase` for SQL Server and `Claims:Kafka:BootstrapServers` for future publisher configuration. Kafka is intentionally not registered or contacted in this foundation stage.

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
