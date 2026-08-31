# Theo dõi tiến độ — ASH: Bảng đánh giá năng lực

Nguồn: [`../../plans/07-ASH-assessment-sheet.md`](../../plans/07-ASH-assessment-sheet.md) và [`../../requirements/09-bang-danh-gia-nang-luc.md`](../../requirements/09-bang-danh-gia-nang-luc.md).

File này là dashboard: chỉ liệt kê trạng thái tổng quan. Mỗi mã task bấm vào để mở file chi tiết trong [`details/`](details/) — nơi ghi Mục đích, Nội dung cụ thể cần làm và Kết quả mong đợi (DoD) của riêng task đó. Lịch sử thực hiện (log) không nằm trong từng file chi tiết mà gom chung tại [`log.md`](log.md), theo trình tự thời gian thật.

Theo dõi toàn dự án nằm ở [`../README.md`](../README.md). Khi một mục ở đây đổi trạng thái, cập nhật chính file này và thêm log tương ứng vào `log.md`; không ghi vào `tasks.md` ở root vì file đó đã legacy/frozen.

## Quy ước trạng thái

- `[ ]` Chưa bắt đầu
- `[~]` Đang thực hiện
- `[x]` Hoàn thành và đã kiểm tra
- `[!]` Bị chặn — ghi rõ nguyên nhân và hướng xử lý trong file chi tiết của task

Không đánh dấu `[x]` nếu chưa chạy kiểm tra tương ứng (build/test/smoke theo mục 9 của plan). Mỗi agent chỉ tự cập nhật task mình phụ trách; thay đổi dependency/scope chung do root cập nhật. Đổi Status ở đây thì đồng thời thêm một bullet log tương ứng vào [`log.md`](log.md).

## Quyết định kỹ thuật cần khoá trước `ASH-BE-00`

Theo mục 13 của plan. Cả 5 quyết định đã được khoá. `ASH-DEC-03`/`04`/`05` **đã áp dụng thật vào code** ở `ASH-BE-00`/`ASH-BE-01` (2026-08-20). `ASH-DEC-02` đã áp dụng phần mở quyền endpoint `sync-assessments`; phần upsert latest vẫn chờ `ASH-BE-03`. `ASH-DEC-01` vẫn chờ `ASH-BE-04`.

| Status | Mã | Quyết định | Chi tiết |
|---|---|---|---|
| `[x]` Legacy/removed | `ASH-DEC-01` | Cách sinh PDF `[F02]`/`[F03]` | Quyết định cũ từng sinh PDF từ sheet trong `[F01]`; hiện đã thay bằng UI preview/html2pdf + upload Drive. |
| `[x]` **Đã code phần quyền** | `ASH-DEC-02` | Cách mở quyền `Teacher` cho `sync-assessments` | Đã bỏ policy `PortalManagers` khỏi endpoint và kiểm tra role `Teacher`/`Admin`/`SuperAdmin` trong handler service. Phần nạp `AssessmentSheetLatest`/`AssessmentRecordLatest` vẫn chờ `ASH-BE-03`. |
| `[x]` **Đã code** | `ASH-DEC-03` | Giữ/bỏ field `ClosedDate` | Đã bỏ trên cả `AssessmentSheet` và `AssessmentSheetLatest`; migration không tạo cột `closed_date`. |
| `[x]` Legacy/removed | `ASH-DEC-04` | Spreadsheet nguồn cho `[F01]` | Không còn tạo Google Sheet riêng `[F01]`; settings template/gid đã gỡ khỏi config hiện hành. |
| `[x]` **Đã code** | `ASH-DEC-05` | Khoá upsert khi đồng bộ `AssessmentSheetLatest`/`AssessmentRecordLatest` | Migration hiện hành tạo unique index `StudentId` (sheet) và (`AssessmentSheetLatestId`, `AssessmentId`) (record). Bản trung gian từng dùng `AssessmentCode` đã được thay bằng FK `AssessmentId`; các log cũ nhắc `AssessmentCode` chỉ còn giá trị lịch sử. |

