# Kế hoạch phân nhóm và cấu hình lịch học cho học sinh

## 1. Thông tin kế hoạch

- **Epic:** `SCH`
- **Thứ tự:** `04`
- **Trạng thái:** Bản đề xuất đang review, chưa triển khai; chờ khóa `SCH-DEC-01`–`SCH-DEC-08`.
- **Ngày lập:** 2026-08-11.
- **Phạm vi:** .NET 10 REST API, PostgreSQL 17 và Angular 15/DevExtreme.
- **Contract nền:** [`01-BASE-admin-portal.md`](01-BASE-admin-portal.md), [`02-ATT-attendance.md`](02-ATT-attendance.md) và [`03-TCH-teacher-management.md`](03-TCH-teacher-management.md).

Mỗi đợt phát triển dùng mã `SCH-00` đến `SCH-05`. Task backend, frontend và kiểm thử dùng hậu tố `SCH-BE-*`, `SCH-FE-*`, `SCH-QA-*`.

Production build, đóng gói và deploy IIS không nằm trong luồng mặc định của epic này. Chỉ thực hiện khi người dùng gọi riêng `$gv-portal-production`.

## 2. Mục tiêu

1. Tại trang `Học sinh`, Admin/SuperAdmin có thể xem nhóm hiện tại và thực hiện phân nhóm, chuyển nhóm hoặc gỡ khỏi nhóm.
2. Mỗi học sinh có một lịch học hằng tuần hiện tại:
   - Hình thức `Học 1-1` hoặc `Học cả ngày`.
   - Chọn một hoặc nhiều ngày từ Thứ Hai đến Thứ Bảy.
3. Danh sách điểm danh theo ngày có thể dùng lịch hiện tại để xác định roster và trạng thái mặc định.
4. Giữ nguyên full daily snapshot: phiếu đã lưu không bị rewrite khi lịch hoặc nhóm hiện tại thay đổi.
5. Giữ code nhỏ, rõ ràng; không tạo thêm một cơ chế phân nhóm song song với endpoint hiện có.

## 3. Ngoài phạm vi v1

- Không có Chủ nhật.
- Không cấu hình giờ bắt đầu/kết thúc, ca sáng/chiều hoặc nhiều khung giờ trong một ngày.
- Không có lịch theo tuần chẵn/lẻ, ngày lễ, nghỉ bù hoặc ngoại lệ theo ngày.
- Không có `effective_from`/`effective_to` hoặc lịch sử phiên bản schedule.
- Không gán riêng giáo viên 1-1 ở cấp Student; quyền điểm danh vẫn đi qua giáo viên phụ trách group.
- Không thay đổi giới hạn tối đa 100 học sinh active trong một group.
- Không tạo StudentGroup thứ hai cho cùng một Student.

## 4. Hiện trạng cần kế thừa

- `Student` đã có `groupId/groupCode/groupName` trong response.
- API đã có nguồn mutation duy nhất cho nhóm:

```http
PUT /api/v1/students/{studentId}/group
```

- Trang `Nhóm` đã hỗ trợ thêm/chuyển/gỡ học sinh nhưng trang `Học sinh` chưa có action tương ứng.
- Student create/full PUT hiện không nhận `groupId`; cần tiếp tục giữ phân nhóm là command riêng để audit, capacity, lock và `snapshotVersion` nhất quán.
- Attendance Missing hiện lấy toàn bộ Student active trong group; trạng thái mặc định là `Present`.
- Attendance Saved đã lưu đầy đủ record, kể cả `Present`, nên là lịch sử authoritative và không phụ thuộc Student hiện tại.
- `OneToOneHour` hiện là trạng thái điểm danh thực tế 60 phút; `StudyMode.OneToOne` mới là cấu hình lịch mặc định, hai khái niệm không được dùng thay nhau trong DTO/domain.

## 5. Luồng nghiệp vụ đề xuất

### 5.1. Phân nhóm tại trang Học sinh

