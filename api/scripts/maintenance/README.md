# Backup / Restore PostgreSQL — Supabase

## Quick Start

Chạy từ thư mục `api/`.

**Backup** Supabase → `api/backups/supabase-db-postgres-<timestamp>.sql.gz`

```powershell
./scripts/maintenance/backup-postgres-supabase.ps1
```

**Restore** bản backup mới nhất ngược lên Supabase

```powershell
$latest = Get-ChildItem .\backups\supabase-db-*.sql* | Sort-Object LastWriteTime -Desc | Select-Object -First 1
./scripts/maintenance/restore-postgres-host.ps1 -Supabase -Force -BackupFile $latest.FullName
```

**Bật backup tự động mỗi 15 phút** (PowerShell as Administrator, làm 1 lần)

```powershell
./scripts/maintenance/register-backup-task.ps1
```

Hết. Các lệnh trên tự đọc `.env` và `backup-config.json` cạnh script, không hỏi gì thêm.

> `restore` **ghi đè dữ liệu** nên bắt buộc `-Force`. Thêm `-WhatIf` để xem trước mà không ghi.
> File `.sql.gz` restore trực tiếp được, script tự giải nén.

---

## Chuẩn bị (1 lần duy nhất)

**1. `api/scripts/maintenance/.env`** — lấy ở Supabase Dashboard → **Connect** → **Session pooler**:

```dotenv
SUPABASE_HOST=aws-0-<region>.pooler.supabase.com
SUPABASE_DB=postgres
SUPABASE_DB_USER=postgres.<project-ref>
SUPABASE_DB_PASSWORD=<password>
```

**2. Tool + DLL** trong cùng thư mục `api/scripts/maintenance/`:

```
pg_dump.exe  pg_restore.exe  psql.exe  pg_dumpall.exe
libpq.dll  libssl-3-x64.dll  libcrypto-3-x64.dll  libintl-9.dll
libiconv-2.dll  liblz4.dll  libzstd.dll  libwinpthread-1.dll
```

Copy nguyên thư mục, đừng copy mỗi `.exe`. Giữ **cả 4 tool cùng một phiên bản** (lệch version gây lỗi kiểu `invalid command \restrict`). Có sẵn ở `%APPDATA%\DBeaverData\drivers\clients\postgresql\win\17\` nếu máy đã cài DBeaver.

Cả `.env` lẫn `*.exe` / `*.dll` đều đã nằm trong `.gitignore`.

---

# Tham khảo

## Việc thường làm

```powershell
# Backup toàn bộ schema, định dạng custom (.dump)
./scripts/maintenance/backup-postgres-supabase.ps1 -Format custom -Schema @()

# Backup để restore ĐÈ lên DB đã có dữ liệu (kèm DROP ... IF EXISTS) - đọc cảnh báo -Clean trước khi dùng
./scripts/maintenance/backup-postgres-supabase.ps1 -Clean

# Backup chỉ cấu trúc, không dữ liệu
./scripts/maintenance/backup-postgres-supabase.ps1 -SchemaOnly

# Nạp bản backup Supabase vào DB local (tên DB / user có thể khác lúc backup)
./scripts/maintenance/restore-postgres-host.ps1 -Force `
  -BackupFile .\backups\supabase-db-postgres-20260902-233021.sql `
  -PgHost localhost -Port 5432 -Username admin_portal -Database admin_portal_dev

