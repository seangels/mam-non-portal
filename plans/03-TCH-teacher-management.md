# Kế hoạch phát triển tính năng quản lý thông tin giáo viên

## 1. Thông tin kế hoạch

- **Epic:** `TCH`
- **Thứ tự:** `03`
- **Trạng thái:** bản nháp để review; chưa triển khai cho đến khi chốt các quyết định `TCH-DEC-*` ở mục 16.
- **Ngày lập:** 2026-08-11.
- **Phạm vi:** .NET 10 REST API, PostgreSQL 17 và Angular/DevExtreme UI.
- **Contract nền:** [`01-BASE-admin-portal.md`](01-BASE-admin-portal.md) và [`02-ATT-attendance.md`](02-ATT-attendance.md).

Mỗi đợt phát triển dùng mã `TCH-00` đến `TCH-06`. Task backend, frontend và kiểm thử lần lượt dùng hậu tố `TCH-BE-*`, `TCH-FE-*`, `TCH-QA-*`. Mã đã cấp không tái sử dụng; task phát sinh thêm hậu tố, ví dụ `TCH-BE-03A`.

Production build, đóng gói và deploy IIS không nằm trong luồng mặc định của epic này. Chỉ thực hiện khi người dùng gọi riêng `$gv-portal-production`.

## 2. Hiện trạng cần kế thừa

- `users` đang là nguồn dữ liệu duy nhất cho email đăng nhập, mật khẩu, họ tên, số điện thoại, role, trạng thái tài khoản và session.
- `teachers` hiện liên kết 1-1 với `users`, chỉ lưu thời hạn được sửa điểm danh `attendanceEditWindowDays` và timestamps.
- API hiện có danh sách/chi tiết giáo viên và endpoint cập nhật chính sách điểm danh; UI đang đặt phần chính sách này trong trang `Nhóm`.
- Tạo User có role `Teacher` sẽ tạo hoặc tái sử dụng Teacher profile. Teacher profile phải được giữ lại khi User đổi role hoặc bị soft-delete để không phá lịch sử điểm danh.
- Một Teacher có thể phụ trách nhiều nhóm. Phân công hiện tại được lưu tại `student_groups.responsible_teacher_id`.
- Đổi họ tên Teacher đang phụ trách nhóm phải tăng `snapshotVersion` của các nhóm liên quan; phiếu điểm danh đã lưu giữ nguyên tên snapshot cũ.
- Admin và SuperAdmin quản lý Teacher. Teacher không được truy cập các API quản trị giáo viên.

## 3. Mục tiêu nghiệp vụ

- Có mục `Giáo viên` riêng trên sidebar cho `Admin` và `SuperAdmin`.
- Xem danh sách, tìm kiếm, lọc, phân trang, sắp xếp và xem chi tiết giáo viên.
- Tạo, chỉnh sửa, đổi mật khẩu và soft-delete giáo viên qua một luồng rõ ràng.
- Quản lý cùng lúc thông tin tài khoản và hồ sơ nghề nghiệp nhưng không lưu trùng dữ liệu giữa `users` và `teachers`.
- Xem các nhóm đang phụ trách; việc phân công/gỡ nhóm vẫn chỉ thực hiện tại màn hình `Nhóm`.
- Giữ nguyên các invariant về authorization, session revoke, group snapshot và lịch sử điểm danh.
- Toàn bộ nội dung người dùng nhìn thấy trên UI chỉ sử dụng tiếng Việt.

## 4. Ngoài phạm vi v1

- Ảnh đại diện và upload tài liệu/hồ sơ.
- CCCD/hộ chiếu, mã số thuế, BHXH, tài khoản ngân hàng, lương và dữ liệu sức khỏe.
- Hợp đồng lao động, chấm công nhân sự và tính lương.
- Lịch sử công tác/phân công nhóm theo `effectiveFrom`/`effectiveTo`.
- Import/export Excel.
- Teacher tự chỉnh sửa hồ sơ cá nhân.
- Danh sách cựu giáo viên hoặc khôi phục Teacher đã xóa trong UI quản trị thông thường.

Các mục trên cần epic riêng vì có thêm yêu cầu về bảo mật dữ liệu, retention, upload/storage hoặc quy trình duyệt.

