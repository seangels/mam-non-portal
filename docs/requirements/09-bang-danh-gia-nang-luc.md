# 09 — Bảng đánh giá năng lực (AssessmentSheet)

> Sơ đồ luồng dữ liệu (Mermaid): [09-bang-danh-gia-nang-luc-so-do-du-lieu.md](09-bang-danh-gia-nang-luc-so-do-du-lieu.md).

## 1. Mục tiêu và phạm vi

- Cho phép giáo viên tạo một **Bảng đánh giá năng lực** (`AssessmentSheet`) cho một học sinh trong một đợt đánh giá cụ thể, dựa trên kho mục đánh giá (`Assessment`) và dữ liệu kết quả gần nhất đọc từ Google Sheet (`AssessmentSheetLatest`/`AssessmentRecordLatest`).
- Bảng đánh giá gồm nhiều mục đánh giá được chọn (`AssessmentRecord`), mỗi mục có **hai cặp field tách biệt**: `PlanGrade`/`PlanNote` (giai đoạn lập kế hoạch — khởi tạo từ `latestGrade`/`note` mà UI gửi kèm mỗi mục được chọn) và `FinalGrade`/`FinalNote` (kết quả đánh giá cuối cùng, nhập sau). Hai cặp này độc lập với nhau — sửa `FinalGrade` không đổi `PlanGrade`, và ngược lại. Chỉ `PlanGrade`/`PlanNote` được tự động fill từ latest; `FinalGrade`/`FinalNote` phải để trống cho tới khi người dùng nhập.
- Toàn bộ vòng đời gắn với 2 loại tài liệu ngoài hệ thống:
  - **[F02]** file PDF kế hoạch cá nhân, render từ trang preview HTML/A4 của UI bằng `html2pdf.js`, dùng dữ liệu `PlanGrade`/`PlanNote`.
  - **[F03]** file PDF kết quả đánh giá, render từ trang preview HTML/A4 của UI bằng `html2pdf.js`, dùng dữ liệu `FinalGrade`/`FinalNote`.
- Luồng tạo/copy Google Sheet riêng cho từng `AssessmentSheet` (`[F01]`, `AssessmentSheetSpreadsheetId`, `export-to-sheet`, `sync-to-sheet`, `generate-plan-pdf`, `generate-result-pdf`) đã **ngừng sử dụng**. Nếu còn cột DB legacy thì chỉ để tương thích dữ liệu/migration cũ, không expose qua API/UI v1 và không có code mới ghi vào.
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
  - `PlanGrade`/`PlanNote` — giai đoạn lập kế hoạch. Hai field này khởi tạo từ `latestGrade`/`note` mà UI gửi trong request tạo mới cho từng mục đã chọn (dữ liệu UI lấy từ cột kết quả/ghi chú gần nhất), sau đó giáo viên có thể sửa lại trong lúc hoàn thiện plan (mục 7). Phục vụ PDF `[F02]`.
  - `FinalGrade`/`FinalNote` — kết quả đánh giá thật, nhập ở bước riêng sau khi đánh giá xong (mục 9), độc lập hoàn toàn với `PlanGrade`/`PlanNote` (sửa cái này không đổi cái kia). Không tự động fill từ latest hoặc từ kế hoạch; nếu chưa nhập thì phải giữ trống/null. Phục vụ PDF `[F03]` và là giá trị ghi vào `[F0.ĐG]` (mục 11).
