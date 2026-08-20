# 07 — API, bảo mật và vận hành

## 1. Quy ước REST

- Tất cả endpoint nghiệp vụ dùng prefix `/api/v1`.
- JSON dùng `camelCase`; enum serialize dạng string.
- Ngày không có thời gian dùng `YYYY-MM-DD`.
- Thời điểm dùng ISO 8601 UTC.
- Create trả `201 Created` và `Location` khi có resource mới.
- Delete/logout thành công trả `204 No Content` khi không cần body.
- Full update dùng `PUT`; trường nullable gửi `null` để xóa rõ ràng.
- Không trả password hash, token, cookie, secret hoặc dữ liệu xác thực nội bộ.

## 2. Danh sách, filter và sort

Response danh sách thống nhất:

```json
{
  "items": [],
  "pagination": {
    "page": 1,
    "pageSize": 20,
    "totalItems": 0,
    "totalPages": 0
  }
}
```

- `page` bắt đầu từ 1.
- `pageSize` mặc định 20 và tối đa 100.
- Filter phải được áp dụng trước count/paging.
- Sort chỉ nhận field trong whitelist, không nhận biểu thức động.
- Mọi sort phải có khóa phụ unique để thứ tự giữa các trang ổn định.
- Query mâu thuẫn, ví dụ vừa chọn `groupId` vừa `unassigned=true`, trả validation error.

## 3. Lỗi API

- Mọi lỗi dùng `application/problem+json` theo `ProblemDetails`.
- Response lỗi có `status`, `code` ổn định, `traceId` và `fieldErrors` khi phù hợp.
- UI xử lý theo `code`, không parse nội dung `title/detail`.
- Quy ước status:
  - `400`: request/validation không hợp lệ.
  - `401`: chưa đăng nhập hoặc token/session không hợp lệ.
  - `403`: đã đăng nhập nhưng không đủ quyền.
  - `404`: resource không tồn tại, đã xóa hoặc cần che tài nguyên ngoài scope.
  - `409`: trùng dữ liệu, stale version hoặc xung đột nghiệp vụ.
- Conflict version/snapshot trả version hiện tại khi có thể để UI hỗ trợ reload.

## 4. Bảo mật

- Production bắt buộc HTTPS.
- JWT signing key, connection string và secret lấy từ cấu hình an toàn của môi trường, không commit vào source.
- Refresh cookie phải là `HttpOnly`, `Secure`, `SameSite=None`.
- CORS chỉ cho phép origin được cấu hình; khi cho credential không được dùng wildcard origin.
- Refresh/logout bắt buộc CSRF protection.
- Login, refresh và setup phải rate-limit.
- Tài khoản có lockout khi đăng nhập sai nhiều lần.
- Giới hạn độ dài mọi chuỗi đầu vào.
- Mọi authorization nghiệp vụ được kiểm tra ở server, không tin role/resource scope do client gửi.
- Swagger/OpenAPI chỉ mở ở môi trường phù hợp hoặc được bảo vệ tại production.
- Không log request body nhạy cảm, password, token, cookie, token hash hoặc secret.

## 5. Concurrency và tính toàn vẹn

- Teacher và Student sử dụng `version/expectedVersion` cho các mutation có nguy cơ lost update.
- Attendance sử dụng snapshot version cho lần tạo và sheet version cho cập nhật.
- Stale request phải bị từ chối toàn bộ, không update một phần.
- Giới hạn 100 Student/group phải đúng kể cả khi có nhiều request đồng thời.
- Một ngày/nhóm chỉ có một attendance sheet.
- Full-roster attendance phải lưu nguyên tử: hoặc toàn bộ thành công hoặc không ghi gì.

## 6. Audit

Audit tối thiểu:

- Login/logout và thay đổi tài khoản.
- CRUD Teacher, Student và Group.
- Đổi mật khẩu, trạng thái hoặc soft-delete.
- Gán/gỡ Teacher, phân/chuyển/gỡ Student.
- Thay đổi policy điểm danh.
- Tạo, cập nhật và khôi phục phiếu điểm danh.

Yêu cầu audit:

- Ghi actor, resource IDs, action, thời điểm, version và các field thay đổi cần thiết.
- Không ghi raw password, token, cookie, note, địa chỉ hoặc toàn bộ request body.
- Audit thao tác thay đổi được giữ 90 ngày.
- Attendance sheet/record là dữ liệu nghiệp vụ và được giữ lâu dài.

## 7. Logging và health check

- Structured logging có correlation/trace ID.
- Ghi method, path, status, duration và lỗi đã chuẩn hóa; không ghi dữ liệu nhạy cảm.
- Có endpoint:

```http
GET /health/live
GET /health/ready
```

- Readiness phải kiểm tra khả năng kết nối PostgreSQL.
- Lỗi chưa xử lý được chuyển thành `ProblemDetails`, không trả stack trace cho client.

## 8. Retention và cleanup

- Audit log quá 90 ngày được dọn theo batch.
- Auth session hết hạn hoặc bị thu hồi đủ điều kiện được giữ 30 ngày rồi dọn.
- Không dọn session còn hoạt động.
- Không dọn attendance sheet/record hoặc Teacher profile lịch sử.
- Cung cấp cả SQL cleanup và công cụ maintenance để scheduler bên ngoài chạy; API không tự chạy cleanup nền khi scale nhiều instance.
- Cleanup có thể chạy lại an toàn và chỉ log số dòng/thời gian/lỗi, không log dữ liệu chi tiết.

## 9. Chất lượng và khả năng bảo trì

- Code phải gọn, rõ ràng, dễ đọc, dễ kiểm thử và dễ chỉnh sửa.
- Không tạo hai nguồn mutation cho cùng nghiệp vụ.
- Contract backend/frontend phải thống nhất; OpenAPI là mô tả API có thể kiểm chứng.
- Các luồng chính phải có unit/integration/UI regression phù hợp, nhưng chi tiết test và lộ trình triển khai nằm ngoài requirements này.
