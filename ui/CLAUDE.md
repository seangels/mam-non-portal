@AGENTS.md

# Claude Code addendum

`ui/AGENTS.md` above (imported via `@AGENTS.md`) was written for Codex's `frontend` custom agent but defines the same role, scope, architecture, API/auth invariants, feature/data rules, environments, commands, and DX19 verification boundary for Claude Code's `frontend` subagent. Everything there applies unchanged.

- Use the `Edit`/`Write` tools for source and document changes, not `apply_patch` (Codex's edit tool).
- When the root Claude Code session delegates frontend work, it uses the `.claude/agents/frontend.md` subagent definition (`../.claude/agents/frontend.md`), the equivalent of Codex's `.codex/agents/frontend.toml`.
- The `$gv-portal-production` gate referenced here is invoked in Claude Code as the skill `gv-portal-production` (`../.claude/skills/gv-portal-production/`). Same boundary: root/infrastructure-owned, explicit invocation only.
- Memory handoff is the same file: update `../.agents/frontend/MEMORY.md` after material frontend work, per the protocol in the root `../AGENTS.md`/`../CLAUDE.md`.
