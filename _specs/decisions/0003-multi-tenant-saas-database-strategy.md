# ADR 0003: Multi-Tenant SaaS Database Strategy

**Date:** 2026-07-20  
**Status:** In Review  
**Decision owners:** Product Owner, Technical Lead, Security/Compliance Owner  
**Related systems:** Domain, Application, Infrastructure, WebApi, CarePath.Contracts, CarePath.Client, CarePath.Client.UI  

## 1. Purpose

This document plans CarePath's conversion from a single-organization healthcare platform into a white-label SaaS product sold to multiple home-care agencies. It compares:

1. A separate operational database for every organization.
2. A shared operational database where every tenant-owned row contains `OrganizationId`.

The primary decision is the isolation model for PHI and sensitive workforce and financial data. Branding is included because organization resolution, authentication, and data routing must all agree on the same tenant before the UI is rendered or PHI is accessed.

This is a decision and discovery document, not authorization to implement. The requirements, design, and task specifications must be created and approved after this decision is accepted.

## 2. Terminology

- **Organization:** A customer agency using CarePath. This is the preferred domain term; `Caregiver` remains an individual worker.
- **Tenant:** The technical isolation boundary representing an organization.
- **Control plane:** Shared, non-clinical SaaS management data such as organization identity, subscription, branding, domain mapping, database location, and provisioning state.
- **Data plane:** An organization's operational healthcare database containing its users, clients, caregivers, shifts, notes, billing, transitions, and audit events.
- **Platform administrator:** A CarePath operator. This is different from an organization administrator.
- **Organization administrator:** A customer administrator whose authority is limited to one organization.

## 3. Current-State Assessment

CarePath's Clean Architecture is suitable for either tenancy strategy, but the current implementation is single-organization:

- PHI-bearing entities do not have `OrganizationId` or another tenant key.
- `CarePathDbContext` is configured from one connection and exposes all operational entity sets.
- The global EF Core filter enforces soft deletion only; it does not enforce tenant isolation.
- `User.Role` is a single role on the user rather than an organization membership.
- Current `Admin` semantics are platform-wide and must be separated from customer administration.
- Authorization checks protect roles and individual resources, but do not establish an organization boundary.
- Branding, organization provisioning, subscriptions, custom domains, and tenant-aware background processing do not yet exist.

CarePath must not host unrelated agencies in the same production data store until one of the tenant-isolation designs is implemented and verified.

## 4. Decision Drivers

The database strategy must be evaluated against these priorities, in order:

1. Prevent cross-organization PHI disclosure.
2. Make authorization failures fail closed.
3. Support per-organization backup, restore, export, suspension, retention, and legal holds.
4. Keep migrations and incident response operable as the customer count grows.
5. Allow one CarePath codebase and deployment to serve differently branded agencies.
6. Support predictable SaaS pricing and infrastructure costs.
7. Permit future enterprise requirements for dedicated infrastructure.
8. Avoid making platform-wide reporting an uncontrolled copy of PHI.

## 5. Option A: Separate Database per Organization

### 5.1 Proposed Architecture

```text
                       Shared CarePath application
                                  |
                     Resolve organization safely
                                  |
                    +-------------+-------------+
                    | Shared control plane      |
                    | - Organizations           |
                    | - Branding and domains    |
                    | - Subscription/status     |
                    | - Encrypted DB locator    |
                    | - Provisioning state      |
                    | - User memberships*       |
                    +-------------+-------------+
                                  |
              +-------------------+-------------------+
              |                   |                   |
       BrightCare DB       HelpingHands DB       Agency N DB
       - clients           - clients             - clients
       - caregivers        - caregivers          - caregivers
       - shifts/notes      - shifts/notes        - shifts/notes
       - billing           - billing             - billing
       - audit events      - audit events        - audit events
```

`*` The final identity design remains a separate decision. The recommended starting point is centralized authentication and organization membership in the control plane, with tenant-scoped operational user profiles in each data plane. The control plane must not become a convenient store for clinical PHI.

