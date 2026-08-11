# Mầm Non Admin UI

Admin portal xây bằng Angular 15 và DevExtreme 23.2. Ứng dụng hỗ trợ đăng nhập, quản lý tài khoản và quản lý học sinh theo contract tại [`../api/plan.md`](../api/plan.md).

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
```

`test:ci` dùng Chrome headless với cấu hình phù hợp CI/container. Build production được xuất vào `dist/DevExtreme-app`.

## Authentication và CSRF

- Access token chỉ giữ trong memory, không ghi vào local storage hoặc session storage.
- Refresh token nằm trong cookie `HttpOnly`, `Secure`, `SameSite=None` do API quản lý.
- Login/refresh trả `csrfToken`; frontend giữ token này trong memory và gửi qua header `X-CSRF-TOKEN` cho request thay đổi dữ liệu.
- Khi reload trang, frontend gọi `GET /auth/csrf`, sau đó `POST /auth/refresh` và `GET /auth/me` để khôi phục phiên.
- Một nhóm request nhận `401` chỉ tạo một refresh request dùng chung. Nếu refresh thất bại, phiên trong memory bị xóa và người dùng được chuyển về trang đăng nhập.

## Phân quyền UI

- `SuperAdmin`: quản lý tài khoản Admin/Teacher và học sinh.
- `Admin`: quản lý tài khoản Teacher và học sinh.
- `Teacher`: chỉ xem trang tổng quan và thông tin tài khoản trong phiên bản hiện tại.

API vẫn là nơi quyết định quyền cuối cùng; ẩn menu và route guard chỉ giúp trải nghiệm người dùng.

## Cấu trúc chính

```text
src/app/
├── core/
│   ├── interceptors/       # Bearer, refresh và CSRF
│   ├── models/             # API DTO và ProblemDetails
│   └── services/           # API client, setup/auth state, user/student client
├── pages/
│   ├── users/              # Remote grid và CRUD tài khoản
│   └── students/           # Remote grid và CRUD học sinh
└── shared/                 # Layout, setup, login và dịch vụ dùng chung
```

Hai grid dùng server-side pagination/filter/sort. `pageSize` tối đa 100 và payload ngày sinh dùng định dạng `YYYY-MM-DD`.

## Deploy IIS local HTTPS

Angular configuration `iis` gọi API tại `https://api-gv-portal.local/api/v1`. Xem script và hướng dẫn đầy đủ tại [`../deploy/iis/HUONG-DAN-DEPLOY-IIS.md`](../deploy/iis/HUONG-DAN-DEPLOY-IIS.md).
