# Test saving an edited journey

This development mode uses the real C# API for accounts, journeys and sharing.
Only route calculation uses MSW fixtures. Costs and geometry are simulated.
Google Maps and new place searches still need a Google browser key; the sample
journey below lets you edit existing stops and settings without one.

## Start the backend

From the repository root:

```sh
cd backend/RouteWise.Api
dotnet ef database update --connection 'Data Source=integration-test.db'
ASPNETCORE_ENVIRONMENT=Development ConnectionStrings__RouteWise='Data Source=integration-test.db' dotnet run --no-launch-profile --urls http://localhost:5001
```

Leave this terminal running. This uses a separate ignored SQLite file,
`backend/RouteWise.Api/integration-test.db`. Port 5001 avoids the macOS service on 5000.

## Start the frontend

The prepared `frontend/.env.local` contains:

```dotenv
VITE_API_BASE_URL=/api
VITE_ENABLE_MOCKS=true
VITE_USE_REAL_BACKEND=true
ROUTEWISE_API_PROXY_TARGET=http://localhost:5001
```

In a second terminal, from the repository root:

```sh
cd frontend
npm run dev
```

If Vite is already running on 5173, use that instance. Restart after editing
environment settings. Keep the relative `/api` URL so login cookies use the proxy.

## Prepare a sample

In a third terminal, from the repository root:

```sh
cd frontend
node --experimental-strip-types scripts/prepare-integration.mjs
```

The script creates a new test account and a three-stop journey through the real
API, reads the journey back, and prints login credentials and a journey link.
Every run creates a fresh account. These are local test records, not mock records.

## Test in the browser

1. Open http://localhost:5173 and log in with the printed credentials. Log out of
   any previous session first.
2. Open **Saved journeys**, then **Nam integration test**. Record the journey URL.
3. Return to **Saved journeys**, open the journey card's **Actions** menu (three
   dots), and choose **Edit route**. Keep the existing places and change Walking to Driving.
4. Click **Find best route**, then click **Save changes**.
5. Confirm the journey URL is unchanged and the travel mode is Driving.
6. Refresh, then reopen it from Saved journeys. The change and the start note
   should remain, with one journey in the list.
7. Edit again, change the mode, but leave without saving. Reopen the saved journey;
   the previously saved mode should remain.

A map-unavailable message is expected without a Google key. Do not clear the
preloaded places, since selecting new ones requires Google Places.

For extra evidence, open browser developer tools → Network. Saving should send
`PATCH /api/journeys/{same-id}` with a `plan` body and return HTTP 200. Reloading
should send GET for that same ID and return the edited plan.

## Return to normal mock development

Set `VITE_USE_REAL_BACKEND=false` in `.env.local` and restart Vite. Keep
`VITE_ENABLE_MOCKS=true`. Stop servers with Ctrl+C when finished.
