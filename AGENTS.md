# Workspace agent rules

This file applies to the entire `api-portal` workspace and is the durable entry point for future Codex sessions.

## Roles and ownership

- Orchestrator/root owns cross-cutting work: `AGENTS.md`, `.agents/`, `docs/tasks/`, `deploy/`, release coordination, and API/UI contract decisions.
- Backend subagent owns `api/`. Read and follow `api/AGENTS.md` before changing backend files.
- Frontend subagent owns `ui/`. Read and follow `ui/AGENTS.md` before changing frontend files.
- Do not edit another role's owned folder unless the task explicitly requires a coordinated contract change. Record contract changes in shared memory before handing off.

Runtime subagent processes do not survive a new chat. Recreate the `backend` and `frontend` subagents when needed; their durable state is stored in the repository files below.

## Project custom agents

- Project-scoped definitions live in `.codex/agents/backend.toml` and `.codex/agents/frontend.toml`.
- For backend implementation, delegate to the custom agent named `backend`. For frontend implementation, delegate to `frontend`.
- When work spans both `api/` and `ui/`, spawn both agents with separate ownership and let the orchestrator coordinate contract decisions and shared files.
- A new chat gets new runtime agent processes. The custom definitions, nested `AGENTS.md` files, and `.agents/**/MEMORY.md` files are the durable handoff; never assume an old process is still running.

## Required reading at the start of a session

1. Read this file.
2. Read `.agents/README.md` and `.agents/shared/MEMORY.md`.
3. Backend work: read `api/AGENTS.md`, `.agents/backend/MEMORY.md`, `docs/plans/01-BASE-admin-portal.md`, `docs/requirements/README.md`, and the relevant feature plan/requirement in `docs/`.
4. Frontend work: read `ui/AGENTS.md`, `.agents/frontend/MEMORY.md`, `docs/requirements/README.md`, and the relevant contract sections in `docs/plans/` used by the UI.
5. Read `docs/tasks/README.md`, then the relevant feature `status.md`/`log.md` under `docs/tasks/`.
6. Recheck runtime facts such as running processes, containers, ports, database contents, IIS state, and generated artifacts. Never treat ephemeral state in memory as guaranteed current.

## Durable memory protocol

- `.agents/shared/MEMORY.md` contains cross-stack decisions and handoff state.
- `.agents/backend/MEMORY.md` and `.agents/frontend/MEMORY.md` contain role-specific architecture, commands, known risks, and last verification.
- Update the appropriate memory file after a material implementation, contract change, deployment change, new known risk, or verification result.
- Keep memory concise and evidence-based. Update the current-state sections instead of pasting chat transcripts or endlessly appending logs.
- Put detailed chronological execution status in `docs/tasks/**`; memory should explain what a future agent must know to continue safely.
- Include the date, affected files/contracts, verification commands/results, and any genuine next action.
- Never store passwords, connection strings with credentials, JWT keys, tokens, cookies, private keys, personal data, or `.env` contents in memory.

## Shared engineering rules

- `docs/plans/01-BASE-admin-portal.md` is the base REST contract; numbered feature plans in `docs/plans/` extend it in dependency order. Coordinate contract changes between backend and frontend.
- Product requirements live under `docs/requirements/`, indexed by `docs/requirements/README.md`. New or changed requirements must be added there, not in root files.
- Preserve user changes and the dirty worktree. Do not reset or overwrite unrelated files.
- Use `apply_patch` for source/document edits.
- Keep code clean, small, readable, and aligned with the existing architecture. Do not add abstractions without a concrete need.
- Update the relevant `docs/tasks/<NN-CODE>/status.md` and `log.md` continuously for multi-step implementation work.
- New task detail files under `docs/tasks/**/details/` must include a short, easy-to-read `Tóm tắt ngắn` section near the top before detailed scope/checklists. The summary should list every main requirement/decision in concise numbered bullets, each with the status icon (`⬜`, `🔄`, `✅`, `⚠️`) used by detail checklists.
- Run verification proportional to the change and record the result in the owning memory file.
- Generated `artifacts/` and `release/` are ignored; rebuild and verify them rather than assuming they exist in another clone.

## Git workflow authorization

- The user authorizes the orchestrator to create local Git commits proactively after a cohesive, reviewed milestone.
- Inspect status and diff first, stage exact in-scope paths, and use a concise commit message containing the relevant development code such as `ATT-BE-03`.
- Do not include unrelated user/agent changes in a commit. Shared cross-stack commits are coordinated by the orchestrator.
- This permission covers local commits only. Do not push, force-push, merge, rebase, amend published history, create tags, or open a pull request unless the user separately requests it.

## Production build and deployment gate

- Production build, IIS package creation, package verification, and deployment are opt-in operations owned by the project skill `.codex/skills/gv-portal-production`.
- Run them only when the user explicitly invokes `$gv-portal-production`. The skill disables implicit invocation; completing normal implementation, tests, review, or a milestone is not permission to run it.
- An invocation without a mode means non-deploying `build`. Only an explicit `deploy` request authorizes changes to IIS, hosts, certificate stores, `C:\inetpub`, or the target database.
- Outside that skill, do not run `npm --prefix ui run build` with the default production configuration, `deploy/iis/build-iis-package.ps1`, `deploy/iis/deploy-iis.ps1`, or `dotnet publish` for release packaging.

## Default verification

Backend:

    dotnet build api/AdminPortal.slnx --no-restore
    dotnet test api/tests/AdminPortal.UnitTests --no-restore

Frontend:

    npm --prefix ui run build -- --configuration development
    npm --prefix ui run test:ci

Production/IIS verification is intentionally excluded from the default gate. Invoke `$gv-portal-production` when it is required.

Tests that require a running Docker engine are **not part of the required gate**. The backend integration suite (`dotnet test api/tests/AdminPortal.IntegrationTests -c Release --no-restore`) uses Testcontainers PostgreSQL and therefore needs Docker. Agents are not required to start Docker or run it. Run it only when Docker is already available and the change touches auth/setup/persistence/API contract, or when the user explicitly asks for it. When it is not run, record it as "not run (Docker not available)" in the owning memory/task log — this is not a blocker and does not hold up marking a task done. Never mark it passed without running it, and never substitute a non-PostgreSQL provider.

## Legacy task file

- Root `tasks.md` is frozen legacy material only. Do not add to it or edit it for new work.
- The archived reference copy is `docs/tasks/archive/root-tasks-legacy-2026-08-25.md`.
- New task dashboards/logs belong under `docs/tasks/`, indexed by `docs/tasks/README.md`.
