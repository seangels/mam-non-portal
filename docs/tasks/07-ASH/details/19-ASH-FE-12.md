# ASH-FE-12 — Preview/In Kết Quả PDF bằng html2pdf

Nguồn tham chiếu: [`ASH-FE-11`](18-ASH-FE-11.md) và [`docs/samples/khcn-standalone.html`](../../../samples/khcn-standalone.html).

## Mục đích

Thêm luồng preview/in PDF kết quả đánh giá cho AssessmentSheet. Người dùng bấm `In Kết Quả PDF` từ màn edit để mở trang preview HTML trước, xem đúng bố cục A4 tương tự luồng kế hoạch, rồi chọn mở blob PDF hoặc tạo file PDF vào thư mục Google Drive của học viên.

## Tóm tắt ngắn

1. ✅ Thêm nút `In Kết Quả PDF` trên màn edit AssessmentSheet.
2. ✅ Nút chỉ enable khi sheet đã lưu, có record, không loading/saving/mutate và `originalStatus !== 'Open'`.
3. ✅ Tạo route/trang preview riêng cho kết quả PDF, dùng lại cơ chế `html2pdf.js`.
4. ✅ PDF kết quả dùng `FinalGrade` và `FinalNote`, không dùng `PlanGrade`/`PlanNote`.
5. ✅ Có nút `Tạo PDF lên Google Drive`; backend lưu link vào `ResultFileLinkPdf`.
6. ✅ UI/label/thông báo lỗi dùng tiếng Việt.
7. ✅ Không đổi database/auth/IIS; thêm companion REST endpoint upload result PDF, không thêm migration.
8. ⚠️ Chưa smoke thủ công và chưa kiểm tra lưu PDF Google Drive thật trong lượt này.

## Phạm vi

- ✅ Thay đổi frontend trong `ui/`.
- ✅ Thay đổi backend nhỏ trong `api/` cho endpoint upload result PDF.
- ✅ Tái sử dụng component/model/CSS preview kế hoạch để code gọn và giữ layout nhất quán.
- ✅ Cập nhật docs task/memory liên quan.
- ✅ Không thêm migration DB.
- ✅ Không đổi auth/routing/IIS.
- ✅ Không chạy production/IIS/deploy.

## Nội dung cần làm

- ✅ Thêm nút `In Kết Quả PDF` cạnh nhóm action records trên màn edit.
  - ✅ Text nút là tiếng Việt: `In Kết Quả PDF`.
  - ✅ Hint nêu rõ mở trang preview kết quả trước khi tạo PDF.
  - ✅ Disable khi:
    - ✅ đang create mới hoặc chưa có `assessmentSheetId`.
    - ✅ đang `loading`.
    - ✅ đang `saving`.
    - ✅ đang thêm/xóa record.
    - ✅ chưa có record.
    - ✅ `originalStatus === 'Open'`.
- ✅ Khi màn edit đang dirty và user bấm `In Kết Quả PDF`, hiển thị confirm tiếng Việt giống luồng kế hoạch: preview dùng dữ liệu đã lưu, muốn PDF mới nhất thì lưu trước.
- ✅ Tạo route preview:
  - ✅ Route: `/#/assessment-sheets/:id/result-pdf-preview`.
  - ✅ Guard/role giống màn edit và plan preview.
  - ✅ Load lại detail theo `id` để refresh trực tiếp được.
  - ✅ Có nút `Quay lại` về màn edit.
  - ✅ Có nút `Mở PDF` tạo blob URL bằng `html2pdf().outputPdf('bloburl')`.
  - ✅ Có nút `Tạo PDF lên Google Drive`.
- ✅ Backend companion upload:
  - ✅ Endpoint: `POST /api/v1/assessment-sheets/{id}/upload-result-pdf`.
  - ✅ Nhận `multipart/form-data` field `file`.
  - ✅ Validate giống `upload-plan-pdf`: PDF, tối đa 10 MB.
  - ✅ Upload vào `Student.DriveFolderId`.
  - ✅ Cập nhật `AssessmentSheet.ResultFileLinkPdf`.
  - ✅ Trả lại `AssessmentSheetDetail`.
  - ✅ Nếu học viên chưa có Drive folder id, trả lỗi hiện có `StudentDriveFolderRequired`.
- ✅ Mapping dữ liệu in:
  - ✅ Thông tin học sinh lấy từ snapshot/detail đang load.
  - ✅ Ngày sinh format `dd/MM/yyyy`.
  - ✅ Độ tuổi tính tại `startDate`.
  - ✅ Đợt đánh giá tính từ `startDate`/`dueDate`.
  - ✅ Bảng record dùng `FinalGrade` và `FinalNote`.
  - ✅ Grade trống hiển thị theo config hiện có cho `Chưa có`.
  - ✅ Tên file PDF dạng an toàn, prefix `kq - <studentCode>.<nick>_<assessmentName>.pdf`.
- ✅ Unit/integration test:
  - ✅ Helper build preview kết quả dùng `FinalGrade`/`FinalNote`, không dùng plan fields.
  - ✅ Filename result PDF đúng prefix `kq`.
  - ✅ Nút `In Kết Quả PDF` chỉ bật khi `originalStatus !== 'Open'` và có record.
  - ✅ Route result preview được khai báo.
  - ✅ Backend endpoint upload result PDF có coverage trong integration test.

## Kiểm thử mong đợi

- ✅ `dotnet build api/AdminPortal.slnx -c Release --no-restore` — pass 0 warning/error.
- ✅ `dotnet test api/tests/AdminPortal.UnitTests -c Release --no-restore` — pass 85/85.
- ✅ `dotnet test api/tests/AdminPortal.IntegrationTests -c Release --no-restore` — pass 29/29 khi chạy ngoài sandbox để truy cập môi trường integration.
- ✅ `npm --prefix ui run test:ci` — pass 116/116.
- ✅ `npm --prefix ui run build -- --configuration development` — pass, chỉ còn warning CommonJS/DevExtreme/html2pdf quen thuộc.
- ⚠️ Chưa smoke thủ công màn edit/result preview và chưa kiểm tra lưu file Drive thật.
