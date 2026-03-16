# Tasks Breakdown: CP-02 Infrastructure & EF Core Layer

**Date**: 2026-02-25
**Author**: Tobi Kareem
**Project**: CarePath Health
**Status**: Draft
**Spec Number**: CP-02
**Related Specs**:
- **Requirements**: [cp-02-infrastructure-ef-core.md](../01-requirements/cp-02-infrastructure-ef-core.md) *(to be created)*
- **Design**: [cp-02-infrastructure-ef-core.md](../02-design/cp-02-infrastructure-ef-core.md) *(to be created)*
- **Domain Tasks (CP-01)**: [cp-01-create-domain-entities.md](./cp-01-create-domain-entities.md)

---

## Executive Summary

> **Implement the Infrastructure & EF Core layer for CarePath Health, adding database persistence via Entity Framework Core 9, SQL Server integration, ASP.NET Identity, repository pattern, Unit of Work, migrations, seed data, and comprehensive infrastructure tests.**

This task spec breaks down the Infrastructure layer implementation into 39 atomic tasks across 10 phases: Project Setup, Core Infrastructure (ValueConverters, Interceptors), EF Core DbContext, Entity Configurations (12 entities), Repository Implementation, Dependency Injection, Migrations, Seed Data, Testing, and Final Verification.

All tasks build on CP-01 (Domain Entities) which must be completed and merged before CP-02 begins.

**Total Estimated Time**: ~45-55 hours (1.5-2 weeks for one developer)

---

## Task Summary by Phase

| Phase | Tasks | Estimated Time | Dependencies |
|-------|-------|----------------|--------------|
| Phase 1: Project Setup | 3 tasks | 2.5 hours | CP-01 Approved |
| Phase 2: Core Infrastructure | 3 tasks | 4 hours | Phase 1 |
| Phase 3: EF Core DbContext | 1 task | 2 hours | Phase 2 |
| Phase 4: Entity Configurations | 12 tasks | 12 hours | Phase 2, Phase 3 |
| Phase 5: Repository Implementation | 3 tasks | 5 hours | Phase 4 |
| Phase 6: Dependency Injection | 3 tasks | 3 hours | Phase 5 |
| Phase 7: Migrations | 3 tasks | 4 hours | Phase 6 |
| Phase 8: Seed Data | 2 tasks | 3 hours | Phase 7 |
| Phase 9: Testing | 8 tasks | 14 hours | All Prior Phases |
| Phase 10: Final Verification | 1 task | 1.5 hours | Phase 9 |

**Total Phases**: 10  
**Total Tasks**: 39 (TASK-040 through TASK-078)

---

## Phase 1: Project Setup

### TASK-040: Create Infrastructure Project Structure

- **Layer**: CarePath.Infrastructure
- **Dependencies**: CP-01 Complete (Domain project merged)
- **Estimate**: 1.5 hours
- **Priority**: Critical (blocks all other tasks)
- **Success Criteria**:
  - `Infrastructure/Infrastructure.csproj` created with .NET 9 configuration
  - Folder structure created: `Persistence/`, `Persistence/Configurations/`, `Persistence/Interceptors/`, `Persistence/Converters/`, `Persistence/Repositories/`, `Identity/`
  - Project builds successfully with zero warnings
  - Implicit usings enabled
  - Nullable reference types enabled
  - Project references Domain project
- **Files**:
  - CREATE: `Infrastructure/Infrastructure.csproj`
  - CREATE: `Infrastructure/Persistence/` (folder)
  - CREATE: `Infrastructure/Persistence/Configurations/` (folder — entity configs only)
  - CREATE: `Infrastructure/Persistence/Configurations/Identity/` (folder)
  - CREATE: `Infrastructure/Persistence/Configurations/Scheduling/` (folder)
  - CREATE: `Infrastructure/Persistence/Configurations/Billing/` (folder)
  - CREATE: `Infrastructure/Persistence/Interceptors/` (folder — sibling of Configurations, not inside)
  - CREATE: `Infrastructure/Persistence/Converters/` (folder — sibling of Configurations, not inside)
  - CREATE: `Infrastructure/Persistence/Repositories/` (folder)
  - CREATE: `Infrastructure/Identity/` (folder)
- **Commands**:
  ```bash
  dotnet new classlib -n Infrastructure -f net9.0
  mkdir -p Infrastructure/Persistence Infrastructure/Persistence/Configurations Infrastructure/Persistence/Interceptors Infrastructure/Persistence/Converters Infrastructure/Persistence/Repositories Infrastructure/Identity
  dotnet add Infrastructure/Infrastructure.csproj reference Domain/Domain.csproj
  dotnet sln CarePath.sln add Infrastructure/Infrastructure.csproj
  dotnet build Infrastructure/Infrastructure.csproj
  ```
- **Implementation Notes**:
  - Set `<ImplicitUsings>enable</ImplicitUsings>` in .csproj
  - Set `<Nullable>enable</Nullable>` in .csproj
  - Set `<RootNamespace>CarePath.Infrastructure</RootNamespace>`

---

### TASK-041: Add NuGet Dependencies to Infrastructure Project

- **Layer**: CarePath.Infrastructure
- **Dependencies**: TASK-040
- **Estimate**: 0.5 hours
- **Priority**: Critical
- **Success Criteria**:
  - All required packages installed with compatible versions
  - `dotnet build Infrastructure/Infrastructure.csproj` succeeds
- **Commands**:
  ```bash
  # EF Core 9
  dotnet add Infrastructure package Microsoft.EntityFrameworkCore --version 9.0.0
  dotnet add Infrastructure package Microsoft.EntityFrameworkCore.SqlServer --version 9.0.0
  dotnet add Infrastructure package Microsoft.EntityFrameworkCore.Tools --version 9.0.0
  
  # Identity
  dotnet add Infrastructure package Microsoft.AspNetCore.Identity.EntityFrameworkCore --version 9.0.0
  
  # Build verification
  dotnet build Infrastructure/Infrastructure.csproj
  ```
- **Implementation Notes**:
  - EF Core Tools (PMC / CLI) must match DbContext project runtime
  - ASP.NET Identity packages depend on EF Core being installed first

---

### TASK-042: Create Infrastructure.Tests Project

- **Layer**: Tests
- **Dependencies**: TASK-040, TASK-041
- **Estimate**: 0.5 hours
- **Priority**: High
- **Success Criteria**:
  - `Infrastructure.Tests/Infrastructure.Tests.csproj` created
  - References Infrastructure, Domain, and test packages
  - Project builds successfully
- **Files**:
  - CREATE: `Infrastructure.Tests/Infrastructure.Tests.csproj`
  - CREATE: `Infrastructure.Tests/Persistence/` (folder)
  - CREATE: `Infrastructure.Tests/Interceptors/` (folder)
  - CREATE: `Infrastructure.Tests/Converters/` (folder)
- **Commands**:
  ```bash
  dotnet new xunit -n Infrastructure.Tests -f net9.0
  dotnet add Infrastructure.Tests reference Infrastructure/Infrastructure.csproj
  dotnet add Infrastructure.Tests reference Domain/Domain.csproj
  dotnet add Infrastructure.Tests package FluentAssertions --version 7.0.0
  dotnet add Infrastructure.Tests package Moq --version 4.20.0
  dotnet add Infrastructure.Tests package Microsoft.EntityFrameworkCore.InMemory --version 9.0.0
  dotnet sln CarePath.sln add Infrastructure.Tests/Infrastructure.Tests.csproj
  ```

---

## Phase 2: Core Infrastructure

### TASK-043: Create UtcDateTimeConverter

- **Layer**: CarePath.Infrastructure
- **Dependencies**: TASK-040, TASK-041
- **Estimate**: 1.5 hours
- **Priority**: Critical (blocks entity configurations)
- **Success Criteria**:
  - `UtcDateTimeConverter.cs` created in `Persistence/Converters/`
  - Handles `DateTime` properties: applies `DateTimeKind.Utc` on read
  - Handles `DateTime?` (nullable) properties separately
  - All DateTime values are stored as UTC in SQL Server
  - No data loss on round-trip conversion
- **Files**:
  - CREATE: `Infrastructure/Persistence/Converters/UtcDateTimeConverter.cs`
