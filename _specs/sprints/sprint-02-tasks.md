# Sprint 2 Tasks - Infrastructure Foundation

Date: 2026-06-27
Author: CarePath Health
Status: Complete
Sprint: Sprint 2
Primary spec: CP-02 Infrastructure / EF Core

Related specs:
- `_specs/sprints/sprint-02-requirements.md`
- `_specs/sprints/sprint-02-design.md`
- `_specs/03-tasks/cp-02-infrastructure-ef-core.md`

## Execution Rule

Sprint 2 tasks implement CP-02 Infrastructure only. Do not add Application services, API endpoints, UI, Twilio, AI/OCR, or file storage. If implementation discovers stale CP-02 details, update the CP-02 spec before coding past the conflict.

## Phase 1 - Project Setup

### S2-TASK-001 - Create Infrastructure project

- Layer: Infrastructure
- Maps to CP task: TASK-040
- Dependencies: Sprint 1 complete, CP-01 complete
- Estimate: 1.5 hours
- Priority: Critical
- Files:
  - CREATE `Infrastructure/Infrastructure.csproj`
  - CREATE `Infrastructure/Persistence/`
  - CREATE `Infrastructure/Persistence/Configurations/`
  - CREATE `Infrastructure/Persistence/Interceptors/`
  - CREATE `Infrastructure/Persistence/Converters/`
  - CREATE `Infrastructure/Persistence/Repositories/`
  - CREATE `Infrastructure/Identity/`
- Success criteria:
  - Project targets .NET 9.
  - Project references Domain only.
  - Build passes with zero warnings.

### S2-TASK-002 - Create Infrastructure.Tests project

- Layer: Infrastructure.Tests
- Maps to CP task: TASK-042
- Dependencies: S2-TASK-001
- Estimate: 0.5 hours
- Priority: Critical
- Files:
  - CREATE `Infrastructure.Tests/Infrastructure.Tests.csproj`
  - CREATE `Infrastructure.Tests/Persistence/`
  - CREATE `Infrastructure.Tests/Converters/`
  - CREATE `Infrastructure.Tests/Interceptors/`
  - CREATE `Infrastructure.Tests/Repositories/`
- Success criteria:
  - Test project references Infrastructure and Domain.
  - Test packages include xUnit, FluentAssertions, Moq, EF Core InMemory, and optional Testcontainers.

### S2-TASK-003 - Add NuGet dependencies

- Layer: Infrastructure
- Maps to CP task: TASK-041
- Dependencies: S2-TASK-001
- Estimate: 0.5 hours
- Priority: Critical
- Success criteria:
  - EF Core 9 SQL Server, EF Core tools/design, and ASP.NET Core Identity EF packages are installed.
  - Package versions are compatible with .NET 9.

## Phase 2 - Core Persistence Services

### S2-TASK-004 - Create UTC DateTime converters

- Layer: Infrastructure
- Maps to CP task: TASK-043
- Dependencies: S2-TASK-003
- Estimate: 1.5 hours
- Priority: Critical
- Files:
  - CREATE `Infrastructure/Persistence/Converters/UtcDateTimeConverter.cs`
- Success criteria:
  - Supports `DateTime` and `DateTime?`.
  - Restores `DateTimeKind.Utc` on read.
  - Tests cover null, unspecified, local, and UTC values.

### S2-TASK-005 - Create AuditableEntityInterceptor

- Layer: Infrastructure
- Maps to CP task: TASK-044
- Dependencies: S2-TASK-003
- Estimate: 2 hours
- Priority: Critical
- Files:
  - CREATE `Infrastructure/Persistence/Interceptors/AuditableEntityInterceptor.cs`
- Success criteria:
  - Sets CreatedAt/CreatedBy for added entities.
  - Sets UpdatedAt/UpdatedBy for modified entities.
  - Handles missing user context.
  - Does not log PHI values.

### S2-TASK-006 - Create ApplicationUser identity schema foundation

