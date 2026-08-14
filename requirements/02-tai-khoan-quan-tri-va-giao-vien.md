# 02 — Tài khoản quản trị và giáo viên

## 1. Ranh giới quản lý

- `/users` là bề mặt quản lý **tài khoản quản trị `Admin`**, chỉ `SuperAdmin` được truy cập.
- `/teachers` là bề mặt canonical để Admin/SuperAdmin tạo, xem, sửa và xóa giáo viên.
- Không cho tạo/sửa/xóa Teacher qua User CRUD vì sẽ bỏ qua version, snapshot và audit của Teacher.
- Đổi mật khẩu Teacher vẫn sử dụng endpoint user-password riêng bằng `userId` của Teacher.

## 2. Tài khoản quản trị

Tài khoản Admin có các thông tin:

- Email đăng nhập.
- Họ tên.
- Số điện thoại tùy chọn.
- Trạng thái `Active`, `Inactive` hoặc `Locked`.
- Mật khẩu chỉ nhận khi tạo/đổi mật khẩu và không bao giờ trả lại.

Yêu cầu:

- Danh sách hỗ trợ phân trang, tìm kiếm, lọc trạng thái và sắp xếp.
- Tạo tài khoản bằng mật khẩu nhập trực tiếp; không có email kích hoạt.
- Email không được trùng.
- Cập nhật dùng full replacement cho các trường editable; field nullable có thể gửi `null` để xóa.
- Xóa là soft-delete và thu hồi toàn bộ session.
- Không trả password hash, token, session hoặc chi tiết bảo mật nội bộ.

Các endpoint chức năng:

```http
GET    /api/v1/users
POST   /api/v1/users
GET    /api/v1/users/{userId}
PUT    /api/v1/users/{userId}
PUT    /api/v1/users/{userId}/password
DELETE /api/v1/users/{userId}
```

## 3. Dữ liệu giáo viên

Teacher sử dụng dữ liệu tài khoản cho email, họ tên, số điện thoại, trạng thái và mật khẩu. Phần hồ sơ Teacher chỉ bổ sung:

- `teacherCode`: bắt buộc, người dùng tự nhập, được phép sửa, trim/uppercase, tối đa 50 ký tự và không trùng với mã Teacher hiện tại.
- `note`: tùy chọn, tối đa 2.000 ký tự.
- `attendanceEditWindowDays`: từ 1 đến 7 ngày, mặc định 7.
- `version`: dùng để phát hiện dữ liệu cũ khi sửa, xóa hoặc cập nhật policy.
- Thời điểm tạo/cập nhật do hệ thống quản lý.

Không thu thập trong v1: ngày sinh, giới tính, địa chỉ, trình độ, chuyên môn, ngày vào làm, trạng thái nhân sự riêng, ảnh hoặc hồ sơ nhạy cảm.

## 4. Chức năng quản lý giáo viên

Admin và SuperAdmin phải có thể:

- Xem danh sách và chi tiết.
- Tạo Teacher cùng tài khoản đăng nhập trong một thao tác nhất quán.
- Sửa mã giáo viên, họ tên, email, số điện thoại, trạng thái và ghi chú.
- Đổi mật khẩu bằng luồng riêng.
- Soft-delete Teacher khi không còn phụ trách nhóm.
- Xem danh sách nhóm đang phụ trách ở chế độ chỉ đọc.
- Đi đến màn hình `Nhóm` để phân/gỡ nhóm hoặc cấu hình policy điểm danh.

Các endpoint chức năng:

```http
GET    /api/v1/teachers
POST   /api/v1/teachers
GET    /api/v1/teachers/{teacherId}
PUT    /api/v1/teachers/{teacherId}
DELETE /api/v1/teachers/{teacherId}?expectedVersion={version}
PUT    /api/v1/teachers/{teacherId}/attendance-policy
PUT    /api/v1/users/{userId}/password
```

## 5. Danh sách và tìm kiếm giáo viên

- Phân trang mặc định 20, cho phép 10/20/50/100 và tối đa 100.
- Filter theo trạng thái tài khoản, nhóm phụ trách hoặc chưa phụ trách nhóm.
- Search trên mã giáo viên, họ tên, email và số điện thoại.
- Search phải là literal substring, không phân biệt hoa/thường và không phân biệt dấu tiếng Việt; `nguyen` phải khớp `Nguyễn`, `dang` phải khớp `Đặng`.
- Kết quả và `totalItems` phải được tính sau search, trước phân trang.
- Sort chỉ nhận trường được cho phép và phải ổn định giữa các trang.
- Không search nội dung ghi chú.

## 6. Tạo và cập nhật

- Tạo Teacher yêu cầu mã, họ tên, email, trạng thái, mật khẩu; số điện thoại và ghi chú là tùy chọn.
- Teacher mới nhận policy sửa điểm danh mặc định 7 ngày; policy không nằm trong form tạo/sửa hồ sơ.
- Full update phải gửi `expectedVersion`; thành công tăng version đúng một lần.
- Cập nhật bằng version cũ phải trả conflict cùng version hiện tại và không ghi dữ liệu một phần.
- Lỗi trùng mã hoặc email phải được phân biệt rõ.
- Trường nullable phải có thể xóa bằng `null`.
- Đổi họ tên Teacher đang phụ trách nhóm phải làm thay đổi version snapshot của các nhóm liên quan; phiếu điểm danh đã lưu không bị sửa lại.
- Đổi mã, email, số điện thoại, ghi chú hoặc policy không làm thay đổi snapshot nhóm.

## 7. Xóa và lifecycle

- Không hard-delete Teacher profile vì dữ liệu lịch sử điểm danh còn tham chiếu.
- Xóa Teacher phải soft-delete tài khoản liên kết và thu hồi toàn bộ session.
- Không cho xóa Teacher đang phụ trách bất kỳ nhóm nào; người quản trị phải gỡ/chuyển nhóm trước.
- Mã hiện tại của Teacher đã xóa tiếp tục được giữ để bảo toàn định danh lịch sử.
- Attendance sheet đã lưu không bị rewrite khi hồ sơ Teacher thay đổi hoặc bị xóa.

## 8. Chính sách điểm danh

- Policy từ 1 đến 7 ngày, mặc định 7.
- Policy chỉ được sửa tại bề mặt quản lý hiện có trong trang `Nhóm`, không đặt trong form Teacher.
- Cập nhật policy phải gửi `expectedVersion` và tham gia cùng cơ chế version của Teacher.
- Nhóm Teacher đang phụ trách chỉ được hiển thị read-only ở trang chi tiết Teacher; việc gán/gỡ chỉ thực hiện ở trang `Nhóm`.

## 9. Giao diện

- Sidebar có mục `Giáo viên` cho Admin/SuperAdmin.
- `/users` đổi nhãn thành `Tài khoản quản trị` và chỉ SuperAdmin thấy.
- Danh sách Teacher có panel filter mặc định mở, grid remote, trạng thái loading/empty/error và responsive.
- Form tạo/sửa dùng trang riêng, hai cột trên desktop và một cột trên mobile.
- Create có mật khẩu/xác nhận mật khẩu; edit không hiển thị password.
- Form có dirty guard, chống submit lặp, focus lỗi đầu tiên; conflict phải giữ draft và cho phép tải bản mới.
- Mọi label, validation, tooltip, dialog và ARIA đều bằng tiếng Việt.