The request pipeline would:

1. Resolve the organization from a verified subdomain or custom domain.
2. Authenticate the user.
3. prove that the user has an active membership in that organization.
4. Issue or validate a token containing immutable `organization_id`, `membership_id`, and user identifiers.
5. Select the organization's registered database without accepting a connection string or tenant ID from the client.
6. create the scoped `CarePathDbContext` for that database.
7. enforce role and object-level authorization inside the selected tenant.
8. record PHI access in that tenant's append-only audit store.

### 5.2 Advantages

- **Strong blast-radius reduction:** A missing tenant predicate in an ordinary repository query cannot return another organization's rows because those rows are not in that database.
- **Simpler tenant-local queries:** Existing entity relationships generally do not require `OrganizationId` on every table and join.
- **Per-organization recovery:** A single customer database can be restored without rolling back every other customer, subject to an approved audit and retention procedure.
- **Cleaner customer offboarding and export:** The organization's operational dataset has a natural physical boundary.
- **Flexible enterprise controls:** High-value customers can receive different regions, performance tiers, encryption keys, retention policies, or maintenance windows.
- **Reduced noisy-neighbor risk:** A large agency can be moved to a larger database tier independently.
- **More direct incident containment:** Credentials or database access can be disabled for one organization without taking every tenant offline.
- **Potentially clearer legal-hold boundaries:** Tenant-local backups and records reduce the need to extract one agency from a shared backup.

### 5.3 Disadvantages

- **Higher operating cost:** Each database has a base compute/storage/backup cost unless the cloud platform provides an effective pooling model.
- **Migration fleet complexity:** Every schema migration must be orchestrated, observed, retried, and version-tracked across all tenant databases.
- **Provisioning complexity:** Customer onboarding must create, configure, migrate, seed, register, and health-check a database transactionally.
- **Harder platform analytics:** Cross-organization dashboards cannot query one transactional database. They need an approved aggregate pipeline with strict PHI minimization.
- **Connection management pressure:** A large tenant count can produce many connection pools and database credentials.
- **More operational artifacts:** Backup policies, restore tests, monitoring, alerts, encryption configuration, and disaster recovery must cover a fleet.
- **Schema skew risk:** Failed or delayed migrations can leave tenants running different schema versions unless deployments fail closed.
- **Support tooling is harder:** Platform support must deliberately select a tenant, justify access, and prevent accidental cross-tenant actions.

### 5.4 Principal Risks and Controls

| Risk | Required control |
|---|---|
| Host header or subdomain spoofing | Resolve only registered, normalized domains behind a trusted proxy; never trust a client-provided `OrganizationId`. |
| Token used against a different tenant | Require token organization claim to exactly match resolved organization; reject before database selection. |
| Wrong database selected | Use a server-side tenant registry keyed by immutable organization ID, typed connection factory, allowlisted server targets, and fail-closed resolution. |
| Connection-string disclosure | Store secrets in an approved secret manager; control-plane records should hold secret references, not plaintext credentials. |
| Migration partially succeeds | Maintain per-tenant schema version and migration status; use staged rollouts, idempotent orchestration where supported, retry policy, and deployment stop thresholds. |
| Destructive rollback destroys PHI | Use forward-only PHI migrations and retention-safe recovery procedures. |
| One tenant is omitted from backup/monitoring | Provision backup, encryption, alerts, and restore-test registration as one automated workflow. |
| Cross-tenant background job | Put immutable organization ID in non-PHI job metadata; resolve the registered database server-side; never serialize PHI into queues unless explicitly approved. |
| Cross-tenant cache or SignalR leak | Prefix cache keys and groups with internal organization ID; authorize membership before joining; do not place PHI in key/group names. |
| Files stored outside the DB leak | Use private organization-scoped storage paths or containers, authorized short-lived access, encryption, malware scanning, and tenant-aware retention. |

## 6. Option B: Shared Database with `OrganizationId` on Every Tenant Row

### 6.1 Proposed Architecture

