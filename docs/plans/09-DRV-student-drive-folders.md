# 09 — DRV — Thư mục Google Drive của giáo viên và học sinh

## 1. Mục tiêu

Hệ thống tự bảo đảm mỗi học sinh có một thư mục Google Drive và đặt thư mục đó đúng vị trí theo phân công hiện tại:

- Học sinh chưa có giáo viên phụ trách nằm trong pool cấu hình bởi `GoogleSheets:StudentDriveFolderPoolId`.
- Học sinh thuộc nhóm có giáo viên phụ trách nằm trong thư mục Drive của giáo viên đó.
- Tên chuẩn của thư mục học sinh là `<studentCode> - <fullName>`.

Teacher có `driveFolderId` riêng. Giá trị này được nhập bằng ID hoặc URL folder và lưu dưới dạng canonical folder ID.

## 2. Quy tắc vòng đời

- Tạo Student không truyền folder: tìm chính xác tên chuẩn trong pool; không có thì tạo, một kết quả thì tái sử dụng, nhiều kết quả thì báo lỗi.
- Tạo Student truyền folder: xác minh đó là folder Drive hợp lệ rồi move vào pool.
- Update Student để trống folder giữ folder hiện tại; dữ liệu cũ chưa có folder thì tự provision. Ghi đè ID dùng folder mới và đưa về destination hiện tại.
- Sửa mã/họ tên không tự đổi tên folder. Action rename riêng dùng dữ liệu Student đã lưu và không tăng Student version.
- Gán/chuyển/gỡ nhóm Student và gán/đổi/gỡ giáo viên phụ trách đều đồng bộ destination ngay.
- Teacher chưa có folder không được gán làm giáo viên phụ trách. Không được xóa folder khỏi Teacher đang phụ trách nhóm.
- Đổi folder Teacher phải move học sinh của mọi nhóm đang phụ trách trước khi commit.

## 3. REST contract

Teacher list/detail/create/update thêm `driveFolderId: string?`.

```http
POST /api/v1/students/{studentId}/drive-folder/rename
Content-Type: application/json

{ "expectedVersion": 3 }
```

Action rename chỉ dành cho Admin/SuperAdmin, dùng dữ liệu đã lưu, không tăng version và trả ID, tên cùng web link canonical của folder.

```http
POST /api/v1/students/drive-folders/reconcile
```

Reconcile xử lý mọi Student chưa soft-delete, kể cả Inactive. Mỗi Student là một đơn vị độc lập; lỗi một bản ghi không dừng phần còn lại. Response có các bộ đếm `processed`, `created`, `reused`, `moved`, `unchanged`, `failed` và danh sách lỗi an toàn theo Student.

## 4. Nhất quán và lỗi

- Luồng nghiệp vụ thường preflight cấu hình, quyền truy cập và destination trước khi đổi Drive.
- Drive lỗi thì không commit thay đổi DB. Nếu DB lỗi sau side effect Drive, hệ thống bù trừ tốt nhất bằng cách move về parent cũ hoặc xóa folder rỗng vừa tạo.
- Reconcile cho phép thành công một phần và báo cáo chi tiết để chạy lại.
- Không trả raw Google exception, credential, token hoặc dữ liệu nhạy cảm qua API, audit hay log.

## 5. Giao diện

- Form/list/detail Teacher cho nhập hoặc mở folder Drive.
- Form sửa Student giữ input ghi đè và có nút `Cập nhật tên thư mục Drive`; nút chỉ hoạt động khi form không có draft chưa lưu.
- Trang Học sinh có nút `Đồng bộ thư mục Drive`, confirm trước khi chạy và hiển thị tổng hợp cùng lỗi theo từng Student bằng tiếng Việt.

## 6. Kiểm thử bắt buộc

- Normalize ID/URL, validate folder, exact-name lookup, create/reuse/duplicate và move/no-op.
- Student create/update/rename/reconcile; Teacher create/update; mọi biến thể gán nhóm và giáo viên.
- Drive failure không commit DB; bù trừ được gọi khi DB thất bại sau Drive.
- UI serialize đúng contract, khóa double-submit/dirty rename và hiển thị báo cáo reconcile.
- Backend build/unit, EF pending-model check, frontend test/development build. Integration chỉ bắt buộc khi Docker sẵn sàng; production build/package theo gate `$gv-portal-production`.
