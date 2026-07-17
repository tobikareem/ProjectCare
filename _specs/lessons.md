# Lessons Learned — CarePath Health

This file captures recurring mistakes, corrections, and hard-won patterns discovered during development. Claude reads this at the start of every session and updates it after any user correction.

**Format:** When a mistake is corrected, add an entry immediately. Group by theme. Remove entries that are no longer relevant.

---

## Domain Layer

- **Namespace for enumerations is `CarePath.Domain.Enumerations`** — not `CarePath.Domain.Enums`. This was confirmed when reviewing the actual code vs design spec samples that used the wrong namespace. Always check the actual file before writing a using directive.
- **CarePlan lives in `Entities/Clinical/`** in actual code, not `Entities/Identity/` as the design spec states. The design spec has a stale folder reference. Trust the actual file structure over the design spec when they conflict; update the spec to match.
- **`VisitNote` and `Shift` tables will be large** — never use `GetAllAsync()` on them. Always add date/client/status filters. `GetPagedAsync` is planned in TASK-019a.
- **Transitions entities go in `Entities/Transitions/`** — new subfolder, not Identity or Scheduling. Pattern mirrors the existing folder split.
- **UserRole values**: Design spec defines: Admin, Coordinator, Caregiver, Client, FacilityManager. The tasks spec initially had SuperAdmin instead of Admin — the design spec takes precedence when there's a conflict.
- **Entity file paths**: Use subfolder structure from design spec: `Entities/Common/`, `Entities/Identity/`, `Entities/Scheduling/`, `Entities/Billing/` — not flat `Entities/`.

## EF Core / Infrastructure

- **Ignore CP-03 scalar placeholders in CP-02 mappings** - `VisitNote.TransitionPlanId` exists in Domain for future integration, but CP-02 must ignore it until Transitions persistence is explicitly configured; otherwise migrations create a partial CP-03 schema without reviewed FKs.
- **Nullable FKs on PHI records still use `DeleteBehavior.Restrict`** - Nullable means the Application layer may explicitly unassign with audit logging; the database must not silently `SET NULL` during parent deletion because that erases clinical history.
- **Do not map CP-03 Transitions DbSets during CP-02 Phase 1** - Transitions entities contain PHI and need explicit configurations before entering the EF model. Mapping them early lets EF conventions create unbounded PHI columns and cascade-delete FKs. Add Transitions DbSets only with their CP-03 backend configurations.
- **`TransitionPlan.TransitionWindowEnd`** must be set in the Application layer before save (`DischargeDate.AddDays(30)` in UTC). Never compute it in the entity constructor — that would make it hard to test without mocking DateTime.
- **`TransitionCheckIn.ResponsesJson`** is PHI stored as `nvarchar(max)`. Configure it that way in EF. Never serialize it into a log string.
- **PHI cascade deletes**: ALL 6 PHI entities (Client, CarePlan, Shift, VisitNote, VisitPhoto, CaregiverCertification) must use `DeleteBehavior.Restrict` — no exceptions. The subagent initially set CaregiverCertification and VisitPhoto to Cascade because they're "dependent" entities, but HIPAA overrides that logic.
- **Folder structure**: Interceptors and Converters live at `Persistence/Interceptors/` and `Persistence/Converters/` — NOT inside `Persistence/Configurations/`. Configurations folder is for entity type configs only (Identity/, Scheduling/, Billing/ subdirectories).
- **Property names must match CP-01**: When writing Infrastructure configurations, always cross-reference the approved CP-01 design spec for exact property names. Common mismatches caught: `IssuingBody` vs `IssuingAuthority`, `TransactionId` vs `ReferenceNumber`, `StartLatitude` vs `CheckInLatitude`.
- **GPS fields are `double?`**: CP-01 defines GPS coordinates as `double?`, not `decimal(10,7)`. EF Core maps `double` to SQL Server `float` by default — no explicit precision config needed.
- **Scope boundaries**: When a feature is listed in both in-scope and out-of-scope sections, it creates implementation confusion. Resolve immediately. If implementation code exists in the design spec, it's in-scope.

- **Development seed credentials must never be hard-coded** - Even development-only seed users become dangerous if an environment is accidentally marked Development. Read temporary passwords from user secrets, environment variables, or other uncommitted configuration and fail closed when missing.
- **Save resurrected soft-deleted principal rows before Identity lookups** - If `ApplicationUser` has a query filter through `DomainUser.IsDeleted`, undeleting the domain user only in memory can hide the existing Identity row and cause duplicate-key failures. Persist the undelete before `UserManager` lookup.
- **PHI migrations must fail closed on destructive rollback** - A migration that introduces clinical PHI tables or PHI linkage columns must not let `Down` drop those records after real use. Make the rollback forward-only or guard it so populated PHI tables/links are preserved.

