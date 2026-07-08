# Sprint 6 Tasks - Blazor Web App MVP

Status: Approved
Parent spec: `_specs/sprints/sprint-06-blazor-web-mvp.md`
Last updated: 2026-07-08

Baseline: Sprints 1-5 complete; the full backend exists and its contract surface is FROZEN for
this sprint except where a D-S6 decision below explicitly authorizes an addition. The web app
consumes `CarePath.Contracts`, `CarePath.Client` (typed clients incl. `TransitionsClient`), and
`CarePath.Client.UI` — never Domain entities, never hand-rolled HTTP. Decisions D-S4-1..8 and
D-S5-1..8 remain normative for anything this sprint touches.

This board is the normative implementation contract for Sprint 6.

---

## Decisions

### D-S6-1 - Project layout and ownership

- New project `CarePath.Web` (Blazor WebAssembly standalone, net9.0): references
  `CarePath.Contracts`, `CarePath.Client`, `CarePath.Client.UI` ONLY. Owner: **Codex** (pages,
  layouts, auth wiring, DI, routing are app-layer integration work).
- `CarePath.Client.UI` (shared primitives) and `CarePath.Client` remain **Claude-owned**; new
  components and typed-client additions land there so Sprint 7 mobile reuses them.
- Per the Sprint 3 shared-client rules: full pages live in `CarePath.Web` and are NOT shared;
  primitives, formatting, and validation display live in `Client.UI` and ARE shared.

### D-S6-2 - Authentication (closes a real gap: no login endpoint exists)

- Codex adds `AuthController` to WebApi: `POST /api/auth/login` (email + password ->
  `AuthTokenResponse`) and `POST /api/auth/refresh`, built on the existing Sprint 3
  `IIdentityService`/`IJwtTokenService`. Anonymous-allowed, rate-limit friendly, lockout
  honored; failures return a single generic `auth.invalid_credentials` code — never
  user-exists/password-wrong distinctions, never lockout state to unauthenticated callers.
- Contracts additions (Claude): `LoginRequest`, `RefreshTokenRequest`, `AuthTokenResponse`
  (access token, expiry, refresh token, `UserRole`, display name). `CarePath.Client` gets
  `AuthClient` + a WASM-friendly in-memory `IAccessTokenProvider` implementation.
- **Token storage: in-memory only.** No tokens in localStorage/sessionStorage (aligns with the
  browser-PHI rule and keeps XSS blast radius small). Session ends on tab close; refresh-token
  persistence UX is a Sprint 7 decision, not an MVP need.

### D-S6-3 - Browser PHI safety (parent spec Story 6, made concrete)

- Route URLs carry Guids only — never names, diagnoses, medications, or discharge text.
- No PHI in `console.*` output: a global Blazor error boundary renders a generic message and
  logs sanitized text (exception type + TraceId-style correlation only, never DTO contents).
  `Console.WriteLine`/`ILogger` calls in Web must never serialize DTOs.
- localStorage/sessionStorage: non-PHI UI preferences only (page size, nav state). Nothing
  else. No analytics/telemetry SDKs in the MVP at all.
- Exit gate check greps `CarePath.Web` for console/serialization patterns and storage writes.

### D-S6-4 - Component set and naming (amends parent spec task list)

- Parent spec's `StatusPill` is satisfied by the existing `StatusBadge` + `StatusBadgeTones`
  (no duplicate primitive). Tone mappings extend to Transitions enums
  (`TransitionPlanStatus`, `TransitionRiskLevel`, `ReminderStatus`, `EscalationLevel`,
  `DischargeDocumentStatus`, `TransitionInstructionStatus`).
- New Client.UI primitives: `KpiCard`, `RiskBadge`, `ShiftCard`, `EscalationBanner`,
  `PatientInstructionCard` (renders patient-facing/care-team instruction DTOs only — its
  parameter type is `TransitionInstructionPatientFacingDto`, making clinical-content leakage a
  compile error), `InstructionReviewCard` (clinical DTO, low-confidence flagging, review
  actions as EventCallbacks), and `AuditTimeline`.
- `AuditTimeline` is a presentation primitive fed by page-supplied entries. There is NO
  audit-log read API and none is added this sprint — wiring it to real audit data is deferred
  (admin feature, post-MVP). It ships with the component library for layout completeness only.

