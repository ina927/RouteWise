# RouteWise API contracts

**Status: FRONTEND PROPOSAL / PENDING BACKEND REVIEW.** This is the Phase 2
frontend boundary, not an approved backend contract or a persistence schema.
Ina defines UI needs; Zoe reviews routing; Nam reviews journeys/users/sharing.
Discuss changes before backend implementation and freeze agreed touchpoints before
integration. No C# DTOs or backend product endpoints were added.

## Boundary and conventions

Feature -> `src/api/*Api.ts` -> browser fetch -> MSW now OR ASP.NET later.
Product code must not import mocks, inspect the mock switch or choose mock-specific
API functions. Query hooks and UI state belong to later phases.

`VITE_API_BASE_URL` defaults to `/api`; an absolute HTTP(S) base with a path prefix
is also supported. API functions encode IDs/query strings and share URL handling
with the mocks. Cross-origin credentials/CORS and production routing are not
decided here. Success responses are direct JSON objects/arrays; deletes return
204. There is no generic response envelope. JSON names use camelCase.

IDs are opaque strings, not database key prescriptions. Distance is in metres;
duration is in seconds. Read/route functions accept optional `AbortSignal`.
Success responses are statically typed, not runtime-schema-validated by the client;
check the future backend against these shapes during integration.

## Contract organisation and identity

| File in `frontend/src/contracts/` | Responsibility |
| --- | --- |
| `places.ts` | Coordinates, Place, RoutePoint |
| `routes.ts` | Value sets, optimise/calculate requests, RouteSegment, RouteResult |
| `journeys.ts` | JourneyPlan, PointAnnotation, Journey, JourneySummary, create/update requests |
| `users.ts` | Minimal UserSummary |
| `sharing.ts` | InvitationStatus, Invitation, send/respond requests |
| `errors.ts` | ApiErrorBody |

Value sets:

- `TravelMode`: `walking | driving`
- `OptimisationObjective`: `distance | time`
- `EndMode`: `open | returnToStart | fixed`
- `InvitationStatus`: `pending | accepted | declined`

`Coordinates = { lat, lng }` uses numeric WGS84 latitude/longitude.
`Place = { placeId, name, address?, coordinates }` contains minimal Google place
data. Keep placeId for later Places photo lookup; no temporary photo URLs, Google
response objects or persistent cover-image fields. No Places-search API is invented.

`RoutePoint = { id, place }` represents **one occurrence**, not one physical place.
The caller assigns distinct IDs before routing and keeps them across reordering
and recalculation. IDs are scoped to a route/journey; array position is order.
No duplicated order/role fields or globally unique point IDs are required.

Return to Start uses a different `returnPointId` from `start.id` for the same
Place, allowing independent notes/times. Never key itinerary metadata by Google
placeId or row index. Broader repeated-place policy is unresolved: the mock allows
distinct occurrences of the same place, without making that a product rule.

## Proposed endpoints

All paths include the default API base; listed responses are successful ones.

| Method / path | API function | Request | Response |
| --- | --- | --- | --- |
| POST `/api/routes/optimize` | `optimizeRoute` | OptimizeRouteRequest | 200 RouteResult |
| POST `/api/routes/calculate` | `calculateRoute` | CalculateRouteRequest | 200 RouteResult |
| GET `/api/journeys` | `getOwnedJourneys` | none | 200 JourneySummary[]; owned only |
| GET `/api/journeys/:id` | `getJourney` | none | 200 Journey; owner or accepted recipient |
| POST `/api/journeys` | `createJourney` | CreateJourneyRequest | 201 Journey |
| PATCH `/api/journeys/:id` | `updateJourney` | UpdateJourneyRequest | 200 Journey; owner only |
| POST `/api/journeys/:id/duplicate` | `duplicateJourney` | none | 201 Journey; owner only |
| DELETE `/api/journeys/:id` | `deleteJourney` | none | 204; owner only |
| GET `/api/users?query=...` | `searchUsers` | username query | 200 UserSummary[] |
| GET `/api/journeys/:id/invitations` | `getJourneyInvitations` | none | 200 Invitation[]; owner only |
| POST `/api/journeys/:id/invitations` | `sendInvitation` | `{ recipientId }` | 201 Invitation; owner only |
| GET `/api/invitations` | `getIncomingInvitations` | none | 200 Invitation[]; recipient, all states |
| PATCH `/api/invitations/:id` | `respondToInvitation` | `{ status: 'accepted' \| 'declined' }` | 200 Invitation; recipient only |
| GET `/api/shared-journeys` | `getSharedJourneys` | none | 200 JourneySummary[]; accepted, not removed |
| DELETE `/api/shared-journeys/:id` | `removeSharedJourney` | none | 204; this recipient only |

