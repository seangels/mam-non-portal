# Kế hoạch Bảng đánh giá năng lực (AssessmentSheet)

## 1. Thông tin kế hoạch

- **Epic:** `ASH` — Assessment Sheet.
- **Thứ tự:** `07`.
- **Trạng thái:** Đang triển khai theo từng phần; dashboard hiện hành ở [`docs/tasks/07-ASH/status.md`](../tasks/07-ASH/status.md).
- **Ngày lập:** 2026-08-20. Cập nhật entity/contract theo source thật: 2026-08-20 (nhiều lần trong ngày). Cập nhật cơ chế Google Sheet (file riêng theo `AssessmentSheet`) và tách `AssessmentRecord.Grade` thành `PlanGrade`/`PlanNote` + `FinalGrade`/`FinalNote` theo xác nhận/source thật mới nhất của người dùng: 2026-08-20.
- **Phạm vi:** .NET 10 REST API, PostgreSQL 17, tích hợp Google Sheets/Drive hiện có, và Angular/DevExtreme UI cho giáo viên.
- **Phụ thuộc:** [`01-BASE-admin-portal.md`](01-BASE-admin-portal.md) (auth, Student). Kế thừa trực tiếp kho `Assessment`/`AssessmentGroup` và `GoogleSheetsService` đã có trong source (không có plan riêng — được thêm ad hoc, xem mục 4).
- **Nguồn yêu cầu:** [`requirements/09-bang-danh-gia-nang-luc.md`](../requirements/09-bang-danh-gia-nang-luc.md) (sơ đồ luồng dữ liệu: [`requirements/09-bang-danh-gia-nang-luc-so-do-du-lieu.md`](../requirements/09-bang-danh-gia-nang-luc-so-do-du-lieu.md)). Mọi quyết định nghiệp vụ đã chốt trong tài liệu đó (mục 15) là nguồn sự thật; plan này chỉ dịch sang kế hoạch kỹ thuật, không lặp lại phần đã giải thích ở đó.

Production build, IIS package và deploy không thuộc plan này; chỉ chạy khi người dùng gọi riêng `$gv-portal-production`.

## 2. Mục tiêu

1. Hoàn thiện end-to-end tính năng `AssessmentSheet` cho giáo viên: tạo, chọn plan có filter, xuất/đồng bộ file Google Sheet riêng `[F01]`, sinh PDF `[F02]`/`[F03]`, nhập kết quả, ghi `[F0.ĐG]`, nạp lại `Assessment`/`AssessmentSheetLatest`/`AssessmentRecordLatest`, chuyển `Open`/`Done`.
2. Backend: wire các entity đã có (`AssessmentSheet`, `AssessmentRecord` — với `PlanGrade`/`PlanNote`/`FinalGrade`/`FinalNote`, `AssessmentSheetLatest`, `AssessmentRecordLatest`, enum `AssessmentSheetStatus`) vào `DbContext`, migration, service, API; thêm field mới `AssessmentSheet.AssessmentSheetSpreadsheetId`.
3. Mở rộng `GoogleSheetsService` để hỗ trợ: **copy toàn bộ file mẫu `gen_assessment_sheet` thành file `[F01]` riêng cho mỗi `AssessmentSheet`** (lazy, tự động), ghi/cập nhật sheet `data` trong `[F01]`, điền `PlanGrade`/`PlanNote` vào sheet `khcn_template` và `FinalGrade`/`FinalNote` vào sheet `KQ_template` sẵn có trong `[F01]` để sinh PDF, ghi `FinalGrade` vào `[F0.ĐG]`, nạp lại `AssessmentSheetLatest`/`AssessmentRecordLatest` (chỉ-đọc, dùng cho UI hiển thị/gửi dữ liệu gần nhất lúc tạo mới), và mở quyền đồng bộ `sync-assessments` cho `Teacher`.
4. Thêm khả năng sinh PDF `[F02]`/`[F03]` từ sheet tương ứng trong `[F01]`.
5. Xây UI Angular/DevExtreme cho giáo viên thao tác toàn bộ luồng trên, tiếng Việt, dùng được trên desktop/tablet cơ bản (không có yêu cầu redesign compact card như `AUI`).
6. Kiểm tra bằng smoke test theo golden path; không xây dựng ma trận test UI/responsive/accessibility hay performance riêng cho tính năng này (xem mục 9).

## 3. Phạm vi và ngoài phạm vi

### 3.1 Trong phạm vi