- Grid hiển thị cột `Nhóm hiện tại`.
- Mỗi row có action theo trạng thái:
  - `Phân nhóm` nếu chưa có group.
  - `Chuyển nhóm` nếu đã có group.
  - `Gỡ khỏi nhóm` trong cùng popup/action.
- Popup chỉ liệt kê group active; server vẫn kiểm tra lại group tồn tại, active và chưa vượt 100 Student active.
- Chuyển/gỡ group phải xác nhận rõ nhóm cũ → nhóm mới và có hiệu lực ngay.
- Student inactive không được phân vào group.
- Nếu Student đã có attendance record hôm nay, giữ rule hiện tại: không move/unassign trong ngày đó.
- Trang `Nhóm` vẫn giữ workflow roster-centric. Hai trang phải gọi cùng endpoint, không tạo contract thứ hai.
- Không đưa `groupId` vào Student create/full PUT. Sau khi tạo Student, người dùng dùng action `Phân nhóm`; cách này giữ mutation boundary hiện tại và tránh create+assign partial transaction ở UI.

### 5.2. Lịch học hằng tuần

- Mỗi Student có đúng một schedule hiện tại.
- `StudyMode`:
  - `FullDay`: Học cả ngày.
  - `OneToOne`: Học 1-1.
- `StudyWeekday` chỉ gồm:
  - `Monday`, `Tuesday`, `Wednesday`, `Thursday`, `Friday`, `Saturday`.
- Schedule bắt buộc có từ 1 đến 6 ngày, không trùng và luôn trả về theo thứ tự Thứ Hai → Thứ Bảy.
- Student inactive vẫn giữ schedule để không mất thông tin và Admin/SuperAdmin vẫn được chỉnh; chỉ Student active mới được phân nhóm/đưa vào roster.
- Thay đổi schedule có hiệu lực ngay cho phiếu `Missing` từ thời điểm lưu; phiếu `Saved` giữ nguyên snapshot cũ.

### 5.3. Ảnh hưởng tới điểm danh — phương án đề xuất

V1 đề xuất schedule **điều khiển roster hiện tại**:

- Roster cho một ngày = Student active, đang thuộc group và có weekday tương ứng trong schedule.
- `FullDay` có trạng thái mặc định `Present`.
- `OneToOne` có trạng thái mặc định `OneToOneHour`.
- Study mode chỉ quyết định default, không khóa các status mà giáo viên có thể chọn. Điều này giữ khả năng sửa tình huống thực tế và không tạo thêm trạng thái vắng riêng trong v1.
- Context count, Missing preview và POST roster validation phải dùng cùng một predicate schedule; không được mỗi nơi tính khác nhau.
- Context count giữ invariant full snapshot: nếu đã có Saved sheet thì dùng số persisted records; chỉ Missing mới đếm current active scheduled roster.
- Group/ngày không có Student có lịch: Daily GET trả `200`, roster rỗng, `canCreate=false`, `readOnlyReason=NoScheduledStudents`; không tạo empty sheet.
- Standard POST sau khi lock group và đọc scheduled roster trả `409 NoScheduledStudents` nếu roster thực sự rỗng. Nếu roster không rỗng nhưng request empty/sai tập Student thì trả `409 AttendanceRosterMismatch`. Historical recovery vẫn bắt buộc ít nhất một Student.
- Standard POST kiểm tra `expectedSnapshotVersion` ngay sau khi lock group, trước empty-roster rule; request stale luôn ưu tiên `409 SnapshotChanged`, kể cả roster mới đã rỗng.
- Chủ nhật luôn không có scheduled roster trong v1.
- Schedule đổi khi Student đang thuộc group phải tăng `group.snapshotVersion` đúng một lần trong cùng transaction.
- Request POST điểm danh dựa trên snapshot cũ sẽ nhận `409 SnapshotChanged`.
- Phiếu Saved không thêm/bớt record và không đổi status khi schedule hiện tại thay đổi.
- Ngày quá khứ chưa có phiếu sau khi schedule đổi tiếp tục dùng quy tắc `HistoricalSnapshotUnavailable`/historical recovery của plan ATT.
- Thứ tự ưu tiên read-only reason: date/window policy → group inactive/thiếu responsible Teacher → historical snapshot/recovery → scheduled roster rỗng. Không dùng current schedule để kết luận `NoScheduledStudents` khi snapshot ngày quá khứ đã không còn chứng minh được.
- Historical recovery dùng danh sách Student/status được Admin/SuperAdmin chọn rõ ràng và không filter lại theo current schedule.
- Historical recovery giữ manual roster và default `Present`; không suy diễn mode/default từ current schedule của Student hiện tại hoặc đã xóa.

