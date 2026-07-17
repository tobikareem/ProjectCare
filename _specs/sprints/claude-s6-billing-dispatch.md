# Claude Dispatch Prompt — Sprint 6 Billing Platform and Typed Client

Copy the prompt below into Claude's dedicated billing task/branch.

---

You are implementing the **contracts, backend, migration, and typed-client** portion of CarePath
Sprint 6 decision D-S6-18. Read
`AGENTS.md`, `CLAUDE.md`, `_specs/lessons.md`, and D-S6-18 plus S6-TASK-056/057/061/062/064 in
`_specs/sprints/sprint-06-tasks.md` before changing code. The approved UI states are
`page-billing-generate`, `page-billing-preview`, and `page-billing-reconciliation` in
`Documentation/Wireframes/carepath-wireframe.html`; consume their requirements but do not edit them.

## Your assigned tasks

Implement **S6-TASK-056, S6-TASK-064, S6-TASK-057, S6-TASK-061, S6-TASK-062, and S6-TASK-058**
in dependency order. Do not mark task rows Done; return evidence to Codex/Tobi for board updates.

You exclusively own these paths for this dispatch:

- `CarePath.Contracts/Billing/**`
- `CarePath.Client/Api/BillingClient.cs` and one new dedicated Billing client route-alignment test
- `Domain/Entities/Billing/**` and new billing enum files only
- `Application/Abstractions/Billing/**`
- `Application/Billing/**`
- `Infrastructure/Billing/**`
- billing EF configuration, required `CarePathDbContext`/DI registration, one new migration and model snapshot
- `WebApi/Controllers/InvoicesController.cs`
- new uniquely named backend billing tests under `Domain.Tests`, `Application.Tests`, and
  `Infrastructure.Tests`

Do **not** edit:

- `Documentation/Wireframes/**`
- `_specs/**` or sprint boards
- `CarePath.Web/**`
- `CarePath.Web.Tests/**`

Codex owns those files. If a required change falls outside your ownership, report the exact change
as a handoff instead of editing it. Preserve all unrelated dirty-worktree changes.

## Locked business behavior

1. A facility is the existing Client billing account with `ServiceType.FacilityStaffing`; do not add
   a Facility entity. Client `ServiceType` and `HourlyBillRate` may supply UI defaults, but shift data
   and server eligibility remain authoritative.
2. `POST /api/invoices/preview` is Admin/Coordinator only and body-based. It accepts client, service
   type, half-open UTC period, page number and bounded page size. It returns page rows plus aggregates
   across the entire matching set and an opaque expiring preview token.
3. Each eligible row may return only: service date/window, billable hours, bill rate, rounded line
   total, caregiver display name, and professional credentials valid on the service date. Credential
   labels are the deterministic sorted set of RN/LPN/GNA/CNA/HHA/CRMA; omit training credentials and
   numbers; use `Caregiver` when none qualify.
4. Explicitly forbid caregiver ID/contact/pay, cost/margin, GPS, notes, visit-note/clinical content,
   diagnosis and credential numbers in preview/reconciliation DTOs. Add reflection denylist tests.
5. One shared SQL-backed eligibility implementation serves preview, create and reconciliation.
   Apply exactly one reason per shift in this precedence: `AlreadyInvoiced`,
   `NonBillableResolved`, `CancelledOrNoShow`, `NotCompleted`, `MissingActualTime`,
   `InvalidBillableTime`, `MissingBillRate`, `Eligible`.
6. A delivered service is revenue-at-risk when unresolved and not eligible; scheduled/in-progress
   becomes aged risk only 24 hours after scheduled end. `AlreadyInvoiced` and
   `NonBillableResolved` are informational, not leakage totals.
7. Use start-inclusive/end-exclusive UTC periods. Calculate existing billable minutes after breaks.
   Round each currency line to two decimals with `MidpointRounding.AwayFromZero`; subtotal is the sum
   of rounded lines.
8. Preview token binds client, service line, period, eligible Shift IDs, relevant shift updated/time/
   rate inputs and totals. Extend `CreateInvoiceRequest` with the token. Expired/tampered/reused or
   changed previews return sanitized `409 invoice.preview_stale`; never silently create a changed
   total. Empty/all-excluded preview cannot generate.
9. Create re-evaluates eligibility inside the transaction. Add a unique SQL Server filtered index on
   `InvoiceLineItems.ShiftId IS NOT NULL` without excluding soft-deleted rows, so historical links
   still block rebilling. Preflight duplicate ShiftIds and fail closed; do not mutate/delete data.
10. Add append-only `BillingReconciliationResolution : BaseEntity`, linked to Shift with Restrict
    delete, safe reason enum, resolver user ID, UTC resolved timestamp, optional bounded PHI-free
    note, and supersession/reopen linkage. Never overwrite/delete prior decisions.
11. Add body-based, Admin/Coordinator-only reconciliation search plus guarded detail,
    resolve/reopen, and time-correction commands beneath `/api/invoices/reconciliation`. Search is
    server-paged, maximum 92-day range, KPIs cover the entire filter, and ordering is oldest service
    date then Shift ID. Already-invoiced detail returns only the owning Invoice ID for navigation.
12. Missing-time correction records actual start/end, break and a safe reason code through a
    dedicated audited command. Bill-rate correction continues through the existing guarded shift
    update route; return that route as the corrective destination rather than duplicating it.
13. Enforce role plus Client/Shift/Caregiver/Invoice object authorization before business-state
    disclosure. All PHI reads/mutations emit ID-only audit events. No display values, token contents,
    request serialization or exception detail may enter logs/errors.
14. After backend routes are frozen, add typed `BillingClient` methods for preview, create with token,
    reconciliation search/detail, resolve/reopen and time correction. Pin every verb/body/template in
    a new uniquely named route-alignment test; do not add raw HTTP calls to Web.

Use exact route/error names consistently and pin them in controller-contract tests. If the existing
architecture cannot safely implement an opaque preview token or SQL transaction boundary, stop and
report the concrete blocker before substituting another design.

## Required tests and evidence

- Contract allowlist/denylist, enum and mapper tests.
- Every role, verb and route template; IDOR and identical PHI-denial behavior.
- Every exclusion and precedence collision; 24-hour boundary; period boundaries; invalid times,
  breaks/rates; qualification labels on service date; stable paging and full-filter aggregates.
- Currency rounding and preview token expiry/tampering/cross-client/staleness.
- Exact-period and overlapping-period duplicates, soft-deleted historical line, and two concurrent
  creates against SQL Server—not mocks alone.
- Append-only resolve/reopen history, record preservation, correction auditing, owning-invoice link,
  bounded-range/page-size guards and deterministic query tests.
- Migration duplicate preflight, idempotent script generation, forward apply, and model validation.

Before handoff run:

```bash
dotnet build CarePath.sln
dotnet test CarePath.sln
```

Also run focused backend tests, migration validation, the repository .NET reviewer, and full
`hipaa-check`. Fix all critical/high findings within your owned paths.

## Handoff to Codex

Return a frozen manifest containing:

- every new/changed contract with exact fields and nullability;
- every route with verb, template, roles, body and response;
- all safe error codes and HTTP statuses;
- preview-token lifetime/semantics;
- reconciliation reason enum and precedence;
- migration name/index definitions;
- exact build/test/reviewer/HIPAA commands and results;
- any required Codex-only change, clearly separated.

State explicitly that Contracts and routes are frozen. Do not begin frontend work and do not mark
the Sprint board complete.
