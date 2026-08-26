@AGENTS.md

# Claude Code addendum

`AGENTS.md` above (imported via `@AGENTS.md`) was written for Codex sessions but applies to Claude Code the same way: roles, ownership, required reading order, durable memory protocol, shared engineering rules, git workflow authorization, default verification, and the legacy `tasks.md` note all apply unchanged. The notes below only cover where Claude Code's tooling differs from Codex's.

## Tool differences from Codex

- Use the `Edit`/`Write` tools for source and document changes. `AGENTS.md`'s instruction to "use `apply_patch`" refers to Codex's edit tool and does not apply in Claude Code.
- Delegating backend/frontend work to a subagent uses Claude Code's `Agent` tool with the project-scoped definitions in `.claude/agents/backend.md` and `.claude/agents/frontend.md` — the Claude-side equivalents of Codex's `.codex/agents/backend.toml` and `.codex/agents/frontend.toml`. Same ownership split: the `backend` agent owns `api/`, the `frontend` agent owns `ui/`. A new chat still gets new runtime agent instances; the durable handoff remains the nested `AGENTS.md`/`CLAUDE.md` files and `.agents/**/MEMORY.md`.
- The production build/package/deploy gate described in `AGENTS.md` under "Production build and deployment gate" is invoked in Claude Code as the skill `gv-portal-production` (defined in `.claude/skills/gv-portal-production/`), the Claude-side port of Codex's `.codex/skills/gv-portal-production/`. Same rule applies: run it only on an explicit user invocation naming this skill; completing normal implementation, tests, review, or a milestone is not permission to run it. An invocation without a mode means non-deploying `build`; only an explicit `deploy` request authorizes IIS/hosts/certificate/`C:\inetpub`/database changes.
- `.codex/` (`config.toml`, `agents/*.toml`, `skills/*`) is Codex-only runtime configuration. Do not edit it from a Claude Code session unless the user is explicitly asking to update Codex's setup too. If a shared rule in `AGENTS.md`/`docs/**` changes, mirror the change into the Claude-side equivalents listed above so both agents stay in sync.

## Durable memory protocol

Same as `AGENTS.md`: read and update `.agents/shared/MEMORY.md` and the relevant `.agents/{backend,frontend}/MEMORY.md`. This memory is agent-agnostic and shared between Codex and Claude Code sessions — it records decisions and verified facts about the codebase, not which CLI produced them. Do not write Claude-specific or Codex-specific asides into it.
