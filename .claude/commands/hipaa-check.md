---
description: "Run CarePath's risk-based HIPAA/PHI engineering review. Use for PHI-adjacent changes, healthcare endpoints and DTOs, authorization, auditing, logging, storage, messaging, migrations, external providers, and pre-merge verification."
allowed-tools: Read, Glob, Grep, Bash(git diff *), Bash(git status *), Bash(rg *), Task, mcp__context7__resolve-library-id, mcp__context7__query-docs
---

# hipaa-check

Read `.agents/skills/hipaa-check/SKILL.md` completely and follow it as the canonical CarePath HIPAA/PHI engineering-review workflow.

- Apply its lightweight impact gate first.
- Run the full checklist only when the gate triggers or before merging a PHI-adjacent feature.
- Read `AGENTS.md`, `_specs/lessons.md`, and applicable approved specs as required by the canonical workflow. Use `CLAUDE.md` only for additional Claude-specific operating context.
- Return the report in the response unless the user explicitly requests a persisted report or an approved task requires one.
- Do not claim legal or operational HIPAA compliance from a code review.
