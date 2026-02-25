---
description: "Run a targeted HIPAA/PHI exposure review on new or modified endpoints, entities, or code changes. Use when adding API endpoints, creating entities that touch patient data, reviewing PRs, or anytime code interacts with Client, CarePlan, Shift, VisitNote, VisitPhoto, or CaregiverCertification data. Also use after implementing any spec to verify PHI safety before merging."
allowed-tools: Read, Write, Edit, Glob, Grep, Bash(dotnet build *), Bash(git diff *), Bash(git status *), Bash(git log *), Task
---

## Purpose

CarePath Health handles Protected Health Information (PHI) subject to HIPAA regulations. This command performs a focused review of code changes to catch PHI exposure risks before they reach production. It checks for the most common ways PHI leaks in .NET applications: logging, URLs, missing authorization, inadequate audit trails, and accidental data exposure in API responses.

---

## PHI Entities (Always Treat as Sensitive)

These entities contain Protected Health Information and require special handling:

| Entity | PHI Fields | Risk Level |
|--------|-----------|------------|
| **Client** | DateOfBirth, MedicalConditions, Allergies, InsuranceProvider, InsurancePolicyNumber, MedicaidNumber, EmergencyContactName, Address | Critical |
| **CarePlan** | Goals, Interventions, Notes, Description | Critical |
| **VisitNote** | ClientCondition, Concerns, Medications, BloodPressureSystolic, BloodPressureDiastolic, Temperature, HeartRate, Activities | Critical |
| **VisitPhoto** | PhotoUrl, Caption (may reference patient) | High |
| **Shift** | GPS coordinates (CheckInLatitude/Longitude — reveals client home location), Notes | High |
| **CaregiverCertification** | CertificationNumber (PII, not PHI, but still sensitive) | Medium |

---

## Step 1: Identify What to Review

Determine the scope of the review:

### If reviewing uncommitted changes:
```bash
git diff --name-only
git diff --staged --name-only
```

### If reviewing a specific feature or spec:
Read the relevant spec files and identify all files listed in the tasks breakdown.

### If reviewing a specific file or endpoint:
Focus on that file and trace its dependencies (what it calls, what calls it).

---

## Step 2: PHI in Logs Check

**Rule: No PHI in logs — never log patient names, DOB, diagnosis, SSN, or address strings.**

Search for logging statements that might contain PHI:

### What to look for:
- `_logger.Log*` or `Log.` calls that interpolate PHI entity properties
- `Console.Write*` calls with patient data
- Exception messages that include PHI (e.g., `throw new Exception($"Client {client.Name} not found")`)
- Serilog structured logging with PHI fields (e.g., `{@Client}` which serializes the entire object)

### Safe patterns:
```csharp
// GOOD - Log IDs only
_logger.LogInformation("Shift {ShiftId} checked in by caregiver {CaregiverId}", shift.Id, caregiverId);

// BAD - Logs patient name
_logger.LogInformation("Client {ClientName} visited at {Address}", client.FullName, client.Address);

// BAD - Serializes entire entity (includes PHI fields)
_logger.LogInformation("Processing visit note {@VisitNote}", visitNote);

// BAD - PHI in exception message
throw new InvalidOperationException($"Client {client.FullName} has expired care plan");

// GOOD - Exception with ID only
throw new InvalidOperationException($"Client {clientId} has expired care plan");
```

### Grep patterns to run:

Use these searches to find potential PHI in logs and exceptions:

**PHI property access in log/exception statements:**
Search for `.FullName`, `.DateOfBirth`, `.Address`, `.MedicalConditions`, `.Allergies`, `.InsuranceProvider`, `.InsurancePolicyNumber`, `.MedicaidNumber`, `.EmergencyContactName`, `.ClientCondition`, `.Concerns`, `.Medications`, `.CertificationNumber`, `.BloodPressure`, `.Temperature`, `.HeartRate` in `.cs` files that also contain `Log` or `throw` or `Exception`.

**Serilog destructuring of PHI entities:**
Search for `{@Client}`, `{@CarePlan}`, `{@VisitNote}`, `{@VisitPhoto}`, `{@Shift}`, `{@CaregiverCertification}` — the `@` prefix causes Serilog to serialize the entire object including all PHI fields.

