# RouteWise design system

Phase 1 establishes visual foundations only. `frontend/src/styles/tokens.css`
is the value source of truth; this document defines usage. `global.css` loads the
local font and tokens once, then applies reset, body, link and keyboard-focus
rules. Product components begin in Phase 3.

**SAME SEMANTIC ROLE = SAME STYLE.** Screenshots guide layout and visual direction;
they do not justify page-specific colours, fonts, padding or other arbitrary values.

## Reference interpretation

All seven approved references were reviewed before choosing values:

| Reference | Shared direction retained |
| --- | --- |
| 01 Plan Journey | Blue selection, thin control borders, clear form grouping, roomy map context |
| 02 Optimised Route | Strong title, blue intermediate-stop identity, coral endpoints/route, restrained panels |
| 03 Journey Details | Title/section hierarchy, secondary metadata, subtle route-tinted surfaces |
| 04 Save Journey Modal | White dialog, compact form rhythm, dim backdrop, modest radius |
| 05 Share Journey Modal | Same dialog language, supporting copy, distinct labelled status treatments |
| 06 Saved Journeys | Clear card titles, thin edges, generous spacing, minimal elevation |
| 07 Shared Trips | Shared typography across lists/cards, blue emphasis, consistent actions |

The result is light, structured and desktop-first. Differences in title size,
blue shade, rounding and control density are normalised below. No map geometry,
imagery, logo, screen layout or behaviour is implemented or inferred. Tabs,
menus and drag handles in the references do not define functionality.

## Colour roles

Use semantic tokens directly; there is no large numeric palette to learn.
Names in this table omit the `--color-` prefix.

| Tokens | Values | Use |
| --- | --- | --- |
| `background`, `surface`, `surface-muted` | `#F8FAFC`, `#FFFFFF`, `#F1F5F9` | Canvas, panels, supporting areas |
| `surface-hover`, `surface-pressed` | `#F1F5F9`, `#E2E8F0` | Neutral interactive surfaces |
| `text`, `text-secondary`, `text-muted` | `#10172B`, `#536078`, `#667085` | Main text, supporting copy, captions/placeholders |
| `text-on-solid` | `#FFFFFF` | Text on primary/danger/route solid backgrounds |
| `primary`, `primary-hover`, `primary-active` | `#0062F5`, `#0053D6`, `#0044B3` | Actions, links, blue stop identity; hover/pressed variants |
| `selected-surface`, `selected-text`, `selected-border` | `#EAF2FF`, primary-active, primary | Persistent selection and active navigation |
| `route`, `route-surface`, `route-text` | `#D83A52`, `#FFF1F3`, `#A82C40` | Route/endpoints, light route tint, text on that tint |
| `danger`, `danger-hover`, `danger-active`, `danger-surface` | `#B42318`, `#912018`, `#7A271A`, `#FEF3F2` | Destructive actions and feedback |
| `error`, `error-surface` | danger, danger-surface | Validation feedback; independently named intent |
| `success`, `success-surface` | `#147D46`, `#EAF7EF` | Positive confirmation/status |
| `border`, `border-control`, `border-control-hover` | `#DCE2EC`, `#7C879B`, text-secondary | Structural edges, identifiable inputs, control hover |
| `disabled-surface`, `disabled-text`, `disabled-border` | surface-muted, `#8993A5`, border | Unavailable controls |
| `focus-ring` | primary-active | Keyboard focus on light surfaces |
| `overlay` | `rgb(16 23 43 / 48%)` | Dialog backdrop |

The blue preserves the references' cobalt character. Coral is slightly deeper
than some reference pins to support white details. **Route colour never means
deletion or an error.** Keep route and danger semantics separate. Use route-text
on route-surface, not route itself.

Muted text and control boundaries are stronger than the faintest mockup values
for readability. Structural borders are decorative; use border-control when a
boundary is needed to identify a control. A pale structural border must not be
the sole indicator of an input, toggle or selection.

Reference contrast checks (sRGB, rounded): white on primary **5.17:1**, white on
route **4.52:1**, muted text on background **4.75:1**, control border on muted
surface **3.31:1**, success on success-surface **4.70:1**, danger on danger-surface
**6.05:1**. These cover the listed pairings, not whole-app accessibility. Future
components must check actual adjacent colours. On map imagery, use contrasting
marker/route halos and explicit identity labels; white-background checks do not
establish map contrast.

## Typography

Inter Variable is self-hosted through `@fontsource-variable/inter` (OFL-1.1).
Its `wght.css` entry supplies normal-style weights with `font-display: swap` and
Unicode subsets. Vite bundles the WOFF2 files; browsers fetch needed subsets.
There are no runtime font CDN requests. Fallbacks are `'Segoe UI', Arial,
sans-serif`, including for characters outside Inter's coverage. A separate
italic font is not loaded because no visual role requires it yet.

The primitive size scale is **12/14/16/20/24/36px**, expressed in rem; weights
are **400/500/600/700**, line heights **1.2/1.3/1.5**. Pixel equivalents assume
the browser's default 16px root size. The root font size is not fixed.

| Semantic token | Size / line height | Weight | Colour / use |
| --- | --- | --- | --- |
| `--font-page-title` | 36px / 1.2 | 700 | text; every page title, including journey names |
| `--font-section-heading` | 24px / 1.3 | 600 | text; sections and future dialog titles |
| `--font-card-title` | 20px / 1.3 | 600 | text; card/list item headings |
| `--font-body` | 16px / 1.5 | 400 | text; paragraphs and entered field values |
| `--font-body-secondary` | 14px / 1.5 | 400 | text-secondary; supporting descriptions |
| `--font-label` | 14px / 1.5 | 600 | text; field labels |
| `--font-caption` | 12px / 1.5 | 500 | text-muted; compact metadata/captions |
| `--font-control` | 16px / 1.5 | 600 | action/selection colour; button/control labels |

