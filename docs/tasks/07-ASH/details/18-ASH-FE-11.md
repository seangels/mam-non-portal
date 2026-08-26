# ASH-FE-11 — Preview/In Kế hoạch PDF bằng html2pdf

Nguồn mẫu: [`docs/samples/khcn-standalone.html`](../../../samples/khcn-standalone.html).

## Mục đích

Thêm luồng preview/in PDF kế hoạch cá nhân cho AssessmentSheet. Người dùng bấm `In Kế hoạch PDF` từ màn edit để mở trang preview HTML trước, xem đúng bố cục A4 theo mẫu `khcn-standalone.html`, rồi chọn mở blob PDF hoặc tạo file PDF vào thư mục Google Drive của học viên.

## Tóm tắt ngắn

1. ✅ Thêm nút `In Kế hoạch PDF` trên màn edit AssessmentSheet; nút này mở trang preview trước khi tạo PDF.
2. ✅ Trang preview render HTML in theo mẫu `docs/samples/khcn-standalone.html`, dùng `html2pdf.js` phía frontend.
3. ✅ Trên preview có 3 thao tác:
   - ✅ `Quay lại` về màn edit AssessmentSheet.
   - ✅ `Mở PDF` tạo blob URL bằng `window.html2pdf().set(options).from(page).outputPdf('bloburl')` rồi `window.open(blobUrl)`.
   - ✅ `Tạo PDF lên Google Drive` tạo PDF và lưu vào thư mục Google Drive của học viên.
4. ✅ PDF sử dụng dữ liệu kế hoạch hiện tại của sheet:
   - ✅ thông tin học sinh.
   - ✅ đợt đánh giá tính từ `startDate`/`dueDate`.
   - ✅ độ tuổi tính từ ngày sinh tới `startDate`.
   - ✅ danh sách Assessment Record.
   - ✅ `PlanGrade`.
   - ✅ `PlanNote`.
5. ✅ Không dùng backend endpoint `generate-plan-pdf` cũ để render từ Google Sheet; nếu lưu Drive cần endpoint/upload flow riêng cho PDF do UI tạo.
6. ✅ Không đổi auth/routing/IIS.
7. ✅ UI, thông báo lỗi, tên file PDF dùng tiếng Việt.

## Phạm vi

- ✅ Phần chính thay đổi frontend trong `ui/`.
- ✅ Nếu source chưa có endpoint nhận PDF blob/base64 để lưu vào Drive folder của học viên, cần tạo task backend/contract companion trước hoặc trong cùng đợt triển khai.
- ✅ Có thể thêm dependency frontend `html2pdf.js` theo version cố định, ưu tiên `0.10.1` như file mẫu đang dùng.
- ✅ Không dùng CDN trong Angular app; thư viện phải được import/bundle từ npm để chạy ổn định khi deploy nội bộ.
- ✅ Nếu package không có type phù hợp với TypeScript 4.3.5, thêm declaration nhỏ, rõ ràng, chỉ đủ cho API đang dùng.
- ✅ Không chạy production/IIS/deploy.

## Nội dung cần làm

- ✅ Thêm nút `In Kế hoạch PDF` ở màn edit AssessmentSheet.
  - ✅ Nút trên màn edit chỉ điều hướng sang trang preview chuẩn bị in, chưa tạo PDF ngay.
  - ✅ Đặt gần nhóm action hiện có của AssessmentSheet, không làm rối sticky form-actions.
  - ✅ Disable khi đang `loading`, `saving`, đang thêm/xóa record, hoặc chưa có record.
- ✅ Tạo route/trang preview in kế hoạch.
  - ✅ Route đề xuất: `/#/assessment-sheets/:id/plan-pdf-preview`.
  - ✅ Dùng guard/role như màn edit AssessmentSheet hiện tại.
  - ✅ Load lại detail từ API theo `id` để preview mở độc lập được khi refresh trang.
  - ✅ Có nút `Quay lại` về `/#/assessment-sheets/:id/edit`.
  - ✅ Có nút `Mở PDF` để tạo blob URL và mở tab mới.
  - ✅ Có nút `Tạo PDF lên Google Drive` để lưu file PDF vào thư mục Drive của học viên.
  - ✅ Hiển thị loading/error tiếng Việt khi đang load sheet hoặc đang tạo PDF.