## 5. Ranh giới dữ liệu và nguồn sự thật

### 5.1. Dữ liệu thuộc `User`

- `email`
- `passwordHash`
- `fullName`
- `phoneNumber`
- `role`
- `status`
- lockout, session và các trường bảo mật tài khoản

Teacher API được phép project và cập nhật các field phù hợp qua một application use case dùng chung, nhưng không sao chép các field này sang bảng `teachers`.

### 5.2. Dữ liệu thuộc `Teacher`

Phạm vi v1 đề xuất:

| Field API | Kiểu | Bắt buộc | Quy tắc đề xuất |
|---|---|---:|---|
| `teacherCode` | string | Có | Server tự sinh, bất biến, unique toàn cục, không tái sử dụng |
| `dateOfBirth` | `DateOnly?` | Không | Không lớn hơn ngày nghiệp vụ hiện tại |
| `gender` | `Gender?` | Không | `Male`, `Female`, `Other` |
| `address` | string? | Không | Trim, tối đa 500 ký tự |
| `qualification` | string? | Không | Trình độ, trim, tối đa 200 ký tự |
| `specialization` | string? | Không | Chuyên môn, trim, tối đa 200 ký tự |
| `startDate` | `DateOnly?` | Không | Ngày vào làm; validation chờ `TCH-DEC-10` |
| `note` | string? | Không | Trim, tối đa 2.000 ký tự |
| `attendanceEditWindowDays` | integer | Có | Từ 1 đến 7, mặc định 7 |
| `version` | integer | Có | Bắt đầu từ 1, tăng sau mỗi lần cập nhật aggregate |

Không thêm `employmentStatus` trong v1. UI phải gọi `User.status` là **Trạng thái tài khoản** để không nhầm với trạng thái nhân sự. Nếu cần `Đang làm việc/Nghỉ phép/Đã nghỉ việc`, phải chốt thêm transition, tác động đến đăng nhập, session và nhóm phụ trách trước khi mở rộng schema.

### 5.3. Quan hệ nhóm

- Teacher detail chỉ đọc danh sách nhóm đang phụ trách.
- Gán/gỡ Teacher tiếp tục qua `PUT /api/v1/student-groups/{groupId}/responsible-teacher`.
- Không nhận `groupIds` trong create/update Teacher để tránh hai nguồn mutation.
- Không thêm `effectiveFrom`/`effectiveTo`; lịch sử đã có trong snapshot của phiếu điểm danh.

## 6. Schema PostgreSQL đề xuất

Mở rộng bảng `teachers`:

```text
teacher_code                 varchar(50)  not null
date_of_birth                date         null
gender                       varchar(20)  null
address                      varchar(500) null
qualification                varchar(200) null
specialization               varchar(200) null
start_date                   date         null
note                         varchar(2000) null
version                      integer      not null default 1
```

Giữ nguyên:

```text
id
user_id
attendance_edit_window_days
created_at
updated_at
```

Index và constraint:

- Unique toàn cục trên `teacher_code`; Teacher profile lịch sử không bị xóa nên mã không tái sử dụng.
- Check `attendance_edit_window_days BETWEEN 1 AND 7`.
- Check ngày sinh hợp lệ khi có giá trị.
- Enum lưu dạng string theo convention hiện tại.
- Không index mọi field; chỉ bổ sung index theo filter đã chứng minh cần thiết sau query-plan test.

Mã giáo viên đề xuất dùng format `GV000001`, `GV000002`, ... và sinh bằng database sequence để an toàn khi có nhiều request đồng thời. Migration phải backfill mã cho Teacher profile hiện có trước khi chuyển column sang `NOT NULL`.

## 7. Authorization

| Hành động | SuperAdmin | Admin | Teacher |
|---|:---:|:---:|:---:|
| Xem danh sách/chi tiết Teacher | Có | Có | Không |
| Tạo/sửa/soft-delete Teacher | Có | Có | Không |
| Đổi mật khẩu Teacher | Có | Có | Không |
| Xem nhóm đang phụ trách | Có | Có | Không |
| Phân công/gỡ nhóm | Có | Có | Không |

Quy tắc bổ sung:

