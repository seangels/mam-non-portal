# Frontend role memory

Last updated: 2026-08-11

## Resume here

- Read `ui/AGENTS.md`, `.agents/shared/MEMORY.md`, the relevant `api/plan.md` contract sections, `ui/README.md`, and the current frontend/deployment sections of `tasks.md` before acting.
- Frontend owns `ui/`. Root owns shared contract/deployment/tracking files; backend owns `api/`. Coordinate any contract change instead of editing across ownership boundaries silently.
- Verify the current source and runtime state. This file records durable context; it does not guarantee that `node_modules`, `.certs`, `dist`, ignored artifacts/releases, API processes, PostgreSQL, or IIS exist now.

## Current frontend architecture

- Angular 15.2 NgModule application with strict TypeScript 4.9, RxJS 7.8, and DevExtreme 23.2.3. Do not assume standalone components or newer Angular APIs.
- `ui/src/app/core/models/` defines API DTOs/enums and `ProblemDetails` mapping. `core/services/` owns the generic API client plus setup/auth/user/student state and clients. `core/interceptors/auth.interceptor.ts` owns bearer, CSRF, and refresh retry behavior.
- `shared/` contains auth/setup forms and reusable shell components/services; `layouts/` contains responsive DevExtreme drawer/toolbars; `pages/` contains dashboard, profile, user management, and student management.
- `AppModule` has an `APP_INITIALIZER`: check setup status first and restore the auth session only if setup is complete. Router uses hash URLs.
- User/student pages use DevExtreme `CustomStore`, remote server paging/sorting, explicit filters, DevExtreme popup forms, confirmation dialogs, notifications, and mutation loading states.

## Security and API contract that must not drift

- Base contract: `/api/v1`, camelCase JSON, string enums, `ProblemDetails` errors, and paged responses shaped as `{ items, pagination }`.
- Access and CSRF tokens remain only in `AuthStateService` memory. Never add browser persistence. Refresh is an API-owned host-only `HttpOnly`, `Secure`, `SameSite=None` cookie; all API requests use credentials.
- Session bootstrap is `GET auth/csrf` -> `POST auth/refresh` -> `GET auth/me`. Login/refresh return access token, expiry, CSRF token, and current user.
- `AuthInterceptor` attaches bearer auth and `X-CSRF-TOKEN` on non-GET/non-HEAD requests, shares one refresh for concurrent 401s, and excludes auth/setup endpoints from retry. A failed refresh clears state and redirects to login.
- Setup is a one-time unauthenticated flow: `GET setup/status`, then `POST setup/super-admin` only for an entirely empty user table. Success routes to login without creating a session; `409` is handled as already initialized. An unavailable setup-status API remains a retryable error and must not be bypassed.
- Full `PUT` is required for user/student updates; cleared optional fields are sent as `null`. Date-only payloads are `YYYY-MM-DD` and must not shift through UTC conversion.
- Roles/statuses: `SuperAdmin|Admin|Teacher`; user `Active|Inactive|Locked`; student `Active|Inactive`; gender `Male|Female|Other`. UI role guards/navigation do not replace server authorization.
- Server list limits: page starts at 1 and page size is at most 100. Keep sort field names within the backend whitelists documented in `ui/AGENTS.md` and `api/plan.md`.

## Implemented feature baseline

As recorded in `tasks.md` on 2026-08-11, the intended first release is complete:

- Real API/environment/model layer and normalized `ApiError`/`ProblemDetails` handling.
- Login, refresh rotation support, logout, `/me`, session restore, route guards, role-aware navigation, and in-memory token state.
- Public registration, forgot/reset-password, and other out-of-scope template flows were removed.
- Dashboard/profile plus remote user CRUD/password change and student CRUD screens, including validation, filters, sort, pagination, loading, confirm, notification, and key 401/403/409 behavior.
- First-run SuperAdmin setup UI and guards are connected to the one-time setup API.
- Focused Jasmine coverage currently consists of 8 specs: setup state (3), API error mapping (2), auth-state clearing (1), and role navigation (2).

