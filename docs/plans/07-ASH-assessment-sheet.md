# Kế hoạch Bảng đánh giá năng lực (AssessmentSheet)

## 1. Thông tin kế hoạch

- **Epic:** `ASH` — Assessment Sheet.
- **Thứ tự:** `07`.
- **Trạng thái:** Đang triển khai theo từng phần; dashboard hiện hành ở [`docs/tasks/07-ASH/status.md`](../tasks/07-ASH/status.md).
- **Ngày lập:** 2026-08-20. Cập nhật entity/contract theo source thật: 2026-08-20 (nhiều lần trong ngày). Cập nhật cleanup 2026-08-27: luồng Google Sheet riêng `[F01]` cho từng `AssessmentSheet` đã ngừng dùng; PDF dùng UI preview/html2pdf rồi upload Drive.
- **Phạm vi:** .NET 10 REST API, PostgreSQL 17, tích hợp Google Sheets/Drive hiện có, và Angular/DevExtreme UI cho giáo viên.
- **Phụ thuộc:** [`01-BASE-admin-portal.md`](01-BASE-admin-portal.md) (auth, Student). Kế thừa trực tiếp kho `Assessment`/`AssessmentGroup` và `GoogleSheetsService` đã có trong source (không có plan riêng — được thêm ad hoc, xem mục 4).
- **Nguồn yêu cầu:** [`requirements/09-bang-danh-gia-nang-luc.md`](../requirements/09-bang-danh-gia-nang-luc.md) (sơ đồ luồng dữ liệu: [`requirements/09-bang-danh-gia-nang-luc-so-do-du-lieu.md`](../requirements/09-bang-danh-gia-nang-luc-so-do-du-lieu.md)). Mọi quyết định nghiệp vụ đã chốt trong tài liệu đó (mục 15) là nguồn sự thật; plan này chỉ dịch sang kế hoạch kỹ thuật, không lặp lại phần đã giải thích ở đó.

Production build, IIS package và deploy không thuộc plan này; chỉ chạy khi người dùng gọi riêng `$gv-portal-production`.

## 2. Mục tiêu

1. Hoàn thiện end-to-end tính năng `AssessmentSheet` cho giáo viên: tạo, chọn plan có filter, preview/upload PDF `[F02]`/`[F03]`, nhập kết quả, ghi `[F0.ĐG]`, nạp lại `Assessment`/`AssessmentSheetLatest`/`AssessmentRecordLatest`, chuyển `Open`/`Done`.
2. Backend: wire các entity đã có (`AssessmentSheet`, `AssessmentRecord` — với `PlanGrade`/`PlanNote`/`FinalGrade`/`FinalNote`, `AssessmentSheetLatest`, `AssessmentRecordLatest`, enum `AssessmentSheetStatus`) vào `DbContext`, migration, service, API. `AssessmentSheet.AssessmentSheetSpreadsheetId` chỉ còn là cột legacy nếu đã tồn tại trong DB/migration cũ, không thuộc contract API/UI v1.
3. Mở rộng `GoogleSheetsService` để hỗ trợ các luồng còn dùng: ghi `FinalGrade`/`FinalNote` vào `[F0.ĐG]`, nạp lại `AssessmentSheetLatest`/`AssessmentRecordLatest` (chỉ-đọc, dùng cho UI hiển thị/gửi dữ liệu gần nhất lúc tạo mới), mở quyền đồng bộ `sync-assessments` cho `Teacher`, và upload PDF do UI render vào Drive của học sinh.
4. Thêm khả năng preview PDF `[F02]`/`[F03]` từ UI bằng `html2pdf.js`, sau đó upload file PDF vào Google Drive qua endpoint upload.
5. Xây UI Angular/DevExtreme cho giáo viên thao tác toàn bộ luồng trên, tiếng Việt, dùng được trên desktop/tablet cơ bản (không có yêu cầu redesign compact card như `AUI`).
6. Kiểm tra bằng smoke test theo golden path; không xây dựng ma trận test UI/responsive/accessibility hay performance riêng cho tính năng này (xem mục 9).

## 3. Phạm vi và ngoài phạm vi

### 3.1 Trong phạm vi

- Domain/EF: hoàn thiện `AssessmentSheet`, `AssessmentRecord` (`PlanGrade`/`PlanNote`/`FinalGrade`/`FinalNote`), `AssessmentSheetLatest`, `AssessmentRecordLatest`, migration mới, `DbContext`/configuration. `AssessmentSheetSpreadsheetId` nếu còn tồn tại là legacy DB-only.
- Application: `AssessmentSheetService` (CRUD, chọn/sửa plan có filter, chuyển `Open`/`Done`), validation, authorization (mở cho `Teacher`/`Admin`/`SuperAdmin`, không giới hạn nhóm).
- Google Sheets/Drive: nạp lại `AssessmentSheetLatest`/`AssessmentRecordLatest` (chỉ-đọc), mở policy `sync-assessments` cho `Teacher`, ghi `FinalGrade`/`FinalNote` vào `[F0.ĐG]` (đúng ô theo mapping đã xác nhận), và upload PDF do UI render vào `Student.DriveFolderId`.
- PDF: sinh `[F02]`/`[F03]` từ UI preview HTML/A4 bằng `html2pdf.js`, lưu link vào `PlanFileLinkPdf`/`ResultFileLinkPdf`, ghi đè bản cũ.
- API endpoints tương ứng dưới `/api/v1/assessment-sheets` (và mở rộng `/api/v1/google-sheets` nếu phù hợp).
- Frontend: trang danh sách + form tạo/sửa `AssessmentSheet` cho giáo viên, chọn học sinh, chọn plan có filter (đọc `latestGrade`/`latestNote` từ `AssessmentSheetLatest`/`AssessmentRecordLatest` qua `GET /assessments?studentId=...`, rồi gửi kèm từng record khi tạo mới), nút preview/upload PDF F02/F03, nút cập nhật kết quả vào F0.ĐG, đồng bộ Assessment/AssessmentSheetLatest/AssessmentRecordLatest, chuyển Open/Done, nhập `PlanGrade`/`PlanNote` và `FinalGrade`/`FinalNote`.
- Smoke test thủ công + build/test mặc định theo `AGENTS.md`/`api/AGENTS.md` (xem mục 9).

