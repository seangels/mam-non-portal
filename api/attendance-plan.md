# Kế hoạch phát triển tính năng điểm danh

## 1. Thông tin kế hoạch

- **Epic:** `ATT`
- **Trạng thái:** `ATT-DEC-01` đến `ATT-DEC-09` đã chốt; còn `ATT-DEC-10` về mô hình lưu trữ trước khi implementation.
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
- Giáo viên chỉ xem và điểm danh học sinh thuộc nhóm mình phụ trách tại ngày được chọn.
- `Admin` và `SuperAdmin` được thao tác mọi nhóm.
- Mỗi giáo viên thường phụ trách một nhóm, nhưng mô hình vẫn hỗ trợ một giáo viên phụ trách nhiều nhóm.
- Mỗi nhóm có tối đa 100 học sinh. UI bố trí để nhìn rõ khoảng 8–10 card trong một viewport và cuộn xuống để xem phần còn lại.
- Màn hình chính hiển thị danh sách dạng card, tối ưu cho thao tác nhanh trên desktop, tablet và điện thoại.
- Bộ lọc mặc định mở, có thể collapse/expand; gồm ngày, nhóm theo điều kiện quyền và tìm kiếm tên/mã/nickname không phân biệt dấu.
- Trạng thái mặc định là `Có mặt`; database chỉ lưu các trường hợp ngoại lệ.
- Ngoại lệ gồm `Vắng nguyên buổi`, `Vắng 1/2 buổi`, `Học 1-1 (1 giờ)`, kèm thông tin phép/không phép khi phù hợp và ghi chú.

## 3. Ngoài phạm vi epic `ATT`

- Báo cáo tháng, thống kê chuyên cần và dashboard phân tích.
- Tính học phí/lương từ dữ liệu 1-1.
- Check-in theo thời điểm đến/về, QR, nhận diện khuôn mặt hoặc định vị.
- Import/export Excel.
- Thông báo tự động cho phụ huynh.
- Offline-first hoặc đồng bộ nhiều thiết bị khi mất mạng.
- Lịch học/ca học tùy biến phức tạp; nếu cần sẽ là feature riêng.

## 4. Quy ước nghiệp vụ đề xuất

### 4.1. Ngày điểm danh

- Điểm danh theo ngày nghiệp vụ `DateOnly`, format API `YYYY-MM-DD`.
- UI mặc định ngày hiện tại theo `Asia/Ho_Chi_Minh`; không dùng chuyển đổi UTC có thể làm lệch ngày.
- Backend dùng option `BusinessTimeZone = Asia/Ho_Chi_Minh` cùng `TimeProvider` để xác định ngày server; không lấy trực tiếp ngày UTC hoặc tin đồng hồ browser.
- Không cho ghi ngày tương lai.
- Mỗi Teacher có `attendanceEditWindowDays` từ 1 đến 7, mặc định 7 và do Admin/SuperAdmin cấu hình riêng.
- Window bao gồm hôm nay: giá trị 1 chỉ cho hôm nay; giá trị 7 cho hôm nay và 6 ngày lịch trước đó. Backend tính theo `serverDate` và `BusinessTimeZone`.
- Admin/SuperAdmin được sửa mọi ngày không nằm trong tương lai.

### 4.2. Trạng thái hiển thị và record lưu trữ

Response dùng bốn trạng thái:

- `Present`: có mặt, được tính toán khi không có exception row.
- `AbsentFullDay`: vắng cả ngày nghiệp vụ; UI vẫn hiển thị nhãn `Vắng nguyên buổi` theo ngôn ngữ người dùng.
- `AbsentHalfDay`: vắng nửa ngày.
- `OneToOneHour`: học 1-1 một đơn vị 60 phút.

Quy tắc lưu:

- Không bao giờ tạo attendance row cho `Present`.
- Chuyển một card về `Present` sẽ xóa exception row hiện hành trong transaction và ghi audit `Attendance.ExceptionCleared`.
- Mỗi học sinh tối đa một exception hiện hành cho một ngày trong phạm vi đầu tiên.
- `isExcused` bắt buộc với hai trạng thái vắng và không áp dụng cho `OneToOneHour`.
- `notes` được trim, nullable, tối đa 2.000 ký tự; có thể nhập cho mọi exception.
- `OneToOneHour` cố định `durationMinutes = 60` trong phạm vi này. Chưa lưu giờ bắt đầu và chưa hỗ trợ nhiều block trong cùng ngày cho đến khi nghiệp vụ xác nhận.
- `OneToOneHour` là trạng thái loại trừ với hai trạng thái vắng trong v1; một student không thể vừa vắng vừa có block 1-1 trong cùng ngày.
- `AbsentHalfDay` bắt buộc có `halfDayPart = Morning | Afternoon`.

### 4.3. Nhóm và phân công

- Một giáo viên thường có một nhóm; đây là quy ước vận hành, không phải hard limit trong database.
- Quan hệ Teacher–Group là nhiều-nhiều để hỗ trợ đúng trường hợp một giáo viên được assign nhiều nhóm.
- Một nhóm có thể có đồng giáo viên nếu nghiệp vụ cần.
- Một học sinh chỉ thuộc một nhóm tại cùng một ngày.
- Tối đa 100 học sinh có membership hiện hành trong một nhóm; API gán/chuyển student kiểm tra giới hạn trong cùng transaction để tránh race.
- Không seed nhóm hoặc assignment. Admin/SuperAdmin tạo nhóm và phân công qua UI/API.

### 4.4. Quyết định mô hình lưu trữ còn mở

Trao đổi sau khi lập draft tạo thêm `ATT-DEC-10`, cần chọn một trong hai trước khi tạo migration:

- **A — Exception-only:** giữ thiết kế hiện tại `attendance_exceptions`; không lưu `Present`, vì vậy cần assignment có `effective_from/effective_to` để tái dựng roster lịch sử.
- **B — Full daily snapshot (đang khuyến nghị):** dùng `attendance_sheets` + `attendance_records`, lưu cả `Present` cho tối đa 100 student mỗi group/ngày; có thể dùng `students.group_id` và teacher/group assignment hiện tại thay cho khoảng hiệu lực nếu chấp nhận rằng ngày chưa tạo sheet không thể tái dựng sau khi cơ cấu nhóm đổi.

Các section schema/REST bên dưới hiện mô tả phương án A và sẽ được thay thế nếu duyệt phương án B. Không bắt đầu `ATT-01` khi `ATT-DEC-10` chưa chốt.

## 5. Thiết kế dữ liệu

Tất cả khóa chính dùng UUID; thời điểm audit dùng UTC `timestamptz`; ngày hiệu lực dùng PostgreSQL `date`.

### 5.1. `teachers`

Profile nghiệp vụ một-một với tài khoản `users`:

```text
id          uuid PK
user_id     uuid NOT NULL UNIQUE FK -> users.id RESTRICT
attendance_edit_window_days smallint NOT NULL DEFAULT 7
created_at  timestamptz
updated_at  timestamptz
```

Quy tắc:

- Không lặp lại email, họ tên, status hoặc password; đọc từ `users`.
- Tạo profile trong cùng transaction khi tạo/chuyển user sang role `Teacher`.
- Migration backfill profile cho user `Teacher` đã tồn tại; đây là chuyển đổi dữ liệu, không phải seed tài khoản.
- Không xóa cứng profile để bảo toàn lịch sử.
- `attendance_edit_window_days` có check constraint từ 1 đến 7; Admin/SuperAdmin có thể cấu hình riêng từng Teacher và mọi thay đổi phải audit.
- Nếu một user từng là Teacher được chuyển về role `Teacher`, service tái sử dụng profile cũ thay vì insert profile trùng `user_id`.
- Đổi role hoặc xóa user Teacher đang có assignment hiệu lực trả `409`; Admin phải kết thúc/chuyển assignment rõ ràng trước.

### 5.2. `student_groups`