- Layer: Infrastructure
- Maps to CP task: TASK-046
- Dependencies: S2-TASK-003
- Estimate: 1.5 hours
- Priority: Critical
- Files:
  - CREATE `Infrastructure/Identity/ApplicationUser.cs`
  - CREATE `Infrastructure/Persistence/Configurations/Identity/ApplicationUserConfiguration.cs`
- Success criteria:
  - Inherits `IdentityUser<Guid>`.
  - Links to Domain `User` with `DomainUserId`.
  - Does not implement login/JWT behavior.

## Phase 3 - DbContext and Entity Configurations

### S2-TASK-007 - Create CarePathDbContext

- Layer: Infrastructure
- Maps to CP task: TASK-045
- Dependencies: S2-TASK-004, S2-TASK-005, S2-TASK-006
- Estimate: 2 hours
- Priority: Critical
- Files:
  - CREATE `Infrastructure/Persistence/CarePathDbContext.cs`
- Success criteria:
  - Inherits IdentityDbContext with Guid keys.
  - Adds DbSets for CP-01 entities only; CP-03 Transitions DbSets are deferred until explicit CP-03 configurations exist.
  - Applies configurations from assembly.
  - Registers audit interceptor via options.

### S2-TASK-008 - Configure identity/person entities

- Layer: Infrastructure
- Maps to CP tasks: TASK-047 through TASK-050, TASK-058
- Dependencies: S2-TASK-007
- Estimate: 4 hours
- Priority: Critical
- Files:
  - CREATE `Infrastructure/Persistence/Configurations/Identity/UserConfiguration.cs`
  - CREATE `Infrastructure/Persistence/Configurations/Identity/CaregiverConfiguration.cs`
  - CREATE `Infrastructure/Persistence/Configurations/Identity/CaregiverCertificationConfiguration.cs`
  - CREATE `Infrastructure/Persistence/Configurations/Identity/ClientConfiguration.cs`
  - CREATE `Infrastructure/Persistence/Configurations/Identity/ApplicationUserConfiguration.cs`
- Success criteria:
  - Fluent API only.
  - String lengths, indexes, UTC dates, and soft-delete filters configured.
  - PHI delete behavior reviewed.

### S2-TASK-009 - Configure clinical and scheduling entities

- Layer: Infrastructure
- Maps to CP tasks: TASK-051 through TASK-054
- Dependencies: S2-TASK-007, S2-TASK-008
- Estimate: 5 hours
- Priority: Critical
- Files:
  - CREATE `Infrastructure/Persistence/Configurations/Clinical/CarePlanConfiguration.cs`
  - CREATE `Infrastructure/Persistence/Configurations/Scheduling/ShiftConfiguration.cs`
  - CREATE `Infrastructure/Persistence/Configurations/Scheduling/VisitNoteConfiguration.cs`
  - CREATE `Infrastructure/Persistence/Configurations/Scheduling/VisitPhotoConfiguration.cs`
- Success criteria:
  - CarePlan uses Clinical folder convention.
  - Shift computed properties are ignored, including total `GrossMargin`.
  - VisitNote/VisitPhoto PHI relationships use retention-safe delete behavior.
  - High-volume query indexes exist for Shift and VisitNote.

### S2-TASK-010 - Configure billing entities

- Layer: Infrastructure
- Maps to CP tasks: TASK-055 through TASK-057
- Dependencies: S2-TASK-007
- Estimate: 3 hours
- Priority: High
- Files:
  - CREATE `Infrastructure/Persistence/Configurations/Billing/InvoiceConfiguration.cs`
  - CREATE `Infrastructure/Persistence/Configurations/Billing/InvoiceLineItemConfiguration.cs`
  - CREATE `Infrastructure/Persistence/Configurations/Billing/PaymentConfiguration.cs`
- Success criteria:
  - Monetary values use decimal precision.
  - Invoice number/reference indexes configured.
  - Financial retention behavior reviewed.

### S2-TASK-011 - Configure Transitions entities

