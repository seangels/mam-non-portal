# ASH-FE-02 — Form chi tiết: sửa plan/PlanGrade, Xuất sang Google Sheet/Đồng bộ

Owner: `frontend`. Phụ thuộc: `ASH-FE-01`, `ASH-BE-03`. Trạng thái: xem [`../status.md`](../status.md). Log lịch sử: [`../log.md`](../log.md).

> Cập nhật 2026-08-27: phần nút "Xuất sang Google Sheet"/"Đồng bộ" cho `[F01]` trong file này là legacy/removed. Form chi tiết hiện chỉ lưu plan/records trong portal; xem `ASH-CL-01`.

Nguồn: [plan mục 6](../../../plans/07-ASH-assessment-sheet.md#6-thiết-kế-google-sheets), [requirements 09 mục 6, 7](../../../requirements/09-bang-danh-gia-nang-luc.md), [sơ đồ luồng dữ liệu](../../../requirements/09-bang-danh-gia-nang-luc-so-do-du-lieu.md).

## Mục đích

Màn hình chi tiết một `AssessmentSheet`, phần **giai đoạn lập kế hoạch**: cho phép sửa lại plan/`PlanGrade`/`PlanNote` sau khi tạo, và hai nút hành động Google Sheet đầu tiên (Xuất, Đồng bộ) — cả hai đều là hành động nút bấm thủ công, không tự động, và **chỉ đụng tới sheet `data`** (sheet `khcn_template`/`KQ_template` chỉ được ghi khi sinh PDF, thuộc `ASH-FE-03`). **Nhập `FinalGrade`/`FinalNote` (giai đoạn kết quả) thuộc phạm vi `ASH-FE-03`, không phải task này.**

## Nội dung cụ thể cần làm

- Form chi tiết hiển thị danh sách `AssessmentRecord` hiện có, cho phép thêm/bớt mục (dùng lại bộ filter ở `ASH-FE-01`) và sửa `PlanGrade`/`PlanNote` trực tiếp — chặn sửa khi `Status = Done`. Không hiển thị/sửa `FinalGrade`/`FinalNote` ở màn hình này (thuộc `ASH-FE-03`).
- Nút "Xuất sang Google Sheet": gọi `POST .../export-to-sheet`, hiển thị kết quả (link tới file `[F01]` riêng của `AssessmentSheet` này — lần đầu bấm sẽ tạo file mới bằng cách copy file mẫu `gen_assessment_sheet`, lần sau tái sử dụng đúng file đó) hoặc lỗi rõ ràng nếu thất bại. **Không** cần hiển thị sheet `khcn_template`/`KQ_template` riêng ở bước này (dữ liệu của chúng chưa được cập nhật cho tới khi sinh PDF).
- Nút "Đồng bộ": gọi `POST .../sync-to-sheet` sau khi sửa plan, không tự động chạy theo mỗi lần lưu; chỉ cập nhật sheet `data` (đủ cả `Plan*`/`Final*` hiện có, dù `Final*` có thể vẫn trống).
- Toàn bộ text tiếng Việt; xử lý lỗi 403/409/404 theo quy ước chung của portal.

## Kết quả mong đợi (Definition of Done)

Sửa plan/`PlanGrade`/`PlanNote`, xuất sheet `data`, đồng bộ đều hoạt động qua UI thật, khớp đúng hành vi nút bấm thủ công đã chốt trong requirements 09 (không có hành động nào tự động chạy ngầm, và sheet `khcn_template`/`KQ_template` trong `[F01]` không bị đụng tới ở bước này — chỉ cập nhật khi sinh PDF ở `ASH-FE-03`).
