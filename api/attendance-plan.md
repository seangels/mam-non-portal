# Kế hoạch phát triển tính năng điểm danh

## 1. Thông tin kế hoạch

- **Epic:** `ATT`
- **Trạng thái:** `ATT-DEC-01` đến `ATT-DEC-10` đã chốt; sẵn sàng khóa OpenAPI draft và triển khai `ATT-01`.
- **Ngày lập:** 2026-08-11.
- **Phạm vi:** .NET 10 REST API, PostgreSQL 17, Angular/DevExtreme UI và đóng gói IIS.
- **Contract nền:** [`plan.md`](plan.md).

Mỗi đợt phát triển có mã `ATT-00` đến `ATT-05`. Task backend, frontend và kiểm thử dùng hậu tố `ATT-BE-*`, `ATT-FE-*`, `ATT-QA-*`. Mã đã cấp không tái sử dụng; việc phát sinh dùng hậu tố, ví dụ `ATT-FE-04A`.

Tên branch/commit đề xuất:

```text
feat/att-02-attendance-read
[ATT-BE-03] Add scoped daily attendance query
```

## 2. Mục tiêu nghiệp vụ

- Thêm item `Điểm danh` trên sidebar cho `Teacher`, `Admin`, `SuperAdmin`.
- Giáo viên chỉ xem và điểm danh học sinh thuộc nhóm mình đang phụ trách; quyền không được suy diễn ngược theo ngày vì v1 không lưu lịch sử phân công.
- `Admin` và `SuperAdmin` được thao tác mọi nhóm.
- Mỗi giáo viên thường phụ trách một nhóm, nhưng mô hình vẫn hỗ trợ một giáo viên phụ trách nhiều nhóm.
- Mỗi nhóm có tối đa 100 học sinh. UI bố trí để nhìn rõ khoảng 8–10 card trong một viewport và cuộn xuống để xem phần còn lại.
- Màn hình chính hiển thị danh sách dạng card, tối ưu cho thao tác nhanh trên desktop, tablet và điện thoại.
- Bộ lọc mặc định mở, có thể collapse/expand; gồm ngày, nhóm theo điều kiện quyền và tìm kiếm tên/mã/nickname không phân biệt dấu.
- Trạng thái mặc định là `Có mặt`; lần xác nhận đầu tiên lưu đầy đủ một record cho mọi học sinh trong phiếu, kể cả `Có mặt`.
- Các trạng thái còn lại gồm `Vắng nguyên buổi`, `Vắng 1/2 buổi`, `Học 1-1 (1 giờ)`, kèm thông tin phép/không phép khi phù hợp và ghi chú.

## 3. Ngoài phạm vi epic `ATT`

- Báo cáo tháng, thống kê chuyên cần và dashboard phân tích.
- Tính học phí/lương từ dữ liệu 1-1.
- Check-in theo thời điểm đến/về, QR, nhận diện khuôn mặt hoặc định vị.
- Import/export Excel.
- Thông báo tự động cho phụ huynh.
- Offline-first hoặc đồng bộ nhiều thiết bị khi mất mạng.
- Lịch học/ca học tùy biến phức tạp; nếu cần sẽ là feature riêng.

## 4. Quy ước nghiệp vụ đã chốt

### 4.1. Ngày điểm danh

- Điểm danh theo ngày nghiệp vụ `DateOnly`, format API `YYYY-MM-DD`.
- UI mặc định ngày hiện tại theo `Asia/Ho_Chi_Minh`; không dùng chuyển đổi UTC có thể làm lệch ngày.
- Backend dùng option `BusinessTimeZone = Asia/Ho_Chi_Minh` cùng `TimeProvider` để xác định ngày server; không lấy trực tiếp ngày UTC hoặc tin đồng hồ browser.
- Không cho ghi ngày tương lai.
- Mỗi Teacher có `attendanceEditWindowDays` từ 1 đến 7, mặc định 7 và do Admin/SuperAdmin cấu hình riêng.
- Window bao gồm hôm nay: giá trị 1 chỉ cho hôm nay; giá trị 7 cho hôm nay và 6 ngày lịch trước đó. Backend tính theo `serverDate` và `BusinessTimeZone`.
- Admin/SuperAdmin được sửa mọi ngày không nằm trong tương lai.

### 4.2. Trạng thái và quy tắc lưu phiếu

Mỗi record học sinh trong phiếu dùng một trong bốn trạng thái:

- `Present`: có mặt và được lưu thành record thật sau khi phiếu được xác nhận.
- `AbsentFullDay`: vắng cả ngày học; UI hiển thị `Vắng nguyên buổi`.
- `AbsentHalfDay`: vắng nửa ngày, bắt buộc chọn `Morning` hoặc `Afternoon`.
- `OneToOneHour`: học 1-1 một block cố định 60 phút, loại trừ với hai trạng thái vắng.

Quy tắc:

- Không có phiếu nghĩa là `Chưa điểm danh`, không có nghĩa tất cả học sinh có mặt.
- Daily GET khi chưa có phiếu chỉ trả preview roster với trạng thái mặc định `Present`; preview chưa phải lịch sử đã xác nhận.
- Lần lưu đầu tạo một `attendance_sheet` và đúng một `attendance_record` cho mọi học sinh trong roster, kể cả `Present`, trong cùng transaction.
- Sau khi tạo, roster và các trường snapshot của phiếu không tự thay đổi khi student/group/teacher hiện tại thay đổi.
- Chuyển từ trạng thái khác về `Present` là update record hiện hữu; clear `halfDayPart`, `isExcused`, `durationMinutes`, không delete row.
- `isExcused` bắt buộc với hai loại vắng và không áp dụng cho `Present`/`OneToOneHour`.
- `notes` nullable, trim, tối đa 2.000 ký tự và áp dụng được cho mọi trạng thái, kể cả `Present`.
- V1 không thêm Draft/Finalized. Phiếu có hai state API/UI: `Missing` và `Saved`; phiếu Saved vẫn được chỉnh trong edit window.

### 4.3. Nhóm và phân công hiện tại

- Mỗi group có tối đa một giáo viên phụ trách hiện tại; một Teacher có thể phụ trách nhiều group nên UI vẫn có conditional group filter.
- Mỗi Student có tối đa một `group_id` hiện tại; không dùng bảng assignment theo thời gian.
- Tối đa 100 Student active trong một group. API gán/chuyển student kiểm tra giới hạn và tăng `snapshot_version` trong cùng transaction để tránh race.
- Không có `effective_from`/`effective_to`. Các thay đổi group/teacher có hiệu lực ngay; lịch sử ngày đã lưu nằm trong snapshot của attendance sheet/record.
- Không seed group hoặc assignment. Admin/SuperAdmin tạo group và phân công qua UI/API.
- Ngày quá khứ chưa từng tạo phiếu có thể không tái dựng chính xác sau khi snapshot thay đổi; API trả `Missing`, đặt `canCreate = false` và không coi preview `Present` là lịch sử đã xác nhận.

