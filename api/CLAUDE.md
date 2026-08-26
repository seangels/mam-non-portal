@AGENTS.md

# Claude Code addendum

`api/AGENTS.md` above (imported via `@AGENTS.md`) was written for Codex's `backend` custom agent but defines the same role, scope, architecture, contract, migration, build/test, and Definition of Done rules for Claude Code's `backend` subagent. Everything there applies unchanged.

- Use the `Edit`/`Write` tools for source and document changes, not `apply_patch` (Codex's edit tool).
- When the root Claude Code session delegates backend work, it uses the `.claude/agents/backend.md` subagent definition (`../.claude/agents/backend.md`), the equivalent of Codex's `.codex/agents/backend.toml`.
- Memory handoff is the same file: update `../.agents/backend/MEMORY.md` after material backend work, per the protocol in the root `../AGENTS.md`/`../CLAUDE.md`.
