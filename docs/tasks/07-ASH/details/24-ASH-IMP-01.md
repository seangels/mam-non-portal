# ASH-IMP-01 — Import AssessmentSheet từ Excel

## Tóm tắt ngắn

1. ✅ Thêm import Excel cho `AssessmentSheet` và `AssessmentRecord` từ file mẫu `docs/samples/import_khcn.xlsx`.
2. ✅ Backend đọc `.xlsx` bằng thư viện `ExcelDataReader`; không tự parse XML Excel thủ công.
3. ✅ Format v1 dùng sheet đầu tiên, hàng 1 là header: `planGrade`, `planNote`, `assessmentCode`, `studentCode`, `studentName`, `startDate`, `dueDate`.
4. ✅ Mỗi dòng dữ liệu là một `AssessmentRecord`; gom thành một `AssessmentSheet` theo `studentCode + startDate + dueDate`.
5. ✅ Tra cứu học sinh bằng `studentCode`, tra cứu mục đánh giá bằng `assessmentCode`.
6. ✅ `planGrade`/`planNote` trong file được lưu vào `PlanGrade`/`PlanNote`; `FinalGrade`/`FinalNote` vẫn để trống.
7. ✅ Dòng trống bỏ qua; dòng trùng cùng `studentCode + startDate + dueDate + assessmentCode` giữ dòng đầu và trả warning.
8. ✅ Nếu có lỗi bắt buộc như thiếu header, thiếu mã, sai ngày, không tìm thấy học sinh/assessment, hoặc sheet hiện có đã `Done`, không ghi DB.
9. ✅ Nếu đã có sheet cùng học sinh + khoảng ngày và chưa `Done`, import sẽ thay toàn bộ records của sheet đó bằng dữ liệu trong file; nếu chưa có thì tạo sheet mới trạng thái `Open`.
10. ✅ UI thêm nút import tiếng Việt trên màn danh sách AssessmentSheet, cho chọn `.xlsx`, gọi API preview và mở popup validate trước khi ghi DB.
11. ✅ Popup preview hiển thị `dxDataGrid` tất cả dòng import đã parse/validate, lỗi/warning từng dòng, summary tổng hợp và nút submit chỉ bật khi file hợp lệ.
12. ✅ `dxDataGrid` trong popup dùng dữ liệu local toàn bộ từ preview response để filter/search/sort client-side; không gọi server paging/filter trong popup.
13. ✅ V1 không gán giáo viên phụ trách từ file vì file mẫu không có `teacherCode`; sheet mới để `ResponsibleTeacherId = null`.

## Contract đề xuất

- `POST /api/v1/assessment-sheets/import-excel/preview`
  - `multipart/form-data`, field `file`.
  - Chấp nhận `.xlsx`, tối đa 10 MB.
  - Chỉ parse + validate, không ghi DB.
  - Quyền: `Teacher`, `Admin`, `SuperAdmin` như các action `AssessmentSheet` hiện tại.
  - Response:
    - `canImport`
    - `validRowCount`
    - `errorCount`
    - `warningCount`
    - `skippedDuplicateRowCount`
    - `rows[]`: toàn bộ dòng non-empty đã parse/validate, gồm `rowNumber`, `assessmentCode`, `studentCode`, `studentName`, `startDate`, `dueDate`, `action`, `errors[]`, `warnings[]`; dòng trống cuối file bỏ qua.
    - `sheets[]`: nhóm sheet dự kiến tạo/cập nhật kèm `studentCode`, `studentName`, `startDate`, `dueDate`, `recordCount`, `action`.
- `POST /api/v1/assessment-sheets/import-excel`
  - `multipart/form-data`, field `file`.
  - Chấp nhận `.xlsx`, tối đa 10 MB.
  - Gọi sau khi user xem popup preview và bấm xác nhận import.
  - Backend vẫn validate lại server-side trước khi ghi DB; không tin tuyệt đối vào preview cũ.
  - Quyền: `Teacher`, `Admin`, `SuperAdmin` như các action `AssessmentSheet` hiện tại.
  - Response:
    - `createdSheetCount`
    - `updatedSheetCount`
    - `importedRecordCount`
    - `skippedDuplicateRowCount`
    - `warnings[]`
    - `sheets[]`: danh sách sheet được tạo/cập nhật kèm `studentCode`, `studentName`, `startDate`, `dueDate`, `recordCount`, `action`.

## Mapping Excel

- `planGrade`: bắt buộc theo header, có thể để trống từng dòng; nếu có giá trị thì phải match nhãn grade hiện có (`Đạt +`, `Chưa đạt -`, `Hỗ trợ +`, `Hỗ trợ -`) và lưu vào `PlanGrade`.
- `planNote`: bắt buộc theo header, có thể để trống từng dòng; lưu vào `PlanNote`.
- `assessmentCode`: bắt buộc, match theo `Assessment.Code` sau khi trim.
- `studentCode`: bắt buộc, match theo `Student.StudentCode` sau khi trim.
- `studentName`: dùng để warning nếu khác tên học sinh trong DB, không dùng làm khóa chính.
- `startDate`, `dueDate`: bắt buộc, hỗ trợ Excel serial number và text date parse được theo format thông dụng.

## DoD

- ✅ Backend có parser `.xlsx` bằng `ExcelDataReader`, không cần gọi Google, không dùng luồng `[F01]`.
- ✅ Backend có endpoint preview validate file không ghi DB.
- ✅ Backend import atomic: có lỗi nghiêm trọng thì không tạo/cập nhật nửa chừng.
- ✅ Backend tạo/cập nhật sheet và records đúng, import `PlanGrade`/`PlanNote` từ file và không tự fill `FinalGrade`/`FinalNote`.
- ✅ Frontend có nút upload `.xlsx`, popup `dxDataGrid` preview/validate bằng toàn bộ dữ liệu local, loading/disabled state và thông báo tiếng Việt.
- ✅ Unit/integration test backend và unit/build frontend pass theo phạm vi.
- ✅ Không chạy production/IIS/deploy.
