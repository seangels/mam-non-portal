# Theo dõi tiến độ — Gói UI cho API chạy từ source

| Status | Mã | Việc cần làm |
|---|---|---|
| `[x]` | `SPA-PKG-01` | Build UI, copy vào ClientApp/build, ZIP và upload Drive từ máy source |
| `[x]` | `SPA-UPD-01` | Máy chạy app tải gói mới nhất theo regex/timestamp và cập nhật ClientApp/build |
| `[x]` | `SPA-RUN-01` | Npm root + shortcut chạy nền: cập nhật UI nếu mới, giữ/chạy API, mở credential-smoke |
| `[x]` | `SPA-LOCAL-01` | Máy app lấy ZIP từ Drive đồng bộ local, đường dẫn trong start-app.config.json |

## Phạm vi và quyết định

- Bốn entry script PowerShell 5.1 trong `deploy/spa/`; máy app dùng start-app/shortcut không cần Node/npm. Npm root build:spa upload như cũ; ui:update/launcher dùng update-ui-from-folder đọc `start-app.config.json`, mặc định `G:\My Drive\scripts\QLMamNonPackages\packages`. Script Google API cũ chỉ để gọi thủ công.
- Drive folder `1EJK12xqBWBlsVchCeXUlwYOpouzZ_D_B` (`packages`). Gói UI có prefix riêng, timestamp UTC trong tên; không chọn theo ngày sửa Drive.
- UI production dùng `/api/v1` cùng host; API hiện có `Spa:ServeFromClientAppBuild` và `Spa:BuildPath`.
- Chỉ gói `ClientApp/build`; giữ cấu hình Development, credential và source API trên máy đích.
- Kiểm thử offline bằng fixture/mô phỏng Drive; chưa thực thi production build, upload thật hoặc cập nhật máy app.
- Launcher/shortcut chạy nền: giữ API kể cả khi cập nhật UI, copy asset trước/index sau và giữ bundle cũ. Folder/config/copy lỗi dùng UI hợp lệ hiện có và tự start API nếu chưa chạy. Khóa chống bấm đồng thời; kiểm process source/cổng, chờ HTTPS health rồi mở URL credential-smoke đã chốt; log runtime. Không OAuth/HTTP lấy package trên máy app.

## Kiểm tra

- Windows PowerShell 5.1: `test-spa-package.ps1` **71**, `test-app-launcher.ps1` **37**, `test-local-ui-package.ps1` **12**, tổng **120 assertions pass**.
- AST parse 10 script pass. `git diff --check` pass. Git ignore giữ token/artifacts/release ngoài Git.
- Build orchestration được chạy bằng npm giả lập trong clone fixture: `npm ci`, cấu hình production rõ ràng, output riêng, copy/ZIP; lỗi npm exit 7 giữ nguyên build cũ và không tạo package mới.
- Updater chạy đầy đủ bằng HTTP mock: refresh token → phân trang Drive → timestamp selection → download → size/MD5 → ZIP validation → install. Có kiểm tra khôi phục build khi swap lỗi, giữ config/credential, bỏ stale asset ở chế độ thay toàn bộ và không ghi file/HTTP với `-WhatIf`.
- Mới: same/older/newer/missing/corrupt state và UI hỏng, NoPackage, live index-last/giữ bundle/rollback, không ghi state khi download lỗi. Launcher mock startup/port/health/browser/COM, giữ API/fallback, path khoảng trắng; mutex chống hai process được kiểm tra bằng PowerShell con thật.
- Local: cấu hình mặc định/chỉnh directory, custom config được launcher/shortcut truyền đúng, chọn tên thay modified time, không HTTP/OAuth, không sửa ZIP nguồn, ZIP chưa sync/hỏng giữ UI/state, folder thiếu/JSON lỗi được báo để fallback. Launcher vẫn tự chạy API khi folder lỗi và UI có sẵn.
- Cả ba npm alias và `start-app.ps1 -CreateDesktopShortcut` chạy native `-WhatIf` pass. Sửa lỗi SourceRoot default param khi PS5.1 `-File`, và null backup File.Replace trên PS5.1 bằng `NullString.Value`.
- Không chạy production build, OAuth/upload Google thật, API/shortcut Desktop thật, IIS, ổ G: thật hoặc máy app. Backend/frontend test/build không chạy vì không đổi source ứng dụng; kiểm tra tập trung vào script triển khai/launcher.
