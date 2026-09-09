# ADR 0003: Multi-Tenant SaaS Database Strategy

**Date:** 2026-07-20  
**Decided:** 2026-09-08  
**Status:** Accepted  
**Decision owners:** Product Owner, Technical Lead, Security/Compliance Owner  
**Related systems:** Domain, Application, Infrastructure, WebApi, CarePath.Contracts, CarePath.Client, CarePath.Client.UI  

## 1. Purpose

This document plans CarePath's conversion from a single-organization healthcare platform into a white-label SaaS product sold to multiple home-care agencies. It compares:

1. A separate operational database for every organization.
2. A shared operational database where every tenant-owned row contains `OrganizationId`.

The primary decision is the isolation model for PHI and sensitive workforce and financial data. Branding is included because organization resolution, authentication, and data routing must all agree on the same tenant before the UI is rendered or PHI is accessed.

This ADR records the accepted decision. It is not authorization to implement: the requirements, design, and task specifications (Phase 1 in Section 11) must be created and approved before code changes begin.

## 2. Terminology

- **Organization:** A customer agency using CarePath. This is the preferred domain term; `Caregiver` remains an individual worker.
- **Tenant:** The technical isolation boundary representing an organization.
- **Control plane:** Shared, non-clinical SaaS management data such as organization identity, subscription, branding, domain mapping, database location, and provisioning state.
- **Data plane:** An organization's operational healthcare database containing its users, clients, caregivers, shifts, notes, billing, transitions, and audit events.
- **Platform administrator:** A CarePath operator. This is different from an organization administrator.
- **Organization administrator:** A customer administrator whose authority is limited to one organization.

## 3. Current-State Assessment

A code review on 2026-09-08 (branch `postgres`) confirmed that CarePath is single-organization by construction. Clean Architecture, the repository pattern in Application, the convention-driven soft-delete filter, and fail-closed object authorization make tenancy tractable, but nothing tenant-aware exists yet.

### 3.1 Persistence

- `BaseEntity` (`Domain/Entities/Common/BaseEntity.cs`) carries `Id`, audit fields, and `IsDeleted` only. None of the 19 domain entities, `ApplicationUser`, or the ASP.NET Identity tables reference an organization.
- The only global query filter is soft deletion, applied by convention in `CarePathDbContext.ApplyBaseEntityConventions`. It does not enforce tenant isolation.
- `AddInfrastructure` resolves one provider and one connection string at startup and registers a single `CarePathDbContext`. There is no per-request context factory.
- Two providers are maintained with separate migration assemblies: SQL Server (`Infrastructure/Migrations`, six migrations) and PostgreSQL (`Infrastructure.Migrations.PostgreSql`, one squashed `InitialCreate`).
- `Database:AutoMigrate` runs migrate-and-seed inside `Program.cs` at boot. This is acceptable for one database and unsuitable for a fleet.
- Six infrastructure files query `CarePathDbContext` directly outside the generic repository (`BillingEligibilityQuery`, `BillingReconciliationStore`, `ShiftBillingQuery`, `AssignmentHistoryQuery`, `ClientAccessEvaluator`, `IdentityService`), about 21 query sites. Five of those call `IgnoreQueryFilters()`, which would also drop any tenant filter added as a global filter.
- Globally unique constraints that assume one organization: domain `User.Email`, the ASP.NET `UserNameIndex`, and `Invoice.InvoiceNumber`. `Caregiver.UserId` and `Client.UserId` are unique, so one domain user cannot be a caregiver at two agencies.
- Three foreign keys could bridge organizations in a shared schema with no structural guard: `Shift.CaregiverId`, `ClientAccessGrant.GranteeUserId`, and `BillingReconciliationResolution.ResolvedByUserId`.

### 3.2 Identity and authorization

- The JWT carries `sub`, `email`, `jti`, and role claims only (`JwtTokenService`). `ICurrentUserContext` exposes user id, name, roles, and correlation id. Neither has an organization.
- `User.Role` is a single role on the user rather than an organization membership; `IdentityRoleManagementService` enforces one role per user.
- `Sprint4ObjectAuthorizationService` authorizes `Admin` and `Coordinator` for every resource before any per-object check. Current `Admin` semantics are therefore platform-wide and must be separated from customer administration.
- `IdorGuard` protects item routes only. List and search endpoints go straight to repositories and can only be isolated at the `DbContext` level.
- `AdminUserManagementService` checks email uniqueness globally and enforces a "last active admin" invariant across the whole user table.
- The Blazor client (`CarePath.Web`) logs in with email and password only, builds its principal from two claims (name and role), and has no organization selection.

