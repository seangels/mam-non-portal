# ASH-BE-06 — Bổ sung AuditLog cho đồng bộ Google Sheet và upload Drive

## Mục đích

Bổ sung audit log rõ ràng cho các hành động tích hợp bên ngoài: đồng bộ dữ liệu từ Google Sheet và upload PDF kế hoạch/kết quả lên Google Drive. Audit phải giúp truy vết ai bấm, lúc nào, tác động tới dữ liệu nào, số lượng bao nhiêu và link/file nào được tạo hoặc cập nhật.

## Tóm tắt ngắn

1. ✅ Bổ sung audit cho `POST /api/v1/google-sheets/sync-assessments`.
2. ✅ Bổ sung/chuẩn hoá audit cho `POST /api/v1/assessment-sheets/{id}/upload-plan-pdf`.
3. ✅ Bổ sung/chuẩn hoá audit cho `POST /api/v1/assessment-sheets/{id}/upload-result-pdf`.
4. ✅ Audit không lưu file bytes, token, credential, secret hoặc raw body nhạy cảm.
5. ✅ Audit upload Drive có fileName, fileSizeBytes, oldLink, newLink, studentId, assessmentSheetId, startDate, dueDate.
6. ✅ Audit sync có spreadsheetId, số dòng đọc/insert/update/delete và actor.
7. ✅ Không đổi database schema nếu dùng `AuditLog` hiện có.
8. ✅ Không đổi Google credentials/auth/IIS/production/deploy.

## Phạm vi

- ✅ Backend trong `api/`.
- ✅ `.agents/backend/MEMORY.md`/shared memory nếu contract/handoff thay đổi.
- ✅ Không chỉnh UI nếu chỉ cần audit backend.
- ✅ Không thêm migration.

## Nội dung cần làm

- ✅ Đồng bộ Google Sheet:
  - ✅ Sau khi `SyncAssessmentsAsync` thành công, thêm audit action `GoogleSheets.AssessmentsSynced`.
  - ✅ Audit context gồm `spreadsheetId`, tổng dòng đọc từ sheet, số student latest mirror, số assessment insert, số record latest insert/update.
  - ✅ Nếu lỗi, chưa audit failure trong v1 vì codebase chưa có pattern audit failure rõ ràng.
- ✅ Upload Drive Kế hoạch:
  - ✅ Giữ action `AssessmentSheet.PlanPdfUploaded` và chuẩn hoá payload hiện có.
  - ✅ `OldValues` có link cũ/trạng thái trước.
  - ✅ `NewValues` có link mới, fileName, fileSizeBytes, studentId, assessmentSheetId, startDate, dueDate.
- ✅ Upload Drive Kết Quả:
  - ✅ Giữ action `AssessmentSheet.ResultPdfUploaded` và chuẩn hoá payload hiện có.
  - ✅ `OldValues` có link cũ/trạng thái trước.
  - ✅ `NewValues` có link mới, fileName, fileSizeBytes, studentId, assessmentSheetId, startDate, dueDate.
- ✅ Test:
  - ⚠️ Sync audit đã compile trong real service nhưng chưa gọi Google Sheet thật trong automated test.
  - ✅ Integration test kiểm tra audit row cho upload plan/result chứa fileName/fileSize/link.
  - ✅ Không assert/lưu dữ liệu nhạy cảm/raw bytes trong audit.

## Kiểm thử mong đợi

- ✅ `dotnet build api/AdminPortal.slnx -c Release --no-restore`
- ✅ `dotnet test api/tests/AdminPortal.UnitTests -c Release --no-restore`
- ✅ `dotnet test api/tests/AdminPortal.IntegrationTests -c Release --no-restore`
- ⚠️ Không cần gọi Google thật nếu fake integration đủ xác minh audit path.
