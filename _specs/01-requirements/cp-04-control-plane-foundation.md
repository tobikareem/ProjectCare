# CP-04 — Control Plane Foundation: Requirements

**Status**: Draft  
**Author**: CarePath Health  
**Created**: 2026-09-08  
**Spec type**: Requirements  
**Parent decision**: [ADR 0003 — Multi-Tenant SaaS Database Strategy](../decisions/0003-multi-tenant-saas-database-strategy.md) (Accepted 2026-09-08)  
**Related specs**: [Design](../02-design/cp-04-control-plane-foundation.md) · [Tasks](../03-tasks/cp-04-control-plane-foundation.md)  
**Successors**: CP-05 Tenant-Aware Identity and Authorization · CP-06 Tenant Data Plane Routing

---

## Executive Summary

Introduce a shared, non-PHI control plane that knows which organizations exist, which host names belong to them, who is a member of each, and where each organization's operational database lives, so that CarePath can resolve a tenant and authenticate a user before any PHI is touched.

CP-04 is the first of three slices that implement ADR 0003. It delivers the control-plane data model, moves platform identity (credentials, refresh tokens) into it, resolves the organization from the request host, issues access tokens bound to exactly one organization, and backfills the existing single-organization deployment as tenant number one. It does **not** create tenant databases or route requests to them; that is CP-06.

---

## 1. Problem Statement

### 1.1 Current State

CarePath is single-organization by construction (ADR 0003 §3):

- No entity, table, claim, or configuration value identifies an organization. `BaseEntity` has no tenant key and the only global query filter is soft delete.
- ASP.NET Core Identity lives inside `CarePathDbContext`, the same context that holds PHI. Credentials and clinical data share one database and one migration history.
- The JWT carries user id, email, and roles only. `ICurrentUserContext` has no organization. The Blazor client logs in with email and password and has no organization selection.
- `User.Role` is a single role stored on the tenant user. `Admin` is effectively platform-wide: `Sprint4ObjectAuthorizationService` authorizes Admin and Coordinator for every resource before any per-object check.
- Email is globally unique in both the domain `User` table and the ASP.NET username index, so one person cannot exist at two agencies.
- There is no platform-operator role, no organization branding, no domain registry, and no record of where a tenant's database is.

### 1.2 Business Impact

- CarePath cannot onboard a second agency without exposing the first agency's PHI. This blocks the SaaS revenue model entirely.
- Both service lines are affected equally. In-Home Care (W-2, 40 to 45 percent margin) and Healthcare Staffing (1099, 25 to 30 percent margin) are per-organization business models; a staffing contractor who works for two agencies is the canonical case for one identity with many memberships.
- Every week the platform stays single-tenant is a week the Transitions B2B hospital channel (CP-03) cannot be sold to more than one discharge partner.

### 1.3 User Impact

| Role | Impact today |
|---|---|
| Platform operator (CarePath staff) | No way to create, suspend, or inspect an organization without direct database access. |
| Organization administrator | Cannot exist as a distinct concept; any Admin is a global Admin. |
| Caregiver with two agencies | Must have two email addresses and two logins. |
| All tenant users | Reach the API at one shared URL with no organization context. |

---

## 2. User Stories

### 2.1 Primary User Stories

```gherkin
Feature: Organization registration
  As a PlatformAdmin
  I want to register a new organization with a slug and primary host name
  So that the organization exists in the control plane before its database is provisioned

  Scenario: Register a new organization
    Given I am authenticated as a PlatformAdmin on the platform host
    When I submit a legal name, display name, slug "brightcare", and default time zone
    Then an Organization is created with Status = Pending and ProvisioningState = Registered
    And an OrganizationDomain "brightcare.carepathhealth.com" is created as the primary, verified host
    And a non-PHI platform audit event "OrganizationRegistered" is recorded
    And no tenant database is created
```

