# ASH-FB-W5 — Màn danh sách: lọc bằng chính lưới + màu hover theme

Tinh chỉnh tiếp theo cho **màn danh sách bảng đánh giá** (nối sau `ASH-FB-W4`). Chủ yếu **FE**; **1 thay đổi contract nhỏ ở BE** (thêm `studentNickName` vào list response, không migration).

## Tóm tắt ngắn

1. ✅ **Bỏ panel lọc riêng, lọc bằng chính lưới.** Gỡ hẳn `<section class="filter-card">` (search, select học sinh/giáo viên, 2 ô tháng, select trạng thái, hàng nút). Lưới tải **toàn bộ** bảng đánh giá về client (`loadAllSheets()` lặp trang `pageSize` 100, `remoteOperations` off) rồi để DevExtreme tự lọc/sắp/phân trang — giống bảng picker mục đánh giá. FE bỏ các field `search`/`status`/`studentId`/`responsibleTeacherId`/`dateFrom`/`dateTo`/`filtersExpanded`, bỏ 2 `CustomStore` học sinh/giáo viên và inject `StudentsService`/`TeachersService`.
2. ✅ **Bật của lưới:** `filter-row`, `header-filter`, `search-panel` (ô tìm kiếm), `column-chooser`. **Không** bật resize/reorder cột và **bỏ** `columnAutoWidth`/`columnHidingEnabled` (người dùng chấp nhận không cần resize; các tính năng này kích hoạt `_synchronizeColumns`/best-fit của DX19 → dễ văng lỗi khi rời màn).
3. ✅ **Toolbar lưới** (`onToolbarPreparing` — DX19 chưa khai báo được option `toolbar`): đẩy `Thêm bảng đánh giá` (dxButton), `Nhập Excel` (dxButton, dùng `@ViewChild('importFileInput')`), `Bulk Action (n)` (dxDropDownButton), `Đặt lại lọc lưới` (dxButton → `grid.clearFilter('row'|'header'|'search')`). Giữ nút "Chọn cột" mặc định. Nhãn/`disabled` của nút Nhập Excel + Bulk Action cập nhật động qua `onInitialized` bắt instance + `syncToolbar()`.
4. ✅ **Cột ngày `Bắt đầu`/`Hạn hoàn thành`:** `dataType="date"`, `filterOperations=['between','>=','<=','=']` + `selectedFilterOperation="between"` → filter row nhập **từ tháng → tới tháng**. `onEditorPreparing` đặt editor ô ngày (cả 2 ô của "between") thành `displayFormat="MM/yyyy"` + `calendarOptions.maxZoomLevel="year"`. `[calculateFilterExpression]="monthBetweenFilterExpression"` quy `[từ, tới]` về khoảng bao trọn tháng: `>=` đầu tháng "từ" **và** `<` đầu tháng kế tiếp của "tới" (chọn giữa tháng vẫn khớp). Hiển thị trong lưới `M/yy` (helper `monthText`, tháng không đệm `0`). Header filter tắt cho 4 cột ngày.
5. ✅ **Trạng thái:** header filter cột `status` map nhãn tiếng Việt (`statusHeaderFilter.dataSource`). Màu status pill phân biệt: `Open` xanh dương, `Planed` cam, `Done` xanh lá, `Canceled` xám + gạch ngang (class `status-<Value>`).
6. ✅ **Dòng `Canceled` gạch ngang cả dòng:** `onRowPrepared` toggle class `sheet-row-canceled` → `::ng-deep .dx-data-row.sheet-row-canceled > td { text-decoration: line-through; color:#98a2b3; }`.
7. ✅ **Màu hover dòng lưới — toàn theme.** `ui/src/styles.scss` override selector hover của `dxDataGrid` + `dxTreeList` từ xám mặc định `rgba(0,0,0,0.04)` → **xanh dương `rgba(51,122,183,0.18)`** (tone màu nhấn theme). Không sửa file theme sinh tự động (`ui/src/themes/generated/**`). Người dùng đã xem 4 phương án màu và chốt phương án xanh dương đậm.
8. ✅ **Cột "Học sinh": thêm tên gọi ở nhà sau mã, không in đậm.** BE: `AssessmentSheetListItemResponse` += `StudentNickName` (từ `StudentSnapshot.NickName` — dữ liệu snapshot đã có sẵn, **không migration**); `search` khớp thêm nickname (`Matches` fold). FE: model `AssessmentSheet.studentNickName`; template `studentCell` hiển thị `<small>{{ code }}{{ nickName ? ' · ' + nickName : '' }}</small>`, `.student-cell small { font-weight: 400 }` (bỏ đậm).
9. ✅ **Sửa lỗi runtime `Cannot read properties of null (reading 'css')` ở `_toggleBestFitMode` ← `_synchronizeColumns` (deferRender).** DevExtreme 19.2 (bị pin): `ResizingController.updateDimensions` lên lịch `_synchronizeColumns` qua `deferRender` (khi route-outlet đổi nội dung gây resize); callback chạy **sau khi widget bị hủy** lúc điều hướng edit/tạo mới → `_rowsView._getTableElement()` trả `null` → "Uncaught (in promise) TypeError" → `NavigationError`, màn edit/new không load.
   - **Fix gốc (đã xác minh bằng log):** `_synchronizeColumns`/`_toggleBestFitMode`/`updateDimensions` nằm trên **`ResizingController`** (`grid._controllers['resizing']`), **KHÔNG** phải view `gridView` — các bản vá trước nhắm `_views.gridView` nên wrapper không bao giờ được gọi. Helper mới `core/errors/dx-grid-bestfit-guard.ts` → `patchGridBestFit(grid.instance, prefix)` ghi đè own-property `_synchronizeColumns` + `_toggleBestFitMode` trên `_controllers.resizing`: bail khi `_rowsView`/`_getTableElement()` null / `component._disposed` (+ `try/catch` nuốt `TypeError` null). Gọi ở `onContentReady` của **cả** `AssessmentSheetsComponent` (list) **và** `AssessmentPickerComponent` (grid trong form). Log xác nhận: `patchGridBestFit — đã bọc ... trên ResizingController` rồi `bestFitGuard — bỏ qua _synchronizeColumns() vì lưới đã detach/disposed`, `NavigationEnd` thẳng, **không còn `NavigationError`**.
   - **Lưới an toàn (giữ lại, giờ không cần tới):** `AppErrorHandler` (`{ provide: ErrorHandler }`) nuốt đúng lỗi này nếu vẫn lọt; `NavigationRecoveryService` (`providedIn:'root'`, khởi tạo sớm ở `AppComponent`) — khi nuốt lỗi thì `navigateByUrl('/home')` rồi replay route add/edit vừa bấm (debounce 1.5s) — trick "gặp lỗi → về home → tự vào lại" người dùng yêu cầu.
   - **Hỗ trợ:** `leaveTo()` delay điều hướng edit/create qua `rAF`+`setTimeout(0)`; lưới danh sách bỏ `columnAutoWidth`/`columnHidingEnabled`/resize/reorder; cờ `destroyed` (`OnDestroy`).
   - **Đã thử & bỏ:** vá `GridView.prototype` global ở `main.ts` (`devextreme-grid-bestfit-patch.ts`); vá `_views.gridView` — cả hai không ăn. `templatesRenderAsynchronously` không có trong devextreme-angular 19.2.5.

