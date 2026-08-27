# ASH-CL-01 — Cleanup luồng Google Sheet riêng của AssessmentSheet

## Tóm tắt ngắn

1. ✅ Gỡ luồng tạo/copy Google Sheet riêng `[F01]` cho từng `AssessmentSheet`.
2. ✅ Gỡ các endpoint/API client/docs cho `export-to-sheet`, `sync-to-sheet`, `generate-plan-pdf`, `generate-result-pdf`.
3. ✅ Giữ `AssessmentSheetSpreadsheetId` là cột DB legacy-only, không expose API/UI v1 và không ghi mới.
4. ✅ Giữ các luồng Google còn dùng: `sync-assessments`, `submit-results`, `upload-plan-pdf`, `upload-result-pdf`.
5. ✅ Cập nhật docs/memory để agent sau không triển khai nhầm flow cũ.

## Mục đích

Hiện tại AssessmentSheet không còn dùng cơ chế tạo một Google Sheet riêng cho từng bảng đánh giá. PDF kế hoạch/kết quả được render từ UI bằng `html2pdf.js` rồi upload Drive. Google Sheet chỉ còn dùng cho dữ liệu nguồn `[F0]`: nạp danh mục/latest và ghi kết quả cuối về `[F0.ĐG]`.

## Nội dung cần làm

- ✅ Backend:
  - Gỡ controller/service/interface method cho `export-to-sheet`, `sync-to-sheet`, `generate-plan-pdf`, `generate-result-pdf`.
  - Gỡ helper Google Drive/Sheets chỉ phục vụ `[F01]`.
  - Gỡ settings template/gid chỉ phục vụ `[F01]`.
  - Không tạo migration drop cột `AssessmentSheetSpreadsheetId` trong lượt này để tránh thay đổi schema/data ngoài phạm vi cleanup.
- ✅ Frontend:
  - Không dùng `assessmentSheetSpreadsheetId` trong model/UI contract.
  - Không gọi endpoint legacy đã gỡ.
- ✅ Docs/handoff:
  - Cập nhật `requirements`, `plans`, `api/README.md`, `requests.http`, task dashboard và memory.
  - Các task/log cũ nếu giữ lại phải được hiểu là lịch sử; tài liệu hiện hành phải ghi rõ `[F01]` là legacy/removed.
- ✅ Verification:
  - Backend build + unit/integration test.
  - Frontend development build + `test:ci`.
  - Không chạy Google thật, không chạy production/IIS.

## DoD

- ✅ Search source không còn route/method/config legacy ngoài cột/migration DB cũ.
- ✅ API/UI build/test pass theo phạm vi thay đổi.
- ✅ Docs/memory thể hiện rõ `AssessmentSheetSpreadsheetId` là legacy DB-only.
- ✅ Có commit local riêng cho cleanup.

## Verification 2026-08-27

- ✅ `dotnet build api/AdminPortal.slnx -c Release --no-restore` — pass 0 warning/0 error.
- ✅ `dotnet test api/tests/AdminPortal.UnitTests -c Release --no-restore` — pass 85/85.
- ✅ `dotnet test api/tests/AdminPortal.IntegrationTests -c Release --no-restore` — pass 30/30 ngoài sandbox để Testcontainers truy cập Docker named pipe.
- ✅ `npm --prefix ui run test:ci` — pass 118/118 ngoài sandbox; trong sandbox vẫn fail `EPERM lstat C:\Users\sangn`.
- ✅ `npm --prefix ui run build -- --configuration development` — pass, hash `4c19eda4cb0e62265a4c`, chỉ warning CommonJS/DevExtreme/html2pdf quen thuộc.
- ✅ Không gọi Google API thật và không chạy production/IIS.
