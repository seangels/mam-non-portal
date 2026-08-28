# ASH-GRP-01 — Chỉnh nhóm snapshot và nhóm Assessment từ records-panel

## Tóm tắt ngắn

1. ⬜ Thêm nút bật/tắt chế độ chỉnh nhóm tại `assessment-sheets-form`.
2. ⬜ Khi bật, ô merge `Nhóm lớn`/`Nhóm nhỏ` hiển thị một textbox và hai nút icon:
   - ⬜ Lưu tên vào snapshot của các `AssessmentRecord` thuộc đúng ô merge.
   - ⬜ Mở popup nhỏ để chỉnh nhóm gốc của các `Assessment` liên quan.
3. ⬜ Popup có hai cách thao tác:
   - ⬜ Chuyển sang một nhóm có sẵn bằng dropdown.
   - ⬜ Đổi trực tiếp tên nhóm gốc.
4. ⬜ Hai luồng snapshot và dữ liệu gốc độc lập; sửa dữ liệu gốc không tự ý sửa snapshot lịch sử.
5. ⚠️ Chưa code vì cần chốt phạm vi cập nhật hàng loạt, quyền, trạng thái được sửa và tác động của đồng bộ Google Sheet.

## Hiện trạng source

- `AssessmentGroup` không phải entity/bảng riêng. API `/assessment-groups` chỉ tổng hợp cây nhóm từ `Assessment.GroupLv1Name/GroupLv2Name/GroupLv3Name` và hiện chỉ có GET.
- `AssessmentRecord.AssessmentSnapshot` lưu `Code`, `Name`, `GroupLv1Name`, `GroupLv2Name`, `GroupLv3Name`, `RowIndex`; không có `AssessmentId` hoặc version.
- Ô merge `groupLv2` đại diện cho dải record cùng `groupLv2Name`; ô merge `groupLv3` đại diện cho dải record cùng cặp `groupLv2Name + groupLv3Name`.
- `PUT /assessment-sheets/{id}/records` hiện thay toàn bộ records và dựng lại snapshot từ `Assessment` gốc; nếu không chỉnh logic này, snapshot tùy chỉnh có thể bị mất khi lưu grade/note hoặc thêm/xóa record.
- `sync-assessments` hiện nạp lại kho `Assessment` từ Google Sheet; tên nhóm sửa trong portal có thể bị ghi đè ở lần đồng bộ sau.

## Thiết kế đề xuất

### 1. Lưu snapshot của ô merge

```http
PATCH /api/v1/assessment-sheets/{sheetId}/record-group-snapshots
```

Payload dự kiến:

```json
{
  "level": 2,
  "recordIds": ["record-id"],
  "expectedGroupLv2Name": "Tên cũ",
  "expectedGroupLv3Name": null,
  "name": "Tên mới"
}
```

- ⬜ UI gửi đúng danh sách `recordIds` thuộc riêng ô merge đang thao tác.
- ⬜ Backend xác minh các record thuộc sheet và cùng nhóm snapshot dự kiến trước khi cập nhật.
- ⬜ Chỉ đổi `AssessmentSnapshot.GroupLv2Name` hoặc `GroupLv3Name`; không đổi `Assessment` gốc.
- ⬜ Trả lại `AssessmentSheetDetail`, UI dựng lại sort/rowspan.
- ⬜ Audit hành động và số record bị ảnh hưởng.
- ⬜ Điều chỉnh luồng replace records để giữ snapshot override của record hiện hữu.

### 2. Chuyển nhóm gốc bằng dropdown

- ⬜ Popup chọn theo full path nhóm, không định danh chỉ bằng tên vì tên có thể trùng giữa các parent.
- ⬜ Request gửi danh sách Assessment xác định từ các record trong ô merge; ưu tiên bổ sung định danh ổn định thay vì bulk update theo tên hiển thị.
- ⬜ Với Lv3, dropdown phải giữ được quan hệ với Lv2 cha.
- ⬜ Có confirm nêu rõ số Assessment bị ảnh hưởng và audit thay đổi.

### 3. Đổi trực tiếp tên nhóm gốc

```http
PUT /api/v1/assessment-groups/rename
```

- ⬜ Match bằng full path `GroupLv1 + GroupLv2 + GroupLv3` tùy level.
- ⬜ Đổi tên toàn bộ Assessment thuộc đúng path sau khi người dùng xác nhận.
- ⬜ Không tự động đổi snapshot các sheet đã tồn tại.
- ⬜ Audit old path, new path, actor và số Assessment bị ảnh hưởng.

## Điểm cần xác nhận

1. ❓ Nút lưu snapshot có cập nhật toàn bộ record thuộc riêng ô merge đang bấm không?
2. ❓ Popup sửa nhóm gốc áp dụng cho các Assessment xuất hiện trong ô merge hay toàn bộ Assessment thuộc group/path cũ?
3. ❓ Đổi tên trực tiếp có phải là rename toàn cục cho mọi Assessment thuộc đúng full path không?
4. ❓ Khi đổi Lv2, giữ Lv3 hiện tại hay bắt buộc chọn Lv3 thuộc Lv2 mới? Khi đổi Lv3 sang nhánh khác có tự đổi Lv2 cha không?
5. ❓ Quyền/trạng thái đề xuất có được chấp nhận không:
   - sửa snapshot: Teacher/Admin/SuperAdmin, trạng thái `Open` và `Planed`;
   - sửa group gốc: chỉ Admin/SuperAdmin;
   - `Done`: khóa cả hai thao tác.
6. ❓ Có chấp nhận group gốc sửa trong portal bị lần `Đồng bộ GGSheet` tiếp theo ghi đè không, hay phải bổ sung cơ chế ghi ngược/override?

## Definition of Done dự kiến

- ⬜ Toggle không làm thay đổi dữ liệu cho tới khi bấm nút lưu/xác nhận popup.
- ⬜ Mỗi ô merge thao tác đúng tập record/assessment của chính nó, kể cả tên group trùng ở nhánh khác.
- ⬜ Snapshot tùy chỉnh không mất khi lưu grade/note hoặc thêm/xóa record.
- ⬜ Sửa master group không tự sửa snapshot lịch sử.
- ⬜ UI tiếng Việt, có loading/confirm/error và chặn double submit.
- ⬜ Backend có validation, authorization, audit và kiểm thử PostgreSQL integration tương ứng.
- ⬜ Frontend có unit test cho grouping/recordIds/state popup và development build/test pass.
- ⬜ Không chạy production/IIS nếu chưa gọi `$gv-portal-production`.
