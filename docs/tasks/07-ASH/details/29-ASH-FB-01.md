# ASH-FB-01 — Batch feedback người dùng (2026-08-31), màn bảng đánh giá năng lực

Bản ghi nhận feedback, **chưa thực thi**. Gom 11 ý người dùng nêu ngày 2026-08-31 thành các nhóm rõ ràng, không trùng lặp; phương án đã chốt xong 2 vòng hỏi–đáp (2026-08-31), không còn câu hỏi mở. Contract nền: [`../../../requirements/09-bang-danh-gia-nang-luc.md`](../../../requirements/09-bang-danh-gia-nang-luc.md), [`../../../plans/07-ASH-assessment-sheet.md`](../../../plans/07-ASH-assessment-sheet.md).

## Tóm tắt ngắn

> **Tiến độ:** Đợt 1 (G1, G2, G8, G9) ✅ **đã code + verify** — xem [`30-ASH-FB-W1.md`](30-ASH-FB-W1.md). Thứ tự thực thi còn lại theo yêu cầu người dùng (2026-08-31): **Đợt 4 → Đợt 2 → Đợt 3**.

1. ✅ **G1 — Tạo mới AssessmentSheet không bắt buộc chọn mục đánh giá.** [`ASH-FB-W1`] `POST /assessment-sheets` cho `records: []` rỗng, ràng buộc khác giữ nguyên; sau khi tạo điều hướng thẳng vào màn edit của sheet vừa tạo; panel chọn mục đánh giá chỉ ở màn edit. *(item 1 — đã chốt)*
2. ✅ **G2 — Records panel: [`ASH-FB-W1`]  cặp icon `thêm`/`xóa` mỗi dòng, bỏ mọi confirm.** Dòng chưa có record: hiện icon `thêm`. Dòng đã có record: ẩn icon `thêm`, hiện icon `xóa`. Thao tác vẫn lưu ngay qua endpoint full-replace records. *(item 2 — đã chốt, đảo confirm của `ASH-FE-07`/`ASH-FE-08`)*
3. ⬜ **G3 — Thứ tự nhóm Lv2 cố định.** `ASSESSMENT_GROUP_LV2_CONFIGS` (order 1–5) đã đúng, không sửa. Không đụng logic so khớp tên (`normalizeVietnamese`) — phát sinh phải hỏi. Áp thứ tự cố định ở: dropdown lọc Nhóm 2 của picker, dropdown Lv2 header filter mới (G5), **grid picker mục đánh giá**, và **trang "DS Đánh giá" (danh mục Assessment)** — cả dropdown Nhóm 2 lẫn default sort grid. FE tự sắp sau khi nhận (không đụng backend `/assessment-groups`). Lv1/Lv3 giữ nguyên (không có danh sách chuẩn). *(item 3 — đã chốt)*
4. ⬜ **G4 — Picker: cột hiển thị + resize.** Hiện đúng 6 cột: Nhóm tuổi (groupLv1), Nhóm Lv2, Nhóm Lv3, Nội dung, Kết quả gần nhất, Ghi chú gần nhất. Ẩn `mã` và `rowIndex`. `allowColumnResizing=true`, `columnResizingMode="widget"`. *(item 4 — đã chốt)*
5. ⬜ **G5 — Bảng picker: 2 vòng lọc.** Vòng 1 = panel filter sẵn có (TagBox…). Vòng 2 = bật `headerFilter` (`allowSelectAll=true`) **và** filter row của datagrid. Mặc định lọc `Kết quả` = tất cả trừ `Đạt +` (gồm `Chưa có`); **không** cần đồng bộ với default TagBox `ASH-FE-05`. Item cho dropdown nhóm Lv1/Lv2/Lv3 + `Kết quả` = dùng cơ chế header filter tự sinh (distinct từ dữ liệu đang hiển thị). *(item 5 + 6 — đã chốt; xem G3 điểm 9 cho thứ tự Lv2)*
6. ⬜ **G6a — Sticky bar edit: nút combo `Hoàn thành kế hoạch`.** Điều kiện status = `Open`. Auto chạy tuần tự như thao tác tay: Lưu (Open) → chuyển `Planed` → Lữu (Planed) → tạo PDF kế hoạch + upload Drive. Không chạy nền/ẩn. Lỗi ở bước nào thì **dừng ở bước đó** như thao tác thủ công. Không confirm trước khi chạy. *(item 7 — đã chốt)*
7. ⬜ **G6b — Sticky bar edit: nút `Tạo mới đánh giá`.** Mở form tạo trống; có nhắc nếu sheet hiện tại còn thay đổi chưa lưu. *(item 8 — đã chốt)*
8. ⬜ **G7a — Danh sách: lọc theo giáo viên phụ trách.** Nguồn dropdown = `GET /teachers`. Role `Teacher` không khóa (vẫn được đổi filter). *(item 10.1 — đã chốt)*
9. ⬜ **G7b — Danh sách: datagrid select multi + paging remote server-side.** `selectAllMode` **không** giữ selection qua các trang. *(item 10.2 — đã chốt)*
10. ⬜ **G7c — Danh sách: dropdown button `Bulk Action`** trên nhiều dòng đã chọn — `Tải PDF khcn`, `Tải ảnh khcn`, `Tải PDF KQ`, `Tải ảnh KQ`. Zip + convert PDF→ảnh làm **ở client**; bytes PDF lấy qua **endpoint backend proxy** (link Drive không fetch được trực tiếp do CORS). Dòng chưa có PDF: bỏ qua kèm cảnh báo. Ảnh: định dạng + độ phân giải mặc định, 1 ảnh/trang. Tên file trong zip: `<tên action> + <timestamp>`. *(item 10.3 — đã chốt)*
11. ✅ **G8 — Upload Google Drive: [`ASH-FB-W1`]  tạo file mới trước, xóa file cũ theo ID đã lưu sau.** Link/ID đổi mỗi lần → cập nhật `PlanFileLinkPdf`/`ResultFileLinkPdf` mỗi lần, không cache link cũ. File cũ đã bị xóa tay trên Drive → bỏ qua lỗi `not found`. *(item 9 — đã chốt)*
12. ✅ **G9 — Thêm status `Canceled`.** [`ASH-FB-W1`] Chuyển sang `Canceled` từ trạng thái nào cũng được; từ `Canceled` chuyển đi về trạng thái nào cũng được (không ràng buộc, không lưu previous). `Canceled` chỉ là nhãn, **vẫn cho sửa**. Không loại khỏi thống kê / danh sách mặc định. `sync-assessments` mode 2 `sheetStatuses` **có** nhận `Canceled`. *(item 11 — đã chốt)*