- Domain/EF: hoàn thiện `AssessmentSheet` (thêm field `AssessmentSheetSpreadsheetId`), `AssessmentRecord` (`PlanGrade`/`PlanNote`/`FinalGrade`/`FinalNote`), `AssessmentSheetLatest`, `AssessmentRecordLatest`, migration mới, `DbContext`/configuration.
- Application: `AssessmentSheetService` (CRUD, chọn/sửa plan có filter, chuyển `Open`/`Done`), validation, authorization (mở cho `Teacher`/`Admin`/`SuperAdmin`, không giới hạn nhóm).
- Google Sheets: copy file mẫu → `[F01]` riêng (lazy, tự động khi cần), "Xuất sang Google Sheet"/"Đồng bộ" (ghi sheet `data` trong `[F01]`, đủ cả `Plan*`/`Final*`), sinh PDF (điền `PlanGrade`/`PlanNote` vào sheet `khcn_template`, `FinalGrade`/`FinalNote` vào sheet `KQ_template`, cả hai sẵn có trong `[F01]`, rồi export), ghi `FinalGrade` vào `[F0.ĐG]` (đúng ô theo mapping đã xác nhận), nạp lại `AssessmentSheetLatest`/`AssessmentRecordLatest` (chỉ-đọc), mở policy `sync-assessments` cho `Teacher`.
- PDF: sinh `[F02]` từ sheet `khcn_template` (dữ liệu `Plan*`) và `[F03]` từ sheet `KQ_template` (dữ liệu `Final*`), cả hai trong `[F01]`, lưu link vào `PlanFileLinkPdf`/`ResultFileLinkPdf`, ghi đè bản cũ.
- API endpoints tương ứng dưới `/api/v1/assessment-sheets` (và mở rộng `/api/v1/google-sheets` nếu phù hợp).
- Frontend: trang danh sách + form tạo/sửa `AssessmentSheet` cho giáo viên, chọn học sinh, chọn plan có filter (đọc `latestGrade`/`latestNote` từ `AssessmentSheetLatest`/`AssessmentRecordLatest` qua `GET /assessments?studentId=...`, rồi gửi kèm từng record khi tạo mới), các nút hành động (Xuất sang Google Sheet, Đồng bộ, Sinh PDF F02/F03, Cập nhật kết quả vào F0.ĐG, Đồng bộ Assessment/AssessmentSheetLatest/AssessmentRecordLatest, chuyển Open/Done), nhập `PlanGrade`/`PlanNote` (giai đoạn kế hoạch) và `FinalGrade`/`FinalNote` (giai đoạn kết quả) ở hai khu vực tách biệt.
- Smoke test thủ công + build/test mặc định theo `AGENTS.md`/`api/AGENTS.md` (xem mục 9).

### 3.2 Ngoài phạm vi

- Không redesign UI dạng compact card như `AUI`; UI đợt này dùng layout form/grid tiêu chuẩn hiện có của portal.
- Không xây bộ test UI tự động (Karma component test chi tiết, ma trận visual/responsive/accessibility) và không xây test performance/load cho tính năng này — chỉ smoke test theo mục 9.
- Không tự động hoá việc xuất/đồng bộ, sinh PDF hay ghi `[F0.ĐG]`; mọi bước này là hành động nút bấm thủ công theo đúng quyết định đã chốt trong requirements 09. (Việc copy file mẫu để tạo `[F01]` là ngoại lệ có chủ đích: nó tự động chạy ngầm bên trong lần đầu tiên một trong các nút bấm đó được gọi, không phải một nút riêng — xem mục 6.)
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
- Bảng mapping `FinalGrade` → nhãn ghi vào `[F0.ĐG]` (và nên dùng thống nhất cho UI/PDF): `A`→`Đạt +`, `B`→`Chưa đạt -`, `C`→`Hỗ trợ +`, `D`→`Hỗ trợ -` (xem lưu ý cần xác nhận lại ở requirements 09 mục 11/15 — cặp `B` trông không đối xứng với 3 dòng còn lại).

Việc đầu tiên của backend agent là đối chiếu lại các file trên với trạng thái `git diff`/`git status` hiện tại (đừng giả định memory cũ còn đúng, kể cả memory từ các bản trước của chính plan này — entity đã đổi nhiều lần trong cùng một ngày).

## 5. Thay đổi domain/data cần chốt kỹ thuật

**`ASH-BE-00` và `ASH-BE-01` đã thực hiện xong (2026-08-20)** — mục này giờ mô tả đúng trạng thái đã code, không còn là việc cần làm. Chi tiết thực thi: [`docs/tasks/07-ASH/log.md`](../tasks/07-ASH/log.md).

