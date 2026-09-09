# CP-04 — Control Plane Foundation: Requirements

**Status**: Draft  
**Author**: CarePath Health  
**Created**: 2026-09-08  
**Revised**: 2026-09-08 (v1.2: no in-app organization switching; organizations are reached only through their own domain)  
**Spec type**: Requirements  
**Parent decision**: [ADR 0003 — Multi-Tenant SaaS Database Strategy](../decisions/0003-multi-tenant-saas-database-strategy.md) (Accepted 2026-09-08)  
**UI source of truth**: `Documentation/Wireframes/carepath-wireframe.html`, "SaaS journey" tab (see §11 for the step-to-requirement map)  
**Related specs**: [Design](../02-design/cp-04-control-plane-foundation.md) · [Tasks](../03-tasks/cp-04-control-plane-foundation.md)  
**Successors**: CP-05 Tenant-Aware Identity and Authorization · CP-06 Tenant Data Plane Routing

---

## Executive Summary

Introduce the shared identity and SaaS management control plane: the store that knows which organizations exist, which host names belong to them, who is a member of each with what role, which sessions are live, and where each organization's operational database is registered, so that CarePath resolves a tenant, authenticates a user, and re-validates authorization state on every protected request before any PHI is touched.

CP-04 is the first of three slices implementing ADR 0003. It delivers steps 1 to 4 of the ADR §8.2 request pipeline, moves platform identity into the control plane, issues tokens bound to exactly one organization and one session, enforces revocation freshness on every request, and backfills the existing deployment as tenant number one. Steps 5 and 6 (database identity verification and tenant-scoped `DbContext` creation) are CP-06.

---

## 1. Problem Statement

### 1.1 Current State

CarePath is single-organization by construction (ADR 0003 §3):

- No entity, table, claim, or configuration value identifies an organization. `BaseEntity` has no tenant key; the only global query filter is soft delete.
- ASP.NET Core Identity lives inside `CarePathDbContext`, sharing a database and migration history with PHI.
- The JWT carries user id, email, and roles only. A role change, membership removal, or user disable takes effect only when the token expires; there is no session, security version, or revocation record.
- `ICurrentUserContext` has no organization. The Blazor client logs in with email and password at one shared URL and has no organization context.
- `User.Role` is a single role on the tenant user. `Admin` is effectively platform-wide: `Sprint4ObjectAuthorizationService` authorizes Admin and Coordinator for every resource before any per-object check.
- Email is globally unique in both the domain `User` table and the ASP.NET username index, so one person cannot exist at two agencies.
- There is no platform-operator role, no organization branding, no domain registry, no readiness state, and no record of where a tenant's database is.

### 1.2 Business Impact

- CarePath cannot onboard a second agency without exposing the first agency's PHI. This blocks the SaaS revenue model.
- Both service lines are affected. In-Home Care (W-2, 40 to 45 percent margin) and Healthcare Staffing (1099, 25 to 30 percent margin) are per-organization business models; a staffing contractor who works for two agencies is the canonical case for one identity with many memberships.
- The Transitions B2B channel (CP-03) cannot be sold to more than one discharge partner until tenancy exists.

### 1.3 User Impact

| Role | Impact today |
|---|---|
| Platform operator (CarePath staff) | No way to create, suspend, or inspect an organization without direct database access. |
| Organization administrator | Cannot exist as a distinct concept; any Admin is a global Admin. Removing someone's access does not end their live session. |
| Caregiver with two agencies | Needs two email addresses and two logins. |
| All tenant users | Reach the API at one shared URL with no organization context; no "where am I working" cue in the UI. |

---

## 2. User Stories

Actors and hosts follow the wireframe: platform operators work at `manage.carepathhealth.com`; each agency works at `{slug}.carepathhealth.com`. There is no in-app organization switch. A person who belongs to several organizations reaches each one only by opening that organization's own address and signing in there.

### 2.1 Primary User Stories

```gherkin
Feature: Organization registration (wireframe steps 2 to 5)
  As a PlatformAdmin
  I want to register a new organization with a slug and primary host name
  So that the organization exists in the control plane before its database is provisioned

  Scenario: Register a new organization
    Given I am authenticated as a PlatformAdmin at manage.carepathhealth.com
    When I submit a legal name, display name, slug "brightcare", default time zone, and the first administrator's email
    Then an Organization is created with Status = Pending and ReadinessState = Provisioning
    And an OrganizationDomain "brightcare.carepathhealth.com" is created as the primary, verified host
    And an Admin membership for the first administrator is created in Status = Active
    And a platform audit event "OrganizationRegistered" is recorded with organization id and actor id only
    And no tenant database is created
    And the readiness screen shows the workspace as not yet ready
```

