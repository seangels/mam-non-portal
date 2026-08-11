# Backend persistent memory

> Snapshot này giúp Backend agent mới tiếp tục công việc qua session khác. Source, test và diff hiện tại vẫn là bằng chứng ưu tiên. Không lưu secret trong file này.

## Trạng thái gần nhất

- Cập nhật: 2026-08-11.
- Backend feature scope trong `plans/01-BASE-admin-portal.md`, `plans/02-ATT-attendance.md` và Teacher management epic `plans/03-TCH-teacher-management.md` đã hoàn tất. `/teachers` hiện là aggregate canonical cho account/profile, mã giáo viên, concurrency, policy và lifecycle.
- Baseline xác minh gần nhất: solution Release build `0 warning / 0 error`; unit test `32/32`; integration test PostgreSQL 17 Testcontainers Release `21/21`; EF báo không có pending model changes.
- Integration Teacher phủ accent/literal search + exact server pagination, create/update/nullable clear/duplicate, version/policy, group snapshot, password/status/delete session revoke, User boundary, audit privacy, concurrent mutation/create, fresh migration và rehearsal Initial -> Attendance -> Teacher management có dữ liệu cũ. Regression group/student còn xác minh StudentGroup list/get trả responsible Teacher name + active student count và Student list trả group code/name.
- Không có blocker backend đã biết tại thời điểm snapshot. Trước mỗi task phải kiểm tra `git status`, `tasks.md` và source vì trạng thái này có thể đã thay đổi.

## Bản đồ code

- `api/src/AdminPortal.Domain`: thêm `Teacher`, `StudentGroup`, `AttendanceSheet`, `AttendanceRecord` và attendance/group enum.
- `api/src/AdminPortal.Application`: thêm feature `Teachers`, `StudentGroups`, `Attendance`, business rules và machine-readable `ProblemCodes`; không phụ thuộc EF implementation.
- `api/src/AdminPortal.Infrastructure`: Npgsql/EF Core, mapping/migration, password/JWT, setup transaction và DI.
- `api/src/AdminPortal.Api`: controller, JWT/session validation, CSRF, CORS/rate limit, ProblemDetails, logging, OpenAPI và health.
- `api/tests/AdminPortal.UnitTests`: authorization/validation rules và `VietnameseSearchNormalizerTests.cs`.
- `api/tests/AdminPortal.IntegrationTests`: `WebApplicationFactory` + PostgreSQL Testcontainers; Teacher contract/race/lifecycle ở `TeacherManagementApiTests.cs`, upgrade rehearsal ở `MigrationUpgradeTests.cs`.
- `api/tools/AdminPortal.Maintenance` và `api/scripts/maintenance/cleanup-retention.sql`: cleanup batch audit 90 ngày/session history 30 ngày.
- Contract nền: `plans/01-BASE-admin-portal.md`; plan có thứ tự tại `plans/README.md`; hướng dẫn chạy/vận hành: `api/README.md`; sample HTTP: `api/requests.http`; tiến độ liên agent: `tasks.md`.

## Contract đang được frontend sử dụng

