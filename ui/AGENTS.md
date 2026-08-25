# Frontend agent rules

This file applies to everything under `ui/`. It defines the durable frontend role for future sessions.

## Resume checklist

Before changing frontend code:

1. Read the workspace `../AGENTS.md` and `.agents/README.md`.
2. Read `../.agents/shared/MEMORY.md` and `../.agents/frontend/MEMORY.md`.
3. Read `../docs/plans/README.md`, the relevant contract sections in the numbered plans, `../docs/tasks/README.md`, the current frontend section in the relevant `../docs/tasks/**` status/log files, and `README.md`.
4. Inspect the current source and worktree. Memory is a handoff aid, not proof that generated files, processes, services, or test results are still current.

## Ownership and coordination

- The frontend role owns `ui/`: Angular source, frontend models/services/interceptors, DevExtreme pages/layouts/themes, frontend tests, and frontend environment files.
- Treat `../docs/plans/01-BASE-admin-portal.md` as the base REST contract and later numbered feature plans as scoped extensions. Do not silently compensate for an API-contract mismatch in the UI; report and coordinate it with the backend/root role.
- `../deploy/`, `../docs/tasks/**`, root/shared memory, and release coordination are owned by the root role. Change them only when the task explicitly assigns cross-cutting work. Do not write to root `../tasks.md`; it is legacy/frozen.
- Preserve unrelated user changes. Do not perform dependency upgrades, an Angular standalone migration, a routing-mode change, or a DevExtreme redesign as incidental work.

## Existing architecture

- Angular 12.2.17, TypeScript 4.3.5 in strict mode, RxJS 7.4.0, and DevExtreme/DevExtreme Angular 19.2.5. Development and build machines must use Node 14.21.3 with npm 8.19.4. These versions are EOL and remain pinned only because the product requires DevExtreme 19.2.5; do not use APIs introduced by newer Angular or DevExtreme releases.
- This is an NgModule application, not a standalone-component application. `AppModule` registers `HttpClientModule`, the auth interceptor, and an `APP_INITIALIZER`.
- Startup order is security-sensitive: `SetupService.loadStatus()` runs first; session restore runs only when setup is complete. If setup status fails, keep the retryable setup error state rather than bypassing initialization.
- Routing uses `useHash: true`. Main routes are `/setup`, `/login-form`, `/home`, `/profile`, `/users`, `/students`, `/student-groups`, and `/attendance`.
- `src/app/core/` contains API DTOs/errors, API clients, auth/setup state, and the interceptor. `shared/` contains reusable UI/auth/layout pieces. `pages/` contains feature screens. `layouts/` contains responsive DevExtreme shells.
- Keep the existing small feature-module pattern. The user/student page module currently lives beside its component in the component TypeScript file.

## API, authentication, and setup invariants

- API prefix is `/api/v1`; JSON is camelCase and enum values are strings.
- Always call the API through `ApiClient` unless there is a concrete reason not to. It normalizes the base URL, sends `withCredentials: true`, builds query parameters, and maps `ProblemDetails` to `ApiError`.
- Never persist access tokens or CSRF tokens in `localStorage`, `sessionStorage`, IndexedDB, source, logs, or memory files. `AuthStateService` intentionally holds them only in process memory.
- The refresh token is an API-owned `HttpOnly`, `Secure`, `SameSite=None` cookie. Frontend code must not try to read it.
- Login/refresh responses provide `accessToken`, `expiresIn`, `csrfToken`, and `user`. On reload, restore in this order: `GET auth/csrf`, `POST auth/refresh`, then `GET auth/me`.
- The interceptor sends `Authorization: Bearer ...`, adds `X-CSRF-TOKEN` to non-GET/non-HEAD requests when available, and coalesces concurrent 401s into one refresh request. Keep auth/setup endpoints out of the automatic 401 retry loop.
- Refresh failure clears all in-memory auth state and returns the user to login. Logout must clear local state even if the bearer token has expired.
- First-run setup uses `GET setup/status` and `POST setup/super-admin`. Setup creates exactly one initial `SuperAdmin`, does not sign the user in, and routes to login afterward. A `409` means another request already initialized the system.
- UI guards/menu visibility are only user-experience controls. The API remains the authorization authority. `SuperAdmin` manages Admin/Teacher, groups and students; `Admin` manages Teacher, groups and students; all three roles can open attendance, while Teacher data remains scoped to current responsible groups.

## Feature and data rules

