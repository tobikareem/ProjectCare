# Spec-Driven Development: Quick Start Guide

Get started with Spec-Driven Development for CarePath Health in 5 minutes.

---

## What is Spec-Driven Development?

**Spec-Driven Development (SDD)** separates planning from execution:

1. **Write specs** → Requirements, Design, Tasks
2. **Get approval** → Stakeholders review and approve
3. **Implement** → Claude Code (or you) builds against approved specs

**Benefits:**
- ✅ Less back-and-forth during implementation
- ✅ Better architectural decisions upfront
- ✅ Clear documentation for future reference
- ✅ AI agents work autonomously with clear specs

---

## Quick Start: Create Your First Spec

### Option 1: Use the Script (Recommended)

```bash
cd _specs
./scripts/new-spec.sh "Your Feature Name"
```

This creates three spec files:
- `01-requirements/your-feature-name.md`
- `02-design/your-feature-name.md`
- `03-tasks/your-feature-name.md`

All pre-linked and ready to fill in!

### Option 2: Manual Creation

```bash
cd _specs
cp templates/REQUIREMENTS_TEMPLATE.md 01-requirements/my-feature.md
cp templates/DESIGN_TEMPLATE.md 02-design/my-feature.md
cp templates/TASKS_TEMPLATE.md 03-tasks/my-feature.md
```

Then fill in each file.

---

## The SDD Workflow

### Step 1: Requirements (30 mins - 2 hours)

**File:** `01-requirements/your-feature.md`

**Fill in:**
- Problem statement (what pain point are you solving?)
- User stories in Gherkin format (Given/When/Then)
- Functional requirements (what the system must do)
- Success criteria (quantitative metrics: 95% success rate, < 500ms response time)

**Example:**
```gherkin
As a Caregiver
I want to view my upcoming shifts
So that I can plan my week

Acceptance Criteria:
- Given I am logged in as a caregiver
- When I navigate to "My Shifts"
- Then I see a list of my shifts for the next 7 days
- And each shift shows: client name, address, date, time, service type
```

**Get approval:** Product Owner, Business Stakeholders

---

### Step 2: Design (1-4 hours)

**File:** `02-design/your-feature.md`

**Fill in:**
- Which layers are affected? (Domain, Application, Infrastructure, API, MAUI, Web)
- Domain entities (C# classes)
- DTOs, services, validators
- EF Core entity configurations
- API endpoints (controllers or minimal APIs)
- UI components (Blazor pages)
- Testing strategy

**Example:**
```csharp
// Domain entity
public class Shift : BaseEntity
{
    public Guid CaregiverId { get; set; }
    public Guid ClientId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public ShiftStatus Status { get; set; }

    // Navigation properties
    public Caregiver Caregiver { get; set; }
    public Client Client { get; set; }
}
```

**Get approval:** Tech Lead, Architect

---

### Step 3: Tasks (2-6 hours)

**File:** `03-tasks/your-feature.md`

**Fill in:**
- Break design into atomic tasks (1-4 hours each)
- Organize by phase (Domain → Application → Infrastructure → API → UI → Testing → Deployment)
- Define dependencies (TASK-001 → TASK-002 → TASK-003)
- Specify files to create/modify
- Write testable success criteria

**Example:**
```markdown
### TASK-001: Create Shift Entity
- Layer: CarePath.Domain
- Dependencies: None
- Estimate: 1 hour
- Success Criteria:
  - Shift entity created with all properties
  - Inherits from BaseEntity
  - Navigation properties configured
  - Unit tests pass
- Files:
  - CREATE: src/CarePath.Domain/Entities/Scheduling/Shift.cs
  - CREATE: tests/CarePath.Domain.Tests/Entities/ShiftTests.cs
```

**Get approval:** Development Team

---

### Step 4: Implementation

Now you (or Claude Code) implement the feature following the specs.

**With Claude Code CLI:**
```bash
claude-code --spec _specs/03-tasks/your-feature.md
```

**With Claude in conversation:**
```
Please implement the feature defined in _specs/03-tasks/your-feature.md.
Follow the tasks in order and reference the requirements and design specs.
```

Claude will:
1. Read all three specs
2. Implement tasks sequentially
3. Create tests for each component
4. Validate against success criteria
5. Create commits/PRs as tasks complete

---

## CarePath Health-Specific Tips

### Always Consider These Layers

```
Domain → Application → Infrastructure → API → UI (MAUI + Web)
```

1. **Domain** (`CarePath.Domain`): Core entities, value objects, interfaces
2. **Application** (`CarePath.Application`): Services, DTOs, validators
3. **Infrastructure** (`CarePath.Infrastructure`): EF Core, repositories, external services
4. **API** (`CarePath.Api`): Controllers, SignalR hubs
5. **UI Mobile** (`CarePath.MauiApp`): .NET MAUI Blazor Hybrid (iOS/Android)
6. **UI Web** (`CarePath.Web`): Blazor WebAssembly admin dashboard

### Common Patterns

**EF Core Migration:**
```bash
dotnet ef migrations add MyFeature --project src/CarePath.Infrastructure --startup-project src/CarePath.Api
dotnet ef database update --project src/CarePath.Api
```

**Repository Pattern:**
```csharp
// Domain interface
public interface IShiftRepository : IRepository<Shift>
{
    Task<List<Shift>> GetShiftsByCaregiverAsync(Guid caregiverId);
}

// Infrastructure implementation
public class ShiftRepository : Repository<Shift>, IShiftRepository
{
    public async Task<List<Shift>> GetShiftsByCaregiverAsync(Guid caregiverId)
    {
        return await _context.Shifts
            .Include(s => s.Client)
            .Where(s => s.CaregiverId == caregiverId)
            .OrderBy(s => s.StartTime)
            .ToListAsync();
    }
}
```

**API Controller:**
```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ShiftsController : ControllerBase
{
    private readonly IShiftService _shiftService;

    [HttpGet("my-shifts")]
    public async Task<ActionResult<List<ShiftDto>>> GetMyShifts()
    {
        var caregiverId = GetCurrentUserId();
        var shifts = await _shiftService.GetShiftsByCaregiverAsync(caregiverId);
        return Ok(shifts);
    }
}
```

---

## FAQs

**Q: This seems like a lot of upfront work. Is it worth it?**
A: Yes! For small bug fixes, you can skip this. But for any new feature, the time spent on specs is saved 10x during implementation. No more "wait, what should this do?" interruptions.

**Q: Do I need to fill in every section of the templates?**
A: No. The templates are comprehensive, but adapt them to your feature size. A small feature might have a 1-page requirements spec. A large feature might have 10 pages.

**Q: Can I update specs during implementation?**
A: Yes! If you discover something needs to change, update the spec first, get it re-approved, then continue. The spec is the source of truth.

**Q: What if I'm prototyping?**
A: For prototypes/POCs, you can skip specs or use a lightweight version. Once you decide to build it for real, create proper specs.

---

## Resources

- **Full README:** [README.md](README.md)
- **Templates:** [templates/](templates/)
- **Examples:** [examples/](examples/)
- **Architecture:** [../Documentation/Architecture.md](../Documentation/Architecture.md)

---

## Getting Help

**Stuck on requirements?**
→ Talk to Product Owner or review user stories with stakeholders

**Stuck on design?**
→ Review `/Documentation/Architecture.md` and talk to Tech Lead

**Stuck on tasks?**
→ Break it down smaller! Each task should be 1-4 hours max

---

**Now go create your first spec!** 🚀

```bash
cd _specs
./scripts/new-spec.sh "My Awesome Feature"
```
