# Sprint 8 - Control Plane Foundation (CP-04)

Status: Approved (2026-10-21, via Claude plan review) - implementation contract in `_specs/sprints/sprint-08-tasks.md`; feature specs: `_specs/01-requirements/cp-04-control-plane-foundation.md`, `_specs/02-design/cp-04-control-plane-foundation.md`, `_specs/03-tasks/cp-04-control-plane-foundation.md`
Primary outcome: CarePath can resolve a tenant from the request host and authenticate a user against a separate, non-PHI control plane before any PHI is touched.

## Sprint Goal

Implement the first of three ADR 0003 slices: control-plane data model and database, platform identity moved out of the PHI database, host-based organization resolution, organization-bound tokens, PlatformAdmin and organization-admin management endpoints, anonymous branding, and backfill of the existing deployment as tenant one.

## Scope

In scope:

- `ControlPlaneDbContext` (separate database) with migrations for SQL Server and PostgreSQL.
- Organization, domain, branding, membership, data-plane locator, append-only platform audit.
- ASP.NET Core Identity re-homed to the control plane (`PlatformUser`, `PlatformRefreshToken`).
- Host resolution middleware, trusted forwarded headers, fail-closed identical error bodies.
- JWT claims `organization_id`, `membership_id`, `token_kind`; token-host binding; `PlatformAdmin` policy.
- `ICurrentUserContext.OrganizationId` / `MembershipId`.
- Platform organization endpoints, organization membership endpoints, anonymous branding endpoint.
- Startup backfill: default organization, legacy identity copy, memberships, PlatformAdmin bootstrap.
- Typed clients and Web login branding / organization-aware auth state.
- Two-organization integration test suite.

Out of scope (CP-05 / CP-06 / later):

- Creating, migrating, or routing to tenant databases; `SchemaVersion` enforcement; scoped `CarePathDbContext` factory.
- Tenant precondition in object authorization; `Admin` -> `OrganizationAdmin` rename; removal of `User.Role`.
- Dropping the legacy tenant `AspNet*` tables (forward-only; after HomeLab verification).
- `SecurityVersion` enforcement, logout/session revocation, authentication epoch.
- Custom domain verification, DNS, certificates; break-glass access; subscriptions; SSO.

## Stories

See requirements spec §2 (organization registration, tenant-specific login, one identity many memberships, token and host must agree, existing deployment becomes tenant one, membership management, organization lifecycle, branding, platform administration boundary).

## Exit Gate

- Two-organization integration tests pass: distinct tokens per host; each token rejected at the other host; no-membership login indistinguishable from invalid credentials; suspension immediate.
- Backfill on an existing database yields one organization, one data-plane record, one membership per active user, and zero login failures for existing accounts.
- `dotnet build CarePath.sln` zero warnings; full test suite green; `dotnet-code-reviewer` and `/hipaa-check` findings resolved.
- A PlatformAdmin can register a second organization through the API without database access.
- Control plane contains no PHI field (reflection test) and the classification flag for the Security/Compliance owner is recorded.
