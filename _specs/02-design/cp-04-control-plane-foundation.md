# CP-04 — Control Plane Foundation: Design

**Status**: Draft  
**Author**: CarePath Health  
**Created**: 2026-09-08  
**Depends on**: CP-02 (Infrastructure, complete), Sprint 3 auth foundation (complete), ADR 0003 (Accepted)  
**Requirements spec**: [`_specs/01-requirements/cp-04-control-plane-foundation.md`](../01-requirements/cp-04-control-plane-foundation.md) (Approved 2026-09-08)  
**Tasks spec**: [`_specs/03-tasks/cp-04-control-plane-foundation.md`](../03-tasks/cp-04-control-plane-foundation.md) (created after this design is approved)  
**UI source of truth**: `Documentation/Wireframes/carepath-wireframe.html`, SaaS journey tab; dialog pattern in [`ui-design-system.md`](ui-design-system.md)

---

## Executive Summary

Add a second EF Core context, `ControlPlaneDbContext`, that owns organizations, domains, branding, memberships, ASP.NET Identity, sessions, refresh-token families, the authentication epoch, and the platform audit log. Resolve the organization from the request host in middleware before authentication, issue JWTs bound to one organization and one session, and re-validate membership, session, and security versions against the control plane on every protected request. `CarePathDbContext` loses Identity and gains `User.PlatformUserId` and a one-row `TenantDeployment` table. A startup backfill turns the existing deployment into organization number one.

---

## 1. Architecture Overview

### 1.1 High-Level Design

```
Browser (Blazor WASM at {slug}.carepathhealth.com or manage.carepathhealth.com)
   │  Authorization: Bearer <jwt>
   ▼
WebApi pipeline (order is normative)
   1. UseForwardedHeaders            known proxies only
   2. OrganizationResolutionMiddleware   Host → IOrganizationContext   ──┐
   3. UseAuthentication (JwtBearer)  signature, issuer, audience, lifetime │
   4. AuthorizationFreshnessMiddleware  claims vs live control-plane state │ ControlPlaneDbContext
   5. UseAuthorization               role policies                        │ (Azure SQL / PostgreSQL dev)
   6. Controllers                    IdorGuard, services                ──┘
                                          │
                                          ▼
                                  CarePathDbContext  (tenant one; CP-06 makes this per-organization)
```

Steps 2 and 4 are the only new pipeline stages. Both fail closed: an unresolved host short-circuits with the anonymous response; a freshness failure short-circuits with 401 or 503 and never reaches a controller.

### 1.2 Affected Layers

- [x] **CarePath.Domain** — `Entities/Platform/*`, new enumerations, `IControlPlaneUnitOfWork`
- [x] **CarePath.Application** — `Abstractions/Platform/*`, `Platform/Services/*`, validators, `ICurrentUserContext` extension, mappers
- [x] **CarePath.Infrastructure** — `ControlPlane/*` (DbContext, configurations, Identity, stores, resolver, backfill), `Persistence` changes, DI
- [x] **Infrastructure.Migrations.PostgreSql** — control-plane migrations for PostgreSQL
- [x] **WebApi** — two middlewares, `PlatformOrganizationsController`, `OrganizationController`, `MembershipsController`, `AuthController` changes, `Program.cs` startup order
- [x] **CarePath.Contracts / CarePath.Client / CarePath.Client.UI** — organization, membership, branding, auth DTOs; typed clients; `ConfirmDialog`
- [x] **CarePath.Web** — branding on sign-in, service-state pages, shell organization cue, sign-out, platform console pages
- [ ] **CarePath.MauiApp** — not in CP-04

### 1.3 Folder Layout

```
Domain/
├── Entities/Platform/
│   ├── Organization.cs, OrganizationDomain.cs, OrganizationBranding.cs
│   ├── OrganizationMembership.cs, OrganizationDataPlane.cs
│   ├── PlatformSession.cs, PlatformRefreshToken.cs, PlatformSecurityState.cs
│   └── PlatformAuditEvent.cs
├── Entities/Identity/TenantDeployment.cs
├── Enumerations/ OrganizationStatus.cs, OrganizationReadinessState.cs, DomainVerificationStatus.cs,
│                 MembershipStatus.cs, TokenKind.cs, OrganizationSuspensionReason.cs
└── Interfaces/Repositories/IControlPlaneUnitOfWork.cs

Application/
├── Abstractions/Platform/ IOrganizationContext.cs, HostKind.cs, IOrganizationResolver.cs,
│                          IAuthorizationSnapshotReader.cs, AuthorizationSnapshot.cs,
│                          IPlatformAuditLogger.cs, PlatformAuditEntry.cs, IControlPlaneClock.cs
├── Abstractions/Auth/     ICurrentUserContext.cs (extended), JwtTokenRequest.cs (extended)
├── Platform/Services/     PlatformAuthService.cs, OrganizationManagementService.cs,
│                          MembershipService.cs, BrandingService.cs, AuthorizationFreshnessService.cs
├── Platform/Validators/   CreateOrganizationRequestValidator.cs, UpdateBrandingRequestValidator.cs,
│                          SuspendOrganizationRequestValidator.cs, SetMaintenanceRequestValidator.cs,
│                          CreateMembershipRequestValidator.cs, UpdateMembershipRoleRequestValidator.cs
└── Common/Mapping/        PlatformContractMapper.cs

Infrastructure/
├── ControlPlane/
│   ├── ControlPlaneDbContext.cs
│   ├── Identity/PlatformUser.cs
│   ├── Configurations/ (one per entity)
│   ├── Migrations/  (SQL Server; [DbContext(typeof(ControlPlaneDbContext))])
│   ├── Repositories/ControlPlaneUnitOfWork.cs
│   ├── HostOrganizationResolver.cs
│   ├── AuthorizationSnapshotReader.cs
│   ├── PlatformAuditLogger.cs
│   ├── ControlPlaneCircuitBreaker.cs
│   └── Bootstrap/ ControlPlaneBackfill.cs, PlatformAdminBootstrap.cs, LegacyIdentityUser.cs
├── Auth/ IdentityService.cs (moved to ControlPlaneDbContext), JwtTokenService.cs (claims), SessionStore.cs
└── Persistence/ CarePathDbContext.cs (no Identity), Configurations/Identity/TenantDeploymentConfiguration.cs

Infrastructure.Migrations.PostgreSql/
└── ControlPlane/Migrations/

WebApi/
├── Middleware/ OrganizationResolutionMiddleware.cs, AuthorizationFreshnessMiddleware.cs
├── Security/ HttpCurrentUserContext.cs (extended), HttpOrganizationContext.cs, CarePathClaimTypes.cs
└── Controllers/ AuthController.cs, OrganizationController.cs, MembershipsController.cs, PlatformOrganizationsController.cs
```

---

## 2. Domain Layer Design

All entities inherit `BaseEntity` (Guid key, audit fields, `IsDeleted`). `PlatformAuditEvent` is the exception to soft delete: it has no delete path at all. Explicit `using CarePath.Domain.Entities.Common;` and `using CarePath.Domain.Enumerations;` on every file.

### 2.1 Entities

```csharp
// Domain/Entities/Platform/Organization.cs
public class Organization : BaseEntity
{
    public string LegalName { get; set; } = string.Empty;          // 200
    public string DisplayName { get; set; } = string.Empty;        // 100
    public string Slug { get; init; } = string.Empty;              // 40, immutable, unique
    public OrganizationStatus Status { get; set; } = OrganizationStatus.Pending;
    public OrganizationReadinessState ReadinessState { get; set; } = OrganizationReadinessState.Provisioning;
    public string DefaultTimeZone { get; set; } = "America/New_York"; // IANA, 64
    public string DataRegion { get; set; } = "US East";            // 50
    public string? MaintenanceMessage { get; set; }                // 200, member-facing
    public DateTime? MaintenanceExpectedEndUtc { get; set; }
    public DateTime? SuspendedAtUtc { get; set; }
    public OrganizationSuspensionReason? SuspensionReason { get; set; }

    public ICollection<OrganizationDomain> Domains { get; set; } = new List<OrganizationDomain>();
    public ICollection<OrganizationMembership> Memberships { get; set; } = new List<OrganizationMembership>();
    public OrganizationBranding? Branding { get; set; }
    public OrganizationDataPlane? DataPlane { get; set; }

    /// <summary>Members may sign in and use the workspace.</summary>
    public bool IsServing => Status == OrganizationStatus.Active && ReadinessState == OrganizationReadinessState.Ready;
}
```

