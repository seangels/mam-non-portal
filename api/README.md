# Admin Portal API

RESTful API quản trị tài khoản Admin, giáo viên, học sinh, nhóm và điểm danh, xây dựng bằng .NET 10, ASP.NET Core, EF Core 10 và PostgreSQL.

## Chức năng

- Login, refresh rotation, logout và `/me`; không có đăng ký công khai.
- Khởi tạo SuperAdmin đầu tiên qua UI/API một lần duy nhất khi database chưa có user.
- JWT access token 15 phút gắn `sid`; mọi request xác thực đều kiểm tra auth session trong PostgreSQL để revoke tức thời.
- Refresh token 30 ngày trong cookie `HttpOnly`, `Secure`, `SameSite=None`.
- Double-submit CSRF cho refresh/logout bằng cookie `XSRF-TOKEN` và header `X-CSRF-TOKEN`.
- Phân quyền `SuperAdmin`, `Admin`, `Teacher`.
- User CRUD dành cho tài khoản Admin; đổi mật khẩu dùng chung cho Admin/Teacher; soft delete, pagination/filter/sort.
- Student CRUD, lịch học tuần, optimistic concurrency, soft delete, pagination/filter/sort và tái sử dụng student code sau khi xóa.
- Teacher CRUD hợp nhất account/profile, mã do người dùng nhập, optimistic concurrency và policy cửa sổ sửa điểm danh 1–7 ngày.
- Student group, phân công giáo viên/học sinh hiện tại, giới hạn 100 học sinh và snapshot version.
- Điểm danh theo ngày với trạng thái Missing/Saved, full-roster first-save/full PUT, immutable identity snapshot và historical recovery có kiểm soát.
- Audit log, ProblemDetails, rate limit, lockout, OpenAPI và health checks.
- Cleanup retention độc lập: audit 90 ngày, auth session 30 ngày.

## Cấu trúc

```text
src/
  AdminPortal.Api
  AdminPortal.Application
  AdminPortal.Domain
  AdminPortal.Infrastructure
tests/
  AdminPortal.UnitTests
  AdminPortal.IntegrationTests
tools/
  AdminPortal.Maintenance
scripts/maintenance/cleanup-retention.sql
```

Controller chỉ xử lý HTTP; use case nằm trong Application; EF Core, PostgreSQL và security implementation nằm trong Infrastructure. Dự án không dùng GenericRepository hoặc MediatR.

## Chạy local

Yêu cầu: .NET SDK 10 và Docker nếu muốn dùng PostgreSQL container/integration test.

1. Sao chép `.env.example` thành `.env` và thay toàn bộ giá trị mẫu bằng secret local.
2. Khởi động PostgreSQL bằng Docker Compose:

```powershell
docker compose up -d
```

3. Cấu hình các biến môi trường dành cho API rồi chạy API trực tiếp để debug:

```powershell
$env:ConnectionStrings__DefaultConnection = "Host=localhost;Port=5432;Database=admin_portal;Username=admin_portal;Password=<POSTGRES_PASSWORD trong .env>"
$env:Jwt__SigningKey = "<khóa ngẫu nhiên ít nhất 32 ký tự>"
$env:Database__MigrateOnStartup = "true"
dotnet run --project src/AdminPortal.Api
```

Docker Compose chỉ chạy PostgreSQL; API không chạy trong container. Khi API khởi động với `Database__MigrateOnStartup=true`, ứng dụng tự apply migration. Không commit `.env`.

Ở lần chạy đầu, frontend gọi `GET /api/v1/setup/status`. Nếu database chưa có bất kỳ user nào, ứng dụng chuyển đến `/setup` để nhập thông tin SuperAdmin. `POST /api/v1/setup/super-admin` chỉ tạo được đúng một tài khoản đầu tiên; sau đó endpoint luôn trả `409 Conflict`. Cơ chế khóa transaction của PostgreSQL bảo vệ cả trường hợp nhiều instance/request khởi tạo đồng thời.