## Tổng quan

| Giai đoạn | Tổng số việc | Chưa bắt đầu | Đang làm | Hoàn thành | Bị chặn |
|---|---:|---:|---:|---:|---:|
| Planning | 1 | 0 | 0 | 1 | 0 |
| Backend | 6 | 0 | 3 | 3 | 0 |
| Backend delta | 1 | 0 | 0 | 1 | 0 |
| Frontend | 6 | 5 | 0 | 1 | 0 |
| Frontend delta | 9 | 0 | 4 | 5 | 0 |
| Contract delta | 2 | 0 | 0 | 2 | 0 |
| Cleanup delta | 1 | 0 | 0 | 1 | 0 |
| Import delta | 2 | 0 | 1 | 1 | 0 |
| Group editing delta | 1 | 1 | 0 | 0 | 0 |
| Feedback batch | 3 | 1 | 0 | 2 | 0 |
| QA | 1 | 1 | 0 | 0 | 0 |

Cập nhật bảng này mỗi khi đổi trạng thái một dòng bên dưới.

## Planning — owner: `root` (orchestrator)

| Status | Mã | Việc cần làm | Phụ thuộc |
|---|---|---|---|
| `[x]` | [`ASH-P-01`](details/01-ASH-P-01.md) | Đối chiếu source hiện có với requirements 09; khoá field/contract và 5 quyết định kỹ thuật | — |

## Backend — owner: `backend`

| Status | Mã | Việc cần làm | Phụ thuộc |
|---|---|---|---|
| `[x]` | [`ASH-BE-00`](details/02-ASH-BE-00.md) | Khoá contract DTO/enum/API surface (`PlanGrade`/`PlanNote`/`FinalGrade`/`FinalNote`, không phải 1 field `Grade`) | `ASH-P-01` |
| `[x]` | [`ASH-BE-01`](details/03-ASH-BE-01.md) | Domain/EF configuration/migration (gồm `AssessmentSheetLatest`/`AssessmentRecordLatest`) | `ASH-BE-00` |
| `[x]` | [`ASH-BE-02`](details/04-ASH-BE-02.md) | `AssessmentSheetService` (CRUD, plan filter, `Open`/`Done`) | `ASH-BE-01` |
| `[~]` | [`ASH-BE-03`](details/05-ASH-BE-03.md) | Mở rộng `GoogleSheetsService` cho sync nguồn `[F0]`, ghi `[F0.ĐG]`, nạp latest; phần copy/ghi `[F01]` trong detail cũ là lịch sử và đã được cleanup ở `ASH-CL-01` | `ASH-BE-02` |
| `[~]` | [`ASH-BE-04`](details/06-ASH-BE-04.md) | Legacy sau cleanup: backend không còn sinh PDF từ Google Sheet; PDF hiện do UI render/upload Drive (`ASH-FE-11/12`) | `ASH-BE-03` |
| `[~]` | [`ASH-BE-05`](details/07-ASH-BE-05.md) | Test, README/`requests.http`, default gate, smoke phần backend | `ASH-BE-04` |

### Backend delta — owner: `backend`

| Status | Mã | Việc cần làm | Phụ thuộc |
|---|---|---|---|
| `[x]` | [`ASH-BE-06`](details/21-ASH-BE-06.md) | Đã bổ sung/chuẩn hoá AuditLog cho đồng bộ Google Sheet và upload Drive kế hoạch/kết quả; không lưu bytes/secret trong audit. Automated gate pass; chưa gọi Google thật cho sync/upload | `ASH-FE-11`, `ASH-FE-12` |

## Frontend — owner: `frontend`

