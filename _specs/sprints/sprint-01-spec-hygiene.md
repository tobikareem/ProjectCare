# Sprint 1 - Spec Hygiene & Architecture Baseline

Status: Completed
Primary outcome: make the project safe to implement from specs without ambiguity.

## Decisions Applied

- Infrastructure / EF Core remains `CP-02`.
- CarePath Transitions is renumbered to `CP-03`.
- Task numbers are historical identifiers, not spec identifiers; completed Transitions Domain tasks remain `TASK-020` through `TASK-024`, while Infrastructure tasks remain `TASK-040+`.
- `Shift.GrossMargin` means total shift margin (`(BillRate - PayRate) * BillableHours`). Future hourly spread metrics must use an explicit name such as `HourlyGrossMargin`.
- Repository implementations live in `Infrastructure/Persistence/Repositories`.
- The EF Core DbContext lives at `Infrastructure/Persistence/CarePathDbContext.cs`.
- `CarePlan` lives at `Domain/Entities/Clinical/CarePlan.cs`.

Decision records:

- `_specs/decisions/0001-spec-numbering.md`
- `_specs/decisions/0002-shift-margin-semantics.md`

## Scope Completed

- [x] Resolved spec numbering/status ambiguity.
- [x] Normalized file path conventions.
- [x] Decided `Shift.GrossMargin` meaning.
- [x] Documented shared-code architecture for Blazor WebAssembly and MAUI Blazor Hybrid.
- [x] Defined global HIPAA, read-audit, object-authorization, provider, and file-storage gates.
- [x] Scheduled template WebApi artifact removal in Sprint 4.

## Stories

### Story 1 - Resolve duplicate CP tracks

```gherkin
Feature: Spec governance
  Scenario: Engineer selects the next implementation spec
    Given the repo previously had both Infrastructure and Transitions labeled CP-02
    When an engineer opens the sprint board
    Then the next prerequisite milestone is unambiguous
    And the Infrastructure and Transitions specs do not share conflicting identifiers
```

Acceptance:

- [x] Infrastructure remains CP-02; Transitions becomes CP-03.
- [x] Filenames/headings/status references updated consistently.
- [x] `_specs/PROGRESS.md`, `_specs/lessons.md`, `AGENTS.md`, and `CLAUDE.md` references updated.

### Story 2 - Normalize architecture paths

```gherkin
Feature: Architecture documentation consistency
  Scenario: Developer creates a new repository implementation
    Given the developer reads AGENTS.md, CLAUDE.md, and sprint specs
    When they look for the repository implementation path
    Then every document points to Infrastructure/Persistence/Repositories
```

Acceptance:

- [x] Standardized `Infrastructure/Persistence/Repositories`.
- [x] Standardized `Infrastructure/Persistence/CarePathDbContext.cs`.
- [x] Standardized `Domain/Entities/Clinical/CarePlan.cs`.

### Story 3 - Decide margin semantics

```gherkin
Feature: Margin calculation clarity
  Scenario: Coordinator views shift profitability
    Given a completed shift has bill rate, pay rate, and billable hours
    When the system displays margin
    Then the displayed metric name clearly distinguishes hourly margin from total shift margin
```

Acceptance:

- [x] `GrossMargin = (BillRate - PayRate) * BillableHours` is the total shift margin.
- [x] Future dashboard/contracts work must use an explicit `HourlyGrossMargin = BillRate - PayRate` metric if hourly spread is needed.
- [x] Silent metric ambiguity is blocked by Decision 0002.

### Story 4 - Define shared-code boundary

```gherkin
Feature: Shared client architecture
  Scenario: Web and mobile both need shift data
    Given Domain entities are persistence-oriented and PHI-sensitive
    When Blazor WebAssembly and MAUI request shift data
    Then both clients use DTOs from shared contracts
    And neither client binds directly to Domain entities
```

Acceptance:

- [x] Defined `CarePath.Contracts`, `CarePath.Client`, and `CarePath.Client.UI`.
- [x] Documented what can be shared: DTOs, typed clients, UI primitives, validation helpers.
- [x] Documented what must not be shared: full pages, platform services, Domain entities.

### Story 5 - Define compliance gates

```gherkin
Feature: Cross-sprint compliance gates
  Scenario: A developer starts a PHI-adjacent story
    Given the story touches Client, CarePlan, Shift, VisitNote, VisitPhoto, CaregiverCertification, or Transitions data
    When the sprint task is marked ready
    Then read/write audit requirements are identified
    And object-level authorization requirements are identified
    And no-PHI logging rules are listed as acceptance criteria
```

Acceptance:

- [x] Added read audit to the global Definition of Done.
- [x] Added IDOR/object-level authorization to API sprint gates.
- [x] Added Twilio BAA/consent/opt-out and file-storage hardening as explicit future gates.

## Follow-Up Tickets

- Sprint 4: remove template `WeatherForecast` controller/model/http sample before core API work is considered complete.
- Sprint 3: create `CarePath.Contracts`, `CarePath.Client`, and shared DTO/client conventions before UI work begins.
- Sprint 7: verify Twilio BAA, patient consent, opt-out, webhook verification, minimum necessary SMS/voice content, encrypted private file/photo storage, short-lived URLs, and malware scanning.

## Exit Gate

- [x] All specs agree on next implementation order.
- [x] No duplicate active CP identifiers.
- [x] Shared-code architecture is documented.
- [x] Compliance gates are visible in the sprint board and tracker.
- [x] Build/test still pass.