# Scripts vận hành PostgreSQL

Script PowerShell cho backup, restore và cleanup retention của database AdminPortal.
Chạy từ thư mục `api/` (hoặc bất kỳ đâu — đường dẫn được tính từ `$PSScriptRoot`).

| Script | Môi trường DB | Mục đích |
| --- | --- | --- |
| `backup-postgres-container.ps1` | DB trong container (Podman **hoặc** Docker) | Dump database (`.dump` custom hoặc `.sql` plain) + globals/cluster |
| `backup-postgres-host.ps1` | PostgreSQL cài trực tiếp trên máy | Dump database (`.dump` custom hoặc `.sql` plain) + globals/cluster |
| `backup-postgres.ps1` | DB trong container (Podman) | Bản cũ, chỉ hỗ trợ podman — dùng biến thể `-container` cho việc mới |
| `restore-postgres.ps1` | DB trong container (Podman) | Restore file `.dump`/`.sql` vào container |
| `restore-postgres-host.ps1` | Host online, **Supabase**, hoặc DB cục bộ | Restore `.dump`/`.sql`; hỗ trợ `-ConnectionString` + `-Supabase` |
| `cleanup-retention.ps1` | DB trong container (Podman) | Chạy `cleanup-retention.sql` + xoá backup cũ |
| `cleanup-retention.sql` | — | Câu lệnh xoá audit 90 ngày / session history 30 ngày |

## Quy ước chung

- File backup ghi vào `api/backups/` (đổi bằng `-OutputDirectory`). Thư mục này nằm trong `.gitignore`.
- Định dạng database dump: `-Format custom` (mặc định) → `.dump` (`pg_dump -Fc`, restore bằng `pg_restore`); `-Format plain` → `.sql` text (restore bằng `psql`, portable, dùng cho Supabase). Globals/cluster luôn là plain `.sql`.
- Tên file: `postgres-db-<database>-<yyyyMMdd-HHmmss>.{dump|sql}`, `postgres-globals-<timestamp>.sql`, `postgres-all-<timestamp>.sql`.
- Retention: `-KeepDays` (mặc định 7) xoá các file backup cũ hơn N ngày sau khi dump xong. Đặt `0` để tắt.
- Script `throw` và dừng ngay khi một lệnh con trả về exit code khác 0 (`$ErrorActionPreference = "Stop"`).
- Output cuối cùng là danh sách `FileInfo` của các file vừa tạo.

## Cấu hình mật khẩu (cho script `-host`)

Script container tự đọc credential từ biến môi trường của container nên **không cần** phần này.
Script `backup-postgres-host.ps1` / `restore-postgres-host.ps1` lấy thông tin kết nối theo thứ tự ưu tiên:

1. Tham số truyền vào (`-Password`, `-Username`, `-Database`, `-PgHost`, `-Port`) — hoặc mật khẩu trong `-ConnectionString`.
2. **File `.env`** (mặc định: file tên `.env` **cùng thư mục với script** — `api/scripts/maintenance/.env`; đổi vị trí bằng `-EnvFile <path>`).
3. `$env:PGPASSWORD` của phiên PowerShell (chỉ cho mật khẩu).
4. Nhập ẩn khi script hỏi (nếu vẫn thiếu và không `-NoPrompt`).
5. File `pgpass` của libpq: `%APPDATA%\postgresql\pgpass.conf` — **chỉ** ở chế độ tool cài trực tiếp; `-ToolContainer` không đọc pgpass của máy host.

> Không đặt mật khẩu thật trực tiếp trên dòng lệnh (`-Password 'abc'`): nó lọt vào
> `Get-History`, PSReadLine history file và danh sách tiến trình. Dùng `.env` hoặc để trống rồi nhập khi được hỏi.

### Cách 1 — file `.env` cạnh script (khuyến nghị, dùng lại nhiều lần)

Tạo `api/scripts/maintenance/.env` (đã nằm trong `.gitignore`, **không commit**). Định dạng dotenv `KEY=VALUE`:

