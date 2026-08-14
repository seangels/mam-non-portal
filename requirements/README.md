# Danh mục yêu cầu hệ thống GV Portal

Thư mục này là bản tổng hợp yêu cầu đã được chốt cho Admin Portal. Nội dung mô tả hệ thống **phải làm gì** và các tiêu chí nghiệp vụ cần giữ; không mô tả class, migration, cấu trúc project, câu lệnh build hoặc kế hoạch triển khai mã nguồn.

## Thứ tự đọc

| Thứ tự | Tài liệu | Nội dung |
|---:|---|---|
| 00 | [Tổng quan và phạm vi](00-tong-quan-va-pham-vi.md) | Mục tiêu, actor, thuật ngữ, phạm vi và thứ tự ưu tiên yêu cầu |
| 01 | [Xác thực và phân quyền](01-xac-thuc-va-phan-quyen.md) | Khởi tạo lần đầu, đăng nhập, phiên đăng nhập và quyền |
| 02 | [Tài khoản quản trị và giáo viên](02-tai-khoan-quan-tri-va-giao-vien.md) | Quản lý Admin, hồ sơ Teacher và chính sách điểm danh |
| 03 | [Học sinh và nhóm](03-hoc-sinh-va-nhom.md) | CRUD học sinh, nhóm, roster và phân công giáo viên |
| 04 | [Lịch học của học sinh](04-lich-hoc-hoc-sinh.md) | Hình thức học, ngày học và ảnh hưởng tới roster điểm danh |
| 05 | [Điểm danh](05-diem-danh.md) | Phiếu hằng ngày, trạng thái, quyền, lưu và khôi phục lịch sử |
| 06 | [Giao diện điểm danh](06-giao-dien-diem-danh.md) | Compact card, responsive, tìm kiếm, dirty state và accessibility |
| 07 | [API, bảo mật và vận hành](07-api-bao-mat-va-van-hanh.md) | Quy ước REST, lỗi, bảo mật, audit, retention và health check |
| 08 | [Triển khai IIS local HTTPS](08-trien-khai-iis-local-https.md) | Yêu cầu gói bàn giao và máy đích IIS/PostgreSQL |

## Thứ tự ưu tiên khi có nội dung cũ mâu thuẫn

Các yêu cầu sau là phiên bản cuối và ghi đè mô tả cũ:

1. Quản lý giáo viên dùng bề mặt `/teachers`; `/users` chỉ còn quản lý tài khoản `Admin`.
2. Lịch học hiện tại quyết định học sinh nào xuất hiện trong preview điểm danh chưa lưu và quyết định trạng thái mặc định.
3. `Chưa điểm danh` là trạng thái được lưu thật với mã `Unmarked`.
4. Điểm danh nghỉ nửa ngày không còn chọn sáng/chiều ở thao tác mới; chỉ chọn phép/không phép, chi tiết ghi trong ghi chú.
5. Card điểm danh chính chỉ hiển thị `nickname · studentCode` trong header ngang; không hiển thị họ tên đầy đủ trong card, tooltip hoặc accessible name.

## Nguồn tổng hợp

Yêu cầu được chắt lọc từ các plan theo thứ tự `BASE → ATT → TCH → SCH → AUI` và các quyết định nghiệp vụ đã xác nhận. Các plan vẫn là nơi lưu phân tích, lộ trình, migration và test matrix; thư mục này là nguồn đọc nhanh cho phạm vi sản phẩm hiện hành.

| Requirements | Nguồn chính |
|---|---|
| 00, 01, 07 | `plans/01-BASE-admin-portal.md` và shared decisions |
| 02 | `plans/03-TCH-teacher-management.md`, ghi đè phần Teacher cũ của BASE |
| 03 | BASE, `plans/02-ATT-attendance.md` và `plans/04-SCH-student-groups-study-schedule.md` |
| 04 | `plans/04-SCH-student-groups-study-schedule.md` |
| 05 | ATT + SCH + AUI theo thứ tự, sau đó áp dụng delta recovery/context hiện có trong source |
| 06 | AUI, yêu cầu UI tiếng Việt của ATT và layout card/recovery hiện có trong source |
| 08 | Các quyết định triển khai IIS/HTTPS đã chốt trong shared memory và tài liệu deploy |
