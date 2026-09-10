# ASH-DEL-01 — Xóa vĩnh viễn bảng đánh giá trong app

## Tóm tắt ngắn

1. ✅ Thêm API xóa vĩnh viễn một `AssessmentSheet` cùng toàn bộ `AssessmentRecord` phụ thuộc trong database app.
2. ✅ Không gọi Google API và không xóa Google Sheet, file PDF/Drive, danh mục `Assessment` hoặc dữ liệu latest.
3. ✅ UI có nút xóa nguy hiểm, xác nhận rõ phạm vi trước khi gọi API và khóa thao tác khi đang xóa.
4. ✅ Xóa thành công quay về danh sách; hủy xác nhận hoặc API lỗi không làm mất state hiện tại.
5. ✅ Có audit, NotFound/authorization nhất quán và regression tests backend/frontend.
6. ✅ Backend Release build + unit test và frontend test + development build đạt; integration chưa chạy vì Docker không khả dụng.

## Phạm vi

- Backend: `DELETE /api/v1/assessment-sheets/{id}`, service xóa records trước vì FK hiện tại dùng `Restrict`, rồi xóa sheet trong cùng `SaveChanges` transaction/unit of work.
- Frontend: service mapping và thao tác xóa trên AssessmentSheet UI với confirm tiếng Việt.
- Không migration nếu model/FK không đổi; không production build, IIS hoặc deploy.
- Không xóa hoặc cập nhật bất kỳ tài nguyên Google nào, kể cả các link đang được lưu trên sheet.

## Điều kiện hoàn thành

- ✅ Teacher/Admin/SuperAdmin xóa được sheet ở mọi trạng thái, kể cả `Done`.
- ✅ ID không tồn tại trả ProblemDetails `AssessmentSheetNotFound`/404; thành công trả `204`.
- ✅ Records bị xóa explicit trước sheet trong cùng `SaveChanges`; không cần migration.
- ✅ Audit `AssessmentSheet.Deleted` chỉ chứa metadata an toàn, không chứa note/link/raw record.
- ✅ UI xác nhận rõ phạm vi app-only, cảnh báo dirty state, khóa mutation và giữ form khi hủy/lỗi.
- ✅ Backend build 0 warning/error + unit **118/118**; frontend test **190/190** + development build hash `a214c33dee979cf7f732`. Integration test đã compile nhưng chưa chạy (Docker không khả dụng); browser smoke chưa chạy.
