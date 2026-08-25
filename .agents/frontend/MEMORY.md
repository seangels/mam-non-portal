# Frontend role memory

Last updated: 2026-08-25

## Resume here

- 2026-08-25: Fixed assessment-sheets form dropdown reload loop. Root cause was `AssessmentSheetFormComponent` exposing `dx-form` `[editorOptions]` via getters that returned new object literals each Angular change-detection pass; DevExtreme 19.2.5 treated those as option changes and reloaded the student `CustomStore` (`/students`) repeatedly. `studentEditorOptions`, `teacherEditorOptions`, `statusEditorOptions`, `assessmentEditorOptions`, `dateEditorOptions`, `noteEditorOptions`, and `formColCountByScreen` are now stable readonly object references. Added a regression spec asserting those option references stay stable. Verification: `npm --prefix ui run test:ci` pass 78/78 after the spec; `$env:NG_BUILD_MAX_WORKERS='1'; npm --prefix ui run build -- --configuration development` pass hash `ef56d719257656ae84c5` with only known DevExtreme/CommonJS warnings. No production/IIS/deploy.
- 2026-08-25: Assessment-sheets UI create/edit checkpoint. `/#/assessment-sheets`, `/#/assessment-sheets/new`, and `/#/assessment-sheets/:id/edit` are visible/route-guarded for `SuperAdmin|Admin|Teacher`. The form component is declared in `AssessmentSheetsModule`, uses Vietnamese copy, creates sheets with selected `assessmentIds`, updates header/status through the existing `/assessment-sheets` endpoints, shows saved record snapshots read-only on edit, and avoids the manager-only Teacher dropdown for Teacher users. Verification run: `$env:NG_BUILD_MAX_WORKERS='1'; npm --prefix ui run build -- --configuration development` passed, then `npm --prefix ui run test:ci` passed 77/77 after updating focused route/navigation/request-mapping specs. The former Teacher student-picker gap was resolved by backend `ASM-BE-01`: `/students` read is now available to Teacher within their responsible group scope.
- 2026-08-24: Added frontend helper command for API-hosted SPA handoff. `ui/package.json` now has `npm run copy` (plus alias `copy:build:api`) which runs `ui/scripts/copy-build-to-api.ps1`. The script copies an existing Angular build from `ui/dist/DevExtreme-app` into `api/src/AdminPortal.Api/ClientApp/build`, cleans the destination before copying while preserving `.gitkeep`, validates `index.html`, and verifies the fixed API target path before deleting/copying. Verification run: `npm --prefix ui run copy -- -WhatIf` passed and changed no files. No Angular build, production/IIS package, or deployment was run.
- Read `ui/AGENTS.md`, `.agents/shared/MEMORY.md`, `plans/README.md`, the relevant numbered plan contract sections, `ui/README.md`, and the current frontend/deployment sections of `tasks.md` before acting.
- Frontend owns `ui/`. Root owns shared contract/deployment/tracking files; backend owns `api/`. Coordinate any contract change instead of editing across ownership boundaries silently.
- Verify the current source and runtime state. This file records durable context; it does not guarantee that `node_modules`, `.certs`, `dist`, ignored artifacts/releases, API processes, PostgreSQL, or IIS exist now.

## Current frontend architecture

- Angular 12.2.17 NgModule application with strict TypeScript 4.3.5, RxJS 7.4.0, and DevExtreme/DevExtreme Angular 19.2.5. Development/build tooling is pinned to Node 14.21.3 and npm 8.19.4; do not assume standalone components or newer Angular/DevExtreme APIs.
- `ui/src/app/core/models/` defines API DTOs/enums and `ProblemDetails` mapping. `core/services/` owns the generic API client plus setup/auth/user/student state and clients. `core/interceptors/auth.interceptor.ts` owns bearer, CSRF, and refresh retry behavior.
- `shared/` contains auth/setup forms and reusable shell components/services; `layouts/` contains responsive DevExtreme drawer/toolbars; `pages/` contains dashboard, profile, administrator-account, Teacher, Student/Group management, and attendance.
- `AppModule` has an `APP_INITIALIZER`: check setup status first and restore the auth session only if setup is complete. Router uses hash URLs.
- User/student pages use DevExtreme `CustomStore`, remote server paging/sorting, explicit filters, DevExtreme popup forms, confirmation dialogs, notifications, and mutation loading states.
- For DevExtreme 19 form editors, keep `[editorOptions]`, nested option objects such as `inputAttr`, and responsive config objects as stable component properties. Do not return fresh object literals from getters or template bindings; it can trigger widget reconfiguration and repeated `CustomStore.load` calls.

## Security and API contract that must not drift

