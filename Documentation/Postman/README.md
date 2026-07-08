# CarePath API — Postman Seed & Smoke-Test Kit

Seeds a **fresh dev database** with the wireframe's demo cast and smoke-tests every write
endpoint plus the key reads the Blazor web app consumes. After a run, every page of
`CarePath.Web` (Overview, Schedule, Caregivers, Clients, Visit notes, Billing, Analytics,
Compliance, Transitions) is backed by real API data.

## Files

| File | Purpose |
|---|---|
| `CarePath-API.postman_collection.json` | The collection — 8 folders, run top-to-bottom |
| `CarePath-Local.postman_environment.json` | `baseUrl` + `devPassword` (fill locally) |

## Prerequisites

1. **API running**: `dotnet run --project WebApi` (listens on `http://localhost:5240`; the
   Development environment auto-migrates and seeds the five staff accounts).
2. **Dev password**: the same secret the seeder reads from user secrets. Set it as the
   `devPassword` value in the imported environment. **Never commit it.**
3. **Fresh database** (for re-runs): the collection creates users with fixed emails
   (unique-constrained), so a second run against the same DB will fail on the create steps.
   Reset first:
   ```bash
   dotnet ef database drop --startup-project WebApi --force
   dotnet run --project WebApi   # re-migrates + re-seeds staff accounts on startup
   ```

## How to run

1. Postman → **Import** → select both JSON files.
2. Select the **CarePath — Local Dev** environment and fill in `devPassword`.
3. Open the collection → **Run** (Collection Runner) → keep the default order → **Run**.
   No per-request delay needed; the check-in steps deliberately wait ~2.5 s each so completed
   shifts accrue real billable seconds.
4. Green run ≈ 90 assertions. Then log into the web app (`dotnet run --project CarePath.Web`,
   sign in as `admin@carepath.local`) — every page should now show data.

CLI alternative: `newman run CarePath-API.postman_collection.json -e CarePath-Local.postman_environment.json --env-var devPassword=<your dev password>`

## What gets created (wireframe cast)

| Who | Role in the data |
|---|---|
| Amara Williams | CNA (W-2). CNA expiring in ~25 days, CPR valid. Completed + in-progress shifts with Jordan |
| David Okafor | HHA (W-2), dementia-trained. Completed + upcoming shifts with Casey |
| Maria Johnson | LPN (W-2), nights. LPN valid, CPR expiring in ~20 days. Shifts with Beverly + Northside |
| Soo-Jin Park | RN (1099 contractor). Facility shifts at Harborview |
| Tunde Adeyemi | CNA + CPR both **expired** — compliance rows; scheduling him is the negative test (400 `caregiver.certification_expired`) |
| Jordan Mitchell | In-home client ($35/h). Care plan, shifts, invoice (past due date), full Transitions plan (High risk, warning-symptom escalation) |
| Casey Reyes | In-home client ($33/h). Care plan, shifts, invoice → **PartiallyPaid** |
| Beverly Thompson | In-home client ($38/h). Care plan, shifts, visit note with vitals, invoice → **Paid** |
| Harborview Center | Facility client ($80/h RN). Completed shift, invoice (Sent), plus the **open/unassigned** RN shift |
| Northside Rehab | Facility client ($60/h LPN nights). Completed night shift, invoice (past due date) |

All dates are computed **relative to the run time** (collection pre-request script), so
"expired" stays expired and "expiring soon" stays inside the 30-day window whenever you run it.
Client/caregiver accounts get a random per-run temporary password (collection variable
`tempPassword`) — generated at runtime, never committed; the run itself logs in as each
caregiver (check-in/out, visit notes) and as Jordan (patient check-in).

## Endpoint coverage

- **Auth**: login (admin/coordinator/clinician/caregivers/patient), refresh.
- **Caregivers**: create ×5, add certification ×8, expiring-certifications read.
- **Clients**: create ×5, care plans ×3.
- **Shifts**: create ×9, update (unassign → open shift), check-in ×6, check-out ×5,
  negative guard test (expired certification).
- **Visit notes**: create ×2 (assigned caregiver), detail read.
- **Billing**: invoice ×5 (both service lines), payment ×2 (full → Paid, half → PartiallyPaid),
  invoice list, **Admin margin summary + per-shift margins**.
- **Transitions**: discharge document → extraction → plan lookup → instruction review loop
  (approves every pending instruction) → activation (e-sign, High risk) → reminder →
  patient warning-symptom check-in → escalation list → coordinator acknowledgement →
  patient-facing view.

## Known API limitations the seed works around (PM flags)

1. **Invoice dollar amounts are small.** Line-item hours come from *actual* check-in/out
   timestamps, which the server sets to "now" — they cannot be backdated through the public
   API (by design). Completed shifts therefore have a few seconds of billable time; invoice
   balances are mostly the `taxAmount` set on each invoice. The wireframe's $3,840-style
   totals are not reproducible without a backend test-data endpoint.
2. **`InvoiceStatus.Overdue` is never set by the API.** Jordan's and Northside's invoices have
   past due dates but stay `Sent` — there is no overdue-transition job/read logic yet. UI
   counts keyed on the Overdue status will show 0.
3. **Open shifts** can't be created unassigned — `CreateShiftRequest.CaregiverId` is required.
   The seed creates then unassigns via `PUT /api/shifts/{id}` (`caregiverId: null`).
4. **Not covered**: access grants (needs a second Client-role user's UserId, not discoverable
   via API), visit-note photo upload (Sprint 7 secure-storage gates), admin users endpoints
   (S6-TASK-036 not yet implemented).

## One-off curl (login example)

```bash
curl -s http://localhost:5240/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@carepath.local","password":"<your dev password>"}'
```
