# ASH-FE-03 — Nhập kết quả, sinh PDF, cập nhật [F0.ĐG], chuyển Open/Done

Owner: `frontend`. Phụ thuộc: `ASH-FE-02`, `ASH-BE-04`. Trạng thái: xem [`../status.md`](../status.md). Log lịch sử: [`../log.md`](../log.md).

Nguồn: [plan mục 6.3, 7, 9](../../../plans/07-ASH-assessment-sheet.md#7-thiết-kế-sinh-pdf-f02f03), [requirements 09 mục 4, 9, 10, 11](../../../requirements/09-bang-danh-gia-nang-luc.md), [sơ đồ luồng dữ liệu](../../../requirements/09-bang-danh-gia-nang-luc-so-do-du-lieu.md).

## Mục đích

Phần **giai đoạn kết quả** của màn hình chi tiết: nhập `FinalGrade`/`FinalNote` (độc lập với `PlanGrade`/`PlanNote` đã nhập ở `ASH-FE-02`), sinh 2 file PDF (mỗi lần bấm sẽ ghi đè dữ liệu mới nhất vào sheet `khcn_template`/`KQ_template` sẵn có trong file `[F01]` riêng của bảng này rồi export — xem `ASH-BE-04`), ghi kết quả chính thức vào `[F0.ĐG]`, và chuyển trạng thái `Open`/`Done`.

## Nội dung cụ thể cần làm

- Khu vực nhập kết quả: `FinalGrade` (ban đầu trống, giáo viên nhập kết quả thật) và `FinalNote`/feedback theo từng mục (tùy chọn), cộng `Feedback` tổng cho toàn `AssessmentSheet` (tùy chọn). Nhập ở đây **không đổi** `PlanGrade`/`PlanNote` của cùng mục — hai cặp hoàn toàn độc lập, hiển thị `PlanGrade` (read-only, tham chiếu) cạnh ô nhập `FinalGrade` để giáo viên đối chiếu tiện lợi.
- Nút sinh PDF `[F02]` (gọi `.../generate-plan-pdf`, dùng `PlanGrade`/`PlanNote`) và `[F03]` (gọi `.../generate-result-pdf`, dùng `FinalGrade`/`FinalNote`); hiển thị link `PlanFileLinkPdf`/`ResultFileLinkPdf` sau khi sinh thành công, cho phép mở file. `[F03]` phải sinh được ngay cả khi còn `AssessmentRecord` thiếu `FinalGrade`. Nên hiển thị trạng thái "đang sinh..." rõ ràng vì thao tác này gồm nhiều bước phía backend (đảm bảo file `[F01]` tồn tại, ghi dữ liệu, export) nên có thể mất vài giây.
- Nút "Cập nhật kết quả vào `[F0.ĐG]`" (gọi `.../submit-results`, ghi nhãn `FinalGrade`); sau khi thành công hiển thị `SubmissionDate` mới.
- Toggle chuyển `Status` `Open`↔`Done`: khi `Done`, UI phải khoá cả form `ASH-FE-02` (plan/`PlanGrade`/`PlanNote`) lẫn form này (`FinalGrade`/`FinalNote`/feedback) — disable, không chỉ ẩn thông báo; chuyển lại `Open` cho phép ngay lập tức, không cần nhập lý do, không có màn hình xác nhận đặc biệt — đúng theo quyết định đã chốt.
- Hiển thị rõ 2 link file PDF, `SubmissionDate`, `DoneDate`, `StartDate`, `DueDate` trên cùng màn hình chi tiết.

## Kết quả mong đợi (Definition of Done)

Toàn bộ 4 hành động (nhập `FinalGrade`/`FinalNote`, sinh PDF, ghi `[F0.ĐG]`, chuyển trạng thái) hoạt động qua UI thật; nhập `FinalGrade` không làm đổi `PlanGrade`; `Done` khoá đúng các trường theo yêu cầu (cả Plan lẫn Final), `Open` mở lại được ngay bởi bất kỳ vai trò nào.
