# Kế hoạch thiết kế RESTful API cho Admin Portal

- **Mã kế hoạch:** `BASE`
- **Thứ tự:** `01`
- **Trạng thái:** đã triển khai; là contract nền cho các plan phía sau.

## 1. Mục tiêu

Xây dựng RESTful API cho Admin Portal với các chức năng cơ bản:

- Đăng nhập và đăng xuất; không hỗ trợ đăng ký công khai.
- Admin quản lý user: xem danh sách, xem chi tiết, thêm, sửa, xóa.
- Admin quản lý student: xem danh sách, xem chi tiết, thêm, sửa, xóa.
- Danh sách hỗ trợ phân trang, tìm kiếm, lọc và sắp xếp.
- Sử dụng .NET 10, ASP.NET Core Web API, Entity Framework Core và PostgreSQL.
- Code rõ ràng, gọn, dễ kiểm thử và dễ chỉnh sửa.

## 2. Phạm vi phiên bản đầu tiên

### Trong phạm vi

- Authentication bằng access token và refresh token.
- Phân quyền `SuperAdmin`, `Admin`, `Teacher`.
- CRUD user và student.
- Soft delete.
- Audit các thao tác quản trị quan trọng.
- Kế hoạch nhóm học sinh và điểm danh được đặc tả riêng tại [`02-ATT-attendance.md`](02-ATT-attendance.md).
- Kế hoạch quản lý thông tin giáo viên được đặc tả riêng tại [`03-TCH-teacher-management.md`](03-TCH-teacher-management.md); các quyết định `TCH-DEC-*` phải được chốt trước khi triển khai.
- OpenAPI/Swagger.
- Health check, logging và xử lý lỗi thống nhất.
- Unit test và integration test cho các luồng chính.
- Docker Compose phục vụ phát triển local.
- SQL cleanup script và console app riêng để dọn audit log/auth session hết hạn.

### Chưa nằm trong phạm vi

- Đăng ký tài khoản công khai.
- Quên hoặc đặt lại mật khẩu qua email.
- Đăng nhập Google, Microsoft hoặc nhà cung cấp bên ngoài.
- Import/export Excel.
- Upload ảnh hoặc tài liệu học sinh.
- Phân quyền chi tiết theo từng chức năng.
- Quản lý học phí.

Các chức năng ngoài phạm vi có thể bổ sung sau mà không cần thay đổi kiến trúc tổng thể.

## 3. Kiến trúc đề xuất

Sử dụng **modular monolith kết hợp feature folders**. Không áp dụng microservices ở giai đoạn đầu.

```text
src/
├── AdminPortal.Api
│   ├── Controllers
│   ├── Authentication
│   ├── Middleware
│   └── Program.cs
├── AdminPortal.Application
│   ├── Auth
│   ├── Users
│   ├── Students
│   └── Common
├── AdminPortal.Domain
│   ├── Entities
│   └── Enums
└── AdminPortal.Infrastructure
    ├── Persistence
    ├── Migrations
    └── Security

tests/
├── AdminPortal.UnitTests
└── AdminPortal.IntegrationTests

tools/
└── AdminPortal.Maintenance

scripts/
└── maintenance/
    └── cleanup-retention.sql
```

### Trách nhiệm từng project

#### `AdminPortal.Api`

- Định nghĩa HTTP endpoint.
- Authentication và authorization middleware.
- Cấu hình dependency injection.
- Chuyển request thành application command/query.
- Chuyển kết quả thành HTTP response.
- Không chứa business logic.

#### `AdminPortal.Application`

- Chứa use case và business rule.
- Request/response DTO.
- Validation.
- Interface cho các dịch vụ hạ tầng cần thiết.

#### `AdminPortal.Domain`

- Entity và enum cốt lõi.
- Business rule không phụ thuộc database hoặc HTTP.

#### `AdminPortal.Infrastructure`

- EF Core `DbContext`.
- Entity configuration và migrations.
- Cài đặt authentication, refresh token và các dịch vụ hạ tầng.