- **Implementation Notes**:
  - Inherit from `ValueConverter<DateTime, DateTime>` for non-nullable
  - Create separate `ValueConverter<DateTime?, DateTime?>` for nullable variant
  - The converter must apply `DateTimeKind.Utc` to ensure all reads return UTC values
  - Use in all entity configurations via `.HasConversion(new UtcDateTimeConverter())`
  - Reference: EF Core 9 documentation on `ValueConverter`

---

### TASK-044: Create AuditableEntityInterceptor

- **Layer**: CarePath.Infrastructure
- **Dependencies**: TASK-040, TASK-041
- **Estimate**: 2 hours
- **Priority**: Critical (blocks DbContext setup)
- **Success Criteria**:
  - `AuditableEntityInterceptor.cs` created in `Persistence/Interceptors/`
  - Inherits from `SaveChangesInterceptor`
  - Automatically sets `CreatedAt`, `CreatedBy` on Added entities
  - Automatically sets `UpdatedAt`, `UpdatedBy` on Modified entities
  - Retrieves current user ID from `IHttpContextAccessor`
  - Gracefully handles null HttpContext (e.g., in background jobs)
  - Does not throw exceptions if HttpContext unavailable
- **Files**:
  - CREATE: `Infrastructure/Persistence/Interceptors/AuditableEntityInterceptor.cs`
- **Implementation Notes**:
  - Inject `IHttpContextAccessor` in constructor
  - Override `SavingChangesAsync(DbContextEventData eventData, InterceptorResult result)`
  - Get user ID from `HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value`
  - Fallback to "System" if user unavailable
  - Set `CreatedAt = DateTime.UtcNow` if not already set
  - Set `UpdatedAt = DateTime.UtcNow` for modified entities
  - Handle both Added and Modified entity states
  - Do NOT throw if HttpContext is null — use system default

---

### TASK-045: Create CarePathDbContext

- **Layer**: CarePath.Infrastructure
- **Dependencies**: TASK-041, TASK-044, CP-01 Complete
- **Estimate**: 2 hours
- **Priority**: Critical (core persistence)
- **Success Criteria**:
  - `CarePathDbContext.cs` created in `Persistence/`
  - Inherits from `IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>`
  - All 12 domain entities have `DbSet<T>` properties
  - `OnModelCreating` applies entity configurations from assembly
  - `SaveChangesAsync` registers interceptors
  - Nullable reference types respected throughout
- **Files**:
  - CREATE: `Infrastructure/Persistence/CarePathDbContext.cs`
- **DbSet Properties** (in order):
  ```
  DbSet<User>
  DbSet<Caregiver>
  DbSet<CaregiverCertification>
  DbSet<Client>
  DbSet<CarePlan>
  DbSet<Shift>
  DbSet<VisitNote>
  DbSet<VisitPhoto>
  DbSet<Invoice>
  DbSet<InvoiceLineItem>
  DbSet<Payment>
  DbSet<ApplicationUser>  (Identity)
  ```
- **Implementation Notes**:
  - DbContext must accept `DbContextOptions<CarePathDbContext>` in constructor
  - Call `base.OnConfiguring(optionsBuilder)` in OnConfiguring if needed
  - In `OnModelCreating(ModelBuilder modelBuilder)`:
    - Call `base.OnModelCreating(modelBuilder)` for Identity tables
    - Apply all entity configurations via `modelBuilder.ApplyConfigurationsFromAssembly(typeof(CarePathDbContext).Assembly)`
  - Register `AuditableEntityInterceptor` via dependency injection (see TASK-062)
  - Do NOT add data seeding in DbContext — that's handled in TASK-068

---

## Phase 3: EF Core DbContext

### TASK-046: Create ApplicationUser (ASP.NET Identity Integration)

- **Layer**: CarePath.Infrastructure
- **Dependencies**: TASK-040, TASK-041, CP-01 Complete
- **Estimate**: 2 hours
- **Priority**: Critical (Identity integration)
- **Success Criteria**:
  - `ApplicationUser.cs` created in `Identity/`
  - Inherits from `IdentityUser<Guid>`
  - Has `Guid DomainUserId` foreign key to domain `User` entity
  - Has `User` navigation property (optional, for eager loading)
  - `ApplicationUserConfiguration.cs` created to configure the relationship
  - Configuration registered in DbContext
- **Files**:
  - CREATE: `Infrastructure/Identity/ApplicationUser.cs`
  - CREATE: `Infrastructure/Persistence/Configurations/ApplicationUserConfiguration.cs`
- **Implementation Notes**:
  - `ApplicationUser` extends ASP.NET Identity with reference to domain `User`
  - The separation allows Identity authentication without exposing all domain user data
  - Foreign key constraint on `DomainUserId` with cascade delete (ApplicationUser records depend on User)
  - Configuration should use Fluent API to set table name, constraints, indexes
  - Include in DbContext via `base.OnModelCreating(modelBuilder)` call (Identity DbContext handles it)

---

## Phase 4: Entity Configurations (12 tasks)

Each entity configuration task creates a Fluent API configuration class that:
- Sets table name explicitly
- Configures primary key (`HasKey(x => x.Id)`)
- Defines property constraints (max length, precision, required)
- Configures all relationships with correct foreign keys and cascade behavior
- Adds indexes on FKs and frequently queried columns
- Applies global query filter `HasQueryFilter(x => !x.IsDeleted)` for soft deletes
- Applies UTC datetime converter to all `DateTime` / `DateTime?` properties
- Ignores computed properties via `.Ignore(x => x.ComputedProperty)`

---

### TASK-047: UserConfiguration

- **Layer**: CarePath.Infrastructure
- **Dependencies**: TASK-045, TASK-046
- **Estimate**: 1 hour
- **Priority**: High
- **Files**: CREATE `Infrastructure/Persistence/Configurations/UserConfiguration.cs`
- **Entity**: User
- **Key Points**:
  - Table name: `"Users"`
  - PK: `Id` (Guid)
  - Configure string properties: `FirstName`, `LastName` (max 100), `Email` (max 256, unique index), `PhoneNumber`, `Address`, `City`, `State`, `ZipCode`
  - Ignore computed property: `FullName`
  - Add index on `Email` (authentication)
  - Add index on `IsDeleted` (soft delete queries)
  - Global query filter: `x => !x.IsDeleted`
  - Navigation: One User → Many Caregivers (if any exist as FK on Caregiver)
  - All DateTime properties get UTC converter

---

### TASK-048: CaregiverConfiguration

- **Layer**: CarePath.Infrastructure
- **Dependencies**: TASK-045, TASK-047
- **Estimate**: 1.5 hours
- **Priority**: High
- **Files**: CREATE `Infrastructure/Persistence/Configurations/CaregiverConfiguration.cs`
- **Entity**: Caregiver
- **Key Points**:
  - Table name: `"Caregivers"`
  - PK: `Id` (Guid)
  - FK: `UserId` → `Users.Id` (required, cascade delete)
  - Configure decimal: `HourlyPayRate` (precision 18, scale 2)
  - Add indexes: `UserId`, `EmploymentType`, `IsDeleted`
  - Global query filter: `x => !x.IsDeleted`
  - Relationship: `HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade)`
  - Relationship: `HasMany(x => x.Certifications).WithOne().HasForeignKey(x => x.CaregiverId)`

---

### TASK-049: CaregiverCertificationConfiguration

- **Layer**: CarePath.Infrastructure
- **Dependencies**: TASK-045, TASK-048
- **Estimate**: 1 hour
- **Priority**: High
- **Files**: CREATE `Infrastructure/Persistence/Configurations/CaregiverCertificationConfiguration.cs`
- **Entity**: CaregiverCertification
- **Key Points**:
  - Table name: `"CaregiverCertifications"`
  - PK: `Id` (Guid)
  - FK: `CaregiverId` → `Caregivers.Id` (required, Restrict — CaregiverCertification is PHI per HIPAA)
  - String properties: `CertificationNumber` (max 50), `IssuingAuthority` (max 100)
  - Ignore computed properties: `IsExpired`, `IsExpiringSoon`
  - Add indexes: `CaregiverId`, `CertificationType`, `ExpirationDate`, `IsDeleted`
  - Global query filter: `x => !x.IsDeleted`
  - All DateTime properties get UTC converter

