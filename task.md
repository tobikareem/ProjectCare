# CarePath Sprint Execution Tracker

Last updated: 2026-07-04  
Status: Sprint 3 in progress - decisions ratified, Contracts scaffolded, task board published

This tracker is the working checklist for moving CarePath from a Domain-only foundation into a usable backend, Blazor web app, and MAUI Blazor Hybrid mobile app. The detailed sprint specs live in `_specs/sprints/`.

## North Star

Build one shared healthcare operations backbone for:

1. In-home care and staffing operations: caregivers, clients, credentials, scheduling, GPS check-in/out, VisitNotes, billing, and margin analytics.
2. CarePath Transitions: 30-day post-discharge support using discharge note intake, clinician verification, reminders/check-ins, caregiver observations, coordinator escalations, and outcome reporting.

## Progress Checklist

| Sprint | Theme | Status | Spec |
|---|---|---:|---|
| Sprint 1 | Spec Hygiene & Architecture Baseline | [x] Complete | `_specs/sprints/sprint-01-spec-hygiene.md` |
| Sprint 2 | Infrastructure Foundation | [x] Complete | `_specs/sprints/sprint-02-infrastructure-foundation.md` |
| Sprint 3 | Application, Auth & Shared Contracts | [~] In Progress (board: `_specs/sprints/sprint-03-tasks.md`) | `_specs/sprints/sprint-03-application-auth-contracts.md` |
| Sprint 4 | Core Operations Backend | [ ] Not Started | `_specs/sprints/sprint-04-core-operations-backend.md` |
| Sprint 5 | CarePath Transitions Backend MVP | [ ] Not Started | `_specs/sprints/sprint-05-transitions-backend-mvp.md` |
| Sprint 6 | Blazor Web App MVP | [ ] Not Started | `_specs/sprints/sprint-06-blazor-web-mvp.md` |
| Sprint 7 | MAUI Mobile MVP, Notifications, AI & Hardening | [ ] Not Started | `_specs/sprints/sprint-07-mobile-notifications-ai-hardening.md` |

## Cross-Sprint Acceptance Gates

- [x] `dotnet build CarePath.sln` passes with zero warnings and zero errors.
- [x] Relevant `dotnet test` suites pass before moving to the next sprint.
- [ ] No Domain dependency on Application, Infrastructure, WebApi, Web, or Mobile.
- [ ] No UI binds directly to Domain entities; UI consumes DTO/contracts from `CarePath.Contracts`.
- [ ] PHI endpoints use role-based authorization and object-level authorization.
- [ ] PHI read/write/update/delete operations are audit logged without logging PHI values.
- [ ] Every `{id}` API route has object-level authorization to prevent IDOR.
- [ ] No `DateTime.Now`; use `DateTime.UtcNow`.
- [ ] No hard deletes for clinical/PHI data; use soft delete.
- [ ] No PHI in logs, exception messages, URLs, or SMS bodies beyond minimum necessary content.
- [ ] SMS/voice delivery has BAA, consent, opt-out, webhook verification, and message minimization requirements.
- [ ] File/photo storage has encryption, access control, short-lived URLs, malware scanning, and no public blobs.
- [ ] Transitions reminders never send before `TransitionPlan.Status == Active`.
- [ ] AI extraction never activates or changes a care plan without clinician approval.

## Current Known State

- [x] CP-01 Domain layer exists and tests pass.
- [x] CP-03 Transitions Domain entities/enums/repository interfaces exist.
- [x] `dotnet build CarePath.sln` passes.
- [x] `dotnet test CarePath.sln` passes.
- [x] Infrastructure project is scaffolded and builds.
- [ ] Application project is not scaffolded.
- [ ] Blazor WebAssembly app is not scaffolded.
- [ ] MAUI Blazor Hybrid app is not scaffolded.
- [ ] Shared contracts/client/UI libraries are not scaffolded.
- [ ] WebApi still contains template WeatherForecast code; scheduled for Sprint 4 cleanup before core API completion.

## Immediate Next Step

Execute the Sprint 3 board at `_specs/sprints/sprint-03-tasks.md`. Critical path: S3-TASK-010 (normalize Application scaffolds) → S3-TASK-020 (Clinician=6) → S3-TASK-030+ (Application boundary). `CarePath.Contracts` envelopes and enum mirrors are scaffolded; its CarePath.sln entry needs re-adding (`dotnet sln add CarePath.Contracts/CarePath.Contracts.csproj`) after a concurrent solution rewrite dropped it. CP-03 Transitions persistence remains deferred until explicit Transitions configurations are approved.



