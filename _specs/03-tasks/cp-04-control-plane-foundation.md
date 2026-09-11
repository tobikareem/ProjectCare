# CP-04 — Control Plane Foundation: Implementation Tasks

**Status**: Draft — prepared for review; implementation is not authorized by this file
**Prepared**: 2026-09-10
**Requirements**: [CP-04 requirements](../01-requirements/cp-04-control-plane-foundation.md)
**Design**: [CP-04 design](../02-design/cp-04-control-plane-foundation.md)

## Scope and approval

Continue the B2B agency model: one operational database per agency, centralized authentication and membership,
domain-only agency entry and monogram-only branding in CP-04. Shared patient identity, cross-agency clinical history,
NIN/SSN collection and patient/account separation are future proposals, not additions to this implementation scope.
CP-04 admits operational traffic only to the verified existing tenant; CP-06 enables newly provisioned tenant databases.

This draft makes the implementation sequence reviewable while approval remains pending. No task begins until the
revised design and this ledger are approved and the requirements' Security/Compliance classification gate is recorded.
Do not infer that sign-off from a request to continue development or from an automated engineering review.

Task IDs are local to CP-04; reference them as CP-04/TASK-001 to avoid ambiguity with historical task numbering.
Every task starts Pending. File groups identify bounded ownership; expand brace notation into individual files.
Estimates are planning estimates, not delivery commitments; split a task further if actual scope exceeds four hours.
Existing code/tests must be inspected before implementation so listed destinations match current conventions.

## Implementation sequence

### TASK-001: Define organization and membership entities

- **Status**: Pending
- **Layer**: Domain
- **Dependencies**: Approved design, task ledger and classification sign-off
- **Estimate**: 3 hours
- **Priority**: Critical
- **Success criteria**: Organization, OrganizationDomain, OrganizationBranding, OrganizationMembership and status enums follow the design, including Updating; pure computed-state tests pass.
- **Files**: CREATE Domain/Entities/Platform/{Organization,OrganizationDomain,OrganizationBranding,OrganizationMembership}.cs; CREATE Domain/Enumerations/{OrganizationStatus,OrganizationReadinessState,DomainVerificationStatus,MembershipStatus}.cs; CREATE Domain.Tests/Platform/OrganizationStateTests.cs

### TASK-002: Define session and refresh security state

- **Status**: Pending
- **Layer**: Domain
- **Dependencies**: TASK-001
- **Estimate**: 3 hours
- **Priority**: Critical
- **Success criteria**: Session issuance baseline, expiry boundaries, token kind and revocation state are represented without external dependencies.
- **Files**: CREATE Domain/Entities/Platform/{PlatformSession,PlatformRefreshToken,PlatformSecurityState}.cs; CREATE Domain/Enumerations/TokenKind.cs; CREATE Domain.Tests/Platform/SessionStateTests.cs

### TASK-003: Define workflows, data-plane registry and audit entities

- **Status**: Pending
- **Layer**: Domain
- **Dependencies**: TASK-001, TASK-002
- **Estimate**: 4 hours
- **Priority**: Critical
- **Success criteria**: Workflow intended states and lease fields, registry IDs and append-only audit contract are modeled; no clinical values in workflow fields.
- **Files**: CREATE Domain/Entities/Platform/{ControlPlaneWorkflow,OrganizationDataPlane,PlatformAuditEvent}.cs; CREATE Domain/Entities/Identity/TenantDeployment.cs; MODIFY Domain/Entities/Identity/User.cs; CREATE Domain/Interfaces/Repositories/IControlPlaneUnitOfWork.cs

### TASK-004: Define client-safe platform and auth contracts

- **Status**: Pending
- **Layer**: Contracts
- **Dependencies**: TASK-001, TASK-002, TASK-003
- **Estimate**: 4 hours
- **Priority**: High
- **Success criteria**: DTOs and enum mirrors match design; host-mode response has no directory/secrets; logo stays null; assembly boundary/parity tests pass.
- **Files**: CREATE CarePath.Contracts/Platform/ DTOs and enum mirrors listed in design section 3.1; MODIFY CarePath.Contracts/Auth/AuthTokenResponse.cs; MODIFY Domain.Tests/Enumerations/ContractEnumParityTests.cs

### TASK-005: Define application boundaries and validators

- **Status**: Pending
- **Layer**: Application
- **Dependencies**: TASK-004
- **Estimate**: 4 hours
- **Priority**: Critical
- **Success criteria**: Separate platform/tenant identity contract, snapshot, workflow storage/clock boundaries and request validators are present; API clients cannot set Updating/Provisioning.
- **Files**: CREATE Application/Abstractions/Platform/ interfaces listed in design section 3.2; MODIFY Application/Abstractions/Auth/{ICurrentUserContext,JwtTokenRequest}.cs; CREATE Application/Platform/Validators/ validators listed in design section 3.5; CREATE Application.Tests/Platform/PlatformValidationTests.cs

