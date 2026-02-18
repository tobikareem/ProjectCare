# Spec-Driven Development (SDD) for CarePath Health

This directory contains specifications for features being developed using the **Spec-Driven Development** workflow.

## Directory Structure

```
_specs/
├── 01-requirements/       # Requirements specifications
├── 02-design/             # Design/architecture specifications
├── 03-tasks/              # Task breakdowns
├── templates/             # Spec templates (.NET-specific)
├── examples/              # Example specs (reference implementations)
├── completed/             # Archived completed specs
└── README.md              # This file
```

---

## Workflow Overview

**Spec-Driven Development** separates planning from execution:

1. **Requirements** → Define WHAT and WHY
2. **Design** → Define HOW (technical architecture)
3. **Tasks** → Define WHEN and WHO (implementation breakdown)
4. **Implementation** → Claude Code executes against approved specs

### Benefits
- **Fewer interruptions**: Decisions made upfront, not during coding
- **Better alignment**: Stakeholders approve before implementation
- **Higher quality**: Claude has clear specifications to follow
- **Traceability**: Requirements → Design → Tasks → Code

---

## How to Use This System

### Step 1: Create Requirements Spec

1. Copy the template:
   ```bash
   ./scripts/new-spec.sh "Feature Name"
   ```
   OR manually:
   ```bash
   cp templates/REQUIREMENTS_TEMPLATE.md 01-requirements/feature-name.md
   ```

2. Fill in the requirements document:
   - Problem statement
   - User stories (Gherkin format)
   - Functional requirements
   - Success criteria
   - Dependencies and risks

3. Review with stakeholders

4. Mark as **Approved** in the status field

### Step 2: Create Design Spec

1. Copy the template:
   ```bash
   cp templates/DESIGN_TEMPLATE.md 02-design/feature-name.md
   ```

2. Fill in the design document:
   - Architecture overview (which layers are affected)
   - Domain entities, DTOs, services
   - EF Core migrations
   - API endpoints
   - UI components
   - Testing strategy

3. Review with tech lead

4. Mark as **Approved**

### Step 3: Create Tasks Breakdown

1. Copy the template:
   ```bash
   cp templates/TASKS_TEMPLATE.md 03-tasks/feature-name.md
   ```

2. Fill in the tasks document:
   - Break design into atomic tasks (1-4 hours each)
   - Define dependencies between tasks
   - Specify success criteria per task
   - List files to create/modify

3. Review with team

4. Mark as **Approved**

### Step 4: Implementation

Once all specs are approved, use Claude Code to implement:

```bash
# In Claude Code CLI
claude-code --spec _specs/03-tasks/feature-name.md
```

OR in Claude conversation:
```
Please implement the feature defined in _specs/03-tasks/gps-check-in.md.
Follow the spec carefully and reference the requirements and design docs.
```

Claude will:
- Read all three specs (requirements, design, tasks)
- Implement tasks in order
- Create tests
- Validate against success criteria
- Create pull requests

---

## Template Reference

### Requirements Template
- **Purpose**: Define what needs to be built and why
- **Sections**: Problem statement, user stories, functional requirements, success criteria
- **Audience**: Product owners, stakeholders, business users
- **When to use**: Beginning of every new feature

### Design Template
- **Purpose**: Define technical architecture and implementation approach
- **Sections**: Domain entities, DTOs, services, EF Core configs, API endpoints, UI components
- **Audience**: Tech leads, architects, developers
- **When to use**: After requirements are approved

### Tasks Template
- **Purpose**: Break design into implementable tasks
- **Sections**: Task ID, dependencies, estimates, success criteria, files to modify
- **Audience**: Developers, Claude Code
- **When to use**: After design is approved

---

## CarePath Health-Specific Guidance

### Architecture Layers
When creating specs, consider which layers are affected:

1. **CarePath.Domain** - Core entities, value objects, interfaces
2. **CarePath.Application** - Services, DTOs, validators, AutoMapper
3. **CarePath.Infrastructure** - EF Core, repositories, external services
4. **CarePath.Api** - Controllers, SignalR hubs, middleware
5. **CarePath.MauiApp** - Mobile UI (.NET MAUI Blazor Hybrid)
6. **CarePath.Web** - Admin dashboard (Blazor WebAssembly)
7. **CarePath.Shared** - Shared DTOs, constants

