# Theo dõi tiến độ — Gói UI cho API chạy từ source

| Status | Mã | Việc cần làm |
|---|---|---|
| `[x]` | `SPA-PKG-01` | Build UI, copy vào ClientApp/build, ZIP và upload Drive từ máy source |
| `[x]` | `SPA-UPD-01` | Máy chạy app tải gói mới nhất theo regex/timestamp và cập nhật ClientApp/build |

## Phạm vi và quyết định

- Hai entry script PowerShell 5.1 trong `deploy/spa/`; máy app không cần Node/npm.
- Drive folder `1EJK12xqBWBlsVchCeXUlwYOpouzZ_D_B` (`packages`). Gói UI có prefix riêng, timestamp UTC trong tên; không chọn theo ngày sửa Drive.
- UI production dùng `/api/v1` cùng host; API hiện có `Spa:ServeFromClientAppBuild` và `Spa:BuildPath`.
- Chỉ gói `ClientApp/build`; giữ cấu hình Development, credential và source API trên máy đích.
- Kiểm thử offline bằng fixture/mô phỏng Drive; chưa thực thi production build, upload thật hoặc cập nhật máy app.

## Kiểm tra

- Windows PowerShell 5.1: `powershell -NoProfile -ExecutionPolicy Bypass -File deploy/spa/tests/test-spa-package.ps1` — **51 assertions pass**.
- AST parse 5 script pass. `git diff --check` pass. Git ignore xác nhận token, artifacts và release không được theo dõi.
- Build orchestration được chạy bằng npm giả lập trong clone fixture: `npm ci`, cấu hình production rõ ràng, output riêng, copy/ZIP; lỗi npm exit 7 giữ nguyên build cũ và không tạo package mới.
- Updater chạy đầy đủ bằng HTTP mock: refresh token → phân trang Drive → timestamp selection → download → size/MD5 → ZIP validation → install. Có kiểm tra khôi phục build khi swap lỗi, giữ config/credential, không giữ stale asset, không ghi file/HTTP với `-WhatIf`.
- Không chạy production build, OAuth/upload Google thật, IIS, hoặc máy app. Backend/frontend test/build không chạy vì không đổi source ứng dụng; kiểm tra tập trung vào hai script mới.
