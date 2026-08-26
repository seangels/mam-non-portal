# ASH-FE-11 — Preview/In Kế hoạch PDF bằng html2pdf

Nguồn mẫu: [`docs/samples/khcn-standalone.html`](../../../samples/khcn-standalone.html).

## Mục đích

Thêm luồng preview/in PDF kế hoạch cá nhân cho AssessmentSheet. Người dùng bấm `In Kế hoạch PDF` từ màn edit để mở trang preview HTML trước, xem đúng bố cục A4 theo mẫu `khcn-standalone.html`, rồi chọn mở blob PDF hoặc tạo file PDF vào thư mục Google Drive của học viên.

## Tóm tắt ngắn

1. ⬜ Thêm nút `In Kế hoạch PDF` trên màn edit AssessmentSheet; nút này mở trang preview trước khi tạo PDF.
2. ⬜ Trang preview render HTML in theo mẫu `docs/samples/khcn-standalone.html`, dùng `html2pdf.js` phía frontend.
3. ⬜ Trên preview có 3 thao tác:
   - ⬜ `Quay lại` về màn edit AssessmentSheet.
   - ⬜ `Mở PDF` tạo blob URL bằng `window.html2pdf().set(options).from(page).outputPdf('bloburl')` rồi `window.open(blobUrl)`.
   - ⬜ `Tạo PDF lên Google Drive` tạo PDF và lưu vào thư mục Google Drive của học viên.
4. ⬜ PDF sử dụng dữ liệu kế hoạch hiện tại của sheet:
   - ⬜ thông tin học sinh.
   - ⬜ đợt đánh giá tính từ `startDate`/`dueDate`.
   - ⬜ độ tuổi tính từ ngày sinh tới `startDate`.
   - ⬜ danh sách Assessment Record.
   - ⬜ `PlanGrade`.
   - ⬜ `PlanNote`.
5. ⬜ Không dùng backend endpoint `generate-plan-pdf` cũ để render từ Google Sheet; nếu lưu Drive cần endpoint/upload flow riêng cho PDF do UI tạo.
6. ⬜ Không đổi auth/routing/IIS.
7. ⬜ UI, thông báo lỗi, tên file PDF dùng tiếng Việt.

## Phạm vi

- ⬜ Phần chính thay đổi frontend trong `ui/`.
- ⬜ Nếu source chưa có endpoint nhận PDF blob/base64 để lưu vào Drive folder của học viên, cần tạo task backend/contract companion trước hoặc trong cùng đợt triển khai.
- ⬜ Có thể thêm dependency frontend `html2pdf.js` theo version cố định, ưu tiên `0.10.1` như file mẫu đang dùng.
- ⬜ Không dùng CDN trong Angular app; thư viện phải được import/bundle từ npm để chạy ổn định khi deploy nội bộ.
- ⬜ Nếu package không có type phù hợp với TypeScript 4.3.5, thêm declaration nhỏ, rõ ràng, chỉ đủ cho API đang dùng.
- ⬜ Không chạy production/IIS/deploy.

## Nội dung cần làm

- ⬜ Thêm nút `In Kế hoạch PDF` ở màn edit AssessmentSheet.
  - ⬜ Nút trên màn edit chỉ điều hướng sang trang preview chuẩn bị in, chưa tạo PDF ngay.
  - ⬜ Đặt gần nhóm action hiện có của AssessmentSheet, không làm rối sticky form-actions.
  - ⬜ Disable khi đang `loading`, `saving`, đang thêm/xóa record, hoặc chưa có record.
- ⬜ Tạo route/trang preview in kế hoạch.
  - ⬜ Route đề xuất: `/#/assessment-sheets/:id/plan-pdf-preview`.
  - ⬜ Dùng guard/role như màn edit AssessmentSheet hiện tại.
  - ⬜ Load lại detail từ API theo `id` để preview mở độc lập được khi refresh trang.
  - ⬜ Có nút `Quay lại` về `/#/assessment-sheets/:id/edit`.
  - ⬜ Có nút `Mở PDF` để tạo blob URL và mở tab mới.
  - ⬜ Có nút `Tạo PDF lên Google Drive` để lưu file PDF vào thư mục Drive của học viên.
  - ⬜ Hiển thị loading/error tiếng Việt khi đang load sheet hoặc đang tạo PDF.
