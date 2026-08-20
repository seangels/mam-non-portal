# 03 — Học sinh và nhóm

## 1. Quản lý học sinh

Admin và SuperAdmin phải có thể xem, tạo, sửa và soft-delete học sinh. Teacher không được sử dụng API/màn hình quản trị học sinh.

Thông tin học sinh:

- Mã học sinh.
- Họ tên đầy đủ.
- Tên thường gọi (`nickName`).
- Ngày sinh.
- Giới tính tùy chọn.
- Trạng thái `Active` hoặc `Inactive`.
- Tên và số điện thoại người giám hộ tùy chọn.
- Ghi chú tùy chọn.
- Nhóm hiện tại.
- Lịch học hiện tại và version.

Student là resource độc lập, không phải tài khoản đăng nhập và không liên kết với User.

Các endpoint chức năng:

```http
GET    /api/v1/students
POST   /api/v1/students
GET    /api/v1/students/{studentId}
PUT    /api/v1/students/{studentId}
DELETE /api/v1/students/{studentId}?expectedVersion={version}
```

## 2. Quy tắc dữ liệu học sinh

- Mã học sinh, họ tên và ngày sinh là bắt buộc.
- Ngày sinh không được nằm trong tương lai.
- Mã học sinh không được trùng giữa các bản ghi chưa bị xóa.
- Cho phép tái sử dụng mã của học sinh đã soft-delete.
- Student inactive không được phân vào nhóm mới.
- Student đang thuộc nhóm không được chuyển sang inactive hoặc xóa; phải gỡ khỏi nhóm trước.
- Create/update phải kèm lịch học hợp lệ theo tài liệu [04](04-lich-hoc-hoc-sinh.md).
- Full update, đổi nhóm và xóa phải dùng `expectedVersion` để tránh ghi đè dữ liệu cũ.

## 3. Danh sách học sinh

- Hỗ trợ phân trang, search, filter và sort trên server.
- Search trên mã, họ tên, nickname, tên và số điện thoại người giám hộ.
- Filter gồm trạng thái, giới tính, khoảng ngày sinh, nhóm, chưa phân nhóm, hình thức học và ngày học.
- `Nhóm` và `Chưa phân nhóm` loại trừ lẫn nhau.
- Danh sách hiển thị nhóm hiện tại và tóm tắt lịch học bằng nhãn tiếng Việt.
- Đổi filter phải quay lại trang 1; tổng số bản ghi lấy từ server.

## 4. Quản lý nhóm

Admin và SuperAdmin phải có thể:

- Tạo, xem, sửa, chuyển inactive và xóa nhóm khi thỏa điều kiện nghiệp vụ.
- Tìm kiếm, lọc, phân trang và sắp xếp danh sách nhóm.
- Gán hoặc gỡ một giáo viên phụ trách hiện tại.
- Xem và quản lý roster học sinh.
- Cấu hình policy điểm danh của giáo viên tại trang `Nhóm`.

Quy tắc:

- Mỗi nhóm có mã, tên và trạng thái `Active|Inactive`.
- Mỗi nhóm có tối đa một giáo viên phụ trách hiện tại.
- Một giáo viên có thể phụ trách nhiều nhóm.
- Mỗi học sinh có tối đa một nhóm hiện tại.
- Một nhóm có tối đa 100 học sinh active.
- Không có `effectiveFrom/effectiveTo`; phân công có hiệu lực ngay.
- Lịch sử đã xác nhận được bảo toàn trong snapshot điểm danh, không suy ra từ assignment hiện tại.
- Không seed nhóm hoặc phân công; mọi thao tác được thực hiện qua UI/API.

## 5. Phân, chuyển và gỡ nhóm cho học sinh

Nguồn mutation duy nhất:

```http
PUT /api/v1/students/{studentId}/group
```

Yêu cầu:

- Gán nhóm là thao tác riêng, không đặt `groupId` trong create/full update Student.
- Trang `Học sinh` và trang `Nhóm` phải dùng cùng một hành vi API.
- Popup chỉ cho chọn nhóm active, hiển thị mã, tên và số lượng hiện tại trên 100.
- Không cho gán học sinh thứ 101; server luôn là nơi quyết định cuối cùng.
- Chuyển/gỡ phải xác nhận rõ nhóm cũ và nhóm mới.
- Nếu học sinh đã có attendance record trong ngày hiện tại, không cho chuyển hoặc gỡ nhóm trong ngày đó.
- Request dùng `expectedVersion`; stale request phải bị từ chối.
- Gửi lại đúng nhóm hiện tại với version đúng là no-op; không tăng version.
- Sau thành công UI phải dùng response mới để cập nhật version và trạng thái nhóm.

## 6. Lifecycle nhóm

- Nhóm inactive không nhận phân công mới hoặc Student mới.
- Không xóa nhóm nếu còn dữ liệu/phân công vi phạm điều kiện nghiệp vụ.
- Việc đổi mã, tên, giáo viên phụ trách hoặc roster phải cập nhật snapshot version để thao tác điểm danh stale bị phát hiện.
- Phiếu điểm danh Saved không bị thay đổi khi nhóm, giáo viên hoặc roster hiện tại thay đổi.

Các endpoint chức năng:

```http
GET    /api/v1/student-groups
POST   /api/v1/student-groups
GET    /api/v1/student-groups/{groupId}
PUT    /api/v1/student-groups/{groupId}
DELETE /api/v1/student-groups/{groupId}
PUT    /api/v1/student-groups/{groupId}/responsible-teacher
```

## 7. Giao diện

- Sidebar có mục `Học sinh` và `Nhóm` cho Admin/SuperAdmin.
- Trang Học sinh có action `Phân nhóm`, `Chuyển nhóm`, `Gỡ khỏi nhóm` theo trạng thái hiện tại.
- Trang Nhóm hỗ trợ workflow roster-centric, gán/gỡ Teacher và policy.
- Nhóm đủ 100 được disable trong picker nhưng lỗi server vẫn phải được hiển thị rõ nếu có race.
- Student inactive chưa có nhóm không được phân nhóm; dữ liệu legacy inactive còn nhóm vẫn phải cho gỡ.
- Tất cả confirm, validation, empty/error state và accessibility text bằng tiếng Việt.
