# CLAUDE.md — CarePath Health

CarePath Health is a .NET 9 healthcare management platform for in-home care (W-2 employees) and healthcare staffing (1099 contractors). Clean Architecture: Domain → Application → Infrastructure → WebApi, with a MAUI Blazor Hybrid mobile app and Blazor WebAssembly admin dashboard planned.

---

## Commands

```bash
# Build
dotnet build CarePath.sln

# Run Web API  (http://localhost:5240 / https://localhost:7028)
dotnet run --project WebApi

# Test — full suite
dotnet test CarePath.sln

# Test — single project
dotnet test Domain.Tests/Domain.Tests.csproj

# Test — single test by name
dotnet test --filter "FullyQualifiedName~TestClassName.TestMethodName"

# Test — with coverage
dotnet test --collect:"XPlat Code Coverage"

# EF Core migrations
dotnet ef migrations add <Name> --project Infrastructure --startup-project WebApi
dotnet ef database update --startup-project WebApi

# New spec
bash _specs/scripts/new-spec.sh CP-XX "Feature Name"
```

---

## Architecture

**Dependency rule: Domain ← Application ← Infrastructure ← WebApi. Never invert.**

| Layer | Namespace | Responsibility |
|---|---|---|
| Domain | `CarePath.Domain` | Entities, enums, interfaces. Zero external dependencies. |
| Application | `CarePath.Application` | Services, DTOs, validators, interfaces (depends on Domain only). |
| Infrastructure | `CarePath.Infrastructure` | EF Core DbContext, repositories, external services. |
| WebApi | `CarePath.WebApi` | ASP.NET Core controllers, middleware, SignalR hubs. |

---

## Project Folder Layout

```
CarePath.Domain/
├── Entities/
│   ├── Common/BaseEntity.cs              # Abstract base — all entities inherit this
│   ├── Identity/                         # User, Caregiver, Client, CaregiverCertification
│   ├── Clinical/                         # CarePlan
│   ├── Scheduling/                       # Shift, VisitNote, VisitPhoto
│   └── Billing/                          # Invoice, InvoiceLineItem, Payment
├── Enumerations/                         # UserRole, EmploymentType, ShiftStatus, etc.
└── Interfaces/Repositories/              # IRepository<T>, IUnitOfWork

CarePath.Infrastructure/                  # (CP-02 — EF Core, Repositories, Identity)
├── Persistence/
│   ├── CarePathDbContext.cs              # EF Core DbContext (IdentityDbContext<ApplicationUser>)
│   ├── Configurations/                   # Fluent API entity configurations (Identity/, Scheduling/, Billing/)
│   ├── Interceptors/                     # AuditableEntityInterceptor (auto-set audit fields)
│   ├── Converters/                       # UtcDateTimeConverter (preserve UTC on SQL Server round-trip)
│   ├── Repositories/Repository.cs        # Generic Repository<T> : IRepository<T>
│   ├── UnitOfWork.cs                     # UnitOfWork : IUnitOfWork
│   └── Migrations/                       # EF Core auto-generated migrations
├── Identity/ApplicationUser.cs           # IdentityUser<Guid> linked to domain User via DomainUserId
└── DependencyInjection.cs                # AddInfrastructure() service registration

_specs/
├── 01-requirements/                      # Problem statement, Gherkin user stories
├── 02-design/                            # Architecture decisions, entity design, API endpoints
└── 03-tasks/                             # Atomic tasks (1–4 hours each), dependencies, files
```

---

## Coding Conventions

### Entity Rules (ENFORCED — never deviate)

- **Primary keys**: `Guid` only — never `int` or auto-increment
- **Timestamps**: `DateTime.UtcNow` always — never `DateTime.Now`
- **Soft deletes**: Set `IsDeleted = true` — never call `DbSet.Remove()`
- **Audit fields**: All entities inherit `BaseEntity` which provides `Id`, `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`, `IsDeleted`
- **Nullable reference types**: Enabled globally — respect nullability annotations; never suppress with `!` without a comment explaining why

### C# Style

