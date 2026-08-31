# ASH-FB-W1 — Feedback batch, Đợt 1 (G2 + G9 + G8 + G1)

Thực thi Đợt 1 của [`29-ASH-FB-01.md`](29-ASH-FB-01.md). Bốn nhóm độc lập, rủi ro thấp, gộp 1 milestone.

## Tóm tắt ngắn

1. ✅ **G2 — Records panel + picker: cặp icon `thêm`/`xóa`, bỏ mọi confirm.** Picker (add-mode): dòng chưa có → icon `thêm` (emit `assessmentAdd`); dòng đã có → icon `xóa` `type="danger"` (emit `assessmentRemove` mới) thay cho badge "Đã có". Form: `addAssessmentToSheet` + `removeAssessmentRecord` bỏ `confirm()`; `removeAssessmentRecordByAssessment` map code→record cho event mới; bảng records + `buildRemoveAssessmentSheetRecordRequest` + `removeRecordHint` bỏ chặn `records.length <= 1` (cho xóa tới rỗng).
2. ✅ **G9 — Thêm status `Canceled`.** BE: enum `AssessmentSheetStatus` + `Canceled` (string ≤20, **không migration**, `has-pending-model-changes` = none); `UpdateAsync` cho sửa meta khi `Open` **hoặc** `Canceled`; `AllSheetStatuses` (+`Canceled`). Không có transition rule (`UpdateStatusAsync` gán thẳng). FE: type + `ASSESSMENT_SHEET_STATUS_OPTIONS` (`Đã hủy`) + 3 helper `can*` + `showPlan`/`showResult` + link `readOnly` coi `Canceled` như `Open`; sync dialog `replaceStatuses`/`sheetStatuses` union += `Canceled`.
3. ✅ **G8 — Upload Google Drive: tạo file mới trước, xóa file cũ theo ID sau.** `GoogleSheetsService.SavePdfToDriveAsync` bỏ nhánh `Files.Update`; luôn `Files.Create` rồi `TryDeleteDriveFileAsync(existingFileId)` (nuốt `GoogleApiException` 404). `PlanFileLinkPdf`/`ResultFileLinkPdf` đổi mỗi lần upload.
4. ✅ **G1 — Tạo mới không bắt buộc mục đánh giá.** BE: `CreateAsync` chỉ `EnsureDistinctIds` khi `records` không rỗng → `POST /assessment-sheets` nhận `records: []`. FE: bỏ `<app-assessment-picker *ngIf="isCreate">`; bỏ guard "chọn ít nhất một mục" trong `save()`; đổi `subtitle` + ẩn count ở create. Điều hướng vào `/…/edit` sau tạo đã có sẵn.

## Phạm vi file

- **Backend `api/`:**
  - `src/AdminPortal.Domain/Enums/AssessmentSheetStatus.cs` — thêm `Canceled`
  - `src/AdminPortal.Application/AssessmentSheets/AssessmentSheetService.cs` — `CreateAsync` (G1), `UpdateAsync` (G9)
  - `src/AdminPortal.Application/GoogleSheets/AssessmentSnapshotReplacementRules.cs` — `AllSheetStatuses` (G9)
  - `src/AdminPortal.Application/GoogleSheets/GoogleSheetsService.cs` — `SavePdfToDriveAsync` (G8)
  - `tests/AdminPortal.UnitTests/*` — case cho G1/G9
  - `README.md` / `requests.http` — nếu đổi hành vi contract (G1 `records: []`, G9 `Canceled`)
- **Frontend `ui/`:**
  - `src/app/core/models/api.models.assessment-sheets.ts` — type + options (G9)
  - `src/app/pages/assessment-sheets/assessment-picker.component.{ts,html}` — icon xóa + event `assessmentRemove` (G2)
  - `src/app/pages/assessment-sheets/assessment-sheets-form.component.{ts,html}` — bỏ confirm, bỏ picker trên create, helper `can*` (G2/G9/G1)
  - `src/app/pages/assessment-sheets/*.spec.ts` — cập nhật/thêm test

## Quyết định áp dụng (từ `29-ASH-FB-01.md`)

- G9 `Canceled` = chỉ nhãn, vẫn cho sửa như `Open`; chuyển qua lại tự do (không transition rule); `DoneDate` = null khi `Canceled`.
- G8 thứ tự an toàn: tạo mới OK rồi mới xóa cũ; xóa theo ID; not-found → bỏ qua.
- G1 ràng buộc khác (student active…) giữ nguyên; chỉ bỏ min-1 record.
- G2 bỏ confirm cả thêm và xóa; cho xóa tới rỗng (đồng bộ G1).

## DoD

- ✅ Backend: `dotnet build api/AdminPortal.slnx -c Release --no-restore` → **0 warning / 0 error**; `dotnet test api/tests/AdminPortal.UnitTests -c Release --no-restore` → **102/102 pass**. (Debug build bị khóa DLL do dev API PID 28144 đang chạy — không phải lỗi compile; đã verify qua Release.)
- ✅ `dotnet-ef migrations has-pending-model-changes` (Release) → **"No changes have been made to the model since the last migration."** G9 không cần migration.
- ✅ Frontend: `npm --prefix ui run test:ci` → **149/149 pass** (+4 test mới: G1 remove-to-empty, G1 not-found throw, G9 `can*` × Canceled, G2 `assessmentRemove`); `npm --prefix ui run build -- --configuration development` → **pass hash `f3c8bc68de8e00d2e26f`** (chỉ warning CommonJS/DevExtreme quen thuộc).
- ➖ Integration suite: **not run (Docker not available)** — npipe `dockerDesktopLinuxEngine` không kết nối. Theo `AGENTS.md` là bước tùy chọn, không chặn done. Chạy lại khi có Docker (đụng contract create `records: []` + status `Canceled`).
- ✅ `api/README.md`, `api/requests.http`, `docs/requirements/09` (§4 bảng trạng thái + §5 tạo rỗng + §8 cơ chế Drive), `docs/plans/07-ASH` (§13 thêm `ASH-FB-W1-G1/G8/G9`), `.agents/{shared,backend,frontend}/MEMORY.md` đã cập nhật.
- ⬜ Smoke thủ công (tạo sheet rỗng → điều hướng edit; Canceled ↔ mọi trạng thái + vẫn sửa được; upload PDF lần 2 kiểm tra file cũ trên Drive bị xóa): **chưa chạy** — cần môi trường chạy + Google live.
- ⬜ Chưa commit.
