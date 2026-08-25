# ASH-FE-09 — Chuyển records-panel sang dạng table nhóm màu theo groupLv2

## Mục đích

Thiết kế lại section `records-panel` trên màn edit AssessmentSheet từ dạng card sang dạng table giống mẫu người dùng cung cấp, giúp giáo viên/admin xem và nhập kết quả đánh giá theo nhóm rõ hơn, ít cuộn ngang/dọc lộn xộn hơn và dễ đối chiếu theo từng nhóm năng lực.

## Phạm vi

- ✅ Chỉ thay đổi UI/UX của `records-panel` trong màn edit AssessmentSheet.
- ✅ Không đổi backend, không đổi REST contract, không đổi payload `PUT /api/v1/assessment-sheets/{id}/records`.
- ✅ Giữ lại các hành vi đã có:
  - ✅ Xóa từng dòng bằng endpoint full-replace records hiện có.
  - ✅ Chặn thao tác khi sheet đang `Done`.
  - ✅ Chặn xóa dòng cuối cùng.
  - ✅ Giữ dirty guard / save guard hiện có.
  - ✅ Giữ sticky `form-actions` nằm ngoài form.
- ✅ Không đụng production/IIS/deploy.

## Bố cục table cần làm

- ✅ Section `records-panel` hiển thị records bằng table thay cho list card.
- ✅ Cột chứa nút xóa nằm sau cột `Ghi chú`.
  - ✅ Dùng icon-only trash giống `ASH-FE-08`.
  - ✅ Vẫn dùng confirm trước khi xóa.
  - ✅ Dòng không được xóa thì button disabled và có hint tiếng Việt.
- ✅ Các cột chính theo thứ tự:
  1. ✅ Nhóm lớn `groupLv2`.
  2. ✅ Nhóm nhỏ `groupLv3`.
  3. ✅ `STT`.
  4. ✅ `Nội dung đánh giá`.
  5. ✅ `Kế hoạch`.
  6. ✅ `Kết quả hiện tại`.
  7. ✅ `Ghi chú`.
  8. ✅ Thao tác xóa.
- ✅ `groupLv2` và `groupLv3` cần hiển thị dạng nhóm dễ nhìn:
  - ✅ Dùng `rowspan` theo các nhóm liên tiếp để nhìn gần mẫu bảng.
  - ✅ Không cần fallback lặp text vì rowspan build/test đã ổn trên Angular 12.
  - ✅ Chữ nhóm hiển thị ngang bình thường, không xoay dọc để tránh tốn diện tích theo chiều cao.
- ✅ `STT` reset theo từng nhóm nhỏ `groupLv3` liên tiếp.
- ✅ `Nội dung đánh giá` hiển thị `assessment.code` + `assessment.name`, ví dụ `B97. Đứng một chân trong 3 giây`.
- ✅ `Kế hoạch` hiển thị `planGrade` thành cột riêng nằm trước `Kết quả hiện tại`.
- ✅ Header `Kết quả hiện tại` có checkbox `Hiện kế hoạch` để bật/ẩn cột `Kế hoạch`.
- ✅ `Kết quả hiện tại` dùng dropdown grade hiện có, hiển thị tiếng Việt.
- ✅ `Ghi chú` dùng textarea gọn trong cell, giới hạn 2.000 ký tự theo backend `FinalNote`.

## Màu sắc cố định theo `groupLv2`

Các màu dưới đây là cố định theo tên nhóm `groupLv2`; không phụ thuộc theme DevExtreme:

| `groupLv2` | Màu nền |
|---|---|
| Tiền tiểu học | `#DCC1CF` |
| Cá nhân và xã hội | `#D0E0E3` |
| Phát triển ngôn ngữ | `#C9DAF8` |
| Phát triển nhận thức | `#C7B7D2` |
| Phát triển thể chất | `#C9DAF8` |

- ✅ Áp màu nền cho toàn bộ dòng hoặc vùng group tương ứng để người dùng nhìn thấy ranh giới nhóm.
- ✅ So khớp tên nhóm bằng normalize tiếng Việt: trim, không nhạy hoa/thường và không nhạy dấu.
- ✅ Nhóm chưa nằm trong danh sách cố định dùng màu mặc định trung tính, không tự sinh màu ngẫu nhiên.

## Hành vi chỉnh sửa dữ liệu

- ✅ `Kết quả hiện tại` bind vào `finalGrade`.
- ✅ `Ghi chú` bind vào `finalNote`.
- ✅ Thay đổi dropdown/ghi chú làm form dirty giống các field hiện tại.
- ✅ Khi bấm `Lưu thay đổi`, payload gửi đầy đủ record hiện có theo contract hiện tại.
- ✅ Không tự động save từng cell trong version này.

## Responsive và khả năng đọc

- ✅ Desktop/tablet: table chiếm đủ chiều ngang section, header sticky trong section.
- ✅ Màn nhỏ: cho phép horizontal scroll trong `records-panel`, không phá layout sticky action bên dưới.
- ✅ Không hard-code width quá cứng; có `min-width` hợp lý để giữ bảng dễ đọc.
- ✅ Font trong `records-panel` và `assessment-picker` dùng mức `small`; riêng ô `Nhóm lớn`/`Nhóm nhỏ` có rowspan 1 dòng dùng `smaller`.
- ✅ Toàn bộ label/hint/confirm/error hiển thị tiếng Việt.

## Kiểm thử mong đợi

- ✅ Thêm test/helper cho mapping màu `groupLv2` và grouping/rowspan.
- ✅ Thêm test request mapping để lưu records sau khi edit kết quả hiện tại/ghi chú.
- ✅ Chạy `npm --prefix ui run test:ci`.
- ✅ Chạy `npm --prefix ui run build -- --configuration development`.
- ⬜ Smoke thủ công màn edit AssessmentSheet:
  - ⬜ Table render đúng nhóm màu.
  - ⬜ Xóa từng dòng vẫn confirm và cập nhật danh sách.
  - ⬜ Đổi dropdown kết quả và ghi chú vẫn lưu được.
  - ⬜ Sheet `Done` không cho chỉnh/xóa.