```gherkin
Feature: Tenant-specific login (wireframe step 6)
  As an organization member
  I want to log in at my organization's own host name
  So that my session is bound to exactly one organization

  Scenario: Successful login at a ready host
    Given "brightcare.carepathhealth.com" is a verified host for an organization with Status = Active and ReadinessState = Ready
    And I hold an Active membership there with role Coordinator
    When I POST valid credentials to /api/auth/login at that host
    Then a PlatformSession is created bound to my user, that membership, and that organization
    And I receive an access token carrying organization_id, membership_id, session_id, membership_security_version, user_security_version, auth_epoch, token_kind = "tenant", and exactly one role "Coordinator"
    And the response body includes the organization display name and slug

  Scenario: Login at a host where I have no membership
    Given "helpinghands.carepathhealth.com" is a verified host for a ready organization
    And I have no Active membership in Helping Hands
    When I POST valid credentials there
    Then the response is 401 with the same body as invalid credentials
    And no session or token is created
    And a platform audit event "LoginDenied" is recorded with reason code, organization id, and a hash of the email, never the email itself
```

```gherkin
Feature: One identity, many memberships
  As a contractor who works for two agencies
  I want one login that works at either agency's host
  So that I do not need two email addresses

  Scenario: Same credentials, two organizations
    Given my platform identity has Active memberships in BrightCare (Caregiver) and Helping Hands (Caregiver)
    When I log in at brightcare.carepathhealth.com
    Then my token's organization_id is BrightCare
    When I log in at helpinghands.carepathhealth.com
    Then my token's organization_id is Helping Hands
    And neither token is accepted at the other host
    And no screen, endpoint, or link lists my other organizations or moves me between them
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
    And the request never reaches a controller and no operational query runs
```

```gherkin
Feature: Authorization freshness (ADR 0003 §10.1)
  As an organization administrator
  I want role and access changes to take effect immediately
  So that an unexpired token cannot keep privileges I have removed

  Scenario: Role downgrade with an unexpired token
    Given Maria holds an Admin token for BrightCare that expires in 50 minutes
    When I change Maria's membership role to Caregiver
    Then the membership SecurityVersion increments in the same transaction
    And Maria's next request with the old token is rejected with 401 on every application instance
    And after signing in again Maria receives a Caregiver token

  Scenario: Membership deactivated
    When I set Maria's BrightCare membership Status to Inactive
    Then every BrightCare session of Maria's is revoked
    And her next request, refresh, or login at BrightCare is rejected
    And her Helping Hands membership and sessions are unaffected

  Scenario: Sign out
    Given Maria is signed in at BrightCare
    When she signs out
    Then her session is revoked, her refresh token family is revoked, and her access token is rejected on the next request
```

```gherkin
Feature: Existing deployment becomes tenant one
  As the operator of the current single-organization deployment
  I want the CP-04 migration to register the existing data as one organization
  So that existing users keep working without manual re-provisioning

  Scenario: Backfill on migration
    Given the control-plane database is empty
    And ControlPlane:DefaultOrganization is configured with a name, slug, time zone, and host names
    When the control-plane migration and startup backfill run
    Then one Organization exists with Status = Active and ReadinessState = Ready
    And its OrganizationDataPlane registers the existing CarePath database with a new DatabaseDeploymentId, LocationRevision 1, and the current SchemaVersion
    And a TenantDeployment metadata row in the existing database carries the same OrganizationId and DatabaseDeploymentId
    And every existing ApplicationUser row has been moved to the control plane with password hash and security stamp intact
    And every active, non-deleted domain User has exactly one Active membership with their current User.Role
    And existing users can log in at a configured host with unchanged credentials
```

### 2.2 Secondary User Stories

```gherkin
Feature: Membership management (wireframe step 7, "Invite your care team")
  Scenario: Organization admin invites an existing platform user
    Given I am an Admin of BrightCare
    And a platform identity already exists for "maria@example.com" as a member of Helping Hands
    When I create a BrightCare membership for that email with role Caregiver
    Then a new OrganizationMembership links the existing platform user to BrightCare
    And a tenant-side User profile is created in BrightCare's database carrying PlatformUserId
    And no second platform identity is created

  Scenario: Organization admin invites a new person
    When I create a membership for an email with no platform identity
    Then a platform identity is provisioned with a temporary password using the existing fail-closed provisioning flow
    And the membership and tenant profile are created as above

  Scenario: Last active organization admin cannot be removed
    Given BrightCare has exactly one Active Admin membership
    When I try to deactivate it or change its role
    Then the request is rejected with 409 "membership.last_active_admin"
```

```gherkin
Feature: Organization lifecycle (wireframe step 9 service states)
  Scenario: Suspend an organization
    Given BrightCare is Active
    When a PlatformAdmin sets its Status to Suspended with a reason
    Then all sessions for BrightCare memberships are revoked
    And all logins and authenticated requests at BrightCare hosts return 401
    And the anonymous branding endpoint at BrightCare hosts returns 404, indistinguishable from an unknown host
    And no data is deleted
    And a platform audit event "OrganizationSuspended" is recorded

  Scenario: Organization in maintenance
    Given BrightCare is Active and a PlatformAdmin sets ReadinessState = Maintenance
    When a member opens the sign-in page
    Then the branding endpoint returns branding plus ServiceState = "Maintenance"
    And the page shows "We're updating your workspace" with no database or routing detail
    And login and every authenticated request return 503 with a PHI-safe body
    And existing sessions are not revoked; they resume when ReadinessState returns to Ready

  Scenario: Control plane unreachable
    Given the control-plane database does not answer within the configured timeout
    When any request reaches host resolution or authorization
    Then the response is 503 with a PHI-safe body
    And no operational query runs and no cached security decision is used
    And the web client shows "We can't open your workspace right now"

  Scenario: Pending organization cannot be used
    Given Helping Hands is registered but ReadinessState is not Ready
    When anyone attempts to log in at its host
    Then the response is 503 and no tenant lookup occurs
```

