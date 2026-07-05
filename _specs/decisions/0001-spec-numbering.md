# Decision 0001 - Spec Numbering Baseline

Status: Accepted
Date: 2026-06-26

## Context

The repository had two active CP-02 tracks:

- Infrastructure / EF Core foundation
- CarePath Transitions

Infrastructure is the next prerequisite layer before Application, API, web, mobile, or Transitions backend work can proceed. Transitions is approved at the Domain level, but its backend workflow depends on Infrastructure and Application foundations.

## Decision

Infrastructure / EF Core remains `CP-02`.

CarePath Transitions is renumbered to `CP-03`.

The implementation order is:

1. `CP-01` - Domain layer
2. `CP-02` - Infrastructure / EF Core foundation
3. `CP-03` - CarePath Transitions domain and backend workflow

Task numbers are historical work item identifiers, not spec identifiers. Completed Transitions Domain tasks remain `TASK-020` through `TASK-024`; Infrastructure implementation tasks remain `TASK-040+`.

## Consequences

- Transitions spec files use `cp-03-transitions.md`.
- Existing completed Transitions Domain work remains valid, but references must use `CP-03`.
- Sprint 2 owns Infrastructure implementation before additional Application/API/UI work begins.