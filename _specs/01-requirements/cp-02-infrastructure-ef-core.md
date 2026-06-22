# Requirements Specification: CP-02 Infrastructure / EF Core

**Date**: 2026-02-25
**Author**: Tobi Kareem
**Project**: CarePath Health
**Status**: Draft
**Related Specs**:
- [CP-01 Design Spec](../02-design/cp-01-create-domain-entities.md) - Domain entities and interfaces (Approved)
- [CP-02 Design Spec](../02-design/cp-02-infrastructure-ef-core.md) - Infrastructure design (to be created)
- [CP-02 Tasks Spec](../03-tasks/cp-02-infrastructure-ef-core.md) - Implementation tasks (to be created after this is approved)
- [CLAUDE.md](../../CLAUDE.md) - Coding conventions and architecture rules
- [Architecture.md](../../Documentation/Architecture.md) - System architecture overview

---

## Executive Summary

> Implement the Infrastructure layer for CarePath Health using Entity Framework Core 9 and SQL Server, creating a robust foundation for persistent data storage with proper entity configurations, audit trail support, soft delete enforcement, global query filters, and repository/unit-of-work implementations that enforce domain rules and ensure HIPAA compliance through encryption, proper cascade behaviors, and no hard deletes on clinical data.

---

## 1. Problem Statement

CarePath Health's Domain layer (CP-01) defines 12 entities, 7 enumerations, and repository interfaces, but has zero implementation. The Infrastructure layer is required to:

1. **Persist the domain model** into SQL Server via EF Core 9
2. **Enforce database constraints and relationships** that match the domain design
3. **Protect PHI (Protected Health Information)** through soft deletes, encryption at rest, and audit logging
4. **Prevent data loss** by restricting cascade deletes on clinical entities
5. **Ensure consistency** through global query filters (IsDeleted == false) and UTC value converters
6. **Enable transactional operations** through unit-of-work pattern and repository implementations
7. **Support development and testing** with seed data, proper migrations, and dependency injection registration

Without this layer, the domain entities cannot be tested in integration scenarios, no data can be persisted, and HIPAA compliance requirements cannot be enforced at the database level.

---

## 2. User Stories (Gherkin Format)

### Story 1: Database Initialization and Configuration

```gherkin
Feature: EF Core DbContext and Entity Configurations
  As a developer
  I want a properly configured DbContext with Fluent API entity mappings
  So that domain entities are correctly mapped to SQL Server tables

  Scenario: Create CarePathDbContext with all entity DbSets
    Given a new ASP.NET Core 9 project with EF Core 9 installed
    When I create CarePathDbContext : DbContext
    Then it should contain DbSet<T> for all 12 entities
    And all DbSets should use generic IRepository<T> pattern

  Scenario: Apply Fluent API configurations to all entities
    Given CarePathDbContext is created
    When OnModelCreating is called
    Then each entity should be configured with:
      - Correct table name (PascalCase plural: Users, Caregivers, Clients, Shifts, etc.)
      - Primary key as Guid
      - Proper foreign key relationships and constraints
      - Cascade delete behavior (Restrict on PHI entities)
      - Decimal precision (18,2) for monetary values
      - String length limits (not nvarchar(max) everywhere)
      - UTC value converter on all DateTime properties

  Scenario: Configure relationships correctly
    Given domain entities define 1:1, 1:many, and optional relationships
    When Fluent API is applied
    Then relationships should be properly configured:
      - User 1:1 Caregiver (via UserId FK)
      - User 1:1 Client (via UserId FK)
      - Caregiver 1:many Shifts (via CaregiverId FK, nullable)
      - Caregiver 1:many VisitNotes (via CaregiverId FK)
      - Caregiver 1:many CaregiverCertifications (via CaregiverId FK)
      - Client 1:many Shifts (via ClientId FK)
      - Client 1:many CarePlans (via ClientId FK)
      - Client 1:many Invoices (via ClientId FK)
      - Shift 1:many VisitNotes (via ShiftId FK)
      - VisitNote 1:many VisitPhotos (via VisitNoteId FK)
      - Invoice 1:many InvoiceLineItems (via InvoiceId FK)
      - Invoice 1:many Payments (via InvoiceId FK)
      - InvoiceLineItem optional:1 Shift (via ShiftId FK, nullable)
```

