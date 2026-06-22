---
name: "migration"
description: "Review, generate, validate, or apply EF Core migrations for CarePath. Use for schema changes, migration requests, database updates, DbContext/configuration changes, or EF Core migration review."
---

# migration

Use this workflow for CarePath EF Core migration work. Treat schema changes as high risk because future tables contain PHI and financial records.

## Authorization Boundary

- Assessment, diff inspection, migration review, builds, tests, and SQL generation are read-only or workspace-local verification.
- Generate a migration only when the user explicitly asks to add/create/generate it or an approved implementation task requires it.
- Apply a migration to a database only when the user explicitly asks for a database update.
- Never infer permission to target production or another shared environment.

## Before Starting

1. Read `AGENTS.md`, `_specs/lessons.md`, and the applicable approved requirements, design, and task specs.
2. Confirm `Infrastructure/Infrastructure.csproj`, a DbContext, entity configurations, DI registration, and a startup project exist.
3. If Infrastructure does not exist, stop and report that migrations are premature. Do not create an ad hoc DbContext.
4. Run:

   ```powershell
   dotnet build CarePath.sln
   dotnet ef migrations list --project Infrastructure --startup-project WebApi
   ```

5. Inspect changed entities, configurations, DbContext code, and existing migrations.

## Pre-flight Review

For each affected entity, verify:

- It inherits `BaseEntity`.
- The primary key is `Guid`.
- Timestamps are UTC.
- Nullability is intentional.
- Navigation properties and aggregate boundaries are clear.
- PHI entities retain soft-delete and audit requirements.

For each EF configuration, verify:

- Fluent API is used.
- Table and column names are intentional.
- Keys, foreign keys, indexes, uniqueness, and delete behavior are explicit.
- `HasQueryFilter(x => !x.IsDeleted)` is applied consistently.
- Decimal precision is explicit for rates and money.
- Bounded strings have appropriate maximum lengths.
- Large text columns are limited to fields that genuinely need them.
- UTC `DateTime` storage behavior is documented and tested.

## Generate

Use a descriptive PascalCase name:

- `Add<Entity>`
- `Update<Entity><Change>`
- `Add<Feature>`
- `Remove<DeprecatedThing>`

Generate with:

```powershell
dotnet ef migrations add <MigrationName> --project Infrastructure --startup-project WebApi
```

Expect the migration, designer file, and model snapshot to change. Keep one logical schema change per migration.

## Review the Generated Migration

Inspect `Up`, `Down`, designer metadata, and the model snapshot.

Check:

- SQL types and nullability match the C# model.
- Money/rates use the approved precision and scale.
- Foreign keys and common query paths have indexes.
- Required relationships do not introduce accidental cascade deletes.
- Client, CarePlan, Shift, VisitNote, VisitPhoto, and CaregiverCertification data cannot be hard-deleted through cascades.
- Renames use explicit rename operations rather than destructive drop/add behavior.
- Type changes preserve existing data.
- New required columns on populated tables have a safe backfill strategy.
- `Down` reverses `Up` as far as the data model permits.
- Seed/data migrations are deterministic and contain no runtime service calls.

For PHI, flag:

- `DeleteBehavior.Cascade`
- raw `DELETE` or `TRUNCATE`
- public media URLs
- unencrypted file exports
- columns likely to leak into logs or URLs
- retention-destructive operations

## Validate

Run:

```powershell
dotnet build CarePath.sln
dotnet test CarePath.sln
dotnet ef migrations script --project Infrastructure --startup-project WebApi --idempotent
```

For release review, generate the exact forward SQL:

```powershell
dotnet ef migrations script <PreviousMigration> <NewMigration> --project Infrastructure --startup-project WebApi -o migration-script.sql
```

Review the SQL for destructive operations, table locks, unbounded data rewrites, missing transaction protection, and incorrect defaults.

## Apply

Only after explicit authorization:

```powershell
dotnet ef database update --project Infrastructure --startup-project WebApi
dotnet ef migrations list --project Infrastructure --startup-project WebApi
```

Then run the API or relevant integration tests and smoke-test the affected behavior.

## Rollback

Before application, identify the previous migration and document rollback:

```powershell
dotnet ef database update <PreviousMigration> --project Infrastructure --startup-project WebApi
```

Use `dotnet ef migrations remove` only for the latest migration and only when it has not been deployed elsewhere.

For a deployed migration, generate and review a targeted rollback script. Do not assume rollback is lossless when columns or tables were removed.

## Report

Summarize:

- Migration name and purpose
- Files changed
- Tables, columns, indexes, and constraints affected
- PHI/data-loss risks
- Build and test results
- SQL review result
- Whether the migration was generated, applied, or only reviewed
- Rollback procedure and any irreversible behavior
