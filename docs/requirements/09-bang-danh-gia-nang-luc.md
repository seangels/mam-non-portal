# 09 — Bảng đánh giá năng lực (AssessmentSheet)

> Sơ đồ luồng dữ liệu (Mermaid): [09-bang-danh-gia-nang-luc-so-do-du-lieu.md](09-bang-danh-gia-nang-luc-so-do-du-lieu.md).

## 1. Mục tiêu và phạm vi

- Cho phép giáo viên tạo một **Bảng đánh giá năng lực** (`AssessmentSheet`) cho một học sinh trong một đợt đánh giá cụ thể, dựa trên kho mục đánh giá (`Assessment`) và dữ liệu kết quả gần nhất đọc từ Google Sheet (`AssessmentSheetLatest`/`AssessmentRecordLatest`).
- Bảng đánh giá gồm nhiều mục đánh giá được chọn (`AssessmentRecord`), mỗi mục có **hai cặp field tách biệt**: `PlanGrade`/`PlanNote` (giai đoạn lập kế hoạch — khởi tạo từ `latestGrade`/`note` mà UI gửi kèm mỗi mục được chọn) và `FinalGrade`/`FinalNote` (kết quả đánh giá cuối cùng, nhập sau). Hai cặp này độc lập với nhau — sửa `FinalGrade` không đổi `PlanGrade`, và ngược lại.
- Toàn bộ vòng đời gắn với 3 loại tài liệu ngoài hệ thống:
  - **[F01]** — file Google Sheet **riêng của từng `AssessmentSheet`**, tạo ra bằng cách **copy toàn bộ file mẫu `gen_assessment_sheet`** (không phải tạo/copy từng sheet lẻ trong một file dùng chung). File `[F01]` này đã có sẵn 3 sheet ngay từ lúc copy: `data`, `khcn_template` (kế hoạch cá nhân — dùng `PlanGrade`/`PlanNote`), `KQ_template` (kết quả — dùng `FinalGrade`/`FinalNote`) — vì đó là bản sao nguyên vẹn của file mẫu. Mọi thao tác cập nhật dữ liệu, sinh PDF đều diễn ra trên `[F01]`, **không bao giờ đụng trực tiếp vào file mẫu `gen_assessment_sheet`**.
  - **[F02]** file PDF kế hoạch cá nhân, sinh từ sheet `khcn_template` trong `[F01]` (dữ liệu `PlanGrade`/`PlanNote`).
  - **[F03]** file PDF kết quả đánh giá, sinh từ sheet `KQ_template` trong `[F01]` (dữ liệu `FinalGrade`/`FinalNote`).
- Kết quả sau khi hoàn tất còn được ghi ngược vào **[F0.ĐG]** (sheet `ĐG` của file nguồn `F0`, cùng file chứa `_data_DG_only_item` hiện đang dùng để đồng bộ `Assessment`) — ghi `FinalGrade`. Ngược lại, kho mục đánh giá (`Assessment`) và dữ liệu kết quả gần nhất (`AssessmentSheetLatest`/`AssessmentRecordLatest`) được **nạp lại (ghi đè, chỉ đọc)** từ `[F0.data_DG]`.
- Tài liệu này mô tả **yêu cầu nghiệp vụ** của tính năng; không mô tả entity, migration hay kế hoạch triển khai mã nguồn.

## 2. Vai trò và quyền

- `Teacher`, `Admin`, `SuperAdmin` đều được tạo, xem và thao tác `AssessmentSheet` cho **bất kỳ học sinh nào**; không giới hạn theo nhóm đang phụ trách (khác với Điểm danh — xem [05](05-diem-danh.md) — vốn giới hạn Teacher theo nhóm).
- Đồng bộ dữ liệu gốc từ Google Sheets (`Assessment`, `AssessmentSheetLatest`/`AssessmentRecordLatest` từ `[F0.data_DG]`) mở cho cả `SuperAdmin`, `Admin`, `Teacher`. Chính sách `PortalManagers` đang áp cho endpoint `sync-assessments` hiện có (giới hạn `Admin`/`SuperAdmin`) sẽ được điều chỉnh để cho phép `Teacher` cùng chạy đồng bộ này; đây là thay đổi chung của endpoint, không tạo endpoint riêng.
- Xem danh mục `Assessment`/`AssessmentGroup` để chọn plan vẫn mở cho cả `SuperAdmin`, `Admin`, `Teacher` như hiện tại.
- Khi danh mục `Assessment` được đọc kèm dữ liệu gần nhất theo `studentId`, dữ liệu latest là dữ liệu gắn với học sinh cụ thể: `Admin`/`SuperAdmin` xem được mọi học sinh; `Teacher` chỉ xem được latest của học sinh thuộc nhóm hiện đang phụ trách. Danh sách trả về vẫn phải có đủ mục đánh giá theo filter, kể cả khi học sinh chưa có `AssessmentSheetLatest` hoặc mục đó chưa có `AssessmentRecordLatest`.