### D-S6-5 - Escalation queue endpoint (authorized API addition)

Story 4 needs an org-wide coordinator escalation queue; the backend only exposes per-plan
escalations. Codex adds `GET /api/transitions/escalations?openOnly=true` (Coordinator;
`PagedRequest` -> `PagedResult<TransitionEscalationDto>`; filtered query scoping; audited reads;
`GetPagedAsync`). Claude adds the matching `TransitionsClient.GetEscalationQueueAsync`. This is
the ONLY new non-auth endpoint authorized this sprint.

### D-S6-6 - Component and page testing

bUnit (new CPM PackageVersion) for Client.UI primitives and critical Web pages: review queue
renders low-confidence flags; activation button disabled while any instruction is Pending;
escalation acknowledge flow; PHI-exposure assertions (rendered markup for care-team/patient
components never contains SourceText/confidence). Playwright/E2E is out of scope (Sprint 7
hardening may add it).

### D-S6-8 - Admin user ROLES and ACCOUNT STATUS management (refined 2026-07-06 per Tobi review)

SCOPE PRECISION: this is **role and account-status management**, NOT a permissions system.
A user has exactly one `UserRole`; what each role may do stays hard-coded in policies and
authorization services. Granular per-permission toggles are explicitly DEFERRED (post-MVP; a
future spec if ever needed). Any implementation drift toward "permissions" stops for PM review.

- **Endpoints (Admin policy):**
  - `GET /api/admin/users?pageNumber=&pageSize=&role=&isActive=&search=` ->
    `PagedResult<UserAccountDto>`. `role` (optional `UserRole`), `isActive` (optional bool),
    `search` matches **email and display name ONLY** — never phone, address, or any PHI field.
  - `GET /api/admin/users/roles` -> all available `UserRole` values for the admin role
    assignment dropdown.
  - `POST /api/admin/users` (staff provisioning: Coordinator, Clinician, FacilityManager,
    Admin — NOT Caregiver/Client, which have their own D-S4-5 profile workflows) -> `UserAccountDto`.
  - `PUT /api/admin/users/{id}/role` (`UpdateUserRoleRequest`) -> `UserAccountDto`.
  - `PUT /api/admin/users/{id}/status` (`UpdateUserStatusRequest`, activate/deactivate) -> `UserAccountDto`.
  - Supersedes D-S6-5's "only new endpoint" line — authorized additions are now: auth
    (D-S6-2), escalation queue (D-S6-5), admin users (D-S6-8), and caregiver/schedule
    read-match endpoints required by D-S6-10.
- **`UserAccountDto` (normative shape):** `Id`, `Email`, `DisplayName`, `Role`, `IsActive`,
  `LastLoginAt?`, `HasCaregiverProfile`, `HasClientProfile`, `CanChangeRole`, `CanDeactivate`,
  `DisabledReason?` — the three action fields are computed server-side from the guardrails so
  the UI renders disabled-with-reason without duplicating rules client-side.
- **Single-role model holds**: "update role" changes Domain `User.Role` + ASP.NET Identity
  role atomically (rollback on partial failure); "remove access" = deactivate (`IsActive =
  false`, login then fails with the generic auth code) — never hard delete, never role-less.
- **Guardrails (Application-enforced, tested):** cannot demote or deactivate the last active
  Admin; users with a Caregiver/Client profile cannot be role-changed away from the profile's
  role (deactivate instead); staff provisioning follows D-S4-5 (temp password from request,
  rollback, no credential material anywhere).
