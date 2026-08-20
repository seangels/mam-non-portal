# Lịch sử thực hiện — ASH: Bảng đánh giá năng lực

File trạng thái: [`status.md`](status.md). Chi tiết từng task (Mục đích/Nội dung/DoD): [`details/`](details/). File log này là nơi ghi lịch sử chung, theo trình tự thời gian thật, cho toàn bộ task của epic — không tách log theo từng file chi tiết.

## Quy ước ghi log

- Mỗi mục là một dòng bullet, gắn đúng mã task (`ASH-P-01`, `ASH-BE-00`…): `- <Mã> (YYYY-MM-DD): <việc đã làm, kết quả, lệnh kiểm tra và số liệu pass/fail>`.
- Ghi theo thời điểm thực hiện thật (không phải ngày lập plan); nối thêm bullet mới ở cuối section tương ứng, không sửa/xoá log cũ — nếu một quyết định trước đó bị đảo ngược, thêm bullet mới ghi rõ thay đổi thay vì chỉnh sửa bullet cũ.
- Khi một task chuyển trạng thái trong `status.md`, phải có ít nhất một bullet log tương ứng ở đây cùng ngày.
- Không ghi secret, connection string có credential, JWT signing key hay token vào file này (theo `AGENTS.md`).

## Planning log

- `ASH-P-01` (2026-08-20): thực hiện đối chiếu source theo checklist. Phát hiện `AssessmentRecord.cs` đã đổi so với lần đối chiếu trước trong cùng ngày: không còn field `Grade` đơn, thay bằng 4 field `PlanGrade`/`PlanNote`/`FinalGrade`/`FinalNote` (khôi phục đúng thiết kế "hai cặp tách biệt" ban đầu). `AssessmentSheet.cs`, `AssessmentSheetLatest.cs`, `AssessmentRecordLatest.cs` không đổi thêm so với lần đối chiếu trước (`AssessmentSheetSpreadsheetId` vẫn chưa có trên `AssessmentSheet` — đúng như dự kiến, đây là việc của `ASH-BE-00`). Đã cập nhật lại toàn bộ requirements 09, sơ đồ luồng dữ liệu (Mermaid + SVG re-render), plan 07-ASH và 13 file `details/`/`status.md` cho khớp field mới. 5 quyết định `ASH-DEC-01..05` giữ nguyên đề xuất mặc định trong plan mục 13 (không có phản hồi khác từ người dùng ngoài các input Google Sheet đã cung cấp) — coi như đã khoá cho `ASH-BE-00` triển khai. `ASH-P-01` hoàn tất.

## Backend log

- `ASH-BE-00` (2026-08-20): thêm field `AssessmentSheet.AssessmentSheetSpreadsheetId` (`string?`); xoá `AssessmentSheetLatest.ClosedDate` (`ASH-DEC-03`); thêm `AssessmentSheetTemplateFileId` vào `IGoogleSheetsSettings`/`GoogleSheetsSettings`/`appsettings.json` với giá trị thật `12ClFCOFCfUJJ1i8QstHweNaSdLfY-1MB2eaCuigqWwQ` (`ASH-DEC-04`). Build `AdminPortal.Domain`/`AdminPortal.Application`: 0 warning/0 error. `ASH-BE-00` hoàn tất.
- `ASH-BE-01` (2026-08-20): thêm `DbSet<AssessmentSheet>`/`AssessmentRecord`/`AssessmentSheetLatest`/`AssessmentRecordLatest` vào `IApplicationDbContext` và `AdminPortalDbContext`; tạo 4 file `IEntityTypeConfiguration` mới (`AssessmentSheetConfiguration`, `AssessmentRecordConfiguration`, `AssessmentSheetLatestConfiguration`, `AssessmentRecordLatestConfiguration`) dùng `ComplexProperty(...).ToJson()` cho `StudentSnapshot`/`AssessmentSnapshot` (map jsonb), `HasConversion<string>()` cho enum, `DeleteBehavior.Restrict` cho mọi FK (khớp quy ước hiện có, không cascade).
  - **Phát hiện khi chạy `dotnet-ef migrations add`:** EF Core từ chối `HasIndex(x => new { x.AssessmentSheetLatestId, x.AssessmentSnapshot.Code })` — lỗi "not a valid member access expression" vì không hỗ trợ index trên sub-property của complex type đã map JSON. Xử lý: thêm field scalar `AssessmentRecordLatest.AssessmentCode` (`string`, required, `maxLength 50`) làm cột index riêng, độc lập với `AssessmentSnapshot` jsonb. Đã cập nhật requirements 09 và plan 07-ASH mục 5/6.5/13 để ghi nhận field mới này và nhắc `ASH-BE-03` phải set nó khi upsert.
  - Sinh migration `20260820094414_AddAssessmentSheetManagement` (tạo 4 bảng `assessment_sheets`, `assessment_records`, `assessment_sheet_latests`, `assessment_record_latests`, đúng unique index theo `ASH-DEC-05`, không có cột `closed_date`).
  - Migration ban đầu bị chặn bởi phân tích tĩnh `CA1861` (2 lệnh `CreateIndex` composite dùng `new[] { ... }` inline, `TreatWarningsAsErrors=true`) — đã sửa bằng cách hoist thành `private static readonly string[]` field ngay trong file migration (không đổi hành vi SQL sinh ra).
  - Kết quả kiểm tra: `dotnet build` cho `Domain`/`Application`/`Infrastructure`/`Api`: 0 warning/0 error. `dotnet-ef migrations has-pending-model-changes`: "No changes have been made to the model since the last migration." `dotnet test tests/AdminPortal.UnitTests`: 40/40 pass (test cũ, chưa có test riêng cho entity mới — thuộc `ASH-BE-02`/`05`). `dotnet build` toàn `AdminPortal.slnx` (gồm `IntegrationTests`) **bị chặn** bởi lỗi restore NuGet tiền tồn tại không liên quan (`NU1903`: advisory bảo mật trên gói `SSH.NET` 2024.2.0, transitive qua Testcontainers) — không phải do thay đổi của epic này; chưa xử lý, cần agent khác/quyết định riêng về nâng cấp dependency.
  - `ASH-BE-01` hoàn tất.
