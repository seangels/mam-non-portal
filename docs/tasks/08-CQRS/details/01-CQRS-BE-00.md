# CQRS-BE-00 — Mediator foundation + migrate Users slice

## Mục đích

Tạo nền Mediator/CQRS nhỏ, dễ đọc trong backend và migrate slice `Users` đầu tiên để controller không gọi service trực tiếp nữa.

## Tóm tắt ngắn

1. ✅ Thêm mediator foundation nội bộ trong `AdminPortal.Application.Common.Mediator`.
2. ✅ Thêm DI registration để resolve handler theo request type.
3. ✅ Tạo command/query cho các use case `Users`: list, get, create, update, change password, delete.
4. ✅ Chuyển `UsersController` sang gọi `IAppMediator.Send(...)`.
5. ✅ Giữ nguyên REST contract/status code/error code hiện tại cho `Users`.
6. ✅ Chạy backend build/unit/integration phù hợp.
7. ❓ Chưa đổi sang package `MediatR` chính thức; nếu muốn dùng package ngoài, cần xác nhận riêng.

## Phạm vi

- Thay đổi trong `api/` và docs/memory liên quan.
- Không sửa `ui/`.
- Không đổi database schema/migration.
- Không đổi route `/api/v1/users`, DTO, role policy hoặc response status.
- Không chạy production/IIS/deploy.

## Nội dung cần làm

- ✅ Tạo abstraction:
  - ✅ `IAppRequest<TResponse>`
  - ✅ `IAppCommand<TResponse>` và `IAppCommand`
  - ✅ `IAppQuery<TResponse>`
  - ✅ `IAppRequestHandler<TRequest,TResponse>`
  - ✅ `IAppMediator`
- ✅ Tạo implementation mediator dùng DI hiện có.
- ✅ Đăng ký mediator + handlers trong DI.
- ✅ Tạo các request CQRS cho `Users`.
- ✅ Đưa logic `Users` qua adapter handler an toàn trong giai đoạn chuyển tiếp; `UserService` vẫn giữ business logic ở checkpoint đầu.
- ✅ Chuyển `UsersController` sang inject `IAppMediator`.
- ✅ Đảm bảo `CreatedAtAction`, `NoContent`, ProblemDetails và authorization policy không đổi.
- ✅ Cập nhật docs task/log/memory.
- ✅ Siết lại guard đọc `TeacherService` và policy `TeachersController` về `PortalManagers` sau khi integration test phát hiện `/teachers` management không được phép mở cho role `Teacher`.

## Kiểm thử mong đợi

- ✅ `dotnet build api/AdminPortal.slnx -c Release --no-restore`
- ✅ `dotnet test api/tests/AdminPortal.UnitTests -c Release --no-restore`
- ✅ `dotnet test api/tests/AdminPortal.IntegrationTests -c Release --no-restore`

## Cần làm rõ

- ❓ Có muốn dùng package `MediatR` chính thức thay mediator nội bộ không? Mặc định hiện tại là mediator nội bộ để không thêm dependency.
- ❓ Có muốn migrate toàn bộ backend trong một milestone lớn không? Mặc định hiện tại là migrate từng slice để giảm rủi ro.
