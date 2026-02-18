# Tasks Breakdown: CP-01 Create Domain Entities

**Date**: 2026-02-16
**Author**: Tobi Kareem
**Project**: CarePath Health
**Status**: Approvedm
**Spec Number**: CP-01
**Related Specs**:
- **Requirements**: [cp-01-create-domain-entities.md](../01-requirements/cp-01-create-domain-entities.md)
- **Design**: [cp-01-create-domain-entities.md](../02-design/cp-01-create-domain-entities.md)

---

## Executive Summary

> **Implement 12 core domain entities with zero external dependencies, following Clean Architecture principles for CarePath Health's dual-service healthcare platform.**

This tasks spec breaks down the domain layer implementation into 39 atomic tasks across 5 phases: Project Setup, Base Infrastructure, Core Entities, Repository Interfaces, and Unit Testing. All tasks focus on the **CarePath.Domain** project with pure .NET 9 code.

**Total Estimated Time**: ~35-40 hours (1-1.5 weeks for one developer)

---

## Task Summary by Phase

| Phase | Tasks | Estimated Time | Dependencies |
|-------|-------|----------------|--------------|
| Phase 1: Project Setup | 3 tasks | 2 hours | None |
| Phase 2: Base Infrastructure | 3 tasks | 3 hours | Phase 1 |
| Phase 3: Core Entities | 12 tasks | 18 hours | Phase 2 |
| Phase 4: Repository Interfaces | 2 tasks | 3 hours | Phase 3 |
| Phase 5: Unit Testing | 19 tasks | 14 hours | Phase 3, 4 |

---

## Phase 1: Project Setup

### TASK-001: Create Domain Project Structure

- **Layer**: CarePath.Domain
- **Dependencies**: None
- **Estimate**: 1 hour
- **Priority**: Critical (blocks all other tasks)
- **Success Criteria**:
  - `Domain/Domain.csproj` exists with correct .NET 9 configuration
  - Folder structure created: `Entities/`, `Enumerations/`, `Interfaces/`
  - Project builds successfully with zero warnings
  - Implicit usings enabled
  - Nullable reference types enabled
- **Files**:
  - CREATE: `Domain/Domain.csproj`
  - CREATE: `Domain/Entities/` (folder)
  - CREATE: `Domain/Enumerations/` (folder)
  - CREATE: `Domain/Interfaces/` (folder)
- **Commands**:
  ```bash
  dotnet new classlib -n Domain -f net9.0
  mkdir -p Domain/Entities Domain/Enumerations Domain/Interfaces
  dotnet sln CarePath.sln add Domain/Domain.csproj
  dotnet build Domain/Domain.csproj
  ```
- **Implementation Notes**:
  - Set `<ImplicitUsings>enable</ImplicitUsings>` in .csproj
  - Set `<Nullable>enable</Nullable>` in .csproj
  - Set `<RootNamespace>CarePath.Domain</RootNamespace>`

---

### TASK-002: Create Domain Tests Project

- **Layer**: Tests
- **Dependencies**: TASK-001
- **Estimate**: 0.5 hours
- **Priority**: Critical
- **Success Criteria**:
  - `Domain.Tests/Domain.Tests.csproj` exists
  - References Domain project
  - Has xUnit, FluentAssertions, Moq packages
  - Test project builds successfully
- **Files**:
  - CREATE: `Domain.Tests/Domain.Tests.csproj`
  - CREATE: `Domain.Tests/Entities/` (folder)
  - CREATE: `Domain.Tests/Enumerations/` (folder)
- **Commands**:
  ```bash
  dotnet new xunit -n Domain.Tests -f net9.0
  dotnet add Domain.Tests/Domain.Tests.csproj reference Domain/Domain.csproj
  dotnet add Domain.Tests package FluentAssertions --version 7.0.0
  dotnet add Domain.Tests package Moq --version 4.20.0
  dotnet sln CarePath.sln add Domain.Tests/Domain.Tests.csproj
  dotnet test Domain.Tests/Domain.Tests.csproj
  ```