### 3.2 Ngoài phạm vi

- Không redesign UI dạng compact card như `AUI`; UI đợt này dùng layout form/grid tiêu chuẩn hiện có của portal.
- Không xây bộ test UI tự động (Karma component test chi tiết, ma trận visual/responsive/accessibility) và không xây test performance/load cho tính năng này — chỉ smoke test theo mục 9.
- Không tự động hoá việc render/upload PDF hay ghi `[F0.ĐG]`; mọi bước này là hành động nút bấm thủ công theo đúng quyết định đã chốt trong requirements 09. Không còn copy file mẫu để tạo Google Sheet riêng `[F01]`.
- Không xây quy trình duyệt/audit log riêng cho chuyển `Done` ↔ `Open` (đổi trạng thái đơn giản, không cần lý do).
- Không đổi phạm vi/permission của các API Student/Group/Attendance hiện có ngoài việc mở policy `sync-assessments`.
- Không thực hiện production/IIS build hay deploy.

## 4. Hiện trạng cần biết trước khi bắt đầu

Đã có trong source (một phần đã commit, một phần đang là working tree chưa commit — kiểm tra `git status` trước khi sửa). **Entity đã được người dùng cập nhật nhiều lần trong cùng ngày 2026-08-20 — mô tả dưới đây phản ánh đúng trạng thái mới nhất tại lần đối chiếu gần nhất (`ASH-P-01`):**

- Entity `Assessment`, enum liên quan, `AssessmentGroupService`/`AssessmentController`/`AssessmentGroupController` (chỉ GET, đã hoạt động).
- `AssessmentSheet` (`api/src/AdminPortal.Domain/Entities/AssessmentSheet.cs`) — bảng làm việc hiện tại của một đợt đánh giá. Đã có sẵn: `Name`, `AssessmentSheetStatus`, `StudentId`/`Student`, `StudentSnapshot` (jsonb), `ResponsibleTeacherId`/`ResponsibleTeacher`, `ResponsibleTeacherFullNameSnapshot`, `Note`, `StartDate`, `DueDate`, `DoneDate`, `SubmissionDate`, `Feedback`, `PlanFileLinkPdf`, `ResultFileLinkPdf`, `UpdatedByUserId`/`UpdatedByUser`, `CreatedAt`, `UpdatedAt`. **Không còn `ClosedDate`** — đã tự bỏ, khớp đúng đề xuất `ASH-DEC-03` cũ. **Chưa có field `AssessmentSheetSpreadsheetId`** — field mới cần thêm (mục 5).
- `AssessmentRecord` (`AssessmentRecord.cs`) — một mục đánh giá trong `AssessmentSheet`, đã có khoá ngoại `AssessmentSheetId`/`AssessmentSheet`. **Có 4 field kết quả, không phải 1:**
  - `PlanGrade` (`AssessmentGrade?`) + `PlanNote` (`string?`, giới hạn 2000 ký tự) — giai đoạn lập kế hoạch.
  - `FinalGrade` (`AssessmentGrade?`) + `FinalNote` (`string?`, giới hạn 2000 ký tự) — kết quả đánh giá thật, độc lập với cặp Plan.
  - Đây là bản khôi phục đúng thiết kế "hai cặp tách biệt" ban đầu; **một phiên bản trung gian của tài liệu này (và của chính entity, trong lúc soạn) từng gộp lại thành một field `Grade` duy nhất — bản đó đã lỗi thời, không dùng nữa.**
- `AssessmentSheetLatest` (`AssessmentSheetLatest.cs`) và `AssessmentRecordLatest` (`AssessmentRecordLatest.cs`) — cặp entity **chỉ-đọc**, được nạp/ghi đè duy nhất bởi luồng fetch từ `[F0.data_DG]`, dùng để UI hiển thị/lọc dữ liệu gần nhất và gửi lại trong request tạo mới `AssessmentSheet` — không phục vụ mục đích nào khác, không liên quan gì tới cơ chế file `[F01]` riêng ở mục 6. `AssessmentRecordLatest.LatestGrade` vẫn là **field đơn** (không tách Plan/Final) vì đây chỉ là dữ liệu nguồn tham chiếu, không phải working record.
  - `AssessmentSheetLatest` mirror gần như nguyên cấu trúc `AssessmentSheet` nhưng **vẫn còn `ClosedDate`** và **không có** `SubmissionDate`/`AssessmentSheetSpreadsheetId`/`PlanFileLinkPdf`/`ResultFileLinkPdf`.
  - `AssessmentRecordLatest` có khoá ngoại `AssessmentSheetLatestId`/`AssessmentSheetLatest`, snapshot mục đánh giá và field `LatestGrade` đơn.
- `GoogleSheetsService.SyncAssessmentsAsync` (đọc `_data_DG_only_item`, ghi đè toàn bộ `Assessment`) và `GoogleSheetsController.SyncAssessmentsFromGoogleSheets` (`POST /api/v1/google-sheets/sync-assessments`, policy `PortalManagers`) — **chưa đọc/ghi `AssessmentSheetLatest`/`AssessmentRecordLatest`, chưa có bất kỳ thao tác Drive file copy hay ghi ngược sheet nào.**
- `StudentSnapshot` (complex type, thêm vào `Student.cs`) đã sẵn sàng để tái dùng.
- Không entity/enum nào trong nhóm trên đã đăng ký vào `DbContext`, chưa có `IEntityTypeConfiguration`, chưa có migration.

