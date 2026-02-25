---
description: "Handle the full EF Core migration workflow — generate, review, validate, and apply migrations. Use when adding or modifying entities, changing EF Core configurations, updating the database schema, or when the user says 'migration', 'migrate', 'database update', 'add migration', 'schema change', or references EF Core migrations."
allowed-tools: Read, Write, Edit, Glob, Grep, Bash(dotnet ef *), Bash(dotnet build *), Bash(dotnet test *), Bash(git diff *), Bash(git status *), Bash(ls *), Task
---

## Purpose

EF Core migrations in CarePath Health need extra care because the database contains PHI (Protected Health Information) and the schema supports a production healthcare business. A bad migration can cause data loss, break HIPAA compliance, or take down the system. This command handles the full migration lifecycle: pre-flight checks, generation, review, validation, and application.

---

## Before You Start

1. **Read `_specs/lessons.md`** — Check for any migration-related lessons from past mistakes.

2. **Verify Infrastructure project exists** — The CarePath Architecture requires an `Infrastructure` project for EF Core. Check if it exists:
   ```bash
   ls Infrastructure/Infrastructure.csproj
   ```
   If the Infrastructure project doesn't exist yet, it must be created first — migrations require a DbContext, entity configurations, and a registered connection string. The Infrastructure project should contain:
   - `Persistence/CarePathDbContext.cs` — The EF Core DbContext with all `DbSet<>` properties
   - `Persistence/Configurations/` — Entity type configurations using Fluent API
   - `Persistence/Repositories/` — Repository implementations
   - `Persistence/Migrations/` — Auto-generated migration files
   - `DependencyInjection.cs` — Service registration

   If it doesn't exist, stop here and create the Infrastructure layer first (see the relevant spec in `_specs/`).

3. **Verify the build** — The project must compile cleanly before generating a migration:
   ```bash
   dotnet build CarePath.sln
   ```
   If the build fails, fix errors first. EF Core cannot generate migrations from a broken build.

4. **Check for pending migrations** — See if there are unapplied migrations already:
   ```bash
   dotnet ef migrations list --project Infrastructure --startup-project WebApi
   ```
   If there are pending migrations, apply them first (or determine if they should be removed).

---

## Step 1: Pre-Flight Checks

Before generating a migration, verify what's changed:

### Identify Schema Changes
```bash
git diff --name-only | grep -E "(Entities|Configurations|DbContext)"
```

Review the actual changes:
- **New entities** — Need new `DbSet<>` in DbContext and new `IEntityTypeConfiguration<>`
- **Modified entities** — Property additions, type changes, relationship changes
- **New/modified configurations** — Fluent API changes
- **DbContext changes** — New DbSets, OnModelCreating changes

### Verify Entity Conventions (from CLAUDE.md)
For every changed entity, confirm:
- [ ] Inherits from `BaseEntity`
- [ ] Primary key is `Guid` (never `int`)
- [ ] All DateTime properties use `DateTime.UtcNow` (not `DateTime.Now`)
- [ ] Has `IsDeleted` support (inherited from BaseEntity)
- [ ] Nullable reference types are respected (no `!` suppressions without comments)
- [ ] Navigation properties are properly defined

### Verify Configuration Conventions
For every entity configuration, confirm:
- [ ] Uses Fluent API (not data annotations)
- [ ] Table name is explicit (`.ToTable("TableName")`)
- [ ] Primary key configured (`.HasKey(x => x.Id)`)
- [ ] Indexes on foreign keys and frequently queried columns
- [ ] Cascade delete behavior is intentional
- [ ] Global query filter for `IsDeleted`: `.HasQueryFilter(x => !x.IsDeleted)`
- [ ] Owned entities (value objects) configured with `.OwnsOne()`
- [ ] Precision configured for decimal properties (rates, amounts)

---

## Step 2: Generate the Migration

### Naming Convention
Migration names should be descriptive and follow the pattern:
- `Add<EntityName>` — For new entities (e.g., `AddCheckInRecord`)
- `Update<EntityName><Change>` — For modifications (e.g., `UpdateShiftAddGpsFields`)
- `Add<Feature>` — For multi-entity features (e.g., `AddGpsCheckInTracking`)
- `Remove<Thing>` — For removals (e.g., `RemoveDeprecatedStatusColumn`)

### Generate Command
```bash
dotnet ef migrations add <MigrationName> --project Infrastructure --startup-project WebApi
```

If the project paths are different (check your solution structure), adjust accordingly. The `--project` flag points to the Infrastructure project (where migrations live), and `--startup-project` points to the WebApi project (which has the connection string and DI configuration).

### Verify Generation Succeeded
Check that the migration files were created:
```bash
ls Infrastructure/Migrations/ | tail -5
```

You should see three files:
- `YYYYMMDDHHMMSS_<MigrationName>.cs` — The migration (Up/Down methods)
- `YYYYMMDDHHMMSS_<MigrationName>.Designer.cs` — Snapshot metadata
- `CarePathDbContextModelSnapshot.cs` — Updated model snapshot

---

## Step 3: Review the Migration

This is the most important step. Read the generated migration file carefully.

### Read the Migration
Read the main migration file (the one without `.Designer.cs`):

### Review Checklist

