# ASH-SYNC-01 — Popup đồng bộ Google Sheets: 2 chế độ (mặc định / thay thế snapshot bản ghi)

## Tóm tắt ngắn

1. ✅ Popup xác nhận "Đồng bộ GGSheet" (màn `Quản lý Đánh giá`) đổi từ confirm Có/Không sang **radio 2 chế độ**.
2. ✅ Chế độ 1 — **Đồng bộ mặc định**: giữ nguyên hành vi cũ (rebuild catalog `Assessment` + mirror `AssessmentSheetLatest`/`AssessmentRecordLatest`), không đụng `AssessmentRecord.AssessmentSnapshot`.
3. ✅ Chế độ 2 — **Đồng bộ + thay thế snapshot bản ghi**: làm hết chế độ 1, rồi ghi đè các trường **đã chọn** từ `Assessment` mới vào `AssessmentRecord.AssessmentSnapshot`, khớp theo `AssessmentSnapshot.Code == Assessment.Code`, chỉ với `AssessmentSheet` có `AssessmentSheetStatus` thuộc danh sách đã chọn.
4. ✅ Trường cho chọn (checkbox): `Name`, `GroupLv1Name`, `GroupLv2Name`, `GroupLv3Name`, `RowIndex`. `Code` là khóa khớp — không thay. Mặc định chỉ tick `Name` (phạm vi hẹp; các nhóm/RowIndex phải tự tick).
5. ✅ Trạng thái sheet cho chọn (checkbox, mặc định tick hết): `Open`, `Planed`, `Done`.
6. ✅ Bản ghi có `Code` không còn trong catalog mới → giữ nguyên, không đếm.
7. ✅ Chỉ đóng dấu `UpdatedAt`/`UpdatedByUserId` khi có giá trị thực sự đổi; response thêm `replacedRecordSnapshots` = số bản ghi đã đổi.
8. ✅ Quyền: chỉ `SuperAdmin`/`Admin` dùng được chế độ 2. UI ẩn checkbox và hiện cảnh báo cho `Teacher`; backend chặn `Teacher` + `replaceRecordSnapshots` → `403`. Thiếu trường/thiếu status → `400`.
9. ✅ Cố ý ghi đè tên nhóm snapshot mà [`25-ASH-GRP-01.md`](25-ASH-GRP-01.md) / [`26-ASH-IMP-02.md`](26-ASH-IMP-02.md) vốn giữ tùy biến — đây là chủ đích của yêu cầu.
10. ✅ Popup tách thành component `app-google-sheets-sync-dialog` (`shared/components/google-sheets-sync-dialog/`) — component chỉ thu thập tùy chọn + phát `confirmSync` (request); host tự gọi API và xử lý kết quả.
11. ✅ Chốt (2026-08-29, người dùng): **chỉ để 1 nút đồng bộ ở màn DS Đánh giá (`pages/assessments`)**. Đã **gỡ hẳn** nút "Đồng bộ Google Sheets" (và code sync liên quan) khỏi màn danh sách bảng đánh giá (`pages/assessment-sheets/assessment-sheets.component`) và picker chọn mục (`assessment-picker.component`).

## Phạm vi

- Backend `api/`:
  - `GoogleSheets/GoogleSheetsModels.cs`: `SyncAssessmentsFromGoogleSheetsRequest` thêm `AssessmentRecordSnapshotReplacement? ReplaceRecordSnapshots`; record mới `AssessmentRecordSnapshotReplacement` (5 cờ bool + `IReadOnlyList<AssessmentSheetStatus>? SheetStatuses`, prop `HasAnyField`); response thêm `int ReplacedRecordSnapshots`.
  - `GoogleSheets/AssessmentSnapshotReplacementRules.cs` (mới): `Validate` (≥1 trường và ≥1 status, nếu không → `AppValidationException`) + `Apply` thuần (merge field đã chọn, đếm bản ghi đổi).
  - `GoogleSheets/GoogleSheetsService.SyncAssessmentsAsync`: guard `Teacher` + `replaceRecordSnapshots` → `ForbiddenException`; gọi `Validate`; sau bước rebuild + bulk insert, load `AssessmentRecords` (tracked, `Include(AssessmentSheet)`) theo `sheetStatuses`, gọi `Apply`, `SaveChangesAsync` khi có thay đổi; audit `GoogleSheets.AssessmentsSynced` ghi kèm `ReplacedRecordSnapshots`, cờ trường, danh sách status.
  - `api/README.md`, `api/requests.http`: mô tả `replaceRecordSnapshots` + ví dụ body chế độ 2.