**Thông tin Google Sheet thật đã được người dùng cung cấp (không còn phải chờ, dùng thẳng khi code):**

- File mẫu `gen_assessment_sheet`, Drive file id `12ClFCOFCfUJJ1i8QstHweNaSdLfY-1MB2eaCuigqWwQ`. Không bao giờ bị chỉnh sửa trực tiếp bởi backend — chỉ dùng làm nguồn cho Drive file copy.
- 3 sheet cố định trong file mẫu (và do đó trong mọi file `[F01]` copy ra, vì Drive giữ nguyên `gid` khi copy toàn bộ file):

  | Sheet | Vai trò | Field nguồn | `gid` |
  |---|---|---|---|
  | `data` | Dữ liệu thô, đầy đủ cả hai cặp | `PlanGrade`/`PlanNote` + `FinalGrade`/`FinalNote` | `0` |
  | `khcn_template` | Nguồn sinh PDF `[F02]` — kế hoạch cá nhân | `PlanGrade`/`PlanNote` | `1320805599` |
  | `KQ_template` | Nguồn sinh PDF `[F03]` — kết quả | `FinalGrade`/`FinalNote` | `1903920808` |

- Vị trí ghi `[F0.ĐG]`: cột `E16:E` = mã mục đánh giá (dò dòng), hàng `H16:16` = mã học sinh (dò cột), ghi tại ô giao nhau. Giá trị ghi là nhãn của **`FinalGrade`** (không phải `PlanGrade`).
- Bảng mapping `FinalGrade` → nhãn ghi vào `[F0.ĐG]` (và dùng thống nhất cho UI/PDF), đã chốt với người dùng 2026-08-30: `A`→`Đạt +` (rank 3, cao nhất), `B`→`Hỗ trợ +` (rank 2), `C`→`Hỗ trợ -` (rank 1), `D`→`Chưa đạt` (rank 0, thấp nhất). Bản này sửa lỗi lệch thứ tự của định nghĩa cũ.

Việc đầu tiên của backend agent là đối chiếu lại các file trên với trạng thái `git diff`/`git status` hiện tại (đừng giả định memory cũ còn đúng, kể cả memory từ các bản trước của chính plan này — entity đã đổi nhiều lần trong cùng một ngày).

## 5. Thay đổi domain/data cần chốt kỹ thuật

**`ASH-BE-00` và `ASH-BE-01` đã thực hiện xong (2026-08-20)** — mục này giờ mô tả đúng trạng thái đã code, không còn là việc cần làm. Chi tiết thực thi: [`docs/tasks/07-ASH/log.md`](../tasks/07-ASH/log.md).

- `AssessmentRecord` giữ nguyên 4 field như mục 4: `PlanGrade`/`PlanNote`/`FinalGrade`/`FinalNote` — không gộp lại thành một field. `PlanGrade`/`PlanNote` khởi tạo từ `records[].latestGrade`/`records[].note` trong request tạo mới (UI lấy từ dữ liệu latest đang hiển thị), sau đó mutable trực tiếp qua bước sửa plan (mục 7). `FinalGrade`/`FinalNote` để trống tới khi nhập kết quả (mục 9), độc lập với `PlanGrade`/`PlanNote`, không tự động fill từ kế hoạch/latest.
- **`AssessmentSheet.AssessmentSheetSpreadsheetId` hiện là legacy DB-only**: field từng phục vụ file Google Sheet riêng `[F01]`, nhưng luồng này đã ngừng dùng từ 2026-08-27. Code mới không expose qua DTO/API/UI và không ghi giá trị mới.
- `AssessmentSheetLatest`/`AssessmentRecordLatest`: đã có EF configuration + migration (`ASH-BE-01`) và luồng đồng bộ `sync-assessments` hiện đã nạp lại cả `Assessment`, `AssessmentSheetLatest`, `AssessmentRecordLatest` từ Google Sheet. Không có API tạo/sửa/xoá trực tiếp nào cho cặp bảng này ngoài luồng đồng bộ.
- **Đính chính kỹ thuật 2026-08-25:** bản trung gian từng dùng field scalar `AssessmentRecordLatest.AssessmentCode` để index/upsert vì EF không index được sub-property JSON. Source hiện tại đã đổi sang liên kết trực tiếp `AssessmentRecordLatest.AssessmentId`/`Assessment` và unique index (`AssessmentSheetLatestId`, `AssessmentId`). Các đoạn/log cũ nhắc `AssessmentCode` chỉ còn giá trị lịch sử, không phải contract hiện hành.
- Khoá upsert khi đồng bộ (`ASH-DEC-05`) đã áp dụng trong migration hiện hành: `AssessmentSheetLatest` unique index trên `StudentId`; `AssessmentRecordLatest` unique index trên (`AssessmentSheetLatestId`, `AssessmentId`).
- `ClosedDate` trên `AssessmentSheetLatest` đã bỏ (`ASH-DEC-03`, áp dụng đầy đủ — trước đó chỉ `AssessmentSheet` đã bỏ, giờ cả hai).
- Các cấu hình cũ phục vụ `[F01]` (`AssessmentSheetTemplateFileId`, `DataSheetName`, `PlanTemplateSheetName/Gid`, `ResultTemplateSheetName/Gid`) đã bị gỡ khỏi settings hiện hành.
- Giữ nguyên nguyên tắc: sửa `AssessmentRecord`/`AssessmentSheet` không được ghi ngược `AssessmentSheetLatest`/`AssessmentRecordLatest`/`Assessment` gốc; và ngược lại, luồng đồng bộ nạp lại không được sửa `AssessmentRecord` đã snapshot trong các `AssessmentSheet` đang tồn tại.
- Migration `20260820094414_AddAssessmentSheetManagement` đã tạo 4 bảng (`assessment_sheets`, `assessment_records`, `assessment_sheet_latests`, `assessment_record_latests`), `dotnet-ef migrations has-pending-model-changes` xanh, `dotnet build` 0 warning/0 error cho `Domain`/`Application`/`Infrastructure`/`Api`/`UnitTests`, 40/40 unit test cũ vẫn pass (chưa có unit test riêng cho entity mới — thuộc `ASH-BE-02`/`05`). Tất cả FK dùng `DeleteBehavior.Restrict` khớp quy ước hiện có của codebase (không cascade).