- **Bảng đánh giá năng lực (`AssessmentSheet`):** hồ sơ một đợt đánh giá của một học sinh, gồm danh sách `AssessmentRecord` đã chọn, trạng thái, mốc thời gian và liên kết tới PDF `[F02]`/`[F03]`.
- **`[F0]`:** file Google Sheet nguồn hiện có (sheet `_data_DG_only_item`), là nơi `Assessment` được đồng bộ vào hệ thống; `[F0.data_DG]` là vùng dữ liệu dùng để nạp lại `Assessment`/`AssessmentSheetLatest`/`AssessmentRecordLatest`; `[F0.ĐG]` là sheet `ĐG` trong cùng file dùng để ghi kết quả đánh giá đã hoàn tất.
- **Google Sheet riêng `[F01]` (legacy):** luồng cũ từng copy file mẫu `gen_assessment_sheet` cho từng `AssessmentSheet`; hiện không còn dùng trong nghiệp vụ v1. Không tạo file `[F01]` mới, không ghi sheet `data`/`khcn_template`/`KQ_template`, không sinh PDF từ Google Sheet riêng.

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
- Liên kết tài liệu: `AssessmentSheet` lưu đường dẫn tới PDF `[F02]`/`[F03]` sau khi UI render và upload lên Google Drive của học sinh. `AssessmentSheetSpreadsheetId` nếu còn trong DB là cột legacy, không thuộc contract API/UI v1.
- Khi `Status = Done`, hệ thống khoá chỉnh sửa: không cho đổi plan (thêm/bớt `AssessmentRecord`), không cho sửa `PlanGrade`/`PlanNote`/`FinalGrade`/`FinalNote` của bất kỳ mục nào và không cho sửa `Feedback` tổng của `AssessmentSheet`. Muốn chỉnh sửa tiếp phải chuyển trạng thái về `Open` trước.
- Chuyển `Done` → `Open` là thao tác đơn giản đổi `Status`, mọi vai trò (`Teacher`/`Admin`/`SuperAdmin`) đều được phép, không yêu cầu nhập lý do hay ghi log riêng.

## 5. Tạo AssessmentSheet và chọn plan

- Giáo viên chọn một học sinh (bất kỳ, không giới hạn theo nhóm phụ trách) và đặt `Name` cho đợt đánh giá.
- Hệ thống snapshot thông tin học sinh tại thời điểm tạo (`StudentSnapshot`: mã học sinh, họ tên, tên gọi, ngày sinh, giới tính) để `AssessmentSheet` không đổi theo khi hồ sơ học sinh gốc thay đổi sau này.
- Giáo viên chọn các mục đánh giá (`plan`) từ kho `Assessment` để đưa vào bảng. Màn hình chọn plan hỗ trợ các bộ lọc sau (kết hợp được với nhau):
  - **Theo học sinh:** lấy thêm `LatestGrade`/ghi chú latest gợi ý dựa trên `AssessmentRecordLatest` (đọc-only, đã fetch từ Google Sheet) của đúng học sinh đang tạo bảng. Việc thiếu dữ liệu latest không được làm ẩn mất mục đánh giá; các field latest để trống/null.
  - **Theo mức grade:** ví dụ lọc các mục có kết quả gần nhất `LatestGrade >= B`, dùng thang xếp hạng đã chốt `A > B > C > D` — `A = 3` (cao nhất) `> B = 2 > C = 1 > D = 0` (thấp nhất); dùng để khoanh vùng các mục học sinh đã đạt mức nhất định hoặc ngược lại cần cải thiện.
  - **Theo kết quả gần nhất trên UI:** TagBox đứng đầu panel filter, cho chọn nhiều giá trị gồm `Chưa có`, `Đạt +`, `Hỗ trợ +`, `Hỗ trợ -`, `Chưa đạt -`. `Chưa có` đại diện cho mục chưa có `LatestGrade`. Filter này chạy trên dữ liệu đã tải về client và trên snapshot hiện hành của chế độ xem (`Xem tất cả` hoặc `Chỉ những mục đã chọn`), không tự gọi lại server.
  - **Theo nhóm phân cấp:** lọc theo `GroupLv1Name`, `GroupLv2Name`, `GroupLv3Name` của `Assessment`.
- Khi bấm tạo mới, UI gửi danh sách `records`, mỗi phần tử gồm `assessmentId`, `latestGrade`, `note`; không chỉ gửi mỗi `assessmentId`. Với mỗi mục được chọn, hệ thống:
  - Snapshot thông tin mục đánh giá vào `AssessmentRecord.AssessmentSnapshot` (mã, tên, các cấp nhóm, `RowIndex`).
  - Khởi tạo `PlanGrade` của `AssessmentRecord` bằng `latestGrade` trong request; nếu UI gửi `null` thì `PlanGrade` để trống.
  - Khởi tạo `PlanNote` bằng `note` trong request; nếu UI gửi trống/null thì `PlanNote` để trống.
  - `FinalGrade`/`FinalNote` để trống — chỉ được nhập sau ở bước riêng (mục 9).
