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

- Production `CarePath.Web` chrome uses neutral company branding (`CarePath Health`) with
  role-aware portal subtitles. The wireframe's prototype view switcher (`Web operations` /
  `Caregiver mobile`) is an authoring control only and must not render in the shipped web app.
- App shell grid: `250px` fixed sidebar + `minmax(0,1fr)` main column
- Sidebar: `--teal-900` background, light text (`#e9f5f4`), section labels `#a7cecc`,
  active nav item `rgb(255 255 255 / 14%)` fill, count pills `#ffcfad`/`#542408`
- Top bar: `--surface`, `min-height: 76px`, bottom border `--line`, search field
  max-width 520px, user menu right-aligned
- Content: `--surface-alt` background; KPI row `repeat(4, minmax(0,1fr))`, gap 15px;
  two-column detail layouts `minmax(0,1.65fr) / minmax(280px,.75fr)`, gap 20px
- User management access layout: directory/editor grid `minmax(0,1.35fr) / minmax(310px,.85fr)`;
  search controls stack below 1050px and role cards stack below 780px
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
`ApiErrorAlert`, `PagedTable`, `AuditTimeline`, `UserAccessDirectory`, `RoleAssignmentPanel`,
`RoleCatalog`. New shared visuals go in `CarePath.Client.UI`
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


## SaaS Journey (2026-09-08, production-style)

The wireframe opens on a nine-step **SaaS journey** rendered as production screens, not
annotated mockups. Copy is short and customer-facing; prototype-only controls (complete
readiness checks, preview service state) sit outside the screen window in the caption row
and must not ship.

1. **Welcome** — public site. No sign-in, no agency lookup. Members use their organization's
   address; CarePath staff get a "Platform sign-in" link to `manage.carepathhealth.com`.
2. **Platform sign-in** — operator accounts only; never opens an agency workspace.
3. **Organizations** — manage console: count tiles, `data-table` of organizations with
   status, readiness, members, created; "New organization"; sidebar Organizations, Operators,
   Audit log, Service status, Sign out.
4. **New organization** — legal name, display name, slug with live address preview, time
   zone, data region, first administrator email; Create / Cancel. Creates Pending /
   Provisioning.
5. **Organization** — detail with address, region, administrator; readiness rows (address,
   database, encryption/backup/monitoring, administrator invitation); Set maintenance and
   Suspend enabled once Ready.
6. **Agency sign-in** — branded with the saved display name, monogram and support contact.
7. **Agency setup** — display name, monogram, logo upload/replace/remove, four approved
   theme token pairs, support email and phone, live sign-in preview, Save / Cancel with
   unsaved-changes state. Onboarding sidebar: numbered getting-started path, locked
   workspace sections, Sign out.
8. **Daily operations** — overview tiles and action rows; full workspace sidebar.
9. **Access & recovery** — Maintenance, Access changed, Service outage, Recovery messages
   with support contact and "Return to sign-in".

There is no organization switcher anywhere; an agency is reached only through its own
address. Only existing tokens are used (`data-table`, `form-field`, `button`, `status`,
teal palette). Journey-specific layout classes are scoped to the journey tab; extract
approved shared patterns before implementing these screens in application projects.
Layouts collapse to one column under 780px and keep labeled inputs, focus rings and
text status labels.

## Confirmation Dialog Pattern (platform console, 2026-09-08)

Source of truth: `carepath-wireframe.html`, SaaS journey step 5 (Organization), Suspend and
Set maintenance actions and their reverse actions. Shared implementation belongs in
`CarePath.Client.UI` as one `ConfirmDialog` component; app projects supply content only.

### When to use

Any platform-operator or organization-admin action that changes who can access a workspace,
stops jobs, or is recorded in the audit log: suspend, reactivate, set maintenance, end
maintenance, deactivate membership, change role. Never for reversible edits that have their
own Save / Cancel (agency setup).

### Anatomy (top to bottom)

1. **Eyebrow** — breadcrumb context, `.eyebrow` (`Organizations / BrightCare`).
2. **Title** — imperative, names the target: `Suspend BrightCare`, `Set BrightCare to maintenance`.
3. **Lead** — one sentence on what the action means for members, `--muted`.
4. **Impact panel** — "What happens" with 2 to 4 bullets. Tone by severity:
   neutral (`--surface-alt` / `--line`), warn (`--amber-soft` / `--amber`), danger
   (`--red-soft` / `--red`). Numbers come from live data (session count).
5. **Fields** — only what the audit event or the member-facing message needs.
6. **Error line** — `aria-live="polite"`, `--red`, appears only after a failed submit.
7. **Footer** — right-aligned: Cancel (`button-secondary`) then the confirming action.
   Destructive confirm uses `button-danger` (`--red` fill, white text); non-destructive uses
   `button-primary`. The confirm label repeats the verb and object; never "OK" or "Yes".

### Tokens and layout

- Native `<dialog>` with `showModal()`; backdrop `rgb(7 63 67 / 45%)` (teal-900 at 45%).
- Panel: `--surface`, `--radius`, `--shadow`, width `min(560px, 100vw - 32px)`, padding 24/28.
- Inputs: `--line` border, `--radius`, focus ring `--focus` 3px offset 2px.
- Under 560px: footer buttons stack full width.
- Inline destructive trigger on the page (`Suspend`) is an outlined `--red` button; the filled
  `--red` button appears only inside the dialog.

### Behavior

- Opens with focus on the first field; Escape and Cancel close with no change; clicking the
  backdrop cancels. Focus returns to the page heading after confirm, to the trigger after cancel.
- Confirm is disabled until required fields are valid. Suspend additionally requires typing
  the organization slug; the button stays disabled until it matches exactly.
- Submit with invalid fields shows the error line and focuses the first invalid field; the
  dialog never closes on failure.
- On confirm the page re-renders: status badges update, the action row shows "Last action"
  with an Audited badge, and the trigger buttons swap to the reverse action.
- Reason text is written to the platform audit log and is never shown to members. The
  maintenance message is the only member-facing text and is limited to 200 characters.

### The four dialogs

| Dialog | Trigger state | Tone | Fields | Confirm | Result |
|---|---|---|---|---|---|
| Set maintenance | Active + Ready | warn | Expected duration (select), Message to members (required, ≤200), Reason (required, ≤120) | Set maintenance | ReadinessState = Maintenance; sessions kept; jobs paused |
| End maintenance | Maintenance | neutral | Reason (required) | End maintenance | ReadinessState = Ready after readiness re-check |
| Suspend | Active + Ready | danger | Reason (select: Customer request, Agreement ended or unpaid, Security incident, Compliance hold, Other), Details (required, ≤300), Type slug to confirm | Suspend organization | Status = Suspended; all sessions revoked; sign-in, refresh and requests refused; jobs stopped; nothing deleted |
| Reactivate | Suspended | neutral | Reason (required) | Reactivate organization | Status = Active after readiness re-check; nobody signed in automatically |

Copy rules: say what members will experience, state that no data is deleted, and never
mention databases, tokens, routing or schema versions.

### Accessibility

- `aria-labelledby` points at the dialog title; the impact list is a real `<ul>`.
- All controls reachable by keyboard in visual order; the disabled confirm button remains in
  the tab order with `aria-disabled` semantics via `disabled`.
- Status is conveyed by text labels, never colour alone.