## Phạm vi file

- `ui/src/app/pages/assessment-sheets/assessment-sheets.component.ts` — viết lại: `sheets[]` + `loadAllSheets()` thay `CustomStore`; `onToolbarPreparing`/`onEditorPreparing`/`onRowPrepared`/`monthBetweenFilterExpression`/`syncToolbar`/`resetGridFilters`; `OnDestroy` + cờ `destroyed`; bỏ `applyFilters`/`scheduleSearch`/`readSort`/`showDialogColumnChooser`/`studentDataSource`/`teacherDataSource` + inject `StudentsService`/`TeachersService`; `retryLoad`/`submitImportExcel` gọi `loadAllSheets()`.
- `ui/src/app/pages/assessment-sheets/assessment-sheets.component.html` — bỏ `filter-card`; `<input #importFileInput>` chuyển ra ngoài; lưới: `[dataSource]="sheets"`, bỏ `[remoteOperations]` + `columnAutoWidth`/`columnHidingEnabled`, thêm `filter-row`/`header-filter`/`search-panel`, `onToolbarPreparing`/`onEditorPreparing`/`onRowPrepared`, cột ngày `dataType=date` + `calculateFilterExpression`, cột `studentCell` thêm `studentNickName`, status pill `[ngClass]="'status-' + status"`.
- `ui/src/app/pages/assessment-sheets/assessment-sheets.component.scss` — bỏ style panel lọc; `.student-cell small` weight 400 (không đậm); thêm `status-Open/Planed/Done/Canceled` + `.sheet-row-canceled > td`.
- `ui/src/app/pages/assessment-sheets/assessment-sheets.module.ts` — bỏ import không dùng của panel (giữ lại `DxDateBoxModule` + `DxDropDownButtonModule` vì filter row ngày + toolbar `dxDropDownButton` cần widget được đăng ký).
- `ui/src/app/pages/assessment-sheets/assessment-sheets.component.spec.ts` — `createComponent` 2 tham số, spy `loadAllSheets`, grid stub `clearFilter`; sửa test reset (không còn `responsibleTeacherId`); `+3 test` (row Canceled gạch ngang, toolbar 4 nút + đồng bộ nhãn Bulk Action).
- `ui/src/app/core/models/api.models.assessment-sheets.ts` — `AssessmentSheet.studentNickName`.
- `ui/src/styles.scss` — override màu hover dòng lưới toàn theme.
- `ui/src/app/core/errors/dx-grid-bestfit-guard.ts` (mới) — `patchGridBestFit()` bọc `_synchronizeColumns`/`_toggleBestFitMode` trên `grid._controllers['resizing']`. **Fix gốc.**
- `ui/src/app/core/errors/app-error-handler.ts` (mới) + `navigation-recovery.service.ts` (mới) + `ui/src/app/app.module.ts` (`{ provide: ErrorHandler, useClass: AppErrorHandler }`) + `app.component.ts` (inject `NavigationRecoveryService` để khởi tạo sớm) — lưới an toàn: nuốt lỗi teardown + trick về `/home` rồi vào lại route cũ.
- `assessment-sheets.component.ts` + `assessment-picker.component.ts` — gọi `patchGridBestFit(grid.instance, prefix)` trong `onGridContentReady`/`onContentReady`; list thêm `(onInitialized)`/`(onContentReady)`, `leaveTo()`, cờ `destroyed`.
- `api/src/AdminPortal.Application/AssessmentSheets/AssessmentSheetModels.cs` — `AssessmentSheetListItemResponse` += `StudentNickName`.
- `api/src/AdminPortal.Application/AssessmentSheets/AssessmentSheetService.cs` — `ProjectList` map `NickName`; `Matches` fold thêm nickname.
- `api/README.md` — ghi `studentNickName` + search khớp nickname.