```gherkin
Feature: Branding
  Scenario: Anonymous branding lookup for the sign-in page
    Given BrightCare has branding with a display name, monogram, primary and accent color tokens, and support contact
    When the web client GETs /api/organization/branding at brightcare.carepathhealth.com without a token
    Then it receives only the approved branding fields and the public ServiceState
    And no member, user, or PHI data is included

  Scenario: Branding for an unknown or suspended host
    When the web client GETs /api/organization/branding at an unregistered or suspended host
    Then the response is 404 with a body identical for both cases
```

```gherkin
Feature: Platform administration boundary (wireframe steps 2 to 5)
  Scenario: PlatformAdmin cannot call tenant endpoints
    Given I hold a PlatformAdmin token (token_kind = "platform") issued at manage.carepathhealth.com
    When I call /api/clients at brightcare.carepathhealth.com
    Then the response is 401 and no PHI query is executed

  Scenario: Tenant token cannot call platform endpoints
    Given I hold an Admin token for BrightCare
    When I call /api/platform/organizations at manage.carepathhealth.com
    Then the response is 401 because the token's organization does not match the platform host
```

### 2.3 Edge Cases

| Case | Required behavior |
|---|---|
| Request host is not registered to any organization | Branding returns 404; everything else returns 401. Bodies are identical to the suspended case. |
| Host name differs by case, trailing dot, or port | Hosts are normalized (lower-case, trailing dot removed, port stripped) before lookup. |
| X-Forwarded-Host present | Honored only from configured known proxies via `ForwardedHeaders` with an explicit allowlist. Otherwise the raw Host header is used. |
| Reserved host labels `manage`, `account`, `api`, `www`, `login`, `admin`, `platform`, `status` | Cannot be used as organization slugs. `manage` is the platform host resolved to no organization; tenant tokens are rejected there and platform tokens are rejected at tenant hosts. |
| Slug format | Lower-case letters, digits, single hyphens, 3 to 40 characters, no leading or trailing hyphen. Immutable after creation. |
| Platform user has zero Active memberships | Login at any tenant host returns 401. Login at `manage` returns 401 unless the user is a PlatformAdmin. |
| Two memberships in one organization for one user | 409. `(OrganizationId, PlatformUserId)` is unique. |
| Refresh token from BrightCare presented at Helping Hands | Rejected; refresh tokens are bound to a session, which is bound to one membership and organization. |
| Refresh token reused after rotation | The whole token family and its session are revoked; a platform audit event "RefreshReplayDetected" is recorded. |
| Refresh after membership deactivated, user disabled, organization suspended, or epoch incremented | 401. |
| Authoritative check times out or the control plane is unavailable | 503, no operational query, no fallback to token-embedded role. Bounded timeout and circuit breaker per ADR §8.6. |
| Authentication epoch incremented (privileged recovery operation) | Every token and refresh token issued before the increment is rejected on all instances. |
| Default organization configuration missing on an empty control plane | Startup fails with a named configuration key. Backfill never guesses. |
| Backfill runs twice | Idempotent; the organization is found by slug and nothing changes. |
| Existing user is soft-deleted or inactive at backfill | No membership is created; the platform identity row is still moved so historical `CreatedBy` strings remain resolvable. |
| PlatformAdmin bootstrap | First PlatformAdmin created from configuration on an empty control plane; password from user secrets or environment, never source. |
| Local development hosts | `{slug}.localhost`, `manage.localhost`, and HomeLab `{slug}.devpi.local` are ordinary domain rows. No development-only override header. |
| Public site (`carepathhealth.com`, `www`) | Serves no sign-in, no agency lookup, no directory, and no link to any agency host. Members use the address their organization gave them; platform operators use `manage.*`. |
| PlatformAdmin visits a tenant host | Login there returns 401 unless the person also holds an Active membership in that organization, in which case they get an ordinary tenant token for that membership, never platform authority. |
| List endpoints | Freshness and organization checks run in middleware, so list and search endpoints that never touch `IdorGuard` are covered. |

---

## 3. Functional Requirements

### 3.1 Core Functionality