The existing non-business `/api/health` passes through to ASP.NET. Other missing
handlers under the API base return mock-only 501, avoiding accidental backend
calls while mocking is enabled. Assets/other origins bypass the mocks.

## ZOE REVIEW: routing shapes

`OptimizeRouteRequest` contains `{ start, destinations, travelMode, objective }`
plus exactly one end-mode variant:

```ts
{ endMode: 'open' }
{ endMode: 'fixed', end: RoutePoint }
{ endMode: 'returnToStart', returnPointId: string }
```

The TypeScript discriminated union requires fixed-end/return identity data; the
mock also validates requests at runtime. Maximum count is:

| End mode | Count |
| --- | --- |
| Open | Start + destinations; final destination comes from this selected set |
| Fixed | Start + destinations + fixed end |
| Return | Start + destinations + separate return occurrence |

A usable route result has at least two occurrences and a segment between each
pair, so open input needs a destination. This is a representation requirement,
not a confirmed minimum-attractions rule. Two-point fixed routes and return
routes with no intermediate points are supported.

`CalculateRouteRequest = { points: RoutePoint[], travelMode }`. Order is
authoritative and returned unchanged; it has no optimisation objective or planner
metadata. The future UI limits dragging to intermediates. This stateless endpoint
cannot infer prior fixed endpoints from an earlier request; it calculates the
supplied endpoints and order.

Both operations return:

```ts
RouteResult = {
  points: RoutePoint[],
  segments: Array<{
    id: string,
    fromPointId: string,
    toPointId: string,
    distanceMeters: number,
    durationSeconds: number,
    path: Array<{ lat: number, lng: number }>
  }>,
  totalDistanceMeters: number,
  totalDurationSeconds: number
}
```

Segments follow point order and identify endpoint occurrences. Segment IDs connect
itinerary sections with future map highlights. The mock derives IDs from ordered
occurrence pairs; the backend format is not prescribed. Geometry is an ordered
coordinate array with at least two positions, not a Google object or undocumented
encoded polyline. Road-snapped geometry need not exactly equal Place coordinates.
Discuss encoded alternatives with Zoe before freeze.

Totals equal segment sums. First/last points identify endpoints; in Open End the
last point tells the UI which destination was chosen. Mode/objective changes make
a new optimise call; manual reorder calls calculate. Reset uses the retained
original result in future frontend state, with no reset endpoint or state code now.
Notes, date and planned times are absent from both routing contracts.

**Zoe review required:** optimise input/end modes, occurrence identity, result and
segment IDs, geometry/units/totals, exact-order calculate, and route validation or
recalculation before persistence. Mocks do not validate road correctness.

## NAM REVIEW: journeys and metadata

`JourneyPlan = { travelMode, objective, endMode, route: RouteResult }` groups route
settings and result for atomic replacement. The objective records the selected
goal even when the current order was manually recalculated.

```ts
Journey = {
  id: string,
  owner: UserSummary,
  name: string,
  date?: string,
  plan: JourneyPlan,
  annotations: PointAnnotation[],
  access: 'owner' | 'shared'
}
PointAnnotation = { pointId: string, note?: string, plannedTime?: string }
```

Access is response context: `owner` is editable, `shared` is read-only. It is not
input or a persisted role framework. Mock mutations independently enforce ownership;
the future backend must enforce identity/access too. Authentication is not defined.

Date is optional `YYYY-MM-DD`. Planned time is optional 24-hour `HH:mm`, per
occurrence including Start and End. Both are **manual metadata only**. No timezone,
calculated arrival, chronological ordering, schedule propagation or optimisation
effect is implied. Notes/time entries can be sparse; at most one annotation per
point. Date/time format checks do not perform timezone conversion or scheduling.

Create: `{ name, date?, plan, annotations? }`; omitted annotations become `[]`.
The server/mock supplies owner and journey ID. This proposal saves the route
snapshot the UI is showing. Zoe/Nam must review how a real backend verifies that
snapshot; this does not authorise trusting arbitrary client costs or prescribe a
database schema.

Update: `{ name?, date?: string | null, plan?, annotations? }`:

- Omitted fields stay unchanged; `date: null` clears it.
- `name` handles rename without a redundant endpoint.
- A supplied plan replaces settings and result together to avoid stale geometry.
- Supplied annotations replace the array; `[]` clears all. Clear an individual
  field by omitting it in the replacement entry, or remove that entry.
- A changed plan with omitted annotations retains surviving point IDs and prunes
  removed ones. Explicit annotations for absent IDs fail.
- Validate before mutation so failed patches preserve previous state.

These are proposed update semantics for review. Duplicate creates an independent
owned journey without invitations. It can keep point IDs because IDs are scoped
to the journey; copies do not share mutable state. Delete removes the owner's
journey permanently; mocks clean up its invitation references too.

`JourneySummary` omits geometry/annotations to keep card lists small:
`{ id, owner, name, date?, representativePlaceId, travelMode, start: {placeId,name}, end: {placeId,name},
locationCount, totalDistanceMeters, totalDurationSeconds }`. Count includes
endpoints/return. A summary is not an access grant, especially inside pending
invitations. Owned/shared lists are separate; details contain explicit access.
Search can run over fetched summaries; no pagination, timestamps or server-side
journey search/sort API is added.

**Confirmed frontend requirement:** `JourneySummary.representativePlaceId: string`
provides one Google place identifier for later Places photo lookup by both Saved
Journeys and Shared Trips cards, without fetching the full Journey. Invitations
reuse the same JourneySummary. This is not an image URL, photo reference, blob or
Google response object, and does not imply RouteWise stores image files. Journey
already contains places in its route points and gains no duplicate photo field.
The final representative-photo selection rule remains unconfirmed; the contract
does not prescribe whether the backend stores, derives or otherwise produces it.

## NAM REVIEW: users and sharing

`UserSummary = { id, username }`. Sharing searches usernames only, case-insensitively,
with self excluded. Email is not part of the product or mock user store. Passwords,
tokens, profiles and avatar fields are never returned. Phase 8.5 adds the session
boundary below without changing public UserSummary.

`Invitation = { id, journey: JourneySummary, sender: UserSummary,
recipient: UserSummary, status: InvitationStatus }`. Sender/owner identify who
shared. Summaries derive from current journey data, not stale invitation copies.
Owners see all invitation statuses. Incoming data includes all statuses too;
derive the sidebar count with `status === 'pending'`, without another endpoint.

Accept grants read-only detail access and adds Shared with me; decline does not.
Shared recipients cannot update/rename/duplicate/delete the owner's journey or
send its invitations. Remove from Shared calls its own endpoint, never DELETE
`/journeys/:id`. It removes only this recipient's listing/read access; owner data
and other recipients remain untouched.

**Nam review required:** save snapshots, patch/annotation semantics, date/time,
summaries, rename/duplicate/delete, user search privacy, invitations/state changes,
owner/shared access and recipient removal/history.

## Errors

Error body: `{ message: string }`. `ApiError` exposes HTTP `status` and `message`.
HTML/empty/malformed error bodies receive a useful HTTP fallback. Network failures
and aborts remain native fetch errors, without an invented HTTP status. Invalid
success JSON remains an error. No-content operations return undefined.

- 400: invalid JSON/shape; missing fixed end/name; more than 10 occurrences;
  invalid occurrence IDs, coordinates, metadata or inconsistent route segments.
- 401: unauthenticated product access/current-user lookup or generic invalid login.
- 403: missing shared access or attempts at owner-only operations.
- 404: unknown journey/user/invitation or missing accepted shared relationship.
- 409: duplicate username (case-insensitive), or mock-only repeated invitation or response to an already answered invitation.
- 501: mock-only missing API handler/method.

Mock validation covers confirmed rules and transport consistency, not speculative
business policies. Unrecognised input fields are ignored, not persisted. Auth
pages map failures to safe messages instead of displaying backend exception details.

## Mock-only choices and unresolved decisions

State is in-memory under `src/mocks`, per tab, reset by full refresh; no localStorage.
The initial viewer is unauthenticated. Login/signup select the same effective
viewer used for all product permissions. `setMockViewer`/`resetMockStore` remain
development helpers, not product APIs. Four journeys/six invitations cover owned,
incoming, outgoing, accepted, declined and multi-recipient cases.

The following are **MOCK-ONLY**, not confirmed product behaviour:

- Synthetic `mock-place-*` IDs and straight two-coordinate paths, not live Google
  IDs or navigable routes. Google integration is not implemented.
- Representative place selection uses the first route point (start) in the shared
  mock summary mapper, deterministically reading its existing placeId. This is
  **MOCK-ONLY**, not a permanent product/business rule. It applies to seeded,
  created, updated and duplicated journeys, including shared/invitation summaries.
  Synthetic fixture IDs are placeholders, not usable for actual Google photo calls.
- Deterministic destination reversal/rotation; invented costs from a small pair
  seed and mode divisor. No real optimisation/routing or Simulated Annealing.
- Monotonic created IDs, duplicate suffix `(copy)`, insertion-order lists.
- Case-insensitive substring user search, empty query -> `[]`, self excluded.
- Repeated invitations for the same recipient/journey return 409 regardless of
  previous state; already-answered responses also return 409. Reinvite/retry and
  idempotency policies are not established by this choice.
- Removal retains accepted invitation history while removing this recipient's
  listing/read access; no fourth invitation status is introduced.
- Removed-point annotation pruning and invitation cleanup on deletion are
  proposed/mock consistency behaviour, not a backend storage design.

Still unresolved: cross-origin deployment/CORS and backend session hardening,
duplicate naming, reinvite/retry policy,
broader repeat-place policy, final geometry/route provenance verification,
concurrent-edit handling, removal/history semantics, database design, production
caching/deployment routing. No timestamps, pagination, photo URLs or social
features were added to anticipate these choices.

## Enablement and validation

Copy `.env.example` to `.env.local`; `VITE_ENABLE_MOCKS=true` starts MSW before
React renders, only in Vite development. Missing/false uses the real API. Restart
Vite and fully reload after changing it. Production ignores the switch even when
true; mock imports are removed from the application bundle. The generated public
worker may be copied as an inert static asset but is not registered in production.

When switching a tab with an old worker, close/reload it. An inactive MSW
registration can also be removed in browser DevTools; do not unregister unrelated
workers. No product code needs to know whether requests are mocked.

`npm run smoke:api` uses native Node assertions and MSW's Node interceptor with the
same API functions/handlers. It then stops MSW and calls a real local HTTP stub
through the unchanged API module. No framework was added. This verifies transport
and state, not browser service-worker registration, UI or ASP.NET product APIs.

## NAM HANDOVER: Authentication and Account (Phase 8.5)

Frontend/MSW contract, pending real ASP.NET implementation. The account model is
username + password only. There is no email, full name, profile, username/password
editing or recovery. CurrentUser aliases UserSummary but means authenticated
session identity. Production must use a server-managed HttpOnly cookie/session;
no JWT, bearer header, token decoding or browser token storage is used.

| Method/path | Request | Success |
| --- | --- | --- |
| POST /api/auth/signup | { username, password } | 201 CurrentUser |
| POST /api/auth/login | { username, password } | 200 CurrentUser |
| POST /api/auth/logout | none | 204 |
| GET /api/auth/me | none | 200 CurrentUser; 401 when unauthenticated |
| DELETE /api/account | none | 204 |

CurrentUser is exactly { id, username }. Confirm password is frontend-only.
Username is trimmed, 3?20 ASCII letters/digits/underscores, case-insensitively
unique, with entered casing preserved for display. Login uses the same casing
policy; passwords are case-sensitive, never trimmed, and require at least 8
characters at signup without complexity rules. Signup validation uses 400;
duplicate username uses 409. Invalid login always uses 401 with the generic
"Invalid username or password." response. Logout is idempotent in MSW.

Hard deletion removes owned journeys, invitations on those journeys, invitations
where the user is sender/recipient, all associated shared relationships, the
credential and user record. Other owners' journeys/other recipients survive.
The username can be registered again with a new ID and no old access/history.

The fetch boundary explicitly uses same-origin cookie credentials, sufficient
for the current /api proxy. Cross-origin credentialed deployment requires a
separate coordinated credentials/CORS decision; it is not silently enabled.
Backend must implement session establishment/invalidation, HttpOnly cookie
configuration, request protection and API permission enforcement. Passwords must
be stored only as hashes, never plaintext; the backend team chooses the hashing
implementation. No cookie middleware, hashing, persistence or C# code is added.

MSW alone uses separate in-memory development credentials and viewer state, not
real HttpOnly cookies. It resets to unauthenticated on full reload, while real
/auth/me should restore a still-valid server session. No fake persistence is added.