```gherkin
Feature: Tenant-specific login
  As an organization member
  I want to log in at my organization's own host name
  So that my session is bound to exactly one organization

  Scenario: Successful login at a registered host
    Given "brightcare.carepathhealth.com" is a verified host for an Active organization
    And I hold an active membership in that organization with role Coordinator
    When I POST valid credentials to /api/auth/login at that host
    Then I receive an access token whose organization_id claim equals BrightCare's id
    And the token carries membership_id and exactly one role, "Coordinator"
    And the response body includes the organization display name and slug

  Scenario: Login at a host for an organization where I have no membership
    Given "helpinghands.carepathhealth.com" is a verified host for an Active organization
    And I have no membership in Helping Hands
    When I POST my valid credentials there
    Then the response is 401 with the same body as invalid credentials
    And no token is issued
    And a non-PHI platform audit event "LoginDeniedNoMembership" is recorded without the email address
```

```gherkin
Feature: One identity, many memberships
  As a 1099 contractor who works for two agencies
  I want one login that works at either agency's host
  So that I do not need two email addresses

  Scenario: Same credentials, two organizations
    Given my platform identity has active memberships in BrightCare (Caregiver) and Helping Hands (Caregiver)
    When I log in at brightcare.carepathhealth.com
    Then my token's organization_id is BrightCare
    When I log in at helpinghands.carepathhealth.com
    Then my token's organization_id is Helping Hands
    And neither token is accepted at the other host
```

```gherkin
Feature: Token and host must agree
  As the platform
  I want to reject any token presented at a host it was not issued for
  So that a token can never act across organizations

  Scenario: Token replayed at another organization's host
    Given I hold a valid token for BrightCare
    When I call any authenticated endpoint at helpinghands.carepathhealth.com
    Then the response is 401
    And the request never reaches a controller
```

```gherkin
Feature: Existing deployment becomes tenant one
  As the operator of the current single-organization deployment
  I want the CP-04 migration to register the existing data as one organization
  So that existing users keep working without manual re-provisioning

  Scenario: Backfill on migration
    Given the control-plane database is empty
    And ControlPlane:DefaultOrganization is configured with a name, slug, and host names
    When the CP-04 migration and startup backfill run
    Then one Organization exists with Status = Active and ProvisioningState = Provisioned
    And its registered data-plane location is the existing CarePath database
    And every existing non-deleted, active domain User has exactly one membership with their current User.Role
    And every existing ApplicationUser row has been moved to the control plane with its password hash intact
    And existing users can log in at a configured host with unchanged credentials
```

### 2.2 Secondary User Stories

```gherkin
Feature: Membership management
  Scenario: Organization admin invites an existing platform user
    Given I am an Admin of BrightCare
    And a platform identity already exists for "maria@example.com" (member of Helping Hands)
    When I create a membership for that email with role Caregiver
    Then a new OrganizationMembership row links the existing platform user to BrightCare
    And no second platform identity is created
    And Maria can log in at brightcare.carepathhealth.com

  Scenario: Organization admin deactivates a membership
    Given Maria holds an active Caregiver membership in BrightCare
    When I set that membership's Status to Inactive
    Then Maria's next login at BrightCare is rejected with 401
    And Maria's existing BrightCare refresh token can no longer be rotated
    And Maria's Helping Hands membership is unaffected

  Scenario: Last active organization admin cannot be deactivated
    Given BrightCare has exactly one active Admin membership
    When I try to deactivate it or change its role
    Then the request is rejected with 409 "membership.last_active_admin"
```

```gherkin
Feature: Organization lifecycle
  Scenario: Suspend an organization
    Given BrightCare is Active
    When a PlatformAdmin sets its Status to Suspended with a reason
    Then all logins at BrightCare hosts return 401
    And all existing BrightCare tokens are rejected at the host resolution step
    And no data is deleted
    And a platform audit event "OrganizationSuspended" is recorded

  Scenario: Pending organization cannot be used
    Given Helping Hands is registered but ProvisioningState is not Provisioned
    When anyone attempts to log in at its host
    Then the response is 401 and no tenant lookup occurs
```

