# STU-GSHEET-02 — Gợi ý mã học sinh trống từ Google Sheet

## Tóm tắt ngắn

1. ✅ Thêm API trả danh sách mã có B nhưng C trống, giữ thứ tự sheet và bỏ mã đang dùng trong database.
2. ✅ Form tạo mới hiển thị dropdown, chọn sẵn mã đầu tiên và vẫn cho nhập mã thủ công.
3. ✅ Sheet lỗi hoặc hết mã không chặn nhập tay; UI hiển thị cảnh báo rõ ràng.
4. ✅ Create kiểm tra lại C trước khi ghi và không ghi đè mã vừa được người khác sử dụng.
5. ✅ Bổ sung backend/frontend tests, verification, requirement và memory.

## Contract và hành vi

- ✅ `GET /api/v1/students/student-code-suggestions`, policy `PortalManagers`, response `{ "items": ["S118", "S119"] }`.
- ✅ Ứng viên có B khác rỗng, C rỗng/whitespace, chưa thuộc Student chưa soft-delete; giữ thứ tự dòng và từ chối B trùng.
- ✅ Khi mở create, tải danh sách và chọn sẵn item đầu; select box cho đổi lựa chọn và nhập custom value.
- ✅ Mã đã có B/C trống: create chỉ ghi C/D/H. Mã nhập tay chưa có B: ghi B/C/D/H theo flow hiện hành.
- ✅ Mã đã có B/C không trống: `409 StudentGoogleSheetsCodeUnavailable`, không ghi Sheet/DB; UI giữ dữ liệu, refresh suggestions, chọn item đầu mới và yêu cầu lưu lại.
- ✅ Không reservation, không auto-resubmit, không đổi schema/database migration.

## Verification

- ✅ Backend Release build 0 warning/error; unit tests 142/142; integration project compile đạt.
- ✅ Frontend Node portable `test:ci` 233/233; development build đạt hash `9de9c4ef05cb5d81e8b8`.
- ⚠️ Integration runtime not run (Docker unavailable).
- ✅ Không gọi ghi Google Sheet thật và không chạy production package/deploy.