---

### TASK-050: ClientConfiguration

- **Layer**: CarePath.Infrastructure
- **Dependencies**: TASK-045, TASK-047
- **Estimate**: 1 hour
- **Priority**: High
- **Files**: CREATE `Infrastructure/Persistence/Configurations/ClientConfiguration.cs`
- **Entity**: Client
- **Key Points**:
  - Table name: `"Clients"`
  - PK: `Id` (Guid)
  - FK: `UserId` → `Users.Id` (required, cascade delete)
  - String properties: `EmergencyContactName` (max 100), `EmergencyContactPhone` (max 20), `EmergencyContactRelationship` (max 100), `SpecialInstructions` (max 1000), `MedicalConditions` (max 1000), `Allergies` (max 500), `LocationNotes` (max 500), `InsuranceProvider` (max 100), `InsurancePolicyNumber` (max 50), `MedicaidNumber` (max 50)
  - Decimal properties: `HourlyBillRate` (18,2)
  - Ignore computed property: `Age`
  - Add indexes: `UserId` (unique), `DateOfBirth`, `IsDeleted`
  - Global query filter: `x => !x.IsDeleted`
  - All DateTime properties (including `DateOfBirth`) get UTC converter

---

### TASK-051: CarePlanConfiguration

- **Layer**: CarePath.Infrastructure
- **Dependencies**: TASK-045, TASK-050
- **Estimate**: 1 hour
- **Priority**: High
- **Files**: CREATE `Infrastructure/Persistence/Configurations/CarePlanConfiguration.cs`
- **Entity**: CarePlan
- **Key Points**:
  - Table name: `"CarePlans"`
  - PK: `Id` (Guid)
  - FK: `ClientId` → `Clients.Id` (required, no cascade delete — HIPAA: preserve care plans independently)
  - String properties: `PrimaryDiagnosis` (max 500), `SpecialNeeds` (max 1000), `Goals` (max 2000), `MedicationNotes` (max 1000)
  - Add indexes: `ClientId`, `Status`, `IsDeleted`
  - Global query filter: `x => !x.IsDeleted`
  - All DateTime properties get UTC converter

---

### TASK-052: ShiftConfiguration (Most Complex)

- **Layer**: CarePath.Infrastructure
- **Dependencies**: TASK-045, TASK-048, TASK-050
- **Estimate**: 2 hours
- **Priority**: Critical (core business entity with complex relationships and computed properties)
- **Files**: CREATE `Infrastructure/Persistence/Configurations/ShiftConfiguration.cs`
- **Entity**: Shift
- **Key Points**:
  - Table name: `"Shifts"`
  - PK: `Id` (Guid)
  - FKs: `CaregiverId` → `Caregivers.Id` (nullable, SetNull — unassigned shifts allowed), `ClientId` → `Clients.Id` (required, Restrict — Shift is PHI)
  - Decimal properties with precision: `BillRate` (18,2), `PayRate` (18,2)
  - GPS fields: `CheckInLatitude`, `CheckInLongitude`, `CheckOutLatitude`, `CheckOutLongitude` — nullable, type `double?`
  - DateTime properties with UTC converter: `ScheduledStartTime`, `ScheduledEndTime`, `ActualStartTime`, `ActualEndTime`, `CheckInTime`, `CheckOutTime`, `CancelledAt`
  - `BreakMinutes` — int, nullable
  - Ignore computed properties: `BillableHours`, `GrossMargin`, `GrossMarginPercentage`
  - Add indexes: `CaregiverId`, `ClientId`, `Status`, `ScheduledStartTime`, `IsDeleted`
  - Global query filter: `x => !x.IsDeleted`
  - **Relationship configuration**:
    - `HasOne(x => x.Client).WithMany(x => x.Shifts).HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict)`
    - `HasOne(x => x.Caregiver).WithMany(x => x.Shifts).HasForeignKey(x => x.CaregiverId).OnDelete(DeleteBehavior.SetNull)`
    - `HasMany(x => x.VisitNotes).WithOne(x => x.Shift).HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Restrict)`
  - **HIPAA Note**: Restrict on Client/VisitNotes prevents cascading deletes that could lose shift history; SetNull on Caregiver allows unassigned shifts
  - Ignore computed properties: `ScheduledDuration`, `ActualDuration`, `BillableHours`, `GrossMargin`, `GrossMarginPercentage`

---

### TASK-053: VisitNoteConfiguration

- **Layer**: CarePath.Infrastructure
- **Dependencies**: TASK-045, TASK-052
- **Estimate**: 1 hour
- **Priority**: High (clinical record — PHI)
- **Files**: CREATE `Infrastructure/Persistence/Configurations/VisitNoteConfiguration.cs`
- **Entity**: VisitNote
- **Key Points**:
  - Table name: `"VisitNotes"`
  - PK: `Id` (Guid)
  - FK: `ShiftId` → `Shifts.Id` (required, no cascade delete — HIPAA: preserve notes independently)
  - String properties: `Notes` (max 4000 — clinical notes can be lengthy)
  - Add indexes: `ShiftId`, `IsDeleted`
  - Global query filter: `x => !x.IsDeleted`
  - All DateTime properties get UTC converter
  - **PHI Alert**: Clinical notes are PHI — ensure logged access is minimal, encrypted at rest

---

### TASK-054: VisitPhotoConfiguration

- **Layer**: CarePath.Infrastructure
- **Dependencies**: TASK-045, TASK-053
- **Estimate**: 1 hour
- **Priority**: High (PHI — photos of clients)
- **Files**: CREATE `Infrastructure/Persistence/Configurations/VisitPhotoConfiguration.cs`
- **Entity**: VisitPhoto
- **Key Points**:
  - Table name: `"VisitPhotos"`
  - PK: `Id` (Guid)
  - FK: `VisitNoteId` → `VisitNotes.Id` (required, Restrict — VisitPhoto is PHI per HIPAA)
  - String properties: `PhotoUrl` (max 2048 — URL length), `FileName` (max 255)
  - Add indexes: `VisitNoteId`, `IsDeleted`
  - Global query filter: `x => !x.IsDeleted`
  - All DateTime properties get UTC converter
  - **PHI Alert**: Photos of clients are PHI — restrict access, ensure encrypted storage

---

### TASK-055: InvoiceConfiguration

- **Layer**: CarePath.Infrastructure
- **Dependencies**: TASK-045, TASK-050
- **Estimate**: 1.5 hours
- **Priority**: High (billing data — sensitive)
- **Files**: CREATE `Infrastructure/Persistence/Configurations/InvoiceConfiguration.cs`
- **Entity**: Invoice
- **Key Points**:
  - Table name: `"Invoices"`
  - PK: `Id` (Guid)
  - FK: `ClientId` → `Clients.Id` (required, no cascade delete — HIPAA: preserve invoices independently)
  - String properties: `InvoiceNumber` (max 20, unique index), `Notes` (max 1000)
  - Decimal properties with precision: `TaxAmount` (18,2)
  - Ignore computed properties: `Subtotal`, `Total`, `AmountPaid`, `Balance`, `IsFullyPaid`
  - Add indexes: `ClientId`, `InvoiceNumber`, `Status`, `DueDate`, `IsDeleted`
  - Global query filter: `x => !x.IsDeleted`
  - All DateTime properties get UTC converter
  - Relationship: `HasMany(x => x.LineItems).WithOne().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade)`

---

### TASK-056: InvoiceLineItemConfiguration

- **Layer**: CarePath.Infrastructure
- **Dependencies**: TASK-045, TASK-055
- **Estimate**: 1 hour
- **Priority**: High
- **Files**: CREATE `Infrastructure/Persistence/Configurations/InvoiceLineItemConfiguration.cs`
- **Entity**: InvoiceLineItem
- **Key Points**:
  - Table name: `"InvoiceLineItems"`
  - PK: `Id` (Guid)
  - FK: `InvoiceId` → `Invoices.Id` (required, cascade delete OK — line items depend on invoice)
  - String properties: `Description` (max 500)
  - Decimal properties with precision: `Hours` (10,2), `RatePerHour` (18,2), `CostPerHour` (18,2)
  - DateTime: `ServiceDate` with UTC converter
  - Ignore computed properties: `Total`, `TotalCost`, `GrossProfit`, `GrossMarginPercentage`
  - Add indexes: `InvoiceId`, `IsDeleted`
  - Global query filter: `x => !x.IsDeleted`