```dotenv
POSTGRES_HOST=localhost
POSTGRES_PORT=5432
POSTGRES_USER=admin_portal
POSTGRES_DB=admin_portal_dev
POSTGRES_PASSWORD=REAL_PASSWORD_HERE
```

Nhận cả tên `PGHOST` / `PGPORT` / `PGUSER` / `PGDATABASE` / `PGPASSWORD`. Sau đó chạy gọn:

```powershell
# Không cần truyền gì thêm — host/port/user/db/password lấy từ .env
./scripts/maintenance/backup-postgres-host.ps1 -Format plain -Schema public -NoOwner -NoPrivileges

# Tham số truyền vào vẫn thắng .env (chỉ override cái cần)
./scripts/maintenance/backup-postgres-host.ps1 -Database another_db
```

Tái dùng `api/.env` sẵn có (`POSTGRES_*`): `-EnvFile ..\.env` (khi chạy từ `api/`) hoặc `-EnvFile (Join-Path $PSScriptRoot ..\..\.env)`.

### Cách 2 — `$env:PGPASSWORD` cho phiên hiện tại

```powershell
$sec = Read-Host "PostgreSQL password" -AsSecureString
$env:PGPASSWORD = [System.Net.NetworkCredential]::new('', $sec).Password
./scripts/maintenance/backup-postgres-host.ps1 -Username admin_portal -Database admin_portal_dev
Remove-Item Env:\PGPASSWORD   # xoá khi xong
```

### Cách 3 — file `pgpass.conf` (bền vững cho máy server)

Mỗi dòng: `hostname:port:database:username:password` (dùng `*` làm wildcard).

```powershell
$pgpass = Join-Path $env:APPDATA "postgresql\pgpass.conf"
New-Item -ItemType Directory -Force -Path (Split-Path $pgpass) | Out-Null
Add-Content -Path $pgpass -Value "localhost:5432:*:gv_portal_app:REAL_PASSWORD" -Encoding ascii
```

Sau đó chạy script không cần `-Password` và không cần `$env:PGPASSWORD`. Đặt quyền file
chỉ cho user hiện tại (`icacls "$pgpass" /inheritance:r /grant:r "$env:USERNAME:R"`).

## backup-postgres-container.ps1 — DB trong container

Tự phát hiện container engine, đọc `POSTGRES_USER` / `POSTGRES_DB` từ biến môi trường
của container, `pg_dump` bên trong container rồi `cp` file ra host (xoá file tạm trong container sau đó).

| Tham số | Mặc định | Ghi chú |
| --- | --- | --- |
| `-Engine` | `auto` | `auto` \| `podman` \| `docker`. `auto` ưu tiên podman, không có thì docker |
| `-ContainerName` | `api_postgres_1` | Tên container PostgreSQL đang chạy |
| `-DatabaseName` | (đọc từ container) | Override `POSTGRES_DB` |
| `-OutputDirectory` | `api/backups` | Thư mục chứa file backup |
| `-Format` | `custom` | `custom` → `.dump` (`-Fc`); `plain` → `.sql` text |
| `-Schema` | (tất cả) | Chỉ dump schema này; lặp được (`--schema`) |
| `-NoOwner` | (off) | `pg_dump --no-owner` |
| `-NoPrivileges` | (off) | `pg_dump --no-privileges` |
| `-KeepDays` | `7` | Xoá backup cũ hơn N ngày; `0` = tắt |
| `-IncludeGlobals` | (off) | Kèm `pg_dumpall --globals-only` (roles/tablespaces) |
| `-ClusterDump` | (off) | Kèm `pg_dumpall --clean --if-exists` (toàn cluster) |

```powershell
# Podman/Docker tự phát hiện, dùng mặc định
./scripts/maintenance/backup-postgres-container.ps1

# Docker, container khác, kèm globals
./scripts/maintenance/backup-postgres-container.ps1 -Engine docker -ContainerName gv_postgres -IncludeGlobals

# SQL text, chỉ schema public, sẵn sàng cho Supabase
./scripts/maintenance/backup-postgres-container.ps1 -Format plain -Schema public -NoOwner -NoPrivileges
```

