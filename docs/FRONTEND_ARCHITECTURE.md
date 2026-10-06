# Frontend architecture direction

## Current foundation (Phases 0-4)

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
the router. Maps, journey presentation and feature orchestration remain later work.

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
No permanent component gallery, JourneyCard or product modal is included.

## Plan a Journey (Phase 4)

`features/journey-planning/PlanningLayout` owns a React context scoped to `/plan`
and `/plan/route`. It retains editable query/Place rows, stable occurrence IDs,
nullable mode/objective/end mode, the submitted request snapshot and the original
successful result. Full reload or leaving this route scope clears memory; there
is no browser storage or application-wide store. Fixed End state stays in the
draft even when its field is unmounted. Typed text alone is never a Place.

`PlanJourneyPage` composes the shared primitives and feature-local PlaceSearchField,
EndModeSelector and JourneyPlanMap. `planning.ts` builds a transport request only
when confirmed requirements are met. The counter counts selected occurrences;
add-row capacity additionally reserves the active mode's endpoint slots and caps
empty rows. No selected input is deleted on mode changes. Unselected search text
and blank rows are omitted from submission, as indicated in the form helper.

Find route uses a TanStack mutation through `routesApi.optimizeRoute`, with an
immediate in-flight guard, disabled controls, abort on unmount, and inline errors.
Success stores the submitted configuration/result and navigates to `/plan/route`.
RouteBoundary contains only neutral Phase 5-pending text; direct access without
an in-memory result redirects to `/plan`. The Plan sidebar intent returns to the
preserved draft. Saved/Shared/Account remain visible and disabled.

`googleMaps.ts` isolates SDK loading, narrow private vendor type descriptions,
autocomplete session tokens, requested place fields, and map/marker instances.
Only application-owned Place data and a small suggestion selection callback
cross into the feature. Search uses a 300ms debounce and discards stale responses.
The map uses Advanced Markers, reads existing CSS colour tokens, and draws no
route lines or numbers. A neutral world view is an implementation default, never
a Places search bias. At narrower desktop widths the feature grid stacks.

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
3: shared UI primitives and application shell; 4: Plan a Journey (complete); 5: Optimised Route;
6: Journey Details and modals; 7: Saved Journeys; 8: Shared Trips; 9: refactor,
QA and integration readiness. Later phases extend these boundaries as needed.