## 6. Thiết kế Google Sheets / Drive hiện hành

### 6.1 Luồng Google Sheet riêng `[F01]` — legacy, không còn dùng

- Không tạo/copy file Google Sheet riêng cho từng `AssessmentSheet`.
- Không còn endpoint `export-to-sheet`, `sync-to-sheet`, `generate-plan-pdf`, `generate-result-pdf`.
- Không còn settings/template/gid riêng cho `[F01]`.
- `AssessmentSheetSpreadsheetId` nếu còn trong entity/DB chỉ để tương thích dữ liệu và migration cũ; không expose trong API/UI v1.

### 6.2 Ghi kết quả vào `[F0.ĐG]`

- Ghi **nhãn** của **`FinalGrade`** (theo bảng mapping ở mục 4, không phải chữ cái `A/B/C/D`, và **không phải** `PlanGrade`) vào đúng ô của sheet `ĐG` trong spreadsheet `[F0]` hiện có (khác `[F01]` — đây là file nguồn dùng chung, không phải file riêng của `AssessmentSheet`).
- Định vị ô: dò cột `E16:E` để tìm dòng khớp mã mục đánh giá (`item_id`), dò hàng `H16:16` để tìm cột khớp mã học sinh; ghi tại ô giao nhau.
- Ngay sau khi ghi thành công, set `AssessmentSheet.SubmissionDate = now`.

### 6.3 Nạp lại `Assessment`/`AssessmentSheetLatest`/`AssessmentRecordLatest` từ `[F0.data_DG]`

- Mở rộng `SyncAssessmentsAsync` để, ngoài việc thay thế `Assessment`, đọc thêm cột kết quả để nạp lại `AssessmentSheetLatest` (một dòng mirror mỗi học sinh, unique index theo `StudentId`) và các `AssessmentRecordLatest` con (`LatestGrade` đơn = kết quả đọc được — không tách Plan/Final ở bảng này). Record latest liên kết tới mục đánh giá bằng `AssessmentId`/`Assessment`, không dùng `AssessmentCode` trong source hiện hành. Cân nhắc đổi tên DTO mẫu có sẵn `AssessmentLastResultGoogleSheetResponse` trong `GoogleSheetsModels.cs` cho khớp ngữ cảnh mới khi code.
- Đổi quyền: `Teacher`/`Admin`/`SuperAdmin` đều gọi được `POST /api/v1/google-sheets/sync-assessments`. **Không đổi định nghĩa policy `PortalManagers`** (sẽ vô tình mở quyền Student/Group/Teacher/User cho `Teacher`) — thêm role-check riêng tại handler (`EnsureAssessmentSyncRole`), theo `ASH-DEC-02`.
- `AssessmentSheetLatest`/`AssessmentRecordLatest` là bảng chỉ-đọc: không service nào khác ngoài luồng đồng bộ này được phép ghi vào chúng. Luồng này hoàn toàn tách biệt khỏi `AssessmentSheet` working records.

## 7. Thiết kế sinh PDF `[F02]`/`[F03]`

PDF hiện do frontend render từ trang preview HTML/A4 bằng `html2pdf.js`, không sinh từ Google Sheet:

1. UI mở route preview kế hoạch/kết quả từ `AssessmentSheetDetail` đã lưu.
2. Nút `Mở PDF` tạo blob URL bằng `window.html2pdf().set(options).from(page).outputPdf('bloburl')` và mở tab mới.
3. Nút `Tạo PDF lên Google Drive` tạo file PDF từ DOM preview và upload multipart field `file` lên backend.
4. Backend upload PDF vào `Student.DriveFolderId`, cập nhật `PlanFileLinkPdf` hoặc `ResultFileLinkPdf`, audit hành động upload và trả lại `AssessmentSheetDetail`.
5. Nếu học sinh chưa có Drive folder id, backend trả `409 StudentDriveFolderRequired`.

## 8. API dự kiến

```http
GET    /api/v1/assessment-sheets
POST   /api/v1/assessment-sheets
GET    /api/v1/assessment-sheets/{id}
PUT    /api/v1/assessment-sheets/{id}
PUT    /api/v1/assessment-sheets/{id}/records
PATCH  /api/v1/assessments/group
POST   /api/v1/assessment-sheets/import-excel/preview
POST   /api/v1/assessment-sheets/import-excel
PUT    /api/v1/assessment-sheets/{id}/status
POST   /api/v1/assessment-sheets/{id}/upload-plan-pdf
POST   /api/v1/assessment-sheets/{id}/upload-result-pdf
POST   /api/v1/assessment-sheets/{id}/submit-results
POST   /api/v1/google-sheets/sync-assessments
```