---

### TASK-057: PaymentConfiguration

- **Layer**: CarePath.Infrastructure
- **Dependencies**: TASK-045, TASK-055
- **Estimate**: 1 hour
- **Priority**: High
- **Files**: CREATE `Infrastructure/Persistence/Configurations/PaymentConfiguration.cs`
- **Entity**: Payment
- **Key Points**:
  - Table name: `"Payments"`
  - PK: `Id` (Guid)
  - FK: `InvoiceId` → `Invoices.Id` (required, `DeleteBehavior.Cascade` — LineItems and Payments cascade with their parent Invoice; soft delete enforced in code)
  - String properties: `ReferenceNumber` (max 50), `Notes` (max 500)
  - Decimal properties with precision: `Amount` (18,2)
  - Add indexes: `InvoiceId`, `PaymentDate`, `IsDeleted`
  - `IsSuccessful` has default value `true`
  - `FailureReason` (max 500)
  - Global query filter: `x => !x.IsDeleted`
  - All DateTime properties get UTC converter

---

### TASK-058: ApplicationUserConfiguration (Identity)

- **Layer**: CarePath.Infrastructure
- **Dependencies**: TASK-045, TASK-046, TASK-047
- **Estimate**: 1 hour
- **Priority**: High
- **Files**: CREATE or MODIFY `Infrastructure/Persistence/Configurations/ApplicationUserConfiguration.cs`
- **Entity**: ApplicationUser
- **Key Points**:
  - Table name: `"AspNetUsers"` (standard ASP.NET Identity)
  - Configure relationship to domain `User`: `HasOne(x => x.DomainUser).WithOne().HasForeignKey<ApplicationUser>(x => x.DomainUserId).OnDelete(DeleteBehavior.Cascade)`
  - Add index on `DomainUserId` for quick lookups
  - String properties: `UserName`, `Email` — inherited from IdentityUser, already configured by Identity
  - Note: Do NOT soft delete ApplicationUser records — Identity needs them for access control

---

## Phase 5: Repository Implementation

### TASK-059: Implement Generic Repository<T>

- **Layer**: CarePath.Infrastructure
- **Dependencies**: TASK-045, CP-01 (IRepository interface)
- **Estimate**: 2 hours
- **Priority**: Critical (core data access)
- **Success Criteria**:
  - `Repository<T>.cs` created in `Persistence/Repositories/`
  - Implements `IRepository<T>` from Domain layer
  - All CRUD methods implemented: `GetByIdAsync`, `GetAllAsync`, `FindAsync`, `AddAsync`, `UpdateAsync`, `DeleteAsync`
  - Soft delete implemented: `DeleteAsync` sets `IsDeleted = true`
  - `GetPagedAsync` implemented for large datasets (Shift, VisitNote)
  - Returns `IReadOnlyList<T>` for query methods
  - No data is permanently deleted from database
- **Files**:
  - CREATE: `Infrastructure/Persistence/Repositories/Repository.cs`
- **Method Signatures**:
  ```csharp
  public async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
  public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
  public async Task<IReadOnlyList<T>> FindAsync(Func<T, bool> predicate, CancellationToken cancellationToken = default)
  public async Task AddAsync(T entity, CancellationToken cancellationToken = default)
  public async Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
  public async Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
  public async Task<(IReadOnlyList<T> Items, int TotalCount)> GetPagedAsync(
      int pageNumber, 
      int pageSize, 
      CancellationToken cancellationToken = default)
  ```
- **Implementation Notes**:
  - Generic constraint: `where T : BaseEntity`
  - Inject `CarePathDbContext` in constructor
  - Global query filter automatically excludes soft-deleted records (configured in entity configurations)
  - `GetPagedAsync` uses `.Skip()` and `.Take()` — use SkipTake for efficient database queries
  - Return type is `IReadOnlyList<T>` to signal materialized results (not lazy IEnumerable)
  - All async methods accept optional `CancellationToken`

---

### TASK-060: Implement Unit of Work Pattern

- **Layer**: CarePath.Infrastructure
- **Dependencies**: TASK-059, CP-01 (IUnitOfWork interface)
- **Estimate**: 2 hours
- **Priority**: Critical
- **Success Criteria**:
  - `UnitOfWork.cs` created in `Persistence/Repositories/`
  - Implements `IUnitOfWork` from Domain layer
  - Lazy-initialized repository properties for all 12 entities
  - `SaveChangesAsync()` persists all changes
  - Transaction management: `BeginTransactionAsync`, `CommitTransactionAsync`, `RollbackTransactionAsync`
  - Implements `IDisposable` and `IAsyncDisposable`
  - Thread-safe access to repositories
- **Files**:
  - CREATE: `Infrastructure/Persistence/Repositories/UnitOfWork.cs`
- **Properties**:
  ```csharp
  IRepository<User> Users { get; }
  IRepository<Caregiver> Caregivers { get; }
  IRepository<CaregiverCertification> CaregiverCertifications { get; }
  IRepository<Client> Clients { get; }
  IRepository<CarePlan> CarePlans { get; }
  IRepository<Shift> Shifts { get; }
  IRepository<VisitNote> VisitNotes { get; }
  IRepository<VisitPhoto> VisitPhotos { get; }
  IRepository<Invoice> Invoices { get; }
  IRepository<InvoiceLineItem> InvoiceLineItems { get; }
  IRepository<Payment> Payments { get; }
  ```
- **Methods**:
  ```csharp
  Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
  Task BeginTransactionAsync(CancellationToken cancellationToken = default);
  Task CommitTransactionAsync(CancellationToken cancellationToken = default);
  Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
  ```
- **Implementation Notes**:
  - Use lazy initialization pattern with backing fields and properties
  - Example: `private IRepository<User>? _userRepository; public IRepository<User> Users => _userRepository ??= new Repository<User>(_context);`
  - Store `IDbContextTransaction` for transaction management
  - Implement `Dispose` and `DisposeAsync` to clean up transaction and DbContext

---

### TASK-061: Update IRepository<T> Interface with GetPagedAsync

- **Layer**: CarePath.Domain
- **Dependencies**: TASK-059, TASK-060
- **Estimate**: 0.5 hours
- **Priority**: High
- **Success Criteria**:
  - `IRepository<T>` updated with `GetPagedAsync` method signature
  - Signature matches implementation in TASK-059
  - Returns tuple of items and total count
- **Files**:
  - MODIFY: `Domain/Interfaces/Repositories/IRepository.cs`
- **Method to Add**:
  ```csharp
  /// <summary>
  /// Get a paginated list of entities.
  /// </summary>
  /// <param name="pageNumber">1-based page number (e.g., 1 for first page)</param>
  /// <param name="pageSize">Number of items per page</param>
  /// <param name="cancellationToken">Cancellation token</param>
  /// <returns>Tuple of items and total count of all items (unpaged)</returns>
  Task<(IReadOnlyList<T> Items, int TotalCount)> GetPagedAsync(
      int pageNumber, 
      int pageSize, 
      CancellationToken cancellationToken = default);
  ```
- **Implementation Notes**:
  - This method prevents N+1 queries and out-of-memory loads on large tables (Shift, VisitNote)
  - Returns total count so UI can calculate total pages
  - Essential for Application layer services that query large datasets

---

## Phase 6: Dependency Injection & Configuration

### TASK-062: Create DependencyInjection Extension Method

- **Layer**: CarePath.Infrastructure
- **Dependencies**: TASK-045, TASK-059, TASK-060, TASK-044
- **Estimate**: 1.5 hours
- **Priority**: Critical (DI setup)
- **Success Criteria**:
  - `DependencyInjection.cs` created in root of Infrastructure project
  - `AddInfrastructure(IServiceCollection services, string connectionString)` extension method defined
  - DbContext registered as scoped
  - Identity registered with token configuration
  - `AuditableEntityInterceptor` registered
  - `IUnitOfWork` registered as scoped
  - All services wire up correctly