Cookie auth luôn có cờ `Secure`. Khi UI gọi API khác site, API phải được phục vụ qua HTTPS (trực tiếp hoặc sau reverse proxy TLS). Port HTTP trong Compose chủ yếu phục vụ health check/test backend; không nên dùng luồng cookie cross-site qua HTTP.

Các cấu hình bắt buộc/quan trọng:

| Environment variable | Mô tả |
|---|---|
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string |
| `Jwt__SigningKey` | Khóa ngẫu nhiên ít nhất 32 ký tự |
| `Security__AllowedOrigins__0` | Origin đầy đủ của UI, không dùng wildcard |
| `Database__MigrateOnStartup` | `true` nếu instance chịu trách nhiệm apply migration |
| `Attendance__BusinessTimeZone` | Múi giờ ngày nghiệp vụ, mặc định `Asia/Ho_Chi_Minh` |
| `Spa__ServeFromClientAppBuild` | `true` để API serve SPA từ `ClientApp/build`; mặc định `false` |
| `Spa__BuildPath` | Đường dẫn thư mục build UI, mặc định `ClientApp/build` tương đối với content root của API |

### Serve SPA từ API

Mặc định API vẫn chạy theo mô hình tách site UI/API. Khi cần một phương án chạy chung, copy output build của UI vào `src/AdminPortal.Api/ClientApp/build` và bật `Spa__ServeFromClientAppBuild=true`. API sẽ serve static files trong thư mục này và fallback các route SPA về `index.html`; các đường `/api`, `/health`, `/openapi` và `/swagger` vẫn trả theo API/404, không bị fallback sang SPA.

### Quản lý migration

Repository pin `dotnet-ef` 10 trong local tool manifest. Restore tool trước khi thao tác migration:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef database update `
  --project src/AdminPortal.Infrastructure `
  --startup-project src/AdminPortal.Api
```

Tạo migration mới sau khi thay đổi model:

```powershell
dotnet tool run dotnet-ef migrations add <MigrationName> `
  --project src/AdminPortal.Infrastructure `
  --startup-project src/AdminPortal.Api `
  --output-dir Persistence/Migrations
```

Đặt `Jwt__SigningKey` tối thiểu 32 ký tự trong environment khi chạy EF CLI qua startup project. Không sửa tay hoặc xóa `AdminPortalDbContextModelSnapshot.cs` vì đây là baseline để EF tạo diff cho migration tiếp theo.

## Authentication và CSRF

`POST /api/v1/auth/login` và `POST /api/v1/auth/refresh` trả:

```json
{
  "accessToken": "...",
  "expiresIn": 900,
  "csrfToken": "...",
  "user": {
    "id": "00000000-0000-0000-0000-000000000000",
    "email": "admin@example.com",
    "fullName": "Admin",
    "phoneNumber": null,
    "role": "SuperAdmin",
    "status": "Active"
  }
}
```

Frontend phải:

1. Gửi request auth với credentials để browser nhận/gửi refresh cookie.
2. Giữ access token trong memory và gửi `Authorization: Bearer <token>`.
3. Giữ `csrfToken` từ response; gửi nó qua `X-CSRF-TOKEN` khi refresh/logout.
4. Khi reload trang, gọi `GET /api/v1/auth/csrf` với credentials để lấy `{ "csrfToken": "..." }`, rồi gọi refresh.
5. Có thể gọi logout chỉ bằng refresh cookie + CSRF; bearer hết hạn không ngăn server revoke session.

## Endpoint

