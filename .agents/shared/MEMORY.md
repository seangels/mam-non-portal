# Shared workspace memory

Last updated: 2026-08-12

## Product and ownership

- Admin portal with a .NET 10 REST API, PostgreSQL 17, and Angular 15/DevExtreme UI.
- Backend owns `api/`; frontend owns `ui/`; orchestrator owns shared contract/deployment/tracking files.
- `plans/01-BASE-admin-portal.md` is the base REST contract; numbered feature plans are indexed in `plans/README.md`. `tasks.md` is the detailed execution log.

## Cross-stack contracts that must remain aligned

- API prefix: `/api/v1` with camelCase JSON and enum values serialized as strings.
- Auth endpoints: `/auth/login`, `/auth/csrf`, `/auth/refresh`, `/auth/logout`, `/auth/me`.
- Access tokens stay in UI memory. Refresh token is a host-only `HttpOnly`, `Secure`, `SameSite=None` cookie.
- Login/refresh return `accessToken`, `expiresIn`, `csrfToken`, and `user`. UI sends `X-CSRF-TOKEN` for refresh/logout and uses credentials for cookie requests.
- First-run setup endpoints: `GET /setup/status` and `POST /setup/super-admin`. UI routes to `/#/setup` only when no user record exists. Setup is one-time, rate-limited, and concurrency-protected by a PostgreSQL advisory transaction lock.
- User, student, and Teacher update endpoints use full `PUT` replacement. Lists return `{ items, pagination }` and use server pagination/filter/sort.
- Roles: `SuperAdmin`, `Admin`, `Teacher`. User statuses: `Active`, `Inactive`, `Locked`. Student statuses: `Active`, `Inactive`.
- Attendance uses current `StudentGroup` assignments (no `effective_from`/`effective_to`) and persisted full daily snapshots, including `Present`. Group snapshot version protects roster/identity; sheet version protects full PUT replacement. Only the current responsible Teacher may read/write their groups; Admin/SuperAdmin also have audited historical recovery.
- Teacher management is canonical at `/teachers` for Admin/SuperAdmin. `Teacher` stores editable user-entered `teacherCode`, nullable `note`, aggregate `version`, attendance policy and timestamps; account fields remain in `User`. Full PUT, policy PUT and DELETE use `expectedVersion`. Group assignment/policy UI remain under `student-groups`; User CRUD only manages Admin accounts, while Teacher password changes still use `/users/{userId}/password`.
- Teacher list search is accent/case-insensitive literal substring search in the .NET API after structured DB filters and before exact total/paging. Do not install PostgreSQL `unaccent` or add search schema for v1; the confirmed scale is below 50 Teachers.
- Student schedule is required `FullDay|OneToOne` plus one to six weekdays Monday–Saturday. Student full PUT, group command and delete use one optimistic `version/expectedVersion`; group assignment remains a separate command. Missing attendance filters the active roster by business weekday and defaults FullDay to `Present`, OneToOne to `OneToOneHour`/60; Saved sheets and manual recovery never re-filter current schedules.

## Deployment decision

- Production build/package/deploy is gated by the project skill `$gv-portal-production` at `.codex/skills/gv-portal-production`. Its `allow_implicit_invocation` is `false`; ordinary implementation or test work must use development verification and must not create a package or mutate IIS automatically.
- The skill has distinct `build`, `verify`, and `deploy` modes. No mode defaults to non-deploying `build`; only explicit `deploy` authorizes target IIS/hosts/certificate/`C:\inetpub`/database changes.
- Build happens on the source/build machine. The IIS target machine receives a ZIP containing publish artifacts and deploy documentation, not source code.
- IIS local HTTPS hostnames are `api-gv-portal.local` and `gv-portal.local`, both on port 443 with SNI.
- IIS physical paths are `C:\inetpub\api-gv-portal.local` and `C:\inetpub\gv-portal.local`.
- `deploy/iis/build-iis-package.ps1` creates the transfer ZIP and SHA-256 file. `deploy/iis/deploy-iis.ps1` runs on the target and prompts for PostgreSQL/JWT secrets as SecureString.
- The target needs IIS, the .NET 10 Hosting Bundle, PostgreSQL 17, and elevated Windows PowerShell 5.1. It does not need source, .NET SDK, Node, or npm.
- The deploy script creates/trusts a local SAN certificate by default, updates hosts entries, configures separate app pools, injects Production settings into deployed API `web.config`, and exposes HTTPS only.
- Generated `artifacts/` and `release/` are ignored and may not exist in a new clone/session.
- Do not run production build/package/deploy as a finalization habit. Require explicit invocation of `$gv-portal-production`; then follow its mode boundary and evidence checklist.

## Last verified baseline