```csharp
// Domain/Entities/Platform/OrganizationDomain.cs
public class OrganizationDomain : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public string HostName { get; set; } = string.Empty;           // 253, normalized, unique
    public bool IsPrimary { get; set; }
    public DomainVerificationStatus VerificationStatus { get; set; } = DomainVerificationStatus.Verified;
}

// Domain/Entities/Platform/OrganizationBranding.cs   (one per organization; Id is the PK, OrganizationId is a unique FK)
public class OrganizationBranding : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public string Monogram { get; set; } = string.Empty;           // 3
    public string? LogoStorageKey { get; set; }                    // 200, private object key
    public string ThemeToken { get; set; } = "harbor";             // 20, allowlist
    public string? SupportEmail { get; set; }                      // 256
    public string? SupportPhone { get; set; }                      // 20
}

// Domain/Entities/Platform/OrganizationMembership.cs
public class OrganizationMembership : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public Guid PlatformUserId { get; set; }                       // control-plane Identity user
    public Guid? TenantUserId { get; set; }                        // tenant User.Id, no FK (other database)
    public UserRole OrganizationRole { get; set; }                 // existing tenant role enum
    public MembershipStatus Status { get; set; } = MembershipStatus.Provisioning;   // Active only after TenantUserId is linked
    public int SecurityVersion { get; set; } = 1;                  // bump on role/status change
    public DateTime? ActivatedAtUtc { get; set; }
    public DateTime? DeactivatedAtUtc { get; set; }

    public bool IsActive => Status == MembershipStatus.Active && !IsDeleted;
}

// Domain/Entities/Platform/OrganizationDataPlane.cs   (one per organization; Id is the PK, OrganizationId is a unique FK)
public class OrganizationDataPlane : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public string Provider { get; set; } = "SqlServer";            // 20
    public string SecretReference { get; set; } = string.Empty;    // 200, never a connection string
    public Guid DatabaseDeploymentId { get; set; }
    public int LocationRevision { get; set; } = 1;
    public string SchemaVersion { get; set; } = string.Empty;      // 64, last verified migration id
    public DateTime RegisteredAtUtc { get; set; }
}

// Domain/Entities/Platform/PlatformSession.cs
public class PlatformSession : BaseEntity
{
    public Guid PlatformUserId { get; set; }
    public Guid? OrganizationId { get; set; }                      // null for platform sessions
    public Guid? MembershipId { get; set; }
    public TokenKind TokenKind { get; set; }
    public DateTime LastRefreshedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? RevocationReason { get; set; }                  // 50, code not prose

    public bool IsRevoked => RevokedAtUtc.HasValue;
    public ICollection<PlatformRefreshToken> RefreshTokens { get; set; } = new List<PlatformRefreshToken>();
}

// Domain/Entities/Platform/PlatformRefreshToken.cs
public class PlatformRefreshToken : BaseEntity
{
    public Guid SessionId { get; set; }
    public PlatformSession Session { get; set; } = null!;
    public Guid FamilyId { get; set; }
    public string TokenHash { get; set; } = string.Empty;          // 128, SHA-256 hex
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }

    public bool IsUsable => UsedAtUtc is null && RevokedAtUtc is null && ExpiresAtUtc > DateTime.UtcNow;
}

// Domain/Entities/Platform/PlatformSecurityState.cs   (single row, fixed Id)
public class PlatformSecurityState : BaseEntity
{
    public static readonly Guid SingletonId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public long AuthenticationEpoch { get; set; } = 1;
}

// Domain/Entities/Platform/PlatformAuditEvent.cs   (append-only)
public class PlatformAuditEvent : BaseEntity
{
    public Guid? ActorPlatformUserId { get; set; }
    public Guid? OrganizationId { get; set; }
    public string Action { get; set; } = string.Empty;             // 64, e.g. "OrganizationSuspended"
    public string EntityType { get; set; } = string.Empty;         // 64
    public Guid? EntityId { get; set; }
    public string Outcome { get; set; } = "Success";               // 20: Success | Denied | Unavailable
    public string? ReasonCode { get; set; }                        // 50
    public string? CorrelationId { get; set; }                     // 64
    public string? SubjectHash { get; set; }                       // 64, SHA-256 of email or host; never plaintext
    public DateTime OccurredAtUtc { get; set; }
}

// Domain/Entities/Identity/TenantDeployment.cs   (tenant database, single row)
public class TenantDeployment : BaseEntity
{
    public Guid OrganizationId { get; init; }
    public Guid DatabaseDeploymentId { get; init; }
    public string SchemaVersion { get; set; } = string.Empty;
}
```

`User` gains `public Guid? PlatformUserId { get; set; }` (nullable in the model so the migration can add the column before backfill fills it; the backfill then asserts no nulls remain). `User.Role` stays and is documented as a mirror written only by `MembershipService` and the backfill.

### 2.2 Enumerations

```csharp
public enum OrganizationStatus { Pending = 0, Active = 1, Suspended = 2, Offboarded = 3 }
public enum OrganizationReadinessState { Provisioning = 0, Ready = 1, Maintenance = 2, Recovering = 3, Failed = 4 }
public enum DomainVerificationStatus { Pending = 0, Verified = 1 }
public enum MembershipStatus { Provisioning = 0, Active = 1, Inactive = 2 }
public enum TokenKind { Tenant = 0, Platform = 1 }
public enum OrganizationSuspensionReason { CustomerRequest = 0, AgreementEnded = 1, SecurityIncident = 2, ComplianceHold = 3, Other = 4 }
```

`Recovering` and `Failed` exist so CP-06 does not change the enum; CP-04 never sets them.

### 2.3 Domain Interfaces

```csharp
// Domain/Interfaces/Repositories/IControlPlaneUnitOfWork.cs
public interface IControlPlaneUnitOfWork
{
    IRepository<Organization> Organizations { get; }
    IRepository<OrganizationDomain> Domains { get; }
    IRepository<OrganizationBranding> Branding { get; }
    IRepository<OrganizationMembership> Memberships { get; }
    IRepository<OrganizationDataPlane> DataPlanes { get; }
    IRepository<PlatformSession> Sessions { get; }
    IRepository<PlatformRefreshToken> RefreshTokens { get; }
    IRepository<PlatformSecurityState> SecurityState { get; }
    IRepository<PlatformAuditEvent> AuditEvents { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<TResult> ExecuteInTransactionAsync<TResult>(IsolationLevel isolationLevel, Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken = default);
}
```

`Repository<T>` is generalized to take a `DbContext` rather than `CarePathDbContext` so the same implementation serves both contexts; `ControlPlaneUnitOfWork` mirrors `UnitOfWork`.

---

## 3. Application Layer Design

### 3.1 Request and Response Contracts (CarePath.Contracts)