No product implementation task was active when this memory was created.

## Environment and deployment handoff

- Development UI: `https://localhost:4200`; development API: `https://localhost:7158/api/v1`. `npm start` runs the HTTPS setup script and writes ignored PEM material to `ui/.certs/`.
- Ordinary production environment uses relative `/api/v1`. The dedicated IIS environment uses `https://api-gv-portal.local/api/v1`; IIS UI URL is `https://gv-portal.local`.
- Builds happen on the source machine. `deploy/iis/build-iis-package.ps1` builds/validates API and the Angular `iis` configuration and creates a ZIP plus SHA-256. The separate IIS target receives artifacts and runs `deploy-iis.ps1` without `-Build`; it does not need Node/npm or the .NET SDK.
- IIS packaging, certificate/hosts bindings, API `web.config`, database/JWT/CORS settings, and deployment are root/infrastructure-owned. UI source and memory must never contain their secret values.
- Generated `artifacts/`, `release/`, `ui/dist/`, `ui/node_modules/`, and `ui/.certs/` are not durable handoff state. Rebuild or regenerate them as required.

## Working commands

Run from the workspace root:

```powershell
npm --prefix ui ci
npm --prefix ui start
npm --prefix ui run build
npm --prefix ui run test:ci
npm --prefix ui run build -- --configuration iis
```

- `npm run build` defaults to production and outputs `ui/dist/DevExtreme-app`.
- `npm run test:ci` uses Karma/Jasmine with `ChromeHeadlessCI`.
- `npm ci` runs `postinstall`, which rebuilds generated DevExtreme theme assets.
- `ui/e2e/` is legacy template material and is not part of the current package scripts or verification gate.
- Source-machine transfer package: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\deploy\iis\build-iis-package.ps1`.

## Last verified baseline

Historical results recorded in `tasks.md` on 2026-08-11:

- Angular production build passed; final setup-flow build was about 3.01 MB raw / 557.44 kB estimated transfer.
- Frontend unit tests passed 8/8.
- Angular IIS build passed and its bundle was verified to contain `https://api-gv-portal.local/api/v1`.
- The cross-machine package process, checksum, and content checks passed. See shared memory for the last package identity; rebuild rather than assuming an ignored ZIP is present.

This memory-setup change only added agent documentation; it did not rerun build or tests. Future implementation work must record fresh commands and results here.

## Known pitfalls

- Do not build the IIS transfer bundle with the ordinary production environment: it would embed relative `/api/v1` instead of the dedicated API hostname.
- Do not switch away from hash routing casually. The current IIS static config supplies a default document but does not define SPA history fallback rewriting.
- Do not derive authorization from hidden menu items; route guards are UX only and the API decides permission.
- Do not turn the one-time setup API into registration, automatically log in after setup, or ignore a setup status error.
- Preserve the interceptor's single-flight refresh behavior. Per-request refresh storms can rotate the refresh token concurrently and invalidate recovery.
- Keep date-only conversion local-calendar based. `toISOString()` can change a student's birthday for positive UTC offsets.
- `environment.prod.ts` is valid only for a same-origin reverse-proxy layout; the current two-host IIS package requires `environment.iis.ts` and API CORS origin `https://gv-portal.local`.
- Angular schematics skip tests by default. Add focused specs manually when behavior changes.
- Avoid unplanned Angular/DevExtreme/CDK upgrades; the current dependency mix builds, while a partial upgrade can break the template and generated themes.

## Memory update and handoff format

After material frontend work, replace/update the relevant current-state sections and include:

- Date and concise decision or behavior change.
- Files/contracts affected and whether backend/root coordination is needed.
- Exact build/test commands and pass/fail counts; say explicitly when a check was not run.
- Remaining risk, blocker, or next action.

Keep chronological detail in `tasks.md`. Never store passwords, connection strings with credentials, JWT keys, tokens, cookies, certificate private keys, `.env` contents, personal records, or other secrets here.
