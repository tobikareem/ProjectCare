# CP-04 — Control Plane Foundation: Design

**Status**: Approved  
**Author**: CarePath Health  
**Created**: 2026-10-21  
**Depends on**: CP-02 (Infrastructure — complete), Sprint 3/4 auth and Application boundary (complete), ADR 0003 (Accepted 2026-09-08)  
**Requirements spec**: `_specs/01-requirements/cp-04-control-plane-foundation.md`  
**Tasks spec**: `_specs/03-tasks/cp-04-control-plane-foundation.md`  
**Successors**: CP-05 Tenant-Aware Identity and Authorization · CP-06 Tenant Data Plane Routing

---

## Architecture Overview

CP-04 adds a second, physically separate database — the **control plane** — that owns platform identity (ASP.NET Core Identity), organizations, host-name mapping, branding, memberships, the data-plane locator, and a non-PHI append-only audit log. The existing `CarePathDbContext` keeps every operational and PHI table but stops hosting Identity. No new projects are introduced; both DbContexts live in `Infrastructure` and share the existing two migrations assemblies (one per provider).

Every request is now resolved to an organization from its host name **before** authentication, and every token is bound to exactly one organization (or to the platform). CP-04 does not create or route to tenant databases; the backfilled organization's data plane still points at the single existing database until CP-06.

```
Request ──► ForwardedHeaders ──► OrganizationResolution ──► Authentication ──► Authorization ──► Controller
                (trusted proxy)     (host → org, fail closed)   (token ↔ host)      (role / policy)

CarePath.Domain/
├── Entities/Platform/
│   ├── Organization.cs
│   ├── OrganizationDomain.cs
│   ├── OrganizationBranding.cs
│   ├── OrganizationMembership.cs
│   ├── OrganizationDataPlane.cs
│   └── PlatformAuditEvent.cs
├── Enumerations/
│   ├── OrganizationStatus.cs · ProvisioningState.cs · DomainVerificationStatus.cs
│   ├── MembershipStatus.cs · DataPlaneProvider.cs · BrandingColorToken.cs
│   └── PlatformAuditAction.cs
└── Interfaces/Repositories/IControlPlaneUnitOfWork.cs

CarePath.Application/
├── Abstractions/Tenancy/      IOrganizationContext, IOrganizationHostResolver, HostResolution, ResolvedOrganization
├── Abstractions/Audit/        IPlatformAuditLogger, PlatformAuditEntry  (+ PhiAuditEntry.OrganizationId)
├── Platform/                  HostNameNormalizer, OrganizationSlug, Services/, Validators/, Mapping/
├── Organization/              Services/ (membership, branding query), Validators/
└── Auth/Services/             ILoginService, LoginService

CarePath.Infrastructure/
├── ControlPlane/
│   ├── Persistence/           ControlPlaneDbContext, ControlPlaneUnitOfWork, Configurations/
│   ├── Identity/              PlatformUser, PlatformRefreshToken
│   ├── Auth/                  PlatformIdentityService, PlatformIdentityProvisioningService
│   ├── Resolution/            EfOrganizationHostResolver, MemoryBrandingCache
│   ├── Audit/                 EfPlatformAuditLogger
│   ├── Bootstrap/             ControlPlaneBootstrapper
│   ├── Migrations/            (SQL Server; [DbContext(typeof(ControlPlaneDbContext))])
│   ├── ControlPlaneOptions.cs
│   └── ControlPlaneServiceCollectionExtensions.cs
└── Persistence/BaseEntityModelConventions.cs   (shared by both contexts)

Infrastructure.Migrations.PostgreSql/Migrations/ControlPlane/   (PostgreSQL control-plane migrations)

CarePath.WebApi/
├── Tenancy/                   OrganizationContext, OrganizationResolutionMiddleware, OrganizationResolutionExtensions
├── Security/                  UnauthorizedResponseWriter (+ OnTokenValidated binding, PlatformAdmin policy)
├── Diagnostics/               ControlPlaneHealthCheck
└── Controllers/               PlatformOrganizationsController, OrganizationMembershipsController, OrganizationBrandingController

CarePath.Contracts/Platform/, CarePath.Contracts/Organization/, Enumerations/{OrganizationStatus,ProvisioningState,MembershipStatus,BrandingColorToken,AuthTokenKind}
CarePath.Client/Api/           OrganizationBrandingClient, OrganizationMembershipsClient, PlatformOrganizationsClient
CarePath.Client.UI/Theming/    BrandingTokens
CarePath.Web/                  Login.razor branding, TokenAuthenticationStateProvider claims
```

### Affected layers

- [x] Domain — new `Entities/Platform/`, seven enumerations, `IControlPlaneUnitOfWork`, `User.PlatformUserId`
- [x] Application — tenancy abstractions, login/membership/platform services, validators, platform audit abstraction
- [x] Infrastructure — `ControlPlaneDbContext`, Identity re-homing, resolver, bootstrapper, migrations (both providers)
- [x] WebApi — forwarded headers, resolution middleware, token binding, three controllers, health check
- [x] Contracts / Client / Client.UI — DTOs, enum mirrors, typed clients, branding token map
- [x] Web — login branding, organization-aware auth state
- [ ] MAUI — none

---

## Entity Designs

All control-plane entities inherit `BaseEntity` (Guid key, audit fields, `IsDeleted`). None holds a clinical, insurance, address, date-of-birth, or care field. A Domain test (`ControlPlaneEntityClassificationTests`) enforces the denylist by reflection.

### Organization

```csharp
using CarePath.Domain.Entities.Common;
using CarePath.Domain.Enumerations;

namespace CarePath.Domain.Entities.Platform;

/// <summary>A customer agency (tenant). Non-PHI.</summary>
public class Organization : BaseEntity
{
    /// <summary>Registered legal name (max 200).</summary>
    public string LegalName { get; set; } = string.Empty;

    /// <summary>Name shown in the UI (max 100).</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Immutable URL-safe identifier: 3–40 chars, [a-z0-9], single hyphens, unique. Reserved: platform, admin, api, www, login.</summary>
    public string Slug { get; set; } = string.Empty;

    public OrganizationStatus Status { get; set; } = OrganizationStatus.Pending;

    public ProvisioningState ProvisioningState { get; set; } = ProvisioningState.Registered;

    /// <summary>IANA time zone id (max 64), e.g. America/New_York.</summary>
    public string DefaultTimeZone { get; set; } = "America/New_York";

    /// <summary>Deployment region label (max 32).</summary>
    public string DataRegion { get; set; } = "us-east";

    /// <summary>Operator-entered reason when Status == Suspended (max 200). Never PHI.</summary>
    public string? SuspensionReason { get; set; }

    public DateTime? SuspendedAtUtc { get; set; }

    /// <summary>True when tenant traffic may be admitted: Active and Provisioned.</summary>
    public bool IsAdmissible => Status == OrganizationStatus.Active && ProvisioningState == ProvisioningState.Provisioned;
}
```

