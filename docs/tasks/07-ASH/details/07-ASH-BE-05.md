# ASH-BE-05 — Test, tài liệu, default gate, smoke phần backend

Owner: `backend`. Phụ thuộc: `ASH-BE-04`. Trạng thái: xem [`../status.md`](../status.md). Log lịch sử: [`../log.md`](../log.md).

Nguồn: [plan mục 9, 10](../../../plans/07-ASH-assessment-sheet.md#9-test--smoke--phạm-vi-đã-được-người-dùng-giới-hạn).

## Mục đích

Khép lại phần backend: viết test tương xứng, đồng bộ tài liệu vận hành, chạy gate mặc định của repo, và chạy phần backend của smoke test golden path. **Chỉ smoke test — không xây ma trận test UI/responsive/accessibility hay performance cho epic này** (yêu cầu đã chốt của người dùng).

## Nội dung cụ thể cần làm

- Viết unit/integration test tương xứng với thay đổi (`AssessmentSheetService`, mở rộng `GoogleSheetsService`, quyền `sync-assessments`) — mức độ tương xứng theo thay đổi, không phải ma trận toàn diện riêng.
- Cập nhật `api/README.md` và `api/requests.http` với các endpoint mới dưới `/api/v1/assessment-sheets` và thay đổi ở `/api/v1/google-sheets/sync-assessments`.
- Chạy default verification gate theo `api/AGENTS.md`: `dotnet build AdminPortal.slnx --no-restore`, `dotnet test tests/AdminPortal.UnitTests --no-restore`, `dotnet test tests/AdminPortal.IntegrationTests --no-restore` (Docker/Testcontainers phải chạy được).
- Xác nhận `dotnet-ef migrations has-pending-model-changes` sạch.
- Chạy phần backend của 10 bước smoke test ở [`13-ASH-QA-01.md`](13-ASH-QA-01.md) mà không cần UI (gọi trực tiếp qua `requests.http`/Swagger) để xác nhận API đứng vững trước khi frontend tích hợp.
- Cập nhật `.agents/backend/MEMORY.md` (quyết định kỹ thuật đã áp dụng, đặc biệt `ASH-DEC-01`/`02`/`03`/`04`/`05`, lệnh kiểm tra và kết quả).

## Kết quả mong đợi (Definition of Done)

Build 0 warning/0 error; unit + integration test pass; README/`requests.http`/memory đã đồng bộ; các bước smoke phía backend (1, 2, 3, 5, 7, 9 trong danh sách 10 bước) chạy được qua API trực tiếp, sẵn sàng để frontend nối vào ở `ASH-FE-04`.

## Tiến độ hiện tại (2026-08-20)

Người dùng yêu cầu "làm phần nào làm trước được" trong khi `ASH-BE-03`/`ASH-BE-04` chưa xong — đã hoàn thành phần không phụ thuộc 2 task đó:

- **Đã xong**: tách `AssessmentSheetRules` (static class, theo đúng convention `AuthorizationRules`/`AttendanceRules`/`StudentRules`) gồm `EnsureAssessmentSheetRole`, `EnsureOpen`, `EnsureDistinctIds`, `GradeRank`; viết `AssessmentSheetRulesTests.cs` (15 test case, bao phủ toàn bộ 4 hàm). `dotnet test tests/AdminPortal.UnitTests -c Release` → 55/55 pass (40 cũ + 15 mới).
- **Đã xong**: `api/README.md` (mục "Bảng đánh giá năng lực") và `api/requests.http` (đủ 12 route `assessment-sheets` + `google-sheets/sync-assessments`) đã cập nhật.
- **Đã xong**: `.agents/backend/MEMORY.md` cập nhật trạng thái thật (không dựa vào bản ghi cũ), gồm cả phát hiện 2 bản stub Google-action song song và `AssessmentSheetAlreadyExists` chưa được throw.
- **Chưa làm, chờ điều kiện**:
  - Test cho `AssessmentSheetService` ở mức tích hợp (cần DbContext/EF InMemory hoặc Testcontainers) — service hiện chỉ có test rule thuần, chưa test luồng CRUD/prefill `PlanGrade` end-to-end.
  - `dotnet test tests/AdminPortal.IntegrationTests` **vẫn bị chặn** bởi `NU1903` (`SSH.NET` qua `Testcontainers.PostgreSql 4.8.1`) — người dùng đã chọn hoãn nâng cấp, không tự ý nâng khi chưa được yêu cầu lại.
  - Bước smoke 1/2/3/5/7/9 qua `requests.http` (gọi API thật, cần server + Postgres chạy) chưa thực hiện — hợp lý để làm sau khi `ASH-BE-03` xong phần đọc `AssessmentRecordLatest` (bước 3 phụ thuộc dữ liệu latest có thật), tránh test dựa trên dữ liệu giả.
  - `dotnet-ef migrations has-pending-model-changes` chưa chạy lại xác nhận sau khi entity `AssessmentRecordLatest` ổn định ở migration `20260820105149_ChangeAssessmentRecordLatest`.