- `AssessmentRecord` giữ nguyên 4 field như mục 4: `PlanGrade`/`PlanNote`/`FinalGrade`/`FinalNote` — không gộp lại thành một field. `PlanGrade`/`PlanNote` khởi tạo từ `records[].latestGrade`/`records[].note` trong request tạo mới (UI lấy từ dữ liệu latest đang hiển thị), sau đó mutable trực tiếp qua bước sửa plan (mục 7). `FinalGrade`/`FinalNote` để trống tới khi nhập kết quả (mục 9), độc lập với `PlanGrade`.
- **Đã thêm field `AssessmentSheet.AssessmentSheetSpreadsheetId`** (`string?`): lưu Drive file id của file `[F01]` riêng — copy từ file mẫu `gen_assessment_sheet`. Null cho tới khi hành động đầu tiên cần `[F01]` được gọi.
- `AssessmentSheetLatest`/`AssessmentRecordLatest`: đã có EF configuration + migration (`ASH-BE-01`) và luồng đồng bộ `sync-assessments` hiện đã nạp lại cả `Assessment`, `AssessmentSheetLatest`, `AssessmentRecordLatest` từ Google Sheet. Không có API tạo/sửa/xoá trực tiếp nào cho cặp bảng này ngoài luồng đồng bộ.
- **Đính chính kỹ thuật 2026-08-25:** bản trung gian từng dùng field scalar `AssessmentRecordLatest.AssessmentCode` để index/upsert vì EF không index được sub-property JSON. Source hiện tại đã đổi sang liên kết trực tiếp `AssessmentRecordLatest.AssessmentId`/`Assessment` và unique index (`AssessmentSheetLatestId`, `AssessmentId`). Các đoạn/log cũ nhắc `AssessmentCode` chỉ còn giá trị lịch sử, không phải contract hiện hành.
- Khoá upsert khi đồng bộ (`ASH-DEC-05`) đã áp dụng trong migration hiện hành: `AssessmentSheetLatest` unique index trên `StudentId`; `AssessmentRecordLatest` unique index trên (`AssessmentSheetLatestId`, `AssessmentId`).
- `ClosedDate` trên `AssessmentSheetLatest` đã bỏ (`ASH-DEC-03`, áp dụng đầy đủ — trước đó chỉ `AssessmentSheet` đã bỏ, giờ cả hai).
- Đã thêm `AssessmentSheetTemplateFileId = "12ClFCOFCfUJJ1i8QstHweNaSdLfY-1MB2eaCuigqWwQ"` vào `GoogleSheetsSettings`/`IGoogleSheetsSettings` và `appsettings.json` (mục `GoogleSheets`) — giải quyết `ASH-DEC-04`.
- Giữ nguyên nguyên tắc: sửa `AssessmentRecord`/`AssessmentSheet` không được ghi ngược `AssessmentSheetLatest`/`AssessmentRecordLatest`/`Assessment` gốc; và ngược lại, luồng đồng bộ nạp lại không được sửa `AssessmentRecord` đã snapshot trong các `AssessmentSheet` đang tồn tại.
- Migration `20260820094414_AddAssessmentSheetManagement` đã tạo 4 bảng (`assessment_sheets`, `assessment_records`, `assessment_sheet_latests`, `assessment_record_latests`), `dotnet-ef migrations has-pending-model-changes` xanh, `dotnet build` 0 warning/0 error cho `Domain`/`Application`/`Infrastructure`/`Api`/`UnitTests`, 40/40 unit test cũ vẫn pass (chưa có unit test riêng cho entity mới — thuộc `ASH-BE-02`/`05`). Tất cả FK dùng `DeleteBehavior.Restrict` khớp quy ước hiện có của codebase (không cascade).

## 6. Thiết kế Google Sheets

### 6.1 File riêng `[F01]` — tạo bằng Drive file copy (lazy)

- Mỗi `AssessmentSheet` có **file Google Sheet riêng** (`[F01]`), tạo bằng Drive API `files.copy` từ file mẫu `gen_assessment_sheet` (id `12ClFCOFCfUJJ1i8QstHweNaSdLfY-1MB2eaCuigqWwQ`, xem mục 4).
- Việc copy là **lazy**: chỉ chạy khi có hành động thực sự cần `[F01]` (export-to-sheet, sync-to-sheet, generate-plan-pdf, generate-result-pdf) VÀ `AssessmentSheet.AssessmentSheetSpreadsheetId` đang null. Nên tách một helper dùng chung, ví dụ `EnsureAssessmentSheetFileAsync`, gọi ở đầu cả 4 action trên: nếu null thì copy file mẫu, lưu id mới vào `AssessmentSheetSpreadsheetId` (và `SaveChangesAsync` ngay để tránh copy trùng nếu request khác chen vào), nếu đã có thì trả về luôn.
- File `[F01]` copy ra đã có sẵn 3 sheet `data`/`khcn_template`/`KQ_template` với đúng `gid` `0`/`1320805599`/`1903920808` như file mẫu (Drive giữ nguyên `gid` nội bộ khi copy toàn bộ file) — **không cần** `AddSheetRequest`/`DuplicateSheetRequest` nào nữa cho luồng bình thường.
- (Tuỳ chọn, không bắt buộc) có thể đặt lại tiêu đề Drive của file vừa copy theo quy ước `<MãHS>.<TênGọi>_<TênĐợt>` để admin dễ nhận diện khi mở Drive thủ công — chỉ là UX, không ảnh hưởng logic vì hệ thống luôn định vị file qua `AssessmentSheetSpreadsheetId`, không qua tên.

### 6.2 Xuất sang Google Sheet / Đồng bộ — chỉ ghi sheet `data`

- "Xuất sang Google Sheet" (`POST .../export-to-sheet`): gọi `EnsureAssessmentSheetFileAsync`, sau đó `spreadsheets.values.update` ghi dữ liệu `AssessmentRecord` hiện tại (snapshot + cả `PlanGrade`/`PlanNote` + `FinalGrade`/`FinalNote`) vào sheet `data` (`gid=0`) của `[F01]`. Idempotent — gọi lại nhiều lần chỉ ghi đè, không tạo file/sheet mới nếu đã tồn tại.
- "Đồng bộ" (`POST .../sync-to-sheet`): cùng cơ chế `EnsureAssessmentSheetFileAsync` + ghi đè sheet `data`. Về mặt kỹ thuật hai action gần như giống hệt nhau (khác tên/route để khớp UX 2 nút riêng theo yêu cầu nghiệp vụ) — có thể dùng chung một hàm nội bộ.
- Không đụng tới `khcn_template`/`KQ_template` ở 2 action này.

### 6.3 Sinh PDF — ghi trực tiếp vào sheet có sẵn, không cần copy sheet riêng

