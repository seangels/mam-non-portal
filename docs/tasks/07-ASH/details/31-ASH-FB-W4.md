# ASH-FB-W4 — Feedback batch, Đợt 4 (G7a + G7b + G7c)

Thực thi Đợt 4 của [`29-ASH-FB-01.md`](29-ASH-FB-01.md) — màn **danh sách** bảng đánh giá. Thứ tự người dùng: sau Đợt 1 làm Đợt 4 trước.

## Tóm tắt ngắn

1. ✅ **G7a — Lọc theo giáo viên phụ trách.** BE: `AssessmentSheetListQuery` + `Guid? ResponsibleTeacherId`; `ListAsync` thêm `.Where(x => x.ResponsibleTeacherId == …)`. FE: dropdown `dx-select-box` (CustomStore từ `TeachersService.list`, status Active) trong panel lọc; role `Teacher` **không** bị khóa. Reset xóa filter này.
2. ✅ **G7b — Datagrid select multi + paging remote server-side.** Paging/sort/filter server-side đã có sẵn (CustomStore). Thêm `<dxo-selection mode="multiple" selectAllMode="page" showCheckBoxesMode="always">` — `selectAllMode="page"` = **không** giữ selection qua các trang. `onSelectionChanged` giữ `selectedSheets`; đổi filter/reset là `clearSelection()`.
3. ✅ **G7c — `Bulk Action` (dropdown button).** 4 action trên các dòng đã chọn: `Tải PDF khcn`, `Tải ảnh khcn`, `Tải PDF KQ`, `Tải ảnh KQ`. **Tải + zip toàn bộ ở backend** (theo yêu cầu người dùng, chốt 2026-09-01 — đảo lựa chọn "zip ở client" ban đầu):
   - `POST /api/v1/assessment-sheets/pdf-archive` `{ ids: Guid[], kind: 'Plan'|'Result', format: 'Pdf'|'Images' }` → `application/zip`.
   - Tên file trong zip = **tên gốc trên Google Drive**, **phẳng ở gốc zip** (không thư mục riêng từng bảng); đụng tên → ` (2)`.
   - `format=Pdf`: zip các PDF gốc tải từ Google Drive.
   - `format=Images`: render từng trang PDF thành PNG bằng **PDFium/SkiaSharp (`PDFtoImage`)** phía server; tên `<tên gốc không đuôi> - trang NNN.png`.
   - Dòng không có link / tải lỗi / render lỗi → **bỏ qua**, ghi vào `_bo-qua.txt` trong zip.
   - FE: `dx-drop-down-button` "Bulk Action (N)"; `onBulkAction` gọi `assessmentSheets.downloadPdfArchive` → blob zip → tải xuống với tên `<tên action> <timestamp>.zip`.

## Quyết định & thay đổi so với `29-ASH-FB-01.md`

- **G7c "client-side zip" → "backend zip toàn bộ"** (người dùng chốt 2026-09-01): sau khi chọn "ảnh render ở backend" (PDFium), người dùng yêu cầu luôn cả bước tải + zip cũng ở backend. Bỏ ý định dùng `jszip`/`pdf.js` ở frontend. FE chỉ POST danh sách id + nhận blob zip + tải xuống.
- **Endpoint 1 file (`GET {id}/pdf-file`) đã bỏ**, thay bằng 1 endpoint gộp `POST /pdf-archive`.
- Dependency mới: backend `PDFtoImage` `5.4.0` (`AdminPortal.Application.csproj`) — kéo `SkiaSharp` + `bblanchon.PDFium.*` native theo RID (IIS Windows dùng `*.Win32`). CA1416 tắt cục bộ tại 2 call site render (lib hỗ trợ Windows/Linux/macOS).

## Phạm vi file