| ID | Requirement | Priority | User Role(s) | Service Line |
|----|-------------|----------|--------------|--------------|
| FR-001 | The system shall maintain a control-plane data store, physically separate from any tenant database, containing Organization, OrganizationDomain, OrganizationBranding, OrganizationMembership, OrganizationDataPlane, PlatformUser (ASP.NET Identity), PlatformSession, PlatformRefreshToken, PlatformSecurityState, and PlatformAuditEvent. | Critical | System | Both |
| FR-002 | The control plane shall hold no clinical, insurance, address, date-of-birth, or care data. Identity and membership records are classified per §3.5; a membership with role Client is treated as PHI-adjacent because it reveals a healthcare relationship. | Critical | System | Both |
| FR-003 | The system shall resolve the organization for every request from the normalized request host by exact match against verified OrganizationDomain rows, before authentication, and store it in a scoped `IOrganizationContext`. | Critical | System | Both |
| FR-004 | Host resolution shall fail closed. Unknown and suspended hosts produce identical anonymous responses; hosts whose organization is not Ready produce 503; none of them reach authentication or a tenant lookup. | Critical | System | Both |
| FR-005 | ASP.NET Core Identity (users, password hashes, lockout, security stamps) shall be stored in the control plane. `CarePathDbContext` shall no longer derive from `IdentityDbContext`. | Critical | System | Both |
| FR-006 | One platform identity may hold memberships in many organizations. Email is unique across the platform. | High | All | Both |
| FR-007 | `OrganizationMembership` is the sole source of truth for a user's role in an organization. The tenant `User.Role` column is written only by the backfill and the membership service as a mirror in CP-04 and removed in CP-05. | Critical | System | Both |
| FR-008 | Login at a tenant host shall succeed only when credentials are valid, the organization is Active and Ready, and the user holds an Active membership there. Success creates a PlatformSession bound to user, membership, and organization. | Critical | All tenant roles | Both |
| FR-009 | Access tokens shall carry `sub`, `email`, `organization_id`, `membership_id`, `session_id`, `membership_security_version`, `user_security_version`, `auth_epoch`, `token_kind` ∈ {tenant, platform}, and exactly one role. All values come from one consistent control-plane read at issuance. | Critical | System | Both |
| FR-010 | On every protected request, after signature and lifetime validation, the system shall read current control-plane state and reject the request with 401 unless: the token's organization equals the resolved host's organization; the organization is Active; the membership is Active with the same SecurityVersion and role; the user is enabled with the same SecurityVersion; the session is not revoked; and the token's `auth_epoch` equals the current epoch. It shall reject with 503 if the organization is not Ready. This runs in middleware before controllers, covering list endpoints. | Critical | System | Both |
| FR-011 | Security decisions in FR-010 shall never be served from a cache or a lagging replica. If the control-plane read fails or exceeds its timeout, the response is 503 and no operational query runs. | Critical | System | Both |
| FR-012 | Refresh tokens shall be server-tracked, hashed, rotated on every use, bound to a session, and grouped in a family. Reuse of a rotated token revokes the family and the session. Rotation re-runs the FR-010 checks. | Critical | All tenant roles | Both |
| FR-013 | Role change, membership deactivation, and membership removal shall increment the membership SecurityVersion atomically with the change. User disable, credential reset, and global sign-out shall increment the user SecurityVersion. Deactivation and suspension shall revoke affected sessions. | Critical | Admin, PlatformAdmin | Both |
| FR-014 | A single protected `PlatformSecurityState.AuthenticationEpoch` shall exist. Only a privileged operations procedure may increment it. Its value is embedded in every token and compared on every request. | High | PlatformAdmin | Both |
| FR-015 | Sign-out shall revoke the current session and its refresh token family. | High | All | Both |
| FR-016 | `ICurrentUserContext` shall expose `OrganizationId`, `MembershipId`, and `SessionId`. All are null for anonymous requests; `OrganizationId` and `MembershipId` are null for platform tokens. | Critical | System | Both |
| FR-017 | PlatformAdmin shall be a control-plane role. Platform tokens are issued only at `manage.*` hosts, carry `token_kind = platform` and no organization, are rejected at tenant hosts, and are subject to the same session, version, and epoch checks. Tenant tokens are rejected at `manage.*`. | Critical | PlatformAdmin | Both |
| FR-018 | PlatformAdmin shall be able to register, list, view, update, suspend, and reactivate organizations, set ReadinessState between Provisioning, Ready, and Maintenance, and manage their domains and branding. Recovering and Failed are set only by CP-06 provisioning and recovery workflows. | High | PlatformAdmin | Both |
| FR-018a | Suspend, reactivate, set maintenance, and end maintenance shall each require a recorded reason, produce a PlatformAuditEvent, and be confirmed through the confirmation dialog pattern in the UI design system. Suspend additionally requires the operator to type the organization slug. Set maintenance carries a member-facing message of at most 200 characters returned by the branding endpoint's ServiceState. Reason text is never shown to members. | High | PlatformAdmin | Both |
| FR-019 | Registering an organization creates it with Status Pending and ReadinessState Provisioning, creates its primary verified subdomain, creates the first Admin membership, and creates no tenant database. | High | PlatformAdmin | Both |
| FR-020 | Organization Admin shall be able to list memberships, create a membership for an existing or new platform user, change a membership's role, and deactivate or reactivate a membership, within their own organization only. The last Active Admin membership cannot be deactivated or demoted. | High | Admin | Both |
| FR-021 | The system shall provide no endpoint, screen, or link that lists a user's other organizations or moves a session between organizations. An organization is reached only by opening its own host and signing in there. | Critical | All | Both |
| FR-021 | The system shall expose an anonymous branding endpoint returning approved branding fields and the public ServiceState (Available, Maintenance, Recovering) for the resolved host. | Medium | Anonymous | Both |
| FR-022 | Branding shall be limited to display name, monogram text, logo storage key, primary and accent color tokens from the design-system palette, support email, and support phone. No free-form CSS, HTML, or script. | Medium | PlatformAdmin, Admin | Both |
| FR-023 | The CP-04 migration and startup backfill shall register the existing deployment as one Active, Ready organization from `ControlPlane:DefaultOrganization`, move all `ApplicationUser` rows, create memberships for active non-deleted users with their current role, register the existing database in OrganizationDataPlane with a new DatabaseDeploymentId, and write the matching TenantDeployment metadata row into the existing database. | Critical | System | Both |
| FR-024 | The first PlatformAdmin shall be bootstrapped from configuration on an empty control plane, with the password read from user secrets or environment. | High | System | Both |
| FR-025 | Every control-plane mutation, denied login, denied host resolution, revocation, refresh replay, and 503 decision shall write an append-only PlatformAuditEvent with actor platform user id, organization id when known, action, entity type, entity id, correlation id, outcome, and UTC timestamp. Never an email, a raw unknown host name (hash only), a token, or any PHI. | Critical | System | Both |
| FR-026 | The Blazor web client shall read branding and ServiceState for its host on load, render the agency-branded sign-in page or the maintenance and outage states from the wireframe, show the current organization and role in the shell, and offer "Sign out". No organization switch is offered. | Medium | All tenant roles | Both |
| FR-027 | The existing tenant API surface shall continue to function unchanged for the backfilled organization. | Critical | All tenant roles | Both |
| FR-028 | Revocation tests shall run against at least two application instances sharing one control plane and prove that role downgrade, deactivation, suspension, sign-out, refresh replay, and epoch increment reject the old token on both instances. | High | System | Both |