| Status | Mã | Việc cần làm | Phụ thuộc |
|---|---|---|---|
| `[ ]` | [`ASH-FE-00`](details/08-ASH-FE-00.md) | Khoá contract cùng backend trước khi code UI | `ASH-BE-00` |
| `[ ]` | [`ASH-FE-01`](details/09-ASH-FE-01.md) | Danh sách + tạo `AssessmentSheet`, chọn plan có filter | `ASH-FE-00`, `ASH-BE-02` |
| `[ ]` | [`ASH-FE-02`](details/10-ASH-FE-02.md) | Form chi tiết: sửa plan/`PlanGrade`; nút Xuất sang Google Sheet/Đồng bộ `[F01]` trong detail cũ là legacy và không còn dùng | `ASH-FE-01`, `ASH-BE-03` |
| `[ ]` | [`ASH-FE-03`](details/11-ASH-FE-03.md) | Nhập `FinalGrade`, sinh PDF, cập nhật `[F0.ĐG]`, chuyển `Open`/`Done` | `ASH-FE-02`, `ASH-BE-04` |
| `[ ]` | [`ASH-FE-04`](details/12-ASH-FE-04.md) | Build/`test:ci` mặc định, docs/memory, smoke phần frontend | `ASH-FE-03` |

### Frontend delta — owner: `frontend`

| Status | Mã | Việc cần làm | Phụ thuộc |
|---|---|---|---|
| `[x]` | `ASH-FE-05` | Picker thêm TagBox lọc `Kết quả gần nhất` ở đầu panel filter, gồm lựa chọn `Chưa có`; filter chạy local trên snapshot `viewMode` | `ASM-LST-03` |
| `[x]` | `ASH-FE-06` | Sửa dropdown Trạng thái trên màn edit: create vẫn readonly, edit cho phép đổi `Open`/`Planed`/`Done` nhưng giữ `editorOptions` ổn định để tránh reload loop DevExtreme 19 | `ASH-CR-01` |
| `[x]` | [`ASH-FE-07`](details/14-ASH-FE-07.md) | Màn edit có nút `Thêm mục đánh giá`, mở picker ở chế độ thêm từng dòng với confirm; dòng đã có record vẫn hiển thị nhưng không cho thêm lại; lưu qua endpoint full-replace records hiện có | `ASH-FE-06`, `PUT /assessment-sheets/{id}/records` |
| `[x]` | [`ASH-FE-08`](details/15-ASH-FE-08.md) | Màn edit có nút xóa từng dòng trong `records-panel`, confirm trước khi xóa và lưu qua endpoint full-replace records hiện có | `ASH-FE-07`, `PUT /assessment-sheets/{id}/records` |
| `[~]` | [`ASH-FE-09`](details/16-ASH-FE-09.md) | Chuyển `records-panel` từ dạng card sang table, cột xóa nằm đầu, hiển thị nhóm `groupLv2`/`groupLv3` và màu nền cố định theo 5 nhóm `groupLv2` | `ASH-FE-08` |
| `[~]` | [`ASH-FE-10`](details/17-ASH-FE-10.md) | Đổi ghi chú từng dòng sang `dxTextArea`; `Open` cho thao tác đầy đủ, `Planed` chỉ khóa thêm/xóa record nhưng vẫn cho nhập `FinalGrade`/`FinalNote`, `Done` giữ khóa như hiện tại | `ASH-FE-09` |
| `[~]` | [`ASH-FE-11`](details/18-ASH-FE-11.md) | Đã code nút `In Kế hoạch PDF`, trang preview A4 (co 1 trang tự động), mở blob PDF bằng `html2pdf.js`, endpoint upload PDF do UI tạo lên Google Drive học viên, show/hide cột Kế hoạch/Kết quả + khoá nút PDF theo `status`, tên file PDF đổi sang cú pháp `khcn - <code>.<nick>_<assessmentName>.pdf`; `npm --prefix ui run test:ci` 114/114, build dev pass; smoke thủ công và kiểm tra lưu Drive thật vẫn chừa lại theo yêu cầu | `ASH-FE-10` |
| `[~]` | [`ASH-FE-12`](details/19-ASH-FE-12.md) | Đã code nút `In Kết Quả PDF`, chỉ enable khi `status != Open`, route preview kết quả dùng `FinalGrade`/`FinalNote`, mở blob PDF và upload Google Drive vào `ResultFileLinkPdf`; backend endpoint `upload-result-pdf` có integration coverage; backend build/unit/integration pass, frontend `test:ci` 116/116 và build dev pass; smoke thủ công và kiểm tra Drive thật chưa chạy | `ASH-FE-11` |
| `[x]` | [`ASH-FE-13`](details/23-ASH-FE-13.md) | Chỉ tự động fill `PlanGrade`/`PlanNote`; không tự động fill `FinalGrade`/`FinalNote` từ kế hoạch/latest khi mở edit, thêm record hoặc lưu records. Frontend `test:ci` 118/118 và build dev pass | `ASH-FE-10`, `ASH-CR-01` |