## 3. Thuật ngữ

- **Đợt đánh giá:** một chu kỳ đánh giá năng lực có tên riêng (`AssessmentSheet.Name`, ví dụ `8.9.10.26` cho đợt tháng 8–9–10/2026).
- **Kho mục đánh giá (`Assessment`):** danh mục mục đánh giá dùng chung, có `Code`, `Name`, `GroupLv1Name`/`GroupLv2Name`/`GroupLv3Name` (phân cấp độ tuổi/nhóm kỹ năng), `RowIndex`; được đồng bộ từ `[F0.data_DG]`.
- **Kết quả gần nhất, chỉ đọc (`AssessmentSheetLatest`/`AssessmentRecordLatest`):** một cặp bảng mirror — `AssessmentSheetLatest` (theo học sinh) chứa `AssessmentRecordLatest` (theo từng mục đánh giá, có `LatestGrade`/ghi chú latest — không tách plan/final vì đây chỉ là dữ liệu nguồn tham chiếu) — được nạp/ghi đè **duy nhất** bởi luồng đồng bộ từ `[F0.data_DG]` (mục 12). Không có thao tác nào khác trong hệ thống được phép ghi vào 2 bảng này; chúng chỉ tồn tại để **hiển thị gợi ý gần nhất trên UI**, rồi UI gửi kèm dữ liệu đó khi tạo `AssessmentSheet`/`AssessmentRecord` mới.
- **Mục đánh giá trong bảng (`AssessmentRecord`):** một dòng trong `AssessmentSheet`, snapshot lại thông tin mục đánh giá (`AssessmentSnapshot`: mã, tên, nhóm, `RowIndex`) và có **hai cặp field độc lập**:
  - `PlanGrade`/`PlanNote` — giai đoạn lập kế hoạch. Hai field này khởi tạo từ `latestGrade`/`note` mà UI gửi trong request tạo mới cho từng mục đã chọn (dữ liệu UI lấy từ cột kết quả/ghi chú gần nhất), sau đó giáo viên có thể sửa lại trong lúc hoàn thiện plan (mục 7). Phục vụ sheet `khcn_template`/PDF `[F02]`.
  - `FinalGrade`/`FinalNote` — kết quả đánh giá thật, nhập ở bước riêng sau khi đánh giá xong (mục 9), độc lập hoàn toàn với `PlanGrade` (sửa cái này không đổi cái kia). Phục vụ sheet `KQ_template`/PDF `[F03]` và là giá trị ghi vào `[F0.ĐG]` (mục 11).
- **Bảng đánh giá năng lực (`AssessmentSheet`):** hồ sơ một đợt đánh giá của một học sinh, gồm danh sách `AssessmentRecord` đã chọn, trạng thái, mốc thời gian, `AssessmentSheetSpreadsheetId` (id file `[F01]` riêng của bảng này) và liên kết tới `[F02]`/`[F03]`.
- **`[F0]`:** file Google Sheet nguồn hiện có (sheet `_data_DG_only_item`), là nơi `Assessment` được đồng bộ vào hệ thống; `[F0.data_DG]` là vùng dữ liệu dùng để nạp lại `Assessment`/`AssessmentSheetLatest`/`AssessmentRecordLatest`; `[F0.ĐG]` là sheet `ĐG` trong cùng file dùng để ghi kết quả đánh giá đã hoàn tất.
- **File mẫu `gen_assessment_sheet`:** file Google Sheet cố định, **không bao giờ bị chỉnh sửa trực tiếp**. Drive file id: `12ClFCOFCfUJJ1i8QstHweNaSdLfY-1MB2eaCuigqWwQ`. Chứa 3 sheet mẫu:

  | Sheet | Vai trò | `gid` |
  |---|---|---|
  | `data` | Mẫu cấu trúc dữ liệu thô (cả `Plan*` và `Final*`) | `0` |
  | `khcn_template` | Mẫu kế hoạch cá nhân — dữ liệu `PlanGrade`/`PlanNote` (nguồn sinh `[F02]`) | `1320805599` |
  | `KQ_template` | Mẫu kết quả đánh giá — dữ liệu `FinalGrade`/`FinalNote` (nguồn sinh `[F03]`) | `1903920808` |

  Vì Google Drive giữ nguyên `gid` nội bộ khi copy toàn bộ file, mọi file `[F01]` (bản copy) đều có đúng 3 sheet này với cùng `gid` như trên — hệ thống có thể dùng thẳng các `gid` cố định này để đọc/ghi mà không cần tra cứu lại theo tên mỗi lần.
- **`[F01]`:** file Google Sheet **riêng của một `AssessmentSheet` cụ thể**, tạo ra bằng Drive file copy từ file mẫu `gen_assessment_sheet` (mục 6). Id của file này được lưu vào `AssessmentSheet.AssessmentSheetSpreadsheetId`. `[F01.data]`/`[F01.khcn]`/`[F01.KQ]` trong tài liệu này chỉ đến 3 sheet `data`/`khcn_template`/`KQ_template` bên trong đúng file `[F01]` đó.