Nếu `SCH-DEC-01` chọn schedule chỉ là metadata, toàn bộ các thay đổi attendance ở mục này bị loại; schedule update không tăng group snapshot và Attendance regression phải chứng minh roster không đổi.

## 6. Thiết kế dữ liệu đề xuất

### 6.1. Enum domain

```text
StudyMode = OneToOne | FullDay

StudyWeekday =
  Monday | Tuesday | Wednesday |
  Thursday | Friday | Saturday
```

JSON serialize enum dạng string; UI chỉ hiển thị nhãn tiếng Việt.

### 6.2. Mở rộng bảng `students`

```text
study_mode          varchar(20) NOT NULL
study_weekday_mask  smallint NOT NULL
version             integer NOT NULL DEFAULT 1
```

Bit mask nội bộ:

| Ngày | Bit | Giá trị |
|---|---:|---:|
| Monday | 0 | 1 |
| Tuesday | 1 | 2 |
| Wednesday | 2 | 4 |
| Thursday | 3 | 8 |
| Friday | 4 | 16 |
| Saturday | 5 | 32 |

Constraints:

```text
study_mode IN ('OneToOne', 'FullDay')
study_weekday_mask BETWEEN 1 AND 63
version >= 1
```

- Bit mask không được expose qua API; Application dùng một helper encode/decode duy nhất, có unit test.
- `version` là EF Core concurrency token (`IsConcurrencyToken`) và database check `version >= 1`.
- Không tạo bảng temporal hoặc PostgreSQL extension.
- Chưa thêm index schedule ở v1 vì dữ liệu đã được thu hẹp bằng index group/status. Chỉ thêm index sau khi `EXPLAIN ANALYZE` chứng minh cần.

### 6.3. Attendance snapshot

Phương án mặc định v1 không thêm `study_mode_snapshot` vào `attendance_records`:

- Record Saved đã lưu status thực tế; schedule chỉ dùng để chọn roster và default ở lần tạo.
- Đổi schedule không rewrite sheet/record.
- Hạn chế được ghi rõ: báo cáo lịch sử sẽ biết status thực tế nhưng không luôn suy ra được study mode tại thời điểm một record vắng được tạo.

Nếu nghiệp vụ cần báo cáo mode lịch sử, `SCH-DEC-04` phải chọn thêm `study_mode_snapshot`, mở rộng recovery DTO và backfill record cũ trước implementation.

## 7. REST contract đề xuất

### 7.1. Student response

```text
StudentResponse {
  ...existingFields,
  groupId,
  groupCode,
  groupName,
  studySchedule: {
    mode: OneToOne | FullDay,
    weekdays: StudyWeekday[]
  },
  version: integer
}
```

- `weekdays` luôn unique và canonical Monday → Saturday.
- Không trả `studyWeekdayMask`.

### 7.2. Create Student

```http
POST /api/v1/students
```

```json
{
  "studentCode": "HS-001",
  "fullName": "Nguyễn Văn A",
  "nickName": "Bé A",
  "dateOfBirth": "2021-05-10",
  "gender": "Male",
  "status": "Active",
  "guardianName": null,
  "guardianPhone": null,
  "note": null,
  "studySchedule": {
    "mode": "FullDay",
    "weekdays": ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"]
  }
}
```

