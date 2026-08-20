# ASH-BE-02 — AssessmentSheetService

Owner: `backend`. Phụ thuộc: `ASH-BE-01`. Trạng thái: xem [`../status.md`](../status.md). Log lịch sử: [`../log.md`](../log.md).

Nguồn: [plan mục 3.1, 8](../../../plans/07-ASH-assessment-sheet.md#8-api-dự-kiến), [requirements 09 mục 5, 7, 14](../../../requirements/09-bang-danh-gia-nang-luc.md).

## Mục đích

Xây dựng logic nghiệp vụ lõi của `AssessmentSheet`: CRUD, chọn/sửa plan có filter (đọc prefill từ `AssessmentRecordLatest`), chuyển `Open`/`Done`. Đây là phần Application layer, chưa đụng tới Google Sheets/PDF (thuộc `ASH-BE-03`/`ASH-BE-04`).

## Nội dung cụ thể cần làm

- Tạo `AssessmentSheetService` (`api/src/AdminPortal.Application/AssessmentSheets/`) triển khai:
  - `POST /assessment-sheets`: tạo với `studentId`, `name`, plan ban đầu (danh sách `assessmentId` đã chọn) — snapshot `StudentSnapshot` + `AssessmentSnapshot`, khởi tạo `PlanGrade` của từng `AssessmentRecord` bằng `LatestGrade` đọc từ `AssessmentRecordLatest` hiện có của đúng học sinh (chỉ đọc, tuyệt đối không ghi vào `AssessmentSheetLatest`/`AssessmentRecordLatest`); `FinalGrade`/`FinalNote` để trống.
  - `GET /assessment-sheets`, `GET /assessment-sheets/{id}`: danh sách/chi tiết, hỗ trợ filter chọn plan theo `studentId`, ngưỡng `LatestGrade` (thang `A > B > C > D`, đọc từ `AssessmentRecordLatest`), `GroupLv1/2/3Name`.
  - `PUT /assessment-sheets/{id}`: sửa field chung (`Name`, `ResponsibleTeacher`, `StartDate`, `DueDate`, `Feedback`, `Note`...).
  - `PUT /assessment-sheets/{id}/records`: full replace danh sách `AssessmentRecord` (thêm/bớt mục, sửa `PlanGrade`/`PlanNote`/`FinalGrade`/`FinalNote`) — chặn khi `Status = Done`. Một endpoint chung cho cả hai giai đoạn (kế hoạch lẫn kết quả); UI gọi cùng endpoint này từ hai màn hình khác nhau (`ASH-FE-02` cho Plan, `ASH-FE-03` cho Final).
  - `PUT /assessment-sheets/{id}/status`: đổi `Open`↔`Done`; set/clear `DoneDate`; chuyển `Done`→`Open` là đổi trạng thái đơn giản, mọi vai trò được phép, không cần lý do/log riêng (theo quyết định đã chốt trong requirements 09 mục 15).
- Authorization: `Teacher`/`Admin`/`SuperAdmin` đều thao tác được `AssessmentSheet` của **bất kỳ học sinh nào**, không giới hạn theo nhóm đang phụ trách (khác Attendance).
- Validate: không tạo `AssessmentSheet` cho học sinh `Inactive`; sửa `PlanGrade`/`FinalGrade`/plan trên `AssessmentRecord` **không được ghi ngược** vào `AssessmentSheetLatest`/`AssessmentRecordLatest`/`Assessment` gốc — hai bảng `*Latest` chỉ bị ghi bởi luồng đồng bộ ở `ASH-BE-03`, không service nào khác. Sửa `FinalGrade` không được làm đổi `PlanGrade` và ngược lại (hai cặp field độc lập).
- Đăng ký `AssessmentSheetsController` (`api/src/AdminPortal.Api/Controllers/`), theo đúng quy ước REST/pagination/ProblemDetails đã có (requirements 07).

## Kết quả mong đợi (Definition of Done)

CRUD + filter + chuyển trạng thái hoạt động đúng qua API, có unit test cho các rule chính (`PlanGrade` khởi tạo đúng từ `LatestGrade` nhưng sau đó độc lập, không ghi ngược `AssessmentRecordLatest`; sửa `FinalGrade` không đổi `PlanGrade`; khoá sửa khi `Done`; quyền không giới hạn nhóm). Build API 0 warning/0 error.