```gherkin
Feature: Branding
  Scenario: Anonymous branding lookup for the login page
    Given BrightCare has branding with a display name, primary color token, and support email
    When the web client GETs /api/organization/branding at brightcare.carepathhealth.com without a token
    Then it receives only the approved branding fields for BrightCare
    And no member, user, or PHI data is included

  Scenario: Branding for an unknown host
    When the web client GETs /api/organization/branding at an unregistered host
    Then the response is 404 with no organization detail
```

```gherkin
Feature: Platform administration boundary
  Scenario: PlatformAdmin cannot call tenant endpoints
    Given I hold a PlatformAdmin token issued at the platform host
    When I call /api/clients at brightcare.carepathhealth.com
    Then the response is 401
    And no PHI query is executed

  Scenario: Tenant token cannot call platform endpoints
    Given I hold an Admin token for BrightCare
    When I call /api/platform/organizations
    Then the response is 403
```

### 2.3 Edge Cases

| Case | Required behavior |
|---|---|
| Request host is not registered to any organization | 404 for anonymous branding, 401 for everything else. Response bodies are identical to the "unknown" case for suspended and pending organizations so an attacker cannot enumerate slugs. |
| Host name differs only by case or trailing dot | Hosts are normalized (lower-case, trailing dot removed, port stripped) before lookup. |
| Host carries an X-Forwarded-Host header | Only honored when the request comes from a configured trusted proxy. Otherwise the raw Host header is used. |
| Slug collides with a reserved word (`platform`, `admin`, `api`, `www`, `login`) or an existing slug | 409. Reserved slugs are a fixed list in code. |
| Slug format | Lower-case letters, digits, single hyphens, 3 to 40 characters, no leading or trailing hyphen. |
| Platform user exists with zero active memberships | Login at any tenant host returns 401. Login at the platform host returns 401 unless the user holds PlatformAdmin. |
| Membership is created for an email with no platform identity | A platform identity is created with a temporary password using the existing provisioning flow; the membership links to it. |
| Two memberships in the same organization for one user | Rejected with 409. `(OrganizationId, PlatformUserId)` is unique. |
| Refresh token issued at BrightCare is presented at Helping Hands | Rejected. Refresh tokens are bound to a membership, not just a user. |
| Refresh token rotation after membership deactivated | Rejected with 401. |
| Organization is suspended while a user holds a valid access token | The host resolution step consults organization status on every request, so the token is refused immediately, not at expiry. |
| Default organization configuration is missing at first startup on an empty control plane | Startup fails with a clear configuration error. Backfill never guesses. |
| Backfill runs twice | Idempotent. Second run finds the organization by slug and makes no changes. |
| Existing user is soft-deleted or inactive at backfill | No membership is created. The platform identity row is still moved so the audit trail of CreatedBy strings remains resolvable. |
| PlatformAdmin bootstrap | The first PlatformAdmin is created from configuration on an empty control plane, following the same fail-closed secret handling as the current development seed (password from user secrets or environment, never in source). |
| Local development hosts | `{slug}.localhost` and HomeLab `{slug}.devpi.local` are registered as ordinary OrganizationDomain rows. There is no development-only override header. |

---

## 3. Functional Requirements

### 3.1 Core Functionality

