# Kế hoạch refactor Backend sang Mediator + CQRS

- **Mã kế hoạch:** `CQRS`
- **Thứ tự:** `08`
- **Trạng thái:** Đang triển khai
- **Phụ thuộc:** `BASE`, các feature backend hiện hữu

## 1. Mục tiêu

Refactor backend .NET theo mô hình Mediator + CQRS để controller mỏng hơn, use case tách rõ theo command/query, code dễ đọc, dễ chỉnh sửa và dễ test hơn.

Không thay đổi REST contract, database schema, authentication flow, authorization policy, ProblemDetails, audit semantics hoặc IIS/deploy flow trong đợt refactor này.

## 2. Nguyên tắc triển khai

- Làm incremental theo từng vertical slice, không rewrite toàn bộ backend trong một commit lớn.
- Controller chỉ map HTTP/cookie/header/file upload sang command/query và trả HTTP response.
- Command dùng cho thao tác ghi/mutation; Query dùng cho thao tác đọc.
- Handler chứa orchestration của use case; rule thuần hiện có như `AuthorizationRules`, `AttendanceRules`, `AssessmentSheetRules` tiếp tục được tái sử dụng.
- Không thêm generic repository; EF Core vẫn được truy cập qua `IApplicationDbContext` trong Application layer như hiện tại.
- Không đổi DTO request/response đang được frontend dùng.
- Mỗi checkpoint phải build/test backend tương ứng.

## 3. Quyết định kỹ thuật ban đầu

| Mã | Nội dung | Quyết định hiện tại |
|---|---|---|
| `CQRS-DEC-01` | Dùng package ngoài hay mediator nội bộ | Mặc định dùng mediator nội bộ nhỏ trong `AdminPortal.Application.Common.Mediator` để tránh thêm dependency/restore/version risk. ❓ Nếu muốn dùng package `MediatR` chính thức, cần xác nhận riêng trước khi đổi hướng. |
| `CQRS-DEC-02` | Phạm vi migrate trong một lượt | Migrate theo slice. Bước đầu tạo foundation và migrate `Users` để chứng minh pattern; các feature còn lại migrate sau theo task riêng. |
| `CQRS-DEC-03` | Giữ service interface cũ hay bỏ ngay | Giai đoạn chuyển tiếp có thể giữ service cũ nếu giúp giảm rủi ro. Khi slice đã chuyển sạch sang handler, controller không inject service đó nữa. |
| `CQRS-DEC-04` | Pipeline behavior | Chưa thêm pipeline logging/validation/transaction ở bước đầu. ❓ Sẽ cân nhắc sau khi 1-2 slice ổn định, tránh làm thay đổi lỗi/transaction semantics. |

## 4. Kiến trúc mục tiêu

```text
AdminPortal.Api
└─ Controllers
   └─ nhận HTTP → sender.Send(command/query)

AdminPortal.Application
├─ Common/Mediator
│  ├─ IAppCommand<TResponse>
│  ├─ IAppQuery<TResponse>
│  ├─ IAppRequestHandler<TRequest,TResponse>
│  └─ IAppMediator / DefaultAppMediator
└─ <Feature>
   ├─ Commands/<UseCase>Command.cs
   ├─ Queries/<UseCase>Query.cs
   └─ handler cùng file command/query nếu handler còn nhỏ
```

## 5. Lộ trình

1. `CQRS-BE-00`: tạo mediator foundation nội bộ, DI registration, migrate `Users` controller sang command/query.
2. `CQRS-BE-01`: migrate `Setup` và `Auth` sang command/query, giữ xử lý cookie/CSRF ở controller.
3. `CQRS-BE-02`: migrate `Students`, gồm group assignment và scoped read của Teacher.
4. `CQRS-BE-03`: migrate `Teachers` và `StudentGroups`.
5. `CQRS-BE-04`: migrate `Attendance`.
6. `CQRS-BE-05`: migrate `Assessments`, `AssessmentGroups`, `AssessmentSheets`, Google actions/upload PDF.
7. `CQRS-BE-06`: cleanup service interfaces cũ nếu không còn dùng, cập nhật docs/memory và chạy full backend gate.

## 6. Definition of Done

- Controller của slice đã migrate không inject service cũ trực tiếp.
- REST response/status code giữ nguyên.
- ProblemDetails/error code giữ nguyên.
- Unit/integration test hiện có vẫn pass hoặc được cập nhật đúng mục tiêu.
- Không có migration DB nếu chỉ refactor code.
- Docs task và memory backend/shared được cập nhật sau mỗi checkpoint.