#### `AdminPortal.Maintenance`

- Console app chạy thủ công hoặc theo scheduler bên ngoài.
- Xóa audit log quá 90 ngày.
- Xóa lịch sử auth session/refresh token đủ điều kiện quá 30 ngày.
- Dùng cùng connection-string convention với API nhưng chạy độc lập với API.

### Nguyên tắc giữ code gọn

- Controller chỉ nhận request, gọi use case và trả response.
- Không trả EF entity trực tiếp ra API.
- Không tạo `GenericRepository`; EF Core đã cung cấp repository/unit-of-work semantics.
- Không dùng MediatR trong phiên bản đầu nếu chưa có nhu cầu pipeline phức tạp.
- Query đọc sử dụng `AsNoTracking()`.
- Không tạo abstraction chỉ để bọc một lời gọi EF Core đơn giản.
- Tổ chức code theo tính năng để file liên quan nằm gần nhau.

## 4. Công nghệ

- .NET 10.
- ASP.NET Core Web API.
- Entity Framework Core 10.
- PostgreSQL.
- `Npgsql.EntityFrameworkCore.PostgreSQL` 10.
- `PasswordHasher<TUser>` của ASP.NET Core Identity cho password hashing; role, status và lockout được quản lý trong domain/schema đơn giản của ứng dụng.
- JWT Bearer access token.
- Refresh token lưu bằng cookie bảo mật.
- OpenAPI/Swagger.
- xUnit.
- Testcontainers PostgreSQL cho integration test.
- Docker/Docker Compose cho môi trường local.

Tham khảo:

- [Npgsql EF Core 10 release notes](https://www.npgsql.org/efcore/release-notes/10.0.html)
- [ASP.NET Core API authentication behavior](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/api-endpoint-auth?view=aspnetcore-10.0)

## 5. Thiết kế dữ liệu

Tất cả bảng sử dụng:

- UUID làm khóa chính.
- `timestamptz` và lưu thời gian theo UTC.
- Tên bảng/cột dạng `snake_case` trong PostgreSQL.

### 5.1. Bảng `users`

```text
id                  uuid PK
email               varchar(255)
normalized_email    varchar(255)
password_hash       text
full_name           varchar(200)
phone_number        varchar(30) nullable
role                varchar(30)
status              varchar(30)
failed_login_count  integer
lockout_end          timestamptz nullable
created_at          timestamptz
updated_at          timestamptz
deleted_at          timestamptz nullable
```

Role ban đầu:

- `SuperAdmin`
- `Admin`
- `Teacher`

Status ban đầu:

- `Active`
- `Inactive`
- `Locked`

User trong hệ thống gồm tài khoản quản trị và giáo viên. Tài khoản `Active` có thể xác thực, nhưng quyền gọi endpoint được quyết định theo role:

- `SuperAdmin`: quản lý admin và giáo viên.
- `Admin`: chỉ quản lý giáo viên và student.
- `Teacher`: không được gọi các API quản trị trong phạm vi phiên bản này.

### 5.2. Bảng `students`

Schema student đã chốt cho phiên bản đầu tiên:

```text
id                uuid PK
student_code      varchar(50)
full_name         varchar(200)
nick_name         varchar(200)
date_of_birth     date
gender            varchar(20) nullable
status            varchar(30)
guardian_name     varchar(200) nullable
guardian_phone    varchar(30) nullable
note              text nullable
created_at        timestamptz
updated_at        timestamptz
deleted_at        timestamptz nullable
```

Student là resource độc lập và không liên kết với user.

### 5.3. Bảng `auth_sessions`

```text
id                        uuid PK
user_id                   uuid FK -> users.id
refresh_token_hash        varchar(255)
refresh_token_expires_at  timestamptz
revoked_at                timestamptz nullable
created_at                timestamptz
last_refreshed_at         timestamptz nullable
created_by_ip             varchar(64) nullable
```

Chỉ lưu hash của refresh token, không lưu token nguyên bản. Access token chứa claim `sid` trỏ tới `auth_sessions.id`; session phải còn hiệu lực ở thời điểm xử lý request.

### 5.4. Bảng `audit_logs`

```text
id                  uuid PK
actor_user_id       uuid nullable FK -> users.id
action              varchar(100)
entity_type         varchar(100)
entity_id           uuid nullable
old_values          jsonb nullable
new_values          jsonb nullable
ip_address          varchar(64) nullable
created_at          timestamptz
```

Audit tối thiểu các hành động:

- Đăng nhập thành công hoặc thất bại.
- Đăng xuất và thu hồi session.
- Tạo, sửa, xóa hoặc đổi trạng thái user.
- Tạo, sửa, xóa student.

## 6. Authentication và authorization

### 6.1. Endpoint

```http
GET  /api/v1/setup/status
POST /api/v1/setup/super-admin

POST /api/v1/auth/login
POST /api/v1/auth/refresh
POST /api/v1/auth/logout
GET  /api/v1/auth/csrf
GET  /api/v1/auth/me
```

Không tạo endpoint đăng ký.

### 6.2. Luồng đăng nhập

1. Chuẩn hóa và tìm user theo email.
2. Kiểm tra mật khẩu qua `PasswordHasher<TUser>` của ASP.NET Core Identity.
3. Kiểm tra trạng thái tài khoản và thời gian lockout.
4. Tạo một auth session trong database.
5. Phát hành access token sống 15 phút, có claim `sid` là ID của auth session.
6. Phát hành refresh token sống 30 ngày.
7. Lưu hash refresh token trong auth session.
8. Gửi refresh token bằng cookie `HttpOnly`, `Secure`, `SameSite=None` vì frontend chạy khác site.
9. Trả access token và CSRF token cho frontend; frontend gửi access token qua header `Authorization: Bearer ...`.
10. Khi bootstrap lại ứng dụng, frontend gọi `GET /api/v1/auth/csrf` bằng refresh cookie để lấy lại CSRF token vì JavaScript ở site frontend không thể đọc cookie host-only của API.

Mọi endpoint yêu cầu xác thực phải kiểm tra cả chữ ký/hạn của JWT và trạng thái auth session tương ứng. Việc kiểm tra session trong PostgreSQL trên mỗi request là đánh đổi có chủ đích để hỗ trợ vô hiệu hóa access token ngay lập tức. Có thể bổ sung distributed cache sau nếu đo đạc cho thấy cần, nhưng cache không được làm mất yêu cầu thu hồi tức thời.

### 6.3. Logout

- Thu hồi refresh token/session hiện tại bằng refresh cookie; logout vẫn hoạt động khi access token đã hết hạn.
- Xóa refresh-token cookie.
- Trả `204 No Content`.
- Mọi access token mang `sid` của session đã thu hồi bị từ chối ngay ở request tiếp theo, kể cả khi JWT chưa hết hạn.

### 6.4. Khởi tạo admin đầu tiên

- Khi frontend khởi động, gọi `GET /api/v1/setup/status`; nếu bảng `users` chưa có bất kỳ bản ghi nào thì chuyển đến màn hình `/setup`.
- Màn hình setup nhận email, họ tên và mật khẩu mạnh rồi gọi `POST /api/v1/setup/super-admin` để tạo tài khoản `SuperAdmin` đầu tiên.
- Hai setup endpoint không yêu cầu đăng nhập, nhưng POST chỉ hoạt động khi bảng `users` hoàn toàn rỗng, kể cả khi xét các bản ghi đã soft delete; sau khi có user phải trả `409 Conflict`.
- Dùng PostgreSQL transaction advisory lock và kiểm tra lại điều kiện bên trong transaction để nhiều request/API instance đồng thời vẫn chỉ tạo được một tài khoản.
- Rate limit POST setup; không hard-code hoặc lưu mật khẩu khởi tạo trong source, config, audit hay log.
- Khởi tạo ngay trong mạng tin cậy trước khi public deployment để tránh bên ngoài chiếm quyền tạo tài khoản đầu tiên.
- Sau khi tạo thành công, frontend chuyển đến màn hình đăng nhập; setup endpoint không tự tạo auth session.

### 6.5. Quy tắc bảo vệ tài khoản quản trị

- Admin không được tự xóa hoặc tự khóa chính mình.
- Không được xóa/vô hiệu hóa `SuperAdmin` cuối cùng.
- Chỉ `SuperAdmin` được tạo, sửa, xóa hoặc đổi trạng thái admin khác.
- `Admin` chỉ được quản lý user có role `Teacher`.
- Không cung cấp API để tạo thêm `SuperAdmin`; endpoint setup chỉ tạo tài khoản đầu tiên khi database hoàn toàn rỗng.
- Thay đổi mật khẩu, role, trạng thái hoặc soft delete user làm thu hồi toàn bộ auth session của user đó.
- Giới hạn số lần đăng nhập sai và áp dụng lockout.
- Rate limit endpoint login và refresh.
- Không ghi password, access token hoặc refresh token vào log.

Tham khảo [ASP.NET Core rate limiting](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0).

### 6.6. Ma trận quyền phiên bản đầu

| Chức năng | SuperAdmin | Admin | Teacher |
|---|---:|---:|---:|
| Login, refresh, logout, xem `/auth/me` | Có | Có | Có |
| Xem/tạo/sửa/xóa user `Teacher` | Có | Có | Không |
| Xem/tạo/sửa/xóa user `Admin` | Có | Không | Không |
| Tạo thêm hoặc quản lý `SuperAdmin` qua user API | Không | Không | Không |
| Xem/tạo/sửa/xóa student | Có | Có | Không |

Quyền phải được kiểm tra ở application use case dựa trên actor và target resource, không chỉ dựa vào việc endpoint có `[Authorize]`.

## 7. User API

### 7.1. Endpoint

```http
GET    /api/v1/users
POST   /api/v1/users
GET    /api/v1/users/{id}
PUT    /api/v1/users/{id}
PUT    /api/v1/users/{id}/password
DELETE /api/v1/users/{id}
```

### 7.2. Lấy danh sách

```http
GET /api/v1/users?page=1&pageSize=20
    &search=nguyen
    &status=Active
    &role=Teacher
    &createdFrom=2026-01-01
    &createdTo=2026-08-11
    &sortBy=createdAt
    &sortOrder=desc
```

Quy tắc:

- `page` bắt đầu từ 1.
- `pageSize` mặc định 20, tối đa 100.
- `search` tìm trên email, họ tên và số điện thoại.
- Chỉ cho phép `sortBy` theo whitelist.
- Luôn thêm `id` làm khóa sắp xếp phụ để thứ tự ổn định.
- Không trả password hash hoặc dữ liệu authentication nội bộ.
- `SuperAdmin` có thể truy vấn `Admin` và `Teacher`; `Admin` chỉ nhận được user `Teacher` trong kết quả.

### 7.3. Tạo user

Request dự kiến:

```json
{
  "email": "user@example.com",
  "fullName": "Nguyễn Văn A",
  "phoneNumber": "0900000000",
  "role": "Teacher",
  "status": "Active",
  "password": "..."
}
```

Kết quả:

- Trả `201 Created`.
- Gửi header `Location` trỏ tới `/api/v1/users/{id}`.
- Email trùng trả `409 Conflict`.
- Admin tạo user bằng mật khẩu được nhập trực tiếp; không có luồng kích hoạt/gửi email.
- Không trả lại mật khẩu trong response và không ghi mật khẩu vào log.
- `SuperAdmin` có thể tạo `Admin` hoặc `Teacher`; `Admin` chỉ có thể tạo `Teacher`.

### 7.4. Cập nhật và xóa

- Dùng `PUT` với đầy đủ dữ liệu editable để có thể xóa rõ ràng các trường nullable bằng `null`.
- Không cho client sửa trực tiếp các trường audit hoặc password hash.
- Đổi role hoặc quản lý tài khoản `Admin` là quyền riêng của `SuperAdmin`.
- Đổi mật khẩu qua endpoint riêng `PUT /api/v1/users/{id}/password` và thu hồi toàn bộ auth session của user.
- Delete thực hiện soft delete và thu hồi toàn bộ auth session của user.
- Trả `404 Not Found` nếu bản ghi không tồn tại hoặc đã bị xóa.

## 8. Student API

### 8.1. Endpoint

```http
GET    /api/v1/students
POST   /api/v1/students
GET    /api/v1/students/{id}
PUT    /api/v1/students/{id}
DELETE /api/v1/students/{id}
```

### 8.2. Lấy danh sách

```http
GET /api/v1/students?page=1&pageSize=20
    &search=HS001
    &status=Active
    &gender=Female
    &dateOfBirthFrom=2020-01-01
    &dateOfBirthTo=2021-12-31
    &sortBy=fullName
    &sortOrder=asc
```

`search` dự kiến tìm trên:

- Mã học sinh.
- Tên học sinh.
- Tên thường gọi (`nickName`).
- Tên người giám hộ.
- Số điện thoại người giám hộ.

### 8.3. Quy tắc dữ liệu

- `studentCode` bắt buộc và không được trùng giữa các bản ghi đang hoạt động.
- `fullName` và `dateOfBirth` bắt buộc.
- `dateOfBirth` không được lớn hơn ngày hiện tại.
- Delete thực hiện soft delete.
- Cho phép tái sử dụng `studentCode` của bản ghi đã soft delete.
- Khi tra cứu theo `studentCode`, mặc định chỉ xét student chưa bị xóa.

## 9. Chuẩn request và response

### 9.1. JSON

- Dùng `camelCase`.
- Ngày không có thời gian dùng định dạng `YYYY-MM-DD`.
- Thời điểm dùng ISO 8601 UTC, ví dụ `2026-08-11T03:30:00Z`.
- Không trả các trường có tính chất bí mật.

### 9.2. Response danh sách

```json
{
  "items": [],
  "pagination": {
    "page": 1,
    "pageSize": 20,
    "totalItems": 135,
    "totalPages": 7
  }
}
```

### 9.3. Error response

Dùng `ProblemDetails` thống nhất toàn API:

```json
{
  "type": "https://api.example.com/problems/validation-error",
  "title": "Dữ liệu không hợp lệ",
  "status": 400,
  "detail": "Một hoặc nhiều trường không hợp lệ",
  "errors": {
    "email": ["Email không đúng định dạng"]
  },
  "traceId": "..."
}
```

### 9.4. HTTP status code

| Status | Sử dụng |
|---|---|
| `200 OK` | Đọc hoặc cập nhật thành công |
| `201 Created` | Tạo resource thành công |
| `204 No Content` | Xóa hoặc logout thành công |
| `400 Bad Request` | Request hoặc validation không hợp lệ |
| `401 Unauthorized` | Chưa đăng nhập hoặc token không hợp lệ |
| `403 Forbidden` | Đã đăng nhập nhưng không đủ quyền |
| `404 Not Found` | Không tìm thấy resource |
| `409 Conflict` | Trùng email, mã học sinh hoặc xung đột nghiệp vụ |

## 10. Phân trang, filter và sắp xếp

Phiên bản đầu dùng offset pagination vì admin portal thường cần:

- Nhảy trực tiếp đến một trang.
- Hiển thị tổng số bản ghi.
- Hiển thị tổng số trang.

Quy tắc sắp xếp:

```sql
ORDER BY created_at DESC, id DESC
```

Mọi kiểu sort đều phải có một trường unique như `id` làm điều kiện phụ để tránh bỏ sót hoặc lặp bản ghi.

Khi số lượng dữ liệu tăng lớn, có thể bổ sung cursor/keyset pagination cho luồng next/previous. Microsoft lưu ý offset pagination tốn chi phí ở trang sâu và khuyến nghị keyset pagination khi không cần truy cập ngẫu nhiên theo số trang.

Tham khảo [EF Core pagination guidance](https://learn.microsoft.com/en-us/ef/core/querying/pagination).

## 11. Index PostgreSQL

Index ban đầu:

- Unique partial index trên `users.normalized_email` khi `deleted_at IS NULL`.
- Unique partial index trên `students.student_code` khi `deleted_at IS NULL`; cách này cho phép tái sử dụng mã của student đã soft delete.
- Index `(status, created_at, id)` trên users và students.
- Unique index trên `auth_sessions.refresh_token_hash`.
- Index `(user_id, revoked_at, refresh_token_expires_at)` trên auth sessions.
- Index `(entity_type, entity_id, created_at)` trên audit logs.

Chỉ thêm PostgreSQL `pg_trgm` cho tìm kiếm tên/email khi dữ liệu hoặc đo đạc hiệu năng cho thấy cần thiết.

## 12. Bảo mật

- Bắt buộc HTTPS ở production.
- JWT signing key lấy từ secret manager/environment và có kế hoạch rotation.
- Refresh-token cookie dùng `HttpOnly` và `Secure`.
- Frontend chạy khác site nên refresh-token cookie dùng `SameSite=None` và bắt buộc `Secure`.
- Danh sách frontend origin được cấu hình bằng `Security:AllowedOrigins` trong `appsettings`, có thể override bằng environment ở từng môi trường.
- CORS chỉ cho phép origin nằm trong `Security:AllowedOrigins`, chỉ cho phép credential khi cần gửi refresh-token cookie và không dùng wildcard origin.
- Endpoint refresh/logout dùng cookie cross-site nên gửi `X-CSRF-TOKEN` lấy từ response login/refresh hoặc `GET /auth/csrf`; API so sánh với cookie host-only `XSRF-TOKEN`.
- Rate limit login và refresh.
- Account lockout khi đăng nhập sai nhiều lần.
- Giới hạn độ dài tất cả chuỗi đầu vào.
- Sort/filter chỉ nhận giá trị trong whitelist.
- Log không chứa dữ liệu nhạy cảm.
- Swagger chỉ mở ở development hoặc được bảo vệ ở production.
- Không đưa connection string hoặc secret vào source control.

Ví dụ cấu hình không chứa secret:

```json
{
  "Security": {
    "AllowedOrigins": [
      "https://admin.example.com"
    ]
  }
}
```

## 13. Logging, audit và vận hành

- Structured logging.
- Correlation/trace ID cho mỗi request.
- Global exception handler trả `ProblemDetails`.
- Log request method, path, status và duration; không log body chứa thông tin nhạy cảm.
- Endpoint kiểm tra trạng thái:
  - `GET /health/live`
  - `GET /health/ready`
- Readiness kiểm tra kết nối PostgreSQL.
- Audit log tách khỏi application log.

### 13.1. Dọn dữ liệu định kỳ

Cung cấp cả SQL script và console project `AdminPortal.Maintenance` để scheduler bên ngoài có thể chạy định kỳ:

- Xóa `audit_logs` có `created_at` quá 90 ngày.
- Xóa auth session đã hết hạn hoặc đã bị thu hồi và đủ điều kiện lưu lịch sử quá 30 ngày.
- Không xóa auth session còn hoạt động.
- Cleanup chạy theo batch để tránh khóa bảng hoặc tạo transaction quá lớn.
- Log số dòng đã xóa, thời gian chạy và lỗi; không log token hash hoặc dữ liệu audit chi tiết.
- API không tự chạy background cleanup để tránh chạy trùng khi scale nhiều instance.
- Với auth session, mốc retention được tính từ `revoked_at` nếu đã thu hồi, nếu không thì từ `refresh_token_expires_at`; chỉ xóa khi mốc này đã quá 30 ngày.

`scripts/maintenance/cleanup-retention.sql` và console app phải dùng cùng một quy tắc retention. Console app có thể được chạy bằng cron, Kubernetes CronJob, Windows Task Scheduler hoặc CI/CD scheduler tùy môi trường triển khai.

## 14. Testing

### 14.1. Unit test

- Validation login.
- Chính sách role `Admin`/`SuperAdmin`.
- Không thể tự xóa hoặc tự khóa tài khoản.
- Không thể vô hiệu hóa `SuperAdmin` cuối cùng.
- Validation user và student.
- Whitelist filter/sort.

### 14.2. Integration test

Chạy với PostgreSQL thật qua Testcontainers:

- Login đúng, sai, inactive và locked.
- Refresh-token rotation.
- Logout, auth-session revocation và xác nhận access token cũ bị từ chối ngay.
- Authorization `401` và `403`.
- CRUD user.
- CRUD student.
- Phân trang, filter và sort.
- Email/mã học sinh trùng.
- Soft delete.
- Quy tắc bảo vệ admin.
- `Admin` không thể quản lý tài khoản `Admin`; `SuperAdmin` có thể quản lý `Admin` và `Teacher`.
- Tái sử dụng `studentCode` sau khi bản ghi cũ đã soft delete.
- CORS và CSRF cho luồng refresh/logout cross-site.
- Maintenance cleanup giữ đúng retention 90/30 ngày và không xóa session còn hoạt động.
- ProblemDetails đúng schema.
- Setup status đúng với database rỗng/đã có user và hai request khởi tạo đồng thời chỉ có một request thành công.

Không dùng SQLite thay cho PostgreSQL trong integration test vì kiểu dữ liệu, index và hành vi query có khác biệt.

## 15. Lộ trình triển khai

### Giai đoạn 1: Khởi tạo nền tảng

- Tạo solution và project structure.
- Cấu hình PostgreSQL, EF Core và migrations.
- Tạo Docker Compose cho local.
- Cấu hình OpenAPI, error handling, logging và health check.

**Tiêu chí hoàn thành:** API chạy được, kết nối PostgreSQL thành công, migration có thể apply từ môi trường sạch.

### Giai đoạn 2: Authentication

- User/auth-session schema và password hashing.
- Setup API/UI tạo SuperAdmin đầu tiên khi database rỗng.
- Login, refresh, logout và `/me`.
- Auth session validation để vô hiệu hóa access token ngay khi logout.
- Authorization policy, rate limit, lockout, CORS và CSRF protection.

**Tiêu chí hoàn thành:** User active có thể xác thực; chỉ role được cấp quyền mới gọi được API quản trị; refresh/logout hoạt động cross-site an toàn; access token của session đã logout bị từ chối ngay.

### Giai đoạn 3: User management

- CRUD user.
- Pagination, search, filter và sort.
- Soft delete.
- Quy tắc `SuperAdmin` quản lý admin và `Admin` chỉ quản lý giáo viên.
- Endpoint đổi mật khẩu và thu hồi toàn bộ auth session của user.

**Tiêu chí hoàn thành:** Toàn bộ user API có integration test cho happy path và lỗi chính.

### Giai đoạn 4: Student management

- CRUD student.
- Pagination, search, filter và sort.
- Validation và unique student code.
- Cho phép tái sử dụng student code sau khi bản ghi cũ đã soft delete.

**Tiêu chí hoàn thành:** Toàn bộ student API có integration test cho happy path và lỗi chính.

### Giai đoạn 5: Audit và hardening

- Audit log.
- Kiểm tra CORS, cookie, secret và dữ liệu nhạy cảm trong log.
- Kiểm thử rate limit và lockout.
- Rà soát index và query.
- Chuẩn bị SQL cleanup script và project `AdminPortal.Maintenance`.
- Kiểm thử retention audit log 90 ngày và auth-session history 30 ngày.

**Tiêu chí hoàn thành:** Các thao tác quản trị quan trọng truy vết được và checklist bảo mật được xác nhận.

### Giai đoạn 6: Đóng gói và bàn giao

- Docker image.
- README hướng dẫn cấu hình, migration, setup lần đầu và chạy test.
- Hướng dẫn cấu hình `Security:AllowedOrigins`, cookie cross-site và lịch chạy maintenance.
- OpenAPI contract và sample request.
- Pipeline build/test cơ bản.

**Tiêu chí hoàn thành:** Một thành viên mới có thể chạy hệ thống từ môi trường sạch theo README.

## 16. Definition of Done

Một tính năng được xem là hoàn thành khi:

- Endpoint đúng contract OpenAPI.
- Có validation và authorization phù hợp.
- Không expose entity hoặc dữ liệu nhạy cảm.
- Có unit/integration test tương xứng với rủi ro.
- Migration và index cần thiết đã được thêm.
- Error response tuân theo `ProblemDetails`.
- Logging/audit không chứa secret.
- Code đã format và không có warning đáng chú ý.
- README được cập nhật nếu thay đổi cấu hình hoặc cách chạy.

## 17. Các quyết định nghiệp vụ đã chốt

- User gồm tài khoản quản trị và giáo viên; không dùng role chung chung `User`.
- Student là resource độc lập, không liên kết với user.
- Schema student sử dụng đúng danh sách trường ở mục 5.2.
- Admin tạo user bằng mật khẩu nhập trực tiếp; không gửi email kích hoạt.
- Chỉ `SuperAdmin` được quản lý tài khoản `Admin`; `Admin` chỉ quản lý `Teacher`.
- User và student sử dụng soft delete.
- Cho phép tái sử dụng `studentCode` sau khi student cũ đã soft delete.
- Frontend và API chạy khác site; danh sách origin được cấu hình trong `appsettings` và override theo môi trường.
- Logout phải vô hiệu hóa cả refresh token và access token ngay lập tức thông qua auth session.
- Audit log được giữ 90 ngày.
- Lịch sử auth session/refresh token đủ điều kiện được giữ 30 ngày.
- Cung cấp SQL cleanup script và một console project riêng; lịch chạy do hạ tầng bên ngoài quản lý.

## 18. Quy ước triển khai mặc định

- Access token sống 15 phút.
- Refresh token sống 30 ngày và được rotate mỗi lần refresh.
- Offset pagination mặc định 20 bản ghi, tối đa 100.
- Audit đăng nhập/đăng xuất và toàn bộ CRUD do admin thực hiện.
- Với cập nhật/xóa, audit lưu các giá trị trước và sau cần thiết để truy vết; không lưu password, token hoặc secret.
- Cleanup dữ liệu chạy theo batch và có thể chạy lại an toàn.

## 19. Kế hoạch mở rộng điểm danh

Feature điểm danh dùng mã epic `ATT` và được chia thành các đợt `ATT-00` đến `ATT-05`.

- Plan cross-stack, schema, REST contract, authorization, UI card-list, test và các quyết định cần review: [`02-ATT-attendance.md`](02-ATT-attendance.md).
- Trạng thái triển khai chi tiết được theo dõi trong `../tasks/README.md` và các thư mục `../tasks/<NN-CODE>/` theo đúng mã đợt.
- Toàn bộ portal chỉ sử dụng tiếng Việt cho visible/accessibility text, bao gồm chuỗi mặc định của DevExtreme; identifier kỹ thuật trong API vẫn dùng tiếng Anh và được frontend ánh xạ tập trung sang nhãn tiếng Việt.
- Các mô tả “Student là resource độc lập” và “Teacher không gọi API quản trị” ở baseline vẫn đúng với CRUD cũ. Khi feature `ATT` được triển khai, Student có `group_id` hiện tại, group có một responsible Teacher hiện tại và mỗi phiếu đã lưu giữ full daily snapshot gồm cả `Present`; không dùng assignment có `effective_from/effective_to`. Teacher chỉ được gọi API điểm danh đã scope theo group đang phụ trách và vẫn không có quyền CRUD quản trị.
