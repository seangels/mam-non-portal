# ASH-GRP-01 — Chỉnh nhóm snapshot và nhóm Assessment từ records-panel

## Tóm tắt ngắn

1. ✅ Mỗi ô merge `Nhóm lớn`/`Nhóm nhỏ` có một nút icon sửa; không còn toggle chế độ chỉnh chung.
2. ✅ Bấm nút mở một popup nhỏ có textbox nhập tên mới cho ô đang chọn.
3. ✅ Nút `Áp dụng` trong popup **chỉ đổi tên nhóm trên state UI** (các `AssessmentRecord.AssessmentSnapshot` thuộc đúng ô merge) và đánh dấu form dirty; **không gọi API**.
4. ✅ Tên nhóm snapshot chỉ được lưu khi bấm `Lưu thay đổi` của cả bảng đánh giá — gộp `groupLv2Name`/`groupLv3Name` vào `PUT /assessment-sheets/{id}/records`.
5. ✅ Mỗi ô merge có nút icon `Hoàn tác`, chỉ hiện khi ô đó đã khác giá trị lúc tải; bấm là trả các dòng trong ô về tên nhóm ban đầu.
6. ✅ Nút riêng `Cập nhật Assessment gốc` (chỉ Admin/SuperAdmin) gọi ngay `PATCH /api/v1/assessments/group` cập nhật bảng `Assessment` danh mục cho các mã trong ô, đồng thời áp tên lên UI.
7. ✅ Popup có checkbox `Ghi ngược Google Sheet`, nhưng checkbox bị disable và có chú thích `Chưa hỗ trợ`; backend Google write-back chưa thuộc phạm vi task này.
8. ✅ Đã gỡ `PATCH /assessment-sheets/{id}/record-group` và `UpdateRecordGroupAsync`; quyền/trạng thái, contract và UI mới đã triển khai; automated gate bắt buộc đã pass.

## Điều chỉnh 2026-08-28

Yêu cầu mới đảo ngược thiết kế ban đầu: popup không còn gọi `PATCH /record-group` ngay và không còn bị khóa khi form dirty. Ba quyết định đã chốt với người dùng:

1. `Cập nhật Assessment gốc`: tách thành nút riêng, áp dụng ngay qua endpoint catalog mới.
2. Nút reset: chỉ ở từng ô merge (không có nút gom "hoàn tác tất cả").
3. Cách lưu snapshot: gộp `groupLv2Name`/`groupLv3Name` vào `PUT .../records`, gỡ endpoint `record-group` cũ; thêm `PATCH /api/v1/assessments/group` catalog-only.

## Hiện trạng source

- `AssessmentGroup` không phải entity/bảng riêng. API `/assessment-groups` chỉ tổng hợp cây nhóm từ `Assessment.GroupLv1Name/GroupLv2Name/GroupLv3Name` và hiện chỉ có GET.
- `AssessmentRecord.AssessmentSnapshot` lưu `Code`, `Name`, `GroupLv1Name`, `GroupLv2Name`, `GroupLv3Name`, `RowIndex`; không có `AssessmentId` hoặc version.
- Ô merge `groupLv2` đại diện cho dải record cùng `groupLv2Name`; ô merge `groupLv3` đại diện cho dải record cùng cặp `groupLv2Name + groupLv3Name`.
- ✅ Từ `ASH-IMP-02`, `PUT /assessment-sheets/{id}/records` map record cũ theo `Assessment.Code` và giữ `GroupLv2Name`/`GroupLv3Name` của snapshot; tên nhóm tùy chỉnh không bị mất khi lưu grade/note hoặc thêm/xóa record.
- `sync-assessments` hiện nạp lại kho `Assessment` từ Google Sheet; tên nhóm sửa trong portal có thể bị ghi đè ở lần đồng bộ sau.

## Luồng UI đã chốt

