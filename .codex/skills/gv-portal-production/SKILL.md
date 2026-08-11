---
name: gv-portal-production
description: Build, verify, package, or deploy the GV Portal production release for IIS. Use only when the user explicitly invokes `$gv-portal-production`; never trigger implicitly during implementation, testing, review, planning, or ordinary handoff work.
---

# GV Portal production

Treat production work as an explicit execution gate. Do not run a production build, create a release package, or change IIS merely because source work is complete.

## Establish the requested mode

- `build`: Run release gates and create a transferable IIS ZIP on the source machine. Do not modify IIS, hosts, certificate stores, `C:\inetpub`, or the target database.
- `verify`: Inspect an existing ZIP and checksum without rebuilding or deploying.
- `deploy`: Deploy an existing verified ZIP on the IIS target machine. This mode may change IIS, hosts, certificates, files under `C:\inetpub`, and apply EF migrations.
- If the invocation does not name a mode, use `build`, the non-deploying mode.
- Never reinterpret `build` as `deploy`. Require the user to explicitly request `deploy` before making target-machine changes.

## Read project state

From the repository root, read these files before acting:

1. `AGENTS.md`
2. `.agents/shared/MEMORY.md`
3. `deploy/iis/HUONG-DAN-DEPLOY-IIS.md`
4. The relevant PowerShell script: `deploy/iis/build-iis-package.ps1` or `deploy/iis/deploy-iis.ps1`

Check `git status`, the current commit, required tool versions, and whether the machine is the source/build machine or IIS target. Preserve unrelated changes. Never read, print, store, commit, or place credentials in command history, logs, memory, artifacts, or responses.

## Build mode

Run only after explicit invocation of this skill.

1. Record the current commit and dirty-worktree state. State clearly if the package contains uncommitted source.
2. Run backend gates:

       dotnet build api/AdminPortal.slnx --no-restore
       dotnet test api/tests/AdminPortal.UnitTests --no-restore
       dotnet test api/tests/AdminPortal.IntegrationTests -c Release --no-restore

3. Run frontend gates:

       npm --prefix ui run test:ci
       npm --prefix ui run build

4. If EF model/schema changed, run `dotnet-ef migrations has-pending-model-changes` with a temporary non-production signing key supplied through the process environment. Do not print the value.
5. Create the IIS package. Use `-SkipNpmInstall` only when `ui/node_modules` is already known to match the lockfile:

       powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\deploy\iis\build-iis-package.ps1

6. Verify the newest ZIP and `.sha256`: checksum matches, API DLL exists, IIS UI bundle contains `https://api-gv-portal.local/api/v1`, and archive entries contain no source, PDB, `appsettings.Development.json`, `.env`, private key, or secret file.
7. Report every gate, ZIP path, byte size, SHA-256, source commit, and whether the worktree was dirty. Do not claim IIS was deployed.

Stop without packaging when a required test or build fails unless the user explicitly authorizes a package with failed or skipped gates. Never conceal DevExtreme license warnings.

## Verify mode

Do not rebuild. Resolve the exact ZIP and adjacent `.sha256`, then perform the package checks from build step 6. Report missing or forbidden entries as failures. Do not extract over an existing deployment directory.

## Deploy mode

Run only on the intended Windows IIS target after the user explicitly requests `deploy`.

1. Confirm the exact ZIP/checksum, target host, and these intended paths:
   - `C:\inetpub\api-gv-portal.local`
   - `C:\inetpub\gv-portal.local`
2. Verify checksum before extraction and confirm the archive passed verify mode.
3. Require an existing PostgreSQL backup and a recoverable copy of the currently deployed application artifacts. If they are absent, create them using the documented procedure or stop and report the blocker.
4. Confirm Windows PowerShell is elevated and the target has IIS, ASP.NET Core Module V2/.NET 10 Hosting Bundle, and PostgreSQL 17 connectivity.
5. Extract to a versioned staging directory outside `C:\inetpub`; never treat the source repository's ignored artifacts as present on another machine.
6. Read PostgreSQL password and JWT signing key interactively as `SecureString`. Reuse the stable production JWT key. Never accept plaintext secrets embedded in the command or files.
7. Run the packaged `deploy\iis\deploy-iis.ps1` without `-Build`. This is the only step authorized to update IIS, hosts, certificate trust, deployed `web.config`, and apply migrations.
8. Verify:
   - `https://api-gv-portal.local/health/live`
   - `https://api-gv-portal.local/health/ready`
   - `https://api-gv-portal.local/api/v1/setup/status`
   - `https://gv-portal.local`
   - Both IIS sites/app pools and HTTPS SNI bindings
9. Report exact verification evidence and any rollback action. Never claim success from script exit alone.

Do not deploy a package built for a different API hostname. Do not use the combined `-Build` deploy path on the target machine. Do not roll back database migrations automatically; follow the documented restore procedure if application rollback is incompatible with the migrated schema.
