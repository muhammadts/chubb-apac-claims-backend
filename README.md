# Chubb APAC Claims Backend

Assessment prototype for motor and property claim workflows. It implements a .NET 10 modular monolith with ASP.NET Core, SQL Server, JWT bearer authentication, and an optional Kafka integration through a SQL transactional outbox. The architecture and target relational model are described in [the architecture and data-model document](docs/Claims_Backend_Architecture_and_SQL_Server_Data_Model.md). No assessment brief file was present in this repository at the time of this review; the comparison therefore uses that document and the checked-in workflow/tests.

This is an assessment prototype, not a production-ready claims platform. Demo assumptions and outstanding business decisions are listed below.

## Prerequisites

- .NET 10 SDK.
- Docker Desktop with Compose, or an existing SQL Server instance. The Compose SQL Server image is for local development.
- `sqlcmd` if creating the local database from the command line.
- A Kafka-compatible broker only if exercising event publishing. Kafka is optional; without a configured bootstrap server the publisher is disabled.
- A JWT issuer that can issue signed bearer tokens for the configured issuer and audience. The API does not include an identity provider or development token issuer.

## Local startup and database migration

From the repository root in PowerShell, start the local SQL Server and set its SA password in the current shell. Choose a strong local-only password when prompted; do not put it in tracked settings.

```powershell
$env:MSSQL_SA_PASSWORD = Read-Host "Enter a strong local SQL Server SA password"
docker compose up -d sqlserver
```

Create the `Claims` database in that instance (for example, with SQL Server Management Studio or `sqlcmd`), then set the connection used by the API:

```powershell
$env:ConnectionStrings__ClaimsDatabase = "Server=localhost,1433;Database=Claims;User Id=sa;Password=$env:MSSQL_SA_PASSWORD;Encrypt=True;TrustServerCertificate=True"
```

The migration updates an existing database; it does not provision SQL Server or create the database resource. Restore the pinned EF tool and apply the migrations:

```powershell
dotnet tool restore
$env:CLAIMS_DATABASE_CONNECTION = $env:ConnectionStrings__ClaimsDatabase
dotnet tool run dotnet-ef database update --project .\src\Chubb.Claims.Infrastructure --startup-project .\src\Chubb.Claims.Infrastructure
```

Alternatively, apply the checked-in idempotent script to an already-created database. Use the authentication mode approved for your SQL Server:

```powershell
sqlcmd -S localhost,1433 -d Claims -U sa -P $env:MSSQL_SA_PASSWORD -C -i .\database\scripts\001-initial-claims-schema-idempotent.sql
```

Configure JWT values through user secrets or environment variables. Required values are `Jwt:Issuer`, `Jwt:Audience`, and either `Jwt:Authority` or a symmetric `Jwt:SigningKey` with at least 32 UTF-8 bytes. `Jwt:ClockSkewSeconds` is optional (default 60; allowed range 0–300). Example environment variable names are `Jwt__Issuer`, `Jwt__Audience`, `Jwt__Authority`, and `Jwt__SigningKey`. Never commit actual signing keys, connection strings, or customer data. `src/Chubb.Claims.Api/appsettings.example.json` contains placeholders, not usable credentials.

Restore, build, test, and run:

```powershell
dotnet restore .\Chubb.Claims.slnx
dotnet build .\Chubb.Claims.slnx --no-restore
dotnet test .\Chubb.Claims.slnx --no-build
dotnet run --project .\src\Chubb.Claims.Api
```

The API health endpoint is `GET /health`. In Development, OpenAPI is served at `/openapi/v1.json` and Swagger UI is available at `/swagger`.

## Authentication and authorization

JWT validation checks issuer, audience, signature, and required unexpired `exp`. The policy roles are `Claimant`, `ClaimsOfficer`, and `Manager`; staff APIs also use team and market scopes from signed `team`/`teams` and `market`/`markets` claims. Claimant access is bound to the authenticated stable `sub` value. The API does not trust subject, role, team, or market values from request bodies. Staff tokens need team and market scope claims. A real identity provider and its role/scope mapping remain deployment decisions.

## API routes