- ⬜ Tạo phần render HTML dùng riêng cho PDF trong trang preview.
  - ⬜ Dựa theo cấu trúc A4 trong `khcn-standalone.html`: tiêu đề `KẾ HOẠCH CÁ NHÂN`, thông tin trẻ, bảng kế hoạch, nhận xét phụ huynh, số trang.
  - ⬜ Không copy nguyên inline script/CDN từ file mẫu vào component.
  - ⬜ Tách CSS preview/PDF riêng khỏi CSS bảng edit để tránh làm vỡ UI chính.
  - ⬜ Preview hiển thị trang A4 thật trên màn hình để user xem trước bố cục trước khi tạo PDF.
  - ⬜ `html2pdf` capture đúng DOM của trang preview, không capture toolbar/nút bấm.
- ⬜ Mapping dữ liệu từ AssessmentSheet hiện tại vào template.
  - ⬜ Tên học sinh lấy từ snapshot/detail đang load.
  - ⬜ Ngày sinh hiển thị dạng `dd/MM/yyyy` nếu có.
  - ⬜ Độ tuổi tính từ ngày sinh tới `startDate`; ví dụ nếu `startDate` là ngày bắt đầu đợt thì tuổi phản ánh tại thời điểm bắt đầu đợt, không phải ngày hiện tại.
  - ⬜ Đợt/tên hiển thị tính từ `startDate` và `dueDate`, không lấy trực tiếp từ tên sheet.
  - ⬜ Format đợt: `N tháng M1.M2...MY.YY`, ví dụ `startDate` trong tháng 6 và `dueDate` trong tháng 8 năm 2026 thì hiển thị `3 tháng 6.7.8.26`.
  - ⬜ Nếu thiếu `startDate` hoặc `dueDate`, hiển thị placeholder tiếng Việt rõ ràng và không tự suy đoán tên đợt.
  - ⬜ Bảng record dùng `PlanGrade` và `PlanNote`, không dùng `FinalGrade`/`FinalNote`.
  - ⬜ Grade hiển thị bằng nhãn tiếng Việt từ config hiện có (`Đạt +`, `Chưa đạt -`, `Hỗ trợ +`, `Hỗ trợ -`, `Chưa có` nếu trống).
  - ⬜ Nhóm lớn/nhóm nhỏ/row order tái sử dụng logic/config hiện có trong `api.models.assessment-sheets` và records table để đồng nhất màu/thứ tự.
- ⬜ Hành vi khi dữ liệu chưa lưu.
  - ⬜ Vì preview là trang riêng và load detail theo `id`, dữ liệu preview mặc định là dữ liệu đã lưu trong hệ thống.
  - ⬜ Nếu màn edit đang dirty mà user bấm `In Kế hoạch PDF`, hiển thị confirm tiếng Việt: nên lưu thay đổi trước nếu muốn PDF phản ánh bản mới nhất.
  - ⬜ Không tự động gọi save trước khi mở preview.
- ⬜ Sinh file PDF bằng `html2pdf.js`.
  - ⬜ Cấu hình A4 portrait, `margin: 0`, `html2canvas.scale: 2`, `html2canvas.letterRendering: true` để vẽ chữ chính xác cao, `useCORS: true`, background trắng, filename tiếng Việt không dấu an toàn ví dụ `ke-hoach-ca-nhan-<ma-hoc-sinh>.pdf`.
  - ⬜ Có fallback `window.print()` nếu thư viện không load được.
  - ⬜ Bắt lỗi và báo `Không thể tạo PDF kế hoạch. Vui lòng thử lại.`
- ⬜ Mở blob PDF từ preview.
  - ⬜ Dùng đúng kiểu:

    ```js
    window.html2pdf().set(options).from(page).outputPdf('bloburl').then(function(blobUrl) {
      window.open(blobUrl);
    });
    ```

  - ⬜ Không tự động download khi user chọn `Mở PDF`; chỉ mở blob URL để xem/in.