**String interpolation in exceptions:**
Search for `throw new.*\$".*client\.` or `throw new.*\$".*patient\.` patterns in `.cs` files — exception messages end up in logs, error reporting, and stack traces.

**Flag**: Any log statement that references a PHI entity's non-ID property.

---

## Step 3: PHI in URLs Check

**Rule: No PHI in URLs — never put patient identifiers in query strings or route parameters without authorization checks.**

### What to look for:
- Route templates with PHI data (e.g., `/api/clients/{ssn}` or `/api/clients?name=John`)
- Query string parameters that expose PHI
- Redirect URLs that embed patient data
- SignalR hub methods that broadcast PHI in method names or group names

### Safe patterns:
```csharp
// GOOD - Use Guid IDs in routes
[HttpGet("{clientId:guid}")]
public async Task<ActionResult<ClientDto>> GetClient(Guid clientId)

// BAD - PHI in query string
[HttpGet("search")]
public async Task<ActionResult> Search([FromQuery] string patientName, [FromQuery] string diagnosis)

// If search by name is needed, use POST with body:
[HttpPost("search")]
public async Task<ActionResult> Search([FromBody] ClientSearchDto searchDto)
```

**Flag**: Any route or query parameter that contains or could contain PHI.

---

## Step 4: Authorization Check

**Rule: Enforce `[Authorize(Roles = "...")]` on every controller/endpoint that touches PHI.**

### What to look for:
- Controllers or endpoints without `[Authorize]` attribute that access PHI entities
- Missing role-based authorization (generic `[Authorize]` without role restriction on sensitive endpoints)
- Endpoints that return PHI data accessible to roles that shouldn't see it
- SignalR hubs without `[Authorize]`

### Expected authorization matrix:
| Endpoint | Admin | Coordinator | Caregiver | Client | FacilityManager |
|----------|-------|-------------|-----------|--------|-----------------|
| GET Client details | Yes | Yes | Own clients only | Own profile | No |
| GET VisitNote | Yes | Yes (read-only) | Own notes | Own notes | No |
| GET CarePlan | Yes | Yes | Assigned clients | Own plan | No |
| POST VisitNote | No | No | Own shifts only | No | No |
| GET Shift with GPS | Yes | Yes | Own shifts | Own shifts | Facility shifts |

### Grep patterns to run:

**Controllers without Authorize attribute:**
Search for `public class.*Controller` in `.cs` files and verify each has `[Authorize]` on the class or every method. Also search for `[AllowAnonymous]` on endpoints that handle PHI entities.

**SignalR hubs without Authorize:**
Search for `: Hub` in `.cs` files and verify each hub class has `[Authorize]`.

**Overly broad authorization:**
Search for `[Authorize]` without `Roles =` on controllers that reference Client, CarePlan, VisitNote, or Shift services — these need role-based restrictions.

**Flag**: Any endpoint touching PHI entities without proper `[Authorize(Roles = "...")]`.

---

## Step 5: Audit Logging Check

**Rule: Every read, write, and delete of PHI must be logged with UserId, Timestamp, Action, EntityType, EntityId.**

### What to look for:
- Repository methods or service methods that access PHI entities without audit logging
- Missing audit trail for reads (not just writes — HIPAA requires logging who viewed PHI)
- Bulk operations that bypass per-record audit logging
- Soft deletes that don't log the deletion event

### Expected audit log entry:
```csharp
// Every PHI access should generate an audit entry like:
{
    "UserId": "guid",
    "Timestamp": "2026-02-21T12:00:00Z",
    "Action": "Read|Create|Update|SoftDelete",
    "EntityType": "Client|CarePlan|VisitNote|...",
    "EntityId": "guid",
    "IpAddress": "optional"
}
```

**Flag**: Any PHI entity access (read, write, or delete) without corresponding audit logging.

---

## Step 6: API Response Exposure Check

**Rule: DTOs should not expose more PHI than necessary for the endpoint's purpose.**

### What to look for:
- DTOs that return full entity objects instead of projected views
- List endpoints that return PHI fields (e.g., a shift list that includes client medical conditions)
- Endpoints that return navigation properties with PHI (e.g., `Shift.Client.MedicalConditions`)
- AutoMapper profiles that map PHI fields to DTOs used in list views