- Sau bước khởi tạo, `PlanGrade` là field mutable mà giáo viên có thể sửa tiếp khi hoàn thiện plan (mục 7); sửa `PlanGrade` không ảnh hưởng ngược tới `AssessmentRecordLatest`.
- Một `AssessmentSheet` có thể có nhiều `AssessmentRecord`; không giới hạn số lượng mục tối thiểu/tối đa.
- Không tạo Google Sheet riêng ở bước này hoặc ở bất kỳ bước nào khác của vòng đời `AssessmentSheet` v1.

## 6. Google Sheet riêng `[F01]` — legacy, không còn dùng

- Không còn nút/hành động tạo file Google Sheet riêng cho từng `AssessmentSheet`.
- Không còn endpoint nghiệp vụ `export-to-sheet`, `sync-to-sheet`, `generate-plan-pdf`, `generate-result-pdf`.
- Không còn cấu hình `AssessmentSheetTemplateFileId`, `DataSheetName`, `PlanTemplateSheetName/Gid`, `ResultTemplateSheetName/Gid`.
- Nếu DB cũ còn cột `AssessmentSheetSpreadsheetId`, cột này chỉ là legacy để tránh phá dữ liệu/migration cũ; API/UI v1 không expose và không ghi giá trị mới.
- Các luồng Google còn dùng trong `AssessmentSheet` v1:
  - `POST /api/v1/google-sheets/sync-assessments`: nạp `Assessment` và latest mirror từ file nguồn `[F0]`.
  - `POST /api/v1/assessment-sheets/{id}/upload-plan-pdf`: nhận PDF do UI render và upload vào `Student.DriveFolderId`.
  - `POST /api/v1/assessment-sheets/{id}/upload-result-pdf`: nhận PDF do UI render và upload vào `Student.DriveFolderId`.
  - `POST /api/v1/assessment-sheets/{id}/submit-results`: ghi `FinalGrade`/`FinalNote` về ResultSource `[F0.ĐG]`.

## 7. Chỉnh sửa plan sau khi tạo

- Sau khi tạo (áp dụng cả khi `AssessmentSheet` đang ở trạng thái `Open`; khi `Done` thì bị khoá theo mục 4), giáo viên có thể:
  - Chọn lại danh sách mục đánh giá (thêm/bớt `AssessmentRecord`).
  - Thay đổi `PlanGrade`/`PlanNote` của một `AssessmentRecord` (ví dụ sửa lại cho đúng thực tế trước khi đánh giá) — **không đụng tới `FinalGrade`/`FinalNote`**, hai cặp field hoàn toàn độc lập.
- Các thay đổi này **không được ghi ngược** vào `AssessmentSheetLatest`/`AssessmentRecordLatest`; hai bảng đó là chỉ-đọc, chỉ được cập nhật qua luồng đồng bộ từ `[F0.data_DG]` (mục 12) — không qua thao tác chỉnh sửa plan của một `AssessmentSheet` cụ thể, và cũng không qua việc ghi kết quả chính thức ở mục 11.
- Chỉnh sửa plan chỉ lưu trong portal; không còn bước đồng bộ plan sang Google Sheet riêng.

### 7.1. Chỉnh tên nhóm snapshot tại records-panel