### Story 2: HIPAA Compliance and Soft Deletes

```gherkin
Feature: Global Query Filter and Soft Delete Enforcement
  As a compliance officer
  I want all soft-deleted records to be automatically excluded from queries
  So that PHI compliance is enforced at the database level

  Scenario: Apply global query filter to all entities
    Given CarePathDbContext OnModelCreating is configured
    When a global query filter is applied
    Then all queries should automatically exclude records where IsDeleted == true
    And no code path can accidentally query deleted records

  Scenario: Enforce no hard deletes on PHI entities
    Given a repository implementation for a PHI entity (Client, CarePlan, Shift, VisitNote, VisitPhoto, CaregiverCertification)
    When DeleteAsync is called
    Then the entity should be soft-deleted (IsDeleted = true)
    And the original record should remain in the database unchanged
    And no UPDATE that sets IsDeleted should trigger a cascade delete

  Scenario: Use DeleteBehavior.Restrict on PHI entity relationships
    Given relationships from PHI entities to non-PHI entities
    When OnModelCreating configures these relationships
    Then cascade delete should be Restrict (not Cascade)
    And attempting to delete a Client with active Shifts should fail with constraint error

  Scenario: Test that non-PHI entities (User, Caregiver) can still be deleted
    Given User and Caregiver are not strictly PHI (they are linked from PHI entities)
    When their cascade delete behavior is configured
    Then it should follow domain design (typically Cascade to dependent records, but never hard-delete in code)
```

### Story 3: DateTime UTC Handling

```gherkin
Feature: UTC DateTime Value Converter
  As a developer
  I want all DateTime properties to be stored and retrieved as UTC
  So that timezone confusion is prevented and HIPAA audit logs are consistent

  Scenario: Apply UTC value converter to all DateTime properties
    Given all domain entities use DateTime for timestamps (CreatedAt, UpdatedAt, ScheduledStartTime, etc.)
    When entity type configurations are created
    Then each DateTime property should have:
      - SQL Server column type: datetime2 (preserves precision)
      - EF Core conversion: DateTime.SpecifyKind(value, DateTimeKind.Utc) on reads
      - Stored as UTC in the database

  Scenario: Verify DateTime round-trip preserves UTC kind
    Given a Shift with CreatedAt = new DateTime(2026, 2, 25, 10, 30, 0, DateTimeKind.Utc)
    When the Shift is saved and retrieved from the database
    Then the retrieved CreatedAt should have DateTimeKind.Utc
    And DateTimeKind.Unspecified is never returned to the application
```

### Story 4: Repository Implementation

```gherkin
Feature: Generic Repository<T> Implementation
  As a developer
  I want a generic repository implementation that provides CRUD and query operations
  So that data access is consistent and testable

  Scenario: Implement IRepository<T> for all entity types
    Given IRepository<T> interface exists in Domain
    When Repository<T> is implemented in Infrastructure
    Then it should support:
      - GetByIdAsync(Guid id)
      - GetAllAsync() - returns IReadOnlyList<T>
      - FindAsync(Expression<Func<T, bool>> predicate) - returns IReadOnlyList<T>
      - AddAsync(T entity)
      - UpdateAsync(T entity)
      - DeleteAsync(T entity) - soft-delete only
      - ExistsAsync(Expression<Func<T, bool>> predicate)
      - CountAsync(Expression<Func<T, bool>>? predicate = null)
      - GetPagedAsync(int pageNumber, int pageSize) [TASK-019a]

  Scenario: Ensure soft delete is enforced in repository
    Given DeleteAsync is called on a domain entity
    When the method executes
    Then it should:
      - Set entity.IsDeleted = true
      - Call UpdateAsync instead of DbSet.Remove()
      - Persist the change to the database
      - Never call DbContext.SaveChanges() within the repository (UoW pattern)

  Scenario: Return IReadOnlyList, not IEnumerable
    Given GetAllAsync or FindAsync is called
    When the query executes
    Then the result should be IReadOnlyList<T>
    And the collection should be fully materialized (not deferred)
    And callers cannot append additional LINQ operators
```

