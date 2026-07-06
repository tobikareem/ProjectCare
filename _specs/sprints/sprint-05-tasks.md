# Sprint 5 Tasks - CarePath Transitions Backend MVP

Status: Complete (closed 2026-07-06 by PM after exit verification `770e4a9`)
Parent spec: `_specs/sprints/sprint-05-transitions-backend-mvp.md`
Design spec: `_specs/02-design/cp-03-transitions.md` (source of truth except where amended by D-S5 decisions below)
Last updated: 2026-07-06

Baseline: Sprints 1-4 are complete and Sprint 4 is PM-closed. Sprint 4 exit verification commit `798612f` passed build/test/reviewer/HIPAA gates. This board is the normative implementation contract
for Sprint 5. Decisions D-S4-1..7 remain in force; the Sprint 3 denial contract and Sprint 4 exit-verification PHI 404 hardening apply to every Transitions route.

Safety context: this is the highest-compliance slice in the project. Four absolutes from the
parent spec's exit gate: (1) no reminders for inactive plans, (2) no autonomous escalation
actions ever, (3) stub/AI output can never bypass clinician approval, (4) `RawContent` and
`SourceText` never appear in logs, errors, or URLs even though reads of them are audited.

Sprint 5 deliberately narrows the older CP-03 requirements/design for MVP safety: no real AI/OCR,
no Twilio delivery/webhooks, no FHIR import, no PDF/photo binary persistence, no autonomous
escalation actions, and no patient/caregiver DTO exposure of review-source content. Where this
board conflicts with `_specs/02-design/cp-03-transitions.md`, the D-S5 decisions below govern
Sprint 5 implementation; update the older design spec later instead of widening this sprint.

---

## Decisions

### D-S5-1 - Transitions persistence unfreeze protocol

The Sprint 2 rule ("do not map CP-03 DbSets") is lifted ONLY via this sequence, in one slice:

1. Write explicit EF configurations for all six entities (`DischargeDocument`, `TransitionPlan`,
   `TransitionInstruction`, `TransitionReminder`, `TransitionCheckIn`, `TransitionEscalation`):
   `DeleteBehavior.Restrict` on every relationship; `RawContent`, `SourceText`, and
   `ResponsesJson` as `nvarchar(max)` (deliberate, PHI); string lengths on all other text;
   decimal `(5,4)` for `ConfidenceScore`; `.Ignore()` on computed properties (`IsActive`,
   `DaysRemaining`, `IsLowConfidence`, `IsOverdue`); indexes on `ClientId`, `Status`, and
   `ScheduledAt` (reminders). Configure navigation collections with explicit backing fields or
   field access where needed for existing `IReadOnlyList<T>` private-set collection shapes, and
   add metadata tests proving EF can map/load the relationships.
2. Map the `VisitNote.TransitionPlanId` FK (nullable) with `Restrict` - never `SET NULL`
   (lessons.md: nullable PHI FKs still use Restrict).
3. Add DbSets to `CarePathDbContext` (centralized UTC/soft-delete conventions apply; configs
   must NOT duplicate query filters or converters).
4. Generate `AddTransitions` migration; REVIEW the generated SQL against rules 1-2 before
   applying (no cascades, no unbounded non-PHI columns); metadata tests assert delete
   behaviors and column types; then `dotnet ef database update`.

This is a schema migration only - six new tables + one nullable FK column; no data backfill.

### D-S5-2 - Clinician relationship source (resolves Sprint 4 carry-forward)

Per CP-03 design ("Coordinator and Clinician can read any plan"): Clinicians may read all
Transitions documents/plans. Clinician access to core Client/CarePlan records widens from the
Sprint 4 conservative deny to: clients having a `TransitionPlan` in any status except
`Cancelled`. Implemented in the existing list-scoping/`IClientAccessEvaluator` seams; the
Sprint 4 "deny by default" behavior remains for clients with no transition relationship.

### D-S5-3 - Source-text visibility (amends Sprint 3 Contracts Plan section 4)

The blanket "never mirror `SourceText`/`RawContent` into any contract" rule is amended for the
clinician review workflow (parent spec Story 8 requires it):

