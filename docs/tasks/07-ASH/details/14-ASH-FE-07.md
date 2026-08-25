# ASH-FE-07 — Thêm mục đánh giá trong màn edit AssessmentSheet

## Mục đích

Cho phép người dùng mở màn chỉnh sửa bảng đánh giá và thêm thêm mục đánh giá vào danh sách record hiện có, không cần tạo lại bảng đánh giá từ đầu.

## Nội dung cần làm

- Thêm nút `Thêm mục đánh giá` trong khu vực `Danh mục đánh giá đã chọn` của màn edit.
- Khi bấm nút, hiển thị `app-assessment-picker` bên ngoài form, dùng lại bộ lọc/search/client cache/sync GGSheet hiện có.
- Picker trong màn edit hiển thị thêm cột thao tác:
  - Dòng chưa có trong sheet: hiện nút `Thêm`.
  - Dòng đã có record trong sheet: vẫn hiện dòng nhưng ẩn/disable nút thêm và hiển thị `Đã có`.
- Khi bấm `Thêm`, hỏi xác nhận trước khi lưu.
- Sau khi xác nhận, gọi endpoint full-replace records hiện có `PUT /api/v1/assessment-sheets/{id}/records`.
- Record mới lấy `latestGrade`/`latestNote` đang hiển thị trong picker để đưa vào `planGrade`/`planNote`; `finalGrade`/`finalNote` để trống.
- Nếu sheet đang `Done`, không cho thêm mục.
- Nếu không map được record cũ sang assessment hiện hành để build request full-replace an toàn, chặn thao tác và báo lỗi thay vì làm mất record.

## Kết quả mong đợi

- Màn create vẫn giữ behavior checkbox chọn nhiều dòng như hiện tại.
- Màn edit có thể thêm từng assessment bằng nút dòng trong picker.
- Danh sách picker vẫn hiển thị đủ assessment, bao gồm dòng đã có record.
- Dòng vừa thêm chuyển sang trạng thái `Đã có` sau khi API trả về.
- Không đổi REST contract backend; chỉ dùng endpoint records đã có.
- Có test frontend cho request mapping/add-mode tối thiểu.
- Chạy `npm --prefix ui run test:ci` và `npm --prefix ui run build -- --configuration development`.
