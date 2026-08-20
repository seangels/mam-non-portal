# 08 — Triển khai IIS local HTTPS

## 1. Mô hình triển khai

- Build/package thực hiện trên máy source/build.
- Máy triển khai là máy Windows 10 khác, đã có IIS và PostgreSQL 17.
- Máy đích nhận gói build đã đóng gói và tài liệu triển khai, không nhận source code.
- API và UI chạy thành hai IIS site/app pool riêng.

## 2. Domain và đường dẫn vật lý

| Thành phần | HTTPS hostname | Đường dẫn IIS |
|---|---|---|
| API | `api-gv-portal.local` | `C:\inetpub\api-gv-portal.local` |
| UI | `gv-portal.local` | `C:\inetpub\gv-portal.local` |

- Cả hai site dùng HTTPS cổng 443 với SNI.
- UI production gọi API qua `https://api-gv-portal.local/api/v1`.
- Hosts trên máy đích phải ánh xạ hai hostname local đúng địa chỉ cần dùng.

## 3. HTTPS local

- Gói deploy phải hỗ trợ tạo hoặc sử dụng certificate local có SAN cho cả hai hostname.
- Certificate phải được bind đúng hai IIS site và được trust trên máy sử dụng portal.
- Không expose binding HTTP công khai trong cấu hình production mặc định.
- Sau deploy phải xác minh không có lỗi certificate ở UI và API health endpoint.

## 4. Yêu cầu máy đích

- Windows 10 với IIS đã bật các thành phần cần thiết.
- .NET 10 Hosting Bundle.
- PostgreSQL 17 và database/user phù hợp.
- Windows PowerShell 5.1 chạy quyền Administrator khi deploy.
- Không yêu cầu .NET SDK, Node.js hoặc npm trên máy đích.

## 5. Gói bàn giao

Gói chuyển sang máy đích phải có:

- API đã publish.
- UI đã build cho IIS/production.
- Script triển khai IIS.
- Hướng dẫn Markdown cho chuẩn bị máy, deploy, cấu hình database/secret, hosts/certificate và rollback/kiểm tra.
- File checksum SHA-256 để kiểm tra gói không bị thay đổi khi chuyển máy.

Gói không được chứa:

- Source code không cần thiết.
- `.env`, mật khẩu, JWT signing key, private key hoặc connection string có credential thật.
- Development config, test artifact hoặc file debug không cần cho runtime.

## 6. Cấu hình bí mật

- PostgreSQL credential và JWT secret chỉ được nhập/cấu hình trên máy đích.
- Script deploy phải nhận secret theo cách không in rõ ra console hoặc ghi vào repository/package.
- CORS production chỉ cho phép origin `https://gv-portal.local` trừ khi người vận hành cấu hình thêm origin hợp lệ.
- Cookie refresh production phải giữ `Secure` và hoạt động qua hai HTTPS hostname.

## 7. Kết quả cần xác minh sau deploy

- `https://api-gv-portal.local/health/live` trả thành công.
- Readiness kết nối được PostgreSQL.
- `https://gv-portal.local` tải được UI không lỗi certificate.
- UI gọi đúng API HTTPS, đăng nhập/refresh/logout hoạt động với cookie và CSRF.
- Database migration/setup chạy đúng với database đích; hệ thống rỗng hiển thị màn khởi tạo SuperAdmin.
- IIS app pools/site bindings đúng và không phụ thuộc source/build tool.

## 8. Ranh giới thực thi

- Tài liệu này mô tả yêu cầu triển khai, không xác nhận máy đích đã được deploy.
- Build production, tạo package, verify package hoặc thay đổi IIS chỉ được thực hiện khi người dùng gọi rõ skill `$gv-portal-production` với mode phù hợp.
