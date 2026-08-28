# Cursor AI Configuration

Project-local Cursor tooling for the NWC template. Structure mirrors `.claude/` so Claude Code and Cursor stay aligned.

```
.cursor/
├── README.md           ← You are here
├── hooks.json          ← Registered Cursor hooks
├── rules/              ← Persistent agent rules (.mdc)
├── skills/             ← Workflow skills (SKILL.md per folder)
├── agents/             ← Specialist personas + routing table
└── hooks/              ← Hook documentation (scripts live in .claude/hooks/)
```

## Quick Start

1. **Rules** load automatically — no setup required after clone.
2. **Skills** — ask the agent to use `scaffold-slice` or `health-check`.
3. **MCP** — build Roslyn navigator once: `dotnet build tools/CWM.RoslynNavigator/CWM.RoslynNavigator.slnx`
4. **Full guide** — see [CURSOR.md](../CURSOR.md) at the repo root.

## Adding New Rules or Skills

- **Rules:** add `.mdc` files under `rules/` with `description`, `globs`, and/or `alwaysApply` frontmatter.
- **Skills:** add `skills/<name>/SKILL.md` with `name` and `description` in frontmatter.

Do not create skills under `~/.cursor/skills-cursor/` — that directory is reserved for Cursor built-ins.