### 3.2 Data Requirements

Control-plane entities (all `Guid` keys, UTC timestamps; full shapes in the design spec):

| Entity | Purpose | Notable rules |
|---|---|---|
| `Organization` | Customer agency | `Slug` unique, immutable; `Status` ∈ {Pending, Active, Suspended, Offboarded}; `ReadinessState` ∈ {Provisioning, Ready, Maintenance, Recovering, Failed}; `DefaultTimeZone` IANA id; `DataRegion` |
| `OrganizationDomain` | Host name mapping | `HostName` normalized, unique platform-wide; `IsPrimary`; `VerificationStatus` ∈ {Pending, Verified}; generated subdomains are Verified on creation |
| `OrganizationBranding` | White-label fields | One row per organization; colors restricted to named design-system tokens |
| `OrganizationMembership` | Authoritative role | `(OrganizationId, PlatformUserId)` unique; `OrganizationRole` ∈ the six tenant roles; `Status` ∈ {Active, Inactive}; `SecurityVersion` int, incremented with every role or status change; `TenantUserId` = tenant-side `User.Id` |
| `OrganizationDataPlane` | Database registry (ADR §9) | One per organization; `Provider`; `SecretReference` (never a connection string); `DatabaseDeploymentId`; `LocationRevision`; `SchemaVersion`; `RegisteredAtUtc` |
| `PlatformUser` | ASP.NET Identity user | `IdentityUser<Guid>`; email unique; `SecurityVersion`; `IsPlatformAdmin`; no `DomainUserId` |
| `PlatformSession` | One sign-in | `PlatformUserId`; `MembershipId` and `OrganizationId` (null for platform sessions); `TokenKind`; `CreatedAtUtc`; `LastRefreshedAtUtc`; `RevokedAtUtc`; `RevocationReason` |
| `PlatformRefreshToken` | Rotated refresh token | `SessionId`; `FamilyId`; `TokenHash`; `ExpiresAtUtc`; `UsedAtUtc`; `RevokedAtUtc` |
| `PlatformSecurityState` | Single row | `AuthenticationEpoch` long; `UpdatedAtUtc`; `UpdatedBy` |
| `PlatformAuditEvent` | Append-only platform audit | Insert-only; no update or delete path in code |

Tenant-side changes in CP-04:

- `User` gains `PlatformUserId` (required after backfill) so a tenant profile traces to its platform identity without a cross-database FK.
- New `TenantDeployment` metadata table in `CarePathDbContext` with exactly one row: `OrganizationId`, `DatabaseDeploymentId`, `SchemaVersion`. Written by backfill and by CP-06 provisioning only; the runtime principal has read access only. Verification at connection checkout is CP-06.
- `ApplicationUser`, `ApplicationUserConfiguration`, and the Identity tables leave `CarePathDbContext` by migration. The migration is forward-only for Identity data: `Down` must not drop control-plane rows, and the old Identity tables are kept until a later cleanup migration after verification.
- `User.Role` remains as a mirror (FR-007). `AdminUsersController` role and status changes are redirected to membership management.