- Role của resource luôn là `Teacher`; create/update Teacher không nhận field `role`.
- Admin không thể dùng Teacher endpoint để tạo hoặc nâng quyền Admin/SuperAdmin.
- Mọi authorization phải kiểm tra tại API; ẩn navigation/action trên UI chỉ là UX.
- Teacher self-service để ngoài v1. Nếu bổ sung sau, dùng endpoint/DTO hạn chế riêng, không tái sử dụng manager `PUT`.

## 8. REST API contract đề xuất

### 8.1. Endpoints

```http
GET    /api/v1/teachers
POST   /api/v1/teachers
GET    /api/v1/teachers/{teacherId}
PUT    /api/v1/teachers/{teacherId}
DELETE /api/v1/teachers/{teacherId}?expectedVersion={version}

PUT    /api/v1/users/{userId}/password
```

Endpoint hiện có được giữ tạm để tương thích trong giai đoạn chuyển đổi:

```http
PUT /api/v1/teachers/{teacherId}/attendance-policy
```

UI mới cập nhật policy qua full Teacher `PUT`. Endpoint cũ phải dùng chung validation/concurrency/audit service và được đánh dấu deprecated trong OpenAPI sau khi trang policy cũ được gỡ.

### 8.2. List query

```text
page=1
pageSize=20
search=
status=Active|Inactive|Locked
gender=Male|Female|Other
groupId={uuid}
unassigned=true|false
startDateFrom=YYYY-MM-DD
startDateTo=YYYY-MM-DD
sortBy=teacherCode|fullName|email|status|startDate|attendanceEditWindowDays|responsibleGroupCount|createdAt|updatedAt
sortOrder=asc|desc
```

Quy tắc:

- `pageSize` từ 1 đến 100.
- `search` tìm theo mã giáo viên, họ tên, email, số điện thoại và chuyên môn.
- Nếu gửi đồng thời `groupId` và `unassigned=true`, trả `400 ValidationFailed`.
- Sort dùng whitelist, không nhận biểu thức động; luôn thêm `id` làm tie-break để phân trang ổn định.
- Response giữ contract chung `{ items, pagination }`.

### 8.3. Response models

```text
TeacherListItemResponse {
  id: uuid
  userId: uuid
  teacherCode: string
  fullName: string
  email: string
  phoneNumber: string | null
  status: UserStatus
  gender: Gender | null
  startDate: date | null
  attendanceEditWindowDays: integer
  responsibleGroupCount: integer
  createdAt: datetime
  updatedAt: datetime
  version: integer
}
```

```text
TeacherDetailResponse {
  ...TeacherListItemResponse
  dateOfBirth: date | null
  address: string | null
  qualification: string | null
  specialization: string | null
  note: string | null
  responsibleGroups: TeacherGroupSummaryResponse[]
}
```

```text
TeacherGroupSummaryResponse {
  id: uuid
  code: string
  name: string
  status: GroupStatus
  studentCount: integer
}
```

Không trả password hash, token, session, lockout detail hoặc dữ liệu auth nội bộ.

### 8.4. Create request

`POST /api/v1/teachers` tạo User role Teacher và Teacher profile trong một transaction.

```text
CreateTeacherRequest {
  fullName: string
  email: string
  phoneNumber: string | null
  status: UserStatus
  password: string
  dateOfBirth: date | null
  gender: Gender | null
  address: string | null
  qualification: string | null
  specialization: string | null
  startDate: date | null
  note: string | null
  attendanceEditWindowDays: integer
}
```

- `teacherCode` do server sinh và trả trong response.
- Thành công trả `201 Created`, header `Location` và full `TeacherDetailResponse`.
- Nếu User hoặc Teacher profile tạo lỗi, toàn bộ transaction rollback.
- Chống double-submit tại server; duplicate email/code trả stable conflict code.

### 8.5. Update request

`PUT /api/v1/teachers/{teacherId}` là full replacement cho toàn bộ field được phép sửa:

```text
UpdateTeacherRequest {
  fullName: string
  email: string
  phoneNumber: string | null
  status: UserStatus
  dateOfBirth: date | null
  gender: Gender | null
  address: string | null
  qualification: string | null
  specialization: string | null
  startDate: date | null
  note: string | null
  attendanceEditWindowDays: integer
  expectedVersion: integer
}
```

