# Development

## Prerequisites

- Node 22.13+ within the 22.x line or Node 24.x (`^22.13.0 || ^24.0.0` in
  `frontend/package.json`), plus npm. No npm major version is enforced.
- .NET 9 SDK, including .NET and ASP.NET Core 9 runtimes. `global.json` permits
  stable .NET 9 feature-band updates and prevents selecting installed .NET 10.
- Git; an editor of your choice. No global npm tooling is needed.

The environment was validated with Node 24.5.0, npm 11.6.0 and .NET SDK 9.0.318.
The declared Node range accommodates the installed tools, including ESLint's
22.13.0 minimum on Node 22. Node 22 execution has not yet been verified.
TypeScript stays on 6.0.x to match the installed TypeScript ESLint peer range.
Use the tracked npm lockfile for reproducible installs.

## Install and run

From the repository root:

```sh
npm --prefix frontend ci
dotnet restore RouteWise.sln
```

Run in separate terminals:

```sh
npm --prefix frontend run dev
```

```sh
dotnet run --project backend/RouteWise.Api --launch-profile http
```

Frontend: `http://localhost:5173`. Backend: `http://localhost:5000`.
Vite reserves port 5173 (`strictPort`) and proxies `/api` to the backend without
rewriting the prefix. `http://localhost:5173/api/health` should return `Healthy`
while both servers run; the direct backend URL works too. This proxy is for
development only. Production hosting and its API routing remain a later decision.

The frontend can boot without a running backend because it makes no requests yet.
Its plain scaffold message and unknown-route fallback are intentional. Stop both
servers with Ctrl+C after use. `npm --prefix frontend run preview` serves an
existing production build locally; it does not launch the API.

## Environment convention

No environment file is required for Phases 0-1. When local values are needed, copy
`frontend/.env.example` to ignored `frontend/.env.local` (PowerShell):

```powershell
Copy-Item frontend/.env.example frontend/.env.local
```

- `VITE_API_BASE_URL=/api`: reserved for the Phase 2 fetch boundary and MSW/real
  endpoint switch. Changing it currently has no effect because there is no client.
- `VITE_GOOGLE_MAPS_API_KEY=`: leave blank until Maps integration. A browser key is
  public and must later be restricted by HTTP referrer and permitted Google APIs.

All `VITE_` values can be included in browser code; they are not secret storage.
Never put backend credentials in them. Use environment variables or .NET user
secrets for future backend secrets, not tracked appsettings files. Restart Vite
after changing environment files. Do not commit local keys or `.env` files.

## Checks

Run from the repository root:

```sh
npm --prefix frontend run typecheck
npm --prefix frontend run lint
npm --prefix frontend run build
npm --prefix frontend run format:check
dotnet restore RouteWise.sln
dotnet build RouteWise.sln
dotnet test RouteWise.sln
```

`npm --prefix frontend run format` formats frontend source/configuration. Prettier
owns formatting and ESLint owns correctness (including type-aware and React rules).
TypeScript checks browser and Vite config separately, with strict checking and no
JavaScript output. Compiler caches live under ignored `node_modules/.tmp`.

Phase 1 uses `@fontsource-variable/inter` through the global stylesheet. Font
files are bundled locally during the Vite build; no font CDN or API key is needed.
Use [Design system](DESIGN_SYSTEM.md) for token values and usage rules. The root
route remains the neutral scaffold; no component gallery or product UI is added.

The NUnit project references the API but intentionally has no test source files.
The test command should build and run discovery successfully; a "No test is
available" discovery message is expected until meaningful backend tests are added.
It does not count as a passing product test or fulfil the assignment test rubric.

## Repository workflow

Keep npm lockfile changes with dependency changes. Review `git diff` and
`git status --short` before committing; generated output and local configuration
are ignored by the root `.gitignore`. Shared VS Code settings/extensions may be
tracked if the team later needs them; user-specific files should stay local.

Phase 0 is the completed engineering foundation. Phase 1 work remains on the
existing `ina-frontend` branch for manual QA and commit by the owner.
Agree cross-team contract changes before implementation. Follow the phase
boundaries in [Frontend architecture](FRONTEND_ARCHITECTURE.md) and the approved
[Project scope](PROJECT_SCOPE.md).