Không đụng `assessment-sheets-form.component.html` (thay đổi `[colSpan]="2"` là của người dùng, không gộp vào commit này).

## Ghi chú kỹ thuật

- **Đánh đổi:** tải hết bảng đánh giá về client (người dùng chọn, thay vì nối remote filtering). Rủi ro khi dữ liệu lớn dần — nếu cần sẽ quay lại `remoteOperations={paging,sorting,filtering}` + dịch filter expression sang query API. Ghi ở `.agents/frontend/MEMORY.md`.
- `dxDropDownButton` trong toolbar lưới: **phải giữ `DxDropDownButtonModule`** trong module, nếu không widget chưa đăng ký → lưới ném lỗi lúc render (đã vấp 1 lần).
- Ô ngày filter row: `DxDateBoxModule` cần trong module để `dxDateBox` được đăng ký cho editor filter row.
- Vá `_toggleBestFitMode` là fix chung cho lỗi best-fit khi hủy lưới lúc điều hướng — không chỉ màn này. Khi nào nâng DevExtreme thì rà lại có còn cần không.
- `StudentNickName` list response: dữ liệu lấy từ `StudentSnapshot.NickName` đã lưu sẵn → **không migration**.

## DoD

- ✅ `npm --prefix ui run test:ci` → **158/158** (+3).
- ✅ `npm --prefix ui run build -- --configuration development` → pass hash `0e1d181459663caaab7e`.
- ✅ `dotnet build api/AdminPortal.slnx -c Release --no-restore` → 0 warning / 0 error (thêm field DTO, không migration).
- ⬜ `dotnet test api/tests/AdminPortal.UnitTests` — chưa chạy lại lượt này (thay đổi BE chỉ là 1 field DTO + projection).
- ✅ `docs/requirements/09` §16.1 + §16.2, `docs/plans/07-ASH` §13, `api/README.md`, `.agents/frontend/MEMORY.md` + `.agents/backend/MEMORY.md` cập nhật.
- ⬜ Smoke thủ công (mở danh sách: filter row/header filter/ô tìm kiếm/column chooser; toolbar 4 nút; lọc `Bắt đầu`/`Hạn hoàn thành` từ tháng → tới tháng; dòng `Canceled` gạch ngang; cột Học sinh có nickname; hover dòng lưới xanh dương ở mọi màn có lưới; **bấm Chỉnh sửa / Thêm bảng đánh giá không còn văng `_toggleBestFitMode`**): chưa chạy.
- ⬜ Commit: chưa.
