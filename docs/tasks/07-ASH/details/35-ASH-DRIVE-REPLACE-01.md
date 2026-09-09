# ASH-DRIVE-REPLACE-01 — Replace PDF trực tiếp trên Google Drive

## Tóm tắt ngắn

1. ✅ Khi link đã lưu có file ID, upload PDF kế hoạch/kết quả dùng Drive `Files.Update` để replace nội dung đúng file đó.
2. ✅ Không tạo file thay thế và không xóa file cũ; file ID/link giữ ổn định sau upload lại.
3. ✅ Khi chưa có file ID cũ, tiếp tục dùng `Files.Create` trong thư mục Drive của học sinh.
4. ✅ Không đổi endpoint, DTO, UI, database hoặc migration.
5. ✅ Đã bổ sung kiểm thử cho hai nhánh create/update và chạy backend verification phù hợp.

## Phạm vi

- Backend: `GoogleSheetsService.SavePdfToDriveAsync` và kiểm thử liên quan.
- Tài liệu: requirement, plan, API README, task log và agent memory.
- Ngoài phạm vi: frontend, production build, IIS và deploy.

## Điều kiện hoàn thành

- ✅ Quyết định nghiệp vụ mới được ghi nhận và ghi đè G8 ngày 2026-08-31.
- ✅ Upload lần đầu tạo file mới trong đúng thư mục học sinh.
- ✅ Upload lại cập nhật nội dung file theo ID hiện có và không gọi delete.
- ✅ Link trả về giữ cùng file ID khi update thành công.
- ✅ Backend unit tests 117/117 và Release solution build đạt 0 warning/error.
- ⚠️ Debug solution build bị khóa DLL bởi API process đang chạy; integration không chạy vì Docker daemon không khả dụng; chưa Google Drive live smoke.