- Layer: Infrastructure
- Maps to CP-03 deferred backend foundation
- Dependencies: CP-03 backend scope approval after CP-02 entity configurations are complete
- Estimate: Deferred
- Priority: Deferred
- Files:
  - DEFER `Infrastructure/Persistence/Configurations/Transitions/*.cs`
- Success criteria:
  - Not implemented in CP-02 Phase 1.
  - CP-03 Transitions entities are not mapped into `CarePathDbContext` until their explicit configurations exist.
  - RawContent, SourceText, and ResponsesJson are not indexed when this deferred work is later implemented.
  - Clinical relationship delete behavior is Restrict.
  - No reminder delivery, AI, OCR, or Twilio implementation is added.

## Phase 4 - Repository and UnitOfWork

### S2-TASK-012 - Implement Repository<T>

- Layer: Infrastructure
- Maps to CP task: TASK-059
- Dependencies: S2-TASK-007 through S2-TASK-010
- Estimate: 2.5 hours
- Priority: Critical
- Files:
  - CREATE `Infrastructure/Persistence/Repositories/Repository.cs`
- Success criteria:
  - Implements Domain `IRepository<T>`.
  - Uses `where T : BaseEntity`.
  - Collection returns are `IReadOnlyList<T>`.
  - DeleteAsync soft-deletes only.
  - `GetPagedAsync` returns items and total count.

### S2-TASK-013 - Implement UnitOfWork

- Layer: Infrastructure
- Maps to CP task: TASK-060
- Dependencies: S2-TASK-012
- Estimate: 2 hours
- Priority: Critical
- Files:
  - CREATE `Infrastructure/Persistence/Repositories/UnitOfWork.cs`
- Success criteria:
  - Exposes repositories for CP-01 entities. Transitions repositories are deferred to CP-03 backend scope.
  - Owns SaveChangesAsync.
  - Supports transactions.
  - Implements sync and async disposal.

### S2-TASK-014 - Update Domain repository contracts for paging

- Layer: Domain
- Maps to CP task: TASK-061
- Dependencies: S2-TASK-012
- Estimate: 1 hour
- Priority: High
- Files:
  - MODIFY `Domain/Interfaces/Repositories/IRepository.cs`
- Success criteria:
  - Adds `GetPagedAsync` without breaking existing Domain tests.
  - Return type uses `IReadOnlyList<T>`.
  - No Domain reference to EF Core.

## Phase 5 - Dependency Injection and WebApi Integration

### S2-TASK-015 - Add Infrastructure service registration

- Layer: Infrastructure
- Maps to CP task: TASK-062
- Dependencies: S2-TASK-013
- Estimate: 1.5 hours
- Priority: Critical
- Files:
  - CREATE `Infrastructure/DependencyInjection.cs`
- Success criteria:
  - Registers DbContext, Identity schema foundation, interceptor, repository, UnitOfWork.
  - Uses SQL Server retry configuration.
  - Does not enable sensitive data logging outside Development.

### S2-TASK-016 - Register Infrastructure from WebApi

- Layer: WebApi
- Maps to CP tasks: TASK-063, TASK-064
- Dependencies: S2-TASK-015
- Estimate: 1 hour
- Priority: Critical
- Files:
  - MODIFY `WebApi/Program.cs`
  - MODIFY `WebApi/appsettings.json`
  - MODIFY `WebApi/appsettings.Development.json`
- Success criteria:
  - `AddInfrastructure(configuration)` is called.
  - Connection string includes encryption settings.
  - No production secrets are committed.

## Phase 6 - Migration and Seed Data

### S2-TASK-017 - Generate InitialCreate migration

- Layer: Infrastructure
- Maps to CP task: TASK-065
- Dependencies: S2-TASK-016
- Estimate: 1 hour
- Priority: Critical
- Files:
  - CREATE `Infrastructure/Persistence/Migrations/*_InitialCreate.cs`
- Success criteria:
  - Migration generates successfully.
  - Migration compiles.

### S2-TASK-018 - Review migration for HIPAA safety

