# Chubb APAC Claims Backend Instructions

Implement the backend from `docs/Claims_Backend_Architecture_and_SQL_Server_Data_Model.md` and the assessment brief. Follow its .NET 10, Clean Architecture, CQRS, SQL Server, JWT, and Kafka transactional-outbox decisions.

## Git workflow

- Stage 0 creates `feature/claims-backend` from the latest `main`. Inspect the branch and working tree first, preserve user changes, and stop if a clean, current `main` cannot be established safely.
- Keep every implementation stage on that branch. Do not create a branch per stage.
- After each stage, stop and report changed files, exact checks and results, unfinished dependencies, and a suggested manual commit message. Wait for review and a manual commit before continuing.
- Never commit, push, open or create a PR, merge, or alter Git remotes.

## Architecture and scope

- Keep Domain framework-free. Keep EF Core and SQL Server in Infrastructure, HTTP/JWT composition in API, and Kafka client types in Infrastructure.
- Commands mutate and queries read. Persist claim changes, activity records, and integration events together through one SQL transaction.
- Use DTOs at boundaries and enforce claimant ownership using the authenticated token subject.
- Do not add secrets, real customer data, an identity provider, market rules, coverage logic, SLA commitments, payment processing, or FX conversion. Label demo assumptions.
- Do not build a frontend, microservices, Redis, Kubernetes, or a second read database.
- Work only on the requested stage. Make the smallest coherent change, add focused automated tests for new behavior, and run relevant builds and tests.