- **Files**:
  - CREATE: `Infrastructure/DependencyInjection.cs`
- **Method Signature**:
  ```csharp
  public static IServiceCollection AddInfrastructure(
      this IServiceCollection services,
      string connectionString)
  {
      // Registration here
      return services;
  }
  ```
- **Registrations**:
  ```csharp
  services.AddDbContext<CarePathDbContext>(options =>
      options.UseSqlServer(connectionString));
  
  services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
  {
      // Password policy for MVP
      options.Password.RequireDigit = true;
      options.Password.RequireLowercase = true;
      options.Password.RequireNonAlphanumeric = false;
      options.Password.RequireUppercase = true;
      options.Password.RequiredLength = 8;
  })
  .AddEntityFrameworkStores<CarePathDbContext>()
  .AddDefaultTokenProviders();
  
  services.AddScoped<AuditableEntityInterceptor>();
  services.AddScoped<IUnitOfWork, UnitOfWork>();
  ```
- **Implementation Notes**:
  - Connection string passed from WebApi startup
  - Identity configuration uses reasonable defaults for healthcare
  - Token providers enable password reset and email confirmation
  - `AddDefaultTokenProviders()` is required for user confirmation tokens

---

### TASK-063: Update WebApi Program.cs for Infrastructure Setup

- **Layer**: CarePath.WebApi
- **Dependencies**: TASK-062, WebApi project must exist
- **Estimate**: 1 hour
- **Priority**: Critical
- **Success Criteria**:
  - WebApi project adds reference to Infrastructure
  - `Program.cs` calls `AddInfrastructure()` with connection string
  - Connection string loaded from configuration
  - DbContext migrations run at startup (optional but recommended)
  - `dotnet build CarePath.sln` succeeds
- **Files**:
  - MODIFY: `WebApi/WebApi.csproj` (add reference to Infrastructure)
  - MODIFY: `WebApi/Program.cs`
  - MODIFY or CREATE: `WebApi/appsettings.json`
- **Implementation in Program.cs**:
  ```csharp
  var builder = WebApplication.CreateBuilder(args);
  
  // Get connection string
  var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
      ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
  
  // Add Infrastructure services
  builder.Services.AddInfrastructure(connectionString);
  
  // ... rest of startup
  var app = builder.Build();
  
  // Optional: Apply migrations at startup
  using (var scope = app.Services.CreateScope())
  {
      var db = scope.ServiceProvider.GetRequiredService<CarePathDbContext>();
      await db.Database.MigrateAsync();
  }
  ```
- **Implementation Notes**:
  - Connection string must be in `appsettings.json` under `ConnectionStrings.DefaultConnection`
  - Auto-migration at startup is optional but helpful for development
  - Ensure WebApi project references Infrastructure project in .csproj

---

### TASK-064: Create appsettings.json with Connection String Configuration

- **Layer**: CarePath.WebApi
- **Dependencies**: TASK-063
- **Estimate**: 0.5 hours
- **Priority**: High
- **Success Criteria**:
  - `appsettings.json` updated with `ConnectionStrings` section
  - Development connection string uses local SQL Server
  - Production connection string uses environment variables or Azure Key Vault pattern
  - Encryption is enabled for HIPAA compliance
- **Files**:
  - MODIFY: `WebApi/appsettings.json`
  - CREATE (optional): `WebApi/appsettings.Production.json`
- **appsettings.json Content**:
  ```json
  {
    "ConnectionStrings": {
      "DefaultConnection": "Server=.;Database=CarePathHealth;Trusted_Connection=true;Encrypt=True;TrustServerCertificate=true;"
    },
    "Logging": {
      "LogLevel": {
        "Default": "Information"
      }
    }
  }
  ```
- **appsettings.Production.json Content**:
  ```json
  {
    "ConnectionStrings": {
      "DefaultConnection": "Server=tcp:{ServerName},1433;Initial Catalog={DatabaseName};Persist Security Info=False;User ID={UserId};Password={Password};MultipleActiveResultSets=False;Encrypt=True;Connection Timeout=30;"
    }
  }
  ```
- **Implementation Notes**:
  - Development: Use `Trusted_Connection=true` with Windows Auth on localhost
  - Production: Use environment variables or Key Vault for secrets
  - Always set `Encrypt=True` for HIPAA compliance
  - TrustServerCertificate allows self-signed certs in development only

---

## Phase 7: Migrations

### TASK-065: Generate Initial EF Core Migration

- **Layer**: CarePath.Infrastructure
- **Dependencies**: TASK-062, TASK-063, TASK-064, All entity configurations (TASK-047 through TASK-058)
- **Estimate**: 1.5 hours
- **Priority**: Critical (create initial schema)
- **Success Criteria**:
  - Migration file generated: `Infrastructure/Migrations/YYYYMMDDHHMMSS_InitialCreate.cs`
  - Designer snapshot updated: `Infrastructure/Migrations/YYYYMMDDHHMMSS_InitialCreate.Designer.cs`
  - Model snapshot updated: `Infrastructure/Migrations/CarePathDbContextModelSnapshot.cs`
  - Migration compiles without errors
  - No warnings from EF Core
- **Commands**:
  ```bash
  dotnet build CarePath.sln
  dotnet ef migrations add InitialCreate --project Infrastructure --startup-project WebApi
  ```
- **Implementation Notes**:
  - Run from repository root
  - All entity configurations must be complete and entity DbSets added to context
  - If migration generation fails, check: DbContext OnModelCreating, entity configurations, DbSet declarations
  - Do NOT modify generated migration files manually unless correcting a critical error

---

### TASK-066: Review and Validate Migration

- **Layer**: CarePath.Infrastructure
- **Dependencies**: TASK-065
- **Estimate**: 2 hours
- **Priority**: Critical (HIPAA compliance)
- **Success Criteria**:
  - Migration Up method creates all 12 entity tables with correct schemas
  - All foreign keys have correct references and cascade behavior
  - All indexes present on FKs and queried columns
  - All constraints match design (max lengths, decimal precision, required fields)
  - Global query filter correctly applied (IsDeleted column exists and is indexed)
  - Migration Down method correctly reverses all changes
  - No PHI data leakage in Up/Down logic
  - No hard deletes on PHI entities
- **Files**:
  - REVIEW: `Infrastructure/Migrations/YYYYMMDDHHMMSS_InitialCreate.cs`
  - REVIEW: `Infrastructure/Migrations/CarePathDbContextModelSnapshot.cs`
- **Review Checklist**:
  - [ ] All 12 tables created: Users, Caregivers, CaregiverCertifications, Clients, CarePlans, Shifts, VisitNotes, VisitPhotos, Invoices, InvoiceLineItems, Payments, AspNetUsers
  - [ ] Column types correct: `uniqueidentifier` for Guid, `datetime2` for DateTime, `decimal(18,2)` for rates/amounts, `nvarchar` for strings
  - [ ] String lengths enforced (no unnecessary `nvarchar(max)`)
  - [ ] Decimal precision: Bill rates, pay rates, amounts are `(18,2)`
  - [ ] Nullable columns match C# nullable annotations
  - [ ] Foreign keys cascade only on non-PHI or dependent entities
  - [ ] Global query filter indexes on `IsDeleted` column
  - [ ] Indexes on frequently queried columns (Email, Status, Dates)
  - [ ] AspNetUser.DomainUserId FK configured
  - [ ] Down method is reversible
- **Commands for Validation**:
  ```bash
  dotnet ef migrations script InitialCreate --project Infrastructure --startup-project WebApi --idempotent -o initial-migration.sql
  # Review initial-migration.sql for correctness
  ```
- **Implementation Notes**:
  - Use `.CloseScript("GO")` in migration script generation for SQL Server compatibility
  - Pay special attention to cascade behavior on Shift, VisitNote, Payment (should be Restrict)
  - Verify no data loss patterns (no column drops, no type conversions without migration path)

---

### TASK-067: Apply Initial Migration to Database

- **Layer**: CarePath.Infrastructure
- **Dependencies**: TASK-066
- **Estimate**: 0.5 hours
- **Priority**: Critical
- **Success Criteria**:
  - Migration applied successfully to local SQL Server database
  - All tables created with correct schemas
  - Database is accessible via DbContext
  - `dotnet ef migrations list` shows migration as applied (no `(Pending)`)
  - Schema matches design specifications