### Story 5: Unit of Work Implementation

```gherkin
Feature: Unit of Work Pattern for Transactional Operations
  As a developer
  I want a unit of work that groups repositories and manages transactions
  So that multiple entity updates can succeed or fail together

  Scenario: Implement IUnitOfWork interface
    Given IUnitOfWork interface exists in Domain with 11 repository properties
    When UnitOfWork class is created
    Then it should:
      - Implement IDisposable and IAsyncDisposable for proper EF Core cleanup
      - Expose IRepository<T> properties for all 11 entity types:
        - Users, Caregivers, CaregiverCertifications, Clients
        - CarePlans, Shifts, VisitNotes, VisitPhotos
        - Invoices, InvoiceLineItems, Payments
      - Provide SaveChangesAsync(CancellationToken) to persist all changes
      - Support transactional operations via:
        - BeginTransactionAsync()
        - CommitTransactionAsync()
        - RollbackTransactionAsync()

  Scenario: Track changes across multiple repositories
    Given a UnitOfWork instance with multiple repositories accessed
    When multiple entities are created/updated via different repositories
    Then SaveChangesAsync should persist all changes in a single DbContext batch
    And audit fields (CreatedBy, UpdatedBy) should be populated before save

  Scenario: Support transaction control
    Given a UnitOfWork with an active business transaction
    When BeginTransactionAsync, multiple operations, then CommitTransactionAsync are called
    Then all changes should be committed atomically
    And RollbackTransactionAsync should discard all pending changes if called instead
```

### Story 6: Audit Trail and Change Tracking

```gherkin
Feature: Audit Logging for HIPAA Compliance
  As a compliance officer
  I want every change to PHI entities to be logged with user and timestamp
  So that audit trails are available for HIPAA investigations

  Scenario: Populate audit fields on entity creation
    Given a new entity is added to the database
    When SaveChangesAsync is called
    Then audit fields should be populated:
      - CreatedAt: DateTime.UtcNow (set by BaseEntity default)
      - CreatedBy: Current user ID (must be provided by application)
      - UpdatedAt: null (not yet updated)
      - UpdatedBy: null (not yet updated)

  Scenario: Populate audit fields on entity update
    Given an existing entity is modified
    When SaveChangesAsync is called
    Then audit fields should be updated:
      - CreatedAt: unchanged
      - CreatedBy: unchanged
      - UpdatedAt: DateTime.UtcNow
      - UpdatedBy: Current user ID

  Scenario: Never expose PHI in audit logs
    Given audit logging is implemented via SaveChangesInterceptor
    When entities with PHI (Client name, SSN, diagnosis, address) are logged
    Then log entries should NOT contain:
      - Patient/client names
      - Social security numbers
      - Medical diagnoses
      - Full addresses
      - Phone numbers
      - Insurance numbers
    And log entries should only contain:
      - UserId (who made the change)
      - Timestamp (when)
      - Action (Create, Update, Delete)
      - EntityType (e.g., "Client")
      - EntityId (Guid - no PII)
```

### Story 7: Database Initialization and Seeding

```gherkin
Feature: Initial Migration and Seed Data
  As a developer
  I want to create initial database schema and seed development data
  So that the application can run immediately after database creation

  Scenario: Create initial migration with all 12 entities
    Given CarePathDbContext is configured with entity models
    When dotnet ef migrations add InitialCreate is executed
    Then the migration should:
      - Create all 12 entity tables (Users, Caregivers, Clients, Shifts, VisitNotes, etc.)
      - Include proper indexes on FK columns
      - Include proper indexes on frequently queried columns (Status, ServiceType, IsDeleted)
      - Create all constraints (PKs, FKs, unique constraints on Email)
      - Support encryption at rest via SQL Server TDE configuration

  Scenario: Seed default admin user
    Given database is initialized
    When application starts for the first time
    Then a default Admin user should be created:
      - Email: admin@carepath.local (or configurable)
      - FirstName: System
      - LastName: Administrator
      - Role: Admin
      - IsActive: true
      - Password: Random/configurable (per environment)

  Scenario: Seed sample development data (development environment only)
    Given the application is running in Development environment
    When the database is seeded
    Then sample data should be created:
      - 5 test Caregivers with varied EmploymentTypes and certifications
      - 10 test Clients with varying ServiceTypes
      - 20 sample Shifts across the caregivers and clients
      - Sample VisitNotes and CarePlans
      - Sample Invoices with LineItems and Payments
    And no sample data should be seeded in Production
```

