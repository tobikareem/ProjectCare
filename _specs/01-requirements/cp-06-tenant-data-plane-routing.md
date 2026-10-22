# CP-06 — Tenant Data Plane Routing: Requirements

**Status**: Draft (not approved; implementation must not start)  
**Author**: CarePath Health  
**Created**: 2026-10-22  
**Spec type**: Requirements  
**Parent decision**: [ADR 0003 — Multi-Tenant SaaS Database Strategy](../decisions/0003-multi-tenant-saas-database-strategy.md)  
**Predecessors**: [CP-04](cp-04-control-plane-foundation.md) (implemented) · [CP-05](cp-05-tenant-aware-authorization.md) (draft; must complete first)  
**Carries findings from**: [Second-opinion review 2026-10-22](../reviews/2026-10-22-second-opinion-review.md) — C1, M7, H8, H7 (deployment evidence), M10 (provider-backed proof)

---

## Executive Summary

Route every operational request to the database that belongs to the organization the host resolved to, provision and verify those databases, and prove isolation with two real databases, so that a second organization can be admitted without sharing PHI with the first.

CP-04 resolves the organization and binds tokens to it, but `AddInfrastructure` still registers one `CarePathDbContext` over one connection string. The interim guard added on 2026-10-22 (`ControlPlane:AllowMultipleActiveOrganizations`, default false) refuses a second Active organization at startup and on reactivation. CP-06 removes the need for that guard by making the data plane a per-request, per-organization decision with admission probes, provisioning, migration leases and restore evidence.

---

## 1. Problem Statement

### 1.1 Current State
- `Infrastructure/DependencyInjection.cs` captures one tenant connection string; `OrganizationDataPlane.SecretReference` is stored but never read on a request path (review C1).
- The integration suite shares one tenant context between both organizations, so its cross-host 401s prove token binding, not data isolation.
- `DataRegion` is a mutable string with no runtime meaning; retention and legal-hold policy cannot be expressed per organization (review H8).
- The Blazor client's committed settings point every host at one absolute API URL, so per-agency hosts resolve branding and tokens against the API host, not the host the user navigated to (review M7; D-CP04-14).
- The HomeLab stack has no demonstrated TLS edge, publishes PostgreSQL on 5432, and has no restore evidence (review H7).

### 1.2 Business Impact
- The product cannot admit a paying second agency on the same deployment; the "SaaS" claim rests on CP-06.
- Operating ten agencies by hand (provisioning, migrations, backups) is where the ADR itself says automation becomes mandatory.

### 1.3 User Impact
- Platform operators have no safe way to activate an organization.
- Agency users at their own host may see the wrong branding or fail login until same-origin routing exists.

---

## 2. User Stories

```gherkin
As a Platform Operator
I want to provision and activate a second organization through the API
So that onboarding an agency needs no database access

Acceptance Criteria:
- Given an organization is Registered with a data-plane locator
- When I provision it
- Then its database exists, carries the organization marker and the current schema version, and its secret is stored by reference only
- And activation is refused until every admission probe (marker, schema, region, storage namespace, backup target, audit target) passes
```

```gherkin
As an Admin of organization A
I want every request I make to touch only organization A's database
So that no record of organization B is readable or writable from my host

Acceptance Criteria:
- Given two Active organizations with two real databases
- When any role at host A requests any PHI endpoint with any id from organization B
- Then the response is the generic 404 and zero queries ran against database B
```

```gherkin
As a Compliance Officer
I want each organization's region and retention policy to be enforced, not described
So that a Nigerian and a Maryland agency can be hosted under their own rules

Acceptance Criteria:
- Given an organization's jurisdiction policy names a region and record-class retention schedule
- When the data plane is provisioned, backed up, exported or a legal hold is applied
- Then each of those operations uses that policy and is audited
```

---

## 3. Functional Requirements