### OrganizationDomain

```csharp
/// <summary>A host name mapped to an organization. HostName is normalized (lower-case, no port, no trailing dot) and unique platform-wide.</summary>
public class OrganizationDomain : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;   // navigation; set by EF

    /// <summary>Normalized host, max 253 (RFC 1123).</summary>
    public string HostName { get; set; } = string.Empty;

    public bool IsPrimary { get; set; }

    public DomainVerificationStatus VerificationStatus { get; set; } = DomainVerificationStatus.Pending;
}
```

### OrganizationBranding

```csharp
/// <summary>White-label fields. Colors are design-system tokens, never raw values. One row per organization.</summary>
public class OrganizationBranding : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;

    /// <summary>Private storage key for the logo blob (max 256). Resolved to a short-lived URL by a later spec; never a public URL.</summary>
    public string? LogoStorageKey { get; set; }

    public BrandingColorToken PrimaryColorToken { get; set; } = BrandingColorToken.Teal900;
    public BrandingColorToken AccentColorToken { get; set; } = BrandingColorToken.Orange;

    /// <summary>Organization support mailbox (max 256). Not a user's email.</summary>
    public string? SupportEmail { get; set; }

    /// <summary>Organization support line (max 20).</summary>
    public string? SupportPhone { get; set; }
}
```

### OrganizationMembership

```csharp
/// <summary>
/// The sole source of truth for a platform user's role in one organization.
/// (OrganizationId, PlatformUserId) is unique. SecurityVersion increments atomically with any role or status change (ADR 0003 §10.1); CP-05 enforces it per request.
/// </summary>
public class OrganizationMembership : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;

    /// <summary>Control-plane Identity user id. Equals the tenant User.Id by construction (D-CP04-02).</summary>
    public Guid PlatformUserId { get; set; }

    /// <summary>Tenant-side User.Id when a tenant profile exists. Null for organizations without a provisioned data plane.</summary>
    public Guid? TenantUserId { get; set; }

    /// <summary>One of the six tenant roles. PlatformAdmin is never a membership role.</summary>
    public UserRole Role { get; set; }

    public MembershipStatus Status { get; set; } = MembershipStatus.Active;

    public int SecurityVersion { get; set; }

    public DateTime? LastLoginAtUtc { get; set; }
}
```

### OrganizationDataPlane

```csharp
/// <summary>Locator for an organization's operational database. Never holds a connection string.</summary>
public class OrganizationDataPlane : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;

    public DataPlaneProvider Provider { get; set; }

    /// <summary>Configuration or secret-manager key that resolves to the connection string (max 200), e.g. "ConnectionStrings:SqlServerConnection".</summary>
    public string SecretReference { get; set; } = string.Empty;

    /// <summary>Orchestrator-maintained admission projection; the database's migration history stays authoritative (ADR 0003 §8.4). Null until CP-06.</summary>
    public string? SchemaVersion { get; set; }

    public DateTime RegisteredAtUtc { get; set; }
}
```

### PlatformAuditEvent

```csharp
/// <summary>Append-only, non-PHI platform audit record. No update or delete path exists in code; IRepository&lt;T&gt; is never exposed for it.</summary>
public class PlatformAuditEvent : BaseEntity
{
    public Guid? ActorPlatformUserId { get; set; }
    public Guid? OrganizationId { get; set; }
    public PlatformAuditAction Action { get; set; }          // stored as string (max 64)
    public string EntityType { get; set; } = string.Empty;    // max 64
    public Guid? EntityId { get; set; }
    public string? CorrelationId { get; set; }                // max 64
    /// <summary>SHA-256 (hex, 64) of the normalized host for denied resolutions. Never the host string.</summary>
    public string? HostHash { get; set; }
    public DateTime OccurredAtUtc { get; set; }
}
```

### Infrastructure-only identity types (not Domain)

```csharp
// Infrastructure/ControlPlane/Identity/PlatformUser.cs
public class PlatformUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;   // max 100; staff name only
    public bool IsPlatformAdmin { get; set; }
}

// Infrastructure/ControlPlane/Identity/PlatformRefreshToken.cs  (credential artifact; not BaseEntity)
public class PlatformRefreshToken
{
    public Guid Id { get; set; }
    public Guid PlatformUserId { get; set; }
    public Guid? MembershipId { get; set; }      // null for platform tokens
    public Guid? OrganizationId { get; set; }    // denormalized for host binding; null for platform tokens
    public string TokenHash { get; set; } = string.Empty;   // SHA-256 base64, max 128, unique
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
```

### Existing entity additions

- `User.PlatformUserId` (`Guid?`): traceability to the platform identity without a cross-database FK. Nullable in CP-04; tightened to NOT NULL in CP-05 after HomeLab backfill verification (D-CP04-07). Unique filtered index.
- `PhiAuditEntry.OrganizationId` (`Guid? = null`, trailing optional parameter): CP-06 routes tenant PHI audit by it; existing call sites compile unchanged.
- `ApplicationUser` and `ApplicationUserConfiguration` are **deleted**. `CarePathDbContext` derives from `DbContext`.

---

## Enumerations

| Enum | Values |
|---|---|
| `OrganizationStatus` | `Pending = 1, Active = 2, Suspended = 3, Offboarded = 4` |
| `ProvisioningState` | `Registered = 1, Provisioning = 2, Provisioned = 3, Failed = 4` |
| `DomainVerificationStatus` | `Pending = 1, Verified = 2` |
| `MembershipStatus` | `Active = 1, Inactive = 2` |
| `DataPlaneProvider` | `SqlServer = 1, PostgreSql = 2` (Domain mirror; Domain must not reference `Infrastructure.DatabaseProvider`) |
| `BrandingColorToken` | `Teal900, Teal700, Teal100, Orange, Green, Amber, Red` → mapped to `--teal-900` etc. in `CarePath.Client.UI/Theming/BrandingTokens.cs` |
| `PlatformAuditAction` | `OrganizationRegistered, OrganizationUpdated, OrganizationSuspended, OrganizationReactivated, DomainAdded, DomainRemoved, BrandingUpdated, MembershipCreated, MembershipRoleChanged, MembershipDeactivated, MembershipReactivated, PlatformAdminBootstrapped, LoginDeniedNoMembership, LoginDeniedInactiveMembership, LoginDeniedNotPlatformAdmin, HostResolutionDenied, RefreshTokenReuseDetected, BackfillOrganizationCreated, BackfillMembershipsCreated` |

