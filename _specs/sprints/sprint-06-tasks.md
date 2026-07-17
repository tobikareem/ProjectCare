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

- New project `CarePath.Web` (Blazor WebAssembly standalone, net10.0): references
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
- Screens that exist in the wireframe (Overview, Schedule, Coverage queue, Create shift
  (`page-shift-create`, added 2026-07-09 — entry point for Overview's `＋ Create shift` and
  Schedule's `＋ New shift`; form maps field-for-field to `CreateShiftRequest` and ends in a
  required caregiver assignment per D-S6-10), Assign caregiver, Caregivers roster/profile
  detail, Add caregiver, Add certifications, Schedule eligible shifts, review queue,
  escalations) follow its structure. Screens it lacks reuse its patterns
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
- Current backend shape (amended by D-S6-12, 2026-07-09): `CreateShiftRequest` creates a
  scheduled shift with an OPTIONAL `CaregiverId` — `null` creates an open shift for the
  coverage queue; `UpdateShiftRequest` can assign, reassign, or clear `CaregiverId`. Sprint 6
  may add read/match endpoints or DTOs for open shifts/eligible caregivers, but must not bypass
  the existing shift validation guards.
- `Shifts (MTD)` is not `Caregiver.TotalShiftsCompleted`. It is a current-month metric derived
  from successful caregiver check-in/out records and returned only through an authorized
  Admin/Coordinator profile or scheduling DTO.

### D-S6-11 - Transitions wireframe read surface (added 2026-07-08 per Tobi backend dispatch)

The wireframe Transitions pages (`page-transitions`, `page-transitions-upload`,
`page-transitions-review`, `page-transitions-patient`) need reads the Sprint 5 surface did not
expose. Authorized additions (all Coordinator+Clinician role-gated, PHI-read-audited, and
pinned in `Sprint4ControllerContractTests` + `Sprint6TransitionsClientRouteAlignmentTests`):

- `GET /api/transitions/plans?status=` — optional `TransitionPlanStatus` filter on the existing
  paged plans route (review queue fetches `PendingVerification` server-side instead of paging
  the whole table). Client: `TransitionsClient.GetPlansAsync(paging, status?)`.
- `GET /api/transitions/plans/{id:guid}/reminders` — plan reminder records ordered by
  `ScheduledAt`; IDOR-guarded. Client: `GetRemindersAsync`.
- `GET /api/transitions/plans/{id:guid}/check-ins` — plan check-in history, most recent first;
  IDOR-guarded; returns the existing PHI-minimal `TransitionCheckInDto` (warning flag + review
  metadata; `ResponsesJson` is never echoed per D-S5-3). Client: `GetCheckInsAsync`.
- `GET /api/transitions/documents?pageNumber=&pageSize=` — paged document status/metadata for
  the upload page's recent-uploads list (`DischargeDocumentDto`, no `RawContent`). Client:
  `GetDischargeDocumentsAsync`.

Known wireframe needs deliberately NOT added (need a follow-up decision before any schema/DTO
change): dashboard `Missed`/`Last check-in` summary columns (per-plan reminder/check-in
aggregates), check-in response-text previews for coordinators (would expose `ResponsesJson`
content — mirror the audited `/content` route pattern if approved), plan-linked visit-note
lists, and a Transitions KPI/metrics endpoint (pages derive KPIs from paged reads for now).

### D-S6-12 - Open-shift creation: nullable CaregiverId on create (approved by Tobi 2026-07-09)

Proposed by Codex during the Create shift wireframe review; endorsed by Claude; approved by
Tobi. Amends the D-S6-10 line "CreateShiftRequest creates a scheduled shift with a required
CaregiverId":

- `CreateShiftRequest.CaregiverId` becomes `Guid?`. `null` creates an OPEN shift
  (`Status = Scheduled`, `CaregiverId = null`) that enters the existing coverage queue.
  `Guid.Empty` remains a validation failure. The domain already modeled this —
  `Shift.CaregiverId` was always nullable with "open shift" documented semantics.
- Guard split: date-range/UTC, break, rates, service type, and client validation run on every
  create; double-booking and credential eligibility guards run ONLY when a caregiver is
  supplied (create) or assigned (update — unchanged). Assignment/reassignment stays on
  `UpdateShiftRequest` with all existing guards.
- Two-step UI flow (no new endpoints): Create shift page saves the shift (open or assigned);
  assignment uses the existing Assign page with the real shift id, so shift-scoped
  `GetEligibleCaregiversAsync` works as-is. The proposed draft-preview eligibility endpoint is
  REJECTED — new PHI-adjacent surface with no MVP need.
- Facility note: there is no Facility entity; facility engagements are `Client` records with
  `ServiceType.FacilityStaffing`. "Client or facility" fields map to `ClientId`.
- Wireframe: `page-shift-create` assignment panel is optional ("assign now or leave open");
  `page-shift-assign` relabeled "Assign open shift"; implementation-facing copy removed.

### D-S6-13 - Shifts list date-range filter for the schedule board (approved by Tobi 2026-07-09)

The weekly schedule board cannot be fed by unfiltered pages (Id-ordered page 1 can miss the
visible week entirely; Shift is a high-volume table). Authorized addition:

- `GET /api/shifts?pageNumber=&pageSize=&fromUtc=&toUtc=` — optional UTC range with half-open
  overlap semantics `[fromUtc, toUtc)`: a shift is included when
  `ScheduledEndTime > fromUtc && ScheduledStartTime < toUtc`, so boundary-touching shifts land
  in exactly one week. `fromUtc > toUtc` fails with stable code `shift.invalid_range`.
  Unspecified/local kinds are normalized to UTC server-side.
- The range composes with the existing role scoping (Admin/Coordinator org-wide;
  Caregiver own-shifts; Client own/granted) — it never widens visibility, and per-row read
  audits are unchanged.
- Client: `ShiftsClient.GetPageAsync(paging, fromUtc?, toUtc?)` (additive optional params;
  existing call sites unaffected). Coverage queue remains unfiltered per D-S6-10.
