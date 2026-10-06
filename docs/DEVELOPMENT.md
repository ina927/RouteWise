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

The frontend can boot without a backend; enable MSW below for product API calls.
The Plan page requires Google configuration for live search/maps. Stop both
servers with Ctrl+C after use. `npm --prefix frontend run preview` serves an
existing production build locally; it does not launch the API.

## Environment convention

For local configuration, copy
`frontend/.env.example` to ignored `frontend/.env.local` (PowerShell):

```powershell
Copy-Item frontend/.env.example frontend/.env.local
```

- `VITE_API_BASE_URL=/api`: base URL used by the frontend API modules.
- `VITE_ENABLE_MOCKS=true`: intercept product HTTP calls in development only;
  missing/false calls real endpoints. MSW does not mock Google search/maps.
- `VITE_GOOGLE_MAPS_API_KEY=`: browser key for Maps JavaScript API and Places API
  (New). Enable both APIs and billing on the Google Cloud project; restrict the
  key by permitted APIs and your local/deployed HTTP referrers. Keep the actual
  key only in ignored `.env.local` or the deployment environment.
- `VITE_GOOGLE_MAP_ID=`: a JavaScript Map ID for Advanced Markers. A configured ID
  is used as-is. Development alone falls back to `DEMO_MAP_ID` when blank.
  Production rejects a missing ID or `DEMO_MAP_ID`; there is no production demo
  fallback. Missing configuration keeps the shell usable with unavailable states.

The Google adapter uses the supported [async script loader and importLibrary](https://developers.google.com/maps/documentation/javascript/load-maps-js-api),
[Place Autocomplete Data API](https://developers.google.com/maps/documentation/javascript/place-autocomplete-data)
with session tokens and `Place.fetchFields`, and [Advanced Markers](https://developers.google.com/maps/documentation/javascript/advanced-markers/start).
No npm loader or map wrapper is installed. It requests only id, displayName,
formattedAddress and location, then converts to the existing RouteWise Place.
No country, location, radius or map-viewport bias is configured; no geolocation
permission is requested. Suggestions include Google Maps attribution.

Script/network timeout and Google authentication failures surface restrained
unavailable states; place request failures stay inline. After correcting key/API
permissions, reload the page to retry initialization. A full reload clears the
planning draft and retained route results by design. Live Google success needs valid
project credentials; controlled browser test responses do not verify that setup.

Phase 5 consumes route order, metrics and geometry through the existing RouteWise
API/MSW boundary. MSW geometry is development data, not navigable road geometry.
The Google adapter presents `RouteResult.segments[].path` with Polyline and
[static dashed symbols](https://developers.google.com/maps/documentation/javascript/examples/overlay-symbol-dashed)
for the selected segment. It does not request Google Routes or calculate costs.

Frontend/Ina owns the browser Maps JavaScript/Places key and configuration.
Backend/Zoe separately owns Google Routes and route-cost configuration. No shared
Google account, project, billing or key is assumed. Live frontend Google testing
is pending billing activation; this does not block MSW-based route development.
Real tiles, Advanced Markers, route polylines and dashed appearance still require
live verification. Temporary SDK doubles verify wiring only and are not shipped.

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
route redirects to Plan; there is no component gallery. `/plan/route` renders the
Optimised Route UI only with an in-memory successful result. A fresh direct visit
redirects to Plan. Save journey is disabled pending Phase 6.

The NUnit project references the API but intentionally has no test source files.
The test command should build and run discovery successfully; a "No test is
available" discovery message is expected until meaningful backend tests are added.
It does not count as a passing product test or fulfil the assignment test rubric.

## Repository workflow

Keep npm lockfile changes with dependency changes. Review `git diff` and
`git status --short` before committing; generated output and local configuration
are ignored by the root `.gitignore`. Shared VS Code settings/extensions may be
tracked if the team later needs them; user-specific files should stay local.

Phases 0-4 are frozen foundations; Phase 5 remains on the existing `ina-frontend`
branch for manual QA and commit by the owner.
Agree cross-team contract changes before implementation. Follow the phase
boundaries in [Frontend architecture](FRONTEND_ARCHITECTURE.md) and the approved
[Project scope](PROJECT_SCOPE.md).
