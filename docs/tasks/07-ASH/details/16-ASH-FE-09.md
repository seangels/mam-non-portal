# ASH-FE-09 — Chuyển records-panel sang dạng table nhóm màu theo groupLv2

## Mục đích

Thiết kế lại section `records-panel` trên màn edit AssessmentSheet từ dạng card sang dạng table giống mẫu người dùng cung cấp, giúp giáo viên/admin xem và nhập kết quả đánh giá theo nhóm rõ hơn, ít cuộn ngang/dọc lộn xộn hơn và dễ đối chiếu theo từng nhóm năng lực.

## Phạm vi

- ⬜ Chỉ thay đổi UI/UX của `records-panel` trong màn edit AssessmentSheet.
- ⬜ Không đổi backend, không đổi REST contract, không đổi payload `PUT /api/v1/assessment-sheets/{id}/records`.
- ⬜ Giữ lại các hành vi đã có:
  - ⬜ Xóa từng dòng bằng endpoint full-replace records hiện có.
  - ⬜ Chặn thao tác khi sheet đang `Done`.
  - ⬜ Chặn xóa dòng cuối cùng.
  - ⬜ Giữ dirty guard / save guard hiện có.
  - ⬜ Giữ sticky `form-actions` nằm ngoài form.
- ⬜ Không đụng production/IIS/deploy.

## Bố cục table cần làm

- ⬜ Section `records-panel` hiển thị records bằng table thay cho list card.
- ⬜ Cột chứa nút xóa nằm đầu tiên của mỗi dòng.
  - ⬜ Dùng icon-only trash giống `ASH-FE-08`.
  - ⬜ Vẫn dùng confirm trước khi xóa.
  - ⬜ Dòng không được xóa thì button disabled và có hint tiếng Việt.
- ⬜ Các cột chính theo thứ tự:
  1. ⬜ Thao tác xóa.
  2. ⬜ Nhóm lớn `groupLv2`.
  3. ⬜ Nhóm nhỏ `groupLv3`.
  4. ⬜ `STT`.
  5. ⬜ `Nội dung đánh giá`.
  6. ⬜ `Kết quả hiện tại`.
  7. ⬜ `Ghi chú`.
- ⬜ `groupLv2` và `groupLv3` cần hiển thị dạng nhóm dễ nhìn:
  - ⬜ Ưu tiên merge/rowspan theo nhóm nếu triển khai gọn và ít rủi ro với Angular 12.
  - ⬜ Nếu rowspan làm template quá phức tạp, có thể dùng cách lặp text ở dòng đầu nhóm và để ô rỗng ở các dòng sau, miễn nhìn giống nhóm và không gây sai dữ liệu.
- ⬜ `STT` đánh số theo từng nhóm con hoặc theo thứ tự hiển thị hiện có của assessment; trước khi code cần đối chiếu dữ liệu hiện tại để chọn cách ít gây lệch nhất.
- ⬜ `Nội dung đánh giá` hiển thị `assessment.code` + `assessment.name`, ví dụ `B97. Đứng một chân trong 3 giây`.
- ⬜ `Kết quả hiện tại` dùng dropdown grade hiện có, hiển thị tiếng Việt.
- ⬜ `Ghi chú` dùng input/textarea gọn trong cell, giữ giới hạn/validation hiện hành nếu source đang có.

## Màu sắc cố định theo `groupLv2`

Các màu dưới đây là cố định theo tên nhóm `groupLv2`; không phụ thuộc theme DevExtreme:

| `groupLv2` | Màu nền |
|---|---|
| Tiền tiểu học | `#DCC1CF` |
| Cá nhân và xã hội | `#D0E0E3` |
| Phát triển ngôn ngữ | `#C9DAF8` |
| Phát triển nhận thức | `#C7B7D2` |
| Phát triển thể chất | `#C9DAF8` |

- ⬜ Áp màu nền cho toàn bộ dòng hoặc vùng group tương ứng để người dùng nhìn thấy ranh giới nhóm.
- ⬜ So khớp tên nhóm nên xử lý trim và không nhạy hoa/thường; nếu dữ liệu có dấu khác biệt nhẹ thì vẫn cố gắng map ổn định ở tầng UI.
- ⬜ Nhóm chưa nằm trong danh sách cố định sẽ dùng màu mặc định trung tính, không tự sinh màu ngẫu nhiên.

## Hành vi chỉnh sửa dữ liệu

- ⬜ `Kết quả hiện tại` ưu tiên bind vào trường kết quả hiện hành đang dùng cho màn edit (`finalGrade` nếu form hiện tại dùng cặp kết quả cuối; giữ đúng mapping source khi triển khai).
- ⬜ `Ghi chú` ưu tiên bind vào ghi chú kết quả hiện hành (`finalNote` nếu form hiện tại dùng cặp kết quả cuối; giữ đúng mapping source khi triển khai).
- ⬜ Thay đổi dropdown/ghi chú phải làm form dirty giống các field hiện tại.
- ⬜ Khi bấm `Lưu thay đổi`, payload vẫn gửi đầy đủ record hiện có theo contract hiện tại.
- ⬜ Không tự động save từng cell trong version này.

## Responsive và khả năng đọc

- ⬜ Desktop/tablet: table chiếm đủ chiều ngang section, header sticky trong section nếu làm được gọn.
- ⬜ Màn nhỏ: cho phép horizontal scroll trong `records-panel`, không phá layout sticky action bên dưới.
- ⬜ Không hard-code width quá cứng; ưu tiên width tối thiểu hợp lý cho cột nội dung và cell nhập liệu.
- ⬜ Toàn bộ label/hint/confirm/error hiển thị tiếng Việt.

## Kiểm thử mong đợi

- ⬜ Thêm test/helper cho mapping màu `groupLv2` nếu tách được logic thuần.
- ⬜ Thêm/điều chỉnh test đảm bảo nút xóa vẫn gọi flow đã có sau khi đổi layout table.
- ⬜ Chạy `npm --prefix ui run test:ci`.
- ⬜ Chạy `npm --prefix ui run build -- --configuration development`.
- ⬜ Smoke thủ công màn edit AssessmentSheet:
  - ⬜ Table render đúng nhóm màu.
  - ⬜ Xóa từng dòng vẫn confirm và cập nhật danh sách.
  - ⬜ Đổi dropdown kết quả và ghi chú vẫn lưu được.
  - ⬜ Sheet `Done` không cho chỉnh/xóa.
