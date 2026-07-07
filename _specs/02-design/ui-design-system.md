# CarePath UI Design System

Status: Approved (Tobi, 2026-07-06)
Source of truth: `Documentation/Wireframes/carepath-wireframe.html`
Shared implementation: `CarePath.Client.UI/wwwroot/carepath-ui.css`
Governing decision: D-S6-9 (`_specs/sprints/sprint-06-tasks.md`)

This spec applies to EVERY CarePath UI surface: the Blazor WebAssembly web app (Sprint 6),
the MAUI Blazor Hybrid mobile app (Sprint 7), and anything after. The wireframe governs;
this document transcribes it. When the two disagree, the wireframe wins — update this file
and the extracted CSS, never the other way around.

## The One Rule

**Nobody invents visual design in code.** No hard-coded hex colors, font families, radii, or
shadows in any `.razor`, `.cs`, or app stylesheet. Everything visual comes from the CSS custom
properties extracted from the wireframe. A screen that needs something the wireframe doesn't
define is a STOP: flag for PM/wireframe update first, extract second, implement third.

## Design Tokens (extracted verbatim)

| Token | Value | Use |
|---|---|---|
| `--ink` | `#172525` | Primary text |
| `--muted` | `#60706e` | Secondary text, labels, meta |
| `--line` | `#cbd6d3` | Borders, dividers, table rules |
| `--surface` | `#ffffff` | Cards, tables, top bar |
| `--surface-alt` | `#f3f7f6` | Content background, subdued fills |
| `--teal-900` | `#073f43` | Sidebar background, brand, primary-dark |
| `--teal-700` | `#08737b` | Primary actions, links, info accents |
| `--teal-100` | `#d9eff0` | Info-soft fills (badges, pager) |
| `--orange` | `#c65323` | Escalation/attention actions |
| `--orange-soft` | `#fff0e8` | Attention-soft fills |
| `--green` / `--green-soft` | `#24734f` / `#e2f3e9` | Success text/fill |
| `--amber` / `--amber-soft` | `#8a6100` / `#fff4cf` | Warning text/fill |
| `--red` / `--red-soft` | `#a33131` / `#fde8e8` | Danger/error text/fill |
| `--shadow` | `0 18px 48px rgb(7 63 67 / 12%)` | Card elevation |
| `--radius` | `14px` | Cards, tables, banners |
| `--focus` | `#ff8a3d` | Focus outline on ALL interactive elements |

## Typography

- Family: `Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif`
- `h1`: `clamp(1.7rem, 3vw, 2.35rem)`, line-height 1.16 — page titles
- `h2`: `1.16rem` — section headings; `h3`: `1rem`
- KPI/metric values: `1.9rem`, weight 850
- Body: default size, `--ink`; secondary/meta: `--muted`, ~0.85–0.9rem
- Badges: `0.8rem`, weight 700

## Layout (web)

- App shell grid: `250px` fixed sidebar + `minmax(0,1fr)` main column
- Sidebar: `--teal-900` background, light text (`#e9f5f4`), section labels `#a7cecc`,
  active nav item `rgb(255 255 255 / 14%)` fill, count pills `#ffcfad`/`#542408`
- Top bar: `--surface`, `min-height: 76px`, bottom border `--line`, search field
  max-width 520px, user menu right-aligned
- Content: `--surface-alt` background; KPI row `repeat(4, minmax(0,1fr))`, gap 15px;
  two-column detail layouts `minmax(0,1.65fr) / minmax(280px,.75fr)`, gap 20px
- Cards: padding 20px, `--surface`, `--line` border, `--radius`, `--shadow`

## Interaction & Accessibility (pairs with D-S6-7)

- Buttons: min-height `44px` (touch target), padding `10px 16px`, radius 8px, weight 700;
  primary = `--teal-700` (hover `--teal-900`), attention = `--orange`,
  success = `--green`, destructive-soft = `--red` on `--red-soft`
- Focus: `outline: 3px solid var(--focus); outline-offset: 2px` on every interactive element
- All status communicated by color must also carry text (badges always have labels)
- Contrast: token pairs above are the approved combinations; don't remix soft fills with
  other text colors

## Status Color Semantics (BadgeTone mapping)

| Tone | Text / Fill | Used for |
|---|---|---|
| Neutral | `--muted` / `--surface-alt` + `--line` border | Draft, Cancelled, Rejected |
| Info | `--teal-700` / `--teal-100` | Scheduled, Sent, Completed plans, Pending docs |
| Success | `--green` / `--green-soft` | Completed, Paid, Active, Approved, Low risk |
| Warning | `--amber` / `--amber-soft` | InProgress, PartiallyPaid, PendingVerification, Medium risk, low-confidence |
| Danger | `--red` / `--red-soft` | NoShow, Overdue, Failed, High risk, urgent escalations |

Canonical mappings live in `CarePath.Client.UI/Components/StatusBadgeTones.cs` — extend there,
never inline in pages.

## Component Inventory (shared, styled by carepath-ui.css)

`StatusBadge`/`RiskBadge` (soft pill), `KpiCard`, `ShiftCard`, `EscalationBanner` (left accent
bar: amber default, red for urgent/911), `PatientInstructionCard` (patient-safe DTO only),
`InstructionReviewCard` (amber-soft when low confidence), `ValidationErrorList`,
`ApiErrorAlert`, `PagedTable`, `AuditTimeline`. New shared visuals go in `CarePath.Client.UI`
with tokens — never one-off styles in app projects.

## Consumption

- Web (`CarePath.Web`): `<link href="_content/CarePath.Client.UI/carepath-ui.css" rel="stylesheet" />`
  in `index.html`; app CSS may add layout glue only (grid placement), no visual tokens.
- Mobile (Sprint 7, MAUI Blazor Hybrid): same stylesheet via static web assets; platform
  chrome follows the same tokens.

## Change Process

1. Change `Documentation/Wireframes/carepath-wireframe.html` (design decision, PM-reviewed).
2. Re-extract affected tokens/patterns into `carepath-ui.css` and update this spec.
3. Components/pages pick the change up via the custom properties — no page edits expected.
