# CarePath Health - Project Progress

Last updated: 2026-07-06

---

## CP-01 - Domain Layer

**Status**: Complete

12 core entities, 8 core enumerations plus CP-03 Transitions Domain additions, repository interfaces, and pure Domain tests are in place.

---

## CP-02 - Infrastructure / EF Core

**Status**: Complete for Sprint 2 foundation

EF Core SQL Server persistence, ASP.NET Core Identity schema foundation, repositories, UnitOfWork, audit field population, UTC conversion, soft-delete filters, initial migration, DI registration, and synthetic development seeding are implemented.

CP-03 Transitions persistence remains deferred until the reviewed Sprint 5 backend slice.

---

## Sprint 3 - Application, Auth & Shared Contracts

**Status**: Implementation complete pending PM sprint close

Completed Sprint 3 slices:
- Shared `CarePath.Contracts` envelopes, enum mirrors, and module DTOs for Identity, Clients/CarePlans, Scheduling, and Billing.
- `CarePath.Client` typed client foundation and `CarePath.Client.UI` reusable primitives.
- Application auth/audit abstractions, `IdorGuard`, and `CreateShiftCommand` validation foundation.
- JWT auth foundation, Identity service, role policies including Clinician, deny-by-default fallback authorization, and IDOR-safe problem-details middleware.
- Domain-to-Contracts manual mappers in Application for Identity, Clients, Scheduling, and Billing.
- PHI boundary tests: enum parity, mapper computed flattening, DTO reflection guards, no Domain type exposure in contract signatures, validation response safety, and identical PHI missing/denied 404 response behavior.

Verification from final Sprint 3 implementation slice:
- `dotnet build CarePath.sln` passed with 0 warnings.
- `dotnet test CarePath.sln` passed: Domain 251, Application 29, Infrastructure 55.
- `dotnet-code-reviewer` reviewed the mapping slice; findings were fixed.
- HIPAA spot check completed: no PHI values in new logs/exceptions/URLs, no direct persisted signature URL mapping, validation responses do not echo submitted values, and PHI missing vs denied responses remain byte-identical.

PM agent still owns sprint board/spec close-out. Do not mark Sprint 3 complete here.

---

## Sprint 4 - Core Operations Backend

**Status**: Exit verification complete pending PM sprint close

Completed Sprint 4 slices:
- Client access grants, infrastructure evaluator, and grant management endpoints.
- Caregiver, client, care plan, scheduling, visit documentation, billing, invoice, and Admin margin Application workflows.
- Authenticated WebApi controllers for Sprint 4 endpoint matrix coverage.
- CarePath.Client typed module clients for caregivers, clients/care plans/grants, shifts/visit notes, visit photos, billing, and Admin margins.
- Sprint 4 test suites for handler validation/auth/audit behavior, guard failures, grant behavior, billing behavior, controller role declarations, DTO boundary reflection, and PHI missing/denied 404 identity.

Verification from S4-TASK-060 final slice:
- `dotnet build CarePath.sln` passed with 0 warnings and 0 errors.
- `dotnet test CarePath.sln` passed: Domain 257, Application 148, Infrastructure 66.
- `dotnet-code-reviewer` reviewed the full Sprint 4 diff `d9e1749..HEAD`; findings addressed before commit.
- HIPAA spot check completed: no `AttemptedValue` in WebApi/Application owned code; no rate/margin/compensation response DTO fields outside Admin margin DTOs; PHI missing vs denied 404 bodies remain byte-identical; logs and exception messages are PHI-free metadata only; no request-body logging is configured for provisioning routes.

PM agent still owns sprint board/spec close-out. Do not mark Sprint 4 complete here.

---
## CP-03 - CarePath Transitions

**Status**: Approved for Domain slice; backend workflow planned for Sprint 5 after Infrastructure/Application prerequisites

30-day post-discharge care management: Intake -> Verify -> Guide -> Escalate.

Domain layer complete:
- 6 Transitions entities in `Domain/Entities/Transitions/`.
- 11 Transitions enumerations.
- `VisitNote.TransitionPlanId` placeholder for future integration.
- Repository interfaces and pure Domain tests.

Backend implementation waits for Sprint 5.

---

## Documentation

| File | Description |
|---|---|
| `AGENTS.md` | Coding conventions, architecture rules, full domain model, Sprint 3 shared-client architecture |
| `CLAUDE.md` | Companion agent instructions and project conventions |
| `_specs/lessons.md` | Recurring implementation lessons and corrections |
| `_specs/PROGRESS.md` | This file - project status and next actions |
| `Documentation/Architecture.md` | Full system architecture with layer diagrams |