- User and student lists use DevExtreme `CustomStore` with server paging/sorting. Map `skip`/`take` to one-based `page`/`pageSize`, return `{ data, totalCount }`, reset to page zero when filters change, and never send a page size above 100.
- Only send sort fields accepted by the API whitelist. Users: `email`, `fullName`, `role`, `status`, `createdAt`. Students: `studentCode`, `fullName`, `nickName`, `dateOfBirth`, `gender`, `status`, `createdAt`.
- Updates are full `PUT` replacements. Send every editable field and send cleared optional values as `null`; do not introduce partial-update semantics without a coordinated contract change.
- Date-only values use `YYYY-MM-DD`. Preserve local calendar dates and avoid UTC conversion that can shift the day.
- Keep DTO enums aligned with the API: roles `SuperAdmin|Admin|Teacher`; user status `Active|Inactive|Locked`; student status `Active|Inactive`; gender `Male|Female|Other`.
- Preserve `ProblemDetails` field errors, status, and trace ID. Keep useful handling for connection errors and 401/403/409 responses.
- Attendance uses a full-roster contract: Missing POST sends every current snapshot student with `expectedSnapshotVersion`; Saved PUT sends every persisted snapshot student with `expectedVersion`. Do not autosave or treat a Missing Present preview as persisted data.
- Attendance search is local accent-insensitive search over the authorized roster. Keep baseline and draft state independent of filtering, preserve DateOnly values, guard dirty route/date/group changes, and never silently overwrite a `409` conflict.
- Continue using DevExtreme validation, disabled/loading states, confirmation dialogs, and notifications for mutations. Server validation and authorization remain mandatory even when the UI validates first.
- Angular schematics currently default to `skipTests`; add or update focused Jasmine tests manually for changed logic, especially auth/setup, routing/roles, API mapping, and data conversion.

## Environments and IIS packaging

- Development: `src/environments/environment.ts` targets `https://localhost:7158/api/v1`; `npm start` serves `https://localhost:4200`.
- `npm start` runs `scripts/setup-https.ps1`, which uses the .NET SDK development certificate and exports ignored PEM files under `.certs/`. Never commit `.certs/` or private keys.
- Default production: `environment.prod.ts` uses relative `/api/v1`, suitable only when a reverse proxy exposes API and UI under the same origin.
- IIS package build: `environment.iis.ts` targets `https://api-gv-portal.local/api/v1`; UI is served at `https://gv-portal.local` with hash routing and the static `../deploy/iis/ui.web.config` security headers.
- The current workflow builds on the source machine and deploys artifacts to another IIS machine. Build the IIS configuration on the source machine; do not use the ordinary production environment for that package.
- The target IIS machine does not need Node/npm or the .NET SDK and must not rebuild source. It receives the ZIP/checksum and runs the deploy script without `-Build`. Deployment scripts and secrets remain root/infrastructure responsibilities.

## Commands and verification

From the workspace root:

```powershell
npm --prefix ui ci
npm --prefix ui start
npm --prefix ui run test:ci
$env:NG_BUILD_MAX_WORKERS = "1"
npm --prefix ui run build -- --configuration development
```

From `ui/`, the equivalent commands omit `--prefix ui`. `test:ci` uses `ChromeHeadlessCI`; the explicit development configuration is the normal build gate. `npm ci` runs the pinned toolchain check and regenerates the DevExtreme 19.2.5 themes through `postinstall`. Run `npm run build-themes` directly only when theme metadata changes.

Production builds, IIS builds/packages and deployment are not normal frontend verification. They may run only when the user explicitly invokes the root-owned `$gv-portal-production` skill. The transfer workflow remains root/infrastructure-owned.

## DX19 verification boundary

- Automated DX19 verification uses the exact Node/npm pins, full `test:ci`, a single-worker development build, logical/targeted dependency checks and source guards. Do not substitute a production/IIS build for this gate.
- Browser smoke during the migration was partial. The user explicitly waived the uncompleted Student, Attendance recovery, responsive, accessibility and final console matrix; never describe that matrix as fully passed.
- A DevExtreme 19 responsive sidebar transition previously produced `TypeError: Cannot read properties of null (reading 'internalFields')`. The user accepted skipping that finding for this migration; it remains a known runtime risk, not a clean-console result.
- The policy NumberBox discrepancy observed through browser automation was manually checked by the user and changes correctly with normal keyboard input. Treat that observation as an automation artifact unless it reproduces through ordinary interaction.
- Production/IIS compatibility has not been verified for the DX19 baseline. Keep the explicit `$gv-portal-production` gate.

## Durable memory and handoff

- After a material frontend change, update `../.agents/frontend/MEMORY.md` with the date, durable decision/current state, affected contract or files, exact verification commands/results, risks, and next action.
- Update shared memory through the root role when auth/API/deployment behavior changes across stacks. Detailed chronological activity belongs in `../docs/tasks/**`, not in role memory.
- Replace stale current-state facts instead of appending chat transcripts. Explicitly distinguish a historical baseline from tests run in the current session.
- Never put passwords, credential-bearing connection strings, JWT keys, access/refresh/CSRF tokens, cookies, private keys, personal data, `.env` contents, or deployed secret values in source, logs, tests, screenshots, or memory.
