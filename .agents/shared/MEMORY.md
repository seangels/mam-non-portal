# Shared workspace memory

Last updated: 2026-08-11

## Product and ownership

- Admin portal with a .NET 10 REST API, PostgreSQL 17, and Angular 15/DevExtreme UI.
- Backend owns `api/`; frontend owns `ui/`; orchestrator owns shared contract/deployment/tracking files.
- `api/plan.md` is the REST contract source. `tasks.md` is the detailed execution log.

## Cross-stack contracts that must remain aligned

- API prefix: `/api/v1` with camelCase JSON and enum values serialized as strings.
- Auth endpoints: `/auth/login`, `/auth/csrf`, `/auth/refresh`, `/auth/logout`, `/auth/me`.
- Access tokens stay in UI memory. Refresh token is a host-only `HttpOnly`, `Secure`, `SameSite=None` cookie.
- Login/refresh return `accessToken`, `expiresIn`, `csrfToken`, and `user`. UI sends `X-CSRF-TOKEN` for refresh/logout and uses credentials for cookie requests.
- First-run setup endpoints: `GET /setup/status` and `POST /setup/super-admin`. UI routes to `/#/setup` only when no user record exists. Setup is one-time, rate-limited, and concurrency-protected by a PostgreSQL advisory transaction lock.
- User and student update endpoints use full `PUT` replacement. Lists return `{ items, pagination }` and use server pagination/filter/sort.
- Roles: `SuperAdmin`, `Admin`, `Teacher`. User statuses: `Active`, `Inactive`, `Locked`. Student statuses: `Active`, `Inactive`.

## Deployment decision

- Build happens on the source/build machine. The IIS target machine receives a ZIP containing publish artifacts and deploy documentation, not source code.
- IIS local HTTPS hostnames are `api-gv-portal.local` and `gv-portal.local`, both on port 443 with SNI.
- IIS physical paths are `C:\inetpub\api-gv-portal.local` and `C:\inetpub\gv-portal.local`.
- `deploy/iis/build-iis-package.ps1` creates the transfer ZIP and SHA-256 file. `deploy/iis/deploy-iis.ps1` runs on the target and prompts for PostgreSQL/JWT secrets as SecureString.
- The target needs IIS, the .NET 10 Hosting Bundle, PostgreSQL 17, and elevated Windows PowerShell 5.1. It does not need source, .NET SDK, Node, or npm.
- The deploy script creates/trusts a local SAN certificate by default, updates hosts entries, configures separate app pools, injects Production settings into deployed API `web.config`, and exposes HTTPS only.
- Generated `artifacts/` and `release/` are ignored and may not exist in a new clone/session.

## Last verified baseline

- Backend build: 0 warnings/errors.
- Backend unit tests: 11/11 passed.
- Backend PostgreSQL/Testcontainers integration tests in Release: 8/8 passed.
- Frontend production/IIS builds passed; frontend tests: 8/8 passed.
- PowerShell 5.1 parser passed for IIS scripts. Build/PrepareOnly and package checksum/content verification passed.
- The last generated package in the current workspace was `release/gv-portal-iis-20260811-102752.zip` with SHA-256 `93EA758E0DCD542FDE73A8659A9D4BC96E3C5BA51381AA60010255B9056866F1`. Rebuild rather than assuming this ignored file exists elsewhere.

## Operational cautions

- Do not store or copy secrets into source, memory, artifacts, or release notes. Secrets are inserted only on the target machine.
- The user authorizes proactive local Git commits at cohesive verified milestones. This does not authorize push, merge, rebase, force operations, tags, or pull requests.
- Docker Compose and a Windows PostgreSQL service can conflict on port 5432. Recheck the port owner before IIS deployment; `docker compose stop` preserves the Docker volume.
- Do not reset PostgreSQL data to demonstrate first-run setup. Use a new empty database when a fresh setup test is required.
- The IIS deployment has been prepared and packaged, but repository memory must not claim the target machine was deployed unless IIS/HTTPS/health were verified on that target.

## Current handoff

- Project custom agents are defined as `backend` and `frontend` under `.codex/agents/`; root and nested `AGENTS.md` files define their scope.
- Attendance epic `ATT` has a draft cross-stack plan at `api/attendance-plan.md`. Implementation has not started. `ATT-DEC-01`–`09` are approved: daily granularity; required Morning/Afternoon for half-day; one exclusive 60-minute 1-1 block; excused flag for absences; per-Teacher 1–7 day edit window configured by Admin/SuperAdmin; maximum 100 students/group with 8–10 cards/viewport and scrolling; assignment UI included; Admin/SuperAdmin select one group; attendance data retained with 90-day change audit. `ATT-DEC-10` storage model remains pending.
- Runtime subagent processes must be recreated in a new chat, then resume from these repository files.
- Future backend/frontend agents should update their role memory and this file if they change a cross-stack contract or deployment behavior.
