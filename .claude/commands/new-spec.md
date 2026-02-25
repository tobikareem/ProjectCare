---
description: "Guide the creation of a complete feature specification across all three spec documents (requirements, design, tasks). Use when starting a new feature, adding a capability, or when the user says 'new spec', 'new feature', 'spec out', 'plan a feature', or references creating specs. This command walks through the full SDD workflow so nothing gets missed."
allowed-tools: Read, Write, Edit, Glob, Grep, Bash(bash _specs/scripts/new-spec.sh *), Bash(ls *), Bash(cat *), Task, mcp__context7__resolve-library-id, mcp__context7__query-docs
---

## Purpose

This command guides Claude through CarePath Health's Spec-Driven Development workflow, producing three linked spec documents for a new feature:

1. **Requirements** (`_specs/01-requirements/<feature-slug>.md`) — WHAT and WHY
2. **Design** (`_specs/02-design/<feature-slug>.md`) — HOW (technical architecture)
3. **Tasks** (`_specs/03-tasks/<feature-slug>.md`) — WHEN, WHO, and in what order

The goal is to produce specs detailed enough that a developer (human or AI) can implement the feature without asking questions.

---

## Before You Start

1. **Read the lessons file** — `_specs/lessons.md` may contain patterns or mistakes relevant to spec writing.
2. **Read existing specs for context** — Check `_specs/01-requirements/`, `_specs/02-design/`, and `_specs/03-tasks/` for completed specs. Understanding how previous specs were written helps maintain consistency.
3. **Read the templates** — Skim `_specs/templates/REQUIREMENTS_TEMPLATE.md`, `_specs/templates/DESIGN_TEMPLATE.md`, and `_specs/templates/TASKS_TEMPLATE.md` to refresh the expected structure.
4. **Identify the next spec ID** — Look at existing specs to determine the next `CP-XX` identifier (e.g., if `cp-01` exists, the next is `cp-02`).

---

## Phase 1: Requirements Gathering (Interview the User)

Before writing anything, gather enough information to write a solid requirements spec. Ask the user about:

### Must-Have Information
- **What problem does this solve?** — What pain point exists today? Who experiences it?
- **Which user roles are affected?** — Admin, Coordinator, Caregiver, Client, FacilityManager?
- **Which service line?** — In-Home Care (W-2, 40-45% margin), Healthcare Staffing (1099, 25-30% margin), or both?
- **What are the primary user stories?** — Use Gherkin format: As a [role], I want [action], So that [benefit]
- **What are the acceptance criteria?** — Given/When/Then for each story
- **Are there HIPAA/PHI implications?** — Does this touch Client, CarePlan, Shift, VisitNote, VisitPhoto, or CaregiverCertification data?

### Good-to-Have Information
- **Success metrics** — How will we know this feature works? (quantitative: <500ms response time, 95% success rate)
- **Edge cases** — What happens when things go wrong? Offline mode? Invalid data?
- **Dependencies** — Does this depend on other features or specs being completed first?
- **Scope boundaries** — What is explicitly NOT included?

If the user provides a brief description (e.g., "I want to add scheduling"), ask targeted follow-up questions to fill gaps. Don't ask everything at once — prioritize the must-haves and infer what you can from context and the existing codebase.

---

## Phase 2: Create Spec Files

### Option A: Use the Script (Preferred)
```bash
bash _specs/scripts/new-spec.sh CP-XX "Feature Name"
```
This creates all three files from templates with cross-links already wired up.

### Option B: Manual Creation
If the script isn't suitable, manually copy and customize the templates.

---

## Phase 3: Write the Requirements Spec

Fill in `_specs/01-requirements/cp-XX-<feature-slug>.md` following the template structure:

1. **Executive Summary** — One sentence: what and why
2. **Problem Statement** — Current state, business impact (reference margin targets), user impact
3. **User Stories** — Gherkin format with acceptance criteria. Cover primary flows, secondary flows, and edge cases
4. **Functional Requirements** — Table format with ID, requirement, priority, user roles, service line
5. **Non-Functional Requirements** — Performance, scalability, reliability, usability, compliance
6. **Success Criteria** — Quantitative metrics and qualitative goals
7. **Scope & Boundaries** — In scope, out of scope, future considerations
8. **Dependencies & Assumptions**
9. **Risks & Mitigation** — Table format
10. **Stakeholder Sign-Off** — Leave as pending

### CarePath-Specific Considerations for Requirements
- Always consider BOTH service lines (In-Home Care and Healthcare Staffing) unless the feature is line-specific
- Reference margin targets: 40-45% for in-home, 25-30% for staffing
- Flag any PHI-adjacent data — triggers HIPAA requirements
- Consider offline mode for mobile (MAUI) features
- Reference the business model from `Documentation/CarePath_Health.pdf` if relevant

**Set status to Draft.** Present to the user for review before moving to design.

---

## Phase 4: Write the Design Spec