## 4. Vòng đời và trạng thái AssessmentSheet

| Trạng thái | Ý nghĩa |
|---|---|
| `Open` | Mới tạo, đang trong quá trình lập kế hoạch và/hoặc đánh giá. |
| `Done` | Giáo viên đã đánh dấu hoàn tất đợt đánh giá cho học sinh này. |

- `AssessmentSheet` không tự chuyển trạng thái; chuyển sang `Done` là thao tác thủ công của giáo viên/quản trị viên phụ trách.
- Trường thời gian:
  - `StartDate`: ngày bắt đầu đợt đánh giá.
  - `DueDate`: hạn hoàn thành đợt đánh giá.
  - `DoneDate`: thời điểm được đánh dấu `Done`; chỉ có giá trị khi trạng thái là `Done`.
  - `SubmissionDate`: thời điểm nộp/bàn giao kết quả (ví dụ gửi cho phụ huynh); độc lập với `DoneDate`.
- Liên kết tài liệu: `AssessmentSheet` lưu `AssessmentSheetSpreadsheetId` (id file `[F01]`, để trống tới khi lần đầu bấm "Xuất sang Google Sheet"/"Sinh PDF") và đường dẫn tới file `[F02]`/`[F03]` sau khi được sinh.
- Khi `Status = Done`, hệ thống khoá chỉnh sửa: không cho đổi plan (thêm/bớt `AssessmentRecord`), không cho sửa `PlanGrade`/`PlanNote`/`FinalGrade`/`FinalNote` của bất kỳ mục nào và không cho sửa `Feedback` tổng của `AssessmentSheet`. Muốn chỉnh sửa tiếp phải chuyển trạng thái về `Open` trước.
- Chuyển `Done` → `Open` là thao tác đơn giản đổi `Status`, mọi vai trò (`Teacher`/`Admin`/`SuperAdmin`) đều được phép, không yêu cầu nhập lý do hay ghi log riêng.

## 5. Tạo AssessmentSheet và chọn plan

- Giáo viên chọn một học sinh (bất kỳ, không giới hạn theo nhóm phụ trách) và đặt `Name` cho đợt đánh giá.
- Hệ thống snapshot thông tin học sinh tại thời điểm tạo (`StudentSnapshot`: mã học sinh, họ tên, tên gọi, ngày sinh, giới tính) để `AssessmentSheet` không đổi theo khi hồ sơ học sinh gốc thay đổi sau này.
- Giáo viên chọn các mục đánh giá (`plan`) từ kho `Assessment` để đưa vào bảng. Màn hình chọn plan hỗ trợ các bộ lọc sau (kết hợp được với nhau):
  - **Theo học sinh:** lấy thêm `LatestGrade`/ghi chú latest gợi ý dựa trên `AssessmentRecordLatest` (đọc-only, đã fetch từ Google Sheet) của đúng học sinh đang tạo bảng. Việc thiếu dữ liệu latest không được làm ẩn mất mục đánh giá; các field latest để trống/null.
  - **Theo mức grade:** ví dụ lọc các mục có kết quả gần nhất `LatestGrade >= B`, dùng thang xếp hạng đã chốt `A > B > C > D` (`A` cao nhất, `D` thấp nhất); dùng để khoanh vùng các mục học sinh đã đạt mức nhất định hoặc ngược lại cần cải thiện.
  - **Theo kết quả gần nhất trên UI:** TagBox đứng đầu panel filter, cho chọn nhiều giá trị gồm `Chưa có`, `Đạt +`, `Chưa đạt -`, `Hỗ trợ +`, `Hỗ trợ -`. `Chưa có` đại diện cho mục chưa có `LatestGrade`. Filter này chạy trên dữ liệu đã tải về client và trên snapshot hiện hành của chế độ xem (`Xem tất cả` hoặc `Chỉ những mục đã chọn`), không tự gọi lại server.
  - **Theo nhóm phân cấp:** lọc theo `GroupLv1Name`, `GroupLv2Name`, `GroupLv3Name` của `Assessment`.
- Khi bấm tạo mới, UI gửi danh sách `records`, mỗi phần tử gồm `assessmentId`, `latestGrade`, `note`; không chỉ gửi mỗi `assessmentId`. Với mỗi mục được chọn, hệ thống:
  - Snapshot thông tin mục đánh giá vào `AssessmentRecord.AssessmentSnapshot` (mã, tên, các cấp nhóm, `RowIndex`).
  - Khởi tạo `PlanGrade` của `AssessmentRecord` bằng `latestGrade` trong request; nếu UI gửi `null` thì `PlanGrade` để trống.
  - Khởi tạo `PlanNote` bằng `note` trong request; nếu UI gửi trống/null thì `PlanNote` để trống.
  - `FinalGrade`/`FinalNote` để trống — chỉ được nhập sau ở bước riêng (mục 9).