All routes are under `/api/v1`. Mutating staff and claimant information-response routes require the current claim ETag in `If-Match`; successful responses return the new ETag. List endpoints use bounded keyset pagination (`pageSize` 1–100 and an opaque `cursor`).

| Method and route | Access | Purpose |
|---|---|---|
| `POST /claims` | Claimant | Submit motor or property claim; returns `201` and an ETag. |
| `GET /claims` | Claimant | List only the caller's claims. |
| `GET /claims/{claimId}` | Claimant | Read an owned claim with claimant-safe details. |
| `GET /claims/{claimId}/status` | Claimant | Read current status. |
| `GET /claims/{claimId}/timeline` | Claimant | Read claimant-visible activity history. |
| `POST /claims/{claimId}/information-requests/{requestId}/responses` | Claimant | Respond to an open request on an owned claim. |
| `GET /staff/claims` | Claims officer or manager | Search scoped work queue by market, status, team, officer, dates, or claim number. |
| `GET /staff/claims/{claimId}` | Claims officer or manager | Read an assigned claim's staff projection. |
| `POST /staff/claims/{id}/assignment` | Claims officer or manager | Assign or reassign within caller scopes. |
| `POST /staff/claims/{id}/review` | Claims officer | Start review. |
| `POST /staff/claims/{id}/information-requests` | Claims officer | Request information. |
| `POST /staff/claims/{id}/resume-review` | Claims officer | Resume once all open requests have responses. |
| `POST /staff/claims/{id}/assessments` | Claims officer | Record assessment and optional reserve estimate. |
| `POST /staff/claims/{id}/decision` | Claims officer | Record one final approval or rejection. |
| `POST /staff/claims/{id}/settlement-workflow` | Claims officer | Record settlement workflow completion; does not transfer funds. |
| `POST /staff/claims/{id}/internal-notes` | Claims officer | Add staff-only note. |
| `GET /manager/workload` | Manager | Read current workload and ageing summary. |
| `GET /manager/performance?fromUtc=…&toUtc=…` | Manager | Read decisions and settlement completion metrics for a UTC interval. |
| `GET /manager/exposure` | Manager | Read reserve estimates by market and currency. |

## Claimant-to-settlement walkthrough

This sequence uses separate tokens issued by a configured identity provider: a claimant token and a staff token with `ClaimsOfficer` role plus permitted team and market claims. Keep the `claimId`, `requestId`, and latest ETag returned by each response. Send the current ETag as `If-Match` on every mutation.

1. Submit a motor or property claim with `POST /api/v1/claims` as the claimant. Capture `claimId` and ETag.
2. As an officer, assign it with `POST /api/v1/staff/claims/{claimId}/assignment`, then start review with `POST /api/v1/staff/claims/{claimId}/review`, using the latest ETag each time.
3. Request more details with `POST /api/v1/staff/claims/{claimId}/information-requests`. The response moves the claim to `AwaitingInformation` and supplies the new ETag.
4. As the claimant, read `GET /api/v1/claims/{claimId}` to see the request and current ETag, then answer at `POST /api/v1/claims/{claimId}/information-requests/{requestId}/responses` with `If-Match`.
5. As the officer, reload `GET /api/v1/staff/claims/{claimId}`, resume review, reload, record an assessment (an optional reserve is a demo estimate), reload, and record an `Approved` decision. Every command uses the latest ETag.
6. Reload once more and call `POST /api/v1/staff/claims/{claimId}/settlement-workflow` with the latest ETag. The resulting `Settled` status means workflow completion only; no payment is made.
7. As the claimant, use `GET /api/v1/claims/{claimId}` and `GET /api/v1/claims/{claimId}/timeline` to inspect the claimant-safe result.

## Architecture and CQRS boundaries

- **Domain** contains claim lifecycle behavior and value objects. It has no ASP.NET, EF Core, SQL Server, JWT, or Kafka dependency.
- **Application** contains command/query handlers, boundary DTOs, authorization context, and ports. Commands mutate; queries return read-only projections.
- **Infrastructure** implements EF Core/SQL Server persistence, SQL transactions, query projections, outbox leasing/retry, and Kafka transport.
- **API** contains HTTP controllers, DTO validation, JWT policies, error mapping, and composition. It does not call the database or Kafka directly.

