# Sprint 4 Tasks - Core Operations Backend

Status: Approved
Parent spec: `_specs/sprints/sprint-04-core-operations-backend.md`
Last updated: 2026-07-04

Baseline: Sprint 3 complete and merged to `main` (PR #11). All Sprint 4 work builds on the
Application boundary, auth foundation, denial-mapping contract (normative section 6 of
`sprint-03-tasks.md`), Contracts DTOs, and client libraries.

This task board is the normative implementation contract for Sprint 4. If this file conflicts
with the parent Sprint 4 spec, update the parent spec first or stop for PM resolution before
implementation.

---

## Decisions

### D-S4-1 - ClientAccessGrant

Family-proxy object-level access becomes real through a `ClientAccessGrant` Domain entity:

- Fields: `Id`, `GranteeUserId`, `ClientId`, `AccessScope`, `GrantedByUserId`, `GrantedAtUtc`,
  `RevokedByUserId`, `RevokedAtUtc`, audit fields from `BaseEntity`.
- `AccessScope` enum values: `PatientFacing`, `Full`.
- `PatientFacing` grants only explicitly patient-facing read models. It must not authorize
  operational staff fields, rates, margins, raw care-plan clinical text, or internal notes.
- `Full` grants the client/family proxy access to PHI-bearing client records that the endpoint
  explicitly allows for Client-role users.
- The actual care recipient has implicit self-access through `Client.UserId`; a grant is for
  other Client-role users such as family proxies.
- Revoked grants and soft-deleted grants never authorize access.
- EF configuration uses `Restrict` delete behavior and indexes `(GranteeUserId, ClientId)`.
- Grant reads/writes are PHI-adjacent and audit logged without PHI values.
- Grant management endpoints are Admin/Coordinator only.

Infrastructure implements the existing `IClientAccessEvaluator`; WebApi still uses the
Sprint 3 denial contract so missing and denied PHI resources remain byte-identical 404 bodies.

### D-S4-2 - Margin Data Is Separate and Admin-Gated

Shift DTOs deliberately exclude rates. Sprint 4 adds `CarePath.Contracts.Billing` margin DTOs
(`ShiftMarginDto`, `MarginSummaryDto`) returned only by Admin-policy endpoints.

Semantics per `_specs/decisions/0002-shift-margin-semantics.md`:

- `GrossMargin` is total shift margin.
- `HourlyGrossMargin` is `BillRate - PayRate`.
- Summary results split In-Home and Facility service lines.
- No rate, margin, or compensation fields appear in normal shift summary/detail DTOs.

### D-S4-3 - VisitPhoto Storage Is Interface-Only This Sprint

`IFileStorageService` is defined in Application for opaque object IDs and short-lived read URLs.
Sprint 4 ships only a development implementation backed by local, private, non-browsable storage.

Real provider work is deferred to Sprint 7: Azure Blob private containers, encryption, malware
scanning, signed URLs, BAA/provider readiness, and production retention rules.

Sprint 4 DTO rules:

- Photo/signature URL fields remain null unless the signed-URL service exists.
- No photo bytes, filesystem paths, blob keys, raw GPS coordinates, or signed URL tokens appear
  in logs, routes, errors, audit details, or test data.
- VisitPhoto metadata is PHI-adjacent and audited on read/write.

### D-S4-4 - Guard Evaluation Lives in Application

Double-booking and expired-certification guards run inside Application command handlers, not in
controllers and not only through database constraints.

Guard failures are validation/business-rule failures, not IDOR cases. They return 400 or 409
`ApiProblemDetails` with stable PHI-free codes:

- `shift.double_booked`
- `caregiver.certification_expired`

Messages must not name a client, caregiver, address, diagnosis, care task, or hidden schedule
detail. They may refer only to fields submitted by the authorized caller.

### D-S4-5 - User/Profile Provisioning

Caregiver and Client creation is a single Application workflow:

- Create or link the Domain `User`.
- Create the role-specific Domain profile (`Caregiver` or `Client`).
- Provision the corresponding ASP.NET Identity account and role through an Application
  abstraction implemented in Infrastructure.
- Temporary password or invite material is never committed, logged, returned, or included in
  validation errors. Sprint 4 may accept a one-time temporary password in an authenticated
  Admin/Coordinator request body; invite-email delivery is out of scope.
- Request-body logging must be disabled (or provisioning routes excluded) so credential material
  can never reach logs; validators for provisioning requests never echo submitted values.
- Email uniqueness and role assignment failures must roll back the Domain profile creation.

### D-S4-6 - Billing and Invoice Rules

Invoice creation in Sprint 4 is deterministic and idempotent by billing period:

- Only completed shifts with billable hours greater than zero are invoice candidates.
- Draft/cancelled/no-show shifts are excluded.
- The request must include client, service line, period start, and period end.
- Creating the same client/service-line/period invoice twice returns a PHI-free conflict.
- Line item descriptions are generic and PHI-free.
- Taxes, adjustments, refunds, write-offs, and payer integrations are out of scope.
- Payments update `AmountPaid`, `Balance`, and invoice status through the Domain calculation
  flow; attempted payment values are never echoed in validation errors.

### D-S4-7 (PM amendment) - VisitNote contract split

Sprint 3 shipped a single `VisitNoteDto`. The endpoint matrix correctly needs a summary/detail
split so paged lists never ship clinical free-text. In S4-TASK-020 (Claude):

- Rename `VisitNoteDto` -> `VisitNoteDetailDto` (pre-release rename; Codex updates the
  Scheduling mapper reference in the same slice).
- Add `VisitNoteSummaryDto`: Id, ShiftId, CaregiverId, VisitDateTime, task booleans, and a
  server-computed `HasConcerns` flag only — NO Activities/ClientCondition/Concerns/Medications
  text, vitals, or signature URLs in list rows.
- Add `VisitPhotoDto` (metadata only: Id, VisitNoteId, TakenAt, caption if PHI-reviewed; URL
  null until signed-URL service exists per D-S4-3).
- Matrix references `CertificationDto` (the existing Sprint 3 name), not
  `CaregiverCertificationDto`.

---

## Implementation Slices

Use these slices for implementation order. Do not begin a later slice if an earlier slice is
failing build/tests.

| Slice | Goal | Includes |
|---|---|---|
| 4A | WebApi cleanup and access-grant foundation | S4-TASK-010..012 |
| 4B | Contracts requests and margin DTOs | S4-TASK-020..021 |
| 4C | Caregiver/client/care-plan workflows | S4-TASK-030..031 |
| 4D | Scheduling guards | S4-TASK-032..033 |
| 4E | Visit documentation and storage abstraction | S4-TASK-034 |
| 4F | Billing and margin visibility | S4-TASK-035 |
| 4G | Controllers, integration tests, clients, exit verification | S4-TASK-040..060 |

---

## Endpoint Matrix

Routes are proposed route contracts for Sprint 4. Controllers may add narrower helper endpoints
only if they preserve these authorization and PHI rules.

| Area | Method/Route | Roles | Object Auth | Request/Response |
|---|---|---|---|---|
| Caregivers | `GET /api/caregivers` | Admin, Coordinator | Filtered query | `PagedRequest` -> `PagedResult<CaregiverSummaryDto>` |
| Caregivers | `GET /api/caregivers/{id}` | Admin, Coordinator, Caregiver | Self or staff policy | `CaregiverDetailDto` |
| Caregivers | `POST /api/caregivers` | Admin, Coordinator | None | `CreateCaregiverRequest` -> `CaregiverDetailDto` |
| Caregivers | `PUT /api/caregivers/{id}` | Admin, Coordinator | Staff policy | `UpdateCaregiverRequest` -> `CaregiverDetailDto` |
| Certifications | `POST /api/caregivers/{id}/certifications` | Admin, Coordinator | Staff policy | `AddCertificationRequest` -> `CertificationDto` |
| Certifications | `GET /api/caregivers/certifications/expiring` | Admin, Coordinator | Filtered query | `PagedResult<CertificationDto>` |
| Clients | `GET /api/clients` | Admin, Coordinator, Clinician | Filtered query | `PagedResult<ClientSummaryDto>` |
| Clients | `GET /api/clients/{id}` | Admin, Coordinator, Clinician, Client, Caregiver | `IIdorGuard` plus grant/self/assignment rules | `ClientDetailDto` |
| Clients | `POST /api/clients` | Admin, Coordinator | None | `CreateClientRequest` -> `ClientDetailDto` |
| Clients | `PUT /api/clients/{id}` | Admin, Coordinator | `IIdorGuard` | `UpdateClientRequest` -> `ClientDetailDto` |
| CarePlans | `GET /api/clients/{clientId}/care-plans` | Admin, Coordinator, Clinician, Client, Caregiver | `IIdorGuard` | `PagedResult<CarePlanDto>` |
| CarePlans | `POST /api/clients/{clientId}/care-plans` | Admin, Coordinator, Clinician | `IIdorGuard` | `CreateCarePlanRequest` -> `CarePlanDto` |
| CarePlans | `PUT /api/care-plans/{id}` | Admin, Coordinator, Clinician | `IIdorGuard` | `UpdateCarePlanRequest` -> `CarePlanDto` |
| Shifts | `GET /api/shifts` | Admin, Coordinator, Caregiver, Client, FacilityManager, Clinician | Filtered query | `PagedResult<ShiftSummaryDto>` |
| Shifts | `GET /api/shifts/{id}` | Admin, Coordinator, Caregiver, Client, FacilityManager, Clinician | `IIdorGuard` | `ShiftDetailDto` |
| Shifts | `POST /api/shifts` | Admin, Coordinator | Assignment guard | `CreateShiftRequest` (maps to `CreateShiftCommand`) -> `ShiftDetailDto` |
| Shifts | `PUT /api/shifts/{id}` | Admin, Coordinator | `IIdorGuard` plus assignment guard | `UpdateShiftRequest` -> `ShiftDetailDto` |
| Shifts | `POST /api/shifts/{id}/check-in` | Caregiver | Assigned caregiver only | `CheckInRequest` -> `ShiftDetailDto` |
| Shifts | `POST /api/shifts/{id}/check-out` | Caregiver | Assigned caregiver only | `CheckOutRequest` -> `ShiftDetailDto` |
| VisitNotes | `GET /api/visit-notes/{id}` | Admin, Coordinator, Clinician, Client, Caregiver | `IIdorGuard` | `VisitNoteDetailDto` |
| VisitNotes | `GET /api/shifts/{shiftId}/visit-notes` | Admin, Coordinator, Clinician, Client, Caregiver | `IIdorGuard` | `PagedResult<VisitNoteSummaryDto>` |
| VisitNotes | `POST /api/shifts/{shiftId}/visit-notes` | Caregiver | Assigned caregiver only | `CreateVisitNoteRequest` -> `VisitNoteDetailDto` |
| VisitPhotos | `POST /api/visit-notes/{id}/photos` | Caregiver | Assigned caregiver only | metadata/upload request -> `VisitPhotoDto` |
| Billing | `GET /api/invoices` | Admin, Coordinator | Filtered query | `PagedResult<InvoiceSummaryDto>` |
| Billing | `GET /api/invoices/{id}` | Admin, Coordinator, Client | `IIdorGuard` | `InvoiceDetailDto` |
| Billing | `POST /api/invoices` | Admin, Coordinator | Billing rule checks | `CreateInvoiceRequest` -> `InvoiceDetailDto` |
| Billing | `POST /api/invoices/{id}/payments` | Admin, Coordinator | `IIdorGuard` | `RecordPaymentRequest` -> `InvoiceDetailDto` |
| Margins | `GET /api/billing/margins` | Admin | Admin policy only | `MarginSummaryDto` |
| Margins | `GET /api/billing/margins/shifts` | Admin | Admin policy only | `PagedRequest` + period filter -> `PagedResult<ShiftMarginDto>` |
| Grants | `GET /api/clients/{clientId}/access-grants` | Admin, Coordinator | `IIdorGuard` | `IReadOnlyList<ClientAccessGrantDto>` |
| Grants | `POST /api/clients/{clientId}/access-grants` | Admin, Coordinator | `IIdorGuard` | `CreateGrantRequest` -> `ClientAccessGrantDto` |
| Grants | `DELETE /api/clients/{clientId}/access-grants/{grantId}` | Admin, Coordinator | `IIdorGuard` | 204 (soft revoke: sets RevokedAt/RevokedBy — never a hard delete) |

Every `{id}` route that touches PHI or PHI-adjacent records must preserve the Sprint 3
not-found/denied byte-identical 404 contract.

List-scoping rules ("Filtered query" definition, enforced in Application query handlers):

- Admin/Coordinator: unscoped (organization-wide).
- Clinician: clients and shifts only for clients with a plan/care relationship they can read.
- Caregiver: own shifts, own certifications, and clients only via current shift assignment.
- Client: own records plus records of clients they hold an unrevoked grant for (scope-limited
  per D-S4-1).
- FacilityManager: staffing-service-line shifts and invoices for their facility only.
- A caller outside scope gets an empty page, never an error revealing that data exists.

---

## Application Handler Matrix

| Area | Commands/Queries | Required concerns |
|---|---|---|
| Caregivers | Create/update caregiver, add/renew certification, get caregiver, paged caregivers, expiring certifications | Validation, Identity provisioning, role policy, audit for certification reads/writes |
| Clients | Create/update client, get client, paged clients | Validation, Identity provisioning, `IClientAccessEvaluator`, PHI read/write audit |
| CarePlans | Create/update/get/list care plans | Validation, `IIdorGuard`, PHI audit, no PHI in errors |
| Scheduling | Create/update/cancel shift, check-in, check-out, get/list shifts | Assignment guard, expired-cert guard, no raw GPS in contracts/logs, PHI audit |
| Visit documentation | Submit VisitNote, list/get notes, attach VisitPhoto metadata | Assigned-caregiver guard, storage abstraction, PHI audit, no bytes/paths in logs |
| Billing | Create invoice, record payment, get/list invoices, margin summary | Idempotency, completed-shift filter, Admin-only margins, no attempted values |
| Grants | Create/list/revoke grants | Admin/Coordinator policy, grant audit, revoked grants ignored |

---

## PHI and Audit Matrix

| Record | PHI class | Audit required | Response/log rule |
|---|---|---|---|
| Client | PHI | Read/write/delete | Summaries expose Age, never DateOfBirth; no insurance/Medicaid fields in contracts |
| CarePlan | Clinical PHI | Read/write/delete | No diagnosis/care instructions in logs or errors |
| Shift | PHI-adjacent/operational PHI | Read/write/delete | No rates in normal shift DTOs; no address/client/caregiver names in guard errors |
| VisitNote | Clinical PHI | Read/write/delete | No note text, vitals, condition, or concerns in logs/errors |
| VisitPhoto | Clinical PHI | Read/write/delete | No bytes, paths, blob keys, GPS coordinates, or signed tokens in logs/routes |
| Invoice | PHI-adjacent billing record | Read/write/delete | Generic line descriptions; no attempted values in validation errors |
| CaregiverCertification | PHI-adjacent employment/credential record | Read/write/delete | No document contents or license identifiers in logs/errors |
| ClientAccessGrant | PHI-adjacent access-control record | Read/write/delete | Audit by IDs and TraceId only; no reason codes in HTTP denial bodies |

All audit entries join to HTTP failures by TraceId. Internal authorization reason codes stay in
audit only and must never appear in HTTP responses.

---

## Task Board

Owners: **Claude** = PM/Contracts lead (Contracts, Client, Client.UI, sprint docs).
**Codex** = implementation (Domain, Application, Infrastructure, WebApi). **Tobi** = approvals.

| ID | Task | Owner | Depends on | Status |
|---|---|---|---|---|
| S4-TASK-001 | Approve this board + decisions D-S4-1..7 | Tobi | - | Done |
| S4-TASK-010 | Remove WeatherForecast controller/model/.http sample from WebApi | Codex | S4-TASK-001 | Done 2026-07-05 (`d9e1749`, verified) |
| S4-TASK-011 | `ClientAccessGrant` Domain entity + `AccessScope` enum + XML docs + domain tests | Codex | S4-TASK-001 | Done 2026-07-05 (`d9e1749`, verified on disk) |
| S4-TASK-012 | ClientAccessGrant EF configuration, migration, `IClientAccessEvaluator` Infrastructure implementation + tests | Codex | S4-TASK-011 | Done 2026-07-05 (`d9e1749` — config, `AddClientAccessGrants` migration, `Infrastructure/Auth/ClientAccessEvaluator`, DI, tests) |
| S4-TASK-020 | Contracts: command/request DTOs for caregivers, clients, care plans, shifts, visit notes, invoices, payments, and grants; VisitNote summary/detail split + VisitPhotoDto per D-S4-7 | Claude | S4-TASK-001 | Done 2026-07-04 — 15 request/DTO files + `AccessScope` mirror; `VisitNoteDto` deleted → `VisitNoteDetailDto` + `VisitNoteSummaryDto` (HasConcerns flag, no clinical text) + `VisitPhotoDto`. **⚠ Codex: the Application `SchedulingContractMapper` still references `VisitNoteDto` — include the one-line rename to `VisitNoteDetailDto` in your FIRST commit (slice 4A), not 4E, or `dotnet build CarePath.sln` stays red.** New files compile 0 warnings |
| S4-TASK-021 | Contracts: margin DTOs (`ShiftMarginDto`, `MarginSummaryDto` with service-line split) per D-S4-2 | Claude | S4-TASK-001 | Done 2026-07-04 — + `ServiceLineMarginDto` (ShiftCount, TotalBillableHours, TotalRevenue, TotalLaborCost, TotalGrossMargin, AverageHourlyGrossMargin, GrossMarginPercentage); semantics per decision 0002 |
| S4-TASK-030 | Application: caregiver + certification commands/queries with validators, user provisioning, authz, audit | Codex | S4-TASK-020 | Pending |
| S4-TASK-031 | Application: client + care plan commands/queries with validators, object-level authz, grant evaluation, PHI read audit | Codex | S4-TASK-012, S4-TASK-020 | Pending |
| S4-TASK-032 | Application: scheduling commands/queries - create/update/cancel shift, check-in/check-out, paged shift dashboard query via `GetPagedAsync` | Codex | S4-TASK-020 | Pending |
| S4-TASK-033 | Application: double-booking guard and expired-certification assignment guard per D-S4-4, with boundary-overlap tests | Codex | S4-TASK-032 | Pending |
| S4-TASK-034 | Application: VisitNote/VisitPhoto commands/queries + `IFileStorageService` interface + dev implementation per D-S4-3 | Codex | S4-TASK-020 | Pending |
| S4-TASK-035 | Application: billing commands/queries - create invoice from completed shifts, record payment, status recalculation, margin queries | Codex | S4-TASK-021 | Pending |
| S4-TASK-040 | WebApi: authenticated controllers for Caregivers, Clients, CarePlans, Shifts, VisitNotes, Invoices, ClientAccessGrants | Codex | S4-TASK-030..035 | Pending |
| S4-TASK-041 | API integration tests for authorization matrix, IDOR, validation, guard failures, grant behavior, PHI audit emission | Codex | S4-TASK-040 | Pending |
| S4-TASK-050 | Client: typed module clients in `CarePath.Client` over `ApiClientBase` | Claude | S4-TASK-040 stable | Pending |
| S4-TASK-060 | Exit verification: build 0 warnings, all tests green, reviewer pass, HIPAA spot check, PROGRESS.md/lessons.md updated | Codex + Claude + Tobi | all above | Pending |

### Success Criteria

- Build zero warnings; relevant tests pass; reviewer subagent clean.
- Every PHI read goes through audit logging; every `{id}` PHI route goes through IDOR/object auth.
- Missing PHI resources and denied PHI resources keep byte-identical 404 response bodies.
- No PHI in logs, exception text, URLs, guard messages, validation attempted values, or test data.
- Contracts stay zero-dependency; no Domain types in client-facing signatures.
- `GetPagedAsync` is used for Shift and VisitNote queries; do not use `GetAllAsync` on large tables.
- Rate and margin fields are only available through Admin-gated margin endpoints.

### Required Tests

- Domain tests for `ClientAccessGrant`, `AccessScope`, computed properties, and billing status flow.
- Application tests for every handler validation path, auth path, audit emission, and PHI-free failure.
- Guard tests for touching endpoints, contained overlaps, spanning overlaps, cancelled/completed shift exclusions, and expired certification boundary dates.
- Reflection tests that no Domain type appears in public Contracts-facing mapper/controller signatures.
- Integration tests asserting missing vs denied PHI resources have identical 404 bodies.
- HIPAA grep checks for `AttemptedValue`, raw GPS names (`Latitude`, `Longitude`) in contract output, and PHI-like seeded/test data.

### Sequencing Notes

- Critical path: 001 -> 011 -> 012 -> 031 -> 040 -> 041 -> 060.
- Claude's 020/021 run immediately after approval, in parallel with Codex's 010..012, so
  Application tasks are never DTO-blocked.
- S4-TASK-050 waits until controller routes stabilize.
- Do not mark Sprint 4 complete in this file; PM closes the sprint after review.

### Deferred

- Real blob storage provider, malware scanning, and production signed URLs -> Sprint 7.
- Transitions workflows -> Sprint 5; any VisitNote/TransitionPlan linkage remains dormant.
- SignalR real-time schedule updates -> Sprint 6/7.
- Invite email delivery, SMS/voice, payer integrations, refunds, adjustments, and taxes -> later sprints.