```text
id          uuid PK
code        varchar(50)
name        varchar(200)
status      varchar(30)       -- Active | Inactive
created_at  timestamptz
updated_at  timestamptz
deleted_at  timestamptz null
```

- `code` unique trên record chưa soft-delete.
- Nhóm inactive chỉ dùng để xem lịch sử; không nhận assignment/attendance mới.

### 5.3. `teacher_group_assignments`

```text
id              uuid PK
teacher_id      uuid FK -> teachers.id
group_id        uuid FK -> student_groups.id
effective_from  date
effective_to    date null       -- exclusive
created_by      uuid FK -> users.id
created_at      timestamptz
updated_by      uuid FK -> users.id
updated_at      timestamptz
```

- Khoảng hiệu lực nửa mở `[effective_from, effective_to)`; khi chuyển từ ngày D, assignment cũ kết thúc tại D và assignment mới bắt đầu tại D.
- Nếu có `effective_to` thì phải thỏa `effective_from < effective_to`.
- Không cho khoảng hiệu lực của cùng Teacher–Group chồng nhau.
- Unique partial assignment đang mở trên `(teacher_id, group_id)` khi `effective_to IS NULL`.
- Assignment service dùng PostgreSQL transaction advisory lock theo teacher trước khi validate/ghi để các request đồng thời không tạo khoảng chồng nhau. Không cần primary group trong v1; Teacher có nhiều group phải chọn rõ.
- Index query theo ngày: `(teacher_id, effective_from, effective_to, group_id)` và `(group_id, effective_from, effective_to, teacher_id)`.

### 5.4. `student_group_assignments`

```text
id              uuid PK
student_id      uuid FK -> students.id
group_id        uuid FK -> student_groups.id
effective_from  date
effective_to    date null       -- exclusive
created_by      uuid FK -> users.id
created_at      timestamptz
updated_by      uuid FK -> users.id
updated_at      timestamptz
```

- Một student không được có hai khoảng assignment chồng nhau.
- Unique partial trên `student_id` khi `effective_to IS NULL` để chỉ có một nhóm hiện hành.
- Khoảng dùng `[effective_from, effective_to)` và `effective_from < effective_to` khi có ngày kết thúc.
- Chuyển nhóm chạy trong một transaction có PostgreSQL advisory lock theo student: kết thúc assignment cũ tại D rồi tạo assignment mới từ D.
- Lịch sử membership là bắt buộc vì hệ thống không lưu row `Present`; roster lịch sử phải được tái dựng đúng sau khi student đổi nhóm.
- Index query theo ngày: `(group_id, effective_from, effective_to, student_id)` và `(student_id, effective_from, effective_to)`.

### 5.5. `attendance_exceptions`

```text
id                  uuid PK
student_id          uuid FK -> students.id
group_id            uuid FK -> student_groups.id
attendance_date     date
exception_type      varchar(40)
half_day_part       varchar(20) null
is_excused          boolean null
duration_minutes    integer null
notes               varchar(2000) null
version             integer
created_by_user_id  uuid FK -> users.id
updated_by_user_id  uuid FK -> users.id
created_at          timestamptz
updated_at          timestamptz
```

- Unique `(student_id, attendance_date)`.
- Index đọc chính `(group_id, attendance_date, student_id)`.
- Check constraints đồng bộ validation theo exception type và bảo đảm `version >= 1`.
- `group_id` là snapshot nhóm được xác minh từ membership tại `attendance_date`, không tin giá trị client tùy ý.
- `version` tăng sau mỗi update và dùng optimistic concurrency; xung đột trả `409`.
- Không soft-delete bảng current-state này. Reset về `Present` hard-delete exception trong transaction sau khi audit.
- Attendance exception là dữ liệu nghiệp vụ, không thuộc cleanup audit/session hiện tại và không tự động bị xóa.

## 6. Authorization