- `groupId` không nằm trong request.
- Success trả `201`, Student `version=1`.

### 7.3. Full update Student

```http
PUT /api/v1/students/{studentId}
```

Request là full replacement tất cả field editable, gồm `studySchedule` và:

```json
{
  "expectedVersion": 3
}
```

- Success tăng Student version đúng một, kể cả payload no-op để contract deterministic.
- Stale trả `409 StudentVersionConflict` kèm `currentVersion`; không ghi partial data/audit success.
- Nếu identity hoặc schedule thay đổi khi Student đang thuộc group, tăng group snapshot đúng một lần.

### 7.4. Assign/move/unassign group

Giữ endpoint hiện tại và mở rộng concurrency:

```http
PUT /api/v1/students/{studentId}/group
```

```json
{
  "groupId": "uuid-or-null",
  "expectedVersion": 3
}
```

- Success trả full `StudentResponse`, tăng Student version đúng một khi assignment thực sự đổi.
- Service lock/load Student và so `expectedVersion` trước mọi business check hoặc nhánh no-op. Stale request gửi cùng group hiện tại vẫn trả conflict.
- Nếu gửi cùng group hiện tại với version đúng, trả response hiện tại và không tăng version/snapshot.
- Stale trả `409 StudentVersionConflict`.
- Group page và Student page đều phải refresh response/version sau success.

### 7.5. Delete Student

```http
DELETE /api/v1/students/{studentId}?expectedVersion=3
```

- Vẫn chặn delete khi còn group.
- Success soft-delete, tăng version trên Student row được giữ lại trong transaction và trả `204`.

### 7.6. List query

Giữ filter hiện tại và bổ sung:

```text
groupId={uuid}
unassigned=true
studyMode=OneToOne|FullDay
studyWeekday=Monday|Tuesday|Wednesday|Thursday|Friday|Saturday
sortBy=...|studyMode
```

- `groupId + unassigned=true` tiếp tục trả `400 ValidationFailed`.
- Filter schedule chạy tại PostgreSQL bằng mode/bit mask trước count/paging.
- Không sort theo tập weekday trong v1.
- Projection list/detail phải translate schedule/group trực tiếp sang SQL; không dùng `.Select(x => Map(x))` đọc navigation rồi client-evaluate.

## 8. Validation và ProblemDetails

### 8.1. Validation schedule

- `studySchedule` bắt buộc trên create/full PUT.
- Mode chỉ nhận `OneToOne|FullDay`.
- Weekdays có 1–6 phần tử.
- Không nhận `Sunday`, giá trị lạ hoặc duplicate.
- Server canonicalize thứ tự; không tin thứ tự client.
- Request thiếu `expectedVersion` hoặc version <= 0 trả `400 ValidationFailed`.
- Lỗi required/enum/empty/duplicate/Sunday trả `400 ValidationFailed` với field path `studySchedule.mode` hoặc `studySchedule.weekdays`; UI dùng path nested để focus mode/checkbox group.

### 8.2. Problem codes mới

```text
StudentVersionConflict
NoScheduledStudents
StudentNotFound
```

- `NoScheduledStudents` đồng thời là `AttendanceReadOnlyReason` cho Daily GET và stable `ProblemDetails.code` cho standard POST.
- `NoScheduledStudents` ở GET là read-only state trong response 200, không phải `ApiError`; chỉ direct/race POST mới nhận ProblemDetails code cùng tên.
- Để POST roster rỗng đi tới business rule này, standard create DTO không dùng attribute `[MinLength(1)]`; service phân biệt roster rỗng với roster mismatch sau khi lock group. Historical recovery DTO vẫn giữ `[MinLength(1)]`.

Giữ các code hiện tại:

```text
GroupCapacityExceeded
StudentAlreadyRecordedToday
StudentInactive
GroupInactive
StudentHasCurrentGroup
SnapshotChanged
HistoricalSnapshotUnavailable
```

