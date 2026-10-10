# Authentication implementation (Nam)

Signup, login, current-user lookup and logout are implemented. Account deletion
is the next account feature. Keep frontend MSW enabled while journey, sharing and
account-deletion endpoints remain unfinished.

| Method | Path | Success |
| --- | --- | --- |
| POST | /api/auth/signup | 201 CurrentUser |
| POST | /api/auth/login | 200 CurrentUser |
| GET | /api/auth/me | 200 CurrentUser |
| POST | /api/auth/logout | 204 |

CurrentUser contains exactly `{ id, username }`. Login trims/normalizes usernames
and verifies the password without trimming it. Missing credentials, unknown users
and wrong passwords return 401 with `Invalid username or password.` Malformed
JSON returns 400. Successful login upgrades older password hashes when needed.
`/auth/me` derives identity from the cookie, uses `Cache-Control: no-store` on its
successful response and returns 401 without a valid session. Logout is repeatable:
it returns 204 even when already logged out (a trusted Origin is still required).

## Request and response

Request: `{ "username": "River", "password": "routewise123" }`.
Success: HTTP 201 with exactly `{ "id": "...", "username": "River" }`, plus an
HttpOnly session cookie. Errors use `{ "message": "..." }`.

Username is trimmed, 3–20 ASCII letters/digits/underscores, and stored with its
original display casing. Uppercase invariant normalization and a unique SQLite
index enforce case-insensitive uniqueness. Passwords require 8 characters,
are never trimmed, and are stored using ASP.NET Core's PasswordHasher<User>.
Invalid JSON/input returns 400; duplicate usernames return 409, including a
unique-index conflict from simultaneous requests. Unexpected failures return a
safe 500 response rather than database exception details.

## Session and request protection

ASP.NET cookie authentication uses MemoryTicketStore (ITicketStore). Only a
protected session reference goes into the cookie; the ticket stays on the server.
The cookie is HttpOnly, SameSite=Strict, a browser-session cookie and Secure outside
development (or on HTTPS in development). Tickets expire after 8 hours without
sliding renewal. The lifetime is an implementation choice, not a brief requirement.
Each authenticated request checks that the user still exists. Missing users cause
session rejection and signout. API authentication failures return JSON 401/403,
not HTML redirects. Logout calls SignOutAsync, which removes the server ticket
and clears the browser cookie. Successful sign-in replaces an existing session
with a new ticket, so an old cookie cannot follow a switch to another account.

Sessions survive page refresh, but restarting the API invalidates them. This
in-memory store is for the current single-process setup; shared/durable sessions
and Data Protection key management need deployment decisions before multiple
instances or restart-persistent sessions are required. Accounts remain in SQLite.

All state-changing `/api` requests require one exact allowed Origin header.
Absent, null, multiple, and unlisted origins return 403 before endpoint execution.
Signup and login also require JSON, rejecting form submissions. This is Origin-based CSRF
protection, not an antiforgery-token flow. It matches the existing same-origin
frontend fetch calls and does not enable credentialed CORS. Only list origins
controlled by this application. GET/HEAD/OPTIONS must remain free of mutations.

Development defaults: `http://localhost:5173` (Vite) and `http://localhost:5000`
(direct API). To override them, set Authentication:AllowedOrigins as a configuration
array. Production has no default allowed origins and uses Secure cookies; configure
the actual HTTPS public origin, for example via the environment variable:

```sh
export Authentication__AllowedOrigins__0=https://routewise.example
```

Non-browser clients must explicitly send the Origin header too. No request body,
password, or cookie logging should be enabled.

## Local manual check

From the repository root, apply the existing migration and start the API:

```sh
cd backend/RouteWise.Api
dotnet ef database update
dotnet run --launch-profile http
```

In another terminal, this creates a real local test account (choose an unused name):

```sh
curl -i http://localhost:5000/api/auth/signup \
  -H 'Origin: http://localhost:5000' \
  -H 'Content-Type: application/json' \
  --data '{"username":"signup_demo","password":"demo-password-123"}'
```

Expect 201, the two-field response, and Set-Cookie. Repeating with the same username
returns 409. This command is only a manual example; automated tests use isolated
SQLite files and never seed the developer database.

## Verification

Run `dotnet test RouteWise.sln` from the repository root. Signup tests migrate a
fresh temporary SQLite database for each test and delete it afterward. They cover
validation, JSON errors, normalization, password hashing, duplicate/racing signups,
cookie reuse, Origin protection, deleted-account rejection, server-side signout
invalidation, JSON authentication failures and production Secure cookies.

AuthenticationTests exercises the real login, /auth/me and logout endpoints:
login/logout/refresh, generic credential errors, malformed input, Origin checks,
replayed cookies after logout/account switching, deleted accounts and hash upgrades.
The older signup tests also retain internal probes to isolate cookie infrastructure;
those probe URLs exist only inside the test host.