```csharp
// Contracts/Auth/AuthTokenResponse.cs  (extended, existing fields kept)
public class AuthTokenResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public DateTime ExpiresAtUtc { get; init; }
    public string RefreshToken { get; init; } = string.Empty;
    public UserRole Role { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public TokenKind TokenKind { get; init; }                 // Contracts enum mirror
    public Guid? OrganizationId { get; init; }
    public string? OrganizationSlug { get; init; }
    public string? OrganizationDisplayName { get; init; }
    public Guid? MembershipId { get; init; }
}

// Contracts/Platform/
public sealed record OrganizationBrandingDto(string DisplayName, string Monogram, string? LogoUrl, string ThemeToken,
    string? SupportEmail, string? SupportPhone, ServiceState ServiceState, string? MaintenanceMessage);
public enum ServiceState { Available = 0, Maintenance = 1, Recovering = 2 }

public sealed record OrganizationSummaryDto(Guid Id, string DisplayName, string Slug, string PrimaryHost,
    OrganizationStatus Status, OrganizationReadinessState ReadinessState, int ActiveMembers, DateTime CreatedAtUtc);
public sealed record OrganizationDetailDto(Guid Id, string LegalName, string DisplayName, string Slug, string DefaultTimeZone,
    string DataRegion, OrganizationStatus Status, OrganizationReadinessState ReadinessState, IReadOnlyList<OrganizationDomainDto> Domains,
    string? FirstAdministratorEmail, string? LastActionSummary, DateTime CreatedAtUtc);
public sealed record OrganizationDomainDto(Guid Id, string HostName, bool IsPrimary, DomainVerificationStatus VerificationStatus);

public sealed class CreateOrganizationRequest { LegalName, DisplayName, Slug, DefaultTimeZone, DataRegion, FirstAdministratorEmail }
public sealed class UpdateOrganizationRequest { LegalName, DisplayName, DefaultTimeZone }
public sealed class SuspendOrganizationRequest { OrganizationSuspensionReason Reason; string Details; string ConfirmSlug }
public sealed class ReactivateOrganizationRequest { string Reason }
public sealed class SetMaintenanceRequest { int? ExpectedMinutes; string Message; string Reason }
public sealed class EndMaintenanceRequest { string Reason }
public sealed class UpdateBrandingRequest { DisplayName, Monogram, ThemeToken, SupportEmail, SupportPhone }

public sealed record MembershipDto(Guid Id, Guid PlatformUserId, string Email, string DisplayName, UserRole Role,
    MembershipStatus Status, DateTime CreatedAtUtc);
public sealed class CreateMembershipRequest { Email, FirstName, LastName, PhoneNumber, UserRole Role }
public sealed class UpdateMembershipRoleRequest { UserRole Role }
public sealed class UpdateMembershipStatusRequest { MembershipStatus Status; string Reason }
```

Enum mirrors (`OrganizationStatus`, `OrganizationReadinessState`, `MembershipStatus`, `TokenKind`, `OrganizationSuspensionReason`, `DomainVerificationStatus`) go in `Contracts/Enumerations` with the existing parity test extended.

### 3.2 Abstractions

```csharp
// Application/Abstractions/Platform/IOrganizationContext.cs   (scoped, set once by middleware)
public enum HostKind { Unknown = 0, Public = 1, Platform = 2, Tenant = 3 }

public interface IOrganizationContext
{
    HostKind HostKind { get; }
    string NormalizedHost { get; }
    Guid? OrganizationId { get; }
    string? Slug { get; }
    OrganizationStatus? Status { get; }
    OrganizationReadinessState? ReadinessState { get; }
    bool IsServing { get; }
}

// Application/Abstractions/Platform/IOrganizationResolver.cs
public sealed record ResolvedOrganization(Guid Id, string Slug, string DisplayName, OrganizationStatus Status,
    OrganizationReadinessState ReadinessState, string? MaintenanceMessage);
public interface IOrganizationResolver
{
    Task<ResolvedOrganization?> ResolveByHostAsync(string normalizedHost, CancellationToken cancellationToken);
}

// Application/Abstractions/Auth/ICurrentUserContext.cs   (extended)
public interface ICurrentUserContext
{
    Guid? UserId { get; }
    string? UserName { get; }
    bool IsAuthenticated { get; }
    IReadOnlySet<string> Roles { get; }
    string? CorrelationId { get; }
    Guid? OrganizationId { get; }      // NEW
    Guid? MembershipId { get; }        // NEW
    Guid? SessionId { get; }           // NEW
    TokenKind? TokenKind { get; }      // NEW
}

// Application/Abstractions/Platform/IAuthorizationSnapshotReader.cs   (the hot path)
public sealed record AuthorizationSnapshot(
    bool UserEnabled, int UserSecurityVersion,
    bool SessionRevoked, TokenKind SessionKind, Guid? SessionOrganizationId,
    bool MembershipActive, int MembershipSecurityVersion, UserRole? MembershipRole,
    OrganizationStatus? OrganizationStatus, OrganizationReadinessState? ReadinessState,
    long AuthenticationEpoch, bool IsPlatformAdmin);

public interface IAuthorizationSnapshotReader
{
    /// <summary>One query. Returns null when the session does not exist.</summary>
    Task<AuthorizationSnapshot?> ReadAsync(Guid sessionId, CancellationToken cancellationToken);
}

// Application/Abstractions/Platform/IPlatformAuditLogger.cs
public sealed record PlatformAuditEntry(string Action, string EntityType, Guid? EntityId, Guid? OrganizationId,
    string Outcome = "Success", string? ReasonCode = null, string? SubjectHash = null);
public interface IPlatformAuditLogger
{
    Task LogAsync(PlatformAuditEntry entry, CancellationToken cancellationToken = default);
}
```

`JwtTokenRequest` is extended to `(Guid UserId, string Email, string Role, TokenKind Kind, Guid SessionId, Guid? OrganizationId, Guid? MembershipId, int? MembershipSecurityVersion, int UserSecurityVersion, long AuthEpoch, string? CorrelationId)`.

### 3.3 Claims

```csharp
// WebApi/Security/CarePathClaimTypes.cs  (also referenced from Infrastructure JwtTokenService via constants in Application)
public static class CarePathClaimTypes
{
    public const string OrganizationId = "organization_id";
    public const string MembershipId = "membership_id";
    public const string SessionId = "session_id";
    public const string MembershipSecurityVersion = "msv";
    public const string UserSecurityVersion = "usv";
    public const string AuthEpoch = "auth_epoch";
    public const string TokenKind = "token_kind";
}
```

Tokens keep `sub`, `email`, `jti`, `ClaimTypes.NameIdentifier`, `ClaimTypes.Email`, exactly one `ClaimTypes.Role`, plus the seven above. Platform tokens omit `organization_id`, `membership_id`, and `msv`.

### 3.4 Services

**AuthorizationFreshnessService** (Application) — pure decision logic, unit-testable:

```csharp
public enum FreshnessOutcome { Allow, Reject, Unavailable }
public sealed record FreshnessDecision(FreshnessOutcome Outcome, string ReasonCode);

public sealed class AuthorizationFreshnessService
{
    public FreshnessDecision Evaluate(TokenClaims token, AuthorizationSnapshot? snapshot, IOrganizationContext host)
    {
        if (snapshot is null)                                   return Reject("session.unknown");
        if (snapshot.SessionRevoked)                            return Reject("session.revoked");
        if (snapshot.AuthenticationEpoch != token.AuthEpoch)    return Reject("epoch.stale");
        if (!snapshot.UserEnabled)                              return Reject("user.disabled");
        if (snapshot.UserSecurityVersion != token.UserSecurityVersion) return Reject("user.version");
        if (snapshot.SessionKind != token.Kind)                 return Reject("token.kind");

        if (token.Kind == TokenKind.Platform)
        {
            if (host.HostKind != HostKind.Platform)             return Reject("host.mismatch");
            if (!snapshot.IsPlatformAdmin)                      return Reject("platform.role");
            return Allow();
        }

        if (host.HostKind != HostKind.Tenant)                   return Reject("host.mismatch");
        if (host.OrganizationId != token.OrganizationId)        return Reject("organization.mismatch");
        if (snapshot.SessionOrganizationId != token.OrganizationId) return Reject("session.organization");
        if (snapshot.OrganizationStatus != OrganizationStatus.Active) return Reject("organization.inactive");
        if (!snapshot.MembershipActive)                         return Reject("membership.inactive");   // Provisioning and Inactive both land here
        if (snapshot.MembershipSecurityVersion != token.MembershipSecurityVersion) return Reject("membership.version");
        if (snapshot.MembershipRole?.ToString() != token.Role)  return Reject("membership.role");
        if (snapshot.ReadinessState != OrganizationReadinessState.Ready) return Unavailable("organization.notready");
        return Allow();
    }
}
```

Reject → 401 with the invalid-credentials problem body; Unavailable → 503 with the PHI-safe body; both write a `PlatformAuditEvent` with the reason code and no subject data. A snapshot read that throws or exceeds the timeout maps to `Unavailable("controlplane.unavailable")`.

