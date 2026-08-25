# Hướng dẫn cho Backend agent

## Phạm vi và vai trò

File này áp dụng cho toàn bộ cây `api/`. Agent `backend` sở hữu implementation .NET, database schema/migration, API contract, test backend và tài liệu vận hành trong `api/`.

- Giữ thay đổi trong `api/` trừ khi nhiệm vụ hiện tại giao rõ phạm vi tích hợp khác. Không tự sửa `ui/`, `deploy/`, `release/` hoặc section của agent khác trong `../docs/tasks/**`.
- Khi API contract thay đổi, phải nêu tác động cho root/frontend trước khi coi công việc hoàn tất. Đồng bộ plan liên quan trong `../docs/plans/`, `api/README.md`, `api/requests.http` và test khi chúng nằm trong phạm vi nhiệm vụ.
- Workspace dùng chung: đọc `git status`/`git diff` trước khi sửa, bảo toàn thay đổi của người dùng và agent khác, không reset/checkout/xóa thay đổi ngoài nhiệm vụ.
- Không coi artifact trong `artifacts/` hoặc `release/` là source of truth. Chúng phải được build lại sau thay đổi backend có ảnh hưởng runtime.

## Bắt đầu một session Backend mới

Đọc theo thứ tự:

1. File này.
2. `../.agents/backend/MEMORY.md` để lấy handoff bền vững.
3. `../docs/plans/README.md`, `../docs/plans/01-BASE-admin-portal.md`, `../docs/requirements/README.md`, feature plan/requirement liên quan, `README.md` (contract vận hành), `../docs/tasks/README.md` và phần Backend/Integration/Deploy liên quan trong `../docs/tasks/**`.
4. Source, config, migration và test trực tiếp liên quan đến nhiệm vụ.
5. `git status --short` và diff trong `api/` để nhận diện thay đổi chưa bàn giao.

Memory chỉ là snapshot định hướng. Khi memory mâu thuẫn với code/test hiện tại, xác minh bằng source và báo sự lệch; không âm thầm dựa vào thông tin cũ.

## Kiến trúc phải giữ

- Target hiện tại là .NET 10 / ASP.NET Core / EF Core 10 / PostgreSQL 17; nullable bật và warning được coi là error qua `Directory.Build.props`.
- Dependency direction: `Domain` chứa entity/enum; `Application` chứa use case, DTO, validation, authorization và interface; `Infrastructure` chứa EF Core/PostgreSQL, password/token và setup implementation; `Api` chỉ composition root, HTTP/auth middleware/controller.
- Controller phải mỏng. Không expose EF entity trực tiếp. Không thêm Generic Repository hoặc MediatR khi chưa có quyết định kiến trúc mới.
- `AdminPortal.Maintenance` và `scripts/maintenance/cleanup-retention.sql` phải giữ cùng semantics retention; API không tự chạy cleanup nền vì có thể scale nhiều instance.
- JSON enum dùng chuỗi; lỗi ứng dụng dùng `ProblemDetails` và có `traceId`; list endpoint dùng pagination giới hạn `pageSize <= 100` và sort/filter whitelist.

## Contract và bất biến quan trọng

- Base route là `/api/v1`. Endpoint hiện hành gồm setup, auth, users và students như liệt kê trong `README.md`.
- `PUT /users/{id}` và `PUT /students/{id}` là full replacement cho các field editable. `null` chủ động xóa field nullable; không đổi sang PATCH nullable mơ hồ nếu chưa thống nhất lại contract với frontend.
- Role: `SuperAdmin` quản lý `Admin` và `Teacher`; `Admin` chỉ quản lý `Teacher`; `Teacher` không được vào API quản trị. Không cho quản lý `SuperAdmin` qua User CRUD và không cho actor tự update/delete tài khoản quản trị hiện tại.
- User và Student là soft-delete. Email chuẩn hóa và `studentCode` chỉ unique trên bản ghi active bằng partial unique index; `studentCode` được tái sử dụng sau soft delete.
- Đổi role/status/password hoặc xóa user phải revoke toàn bộ auth session đang hoạt động của user.
- Setup là luồng anonymous một lần: `GET /setup/status`, `POST /setup/super-admin`. Kiểm tra phải bao gồm cả user soft-deleted (`IgnoreQueryFilters`) và POST phải giữ PostgreSQL transaction advisory lock để nhiều instance vẫn chỉ tạo một SuperAdmin đầu tiên.
- Access token mặc định 15 phút, chứa `sid`; mọi bearer request phải kiểm tra session PostgreSQL để logout/revoke có hiệu lực ngay. Refresh token mặc định 30 ngày, chỉ lưu hash trong DB và rotate mỗi lần refresh.
- Refresh cookie là `HttpOnly`, `Secure`, `SameSite=None`; cookie CSRF không HttpOnly. Refresh/logout dùng double-submit cookie `XSRF-TOKEN` và header `X-CSRF-TOKEN`. Logout chủ ý cho anonymous để vẫn revoke bằng refresh cookie + CSRF khi bearer đã hết hạn.
- Audit phủ auth và CRUD nhưng tuyệt đối không ghi password, raw token, token hash, secret hoặc request body nhạy cảm. Retention: audit 90 ngày; session đã revoke/hết hạn đủ điều kiện 30 ngày; không xóa session còn hoạt động.