# Máy không có pg_dump.exe -> chạy tool trong container
./scripts/maintenance/backup-postgres-supabase.ps1 -ToolContainer api_postgres_1
```

## Cách script tìm tool

`-PgBinDir` → **cùng thư mục script** → `PATH` → `C:\Program Files\PostgreSQL\*\bin` → bộ client DBeaver.
Không có tool trên máy thì dùng `-ToolContainer <ten-container>` (Docker/Podman).

## Cách script lấy thông tin kết nối

`-ConnectionString` → `.env` `SUPABASE_CONNECTION_URL` → `.env` khoá rời `SUPABASE_HOST` / `SUPABASE_DB_USER` / `SUPABASE_DB_PASSWORD` / `SUPABASE_DB` → hỏi trực tiếp.

Khoá rời an toàn hơn `SUPABASE_CONNECTION_URL` khi password chứa `[ ] @ : /` — các ký tự này làm hỏng phân giải URI.

Riêng `restore-postgres-host.ps1` **chỉ** đọc connection string từ `.env` khi có cờ `-Supabase`, để lệnh restore local không vô tình trỏ lên Supabase.

## `backup-postgres-supabase.ps1`

Mặc định: `--schema=public --no-owner --no-privileges`, `sslmode=require`, plain `.sql`.

| Tham số | Mặc định | Ghi chú |
| --- | --- | --- |
| `-ConnectionString` | `.env` | |
| `-EnvFile` | `.env` cạnh script | |
| `-Format` | `plain` → `.sql` | `custom` → `.dump` |
| `-Schema` | `public` | `-Schema @()` = mọi schema |
| `-ExcludeSchema` | — | loại schema khỏi dump |
| `-SchemaOnly` / `-DataOnly` | off | chỉ cấu trúc / chỉ dữ liệu |
| `-IncludeOwner` | off | giữ `OWNER TO` + `GRANT` |
| `-Clean` | off | ⚠️ thêm `DROP ... IF EXISTS` trước mọi `CREATE`. **Chỉ dùng khi thực sự cần ghi đè** — xem cảnh báo bên dưới |
| `-OutputDirectory` | `api/backups` | |
| `-KeepDays` | `7` | xoá backup cũ hơn N ngày; `0` = tắt |
| `-NoSqlFixups` | off | tắt phần chỉnh file `.sql` |
| `-Compress` | theo `backup-config.json` | Nén gzip → `.sql.gz` (~15% kích thước). Restore tự giải nén |
| `-ConfigFile` | `backup-config.json` cạnh script | Đọc `outputDirectory` + `compress` |
| `-ToolContainer` / `-Engine` | — | chạy `pg_dump` trong container |
| `-PgBinDir` | tự dò | |

**SQL fixups** — với `-Format plain`, sau khi dump script tự sửa để file restore được vào DB đã có schema `public`:

- `CREATE SCHEMA public;` → `CREATE SCHEMA IF NOT EXISTS public;`
- comment lại `COMMENT ON SCHEMA public ...` (hay lỗi quyền trên managed DB)
- comment lại meta-command `\restrict` / `\unrestrict` do `pg_dump` ≥ 17.6 sinh ra (psql cũ hơn báo `invalid command \restrict`)
- comment lại `DROP SCHEMA IF EXISTS public;` do `-Clean` sinh ra (xoá cả schema là quá tay, trên Supabase còn không đủ quyền)

> ⚠️ **Cẩn thận với `-Clean`.** Nó sinh `DROP TABLE` cho mọi bảng trước khi tạo lại. Nếu restore đứt đoạn giữa chừng
> (ví dụ `DROP FUNCTION` vướng object phụ thuộc) thì database đã bị xóa bảng mà chưa kịp tạo lại — **mất dữ liệu**.
> Restore `.sql` nay mặc định chạy `--single-transaction` nên lỗi sẽ rollback, nhưng vẫn nên:
> restore vào **database rỗng** thay vì dùng `-Clean`, và luôn có bản backup tốt trước khi ghi đè.

## `restore-postgres-host.ps1`

Nạp `.sql` (qua `psql`) hoặc `.dump` (qua `pg_restore --no-owner --no-acl`) vào Supabase, server online, hoặc DB cục bộ.

| Tham số | Mặc định | Ghi chú |
| --- | --- | --- |
| `-BackupFile` | **bắt buộc** | `.sql`, `.sql.gz` (tự giải nén), hoặc `.dump` |
| `-Force` | off | **bắt buộc** — xác nhận ghi đè |
| `-Supabase` | off | preset: `sslmode=require`, bỏ `--clean`, cấm tạo/drop DB, đọc `.env` |
| `-ConnectionString` | — | thay cho `-PgHost/-Port/-Username/-Database` |
| `-PgHost` / `-Port` / `-Username` / `-Database` | `.env` → hỏi | dùng khi restore vào DB khác |
| `-Password` | `.env` → `$env:PGPASSWORD` → hỏi ẩn | |
| `-EnvFile` | `.env` cạnh script | |
| `-Sslmode` | `require` khi `-Supabase` | |
| `-NoSingleTransaction` | off | Tắt `--single-transaction`. **Mặc định restore `.sql` chạy trong 1 transaction** — lỗi giữa chừng sẽ rollback thay vì để DB nửa vời |
| `-NoClean` | off (tự bật khi `-Supabase`) | bỏ `--clean --if-exists` cho `.dump` |
| `-CreateDatabase` / `-RecreateDatabase` | off | chỉ `.dump`, **không** dùng với `-Supabase` |
| `-ToolContainer` / `-Engine` | — | chạy tool trong container |
| `-NoPrompt` | off | không hỏi, thiếu giá trị thì lỗi |

## Backup tự động theo lịch

3 file làm việc cùng nhau:

| File | Vai trò |
| --- | --- |
| `backup-config.json` | Cấu hình chung: nơi lưu, nén, retention, chu kỳ |
| `run-backup-cycle.ps1` | 1 chu kỳ: backup → kiểm tra file → retention → ghi log. Task gọi file này |
| `register-backup-task.ps1` | Tạo / gỡ Windows Scheduled Task |

### `backup-config.json`

```json
{
  "outputDirectory": "..\..\backups",
  "compress": true,
  "keepHours": 6,
  "keepDays": 45,
  "intervalMinutes": 15,
  "minValidBytes": 20480
}
```

`outputDirectory` nhận **đường dẫn tương đối** (tính từ thư mục chứa script) hoặc **tuyệt đối Windows**:

```json
"outputDirectory": "..\..\backups"
"outputDirectory": "D:\Backups\gv-portal"
"outputDirectory": "%USERPROFILE%\Documents\backups"
"outputDirectory": "\\nas\backups\gv-portal"
```

### Quy tắc retention — 3 tầng, xét theo thứ tự

| Tầng | Luật | Kết quả |
| --- | --- | --- |
| 1 | File cũ hơn `keepDays` ngày lịch | **Xoá** — thắng mọi luật khác |
| 2 | File trong `keepHours` giờ gần nhất | **Giữ hết** |
| 3 | Còn lại → gom theo ngày lịch, mỗi ngày giữ **1 bản mới nhất** | Xoá phần còn lại |

Ví dụ chạy lúc **12:51 ngày 3/9** (mốc 6h = 06:51):

```
3/9   12:45, 12:30 ... 07:00      giu (24 ban, trong cua so 6h)
3/9   06:45 ve 00:00              XOA  (ban dai dien cua 3/9 la 12:45, da giu o tang 2)
2/9   chi con 23:45               giu 1 ban
1/9   chi con 23:45               giu 1 ban
...
21/7  (ngay thu 45)               giu 1 ban
20/7  tro ve truoc                XOA (qua 45 ngay)
```

Ở trạng thái ổn định: **24 + 45 = 69 file ≈ 45 MB** (đã nén).

> ⚠️ Backup ban ngày của **chính hôm nay** bị gọn xuống 1 bản ngay trong ngày, không đợi sang ngày mới.

### An toàn

- Chỉ động file khớp `supabase-db-*-yyyyMMdd-HHmmss.{sql,sql.gz,dump}`. File khác (`RECOVERY-*.sql`…) **không bị đụng**.
- Không đệ quy vào thư mục con.
- **Không bao giờ xoá file mới nhất tuyệt đối.**
- File nhỏ hơn `minValidBytes` coi là hỏng → không được làm đại diện của ngày, và bị xoá ngay sau khi backup.
- Backup lỗi thì **retention vẫn chạy**, task trả exit code 1 để Task Scheduler báo.

Xem trước sẽ xoá gì mà không xoá thật:

```powershell
./scripts/maintenance/retention-backups.ps1 -DryRun
```

### Quản lý task

```powershell
./scripts/maintenance/register-backup-task.ps1 -WhatIf        # xem se tao gi
./scripts/maintenance/register-backup-task.ps1                # tao (can Administrator)
./scripts/maintenance/register-backup-task.ps1 -Remove        # go

