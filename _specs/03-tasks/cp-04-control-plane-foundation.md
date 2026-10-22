# Tasks Breakdown: CP-04 Control Plane Foundation

**Date**: 2026-10-21  
**Author**: CarePath Health  
**Project**: CarePath Health  
**Status**: Approved  
**Spec Number**: CP-04  
**Related Specs**:
- **Requirements**: [cp-04-control-plane-foundation.md](../01-requirements/cp-04-control-plane-foundation.md)
- **Design**: [cp-04-control-plane-foundation.md](../02-design/cp-04-control-plane-foundation.md)
- **Parent decision**: [ADR 0003](../decisions/0003-multi-tenant-saas-database-strategy.md)
- **Sprint board**: [sprint-08-tasks.md](../sprints/sprint-08-tasks.md)

> Tasks are sequenced by dependency and grouped into six implementation slices. Each slice ends with `dotnet build CarePath.sln` (zero warnings), the full `dotnet test CarePath.sln`, a `dotnet-code-reviewer` pass, and one commit. The application stays runnable between slices. Never skip ahead.

---

## Executive Summary

> **Add a physically separate, non-PHI control plane (organizations, host mapping, branding, memberships, data-plane locator, platform identity, append-only audit), resolve the organization from the request host before authentication, bind every token to one organization, and backfill the existing deployment as tenant one — without creating or routing to tenant databases.**

21 tasks (TASK-079 through TASK-099) across six slices, ~61 hours. Task numbering continues the global sequence (ADR 0001: task numbers are historical identifiers). One further task (TASK-100, drop legacy Identity tables) is explicitly deferred to CP-05.

---

## Task Summary by Slice

| Slice | Tasks | Estimate | Depends on |
|---|---|---|---|
| 1. Control-plane model and persistence | TASK-079..083 | 11 h | Specs approved |
| 2. Default organization and host resolution | TASK-084..086 | 9 h | Slice 1 |
| 3. Identity moves to the control plane | TASK-087..091 | 18 h | Slice 2 |
| 4. Platform and organization administration APIs | TASK-092..095 | 12 h | Slice 3 |
| 5. Typed clients and Web | TASK-096..097 | 5 h | Slice 4 |
| 6. Two-organization verification and close-out | TASK-098..099 | 6 h | Slice 5 |

---

## Slice 1 — Control-plane model and persistence (additive; no runtime change)

### TASK-079: Platform domain entities and enumerations
- **Layer**: CarePath.Domain
- **Dependencies**: Specs approved
- **Estimate**: 2 hours
- **Priority**: Critical
- **Success Criteria**:
  - Six entities in `Domain/Entities/Platform/` inherit `BaseEntity`, carry XML docs, and match the design shapes
  - Seven enumerations added with explicit integer values
  - `User.PlatformUserId` (`Guid?`) added
  - `Domain.Tests/Platform/ControlPlaneEntityClassificationTests.cs` asserts no property on any Platform entity matches the PHI denylist (DateOfBirth, Address, City, Zip, Diagnosis, Medical, Insurance, Allerg, Latitude, Longitude, RawContent, SourceText, Responses, Notes)
  - `Organization.IsAdmissible` has unit tests for all four status/provisioning combinations
- **Files**:
  - CREATE: `Domain/Entities/Platform/{Organization,OrganizationDomain,OrganizationBranding,OrganizationMembership,OrganizationDataPlane,PlatformAuditEvent}.cs`
  - CREATE: `Domain/Enumerations/{OrganizationStatus,ProvisioningState,DomainVerificationStatus,MembershipStatus,DataPlaneProvider,BrandingColorToken,PlatformAuditAction}.cs`
  - CREATE: `Domain.Tests/Platform/ControlPlaneEntityClassificationTests.cs`, `Domain.Tests/Platform/OrganizationTests.cs`
  - MODIFY: `Domain/Entities/Identity/User.cs`