- Layer: Infrastructure
- Maps to CP task: TASK-066
- Dependencies: S2-TASK-017
- Estimate: 1.5 hours
- Priority: Critical
- Success criteria:
  - No PHI cascade delete surprises.
  - PKs are Guid.
  - Indexes and string lengths are correct.
  - Raw PHI text fields are not indexed.

### S2-TASK-019 - Apply migration locally

- Layer: Infrastructure/WebApi
- Maps to CP task: TASK-067
- Dependencies: S2-TASK-018
- Estimate: 1 hour
- Priority: High
- Success criteria:
  - `dotnet ef database update --startup-project WebApi` succeeds locally.
  - Database schema is inspectable.

### S2-TASK-020 - Add synthetic seed strategy

- Layer: Infrastructure
- Maps to CP tasks: TASK-068, TASK-069
- Dependencies: S2-TASK-019
- Estimate: 3 hours
- Priority: High
- Files:
  - CREATE `Infrastructure/Persistence/CarePathDbContextSeed.cs`
- Success criteria:
  - Idempotent seed logic.
  - Development-only by default.
  - Seed data is obviously synthetic.
  - No real or production-like PHI.

## Phase 7 - Tests and Verification

### S2-TASK-021 - Add converter and interceptor tests

- Layer: Infrastructure.Tests
- Maps to CP tasks: TASK-074, TASK-075
- Dependencies: S2-TASK-004, S2-TASK-005
- Estimate: 2.5 hours
- Priority: High
- Files:
  - CREATE `Infrastructure.Tests/Converters/UtcDateTimeConverterTests.cs`
  - CREATE `Infrastructure.Tests/Interceptors/AuditableEntityInterceptorTests.cs`
- Success criteria:
  - UTC behavior tested.
  - Audit fields tested.
  - Null/system actor context tested.

### S2-TASK-022 - Add DbContext and entity configuration tests

- Layer: Infrastructure.Tests
- Maps to CP tasks: TASK-070, TASK-073
- Dependencies: S2-TASK-007 through S2-TASK-010
- Estimate: 4 hours
- Priority: Critical
- Files:
  - CREATE `Infrastructure.Tests/Persistence/CarePathDbContextTests.cs`
  - CREATE `Infrastructure.Tests/Persistence/EntityConfigurationTests.cs`
- Success criteria:
  - Model builds.
  - Configurations apply.
  - Soft-delete filters exist.
  - PHI delete behaviors are verified.

### S2-TASK-023 - Add repository and UnitOfWork tests

- Layer: Infrastructure.Tests
- Maps to CP tasks: TASK-071, TASK-072, TASK-076
- Dependencies: S2-TASK-012, S2-TASK-013
- Estimate: 4 hours
- Priority: Critical
- Files:
  - CREATE `Infrastructure.Tests/Repositories/RepositoryTests.cs`
  - CREATE `Infrastructure.Tests/Repositories/UnitOfWorkTests.cs`
  - CREATE `Infrastructure.Tests/Persistence/FullCrudIntegrationTests.cs`
- Success criteria:
  - CRUD, soft delete, paging, transactions, and full lifecycle are tested.

### S2-TASK-024 - Add migration and seed verification tests/checklist

- Layer: Infrastructure.Tests / Documentation
- Maps to CP tasks: TASK-066, TASK-069
- Dependencies: S2-TASK-017 through S2-TASK-020
- Estimate: 2 hours
- Priority: High
- Files:
  - CREATE `Infrastructure.Tests/Persistence/MigrationShapeTests.cs` or documented migration checklist
  - CREATE `Infrastructure.Tests/Persistence/SeedDataTests.cs`
- Success criteria:
  - Migration shape reviewed.
  - Seed data is idempotent and synthetic-only.

### S2-TASK-025 - Run full verification and reviewer

- Layer: Solution
- Maps to CP tasks: TASK-077, TASK-078
- Dependencies: All Sprint 2 tasks
- Estimate: 1.5 hours
- Priority: Critical
- Success criteria:
  - `dotnet build CarePath.sln` passes with zero warnings.
  - `dotnet test CarePath.sln` passes.
  - dotnet-code-reviewer reports no critical issues.
  - HIPAA spot-check confirms no PHI in logs, seed data, URLs, or migration comments.

