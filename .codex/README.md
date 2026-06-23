# CarePath Codex Configuration

This directory is the Codex-native representation of the repository's `.claude` setup. Codex uses more than one repository surface:

- `AGENTS.md` for durable project instructions.
- `.codex/config.toml` for project settings and MCP servers.
- `.codex/agents/*.toml` for custom subagents.
- `.codex/rules/*.rules` for command approval policy.
- `.codex/PROJECT_CONTEXT.md` for a curated repository-orientation snapshot.
- `.agents/skills/*/SKILL.md` for reusable project workflows.
- `_specs/lessons.md` for durable project lessons.

## Product Context

CarePath Health is intended to operate two Maryland healthcare service lines from one platform:

1. In-home care delivered by W-2 caregivers, with scheduling, GPS-verified visits, care plans, visit documentation, family visibility, billing, and a 40-45% gross-margin target.
2. Facility staffing delivered primarily by 1099 clinicians, with credential-aware scheduling, per-diem or contract staffing, billing, and a 25-30% gross-margin target.

The software is meant to replace spreadsheet, phone, text-message, and paper workflows with auditable scheduling, credentialing, clinical documentation, billing, margin analytics, and eventually caregiver/mobile and administrator/web applications.

## Current Maturity

As of June 22, 2026:

- Domain foundation: implemented with entities, enumerations, repository abstractions, business calculations, XML documentation, and a substantial unit-test suite.
- WebApi: scaffold only; the WeatherForecast sample remains.
- Application, Infrastructure, EF Core, SQL Server, authentication, authorization, PHI audit logging, SignalR, MAUI, and Blazor: not implemented.
- The project is not yet operationally HIPAA-compliant. Domain fields and soft-delete flags are foundations, not enforcement.

## Source-of-Truth Order

For implementation decisions:

1. Current user instruction.
2. `AGENTS.md` and `_specs/lessons.md`.
3. Approved requirements spec for intended behavior and acceptance criteria.
4. Approved design spec for technical implementation decisions.
5. Approved task spec.
6. Current code when it intentionally records an approved deviation.
7. `Documentation/Architecture.md` and business documents as product context.

If design and tasks disagree, update the task spec before implementation. If requirements and design disagree, stop and resolve the specs rather than choosing one silently. Do not infer unresolved authentication, persistence, encryption, or retention decisions.

## Claude-to-Codex Mapping

| Claude source | Codex representation |
|---|---|
| `.claude/agents/dotnet-code-reviewer.md` | `.codex/agents/dotnet-code-reviewer.toml` |
| `.claude/commands/code-review.md` | `.agents/skills/code-review/SKILL.md` |
| `.claude/commands/commit-message.md` | `.agents/skills/commit-message/SKILL.md` |
| `.claude/commands/dotnet-code.md` | `.agents/skills/dotnet-code/SKILL.md` |
| `.claude/commands/hipaa-check.md` | `.agents/skills/hipaa-check/SKILL.md` |
| `.claude/commands/migration.md` | `.agents/skills/migration/SKILL.md` |
| `.claude/commands/new-spec.md` | `.agents/skills/new-spec/SKILL.md` |
| `.claude/mcp.json` | `.codex/config.toml` |
| `.claude/settings.local.json` | `.codex/rules/carepath.rules` for the safe shared subset; remaining permissions stay user-local |
| `.claude/agent-memory/.../MEMORY.md` | Curated project state is in `.codex/PROJECT_CONTEXT.md`; stable rules belong in `AGENTS.md`; corrections belong in `_specs/lessons.md`; generated Codex memory remains user-local |

GitHub and browser capabilities are supplied by the installed Codex GitHub and Browser plugins, so raw duplicate MCP entries are intentionally omitted.

## Security Notes

- The Context7 key found in `.claude/mcp.json` was not copied. Rotate that key and set `CONTEXT7_API_KEY` locally.
- Do not commit API keys, connection strings, JWT signing keys, patient data, or production database details.
- PHI access requires authorization, ownership checks, minimal DTOs, append-only read/write/delete auditing, private media storage, encryption, and retention controls. Those controls do not exist yet.

## Known Documentation Drift

Before the next implementation phase, reconcile these items in the approved specs:

- Several documents use planned `src/CarePath.*`, `CarePath.Api`, `Enums/`, and test paths that do not match the current root-level `Domain`, `Domain.Tests`, `WebApi`, and `Enumerations` layout.
- The CP-01 task status contains the typo `Approvedm`.
- The CP-01 requirements describe the pre-implementation state ("No domain model exists").
- Authentication design text references `PasswordHash` and `RefreshToken`, but the current `User` entity does not contain them.
- The design's shift margin and signature-storage examples differ from the implemented model.
- The infrastructure threat model must distinguish SQL Server TDE, application/column encryption, private blob storage, and access auditing.