1. ✅ Tại ô merge Lv2/Lv3, user bấm nút icon sửa.
2. ✅ Popup hiển thị:
   - textbox `Tên nhóm` được fill bằng tên snapshot hiện tại;
   - dòng hint: `Áp dụng` chỉ đổi trên giao diện, bấm `Lưu thay đổi` để lưu;
   - nút `Cập nhật Assessment gốc` (chỉ Admin/SuperAdmin) + cảnh báo tên gốc có thể bị lần `Đồng bộ GGSheet` sau ghi đè;
   - checkbox `Ghi ngược Google Sheet`, luôn `disabled`, mặc định `false`;
   - nút `Hủy` và `Áp dụng`.
3. ✅ Bấm `Áp dụng`: UI ghi tên mới vào `record.assessment.groupLv2Name`/`groupLv3Name` của các dòng trong ô, dựng lại sort/màu/rowspan, đóng popup. Form trở thành dirty (serialize records có kèm tên nhóm). Không gọi API.
4. ✅ Mỗi ô merge có nút `Hoàn tác` (`icon="undo"`), chỉ hiện khi tên nhóm hiện tại của ô khác baseline lúc tải; bấm là trả các dòng trong ô về tên nhóm ban đầu và dựng lại bảng.
5. ✅ Bấm `Lưu thay đổi` của bảng đánh giá: nếu `recordsDirty()` true, `PUT /assessment-sheets/{id}/records` mang theo `groupLv2Name`/`groupLv3Name` từng record → backend lưu snapshot. Add/xóa record (endpoint replace tức thì) cũng mang theo tên nhóm hiện tại nên không làm mất chỉnh sửa cục bộ.
6. ✅ Bấm `Cập nhật Assessment gốc`: confirm → `PATCH /api/v1/assessments/group` với `level`, `assessmentCodes` (distinct code trong ô), `name`. Thành công thì cũng áp tên lên UI (giống `Áp dụng`) để hiển thị nhất quán, đóng popup. Lỗi hiển thị trong popup.
7. ✅ `Done` khóa mọi nút (`groupEditActionDisabled`); Teacher không thấy nút `Cập nhật Assessment gốc`.

## Contract (sau điều chỉnh 2026-08-28)

### 1. Snapshot group name gộp vào replace-records

`AssessmentSheetRecordRequest` (dùng cho `PUT /assessment-sheets/{id}/records`) có thêm hai field nullable `groupLv2Name`, `groupLv3Name` (tối đa 500).

`AssessmentSheetRules.BuildReplacementRecord` chọn tên nhóm snapshot theo thứ tự:
`NormalizeOptional(requestRecord.GroupLvXName)` → `previousSnapshot?.GroupLvXName` (map theo `Assessment.Code`, giữ tên import khcn) → `assessment.GroupLvXName` (Assessment gốc).

- ✅ UI gửi giá trị `record.assessment.groupLvXName` hiện tại cho mọi record (bỏ trống = `null` = không ghi đè, giữ hành vi cũ).
- ✅ Không cần endpoint riêng; `Done` vẫn bị chặn bởi `EnsureOpen` như các thao tác record khác.

### 2. `PATCH /api/v1/assessments/group` — catalog-only

```json
{ "level": 3, "assessmentCodes": ["A01", "A02"], "name": "Tên mới" }
```

- ✅ Chỉ `Admin|SuperAdmin` (Teacher → `403`).
- ✅ `level` 2 → `Assessment.GroupLv2Name`, 3 → `GroupLv3Name`; đặt tất cả code trong danh sách về `name` (đã trim).
- ✅ Code không tìm đủ → `404 AssessmentNotFound`; code trùng → `409 SnapshotChanged`.
- ✅ Trả `{ updatedCount }`; audit `Assessment.GroupUpdated` (EntityType `Assessment`, kèm `AssessmentCodes`, không ghi secret).
- ✅ Không đụng snapshot của bất kỳ sheet nào. Tên gốc vẫn có thể bị `sync-assessments` sau ghi đè.