### 4.4. Mô hình lưu trữ đã chốt

`ATT-DEC-10` chọn **full daily snapshot**: `attendance_sheets` + `attendance_records`, lưu cả `Present`. Không tạo `attendance_exceptions`, `teacher_group_assignments` hoặc `student_group_assignments` temporal.

## 5. Thiết kế dữ liệu

Tất cả khóa chính dùng UUID; thời điểm audit dùng UTC `timestamptz`; ngày điểm danh dùng PostgreSQL `date`.

### 5.1. `teachers`

```text
id                           uuid PK
user_id                      uuid NOT NULL UNIQUE FK -> users.id RESTRICT
attendance_edit_window_days  smallint NOT NULL DEFAULT 7
created_at                   timestamptz
updated_at                   timestamptz
```

- Không lặp email, tên, status hoặc password; đọc từ `users`.
- Tạo/reuse profile trong cùng transaction khi user trở thành role `Teacher`; migration backfill Teacher hiện có, không seed tài khoản.
- `attendance_edit_window_days` có check constraint từ 1 đến 7 và được đọc từ DB mỗi request, không đặt trong JWT.
- Đổi role/xóa Teacher đang phụ trách group trả `409`; phải chuyển hoặc gỡ group trước.

### 5.2. `student_groups`

```text
id                      uuid PK
code                    varchar(50)
name                    varchar(200)
status                  varchar(30)       -- Active | Inactive
responsible_teacher_id  uuid null FK -> teachers.id RESTRICT
snapshot_version        integer NOT NULL DEFAULT 1
snapshot_changed_at     timestamptz NOT NULL
created_at              timestamptz
updated_at              timestamptz
deleted_at              timestamptz null
```

- `code` unique trên record chưa soft-delete; `snapshot_version >= 1`.
- Một Teacher có thể xuất hiện ở nhiều group; một group chỉ có một responsible Teacher trong v1.
- `snapshot_version` tăng nguyên tử khi code/name của group, responsible Teacher, tên hiển thị của Teacher, roster Student hoặc các field snapshot của Student (`studentCode`, `fullName`, `nickName`) thay đổi. `snapshot_changed_at` ghi lần thay đổi snapshot gần nhất để đánh giá khả năng tạo phiếu quá khứ.
- Các luồng User/Student CRUD liên quan phải tăng version cho mọi group bị ảnh hưởng trong cùng transaction; stale preview vì đổi identity cũng bị từ chối như stale roster.
- Nhóm inactive/deleted không nhận phân công hoặc sheet mới theo luồng `CurrentSnapshot`; `HistoricalRecovery` của Admin/SuperAdmin là ngoại lệ duy nhất. Không inactive/delete khi còn Teacher/Student hiện tại.

### 5.3. Mở rộng `students`

```text
group_id           uuid null FK -> student_groups.id RESTRICT
group_assigned_at  timestamptz null
group_assigned_by  uuid null FK -> users.id RESTRICT
```

- `group_id = null` nghĩa là chưa phân nhóm.
- Index partial `(group_id, status, id) WHERE deleted_at IS NULL` phục vụ roster.
- Gán/chuyển group khóa Student và group cũ/mới trong transaction, kiểm tra cap 100, rồi tăng version của group bị ảnh hưởng.
- Student đã có attendance record trong ngày hiện tại không được chuyển group trong ngày đó; thao tác trả `409` để tránh xuất hiện ở hai phiếu.
- Inactive/delete Student còn `group_id` trả `409`; phải gỡ group trước.

### 5.4. `attendance_sheets`

```text
id                                 uuid PK
group_id                           uuid NOT NULL FK -> student_groups.id RESTRICT
attendance_date                    date NOT NULL
group_code_snapshot                varchar(50) NOT NULL
group_name_snapshot                varchar(200) NOT NULL
responsible_teacher_id             uuid NOT NULL FK -> teachers.id RESTRICT
responsible_teacher_name_snapshot  varchar(200) NOT NULL
snapshot_source                    varchar(30) NOT NULL
source_snapshot_version            integer null
recovery_reason                    varchar(500) null
version                            integer NOT NULL DEFAULT 1
created_by_user_id                 uuid NOT NULL FK -> users.id RESTRICT
updated_by_user_id                 uuid NOT NULL FK -> users.id RESTRICT
created_at                         timestamptz
updated_at                         timestamptz
```

- Unique `(group_id, attendance_date)`; `version >= 1`; index `(attendance_date, group_id)`.
- `version` là optimistic concurrency token của toàn phiếu và tăng đúng một lần mỗi batch update.
- `snapshot_source = CurrentSnapshot` bắt buộc có `source_snapshot_version` và `recovery_reason = null`; `snapshot_source = HistoricalRecovery` bắt buộc source version null và recovery reason đã trim, không rỗng.
- Không soft-delete phiếu trong v1. Maintenance không xóa sheet/record.
- Sheet snapshot group/teacher để lịch sử không đổi khi tên hoặc phân công hiện tại thay đổi. Provenance/recovery reason thuộc dữ liệu nghiệp vụ giữ lâu dài, không chỉ nằm trong audit 90 ngày.

### 5.5. `attendance_records`

```text
id                     uuid PK
sheet_id               uuid NOT NULL FK -> attendance_sheets.id RESTRICT
attendance_date        date NOT NULL
student_id             uuid NOT NULL FK -> students.id RESTRICT
student_code_snapshot  varchar(50) NOT NULL
full_name_snapshot     varchar(200) NOT NULL
nick_name_snapshot     varchar(200) NOT NULL
status                 varchar(40) NOT NULL
half_day_part          varchar(20) null
is_excused             boolean null
duration_minutes       integer null
notes                  varchar(2000) null
updated_by_user_id     uuid NOT NULL FK -> users.id RESTRICT
created_at             timestamptz
updated_at             timestamptz
```

- Unique `(sheet_id, student_id)` và `(student_id, attendance_date)`; record date phải khớp sheet date bằng composite FK/constraint tương đương.
- Sheet creation insert đủ N records mặc định `Present`, sau đó áp dữ liệu gửi lên trong cùng transaction.
- Check constraint đồng bộ bảng validation ở mục 7.4.
- Saved search/display dùng snapshot fields, không dùng tên/code hiện tại từ `students`.