### Story 8: Encryption at Rest Configuration

```gherkin
Feature: SQL Server Transparent Data Encryption (TDE)
  As a security officer
  I want sensitive data to be encrypted at rest in SQL Server
  So that PHI is protected even if the database file is compromised

  Scenario: Configure TDE in connection string
    Given the application is deployed to production
    When the DbContext connects to SQL Server
    Then the connection string should support TDE:
      - "Encrypt=true; TrustServerCertificate=false;" (production)
      - Or SQL Server TDE enabled on the database itself

  Scenario: Document encryption requirements
    Given CarePath Health handles PHI
    When infrastructure documentation is created
    Then it should specify:
      - SQL Server Transparent Data Encryption must be enabled
      - Or Azure SQL Database with Transparent Data Encryption enabled
      - Keys should be managed via Azure Key Vault (if Azure)
      - Annual key rotation policy
```

### Story 9: Dependency Injection Registration

```gherkin
Feature: Infrastructure Service Registration
  As a developer
  I want all Infrastructure services registered in DI container
  So that the application can request repositories and UnitOfWork without manual instantiation

  Scenario: Register DbContext and repositories
    Given a DependencyInjection.cs registration method in Infrastructure
    When services.AddInfrastructure() is called from Startup
    Then the following should be registered:
      - DbContext: services.AddDbContext<CarePathDbContext>(...)
      - UnitOfWork: services.AddScoped<IUnitOfWork, UnitOfWork>()
      - Generic Repository: services.AddScoped(typeof(IRepository<>), typeof(Repository<>))
      - SaveChangesInterceptor for audit logging
      - Any external service integrations (email, SMS, storage, etc.)

  Scenario: Support multiple environments
    Given a Development vs Production environment
    When DependencyInjection is called
    Then it should:
      - Use local SQL Server in Development
      - Use Azure SQL Database in Production
      - Enable lazy loading (Development only)
      - Disable sensitive logging in Production
```

### Story 10: Paging Support for Large Tables

```gherkin
Feature: GetPagedAsync for Shift and VisitNote Tables
  As a developer
  I want to efficiently query large tables without loading all records
  So that performance remains acceptable as Shift and VisitNote grow

  Scenario: Add GetPagedAsync to IRepository<T>
    Given IRepository<T> interface in Domain
    When TASK-019a is implemented
    Then GetPagedAsync(int pageNumber, int pageSize, CancellationToken) should be added
    And return: Task<(IReadOnlyList<T> items, int totalCount)>

  Scenario: Implement paging in Repository<T>
    Given GetPagedAsync signature is defined
    When Repository<T> implements it
    Then it should:
      - Support 1-based page numbering
      - Calculate correct SKIP/TAKE offsets
      - Execute COUNT query to get total records
      - Return both items and total count
      - Apply global query filter (IsDeleted == false)
      - Be used by Shift and VisitNote queries in Application layer
```

---

## 3. Functional Requirements

