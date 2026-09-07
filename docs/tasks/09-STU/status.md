# Theo dõi tiến độ — Học sinh

## Tổng quan

| Status | Mã | Việc cần làm |
|---|---|---|
| `[x]` | `STU-STATUS-01` | Cho phép chuyển học sinh đang thuộc nhóm sang `Inactive` mà không gỡ nhóm |

## Phạm vi

- Backend bỏ chặn đổi trạng thái sang `Inactive` khi học sinh còn nhóm, nhưng vẫn giữ chặn xóa và phân/chuyển nhóm đối với học sinh inactive.
- Frontend cho lưu trạng thái `Đã nghỉ` khi học sinh còn nhóm, không yêu cầu gỡ nhóm trước.
- Cập nhật kiểm thử, yêu cầu sản phẩm và tài liệu quyết định liên quan.

## Kết quả kiểm tra

- Backend build: đạt, 0 warning/error.
- Backend unit tests: 113/113 đạt.
- Frontend tests: 168/168 đạt.
- Frontend development build: đạt.
- Backend integration tests: chưa chạy vì Docker daemon không khả dụng; solution build đã compile project integration thành công.