- `generate-plan-pdf`: gọi `EnsureAssessmentSheetFileAsync`, sau đó `spreadsheets.values.update` ghi `AssessmentSnapshot` + `PlanGrade`/`PlanNote` vào sheet `khcn_template` (`gid=1320805599`) đã có sẵn trong `[F01]`.
- `generate-result-pdf`: tương tự, ghi `AssessmentSnapshot` + `FinalGrade`/`FinalNote` vào sheet `KQ_template` (`gid=1903920808`).
- Cả hai **không cần `DuplicateSheetRequest`** như bản trước của plan từng thiết kế, vì sheet đó đã tồn tại ngay từ lúc file được copy.
- Chỉnh format/merge cell nếu cần bằng `batchUpdate` (`MergeCellsRequest`, `UpdateDimensionPropertiesRequest`...) — mapping cột/vị trí chi tiết vẫn nằm trong phần "sẽ bổ sung sau" của requirements 09 mục 15.
- Export sheet đó (theo `ASH-DEC-01`) sang PDF, xem mục 7.

### 6.4 Ghi kết quả vào `[F0.ĐG]`

- Ghi **nhãn** của **`FinalGrade`** (theo bảng mapping ở mục 4, không phải chữ cái `A/B/C/D`, và **không phải** `PlanGrade`) vào đúng ô của sheet `ĐG` trong spreadsheet `[F0]` hiện có (khác `[F01]` — đây là file nguồn dùng chung, không phải file riêng của `AssessmentSheet`).
- Định vị ô: dò cột `E16:E` để tìm dòng khớp mã mục đánh giá (`item_id`), dò hàng `H16:16` để tìm cột khớp mã học sinh; ghi tại ô giao nhau.
- Ngay sau khi ghi thành công, set `AssessmentSheet.SubmissionDate = now`.

### 6.5 Nạp lại `Assessment`/`AssessmentSheetLatest`/`AssessmentRecordLatest` từ `[F0.data_DG]`

- Mở rộng `SyncAssessmentsAsync` để, ngoài việc thay thế `Assessment`, đọc thêm cột kết quả để nạp lại `AssessmentSheetLatest` (một dòng mirror mỗi học sinh, unique index theo `StudentId`) và các `AssessmentRecordLatest` con (`LatestGrade` đơn = kết quả đọc được — không tách Plan/Final ở bảng này). Record latest liên kết tới mục đánh giá bằng `AssessmentId`/`Assessment`, không dùng `AssessmentCode` trong source hiện hành. Cân nhắc đổi tên DTO mẫu có sẵn `AssessmentLastResultGoogleSheetResponse` trong `GoogleSheetsModels.cs` cho khớp ngữ cảnh mới khi code.
- Đổi quyền: `Teacher`/`Admin`/`SuperAdmin` đều gọi được `POST /api/v1/google-sheets/sync-assessments`. **Không đổi định nghĩa policy `PortalManagers`** (sẽ vô tình mở quyền Student/Group/Teacher/User cho `Teacher`) — thêm role-check riêng tại handler (`EnsureAssessmentSyncRole`), theo `ASH-DEC-02`.
- `AssessmentSheetLatest`/`AssessmentRecordLatest` là bảng chỉ-đọc: không service nào khác ngoài luồng đồng bộ này được phép ghi vào chúng. Luồng này hoàn toàn tách biệt khỏi cơ chế file `[F01]` ở mục 6.1–6.4 — không liên quan tới nhau.

## 7. Thiết kế sinh PDF `[F02]`/`[F03]`

Đơn giản hơn bản trước của plan vì sheet nguồn (`khcn_template`/`KQ_template`) đã có sẵn trong `[F01]` (mục 6.3), không cần bước "make copy sheet" riêng nữa:

1. `EnsureAssessmentSheetFileAsync` + ghi dữ liệu vào sheet `khcn_template` (`Plan*`) hoặc `KQ_template` (`Final*`) của `[F01]` (mục 6.3).
2. **Export sang PDF (cần `ASH-DEC-01` khoá trước khi code):** đề xuất dùng chính Google Sheets/Drive export sheet đó sang PDF qua URL dạng `.../export?format=pdf&gid={sheetId}`, dùng `fileId = AssessmentSheetSpreadsheetId`, `gid = 1320805599` (khcn) hoặc `1903920808` (KQ), xác thực bằng access token của service account hiện có (có thể cần mở rộng scope từ `Spreadsheets` sang thêm `https://www.googleapis.com/auth/drive.readonly`). Ưu điểm: PDF khớp chính xác template mà đội vận hành thiết kế, không cần dựng lại layout trong code .NET, không thêm dependency PDF nặng.
3. Backend tải PDF bytes về, lưu vào vị trí lưu trữ file dùng chung của portal (hoặc cấu hình mới nếu chưa có), set `AssessmentSheet.PlanFileLinkPdf`/`ResultFileLinkPdf`. Chỉ giữ **bản mới nhất** — mỗi lần sinh lại lặp lại bước 1–3 và ghi đè link.
4. Nếu bước 2 không khả thi (giới hạn quyền Drive export), phương án dự phòng: dựng PDF trong .NET bằng thư viện có license phù hợp dự án nhỏ (ví dụ QuestPDF Community), đọc dữ liệu trực tiếp từ `AssessmentRecord` (bỏ qua bước 1 vì không cần sheet trung gian) — chỉ chuyển sang phương án này nếu phương án chính bị chặn, và phải ghi rõ lý do trong `.agents/backend/MEMORY.md`.