- Không cho sửa `teacherCode`, `userId`, `role`, `responsibleGroups`, timestamps.
- Field nullable gửi `null` để xóa giá trị.
- Thành công tăng `version` đúng 1 và trả full detail mới.
- Stale update trả `409 TeacherVersionConflict` với `currentVersion`; không update một phần.

### 8.6. Delete và đổi mật khẩu

- `DELETE` soft-delete User liên kết, revoke toàn bộ session và giữ nguyên Teacher row/mã để bảo toàn lịch sử.
- Không xóa hoặc rewrite attendance sheet/record đã lưu.
- Nếu Teacher còn nhóm phụ trách, trả `409 TeacherHasResponsibleGroups`; Admin phải gỡ/chuyển nhóm trước.
- `expectedVersion` đặt trong query để tránh DELETE body khó tương thích qua proxy/client.
- Đổi mật khẩu tiếp tục dùng `PUT /users/{userId}/password`; UI lấy đúng `userId` từ Teacher response. Mọi session cũ bị revoke theo contract hiện tại.

### 8.7. Chuyển quyền sở hữu khỏi User CRUD

Sau khi epic hoàn tất:

- `/teachers` là bề mặt canonical để tạo/sửa/xóa Teacher.
- UI `/users` đổi thành `Tài khoản quản trị`, chỉ hiển thị cho SuperAdmin và không còn option tạo/sửa/xóa role Teacher.
- API User CRUD phải từ chối mutation đối với Teacher bằng code `TeacherMustBeManagedViaTeachers`, hoặc delegate vào cùng Teacher aggregate coordinator nếu cần tương thích ngắn hạn.
- Không để User PUT cũ âm thầm cập nhật Teacher vì sẽ bypass `expectedVersion`, group snapshot và audit của Teacher.

Quy tắc chuyển đổi chính xác cần chốt tại `TCH-DEC-03` trước khi sửa contract hiện hữu.

## 9. Validation và chuẩn hóa

| Field | Quy tắc |
|---|---|
| `fullName` | Bắt buộc, trim, tối đa 200 |
| `email` | Bắt buộc, email hợp lệ, tối đa 255, normalize theo User hiện tại |
| `phoneNumber` | Nullable, trim, tối đa 30 |
| `password` | 12–128 ký tự theo policy hiện tại |
| `dateOfBirth` | Nullable, không phải `0001-01-01`, không lớn hơn ngày server |
| `gender` | Nullable, enum hợp lệ |
| `address` | Nullable, trim, tối đa 500 |
| `qualification` | Nullable, trim, tối đa 200 |
| `specialization` | Nullable, trim, tối đa 200 |
| `startDate` | Nullable, không phải `0001-01-01`; ngày tương lai chờ quyết định |
| `note` | Nullable, trim, tối đa 2.000 |
| `attendanceEditWindowDays` | Integer từ 1 đến 7 |
| `expectedVersion` | Integer lớn hơn 0 |

API trả `application/problem+json`, có `code`, `traceId`, `fieldErrors` khi phù hợp. UI ánh xạ code/enum sang tiếng Việt và không hiển thị raw `title`, `detail` hoặc identifier tiếng Anh.

## 10. Lifecycle, concurrency và attendance invariant

- Một User role Teacher tương ứng đúng một Teacher profile; migration và command phải giữ invariant này.
- Teacher profile không hard-delete và mã giáo viên không tái sử dụng.
- Lock/update theo thứ tự nhất quán: Teacher, User, sau đó các group liên quan theo `id` tăng dần.
- Đổi `fullName` phải tăng `snapshotVersion` của tất cả nhóm đang phụ trách đúng một lần trong cùng transaction.
- Đổi email, phone, policy hoặc các field hồ sơ khác không tăng group snapshot.
- Attendance sheet đã lưu không bị rewrite khi hồ sơ Teacher thay đổi.
- Không snapshot `teacherCode` vào attendance v1; lịch sử tiếp tục dùng `responsibleTeacherId` và `responsibleTeacherNameSnapshot`.
- `version` bảo vệ toàn bộ aggregate Teacher–User khỏi lost update. Endpoint policy tương thích cũng phải tăng cùng version.
- Update status/password/delete áp dụng session revoke theo security contract hiện tại.