---

### TASK-003: Configure Solution and Verify Build

- **Layer**: Solution
- **Dependencies**: TASK-001, TASK-002
- **Estimate**: 0.5 hours
- **Priority**: High
- **Success Criteria**:
  - `CarePath.sln` includes Domain and Domain.Tests projects
  - `dotnet build CarePath.sln` succeeds with zero errors/warnings
  - `dotnet test CarePath.sln` runs (even with no tests yet)
- **Commands**:
  ```bash
  dotnet build CarePath.sln
  dotnet test CarePath.sln
  ```

---

## Phase 2: Base Infrastructure

### TASK-004: Create BaseEntity Abstract Class

- **Layer**: CarePath.Domain
- **Dependencies**: TASK-001
- **Estimate**: 1 hour
- **Priority**: Critical (blocks all entity tasks)
- **Success Criteria**:
  - `BaseEntity.cs` created with all audit properties
  - Uses `Guid` primary key (not auto-increment int)
  - `CreatedAt` defaults to `DateTime.UtcNow`
  - `Id` defaults to `Guid.NewGuid()`
  - Soft delete with `IsDeleted` flag
  - Full XML documentation
- **Files**:
  - CREATE: `Domain/Entities/BaseEntity.cs`
- **Implementation**: See design spec Section 2.1 for complete code

---

### TASK-005: Create Core Enumerations File

- **Layer**: CarePath.Domain
- **Dependencies**: TASK-001
- **Estimate**: 1.5 hours
- **Priority**: Critical (blocks entity tasks)
- **Success Criteria**:
  - All 7 enumerations created in `Enumerations.cs`
  - Each enum has XML documentation explaining business context
  - Enums: EmploymentType, CertificationType, ServiceType, ShiftStatus, InvoiceStatus, PaymentMethod, PaymentStatus
- **Files**:
  - CREATE: `Domain/Enumerations/Enumerations.cs`
- **Implementation**: See design spec Section 3 for complete code

---

### TASK-006: Create UserRole Enum (Separate File)

- **Layer**: CarePath.Domain
- **Dependencies**: TASK-001
- **Estimate**: 0.5 hours
- **Priority**: High
- **Success Criteria**:
  - `UserRole.cs` created in Enumerations folder
  - Roles: SuperAdmin, Admin, CareCoordinator, Caregiver, Client
  - Includes XML comments explaining each role's purpose
- **Files**:
  - CREATE: `Domain/Enumerations/UserRole.cs`
- **Implementation**: See design spec Section 3 for complete code

---

## Phase 3: Core Entities

### TASK-007: Create User Entity
- **Dependencies**: TASK-004, TASK-006
- **Estimate**: 1.5 hours
- **Files**: CREATE `Domain/Entities/User.cs`
- **Implementation**: See design spec Section 2.1

### TASK-008: Create Caregiver Entity
- **Dependencies**: TASK-004, TASK-005, TASK-007
- **Estimate**: 2 hours
- **Files**: CREATE `Domain/Entities/Caregiver.cs`
- **Implementation**: See design spec Section 2.2

### TASK-009: Create Client Entity
- **Dependencies**: TASK-004, TASK-007
- **Estimate**: 1.5 hours
- **Files**: CREATE `Domain/Entities/Client.cs`
- **Implementation**: See design spec Section 2.3

### TASK-010: Create CarePlan Entity
- **Dependencies**: TASK-004, TASK-009
- **Estimate**: 1 hour
- **Files**: CREATE `Domain/Entities/CarePlan.cs`
- **Implementation**: See design spec Section 2.4

### TASK-011: Create Shift Entity
- **Dependencies**: TASK-004, TASK-005, TASK-008, TASK-009
- **Estimate**: 2 hours
- **Priority**: Critical (core business entity with computed margin properties)
- **Files**: CREATE `Domain/Entities/Shift.cs`
- **Implementation**: See design spec Section 2.5 - includes BillableHours, GrossMargin, GrossMarginPercentage