- Pinned by `Sprint4SchedulingServiceTests` range tests and the
  `Sprint6ClientRouteAlignmentTests` URL pin.

### D-S6-14 - Care-plan summary list + detail read split (approved by Tobi 2026-07-17)

PROBLEM: the client-detail page must list a client's care plans, but the only read —
`GET /api/clients/{clientId}/care-plans` — returned `CarePlanDto` rows carrying full clinical
text (`Description`, `Goals`, `Interventions`, `Notes`), shipping clinical PHI to the browser
for a list view (minimum-necessary violation), and no per-plan detail GET existed for drill-in.

DECISION (two coordinated changes, one slice):

- **Summary list** — the existing nested route now returns `PagedResult<CarePlanSummaryDto>`
  (`Id`, `ClientId`, `Title`, `StartDate`, `EndDate?`, `IsActive` — nothing else; a broad
  reflection denylist test fails if clinical fields are ever added). Same five roles, same
  client-level object authorization, per-row CarePlan read audit; filtered/paged/ordered at
  the repository (`StartDate` descending, `Id` tiebreaker) via a new generic descending-order
  paging overload. Breaking response-shape change accepted because no shipped Razor component
  consumed the route (verified 2026-07-17).
- **Detail read** — new `GET /api/care-plans/{id:guid}` returns the full `CarePlanDto`
  (same five roles; `ProtectedResourceType.CarePlan` IDOR guard, which delegates to the
  owning client's staff/clinician/self-grant/assignment rules; audited clinical read;
  PHI-safe identical missing/denied semantics). Clinical text loads only on drill-in.
- Client: `ClientsClient.GetCarePlansAsync` retyped to the summary shape;
  new `ClientsClient.GetCarePlanAsync(planId)`. Route shapes pinned in
  `Sprint6ClientRouteAlignmentTests`.
- Out of scope: search/filters, plan history/versioning, patient-facing rendering
  (Sprint 7), any create/update contract change. CarePath.Web consumption is a separate
  Codex-owned task.

### D-S6-15 - Coverage-queue candidate audit dedup (accepted by Tobi 2026-07-17)

PROBLEM: `GetCoverageQueueAsync` caches caregiver candidate pages
(`ActiveCaregiverCandidateSource`) and reuses them across every open-shift row in one
response. Caregiver and certification `Read` audit events are emitted once per candidate
when its page is first loaded — not once per shift row that discloses the candidate in
`BestMatches`. A hipaa-check flagged this "one read, N disclosures" granularity as an
undecided trade-off.

DECISION: per-page-load audit dedup is accepted for this read-only, Admin/Coordinator-only
matching flow. One audited `Read` per caregiver/certification per coverage request is the
recorded semantics; row-level disclosure correlation is not required. The behavior is
pinned by `GetCoverageQueueAsync_WhenMultipleOpenShiftsShareCandidates_AuditsEachCandidateReadOnce`
(Sprint4SchedulingServiceTests) — a change back to per-row auditing (or a per-row source
rebuild that double-audits) fails that test, forcing a revisit of this decision.
Contrast: `GetEligibleCaregiversAsync` remains fresh-audit-per-call (no caching).

### D-S6-16 - Client-caregiver assignment history (approved by Tobi 2026-07-17)

Caregiver/client association is a scheduling relationship, not a new permanent relationship
entity. A pair is associated when a non-deleted Shift has both its `ClientId` and
`CaregiverId`. Cancelled shifts do not establish an association. This supports both in-home
care and facility staffing without duplicating scheduling data or requiring a migration.

- **Admin/Coordinator client detail:** the Clients page remains the client directory. Selecting
  a client shows the latest relationships as a compact preview and a **View all** action opens a
  full-width, server-paginated, filterable caregiver assignment table. Rows include caregiver
  display name, first/last assigned dates, next shift date when applicable, completed shift
  count, and derived `Current`/`Previous` status. It does not add caregiver columns to the roster.
- **Admin/Coordinator caregiver detail:** the Caregivers profile panel shows the inverse paged
  client assignment history using the same compact-preview plus full-table pattern and the same
  date/status semantics.
- **Caregiver self-service:** caregivers receive a separate **My clients** view. The server
  derives the caregiver profile from the authenticated user and returns only clients associated
  through that caregiver's own current or historical shifts. The view is server-paginated and
  status-filterable; a caller cannot supply another caregiver ID to widen scope.
- **Minimum necessary:** relationship summaries exclude addresses, DOB, diagnoses, care-plan
  text, visit-note text, GPS, rates, margins, and other clinical/financial fields. Caregiver
  self-service uses abbreviated client display names and service context only; opening clinical
  detail remains governed by its existing object authorization.
- **Current/history semantics:** `Current` means the pair has a non-cancelled Scheduled or
  InProgress shift whose end is on or after the UTC request time. Otherwise a pair with at least
  one completed/past non-cancelled shift is `Previous`. Aggregates are computed from filtered
  database queries and ordered deterministically; no unbounded `GetAllAsync()` scan is allowed.
- **Security and audit:** staff routes are Admin/Coordinator-only. Every returned relationship
  emits an ID-only `AssignmentRelationship` read audit keyed by a deterministic client/caregiver
  pair ID, alongside the parent/counterpart resource reads. This is the approved aggregate-read
  audit boundary: it preserves page-level traceability without enumerating an unbounded lifetime
  of contributing Shift IDs. Denials are PHI-safe; routes contain GUIDs only; list reads are paged.
- **Out of scope:** editable primary-caregiver assignments, care-team grants, clinical detail,
  reassignment, and a new Client-Caregiver persistence entity.

Tobi approved the scalable compact-preview plus full-table wireframe and requested exhaustive
implementation on 2026-07-17.

### D-S6-17 - Client-self My caregivers (approved by Tobi 2026-07-17)

Authenticated clients may see their own current care team and limited previous-caregiver history
through a dedicated client-self surface. This does not widen either staff assignment endpoint.

