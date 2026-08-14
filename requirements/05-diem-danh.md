# 05 — Điểm danh

## 1. Mục tiêu và quyền

- Sidebar có mục `Điểm danh` cho `Teacher`, `Admin` và `SuperAdmin`.
- Teacher chỉ xem/tạo/sửa phiếu của nhóm đang phụ trách hiện tại.
- Admin/SuperAdmin được thao tác mọi nhóm nhưng phải chọn rõ một nhóm; không có lựa chọn `Tất cả nhóm`.
- Teacher có một nhóm được tự động chọn nhóm; nhiều nhóm thì phải chọn; không có nhóm thì hiển thị empty state và không tải daily.
- Quyền Teacher bị mất ngay khi không còn phụ trách nhóm; Admin/SuperAdmin vẫn xem được lịch sử.

## 2. Ngày nghiệp vụ và thời hạn sửa

- Điểm danh theo một ngày `YYYY-MM-DD`, dựa trên múi giờ `Asia/Ho_Chi_Minh`.
- UI mặc định ngày hiện tại của hệ thống và hiển thị theo `dd/MM/yyyy`.
- Không role nào được tạo/sửa ngày tương lai.
- Mỗi Teacher có edit window từ 1 đến 7 ngày, mặc định 7.
- Window bao gồm hôm nay: 1 chỉ hôm nay; 7 gồm hôm nay và 6 ngày trước.
- Admin/SuperAdmin không bị giới hạn bởi window của Teacher nhưng vẫn bị chặn ngày tương lai.

## 3. Phiếu Missing và Saved

### `Missing`

- Nghĩa là chưa có phiếu được lưu; không được diễn giải là học sinh đã có mặt.
- GET chỉ trả preview roster theo nhóm và lịch học hiện tại; không ghi database.
- Preview dùng default `Present` cho học cả ngày và `OneToOneHour` cho học 1-1.
- Lần lưu đầu phải lưu đầy đủ một record cho mọi học sinh trong roster, kể cả `Present` hoặc `Unmarked`.
- Nút lưu lần đầu vẫn cho phép bấm khi dirty count bằng 0 vì đây là thao tác xác nhận toàn bộ preview.

### `Saved`

- Là snapshot authoritative của ngày/nhóm.
- Lưu đầy đủ record cho mọi học sinh thuộc roster lúc tạo phiếu.
- Thay đổi Student, lịch học, nhóm hoặc Teacher hiện tại không tự sửa phiếu đã lưu.
- Phiếu vẫn được chỉnh khi actor còn quyền và ngày còn trong policy.
- Update gửi full roster của snapshot cùng `expectedVersion`.
- Không có trạng thái Draft/Finalized riêng trong v1.

## 4. Roster

- Một daily request luôn scope vào đúng một nhóm và trả tối đa 100 học sinh; không phân trang trong màn hình điểm danh.
- Với Missing, roster gồm Student active thuộc nhóm và có lịch trong ngày đang chọn.
- Với Saved, roster là các record snapshot đã lưu, không lọc lại theo lịch hiện tại.
- Số học sinh hiển thị tại danh sách nhóm trong context ưu tiên số roster hiện tại theo lịch; nếu không có số roster hiện tại thì dùng số record Saved làm giá trị dự phòng. Summary của phiếu Saved vẫn tính từ chính snapshot records.
- Nếu không có học sinh theo lịch, GET trả response 200 read-only với lý do `NoScheduledStudents`; không hiển thị/lưu phiếu rỗng.
- Snapshot/version phải phát hiện thay đổi roster/identity xảy ra giữa lúc tải và lúc lưu; request stale bị từ chối thay vì lưu nhầm.

## 5. Trạng thái điểm danh hiện hành

| Mã API | Nhãn UI | Quy tắc |
|---|---|---|
| `Present` | `Có mặt` | Không có field nghỉ hoặc duration; ghi chú vẫn được phép |
| `AbsentFullDay` | `Nghỉ` / `Nghỉ cả ngày` | Bắt buộc chọn có phép/không phép |
| `AbsentHalfDay` | `Nghỉ 1/2` | Bắt buộc chọn có phép/không phép; không chọn sáng/chiều; chi tiết ghi trong ghi chú |
| `OneToOneHour` | `1-1` | Một block cố định 60 phút, không có giờ bắt đầu và không đồng thời với trạng thái vắng |
| `Unmarked` | `Chưa điểm danh` | Trạng thái được lưu thật; mọi field điều kiện null; chỉ xuất hiện khi người dùng chủ động chọn |

Quy tắc bổ sung:

- Đổi về `Present` hoặc `Unmarked` phải clear các field điều kiện nhưng giữ ghi chú.
- `isExcused` chỉ áp dụng và bắt buộc cho hai trạng thái nghỉ.
- Mọi thao tác mới với `AbsentHalfDay` gửi `halfDayPart=null`.
- Dữ liệu Saved cũ có Morning/Afternoon phải được giữ nguyên nếu record vẫn ở trạng thái nghỉ nửa ngày và người dùng không đổi trạng thái; khi đổi trạng thái thì giá trị legacy được clear.
- Ghi chú nullable và áp dụng cho mọi trạng thái.
- API chấp nhận ghi chú tối đa 2.000 ký tự; UI chỉ cho nhập/sửa tối đa 200 ký tự và không được cắt dữ liệu cũ dài hơn nếu người dùng chưa sửa.
- `Unmarked` có count riêng và không tính vào có mặt, vắng hoặc 1-1.

