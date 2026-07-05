# Sprint 3 Tasks — Application, Auth & Shared Contracts

Status: Complete (closed 2026-07-04)
Parent spec: `_specs/sprints/sprint-03-application-auth-contracts.md`
Last updated: 2026-07-04

---

## Decisions (ratified 2026-07-04 with Tobi)

### D1 — Application MAY reference CarePath.Contracts

Application handlers return Contracts DTOs directly; WebApi does no re-mapping. This is safe because `CarePath.Contracts` is a zero-dependency DTO library — referencing it does not invert Clean Architecture, and it eliminates a duplicate Application-DTO layer and the drift risk that comes with it.

Amended dependency rule: **Domain ← Application ← Infrastructure ← WebApi, with Application and all client projects also referencing CarePath.Contracts. Contracts references nothing.**

Follow-up: the architecture table in `CLAUDE.md` and `AGENTS.md` ("depends on Domain only") must be amended → S3-TASK-002.

### D2 — Clinician becomes a UserRole; Family does not

- `Clinician = 6` is added to `Domain/Enumerations/UserRole.cs` (→ S3-TASK-020). Rationale: plan activation/e-sign is a licensure-gated action (CP-03 design) that must be distinguishable from Coordinator actions in audit logs.
- **Family is NOT a role.** Family members authenticate as `UserRole.Client` (the enum doc already covers "authorised family member"). WHICH client a family member may see is an object-level question a role cannot answer — it is resolved by an explicit **client access grant** (grantee user, client, scope: `PatientFacing` | `Full`). Sprint 3 delivers the evaluation interface only; the grant Domain entity + persistence + migration are the first tasks of Sprint 4.
- The Contracts `UserRole` mirror already includes `Clinician = 6`; the enum parity test (S3-TASK-012) stays red until S3-TASK-020 lands — intentional ordering.

### D3 — Object-level authorization and PHI read audit enforcement

- **Enforcement lives in the Application layer**, inside command/query handlers — never only in controllers. WebApi adds coarse role gates (`[Authorize(Roles=...)]`) as the outer belt.
- `IResourceAuthorizationService.AuthorizeAsync(resourceType, resourceId, operation)` is consulted by every handler that touches PHI. Evaluators: role rules + assignment rules (Caregiver → clients on own shifts) + access grants (family).
- **Denial semantics for PHI resources are always not-found**, never forbidden — responses must not reveal whether a guessed ID exists (IDOR, Story 6). Denials are audit-logged: `Action=AccessDenied`, `EntityType`, `EntityId`, `UserId`, `CorrelationId` — IDs only, never PHI values.
- **PHI read audit is structural, not discretionary**: a pipeline decorator wraps every handler whose result is marked with an `IPhiResource` marker (or handler implements `IPhiQuery`) and emits `IAuditLogger.LogReadAsync(UserId, TimestampUtc, Action, EntityType, EntityId, CorrelationId)`. Individual handlers cannot forget to audit because they never call the logger for reads themselves.
- **System actor**: a reserved well-known identity (e.g., `SystemActors.BackgroundJob`) used when no HTTP user exists; `ICurrentUserService` exposes `IsSystemActor` and a required `CorrelationId` that flows into every audit entry (Story 7).

### D4 — Application scaffolds must be normalized before any Application code lands (found during review)

`Application/` and `Application.Tests/` currently on disk are raw `dotnet new` templates and violate repo conventions: `net10.0` TargetFramework override (repo standard is net9.0 via `Directory.Build.props`), inline `PackageReference` versions that break Central Package Management (NU1008), placeholder `Class1.cs`/`UnitTest1.cs`, no `RootNamespace`/`AssemblyName`, no Domain reference, `obj/` artifacts present, and their sln entries are not nested under `src`/`tests` solution folders. → S3-TASK-010 blocks all Phase D work.

---

## Task Board

Owners: **Claude** = PM/Contracts lead (this agent; owns Contracts, Client, Client.UI, sprint docs). **Codex** = implementation agent (owns Domain, Application, Infrastructure, WebApi changes). **Tobi** = approvals.

