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