## 8. API dự kiến

```http
GET    /api/v1/assessment-sheets
POST   /api/v1/assessment-sheets
GET    /api/v1/assessment-sheets/{id}
PUT    /api/v1/assessment-sheets/{id}
PUT    /api/v1/assessment-sheets/{id}/records
PUT    /api/v1/assessment-sheets/{id}/status
POST   /api/v1/assessment-sheets/{id}/export-to-sheet
POST   /api/v1/assessment-sheets/{id}/sync-to-sheet
POST   /api/v1/assessment-sheets/{id}/generate-plan-pdf
POST   /api/v1/assessment-sheets/{id}/generate-result-pdf
POST   /api/v1/assessment-sheets/{id}/submit-results
POST   /api/v1/google-sheets/sync-assessments
```

- `POST /assessment-sheets`: tạo với `studentId`, thông tin header và plan ban đầu dưới dạng `records: [{ assessmentId, latestGrade, note }]` — không chỉ gửi mỗi `assessmentId`. Server snapshot `StudentSnapshot` + `AssessmentSnapshot`, lưu `latestGrade`/`note` request vào `PlanGrade`/`PlanNote` của từng `AssessmentRecord`; `FinalGrade`/`FinalNote` để trống. Không copy file `[F01]` ở bước này (lazy, xem mục 6.1).
- `PUT /{id}/records`: full replace danh sách `AssessmentRecord` (thêm/bớt mục, sửa `PlanGrade`/`PlanNote`/`FinalGrade`/`FinalNote`) — chặn khi `Status = Done`. Một endpoint chung cho cả 4 field; UI tách hai khu vực nhập liệu (`ASH-FE-02` cho Plan, `ASH-FE-03` cho Final) nhưng gọi cùng endpoint này.
- `PUT /{id}/status`: đổi `Open`↔`Done`, set/clear `DoneDate`.
- `export-to-sheet`, `sync-to-sheet`, `generate-plan-pdf`, `generate-result-pdf`, `submit-results` (ghi `[F0.ĐG]` bằng `FinalGrade` + set `SubmissionDate`) đều là action endpoint riêng, không gộp vào `PUT` chính, đúng với việc mỗi bước là thao tác nút bấm độc lập. 4 action đầu đều tự đảm bảo `[F01]` tồn tại (mục 6.1) — không bắt buộc gọi đúng thứ tự.
- Response của `AssessmentSheet` nên có thể expose `AssessmentSheetSpreadsheetId` (hoặc một link Drive dựng sẵn từ id đó) để UI có thể cho người dùng mở trực tiếp file `[F01]` nếu cần — quyết định UI cụ thể thuộc `ASH-FE-02`/`ASH-FE-03`.
- `GET /api/v1/assessments` hỗ trợ query `studentId` không bắt buộc. Khi có `studentId`, API vẫn trả đủ `Assessment` theo filter/sort/paging hiện tại và left join sang `AssessmentSheetLatest`/`AssessmentRecordLatest` để bổ sung `latestGrade`/`latestNote` nullable; nếu chưa có sheet latest hoặc record latest thì không làm mất dòng assessment. Field `note` hiện hữu vẫn là ghi chú gốc của `Assessment`, không phải ghi chú latest. UI picker tải toàn bộ danh sách vào client cache; TagBox `Kết quả gần nhất` đứng đầu panel filter và lọc local trên snapshot `viewMode`, gồm cả lựa chọn `Chưa có` cho `latestGrade` null/empty.
- Toàn bộ theo quy ước REST/error/pagination đã có trong [requirements/07](../requirements/07-api-bao-mat-va-van-hanh.md); không cần tài liệu hoá lại ở đây.

## 9. Test & smoke — phạm vi đã được người dùng giới hạn

Người dùng đã yêu cầu rõ: **chỉ smoke test, không cần test UI, không cần test performance** cho epic này. Áp dụng như sau:

- Vẫn giữ nguyên gate mặc định bắt buộc của repo (không phải phần mở rộng của plan này, mà là yêu cầu chung trong `AGENTS.md`/`api/AGENTS.md`/`ui/AGENTS.md`): `dotnet build`, `dotnet test` unit/integration backend, `npm run build -- --configuration development`, `npm run test:ci` frontend. Đây là compile/regression tối thiểu, không phải "test UI" theo nghĩa ma trận visual/responsive/accessibility.
- **Không** viết ma trận Unit/component chi tiết theo từng trạng thái UI, **không** viết test visual/responsive/accessibility (như plan `AUI` mục 14), **không** đo hoặc kiểm performance/load.
- Yêu cầu bắt buộc duy nhất về kiểm thử tính năng: **smoke test thủ công theo golden path**, chạy trên môi trường Development sau khi build:
  1. Đăng nhập `Teacher`; tạo `AssessmentSheet` cho một học sinh bất kỳ (không giới hạn nhóm). Xác nhận `AssessmentSheetSpreadsheetId` vẫn null lúc này (chưa copy file).
  2. Chọn plan bằng ít nhất một filter (`grade`, hoặc `GroupLv1/2/3Name`); xác nhận request tạo mới gửi đủ `assessmentId`, `latestGrade`, `note` cho từng mục được chọn và `AssessmentRecord` tạo ra có `PlanGrade`/`PlanNote` đúng theo payload; `FinalGrade`/`FinalNote` để trống.
  3. Bấm "Xuất sang Google Sheet"; xác nhận `AssessmentSheetSpreadsheetId` được set (file `[F01]` mới xuất hiện trên Drive, là bản copy của `gen_assessment_sheet`), và sheet `data` (`gid=0`) trong `[F01]` có đúng dữ liệu (cả `Plan*` lẫn `Final*`, dù `Final*` còn trống).
  4. Sửa plan (đổi `PlanGrade`/`PlanNote` một vài mục), bấm "Đồng bộ"; xác nhận sheet `data` cập nhật, **không** tạo thêm file `[F01]` khác (vẫn dùng đúng `AssessmentSheetSpreadsheetId` cũ).
  5. Sinh PDF `[F02]`; xác nhận sheet `khcn_template` (`gid=1320805599`) trong **đúng file `[F01]` đã có** được điền `PlanGrade`/`PlanNote` mới, link PDF lưu vào `AssessmentSheet.PlanFileLinkPdf` và file mở được.
  6. Nhập `FinalGrade`/`FinalNote` cho một số mục (không cần đủ hết) — xác nhận `PlanGrade` không đổi; sinh PDF `[F03]` dù còn thiếu — xác nhận sheet `KQ_template` (`gid=1903920808`) được điền `Final*` và vẫn sinh được PDF.
  7. Bấm cập nhật kết quả vào `[F0.ĐG]`; xác nhận `SubmissionDate` được set và đúng ô (dò theo `E16:E`/`H16:16`) trên sheet `ĐG` của `[F0]` có nhãn của **`FinalGrade`** đúng theo bảng mapping (mục 4).
  8. Chuyển `Status` sang `Done`; xác nhận bị khoá sửa plan/`PlanGrade`/`PlanNote`/`FinalGrade`/`FinalNote`/feedback; chuyển lại `Open` (bất kỳ vai trò) và xác nhận sửa được tiếp.
  9. Chạy `POST /google-sheets/sync-assessments` bằng tài khoản `Teacher`; xác nhận không còn bị `403` và `Assessment`/`AssessmentSheetLatest`/`AssessmentRecordLatest` được nạp lại mà không đổi `AssessmentRecord` đã snapshot trong `AssessmentSheet` đang mở ở bước 1–8, và không đụng tới file `[F01]` của bảng đó.
  10. Kiểm tra nhanh bằng `Admin`/`SuperAdmin` rằng họ cũng thấy/sửa được đúng `AssessmentSheet` do `Teacher` tạo ở bước 1 (không giới hạn theo nhóm), bao gồm cả việc thấy đúng `AssessmentSheetSpreadsheetId`/link PDF/cả hai cặp `Plan*`/`Final*`.
- Ghi lại kết quả smoke test (pass/fail từng bước, ngày chạy, evidence) trong `.agents/backend/MEMORY.md` và/hoặc `.agents/frontend/MEMORY.md` tuỳ bước thuộc phía nào; không cần báo cáo test coverage riêng.
- Nếu một bước smoke phụ thuộc vào phần "sẽ bổ sung sau" (mapping cột chi tiết trong sheet `data`/`khcn_template`/`KQ_template`, xem requirements 09 mục 15) mà chưa có, ghi rõ đây là blocker chờ input người dùng thay vì tự suy diễn định dạng cột.

## 10. File dự kiến thay đổi

Backend:

- `api/src/AdminPortal.Domain/Entities/AssessmentSheet.cs` (thêm field `AssessmentSheetSpreadsheetId`), `AssessmentRecord.cs` (đã có `PlanGrade`/`PlanNote`/`FinalGrade`/`FinalNote`), `AssessmentSheetLatest.cs`, `AssessmentRecordLatest.cs`.
- `api/src/AdminPortal.Domain/Enums/AssessmentSheetStatus.cs` (nếu cần).
- `api/src/AdminPortal.Infrastructure/Persistence/Configurations/AssessmentSheetConfiguration.cs`, `AssessmentRecordConfiguration.cs`, `AssessmentSheetLatestConfiguration.cs`, `AssessmentRecordLatestConfiguration.cs` (mới).
- `api/src/AdminPortal.Infrastructure/Persistence/*DbContext*.cs` (đăng ký `DbSet`).
- EF migration + Designer + model snapshot mới (sinh bằng CLI theo `api/AGENTS.md`).
- `api/src/AdminPortal.Application/AssessmentSheets/` (mới: models, service, validation).
- `api/src/AdminPortal.Application/GoogleSheets/GoogleSheetsService.cs`, `GoogleSheetsModels.cs`, `IGoogleSheetsService.cs` (mở rộng theo mục 6: Drive file copy, `EnsureAssessmentSheetFileAsync`, ghi sheet theo `gid` cố định với đúng field group (`Plan*`/`Final*`), ghi `[F0.ĐG]` theo vị trí `E16:E`/`H16:16` bằng `FinalGrade`, mapping nhãn).
- `api/src/AdminPortal.Api/Controllers/AssessmentSheetsController.cs` (mới), `GoogleSheetsController.cs` (cập nhật quyền).
- `api/src/AdminPortal.Api/Authentication/AuthenticationExtensions.cs` (chỉ nếu quyết định thêm role check tại handler thay vì policy — xem mục 6.5, không đổi định nghĩa `PortalManagers`).
- `api/appsettings*.json` (cấu hình `AssessmentSheetTemplateFileId = 12ClFCOFCfUJJ1i8QstHweNaSdLfY-1MB2eaCuigqWwQ`; scope Drive nếu áp dụng phương án export PDF; quyền edit của service account trên file mẫu cần xác nhận trước khi chạy thật — xem mục 13).
- `api/tests/AdminPortal.UnitTests/...`, `api/tests/AdminPortal.IntegrationTests/...` (test tương xứng theo gate mặc định, không phải ma trận riêng).