- `TransitionInstructionClinicalDto` carries `SourceText` - Clinician/Coordinator endpoints
  only; every read audited.
- `RawContent` is exposed ONLY via a dedicated `GET .../documents/{id}/content` endpoint
  (Clinician/Coordinator, audited) - never embedded in list, plan, or status DTOs.
- Patient-facing and caregiver/care-team-safe contracts NEVER carry `SourceText`, `RawContent`, confidence scores, reviewer notes, or `ResponsesJson`.
- Unchanged: neither field ever appears in logs, exceptions, URLs, or audit entry values.

### D-S5-4 - Extraction stub

`IDischargeExtractionService` lives in Application (lessons.md: never invert the dependency);
Infrastructure ships a deterministic rule-based stub (keyword/section heuristics - no network,
no AI provider). Every produced instruction starts `TransitionInstructionStatus.Pending`;
`ConfidenceScore < 0.75m` marks `IsLowConfidence`. Stub output is draft-only: nothing it
produces is patient-visible or actionable until clinician review (D-S5-5). Real AI/OCR is
Sprint 7 and must implement the same interface behind the third-party provider gates.

### D-S5-5 - Activation and e-signature (MVP form)

Activation requires: authenticated Clinician role; every instruction in a terminal review state
(`Approved`, `Modified`, or `Rejected` - none `Pending`); an explicit activation payload; and
plan status `PendingVerification`. Activation records the existing Domain e-signature fields (`VerifiedBy`, `VerifiedAt`, and `ActivatedAt`)
using the authenticated clinician user id and server UTC clock, and computes `TransitionWindowEnd = DischargeDate.AddDays(30)`
in UTC **in the Application handler** (never in the entity, never local time). External
e-sign providers are out of scope. Activation is audit logged as a write.

### D-S5-6 - Reminder guard and check-in intake

- Reminder scheduling: Application handler rejects unless `TransitionPlan.Status == Active`
  AND `ScheduledAt` falls within the transition window. Stable error code
  `transition.plan_not_active` / `transition.outside_window`. No delivery in Sprint 5 -
  records only (`ReminderStatus.Scheduled`); Twilio delivery is Sprint 7.
- Check-ins arrive via the authenticated API only: `Client` role (self, or grantee with any
  scope - check-in submission is patient-facing). The Twilio webhook intake path (System
  actor) is Sprint 7; the command layer is designed so both paths converge on one handler.

### D-S5-7 - Escalation evaluator: records only

Warning-symptom check-ins, missed critical reminders, and caregiver alerts create
`TransitionEscalation` records (default `EscalationLevel.CoordinatorAlert`) surfaced on the
coordinator dashboard query. The system NEVER contacts family, recommends urgent care, or
dials 911 - `FamilyNotification`/`UrgentCare`/`Emergency911` levels are set only by a
coordinator acknowledging and documenting a human decision. Enforced by tests.

### D-S5-8 - PHI 404 response-body identity

All Transitions missing/denied PHI 404 JSON bodies must be byte-identical in real requests.
Do not include per-request `TraceId`, denial reason codes, route ids, or entity names in those
404 bodies. Correlation stays in audit/log metadata only.

---

## Implementation Slices

| Slice | Goal | Includes |
|---|---|---|
| 5A | Persistence unfreeze: configs, DbSets, `AddTransitions` migration, metadata tests | S5-TASK-010..011 |
| 5B | Transitions Contracts: enum mirrors + clinical/patient-facing/care-team DTO split + requests | S5-TASK-020 |
| 5C | Intake + extraction stub + draft plan assembly | S5-TASK-030..031 |
| 5D | Clinician review, activation, reminder guard | S5-TASK-032..034 |
| 5E | Check-ins, escalation evaluator, VisitNote linkage, Clinician scoping (D-S5-2) | S5-TASK-035..037 |
| 5F | `TransitionsController` + patient-facing routes + audit + IDOR tests | S5-TASK-040..041 |
| 5G | `TransitionsClient` typed client + exit verification | S5-TASK-050..060 |

---

## Endpoint Matrix