## Testing

- **Domain unit tests are pure** — no EF Core, no mocks, no database. `TransitionPlan.IsActive` and `DaysRemaining` need edge-case tests: exactly at window boundary, past window (must return 0, not negative), status != Active.

## HIPAA / Compliance

- **Admin is a platform-wide operational superuser** - If a workflow is available to Coordinator or Clinician in the staff web app, include Admin consistently in Web UI route/nav gates, WebApi role attributes, and Application service role helpers. Do not expose patient self-service actions to Admin unless the workflow explicitly supports staff acting on behalf of the patient with audit semantics.
- **Two new fields that must never be logged**: `DischargeDocument.RawContent` (raw OCR/FHIR text) and `TransitionInstruction.SourceText` (original discharge document excerpt). Both are PHI. Add a comment in code on both properties: `// PHI — never log this field`.
- **Reminders must not fire before `TransitionPlan.Status == Active`** — enforce this check in the Application command handler, not only at the API layer. Belt-and-suspenders.

- **Financial fields are not general response DTO fields** - Rate, margin, and compensation values must stay out of normal detail/list DTO responses. If an operational workflow needs rates as Admin/Coordinator write inputs, keep those fields on request contracts only; returned compensation metrics belong behind Admin-gated margin DTOs and endpoints.
- **Caregiver pay rate can be an Admin/Coordinator profile-detail field when explicitly approved** - The earlier "input only" UI assumption was corrected. Keep pay rate out of roster/general summaries, but show it in caregiver profile detail when the workflow is Admin/Coordinator-scoped and the contract explicitly supports that authorized view.
- **Caregiver `Shifts (MTD)` is check-in-derived, not the lifetime counter** - Do not use `Caregiver.TotalShiftsCompleted` for month-to-date profile metrics. Add/query current-month shift records from successful caregiver check-in/out data and expose the result through an authorized profile metric.
- **Caregiver certification setup must support multiple active credentials** - A caregiver commonly has more than one credential/training record. The setup UI should let coordinators save a list of certifications before moving to eligible shifts; backend calls may still persist them one record at a time.
- **Validation responses must not use FluentValidation message text for PHI-adjacent requests** - Default FluentValidation messages can include attempted values, including GPS coordinates or clinical vitals. Middleware should return generic field errors and preserve only PHI-free property names/error codes unless a response contract explicitly proves the message is safe.
- **PHI 404 response bodies cannot include per-request identifiers** - Missing-vs-denied PHI resources must be byte-identical in real requests, not only tests with a forced trace id. Keep TraceId in logs/audit/headers if needed, but omit it from PHI 404 JSON bodies.
- **Order authorization checks before business-state checks on PHI routes** - Guard error semantics such as `transition.plan_not_active` or `transition.outside_window` can disclose record existence or status to unauthorized callers. Evaluate self/grant/object access first, return PHI-safe denial for unauthorized callers, and only then run business-state guards for authorized users.
- **Audit internal PHI field reads, not only endpoint reads** - If an Application handler reads high-risk fields such as `RawContent`, `SourceText`, or `ResponsesJson` for internal processing, emit a PHI read audit before accessing the value even when the same handler also writes status changes.
- **Care-team-safe Transitions views still require activation** - Caregiver/care-team routes must not expose draft, pending-verification, or expired transition plan metadata. Verify current assignment first, then return only active in-window plans; otherwise use PHI-safe not found semantics.

## Spec Workflow

- **Do not infer credential validity from historical configuration notes** - A missing legacy config file or an old migration note does not prove that a current environment credential is exposed, invalid, or needs rotation. Report only verified credential findings; when the user confirms the active Context7 key is good, treat that as authoritative and remove speculative rotation concerns.
- **User corrections from code review must update task status immediately** - If review shows a task was over-marked complete or scope was wrong, correct the board/spec in the same turn before continuing implementation.
- **Spec numbering baseline**: CP-02 is Infrastructure / EF Core. CP-03 is CarePath Transitions. Transitions Domain work already exists; backend work waits for Infrastructure and Application foundations.
- **Task numbers are historical identifiers, not CP identifiers** — completed Transitions Domain tasks remain TASK-020 through TASK-024; CP-02 Infrastructure tasks remain TASK-040+. Do not renumber completed tasks just to match CP numbers.
- **Design spec is source of truth**: When tasks spec or CLAUDE.md conflicts with the approved design spec, the design spec wins. Update the stale document, not the design spec.
- **User corrections are authoritative**: When the user edits a spec directly (e.g., changing file paths, correcting role names), those edits override any prior assumptions. Check the system-reminder for modifications.