### 3.3 Audit, storage, jobs, configuration

- `LoggingPhiAuditLogger` writes PHI audit entries to Serilog. There is no append-only audit table and `PhiAuditEntry` has no organization field.
- `LocalFileStorageService` writes all objects to one flat directory keyed by GUID, with no organization prefix and no signed read URLs.
- No hosted services, SignalR hubs, or queues exist. Transition reminders are persisted with `Scheduled` status and never dispatched; the future dispatcher will be the first execution path without an HTTP user context.
- JWT issuer, audience, signing key, CORS origins, storage root, and Data Protection application name are single global values.
- Maryland is baked into the model: `User.State` defaults to `"Maryland"`, `CertificationType` is scoped to the Maryland Board of Nursing, and billing thresholds (`BillingMath`, `BillingReconciliationService`) are compile-time constants rather than tenant settings.

CarePath must not host unrelated agencies in the same production data store until the tenant-isolation design in this ADR is implemented and verified.

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

`*` Identity design is decided in Section 10: centralized authentication and organization memberships live in the control plane, with tenant-scoped operational user profiles in each data plane. The control plane must not become a convenient store for clinical PHI.

The request pipeline would:

1. Resolve the organization from a verified subdomain or custom domain.
2. Authenticate the user.
3. prove that the user has an active membership in that organization.
4. Issue or validate a token containing immutable `organization_id`, `membership_id`, and user identifiers.
5. Select the organization's registered database without accepting a connection string or tenant ID from the client.
6. Verify database identity and schema compatibility before creating the scoped `CarePathDbContext` (Section 8.2).
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
| Wrong database selected | Verify the database's immutable organization marker and deployment ID against the registry before operational queries; use database-scoped runtime credentials and the checks in Section 8.2. |
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
| Noisy-neighbor containment | 3 | 2 | Elastic pools share compute; per-database limits and pool headroom reduce contention. Dedicated capacity provides stronger isolation (Section 8.7). |
| Very large tenant count | 2 | 5 | Thousands of small databases require mature fleet automation. |
| Enterprise customization | 5 | 3 | Dedicated databases allow tenant-specific infrastructure tiers without changing the application model. |
| Future regional placement | 5 | 3 | A database locator can route organizations to approved regions. |
| Developer query simplicity | 4 | 2 | Separate databases reduce tenant predicates but require correct request-to-database routing. |

## 8. Decision

CarePath adopts a **shared identity and SaaS management control plane with a separate operational database per organization** for the initial SaaS release. The planning assumption is **tens of agencies (up to about 50)** over three years. A shared Azure SQL elastic pool is the initial tenant capacity model, subject to workload sizing and Section 8.7. Scripts may support synthetic development; automated provisioning, migrations, and recovery are required before production tenant onboarding. This revision strengthens the accepted architecture without changing the database-per-organization decision.

This prioritizes PHI isolation, incident containment, tenant-local restore, offboarding, and enterprise flexibility over the lowest possible infrastructure cost. It does not make CarePath HIPAA-compliant by itself and does not remove role, membership, object-level authorization, auditing, encryption, retention, logging, and vendor-agreement requirements.

### 8.1 Database engine

- **Azure SQL** is the production engine for every tenant operational database and for the shared control plane. Elastic pools provide the cost-sharing model for database-per-tenant. Azure SQL TDE provides encryption at rest for the database layer and forms part of the overall encryption-at-rest controls; files, backups, exports, logs, and secrets carry their own controls (Sections 5.4 and 15).
- **PostgreSQL** remains a supported development and HomeLab provider used to validate persistence portability. It is not an initial production tenant option.
- Separate migration assemblies (`Infrastructure/Migrations` for SQL Server, `Infrastructure.Migrations.PostgreSql`) are retained while engineering supports both providers. Every schema change must be added to both. CI must verify that both migration models represent the same domain schema intent and that neither provider has uncommitted model changes; provider-specific store types, index definitions, and annotations may legitimately differ, so byte-for-byte snapshot equivalence is not required.