### 3. Đã gỡ

- `PATCH /assessment-sheets/{id}/record-group`, `IAssessmentSheetService.UpdateRecordGroupAsync`, `AssessmentSheetService.UpdateRecordGroupAsync`.
- DTO `UpdateAssessmentSheetRecordGroupRequest`; helper `AssessmentSheetRules.EnsureCanUpdateRecordGroup`/`NormalizeRecordGroupName`/`ResolveRecordGroupSelection` (+ helper phụ).
- FE: `AssessmentSheetsService.updateRecordGroup`, `buildUpdateAssessmentSheetRecordGroupRequest`.

### Google Sheet write-back ngoài scope

- ✅ UI vẫn hiển thị checkbox `Ghi ngược Google Sheet` để thể hiện hướng mở rộng sau này.
- ✅ Checkbox luôn disable, không tham gia payload và có hint `Chưa hỗ trợ`.
- ✅ Không thêm endpoint/service backend để ghi group name ngược Google Sheet trong task này.
- ✅ `sync-assessments` giữ hành vi hiện tại; Assessment gốc sửa trong portal có thể bị dữ liệu nguồn ghi đè.

## Điểm cần xác nhận

1. ✅ Mỗi thao tác chỉ cập nhật toàn bộ record thuộc riêng ô merge đang bấm.
2. ✅ Nút `Cập nhật Assessment gốc` chỉ cập nhật các Assessment xuất hiện trong ô merge (theo distinct code); không rename toàn cục theo group/path.
3. ✅ Không còn dropdown chuyển group; popup chỉ có một textbox tên mới.
4. ✅ Google write-back chưa implement backend; checkbox hiển thị nhưng disable.
5. ✅ Quyền/trạng thái đã chốt:
   - đổi tên nhóm snapshot (UI + `PUT .../records`): Teacher/Admin/SuperAdmin, trạng thái `Open` và `Planed`;
   - `Cập nhật Assessment gốc` (`PATCH /assessments/group`): chỉ Admin/SuperAdmin;
   - `Done`: khóa cả hai thao tác.
6. ✅ Chấp nhận Assessment gốc sửa trong portal có thể bị lần `Đồng bộ GGSheet` tiếp theo ghi đè; popup cảnh báo rõ ở nút `Cập nhật Assessment gốc` và trong confirm.

## Definition of Done

- ✅ Nút sửa chỉ mở popup; `Áp dụng` chỉ đổi state UI, không gọi API và không lưu cho tới khi bấm `Lưu thay đổi`.
- ✅ Nút `Hoàn tác` từng ô merge trả các dòng về tên nhóm lúc tải; chỉ hiện khi ô có thay đổi.
- ✅ Mỗi ô merge thao tác đúng tập record/assessment của chính nó; UI không gom các dải trùng tên nằm rời nhau.
- ✅ Snapshot tùy chỉnh không mất khi lưu grade/note hoặc thêm/xóa record (fold group name vào `PUT .../records` + fallback previous snapshot từ `ASH-IMP-02`); có unit test khóa hành vi.
- ✅ `PATCH /assessments/group` chỉ ảnh hưởng bảng Assessment danh mục cho các code trong ô, có authorization + audit.
- ✅ Checkbox Google Sheet hiển thị disabled, không gọi backend.
- ✅ UI tiếng Việt, có loading/error và chặn double submit.
- ✅ Backend có validation, authorization, audit; integration test cập nhật cho luồng mới.
- ⚠️ Integration suite chưa chạy (Docker không sẵn); build project integration pass. Gate tùy chọn theo `api/AGENTS.md`.
- ✅ Frontend `test:ci` 132/132 và development build pass (hash `9a79306b5f2b50191a8b`).
- ✅ Backend unit 95/95 và Release solution build pass 0 warning/error.
- ✅ Không chạy production/IIS vì chưa gọi `$gv-portal-production`.
