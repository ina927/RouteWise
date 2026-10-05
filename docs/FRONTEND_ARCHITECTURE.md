# Frontend architecture direction

## What exists in Phase 0

`src/main.tsx` mounts React under StrictMode and imports the minimal global reset.
`app/App.tsx` composes `AppProviders` with the central router. `AppProviders.tsx`
owns one stable TanStack Query client; `router.tsx` owns route definitions outside
React renders. Root and unknown-route messages are neutral bootstrap fallbacks.
They are not product screens.

Vite handles CSS Modules natively. No module styles, design tokens, maps, product
components, data contracts, API client or mocks are implemented yet. dnd-kit,
Lucide and MSW are installed as confirmed future dependencies only.

## Future responsibilities

Create directories when they have real responsibilities, not as empty placeholders:

```text
src/
  app/                         bootstrap, providers, routing
  components/
    ui/                        reusable visual primitives
    navigation/                shared navigation
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

Use browser `fetch` through a small API boundary. MSW will intercept those same
HTTP requests during mock-first development. Switching to real endpoints should
change configuration/mock startup, not page components. `VITE_API_BASE_URL` is
reserved for that boundary; Phase 0 does not consume it or make product requests.
Do not add mock handlers or infer payloads until contracts are agreed.

## Visual rules (Phase 1 onward)

Centralise colours, typography, spacing, radii and shadows with native CSS
variables; use CSS Modules for component styles. The same semantic role uses the
same shared visual rule across views. Mockups guide layout, not separate per-page
typography systems. Avoid repeated hardcoded values and copied components.
Use Lucide vector icons consistently; never use emojis as product UI icons.
Comments should explain non-obvious intent, not restate code.

## Phase boundaries

0: bootstrap/tooling (current); 1: design system; 2: contracts/API boundary/MSW;
3: shared UI/domain/map/journey components; 4: Plan a Journey; 5: Optimised Route;
6: Journey Details and modals; 7: Saved Journeys; 8: Shared Trips; 9: refactor,
QA and integration readiness. Later phases extend these boundaries as needed.
