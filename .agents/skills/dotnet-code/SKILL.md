---
name: "dotnet-code"
description: "Implement .NET features from specs or user instructions, then review with the dotnet-code-reviewer subagent. Use when asked to build, implement, code, or create a feature — e.g., 'implement CP-02', 'code the Shift endpoint', 'build the Application layer'."
---

# dotnet-code

Use this skill for spec-driven CarePath implementation work.

## Process

1. **Understand the task** — Read `AGENTS.md`, `_specs/lessons.md`, the user's instructions, and all applicable requirements, design, and task specs. Do not implement a feature whose relevant spec is not approved. If design and tasks conflict, update the task spec first. If requirements and design conflict, stop and resolve the specs before coding.

2. **Implement the code** — Write clean C# following Clean Architecture and all conventions in AGENTS.md. Use Context7 to look up .NET APIs or library usage when uncertain.

3. **Build and verify** — Run `dotnet build CarePath.sln`. Fix any compilation errors. If tests exist for the changed area, run `dotnet test CarePath.sln`.

4. **Review** — Invoke the **dotnet-code-reviewer** subagent to review all new/changed code. Implement any critical or improvement suggestions.

5. **Iterate** — If the reviewer found issues, fix them, rebuild, and re-review. Stop after 3 review cycles maximum. If critical issues remain after 3 cycles, report them to the user with context.

6. **Report** — Summarize what was implemented, files created/modified, review findings, and what was fixed.