### TASK-006: Implement pure freshness decisions

- **Status**: Pending
- **Layer**: Application
- **Dependencies**: TASK-005
- **Estimate**: 3 hours
- **Priority**: Critical
- **Success criteria**: Reject mismatched subject/session/membership links, inactive/updating status and stale versions; deliberately unequal tenant/platform IDs work; tests cover platform vs tenant role boundaries.
- **Files**: CREATE Application/Platform/Services/AuthorizationFreshnessService.cs; CREATE Application.Tests/Platform/AuthorizationFreshnessServiceTests.cs

### TASK-007: Map organization and membership persistence

- **Status**: Pending
- **Layer**: Infrastructure
- **Dependencies**: TASK-003, TASK-005
- **Estimate**: 4 hours
- **Priority**: Critical
- **Success criteria**: ControlPlaneDbContext and PlatformUser model organization/membership relations, indexes, nullability and Restrict foreign keys for both providers.
- **Files**: CREATE Infrastructure/ControlPlane/{ControlPlaneDbContext.cs,Identity/PlatformUser.cs}; CREATE Infrastructure/ControlPlane/Configurations/ organization, domain, branding and membership configurations

### TASK-008: Map session, workflow, registry and audit persistence

- **Status**: Pending
- **Layer**: Infrastructure
- **Dependencies**: TASK-007
- **Estimate**: 4 hours
- **Priority**: Critical
- **Success criteria**: Unique token hashes and unfinished workflows, concurrency tokens, lease fields and append-only audit restrictions match design; no cross-database FK.
- **Files**: CREATE Infrastructure/ControlPlane/Configurations/ session, refresh, workflow, registry, security-state and audit configurations; CREATE Infrastructure/ControlPlane/Repositories/ControlPlaneUnitOfWork.cs; MODIFY Infrastructure/Persistence/Repositories/Repository.cs

### TASK-009: Create and validate control-plane migrations

- **Status**: Pending
- **Layer**: Infrastructure
- **Dependencies**: TASK-008
- **Estimate**: 4 hours
- **Priority**: Critical
- **Success criteria**: SQL Server and PostgreSQL migrations express equivalent schema intent; scripts reviewed; migrations not applied to production; fixtures can provision isolated test databases.
- **Files**: CREATE Infrastructure/ControlPlane/Migrations/InitialControlPlane migration; CREATE Infrastructure.Migrations.PostgreSql/ControlPlane/InitialControlPlane migration; CREATE Infrastructure.Tests/ControlPlane/ProviderFixture.cs

### TASK-010: Prepare additive tenant identity migration

- **Status**: Pending
- **Layer**: Infrastructure
- **Dependencies**: TASK-009
- **Estimate**: 3 hours
- **Priority**: Critical
- **Success criteria**: Tenant marker and nullable platform linkage added; legacy Identity tables preserved; destructive Down guarded; ownership uniqueness tested.
- **Files**: MODIFY Infrastructure/Persistence/CarePathDbContext.cs; CREATE Infrastructure/Persistence/Configurations/Identity/TenantDeploymentConfiguration.cs; MODIFY user configuration; CREATE tenant migrations for both providers

### TASK-011: Implement host resolution and authoritative reads

- **Status**: Pending
- **Layer**: Infrastructure
- **Dependencies**: TASK-006, TASK-009
- **Estimate**: 4 hours
- **Priority**: Critical
- **Success criteria**: Known proxy/host rules, default-tenant admission and snapshot reads fail closed; timeout and circuit breaker have no cached security fallback.
- **Files**: CREATE Infrastructure/ControlPlane/{HostOrganizationResolver,AuthorizationSnapshotReader,ControlPlaneCircuitBreaker}.cs; CREATE Infrastructure.Tests/ControlPlane/ResolutionTests.cs

### TASK-012: Implement session storage and refresh rotation

- **Status**: Pending
- **Layer**: Infrastructure
- **Dependencies**: TASK-009
- **Estimate**: 4 hours
- **Priority**: Critical
- **Success criteria**: Persist immutable issuance versions, enforce expiry and serialized rotation; replay revocation commits on rejection; real-provider concurrency tests pass.
- **Files**: CREATE Infrastructure/Auth/SessionStore.cs; MODIFY Infrastructure/Auth/JwtTokenService.cs; CREATE Infrastructure.Tests/ControlPlane/RefreshRotationTests.cs

### TASK-013: Implement login, refresh and logout coordination

