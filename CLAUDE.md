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
│   ├── Identity/                         # User, Caregiver, Client, CarePlan, CaregiverCertification
│   ├── Scheduling/                       # Shift, VisitNote, VisitPhoto
│   └── Billing/                          # Invoice, InvoiceLineItem, Payment
├── Enumerations/                         # UserRole, EmploymentType, ShiftStatus, etc.
└── Interfaces/Repositories/              # IRepository<T>, IUnitOfWork

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
- Implementations in `Infrastructure/Repositories/`
- Use `where T : BaseEntity` constraint on `IRepository<T>` — not `where T : class`
- Apply a global EF Core query filter (`IsDeleted == false`) in Infrastructure so soft-deleted records are automatically excluded

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
- **Audit logging** — every read, write, and delete of PHI must be logged with `UserId`, `Timestamp`, `Action`, `EntityType`, `EntityId`
- **No PHI in logs** — never log patient names, DOB, diagnosis, SSN, or address strings
- **No PHI in URLs** — never put patient identifiers in query strings or route parameters without authorization checks
- **Data retention**: 6 years for all medical records (Maryland requirement)
- `IsDeleted` soft-delete exists partly for compliance — hard deletes are forbidden on clinical data

Affected entities (always treat as PHI): `Client`, `CarePlan`, `Shift`, `VisitNote`, `VisitPhoto`, `CaregiverCertification`.

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

**Entities**: `User`, `Caregiver`, `Client`, `CarePlan`, `Shift`, `VisitNote`, `VisitPhoto`, `Invoice`, `InvoiceLineItem`, `Payment`, `CaregiverCertification`

**Key computed properties** (pure C#, no EF involvement):
- `User.FullName` = `$"{FirstName} {LastName}"`
- `Client.Age` = calculated from `DateOfBirth` (handle before/after birthday edge cases)
- `Shift.BillableHours` = `(ActualEnd - ActualStart - BreakMinutes) / 60`
- `Shift.GrossMargin` = `BillRate - PayRate`
- `Shift.GrossMarginPercentage` = `(GrossMargin / BillRate) * 100` — guard against zero `BillRate`
- `CaregiverCertification.IsExpired` = `ExpirationDate.Date < DateTime.UtcNow.Date`
- `CaregiverCertification.IsExpiringSoon` = expires within 30 days
- `Invoice.Subtotal` = sum of `LineItems.Amount`
- `Invoice.Balance` = `TotalAmount - AmountPaid`

**Enumerations**: `UserRole` (Admin, Coordinator, Caregiver, Client, FacilityManager), `EmploymentType`, `CertificationType`, `ServiceType`, `ShiftStatus`, `InvoiceStatus`, `PaymentMethod`, `PaymentStatus`

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

---

## Core Principles

- **Simplicity first** — Make every change as small and focused as possible. Touch only the files the task requires. A clean diff is a sign of good work.
- **No laziness** — Find root causes; never apply temporary fixes or workarounds that defer the real problem. Hold to senior developer standards on every task, not just the ones that feel important.
- **Minimal impact** — Changes should only affect what is necessary to fulfil the task. Avoid side-effect edits, opportunistic refactors, or touching unrelated code — these introduce unreviewed risk.