### 5.6. Invariant concurrency và lock

- Mọi mutation làm đổi attendance snapshot phải lock group row liên quan rồi tăng `snapshot_version` và `snapshot_changed_at` trong cùng transaction: group code/name, responsible Teacher, tên Teacher, Student code/name/nickname và assign/move/unassign Student.
- Flow chạm nhiều group lock theo thứ tự UUID tăng dần để tránh deadlock. Sheet creation lock group row trước khi đọc version, identity và active roster; vì mọi snapshot mutation cũng đi qua lock này nên snapshot được tạo là nhất quán.
- `attendance_sheets.version` là EF Core concurrency token hoặc được update bằng điều kiện tương đương `WHERE id = @id AND version = @expectedVersion`; affected rows bằng 0 trả `409 SheetVersionConflict`.
- Mọi record mutation và sheet-version update nằm trong một transaction. Nếu validation/concurrency thất bại, toàn bộ batch rollback; không để sheet version hoặc một phần records được ghi riêng lẻ.
- Unique database constraints vẫn là lớp bảo vệ cuối cho concurrent create và `(student_id, attendance_date)`; application map database conflict sang machine-readable `ProblemDetails.code`.

## 6. Authorization

| Hành động | SuperAdmin | Admin | Teacher |
|---|---:|---:|---:|
| Xem nhóm khả dụng | Tất cả | Tất cả | Chỉ group đang phụ trách |
| Xem phiếu/preview | Tất cả | Tất cả | Chỉ group đang phụ trách |
| Tạo/sửa phiếu | Tất cả ngày không tương lai | Tất cả ngày không tương lai | Chỉ group đang phụ trách và trong edit window |
| CRUD nhóm/phân công | Có | Có | Không |
| CRUD user/student cũ | Theo contract hiện tại | Theo contract hiện tại | Không |

Quy tắc bắt buộc:

- Không nhận `teacherId` từ client để quyết định scope; resolve Teacher từ current actor và `student_groups.responsible_teacher_id`.
- Teacher gửi group không phụ trách trả `403`; attendance/student ID ngoài scope trả `404` để hạn chế dò dữ liệu.
- Quyền Teacher dựa trên phân công hiện tại. Khi bị gỡ group, Teacher mất quyền truy cập phiếu của group đó; Admin/SuperAdmin vẫn xem được lịch sử.
- Teacher được create/edit khi `businessToday - (attendanceEditWindowDays - 1) <= attendanceDate <= businessToday`.
- Admin/SuperAdmin không chịu Teacher window nhưng mọi role đều bị chặn ngày tương lai.
- Tạo sheet chuẩn yêu cầu group active, có responsible Teacher và roster hợp lệ. `HistoricalRecovery` là ngoại lệ Admin/SuperAdmin có điều kiện như mục 7.4. Sửa sheet dùng snapshot records; thay đổi group/student hiện tại không tự sửa sheet cũ.
- GET không tạo dữ liệu. Missing preview không được coi là phiếu đã lưu.
- UI guard/menu chỉ là UX; application service là nơi thực thi quyền.

## 7. REST contract đề xuất

### 7.1. Quản lý giáo viên, nhóm và phân công hiện tại

`Admin` và `SuperAdmin`:

```http
GET    /api/v1/teachers
GET    /api/v1/teachers/{id}
PUT    /api/v1/teachers/{id}/attendance-policy

GET    /api/v1/student-groups
POST   /api/v1/student-groups
GET    /api/v1/student-groups/{id}
PUT    /api/v1/student-groups/{id}
DELETE /api/v1/student-groups/{id}

PUT    /api/v1/student-groups/{groupId}/responsible-teacher
PUT    /api/v1/students/{studentId}/group
```

- Responsible-teacher request nhận `{ "teacherId": "uuid-or-null" }`; một Teacher có thể phụ trách nhiều group.
- Student-group request nhận `{ "groupId": "uuid-or-null" }`; move chạy atomically, kiểm tra group active/cap 100 và tăng `snapshot_version` group cũ/mới.
- Không có effective date hoặc endpoint kết thúc lịch sử; mọi thay đổi có hiệu lực ngay và được audit.
- `GET /teachers` trả `{ items, pagination }`, hỗ trợ `search`, `status`, `page`, `pageSize`, sort whitelist; chỉ trả profile có user role `Teacher` theo filter nghiệp vụ và không trả thông tin authentication.
- `PUT /teachers/{id}/attendance-policy` nhận `{ "attendanceEditWindowDays": 1..7 }`; Admin/SuperAdmin đều được cấu hình và response trả policy mới.
- `GET /student-groups` hỗ trợ `search`, `status`, pagination và sort whitelist.
- Có filter `unassigned=true` cho Teacher/Student theo phân công hiện tại.
- Group code/name bắt buộc, trim, giới hạn độ dài; code được normalize và unique trên record chưa xóa. Mọi FK dùng delete behavior `RESTRICT`.

### 7.2. Context màn hình điểm danh

```http
GET /api/v1/attendance/context?date=2026-08-11
```

Response:

```json
{
  "date": "2026-08-11",
  "serverDate": "2026-08-11",
  "groups": [
    {
      "id": "00000000-0000-0000-0000-000000000000",
      "code": "N1",
      "name": "Nhóm 1",
      "studentCount": 10
    }
  ],
  "attendanceEditWindowDays": 7,
  "canEdit": true,
  "readOnlyReason": null
}
```

- Teacher chỉ nhận group đang có `responsible_teacher_id` là Teacher hiện tại.
- Admin/SuperAdmin nhận mọi group active; khi xem ngày quá khứ, bổ sung group có Saved sheet tại ngày đó kể cả group hiện inactive/soft-delete.
- `studentCount` là current active roster khi sheet Missing và là số snapshot records khi sheet Saved.
- `serverDate`, `canEdit`, `readOnlyReason` giúp UI hiển thị read-only đúng policy; backend vẫn tự kiểm tra lại khi mutation.
- Context trả thêm `attendanceEditWindowDays` của Teacher để UI giải thích window; backend tính `canEdit` bằng ngày server và không tin giá trị client.
- Không dùng assignment theo ngày; quyền Teacher vẫn dựa trên group hiện tại, còn Admin/SuperAdmin dùng sheet snapshot để xem lịch sử.
- UI tự xác định hiển thị group filter dựa trên role và số group; backend không tin quyết định ẩn/hiện của UI.

### 7.3. Danh sách card theo ngày

