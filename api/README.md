# Admin Portal API

RESTful API quản trị user và student, xây dựng bằng .NET 10, ASP.NET Core, EF Core 10 và PostgreSQL.

## Chức năng

- Login, refresh rotation, logout và `/me`; không có đăng ký công khai.
- Khởi tạo SuperAdmin đầu tiên qua UI/API một lần duy nhất khi database chưa có user.
- JWT access token 15 phút gắn `sid`; mọi request xác thực đều kiểm tra auth session trong PostgreSQL để revoke tức thời.
- Refresh token 30 ngày trong cookie `HttpOnly`, `Secure`, `SameSite=None`.
- Double-submit CSRF cho refresh/logout bằng cookie `XSRF-TOKEN` và header `X-CSRF-TOKEN`.
- Phân quyền `SuperAdmin`, `Admin`, `Teacher`.
- User CRUD, đổi mật khẩu, soft delete, pagination/filter/sort.
- Student CRUD, soft delete, pagination/filter/sort và tái sử dụng student code sau khi xóa.
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
DELETE /api/v1/students/{id}
```

Hai setup endpoint không yêu cầu đăng nhập. POST được rate limit và chỉ hoạt động khi bảng `users` hoàn toàn rỗng, kể cả khi xét các bản ghi đã soft delete. Mật khẩu SuperAdmin phải đạt password policy chung: tối thiểu 12 ký tự, có chữ hoa, chữ thường, số và ký tự đặc biệt.

Vì POST setup chủ đích cho phép anonymous ở lần chạy đầu, nên khởi tạo SuperAdmin ngay trước khi mở hệ thống ra Internet hoặc tạm giới hạn API/UI trong mạng tin cậy cho đến khi setup hoàn tất.

Các endpoint `PUT` là full replacement đối với toàn bộ field editable. Gửi `null` để xóa field nullable như `phoneNumber`, `gender`, `guardianName`, `guardianPhone`, `note`.

List dùng `page` (mặc định 1), `pageSize` (mặc định 20, tối đa 100), `search`, filter theo resource, `sortBy` whitelist và `sortOrder=asc|desc`.

User sort: `email`, `fullName`, `role`, `status`, `createdAt`.

Student sort: `studentCode`, `fullName`, `nickName`, `dateOfBirth`, `gender`, `status`, `createdAt`.

Enum được gửi/nhận dưới dạng chuỗi. `StudentStatus` chỉ có `Active` và `Inactive`; `Gender` có `Male`, `Female`, `Other` hoặc `null`.

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

Integration test dùng PostgreSQL thật qua Testcontainers nên Docker engine phải chạy.

## Deploy IIS local HTTPS

Bộ script build/deploy hai site `https://api-gv-portal.local` và `https://gv-portal.local`, cấu hình PostgreSQL 17, certificate local, hosts file và IIS app pools nằm tại [`../deploy/iis/HUONG-DAN-DEPLOY-IIS.md`](../deploy/iis/HUONG-DAN-DEPLOY-IIS.md).

## Cleanup retention

Chạy console app từ scheduler bên ngoài:

```powershell
dotnet run --project tools/AdminPortal.Maintenance
```

Hoặc chạy `scripts/maintenance/cleanup-retention.sql` bằng client PostgreSQL. Cả hai cách đều xóa theo batch, có thể chạy lại an toàn và không xóa session còn hoạt động.