| ID | Requirement | Priority | Notes |
|---|---|---|---|
| FR-001 | Create CarePathDbContext inheriting DbContext with DbSet<> for all 12 entities | Critical | Must include: User, Caregiver, CaregiverCertification, Client, CarePlan, Shift, VisitNote, VisitPhoto, Invoice, InvoiceLineItem, Payment |
| FR-002 | Configure all entities via Fluent API in OnModelCreating | Critical | Tables: Users, Caregivers, CaregiverCertifications, Clients, CarePlans, Shifts, VisitNotes, VisitPhotos, Invoices, InvoiceLineItems, Payments |
| FR-003 | Set primary key to Guid for all entities | Critical | No int or auto-increment; every PK must be Guid |
| FR-004 | Configure foreign key relationships (1:1, 1:many, optional) | Critical | Includes nullable FKs (CaregiverId on Shift, ShiftId on InvoiceLineItem) |
| FR-005 | Apply cascade delete behavior: Cascade on non-PHI, Restrict on PHI | Critical | PHI entities: Client, CarePlan, Shift, VisitNote, VisitPhoto, CaregiverCertification |
| FR-006 | Set decimal precision (18,2) for monetary columns | High | BillRate, PayRate, Amount, Total, Balance, etc. |
| FR-007 | Define string length limits for properties (not nvarchar(max)) | High | Email (256), PhoneNumber (20), FirstName/LastName (100), etc. |
| FR-008 | Create UTC value converter for all DateTime properties | Critical | Ensures DateTimeKind.Utc on all reads; prevents timezone issues |
| FR-009 | Apply global query filter IsDeleted == false | Critical | Automatically excludes soft-deleted records from all queries |
| FR-010 | Implement Repository<T> with GetByIdAsync, GetAllAsync, FindAsync, AddAsync, UpdateAsync, DeleteAsync | Critical | Returns IReadOnlyList<T>; soft-delete only in DeleteAsync |
| FR-011 | Implement soft-delete in repository (set IsDeleted = true) | Critical | Never call DbSet.Remove(); always set IsDeleted |
| FR-012 | Add ExistsAsync and CountAsync to repository | High | Used by application validation and analytics |
| FR-013 | Implement IUnitOfWork with 11 repository properties | Critical | Users, Caregivers, CaregiverCertifications, Clients, CarePlans, Shifts, VisitNotes, VisitPhotos, Invoices, InvoiceLineItems, Payments |
| FR-014 | Implement SaveChangesAsync in UnitOfWork | Critical | Persists all repository changes in single DbContext batch |
| FR-015 | Implement transaction support (BeginTransactionAsync, CommitTransactionAsync, RollbackTransactionAsync) | High | For atomic multi-entity updates |
| FR-016 | Implement IDisposable and IAsyncDisposable on UnitOfWork | Critical | Required for proper EF Core DbContext disposal |
| FR-017 | Create SaveChangesInterceptor for audit trail logging | High | Logs CreatedBy, UpdatedBy, timestamps on all changes; NO PHI in logs |
| FR-018 | Populate CreatedAt, CreatedBy on entity creation | High | Automatic via SaveChangesInterceptor |
| FR-019 | Populate UpdatedAt, UpdatedBy on entity modification | High | Automatic via SaveChangesInterceptor |
| FR-020 | Create initial EF Core migration with all 12 tables | Critical | Migration must create schema, indexes, constraints |
| FR-021 | Create indexes on foreign key columns | High | Improves query performance on Shift.CaregiverId, VisitNote.ShiftId, etc. |
| FR-022 | Create indexes on status/enum columns | High | Shift.Status, Invoice.Status, Client.ServiceType queries will filter by these |
| FR-023 | Create unique constraint on User.Email | High | Email must be unique for authentication |
| FR-024 | Seed default Admin user on database initialization | High | Email: admin@carepath.local (configurable), Role: Admin |
| FR-025 | Seed development sample data when environment is Development | Medium | 5 Caregivers, 10 Clients, 20 Shifts, VisitNotes, Invoices (dev only) |
| FR-026 | Register DbContext in DI container | Critical | services.AddDbContext<CarePathDbContext>(...) |
| FR-027 | Register IRepository<T> and IUnitOfWork in DI container | Critical | services.AddScoped<IUnitOfWork, UnitOfWork>() |
| FR-028 | Register SaveChangesInterceptor in DI container | High | Must be added to DbContextOptions |
| FR-029 | Support multiple environments (Dev, Staging, Production) | High | Different connection strings, logging, data seeding per environment |
| FR-030 | Document encryption at rest requirements (SQL Server TDE) | Medium | Part of deployment guide; Encrypt=true in connection string |
| FR-031 | Add GetPagedAsync(int pageNumber, int pageSize) to IRepository<T> (TASK-019a) | High | For efficient pagination of Shift and VisitNote tables |
| FR-032 | Implement GetPagedAsync in Repository<T> | High | Returns (IReadOnlyList<T> items, int totalCount) |
| FR-033 | Apply global query filter to IsDeleted on GetPagedAsync | High | Paging must also exclude soft-deleted records |
| FR-034 | Create ApplicationUser : IdentityUser<Guid> with DomainUserId link as Identity schema foundation | High | ApplicationUser entity, configuration, and IdentityDbContext inheritance are in-scope for CP-02; authentication/authorization logic is deferred |