```http
GET /api/v1/attendance/daily
    ?date=2026-08-11
    &groupId={uuid}
```

- `groupId` bắt buộc với Admin/SuperAdmin và Teacher có nhiều group.
- Teacher có đúng một group có thể bỏ `groupId`; backend tự resolve.
- Admin/SuperAdmin bắt buộc chọn một group, không có lựa chọn `Tất cả nhóm`, để tránh thao tác nhầm và giữ danh sách nhỏ.
- Không phân trang trong v1 vì luôn scope một group và giới hạn nghiệp vụ là 100 học sinh. API trả toàn bộ roster của group; UI cuộn danh sách card.
- Nếu sheet Saved, đọc `attendance_records` snapshot. Nếu sheet Missing, trả preview current roster với `Present` nhưng không ghi DB.

Response Missing đề xuất:

```json
{
  "date": "2026-08-11",
  "serverDate": "2026-08-11",
  "group": {
    "id": "00000000-0000-0000-0000-000000000000",
    "code": "N1",
    "name": "Nhóm 1"
  },
  "sheetState": "Missing",
  "sheetId": null,
  "sheetVersion": null,
  "snapshotSource": null,
  "currentSnapshotVersion": 5,
  "sourceSnapshotVersion": null,
  "canCreate": true,
  "canEdit": false,
  "canRecover": false,
  "readOnlyReason": null,
  "summary": {
    "rosterTotal": 10,
    "present": 10,
    "absent": 0,
    "oneToOne": 0
  },
  "items": [
    {
      "entryId": null,
      "studentId": "00000000-0000-0000-0000-000000000000",
      "studentCode": "HS001",
      "fullName": "Nguyễn Văn An",
      "nickName": "Bé An",
      "status": "Present",
      "halfDayPart": null,
      "isExcused": null,
      "durationMinutes": null,
      "notes": null,
      "updatedAt": null
    }
  ]
}
```

Quy tắc hiển thị:

- `sheetState = Missing` phải có banner `Phiếu chưa được lưu`; summary chỉ là preview, không khẳng định học sinh đã có mặt.
- `sheetState = Saved` trả `sheetId`, `sheetVersion`, record có `entryId` và summary chính thức từ toàn bộ snapshot.
- Không có sheet quá khứ và không thể xác minh snapshot trả `canCreate = false`, `readOnlyReason = HistoricalSnapshotUnavailable`; Admin/SuperAdmin nhận thêm `canRecover = true` khi đủ điều kiện recovery, Teacher luôn nhận `false`.
- Với sheet Missing, `currentSnapshotVersion` là version dùng cho POST và `sourceSnapshotVersion = null`. Với sheet Saved, `currentSnapshotVersion = null`, `sourceSnapshotVersion` chỉ có giá trị cho `CurrentSnapshot`; update chỉ concurrency bằng `sheetVersion`.
- Hai quyền không thay thế nhau: Missing dùng `canCreate`, Saved dùng `canEdit`. UI tính `canModify = sheetState === 'Missing' ? canCreate : canEdit` và không dựa vào `canEdit` để khóa first-save.
- Search không dấu chạy local trên toàn bộ tối đa 100 authorized items; summary luôn tính toàn phiếu/preview, không bị search thay đổi.

Response Saved rút gọn:

```json
{
  "date": "2026-08-11",
  "serverDate": "2026-08-11",
  "group": {
    "id": "00000000-0000-0000-0000-000000000000",
    "code": "N1",
    "name": "Nhóm 1"
  },
  "sheetState": "Saved",
  "sheetId": "00000000-0000-0000-0000-000000000000",
  "sheetVersion": 3,
  "snapshotSource": "CurrentSnapshot",
  "currentSnapshotVersion": null,
  "sourceSnapshotVersion": 5,
  "canCreate": false,
  "canEdit": true,
  "canRecover": false,
  "readOnlyReason": null,
  "summary": {
    "rosterTotal": 10,
    "present": 8,
    "absent": 2,
    "oneToOne": 0
  },
  "items": [
    {
      "entryId": "00000000-0000-0000-0000-000000000000",
      "studentId": "00000000-0000-0000-0000-000000000000",
      "studentCode": "HS001",
      "fullName": "Nguyễn Văn An",
      "nickName": "Bé An",
      "status": "Present",
      "halfDayPart": null,
      "isExcused": null,
      "durationMinutes": null,
      "notes": null,
      "updatedAt": "2026-08-11T02:30:00Z"
    }
  ]
}
```

Saved sheet phục hồi trả `snapshotSource = HistoricalRecovery`, cả hai version snapshot đều null; UI hiển thị provenance warning nhưng vẫn dùng `sheetVersion` để update theo quyền.

### 7.4. Tạo và cập nhật phiếu

Tạo lần đầu:

```http
POST /api/v1/attendance/sheets
```

Request:

```json
{
  "groupId": "00000000-0000-0000-0000-000000000000",
  "date": "2026-08-11",
  "expectedSnapshotVersion": 5,
  "records": [
    {
      "studentId": "00000000-0000-0000-0000-000000000000",
      "status": "AbsentHalfDay",
      "halfDayPart": "Morning",
      "isExcused": true,
      "durationMinutes": null,
      "notes": "Gia đình đã báo trước"
    }
  ]
}
```

Ví dụ được rút gọn còn một phần tử; request thực tế phải gửi đúng toàn bộ roster.

Lần tạo đầu:

- Client gửi full roster tối đa 100 records, kể cả các card `Present`; thao tác lưu với toàn bộ Present vẫn tạo sheet hợp lệ.
- Server lock group, so sánh `expectedSnapshotVersion`, kiểm tra danh sách student khớp current active roster và tạo sheet + toàn bộ records atomically. `source_snapshot_version` luôn lấy từ group row đã lock, không copy mù giá trị client.
- Nếu tên nhóm, responsible Teacher hoặc roster thay đổi giữa GET và POST, trả `409 SnapshotChanged`; không tự sửa snapshot hay thêm student mà người dùng chưa thấy.
- Một group/date chỉ có một sheet; concurrent create chỉ một request nhận `201 Created`, request còn lại `409`.
- Tạo sheet quá khứ chỉ dùng current snapshot khi `snapshot_changed_at` không muộn hơn cuối ngày nghiệp vụ cần tạo; điều đó chứng minh snapshot hiện tại chưa đổi sau ngày đó. Nếu không chứng minh được, trả `409 HistoricalSnapshotUnavailable`.
- Admin/SuperAdmin recovery lịch sử là command riêng với danh sách student, responsible Teacher, `acknowledgeHistoricalSnapshot=true` và reason bắt buộc; audit `Attendance.HistoricalSheetRecovered`. Teacher không có recovery này.
- POST chuẩn thành công trả `201 Created`, header `Location: /api/v1/attendance/sheets/{sheetId}` và full Saved snapshot.