## backup-postgres-host.ps1 — PostgreSQL cài trực tiếp

Gọi `pg_dump` / `pg_dumpall` trên host. Tự tìm binary theo thứ tự: `-PgBinDir` →
`PATH` → `C:\Program Files\PostgreSQL\*\bin` (chọn version cao nhất).

**Nhập tương tác:** chạy không đủ tham số thì script hỏi trực tiếp `Host` → `Cổng`
→ `Username` → `Database` → `Mật khẩu` (nhập ẩn). Bật `-NoPrompt` để tắt hỏi
(thiếu giá trị sẽ `throw` — dùng cho chạy tự động / scheduled task).

| Tham số | Mặc định | Ghi chú |
| --- | --- | --- |
| `-Username` | `.env` → hỏi | User PostgreSQL (key `POSTGRES_USER`/`PGUSER`) |
| `-Database` | `.env` → hỏi | Database cần backup (key `POSTGRES_DB`/`PGDATABASE`) |
| `-PgHost` | `.env` → hỏi (mặc định `localhost`) | Host (key `POSTGRES_HOST`/`PGHOST`) |
| `-Port` | `.env` → hỏi (mặc định `5432`) | Cổng (key `POSTGRES_PORT`/`PGPORT`) |
| `-Password` | `.env` → `$env:PGPASSWORD` → hỏi ẩn | Set `$env:PGPASSWORD` tạm cho tiến trình (key `POSTGRES_PASSWORD`/`PGPASSWORD`) |
| `-EnvFile` | `.env` cùng thư mục script | Đường dẫn file dotenv cấp thông tin kết nối |
| `-PgBinDir` | (tự dò) | Thư mục chứa `pg_dump.exe` / `pg_dumpall.exe` |
| `-OutputDirectory` | `api/backups` | Thư mục chứa file backup |
| `-Format` | `custom` | `custom` → `.dump` (`-Fc`, restore bằng `pg_restore`); `plain` → `.sql` text (restore bằng `psql`) |
| `-Schema` | (tất cả) | Chỉ dump schema này; lặp được (`--schema`) |
| `-NoOwner` | (off) | `pg_dump --no-owner` |
| `-NoPrivileges` | (off) | `pg_dump --no-privileges` |
| `-KeepDays` | `7` | Xoá backup cũ hơn N ngày; `0` = tắt |
| `-IncludeGlobals` | (off) | Kèm `pg_dumpall --globals-only` |
| `-ClusterDump` | (off) | Kèm `pg_dumpall --clean --if-exists` |
| `-NoPrompt` | (off) | Không hỏi; thiếu giá trị kết nối thì `throw` |