---

## 4. Non-Functional Requirements

| Category | Requirement |
|---|---|
| **Performance** | GetAllAsync on Shift/VisitNote must not load >1000 records without explicit filtering; implement GetPagedAsync to prevent memory exhaustion |
| **Scalability** | Repository pattern isolates data access, allowing future changes to query strategy without affecting application |
| **Reliability** | Transactions ensure atomic multi-entity updates; no partial writes |
| **Security** | Encryption at rest via SQL Server TDE; no PHI in application logs; audit trail logged for compliance |
| **Testability** | IRepository<T> and IUnitOfWork interfaces enable mocking in Application tests; no DbContext dependency in domain |
| **Maintainability** | Fluent API configurations in separate EntityTypeConfiguration<T> classes keep DbContext clean |
| **Usability** | DI registration automatic; developers use IUnitOfWork injected into services, not DbContext directly |
| **Auditability** | SaveChangesInterceptor logs all PHI changes; CreatedBy/UpdatedBy track user actions per HIPAA audit trail requirements |

---

## 5. HIPAA & Compliance Requirements

### 5.1 Data Protection

| Requirement | Implementation |
|---|---|
| **Encryption at Rest** | SQL Server Transparent Data Encryption (TDE) enabled on database; connection string specifies Encrypt=true |
| **No PHI in Logs** | SaveChangesInterceptor logs only: UserId, Timestamp, Action (Create/Update/Delete), EntityType, EntityId — never logs patient names, DOB, addresses, diagnoses, SSNs |
| **No PHI in URLs** | Infrastructure layer does not expose entity IDs in query strings; API layer enforces authorization checks |
| **Soft Delete Enforcement** | Global query filter + soft-delete-only DeleteAsync prevents accidental hard deletes of clinical data |
| **Audit Trail** | Every read, write, update of PHI entity is logged with CreatedBy/UpdatedBy timestamps; 6-year retention via soft deletes |

### 5.2 Cascade Delete Policy

| Entity Type | Cascade Delete Behavior | Rationale |
|---|---|---|
| **PHI Entities** (Client, CarePlan, Shift, VisitNote, VisitPhoto, CaregiverCertification) | **Restrict** | Cannot be deleted; data must be retained 6 years per Maryland law; soft delete only |
| **Non-PHI Entities** (User, Caregiver) | **Cascade** (with soft delete in code) | Supports cleanup of terminated users/caregivers, but never hard-deletes; always set IsDeleted=true |
| **Financial Entities** (Invoice, InvoiceLineItem, Payment) | **Restrict** (from Client) / **Cascade** (LineItems/Payments from Invoice) | Invoices restricted from Client deletion; LineItems and Payments cascade with their parent Invoice; all use soft delete in code |

### 5.3 Data Retention

| Entity | Retention Period | Implementation |
|---|---|---|
| Client / CarePlan / VisitNote / VisitPhoto / CaregiverCertification | 6 years minimum | Soft delete only; IsDeleted=true but record remains in database |
| Shift | 6 years minimum | Soft delete only; linked from VisitNote and Invoice |
| Invoice / Payment | 6 years minimum | Soft delete only; required for financial audit trail |
| User / Caregiver | At least 6 years | Soft delete only; audit trails depend on CreatedBy/UpdatedBy |

---

## 6. Success Criteria

### Phase 1: Core Infrastructure (Approved for Implementation)

