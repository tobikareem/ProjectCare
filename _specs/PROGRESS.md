# CarePath Health — Project Progress

Last updated: 2026-06-22

---

## CP-01 — Domain Layer

**Status**: Complete  
**All 39 tasks done.** 12 entities, 8 enumerations, `IRepository<T>` / `IUnitOfWork` interfaces, full test suite.

**Entities implemented**: `User`, `Caregiver`, `Client`, `CarePlan`, `Shift`, `VisitNote`, `VisitPhoto`, `Invoice`, `InvoiceLineItem`, `Payment`, `CaregiverCertification`

> Note: `CarePlan` lives in `Entities/Clinical/` in the actual code, not `Entities/Identity/` as the design spec states. Trust the actual file structure.

---

## CP-02 — CarePath Transitions

**Status**: Approved 2026-06-22 — implementation in progress

**What it is**: 30-day post-discharge care management. Intake → Verify → Guide → Escalate.

**6 new entities** (`Entities/Transitions/`):
- `DischargeDocument` — source upload
- `TransitionPlan` — clinician-verified plan (status: Draft → PendingVerification → Active → Completed)
- `TransitionInstruction` — extracted item with AI confidence score
- `TransitionReminder` — scheduled delivery (App / SMS / Voice via Twilio)
- `TransitionCheckIn` — patient symptom response
- `TransitionEscalation` — coordinator alert

**11 new enumerations** — see CLAUDE.md for full list

**Existing change**: `VisitNote` gains optional `TransitionPlanId` FK

**Spec files**:
- `_specs/01-requirements/cp-02-transitions.md`
- `_specs/02-design/cp-02-transitions.md`
- `_specs/03-tasks/cp-02-transitions.md` (TASK-020 through TASK-037)

**MVP phases**:
- Month 1: Domain entities + enumerations (TASK-020–024)
- Month 1–2: Infrastructure — EF Core, Twilio, AI extraction interface (TASK-025–028)
- Month 2: Application commands + queries (TASK-029–032)
- Month 2–3: WebApi controller (TASK-033)
- Month 4+: FHIR import, multilingual, outcome reporting (TASK-034–037)

**Domain layer complete** (2026-06-22):
- TASK-020 ✅ — 11 enumerations added to `Domain/Enumerations/`
- TASK-021 ✅ — 6 entities created in `Domain/Entities/Transitions/`
- TASK-022 ✅ — `VisitNote.TransitionPlanId` added
- TASK-023 ✅ — 3 repository interfaces added to `Domain/Interfaces/Repositories/`
- TASK-024 ✅ — 3 test files, 24 unit tests in `Domain.Tests/Entities/Transitions/`

**Next action**: Run `dotnet build CarePath.sln` and `dotnet test Domain.Tests` to verify. Then begin TASK-025 (EF Core DbContext additions) — Infrastructure layer.

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