- `POST /assessment-sheets`: tạo với `studentId`, thông tin header và plan ban đầu dưới dạng `records: [{ assessmentId, latestGrade, note }]` — không chỉ gửi mỗi `assessmentId`. Server snapshot `StudentSnapshot` + `AssessmentSnapshot`, lưu `latestGrade`/`note` request vào `PlanGrade`/`PlanNote` của từng `AssessmentRecord`; `FinalGrade`/`FinalNote` để trống. Không tạo Google Sheet riêng.
- `PUT /{id}/records`: full replace danh sách `AssessmentRecord` (thêm/bớt mục, sửa `PlanGrade`/`PlanNote`/`FinalGrade`/`FinalNote`, `displayOrder`, `groupLv2Name`/`groupLv3Name` snapshot) — chặn khi `Status = Done`. Một endpoint chung cho cả các field; UI tách khu vực nhập liệu nhưng gọi cùng endpoint này. Tên nhóm snapshot của từng record: ưu tiên `groupLv2Name`/`groupLv3Name` trong request (UI ASH-GRP-01 đổi tại chỗ, gửi kèm khi bấm "Lưu thay đổi"; bỏ trống = không gửi), rồi tới snapshot tùy chỉnh trước đó (map theo `Assessment.Code`, giữ tên do import khcn), cuối cùng mới fallback `Assessment` gốc.
- `import-excel/preview` + `import-excel` (`ASH-IMP-01`, `ASH-IMP-02`): nhận `.xlsx` (`multipart/form-data` field `file`, ≤ 10 MB), đọc bằng `ExcelDataReader`, gom sheet theo `studentCode + startDate + dueDate`. Header bắt buộc `planGrade`/`planNote`/`assessmentCode`/`studentCode`/`studentName`/`startDate`/`dueDate`; header tùy chọn `STT` (→ `DisplayOrder` số chạy toàn cục theo thứ tự dòng), `groupLv2Name`/`groupLv3Name` (fill-down kiểu ô merge → `AssessmentSnapshot`, trống hẳn thì fallback `Assessment`). `preview` chỉ validate và trả toàn bộ dòng đã parse (kèm `stt`/`groupLv2Name`/`groupLv3Name` hiệu lực) cho popup `dxDataGrid`; `import-excel` validate lại rồi tạo/thay records, không gọi Google/[F01].
- `PATCH /assessments/group` (ASH-GRP-01, tách khỏi luồng sheet): chỉ `Admin|SuperAdmin`; đặt `groupLv2Name` (`level=2`) hoặc `groupLv3Name` (`level=3`) trên bảng `Assessment` danh mục cho tập `assessmentCodes` gửi lên về `name` mới, có audit `Assessment.GroupUpdated`. Không đụng snapshot của bất kỳ sheet nào — việc đổi tên nhóm snapshot đi qua `PUT /{id}/records`. UI (nút "Cập nhật Assessment gốc" trong popup ô merge) thường gọi kèm việc áp tên tại chỗ trên giao diện để hiển thị nhất quán. Tên gốc vẫn có thể bị lần `sync-assessments` sau ghi đè. Không ghi ngược Google Sheet trong phiên bản này.
- `PUT /{id}/status`: đổi `Open`↔`Done`, set/clear `DoneDate`.
- `submit-results` (nút UI `Cập nhật Kết Quả`): ghi `[F0.ĐG]` bằng `FinalGrade`/`FinalNote` + set `SubmissionDate`. Endpoint phải đọc giá trị hiện tại trên ResultSource trước, chỉ ghi cell có thay đổi và audit riêng từng cell được ghi; `FinalNote` nằm ở cột ngay bên phải cột kết quả của học sinh. UI chỉ hiện nút khi sheet đã `Done`; role `Teacher` bị disable nút ở UI nhưng backend endpoint không đổi quyền và không chặn riêng Teacher.
- `upload-plan-pdf`: companion endpoint cho UI preview `In Kế hoạch PDF` (`ASH-FE-11`). Frontend render HTML/A4 bằng `html2pdf.js`, gửi PDF qua `multipart/form-data` field `file`; backend upload file này vào `Student.DriveFolderId`, cập nhật `PlanFileLinkPdf`, trả `AssessmentSheetDetail`. Luồng này không gọi `generate-plan-pdf`, không tạo/ghi `[F01]`, và trả `409 StudentDriveFolderRequired` nếu học sinh chưa có Drive folder id.
- `upload-result-pdf`: companion endpoint cho UI preview `In Kết Quả PDF` (`ASH-FE-12`). Frontend render HTML/A4 bằng `html2pdf.js`, dùng `FinalGrade`/`FinalNote`, gửi PDF qua `multipart/form-data` field `file`; backend upload file này vào `Student.DriveFolderId`, cập nhật `ResultFileLinkPdf`, trả `AssessmentSheetDetail`. Luồng này không gọi `generate-result-pdf`, không tạo/ghi `[F01]`, và trả `409 StudentDriveFolderRequired` nếu học sinh chưa có Drive folder id.
- Các endpoint legacy đã gỡ/không dùng: `export-to-sheet`, `sync-to-sheet`, `generate-plan-pdf`, `generate-result-pdf`. Response `AssessmentSheet` không expose `AssessmentSheetSpreadsheetId` trong contract v1.
- `GET /api/v1/assessments` hỗ trợ query `studentId` không bắt buộc. Khi có `studentId`, API vẫn trả đủ `Assessment` theo filter/sort/paging hiện tại và left join sang `AssessmentSheetLatest`/`AssessmentRecordLatest` để bổ sung `latestGrade`/`latestNote` nullable; nếu chưa có sheet latest hoặc record latest thì không làm mất dòng assessment. Field `note` hiện hữu vẫn là ghi chú gốc của `Assessment`, không phải ghi chú latest. UI picker tải toàn bộ danh sách vào client cache; TagBox `Kết quả gần nhất` đứng đầu panel filter và lọc local trên snapshot `viewMode`, gồm cả lựa chọn `Chưa có` cho `latestGrade` null/empty.
- Toàn bộ theo quy ước REST/error/pagination đã có trong [requirements/07](../requirements/07-api-bao-mat-va-van-hanh.md); không cần tài liệu hoá lại ở đây.

