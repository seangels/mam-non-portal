# ASH-KQ-DIRECT-01 — Cập nhật trực tiếp kết quả Google Sheet theo học sinh

## Tóm tắt ngắn

1. ✅ Thêm màn hình manager-only chọn học sinh và đọc trực tiếp toàn bộ kết quả từ `[F0.ĐG]`.
2. ✅ Cho sửa `grade`/`note` tại client, chỉ ghi batch khi bấm nút lưu trên sticky bar.
3. ✅ Chặn ghi đè bằng version theo dòng; xung đột trả `409` và không ghi partial.
4. ✅ Sau khi Google ghi thành công, cập nhật mirror latest của đúng học sinh và audit từng ô thay đổi.
5. ✅ Chặn `Teacher` ở menu, route và API; hỗ trợ cả học sinh đang học lẫn đã nghỉ.
6. ⚠️ Automated gate đã đạt; integration runtime không chạy vì Docker không khả dụng, Google live smoke không chạy vì chưa có học sinh test được chỉ định.

## Contract đã chốt

- `GET /api/v1/students/{studentId}/assessment-results` đọc live `[F0.ĐG]`, ghép toàn bộ catalog `Assessment` và trả version opaque theo dòng.
- `PATCH /api/v1/students/{studentId}/assessment-results` chỉ nhận các dòng đã sửa với `assessmentId`, `expectedVersion`, `grade`, `note`.
- `grade` nhận `A|B|C|D|null`; `note` tối đa 2.000 ký tự; `null`/rỗng dùng để xóa ô.
- Chỉ `SuperAdmin`/`Admin`; `Teacher` nhận `403`.
- Sau save, rebuild `AssessmentSheetLatest`/`AssessmentRecordLatest` của học sinh đang chọn; full sync cũng phải giữ record note-only.
- Không migration, production build, IIS package hoặc deploy.

## Điều kiện hoàn thành

- ✅ UI route `/#/assessment-results`, menu `Cập nhật kết quả`, DataGrid có selector học sinh, edit client-only, sticky save và nút lên đầu/xuống cuối trang.
- ✅ GET phản ánh giá trị live Google thay vì cache portal; PATCH chỉ batch-update ô thực sự đổi.
- ✅ Version stale trả `AssessmentResultsVersionConflict` và không ghi bất kỳ ô nào; integration test concurrency xác nhận đúng một lượt thành công và một lượt conflict khi cùng version.
- ✅ Mirror latest của học sinh phản ánh grade/note mới ngay sau save; note-only không bị full sync làm mất.
- ✅ Có audit tổng hợp/từng ô và xử lý riêng trường hợp Google đã ghi nhưng readback/DB thất bại.
- ✅ Backend Release build 0 warning/error, unit **123/123**; frontend test **208/208**, development build pass hash `acf203d50ae873a150d8`. Dropdown grade dùng template màu và header filter dùng cùng nhãn với form AssessmentSheet; icon sticky là `arrowup`/`arrowdown`; subtitle hiển thị ngày sinh + tuổi, dòng phụ học sinh ngoài danh sách AssessmentSheet cũng có tuổi từ snapshot DOB. Debug solution build bị process API đang chạy giữ DLL; integration runtime không chạy vì Docker không khả dụng; Google live smoke không chạy vì chưa có học sinh test được chỉ định.
