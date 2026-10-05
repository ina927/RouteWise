# API contracts: deferred to Phase 2

There are no RouteWise request/response schemas, DTOs, domain models or product
endpoints in Phase 0. This document records ownership and process only.

- **Ina** defines frontend-required data and request/response needs.
- **Zoe** reviews route/optimisation contracts.
- **Nam** reviews journey/account/sharing contracts.
- Discuss changes before backend implementation. Freeze the agreed contracts
  before parallel frontend/backend integration.

Phase 2 will record the agreed cross-team touchpoints and provide matching MSW
HTTP behaviour. Mock and real responses must satisfy the same agreed contracts.
Zoe and Nam retain ownership of backend implementation details; frontend needs
do not prescribe their schema or internal architecture.

`GET /api/health` is an infrastructure-only ASP.NET health check returning plain
text `Healthy` on success. It does not define a product response convention.