### TASK-012: Create VisitNote Entity
- **Dependencies**: TASK-004, TASK-011
- **Estimate**: 1.5 hours
- **Files**: CREATE `Domain/Entities/VisitNote.cs`
- **Implementation**: See design spec Section 2.6

### TASK-013: Create VisitPhoto Entity
- **Dependencies**: TASK-004, TASK-012
- **Estimate**: 1 hour
- **Files**: CREATE `Domain/Entities/VisitPhoto.cs`
- **Implementation**: See design spec Section 2.7

### TASK-014: Create Invoice Entity
- **Dependencies**: TASK-004, TASK-005, TASK-009
- **Estimate**: 2 hours
- **Priority**: High (core billing entity with computed properties)
- **Files**: CREATE `Domain/Entities/Invoice.cs`
- **Implementation**: See design spec Section 2.8 - includes Subtotal, TotalAmount, AmountPaid, Balance

### TASK-015: Create InvoiceLineItem Entity
- **Dependencies**: TASK-004, TASK-014
- **Estimate**: 1 hour
- **Files**: CREATE `Domain/Entities/InvoiceLineItem.cs`
- **Implementation**: See design spec Section 2.9

### TASK-016: Create Payment Entity
- **Dependencies**: TASK-004, TASK-005, TASK-014
- **Estimate**: 1.5 hours
- **Files**: CREATE `Domain/Entities/Payment.cs`
- **Implementation**: See design spec Section 2.10

### TASK-017: Create CaregiverCertification Entity
- **Dependencies**: TASK-004, TASK-005, TASK-008
- **Estimate**: 1 hour
- **Files**: CREATE `Domain/Entities/CaregiverCertification.cs`
- **Implementation**: See design spec Section 2.11

### TASK-018: Update All Entity Namespaces and Verify Compilation
- **Dependencies**: TASK-007 through TASK-017
- **Estimate**: 0.5 hours
- **Priority**: High
- **Success Criteria**: `dotnet build Domain/Domain.csproj` succeeds with zero errors/warnings

---

## Phase 4: Repository Interfaces

### TASK-019: Create IRepository<T> Generic Interface
- **Dependencies**: TASK-004
- **Estimate**: 1.5 hours
- **Files**: CREATE `Domain/Interfaces/IRepository.cs`
- **Implementation**: See design spec Section 4.1
- **Methods**: GetByIdAsync, GetAllAsync, FindAsync, AddAsync, UpdateAsync, DeleteAsync (soft delete)

### TASK-020: Create IUnitOfWork Interface
- **Dependencies**: TASK-019
- **Estimate**: 1.5 hours
- **Files**: CREATE `Domain/Interfaces/IUnitOfWork.cs`
- **Implementation**: See design spec Section 4.2
- **Properties**: Repository for each entity (IRepository<User>, IRepository<Caregiver>, etc.)
- **Methods**: SaveChangesAsync, BeginTransactionAsync, CommitTransactionAsync, RollbackTransactionAsync

---

## Phase 5: Unit Testing

### TASK-021: Create BaseEntityTests
- **Dependencies**: TASK-004
- **Estimate**: 1 hour
- **Files**: CREATE `Domain.Tests/Entities/BaseEntityTests.cs`
- **Tests**: Default values, audit fields, soft delete
- **Implementation**: See design spec Section 5.1

### TASK-022: Create UserTests
- **Dependencies**: TASK-007
- **Estimate**: 1 hour
- **Files**: CREATE `Domain.Tests/Entities/UserTests.cs`
- **Tests**: FullName computed property, UserRole assignment

### TASK-023: Create CaregiverTests
- **Dependencies**: TASK-008
- **Estimate**: 1 hour
- **Files**: CREATE `Domain.Tests/Entities/CaregiverTests.cs`
- **Tests**: ActiveCertifications computed property, EmploymentType scenarios

