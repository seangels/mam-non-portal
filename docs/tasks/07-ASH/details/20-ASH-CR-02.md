# ASH-CR-02 — Nút Cập nhật Kết Quả vào Google Sheet ResultSource

## Mục đích

Thêm luồng `Cập nhật Kết Quả` trên màn edit AssessmentSheet để ghi `FinalGrade` và `FinalNote` vào Google Sheet nguồn ResultSource. Luồng này chỉ ghi những ô có thay đổi so với giá trị hiện tại trong Google Sheet và luôn tạo audit log chi tiết cho từng cell được ghi.

## Tóm tắt ngắn

1. ✅ Thêm nút `Cập nhật Kết Quả` trên màn edit AssessmentSheet.
2. ✅ Nút gọi endpoint hiện có `POST /api/v1/assessment-sheets/{id}/submit-results`.
3. ✅ Backend đọc giá trị hiện tại trên ResultSource trước khi ghi.
4. ✅ Chỉ ghi cell khi giá trị mới khác giá trị hiện tại.
5. ✅ Ghi `FinalGrade` theo nhãn tiếng Việt hiện có (`Đạt +`, `Chưa đạt -`, `Hỗ trợ +`, `Hỗ trợ -`).
6. ✅ Ghi `FinalNote` vào cột kế bên phải cột kết quả của học sinh; tại hàng định vị mã học sinh, cột note này để trống.
7. ✅ Mỗi cell được ghi phải có audit log riêng, gồm vị trí ô, giá trị hiện tại, giá trị mới, studentCode, studentName, studentId, assessmentSheetId, startDate, dueDate, FinalGrade, FinalNote, thời gian, actor và thông tin assessment cần thiết.
8. ✅ Sau khi ghi thành công, backend cập nhật `SubmissionDate`, `UpdatedAt`, `UpdatedByUserId`.
9. ✅ Không đổi database schema nếu dùng `AuditLog` hiện có.
10. ✅ Không đổi auth, hash routing, IIS hay production/deploy.

## Phạm vi

- ✅ Backend trong `api/`: cải tiến `SubmitResultsAsync`/`WriteFinalGradesToSourceSheetAsync` hoặc helper tương đương.
- ✅ Frontend trong `ui/`: thêm nút tiếng Việt và trạng thái loading/confirm/notify.
- ✅ Docs/README/request sample/memory liên quan.
- ✅ Không tạo migration nếu `AuditLog.OldValues/NewValues` hiện có đủ lưu JSON detail.
- ✅ Không chạy production/IIS/deploy.

## Nội dung cần làm

- ✅ API contract:
  - ✅ Giữ endpoint `POST /api/v1/assessment-sheets/{id}/submit-results` để tránh đổi contract lớn.
  - ✅ Response vẫn là `AssessmentSheetDetail`.
  - ✅ Lỗi Google vẫn map về `AssessmentSheetGoogleOperationFailed`.
- ✅ Backend ghi ResultSource:
  - ✅ Dò cột học sinh theo `studentCode` từ config `ResultSource_*` hiện có.
  - ✅ Dò dòng mục đánh giá theo `assessment.code`.
  - ✅ Đọc giá trị hiện tại của cell trước khi ghi.
  - ✅ Chỉ thêm vào batch update nếu giá trị mới khác giá trị hiện tại.
  - ✅ Nếu không tìm thấy studentCode hoặc assessment code thì fail trước khi ghi phần nào.
  - ✅ Nếu không có cell nào thay đổi, không gọi batch update nhưng vẫn cập nhật `SubmissionDate`.
- ✅ Backend ghi `FinalNote`:
  - ✅ Tìm cột kết quả theo `studentCode` ở hàng định vị mã học sinh.
  - ✅ Cột `FinalNote` là cột ngay bên phải cột kết quả của học sinh.
  - ✅ Tại hàng định vị mã học sinh, cột `FinalNote` để trống; nếu kiểm tra thấy không trống thì fail trước khi ghi để tránh sai mapping.
  - ✅ Đọc giá trị note hiện tại trước khi ghi và chỉ ghi khi có thay đổi.
- ✅ Backend audit từng cell:
  - ✅ Action: `AssessmentSheet.ResultSourceCellUpdated`.
  - ✅ `EntityType = AssessmentSheet`, `EntityId = assessmentSheetId`.
  - ✅ `OldValues` chứa giá trị hiện tại của ô.
  - ✅ `NewValues` chứa giá trị mới và đầy đủ context:
    - ✅ spreadsheetId, sheetName, cell/range, row, column, assessmentCode, assessmentName.
    - ✅ studentCode, studentName, studentId.
    - ✅ assessmentSheetId, startDate, dueDate.
    - ✅ finalGrade, finalGradeLabel, finalNote.
    - ✅ actorUserId/ipAddress/createdAt đã có ở `AuditLog`.
- ✅ Frontend:
  - ✅ Thêm nút `Cập nhật Kết Quả` cạnh nhóm action records.
  - ✅ Disable khi create mới, đang loading/saving/mutate hoặc không có record.
  - ✅ Nếu form dirty, hỏi xác nhận: phải lưu trước nếu muốn ghi dữ liệu mới nhất.
  - ✅ Khi user xác nhận, gọi `submit-results`.
  - ✅ Hiển thị loading/notify tiếng Việt.
  - ✅ Sau khi thành công, apply lại detail trả về.

## Kiểm thử mong đợi

- ✅ Backend integration test fake Google service xác nhận endpoint gọi đúng và có audit log cell khi có thay đổi.
- ✅ Frontend unit test cho enable/disable và gọi service `submitResults`.
- ✅ `dotnet build api/AdminPortal.slnx -c Release --no-restore`
- ✅ `dotnet test api/tests/AdminPortal.UnitTests -c Release --no-restore`
- ✅ `dotnet test api/tests/AdminPortal.IntegrationTests -c Release --no-restore`
- ✅ `npm --prefix ui run test:ci`
- ✅ `npm --prefix ui run build -- --configuration development`
- ⚠️ Smoke Google Sheet thật chỉ chạy khi được phép tương tác Google thật; nếu không, ghi rõ chưa chạy.
