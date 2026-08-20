# ASH-FE-00 — Khoá contract cùng backend

Owner: `frontend`. Phụ thuộc: `ASH-BE-00`. Trạng thái: xem [`../status.md`](../status.md). Log lịch sử: [`../log.md`](../log.md).

Nguồn: [plan mục 8](../../../plans/07-ASH-assessment-sheet.md#8-api-dự-kiến).

## Mục đích

Đảm bảo frontend không tự đoán DTO/API shape trước khi backend khoá contract ở `ASH-BE-00`. Đây là bước đối chiếu, không phải bước dựng UI.

## Nội dung cụ thể cần làm

- Đọc lại contract đã khoá ở `ASH-BE-00`: field cuối cùng của `AssessmentSheet`/`AssessmentRecord` — `AssessmentRecord` có **4 field ghi được**: `planGrade`/`planNote` (giai đoạn kế hoạch) và `finalGrade`/`finalNote` (kết quả, độc lập) — **không phải một field `grade` đơn**; `AssessmentSheet` có `SubmissionDate`, `AssessmentSheetSpreadsheetId`, `PlanFileLinkPdf`, `ResultFileLinkPdf`. Field `latestGrade` chỉ xuất hiện ở phía đọc (từ `AssessmentRecordLatest`, dùng cho filter chọn plan và prefill `planGrade`), toàn bộ endpoint ở plan mục 8 (`GET/POST /assessment-sheets`, `PUT /{id}`, `PUT /{id}/records`, `PUT /{id}/status`, `POST /{id}/export-to-sheet`, `.../sync-to-sheet`, `.../generate-plan-pdf`, `.../generate-result-pdf`, `.../submit-results`, `POST /google-sheets/sync-assessments`).
- Nếu backend chưa có OpenAPI/Swagger cập nhật cho các endpoint này, phối hợp trực tiếp với backend agent để lấy đúng request/response shape thay vì tự suy đoán.
- Chuẩn bị model/interface TypeScript tương ứng (chưa cần UI) trong thư mục service hiện có của `ui/src/app`.
- Xác nhận quyền hiển thị menu/route: `Teacher`/`Admin`/`SuperAdmin` đều thấy được trang `AssessmentSheet`, không giới hạn theo nhóm.

## Kết quả mong đợi (Definition of Done)

Model/interface TypeScript khớp 100% với contract backend đã khoá; không có field/endpoint nào frontend phải đoán khi bắt đầu `ASH-FE-01`.