- Sau bước khởi tạo, `PlanGrade` là field mutable mà giáo viên có thể sửa tiếp khi hoàn thiện plan (mục 7); sửa `PlanGrade` không ảnh hưởng ngược tới `AssessmentRecordLatest`.
- Một `AssessmentSheet` có thể có nhiều `AssessmentRecord`; không giới hạn số lượng mục tối thiểu/tối đa.
- **Chưa cần tạo file `[F01]` ở bước này.** Việc copy file mẫu chỉ xảy ra khi lần đầu tiên một hành động cần tới `[F01]` được gọi (Xuất sang Google Sheet, hoặc Sinh PDF nếu giáo viên bấm PDF trước) — xem mục 6.

## 6. File riêng [F01] và cập nhật sheet `data`

- **Tạo file `[F01]` (lazy, tự động, chỉ một lần cho mỗi `AssessmentSheet`):** hành động đầu tiên cần tới `[F01]` (thường là "Xuất sang Google Sheet", nhưng "Sinh PDF F02"/"F03" cũng có thể là hành động đầu tiên nếu giáo viên bấm PDF trước) sẽ kiểm tra `AssessmentSheetSpreadsheetId`:
  - Nếu **chưa có**: copy toàn bộ file mẫu `gen_assessment_sheet` (Drive file copy) → được file `[F01]` mới, đã có sẵn 3 sheet `data`/`khcn_template`/`KQ_template`. Lưu id file mới vào `AssessmentSheetSpreadsheetId`.
  - Nếu **đã có**: dùng thẳng file đó, không copy lại.
  - Nhờ vậy các hành động không bắt buộc phải theo đúng thứ tự bấm nút; hành động nào cũng tự đảm bảo `[F01]` tồn tại trước khi làm việc tiếp.
- **"Xuất sang Google Sheet"** (nút bấm thủ công): đảm bảo `[F01]` tồn tại (tạo nếu chưa có theo trên), sau đó ghi dữ liệu `AssessmentRecord` hiện tại (snapshot + `PlanGrade`/`PlanNote` + `FinalGrade`/`FinalNote`) vào sheet `data` (`gid=0`) của `[F01]` — sheet `data` là bản dump đầy đủ cả hai cặp field.
- **"Đồng bộ"** (nút bấm thủ công, dùng sau khi sửa plan): ghi đè lại sheet `data` của `[F01]` từ trạng thái `AssessmentRecord` hiện tại trên portal — một chiều portal → sheet, không đọc ngược.
- Sheet `khcn_template`/`KQ_template` trong `[F01]` **không** cần thao tác gì ở bước này — chúng chỉ được điền dữ liệu khi giáo viên bấm "Sinh PDF" tương ứng (mục 8, 10).
- Định dạng chi tiết từng cột trong sheet `data`/`khcn_template`/`KQ_template` (map với field nào của `AssessmentRecord`/`AssessmentSheet`) sẽ được bổ sung sau (xem mục 15).

## 7. Chỉnh sửa plan sau khi tạo

- Sau khi tạo (áp dụng cả khi `AssessmentSheet` đang ở trạng thái `Open`; khi `Done` thì bị khoá theo mục 4), giáo viên có thể:
  - Chọn lại danh sách mục đánh giá (thêm/bớt `AssessmentRecord`).
  - Thay đổi `PlanGrade`/`PlanNote` của một `AssessmentRecord` (ví dụ sửa lại cho đúng thực tế trước khi đánh giá) — **không đụng tới `FinalGrade`/`FinalNote`**, hai cặp field hoàn toàn độc lập.
- Các thay đổi này **không được ghi ngược** vào `AssessmentSheetLatest`/`AssessmentRecordLatest`; hai bảng đó là chỉ-đọc, chỉ được cập nhật qua luồng đồng bộ từ `[F0.data_DG]` (mục 12) — không qua thao tác chỉnh sửa plan của một `AssessmentSheet` cụ thể, và cũng không qua việc ghi kết quả chính thức ở mục 11.
- Đồng bộ thay đổi plan lên sheet `data` của `[F01]` **không tự động**; giáo viên bấm nút "Đồng bộ" riêng (mục 6).

## 8. Sinh PDF [F02] — Kế hoạch cá nhân

- Khi người dùng bấm nút sinh PDF (không tự động), hệ thống thực hiện tuần tự:
  1. Đảm bảo `[F01]` tồn tại (copy file mẫu nếu đây là hành động đầu tiên cần `[F01]` — mục 6).
  2. Ghi dữ liệu hiện tại (mục đánh giá + `PlanGrade`/`PlanNote`) vào sheet `khcn_template` (`gid=1320805599`) của `[F01]`, chỉnh format/merge cell cho khớp nội dung nếu cần. Không cần copy/tạo sheet mới vì sheet này đã có sẵn trong `[F01]` từ lúc file được tạo.
  3. Export chính sheet `khcn_template` đó (theo `gid`) thành file PDF.
