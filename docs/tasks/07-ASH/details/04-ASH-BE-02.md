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

## Hoàn thành (2026-08-20)

Đã tách toàn bộ logic thuần (không I/O) khỏi `AssessmentSheetService` vào `AssessmentSheetRules.cs` (static class, theo convention `AuthorizationRules`/`AttendanceRules`/`StudentRules`), gồm `EnsureAssessmentSheetRole`, `EnsureOpen`, `EnsureDistinctIds`, `GradeRank`, `BuildRecords` (khởi tạo `AssessmentRecord` lúc tạo, prefill `PlanGrade` từ `LatestGrade`), `BuildReplacementRecord` (map request → entity lúc `PUT .../records`, giữ `Plan*`/`Final*` độc lập). `AssessmentSheetRulesTests.cs` có 19 test case bao phủ đúng các rule DoD yêu cầu:

- `BuildRecordsPrefillsPlanGradeFromLatestGradesByCode`/`BuildRecordsAlwaysLeavesFinalGradeAndFinalNoteEmpty` — `PlanGrade` khởi tạo từ `LatestGrade`, `FinalGrade` luôn trống lúc tạo.
- `BuildReplacementRecordKeepsPlanAndFinalGradeIndependent`/`BuildReplacementRecordTrimsNotesAndTreatsBlankAsNull` — sửa `FinalGrade` không đổi `PlanGrade` và ngược lại.
- `DoneSheetRejectsEnsureOpen`/`OpenSheetPassesEnsureOpen` — khoá sửa khi `Done`.
- `EveryStaffRoleCanAccessAssessmentSheets`/`UnknownOrUnassignedRoleIsForbidden` — quyền mở cho `Teacher`/`Admin`/`SuperAdmin`, không có tham số nhóm nào trong rule (đúng "không giới hạn nhóm").

"Không ghi ngược `AssessmentRecordLatest`" xác nhận bằng kiểm tra cấu trúc mã: `BuildRecords`/`BuildReplacementRecord` là hàm thuần (không nhận `IApplicationDbContext`, không thể ghi bất kỳ bảng nào); `AssessmentSheetService` chỉ đọc `AssessmentRecordLatests`/`AssessmentSheetLatests` qua `.AsNoTracking()` trong `LoadLatestGradesAsync`, không có lệnh `Add`/`Update`/`Remove` nào nhắm tới 2 `DbSet` đó trong toàn file.

**Đã thử và loại bỏ:** test tích hợp bằng EF Core InMemory provider (gọi thẳng `AssessmentSheetService.CreateAsync`/`ReplaceRecordsAsync` qua `AdminPortalDbContext` in-memory) — phát hiện InMemory provider không tương thích với cách codebase map `AssessmentSnapshot`/`StudentSnapshot` qua `ComplexProperty(...).ToJson()` (jsonb): bất kỳ truy vấn nào `OrderBy`/`ThenBy`/`Select`/`ToDictionaryAsync` trên sub-property của complex type JSON đều ném `KeyNotFoundException` trong bộ dịch shaper của InMemory, kể cả `BuildDetailAsync` cơ bản nhất (`.ThenBy(x => x.AssessmentSnapshot.Code)`). Đây là giới hạn thật của EF Core InMemory provider với JSON complex type, không phải lỗi test. Đã gỡ bỏ file test và package `Microsoft.EntityFrameworkCore.InMemory` khỏi `AdminPortal.UnitTests.csproj`. Kết luận: xác minh round-trip CRUD qua DB thật chỉ khả thi qua `AdminPortal.IntegrationTests` (Postgres thật, đang bị chặn bởi `NU1903`) hoặc smoke test thủ công (`ASH-QA-01`).

Verification: `dotnet build src/AdminPortal.Api/AdminPortal.Api.csproj -c Release` → 0 Warning/0 Error; `dotnet test tests/AdminPortal.UnitTests -c Release` → 59/59 pass.
