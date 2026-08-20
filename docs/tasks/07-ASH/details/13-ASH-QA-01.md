# ASH-QA-01 — Smoke test golden path

Owner: chưa có agent QA riêng — root điều phối, backend (`ASH-BE-05`) và frontend (`ASH-FE-04`) tự chạy phần liên quan. Phụ thuộc: `ASH-BE-05`, `ASH-FE-04`. Trạng thái: xem [`../status.md`](../status.md). Log lịch sử: [`../log.md`](../log.md).

Nguồn: [plan mục 9](../../../plans/07-ASH-assessment-sheet.md#9-test--smoke--phạm-vi-đã-được-người-dùng-giới-hạn).

## Mục đích

Xác nhận toàn bộ luồng nghiệp vụ end-to-end hoạt động thật trên môi trường Development, theo đúng phạm vi kiểm thử người dùng đã giới hạn: **chỉ smoke test theo golden path, không mở rộng UI/responsive/accessibility/performance**.

## Nội dung cụ thể cần làm — 10 bước smoke

1. `Teacher` tạo `AssessmentSheet` cho học sinh bất kỳ (không giới hạn nhóm). Xác nhận `AssessmentSheetSpreadsheetId` vẫn null (chưa copy file `[F01]`).
2. Chọn plan bằng ít nhất một filter (`grade` hoặc `GroupLv1/2/3Name`); xác nhận `PlanGrade` khởi tạo của từng `AssessmentRecord` đúng theo `LatestGrade` hiện có trong `AssessmentRecordLatest`; `FinalGrade` để trống.
3. Bấm "Xuất sang Google Sheet"; xác nhận `AssessmentSheetSpreadsheetId` được set (file `[F01]` mới xuất hiện trên Drive, là bản copy của file mẫu `gen_assessment_sheet`), và sheet `data` (`gid=0`) trong `[F01]` có đúng dữ liệu (cả `Plan*` lẫn `Final*`, dù `Final*` còn trống).
4. Sửa plan (đổi `PlanGrade`/`PlanNote` một vài mục), bấm "Đồng bộ"; xác nhận sheet `data` cập nhật và **không** tạo thêm file `[F01]` khác (vẫn dùng đúng `AssessmentSheetSpreadsheetId` cũ).
5. Sinh PDF `[F02]`; xác nhận sheet `khcn_template` (`gid=1320805599`) **trong đúng file `[F01]` đã có** được điền `PlanGrade`/`PlanNote` mới, link PDF lưu vào `AssessmentSheet.PlanFileLinkPdf` và file mở được.
6. Nhập `FinalGrade`/`FinalNote` cho một số mục (không cần đủ hết) — xác nhận `PlanGrade` không đổi; sinh PDF `[F03]` dù còn thiếu — xác nhận sheet `KQ_template` (`gid=1903920808`) được điền `Final*` và vẫn sinh được PDF.
7. Bấm cập nhật kết quả vào `[F0.ĐG]`; xác nhận `SubmissionDate` được set và đúng ô (dò theo `E16:E`/`H16:16`) trên sheet `ĐG` của `[F0]` có nhãn của **`FinalGrade`** đúng theo bảng mapping (`A`→`Đạt +`, `B`→`Chưa đạt -`, `C`→`Hỗ trợ +`, `D`→`Hỗ trợ -`).
8. Chuyển `Status` sang `Done`; xác nhận bị khoá sửa plan/`PlanGrade`/`PlanNote`/`FinalGrade`/`FinalNote`/feedback; chuyển lại `Open` (bất kỳ vai trò) và xác nhận sửa được tiếp.
9. Gọi `POST /google-sheets/sync-assessments` bằng tài khoản `Teacher`; xác nhận không còn `403` và `Assessment`/`AssessmentSheetLatest`/`AssessmentRecordLatest` được nạp lại mà không đổi `AssessmentRecord` đã snapshot, và không đụng tới file `[F01]` của bảng đó.
10. Xác nhận `Admin`/`SuperAdmin` cũng thấy/sửa được `AssessmentSheet` do `Teacher` tạo ở bước 1 (không giới hạn theo nhóm), bao gồm thấy đúng `AssessmentSheetSpreadsheetId`/link PDF/cả `Plan*`/`Final*`.

Nếu một bước phụ thuộc vào phần "sẽ bổ sung sau" (mapping cột chi tiết trong sheet `data`/`khcn_template`/`KQ_template`, xem requirements 09 mục 15 — vị trí ghi `[F0.ĐG]` đã có, không còn là blocker) mà chưa có, ghi rõ đây là blocker chờ input người dùng thay vì tự suy diễn định dạng cột.

## Kết quả mong đợi (Definition of Done)

Cả 10 bước pass trên môi trường Development; kết quả pass/fail từng bước cùng ngày chạy và evidence được ghi vào [`../log.md`](../log.md) (mục QA / smoke test log) và `.agents/backend/MEMORY.md`/`.agents/frontend/MEMORY.md` tương ứng.