All organizations share one operational schema. Every tenant-owned record contains a non-null `OrganizationId`. EF Core global filters, repositories, write interceptors, relationships, indexes, authorization, caches, jobs, and tests enforce the boundary.

For important relationships, the database must prevent cross-tenant references. A shift from Organization A must not reference a client from Organization B merely because both IDs are valid. This generally requires tenant-aware alternate/composite keys and foreign keys, or an equivalent database-enforced design.

### 6.2 Advantages

- **Lower initial infrastructure cost:** Small tenants share database capacity.
- **One migration target:** Schema updates are operationally simpler and immediately consistent.
- **Simpler platform-wide aggregation:** Approved aggregate reports can be produced without a fleet extraction pipeline, although authorization and minimum-necessary rules still apply.
- **Simpler connection management:** The application maintains a small number of connection pools.
- **Fast provisioning:** Creating an organization is primarily a data transaction rather than infrastructure provisioning.
- **Efficient for many very small tenants:** Shared compute can provide better utilization.

### 6.3 Disadvantages

- **Higher cross-tenant disclosure risk:** One missing tenant predicate, disabled filter, unsafe raw SQL query, incorrect join, cache collision, or background-job bug can expose another agency's PHI.
- **Tenant key propagates everywhere:** Domain entities, identity, unique indexes, foreign keys, repository methods, audit records, blobs, events, and tests all become tenant-aware.
- **Harder tenant-local restore:** Restoring one organization's data from shared backups requires selective recovery and reconciliation, not a normal database restore.
- **Harder deletion/export/legal hold:** Tenant data is interleaved across tables and backups.
- **Shared performance contention:** One agency's large report or import can affect all customers.
- **More dangerous platform mistakes:** A support query or migration can affect every organization at once.
- **Future dedicated-database move is non-trivial:** Tenant extraction must copy a consistent graph of operational records and preserve audit/retention requirements.

### 6.4 Minimum Controls if Selected

- Non-null `OrganizationId` on every tenant-owned entity, including users, audit events, billing, transitions, and join entities.
- Tenant-aware database constraints that reject cross-organization relationships.
- A scoped tenant context established before `DbContext` creation.
- Combined global filters for `OrganizationId` and `IsDeleted`.
- Save interceptors that reject inserts/updates with missing or mismatched organization ownership.
- No unscoped generic repositories for tenant entities.
- Tenant-scoped uniqueness, for example `(OrganizationId, Email)` where business rules permit.
- Explicit protection and review for `IgnoreQueryFilters`, raw SQL, bulk operations, migrations, jobs, exports, caches, blobs, and SignalR.
- Integration tests that seed at least two tenants and attempt cross-tenant reads, writes, references, exports, and guessed IDs.
- Consider SQL Server Row-Level Security as defense in depth, while recognizing it adds session-context and operational complexity and does not replace application authorization.

## 7. Side-by-Side Assessment

Scores use 1 (weak) to 5 (strong). Cost scores favor lower cost; simplicity scores favor easier operation.

| Decision factor | Separate DB per organization | Shared DB + `OrganizationId` | Notes |
|---|---:|---:|---|
| Cross-tenant PHI isolation | 5 | 3 | Separate databases remove a major class of missing-predicate failures, but application authorization remains mandatory. |
| Initial infrastructure cost | 2 | 5 | Separate databases have a larger per-customer floor. |
| Operational simplicity at small scale | 3 | 5 | One shared schema is easier until tenant-specific restore or incidents occur. |
| Migration simplicity | 2 | 5 | Database fleets require orchestration and schema-version tracking. |
| Per-tenant backup/restore | 5 | 2 | Physical database boundaries are materially easier to recover independently. |
| Customer export/offboarding | 5 | 3 | Separate databases simplify dataset boundaries but files and control-plane data still require coordination. |
| Platform analytics | 2 | 5 | Separate databases need a privacy-reviewed aggregation pipeline. |
| Noisy-neighbor containment | 5 | 2 | Dedicated resources can be scaled or throttled independently. |
| Very large tenant count | 2 | 5 | Thousands of small databases require mature fleet automation. |
| Enterprise customization | 5 | 3 | Dedicated databases allow tenant-specific infrastructure tiers without changing the application model. |
| Future regional placement | 5 | 3 | A database locator can route organizations to approved regions. |
| Developer query simplicity | 4 | 2 | Separate databases reduce tenant predicates but require correct request-to-database routing. |

