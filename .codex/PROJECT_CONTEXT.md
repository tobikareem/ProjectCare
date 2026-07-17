# CarePath Project Context

Snapshot date: July 16, 2026

This is a curated orientation snapshot. It does not replace `AGENTS.md`, `_specs/lessons.md`, or approved feature specs.

## Product and Architecture

CarePath Health supports Maryland in-home care and healthcare staffing. Its dependency direction is:

```text
Domain <- Application <- Infrastructure <- WebApi
```

Client-safe contracts and reusable client code are separated into `CarePath.Contracts`, `CarePath.Client`, and `CarePath.Client.UI`. `CarePath.Web` is the Blazor web application. Domain entities must never cross into client projects.

## Current Solution

The solution currently contains:

- `Domain` and `Domain.Tests`
- `Application` and `Application.Tests`
- `Infrastructure` and `Infrastructure.Tests`
- `WebApi`
- `CarePath.Contracts`
- `CarePath.Client`
- `CarePath.Client.UI`
- `CarePath.Web` and `CarePath.Web.Tests`

The repository targets .NET 10 and C# 14. EF Core persistence, migrations, Identity foundations, application use cases, contracts, typed clients, shared UI, API surfaces, and Transitions backend components are present. Consult the sprint board and approved specs for completion status at task granularity.

## Source-of-Truth Order

1. Current user instruction.
2. `AGENTS.md` and `_specs/lessons.md`.
3. Approved requirements.
4. Approved design.
5. Approved tasks.
6. Current code when it records an approved deviation.

Resolve requirements/design conflicts before implementation. When design and tasks disagree, update the task spec first.

## Compliance Boundary

Treat `Client`, `CarePlan`, `Shift`, `VisitNote`, `VisitPhoto`, `CaregiverCertification`, `DischargeDocument`, `TransitionPlan`, `TransitionInstruction`, and `TransitionCheckIn` as PHI.

Reviews must verify role and object authorization, minimum-necessary DTOs, append-only PHI access auditing, private media storage, encryption, retention-safe soft deletion, and absence of PHI from logs, exception bodies, URLs, telemetry, and broad SignalR messages.

Do not describe the application as operationally HIPAA-compliant without verifying deployed administrative, physical, and technical controls.

## Durable Implementation Rules

- Repository interfaces live in `Domain/Interfaces/Repositories`.
- Use UTC timestamps, `Guid` keys, `BaseEntity`, and soft deletion.
- Filter or page high-volume `Shift` and `VisitNote` queries.
- Deployed PHI-bearing migrations should be corrected forward; rollback must fail closed when retained records could be destroyed.
- The UI source of truth is `Documentation/Wireframes/carepath-wireframe.html`, with design rules in `_specs/02-design/ui-design-system.md`.
- Historical Claude reviewer notes are supporting history only; resolved findings must not override current code or approved specs.
