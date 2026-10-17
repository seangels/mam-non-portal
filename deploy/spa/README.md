# UI package cho API chạy từ source

Hai entry script chạy trên Windows PowerShell 5.1, từ bất kỳ working directory nào. `GoogleDrive.ps1` và `SpaPackage.ps1` là helper đi cùng trong Git.

## 1. Máy source/build

Máy này cần Node portable **14.21.3 + npm 8.19.4** tại `ui/.tools/node-v14.21.3-win-x64` như hướng dẫn `ui/README.md` và OAuth Desktop `client_secret.json`.

Từ root repository:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\spa\build-ui-package.ps1
```

Script đăng nhập Drive nếu cần, kích hoạt Node portable, chạy `npm ci`, build UI với cấu hình **production** (`apiBaseUrl: '/api/v1'`), copy vào `api/src/AdminPortal.Api/ClientApp/build`, tạo `release/spa/gv-portal-ui-<timestamp>.zip` rồi upload ZIP vào [folder packages](https://drive.google.com/drive/folders/1EJK12xqBWBlsVchCeXUlwYOpouzZ_D_B). Upload được kiểm tra lại bằng metadata kích thước/MD5 trên Drive. File SHA-256 nằm cạnh ZIP trên máy build để đối chiếu thủ công; script cập nhật dùng MD5 do Drive cung cấp, không cần sidecar.

ZIP chỉ có `ClientApp/build/**`. Build vào thư mục tạm mới mỗi lần; lỗi build không dùng lại `dist` cũ. Build trước trên máy nguồn cũng được giữ trong `artifacts/spa/backups/`.

Tùy chọn:

```powershell
# Đã có node_modules đúng package-lock, bỏ npm ci:
.\deploy\spa\build-ui-package.ps1 -SkipNpmInstall

# Build/copy/ZIP, không đăng nhập hay upload Drive:
.\deploy\spa\build-ui-package.ps1 -SkipUpload

# Xem thao tác dự kiến, không chạy build hoặc OAuth:
.\deploy\spa\build-ui-package.ps1 -WhatIf
```

## 2. Máy chạy app

Clone/pull repository để có `deploy/spa/`; cài .NET SDK 10, chuẩn bị PostgreSQL và cấu hình API như bình thường. Máy này **không cần Node/npm**.

Copy `appsettings.Development.json` và OAuth Desktop `client_secret.json` vào `api/src/AdminPortal.Api/`. Tên cấu hình đúng là **`appsettings.Development.json`**. Dùng cùng OAuth Desktop client trên hai máy; đăng nhập tài khoản có quyền folder `packages`.

Dừng API trước khi thay UI để tránh trình duyệt đang tải dở các bundle của bản cũ. Chạy từ root clone trên máy app:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\spa\update-ui-from-drive.ps1
```

Script duyệt **tất cả trang** file trong folder, lọc regex và chọn **timestamp trong tên mới nhất**. Không chọn theo thời gian modified/upload của Drive. Tải ZIP, kiểm tra size/MD5, kiểm tra toàn bộ đường dẫn ZIP và `index.html`/`main*.js` rồi mới thay `ClientApp/build`. Không chấp nhận ZIP chứa file ngoài `ClientApp/build`, đường dẫn thoát thư mục, symlink, tên trùng hoặc timestamp mới nhất bị trùng giữa nhiều gói.

Build cũ được giữ tại đường dẫn in ra trong `artifacts/spa/backups/build-...`. Nếu bước chuyển build mới lỗi, script chuyển build cũ trở lại. Các bản backup giữ lại để vận hành tự dọn khi không còn cần.

Bật SPA bằng biến môi trường trước khi chạy API (hoặc thêm hai key tương đương vào mục `Spa` trong cấu hình đang dùng):

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:Spa__ServeFromClientAppBuild = 'true'
$env:Spa__BuildPath = 'ClientApp/build'
dotnet run --project .\api\src\AdminPortal.Api --no-launch-profile
```

Nếu chạy API bằng service/task, cấu hình các biến trên trong môi trường của service/task. UI gọi `/api/v1` trên cùng host/cổng API. Có thể cập nhật clone khác bằng `-SourceRoot D:\apps\api-portal`.

## OAuth và tên gói

- Mặc định tìm `api/src/AdminPortal.Api/client_secret.json`, rồi `deploy/iis/client_secret.json`; có thể ghi đè bằng `-ClientSecretFile <path>`. Đường dẫn tương đối tính từ `SourceRoot`.
- Lần đầu mỗi máy mở trình duyệt trên **chính máy đó** để đăng nhập Google, thời hạn 5 phút. Các lần sau dùng refresh token; lỗi token có thể xử lý bằng `-ReAuth`.
- Token upload/download tách riêng tại `deploy/spa/upload.token.json` và `deploy/spa/download.token.json`, được Git bỏ qua. Không cần copy token qua máy app. `-TokenFile <path>` cho phép chọn vị trí khác; giữ file ngoài Git.
- Máy build yêu cầu scope `drive` để ghi vào folder đã có; máy app yêu cầu `drive.readonly`. Cần bật Google Drive API cho OAuth project. Không thay đổi sharing của folder.
- Tên ví dụ: `gv-portal-ui-20261017T093045123Z.zip` = **2026-10-17 09:30:45.123 UTC** (16:30:45.123 Việt Nam).
- Regex mặc định: `\Agv-portal-ui-(?<timestamp>\d{8}T\d{9}Z)\.zip\z`; format `yyyyMMdd'T'HHmmssfff'Z'`.
- Script cập nhật cho ghi đè `-PackageRegex` và `-TimestampFormat`; regex phải khớp toàn bộ tên và có group `timestamp`. Mặc định bỏ qua gói IIS, sidecar và tên/ngày không hợp lệ.
- `-WhatIf` ở cả hai entry script không chạy build, OAuth hoặc thay file.

Tài liệu Google dùng khi triển khai: [OAuth Desktop/PKCE](https://developers.google.com/identity/protocols/oauth2/native-app), [files.list và phân trang](https://developers.google.com/workspace/drive/api/reference/rest/v3/files/list), [upload resumable](https://developers.google.com/workspace/drive/api/guides/manage-uploads).

## Kiểm tra offline cho script

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\spa\tests\test-spa-package.ps1
```

Kiểm tra dùng source/ZIP giả và HTTP mock trong `artifacts/spa-tests/`, không build production, không đăng nhập/upload Drive, không thay ClientApp thực tế. Gate production trong `AGENTS.md` vẫn áp dụng khi agent được yêu cầu thực thi build; việc thêm các script này không tự chạy gate hoặc luồng IIS.