See `/Documentation/Architecture.md` for full architecture details.

### Common Patterns

**Entity Framework Core Migrations**:
```bash
dotnet ef migrations add <MigrationName> --project src/CarePath.Infrastructure --startup-project src/CarePath.Api
dotnet ef database update --project src/CarePath.Api
```

**Running Tests**:
```bash
dotnet test tests/CarePath.Domain.Tests/
dotnet test tests/CarePath.Application.Tests/
dotnet test tests/CarePath.Api.Tests/
```

**Running the API**:
```bash
cd src/CarePath.Api
dotnet run
# API available at: https://localhost:7001
```

**Running Mobile App**:
```bash
cd src/CarePath.MauiApp
dotnet build -t:Run -f net9.0-android  # Android
dotnet build -t:Run -f net9.0-ios      # iOS
```

---

## Best Practices

### Requirements Specs
- Write user stories in **Gherkin format** (Given/When/Then)
- Include **quantitative success metrics** (95% success rate, < 500ms response time)
- Consider both **In-Home Care** and **Healthcare Staffing** service lines
- Think about **HIPAA compliance** and Maryland healthcare regulations

### Design Specs
- Always reference the `/Documentation/Architecture.md` file
- Follow **Clean Architecture** principles (Domain → Application → Infrastructure → API/UI)
- Use **Entity Framework Core Fluent API** for entity configurations
- Plan **EF Core migrations** carefully (include rollback strategy)
- Consider **SignalR** for real-time features
- Plan for **offline mode** in mobile app (SQLite)

### Tasks Specs
- Keep tasks **atomic** (1-4 hours each)
- Define **clear dependencies** (TASK-001 → TASK-002 → TASK-003)
- Include **specific file paths** to create/modify
- Write **testable success criteria** (not "works", but "unit tests pass with >80% coverage")
- Estimate **realistically** (add buffer for testing and debugging)

---

## Examples

See the `examples/` folder for real-world spec examples:
- `examples/gps-check-in/` - Full SDD workflow for GPS-based check-in feature

---

## Scripts

### new-spec.sh
Creates a new spec from templates:
```bash
./scripts/new-spec.sh "GPS Check-In Feature"
```

This will create:
- `01-requirements/gps-check-in.md`
- `02-design/gps-check-in.md`
- `03-tasks/gps-check-in.md`

All linked together with relative paths.

---

## FAQ

**Q: Do I need to fill in all three specs before starting?**
A: No! You create them sequentially. Requirements → (get approved) → Design → (get approved) → Tasks → (get approved) → Implementation.

**Q: Can I skip the requirements or design phase?**
A: For small bug fixes, yes. But for any new feature or significant change, the specs will save you time by preventing rework.

**Q: How detailed should the specs be?**
A: Detailed enough that Claude Code (or a new developer) could implement the feature without asking questions.

**Q: What if requirements change mid-implementation?**
A: Update the spec first, get it re-approved, then continue. The spec is the source of truth.

**Q: Can I use this with other AI coding tools (Cursor, Copilot)?**
A: Yes! The specs are tool-agnostic. Any developer (human or AI) can read them.

---

## Archiving Completed Specs

Once a feature is fully implemented and deployed, move specs to `completed/`:

```bash
mkdir completed/gps-check-in/
mv 01-requirements/gps-check-in.md completed/gps-check-in/
mv 02-design/gps-check-in.md completed/gps-check-in/
mv 03-tasks/gps-check-in.md completed/gps-check-in/
```

This keeps active specs organized.

---

## Support

For questions about:
- **SDD workflow**: See this README or community resources
- **CarePath architecture**: See `/Documentation/Architecture.md`
- **.NET 9 / EF Core 9**: See Microsoft documentation
- **Claude Code**: See Claude Code documentation

---

**Happy Spec-Driven Development!** 🚀
