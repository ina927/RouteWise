# RouteWise

RouteWise helps travellers plan a day out by finding an efficient order for the
places they want to visit. It combines Google Maps and Places with Google Routes
travel data and a C# Simulated Annealing optimiser.

## Features

- Plan walking or driving journeys and optimise for distance or travel time.
- Choose an open end, a fixed destination, or a return to the starting point.
- Select up to 10 locations, including the start and any separate end; returning
  to the start does not use another location slot.
- Reorder intermediate stops, compare the result, and reset to the optimised route.
- Save journeys with dates, stop notes and planned times; search, edit, rename,
  duplicate or delete saved journeys.
- Invite other users by username to view a saved journey. Recipients can accept,
  decline or remove it, and owners can re-invite recipients who removed it.
- Sign up and log in with a username and password.

## Technology

- **Frontend:** React, TypeScript, Vite, React Router, TanStack Query and CSS Modules.
- **Backend:** C# / ASP.NET Core targeting .NET 9, Entity Framework Core and SQLite.
- **Maps and routing:** Google Maps JavaScript API, Places API (New), Routes API
  and a custom Simulated Annealing algorithm.
- **Testing:** NUnit and ASP.NET Core integration tests; MSW for optional frontend mocks.

## Requirements

- Node.js **22.13 or later in the 22.x line**, or **24.x**, with npm.
- **.NET 9 SDK**, including the .NET and ASP.NET Core 9 runtimes.
- A Google Cloud project with billing enabled and the APIs listed below enabled.
  Live map, place-search and route requests require internet access and may incur
  Google API charges.

## Setup

Start in the repository root. The commands below use the checked-in npm lockfile
and local EF Core tool manifest.

```sh
npm --prefix frontend ci
dotnet restore RouteWise.sln
dotnet tool restore
```

### Frontend Google key and real API connection

Copy `frontend/.env.example` to `frontend/.env.local`, then use these values,
replacing the browser-key placeholder:

```dotenv
VITE_API_BASE_URL=/api
VITE_ENABLE_MOCKS=false
VITE_USE_REAL_BACKEND=true
ROUTEWISE_API_PROXY_TARGET=http://localhost:5000
VITE_GOOGLE_MAPS_API_KEY=YOUR_BROWSER_GOOGLE_MAPS_KEY
VITE_GOOGLE_MAP_ID=
```

Enable **Maps JavaScript API** and **Places API (New)** for this browser key.
Restrict it to those APIs and the local website referrer `http://localhost:5173/*`.
For local development, a blank Map ID uses Google's demo Map ID. To run a
production build, provide your own JavaScript Map ID before building.

The example file enables MSW by default, so explicitly set
`VITE_ENABLE_MOCKS=false` to send all application requests to the real backend.
With mocks off, `VITE_USE_REAL_BACKEND` is ignored. Keep the relative `/api` URL
so the Vite proxy can forward requests and session cookies to the backend.
Restart Vite and reload the browser after changing these settings.

### Backend Google Routes key

Enable **Routes API** for a separate server key and restrict that key to Routes
API. Store it in .NET User Secrets, using the configuration name expected by the
backend:

```sh
dotnet user-secrets set "GoogleMaps:ApiKey" "YOUR_BACKEND_GOOGLE_ROUTES_KEY" --project backend/RouteWise.Api
```

The HTTP launch profile uses the Development environment, which loads User
Secrets. The environment variable `GoogleMaps__ApiKey` is an alternative.
Never put the backend key in a `VITE_` variable or commit either key. Browser
configuration is visible to users; `frontend/.env.local` is ignored by Git.

### Initialise SQLite

Apply the existing migration from the API directory:

```sh
cd backend/RouteWise.Api
dotnet ef database update
cd ../..
```

This creates `backend/RouteWise.Api/routewise.db` with the required tables. No
separate database server or sample-data import is needed. Use the same API
working directory when running the backend so it opens the same database.
The app does not apply migrations automatically. If you override
`ConnectionStrings:RouteWise`, use the same value for migration and runtime.

## Run locally

Open two terminals, both initially in the repository root.

**Backend:**

```sh
cd backend/RouteWise.Api
dotnet run --launch-profile http
```

**Frontend:**

```sh
npm --prefix frontend run dev
```

- Open the app at **http://localhost:5173**.
- The backend listens at **http://localhost:5000**.
- **http://localhost:5173/api/health** should return `Healthy` when both servers
  and the proxy are working; **http://localhost:5000/api/health** checks the API directly.

Use `localhost` consistently, as the default development origin checks expect
it. Stop each server with Ctrl+C. Restart the backend after changing C# code;
restarting it clears active login sessions but keeps saved data.

## Try the app

1. **Signup:** create an account with a username and password.
2. **Plan:** choose walking or driving, an optimisation objective, a starting
   place and destinations from the search results, then choose an end mode.
3. **Optimise:** select **Find best route** to view the route and travel totals.
   Drag intermediate stops to change their order and compare the result.
4. **Save:** give the journey a name and optional date, then add notes or planned
   times in its details. Saved journeys remain available after a refresh.
5. **Share:** open the saved journey and select **Share**, then search for another
   registered username and invite them. Use a second browser or private window
   for the recipient to accept the invitation and view the journey without editing it.

## Build and test

From the repository root, with the development API stopped to avoid locked build files:

```sh
dotnet build RouteWise.sln
dotnet test RouteWise.sln
npm --prefix frontend run typecheck
npm --prefix frontend run lint
npm --prefix frontend run build
```

NUnit tests use isolated test databases and fake Google responses; they do not
require live Google keys or modify the application's database. The frontend build
is written to `frontend/dist`. The Vite `/api` proxy is a development-server setting;
for the local assessment workflow, use `npm --prefix frontend run dev` as above.

If PowerShell blocks `npm.ps1`, use `npm.cmd` in place of `npm`.
If SQLite reports missing tables, reapply the migration from the API directory.
For map or routing failures, check the relevant Google key, enabled APIs, billing
and key restrictions. `ECONNREFUSED` from Vite means the API is not reachable at
the configured proxy address.

## Team contributions

| Team member | Main contributions |
| --- | --- |
| Ina | React frontend, UI and interaction design, Google Maps/Places integration, input validation, and frontend API integration. |
| Zoe | C# Simulated Annealing optimiser, Google Routes integration, route calculation and recalculation, and routing tests. |
| Nam | EF Core/SQLite persistence, authentication, saved journey management, sharing and access control, and related backend tests. |