## Truy vết 11 item gốc → nhóm

| Item gốc | Nội dung | Nhóm | Stack | Trạng thái chốt |
|---|---|---|---|---|
| 1 | Tạo mới không cần chọn assessment; pick chỉ ở edit | G1 | FE + BE | ✅ chốt |
| 2 | Nút add/xóa dòng dạng icon, bỏ confirm | G2 | FE | ✅ chốt |
| 3 | Thứ tự nhóm Lv2 cố định ở dropdown + default + mọi danh sách | G3 | FE | ✅ chốt (FE tự sắp; áp cả picker grid + trang DS Đánh giá) |
| 4 | Picker: đủ cột, ẩn `mã`/`rowIndex`, resize | G4 | FE | ✅ chốt |
| 5 | Default filter `Kết quả` = trừ `Đạt +` | G5 | FE | ✅ chốt |
| 6 | Bật header/row filter; Lv1/Lv2/Lv3 + `Kết quả` dạng dropdown | G5 | FE | ✅ chốt |
| 7 | Nút combo `Hoàn thành kế hoạch` | G6a | FE (+ BE status) | ✅ chốt |
| 8 | Nút `Tạo mới đánh giá` ở sticky bar edit | G6b | FE | ✅ chốt |
| 9 | Upload Drive: xóa + tạo mới thay vì replace | G8 | BE | ✅ chốt |
| 10.1 | List: lọc theo giáo viên phụ trách | G7a | BE + FE | ✅ chốt |
| 10.2 | List: select multi + paging remote server-side | G7b | FE (+ BE) | ✅ chốt |
| 10.3 | List: `Bulk Action` dropdown — tải PDF/ảnh khcn/KQ zip | G7c | FE (+ BE?) | ✅ chốt (rủi ro CORS mở) |
| 11 | Thêm status `Canceled` | G9 | BE + FE | ✅ chốt |

