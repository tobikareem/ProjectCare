# CarePath Health

A .NET 9 healthcare management platform for **in-home care** (W-2 employees) and **healthcare staffing** (1099 contractors), built for the Maryland healthcare market.

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

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
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

### EF Core Migrations

```bash
# Add a new migration
dotnet ef migrations add <MigrationName> --project Infrastructure --startup-project WebApi

# Apply migrations
dotnet ef database update --startup-project WebApi
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
- **Authorization** — role-based access control

## License

This project is proprietary. All rights reserved.