- `ASH-BE-02`/`ASH-BE-03`/`ASH-BE-04` partial (2026-08-20): tạo `AssessmentSheets` application surface (`IAssessmentSheetService`, DTO, `AssessmentSheetService`) và `AssessmentSheetsController` với list/detail/plan-candidates/create/update/replace-records/status + các route action `export-to-sheet`, `sync-to-sheet`, `generate-plan-pdf`, `generate-result-pdf`, `submit-results`. BE-02 đã có logic CRUD lõi: mọi role `Teacher`/`Admin`/`SuperAdmin` được thao tác không giới hạn nhóm; create snapshot Student/Assessment, chặn Student inactive, prefill `PlanGrade` từ `AssessmentRecordLatest.LatestGrade`, để trống `Final*`; replace records giữ `Plan*` và `Final*` độc lập; `Done` khoá update/records và `Open` mở lại. Đăng ký DI `IAssessmentSheetService`.
  - BE-03 đã làm phần có thể làm trước: mở rộng `IGoogleSheetsService` bằng các method action cho F01/F0.ĐG/PDF; bỏ policy `PortalManagers` khỏi `POST /google-sheets/sync-assessments` và chuyển sang role-check `Teacher`/`Admin`/`SuperAdmin` trong handler để không mở nhầm policy quản trị khác. Các action F01/F0.ĐG hiện trả `ProblemCodes.AssessmentSheetGoogleMappingBlocked` với thông điệp blocker vì chưa có mapping cột/vị trí sheet `data`, dò ô F0.ĐG và Drive copy implementation.
  - BE-04 đã làm phần interface/endpoint trước: route sinh F02/F03 tồn tại và trả blocker rõ (`AssessmentSheetGoogleMappingBlocked`) vì chưa có mapping template `khcn_template`/`KQ_template`, export PDF và cấu hình nơi lưu file.
  - Verification: `dotnet build api/src/AdminPortal.Api/AdminPortal.Api.csproj -c Release --no-restore` pass 0 warning/0 error; `dotnet test api/tests/AdminPortal.UnitTests/AdminPortal.UnitTests.csproj --no-restore` pass 40/40. `dotnet build api/AdminPortal.slnx --no-restore` và integration test vẫn bị chặn bởi `NU1903` package `SSH.NET` 2024.2.0 (transitive Testcontainers) như log BE-01; Debug build API riêng còn bị khóa DLL bởi process dotnet đang chạy nên dùng Release build để xác minh compile.

## Frontend log

_Chưa có hoạt động._

## QA / smoke test log

_Chưa có hoạt động. Khi chạy `ASH-QA-01`, ghi kết quả từng bước trong 10 bước (plan mục 9 / `details/ASH-QA-01.md`) theo dạng: `- ASH-QA-01 bước N (YYYY-MM-DD): pass|fail — mô tả evidence`._

## Quyết định/điều chỉnh phạm vi

- (2026-08-20): Người dùng xác nhận trực tiếp áp dụng `ASH-DEC-01` (sinh PDF: ghi trực tiếp vào sheet có sẵn trong `[F01]`, export theo `gid`), `ASH-DEC-02` (mở quyền `Teacher` cho `sync-assessments` bằng role-check tại handler, không đổi policy `PortalManagers`), và `ASH-DEC-05` (khoá upsert `AssessmentSheetLatest` theo `StudentId`, `AssessmentRecordLatest` theo `AssessmentSheetLatestId` + mã mục đánh giá) đúng theo đề xuất mặc định trong plan mục 13. Đã cập nhật `status.md` (tick Status, đổi cột "Áp dụng theo" → "Đã chốt") và plan mục 13 tương ứng. Cả 3 quyết định chưa được code — chờ `ASH-BE-00`/`ASH-BE-01`/`ASH-BE-03` triển khai.