**PlatformAuthService** (Application) — `LoginAsync`, `RefreshAsync`, `LogoutAsync`:

```
LoginAsync(email, password, host, correlationId)
  1. host.HostKind must be Tenant with IsServing, or Platform         → else 401 (Tenant not serving → 503)
  2. IIdentityService.ValidateCredentialsAsync(email, password)         → 401 on failure (lockout preserved)
  3. Tenant: find Active membership (organizationId, platformUserId)    → 401 "membership.none"
     Platform: user must be PlatformAdmin                               → 401 "platform.role"
  4. Read PlatformSecurityState.AuthenticationEpoch
  5. Create PlatformSession(kind, orgId?, membershipId?) + PlatformRefreshToken(new FamilyId)
  6. IJwtTokenService.CreateTokenAsync(JwtTokenRequest{...versions, epoch, sessionId})
  7. Audit "LoginSucceeded" (org, session id) / "LoginDenied" (reason code, SubjectHash = sha256(email))
  8. Return AuthTokenResponse

RefreshAsync(refreshToken, host)
  1. Hash; load PlatformRefreshToken with Session
  2. If token already used or revoked → revoke whole family and session; audit "RefreshReplayDetected"; 401
  3. Read snapshot for session; Evaluate(...) as above with the token's stored claims → 401/503
  4. Mark token used; issue new token in same family; re-issue JWT with current versions
  Steps 1 to 4 run in one Serializable transaction so concurrent replay cannot double-rotate.

LogoutAsync(sessionId)
  Revoke session ("logout") and all tokens in its families; audit "SessionRevoked".
```

**OrganizationManagementService** (Application, PlatformAdmin only) — `RegisterAsync`, `UpdateAsync`, `SuspendAsync`, `ReactivateAsync`, `SetMaintenanceAsync`, `EndMaintenanceAsync`, `ListAsync`, `GetAsync`. Register creates the organization (Pending/Provisioning), the primary verified domain `{slug}.{ControlPlane:BaseDomain}`, and the first Admin membership via `MembershipService.CreateFirstAdminAsync`, which ensures the platform identity and leaves the membership in Provisioning with no tenant profile. Nothing touches a tenant database at registration; CP-06 completes the membership when it provisions the database. Suspend verifies `ConfirmSlug == Slug`, sets status and reason, revokes every session whose `OrganizationId` matches, bumps every membership's `SecurityVersion`, and audits. Set maintenance writes `MaintenanceMessage` and `MaintenanceExpectedEndUtc` and does not touch sessions.

**MembershipService** (Application, Admin within own organization or PlatformAdmin for the first admin). Operations span two databases with no distributed transaction, so the intermediate state is designed to be safe: a membership is never Active without a linked tenant profile.

```
Lifecycle
  Provisioning ──(tenant profile created and TenantUserId linked)──▶ Active ◀──▶ Inactive
  Only Active memberships can sign in or pass the freshness check (§3.4 treats Provisioning as "membership.inactive").

CreateAsync(orgId, request)             — organization must be Ready (has a data plane)
  1. Validate; find PlatformUser by email or provision one (IIdentityProvisioningService, temporary-password path unchanged)
  2. Assert no membership (orgId, platformUserId)                        → 409 "membership.exists"
  3. Control plane: add membership { Status = Provisioning, TenantUserId = null, SecurityVersion 1 }; SaveChanges; audit "MembershipProvisioning"
  4. Tenant: create domain User { PlatformUserId, Role = request.Role (mirror), IsActive = true }; SaveChanges
  5. Control plane: set TenantUserId, Status = Active, ActivatedAtUtc; SaveChanges; audit "MembershipActivated"
  Failure after 3: membership stays Provisioning (cannot sign in). A retry of CreateAsync for the same email finds the
  Provisioning membership, looks for an existing tenant User by PlatformUserId, and resumes at step 4 or 5.
  No compensating delete is needed because Provisioning is already a safe state.

CreateFirstAdminAsync(orgId, email)     — called by OrganizationManagementService.RegisterAsync; organization is NOT Ready
  Steps 1 to 3 only. The membership stays Provisioning with TenantUserId = null until CP-06 provisioning creates the
  tenant database, runs steps 4 and 5, and then sets the organization Ready. Until then the administrator cannot sign in,
  which matches the wireframe readiness screen ("Administrator invitation: Waiting").

ChangeRoleAsync / SetStatusAsync         — Active or Inactive memberships only; Provisioning returns 409 "membership.provisioning"
  Serializable transaction on the control plane: guard "last active admin" per organization,
  apply change, SecurityVersion++, revoke sessions for that membership when deactivating,
  mirror Role/IsActive onto the tenant User, audit.
```

The backfill (§4.6) is the one path that creates memberships directly as Active, because the tenant profiles already exist and are linked in the same step.

**BrandingService** — anonymous `GetForHostAsync(host)` returning `OrganizationBrandingDto` with `ServiceState` derived from `ReadinessState` (Maintenance → Maintenance, Recovering → Recovering, else Available); 404 for Unknown, Public, Suspended, or Pending. `UpdateAsync` for Admin, theme token validated against the allowlist `harbor | coastal | meadow | sunrise`.

### 3.5 Validators (FluentValidation)

| Validator | Rules |
|---|---|
| `CreateOrganizationRequestValidator` | LegalName 1–200; DisplayName 1–100; Slug matches `^[a-z0-9]([a-z0-9-]{1,38}[a-z0-9])?$` and not in `ReservedSlugs` (`manage account api www login admin platform status`); DefaultTimeZone in `TimeZoneInfo.TryFindSystemTimeZoneById`; DataRegion in configured list; FirstAdministratorEmail valid |
| `SuspendOrganizationRequestValidator` | Reason enum defined; Details 1–300; ConfirmSlug not empty (equality checked in service so the message never reveals the slug) |
| `SetMaintenanceRequestValidator` | Message 1–200; Reason 1–120; ExpectedMinutes null or 5–1440 |
| `UpdateBrandingRequestValidator` | Monogram 1–3 letters; ThemeToken in allowlist; SupportEmail valid; SupportPhone 7–20 |
| `CreateMembershipRequestValidator` | Email valid; names 1–100; phone; Role in the six tenant roles (never PlatformAdmin) |
| `UpdateMembershipRoleRequestValidator` | Role in the six tenant roles |

### 3.6 Mapping

`PlatformContractMapper` is a manual mapper like the existing `IdentityContractMapper`. Reflection guard tests extend the existing DTO PHI checks: no DTO in `Contracts/Platform` exposes `TenantUserId` of a Client-role membership to another organization, and `MembershipDto` is only served to the membership's own organization.

---

## 4. Infrastructure Layer Design

### 4.1 ControlPlaneDbContext

```csharp
// Infrastructure/ControlPlane/ControlPlaneDbContext.cs
public class ControlPlaneDbContext : IdentityDbContext<PlatformUser, IdentityRole<Guid>, Guid>
{
    // DbSets: Organizations, OrganizationDomains, OrganizationBrandings, OrganizationMemberships,
    //         OrganizationDataPlanes, PlatformSessions, PlatformRefreshTokens, PlatformSecurityStates, PlatformAuditEvents
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ControlPlaneDbContext).Assembly,
            t => t.Namespace!.StartsWith("CarePath.Infrastructure.ControlPlane.Configurations"));
        ApplyBaseEntityConventions(builder);   // same soft-delete filter + UTC converters as CarePathDbContext, shared helper
        builder.Entity<PlatformAuditEvent>().HasQueryFilter(null);   // audit rows are never soft-deleted or filtered
    }
}
```

`AuditableEntityInterceptor` is registered on both contexts. The shared convention helper moves to `Infrastructure/Persistence/BaseEntityConventions.cs`.

```csharp
// Infrastructure/ControlPlane/Identity/PlatformUser.cs
public class PlatformUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public int SecurityVersion { get; set; } = 1;
    public bool IsPlatformAdmin { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime? LastLoginAtUtc { get; set; }
}
```

`RefreshTokenHash` and `RefreshTokenExpiresAtUtc` do not carry over; refresh tokens live in `PlatformRefreshToken`. ASP.NET Identity roles in the control plane hold only `PlatformAdmin`; tenant roles are not Identity roles anymore.

