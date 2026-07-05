# Sprint 2 Requirements - Infrastructure Foundation

Date: 2026-06-27
Author: CarePath Health
Status: In Review
Sprint: Sprint 2
Primary spec: CP-02 Infrastructure / EF Core

Related specs:
- `_specs/sprints/sprint-02-infrastructure-foundation.md`
- `_specs/01-requirements/cp-02-infrastructure-ef-core.md`
- `_specs/02-design/cp-02-infrastructure-ef-core.md`
- `_specs/03-tasks/cp-02-infrastructure-ef-core.md`

## Executive Summary

Sprint 2 creates the HIPAA-aware persistence foundation for CarePath by adding the Infrastructure project, EF Core DbContext, SQL Server mappings, soft-delete enforcement, audit field population, repositories, Unit of Work, migrations, synthetic seed data, and Infrastructure tests.

## Problem Statement

CarePath has Domain entities and repository interfaces, but no durable persistence layer. Without Sprint 2, Application, API, web, mobile, and Transitions workflows cannot safely store or retrieve data, and PHI safeguards cannot be verified at the database boundary.

Sprint 2 solves three baseline risks:

- Persistence risk: Domain objects cannot be saved, queried, migrated, or tested against SQL Server.
- Compliance risk: PHI could be hard-deleted, cascade-deleted, queried after soft deletion, or logged without consistent controls.
- Implementation risk: later sprints would invent ad hoc data access instead of using a tested repository/UnitOfWork boundary.

## Story Requirements

### Story 1 - Create the Infrastructure project baseline

```gherkin
Feature: Infrastructure project setup
  Scenario: Developer starts Sprint 2 implementation
    Given CP-01 Domain is complete
    And Sprint 1 spec hygiene is complete
    When the developer creates the Infrastructure project
    Then the project targets .NET 9
    And it references Domain only
    And it contains Persistence, Configurations, Interceptors, Converters, Repositories, Identity, and Migrations folders
    And `dotnet build CarePath.sln` passes with zero warnings
```

Acceptance:
- [ ] `Infrastructure/Infrastructure.csproj` exists and references `Domain/Domain.csproj`.
- [ ] `Infrastructure.Tests/Infrastructure.Tests.csproj` exists and references Infrastructure and Domain.
- [ ] EF Core, SQL Server, Identity, InMemory/Test packages are installed with compatible .NET 9 versions.
- [ ] Infrastructure does not reference Application, WebApi, Web, or Mobile.

### Story 2 - Persist CP-01 and CP-03 Domain entities safely

```gherkin
Feature: EF Core persistence mapping
  Scenario: Saving healthcare operations data
    Given Domain entities exist for identity, clinical, scheduling, billing, and Transitions
    When CarePathDbContext is configured
    Then all supported entities have DbSet properties
    And all entity mappings use Fluent API
    And computed properties are ignored
    And database constraints match Domain nullability and relationships
```

Acceptance:
- [ ] `CarePathDbContext` lives at `Infrastructure/Persistence/CarePathDbContext.cs`.
- [ ] CP-01 entities are mapped: User, Caregiver, Client, CarePlan, Shift, VisitNote, VisitPhoto, Invoice, InvoiceLineItem, Payment, CaregiverCertification.
- [ ] CP-03 Transitions Domain entities are mapped only if their Domain classes are present in the current branch.
- [ ] `CarePlan` mapping uses the actual Domain path `Domain/Entities/Clinical/CarePlan.cs`.
- [ ] All configurations live under `Infrastructure/Persistence/Configurations/`.

### Story 3 - Enforce soft delete and PHI retention

```gherkin
Feature: Soft delete enforcement
  Scenario: A clinical record is deleted
    Given a PHI entity exists in the database
    When repository DeleteAsync is called
    Then IsDeleted is set to true
    And DbSet.Remove is not called
    And normal queries exclude the record
    And the row remains in the database for retention
```