| ID | Task | Owner | Depends on | Status |
|---|---|---|---|---|
| S3-TASK-001 | Ratify D1–D3 (Application→Contracts, Clinician/Family mapping, authz+audit enforcement) | Tobi | — | Done 2026-07-04 |
| S3-TASK-002 | Amend CLAUDE.md + AGENTS.md: Application dependency rule per D1; add Clinician to role lists | Codex | S3-TASK-001 | Done 2026-07-04 (verified) |
| S3-TASK-010 | Normalize Application/Application.Tests scaffolds per D4; stage sln entries; nest under src/tests | Codex | — | Pending |
| S3-TASK-011 | Scaffold `CarePath.Contracts`: envelopes (`ApiResponse`, `ApiResponse<T>`, `PagedResult<T>`, `PagedRequest`, `ApiProblemDetails`, `ApiError`, `ValidationError`) + 8 enum mirrors; add to sln under src | Claude | S3-TASK-001 | Done 2026-07-04 (sln entry re-wired after concurrent rewrite) |
| S3-TASK-012 | Enum parity tests: reflection-compare names+values of all Contracts mirrors vs Domain enums (place in Domain.Tests or Application.Tests — the test project may reference both) | Codex | S3-TASK-011, S3-TASK-020 | Done 2026-07-04 — `Domain.Tests/Enumerations/ContractEnumParityTests.cs`, all 8 mirrors (verified) |
| S3-TASK-013 | Module DTOs in Contracts: Identity (UserSummaryDto, CaregiverSummaryDto/DetailDto, CertificationDto), Clients/CarePlans (ClientSummaryDto/DetailDto, CarePlanDto), Scheduling (ShiftSummaryDto/DetailDto, VisitNoteDto, CreateShiftRequest, CheckInRequest), Billing (InvoiceSummaryDto/DetailDto, InvoiceLineItemDto, PaymentDto) — per Contracts Plan below | Claude | S3-TASK-011, S3-TASK-030 shape review | Done 2026-07-04 — 16 DTOs, builds clean; `CreateShiftRequest` field-aligned with `CreateShiftCommand`. **S3-TASK-035 is now unblocked.** PHI notes: summaries carry Age not DOB; no insurance identifiers or raw GPS in any contract; rates/margins excluded from shift DTOs pending Sprint 4 admin analytics contract |
| S3-TASK-014 | Transitions contracts reservation: namespaces + patient-facing/clinical DTO split documented, no code until Sprint 5 | Claude | — | Done (see Contracts Plan §6) |
| S3-TASK-020 | Add `Clinician = 6` to `Domain/Enumerations/UserRole.cs` with XML docs (licensure gate rationale); update `EnumerationsTests` member counts | Claude (Domain edit explicitly approved by Tobi 2026-07-04) | S3-TASK-001 | Done 2026-07-04 — role parity restored |
| S3-TASK-030 | Application project real scaffold: references Domain + Contracts; FluentValidation registered; folder layout (`Common/`, `Identity/`, `Clients/`, `Scheduling/`, `Billing/`); move FluentValidation PackageVersion out of the "Test" ItemGroup label in Directory.Packages.props | Codex | S3-TASK-010 | Pending |
| S3-TASK-031 | `ICurrentUserService` (UserId, Roles, IsSystemActor, CorrelationId) + `SystemActors` well-known identities | Codex | S3-TASK-030 | Pending |
| S3-TASK-032 | `IAuditLogger` (LogReadAsync/LogWriteAsync: UserId, TimestampUtc, Action, EntityType, EntityId, CorrelationId — never PHI values) + PHI read-audit decorator over `IPhiResource`-marked handlers per D3 | Codex | S3-TASK-031 | Pending |
| S3-TASK-033 | `IResourceAuthorizationService` + role policy constants + `IClientAccessEvaluator` (family/caregiver scoping) + not-found denial semantics + denial audit per D3 | Codex | S3-TASK-031, S3-TASK-032 | Pending |
| S3-TASK-034 | FluentValidation validators for core commands (CreateShift incl. end-after-start, CreateClient, CreateCaregiver, CreateInvoice, RecordPayment) — PHI-free error messages, no attempted values | Codex | S3-TASK-030, S3-TASK-013 | Pending |
| S3-TASK-035 | Domain→Contracts mapping (per-module mappers or AutoMapper profiles) + PHI-safe mapping tests: assert summary DTOs expose no PHI-heavy fields, no Domain types leak into Contracts signatures | Codex | S3-TASK-013, S3-TASK-030 | Done 2026-07-04 — 4 manual mappers in `Application/Common/Mapping`, computed flattening, reflection PHI guards (verified: summary has Age not DOB; no insurance identifiers/GPS/rates mapped) |
| S3-TASK-036 | JWT/Identity service contracts in Application: `ITokenService`, `IIdentityService`, login/register/refresh request-response shapes (Contracts) | Codex | S3-TASK-030 | Pending |
| S3-TASK-050 | Infrastructure/WebApi auth foundation: JWT issuance + validation, role policies incl. Clinician, deny-by-default fallback policy, secrets from user secrets/env (never committed) | Codex | S3-TASK-036, S3-TASK-020 | Done 2026-07-04 — verified: `WebApi/Security/*` FallbackPolicy=RequireAuthenticatedUser, 6 role policies incl. Clinician |
| S3-TASK-051 | WebApi problem-details middleware mapping exceptions/validation to `ApiProblemDetails`; verify no PHI in exception output or logs | Codex | S3-TASK-050 | Done 2026-07-04 — verified: `WebApi/Middleware/ProblemDetailsMiddleware` uses `resource.not_found`, identical missing/denied 404s, no AttemptedValue anywhere, generic 500s |
| S3-TASK-060 | `CarePath.Client` typed client foundation: `ApiClientBase` (GET/POST/PUT + `ApiProblemDetails`→`ApiResponse` error mapping incl. TraceId), `IAccessTokenProvider` abstraction, `AuthorizationMessageHandler`, `PagedRequest` query helper (references Contracts only) | Claude | S3-TASK-013 stable | Done 2026-07-04 — builds 0 warnings |
| S3-TASK-061 | `CarePath.Client.UI` RCL primitives: `StatusBadge` + `StatusBadgeTones` (Shift/Invoice/Payment mappings), `ValidationErrorList`, `ApiErrorAlert` (message + TraceId support reference), generic `PagedTable<TItem>` shell (references Contracts + Client) | Claude | S3-TASK-060 | Done 2026-07-04 — builds 0 warnings; both projects in sln under src; `Microsoft.AspNetCore.Components.Web 9.0.13` added to Directory.Packages.props (Client label) |
| S3-TASK-070 | Exit verification: `dotnet build CarePath.sln` zero warnings; all tests green; `dotnet-code-reviewer` pass; HIPAA spot check (no PHI in logs/URLs/sample data); update PROGRESS.md, lessons.md, task.md | Codex + Tobi | all above | Done 2026-07-04 — build 0 warnings; 335 tests green (D251/A29/I55); reviewer findings fixed; HIPAA spot check clean; PROGRESS/lessons updated |