- Mỗi ô merge `Nhóm lớn` (`GroupLv2Name`) hoặc `Nhóm nhỏ` (`GroupLv3Name`) có một nút icon sửa. Bấm nút mở popup nhỏ với textbox nhập tên mới.
- Nút `Áp dụng` trong popup **chỉ cập nhật tên nhóm trên giao diện** cho toàn bộ `AssessmentRecord` thuộc đúng ô merge đang chọn (đổi `AssessmentSnapshot` trong bộ nhớ, đánh dấu form có thay đổi chưa lưu). Tên nhóm mới **chỉ được ghi xuống DB khi người dùng bấm `Lưu thay đổi` của cả bảng đánh giá** (đi cùng luồng lưu records).
- Mỗi ô merge có nút `Hoàn tác`, chỉ hiện khi tên nhóm của ô đã khác giá trị lúc mở bảng; bấm là trả các dòng trong ô về tên nhóm ban đầu.
- Tiêu đề cột `Nhóm nhỏ` có checkbox `Hiện` bật/tắt một cột `Di chuyển` nằm giữa cột `Nhóm lớn` và `Nhóm nhỏ`. Cột này có nút lên/xuống ở ô merge của từng nhóm nhỏ, cho phép di chuyển cả dải record của một nhóm nhỏ lên/xuống **trong phạm vi nhóm lớn của nó**; STT/`displayOrder` được đánh lại theo thứ tự hiển thị mới. Không di chuyển nhóm nhỏ vượt ra ngoài nhóm lớn.
- Tương tự, tiêu đề cột `Nhóm lớn` có checkbox `Hiện` bật/tắt một cột `Di chuyển` nằm trước cột `Nhóm lớn`, cho phép di chuyển cả một nhóm lớn lên/xuống. Thứ tự nhóm lớn tùy chỉnh này ghi đè thứ tự cấu hình mặc định và được giữ lại qua lần lưu (nhờ `displayOrder`); các nhóm lớn chưa được dời vẫn theo thứ tự cấu hình.
- Popup có nút riêng `Cập nhật Assessment gốc` (chỉ Admin/SuperAdmin). Bấm sẽ cập nhật ngay các `Assessment` danh mục xuất hiện trong ô merge (theo mã); không rename Assessment khác chỉ vì trùng tên group. UI đồng thời áp tên mới lên giao diện.
- Popup có checkbox `Ghi ngược Google Sheet`, nhưng checkbox phải bị disable và hiển thị chú thích `Chưa hỗ trợ`; không có backend write-back Google Sheet trong phiên bản này.
- Nút/confirm `Cập nhật Assessment gốc` phải cảnh báo thay đổi có thể bị lần `Đồng bộ GGSheet` tiếp theo ghi đè.
- Quyền/trạng thái:
  - Teacher/Admin/SuperAdmin được đổi tên nhóm snapshot khi sheet `Open` hoặc `Planed`.
  - Chỉ Admin/SuperAdmin dùng được `Cập nhật Assessment gốc`.
  - Sheet `Done` khóa toàn bộ thao tác sửa group.
- Snapshot tùy chỉnh phải được giữ lại khi lưu grade/note hoặc thêm/xóa record; luồng replace record không được âm thầm dựng lại và ghi đè snapshot đã chỉnh.

## 8. Sinh PDF [F02] — Kế hoạch cá nhân

- Khi người dùng bấm nút `In Kế hoạch PDF` (không tự động), UI mở trang preview HTML/A4, render dữ liệu kế hoạch hiện tại (`PlanGrade`/`PlanNote`) bằng `html2pdf.js`.
- Link tới file PDF này được lưu vào field `PlanFileLinkPdf` của `AssessmentSheet`.
- PDF `[F02]` phản ánh plan tại thời điểm render; chỉ giữ **bản mới nhất** trên link hiện tại.
- Nút `Mở PDF` chỉ mở blob URL để xem/in; nút `Tạo PDF lên Google Drive` upload PDF do UI tạo qua `POST /api/v1/assessment-sheets/{id}/upload-plan-pdf`, lưu vào `Student.DriveFolderId` và cập nhật `PlanFileLinkPdf`. Luồng này không gọi `generate-plan-pdf` cũ và không phụ thuộc việc tạo/ghi `[F01]`; nếu học sinh chưa có Drive folder id thì báo lỗi rõ.

## 9. Nhập kết quả đánh giá

- Với mỗi `AssessmentRecord`, giáo viên nhập `FinalGrade` (kết quả đánh giá thật, để trống cho tới bước này) và có thể nhập `FinalNote`/feedback riêng cho mục đó (tùy chọn) — **độc lập hoàn toàn** với `PlanGrade`/`PlanNote` đã nhập ở bước lập kế hoạch (mục 7); nhập `FinalGrade` không đổi `PlanGrade`. UI không được tự động lấy `PlanGrade`/`PlanNote` để lấp vào `FinalGrade`/`FinalNote`.
- `AssessmentSheet` có `Feedback` tổng cho toàn bộ đợt đánh giá (tùy chọn).
- Nhập kết quả là thao tác trong phạm vi `AssessmentSheet`/`AssessmentRecord` hiện có; không ghi gì vào `AssessmentSheetLatest`/`AssessmentRecordLatest` tại bước này — hai bảng đó chỉ được ghi bởi luồng nạp lại ở mục 12 (xem mục 11 cho việc chính thức hoá kết quả về phía file nguồn `[F0]`).

