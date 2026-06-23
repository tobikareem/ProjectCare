---
name: "commit-message"
description: "Create a conventional commit message from staged git changes. Use when the user wants to commit, needs help writing a commit message, or asks to prepare staged changes."
---

# commit-message

Use this skill for the CarePath staged-change commit-message workflow.

Inspect staged changes with `git status` and `git diff --staged`. Also check `git log --oneline -5` to match the repository's existing commit style.

Propose a commit message in present tense, explaining "why" not just "what". Use this format:

```
<emoji> <type>: <concise description>

<optional body explaining why>
```

Allowed types:

- ✨ `feat:` — New feature
- 🐛 `fix:` — Bug fix
- 🔨 `refactor:` — Refactoring
- 📝 `docs:` — Documentation
- 🎨 `style:` — Formatting
- ✅ `test:` — Tests
- ⚡ `perf:` — Performance

## Workflow

1. Show a brief summary of what is staged.
2. Propose the commit message with the appropriate emoji and type.
3. Ask for user confirmation before committing. Do NOT auto-commit.