## Database và migration

- PostgreSQL là database chuẩn, dùng snake_case, query filter soft-delete và index trong các EF configuration. Không dùng SQLite để thay integration test PostgreSQL.
- Không sửa tay hoặc xóa `Persistence/Migrations/AdminPortalDbContextModelSnapshot.cs`; mọi model change cần migration mới, Designer/snapshot tương ứng và kiểm tra pending model changes.
- `DbContext` và JWT bearer hiện resolve configuration lazily qua DI để `WebApplicationFactory` override đúng cấu hình test. Không đổi lại thành capture configuration/options tại registration time.
- `Database__MigrateOnStartup=true` chỉ bật cho instance có trách nhiệm apply migration. Trong triển khai nhiều instance, phải có một chủ thể migration rõ ràng.

Lệnh migration chạy từ `api/`:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef migrations add <MigrationName> `
  --project src/AdminPortal.Infrastructure `
  --startup-project src/AdminPortal.Api `
  --output-dir Persistence/Migrations
dotnet tool run dotnet-ef migrations has-pending-model-changes `
  --project src/AdminPortal.Infrastructure `
  --startup-project src/AdminPortal.Api
```

EF CLI qua startup project cần `Jwt__SigningKey` tối thiểu 32 ký tự trong environment của process. Dùng giá trị tạm cục bộ; không ghi nó vào repo, log hoặc memory.

## Build và test

Chạy từ `api/` với mức kiểm tra tương xứng thay đổi:

```powershell
dotnet restore AdminPortal.slnx
dotnet build AdminPortal.slnx --no-restore
dotnet test tests/AdminPortal.UnitTests --no-restore
dotnet test tests/AdminPortal.IntegrationTests --no-restore
```

- Integration test dùng PostgreSQL thật qua Testcontainers nên Docker engine phải chạy. Nếu bị chặn bởi môi trường, ghi chính xác blocker; không đánh dấu pass và không thay bằng provider khác. Nếu user chủ động giới hạn checkpoint là build/unit/no DB runtime, bỏ qua integration trong checkpoint đó và ghi rõ "not run/skipped", không ghi là pass.
- Với thay đổi auth/setup/persistence/API contract, ưu tiên chạy cả integration suite từ database sạch. Với model change, chạy thêm `migrations has-pending-model-changes`.
- Không dùng `--no-build` nếu chưa build đúng configuration trong cùng lượt kiểm tra. Ghi lại command, configuration và số test pass/fail trong handoff.

## IIS và đóng gói máy khác

- Hướng dẫn/scripting nguồn nằm ở `../deploy/iis/`; backend target là `https://api-gv-portal.local`, UI origin là `https://gv-portal.local`.
- IIS production nhận cấu hình qua environment variables trong generated `web.config`: `ASPNETCORE_ENVIRONMENT`, `ConnectionStrings__DefaultConnection`, `Jwt__SigningKey`, `Security__AllowedOrigins__0`, `Database__MigrateOnStartup` và các option JWT liên quan. Không hard-code giá trị thật vào source/artifact.
- Máy source build package bằng `../deploy/iis/build-iis-package.ps1`; máy IIS đích triển khai package đã publish và không cần .NET SDK, Node hoặc npm. Không truyền `-Build` trên máy đích.
- API trên IIS cần .NET 10 Hosting Bundle/ANCM; PostgreSQL là dịch vụ riêng. Cookie cross-site bắt buộc cả API và UI chạy HTTPS đúng hostname/origin.
- Không tự chạy deploy làm thay đổi IIS, certificate store, hosts file, `C:\inetpub` hoặc database nếu nhiệm vụ chỉ yêu cầu build/package/review.

## Definition of Done và handoff

Một thay đổi backend chỉ hoàn tất khi contract/validation/authorization đúng, không lộ entity hoặc dữ liệu nhạy cảm, migration/index đầy đủ nếu cần, test phù hợp đã chạy, build không warning và tài liệu/sample request đã đồng bộ khi có thay đổi vận hành.

Cuối mỗi nhiệm vụ có thay đổi hoặc phát hiện quan trọng:

1. Cập nhật `../.agents/backend/MEMORY.md`: ngày, trạng thái hiện tại, quyết định/bất biến mới, file chính, lệnh kiểm tra và kết quả, blocker/next step.
2. Thay thế thông tin lỗi thời thay vì nối log dài vô hạn. Phân biệt rõ “đã xác minh trong lượt này” với baseline cũ.
3. Nếu nhiệm vụ cho phép cập nhật task, chỉ cập nhật phần Backend hoặc log tích hợp được root giao trong `../docs/tasks/**`; không sửa trạng thái agent khác. Không ghi vào root `../tasks.md` vì file đó đã legacy/frozen.
4. Bàn giao rõ thay đổi contract/migration/deploy cần root hoặc frontend xử lý.

Không bao giờ lưu vào `AGENTS.md`, memory, task log hoặc source control: mật khẩu, JWT signing key, refresh/access/CSRF token, connection string có credential, certificate private key hay secret từ environment. Chỉ ghi tên biến và placeholder không nhạy cảm.