### TASK-024: Create ClientTests
- **Dependencies**: TASK-009
- **Estimate**: 1 hour
- **Files**: CREATE `Domain.Tests/Entities/ClientTests.cs`
- **Tests**: Age computed property (various birthdates, before/after birthday edge cases)

### TASK-025: Create ShiftTests (Computed Properties)
- **Dependencies**: TASK-011
- **Estimate**: 2 hours
- **Priority**: Critical (tests core business logic)
- **Files**: CREATE `Domain.Tests/Entities/ShiftTests.cs`
- **Tests**: 
  - BillableHours calculation (with/without breaks)
  - GrossMargin calculation
  - GrossMarginPercentage calculation
  - Edge cases (null times, zero BillRate)
  - Margin targets (40-45% for W2, 25-30% for 1099)
- **Implementation**: See design spec Section 5.1 for example tests

### TASK-026: Create InvoiceTests (Computed Properties)
- **Dependencies**: TASK-014, TASK-015, TASK-016
- **Estimate**: 1.5 hours
- **Files**: CREATE `Domain.Tests/Entities/InvoiceTests.cs`
- **Tests**:
  - Subtotal calculation (sum of line items)
  - TotalAmount with tax and discounts
  - AmountPaid (sum of payments)
  - Balance calculation
  - Multiple line items and partial payments
- **Implementation**: See design spec Section 5.1 for example tests

### TASK-027: Create VisitNoteTests
- **Dependencies**: TASK-012
- **Estimate**: 0.5 hours
- **Files**: CREATE `Domain.Tests/Entities/VisitNoteTests.cs`

### TASK-028: Create CaregiverCertificationTests
- **Dependencies**: TASK-017
- **Estimate**: 1 hour
- **Files**: CREATE `Domain.Tests/Entities/CaregiverCertificationTests.cs`
- **Tests**: IsExpired computed property (future/past dates, edge case: expiration date equals today)

### TASK-029: Create EnumerationsTests
- **Dependencies**: TASK-005, TASK-006
- **Estimate**: 1 hour
- **Files**: CREATE `Domain.Tests/Enumerations/EnumerationsTests.cs`

### TASK-030: Create CarePlanTests
- **Dependencies**: TASK-010
- **Estimate**: 0.5 hours
- **Files**: CREATE `Domain.Tests/Entities/CarePlanTests.cs`

### TASK-031: Create InvoiceLineItemTests
- **Dependencies**: TASK-015
- **Estimate**: 0.5 hours
- **Files**: CREATE `Domain.Tests/Entities/InvoiceLineItemTests.cs`
- **Tests**: Amount computed property (Quantity * UnitPrice)

### TASK-032: Create PaymentTests
- **Dependencies**: TASK-016
- **Estimate**: 0.5 hours
- **Files**: CREATE `Domain.Tests/Entities/PaymentTests.cs`

### TASK-033: Create VisitPhotoTests
- **Dependencies**: TASK-013
- **Estimate**: 0.5 hours
- **Files**: CREATE `Domain.Tests/Entities/VisitPhotoTests.cs`

### TASK-034: Integration Test - User → Caregiver → Shift Navigation
- **Dependencies**: TASK-007, TASK-008, TASK-011
- **Estimate**: 1 hour
- **Files**: CREATE `Domain.Tests/Integration/UserCaregiverShiftNavigationTests.cs`

### TASK-035: Integration Test - Client → CarePlan → Shift Navigation
- **Dependencies**: TASK-009, TASK-010, TASK-011
- **Estimate**: 1 hour
- **Files**: CREATE `Domain.Tests/Integration/ClientCarePlanNavigationTests.cs`

### TASK-036: Integration Test - Invoice → LineItems → Payments
- **Dependencies**: TASK-014, TASK-015, TASK-016
- **Estimate**: 1 hour
- **Files**: CREATE `Domain.Tests/Integration/InvoicePaymentNavigationTests.cs`