### TASK-080: Control-plane abstractions and shared rules
- **Layer**: CarePath.Domain, CarePath.Application
- **Dependencies**: TASK-079
- **Estimate**: 2 hours
- **Priority**: Critical
- **Success Criteria**:
  - `IControlPlaneUnitOfWork` mirrors `IUnitOfWork` shape for the five platform repositories
  - `IOrganizationContext`, `OrganizationResolutionKind`, `ResolvedOrganization`, `IOrganizationHostResolver`, `HostResolution` defined in `Application/Abstractions/Tenancy/`
  - `IPlatformAuditLogger` + `PlatformAuditEntry` defined; `PhiAuditEntry` gains trailing optional `OrganizationId`
  - `HostNameNormalizer` and `OrganizationSlug` (regex + reserved list) with unit tests covering case, port, trailing dot, length, empty labels, reserved words
  - `ApplicationRoles.PlatformAdmin` constant exists and is **not** in `ApplicationRoles.All`
- **Files**:
  - CREATE: `Domain/Interfaces/Repositories/IControlPlaneUnitOfWork.cs`
  - CREATE: `Application/Abstractions/Tenancy/{IOrganizationContext,OrganizationResolutionKind,ResolvedOrganization,IOrganizationHostResolver,HostResolution}.cs`
  - CREATE: `Application/Abstractions/Audit/{IPlatformAuditLogger,PlatformAuditEntry}.cs`
  - CREATE: `Application/Platform/{HostNameNormalizer,OrganizationSlug}.cs`
  - CREATE: `Application.Tests/Platform/{HostNameNormalizerTests,OrganizationSlugTests}.cs`
  - MODIFY: `Application/Abstractions/Audit/PhiAuditEntry.cs`, `Application/Abstractions/Auth/ApplicationRoles.cs`

### TASK-081: ControlPlaneDbContext, PlatformUser, configurations, unit of work
- **Layer**: CarePath.Infrastructure
- **Dependencies**: TASK-080
- **Estimate**: 3 hours
- **Priority**: Critical
- **Success Criteria**:
  - `ControlPlaneDbContext : IdentityUserContext<PlatformUser, Guid>` with the seven DbSets and explicit Fluent configurations (lengths, unique/filtered indexes, `Restrict`, enums as strings)
  - `BaseEntityModelConventions` extracted and used by both contexts; existing `CarePathDbContextTests` still pass
  - `Repository<T>` gains a `protected internal` `DbContext` constructor; `ControlPlaneUnitOfWork` builds repositories over the control-plane context
  - `ControlPlaneDbContextTests` (InMemory + SQL Server metadata-only) verify soft-delete filter, UTC converters, unique indexes, and that `PlatformAuditEvent` has no update/delete API surface
- **Files**:
  - CREATE: `Infrastructure/ControlPlane/Persistence/ControlPlaneDbContext.cs`, `Infrastructure/ControlPlane/Persistence/ControlPlaneUnitOfWork.cs`
  - CREATE: `Infrastructure/ControlPlane/Persistence/Configurations/{Organization,OrganizationDomain,OrganizationBranding,OrganizationMembership,OrganizationDataPlane,PlatformAuditEvent,PlatformRefreshToken,PlatformUser}Configuration.cs`
  - CREATE: `Infrastructure/ControlPlane/Identity/{PlatformUser,PlatformRefreshToken}.cs`
  - CREATE: `Infrastructure/Persistence/BaseEntityModelConventions.cs`
  - CREATE: `Infrastructure.Tests/ControlPlane/ControlPlaneDbContextTests.cs`
  - MODIFY: `Infrastructure/Persistence/CarePathDbContext.cs`, `Infrastructure/Persistence/Repositories/Repository.cs`

### TASK-082: Control-plane DI, options, connection strings, audit logger, health check
- **Layer**: CarePath.Infrastructure, CarePath.WebApi
- **Dependencies**: TASK-081
- **Estimate**: 2 hours
- **Priority**: Critical
- **Success Criteria**:
  - `ControlPlaneOptions` bound from `ControlPlane:*` with `ValidateOnStart`
  - `AddControlPlane` registers the context (correct migrations assembly per provider), `AddIdentityCore<PlatformUser>`, `IControlPlaneUnitOfWork`, `IPlatformAuditLogger` (with host-denial throttle), memory cache
  - Connection string resolution never falls back to the tenant string; a test proves the throw
  - `ControlPlaneHealthCheck` tagged `ready`
  - `appsettings.json`, `appsettings.Development.json`, `appsettings.HomeLab.json` carry the documented keys (secrets excluded)