```text
GET    /api/v1/setup/status
POST   /api/v1/setup/super-admin

POST   /api/v1/auth/login
GET    /api/v1/auth/csrf
POST   /api/v1/auth/refresh
POST   /api/v1/auth/logout
GET    /api/v1/auth/me

GET    /api/v1/users
POST   /api/v1/users
GET    /api/v1/users/{id}
PUT    /api/v1/users/{id}
PUT    /api/v1/users/{id}/password
DELETE /api/v1/users/{id}

GET    /api/v1/students
POST   /api/v1/students
GET    /api/v1/students/{id}
PUT    /api/v1/students/{id}
DELETE /api/v1/students/{id}?expectedVersion={version}
PUT    /api/v1/students/{id}/group

GET    /api/v1/teachers
POST   /api/v1/teachers
GET    /api/v1/teachers/{id}
PUT    /api/v1/teachers/{id}
DELETE /api/v1/teachers/{id}?expectedVersion={version}
PUT    /api/v1/teachers/{id}/attendance-policy

GET    /api/v1/student-groups
POST   /api/v1/student-groups
GET    /api/v1/student-groups/{id}
PUT    /api/v1/student-groups/{id}
DELETE /api/v1/student-groups/{id}
PUT    /api/v1/student-groups/{id}/responsible-teacher

GET    /api/v1/attendance/context
GET    /api/v1/attendance/daily
POST   /api/v1/attendance/sheets
PUT    /api/v1/attendance/sheets/{sheetId}
POST   /api/v1/attendance/sheets/historical-recovery
GET    /api/v1/attendance/historical-recovery/group-candidates
GET    /api/v1/attendance/historical-recovery/student-candidates
GET    /api/v1/attendance/historical-recovery/teacher-candidates

GET    /api/v1/assessments
GET    /api/v1/assessments/{id}
GET    /api/v1/assessment-sheets
GET    /api/v1/assessment-sheets/plan-candidates
GET    /api/v1/assessment-sheets/{id}
POST   /api/v1/assessment-sheets
PUT    /api/v1/assessment-sheets/{id}
PUT    /api/v1/assessment-sheets/{id}/records
PUT    /api/v1/assessment-sheets/{id}/status
POST   /api/v1/assessment-sheets/{id}/export-to-sheet
POST   /api/v1/assessment-sheets/{id}/sync-to-sheet
POST   /api/v1/assessment-sheets/{id}/generate-plan-pdf
POST   /api/v1/assessment-sheets/{id}/generate-result-pdf
POST   /api/v1/assessment-sheets/{id}/submit-results
POST   /api/v1/google-sheets/sync-assessments
```

Hai setup endpoint không yêu cầu đăng nhập. POST được rate limit và chỉ hoạt động khi bảng `users` hoàn toàn rỗng, kể cả khi xét các bản ghi đã soft delete. Mật khẩu SuperAdmin phải đạt password policy chung: tối thiểu 12 ký tự, có chữ hoa, chữ thường, số và ký tự đặc biệt.

Vì POST setup chủ đích cho phép anonymous ở lần chạy đầu, nên khởi tạo SuperAdmin ngay trước khi mở hệ thống ra Internet hoặc tạm giới hạn API/UI trong mạng tin cậy cho đến khi setup hoàn tất.

Các endpoint `PUT` là full replacement đối với toàn bộ field editable. Gửi `null` để xóa field nullable như `phoneNumber`, `gender`, `guardianName`, `guardianPhone`, `note`.

List dùng `page` (mặc định 1), `pageSize` (mặc định 20, tối đa 100), `search`, filter theo resource, `sortBy` whitelist và `sortOrder=asc|desc`.

User CRUD chỉ quản lý tài khoản role `Admin` và list chỉ dành cho `SuperAdmin`. Mọi create/update/delete role `Teacher` qua `/users` trả `409 TeacherMustBeManagedViaTeachers`; riêng `PUT /users/{userId}/password` tiếp tục dùng cho cả Admin và Teacher.

User sort: `email`, `fullName`, `role`, `status`, `createdAt`.

Student sort: `studentCode`, `fullName`, `nickName`, `dateOfBirth`, `gender`, `status`, `studyMode`, `createdAt`.

