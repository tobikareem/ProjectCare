---
name: cp02-phase1-infrastructure-review
description: CP-02 Phase 1 (feature/dispatch) Infrastructure EF Core layer review findings — what shipped, what's missing, and critical patterns to enforce when remaining tasks are implemented
metadata:
  type: project
---

CP-02 Phase 1 Infrastructure changeset reviewed 2026-06-27 on feature/dispatch branch.

**Why:** Review was requested after initial implementation of Infrastructure project scaffolding, core EF Core infrastructure components, and tests.

**How to apply:** Use these notes when reviewing subsequent CP-02 tasks (entity configurations, Repository, UnitOfWork, DI, migrations, seed data).

## What shipped in this changeset

- `Infrastructure/Infrastructure.csproj` — correct project setup; `FrameworkReference Microsoft.AspNetCore.App`, CPM package refs, `GenerateDocumentationFile=true`, `TreatWarningsAsErrors=true`
- `Infrastructure/Identity/ApplicationUser.cs` — IdentityUser<Guid> with DomainUserId FK; **BUG: DomainUser is `User?` (nullable) but FK is required; must be `User DomainUser { get; set; } = null!;`**
- `Infrastructure/Persistence/CarePathDbContext.cs` — IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>; `DomainUsers` (not `Users` — avoids collision with Identity's Users property, CORRECT); includes CP-03 Transitions DbSets (out of CP-02 scope)
- `Infrastructure/Persistence/Configurations/Identity/ApplicationUserConfiguration.cs` — unique index + FK relationship with Restrict; **missing: `.HasDatabaseName()` on index, string length constraints, explicit `IsRequired()` on DomainUserId**
- `Infrastructure/Persistence/Converters/UtcDateTimeConverter.cs` — both `UtcDateTimeConverter` and `NullableUtcDateTimeConverter` in one file; **write path bug: `Unspecified` DateTimes treated as Local and shifted**
- `Infrastructure/Persistence/Interceptors/AuditableEntityInterceptor.cs` — overrides both sync and async SavingChanges (better than design spec); `??=` on CreatedBy correct; `entry.Property(nameof(BaseEntity.CreatedAt)).CurrentValue = now` for init-only property correct
- Test project: 3 test files covering converter, interceptor, DbContext; well-structured

## What's missing (spec deferred, not bugs)
- No entity configurations for 12 CP-02 entities (TASK-047 to TASK-058) — HIPAA risk: EF Core defaults to Cascade on required FKs
- No `Repository<T>` (TASK-059)
- No `UnitOfWork` (TASK-060)
- No `DependencyInjection.cs` (TASK-062)
- No migrations (TASK-065+)
- No seed data (TASK-068)

## Architecture decisions made (good)
- Centralized `ApplyBaseEntityConventions` applies UTC converters and soft-delete query filters to ALL BaseEntity descendants in one pass — better than per-entity config in design spec
- When entity configurations are written, they MUST NOT include `HasQueryFilter` or UTC converter calls — the convention runs AFTER `ApplyConfigurationsFromAssembly` and would overwrite them anyway
- `DbSet<T>` properties use expression-body `=> Set<T>()` pattern (cleaner than `null!` auto-props)
- Static readonly converter instances shared across context instances (efficient, correct)

## Key rules to enforce in future CP-02 entity configuration reviews
1. Each entity config MUST NOT include `HasQueryFilter` (centralized convention handles it)
2. Each entity config MUST NOT include per-property UTC converter (centralized convention handles it)
3. Each entity config MUST include `OnDelete(DeleteBehavior.Restrict)` for all PHI entity relationships
4. Each entity config MUST include `.Ignore()` for all computed properties (FullName, Age, BillableHours, GrossMargin, etc.)
5. `DomainUserId` on ApplicationUser: `DeleteBehavior.Restrict` is correct (not Cascade from design spec) — hard deletes never happen in this system
6. CP-03 Transitions entities must be removed from CarePathDbContext until CP-03 entity configurations are ready

## Critical test gaps to flag
- No test verifying CreatedBy is NOT overwritten on update (using different actors for Add vs Update)
- No test for UtcDateTimeConverter with `DateTimeKind.Unspecified` input
- No NullableConverter test for non-null DateTime? round-trip
