# ASH-FB-W3 — Feedback batch, Đợt 3 (G6b + G6a)

Thực thi Đợt 3 (cuối) của [`29-ASH-FB-01.md`](29-ASH-FB-01.md) — thanh sticky màn chỉnh sửa bảng đánh giá. **FE-only.**

## Tóm tắt ngắn

1. ✅ **G6b — Nút "Tạo mới đánh giá" ở thanh sticky.** Chỉ hiện ở màn edit (`!isCreate`). `openCreateNew()` → `router.navigate(['/assessment-sheets/new'])`. Route `/new` khác config `/:id/edit` nên Angular dựng lại `AssessmentSheetFormComponent` (form trống, `ngOnInit` chạy lại). `PendingChangesGuard.canDeactivate` → `canLeave()` tự nhắc "thay đổi chưa lưu".
2. ✅ **G6a — Nút combo "Hoàn thành kế hoạch".** Hiện khi `!isCreate && assessmentSheetId && originalStatus === 'Open'`; enable thêm điều kiện không đang bận + `hasRecords`. `completePlan()` chạy tuần tự như thao tác tay, **không confirm**, **lỗi bước nào dừng bước đó** (không rollback):
   1. `editor.status = 'Open'` → `save()`. Lỗi (`formError`) → dừng.
   2. `editor.status = 'Planed'` → `save()` (`saveExisting()` tự gọi `updateStatus` khi status khác baseline). Lỗi → dừng.
   3. `canOpenPlanPdfPreview()` (yêu cầu đã rời `Open` + có mục đánh giá) — không đạt → `formError` + dừng.
   4. `router.navigate(['/assessment-sheets', id, 'plan-pdf-preview'], { queryParams: { auto: 1 } })` (đặt `allowPreviewNavigationOnce` phòng hờ; sau 2 lần save `dirty=false` nên guard không hỏi).
   - Trang preview kế hoạch (`assessment-sheet-plan-preview.component`): đọc `?auto=1` → `autoUpload`. Sau khi `load()` + `ngAfterViewChecked` fit xong (`model === lastFittedModel`), chạy **một lần** (`autoUploadStarted`) `setTimeout(300ms)` → `uploadPdfToDrive()` (tạo PDF bằng `html2pdf` + `upload-plan-pdf` lên Drive). Không lỗi thì `goBack()` về màn edit.

## Phạm vi file (tất cả `ui/`)

- `src/app/pages/assessment-sheets/assessment-sheets-form.component.{ts,html}` — `completingPlan` flag; getters `canShowCompletePlan`/`canCompletePlan`; `openCreateNew()`; `completePlan()`; 2 nút mới ở `.form-actions` (+ `completingPlan` vào `[disabled]` các nút cũ).
- `src/app/pages/assessment-sheets/assessment-sheet-plan-preview.component.ts` — `autoUpload`/`autoUploadStarted`; đọc `?auto=1`; `runAutoUpload()` trong `ngAfterViewChecked`.
- `src/app/pages/assessment-sheets/assessment-sheets.component.spec.ts` — `+ 2 test` (canShowCompletePlan; openCreateNew điều hướng).

Không đụng backend (dùng lại `upload-plan-pdf`, `update`, `update-status` có sẵn). Không migration.

## Tinh chỉnh thêm (người dùng, 2026-09-01)

- `startDate`/`dueDate` nhập theo **tháng**: dx-date-box `displayFormat="MM/yyyy"` + `calendarOptions.maxZoomLevel="year"` (lịch dừng ở mức chọn tháng). Áp ở form edit (`dateEditorOptions`) và 2 ô lọc "Từ tháng"/"Đến tháng" của màn danh sách.
- Cột `Bắt đầu`/`Hạn hoàn thành` trong lưới danh sách hiển thị `MM/yy` (helper `monthText()`); các cột ngày khác (`Ngày nộp`, `Cập nhật`) giữ nguyên đầy đủ.

## Quyết định (theo `29-ASH-FB-01.md` G6a/G6b, chốt 2026-08-31)

- Combo là "auto action" — chạy nổi (không nền/ẩn), lỗi ở bước nào dừng bước đó như thao tác tay, không confirm tổng.
- Bước 4 "tạo PDF + upload Drive": tái dùng trang preview có sẵn (`ASH-FE-11`) + cờ `?auto=1` để tự bấm "Tạo PDF lên Drive" rồi quay lại.
- G6b: form tạo trống; nhắc thay đổi chưa lưu (dùng `PendingChangesGuard` sẵn có).

## DoD

- ✅ `npm --prefix ui run test:ci` → **156/156** (+2).
- ✅ `npm --prefix ui run build -- --configuration development` → pass hash `0b9676005ccccd05942c` (kèm month-picker startDate/dueDate).
- ➖ Backend: không đổi.
- ✅ `docs/requirements/09` §7 (hoàn thiện plan) + `docs/plans/07-ASH` §13 + `.agents/frontend/MEMORY.md` cập nhật.
- ⬜ Smoke thủ công (Open sheet → "Hoàn thành kế hoạch": lưu → Planed → mở preview → PDF lên Drive → về edit; "Tạo mới đánh giá" khi form dirty → nhắc): chưa chạy (cần Google Drive live).
- ✅ Commit `e6a3a00`.
