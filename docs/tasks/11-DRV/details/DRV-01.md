# DRV-01 — Tự động quản lý thư mục Google Drive

## Tóm tắt ngắn

1. ✅ Thêm folder pool cấu hình và field Drive cho Teacher.
2. ✅ Tự tạo/tìm/move folder Student theo nhóm và giáo viên phụ trách.
3. ✅ Thêm action đổi tên riêng và reconcile dữ liệu cũ.
4. ✅ Bảo đảm lỗi Drive không âm thầm làm DB và Drive lệch nhau.
5. ⚠️ Test source và tài liệu đã xong; production package chưa tạo vì gate môi trường chưa đạt.

## Checklist

- ✅ Backend schema, migration, API và Google Drive adapter.
- ✅ Frontend Teacher/Student workflow bằng tiếng Việt.
- ⚠️ Unit/frontend đạt; integration không khởi chạy được do Docker không khả dụng.
- ✅ Cập nhật contract, requirements, memory và task log.
- ⚠️ `$gv-portal-production build` dừng trước packaging vì API đang chạy khóa Debug DLL; chưa có ZIP để kiểm tra.