Get-ScheduledTask -TaskName 'GvPortal-Supabase-Backup' | Get-ScheduledTaskInfo
Start-ScheduledTask -TaskName 'GvPortal-Supabase-Backup'      # chay thu ngay
Get-Content .\backups\backup.log -Tail 20                    # xem log
```

Cấu hình task: `IgnoreNew` (bỏ qua nếu lần trước chưa xong) · `StartWhenAvailable` (chạy bù khi máy vừa thức) · không tự đánh thức máy · chạy cả khi dùng pin · `LogonType S4U` (chạy cả khi chưa đăng nhập, **không cần lưu mật khẩu**). Dùng `-Interactive` nếu chỉ muốn chạy khi đã đăng nhập.

## Xử lý sự cố

| Triệu chứng | Cách xử lý |
| --- | --- |
| `.exe` thoát ngay, exit code 53, không in gì | Thiếu DLL cạnh `.exe` — copy đủ 8 DLL ở trên |
| `Network unreachable` tới `db.<ref>.supabase.co` | Direct connection chỉ có IPv6 → đổi sang **Session pooler** |
| `password authentication failed for user "postgres"` | Password sai, hoặc chứa `[ ] @ : /` làm hỏng URI → dùng khoá rời `SUPABASE_DB_*` |
| `schema "public" already exists` | Dump tạo kèm `-NoSqlFixups` → dump lại, bỏ cờ đó |
| `relation "..." already exists` | Đang restore vào DB **đã có dữ liệu**. Ưu tiên restore vào database rỗng; chỉ dùng `-Clean` khi chấp nhận ghi đè (đọc cảnh báo ở trên) |
| `function "..." already exists` | Fixup `CREATE OR REPLACE FUNCTION` đã xử lý — dump lại bằng script là hết |
| `cannot drop function ... because other objects depend on it` | Event trigger phụ thuộc vào function. **Đừng thêm `CASCADE`** (sẽ xoá luôn event trigger mà dump không tạo lại). Dump lại bằng script — fixup bỏ `DROP FUNCTION` và dùng `CREATE OR REPLACE` |
| `invalid command \restrict` khi restore | `psql.exe` cũ hơn `pg_dump.exe`. Dump lại (fixup tự xử lý), hoặc dùng `psql.exe` cùng phiên bản với `pg_dump.exe` |
| `pg_dump: error: server version mismatch` | Client cũ hơn server → dùng client ≥ 17 |
| Task chạy nhưng không ra file | Xem `backups\backup.log`. Task trả exit 1 khi backup lỗi — xem cột Last Run Result trong Task Scheduler |
| Retention xoá nhầm | Chạy `retention-backups.ps1 -DryRun` để xem trước; file không khớp pattern không bao giờ bị đụng |
| `Cannot connect to Podman` | Chỉ ảnh hưởng `-ToolContainer` → bỏ cờ đó để chạy bằng `.exe` trên máy |

## Ghi chú

- Không commit file trong `api/backups/` — chứa dữ liệu thật.
- Không restore roles/globals (`pg_dumpall`) lên Supabase; Supabase tự quản lý.
- Chỉ restore schema ứng dụng (`public`), không đụng `auth`, `storage`, `realtime`, `extensions`.
