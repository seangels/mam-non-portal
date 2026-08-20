# 06 — Giao diện điểm danh

## 1. Bố cục tổng thể

- Route sử dụng `/#/attendance` và hiển thị cho cả ba role.
- Trang có tiêu đề, panel lọc, trạng thái phiếu, summary, danh sách card và sticky save action.
- Panel lọc mặc định mở và có nút thu gọn/mở rộng với accessible state.
- Main daily list dùng compact card; popup historical recovery dùng danh sách chọn nhiều riêng.

## 2. Card học sinh

- Card chỉ hiển thị định danh `nickname · studentCode`.
- Không đưa `fullName` vào card, tooltip hoặc accessible name.
- Định danh nằm trong header ngang ở cả desktop và mobile; marker `Đã thay đổi` cũng nằm trong header này khi card dirty.
- Nội dung card gồm status select nhỏ gọn bo góc, tối đa một control điều kiện và textarea ghi chú.
- Card có trạng thái rõ cho dirty, invalid, read-only và disabled.
- Lỗi được phép làm card cao hơn; không được che hoặc cắt error message.

## 3. Status và control

| Trạng thái | Nhãn compact trong status select | Control phụ |
|---|---|---|
| Có mặt | `Có mặt` | Không có |
| Nghỉ cả ngày | `Nghỉ` | `Có phép` / `Không phép` |
| Nghỉ nửa ngày | `Nghỉ 1/2` | `Có phép` / `Không phép`; chi tiết buổi ghi trong notes |
| Học 1-1 | `1-1` | Không có control/chip riêng; dữ liệu vẫn là block cố định 60 phút |
| Chưa điểm danh | `Chưa điểm danh` | Không có |

- Dùng text đầy đủ/accessible label; màu không phải tín hiệu duy nhất.
- Chuyển status phải clear/set field điều kiện đúng nghiệp vụ và giữ ghi chú.
- `Chưa điểm danh` là status được lưu thật, không phải state tạm của UI.

## 4. Ghi chú

- Textarea khoảng hai dòng, có counter và có thể cuộn nội bộ khi cần.
- Người dùng chỉ được nhập/sửa tối đa 200 ký tự.
- Nếu dữ liệu cũ từ API dài hơn 200 ký tự, UI phải hiển thị và round-trip nguyên vẹn khi chưa chỉnh sửa.
- Ngay khi người dùng sửa dữ liệu legacy dài, validation yêu cầu rút còn tối đa 200 ký tự.
- Ghi chú không bị xóa khi đổi trạng thái.

## 5. Grid và responsive

- Grid phải fluid, không hard-code số cột nếu sidebar, zoom hoặc container không đủ rộng.
- Mục tiêu tại desktop 1366 px là 5 card/hàng khi không gian cho phép.
- Desktop nhỏ khoảng 1024 px hiển thị khoảng 4 card/hàng.
- Tablet thu về 2–3 card tùy container.
- Mobile dưới 700 px hiển thị một card/hàng, identity nằm ngang và control cao tối thiểu 44 px.
- Header định danh luôn nằm ngang và không chiếm một cột dọc riêng.
- Thường nhìn được khoảng 8–10 học sinh trong vùng thao tác; roster lớn cuộn dọc tới giới hạn 100, không phân trang.
- Không cần virtual scroll ở v1; danh sách phải giữ ổn định theo student ID.

## 6. Filter, summary và page states

- Search local không dấu trên mã, họ tên và nickname; card bị ẩn vẫn giữ draft.
- Status filter có đủ năm trạng thái.
- Summary tính trên toàn roster, không chỉ các card đang hiển thị.
- Có state tiếng Việt riêng cho loading, lỗi API, chưa được phân nhóm, chưa chọn nhóm, không có học sinh theo lịch và không có kết quả search.
- Missing phải hiển thị rõ `Phiếu chưa được lưu`; không diễn giải preview thành lịch sử có mặt.
- Saved read-only vẫn hiển thị snapshot nhưng khóa mọi control/save và giải thích lý do.
- Conflict không tự tải đè draft; phải có CTA tải bản mới.

## 7. Accessibility

- Toàn bộ label, tooltip, validation, dialog, toast, `aria-label` và screen-reader text bằng tiếng Việt.
- Mỗi card có accessible name chỉ gồm nickname và mã học sinh.
- Status và field phụ có label riêng gắn với đúng học sinh.
- Tab order trong card: status → field phụ → ghi chú → card tiếp theo.
- Focus ring rõ; card lỗi có text, không chỉ border/màu.
- Nickname/mã bị rút gọn trực quan vẫn phải có accessible text đầy đủ.
- Touch target mobile tối thiểu khoảng 44 px và layout dùng được ở zoom 200%.
- Màu trạng thái phải đạt mức tương phản WCAG AA ở enabled, focus, disabled và read-only.

## 8. Popup khôi phục lịch sử

- Student candidate hiển thị mã, họ tên, nickname, trạng thái inactive/đã xóa và ngữ cảnh nhóm/giáo viên hiện tại khi có.
- Dùng danh sách cao cố định có tìm kiếm `contains`, checkbox chọn nhiều, chọn tất cả trong trang và giới hạn 100 Student.
- Khi mở popup, selection Student được reset để tránh vô tình dùng lại lựa chọn của lần trước.
- Sau khi chọn, từng Student có control trạng thái, phép/không phép khi nghỉ và ghi chú tối đa 200 ký tự.
- Lý do khôi phục bắt buộc và tối đa 200 ký tự.
- Checkbox xác nhận phải nêu rõ danh sách phản ánh đúng nhóm, giáo viên và học sinh tại ngày đang khôi phục.

## 9. Ngôn ngữ giao diện

- Toàn portal chỉ hiển thị tiếng Việt cho người dùng và công nghệ hỗ trợ.
- Enum, route, JSON field và error code vẫn dùng identifier kỹ thuật tiếng Anh trong API/code nhưng phải được ánh xạ sang tiếng Việt trước khi render.
- Lỗi chưa biết dùng thông báo dự phòng tiếng Việt và có thể hiển thị `traceId` dưới nhãn `Mã tra cứu`.
- Không hiển thị raw exception, raw response, `ProblemDetails.title/detail` không bảo đảm tiếng Việt hoặc mã enum tiếng Anh.
- Ngày hiển thị `dd/MM/yyyy`, locale `vi-VN`; dữ liệu người dùng nhập được giữ nguyên văn.