- **Actor DB re-check (task-level requirement):** every admin-users command handler verifies
  from the DATABASE that the acting user is currently an active Admin — JWT claims alone are
  insufficient (a demoted/deactivated admin's live token must not administer users).
- **Token staleness:** role changes apply at the target's next login/refresh; the page states
  "changes apply at next sign-in".
- **Audit (security/admin events, not PHI):** reuse the existing `IPhiAuditLogger` pipeline
  explicitly, with `EntityType = UserAccount` and actions `StaffProvisioned` / `RoleChanged`
  (old + new role enum values) / `AccountActivated` / `AccountDeactivated`. Values are actor
  ID, target user ID, role enum values, timestamp, TraceId — never emails or names in audit
  values or log statements.
- **Seeder extension (S6-TASK-013):** `coordinator@carepath.local` + `clinician@carepath.local`
  dev accounts alongside the existing three.

### D-S6-9 - Wireframe is the UI source of truth (added 2026-07-06 per Tobi)

`Documentation/Wireframes/carepath-wireframe.html` is the SOURCE OF TRUTH for all UI design:
colors, typography, spacing, layout, and interaction patterns. Full transcribed spec:
`_specs/02-design/ui-design-system.md` (tokens, typography, layout metrics, tone semantics,
change process). Rules:

- Its design tokens are extracted verbatim into `CarePath.Client.UI/wwwroot/carepath-ui.css`
  (`--ink`, `--muted`, `--line`, `--surface`/`--surface-alt`, `--teal-900/700/100`,
  `--orange(-soft)`, `--green(-soft)`, `--amber(-soft)`, `--red(-soft)`, `--shadow`,
  `--radius: 14px`, `--focus: #ff8a3d`). Font: Inter with the wireframe's fallback stack.
  Nobody invents colors/fonts in code — change the wireframe first, then re-extract.
- `CarePath.Web` links the shared stylesheet (`_content/CarePath.Client.UI/carepath-ui.css`)
  and implements page LAYOUT per the wireframe: teal-900 sidebar with role-scoped nav,
  white top bar with `--line` bottom border, `--surface-alt` content background, card grids
  with `--radius`/`--shadow`, and the wireframe's `--focus` outline on all interactive
  elements.
- Badge tone -> wireframe color mapping (already applied in the shared CSS):
  Success=green/green-soft, Warning=amber/amber-soft, Danger=red/red-soft,
  Info=teal-700/teal-100, Neutral=muted/surface-alt.
- Screens that exist in the wireframe (Overview, Schedule, Coverage queue, Assign caregiver,
  Caregivers roster/profile detail, Add caregiver, Add certifications, Schedule eligible shifts,
  review queue, escalations) follow its structure. Screens it lacks reuse its patterns
  (top-bar + filter row + card/table grid) and get added to the wireframe before implementation.
- Exit verification includes a side-by-side wireframe-vs-app pass per screen.

### D-S6-10 - Caregiver management and shift-assignment flow (added 2026-07-08 per Tobi)

The caregiver and scheduling UX follows the updated wireframe exactly:

- `Caregivers` roster columns are `Name`, `Type`, `Rating`, `Status`, `View`. Do not restore
  roster columns for certifications, pay rate, or MTD shifts unless the backend summary DTO is
  intentionally expanded in a future decision.
- `View` opens the right-side `Profile detail` panel. The panel shows identity, contact,
  employment/availability, skills, certifications, performance, Admin/Coordinator pay rate, and
  check-in-derived `Shifts (MTD)`/`Billable hours (MTD)`.
- `+ Add caregiver` from the Caregivers page and Overview quick actions opens Step 1:
  `page-caregiver-create`, mapped field-for-field to `CreateCaregiverRequest`. No certification
  fields belong in Step 1, and the temporary password is never displayed outside the password
  input.
- Step 2 `page-caregiver-certification` supports multiple certifications. The UI may save one
  `AddCertificationRequest` at a time, but it must keep a saved-list state and allow
  "Save and add another" before continuing.
- Step 3 `page-caregiver-eligible-shifts` is the caregiver-filtered scheduling shortcut. It
  lists open shifts that match the selected caregiver's credentials, skills, availability, active
  status, weekly capacity, and double-booking checks.
- The Schedule page remains the primary place to assign caregivers to shifts. Its `Coverage
  queue` and `Assign caregiver` page are the shift-first workflow; caregiver profile Step 3 is a
  filtered shortcut back into the same assignment rules.
- Current backend shape: `CreateShiftRequest` creates a scheduled shift with a required
  `CaregiverId`; `UpdateShiftRequest` can assign, reassign, or clear `CaregiverId`. Sprint 6 may
  add read/match endpoints or DTOs for open shifts/eligible caregivers, but must not bypass the
  existing shift validation guards.
- `Shifts (MTD)` is not `Caregiver.TotalShiftsCompleted`. It is a current-month metric derived
  from successful caregiver check-in/out records and returned only through an authorized
  Admin/Coordinator profile or scheduling DTO.

### D-S6-7 - Accessibility baseline

Every interactive primitive: keyboard operable, labeled (aria-label or visible label), visible
focus state, and WCAG AA contrast for the built-in badge/banner tones. Checked per component in
review; a page-level keyboard-only walkthrough of the two core workflows (review->activate,
escalation->acknowledge) is part of exit verification.

---

## Implementation Slices

| Slice | Goal | Includes |
|---|---|---|
| 6A | Auth endpoint + Web scaffold + authenticated layout/role nav | S6-TASK-010..012 |
| 6B | Contracts/Client auth additions + Client.UI component expansion | S6-TASK-020..021 |
| 6C | Coordinator dashboard | S6-TASK-030 |
| 6D | Schedule board, coverage queue, shift assignment | S6-TASK-031, S6-TASK-038 |
| 6H | Caregiver roster, profile detail, create/certify/eligible-shifts flow | S6-TASK-039, S6-TASK-041..043 |
| 6E | Transitions: review queue, activation, escalation queue (+ D-S6-5 endpoint) | S6-TASK-032..035 |
| 6G | User management per D-S6-8: seeder, endpoints, contracts/client, Users page | S6-TASK-013, 023, 036..037 |
| 6F | Accessibility pass + browser PHI safety review + exit verification | S6-TASK-040, S6-TASK-050..060 |

## Screen Matrix

| Screen | Role(s) | Data sources / typed clients | Notes |
|---|---|---|---|
| Login | anonymous | `AuthClient` (D-S6-2) | Generic failure message only |
| Overview | Coordinator, Admin | ShiftsClient (open/covered shifts), CaregiversClient (active/expiring cert counts), TransitionsClient (open escalations), billing summaries when available | Must match wireframe dashboard; quick action `+ Add caregiver` routes to Step 1 create |
| Schedule board | Coordinator, Admin | ShiftsClient paged board/list | Weekly board, open/unassigned styling, coverage queue entry point; guard errors surfaced via ApiErrorAlert |
| Coverage queue | Coordinator, Admin | ShiftsClient open shifts + eligible caregiver/match DTOs from S6-TASK-038 | Shift-first workflow; `Assign caregiver` opens assignment page |
| Assign caregiver | Coordinator, Admin | ShiftsClient CreateShift/UpdateShift; eligible caregivers from S6-TASK-038; CaregiversClient summaries/details as needed | Creates assigned shifts or reassigns existing shifts; uses existing double-booking/certification guards |
| Caregivers roster | Coordinator, Admin | CaregiversClient paged `CaregiverSummaryDto` | Columns exactly `Name`, `Type`, `Rating`, `Status`, `View`; no pay/certification/MTD columns |
| Caregiver profile detail panel | Coordinator, Admin | CaregiversClient detail/profile DTO from S6-TASK-039 | Shows contact, employment, pay rate, skills, certifications, performance, check-in-derived MTD metrics; Add certification and Schedule eligible shifts actions |
| Add caregiver Step 1 | Coordinator, Admin | CaregiversClient.CreateAsync (`CreateCaregiverRequest`) | Account, employment, skills, availability only; no certification fields; temporary password not echoed |
| Add certifications Step 2 | Coordinator, Admin | CaregiversClient.AddCertificationAsync (`AddCertificationRequest`) | Supports multiple saved certifications before continuing; backend calls may save one record at a time |
| Schedule eligible shifts Step 3 | Coordinator, Admin | Eligible open shifts DTOs from S6-TASK-038 + ShiftsClient assignment commands | Caregiver-filtered shortcut into shift assignment; same eligibility rules as Schedule |
| Credentials | Coordinator, Admin | CaregiversClient expiring certifications | Expiry badges via StatusBadgeTones; remains the compliance/credential review view, not caregiver create |
| Review queue | Clinician | TransitionsClient plans (PendingVerification filter) + GetPlanAsync | InstructionReviewCard; low-confidence flagged |
| Activation screen | Clinician | TransitionsClient ReviewInstruction/ActivatePlan | Activate disabled until no Pending instructions; e-sign confirm checkbox maps to `ConfirmESignature` |
| Escalation queue | Coordinator | TransitionsClient GetEscalationQueueAsync (D-S6-5) + Acknowledge | EscalationBanner; resolution note + level per D-S5-7 |
| Users (admin) | Admin | AdminUsersClient (D-S6-8) | Create staff accounts; change role; activate/deactivate; last-Admin and profile-role guardrails surfaced as disabled actions with explanations; "changes apply at next sign-in" notice |
| Margin snapshot (stretch) | Admin | BillingClient margin endpoints | Only if 6C-6E land early; Admin-only route guard |

## Caregiver/Schedule API Integration Matrix

| Wireframe page state | Required contract/client work | Write path |
|---|---|---|
| `page-caregivers` roster | Existing `CaregiversClient.GetPageAsync(PagedRequest)` returning `CaregiverSummaryDto`; keep summary lean | None |
| `Profile detail` panel | S6-TASK-039 updates/adds authorized caregiver profile detail DTO with `HourlyPayRate`, check-in-derived `ShiftsMtd`, `BillableHoursMtd`, certifications, skills, availability, rating, completed shifts, and no-shows | `CaregiversClient.AddCertificationAsync` from panel action; update profile remains `CaregiversClient.UpdateAsync` |
| `page-caregiver-create` | Existing `CreateCaregiverRequest`; Web form maps every field and no certification fields | `CaregiversClient.CreateAsync` |
| `page-caregiver-certification` | Existing `AddCertificationRequest`; Web maintains multi-cert saved-list state and calls the endpoint once per saved credential | `CaregiversClient.AddCertificationAsync` |
| `page-caregiver-eligible-shifts` | S6-TASK-038 adds eligible open shifts for caregiver DTO/client method; response includes match reasons, blocking reasons, requirement labels, and assignability | Assign/reassign through `ShiftsClient` create/update methods only |
| Schedule `Coverage queue` | S6-TASK-038 adds open shift coverage DTO/client method; response includes requirement, time, best matches, and coverage status | Assign/reassign through `ShiftsClient` create/update methods only |
| `page-shift-assign` | S6-TASK-038 adds eligible caregivers for shift DTO/client method; existing shift requests still own writes | `ShiftsClient.CreateAsync` for new assigned shifts; `ShiftsClient.UpdateAsync` for assignment/reassignment/clear |

### Final S6-TASK-038/039 contract surface (locked 2026-07-07, Claude + Codex)

Routes, DTOs, and typed-client methods below are the agreed shapes; Web pages build against
these and nothing else. All four reads are `Admin,Coordinator` role-gated and paged via
`PagedRequest` (`pageNumber`/`pageSize` only).

| Route | Response | Typed client method |
|---|---|---|
| `GET /api/caregivers/{id}` | enriched `CaregiverDetailDto` | `CaregiversClient.GetAsync` |
| `GET /api/shifts/coverage?pageNumber=&pageSize=` | `PagedResult<OpenShiftCoverageDto>` | `ShiftsClient.GetCoverageQueueAsync` |
| `GET /api/shifts/{id}/eligible-caregivers?pageNumber=&pageSize=` | `PagedResult<EligibleCaregiverDto>` | `ShiftsClient.GetEligibleCaregiversAsync` |
| `GET /api/caregivers/{id}/eligible-shifts?pageNumber=&pageSize=` | `PagedResult<EligibleOpenShiftDto>` | `CaregiversClient.GetEligibleOpenShiftsAsync` |

Decisions baked into this surface:

- **No separate `/profile` route.** S6-TASK-039 enriched the existing `CaregiverDetailDto`
  (adds `IsActive`, `HourlyPayRate`, check-in-derived `ShiftsMtd`/`BillableHoursMtd`) instead
  of a parallel profile DTO; the profile panel loads `GET /api/caregivers/{id}`.
- **Coverage queue has no `fromUtc`/`toUtc` filters in the MVP.** The server scopes to open
  (`CaregiverId == null`, `Scheduled`) shifts; date filtering is a future addition if the
  board needs it.
- **All three matching DTOs are rate-free** (`OpenShiftCoverageDto`, `EligibleCaregiverDto`,
  `EligibleOpenShiftDto`). Assignment does not need to round-trip rates: `UpdateShiftAsync`
  preserves the shift's existing `BillRate`/`PayRate` when the request sends 0. The only
  approved rate fields outside Admin margin DTOs are `CaregiverDetailDto.HourlyPayRate`
  (profile detail) — pinned in `DomainToContractMapperTests`.
- **Match strength is derived, not a field.** `IsAssignable == true` renders Strong (green);
  `IsAssignable == false` renders Blocked (red) with `BlockingReasons`. The wireframe's amber
  "Time risk" middle state needs a soft-warning list the eligibility result does not emit yet
  — deferred unless a follow-up decision adds it (no schema change required).
- Route/role/template shapes are pinned by `Sprint4ControllerContractTests` and
  `Sprint6ClientRouteAlignmentTests` (client URL ↔ controller template alignment).

---

## Task Board

Owners: **Claude** = PM/Contracts/Client/Client.UI + sprint docs. **Codex** = WebApi additions +
`CarePath.Web`. **Tobi** = approvals.

| ID | Task | Owner | Depends on | Status |
|---|---|---|---|---|
| S6-TASK-001 | Approve this board + decisions D-S6-1..8 | Tobi | - | Done 2026-07-06 (D-S6-8 added and refined post-approval per Tobi review) |
| S6-TASK-010 | WebApi `AuthController` per D-S6-2 (login/refresh on existing token services; generic failure code; lockout honored; no credential material logged) + tests | Codex | S6-TASK-001 | Done 2026-07-06 (`feat: auth login/refresh endpoints (S6-TASK-010) + auth contracts (S6-TASK-020 partial)`) |
| S6-TASK-011 | `CarePath.Web` scaffold: Blazor WASM, references Contracts/Client/Client.UI only, sln entry under src, DI for typed clients + `AuthorizationMessageHandler` | Codex | S6-TASK-001 | Done 2026-07-07 — scaffold committed; `AuthClient` + Client-owned in-memory token provider wired after S6-TASK-020 landed; verified by full-sln build/test 0 warnings |
| S6-TASK-012 | Authenticated layout + role-based navigation + global sanitized error boundary per D-S6-3 | Codex | S6-TASK-011, S6-TASK-021 | Pending |
| S6-TASK-013 | Seeder: add `coordinator@carepath.local` + `clinician@carepath.local` dev accounts per D-S6-8 (dev-only, same secret password source). Standalone task | Codex | S6-TASK-001 | Pending |
| S6-TASK-023 | Contracts: `UserAccountDto` (normative D-S6-8 shape incl. `CanChangeRole`/`CanDeactivate`/`DisabledReason`), `CreateStaffUserRequest`, `UpdateUserRoleRequest`, `UpdateUserStatusRequest`; Client: `AdminUsersClient` incl. list filters (role/isActive/search) | Claude | S6-TASK-001 | Code complete 2026-07-06 — `CarePath.Contracts/Admin/` (4 files, normative DTO shape) + `AdminUsersClient`; Done 2026-07-06 — verified by full-sln build 0 warnings; S6-TASK-036 is type-unblocked |
| S6-TASK-036 | WebApi/Application: admin users endpoints per D-S6-8 — Admin policy PLUS per-command actor DB re-check (active Admin from database, not JWT claims); atomic role re-sync with rollback; last-Admin + profile-role guardrails computed into the DTO action fields; search on email/display-name only; security/admin audit events via `IPhiAuditLogger` (IDs + role enums only); D-S4-5 provisioning rules; tests for every guardrail + the demoted-admin-live-token case | Codex | S6-TASK-023 | Pending |
| S6-TASK-037 | Web: Users page per D-S6-8 (list/filter/search, create staff, change role, activate/deactivate; renders `CanChangeRole`/`CanDeactivate`/`DisabledReason` as disabled-with-reason; next-sign-in notice) + bUnit tests | Codex | S6-TASK-012, S6-TASK-036, S6-TASK-021 | Pending |
| S6-TASK-020 | Contracts: `LoginRequest`, `RefreshTokenRequest`, `AuthTokenResponse`; Client: `AuthClient`, in-memory `IAccessTokenProvider` implementation | Claude | S6-TASK-001 | Code complete 2026-07-06 — contracts committed in `c42ed21`; `AuthClient` + `InMemoryAccessTokenProvider` (with `CurrentSession`/`CurrentRefreshToken` for the auth state provider) in working tree. Done 2026-07-06 — verified by full-sln build 0 warnings (f019081 report) |
| S6-TASK-021 | Client.UI: `KpiCard`, `RiskBadge`, `ShiftCard`, `EscalationBanner`, `PatientInstructionCard` (patient-safe DTO param), `InstructionReviewCard`, `AuditTimeline` (+ `AuditTimelineEntry` record); StatusBadgeTones extended to 6 Transitions enums; aria labels/roles + native-button keyboard operability per D-S6-7 | Claude | S6-TASK-001 | Done 2026-07-06 — 9 files + `wwwroot/carepath-ui.css` (D-S6-9 tokens); verified by full-sln build 0 warnings; stylesheet linked by CarePath.Web (`f019081`) |
| S6-TASK-022 | Client: `TransitionsClient.GetEscalationQueueAsync` for the D-S6-5 endpoint | Claude | S6-TASK-034 route shape | Pending |
| S6-TASK-030 | Web: Overview page per wireframe (KPI cards, today's schedule, needs-attention list, quick actions). `+ Add caregiver` quick action must route to `page-caregiver-create`; schedule links route to Schedule. Uses typed clients only and renders ApiErrorAlert for network/API failures | Codex | S6-TASK-012, S6-TASK-021 | Pending |
| S6-TASK-031 | Web: Schedule board + Coverage queue + Assign caregiver page per wireframe. Board shows open/unassigned styling; Coverage queue lists open shifts and best matches; Assign caregiver supports create assigned shift and update/reassign via `ShiftsClient`; guard errors (`shift.double_booked`, `caregiver.certification_expired`) surface through ApiErrorAlert | Codex | S6-TASK-012, S6-TASK-021, S6-TASK-038 | Pending |
| S6-TASK-032 | Web: clinician review queue (clinical DTOs; low-confidence flags; source text visible per D-S5-3) | Codex | S6-TASK-012, S6-TASK-021 | Pending |
| S6-TASK-033 | Web: activation screen (disabled until instructions terminal; ConfirmESignature; success -> plan Active state visible) | Codex | S6-TASK-032 | Pending |
| S6-TASK-034 | WebApi: escalation queue endpoint per D-S6-5 + tests (scoping, audit, paged) | Codex | S6-TASK-001 | Pending |
| S6-TASK-035 | Web: escalation queue screen (acknowledge with resolution note + human-decision level) | Codex | S6-TASK-034, S6-TASK-022 | Pending |
| S6-TASK-038 | Contracts/Client/WebApi/Application: shift assignment read/match APIs for the wireframe. Add paged Admin/Coordinator-safe endpoints/DTOs for open shifts, eligible caregivers for a shift, and eligible open shifts for a caregiver. Must use existing `CreateShiftRequest`/`UpdateShiftRequest` for writes, preserve double-booking/certification guards, avoid `GetAllAsync()` on shifts, and never log PHI shift/client values | Codex + Claude | S6-TASK-001 | In progress 2026-07-07 — contract surface locked (see "Final S6-TASK-038/039 contract surface"): 3 DTOs + `ShiftsClient.GetCoverageQueueAsync`/`GetEligibleCaregiversAsync` + `CaregiversClient.GetEligibleOpenShiftsAsync` + endpoints/eligibility in tree; route/role/shape tests added (`Sprint4ControllerContractTests`, `Sprint6ClientRouteAlignmentTests`, `DomainToContractMapperTests`) |
| S6-TASK-039 | Contracts/Client/WebApi/Application: caregiver Admin/Coordinator profile detail contract aligned to the wireframe. Expose pay rate only in authorized profile detail, not roster summaries; add check-in-derived `Shifts (MTD)` and `BillableHours (MTD)`; keep certifications in detail; update stale contract docs that say detail cannot include compensation; tests prove roster DTO still excludes pay/certification/MTD columns | Codex + Claude | S6-TASK-001 | In progress 2026-07-07 — `CaregiverDetailDto` enriched (`IsActive`, `HourlyPayRate`, `ShiftsMtd`, `BillableHoursMtd`); stale "no compensation" doc comment replaced; `GetCaregiverAsync` wired to check-in-derived MTD + read audit (cross-ownership hardening edit by Claude — Codex please review); roster exact-shape, detail-shape, MTD-source, and cert-separation tests added |
| S6-TASK-041 | Web: Caregivers roster + right-side Profile detail panel per wireframe. Roster columns exactly `Name`, `Type`, `Rating`, `Status`, `View`; `View` loads profile detail with contact, employment/pay rate, availability, skills, certifications, performance/MTD metrics, Add certification, and Schedule eligible shifts actions | Codex | S6-TASK-012, S6-TASK-021, S6-TASK-039 | Pending |
| S6-TASK-042 | Web: Add caregiver Step 1 + Add certifications Step 2 per wireframe. Step 1 maps field-for-field to `CreateCaregiverRequest` and never includes certifications; Step 2 saves one or more `AddCertificationRequest` records with a saved-list state, `Save and add another`, and `Continue to eligible shifts`; temporary password is never displayed after submit | Codex | S6-TASK-041 | Pending |
| S6-TASK-043 | Web: Schedule eligible shifts Step 3 per wireframe. After caregiver create/certification or from profile detail, list eligible open shifts for the selected caregiver, explain match reasons, and assign/review shifts through the same Schedule assignment API path; blocked/expired-cert cases render as non-assignable | Codex | S6-TASK-031, S6-TASK-038, S6-TASK-042 | Pending |
| S6-TASK-040 | bUnit test suite per D-S6-6 incl. PHI-exposure markup assertions. Must cover Overview quick action routing, Schedule coverage assignment, Caregiver roster/profile detail, Add caregiver Step 1, multi-certification Step 2, and eligible-shifts Step 3 in addition to Transitions pages | Codex (pages) + Claude (primitives) | S6-TASK-030..035, S6-TASK-037, S6-TASK-041..043 | Pending |
| S6-TASK-050 | Browser PHI safety review per D-S6-3: grep Web for console/DTO serialization/storage writes; verify URLs are Guid-only; keyboard-only walkthrough of review->activate, escalation->acknowledge, caregiver create->certify->eligible-shifts, and schedule coverage->assign workflows | Codex + Claude | S6-TASK-040 | Pending |
| S6-TASK-060 | Exit verification: build 0 warnings, all tests green, reviewer pass, exit-gate items checked; PROGRESS/lessons updated; PM closes after review | Codex + Claude + Tobi | all above | Pending |

### Success criteria (every task)

- Build zero warnings; tests green; reviewer clean; synthetic data only in tests/fixtures.
- `CarePath.Web` never references Domain/Application/Infrastructure/WebApi; no raw HttpClient
  calls outside `CarePath.Client`.
- No PHI in console output, storage, URLs, or error boundary text (D-S6-3).
- Patient/care-team components are typed to patient-safe DTOs (leakage = compile error).
- All list screens page via `PagedRequest`/`PagedTable` — no unbounded fetches.

- Every Web page task cites the exact `Documentation/Wireframes/carepath-wireframe.html` page
  state it implements and passes a side-by-side visual/layout review against that state.
- Caregiver roster, profile detail, create, certification, eligible-shifts, Schedule coverage,
  and Assign caregiver pages use typed clients only. Any missing API shape must be added to
  `CarePath.Contracts` + `CarePath.Client` before the page reaches beyond existing client APIs.
- Caregiver profile pay rate is authorized detail data only; it must not appear in roster,
  quick-search, generic summaries, logs, URLs, browser storage, or validation errors.
- `Shifts (MTD)` and `BillableHours (MTD)` are derived from current-month check-in/out shift
  records. Tests must fail if implementation uses `Caregiver.TotalShiftsCompleted` as the MTD
  source.

### Sequencing notes

- Critical path: 001 -> 010/011 -> 012 -> 030/038/039 -> 031/041 -> 042 -> 043 -> 040 -> 050 -> 060.
- Transitions branch remains: 032 -> 033 and 034 -> 022 -> 035.
- Claude's 020/021 start immediately after approval, parallel with Codex's 010/011 — 012
  needs 021's primitives, so Client.UI lands first.
- Cross-ownership hardening edits: propose in slice reports; owner applies (D-S4-8 note 3).
- Commit per slice, green build/tests; second-half owner commits coordinated changes together.

### Deferred (explicitly)

- Patient/family web or mobile UX, offline queue, push notifications -> Sprint 7.
- Refresh-token persistence UX, "remember me" -> Sprint 7 decision.
- Audit-log read API + real AuditTimeline wiring -> post-MVP admin feature.
- Playwright/E2E, analytics/telemetry -> Sprint 7 hardening (with PHI review).
- Margin dashboard beyond the stretch snapshot -> Sprint 7/post-MVP.