## Chi tiết từng nhóm

### G1 — Tạo mới AssessmentSheet không bắt buộc mục đánh giá *(item 1)*

- **Feedback:** Tạo master trước; panel pick mục đánh giá chỉ hiện lúc edit.
- **✅ Chốt 2026-08-31:**
  - `POST /assessment-sheets` chấp nhận `records: []` rỗng. Các ràng buộc khác (học sinh active, kỳ hợp lệ…) giữ nguyên.
  - Sau khi tạo rỗng, điều hướng thẳng vào màn edit của sheet vừa tạo.
  - Màn tạo (`ASH-FE-01`) bỏ hẳn bước chọn mục đánh giá; picker chỉ ở màn edit (đã có nút `Thêm mục đánh giá` từ `ASH-FE-07`).
- **Ảnh hưởng:** backend nới validate `records` (bỏ min ≥ 1 nếu đang có); FE tách form tạo/edit.

### G2 — Records panel: cặp icon thêm/xóa, bỏ confirm *(item 2)*

- **✅ Chốt 2026-08-31:**
  - Bỏ confirm cho **cả** thêm và xóa.
  - Dòng chưa có record → hiện icon `thêm`. Dòng đã có record → **ẩn** icon `thêm`, **hiện** icon `xóa`.
  - Thao tác vẫn lưu ngay qua endpoint full-replace records như hiện tại (không chỉ đánh dirty).
- **Đảo ngược:** yêu cầu confirm của `ASH-FE-07` (thêm) và `ASH-FE-08` (xóa).

### G3 — Thứ tự nhóm Lv2 cố định *(item 3)*

- **Feedback:** Thứ tự group Lv2 **luôn**: `Phát triển thể chất` → `Phát triển nhận thức` → `Phát triển ngôn ngữ` → `Cá nhân và xã hội` → `Tiền tiểu học`. Áp cho dropdown, thứ tự mặc định khi khởi tạo record, và mọi danh sách liên quan.
- **✅ Chốt 2026-08-31:**
  - `ASSESSMENT_GROUP_LV2_CONFIGS` ([`ui/src/app/core/models/api.models.assessment-sheets.ts:22`](../../../../ui/src/app/core/models/api.models.assessment-sheets.ts)) đã đúng `displayOrder` 1–5, **không sửa**.
  - **Không** đụng logic so khớp tên nhóm (`normalizeVietnamese` folding case + dấu, xử lý newline theo `ASH-SYNC-02`). Nếu khi thực thi phát sinh nhu cầu chỉnh → **phải hỏi người dùng trước**.
- **Rà 9 điểm dùng/thiếu thứ tự nhóm Lv2 — đã chốt hết:**

  | # | Chỗ | Hiện tại | Kết luận (chốt 2026-08-31) |
  |---|---|---|---|
  | 1 | `assessment-sheets-form.component.ts` — bảng records màn edit (`GROUP_LV2_DISPLAY_ORDER_INDEX`) | ✅ Đã theo thứ tự cố định 1–5; người dùng vẫn kéo `groupLv2Order` đổi sau | Giữ nguyên |
  | 2 | `assessment-sheet-plan-preview.models.ts` — PDF kế hoạch/kết quả (`buildAssessmentSheetRecordRows`) | ✅ Dùng chung hàm điểm 1 | Giữ nguyên |
  | 3 | `assessment-picker.component.ts` `buildGroupOptions` — dropdown lọc **Nhóm 2** trong picker | ❌ Sắp alphabet `localeCompare('vi')` | **Đổi** sang thứ tự cố định |
  | 4 | `assessment-picker.component.ts` — grid mục đánh giá | Sort theo `rowindex` catalog | **Đổi**: nhóm/sắp theo Lv2 cố định (FE sắp sau khi nhận) |
  | 5 | `assessments.component.ts` ("DS Đánh giá" — danh mục) — dropdown lọc **Nhóm 2** (API `groups.list` `sortBy:'name'`) | ❌ Sắp alphabet phía server | **Đổi**: FE nhận rồi tự sắp lại theo thứ tự cố định |
  | 6 | `assessments.component.ts` — grid danh mục | Sort theo cột người dùng chọn | **Đổi**: default sort theo Lv2 cố định (người dùng vẫn đổi sort được) |
  | 7 | Backend `AssessmentGroupService.ListAsync` (`GET /assessment-groups`) | ❌ Không có khái niệm "thứ tự 5 nhóm chuẩn" | **Không đụng backend** — FE tự sắp sau khi nhận |
  | 8 | `google-sheets-sync-dialog.component.ts` | Chỉ là checkbox cờ replace-field | Không liên quan |
  | 9 | Dropdown lọc **Nhóm Lv2** mới ở G5 (header/row filter của picker grid) | Chưa có | **Cần**: item Lv2 theo thứ tự cố định |