Acceptance:
- [ ] Repository delete behavior sets `IsDeleted = true` for all `BaseEntity` types.
- [ ] Global query filters exclude `IsDeleted == true` records.
- [ ] Tests verify deleted records are excluded by normal repository queries.
- [ ] No implementation uses hard delete for Client, CarePlan, Shift, VisitNote, VisitPhoto, CaregiverCertification, or Transitions clinical entities.

### Story 4 - Prevent cascade deletes on PHI relationships

```gherkin
Feature: HIPAA-safe relationship behavior
  Scenario: A client has clinical child records
    Given a Client has CarePlan, Shift, VisitNote, VisitPhoto, and TransitionPlan records
    When EF Core relationships are configured
    Then PHI relationships use DeleteBehavior.Restrict
    And deleting a parent cannot cascade-delete clinical child data
```

Acceptance:
- [ ] Client to CarePlan, Shift, Invoice, and Transitions clinical relationships are reviewed for retention risk.
- [ ] VisitNote and VisitPhoto relationships do not cascade-delete PHI media or notes.
- [ ] Migration review confirms no unexpected cascade delete on PHI tables.
- [ ] Tests verify configured delete behavior for PHI relationships.

### Story 5 - Preserve UTC DateTime values

```gherkin
Feature: UTC date/time persistence
  Scenario: DateTime values round-trip through SQL Server
    Given an entity has DateTime and nullable DateTime properties
    When it is saved and reloaded through EF Core
    Then all returned DateTime values have DateTimeKind.Utc
    And no DateTime.Now values are introduced
```

Acceptance:
- [ ] UTC converters exist under `Infrastructure/Persistence/Converters/`.
- [ ] Every DateTime and DateTime? property is configured to round-trip as UTC.
- [ ] Tests cover non-nullable, nullable, unspecified-kind, and UTC-kind values.
- [ ] Audit timestamps use `DateTime.UtcNow`.

### Story 6 - Provide repository and UnitOfWork boundaries

```gherkin
Feature: Repository and UnitOfWork persistence boundary
  Scenario: Application services need persistence
    Given Application will depend on Domain repository interfaces
    When Infrastructure implements those interfaces
    Then Application can use IRepository<T> and IUnitOfWork without referencing EF Core
    And multiple repository changes can be saved transactionally
```

Acceptance:
- [ ] `Repository<T>` lives in `Infrastructure/Persistence/Repositories/`.
- [ ] `Repository<T>` implements Domain `IRepository<T>` with `where T : BaseEntity`.
- [ ] Repository collection reads return `IReadOnlyList<T>`.
- [ ] `GetPagedAsync` exists for high-volume tables and applies query filters.
- [ ] `UnitOfWork` exposes repository properties and transaction methods.
- [ ] Repository does not call `SaveChangesAsync`; UnitOfWork owns commit behavior.

### Story 7 - Populate audit fields without logging PHI

```gherkin
Feature: Audit field population
  Scenario: A user creates or updates a PHI entity
    Given the current user is available from the request context
    When SaveChangesAsync is called
    Then CreatedAt/CreatedBy or UpdatedAt/UpdatedBy are populated
    And audit implementation logs only metadata
    And patient names, DOB, diagnosis, address, note content, RawContent, and SourceText are never logged
```

Acceptance:
- [ ] `AuditableEntityInterceptor` sets audit fields for added and modified entities.
- [ ] Interceptor handles missing HttpContext for background/system operations.
- [ ] Logs include only user id, timestamp, action, entity type, and entity id.
- [ ] Tests verify audit fields and no exception when user context is absent.

### Story 8 - Generate and review the initial migration

```gherkin
Feature: Initial schema migration
  Scenario: Developer creates the database schema
    Given EF Core configurations are complete
    When InitialCreate migration is generated
    Then the migration creates expected tables, constraints, indexes, and Identity schema
    And the migration is reviewed for HIPAA-safe delete behavior
```

Acceptance:
- [ ] Initial migration is generated under `Infrastructure/Persistence/Migrations/`.
- [ ] Migration creates Identity tables and Domain tables.
- [ ] Migration review checklist is completed before database update.
- [ ] `dotnet ef database update --startup-project WebApi` succeeds in local development.

### Story 9 - Seed only synthetic development data