## 9. Test & smoke — phạm vi đã được người dùng giới hạn

Người dùng đã yêu cầu rõ: **chỉ smoke test, không cần test UI, không cần test performance** cho epic này. Áp dụng như sau:

- Vẫn giữ nguyên gate mặc định bắt buộc của repo (không phải phần mở rộng của plan này, mà là yêu cầu chung trong `AGENTS.md`/`api/AGENTS.md`/`ui/AGENTS.md`): `dotnet build`, `dotnet test` unit/integration backend, `npm run build -- --configuration development`, `npm run test:ci` frontend. Đây là compile/regression tối thiểu, không phải "test UI" theo nghĩa ma trận visual/responsive/accessibility.
- **Không** viết ma trận Unit/component chi tiết theo từng trạng thái UI, **không** viết test visual/responsive/accessibility (như plan `AUI` mục 14), **không** đo hoặc kiểm performance/load.
- Yêu cầu bắt buộc duy nhất về kiểm thử tính năng: **smoke test thủ công theo golden path**, chạy trên môi trường Development sau khi build:
  1. Đăng nhập `Teacher`; tạo `AssessmentSheet` cho một học sinh bất kỳ (không giới hạn nhóm). Xác nhận không có thao tác tạo/copy Google Sheet riêng.
  2. Chọn plan bằng ít nhất một filter (`grade`, hoặc `GroupLv1/2/3Name`); xác nhận request tạo mới gửi đủ `assessmentId`, `latestGrade`, `note` cho từng mục được chọn và `AssessmentRecord` tạo ra có `PlanGrade`/`PlanNote` đúng theo payload; `FinalGrade`/`FinalNote` để trống.
  3. Sửa plan (đổi `PlanGrade`/`PlanNote` một vài mục), lưu lại và xác nhận chỉ dữ liệu portal đổi, không có request legacy `export-to-sheet`/`sync-to-sheet`.
  4. Mở preview PDF `[F02]`; xác nhận PDF kế hoạch render từ UI, mở blob PDF được, upload Drive cập nhật `AssessmentSheet.PlanFileLinkPdf`.
  5. Nhập `FinalGrade`/`FinalNote` cho một số mục (không cần đủ hết) — xác nhận `PlanGrade` không đổi; mở preview PDF `[F03]` dù còn thiếu và upload Drive cập nhật `ResultFileLinkPdf`.
  6. Chuyển `Status` sang `Done`, bấm cập nhật kết quả vào `[F0.ĐG]`; xác nhận `SubmissionDate` được set và đúng ô (dò theo `E16:E`/`H16:16`) trên sheet `ĐG` của `[F0]` có nhãn của **`FinalGrade`** đúng theo bảng mapping (mục 4), `FinalNote` ở cột kế bên.
  7. Xác nhận `Done` vẫn khoá như logic hiện hành; chuyển lại `Open` (bất kỳ vai trò) và xác nhận sửa được tiếp.
  8. Chạy `POST /google-sheets/sync-assessments` bằng tài khoản `Teacher`; xác nhận không còn bị `403` và `Assessment`/`AssessmentSheetLatest`/`AssessmentRecordLatest` được nạp lại mà không đổi `AssessmentRecord` đã snapshot trong `AssessmentSheet` đang mở.
  9. Kiểm tra nhanh bằng `Admin`/`SuperAdmin` rằng họ cũng thấy/sửa được đúng `AssessmentSheet` do `Teacher` tạo ở bước 1 (không giới hạn theo nhóm), bao gồm link PDF/cả hai cặp `Plan*`/`Final*`.
- Ghi lại kết quả smoke test (pass/fail từng bước, ngày chạy, evidence) trong `.agents/backend/MEMORY.md` và/hoặc `.agents/frontend/MEMORY.md` tuỳ bước thuộc phía nào; không cần báo cáo test coverage riêng.
- Không smoke test thao tác tạo/copy Google Sheet riêng `[F01]` vì luồng này đã bị gỡ.

## 10. File dự kiến thay đổi

Backend:

- `api/src/AdminPortal.Domain/Entities/AssessmentSheet.cs` (`AssessmentSheetSpreadsheetId` là legacy DB-only), `AssessmentRecord.cs` (đã có `PlanGrade`/`PlanNote`/`FinalGrade`/`FinalNote`), `AssessmentSheetLatest.cs`, `AssessmentRecordLatest.cs`.
- `api/src/AdminPortal.Domain/Enums/AssessmentSheetStatus.cs` (nếu cần).
- `api/src/AdminPortal.Infrastructure/Persistence/Configurations/AssessmentSheetConfiguration.cs`, `AssessmentRecordConfiguration.cs`, `AssessmentSheetLatestConfiguration.cs`, `AssessmentRecordLatestConfiguration.cs` (mới).
- `api/src/AdminPortal.Infrastructure/Persistence/*DbContext*.cs` (đăng ký `DbSet`).
- EF migration + Designer + model snapshot mới (sinh bằng CLI theo `api/AGENTS.md`).
- `api/src/AdminPortal.Application/AssessmentSheets/` (mới: models, service, validation).
- `api/src/AdminPortal.Application/GoogleSheets/GoogleSheetsService.cs`, `GoogleSheetsModels.cs`, `IGoogleSheetsService.cs` (sync nguồn `[F0]`, upload PDF Drive, ghi `[F0.ĐG]` theo vị trí `E16:E`/`H16:16` bằng `FinalGrade`/`FinalNote`, mapping nhãn).
- `api/src/AdminPortal.Api/Controllers/AssessmentSheetsController.cs` (mới), `GoogleSheetsController.cs` (cập nhật quyền).
- `api/src/AdminPortal.Api/Authentication/AuthenticationExtensions.cs` (chỉ nếu quyết định thêm role check tại handler thay vì policy — xem mục 6.5, không đổi định nghĩa `PortalManagers`).
- `api/appsettings*.json` (chỉ giữ cấu hình Google nguồn/upload còn dùng; các key template/gid cho `[F01]` không còn thuộc config hiện hành).
- `api/tests/AdminPortal.UnitTests/...`, `api/tests/AdminPortal.IntegrationTests/...` (test tương xứng theo gate mặc định, không phải ma trận riêng).