- **Lv1 / Lv3:** không có danh sách chuẩn cố định → dropdown Lv1 (Nhóm tuổi) và Lv3 **giữ nguyên** (alphabet / `rowIndex`).

### G4 — Dialog chọn mục đánh giá (picker) *(item 4)*

- **✅ Chốt 2026-08-31:**
  - "Đủ cột" = đúng 6 cột: **Nhóm tuổi** (`groupLv1Name`), **Nhóm Lv2** (`groupLv2Name`), **Nhóm Lv3** (`groupLv3Name`), **Nội dung** (`name`), **Kết quả gần nhất** (`latestGrade`), **Ghi chú gần nhất** (`latestNote`).
  - Ẩn cột `mã` (`code`) và `rowIndex`.
  - `allowColumnResizing = true`, `columnResizingMode = "widget"`. Chỉnh lại width mặc định cho hợp 6 cột.

### G5 — Bảng picker: 2 vòng lọc *(item 5, 6)*

- **✅ Chốt 2026-08-31:**
  - Phạm vi: **bảng picker mục đánh giá** (không phải danh sách assessment sheets).
  - **Vòng 1** = panel filter hiện có ở đầu picker (search, dropdown nhóm, TagBox `Kết quả gần nhất` của `ASH-FE-05`…). Giữ nguyên.
  - **Vòng 2** = bật `headerFilter` (`allowSelectAll = true`) **và** filter row của datagrid, chạy trên kết quả sau vòng 1.
  - **G5a — mặc định:** filter `Kết quả` mặc định chọn **tất cả trừ `Đạt +`**, bao gồm `Chưa có`. **Không** cần đồng bộ với default của TagBox `ASH-FE-05` (2 chỗ độc lập).
  - **G5b — kiểu dropdown:** các cột `Nhóm Lv1` / `Nhóm Lv2` / `Nhóm Lv3` / `Kết quả` dùng bộ lọc kiểu select. Nguồn item = cơ chế header filter tự sinh của DevExtreme (distinct từ dữ liệu đang hiển thị). Riêng thứ tự item `Nhóm Lv2` xem G3 điểm 9.

### G6 — Thanh sticky màn edit *(item 7, 8)*

#### G6a — Nút combo `Hoàn thành kế hoạch` *(item 7)*

- **✅ Chốt 2026-08-31:**
  - Hiển thị/enable khi status = `Open`.
  - Coi như **auto action** — chạy tuần tự đúng như người dùng bấm tay từng bước, **không** chạy nền, **không** ẩn UI:
    1. Lưu thay đổi hiện tại (đang `Open`).
    2. Chuyển status → `Planed`.
    3. Lưu ở `Planed`.
    4. Tạo PDF kế hoạch + upload Google Drive (luồng `upload-plan-pdf` của `ASH-FE-11`).
  - Lỗi ở bước nào thì **dừng ngay ở bước đó**, để nguyên trạng thái đã đạt (giống thao tác thủ công gặp lỗi), báo lỗi cho người dùng. Không rollback.
  - **Không** confirm trước khi chạy combo.

#### G6b — Nút `Tạo mới đánh giá` ở sticky bar *(item 8)*

