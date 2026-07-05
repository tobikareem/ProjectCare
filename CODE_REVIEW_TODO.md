# Code Review TO-DO — CP-02 Infrastructure/EF Core (`feature/dispatch`)

Source: `dotnet-code-reviewer` review of staged changes, 2026-07-04.
Review result: no critical issues; build 0 warnings; 289/289 tests passing.
Items below are the outstanding action plan — implement, then re-verify.

## Fixes (spec conformance)

- [ ] **Fix `User.Address` max length 200 → 500**
  `Infrastructure/Persistence/Configurations/Identity/UserConfiguration.cs:23` — CLAUDE.md convention says `Address=500`.
- [ ] **Fix `Invoice.InvoiceNumber` max length 50 → 20**
  `Infrastructure/Persistence/Configurations/Billing/InvoiceConfiguration.cs:19` — CLAUDE.md says `InvoiceNumber=20`; documented format `INV-YYYYMMDD-XXXX` is 17 chars.
  - [ ] Update the `.GetMaxLength().Should().Be(50)` assertion in `Infrastructure.Tests/Persistence/EntityConfigurationTests.cs:71` to expect 20.
  - [ ] *(Alternative if 50 was intentional headroom: update CLAUDE.md instead — confirm with owner first.)*
- [ ] **Standardize `CreatedBy`/`UpdatedBy` to `HasMaxLength(256)`** in the seven configs currently at 100: Shift, VisitNote, VisitPhoto, CarePlan, Invoice, InvoiceLineItem, Payment.
  Reason: audit interceptor fallback `user?.Identity?.Name` can be an email (up to 256 chars) → `SaveChangesAsync` truncation exception once real auth claims flow.
- [ ] **Regenerate the `InitialCreate` migration + snapshot** after the column-length changes (migration is uncommitted, so drop and re-add is clean), then re-verify `MigrationShapeTests`.

## Design decisions (need a call before/at Application layer)

- [ ] **`Repository<T>.GetPagedAsync` ordering** (`Infrastructure/Persistence/Repositories/Repository.cs:100-104`) — currently orders by `Id` (Guid = effectively random order). Add an optional `orderBy` parameter (touches `IRepository<T>` in Domain too) or document that ordering is decided when Shift/VisitNote list views land.
- [ ] **Standalone `IRepository<T>` DI registration** (`Infrastructure/DependencyInjection.cs:82`) — redundant alongside `IUnitOfWork` (which builds its own repositories). Remove per simplicity-first, unless a direct-injection consumer is planned.

## Deferred (not bugs today — do not forget)

- [ ] **`WebApi/Program.cs:34`** — `UseAuthorization()` present with no `UseAuthentication()`/auth scheme. Must be wired before the first `[Authorize]` endpoint (CP-03/auth work).

## Verification (after implementing)

- [ ] `dotnet build CarePath.sln` — zero warnings
- [ ] `dotnet test CarePath.sln` — all passing
- [ ] Re-run `/code-review` on the updated diff