- ⬜ Tạo PDF trên thư mục Google Drive của học viên.
  - ⬜ Tạo PDF blob từ chính DOM preview bằng `html2pdf`.
  - ⬜ Upload/lưu vào Drive folder của học viên (`Student.driveFolderId`) thông qua backend để không đưa credential Google vào frontend.
  - ⬜ Nếu học viên chưa có Drive folder id, báo lỗi tiếng Việt và không tạo file.
  - ⬜ Sau khi tạo thành công, hiển thị thông báo và link mở file nếu API trả về.
  - ⬜ Làm rõ trước khi code: dùng lại field `PlanFileLinkPdf` để lưu link bản PDF này hay chỉ tạo file Drive không cập nhật database. Mặc định đề xuất: cập nhật `PlanFileLinkPdf` vì đây vẫn là PDF kế hoạch cá nhân mới nhất.
- ⬜ Hỗ trợ nhiều record.
  - ⬜ Không hard-code chỉ một trang theo dữ liệu mẫu.
  - ⬜ Có quy tắc page-break để bảng dài có thể sang trang A4 tiếp theo.
  - ⬜ Header bảng nên lặp lại hoặc tối thiểu không làm mất cấu trúc khi qua trang.

## Ghi chú thiết kế

- ⬜ Task này thay hướng trải nghiệm: user xem trang preview HTML trước, sau đó chọn mở blob PDF hoặc lưu PDF lên Drive.
- ⬜ Luồng render PDF không dùng backend `generate-plan-pdf` cũ vì backend flow đó render từ Google Sheet, không phải từ HTML preview.
- ⬜ Phần lưu Drive cần backend làm vai trò upload/lưu bằng service account; frontend không chứa Google credential.
- ⬜ Cần chốt khi implement: lưu link Drive về `PlanFileLinkPdf` hay chỉ trả link tạm. Mặc định đề xuất lưu `PlanFileLinkPdf`.
- ⬜ Tránh tạo object literal mới trong template cho DevExtreme 19.2.5; config/options nên là property ổn định trong component.
- ⬜ Không dùng API mới của Angular sau 12.2.17; giữ NgModule/component pattern hiện tại.
- ⬜ Ưu tiên tách helper build dữ liệu in để có unit test nhẹ, thay vì nhồi toàn bộ logic vào click handler.

## Kiểm thử mong đợi

- ⬜ Cập nhật/thêm unit test cho helper build dữ liệu in:
  - ⬜ map đúng student snapshot.
  - ⬜ tính đúng đợt từ `startDate`/`dueDate`, ví dụ tháng 6 tới tháng 8 năm 2026 là `3 tháng 6.7.8.26`.
  - ⬜ tính đúng độ tuổi tại `startDate` từ ngày sinh.
  - ⬜ dùng `PlanGrade`/`PlanNote`, không dùng `FinalGrade`/`FinalNote`.
  - ⬜ giữ đúng sort/order nhóm như records table.
  - ⬜ tạo filename an toàn.
- ⬜ Cập nhật/thêm test route/permission hoặc component state cho preview nếu phù hợp với cấu trúc test hiện tại.
- ⬜ Nếu thêm backend upload endpoint, chạy thêm backend build/unit/integration tương ứng và cập nhật memory backend/shared contract.
- ⬜ Chạy `npm --prefix ui run test:ci`.
- ⬜ Chạy `npm --prefix ui run build -- --configuration development`.
- ⬜ Smoke thủ công màn edit AssessmentSheet:
  - ⬜ Bấm `In Kế hoạch PDF`: mở trang preview đúng sheet.
  - ⬜ Trên preview bấm `Quay lại`: trở về màn edit đúng id.
  - ⬜ Trên preview bấm `Mở PDF`: mở blob PDF bằng tab mới.
  - ⬜ Trên preview bấm `Tạo PDF lên Google Drive`: file được tạo trong thư mục Drive của học viên; nếu thiếu `driveFolderId` thì báo lỗi rõ.
  - ⬜ PDF hiển thị đúng thông tin học sinh và record kế hoạch.
  - ⬜ Đợt hiển thị đúng theo `startDate`/`dueDate`; độ tuổi tính đúng tại `startDate`.
  - ⬜ Record dùng `PlanGrade`/`PlanNote`, không lẫn `FinalGrade`/`FinalNote`.
  - ⬜ Bảng dài qua trang không bị cắt mất nội dung chính.
  - ⬜ Khi edit đang dirty, confirm xuất hiện trước khi mở preview; preview không tự save dữ liệu.
  - ⬜ Không có request backend `generate-plan-pdf` cũ khi mở/mở blob PDF.