UI map toàn bộ code sang tiếng Việt và không hiển thị raw code/title/detail tiếng Anh.

## 9. Authorization, lock và audit

- Chỉ Admin/SuperAdmin được CRUD Student, chỉnh schedule và phân nhóm.
- Teacher chỉ thấy Student qua roster/attendance đã được scope; không có endpoint tự sửa schedule.
- Lock order:

```text
Student -> group cũ/mới theo UUID tăng dần
```

- Full PUT, assign group và delete dùng cùng Student optimistic `version`.
- Concurrent profile/schedule update và move: chỉ một request với cùng expected version thắng.
- Full PUT so version trước business validation và luôn tăng version một kể cả payload no-op. Group snapshot chỉ tăng khi `studentCode`, `fullName`, `nickName` hoặc schedule thực sự đổi; date of birth, gender, guardian và note không làm đổi attendance snapshot.
- Đổi riêng `StudyMode` của Student trong group cũng tăng group snapshot vì default attendance thay đổi. Một request đổi nhiều identity/schedule fields chỉ tăng group snapshot đúng một.
- Attendance sheet create tiếp tục lock group trước khi đọc scheduled roster/source snapshot.
- Business weekday luôn lấy từ `Asia/Ho_Chi_Minh`/server business date kế thừa ATT, không lấy UTC hoặc clock của browser.
- Audit actions giữ `Student.Created`, `Student.Updated`, `Student.Deleted`, `Student.GroupAssigned/Moved/Removed`.
- Audit thêm `changedFields`, mode/day-mask và version before/after; đồng thời refactor Student audit hiện tại để không ghi raw fullName, nickName, guardian info, note hoặc toàn bộ request body.

## 10. Thiết kế UI tiếng Việt

### 10.1. Student list

- Filter thêm:
  - `Nhóm` dùng remote active group picker.
  - `Chưa phân nhóm`, loại trừ với group picker.
  - `Hình thức học`.
  - `Ngày học` nếu không làm panel quá chật; có thể đưa vào hàng filter mở rộng.
- Đổi filter reset page về 1 và dùng server `totalItems`.
- Cột thêm:
  - `Nhóm hiện tại`: `MÃ · Tên`, fallback `Chưa phân nhóm`.
  - `Lịch học`: `Học cả ngày · T2, T3, T5` hoặc `Học 1-1 · T2, T4`.
- Row action thêm `Phân nhóm/Chuyển nhóm`; gỡ nhóm nằm trong popup.
- Cột lịch map remote sort selector rõ thành `studyMode`; cột group không cho sort vì backend không whitelist group sort. Filter `Ngày học` là single-select trong v1.
- Grid responsive ưu tiên mã, tên, nhóm, lịch và action; field phụ chuyển vào adaptive detail trên màn nhỏ.

### 10.2. Popup phân nhóm

- Hiển thị học sinh và nhóm hiện tại.
- Picker chỉ group active, cho phép clear thành `Chưa phân nhóm`.
- Picker dùng remote paging/search/server total, hiển thị `MÃ · Tên · studentCount/100`; disable group đủ 100 và loại current group khỏi lựa chọn move, nhưng server vẫn là authority.
- Confirm rõ move/unassign; disable double submit.
- Sau success dùng Student response mới để cập nhật version rồi refresh grid.
- Cả Student page và Group roster phải đổi assignment client từ `void` sang full `StudentResponse` và giữ version mới trả về.
- `StudentsService.assignGroup` là client canonical cho cả hai page; không giữ hai service method khác nhau cùng gọi một endpoint.
- Copy lỗi cụ thể cho group đầy, Student inactive, group inactive, đã điểm danh hôm nay và version conflict.

### 10.3. Form create/edit Student

- Thêm section full-width `Lịch học hằng tuần`.
- Radio/segmented control:
  - `Học cả ngày`.
  - `Học 1-1`.
