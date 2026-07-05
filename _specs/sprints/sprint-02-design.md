# Sprint 2 Design - Infrastructure Foundation

Date: 2026-06-27
Author: CarePath Health
Status: Draft
Sprint: Sprint 2
Primary spec: CP-02 Infrastructure / EF Core

Related specs:
- `_specs/sprints/sprint-02-requirements.md`
- `_specs/sprints/sprint-02-tasks.md`
- `_specs/02-design/cp-02-infrastructure-ef-core.md`

## Executive Summary

Sprint 2 implements a Clean Architecture Infrastructure layer around EF Core 9 and SQL Server. Infrastructure depends on Domain, implements Domain repository interfaces, enforces HIPAA-safe persistence rules, and exposes registration through `AddInfrastructure()` for WebApi.

## Architecture Overview

```text
WebApi
  -> Infrastructure.DependencyInjection
       -> Persistence/CarePathDbContext
       -> Persistence/Repositories/Repository<T>
       -> Persistence/Repositories/UnitOfWork
       -> Persistence/Interceptors/AuditableEntityInterceptor
       -> Persistence/Converters/UtcDateTimeConverter
       -> Identity/ApplicationUser
  -> Domain interfaces and entities

Domain remains independent.
Application is not required for Sprint 2.
```

## Affected Projects

| Project | Changes |
|---|---|
| `Domain` | Add `GetPagedAsync` to repository contract if not already present. No EF references. |
| `Infrastructure` | New project containing EF Core, Identity schema foundation, repositories, migrations, seed strategy, DI. |
| `Infrastructure.Tests` | New test project for persistence behavior. |
| `WebApi` | Add Infrastructure registration and connection string config only. No controllers/endpoints. |

## Design Decisions

| Decision | Outcome |
|---|---|
| DbContext path | `Infrastructure/Persistence/CarePathDbContext.cs` |
| Repository path | `Infrastructure/Persistence/Repositories/` |
| Configuration path | `Infrastructure/Persistence/Configurations/<Area>/` |
| Interceptors/converters | Sibling folders under `Persistence/`, not under `Configurations/` |
| Identity model | `ApplicationUser : IdentityUser<Guid>` linked to Domain `User` by `DomainUserId` |
| Deletion | Repository soft delete only; PHI FK delete behavior is `Restrict` |
| Time | `DateTime.UtcNow` and UTC converters for all DateTime values |
| Seed data | Synthetic-only; development-only unless explicitly approved |

## Persistence Model

`CarePathDbContext` inherits `IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>` and defines DbSets for all persisted Domain entities.

Minimum DbSets:
- Identity/people: `Users`, `Caregivers`, `CaregiverCertifications`, `Clients`
- Clinical: `CarePlans`
- Scheduling: `Shifts`, `VisitNotes`, `VisitPhotos`
- Billing: `Invoices`, `InvoiceLineItems`, `Payments`
- Transitions if present: `DischargeDocuments`, `TransitionPlans`, `TransitionInstructions`, `TransitionReminders`, `TransitionCheckIns`, `TransitionEscalations`

## Entity Configuration Matrix

| Area | Entity | Configuration file | Key requirements |
|---|---|---|---|
| Identity | User | `Configurations/Identity/UserConfiguration.cs` | Email unique, FullName ignored, soft-delete filter. |
| Identity | Caregiver | `Configurations/Identity/CaregiverConfiguration.cs` | User FK, employment type index, rate precision. |
| Identity | CaregiverCertification | `Configurations/Identity/CaregiverCertificationConfiguration.cs` | Restrict delete, expiration index, computed properties ignored. |
| Identity | Client | `Configurations/Identity/ClientConfiguration.cs` | User FK, PHI string lengths, age ignored, restrict clinical children. |
| Clinical | CarePlan | `Configurations/Clinical/CarePlanConfiguration.cs` | Client FK restrict, PHI text length, status/indexes. |
| Scheduling | Shift | `Configurations/Scheduling/ShiftConfiguration.cs` | Client restrict, caregiver nullable/set-null, dates UTC, margin properties ignored. |
| Scheduling | VisitNote | `Configurations/Scheduling/VisitNoteConfiguration.cs` | Shift FK restrict, clinical notes length, TransitionPlanId optional. |
| Scheduling | VisitPhoto | `Configurations/Scheduling/VisitPhotoConfiguration.cs` | VisitNote FK restrict, no public URL assumption, PHI retention. |
| Billing | Invoice | `Configurations/Billing/InvoiceConfiguration.cs` | Client restrict, invoice number unique, totals ignored. |
| Billing | InvoiceLineItem | `Configurations/Billing/InvoiceLineItemConfiguration.cs` | Invoice FK, optional Shift FK, monetary precision. |
| Billing | Payment | `Configurations/Billing/PaymentConfiguration.cs` | Invoice FK, reference number, payment date index. |
| Transitions | Transition entities | `Configurations/Transitions/*.cs` | RawContent/SourceText no indexes, restrict clinical relationships. |