### Safe patterns:
```csharp
// GOOD - Separate DTOs for list vs detail
public record ShiftListDto  // For list views - NO PHI
{
    public Guid Id { get; init; }
    public string ClientName { get; init; }  // OK - name only
    public DateTime ScheduledStart { get; init; }
    public ShiftStatus Status { get; init; }
}

public record ShiftDetailDto  // For detail view - includes PHI, requires authorization
{
    public Guid Id { get; init; }
    public ClientDetailDto Client { get; init; }  // Full client info - PHI
    public List<VisitNoteDto> VisitNotes { get; init; }  // PHI
}
```

**Flag**: Any DTO that exposes PHI fields beyond what's needed for its purpose.

---

## Step 7: Data Retention and Soft Delete Check

**Rule: `IsDeleted` soft-delete is required — hard deletes are forbidden on clinical data.**

### What to look for:
- Any call to `DbSet.Remove()` or `RemoveRange()` on PHI entities
- Any raw SQL `DELETE FROM` on PHI tables
- Missing `IsDeleted` global query filter in EF Core configurations
- Endpoints that permanently destroy data

### Grep patterns to run:

**Hard deletes:**
Search for `.Remove(`, `.RemoveRange(`, `DELETE FROM`, `TRUNCATE` in `.cs` files. Any match involving Client, CarePlan, VisitNote, VisitPhoto, Shift, or CaregiverCertification is a critical violation.

**Missing global query filter:**
Search for `HasQueryFilter` in entity configuration files. Every PHI entity configuration must have `.HasQueryFilter(x => !x.IsDeleted)`. If an entity configuration exists without this line, soft-deleted PHI records will leak into queries.

**Flag**: Any hard delete operation on PHI entities.

---

## Step 8: Encryption at Rest Check

**Rule: PHI data requires encryption at rest — SQL Server TDE.**

### What to look for (Infrastructure layer):
- Connection strings without `Encrypt=True` or TDE configuration
- PHI data stored in local files, logs, or caches without encryption
- Temporary files containing PHI (e.g., export files, report generation)

This is primarily an infrastructure/deployment concern, but flag if code writes PHI to unencrypted storage.

---

## Output: HIPAA Review Report

After completing all checks, produce a structured report:

```markdown
# HIPAA/PHI Review Report
**Date**: [today]
**Scope**: [what was reviewed — files, feature, PR]
**Reviewer**: Claude (automated)

## Summary
- **Critical Issues**: [count] (must fix before merge)
- **Warnings**: [count] (should fix)
- **Info**: [count] (best practice recommendations)

## Findings

### Critical Issues
[For each: description, file, line number, what to fix]

### Warnings
[For each: description, file, line number, recommendation]

### Info
[Best practice suggestions]

## PHI Entity Coverage
| Entity | Logging Safe | URLs Safe | Auth OK | Audit OK | DTO Scoped | Soft Delete |
|--------|-------------|-----------|---------|----------|------------|-------------|
| Client | Yes/No | Yes/No | Yes/No | Yes/No | Yes/No | Yes/No |
| CarePlan | ... | ... | ... | ... | ... | ... |
| ... | ... | ... | ... | ... | ... | ... |

## Recommendation
[PASS / PASS WITH WARNINGS / FAIL — with summary of required actions]
```

Save this report to `_specs/hipaa-reviews/` with a descriptive filename (e.g., `hipaa-review-cp-02-scheduling-2026-02-21.md`).

---

## Common Violations in .NET Healthcare Apps

These are the patterns that most frequently cause HIPAA issues in projects like CarePath:

1. **Serilog `{@Object}` destructuring** — Serializes entire entities including PHI fields into logs
2. **Exception messages with patient data** — Stack traces end up in logs and error reporting tools
3. **AutoMapper auto-flattening** — Maps PHI fields into DTOs that were intended to be lightweight
4. **Generic `[Authorize]` without roles** — Any authenticated user can access any patient's data
5. **Missing audit on GET endpoints** — Teams audit writes but forget that HIPAA requires logging reads too
6. **EF Core `.Include()` over-fetching** — Loading navigation properties brings in PHI data that then gets serialized
7. **SignalR broadcasting** — Real-time updates sent to all connected clients instead of scoped groups
