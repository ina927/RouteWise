# Frontend architecture direction

## Current foundation (Phases 0-7)

`src/main.tsx` mounts React under StrictMode and imports `styles/global.css`.
`app/App.tsx` composes `AppProviders` with the central router. `AppProviders.tsx`
owns one stable TanStack Query client; `router.tsx` owns route definitions outside
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
preserved draft. Saved is enabled in Phase 7; Shared/Account remain disabled.

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
The approved `Plan a journey` action goes to `/plan` and starts a fresh planning
session; leaving PlanningLayout clears its previous memory. Phase 7 enables Saved
navigation and marks Saved active for owned Details. Shared/Account remain disabled.

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
6: Save Journey and Journey Details (complete; live Google verification pending); 7: Saved Journeys (implemented; manual QA pending); 8: Shared Trips; 9: refactor,
QA and integration readiness. Later phases extend these boundaries as needed.