- **Commands**:
  ```bash
  dotnet ef database update --project Infrastructure --startup-project WebApi
  dotnet ef migrations list --project Infrastructure --startup-project WebApi
  ```
- **Verification**:
  - Connect to database with SQL Server Management Studio
  - Verify all 12 tables exist with correct columns, types, and constraints
  - Verify indexes are present
  - Verify Foreign Key constraints
- **Implementation Notes**:
  - Connection string must be correct in appsettings.json
  - SQL Server must be running locally (or connection string must point to remote)
  - Database is created automatically if it doesn't exist (provided identity has CREATE DATABASE rights)

---

## Phase 8: Seed Data

### TASK-068: Create Seed Data Configuration

- **Layer**: CarePath.Infrastructure
- **Dependencies**: TASK-045, TASK-067
- **Estimate**: 2 hours
- **Priority**: High (development/demo data)
- **Success Criteria**:
  - Seed data method created in Infrastructure (e.g., `CarePathDbContextSeed.cs`)
  - Default Admin user created with secure temp password (must be changed on first login)
  - Sample Caregiver(s) created for testing
  - Sample Client(s) created for testing
  - Seed method is idempotent (safe to run multiple times)
  - No seed data for production environments
- **Files**:
  - CREATE: `Infrastructure/Persistence/CarePathDbContextSeed.cs`
- **Seed Data to Create** (development only):
  - **Admin User**: Email `admin@carepath.local`, Role: Admin
  - **Sample Caregiver**: Email `caregiver@carepath.local`, EmploymentType: W2Employee
  - **Sample Client**: Email `client@carepath.local`
  - All with placeholder/temporary credentials
- **Implementation Notes**:
  - Use `HasData()` in entity configuration OR a separate seed method (recommend separate for clarity)
  - Seed method signature: `public static async Task SeedAsync(CarePathDbContext context, IUserManager<ApplicationUser> userManager, RoleManager<IdentityRole<Guid>> roleManager)`
  - Only seed if database is empty (idempotent check)
  - This is for development/demo — never include real user passwords
  - Production environments use separate data migration approach

---

### TASK-069: Verify Seed Data Applied Correctly

- **Layer**: CarePath.Infrastructure
- **Dependencies**: TASK-068
- **Estimate**: 1 hour
- **Priority**: High
- **Success Criteria**:
  - Seed data method is called at startup
  - Admin user, sample caregiver, sample client exist in database
  - Can verify via SQL Server Management Studio or Entity Framework queries
  - Sample data populated tables correctly
- **Commands**:
  ```bash
  # In WebApi Program.cs or separate initialization:
  using (var scope = app.Services.CreateScope())
  {
      var context = scope.ServiceProvider.GetRequiredService<CarePathDbContext>();
      var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
      var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
      await CarePathDbContextSeed.SeedAsync(context, userManager, roleManager);
  }
  ```
- **Verification**:
  - Connect to database with SQL Server Management Studio
  - Query `Users` table — should have admin, caregiver, client records
  - Query `Caregivers` table — should have sample caregiver
  - Query `Clients` table — should have sample client
  - Verify `AspNetUsers` table has corresponding Identity records

---

## Phase 9: Testing

### TASK-070: Create EF Core DbContext Tests (In-Memory)

- **Layer**: CarePath.Infrastructure.Tests
- **Dependencies**: TASK-042, TASK-045, TASK-047 through TASK-058
- **Estimate**: 2 hours
- **Priority**: High
- **Success Criteria**:
  - `DbContextTests.cs` created in `Infrastructure.Tests/Persistence/`
  - Tests verify DbContext configuration is correct
  - All entity DbSets accessible
  - In-memory database used for testing
  - Global query filter verified (IsDeleted records excluded)
- **Files**:
  - CREATE: `Infrastructure.Tests/Persistence/DbContextTests.cs`
- **Test Cases**:
  - Can create DbContext with in-memory database
  - All DbSet properties are accessible
  - Can add entity to DbContext
  - Soft delete: entity with `IsDeleted = true` is excluded from queries
  - Navigation properties are correctly lazy/eager loaded
  - Global query filter works for all entities
- **Implementation Notes**:
  - Use `new DbContextOptionsBuilder<CarePathDbContext>().UseInMemoryDatabase("TestDb")`
  - Test class pattern: `public class DbContextTests { [Fact] public async Task... { } }`
  - Each test should get a fresh in-memory database instance
  - Verify global query filter with `dbContext.Users.Where(u => !u.IsDeleted)` behavior

---

### TASK-071: Create Repository<T> Tests

- **Layer**: CarePath.Infrastructure.Tests
- **Dependencies**: TASK-042, TASK-059, TASK-070
- **Estimate**: 2 hours
- **Priority**: High
- **Success Criteria**:
  - `RepositoryTests.cs` created in `Infrastructure.Tests/Persistence/Repositories/`
  - Tests verify all CRUD operations
  - GetPagedAsync tested with pagination boundaries
  - Soft delete verified
  - No returned deleted records
- **Files**:
  - CREATE: `Infrastructure.Tests/Persistence/Repositories/RepositoryTests.cs`
- **Test Cases**:
  - GetByIdAsync returns entity if exists
  - GetByIdAsync returns null if not found
  - GetAllAsync returns all non-deleted entities
  - FindAsync filters correctly
  - AddAsync persists new entity
  - UpdateAsync updates existing entity
  - DeleteAsync sets IsDeleted = true (soft delete)
  - DeleteAsync prevents retrieval by GetAllAsync/GetByIdAsync
  - GetPagedAsync returns correct page and total count
- **Implementation Notes**:
  - Generic test using `User` entity as test type
  - Mock `CarePathDbContext` using Moq if needed, OR use in-memory DbContext
  - Prefer in-memory for integration-style testing
  - Verify no `DbSet.Remove()` is called — only `IsDeleted = true`

---

### TASK-072: Create UnitOfWork Tests

- **Layer**: CarePath.Infrastructure.Tests
- **Dependencies**: TASK-042, TASK-060, TASK-071
- **Estimate**: 1.5 hours
- **Priority**: High
- **Success Criteria**:
  - `UnitOfWorkTests.cs` created in `Infrastructure.Tests/Persistence/Repositories/`
  - Tests verify repository access and lazy initialization
  - Tests verify SaveChangesAsync persists changes
  - Tests verify transaction management
- **Files**:
  - CREATE: `Infrastructure.Tests/Persistence/Repositories/UnitOfWorkTests.cs`
- **Test Cases**:
  - Can access IRepository<User> without exception
  - Repositories are lazily initialized (created on first access)
  - Same repository instance returned on repeated access
  - SaveChangesAsync persists changes to in-memory DbContext
  - BeginTransactionAsync / CommitTransactionAsync / RollbackTransactionAsync work
  - Implement IDisposable correctly
- **Implementation Notes**:
  - Use in-memory DbContext for UnitOfWork testing
  - Verify lazy initialization doesn't create duplicate repositories
  - Test transaction rollback by modifying an entity, rolling back, and verifying change didn't persist

---

### TASK-073: Create Entity Configuration Tests

- **Layer**: CarePath.Infrastructure.Tests
- **Dependencies**: TASK-042, TASK-047 through TASK-058
- **Estimate**: 2 hours
- **Priority**: High
- **Success Criteria**:
  - `EntityConfigurationTests.cs` created in `Infrastructure.Tests/Persistence/`
  - Tests verify all entity configurations are correct
  - Column constraints verified (max lengths, decimal precision)
  - Indexes verified
  - Global query filter verified
  - Cascade behaviors verified
- **Files**:
  - CREATE: `Infrastructure.Tests/Persistence/EntityConfigurationTests.cs`
- **Test Cases** (for each entity):
  - Table name is correct
  - Primary key is configured correctly
  - Foreign keys reference correct entities
  - Cascade delete behavior is intentional
  - String max lengths are enforced
  - Decimal precision is correct (e.g., rates are 18,2)
  - Global query filter excludes IsDeleted records
  - Computed properties are ignored
  - Indexes exist on FKs and queried columns