## 11. ProblemDetails codes

Các code tối thiểu cần khóa trong OpenAPI và dictionary UI:

```text
TeacherNotFound
TeacherCodeAlreadyExists
TeacherVersionConflict
TeacherHasResponsibleGroups
TeacherMustBeManagedViaTeachers
InvalidTeacherDateOfBirth
InvalidTeacherStartDate
InvalidAttendanceEditWindow
EmailAlreadyExists
ValidationFailed
```

Tên cuối cùng phải thống nhất với convention hiện có; không tạo hai code khác nhau cho cùng lỗi.

## 12. Audit và bảo mật dữ liệu

Audit actions đề xuất:

```text
Teacher.Created
Teacher.Updated
Teacher.Deleted
Teacher.AttendancePolicyUpdated
```

Quy tắc:

- Ghi `teacherId`, `userId`, danh sách field thay đổi, version trước/sau và action result.
- Có thể ghi transition enum như status cũ/mới.
- Không ghi raw password, token, cookie, note, address, ngày sinh hoặc toàn bộ request body.
- Password change tiếp tục dùng audit action của User hiện tại.
- Giữ retention audit hiện tại; dữ liệu Teacher/attendance lịch sử không bị cleanup bởi maintenance job.

## 13. Thiết kế UI

### 13.1. Navigation và route

- Sidebar thêm `Giáo viên` cho Admin/SuperAdmin.
- Routes:

```text
/teachers
/teachers/new
/teachers/:id
/teachers/:id/edit
```

- Dùng `SetupCompletedGuard`, auth guard và role guard hiện có.
- `/users` đổi nhãn thành `Tài khoản quản trị`, chỉ SuperAdmin thấy sau khi chốt chuyển đổi contract.

### 13.2. Danh sách

- DevExtreme `CustomStore`, server paging/filter/sort.
- Page size: 10/20/50/100, mặc định 20; đổi filter quay về trang 1.
- Panel filter mặc định mở, cho collapse/expand.
- Filter gồm search, trạng thái tài khoản, giới tính, nhóm và `Chưa phụ trách nhóm`.
- Cột chính: mã, họ tên, email/số điện thoại, trạng thái, nhóm phụ trách, thời hạn sửa điểm danh, ngày vào làm và thao tác.
- Click row mở detail; thao tác gồm `Xem`, `Chỉnh sửa`, `Đổi mật khẩu`, `Xóa`.
- Desktop dùng grid; màn hình hẹp giữ cột định danh và đưa phần còn lại vào adaptive detail/card.

### 13.3. Chi tiết

Chia thành các card:

1. Thông tin cá nhân.
2. Thông tin nghề nghiệp.
3. Tài khoản đăng nhập và trạng thái.
4. Chính sách điểm danh.
5. Nhóm đang phụ trách, chỉ đọc, có CTA sang trang `Nhóm`.

Có loading, empty, 403, 404, retry và trace reference bằng nội dung tiếng Việt.

### 13.4. Tạo và chỉnh sửa

- Dùng trang riêng thay vì popup lớn để hỗ trợ form dài, responsive và dirty guard.
- Form hai cột trên desktop, một cột trên mobile.
- Create có mật khẩu/xác nhận mật khẩu; edit không hiển thị password.
- Mã giáo viên hiển thị read-only; ở create ghi rõ mã sẽ được tạo sau khi lưu.
- Full PUT gửi đủ field nullable và `expectedVersion`.
- Có dirty route guard, `beforeunload`, chống double-submit và focus field lỗi đầu tiên.
- Lỗi 409 giữ nguyên draft, hiển thị CTA tải dữ liệu mới; không tự ghi đè dữ liệu server.

### 13.5. Dọn bề mặt quản trị cũ

- Bỏ tab `Chính sách giáo viên` khỏi trang `Nhóm`; thay bằng link đến `Giáo viên`.
- Trang `Nhóm` vẫn là nơi duy nhất gán/gỡ Teacher và quản lý roster.
- Trang User không còn là bề mặt quản lý Teacher sau khi chuyển đổi xong.

### 13.6. Tiếng Việt và accessibility

