# Mầm Non Admin UI

Admin portal xây bằng Angular 15 và DevExtreme 23.2. Ứng dụng hỗ trợ đăng nhập, quản lý tài khoản quản trị, giáo viên, nhóm/học sinh và điểm danh theo contract nền tại [`../plans/01-BASE-admin-portal.md`](../plans/01-BASE-admin-portal.md), plan [`../plans/02-ATT-attendance.md`](../plans/02-ATT-attendance.md), [`../plans/03-TCH-teacher-management.md`](../plans/03-TCH-teacher-management.md) và [`../plans/04-SCH-student-groups-study-schedule.md`](../plans/04-SCH-student-groups-study-schedule.md). Danh mục và thứ tự các kế hoạch nằm tại [`../plans/README.md`](../plans/README.md).

## Yêu cầu

- Node.js 18 hoặc 20.
- .NET SDK 10 để tạo và trust chứng thư HTTPS development dùng chung với API.
- API đang chạy; cấu hình development mặc định là `https://localhost:7158/api/v1`.

## Chạy local

```bash
npm ci
npm start
```

Lần chạy đầu, script `prestart` sẽ tạo/trust chứng thư HTTPS development của .NET và export cặp PEM vào `.certs/` (thư mục này không được commit). Chấp nhận hộp thoại trust certificate của hệ điều hành nếu được hỏi.

Mở `https://localhost:4200`. API phải cho phép origin này trong `Security:AllowedOrigins` và cho phép credential. Có thể chủ động tạo lại chứng thư bằng `npm run setup:https`; script cũng giúp cả UI và API tại `https://localhost:7158` dùng chứng thư localhost đã được hệ điều hành tin cậy.

Khi khởi động, UI kiểm tra `GET /api/v1/setup/status`. Nếu database chưa có user, mọi route được chuyển đến `/#/setup` để tạo SuperAdmin đầu tiên; sau khi tạo thành công người dùng được chuyển đến màn đăng nhập. Nếu API tạm thời không truy cập được, màn setup hiển thị lỗi và cho phép kiểm tra lại thay vì bỏ qua bước khởi tạo.

Đổi API URL development tại `src/environments/environment.ts`. Production mặc định dùng `/api/v1` để hoạt động qua reverse proxy cùng host; có thể đổi trong `src/environments/environment.prod.ts` trước khi build nếu triển khai khác host.

## Lệnh kiểm tra

```bash
npm run build
npm run test:ci
npm run build -- --configuration iis
```

`test:ci` dùng Chrome headless với cấu hình phù hợp CI/container. Build production được xuất vào `dist/DevExtreme-app`. Configuration `iis` phải được dùng cho gói IIS hai host để bundle gọi đúng `https://api-gv-portal.local/api/v1`.

## Authentication và CSRF

- Access token chỉ giữ trong memory, không ghi vào local storage hoặc session storage.
- Refresh token nằm trong cookie `HttpOnly`, `Secure`, `SameSite=None` do API quản lý.
- Login/refresh trả `csrfToken`; frontend giữ token này trong memory và gửi qua header `X-CSRF-TOKEN` cho request thay đổi dữ liệu.
- Khi reload trang, frontend gọi `GET /auth/csrf`, sau đó `POST /auth/refresh` và `GET /auth/me` để khôi phục phiên.
- Một nhóm request nhận `401` chỉ tạo một refresh request dùng chung. Nếu refresh thất bại, phiên trong memory bị xóa và người dùng được chuyển về trang đăng nhập.

## Phân quyền UI

- `SuperAdmin`: quản lý tài khoản quản trị, giáo viên, nhóm/học sinh, chính sách điểm danh và mọi nhóm điểm danh.
- `Admin`: quản lý giáo viên, nhóm/học sinh, chính sách điểm danh và mọi nhóm điểm danh.
- `Teacher`: điểm danh các nhóm đang được phân công trong thời hạn chỉnh sửa được cấu hình.

API vẫn là nơi quyết định quyền cuối cùng; ẩn menu và route guard chỉ giúp trải nghiệm người dùng.

## Cấu trúc chính

```text
src/app/
├── core/
│   ├── interceptors/       # Bearer, refresh và CSRF
│   ├── models/             # API DTO và ProblemDetails
│   └── services/           # API client, setup/auth state, user/student client
├── pages/
│   ├── users/              # Remote grid và CRUD tài khoản Admin, chỉ SuperAdmin
│   ├── teachers/           # Danh sách, chi tiết, tạo/sửa/xóa giáo viên
│   ├── students/           # Remote grid và CRUD học sinh
│   ├── student-groups/     # Nhóm, roster, giáo viên phụ trách và policy
│   └── attendance/         # Context, card editor, full save và recovery
└── shared/                 # Layout, setup, login và dịch vụ dùng chung
```