| ID | Requirement | Priority | Origin |
|----|-------------|----------|--------|
| FR-001 | `CarePathDbContext` shall be created per request from the resolved organization's `OrganizationDataPlane` through a scoped factory; there is no default or fallback tenant database. Requests at an organization with no provisioned data plane fail closed. | Critical | C1 |
| FR-002 | Each tenant database shall carry an organization marker row (organization id, schema version, region); the factory verifies the marker on first use per connection lifecycle and refuses on mismatch. | Critical | C1, ADR §fail-closed |
| FR-003 | Secrets for data planes shall be resolved by reference (configuration key or vault path) with least-privilege per-database credentials; connection strings are never stored in the control plane. | Critical | ADR |
| FR-004 | A single idempotent provisioner/migrator with a lease per database shall create, migrate and verify tenant databases; schema compatibility between the running app version and each database is checked before admission and on every startup. | High | review "at ten" |
| FR-005 | Admission probes (marker, schema, region, private storage namespace, backup target, audit target) shall gate `Active`; the interim `AllowMultipleActiveOrganizations` guard is removed only when FR-001..FR-005 are live. | Critical | C1 |
| FR-006 | `DataRegion` becomes a verified placement (database, identity governance per the CP-05 FR-009 decision, files, backups, subprocessors) and changes only through a controlled migration workflow with audit. | High | H8 |
| FR-007 | A per-organization jurisdiction/retention policy (record class, start event, minority rule, legal hold, destruction/export) shall exist and be enforced by export, hold and disposition operations; the blanket "six years" text in CLAUDE.md/README is replaced by a reference to the policy. | High | H8 |
| FR-008 | The Web client shall be served same-origin at each verified organization host (empty `Api:BaseAddress`), with CORS/TLS deployment tests; a two-host browser test proves distinct branding and token organizations. | High | M7, D-CP04-14 |
| FR-009 | File/photo storage, caches and background jobs shall be keyed by organization and verified by the same admission probes. | High | CP-04 deferral |
| FR-010 | A documented, tested production boundary shall exist for the first non-synthetic deployment: TLS edge, trusted proxy list, database ingress restricted to the application network, encrypted volumes/backups, restore drill with recorded RPO/RTO. HomeLab stays synthetic unless it meets this. | Critical | H7 |
| FR-011 | Isolation shall be proven with a two-real-database API matrix (every role, guessed ids, files, audit namespaces) run on SQL Server and PostgreSQL in CI; the InMemory suite remains for fast feedback only. | Critical | M10 |

---

## 4. Non-Functional Requirements

- **Connection budget**: measure pool multiplication (instances × databases) and cap per-tenant pools; reserve control-plane capacity.
- **Fleet operations**: canary upgrades, retries with stop thresholds, per-tenant restore without cross-impact; practiced control-plane recovery.
- **No PHI in the control plane** per the CP-05 FR-009 decision; locator rows hold references, never credentials.

---

## 5. Success Criteria

- Two real databases, two Active organizations, the full API matrix green on both providers with zero cross-database queries (asserted through the EF command interceptor).
- Provision → migrate → verify → activate → suspend → restore for a synthetic agency with no effect on the other.
- The interim `AllowMultipleActiveOrganizations` guard is deleted in the same change that proves FR-005.
- Documented production boundary for the first non-synthetic environment, with a restore drill record.

---

## 6. Scope & Boundaries

**In scope**: FR-001..FR-011, provisioning tooling, deployment runbook, CI provider jobs.  
**Out of scope**: market-specific product changes (CP-07), messaging delivery (Sprint 7), billing per tenant (later).

---

## 7. Dependencies & Assumptions

- CP-05 complete (authoritative authorization and durable audit) — routing more tenants onto unaudited access is not acceptable.
- A CI capability to run SQL Server and PostgreSQL (Testcontainers or hosted).
- The FR-009 classification decision from CP-05 determines whether identity is regional.

---

## 8. Risks & Mitigation

| Risk | Mitigation |
|---|---|
| Per-request context creation cost | Factory caches `DbContextOptions` per data plane; connection pooling per database; measure |
| Secret reference resolution adds an external dependency | Cache resolved references for the process lifetime with explicit rotation; fail closed on miss |
| Shared-schema temptation to cut database count | ADR decision stands; revisit only with measured operator cost |

---

## 9. Stakeholder Sign-Off

| Role | Name | Decision | Date |
|---|---|---|---|
| Product Owner | Tobi | Pending | |
| Operations | — | Pending (FR-010) | |

---

## 10. Related Documents

- `_specs/reviews/2026-10-22-second-opinion-review.md` — C1, M7, H7, H8, M10 and the proposed Sprint 10 board
- `_specs/02-design/cp-04-control-plane-foundation.md` — D-CP04-14 (branding host), startup sequence
- `README.md` — `ControlPlane:AllowMultipleActiveOrganizations` (interim guard this spec retires)

## Revision History

| Date | Change |
|---|---|
| 2026-10-22 | Draft created from ADR 0003 slice 3 and the second-opinion review findings |
