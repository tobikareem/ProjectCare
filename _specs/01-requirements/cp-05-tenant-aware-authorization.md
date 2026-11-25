# CP-05 — Tenant-Aware Identity and Authorization: Requirements

**Status**: Draft (not approved; implementation must not start)  
**Author**: CarePath Health  
**Created**: 2026-10-22  
**Spec type**: Requirements  
**Parent decision**: [ADR 0003 — Multi-Tenant SaaS Database Strategy](../decisions/0003-multi-tenant-saas-database-strategy.md)  
**Predecessor**: [CP-04 Control Plane Foundation](cp-04-control-plane-foundation.md) (implemented 2026-10-21)  
**Successor**: [CP-06 Tenant Data Plane Routing](cp-06-tenant-data-plane-routing.md)  
**Carries findings from**: [Second-opinion review 2026-10-22](../reviews/2026-10-22-second-opinion-review.md) — H1, H2, H3, H4, M4, M5, M8, plus the CP-04 board's own CP-05 deferrals

---

## Executive Summary

Make every protected request's authorization authoritative at request time, make the PHI audit trail durable, and close the identity-plane gaps the CP-04 reviews left open, so that a single-organization pilot with real patient data is defensible before any data-plane routing work begins.

CP-04 proved host → organization resolution and organization-bound tokens. It deliberately deferred the three things that decide whether a dismissed caregiver, a demoted admin, or a stolen token can still reach PHI: per-request validation of the membership's current state, a session model that can be revoked, and a PHI audit sink that survives log rotation. The second-opinion review ranked those as the blockers for either market. CP-05 delivers them, together with the membership-write hardening (invitation acceptance, atomic last-admin guard, refresh-family atomicity) and the scheduling-reservation race that share the same root cause: a decision made from stale state.

---

## 1. Problem Statement

### 1.1 Current State
- `OnTokenValidated` checks token kind, the host organization and that a membership id parses. It never loads the membership, so deactivation, role change or platform-admin removal takes effect only when the access token expires (60 minutes). `TwoOrganizationIntegrationTests.Refresh_AtAnotherHostOrAfterDeactivation_*` pins this as characterization (review H1; D-CP04-05).
- `SecurityVersion` increments on every membership change but is carried in no token and compared nowhere.
- `IPhiAuditLogger` is implemented by `LoggingPhiAuditLogger`, which writes an `ILogger` line and returns. There is no durable, append-only tenant audit table; the IDOR guard records Create/Update/Delete before the service commits, and services also record reads the guard already recorded (review H2; `CODE_REVIEW_TODO.md` §2).
- `PlatformUser` (email, display name, phone) joined to an `OrganizationMembership` with `Role = Client` identifies a person's healthcare relationship. The design calls the control plane non-PHI and asks for a classification sign-off (S8-TASK-002) that the review says cannot make a protected relationship unprotected (review H3).
- An organization Admin can attach any existing platform identity to their organization by email; the identity holder never accepts (review M4). The last-active-Admin guard is a count followed by a separate write (M5). Refresh rotation revokes and inserts in two statements, slides seven days per rotation with no absolute limit, and revokes the whole membership's tokens on reuse rather than one login family (M8).
- `ShiftOperationsService` checks caregiver overlap before opening the write transaction; two coordinators can double-book (review H4). The same stale-read-then-write pattern underlies M5.

### 1.2 Business Impact
- No real-patient pilot in either market can pass an offboarding test today: a dismissed worker keeps PHI access for up to an hour, and the audit trail of what they read is a log line.
- Every CP-06 tenant inherits these gaps; fixing them after routing exists is strictly more expensive.

### 1.3 User Impact
- Organization Admins: role and status changes are not immediate; the admin lockout guard is best-effort.
- Compliance owners: cannot answer "who read this record" from a durable, queryable source.
- Caregivers and coordinators: concurrent scheduling can produce overlapping assignments that only surface in the field.

---

## 2. User Stories

```gherkin
As an Organization Admin
I want a deactivated or demoted member's next request to be refused
So that offboarding is effective the moment I confirm it

Acceptance Criteria:
- Given a member holds a valid access token
- When their membership is set Inactive, or their role is changed, or their platform identity is disabled
- Then their next protected request at that host returns the generic 401 on every API instance
- And their refresh token no longer rotates
```

