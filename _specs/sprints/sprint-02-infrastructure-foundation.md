# Sprint 2 - Infrastructure Foundation

Status: Complete
Primary outcome: durable, HIPAA-aware persistence for CP-01 Domain entities. CP-03 Transitions persistence is deferred until explicit Transitions configurations are in scope.

## Sprint 2 Spec Set

- Requirements and story acceptance: `_specs/sprints/sprint-02-requirements.md`
- Technical design: `_specs/sprints/sprint-02-design.md`
- Execution tasks: `_specs/sprints/sprint-02-tasks.md`
- Backing CP-02 requirements: `_specs/01-requirements/cp-02-infrastructure-ef-core.md`
- Backing CP-02 design: `_specs/02-design/cp-02-infrastructure-ef-core.md`
- Backing CP-02 tasks: `_specs/03-tasks/cp-02-infrastructure-ef-core.md`

## Sprint Goal

Create the `Infrastructure` and `Infrastructure.Tests` projects with EF Core, SQL Server, entity configurations, repositories, Unit of Work, audit fields, soft-delete filters, UTC conversion, synthetic seed strategy, and migrations.

## Scope

In scope:

- `Infrastructure` project.
- `Infrastructure.Tests` project.
- EF Core 9 + SQL Server.
- `CarePathDbContext`.
- Entity configurations for CP-01 Domain entities. CP-03 Transitions configurations are deferred.
- `ApplicationUser` Identity schema foundation.
- Repository and UnitOfWork implementations.
- `GetPagedAsync`.
- Initial migration.
- Synthetic-only seed strategy.
- Tests for soft delete, UTC conversion, repository behavior, cascade restrictions, and audit field population.

Out of scope:

- Login endpoints and JWT issuance.
- Business use cases.
- Blazor/MAUI UI.
- Real Twilio/AI integrations.

## Stories

### Story 1 - Persist Domain entities safely

```gherkin
Feature: EF Core persistence
  Scenario: Saving a client with PHI
    Given a coordinator creates a Client record
    When the Infrastructure layer saves the record
    Then the record uses a Guid primary key
    And audit fields are populated
    And DateTime values are stored as UTC
    And the record is not hard-deleted when removed
```

### Story 2 - Enforce soft deletes

```gherkin
Feature: Soft delete enforcement
  Scenario: A PHI record is deleted
    Given a Client record exists
    When repository DeleteAsync is called
    Then IsDeleted is set to true
    And DbSet.Remove is not called
    And normal queries exclude the deleted record
```

### Story 3 - Prevent PHI cascade deletes

```gherkin
Feature: HIPAA-safe relational behavior
  Scenario: A parent record with clinical child records is deleted
    Given a Client has CarePlan, Shift, VisitNote, and TransitionPlan records
    When deletion behavior is configured
    Then PHI relationships use DeleteBehavior.Restrict
    And clinical data is retained for compliance
```

### Story 4 - Page high-volume data

```gherkin
Feature: Paged repository reads
  Scenario: Coordinator opens a dashboard with thousands of VisitNotes
    Given VisitNote is a high-volume table
    When the Application layer requests dashboard data
    Then it can call GetPagedAsync with page number and page size
    And it does not load all VisitNotes into memory
```

### Story 5 - Persist Transitions entities (Deferred)

```gherkin
Feature: Transition persistence
  Scenario: A discharge document generates a transition plan
    Given a DischargeDocument exists for a Client
    When a TransitionPlan and instructions are saved
    Then the relationships are configured
    And RawContent and SourceText are stored without indexes
    And no PHI values appear in logs or exception messages
```

### Story 6 - Protect development seed data

```gherkin
Feature: Safe seed data
  Scenario: Developer seeds a local database
    Given seed data is enabled for development
    When the database is initialized
    Then all seeded people, addresses, phone numbers, and clinical notes are obviously synthetic
    And no production-like patient data is committed to source control
```

## Tasks

- [x] Create `Infrastructure/Infrastructure.csproj`.
- [x] Create `Infrastructure.Tests/Infrastructure.Tests.csproj`.
- [x] Add EF Core, SQL Server, Identity, and test package references.
- [x] Create `Persistence/CarePathDbContext.cs`.
- [x] Create UTC DateTime converter.
- [x] Create auditable entity interceptor.
- [x] Configure all CP-01 entities.
- [x] Configure all Transitions entities. *(Deferred to CP-03 backend scope; CP-02 explicitly does not map Transitions DbSets.)*
- [x] Implement global query filters.
- [x] Implement repository and UnitOfWork.
- [x] Add `GetPagedAsync` to `IRepository<T>` and implementation.
- [x] Add Transitions repositories to UnitOfWork. *(Deferred to CP-03 backend scope; not part of CP-02 completion.)*
- [x] Generate and review initial migration.
- [x] Add synthetic-only seed strategy.
- [x] Add Infrastructure tests. *(UTC converter, audit interceptor, DbContext model, soft-delete filter, entity-configuration metadata, repository, UnitOfWork, DI registration, migration-shape, and seed tests are complete.)*

## Exit Gate

- [x] `dotnet build CarePath.sln` passes.
- [x] `dotnet test CarePath.sln` passes.
- [x] Migration reviewed for no cascade delete on PHI entities.
- [x] Soft delete and UTC behavior verified by tests.
- [x] Infrastructure can be registered from WebApi.
- [x] Seed data contains no real or production-like PHI.