Recovery quản trị cho ngày quá khứ không thể tái dựng từ current snapshot:

```http
POST /api/v1/attendance/sheets/historical-recovery
```

Candidate lookup chỉ dành cho Admin/SuperAdmin:

```http
GET /api/v1/attendance/historical-recovery/group-candidates?search={text}&page=1&pageSize=20
GET /api/v1/attendance/historical-recovery/student-candidates?search={text}&page=1&pageSize=20
GET /api/v1/attendance/historical-recovery/teacher-candidates?search={text}&page=1&pageSize=20
```

- Ba endpoint trả `{ items, pagination }`, search không dấu và có thể tìm cả record inactive/soft-delete/former role để đối chiếu phiếu giấy; không trả email đăng nhập, password, session hoặc dữ liệu authentication.
- Group item gồm `id`, `code`, `name`, `status`, `isDeleted`; endpoint này là nguồn chọn group inactive/soft-delete chưa có Saved sheet, vốn không xuất hiện trong context thông thường.
- Student item gồm `id`, `studentCode`, `fullName`, `nickName`, `status`, `isDeleted`, `currentGroupId`. Teacher item gồm `id`, `userId`, `fullName`, `status`, `isDeleted`, `isCurrentTeacherRole`.
- Chỉ dùng cho recovery picker; không thay đổi semantics các list quản trị hiện hành và không tự suy diễn ai thuộc roster lịch sử.

```json
{
  "groupId": "00000000-0000-0000-0000-000000000000",
  "date": "2026-08-01",
  "responsibleTeacherId": "00000000-0000-0000-0000-000000000000",
  "records": [
    {
      "studentId": "00000000-0000-0000-0000-000000000000",
      "status": "Present",
      "halfDayPart": null,
      "isExcused": null,
      "durationMinutes": null,
      "notes": null
    }
  ],
  "acknowledgeHistoricalSnapshot": true,
  "recoveryReason": "Bổ sung phiếu giấy đã được đối chiếu"
}
```

- Chỉ Admin/SuperAdmin được gọi; bắt buộc `date < serverDate`, group/date chưa có sheet và standard creation thật sự không khả dụng vì snapshot đã đổi sau cuối ngày mục tiêu, group hiện inactive/soft-delete hoặc group không còn responsible Teacher. Không dùng recovery cho hôm nay, tương lai hoặc để bypass POST chuẩn.
- Group phải tồn tại kể cả inactive/soft-delete; `records` có từ 1 đến 100 Student ID có thật, duy nhất, kể cả inactive/soft-delete và không Student nào đã nằm trong sheet khác cùng ngày.
- Responsible Teacher profile phải tồn tại; recovery cho phép profile có user hiện inactive/soft-delete/đã đổi role vì đây là đối chiếu lịch sử.
- `acknowledgeHistoricalSnapshot` phải là `true`; `recoveryReason` trim, bắt buộc và tối đa 500 ký tự.
- Server lấy code/name/nickname snapshot từ dữ liệu tốt nhất hiện đang biết, không cho client tự ghi đè identity snapshot. Group code/name và Teacher/Student identity có thể không phải giá trị thật tại ngày quá khứ; UI phải cảnh báo rõ điều này.
- Đây là thao tác phục hồi có chủ ý, không phải cách tạo phiếu thông thường. Sheet lưu `snapshotSource = HistoricalRecovery`, source version null và reason lâu dài; concurrent normal/recovery create vẫn bị unique `(group_id, attendance_date)` chặn và trả `409 AttendanceSheetAlreadyExists`.
- Recovery thành công cũng trả `201 Created`, `Location` và full Saved snapshot với `snapshotSource = HistoricalRecovery`.

Cập nhật sheet đã lưu:

```http
PUT /api/v1/attendance/sheets/{sheetId}
```

```json
{
  "expectedVersion": 3,
  "records": [
    {
      "studentId": "00000000-0000-0000-0000-000000000000",
      "status": "Present",
      "halfDayPart": null,
      "isExcused": null,
      "durationMinutes": null,
      "notes": null
    }
  ]
}
```

Quy tắc update:

- `PUT` là full replacement trạng thái của roster snapshot; danh sách student ID phải khớp chính xác records của sheet.
- `expectedVersion` phải khớp aggregate sheet version; stale request trả `409` với `currentVersion`.
- Batch chạy all-or-nothing và tăng sheet version đúng một lần.
- `Present` là persisted status bình thường, không delete row; `halfDayPart`, `isExcused`, `durationMinutes` phải null nhưng `notes` vẫn được phép.
- Response trả full Saved snapshot mới để UI thay baseline/draft bằng server source of truth.
- Validation field dùng path như `records[0].halfDayPart` để UI map đúng card.
- Không có DELETE sheet/record trong v1.

Mọi lỗi nghiệp vụ dùng `ProblemDetails` với extension `code` ổn định để UI xử lý, không parse title/detail tiếng Việt. Tối thiểu gồm `SnapshotChanged`, `AttendanceSheetAlreadyExists`, `SheetVersionConflict`, `HistoricalSnapshotUnavailable`, `ResponsibleTeacherRequired`, `GroupInactive`, `AttendanceRosterMismatch` và `AttendanceEditWindowExceeded`; `409` trả thêm version hiện tại khi có thể.

Validation theo status:

| Status | `halfDayPart` | `isExcused` | `durationMinutes` |
|---|---|---|---|
| `Present` | null | null | null |
| `AbsentFullDay` | null | bắt buộc | null |
| `AbsentHalfDay` | bắt buộc | bắt buộc | null |
| `OneToOneHour` | null | null | `60` |

## 8. Search tiếng Việt không dấu

- API chỉ trả full roster của một group đã authorization, tối đa 100 records; không có pagination hoặc `search` query ở daily endpoint v1.
- Frontend giữ toàn bộ roster trong state và filter local trên `studentCode`, `fullName`, `nickName`. Điều này giữ được dirty state của card đang bị search ẩn.
- Normalization: lowercase, Unicode NFD, bỏ combining marks, đổi `đ/Đ` thành `d`, trim/collapse whitespace và contains-match.
- Search không thay đổi persisted state hoặc summary toàn phiếu; UI hiển thị thêm số card đang match.
- Không cần PostgreSQL extension/index cho attendance search v1.

