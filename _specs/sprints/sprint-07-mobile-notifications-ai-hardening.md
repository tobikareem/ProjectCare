# Sprint 7 - MAUI Mobile MVP, Notifications, AI & Hardening

Status: Draft
Primary outcome: mobile caregiver/patient workflow plus delivery, AI/OCR, and compliance hardening.

## Sprint Goal

Create the MAUI Blazor Hybrid mobile app and finish MVP delivery mechanics: GPS check-in/out, VisitNotes, reminder acknowledgement, patient check-ins, offline basics, Twilio SMS/voice, AI/OCR extraction, secure attachment storage, and compliance hardening.

## Scope

In scope:

- `CarePath.Mobile` MAUI Blazor Hybrid app.
- Login and secure token storage.
- Caregiver shift list/detail.
- GPS check-in/out.
- Caregiver month-to-date shift metrics derived from successful check-in/out records.
- VisitNote entry with photos/signatures.
- Patient reminder acknowledgement and symptom check-ins.
- Basic offline queue for VisitNotes/check-ins.
- Twilio SMS/voice delivery service and webhook handling.
- Twilio BAA/consent/opt-out/message-minimization requirements.
- AI/OCR extraction implementation behind existing interface.
- Secure file/photo storage hardening.
- Temporary-password hardening: force password change on first login for provisioned accounts.
- Compliance hardening and end-to-end tests.

Out of scope:

- Full FHIR import.
- Predictive risk scoring.
- Multi-language production rollout unless separately approved.

## Stories

### Story 1 - Caregiver shift execution

```gherkin
Feature: Mobile shift execution
  Scenario: Caregiver completes a shift
    Given the caregiver is authenticated on mobile
    And they have an assigned shift today
    When they check in with GPS, complete care, submit a VisitNote, collect signatures, and check out
    Then the shift is updated
    And the VisitNote is available to the coordinator
```

### Story 2 - Offline VisitNote queue

```gherkin
Feature: Offline mobile documentation
  Scenario: Caregiver loses connectivity during a visit
    Given the caregiver is documenting a VisitNote
    And the device is offline
    When they save the note
    Then the note is stored in an encrypted local queue
    And it syncs when connectivity returns
```

### Story 3 - Patient reminder acknowledgement

```gherkin
Feature: Reminder acknowledgement
  Scenario: Patient acknowledges medication reminder
    Given a patient has an active transition reminder
    When they acknowledge it through mobile or SMS
    Then the reminder status becomes Acknowledged
    And the acknowledgement timestamp is recorded in UTC
```

### Story 4 - SMS/voice delivery

```gherkin
Feature: Twilio reminder delivery
  Scenario: System sends a reminder
    Given a TransitionPlan is Active
    And a reminder is due
    When the reminder worker dispatches it through Twilio
    Then delivery status is recorded
    And message logs do not contain PHI beyond the minimum necessary content
```

### Story 5 - SMS consent and opt-out

```gherkin
Feature: SMS consent and opt-out
  Scenario: Patient opts out of SMS reminders
    Given the patient has SMS reminders enabled
    When Twilio sends an opt-out webhook
    Then SMS reminders are disabled for that patient
    And future reminders use another configured channel or become coordinator-visible exceptions
```

### Story 6 - Twilio failure logging

```gherkin
Feature: PHI-safe provider logging
  Scenario: Twilio fails to deliver a reminder
    Given Twilio returns a delivery failure
    When the failure is logged
    Then the log contains provider message ID, entity ID, status, timestamp, and correlation ID
    And the log does not contain medication names, symptoms, diagnosis, patient name, or discharge text
```

### Story 7 - AI extraction with clinical guardrails

```gherkin
Feature: AI discharge extraction
  Scenario: AI extracts discharge instructions
    Given a DischargeDocument contains clinical instructions
    When the AI/OCR service extracts structured instructions
    Then each instruction includes confidence and source reference
    And the plan remains PendingVerification until a clinician approves it
```

### Story 8 - End-to-end 30-day workflow

