# Sprint 6 Tasks - Blazor Web App MVP

Status: Approved
Parent spec: `_specs/sprints/sprint-06-blazor-web-mvp.md`
Last updated: 2026-07-06

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
  - `POST /api/admin/users` (staff provisioning: Coordinator, Clinician, FacilityManager,
    Admin — NOT Caregiver/Client, which have their own D-S4-5 profile workflows) -> `UserAccountDto`.
  - `PUT /api/admin/users/{id}/role` (`UpdateUserRoleRequest`) -> `UserAccountDto`.
  - `PUT /api/admin/users/{id}/status` (`UpdateUserStatusRequest`, activate/deactivate) -> `UserAccountDto`.
  - Supersedes D-S6-5's "only new endpoint" line — authorized additions are now: auth
    (D-S6-2), escalation queue (D-S6-5), admin users (D-S6-8).
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
- Screens that exist in the wireframe (dashboard, scheduling, review queue, escalations)
  follow its structure; screens it lacks (e.g., the D-S6-8 Users page) reuse its patterns
  (top-bar + filter row + card/table grid) and get added to the wireframe afterward.
- Exit verification includes a side-by-side wireframe-vs-app pass per screen.

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
| 6D | Scheduling + credential screens | S6-TASK-031 |
| 6E | Transitions: review queue, activation, escalation queue (+ D-S6-5 endpoint) | S6-TASK-032..035 |
| 6G | User management per D-S6-8: seeder, endpoints, contracts/client, Users page | S6-TASK-013, 023, 036..037 |
| 6F | Accessibility pass + browser PHI safety review + exit verification | S6-TASK-040..060 |

## Screen Matrix

| Screen | Role(s) | Data sources (existing clients) | Notes |
|---|---|---|---|
| Login | anonymous | `AuthClient` (D-S6-2) | Generic failure message only |
| Coordinator dashboard | Coordinator, Admin | ShiftsClient (open shifts), VisitNotesClient via ShiftsClient summaries (overdue notes), CaregiversClient (expiring certs), TransitionsClient (plans page + escalation queue) | KpiCards + drill-down links |
| Schedule board | Coordinator, Admin | ShiftsClient paged + CreateShift/UpdateShift | Guard errors (`shift.double_booked`, `caregiver.certification_expired`) surfaced via ApiErrorAlert |
| Credentials | Coordinator, Admin | CaregiversClient expiring certifications | Expiry badges via StatusBadgeTones |
| Review queue | Clinician | TransitionsClient plans (PendingVerification filter) + GetPlanAsync | InstructionReviewCard; low-confidence flagged |
| Activation screen | Clinician | TransitionsClient ReviewInstruction/ActivatePlan | Activate disabled until no Pending instructions; e-sign confirm checkbox maps to `ConfirmESignature` |
| Escalation queue | Coordinator | TransitionsClient GetEscalationQueueAsync (D-S6-5) + Acknowledge | EscalationBanner; resolution note + level per D-S5-7 |
| Users (admin) | Admin | AdminUsersClient (D-S6-8) | Create staff accounts; change role; activate/deactivate; last-Admin and profile-role guardrails surfaced as disabled actions with explanations; "changes apply at next sign-in" notice |
| Margin snapshot (stretch) | Admin | BillingClient margin endpoints | Only if 6C-6E land early; Admin-only route guard |

---

## Task Board

Owners: **Claude** = PM/Contracts/Client/Client.UI + sprint docs. **Codex** = WebApi additions +
`CarePath.Web`. **Tobi** = approvals.

