# Persistent agent memory

The live backend/frontend subagent processes exist only inside one chat thread. This directory makes their useful context durable across new sessions.

## Memory map

- `shared/MEMORY.md`: cross-stack contracts, deployment decisions, current handoff, and workspace-wide risks.
- `backend/MEMORY.md`: backend architecture, security invariants, database/API state, commands, and backend verification.
- `frontend/MEMORY.md`: frontend architecture, auth/setup behavior, environment configuration, commands, and frontend verification.
- `../tasks.md`: detailed chronological status and execution log.
- `../api/plan.md`: REST API contract and business rules.

## How to resume in a new chat

1. Start from the workspace root so root `AGENTS.md` is in scope.
2. Codex loads the project custom-agent definitions from `../.codex/agents/backend.toml` and `../.codex/agents/frontend.toml` when the project is trusted.
3. Recreate the named `backend` subagent for `api/` and the named `frontend` subagent for `ui/` when their work is requested.
4. Each custom agent is already instructed to read its nested `AGENTS.md`, role memory, shared memory, and relevant `tasks.md` entries before acting.
5. Recheck all runtime state. Containers, local processes, IIS bindings, ports, certificates, databases, ignored artifacts, and release ZIP files may have changed or may not exist in a fresh clone.

## Maintenance rules

- Store decisions and verified facts, not raw conversation transcripts.
- Keep the `Last updated` date and `Last verified` section accurate.
- Replace obsolete current-state facts and preserve only decisions that still constrain implementation.
- Put secrets nowhere in `.agents/`. Refer to the configuration key name or secret store, never its value.
- If backend and frontend disagree, update `shared/MEMORY.md` and `api/plan.md` as part of resolving the contract.