- Use explicit `using` directives at the top of each entity file — do not rely on implicit usings alone, as it hides dependencies
- Add `using CarePath.Domain.Entities.Common;` explicitly on all entity files
- Add `using CarePath.Domain.Enumerations;` on files that reference enums
- Use `IReadOnlyList<T>` for collection return types from repositories — not `IEnumerable<T>`
- Prefer pattern matching and expression-bodied members for simple computed properties
- XML documentation (`/// <summary>`) on all public types and members in Domain

### Validation

- Use **FluentValidation** in the Application layer — never data annotations on DTOs
- Validate at the Application boundary (command/query handlers), not in domain entities

### Repository Pattern

- Interfaces defined in `Domain/Interfaces/Repositories/`
- Implementations in `Infrastructure/Persistence/Repositories/`
- Use `where T : BaseEntity` constraint on `IRepository<T>` — not `where T : class`
- Apply a global EF Core query filter (`IsDeleted == false`) in Infrastructure so soft-deleted records are automatically excluded
- Use `GetPagedAsync` (not `GetAllAsync`) for high-volume tables (`Shift`, `VisitNote`)

### Infrastructure / EF Core Conventions

- **Entity configurations**: Fluent API only — never data annotations on entities
- **UTC DateTime converter**: Apply on every `DateTime` and `DateTime?` property to preserve `DateTimeKind.Utc` through SQL Server round-trips
- **Decimal precision**: `(18, 2)` for all monetary values (BillRate, PayRate, Amount, etc.)
- **String lengths**: Email=256, Names=100, PhoneNumber=20, ZipCode=10, CertificationNumber=50, InvoiceNumber=20, Address=500
- **Cascade deletes**: `DeleteBehavior.Restrict` on PHI entities (Client, CarePlan, VisitNote, VisitPhoto, Shift, CaregiverCertification) — never cascade delete clinical data
- **Computed properties**: `.Ignore()` in configuration — never persist to database (FullName, Age, BillableHours, GrossMargin, etc.)
- **Audit interceptor**: `SaveChangesInterceptor` auto-sets `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy` — never set these manually in service code
- **ASP.NET Core Identity**: Pattern A (Separate Tables) — `ApplicationUser : IdentityUser<Guid>` with `DomainUserId` FK to domain `User`
- **Connection strings**: Must include `Encrypt=True` for HIPAA compliance


---

## Shared Client Architecture

Blazor WebAssembly and MAUI Blazor Hybrid must share client-safe code without binding directly to Domain entities.

| Project | Purpose | May Reference |
|---|---|---|
| `CarePath.Contracts` | DTOs, request/response models, pagination/result envelopes, enum mirrors safe for clients | No Domain, Infrastructure, or WebApi references |
| `CarePath.Client` | Typed API clients, auth token handling abstractions, retry/error mapping, client-side validation helpers | `CarePath.Contracts` |
| `CarePath.Client.UI` | Reusable Razor components, forms, table primitives, status badges, validation display | `CarePath.Contracts`, `CarePath.Client` |

Allowed to share: DTOs, typed clients, UI primitives, formatting helpers, and validation helpers that do not expose PHI beyond the current authorized view.

Do not share: full pages, platform-specific services, persistence entities, EF models, Domain entities, WebApi controllers, secrets, or provider SDK clients.

---

## Testing Conventions

- **Framework**: xUnit + FluentAssertions + Moq
- **Naming**: `MethodName_StateUnderTest_ExpectedBehavior` (e.g., `BillableHours_WhenBreakTimeIsNull_ExcludesBreakFromCalculation`)
- **Structure**: Arrange / Act / Assert with blank lines between sections
- **Domain tests**: Pure unit tests — no EF Core, no mocks for domain entities
- **Application tests**: Mock repository interfaces with Moq
- Test computed properties with edge cases: null inputs, zero denominators, boundary dates
- Run single tests during development; run full suite before committing

---

## HIPAA & Compliance

All PHI (Protected Health Information) data requires:

- **Encryption at rest** — SQL Server Transparent Data Encryption
- **Role-based authorization** — enforce `[Authorize(Roles = "...")]` on every controller/endpoint that touches PHI
- **Object-level authorization** — every `{id}` route that touches PHI must verify the current user can access that specific record to prevent IDOR
- **Audit logging** — every read, write, update, and delete of PHI must be logged with `UserId`, `Timestamp`, `Action`, `EntityType`, `EntityId`; never log PHI values
- **No PHI in logs** — never log patient names, DOB, diagnosis, SSN, or address strings
- **No PHI in URLs** — never put patient identifiers in query strings or route parameters without authorization checks
- **Data retention**: 6 years for all medical records (Maryland requirement)
- `IsDeleted` soft-delete exists partly for compliance — hard deletes are forbidden on clinical data
- **Third-party provider gates** — Twilio/SMS/voice, AI/OCR, email, and storage providers must have HIPAA readiness documented before PHI is sent externally; SMS/voice also needs BAA, consent, opt-out, webhook verification, and minimum-necessary content rules
- **File/photo storage gates** — private blobs only, encryption, access control, short-lived URLs, malware scanning, and no public containers

Affected entities (always treat as PHI): `Client`, `CarePlan`, `Shift`, `VisitNote`, `VisitPhoto`, `CaregiverCertification`, `DischargeDocument`, `TransitionPlan`, `TransitionInstruction`, `TransitionCheckIn`.

---

## Workflow Orchestration

### 1. Plan Before You Code
Enter plan mode for **any non-trivial task** (3+ steps, new entity, new endpoint, architectural change). Read the relevant spec in `_specs/` before touching a file. If implementation reveals the spec is ambiguous or stale, **stop and resolve the spec first** — don't guess and push forward.

### 2. Subagent Strategy
Use subagents to keep the main context window clean and focused:
- Use the `dotnet-code-reviewer` subagent after every implementation — not just when asked
- Use the Task tool to offload exploratory research (reading large specs, tracing cross-layer dependencies) to a subagent
- One focused task per subagent invocation — avoid overloading a single subagent with multiple concerns

### 3. Self-Improvement Loop
After **any correction from the user**, immediately update `_specs/lessons.md` with the pattern:
- What the mistake was
- Why it happened
- The rule that prevents it next time

Read `_specs/lessons.md` at the start of every session before writing code. Ruthlessly refine entries until the mistake rate drops.

### 4. Verification Before Done
Never mark a task complete without proving it works:
- `dotnet build CarePath.sln` passes with zero warnings
- Relevant `dotnet test` passes
- `dotnet-code-reviewer` subagent finds no critical issues
- If PHI-adjacent: spot-check that no patient data is leaking into logs or URLs

Ask yourself: **"Would a senior .NET engineer approve this PR?"** If not, keep going.

### 5. Demand Elegance (Balanced)
For non-trivial implementations, pause before submitting and ask: *"Is there a simpler, more idiomatic .NET way to do this?"* If a fix feels hacky or layers workarounds, stop and implement the clean solution instead. Skip this for obvious, self-contained changes — don't over-engineer simple things.

### 6. Autonomous Bug Fixing
When given a failing build, test failure, or bug report: **just fix it.** Point at the error, trace the cause, resolve it — don't ask for hand-holding. If the root cause is in a spec, update the spec and the code together.

---

## Spec-Driven Development Workflow

**Read the spec before writing any code.** Every feature follows three phases in `_specs/`:

1. **Requirements** (`01-requirements/`) — user stories in Gherkin, acceptance criteria
2. **Design** (`02-design/`) — entity design, API endpoints, architecture decisions
3. **Tasks** (`03-tasks/`) — atomic tasks with file lists, dependencies, estimates

All specs must be in `Approved` status before implementation begins. If a task spec is stale or contradicts the design spec, the design spec takes precedence — update the task spec first.

**Lessons file**: `_specs/lessons.md` — updated after every user correction; read at session start.

---

## Task Management

Follow this sequence for every implementation task:

1. **Plan first** — Read the spec, identify all files to create/modify, note dependencies. If non-trivial, enter plan mode and confirm the approach before writing code.
2. **Verify the plan** — Check in with the user before starting implementation if scope is large or ambiguous.
3. **Track progress** — Work through tasks in dependency order (as defined in `_specs/03-tasks/`); don't skip ahead.
4. **Explain changes** — Provide a high-level summary at each meaningful step — what was done and why, not just what files changed.
5. **Document results** — After implementation, confirm: build passes, tests pass, reviewer subagent approves.
6. **Capture lessons** — If the user corrects anything, update `_specs/lessons.md` immediately before moving on.