- Frontend `ui/`:
  - `core/models/api.models.ts`: `AssessmentRecordSnapshotReplacement`, cập nhật request/response sync.
  - `shared/components/google-sheets-sync-dialog/` (mới, `.ts`+`.html`+`.scss`+`.spec.ts`): component + `GoogleSheetsSyncDialogModule`. `dx-popup` chứa radio `syncModeOptions` + 2 nhóm `dx-check-box`; getter `canReplaceSnapshot` (inject `AuthService`)/`replaceSelected`/`anyReplace*`/`confirmDisabled`; `onShowing()` reset về mặc định; `buildRequest()`; `confirm()` phát `confirmSync` + đóng popup. `@Input() visible` + `@Output() visibleChange` (hỗ trợ `[(visible)]`). `shared/components/index.ts` export thêm.
  - `pages/assessments/assessments.component.{ts,html,scss}` + `assessments.module.ts`: **nơi duy nhất** dùng dialog. Nút "Đồng bộ GGSheet" → `openSyncDialog()`; `<app-google-sheets-sync-dialog [(visible)]="syncDialogVisible" (confirmSync)="onSyncConfirmed($event)">`; `onSyncConfirmed(request)` gọi API + toast (`Đã thay thế snapshot [N] dòng bản ghi.` khi `replacedRecordSnapshots > 0`). Module: bỏ `DxRadioGroupModule`, thêm `GoogleSheetsSyncDialogModule`.
  - `pages/assessment-sheets/assessment-sheets.component.{ts,html}`: **gỡ** nút "Đồng bộ Google Sheets" + `syncAssessmentsFromGoogleSheets()`/`syncing`/`syncButtonText`/`syncDisabled` + inject `GoogleSheetsService`. `importDisabled` bỏ điều kiện `syncing`.
  - `pages/assessment-sheets/assessment-picker.component.{ts,html}`: **gỡ** nút "Đồng bộ GGSheet" + `syncAssessmentsFromGGSheet()`/`saving`/`saveButtonText`/`saveDisabled` + inject `GoogleSheetsService` + import `custom`.
  - `assessment-sheets.module.ts`: không thêm `GoogleSheetsSyncDialogModule` (không còn dùng ở đây).
  - Spec: `assessment-sheets.component.spec.ts` cập nhật constructor 2 component (bỏ đối số `googleSheet`).
  - `shared/components/google-sheets-sync-dialog/google-sheets-sync-dialog.component.spec.ts` (mới): 5 test — default phát `{}` + đóng popup, admin `buildRequest` đúng, teacher gate về `{}`, `confirmDisabled` khi chưa chọn gì, `onShowing` reset mặc định.
- Không migration mới (`AssessmentSnapshot` là complex property `ToJson`, không đổi schema).
- Không production/IIS/deploy; không thêm lệnh gọi Google mới (dùng lại luồng `SyncAssessmentsAsync`).

## Ràng buộc & quyết định

- Chốt qua `AskUserQuestion` (2026-08-29):
  1. Trường thay thế = `Name` + `Lv1/Lv2/Lv3` + `RowIndex` (tất cả trừ `Code`).
  2. "Cho phép lựa status sheets luôn" → popup có thêm nhóm checkbox trạng thái sheet; mặc định tick cả 3.
  3. Quyền: `SuperAdmin`/`Admin` bật — `Teacher` disable (UI ẩn + backend `403`). Hệ thống không có role "Quản lý" riêng nên map vào `Admin`.
- Làm trực tiếp trên nhánh `minor/assessment-sheet-stt-edit` (nhánh ASH mới nhất) theo yêu cầu người dùng.

## DoD

- ✅ `dotnet build api/AdminPortal.slnx --no-restore` pass 0 warning/error.
- ✅ `dotnet test api/tests/AdminPortal.UnitTests --no-restore` pass 101/101 (thêm `AssessmentSnapshotReplacementRulesTests`, 6 case).
- ➖ Integration suite: **not run** — luồng `SyncAssessmentsAsync` thật cần Google Sheets live; `FakeGoogleSheetsService` trong integration test ném `NotImplementedException` nên endpoint không test được logic này (trạng thái sẵn có, không do task này gây ra). Logic thay thế đã tách ra `AssessmentSnapshotReplacementRules` và test thuần.
- ✅ `npm --prefix ui run test:ci` pass 142/142 (5 test cho `GoogleSheetsSyncDialogComponent`; sửa constructor 2 spec).
- ✅ `npm --prefix ui run build -- --configuration development` pass, chỉ warning CommonJS/DevExtreme quen thuộc.
- ✅ `api/README.md`, `api/requests.http` cập nhật.
- ✅ Chỉ còn 1 nút đồng bộ ở màn DS Đánh giá (`pages/assessments`); đã gỡ nút ở `assessment-sheets` list và `assessment-picker`.
- ⬜ Smoke thủ công trên browser + chạy đồng bộ Google thật: chưa chạy.