Test bắt buộc: `nguyen`, `Nguyễn`, `NGUYEN` trả cùng kết quả; mã và nickname cũng tìm đúng; ẩn/hiện card không làm mất thay đổi local.

## 9. UI/UX

### 9.1. Navigation và route

- Sidebar thêm `Điểm danh`, path `/attendance`, hiển thị cho cả ba role.
- URL thực tế `/#/attendance` vì app dùng hash routing.
- Route dùng `SetupCompletedGuard`, `AuthGuardService`, `RoleGuard` với `SuperAdmin`, `Admin`, `Teacher`.
- Có CTA điểm danh trên trang Home; Teacher không còn empty state chỉ có dashboard/profile.

### 9.2. Filter panel

- Panel mặc định expand; header có nút collapse/expand, tooltip, `aria-expanded`, `aria-controls`.
- Filter ngày mặc định hôm nay, group và full-text search.
- Admin/SuperAdmin: luôn hiện group selector và bắt buộc chọn một group.
- Teacher có trên một group: hiện selector.
- Teacher có đúng một group: ẩn selector, tự chọn group và hiển thị tên group dạng badge/read-only.
- Teacher không có group: empty state `Chưa được phân công nhóm`, không gọi daily API.
- Search placeholder: `Tên, mã học sinh, tên gọi`; filter local không dấu trên full roster, đổi search thì scroll về đầu nhưng không làm mất draft của card bị ẩn.
- Đổi ngày/group khi còn dirty changes phải cảnh báo trước.
- Admin/SuperAdmin chưa chọn group thì hiển thị hướng dẫn chọn nhóm và không gọi daily API.

### 9.3. Card list

Mỗi card hiển thị:

- Họ tên, mã học sinh, nickname và tên nhóm khi cần.
- Trạng thái bằng text + icon, không chỉ bằng màu.
- Điều khiển chọn `Có mặt`, `Vắng nguyên buổi`, `Vắng 1/2 buổi`, `Học 1-1 (1 giờ)`.
- Chọn sáng/chiều khi vắng 1/2 buổi.
- Chọn rõ `Có xin phép`/`Không phép` khi vắng.
- Notes với counter giới hạn ký tự.
- Trạng thái phiếu `Chưa lưu`/`Đã lưu`, dirty/đang lưu/lỗi.

Vùng danh sách có chiều cao/responsive spacing để thường hiển thị rõ khoảng 8–10 card cùng lúc. Khi group có nhiều hơn, người dùng cuộn dọc; không cắt dữ liệu, không giới hạn 10 card và không dùng pagination ở màn hình v1.

UI giữ `baselineByStudentId` và `draftByStudentId`. Khi chọn `Có mặt`, clear các field conditional nhưng giữ record. Không autosave ngay khi chạm.

- Sheet Missing: banner `Phiếu chưa được lưu`; nút `Lưu phiếu` vẫn enabled khi dirty count bằng 0 vì lần lưu đầu xác nhận toàn bộ roster Present.
- Sheet Saved: không dirty thì disable save; có dirty dùng sticky action `Lưu thay đổi (n)`, nhưng request PUT gửi full roster tối đa 100 records.
- Sau save thành công, full response thay cả baseline/draft và dirty về 0.
- Search/collapse/scroll không tạo dirty. Validation card đang bị search ẩn phải tự hiện rồi scroll/focus tới lỗi.

Summary đầu danh sách:

```text
10 học sinh · Có mặt 8 · Vắng 2 · Học 1-1 0
```

### 9.4. Error, loading và accessibility

- State riêng cho loading context, loading list và saving batch.
- `401` dùng interceptor/session restore hiện có.
- Missing + `canCreate = true` cho sửa preview và first-save; Missing + `canCreate = false` là read-only/recovery.
- Saved + `canEdit = true` cho update; Saved + `canEdit = false` vẫn hiển thị snapshot nhưng khóa controls/save và nêu `readOnlyReason`.
- Khi group bị chuyển khỏi Teacher, `403` xóa snapshot cũ và reload context.
- `409` báo dữ liệu đã thay đổi và cho reload, không silent overwrite.
- Missing có `canRecover = true`: Admin/SuperAdmin có CTA mở recovery flow, bắt buộc chọn roster/Teacher, xác nhận cảnh báo và nhập lý do; Teacher chỉ thấy read-only explanation.
- Validation map đúng card/field; có trace ID khi cần hỗ trợ.
- Empty state riêng cho chưa phân công, nhóm rỗng, search rỗng và API lỗi.
- Mobile controls full width, touch target đủ lớn; sticky save không che card cuối.
- Radio/segmented controls có accessible label chứa tên học sinh; tab order theo card và focus lỗi đầu tiên.
- Dirty guard bao phủ đổi ngày/group, chuyển route/sidebar và `beforeunload`. Search local không kích hoạt guard.
- Missing nhưng chưa dirty không cần beforeunload warning; banner phải thể hiện rõ ngày đó chưa được ghi nhận.

## 10. Quản trị nhóm/phân công qua UI

Tính năng chưa vận hành hoàn chỉnh nếu chỉ có schema/API. `ATT-01` phải cung cấp UI cho Admin/SuperAdmin:

- Tạo/sửa/inactive nhóm.
- Gán/gỡ một responsible Teacher hiện tại cho group; một Teacher có thể phụ trách nhiều group.
- Gán/gỡ/chuyển current group của Student, không nhập ngày hiệu lực.
- Danh sách cảnh báo Teacher/Student chưa được phân công.
- Hiển thị số lượng hiện tại trên tối đa 100; từ chối gán/chuyển học sinh thứ 101 bằng validation rõ ràng.

Vị trí UI đã chốt: thêm trang `/student-groups` và item sidebar `Nhóm`; dùng picker từ user role Teacher và Student hiện có, không tạo duplicate tài khoản/học sinh. Trang quản lý Teacher cho Admin/SuperAdmin cấu hình window sửa điểm danh từ 1 đến 7 ngày. UI hiển thị warning rõ khi move bị chặn vì Student đã nằm trong sheet của ngày hiện tại.

## 11. Audit, logging và retention

Audit tối thiểu:

- `TeacherProfile.Created`.
- `Teacher.AttendancePolicyUpdated`.
- `Group.Created`, `Group.Updated`, `Group.Deleted`.
- `Group.ResponsibleTeacherAssigned`, `Group.ResponsibleTeacherRemoved`.
- `Student.GroupAssigned`, `Student.GroupMoved`, `Student.GroupRemoved`.
- `Attendance.SheetCreated`, `Attendance.SheetUpdated`, `Attendance.HistoricalSheetRecovered`.

