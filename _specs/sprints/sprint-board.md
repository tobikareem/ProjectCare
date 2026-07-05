# CarePath 7-Sprint Board

Status: Active
Owner: Product/Engineering
Purpose: Track the staged path from Domain foundation to web/mobile MVP while preserving Clean Architecture and HIPAA controls.

## Board

| Sprint | Goal | Key Deliverables | Exit Gate |
|---|---|---|---|
| Sprint 1 | Clean up planning and architecture baseline | Spec drift resolved, CP-02/CP-03 numbering fixed, shared-code strategy defined, compliance gates defined | Complete |
| Sprint 2 | Build persistence foundation | Complete: Infrastructure project, EF Core packages, DbContext, UTC converter, audit interceptor, Identity user foundation, CP-01 entity configurations, repositories, UnitOfWork, DI/WebApi registration, InitialCreate migration, local DB update, synthetic seed strategy, migration/seed tests, HIPAA spot-check, and reviewer verification. | Complete |
| Sprint 3 | Build application/auth/contracts boundary | In progress (2026-07-04): decisions D1–D4 ratified, `CarePath.Contracts` envelopes + enum mirrors scaffolded; remaining: Application boundary, auth foundation, DTOs, validators, typed client. Board: `_specs/sprints/sprint-03-tasks.md` | UI/API have stable contracts and business boundary |
| Sprint 4 | Build core operations backend | Caregiver/client/care plan/scheduling/VisitNote/billing APIs and workflows | Operations backend supports web/mobile MVP |
| Sprint 5 | Build Transitions backend MVP | Discharge intake, extraction stub, clinical review, activation, check-ins, escalations | 30-day transition loop works through API |
| Sprint 6 | Build Blazor web MVP | Admin/coordinator/clinician dashboard, review queue, scheduling, escalations | Web users can run operations and Transitions workflows |
| Sprint 7 | Build MAUI mobile MVP and harden | Mobile app, offline basics, GPS, reminders, Twilio, AI/OCR, secure file/photo storage, compliance hardening | End-to-end web/mobile transition workflow is demoable |

## Dependency Flow

```text
Sprint 1
  -> Sprint 2
    -> Sprint 3
      -> Sprint 4
        -> Sprint 5
          -> Sprint 6
            -> Sprint 7
```

## Definition of Ready for Any Sprint

- [ ] Prior sprint exit gate is complete.
- [ ] Related specs are reviewed and not contradictory.
- [ ] HIPAA/PHI impact is explicitly identified.
- [ ] Read/write audit impact is identified for every PHI workflow, including PHI reads.
- [ ] Object-level authorization rules are identified for every route using `{id}` to prevent IDOR.
- [ ] Files/projects to create or modify are known.
- [ ] Verification command is known.
- [ ] Third-party provider compliance is identified when SMS, voice, AI, OCR, email, or storage providers touch PHI.
- [ ] File/photo storage security requirements are identified when uploads or generated files are in scope.

## Definition of Done for Any Sprint

- [ ] Sprint stories implemented or intentionally deferred with reason.
- [ ] Build passes.
- [ ] Relevant tests pass.
- [ ] PHI and authorization checks are complete for affected code.
- [ ] No PHI appears in server logs, browser console output, local mobile logs, route names, query strings, or third-party provider logs.
- [ ] Sprint spec updated with actual status.
- [ ] PHI read operations are audit logged with user, timestamp, action, entity type, and entity id, without PHI values.
- [ ] Third-party provider gates are satisfied before any PHI is sent externally.
- [ ] File/photo storage uses private access, encryption, short-lived URLs, malware scanning, and no public blobs.
- [ ] `task.md` checkbox updated.