- **✅ Chốt 2026-08-31:** Mở form **tạo trống**. Nếu sheet đang mở còn thay đổi chưa lưu → nhắc (guard dirty) trước khi rời đi.

### G7 — Danh sách assessment sheets *(item 10.1, 10.2, 10.3)*

#### G7a — Lọc theo giáo viên phụ trách *(item 10.1)*

- **✅ Chốt 2026-08-31:** Nguồn dropdown giáo viên = `GET /teachers`. Role `Teacher` **không** bị khóa filter (vẫn tự đổi được).
- **Ảnh hưởng:** `GET /assessment-sheets` thêm param filter giáo viên phụ trách (vd `responsibleTeacherId`).

#### G7b — Select multi + paging remote server-side *(item 10.2)*

- **✅ Chốt 2026-08-31:** Datagrid danh sách bật selection `multiple`, paging remote server-side. `selectAllMode` **không** giữ selection qua các trang (chọn lại theo từng trang).
- **Ảnh hưởng:** xác nhận `GET /assessment-sheets` đủ server sort/filter cho các cột hiển thị; bổ sung whitelist nếu thiếu.

#### G7c — Dropdown button `Bulk Action` *(item 10.3)*

- **✅ Chốt 2026-08-31:**
  - 4 action trên các dòng đã chọn: `Tải PDF khcn` (kế hoạch), `Tải ảnh khcn`, `Tải PDF KQ` (kết quả), `Tải ảnh KQ`.
  - `Tải PDF *` = gom file PDF các dòng đã chọn → zip → download.
  - `Tải ảnh *` = lấy PDF các dòng đã chọn → chuyển thành ảnh → zip → download.
  - Zip + convert PDF→ảnh làm **ở client** (vd JSZip + pdf.js).
  - Dòng chưa có PDF (`planFileLinkPdf` / `resultFileLinkPdf` null) → **bỏ qua kèm cảnh báo**, không chặn action.
  - Ảnh: định dạng + độ phân giải **mặc định** (đề xuất PNG, scale mặc định pdf.js), **1 ảnh / trang**.
  - Tên file trong zip: `<tên action> + <timestamp>` (vd `Tai-PDF-khcn_20260831-1530.zip`, từng file bên trong theo mã/nick + trang).
- **✅ Chốt CORS 2026-08-31:** `planFileLinkPdf` / `resultFileLinkPdf` là link Google Drive, client fetch trực tiếp thường bị CORS / yêu cầu đăng nhập Drive. → **Thêm endpoint backend proxy stream bytes PDF** (backend dùng credential Drive có sẵn tải file rồi trả về cho client). Client vẫn lo phần zip + convert PDF→ảnh. Endpoint mới cần: nhận danh sách sheet id + loại (`plan`/`result`), kiểm quyền như `GET /assessment-sheets/{id}`, trả từng file bytes (hoặc stream nhiều file) — chi tiết contract chốt ở task con.

### G8 — Cơ chế upload Google Drive: xóa + tạo mới *(item 9)*

- **✅ Chốt 2026-08-31:**
  - Thứ tự an toàn: **tạo file mới thành công trước, rồi mới xóa file cũ** (tránh mất cả hai khi lỗi giữa chừng).
  - Xóa file cũ **theo file ID đã lưu**. File cũ đã bị xóa tay trên Drive → bỏ qua lỗi `not found`, coi như thành công.
  - File ID + link đổi sau mỗi lần upload → cập nhật `PlanFileLinkPdf` / `ResultFileLinkPdf` mỗi lần, không cache link cũ.
- **Ảnh hưởng:** `GoogleSheetsService` / Drive helper của các endpoint `upload-plan-pdf`, `upload-result-pdf` (`ASH-FE-11`/`ASH-FE-12`) và mọi luồng upload Drive khác trong epic.

### G9 — Thêm status `Canceled` *(item 11)*