### TASK-037: Integration Test - Shift → VisitNote → Photos
- **Dependencies**: TASK-011, TASK-012, TASK-013
- **Estimate**: 1 hour
- **Files**: CREATE `Domain.Tests/Integration/ShiftVisitNoteNavigationTests.cs`

### TASK-038: Margin Calculation Test Suite (Multiple Shifts)
- **Dependencies**: TASK-011
- **Estimate**: 1 hour
- **Files**: CREATE `Domain.Tests/Business/MarginCalculationTests.cs`
- **Tests**: Test margin calculations across multiple shift scenarios (W2 vs 1099, various rates, target validation)

### TASK-039: Run Full Test Suite and Generate Coverage Report
- **Dependencies**: TASK-021 through TASK-038
- **Estimate**: 0.5 hours
- **Commands**:
  ```bash
  dotnet test CarePath.sln
  dotnet test --collect:"XPlat Code Coverage"
  ```
- **Success Criteria**: All tests pass, coverage >80%

---

## Success Criteria (Overall)

### Phase 1-3 Complete (Entities)
- ✅ All 12 entities created with zero compilation errors
- ✅ All entities inherit from BaseEntity
- ✅ All navigation properties correctly defined
- ✅ All computed properties implemented
- ✅ Full XML documentation on all entities
- ✅ Zero external dependencies (pure .NET 9)

### Phase 4 Complete (Repositories)
- ✅ IRepository<T> interface defined
- ✅ IUnitOfWork interface defined with all entity repositories
- ✅ Full XML documentation on interfaces

### Phase 5 Complete (Tests)
- ✅ All entity tests passing (19 test classes)
- ✅ Code coverage >80% for domain entities
- ✅ All computed properties tested
- ✅ Edge cases covered (null values, zero divisions, etc.)
- ✅ Integration tests for navigation properties passing

### Final Verification
```bash
# All commands succeed with zero errors/warnings
dotnet build CarePath.sln
dotnet test CarePath.sln
dotnet test --collect:"XPlat Code Coverage"
```

---

## Dependencies Graph

```
TASK-001 (Domain Project)
   └─> TASK-002 (Tests Project)
   └─> TASK-004 (BaseEntity) ──┬─> TASK-007 (User) ──┬─> TASK-008 (Caregiver) ──> TASK-011 (Shift)
                                │                      └─> TASK-009 (Client) ─────> TASK-011 (Shift)
                                └─> TASK-005 (Enums) ──┴─> TASK-011 (Shift) ──┬─> TASK-012 (VisitNote)
                                                                                └─> TASK-014 (Invoice)
TASK-011 (Shift) ──> TASK-012 (VisitNote) ──> TASK-013 (VisitPhoto)
TASK-014 (Invoice) ──┬─> TASK-015 (InvoiceLineItem)
                      └─> TASK-016 (Payment)
TASK-008 (Caregiver) ──> TASK-017 (CaregiverCertification)

TASK-004 (BaseEntity) ──> TASK-019 (IRepository) ──> TASK-020 (IUnitOfWork)

All Entity Tasks (TASK-007 to TASK-017) ──> Testing Tasks (TASK-021 to TASK-039)
```

---

## Out of Scope (Future Specs)

❌ **Not in CP-01** (will be in separate specs):
- EF Core DbContext, migrations, or database schema
- Application layer (services, DTOs, validators)
- Infrastructure layer (repository implementations)
- API endpoints or controllers
- MAUI mobile app or Blazor web UI
- ASP.NET Core Identity integration (see Design Section 8)

---

## Related Documents

- **[Requirements Spec](../01-requirements/cp-01-create-domain-entities.md)**
- **[Design Spec](../02-design/cp-01-create-domain-entities.md)**
- **[Architecture.md](../../Documentation/Architecture.md)**
- **[CarePath_Health.pdf](../../Documentation/CarePath_Health.pdf)**

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-02-16 | Tobi Kareem | Initial tasks breakdown (39 tasks, 5 phases) |
