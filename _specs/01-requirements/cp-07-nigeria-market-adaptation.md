# CP-07 — Nigeria Market Adaptation: Requirements

**Status**: Draft — needs product decisions before it can be approved (see §0)  
**Author**: CarePath Health  
**Created**: 2026-10-22  
**Spec type**: Requirements  
**Predecessors**: [CP-05](cp-05-tenant-aware-authorization.md) · [CP-06](cp-06-tenant-data-plane-routing.md) (both draft)  
**Carries findings from**: [Second-opinion review 2026-10-22](../reviews/2026-10-22-second-opinion-review.md) — H9, M1, M2, M3, H8 (Nigeria), M9, and the "Country readiness" section

---

## 0. Decisions the Product Owner must make first

These change what gets built, so the spec cannot be approved until each has an answer.

| # | Decision | Options | Why it matters |
|---|---|---|---|
| D1 | Which pilot track goes first | Maryland private-pay **or** Nigeria private-pay (not both) | The review's Sprint 10 proposal assumes one; the two tracks share CP-05/06 but diverge here |
| D2 | Launch city and agency profile for Nigeria | e.g. Lagos / Abuja; home-care only vs. staffing too | Credential rules, address model and payment rails depend on it |
| D3 | Currency model | Single currency per organization (recommended) vs. multi-currency invoices | Multi-currency adds FX and allocation rules the review says to keep out of a pilot |
| D4 | Payment rails for the pilot | Cash and bank transfer receipts only vs. a local provider (settlement callbacks) | Provider callbacks need the idempotency/authentication work from H5 before they are safe |
| D5 | Offline scope | Capture-only queue (check-in/out, visit note) vs. full offline schedule | Determines whether a native app is required for the pilot |
| D6 | Patient identity | Patient-without-login with family proxy grants (recommended) vs. mandatory patient account | Today every client requires an email (`CreateClientRequestValidator`) |
| D7 | Nigerian compliance owner | Named DPO/compliance owner | NDPA 2023 duties, GAID 2025 interpretation and breach handling need an accountable person |
| D8 | Messaging | SMS/voice for reminders in the pilot, or staffed coordinator queue only | Consent, opt-out, provider contract and language rules precede any send |

---

## Executive Summary

Make the product express a market as configuration and data, not as scattered country conditionals, so that a Nigerian home-care agency can register patients and family payers, verify locally licensed staff, schedule in its own time zone, bill and receipt in naira, and keep working through connectivity loss, under Nigerian data-protection obligations.

The review's conclusion is that Nigeria is "not a currency-symbol change". The current Domain, Contracts and Web assume USD, US tax classifications, Maryland credential categories, US address defaults, a mandatory patient email, operator-device local time, and an always-online caregiver. Each of those is a model gap, and the layers can absorb them cleanly only if the fix is designed once.

---

## 1. Problem Statement

### 1.1 Current State (per review evidence)
- **Money (H9)**: `Invoice`, `Payment`, `Shift` rates and all billing DTOs are decimal with no currency; `BusinessFormatting` hard-codes `$`; another page uses browser-culture `C`.
- **Time (M1)**: `Organization.DefaultTimeZone` is stored but shift and transition pages convert with `DateTimeKind.Local`/`ToUniversalTime()`, so entry follows the operator's device.
- **Workforce and credentials (M3)**: `EmploymentType` is W2/1099; `CertificationType` encodes Maryland categories; address defaults to Maryland; phone validation is length-only.
- **Patient identity (M3/D6)**: every client needs an email login; family proxies exist only as access grants on an existing client account.
- **Offline (M2)**: no native app or queue; check-in uses server receipt time as actual start.
- **Privacy (H8)**: no per-country policy object; retention is prose; no rights-request or breach workflow.
- **Messaging (M9)**: reminders are records, not deliveries; no consent/opt-out/provider gates.

### 1.2 Business Impact
- Without this work a Nigerian agency cannot produce a legally and economically unambiguous invoice, cannot verify a nurse's NMCN registration, and cannot keep caregivers working in the field.

### 1.3 User Impact
- Agency owners: workarounds for family payers and local staff categories.
- Caregivers: lost visits during outages; wrong-timezone schedules.
- Compliance owner: no model for NDPA obligations.

---

## 2. User Stories

```gherkin
As a Nigerian agency owner
I want to register a patient whose daughter arranges and pays for care
So that the patient does not need an email account

Acceptance Criteria:
- Given a patient record without a login
- When I add the daughter as a verified family proxy with billing authority
- Then schedules, notes and invoices work, and the proxy's access is scoped by an explicit grant
```

```gherkin
As a Coordinator in Lagos
I want to enter shift times in the agency's time zone
So that a remote coordinator's device clock cannot shift the schedule

Acceptance Criteria:
- Given the organization time zone is Africa/Lagos
- When any user enters a shift start of 08:00
- Then the stored UTC instant is 07:00Z, and the schedule renders 08:00 for everyone
```

```gherkin
As an agency owner
I want invoices, payments and rate snapshots to carry NGN
So that every financial record is unambiguous

Acceptance Criteria:
- Given the organization currency is NGN
- When an invoice is generated and a bank-transfer receipt is recorded
- Then every amount carries NGN, formatting uses the currency and locale, and a USD amount cannot be allocated to it
```

```gherkin
As a Caregiver on a low-end Android phone
I want to check in and write a visit note without a connection
So that a dropped signal does not lose my visit

Acceptance Criteria:
- Given no connectivity
- When I check in and save a note
- Then they are queued encrypted on the device with capture time and device provenance, visibly pending
- And on reconnect they sync idempotently after re-authorization, and a revoked membership discards them
```