- **Implementation Notes**:
  - Use `modelBuilder` metadata to verify configuration
  - Example: `var entity = modelBuilder.Entity<User>(); var props = entity.Metadata.GetProperties();`
  - Verify column types via `EF_METADATA` or direct schema inspection
  - This test class is integration-style; it validates the configuration matches the design

---

### TASK-074: Create AuditableEntityInterceptor Tests

- **Layer**: CarePath.Infrastructure.Tests
- **Dependencies**: TASK-042, TASK-044
- **Estimate**: 1.5 hours
- **Priority**: High
- **Success Criteria**:
  - `AuditableEntityInterceptorTests.cs` created in `Infrastructure.Tests/Interceptors/`
  - Tests verify audit fields are set on create and update
  - Tests verify user ID is captured from HttpContext
  - Tests verify graceful handling of null HttpContext
- **Files**:
  - CREATE: `Infrastructure.Tests/Interceptors/AuditableEntityInterceptorTests.cs`
- **Test Cases**:
  - CreatedAt and CreatedBy set on new entity
  - UpdatedAt and UpdatedBy set on modified entity
  - CreatedAt not overwritten on update
  - User ID captured from HttpContext claims
  - Falls back to "System" if HttpContext null
  - Timestamps are UTC
- **Implementation Notes**:
  - Mock `IHttpContextAccessor` to simulate HttpContext and claims
  - Create a test entity (e.g., `User`) and track state changes
  - Verify `SavingChangesAsync` is called and sets properties correctly

---

### TASK-075: Create UtcDateTimeConverter Tests

- **Layer**: CarePath.Infrastructure.Tests
- **Dependencies**: TASK-042, TASK-043
- **Estimate**: 1 hour
- **Priority**: High
- **Success Criteria**:
  - `UtcDateTimeConverterTests.cs` created in `Infrastructure.Tests/Converters/`
  - Tests verify DateTime values are converted to UTC on read
  - Tests verify nullable DateTime values are handled
  - Tests verify round-trip conversion (write → read) preserves value
- **Files**:
  - CREATE: `Infrastructure.Tests/Converters/UtcDateTimeConverterTests.cs`
- **Test Cases**:
  - Non-nullable converter: DateTime with kind Unspecified → Utc
  - Non-nullable converter: DateTime with kind Local → Utc (value preserved)
  - Nullable converter: null → null
  - Nullable converter: DateTime? with kind Unspecified → Utc
  - Round-trip: write value → read value = original value (same instant in time)
- **Implementation Notes**:
  - Use `new UtcDateTimeConverter()` directly to test conversion logic
  - Verify `DateTimeKind.Utc` is applied to all read values
  - Ensure no data loss on conversion (fractional seconds, etc.)

---

### TASK-076: Integration Test — Full CRUD Cycle

- **Layer**: CarePath.Infrastructure.Tests
- **Dependencies**: TASK-042, TASK-059, TASK-060, TASK-070 through TASK-075
- **Estimate**: 1.5 hours
- **Priority**: High
- **Success Criteria**:
  - `FullCrudIntegrationTests.cs` created in `Infrastructure.Tests/Persistence/`
  - Tests full lifecycle: Create → Read → Update → Soft Delete
  - Tests demonstrate Repository and UnitOfWork working together
  - Tests verify soft delete prevents retrieval
- **Files**:
  - CREATE: `Infrastructure.Tests/Persistence/FullCrudIntegrationTests.cs`
- **Test Cases**:
  - Add new User via Repository.AddAsync
  - SaveChangesAsync persists
  - Retrieve via GetByIdAsync
  - Update User properties via UpdateAsync
  - Delete via DeleteAsync (sets IsDeleted)
  - Verify GetByIdAsync returns null after delete
  - Verify GetAllAsync excludes deleted entity
- **Implementation Notes**:
  - Use real UnitOfWork and Repository instances with in-memory DbContext
  - This test validates the entire persistence stack works end-to-end
  - Include a Caregiver or multi-entity relationship test to verify navigation properties

---

### TASK-077: Run Full Infrastructure Test Suite

- **Layer**: CarePath.Infrastructure.Tests
- **Dependencies**: TASK-070 through TASK-076
- **Estimate**: 0.5 hours
- **Priority**: High
- **Success Criteria**:
  - All tests in Infrastructure.Tests project pass
  - Code coverage > 80% for Infrastructure layer
  - No test warnings or failures
  - Test output is clean and informative
- **Commands**:
  ```bash
  dotnet test Infrastructure.Tests/Infrastructure.Tests.csproj
  dotnet test --collect:"XPlat Code Coverage" Infrastructure.Tests/Infrastructure.Tests.csproj
  ```
- **Success Criteria Details**:
  - ✅ All DbContext tests pass
  - ✅ All Repository tests pass
  - ✅ All UnitOfWork tests pass
  - ✅ All Entity Configuration tests pass
  - ✅ All Interceptor tests pass
  - ✅ All Converter tests pass
  - ✅ Integration tests pass
  - ✅ Coverage report shows >80% Infrastructure layer coverage
- **Implementation Notes**:
  - If any test fails, do not proceed to Phase 10 — fix failures and re-run
  - Review coverage report to identify untested code paths
  - Aim for high coverage on critical paths: Repository CRUD, UnitOfWork SaveChanges, audit interceptor

---

## Phase 10: Final Verification

### TASK-078: Full Build Verification and HIPAA Compliance Check

- **Layer**: Solution
- **Dependencies**: All prior phases
- **Estimate**: 1.5 hours
- **Priority**: Critical
- **Success Criteria**:
  - `dotnet build CarePath.sln` succeeds with zero errors and zero warnings
  - `dotnet test CarePath.sln` passes (all Domain + Infrastructure tests)
  - No sensitive data logged or leaked to URLs
  - No PHI in exception messages
  - HIPAA compliance patterns verified
  - Connection string uses encryption (Encrypt=True)
- **Commands**:
  ```bash
  dotnet build CarePath.sln --no-incremental
  dotnet test CarePath.sln --no-build
  ```
- **HIPAA Compliance Checklist**:
  - [ ] No patient names, SSNs, medical record numbers in logs
  - [ ] No PHI in exception messages (use generic error messages for end users)
  - [ ] No PHI in URL query strings or route parameters
  - [ ] Soft delete `IsDeleted` flag exists on all PHI entities (Client, Shift, VisitNote, VisitPhoto, CarePlan, CaregiverCertification)
  - [ ] Global query filter applied so soft-deleted records are auto-excluded
  - [ ] Cascade delete restricted on PHI entities (Restrict, not Cascade)
  - [ ] Connection string enforces encryption (Encrypt=True)
  - [ ] Audit fields present: CreatedBy, UpdatedBy (for user activity tracking)
  - [ ] AuditableEntityInterceptor captures user ID
  - [ ] No hard deletes on clinical data
- **Implementation Notes**:
  - Zero warnings is a hard requirement for production quality
  - Review any compiler warnings and fix or suppress with comments
  - HIPAA compliance is non-negotiable for healthcare data

---

## Success Criteria (Overall)

### Phase 1-3 Complete (Infrastructure & DbContext)
- ✅ Infrastructure project created with all folders and NuGet packages
- ✅ Infrastructure.Tests project created and references correct packages
- ✅ UtcDateTimeConverter created and handles DateTime/DateTime? conversion to UTC
- ✅ AuditableEntityInterceptor created and intercepts SaveChangesAsync to set audit fields
- ✅ CarePathDbContext created with all 12 entity DbSets
- ✅ ApplicationUser and ApplicationUserConfiguration created for Identity integration

### Phase 4 Complete (Entity Configurations)
- ✅ All 12 entity configurations created (TASK-047 through TASK-058)
- ✅ Each configuration sets table name, primary key, constraints, indexes, relationships, global query filter
- ✅ All string lengths enforced
- ✅ All decimal properties have precision (18,2)
- ✅ All DateTime properties use UTC converter
- ✅ All computed properties ignored
- ✅ Cascade delete behavior intentional (Restrict on PHI, Cascade on dependent entities)
- ✅ Global query filter applied to all entity configurations