Contracts mirrors: `OrganizationStatus`, `ProvisioningState`, `MembershipStatus`, `BrandingColorToken`, `AuthTokenKind { Tenant = 1, Platform = 2 }`. Existing enum-parity tests are extended to cover them.

---

## Persistence

### ControlPlaneDbContext

- `ControlPlaneDbContext : IdentityUserContext<PlatformUser, Guid>` — **not** `IdentityDbContext`. The control plane has no Identity roles: `PlatformAdmin` is `PlatformUser.IsPlatformAdmin`; tenant roles live on memberships (D-CP04-04).
- DbSets: `Organizations`, `OrganizationDomains`, `OrganizationBrandings`, `OrganizationMemberships`, `OrganizationDataPlanes`, `PlatformAuditEvents`, `PlatformRefreshTokens` (+ Identity `Users`, `UserClaims`, `UserLogins`, `UserTokens`).
- Same `AuditableEntityInterceptor`; `BaseEntityModelConventions.Apply(modelBuilder)` (extracted from `CarePathDbContext.ApplyBaseEntityConventions`) applies the soft-delete filter and UTC converters to every `BaseEntity` type. `SqlDialect` handles provider-specific filtered indexes.
- Configurations (Fluent API only), all FKs `DeleteBehavior.Restrict`:
  - `Organizations`: unique `Slug`; index `Status`.
  - `OrganizationDomains`: unique `HostName`; index `(OrganizationId, IsPrimary)`.
  - `OrganizationBrandings`: unique `OrganizationId`; enums stored as strings.
  - `OrganizationMemberships`: unique `(OrganizationId, PlatformUserId)`; index `PlatformUserId`; `Role`/`Status` as strings; `SecurityVersion` concurrency-safe via `ExecuteUpdateAsync` increments.
  - `OrganizationDataPlanes`: unique `OrganizationId`.
  - `PlatformRefreshTokens`: unique `TokenHash`; index `(MembershipId, RevokedAtUtc)`; index `PlatformUserId`.
  - `PlatformAuditEvents`: index `(OrganizationId, OccurredAtUtc)`; index `(HostHash, OccurredAtUtc)`.
  - `AspNetUsers` (PlatformUser): `DisplayName` 100; `Email` unique via Identity's normalized index.

### Repositories

- `IControlPlaneUnitOfWork` (Domain): `Organizations`, `Domains`, `Brandings`, `Memberships`, `DataPlanes` as `IRepository<T>`, plus `SaveChangesAsync` and `ExecuteInTransactionAsync` overloads matching `IUnitOfWork`. Implementation `ControlPlaneUnitOfWork` over `ControlPlaneDbContext`.
- `Repository<T>` gains `protected internal Repository(DbContext context)`; the public `Repository(CarePathDbContext)` constructor chains to it so open-generic DI still binds the tenant context.
- Hot path bypasses repositories: `EfOrganizationHostResolver` issues one projected query (`OrganizationDomains` join `Organizations` by PK).
- `PlatformAuditEvent` is written only through `IPlatformAuditLogger` (`Add` + `SaveChangesAsync`); no repository exposes it.

### Registration and configuration

`AddInfrastructure(configuration)` calls `AddControlPlane(services, configuration, provider)`:

- Connection string: `ConnectionStrings:ControlPlane{Provider}Connection`, fallback `ConnectionStrings:ControlPlaneConnection`, else throw. **No fallback to the tenant string** — pointing the control plane at the tenant database silently recreates the single-database design.
- Migrations assemblies: SQL Server stays `CarePath.Infrastructure` (folder `Infrastructure/ControlPlane/Migrations/`); PostgreSQL stays `CarePath.Infrastructure.Migrations.PostgreSql` (folder `Migrations/ControlPlane/`). EF filters migration classes by `[DbContext]`, so two contexts share one assembly per provider.
- Identity: `AddIdentityCore<PlatformUser>(options)` with the existing password/lockout options, `.AddEntityFrameworkStores<ControlPlaneDbContext>().AddDefaultTokenProviders()`. No `AddRoles`, no `SignInManager` (no cookie scheme dependency).
- `ControlPlaneOptions` bound from `ControlPlane:*` with `ValidateDataAnnotations().ValidateOnStart()`.
- `services.AddMemoryCache()` for the public branding cache.

```jsonc
"ConnectionStrings": {
  "ControlPlaneSqlServerConnection": "Server=(localdb)\\mssqllocaldb;Database=CarePathControlPlane;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True",
  "ControlPlanePostgreSqlConnection": "Host=localhost;Database=carepath_controlplane;Username=devuser;Password=<secret>"
},
"ControlPlane": {
  "PlatformHostNames": [ "platform.localhost", "platform.devpi.local" ],
  "PlatformBaseDomain": "localhost",
  "DefaultOrganization": {
    "LegalName": "CarePath Health LLC",
    "DisplayName": "CarePath Health",
    "Slug": "carepath",
    "DefaultTimeZone": "America/New_York",
    "DataRegion": "us-east",
    "HostNames": [ "localhost", "carepath.localhost", "carepath.devpi.local" ],
    "DataPlane": { "Provider": "SqlServer", "SecretReference": "ConnectionStrings:SqlServerConnection" }
  },
  "PlatformAdmin": { "Email": "platform-admin@carepath.local", "DisplayName": "Platform Operator" },
  "TrustedProxies": { "KnownProxies": [], "KnownNetworks": [] },
  "BrandingCacheSeconds": 60,
  "HostDenialAuditWindowSeconds": 60
}
```

`ControlPlane:PlatformAdmin:Password` and `SeedData:DefaultPassword` come only from user secrets or environment variables. Production supplies `DefaultOrganization` and `TrustedProxies` through `CONTROLPLANE__*` environment variables. The committed Development defaults above are the Product Owner-approved values (2026-10-21).

### `dotnet ef` commands (two contexts)

```bash
# Control plane — SQL Server
dotnet ef migrations add <Name> --project Infrastructure --startup-project WebApi --context ControlPlaneDbContext --output-dir ControlPlane/Migrations
# Control plane — PostgreSQL
Database__Provider=PostgreSql dotnet ef migrations add <Name> --project Infrastructure.Migrations.PostgreSql --startup-project WebApi --context ControlPlaneDbContext --output-dir Migrations/ControlPlane
# Tenant — add --context CarePathDbContext to the existing commands
```

---

## Startup Sequence

Replaces the AutoMigrate block in `WebApi/Program.cs`. Steps 2, 4, 5, 6 are idempotent and run on every startup regardless of `Database:AutoMigrate` (requirement §4.3).

