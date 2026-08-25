# ASH-FE-10 — Khóa chỉnh sửa AssessmentSheet theo trạng thái và đổi ghi chú record sang dxTextArea

## Mục đích

Chuẩn hóa hành vi chỉnh sửa trên màn edit AssessmentSheet để giáo viên/admin chỉ thao tác khi bảng đánh giá còn ở trạng thái `Open`, đồng thời đổi ô ghi chú từng dòng record sang DevExtreme `dxTextArea` cho đồng bộ UI.

## Phạm vi

- ⬜ Chỉ thay đổi frontend UI trong màn edit AssessmentSheet.
- ⬜ Không đổi backend, không đổi REST contract, không đổi policy/validation hiện tại dưới BE.
- ⬜ Không đổi payload `PUT /api/v1/assessment-sheets/{id}` và `PUT /api/v1/assessment-sheets/{id}/records`.
- ⬜ Không chạy production/IIS/deploy.

## Nội dung cần làm

- ⬜ Đổi ô `Ghi chú` của mỗi dòng trong `records-panel` từ native `<textarea>` sang DevExtreme `dxTextArea`.
  - ⬜ Vẫn bind vào `record.finalNote`.
  - ⬜ Vẫn giới hạn tối đa 2.000 ký tự theo backend `FinalNote`.
  - ⬜ Vẫn hiển thị tiếng Việt, placeholder `Ghi chú`.
  - ⬜ Giữ chiều cao gọn khoảng 1 hàng mặc định để không làm bảng cao lên.
  - ⬜ Không tạo object literal editor options mới trong template gây reload loop với DevExtreme 19.2.5.
- ⬜ Tạo helper/trạng thái UI để xác định sheet còn được chỉnh sửa hay không.
  - ⬜ `Open`: cho phép chỉnh sửa.
  - ⬜ `Planed`: khóa chỉnh sửa trên UI.
  - ⬜ `Done`: tiếp tục khóa chỉnh sửa như hiện tại.
- ⬜ Khi trạng thái hiện tại là `Open`, UI vẫn cho phép:
  - ⬜ Đổi giáo viên phụ trách.
  - ⬜ Thêm Assessment Record bằng picker hiện có.
  - ⬜ Xóa Assessment Record từng dòng bằng icon trash hiện có.
  - ⬜ Chỉnh `Kết quả hiện tại`/`Ghi chú` record nếu màn hiện tại đang cho phép.
- ⬜ Khi trạng thái hiện tại là `Planed`, UI không cho sửa đổi nữa:
  - ⬜ Không cho đổi giáo viên phụ trách.
  - ⬜ Không cho thêm Assessment Record.
  - ⬜ Không cho xóa Assessment Record.
  - ⬜ Không cho chỉnh `Kết quả hiện tại`.
  - ⬜ Không cho chỉnh `Ghi chú` từng dòng.
  - ⬜ Nút thao tác bị disabled và hint/message phải bằng tiếng Việt.
- ⬜ Nếu user đổi trạng thái từ `Open` sang `Planed` trên form nhưng chưa lưu:
  - ⬜ UI phải khóa ngay các thao tác chỉnh sửa record/giáo viên theo trạng thái đang chọn trong form.
  - ⬜ Vẫn cho bấm `Lưu thay đổi` để lưu trạng thái `Planed`.
  - ⬜ Không tự động gọi API khi chỉ đổi trạng thái.
- ⬜ Sau khi sheet đã load từ API ở trạng thái `Planed`, form hiển thị read-only theo rule trên.

## Ghi chú thiết kế

- ⬜ Rule khóa chỉ bắt trên UI ở v1; backend giữ logic hiện tại.
- ⬜ Nên gom điều kiện khóa vào helper dễ đọc, ví dụ `canEditAssessmentSheet`, `canMutateRecords`, `canEditRecordValues`, thay vì rải điều kiện `originalStatus === 'Done'` nhiều nơi.
- ⬜ Cần rà lại các chỗ đang dùng `originalStatus === 'Done'` để thay bằng rule mới bao gồm `Planed`.
- ⬜ Tránh đổi auth flow, hash routing, IIS environment và REST contract.

## Kiểm thử mong đợi

- ⬜ Cập nhật/thêm unit test cho helper khóa chỉnh sửa theo trạng thái.
- ⬜ Cập nhật/thêm test đảm bảo `Open` cho add/remove/change teacher còn `Planed`/`Done` bị khóa trên UI.
- ⬜ Chạy `npm --prefix ui run test:ci`.
- ⬜ Chạy `npm --prefix ui run build -- --configuration development`.
- ⬜ Smoke thủ công màn edit AssessmentSheet:
  - ⬜ Sheet `Open`: đổi giáo viên phụ trách được.
  - ⬜ Sheet `Open`: thêm/xóa Assessment Record được.
  - ⬜ Sheet `Open`: ghi chú từng dòng dùng `dxTextArea` và lưu được.
  - ⬜ Đổi status `Open` → `Planed` chưa lưu: UI khóa record/teacher ngay nhưng vẫn lưu được.
  - ⬜ Sheet `Planed` load lại: không sửa giáo viên, record, kết quả hiện tại, ghi chú.
  - ⬜ Sheet `Done`: vẫn khóa như trước.