## Repository and UnitOfWork Design

`Repository<T>`:
- Generic constraint: `where T : BaseEntity`.
- Uses EF Core `DbSet<T>`.
- Returns `IReadOnlyList<T>` for collection methods.
- Does not call `SaveChangesAsync`.
- Implements soft delete by setting `IsDeleted = true`.
- Implements `GetPagedAsync(pageNumber, pageSize, cancellationToken)` with total count.

`UnitOfWork`:
- Owns `CarePathDbContext` commit lifecycle.
- Lazily creates repositories.
- Exposes `SaveChangesAsync`.
- Supports begin/commit/rollback transaction methods.
- Implements `IDisposable` and `IAsyncDisposable`.

## Audit Design

`AuditableEntityInterceptor` runs during SaveChanges:
- Added entities: set `CreatedAt`, `CreatedBy`.
- Modified entities: preserve `CreatedAt`/`CreatedBy`, set `UpdatedAt`, `UpdatedBy`.
- Missing user context: use a system actor value.
- Logs only metadata: user id, timestamp, action, entity type, entity id.
- Never logs PHI values.

Read audit is not implemented in Sprint 2 unless a read service is added; Sprint 2 defines persistence metadata and contracts needed by later Application/API read-audit implementations.

## UTC DateTime Design

Create converters for `DateTime` and `DateTime?`. Apply converters to every DateTime property through configuration helpers or explicit configuration. Tests must verify SQL Server-style round-trip behavior returns UTC kind.

## Migration Design

Migration name: `InitialCreate`

Review checklist:
- All primary keys are `uniqueidentifier`.
- No unexpected cascade delete on PHI relationships.
- Monetary columns use decimal precision.
- Large PHI text fields are not indexed.
- `IsDeleted` exists and is indexed where useful.
- Identity tables use Guid keys.
- Down migration is reversible.

## Seed Data Design

Seed data must be obviously synthetic:
- Names like `Synthetic Client 001`, not realistic names.
- Fake addresses and clinical notes explicitly marked synthetic.
- No production-like discharge text, diagnosis details, patient names, DOBs, or real addresses.
- Seeding runs only in Development unless explicitly enabled.

## Testing Strategy

| Test area | Required coverage |
|---|---|
| DbContext | DbSets, model creation, configuration application. |
| Entity configuration | table names, FKs, delete behaviors, indexes, string lengths, decimal precision, ignored computed properties. |
| Soft delete | DeleteAsync sets IsDeleted and query filter excludes records. |
| UTC converter | DateTime and DateTime? round trips preserve UTC. |
| Audit interceptor | create/update fields, current user capture, system fallback. |
| Repository | CRUD, FindAsync, CountAsync, ExistsAsync, GetPagedAsync. |
| UnitOfWork | lazy repo creation, SaveChanges, transactions, disposal. |
| Migration | schema generated, no PHI cascade delete, Identity tables present. |
| Seed data | idempotent, development-only, synthetic-only. |

## Security and Compliance

- Connection strings must use encryption settings appropriate to environment.
- Production secrets must not be stored in source control.
- Sensitive EF logging must be disabled outside Development.
- No PHI in logs, exception messages, migration comments, or seed data.
- PHI relationship delete behavior must prioritize retention over convenience.

## Deployment and Rollback

Local development commands:

```bash
dotnet ef migrations add InitialCreate --project Infrastructure --startup-project WebApi
dotnet ef database update --startup-project WebApi
```

Rollback:

```bash
dotnet ef database update 0 --project Infrastructure --startup-project WebApi
```

Production migration execution must be handled by a reviewed deployment process, not automatic startup migration, unless explicitly approved.

## Open Questions

- Should SQL Server Testcontainers be required for Sprint 2, or is EF InMemory plus migration review sufficient for the first pass?
- Should `GetAllAsync` remain on `IRepository<T>` after `GetPagedAsync` is added, or should Application avoid using it for high-volume entities by policy only?
- Should seed data include CP-03 Transitions examples now, or wait until Sprint 5 backend implementation?

## Revision History

| Version | Date | Author | Changes |
|---|---|---|---|
| 1.0 | 2026-06-27 | Codex | Initial Sprint 2 design spec. |