- **Files**:
  - CREATE: `Infrastructure/ControlPlane/ControlPlaneOptions.cs`, `Infrastructure/ControlPlane/ControlPlaneServiceCollectionExtensions.cs`, `Infrastructure/ControlPlane/Audit/EfPlatformAuditLogger.cs`, `WebApi/Diagnostics/ControlPlaneHealthCheck.cs`
  - MODIFY: `Infrastructure/DependencyInjection.cs`, `WebApi/Program.cs`, `WebApi/appsettings.json`, `WebApi/appsettings.Development.json`, `WebApi/appsettings.HomeLab.json`, `Infrastructure.Tests/Persistence/DependencyInjectionTests.cs`

### TASK-083: Control-plane migrations (SQL Server and PostgreSQL) and shape tests
- **Layer**: CarePath.Infrastructure
- **Dependencies**: TASK-082
- **Estimate**: 2 hours
- **Priority**: Critical
- **Success Criteria**:
  - `InitialControlPlane` generated for both providers with `--context ControlPlaneDbContext`; snapshots committed; no uncommitted model changes for either provider
  - `ControlPlaneMigrationShapeTests` reads both migration files and asserts: expected table set, no `AspNetRoles`/`AspNetUserRoles`, all FKs `Restrict`, no PHI column names, unique `Slug`/`HostName`/`(OrganizationId, PlatformUserId)`
  - `Program.cs` migrates the control plane before the tenant context when `Database:AutoMigrate` is true
  - CLAUDE.md and AGENTS.md command blocks updated with `--context`
- **Files**:
  - CREATE: `Infrastructure/ControlPlane/Migrations/<ts>_InitialControlPlane.cs` (+ Designer, `ControlPlaneDbContextModelSnapshot.cs`)
  - CREATE: `Infrastructure.Migrations.PostgreSql/Migrations/ControlPlane/<ts>_InitialControlPlane.cs` (+ Designer, snapshot)
  - CREATE: `Infrastructure.Tests/Persistence/ControlPlaneMigrationShapeTests.cs`
  - MODIFY: `WebApi/Program.cs`, `CLAUDE.md`, `AGENTS.md`

---

## Slice 2 — Default organization and host resolution (login still legacy)

### TASK-084: Default organization bootstrap
- **Layer**: CarePath.Infrastructure, CarePath.WebApi
- **Dependencies**: TASK-083
- **Estimate**: 3 hours
- **Priority**: Critical
- **Success Criteria**:
  - `ControlPlaneBootstrapper.EnsureDefaultOrganizationAsync` creates organization (Active, Provisioned), Verified domains, default branding, and data plane on an empty control plane; second run changes nothing (row counts and `UpdatedAt` unchanged)
  - Missing `ControlPlane:DefaultOrganization` on an empty control plane throws naming the key; invalid slug or host throws
  - Audit `BackfillOrganizationCreated` written once
  - Runs every startup regardless of `Database:AutoMigrate`
- **Files**:
  - CREATE: `Infrastructure/ControlPlane/Bootstrap/ControlPlaneBootstrapper.cs`, `Infrastructure.Tests/ControlPlane/ControlPlaneBootstrapperTests.cs`
  - MODIFY: `WebApi/Program.cs`

### TASK-085: Host resolver, organization context, resolution middleware, forwarded headers, shared 401 writer
- **Layer**: CarePath.Infrastructure, CarePath.WebApi
- **Dependencies**: TASK-084
- **Estimate**: 4 hours
- **Priority**: Critical
- **Success Criteria**:
  - `EfOrganizationHostResolver` issues one projected query and returns Tenant only for Verified + Active + Provisioned
  - Middleware behavior matches the design table (health bypass, platform host from config, branding pass-through, generic 401, 503 on resolver failure, throttled `HostResolutionDenied` audit with host hash)
  - `UseForwardedHeaders` configured from `ControlPlane:TrustedProxies`; forged `X-Forwarded-Host` from an unlisted source is ignored, honored from a known proxy
  - `UnauthorizedResponseWriter` used by middleware, `AuthController`, and `OnChallenge`; test asserts byte-identical bodies for unknown/pending/suspended/unverified hosts and invalid credentials
  - Existing tests pass with the default organization's `localhost` host resolving in TestServer hosts