Frontend:

- `ui/src/app/pages/assessment-sheets/` (mới: danh sách, form tạo/sửa, chọn plan có filter, panel hành động, hai khu vực nhập `Plan*`/`Final*` tách biệt).
- Model/service API client tương ứng trong `ui/src/app/core` hoặc thư mục service hiện có.
- Route/menu sidebar cho `Teacher`/`Admin`/`SuperAdmin`.
- Test compile-level theo gate mặc định (`npm run test:ci`); không thêm bộ test UI chuyên sâu theo yêu cầu phạm vi ở mục 9.

Tài liệu/handoff:

- `api/README.md`, `api/requests.http` (endpoint mới).
- `.agents/backend/MEMORY.md`, `.agents/frontend/MEMORY.md`, `.agents/shared/MEMORY.md` (quyết định kỹ thuật, đặc biệt mục 6.1 và 7, và việc tách `Plan*`/`Final*`).
- `docs/tasks/**`, `docs/plans/README.md` (thêm dòng `ASH`).

## 11. Mã đợt triển khai

### Planning

- `ASH-P-01`: đối chiếu source hiện có (mục 4) với `requirements/09`, khoá field/contract cuối theo mục 5 — lưu ý nền entity/kiến trúc Google Sheet đã đổi nhiều lần trong cùng ngày lập plan, kể cả việc `AssessmentRecord.Grade` (một field) đã được thay bằng `PlanGrade`/`PlanNote`/`FinalGrade`/`FinalNote` (bốn field).

### Backend

- `ASH-BE-00`: khoá contract DTO/enum/API surface (mục 5, 8); thêm field `AssessmentSheetSpreadsheetId`; quyết định giữ/bỏ `ClosedDate` trên `AssessmentSheetLatest`; khoá `ASH-DEC-01`, `ASH-DEC-02`, `ASH-DEC-05` (mục 13, `ASH-DEC-04` đã có giá trị cụ thể) trước khi code.
- `ASH-BE-01`: domain/config/migration cho `AssessmentSheet` (gồm field mới)/`AssessmentRecord` (4 field kết quả)/`AssessmentSheetLatest`/`AssessmentRecordLatest`, fresh + `has-pending-model-changes` xanh.
- `ASH-BE-02`: `AssessmentSheetService` — CRUD, chọn/sửa plan có filter, khởi tạo `PlanGrade`/`PlanNote` từ `records[].latestGrade`/`records[].note` trong request tạo mới, chuyển `Open`/`Done`, authorization mở cho mọi vai trò.
- `ASH-BE-03`: mở rộng `GoogleSheetsService` — `EnsureAssessmentSheetFileAsync` (Drive file copy lazy), ghi/cập nhật sheet `data` (đủ cả 4 field), ghi `[F0.ĐG]` theo vị trí `E16:E`/`H16:16` bằng `FinalGrade` + mapping nhãn + `SubmissionDate`, nạp lại `AssessmentSheetLatest`/`AssessmentRecordLatest` (chỉ-đọc), mở quyền `Teacher` cho `sync-assessments` theo đúng cách ở mục 6.5.
- `ASH-BE-04`: sinh PDF `[F02]`/`[F03]` theo phương án đã khoá ở `ASH-DEC-01` — ghi `Plan*` trực tiếp vào sheet `khcn_template`, `Final*` vào `KQ_template` (cả hai sẵn có trong `[F01]`, không cần duplicate sheet), rồi export PDF (mục 7).
- `ASH-BE-05`: unit/integration test tương xứng thay đổi, README/`requests.http`, chạy default verification gate (mục 9), ghi kết quả smoke phần backend.

### Frontend

- `ASH-FE-00`: khoá contract cùng backend trước khi code UI (không tự đoán DTO), gồm cả field `AssessmentSheetSpreadsheetId` và 4 field `Plan*`/`Final*` trên `AssessmentRecord`.
- `ASH-FE-01`: trang danh sách + tạo `AssessmentSheet`, chọn học sinh, chọn plan có filter (`grade` đọc từ `AssessmentRecordLatest`, `GroupLv1/2/3Name`).
- `ASH-FE-02`: form chi tiết — sửa plan/`PlanGrade`/`PlanNote`, nút Xuất sang Google Sheet/Đồng bộ.
- `ASH-FE-03`: khu vực nhập `FinalGrade`/`FinalNote`, nút sinh PDF `[F02]`/`[F03]`, nút cập nhật `[F0.ĐG]`, hiển thị `SubmissionDate`/link file, chuyển `Open`/`Done`.
- `ASH-FE-04`: build/`test:ci` mặc định, cập nhật docs/memory, chạy phần frontend của smoke test (mục 9) phối hợp backend.