- Sáu checkbox có nhãn đầy đủ `Thứ Hai` đến `Thứ Bảy`.
- Ít nhất một ngày bắt buộc; focus checkbox group khi validation fail.
- Desktop giữ form 2 cột, section lịch full-width; mobile 1 cột.
- Dùng `fieldset/legend` hoặc ARIA group `Ngày học trong tuần`, keyboard toggle và touch target tối thiểu khoảng 44 px.
- Edit giữ dirty guard nếu chuyển Student form sang page; nếu vẫn là popup, chặn đóng/outside click khi đang lưu và cảnh báo khi có draft chưa lưu.
- Student đang thuộc group vẫn bị chặn chuyển Inactive/delete; UI giữ draft và hướng dẫn `Gỡ khỏi nhóm` trước. Student inactive, unassigned thì ẩn/disable `Phân nhóm`; dữ liệu legacy inactive còn group vẫn phải cho gỡ.

### 10.4. Attendance UI

- Missing cards nhận default từ backend; UI không tự suy luận lại mode/day.
- Thay banner cũ khẳng định mọi học sinh mặc định `Có mặt` bằng copy trung tính: `Trạng thái được gợi ý theo lịch học của từng học sinh.`
- Khi group/ngày không có học sinh theo lịch, hiển thị `Không có học sinh có lịch học trong ngày này`; không hiện nút lưu phiếu.
- Context group count hiển thị `N học sinh có lịch` cho ngày đang chọn; không dùng count này để đánh giá capacity.
- Saved sheet tiếp tục render snapshot records, không re-filter theo schedule hiện tại.

## 11. Migration và compatibility

Migration nâng cấp từ `AddTeacherManagement`:

1. Thêm tạm `study_mode`, `study_weekday_mask` nullable hoặc có default chỉ phục vụ migration; thêm `version` default 1.
2. Backfill mọi Student, kể cả soft-deleted:
   - `study_mode = FullDay`.
   - `study_weekday_mask = 63` tương ứng Thứ Hai–Thứ Bảy.
   - `version = 1`.
3. Đặt NOT NULL và check constraints; bỏ default tạm của `study_mode`/`study_weekday_mask` để raw SQL insert không âm thầm tạo lịch chưa được chọn. Chỉ giữ default `version=1`.
4. Nếu schedule điều khiển attendance, tăng mỗi group current/non-deleted có Student active đúng một lần, không tăng theo số Student: `snapshot_version += 1`, đồng thời cập nhật `snapshot_changed_at` và `updated_at` bằng migration time.

Không thay đổi Saved sheet/record. Không seed Student/group/schedule mới. Không cài PostgreSQL extension.

- Capacity 100 tiếp tục tính toàn bộ Student active đang được assign, không tính riêng theo weekday.
- `StudentGroup.studentCount` luôn là tổng active membership để kiểm cap; chỉ `AttendanceContextGroup.studentCount` là scheduled count theo ngày.
- Hệ quả có chủ đích: past Missing có thể chuyển sang recovery sau migration; Saved sheets không đổi.

Contract thay đổi có chủ đích:

- Student response thêm schedule/version.
- Create/full PUT bắt buộc schedule.
- Full PUT, group command và DELETE dùng `expectedVersion`.
- UI `students` và `student-groups` phải được nâng cùng backend để không gửi request cũ thiếu version.
- Frontend phải khai báo explicit Create/Update interfaces; không dùng `Omit<Student,...>` vì dễ vô tình gửi `groupId`, `version` hoặc response-only fields.

## 12. Test matrix

### 12.1. Unit/domain

- Encode/decode weekday mask và canonical order.
- Monday/Saturday boundaries; reject empty, duplicate, Sunday và enum lạ.
- Default status: FullDay → Present, OneToOne → OneToOneHour.
- StudyMode/day filter predicate theo business date.
- Student version increment/no-op group behavior.
- Validation attribute cho nested positional DTO dùng đúng target `[param: ...]`; service vẫn kiểm tra duplicate weekday/canonical order.