- Link tới file PDF này được lưu vào field `PlanFileLinkPdf` của `AssessmentSheet`.
- PDF `[F02]` phản ánh plan (mục đánh giá + `PlanGrade`/`PlanNote` hiện tại) tại thời điểm sinh; chỉ giữ **bản mới nhất** — mỗi lần bấm sinh lại sẽ lặp lại bước 2–3 (ghi đè dữ liệu, export lại) và ghi đè link `PlanFileLinkPdf` hiện tại, không giữ lịch sử các bản PDF cũ.
- Biến thể UI-preview mới (`ASH-FE-11`): màn edit có nút `In Kế hoạch PDF` mở trang preview HTML/A4 trước, dùng `html2pdf.js` để tạo blob PDF từ DOM preview. Nút `Mở PDF` chỉ mở blob URL để xem/in; nút `Tạo PDF lên Google Drive` upload PDF do UI tạo qua `POST /api/v1/assessment-sheets/{id}/upload-plan-pdf`, lưu vào `Student.DriveFolderId` và cập nhật `PlanFileLinkPdf`. Luồng này không gọi `generate-plan-pdf` cũ và không phụ thuộc việc tạo/ghi `[F01]`; nếu học sinh chưa có Drive folder id thì báo lỗi rõ.

## 9. Nhập kết quả đánh giá

- Với mỗi `AssessmentRecord`, giáo viên nhập `FinalGrade` (kết quả đánh giá thật, để trống cho tới bước này) và có thể nhập `FinalNote`/feedback riêng cho mục đó (tùy chọn) — **độc lập hoàn toàn** với `PlanGrade`/`PlanNote` đã nhập ở bước lập kế hoạch (mục 7); nhập `FinalGrade` không đổi `PlanGrade`.
- `AssessmentSheet` có `Feedback` tổng cho toàn bộ đợt đánh giá (tùy chọn).
- Nhập kết quả là thao tác trong phạm vi `AssessmentSheet`/`AssessmentRecord` hiện có; không ghi gì vào `AssessmentSheetLatest`/`AssessmentRecordLatest` tại bước này — hai bảng đó chỉ được ghi bởi luồng nạp lại ở mục 12 (xem mục 11 cho việc chính thức hoá kết quả về phía file nguồn `[F0]`).

## 10. Sinh PDF [F03] — Kết quả đánh giá

- Cùng cơ chế như `[F02]` (mục 8), áp dụng cho sheet `KQ_template` (`gid=1903920808`) của `[F01]`: đảm bảo `[F01]` tồn tại, ghi `FinalGrade`/`FinalNote`/feedback hiện tại vào sheet (chỉnh format/merge nếu cần), rồi export sheet đó thành PDF. Cho phép sinh PDF ngay cả khi còn `AssessmentRecord` chưa có `FinalGrade`; các mục còn thiếu hiển thị trống trên PDF.
- Link tới file PDF này được lưu vào field `ResultFileLinkPdf` của `AssessmentSheet`; chỉ giữ **bản mới nhất**, mỗi lần sinh lại lặp lại chu trình ghi đè–export và ghi đè link hiện tại.
- Biến thể UI-preview mới (`ASH-FE-12`): màn edit có nút `In Kết Quả PDF`, chỉ bật khi bảng đã lưu, có record và trạng thái đã lưu khác `Open`. Nút mở trang preview HTML/A4 trước, dùng `FinalGrade`/`FinalNote`, sau đó cho `Mở PDF` bằng blob URL hoặc `Tạo PDF lên Google Drive` bằng cách upload PDF do UI tạo qua `POST /api/v1/assessment-sheets/{id}/upload-result-pdf`. Backend lưu file vào `Student.DriveFolderId` và cập nhật `ResultFileLinkPdf`. Luồng này không gọi `generate-result-pdf` cũ và không phụ thuộc việc tạo/ghi `[F01]`; nếu học sinh chưa có Drive folder id thì báo lỗi rõ.

## 11. Ghi kết quả vào [F0.ĐG]

