# ASH-FE-01 — Danh sách + tạo AssessmentSheet, chọn plan có filter

Owner: `frontend`. Phụ thuộc: `ASH-FE-00`, `ASH-BE-02`. Trạng thái: xem [`../status.md`](../status.md). Log lịch sử: [`../log.md`](../log.md).

Nguồn: [plan mục 3.1, 5](../../../plans/07-ASH-assessment-sheet.md#3-phạm-vi-và-ngoài-phạm-vi), [requirements 09 mục 5](../../../requirements/09-bang-danh-gia-nang-luc.md#5-tạo-assessmentsheet-và-chọn-plan).

## Mục đích

Dựng màn hình đầu tiên giáo viên chạm vào: danh sách `AssessmentSheet` và luồng tạo mới, gồm bước chọn plan có filter — không dùng compact-card layout của `AUI`, chỉ dùng layout form/grid tiêu chuẩn hiện có của portal.

## Nội dung cụ thể cần làm

- Trang danh sách `AssessmentSheet` (`ui/src/app/pages/assessment-sheets/`): pagination, filter theo trạng thái `Open`/`Done`, hiển thị học sinh/đợt/trạng thái/`SubmissionDate`.
- Form tạo mới: chọn học sinh bất kỳ (không giới hạn nhóm), đặt `Name` cho đợt đánh giá.
- Bước chọn plan: hiển thị danh sách `Assessment` với 3 filter kết hợp được — theo học sinh (mặc định, tính `LatestGrade` gợi ý đọc từ `AssessmentRecordLatest` — bảng chỉ-đọc, chỉ dùng để prefill), theo ngưỡng `LatestGrade` (`A > B > C > D`), theo `GroupLv1/2/3Name`.
- Sau khi chọn plan và submit, gọi `POST /assessment-sheets`, hiển thị `PlanGrade` khởi tạo của từng `AssessmentRecord` (đã được server điền từ `LatestGrade`) đúng như server trả về; `FinalGrade` để trống (chưa nhập ở bước này).
- Route/menu sidebar cho `Teacher`/`Admin`/`SuperAdmin`.

## Kết quả mong đợi (Definition of Done)

Giáo viên tạo được `AssessmentSheet` mới cho học sinh bất kỳ, chọn plan bằng ít nhất một filter, và thấy đúng `PlanGrade` khởi tạo (= `LatestGrade` tại thời điểm tạo) theo dữ liệu server trả về. Build development pass.