Enum được gửi/nhận dưới dạng chuỗi. `StudentStatus` chỉ có `Active` và `Inactive`; `Gender` có `Male`, `Female`, `Other` hoặc `null`.

### Quản lý học sinh và lịch học

- `GET /students` và `GET /students/{id}` cho phép `SuperAdmin`, `Admin` và `Teacher`. Với `Teacher`, API chỉ trả học sinh thuộc các nhóm mà Teacher hiện đang được gán phụ trách; học sinh ngoài scope trả `StudentNotFound`/404 như không tồn tại.
- Các mutation `POST /students`, `PUT /students/{id}`, `PUT /students/{id}/group` và `DELETE /students/{id}` vẫn chỉ dành cho `Admin`/`SuperAdmin` qua policy `PortalManagers`; `Teacher` nhận 403.
- `StudentResponse` trả `studySchedule: { mode, weekdays }` và `version`; weekday luôn canonical từ `Monday` đến `Saturday`, không expose bit mask PostgreSQL.
- Create/full PUT bắt buộc `studySchedule`. `mode` là `OneToOne|FullDay`; `weekdays` có 1–6 ngày unique, không có Chủ nhật. Full PUT thêm `expectedVersion` và luôn tăng version một, kể cả payload no-op.
- List nhận thêm `studyMode`, `studyWeekday`; filter được áp dụng trước `totalItems`/paging tại PostgreSQL.
- Phân/chuyển/gỡ nhóm chỉ qua `PUT /students/{id}/group` với `{ "groupId": "uuid-or-null", "expectedVersion": n }`. Cùng group và version hiện tại là no-op; assignment thật tăng Student version và snapshot group.
- DELETE nhận `expectedVersion` trong query. Stale PUT/group/delete trả `409 StudentVersionConflict` kèm `currentVersion`; Student không tồn tại trả `StudentNotFound`.
- Student legacy được migration backfill `FullDay`, Thứ Hai–Thứ Bảy, version 1. Audit chỉ lưu ID/mã, metadata field thay đổi, mode/mask và version; không lưu raw tên, guardian hoặc note.

### Quản lý giáo viên

`/api/v1/teachers` là mutation surface canonical và atomic cho cả User role `Teacher` lẫn Teacher profile. Các endpoint cần role `Admin` hoặc `SuperAdmin`:

- List nhận `page`, `pageSize`, `search`, `status`, `groupId`, `unassigned`, `sortBy`, `sortOrder`. `sortBy` hỗ trợ `teacherCode`, `fullName`, `email`, `status`, `attendanceEditWindowDays`, `responsibleGroupCount`, `createdAt`, `updatedAt`.
- `search` là literal substring trên mã, họ tên, email và điện thoại; server bỏ dấu tiếng Việt/không phân biệt hoa thường trước khi tính `totalItems` và phân trang. `%`/`_` không phải wildcard.
- Không gửi đồng thời `groupId` và `unassigned=true`; API trả `400 ValidationFailed`.
- Create nhận `teacherCode`, `fullName`, `email`, `phoneNumber`, `status`, `password`, `note`; server trim và uppercase mã. Policy mặc định là 7 ngày.
- Full PUT nhận các field editable trên, trừ password, cộng `expectedVersion`. `phoneNumber`/`note` nhận `null` để xóa. Thành công tăng `version` đúng một.
- Policy PUT nhận `{ "attendanceEditWindowDays": 1..7, "expectedVersion": n }` và dùng chung Teacher version.
- DELETE nhận `expectedVersion` trong query, bị chặn khi còn nhóm phụ trách; khi thành công soft-delete User, revoke session nhưng giữ Teacher row/mã/lịch sử.
- Phân công nhóm chỉ qua `PUT /student-groups/{id}/responsible-teacher`; Teacher detail chỉ đọc các nhóm hiện tại.