| ID | Requirement | Priority | User Role(s) | Service Line |
|----|-------------|----------|--------------|--------------|
| FR-001 | The system shall maintain a control-plane data store, physically separate from any tenant database, containing Organization, OrganizationDomain, OrganizationBranding, OrganizationMembership, PlatformUser (ASP.NET Identity), OrganizationDataPlane (database locator record), and PlatformAuditEvent. | Critical | System | Both |
| FR-002 | The control plane shall contain no PHI. No field on any control-plane entity may hold clinical, insurance, address, date-of-birth, or care data. | Critical | System | Both |
| FR-003 | The system shall resolve the organization for every request from the normalized request host by exact match against verified OrganizationDomain rows, before authentication. | Critical | System | Both |
| FR-004 | Host resolution shall fail closed: unknown, unverified, pending, or suspended hosts produce the same anonymous response and never reach authentication or a tenant lookup. | Critical | System | Both |
| FR-005 | ASP.NET Core Identity (users, password hashes, lockout, refresh tokens) shall be stored in the control plane. `CarePathDbContext` shall no longer derive from `IdentityDbContext`. | Critical | System | Both |
| FR-006 | One platform identity may hold memberships in many organizations. Email is unique across the platform. | High | All | Both |
| FR-007 | `OrganizationMembership` is the sole source of truth for a user's role in an organization. The tenant `User.Role` column is retained read-only for compatibility in CP-04 and removed in CP-05. | Critical | System | Both |
| FR-008 | Login at a tenant host shall succeed only when credentials are valid, the organization is Active and Provisioned, and the user holds an Active membership there. | Critical | All tenant roles | Both |
| FR-009 | Access tokens shall carry `organization_id`, `membership_id`, `sub`, `email`, and exactly one role. A token issued at one organization is invalid at every other host. | Critical | System | Both |
| FR-010 | Every authenticated request shall verify that the token's `organization_id` equals the resolved organization before authorization runs. Mismatch returns 401. | Critical | System | Both |
| FR-011 | Refresh tokens shall be bound to a membership. Rotation shall re-validate organization status and membership status. | High | All tenant roles | Both |
| FR-012 | `ICurrentUserContext` shall expose `OrganizationId` and `MembershipId`. Both are null for anonymous and platform requests. | Critical | System | Both |
| FR-013 | PlatformAdmin shall be a control-plane role, issued only at the platform host, with no `organization_id` claim. PlatformAdmin tokens are rejected at tenant hosts; tenant tokens are rejected at platform endpoints. | Critical | PlatformAdmin | Both |
| FR-014 | PlatformAdmin shall be able to register, list, view, update, suspend, and reactivate organizations and manage their domains and branding. | High | PlatformAdmin | Both |
| FR-015 | Registering an organization creates it with Status Pending and ProvisioningState Registered and creates no tenant database. Only CP-06 provisioning can move it to Provisioned and Active. | High | PlatformAdmin | Both |
| FR-016 | Organization Admin shall be able to list memberships, create a membership for an existing or new platform user, change a membership's role, and deactivate or reactivate a membership, within their own organization only. | High | Admin | Both |
| FR-017 | The last active Admin membership of an organization cannot be deactivated or demoted. | High | Admin | Both |
| FR-018 | The system shall expose an anonymous branding endpoint returning only approved branding fields for the resolved host. | Medium | Anonymous | Both |
| FR-019 | Branding shall be limited to display name, logo storage key, primary and accent color tokens chosen from the design-system palette, support email, and support phone. No free-form CSS, HTML, or script. | Medium | PlatformAdmin, Admin | Both |
| FR-020 | The CP-04 migration and startup backfill shall register the existing deployment as one Active, Provisioned organization from `ControlPlane:DefaultOrganization` configuration, move all `ApplicationUser` rows to the control plane, and create memberships for all active, non-deleted domain users using their current `User.Role`. | Critical | System | Both |
| FR-021 | The first PlatformAdmin shall be bootstrapped from configuration on an empty control plane, with the password read from user secrets or environment and never from source. | High | System | Both |
| FR-022 | Every control-plane mutation and every denied login or host resolution shall write a PlatformAuditEvent containing actor platform user id, organization id when known, action, entity type, entity id, correlation id, and UTC timestamp. Never the email address, host name of a failed lookup beyond a hash, or any PHI. | Critical | System | Both |
| FR-023 | The Blazor web client shall read organization branding for its host on load, display it on the login page, and surface `OrganizationId`, organization display name, and role from `AuthTokenResponse` in its authentication state. | Medium | All tenant roles | Both |
| FR-024 | The existing tenant API surface (clients, caregivers, shifts, billing, transitions, admin users) shall continue to function unchanged for the backfilled organization. | Critical | All tenant roles | Both |

### 3.2 Data Requirements

Control-plane entities (all `Guid` keys, UTC timestamps, soft delete via `IsDeleted`; full shapes in the design spec):

