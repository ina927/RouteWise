# Authentication implementation (Nam)

Signup, login, current-user lookup, logout and account deletion are implemented.
Journey management, user search and invitation sharing also use these authenticated
sessions. Journey PATCH supports name/date updates and replacement of stop notes
and planned times, plus transactional route-plan replacement. Route updates preserve
the journey ID, owner and invitations. Optional integration mode has verified real
login and saved-route editing in the browser while retaining simulated routing;
see [Integration testing](INTEGRATION_TESTING.md). Full routing integration remains pending.

Journey detail lookup delegates database and access checks to `JourneyService`.
Its internal `ServiceResult<JourneyResponse>` is mapped by the endpoint to the
existing 200/403/404 responses. Authentication remains enforced at the HTTP boundary;
owner/shared-access rules remain enforced inside the service. Other operations
have not yet been extracted into services.

| Method | Path | Success |
| --- | --- | --- |
| POST | /api/auth/signup | 201 CurrentUser |
| POST | /api/auth/login | 200 CurrentUser |
| GET | /api/auth/me | 200 CurrentUser |
| POST | /api/auth/logout | 204 |
| DELETE | /api/account | 204 |

CurrentUser contains exactly `{ id, username }`. Login trims/normalizes usernames
and verifies the password without trimming it. Missing credentials, unknown users
and wrong passwords return 401 with `Invalid username or password.` Malformed
JSON returns 400. Successful login upgrades older password hashes when needed.
`/auth/me` derives identity from the cookie, uses `Cache-Control: no-store` on its
successful response and returns 401 without a valid session. Logout is repeatable:
it returns 204 even when already logged out (a trusted Origin is still required).

## Account deletion

DELETE /api/account requires a valid session and trusted Origin; no request body
or user ID is needed. It deletes only the authenticated user. SQLite cascades remove
owned journeys, points/segments and invitations on those journeys or involving the
user. Other owners' journeys and other recipients' invitations survive.

The current session is removed and its cookie cleared after the database deletion
succeeds. Other sessions are rejected on their next request because the user ID no
longer exists. Deleted usernames can be registered again with a new ID and no old
access. Unauthenticated requests return 401; missing/untrusted Origin returns 403.
The existing frontend confirmation dialog supplies the user confirmation; no extra
password-confirmation field or API is added. Tests exercise deletion only against
isolated temporary databases, never the developer's local account.

## Request and response

Signup request: `{ "username": "River", "password": "routewise123" }`.
Success: HTTP 201 with exactly `{ "id": "...", "username": "River" }`, plus an
HttpOnly session cookie. Errors use `{ "message": "..." }`.

Username is trimmed, 3–20 ASCII letters/digits/underscores, and stored with its
original display casing. Uppercase invariant normalization and a unique SQLite
index enforce case-insensitive uniqueness. Passwords require 8 characters,
are never trimmed, and are stored using ASP.NET Core's PasswordHasher<User>.
Invalid JSON/input returns 400; duplicate usernames return 409, including a
unique-index conflict from simultaneous requests. Unexpected failures return a
safe 500 response rather than database exception details.

## Journey and sharing authorization

All implemented journey, user-search and invitation endpoints require a session.
Identity comes from the authenticated user, not a caller-supplied user or sender ID.

- Owners can create, list, view, rename, duplicate and delete their journeys.
  Only owners can send invitations or list invitations for a journey.
- Username search excludes the caller and returns only `{ id, username }`.
- Incoming invitations are filtered to the current recipient and include pending,
  accepted and declined states. Only that recipient can accept or decline them.
- Pending or declined invitations grant no journey-detail access. Accepted
  invitations grant read-only access only while `RemovedFromShared` is false.
- Removing a shared journey marks only the caller's active accepted relationships
  as removed. The owner's journey, other recipients' access and invitation history
  remain intact. Duplicating a journey does not copy its invitations.

For the current provisional policy, sending another invitation for an existing
journey/recipient pair returns 409 regardless of its status. This lookup is not a
database uniqueness guarantee under concurrent sends. Responses to invitations
use a conditional update of pending rows, so a second answer cannot overwrite the
first; it returns 409. Reinvite/retry behavior remains a product decision.

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

AccountTests covers authorization, Origin protection, session invalidation, username
reuse with a new identity, and cascade deletion while preserving unrelated data.

Journey, user-search and invitation integration tests exercise authenticated HTTP
requests against isolated temporary databases. They cover owner/recipient boundaries,
public response fields, invitation state transitions, competing responses, pending
access denial, shared removal and isolation between recipients. The latest verified
full backend run passed 256 tests. Route-update tests cover replacement plans,
annotation handling, preserved shared access and rollback after a database failure.
This suite passed again after the journey lookup service extraction. The suite
alone does not establish browser integration. A separate browser check verified
real login, saving and reopening an edited route, retained annotations, stable
journey identity and leaving without saving, using simulated route calculation.