- **✅ Chốt 2026-08-31:**
  - Chuyển sang `Canceled` được **từ bất kỳ trạng thái nào** (`Open` / `Planed` / `Done`).
  - Từ `Canceled` chuyển đi được **về bất kỳ trạng thái nào** (`Open` / `Planed` / `Done`) — người dùng tự chọn, không ràng buộc, không cần lưu previous status.
  - `Canceled` **chỉ là nhãn phân loại**, không side-effect: **vẫn cho sửa** records/grade/note như `Open`.
  - **Không** loại khỏi thống kê / danh sách mặc định (không auto-ẩn).
  - `sync-assessments` mode 2 `sheetStatuses` (`ASH-SYNC-01`) **có** nhận `Canceled`.
- **Ảnh hưởng:**
  - Backend: enum `AssessmentSheetStatus` thêm `Canceled` (lưu dạng string → **không cần migration schema**, cần xác nhận `HasConversion<string>()`), nới rule chuyển trạng thái, thêm `Canceled` vào whitelist `sheetStatuses`.
  - FE: `AssessmentSheetStatus` type + `ASSESSMENT_SHEET_STATUS_OPTIONS` (đề xuất nhãn `Đã hủy`), dropdown status màn edit (`ASH-FE-06`), filter danh sách.

## Câu hỏi còn mở (tổng hợp)

Không còn — 6 câu hỏi round 2 đã được người dùng trả lời (2026-08-31):

1. **G3 điểm 4** — Grid picker mục đánh giá: **có** nhóm/sắp theo Lv2 cố định.
2. **G3 điểm 5 + 6** — Trang "DS Đánh giá": **có** trong phạm vi — dropdown Nhóm 2 + default sort grid theo thứ tự cố định.
3. **G3 điểm 7** — **FE tự sắp** sau khi nhận; không đụng backend `GET /assessment-groups`.
4. **G3 phụ** — Dropdown Lv1 và Lv3 **giữ nguyên** (không có danh sách chuẩn).
5. **G7c CORS** — **Có**: thêm endpoint backend proxy stream PDF; client vẫn lo zip + PDF→ảnh.
6. **G9** — Từ `Canceled` chuyển đi về **bất kỳ trạng thái nào**, không lưu previous status.

## Thứ tự đề xuất thực thi

Phụ thuộc thực tế: G1→G6b; G7b→G7c; G3/G4/G5 dùng chung file picker nên gộp 1 đợt. Còn lại độc lập.

### Đợt 1 — làm được ngay, độc lập, rủi ro thấp

| Ưu tiên | Nhóm | Stack | Ghi chú |
|---|---|---|---|
| 1 | **G2** | FE | Nhỏ nhất. Đổi cặp icon thêm/xóa + bỏ confirm. Không đụng contract. |
| 2 | **G9** | BE + FE | Verify `AssessmentSheetStatus` lưu string (không migration) rồi thêm `Canceled` vào enum/options/dropdown/filter + `sheetStatuses` whitelist. |
| 3 | **G8** | BE | Chỉ Drive helper của `upload-plan-pdf`/`upload-result-pdf`: tạo mới trước, xóa cũ theo ID sau. Gọn, độc lập. |
| 4 | **G1** | BE + FE | BE nới validate cho `records: []`; FE tách form tạo (bỏ picker) + điều hướng vào edit. Mở khóa G6b. |

### Đợt 2 — đại tu bảng picker (gộp chung 1 lượt để tránh xung đột file)

| Ưu tiên | Nhóm | Stack | Ghi chú |
|---|---|---|---|
| 5 | **G3 + G4 + G5** | FE | Cùng `assessment-picker.component.*` + `assessments.component.*`. G3 sắp thứ tự Lv2 cố định (FE tự sắp) ở picker grid + trang "DS Đánh giá"; G4 cấu hình 6 cột + resize `widget`; G5 bật header/row filter + default `Kết quả` trừ `Đạt +`. |

### Đợt 3 — phụ thuộc Đợt 1

| Ưu tiên | Nhóm | Stack | Ghi chú |
|---|---|---|---|
| 6 | **G6b** | FE | Sau G1 (cần flow "tạo trống"). Nút sticky + guard dirty. |
| 7 | **G6a** | FE | Combo `Hoàn thành kế hoạch`. Độc lập nhưng phức tạp nhất của phần edit (chuỗi lưu→đổi status→lưu→PDF, dừng khi lỗi). Nên sau G8 (upload đã đúng) và G9 (enum status ổn định). |

