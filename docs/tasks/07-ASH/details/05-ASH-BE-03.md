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