| Hành động | SuperAdmin | Admin | Teacher |
|---|---:|---:|---:|
| Xem nhóm khả dụng | Tất cả | Tất cả | Chỉ assignment có hiệu lực theo ngày |
| Xem danh sách điểm danh | Tất cả | Tất cả | Chỉ student thuộc nhóm được giao theo ngày |
| Ghi/xóa exception | Tất cả | Tất cả | Chỉ student thuộc nhóm được giao theo ngày |
| CRUD nhóm/phân công | Có | Có | Không |
| CRUD user/student cũ | Theo contract hiện tại | Theo contract hiện tại | Không |

Quy tắc bắt buộc:

- Không nhận `teacherId` từ client để quyết định scope; resolve từ current actor.
- Teacher gửi `groupId` không được assign trả `403`.
- Student/attendance ID ngoài scope trả `404` để hạn chế dò dữ liệu.
- Mọi kiểm tra lịch sử dùng assignment hiệu lực tại ngày điểm danh, không dùng assignment hiện tại.
- Không được ghi nếu student không thuộc group tại ngày được chọn hoặc ngày nằm ngoài khoảng membership. Student bị inactive/deleted sau ngày lịch sử không làm mất snapshot; quyền sửa lịch sử vẫn theo date policy và assignment tại ngày đó.
- Đổi role/xóa Teacher, inactive/xóa Student hoặc inactive/xóa Group khi còn assignment mở trả `409`; Admin phải kết thúc/chuyển assignment trước, không tự đóng ngầm.
- Daily query lịch sử lấy membership làm nguồn và dùng `IgnoreQueryFilters` có kiểm soát để Student/Group đã inactive hoặc soft-delete sau đó vẫn xuất hiện đúng tại ngày từng thuộc nhóm. Chỉ ngày nằm trong membership mới được đọc/ghi theo policy ngày.
- UI guard/menu chỉ là UX; application service là nơi thực thi quyền.

## 7. REST contract đề xuất

### 7.1. Quản lý giáo viên, nhóm và assignment

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

POST   /api/v1/student-groups/{groupId}/teacher-assignments
PUT    /api/v1/teacher-group-assignments/{assignmentId}/end-date

POST   /api/v1/student-group-assignments/move
PUT    /api/v1/student-group-assignments/{assignmentId}/end-date
```

- Teacher assignment request chứa `teacherId`, `effectiveFrom`; endpoint end-date nhận body `{ "effectiveTo": "YYYY-MM-DD" }` và không xóa lịch sử.
- Student `move` nhận `studentId`, `targetGroupId`, `effectiveFrom`; service kết thúc membership cũ và tạo membership mới atomically. Endpoint end-date dùng cho trường hợp rời nhóm nhưng chưa có nhóm mới.
- Mỗi response assignment có `assignmentId`; cập nhật lịch sử dùng ID này, không suy luận từ cặp teacher/group có thể được gán lại nhiều lần.
- Không dùng DELETE body vì client/proxy/IIS có thể xử lý không đồng nhất.
- `GET /teachers` trả `{ items, pagination }`, hỗ trợ `search`, `status`, `page`, `pageSize`, sort whitelist; chỉ trả profile có user role `Teacher` theo filter nghiệp vụ và không trả thông tin authentication.
- `PUT /teachers/{id}/attendance-policy` nhận `{ "attendanceEditWindowDays": 1..7 }`; Admin/SuperAdmin đều được cấu hình và response trả policy mới.
- `GET /student-groups` hỗ trợ `search`, `status`, pagination và sort whitelist.
- Filter `unassigned=true` luôn đi kèm `asOfDate`; nghĩa là không có assignment hiệu lực tại ngày đó, không chỉ kiểm tra row đang mở.
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

- Teacher chỉ nhận groups được assign tại `date`.
- Admin/SuperAdmin nhận mọi group phù hợp với trạng thái/ngày.
- `studentCount` là số membership hiệu lực tại `date`; daily history có thể bao gồm student đã inactive/deleted sau ngày đó.
- `serverDate`, `canEdit`, `readOnlyReason` giúp UI hiển thị read-only đúng policy; backend vẫn tự kiểm tra lại khi mutation.
- Context trả thêm `attendanceEditWindowDays` của Teacher để UI giải thích window; backend tính `canEdit` bằng ngày server và không tin giá trị client.
- Khi xem ngày lịch sử, context vẫn trả Group từng có assignment/membership tại ngày đó kể cả hiện đã inactive/soft-delete.
- UI tự xác định hiển thị group filter dựa trên role và số group; backend không tin quyết định ẩn/hiện của UI.

### 7.3. Danh sách card theo ngày

```http
GET /api/v1/attendance/daily
    ?date=2026-08-11
    &groupId={uuid}
    &search=nguyen