| Entity | Purpose | Notable rules |
|---|---|---|
| `Organization` | Customer agency | `Slug` unique and immutable after creation; `Status` ∈ {Pending, Active, Suspended, Offboarded}; `ProvisioningState` ∈ {Registered, Provisioning, Provisioned, Failed}; `DefaultTimeZone` IANA id; `DataRegion` |
| `OrganizationDomain` | Host name mapping | `HostName` normalized and unique across the platform; `IsPrimary`; `VerificationStatus` ∈ {Pending, Verified}; system-generated `{slug}` subdomains are Verified on creation |
| `OrganizationBranding` | White-label fields | One row per organization; color values restricted to named design-system tokens |
| `OrganizationMembership` | Authoritative role | `(OrganizationId, PlatformUserId)` unique; `OrganizationRole` ∈ the six existing tenant roles; `Status` ∈ {Active, Inactive}; `TenantUserId` holds the tenant-side `User.Id` |
| `PlatformUser` | ASP.NET Identity user | `IdentityUser<Guid>`; email unique; `IsPlatformAdmin` flag or Identity role `PlatformAdmin`; no `DomainUserId` |
| `PlatformRefreshToken` | Refresh token per membership | Hash, expiry, `MembershipId`; replaces the two columns on today's `ApplicationUser` |
| `OrganizationDataPlane` | Database locator | `Provider`, `SecretReference` (never a plaintext connection string), `SchemaVersion`, `RegisteredAtUtc`; CP-04 populates it only for the backfilled organization |
| `PlatformAuditEvent` | Append-only platform audit | Insert-only; no update or delete path in code |

Tenant-side changes in CP-04:

- `User` gains `PlatformUserId` (`Guid`, required after backfill) so a tenant profile can be traced to its platform identity without a cross-database FK.
- `ApplicationUser`, `ApplicationUserConfiguration`, and the Identity table set are removed from `CarePathDbContext` by migration. The migration is forward-only for the Identity tables: `Down` must not drop control-plane data.
- `User.Role` remains but becomes read-only from the API's perspective; `AdminUsersController` role changes are redirected to membership management.

Validation rules: slug format and reserved list, host name RFC 1123 format and length ≤ 253, color tokens from an allowlist, email format, time zone must be a valid IANA id, all enforced by FluentValidation in the Application layer.

### 3.3 Integration Requirements

- **Layers**: Domain (control-plane entities and enums in a new `Entities/Platform/` folder), Application (services, validators, `ICurrentUserContext` extension, `IOrganizationContext`), Infrastructure (new `ControlPlaneDbContext`, its own migrations, host resolver, Identity re-homing, backfill), WebApi (host resolution middleware, platform and organization controllers, auth changes), Contracts and Client (organization DTOs, extended `AuthTokenResponse`), Web (branding on login, organization in auth state).
- **External services**: none. No email, SMS, DNS, or certificate automation in CP-04. Custom-domain verification is a later spec; CP-04 only stores system-generated subdomains as verified.
- **SignalR**: none.
- **Database**: the control plane is a separate database on the same server as the existing database in development and HomeLab, and a separate Azure SQL database in production. Both SQL Server and PostgreSQL migration sets are produced for the control plane, per ADR 0003 §8.1.

### 3.4 Security & Authorization

- Roles: existing six tenant roles unchanged in name for CP-04; new `PlatformAdmin` control-plane role. Renaming `Admin` to `OrganizationAdmin` is deferred to CP-05 so this spec does not touch every controller attribute.
- JWT: new claims `organization_id`, `membership_id`, `token_kind` ∈ {tenant, platform}. Single global issuer, audience, and signing key remain (ADR 0003 §10).
- Host resolution runs before authentication in the pipeline and stores the resolved organization in a scoped `IOrganizationContext`. Authentication then rejects any token whose `organization_id` does not equal the resolved organization id.
- Object-level authorization in `Sprint4ObjectAuthorizationService` is unchanged in CP-04 because the backfilled organization is the only one whose data is reachable; the tenant precondition is CP-05 scope.
- HIPAA: the control plane holds no PHI. Platform audit events are non-PHI. Failed-login and unknown-host events store a SHA-256 hash of the host and no email. `IPhiAuditLogger` continues to cover tenant PHI access and gains `OrganizationId` in `PhiAuditEntry` so CP-06 can route it.
- Secrets: `OrganizationDataPlane.SecretReference` names a secret in configuration or a secret manager; the control plane never stores a connection string.

