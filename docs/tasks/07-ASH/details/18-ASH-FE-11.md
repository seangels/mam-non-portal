# ASH-FE-11 — Thêm nút In Kế hoạch PDF bằng html2pdf

Nguồn mẫu: [`docs/samples/khcn-standalone.html`](../../../samples/khcn-standalone.html).

## Mục đích

Thêm thao tác in/xuất PDF kế hoạch cá nhân trực tiếp trên UI AssessmentSheet, dựa theo layout A4 trong file mẫu `khcn-standalone.html`. PDF được tạo ở phía trình duyệt bằng thư viện `html2pdf.js`, phục vụ nhu cầu in nhanh kế hoạch hiện tại mà không phụ thuộc vào luồng Google Sheet/Drive PDF hiện có.

## Tóm tắt ngắn

1. ⬜ Thêm nút `In Kế hoạch PDF` trên màn edit AssessmentSheet.
2. ⬜ Dùng `html2pdf.js` phía frontend để tạo PDF từ HTML template, dựa theo mẫu `docs/samples/khcn-standalone.html`.
3. ⬜ PDF sử dụng dữ liệu kế hoạch hiện tại của sheet:
   - ⬜ thông tin học sinh
   - ⬜ tên/đợt đánh giá
   - ⬜ danh sách Assessment Record
   - ⬜ `PlanGrade`
   - ⬜ `PlanNote`
4. ⬜ Không gọi backend endpoint `generate-plan-pdf`, không cập nhật `PlanFileLinkPdf`, không tạo/cập nhật file Google Drive trong task này.
5. ⬜ Không đổi REST contract, không đổi backend, không đổi auth/routing/IIS.
6. ⬜ UI, thông báo lỗi, tên file PDF dùng tiếng Việt.

## Phạm vi

- ⬜ Chỉ thay đổi frontend trong `ui/`.
- ⬜ Có thể thêm dependency frontend `html2pdf.js` theo version cố định, ưu tiên `0.10.1` như file mẫu đang dùng.
- ⬜ Không dùng CDN trong Angular app; thư viện phải được import/bundle từ npm để chạy ổn định khi deploy nội bộ.
- ⬜ Nếu package không có type phù hợp với TypeScript 4.3.5, thêm declaration nhỏ, rõ ràng, chỉ đủ cho API đang dùng.
- ⬜ Không chạy production/IIS/deploy.

## Nội dung cần làm

- ⬜ Thêm nút `In Kế hoạch PDF` ở màn edit AssessmentSheet.
  - ⬜ Đặt gần nhóm action hiện có của AssessmentSheet, không làm rối sticky form-actions.
  - ⬜ Disable khi đang `loading`, `saving`, đang thêm/xóa record, hoặc chưa có record.
  - ⬜ Hiển thị trạng thái đang tạo PDF để tránh bấm nhiều lần.
- ⬜ Tạo phần render HTML dùng riêng cho PDF.
  - ⬜ Dựa theo cấu trúc A4 trong `khcn-standalone.html`: tiêu đề `KẾ HOẠCH CÁ NHÂN`, thông tin trẻ, bảng kế hoạch, nhận xét phụ huynh, số trang.
  - ⬜ Không copy nguyên inline script/CDN từ file mẫu vào component.
  - ⬜ Tách CSS in PDF riêng khỏi CSS bảng edit nếu cần để tránh làm vỡ UI chính.
  - ⬜ Render vùng PDF ẩn/off-screen hoặc component con riêng để `html2pdf` capture đúng DOM.
