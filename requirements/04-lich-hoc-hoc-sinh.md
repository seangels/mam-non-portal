# 04 — Lịch học của học sinh

## 1. Mô hình nghiệp vụ

Mỗi học sinh có đúng một lịch học hiện tại gồm:

- Hình thức `FullDay` — hiển thị `Học cả ngày`.
- Hoặc hình thức `OneToOne` — hiển thị `Học 1-1`.
- Một đến sáu ngày học từ Thứ Hai đến Thứ Bảy.

Không hỗ trợ Chủ nhật trong v1.

## 2. Quy tắc lịch học

- Lịch học bắt buộc trên create và full update Student.
- Phải chọn ít nhất một và tối đa sáu ngày.
- Không nhận ngày trùng, `Sunday` hoặc giá trị ngoài danh sách.
- Thứ tự trả về luôn là Thứ Hai → Thứ Bảy, không phụ thuộc thứ tự client gửi.
- Student inactive vẫn giữ và có thể sửa lịch; chỉ Student active mới được phân nhóm và xuất hiện trong roster.
- Lịch mới có hiệu lực ngay sau khi lưu.
- Không có lịch sử phiên bản, ngày hiệu lực hoặc ngoại lệ theo từng ngày.
- Không cấu hình giờ bắt đầu/kết thúc, ca sáng/chiều hoặc nhiều block trong một ngày.

## 3. Ảnh hưởng tới điểm danh

- Roster preview của một ngày chỉ gồm Student active, đang thuộc nhóm và có ngày đó trong lịch học hiện tại.
- `FullDay` mặc định trạng thái điểm danh `Present`.
- `OneToOne` mặc định `OneToOneHour` với thời lượng 60 phút.
- Hình thức học chỉ đặt trạng thái gợi ý; người điểm danh vẫn có thể chọn mọi trạng thái hợp lệ khác.
- Context, preview và lúc tạo phiếu phải sử dụng cùng một roster theo lịch học.
- Khi không có học sinh có lịch, GET trả trạng thái read-only `NoScheduledStudents`; UI không hiển thị nút lưu phiếu.
- Không được tạo phiếu rỗng.
- Thay đổi schedule của Student đang thuộc nhóm phải làm snapshot nhóm thay đổi để request tạo phiếu dựa trên roster cũ bị từ chối.
- Phiếu Saved không được thêm/bớt record hoặc thay trạng thái khi lịch hiện tại thay đổi.
- Historical recovery không lọc theo lịch hiện tại; quản trị viên chọn roster thủ công và trạng thái mặc định là `Present`.
- Chủ nhật luôn không có roster theo lịch trong v1.

## 4. Version và xung đột

- Student có một version dùng chung cho full update, đổi nhóm và delete.
- Mọi full update hợp lệ tăng version một lần, kể cả payload không đổi.
- Request dùng version cũ phải trả conflict kèm version hiện tại và không ghi dữ liệu một phần.
- Thay đổi identity hoặc schedule khi Student đang thuộc nhóm làm snapshot version của nhóm tăng đúng một lần.

## 5. Giao diện lịch học

- Form tạo/sửa Student có section `Lịch học hằng tuần`.
- Chọn hình thức bằng radio/segmented control `Học cả ngày` hoặc `Học 1-1`.
- Có đúng sáu checkbox với nhãn đầy đủ `Thứ Hai` đến `Thứ Bảy`.
- Validation ít nhất một ngày phải focus đúng nhóm checkbox.
- Desktop dùng section full-width trong form; mobile dùng một cột.
- Checkbox và control phải dùng được bằng bàn phím, có group label/legend và touch target khoảng 44 px.
- Danh sách Student hiển thị dạng `Học cả ngày · T2, T3...` hoặc `Học 1-1 · T2, T4...`.
- UI không tự suy luận lại default điểm danh; phải hiển thị status backend trả về.

## 6. Ngoài phạm vi

- Lịch theo tuần chẵn/lẻ, ngày lễ, nghỉ bù hoặc ngoại lệ ngày cụ thể.
- Snapshot hình thức học trong attendance record riêng.
- Giáo viên 1-1 riêng ở cấp Student.
- Lịch sử `effectiveFrom/effectiveTo` của schedule.