- **Files**:
  - CREATE: `Infrastructure/ControlPlane/Resolution/EfOrganizationHostResolver.cs`, `WebApi/Tenancy/{OrganizationContext,OrganizationResolutionMiddleware,OrganizationResolutionExtensions}.cs`, `WebApi/Security/UnauthorizedResponseWriter.cs`, `Application.Tests/WebApi/OrganizationResolutionMiddlewareTests.cs`
  - MODIFY: `WebApi/Program.cs`, `WebApi/Controllers/AuthController.cs`, `WebApi/Security/AuthenticationServiceCollectionExtensions.cs`, `Application.Tests/WebApi/AuthControllerTests.cs`, `Infrastructure/DependencyInjection.cs`

### TASK-086: Anonymous branding endpoint with public cache
- **Layer**: CarePath.Application, CarePath.WebApi, CarePath.Contracts
- **Dependencies**: TASK-085
- **Estimate**: 2 hours
- **Priority**: High
- **Success Criteria**:
  - `GET /api/organization/branding` returns `OrganizationBrandingDto` for a resolved tenant and the standard 404 body otherwise
  - `IBrandingCache` (memory, `BrandingCacheSeconds`) with invalidation hook; DTO contains no storage key, member, or PHI data
  - Contracts enum mirror `BrandingColorToken` with parity test
- **Files**:
  - CREATE: `CarePath.Contracts/Organization/OrganizationBrandingDto.cs`, `CarePath.Contracts/Enumerations/BrandingColorToken.cs`, `Application/Organization/Services/{IOrganizationBrandingQuery,OrganizationBrandingQuery}.cs`, `Application/Abstractions/Tenancy/IBrandingCache.cs`, `Infrastructure/ControlPlane/Resolution/MemoryBrandingCache.cs`, `WebApi/Controllers/OrganizationBrandingController.cs`, `Application.Tests/WebApi/OrganizationBrandingControllerTests.cs`
  - MODIFY: `Application/DependencyInjection.cs`, `Infrastructure/ControlPlane/ControlPlaneServiceCollectionExtensions.cs`, enum parity tests

---

## Slice 3 — Identity moves to the control plane

### TASK-087: PlatformIdentityService and control-plane provisioning
- **Layer**: CarePath.Infrastructure, CarePath.Application (abstractions)
- **Dependencies**: TASK-083
- **Estimate**: 4 hours
- **Priority**: Critical
- **Success Criteria**:
  - `IIdentityService` reshaped: `ValidateCredentialsAsync` returns platform-shaped result (`PlatformUserId`, `Email`, `DisplayName`, `IsPlatformAdmin`); `IssueRefreshTokenAsync(userId, membershipId?, organizationId?)`; `RotateRefreshTokenAsync(token, expectedKind, expectedOrganizationId?)`
  - Implementation uses `UserManager<PlatformUser>` only (no `SignInManager`); lockout semantics preserved (5 attempts / 15 min)
  - Rotation is single-winner via conditional `ExecuteUpdateAsync`; reuse of a revoked token revokes the family and audits `RefreshTokenReuseDetected`
  - `IIdentityProvisioningService` re-implemented over `PlatformUser` (request: `PlatformUserId`, `Email`, `PhoneNumber`, `DisplayName`, `TemporaryPassword`)
- **Files**:
  - CREATE: `Infrastructure/ControlPlane/Auth/{PlatformIdentityService,PlatformIdentityProvisioningService}.cs`, `Infrastructure.Tests/ControlPlane/PlatformIdentityServiceTests.cs`
  - MODIFY: `Application/Abstractions/Auth/{IIdentityService,IdentityUserResult,RefreshTokenRotationResult,IdentityProvisioningRequest,IIdentityProvisioningService}.cs`

### TASK-088: Legacy identity copy, membership backfill, PlatformAdmin bootstrap
- **Layer**: CarePath.Infrastructure, CarePath.WebApi
- **Dependencies**: TASK-084, TASK-087
- **Estimate**: 3 hours
- **Priority**: Critical
- **Success Criteria**:
  - `CopyLegacyIdentityAsync` copies password hash, security stamp, concurrency stamp, lockout fields verbatim; no-op when `AspNetUsers` is absent or all rows exist; fails closed on `Id != DomainUserId`
  - `EnsureMembershipsAsync` creates one Active membership per active non-deleted `User` with `User.Role` and sets `User.PlatformUserId`; inactive/deleted users get no membership; idempotent
  - `EnsurePlatformAdminAsync` fails closed without password; never elevates an existing non-admin; idempotent
  - Tests run against a SQLite tenant database whose `AspNetUsers` table is created by raw SQL (the table is no longer in the model after TASK-091; the test must not depend on it)