## Contract delta — owner: `root` / phối hợp backend + frontend

| Status | Mã | Việc cần làm | Phụ thuộc |
|---|---|---|---|
| `[x]` | `ASH-CR-01` | `POST /assessment-sheets` đổi payload tạo mới từ `assessmentIds[]` sang `records[]` gồm `assessmentId`, `latestGrade`, `note`; backend lưu vào `PlanGrade`/`PlanNote`, UI picker gửi dữ liệu latest đang hiển thị | `ASH-FE-05`, latest contract `/assessments?studentId=...` |
| `[x]` | [`ASH-CR-02`](details/20-ASH-CR-02.md) | Đã thêm nút `Cập nhật Kết Quả` gọi `submit-results`; UI chỉ hiện khi sheet `Done` và disable cho role `Teacher`, còn backend không chặn riêng Teacher. Backend ghi ResultSource chỉ với cell có thay đổi và audit từng cell được ghi; `FinalNote` ghi vào cột kế bên phải cột kết quả của học sinh; automated gate pass, chưa smoke Google Sheet thật | `ASH-FE-10`, `ASH-BE-03` |

## Cleanup delta — owner: `root` / phối hợp backend + frontend

| Status | Mã | Việc cần làm | Phụ thuộc |
|---|---|---|---|
| `[x]` | [`ASH-CL-01`](details/22-ASH-CL-01.md) | Cleanup luồng Google Sheet riêng `[F01]`: gỡ endpoint/service/config/model/docs cũ; giữ `AssessmentSheetSpreadsheetId` là legacy DB-only. Backend build/unit/integration và UI test/build dev pass; chưa gọi Google thật | `ASH-FE-11`, `ASH-FE-12`, `ASH-CR-02` |

## Import delta — owner: `root` / phối hợp backend + frontend

| Status | Mã | Việc cần làm | Phụ thuộc |
|---|---|---|---|
| `[x]` | [`ASH-IMP-01`](details/24-ASH-IMP-01.md) | Import `AssessmentSheet` + `AssessmentRecord` từ Excel mẫu `import_khcn.xlsx`; backend đọc bằng `ExcelDataReader`, preview validate trước khi ghi DB, frontend thêm popup datagrid xác nhận import. Automated gate pass; chưa smoke browser thủ công với file mẫu thật | `ASH-FE-13`, `ASH-BE-02` |
| `[~]` | [`ASH-IMP-02`](details/26-ASH-IMP-02.md) | File import thêm cột tùy chọn `STT` → `DisplayOrder` (số chạy toàn cục theo thứ tự dòng), `groupLv2Name`/`groupLv3Name` fill-down kiểu ô merge → `AssessmentSnapshot`; `PUT .../records` giữ tên nhóm snapshot; preview + popup thêm 3 cột. Backend build + unit pass; integration chưa chạy (Docker daemon off), frontend `test:ci`/build chưa chạy (ổ C: đầy) | `ASH-IMP-01`, `ASH-STT-01` |

