# CP-03 — CarePath Transitions: Requirements

**Status**: Approved  
**Author**: CarePath Health  
**Created**: 2026-06-22  
**Spec type**: Requirements

---

## Problem Statement

When a patient is discharged from a hospital, they receive a dense set of instructions covering medications, follow-up appointments, diet restrictions, wound care, activity limits, and warning symptoms. These instructions are given once, under stress, in medical language. Within 30 days, 20% of Medicare patients are readmitted — and 27% of those readmissions are preventable.

The gap is not medical — it is operational. No one is watching between discharge and the first follow-up appointment.

CarePath Transitions closes that gap. It digitizes the discharge plan, gets it clinician-verified, and delivers 30 days of structured daily support to the patient through their existing CarePath caregiver, their family, and a care coordinator.

---

## Business Goals

1. Reduce 30-day readmissions for CarePath clients.
2. Open a B2B hospital revenue stream — sell to hospital discharge departments as a penalty-avoidance product under CMS HRRP.
3. Differentiate CarePath from remote-only competitors by leveraging in-home caregivers as adherence observers.
4. Align with Maryland HSCRC Readmission Reduction Incentive Program.

---

## User Roles

| Role | Description |
|---|---|
| **Patient** | The discharged individual. Receives reminders, responds to check-ins. May have limited tech literacy. |
| **Family / Proxy** | Authorized family member with read access to the transition plan and alerts. |
| **Verifying Clinician** | Licensed clinician (RN or MD) who reviews AI-extracted draft plans and activates them via e-signature. |
| **Care Coordinator** | CarePath employee who monitors all active transition plans, receives escalations, and contacts patients. |
| **In-Home Caregiver** | CarePath caregiver (W-2) who attends scheduled shifts. Their VisitNotes feed adherence data into the plan. |
| **Admin** | Platform administrator. Manages clinician assignments and system configuration. |

---

## Functional Requirements

### FR-001 — Discharge Document Intake
The system shall allow a Care Coordinator or clinician to upload a discharge document as a PDF or photo (JPEG/PNG). The system shall extract structured data from the upload using AI processing.

### FR-002 — AI Extraction with Confidence Scoring
The system shall extract the following categories from the discharge document:
- Medications (name, dose, frequency, timing, duration)
- Follow-up appointments (provider, date/time if present, location)
- Diet restrictions
- Activity restrictions
- Wound care instructions
- Warning signs requiring escalation
- Equipment and supply needs
- Other instructions

Each extracted item shall carry a confidence score (0.0–1.0). Items below 0.75 confidence shall be flagged for mandatory clinician review. The original source text shall be preserved alongside every extracted item.

### FR-003 — Clinical Review Queue
The system shall present the AI-extracted draft to a verifying clinician in a structured review interface. The clinician shall be able to:
- Approve, edit, or reject each individual instruction
- Add clinical notes to any instruction
- Flag items for pharmacist review
- Reject the entire plan with a reason

### FR-004 — E-Signature Activation
A `TransitionPlan` shall not become `Active` until a verifying clinician provides an e-signature. The e-signature and timestamp shall be stored as part of the audit trail.

### FR-005 — Medication Reminder Delivery
Once a plan is `Active`, the system shall deliver medication reminders to the patient via the configured channel (App, SMS, or Voice call). Reminders shall follow the exact schedule extracted from the discharge document. Unacknowledged reminders shall be retried once before being marked `Missed`.

### FR-006 — Appointment Reminders
The system shall send appointment reminders 48 hours and 2 hours before each scheduled follow-up. The patient may acknowledge via app tap, SMS reply, or IVR keypress.

### FR-007 — Symptom Check-In Prompts
The system shall send daily symptom check-in prompts during Days 1–7 (highest risk) and every other day thereafter. Responses shall be recorded in `TransitionCheckIn`. Responses matching clinician-defined warning sign criteria shall immediately set `ContainsWarningSymptom = true` and trigger an escalation evaluation.

### FR-008 — Tiered Reminder Intensity by Risk Level
The `TransitionPlan.RiskLevel` field (Low / Medium / High) shall control reminder frequency:
- **High**: Daily check-ins; all missed reminders escalate within 4 hours
- **Medium**: Check-ins on Days 1–7, then every 2 days; missed escalations within 8 hours
- **Low**: Check-ins on Days 1–3, 7, 14, 21, 30; missed escalations within 24 hours

### FR-009 — Escalation Workflow
The system shall evaluate escalation conditions after every missed reminder and warning symptom event. Escalation shall proceed through configured levels:
1. `CoordinatorAlert` — coordinator receives an alert in the dashboard
2. `FamilyNotification` — family proxy receives a notification
3. `UrgentCare` — coordinator calls patient with urgent care guidance
4. `Emergency911` — coordinator advises patient or family to call emergency services

