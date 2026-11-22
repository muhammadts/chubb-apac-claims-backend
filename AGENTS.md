# Chubb APAC Claims Backend Instructions

Implement the backend from `docs/Claims_Backend_Architecture_and_SQL_Server_Data_Model.md` and the assessment brief. Follow its .NET 10, Clean Architecture, CQRS, SQL Server, JWT, and Kafka transactional-outbox decisions.

## Git workflow

Create a separate feature branch for each stage, from the latest `main`, after the previous stage's PR has been merged:

| Stage | Branch |
|---|---|
| 0 — Foundation | `feature/foundation` |
| 1 — Claims domain | `feature/claims-domain` |
| 2 — CQRS application | `feature/cqrs-application` |
| 3 — SQL Server infrastructure | `feature/sqlserver-infrastructure` |
| 4 — Claimant API | `feature/claimant-api` |
| 5 — Staff workflow | `feature/staff-workflow` |
| 6 — JWT authorization | `feature/jwt-auth` |
| 7 — Kafka outbox | `feature/kafka-outbox` |
| 8 — Manager reporting | `feature/manager-reporting` |
| 9 — Quality and deliverables | `feature/quality-deliverables` |

At the end of each stage, Codex stops and reports changed files, exact checks run, and a suggested commit message. You review and commit the stage manually, then push the branch and create/merge its PR. Start the next stage only after its predecessor is merged to `main`. Codex must not commit, push, open/create a PR, merge, or start another stage.


## Architecture and scope

- Keep Domain framework-free. Keep EF Core and SQL Server in Infrastructure, HTTP/JWT composition in API, and Kafka client types in Infrastructure.
- Commands mutate and queries read. Persist claim changes, activity records, and integration events together through one SQL transaction.
- Use DTOs at boundaries and enforce claimant ownership using the authenticated token subject.
- Do not add secrets, real customer data, an identity provider, market rules, coverage logic, SLA commitments, payment processing, or FX conversion. Label demo assumptions.
- Do not build a frontend, microservices, Redis, Kubernetes, or a second read database.
- Work only on the requested stage. Make the smallest coherent change, add focused automated tests for new behavior, and run relevant builds and tests.