Validation (FluentValidation in Application): slug format and reserved list; host name RFC 1123 and ≤ 253 characters; color tokens from an allowlist; email format; IANA time zone; membership role from the tenant role set.

### 3.3 Integration Requirements

- **Layers**: Domain (control-plane entities and enums in `Entities/Platform/`), Application (services, validators, `IOrganizationContext`, extended `ICurrentUserContext`, `IAuthorizationFreshnessCheck`), Infrastructure (`ControlPlaneDbContext` with its own migrations for both providers, host resolver, Identity re-homing, session and refresh stores, backfill), WebApi (host resolution and freshness middleware, platform and organization controllers, auth changes), Contracts and Client (organization DTOs, extended `AuthTokenResponse`), Web (branding, service states, shell organization cue, sign-out).
- **External services**: none. No email, SMS, DNS, or certificate automation. Custom-domain verification is a later spec.
- **SignalR**: none.
- **Database**: the control plane is a separate database on the same server as the existing database in development and HomeLab, and a separate Azure SQL database in production. Both SQL Server and PostgreSQL migration sets are produced (ADR §8.1).

### 3.4 Security & Authorization

- Roles: the six tenant roles keep their names in CP-04; `PlatformAdmin` is new. Renaming `Admin` to `OrganizationAdmin` is CP-05.
- Pipeline order in WebApi: forwarded headers (allowlisted proxies) → host resolution → JWT signature, issuer, audience, lifetime, required claims → authorization freshness (FR-010) → controllers. Nothing after host resolution runs for an unresolved host.
- Object-level authorization in `Sprint4ObjectAuthorizationService` is unchanged in CP-04 because only the backfilled organization's data is reachable; the verified-context organization precondition is CP-05.
- Secrets: `OrganizationDataPlane.SecretReference` names a secret in configuration or a secret manager; the control plane never stores a connection string.
- HIPAA: tenant PHI access continues through `IPhiAuditLogger`, which gains `OrganizationId` in `PhiAuditEntry` so CP-06 can route it.

### 3.5 Control-Plane Data Classification (ADR §8.6)

| Data | Classification | Controls in CP-04 |
|---|---|---|
| Organization, domain, branding, data-plane registry, security state | Platform configuration, not PHI | Encrypted at rest (Azure SQL TDE in production); PlatformAdmin-only writes; audited |
| PlatformUser (email, name, hash, lockout) | Personal data, security-critical | Same, plus Identity hashing, lockout, and no email in audit or logs |
| Membership with a staff role | Personal data | Audited, organization-scoped reads |
| Membership with role Client, and its TenantUserId | PHI-adjacent (reveals a healthcare relationship) | Never returned by list endpoints to other organizations; never in platform audit payloads beyond ids; excluded from any future platform analytics; retained under the 6-year rule |
| PlatformSession, PlatformRefreshToken | Security-critical | Hashes only; retained for the token lifetime plus the revocation retention window; never logged |
| PlatformAuditEvent | Security evidence | Append-only; ids and hashes only |

The Security/Compliance Owner must confirm this table before the design spec is approved.

---

## 4. Non-Functional Requirements

### 4.1 Performance

- Host resolution plus the FR-010 freshness read shall add at most two control-plane round trips per protected request and complete in under 10 ms at p95 on the same network. The design should satisfy FR-010 with one query joining organization, membership, user, session, and security state.
- Public branding for a registered, non-suspended host may be cached for up to 60 seconds. Nothing that gates access may be cached.
- Login end-to-end under 400 ms at p95.

### 4.2 Scalability

- Sized for up to 50 organizations and up to 5,000 platform users over three years (ADR §8). Control-plane tables are small; the hot paths are indexed lookups on `OrganizationDomain.HostName` and `PlatformSession.Id`.
- Control-plane capacity is reserved separately from tenant pools (ADR §8.7). Production sizing is a CP-06 and operations task; CP-04 must not introduce per-request work that scales with tenant count.

### 4.3 Reliability

- Control-plane reads use a bounded timeout and a circuit breaker; on timeout or open circuit the API returns 503 and pauses nothing that has not started.
- Backfill and PlatformAdmin bootstrap are idempotent and safe on every startup.
- Control-plane migrations run before tenant migrations at startup when `Database:AutoMigrate` is true.
- The authentication epoch lives in the control-plane database in CP-04; its durable out-of-band copy for recovery is defined with the control-plane restore procedure in CP-06.

### 4.4 Usability

- Screens follow the SaaS journey tab of the wireframe: agency-branded sign-in, agency setup, daily operations shell with organization cue, and the four service states. No screen outside the wireframe is introduced.
- Service-state copy describes next actions without database, token, or routing detail.
- Unknown host, suspended host, wrong-organization token, and invalid credentials are indistinguishable to the client.