CQRS here separates command and query use cases; it does not introduce event sourcing or a second read database. Claim, workflow history, activity, and outbox rows are committed together in one SQL transaction. SQL Server `rowversion` values are returned as ETags to prevent silent lost updates. REST is used for synchronous commands and reads that need immediate validation, authorization, or results. Kafka is used for asynchronous integration notifications after a committed change, so broker availability does not decide whether a claim command commits.

## SQL schema overview

EF Core migrations create 20 assessment tables: `Markets`, `ClaimantProfiles`, `StaffProfiles`, `Teams`, `TeamMarkets`, `TeamMemberships`, `Claims`, `MotorClaimDetails`, `PropertyClaimDetails`, `ClaimAssignments`, `ClaimInformationRequests`, `ClaimInformationResponses`, `ClaimDocuments`, `ClaimAssessments`, `ClaimReserveHistory`, `ClaimDecisions`, `ClaimSettlements`, `ClaimActivities`, `ClaimNotes`, and `OutboxMessages`. Indexes support claimant histories, staff queues, reporting, and pending outbox work. `Claims.RowVersion` provides optimistic concurrency. `IdempotencyRecords` is not implemented; policy administration and document binary storage are outside scope. `database/scripts/` contains the initial schema scripts.

## Kafka outbox semantics (demo)

When `Claims:Kafka:BootstrapServers` is configured, the Infrastructure hosted publisher leases pending SQL outbox rows, publishes each event, and records acknowledgement after Kafka confirms the send. Local topic defaults are `claims.submitted.v1`, `claims.assigned.v1`, `claims.information-requested.v1`, `claims.status-changed.v1`, and `claims.decision-recorded.v1`. Start the optional local broker with `docker compose -f compose.kafka.yaml up -d` and set `Claims__Kafka__BootstrapServers=localhost:9092`.

Delivery is **at least once**, not exactly once. A process can stop after Kafka accepts a message but before SQL records the acknowledgement, so a later lease recovery can publish a duplicate. Consumers must deduplicate using stable `EventId`. Failed sends record attempt count and error, release the lease, and retry with bounded exponential backoff. Payloads are allowlisted and do not contain claim narratives, contact details, documents, or staff-only notes. Topic names and broker settings are demo defaults.

## Manager reporting definitions (demo)

- Workload and exposure include current statuses `Submitted`, `InReview`, `Assessment`, `AwaitingInformation`, and `Approved`; they exclude `Rejected` and `Settled`. Current claims are grouped by market, active team, assignee, and status. Unassigned claims are visible to managers with the matching market scope and appear under a null team.
- Ageing is elapsed UTC days since submission in bands 0–7, 8–30, 31–90, and 90+. These are reporting thresholds, not service targets.
- Performance counts decisions by decision timestamp and completed settlements by completion timestamp in `[fromUtc, toUtc)`. Elapsed duration is measured from submission. Team attribution uses the assignment active at that decision or settlement timestamp. These are operational workflow measurements, not SLAs.
- Exposure sums each included claim's current reserve estimate grouped by market and currency, and counts claims with no reserve separately. Amounts are not added across currencies and are not booked liabilities. There is no FX conversion, coverage logic, or SLA target.

## Assumptions, limitations, and next steps

Market codes, currencies, time zones, claim number format, required motor/property fields, identity provider and claim mappings, team metadata, authorization policy, reserve authority, exposure status set, event consumers, retention, privacy rules, and settlement authority need business validation. No real customer data, identity provider, policy/coverage rules, payment processing, FX conversion, or document-byte storage is included. Submission idempotency is not implemented. The prototype needs operational security, privacy, availability, retention, auditing, monitoring, backup, and recovery decisions before any production use.

Suggested next steps are to validate assumptions with the business, provision approved reference/team data and SQL Server/Kafka environments, integrate the selected identity provider, and agree on operational controls and consumer deduplication before extending the workflow.

## Assessor walkthrough and working journal

- [Assessor walkthrough outline](docs/ASSESSOR_WALKTHROUGH.md) gives a short route through the architecture and demo lifecycle.
- [Working journal](docs/WORKING_JOURNAL.md) records only work, decisions, and checks actually performed during this stage.
