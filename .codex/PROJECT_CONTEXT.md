# CarePath Project Context

Snapshot date: June 22, 2026

This file is a curated orientation snapshot derived from `Documentation/`, `_specs/`, the current solution, and the useful parts of `.claude/agent-memory/`. It is supporting context, not a substitute for `AGENTS.md` or approved specs.

## What CarePath Is Solving

CarePath Health is intended to run two related Maryland healthcare businesses:

### In-Home Care

- W-2 caregivers provide personal care in a client's home.
- Families need a lower-cost, more transparent alternative to institutional care.
- Operations need caregiver credentialing, scheduling, GPS check-in/out, visit notes, care plans, family visibility, billing, and quality tracking.
- Target gross margin: 40-45%.

### Healthcare Staffing

- CNAs, LPNs, and RNs fill facility per-diem and contract shifts.
- Facilities need faster, more reliable coverage during persistent workforce shortages.
- Operations need credential-aware matching, shift fulfillment, time tracking, facility billing, and profitability reporting.
- Target gross margin: 25-30%.

The competitive advantage is intended to come from operational automation and data: scheduling optimization, credential alerts, real-time service visibility, billing automation, margin analytics, and eventually predictive matching.

## Intended Users

- Admin: business configuration, users, financial oversight, compliance, and analytics.
- Coordinator: clients, caregivers, schedules, exceptions, and invoicing.
- Caregiver: assigned shifts, GPS attendance, visit documentation, and credentials.
- Client/family: care-plan and service visibility.
- Facility manager: staffing requests and facility shift visibility.

## Intended Architecture

Dependency direction:

```text
Domain <- Application <- Infrastructure <- WebApi
```

Planned presentation clients:

- .NET MAUI Blazor Hybrid caregiver/mobile app.
- Blazor WebAssembly administrator dashboard.

The Domain layer owns business concepts and abstractions. Application owns use cases, DTOs, validation, and mapping. Infrastructure owns EF Core, identity integration, storage, messaging, and external services. WebApi owns HTTP, authentication/authorization enforcement, middleware, and SignalR composition.

## Current Implementation

The solution currently contains:

- `Domain/`
- `Domain.Tests/`
- `WebApi/`

Implemented domain foundation:

- 12 entity types including `BaseEntity`.
- 8 enumerations.
- Generic repository and unit-of-work interfaces under `Domain/Interfaces/Repositories/`.
- Business calculations for age, billable hours, shift margin, invoice totals/balance/status, and certification state.
- XML documentation on the domain model.
- Domain unit and navigation tests.

Important implemented decisions:

- `BaseEntity.Id` and `CreatedAt` are init-only.
- `CarePlan` is in the Clinical namespace/folder.
- Visit signatures are URL fields rather than base64 payload fields.
- Shift gross margin is total shift margin, not only hourly rate spread.
- Invoice status supports partial payments and recalculation.
- Caregiver performance counters are changed through domain methods.
- Certification types include Maryland-relevant GNA and CRMA values.
- Payment methods distinguish Medicaid from general insurance.

Not implemented:

- Application project and use cases.
- Infrastructure project, DbContext, EF configurations, migrations, repositories, and SQL Server.
- ASP.NET Core Identity/JWT integration.
- Role and ownership authorization.
- Append-only PHI access auditing.
- Encryption and private media-storage implementation.
- Production API endpoints.
- SignalR.
- MAUI and Blazor clients.

The WebApi remains the template scaffold and still exposes WeatherForecast.

## Compliance Boundary

Always treat these entities as PHI-adjacent or sensitive:

- `Client`
- `CarePlan`
- `Shift`
- `VisitNote`
- `VisitPhoto`
- `CaregiverCertification`

Soft delete and audit columns are not sufficient for HIPAA compliance. Production readiness also requires:

- Role and resource-level authorization.
- Minimum-necessary DTOs.
- Append-only auditing of PHI reads, writes, and soft deletes.
- No PHI in application logs, exception messages, URLs, telemetry, or broad SignalR broadcasts.
- Private object storage with short-lived authorized media access.
- Encryption at rest and a documented application/column-encryption threat model.
- Retention, legal-hold, archival, and disposal policies.
- Operational monitoring and incident-response controls.

Do not describe the current application as HIPAA-compliant.

## Unresolved Architecture Decisions

Resolve these in approved specs before Infrastructure or authentication work:

1. `DateTime` versus `DateTimeOffset`, including how UTC kind is preserved in SQL Server.
2. ASP.NET Core Identity relationship to the domain `User` entity.
3. JWT and refresh-token ownership and storage.
4. TDE versus application/column encryption for sensitive fields.
5. PHI audit-event schema, append-only guarantees, and storage.
6. Private photo/signature object-key model and authorized download flow.
7. Six-year minimum retention, legal holds, archival, and deletion eligibility.
8. Pagination/filtering contracts for large `Shift` and `VisitNote` tables.

## Current Baseline Findings

- `dotnet build CarePath.sln` passes with 0 warnings and 0 errors.
- `dotnet test CarePath.sln` currently reports 218 passed and 1 failed.
- The failure is `ClientTests.Age_IsLeapYearSafe_ForFeb29Birthday`: it hard-codes age 25 for a February 29, 2000 birth date, but the correct age on June 22, 2026 is 26.
- `CaregiverCertification.IsExpired` compares exact timestamps, while `AGENTS.md` specifies date-only expiration semantics.
- `WebApi/Controllers/WeatherForecastController.cs` uses `DateTime.Now`, violating the UTC-only rule; the controller is still scaffold code.
- No `global.json` pins the intended .NET SDK, so builds may use a newer installed SDK to target .NET 9.

## Documentation Drift

The business documents remain useful for goals and economics, but technical examples are partly aspirational. Known drift includes:

- Planned `src/CarePath.*` and `CarePath.Api` paths versus actual root-level projects and `WebApi`.
- `Enums/` examples versus the implemented `Enumerations/` folder.
- Pre-implementation wording in CP-01 requirements.
- Authentication examples containing `PasswordHash` and `RefreshToken` that are absent from the current domain `User`.
- Older margin and signature-storage examples that differ from the shipped domain model.
- Premature "HIPAA Compliant" wording in the progress deck.

Use current approved specs and code together, and resolve contradictions before extending the architecture.