```gherkin
Feature: CarePath Transitions end-to-end
  Scenario: Patient completes the 30-day transition window
    Given a patient has an activated TransitionPlan
    When reminders, check-ins, caregiver observations, and escalations occur over 30 days
    Then the coordinator can view adherence and escalation history
    And the outcome package can support hospital-facing reporting
```

### Story 9 - Secure file and photo storage

```gherkin
Feature: Secure attachment storage
  Scenario: Caregiver uploads a visit photo
    Given a VisitPhoto may contain PHI
    When the photo is uploaded
    Then the blob is private and encrypted
    And access uses short-lived authorized URLs
    And malware scanning or equivalent content safety workflow is applied
    And no public blob URL is stored or returned
```

### Story 10 - Temporary passwords must be temporary

```gherkin
Feature: Forced password change on first login
  Scenario: Provisioned user signs in with a temporary password
    Given an Admin or Coordinator provisioned an account with a temporary password
    (staff via User Management, or the caregiver/client create flows)
    When the user authenticates for the first time
    Then the API refuses normal operation until the password is changed
    And the change-password flow enforces the standard password policy
    And the temporary password is never logged, echoed, or reusable after the change
    And accounts provisioned before this feature are migrated to require a change on next login
```

Gap recorded 2026-07-17: all three provisioning flows (`CreateStaffUserRequest`,
`CreateCaregiverRequest`, `CreateClientRequest`) accept an admin-chosen `TemporaryPassword`,
but no `MustChangePassword` mechanism exists — users can keep the coordinator-chosen
password indefinitely. Needs an Identity-level flag checked at login/refresh, a
change-password endpoint + client method, and mobile/web first-login UX.

## Tasks

- [ ] Create `CarePath.Mobile`.
- [ ] Add secure token storage.
- [ ] Add mobile app shell and role-aware navigation.
- [ ] Implement caregiver shift list/detail.
- [ ] Implement GPS check-in/out service and permissions.
- [ ] Add caregiver `Shifts (MTD)` metrics from successful caregiver check-ins/check-outs: query current-month checked-in/completed shifts by caregiver, return Admin/Coordinator-safe profile metrics, and avoid using lifetime `TotalShiftsCompleted` as the MTD source.
- [ ] Implement VisitNote form, photo upload, and signatures.
- [ ] Implement offline queue for VisitNotes/check-ins.
- [ ] Implement patient reminders/check-ins UI.
- [ ] Implement Twilio delivery service and webhook verification.
- [ ] Document BAA, consent, opt-out, and message minimization requirements for Twilio.
- [ ] Implement reminder background worker.
- [ ] Implement AI/OCR provider adapter.
- [ ] Implement secure file/photo storage pattern with private blobs and short-lived URLs.
- [ ] Add forced password change on first login: `MustChangePassword` flag set by all three
      provisioning flows (staff, caregiver, client) and by a seed/migration for existing
      accounts; login/refresh returns a stable `auth.password_change_required` code; new
      change-password endpoint + `AuthClient` method; web + mobile first-login screens;
      tests prove a temporary password cannot be used for normal API operation.
- [ ] Add PHI-safe logging tests.
- [ ] Add end-to-end workflow tests or scripted demo.

## Exit Gate

- [ ] Caregiver can complete a shift from mobile.
- [ ] Admin/Coordinator caregiver profile shows `Shifts (MTD)` from check-in-derived shift data.
- [ ] Patient can acknowledge reminders/check-ins.
- [ ] Twilio delivery and webhooks are verified in non-production mode.
- [ ] SMS consent and opt-out flow is implemented or explicitly blocked before production.
- [ ] AI/OCR extraction requires clinician verification.
- [ ] Attachment storage is private, encrypted, scanned/validated, and never public.
- [ ] Offline queue handles at least VisitNote draft and retry.
- [ ] Temporary passwords cannot be used beyond first login; forced change verified for staff, caregiver, and client accounts.
- [ ] End-to-end demo works from discharge upload to escalation/outcome view.
- [ ] Build/tests pass.