## 6. Tạo và cập nhật phiếu

Các endpoint chính:

```http
GET  /api/v1/attendance/context?date={date}
GET  /api/v1/attendance/daily?date={date}&groupId={groupId}
POST /api/v1/attendance
PUT  /api/v1/attendance/{sheetId}
```

- POST lần đầu gửi group, date, snapshot version và full roster.
- PUT gửi sheet expected version và full roster snapshot.
- Thiếu, thừa hoặc trùng học sinh phải bị từ chối bằng roster mismatch.
- Một ngày/nhóm chỉ có một phiếu.
- Save thành công trả full Saved response để UI thay baseline/draft bằng server source of truth.
- Conflict snapshot/sheet phải giữ draft trên UI và cho người dùng tải dữ liệu mới; không silent overwrite.
- Validation phải chỉ ra đúng record/field để UI focus card lỗi.

## 7. Tìm kiếm, lọc và tổng hợp

- Filter panel mặc định mở, có thể collapse/expand.
- Filter gồm ngày, nhóm theo role và tìm kiếm `Tên, mã học sinh, tên gọi`.
- Search không phân biệt dấu/hoa thường, chạy trên roster đã được cấp quyền và không gọi lại API.
- Search/filter chỉ ẩn card, không xóa draft và không thay đổi summary toàn phiếu.
- Có filter theo năm trạng thái, gồm `Chưa điểm danh`.
- Summary toàn roster gồm tổng số, có mặt, vắng, 1-1 và chưa điểm danh; tổng các nhóm trạng thái phải bằng tổng roster.

## 8. Dirty state và lưu

- Không autosave khi người dùng đổi status hoặc ghi chú.
- Missing có thể lưu dù chưa có thay đổi local.
- Saved chỉ cho lưu khi có thay đổi.
- Sticky action hiển thị số thay đổi chưa lưu và không che card cuối.
- Đổi ngày, đổi nhóm, chuyển route/sidebar hoặc đóng trình duyệt khi có draft phải cảnh báo.
- Search, collapse hoặc scroll không tạo dirty.
- Nếu card lỗi đang bị filter ẩn, UI phải hiện lại rồi scroll/focus đến lỗi.
- Save thành công reset dirty về 0; lỗi 409 giữ nguyên draft.

## 9. Khôi phục lịch sử

- Chỉ Admin/SuperAdmin được khôi phục ngày quá khứ khi snapshot không còn tái dựng chính xác.
- Teacher chỉ thấy lý do read-only, không có quyền recovery.
- Recovery bắt buộc chọn rõ group, Teacher và roster lịch sử, xác nhận danh sách phản ánh đúng ngày lịch sử và nhập lý do. UI giới hạn lý do ở 200 ký tự; API tiếp tục chấp nhận tối đa 500 ký tự để tương thích contract hiện có.
- Candidate search có thể tìm record inactive, soft-delete hoặc former role nhưng không lộ email đăng nhập, mật khẩu hoặc session.
- Candidate Student cung cấp mã, họ tên, nickname, trạng thái, tình trạng đã xóa và thông tin nhóm/giáo viên phụ trách hiện tại nếu có để quản trị viên đối chiếu trước khi chọn.
- UI hiển thị tối đa 100 Student candidate trong danh sách có ô tìm kiếm, checkbox chọn nhiều, chọn tất cả trong trang và bộ đếm `đã chọn/100`; mỗi lần mở lại recovery phải bắt đầu với selection rỗng.
- Identity snapshot do server lấy từ dữ liệu tốt nhất hiện có; danh sách candidate phải cung cấp đủ ngữ cảnh nhóm/giáo viên hiện tại để quản trị viên tự đối chiếu trước khi xác nhận.
- Recovery không lọc current schedule và mặc định thủ công là `Present`.
- Recovery giữ layout riêng hiện tại, không nằm trong redesign compact card v1.
- Phiếu recovery sau khi lưu vẫn dùng sheet version để cập nhật theo quyền.
- Nếu group/ngày đã có phiếu, recovery được phép tái dựng lại phiếu đó thành snapshot `HistoricalRecovery`: giữ các record hiện có không được chọn lại, áp dụng dữ liệu mới cho Student được chọn và thêm Student mới. Sau thao tác vẫn chỉ tồn tại một phiếu cho group/ngày.
- Việc tái dựng phiếu đang có không được âm thầm làm mất record cũ; Teacher snapshot và lý do recovery dùng lựa chọn mới của quản trị viên.

## 10. Lưu giữ và audit

- Attendance sheet/record là dữ liệu nghiệp vụ được giữ lâu dài và không thuộc cleanup tự động.
- Audit thay đổi điểm danh giữ 90 ngày.
- Audit ghi actor, IDs, ngày, trạng thái, phép/không phép, duration và version.
- Không ghi raw notes hoặc toàn bộ request body; chỉ cần đánh dấu notes có thay đổi.