Audit attendance chứa actor, IDs, ngày, trạng thái, phép/không phép, duration và version. Mặc định chỉ ghi `notesChanged`, không nhân đôi raw notes vào audit/application log. Không log request body.

Attendance sheet/record là dữ liệu nghiệp vụ và được giữ lâu dài, không thuộc cleanup tự động. Audit thay đổi giữ 90 ngày theo policy hiện tại.

## 12. Migration và tương thích dữ liệu hiện có

Migration `AddAttendanceFoundation`:

1. Tạo/backfill `teachers`, mặc định `attendance_edit_window_days = 7`.
2. Tạo `student_groups` với responsible Teacher, snapshot version/timestamp.
3. Thêm nullable current group fields vào `students`.
4. Tạo `attendance_sheets` và `attendance_records` cùng FK `RESTRICT`, unique/check/index và sheet concurrency version.
5. Không tạo temporal assignment hoặc `attendance_exceptions`.
6. Không tự tạo group, phân nhóm, sheet hoặc record; existing Student giữ `group_id = null` và Admin xử lý qua UI.
7. Sinh Designer/snapshot, chạy `has-pending-model-changes`, test database rỗng và database hiện có User/Student.

Release phải cập nhật OpenAPI, `api/README.md`, `api/requests.http`, agent memory và IIS package sau khi tất cả test đạt.

## 13. Test plan

### 13.1. Backend unit

- Lifecycle Teacher/group/student current assignment và conflict khi còn liên kết.
- Validation/configuration `attendanceEditWindowDays` từ 1 đến 7.
- Validation từng attendance status và conditional fields.
- Authorization theo role/group/date.
- Resolve group khi Teacher có 0/1/nhiều nhóm.
- Group cap 100 và snapshot-version rule, gồm cả thay đổi identity snapshot từ User/Student CRUD.
- Sheet concurrency version và full-replacement batch all-or-nothing.
- Business date/timezone quanh ranh giới UTC và local day.
- Window 1 ngày/7 ngày và policy riêng của hai Teacher khác nhau.

### 13.2. PostgreSQL integration

- Migration/backfill từ database hiện có.
- Concurrent Student assignment không vượt cap 100; snapshot version group cũ/mới tăng đúng.
- Đổi code/name group, responsible Teacher, tên Teacher hoặc code/name/nickname Student làm tăng snapshot version của đúng group.
- GET Missing không ghi DB và trả preview Present với `sheetState = Missing`.
- POST full roster tạo đúng N persisted records, kể cả khi tất cả Present.
- Snapshot student/group/teacher không đổi sau rename, move hoặc soft-delete dữ liệu hiện tại.
- Standard create lưu đúng `snapshotSource = CurrentSnapshot` và `sourceSnapshotVersion` từ group row đã lock.
- Teacher A không xem/sửa student nhóm Teacher B.
- Teacher nhiều nhóm lọc đúng; Admin/SuperAdmin thao tác mọi nhóm.
- Missing historical sheet với snapshot đã đổi trả `409`; Admin recovery yêu cầu student list/Teacher/reason/acknowledgement và audit.
- Recovery lưu `snapshotSource = HistoricalRecovery`, source version null và provenance/reason vẫn còn sau audit cleanup.
- Concurrent standard create/recovery chỉ tạo đúng một sheet.
- `expectedSnapshotVersion` stale và concurrent create trả `409`.
- User/Student rename đồng thời với create không thể sinh identity snapshot mới kèm source version cũ.
- Sheet-version stale trả `409`; full update rollback toàn bộ khi một record lỗi.
- Full PUT thiếu, thừa hoặc trùng `studentId` trả `AttendanceRosterMismatch` và rollback toàn batch.
- Update về `Present` giữ row, clear các field conditional và lưu `notes` nếu có.
- Unique student/date ngăn Student xuất hiện trong hai sheet cùng ngày.
- Student/group/user lifecycle conflict đúng contract.
- Validation `400`, auth `401/403/404`, race `409`.
- Teacher ngoài edit window bị từ chối; Admin/SuperAdmin vẫn sửa được ngày không tương lai.
- Audit đủ nhưng không chứa raw notes/body/secret.
- Sheet/records không bị maintenance cleanup.
- OpenAPI schema đúng.

### 13.3. Frontend

- Sidebar/route cho ba role; route từ chối user chưa xác thực, còn scope assignment được API kiểm tra.
- Teacher 0/1/nhiều nhóm và Admin/SuperAdmin hiển thị selector đúng.
- Admin/SuperAdmin chưa chọn group thì không gọi daily API.
- Filter mặc định expand; collapse/ARIA đúng.
- Ngày serialize theo local calendar.
- Search không dấu local đúng name/code/nickname; card bị ẩn vẫn giữ draft.
- Missing trả preview Present nhưng banner `Phiếu chưa được lưu`; không khẳng định đã điểm danh.
- Lưu lần đầu với toàn bộ Present vẫn gửi full roster và chuyển sang Saved.
- Conditional fields và validation theo status.
- Saved đổi vắng về Present gửi persisted Present, clear các field conditional, giữ notes theo nội dung người dùng và không delete.
- PUT gửi full roster tối đa 100; response server thay baseline/draft và dirty về 0.
- Dirty-change guard, double-submit protection và 401/403/409/network states.
- Chỉ `canRecover = true` mới hiện recovery flow cho Admin/SuperAdmin; warning/acknowledgement/reason và full records được gửi đúng contract.
- UI áp đúng state-specific permission (`Missing` dùng `canCreate`, `Saved` dùng `canEdit`); response cũ không ghi đè state khi filter đổi nhanh.
- UI hiển thị đúng policy 1–7 ngày của Teacher và read-only reason ngoài window.
- 100 cards render ổn với `trackBy`, normal vertical scroll và sticky save không che card cuối.
- Responsive/keyboard interaction quan trọng.

Verification cuối epic:

```powershell
dotnet build api/AdminPortal.slnx --no-restore
dotnet test api/tests/AdminPortal.UnitTests --no-restore
dotnet test api/tests/AdminPortal.IntegrationTests -c Release --no-restore
npm --prefix ui run build
npm --prefix ui run test:ci
npm --prefix ui run build -- --configuration iis
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\deploy\iis\build-iis-package.ps1
```

## 14. Các đợt phát triển và mã số

### `ATT-00` — Chốt nghiệp vụ và contract

