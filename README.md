# CarePath Health

A .NET 10 healthcare management platform for **in-home care** (W-2 employees) and **healthcare staffing** (1099 contractors), built for the Maryland healthcare market.

CarePath Health streamlines caregiver scheduling, GPS-verified visit tracking, real-time billing, and margin analytics — all under a single platform designed with HIPAA compliance in mind.

## Tech Stack

- **Backend:** ASP.NET Core 9 Web API, SignalR, Entity Framework Core 9
- **Database:** SQL Server
- **Mobile:** .NET MAUI Blazor Hybrid (iOS/Android) — *planned*
- **Web Admin:** Blazor WebAssembly — *planned*
- **Auth:** ASP.NET Core Identity + JWT
- **Testing:** xUnit, Moq, FluentAssertions
- **Validation:** FluentValidation
- **Logging:** Serilog

## Architecture

CarePath follows **Clean Architecture** with strict dependency rules:

```
Domain  ←  Application  ←  Infrastructure  ←  WebApi
(innermost, zero deps)                        (outermost)
```

| Layer | Responsibility |
|---|---|
| **Domain** | Entities, value objects, enums, domain events. No external dependencies. |
| **Application** | Services, DTOs, validators, interfaces. Depends on Domain only. |
| **Infrastructure** | EF Core DbContext, repositories, external service integrations. |
| **WebApi** | ASP.NET Core controllers, middleware, SignalR hubs. |

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- SQL Server (local or remote)
- Visual Studio 2022+ or VS Code with the C# extension

### Build

```bash
dotnet build CarePath.sln
```

### Run the Web API

```bash
dotnet run --project WebApi
```

The API will be available at `http://localhost:5240` and `https://localhost:7028`.

### Run Tests

```bash
dotnet test CarePath.sln
```

### Control plane (CP-04)

The API uses two databases: the **control plane** (organizations, host names, branding, memberships, platform identity, append-only audit — never PHI) and one **tenant** database per organization (PHI). Configuration:

| Key | Purpose |
|---|---|
| `ConnectionStrings:ControlPlane{SqlServer\|PostgreSql}Connection` (or `ControlPlaneConnection`) | Control-plane database. Required; never defaults to the tenant string and may not equal it. |
| `ControlPlane:PlatformHostNames` | Hosts that serve platform-operator traffic (e.g. `platform.localhost`). Never assignable to an organization. |
| `ControlPlane:PlatformBaseDomain` | Base domain for generated `{slug}.{base}` organization hosts. |
| `ControlPlane:DefaultOrganization:*` | How the existing deployment is registered as organization one (display name, slug, hosts, data-plane locator). Required on an empty control plane outside Development. |
| `ControlPlane:PlatformAdmin:Email` / `DisplayName` | First platform operator, created once on an empty control plane. |
| `ControlPlane:PlatformAdmin:Password` | **Secret** — user secrets or environment (`CONTROLPLANE__PLATFORMADMIN__PASSWORD`) only. Startup fails closed without it when the operator must be created. |
| `ControlPlane:TrustedProxies:KnownProxies` / `KnownNetworks` | Reverse proxies whose `X-Forwarded-*` headers are honored. Empty = none trusted. |

Requests are routed to an organization by the **host name** before authentication; an unknown or suspended host receives the same generic 401 as a bad login. Tokens are bound to the organization they were issued for. Development hosts: `localhost` / `carepath.localhost` (CarePath Health) and `helpinghands.localhost` (synthetic second organization sharing the dev database); `platform.localhost` is the platform host. The Blazor client reads an empty `Api:BaseAddress` as same-origin; with an absolute address the organization is the one owning the API host.

```bash
# Set the platform operator password for local development (never commit it)
dotnet user-secrets set "ControlPlane:PlatformAdmin:Password" "<strong password>" --project WebApi
```

### EF Core Migrations

Two DbContexts, so `--context` is required on every command.

```bash
# Tenant (PHI) context — SQL Server
dotnet ef migrations add <MigrationName> --project Infrastructure --startup-project WebApi --context CarePathDbContext
dotnet ef database update --startup-project WebApi --context CarePathDbContext

# Control-plane context — SQL Server
dotnet ef migrations add <MigrationName> --project Infrastructure --startup-project WebApi --context ControlPlaneDbContext --output-dir ControlPlane/Migrations
dotnet ef database update --startup-project WebApi --context ControlPlaneDbContext

# PostgreSQL (either context): prefix with Database__Provider=PostgreSql and target Infrastructure.Migrations.PostgreSql
Database__Provider=PostgreSql dotnet ef migrations add <MigrationName> --project Infrastructure.Migrations.PostgreSql --startup-project WebApi --context ControlPlaneDbContext --output-dir Migrations/ControlPlane
```

## Project Structure

```
CarePath.sln
├── Domain/              # Core entities, value objects, enums, interfaces
├── WebApi/              # ASP.NET Core 9 API (controllers, middleware, hubs)
├── Documentation/       # Architecture docs and business references
└── _specs/              # Spec-driven development workflow
    ├── 01-requirements/ # Problem statements and user stories (Gherkin)
    ├── 02-design/       # Architecture decisions, entity design, API endpoints
    └── 03-tasks/        # Atomic implementation tasks
```

**Planned layers** (not yet scaffolded): Application, Infrastructure, MAUI Mobile, Blazor Web Admin, Shared.

## Domain Model

**Core Entities:** User, Caregiver, Client, CarePlan, Shift, VisitNote, VisitPhoto, Invoice, InvoiceLineItem, Payment, CaregiverCertification

**Value Objects:** Address, GpsCoordinates, Money, TimeRange, PhoneNumber

**Key Enums:** UserRole, EmploymentType (W-2/1099), CertificationType (CNA, RN, LPN), ServiceType, ShiftStatus, InvoiceStatus, PaymentMethod

## Key Conventions

- **Guid primary keys** — no auto-increment integers
- **UTC timestamps** for all DateTime properties
- **Soft deletes** via `IsDeleted` flag — never hard delete
- **Audit fields** on all entities: `CreatedBy`, `UpdatedBy`, `CreatedAt`, `UpdatedAt`
- **Nullable reference types** enabled across all projects
- **Spec-driven development** — features follow a three-phase process in `_specs/`

## Spec-Driven Development

All features follow a structured workflow:

1. **Requirements** (`_specs/01-requirements/`) — Problem statement, user stories, acceptance criteria
2. **Design** (`_specs/02-design/`) — Architecture decisions, entity design, API endpoints
3. **Tasks** (`_specs/03-tasks/`) — Atomic tasks (1–4 hours each), with dependencies and file lists

Create a new spec:

```bash
bash _specs/scripts/new-spec.sh CP-XX "Feature Name"
```

## Compliance

- **HIPAA** — required for all protected health information (PHI)
- **Data Retention** — 6-year minimum for medical records (Maryland requirement)
- **Encryption** — at rest and in transit
- **Authorization** — role-based access control; organization-bound tokens and host-based organization resolution (CP-04)
- **Control plane** — holds no PHI; identity record = email, display name, organization, role (classification sign-off tracked as S8-TASK-002)

## License

This project is proprietary. All rights reserved.