- **Files**:
  - MODIFY: `Infrastructure/ControlPlane/Bootstrap/ControlPlaneBootstrapper.cs`, `Infrastructure.Tests/ControlPlane/ControlPlaneBootstrapperTests.cs`, `WebApi/Program.cs`

### TASK-089: LoginService and membership core service
- **Layer**: CarePath.Application
- **Dependencies**: TASK-087, TASK-085
- **Estimate**: 4 hours
- **Priority**: Critical
- **Success Criteria**:
  - `LoginService.LoginAsync` / `RefreshAsync` follow the design flow; every failure path yields one failed result; audit entries never contain an email
  - `OrganizationMembershipService` core: find active membership, `ChangeRoleAsync`, `SetStatusAsync` with last-active-Admin guard (409 `membership.last_active_admin`), `SecurityVersion++`, mirror to `User.Role`/`User.IsActive`, platform audit
  - Validators for role/status requests
  - Unit tests with `MockUnitOfWork`-style fakes for both unit-of-work interfaces
- **Files**:
  - CREATE: `Application/Auth/Services/{ILoginService,LoginService,LoginResult}.cs`, `Application/Organization/Services/{IOrganizationMembershipService,OrganizationMembershipService}.cs`, `Application/Organization/Validators/{UpdateMembershipRoleRequestValidator,UpdateMembershipStatusRequestValidator}.cs`, `Application.Tests/Auth/LoginServiceTests.cs`, `Application.Tests/Organization/OrganizationMembershipServiceTests.cs`, `Application.Tests/TestDoubles/MockControlPlaneUnitOfWork.cs`
  - MODIFY: `Application/DependencyInjection.cs`

### TASK-090: Token claims, current-user context, binding, controller switch-over
- **Layer**: CarePath.Application, CarePath.Infrastructure, CarePath.WebApi, CarePath.Contracts
- **Dependencies**: TASK-088, TASK-089
- **Estimate**: 4 hours
- **Priority**: Critical
- **Success Criteria**:
  - JWT carries `organization_id`, `membership_id`, `token_kind`; `OnTokenValidated` binding per design; `PlatformAdmin` policy; role policies require `token_kind=tenant`
  - `ICurrentUserContext.OrganizationId` / `MembershipId` implemented in `HttpCurrentUserContext` and all test fakes
  - `AuthController` delegates to `ILoginService`; `AuthTokenResponse` extended (`Role` nullable, `TokenKind`, organization fields); `AuthTokenKind` mirror
  - `AdminUserManagementService` delegates to the membership service; `IIdentityRoleManagementService` and legacy `IdentityService`/`IdentityProvisioningService` deleted with their tests
  - `AuthConfigurationTests`, `AuthControllerTests`, `AdminUserManagementServiceTests`, `JwtTokenServiceTests` updated and green
- **Files**:
  - CREATE: `CarePath.Contracts/Enumerations/AuthTokenKind.cs`
  - MODIFY: `Application/Abstractions/Auth/{ICurrentUserContext,JwtTokenRequest}.cs`, `Infrastructure/Auth/JwtTokenService.cs`, `WebApi/Security/{HttpCurrentUserContext,AuthenticationServiceCollectionExtensions,CarePathAuthorizationPolicies}.cs`, `WebApi/Controllers/AuthController.cs`, `CarePath.Contracts/Auth/AuthTokenResponse.cs`, `Application/Admin/Services/AdminUserManagementService.cs`, `Infrastructure/DependencyInjection.cs`, all `ICurrentUserContext` fakes under `Application.Tests/`, `Application.Tests/WebApi/{AuthConfigurationTests,AuthControllerTests}.cs`, `Application.Tests/Admin/AdminUserManagementServiceTests.cs`, `Infrastructure.Tests/Auth/JwtTokenServiceTests.cs`
  - DELETE: `Infrastructure/Auth/{IdentityService,IdentityProvisioningService,IdentityRoleManagementService}.cs`, `Application/Abstractions/Auth/IIdentityRoleManagementService.cs`, `Infrastructure.Tests/Auth/IdentityServiceTests.cs`