- ✅ Tạo phần render HTML dùng riêng cho PDF trong trang preview.
  - ✅ Dựa theo cấu trúc A4 trong `khcn-standalone.html`: tiêu đề `KẾ HOẠCH CÁ NHÂN`, thông tin trẻ, bảng kế hoạch, nhận xét phụ huynh, số trang.
  - ✅ Không copy nguyên inline script/CDN từ file mẫu vào component.
  - ✅ Tách CSS preview/PDF riêng khỏi CSS bảng edit để tránh làm vỡ UI chính.
  - ✅ Preview hiển thị trang A4 thật trên màn hình để user xem trước bố cục trước khi tạo PDF.
  - ✅ `html2pdf` capture đúng DOM của trang preview, không capture toolbar/nút bấm.
- ✅ Mapping dữ liệu từ AssessmentSheet hiện tại vào template.
  - ✅ Tên học sinh lấy từ snapshot/detail đang load.
  - ✅ Ngày sinh hiển thị dạng `dd/MM/yyyy` nếu có.
  - ✅ Độ tuổi tính từ ngày sinh tới `startDate`; ví dụ nếu `startDate` là ngày bắt đầu đợt thì tuổi phản ánh tại thời điểm bắt đầu đợt, không phải ngày hiện tại.
  - ✅ Đợt/tên hiển thị tính từ `startDate` và `dueDate`, không lấy trực tiếp từ tên sheet.
  - ✅ Format đợt: `N tháng M1.M2...MY.YY`, ví dụ `startDate` trong tháng 6 và `dueDate` trong tháng 8 năm 2026 thì hiển thị `3 tháng 6.7.8.26`.
  - ✅ Nếu thiếu `startDate` hoặc `dueDate`, hiển thị placeholder tiếng Việt rõ ràng và không tự suy đoán tên đợt.
  - ✅ Bảng record dùng `PlanGrade` và `PlanNote`, không dùng `FinalGrade`/`FinalNote`.
  - ✅ Grade hiển thị bằng nhãn tiếng Việt từ config hiện có (`Đạt +`, `Chưa đạt -`, `Hỗ trợ +`, `Hỗ trợ -`, `Chưa có` nếu trống).
  - ✅ Nhóm lớn/nhóm nhỏ/row order tái sử dụng logic/config hiện có trong `api.models.assessment-sheets` và records table để đồng nhất màu/thứ tự.
- ✅ Hành vi khi dữ liệu chưa lưu.
  - ✅ Vì preview là trang riêng và load detail theo `id`, dữ liệu preview mặc định là dữ liệu đã lưu trong hệ thống.
  - ✅ Nếu màn edit đang dirty mà user bấm `In Kế hoạch PDF`, hiển thị confirm tiếng Việt: nên lưu thay đổi trước nếu muốn PDF phản ánh bản mới nhất.
  - ✅ Không tự động gọi save trước khi mở preview.
- ✅ Sinh file PDF bằng `html2pdf.js`.
  - ✅ Cấu hình A4 portrait, `margin: 0`, `html2canvas.scale: 2`, `html2canvas.letterRendering: true` để vẽ chữ chính xác cao, `useCORS: true`, background trắng, filename tiếng Việt không dấu an toàn ví dụ `ke-hoach-ca-nhan-<ma-hoc-sinh>.pdf`.
  - ✅ Có fallback `window.print()` nếu thư viện không load được.
  - ✅ Bắt lỗi và báo `Không thể tạo PDF kế hoạch. Vui lòng thử lại.`
