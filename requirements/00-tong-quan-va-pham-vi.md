# 00 — Tổng quan và phạm vi

## 1. Mục tiêu sản phẩm

GV Portal là cổng quản trị mầm non gồm REST API và giao diện web, phục vụ:

- Đăng nhập an toàn, không có đăng ký công khai.
- Quản lý tài khoản quản trị và giáo viên.
- Quản lý học sinh, nhóm, lịch học và phân công giáo viên.
- Giáo viên điểm danh học sinh thuộc nhóm mình phụ trách.
- Admin và SuperAdmin quản trị dữ liệu, xem và xử lý điểm danh trong phạm vi được cấp.
- Danh sách quản trị có phân trang, tìm kiếm, lọc và sắp xếp.
- Giao diện người dùng hoàn toàn bằng tiếng Việt, dùng được trên desktop, tablet và mobile.

## 2. Vai trò

### `SuperAdmin` — Siêu quản trị viên

- Quản lý tài khoản `Admin`.
- Quản lý giáo viên, học sinh, nhóm, lịch học và phân công.
- Xem, tạo, sửa phiếu điểm danh của mọi nhóm cho mọi ngày không nằm trong tương lai.
- Thực hiện khôi phục phiếu lịch sử khi đủ điều kiện.

### `Admin` — Quản trị viên

- Quản lý giáo viên, học sinh, nhóm, lịch học và phân công.
- Không quản lý tài khoản `Admin` hoặc tạo thêm `SuperAdmin`.
- Xem, tạo, sửa phiếu điểm danh của mọi nhóm cho mọi ngày không nằm trong tương lai.
- Thực hiện khôi phục phiếu lịch sử khi đủ điều kiện.

### `Teacher` — Giáo viên

- Đăng nhập và sử dụng màn hình điểm danh.
- Chỉ xem và điểm danh nhóm đang được phân công phụ trách.
- Bị giới hạn thời gian sửa phiếu từ 1 đến 7 ngày theo cấu hình riêng.
- Không sử dụng các màn hình quản trị tài khoản, giáo viên, học sinh hoặc nhóm.

## 3. Thuật ngữ chính

- **Nhóm hiện tại:** nhóm mà học sinh đang thuộc tại thời điểm hiện tại; không có lịch sử phân nhóm theo ngày hiệu lực.
- **Giáo viên phụ trách hiện tại:** giáo viên đang được gán cho nhóm; mỗi nhóm có tối đa một người phụ trách.
- **Lịch học hiện tại:** một hình thức học và tập ngày Thứ Hai–Thứ Bảy áp dụng ngay sau khi lưu.
- **Phiếu Missing:** ngày/nhóm chưa có phiếu được lưu; dữ liệu hiển thị chỉ là preview.
- **Phiếu Saved:** phiếu đã lưu đầy đủ record cho mọi học sinh thuộc snapshot của ngày đó.
- **Snapshot:** dữ liệu nhóm, giáo viên và học sinh được giữ nguyên trong phiếu đã lưu dù dữ liệu hiện tại thay đổi.
- **Historical recovery:** luồng quản trị tạo lại phiếu quá khứ từ roster được chọn thủ công khi snapshot hiện tại không còn đủ để tái dựng.

## 4. Phạm vi phiên bản hiện hành

- Xác thực bằng access token và refresh token.
- Khởi tạo `SuperAdmin` đầu tiên khi hệ thống chưa có bất kỳ user nào.
- CRUD mềm cho tài khoản, giáo viên và học sinh theo quyền.
- CRUD nhóm, phân công giáo viên và phân/chuyển/gỡ học sinh.
- Lịch học bắt buộc cho học sinh.
- Điểm danh theo ngày với full daily snapshot.
- Audit thao tác quản trị quan trọng, health check và lỗi API thống nhất.
- OpenAPI/Swagger cho môi trường phù hợp.
- Triển khai tách API/UI trên IIS local HTTPS và PostgreSQL 17.

## 5. Ngoài phạm vi hiện tại

- Đăng ký tài khoản công khai, quên mật khẩu hoặc kích hoạt qua email.
- Đăng nhập Google, Microsoft hoặc nhà cung cấp ngoài.
- Học phí, lương, hợp đồng, hồ sơ nhân sự mở rộng hoặc chấm công nhân viên.
- Upload ảnh/tài liệu, CCCD, thuế, BHXH, ngân hàng hoặc dữ liệu sức khỏe.
- Import/export Excel.
- Báo cáo tháng, dashboard chuyên cần hoặc thông báo tự động cho phụ huynh.
- QR, nhận diện khuôn mặt, định vị, check-in giờ đến/về hoặc offline-first.
- Lịch học phức tạp theo giờ, ca, tuần chẵn/lẻ, ngày lễ hoặc ngoại lệ từng ngày.
- Lịch sử `effectiveFrom/effectiveTo` cho nhóm, giáo viên phụ trách hoặc lịch học.
- Giáo viên tự chỉnh hồ sơ cá nhân.

## 6. Ràng buộc nền tảng

- Backend sử dụng .NET 10 và PostgreSQL 17.
- Giao diện quản trị sử dụng Angular/DevExtreme.
- API và UI có thể chạy khác origin; production bắt buộc HTTPS.
- Mã kỹ thuật và enum trong API có thể dùng tiếng Anh, nhưng mọi nội dung người dùng nhìn thấy hoặc screen reader đọc phải là tiếng Việt.