- **Backend `api/`:**
  - `src/AdminPortal.Application/AdminPortal.Application.csproj` — `+ PDFtoImage 5.4.0`
  - `src/AdminPortal.Application/AssessmentSheets/AssessmentSheetModels.cs` — `+ ResponsibleTeacherId`; `+ AssessmentSheetPdfKind` / `AssessmentSheetPdfArchiveFormat` / `AssessmentSheetPdfArchiveRequest` / `AssessmentSheetPdfArchiveResult`
  - `src/AdminPortal.Application/AssessmentSheets/AssessmentSheetService.cs` — `ListAsync` filter; `BuildPdfArchiveAsync` + `UniqueName`
  - `src/AdminPortal.Application/AssessmentSheets/AssessmentSheetPdfArchive.cs` (mới) — `BuildZip` + `PdfToPngPages`
  - `src/AdminPortal.Application/AssessmentSheets/IAssessmentSheetService.cs` — `+ BuildPdfArchiveAsync`
  - `src/AdminPortal.Application/GoogleSheets/IGoogleSheetsService.cs` + `GoogleSheetsService.cs` — `+ DownloadAssessmentSheetPdfAsync` (Drive `Files.Get(...).DownloadAsync`)
  - `src/AdminPortal.Application/Common/ProblemCodes.cs` — `+ AssessmentSheetPdfNotAvailable` (khai báo; hiện chưa throw nơi nào — endpoint archive bỏ qua thay vì lỗi)
  - `src/AdminPortal.Api/Controllers/AssessmentSheetsController.cs` — `POST pdf-archive`
  - `tests/AdminPortal.UnitTests/AssessmentSheetPdfArchiveTests.cs` (mới, 2 test); `tests/AdminPortal.IntegrationTests/AdminPortalApiTests.cs` — fake `+ DownloadAssessmentSheetPdfAsync`, `+ 1 test` (filter + archive)
- **Frontend `ui/`:**
  - `src/app/core/services/api-client.service.ts` — `+ postBlob`
  - `src/app/core/services/assessment-sheets.service.ts` — `+ downloadPdfArchive`
  - `src/app/core/models/api.models.assessment-sheets.ts` — `AssessmentSheetListQuery + responsibleTeacherId`
  - `src/app/pages/assessment-sheets/assessment-sheets.component.{ts,html}` — teacher filter, multi-select, Bulk Action dropdown + download
  - `src/app/pages/assessment-sheets/assessment-sheets.module.ts` — `+ DxDropDownButtonModule`
  - `src/app/pages/assessment-sheets/assessment-sheets.component.spec.ts` — `+ 3 test`

## Hạn chế đã biết

- Lỗi HTTP của `pdf-archive` (403/network) trả body dạng `Blob` nên `ApiError` chỉ hiển thị thông báo chung, không parse được `ProblemDetails`. Lỗi nghiệp vụ thường gặp (thiếu PDF) đã xử lý bằng "bỏ qua + `_bo-qua.txt`" trong zip 200, không phải lỗi.
- `PDFtoImage` cần native PDFium — khi đóng gói IIS phải chắc `runtimes/win-x64/native` có trong publish output (kiểm ở bước `$gv-portal-production`).

## DoD

- ✅ Backend `dotnet build api/AdminPortal.slnx -c Release --no-restore` → 0/0; `dotnet test api/tests/AdminPortal.UnitTests -c Release --no-restore` → **104/104** (+2 `AssessmentSheetPdfArchiveTests`).
- ✅ Frontend `npm --prefix ui run test:ci` → **152/152** (+3); `npm --prefix ui run build -- --configuration development` → pass hash `a20900fdd5061f798571`.
- ➖ Integration suite: **not run (Docker not available)** — test mới `AssessmentSheetListFiltersByResponsibleTeacherAndBulkPdfArchiveZipsSelectedPlans` đã compile trong Release build.
- ✅ `api/README.md`, `api/requests.http`, `docs/requirements/09`, `docs/plans/07-ASH`, `.agents/{shared,backend,frontend}/MEMORY.md` cập nhật.
- ⬜ Smoke thủ công (lọc GV; chọn nhiều dòng; tải 4 kiểu zip; mở `_bo-qua.txt`): chưa chạy — cần Google Drive live.
- ⬜ Chưa commit.