## 10. Sinh PDF [F03] — Kết quả đánh giá

- Cùng cơ chế preview/upload như `[F02]` (mục 8), nhưng dùng `FinalGrade`/`FinalNote`/feedback hiện tại. Cho phép sinh PDF ngay cả khi còn `AssessmentRecord` chưa có `FinalGrade`; các mục còn thiếu hiển thị trống trên PDF.
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
- **Bảng mapping `FinalGrade` → nhãn ghi vào `[F0.ĐG]`** (đã chốt với người dùng 2026-08-30 — ghi nguyên văn, không tự suy diễn lại thứ tự):

  | `FinalGrade` | Nhãn ghi vào `[F0.ĐG]` | Rank (thang xếp hạng) |
  |---|---|---|
  | `A` | `Đạt +` | 3 (cao nhất) |
  | `B` | `Hỗ trợ +` | 2 |
  | `C` | `Hỗ trợ -` | 1 |
  | `D` | `Chưa đạt -` | 0 (thấp nhất) |

  Mapping này dùng thống nhất ở mọi nơi hiển thị `FinalGrade`/`PlanGrade` cho người dùng cuối (UI, PDF `[F02]`/`[F03]`), không chỉ riêng khi ghi `[F0.ĐG]`, để tránh vừa hiện "A/B/C/D" vừa hiện nhãn tiếng Việt ở hai chỗ khác nhau. Đây là bản định nghĩa lại thứ tự đã sửa lỗi lệch trước đó (bản cũ gán nhầm `B` → `Chưa đạt -`, `C` → `Hỗ trợ +`, `D` → `Hỗ trợ -`); nhãn chữ giữ nguyên `Chưa đạt -`, chỉ đổi enum gắn với nó và thang rank.
- Đây là bước ghi kết quả về file nguồn dùng chung toàn trường/toàn khối; không còn bản ghi Google Sheet riêng `[F01.KQ]` cho một đợt của một học sinh.
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
| `PlanFileLinkPdf` | Không | Đường dẫn PDF kế hoạch cá nhân (`[F02]`) sau khi sinh. |
| `ResultFileLinkPdf` | Không | Đường dẫn PDF kết quả đánh giá (`[F03]`) sau khi sinh. |
| `Feedback` | Không | Nhận xét tổng cho toàn đợt. |
| `Note` | Không | Ghi chú nội bộ. |

Mỗi `AssessmentRecord` trong `AssessmentSheet` có: snapshot mục đánh giá (`AssessmentSnapshot`), `PlanGrade`/`PlanNote` (giai đoạn kế hoạch — khởi tạo từ `latestGrade`/`note` mà UI gửi kèm record tạo mới, sau đó giáo viên chỉnh sửa trực tiếp khi hoàn thiện plan), `FinalGrade`/`FinalNote` (kết quả đánh giá thật, nhập ở bước riêng sau — mục 9). Hai cặp field độc lập, không cặp nào ghi đè cặp còn lại; `FinalGrade`/`FinalNote` không tự động fill từ `PlanGrade`/`PlanNote`.

### Cặp bảng chỉ-đọc AssessmentSheetLatest / AssessmentRecordLatest

Cấu trúc mirror theo hình dạng `AssessmentSheet`/`AssessmentRecord` (theo học sinh → theo từng mục đánh giá), nhưng:

- Chỉ được ghi bởi luồng đồng bộ ở mục 12; không có API/UI nào cho phép sửa trực tiếp.
- `AssessmentRecordLatest.LatestGrade` là **field đơn** (không tách plan/final vì đây chỉ là dữ liệu nguồn tham chiếu) — dùng để UI hiển thị/lọc kết quả gần nhất và gửi lại trong `records[].latestGrade` khi tạo `AssessmentRecord` mới (mục 5).
- `AssessmentRecordLatest` liên kết trực tiếp tới `Assessment` bằng `AssessmentId`/`Assessment`; khoá duy nhất theo `AssessmentSheetLatestId` + `AssessmentId` để xác định đúng dòng khi đồng bộ ghi đè theo từng học sinh + mục đánh giá. Bản trung gian từng có field kỹ thuật `AssessmentCode` đã lỗi thời và không còn là contract hiện hành.
- Không có `SubmissionDate`, `PlanFileLinkPdf`, `ResultFileLinkPdf` — các field gắn với vòng đời làm việc/PDF của một `AssessmentSheet` thật không áp dụng cho bảng chỉ-đọc này.
- Bị ghi đè mỗi lần đồng bộ lại; không phải nơi lưu lịch sử theo thời gian, chỉ phản ánh trạng thái tại lần fetch gần nhất.

## 14. Ràng buộc và validation

- Mỗi đợt đánh giá của một học sinh ứng với đúng một `AssessmentSheet` và một `Name`; `Name` bắt buộc và duy nhất trong phạm vi một học sinh. Nhiều học sinh khác nhau có thể dùng chung một `Name` đợt (ví dụ cùng đợt `8.9.10.26` nhưng mỗi học sinh có `AssessmentSheet` riêng).
- Import Excel AssessmentSheet v1 dùng file `.xlsx` với header bắt buộc `planGrade`, `planNote`, `assessmentCode`, `studentCode`, `studentName`, `startDate`, `dueDate`; backend đọc bằng `ExcelDataReader`, gom sheet theo `studentCode + startDate + dueDate`, tra cứu học sinh/mục đánh giá theo mã, bỏ qua dòng trống, cảnh báo dòng trùng, và rollback toàn bộ nếu có lỗi bắt buộc. UI phải preview/validate trước bằng popup `dxDataGrid` hiển thị toàn bộ dòng import đã parse/validate, lỗi/warning và summary; datagrid filter/search/sort client-side trên dữ liệu local preview, không gọi server paging/filter trong popup; chỉ khi file hợp lệ user mới bấm xác nhận để ghi DB. `planGrade`/`planNote` trong file lưu vào `PlanGrade`/`PlanNote`; `FinalGrade`/`FinalNote` vẫn để trống.
- File import còn hỗ trợ 3 cột **tùy chọn** (file không có 3 cột này vẫn import bình thường):
  - `STT`: trong file thường reset số theo từng nhóm nhỏ. Import bỏ qua con số per-nhóm và ghi `AssessmentRecord.DisplayOrder` là số chạy toàn cục `1..N` theo đúng thứ tự dòng của mỗi sheet, để form giữ đúng thứ tự file khi tải lại. `STT` không phải số nguyên chỉ tạo cảnh báo, không chặn import.
  - `groupLv2Name`, `groupLv3Name`: điền kiểu ô merge (chỉ ở dòng đầu mỗi cụm). Import fill-down giá trị non-empty gần nhất phía trên cho từng cột độc lập, reset theo từng cụm học sinh, rồi ghi giá trị hiệu lực vào `AssessmentSnapshot.GroupLv2Name`/`GroupLv3Name`; ô trống và không có gì để kế thừa thì dùng tên nhóm của `Assessment`. `Code`/`Name`/`GroupLv1Name`/`RowIndex` của snapshot vẫn lấy từ `Assessment`.
  - Preview trả thêm `stt`, `groupLv2Name`, `groupLv3Name` (giá trị hiệu lực) và popup hiển thị 3 cột tương ứng.