## Group editing delta — owner: `root` / phối hợp backend + frontend

| Status | Mã | Việc cần làm | Phụ thuộc |
|---|---|---|---|
| `[x]` | [`ASH-GRP-01`](details/25-ASH-GRP-01.md) | Điều chỉnh 2026-08-28: popup `Áp dụng` chỉ đổi state UI + đánh dấu dirty; lưu snapshot khi bấm `Lưu thay đổi` (gộp `groupLv2Name`/`groupLv3Name` vào `PUT .../records`). Mỗi ô merge có nút hoàn tác về giá trị lúc tải. `Cập nhật Assessment gốc` tách thành nút riêng gọi `PATCH /api/v1/assessments/group` (Admin/SuperAdmin). Đã gỡ `PATCH /assessment-sheets/{id}/record-group`. Backend unit 95/95 + Release build 0/0; frontend 132/132 + dev build pass; integration project build pass, suite chưa chạy (Docker không sẵn) | `ASH-FE-09`, `PUT /assessment-sheets/{id}/records` |

## Google Sheets sync delta — owner: `root` / phối hợp backend + frontend

| Status | Mã | Việc cần làm | Phụ thuộc |
|---|---|---|---|
| `[~]` | [`ASH-SYNC-01`](details/27-ASH-SYNC-01.md) | Popup "Đồng bộ GGSheet" thêm 2 chế độ: (1) mặc định như cũ; (2) chỉ `Admin`/`SuperAdmin` — sau rebuild catalog, ghi đè các trường đã chọn (`Name`/`Lv1`/`Lv2`/`Lv3`/`RowIndex`) vào `AssessmentRecord.AssessmentSnapshot`, giới hạn theo trạng thái sheet đã chọn. `Teacher` disable UI + backend `403`; thiếu trường/status → `400`. Popup là component `app-google-sheets-sync-dialog`; **chỉ để 1 nút ở màn DS Đánh giá** (`pages/assessments`), đã gỡ nút đồng bộ khỏi màn danh sách bảng đánh giá và picker chọn mục. Backend build + unit 101/101; frontend `test:ci` 142/142 + dev build pass; integration suite không chạy (luồng cần Google live). Smoke thủ công chưa chạy | `ASH-GRP-01`, `ASH-IMP-02`, `POST /google-sheets/sync-assessments` |

| `[~]` | [`ASH-SYNC-02`](details/28-ASH-SYNC-02.md) | Đảo quyết định "một dòng" (2026-08-28): `Assessment.Name`/`GroupLv1/2/3Name` giữ **nguyên xuống dòng** từ sync tới `AssessmentSnapshot`. `AssessmentSyncTextNormalizer` giờ chỉ `Trim()` hai đầu. Các đường ghi khác đã trim-only, không cần sửa. Backend build (Application) 0/0 + unit 102/102; integration không chạy (Google live). UI rendering newline ngoài phạm vi | `ASH-SYNC-01` |

## Feedback batch — owner: `root` (điều phối; tách task con sau khi chốt)

