# Sprint 8 Tasks - Control Plane Foundation (CP-04)

Status: Approved
Parent spec: `_specs/sprints/sprint-08-control-plane-foundation.md`
Feature specs: `_specs/01-requirements/cp-04-control-plane-foundation.md` · `_specs/02-design/cp-04-control-plane-foundation.md` · `_specs/03-tasks/cp-04-control-plane-foundation.md`
Last updated: 2026-10-21

Baseline: Sprints 1-6 complete; ADR 0003 accepted. The tenant API surface (clients, caregivers,
shifts, billing, transitions, admin users) is FROZEN in route shape and DTOs except where a
D-S8 decision below authorizes a change. Decisions D-S4-1..8, D-S5-1..8, and D-S6-1..18 remain
normative for anything this sprint touches.

This board is the normative implementation contract for Sprint 8. The per-task detail
(files, success criteria, estimates) lives in `_specs/03-tasks/cp-04-control-plane-foundation.md`.

---

## Decisions

### D-S8-1 - Delivery mode (approved by Tobi 2026-10-21)
- Specs were approved through the Claude plan review on 2026-10-21; implementation proceeds on branch `claude/friendly-brown-v1nolz` in six slices, one commit per slice, each verified by build (zero warnings), full test suite, and `dotnet-code-reviewer`.
- Owner: **Claude** for all code and docs. **Tobi** approves the board, reviews the Security/Compliance classification flag, and performs the HomeLab login verification before CP-05's table drop.

### D-S8-2 - Authoritative control-plane reads, no security cache (D-CP04-03)
- Host → organization (with status and provisioning state) is one indexed control-plane query per request. Membership status is read at login and refresh. Only public branding is cached (60 s).

### D-S8-3 - `SecurityVersion` column now, enforcement in CP-05 (D-CP04-05)

### D-S8-4 - Task numbering continues globally at TASK-079 (ADR 0001)

### D-S8-5 - Default organization values (Product Owner, 2026-10-21)
- DisplayName "CarePath Health", slug `carepath`, development hosts `localhost`, `carepath.localhost`, HomeLab `carepath.devpi.local`; production from `CONTROLPLANE__*` environment variables. Missing configuration on an empty control plane outside Development fails startup.

### D-S8-6 - Throttled unknown-host audit (D-CP04-06)

### D-S8-7 - `AuthTokenResponse.Role` nullable with `TokenKind` (D-CP04-11)

### D-S8-8 - Legacy tenant Identity tables retained; drop migration is CP-05 (D-CP04-08)

### D-S8-9 - Development seed creates `helpinghands` as a second Active organization sharing the development database (dev-only), with the seeded caregiver holding a membership there

### D-S8-10 - Pre-existing build break fixed in this sprint
- The baseline build in a fresh environment fails on NuGet audit advisories for transitive packages (`System.Security.Cryptography.Pkcs` via `Microsoft.Data.SqlClient`, `AngleSharp` via `bunit`). Patched versions are pinned centrally in `Directory.Packages.props` with direct references in the consuming projects. No behavior change.

---

## Implementation Slices

| Slice | Goal | Includes |
|---|---|---|
| 1 | Control-plane model and persistence (additive) | TASK-079..083 |
| 2 | Default organization bootstrap and host resolution (login still legacy) | TASK-084..086 |
| 3 | Identity moves to the control plane | TASK-087..091 |
| 4 | Platform and organization administration APIs | TASK-092..095 |
| 5 | Typed clients and Web | TASK-096..097 |
| 6 | Two-organization verification and close-out | TASK-098..099 |

## Task Board

**Claude** = all implementation and docs. **Tobi** = approvals, classification sign-off, HomeLab verification.

| ID | Task | Owner | Depends on | Status |
|---|---|---|---|---|
| S8-TASK-001 | Approve this board + decisions D-S8-1..10 | Tobi | - | Done 2026-10-21 via plan review |
| S8-TASK-002 | Security/Compliance acceptance of the control-plane identity record classification (design spec, HIPAA section) | Tobi | - | Pending |
| TASK-079 | Platform domain entities and enumerations | Claude | S8-TASK-001 | Not Started |
| TASK-080 | Control-plane abstractions and shared rules | Claude | TASK-079 | Not Started |
| TASK-081 | ControlPlaneDbContext, PlatformUser, configurations, unit of work | Claude | TASK-080 | Not Started |
| TASK-082 | Control-plane DI, options, connection strings, audit logger, health check | Claude | TASK-081 | Not Started |
| TASK-083 | Control-plane migrations (both providers) and shape tests | Claude | TASK-082 | Not Started |
| TASK-084 | Default organization bootstrap | Claude | TASK-083 | Not Started |
| TASK-085 | Host resolver, organization context, resolution middleware, forwarded headers, shared 401 writer | Claude | TASK-084 | Not Started |
| TASK-086 | Anonymous branding endpoint with public cache | Claude | TASK-085 | Not Started |
| TASK-087 | PlatformIdentityService and control-plane provisioning | Claude | TASK-083 | Not Started |
| TASK-088 | Legacy identity copy, membership backfill, PlatformAdmin bootstrap | Claude | TASK-084, TASK-087 | Not Started |
| TASK-089 | LoginService and membership core service | Claude | TASK-087, TASK-085 | Not Started |
| TASK-090 | Token claims, current-user context, binding, controller switch-over | Claude | TASK-088, TASK-089 | Not Started |
| TASK-091 | Tenant migration RemoveIdentityFromTenantContext, de-Identity CarePathDbContext, seed rework | Claude | TASK-090 | Not Started |
| TASK-092 | Platform and organization contracts | Claude | TASK-086 | Not Started |
| TASK-093 | PlatformOrganizationService with validators and platform audit | Claude | TASK-092, TASK-089 | Not Started |
| TASK-094 | Membership admin surface with two-phase create | Claude | TASK-092, TASK-089 | Not Started |
| TASK-095 | Platform and membership controllers, reflection tests | Claude | TASK-093, TASK-094 | Not Started |
| TASK-096 | Typed clients and route alignment tests | Claude | TASK-095 | Not Started |
| TASK-097 | Login branding and organization-aware auth state | Claude | TASK-096, TASK-090 | Not Started |
| TASK-098 | Two-organization integration suite | Claude | TASK-095 | Not Started |
| TASK-099 | Configuration, documentation, HIPAA check, lessons, board close-out | Claude | TASK-098 | Not Started |
| S8-TASK-003 | HomeLab verification: every existing account logs in after backfill (gate for CP-05 table drop) | Tobi | TASK-099 | Pending |

### Success criteria (every task)

- `dotnet build CarePath.sln` zero warnings; relevant `dotnet test` green before commit.
- No PHI, email, or host string in logs, exception messages, or platform audit events.
- No Domain types in Contracts; no new endpoint without role/policy and host-kind enforcement.

### Sequencing notes

Critical path: 079 -> 080 -> 081 -> 082 -> 083 -> 084 -> 085 -> 087 -> 088 -> 089 -> 090 -> 091 -> 092 -> 093/094 -> 095 -> 096 -> 097 -> 098 -> 099.

### Deferred (explicitly)

- TASK-100 `DropLegacyIdentityTables` (CP-05, after S8-TASK-003).
- `User.PlatformUserId` NOT NULL; `User.Role` removal; `SecurityVersion` enforcement; logout; `Admin` -> `OrganizationAdmin`; tenant precondition in object authorization (CP-05).
- Tenant database provisioning and routing; PHI audit persistence; organization-scoped storage/cache keys (CP-06).