- Khi người dùng bấm nút `Cập nhật Kết Quả`, hệ thống ghi **nhãn** của `FinalGrade` (không phải chữ cái `A/B/C/D` — xem bảng mapping bên dưới, và **không phải** `PlanGrade`) của từng mục đánh giá vào sheet `ĐG` của file nguồn `[F0]` (`[F0.ĐG]`), theo đúng học sinh và mục đánh giá tương ứng. Đây là thao tác thủ công riêng, không tự chạy kèm các bước khác.
- Trên UI v1, nút `Cập nhật Kết Quả` chỉ xuất hiện khi `AssessmentSheetStatus = Done`; nếu người dùng role `Teacher` thì nút vẫn bị disable. Đây là giới hạn UI-only; backend endpoint `submit-results` vẫn giữ quyền hiện tại và không chặn riêng role `Teacher`.
- Trước khi ghi, hệ thống phải đọc giá trị hiện tại của từng ô ResultSource cần cập nhật và chỉ ghi những ô có thay đổi. Nếu giá trị mới trùng giá trị hiện tại thì bỏ qua cell đó để tránh phát sinh ghi/audit nhiễu.
- Mỗi cell thật sự được ghi phải có `AuditLog` riêng, tối thiểu lưu vị trí ô/range, giá trị hiện tại, giá trị mới, `studentCode`, `studentName`, `studentId`, `assessmentSheetId`, `startDate`, `dueDate`, `FinalGrade`, `FinalNote`, thông tin mục đánh giá và thời gian/actor. Audit không được lưu token, credential, file bytes hay dữ liệu nhạy cảm không cần thiết.
- `FinalNote` cũng thuộc phạm vi cần cập nhật ra ResultSource khi có thay đổi. Cột `FinalNote` nằm ngay bên phải cột kết quả của học sinh; cột kết quả là cột có `studentCode` ở hàng định vị mã học sinh, còn ô cùng hàng ở cột `FinalNote` để trống.
- **Cách định vị ô cần ghi** (đã xác nhận với người dùng):
  - Cột **`E16:E`**: chứa mã mục đánh giá (`item_id`), mỗi dòng một mã, bắt đầu từ dòng 16 — dò cột này để tìm đúng **dòng**.
  - Hàng **`H16:16`**: chứa mã học sinh, mỗi cột một mã, bắt đầu từ cột H — dò hàng này để tìm đúng **cột**.
  - Ô cần ghi là giao điểm của dòng tìm được (theo mã mục đánh giá) và cột tìm được (theo mã học sinh).
- **Bảng mapping `FinalGrade` → nhãn ghi vào `[F0.ĐG]`** (đã xác nhận với người dùng — ghi nguyên văn, không tự suy diễn lại thứ tự):

  | `FinalGrade` | Nhãn ghi vào `[F0.ĐG]` |
  |---|---|
  | `A` | `Đạt +` |
  | `B` | `Chưa đạt -` |
  | `C` | `Hỗ trợ +` |
  | `D` | `Hỗ trợ -` |

  Mapping này nên dùng thống nhất ở mọi nơi hiển thị `FinalGrade`/`PlanGrade` cho người dùng cuối (UI, PDF `[F02]`/`[F03]`), không chỉ riêng khi ghi `[F0.ĐG]`, để tránh vừa hiện "A/B/C/D" vừa hiện nhãn tiếng Việt ở hai chỗ khác nhau. **Lưu ý:** cặp `B` → `Chưa đạt -` trông không đối xứng với 3 dòng còn lại (`A`/`C`/`D` đều là biến thể `+`/`-` của cùng một khái niệm "Đạt"/"Hỗ trợ", còn `B` nhảy sang khái niệm "Chưa đạt"); nên xác nhận lại một lần nữa với đội vận hành trước khi dùng mapping này để ghi dữ liệu thật, đề phòng sai sót khi nhập.
- Đây là bước ghi kết quả về file nguồn dùng chung toàn trường/toàn khối, khác với `[F01.KQ]` là bản ghi riêng cho một đợt của một học sinh.
- Ngay khi thao tác này thực hiện thành công, hệ thống set `SubmissionDate` của `AssessmentSheet` bằng thời điểm cập nhật.
- Việc ghi vào `[F0.ĐG]` **không** tự động nạp lại `AssessmentSheetLatest`/`AssessmentRecordLatest`; đây là hai bước tách biệt — nạp lại là một thao tác thủ công riêng, xem mục 12.

## 12. Nạp lại Assessment và AssessmentSheetLatest/AssessmentRecordLatest từ [F0.data_DG]

- `Teacher`, `Admin`, `SuperAdmin` đều được chạy đồng bộ (thao tác thủ công, không tự động) để nạp lại `Assessment` (kho mục đánh giá) và `AssessmentSheetLatest`/`AssessmentRecordLatest` (kết quả gần nhất theo học sinh, chỉ-đọc) từ vùng dữ liệu `[F0.data_DG]`, qua chính endpoint `sync-assessments` hiện có sau khi mở rộng chính sách quyền (mục 2).
- Hành vi kế thừa cơ chế `sync-assessments` hiện có cho `Assessment` (đọc toàn bộ, thay thế dữ liệu hiện tại); với `AssessmentSheetLatest`/`AssessmentRecordLatest`, đồng bộ đọc và **ghi đè hoàn toàn** dữ liệu đọc-only theo học sinh + mục đánh giá — không được suy diễn chi tiết thuật toán trong tài liệu này.
- Đây là **bảng chỉ đọc dành riêng cho mục đích hiển thị dữ liệu gần nhất/prefill trên UI**: không service/luồng nào khác trong hệ thống được phép ghi vào `AssessmentSheetLatest`/`AssessmentRecordLatest` ngoài luồng đồng bộ này.
- Đồng bộ nguồn không được tự ý sửa `AssessmentRecord` đã snapshot trong các `AssessmentSheet` đang tồn tại (đảm bảo nguyên tắc "không đổi giá trị gốc" ở mục 7 vẫn đúng theo chiều ngược lại).