| # | Step | Gate | Failure behavior |
|---|---|---|---|
| 1 | `ControlPlaneDbContext.Database.MigrateAsync()` | `Database:AutoMigrate` | throw |
| 2 | `ControlPlaneBootstrapper.CopyLegacyIdentityAsync()` — reads tenant `AspNetUsers` through `CarePathDbContext.Database.GetDbConnection()` with provider-neutral SQL (identifiers via `SqlDialect.Quote`), after an `information_schema.tables` existence check; copies `Id, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed, PasswordHash, SecurityStamp, ConcurrencyStamp, PhoneNumber, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnd, LockoutEnabled, AccessFailedCount` verbatim into `PlatformUser` rows via `DbSet.Add` (never `UserManager.CreateAsync`, which would rehash); `DisplayName` from tenant `User.FullName`; `IsPlatformAdmin=false`. Asserts `Id == DomainUserId` for every legacy row and fails closed otherwise. | always | throw (never partial) |
| 3 | `CarePathDbContext.Database.MigrateAsync()` — applies `RemoveIdentityFromTenantContext` | `Database:AutoMigrate` | throw |
| 4 | `EnsureDefaultOrganizationAsync()` — on an empty control plane requires `ControlPlane:DefaultOrganization`; creates `Organization` (Active, Provisioned), its `OrganizationDomain` rows (Verified), default `OrganizationBranding`, and `OrganizationDataPlane`; subsequent runs find by slug and change nothing | always | throw naming the missing key |
| 5 | `EnsureMembershipsAsync()` — for every non-deleted, `IsActive` tenant `User` without a membership in the default organization: create Active membership with `Role = User.Role`, `TenantUserId = User.Id`; set `User.PlatformUserId = User.Id`. Inactive users get an *Inactive* membership (cannot log in; keeps the backfill complete); deleted users get none (identity row already copied in step 2) | always | throw |
| 6 | `EnsurePlatformAdminAsync()` — when no `IsPlatformAdmin` user exists: create from `ControlPlane:PlatformAdmin` with password from secrets; if the email exists as a non-admin, throw (never silently elevate) | always | throw |
| 7 | `CarePathDbContextSeed.SeedAsync(...)` — Development only; seeds `PlatformUser`s + memberships through the control plane; also seeds `helpinghands` (Active, Provisioned, data plane = same dev database, flagged dev-only) with the seeded caregiver holding a Caregiver membership there | Development | throw |

HomeLab (`AutoMigrate=false`) runbook: apply `InitialControlPlane`, apply tenant `RemoveIdentityFromTenantContext`, start the app (steps 2/4/5/6 run), verify every existing account logs in. The legacy `AspNet*` tables remain unmanaged in the tenant database; the drop migration ships in CP-05 after that verification (D-CP04-08).

---

## Request Pipeline

```
UseCarePathProblemDetails
UseForwardedHeaders            ForwardedHeaders.XForwardedFor | XForwardedProto | XForwardedHost
                               KnownProxies/KnownNetworks cleared, then filled from ControlPlane:TrustedProxies; ForwardLimit = 1
UseHttpsRedirection            (non-Development)
UseSwagger / UseSwaggerUI      (Development or Swagger:Enabled)
startup block                  (table above)
UseCors("WebClient")
UseOrganizationResolution      ← new
UseAuthentication
UseAuthorization
MapHealthChecks(/health/live, /health/ready)   — /health/ready now includes ControlPlaneHealthCheck
MapControllers
```

### OrganizationResolutionMiddleware

| Situation | Behavior |
|---|---|
| Path starts with `/health` | skip resolution (Docker probes use `localhost`) |
| Normalized host ∈ `ControlPlane:PlatformHostNames` | `Kind = Platform`, continue |
| `EfOrganizationHostResolver` returns a tenant: domain `Verified`, organization not deleted, `Status == Active`, `ProvisioningState == Provisioned` | `Kind = Tenant` with id, slug, display name; continue |
| Unresolved (unknown, unverified, pending, suspended, offboarded, malformed host) and path is `GET /api/organization/branding` | `Kind = Unresolved`, continue; controller throws `ResourceNotFoundException` → existing 404 body (`resource.not_found`, no traceId) |
| Unresolved, any other path | write the shared generic 401 body and stop; audit `HostResolutionDenied` with `HostHash` (throttled: one event per host-hash per `HostDenialAuditWindowSeconds` per instance, D-CP04-06); log reason code only (never the host string) |
| Resolver throws `DbException` / `TimeoutException` | 503 problem body `"Service unavailable."` with no detail; log at Error with exception type only |

`HostNameNormalizer.Normalize(string?)` (Application): trim, strip `:port`, strip one trailing `.`, lower-case invariant; returns null when empty, non-ASCII, > 253 chars, or any label is empty or > 63 chars. Used by the middleware, validators, bootstrapper, and seed.

`IOrganizationContext` (Application, scoped): `OrganizationResolutionKind Kind` (`Unresolved`, `Platform`, `Tenant`), `Guid? OrganizationId`, `string? Slug`, `string? DisplayName`. WebApi `OrganizationContext` is the mutable holder set once by the middleware.

The generic 401 body is centralized in `WebApi/Security/UnauthorizedResponseWriter.cs` (`ProblemDetailsResponse`, code `auth.invalid_credentials`, `TraceId = null`, `application/problem+json`). `AuthController` failures, the resolution middleware, and `JwtBearerEvents.OnChallenge` all use it, which makes unknown-host, wrong-organization, suspended-organization, and invalid-credential responses byte-identical (requirement §4.4, §2.3).

---

## Authentication and Tokens

### Claims

`JwtTokenRequest(Guid UserId, string Email, IReadOnlySet<string> Roles, string? CorrelationId, string TokenKind, Guid? OrganizationId, Guid? MembershipId)`.

| Claim | Tenant token | Platform token |
|---|---|---|
| `sub`, `nameid`, `email`, `jti`, `correlation_id` | as today | as today |
| `role` | exactly one of the six tenant roles | `PlatformAdmin` |
| `token_kind` | `tenant` | `platform` |
| `organization_id` | organization Guid | absent |
| `membership_id` | membership Guid | absent |

Issuer, audience, signing key, and lifetime are unchanged (`Jwt:*`). `SecurityVersion` is **not** a claim in CP-04 (D-CP04-05).

### Binding (token ↔ host)

Enforced in `JwtBearerEvents.OnTokenValidated` inside `AddCarePathAuthentication`:

- `Kind == Tenant`: require `token_kind == tenant`, `organization_id` parses and equals `IOrganizationContext.OrganizationId`, `membership_id` present. Else `context.Fail(...)`.
- `Kind == Platform`: require `token_kind == platform` and no `organization_id`.
- `Kind == Unresolved` never reaches authentication except on the anonymous branding route.

