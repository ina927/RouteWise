# RouteWise

RouteWise is a desktop-first, single-day travel itinerary optimiser. Travellers
choose places they already want to visit; the C# backend will recommend an efficient
visiting order using Simulated Annealing.

**Current phase: Phase 5, Optimised Route.** `/` redirects to `/plan` for place
selection and optimisation submission. `/plan/route` displays route order,
totals/segments, automatic re-optimisation on setting changes, intermediate-only
reordering via exact-order calculation, and local Reset to the latest successful
optimised baseline. Save journey remains disabled until Phase 6.
The C# API still exposes only `GET /api/health`; product endpoints use MSW locally.

## Stack and structure

- `frontend/`: React, TypeScript and Vite; React Router, TanStack Query and CSS Modules.
- `backend/RouteWise.Api/`: C# .NET 9 ASP.NET Core Web API shell.
- `tests/RouteWise.Tests/`: .NET 9 NUnit project referencing the API.
- `docs/`: scope, frontend direction, contract ownership and development guide.
- `RouteWise.sln`: the two .NET projects; the frontend uses npm separately.

SQLite, EF Core, LINQ and backend Google Routes integration belong to later
implementation. Frontend Maps/Places presentation is implemented; live Google
verification awaits browser-key/billing activation. Frontend development uses MSW behind an HTTP API boundary before
connecting real backend endpoints. Phase 2 contracts still await backend review.

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
For Plan search/maps, configure a Google browser key and Map ID as described in
[Development](docs/DEVELOPMENT.md). Copy `.env.example` to `.env.local` to enable
MSW for optimisation. Missing Google configuration displays an unavailable state;
MSW does not provide fake Places results or map tiles.
Visit `/api/health` at either origin to check the API while both servers run.
Stop each server with Ctrl+C.

Run the test shell with `dotnet test RouteWise.sln`. No tests are expected in
the foundation phases; successful execution does not represent assignment test coverage.

See [Development](docs/DEVELOPMENT.md) for configuration and validation commands,
[Project scope](docs/PROJECT_SCOPE.md) for product boundaries,
[Frontend architecture](docs/FRONTEND_ARCHITECTURE.md) for future organisation,
[Design system](docs/DESIGN_SYSTEM.md) for shared visual rules,
and [API contracts](docs/API_CONTRACTS.md) for the Phase 2 agreement process.
