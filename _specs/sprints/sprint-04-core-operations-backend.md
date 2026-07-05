# Sprint 4 - Core Operations Backend

Status: Draft
Primary outcome: backend workflows for in-home care and staffing operations.

## Sprint Goal

Implement the API and Application workflows needed for caregiver/client management, credentials, scheduling, VisitNotes, billing basics, and margin visibility.

## Scope

In scope:

- Caregiver management.
- Client and care plan management.
- Credential/certification compliance.
- Shift scheduling and assignment.
- Double-booking and expired-certification assignment guards.
- VisitNote and VisitPhoto API support.
- Billing and invoice basics.
- Margin DTOs and queries.
- WebApi cleanup and real controllers.

Out of scope:

- Full Blazor pages.
- Full MAUI app.
- Transitions-specific workflows beyond VisitNote linkage readiness.

## Stories

### Story 1 - Manage caregivers

```gherkin
Feature: Caregiver management
  Scenario: Coordinator adds a caregiver
    Given the coordinator is authorized
    When they create a caregiver profile with employment type and pay rate
    Then the caregiver is saved
    And certification requirements can be tracked
```

### Story 2 - Manage clients and care plans

```gherkin
Feature: Client care planning
  Scenario: Coordinator creates a client care plan
    Given a client exists
    When the coordinator creates a CarePlan
    Then the CarePlan is linked to the client
    And PHI fields are persisted and audit logged
```

### Story 3 - Schedule a shift

```gherkin
Feature: Shift scheduling
  Scenario: Coordinator assigns a caregiver to an in-home shift
    Given the caregiver is available and credentialed
    And the client has an active care plan
    When the coordinator creates the shift
    Then the shift is scheduled
    And the caregiver can see it on mobile through API contracts
```

### Story 4 - Prevent double-booking

```gherkin
Feature: Scheduling conflict prevention
  Scenario: Coordinator assigns overlapping shifts
    Given a caregiver already has a scheduled shift from 9 AM to 1 PM
    When the coordinator assigns another shift that overlaps that window
    Then the assignment is rejected
    And the error response does not include PHI
```

### Story 5 - Prevent expired-certification assignment

```gherkin
Feature: Credential compliance
  Scenario: Coordinator assigns caregiver with expired required certification
    Given a shift requires a certification
    And the caregiver's matching certification is expired
    When the coordinator tries to assign the caregiver
    Then the assignment is rejected
    And the coordinator sees a compliance-safe reason
```

### Story 6 - Submit a VisitNote

```gherkin
Feature: Visit documentation
  Scenario: Caregiver submits a VisitNote
    Given the caregiver is assigned to the shift
    When they submit activities, condition, concerns, vitals, photos, and signatures
    Then the VisitNote is saved
    And the note is linked to the shift
    And PHI-safe audit records are created
```

### Story 7 - Generate billing summary

```gherkin
Feature: Billing and margin
  Scenario: Admin reviews completed shift profitability
    Given a completed shift has bill rate, pay rate, and billable hours
    When the admin opens margin analytics
    Then the system returns clear hourly and total margin metrics
    And the result distinguishes in-home and staffing service lines
```

## Tasks

- [ ] Remove WeatherForecast controller/model/http sample.
- [ ] Add authenticated controllers for caregivers, clients, care plans, shifts, visit notes, and billing.
- [ ] Add Application commands/queries for core operations.
- [ ] Add validators for create/update requests.
- [ ] Add object-level authorization checks for caregiver/client access.
- [ ] Add margin query DTOs.
- [ ] Add paged dashboard queries for shifts and visit notes.
- [ ] Add double-booking guard.
- [ ] Add expired-certification assignment guard.
- [ ] Add API integration tests for authorization and validation.

## Exit Gate

- [ ] WebApi exposes real CarePath APIs.
- [ ] Template WeatherForecast code is removed.
- [ ] Core operations can be exercised through API tests.
- [ ] PHI routes are authorized and audit-aware.
- [ ] Double-booking and expired-certification assignment are rejected by Application logic.
- [ ] Build/tests pass.