| ID | Task | Owner | Depends on | Status |
|---|---|---|---|---|
| S6-TASK-001 | Approve this board + decisions D-S6-1..8 | Tobi | - | Done 2026-07-06 (D-S6-8 added and refined post-approval per Tobi review) |
| S6-TASK-010 | WebApi `AuthController` per D-S6-2 (login/refresh on existing token services; generic failure code; lockout honored; no credential material logged) + tests | Codex | S6-TASK-001 | Done 2026-07-06 (`feat: auth login/refresh endpoints (S6-TASK-010) + auth contracts (S6-TASK-020 partial)`) |
| S6-TASK-011 | `CarePath.Web` scaffold: Blazor WASM, references Contracts/Client/Client.UI only, sln entry under src, DI for typed clients + `AuthorizationMessageHandler` | Codex | S6-TASK-001 | Pending |
| S6-TASK-012 | Authenticated layout + role-based navigation + global sanitized error boundary per D-S6-3 | Codex | S6-TASK-011, S6-TASK-021 | Pending |
| S6-TASK-013 | Seeder: add `coordinator@carepath.local` + `clinician@carepath.local` dev accounts per D-S6-8 (dev-only, same secret password source). Standalone task | Codex | S6-TASK-001 | Pending |
| S6-TASK-023 | Contracts: `UserAccountDto` (normative D-S6-8 shape incl. `CanChangeRole`/`CanDeactivate`/`DisabledReason`), `CreateStaffUserRequest`, `UpdateUserRoleRequest`, `UpdateUserStatusRequest`; Client: `AdminUsersClient` incl. list filters (role/isActive/search) | Claude | S6-TASK-001 | Code complete 2026-07-06 — `CarePath.Contracts/Admin/` (4 files, normative DTO shape) + `AdminUsersClient`; Done 2026-07-06 — verified by full-sln build 0 warnings; S6-TASK-036 is type-unblocked |
| S6-TASK-036 | WebApi/Application: admin users endpoints per D-S6-8 — Admin policy PLUS per-command actor DB re-check (active Admin from database, not JWT claims); atomic role re-sync with rollback; last-Admin + profile-role guardrails computed into the DTO action fields; search on email/display-name only; security/admin audit events via `IPhiAuditLogger` (IDs + role enums only); D-S4-5 provisioning rules; tests for every guardrail + the demoted-admin-live-token case | Codex | S6-TASK-023 | Pending |
| S6-TASK-037 | Web: Users page per D-S6-8 (list/filter/search, create staff, change role, activate/deactivate; renders `CanChangeRole`/`CanDeactivate`/`DisabledReason` as disabled-with-reason; next-sign-in notice) + bUnit tests | Codex | S6-TASK-012, S6-TASK-036, S6-TASK-021 | Pending |
| S6-TASK-020 | Contracts: `LoginRequest`, `RefreshTokenRequest`, `AuthTokenResponse`; Client: `AuthClient`, in-memory `IAccessTokenProvider` implementation | Claude | S6-TASK-001 | Code complete 2026-07-06 — contracts committed in `c42ed21`; `AuthClient` + `InMemoryAccessTokenProvider` (with `CurrentSession`/`CurrentRefreshToken` for the auth state provider) in working tree. Done 2026-07-06 — verified by full-sln build 0 warnings (f019081 report) |
| S6-TASK-021 | Client.UI: `KpiCard`, `RiskBadge`, `ShiftCard`, `EscalationBanner`, `PatientInstructionCard` (patient-safe DTO param), `InstructionReviewCard`, `AuditTimeline` (+ `AuditTimelineEntry` record); StatusBadgeTones extended to 6 Transitions enums; aria labels/roles + native-button keyboard operability per D-S6-7 | Claude | S6-TASK-001 | Done 2026-07-06 — 9 files + `wwwroot/carepath-ui.css` (D-S6-9 tokens); verified by full-sln build 0 warnings; stylesheet linked by CarePath.Web (`f019081`) |
| S6-TASK-022 | Client: `TransitionsClient.GetEscalationQueueAsync` for the D-S6-5 endpoint | Claude | S6-TASK-034 route shape | Pending |
| S6-TASK-030 | Web: coordinator dashboard (open shifts, overdue visit notes, expiring credentials, active plans, open escalations) | Codex | S6-TASK-012, S6-TASK-021 | Pending |
| S6-TASK-031 | Web: schedule board + credential screens with guard-error surfacing | Codex | S6-TASK-030 | Pending |
| S6-TASK-032 | Web: clinician review queue (clinical DTOs; low-confidence flags; source text visible per D-S5-3) | Codex | S6-TASK-012, S6-TASK-021 | Pending |
| S6-TASK-033 | Web: activation screen (disabled until instructions terminal; ConfirmESignature; success -> plan Active state visible) | Codex | S6-TASK-032 | Pending |
| S6-TASK-034 | WebApi: escalation queue endpoint per D-S6-5 + tests (scoping, audit, paged) | Codex | S6-TASK-001 | Pending |
| S6-TASK-035 | Web: escalation queue screen (acknowledge with resolution note + human-decision level) | Codex | S6-TASK-034, S6-TASK-022 | Pending |
| S6-TASK-040 | bUnit test suite per D-S6-6 incl. PHI-exposure markup assertions | Codex (pages) + Claude (primitives) | S6-TASK-030..035 | Pending |
| S6-TASK-050 | Browser PHI safety review per D-S6-3: grep Web for console/DTO serialization/storage writes; verify URLs are Guid-only; keyboard-only walkthrough of the two core workflows | Codex + Claude | S6-TASK-040 | Pending |
| S6-TASK-060 | Exit verification: build 0 warnings, all tests green, reviewer pass, exit-gate items checked; PROGRESS/lessons updated; PM closes after review | Codex + Claude + Tobi | all above | Pending |

### Success criteria (every task)

- Build zero warnings; tests green; reviewer clean; synthetic data only in tests/fixtures.
- `CarePath.Web` never references Domain/Application/Infrastructure/WebApi; no raw HttpClient
  calls outside `CarePath.Client`.
- No PHI in console output, storage, URLs, or error boundary text (D-S6-3).
- Patient/care-team components are typed to patient-safe DTOs (leakage = compile error).
- All list screens page via `PagedRequest`/`PagedTable` — no unbounded fetches.

### Sequencing notes

- Critical path: 001 -> 010/011 -> 012 -> 032 -> 033 -> 060; escalation branch 034 -> 022 -> 035.
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
