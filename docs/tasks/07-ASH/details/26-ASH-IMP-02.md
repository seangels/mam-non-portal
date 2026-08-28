# ASH-IMP-02 — Import khcn: thêm cột STT / groupLv2Name / groupLv3Name

## Tóm tắt ngắn

1. ✅ File mẫu `import_khcn.xlsx` thêm 3 cột **tùy chọn**: `STT`, `groupLv2Name`, `groupLv3Name` (file cũ thiếu vẫn import được).
2. ✅ `STT` trong file reset theo từng nhóm nhỏ; import ghi `AssessmentRecord.DisplayOrder` là số chạy toàn cục `1..N` theo thứ tự dòng để giữ đúng thứ tự file. STT không phải số nguyên chỉ ghi cảnh báo, không chặn import.
3. ✅ `groupLv2Name`/`groupLv3Name` điền kiểu ô merge (chỉ ở dòng đầu mỗi cụm); import **fill-down** giá trị non-empty gần nhất phía trên cho từng cột độc lập, reset theo từng cụm học sinh (`studentCode + startDate + dueDate`).
4. ✅ Giá trị hiệu lực của `groupLv2Name`/`groupLv3Name` ghi vào `AssessmentRecord.AssessmentSnapshot`; ô trống và không có gì để kế thừa thì dùng tên nhóm của `Assessment` khớp mã. `Code`/`Name`/`GroupLv1Name`/`RowIndex` vẫn lấy từ `Assessment`.
5. ✅ Preview trả thêm `stt`, `groupLv2Name`, `groupLv3Name` (giá trị hiệu lực) cho từng dòng; popup `dxDataGrid` thêm 3 cột `STT`/`Nhóm lớn`/`Nhóm nhỏ`.
6. ✅ `PUT /assessment-sheets/{id}/records` giữ nguyên `GroupLv2Name`/`GroupLv3Name` trong snapshot của record đã có (map theo `Assessment.Code`) thay vì dựng lại từ `Assessment` gốc — tên nhóm import (hoặc do người dùng tự sửa) không bị mất khi sắp STT, thêm/xóa mục hay lưu ghi chú. Cùng hiệu lực với yêu cầu "giữ snapshot tùy chỉnh" của [`25-ASH-GRP-01.md`](25-ASH-GRP-01.md).
7. ✅ Form bảng đánh giá hiển thị/nhóm/tô màu/sắp xếp `groupLv2`/`groupLv3` theo snapshot (đã đúng từ trước; bổ sung test khóa hành vi khi tên snapshot khác casing với danh mục).

## Phạm vi

- Backend `api/`: parser Excel, `BuildImportedRecord`, `ReplaceRecordsAsync` + `AssessmentSheetRules.BuildReplacementRecord`, preview DTO.
- Frontend `ui/`: `AssessmentSheetImportExcelPreviewRow`, cột popup preview, test.
- Không migration mới (`AssessmentRecord.DisplayOrder` đã có từ `ASH-STT-01`; snapshot là jsonb).
- Không đổi endpoint/route; response preview là bổ sung field, không phá contract cũ.
- Không production/IIS/deploy; không gọi Google.

## Mapping Excel (bổ sung `24-ASH-IMP-01.md`)

- `STT`: tùy chọn, số nguyên. Không parse được → warning `STT không hợp lệ, bỏ qua giá trị này.`, dòng vẫn import. Import bỏ qua con số này và tự đánh `DisplayOrder = 1..N` theo thứ tự dòng trong file cho mỗi sheet-group.
- `groupLv2Name`, `groupLv3Name`: tùy chọn. Fill-down non-empty gần nhất phía trên, độc lập từng cột, reset khi sang cụm học sinh khác. Ghi vào `AssessmentSnapshot.GroupLv2Name`/`GroupLv3Name`; trống hẳn thì fallback về `Assessment`.

## DoD

- ✅ Backend build 0 warning; unit test pass 94/94 (`AssessmentSheetRulesTests` có case ưu tiên snapshot cũ + fallback Assessment). Đây là gate bắt buộc theo `AGENTS.md`.
- ✅ Đã viết integration test `AssessmentSheetImportExcelMapsSttAndGroupNamesIntoSnapshotAndKeepsThemOnRecordReplace` (fill-down, DisplayOrder chạy số, giữ tên nhóm qua `PUT .../records`); project integration build 0/0.
- ➖ Integration suite chạy thật: **not run (Docker not available)** — theo `AGENTS.md`, integration cần Docker là bước tùy chọn, không chặn done. Chạy lại khi có Docker.
- ⬜ Frontend `test:ci` + build dev: **chưa chạy** — ổ đĩa C: đầy (~16 MB trống, ENOSPC ở temp). Chạy lại khi có dung lượng.
- ✅ `api/README.md`, `api/requests.http`, requirements 09 §14, plan 07-ASH cập nhật.
