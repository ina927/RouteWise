# RouteWise

RouteWise is a desktop-first, single-day travel itinerary optimiser. Travellers
choose places they already want to visit; the C# backend will recommend an efficient
visiting order using Simulated Annealing.

**Frontend phase: Phase 8.5, Authentication and Account. Backend: accounts,
saved journey management and sharing implemented; integration pending.**
The frontend flows below currently use MSW for product API calls.
`/` redirects to `/plan` for place
selection and optimisation submission. `/plan/route` displays route order,
totals/segments, automatic re-optimisation on setting changes, intermediate-only
reordering via exact-order calculation, and local Reset to the latest successful
optimised baseline. Save journey stores the displayed route snapshot and opens
`/journeys/:journeyId`. Details loads through the API independently of planning
memory, with owner metadata/annotation editing and shared read-only presentation.
`/saved` lists owned journeys with search/sort, rename, duplicate and confirmed
delete. Edit route reuses Plan/Optimised Route and saves the changed plan back to
the same Journey. `/shared` manages incoming invitations and accepted read-only
journeys. Owners can search users and invite them from Journey Details. Removing
a shared journey removes only the recipient relationship. `/login` and `/signup`
are public; product routes require a username/password session. `/account` displays
the username and provides logout and confirmed account deletion. Email is excluded.
The C# API exposes health, signup, login, current-user lookup, logout and account
deletion. Accounts use hashed passwords in SQLite and HttpOnly server-managed cookie
sessions. The backend also supports journey creation, owned lists, owner/shared
details, rename, duplication and deletion; username search; sending and listing
invitations; accept/decline responses; and shared journey listing and removal.

Journey PATCH supports renaming, setting/clearing dates and replacing stop notes
and planned times, and replacing the saved route plan. Replacement preserves the
journey ID, owner and invitations. Omitted fields stay unchanged, except annotations
for removed stops are dropped when replacing a plan; an empty annotation array clears
all annotations. Route replacement is transactional, so a failed save preserves the
original journey. Keep the frontend on MSW until real API integration
is verified with Ina and route optimisation/cost integration is ready with Zoe.
Repeat-invitation/reinvite rules remain provisional, and the current send-invitation
lookup does not guarantee uniqueness under concurrent requests. Journey snapshot
validation checks structure and consistency, not the authenticity of road costs.
See
[Authentication](docs/AUTHENTICATION.md) for setup, request protection and tests.

## Stack and structure

- `frontend/`: React, TypeScript and Vite; React Router, TanStack Query and CSS Modules.
- `backend/RouteWise.Api/`: C# .NET 9 ASP.NET Core Web API with EF Core/SQLite persistence.
- `tests/RouteWise.Tests/`: .NET 9 NUnit project referencing the API.
- `docs/`: scope, frontend direction, contract ownership and development guide.
- `RouteWise.sln`: the two .NET projects; the frontend uses npm separately.

SQLite/EF Core persistence models and the initial migration support the implemented
account, journey and invitation endpoints.
Backend Google Routes integration belongs to later implementation.
Frontend Maps/Places presentation is implemented; live Google
verification awaits browser-key/billing activation. Frontend development uses MSW behind an HTTP API boundary before
connecting real backend endpoints. Outstanding contract decisions include full
journey update semantics, sharing retry/reinvite policies and route provenance.

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

Open `http://localhost:5173` (unauthenticated visits go to Login). With MSW enabled,
use `river` / `routewise123` for development only; full reload resets mock auth. Vite forwards `/api` to `http://localhost:5000`.
For Plan search/maps, configure a Google browser key and Map ID as described in
[Development](docs/DEVELOPMENT.md). Copy `.env.example` to `.env.local` to enable
MSW for optimisation. Missing Google configuration displays an unavailable state;
MSW does not provide fake Places results or map tiles.
Visit `/api/health` at either origin to check the API while both servers run.
Stop each server with Ctrl+C.

Run the backend suite with `dotnet test RouteWise.sln`. The latest verified run
passed 256 tests covering persistence, accounts, journey validation/management,
user search, invitations and access isolation. Database tests use isolated test
databases, not the developer's local data. This does not verify real frontend
integration or live Google services. Route-update tests include annotation handling,
preserved shared access and rollback after a database failure.

See [Development](docs/DEVELOPMENT.md) for configuration and validation commands,
[Project scope](docs/PROJECT_SCOPE.md) for product boundaries,
[Frontend architecture](docs/FRONTEND_ARCHITECTURE.md) for future organisation,
[Design system](docs/DESIGN_SYSTEM.md) for shared visual rules,
and [API contracts](docs/API_CONTRACTS.md) for the Phase 2 agreement process.