| Method/Route | Roles | Object auth | Request/Response |
|---|---|---|---|
| `POST /api/transitions/documents` | Coordinator | Client-scope check on payload ClientId | `CreateDischargeDocumentRequest` -> `DischargeDocumentDto` |
| `GET /api/transitions/documents/{id}` | Coordinator, Clinician | `IIdorGuard` | `DischargeDocumentDto` (status/metadata - no RawContent) |
| `GET /api/transitions/documents/{id}/content` | Coordinator, Clinician | `IIdorGuard` | `DischargeDocumentContentDto` (RawContent; audited read, D-S5-3) |
| `POST /api/transitions/documents/{id}/extract` | Coordinator | `IIdorGuard` | 202; stub creates draft plan + instructions |
| `GET /api/transitions/plans` | Coordinator, Clinician | Filtered query | `PagedRequest` -> `PagedResult<TransitionPlanSummaryDto>` (dashboard) |
| `GET /api/transitions/plans/{id}` | Coordinator, Clinician | `IIdorGuard` | `TransitionPlanClinicalDto` (incl. instructions with SourceText/confidence) |
| `GET /api/transitions/plans/{id}/patient-view` | Client (self or grantee) | Grant/self evaluation | `TransitionPlanPatientFacingDto` (approved patient-facing instructions only) |
| `PUT /api/transitions/plans/{id}/instructions/{instructionId}` | Clinician | `IIdorGuard` | `ReviewInstructionRequest` (approve/modify/reject + note) -> `TransitionInstructionClinicalDto` |
| `POST /api/transitions/plans/{id}/activate` | Clinician | `IIdorGuard` | `ActivatePlanRequest` -> `TransitionPlanClinicalDto` (D-S5-5) |
| `POST /api/transitions/plans/{id}/reminders` | Coordinator | `IIdorGuard` | `ScheduleReminderRequest` -> `TransitionReminderDto` (guard, D-S5-6) |
| `POST /api/transitions/plans/{id}/check-ins` | Client (self or grantee) | Grant/self evaluation | `CreateCheckInRequest` -> `TransitionCheckInDto` |
| `GET /api/transitions/plans/{id}/escalations` | Coordinator | `IIdorGuard` | `IReadOnlyList<TransitionEscalationDto>` |
| `POST /api/transitions/escalations/{id}/acknowledge` | Coordinator | `IIdorGuard` | `AcknowledgeEscalationRequest` (resolution note, chosen level) -> `TransitionEscalationDto` |
| `GET /api/transitions/plans/client/{clientId}` | Coordinator, Clinician, Caregiver (assigned) | `IIdorGuard` + assignment/scope rules | Coordinator/Clinician: `TransitionPlanClinicalDto`; Caregiver: `TransitionPlanCareTeamDto`; otherwise 404 |

List scoping: Coordinator/Clinician unscoped for Transitions (D-S5-2); Caregiver only via
current shift assignment and only through care-team-safe DTOs; Client role only through the
patient-view/check-in routes. All PHI `{id}` routes keep byte-identical missing/denied 404
bodies with no body `TraceId` (D-S5-8).

## PHI and Audit Matrix (additions)

| Record | PHI class | Response/log rule |
|---|---|---|
| DischargeDocument | Clinical PHI | RawContent only via `/content` endpoint; never in logs/errors/audit values |
| TransitionPlan | Clinical PHI | Clinical vs patient-facing/care-team-safe DTO split (D-S5-3); activation audited as write |
| TransitionInstruction | Clinical PHI | SourceText clinical-only; never in patient DTOs, logs, or validation errors |
| TransitionReminder | PHI-adjacent | Message content never logged; guard failures use stable PHI-free codes |
| TransitionCheckIn | Clinical PHI | ResponsesJson never logged/serialized into errors; warning flags audited |
| TransitionEscalation | Clinical PHI | Trigger detail IDs only in audit; no symptom text in dashboards' log output |

---

## Task Board

Owners: **Claude** = PM/Contracts lead (Contracts, Client, sprint docs). **Codex** =
implementation (Domain/Application/Infrastructure/WebApi). **Tobi** = approvals.

