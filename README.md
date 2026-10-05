# RouteWise

RouteWise is a desktop-first, single-day travel itinerary optimiser. Travellers
choose places they already want to visit; the C# backend will recommend an efficient
visiting order using Simulated Annealing.

**Current phase: Phase 1, design system foundation.** Phase 0 engineering remains
unchanged. The frontend has shared visual tokens and a local Inter font, and still
displays a neutral scaffold message. The API exposes only `GET /api/health`.
No product components, features or meaningful tests exist yet.

## Stack and structure

- `frontend/`: React, TypeScript and Vite; React Router, TanStack Query and CSS Modules.
- `backend/RouteWise.Api/`: C# .NET 9 ASP.NET Core Web API shell.
- `tests/RouteWise.Tests/`: .NET 9 NUnit project referencing the API.
- `docs/`: scope, frontend direction, contract ownership and development guide.
- `RouteWise.sln`: the two .NET projects; the frontend uses npm separately.

SQLite, EF Core, LINQ and Google Maps / Places / Routes integration belong to later
implementation. Frontend development will initially use MSW mocks behind an HTTP
API boundary before connecting real backend endpoints. Contracts and mocks begin
in Phase 2.

## Team

| Owner | Responsibility |
| --- | --- |
| Ina | Frontend/product UI, frontend architecture, data needs and mock-first UI |
| Zoe | C# route optimisation, route-cost integration, recalculation and related tests |
| Nam | Persistence, accounts, saved journeys, sharing/invitations and related tests |

Ina defines frontend-facing contracts in Phase 2. Zoe and Nam review their areas
before backend implementation and integration.

## Run locally

Use Node 22.13+ within the 22.x line or Node 24.x, npm, and the .NET 9 SDK with its
ASP.NET Core runtime. `global.json` selects a stable installed .NET 9 SDK.
See [Development](docs/DEVELOPMENT.md) for the verified environment.

From the repository root, in one terminal:

```sh
cd frontend
npm ci
npm run dev
```

In a second terminal, from the repository root:

```sh
dotnet restore RouteWise.sln
dotnet build RouteWise.sln
dotnet run --project backend/RouteWise.Api --launch-profile http
```

Open `http://localhost:5173`. Vite forwards `/api` to `http://localhost:5000`.
Visit `/api/health` at either origin to check the API while both servers run.
Stop each server with Ctrl+C.

Run the test shell with `dotnet test RouteWise.sln`. No tests are expected in
the foundation phases; successful execution does not represent assignment test coverage.

See [Development](docs/DEVELOPMENT.md) for configuration and validation commands,
[Project scope](docs/PROJECT_SCOPE.md) for product boundaries,
[Frontend architecture](docs/FRONTEND_ARCHITECTURE.md) for future organisation,
[Design system](docs/DESIGN_SYSTEM.md) for shared visual rules,
and [API contracts](docs/API_CONTRACTS.md) for the Phase 2 agreement process.