Các conflict/validation code ổn định: `TeacherNotFound`, `TeacherCodeAlreadyExists`, `EmailAlreadyExists`, `TeacherVersionConflict` (kèm `currentVersion`), `TeacherHasResponsibleGroups`, `TeacherMustBeManagedViaTeachers`, `InvalidAttendanceEditWindow`, `ValidationFailed`.

### Contract điểm danh

- Ngày nghiệp vụ dùng `Asia/Ho_Chi_Minh`; mọi role bị chặn mutation ngày tương lai. Teacher chỉ thao tác group đang phụ trách và trong policy riêng 1–7 ngày.
- `GET /attendance/daily` không ghi database. Khi chưa có phiếu, roster chỉ gồm Student active thuộc group và có lịch trong weekday đó. `FullDay` mặc định `Present`; `OneToOne` mặc định `OneToOneHour`/60 phút. Chỉ `POST /attendance/sheets` mới xác nhận/lưu phiếu.
- Nếu ngày đó không có Student có lịch, Missing trả items rỗng, `canCreate=false`, `readOnlyReason=NoScheduledStudents`; standard POST trả `409 NoScheduledStudents`. Historical recovery vẫn dùng roster explicit và không suy diễn lịch hiện tại.
- POST lần đầu và PUT cập nhật đều nhận đúng full roster, tối đa 100 record. POST dùng `expectedSnapshotVersion`; PUT dùng `expectedVersion`. Conflict trả `ProblemDetails.code` ổn định như `SnapshotChanged` hoặc `SheetVersionConflict`.
- Trạng thái hỗ trợ: `Present`, `AbsentFullDay`, `AbsentHalfDay`, `OneToOneHour`, `Unmarked`. Summary trả riêng `unmarked`; Missing vẫn mặc định theo schedule và không tự sinh `Unmarked`.
- Write mới `AbsentHalfDay` gửi `halfDayPart=null`, bắt buộc `isExcused`; `Unmarked` có toàn bộ conditional field null. Column/wire `halfDayPart` vẫn tồn tại để đọc dữ liệu Morning/Afternoon legacy. Full PUT giữ part legacy nếu record vẫn là `AbsentHalfDay`, nhưng clear khi đổi status. API notes tiếp tục tối đa 2.000 ký tự để round-trip dữ liệu cũ.
- Phiếu đã lưu giữ snapshot code/name/nickname của group, Teacher và Student. Rename/move/soft-delete dữ liệu hiện tại không sửa phiếu cũ.
- Historical recovery chỉ dành cho Admin/SuperAdmin khi không thể chứng minh current snapshot của ngày quá khứ; bắt buộc acknowledgment, reason, Teacher và danh sách Student rõ ràng.

### Bảng đánh giá năng lực (AssessmentSheet)