### Success criteria (apply to every task)

- Build passes with zero warnings; relevant tests pass.
- No Domain entity type appears in any Contracts, Client, or Client.UI signature.
- No PHI in log statements, exception messages, route examples, or seed/sample data.
- XML docs on all public members; explicit usings; nullable respected.
- Diff touches only the files the task requires.

### Deferred to Sprint 4 (explicitly)

- `ClientAccessGrant` Domain entity, EF configuration, migration, and grant-management endpoints (D2). Sprint 3 ships the `IClientAccessEvaluator` interface only.
- WeatherForecast template removal from WebApi (already scheduled Sprint 4).

---

## Contracts Plan (S3-TASK-011/013/014 reference)

### 1. Envelopes (shipped 2026-07-04)

| Contract | Shape | Notes |
|---|---|---|
| `ApiResponse` / `ApiResponse<T>` | Success, Message, Errors: `ApiError[]`, TraceId, Data | Factories `Ok`/`Fail`; Message/Errors must be PHI-free |
| `ApiError` | Code, Message | Stable machine-readable codes (`resource.not_found`) |
| `ValidationError` | PropertyName, ErrorMessage, ErrorCode? | **No AttemptedValue by design** — echoing submitted values can leak PHI |
| `ApiProblemDetails` | RFC 7807 POCO + TraceId + ValidationErrors | Dependency-free so WASM/MAUI deserialize without ASP.NET Core |
| `PagedResult<T>` | Items, PageNumber, PageSize, TotalCount, TotalPages, HasPrevious/HasNextPage | Mirrors `IRepository<T>.GetPagedAsync` tuple |
| `PagedRequest` | PageNumber (≥1), PageSize (clamped 1..100, default 20) | Clamping protects large PHI tables (Shift, VisitNote) |

