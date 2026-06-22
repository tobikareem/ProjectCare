# Lessons Learned — CarePath Health

This file captures recurring mistakes, corrections, and hard-won patterns discovered during development. Claude reads this at the start of every session and updates it after any user correction.

**Format:** When a mistake is corrected, add an entry immediately. Group by theme. Remove entries that are no longer relevant.

---

## Domain Layer

- **Namespace for enumerations is `CarePath.Domain.Enumerations`** — not `CarePath.Domain.Enums`. This was confirmed when reviewing the actual code vs design spec samples that used the wrong namespace. Always check the actual file before writing a using directive.
- **CarePlan lives in `Entities/Clinical/`** in actual code, not `Entities/Identity/` as the design spec states. The design spec has a stale folder reference. Trust the actual file structure over the design spec when they conflict; update the spec to match.
- **`VisitNote` and `Shift` tables will be large** — never use `GetAllAsync()` on them. Always add date/client/status filters. `GetPagedAsync` is planned in TASK-019a.
- **Transitions entities go in `Entities/Transitions/`** — new subfolder, not Identity or Scheduling. Pattern mirrors the existing folder split.

## EF Core / Infrastructure

- **`TransitionPlan.TransitionWindowEnd`** must be set in the Application layer before save (`DischargeDate.AddDays(30)` in UTC). Never compute it in the entity constructor — that would make it hard to test without mocking DateTime.
- **`TransitionCheckIn.ResponsesJson`** is PHI stored as `nvarchar(max)`. Configure it that way in EF. Never serialize it into a log string.

## Testing

- **Domain unit tests are pure** — no EF Core, no mocks, no database. `TransitionPlan.IsActive` and `DaysRemaining` need edge-case tests: exactly at window boundary, past window (must return 0, not negative), status != Active.

## HIPAA / Compliance

- **Two new fields that must never be logged**: `DischargeDocument.RawContent` (raw OCR/FHIR text) and `TransitionInstruction.SourceText` (original discharge document excerpt). Both are PHI. Add a comment in code on both properties: `// PHI — never log this field`.
- **Reminders must not fire before `TransitionPlan.Status == Active`** — enforce this check in the Application command handler, not only at the API layer. Belt-and-suspenders.

## Spec Workflow

- **CP-02 Transitions specs are in Draft status as of 2026-06-22** — they must be reviewed and moved to Approved before TASK-020 begins. Do not start implementation with Draft specs.
- **When a task spec says TASK-XXX, the XX number continues from the last CP-01 task** — CP-01 ended at TASK-019a. CP-02 starts at TASK-020.

## Architecture / Design Decisions

- **No autonomous escalation** — the system creates `TransitionEscalation` records and surfaces them on the coordinator dashboard. It never autonomously contacts family, recommends urgent care, or calls 911. Coordinator makes all human contact decisions. This is non-negotiable for regulatory reasons.
- **AI extraction interface lives in Application, not Infrastructure** — `IDischargeExtractionService` is defined in `CarePath.Application/Transitions/Interfaces/`. The Infrastructure implementation (wrapping OpenAI/Azure OpenAI) depends on Application, not the other way around. Never break the dependency rule.
- **Twilio credentials go in user secrets / appsettings** — never hardcode. Never log Twilio API keys. Twilio has a HIPAA BAA available — ensure it is signed before processing any PHI via SMS.

## Session History

### Session: 2026-06-22
**What was built**:
- Reviewed CP-01 (Domain layer) — confirmed all 39 tasks complete
- Updated wireframe navigation (`carepath-wireframe.html`) with working page/screen switching
- Scoped CarePath Transitions feature (30-day post-discharge care management)
- Created `CarePath_Transitions_Feature_Presentation_v2.pptx` — 10 slides, full B2B business case, competitor landscape, solution, MVP path
- Created three spec files for CP-02 (requirements, design, tasks)
- Updated CLAUDE.md with full Transitions domain model and new entities

**What's next**:
- Get CP-02 specs approved (review with Tobi)
- Begin TASK-020 (enumerations) when specs reach Approved status
- Application and Infrastructure layers (CP-01 work) are the prerequisite for shipping CP-02