### 8.2 Request pipeline

Every request that can reach PHI follows this order. Each step fails closed; no later step runs if an earlier one does not produce a verified result.

```text
Request arrives
  1. Resolve organization from the verified host name (subdomain or custom domain)
  2. Validate the access token signature, issuer, audience, lifetime, and required claims
  3. Match token organization to resolved host; validate current identity, membership,
     session, role, and security versions against authoritative control-plane state
  4. Require active organization status and a ready, application-compatible registry entry
  5. Open a restricted metadata connection and verify actual database identity and schema
  6. Create the tenant-scoped CarePathDbContext on the verified connection
  7. Run role and object-level authorization inside that tenant database
  8. Record PHI access in that tenant's append-only audit store
```

Steps 1 to 4 use the control plane; step 5 reads only database bootstrap metadata. No operational record may be queried before these checks succeed. A signed token carries organization identity, but client input never determines the connection string or database name. Identity and membership metadata require explicit classification under Section 8.6.

Each tenant database contains exactly one provisioning-controlled metadata record with immutable `OrganizationId`, a `DatabaseDeploymentId` unique to that deployed database, and `SchemaVersion`. The registry holds the expected organization, deployment ID, location revision, readiness state, and verified schema version. A restored or relocated database receives a new deployment ID through the privileged recovery workflow; its organization ID is preserved. Runtime credentials can read this metadata but cannot modify it.

```text
Registry missing/not ready, actual organization or deployment ID mismatched,
actual schema differs from registry, or schema outside the supported set
        ↓
reject tenant traffic (503 with no tenant detail, non-PHI platform audit event)
        ↓
dispose the metadata connection; do not create the operational DbContext,
query operational tables, retry another tenant, or fall back to a default database
```

Verify metadata on every connection checkout used for operational work, including pooled connections, retries, jobs, and support sessions. Bind the resulting context immutably to the organization and registry revision; reopening a connection requires verification again. Route changes first block new work and drain or cancel existing scopes. There is no cached database-identity bypass. Runtime database principals are scoped to one tenant database with no schema-management privilege; provisioning/migration principals are separate. The shared application remains a common trust boundary even with separate principals.

Tenant-local records inherit their organization scope from this verified context. Authorization must compare that scope with the authenticated organization before role shortcuts, including list queries; it must not assume an `OrganizationId` property exists on every entity. Files and messages carry independently checked organization scope. Section 8.4 defines schema admission and migration coordination.

### 8.3 Evolution path

1. Begin with separate databases and a database locator abstraction.
2. Keep the application schema identical across tenants; do not fork customer-specific schemas.
3. Automate provisioning, migration, backup registration, restore testing, monitoring, and suspension before onboarding production tenants.
4. Use Azure SQL elastic pools for cost sharing without combining tenant schemas.
5. Reassess measured workload, cost, and fleet operability when scale exceeds the planning range. Additional pools or dedicated capacity preserve this architecture. Shared-schema sharding requires a separate approved ADR and is not an automatic consequence of reaching 50 organizations.

### 8.4 Schema compatibility and fleet migration

- Each application release declares an explicit, tested set of supported tenant schema versions; an unlisted version is rejected under Section 8.2. A single version remains valid for releases that use maintenance windows. Version proximity alone does not prove compatibility.
- Use additive schema changes followed by application rollout and later cleanup when rolling availability is required. Every application version still serving traffic must support the admitted schema. Cleanup waits until older instances and jobs are drained and protected data is preserved; PHI migrations remain forward-only.
- Before migrating a tenant, mark it unavailable for new operational work, drain requests/jobs, and acquire a per-tenant migration lease. Perform the migration, validate the actual schema, then publish the verified registry version and readiness state. A lease expiry or process crash must leave the tenant unavailable until reconciliation confirms completion; it must not reopen traffic automatically.
- The database's applied migration history is authoritative for schema state; registry `SchemaVersion` is an orchestrator-maintained admission projection. Because those stores cannot be updated atomically, disagreement rejects traffic until orchestration reconciles them. Application requests never migrate or repair metadata.
- Canary failures stop fleet rollout at approved thresholds. Rollback may restore application binaries only when they support the current schema; otherwise keep the affected tenant in maintenance and deploy a forward fix. Phase 1 must approve compatibility tests, maintenance duration, stop thresholds, and deployment ordering for both data-plane and control-plane schema changes.