## 13. Trường dữ liệu chính của AssessmentSheet

| Field | Bắt buộc | Mô tả |
|---|---|---|
| `Name` | Có | Tên đợt đánh giá. |
| `Status` | Có | `Open` hoặc `Done`. |
| `Student` | Có | Học sinh được đánh giá. |
| `StudentSnapshot` | Có | Snapshot hồ sơ học sinh tại thời điểm tạo. |
| `ResponsibleTeacher` | Không | Giáo viên phụ trách đợt đánh giá; có thể khác giáo viên đang phụ trách nhóm hiện tại. Tên giáo viên cũng được snapshot riêng. |
| `StartDate` | Không | Ngày bắt đầu đợt. |
| `DueDate` | Không | Hạn hoàn thành. |
| `DoneDate` | Không | Thời điểm đánh dấu `Done`. |
| `SubmissionDate` | Không | Thời điểm nộp/bàn giao kết quả; được hệ thống set tự động khi bấm nút cập nhật kết quả vào `[F0.ĐG]` (mục 11). |
| `AssessmentSheetSpreadsheetId` | Không | Id file Google Sheet `[F01]` riêng của bảng này — copy từ file mẫu `gen_assessment_sheet`; để trống tới khi hành động đầu tiên cần `[F01]` được gọi (mục 6). |
| `PlanFileLinkPdf` | Không | Đường dẫn PDF kế hoạch cá nhân (`[F02]`) sau khi sinh. |
| `ResultFileLinkPdf` | Không | Đường dẫn PDF kết quả đánh giá (`[F03]`) sau khi sinh. |
| `Feedback` | Không | Nhận xét tổng cho toàn đợt. |
| `Note` | Không | Ghi chú nội bộ. |

Mỗi `AssessmentRecord` trong `AssessmentSheet` có: snapshot mục đánh giá (`AssessmentSnapshot`), `PlanGrade`/`PlanNote` (giai đoạn kế hoạch — khởi tạo từ `latestGrade`/`note` mà UI gửi kèm record tạo mới, sau đó giáo viên chỉnh sửa trực tiếp khi hoàn thiện plan), `FinalGrade`/`FinalNote` (kết quả đánh giá thật, nhập ở bước riêng sau — mục 9). Hai cặp field độc lập, không cặp nào ghi đè cặp còn lại.

### Cặp bảng chỉ-đọc AssessmentSheetLatest / AssessmentRecordLatest

Cấu trúc mirror theo hình dạng `AssessmentSheet`/`AssessmentRecord` (theo học sinh → theo từng mục đánh giá), nhưng:

- Chỉ được ghi bởi luồng đồng bộ ở mục 12; không có API/UI nào cho phép sửa trực tiếp.
- `AssessmentRecordLatest.LatestGrade` là **field đơn** (không tách plan/final vì đây chỉ là dữ liệu nguồn tham chiếu) — dùng để UI hiển thị/lọc kết quả gần nhất và gửi lại trong `records[].latestGrade` khi tạo `AssessmentRecord` mới (mục 5).
- `AssessmentRecordLatest` liên kết trực tiếp tới `Assessment` bằng `AssessmentId`/`Assessment`; khoá duy nhất theo `AssessmentSheetLatestId` + `AssessmentId` để xác định đúng dòng khi đồng bộ ghi đè theo từng học sinh + mục đánh giá. Bản trung gian từng có field kỹ thuật `AssessmentCode` đã lỗi thời và không còn là contract hiện hành.
- Không có `SubmissionDate`, `AssessmentSheetSpreadsheetId`, `PlanFileLinkPdf`, `ResultFileLinkPdf` — các field gắn với vòng đời làm việc/file `[F01]` của một `AssessmentSheet` thật không áp dụng cho bảng chỉ-đọc này.
- Bị ghi đè mỗi lần đồng bộ lại; không phải nơi lưu lịch sử theo thời gian, chỉ phản ánh trạng thái tại lần fetch gần nhất.

## 14. Ràng buộc và validation