- Route: authenticated body-based `POST /api/clients/me/caregiver-assignments/search`; the server
  derives the Client profile from `ICurrentUserContext.UserId`. No client ID is accepted from the
  browser, and a Client-role family proxy without its own Client profile receives the same PHI-safe
  not-found response. Grant-aware proxy access is out of scope pending a separate decision.
- Dedicated `MyCaregiverAssignmentSummaryDto`: abbreviated caregiver display name, first/last
  relationship dates, next Scheduled shift when present, and `Current`/`Previous` status only.
  It excludes CaregiverId, contact/employment details, credentials, performance, rates, completed
  shift totals, last-shift detail, and all other-client information.
- UI: responsive `/my-caregivers`, status filter, server paging, empty/loading/error states, and
  stale-PHI clearing before every reload. Current relationships appear first.
- Authorization/audit: Client role only, owner identity derived server-side; each returned pair
  uses the approved D-S6-16 deterministic `AssignmentRelationship` audit plus parent/counterpart
  ID-only read audits. Searches remain in the authenticated request body, never the URL.

### D-S6-18 - Invoice generation preview and double-billing guard (approved by Tobi 2026-07-17)

Admin and Coordinator generate one invoice for a selected client, service line, and half-open UTC
period (`PeriodStartUtc <= ScheduledStartTime < PeriodEndUtc`). Wireframe states
`page-billing-generate` (editable selection) and `page-billing-preview` (read-only preview result)
are the visual source of truth for the Select -> Preview -> Generate flow.

- Reuse the existing server-paged `ClientsClient.GetPageAsync` for client/facility selection; a
  separate picker endpoint is not required. In Sprint 6, a facility is represented by its existing
  Client billing account; no new Facility entity is introduced. Future name search uses an
  authenticated request body. Selecting an account may prefill its service type and billing rate,
  but the server remains authoritative and the user must choose when more than one applies.
- Add `POST /api/invoices/preview` for `Admin,Coordinator`. Its body contains `ClientId`,
  `ServiceType`, period, and paging only. The dedicated response carries minimum-necessary billable
  rows plus eligible count, billable hours, and subtotal aggregates across the full result set.
- Each eligible row includes the performing caregiver's display name and qualification label so
  staff can verify who delivered the service. The label is the sorted set of professional
  credentials (RN/LPN/GNA/CNA/HHA/CRMA) valid on the service date; training credentials are omitted
  and an empty set renders `Caregiver`. It still excludes caregiver ID, contact
  details, pay rate, cost/margin, notes, GPS, visit-note data, diagnoses, and clinical fields.
  Reads and creation emit ID-only audit events; logs and errors never include display values.
- Eligibility requires the selected client/service line, `Completed` status, valid actual
  check-in/out, positive time after breaks, an in-period scheduled start, and no existing invoice
  line linked to the shift (soft-deleted lines also block rebilling — reinstatement is a manual
  review, per the DoD). Exclusion counts are links to a separately paged, authorized review
  result. Review rows expose only service/client/caregiver summary, safe reason, age, estimated
  billable value when calculable, and the corrective destination. `Already invoiced` links to the
  invoice that owns the line and is not counted as revenue at risk.
- Preview and create call the same eligibility query. Creation re-evaluates transactionally. Add a
  unique filtered index on non-null `InvoiceLineItems.ShiftId` so overlapping periods or concurrent
  requests cannot bill a shift twice; duplicate-data migration preflight fails rather than mutates.
  A uniqueness race returns a sanitized refresh/re-preview error.
- Preview returns an opaque, expiring `PreviewToken` bound to the client, service line, period,
  eligible shift IDs, billable inputs, and totals. Create adds that token to the existing request
  fields (`DueDate`, non-negative `TaxAmount`, optional PHI-free billing note). If eligibility or
  totals changed, create returns a sanitized `409 invoice.preview_stale`; it never silently creates
  a different invoice. Success opens invoice detail; empty/all-excluded previews cannot generate.
- Billable hours follow the existing minute calculation after breaks. Each line total is rounded
  to two decimal places using `MidpointRounding.AwayFromZero`; subtotal is the sum of rounded lines.
  UTC period boundaries remain start-inclusive/end-exclusive.
- Full revenue-leakage protection is part of this slice through `page-billing-reconciliation`:
  server-paged unresolved services, reason and aging filters, oldest-first prioritization, totals,
  and corrective links. Items leave the unresolved queue only when invoice-eligible or when an
  authorized user records a required, audited non-billable reason; source records are never deleted.
- Exclusion precedence is one reason per shift: `AlreadyInvoiced`, `NonBillableResolved`,
  `CancelledOrNoShow`, `NotCompleted`, `MissingActualTime`, `InvalidBillableTime`, `MissingBillRate`,
  then `Eligible`. Already-invoiced and resolved rows are informational and excluded from revenue at
  risk. Scheduled/in-progress shifts become leakage candidates only after scheduled end plus 24
  hours; KPI totals cover the entire filtered result, not only the displayed page.
- Persist non-billable decisions as append-only `BillingReconciliationResolution` records linked to
  Shift, with reason enum, resolver ID, UTC timestamp, and optional PHI-free note. Corrections never
  delete/overwrite prior resolutions; reopening appends a superseding record. Missing-time correction
  uses a dedicated audited Admin/Coordinator command; bill-rate correction continues through the
  guarded shift update path. All reconciliation searches are body-based, maximum 92-day windows,
  page-size bounded, and ordered oldest service first then Shift ID.
- Out of scope: bulk multi-client and recurring invoices, invoice editing, PDF/export, insurance
  claims, caregiver payroll, payment-processing changes, and clinical text on invoices.

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
| 6I | Client-caregiver assignment history and self-service views | S6-TASK-046..049, S6-TASK-051..053 |
| 6J | Invoice generation, reconciliation, and full revenue-leakage protection | S6-TASK-055..059, S6-TASK-061..065 |
| 6E | Transitions: review queue, activation, escalation queue (+ D-S6-5 endpoint) | S6-TASK-032..035 |
| 6G | User management per D-S6-8: seeder, endpoints, contracts/client, Users page | S6-TASK-013, 023, 036..037 |
| 6F | Accessibility pass + browser PHI safety review + exit verification | S6-TASK-040, S6-TASK-050..060 |

