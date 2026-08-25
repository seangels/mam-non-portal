# ASH-FE-10 — Khóa thêm/xóa record theo trạng thái và đổi ghi chú record sang dxTextArea

## Mục đích

Chuẩn hóa hành vi chỉnh sửa trên màn edit AssessmentSheet theo trạng thái: `Open` cho thao tác đầy đủ, `Planed` chỉ khóa thêm/xóa Assessment Record nhưng vẫn cho nhập kết quả/ghi chú cuối, đồng thời đổi ô ghi chú từng dòng record sang DevExtreme `dxTextArea` cho đồng bộ UI.

## Tóm tắt ngắn

1. ⬜ `Planed` chỉ khóa:
   - ⬜ Thêm Assessment Record.
   - ⬜ Xóa Assessment Record.
2. ⬜ `Planed` vẫn cho:
   - ⬜ Nhập/sửa `FinalGrade`.
   - ⬜ Nhập/sửa `FinalNote`.
   - ⬜ Đổi giáo viên phụ trách nếu form/user role hiện tại có quyền.
3. ⬜ `Done` giữ khóa như hiện tại.
4. ⬜ Vẫn là UI-only, không đổi BE/REST contract.

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
- ⬜ Tạo helper/trạng thái UI để xác định quyền thao tác theo từng nhóm hành động.
  - ⬜ `Open`: cho phép chỉnh sửa đầy đủ theo UI hiện tại.
  - ⬜ `Planed`: chỉ khóa thêm/xóa Assessment Record; vẫn cho nhập `FinalGrade` và `FinalNote`.
  - ⬜ `Done`: tiếp tục khóa chỉnh sửa như hiện tại.
- ⬜ Khi trạng thái hiện tại là `Open`, UI vẫn cho phép:
  - ⬜ Đổi giáo viên phụ trách.
  - ⬜ Thêm Assessment Record bằng picker hiện có.
  - ⬜ Xóa Assessment Record từng dòng bằng icon trash hiện có.
  - ⬜ Chỉnh `Kết quả hiện tại`/`Ghi chú` record nếu màn hiện tại đang cho phép.
- ⬜ Khi trạng thái hiện tại là `Planed`, UI chỉ khóa thay đổi cấu trúc record:
  - ⬜ Không cho thêm Assessment Record.
  - ⬜ Không cho xóa Assessment Record.
  - ⬜ Vẫn cho đổi giáo viên phụ trách nếu form hiện tại có quyền đổi.
  - ⬜ Vẫn cho chỉnh `Kết quả hiện tại` (`FinalGrade`).
  - ⬜ Vẫn cho chỉnh `Ghi chú` từng dòng (`FinalNote`).
  - ⬜ Nút thêm/xóa record bị disabled và hint/message phải bằng tiếng Việt.
- ⬜ Nếu user đổi trạng thái từ `Open` sang `Planed` trên form nhưng chưa lưu:
  - ⬜ UI phải khóa ngay thao tác thêm/xóa Assessment Record theo trạng thái đang chọn trong form.
  - ⬜ UI vẫn cho nhập/lưu `FinalGrade` và `FinalNote`.
  - ⬜ Vẫn cho bấm `Lưu thay đổi` để lưu trạng thái `Planed`.
  - ⬜ Không tự động gọi API khi chỉ đổi trạng thái.
- ⬜ Sau khi sheet đã load từ API ở trạng thái `Planed`, form áp dụng rule khóa thêm/xóa record nhưng không khóa nhập kết quả cuối.

## Ghi chú thiết kế

- ⬜ Rule khóa chỉ bắt trên UI ở v1; backend giữ logic hiện tại.
- ⬜ Nên gom điều kiện khóa vào helper dễ đọc, ví dụ `canMutateRecords`, `canEditRecordValues`, `canEditHeader`, thay vì rải điều kiện `originalStatus === 'Done'` nhiều nơi.
- ⬜ Cần rà lại các chỗ đang dùng `originalStatus === 'Done'`: add/remove record cần khóa cả `Planed` và `Done`; edit `FinalGrade`/`FinalNote` chỉ nên khóa theo rule hiện tại của `Done`.
- ⬜ Tránh đổi auth flow, hash routing, IIS environment và REST contract.

## Kiểm thử mong đợi

- ⬜ Cập nhật/thêm unit test cho helper khóa thao tác theo trạng thái.
- ⬜ Cập nhật/thêm test đảm bảo `Open` cho add/remove record; `Planed` khóa add/remove nhưng vẫn cho edit `FinalGrade`/`FinalNote`; `Done` vẫn khóa như trước.
- ⬜ Chạy `npm --prefix ui run test:ci`.
- ⬜ Chạy `npm --prefix ui run build -- --configuration development`.
- ⬜ Smoke thủ công màn edit AssessmentSheet:
  - ⬜ Sheet `Open`: đổi giáo viên phụ trách được.
  - ⬜ Sheet `Open`: thêm/xóa Assessment Record được.
  - ⬜ Sheet `Open`: ghi chú từng dòng dùng `dxTextArea` và lưu được.
  - ⬜ Đổi status `Open` → `Planed` chưa lưu: UI khóa thêm/xóa record ngay nhưng vẫn lưu được.
  - ⬜ Sheet `Planed` load lại: không thêm/xóa record được, nhưng vẫn sửa `FinalGrade`/`FinalNote` được.
  - ⬜ Sheet `Done`: vẫn khóa như trước.
