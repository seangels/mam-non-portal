# ASH-FB-W2 — Feedback batch, Đợt 2 (G3 + G4 + G5)

Thực thi Đợt 2 của [`29-ASH-FB-01.md`](29-ASH-FB-01.md) — **đại tu bảng picker mục đánh giá** + thứ tự nhóm Lv2 ở trang "DS Đánh giá". Toàn bộ **FE-only**.

## Tóm tắt ngắn

1. ✅ **G3 — Thứ tự nhóm Lv2 cố định (FE tự sắp).** Helper dùng chung mới trong `api.models.assessment-sheets.ts`: `assessmentGroupLv2Order(name)` (1..5 theo `ASSESSMENT_GROUP_LV2_CONFIGS`, khớp không dấu/không phân biệt hoa thường qua `normalizeVietnamese`, nhóm lạ xếp cuối) + `compareAssessmentByFixedGroupOrder(a, b)` (nhóm Lv2 cố định → **cùng nhóm thì theo `rowIndex`** → mã). Áp ở:
   - Picker: `buildGroupOptions` cho dropdown **Nhóm 2** (Lv1/Lv3 giữ abc); `applyFilters` sắp `filteredAssessments` theo comparator; header filter cột **Nhóm 2** giữ thứ tự cố định (`groupLv2HeaderFilter.dataSource.postProcess`).
   - Trang "DS Đánh giá" (`assessments.component.ts`): `loadLv2DataSouce` sắp lại `result.items` cho dropdown Nhóm 2; lưới — khi **chưa chọn sort cột nào** thì sắp `result.items` theo comparator (trang tải hết `pageSize 5000` nên sắp client-side là đủ); `readSort` trả thêm `isExplicit`.
   - `ASSESSMENT_GROUP_LV2_CONFIGS` (order 1–5) **không đổi**; `normalizeVietnamese` **không đụng**. Lv1/Lv3 không có danh mục chuẩn → giữ nguyên.
2. ✅ **G4 — Picker: cột hiển thị + resize + reorder.** Thứ tự cột mặc định: `Thao tác` (cột chọn/thêm) → `Nội dung` (name) → `Kết quả gần nhất` (latestGrade) → `Nhóm 3` (groupLv3Name) → `Nhóm 2` (groupLv2Name) → `Nhóm tuổi` (groupLv1Name). **Ẩn mặc định** (vẫn bật lại được qua column chooser): `Ghi chú gần nhất` (latestNote), `Mã` (code), `RowIndex`. `[allowColumnResizing]="true"` + `columnResizingMode="widget"` + `[allowColumnReordering]="true"`; đặt lại `width`/`minWidth`. Cột `Thao tác` `[fixed]="true" fixedPosition="left"` + `[allowResizing]="false"` + `[allowReordering]="false"` (ghim trái, người dùng đổi thứ tự các cột còn lại).
3. ✅ **G5 — Bảng picker: chỉ dùng filter của lưới.** (Ban đầu giữ panel filter cũ làm "vòng 1"; sau đó người dùng chốt **bỏ hẳn panel** — xem "Tinh chỉnh thêm".) Lưới bật `<dxo-filter-row>` **và** `<dxo-header-filter>`.
   - `<dxo-header-filter>` không có `[allowSelectAll]` ở DevExtreme 19.2 (popup vốn đã có "(Select All)") → bỏ thuộc tính đó.
   - Cột `Kết quả gần nhất`: `calculateCellValue` trả **nhãn** (null → `Chưa có`) để header filter gom nhóm được; `[filterValues]` mặc định = tất cả nhãn **trừ `Đạt +`** (gồm `Chưa có`). **Không** đồng bộ với default TagBox `ASH-FE-05` (2 chỗ độc lập).
   - Header filter là "dạng dropdown" cho `Nhóm 1/2/3` + `Kết quả`; tắt header filter ở `Nội dung`/`Ghi chú gần nhất` (free text, chỉ dùng filter row).
   - **Nút "Đặt lại lọc lưới"** riêng (icon `clearformat`) ở panel: `resetGridFilters()` = `grid.clearFilter('row')` + `grid.clearFilter('header')` + `columnOption('latestGrade', 'filterValues', <mặc định trừ Đạt +>)`. Nút "Đặt lại" (cũ) reset cả panel + gọi luôn `resetGridFilters()`.

### Tinh chỉnh thêm (người dùng, 2026-09-01)

