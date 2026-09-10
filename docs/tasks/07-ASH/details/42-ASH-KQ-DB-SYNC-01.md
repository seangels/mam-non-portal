# ASH-KQ-DB-SYNC-01 — Đọc kết quả từ DB và dùng chung đồng bộ Google Sheets

## Tóm tắt ngắn

1. ✅ Trang cập nhật kết quả đọc toàn bộ catalog và mirror latest từ database; GET không gọi hoặc fallback Google Sheets.
2. ✅ Lưu trực tiếp kiểm tra version DB, chặn lệch nguồn Google/DB, ghi Google rồi cập nhật mirror DB theo đúng các dòng thay đổi.
3. ✅ `submit-results` dùng chung mirror updater để cập nhật ngay grade/note, kể cả clear, note-only và trường hợp Google không có ô cần ghi.
4. ✅ Dùng chung popup và service đồng bộ Google Sheets ở cả `/assessments` và `/assessment-results`; giữ quyền đồng bộ hiện tại của Teacher.
5. ✅ UI phân biệt xung đột DB với lệch nguồn/hậu ghi, bảo toàn draft khi đồng bộ lỗi và reload snapshot DB sau khi đồng bộ thành công.
6. ⚠️ Backend/frontend automated gate đạt; integration runtime không chạy vì Docker không sẵn có, Google live/browser smoke không chạy vì chưa có học sinh test được chỉ định.

## Contract đã chốt

- `GET /api/v1/students/{studentId}/assessment-results` chỉ đọc Student, Assessment và `AssessmentSheetLatest`/`AssessmentRecordLatest` từ database.
- `version` là token opaque theo từng dòng, sinh từ Assessment và record latest tương ứng; client chỉ gửi lại qua `expectedVersion`.
- PATCH kiểm tra toàn bộ version DB trước khi gọi Google. Version cũ trả `409 AssessmentResultsVersionConflict` và không gọi Google.
- Trước batch write, backend so sánh các ô Google dirty với baseline DB. Nếu lệch, trả `409 AssessmentResultsSourceOutOfSync` cùng `sourceConflicts` và không ghi partial.
- Sau write/readback, backend upsert hoặc delete đúng tập record latest đã sửa; note-only được giữ, grade/note cùng rỗng thì xóa.
- Nút `Đồng bộ GGSheet` dùng chung `GoogleSheetsSyncDialogComponent` và `GoogleSheetsService`; Teacher vẫn dùng tại `/assessments`, còn direct-results chỉ dành cho Admin/SuperAdmin.

## Điều kiện hoàn thành

- ✅ GET có test xác nhận toàn bộ dữ liệu đến từ DB và fake Google không được gọi.
- ✅ PATCH có test version stale trước Google, source drift không ghi partial, save/clear/note-only cập nhật Google và DB, cùng concurrency một thành công/một conflict.
- ✅ `submit-results` cập nhật mirror theo đúng tập Assessment vừa submit, kể cả no-op Google, clear và note-only.
- ✅ Hai trang mở cùng shared popup; assessment-results khóa edit/save/selector trong lúc sync, xác nhận draft, reload thành công và giữ draft khi lỗi.
- ✅ UI có CTA riêng cho DB conflict, source drift và post-write failure; loading copy nêu rõ dữ liệu portal.
- ⚠️ Release solution build 0 warning/error, backend unit **124/124**, frontend test **213/213**, development build pass hash `8fe2d28674c0566c3804`; integration runtime không chạy vì Docker không sẵn có.
