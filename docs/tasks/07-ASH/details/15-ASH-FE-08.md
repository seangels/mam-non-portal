# ASH-FE-08 — Xóa từng mục đánh giá trong records-panel

## Mục đích

Cho phép người dùng xóa từng mục đánh giá đã có trong section `records-panel` của màn edit AssessmentSheet.

## Nội dung cần làm

- ✅ Thêm nút icon xóa cho mỗi dòng record trong `records-panel`.
- ✅ Khi bấm xóa, hỏi xác nhận trước khi lưu vì đây là thao tác mất dữ liệu khỏi bảng đánh giá hiện tại.
- ✅ Lưu thay đổi bằng endpoint full-replace records hiện có `PUT /api/v1/assessment-sheets/{id}/records`.
- ✅ Giữ nguyên `planGrade`/`planNote`/`finalGrade`/`finalNote` của các dòng còn lại.
- ✅ Nếu sheet đang `Done`, không cho xóa.
- ✅ Không cho xóa dòng cuối cùng vì backend hiện yêu cầu danh sách records có ít nhất một mục.
- ✅ Nếu không map được record còn lại sang assessment hiện hành để build request full-replace an toàn, chặn thao tác và báo lỗi thay vì làm mất record.

## Kết quả mong đợi

- ✅ Mỗi record card có nút xóa từng dòng.
- ✅ Sau confirm, dòng được xóa biến mất khỏi danh sách khi API trả về thành công.
- ✅ Dữ liệu các dòng còn lại được round-trip đầy đủ.
- ✅ Không đổi REST contract backend; chỉ dùng endpoint records đã có.
- ✅ Có test frontend cho request mapping xóa tối thiểu.
- ✅ Chạy `npm --prefix ui run test:ci` và `npm --prefix ui run build -- --configuration development`.