```gherkin
As a Compliance Officer
I want every PHI read, write, soft delete and denied access recorded in an append-only audit store
So that an investigation does not depend on log retention

Acceptance Criteria:
- Given any PHI endpoint or service operation
- When it completes, fails, or is denied
- Then exactly one audit event per logical operation exists with actor, organization, timestamp, action, outcome, entity type/id and correlation id
- And an audit write failure fails the operation (fail closed), never silently continues
- And no audit row can be updated or deleted through the application
```

```gherkin
As a platform identity holder
I want to accept an organization's invitation before I am its member
So that an agency cannot attach my account without my knowledge

Acceptance Criteria:
- Given an Admin creates a membership for an email that already has a platform identity
- When the request succeeds
- Then the membership is Pending with a signed, expiring, one-use acceptance
- And the response is indistinguishable from the new-identity case (D-CP04-12 still holds)
- And the membership becomes Active only after acceptance at that organization's host
```

```gherkin
As a Coordinator
I want a caregiver reservation to be decided inside the database transaction
So that two of us cannot book the same caregiver for overlapping shifts

Acceptance Criteria:
- Given two overlapping create or update requests for one caregiver arrive concurrently
- When both pass the pre-check
- Then exactly one commits and the other receives the existing overlap conflict
- And the test runs against SQL Server and PostgreSQL, not the in-memory provider
```

---

## 3. Functional Requirements

| ID | Requirement | Priority | Origin |
|----|-------------|----------|--------|
| FR-001 | Every protected request shall load the membership named by the token (or the platform user for platform tokens) and refuse the request when it is missing, Inactive, belongs to another organization, or its `SecurityVersion` differs from the token's `security_version` claim. | Critical | H1, D-CP04-05 |
| FR-002 | Access tokens shall carry `security_version` and a `session_id`; a session (login family) shall be revocable individually, and revocation shall take effect on the next request. A logout endpoint shall revoke the caller's session. | Critical | H1, M8 |
| FR-003 | Refresh tokens shall belong to a session family with an absolute lifetime (configurable, default 30 days) in addition to the sliding 7-day lifetime; rotation (revoke old, insert new) shall be one atomic database operation; reuse after the grace window shall revoke that family only; binding shall be validated before any family-wide effect. | High | M8 |
| FR-004 | A durable tenant PHI audit store shall replace `LoggingPhiAuditLogger`: append-only, with event time, actor, organization id, action, **outcome** (Authorized / Denied / Committed / Failed), entity type and id, correlation id, and background-job name where applicable. Audit writes shall be part of the committing transaction for writes, and an audit write failure shall fail the operation. | Critical | H2 |
| FR-005 | Audit ownership shall be defined once: the IDOR guard records the authorization decision; the service records the committed read or write. No logical operation produces two success rows. Existing tests asserting duplicate rows shall be corrected. | High | H2, TODO §2 |
| FR-006 | Membership creation for an existing platform identity shall create a Pending membership and an acceptance (signed, one-use, expiring, audited); the identity holder accepts at the organization host while authenticated. Responses stay uniform with the new-identity path. | High | M4 |
| FR-007 | Role change and deactivation shall be serialized per organization (serializable transaction or an organization-row lock) so the last-active-Admin invariant holds under concurrent requests; `OrganizationMembership` gains a concurrency token. | High | M5 |
| FR-008 | Caregiver shift reservation (create and update) shall be decided inside the write transaction with a provider-appropriate guard (serializable predicate / `UPDLOCK` range on SQL Server, exclusion constraint or advisory lock on PostgreSQL). | High | H4 |
| FR-009 | The control-plane identity record shall be classified from joined data flows, not column names. Either (a) patient and family identities are kept tenant-local and the control plane holds workforce identity only, or (b) the control plane is governed as potentially health-identifying data (region, audit, contracts, operator access review). The choice is a design decision recorded in CP-05 design before implementation; S8-TASK-002 is reframed accordingly. | Critical | H3 |
| FR-010 | `CopyLegacyIdentityAsync` shall record a one-time completion marker and skip once it has run; `DropLegacyIdentityTables` (TASK-100) ships in this spec after S8-TASK-003 HomeLab verification. | Medium | CP-04 board |
| FR-011 | `User.PlatformUserId` becomes NOT NULL; `User.Role` and `User.IsActive` are removed once every reader uses the membership; `Admin` is renamed `OrganizationAdmin` in policies with a compatibility window. | Medium | CP-04 board |
| FR-012 | Unknown-email logins keep the password-hash timing decoy; platform-admin login denial remains the generic 401. | Low | CP-04 review |