## Summary

| Phase | Tasks | Estimate |
|---|---:|---:|
| Project setup | 3 | 2.5h |
| Core persistence services | 3 | 5h |
| DbContext/configurations | 5 | 17h |
| Repository/UnitOfWork | 3 | 5.5h |
| DI/WebApi integration | 2 | 2.5h |
| Migration/seed | 4 | 6.5h |
| Tests/verification | 5 | 14h |
| Total | 25 | 53h |

## Critical Path

S2-TASK-001 -> S2-TASK-003 -> S2-TASK-005 -> S2-TASK-007 -> S2-TASK-009 -> S2-TASK-012 -> S2-TASK-013 -> S2-TASK-015 -> S2-TASK-016 -> S2-TASK-017 -> S2-TASK-018 -> S2-TASK-019 -> S2-TASK-022 -> S2-TASK-023 -> S2-TASK-025

## Progress Tracking

| Task | Status | Notes |
|---|---|---|
| S2-TASK-001 | Complete | Infrastructure project builds and is in solution |
| S2-TASK-002 | Complete | Test project builds and is in solution |
| S2-TASK-003 | Complete | EF Core/SQL Server/Identity/test package versions added |
| S2-TASK-004 | Complete | UTC DateTime and nullable converter implemented and tested |
| S2-TASK-005 | Complete | SaveChanges interceptor implemented and tested |
| S2-TASK-006 | Complete | Identity user and DomainUser relationship config implemented |
| S2-TASK-007 | Complete | CarePathDbContext added with CP-01 DbSets plus UTC/soft-delete conventions; Transitions DbSets intentionally deferred |
| S2-TASK-008 | Complete | Identity/person entity configurations implemented and covered by metadata tests |
| S2-TASK-009 | Complete | Clinical/scheduling configurations implemented with PHI-safe delete behavior |
| S2-TASK-010 | Complete | Billing configurations implemented with precision, indexes, and retention-safe relationships |
| S2-TASK-011 | Deferred | Transitions persistence deferred until CP-03 backend configs exist |
| S2-TASK-012 | Complete | Generic Repository<T> implemented with soft delete and paging |
| S2-TASK-013 | Complete | UnitOfWork implemented with lazy repositories, SaveChanges, disposal, and transaction guards |
| S2-TASK-014 | Complete | IRepository<T> now exposes GetPagedAsync without EF dependencies |
| S2-TASK-015 | Complete | AddInfrastructure registers DbContext, Identity, interceptor, repositories, UnitOfWork, SQL retry, and logging/accessor dependencies |
| S2-TASK-016 | Complete | WebApi references Infrastructure, calls AddInfrastructure, and has encrypted non-secret connection strings |
| S2-TASK-017 | Complete | InitialCreate migration generated and compiles |
| S2-TASK-018 | Complete | Migration reviewed: no CP-03 tables, PHI FKs restrict deletes, lengths/precision/indexes verified |
| S2-TASK-019 | Complete | InitialCreate applied to local CarePathHealth_Dev database and listed as applied |
| S2-TASK-020 | Complete | Development-only synthetic seed strategy added; password comes from external configuration |
| S2-TASK-021 | Complete | Focused converter and interceptor tests added |
| S2-TASK-022 | Complete | DbContext model, soft-delete filters, delete behaviors, lengths, precision, and computed-property ignores verified |
| S2-TASK-023 | Complete | Repository and UnitOfWork tests cover CRUD, soft delete, paging, lazy reuse, SaveChanges, and transaction guards |
| S2-TASK-024 | Complete | Migration shape and synthetic seed tests added, including idempotency and missing-password coverage |
| S2-TASK-025 | Complete | Final build, tests, migration state, HIPAA spot-check, and dotnet-code-reviewer verification passed |

## Revision History

| Version | Date | Author | Changes |
|---|---|---|---|
| 1.0 | 2026-06-27 | Codex | Initial Sprint 2 task spec. |




