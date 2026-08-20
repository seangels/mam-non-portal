# 01 — Xác thực và phân quyền

## 1. Khởi tạo hệ thống lần đầu

- Khi hệ thống chưa có bất kỳ user nào, UI phải hiển thị màn hình khởi tạo `SuperAdmin` thay vì màn hình đăng nhập thông thường.
- Màn hình khởi tạo yêu cầu email, họ tên và mật khẩu mạnh.
- Chỉ được tạo đúng một `SuperAdmin` đầu tiên; sau khi đã có bất kỳ user nào, thao tác setup phải bị từ chối.
- Điều kiện “chưa có user” phải xét cả bản ghi đã soft-delete để không mở lại setup ngoài ý muốn.
- Setup không tự đăng nhập; sau khi thành công người dùng được chuyển tới màn hình đăng nhập.
- Không seed hoặc hard-code tài khoản/mật khẩu khởi tạo.

Các endpoint chức năng:

```http
GET  /api/v1/setup/status
POST /api/v1/setup/super-admin
```

## 2. Đăng nhập và phiên đăng nhập

- Không có endpoint đăng ký công khai.
- Người dùng đăng nhập bằng email và mật khẩu.
- Chỉ tài khoản hợp lệ, không bị vô hiệu hóa hoặc khóa mới đăng nhập được.
- Access token có thời hạn 15 phút và được UI giữ trong bộ nhớ.
- Refresh token có thời hạn 30 ngày, được rotate khi refresh và lưu bằng cookie `HttpOnly`, `Secure`, `SameSite=None`.
- Mọi request cần xác thực phải kiểm tra cả token và trạng thái phiên để session bị thu hồi có hiệu lực ngay.
- UI có thể khôi phục phiên sau khi tải lại trang thông qua refresh cookie và CSRF token.

Các endpoint chức năng:

```http
POST /api/v1/auth/login
GET  /api/v1/auth/csrf
POST /api/v1/auth/refresh
POST /api/v1/auth/logout
GET  /api/v1/auth/me
```

## 3. Đăng xuất và thu hồi phiên

- Logout phải hoạt động kể cả khi access token đã hết hạn nhưng refresh cookie còn hợp lệ.
- Logout xóa cookie và thu hồi session hiện tại.
- Access token gắn với session đã thu hồi phải bị từ chối ngay ở request tiếp theo.
- Đổi mật khẩu, đổi role, đổi trạng thái hoặc soft-delete tài khoản phải thu hồi toàn bộ session của tài khoản đó.

## 4. Bảo vệ đăng nhập

- Giới hạn số lần đăng nhập sai và khóa tạm tài khoản khi vượt ngưỡng.
- Áp dụng rate limit cho login, refresh và setup.
- Refresh/logout phải được bảo vệ CSRF bằng header `X-CSRF-TOKEN` và cookie tương ứng.
- Không ghi mật khẩu, access token, refresh token, cookie hoặc secret vào log/audit.

## 5. Quyền quản trị hiện hành

| Chức năng | SuperAdmin | Admin | Teacher |
|---|---:|---:|---:|
| Đăng nhập, refresh, logout, xem thông tin bản thân | Có | Có | Có |
| Quản lý tài khoản Admin | Có | Không | Không |
| Quản lý Teacher qua `/teachers` | Có | Có | Không |
| Quản lý học sinh, nhóm, lịch học, phân công | Có | Có | Không |
| Xem/tạo/sửa điểm danh mọi nhóm, ngày không tương lai | Có | Có | Không |
| Điểm danh nhóm đang phụ trách trong edit window | Không áp dụng giới hạn này | Không áp dụng giới hạn này | Có |
| Tạo thêm/quản lý SuperAdmin qua API thông thường | Không | Không | Không |

- Quyền phải được thực thi ở server dựa trên actor và resource đích; việc ẩn menu/route ở UI chỉ là hỗ trợ trải nghiệm.
- Tài nguyên ngoài scope của Teacher phải bị từ chối mà không làm lộ dữ liệu ngoài quyền.

## 6. Bảo vệ tài khoản quản trị

- Không cho người dùng tự xóa hoặc tự khóa chính mình.
- Không cho xóa hoặc vô hiệu hóa `SuperAdmin` cuối cùng.
- Chỉ `SuperAdmin` được tạo, sửa, xóa hoặc đổi trạng thái tài khoản `Admin`.
- Không có luồng tạo thêm `SuperAdmin` sau setup lần đầu.