```

- `groupId` bắt buộc với Admin/SuperAdmin và Teacher có nhiều group.
- Teacher có đúng một group có thể bỏ `groupId`; backend tự resolve.
- Admin/SuperAdmin bắt buộc chọn một group, không có lựa chọn `Tất cả nhóm`, để tránh thao tác nhầm và giữ danh sách nhỏ.
- Search server-side trên `studentCode`, `fullName`, `nickName`, không phân biệt hoa thường hoặc dấu tiếng Việt.
- Không phân trang trong v1 vì luôn scope một group và giới hạn nghiệp vụ là 100 học sinh. API trả toàn bộ roster của group; UI cuộn danh sách card.

Response đề xuất:

```json
{
  "date": "2026-08-11",
  "group": {
    "id": "00000000-0000-0000-0000-000000000000",
    "code": "N1",
    "name": "Nhóm 1"
  },
  "summary": {
    "rosterTotal": 10,
    "present": 8,
    "absent": 2,
    "oneToOne": 0
  },
  "matchedCount": 10,
  "items": [
    {
      "studentId": "00000000-0000-0000-0000-000000000000",
      "studentCode": "HS001",
      "fullName": "Nguyễn Văn An",
      "nickName": "Bé An",
      "status": "Present",
      "halfDayPart": null,
      "isExcused": null,
      "durationMinutes": null,
      "notes": null,
      "version": null,
      "updatedAt": null
    }
  ]
}
```

`summary` luôn tính trên toàn roster trước search; `matchedCount` và `items` phản ánh kết quả sau search.

### 7.4. Lưu các card đã thay đổi

```http
PUT /api/v1/attendance/daily/2026-08-11
```

Request:

```json
{
  "groupId": "00000000-0000-0000-0000-000000000000",
  "changes": [
    {
      "studentId": "00000000-0000-0000-0000-000000000000",
      "status": "AbsentHalfDay",
      "halfDayPart": "Morning",
      "isExcused": true,
      "durationMinutes": null,
      "notes": "Gia đình đã báo trước",
      "expectedVersion": null
    }
  ]
}
```

Quy tắc:

- Chỉ gửi card dirty; không gửi toàn bộ roster.
- `status = Present` có nghĩa clear exception, không insert row Present.
- Batch chạy all-or-nothing trong một transaction.
- `expectedVersion` phải khớp record hiện tại; stale update/delete trả `409 Conflict` và UI yêu cầu reload.
- Tạo exception: `expectedVersion = null` và server phải đang không có row. Update/clear: bắt buộc version dương khớp row hiện tại.
- `Present + null` chỉ no-op nếu server cũng không có exception; nếu vừa có actor khác tạo row thì trả `409`.
- `expectedVersion` có giá trị nhưng row đã bị clear cũng trả `409`.
- Conflict batch trả `ProblemDetails` với extension `conflicts` chứa `studentId` và `currentVersion`; validation field dùng path như `changes[0].halfDayPart` để UI map đúng card.
- Integer version ngăn ghi đè exception đang tồn tại. Chuỗi create→clear hoàn tất giữa hai snapshot (ABA) không được phát hiện trong v1 vì current-state table không giữ tombstone; mọi create/update/clear vẫn có audit.
- Response trả snapshot daily mới để UI dùng server làm source of truth.
- Giới hạn đề xuất tối đa 100 changes/request.

Validation theo status:

| Status | `halfDayPart` | `isExcused` | `durationMinutes` |
|---|---|---|---|
| `Present` | null | null | null |
| `AbsentFullDay` | null | bắt buộc | null |
| `AbsentHalfDay` | bắt buộc | bắt buộc | null |
| `OneToOneHour` | null | null | `60` |

## 8. Search tiếng Việt không dấu

Phạm vi một nhóm nhỏ cho phép giải pháp đơn giản và dễ vận hành:

1. Query PostgreSQL áp authorization, membership theo ngày, status và group trước.
2. Materialize roster đã scope ở application service.
3. Dùng một `VietnameseTextNormalizer` chung cho query và ba field `studentCode`, `fullName`, `nickName`: lowercase invariant, Unicode decomposition, bỏ combining marks, đổi `đ/Đ` thành `d`, trim/collapse whitespace.
4. Contains-match trên chuỗi normalized.

Giải pháp này vẫn là server-side search, không tải dữ liệu ngoài quyền về browser, không yêu cầu quyền cài PostgreSQL extension trên máy IIS và đủ cho tối đa 100 học sinh/nhóm. Nếu phạm vi sau này vượt giới hạn này, bổ sung persisted normalized column và GIN `pg_trgm` trong một migration riêng.

Test bắt buộc: `nguyen`, `Nguyễn`, `NGUYEN` trả cùng kết quả; mã và nickname cũng tìm đúng.

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
- Search placeholder: `Tên, mã học sinh, tên gọi`; trim, debounce khoảng 300 ms, bỏ request trùng và hủy/ignore response cũ.
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
- Trạng thái dirty/đang lưu/đã lưu/lỗi.

Vùng danh sách có chiều cao/responsive spacing để thường hiển thị rõ khoảng 8–10 card cùng lúc. Khi group có nhiều hơn, người dùng cuộn dọc; không cắt dữ liệu, không giới hạn 10 card và không dùng pagination ở màn hình v1.

Khi chọn `Có mặt`, UI xóa dữ liệu exception cũ khỏi form change. Dùng sticky action `Lưu thay đổi (n)` và chỉ gửi card dirty; không autosave ngay khi chạm để giảm ghi nhầm trên thiết bị cảm ứng.

Summary đầu danh sách:

```text
10 học sinh · Có mặt 8 · Vắng 2 · Học 1-1 0
```

### 9.4. Error, loading và accessibility

- State riêng cho loading context, loading list và saving batch.
- `401` dùng interceptor/session restore hiện có.
- `canEdit = false` vẫn hiển thị snapshot nhưng khóa controls/save và nêu `readOnlyReason`.
- Khi assignment bị thu hồi, `403` xóa snapshot cũ và reload context.
- `409` báo dữ liệu đã thay đổi và cho reload, không silent overwrite.
- Validation map đúng card/field; có trace ID khi cần hỗ trợ.
- Empty state riêng cho chưa phân công, nhóm rỗng, search rỗng và API lỗi.
- Mobile controls full width, touch target đủ lớn; sticky save không che card cuối.
- Radio/segmented controls có accessible label chứa tên học sinh; tab order theo card và focus lỗi đầu tiên.
- Dirty guard bao phủ đổi filter, chuyển route/sidebar và `beforeunload`; response cũ không được ghi đè state sau khi ngày/group/search đổi nhanh.

## 10. Quản trị nhóm/phân công qua UI

Tính năng chưa vận hành hoàn chỉnh nếu chỉ có schema/API. `ATT-01` phải cung cấp UI cho Admin/SuperAdmin:

- Tạo/sửa/inactive nhóm.
- Gán/gỡ giáo viên với ngày hiệu lực.
- Gán/chuyển học sinh với ngày hiệu lực.
- Danh sách cảnh báo Teacher/Student chưa được phân công.
- Hiển thị số lượng hiện tại trên tối đa 100; từ chối gán/chuyển học sinh thứ 101 bằng validation rõ ràng.

Vị trí UI đã chốt: thêm trang `/student-groups` và item sidebar `Nhóm`; dùng picker từ user role Teacher và student hiện có, không tạo duplicate tài khoản/học sinh. Trang quản lý Teacher cho Admin/SuperAdmin cấu hình window sửa điểm danh từ 1 đến 7 ngày.

## 11. Audit, logging và retention

Audit tối thiểu:

- `TeacherProfile.Created`.
- `Teacher.AttendancePolicyUpdated`.
- `Group.Created`, `Group.Updated`, `Group.Deleted`.
- `TeacherGroup.Assigned`, `TeacherGroup.Unassigned`.
- `StudentGroup.Assigned`, `StudentGroup.Moved`, `StudentGroup.Unassigned`.
- `Attendance.ExceptionCreated`, `Attendance.ExceptionUpdated`, `Attendance.ExceptionCleared`.

Audit attendance chứa actor, IDs, ngày, trạng thái, phép/không phép, duration và version. Mặc định chỉ ghi `notesChanged`, không nhân đôi raw notes vào audit/application log. Không log request body.

Attendance exception/record là dữ liệu nghiệp vụ và được giữ lâu dài, không thuộc cleanup tự động. Audit thay đổi tạm giữ 90 ngày theo policy hiện tại.

## 12. Migration và tương thích dữ liệu hiện có

Migration `AddAttendanceFoundation`:

1. Tạo `teachers`, `student_groups`, hai bảng assignment và `attendance_exceptions`.
2. Tạo FK `RESTRICT`, check constraint, partial unique/index và concurrency version.
3. Backfill `teachers` cho user role `Teacher` hiện có.
4. Không tự tạo group, không tự assign student và không tạo attendance record.
5. Admin dùng UI để xử lý teacher/student chưa assign.
6. Sinh Designer/snapshot và chạy `has-pending-model-changes`.
7. Test migration cả database rỗng và database đang có user/student.

Release phải cập nhật OpenAPI, `api/README.md`, `api/requests.http`, agent memory và IIS package sau khi tất cả test đạt.

## 13. Test plan

### 13.1. Backend unit

- Date-range assignment không chồng nhau.
- Lifecycle Teacher user/profile và conflict khi còn assignment.
- Validation/configuration `attendanceEditWindowDays` từ 1 đến 7.
- Validation từng exception status.
- Authorization theo role/group/date.
- Vietnamese normalization và contains search.
- Resolve group khi Teacher có 0/1/nhiều nhóm.
- Concurrency version và batch all-or-nothing.
- Business date/timezone quanh ranh giới UTC và local day.
- Window 1 ngày/7 ngày và policy riêng của hai Teacher khác nhau.

### 13.2. PostgreSQL integration

- Migration/backfill từ database hiện có.
- Hai request assignment đồng thời không tạo khoảng overlap nhờ transaction advisory lock.
- Không có exception row thì list trả `Present`.
- Batch tạo/sửa/clear đúng số row và summary.
- Teacher A không xem/sửa student nhóm Teacher B.
- Teacher nhiều nhóm lọc đúng; Admin/SuperAdmin thao tác mọi nhóm.
- Lịch sử đúng sau khi student/teacher đổi nhóm.
- Search `nguyen` tìm được `Nguyễn`; code/nickname đúng.
- Student inactive/deleted hoặc ngoài membership không được ghi.
- Validation `400`, auth `401/403/404`, stale version/race `409`.
- Teacher ngoài edit window bị từ chối; Admin/SuperAdmin vẫn sửa được ngày không tương lai.
- Audit đủ nhưng không chứa raw notes/body/secret.
- OpenAPI schema đúng.

### 13.3. Frontend

- Sidebar/route cho ba role; route từ chối user chưa xác thực, còn scope assignment được API kiểm tra.
- Teacher 0/1/nhiều nhóm và Admin/SuperAdmin hiển thị selector đúng.
- Admin/SuperAdmin chưa chọn group thì không gọi daily API.
- Filter mặc định expand; collapse/ARIA đúng.
- Ngày serialize theo local calendar.
- Search trim/debounce/cancel đúng query.
- Không có exception row thì API trả computed `status = Present` và UI hiển thị `Có mặt`.
- Conditional fields và validation theo status.
- Chỉ gửi card dirty; `Present` sinh change clear chứ không tạo row.
- Dirty-change guard, double-submit protection và 401/403/409/network states.
- `canEdit = false` vẫn xem được snapshot nhưng không mutation; response cũ không ghi đè state khi filter đổi nhanh.
- UI hiển thị đúng policy 1–7 ngày của Teacher và read-only reason ngoài window.
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

### `ATT-01` — Nền tảng Teacher/Group/Assignment

- `ATT-BE-01`: entity/config/migration/backfill Teacher.
- `ATT-BE-02`: group, teacher-group, student-group API và lifecycle rule.
- `ATT-FE-01`: trang quản trị nhóm/phân công, unassigned state và kiểm tra giới hạn 100 học sinh.
- `ATT-QA-01`: migration test, assignment authorization và regression User/Student CRUD.

**DoD:** Admin/SuperAdmin tạo group và phân công hoàn toàn qua UI, không seed/manual SQL; lịch sử assignment đúng.

### `ATT-02` — Vertical slice đọc điểm danh

- `ATT-BE-03`: context/daily query, computed Present, date/group scope và search không dấu.
- `ATT-FE-02`: models/service, navigation, route và role visibility.
- `ATT-FE-03`: filter panel, group logic, search và card-list read-only.
- `ATT-QA-02`: test Teacher A/B, Admin/SuperAdmin, 0/1/nhiều group và Vietnamese search.

**DoD:** ba role mở được trang phù hợp; không actor nào đọc student ngoài scope.

### `ATT-03` — Vertical slice ghi điểm danh

- `ATT-BE-04`: batch upsert/clear exception, validation, optimistic concurrency, transaction và audit.
- `ATT-FE-04`: editor card, conditional fields, dirty state và sticky save.
- `ATT-QA-03`: test bốn trạng thái, không lưu Present, batch rollback và permission.

**DoD:** lưu/clear hoạt động end-to-end; database chỉ có exception rows.

### `ATT-04` — UX, edge cases và hardening

- `ATT-BE-05`: index/query review, audit/privacy, lifecycle và edge cases lịch sử.
- `ATT-FE-05`: dirty-change guard, error/loading/empty states, responsive và accessibility.
- `ATT-QA-04`: stale update `409`, race, mobile/keyboard và regression auth/session.

**DoD:** stale update trên exception hiện hành bị từ chối, không leak scope, UI dùng ổn trên desktop/mobile và audit không chứa dữ liệu không cần thiết.

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
| `ATT-DEC-10` | Chỉ lưu exception hay lưu đủ phiếu daily gồm `Present`? | **Chờ chốt:** A — exception-only + temporal assignment; B — full daily snapshot, đang khuyến nghị. |

`ATT-DEC-01` đến `ATT-DEC-09` đã được duyệt. Không bắt đầu `ATT-01` trước khi `ATT-DEC-10` được chốt vì quyết định này thay đổi trực tiếp schema, migration và REST contract.

## 16. Definition of Done toàn epic

- Teacher chỉ đọc/ghi đúng student thuộc assignment tại ngày điểm danh.
- Admin/SuperAdmin thao tác mọi group nhưng phải chọn group rõ ràng.
- Storage tuân theo `ATT-DEC-10`: phương án A không có row `Present`; phương án B lưu snapshot đầy đủ gồm `Present`.
- Search tên/mã/nickname không phân biệt dấu và hoa thường.
- UI card-list, filter collapse mặc định expand và conditional group filter đúng yêu cầu.
- Có UI quản lý Teacher/Group/Student assignment, không cần seed/manual SQL.
- Migration nâng cấp được database hiện có và rollback/backup procedure được ghi rõ.
- Validation, `ProblemDetails`, concurrency, audit/privacy và OpenAPI thống nhất.
- Backend/frontend/integration test đạt; build không warning mới.
- Agent memory, `tasks.md`, README và IIS release package được cập nhật.