### 12.2. PostgreSQL integration

- Fresh migration và upgrade từ TCH có Student/group/sheet cũ.
- Backfill FullDay + Monday–Saturday + version 1, kể cả soft-deleted Student.
- Create/list/get/full PUT trả schedule đúng và không lộ mask.
- Filter mode/weekday/group/unassigned, paging/total/sort ổn định.
- Stale full PUT/group/delete trả `StudentVersionConflict` và rollback.
- Concurrent update-vs-move chỉ một request thắng.
- GET Missing đồng thời schedule PUT và POST first-save: sheet phải dùng trọn schedule/version cũ hoặc POST thấy version mới; không được lưu roster/default mới với `sourceSnapshotVersion` cũ.
- Assign/move/unassign: active/group active/cap 100/same-day attendance/snapshot invariants.
- Schedule change của grouped Student tăng group snapshot đúng một; unassigned Student không tăng group.
- Monday/Saturday include đúng roster; ngày không chọn và Sunday exclude.
- Mixed FullDay/OneToOne có default status đúng.
- Context count, Missing GET và POST validation dùng cùng scheduled roster.
- Context Saved luôn dùng persisted record count, không bị current schedule làm thay đổi.
- Daily GET roster rỗng trả reason; standard POST roster rỗng trả `NoScheduledStudents`, còn request sai roster trả `AttendanceRosterMismatch`.
- Saved sheet bất biến sau đổi schedule/mode/group.
- Full PUT no-op tăng Student version một nhưng không tăng group snapshot; same-group command không tăng cả hai.
- Mode-only change của grouped Student tăng group snapshot đúng một.
- Raw SQL bị DB constraint chặn mask `0`, `64`, mode lạ và version `0`; migration bump mỗi group đúng một dù group có nhiều Student.
- Filter weekday/mode có `totalItems` chính xác trước paging và stable `id` tie-break.
- Anonymous nhận `401`, Teacher nhận `403`; client cũ thiếu expectedVersion nhận `400 ValidationFailed`.
- Audit không chứa raw fullName, nickName, guardian hoặc note.
- Sunday tính theo business timezone `Asia/Ho_Chi_Minh`, không theo UTC/browser.
- Historical recovery regression.
- EF `has-pending-model-changes` sạch.

### 12.3. Frontend

- Enum ↔ checkbox round-trip và canonical order.
- Create/update body có schedule/version và không có groupId.
- Group/unassigned mutual exclusion, filter reset page, server total.
- Hiển thị group/schedule summary bằng tiếng Việt.
- Assign/move/unassign dùng đúng endpoint/version, confirm và refresh response.
- Conflict 409 giữ draft/popup và có CTA tải bản mới.
- Validation ít nhất một ngày, double-submit guard, loading/error/empty state.
- Responsive/adaptive, keyboard, ARIA và touch target.
- Regression trang `Nhóm` dùng request mới có version.
- Attendance Missing/empty/Saved không tự lọc sai phía client.
- Banner Missing không khẳng định mọi card mặc định Present; UI giữ nguyên status/default backend trả.
- `NoScheduledStudents` GET 200 dùng read-only label/empty state và ẩn CTA; POST code được map riêng.
- Recovery manual roster không filter current schedule và giữ default Present.
- Nested field errors focus đúng mode/weekday group; không có checkbox Sunday.
- Remote group picker paging/search/byKey/full-state và inactive/grouped guidance.
- Composite grid columns không gửi sort key ngoài whitelist.

## 13. Phân chia đợt phát triển

### `SCH-00` — Khóa quyết định và contract

- `SCH-BE-00`: enum/schema/DTO/ProblemDetails/OpenAPI contract.
- `SCH-FE-00`: wireflow, models, copy tiếng Việt và permission matrix.
- `SCH-QA-00`: traceability quyết định → API → UI → test.

### `SCH-01` — Schema, migration và Student concurrency