- Backend build: 0 warnings/errors.
- Backend unit tests: 38/38 passed.
- Backend PostgreSQL 17/Testcontainers integration tests in Release: 24/24 passed, including fresh startup, `InitialCreate` → attendance → Teacher-management → Student-schedule upgrade rehearsal, optimistic-concurrency and schedule-vs-first-save race coverage.
- EF Core reports no pending model changes. The latest migration is `20260811172348_AddStudentStudySchedule`.
- Frontend development build passed for the SCH source and ChromeHeadlessCI tests: 50/50. Production/IIS AOT was intentionally not run during SCH; the last explicit production verification remains the earlier ATT/deployment baseline.
- PowerShell 5.1 parser passed for IIS scripts. Build/PrepareOnly and package checksum/content verification passed.
- The last generated package in the current workspace was `release/gv-portal-iis-20260811-132500.zip` (6,935,733 bytes) with SHA-256 `389E4D5CD4510A377AF41C83A20BA7C7C68C41543B8ED85647D04DFADD07C523`. It contains 103 entries, the expected API/UI HTTPS bundle, and no source/PDB/Development config/secret file. Rebuild rather than assuming this ignored file exists elsewhere.

## Operational cautions

- Do not store or copy secrets into source, memory, artifacts, or release notes. Secrets are inserted only on the target machine.
- The user authorizes proactive local Git commits at cohesive verified milestones. This does not authorize push, merge, rebase, force operations, tags, or pull requests.
- Docker Compose and a Windows PostgreSQL service can conflict on port 5432. Recheck the port owner before IIS deployment; `docker compose stop` preserves the Docker volume.
- Do not reset PostgreSQL data to demonstrate first-run setup. Use a new empty database when a fresh setup test is required.
- The IIS deployment has been prepared and packaged, but repository memory must not claim the target machine was deployed unless IIS/HTTPS/health were verified on that target.

## Current handoff

- Project custom agents are defined as `backend` and `frontend` under `.codex/agents/`; root and nested `AGENTS.md` files define their scope.
- Attendance epic `ATT` at `plans/02-ATT-attendance.md` is implemented and verified. Storage uses full daily `attendance_sheets` + `attendance_records`, including persisted `Present`; Missing is not attendance. Current group/student assignment has no `effective_from/effective_to`; saved sheets snapshot group, responsible Teacher and Student fields. Group `snapshotVersion` protects all snapshot inputs and historical creation. Sheet provenance is `CurrentSnapshot` or `HistoricalRecovery`; recovery has no source version, persists its reason beyond the 90-day audit window, and is restricted to an acknowledged Admin/SuperAdmin flow with historical candidate lookup. Teacher edit window is 1–7 days per profile; groups max at 100 with 8–10 cards/viewport and scrolling. Attendance data is retained; change audit remains 90 days. All user-visible and accessibility UI text is Vietnamese-only; English API identifiers/error codes are mapped centrally and never rendered raw.
- Teacher management epic `TCH` at `plans/03-TCH-teacher-management.md` is implemented and verified. Teacher only adds editable user-entered `teacherCode`, nullable `note`, and aggregate `version` to the existing identity/policy/timestamps; account fields stay in User; `/teachers` is canonical; group assignment and attendance policy stay in `student-groups`; no HR fields/self-service/upload/start date. Soft-delete keeps Teacher/code/history and revokes User sessions; full PUT/policy/delete share optimistic versioning. Accent/case-insensitive search runs in the .NET API over all structured-filter candidates before exact total/paging; no PostgreSQL `unaccent` or search schema/index is used. Product scale is confirmed below 50 Teachers; candidate-count/duration telemetry remains the signal to revisit this approach.
- Student group/schedule epic `SCH` at `plans/04-SCH-student-groups-study-schedule.md` is implemented and verified. `/students/{id}/group` remains the only group mutation. Student schedule is required `FullDay|OneToOne` plus one to six weekdays Monday–Saturday, protected by Student optimistic versioning. Current schedule derives Missing attendance roster/defaults while Saved sheets and manual historical recovery remain unchanged. PostgreSQL Student row locks use `FOR NO KEY UPDATE` so concurrent schedule mutation remains serialized without deadlocking AttendanceRecord foreign-key checks.
- Attendance compact-card epic draft `AUI` is under review at `plans/05-AUI-attendance-compact-cards.md`; no source implementation has started. `AUI-DEC-01`–`07` are locked: persisted `AttendanceStatus.Unmarked`; Missing defaults Present/OneToOneHour by schedule; new AbsentHalfDay writes omit `halfDayPart` but retain excused/unexcused; cards identify only `nickname · studentCode`; recovery is outside the v1 redesign; UI notes max is 200 while the API remains 2,000 and unchanged longer legacy values must not be truncated; the desktop grid is fluid with a five-card target at 1366 px. Implementation spans API/DB/UI and preserves legacy half-day data. Only `AUI-DEC-08` remains open.
- Runtime subagent processes must be recreated in a new chat, then resume from these repository files.
- Future backend/frontend agents should update their role memory and this file if they change a cross-stack contract or deployment behavior.