A failed `OnTokenValidated` leaves the request unauthenticated; the existing fallback policy denies; `OnChallenge` writes the shared body. Controllers never run. Belt and braces in policies: every tenant role policy adds `RequireClaim("token_kind", "tenant")`; new policy `PlatformAdmin` = `RequireRole("PlatformAdmin")` + `RequireClaim("token_kind", "platform")`. `ApplicationRoles.PlatformAdmin` is a separate constant, deliberately **not** in `ApplicationRoles.All`, so the role-policy loop and the Contracts `UserRole` parity test are unchanged.

Outcomes the requirements name:

| Case | Result |
|---|---|
| Tenant token replayed at another organization's host | 401, controller never runs |
| PlatformAdmin token at a tenant host | 401 |
| Tenant Admin token calling `/api/platform/organizations` at its own host | 403 (policy) |
| Tenant Admin token calling `/api/platform/organizations` at the platform host | 401 (kind mismatch) — the requirements scenario is amended to name the host |

### ICurrentUserContext

Gains explicit `Guid? OrganizationId` and `Guid? MembershipId` (both null for anonymous and platform requests). Not default interface members: a production implementation that forgets them must fail to compile. `HttpCurrentUserContext` reads the two claims. The ten test fakes in `Application.Tests` each gain two members.

### Login and refresh (`Application/Auth/Services/LoginService`)

1. `IOrganizationContext.Kind` decides the path; `Unresolved` cannot occur (middleware stopped it).
2. `IIdentityService.ValidateCredentialsAsync(email, password)` → `IdentityUserResult(Succeeded, PlatformUserId, Email, DisplayName, IsPlatformAdmin, FailureCode)`; lockout via `UserManager.AccessFailedAsync` / `IsLockedOutAsync` / `ResetAccessFailedCountAsync`.
3. Tenant: find membership `(OrganizationId, PlatformUserId)`; require `Status == Active`; else audit `LoginDeniedNoMembership` / `LoginDeniedInactiveMembership` (actor id, org id, no email) and fail.
   Platform: require `IsPlatformAdmin`; else audit `LoginDeniedNotPlatformAdmin` and fail.
4. Issue `PlatformRefreshToken` bound to `(PlatformUserId, MembershipId?, OrganizationId?)`; issue JWT.
5. Tenant: set `OrganizationMembership.LastLoginAtUtc`; when `TenantUserId` is set, update tenant `User.LastLoginAt` through `IUnitOfWork` (best effort inside the same request; failure logged, login still succeeds).
6. Every failure returns the same failed result so the controller emits the single generic body.

Refresh (`RotateRefreshTokenAsync`): hash → lookup; require not expired, not revoked, binding matches the resolved host (tenant: `OrganizationId == resolved`; platform: `MembershipId == null` and `Kind == Platform`); re-check user lockout, membership `Active` (tenant), `IsPlatformAdmin` (platform); revoke the old row with a conditional `ExecuteUpdateAsync ... WHERE RevokedAtUtc IS NULL` (single winner, as today); insert the new row. Presenting an already-revoked token revokes every active token for that membership (or user, for platform) and audits `RefreshTokenReuseDetected`. Lifetime stays 7 days. No logout endpoint in CP-04 (session revocation is CP-05).

---

## Application Services

| Service | Responsibility |
|---|---|
| `ILoginService` / `LoginService` | Login and refresh as above. `AuthController` becomes a thin adapter. |
| `IOrganizationMembershipService` / `OrganizationMembershipService` | `ListAsync` (paged, own organization only), `CreateAsync` (existing platform identity → link; new email → provision `PlatformUser` with temporary password, then tenant `User` with `Id = PlatformUser.Id` when the organization is the backfilled one, `TenantUserId` set; compensating soft delete of the membership if the tenant write fails — documented two-phase write, no distributed transaction), `ChangeRoleAsync`, `SetStatusAsync`. Last-active-Admin guard on role change and deactivation (409 `membership.last_active_admin`). Every change: `SecurityVersion++`, platform audit, and mirror to tenant `User.Role` / `User.IsActive` while CP-04 keeps those columns (D-CP04-09). Organization always comes from `ICurrentUserContext.OrganizationId`, never from the request. |
| `IPlatformOrganizationService` / `PlatformOrganizationService` | `RegisterAsync` (→ `Pending`/`Registered`; primary domain `{slug}.{ControlPlane:PlatformBaseDomain}` Verified; default branding; audit), `ListAsync`, `GetAsync`, `UpdateAsync` (names, time zone, region), `SuspendAsync(reason)`, `ReactivateAsync` (only `Suspended → Active`, and only when `Provisioned`; Pending organizations cannot be activated in CP-04 — FR-015), `AddDomainAsync` (system subdomains Verified; other hosts Pending — verification workflow is a later spec; platform/base-domain/IP hosts rejected and only Verified hosts may be primary, D-CP04-13), `RemoveDomainAsync` (soft; never the last Verified domain nor the primary host), `GetBrandingAsync`, `UpdateBrandingAsync`. |
| `IOrganizationBrandingQuery` | Anonymous branding read for the resolved tenant through `IBrandingCache` (60 s, invalidated on `UpdateBrandingAsync`/`UpdateAsync`/`SuspendAsync`). |
| `AdminUserManagementService` (existing) | `CreateStaffUserAsync` → `IOrganizationMembershipService.CreateAsync`; `UpdateRoleAsync` → `ChangeRoleAsync`; `UpdateStatusAsync` → `SetStatusAsync`. Routes and DTOs unchanged. `IIdentityRoleManagementService` deleted. |

Validation (FluentValidation, Application): slug `^[a-z0-9](?:[a-z0-9-]{1,38}[a-z0-9])$` with no `--`, reserved list `platform, admin, api, www, login`; host RFC 1123 after normalization; time zone via `TimeZoneInfo.TryFindSystemTimeZoneById`; email format; phone max 20; color tokens by enum; `SuspensionReason` max 200. Validation responses keep the existing generic-message behavior (no attempted values).

---

## API Endpoints

| Method | Route | Auth | Host | Description |
|---|---|---|---|---|
| POST | `/api/auth/login` | anonymous | tenant or platform | Organization-bound (or platform) login |
| POST | `/api/auth/refresh` | anonymous | tenant or platform | Rotate refresh token bound to membership |
| GET | `/api/organization/branding` | anonymous | tenant | Approved branding fields only; 404 when unresolved |
| GET | `/api/organization/memberships` | Admin | tenant | Paged memberships of the caller's organization |
| POST | `/api/organization/memberships` | Admin | tenant | Create membership (existing or new platform user) |
| PUT | `/api/organization/memberships/{id}/role` | Admin | tenant | Change role (last-admin guard) |
| PUT | `/api/organization/memberships/{id}/status` | Admin | tenant | Activate / deactivate (last-admin guard) |
| GET | `/api/platform/organizations` | PlatformAdmin policy | platform | Paged list |
| POST | `/api/platform/organizations` | PlatformAdmin | platform | Register (Pending / Registered) |
| GET | `/api/platform/organizations/{id}` | PlatformAdmin | platform | Detail incl. domains, data plane summary |
| PUT | `/api/platform/organizations/{id}` | PlatformAdmin | platform | Update names / time zone / region |
| POST | `/api/platform/organizations/{id}/suspend` | PlatformAdmin | platform | Suspend with reason |
| POST | `/api/platform/organizations/{id}/reactivate` | PlatformAdmin | platform | Suspended → Active (Provisioned only) |
| GET / PUT | `/api/platform/organizations/{id}/branding` | PlatformAdmin | platform | Branding |
| GET / POST | `/api/platform/organizations/{id}/domains` | PlatformAdmin | platform | Domains |
| DELETE | `/api/platform/organizations/{id}/domains/{domainId}` | PlatformAdmin | platform | Soft-remove domain |
| existing | `/api/admin/users…` | Admin | tenant | Unchanged routes; delegate to membership service |