---

## Domain Model

### Core Entities (CP-01 — Implemented)

**Entities**: `User`, `Caregiver`, `Client`, `CarePlan`, `Shift`, `VisitNote`, `VisitPhoto`, `Invoice`, `InvoiceLineItem`, `Payment`, `CaregiverCertification`

**Key computed properties** (pure C#, no EF involvement):
- `User.FullName` = `$"{FirstName} {LastName}"`
- `Client.Age` = calculated from `DateOfBirth` (handle before/after birthday edge cases)
- `Shift.BillableHours` = `(ActualEnd - ActualStart - BreakMinutes) / 60`
- `Shift.GrossMargin` = `(BillRate - PayRate) * BillableHours` (total shift margin)
- `Shift.GrossMarginPercentage` = `(GrossMargin / (BillRate * BillableHours)) * 100` — guard against zero `BillRate` or zero `BillableHours`
- Future dashboard/contracts work uses `HourlyGrossMargin = BillRate - PayRate` when hourly spread is needed
- `CaregiverCertification.IsExpired` = `ExpirationDate.Date < DateTime.UtcNow.Date`
- `CaregiverCertification.IsExpiringSoon` = expires within 30 days
- `Invoice.Subtotal` = sum of `LineItems.Amount`
- `Invoice.Balance` = `TotalAmount - AmountPaid`

**Enumerations**: `UserRole` (Admin, Coordinator, Caregiver, Client, FacilityManager), `EmploymentType` (W2Employee, Contractor1099), `CertificationType`, `ServiceType` (InHomeCare, FacilityStaffing), `ShiftStatus` (Scheduled, InProgress, Completed, Cancelled, NoShow), `InvoiceStatus`, `PaymentMethod`

---

### CarePath Transitions Entities (CP-03 — Domain implemented, backend planned)

**Spec**: `_specs/01-requirements/cp-03-transitions.md`, `_specs/02-design/cp-03-transitions.md`

**Purpose**: 30-day post-discharge care management. Four-step workflow: Intake → Verify → Guide → Escalate.

**Folder**: `CarePath.Domain/Entities/Transitions/`

| Entity | Responsibility |
|---|---|
| `DischargeDocument` | Uploaded/imported source document (PDF, photo, FHIR). Holds raw content and extraction status. |
| `TransitionPlan` | The clinician-verified, activated 30-day care plan. One plan per discharge episode. |
| `TransitionInstruction` | A single extracted instruction (medication, appointment, diet, etc.) within a plan. Holds AI confidence score and source text link. |
| `TransitionReminder` | A scheduled reminder/check-in sent to patient via app, SMS, or voice. Tracks acknowledgement. |
| `TransitionCheckIn` | Patient's symptom/adherence response to a check-in prompt. Flags warning symptoms. |
| `TransitionEscalation` | An escalation event triggered by missed tasks, warning symptoms, or failed contact. |

**Computed properties for Transitions**:
- `TransitionPlan.IsActive` = `Status == TransitionPlanStatus.Active && DateTime.UtcNow <= TransitionWindowEnd`
- `TransitionPlan.DaysRemaining` = `(TransitionWindowEnd - DateTime.UtcNow).Days` (guard negative)
- `TransitionInstruction.IsLowConfidence` = `ConfidenceScore < 0.75m`
- `TransitionReminder.IsOverdue` = `Status == ReminderStatus.Scheduled && ScheduledAt < DateTime.UtcNow`

**New enumerations for Transitions**:
- `DischargeDocumentSourceType`: `PdfUpload`, `PhotoUpload`, `FhirImport`
- `DischargeDocumentStatus`: `Pending`, `Extracting`, `AwaitingReview`, `Approved`, `Rejected`
- `TransitionPlanStatus`: `Draft`, `PendingVerification`, `Active`, `Completed`, `Cancelled`
- `TransitionRiskLevel`: `Low`, `Medium`, `High` — controls reminder intensity (High = daily touch)
- `TransitionInstructionCategory`: `Medication`, `Appointment`, `Diet`, `Activity`, `WoundCare`, `WarningSigns`, `Equipment`, `Other`
- `TransitionInstructionStatus`: `Pending`, `Approved`, `Rejected`, `Modified`
- `ReminderType`: `Medication`, `Appointment`, `SymptomCheckIn`, `Refill`, `Equipment`, `Activity`, `Diet`
- `ReminderChannel`: `App`, `Sms`, `Voice`
- `ReminderStatus`: `Scheduled`, `Sent`, `Acknowledged`, `Missed`, `Failed`
- `EscalationTriggerType`: `MissedCriticalTask`, `WarningSymptomsReported`, `FailedContact`, `CaregiverAlert`
- `EscalationLevel`: `CoordinatorAlert`, `FamilyNotification`, `UrgentCare`, `Emergency911`

**Key relationships**:
- `DischargeDocument.ClientId` → `Client.Id`
- `TransitionPlan.ClientId` → `Client.Id`
- `TransitionPlan.DischargeDocumentId` → `DischargeDocument.Id`
- `VisitNote` will gain optional `TransitionPlanId` FK so in-home caregiver observations feed adherence tracking

**PHI note**: `DischargeDocument`, `TransitionPlan`, `TransitionInstruction`, `TransitionCheckIn` all contain clinical PHI. Apply all HIPAA rules — audit logging, no PHI in logs, role-based authorization.

---

## Tech Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 9, C# 13 |
| ORM | EF Core 9 + SQL Server |
| Auth | ASP.NET Core Identity + JWT |
| Real-time | SignalR |
| Validation | FluentValidation |
| Mapping | AutoMapper |
| Logging | Serilog |
| Testing | xUnit + Moq + FluentAssertions |
| Mobile | .NET MAUI Blazor Hybrid |
| Admin UI | Blazor WebAssembly |
| Docs | Context7 MCP (`.claude/mcp.json`) |
| SMS/Voice | Twilio (planned for CP-03 Transitions reminders) |

---

## Custom Commands (Claude Code)

Use these slash commands in Claude Code:

| Command | What it does |
|---|---|
| `/dotnet-code` | Implement a feature from spec, build, then auto-run the `dotnet-code-reviewer` subagent |
| `/code-review` | Review all uncommitted changes via the `dotnet-code-reviewer` subagent |
| `/commit-message` | Generate an emoji-typed commit message from staged changes |

The `dotnet-code-reviewer` subagent (`.claude/agents/dotnet-code-reviewer.md`) is an expert .NET reviewer that enforces all rules above, checks specs, and uses Context7 for official docs. It is invoked automatically by both `/dotnet-code` and `/code-review`.

---

## Common Mistakes to Avoid

- **`DateTime.Now`** — always use `DateTime.UtcNow`
- **`int` primary keys** — always `Guid`
- **Hard deletes** — always set `IsDeleted = true`
- **Missing `BaseEntity` inheritance** — every entity must inherit it
- **Validation in entities** — belongs in FluentValidation validators in Application layer
- **Cross-layer imports** — Domain must never reference Application or Infrastructure namespaces
- **`GetAllAsync()` on large tables** — `Shift` and `VisitNote` will grow large; always add filtering; `GetPagedAsync` will be added in the Infrastructure spec (TASK-019a)
- **PHI in logs or exception messages** — never include patient data in log strings
- **Discharge content in logs** — `DischargeDocument.RawContent` and `TransitionInstruction.SourceText` are PHI; never log them
- **`TransitionWindowEnd` calculation** — always `DischargeDate.AddDays(30)` in UTC; do not use local time
- **Reminder delivery** — never send reminders before `TransitionPlan.Status == Active`; check this in the Application layer, not just the API layer

---

## Core Principles

- **Simplicity first** — Make every change as small and focused as possible. Touch only the files the task requires. A clean diff is a sign of good work.
- **No laziness** — Find root causes; never apply temporary fixes or workarounds that defer the real problem. Hold to senior developer standards on every task, not just the ones that feel important.
- **Minimal impact** — Changes should only affect what is necessary to fulfil the task. Avoid side-effect edits, opportunistic refactors, or touching unrelated code — these introduce unreviewed risk.