- Mỗi đợt đánh giá của một học sinh ứng với đúng một `AssessmentSheet` và một `Name`; `Name` bắt buộc và duy nhất trong phạm vi một học sinh. Nhiều học sinh khác nhau có thể dùng chung một `Name` đợt (ví dụ cùng đợt `8.9.10.26` nhưng mỗi học sinh có `AssessmentSheet` và file `[F01]` riêng).
- Không cho tạo `AssessmentSheet` cho học sinh `Inactive` (nhất quán với ràng buộc học sinh `Inactive` không được thêm dữ liệu nghiệp vụ mới, xem [03](03-hoc-sinh-va-nhom.md)).
- `DoneDate` chỉ được set khi `Status = Done`; chuyển trạng thái phải đi kèm cập nhật `DoneDate` nhất quán.
- Không giới hạn số lượng `AssessmentRecord` tối thiểu/tối đa trong một `AssessmentSheet`.
- `Teacher`/`Admin`/`SuperAdmin` đều xem/sửa được `AssessmentSheet` của mọi học sinh, không phân biệt ai là người tạo hay ai đang phụ trách nhóm nào (xem mục 2).
- Lỗi khi đọc/ghi Google Sheet (kể cả lỗi copy file mẫu) hoặc sinh PDF phải trả lỗi rõ ràng cho người dùng, không để `AssessmentSheet` ở trạng thái nửa vời (ví dụ có link PDF nhưng file thực tế chưa được tạo thành công, hoặc `AssessmentSheetSpreadsheetId` trỏ tới file không truy cập được).

## 15. Giả định đã chốt và phần còn để bổ sung

Các quyết định nghiệp vụ đã được chốt và áp dụng xuyên suốt tài liệu:

- Quyền không giới hạn theo nhóm cho `AssessmentSheet`; endpoint `sync-assessments` được điều chỉnh chính sách `PortalManagers` để cho phép cả `Teacher`.
- Chuyển `Done` → `Open` là thao tác đổi trạng thái đơn giản, mọi vai trò được phép, không cần lý do/log riêng.
- Filter chọn plan theo học sinh, theo `LatestGrade` (thang `A > B > C > D`) và theo `GroupLv1/2/3Name`.
- Mỗi `AssessmentSheet` có file `[F01]` riêng, tạo bằng cách copy toàn bộ file mẫu `gen_assessment_sheet` (id `12ClFCOFCfUJJ1i8QstHweNaSdLfY-1MB2eaCuigqWwQ`) — việc copy chỉ xảy ra một lần (lazy, tự động khi cần), lưu id vào `AssessmentSheetSpreadsheetId`. 3 sheet bên trong giữ tên cố định `data`/`khcn_template`/`KQ_template` (`gid` cố định `0`/`1320805599`/`1903920808`).
- "Xuất sang Google Sheet" và "Đồng bộ" chỉ ghi vào sheet `data` của `[F01]`. Sheet `khcn_template`/`KQ_template` chỉ được điền dữ liệu tại đúng thời điểm bấm "Sinh PDF" tương ứng.
- PDF `[F02]`/`[F03]` sinh theo nút bấm, chỉ giữ bản mới nhất; `[F03]` được phép sinh dù còn mục thiếu `FinalGrade`.
- **`AssessmentRecord` có hai cặp field độc lập: `PlanGrade`/`PlanNote` và `FinalGrade`/`FinalNote`** — đây là thiết kế đã khôi phục đúng theo quyết định "giữ lại giá trị khởi tạo phục vụ đối chiếu/audit" ban đầu (khác một phiên bản trung gian của tài liệu này từng gộp chung thành một field `Grade` duy nhất — phiên bản đó đã lỗi thời). Khi tạo mới, UI gửi `records[].latestGrade`/`records[].note` theo dữ liệu gần nhất đang hiển thị để backend lưu vào `PlanGrade`/`PlanNote`, dùng cho `[F02]`; `FinalGrade` nhập riêng sau, dùng cho `[F03]` và `[F0.ĐG]`.
- Ghi `[F0.ĐG]` (dùng `FinalGrade`) và nạp lại `AssessmentSheetLatest`/`AssessmentRecordLatest`/`Assessment` từ `[F0.data_DG]` là hai thao tác thủ công tách biệt; ghi `[F0.ĐG]` tự set `SubmissionDate`.
- **Đã xác nhận vị trí ghi `[F0.ĐG]`:** cột `E16:E` = mã mục đánh giá (dò dòng), hàng `H16:16` = mã học sinh (dò cột), ghi tại ô giao nhau; giá trị ghi là nhãn theo bảng mapping ở mục 11 (không phải chữ cái `A/B/C/D`).
- Mỗi đợt–học sinh ứng với đúng một `AssessmentSheet`/`Name`; không giới hạn số lượng `AssessmentRecord`.

Còn một phần chưa có, sẽ được bổ sung sau trước khi triển khai:

- Định dạng chi tiết từng cột trong sheet `data`/`khcn_template`/`KQ_template` (map với field nào của `AssessmentRecord`/`AssessmentSheet`) — người dùng sẽ chủ động cung cấp sau cùng đội vận hành Google Sheet.
- Xác nhận lại bảng mapping `FinalGrade` → nhãn ở mục 11 (đặc biệt cặp `B` → `Chưa đạt -`) trước khi dùng để ghi dữ liệu thật vào `[F0.ĐG]`.