### Phase 5 Complete (Repository & UnitOfWork)
- ✅ Generic Repository<T> implements IRepository<T> with all CRUD methods
- ✅ GetPagedAsync implemented for large datasets
- ✅ Soft delete working (DeleteAsync sets IsDeleted, deleted records excluded from queries)
- ✅ IUnitOfWork implemented with lazy-initialized repositories
- ✅ Transaction management working (BeginTransaction, Commit, Rollback)
- ✅ IRepository<T> interface updated with GetPagedAsync signature

### Phase 6 Complete (DI & Configuration)
- ✅ DependencyInjection.cs extension method created
- ✅ DbContext registered as scoped
- ✅ Identity registered with password policies
- ✅ AuditableEntityInterceptor registered
- ✅ IUnitOfWork registered
- ✅ WebApi Program.cs calls AddInfrastructure()
- ✅ appsettings.json has connection string with Encrypt=True
- ✅ Auto-migration at startup enabled (optional)

### Phase 7 Complete (Migrations)
- ✅ Initial migration generated without errors
- ✅ Migration reviewed and validated (correct schemas, constraints, indexes, cascade behaviors)
- ✅ No hard deletes on PHI entities in migration
- ✅ Migration applied to database successfully
- ✅ Database tables created with correct schemas
- ✅ Foreign keys and indexes verified in database

### Phase 8 Complete (Seed Data)
- ✅ Seed data configuration created
- ✅ Default admin user seeded
- ✅ Sample caregiver and client seeded (development only)
- ✅ Seed data idempotent (safe to run multiple times)
- ✅ Seed data applied at startup
- ✅ Seed data verified in database

### Phase 9 Complete (Testing)
- ✅ DbContext tests passing
- ✅ Repository tests passing
- ✅ UnitOfWork tests passing
- ✅ Entity Configuration tests passing
- ✅ Interceptor tests passing
- ✅ Converter tests passing
- ✅ Integration tests passing
- ✅ Code coverage > 80%
- ✅ All tests pass in full suite

### Phase 10 Complete (Verification)
- ✅ `dotnet build CarePath.sln` succeeds with zero errors and zero warnings
- ✅ `dotnet test CarePath.sln` passes
- ✅ HIPAA compliance checklist satisfied
- ✅ No sensitive data in logs or URLs
- ✅ Encryption enabled on connection string
- ✅ Soft delete and audit fields verified

---

## Dependencies Graph

```
TASK-040 (Infrastructure Project)
   ├─> TASK-041 (NuGet Packages)
   │    ├─> TASK-043 (UtcDateTimeConverter)
   │    ├─> TASK-044 (AuditableEntityInterceptor)
   │    └─> TASK-045 (CarePathDbContext)
   │         ├─> TASK-046 (ApplicationUser)
   │         │    └─> TASK-058 (ApplicationUserConfiguration)
   │         │         └─> TASK-047 through TASK-057 (Entity Configurations)
   │         │              └─> TASK-052 (ShiftConfiguration — most complex)
   │         │                   └─> TASK-059 (Repository<T>)
   │         │                       └─> TASK-060 (UnitOfWork)
   │         │                           └─> TASK-061 (Update IRepository<T>)
   │         │
   │         └─> TASK-062 (DependencyInjection.cs)
   │              └─> TASK-063 (WebApi Program.cs)
   │                   ├─> TASK-064 (appsettings.json)
   │                   ├─> TASK-065 (Generate InitialCreate Migration)
   │                   │    └─> TASK-066 (Review Migration)
   │                   │         └─> TASK-067 (Apply Migration)
   │                   │              ├─> TASK-068 (Seed Data Configuration)
   │                   │              │    └─> TASK-069 (Verify Seed Data)
   │                   │              │
   │                   │              └─> TASK-070 (DbContext Tests)
   │                   │                   ├─> TASK-071 (Repository Tests)
   │                   │                   ├─> TASK-072 (UnitOfWork Tests)
   │                   │                   ├─> TASK-073 (Entity Configuration Tests)
   │                   │                   ├─> TASK-074 (Interceptor Tests)
   │                   │                   ├─> TASK-075 (Converter Tests)
   │                   │                   └─> TASK-076 (Integration Tests)
   │                   │                        └─> TASK-077 (Run Full Test Suite)
   │                   │                             └─> TASK-078 (Final Verification)
   │
   └─> TASK-042 (Infrastructure.Tests Project)
        └─> (All tests depend on this)
```

---

## Critical Path

The longest dependency chain is:
```
TASK-040 → TASK-041 → TASK-044 → TASK-045 → TASK-052 → TASK-059 
→ TASK-060 → TASK-061 → TASK-062 → TASK-063 → TASK-065 → TASK-066 
→ TASK-067 → TASK-070 → TASK-076 → TASK-077 → TASK-078
```

**Critical Path Duration**: ~30-35 hours (77% of total estimate)

**Parallel Opportunities**:
- TASK-041 and TASK-042 can run in parallel
- TASK-043 and TASK-044 can run in parallel with TASK-045
- TASK-047 through TASK-057 can run in parallel (once TASK-045 is complete)
- TASK-070 through TASK-075 can run in parallel (once TASK-067 is complete)

---

## Out of Scope (Future Specs)

❌ **Not in CP-02**:
- Application layer (DTOs, Services, Validators, AutoMapper)
- API endpoints or controllers
- Authentication/Authorization logic (beyond Identity setup)
- MAUI mobile app or Blazor web UI
- Advanced features (logging, caching, notification services)
- Performance optimization or query tuning
- API documentation (Swagger/OpenAPI)

---

## Related Documents

- **[Domain Entities Tasks (CP-01)](./cp-01-create-domain-entities.md)** — Must be completed first
- **[Domain Entities Design (CP-01)](../02-design/cp-01-create-domain-entities.md)** — Entity schemas, relationships
- **[Migration Workflow Command](../../.claude/commands/migration.md)** — EF Core migration best practices
- **[CLAUDE.md](../../CLAUDE.md)** — Architecture, conventions, coding standards
- **[Architecture Documentation](../../Documentation/Architecture.md)** — System design

---

## Notes for Implementation

### Recommended Order of Execution

1. **Complete CP-01 (Domain Entities)** — Must be done first and merged
2. **Phase 1-3 (Setup & Core Infrastructure)** — Complete in sequence; enables parallel work
3. **Phase 4 (Entity Configurations)** — Can parallelize TASK-047 through TASK-057
4. **Phase 5-6 (Repositories & DI)** — Sequential, builds on Phase 4
5. **Phase 7 (Migrations)** — Must follow Phase 6; single logical unit
6. **Phase 8 (Seed Data)** — Can overlap with Phase 7
7. **Phase 9 (Testing)** — Can start once Phase 7 is complete; parallelize tests
8. **Phase 10 (Verification)** — Final gate before completion

### Team Coordination (if multiple developers)

- **Dev 1**: TASK-040 through TASK-046 (sequential, blocking)
- **Dev 2**: TASK-047 through TASK-058 (parallel, once Phase 3 complete)
- **Dev 1**: TASK-059 through TASK-067 (sequential after Phase 4)
- **Dev 1 + Dev 2**: TASK-070 through TASK-077 (parallel testing)
- **Dev 1**: TASK-078 (final verification, merge & review)

### Estimated Effort (Single Developer)

- Phase 1-3: 6.5 hours
- Phase 4: 12 hours
- Phase 5-6: 8 hours
- Phase 7: 4 hours
- Phase 8: 3 hours
- Phase 9: 14 hours
- Phase 10: 1.5 hours
- **Total: ~49 hours (1 week full-time)**

### Known Risks & Mitigations

| Risk | Mitigation |
|------|-----------|
| Entity configurations missing required constraints | Comprehensive Entity Configuration Tests (TASK-073) |
| Migration generates unexpected schema | Review migration file (TASK-066) before applying |
| Cascade delete on PHI entities causes data loss | Manual review + HIPAA checklist (TASK-078) |
| Soft delete not working (deleted records still retrieved) | Global query filter verification in tests (TASK-070, TASK-073) |
| Audit fields not captured | AuditableEntityInterceptor tests (TASK-074) |
| DateTime values not UTC | UtcDateTimeConverter tests (TASK-075) + entity config tests |

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-02-25 | Tobi Kareem | Initial tasks breakdown (40 tasks, 10 phases, ~49 hours) — Status: Draft |