- Label, tooltip, menu, confirm, validation, loading, empty state, ARIA và DevExtreme message đều bằng tiếng Việt.
- Enum/status dùng dictionary tập trung; trạng thái không chỉ phân biệt bằng màu.
- Form có label thật, error summary và focus control lỗi đầu tiên.
- Nút icon có text hoặc `aria-label`; touch target tối thiểu khoảng 44px.
- Sticky action không che field cuối; keyboard và screen reader thao tác được.

## 14. Kế hoạch triển khai theo mã

### `TCH-00` — Khóa phạm vi và contract

- `TCH-BE-00`: khóa field ownership, DTO, endpoint, authorization, ProblemDetails và OpenAPI draft.
- `TCH-FE-00`: khóa route, wireflow, form model, dictionary tiếng Việt và permission matrix.
- `TCH-QA-00`: lập traceability từ quyết định nghiệp vụ đến test cases.

### `TCH-01` — Schema và read slice

- `TCH-BE-01`: mở rộng entity/config, sequence mã, migration/backfill, index và version.
- `TCH-BE-02`: list/detail query, paging/filter/sort, projection User–Teacher–Group và authorization.
- `TCH-FE-01`: models/service, navigation, route, list/filter/paging.
- `TCH-FE-02`: detail read-only và danh sách nhóm phụ trách.
- `TCH-QA-01`: fresh migration, upgrade rehearsal và list/detail contract tests.

### `TCH-02` — Create/update aggregate

- `TCH-BE-03`: atomic create User + Teacher, generated code và rollback conflict.
- `TCH-BE-04`: full PUT, shared User coordinator, validation, version conflict và group snapshot.
- `TCH-FE-03`: create/edit form, field mapping, validation và dirty guard.
- `TCH-QA-02`: create/update/nullable clear/duplicate/concurrency/snapshot tests.

### `TCH-03` — Password, lifecycle và chuyển đổi User CRUD

- `TCH-BE-05`: password integration, soft-delete, group blocker, session revoke và canonical mutation boundary.
- `TCH-FE-04`: password/delete flows; chuyển `/users` thành quản lý tài khoản Admin.
- `TCH-QA-03`: role/status/password/delete/session và backward-contract regression.

### `TCH-04` — Hợp nhất policy và UX nhóm

- `TCH-BE-06`: dùng chung version/audit cho attendance policy; deprecate endpoint cũ khi an toàn.
- `TCH-FE-05`: đưa policy vào Teacher form, bỏ tab policy cũ, giữ group assignment read-only tại Teacher detail.
- `TCH-QA-04`: policy biên 1/7, group assignment và attendance regression.

### `TCH-05` — Hardening

- `TCH-BE-07`: audit privacy, query performance, stable locks và OpenAPI hardening.
- `TCH-FE-06`: responsive, accessibility, toàn bộ copy tiếng Việt và error/concurrency states.
- `TCH-QA-05`: security/privacy/a11y/mobile/network/race coverage.

### `TCH-06` — Tài liệu và bàn giao

- `TCH-BE-08`: README, `requests.http`, migration notes và API examples.
- `TCH-FE-07`: README, route/role documentation và UI regression tests.
- `TCH-QA-06`: backend build, unit/integration PostgreSQL, frontend development build/test và diff review.

`TCH-QA-06` không tự chạy production build/package/deploy. Việc đó chỉ thực hiện qua skill riêng khi được gọi.

## 15. Test matrix tối thiểu

### Backend/unit và integration

- Validation trim/length/date/enum/policy/version; full PUT clear nullable bằng `null`.
- Sequence sinh mã unique khi request đồng thời; code cũ được backfill và không tái sử dụng.
- Create User + Teacher atomic; duplicate email hoặc lỗi profile rollback toàn bộ.
- List pagination/search/filter/sort có metadata đúng và tie-break ổn định.
- `groupId + unassigned=true` trả 400; Teacher không truy cập manager APIs.
- Update tăng version đúng 1; stale version trả 409 và rollback toàn bộ.
- Đổi fullName tăng đúng một lần snapshot của mọi group đang phụ trách; field khác không tăng.
- Concurrent rename và tạo phiếu điểm danh cho name/snapshot version nhất quán.
- Xóa bị chặn khi còn group; xóa hợp lệ revoke session nhưng Teacher/history vẫn còn.
- User CRUD không bypass Teacher coordinator sau khi chuyển đổi.
- Response/OpenAPI không lộ password hash, token, session hoặc auth internals.
- Audit đúng action và không chứa raw note/address/ngày sinh/password.
- Fresh PostgreSQL migration, upgrade từ attendance baseline và EF no pending model changes.
- Toàn bộ auth, group, student và attendance integration tests hiện tại vẫn pass.