---

## 4. Non-Functional Requirements

### 4.1 Performance

- Host resolution adds at most one control-plane query per request and must complete in under 5 ms at p95 with a warm in-memory cache keyed by normalized host, invalidated on any organization or domain mutation and with a 60-second maximum TTL so suspension takes effect within a minute even across instances.
- Login end-to-end (resolve, credential check, membership check, token issue) under 400 ms at p95.
- No additional query on tenant endpoints beyond host resolution; the organization id comes from the token.

### 4.2 Scalability

- Designed for up to 50 organizations and up to 5,000 platform users over three years (ADR 0003 §8). No partitioning required.
- Control-plane tables are small; the hot path is a single indexed lookup on `OrganizationDomain.HostName`.

### 4.3 Reliability

- Backfill and PlatformAdmin bootstrap are idempotent and safe to re-run at every startup.
- If the control-plane database is unreachable, the API returns 503 for all requests and never falls back to single-tenant behavior.
- Control-plane migrations run before tenant migrations at startup when `Database:AutoMigrate` is true.

### 4.4 Usability

- Login page shows the organization's display name and colors within the existing wireframe shell (`Documentation/Wireframes/carepath-wireframe.html`); no new screens are introduced. Branding fields map to existing design tokens only.
- Error responses for unknown host, wrong organization, and invalid credentials are indistinguishable to the client and use the existing problem-details shape.

### 4.5 Compliance

- HIPAA: PHI stays in the tenant database. The control plane is classified non-PHI and its audit trail is append-only.
- Retention: `PlatformAuditEvent` and `OrganizationMembership` history are retained for 6 years, matching the existing retention rule.
- No PHI in logs or URLs; organization slugs are not PHI and may appear in host names.

---

## 5. Success Criteria

### 5.1 Quantitative Metrics

- Two-organization integration tests pass: a user with memberships in both receives distinct tokens, and each token is rejected at the other host in 100 percent of cases.
- Backfill on a copy of the current HomeLab database produces one organization, one data-plane record, and one membership per active user, with zero login failures for existing accounts.
- `dotnet build CarePath.sln` passes with zero warnings; full test suite passes.
- Host resolution p95 under 5 ms with cache warm, measured in the integration test host.

### 5.2 Qualitative Goals

- A PlatformAdmin can register a second organization through the API without touching a database.
- The `dotnet-code-reviewer` and `/hipaa-check` reviews find no PHI in the control plane and no cross-organization path in the login flow.
- CP-05 and CP-06 can start without revisiting the control-plane schema.

---

## 6. Scope & Boundaries

### 6.1 In Scope

- Control-plane entities, `ControlPlaneDbContext`, migrations for both providers, DI registration.
- Moving ASP.NET Identity and refresh tokens to the control plane.
- Host resolution middleware and `IOrganizationContext`.
- Organization-bound login, refresh, and token claims; `ICurrentUserContext` extension.
- PlatformAdmin role and bootstrap; platform organization, domain, and branding endpoints.
- Organization Admin membership endpoints.
- Anonymous branding endpoint and web login-page branding.
- Backfill of the existing deployment as tenant one, including `OrganizationDataPlane` registration for its existing database.
- `PlatformAuditEvent` append-only store.
- `User.PlatformUserId` and `PhiAuditEntry.OrganizationId`.

### 6.2 Out of Scope

- Creating, migrating, or routing to tenant databases; `SchemaVersion` enforcement; the scoped `CarePathDbContext` factory (CP-06).
- Tenant precondition in object authorization; renaming `Admin` to `OrganizationAdmin`; removing `User.Role` (CP-05).
- Custom domain verification, DNS, certificates, wildcard TLS configuration (Phase 4 of ADR 0003).
- Break-glass elevated access for PlatformAdmin.
- Subscription, billing plan, or pricing records.
- Organization-scoped file storage, Data Protection purposes, cache keys (CP-06).
- In-app organization switcher UI; switching is by visiting the other host.
- External SSO per organization.
- Persisting the PHI audit log to a tenant table (CP-06).