`{id}` routes on membership endpoints are scoped by the caller's organization in the service (a membership id from another organization yields the PHI-safe 404 shape). Platform routes carry no PHI.

---

## Contracts, Client, Web

- `CarePath.Contracts/Auth/AuthTokenResponse`: `Role` becomes `UserRole?` (null for platform tokens); add `AuthTokenKind TokenKind`, `Guid? OrganizationId`, `string? OrganizationSlug`, `string? OrganizationDisplayName`, `Guid? MembershipId`.
- `CarePath.Contracts/Organization/`: `OrganizationBrandingDto` (DisplayName, Slug, PrimaryColorToken, AccentColorToken, SupportEmail, SupportPhone — no storage key), `MembershipDto`, `CreateMembershipRequest` (Email, Role, FirstName, LastName, PhoneNumber, `TemporaryPassword?` required only when no platform identity exists), `UpdateMembershipRoleRequest`, `UpdateMembershipStatusRequest`.
- `CarePath.Contracts/Platform/`: `OrganizationSummaryDto`, `OrganizationDto`, `RegisterOrganizationRequest`, `UpdateOrganizationRequest`, `SuspendOrganizationRequest`, `OrganizationDomainDto`, `AddOrganizationDomainRequest`, `OrganizationBrandingDetailDto`, `UpdateOrganizationBrandingRequest`.
- `CarePath.Client/Api/`: `OrganizationBrandingClient`, `OrganizationMembershipsClient`, `PlatformOrganizationsClient` over `ApiClientBase`.
- `CarePath.Client.UI/Theming/BrandingTokens.cs`: `CssVariable(BrandingColorToken)` → `var(--teal-900)` etc. No literal colors anywhere in Razor (UI design-system rule).
- `CarePath.Web/Pages/Login.razor`: loads branding in `OnInitializedAsync`; renders `DisplayName` in the existing `.auth-brand` block and applies token variables; on 404 or error keeps the neutral CarePath brand silently (indistinguishability). No new screens; the wireframe's platform console remains prototype-only, platform admins use the API/Swagger in CP-04.
- `TokenAuthenticationStateProvider`: adds `organization_id`, `organization_name`, `membership_id`, `token_kind` claims; adds `ClaimTypes.Role` only when `Role` is non-null.
- `CarePath.Web/Program.cs`: empty `Api:BaseAddress` → `builder.HostEnvironment.BaseAddress` (same-origin per organization host); Development keeps the explicit value. `Cors:AllowedOrigins` must list each organization's web origin (configuration task).

---

## Testing

| Suite | Location | Proves |
|---|---|---|
| `ControlPlaneEntityClassificationTests` | `Domain.Tests/Platform/` | No property on any `Entities/Platform` type matches the PHI denylist |
| `HostNameNormalizerTests`, `OrganizationSlugTests` | `Application.Tests/Platform/` | Normalization and slug rules incl. reserved words |
| `ControlPlaneDbContextTests`, `DependencyInjectionTests` | `Infrastructure.Tests/ControlPlane/`, `Persistence/` | Model shape, filters, converters, DI resolves both contexts, control plane never falls back to the tenant string |
| `ControlPlaneMigrationShapeTests` + `MigrationShapeTests` | `Infrastructure.Tests/Persistence/` | Both providers: control-plane tables present, no `AspNetRoles`, no PHI column names; tenant `RemoveIdentityFromTenantContext` adds `PlatformUserId` and contains no `DropTable` |
| `ControlPlaneBootstrapperTests` | `Infrastructure.Tests/ControlPlane/` | Default org idempotent; missing config throws naming the key; legacy copy (SQLite tenant DB with raw-SQL `AspNetUsers`) preserves hashes/stamps verbatim, is idempotent, fails closed on Id mismatch; inactive users copied without membership; PlatformAdmin bootstrap fail-closed and never elevates |
| `PlatformIdentityServiceTests` | `Infrastructure.Tests/ControlPlane/` | Lockout, refresh binding, single-winner rotation, family revoke on reuse |
| `LoginServiceTests`, `OrganizationMembershipServiceTests`, `PlatformOrganizationServiceTests` | `Application.Tests/` | Membership gating, last-admin guard, mirror writes, audit entries without email, two-phase create compensation, lifecycle transitions |
| `OrganizationResolutionMiddlewareTests` | `Application.Tests/WebApi/` | Byte-identical 401/404 across unknown/pending/suspended/unverified; normalization; forged `X-Forwarded-Host` ignored from an unlisted source, honored from a known proxy; 503 on resolver failure; `/health` bypass |
| `AuthConfigurationTests`, `AuthControllerTests`, `Sprint4ControllerContractTests` (+ policy theory), `Cp04ClientRouteAlignmentTests` | `Application.Tests/WebApi/` | PlatformAdmin policy, `token_kind` requirement, anonymous action set unchanged, controller attributes, client routes |
| `ControlPlaneContractTests` | `Application.Tests/Contracts/` | New DTOs carry no PHI/financial fields; `AuthTokenResponse` safe |
| `TwoOrganizationIntegrationTests` | `Application.Tests/WebApi/` (TestServer, InMemory for both contexts, real auth pipeline) | Distinct `organization_id` per host; cross-host 401 with controller never invoked; no-membership 401 byte-equal; suspension immediate; refresh rejected cross-host and after deactivation; platform token 401 at tenant host; tenant token 403 at platform route; host resolution < 5 ms warm |
| `LoginBrandingTests` | `CarePath.Web.Tests/` (bUnit) | Branding rendered from DTO; 404 falls back silently; no literal colors |

---

## HIPAA Considerations