Các grid quản trị dùng server-side pagination/filter/sort. `pageSize` tối đa 100 và payload ngày sinh dùng định dạng `YYYY-MM-DD`.

## Quản lý giáo viên

- `/#/teachers`, `/#/teachers/new`, `/#/teachers/:id` và `/#/teachers/:id/edit` dành cho Admin/SuperAdmin; Teacher không được truy cập.
- Danh sách tìm kiếm/lọc/phân trang hoàn toàn tại API. Frontend gửi từ khóa đã trim, lấy tổng số từ `pagination.totalItems` và không tự lọc không dấu trên từng trang.
- Hồ sơ Teacher chỉ thêm mã giáo viên, ghi chú và version; họ tên, email, số điện thoại và trạng thái vẫn thuộc tài khoản User. Mã do người dùng nhập, được chuẩn hóa chữ hoa và có thể sửa.
- Tạo giáo viên tạo đồng thời tài khoản Teacher. Cập nhật là full `PUT` kèm `expectedVersion`; xung đột giữ nguyên dữ liệu đang nhập để người dùng chủ động tải bản mới.
- Đổi mật khẩu dùng `userId` liên kết. Xóa dùng soft-delete kèm version, thu hồi phiên đăng nhập và bị chặn khi giáo viên còn phụ trách nhóm.
- Phân công nhóm và cấu hình thời hạn sửa điểm danh vẫn chỉ thực hiện tại trang `Nhóm`. Trang chi tiết giáo viên chỉ đọc hai thông tin này.
- `/#/users` được đổi thành `Tài khoản quản trị`, chỉ SuperAdmin thấy và chỉ tạo/sửa/xóa tài khoản Admin.

## Điểm danh

- Route `/#/attendance` dành cho cả ba role; ngày dùng lịch nghiệp vụ `Asia/Ho_Chi_Minh` và payload `YYYY-MM-DD`.
- Admin/SuperAdmin chọn một nhóm bất kỳ. Teacher chỉ thấy nhóm đang phụ trách; một nhóm được tự chọn, nhiều nhóm dùng bộ chọn và chưa có nhóm hiển thị hướng dẫn riêng.
- Danh sách tối đa 100 học sinh được tải một lần rồi tìm kiếm không dấu/lọc cục bộ để không làm mất bản nháp. Phiếu `Missing` chỉ là bản xem trước và vẫn phải bấm **Lưu phiếu** dù mọi học sinh đều có mặt.
- Lần lưu đầu dùng POST với toàn bộ roster và snapshot version. Phiếu đã lưu dùng full PUT với sheet version; xung đột `409` không ghi đè âm thầm.
- Trạng thái hỗ trợ có mặt, vắng nguyên buổi, vắng nửa buổi và học riêng một giờ. Đổi ngày/nhóm/route khi có thay đổi sẽ hỏi xác nhận; đóng hoặc tải lại tab cũng được trình duyệt cảnh báo.
- Khôi phục lịch sử chỉ dành cho Admin/SuperAdmin khi snapshot chuẩn không còn khả dụng. Người dùng phải chọn thủ công nhóm, giáo viên, 1–100 học sinh, nhập lý do và xác nhận cảnh báo.

## Lịch học và phân nhóm học sinh

- Trang `/#/students` hiển thị nhóm hiện tại và lịch học hằng tuần, đồng thời hỗ trợ lọc từ API theo nhóm, trạng thái chưa phân nhóm, hình thức và ngày học.
- Mỗi học sinh có một hình thức `Học cả ngày` hoặc `Học 1-1` và từ một đến sáu ngày học Thứ Hai–Thứ Bảy. Create/full PUT luôn gửi schedule; cập nhật, phân nhóm và xóa dùng `expectedVersion` để không ghi đè dữ liệu cũ.
- Phân/chuyển/gỡ nhóm tại trang Học sinh và roster trang Nhóm cùng gọi `PUT /students/{id}/group`. Nhóm đủ 100 học sinh không thể chọn; thay đổi nhóm có hiệu lực ngay.
- Danh sách điểm danh `Missing` do API lọc theo lịch của ngày nghiệp vụ. `FullDay` được gợi ý `Có mặt`, `OneToOne` được gợi ý `Học 1-1 (1 giờ)`; UI giữ nguyên default từ API và không tự tính lại.
- Ngày không có học sinh có lịch là trạng thái chỉ xem `NoScheduledStudents` và không hiện nút lưu. Phiếu `Saved` cùng historical recovery vẫn giữ snapshot/manual roster, không bị lịch hiện tại lọc lại.

## Deploy IIS local HTTPS

Angular configuration `iis` gọi API tại `https://api-gv-portal.local/api/v1`. Xem script và hướng dẫn đầy đủ tại [`../deploy/iis/HUONG-DAN-DEPLOY-IIS.md`](../deploy/iis/HUONG-DAN-DEPLOY-IIS.md).