---

## 3. Functional Requirements

| ID | Requirement | Priority | Origin |
|----|-------------|----------|--------|
| FR-001 | Organizations gain a versioned jurisdiction/service policy (country, provider class, currency, time zone, retention schedule reference, messaging policy, address/phone country) that services and validators read; no country `if` statements in pages or validators. | Critical | review §"Configuration and model boundaries" |
| FR-002 | `Invoice`, `Payment`, `InvoiceLineItem` and shift rate snapshots carry an immutable ISO-4217 currency; organization default applies at creation; allocation and recalculation reject mixed currencies; historical rows are backfilled USD only where the organization policy says USD. Contracts and Web formatting use currency + locale. | Critical | H9, D3 |
| FR-003 | All civil date-time entry and display use the organization time zone with explicit DST handling; `DateOnly` for civil dates where contracts allow; UTC instants remain the stored truth. | High | M1 |
| FR-004 | Workforce engagement type is separated from country tax classification; credentials carry jurisdiction, issuer, license number, scope and verification evidence (e.g. NMCN for RN/RM), with training distinct from licensure; legacy enum values keep their numeric contracts. | High | M3 |
| FR-005 | Address supports country, state/region, LGA, postal code and landmark; phone numbers are normalized to E.164 with the organization's country as default; ownership verification is separate from syntax. | High | M3 |
| FR-006 | Patients can exist without a login; family proxies are explicit, verified grants with optional billing authority; patient-facing channels bind to the proxy where the patient has none. | High | D6, M3 |
| FR-007 | Receipt recording supports cash and bank transfer with reconciliation fields; any external provider integration (D4) must authenticate callbacks, deduplicate by provider reference (reusing the H5 idempotency pattern) and record settlement separately from recording. | High | H9, H5 |
| FR-008 | An offline capture protocol for check-in/out and visit notes: encrypted device queue partitioned by user and organization, capture and receipt timestamps, device clock/GPS provenance, idempotent sync, conflict and supervisor-correction rules, expired-assignment handling, remote wipe. Pilot field release requires a real-device outage test. | High (if D1 = Nigeria) | M2, D5 |
| FR-009 | Nigerian privacy operations: lawful basis and sensitive-data condition recorded per processing purpose, DPIA, processor contracts, data-subject rights case register (access, correction, erasure/restriction, portability, objection), breach escalation meeting NDPA §40 timelines, international-transfer record per §§41–43, DPO/registration applicability assessment. | Critical (Nigeria track) | H8, D7 |
| FR-010 | Messaging for reminders is gated behind consent, opt-out, language, minimal content, provider contract and delivery reliability; until then Transitions runs as a staffed coordinator queue with response targets. | High | M9, D8 |
| FR-011 | NHIA/HMO claims are out of scope until eligibility and demand are validated; `PaymentMethod` is not relabelled to represent insurance schemes. | — | review Nigeria section |

---

## 4. Non-Functional Requirements

- Low-end Android: the offline packet must stay small and encrypted; no PHI in notifications.
- Regulatory sources are rechecked at implementation by the compliance owner (final GAID 2025 text could not be fully retrieved during the review).
- Changes preserve existing US records and numeric enum contracts.

---

## 5. Success Criteria

- The Nigerian agency-owner scenario in the review (patient without login, verified proxy, NMCN evidence, NGN schedule/bill/receipt) completes end-to-end on the pilot deployment.
- Lagos and Maryland DST tests pass; remote-coordinator entry cannot shift a schedule.
- Offline capture survives airplane mode, process death and reconnect after revocation on a real low-end device.
- Rights-request and breach tabletop exercises completed with the named owner.

---

## 6. Scope & Boundaries

**In scope**: FR-001..FR-010 for the chosen track; configuration, Domain/Contracts/Web changes, migrations.  
**Out of scope**: NHIA claims, multi-currency FX, AI/OCR, automated emergency contact, general availability in both countries.

---

## 7. Dependencies & Assumptions

- CP-05 and CP-06 complete; one pilot track chosen (D1); compliance owner named (D7).
- Customer interviews in the launch city validate connectivity, device, pricing and workflow assumptions — the review's personas are hypotheses, not data.

---

## 8. Risks & Mitigation

| Risk | Mitigation |
|---|---|
| Country logic leaks into pages and validators | FR-001 policy object is the only source; reviewer rule added to CLAUDE.md when approved |
| Currency backfill misstates historical records | Backfill only under an explicit organization policy; never reinterpret amounts |
| Offline sync creates duplicate or back-dated records | Idempotency keys, provenance, supervisor correction with audit |

---

## 9. Stakeholder Sign-Off

| Role | Name | Decision | Date |
|---|---|---|---|
| Product Owner | Tobi | Pending — decisions D1–D8 | |
| Nigerian compliance owner | — | Pending (D7) | |

---

## 10. Related Documents

- `_specs/reviews/2026-10-22-second-opinion-review.md` — H9, M1–M3, M9, "Country readiness and policy requirements", stakeholder scenarios
- `_specs/sprints/sprint-07-mobile-notifications-ai-hardening.md` — overlapping offline/messaging stories (to be split per the review's disagreement table)

## Revision History

| Date | Change |
|---|---|
| 2026-10-22 | Draft created from the second-opinion review; blocked on product decisions D1–D8 |
