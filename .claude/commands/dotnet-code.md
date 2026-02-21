---
description: "Implement .NET features from specs or user instructions, then review with the dotnet-code-reviewer subagent. Use when asked to build, implement, code, or create a feature — e.g., 'implement CP-02', 'code the Shift endpoint', 'build the Application layer'."
allowed-tools: Read, Write, Edit, Glob, Grep, Task, Bash(dotnet build *), Bash(dotnet test *), Bash(dotnet run *), Bash(dotnet ef *), Bash(git diff *), Bash(git status *), mcp__context7__resolve-library-id, mcp__context7__query-docs
---

## Process

1. **Understand the task** — Read the user's instructions or referenced spec in `_specs/`. If a spec exists, follow it precisely.

2. **Implement the code** — Write clean C# following Clean Architecture and all conventions in CLAUDE.md. Use Context7 to look up .NET APIs or library usage when uncertain.

3. **Build and verify** — Run `dotnet build CarePath.sln`. Fix any compilation errors. If tests exist for the changed area, run `dotnet test CarePath.sln`.

4. **Review** — Use the Task tool to invoke the **dotnet-code-reviewer** subagent to review all new/changed code. Implement any critical or improvement suggestions.

5. **Iterate** — If the reviewer found issues, fix them, rebuild, and re-review. Stop after 3 review cycles maximum. If critical issues remain after 3 cycles, report them to the user with context.

6. **Report** — Summarize what was implemented, files created/modified, review findings, and what was fixed.