| Status | Mã | Việc cần làm | Phụ thuộc |
|---|---|---|---|
| `[ ]` | [`ASH-FB-01`](details/29-ASH-FB-01.md) | Ghi nhận batch feedback 2026-08-31: 11 item gom thành 9 nhóm — G1 tạo mới không bắt buộc mục đánh giá (`records: []` rỗng → điều hướng vào edit); G2 records panel cặp icon thêm/xóa bỏ confirm, đã có record thì ẩn `thêm` hiện `xóa` (đảo `ASH-FE-07`/`08`); G3 thứ tự nhóm Lv2 cố định — `ASSESSMENT_GROUP_LV2_CONFIGS` giữ nguyên, đã rà 9 điểm dùng, còn 5 điểm chờ confirm; G4 picker 6 cột/ẩn mã+rowIndex/resize `widget`; G5 bảng picker bật header+row filter, default `Kết quả` = trừ `Đạt +`; G6a sticky bar nút combo `Hoàn thành kế hoạch` (auto action, lỗi thì dừng), G6b nút `Tạo mới đánh giá` (form trống, guard dirty); G7a lọc theo GV (`GET /teachers`, Teacher không khóa), G7b select multi server-side không giữ qua trang, G7c `Bulk Action` zip client-side (rủi ro CORS link Drive mở); G8 upload Drive tạo mới trước xóa cũ theo ID sau; G9 status `Canceled` — chuyển từ mọi trạng thái, về `Open` được, chỉ là nhãn vẫn cho sửa, sync mode 2 nhận `Canceled`. Đã chốt xong 2 vòng hỏi–đáp 2026-08-31 (G3 áp cả grid picker + trang "DS Đánh giá", FE tự sắp; G7c thêm endpoint backend proxy stream PDF; G9 từ `Canceled` về trạng thái nào cũng được). Không còn câu hỏi mở. Thứ tự thực thi: Đợt 1 → **Đợt 4 → Đợt 2 → Đợt 3** (người dùng đổi 2026-08-31) | — |
| `[x]` | [`ASH-FB-W1`](details/30-ASH-FB-W1.md) | **Đợt 1** feedback batch (G2+G9+G8+G1). G2: picker add-mode dòng đã-có hiện nút xóa (`assessmentRemove`), form bỏ mọi `confirm()` thêm/xóa, cho xóa tới rỗng. G9: `AssessmentSheetStatus` += `Canceled` (string, không migration) — nhãn phân loại, không transition rule, sửa được như `Open`, sync mode-2 nhận `Canceled`. G8: `SavePdfToDriveAsync` bỏ `Files.Update` → tạo file mới + xóa file cũ theo id (404 bỏ qua). G1: `POST /assessment-sheets` nhận `records: []`, UI create bỏ picker + điều hướng edit. BE build Release 0/0 + unit 102/102 + `has-pending-model-changes` none; FE `test:ci` 149/149 + dev build `f3c8bc68`. Integration not run (Docker off). Commit `9155e54` (fixup annotation `MinLength(1)` gộp vào `ASH-FB-W4`) | `ASH-FB-01` |
| `[x]` | [`ASH-FB-W4`](details/31-ASH-FB-W4.md) | **Đợt 4** feedback batch (G7a+G7b+G7c) — màn danh sách. G7a: `GET /assessment-sheets` + `responsibleTeacherId`; FE dropdown từ `/teachers` (Teacher không khóa). G7b: datagrid multi-select `selectAllMode=page` (không giữ chọn qua trang). G7c: `POST /assessment-sheets/pdf-archive` `{ids,kind,format}` → zip — **tải + zip toàn bộ ở backend** (đảo "zip ở client"), `format=Images` render PDF→PNG bằng `PDFtoImage`/PDFium; dòng thiếu/lỗi ghi `_bo-qua.txt`. Dep mới `PDFtoImage 5.4.0`. Kèm fixup Đợt 1: bỏ `MinLength(1)` khỏi `Records` của create + replace-records. BE build Release 0/0 + unit 104/104; FE `test:ci` 152/152 + dev build `a20900fd`. Integration not run (Docker off; +1 test compiled). README/requests.http/req09§16/plan07/memory synced; chưa commit | `ASH-FB-W1` |

## QA — owner: chưa có agent QA riêng (root điều phối, backend/frontend tự chạy phần của mình)

| Status | Mã | Việc cần làm | Phụ thuộc |
|---|---|---|---|
| `[ ]` | [`ASH-QA-01`](details/13-ASH-QA-01.md) | Chạy đủ 10 bước smoke test theo golden path | `ASH-BE-05`, `ASH-FE-04` |
