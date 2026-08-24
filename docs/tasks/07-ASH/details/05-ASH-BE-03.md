# ASH-BE-03 — Mở rộng GoogleSheetsService

Owner: `backend`. Phụ thuộc: `ASH-BE-02`. Trạng thái: xem [`../status.md`](../status.md). Log lịch sử: [`../log.md`](../log.md).

Nguồn: [plan mục 6](../../../plans/07-ASH-assessment-sheet.md#6-thiết-kế-google-sheets), [sơ đồ luồng dữ liệu](../../../requirements/09-bang-danh-gia-nang-luc-so-do-du-lieu.md).

## Mục đích

Phần tích hợp Google Sheets của epic, **trừ việc điền dữ liệu sinh PDF** (thuộc `ASH-BE-04`): tạo file riêng `[F01]` cho mỗi `AssessmentSheet`, xuất/đồng bộ sheet `data`, ghi kết quả vào `[F0.ĐG]`, và nạp lại `Assessment`/`AssessmentSheetLatest`/`AssessmentRecordLatest`. Mọi hành động nút bấm đều thủ công (không tự động hoá) — ngoại lệ duy nhất là bước copy file mẫu, tự chạy ngầm bên trong lần đầu một hành động cần `[F01]`.

**Kiến trúc đã xác nhận với người dùng (2026-08-20), khác bản đầu của plan:** không có "1 file dùng chung, đặt tên sheet theo đợt–học sinh". Mỗi `AssessmentSheet` có **file riêng `[F01]`**, tạo bằng Drive file copy từ file mẫu `gen_assessment_sheet`.

## Nội dung cụ thể cần làm

**1. `EnsureAssessmentSheetFileAsync` — helper dùng chung, lazy Drive file copy:**

- Input: `AssessmentSheet` hiện tại.
- Nếu `AssessmentSheetSpreadsheetId` đã có giá trị: trả về luôn, không làm gì thêm.
- Nếu null: gọi Drive API `files.copy` với nguồn là file mẫu id `12ClFCOFCfUJJ1i8QstHweNaSdLfY-1MB2eaCuigqWwQ` (đọc từ config `AssessmentSheetTemplateFileId`) → nhận file id mới. Lưu vào `AssessmentSheet.AssessmentSheetSpreadsheetId` và `SaveChangesAsync` ngay để tránh copy trùng nếu có request khác chen vào cùng lúc.
- File mới đã có sẵn 3 sheet cố định `data` (`gid=0`), `khcn_template` (`gid=1320805599`), `KQ_template` (`gid=1903920808`) — **không cần** `AddSheetRequest`/`DuplicateSheetRequest` nào ở helper này hay bất kỳ action nào khác.
- Gọi helper này ở đầu cả 4 action bên dưới (export, sync, generate-plan-pdf, generate-result-pdf) — nhờ vậy các nút không bắt buộc phải bấm đúng thứ tự.

**2. "Xuất sang Google Sheet" / "Đồng bộ" — chỉ ghi sheet `data`** (`POST .../export-to-sheet`, `POST .../sync-to-sheet`):

- Gọi `EnsureAssessmentSheetFileAsync`, sau đó `spreadsheets.values.update` ghi dữ liệu `AssessmentRecord` hiện tại (snapshot + cả `PlanGrade`/`PlanNote` + `FinalGrade`/`FinalNote`) vào sheet `data` (`gid=0`) của `[F01]`. Về kỹ thuật hai action gần như giống hệt nhau (khác route để khớp UX 2 nút riêng theo yêu cầu nghiệp vụ).
- Không đụng tới `khcn_template`/`KQ_template` ở đây (thuộc `ASH-BE-04`).

**3. Ghi kết quả vào `[F0.ĐG]`** (`POST .../submit-results`):

- Ghi **nhãn** của **`FinalGrade`** (không phải `PlanGrade`, không phải chữ cái `A/B/C/D`) theo bảng mapping: `A`→`Đạt +`, `B`→`Chưa đạt -`, `C`→`Hỗ trợ +`, `D`→`Hỗ trợ -`.
- Định vị ô: dò cột `E16:E` của sheet `ĐG` trong file `[F0]` để tìm dòng khớp mã mục đánh giá (`item_id`); dò hàng `H16:16` để tìm cột khớp mã học sinh; ghi tại ô giao nhau.
- Ngay sau khi ghi thành công, set `AssessmentSheet.SubmissionDate = now`.

**4. Nạp lại `Assessment`/`AssessmentSheetLatest`/`AssessmentRecordLatest` từ `[F0.data_DG]`** (`POST /google-sheets/sync-assessments`):

- Mở rộng `SyncAssessmentsAsync` để, ngoài thay thế `Assessment`, đọc thêm cột kết quả để **upsert** `AssessmentSheetLatest` (theo `StudentId` — `ASH-DEC-05`) và các `AssessmentRecordLatest` con (`LatestGrade` field đơn = kết quả đọc được — bảng này không tách `Plan`/`Final`).
- Đổi quyền: `Teacher`/`Admin`/`SuperAdmin` đều gọi được. **Không đổi định nghĩa policy `PortalManagers`** — thêm role-check riêng ngay tại handler (`EnsureAssessmentSyncRole`), theo `ASH-DEC-02`.
- Luồng này **hoàn toàn tách biệt** khỏi cơ chế file `[F01]` ở mục 1–3 — không liên quan tới nhau, đừng lẫn lộn khi code (một bên là file Drive riêng cho việc xuất/PDF, một bên là bảng DB chỉ-đọc để prefill).

## Kết quả mong đợi (Definition of Done)

4 hành động (export/sync chỉ sheet `data`, submit-results đúng vị trí + đúng nhãn, sync-assessments mở rộng) hoạt động qua API thật với Google Sheets; mỗi `AssessmentSheet` chỉ tạo đúng một file `[F01]` dù bấm nút nhiều lần; file mẫu `gen_assessment_sheet` không bị sửa. `Teacher` gọi `sync-assessments` không còn `403`; các endpoint quản trị khác vẫn giữ nguyên `PortalManagers`. Nếu mapping cột chi tiết trong sheet `data` chưa có, ghi rõ đây là blocker chờ input thay vì tự suy diễn.

## Tiến độ (2026-08-20) — 3/4 mục đã code thật, mục 4 còn thiếu

Đã implement thật (không còn stub) trong `GoogleSheetsService.cs`, gọi từ `AssessmentSheetService.cs`:

- **Mục 1 — `EnsureAssessmentSheetSpreadsheetAsync`**: Drive `files.copy` từ `AssessmentSheetTemplateFileId`, lazy (chỉ copy khi `AssessmentSheetSpreadsheetId` null), `SaveChangesAsync` ngay sau khi set id để tránh copy trùng. Cần thêm package `Google.Apis.Drive.v3` và mở rộng scope credential sang `DriveService.Scope.Drive` (trước đây chỉ có `Spreadsheets`).
- **Mục 2 — export-to-sheet/sync-to-sheet**: dùng chung `WriteRecordsToSheetAsync` (ghi sheet `data`, gid 0, cả `Plan*`/`Final*`), idempotent (`Values.Clear` một vùng rộng rồi `Values.Update` đè lên, không tích luỹ dòng cũ).
- **Mục 3 — submit-results ghi `[F0.ĐG]`**: `WriteFinalGradesToSourceSheetAsync` — đọc `ĐG!E16:E1000` (mã mục) và `ĐG!H16:BZ16` (mã học sinh), dò vị trí bằng `GoogleSheetsGridLocator` (helper thuần, có unit test riêng), ghi nhãn `FinalGrade` (qua `AssessmentSheetRules.GradeLabel`, có unit test) bằng `Values.BatchUpdate`; nếu bất kỳ mã mục/mã học sinh nào không dò được vị trí, **không ghi phần nào cả** và throw lỗi rõ (đúng yêu cầu "không để nửa vời" ở requirements 09 mục 14). `AssessmentSheetService.SubmitResultsAsync` set `SubmissionDate` sau khi ghi thành công.
- **TẠM/CHƯA XÁC NHẬN**: định dạng cột trong sheet `data` (mục 2) là suy đoán hợp lý của tôi (Mã mục/Tên mục/Nhóm 1-2-3/Kế hoạch/Ghi chú kế hoạch/Kết quả/Ghi chú kết quả, header dòng 1, dữ liệu từ dòng 2), **chưa có mapping thật từ đội vận hành** (requirements 09 mục 15). Người dùng đã xác nhận chấp nhận rủi ro này (chọn "Dùng mapping tạm suy đoán hợp lý" khi được hỏi) — sửa lại `WriteRecordsToSheetAsync`/`BuildHeaderRow` trong `GoogleSheetsService.cs` khi có mapping chính thức. Vị trí ghi `[F0.ĐG]` (mục 3) và bảng nhãn `GradeLabel` thì **có mapping thật đầy đủ**, không phải suy đoán.
## Mục 4 hoàn tất (2026-08-20, sau khi hỏi lại người dùng)

Người dùng xác nhận cấu trúc cột thật của `_data_DG` (khác `_data_DG_only_item`):

- Sheet `_data_DG`, header dòng 7, dữ liệu từ dòng 9. Mỗi dòng là một cặp (học sinh, mục đánh giá, kết quả) — dạng bảng dài (long format), không phải ma trận như `[F0.ĐG]`.
- Cột (đọc theo tên header, cùng cơ chế `ReadHeaderMappingsAsync` đã có sẵn cho `_data_DG_only_item`): `ma_hs` = mã học sinh, `item_id` = mã mục đánh giá (khớp `Assessment.Code`), `ket_qua` = nhãn kết quả — **cùng 4 nhãn đã xác nhận ở mục 3** (`Đạt +`/`Chưa đạt -`/`Hỗ trợ +`/`Hỗ trợ -`), không phải chữ cái A/B/C/D. Đây là mapping **thật, đã xác nhận**, không phải suy đoán.

Đã implement `GoogleSheetsService.ReadLatestResultsAsync` (đọc `_data_DG`) và mở rộng `SyncAssessmentsAsync`:

- Thêm `AssessmentSheetRules.TryParseGradeLabel` (chiều ngược của `GradeLabel`, cùng bảng, đảm bảo nhất quán 2 chiều — có unit test).
- **Phát hiện quan trọng khi code**: `AssessmentRecordLatest` có FK `Restrict` (không cascade) tới cả `Assessment` lẫn `AssessmentSheetLatest`. `SyncAssessmentsAsync` cũ xoá toàn bộ `Assessment` bằng `ExecuteDeleteAsync` mỗi lần chạy — nếu thêm `AssessmentRecordLatest` mà không đổi thứ tự xoá, lần chạy **thứ hai trở đi** sẽ vỡ ràng buộc khoá ngoại. Đã sửa thứ tự: xoá `AssessmentRecordLatest` (con) trước, rồi `AssessmentSheetLatest` và `Assessment` (cha), rồi mới insert lại cả 3 theo thứ tự cha trước con, dùng `Id` mới của `Assessment`/`AssessmentSheetLatest` vừa tạo trong cùng lượt chạy (không cần round-trip DB để tra lại).
- Học sinh trong `_data_DG` không khớp `Student.StudentCode` nào trong DB, hoặc mục đánh giá không khớp `Assessment.Code` nào vừa đồng bộ, hoặc nhãn kết quả không khớp bảng mapping → bỏ qua dòng đó (best-effort, không làm fail cả lượt sync) — khác nguyên tắc "throw nếu nửa vời" của `submit-results`, vì đây là đồng bộ hàng loạt nhiều học sinh/mục, không phải một hành động đơn của người dùng trên một `AssessmentSheet`.
- `AssessmentSheetLatest.Name` đặt cố định `"Kết quả gần nhất"` (field này không có ý nghĩa nghiệp vụ hiển thị, không API/UI nào đọc nó — không phải giá trị người dùng xác nhận, chỉ là default hợp lý).

Verification: `dotnet build src/AdminPortal.Api/AdminPortal.Api.csproj -c Release` → 0/0; `dotnet test tests/AdminPortal.UnitTests -c Release` → 83/83 pass (thêm `TryParseGradeLabel` 5 test case); `dotnet-ef migrations has-pending-model-changes --configuration Release` → sạch. **Vẫn chưa gọi Google API thật lần nào** (kể cả đọc `_data_DG`) — người dùng đã xác nhận chưa cần chạy live lượt này (xem `.agents/backend/MEMORY.md`).

`ASH-BE-03` giờ đã đủ cả 4 mục theo DoD ban đầu (chỉ còn thiếu bước chạy live để xác nhận thật — lý do duy nhất chưa đánh `[x]` trong `status.md`).