### TASK-091: Tenant migration `RemoveIdentityFromTenantContext`, de-Identity CarePathDbContext, seed rework
- **Layer**: CarePath.Infrastructure, CarePath.WebApi
- **Dependencies**: TASK-090
- **Estimate**: 3 hours
- **Priority**: Critical
- **Success Criteria**:
  - `CarePathDbContext : DbContext`; `ApplicationUser` and its configuration deleted; `UserConfiguration` maps `PlatformUserId` with a filtered unique index
  - Migration generated for both providers; scaffolded `DropTable` operations for `AspNet*` hand-removed from `Up`; `Down` drops only the new column/index; shape test asserts no `DropTable`
  - `AddIdentity<ApplicationUser>` removed from DI
  - Seed creates `PlatformUser`s + memberships through the control plane for the five fixed users, and `helpinghands` (Active, Provisioned, dev-only data plane) with the seeded caregiver's second membership; idempotent; password still fail-closed
  - `MigrationShapeTests`, `CarePathDbContextSeedTests`, `CarePathDbContextTests`, `EntityConfigurationTests` updated and green
- **Files**:
  - CREATE: `Infrastructure/Migrations/<ts>_RemoveIdentityFromTenantContext.cs` (+ Designer), `Infrastructure.Migrations.PostgreSql/Migrations/<ts>_RemoveIdentityFromTenantContext.cs` (+ Designer)
  - MODIFY: `Infrastructure/Persistence/CarePathDbContext.cs`, `Infrastructure/Persistence/Configurations/Identity/UserConfiguration.cs`, `Infrastructure/Persistence/CarePathDbContextSeed.cs`, `Infrastructure/DependencyInjection.cs`, `WebApi/Program.cs`, both model snapshots, `Infrastructure.Tests/Persistence/{MigrationShapeTests,CarePathDbContextSeedTests,CarePathDbContextTests,EntityConfigurationTests}.cs`
  - DELETE: `Infrastructure/Identity/ApplicationUser.cs`, `Infrastructure/Persistence/Configurations/Identity/ApplicationUserConfiguration.cs`

---

## Slice 4 — Platform and organization administration APIs

### TASK-092: Platform and organization contracts
- **Layer**: CarePath.Contracts
- **Dependencies**: TASK-086
- **Estimate**: 2 hours
- **Priority**: High
- **Success Criteria**:
  - DTOs and requests under `CarePath.Contracts/Platform/` and `/Organization/` per design; enum mirrors `OrganizationStatus`, `ProvisioningState`, `MembershipStatus` with parity tests
  - `ControlPlaneContractTests` proves no PHI/financial field names and no Domain types in signatures
- **Files**:
  - CREATE: `CarePath.Contracts/Platform/{OrganizationSummaryDto,OrganizationDto,RegisterOrganizationRequest,UpdateOrganizationRequest,SuspendOrganizationRequest,OrganizationDomainDto,AddOrganizationDomainRequest,OrganizationBrandingDetailDto,UpdateOrganizationBrandingRequest}.cs`, `CarePath.Contracts/Organization/{MembershipDto,CreateMembershipRequest,UpdateMembershipRoleRequest,UpdateMembershipStatusRequest}.cs`, `CarePath.Contracts/Enumerations/{OrganizationStatus,ProvisioningState,MembershipStatus}.cs`, `Application.Tests/Contracts/ControlPlaneContractTests.cs`

### TASK-093: PlatformOrganizationService with validators and platform audit
- **Layer**: CarePath.Application
- **Dependencies**: TASK-092, TASK-089
- **Estimate**: 4 hours
- **Priority**: High
- **Success Criteria**:
  - Register/list/get/update/suspend/reactivate/domains/branding per design; reserved-slug and duplicate-slug/host 409s; Pending organizations cannot be reactivated; last Verified domain cannot be removed
  - Every mutation writes a `PlatformAuditEvent`; branding cache invalidated on branding/organization change
  - Validators for all requests; mapper to Contracts
- **Files**:
  - CREATE: `Application/Platform/Services/{IPlatformOrganizationService,PlatformOrganizationService}.cs`, `Application/Platform/Validators/*.cs`, `Application/Platform/Mapping/PlatformContractMapper.cs`, `Application.Tests/Platform/PlatformOrganizationServiceTests.cs`
  - MODIFY: `Application/DependencyInjection.cs`