- API base `/api/v1`; JSON enum là chuỗi; lỗi là `ProblemDetails` có `traceId`.
- Setup anonymous: `GET /setup/status`, `POST /setup/super-admin`; POST chỉ thành công khi chưa từng có user, kể cả record soft-deleted.
- Auth: `POST /auth/login`, `GET /auth/csrf`, `POST /auth/refresh`, `POST /auth/logout`, `GET /auth/me`.
- Login/refresh trả access token, `expiresIn`, `csrfToken`, user. Access token giữ ở memory phía UI; refresh cookie là Secure/HttpOnly/SameSite=None; CSRF gửi qua `X-CSRF-TOKEN`.
- `/users` list/detail/create/full PUT/delete chỉ quản lý role `Admin` và list chỉ dành cho SuperAdmin. Teacher qua User CRUD trả `TeacherMustBeManagedViaTeachers`; ngoại lệ duy nhất là `PUT /users/{userId}/password`, tiếp tục hỗ trợ Teacher.
- Teacher management canonical: GET/POST list-create, GET/PUT/DELETE detail, và `PUT /teachers/{id}/attendance-policy`. Create tạo User+Teacher atomic; full PUT/policy nhận `expectedVersion`, DELETE nhận query `expectedVersion`; policy 1–7 ngày.
- Teacher list filter `status/groupId/unassigned`, sort whitelist theo plan và search literal mã/tên/email/phone không dấu/case-insensitive tại .NET trên toàn candidate trước total/paging. Không dùng PostgreSQL `unaccent`; blank search giữ DB fast path.
- Teacher response có `id/userId/teacherCode`, field account, policy, group count, timestamps và integer `version`; detail thêm nullable `note` và responsible group summaries. Group assignment chỉ qua StudentGroup endpoint.
- Attendance: GET context/daily; POST sheet full roster; PUT sheet full replacement; Admin/SuperAdmin historical-recovery và ba candidate API. DTO/date/query chính xác nằm trong `plans/02-ATT-attendance.md` section 7.
- Pagination mặc định page 1/pageSize 20, tối đa 100. User sort whitelist: `email`, `fullName`, `role`, `status`, `createdAt`. Student: `studentCode`, `fullName`, `nickName`, `dateOfBirth`, `gender`, `status`, `createdAt`.
- `UserStatus`: `Active`, `Inactive`, `Locked`; `StudentStatus`: `Active`, `Inactive`; `Gender`: `Male`, `Female`, `Other` hoặc null.
- Role rule: SuperAdmin quản lý Admin qua `/users` và Teacher qua `/teachers`; Admin chỉ Teacher qua `/teachers`; Teacher không truy cập API quản trị. User list không trả SuperAdmin/Teacher.

## Bất biến security và persistence

- Access token mặc định 15 phút có `sid`; JWT validation truy vấn `auth_sessions` mỗi bearer request để revoke tức thì. Refresh token 30 ngày, rotate khi refresh, DB chỉ lưu hash.
- Thay role/status/password hoặc soft-delete user phải revoke mọi session active. Logout có thể chạy không bearer bằng refresh cookie + CSRF.
- Password policy: tối thiểu 12 ký tự, có chữ hoa, chữ thường, số và ký tự đặc biệt. Login lockout sau 5 lần sai trong 15 phút. Không tiết lộ lý do auth chi tiết cho client.
- Setup dùng PostgreSQL transaction advisory lock `SetupService` và `IgnoreQueryFilters()` để chống race/mở lại setup sau soft delete.
- User/Student dùng global soft-delete query filter. Unique normalized email và student code là partial unique index trên `deleted_at IS NULL`; student code có thể tái sử dụng sau delete.
- Teacher row không hard-delete; `teacher_code` trim/uppercase/unique trên mọi Teacher row nên mã của Teacher đã xóa vẫn được giữ. Legacy profile được backfill `GV-MIG-{UUID}`. Full PUT/policy/delete tăng aggregate `version`; password và group assignment không tăng Teacher version.
- Lock order Teacher -> User -> group UUID tăng dần. Rename fullName tăng snapshot version đúng một cho mọi group vẫn đang phụ trách trong cùng transaction; code/email/phone/note/policy không tăng group snapshot. StudentGroup assignment lock Teacher trước Group để không đảo lock order.
- Mọi snapshot mutation lock group row (nhiều group theo UUID tăng dần) và tăng `snapshot_version`; create sheet cũng lock group. PUT sheet lock row và dùng `expectedVersion`; conflict trả `ProblemDetails.code` cùng version hiện tại khi có.
- Missing daily chỉ là preview Present và không ghi DB. Saved sheet giữ identity snapshot độc lập dữ liệu hiện tại. Attendance records luôn persisted kể cả Present; không có DELETE sheet/record v1.
- Ngày nghiệp vụ mặc định `Asia/Ho_Chi_Minh`; Teacher dùng policy riêng, Admin/SuperAdmin thao tác mọi ngày không tương lai. Recovery chỉ dành manager khi standard historical snapshot không khả dụng.
- Teacher audit chỉ lưu IDs, teacherCode, status/presence flags, changed fields và version transition; không lưu raw fullName/email/phone/note/password. Request logger không log body. Cleanup chạy bên ngoài API, theo batch và an toàn khi chạy lại.
- CORS chỉ dùng allow-list origin cụ thể với credentials, không wildcard. Cookie cross-site yêu cầu HTTPS cho cả API/UI.

## Những lỗi đã từng phát hiện — không tái tạo