**Residual verification (not provable from code; from the 2026-10-21 `/hipaa-check` run):** encryption at rest for the control-plane database and its backups (TDE / volume encryption); TLS for both PostgreSQL connections (`SSL Mode=Require` or server-side `hostssl`); legacy `AspNetUsers` hashes in the tenant database until TASK-100 ships after S8-TASK-003, with interim backups under credential-data controls; HomeLab `ControlPlane:TrustedProxies:KnownNetworks` set so forwarded headers come only from nginx; secrets (`CONTROLPLANE__PLATFORMADMIN__PASSWORD`, JWT key, seed password) outside image and git; 6-year retention/backup policy for `PlatformAuditEvents` and `OrganizationMemberships`; CORS `AllowedOrigins` per organization web origin.

- The control plane holds no clinical, insurance, address, DOB, or care field; a Domain reflection test enforces this.
- **Classification flag for the Security/Compliance owner (ADR 0003 §8.6):** `PlatformUser.Email` + `OrganizationMembership.Role == Client` reveals a healthcare relationship. CP-04 cannot avoid this because email is the login identifier. Sign-off must explicitly accept "email + display name + phone number + organization + role" as the control-plane identity record and treat the store as sensitive-but-non-clinical, with the same encryption-at-rest and access controls as the tenant databases. The sign-off row in the requirements spec stays Pending until that acceptance is recorded.
- Platform audit events never contain an email, a host string (SHA-256 hash only), or PHI. Logs record reason codes and exception types only.
- Unknown-host, pending, suspended, wrong-organization, and invalid-credential responses are byte-identical; PHI 404 bodies are unchanged.
- No PHI in URLs: organization slugs appear in host names and are not PHI. Membership routes use membership ids scoped to the caller's organization.
- `IPhiAuditLogger` continues to cover every tenant PHI access; `PhiAuditEntry.OrganizationId` is populated from `ICurrentUserContext` where available.
- Secrets: `OrganizationDataPlane.SecretReference` is a key, never a connection string; passwords for bootstrap and seed come only from secrets/environment.
- Migrations: tenant Identity tables are **not** dropped in CP-04; the drop is forward-only and ships in CP-05 after verification (`_specs/lessons.md`: destructive rollback must fail closed).

---

## Deployment and Rollback

- New database per environment (`CarePathControlPlane` / `carepath_controlplane`). Both migration sets are generated in the same task (ADR 0003 §8.1 "same domain schema intent, no uncommitted model changes per provider").
- Order on first deploy: control-plane migration → legacy copy → tenant migration → bootstrap. All steps are re-runnable.
- Rollback: redeploy the previous build **without** reverting migrations. The tenant `AspNet*` tables still exist and are untouched, so the previous build's Identity works; `Users.PlatformUserId` is an extra nullable column the previous build ignores. The control-plane database can be left in place. Forward-only by design.
- Infrastructure tasks outside code: wildcard DNS `*.carepathhealth.com`; nginx `server_name` stays `_` (host is forwarded as-is); `CONTROLPLANE__TRUSTEDPROXIES__KNOWNNETWORKS__0` set to the compose network CIDR on HomeLab.

---

## Architectural Decisions

### D-CP04-01 — Separate control-plane database and DbContext, no new projects
**Decision**: `ControlPlaneDbContext` lives in `Infrastructure`, with migrations in the existing per-provider assemblies, selected by `[DbContext]`.  
**Reason**: ADR 0003 requires physical separation of identity/organization data from PHI; the existing assembly split exists for providers, not contexts.  
**Trade-off**: Every `dotnet ef` command needs `--context`. Documented in CLAUDE.md.

### D-CP04-02 — `PlatformUser.Id == tenant User.Id`
**Decision**: Keep the existing invariant (set today by `IdentityProvisioningService` and the seed) so `sub` is both the platform user id and the tenant user id.  
**Reason**: Every Application service already treats `ICurrentUserContext.UserId` as `User.Id`; the backfill becomes a by-Id copy; CP-04 avoids touching object authorization.  
**Trade-off**: A tenant `User` row in a second organization's data plane (CP-06) must be created with the same id; `TenantUserId` remains on the membership for that.

### D-CP04-03 — No security-relevant cache; authoritative read per request
**Decision**: Host → organization (with status and provisioning state) is one indexed control-plane query per request. Membership status is read on login/refresh. Only public branding is cached (60 s).  
**Reason**: ADR 0003 §8.6 forbids accepting status from stale caches without separately approved staleness; a "cache host but read status" design still costs one query, so the cache buys nothing. Suspension takes effect on the next request. Supersedes requirements §4.1.  
**Trade-off**: One control-plane round trip per request. Tables hold ≤ 50 rows; a resolution cache is a CP-06 optimization that must come with the §8.6 approval.

### D-CP04-04 — `IdentityUserContext<PlatformUser, Guid>`, no Identity roles
**Decision**: The control plane has no `AspNetRoles`; `PlatformAdmin` is a flag; tenant roles live on memberships.  
**Reason**: Roles have exactly one source of truth (ADR §10, lessons.md); avoids the double `IRoleStore` registration hazard during the transition.  
**Trade-off**: `[Authorize(Roles = ...)]` on existing controllers still works because the role claim is emitted from the membership at token time.

### D-CP04-05 — `SecurityVersion` column now, enforcement in CP-05
**Decision**: Add and increment `OrganizationMembership.SecurityVersion` on every role/status change; do not emit or validate it in CP-04.  
**Reason**: Fixes the schema so CP-05 needs no migration; per-request validation belongs with the session/epoch work.  
**Trade-off**: In CP-04 a role or status change takes effect at the next login or refresh, bounded by `Jwt:AccessTokenExpirationMinutes` (60).

### D-CP04-06 — Throttled host-denial audit
**Decision**: `HostResolutionDenied` is written at most once per host-hash per `HostDenialAuditWindowSeconds` per instance; denied logins are always audited.  
**Reason**: FR-022 as written is a write-amplification vector for anyone scanning the public IP with random `Host` values.  
**Trade-off**: Per-instance throttle means up to N events per window across N replicas.

### D-CP04-07 — `User.PlatformUserId` nullable in CP-04
**Decision**: Add as nullable with a filtered unique index; CP-05 makes it NOT NULL after HomeLab verification.  
**Reason**: NOT NULL in the same migration would require the cross-database backfill to run inside the migration.  
**Trade-off**: Login logic, not the schema, guarantees the link in CP-04.

### D-CP04-08 — Legacy tenant Identity tables retained until CP-05
**Decision**: `RemoveIdentityFromTenantContext` removes Identity from the EF model and adds `PlatformUserId` only; scaffolded `DropTable` operations are hand-removed. The drop migration is a CP-05 task.  
**Reason**: Forward-only destructive change; the previous build must remain deployable until every existing login is verified on HomeLab.  
**Trade-off**: Unmanaged tables linger in the tenant database for one release.