Frontend:

- `ui/src/app/pages/assessment-sheets/` (mới: danh sách, form tạo/sửa, chọn plan có filter, panel hành động, hai khu vực nhập `Plan*`/`Final*` tách biệt).
- Model/service API client tương ứng trong `ui/src/app/core` hoặc thư mục service hiện có.
- Route/menu sidebar cho `Teacher`/`Admin`/`SuperAdmin`.
- Test compile-level theo gate mặc định (`npm run test:ci`); không thêm bộ test UI chuyên sâu theo yêu cầu phạm vi ở mục 9.

Tài liệu/handoff:

- `api/README.md`, `api/requests.http` (endpoint mới).
- `.agents/backend/MEMORY.md`, `.agents/frontend/MEMORY.md`, `.agents/shared/MEMORY.md` (quyết định kỹ thuật, đặc biệt việc ngừng dùng `[F01]`, upload PDF từ UI và tách `Plan*`/`Final*`).
- `docs/tasks/**`, `docs/plans/README.md` (thêm dòng `ASH`).

## 11. Mã đợt triển khai

### Planning

- `ASH-P-01`: đối chiếu source hiện có (mục 4) với `requirements/09`, khoá field/contract cuối theo mục 5 — lưu ý nền entity/kiến trúc Google Sheet đã đổi nhiều lần trong cùng ngày lập plan, kể cả việc `AssessmentRecord.Grade` (một field) đã được thay bằng `PlanGrade`/`PlanNote`/`FinalGrade`/`FinalNote` (bốn field).

### Backend

- `ASH-BE-00`: khoá contract DTO/enum/API surface (mục 5, 8); quyết định giữ/bỏ `ClosedDate` trên `AssessmentSheetLatest`; khoá `ASH-DEC-01`, `ASH-DEC-02`, `ASH-DEC-05` trước khi code. `AssessmentSheetSpreadsheetId` hiện chỉ còn legacy DB-only.
- `ASH-BE-01`: domain/config/migration cho `AssessmentSheet` (gồm field mới)/`AssessmentRecord` (4 field kết quả)/`AssessmentSheetLatest`/`AssessmentRecordLatest`, fresh + `has-pending-model-changes` xanh.
- `ASH-BE-02`: `AssessmentSheetService` — CRUD, chọn/sửa plan có filter, khởi tạo `PlanGrade`/`PlanNote` từ `records[].latestGrade`/`records[].note` trong request tạo mới, chuyển `Open`/`Done`, authorization mở cho mọi vai trò.
- `ASH-BE-03`: mở rộng `GoogleSheetsService` — ghi `[F0.ĐG]` theo vị trí `E16:E`/`H16:16` bằng `FinalGrade`/`FinalNote` + mapping nhãn + `SubmissionDate`, nạp lại `AssessmentSheetLatest`/`AssessmentRecordLatest` (chỉ-đọc), mở quyền `Teacher` cho `sync-assessments`.
- `ASH-BE-04`: legacy sau cleanup — backend không còn sinh PDF từ Google Sheet; PDF `[F02]`/`[F03]` do UI render và backend chỉ nhận upload Drive.
- `ASH-BE-05`: unit/integration test tương xứng thay đổi, README/`requests.http`, chạy default verification gate (mục 9), ghi kết quả smoke phần backend.

### Frontend

- `ASH-FE-00`: khoá contract cùng backend trước khi code UI (không tự đoán DTO), gồm 4 field `Plan*`/`Final*` trên `AssessmentRecord`; không dùng `assessmentSheetSpreadsheetId` trong UI contract v1.
- `ASH-FE-01`: trang danh sách + tạo `AssessmentSheet`, chọn học sinh, chọn plan có filter (`grade` đọc từ `AssessmentRecordLatest`, `GroupLv1/2/3Name`).
- `ASH-FE-02`: form chi tiết — sửa plan/`PlanGrade`/`PlanNote`; không còn nút Xuất sang Google Sheet/Đồng bộ riêng cho `[F01]`.
- `ASH-FE-03`: khu vực nhập `FinalGrade`/`FinalNote`, nút preview/upload PDF `[F02]`/`[F03]`, nút cập nhật `[F0.ĐG]`, hiển thị `SubmissionDate`/link file, chuyển `Open`/`Done`.
- `ASH-FE-04`: build/`test:ci` mặc định, cập nhật docs/memory, chạy phần frontend của smoke test (mục 9) phối hợp backend.

### QA

- `ASH-QA-01`: chạy đầy đủ smoke ở mục 9 trên môi trường Development, ghi kết quả pass/fail vào memory; không mở rộng ngoài phạm vi đã giới hạn.

## 12. Definition of Done