Use `font: var(--font-page-title)` or the appropriate role. Font shorthands do
not set colour or tracking. Page/section headings additionally use
`--letter-spacing-heading: -0.025em`; other roles use normal tracking.
Headings inherit the base font until a visual role is applied, avoiding a second
browser-default scale. Choose heading HTML levels by document hierarchy, not
font size. Use sentence case; no global uppercase labels. If numeric alignment
is needed later, apply `tabular-nums` locally.

## Spacing and shared dimensions

`--space-0/1/2/3/4/6/8/12/16` map to **0/4/8/12/16/24/32/48/64px** (rem-based).
Use 4-8 for close relationships, 8 between label and control, 16 between fields,
24 for panel/card padding, 32 between sections and 48-64 for large gaps.
Choose from this scale rather than remeasuring each image.

| Token | Value | Convention |
| --- | --- | --- |
| `--control-height-compact` | 36px | Deliberately compact secondary contexts |
| `--control-height-default` | 44px | Default input/button/select/segmented-control target |
| `--control-height-large` | 48px | Prominent actions, used consistently |
| `--control-padding-inline` | 16px | Horizontal control inset |
| `--control-content-gap` | 8px | Icon-to-label gap |
| `--layout-sidebar-width` | 112px | Future desktop icon-and-label rail |
| `--layout-page-gutter` | `clamp(24px, 3vw, 48px)` | Shared fluid page inset using spacing tokens |
| `--layout-section-gap` | 32px | Major content sections |

Heights are **minimum block sizes**, not text-clipping fixed heights. Allow label
wrapping and text zoom. IconButton hit areas use a control size, not glyph size.
Phase 1 imposes no content-width cap, feature grid, split ratio or dialog width.

## Shape and depth

- `--radius-control`: **8px**; controls and future navigation selection.
- `--radius-surface`: **12px**; cards, panels, map containers and dialogs.
- `--radius-full`: **9999px**; circular indicators and pill statuses only.
- `--border-width`: **1px**; `--border-width-emphasis`: **2px** for state emphasis.
- `--shadow-none`: **none**; default for ordinary panels/cards.
- `--shadow-subtle`: **0 1px 2px / 6% navy**; small lifted controls if needed.
- `--shadow-overlay`: **0 16px 48px / 18% navy**; dialogs/popovers, not every card.

Use borders before shadows. Avoid gradients and decorative glows. State changes
must not move layout: reserve border space or use an inset state indicator
instead of changing outer dimensions.

## Icons and states

Use Lucide React, named imports and `currentColor`. **Never use emoji as product
UI icons.** `--icon-size-sm/md/lg` are **16/20/24px**; default is 20, compact
metadata 16, navigation/prominent controls 24. `--icon-stroke-width` is **2** SVG
units. Keep weight consistent instead of matching inconsistent screenshot fills.
No icon wrapper or logo component is created now. Later icon-only actions need
accessible names; decorative icons beside text must be hidden from assistive
technology.

| State | Future component rule |
| --- | --- |
| Default | surface/text with appropriate structural or control border |
| Hover / pressed | Matching primary/danger variants; surface-hover/pressed for neutral controls |
| Selected | selected-surface/text/border, or primary + text-on-solid for filled selection; add non-colour evidence |
| Focus-visible | Global 3px focus-ring outline with 3px offset; keep it visible and unclipped |
| Disabled | Disabled colours and correct semantics; suppress hover/active treatment, not surrounding explanations |
| Error | error border/text, optional error-surface, explanatory text |
| Success | success on success-surface, explicit label |
| Destructive | danger family; never route tokens |

Pointer `:active` is a momentary press, not persistent selection. Focus remains
visible alongside error/selection. Preserve inline link underlines. Status badges
may later use the selected, success and neutral treatments in the references;
colour never replaces a label or defines a data contract.

`--duration-fast`: **120ms**; `--duration-normal`: **180ms**;
`--ease-standard`: **cubic-bezier(0.2, 0, 0, 1)**. Transition only changed
properties, not `all`. Both durations become **0ms** under reduced motion; do not
depend on transition completion for behaviour. Global focus uses system
`Highlight` in forced-colours mode; do not disable browser support for it.

Target 4.5:1 for ordinary text and 3:1 for identifiable control boundaries/focus
against adjacent colours. See [W3C text contrast guidance](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html)
and [non-text contrast guidance](https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html).
These are component review targets, not a claim of completed accessibility QA.

## Desktop responsiveness and usage

Use shared gutters and rem-based dimensions; no fixed viewport width or global
minimum-width lock. Later sidebar/content layouts need `min-width: 0`, wrapping
and intentional overflow. Check laptop/desktop widths such as 1024/1280/1440px
and browser zoom. The rail must accommodate label wrapping; feature phases decide
when columns stack or regions scroll. Do not shrink all text to fit a screenshot
or introduce mobile navigation here.

Use CSS Modules for components in Phase 3 onward. Example consumption:

```css
.title {
  font: var(--font-page-title);
  color: var(--color-text);
  letter-spacing: var(--letter-spacing-heading);
}
```

Reuse roles even when page layouts differ. Do not override root tokens per page,
copy raw palette values into modules or create global tokens for one-off feature
ratios. Add/document a token only when a real shared responsibility is missing.
No product components, screens, contracts/API, mocks, maps or business behaviour
are implemented in this phase.
