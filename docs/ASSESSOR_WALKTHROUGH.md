# Assessor walkthrough outline

Target length: 8–10 minutes. Use synthetic data only and tokens from an already configured issuer.

1. **Scope and structure (1 minute):** show the solution projects and the architecture document. Explain the Domain, Application, Infrastructure, and API responsibilities and the single SQL Server write/read store.
2. **Persistence and boundaries (1 minute):** show the EF migrations/schema overview and explain `rowversion`/ETags, explicit DTOs, and atomic claim/activity/outbox persistence.
3. **Claimant submission (1 minute):** submit a synthetic motor or property claim and show `201`, claim number, status, and ETag.
4. **Staff workflow (2 minutes):** assign within staff scopes, start review, request information, and show that the claimant timeline gets only claimant-visible activity.
5. **Claimant ownership (1 minute):** respond to the open request with the claimant token, then show that an unrelated claimant cannot read or answer that claim.
6. **Decision and settlement (1 minute):** resume review, record an assessment and an explicit demo reserve estimate, record approval, and complete the settlement workflow. State clearly that settlement does not execute payment.
7. **CQRS and integration (1 minute):** identify a command and a query; explain why REST provides immediate results while Kafka carries post-commit notifications. If a broker is available, show an event and stable `EventId`; otherwise describe the test coverage without presenting it as a live integration.
8. **Manager reporting (1 minute):** review workload, performance, and exposure definitions and explain their scopes, included statuses, ageing bands, and currency grouping.
9. **Limitations (1 minute):** call out demo assumptions, missing identity provider, unimplemented idempotency/document storage/payment/coverage/FX, and service checks that could not be run.

For the exact route sequence and ETag handling, see the README's claimant-to-settlement walkthrough. Do not use real claimant data or present demo estimates as booked liabilities.
