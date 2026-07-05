---
name: cp02-phase2-infrastructure-review
description: CP-02 Phase 2 (feature/dispatch) review — entity configurations, migrations, seed, Repository/UnitOfWork/DI shipped. Supersedes phase1 findings that were fixed.
metadata:
  type: project
---

CP-02 Phase 2 changeset reviewed 2026-07-04 on feature/dispatch branch. Build: 0 warnings/errors. Tests: 289/289 passing (242 Domain.Tests + 47 Infrastructure.Tests).

**Why:** Second review after entity configurations, migrations, seed data, Repository<T>, UnitOfWork, DependencyInjection.cs were added, completing Sprint 2 per sprint-board.md.

**How to apply:** Use when reviewing CP-02 follow-up PRs, Application-layer work that consumes IUnitOfWork/IRepository, or CP-03 Infrastructure work that reuses these conventions.

## All prior phase1 bugs confirmed FIXED
- `ApplicationUser.DomainUser` is now `User DomainUser { get; set; } = null!;` (non-nullable) — correct
- `CarePathDbContext.OnConfiguring` calls `base.OnConfiguring` then `AddInterceptors` — correct order, no warnings
- `WebApi.csproj` now has RootNamespace/AssemblyName — resolved
- CP-03 Transitions DbSets confirmed NOT mapped in CarePathDbContext (explicit comment explains why) — `MigrationShapeTests` guards this via text assertions on the generated migration file (`NotContain("Transition")`, `NotContain("Discharge")`)

## New findings (Phase 2) — none rise to Critical/HIPAA-breaking
- **Improvement**: `User.Address` is `HasMaxLength(200)` in `UserConfiguration.cs:23`, but CLAUDE.md's documented convention is `Address=500`. Real deviation from the enforced string-length spec (not caught by any test).
- **Improvement**: `Invoice.InvoiceNumber` is `HasMaxLength(50)` in `InvoiceConfiguration.cs:19`, but CLAUDE.md specifies `InvoiceNumber=20`. Format used is `INV-YYYYMMDD-XXXX` (17 chars) so 20 would suffice; 50 is just spec drift, not a functional bug. `EntityConfigurationTests` asserts the wrong number (50) as "expected," locking in the drift.
- **Improvement**: `CreatedBy`/`UpdatedBy` max length is inconsistent across configs — Identity entities (User, Caregiver, CaregiverCertification, Client) use 256; Scheduling/Billing/Clinical entities (Shift, VisitNote, VisitPhoto, CarePlan, Invoice, InvoiceLineItem, Payment) use 100. `AuditableEntityInterceptor.GetCurrentActor()` falls back through `ClaimTypes.NameIdentifier` → `sub` → `Identity.Name` → `"System"`; the first two are normally GUIDs (fits in 100) but `Identity.Name` could be an email (up to 256) which would throw on SaveChanges against a 100-length column. Standardize to 256 everywhere defensively.
- **Improvement**: `Repository<T>.GetPagedAsync` (`Infrastructure/Persistence/Repositories/Repository.cs:100-104`) hardcodes `.OrderBy(entity => entity.Id)` — deterministic pagination but not business-meaningful ordering (e.g., paging Shifts by Guid doesn't sort by ScheduledStartTime). CLAUDE.md calls out `GetPagedAsync` specifically for Shift/VisitNote, where callers will want date-based ordering. Consider an optional `orderBy` expression parameter or document the limitation clearly and expect concrete repository overrides for meaningful sort orders.
- **Nitpick**: `services.AddScoped(typeof(IRepository<>), typeof(Repository<>))` in `DependencyInjection.cs:82` is registered alongside `IUnitOfWork`, but `UnitOfWork` constructs its own `Repository<T>` instances internally rather than resolving `IRepository<T>` from DI. The generic registration is only useful for read-only consumers that never call `SaveChangesAsync` (which lives only on `IUnitOfWork`). Consider removing it to avoid two ways to get the same repository, per the project's "simplicity first" principle — or document that write flows must go through `IUnitOfWork`.
- **Nitpick**: `Repository<T>.UpdateAsync`/`DeleteAsync` call `_dbSet.Update(entity)` unconditionally, which marks all scalar properties Modified even if the entity was already tracked and only one field changed — produces full-column UPDATE statements. Acceptable trade-off for a generic repository; not a blocker.
- **Nitpick**: `WebApi/Program.cs` calls `app.UseAuthorization()` with `AddIdentity(...)` registered but no explicit `AddAuthorization()`/authentication scheme (cookie or JWT) configured yet, and no `app.UseAuthentication()` before `UseAuthorization()`. Not a bug today (no `[Authorize]` attributes exist yet), but flag for the upcoming auth/CP-03 task so it isn't forgotten.

## Excellent patterns worth reusing/citing in future reviews
- `CarePathDbContextSeed.SeedAsync` — dev-only guard (`environment.IsDevelopment()`), password sourced from config with fail-closed exception if missing, idempotent (re-running doesn't duplicate rows), and explicitly reactivates soft-deleted synthetic rows in the correct order: `EnsureDomainUserAsync` persists the domain-user undelete via `SaveChangesAsync` BEFORE `EnsureIdentityUserAsync` runs its `UserManager.FindByEmailAsync` lookup. This ordering matters because `ApplicationUserConfiguration.HasQueryFilter(user => !user.DomainUser.IsDeleted)` would otherwise hide the Identity row and cause a duplicate-key insert. Captured correctly in `_specs/lessons.md` under "Save resurrected soft-deleted principal rows before Identity lookups."
- `MigrationShapeTests` reads the generated migration `.cs` file as raw text and regex-asserts FK `Restrict` behavior + absence of `DeleteData`/`TRUNCATE`/PHI raw-content columns — a lightweight, DB-free way to guard schema-level HIPAA invariants in CI.
- `EntityConfigurationTests` builds the EF model via `UseSqlServer(...)` with a fake connection string and never opens a connection (`context.Model` triggers `OnModelCreating` without hitting the DB) — correct pattern for pure metadata assertions (max lengths, precision, delete behavior, query filters, Ignore'd computed properties) without needing SQL Server or InMemory.
- `AuditableEntityInterceptor` is injected into `CarePathDbContext` via constructor + `OnConfiguring` override (not via `AddDbContext(options => options.AddInterceptors(...))`), which is the correct/documented EF Core pattern when an interceptor needs scoped DI dependencies (`IHttpContextAccessor`) that aren't available at `AddDbContext` configuration time.
- All PHI foreign keys use `DeleteBehavior.Restrict`; only genuinely non-PHI ASP.NET Identity join tables (AspNetUserRoles, AspNetUserClaims, AspNetUserLogins, AspNetUserTokens, AspNetRoleClaims) use `Cascade` — correct and verified directly in the generated migration.
- `Database.MigrateAsync()` + seed only run inside `if (app.Environment.IsDevelopment())` in `Program.cs` — avoids auto-migration races/surprises in Staging/Production.