- **Status**: Pending
- **Layer**: Application + Infrastructure
- **Dependencies**: TASK-006, TASK-011, TASK-012
- **Estimate**: 4 hours
- **Priority**: Critical
- **Success criteria**: Credential security stamp is rechecked; tenant current-user binding uses linked tenant ID; refresh uses issued baseline; legacy refresh path retired.
- **Files**: CREATE Application/Platform/Services/PlatformAuthService.cs; MODIFY Infrastructure/Auth/IdentityService.cs; CREATE Application.Tests/Platform/PlatformAuthServiceTests.cs

### TASK-014: Implement durable workflow ownership and reconciliation

- **Status**: Pending
- **Layer**: Infrastructure
- **Dependencies**: TASK-008, TASK-009
- **Estimate**: 4 hours
- **Priority**: Critical
- **Success criteria**: Leases and expected versions fence stale workers; one unfinished operation per target; commit-boundary failure and resume tests pass on both providers.
- **Files**: CREATE Infrastructure/ControlPlane/WorkflowStore.cs; CREATE Infrastructure.Tests/ControlPlane/WorkflowRecoveryTests.cs

### TASK-015: Implement membership creation and first-admin registration

- **Status**: Pending
- **Layer**: Application
- **Dependencies**: TASK-013, TASK-014
- **Estimate**: 4 hours
- **Priority**: Critical
- **Success criteria**: Creation activates last, retries reuse the same operation; first admin stays Provisioning until CP-06; never writes tenant-one data for another organization.
- **Files**: CREATE Application/Platform/Services/MembershipService.cs; MODIFY Infrastructure/Auth/IdentityProvisioningService.cs; CREATE Application.Tests/Platform/MembershipCreationTests.cs

### TASK-016: Implement role/status mutation workflow

- **Status**: Pending
- **Layer**: Application
- **Dependencies**: TASK-015
- **Estimate**: 4 hours
- **Priority**: Critical
- **Success criteria**: Updating blocks access before tenant projection; versions/revocation precede finalization; concurrent final-admin removal fails safely; failures resume without automatic reactivation.
- **Files**: MODIFY Application/Platform/Services/MembershipService.cs; CREATE Application.Tests/Platform/MembershipMutationTests.cs; CREATE Infrastructure.Tests/ControlPlane/LastAdministratorConcurrencyTests.cs

### TASK-017: Integrate every existing account lifecycle path

- **Status**: Pending
- **Layer**: Application + API
- **Dependencies**: TASK-016
- **Estimate**: 4 hours
- **Priority**: Critical
- **Success criteria**: Admin and caregiver creation/termination/reactivation use coordinator; authorized Coordinator scope preserved; regression tests prove termination revokes access.
- **Files**: MODIFY Application/Identity/Services/CaregiverOperationsService.cs; MODIFY existing admin-user management services and WebApi/Controllers/AdminUsersController.cs; MODIFY corresponding Application.Tests/Identity tests

### TASK-018: Implement platform organization and branding services

- **Status**: Pending
- **Layer**: Application
- **Dependencies**: TASK-015, TASK-016
- **Estimate**: 4 hours
- **Priority**: High
- **Success criteria**: Registration, pagination, maintenance, suspension and reactivation follow policies and audit contracts; branding is monogram-only; availability does not imply provisioning completion.
- **Files**: CREATE Application/Platform/Services/{OrganizationManagementService,BrandingService}.cs; CREATE Application/Common/Mapping/PlatformContractMapper.cs; CREATE Application.Tests/Platform/OrganizationManagementTests.cs

### TASK-019: Implement backfill and operator bootstrap

- **Status**: Pending
- **Layer**: Infrastructure
- **Dependencies**: TASK-010, TASK-014, TASK-017
- **Estimate**: 4 hours
- **Priority**: Critical
- **Success criteria**: Lease-controlled saga preserves password hashes and tenant links; completed marker validation does not change maintenance/suspension; old writers drained during cutover.
- **Files**: CREATE Infrastructure/ControlPlane/Bootstrap/{ControlPlaneBackfill,PlatformAdminBootstrap,LegacyIdentityUser}.cs; CREATE Infrastructure.Tests/ControlPlane/BackfillRecoveryTests.cs

### TASK-020: Integrate middleware, endpoints and startup gates

- **Status**: Pending
- **Layer**: WebApi
- **Dependencies**: TASK-011, TASK-013, TASK-018, TASK-019
- **Estimate**: 4 hours
- **Priority**: Critical
- **Success criteria**: Explicit host context, verified current-user binding, freshness before authorization, correct 401/404/503 and default-tenant-only admission; no production auto-cutover.
- **Files**: CREATE WebApi/Middleware/{OrganizationResolutionMiddleware,AuthorizationFreshnessMiddleware}.cs; MODIFY WebApi/Security/HttpCurrentUserContext.cs; CREATE platform/organization/membership controllers; MODIFY WebApi/Controllers/AuthController.cs and WebApi/Program.cs