```gherkin
Feature: Safe development seed data
  Scenario: Developer initializes a local database
    Given the app runs in Development
    When seeding runs
    Then seeded people, addresses, phone numbers, notes, and billing data are obviously synthetic
    And no seed data runs in Production unless explicitly approved
```

Acceptance:
- [ ] Seed data is idempotent.
- [ ] Default admin identity uses configurable credentials or documented local-only defaults.
- [ ] Sample clients/caregivers/notes/invoices are clearly fake.
- [ ] Production-like PHI is never committed.

### Story 10 - Verify Infrastructure quality gates

```gherkin
Feature: Infrastructure verification
  Scenario: Sprint 2 is ready to close
    Given all Infrastructure tasks are implemented
    When verification runs
    Then build and tests pass
    And soft delete, UTC conversion, cascade restriction, repository behavior, migration, and audit field tests pass
```

Acceptance:
- [ ] `dotnet build CarePath.sln` passes with zero warnings.
- [ ] `dotnet test CarePath.sln` passes.
- [ ] Infrastructure tests cover DbContext, converters, interceptor, repository, UnitOfWork, configurations, migration shape, and seed data.
- [ ] dotnet-code-reviewer reports no critical issues.

## Functional Requirements

| ID | Requirement | Priority |
|---|---|---|
| S2-FR-001 | Create `Infrastructure` and `Infrastructure.Tests` projects. | Critical |
| S2-FR-002 | Add EF Core 9, SQL Server, ASP.NET Core Identity EF, and test dependencies. | Critical |
| S2-FR-003 | Create `CarePathDbContext` with Identity schema foundation and Domain DbSets. | Critical |
| S2-FR-004 | Configure all entities with Fluent API only. | Critical |
| S2-FR-005 | Apply global soft-delete query filters to every `BaseEntity`. | Critical |
| S2-FR-006 | Implement UTC DateTime converters for all DateTime properties. | Critical |
| S2-FR-007 | Implement `AuditableEntityInterceptor` for audit fields. | Critical |
| S2-FR-008 | Implement `Repository<T>` and `UnitOfWork`. | Critical |
| S2-FR-009 | Add `GetPagedAsync` to Domain repository contract and Infrastructure implementation. | High |
| S2-FR-010 | Generate and review InitialCreate migration. | Critical |
| S2-FR-011 | Add synthetic-only seed strategy. | High |
| S2-FR-012 | Add Infrastructure tests for all persistence guardrails. | Critical |

## Non-Functional Requirements

| Category | Requirement |
|---|---|
| Security | No PHI in logs, exception messages, seed data, or migration comments. |
| Compliance | Soft delete and retention behavior must be verified for all PHI entities. |
| Performance | Shift and VisitNote queries must have paging support and indexes on common filters. |
| Reliability | UnitOfWork transactions must support rollback on failures. |
| Maintainability | Entity configurations must be separate files; DbContext stays small. |
| Testability | Repository and UnitOfWork behavior must be testable without production SQL Server. |

## Scope

In scope: Infrastructure project and tests, EF Core + SQL Server persistence, Identity schema foundation, CP-01 mappings, CP-03 mappings where Domain entities exist, repositories, UnitOfWork, migration, seed, and tests.

Out of scope: login/JWT endpoints, Application services, API controllers, UI, real Twilio/AI/OCR/file storage, and persisted PDF/photo binary storage.

## Risks and Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| EF mappings drift from actual Domain property names | Build failures or wrong schema | Cross-check actual entity files before configuration. |
| Cascade delete accidentally removes PHI | Compliance failure | Migration review and configuration tests for delete behavior. |
| DateTime kind is lost in SQL Server | Incorrect audit/scheduling behavior | UTC converters and round-trip tests. |
| Seed data resembles real PHI | Compliance exposure | Synthetic-only seed checklist and code review. |
| Repository permits unbounded high-volume reads | Performance failure | Add paging and avoid `GetAllAsync` for Shift/VisitNote workflows. |

## Revision History

| Version | Date | Author | Changes |
|---|---|---|---|
| 1.0 | 2026-06-27 | Codex | Initial Sprint 2 story requirements. |