- ⬜ Mapping dữ liệu từ AssessmentSheet hiện tại vào template.
  - ⬜ Tên học sinh lấy từ snapshot/detail đang load.
  - ⬜ Ngày sinh hiển thị dạng `dd/MM/yyyy` nếu có.
  - ⬜ Độ tuổi nếu chưa có helper hiện tại thì có thể tạm không hiển thị hoặc tính từ ngày sinh theo local date; không gọi API mới chỉ để tính tuổi.
  - ⬜ Đợt/tên sheet lấy từ `AssessmentSheet.name` nếu DTO/UI hiện có có field này; nếu form hiện tại chưa expose `name`, ghi rõ blocker/điều chỉnh cần bổ sung trước khi code.
  - ⬜ Bảng record dùng `PlanGrade` và `PlanNote`, không dùng `FinalGrade`/`FinalNote`.
  - ⬜ Grade hiển thị bằng nhãn tiếng Việt từ config hiện có (`Đạt +`, `Chưa đạt -`, `Hỗ trợ +`, `Hỗ trợ -`, `Chưa có` nếu trống).
  - ⬜ Nhóm lớn/nhóm nhỏ/row order tái sử dụng logic/config hiện có trong `api.models.assessment-sheets` và records table để đồng nhất màu/thứ tự.
- ⬜ Hành vi khi dữ liệu chưa lưu.
  - ⬜ PDF ưu tiên phản ánh dữ liệu đang hiển thị trên màn hình, gồm cả thay đổi chưa lưu.
  - ⬜ Nếu form đang dirty, hiển thị confirm tiếng Việt trước khi in: PDF sẽ dùng dữ liệu đang nhập hiện tại và chưa tự lưu vào hệ thống.
  - ⬜ Không tự động gọi save trước khi in.
- ⬜ Sinh file PDF bằng `html2pdf.js`.
  - ⬜ Cấu hình A4 portrait, `margin: 0`, `html2canvas.scale: 2`, background trắng, filename tiếng Việt không dấu an toàn ví dụ `ke-hoach-ca-nhan-<ma-hoc-sinh>.pdf`.
  - ⬜ Có fallback `window.print()` nếu thư viện không load được.
  - ⬜ Bắt lỗi và báo `Không thể tạo PDF kế hoạch. Vui lòng thử lại.`
- ⬜ Hỗ trợ nhiều record.
  - ⬜ Không hard-code chỉ một trang theo dữ liệu mẫu.
  - ⬜ Có quy tắc page-break để bảng dài có thể sang trang A4 tiếp theo.
  - ⬜ Header bảng nên lặp lại hoặc tối thiểu không làm mất cấu trúc khi qua trang.

## Ghi chú thiết kế

- ⬜ Task này là luồng in PDF local trên UI, khác với luồng backend `generate-plan-pdf`/Google Drive đã có trong ASH-BE-04.
- ⬜ Không ghi link PDF về database trong task này; nếu sau này cần lưu file/link thì tạo task contract/backend riêng.
- ⬜ Tránh tạo object literal mới trong template cho DevExtreme 19.2.5; config/options nên là property ổn định trong component.
- ⬜ Không dùng API mới của Angular sau 12.2.17; giữ NgModule/component pattern hiện tại.
- ⬜ Ưu tiên tách helper build dữ liệu in để có unit test nhẹ, thay vì nhồi toàn bộ logic vào click handler.

## Kiểm thử mong đợi

- ⬜ Cập nhật/thêm unit test cho helper build dữ liệu in:
  - ⬜ map đúng student snapshot.
  - ⬜ dùng `PlanGrade`/`PlanNote`, không dùng `FinalGrade`/`FinalNote`.
  - ⬜ giữ đúng sort/order nhóm như records table.
  - ⬜ tạo filename an toàn.
- ⬜ Cập nhật/thêm test component state nếu có helper cho dirty confirm/loading.
- ⬜ Chạy `npm --prefix ui run test:ci`.
- ⬜ Chạy `npm --prefix ui run build -- --configuration development`.
- ⬜ Smoke thủ công màn edit AssessmentSheet:
  - ⬜ Bấm `In Kế hoạch PDF` khi không dirty: tải/mở PDF được.
  - ⬜ PDF hiển thị đúng thông tin học sinh và record kế hoạch.
  - ⬜ Record dùng `PlanGrade`/`PlanNote`, không lẫn `FinalGrade`/`FinalNote`.
  - ⬜ Bảng dài qua trang không bị cắt mất nội dung chính.
  - ⬜ Khi dirty, confirm xuất hiện; chọn tiếp tục thì PDF dùng dữ liệu đang nhập, chọn hủy thì không tạo PDF.
  - ⬜ Không có request backend `generate-plan-pdf` và không cập nhật `PlanFileLinkPdf`.