### QA

- `ASH-QA-01`: chạy đầy đủ 10 bước smoke ở mục 9 trên môi trường Development, ghi kết quả pass/fail vào memory; không mở rộng ngoài phạm vi đã giới hạn.

## 12. Definition of Done

- Toàn bộ luồng ở mục 9 (10 bước smoke) chạy pass trên môi trường Development.
- `dotnet build`/`dotnet test` (unit + integration) và `npm run build -- --configuration development`/`npm run test:ci` đều pass theo gate mặc định của `AGENTS.md`.
- EF xác nhận không còn pending model changes sau migration mới.
- `Teacher` gọi được `sync-assessments` không còn `403`; các endpoint quản trị khác (Students/Groups/Teachers/Users/Attendance recovery) vẫn giữ nguyên giới hạn `PortalManagers` — không bị mở nhầm quyền.
- Mỗi `AssessmentSheet` chỉ tạo đúng một file `[F01]` (không copy trùng khi bấm lại nút nhiều lần); file mẫu `gen_assessment_sheet` không bị chỉnh sửa bởi bất kỳ luồng nào.
- Sửa `PlanGrade`/`FinalGrade`/plan trên một `AssessmentSheet` không ghi ngược `AssessmentSheetLatest`/`AssessmentRecordLatest`/`Assessment` gốc; hai bảng `*Latest` chỉ bị ghi bởi đúng một luồng đồng bộ (mục 12 requirements 09), không bởi bất kỳ API nào khác. Sửa `FinalGrade` không làm đổi `PlanGrade` và ngược lại.
- `Done` khoá đúng các field theo mục 4 của requirements 09; `Open` mở lại được bởi mọi vai trò, không cần lý do.
- README/`requests.http`/`docs/tasks/**`/`docs/plans/README.md`/memory được cập nhật.
- Không chạy production/IIS build trong phạm vi plan này.

## 13. Quyết định cần user khoá

Cả 5 quyết định dưới đây **đã được chốt**. `ASH-DEC-03`/`04`/`05` đã **áp dụng thật vào code** ở `ASH-BE-00`/`ASH-BE-01` (2026-08-20). `ASH-DEC-01`/`02` vẫn chờ `ASH-BE-04`/`ASH-BE-03` triển khai.

| Mã | Quyết định | Đã chốt | Trạng thái code |
|---|---|---|---|
| `ASH-DEC-01` | Cách sinh PDF `[F02]`/`[F03]` | Ghi dữ liệu (`Plan*` cho khcn, `Final*` cho KQ) trực tiếp vào sheet `khcn_template`/`KQ_template` sẵn có trong `[F01]`, rồi export sheet đó sang PDF theo `gid` qua Google Sheets/Drive (mục 7); chỉ chuyển sang thư viện PDF .NET (ví dụ QuestPDF Community) nếu export bị chặn về quyền/scope. | Chưa — chờ `ASH-BE-04` |
| `ASH-DEC-02` | Cách mở quyền `Teacher` cho `sync-assessments` | Thêm role check `Teacher`/`Admin`/`SuperAdmin` ngay tại handler `sync-assessments`, giữ nguyên định nghĩa policy `PortalManagers` dùng chung cho các API quản trị khác (mục 6.5) — tránh mở nhầm quyền Student/Group/Teacher/User cho `Teacher`. | Chưa — chờ `ASH-BE-03` |
| `ASH-DEC-03` | Giữ hay bỏ field `ClosedDate` trên `AssessmentSheetLatest` | Bỏ, vì bảng chỉ-đọc/prefill không có ý nghĩa dùng field này (đã bỏ trên `AssessmentSheet` từ trước). | **Đã code** — field đã xoá khỏi `AssessmentSheetLatest.cs`, migration không tạo cột `closed_date`. |
| `ASH-DEC-04` | Spreadsheet nguồn cho `[F01]` | File mẫu `gen_assessment_sheet`, id `12ClFCOFCfUJJ1i8QstHweNaSdLfY-1MB2eaCuigqWwQ`, cấu hình vào setting `AssessmentSheetTemplateFileId`. | **Đã code** — thêm vào `GoogleSheetsSettings`/`IGoogleSheetsSettings` và `appsettings.json`. Còn một điều kiện tiên quyết vận hành (không phải code): xác nhận service account đã có quyền đọc file mẫu để copy và tạo file mới trong Drive hay chưa. |
| `ASH-DEC-05` | Khoá upsert khi đồng bộ `AssessmentSheetLatest`/`AssessmentRecordLatest` | `AssessmentSheetLatest` unique theo `StudentId`; `AssessmentRecordLatest` unique theo (`AssessmentSheetLatestId`, mục đánh giá). | **Đã code** — migration hiện hành tạo unique index theo (`AssessmentSheetLatestId`, `AssessmentId`). Bản trung gian `AssessmentCode` đã bị thay thế, chỉ còn trong log lịch sử. |

Đây là các quyết định kỹ thuật có thể đảo ngược nếu phát hiện vấn đề khi code, không phải quyết định nghiệp vụ (nghiệp vụ đã chốt xong trong requirements 09).