All escalation events shall be stored in `TransitionEscalation`. The system shall never autonomously call 911 — it presents the option to the coordinator.

### FR-010 — Care Coordinator Dashboard
The system shall provide a dashboard showing all active transition plans with:
- Patient name, risk level, and days remaining
- Missed reminders and unacknowledged check-ins
- Warning symptom flags
- Escalation history
- Direct link to the assigned in-home caregiver

### FR-011 — Caregiver VisitNote Integration
When a caregiver completes a VisitNote for a client with an active transition plan, the VisitNote shall optionally reference the `TransitionPlanId`. The coordinator dashboard shall surface caregiver observations linked to the plan.

### FR-012 — Family Proxy Access
An authorized family member shall be able to view (read-only) the active transition plan, reminder history, and check-in responses. The family proxy shall receive notifications on escalation events.

### FR-013 — Prescription Refill Reminders
The system shall calculate when each medication supply will run out based on quantity dispensed and daily dose. A refill reminder shall be sent 5 days before the projected run-out date.

### FR-014 — FHIR Import (Phase 2)
The system shall support direct import of structured discharge summaries from hospital EHR systems via HL7 FHIR R4. This is a Phase 2 capability and is not required for MVP.

### FR-015 — Multilingual Support (Phase 2)
Patient-facing instructions and reminders shall be deliverable in the patient's preferred language. AI translation shall be applied to extracted instructions. Phase 2 capability.

### FR-016 — Outcome Reporting (Phase 2)
The system shall generate a 30-day outcome report per transition plan, including: adherence rate, missed reminders, escalations, check-in responses, and readmission status (if captured). Reports shall be exportable for hospital B2B contracts.

---

## Non-Functional Requirements

### NFR-001 — HIPAA Compliance
All data within the Transitions feature is PHI. The full HIPAA framework defined in CLAUDE.md applies: encryption at rest, role-based authorization on every endpoint, audit logging on every PHI read/write/delete, no PHI in logs or URLs.

### NFR-002 — Safety Guardrails
The system shall never autonomously: change medication dosages, provide a medical diagnosis, override clinician instructions, or contact emergency services. All escalation scripts shall be authored by clinicians.

### NFR-003 — Audit Trail
Every state change on `DischargeDocument`, `TransitionPlan`, `TransitionInstruction`, and `TransitionEscalation` shall be captured in the audit log with `UserId`, `Timestamp`, `Action`, `EntityType`, `EntityId`. No exceptions.

### NFR-004 — SMS Delivery
SMS delivery shall use Twilio. Voice call delivery shall use Twilio Programmable Voice. The system shall not rely solely on in-app delivery — SMS must be supported because elderly patients frequently do not have smartphones.

### NFR-005 — Data Retention
All transition data shall be retained for a minimum of 6 years per Maryland medical record retention law (consistent with existing platform policy).

---

## Acceptance Criteria (High-Level)

```gherkin
Feature: Discharge Document Intake

  Scenario: Coordinator uploads discharge PDF
    Given a client has been discharged from hospital
    When the coordinator uploads a discharge PDF
    Then the system extracts medications, appointments, diet, activity, wound care, warning signs
    And each item has a confidence score
    And items below 0.75 confidence are flagged for review
    And all items link to their source text in the original document

  Scenario: Clinician activates a transition plan
    Given an AI-extracted draft is in PendingVerification status
    When the clinician reviews all items and provides an e-signature
    Then the plan status changes to Active
    And the TransitionWindowEnd is set to DischargeDate + 30 days
    And the first set of reminders is scheduled

  Scenario: Patient misses a critical medication reminder (High risk)
    Given a patient has a High-risk TransitionPlan
    And a medication reminder was sent and not acknowledged within 4 hours
    When the escalation evaluator runs
    Then a CoordinatorAlert escalation is created
    And the coordinator receives a dashboard alert

  Scenario: Patient reports a warning symptom
    Given a patient completes a symptom check-in
    And the response matches a clinician-defined warning sign
    When the check-in is processed
    Then ContainsWarningSymptom is set to true
    And an escalation is triggered immediately regardless of risk level
```

## Cross-Sprint Compliance Gates

- Every Transitions endpoint that accepts an entity id must enforce object-level authorization to prevent IDOR.
- Every PHI read, write, update, and delete must be audit logged without PHI values.
- DischargeDocument.RawContent and TransitionInstruction.SourceText must never appear in logs, URLs, exception messages, or third-party provider logs.