### 4.2 Configurations (Fluent API)

| Entity | Table | Keys and indexes | Notes |
|---|---|---|---|
| Organization | `Organizations` | unique `Slug`; index `Status`; index `ReadinessState` | string lengths per §2.1; enums stored as int |
| OrganizationDomain | `OrganizationDomains` | unique `HostName`; index `(OrganizationId, IsPrimary)` | FK Restrict |
| OrganizationBranding | `OrganizationBrandings` | PK `Id` (BaseEntity); unique `OrganizationId` | one-to-one via unique FK, `HasOne(...).WithOne(o => o.Branding).HasForeignKey<OrganizationBranding>(b => b.OrganizationId)`; no shared primary keys anywhere in CP-04 |
| OrganizationMembership | `OrganizationMemberships` | unique `(OrganizationId, PlatformUserId)`; index `(PlatformUserId, Status)`; index `(OrganizationId, Status, OrganizationRole)` for the last-admin check | FK to Organization Restrict; FK to `AspNetUsers` Restrict |
| OrganizationDataPlane | `OrganizationDataPlanes` | PK `Id` (BaseEntity); unique `OrganizationId`; unique `DatabaseDeploymentId` | one-to-one via unique FK; `SecretReference` never logged |
| PlatformSession | `PlatformSessions` | index `(PlatformUserId, RevokedAtUtc)`; index `(OrganizationId, RevokedAtUtc)`; index `(MembershipId, RevokedAtUtc)` | mass revocation paths |
| PlatformRefreshToken | `PlatformRefreshTokens` | unique `TokenHash`; index `FamilyId`; index `SessionId` | |
| PlatformSecurityState | `PlatformSecurityState` | PK fixed Guid; `RowVersion` concurrency token | seeded with epoch 1 in migration |
| PlatformAuditEvent | `PlatformAuditEvents` | index `(OrganizationId, OccurredAtUtc)`; index `(Action, OccurredAtUtc)` | no update path; `IsDeleted` column exists via BaseEntity but is never set |
| TenantDeployment (tenant DB) | `TenantDeployment` | PK `Id` (BaseEntity); unique `OrganizationId` | single row; runtime principal SELECT only (Azure SQL permission task) |

Concurrency: `OrganizationMembership.SecurityVersion` and `PlatformUser.SecurityVersion` updates use `RowVersion` tokens so two concurrent role changes cannot both succeed with the same version.

### 4.3 Provider-Specific SQL

`SqlDialect` already abstracts filtered-index syntax. New uses: filtered unique index on `PlatformRefreshToken.TokenHash`, and the `PlatformSecurityState` seed. Both control-plane migration sets are generated in the same task and reviewed together.

### 4.4 Migrations

Two contexts, two providers, four migration sets:

| Context | Provider | Location | Command |
|---|---|---|---|
| ControlPlaneDbContext | SQL Server | `Infrastructure/ControlPlane/Migrations` | `dotnet ef migrations add InitialControlPlane --context ControlPlaneDbContext --project Infrastructure --startup-project WebApi --output-dir ControlPlane/Migrations` |
| ControlPlaneDbContext | PostgreSQL | `Infrastructure.Migrations.PostgreSql/ControlPlane` | same with `--project Infrastructure.Migrations.PostgreSql --output-dir ControlPlane` and `Database:Provider=PostgreSql` |
| CarePathDbContext | SQL Server | `Infrastructure/Migrations` | `dotnet ef migrations add RemoveIdentityAddTenantDeployment --context CarePathDbContext ...` |
| CarePathDbContext | PostgreSQL | `Infrastructure.Migrations.PostgreSql/Migrations` | same |

The tenant migration `RemoveIdentityAddTenantDeployment`:

- **Up**: add `Users.PlatformUserId` (nullable uniqueidentifier, unique filtered index where not null); create `TenantDeployment`; **does not drop** `AspNet*` tables. The Identity entity types are removed from the model, so EF would generate `DropTable`; those operations are deleted from the scaffolded migration by hand and the snapshot is left without them (a lesson already recorded: PHI-adjacent migrations are forward-only).
- **Down**: drop `TenantDeployment` and `PlatformUserId` only.
- A later cleanup migration (post-CP-06, after verification) drops the legacy Identity tables.

The control-plane `InitialControlPlane` migration seeds `PlatformSecurityState` (epoch 1) and the `PlatformAdmin` Identity role.

### 4.5 Reading Legacy Identity Rows During Backfill

```csharp
// Infrastructure/ControlPlane/Bootstrap/LegacyIdentityUser.cs   (keyless, excluded from migrations)
[Keyless] public sealed class LegacyIdentityUser { Id, DomainUserId, UserName, NormalizedUserName, Email, NormalizedEmail,
    EmailConfirmed, PasswordHash, SecurityStamp, ConcurrencyStamp, PhoneNumber, PhoneNumberConfirmed, LockoutEnd, LockoutEnabled, AccessFailedCount }
// CarePathDbContext.OnModelCreating:
builder.Entity<LegacyIdentityUser>().HasNoKey().ToTable("AspNetUsers", t => t.ExcludeFromMigrations());
```

The backfill reads `context.Set<LegacyIdentityUser>()` and legacy `AspNetUserRoles` the same way; both mappings are removed with the cleanup migration.

### 4.6 Backfill and Bootstrap

```
ControlPlaneBackfill.RunAsync   — idempotent saga across two databases; there is NO transaction spanning both.
Each step is guarded by a check of existing state, so a crash between steps is recovered by simply running again.
The organization is created NOT Ready and is set Ready only in the final step, so a half-finished backfill can never
serve traffic. Runs at startup after both migrations whenever ControlPlane:DefaultOrganization is bound.

  Guard 0  Require config: Name, DisplayName, Slug, DefaultTimeZone, HostNames[] (fail closed naming the key).
  Step A   [control plane, tx A]  Find Organization by Slug.
           - none      → create Organization { Status Active, ReadinessState Provisioning }, Domains, Branding,
                         DataPlane { DatabaseDeploymentId = new Guid, LocationRevision 1, SchemaVersion = last applied tenant migration }.
           - exists    → reuse; if ReadinessState == Ready → verify TenantDeployment exists in the tenant DB and return (no-op path).
           Commit A.
  Step B   [tenant, tx B]  Find TenantDeployment.
           - none      → insert { OrganizationId, DatabaseDeploymentId = DataPlane.DatabaseDeploymentId, SchemaVersion }.
           - exists with a different DatabaseDeploymentId → STOP with an operator error; this database already belongs to
             another registration and must not be re-pointed silently.
           Add Users.PlatformUserId for every domain User that has a legacy AspNetUsers row and PlatformUserId == null
           (PlatformUserId := legacy Id). Commit B.
  Step C   [control plane, tx C]  For each LegacyIdentityUser (read via the keyless mapping on the tenant context):
           - PlatformUser with that Id exists → skip; else insert with identical Id, UserName, NormalizedUserName, Email,
             NormalizedEmail, PasswordHash, SecurityStamp, ConcurrencyStamp, lockout fields; names from the domain User.
           For each domain User with IsActive && !IsDeleted:
           - Membership (OrganizationId, PlatformUserId) exists → skip; else insert { TenantUserId = User.Id,
             OrganizationRole = User.Role, Status Active, SecurityVersion 1 }  (Active is safe here: the profile already exists).
           Commit C.
  Step D   [control plane, tx D]  Re-read counts; set Organization.ReadinessState = Ready; audit "OrganizationBackfilled"
           with ReasonCode "users=42;memberships=40". Commit D.

  Failure matrix
    A ok, B fails   → restart: A finds the organization (Provisioning), B retries. Nothing served meanwhile.
    B ok, C fails   → restart: A reuses, B finds TenantDeployment and skips, C resumes per-row (skips existing rows).
    C ok, D fails   → restart: same as above; D sets Ready. Memberships exist but the organization is not Ready, so no login.
    Any step partially committed is impossible within a single database because each step is one transaction.
```

