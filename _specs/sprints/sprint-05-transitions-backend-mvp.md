# Sprint 5 - CarePath Transitions Backend MVP

Status: Complete (closed 2026-07-06) - implementation contract and per-task record in `_specs/sprints/sprint-05-tasks.md`; decisions D-S5-1..8 remain normative for Sprint 6/7 work touching Transitions
Primary outcome: working backend for the 30-day discharge workflow.

## Sprint Goal

Implement the Transitions vertical slice: discharge intake, extraction stub/manual plan creation, clinician review, activation, reminder/check-in records, escalation records, coordinator dashboard queries, and safe patient/care-team read models. Sprint 5 intentionally narrows older CP-03 requirements: no real AI/OCR, Twilio delivery/webhooks, FHIR import, binary document persistence, or autonomous escalation actions.

## Scope

In scope:

- Discharge document intake metadata and clinician-entered/imported raw text persistence only; no persisted PDF/photo binaries unless secure file/photo storage gates are implemented in this sprint.
- Extraction interface with deterministic stub.
- Structured TransitionPlan draft.
- Instruction-level clinician review.
- E-signature activation.
- Reminder scheduling records.
- Patient check-in recording.
- Coordinator escalation records.
- VisitNote linkage to active TransitionPlan.
- Read/write audit on every Transitions PHI endpoint.
- Object-level authorization on every Transitions route that accepts an entity id to prevent IDOR.
- Clinical/patient-facing/care-team DTO split: `RawContent`, `SourceText`, `ResponsesJson`, confidence scores, and clinician review notes are clinical-only and never returned to patient or caregiver/care-team-safe routes.
- PHI 404 response bodies remain byte-identical for missing and denied resources and omit per-request TraceId.

Out of scope:

- Full AI/OCR extraction implementation.
- Full Twilio delivery.
- FHIR import.
- Multilingual translation.
- Hospital outcome export.
- Persisted PDF/photo binaries before private encrypted storage, short-lived access URLs, malware scanning, access control, and no-public-blob controls are implemented.

## Stories

### Story 1 - Upload discharge document

```gherkin
Feature: Discharge document intake
  Scenario: Coordinator uploads discharge notes
    Given a client was recently discharged
    When the coordinator submits discharge intake metadata and approved raw text
    Then a DischargeDocument record is created without storing binary media unless secure storage gates are complete
    And extraction status becomes Pending
    And raw content is treated as PHI
```

### Story 2 - Create extracted draft

```gherkin
Feature: AI-assisted extraction stub
  Scenario: System extracts instructions from discharge content
    Given a DischargeDocument is ready for extraction
    When the extraction service processes the document
    Then it creates draft TransitionInstructions
    And each instruction has category, source text, and confidence score
    And low-confidence items are marked for review
```

### Story 3 - Clinician verifies plan

```gherkin
Feature: Clinical verification
  Scenario: Clinician activates a transition plan
    Given a draft plan is PendingVerification
    When the clinician approves or modifies every required instruction
    And the clinician e-signs the plan
    Then the plan becomes Active
    And TransitionWindowEnd equals DischargeDate plus 30 days in UTC
    And the existing VerifiedBy, VerifiedAt, and ActivatedAt fields record the MVP e-signature
```

### Story 4 - Block pre-activation reminders

```gherkin
Feature: Reminder safety guard
  Scenario: System attempts to schedule reminders for a draft plan
    Given a TransitionPlan is not Active
    When reminder scheduling is requested
    Then the Application layer rejects the operation
    And no patient reminder is created or sent
```

### Story 5 - Record patient check-in

```gherkin
Feature: Transition check-ins
  Scenario: Patient reports warning symptoms
    Given the patient has an Active TransitionPlan
    When they submit a symptom check-in with warning signs
    Then a TransitionCheckIn is saved
    And a TransitionEscalation is created for coordinator review
```

### Story 6 - Link VisitNote observations

```gherkin
Feature: Caregiver transition observations
  Scenario: Caregiver submits a VisitNote during the 30-day window
    Given the client has an Active TransitionPlan
    And the caregiver is assigned to the shift
    When the VisitNote is submitted
    Then the VisitNote is linked to the TransitionPlan
    And coordinator dashboard signals include the observation
    And caregiver-facing transition reads do not expose SourceText, RawContent, confidence scores, or clinician review notes
```

### Story 7 - Deny autonomous escalation actions

```gherkin
Feature: Escalation safety
  Scenario: Warning symptoms are reported
    Given a patient reports a warning symptom
    When the escalation evaluator runs
    Then it creates a CoordinatorAlert escalation
    And it does not autonomously contact family, urgent care, or 911
```

### Story 8 - Audit transition reads

```gherkin
Feature: Transition read audit
  Scenario: Clinician views source discharge text
    Given SourceText and RawContent contain PHI
    When a clinician opens the review screen through the API
    Then the read is audit logged
    And the audit log does not contain SourceText or RawContent
    And missing and denied PHI reads return byte-identical 404 bodies without body TraceId
```

## Tasks

- [ ] Implement Transitions EF configurations, explicit collection/backing-field relationship mapping, and `VisitNote.TransitionPlanId` mapping.
- [ ] Add Transitions Contracts/Application DTOs/commands/queries with clinical, patient-facing, and care-team-safe DTO separation.
- [ ] Implement extraction service interface and stub.
- [ ] Implement upload command.
- [ ] Implement clinical review and activation command using existing `VerifiedBy`, `VerifiedAt`, and `ActivatedAt` e-signature fields.
- [ ] Implement reminder scheduling guard.
- [ ] Implement check-in command and escalation evaluator.
- [ ] Implement coordinator dashboard query.
- [ ] Add `TransitionsController` with role-based authorization.
- [ ] Enforce secure file/photo storage gates before accepting persisted PDF/photo binaries, or reject/defer binary uploads while allowing metadata/raw-text intake only.
- [ ] Add read/write audit for Transitions endpoints.
- [ ] Add object-level authorization checks and IDOR denial tests for Transitions `{id}` routes.
- [ ] Add Application/API tests for all safety guards, DTO exposure boundaries, and byte-identical PHI 404 bodies without TraceId.

## Exit Gate

- [ ] Discharge intake through activation works through API.
- [ ] PDF/photo binaries are not persisted unless private encrypted storage, short-lived access URLs, malware scanning, access control, and no-public-blob controls are complete.
- [ ] No reminders can be created for inactive plans.
- [ ] Warning symptoms create coordinator escalation records only.
- [ ] Family, urgent-care, and 911 actions are never automated.
- [ ] Transitions PHI reads are audit logged.
- [ ] Transitions `{id}` routes deny cross-client/cross-tenant access with byte-identical PHI 404 response bodies and no body TraceId.
- [ ] AI/stub output cannot bypass clinician approval.
- [ ] Patient/caregiver/care-team-safe DTOs never expose RawContent, SourceText, ResponsesJson, confidence scores, or clinician review notes.
- [ ] Build/tests pass.
