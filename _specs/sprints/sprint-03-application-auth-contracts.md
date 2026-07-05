# Sprint 3 - Application, Auth & Shared Contracts

Status: Complete (closed 2026-07-04)
Primary outcome: create the business boundary and shared DTO/client contract layer before UI work begins.

> **Implementation breakdown**: `_specs/sprints/sprint-03-tasks.md` — S3-TASK board with dependencies, owners, and success criteria, plus the ratified decisions D1–D4 (Application→Contracts dependency, Clinician/Family role mapping, object-level authorization + PHI read audit enforcement, Application scaffold normalization) and the full Contracts plan. Read that file before implementing any Sprint 3 task.

## Sprint Goal

Create `Application`, authentication/authorization foundations, shared client contracts, DTOs, validators, typed API-client conventions, and PHI-safe audit abstractions.

## Scope

In scope:

- `Application` project.
- `Application.Tests` project.
- `CarePath.Contracts` project for client-safe DTOs and response models.
- `CarePath.Client` typed API client for Blazor WebAssembly and MAUI.
- `CarePath.Client.UI` Razor Class Library for reusable client components and UI primitives.
- Current-user abstraction.
- FluentValidation.
- Auth/JWT/role policy design and implementation foundation.
- Object-level authorization model.
- IDOR prevention policy for every route that accepts an entity ID.
- API response, validation error, paging, and problem-details contracts.
- PHI-safe audit interfaces.
- System-actor audit support for background jobs.

Out of scope:

- Full web/mobile UI.
- Real SMS/AI provider implementation.
- Final hospital-facing reports.

## Stories

### Story 1 - Application boundary protects Domain

```gherkin
Feature: Application service boundary
  Scenario: UI requests client data
    Given the UI needs to display a client summary
    When the UI calls the API
    Then the Application layer maps Domain data to a DTO
    And the UI never receives a Domain entity directly
```

### Story 2 - Validate commands at the boundary

```gherkin
Feature: FluentValidation boundary
  Scenario: Coordinator creates a shift with an invalid date range
    Given a CreateShift request has an end time before start time
    When the request reaches the Application layer
    Then FluentValidation rejects it
    And no Domain entity is persisted
```

### Story 3 - Role authorization

```gherkin
Feature: Role-based authorization
  Scenario: Caregiver attempts to access an unassigned client
    Given a caregiver is authenticated
    And the caregiver is not assigned to the client
    When the caregiver requests the client's PHI
    Then access is denied
    And the denial does not reveal PHI
```

### Story 4 - Family proxy access

```gherkin
Feature: Family proxy authorization
  Scenario: Authorized family member views a transition plan
    Given a family proxy has explicit access to a client
    When they request the active TransitionPlan
    Then they can view approved patient-facing instructions
    And they cannot view internal clinical notes unless explicitly authorized
```

### Story 5 - PHI-safe audit abstractions

```gherkin
Feature: Audit logging
  Scenario: Coordinator reads a VisitNote
    Given VisitNote contains PHI
    When a coordinator reads the VisitNote
    Then the read is audit logged with UserId, Timestamp, Action, EntityType, and EntityId
    And the audit log does not contain Activities, ClientCondition, Concerns, or Medications text
```

### Story 6 - Prevent IDOR on entity routes

```gherkin
Feature: Object-level authorization
  Scenario: Authenticated user guesses another client's identifier
    Given the user is authenticated
    And the user is not authorized for the requested client
    When they call an endpoint with that client's ID
    Then the request is denied
    And the response does not reveal whether the client exists
    And the access attempt is audit logged without PHI values
```

### Story 7 - Background-job audit context

```gherkin
Feature: System actor audit
  Scenario: Reminder background job updates reminder status
    Given a background job processes a due reminder
    When it updates ReminderStatus
    Then the audit record uses a system actor identity
    And the audit entry contains EntityType, EntityId, Action, Timestamp, and correlation ID
    And it does not include reminder message content
```

## Tasks

Task-level tracking has moved to `_specs/sprints/sprint-03-tasks.md` (S3-TASK IDs). The checklist below is the summary view.

- [x] Create `Application` and `Application.Tests`. (S3-TASK-010/030)
- [x] Create `CarePath.Contracts`. (S3-TASK-011 — envelopes + 8 enum mirrors)
- [x] Create `CarePath.Client`. (S3-TASK-060)
- [x] Create `CarePath.Client.UI`. (S3-TASK-061)
- [x] Define `ApiResponse<T>`, `PagedResult<T>`, validation error, and problem details contracts. (S3-TASK-011)
- [x] Define client-safe DTOs for identity, clients, care plans, shifts, visit notes, billing. Transitions DTOs deliberately reserved for Sprint 5. (S3-TASK-013/014)
- [x] Add FluentValidation validators for core commands. (S3-TASK-034)
- [x] Add current-user and permission interfaces. (S3-TASK-031)
- [x] Add audit logging interfaces. (S3-TASK-032)
- [x] Add role and object-level authorization services. (S3-TASK-033)
- [x] Add IDOR-safe authorization behavior for `{id}` endpoints. (S3-TASK-033/051 — identical missing/denied 404s)
- [x] Add system-actor audit support for background jobs. (S3-TASK-031/032)
- [x] Add JWT/Identity service contracts. (S3-TASK-036/050)
- [x] Add Application tests for validation, authorization, and PHI-safe DTO mapping. (S3-TASK-034/035)

## Exit Gate

- [x] UI-facing DTOs exist for core modules.
- [x] Application does not depend on Infrastructure. (architecture tests enforce)
- [x] Authorization model covers Admin, Coordinator, Caregiver, Client/Family, FacilityManager, and Clinician.
- [x] PHI-safe audit interfaces exist.
- [x] PHI read audit and background-job audit contracts exist.
- [x] Build/tests pass. (0 warnings; Domain 251 / Application 29 / Infrastructure 55, 2026-07-04)

Sprint closed 2026-07-04 by PM/spec owner after independent verification.