PlatformAdminBootstrap.RunAsync
  If no PlatformUser has IsPlatformAdmin: require ControlPlane:PlatformAdmin:Email and :Password (user secrets / env, never source);
  create PlatformUser with IsPlatformAdmin = true and Identity role PlatformAdmin; audit "PlatformAdminBootstrapped".
  Mirrors CarePathDbContextSeed's fail-closed handling of SeedData:DefaultPassword.
```

### 4.7 Host Resolution and Snapshot Reader

```csharp
// Infrastructure/ControlPlane/HostOrganizationResolver.cs
public async Task<ResolvedOrganization?> ResolveByHostAsync(string host, CancellationToken ct) =>
    await _db.OrganizationDomains
        .Where(d => d.HostName == host && d.VerificationStatus == DomainVerificationStatus.Verified)
        .Select(d => new ResolvedOrganization(d.OrganizationId, d.Organization.Slug, d.Organization.DisplayName,
            d.Organization.Status, d.Organization.ReadinessState, d.Organization.MaintenanceMessage))
        .FirstOrDefaultAsync(ct);

// Infrastructure/ControlPlane/AuthorizationSnapshotReader.cs  — one round trip
public async Task<AuthorizationSnapshot?> ReadAsync(Guid sessionId, CancellationToken ct) =>
    await (from s in _db.PlatformSessions.IgnoreQueryFilters()
           join u in _db.Users on s.PlatformUserId equals u.Id
           join m in _db.OrganizationMemberships.IgnoreQueryFilters() on s.MembershipId equals m.Id into ms
           from m in ms.DefaultIfEmpty()
           join o in _db.Organizations on s.OrganizationId equals o.Id into os
           from o in os.DefaultIfEmpty()
           where s.Id == sessionId
           select new AuthorizationSnapshot(u.IsEnabled, u.SecurityVersion, s.RevokedAtUtc != null, s.TokenKind, s.OrganizationId,
               m != null && m.Status == MembershipStatus.Active && !m.IsDeleted, m != null ? m.SecurityVersion : 0,
               m != null ? m.OrganizationRole : null, o != null ? o.Status : null, o != null ? o.ReadinessState : null,
               _db.PlatformSecurityStates.Select(x => x.AuthenticationEpoch).First(), u.IsPlatformAdmin))
        .FirstOrDefaultAsync(ct);