### D-CP04-09 — `User.Role` and `User.IsActive` mirrored from memberships
**Decision**: The membership service is the only writer of `User.Role` / `User.IsActive` (besides the backfill); existing read paths keep working. CP-05 removes `User.Role`.  
**Reason**: FR-007 and the requirements risk table; avoids touching every consumer of `User.Role` in CP-04.  
**Trade-off**: One replicated fact for one release, with one declared writer.

### D-CP04-10 — Platform host names in configuration
**Decision**: `ControlPlane:PlatformHostNames` is configuration, not `OrganizationDomain` rows.  
**Reason**: The platform host must not be editable through the API it protects; the reserved slug `platform` keeps the subdomain free.  
**Trade-off**: Adding a platform host is a deployment change.

### D-CP04-11 — `AuthTokenResponse.Role` nullable with `TokenKind`
**Decision**: Platform tokens return `Role = null`, `TokenKind = Platform`.  
**Reason**: Adding `PlatformAdmin` to the Contracts `UserRole` would break Domain parity and leak a control-plane role into tenant checks.  
**Trade-off**: Two Web files handle the nullable role.

### D-CP04-12 — Membership create never reveals whether an identity exists (slice 4 review, 2026-10-21)
**Decision**: `CreateMembershipRequest.TemporaryPassword` is always required (8–128) and ignored when the email already belongs to a platform identity; the response DTO is built from the request (never from the directory); the write order is control plane first (identity when new, then membership) and tenant profile second, with compensating soft delete of the membership (`CompensateAsync` for a provisioned identity) so the filtered unique indexes let a retry succeed.  
**Reason**: With the original "password required only for new emails" rule an organization Admin could enumerate every email registered anywhere on the platform (400 vs 201) and read another organization's display name.  
**Trade-off**: Linking an existing identity into an organization still happens without that person's consent; an invitation/acceptance flow is deferred to CP-05 and recorded there. Accepted until CP-06: the tenant-side check "a tenant user with this email already exists" (generic 400) can only reveal users of the caller's own data plane; while two organizations share one development/HomeLab database it also reveals the other organization's users, which ends when CP-06 provisions one data plane per organization.

### D-CP04-13 — Host assignment guards (slice 4 review, 2026-10-21)
**Decision**: `AddDomainAsync` rejects (generic 400) any host that is a configured platform host, the platform base domain, any label under the base domain other than the organization's own `{slug}.{base}`, or an IP literal; only a Verified host may be marked primary (409 `organization.domain_unverified`); `RemoveDomainAsync` refuses the primary host (409 `organization.primary_domain`) in addition to the last Verified host. Branding reads never write (defaults are returned until the first update). `LogoStorageKey` must be a relative, traversal-free key; `DefaultTimeZone` must be an IANA id (`Area/Location` or `UTC`).  
**Reason**: Pending hosts are not resolvable today, but once the verification workflow ships an unguarded host would be a takeover of another organization's subdomain or of the platform host itself.  
**Trade-off**: `IPlatformDomainSettings` exposes the platform host set to the Application layer.

### D-CP04-14 — Branding follows the API host; same-origin deployment is the target (slice 5 review, 2026-10-21)
**Decision**: The public branding endpoint resolves the organization from the host the API request arrives at. `CarePath.Web` treats an empty `Api:BaseAddress` as same-origin, so a deployment that serves the app and the API from one organization host gets that organization's branding automatically. The three committed appsettings keep absolute API URLs; with them the branding shown is that of the API host (`localhost` → the default organization), which is correct for development and documented for HomeLab/production in TASK-099. The branding client carries no authorization handler, and the login page renders the neutral brand first and applies branding only when a well-formed response arrives (any transport, JSON or cancellation failure is swallowed without logging).  
**Reason**: Reading the organization from `Origin`/`Referer` would let a browser choose the organization; the request host is the only server-controlled signal (FR-012).  
**Trade-off**: Split-host deployments must register the API host as a domain of the organization, or move to same-origin (CP-06 routing work).

---

## Requirement Amendments (recorded here; applied to the requirements spec)

1. §4.1 cache → superseded by D-CP04-03.
2. FR-022 denied host resolution → throttled (D-CP04-06).
3. §3.2 `PlatformUser` base context → `IdentityUserContext` (D-CP04-04).
4. §3.2 `User.PlatformUserId` required → nullable in CP-04 (D-CP04-07).
5. §3.2 forward-only Identity migration → tables retained; drop is CP-05 (D-CP04-08).
6. §2.2 "tenant token cannot call platform endpoints → 403" → 403 at the tenant's own host, 401 at the platform host.
7. §2.3 development hosts → platform hosts come from configuration (D-CP04-10).
8. `AuthTokenResponse` shape (D-CP04-11).
9. `OrganizationMembership` gains `SecurityVersion` (D-CP04-05).
10. Development seed adds `helpinghands` as a second Active organization sharing the dev database (dev-only).
11. FR-016 membership create → temporary password always required; response never reveals identity existence (D-CP04-12).
12. FR-014 domains → host assignment guards and primary-host rules (D-CP04-13).
13. FR-012 branding → resolved from the API request host; same-origin is the deployment target (D-CP04-14).
15. HIPAA review (2026-10-21, PASS WITH WARNINGS): malformed-host denials now share a sentinel hash so the audit throttle and global budget apply; PHI audit events carry the resolved organization id (`LoggingPhiAuditLogger`); the control-plane identity record includes the phone number (S8-TASK-002 wording updated); startup step 5 creates an *Inactive* membership for inactive users (not none) so the backfill is complete and such users still cannot log in.
14. Final-review hardening (2026-10-21): refresh applies the same admissibility rule as login (inactive tenant profile cannot refresh); `Repository.UpdateAsync` leaves tracked entities to change tracking so a login's `LastLoginAt` write cannot overwrite a concurrent deactivation; unknown-email logins burn a password hash (no timing oracle); the host-denial throttle checks the per-hash window before the global budget; a default-organization host equal to a platform host fails startup. Deferred to CP-05 (listed on the Sprint 8 board): absolute refresh lifetime, legacy-copy marker, membership concurrency token, relational rotation test.

---

## Related Documents

- `_specs/01-requirements/cp-04-control-plane-foundation.md`
- `_specs/03-tasks/cp-04-control-plane-foundation.md`
- `_specs/decisions/0003-multi-tenant-saas-database-strategy.md`
- `_specs/sprints/sprint-08-control-plane-foundation.md`, `_specs/sprints/sprint-08-tasks.md`
- `Documentation/Architecture.md`

## Revision History

| Version | Date | Author | Changes |
|---|---|---|---|
| 1.0 | 2026-10-21 | CarePath Health | Initial design from the CP-04 requirements and ADR 0003; approved by Tobi via Claude plan review 2026-10-21 (decisions D-CP04-01..11) |
