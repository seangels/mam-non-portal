# ASH-PICKER-MULTI-01 — Chọn và thêm nhiều mục từ Assessment picker

## Tóm tắt ngắn

1. ✅ Picker trên form chỉnh sửa AssessmentSheet có cột checkbox để chọn nhiều dòng.
2. ✅ Toolbar DataGrid có nút thêm toàn bộ mục đang chọn vào sheet trong một lần lưu.
3. ✅ Không thêm trùng các mục đã có; giữ thao tác thêm/xóa từng dòng hiện hữu.
4. ✅ Chỉ xóa selection sau khi lưu thành công; lỗi API giữ selection để thử lại.
5. ✅ Bổ sung unit test và chạy `test:ci` cùng development build của frontend.
6. ✅ Follow-up: thêm thao tác `Chọn tất cả` cho toàn bộ dòng hợp lệ đang thỏa bộ lọc; bỏ chọn chỉ tác động tập đang lọc và giữ lựa chọn ngoài bộ lọc.

## Phạm vi

- Frontend: `assessment-picker.component.*`, `assessment-sheets-form.component.*` và unit test liên quan.
- Dùng lại `PUT /assessment-sheets/{id}/records` theo contract full-replace; không đổi backend/REST/schema.
- Không production build, IIS hoặc deploy.

## Điều kiện hoàn thành

- ✅ Checkbox chọn nhiều dòng xuất hiện trong picker ở chế độ thêm; dòng đã có bị vô hiệu hóa.
- ✅ Nút toolbar `Thêm các mục đã chọn (n)` thể hiện số dòng đã chọn và bị vô hiệu hóa khi không có dòng hợp lệ hoặc form bị khóa.
- ✅ Một lần bấm tạo đúng một request chứa records cũ cộng các mục mới, không trùng mã.
- ✅ Thành công cập nhật sheet và xóa selection; thất bại hiển thị lỗi và giữ selection.
- ✅ `npm --prefix ui run test:ci` đạt 175/175 (root chạy lại độc lập cũng đạt 175/175).
- ✅ `$env:NG_BUILD_MAX_WORKERS='1'; npm --prefix ui run build -- --configuration development` đạt, hash `2def996d74e49dd50ffe`; chỉ có warning CommonJS/DevExtreme đã biết.
- ⚠️ Chưa smoke thủ công trên trình duyệt.
- ✅ `Chọn tất cả` có trạng thái checked/indeterminate, bộ đếm và ARIA label/hint; chọn trên toàn bộ kết quả khớp filter thay vì chỉ trang hiện tại.