---

## 4. Non-Functional Requirements

- **Per-request cost**: FR-001 adds one indexed control-plane read per protected request. No security-state cache (D-CP04-03). Budget: ≤ 2 ms p95 warm on the HomeLab PostgreSQL.
- **Fail closed**: a control-plane outage returns 503 with no detail; it never falls back to token claims.
- **Provider parity**: FR-003, FR-007 and FR-008 ship with tests on SQL Server and PostgreSQL (Testcontainers or a CI-provisioned instance); the in-memory provider is not evidence for these requirements.
- **HIPAA**: audit rows never carry PHI values; the audit store is covered by the same retention and backup controls as the tenant database.

---

## 5. Success Criteria

- Deactivate, demote, revoke-session and disable-identity each cause the next request to fail on two API instances sharing one control plane.
- A durable audit row exists for every PHI operation exercised by the existing Application test suite, with exactly one success row per logical operation and a Denied row for every denial.
- Concurrent overlapping shift creates on a real provider produce one commit and one conflict, 100 of 100 runs.
- Concurrent last-admin demotions leave at least one Active Admin, 100 of 100 runs.
- The classification decision (FR-009) is recorded and signed; the control-plane entity tests enforce whichever option was chosen.
- HomeLab verification (S8-TASK-003) passes and the legacy Identity tables are dropped by migration.

---

## 6. Scope & Boundaries

**In scope**: FR-001..FR-012, their migrations (control-plane and tenant, both providers), the audit store design, the invitation flow and its Web screen, logout.

**Out of scope**: routing requests to per-organization databases (CP-06); currency, time zone, credential and offline work (CP-07); SMS/voice delivery (Sprint 7); MAUI.

---

## 7. Dependencies & Assumptions

- S8-TASK-002 is replaced by the FR-009 decision; S8-TASK-003 (HomeLab login verification) gates FR-010.
- The durable audit store lives in the tenant database (it is PHI-adjacent) unless the FR-009 decision says otherwise.
- `ControlPlane:AllowMultipleActiveOrganizations` stays false in every non-synthetic environment throughout CP-05.

---

## 8. Risks & Mitigation

| Risk | Mitigation |
|---|---|
| Per-request membership read regresses latency | Single covering index on (Id, OrganizationId, Status, SecurityVersion); measure before/after in the integration host and on HomeLab |
| Audit-in-transaction increases write contention | Audit rows are inserts only; no reads inside the transaction; batch the per-row list audits |
| Invitation flow leaks identity existence | Reuse D-CP04-12 uniform responses; acceptance tokens are opaque and single-use |
| Dropping legacy tables before every account is verified | FR-010 ordered after S8-TASK-003; migration refuses when unverified accounts remain |

---

## 9. Stakeholder Sign-Off

| Role | Name | Decision | Date |
|---|---|---|---|
| Product Owner | Tobi | Pending | |
| Security/Compliance | — | Pending (FR-009 decision) | |

---

## 10. Related Documents

- `_specs/reviews/2026-10-22-second-opinion-review.md` — findings H1–H4, M4, M5, M8 and the proposed Sprint 9 board
- `_specs/02-design/cp-04-control-plane-foundation.md` — D-CP04-05 (SecurityVersion), D-CP04-12 (uniform create), HIPAA residual list
- `_specs/sprints/sprint-08-tasks.md` — "Deferred (explicitly)" list
- `CODE_REVIEW_TODO.md` — §1 authorization consolidation (done for care plans; remaining services), §2 audit ownership

## Revision History

| Date | Change |
|---|---|
| 2026-10-22 | Draft created from the CP-04 deferral list and the second-opinion review findings |