### 8.5 Tenant restore and audit continuity

- Restore to a new, isolated database. Preserve the source database and all recoverable post-restore-point audit evidence under retention and legal-hold rules. Append-only tables alone do not preserve audit continuity when an older database becomes active.
- Before production onboarding, approve a tenant-scoped immutable audit archive or equivalent recovery-independent evidence store, with restricted access and a tested recovery objective. It is not a platform analytics store. Preserve actor, action, outcome, sequence/correlation, and original timestamps without clinical values; record recovery boundaries and any unrecoverable evidence gap explicitly.
- Suspend tenant HTTP traffic and jobs; drain in-flight operations and fence older workers using the registry location revision. Recover and validate the database, reconcile its audit history with preserved evidence, and apply forward migrations to a supported schema before admission.
- Reconcile referenced file versions, exports/signed access, pending jobs, retries, and completed external deliveries. Preserve delivery/idempotency evidence outside the rewound operational state so a restored reminder or billing workflow cannot repeat an already completed external action. Do not resume workers until reconciliation passes.
- Keep current control-plane memberships, revocations, and suspension status authoritative. A tenant restore must not restore privileges. Validate tenant-local user-profile links against current membership state before reopening access.
- Assign a new deployment ID, publish the new location revision through a conditional registry update, invalidate routing caches and old connection pools, and resume only after identity, schema, file, audit, and authorization checks pass. Failed cutover keeps the tenant suspended; returning to a previous database after new writes requires reconciliation, not a blind locator reversal.
- Phase 1 must set RPO/RTO separately for operational records, audit evidence, files, and control-plane state, including regional loss and unavailable source-database scenarios. Recovery drills must demonstrate the full cutover and evidence preservation, not only successful SQL restore.

### 8.6 Control-plane resilience and classification

- The control plane is shared security-critical infrastructure. Identifiable `Client` memberships can reveal a healthcare relationship; do not label the entire store non-PHI. Phase 1 must classify identity/membership records and apply the approved access, audit, encryption, retention, and provider safeguards. Clinical documents and operational clinical content remain in tenant data planes. Platform event payloads exclude patient-identifying membership details and clinical values.
- Initial authorization uses authoritative control-plane reads on every protected request and before each job or hub delivery that can disclose PHI. Status, role/security versions, session revocation, and routing readiness must not be accepted from stale caches or lagging replicas. If those checks time out or are unavailable, return a PHI-safe 503 and perform no operational query; pause jobs with bounded retry/backoff. A valid JWT alone does not authorize offline access.
- Public approved branding may remain cached during an outage, but cannot authorize access or choose a database. Any future cache for security decisions requires a separately approved maximum staleness and demonstrated revocation bound; cache invalidation events alone are insufficient.
- Set control-plane availability and recovery objectives before implementation, with bounded dependency timeouts, circuit breakers, retry budgets, failover tests, and protected recovery procedures. Reserve capacity separately from tenant workloads so tenant pool saturation does not exhaust the identity/routing database. Never use another organization or a default database as an availability fallback.
- A control-plane restore must reconcile current registry deployments, memberships, and revocation evidence before protected traffic resumes. Increment a protected global authentication epoch outside the restored state to invalidate pre-recovery tokens and refresh sessions; this prevents recovery from reviving revoked credentials. Define and test the epoch's durable storage and recovery in Phase 1.

### 8.7 Elastic-pool isolation and capacity assumptions

