# ASH-BE-00 — Khoá contract DTO/enum/API surface

Owner: `backend`. Phụ thuộc: `ASH-P-01`. Trạng thái: xem [`../status.md`](../status.md). **Đã hoàn thành 2026-08-20** — xem [`../log.md`](../log.md) mục Backend log.

Nguồn: [plan mục 5, 8](../../../plans/07-ASH-assessment-sheet.md#5-thay-đổi-domaindata-cần-chốt-kỹ-thuật).

## Mục đích

`ASH-P-01` chốt quyết định trên giấy; `ASH-BE-00` là nơi quyết định đó trở thành code — bước contract-lock đầu tiên trước khi viết migration/service/API thật. Không code tiếp các bước sau (`ASH-BE-01..05`) nếu bước này chưa xong.

**Lưu ý:** phần lớn field mà bản đầu của plan từng yêu cầu sửa (`SubmissionDate`, `PlanFileLinkPdf`, `ResultFileLinkPdf`, bỏ `ClosedDate` trên `AssessmentSheet`) **đã có sẵn trong entity** — không cần sửa lại. `AssessmentRecord` cũng đã có sẵn 4 field kết quả (`PlanGrade`/`PlanNote`/`FinalGrade`/`FinalNote`) — không cần tách nữa, chỉ cần rà soát khớp DTO. Việc còn thiếu là: (1) entity mới `AssessmentSheetLatest`/`AssessmentRecordLatest`, (2) field mới `AssessmentSheet.AssessmentSheetSpreadsheetId` (lưu id file `[F01]` riêng — kiến trúc mới, xác nhận 2026-08-20), và (3) các quyết định API surface.

## Nội dung cụ thể cần làm

- Rà soát `AssessmentSheet`/`AssessmentRecord` hiện tại, xác nhận đúng field đã có (không sửa gì thêm trừ khi phát hiện sai lệch so với `ASH-P-01`).
- **Thêm field mới `AssessmentSheet.AssessmentSheetSpreadsheetId`** (`string?`) — lưu Drive file id của file `[F01]` riêng (copy từ file mẫu `gen_assessment_sheet`, id `12ClFCOFCfUJJ1i8QstHweNaSdLfY-1MB2eaCuigqWwQ`). Null tới khi lần đầu cần `[F01]`.
- Áp dụng `ASH-DEC-03`: bỏ `ClosedDate` trên `AssessmentSheetLatest` khi định nghĩa entity/configuration.
- Áp dụng `ASH-DEC-05`: khoá index/unique constraint dự kiến cho `AssessmentSheetLatest` (theo `StudentId`) và `AssessmentRecordLatest` (theo `AssessmentSheetLatestId` + mã mục đánh giá) — chuẩn bị cho migration ở `ASH-BE-01`.
- Rà soát toàn bộ API surface dự kiến ở plan mục 8 (`GET/POST /assessment-sheets`, `PUT /{id}`, `PUT /{id}/records`, `PUT /{id}/status`, `POST /{id}/export-to-sheet`, `.../sync-to-sheet`, `.../generate-plan-pdf`, `.../generate-result-pdf`, `.../submit-results`) và khoá tên/shape DTO tương ứng — `AssessmentRecord` có 4 field ghi được: `planGrade`/`planNote`/`finalGrade`/`finalNote`, tất cả cùng đi qua `PUT /{id}/records`.
- Khoá `ASH-DEC-01` (cách sinh PDF — ghi trực tiếp vào sheet `khcn_template`/`KQ_template` sẵn có, không cần bước copy sheet nữa), `ASH-DEC-02` (cách mở quyền `Teacher` cho `sync-assessments`) thành quyết định code-level cụ thể. `ASH-DEC-04` (spreadsheet nguồn cho `[F01]`) đã có giá trị cụ thể — chỉ cần đưa vào config `AssessmentSheetTemplateFileId`, không phải quyết định mở nữa.
- Cấu hình `AssessmentSheetTemplateFileId = "12ClFCOFCfUJJ1i8QstHweNaSdLfY-1MB2eaCuigqWwQ"` trong settings.
- Không đổi định nghĩa policy `PortalManagers` dùng chung; quyền `Teacher` cho `sync-assessments` phải là role-check riêng tại handler (xem `ASH-DEC-02`).

## Kết quả mong đợi (Definition of Done)

Entity/enum/DTO đã đúng tên và field cuối cùng (gồm `AssessmentSheetSpreadsheetId`), build được (chưa cần migration/service hoàn chỉnh). 5 quyết định `ASH-DEC-01..05` đã có phương án code-level rõ ràng, sẵn sàng để `ASH-BE-01` tạo migration dựa trên đó.

**Đã đạt được:** thêm `AssessmentSheet.AssessmentSheetSpreadsheetId` (`string?`); xoá `AssessmentSheetLatest.ClosedDate`; thêm `GoogleSheetsSettings.AssessmentSheetTemplateFileId`/`IGoogleSheetsSettings.AssessmentSheetTemplateFileId` với giá trị thật trong `appsettings.json`. Build `Domain`/`Application` 0 warning/0 error. DTO/Controller/Service (`AssessmentSheetService`, `AssessmentSheetsController`) chưa viết — đúng phạm vi, đó là việc của `ASH-BE-02`; `ASH-DEC-01`/`02` (PDF, quyền `Teacher`) vẫn chỉ là quyết định đã khoá, code thật thuộc `ASH-BE-04`/`ASH-BE-03`.

Log lịch sử của task này nằm ở [`../log.md`](../log.md), không lặp lại trong file này.