- ✅ Mở blob PDF từ preview.
  - ✅ Dùng đúng kiểu:

    ```js
    window.html2pdf().set(options).from(page).outputPdf('bloburl').then(function(blobUrl) {
      window.open(blobUrl);
    });
    ```

  - ✅ Không tự động download khi user chọn `Mở PDF`; chỉ mở blob URL để xem/in.
- ✅ Tạo PDF trên thư mục Google Drive của học viên.
  - ✅ Tạo PDF blob từ chính DOM preview bằng `html2pdf`.
  - ✅ Upload/lưu vào Drive folder của học viên (`Student.driveFolderId`) thông qua backend để không đưa credential Google vào frontend.
  - ✅ Nếu học viên chưa có Drive folder id, báo lỗi tiếng Việt và không tạo file.
  - ✅ Sau khi tạo thành công, hiển thị thông báo và link mở file nếu API trả về.
  - ❓ Câu hỏi cần làm rõ khi đổi hướng sau này: dùng lại field `PlanFileLinkPdf` để lưu link bản PDF này hay chỉ tạo file Drive không cập nhật database.
  - ✅ Trong lần triển khai hiện tại, áp dụng mặc định đề xuất: cập nhật `PlanFileLinkPdf` vì đây vẫn là PDF kế hoạch cá nhân mới nhất.
- ✅ Hỗ trợ nhiều record.
  - ✅ Không hard-code chỉ một trang theo dữ liệu mẫu.
  - ⚠️ **Đổi hướng sau khi code lần đầu** (commit `25fe8e4`, 2026-08-26): thay vì cho bảng dài sang trang A4 tiếp theo bằng page-break, preview giờ ép luôn 1 trang cố định (`.pdf-page` `height: 295mm`, `overflow: hidden`) và tự động `scale()` nội dung xuống vừa 1 trang (`fitContentToPage()`, đo `.pdf-content.scrollHeight` so với chiều cao khả dụng, không bao giờ scale lên) theo cơ chế đã kiểm chứng ở `docs/samples/khcn-standalone.html`. Đánh đổi: sheet nhiều dòng bị co chữ nhỏ lại thay vì tràn sang trang 2. `page-break-inside: avoid`/`pagebreak.avoid` trong CSS/`pdfOptions` vẫn còn trong code nhưng không còn tác dụng thật (không bao giờ có trang 2 để mà tránh vỡ dòng) — coi là dead code vô hại, chưa dọn.
  - ⬜ Header bảng lặp lại khi qua trang: không còn áp dụng vì thiết kế đã chuyển sang ép 1 trang duy nhất (không có "trang tiếp theo" nữa).

## Ghi chú thiết kế

- ✅ Task này thay hướng trải nghiệm: user xem trang preview HTML trước, sau đó chọn mở blob PDF hoặc lưu PDF lên Drive.
- ✅ Luồng render PDF không dùng backend `generate-plan-pdf` cũ vì backend flow đó render từ Google Sheet, không phải từ HTML preview.
- ✅ Phần lưu Drive cần backend làm vai trò upload/lưu bằng service account; frontend không chứa Google credential.
- ❓ Câu hỏi cần làm rõ khi đổi hướng sau này: lưu link Drive về `PlanFileLinkPdf` hay chỉ trả link tạm.
- ✅ Trong lần triển khai hiện tại, áp dụng mặc định đề xuất: lưu `PlanFileLinkPdf`.
- ✅ Tránh tạo object literal mới trong template cho DevExtreme 19.2.5; config/options nên là property ổn định trong component.
- ✅ Không dùng API mới của Angular sau 12.2.17; giữ NgModule/component pattern hiện tại.
- ✅ Ưu tiên tách helper build dữ liệu in để có unit test nhẹ, thay vì nhồi toàn bộ logic vào click handler.
- ⚠️ **Bổ sung sau khi code** (commit `788c48d`, 2026-08-26, ngoài scope DoD ban đầu của task này): nút `In Kế hoạch PDF` trên màn edit chỉ bật khi `originalStatus !== 'Open'` (`canOpenPlanPdfPreview()`), vì PDF chỉ có ý nghĩa khi sheet đã có kế hoạch chốt (`Planed`/`Done`). Cùng lúc `records-panel` (bảng đưa vào từ `ASH-FE-09`) ẩn hẳn cột `Kết quả hiện tại`/`Ghi chú` của nó khi `Open` (chỉ hiện ở `Planed`/`Done`), và luôn hiện cột `Kế hoạch`/`Ghi chú` khi `Open` bất kể checkbox `Hiện kế hoạch`.