- [x] **CarePathDbContext created** with DbSet<> for all 12 entities
- [x] **Fluent API configurations** applied to all entities (table names, PKs, FKs, indexes, string/decimal precision)
- [x] **UTC value converters** configured on all DateTime properties
- [x] **Global query filter** (IsDeleted == false) applied to all entities
- [x] **Repository<T> implementation** with 8 core CRUD methods + soft delete enforcement
- [x] **IUnitOfWork implementation** with 11 repository properties, SaveChangesAsync, and transaction support
- [x] **SaveChangesInterceptor** for audit trail logging (no PHI in logs)
- [x] **Initial migration** created with all tables, indexes, constraints
- [x] **Seed data** (Admin user + dev sample data)
- [x] **DependencyInjection.cs** with DbContext, UnitOfWork, Repository, Interceptor registration
- [x] **Zero compiler warnings** and all tests pass
- [x] **Documentation** updated (Architecture.md, migration guide)

### Phase 2: Advanced Query Support (Future)

- [ ] Multi-field sorting via IQueryable extensions
- [ ] Specification pattern for complex queries
- [ ] Include/ThenInclude eager loading helpers

### Phase 3: Identity Authentication & Authorization (Future - separate CP-XX spec)

- [ ] JWT token generation and validation
- [ ] Login/logout API endpoints
- [ ] Role-based authorization on controllers
- [ ] Note: ApplicationUser schema and IdentityDbContext are created in CP-02 as foundation

---

## 7. Scope & Boundaries

### In Scope for CP-02

- DbContext creation and configuration for all 12 entities
- Fluent API entity type configurations (table names, relationships, constraints, indexes)
- UTC value converters on all DateTime properties
- Global query filter for soft deletes
- Repository<T> implementation (8 core methods)
- IUnitOfWork implementation (11 repositories + transaction support)
- SaveChangesInterceptor for audit logging
- Initial migration with all tables and indexes
- Seed data (Admin user + dev sample data)
- Dependency injection registration
- Documentation (CLAUDE.md updates, migration guide)
- Unit tests for repository methods
- Integration tests for entity mappings and soft deletes

### Out of Scope for CP-02

- ~~ASP.NET Core Identity authentication/authorization logic~~ (JWT, login endpoints, role-based auth — deferred to separate CP-XX spec; only the Identity schema foundation is in CP-02 scope)
- ~~Advanced query filtering (sorting, multi-field filtering)~~ (deferred to Application layer)
- ~~Specific Application layer services~~ (Domain → Application transition)
- ~~API controllers and endpoints~~ (API layer is separate)
- ~~External service integrations~~ (Email, SMS, Payment, Storage — handled in separate infrastructure specs)
- ~~Data encryption at field level~~ (SQL Server TDE handles at-rest; field-level encryption deferred to Phase 3)
- ~~Change tracking/event sourcing~~ (deferred to Phase 3)

---

## 8. Dependencies

### Required (Must be completed before CP-02 implementation)

1. **CP-01: Domain Entities** (Approved)
   - All 12 entities must be implemented and tested
   - All 7 enumerations must be defined
   - IRepository<T> and IUnitOfWork interfaces must exist in Domain

2. **.NET 9 + EF Core 9** installed and configured
   - `Microsoft.EntityFrameworkCore.SqlServer` NuGet package
   - `Microsoft.EntityFrameworkCore.Design` for migrations CLI

3. **SQL Server** (local or Azure)
   - Development: SQL Server 2019+ local instance or Express
   - Production: Azure SQL Database or managed SQL Server

### Related (May be needed as CP-02 progresses)

- ASP.NET Core 9 WebApi project (for dependency injection and `Program.cs`)
- Serilog logging configuration (for audit logging)
- Application layer DTOs (for testing repository return types)

---

## 9. Risks & Mitigation