Mật khẩu: xem [Cấu hình mật khẩu](#cấu-hình-mật-khẩu-cho-script--host).

```powershell
# Nhập tương tác toàn bộ thông tin kết nối
./scripts/maintenance/backup-postgres-host.ps1

# Dev cục bộ (chỉ hỏi mật khẩu)
./scripts/maintenance/backup-postgres-host.ps1 -Username admin_portal -Database admin_portal_dev

# SQL text sẵn sàng restore lên Supabase (chỉ schema public, bỏ owner/grant)
./scripts/maintenance/backup-postgres-host.ps1 -Username gv_portal_app -Database gv_portal `
  -Format plain -Schema public -NoOwner -NoPrivileges

# Scheduled task: không hỏi, mật khẩu qua $env:PGPASSWORD hoặc pgpass.conf
./scripts/maintenance/backup-postgres-host.ps1 `
  -PgHost localhost -Port 5432 -Username gv_portal_app -Database gv_portal `
  -IncludeGlobals -PgBinDir "C:\Program Files\PostgreSQL\17\bin" -NoPrompt
```

## restore-postgres.ps1 — restore vào container (Podman)

Restore một file `.dump` (qua `pg_restore --clean --if-exists --no-owner --no-acl`) hoặc `.sql`
(qua `psql`) vào container. **Có tính phá huỷ dữ liệu** — bắt buộc `-Force`.

| Tham số | Mặc định | Ghi chú |
| --- | --- | --- |
| `-BackupFile` | **bắt buộc** | Đường dẫn file `.dump` hoặc `.sql` |
| `-ContainerName` | `api_postgres_1` | Container PostgreSQL đang chạy |
| `-DatabaseName` | (đọc từ container) | Override `POSTGRES_DB` |
| `-RecreateDatabase` | (off) | `dropdb` + `createdb` trước khi restore (chỉ với `.dump`) |
| `-Force` | (off) | **Bắt buộc** để xác nhận thao tác phá huỷ |

```powershell
./scripts/maintenance/restore-postgres.ps1 -BackupFile ./backups/postgres-db-admin_portal-20260901-120000.dump -Force
```

## restore-postgres-host.ps1 — restore vào host online / Supabase

Nạp file `.dump` (qua `pg_restore --no-owner --no-acl`, kèm `--clean --if-exists` trừ
khi `-NoClean`) hoặc `.sql` (qua `psql`) vào một PostgreSQL đang chạy — cục bộ,
server online, hoặc **Supabase** (xem [Migrate lên Supabase](#migrate-lên-supabase-backup-sql--restore)).
**Có tính phá huỷ dữ liệu** — bắt buộc `-Force`.

Chọn đích: tham số rời `-PgHost/-Port/-Username/-Database`, hoặc `-ConnectionString`
`"postgresql://user:pass@host:port/db"` (dùng nguyên chuỗi, hợp với chuỗi copy từ dashboard).

**Nhập tương tác:** thiếu tham số thì hỏi `Host` → `Cổng` → `Username` → `Database`
→ `Mật khẩu` (nhập ẩn). `-NoPrompt` để tắt hỏi.

**Nguồn client tool** (`pg_restore` / `psql` / `createdb` / `dropdb`):

- Mặc định — cài trực tiếp trên máy (`-PgBinDir` → `PATH` → `C:\Program Files\PostgreSQL\*\bin`).
- `-ToolContainer <tên>` — chạy tool **bên trong** container Docker/Podman rồi kết
  nối ra `-PgHost` online. File dump được `cp` vào container `/tmp` rồi xoá sau khi xong.
  Dùng khi máy chỉ có container, không cài PostgreSQL client.
  ⚠️ Khi đó `-PgHost` **không được** là `localhost` (đó là loopback của container);
  dùng `host.docker.internal` cho PostgreSQL trên máy host, hoặc IP/DNS thật của server.

Database đích và user có thể **khác** với lúc backup:

- `.dump` custom format không gắn cứng tên database → nạp vào DB nào tuỳ `-Database`.
- `--no-owner --no-acl` bỏ qua owner/grant gốc; object thuộc về `-Username` đang kết nối.
- `-CreateDatabase` tạo DB đích (owner = `-Username`) nếu chưa có; `-RecreateDatabase` drop rồi tạo lại.
- Với `.sql` plain, remap owner **không** áp dụng — dùng cho globals/cluster dump.

| Tham số | Mặc định | Ghi chú |
| --- | --- | --- |
| `-BackupFile` | **bắt buộc** | Đường dẫn file `.dump` hoặc `.sql` |
| `-Username` | `.env` → hỏi | User để kết nối; cũng là owner sau restore (`.dump`). Key `POSTGRES_USER`/`PGUSER` |
| `-Database` | `.env` → hỏi | Database đích (có thể khác tên lúc backup). Key `POSTGRES_DB`/`PGDATABASE` |
| `-PgHost` | `.env` → hỏi (mặc định `localhost`, hoặc `host.docker.internal` khi `-ToolContainer`) | Host. Key `POSTGRES_HOST`/`PGHOST` |
| `-Port` | `.env` → hỏi (mặc định `5432`) | Cổng. Key `POSTGRES_PORT`/`PGPORT` |
| `-Password` | `-ConnectionString` → `.env` → `$env:PGPASSWORD` → hỏi ẩn | Native: `$env:PGPASSWORD` tạm. Container: `exec -e PGPASSWORD`. Key `POSTGRES_PASSWORD`/`PGPASSWORD` |
| `-EnvFile` | `.env` cùng thư mục script | File dotenv cấp thông tin kết nối (bỏ qua host/user/db khi dùng `-ConnectionString`) |
| `-ConnectionString` | (không) | `postgresql://user:pass@host:port/db` — dùng nguyên chuỗi thay cho `-PgHost/-Port/-Username/-Database` (tiện cho chuỗi copy từ dashboard Supabase) |
| `-Sslmode` | (không; `require` khi `-Supabase`) | Đặt `PGSSLMODE` (`require` / `verify-full` / …) |
| `-Supabase` | (off) | Preset Supabase: `sslmode=require`, bỏ `--clean`, cấm create/recreate DB, in ghi chú |
| `-SingleTransaction` | (off) | `pg_restore --single-transaction` / `psql --single-transaction` (all-or-nothing) |
| `-NoClean` | (off) | Bỏ `--clean --if-exists` khi restore `.dump` (nạp vào schema rỗng) |
| `-PgBinDir` | (tự dò) | Thư mục chứa tool (chế độ native) |
| `-ToolContainer` | (không) | Chạy client tool trong container này thay vì trên máy |
| `-Engine` | `auto` | `auto` \| `podman` \| `docker` — engine cho `-ToolContainer` |
| `-MaintenanceDatabase` | `postgres` | DB để chạy create/drop và nạp file `.sql` (khi không dùng `-ConnectionString`) |
| `-CreateDatabase` | (off) | Tạo DB đích nếu chưa tồn tại (chỉ `.dump`; **không** với `-Supabase`/`-ConnectionString`) |
| `-RecreateDatabase` | (off) | `dropdb --if-exists` + `createdb` trước khi restore (chỉ `.dump`; **không** với `-Supabase`/`-ConnectionString`) |
| `-NoPrompt` | (off) | Không hỏi; thiếu giá trị kết nối thì `throw` |
| `-Force` | (off) | **Bắt buộc** để xác nhận thao tác phá huỷ |

```powershell
# Tool cài trực tiếp: nạp dump cũ vào DB mới tên khác + user khác
./scripts/maintenance/restore-postgres-host.ps1 `
  -Username gv_portal_app -Database gv_portal_restore `
  -BackupFile ./backups/postgres-db-admin_portal-20260901-120000.dump `
  -CreateDatabase -Force

# Máy chỉ có Docker: dùng client tool trong container, restore lên server online
./scripts/maintenance/restore-postgres-host.ps1 `
  -BackupFile .\backups\postgres-db-gv_portal-20260901-120000.dump `
  -ToolContainer api_postgres_1 -PgHost db.internal.example.com -Port 5432 `
  -Username gv_portal_app -Database gv_portal -CreateDatabase -Force

# Container tool -> PostgreSQL cài trực tiếp trên chính máy host
./scripts/maintenance/restore-postgres-host.ps1 `
  -BackupFile .\backups\postgres-db-gv_portal-20260901-120000.dump `
  -ToolContainer api_postgres_1 -PgHost host.docker.internal `
  -Username gv_portal_app -Database gv_portal -RecreateDatabase -Force
```

## Migrate lên Supabase (backup SQL → restore)

Supabase là PostgreSQL host — dùng `backup-postgres-*.ps1` để tạo dump rồi
`restore-postgres-host.ps1 -Supabase` để nạp lên.

**Lấy chuỗi kết nối:** Supabase Dashboard → Project Settings → Database →
*Connection string*. Dùng **Session pooler** (`...pooler.supabase.com:5432`,
user `postgres.<project-ref>`) hoặc **Direct connection**
(`db.<ref>.supabase.co:5432`, chỉ IPv6 nếu không mua add-on IPv4).

**Lưu ý Supabase:**

- Chỉ restore schema ứng dụng (thường `public`). Không đụng `auth`, `storage`,
  `realtime`, `extensions`, … do Supabase quản lý.
- Không restore roles/globals (`pg_dumpall`) — Supabase cấp sẵn.
- Dump nên có `--no-owner --no-privileges` để không vướng role không tồn tại.
- `-Supabase` tự đặt `sslmode=require` và bỏ `--clean`; thêm `-SingleTransaction`
  cho dump nhỏ nếu muốn all-or-nothing.
- Direct connection chỉ IPv6 → nếu máy không có IPv6, dùng Session pooler, hoặc
  `-ToolContainer` (container thường có IPv4).

```powershell
# 1) Backup dạng SQL text, chỉ schema public, bỏ owner/grant
./scripts/maintenance/backup-postgres-host.ps1 `
  -Username gv_portal_app -Database gv_portal `
  -Format plain -Schema public -NoOwner -NoPrivileges

# 2a) Restore lên Supabase bằng connection string (mật khẩu nằm trong chuỗi)
./scripts/maintenance/restore-postgres-host.ps1 -Supabase -Force `
  -BackupFile .\backups\postgres-db-gv_portal-20260901-120000.sql `
  -ConnectionString "postgresql://postgres.abcdxyz:PWD@aws-0-ap-southeast-1.pooler.supabase.com:5432/postgres"

# 2b) Hoặc tham số rời + hỏi mật khẩu ẩn
./scripts/maintenance/restore-postgres-host.ps1 -Supabase -Force `
  -BackupFile .\backups\postgres-db-gv_portal-20260901-120000.sql `
  -PgHost aws-0-ap-southeast-1.pooler.supabase.com -Port 5432 `
  -Username postgres.abcdxyz -Database postgres

# 2c) Máy chỉ có Docker (không cài psql) -> client tool trong container
./scripts/maintenance/restore-postgres-host.ps1 -Supabase -Force -ToolContainer api_postgres_1 `
  -BackupFile .\backups\postgres-db-gv_portal-20260901-120000.dump `
  -PgHost aws-0-ap-southeast-1.pooler.supabase.com -Port 5432 `
  -Username postgres.abcdxyz -Database postgres
```

File `.dump` custom cũng dùng được với `-Supabase` (nạp qua `pg_restore --no-owner
--no-acl`, không `--clean`); file `.sql` plain nạp qua `psql`.

## cleanup-retention.ps1 — retention (Podman)

Copy `cleanup-retention.sql` vào container và chạy bằng `psql`, sau đó xoá các file
backup cũ hơn `-BackupKeepDays`. Ngữ nghĩa retention phải khớp với
`api/tools/AdminPortal.Maintenance`.

| Tham số | Mặc định | Ghi chú |
| --- | --- | --- |
| `-ContainerName` | `api_postgres_1` | Container PostgreSQL đang chạy |
| `-DatabaseName` | (đọc từ container) | Override `POSTGRES_DB` |
| `-SqlFile` | `cleanup-retention.sql` cạnh script | File SQL retention |
| `-BackupDirectory` | `api/backups` | Thư mục backup cần dọn |
| `-BackupKeepDays` | `7` | Xoá backup cũ hơn N ngày; `0` = tắt |
| `-SkipDatabaseCleanup` | (off) | Chỉ dọn file backup, không đụng DB |
| `-SkipBackupCleanup` | (off) | Chỉ chạy SQL retention, không xoá file |

```powershell
./scripts/maintenance/cleanup-retention.ps1
```

## Ghi chú

- Docker Compose của project expose PostgreSQL ra cổng `5432`, có thể xung đột với
  PostgreSQL cài trực tiếp trên Windows. Kiểm tra chủ sở hữu cổng trước khi backup để
  chắc chắn đang nối đúng database (xem `deploy/iis/HUONG-DAN-DEPLOY-IIS.md`).
- `podman` và `docker` dùng chung cú pháp `exec` / `cp` / `inspect` nên
  `backup-postgres-container.ps1` chạy được với cả hai không cần đổi code.
- Không commit file trong `api/backups/` — có thể chứa dữ liệu cá nhân.