- Base contract: `/api/v1`, camelCase JSON, string enums, `ProblemDetails` errors, and paged responses shaped as `{ items, pagination }`.
- Access and CSRF tokens remain only in `AuthStateService` memory. Never add browser persistence. Refresh is an API-owned host-only `HttpOnly`, `Secure`, `SameSite=None` cookie; all API requests use credentials.
- Session bootstrap is `GET auth/csrf` -> `POST auth/refresh` -> `GET auth/me`. Login/refresh return access token, expiry, CSRF token, and current user.
- `AuthInterceptor` attaches bearer auth and `X-CSRF-TOKEN` on non-GET/non-HEAD requests, shares one refresh for concurrent 401s, and excludes auth/setup endpoints from retry. A failed refresh clears state and redirects to login.
- Setup is a one-time unauthenticated flow: `GET setup/status`, then `POST setup/super-admin` only for an entirely empty user table. Success routes to login without creating a session; `409` is handled as already initialized. An unavailable setup-status API remains a retryable error and must not be bypassed.
- Full `PUT` is required for user/student updates; cleared optional fields are sent as `null`. Date-only payloads are `YYYY-MM-DD` and must not shift through UTC conversion.
- Roles/statuses: `SuperAdmin|Admin|Teacher`; user `Active|Inactive|Locked`; student `Active|Inactive`; gender `Male|Female|Other`. UI role guards/navigation do not replace server authorization.
- Server list limits: page starts at 1 and page size is at most 100. Keep sort field names within the backend whitelists documented in `ui/AGENTS.md` and the relevant numbered plan in `plans/`.
- Attendance is a full-roster aggregate: Missing POST sends all current snapshot students plus `expectedSnapshotVersion`; Saved PUT sends all persisted snapshot students plus `expectedVersion`. `Unmarked` is a persisted status; Missing defaults still come only from the backend. UI keeps baseline/drafts in memory and never silently overwrites `409`.
- Attendance dates remain DateOnly and follow server business date in `Asia/Ho_Chi_Minh`. Teacher only receives current responsible groups; Admin/SuperAdmin can select all groups and alone can run acknowledged historical recovery.
- Teacher management uses `/teachers` as the canonical aggregate. List/detail combine User account fields with `teacherCode`, `note`, attendance policy, responsible-group summaries, timestamps and `version`; create/full PUT/delete/policy carry the TCH contract and optimistic concurrency. Teacher list search is always remote and trusts server `totalItems`; do not apply local accent filtering to paged rows.
- `/users` is now `Tài khoản quản trị`: only SuperAdmin can open it and its UI always queries/creates/updates role `Admin`. Password change remains the only Teacher action routed through `/users/{userId}/password`.
- Student schedule contract adds `StudyMode = OneToOne|FullDay`, canonical Monday–Saturday weekdays and aggregate `version`. Student create/full PUT always sends `studySchedule`; update/group/delete use `expectedVersion`. `StudentsService.assignGroup` is the only frontend client for `PUT /students/{id}/group` and is shared by Student and Group pages.
- Scheduled attendance is operational: Missing roster/defaults come only from the backend for the selected business date; UI must not derive or filter them again. `NoScheduledStudents` is a Daily GET read-only reason and a standard POST error code. Saved sheets and historical recovery remain independent of the current schedule; recovery continues as a manual roster with `Present` defaults.

## Historical feature baseline (verified 2026-08-12, before DX19 migration)

As implemented and verified on 2026-08-12:

- Real API/environment/model layer and normalized `ApiError`/`ProblemDetails` handling.
- Login, refresh rotation support, logout, `/me`, session restore, route guards, role-aware navigation, and in-memory token state.
- Public registration, forgot/reset-password, and other out-of-scope template flows were removed.
- Dashboard/profile plus remote user CRUD/password change and student CRUD screens, including validation, filters, sort, pagination, loading, confirm, notification, and key 401/403/409 behavior.
- First-run SuperAdmin setup UI and guards are connected to the one-time setup API.
- `/student-groups` gives Admin/SuperAdmin remote group CRUD/filter, responsible Teacher assignment, current roster add/move/remove with the 100-student cap, and Teacher attendance-edit policy 1–7 days.
- `/teachers`, `/teachers/new`, `/teachers/:id`, and `/teachers/:id/edit` give Admin/SuperAdmin remote list/filter/paging, read-only detail/group summaries, atomic create, full versioned update, password change and versioned soft-delete. The form supports editable user-entered Teacher code, nullable clearing, dirty-route/beforeunload protection, double-submit prevention and conflict recovery without overwriting the draft.
- Teacher group assignment and attendance policy remain exclusively in `/student-groups`. Policy PUT now sends `expectedVersion`; a conflict keeps the popup open and lets the manager load the latest Teacher version. Teacher detail only links to these controls.
- `/#/attendance` is available to all roles. Its main daily list uses a fluid compact-card grid with a 195 px minimum track (five cards at the verified 1036 px content width on a 1366 px viewport), a vertical desktop/horizontal mobile identity rail containing only nickname and student code, native status/permission selects, five accessible text-and-color status states, accent-insensitive local search, full-roster POST/PUT, dirty guards and explicit conflict/read-only states. For past dates, managers can open manual recovery from the toolbar without selecting a current context group, so inactive/deleted historical groups remain reachable.
- All portal labels/error copy use Vietnamese dictionaries and DevExtreme `vi` messages. Raw `ProblemDetails.title/detail` is not displayed; stable codes map to Vietnamese copy and trace ID remains available for support.
- `/#/students` now has remote group/unassigned/study-mode/weekday filters, adaptive group/schedule columns, a versioned assign/move/unassign popup and a responsive schedule editor with exactly six Vietnamese weekday checkboxes. Group roster uses the same versioned Student command and exposes conflict reload; schedule/nested validation conflicts keep the Student draft.
- Attendance context displays the date-specific scheduled count. Missing copy no longer claims every row defaults to Present; an empty scheduled roster is read-only and has no save action. `AbsentHalfDay` and `AbsentFullDay` use only permission state; every UI write, including recovery, sends `halfDayPart=null`. Notes entered or edited in UI are limited to 200 characters, while an untouched Saved legacy note over 200 is round-tripped exactly through full-roster PUT.
- Focused Jasmine coverage currently consists of 59 specs, including DateOnly, Vietnamese attendance search, AUI Unmarked/default/full-roster/half-day/legacy-note/read-only/conflict/recovery behavior, Teacher route/role/dashboard boundaries, remote paging mapping, canonical Teacher service endpoints, form request mapping, policy concurrency, administrator-account isolation, API error mapping, and auth/setup state.

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
npm --prefix ui run test:ci
$env:NG_BUILD_MAX_WORKERS = "1"
npm --prefix ui run build -- --configuration development
```

- `npm run test:ci` uses Karma/Jasmine with `ChromeHeadlessCI`.
- `npm ci` runs `postinstall`, which rebuilds generated DevExtreme theme assets.
- `ui/e2e/` is legacy template material and is not part of the current package scripts or verification gate.
- Production build, IIS build/package and deployment require an explicit `$gv-portal-production` invocation and remain root/infrastructure-owned.

## Last verified baseline

DX19 final automated gate and user-waived browser checkpoint on 2026-08-14:

- Toolchain facts: Node `v14.21.3`, npm `8.19.4`; direct local Angular CLI reports CLI/Angular `12.2.17`, CDK `12.2.13`, TypeScript `4.3.5` and RxJS `7.4.0`. `npm --prefix ui exec ng -- version` is not a reliable root-level probe (it reports an invalid config), while `ui\\node_modules\\.bin\\ng.cmd version` runs successfully and direct JSON-schema validation of `ui/angular.json` is valid.
- Fresh `npm --prefix ui run preinstall` passed. `npm --prefix ui run test:ci` passed 72/72 in ChromeHeadless 151. `NG_BUILD_MAX_WORKERS=1; npm --prefix ui run build -- --configuration development` passed with 11.77 MB initial output and hash `6138eb38f246f20a5a66`; it emitted 16 known DevExtreme 19/CommonJS optimization-bailout warnings and no template/type error.
- Logical `npm --prefix ui ls --package-lock-only` and the targeted Angular/DevExtreme package tree both exited 0 at the exact pinned matrix; `package-lock.json` remains lockfile v2. Guards found no floating DevExtreme version, compiler/routing/install bypass, disabled/focused Jasmine spec or known newer DevExtreme API name. Scoped diff checking passed. The normal npm physical scan after ngcc reports root extraneous `__ngcc_entry_points__.json`, `bindings@1.5.0`, `file-uri-to-path@1.0.0`, and `nan@2.28.0`; the latter three are only children of optional Darwin-only `fsevents@1.2.13` absent on Windows. Record them without pruning or reinstalling node_modules. Fresh `npm --prefix ui audit --json` remains 112 findings (6 low, 63 moderate, 38 high, 5 critical); no `npm audit fix` is authorized because the EOL version matrix is pinned.
- Manual browser smoke was partial, then the user explicitly waived the remaining matrix. Verified portions included auth/setup guards and hash routing, the authenticated-to-login teardown, administrator and Teacher lifecycle coverage, Group create/assign/unassign/delete, Attendance card density/search/draft discard, and selected desktop/mobile layouts. Student CRUD/schedule/assignment, remaining Attendance save/conflict/read-only/no-scheduled cases, historical recovery, detailed responsive/a11y traversal and the final console gate were skipped, not passed.
- The policy NumberBox result seen through browser automation was manually checked by the user and normal keyboard editing works; no source change was required. A responsive sidebar transition separately produced `TypeError: Cannot read properties of null (reading 'internalFields')` inside DevExtreme navigation code. The user accepted skipping that finding for this migration, so it remains a known runtime risk and the smoke result must not be called console-clean.
- No production build, IIS build/package, deploy, dependency mutation, API/REST contract change, hash-routing change, auth-flow change or environment change was made. Production/IIS compatibility remains unverified until `$gv-portal-production` is explicitly invoked.

Fresh compact-attendance verification on 2026-08-12:

- `npm --prefix ui run test:ci`: passed 59/59 in Chrome Headless 151. Dependency console emitted the existing DevExtreme W0019 license warning and Inferno development-mode warning; tests had no failures. The first full run exposed an existing Student group-summary regression (`groupText` used the responsible Teacher instead of the group name); the one-line fix was retained and the rerun passed.
- `npm --prefix ui run build -- --configuration development` with `NG_BUILD_MAX_WORKERS=1`: passed after the 195 px density correction and compact pill fit adjustment, 11.00 MB initial raw development output; final hash `dfa785fd5adcf72a`. Compact-card rules are split into a second component stylesheet for maintainability; both stylesheet source files remain below 5 KB. Focused attendance rerun passed 16/16 after this CSS-only correction.
- Per the AUI execution gate, no production build, IIS build/package, or deploy was run. Generated `ui/dist/` remains ignored and must be rebuilt when the production skill is explicitly invoked.
- The prior ATT production/IIS package baseline remains historical evidence only; root-owned shared memory records its hash. It has not been rebuilt with TCH changes.

## Known pitfalls

- Do not build the IIS transfer bundle with the ordinary production environment: it would embed relative `/api/v1` instead of the dedicated API hostname.
- Do not switch away from hash routing casually. The current IIS static config supplies a default document but does not define SPA history fallback rewriting.
- Do not derive authorization from hidden menu items; route guards are UX only and the API decides permission.
- Do not turn the one-time setup API into registration, automatically log in after setup, or ignore a setup status error.
- Preserve the interceptor's single-flight refresh behavior. Per-request refresh storms can rotate the refresh token concurrently and invalidate recovery.
- Keep date-only conversion local-calendar based. `toISOString()` can change a student's birthday for positive UTC offsets.
- `environment.prod.ts` is valid only for a same-origin reverse-proxy layout; the current two-host IIS package requires `environment.iis.ts` and API CORS origin `https://gv-portal.local`.
- Angular schematics skip tests by default. Add focused specs manually when behavior changes.
- Teacher list search is server-side across the full candidate set. Reusing the local `includesVietnamese` helper on paged Teacher rows would produce incorrect pages and totals.
- Teacher policy is still edited in `/student-groups`, not the Teacher form. Always carry the row `version` as `expectedVersion` and retain the conflict-reload behavior.
- Keep Student request DTOs explicit; deriving them with `Omit<Student,...>` can leak group/version/response fields. Never call the Student group endpoint from `StudentGroupsService`; both management pages use `StudentsService.assignGroup` and must refresh the returned version.
- `StudyMode.OneToOne` is schedule metadata while `AttendanceStatus.OneToOneHour` is the persisted 60-minute status. Missing defaults come from the API; Saved and recovery records must never be re-filtered against the current schedule.
- Keep the separate raw-note baseline in the attendance editor. It is required so full-roster PUT preserves untouched API legacy notes over 200 characters even though all newly entered or edited notes are limited to 200 in the UI.
- Karma loads DevExtreme components and currently prints W0019 when no local DevExtreme license key is installed; this is a licensing/setup warning rather than a test failure and must be resolved by the product owner before commercial deployment.
- The DX19 browser matrix was not completed by user decision. Preserve the accepted responsive sidebar `internalFields` finding in future handoffs until it is reproduced and fixed or explicitly closed in a later development cycle; do not infer a clean runtime console from the automated gate.
- Browser automation may not commit DevExtreme 19 NumberBox values the same way as ordinary keyboard input. The user manually verified the policy value does change; reproduce with normal interaction before treating an automation-only mismatch as an application defect.
- Avoid unplanned Angular/DevExtreme/CDK upgrades; the current dependency mix builds, while a partial upgrade can break the template and generated themes.

## Memory update and handoff format

After material frontend work, replace/update the relevant current-state sections and include:

- Date and concise decision or behavior change.
- Files/contracts affected and whether backend/root coordination is needed.
- Exact build/test commands and pass/fail counts; say explicitly when a check was not run.
- Remaining risk, blocker, or next action.

Keep chronological detail in `tasks.md`. Never store passwords, connection strings with credentials, JWT keys, tokens, cookies, certificate private keys, `.env` contents, personal records, or other secrets here.
