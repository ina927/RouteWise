# Frontend architecture direction

## Current foundation (Phases 0-8.5)

`src/main.tsx` mounts React under StrictMode and imports `styles/global.css`.
`app/App.tsx` composes `AppProviders` with the central router. `AppProviders.tsx`
owns the root authentication Query client; `router.tsx` owns route definitions outside
React renders. Root redirects to `/plan`; unknown routes retain the neutral
bootstrap fallback. `/plan` and `/plan/route` share PlanningLayout.

Phase 1 adds `styles/tokens.css` and a self-hosted Inter font, both loaded by
`global.css`. See [Design system](DESIGN_SYSTEM.md) for the visual source of truth.
Vite handles CSS Modules natively. Phase 2 provides frontend contracts, fetch
modules and MSW handlers; contracts remain pending backend-team review. Phase 3
adds shared layout and UI components without mounting feature pages or changing
the router. Phases 4-5 add planning and route presentation within this foundation.

## Shared UI foundation (Phase 3)

- `components/layout`: AppShell owns only the sidebar/main structure and skip
  link; Sidebar receives `activeItem`, `pendingSharedCount` and `onNavigate`.
  The callback emits `plan | saved | shared | account` intentions. The route
  layout owns destinations. Optional `disabledItems` applies native disabled
  semantics and styling without emitting navigation or adding business state.
  PageHeader accepts a title, optional subtitle and actions.
- `components/ui`: Button, IconButton, TextInput, SearchInput, Select,
  SegmentedControl, Toggle, Badge and Modal. Import components directly.
  Field is an internal label/helper/error structure shared by TextInput and Select;
  SearchInput composes TextInput and IconButton shares Button's visual styles.
- Data in, events out: no API, mock, contract or feature imports. Search, segmented
  selection, toggles and modals are controlled by their future consumers.
- Native buttons/inputs/select/radios provide keyboard semantics. Inputs/selects
  require a label (optionally visually hidden) and associate messages with IDs.
  IconButton requires a label. Useful native props/ref are passed to controls.
- Modal uses native `dialog.showModal()` for modal focus containment and inert
  background. It focuses its title or a supplied `initialFocusRef`, closes via
  Escape/close button, restores the connected opener, and locks background scroll.
  Consumers set `open=false` in `onClose`; compose fields and footer actions.
  Backdrop clicks do not dismiss. Use one application modal at a time; nested
  dialog orchestration is outside this foundation.

AppShell now wraps planning routes; do not place a second `main` inside it.
No permanent component gallery is included; feature phases compose these primitives.

## Plan a Journey (Phase 4)

`features/journey-planning/PlanningLayout` owns a React context scoped to `/plan`
and `/plan/route`. It retains editable query/Place rows, stable occurrence IDs,
nullable mode/objective/end mode and the route state described below.
Full reload or leaving this route scope clears memory; there
is no browser storage or application-wide store. Fixed End state stays in the
draft even when its field is unmounted. Typed text alone is never a Place.

`PlanJourneyPage` composes the shared primitives and feature-local PlaceSearchField,
EndModeSelector and JourneyPlanMap. `planning.ts` builds a transport request only
when confirmed requirements are met. The counter counts selected occurrences;
add-row capacity additionally reserves the active mode's endpoint slots and caps
empty rows. No selected input is deleted on mode changes. Unselected search text
and blank rows are omitted from submission.

Find route uses a TanStack mutation through `routesApi.optimizeRoute`, with an
immediate in-flight guard, disabled controls, abort on unmount, and inline errors.
Success stores the submitted configuration/result and navigates to `/plan/route`.
RouteBoundary renders OptimisedRoutePage; direct access without
an in-memory result redirects to `/plan`. The Plan sidebar intent returns to the
preserved draft. Plan, Saved, Shared and Account are enabled after authentication.

`googleMaps.ts` isolates SDK loading, narrow private vendor type descriptions,
autocomplete session tokens, requested place fields, and map/marker instances.
Only application-owned Place data and a small suggestion selection callback
cross into the feature. Search uses a 300ms debounce and discards stale responses.
The map uses Advanced Markers, reads existing CSS colour tokens, and draws no
route lines or numbers on Plan. A neutral world view is an implementation default, never
a Places search bias. At narrower desktop widths the feature grid stacks.

## Optimised Route (Phase 5)

`features/route-optimisation` owns the result page, route-update hook, stop list,
map presentation and small metre/second formatters. PlanningLayout still owns
the in-memory state shared by both routes; no application-wide store was added.

`PlanningResult` contains:

- `request`: the last successful optimisation's submitted planning problem/settings.
- `optimisedRoute`: the latest successful optimisation baseline.
- `currentRoute`: the currently displayed successful result.
- `modified`: true after manual calculation, cleared by optimisation or Reset.

Plan submission initialises both results. Mode/objective changes immediately call
the existing `optimizeRoute()` through a TanStack mutation, preserving the submitted
places and end configuration independently of current manual order. Successful
updates replace both results and copy only mode/objective into the editable draft.
Failures retain the last valid result and restore its successful settings. Abort
controllers plus request-identity checks discard superseded/unmounted responses.
Controls remain available for rapid optimisation changes; drag and Reset are
disabled while updating. Exact-order calculation also disables setting changes.

Only intermediate rows register with dnd-kit, using explicit handles and keyboard
sensors. Drop submits the exact new order to `calculateRoute()` without objective.
The old valid list/metrics remain until success; failures preserve them. Manual
calculation never changes the baseline. Reset clones the retained baseline locally,
without either API call. Edit places simply navigates back, preserving input rows,
queries, inactive Fixed End and the latest successful settings.

Segment hover/focus and click selection are page-local UI state keyed by segment
ID. Clicking a selected segment clears it; invalid IDs are pruned when the result
changes. Neither interaction calls the API or fits the map. Numbering is derived
from displayed order; Start/End (including Open End's actual final point) are fixed.

The existing Google adapter shares loading, initialisation, colours and bounds
between Plan and Route maps. Route rendering draws only `segments[].path`, with
solid coral lines, static thicker hover emphasis and a dashed selected segment.
Static dashes require no motion exception. All path coordinates participate in
route fitting, which occurs on result/size changes, never on highlight changes.
Co-located Start/End share a visible coral pin labelled Start / End; both occurrence
IDs remain distinct in the list and route state. Intermediates have blue numbered
pins. Map failure does not disable non-map route interactions.

Phase 6 activates Save journey without changing route-update/reset behaviour.
No new dependency or Phase 3 shared-component API was added.
`dnd-kit-compat.d.ts` supplies React 19 JSX type aliases needed by the installed
dnd-kit declarations, without disabling library type checking.

## Save Journey and Journey Details (Phase 6)

`features/journey-details` owns SaveJourneyModal, JourneyDetailsPage, the itinerary,
small explicit-save field editor, PlacePhoto and the query/update helpers.
The save modal captures a clone of the currently displayed route and its last
successful settings when opened, including a manually modified route. Pending
route changes do not alter this snapshot. Creation uses a trimmed required name,
optional calendar date and empty annotations; it never routes or optimises.
Duplicate submission and closing during submission are blocked. Failure retains
the form; success caches the returned Journey and navigates to its returned ID.

`/journeys/:journeyId` is outside PlanningLayout. `getJourney(id)` and the TanStack
Query key `['journey', id]` own server state. Direct/refresh access fetches the API,
with loading, unavailable and not-found states rather than a Plan redirect.
Details returns to Saved for owner access and Shared for shared access, with the
matching Sidebar item active. This uses API access rather than browser history.
Plan navigation starts a fresh session; leaving PlanningLayout clears its memory.

Owner fields use one editor at a time with Save/Cancel (and Clear for optional
fields). Unchanged values do not submit. Successful responses replace the query
cache; failures keep the last server state and editing input. A synchronous guard
serialises updates, and annotation replacements merge against the latest cached
Journey by RoutePoint.id. Notes and manual HH:mm times preserve each other and all
other occurrences. Empty annotations are removed. Clearing date sends null.
No timezone conversion, scheduling or route API call is involved.

Itinerary is the default local tab; keyboard arrows/Home/End switch tabs. Plan
times is presentation only, initially on when saved times exist; hiding it never
deletes values. Shared access shows notes/times and an explicit read-only label,
with no edit controls or mutation path. Map View uses a compact read-only itinerary
alongside the existing OptimisedRouteMap with context-appropriate fallback copy.
Saved segment geometry and Phase 5 metric formatters are reused unchanged.

Intermediate photo slots call `getPlacePhoto(placeId)` within the existing Google
adapter. It requests only photos, obtains a transient URI with getURI and maps
author attribution into presentation data. No vendor object or photo field enters
Journey contracts, API requests or persistent storage. Start/End have no photo.
Unavailable/no-photo/broken-image cases use a neutral slot. Successful images show
Google Maps and supplied author attribution. Live Google verification is pending.

MSW remains per-tab memory and resets on full reload. Seeded detail IDs verify
refresh loading; newly created journeys and edits do not survive that reset.
Durable persistence verification awaits the backend. No browser persistence,
sharing workflow or backend implementation is added here; Saved listing follows in Phase 7.

## Saved Journeys (Phase 7)

`features/saved-journeys` owns `/saved`, JourneyCard, the accessible card action
menu and rename/delete dialog. TanStack Query loads `getOwnedJourneys()` under
`['owned-journeys']`. Search matches name/start/end case-insensitively; client-side
sort defaults to Name A–Z. Date sorts place undated journeys last in both directions.
The grid uses at most three columns, reducing to two at 1280/1024px.

Cards use `representativePlaceId` with the existing PlacePhoto/Google adapter and
neutral fallback. The final representative-place selection rule is still undecided;
no photo field or persistence was added. Live Google verification remains pending.
Main-card buttons and action menus are separate focus targets; author links stay
independently clickable. Menus support arrow/Home/End, Escape, Tab and outside close.
Rename sends only name; duplicate uses existing API naming/independence semantics;
delete requires confirmation. Pending guards prevent duplicate submits, failures
retain dialog input or appear on the affected card, and duplication announces
feature-local status feedback. No global notification framework was introduced.

Edit route navigates to `/plan` with a one-use Journey ID in navigation state.
PlanningLayout consumes and clears that intent, loads the full Journey, verifies
owner access and initialises a feature-local in-memory session. The draft retains
occurrence IDs, including the separate return occurrence, and reconstructs query
text from stored place names. Open End includes all points after Start as
destinations; Fixed/Return exclude their final endpoint. Initial result is empty:
the user runs normal Find route; stored order never implies optimisation priority.
Leaving the planning scope clears the session. New journey from Saved therefore
starts fresh, and consumed history state cannot restore a previous edit.

Optimised Route uses Save changes for an edit session and sends only `{ plan }`
with last successful settings/current displayed route. It never routes again or
opens the creation modal. Omitted metadata/annotations follow existing API retain/
prune semantics. Failure preserves the session/result; success navigates to Details.
New journeys retain the Phase 6 creation flow. Shared/Account workflows remain out
of scope, and no contracts, dependencies or backend code changed.

`journeyData.cacheJourney` caches returned detail objects and invalidates owned
summaries after create, rename, detail edits, route updates and duplicate. Delete
cancels related fetches, removes detail/list entries and invalidates the list.
The summary is re-fetched rather than inventing a client photo-selection policy.

## Shared Trips and Journey Sharing (Phase 8)

`app/ProductShell` connects navigation and the shared incoming-invitation query to
presentational AppShell/Sidebar. Plan, Saved, Shared and Account are enabled
inside authenticated routes. The red notification count is derived from incoming invitations
whose status is pending, hidden at zero and shared across page observers.

`features/sharing` owns `/shared`, compact pending rows, accepted cards, removal
confirmation and the owner-only Share modal. Pending and accepted sections have
independent loading/error/empty states. Accept/Decline use the existing response
API, update incoming cache immediately, and refetch it; Accept also adds the
returned summary to an existing shared-list cache and invalidates that list.
Removal calls only the shared-relationship endpoint, cancels related reads,
removes list/detail caches and invalidates shared journeys. It does not delete an
owner journey or modify invitation history. Mutation guards block repeat submits;
errors retain the affected row/dialog for retry.

Keys are `['incoming-invitations']`, `['shared-journeys']`,
`['journey-invitations', id]` and `['user-search', trimmedQuery]`, alongside the
existing detail/owned keys. Incoming data has a 30-second freshness window to
avoid repeated shell fetches, with normal query refetch behaviour thereafter.
No polling, global cache clearing or browser persistence was introduced.

The Share modal trims/debounces search by 300ms, skips empty input and uses
abortable query keys plus current-term gating to prevent stale results appearing.
Sharing lookup/display is username-only. The UI filters returned results against
the username and excludes all Pending/Accepted/Declined recipients. A compact
keyboard-accessible dropdown selects a recipient; a separate Invite button sends
only after selection. Success clears selection/search. Sending retains the same
journey-invitations cache update/invalidation. Existing invitations remain below
with status badges. No reinvite policy is introduced. Initial avatars derive their
letter from username and a stable restrained colour from user ID; no profile data.

`features/journeys` contains the extracted card visual shell, grid CSS and
keyboard menu shared by Saved and Shared. Saved keeps its explicit owned-action
wrapper; Shared supplies only Remove from Shared. Saved and Shared use the same
24.75rem (396px) maximum card width, 40px gaps and at-most-three columns.
The sharing byline renders only when present, leaving no empty slot in Saved.
Longer content can grow without clipping. Photo treatment is retained.
PlacePhoto and read-only Journey Details are reused. No contract, dependency,
mock permission, backend, authentication or social feature was added. Live Google
photo verification remains pending; Phase 9 has not begun.

## Authentication and Account (Phase 8.5)

`features/auth` owns Login/Signup, Account, session context/query and guards.
The root QueryClient owns only `['current-user']`, from GET /auth/me. 401 means
unauthenticated; other errors show a retry screen. Public-only and protected
pathless router boundaries prevent content flashes and redirect loops. Login and
signup always enter /plan; no return-URL scheme was added. A one-use sign-out
destination lets the guard send deletion to /signup and logout to /login; the
public boundary resets it so later unauthenticated protected visits use /login.

Each authenticated mount gets a separate ProductSession QueryClient keyed by user
ID. Logout/deletion unmounts it, cancels reads and clears queries/mutations; the
next login receives a fresh client even for the same user. Late feature callbacks
can only reach the discarded client, never the next user's cache. Existing
journey/invitation keys and feature invalidation stay unchanged. Product 401
errors request a session refresh. Auth state never lives in PlanningLayout;
its draft is naturally destroyed when protected content unmounts.

Auth submissions use authApi, with local guarded pending/error state. Passwords
remain native input/request-scope values, are cleared after API attempts, and
never enter query/mutation variables, public objects or browser storage. Confirm
password is validated locally only. Login/signup validation and safe errors are
feature-owned; mock credentials are separate from public users. Signup and login
use case-insensitive usernames, preserving display casing. Account deletion is a
hard cascade implemented only in MSW now. See API_CONTRACTS for backend handover.

Login/Signup use compact standalone forms; Account uses ProductShell with username,
logout and a separate confirmed deletion action. No email, profile, editing,
recovery, dependency, backend code or global design refinement was introduced.
MSW resets authentication on reload; real cookie/session restoration is pending
backend implementation. Phases 9/global visual refinement have not begun.

## Future responsibilities

Create directories when they have real responsibilities, not as empty placeholders:

```text
src/
  app/                         bootstrap, providers, routing
  components/
    ui/                        reusable visual primitives
    layout/                    application shell, sidebar and page header
    maps/                      map presentation and interaction
    journey/                   reusable journey presentation
  features/
    journey-planning/
    route-optimisation/
    journey-details/
    saved-journeys/
    shared-trips/
  api/                         fetch boundary and HTTP behaviour
  contracts/                   agreed frontend-facing data shapes
  mocks/                       MSW HTTP handlers and fixtures
  styles/                      global foundation and central design tokens
  hooks/                       genuinely shared hooks
  utils/                       small shared utilities
  assets/                      actual product assets
```

Keep feature-specific UI, hooks and data orchestration together. Pages compose
components; components render data and raise interactions; the API boundary owns
transport details. Shared components must not depend on feature internals.
Extract reuse when it is real; avoid giant pages and speculative abstractions.

TanStack Query will manage server state. Local interaction state belongs in React
components/hooks, and journey drafts stay in the relevant feature. Do not copy
server data into a global store without a concrete need. Core optimisation,
route-cost logic and persistence stay in C#.

## Contracts and mock-to-real integration (Phase 2)

Ina defines frontend data needs, then Zoe/Nam review relevant contracts before
implementation. Pages must not depend on EF entities, backend class layout or
Google service response shapes. Agreed frontend contracts preserve each team's
freedom to implement its own internals.

Use browser `fetch` through a small API boundary. MSW intercepts those same
HTTP requests during mock-first development. Switching to real endpoints should
change configuration/mock startup, not page components. `VITE_API_BASE_URL` sets
that boundary; `VITE_ENABLE_MOCKS=true` enables development mocking. See
[API contracts](API_CONTRACTS.md) for proposed shapes and mock-only decisions.
Phase 3 leaves this layer unchanged.

## Visual rules (Phase 1 onward)

Colours, typography, spacing, radii and shadows are centralised in `tokens.css`;
later component styles use CSS Modules. The same semantic role uses the
same shared visual rule across views. Mockups guide layout, not separate per-page
typography systems. Avoid repeated hardcoded values and copied components.
Use Lucide vector icons consistently; never use emojis as product UI icons.
Comments should explain non-obvious intent, not restate code.

## Phase boundaries

0: bootstrap/tooling; 1: design system; 2: contracts/API boundary/MSW;
3: shared UI primitives and application shell; 4: Plan a Journey (complete); 5: Optimised Route (complete; live Google verification pending);
6: Save Journey and Journey Details (complete; live Google verification pending); 7: Saved Journeys (complete); 8: Shared Trips and Journey Sharing (complete); 8.5: Authentication and Account
(implemented; manual visual QA pending); 9: refactor,
QA and integration readiness. Later phases extend these boundaries as needed.