## 8. Preliminary Recommendation

Use a **shared control plane with a separate operational database per organization** for CarePath's initial SaaS offering, provided the near-term customer count is measured in tens or low hundreds rather than many thousands of micro-tenants.

This recommendation prioritizes PHI isolation, incident containment, tenant-local restore, offboarding, and enterprise flexibility over the lowest possible infrastructure cost. It does not make CarePath HIPAA-compliant by itself and does not remove role, membership, object-level authorization, auditing, encryption, retention, logging, and vendor-agreement requirements.

The recommendation should change to shared-database tenancy if validated business projections show all of the following:

- A very large number of small, price-sensitive agencies.
- Database-per-tenant cost makes the target subscription price unviable.
- The team can implement and continuously test database-enforced tenant relationships, query isolation, job isolation, cache isolation, storage isolation, and tenant-selective recovery.
- Enterprise customers do not require dedicated data infrastructure, or a hybrid extraction/routing model is accepted.

### 8.1 Suggested Evolution Path

1. Begin with separate databases and a database locator abstraction.
2. Keep the application schema identical across tenants; do not fork customer-specific schemas.
3. Automate provisioning, migration, backup registration, restore testing, monitoring, and suspension before onboarding production tenants.
4. Use database/server pooling where the selected SQL hosting platform supports safe cost sharing without combining tenant schemas.
5. If scale later requires it, extend the locator so small tenants can use approved shared database shards while enterprise tenants remain dedicated. This hybrid option must be designed explicitly; it should not emerge through ad hoc exceptions.

## 9. Branding and Tenant Resolution Plan

Branding belongs in the shared control plane because it is needed before the operational database is opened.

Recommended control-plane records:

```text
Organization
- Id (Guid)
- LegalName
- DisplayName
- Slug
- Status
- DefaultTimeZone
- DataRegion
- DatabaseLocatorSecretReference
- SchemaVersion

OrganizationDomain
- Id (Guid)
- OrganizationId
- HostName
- IsPrimary
- VerificationStatus

OrganizationBranding
- OrganizationId
- LogoStorageKey
- PrimaryColorToken
- AccentColorToken
- SupportEmail
- SupportPhone

OrganizationMembership
- Id (Guid)
- OrganizationId
- PlatformUserId
- OrganizationRole
- Status
```

Rules:

- Default URL: `{slug}.carepathhealth.com`.
- Optional custom domain after ownership verification and certificate provisioning.
- Slugs and domains must be globally unique and normalized.
- Branding supports only approved theme tokens and validated private logo assets; no arbitrary customer CSS, HTML, or JavaScript.
- Authentication pages may display branding, but tenant identity must come from a verified domain—not from branding data sent by the browser.
- The resolved organization, membership, token claim, database locator, storage scope, and audit scope must all match.
- A disabled or suspended organization must fail closed while preserving records and retention obligations.

## 10. Identity and Authorization Decisions Still Required

Before implementation, approve answers to these questions:

1. Can one person belong to multiple organizations with one login?
2. Is an email globally unique, or unique only within an organization?
3. Will customer users switch organizations in one session, or authenticate through a tenant-specific URL each time?
4. What exact support access can `PlatformAdmin` receive, and does it require time-limited approval, reason capture, and enhanced audit?
5. Which roles are organization roles versus platform roles?
6. Can an organization manage its own identity provider through SSO in a later tier?

Preliminary preference: centralized authentication, tenant-specific entry URLs, explicit organization memberships, one active organization per access token, and no routine platform-administrator access to PHI.

## 11. Delivery Plan

