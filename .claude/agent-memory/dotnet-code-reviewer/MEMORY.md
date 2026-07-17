# CarePath Health — Code Reviewer Memory

Last refreshed: 2026-07-16

This file is a compact orientation aid, not a source of truth. Read `AGENTS.md`, `_specs/lessons.md`, and the applicable approved specs before every review. Do not preserve resolved findings here.

## Sprint 6 (current)

- Active board: `_specs/sprints/sprint-06-tasks.md` — decisions D-S6-1..10, the locked
  S6-TASK-038/039 caregiver/shift-matching contract surface, and the task table are all in
  this one file. Read the relevant D-S6-N decision block before reviewing Sprint 6 work.
- Codex and Claude edit the same working tree concurrently. Backend reviews (Application/
  Infrastructure/WebApi/Domain) may be requested while Codex is mid-edit on `CarePath.Web`/
  `CarePath.Client` — respect stated out-of-scope boundaries in the review request rather than
  assuming the whole diff is reviewable.
- Sprint 6 intentionally superseded the Sprint 4 endpoint role matrix in places (e.g. caregiver
  self-access to `GET /api/caregivers/{id}` was removed per the locked contract). Check
  sprint-06-tasks.md before flagging a role-gate narrowing as a regression.

## Current Platform

- Runtime: .NET 10 / C# 14.
- Architecture: Domain → Application → Infrastructure → WebApi.
- Shared clients: `CarePath.Contracts`, `CarePath.Client`, `CarePath.Client.UI`, and `CarePath.Web`.
- Tests: `Domain.Tests`, `Application.Tests`, `Infrastructure.Tests`, and `CarePath.Web.Tests`.
- Repository interfaces live in `Domain/Interfaces/Repositories`; Infrastructure supplies implementations.

## Review Priorities

- Correctness, security, HIPAA/PHI exposure, authorization, and data-retention safety first.
- All entities use `Guid`, inherit `BaseEntity`, use UTC timestamps, and soft-delete clinical records.
- PHI access needs role and object authorization plus separate append-only audit events.
- Never approve PHI in logs, exception bodies, URLs, telemetry, or public media locations.
- Application validation uses FluentValidation; collection repository results use `IReadOnlyList<T>`.
- `Shift` and `VisitNote` queries must be filtered or paged.
- Review migrations for destructive `Down` behavior; deployed PHI-bearing changes should use forward corrective migrations.

## Current Feature Context

- CP-01 Domain and CP-02 Infrastructure foundations are implemented.
- Application, contracts, typed clients, shared UI, Web API surfaces, and the Blazor web project now exist.
- CP-03 Transitions includes domain and backend work; its clinical entities and document content are PHI.
- The wireframe and `_specs/02-design/ui-design-system.md` govern UI implementation.

Keep detailed review results in the review response or explicitly requested review artifacts. Put durable corrections in `_specs/lessons.md`.