#### Up Method (applying the migration):
- [ ] **New tables**: Correct column names, types, and nullability?
- [ ] **Column types match C# types**: `Guid` -> `uniqueidentifier`, `DateTime` -> `datetime2`, `decimal` -> `decimal(precision, scale)`
- [ ] **Decimal precision**: Bill rates, pay rates, amounts should have explicit precision (e.g., `precision: 18, scale: 2`)
- [ ] **String lengths**: Are `nvarchar(max)` fields intentional? Should any have length limits?
- [ ] **Indexes**: Foreign keys and frequently-queried columns have indexes?
- [ ] **Foreign keys**: Correct relationships and cascade behavior? (Careful: cascade delete on PHI tables can violate data retention!)
- [ ] **Default values**: Any `HasDefaultValue()` or `HasDefaultValueSql()` correct?
- [ ] **No data loss**: Are there any column drops, type changes, or table renames that could lose data?
- [ ] **PHI tables**: No hard delete cascades on Client, CarePlan, VisitNote, VisitPhoto, Shift, CaregiverCertification

#### Down Method (rolling back):
- [ ] **Reversible**: Does the Down method correctly undo every change in Up?
- [ ] **Data preservation**: If Up adds a column, Down should drop it (not the whole table)
- [ ] **Index cleanup**: Indexes created in Up are dropped in Down?

### Common Migration Issues to Watch For

1. **Missing `IsDeleted` query filter** — If you see a new entity without a global query filter in its configuration, that's a bug. Soft-deleted records will appear in queries.

2. **Cascade delete on PHI** — EF Core defaults to cascade delete for required relationships. For PHI entities, this is dangerous:
   ```csharp
   // BAD - Deleting a client cascades to their care plans
   .OnDelete(DeleteBehavior.Cascade)

   // GOOD - Restrict or use soft delete
   .OnDelete(DeleteBehavior.Restrict)
   ```

3. **Missing precision on decimals** — Without explicit precision, EF Core uses `decimal(18,2)` by default. Verify this is appropriate for rates and amounts.

4. **Overly broad nvarchar(max)** — For columns like `CertificationNumber`, `PhoneNumber`, `ZipCode`, use explicit lengths.

5. **Forgot to add DbSet** — Entity configuration exists but `DbSet<Entity>` is missing from DbContext.

---

## Step 4: Validate the Migration

### Build After Migration
```bash
dotnet build CarePath.sln
```
The migration must not introduce build errors.

### Run Existing Tests
```bash
dotnet test CarePath.sln
```
Existing tests should still pass. If tests use an in-memory database or test containers, the migration will be applied automatically.

### Check for Idempotency
If applied to an existing database, the migration should not fail:
```bash
dotnet ef migrations script --project Infrastructure --startup-project WebApi --idempotent
```
This generates a SQL script with `IF NOT EXISTS` guards. Review it for correctness.

### Generate SQL Script for Review
For production deployments, always generate and review the raw SQL:
```bash
dotnet ef migrations script <PreviousMigration> <NewMigration> --project Infrastructure --startup-project WebApi -o migration-script.sql
```
Read the generated SQL to verify it matches expectations.

---

## Step 5: Apply the Migration

### Development/Local
```bash
dotnet ef database update --project Infrastructure --startup-project WebApi
```

### Verify Application
After applying:
```bash
dotnet ef migrations list --project Infrastructure --startup-project WebApi
```
The new migration should show as applied (no `(Pending)` marker).

### Smoke Test
Run the API and verify the affected endpoints work:
```bash
dotnet run --project WebApi
```
Test with a quick request to an affected endpoint (use curl or the Swagger UI at `/swagger`).

---

## Step 6: Document the Migration

Add a brief note about the migration to the relevant spec or create a summary:

```markdown
### Migration: <MigrationName>
- **Date**: YYYY-MM-DD
- **Changes**: [brief description]
- **Tables affected**: [list]
- **New indexes**: [list]
- **Rollback tested**: Yes/No
```

---

## Rollback Procedure

If the migration needs to be reverted:

### Revert to Previous Migration
```bash
dotnet ef database update <PreviousMigrationName> --project Infrastructure --startup-project WebApi
```

### Remove the Migration Files
```bash
dotnet ef migrations remove --project Infrastructure --startup-project WebApi
```
This removes the last migration file. Only use if the migration hasn't been applied to other environments.

### Emergency Rollback (Production)
If already deployed to production:
1. Use the idempotent script's Down method
2. Or generate a targeted rollback script:
   ```bash
   dotnet ef migrations script <NewMigration> <PreviousMigration> --project Infrastructure --startup-project WebApi -o rollback-script.sql
   ```
3. Review and apply the rollback script manually

---

## Migration Anti-Patterns for CarePath

These are mistakes that have been made before (or are easy to make) in healthcare EF Core projects:

1. **Renaming columns instead of adding new ones** — In production with data, a rename is actually a drop + add (data loss). Add the new column, migrate data, then drop the old one in a separate migration.

2. **Changing column types with data** — Changing `nvarchar` to `int` will fail if the column has data. Add a new column, convert data, drop the old column.

3. **Multiple migrations in one PR** — Keep one migration per logical change. Multiple migrations in one PR make rollbacks harder.

4. **Not testing Down method** — Always verify the rollback works by applying and then reverting the migration locally.

5. **Forgetting seed data** — If the migration adds a lookup table (e.g., new enum values), include seed data in the migration or the configuration.

6. **Breaking the model snapshot** — If you have merge conflicts in `CarePathDbContextModelSnapshot.cs`, don't manually resolve them. Instead, remove both conflicting migrations and regenerate from the merged code.

7. **Migrations that depend on runtime data** — Migrations should be deterministic. Don't reference services, configs, or runtime values in migration code.