### 6.3 Future Considerations

- `OrganizationDataPlane` is shaped so CP-06 can add pool, region, and health fields without a breaking migration.
- `OrganizationDomain.VerificationStatus` already exists so custom domains only add a verification workflow.
- `token_kind` leaves room for a future break-glass token kind.

---

## 7. Dependencies & Assumptions

### 7.1 Technical Dependencies

- .NET 10, EF Core 10, ASP.NET Core Identity 10 (already in `Directory.Packages.props`).
- Two DbContexts in one process, each with its own migrations assembly per provider.
- A reverse proxy in production that forwards the original host; `ForwardedHeaders` middleware configured with an explicit known-proxy list.
- Wildcard DNS for `*.carepathhealth.com` in production (infrastructure task, not code).

### 7.2 Business Dependencies

- ADR 0003 accepted (done 2026-09-08).
- Product owner supplies the default organization name and slug for the backfill.
- Security/compliance owner reviews the non-PHI classification of every control-plane field before the design spec is approved.

### 7.3 Assumptions

- The current deployment is one organization; all existing users belong to it.
- Every existing `ApplicationUser` has exactly one linked domain `User`, as enforced by today's unique index on `DomainUserId`.
- Email remains the login identifier.
- The Blazor client is served on the same host as the API for each organization, or CORS origins are registered per organization domain (the latter is a CP-04 configuration task).

---

## 8. Risks & Mitigation

| Risk | Likelihood | Impact | Mitigation Strategy |
|------|------------|--------|---------------------|
| Identity move breaks existing logins | Medium | Critical | Backfill copies rows including password hash and security stamp verbatim; integration test logs in as every seeded user before and after migration; forward-only migration keeps the old tables until verified. |
| Host resolution cache serves a suspended organization | Low | High | 60-second TTL plus explicit invalidation on mutation; suspension test asserts rejection within the TTL. |
| Host spoofing through X-Forwarded-Host | Medium | Critical | Forwarded headers honored only from configured known proxies; test with an unlisted source IP. |
| Slug enumeration through differing error bodies | Medium | Medium | Single anonymous error shape for unknown, pending, and suspended hosts; test asserts byte-identical bodies. |
| Two DbContexts complicate transactions | Medium | Medium | Membership creation writes to the control plane and the tenant profile in two steps with compensating cleanup; no distributed transaction. Documented in the design spec. |
| Control-plane migration diverges between SQL Server and PostgreSQL | Medium | Medium | Both migration sets generated in the same task; CI check per ADR 0003 §8.1. |
| `User.Role` and membership role drift during CP-04 | Low | Medium | All role writes go through membership; `User.Role` is written only by the backfill and by the membership service as a mirror until CP-05 removes it. |
| Missing default organization configuration in a new environment | Medium | Low | Startup fails closed with a named configuration key in the error. |

---

## 9. Stakeholder Sign-Off

| Stakeholder | Role | Status | Date | Comments |
|-------------|------|--------|------|----------|
| Tobi Kareem | Product Owner | Pending | - | - |
| Tobi Kareem | Tech Lead | Pending | - | - |
| TBD | Security/Compliance Owner | Pending | - | Must confirm non-PHI classification of control-plane fields |

---

## 10. Related Documents

- [ADR 0003 — Multi-Tenant SaaS Database Strategy](../decisions/0003-multi-tenant-saas-database-strategy.md)
- [Architecture.md](../../Documentation/Architecture.md)
- [Design Spec](../02-design/cp-04-control-plane-foundation.md) — created after this spec is approved
- [Tasks Spec](../03-tasks/cp-04-control-plane-foundation.md) — created after design is approved
- [CP-02 Infrastructure / EF Core](cp-02-infrastructure-ef-core.md) — current persistence baseline

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-09-08 | CarePath Health | Initial draft from ADR 0003 decisions and 2026-09-08 code review |