### Frontend

- Navigation/route cho Admin, SuperAdmin; Teacher bị từ chối.
- `CustomStore` map page/pageSize/filter/sort đúng và reset trang khi đổi filter.
- List/detail/loading/empty/403/404/network/retry.
- Create/update full form, nullable clear, password confirm và double-submit guard.
- 409 giữ draft và có hành động tải bản server mới.
- Password change dùng đúng `userId`; delete confirm và group-blocker copy tiếng Việt.
- Group chỉ đọc tại Teacher detail; assignment vẫn qua trang `Nhóm`.
- Không hiển thị raw enum, code, `ProblemDetails.title/detail` hoặc copy tiếng Anh.
- Keyboard, focus lỗi đầu tiên, screen reader, mobile một cột và adaptive grid.
- Regression routes `/users`, `/student-groups`, `/attendance`, auth refresh và first-run setup.

## 16. Quyết định cần review

Các giá trị dưới đây là **đề xuất**, chưa phải contract đã chốt:

| Mã | Quyết định | Đề xuất |
|---|---|---|
| `TCH-DEC-01` | Bộ field hồ sơ v1 | Dùng bộ field tại mục 5.2; chưa có ảnh/CCCD/tài liệu |
| `TCH-DEC-02` | Mã giáo viên | Server tự sinh `GV000001`, bắt buộc, bất biến, không tái sử dụng |
| `TCH-DEC-03` | Bề mặt create/update/delete | `/teachers` là canonical và atomic; `/users` chỉ quản lý Admin sau chuyển đổi |
| `TCH-DEC-04` | Trạng thái nhân sự riêng | Chưa thêm trong v1; chỉ hiển thị rõ `Trạng thái tài khoản` |
| `TCH-DEC-05` | Phân công nhóm | Chỉ sửa tại trang/endpoint `student-groups`; Teacher detail chỉ đọc |
| `TCH-DEC-06` | Chính sách điểm danh | Chuyển vào Teacher form; endpoint policy cũ giữ tạm rồi deprecate |
| `TCH-DEC-07` | Teacher self-service | Không có trong v1 |
| `TCH-DEC-08` | Dữ liệu nhạy cảm/upload | Ngoài phạm vi v1 |
| `TCH-DEC-09` | Search tên không dấu | Hỗ trợ ở server; khóa giải pháp/index sau benchmark PostgreSQL |
| `TCH-DEC-10` | Ngày vào làm tương lai | Đề xuất cho phép để nhập trước nhân sự sắp nhận việc |
| `TCH-DEC-11` | Optimistic concurrency | Dùng `expectedVersion` trong full PUT; DELETE dùng query version |
| `TCH-DEC-12` | Xóa Teacher | Soft-delete User, revoke session, giữ Teacher/mã/lịch sử vĩnh viễn |

Chỉ bắt đầu `TCH-00` sau khi người dùng duyệt hoặc điều chỉnh các quyết định trên.

## 17. Definition of Done

- Các quyết định `TCH-DEC-*` đã chốt và contract OpenAPI/API/UI đồng nhất.
- Migration chạy được từ database sạch và từ baseline hiện tại, không mất Teacher/Group/Attendance history.
- API đúng authorization, validation, atomicity, concurrency, audit privacy và ProblemDetails contract.
- UI quản lý Teacher hoàn chỉnh, responsive, accessible và 100% nội dung hiển thị bằng tiếng Việt.
- Không còn mutation Teacher qua UI User cũ; group assignment chỉ có một nguồn mutation.
- Backend build/unit/PostgreSQL integration và frontend development build/unit tests pass.
- Tài liệu, `tasks.md` và durable memory được cập nhật.
- Production/IIS chỉ được build hoặc deploy khi người dùng gọi `$gv-portal-production` riêng.