### 4.5 Compliance

- PHI stays in the tenant database. Control-plane data is classified per §3.5.
- Retention: PlatformAuditEvent, membership history, and revocation evidence for 6 years.
- No PHI or credentials in logs or URLs; organization slugs may appear in host names.
- Simulation controls in the wireframe are prototype-only and must not ship.

---

## 5. Success Criteria

### 5.1 Quantitative Metrics

- Two-organization tests: distinct tokens per host, each rejected at the other host, in 100 percent of cases.
- Revocation tests (FR-028) pass on two application instances for all six revocation causes.
- Backfill on a copy of the HomeLab database yields one organization, one data-plane record, one TenantDeployment row, and one membership per active user, with zero login failures for existing accounts.
- Freshness overhead under 10 ms p95 in the integration test host.
- `dotnet build CarePath.sln` with zero warnings; full test suite passes.

### 5.2 Qualitative Goals

- A PlatformAdmin registers a second organization through the API without touching a database, and the readiness screen reflects Provisioning until CP-06 flips it.
- `dotnet-code-reviewer` and `/hipaa-check` find no PHI in the control plane beyond the classified membership fields and no cross-organization path in login, refresh, or switching.
- CP-05 and CP-06 start without revisiting the control-plane schema.

---

## 6. Scope & Boundaries

### 6.1 In Scope

- Control-plane entities, `ControlPlaneDbContext`, migrations for both providers, DI registration.
- Moving ASP.NET Identity to the control plane; sessions, rotated refresh token families, security versions, authentication epoch.
- Host resolution middleware, `IOrganizationContext`, and per-request authorization freshness middleware (ADR §8.2 steps 1 to 4).
- Organization-bound login, refresh, sign-out; extended token claims; `ICurrentUserContext` extension.
- PlatformAdmin role and bootstrap; platform organization, domain, branding, readiness endpoints at `manage.*`.
- Organization Admin membership endpoints.
- Anonymous branding endpoint with ServiceState; web sign-in branding, service-state screens, shell organization cue, sign-out.
- Backfill of the existing deployment as tenant one, including OrganizationDataPlane registration and the TenantDeployment metadata row.
- PlatformAuditEvent append-only store.
- `User.PlatformUserId`, `PhiAuditEntry.OrganizationId`.

### 6.2 Out of Scope

- Creating, migrating, or routing to tenant databases; database identity verification at connection checkout; scoped `CarePathDbContext` factory; Recovering and Failed workflows (CP-06).
- Verified-context organization precondition in object authorization; renaming `Admin`; removing `User.Role` (CP-05).
- Any organization switching, membership listing for end users, or cross-subdomain single sign-on. Access is by domain only.
- Custom domain verification, DNS, certificates, wildcard TLS.
- Break-glass elevated access.
- Subscription, billing plan, pricing.
- Organization-scoped file storage, Data Protection purposes, cache keys (CP-06).
- Bounding already-admitted long-running requests after revocation (ADR §10.1); CP-04 has no streams or exports that outlive a request.
- Persisting the tenant PHI audit log to a tenant table (CP-06).
- External SSO per organization.

### 6.3 Future Considerations

- `PlatformSession` and `token_kind` leave room for a break-glass session kind.
- `OrganizationDomain.VerificationStatus` exists so custom domains add only a verification workflow.
- `OrganizationDataPlane` is shaped so CP-06 adds pool, region, and health fields without a breaking change.

---

## 7. Dependencies & Assumptions

### 7.1 Technical Dependencies

- .NET 10, EF Core 10, ASP.NET Core Identity 10 (already in `Directory.Packages.props`).
- Two DbContexts in one process, each with its own migrations assembly per provider.
- A reverse proxy in production that forwards the original host; `ForwardedHeaders` configured with an explicit known-proxy list.
- Wildcard DNS and certificate for `*.carepathhealth.com` (infrastructure task).
- Blazor client served per organization host, or CORS origins registered per organization domain (CP-04 configuration task).

### 7.2 Business Dependencies

- ADR 0003 accepted (2026-09-08).
- Product Owner supplies the default organization name, slug, and time zone for the backfill.
- Security/Compliance Owner confirms §3.5 before design approval.
- Product Owner approves the SaaS journey wireframe as the CP-04 UI source of truth.

### 7.3 Assumptions

- The current deployment is one organization; all existing users belong to it.
- Every existing `ApplicationUser` has exactly one linked domain `User` (enforced today by the unique index on `DomainUserId`).
- Email remains the login identifier.
- Access token lifetime stays at the configured default (60 minutes) with refresh at 7 days; FR-010 makes the access lifetime a latency bound for nothing, since revocation is immediate.

---

## 8. Risks & Mitigation