- `ATT-P-01`: phân tích gap backend/frontend và yêu cầu nghiệp vụ.
- `ATT-P-02`: tạo plan cross-stack và mã các đợt phát triển.
- `ATT-P-03`: ghi nhận `ATT-DEC-01`–`09` và chốt `ATT-DEC-10` về storage model.
- `ATT-BE-00`: khóa schema, enum, authorization và OpenAPI draft.
- `ATT-FE-00`: khóa wireflow filter/card/save và UI quản trị assignment.

**DoD:** không còn quyết định ảnh hưởng schema/REST; acceptance criteria được duyệt.

### `ATT-01` — Nền tảng Teacher/Group/current roster

- `ATT-BE-01`: entity/config/migration/backfill Teacher.
- `ATT-BE-02`: group, responsible Teacher, Student current group, snapshot version và lifecycle rule.
- `ATT-FE-01`: trang quản trị nhóm/phân công, unassigned state và kiểm tra giới hạn 100 học sinh.
- `ATT-QA-01`: migration, current assignment authorization/cap/race và regression User/Student CRUD.

**DoD:** Admin/SuperAdmin tạo group và phân công hiện tại hoàn toàn qua UI, không seed/manual SQL; không có temporal assignment.

### `ATT-02` — Vertical slice đọc điểm danh

- `ATT-BE-03`: context/daily query, Missing preview/Saved snapshot và date/group scope.
- `ATT-FE-02`: models/service, navigation, route và role visibility.
- `ATT-FE-03`: filter panel, group logic, local search và card-list Missing/Saved read-only slice.
- `ATT-QA-02`: test Teacher A/B, Admin/SuperAdmin, 0/1/nhiều group và Vietnamese search.

**DoD:** ba role mở được trang phù hợp; không actor nào đọc student ngoài scope.

### `ATT-03` — Vertical slice lưu phiếu đầy đủ

- `ATT-BE-04`: create full sheet/records, full PUT, snapshot/sheet concurrency, transaction và audit.
- `ATT-FE-04`: editor card, Missing/Saved state, full-roster save, dirty state và sticky action.
- `ATT-QA-03`: test bốn trạng thái, persisted Present, snapshot, batch rollback và permission.

**DoD:** sheet Saved có đúng một persisted record cho mọi Student snapshot; Missing không bị hiểu thành Present.

### `ATT-04` — UX, edge cases và hardening

- `ATT-BE-05`: historical missing/recovery, index/query review, audit/privacy và lifecycle edge cases.
- `ATT-FE-05`: dirty-change guard, Admin historical-recovery flow, error/loading/empty states, responsive và accessibility.
- `ATT-QA-04`: stale update `409`, race, mobile/keyboard và regression auth/session.

**DoD:** stale snapshot/sheet bị từ chối, historical recovery không âm thầm sai roster, không leak scope và UI dùng ổn trên desktop/mobile.

### `ATT-05` — Tài liệu và release IIS

- `ATT-BE-06`: OpenAPI, README, requests và migration notes.
- `ATT-FE-06`: test hoàn thiện, copy/label và IIS environment verification.
- `ATT-QA-05`: full build/test, database upgrade rehearsal, package/checksum/content scan.

**DoD:** toàn bộ gate xanh; package IIS mới được build trên máy source và sẵn sàng chuyển sang máy đích.

## 15. Quyết định nghiệp vụ

| ID | Câu hỏi | Quyết định |
|---|---|---|
| `ATT-DEC-01` | “Buổi” là cả ngày hay một ca học? | **Đã chốt:** một record/student/ngày; API dùng `AbsentFullDay`/`AbsentHalfDay`, UI giữ nhãn nghiệp vụ. |
| `ATT-DEC-02` | Vắng 1/2 buổi có cần chọn sáng/chiều? | **Đã chốt:** bắt buộc `Morning` hoặc `Afternoon`. |
| `ATT-DEC-03` | 1-1 có nhiều block/ngày/cần giờ bắt đầu hoặc đồng thời với vắng? | **Đã chốt:** một block cố định 60 phút/ngày, không giờ bắt đầu và loại trừ với trạng thái vắng. |
| `ATT-DEC-04` | Phép/không phép áp dụng cho trạng thái nào? | **Đã chốt:** áp dụng cho các loại vắng và bắt buộc chọn rõ. |
| `ATT-DEC-05` | Teacher được sửa ngày quá khứ bao lâu? | **Đã chốt:** window 1–7 ngày, mặc định 7, Admin/SuperAdmin cấu hình riêng từng Teacher; hai role quản trị sửa mọi ngày không tương lai. |
| `ATT-DEC-06` | Giới hạn học sinh/nhóm và cách hiển thị? | **Đã chốt:** tối đa 100; UI hiển thị khoảng 8–10 card/viewport và scroll phần còn lại. |
| `ATT-DEC-07` | Có làm UI quản trị nhóm/phân công trong epic? | **Đã chốt:** có, tại `/student-groups`; không dùng seed/manual SQL. |
| `ATT-DEC-08` | Admin có lựa chọn tất cả nhóm trên attendance page? | **Đã chốt:** không; bắt buộc một group để tránh thao tác nhầm. |
| `ATT-DEC-09` | Retention lịch sử thay đổi attendance? | **Đã chốt:** attendance data giữ lâu dài; audit thay đổi giữ 90 ngày theo policy hiện tại. |
| `ATT-DEC-10` | Chỉ lưu exception hay lưu đủ phiếu daily gồm `Present`? | **Đã chốt:** full daily snapshot `attendance_sheets` + `attendance_records`, lưu cả `Present`; bỏ temporal assignment. |

`ATT-DEC-01` đến `ATT-DEC-10` đã được duyệt. `ATT-P-03` hoàn tất; `ATT-01` có thể bắt đầu sau khi OpenAPI draft khớp plan này.

## 16. Definition of Done toàn epic

- Teacher chỉ đọc/ghi group đang phụ trách và tuân thủ edit window; Admin/SuperAdmin toàn quyền trên ngày không tương lai.
- Admin/SuperAdmin thao tác mọi group nhưng phải chọn group rõ ràng.
- Missing không được hiểu là đã điểm danh; mỗi Saved sheet lưu snapshot đầy đủ gồm cả `Present`.
- Search tên/mã/nickname không phân biệt dấu và hoa thường.
- UI card-list, filter collapse mặc định expand và conditional group filter đúng yêu cầu.
- Có UI quản lý responsible Teacher/current Student group, không cần seed/manual SQL.
- Migration nâng cấp được database hiện có và rollback/backup procedure được ghi rõ.
- Validation, `ProblemDetails`, concurrency, audit/privacy và OpenAPI thống nhất.
- Backend/frontend/integration test đạt; build không warning mới.
- Agent memory, `tasks.md`, README và IIS release package được cập nhật.