- Yêu cầu nghiệp vụ đầy đủ: [`docs/requirements/09-bang-danh-gia-nang-luc.md`](../docs/requirements/09-bang-danh-gia-nang-luc.md); kế hoạch kỹ thuật: [`docs/plans/07-ASH-assessment-sheet.md`](../docs/plans/07-ASH-assessment-sheet.md); tiến độ: [`docs/tasks/07-ASH/status.md`](../docs/tasks/07-ASH/status.md).
- Quyền: `Teacher`/`Admin`/`SuperAdmin` đều thao tác được `AssessmentSheet` của bất kỳ học sinh nào, không giới hạn theo nhóm — khác Attendance.
- `GET /assessments` nhận `studentId` không bắt buộc. Khi có `studentId`, response vẫn trả đủ `Assessment` theo filter/paging/sort hiện tại và left join sang `AssessmentSheetLatest`/`AssessmentRecordLatest` của học sinh đó để bổ sung `latestGrade` và `latestNote` nullable; nếu học sinh chưa có sheet latest hoặc một assessment chưa có record latest thì các field latest là `null`. Field `note` hiện hữu tiếp tục là ghi chú gốc của `Assessment`, không phải ghi chú latest.
- `AssessmentRecord` có 4 field kết quả độc lập: `planGrade`/`planNote` (giai đoạn kế hoạch, `planGrade` được server prefill từ dữ liệu gần nhất khi tạo) và `finalGrade`/`finalNote` (kết quả cuối, nhập riêng, không bị `PUT .../status` hay việc sửa cặp Plan ảnh hưởng).
- `PUT /assessment-sheets/{id}` và `PUT /assessment-sheets/{id}/records` bị chặn (`409 AssessmentSheetDone`) khi `status = Done`; `PUT /assessment-sheets/{id}/status` luôn thực hiện được ở cả hai chiều `Open`↔`Done`, không cần lý do.
- `GET /assessment-sheets/plan-candidates` dùng để chọn plan lúc tạo/sửa: lọc theo `studentId` (bắt buộc), `groupLv1Name`/`groupLv2Name`/`groupLv3Name`, `latestGradeAtOrBelow` (thang `A > B > C > D`), có `search`.
- `export-to-sheet`/`sync-to-sheet`/`generate-plan-pdf`/`generate-result-pdf` gọi Google Drive/Sheets thật: tạo (lazy, một lần) file `[F01]` riêng bằng cách copy file mẫu `gen_assessment_sheet`, lưu id vào `assessmentSheetSpreadsheetId`, rồi ghi dữ liệu vào sheet `data`/`khcn_template`/`KQ_template` tương ứng; 2 action sinh PDF còn export sheet đó sang PDF (qua Drive export URL) rồi **tải thẳng lên Google Drive** (không lưu file cục bộ) — file `[F01]` và các PDF được đặt vào `Student.DriveFolderId` (nhập thủ công ở UI quản lý Student, `null` thì Drive API tự chọn vị trí mặc định). Sinh PDF lần sau ghi đè đúng file Drive cũ (không tạo file rác) nếu `PlanFileLinkPdf`/`ResultFileLinkPdf` đã có link cũ. `submit-results` ghi nhãn `finalGrade` vào `[F0.ĐG]` (dò `E16:E`/`H16:16`) và set `submissionDate`. **Định dạng cột trong sheet `data`/`khcn_template`/`KQ_template` hiện là suy đoán tạm (TẠM/CHƯA XÁC NHẬN), chưa có mapping thật từ đội vận hành** (xem `docs/requirements/09-bang-danh-gia-nang-luc.md` mục 15) — sửa lại `WriteRecordsToSheetAsync` trong `GoogleSheetsService.cs` khi có mapping chính thức. Lỗi Google/Drive thật (không phải mapping) trả `409 AssessmentSheetGoogleOperationFailed`. Chưa chạy được smoke test thật với các action này (cần xác nhận quyền service account trên Drive trước).
- `Student.DriveFolderId`: id thư mục Google Drive riêng của học sinh, nhập thủ công qua `POST`/`PUT /students` (field `driveFolderId`, không bắt buộc) — backend chỉ đọc để đặt `[F01]`/PDF vào đúng thư mục, không tự tạo thư mục.
- `POST /google-sheets/sync-assessments` giờ mở cho cả `Teacher` (trước đây chỉ `Admin`/`SuperAdmin`); đồng bộ lại `Assessment` (từ `_data_DG_only_item`) và nạp lại/ghi đè hoàn toàn `AssessmentSheetLatest`/`AssessmentRecordLatest` (dữ liệu gợi ý `planGrade`, từ sheet `_data_DG`, cột `ma_hs`/`item_id`/`ket_qua`). Dòng có mã học sinh/mục đánh giá/nhãn kết quả không khớp dữ liệu hiện có sẽ bị bỏ qua (best-effort theo từng dòng, không làm fail cả lượt đồng bộ).
- Tên/vị trí sheet dùng trong tích hợp Google Sheets (`data`/`khcn_template`/`KQ_template`/`gid`, sheet `ĐG` và vị trí dò ô `E16:E`/`H16:16`, sheet `_data_DG` và dòng header/dữ liệu) đều **cấu hình được** qua mục `GoogleSheets` trong `appsettings.json` (`ResultSourceSheetName`, `ResultSourceFirstDataRow`, `ResultSourceFirstStudentColumnIndex`, `DataSheetName`, `PlanTemplateSheetName`/`PlanTemplateSheetGid`, `ResultTemplateSheetName`/`ResultTemplateSheetGid`, `LatestResultsSheetName`, `LatestResultsHeaderRow`, `LatestResultsFirstDataRow`) — không còn hardcode trong `GoogleSheetsService.cs`, đổi cấu trúc sheet nguồn không cần build lại.