### Đợt 4 — danh sách + bulk

**Phụ thuộc đợt trước: KHÔNG.** Đợt 4 không phụ thuộc Đợt 1/2/3. Tiền đề hạ tầng đã sẵn:

- Danh sách (`assessment-sheets.component.ts`) đã dùng `CustomStore` **paging/sort/filter server-side** rồi (`page`/`pageSize`/`sortBy`/`sortOrder` + `search`/`studentId`/`status`/`dateFrom`/`dateTo`). → G7b chỉ còn thêm `selection.mode = 'multiple'` + toolbar, **không** phải dựng lại remote paging.
- `TeachersService.list()` đã có và đang dùng ở form edit (`assessment-sheets-form.component.ts:426`). → G7a tái dùng, không cần hạ tầng mới.
- `planFileLinkPdf`/`resultFileLinkPdf` đã có sẵn trên `AssessmentSheet`. → G7c chỉ đọc field này.

**Phụ thuộc nội bộ trong đợt:**

| Ưu tiên | Nhóm | Stack | Phụ thuộc | Ghi chú |
|---|---|---|---|---|
| 8 | **G7a** | BE + FE | — (độc lập) | BE: thêm `ResponsibleTeacherId` vào `AssessmentSheetListQuery` + `.Where(...)` + whitelist `ApplySheetSort` nếu cần cột GV. FE: dropdown bind `teachers.list`, truyền param. Chạy song song với G7b được. |
| 9 | **G7b** | FE (BE chỉ verify) | — (độc lập) | Chỉ bật `selection.mode='multiple'` + `selectAllMode` không giữ qua trang. **Tiền đề cứng của G7c.** |
| 10 | **G7c** | BE + FE | **G7b** (cần multi-select) | Lớn/rủi ro nhất: endpoint backend proxy stream bytes PDF (mới) + FE thêm lib `JSZip` + `pdf.js` + dropdown `Bulk Action` + convert PDF→ảnh. Làm cuối. |

**Lưu ý chồng file (không phải phụ thuộc):** G1, G7a, G7b, G9 đều đụng `assessment-sheets.component.{ts,html}` (G9 + G7a: vùng filter dropdown; G1: `openCreate()`). Nếu làm song song cần merge thủ công vùng nhỏ, không có ràng buộc thứ tự.

## Việc tiếp theo (chưa làm)

- ⬜ Tách task con theo nhóm (mã dự kiến `ASH-FB-01a..i` hoặc mã chức năng riêng); cập nhật `status.md` + `log.md`.
- ⬜ Đồng bộ `requirements/09` + `plans/07-ASH` cho từng nhóm khi bắt đầu task con.
- ⬜ Cập nhật `.agents/shared/MEMORY.md` khi có thay đổi contract thực sự: G1 `records: []` rỗng, G7a filter param `responsibleTeacherId` trên `GET /assessment-sheets`, G7c endpoint proxy PDF mới, G8 Drive delete+recreate, G9 enum `AssessmentSheetStatus` thêm `Canceled` + `sheetStatuses` whitelist.
- ⬜ Xác nhận backend `AssessmentSheetStatus` lưu dạng string (`HasConversion<string>()`) để khẳng định G9 không cần migration schema.

## DoD của riêng bản ghi nhận này

- ✅ 11 item gốc gom thành 9 nhóm, mỗi item truy vết về nhóm, không lặp ý.
- ✅ Mỗi nhóm ghi phương án đã chốt (2 vòng hỏi–đáp, 2026-08-31) + chồng lấn với task ASH hiện có.
- ✅ G3 đã rà toàn bộ 9 điểm dùng thứ tự nhóm Lv2 trong `ui/` + backend; mỗi điểm có kết luận rõ (giữ nguyên / đổi / không liên quan).
- ✅ Không còn câu hỏi mở.
- ✅ Đăng ký ở `status.md` (mục "Feedback batch") + bullet `log.md`.
- ⬜ Chưa sửa source, chưa tách task con, chưa đồng bộ requirements/plan — cố ý, chờ lệnh thực thi.