## Kiểm thử mong đợi

- ✅ Cập nhật/thêm unit test cho helper build dữ liệu in:
  - ✅ map đúng student snapshot.
  - ✅ tính đúng đợt từ `startDate`/`dueDate`, ví dụ tháng 6 tới tháng 8 năm 2026 là `3 tháng 6.7.8.26`.
  - ✅ tính đúng độ tuổi tại `startDate` từ ngày sinh.
  - ✅ dùng `PlanGrade`/`PlanNote`, không dùng `FinalGrade`/`FinalNote`.
  - ✅ giữ đúng sort/order nhóm như records table.
  - ✅ tạo filename an toàn — cú pháp đổi lại (2026-08-26, theo yêu cầu người dùng) thành `khcn - <studentCode>.<studentNickName>_<assessmentName>.pdf`; `assessmentName` là công thức riêng biệt với `formatAssessmentPeriod` (header preview giữ nguyên "N tháng ..."), chỉ tính tháng của `dueDate` nếu đó là ngày cuối tháng, ví dụ `2026-03-01`→`2026-06-26` ra `3.4.5.26`, `2026-06-01`→`2026-08-31` ra `6.7.8.26`.
- ✅ Cập nhật/thêm test route/permission hoặc component state cho preview nếu phù hợp với cấu trúc test hiện tại.
- ✅ Nếu thêm backend upload endpoint, chạy thêm backend build/unit/integration tương ứng và cập nhật memory backend/shared contract.
- ✅ Chạy `npm --prefix ui run test:ci` — audit (2026-08-26) khi review commit `25fe8e4`/`788c48d` từng phát hiện 110/114 pass (4 fail); đã vá trong cùng lượt (đổi filename theo cú pháp mới, đổi fallback `assessmentGradeColor`/`assessmentGradeBgColor` sang `''` theo yêu cầu người dùng, cập nhật test `canOpenPlanPdfPreview` theo rule khoá khi `Open`) → **114/114 pass**, xem `log.md`.
- ✅ Chạy `npm --prefix ui run build -- --configuration development` — pass (hash `5db6c70f5becc2ddf940` sau khi vá).
- ⚠️ Smoke thủ công màn edit AssessmentSheet: chưa chạy trong lượt này.
  - ⚠️ Bấm `In Kế hoạch PDF`: mở trang preview đúng sheet.
  - ⚠️ Trên preview bấm `Quay lại`: trở về màn edit đúng id.
  - ⚠️ Trên preview bấm `Mở PDF`: mở blob PDF bằng tab mới.
  - ⚠️ Trên preview bấm `Tạo PDF lên Google Drive`: chừa lại phần kiểm tra lưu file PDF Google Drive thật theo yêu cầu.
  - ⚠️ PDF hiển thị đúng thông tin học sinh và record kế hoạch.
  - ⚠️ Đợt hiển thị đúng theo `startDate`/`dueDate`; độ tuổi tính đúng tại `startDate`.
  - ⚠️ Record dùng `PlanGrade`/`PlanNote`, không lẫn `FinalGrade`/`FinalNote`.
  - ⚠️ Bảng dài qua trang không bị cắt mất nội dung chính.
  - ⚠️ Khi edit đang dirty, confirm xuất hiện trước khi mở preview; preview không tự save dữ liệu.
  - ⚠️ Không có request backend `generate-plan-pdf` cũ khi mở/mở blob PDF.