### Phase 0: Product and Architecture Decisions

- Approve database isolation model and expected three-year tenant scale.
- Define subscription tiers, target price floor, and whether dedicated database cost is included.
- Decide centralized identity and multi-organization membership behavior.
- Define platform support-access policy.
- Define regional hosting, recovery objectives, retention, offboarding, and legal-hold expectations.
- Conduct legal/compliance review of CarePath's business-associate responsibilities and customer BAA model.

**Exit gate:** ADR accepted and unresolved decisions have named owners and deadlines.

### Phase 1: Approved Specifications and Threat Model

- Create the three linked CarePath specifications: requirements, design, and atomic tasks.
- Inventory every tenant-owned entity and non-database artifact.
- Threat-model tenant resolution, authentication, database routing, jobs, caching, files, messaging, SignalR, support access, exports, and analytics.
- Define control-plane/data-plane data classification and prohibit PHI from the control plane unless specifically approved.
- Define measurable isolation, recovery, availability, and migration success criteria.

**Exit gate:** Requirements and design approved; tasks ready; security/compliance review complete.

### Phase 2: Control Plane Foundation

- Add organization, domain, branding, membership, subscription/status, database registry, and provisioning models.
- Implement verified hostname resolution behind the deployment proxy.
- Implement tenant-aware current-user context and token validation.
- Split `PlatformAdmin` from `OrganizationAdmin` semantics.
- Build an internal provisioning state machine with idempotency and audit events.

**Exit gate:** A test organization can be safely resolved and authenticated without accessing PHI.

### Phase 3: Tenant Data Plane and Routing

- Make `CarePathDbContext` creation tenant-aware through a server-side database locator.
- Define tenant database bootstrap, migrations, seed policy, encryption, backup registration, monitoring, and health checks.
- Move operational user profiles and all PHI workflows behind tenant database routing.
- Scope blobs, cache keys, SignalR groups, queued jobs, exports, and audit events to the resolved organization.
- Prevent connection reuse or context leakage between organizations.

**Exit gate:** Automated two-tenant tests prove database, file, cache, job, hub, and API isolation.

### Phase 4: White-Label Experience

- Add organization branding contracts and a validated configuration UI.
- Apply branding through CarePath.Client.UI design tokens while preserving the wireframe system and accessibility.
- Support subdomain routing, then verified custom domains.
- Keep one shared application build; prohibit tenant-specific code forks.

**Exit gate:** Two agencies display distinct approved branding without changing authorization or data routing.

### Phase 5: Fleet Operations

- Create tenant-aware migration orchestration with canary rollout, version tracking, retries, and stop thresholds.
- Automate backup validation and tenant-local restore drills.
- Add provisioning, suspension, reactivation, export, and offboarding runbooks.
- Add per-tenant health, capacity, cost, and security monitoring without PHI in telemetry.
- Add privacy-reviewed aggregate analytics where justified.

**Exit gate:** A production-readiness exercise provisions, upgrades, restores, suspends, and exports a synthetic tenant successfully.

### Phase 6: Pilot and General Availability

- Pilot with synthetic data, then a tightly controlled design partner after contractual and compliance gates.
- Run penetration testing focused on tenant-boundary failures and IDOR.
- Verify BAAs and appropriate agreements with customers and infrastructure/service providers.
- Verify encryption, backup, disaster recovery, incident response, access review, vulnerability management, and audit-monitoring evidence.
- Establish SLOs, support process, breach/incident escalation, and capacity thresholds.

**Exit gate:** Product, engineering, security/compliance, and operations approve general availability.

## 12. Verification Strategy

At minimum, automated tests must cover:

- Unknown, malformed, duplicated, disabled, and spoofed tenant domains.
- Token organization mismatch and inactive membership.
- Same user with memberships in two organizations.
- Cross-organization guessed IDs returning indistinguishable denied/not-found behavior where required.
- Database locator returning no match, stale secret, unavailable database, or wrong schema version.
- Concurrent requests for different organizations on reused server threads/scopes.
- Background job, retry, cache, blob, SignalR, export, and audit isolation.
- Platform administrator denied PHI unless an approved elevated-access flow is active.
- Organization-local backup restore and recovery-point verification.
- Migration canary failure, partial fleet failure, retry, and deployment halt.
- Suspended tenants denied service without destructive deletion.
- Branding validation, domain verification, asset authorization, and accessibility.

Production evidence must additionally verify encryption at rest and in transit, backups, restore exercises, secret management, BAAs, audit review, retention, legal holds, and incident-response processes. These cannot be proven solely by application unit tests.

## 13. Cost Model to Validate Before Final Decision

Obtain real vendor pricing and estimate all values monthly at 10, 50, 100, 500, and 1,000 organizations:

```text
Separate-database monthly cost =
  control plane
  + application hosting
  + database pool/base cost
  + per-tenant database storage and backup
  + monitoring/logging
  + provisioning and migration operations
  + disaster-recovery replica cost

Shared-database monthly cost =
  control plane/application hosting
  + shared database compute/storage/backup
  + monitoring/logging
  + engineering and testing cost for pervasive tenant enforcement
  + tenant-selective export/restore tooling
  + expected enterprise dedicated-database exceptions
```

Do not decide from database list price alone. Include engineering labor, on-call burden, restore complexity, incident blast radius, customer security requirements, and the revenue enabled by a dedicated-data tier.

## 14. Decision Questions for Review

The product owner should answer these before this ADR becomes `Accepted`:

1. How many paying organizations are expected after 12, 24, and 36 months?
2. What are the smallest and largest expected caregiver/client counts per organization?
3. What monthly subscription price and gross-margin target must the architecture support?
4. Is a dedicated database a standard feature or an enterprise tier?
5. Does one login need access to multiple agencies?
6. Are custom domains required at launch or after subdomain launch?
7. Are organizations limited to one US hosting region initially?
8. What recovery point objective and recovery time objective will be promised?
9. Is cross-organization benchmarking required, and can it use de-identified or aggregate data only?
10. What platform-support access to customer PHI is contractually and operationally acceptable?

## 15. HIPAA/PHI Engineering Review

**Scope:** Proposed multi-tenant SaaS and white-label architecture.  
**Impact gate:** Triggered—this changes who can access every PHI-bearing record and how data is stored, routed, audited, backed up, and recovered.

### Findings

#### Critical

- The current single-database implementation has no organization boundary. It must not serve unrelated agencies as tenants until the selected isolation model and cross-tenant verification are complete.
- Database-per-organization does not protect against wrong-database routing. Tenant resolution, membership, token claims, locator results, files, caches, jobs, and audit scope must match and fail closed.

#### Warnings

- HIPAA compliance, TDE/encryption, backups, legal holds, BAAs, provider configuration, and operational monitoring cannot be established by this design document or source code alone.
- Centralized platform support creates a high-risk access path and needs an explicit, audited, minimum-necessary elevated-access design.
- Platform analytics can become a new cross-tenant PHI store and must be excluded until separately specified and approved.

#### Information

- HHS guidance indicates that a software or cloud provider that maintains or accesses ePHI on behalf of a regulated customer may be a business associate and requires appropriate contracts and safeguards. Product counsel and a qualified compliance owner should determine the exact obligations for CarePath and each customer relationship.

### Recommendation

**PASS WITH WARNINGS for continued planning only.** Do not begin multi-agency production onboarding until an approved specification, threat model, implementation, isolation tests, operational evidence, and contractual/compliance gates are complete.

## 16. Proposed Decision

**Proposed:** Adopt a shared non-PHI control plane and a separate operational database per organization for the initial CarePath SaaS release. Preserve a database-locator abstraction so a deliberately designed hybrid strategy remains possible later. Keep schemas uniform across organizations and automate the entire database lifecycle.

**Status:** In Review. No architectural option is accepted until the questions in Section 14 are answered and the cost model is validated with the intended hosting provider.