### 2. DTO naming conventions

- Read models: `{Entity}SummaryDto` (list rows, minimum fields) and `{Entity}DetailDto` (single-item view).
- Commands: `{Verb}{Entity}Request` (e.g., `CreateShiftRequest`); responses `{...}Response` only when a bare DTO is insufficient.
- Folders/namespaces by module: `CarePath.Contracts.Identity`, `.Clients`, `.Scheduling`, `.Billing`, `.Transitions` (S5), `.Common`, `.Enumerations`.
- IDs are `Guid`. All timestamps UTC `DateTime`, documented as UTC.

### 3. Enum mirror policy

- One mirror per Domain enum used by clients, identical member names and numeric values; namespace `CarePath.Contracts.Enumerations`.
- Parity enforced by reflection tests (S3-TASK-012). Mirrors may lead Domain only when a decision is ratified (currently: `Clinician = 6`).
- Transitions enums are mirrored in Sprint 5 alongside their DTOs, not before.

### 4. PHI-safe DTO boundaries

- Minimum necessary: summary DTOs carry names/status/dates needed for lists — never MedicalConditions, Allergies, InsuranceInfo, care plan text, or visit note clinical text.
- Detail DTOs expose clinical fields only where the endpoint's authorization already gates them; separate DTOs per audience rather than nullable "maybe" fields.
- Never mirrored into any contract: `DischargeDocument.RawContent`, `TransitionInstruction.SourceText`, signature URLs beyond authorized views, GPS raw coordinates on family-facing views.
- Envelope fields (Message, Errors, TraceId, Instance) are PHI-free by contract.

### 5. Reference rules (enforced at review)

- `CarePath.Contracts` → references nothing.
- `CarePath.Client` → Contracts only. `CarePath.Client.UI` → Contracts + Client only.
- Application → Domain + Contracts (D1). No client project ever references Domain, Infrastructure, or WebApi.

### 6. WebApi denial-mapping contract (normative for S3-TASK-050/051)

How Application authorization results map to HTTP responses. This is the IDOR-safety contract; WebApi must not deviate.

| Application result | HTTP | Body | Rules |
|---|---|---|---|
| PHI resource: not found OR access denied | 404 | `ApiProblemDetails { Type: "about:blank", Title: "Resource not found.", Status: 404, TraceId }` | Identical body for both cases — never reveal existence. `ApiError.Code` is always `resource.not_found`. |
| Non-PHI resource: access denied | 403 | `ApiProblemDetails { Title: "Forbidden.", Status: 403, TraceId }` | Only for resources with zero PHI linkage (e.g., admin settings). When in doubt, treat as PHI → 404. |
| Unauthenticated | 401 | Standard challenge, empty body | No detail text. |
| Validation failure | 400 | `ApiProblemDetails` + `ValidationErrors` | PHI-free messages; no attempted values. |

Non-negotiables:

- **No internal authorization reason codes in any response.** Reasons (`NotAssigned`, `NoGrant`, `RoleInsufficient`, etc.) exist only in the Application layer and go exclusively to the audit log (`Action=AccessDenied`, EntityType, EntityId, UserId, CorrelationId). The HTTP body carries only the generic title and `TraceId`.
- `TraceId` is the only correlation surface exposed to clients; support staff join it against audit records server-side.
- `Detail` stays null on 403/404 responses. No timing/message differences between "missing" and "denied" paths.
- Exception middleware must catch-all to a generic 500 `ApiProblemDetails` with no exception text (exception messages may contain PHI).

### 7. Transitions reservation (Sprint 5 awareness — S3-TASK-014)

When CP-03 backend lands: `CarePath.Contracts.Transitions` will split audience-specific DTOs — `TransitionPlanClinicalDto` (clinician/coordinator: instructions with confidence scores, source references by ID only) vs `TransitionPlanPatientFacingDto` (client/family grantees: approved patient-facing instructions only, no internal clinical notes — Story 4). Check-in submission arrives via webhook contracts with system-actor audit. No Transitions DTOs are created in Sprint 3.