### TASK-021: Verify cross-instance security and provider failures

- **Status**: Pending
- **Layer**: Integration tests
- **Dependencies**: TASK-020
- **Estimate**: 4 hours
- **Priority**: Critical
- **Success criteria**: Two API instances reject revoked tokens/refresh; wrong host and subject IDs, maintenance restart, stale workers, failed audits and connection failures never disclose operational records.
- **Files**: CREATE Infrastructure.Tests/ControlPlane/ControlPlaneApiIntegrationTests.cs; MODIFY applicable API fixtures in existing test projects

### TASK-022: Implement typed clients and shared branding/dialog components

- **Status**: Pending
- **Layer**: Client + Client.UI
- **Dependencies**: TASK-004, TASK-020
- **Estimate**: 4 hours
- **Priority**: High
- **Success criteria**: Typed APIs map safe errors; one shared ConfirmDialog preserves focus; branding uses approved tokens; host bootstrap exposes no agency chooser.
- **Files**: CREATE CarePath.Client/Api/{OrganizationClient,MembershipsClient,PlatformOrganizationsClient}.cs; MODIFY auth client; CREATE CarePath.Client.UI/Components/{ConfirmDialog,BrandedHero,OrganizationBadge}.razor; MODIFY CarePath.Client.UI/wwwroot/carepath-ui.css

### TASK-023: Implement agency sign-in, setup and service states

- **Status**: Pending
- **Layer**: Web
- **Dependencies**: TASK-022
- **Estimate**: 4 hours
- **Priority**: High
- **Success criteria**: One login route chooses form by explicit host mode; name/monogram/theme persist; status views fail closed; no logo upload or organization switching; keyboard/narrow-layout tests pass.
- **Files**: MODIFY CarePath.Web/Pages/Login.razor and auth state provider; CREATE agency setup and service-state pages per design section 6.3; MODIFY layout/nav; CREATE CarePath.Web.Tests/AgencySetupTests.cs

### TASK-024: Implement platform console and lifecycle dialogs

- **Status**: Pending
- **Layer**: Web
- **Dependencies**: TASK-022, TASK-023
- **Estimate**: 4 hours
- **Priority**: High
- **Success criteria**: Platform login and organization list/create/detail follow wireframe; typed confirmation, member-facing maintenance copy and protected operator actions work.
- **Files**: CREATE CarePath.Web/Pages/Platform/{OperatorLoginForm,Organizations,OrganizationNew,OrganizationDetail}.razor; CREATE CarePath.Web.Tests/PlatformOrganizationTests.cs

### TASK-025: Document and rehearse cutover/forward recovery

- **Status**: Pending
- **Layer**: Operations + Infrastructure
- **Dependencies**: TASK-019, TASK-021, TASK-024
- **Estimate**: 4 hours
- **Priority**: Critical
- **Success criteria**: Synthetic cutover preserves identity, blocks old binaries after cutoff and rejects unsafe rollback; separate control-plane capacity and pending deployment evidence documented.
- **Files**: CREATE Documentation/Runbooks/cp-04-cutover.md; MODIFY environment configuration examples without secrets; CREATE Infrastructure.Tests/ControlPlane/CutoverAcceptanceTests.cs

### TASK-026: Complete release verification and reviews

- **Status**: Pending
- **Layer**: All
- **Dependencies**: TASK-025
- **Estimate**: 4 hours
- **Priority**: Critical
- **Success criteria**: Full build has zero warnings; full solution tests and both provider checks pass; dotnet-code-reviewer has no critical findings; HIPAA engineering review and required approvals recorded.
- **Files**: MODIFY this task ledger with evidence; CREATE approved verification evidence under Documentation/Runbooks as needed; no unrelated refactors

## Execution checkpoints

- After TASK-003: additive domain foundation reviewed; no application startup/auth changes yet.
- After TASK-010: both migration sets inspected; production database changes remain a separate deployment action.
- After TASK-021: server admission, invalidation and recovery behavior proven before UI acceptance.
- After TASK-026: implementation complete only with linked evidence, reviewer approval and all required gates.

## Approval record

| Gate | Status |
|---|---|
| Requirements | Approved in requirements document; pending classification sign-off remains binding |
| Revised design | Pending recorded approval |
| Security/Compliance classification | Pending; accountable owner not yet recorded |
| This task ledger | Draft; pending approval |
| Production readiness | Not evaluated; CP-06 and operational gates remain |
