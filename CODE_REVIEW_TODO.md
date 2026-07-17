# Code Review TO-DO

Running action plan from code/architecture reviews. Check items off as they land;
add a dated section per review rather than rewriting history.

---

## Architecture assessment action plan — 2026-07-17

Source: Claude's whole-codebase assessment after the S6-TASK-036/038/039 and D-S6-14
reviews (architecture 9/10, implementation correctness 7.5/10). These three items are
what closes the gap — the dominant bug class is "the second copy of a rule drifting
from the first."

### 1. Collapse duplicated authorization logic (highest leverage)

Object-level access rules currently exist twice: once in
`Application/Auth/Sprint4ObjectAuthorizationService` (behind `IIdorGuard`) and again as
hand-written `EnsureCan*` methods inside each Application service. Both recent
authorization bugs lived in exactly this duplication. Make the per-service checks
delegate to the single `IObjectAuthorizationService` so each rule can only exist once.

- [ ] Fix `EnsureCanWriteClientClinicalRecordAsync` denying Clinicians that the
      controller + IDOR guard authorize on care-plan create/update
      (`Application/Clients/Services/ClientOperationsService.cs` ~line 475; also
      ticketed as a spawned task 2026-07-17) — add missing
      `CreateCarePlanAsync`/`UpdateCarePlanAsync` tests while there.
- [ ] Remove the dead caregiver self-access branch in
      `Sprint4ObjectAuthorizationService.AuthorizeCaregiverAsync` (~lines 237-248) —
      unreachable since the S6-TASK-039 re-gate, and a latent trap if a route ever
      re-adds the Caregiver role.
- [ ] Refactor `ClientOperationsService.EnsureCanReadClientAsync` /
      `EnsureCanWriteClientClinicalRecordAsync`, `ShiftOperationsService.EnsureCanReadShiftAsync`,
      and `CaregiverOperationsService.CanReadCaregiverDetail` to delegate to
      `IObjectAuthorizationService` (keep the service-layer call as the second
      enforcement layer — the point is one rule source, not one enforcement point).
- [ ] Verify `TransitionsService` role/scoping helpers against the same source of truth
      or document why its clinician/care-team scoping is intentionally distinct (D-S5-3).
- [ ] Tests: for each refactored service, denial tests must fail if the delegate is
      bypassed (mock `IObjectAuthorizationService` denied → service throws before data access).

### 2. Decide and implement audit ownership (before the table gets expensive)

Every guarded read currently writes two near-identical `PhiAuditEntry` rows: one from
`IdorGuard.EnsureAuthorizedAsync` on success, one from the Application service. Over the
6-year retention window this doubles the audit table. Recommended: guard owns success
read-audits for `{id}` routes; services audit only what the guard cannot see (per-row
list reads, writes, denials, internal high-risk field reads).

- [ ] Record the ownership decision (D-S7-x) on the sprint board.
- [ ] Remove the duplicate service-side read audit on guard-covered `{id}` routes
      (CarePlan detail, Client detail, Shift detail, Caregiver detail, Transitions
      document/plan/instruction/escalation routes).
- [ ] Keep/verify: per-row audits on paged lists, write audits, `AccessDenied` audits,
      and internal `RawContent`/`SourceText`/`ResponsesJson` read audits (lessons.md rule).
- [ ] Update audit-verifying tests to assert exactly ONE read entry per logical read.

### 3. Close the write-path and browser-side test gaps

Read paths are densely tested; writes and the browser are not.

- [ ] Application: add missing write-path tests — care-plan create/update (zero today),
      client create/update guard behavior, and any other command handler without a
      denial + rollback + audit test (sweep `Application.Tests` per service).
- [ ] Web: land S6-TASK-040 (bUnit suite incl. PHI-exposure markup assertions) and
      S6-TASK-050 (browser PHI safety review) — tracked on the Sprint 6 board.
- [ ] Optional stretch: scan-capped coverage matches (`CandidateScanMaxPages`) still
      have no client-visible "scan capped" indicator — needs a small contract addition
      + decision if coordinators ever ask why matches are missing on huge rosters.

Done when: all three sections checked, `dotnet build` 0 warnings, full `dotnet test`
green, reviewer pass on the refactor diff, and re-scored.

---

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

- [x] **`Repository<T>.GetPagedAsync` ordering** — resolved 2026-07-17: ordered overloads added (`GetPagedAsync(predicate, orderBy, thenBy, ...)` for the admin user list, generic `GetPagedDescendingAsync<TKey>` for care-plan lists), both with an `Id` tiebreaker for deterministic paging; covered by `Infrastructure.Tests/Repositories/RepositoryTests`.
- [ ] **Standalone `IRepository<T>` DI registration** (`Infrastructure/DependencyInjection.cs:82`) — redundant alongside `IUnitOfWork` (which builds its own repositories). Remove per simplicity-first, unless a direct-injection consumer is planned.

## Deferred (not bugs today — do not forget)

- [x] **`WebApi/Program.cs`** — resolved by S6-TASK-010 (D-S6-2 auth): `UseAuthentication()` + `UseAuthorization()` both wired (currently lines 115-116) with the JWT scheme and login/refresh endpoints live.

## Verification (after implementing)

- [ ] `dotnet build CarePath.sln` — zero warnings
- [ ] `dotnet test CarePath.sln` — all passing
- [ ] Re-run `/code-review` on the updated diff