- `SCH-BE-01`: columns/check/backfill/version/migration upgrade proof.
- `SCH-BE-02`: Student create/full PUT/list filters/version/audit.
- `SCH-FE-01`: schedule section trong create/edit và schedule summary/filter.
- `SCH-QA-01`: migration/CRUD/filter/concurrency.

### `SCH-02` — Phân nhóm tại trang Học sinh

- `SCH-BE-03`: expectedVersion cho group command/delete và regression invariants.
- `SCH-FE-02`: group filter/column/popup/action tại Student page.
- `SCH-FE-03`: nâng trang Group dùng cùng response/version.
- `SCH-QA-02`: assign/move/unassign/cap/inactive/today/race.

### `SCH-03` — Tích hợp attendance theo lịch

- `SCH-BE-04`: scheduled roster/context/Missing/POST/default/empty reason.
- `SCH-FE-04`: attendance empty/default UX, không re-filter Saved.
- `SCH-QA-03`: weekday/timezone/snapshot/recovery/Saved regression.

### `SCH-04` — Hardening UI và vận hành

- `SCH-BE-05`: audit privacy, query plan, OpenAPI/README/requests.
- `SCH-FE-05`: responsive, accessibility, Vietnamese errors, dirty/conflict UX.
- `SCH-QA-04`: auth/privacy/performance/network/mobile.

### `SCH-05` — Final regression và bàn giao

- Full backend build/unit/PostgreSQL integration/EF pending-model.
- Frontend development build/ChromeHeadlessCI.
- Cập nhật docs, tasks và agent memory.
- Production/IIS chỉ chạy khi gọi skill riêng.

## 14. Các quyết định cần khóa

| Mã | Quyết định | Đề xuất |
|---|---|---|
| `SCH-DEC-01` | Schedule có lọc attendance roster hay chỉ là metadata? | Lọc roster theo weekday; đây mới là ý nghĩa nghiệp vụ của lịch học |
| `SCH-DEC-02` | Backfill Student cũ | `FullDay` + Thứ Hai–Thứ Bảy + version 1 |
| `SCH-DEC-03` | Ảnh hưởng của mode lên attendance status | Chỉ đặt default; không giới hạn status giáo viên có thể chọn |
| `SCH-DEC-04` | Có snapshot study mode vào attendance record? | Chưa làm v1; chỉ thêm nếu cần báo cáo mode lịch sử |
| `SCH-DEC-05` | Có lịch sử/effective date cho schedule? | Không; chỉ một schedule hiện tại, Saved sheet giữ lịch sử thực tế |
| `SCH-DEC-06` | Có thêm Student version/expectedVersion? | Có, dùng chung cho full PUT/group/delete để tránh lost update |
| `SCH-DEC-07` | Chủ nhật hoặc ngày không có scheduled Student | Không cho tạo empty sheet; trả `NoScheduledStudents` |
| `SCH-DEC-08` | Phân nhóm lúc create hay action riêng? | Action riêng dùng endpoint `/students/{id}/group`; giữ một mutation surface |

Không bắt đầu `SCH-00` cho đến khi tám quyết định trên được user xác nhận hoặc điều chỉnh.

## 15. Definition of Done

- Student page phân/chuyển/gỡ group bằng đúng endpoint và hiển thị group hiện tại.
- Mọi Student có schedule hợp lệ với mode và 1–6 ngày Thứ Hai–Thứ Bảy.
- API không expose weekday mask; JSON enum string/camelCase, UI tiếng Việt.
- Student full PUT/group/delete chống stale write bằng expected version.
- Nếu `SCH-DEC-01` chọn operational schedule, context/Missing/POST dùng cùng scheduled roster và Saved sheet bất biến.
- Migration fresh/upgrade/backfill, database constraints và EF snapshot đều sạch.
- Audit không chứa dữ liệu cá nhân/note thô; authorization nằm ở application service.
- Backend/frontend regression đều pass và `tasks.md`/memory/docs được cập nhật.