- Toàn bộ luồng ở mục 9 chạy pass trên môi trường Development.
- `dotnet build`/`dotnet test` (unit + integration) và `npm run build -- --configuration development`/`npm run test:ci` đều pass theo gate mặc định của `AGENTS.md`.
- EF xác nhận không còn pending model changes sau migration mới.
- `Teacher` gọi được `sync-assessments` không còn `403`; các endpoint quản trị khác (Students/Groups/Teachers/Users/Attendance recovery) vẫn giữ nguyên giới hạn `PortalManagers` — không bị mở nhầm quyền.
- Không còn tạo/copy Google Sheet riêng `[F01]`; source không còn endpoint/config/service method cho `export-to-sheet`, `sync-to-sheet`, `generate-plan-pdf`, `generate-result-pdf`.
- Sửa `PlanGrade`/`FinalGrade`/plan trên một `AssessmentSheet` không ghi ngược `AssessmentSheetLatest`/`AssessmentRecordLatest`/`Assessment` gốc; hai bảng `*Latest` chỉ bị ghi bởi đúng một luồng đồng bộ (mục 12 requirements 09), không bởi bất kỳ API nào khác. Sửa `FinalGrade` không làm đổi `PlanGrade` và ngược lại.
- `Done` khoá đúng các field theo mục 4 của requirements 09; `Open` mở lại được bởi mọi vai trò, không cần lý do.
- README/`requests.http`/`docs/tasks/**`/`docs/plans/README.md`/memory được cập nhật.
- Không chạy production/IIS build trong phạm vi plan này.

## 13. Quyết định cần user khoá

Các quyết định dưới đây là lịch sử của epic. Sau cleanup 2026-08-27, các quyết định liên quan `[F01]`/PDF từ Google Sheet được đánh dấu legacy và không còn là hướng triển khai hiện hành.

| Mã | Quyết định | Đã chốt | Trạng thái code |
|---|---|---|---|
| `ASH-DEC-01` | Cách sinh PDF `[F02]`/`[F03]` | Legacy: từng chốt sinh PDF từ sheet `khcn_template`/`KQ_template` trong `[F01]`. Hiện đã thay bằng UI preview/html2pdf + upload Drive. | **Legacy/removed** |
| `ASH-DEC-02` | Cách mở quyền `Teacher` cho `sync-assessments` | Thêm role check `Teacher`/`Admin`/`SuperAdmin` ngay tại handler `sync-assessments`, giữ nguyên định nghĩa policy `PortalManagers` dùng chung cho các API quản trị khác (mục 6.5) — tránh mở nhầm quyền Student/Group/Teacher/User cho `Teacher`. | Chưa — chờ `ASH-BE-03` |
| `ASH-DEC-03` | Giữ hay bỏ field `ClosedDate` trên `AssessmentSheetLatest` | Bỏ, vì bảng chỉ-đọc/prefill không có ý nghĩa dùng field này (đã bỏ trên `AssessmentSheet` từ trước). | **Đã code** — field đã xoá khỏi `AssessmentSheetLatest.cs`, migration không tạo cột `closed_date`. |
| `ASH-DEC-04` | Spreadsheet nguồn cho `[F01]` | Legacy: từng dùng file mẫu `gen_assessment_sheet`. Hiện không còn tạo `[F01]`, setting template đã gỡ khỏi config hiện hành. | **Legacy/removed** |
| `ASH-DEC-05` | Khoá upsert khi đồng bộ `AssessmentSheetLatest`/`AssessmentRecordLatest` | `AssessmentSheetLatest` unique theo `StudentId`; `AssessmentRecordLatest` unique theo (`AssessmentSheetLatestId`, mục đánh giá). | **Đã code** — migration hiện hành tạo unique index theo (`AssessmentSheetLatestId`, `AssessmentId`). Bản trung gian `AssessmentCode` đã bị thay thế, chỉ còn trong log lịch sử. |
| `ASH-GRP-DEC-01` | Phạm vi popup sửa group | Một popup cho đúng ô merge; nút "Áp dụng" chỉ đổi tên nhóm trên state UI. Không dùng dropdown và không rename toàn cục theo tên group. | **Đã code** — `ASH-GRP-01` |
| `ASH-GRP-DEC-02` | Ghi ngược Google Sheet | Hiển thị checkbox nhưng disable với nhãn `Chưa hỗ trợ`; chưa implement backend write-back. Chấp nhận Assessment gốc có thể bị lần sync sau ghi đè và phải cảnh báo trên UI. | **Đã code phần UI disabled** — backend write-back vẫn ngoài scope |
| `ASH-GRP-DEC-03` | Quyền và trạng thái | Teacher/Admin/SuperAdmin đổi tên nhóm snapshot khi `Open`/`Planed`; chỉ Admin/SuperAdmin dùng nút "Cập nhật Assessment gốc"; `Done` khóa toàn bộ. | **Đã code** — `ASH-GRP-01` |
| `ASH-GRP-DEC-04` | Cách lưu tên nhóm snapshot | Popup chỉ apply lên UI; lưu thật khi bấm "Lưu thay đổi" của bảng đánh giá, gộp `groupLv2Name`/`groupLv3Name` vào `PUT /{id}/records`. Mỗi ô merge có nút hoàn tác về giá trị lúc tải. Gỡ `PATCH /assessment-sheets/{id}/record-group`; thêm `PATCH /assessments/group` catalog-only. | **Đã code** — `ASH-GRP-01` (điều chỉnh 2026-08-28) |

Đây là các quyết định kỹ thuật có thể đảo ngược nếu phát hiện vấn đề khi code, không phải quyết định nghiệp vụ (nghiệp vụ đã chốt xong trong requirements 09).