## Screen Matrix

| Screen | Role(s) | Data sources / typed clients | Notes |
|---|---|---|---|
| Login | anonymous | `AuthClient` (D-S6-2) | Generic failure message only |
| Overview | Coordinator, Admin | ShiftsClient (open/covered shifts), CaregiversClient (active/expiring cert counts), TransitionsClient (open escalations), billing summaries when available | Must match wireframe dashboard; quick action `+ Add caregiver` routes to Step 1 create |
| Schedule board | Coordinator, Admin | ShiftsClient paged board/list | Weekly board, open/unassigned styling, coverage queue entry point; guard errors surfaced via ApiErrorAlert |
| Create shift | Coordinator, Admin | ClientsClient.GetPageAsync (client/facility select), ShiftsClient.CreateAsync (open or assigned per D-S6-12) | Wireframe `page-shift-create`; entry from Overview `＋ Create shift` and Schedule `＋ New shift`; "Create & assign" continues to Assign page with the new shift id |
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
| Billing list/detail | Admin, Coordinator | `BillingClient` list/detail/payment reads and writes | Existing invoice management; Generate invoices routes to `page-billing-generate` |
| Generate invoice | Admin, Coordinator | `ClientsClient` server-paged picker + `BillingClient` preview/create | D-S6-18 Select -> Preview -> Generate; safe exclusions and creation-time revalidation |
| Billing reconciliation | Admin, Coordinator | `BillingClient` reconciliation search/detail/correction destinations | D-S6-18 server-paged leakage queue; caregiver/service attribution, safe reasons, aging, estimated value, and invoice links |
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
| S6-TASK-012 | Authenticated layout + role-based navigation + global sanitized error boundary per D-S6-3 | Codex | S6-TASK-011, S6-TASK-021 | Done 2026-07-16 — boundary moved inside `MainLayout` around `@Body`, navigation recovery added, PHI-safe logging tested, and per-link role navigation aligned with page authorization (including Clinician workspace and Admin-only Analytics); Web tests green |
| S6-TASK-013 | Seeder: add `coordinator@carepath.local` + `clinician@carepath.local` dev accounts per D-S6-8 (dev-only, same secret password source). Standalone task | Codex | S6-TASK-001 | Done 2026-07-17 — development-only coordinator/clinician Domain + Identity users use the configured seed secret; production no-op, missing-secret failure, idempotency, IDs, and roles covered by Infrastructure tests |
| S6-TASK-023 | Contracts: `UserAccountDto` (normative D-S6-8 shape incl. `CanChangeRole`/`CanDeactivate`/`DisabledReason`), `CreateStaffUserRequest`, `UpdateUserRoleRequest`, `UpdateUserStatusRequest`; Client: `AdminUsersClient` incl. list filters (role/isActive/search) | Claude | S6-TASK-001 | Code complete 2026-07-06 — `CarePath.Contracts/Admin/` (4 files, normative DTO shape) + `AdminUsersClient`; Done 2026-07-06 — verified by full-sln build 0 warnings; S6-TASK-036 is type-unblocked |
| S6-TASK-036 | WebApi/Application: admin users endpoints per D-S6-8 — Admin policy PLUS per-command actor DB re-check (active Admin from database, not JWT claims); atomic role re-sync with rollback; last-Admin + profile-role guardrails computed into the DTO action fields; search on email/display-name only; security/admin audit events via `IPhiAuditLogger` (IDs + role enums only); D-S4-5 provisioning rules; tests for every guardrail + the demoted-admin-live-token case | Codex | S6-TASK-023 | Done 2026-07-17 — merged via `cebf764`/PR #21; active-Admin DB recheck, serializable role sync/rollback, guardrail DTO fields, SQL-paged email/display-name search, provisioning, PHI-safe audit metadata, and live-token/rollback/guardrail tests independently reviewed |
| S6-TASK-037 | Web: Users page per D-S6-8 (list/filter/search, create staff, change role, activate/deactivate; renders `CanChangeRole`/`CanDeactivate`/`DisabledReason` as disabled-with-reason; next-sign-in notice) + bUnit tests | Codex | S6-TASK-012, S6-TASK-036, S6-TASK-021 | Done 2026-07-16 — Users page and guardrail rendering complete; disabled reasons, paging, and next-sign-in notice covered by Web tests; backend dependency remains owned by S6-TASK-036 |
| S6-TASK-020 | Contracts: `LoginRequest`, `RefreshTokenRequest`, `AuthTokenResponse`; Client: `AuthClient`, in-memory `IAccessTokenProvider` implementation | Claude | S6-TASK-001 | Code complete 2026-07-06 — contracts committed in `c42ed21`; `AuthClient` + `InMemoryAccessTokenProvider` (with `CurrentSession`/`CurrentRefreshToken` for the auth state provider) in working tree. Done 2026-07-06 — verified by full-sln build 0 warnings (f019081 report) |
| S6-TASK-021 | Client.UI: `KpiCard`, `RiskBadge`, `ShiftCard`, `EscalationBanner`, `PatientInstructionCard` (patient-safe DTO param), `InstructionReviewCard`, `AuditTimeline` (+ `AuditTimelineEntry` record); StatusBadgeTones extended to 6 Transitions enums; aria labels/roles + native-button keyboard operability per D-S6-7 | Claude | S6-TASK-001 | Done 2026-07-06 — 9 files + `wwwroot/carepath-ui.css` (D-S6-9 tokens); verified by full-sln build 0 warnings; stylesheet linked by CarePath.Web (`f019081`) |
| S6-TASK-022 | Client: `TransitionsClient.GetEscalationQueueAsync` for the D-S6-5 endpoint | Claude | S6-TASK-034 route shape | Done 2026-07-08 — method in `TransitionsClient` (openOnly + paging); route shape pinned by `Sprint6TransitionsClientRouteAlignmentTests` |
| S6-TASK-030 | Web: Overview page per wireframe (KPI cards, today's schedule, needs-attention list, quick actions). `+ Add caregiver` quick action must route to `page-caregiver-create`; schedule links route to Schedule. Uses typed clients only and renders ApiErrorAlert for network/API failures | Codex | S6-TASK-012, S6-TASK-021 | Done 2026-07-16 — typed shift/coverage data, PHI-safe error rendering, role gate, all quick-action routes, truncated-result guards, and removal of fabricated live metrics verified by Web tests |
| S6-TASK-031 | Web: Schedule board + Coverage queue + Assign caregiver page per wireframe. Board shows open/unassigned styling; Coverage queue lists open shifts and best matches; Assign caregiver supports create assigned shift and update/reassign via `ShiftsClient`; guard errors (`shift.double_booked`, `caregiver.certification_expired`) surface through ApiErrorAlert | Codex | S6-TASK-012, S6-TASK-021, S6-TASK-038 | Done 2026-07-09 — weekly board loads assigned/open shifts through `ShiftsClient.GetPageAsync(paging, fromUtc, toUtc)`, places shifts by local week/day/hour, supports previous/today/next week navigation, horizontal scrolling, open/completed/cancelled handling, and hover/focus detail cards with summary-only fields; Coverage queue and Assign open shift remain wired. Evidence: `dotnet test CarePath.Web.Tests/CarePath.Web.Tests.csproj /nr:false -p:BaseOutputPath=%TEMP%\carepath-schedule-testbin\` (17 passed); `dotnet build CarePath.sln /nr:false -p:BaseOutputPath=%TEMP%\carepath-solution-buildbin\` (0 warnings/errors); `dotnet test CarePath.sln /nr:false -p:BaseOutputPath=.tmpbuild\` (706 passed; `.tmpbuild` removed afterward). Normal default-output build/test attempted but blocked by active Visual Studio/WebApi DLL locks. |
| S6-TASK-032 | Web: clinician review queue (clinical DTOs; low-confidence flags; source text visible per D-S5-3) | Codex | S6-TASK-012, S6-TASK-021 | Done 2026-07-16 — clinical review queue, low-confidence/source-text review surface, typed paging, and durable success feedback implemented |
| S6-TASK-033 | Web: activation screen (disabled until instructions terminal; ConfirmESignature; success -> plan Active state visible) | Codex | S6-TASK-032 | Done 2026-07-16 — terminal-instruction gate, clinician e-sign confirmation, activation request, active-state refresh, and durable success feedback implemented |
| S6-TASK-034 | WebApi: escalation queue endpoint per D-S6-5 + tests (scoping, audit, paged) | Codex | S6-TASK-001 | Done 2026-07-08 — `GetEscalationQueue` endpoint + `GetEscalationQueueAsync` service (openOnly predicate, `GetPagedAsync`, per-row read audit, Coordinator-only) verified in tree with service/controller/contract tests |
| S6-TASK-035 | Web: escalation queue screen (acknowledge with resolution note + human-decision level) | Codex | S6-TASK-034, S6-TASK-022 | Done 2026-07-16 — paged coordinator queue, resolution note, human-decision level, acknowledgement refresh, durable success feedback, and matching wireframe state implemented |
| S6-TASK-038 | Contracts/Client/WebApi/Application: shift assignment read/match APIs for the wireframe. Add paged Admin/Coordinator-safe endpoints/DTOs for open shifts, eligible caregivers for a shift, and eligible open shifts for a caregiver. Must use existing `CreateShiftRequest`/`UpdateShiftRequest` for writes, preserve double-booking/certification guards, avoid `GetAllAsync()` on shifts, and never log PHI shift/client values | Codex + Claude | S6-TASK-001 | Done 2026-07-17 — finalized in `cebf764`/PR #21; locked routes/roles/typed clients align, shift and caregiver reads are filtered/paged, candidate scans are deterministic/cached/bounded, assignment guards remain on existing writes, PHI reads are audited, and route/eligibility/performance tests pass |
| S6-TASK-039 | Contracts/Client/WebApi/Application: caregiver Admin/Coordinator profile detail contract aligned to the wireframe. Expose pay rate only in authorized profile detail, not roster summaries; add check-in-derived `Shifts (MTD)` and `BillableHours (MTD)`; keep certifications in detail; update stale contract docs that say detail cannot include compensation; tests prove roster DTO still excludes pay/certification/MTD columns | Codex + Claude | S6-TASK-001 | Done 2026-07-17 — finalized in `cebf764`/PR #21; Admin/Coordinator-only enriched detail, object authorization, caregiver/certification/shift read audit, check-in/out-derived MTD metrics, rate-free exact roster shape, role/DTO/MTD tests, and stale Sprint 4 contract documentation corrected |
| S6-TASK-041 | Web: Caregivers roster + right-side Profile detail panel per wireframe. Roster columns exactly `Name`, `Type`, `Rating`, `Status`, `View`; `View` loads profile detail with contact, employment/pay rate, availability, skills, certifications, performance/MTD metrics, Add certification, and Schedule eligible shifts actions | Codex | S6-TASK-012, S6-TASK-021, S6-TASK-039 | Done 2026-07-16 — exact roster columns, authorized detail panel, certifications/skills/availability/performance data, and workflow actions implemented and covered by Web tests |
| S6-TASK-042 | Web: Add caregiver Step 1 + Add certifications Step 2 per wireframe. Step 1 maps field-for-field to `CreateCaregiverRequest` and never includes certifications; Step 2 saves one or more `AddCertificationRequest` records with a saved-list state, `Save and add another`, and `Continue to eligible shifts`; temporary password is never displayed after submit | Codex | S6-TASK-041 | Done 2026-07-16 — account/profile creation, post-submit password clearing, repeated certification saves, saved-list state, and continuation routing implemented and tested |
| S6-TASK-043 | Web: Schedule eligible shifts Step 3 per wireframe. After caregiver create/certification or from profile detail, list eligible open shifts for the selected caregiver, explain match reasons, and assign/review shifts through the same Schedule assignment API path; blocked/expired-cert cases render as non-assignable | Codex | S6-TASK-031, S6-TASK-038, S6-TASK-042 | Done 2026-07-16 — server paging, match/block reasons, disabled ineligible actions, assignment through `ShiftsClient`, and post-assignment page clamping implemented; paging/blocking tests green |
| S6-TASK-044 | Web: Create shift page per wireframe `page-shift-create` (D-S6-12). Route `/shifts/create`; form maps field-for-field to `CreateShiftRequest`; client/facility select via `ClientsClient.GetPageAsync`; actions `Create & assign shift` (navigates to Assign page with new shift id) and `Create open shift` (saves unassigned → Schedule/coverage queue); wire Overview `＋ Create shift` and Schedule `＋ New shift` buttons to this route; guard errors via ApiErrorAlert; bUnit tests | Codex | S6-TASK-012, S6-TASK-031, D-S6-12 backend (done) | Done 2026-07-09 — `ShiftCreate` route `/shifts/create`, Overview/Schedule entry points wired, open/create-then-assign flows use `ShiftsClient.CreateAsync` with `CaregiverId = null`; verified by `dotnet test CarePath.Web.Tests/CarePath.Web.Tests.csproj /nr:false` (12 passed) |
| S6-TASK-045 | Contracts/Application/Client: D-S6-12 backend — nullable `CreateShiftRequest.CaregiverId`, conditional eligibility guards in `ShiftOperationsService.CreateShiftAsync`, validator `NotEqual(Guid.Empty)` for supplied values, `ShiftsClient.CreateAsync` open-shift docs, contract-shape pin test + open-shift service tests | Claude | S6-TASK-001 | Done 2026-07-09 — all Application tests green (332); wireframe `page-shift-create`/`page-shift-assign` reworked and browser-verified |
| S6-TASK-046 | Contracts/Application/Client: D-S6-14 backend — `CarePlanSummaryDto` + explicit mapper; list route converted to repository-paged summaries (StartDate desc, Id tiebreak, per-row read audit); new audited `GET /api/care-plans/{id:guid}` detail read behind the CarePlan IDOR guard PLUS an in-service `EnsureCanReadClientAsync` second layer; `ClientsClient` retype + `GetCarePlanAsync`; allowlist/denylist/mapper/service/controller/client/repo tests | Claude | S6-TASK-001, D-S6-14 | Done 2026-07-17 — build 0 warnings; Domain 268 + Application 381 + Infrastructure 86 + Web 39 green (381 includes 3 hipaa-check hardening tests: audit-dedup pin per D-S6-15 + admin search length limit); reviewer clean (defense-in-depth improvement applied); full hipaa-check PASS. Follow-ups: pre-existing Clinician denial in `EnsureCanWriteClientClinicalRecordAsync` (ticketed separately); systemic duplicate read-audit rows (guard + service) noted for a dedicated cleanup decision |
| S6-TASK-054 | Wireframe: D-S6-16 client detail caregiver history, caregiver profile client history, and caregiver-self My clients current/history states using synthetic data and minimum-necessary fields | Codex | D-S6-16 | Done 2026-07-17 — visual reference added; awaiting Tobi visual approval before implementation |
| S6-TASK-047 | Contracts/Application/WebApi: paged D-S6-16 relationship summaries derived from Shift, staff client/caregiver history reads, authenticated caregiver My clients read, object authorization, ID-only PHI audit, deterministic SQL aggregation, and contract/service/controller tests | Codex | S6-TASK-054 approval | Done 2026-07-17 — body-based PHI-safe search, SQL aggregation/paging, current-user caregiver scope, aggregate relationship audit, DTO denylist, role/route/service/provider tests |
| S6-TASK-048 | Client/Web: typed-client methods plus Admin/Coordinator caregiver history on Client detail and client history on Caregiver detail; paged/error/empty states and bUnit authorization/PHI-markup tests | Codex | S6-TASK-047 | Done 2026-07-17 — recent previews, full filtered/paged tables, stale-PHI clearing, typed clients, and bUnit coverage implemented |
| S6-TASK-049 | Caregiver UI: My clients current and previous assignment history, scoped exclusively to authenticated caregiver; minimum-necessary rendering, paging/empty/error states, and authorization tests | Codex | S6-TASK-047 | Done 2026-07-17 — responsive Web `/my-clients` delivered (no MAUI project exists yet), self-derived scope, abbreviated DTO, navigation and PHI-markup tests |
| S6-TASK-051 | Wireframe: D-S6-17 client-self My caregivers current/previous view with minimum-necessary fields, filters, pagination, and responsive state | Codex | D-S6-17 | Done 2026-07-17 — Web visual reference added and approved through implementation request |
| S6-TASK-052 | Contracts/Application/WebApi/Client: dedicated client-self DTO and `/api/clients/me/caregiver-assignments/search`; derive owner profile from authenticated user, PHI-safe proxy/no-profile response, aggregate audit, role/route/service/DTO tests | Codex | S6-TASK-051 | Done 2026-07-17 — owner-derived Client scope, proxy-safe not-found, exact five-field DTO, POST-body search, pair audit, and role/route/service/contract tests verified |
| S6-TASK-053 | Web: responsive `/my-caregivers`, Client-only navigation, status filtering, server paging, empty/error/loading states, stale-PHI clearing, and bUnit authorization/minimum-necessary tests | Codex | S6-TASK-052 | Done 2026-07-17 — responsive paged view, Client-only nav, current/previous filter, minimum-necessary markup, stale-PHI regression, and reviewer gate complete |
| S6-TASK-055 | Wireframe/spec: D-S6-18 separate `page-billing-generate` selection and `page-billing-preview` read-only review states with caregiver/role attribution and linked exclusions, plus `page-billing-reconciliation` leakage KPIs, filters, corrective actions, paging, and preservation notice | Codex | D-S6-18 | Done 2026-07-17 — expanded interactive visual reference and synchronized decision added; awaiting Tobi visual approval before production implementation |
| S6-TASK-056 | Contracts: lock preview, exclusion, reconciliation search/detail, time-correction, non-billable resolution, and stale-preview request/response shapes; opaque preview token; caregiver display + service-date credential label; DTO allowlist/denylist tests and XML docs | Claude | S6-TASK-055 approval | Done 2026-07-17 — 14 contract types + 4 enum mirrors under CarePath.Contracts/Billing; allowlist/denylist + enum-parity tests in Sprint6BillingPlatformTests; contracts frozen for Codex |
| S6-TASK-064 | Domain/Infrastructure: append-only `BillingReconciliationResolution` entity/reason enum/configuration; unique non-null Shift line-item index that blocks historical rebilling; indexes for bounded reconciliation queries; fail-closed duplicate preflight migration and snapshot; SQL/migration/configuration tests | Claude | S6-TASK-056 | Done 2026-07-17 — entity + `UX_InvoiceLineItems_ShiftId_NotNull` filtered index; migration 20260717215349 with THROW 51003/51004 guards; Sprint6BillingPersistenceTests pin filter, Restrict FKs, and fail-closed SQL |
| S6-TASK-057 | Application/WebApi preview + create: one shared eligibility projection and precedence; qualification label; aggregates/rounding; opaque preview token; transactional revalidation; sanitized stale/unique conflicts; `Admin,Coordinator` endpoint, object authorization, ID-only audits, and exhaustive service/controller tests | Claude | S6-TASK-056, S6-TASK-064 | Done 2026-07-17 — BillingOperationsService preview/create over shared `BillingEligibilityQuery.Classify`; DataProtection preview token (15-min); stale/duplicate/race paths covered in Sprint4BillingServiceTests + token crypto tests |
| S6-TASK-061 | Application/Infrastructure reconciliation: bounded SQL search/detail/KPIs, oldest-first stable paging, aged-risk rule, owning-invoice lookup, append-only non-billable resolve/reopen, dedicated audited time correction, existing guarded rate correction, record preservation, authorization/audit/query tests | Claude | S6-TASK-056, S6-TASK-064 | Done 2026-07-17 — BillingReconciliationService + append-only store; ≤92-day bounded search, 7-day aged rule, resolve/reopen/correct-time with plausibility bound (`reconciliation.window_implausible`); behavior pinned in Sprint6BillingPlatformTests |
| S6-TASK-062 | WebApi backend completion: body-based reconciliation search, detail, resolution/reopen and time-correction routes; role/template pins, PHI-safe identical denial/error behavior, client route manifest, integration/concurrency/migration evidence, backend reviewer and HIPAA pass | Claude | S6-TASK-057, S6-TASK-061 | Done 2026-07-17 — 6 InvoicesController actions (`Admin,Coordinator` + IDOR guards); role/template pins in Sprint4ControllerContractTests; reviewer + HIPAA pass recorded in handoff manifest; real SQL concurrency evidence deferred to S6-TASK-059 (no SQL Server in dev env) |
| S6-TASK-058 | Client handoff: add typed preview/create/reconciliation/detail/resolve/reopen/time-correction methods to `BillingClient`; pin verb/body/template alignment in a dedicated test; publish frozen contract/route/error/client manifest | Claude | S6-TASK-062 | Done 2026-07-17 — 6 BillingClient methods (token/PHI always in body, never URL); Sprint6BillingClientRouteAlignmentTests pins all routes; frozen handoff manifest delivered — Codex Web (S6-TASK-065/063) unblocked |
| S6-TASK-065 | Web Generate invoice: `/billing/generate` Select and explicit Preview states; server-paged client selector with defaults, caregiver/credential attribution, linked exclusions, read-only snapshot, Back/Edit, stale-preview recovery, confirmation, invoice-detail navigation and bUnit tests | Codex | S6-TASK-058 frozen handoff | Pending — Web/Web.Tests only; do not edit Claude-owned Contracts/Client/backend |
| S6-TASK-063 | Web reconciliation: KPI cards, body-based filters/server paging, linked exclusion drill-through, shift/time/rate correction destinations, owning-invoice navigation, resolve/reopen confirmation, stale-PHI clearing, role gates, responsive/keyboard states and bUnit tests | Codex | S6-TASK-058 frozen handoff, S6-TASK-065 | Pending |
| S6-TASK-059 | Integrated verification: route alignment, bUnit workflows and PHI assertions, SQL concurrency/migration evidence, browser responsive/keyboard walkthrough, full build/tests, dotnet reviewer, full HIPAA check, board evidence and PM sign-off | Codex + Claude + Tobi | S6-TASK-057..058, S6-TASK-061..065 | Pending |
| S6-TASK-040 | bUnit test suite per D-S6-6 incl. PHI-exposure markup assertions. Must cover Overview quick action routing, Schedule coverage assignment, Caregiver roster/profile detail, Add caregiver Step 1, multi-certification Step 2, and eligible-shifts Step 3 in addition to Transitions pages | Codex (pages) + Claude (primitives) | S6-TASK-030..035, S6-TASK-037, S6-TASK-041..043 | Pending |
| S6-TASK-050 | Browser PHI safety review per D-S6-3: grep Web for console/DTO serialization/storage writes; verify URLs are Guid-only; keyboard-only walkthrough of review->activate, escalation->acknowledge, caregiver create->certify->eligible-shifts, and schedule coverage->assign workflows | Codex + Claude | S6-TASK-040 | Pending |
| S6-TASK-060 | Exit verification: build 0 warnings, all tests green, reviewer pass, exit-gate items checked; PROGRESS/lessons updated; PM closes after review | Codex + Claude + Tobi | all above | Pending |

### D-S6-18 delivery workflow and ownership

1. **Visual approval:** Tobi approves `page-billing-generate`, `page-billing-preview`, and
   `page-billing-reconciliation`. No production UI precedes this gate.
2. **Claude contract slice (S6-TASK-056):** create and test all client-safe Billing contracts.
   Contract names, fields, enums, route bodies, error codes, paging and token semantics become frozen.
3. **Claude platform slice (S6-TASK-064, 057, 061, 062, 058):** add reconciliation history,
   migration, shared eligibility query, preview/create safety, reconciliation/correction endpoints,
   audit, backend tests, and typed-client methods. Claude publishes a frozen contract/route/error/
   client manifest and exact test evidence; Claude does not mark the board Done.
4. **Codex UI slice (S6-TASK-065, 063):** only after the frozen handoff, implement the three
   approved wireframe states. Codex does not change frozen Contracts, Client, or backend
   contracts; any mismatch returns to Claude as a narrowly scoped follow-up.
5. **Integration (S6-TASK-059):** run route alignment, bUnit, SQL migration/concurrency, browser,
   full solution, reviewer and HIPAA gates. Tobi reviews the working flow and signs off.
6. **Closeout:** task rows move to Done only with commands/results recorded. S6-TASK-050 and 060
   absorb the billing workflow into Sprint 6's overall safety and exit checks.

Delivery estimate (each work unit is kept within the repository's 1–4 hour task rule):

| Task | Owner | Estimate | Primary deliverable |
|---|---|---:|---|
| S6-TASK-056 | Claude | 3h | Frozen client-safe contracts and contract tests |
| S6-TASK-064 | Claude | 4h | Reconciliation history, EF configuration, protected index and migration |
| S6-TASK-057 | Claude | 4h | Shared preview/create service, API, token and transaction safety |
| S6-TASK-061 | Claude | 4h | Reconciliation SQL queries, correction/resolution services and tests |
| S6-TASK-062 | Claude | 3h | Reconciliation controllers, route pins and backend verification manifest |
| S6-TASK-058 | Claude | 2h | Typed BillingClient methods, route pins and frozen handoff manifest |
| S6-TASK-065 | Codex | 4h | Select/Preview/Generate Web workflow and bUnit tests |
| S6-TASK-063 | Codex | 4h | Reconciliation Web workflow, responsive states and bUnit tests |
| S6-TASK-059 | Joint | 3h | SQL/browser/full-suite/reviewer/HIPAA evidence and sign-off package |

Estimated engineering effort: **31 hours**, excluding review latency and environment provisioning.

Exclusive file ownership until the contract-freeze handoff:

- **Claude:** `CarePath.Contracts/Billing/**`, `CarePath.Client/Api/BillingClient.cs`, its dedicated
  route-alignment test, `Domain/Entities/Billing/**`, billing enums,
  `Application/Abstractions/Billing/**`, `Application/Billing/**`, `Infrastructure/Billing/**`,
  billing EF configurations/migrations/snapshot, `WebApi/Controllers/InvoicesController.cs`, and
  new uniquely named backend billing test files.
- **Codex:** `CarePath.Web/Pages/Billing*.razor`, Web-only styles/layout glue,
  `CarePath.Web.Tests/**`, wireframe, and Sprint documentation.
- Shared files such as DI registration, DbContext, solution-level test utilities, or existing broad
  contract tests are edited by Claude during backend work and frozen before Codex begins. Codex
  requests a follow-up rather than editing them concurrently.

### D-S6-18 Definition of Done

Billing generation and reconciliation are Done only when all of the following evidence exists:

- The implemented Select -> Preview -> Edit/Generate and reconciliation screens match all three
  wireframe states at desktop and narrow widths; loading, empty, all-excluded, stale, error and
  success states are present and keyboard operable.
- Admin and Coordinator succeed; Caregiver, Client, Clinician, FacilityManager and anonymous users
  are denied by both API and UI. Object access is checked before state disclosure and unauthorized
  PHI resources use identical sanitized responses.
- Preview/create share one eligibility implementation and locked precedence. Tests cover every
  exclusion, 24-hour aged-risk boundary, start-inclusive/end-exclusive UTC boundaries, break/time
  edges, missing/zero rate, service-line/client isolation, stable paging, and whole-result KPIs.
- Currency tests prove per-line `AwayFromZero` rounding and subtotal-as-sum-of-lines. Preview token
  tests prove expiry, tampering, cross-client reuse and changed shift/time/rate sets return sanitized
  stale conflicts without creating an invoice.
- Database evidence proves a shift cannot be billed twice across exact or overlapping periods,
  concurrent requests, or historical soft-deleted invoice lines. Migration preflight fails closed
  on existing duplicates; idempotent SQL and a real SQL Server concurrency test pass.
- Reconciliation never loses source data: resolution/reopen is append-only, corrections are audited,
  reason/aging filters and totals are correct, already-invoiced rows link to their invoice, and every
  unresolved delivered service remains visible until eligible or explicitly resolved.
- Contract denylist/reflection tests prevent caregiver IDs/contact/pay, cost/margin, GPS, clinical
  notes, diagnoses and credential numbers from preview/reconciliation DTOs. Display name and valid
  professional credential labels are the only approved caregiver attributes.
- Every returned Shift/Client/Caregiver/Invoice row and every mutation has ID-only audit evidence.
  No PHI appears in URLs/query strings, logs, console, browser storage, validation/errors or test
  fixtures; the full `$hipaa-check` reports PASS.
- Typed-client URLs exactly match controller verbs/templates. bUnit covers select -> preview -> edit,
  generate, stale re-preview, empty/all-excluded, exclusion drill-through, resolution/reopen,
  correction navigation, paging, stale-PHI clearing and role navigation.
- `dotnet build CarePath.sln` completes with zero warnings; targeted and full `dotnet test
  CarePath.sln` pass; migration validation passes; the dotnet-code-reviewer reports no critical or
  high issues; exact commands/counts are written into S6-TASK-056..059 and S6-TASK-061..065 before
  Tobi signs off.

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
- Billing branch: 055 approval -> Claude 056 -> 064 -> 057/061 -> 062 -> 058 frozen handoff -> Codex 065 -> 063 -> joint 059 -> 050 -> 060.
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
