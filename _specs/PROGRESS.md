# CarePath Health — Project Progress

Last updated: 2026-06-27

---

## CP-01 — Domain Layer

**Status**: Complete  
**All 39 tasks done.** 12 entities, 8 enumerations, `IRepository<T>` / `IUnitOfWork` interfaces, full test suite.

**Entities implemented**: `User`, `Caregiver`, `Client`, `CarePlan`, `Shift`, `VisitNote`, `VisitPhoto`, `Invoice`, `InvoiceLineItem`, `Payment`, `CaregiverCertification`

> Note: `CarePlan` lives in `Entities/Clinical/` in the actual code, not `Entities/Identity/` as the design spec states. Trust the actual file structure.

---

## CP-02 — Infrastructure / EF Core

**Status**: Sprint 2 implementation in progress

**What it is**: EF Core persistence, SQL Server, Identity schema foundation, repositories, UnitOfWork, audit field population, soft-delete filters, UTC conversion, migrations, and synthetic seed strategy.

**Completed in current Sprint 2 slice**:
- `Infrastructure` and `Infrastructure.Tests` projects scaffolded and added to `CarePath.sln`.
- EF Core, SQL Server, Identity EF, EF tools/design, and EF InMemory package versions added.
- `CarePathDbContext` created for CP-01 entities only, with centralized UTC conversion and soft-delete conventions.
- `ApplicationUser` Identity schema foundation added and linked to Domain `User`.
- UTC converters, audit interceptor, DbContext smoke tests, soft-delete filter tests, and audit/converter tests added.

**Still remaining before migration**:
- Explicit CP-01 entity configurations with string lengths, indexes, decimal precision, ignored computed properties, and PHI-safe delete behaviors.
- Repository, UnitOfWork, DI/WebApi registration, migration, migration review, local database update, synthetic seed strategy, and full Infrastructure tests.
- CP-03 Transitions persistence is deferred until explicit Transitions configurations are in scope; do not map Transitions DbSets in CP-02 Phase 1.

**Spec files**:
- `_specs/01-requirements/cp-02-infrastructure-ef-core.md`
- `_specs/02-design/cp-02-infrastructure-ef-core.md`
- `_specs/03-tasks/cp-02-infrastructure-ef-core.md`

**Next action**: Implement CP-01 entity configurations and delete-behavior metadata tests before generating any migration.

---

## CP-03 — CarePath Transitions

**Status**: Approved for Domain slice; backend workflow planned for Sprint 5 after Infrastructure/Application prerequisites

**What it is**: 30-day post-discharge care management. Intake -> Verify -> Guide -> Escalate.

**6 new entities** (`Entities/Transitions/`):
- `DischargeDocument` — source upload
- `TransitionPlan` — clinician-verified plan (status: Draft -> PendingVerification -> Active -> Completed)
- `TransitionInstruction` — extracted item with AI confidence score
- `TransitionReminder` — scheduled delivery (App / SMS / Voice via Twilio)
- `TransitionCheckIn` — patient symptom response
- `TransitionEscalation` — coordinator alert

**11 new enumerations** — see AGENTS.md / CLAUDE.md for full list

**Existing change**: `VisitNote` has optional `TransitionPlanId` FK

**Spec files**:
- `_specs/01-requirements/cp-03-transitions.md`
- `_specs/02-design/cp-03-transitions.md`
- `_specs/03-tasks/cp-03-transitions.md` (TASK-020 through TASK-037)

**Domain layer complete** (2026-06-22):
- TASK-020 complete — 11 enumerations added to `Domain/Enumerations/`
- TASK-021 complete — 6 entities created in `Domain/Entities/Transitions/`
- TASK-022 complete — `VisitNote.TransitionPlanId` added
- TASK-023 complete — 3 repository interfaces added to `Domain/Interfaces/Repositories/`
- TASK-024 complete — 3 test files, 24 unit tests in `Domain.Tests/Entities/Transitions/`

**Next action**: Do not continue CP-03 backend work until CP-02 Infrastructure and Sprint 3 Application/contracts are in place.

---

## Application Layer — Not Yet Started

All features above require the Application layer (services, DTOs, validators, CQRS handlers). This is the next major milestone after spec approval. See `Documentation/Architecture.md` for planned structure.

---

## Documentation

| File | Description |
|---|---|
| `CLAUDE.md` | Coding conventions, architecture rules, full domain model, Transitions scope |
| `_specs/lessons.md` | Session notes, common mistakes, architectural decisions |
| `_specs/PROGRESS.md` | This file — project status and next actions |
| `Documentation/Architecture.md` | Full system architecture with layer diagrams |
| `Documentation/Wireframes/carepath-wireframe.html` | Functional web + mobile wireframe with working navigation |
| `Documentation/CarePath_Transitions_Feature_Presentation_v2.pptx` | 10-slide Transitions business case deck |
