# CP-02 — CarePath Transitions: Tasks

**Status**: Approved  
**Created**: 2026-06-22  
**Depends on**: CP-01 (complete)  
**Design spec**: `_specs/02-design/cp-02-transitions.md`  
**Requirements spec**: `_specs/01-requirements/cp-02-transitions.md`

> All specs must reach **Approved** status before implementation begins.
> Tasks are sequenced by dependency. Never skip ahead.

---

## Phase 1 — Domain Layer (MVP Core — Month 1)

### TASK-020 — Add Transitions enumerations
**Estimate**: 1 hour  
**Files**:
- `CarePath.Domain/Enumerations/DischargeDocumentSourceType.cs`
- `CarePath.Domain/Enumerations/DischargeDocumentStatus.cs`
- `CarePath.Domain/Enumerations/TransitionPlanStatus.cs`
- `CarePath.Domain/Enumerations/TransitionRiskLevel.cs`
- `CarePath.Domain/Enumerations/TransitionInstructionCategory.cs`
- `CarePath.Domain/Enumerations/TransitionInstructionStatus.cs`
- `CarePath.Domain/Enumerations/ReminderType.cs`
- `CarePath.Domain/Enumerations/ReminderChannel.cs`
- `CarePath.Domain/Enumerations/ReminderStatus.cs`
- `CarePath.Domain/Enumerations/EscalationTriggerType.cs`
- `CarePath.Domain/Enumerations/EscalationLevel.cs`

**Acceptance**: `dotnet build` passes, all 11 enums present in `CarePath.Domain.Enumerations` namespace.

---

### TASK-021 — Create Transitions domain entities
**Estimate**: 3 hours  
**Depends on**: TASK-020  
**Files**:
- `CarePath.Domain/Entities/Transitions/DischargeDocument.cs`
- `CarePath.Domain/Entities/Transitions/TransitionPlan.cs`
- `CarePath.Domain/Entities/Transitions/TransitionInstruction.cs`
- `CarePath.Domain/Entities/Transitions/TransitionReminder.cs`
- `CarePath.Domain/Entities/Transitions/TransitionCheckIn.cs`
- `CarePath.Domain/Entities/Transitions/TransitionEscalation.cs`

**Rules**: All entities inherit `BaseEntity`. Explicit `using` directives. XML docs on all public members. Computed properties must be pure C# (no EF involvement). Follow designs in `_specs/02-design/cp-02-transitions.md` exactly.

**Acceptance**: `dotnet build` passes zero warnings. Entities compile with correct namespace and base class.

---

### TASK-022 — Add TransitionPlanId to VisitNote
**Estimate**: 30 minutes  
**Depends on**: TASK-021  
**Files**:
- `CarePath.Domain/Entities/Scheduling/VisitNote.cs`

**Change**: Add `public Guid? TransitionPlanId { get; set; }` with XML doc comment. No FK navigation property on VisitNote (avoid circular reference; query via TransitionPlan side).

**Acceptance**: Build passes. Existing VisitNote tests still pass.

---

### TASK-023 — Add Transitions repository interfaces
**Estimate**: 1 hour  
**Depends on**: TASK-021  
**Files**:
- `CarePath.Domain/Interfaces/Repositories/IDischargeDocumentRepository.cs`
- `CarePath.Domain/Interfaces/Repositories/ITransitionPlanRepository.cs`
- `CarePath.Domain/Interfaces/Repositories/ITransitionReminderRepository.cs`

**Key methods**:
- `ITransitionPlanRepository.GetActiveByClientIdAsync(Guid clientId)`
- `ITransitionPlanRepository.GetAllActiveAsync()` — coordinator dashboard query
- `ITransitionReminderRepository.GetOverdueAsync(DateTime asOf)` — escalation evaluator

**Acceptance**: Interfaces compile. All return types use `IReadOnlyList<T>` or `Task<T>`. No implementation code in Domain.

---

### TASK-024 — Write Domain unit tests for Transitions entities
**Estimate**: 2 hours  
**Depends on**: TASK-021  
**Files**:
- `Domain.Tests/Transitions/TransitionPlanTests.cs`
- `Domain.Tests/Transitions/TransitionInstructionTests.cs`
- `Domain.Tests/Transitions/TransitionReminderTests.cs`

**Tests to cover**:
- `TransitionPlan.IsActive` — true when Active and within window; false when past window; false when not Active
- `TransitionPlan.DaysRemaining` — correct days; zero when past window (no negative)
- `TransitionInstruction.IsLowConfidence` — true below 0.75; false at 0.75; false above
- `TransitionReminder.IsOverdue` — true when Scheduled and ScheduledAt in past; false when Sent; false when future

**Acceptance**: `dotnet test Domain.Tests` passes. All edge cases covered.

---

## Phase 2 — Infrastructure Layer (MVP Core — Month 1–2)

### TASK-025 — EF Core DbContext additions
**Estimate**: 1.5 hours  
**Depends on**: TASK-021  
**Files**:
- `CarePath.Infrastructure/Persistence/AppDbContext.cs` (add DbSets)
- `CarePath.Infrastructure/Persistence/Configurations/Transitions/` (6 entity configuration files)

**Configuration notes**:
- `DischargeDocument.RawContent` — configure as `nvarchar(max)`, no index
- `TransitionCheckIn.ResponsesJson` — configure as `nvarchar(max)`
- `TransitionInstruction.ConfidenceScore` — `decimal(5, 4)` precision
- Apply global `IsDeleted == false` query filter on all Transitions entities
- `TransitionPlan.TransitionWindowEnd` — never null; always computed as `DischargeDate.AddDays(30)` in Application layer before save

