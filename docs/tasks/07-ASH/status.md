# Theo dõi tiến độ — ASH: Bảng đánh giá năng lực

Nguồn: [`../../plans/07-ASH-assessment-sheet.md`](../../plans/07-ASH-assessment-sheet.md) và [`../../requirements/09-bang-danh-gia-nang-luc.md`](../../requirements/09-bang-danh-gia-nang-luc.md).

File này là dashboard: chỉ liệt kê trạng thái tổng quan. Mỗi mã task bấm vào để mở file chi tiết trong [`details/`](details/) — nơi ghi Mục đích, Nội dung cụ thể cần làm và Kết quả mong đợi (DoD) của riêng task đó. Lịch sử thực hiện (log) không nằm trong từng file chi tiết mà gom chung tại [`log.md`](log.md), theo trình tự thời gian thật.

Theo dõi toàn dự án vẫn nằm ở [`../../../tasks.md`](../../../tasks.md). Khi một mục ở đây đổi trạng thái, cập nhật cả dòng tương ứng (nếu có) ở `tasks.md`.

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
| `[x]` Đã chốt — chưa code | `ASH-DEC-01` | Cách sinh PDF `[F02]`/`[F03]` | Ghi dữ liệu trực tiếp vào sheet `khcn_template`/`KQ_template` sẵn có trong file `[F01]` (không cần copy sheet) → export sang PDF theo `gid` qua Google Sheets/Drive. `ASH-BE-04` triển khai theo đúng phương án này. |
| `[x]` **Đã code phần quyền** | `ASH-DEC-02` | Cách mở quyền `Teacher` cho `sync-assessments` | Đã bỏ policy `PortalManagers` khỏi endpoint và kiểm tra role `Teacher`/`Admin`/`SuperAdmin` trong handler service. Phần nạp `AssessmentSheetLatest`/`AssessmentRecordLatest` vẫn chờ `ASH-BE-03`. |
| `[x]` **Đã code** | `ASH-DEC-03` | Giữ/bỏ field `ClosedDate` | Đã bỏ trên cả `AssessmentSheet` và `AssessmentSheetLatest`; migration không tạo cột `closed_date`. |
| `[x]` **Đã code** | `ASH-DEC-04` | Spreadsheet nguồn cho `[F01]` | Đã thêm `AssessmentSheetTemplateFileId = 12ClFCOFCfUJJ1i8QstHweNaSdLfY-1MB2eaCuigqWwQ` vào `GoogleSheetsSettings`/`appsettings.json`. Còn cần xác nhận riêng (không phải code): service account đã có quyền đọc/copy file mẫu chưa. |
| `[x]` **Đã code** | `ASH-DEC-05` | Khoá upsert khi đồng bộ `AssessmentSheetLatest`/`AssessmentRecordLatest` | Migration đã tạo unique index `StudentId` (sheet) và (`AssessmentSheetLatestId`, `AssessmentCode`) (record) — `AssessmentCode` là field scalar mới thêm vào `AssessmentRecordLatest` vì EF không hỗ trợ index sub-property JSON. `ASH-BE-03` phải nhớ set field này khi upsert. |

## Tổng quan

| Giai đoạn | Tổng số việc | Chưa bắt đầu | Đang làm | Hoàn thành | Bị chặn |
|---|---:|---:|---:|---:|---:|
| Planning | 1 | 0 | 0 | 1 | 0 |
| Backend | 6 | 0 | 3 | 3 | 0 |
| Frontend | 5 | 5 | 0 | 0 | 0 |
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
| `[~]` | [`ASH-BE-03`](details/05-ASH-BE-03.md) | Mở rộng `GoogleSheetsService` (copy file mẫu → `[F01]` riêng, ghi sheet `data`, `[F0.ĐG]`, nạp lại) — đủ 4/4 mục đã code thật (kể cả upsert `AssessmentSheetLatest`/`AssessmentRecordLatest` từ `_data_DG`); chưa đánh `[x]` vì chưa gọi Google API thật lần nào để xác nhận | `ASH-BE-02` |
| `[~]` | [`ASH-BE-04`](details/06-ASH-BE-04.md) | Sinh PDF `[F02]`/`[F03]` (ghi trực tiếp vào sheet sẵn có trong `[F01]` → export) — đã code thật theo `ASH-DEC-01`, dùng mapping cột tạm (chưa xác nhận); **chưa gọi Google API thật lần nào nên chưa được đánh `[x]`** theo quy ước (chỉ tick khi đã chạy kiểm tra tương ứng) | `ASH-BE-03` |
| `[~]` | [`ASH-BE-05`](details/07-ASH-BE-05.md) | Test, README/`requests.http`, default gate, smoke phần backend | `ASH-BE-04` |

## Frontend — owner: `frontend`

| Status | Mã | Việc cần làm | Phụ thuộc |
|---|---|---|---|
| `[ ]` | [`ASH-FE-00`](details/08-ASH-FE-00.md) | Khoá contract cùng backend trước khi code UI | `ASH-BE-00` |
| `[ ]` | [`ASH-FE-01`](details/09-ASH-FE-01.md) | Danh sách + tạo `AssessmentSheet`, chọn plan có filter | `ASH-FE-00`, `ASH-BE-02` |
| `[ ]` | [`ASH-FE-02`](details/10-ASH-FE-02.md) | Form chi tiết: sửa plan/`PlanGrade`, Xuất sang Google Sheet/Đồng bộ | `ASH-FE-01`, `ASH-BE-03` |
| `[ ]` | [`ASH-FE-03`](details/11-ASH-FE-03.md) | Nhập `FinalGrade`, sinh PDF, cập nhật `[F0.ĐG]`, chuyển `Open`/`Done` | `ASH-FE-02`, `ASH-BE-04` |
| `[ ]` | [`ASH-FE-04`](details/12-ASH-FE-04.md) | Build/`test:ci` mặc định, docs/memory, smoke phần frontend | `ASH-FE-03` |

## QA — owner: chưa có agent QA riêng (root điều phối, backend/frontend tự chạy phần của mình)

| Status | Mã | Việc cần làm | Phụ thuộc |
|---|---|---|---|
| `[ ]` | [`ASH-QA-01`](details/13-ASH-QA-01.md) | Chạy đủ 10 bước smoke test theo golden path | `ASH-BE-05`, `ASH-FE-04` |