### TASK-094: Membership admin surface with two-phase create
- **Layer**: CarePath.Application
- **Dependencies**: TASK-092, TASK-089
- **Estimate**: 3 hours
- **Priority**: High
- **Success Criteria**:
  - `ListAsync` paged within the caller's organization; `CreateAsync` links an existing platform identity or provisions a new one, creates the tenant `User` (same id) for the backfilled organization, compensates on tenant failure; duplicate membership 409
  - Membership ids from other organizations yield the PHI-safe not-found shape
- **Files**:
  - CREATE: `Application/Organization/Validators/CreateMembershipRequestValidator.cs`, `Application/Organization/Mapping/MembershipContractMapper.cs`
  - MODIFY: `Application/Organization/Services/OrganizationMembershipService.cs`, `Application.Tests/Organization/OrganizationMembershipServiceTests.cs`

### TASK-095: Platform and membership controllers, reflection tests
- **Layer**: CarePath.WebApi
- **Dependencies**: TASK-093, TASK-094
- **Estimate**: 3 hours
- **Priority**: High
- **Success Criteria**:
  - `PlatformOrganizationsController` (`[Authorize(Policy = "PlatformAdmin")]`) and `OrganizationMembershipsController` (`[Authorize(Roles = "Admin")]`) per endpoint table
  - `Sprint4ControllerContractTests` extended with membership rows and a policy-based theory for platform actions; `AuthorizeOperationFilter` understands policy attributes
  - Controller tests via TestServer: tenant Admin → 403 on platform routes at own host; platform token → 401 at tenant host
- **Files**:
  - CREATE: `WebApi/Controllers/{PlatformOrganizationsController,OrganizationMembershipsController}.cs`, `Application.Tests/WebApi/{PlatformOrganizationsControllerTests,OrganizationMembershipsControllerTests}.cs`
  - MODIFY: `Application.Tests/WebApi/Sprint4ControllerContractTests.cs`, `WebApi/OpenApi/AuthorizeOperationFilter.cs`

---

## Slice 5 — Typed clients and Web

### TASK-096: Typed clients and route alignment tests
- **Layer**: CarePath.Client
- **Dependencies**: TASK-095
- **Estimate**: 2 hours
- **Priority**: High
- **Success Criteria**: three clients over `ApiClientBase`; `Cp04ClientRouteAlignmentTests` asserts every client route matches a controller route; clients registered in `CarePath.Web/Program.cs`
- **Files**:
  - CREATE: `CarePath.Client/Api/{OrganizationBrandingClient,OrganizationMembershipsClient,PlatformOrganizationsClient}.cs`, `Application.Tests/WebApi/Cp04ClientRouteAlignmentTests.cs`
  - MODIFY: `CarePath.Web/Program.cs`

### TASK-097: Login branding and organization-aware auth state
- **Layer**: CarePath.Client.UI, CarePath.Web
- **Dependencies**: TASK-096, TASK-090
- **Estimate**: 3 hours
- **Priority**: High
- **Success Criteria**:
  - `BrandingTokens.CssVariable` maps every `BrandingColorToken`; no literal colors in Razor
  - `Login.razor` shows organization display name and applies tokens; silent neutral fallback on 404/error
  - `TokenAuthenticationStateProvider` exposes organization/membership/kind claims; role claim only when non-null
  - Same-origin API base address when `Api:BaseAddress` is empty
  - bUnit `LoginBrandingTests`; existing Web tests adjusted for nullable `Role`
- **Files**:
  - CREATE: `CarePath.Client.UI/Theming/BrandingTokens.cs`, `CarePath.Web.Tests/LoginBrandingTests.cs`
  - MODIFY: `CarePath.Web/Pages/Login.razor`, `CarePath.Web/Auth/TokenAuthenticationStateProvider.cs`, `CarePath.Web/Program.cs`, `CarePath.Web/wwwroot/appsettings.json`, affected `CarePath.Web.Tests/*.cs`

---

## Slice 6 — Two-organization verification and close-out

