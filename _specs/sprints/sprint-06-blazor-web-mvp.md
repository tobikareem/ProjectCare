# Sprint 6 - Blazor Web App MVP

Status: Approved - implementation contract proposed in `_specs/sprints/sprint-06-tasks.md` (S6-TASK board, decisions D-S6-1..7: project ownership, auth endpoint + in-memory tokens, browser PHI safety, component set incl. StatusPill->StatusBadge reconciliation, escalation-queue endpoint authorization, bUnit testing, accessibility baseline); approve the board before implementation
Primary outcome: coordinator/admin/clinician web app using shared contracts and UI primitives.

## Sprint Goal

Create the Blazor WebAssembly admin/coordinator/clinician experience for operations and Transitions workflows.

## Scope

In scope:

- `CarePath.Web` Blazor WebAssembly app.
- `CarePath.Client` typed API client if not already created.
- `CarePath.Client.UI` Razor Class Library.
- Authentication state and role-based navigation.
- Coordinator dashboard.
- Scheduling and credential views.
- Clinical review queue.
- Transition plan activation.
- Escalation queue.
- Browser-side PHI safety: no PHI in console logs, local storage, analytics payloads, or URLs.

Out of scope:

- Native mobile features.
- Offline mobile queue.
- Full patient/family mobile UX.

## Stories

### Story 1 - Coordinator dashboard

```gherkin
Feature: Coordinator dashboard
  Scenario: Coordinator opens the web app
    Given the coordinator is authenticated
    When the dashboard loads
    Then they see open shifts, overdue VisitNotes, expiring credentials, active transition plans, and escalations
```

### Story 2 - Clinician review queue

```gherkin
Feature: Clinical review queue
  Scenario: Clinician reviews extracted instructions
    Given a TransitionPlan is PendingVerification
    When the clinician opens the review queue
    Then each instruction shows category, patient-safe instruction text, source reference, confidence score, and status
    And low-confidence instructions are clearly flagged
```

### Story 3 - E-sign activation

```gherkin
Feature: Transition activation
  Scenario: Clinician activates a verified plan
    Given all required instructions are approved or modified
    When the clinician e-signs the plan
    Then the plan status changes to Active
    And reminders become eligible for scheduling
```

### Story 4 - Escalation management

```gherkin
Feature: Coordinator escalation queue
  Scenario: Coordinator resolves a warning symptom escalation
    Given a TransitionEscalation is open
    When the coordinator reviews and acknowledges it
    Then the escalation is marked acknowledged
    And a PHI-safe resolution note can be saved
```

### Story 5 - Shared UI components

```gherkin
Feature: Shared UI library
  Scenario: Web and mobile display transition risk
    Given both apps need to show transition risk level
    When the apps render a RiskBadge component
    Then they use the same shared UI primitive
    And each app controls its own page layout
```

### Story 6 - Avoid PHI in browser diagnostics

```gherkin
Feature: Browser PHI safety
  Scenario: Web app displays a transition instruction
    Given the instruction contains medication or symptom information
    When the page renders or fails
    Then no PHI is written to browser console logs
    And no PHI is stored in local storage
    And route URLs do not contain patient names, diagnoses, medication names, or discharge text
```

## Tasks

- [ ] Create `CarePath.Web`.
- [ ] Create or complete `CarePath.Client`.
- [ ] Create `CarePath.Client.UI`.
- [ ] Build shared components: `KpiCard`, `StatusPill`, `RiskBadge`, `ShiftCard`, `EscalationBanner`, `PatientInstructionCard`, `AuditTimeline`.
- [ ] Implement authenticated layout and role navigation.
- [ ] Build coordinator dashboard.
- [ ] Build scheduling/credential screens.
- [ ] Build Transitions review queue and activation screen.
- [ ] Build escalation queue.
- [ ] Add accessibility checks for keyboard navigation, labels, focus states, and contrast.
- [ ] Add browser-side PHI safety review.

## Exit Gate

- [ ] Web app can complete coordinator/clinician Transitions workflow.
- [ ] Web app uses DTOs/contracts, not Domain entities.
- [ ] Shared UI primitives are reusable by mobile.
- [ ] Accessibility baseline is met.
- [ ] No PHI in browser logs, URLs, local storage, or analytics payloads.
- [ ] Build/tests pass.