Migration `AddAttendanceFoundation` tạo toàn bộ schema attendance, backfill Teacher profile cho user role `Teacher` hiện có và để `group_id` của Student hiện tại là `null`. Không seed group hoặc attendance sheet.

Migration `AddTeacherManagement` bổ sung `teacher_code`, `note`, `version`, unique/check constraints và backfill mã legacy theo dạng `GV-MIG-{UUID}`. Migration không cài PostgreSQL `unaccent` và giữ nguyên User/Student/Teacher/attendance hiện có. Khi nâng cấp, apply tuần tự migration attendance rồi Teacher management bằng EF như bình thường.

Migration `AddStudentStudySchedule` bổ sung `study_mode`, `study_weekday_mask`, Student `version` và check constraints. Upgrade backfill mọi Student, kể cả soft-deleted, thành `FullDay`/mask 63/version 1; mỗi group hiện tại có Student active được tăng snapshot đúng một lần. Phiếu Saved hiện có không bị rewrite.

Migration `AddAttendanceUnmarkedStatus` chỉ thay check constraint của `attendance_records`: thêm persisted `Unmarked` và cho phép `AbsentHalfDay.half_day_part` null cho write mới. Migration không drop column và không rewrite record Morning/Afternoon legacy.

Trước khi release nên chạy:

```powershell
dotnet tool run dotnet-ef migrations has-pending-model-changes `
  --project src/AdminPortal.Infrastructure `
  --startup-project src/AdminPortal.Api
```

## OpenAPI, lỗi và health check

- OpenAPI JSON trong Development: `/openapi/v1.json`.
- Liveness: `/health/live`.
- Readiness kiểm tra PostgreSQL: `/health/ready`.
- Lỗi trả theo `ProblemDetails` và luôn có `traceId`.

## Build và test

```powershell
dotnet restore AdminPortal.slnx
dotnet build AdminPortal.slnx --no-restore
dotnet test tests/AdminPortal.UnitTests --no-restore
dotnet test tests/AdminPortal.IntegrationTests --no-restore
```

Integration test dùng PostgreSQL thật qua Testcontainers nên Docker engine phải chạy. Nếu một checkpoint được user giới hạn rõ là chỉ build/unit/no DB runtime, ghi integration là not run/skipped thay vì pass.

## Deploy IIS local HTTPS

Bộ script build/deploy hai site `https://api-gv-portal.local` và `https://gv-portal.local`, cấu hình PostgreSQL 17, certificate local, hosts file và IIS app pools nằm tại [`../deploy/iis/HUONG-DAN-DEPLOY-IIS.md`](../deploy/iis/HUONG-DAN-DEPLOY-IIS.md).

## Cleanup retention

Chạy console app từ scheduler bên ngoài:

```powershell
dotnet run --project tools/AdminPortal.Maintenance
```

Hoặc chạy `scripts/maintenance/cleanup-retention.sql` bằng client PostgreSQL. Cả hai cách đều xóa theo batch, có thể chạy lại an toàn và không xóa session còn hoạt động.