### TASK-098: Two-organization integration suite
- **Layer**: Application.Tests (WebApi host)
- **Dependencies**: TASK-095
- **Estimate**: 4 hours
- **Priority**: Critical
- **Success Criteria**: `ControlPlaneTestHost` builds a TestServer with both contexts on InMemory and the real pipeline; tests cover every §5.1 success criterion: distinct tokens per host, cross-host 401 with controller never invoked, no-membership 401 byte-equal to invalid credentials, suspension immediate, refresh rejected cross-host and after deactivation, platform token 401 at tenant host, tenant token 403 at platform route, warm host resolution under 5 ms
- **Files**:
  - CREATE: `Application.Tests/WebApi/{ControlPlaneTestHost,TwoOrganizationIntegrationTests}.cs`
  - MODIFY: `Application.Tests/Application.Tests.csproj` (EF InMemory reference)

### TASK-099: Configuration, documentation, HIPAA check, lessons, board close-out
- **Layer**: Cross-cutting
- **Dependencies**: TASK-098
- **Estimate**: 2 hours
- **Priority**: High
- **Success Criteria**:
  - CLAUDE.md / AGENTS.md: control-plane folder layout, commands, conventions; README and docker-compose comment for `CONTROLPLANE__*` keys
  - `/hipaa-check` and `dotnet-code-reviewer` run on the full CP-04 diff with findings resolved
  - `_specs/lessons.md` entries for anything corrected during implementation
  - `_specs/PROGRESS.md`, `task.md`, `_specs/sprints/sprint-08-tasks.md` status rows updated with evidence
- **Files**:
  - MODIFY: `CLAUDE.md`, `AGENTS.md`, `README.md`, `docker-compose.yml`, `_specs/lessons.md`, `_specs/PROGRESS.md`, `task.md`, `_specs/sprints/sprint-08-tasks.md`

---

## Deferred to CP-05 (explicitly)

- **TASK-100** (reserved): `DropLegacyIdentityTables` forward-only migration for both providers, after a verified HomeLab run of every existing login.
- `User.PlatformUserId` NOT NULL tightening; removal of `User.Role`.
- `SecurityVersion` token claim and per-request validation; logout / session revocation; authentication epoch.
- `Admin` → `OrganizationAdmin` rename; tenant precondition in `Sprint4ObjectAuthorizationService`.

---

## Critical Path

`TASK-079 → 080 → 081 → 082 → 083 → 084 → 085 → 087 → 088 → 089 → 090 → 091 → 092 → 093/094 → 095 → 096 → 097 → 098 → 099`

## Risk Areas

| Risk | Mitigation |
|---|---|
| Identity move breaks existing logins | Verbatim copy incl. hashes/stamps (TASK-088 tests); legacy tables retained (D-CP04-08); login integration test over copied rows |
| Two Identity registrations collide during the transition | `IdentityUserContext` + `AddIdentityCore` (no role store) in TASK-082; legacy `AddIdentity` removed in TASK-091 |
| Host spoofing via `X-Forwarded-Host` | Known-proxy list, `ForwardLimit = 1`, middleware tests (TASK-085) |
| Slug/host enumeration via differing bodies | Shared `UnauthorizedResponseWriter`; byte-equality tests |
| Two DbContexts and transactions | Two-phase membership create with compensation (TASK-094); no distributed transaction |
| Provider migration drift | Both migration sets per task; shape tests read both files |
| `User.Role` drift | Single writer (membership service) + backfill; CP-05 removes the column |

## Progress Tracking

| Task | Status | Notes |
|---|---|---|
| TASK-079 … TASK-099 | Done 2026-10-21 | Evidence per row in `_specs/sprints/sprint-08-tasks.md`; TASK-100 (legacy table drop) deferred to CP-05 after S8-TASK-003 |

## Global Compliance Acceptance

- [ ] Control-plane entities contain no PHI field (reflection test green)
- [ ] No email, host string, or PHI in any log line or platform audit event
- [ ] Unknown-host / suspended / wrong-organization / invalid-credential bodies byte-identical
- [ ] Every `{id}` membership route scoped to the caller's organization
- [ ] Tenant Identity tables not dropped in CP-04
- [ ] Both provider migration sets committed with no uncommitted model changes
- [ ] `dotnet build CarePath.sln` zero warnings; full test suite green; `dotnet-code-reviewer` and `/hipaa-check` findings resolved

## Revision History

| Version | Date | Author | Changes |
|---|---|---|---|
| 1.0 | 2026-10-21 | CarePath Health | Initial breakdown (21 tasks, 6 slices, ~61 h); approved by Tobi via Claude plan review 2026-10-21 |