- `AddDbContext` phải resolve `IConfiguration` lazily trong factory; nếu đọc connection string khi registration, integration test override sẽ bị bỏ qua.
- JWT bearer phải configure qua DI/`IOptions<JwtOptions>` lazily; capture option rỗng sớm từng gây `IDX10703` trong test.
- Validation attribute trên positional request record phải dùng target `[param: ...]` với ASP.NET Core 10.
- Không dùng `.Select(x => Map(x))` khi `Map` đọc navigation. EF chỉ cho client evaluation ở top-level projection nên navigation không được đưa vào SQL, từng làm `StudentGroup.responsibleTeacherName/studentCount` và Student list `groupCode/groupName` bị null/0. Dùng inline/IQueryable projection để EF translate join/count.
- `AuthSession` cần query filter tương thích soft-delete navigation `User`; bỏ nó sẽ tạo warning required-navigation và có thể làm lệch semantics session.
- Middleware request logging phải bọc exception handler đúng thứ tự để log status đã map (ví dụ setup conflict là 409, không phải 200).
- Không sửa/xóa EF Designer hoặc `AdminPortalDbContextModelSnapshot.cs`. Migration hiện có: `20260811000000_InitialCreate`, `20260811130802_AddAttendanceFoundation`, `20260811150730_AddTeacherManagement`. Migration TCH thêm/backfill `teacher_code`, `note`, integer `version`, unique/check constraints; không thêm extension/search column.
- EF tooling hiện cảnh báo required navigation trỏ tới entity có soft-delete query filter (`Teacher/User`, attendance `Student/User`). Đây là chủ ý: FK lịch sử vẫn non-null + RESTRICT; các query recovery/history dùng `IgnoreQueryFilters`. Pending-model check vẫn sạch.

## Build, test và migration nhanh

Chạy trong `api/`:

```powershell
dotnet restore AdminPortal.slnx
dotnet build AdminPortal.slnx --no-restore
dotnet test tests/AdminPortal.UnitTests --no-restore
dotnet test tests/AdminPortal.IntegrationTests --no-restore
dotnet tool restore
dotnet tool run dotnet-ef migrations has-pending-model-changes `
  --project src/AdminPortal.Infrastructure `
  --startup-project src/AdminPortal.Api
```

Integration test cần Docker engine vì dùng PostgreSQL thật. EF CLI cần process environment có `Jwt__SigningKey` tạm tối thiểu 32 ký tự; không ghi giá trị vào file/handoff.

## Deploy/IIS snapshot

- Build diễn ra trên máy source; máy IIS khác chỉ nhận package publish.
- Host cố định hiện tại: API `https://api-gv-portal.local`, UI `https://gv-portal.local`; UI origin phải khớp chính xác `Security__AllowedOrigins__0`.
- IIS target hiện tại: `C:\inetpub\api-gv-portal.local` và `C:\inetpub\gv-portal.local`, hai app pool/SNI binding riêng. API cần .NET 10 Hosting Bundle; PostgreSQL 17 chạy riêng.
- Script nguồn: `deploy/iis/build-iis-package.ps1`, `deploy/iis/deploy-iis.ps1`; hướng dẫn: `deploy/iis/HUONG-DAN-DEPLOY-IIS.md`.
- Package đã xác minh tại snapshot: `release/gv-portal-iis-20260811-102752.zip`, SHA-256 `93EA758E0DCD542FDE73A8659A9D4BC96E3C5BA51381AA60010255B9056866F1`; không chứa source, secret, PDB hoặc `appsettings.Development.json`.
- Package trên chỉ là artifact của baseline 2026-08-11. Sau bất kỳ runtime change nào phải build package mới, kiểm tra checksum/nội dung và cập nhật mục này; không deploy nhầm artifact cũ. Không chạy `-Build` trên máy IIS đích.

## Protocol cập nhật và handoff

Khi kết thúc một task backend, cập nhật ngắn gọn file này:

- Ngày và mục tiêu vừa xử lý.
- Contract/quyết định/bất biến mới; xóa hoặc sửa thông tin đã lỗi thời.
- File chính đã đổi.
- Command test/build/migration đã thực sự chạy, configuration và kết quả chính xác.
- Blocker hoặc việc tiếp theo, kèm owner nếu cần frontend/root/deploy phối hợp.
- Nếu sinh package mới, ghi tên file và SHA-256 sau khi đã verify.

Không biến memory thành nhật ký dài theo từng thao tác. Giữ “trạng thái hiện tại + lý do quan trọng + next step”. Tuyệt đối không ghi password, JWT key, token, connection string có credential, private key/certificate secret hoặc giá trị secret từ environment.