- A database per organization isolates rows and database permissions, not compute, application availability, or regional failures. Tenant databases in one elastic pool share finite resources. The 3/5 containment score assumes enforced per-database limits and monitored headroom; it is not a dedicated-performance guarantee.
- Size for concurrent peak scheduling, reporting, imports, jobs, migrations, and restore headroom, rather than organization count alone. Phase 1 must specify per-database resource bounds, application concurrency/rate limits, connection-pool budgets, query timeouts, and pool saturation alerts supported by the selected service tier.
- Approve measured latency/error thresholds, sustained utilization thresholds, and an accountable operator for scaling a pool or moving a busy tenant to another pool/dedicated capacity. Exercise a noisy tenant beside a normal tenant and verify the latter's SLO under the configured controls before onboarding.
- Include reserved control-plane capacity, additional pools, audit archives, restore overlap, and disaster recovery in Section 13 estimates. Do not promise dedicated compute or independent outage containment in the standard pooled tier.

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
- SchemaVersion   (verified projection of applied migrations; see 8.4)
- DatabaseDeploymentId
- LocationRevision
- ReadinessState  (provisioning, ready, maintenance, recovering, failed)

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
- SecurityVersion (changes atomically with role/status changes)
```

Rules:

- Launch URL model: `{slug}.carepathhealth.com` for every organization, served under a wildcard certificate behind a trusted proxy that sets the host header.
- Verified custom domains are a later tier, added after ownership verification and certificate provisioning.
- Slugs and domains must be globally unique and normalized.
- Branding supports only approved theme tokens and validated private logo assets; no arbitrary customer CSS, HTML, or JavaScript.
- Authentication pages may display branding, but tenant identity must come from a verified domain—not from branding data sent by the browser.
- The resolved organization, membership, token claim, database locator, storage scope, and audit scope must all match.
- A disabled or suspended organization must fail closed while preserving records and retention obligations.

## 10. Identity and Authorization Decisions

The following decisions are recorded and supersede the open questions in earlier drafts.

| Question | Decision |
|---|---|
| Can one person belong to multiple organizations with one login? | **Yes.** One platform identity, many memberships. This supports 1099 contractors who work for several agencies. |
| Is an email globally unique, or unique only within an organization? | **Globally unique in the control plane.** Email identifies the platform identity. The per-tenant `User` profile is keyed by the platform user id, not by email. |
| Where do credentials and ASP.NET Identity tables live? | **Control plane only.** `AspNetUsers`, password hashes, refresh tokens, lockout state, and memberships live in the control-plane database. Tenant databases hold only the tenant-local operational `User` profile. |
| How does a user reach their organization? | **Tenant-specific entry URL.** The subdomain resolves the organization before login. |
| How does a user with several memberships switch organizations? | **New token per organization, one active organization per token.** Switching re-issues an access token with a different `organization_id` claim. No token ever spans two tenants. |
| Which roles are organization roles versus platform roles? | `Admin` (renamed `OrganizationAdmin`), `Coordinator`, `Caregiver`, `Client`, `FacilityManager`, and `Clinician` are organization roles. `PlatformAdmin` is a control-plane role that never appears in a tenant token. |
| What is the source of truth for a user's role in an organization? | **`OrganizationMembership` in the control plane is authoritative.** The role is copied into the token but accepted only after current membership role and security-version validation (Section 10.1). The tenant `User` profile does not carry a role. |
| What access does `PlatformAdmin` have to customer PHI? | **None by default.** Platform administrators manage organizations, subscriptions, domains, and provisioning. PHI access requires a break-glass elevated session that is time-boxed, approved, records a reason, and produces enhanced audit events in both the control plane and the tenant audit store. |
| Can an organization bring its own identity provider? | Deferred to a later tier. The control-plane identity design must not preclude external SSO per organization. |

Consequences for the current code:

- `Infrastructure/Identity/ApplicationUser` and the Identity `DbContext` registration move to a control-plane context; `CarePathDbContext` stops inheriting from `IdentityDbContext`.
- `User.Role` is removed from the tenant `User` entity. Role checks read the membership role from the token; the single-role enforcement in `IdentityRoleManagementService` moves to control-plane membership management.
- The unconditional `Admin`/`Coordinator` shortcut in `Sprint4ObjectAuthorizationService` is replaced by a verified-context organization precondition evaluated before any role shortcut (Section 8.2), with current token security state validated under Section 10.1.
- Email uniqueness checks in `AdminUserManagementService` and `IdentityProvisioningService` become control-plane operations; the "last active admin" invariant is evaluated per organization.
- `ICurrentUserContext` gains `OrganizationId` and `MembershipId`; `AuthTokenResponse` and the Blazor authentication state provider surface the same values.

### 10.1 Token invalidation and authorization freshness

- Tokens carry immutable user, organization, membership, and session identifiers, the membership security version, user security version, and global authentication epoch. Roles and these versions are issued from a consistent control-plane authorization snapshot. Compare all identifiers, versions, current role, and active user/membership/organization/session state before accepting the token for PHI access.
- Role changes and membership disable/removal atomically increment membership `SecurityVersion`; user disable, credential compromise, and global sign-out increment the user security version. Session logout revokes that session. Retain revocation state for the required token/session lifetime and preserve it through recovery. An old version or revoked session rejects authorization even when JWT signature and expiry remain valid.
- Authorization checks starting after a committed revocation must reject the old token. Phase 1 must bound and test already admitted requests; long-running exports, streams, and privileged writes must recheck before subsequent disclosure or irreversible action. Hub deliveries and user-delegated jobs apply the same checks. Service jobs use explicit tenant-scoped service authority and current organization/readiness checks rather than a stored user token.
- Refresh tokens are server-tracked, rotated on use, and bound to their user, session, and organization. Refresh revalidates current security state; reuse revokes the affected token family. Switching organization requires a fresh membership check and tenant-scoped issuance; it cannot broaden an existing token. Phase 1 defines access/refresh lifetimes and cross-subdomain session handling without placing tokens in URLs.
- Break-glass sessions have separately approved scope, expiry, and revocation checked against current state, with actor attribution and tenant audit. Membership checks must not accidentally create a routine platform-admin bypass.
- On authoritative-store failure, apply Section 8.6; do not continue with the role embedded in an otherwise valid token. Automated tests must prove downgrade, suspension, logout, refresh replay, and recovery invalidate access across application instances.

## 11. Delivery Plan

### Phase 0: Product and Architecture Decisions

- Approve database isolation model and expected three-year tenant scale.
- Define subscription tiers, target price floor, and whether dedicated database cost is included.
- Decide centralized identity and multi-organization membership behavior.
- Define platform support-access policy.
- Define regional hosting, recovery objectives, retention, offboarding, and legal-hold expectations.
- Conduct legal/compliance review of CarePath's business-associate responsibilities and customer BAA model.

**Exit gate:** ADR accepted and unresolved decisions have named owners and deadlines. *Status 2026-09-08: architecture, engine, scale, identity, and platform-access decisions are recorded in Sections 8 and 10. Subscription tiers, price floor, regional hosting, recovery objectives, and the customer BAA model remain open (Section 14).*

### Phase 1: Approved Specifications and Threat Model

- Create the three linked CarePath specifications: requirements, design, and atomic tasks.
- Inventory every tenant-owned entity and non-database artifact.
- Threat-model tenant resolution, authentication, database routing, jobs, caching, files, messaging, SignalR, support access, exports, and analytics.
- Define control-plane/data-plane data classification and prohibit PHI from the control plane unless specifically approved.
- Define measurable isolation, recovery, availability, and migration success criteria, including every contract in Sections 8.2, 8.4-8.7, and 10.1. Technical Lead owns routing, token, schema, and resilience design; Security/Compliance Owner owns classification and audit/retention review; Product Owner owns service objectives and capacity economics. All thresholds, recovery objectives, and unresolved implementation choices require recorded approval before Phase 1 exits.

**Exit gate:** Requirements and design approved; tasks ready; security/compliance review complete.

### Phase 2: Control Plane Foundation

- Add organization, domain, branding, membership, subscription/status, database registry, and provisioning models.
- Implement verified hostname resolution behind the deployment proxy.
- Implement tenant-aware current-user context, authoritative token/session invalidation, and control-plane outage/recovery behavior.
- Split `PlatformAdmin` from `OrganizationAdmin` semantics.
- Build an internal provisioning state machine with idempotency and audit events.

**Exit gate:** A test organization can be safely resolved and authenticated without accessing PHI.

### Phase 3: Tenant Data Plane and Routing

- Make `CarePathDbContext` creation tenant-aware through a server-side database locator with independent database-marker verification, scoped runtime credentials, and connection-reopen checks.
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

- Create tenant-aware migration orchestration with tested application/schema compatibility, per-tenant draining and leases, canary rollout, version reconciliation, retries, and stop thresholds.
- Automate backup validation and tenant-local restore drills, including independent audit preservation, files/jobs/delivery reconciliation, location fencing, and safe cutover.
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
- Deliberately swapped locators/credentials, cloned database markers, wrong deployment IDs, metadata absence, pooled-connection reuse, reconnects, and stale routing revisions; assert zero operational queries on admission failure.
- Role downgrade with an unexpired JWT, user/session revocation, membership suspension, refresh reuse, and break-glass expiry across multiple application instances; verify post-commit checks and bounded in-flight work.
- Control-plane timeout, circuit opening, stale replicas/caches, failover, and restored security state; verify PHI-safe failure, paused jobs, and authentication-epoch invalidation without fallback routing.
- Mixed application versions, supported/unsupported schema sets, migration crashes between database and registry updates, lease loss, and old workers surviving maintenance; verify only compatible, ready deployments reopen.
- Tenant restore with post-restore-point audit events, newer files, revoked memberships, and already delivered reminders; verify evidence continuity, no privilege revival, no duplicate external actions, and failed-cutover containment.
- A saturated tenant beside a normal tenant under configured pool and application limits, concurrent restore/migration load, connection budgets, and independent control-plane capacity; verify approved SLOs and scaling/move triggers.

Production evidence must additionally verify encryption at rest and in transit, backups, restore exercises, secret management, BAAs, audit review, retention, legal holds, and incident-response processes. These cannot be proven solely by application unit tests.

## 13. Cost Model to Validate Before Implementation

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

## 14. Decision Questions

Answers recorded on 2026-09-08. Items marked *Open* still need an owner before Phase 1 exits.

| # | Question | Answer |
|---|---|---|
| 1 | Expected paying organizations after 12, 24, and 36 months? | Planning assumption: tens of agencies, up to about 50 within three years; measured workload determines capacity. |
| 2 | Smallest and largest expected caregiver and client counts per organization? | *Open.* Needed for elastic pool sizing. |
| 3 | Subscription price and gross-margin target? | *Open.* Needed to validate the Section 13 cost model. |
| 4 | Is a dedicated database standard or an enterprise tier? | Standard. Every organization receives its own Azure SQL database in a shared elastic pool. |
| 5 | Does one login need access to multiple agencies? | Yes. One identity, many memberships (Section 10). |
| 6 | Custom domains at launch or after subdomain launch? | After. Subdomains at launch, verified custom domains as a later tier. |
| 7 | Single US hosting region initially? | *Open.* The locator carries `DataRegion` so a later answer does not change the model. |
| 8 | Recovery point and recovery time objectives? | *Open.* Product Owner and Technical Lead must approve separate operational, audit, file, and control-plane objectives before Phase 1 exits (Sections 8.5-8.6). |
| 9 | Cross-organization benchmarking? | Not in scope. Any future aggregate reporting requires a separate privacy-reviewed specification. |
| 10 | Platform-support access to PHI? | None by default; break-glass with approval, reason capture, time limit, and enhanced audit (Section 10). |

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
- Identifiable client memberships require explicit control-plane classification. Routing, invalidation, recovery, compatibility, resilience, and resource controls in Sections 8 and 10 are design requirements; implementation tests and deployment evidence remain unverified.

#### Information

- HHS guidance indicates that a software or cloud provider that maintains or accesses ePHI on behalf of a regulated customer may be a business associate and requires appropriate contracts and safeguards. Product counsel and a qualified compliance owner should determine the exact obligations for CarePath and each customer relationship.

### Recommendation

**PASS WITH WARNINGS for continued planning only.** Do not begin multi-agency production onboarding until an approved specification, threat model, implementation, isolation tests, operational evidence, and contractual/compliance gates are complete.

## 16. Recorded Decision

**Accepted 2026-09-08.**

- Shared identity and SaaS management control plane plus one operational database per organization, with explicit classification of identifiable memberships.
- Azure SQL for production control plane and tenant databases; PostgreSQL retained for development and HomeLab portability only.
- Control-plane identity: one platform login, globally unique email, explicit organization memberships, one active organization per access token, tenant-specific subdomain entry URLs at launch.
- `PlatformAdmin` has no routine PHI access; break-glass only.
- Append-only PHI audit records live in each tenant database with recovery-independent preservation under Section 8.5. Control-plane event payloads exclude clinical values and patient-identifying membership details.
- Planning scale: up to about 50 organizations over three years. Growth triggers capacity/operations reassessment; shared-schema sharding requires a separate approved ADR.
- Independent database identity verification, authoritative token invalidation, reconciled restores, tested schema compatibility, fail-closed control-plane resilience, and measured elastic-pool limits are mandatory specification and verification gates before implementation and production onboarding respectively.

Implementation may begin only after the Phase 1 requirements, design, and task specifications are approved.