```

`IgnoreQueryFilters` is deliberate here: a soft-deleted membership must be *seen* so it can be rejected, not silently treated as absent. Both calls run under a linked `CancellationTokenSource` with `ControlPlane:QueryTimeoutMs` (default 1500) and pass through `ControlPlaneCircuitBreaker` (opens after `ControlPlane:CircuitBreakerFailures` consecutive failures, default 5, half-open after 10 s). While open, the resolver and reader throw `ControlPlaneUnavailableException` immediately.

### 4.8 IdentityService and Sessions

`IdentityService` moves to `ControlPlaneDbContext` and `PlatformUser`. `ValidateCredentialsAsync` keeps lockout-on-failure and returns `IdentityUserResult(UserId, Email, DisplayName, IsPlatformAdmin)`; roles are no longer returned by Identity. `IssueRefreshTokenAsync` and `RotateRefreshTokenAsync` are replaced by `SessionStore` (Infrastructure) used by `PlatformAuthService`: `CreateSessionAsync`, `IssueRefreshTokenAsync(sessionId, familyId)`, `ConsumeRefreshTokenAsync(hash)` (Serializable), `RevokeSessionAsync`, `RevokeSessionsForMembershipAsync`, `RevokeSessionsForOrganizationAsync`.

Refresh token: 32 random bytes, base64url to the client, SHA-256 hex stored; lifetime `ControlPlane:RefreshTokenDays` (default 7). Access token lifetime stays `Jwt:AccessTokenExpirationMinutes` (default 60).

### 4.9 Dependency Injection

`AddInfrastructure` gains:

```csharp
services.AddDbContext<ControlPlaneDbContext>(o => Configure(o, controlPlaneProvider, controlPlaneConnection, "ControlPlane"));
services.AddIdentity<PlatformUser, IdentityRole<Guid>>(existingOptions).AddEntityFrameworkStores<ControlPlaneDbContext>().AddDefaultTokenProviders();
services.AddScoped<IControlPlaneUnitOfWork, ControlPlaneUnitOfWork>();
services.AddScoped<IOrganizationResolver, HostOrganizationResolver>();
services.AddScoped<IAuthorizationSnapshotReader, AuthorizationSnapshotReader>();
services.AddScoped<IPlatformAuditLogger, PlatformAuditLogger>();
services.AddSingleton<ControlPlaneCircuitBreaker>();
services.AddScoped<SessionStore>();
services.AddScoped<ControlPlaneBackfill>(); services.AddScoped<PlatformAdminBootstrap>();
```

Configuration keys: `ControlPlane:Provider` (defaults to `Database:Provider`), `ConnectionStrings:ControlPlane{Provider}Connection` (fallback `ConnectionStrings:ControlPlaneConnection`), `ControlPlane:BaseDomain` (`carepathhealth.com`; `localhost` in Development), `ControlPlane:PlatformHosts[]` (`manage.carepathhealth.com`, `manage.localhost`), `ControlPlane:PublicHosts[]`, `ControlPlane:DefaultOrganization:*`, `ControlPlane:PlatformAdmin:*`, `ControlPlane:QueryTimeoutMs`, `ControlPlane:CircuitBreakerFailures`, `ControlPlane:RefreshTokenDays`, `ForwardedHeaders:KnownProxies[]`.

---

## 5. API Layer Design

### 5.1 Middleware

```csharp
// WebApi/Middleware/OrganizationResolutionMiddleware.cs
public async Task InvokeAsync(HttpContext ctx, IOrganizationResolver resolver, HttpOrganizationContext org, IOptions<ControlPlaneOptions> opt)
{
    var host = HostNormalizer.Normalize(ctx.Request.Host.Host);        // lower-case, trailing dot and port removed
    if (opt.Value.PublicHosts.Contains(host))   { org.Set(HostKind.Public, host, null); }
    else if (opt.Value.PlatformHosts.Contains(host)) { org.Set(HostKind.Platform, host, null); }
    else
    {
        ResolvedOrganization? r;
        try { r = await resolver.ResolveByHostAsync(host, ctx.RequestAborted); }
        catch (ControlPlaneUnavailableException) { await Problem503(ctx); return; }
        if (r is null || r.Status is OrganizationStatus.Suspended or OrganizationStatus.Offboarded)
        { org.Set(HostKind.Unknown, host, null); }
        else { org.Set(HostKind.Tenant, host, r); }
    }
    await _next(ctx);
}
```

Unknown hosts are not rejected here; controllers see `HostKind.Unknown` and the branding endpoint returns 404 while every other endpoint returns 401 through the freshness middleware (`host.mismatch`). This keeps unknown and suspended byte-identical.

```csharp
// WebApi/Middleware/AuthorizationFreshnessMiddleware.cs   (after UseAuthentication, before UseAuthorization)
if (ctx.User.Identity?.IsAuthenticated != true) { await _next(ctx); return; }   // anonymous endpoints decide for themselves
var claims = TokenClaims.From(ctx.User) ?? reject("token.claims");
AuthorizationSnapshot? snap;
try { snap = await reader.ReadAsync(claims.SessionId, ctx.RequestAborted); }
catch (Exception e) when (e is ControlPlaneUnavailableException or OperationCanceledException or DbException) { await audit("Unavailable","controlplane.unavailable"); await Problem503(ctx); return; }
var decision = freshness.Evaluate(claims, snap, org);
switch (decision.Outcome)
{
    case Allow: currentUser.Bind(claims); await _next(ctx); return;
    case Reject: await audit("Denied", decision.ReasonCode); await Problem401(ctx); return;
    case Unavailable: await audit("Unavailable", decision.ReasonCode); await Problem503(ctx); return;
}
```

`HttpCurrentUserContext` reads the new claims; `Roles` is the single role claim. `ProblemDetailsMiddleware` gains the 503 body: `{ type: "about:blank", title: "Service unavailable.", status: 503, errors: [{ code: "service.unavailable" }] }`.

### 5.2 Endpoints

| Method | Route | Host | Auth | Request → Response | Notes |
|---|---|---|---|---|---|
| POST | `/api/auth/login` | Tenant or Platform | anonymous | `LoginRequest` → `AuthTokenResponse` | 401 identical for bad password / no membership / unknown host; 503 when tenant not Ready |
| POST | `/api/auth/refresh` | same host as issuance | anonymous | `RefreshTokenRequest` → `AuthTokenResponse` | replay revokes family |
| POST | `/api/auth/logout` | any | bearer | — → 204 | revokes session |
| GET | `/api/organization/branding` | Tenant | anonymous | → `OrganizationBrandingDto` | 404 for Unknown/Public/Platform |
| PUT | `/api/organization/branding` | Tenant | Admin | `UpdateBrandingRequest` → `OrganizationBrandingDto` | |
| GET | `/api/organization/memberships` | Tenant | Admin | `PagedRequest` → `PagedResult<MembershipDto>` | own organization only |
| POST | `/api/organization/memberships` | Tenant | Admin | `CreateMembershipRequest` → `MembershipDto` | 409 `membership.exists` |
| PUT | `/api/organization/memberships/{id}/role` | Tenant | Admin | `UpdateMembershipRoleRequest` → `MembershipDto` | 409 `membership.last_active_admin` |
| PUT | `/api/organization/memberships/{id}/status` | Tenant | Admin | `UpdateMembershipStatusRequest` → `MembershipDto` | revokes sessions on Inactive |
| GET | `/api/platform/organizations` | Platform | PlatformAdmin | `PagedRequest` → `PagedResult<OrganizationSummaryDto>` | |
| POST | `/api/platform/organizations` | Platform | PlatformAdmin | `CreateOrganizationRequest` → `OrganizationDetailDto` | 409 `organization.slug_taken` / `organization.slug_reserved` |
| GET | `/api/platform/organizations/{id}` | Platform | PlatformAdmin | → `OrganizationDetailDto` | |
| PUT | `/api/platform/organizations/{id}` | Platform | PlatformAdmin | `UpdateOrganizationRequest` → detail | slug immutable |
| POST | `/api/platform/organizations/{id}/suspend` | Platform | PlatformAdmin | `SuspendOrganizationRequest` → detail | 409 `organization.confirm_mismatch` |
| POST | `/api/platform/organizations/{id}/reactivate` | Platform | PlatformAdmin | `ReactivateOrganizationRequest` → detail | |
| POST | `/api/platform/organizations/{id}/maintenance` | Platform | PlatformAdmin | `SetMaintenanceRequest` → detail | |
| DELETE | `/api/platform/organizations/{id}/maintenance` | Platform | PlatformAdmin | `EndMaintenanceRequest` (body) → detail | |
| GET/PUT | `/api/platform/organizations/{id}/branding` | Platform | PlatformAdmin | branding DTOs | |

Existing tenant endpoints are unchanged. `AdminUsersController` keeps its routes but delegates create, role, and status to `MembershipService`; its list joins tenant `User` rows with memberships by `PlatformUserId`.

Authorization policies: `PlatformAdmin` policy requires role claim `PlatformAdmin` and `token_kind = platform`. Existing role policies additionally require `token_kind = tenant`, added in `AddCarePathAuthentication`.

### 5.3 Program.cs Startup Order

```
1. control-plane connectivity probe (log provider + result)
2. if Database:AutoMigrate: ControlPlaneDbContext.MigrateAsync()
3. if Database:AutoMigrate: CarePathDbContext.MigrateAsync()
4. PlatformAdminBootstrap.RunAsync()
5. ControlPlaneBackfill.RunAsync()            (no-op when the default organization already exists)
6. CarePathDbContextSeed.SeedAsync()          (Development only; now creates memberships for its five users)
```

Pipeline: `UseCarePathProblemDetails → UseForwardedHeaders → UseHttpsRedirection (non-dev) → Swagger → UseCors → UseOrganizationResolution → UseAuthentication → UseAuthorizationFreshness → UseAuthorization → MapControllers`. CORS: a custom `ICorsPolicyProvider` is authoritative in every environment. Per request it allows the origin only if the origin's host equals the request host (same-origin deployment), or is a configured platform host, or is a verified `OrganizationDomain`. Domain lookups for CORS may be cached for 60 seconds because CORS is not an authorization decision; the freshness middleware still governs access. `Cors:AllowedOrigins` remains only as an additive list for local tooling (Swagger UI on another port) and is empty in production. No startup snapshot is built, so registering an organization needs no restart.

---

## 6. Presentation Layer Design

### 6.1 CarePath.Client

New typed clients: `OrganizationClient` (`GetBrandingAsync`, `UpdateBrandingAsync`), `MembershipsClient`, `PlatformOrganizationsClient`. `AuthClient` gains `LogoutAsync`. `InMemoryAccessTokenProvider` stores the full `AuthTokenResponse` as today.

### 6.2 CarePath.Client.UI

- `ConfirmDialog.razor` implementing the Confirmation Dialog Pattern (title, lead, impact tone, fields via `RenderFragment`, confirm label and kind, `ConfirmSlug` mode). Rendered with the native `dialog` element through a small JS interop for `showModal`/`close`.
- `BrandedHero.razor` for the sign-in split layout; `OrganizationBadge.razor` for the shell cue.
- `carepath-ui.css` gains the `.cp-dialog*` rules and theme token classes `theme-harbor|coastal|meadow|sunrise` mapping to `--brand-primary` / `--brand-accent`, extracted from the wireframe.

### 6.3 CarePath.Web

| Page | Route | Host | Notes |
|---|---|---|---|
| `Login.razor` | `/login` | Tenant | loads branding on init; shows maintenance/recovering state instead of the form when `ServiceState != Available`; support contact in footer |
| `ServiceState.razor` | `/unavailable` | Tenant | Access changed (after 401 on a previously valid session), Service outage (503), Maintenance, Recovery — copy from the wireframe |
| `MainLayout.razor` | — | Tenant | organization display name, monogram/logo, role badge; Sign out calls logout then clears session |
| `Platform/Login.razor` | `/login` | Platform | operator sign-in; `token_kind = platform` |
| `Platform/Organizations.razor` | `/platform/organizations` | Platform | table + tiles + New organization |
| `Platform/OrganizationNew.razor` | `/platform/organizations/new` | Platform | create form with slug address preview |
| `Platform/OrganizationDetail.razor` | `/platform/organizations/{id}` | Platform | readiness rows, Set maintenance / Suspend / End maintenance / Reactivate via `ConfirmDialog` |
| `Settings/AgencySetup.razor` | `/settings/agency` | Tenant, Admin | display name, monogram, logo, theme, support; live preview; Save/Cancel |

The app decides tenant vs platform mode from `OrganizationBrandingDto` (404 on the platform host) at startup and shows the matching layout. `TokenAuthenticationStateProvider` adds claims for organization id, membership id, and token kind; `NavMenu` uses the onboarding sidebar while the organization has no caregivers (a `HasCaregivers` flag on the existing caregivers list response).

A 401 on any request after a successful sign-in clears the session and routes to `/unavailable?state=access-changed`; a 503 routes to `/unavailable?state=outage` without clearing the session.

---

## 7. Testing Strategy

### 7.1 Domain (pure)

`OrganizationTests.IsServing_*`, `OrganizationMembershipTests.IsActive_*`, `PlatformRefreshTokenTests.IsUsable_*` with boundary dates, `PlatformSessionTests.IsRevoked_*`.

### 7.2 Application (Moq)

- `AuthorizationFreshnessServiceTests`: one test per reason code in §3.4, plus Allow for tenant and platform; snapshot null; readiness Maintenance → Unavailable.
- `PlatformAuthServiceTests`: login at non-serving host, no membership, platform host without PlatformAdmin, refresh replay revokes family, logout revokes.
- `OrganizationManagementServiceTests`: reserved slug, duplicate slug, confirm-slug mismatch, suspend revokes sessions and bumps versions, maintenance keeps sessions.
- `MembershipServiceTests`: existing platform user reuse, tenant profile failure deactivates membership, last-active-admin guard, role change bumps version and mirrors `User.Role`.
- Validator tests for each validator; contract parity and DTO reflection guards extended to `Contracts/Platform`.
- Architecture test: `Application` still references no Infrastructure; `Domain` has no reference to `Microsoft.AspNetCore.Identity`.

### 7.3 Infrastructure (EF Core InMemory / Sqlite)

- `ControlPlaneDbContextTests`: configurations, unique indexes (`Slug`, `HostName`, `(OrganizationId, PlatformUserId)`, `TokenHash`), audit event has no filter, soft-delete filter on the rest.
- `AuthorizationSnapshotReaderTests`: single query (assert `Database` command count = 1 via a test interceptor), soft-deleted membership still returned as inactive.
- `HostOrganizationResolverTests`: normalization, unverified domain ignored, suspended returns status.
- `ControlPlaneBackfillTests`: fresh run creates everything; second run no-op; missing config throws naming the key; inactive user gets no membership; password hash preserved and login succeeds via `UserManager`.
- `MigrationShapeTests` extended: tenant migration contains no `DropTable("AspNetUsers")`; control-plane snapshots exist for both providers.

### 7.4 WebApi integration (TestHost)

- Two `WebApplicationFactory<Program>` instances sharing one Sqlite connection for the control plane: revocation by role change, deactivation, suspension, logout, refresh replay, and epoch increment rejects the old token on both instances (FR-029).
- Unknown vs suspended host: byte-identical 404 (branding) and 401 (any other endpoint).
- Token from organization A at host B → 401, no tenant query (assert with a `DbCommandInterceptor` on `CarePathDbContext`).
- Platform token at tenant host and tenant token at `manage` → 401.
- Maintenance → branding returns state, login 503, existing session 503 then 200 after end.
- Control-plane unavailable (circuit forced open) → 503 and zero `CarePathDbContext` commands.
- `X-Forwarded-Host` from an unlisted address is ignored.
- All Sprint 4/5/6 controller tests still pass with tenant tokens for the backfilled organization.

### 7.5 Web (bUnit)

Login page renders branding and each service state; layout shows organization cue and role; onboarding sidebar when no caregivers; `ConfirmDialog` disables confirm until slug matches.

---

## 8. Performance Considerations

- Hot path per protected request: one `OrganizationDomains` lookup (unique index) and one snapshot query (PK on `PlatformSessions` plus three PK joins). Both under 5 ms on a warm pool; budget 10 ms p95 asserted in the integration test host with Sqlite as a smoke check, measured properly on Azure SQL in CP-06 load tests.
- No caching of anything in the hot path (ADR §8.6). Branding responses carry `Cache-Control: public, max-age=60`.
- Mass revocation (suspend) is one `ExecuteUpdate` over `PlatformSessions` by `OrganizationId` and one over memberships; no per-row loop.
- Control-plane pool: `Max Pool Size` set separately from the tenant pool so tenant saturation cannot starve identity (ADR §8.6).

---

## 9. Security Considerations

- Tenant identity comes only from the verified host; `organization_id` in the token must match it, and neither can be supplied by the client in a body, header, or query.
- No in-app organization switching and no membership listing for end users (FR-021).
- `SecretReference` is a configuration key or secret-manager name; connection strings never enter the control plane.
- Audit events store `SubjectHash` (SHA-256 of lower-cased email or host) and reason codes; never email, host names of unknown lookups, tokens, or PHI. Reason free text from the dialogs is stored on `Organization`/`Membership` rows, not in the audit event, and is never returned to tenant users.
- Client-role memberships are PHI-adjacent (requirements §3.5): `MembershipDto` is served only to the owning organization's Admin; `PlatformAuditEvent` never carries role or names.
- Refresh tokens are opaque, hashed at rest, single-use, family-revoked on reuse, and bound to a session that is bound to an organization.
- The authentication epoch is changed only by a privileged operations script (documented in the tasks spec), never by an API endpoint in CP-04.
- `IgnoreQueryFilters` is used in exactly two new places (snapshot reader, session revocation) and each is commented with why.
- **Accepted trade-off: the control plane is a single point of failure by design.** Every protected request depends on a live control-plane read, and unavailability yields 503 for every organization with no cached fallback (ADR §8.6). At the committed scale of about 50 organizations this is preferred over any path that could serve PHI on stale authorization. Consequences owned by CP-06 and operations: the control-plane database gets reserved capacity, its own availability and recovery objectives, and a failover drill that deliberately stops it and confirms every tenant fails closed. Public branding is the only thing allowed to keep working during an outage.

---

## 10. Deployment Plan

1. Provision the control-plane database (Azure SQL in the same elastic pool with reserved DTUs; PostgreSQL database `carepath_controlplane` in HomeLab) and set `ConnectionStrings:ControlPlane*Connection`, `ControlPlane:DefaultOrganization:*`, `ControlPlane:PlatformAdmin:*`, `ControlPlane:PlatformHosts`, `ForwardedHeaders:KnownProxies` in the environment (Pi env file / App Service settings).
2. DNS: `manage` and the default organization's host(s) point at the API; wildcard is an infrastructure task tracked in CP-06.
3. Deploy with `Database:AutoMigrate=true` once; startup runs the §5.3 sequence and logs counts. Verify: one organization, memberships = active users, all seeded users log in.
4. Web: publish per host (or one publish with runtime `Api:BaseAddress` = same origin).
5. Rollback: application binaries can roll back to the previous release because the tenant migration is additive and the legacy Identity tables are intact; the control-plane database is left in place. No data rollback is required.
6. Post-CP-06 cleanup migration drops the legacy Identity tables after a verified restore drill.

---

## 11. Monitoring & Observability

- Metrics: `controlplane.resolve.duration`, `controlplane.snapshot.duration`, `auth.freshness.reject{reason}`, `auth.freshness.unavailable`, `controlplane.circuit.state`, `auth.login{outcome}`, `auth.refresh.replay`.
- Logs (Serilog, no PHI): reason codes, organization id, session id, correlation id. Never email, host of failed lookups, or token contents.
- Alerts: freshness unavailable rate > 1% over 5 minutes; circuit open; login denied spike per organization; any `RefreshReplayDetected`.

---

## 12. Dependencies

No new NuGet packages. `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.SqlServer`, `Npgsql.EntityFrameworkCore.PostgreSQL`, and `System.IdentityModel.Tokens.Jwt` are already pinned in `Directory.Packages.props`. The circuit breaker is a 40-line class rather than a Polly dependency.

---

## 13. Open Questions & Decisions Needed

- [ ] **Suspension reasons** — the five `OrganizationSuspensionReason` values become an audit enum; confirm the list.
- [ ] **Data regions** — `US East`, `US West` are placeholders; the configured list should match the Azure regions actually provisioned.
- [ ] **Maintenance duration** — stored as `MaintenanceExpectedEndUtc`; CP-04 does not auto-end maintenance. Confirm that ending is always manual.
- [ ] **Seed users in Development** — the seed will create memberships for its five users in the default organization; confirm that Development also gets a second seeded organization for two-tenant manual testing (recommended: yes, `helpinghands.localhost`).
- [ ] **Logo upload** — `LogoStorageKey` exists but upload uses `IFileStorageService`, which is disabled outside `Storage:EnableLocalPrivateStorage`; CP-04 ships monogram-only unless local private storage is enabled.

---

## 14. Related Documents

- [Requirements](../01-requirements/cp-04-control-plane-foundation.md) · [Tasks](../03-tasks/cp-04-control-plane-foundation.md)
- [ADR 0003](../decisions/0003-multi-tenant-saas-database-strategy.md) · [UI design system](ui-design-system.md) · [Architecture.md](../../Documentation/Architecture.md)

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.1 | 2026-09-10 | CarePath Health | Review fixes: membership Provisioning state and activate-last sequence; first Admin membership created without tenant profile until CP-06; Id PK with unique OrganizationId FK on one-to-one entities; backfill rewritten as an idempotent saga with failure matrix; CORS provider made authoritative; control-plane single-point-of-failure trade-off recorded |
| 1.0 | 2026-09-08 | CarePath Health | Initial design from approved requirements v1.4 |