| Risk | Impact | Mitigation |
|---|---|---|
| **DateTime round-trip loses UTC kind** | Audit logs show wrong timestamps; timezone issues in production | Use UTC value converter on all DateTime properties (FR-008); test round-trip explicitly |
| **Hard delete accidentally called** | PHI data lost; HIPAA violation; audit trail incomplete | Implement soft delete only in DeleteAsync; global query filter prevents queries on deleted records; code review ensures no DbSet.Remove() calls |
| **Cascade deletes violate HIPAA** | PHI accidentally deleted; audit trail lost | Set cascade delete to Restrict on PHI entities (FR-005); code review + integration tests verify constraint violations |
| **PHI leaked in application logs** | HIPAA violation; PII exposure; security breach | SaveChangesInterceptor never logs patient names, SSNs, diagnoses; log only UserId, EntityType, EntityId (FR-017) |
| **GetAllAsync on Shift/VisitNote causes OOM** | Production crash; data unavailable | Implement GetPagedAsync immediately (TASK-019a); add repository method that enforces filtering on large tables |
| **Encryption at rest not enabled** | Database files vulnerable if compromised | Document TDE requirement; include connection string config in deployment guide (FR-030) |
| **UnitOfWork not disposed properly** | DbContext connection leaks; memory issues | Implement IDisposable and IAsyncDisposable; use `using` statement in consuming code; integration tests verify disposal |
| **Migration conflicts if multiple developers work on schema** | Merge conflicts; migration failures | One developer per infrastructure task; migrations reviewed before commit; CI/CD runs migrations on every build |

---

## 10. Acceptance Criteria

### Definition of Done

A requirement is complete when:

1. **Code passes compilation** — zero warnings, builds cleanly with `dotnet build CarePath.sln`
2. **Tests pass** — unit tests for repository methods, integration tests for entity mappings
3. **Soft delete verified** — test confirms DeleteAsync sets IsDeleted=true, not hard delete
4. **UTC converter tested** — test confirms DateTime round-trip preserves DateTimeKind.Utc
5. **Global filter tested** — query after soft delete returns 0 results
6. **No PHI in logs** — SaveChangesInterceptor logs reviewed; never logs patient PII
7. **Migration tested** — `dotnet ef database update` creates all tables and constraints
8. **Seed data tested** — Default Admin user and dev sample data verified
9. **DI registration tested** — IUnitOfWork injected successfully; no null reference exceptions
10. **Documentation complete** — CLAUDE.md updated with migration instructions; Architecture.md references this spec
11. **Code review approved** — dotnet-code-reviewer subagent confirms no architecture violations
12. **Ready for Application layer** — No blocking issues; Application services can depend on IRepository<T> and IUnitOfWork

---

## 11. Estimated Timeline

- **Planning & Design**: 2-4 hours (read CP-01, review CLAUDE.md, confirm approach)
- **DbContext & Entity Configurations**: 8-12 hours
- **Repository Implementation**: 6-8 hours
- **UnitOfWork Implementation**: 4-6 hours
- **SaveChangesInterceptor & Audit Logging**: 4-6 hours
- **Initial Migration & Seed Data**: 4-6 hours
- **Dependency Injection**: 2-3 hours
- **Testing (Unit + Integration)**: 8-12 hours
- **Documentation & Code Review**: 3-4 hours
- **Total**: ~45-60 hours (1-2 weeks for one developer working full-time)

---

## 12. Related Documents

- **[CP-01 Requirements Spec](../01-requirements/cp-01-create-domain-entities.md)** — Domain entity requirements
- **[CP-01 Design Spec](../02-design/cp-01-create-domain-entities.md)** — Entity designs with code samples
- **[Architecture.md](../../Documentation/Architecture.md)** — Full system architecture
- **[CLAUDE.md](../../CLAUDE.md)** — Coding conventions, HIPAA requirements, testing standards
- **[EF Core Official Docs](https://learn.microsoft.com/en-us/ef/core/)** — Microsoft EF Core documentation

---

## Revision History

| Version | Date | Author | Status | Changes |
|---------|------|--------|--------|---------|
| 1.0 | 2026-02-25 | Tobi Kareem | **Draft** | Initial comprehensive requirements spec for Infrastructure/EF Core layer |

---

**Next Step**: Once this spec is approved, the Design Spec (CP-02 Design) will be created with detailed Fluent API configuration code samples. After design approval, implementation tasks (CP-02 Tasks) will be created and development will begin.
