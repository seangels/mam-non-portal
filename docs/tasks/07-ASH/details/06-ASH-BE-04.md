# ASH-BE-04 — Sinh PDF [F02]/[F03]

Owner: `backend`. Phụ thuộc: `ASH-BE-03`. Trạng thái: xem [`../status.md`](../status.md). Log lịch sử: [`../log.md`](../log.md).

Nguồn: [plan mục 7](../../../plans/07-ASH-assessment-sheet.md#7-thiết-kế-sinh-pdf-f02f03), [sơ đồ luồng dữ liệu](../../../requirements/09-bang-danh-gia-nang-luc-so-do-du-lieu.md).

## Mục đích

Sinh file PDF `[F02]` (kế hoạch cá nhân) và `[F03]` (kết quả đánh giá) theo nút bấm thủ công, ghi trực tiếp vào 2 sheet đã có sẵn trong file riêng `[F01]` của `AssessmentSheet` (không cần copy/tạo sheet nào nữa — khác thiết kế ban đầu của plan).

## Nội dung cụ thể cần làm

Quy trình giống hệt nhau cho `generate-plan-pdf` (sheet `khcn_template`, `gid=1320805599`, ghi `PlanFileLinkPdf`) và `generate-result-pdf` (sheet `KQ_template`, `gid=1903920808`, ghi `ResultFileLinkPdf`):

1. Gọi `EnsureAssessmentSheetFileAsync` (từ `ASH-BE-03`) để đảm bảo `[F01]` tồn tại (copy file mẫu nếu đây là hành động đầu tiên cần `[F01]` cho `AssessmentSheet` này).
2. **Ghi dữ liệu trực tiếp vào sheet** (`spreadsheets.values.update`): điền `AssessmentSnapshot` + `PlanGrade`/`PlanNote` (cho `khcn_template`) hoặc `AssessmentSnapshot` + `FinalGrade`/`FinalNote` (cho `KQ_template`) hiện tại vào đúng vị trí trên sheet tương ứng — sheet này đã tồn tại sẵn trong `[F01]` từ lúc file được copy, **không cần** `DuplicateSheetRequest`/`AddSheetRequest`. Nếu cần chỉnh merge cell/độ cao dòng theo nội dung, dùng thêm `batchUpdate` (`MergeCellsRequest`, `UpdateDimensionPropertiesRequest`...). Mapping cột/vị trí chi tiết vẫn nằm trong phần "sẽ bổ sung sau" của requirements 09 mục 15. Cho phép sinh PDF ngay cả khi còn `AssessmentRecord` chưa có `PlanGrade`/`FinalGrade` — hiển thị trống.
3. **Export sang PDF** (theo `ASH-DEC-01`): đề xuất mặc định — export chính sheet vừa ghi ở bước 2 sang PDF qua `.../export?format=pdf&gid={gid}` của Sheets/Drive, dùng `fileId = AssessmentSheetSpreadsheetId`, `gid` cố định (`1320805599` cho khcn, `1903920808` cho KQ), xác thực bằng access token service account hiện có (có thể cần thêm scope `drive.readonly`).
4. Tải PDF bytes về, lưu vào vị trí lưu trữ file dùng chung của portal (hoặc cấu hình mới nếu chưa có), set `AssessmentSheet.PlanFileLinkPdf`/`ResultFileLinkPdf`. Chỉ giữ **bản mới nhất** — mỗi lần sinh lại lặp lại toàn bộ chu trình 1–4 (ghi đè dữ liệu trong đúng sheet đó, export lại) và ghi đè link.
5. Nếu bước 3 không khả thi (giới hạn quyền Drive export), phương án dự phòng: dựng PDF trong .NET bằng thư viện phù hợp license (ví dụ QuestPDF Community), đọc trực tiếp từ `AssessmentRecord` — bỏ qua bước 1–2 vì không cần file/sheet trung gian. Chỉ chuyển sang phương án này khi phương án chính thực sự bị chặn, ghi rõ lý do trong `.agents/backend/MEMORY.md`.

## Kết quả mong đợi (Definition of Done)

Bấm nút sinh được cả `[F02]` và `[F03]`; xác nhận sheet `khcn_template`/`KQ_template` **trong đúng file `[F01]` đã tồn tại từ trước** (không phải sheet/file mới) được cập nhật đúng dữ liệu (`Plan*` cho khcn, `Final*` cho KQ) đúng thời điểm bấm nút; link PDF lưu đúng field và file mở được, kể cả khi `[F03]` còn thiếu một số `FinalGrade`. Sinh lại chỉ ghi đè dữ liệu/PDF, không tích luỹ sheet hay file thừa.