- Lưới picker: `pageSize` mặc định **50** (`gridDefaultPageSize`), `allowedPageSizes` = `[20, 50, 100, 200, 1000, 2000]`.
- Thanh sticky màn edit (`.form-actions`): thêm 2 nút icon (chỉ `!isCreate`) — `arrowup` `scrollToRecords()` (`#assessment-records-heading`) và `arrowdown` `scrollToBottom()`. Shell cuộn ở `dx-scroll-view` (`.dx-scrollable-container`), không phải window → cả hai dùng `scrollIntoView({behavior:'smooth'})`; nút xuống nhắm `document.querySelector('app-footer')` (footer của shell).
- **Bỏ hẳn panel filter (vòng 1)** của picker: gỡ toàn bộ `<section class="assessment-picker-filter">` (search, TagBox `Kết quả gần nhất`, dropdown Lv1/Lv2/Lv3, view mode, select-all-visible, 3 nút). Việc lọc giờ hoàn toàn bằng filter row + header filter của lưới (vòng 2). Component giữ lại các method/field cũ (`search`, `groupLvXName`, `latestGradeFilters`, `viewMode`, `applyFilters`, `buildGroupOptions`…) để spec cũ vẫn chạy; chúng luôn ở giá trị mặc định nên `filteredAssessments` = toàn bộ (đã sắp theo G3).
- **Toolbar của lưới** (DevExtreme 19.2 chưa khai báo được option `toolbar` → dùng `(onToolbarPreparing)`): giữ nút **"Chọn cột"** mặc định của datagrid (`<dxo-column-chooser [enabled]="true" mode="select">`, bỏ hàm custom `showDialogColumnChooser`/`isColumnChooserOpen`) + thêm nút **"Đặt lại lọc lưới"** (`resetGridFilters`) ở đầu toolbar.

## Phạm vi file (tất cả `ui/`)

- `src/app/core/models/api.models.assessment-sheets.ts` — `+ assessmentGroupLv2Order`, `+ compareAssessmentByFixedGroupOrder`, `+ import normalizeVietnamese`.
- `src/app/pages/assessment-sheets/assessment-picker.component.{ts,html}` — G3 sort + G4 cột/resize + G5 header/row filter; bỏ template `latestGradeCell` (thay bằng `calculateCellValue`), giữ method `latestGradeText` (spec còn dùng).
- `src/app/pages/assessments/assessments.component.ts` — G3 dropdown Nhóm 2 + default sort lưới; `readSort` + `isExplicit`.
- `src/app/pages/assessment-sheets/assessment-sheets-form.component.{ts,html}` — 2 nút "lên đầu"/"xuống cuối" ở thanh sticky (`scrollToRecords`/`scrollToBottom`).
- `src/app/pages/assessment-sheets/assessment-sheets.component.spec.ts` — `+ 2 test` cho `assessmentGroupLv2Order` / `compareAssessmentByFixedGroupOrder`.

Không đụng backend. Không migration. Không production/IIS/deploy.

## Quyết định làm rõ trong lúc code

- **Tiebreak cùng nhóm Lv2**: người dùng chốt (2026-09-01) — cùng thứ tự nhóm Lv2 thì "các phần còn lại cứ theo `rowIndex` như hiện tại". Bỏ bước so `groupLv3Name` abc; comparator giờ là Lv2 → `rowIndex` → mã (mã chỉ là tiebreak cuối).
- `dxo-header-filter [allowSelectAll]` không tồn tại ở DevExtreme 19.2 → gỡ (người dùng xác nhận).

## DoD

- ✅ `npm --prefix ui run test:ci` → **154/154** (+2).
- ✅ `npm --prefix ui run build -- --configuration development` → pass hash `ec9bc855bc3298eeaff4` (đã gỡ panel filter, thêm toolbar + nút cuộn) (chỉ warning CommonJS/DevExtreme quen thuộc).
- ➖ Backend: không đổi — không chạy lại gate BE.
- ✅ `docs/requirements/09` §5 + §16 (picker), `docs/plans/07-ASH` §13, `.agents/frontend/MEMORY.md` cập nhật.
- ⬜ Smoke thủ công (mở picker: thứ tự nhóm Lv2, 6 cột, resize, header/row filter, default `Kết quả`; trang "DS Đánh giá" dropdown + sort): chưa chạy.
- ✅ Commit `a800bc7` (+ follow-up pageSize=50).
