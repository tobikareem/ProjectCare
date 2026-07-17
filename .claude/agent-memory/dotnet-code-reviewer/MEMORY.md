# CarePath Health — Code Reviewer Memory

Last refreshed: 2026-07-16

This file is a compact orientation aid, not a source of truth. Read `AGENTS.md`, `_specs/lessons.md`, and the applicable approved specs before every review. Do not preserve resolved findings here.

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
