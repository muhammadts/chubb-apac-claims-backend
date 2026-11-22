# Chubb APAC Claims Backend

Backend foundation for the Chubb APAC claims assessment. The architecture and initial SQL Server data model are documented in [the architecture brief](docs/Claims_Backend_Architecture_and_SQL_Server_Data_Model.md).

## Prerequisites

- .NET 10 SDK
- SQL Server for local development (SQL Server Developer or Express, or a local SQL Server instance)
- A Kafka-compatible broker for later outbox publisher work; this foundation installs the client package but does not connect to a broker

## Local configuration

Copy `src/Chubb.Claims.Api/appsettings.example.json` to `src/Chubb.Claims.Api/appsettings.local.json` and replace the placeholders. This local file is loaded by the API and ignored by Git. Keep credentials and signing keys there, in user secrets, or in environment variables; never commit them. The example contains placeholders only.

The API reads `ConnectionStrings:ClaimsDatabase` for SQL Server and `Claims:Kafka:BootstrapServers` for future publisher configuration. Kafka is intentionally not registered or contacted in this foundation stage.

## Build and run

```powershell
dotnet restore .\Chubb.Claims.slnx
dotnet build .\Chubb.Claims.slnx --no-restore
dotnet test .\Chubb.Claims.slnx --no-build
dotnet run --project .\src\Chubb.Claims.Api
```

The API exposes `GET /health`. In Development it also serves the generated OpenAPI document at `/openapi/v1.json`.