- Trong form bảng đánh giá, tên nhóm `GroupLv2Name`/`GroupLv3Name` hiển thị/gộp ô/tô màu/sắp xếp **theo snapshot** của `AssessmentRecord`, không đọc lại từ danh mục `Assessment`. Tên nhóm snapshot (do import khcn hoặc chỉnh tay ở mục 7.1) phải được giữ nguyên khi lưu grade/note hoặc thêm/xóa/sắp lại record — `PUT /assessment-sheets/{id}/records` map record cũ theo `Assessment.Code` để giữ tên nhóm snapshot thay vì dựng lại từ `Assessment`.
- Không cho tạo `AssessmentSheet` cho học sinh `Inactive` (nhất quán với ràng buộc học sinh `Inactive` không được thêm dữ liệu nghiệp vụ mới, xem [03](03-hoc-sinh-va-nhom.md)).
- `DoneDate` chỉ được set khi `Status = Done`; chuyển trạng thái phải đi kèm cập nhật `DoneDate` nhất quán.
- Không giới hạn số lượng `AssessmentRecord` tối thiểu/tối đa trong một `AssessmentSheet`.
- `Teacher`/`Admin`/`SuperAdmin` đều xem/sửa được `AssessmentSheet` của mọi học sinh, không phân biệt ai là người tạo hay ai đang phụ trách nhóm nào (xem mục 2).
- Lỗi khi đọc/ghi Google Sheet nguồn, upload PDF lên Drive hoặc render/mở PDF phải trả lỗi rõ ràng cho người dùng, không để `AssessmentSheet` ở trạng thái nửa vời (ví dụ có link PDF nhưng file thực tế chưa upload thành công).

## 15. Giả định đã chốt và phần còn để bổ sung

Các quyết định nghiệp vụ đã được chốt và áp dụng xuyên suốt tài liệu:

- Quyền không giới hạn theo nhóm cho `AssessmentSheet`; endpoint `sync-assessments` được điều chỉnh chính sách `PortalManagers` để cho phép cả `Teacher`.
- Chuyển `Done` → `Open` là thao tác đổi trạng thái đơn giản, mọi vai trò được phép, không cần lý do/log riêng.
- Filter chọn plan theo học sinh, theo `LatestGrade` (thang `A > B > C > D`) và theo `GroupLv1/2/3Name`.
- Không còn tạo/copy Google Sheet riêng `[F01]` cho từng `AssessmentSheet`; các field/config/endpoint phục vụ `[F01]` là legacy và không thuộc contract API/UI v1.
- PDF `[F02]`/`[F03]` sinh theo nút bấm bằng trang preview UI + `html2pdf.js`, sau đó có thể upload lên Google Drive của học sinh; chỉ giữ bản mới nhất; `[F03]` được phép sinh dù còn mục thiếu `FinalGrade`.
- **`AssessmentRecord` có hai cặp field độc lập: `PlanGrade`/`PlanNote` và `FinalGrade`/`FinalNote`** — đây là thiết kế đã khôi phục đúng theo quyết định "giữ lại giá trị khởi tạo phục vụ đối chiếu/audit" ban đầu (khác một phiên bản trung gian của tài liệu này từng gộp chung thành một field `Grade` duy nhất — phiên bản đó đã lỗi thời). Khi tạo mới, UI gửi `records[].latestGrade`/`records[].note` theo dữ liệu gần nhất đang hiển thị để backend lưu vào `PlanGrade`/`PlanNote`, dùng cho `[F02]`; `FinalGrade`/`FinalNote` nhập riêng sau, không tự động fill từ kế hoạch/latest, dùng cho `[F03]` và `[F0.ĐG]`.
- Ghi `[F0.ĐG]` (dùng `FinalGrade`/`FinalNote`) và nạp lại `AssessmentSheetLatest`/`AssessmentRecordLatest`/`Assessment` từ `[F0.data_DG]` là hai thao tác thủ công tách biệt; ghi `[F0.ĐG]` tự set `SubmissionDate`.
- **Đã xác nhận vị trí ghi `[F0.ĐG]`:** cột `E16:E` = mã mục đánh giá (dò dòng), hàng `H16:16` = mã học sinh (dò cột), ghi tại ô giao nhau; giá trị ghi là nhãn theo bảng mapping ở mục 11 (không phải chữ cái `A/B/C/D`).
- Mỗi đợt–học sinh ứng với đúng một `AssessmentSheet`/`Name`; không giới hạn số lượng `AssessmentRecord`.

Các điểm trước đây cần xác nhận, nay đã chốt:

- Bảng mapping `FinalGrade` → nhãn ở mục 11 đã được người dùng định nghĩa lại và chốt (2026-08-30): `A` → `Đạt +` (rank 3) → `B` → `Hỗ trợ +` (rank 2) → `C` → `Hỗ trợ -` (rank 1) → `D` → `Chưa đạt -` (rank 0). Dùng trực tiếp để ghi `[F0.ĐG]` và hiển thị UI/PDF.
