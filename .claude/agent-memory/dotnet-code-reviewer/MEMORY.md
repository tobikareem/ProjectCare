# CarePath Health — Code Reviewer Memory

## Project Identity
- Solution: `CarePath.sln` at repo root `c:\Users\toboi\ProjectCare\`
- Stack: .NET 9 / C# 13, Clean Architecture, EF Core 9 (future), SQL Server, xUnit + FluentAssertions + Moq

## Architecture Layers (inner → outer)
- **Domain** (`Domain/`) — zero dependencies; RootNamespace=CarePath.Domain
- **Application** (planned) — depends on Domain only
- **Infrastructure** (planned) — depends on Application & Domain
- **WebApi** (`WebApi/`) — depends on all inner layers
- **Tests**: `Domain.Tests/` nested under `tests` solution folder; `src` solution folder holds production projects

## Key Conventions (enforced in reviews)
- Guid PKs (never int auto-increment)
- DateTime.UtcNow only (never DateTime.Now)
- Soft deletes via IsDeleted (never hard delete)
- All entities inherit BaseEntity (Id, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, IsDeleted)
- Nullable reference types enabled on all projects
- FluentValidation for input validation (no data annotations on DTOs)
- Repository interfaces defined in Application, implemented in Infrastructure

## CP-01 Domain Entities Spec
- 12 core entities: BaseEntity, User, Caregiver, CaregiverCertification, Client, CarePlan, Shift, VisitNote, VisitPhoto, Invoice, InvoiceLineItem, Payment
- 8 enumerations (as shipped): UserRole, EmploymentType, CertificationType, ServiceType, ShiftStatus, InvoiceStatus, PaymentMethod, PaymentStatus
- Repository interfaces: IRepository<T>, IUnitOfWork
- Full XML documentation required on all public members (per requirements spec Section 4.4, 5.1)
- Design spec uses `Enums/` as folder name; actual folder is `Enumerations/` — design spec Section 1.2 needs updating
- Identity integration deferred to future phase; Pattern A (separate tables) recommended in design spec
- PHI fields: Client.DateOfBirth, MedicalConditions, Allergies, InsuranceInfo; CarePlan fields; VisitNote fields

## Package Versions (as of 2026-02-16)
- FluentAssertions 7.0.0 is used; 8.8.0 is latest (FA 8.x has major breaking changes — commercial license required for some use cases; 7.x is acceptable for now but track)
- Moq 4.20.72 used (NU1901 vulnerability resolved)
- xunit 2.9.2 used; 2.9.3 is latest (minor patch)
- xunit.runner.visualstudio 2.8.2 used; 3.1.5 is latest (major version jump — xunit v3 ecosystem)
- Microsoft.NET.Test.Sdk 17.12.0 used; 18.0.1 is latest
- coverlet.collector 6.0.2 used; 8.0.0 is latest

## Solution Structure (CarePath.sln)
- `src` solution folder: Domain, WebApi
- `tests` solution folder: Domain.Tests
- Domain project type GUID: {FAE04EC0-...} (C# class library)

## CP-01 Phase 1 Status (TASK-001 to TASK-003) — COMPLETE
- Build passes 0 warnings / 0 errors
- `Domain.csproj` has GenerateDocumentationFile, TreatWarningsAsErrors, WarningsNotAsErrors=CS1591
- `Directory.Build.props` at solution root (TargetFramework=net9.0, ImplicitUsings, Nullable for all projects)
- `Directory.Packages.props` — Central Package Management (CPM) enabled

## CP-01 Phase 2 Status (TASK-004 to TASK-006) — COMPLETE (reviewed 2026-02-16)
- Build passes 0 warnings / 0 errors
- BaseEntity: `Id` and `CreatedAt` correctly use `init` (improvement over design spec which showed `set`)
- BaseEntity: `UpdatedAt`, `CreatedBy`, `UpdatedBy`, `IsDeleted` correctly use `set`
- BaseEntity: No explicit `IsDeleted = false` default — removed (CLR default applies; clean)
- Enumerations: 8 files, one enum per file in `Domain/Enumerations/` — correct
- UserRole: Admin/Coordinator/Caregiver/Client/FacilityManager — matches design spec
- PaymentStatus added (Pending/Successful/Failed/Refunded) — resolves bool IsSuccessful spec mismatch
- TASKS-005/006 success criteria updated in tasks spec to match delivered code
- All enum members start from 1 (not 0) — correct defensive convention

## BaseEntity Design Decisions (confirmed as shipped)
- `Id { get; init; }` — LOCKED, correct
- `CreatedAt { get; init; }` — LOCKED, correct
- `DateTime` used (not DateTimeOffset) — UNRESOLVED; must decide before EF Core Infrastructure phase
- `IsDeleted { get; set; }` no explicit default — clean, correct
- Namespace: `CarePath.Domain.Entities.Common` — future entity files will need `using CarePath.Domain.Entities.Common;`

## CP-01 Phase 3 Status (TASK-007+) — RE-REVIEWED 2026-02-16 (second pass)

### Confirmed Fixed in Phase 3 Re-Review
- `Client.Age` — FIXED: uses `today.AddYears(-age)` pattern; leap-year safe
- `Shift.BillableHours` — FIXED: `totalMinutes <= 0` guard present; no negative values
- `CaregiverCertification.IsExpiringSoon` — FIXED: `!IsExpired` guard present; uses `ExpirationAlertDays` const
- `Caregiver.HireDate` — FIXED: no default value, comment says set by Application layer
- `Invoice.AmountPaid` — FIXED: uses `PaymentStatus.Settled` (not bool IsSuccessful)

### Phase 3 Remaining Critical (must fix before EF Core phase)
- `CarePlan` — wrong namespace `CarePath.Domain.Entities.Identity`; must be `CarePath.Domain.Entities.Clinical`
- XML documentation MISSING on 10 of 11 entities and all members (requirements spec mandates 100%)
- `VisitNote.CaregiverSignature` / `ClientOrFamilySignature` — must be renamed to `*Url`; blob storage URLs, NOT base64 payloads; nvarchar(max) risk
- `Domain.csproj` `WarningsNotAsErrors=CS1591` must STAY until XML docs are complete (note was premature)

### Phase 3 Remaining Improvements
- `Invoice.RecalculateStatus()` method MISSING — `InvoiceStatus.PartiallyPaid` is orphaned (no code sets it)
- `Caregiver.TotalShiftsCompleted` / `NoShowCount` — `public set`; need `private set` + `RecordCompletedShift()` / `RecordNoShow()` domain methods
- `VisitNote.VisitDateTime` and `Invoice.InvoiceDate` — remove `= DateTime.UtcNow` default; Application layer must supply
- `Shift.GrossMargin` — per-hour delta, not total; rename to `HourlyGrossMargin` or change to `(BillRate-PayRate)*BillableHours`
- `User.State` — non-nullable in submitted code, nullable in design spec; align to `string?`
- `InvoiceLineItem.Hours` — rename to `BillableHours` for clarity

### Entity Folder/Namespace Map (confirmed as shipped)
- `Domain/Entities/Common/` — `CarePath.Domain.Entities.Common` (BaseEntity)
- `Domain/Entities/Identity/` — `CarePath.Domain.Entities.Identity` (User, Caregiver, CaregiverCertification, Client, CarePlan — CarePlan should move)
- `Domain/Entities/Scheduling/` — `CarePath.Domain.Entities.Scheduling` (Shift, VisitNote, VisitPhoto)
- `Domain/Entities/Billing/` — `CarePath.Domain.Entities.Billing` (Invoice, InvoiceLineItem, Payment)
- Target: `Domain/Entities/Clinical/` — `CarePath.Domain.Entities.Clinical` (CarePlan, after fix)

### EF Core Infrastructure Flags (carry forward)
- `User.Email` — needs `HasIndex(u => u.Email).IsUnique()` in EF Core config
- `DateTime` vs `DateTimeOffset` — still UNRESOLVED; `DateTime` requires ValueConverter to preserve UTC Kind in SQL Server
- All PHI fields (Client.MedicalConditions, Allergies, CarePlan.Goals/Interventions/Notes, VisitNote.ClientCondition/Concerns/Medications) need encryption at rest
- `VisitNote.CaregiverSignature` / `ClientOrFamilySignature` — store blob URLs, NOT Base64; confirm in EF Core column type (nvarchar(500) max for URL)

## CP-01 Phase 4 Status (TASK-019, TASK-020) — COMPLETE (re-reviewed 2026-02-16, second pass)

### All Issues From First Phase 4 Review — RESOLVED
- `IUnitOfWork : IDisposable, IAsyncDisposable` — FIXED (IAsyncDisposable added)
- `IReadOnlyList<T>` from `GetAllAsync` / `FindAsync` — FIXED (was IEnumerable<T>)
- Tasks spec TASK-019/020 stale paths — FIXED (updated to `Domain/Interfaces/Repositories/`)
- `Identity/CarePlan.cs` tombstone file — DELETED (clean)

### Final Known Minor Items (carry forward as suggestions only)
- `IUnitOfWork` `<remarks>` disposal note (line 25) references only `IDisposable`; does not mention `IAsyncDisposable` — cosmetic doc gap only
- Design spec Section 4.1 shows `where T : class` and `IEnumerable<T>`; shipped code is strictly better; update design spec to match implementation

### Phase 4 Carry-Forward to Infrastructure
- Implementation must register `IUnitOfWork` as scoped in DI (EF Core DbContext is scoped)
- `BeginTransactionAsync` / `CommitTransactionAsync` / `RollbackTransactionAsync` return `Task` (not `Task<IDbContextTransaction>`) — correct for interface abstraction; document that nested transactions are not supported
- `SaveChangesAsync` returning `int` — standard EF Core pattern; correct
- Global query filter `IsDeleted == false` must be applied in EF Core `OnModelCreating` — documented in IRepository XML doc
- `AddGetPagedAsync` overload should be added in concrete repository before production use of `GetAllAsync` on large tables (Shift, VisitNote)

## Open Issues (carry forward beyond Phase 3)
- `PaymentMethod`: `Insurance=5` conflates private insurance and Medicaid — RESOLVED: Medicaid=6 added; EnumerationsTests confirms both exist
- `CertificationType` missing Maryland-specific: `GNA=9` (Geriatric Nursing Assistant), `CRMA=10` — RESOLVED: both added; CertificationType now has 10 members; tests confirm
- `Dementia`/`Alzheimers` overlap — RESOLVED: kept as two distinct enum members; documented in CaregiverCertification XML remarks
- `DateTime` vs `DateTimeOffset` — still UNRESOLVED; must decide before EF Core Infrastructure layer
- Design spec Section 1.2 folder name (`Enums/`) must be corrected to `Enumerations/`
- `Domain.Tests.csproj` — RESOLVED: `<AssemblyName>`, `<RootNamespace>`, `<TreatWarningsAsErrors>true` all present; `coverlet.collector` has `PrivateAssets="all"` — RESOLVED
- `WebApi.csproj` missing `<RootNamespace>CarePath.WebApi</RootNamespace>` and `<AssemblyName>CarePath.WebApi</AssemblyName>` — STILL OPEN
- Phase 3 issues resolved in Domain entity code: `Invoice.RecalculateStatus()` ADDED; `Caregiver.TotalShiftsCompleted`/`NoShowCount` have `private set` + domain methods; `VisitNote.*SignatureUrl` renamed correctly; `CarePlan` in `Clinical` namespace; `InvoiceLineItem.BillableHours` renamed correctly; `User.State` is `string?` — all confirmed RESOLVED by reading source files 2026-02-16

## CP-01 Phase 5 Test Suite (Domain.Tests) — REVIEWED 2026-02-16
- 184 tests, 18 files: Entities/, Business/, Enumerations/, Integration/
- `Domain.Tests.csproj`: RootNamespace, AssemblyName, TreatWarningsAsErrors all present; coverlet PrivateAssets="all" — CLEAN
- `User.FullName` now uses `.Trim()` — single source change; tests cover empty-last-name case; missing: empty-first-name test
- `Payment.PaymentDate = DateTime.UtcNow` default — test uses before/after bracket pattern; same pattern used for `BaseEntity.CreatedAt` and `VisitPhoto.TakenAt`; correctly validated
- `IsExpiringSoon` boundary: test deliberately uses 31 days (not 30) to avoid timing race; comment explains why; correct
- `InvoiceTests.RecalculateStatus_DoesNotChangeStatus_WhenNoPaymentsMade` — COVERAGE GAP: initial Status is Sent; AmountPaid=0; Balance=280 > 0; method does nothing because `AmountPaid > 0` is false. Test passes but the label "DoesNotChangeStatus" is ambiguous — the method genuinely skips. Add a separate no-op test when Status=Draft to distinguish from the Sent scenario.
- `MarginCalculationTests`: all math independently verified; exact decimal arithmetic; no floating-point drift
- `EnumerationsTests`: uses `Enum.IsDefined()` pattern (verbose but clear); member-count tests will catch accidental additions — good regression safety