| Risk | Likelihood | Impact | Mitigation Strategy |
|------|------------|--------|---------------------|
| Identity move breaks existing logins | Medium | Critical | Backfill copies rows including password hash and security stamp; integration test logs in as every seeded user before and after; forward-only migration keeps old tables until verified. |
| Per-request freshness read becomes a latency or availability bottleneck | Medium | High | Single indexed query; bounded timeout; circuit breaker returns PHI-safe 503; p95 budget measured in tests; control-plane capacity reserved separately. |
| Host spoofing through X-Forwarded-Host | Medium | Critical | Forwarded headers honored only from configured known proxies; test with an unlisted source. |
| Two DbContexts complicate membership creation | Medium | Medium | Control-plane write first, tenant profile second, compensating deactivation on failure; documented in design; no distributed transaction. |
| Control-plane migrations diverge between providers | Medium | Medium | Both sets generated in the same task; CI intent check per ADR §8.1. |
| `User.Role` and membership role drift | Low | Medium | All role writes go through the membership service, which mirrors to `User.Role` until CP-05 removes it. |
| Epoch increment during an incident logs out every user | Low | Medium | Documented as a deliberate privileged action; audit event; runbook in CP-06 recovery procedure. |
| Missing default organization configuration in a new environment | Medium | Low | Startup fails closed naming the configuration key. |

---

## 9. Stakeholder Sign-Off

| Stakeholder | Role | Status | Date | Comments |
|-------------|------|--------|------|----------|
| Tobi Kareem | Product Owner | Pending | - | Also approves the SaaS journey wireframe |
| Tobi Kareem | Tech Lead | Pending | - | - |
| TBD | Security/Compliance Owner | Pending | - | Must confirm §3.5 classification |

---

## 10. Related Documents

- [ADR 0003 — Multi-Tenant SaaS Database Strategy](../decisions/0003-multi-tenant-saas-database-strategy.md)
- [UI Design System — SaaS Journey Proposal](../02-design/ui-design-system.md)
- `Documentation/Wireframes/carepath-wireframe.html` (SaaS journey tab)
- [Architecture.md](../../Documentation/Architecture.md)
- [Design Spec](../02-design/cp-04-control-plane-foundation.md) — after this spec is approved
- [Tasks Spec](../03-tasks/cp-04-control-plane-foundation.md) — after design is approved
- [CP-02 Infrastructure / EF Core](cp-02-infrastructure-ef-core.md)

---

## 11. Wireframe Step Map

| Journey step | Host | Actor | Requirements exercised | Behind the screen |
|---|---|---|---|---|
| 1 Welcome | carepathhealth.com | Anyone | FR-021 (no discovery) | Public site; no sign-in, lookup, or agency link; staff pointed to `manage.*` |
| 2 Platform sign-in | manage.carepathhealth.com | PlatformAdmin | FR-017, FR-026 | Operator credentials; platform session and `token_kind = platform`; no organization |
| 3 Organizations | manage.carepathhealth.com | PlatformAdmin | FR-018 | List with status, readiness, member count; "New organization" |
| 4 New organization | manage.carepathhealth.com | PlatformAdmin | FR-018, FR-019, FR-025 | Legal and display name, slug with address preview, time zone, region, first administrator; creates Pending / Provisioning, primary domain, first Admin membership, audit event |
| 5 Organization | manage.carepathhealth.com | PlatformAdmin | FR-018, FR-021 (branding ServiceState) | Readiness rows (address, database, encryption and backup, administrator invitation); Set maintenance and Suspend once Ready; CP-06 performs the checks, CP-04 shows the state |
| 6 Agency sign-in | {slug}.carepathhealth.com | Member | FR-003, FR-004, FR-008, FR-009, FR-021, FR-026 | Host resolution, branding, credential and membership check, session and token issue |
| 7 Agency setup | {slug}.carepathhealth.com | Admin | FR-020, FR-022 | Display name, monogram or logo, approved theme, support contact, live sign-in preview; onboarding sidebar |
| 8 Daily operations | {slug}.carepathhealth.com | Member | FR-010, FR-011, FR-016, FR-027 | Freshness check on every request; organization and role cue in shell |
| 9 Access & recovery | {slug}.carepathhealth.com | Member | FR-004, FR-011, FR-013, FR-015, FR-018 | Maintenance = 503 with branding; Access changed = 401 after revocation; Service outage = control-plane 503; Recovery = ReadinessState Recovering (CP-06) |

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-09-08 | CarePath Health | Initial draft from ADR 0003 decisions and 2026-09-08 code review |
| 1.1 | 2026-09-08 | CarePath Health | Aligned to ADR §8.2 pipeline, §8.6 classification and no-cache rule, §9 registry fields, §10.1 sessions, security versions, epoch, refresh families; added service states, platform hosts, and wireframe step map |
| 1.3 | 2026-09-08 | CarePath Health | Wireframe rewritten as production-style screens with a platform sign-in, organizations list, new-organization form, and organization detail; step map is now nine steps |
| 1.2 | 2026-09-08 | CarePath Health | Removed organization switcher and membership listing (FR-021 now forbids them); access to an organization is only through its own domain; step map reduced to seven steps |
