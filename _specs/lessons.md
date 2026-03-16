# Lessons Learned — CarePath Health

This file captures recurring mistakes, corrections, and hard-won patterns discovered during development. Claude reads this at the start of every session and updates it after any user correction.

**Format:** When a mistake is corrected, add an entry immediately. Group by theme. Remove entries that are no longer relevant.

---

## Domain Layer

- **UserRole values**: Design spec defines: Admin, Coordinator, Caregiver, Client, FacilityManager. The tasks spec initially had SuperAdmin instead of Admin — the design spec takes precedence when there's a conflict.
- **Entity file paths**: Use subfolder structure from design spec: `Entities/Common/`, `Entities/Identity/`, `Entities/Scheduling/`, `Entities/Billing/` — not flat `Entities/`.

## EF Core / Infrastructure

- **PHI cascade deletes**: ALL 6 PHI entities (Client, CarePlan, Shift, VisitNote, VisitPhoto, CaregiverCertification) must use `DeleteBehavior.Restrict` — no exceptions. The subagent initially set CaregiverCertification and VisitPhoto to Cascade because they're "dependent" entities, but HIPAA overrides that logic.
- **Folder structure**: Interceptors and Converters live at `Persistence/Interceptors/` and `Persistence/Converters/` — NOT inside `Persistence/Configurations/`. Configurations folder is for entity type configs only (Identity/, Scheduling/, Billing/ subdirectories).
- **Property names must match CP-01**: When writing Infrastructure configurations, always cross-reference the approved CP-01 design spec for exact property names. Common mismatches caught: `IssuingBody` vs `IssuingAuthority`, `TransactionId` vs `ReferenceNumber`, `StartLatitude` vs `CheckInLatitude`.
- **GPS fields are `double?`**: CP-01 defines GPS coordinates as `double?`, not `decimal(10,7)`. EF Core maps `double` to SQL Server `float` by default — no explicit precision config needed.
- **Scope boundaries**: When a feature is listed in both in-scope and out-of-scope sections, it creates implementation confusion. Resolve immediately. If implementation code exists in the design spec, it's in-scope.

## Testing

*(No entries yet)*

## HIPAA / Compliance

*(No entries yet)*

## Spec Workflow

- **Design spec is source of truth**: When tasks spec or CLAUDE.md conflicts with the approved design spec, the design spec wins. Update the stale document, not the design spec.
- **User corrections are authoritative**: When the user edits a spec directly (e.g., changing file paths, correcting role names), those edits override any prior assumptions. Check the system-reminder for modifications.