## Architecture / Design Decisions

- **No autonomous escalation** — the system creates `TransitionEscalation` records and surfaces them on the coordinator dashboard. It never autonomously contacts family, recommends urgent care, or calls 911. Coordinator makes all human contact decisions. This is non-negotiable for regulatory reasons.
- **AI extraction interface lives in Application, not Infrastructure** — `IDischargeExtractionService` is defined in `CarePath.Application/Transitions/Interfaces/`. The Infrastructure implementation (wrapping OpenAI/Azure OpenAI) depends on Application, not the other way around. Never break the dependency rule.
- **Twilio credentials go in user secrets / appsettings** — never hardcode. Never log Twilio API keys. Twilio has a HIPAA BAA available — ensure it is signed before processing any PHI via SMS.

## UI / Design

- **The wireframe is the UI source of truth** (`Documentation/Wireframes/carepath-wireframe.html`; spec: `_specs/02-design/ui-design-system.md`). Sprint 6 lesson: shared UI components were initially built without consulting it — always extract design tokens from the wireframe (`CarePath.Client.UI/wwwroot/carepath-ui.css`) before building any visual component, and never hard-code colors/fonts/radii in pages or components. Screens the wireframe lacks require a PM/wireframe update before implementation.
- **CarePath.Web must consume the wireframe shell, not the Blazor template shell** - Link `_content/CarePath.Client.UI/carepath-ui.css`, use the wireframe variables (`--teal-900`, `--surface-alt`, `--line`, `--radius`, `--shadow`, `--focus`, Inter stack), and remove Bootstrap/template hard-coded colors, fonts, and radii from Web pages/layouts before considering a Web slice complete.
- **Do not approximate the wireframe layout** - A UI pass that only uses the shared tokens but changes navigation grouping, route behavior, spacing, icons, labels, or page structure is not compliant with D-S6-9. Reproduce the relevant `carepath-wireframe.html` desktop shell/page structure first, then adapt only where Blazor routing requires it.
- **Scoped layout CSS can silently override the wireframe shell** - Blazor template-era scoped files such as `NavMenu.razor.css` can outweigh app-level responsive rules. When shell/navigation behavior looks wrong at narrow widths, inspect generated scoped selectors before changing global CSS; remove stale template rules instead of layering workarounds.
- **Centralize typed-client network failure handling** - Web pages should not each catch `HttpRequestException` for expected API/network failures. Prefer `ApiClientBase` mapping transient/network failures into PHI-safe `ApiResponse` errors so pages render `ApiErrorAlert` consistently without logging or serializing DTOs.

## Session History

### Session: 2026-06-22
**What was built**:
- Reviewed CP-01 (Domain layer) — confirmed all 39 tasks complete
- Updated wireframe navigation (`carepath-wireframe.html`) with working page/screen switching
- Scoped CarePath Transitions feature (30-day post-discharge care management)
- Created `CarePath_Transitions_Feature_Presentation_v2.pptx` — 10 slides, full B2B business case, competitor landscape, solution, MVP path
- Created three spec files for CP-03 Transitions (requirements, design, tasks); originally CP-02 before Sprint 1 renumbering
- Updated CLAUDE.md with full Transitions domain model and new entities

**What's next**:
- CP-03 Transitions specs are approved for the Domain slice; backend work is scheduled after Infrastructure/Application prerequisites
- TASK-020 through TASK-024 for CP-03 Transitions Domain are complete
- Infrastructure (CP-02) and Application/contracts are prerequisites for shipping CP-03 Transitions backend
## Sprint 3 Mapping / Contracts

- **Never map persisted media/blob URLs directly into DTOs** - Signature URLs are biometric PHI and persisted storage locators may be durable or public. Application mappers should leave signature URL fields null until an authorized, audited short-lived URL service exists.
- **Summary DTO PHI guards must use a broad denylist** - Client summaries must exclude DOB and clinical/insurance/location fields, but all `*SummaryDto` types also need reflection guards for care-plan text, visit-note clinical text, notes, signature URLs, raw GPS, and rate/margin fields. Detail DTO exposure is allowed only behind role + object authorization.
- **Contracts boundary tests should inspect the Contracts assembly directly** - Mapper-signature tests are useful, but Sprint 3 also requires proving `CarePath.Contracts` has no Domain assembly reference and no public member types from `CarePath.Domain.*`.