**Acceptance**: `dotnet ef migrations add AddTransitions` runs without error. Migration SQL reviewed for correctness.

---

### TASK-026 — Implement Transitions repositories
**Estimate**: 2 hours  
**Depends on**: TASK-023, TASK-025  
**Files**:
- `CarePath.Infrastructure/Repositories/Transitions/DischargeDocumentRepository.cs`
- `CarePath.Infrastructure/Repositories/Transitions/TransitionPlanRepository.cs`
- `CarePath.Infrastructure/Repositories/Transitions/TransitionReminderRepository.cs`

**Notes**: `GetAllActiveAsync()` must filter `Status == Active AND TransitionWindowEnd >= DateTime.UtcNow`. Add paging via `GetPagedAsync` pattern from the Infrastructure spec.

---

### TASK-027 — Define Application service interfaces for extraction and delivery
**Estimate**: 1 hour  
**Depends on**: TASK-021  
**Files**:
- `CarePath.Application/Transitions/Interfaces/IDischargeExtractionService.cs`
- `CarePath.Application/Transitions/Interfaces/IReminderDeliveryService.cs`
- `CarePath.Application/Transitions/DTOs/ExtractedInstructionDto.cs`

**Rules**: Interfaces live in Application. No Twilio SDK references in Application. No AI SDK references in Application.

---

### TASK-028 — Implement TwilioReminderService
**Estimate**: 2 hours  
**Depends on**: TASK-027  
**Files**:
- `CarePath.Infrastructure/Transitions/Services/TwilioReminderService.cs`

**Notes**: Wrap Twilio REST client. Implement `IReminderDeliveryService`. Log delivery success/failure — never log the message content (it may contain PHI). Store Twilio credentials in `appsettings` / user secrets, not hardcoded.

---

## Phase 3 — Application Layer Commands & Queries (MVP Core — Month 2)

### TASK-029 — UploadDischargeDocumentCommand
**Estimate**: 2 hours  
**Depends on**: TASK-026, TASK-027  
**Files**:
- `CarePath.Application/Transitions/Commands/UploadDischargeDocumentCommand.cs`
- `CarePath.Application/Transitions/Validators/UploadDischargeDocumentValidator.cs`

**Behaviour**: Creates `DischargeDocument` with `Status = Pending`. Enqueues extraction background job. Returns `202 Accepted` with document ID.

---

### TASK-030 — ActivateTransitionPlanCommand
**Estimate**: 2 hours  
**Depends on**: TASK-026  
**Files**:
- `CarePath.Application/Transitions/Commands/ActivateTransitionPlanCommand.cs`

**Behaviour**: Validates all Instructions are `Approved` or `Rejected` (none `Pending`). Sets `Status = Active`, `ActivatedAt = DateTime.UtcNow`, `VerifiedBy = currentUserId`, `VerifiedAt = DateTime.UtcNow`. Schedules first batch of reminders.

**Guard**: Throw `InvalidOperationException` if any Instruction is still `Pending`. Log the plan ID in the audit trail (not patient name).

---

### TASK-031 — RecordCheckInCommand + escalation evaluation
**Estimate**: 3 hours  
**Depends on**: TASK-026  
**Files**:
- `CarePath.Application/Transitions/Commands/RecordCheckInCommand.cs`
- `CarePath.Application/Transitions/Services/EscalationEvaluatorService.cs`

**Behaviour**: Creates `TransitionCheckIn`. If `ContainsWarningSymptom = true`, immediately creates a `TransitionEscalation` at `EscalationLevel.CoordinatorAlert`. Never log `ResponsesJson`.

---

### TASK-032 — Coordinator dashboard query
**Estimate**: 2 hours  
**Depends on**: TASK-026  
**Files**:
- `CarePath.Application/Transitions/Queries/GetActiveTransitionPlansQuery.cs`
- `CarePath.Application/Transitions/DTOs/TransitionPlanSummaryDto.cs`

**DTO fields**: ClientId, ClientName, RiskLevel, DaysRemaining, MissedReminderCount, PendingEscalationCount, LastCheckInDate.

---

## Phase 4 — WebApi Layer (MVP Core — Month 2–3)

### TASK-033 — TransitionsController
**Estimate**: 2 hours  
**Depends on**: TASK-029 through TASK-032  
**Files**:
- `CarePath.WebApi/Controllers/TransitionsController.cs`

**Rules**: Every endpoint has `[Authorize(Roles = "...")]`. No PHI in route parameters without authorization check. Return `202 Accepted` for async document upload. Full endpoint list in design spec.

---

## Phase 5 — Next (Month 4–6)

### TASK-034 — FHIR R4 import integration
- Implement `FhirDischargeExtractionService` in Infrastructure
- Parses `DocumentReference` + `MedicationRequest` + `Appointment` FHIR resources
- Maps to `ExtractedInstructionDto` for the same Application pipeline

### TASK-035 — Outcome reporting
- `TransitionOutcomeReport` DTO
- Export as PDF or CSV for hospital B2B contracts
- Include: adherence rate, missed reminders, escalation count, check-in adherence, 30-day outcome

### TASK-036 — Multilingual patient instructions
- Apply AI translation to `TransitionInstruction.InstructionText` before delivery
- Store translated text per-language in `TransitionInstructionTranslation` child entity

### TASK-037 — DischargeExtractionService (AI implementation)
- Wrap Azure OpenAI GPT-4o or OpenAI API
- Structured output → `ExtractedInstructionDto[]`
- Include confidence scoring in the prompt schema
