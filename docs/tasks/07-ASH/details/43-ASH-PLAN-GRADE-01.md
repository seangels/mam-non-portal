# ASH-PLAN-GRADE-01 — Sửa mức đánh giá kế hoạch

## Tóm tắt ngắn

1. ✅ Cho Teacher/Admin/SuperAdmin sửa riêng `PlanGrade` ở cột Kế hoạch khi trạng thái đang chọn là `Open` hoặc `Canceled`; `Planed`/`Done` chỉ đọc.
2. ✅ Dùng dropdown màu với bốn mức hiện có và lựa chọn để trống; giữ nguyên `PlanNote`, kết quả và latest.
3. ✅ Lưu bằng `Lưu thay đổi`, giữ dirty guard và khóa control/handler khi đang có thao tác dữ liệu; hoàn thành kế hoạch lưu mức mới trước khi chuyển `Planed`.
4. ✅ Frontend `test:ci` đạt 222/222 và development build thành công bằng Node portable; đã cập nhật bằng chứng và memory.

## Triển khai

- ✅ Frontend sửa form assessment-sheets, tận dụng grade options, style và luồng lưu full-replace sẵn có; cột Kế hoạch rộng 100px như cột Kết quả. Không đổi API/DTO/database.
- ✅ Khóa sửa khi tải, lưu, hoàn thành kế hoạch, thêm/xóa record hoặc sheet, cập nhật catalog, chuẩn bị/gửi kết quả.
- ✅ Cập nhật requirements 09, dashboard/log ASH và frontend memory.

## Kiểm chứng

- ✅ Tám regression tests trong `assessment-sheets-plan-grade.spec.ts`: các role/trạng thái, đổi trạng thái đang chọn, busy lock/handler, dirty/hoàn tác/beforeunload, lưu và tải lại A/null, hoàn thành kế hoạch thành công/thất bại. Các ca lưu/tải lại dùng service stub, không gọi API thật.
- ✅ Mức kế hoạch mới được lưu trước khi đổi trạng thái; lỗi lưu giữ draft và không chuyển `Planed`.
- ✅ `npm --prefix ui run test:ci`: **222/222 pass**, exit 0 (214 test cũ + 8 test mới).
- ✅ `npm.cmd --prefix ui run build -- --configuration=development`: **pass**, exit 0, hash `d04cf984caee75ab5868`; chỉ CommonJS warnings đã biết. Cả hai lệnh dùng Node portable 14.21.3/npm 8.19.4 sau khi dot-source wrapper trong PowerShell với execution policy ở phạm vi process.
- ✅ Root review source/test và `git diff --check`: sạch ngoài cảnh báo LF/CRLF quen thuộc.
- ⚠️ Browser smoke và API thật chưa chạy; không chạy backend tests, production/IIS/deploy vì thay đổi chỉ ở frontend.