After the user approves the requirements (or while reviewing — ask), fill in `_specs/02-design/cp-XX-<feature-slug>.md`:

1. **Architecture Overview** — ASCII diagram of component interactions; check all affected layers
2. **Domain Layer** — New/modified entities (C# code), value objects, enums, domain interfaces, domain events
3. **Application Layer** — DTOs (request + response), services (interface + implementation outline), FluentValidation validators, AutoMapper profiles
4. **Infrastructure Layer** — EF Core DbContext changes, entity configurations (Fluent API), migration strategy with expected schema changes, repository implementations, external service integrations
5. **API Layer** — Controllers with endpoint definitions (HTTP method, route, auth, response types), SignalR hubs if real-time needed, middleware if needed
6. **Presentation Layer** — MAUI mobile pages (Blazor Razor), Blazor WebAssembly admin pages, platform-specific config (Android/iOS permissions)
7. **Testing Strategy** — Unit tests (Domain, Application), integration tests (API), E2E tests (MAUI)
8. **Performance Considerations** — Database indexes, caching, API optimization, mobile optimization
9. **Security** — Auth/authz, data protection, HIPAA, input validation
10. **Deployment Plan** — Database migration, API, mobile, web, rollback plan

### CarePath-Specific Design Rules (from CLAUDE.md)
- **Dependency rule**: Domain <- Application <- Infrastructure <- WebApi. Never invert.
- **Primary keys**: `Guid` only — never `int`
- **Timestamps**: `DateTime.UtcNow` always
- **Soft deletes**: `IsDeleted = true` — never `DbSet.Remove()`
- **All entities**: Inherit `BaseEntity`
- **Validation**: FluentValidation in Application layer, not data annotations
- **Repositories**: Interfaces in Domain, implementations in Infrastructure
- **Collections**: `IReadOnlyList<T>` for return types
- **Global query filter**: `IsDeleted == false` in Infrastructure

**Set status to Draft.** Present to the user.

---

## Phase 5: Write the Tasks Spec

After design approval, fill in `_specs/03-tasks/cp-XX-<feature-slug>.md`:

### Task Structure
Each task must include:
- **ID**: `TASK-XXX` (sequential within this spec)
- **Title**: Action-oriented (e.g., "Create Shift Entity", not "Shift Entity")
- **Layer**: Which project (Domain, Application, Infrastructure, WebApi, MauiApp, Web)
- **Dependencies**: Which tasks must complete first
- **Estimate**: Hours (1-4 per task; if >4, break it down further)
- **Priority**: Critical, High, Medium, Low
- **Success Criteria**: Testable outcomes (not "works" but "unit tests pass with >90% coverage")
- **Files**: CREATE or MODIFY with specific file paths
- **Implementation Notes**: Code snippets if helpful

### Task Phases (follow this order)
1. **Phase 1: Domain Layer** — Entities, enums, value objects, interfaces
2. **Phase 2: Application Layer** — DTOs, services, validators, mappers
3. **Phase 3: Infrastructure Layer** — DbContext, configurations, migrations, repositories, external services, DI registration
4. **Phase 4: API Layer** — Controllers, SignalR hubs, middleware, Swagger
5. **Phase 5: Mobile App** — MAUI pages, services, platform config
6. **Phase 6: Web Dashboard** — Blazor pages, SignalR integration
7. **Phase 7: Testing** — Unit, integration, E2E, manual QA
8. **Phase 8: Deployment** — Database, API, mobile, web
9. **Phase 9: Monitoring** — Metrics, logging, alerts

### Include at the Bottom
- **Summary table** with total estimates per phase
- **Critical path** — the longest chain of dependent tasks
- **Risk areas**
- **Progress tracking table** (all tasks start as "Not Started")

---

## Phase 6: Review and Finalize

1. **Cross-check consistency** — Entities in requirements match design match tasks. No orphaned references.
2. **Verify HIPAA coverage** — Any PHI entity has encryption, audit logging, and role-based access planned.
3. **Check cross-links** — All three specs link to each other correctly.
4. **Present to user** — Summarize what was created, total estimated effort, and critical path.
5. **Set final status** — Requirements: "In Review", Design: "Draft", Tasks: "Draft" (or whatever the user agrees to).

---

## Common Pitfalls

- **Specs too vague**: "System should handle payments" is not enough. Specify payment methods, partial payments, failure handling.
- **Missing edge cases**: Always think about: null inputs, zero values, boundary dates, offline mode, expired certifications, cancelled shifts.
- **Forgetting service line differences**: In-Home Care (W-2) and Healthcare Staffing (1099) have different margin targets, employment models, and workflows.
- **Skipping non-functional requirements**: Performance, scalability, and HIPAA compliance are not optional for CarePath.
- **Tasks too large**: If a task takes more than 4 hours, break it down. Each task should be independently testable.
- **Missing file paths**: Tasks must specify exact file paths to create or modify — no ambiguity.
