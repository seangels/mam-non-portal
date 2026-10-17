# Theo dõi tiến độ — Học sinh

## Tổng quan

| Status | Mã | Việc cần làm |
|---|---|---|
| `[x]` | `STU-STATUS-01` | Cho phép chuyển học sinh đang thuộc nhóm sang `Inactive` mà không gỡ nhóm |
| `[x]` | `STU-GSHEET-01` | Đồng bộ tạo/sửa học sinh sang Google Sheet, chỉ ghi B khi tạo khóa và C/D/H cho dữ liệu |
| `[x]` | `STU-GSHEET-02` | Gợi ý mã có B nhưng C trống khi tạo Student và chống ghi đè mã vừa được sử dụng |

## Phạm vi

- Backend bỏ chặn đổi trạng thái sang `Inactive` khi học sinh còn nhóm, nhưng vẫn giữ chặn xóa và phân/chuyển nhóm đối với học sinh inactive.
- Frontend cho lưu trạng thái `Đã nghỉ` khi học sinh còn nhóm, không yêu cầu gỡ nhóm trước.
- Cập nhật kiểm thử, yêu cầu sản phẩm và tài liệu quyết định liên quan.
- Kết nối Student create/update với tab `TT Học sinh` (gid `1543249668`) trong spreadsheet cấu hình; lookup theo `studentCode` ở B, chỉ ghi C/D/H khi cập nhật.
- Form tạo mới dùng danh sách mã B có tên C trống, chọn sẵn mã đầu tiên, hỗ trợ nhập tay và refresh khi mã gợi ý đã stale.

## Kết quả kiểm tra

- Backend build: đạt, 0 warning/error.
- Backend unit tests: 113/113 đạt.
- Frontend tests: 168/168 đạt.
- Frontend development build: đạt.
- Backend integration tests: chưa chạy vì Docker daemon không khả dụng; solution build đã compile project integration thành công.
- `STU-GSHEET-01`: backend Release build đạt 0 warning/error; unit tests 138/138 đạt; EF pending-model sạch. Integration runtime không chạy; project integration đã compile trong solution build.
- `STU-GSHEET-02`: backend Release build đạt 0 warning/error, unit 142/142; frontend test 233/233 và development build hash `9de9c4ef05cb5d81e8b8` đạt. Integration runtime không chạy; project integration đã compile.