| ID | Task | Owner | Depends on | Status |
|---|---|---|---|---|
| S5-TASK-001 | Approve this board + decisions D-S5-1..8 | Tobi | - | Done 2026-07-06 |
| S5-TASK-010 | Transitions EF configurations (6 entities) + `VisitNote.TransitionPlanId` mapping + DbSets, including explicit collection/backing-field relationship mapping per D-S5-1 rules 1-3 | Codex | S5-TASK-001 | Done 2026-07-06 (`39f84ff`) |
| S5-TASK-011 | `AddTransitions` migration: generate, review SQL vs D-S5-1, metadata tests (delete behaviors, column types, indexes), apply locally | Codex | S5-TASK-010 | Done 2026-07-06 (`39f84ff`) |
| S5-TASK-020 | Contracts: Transitions enum mirrors (11) + DTOs per D-S5-3 split (`DischargeDocumentDto`, `DischargeDocumentContentDto`, `TransitionPlanSummaryDto`, `TransitionPlanClinicalDto`, `TransitionPlanPatientFacingDto`, `TransitionInstructionClinicalDto`, `TransitionInstructionPatientFacingDto`, `TransitionPlanCareTeamDto`, `TransitionReminderDto`, `TransitionCheckInDto`, `TransitionEscalationDto`) + requests (`CreateDischargeDocumentRequest`, `ReviewInstructionRequest`, `ActivatePlanRequest`, `ScheduleReminderRequest`, `CreateCheckInRequest`, `AcknowledgeEscalationRequest`) + enum parity tests hook | Claude | S5-TASK-001 | Done 2026-07-06 — 11 enum mirrors (values verified against Domain, incl. ReminderType Diet=6/Activity=7 code order) + 11 DTOs (three-tier split; TransitionCheckInDto deliberately has no ResponsesJson; patient/care-team DTOs reuse TransitionInstructionPatientFacingDto) + 6 requests; 28 files compile 0 warnings. Codex: extend ContractEnumParityTests to the 11 new mirrors in slice 5A |
| S5-TASK-030 | Application: discharge intake command (metadata + raw text only; binary uploads rejected with stable code - storage gates are Sprint 7), document status query, `/content` query with audited read | Codex | S5-TASK-011, S5-TASK-020 | Done 2026-07-06 (`eba624e`) |
| S5-TASK-031 | Application: `IDischargeExtractionService` + deterministic Infrastructure stub + draft-plan assembly (plan `Draft`->`PendingVerification`, instructions `Pending`, confidence flags) per D-S5-4 | Codex | S5-TASK-030 | Done 2026-07-06 (`eba624e`) |
| S5-TASK-032 | Application: instruction review command (approve/modify/reject + clinician note) | Codex | S5-TASK-031 | Done 2026-07-06 (`42f0f91`) |
| S5-TASK-033 | Application: activation command per D-S5-5 (all-instructions-terminal check, existing `VerifiedBy`/`VerifiedAt`/`ActivatedAt` e-sign fields, `TransitionWindowEnd` computed in handler, write audit) | Codex | S5-TASK-032 | Done 2026-07-06 (`42f0f91`) |
| S5-TASK-034 | Application: reminder scheduling command + guard per D-S5-6 (`transition.plan_not_active`, `transition.outside_window`); records only | Codex | S5-TASK-033 | Done 2026-07-06 (`42f0f91`) |
| S5-TASK-035 | Application: check-in command (self/grantee auth) + escalation evaluator per D-S5-7 (records only; CoordinatorAlert default; level changes only via coordinator acknowledge) | Codex | S5-TASK-033 | Done 2026-07-06 (`6f6cb91`) |
| S5-TASK-036 | Application: VisitNote linkage - on submit during an active window, set `TransitionPlanId`; coordinator dashboard query (paged plans + escalation/check-in signals via `GetPagedAsync`) | Codex | S5-TASK-035 | Done 2026-07-06 (`6f6cb91`) |
| S5-TASK-037 | Clinician scoping update per D-S5-2 in `ProtectedResourceType`, `Sprint4ObjectAuthorizationService`, `ClientOperationsService` list/detail scoping, and `IClientAccessEvaluator`/query seams as needed + tests (transition clients readable; non-transition clients still denied) | Codex | S5-TASK-011 | Done 2026-07-06 (`6f6cb91`; hardened in `48fd8fd` — caregiver care-team access requires a current Scheduled/InProgress shift) |
| S5-TASK-040 | WebApi: `TransitionsController` per endpoint matrix; role policies; `IIdorGuard` on every `{id}` route; patient-view + check-in routes wired to grant/self evaluation | Codex | S5-TASK-030..037 | Done 2026-07-06 (`c71f9f6`; hardened in `48fd8fd` — self/grant evaluation runs BEFORE active/window checks so plan status is never disclosed to unauthorized callers) |
| S5-TASK-041 | Tests: safety-guard suite (pre-activation reminder block, stub-cannot-bypass-review, no-autonomous-escalation, window boundary at exactly +30 days), IDOR byte-identical 404s on all Transitions routes with no body TraceId, PHI audit emission for RawContent/SourceText reads, patient/caregiver exposure tests (no SourceText/RawContent/ResponsesJson/confidence/reviewer notes) | Codex | S5-TASK-040 | Done 2026-07-06 (`c71f9f6` + `48fd8fd` — incl. Transitions byte-identical 404s; middleware now omits null traceId key entirely) |
| S5-TASK-050 | Client: `TransitionsClient` typed client over `ApiClientBase` | Claude | S5-TASK-040 stable | Done 2026-07-06 — 15 methods covering all 14 matrix routes (role-shaped client-plan route split into `GetPlanForClientAsync`/`GetCareTeamPlanForClientAsync`); body-less `PostAsync` added to ApiClientBase for the extract trigger; builds 0 warnings. Uncommitted: `CarePath.Client/ApiClientBase.cs`, `CarePath.Client/Api/TransitionsClient.cs`, board |
| S5-TASK-060 | Exit verification: build 0 warnings, all tests green, reviewer pass, HIPAA spot check (RawContent/SourceText grep of log statements, no PHI in errors/URLs, storage-gate rejection verified), PROGRESS/lessons updated; PM closes after review | Codex + Claude + Tobi | all above | Done 2026-07-06 (`770e4a9`; S5-TASK-050 in `c7880fb`) — 579 tests green (D268/A237/I74); reviewer found and fixed 2 high issues (RawContent read-audit ordering; caregiver care-team active-window scoping), re-review clean; all four safety absolutes verified by named tests; binary intake rejected with `transition.intake_source_deferred`; exposure and byte-identical 404 tests pass; authorization-before-state lesson recorded in lessons.md |

