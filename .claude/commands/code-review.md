---
description: Reviews uncommitted code changes on the current branch using the dotnet-code-reviewer subagent.
allowed-tools: Bash(git diff:*), Bash(git diff --staged:*), Bash(git log:*), Bash(git status:*)
---

Your job is to review uncommitted code changes on the current branch.

## Process

1. **Gather the diff** — collect both staged and unstaged changes:
   - Use `git diff` for unstaged changes
   - Use `git diff --staged` for staged changes
   - Use `git status` to see changed file list
   - If both diffs are empty, tell the user there are no changes to review and stop.

2. **Invoke the dotnet-code-reviewer subagent** to review all changed code.
   - Provide the subagent with the combined diff output
   - Tell the subagent to focus ONLY on the changed code — not the entire codebase
   - Tell the subagent to be evidence-based: reference file paths, line numbers, and code snippets

3. **Produce a unified report** with these sections:
   1. Summary (max 8 bullets of key findings)
   2. Critical issues (must-fix: bugs, security, data loss)
   3. Improvements (performance, architecture, validation)
   4. Suggestions (naming, style, modern C# features)
   5. What's done well (acknowledge good patterns)
   6. Action plan (ordered checklist of fixes)
   7. Questions/uncertainties (anything needing human clarification)

## Rules

- Do NOT edit any files yet.
- Do NOT run formatting-only changes unless they fix a cited issue.

Finish by asking:
"Do you want me to implement the action plan now?"

Wait for user confirmation before making any changes.