### Success criteria (every task)

- Build zero warnings; tests green; reviewer subagent clean; synthetic test data only.
- Every Transitions PHI read/write audited (IDs + TraceId metadata only, never content values).
- Byte-identical missing/denied 404 bodies on all `{id}` routes; no reason codes or per-request TraceId in PHI 404 response bodies.
- `RawContent`/`SourceText`/`ResponsesJson` never in logs, exceptions, URLs, patient DTOs, or caregiver/care-team-safe DTOs.
- No reminder record for a non-Active plan can exist (guard + DB-state tests).
- Escalation records never trigger outbound actions; only coordinators change escalation level.
- `GetPagedAsync` for all plan/reminder/check-in list queries.

### Sequencing notes

- Critical path: 001 -> 010 -> 011 -> 030 -> 031 -> 032 -> 033 -> (034, 035) -> 036 -> 040 -> 041 -> 060.
- Claude's S5-TASK-020 runs immediately after approval, parallel with 5A, so 030 is never DTO-blocked.
- S5-TASK-037 (Clinician scoping) can run parallel to 5C/5D once the migration lands.
- Commit at the end of each slice with green build/tests; if two agents hold halves of one
  coordinated change, the second-half owner commits both together.
- Cross-ownership hardening edits are proposed in slice reports and applied by the owning agent
  (Sprint 4 board, D-S4-8 process note 3).

### Deferred (explicitly)

- Real AI/OCR extraction, Twilio SMS/voice delivery + webhook intake (System actor), FHIR
  import, multilingual translation, hospital outcome export -> Sprint 7 (with provider gates).
- PDF/photo binary persistence -> Sprint 7 (secure storage gates: private encrypted blobs,
  short-lived URLs, malware scanning, no public containers).
- Blazor coordinator dashboard/review UI -> Sprint 6 (this sprint delivers the queries/APIs).
