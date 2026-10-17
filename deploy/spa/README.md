# UI package và launcher GV Portal

Các script chạy trên Windows PowerShell 5.1. Lệnh npm chạy từ root repository; máy app dùng launcher/shortcut trực tiếp không cần Node/npm. Các helper đi cùng trong Git.

## 1. Máy source/build

Máy này cần Node portable **14.21.3 + npm 8.19.4** tại `ui/.tools/node-v14.21.3-win-x64` như hướng dẫn `ui/README.md`, `node_modules` đúng lockfile và OAuth Desktop `client_secret.json`. Nếu chưa cài dependency, chạy `npm run ui -- ci` một lần trước.

Từ root repository:

```powershell
npm run build:spa
```

Lệnh npm gọi builder với `-SkipNpmInstall`, **không chạy lại npm ci**, upload bật mặc định. Script đăng nhập Drive nếu cần, kích hoạt Node portable, build UI với cấu hình **production** (`apiBaseUrl: '/api/v1'`), copy vào `api/src/AdminPortal.Api/ClientApp/build`, tạo `release/spa/gv-portal-ui-<timestamp>.zip` rồi upload ZIP vào [folder packages](https://drive.google.com/drive/folders/1EJK12xqBWBlsVchCeXUlwYOpouzZ_D_B). Upload được kiểm tra lại bằng metadata kích thước/MD5 trên Drive. File SHA-256 nằm cạnh ZIP trên máy build để đối chiếu thủ công; script cập nhật dùng MD5 do Drive cung cấp, không cần sidecar.

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

Clone/pull repository để có `deploy/spa/`; cài .NET SDK 10, chuẩn bị PostgreSQL, cấu hình API và HTTPS development certificate hợp lệ/được trình duyệt tin cậy. Máy này **không cần Node/npm**.

Copy `appsettings.Development.json` và OAuth Desktop `client_secret.json` vào `api/src/AdminPortal.Api/`. Tên cấu hình đúng là **`appsettings.Development.json`**. Dùng cùng OAuth Desktop client trên hai máy; đăng nhập tài khoản có quyền folder `packages`.

Từ root clone trên máy app, tạo shortcut **một lần**:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\spa\start-app.ps1 -CreateDesktopShortcut
```

Lệnh trên chỉ tạo `GV Portal.lnk` trên Desktop user hiện tại, không chạy app. Shortcut gọi launcher PowerShell ẩn với đường dẫn tuyệt đối có quote, làm việc với source chứa khoảng trắng và không phụ thuộc working directory Desktop. Nếu chuyển clone sang chỗ khác, tạo lại shortcut.

Bấm **GV Portal** để tự kiểm tra/copy UI mới, giữ API đang chạy hoặc chạy API nền nếu chưa có, chờ `https://localhost:7158/health/live` thành công tối đa 120 giây rồi mở:

`https://localhost:7158/api/v1/google-sheets/credential-smoke?return=%2F`

Không hiện cửa sổ console API. Launcher kết thúc sau khi mở trình duyệt, API tiếp tục chạy nền. Các lần bấm sau vẫn kiểm tra UI và mở trình duyệt; **không restart API kể cả có UI mới**. Khóa theo source root ngăn hai lượt launcher đồng thời. Nếu có npm, có thể gọi `npm run app:start`; hoặc chạy trực tiếp:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File .\deploy\spa\start-app.ps1
```

API mới được launcher bật Development, `Spa__ServeFromClientAppBuild=true`, `Spa__BuildPath=ClientApp/build`, HTTPS 7158 + HTTP localhost 5158. API chạy từ trước cần bật SPA từ đầu; launcher không thay môi trường tiến trình đang chạy. `-SourceRoot D:\apps\api-portal` cho phép chọn clone khác.

## Chỉ cập nhật UI và xử lý lỗi

```powershell
npm run ui:update

# Máy không có Node/npm:
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\spa\update-ui-from-drive.ps1 -IfNewer -LiveUpdate
```

- Duyệt **tất cả trang** Drive, chọn **timestamp trong tên**, không dùng thời gian modified/upload. Lưu phiên bản tại `artifacts/spa/installed-ui.json` sau khi cài thành công.
- `-IfNewer` bỏ download/copy khi đã có bản bằng/mới hơn, không downgrade. Thiếu/hỏng trạng thái hoặc UI thiếu asset mà index tham chiếu thì cài lại bản mới nhất.
- Kiểm tra size/MD5, đường dẫn ZIP và build trước khi cài. Không chấp nhận ZIP ngoài `ClientApp/build`, traversal, symlink, tên trùng hoặc timestamp mới nhất trùng.
- `-LiveUpdate` giữ thư mục đang phục vụ, backup file bị thay trong `artifacts/spa/backups/`, copy asset trước và đổi index sau cùng. Giữ bundle cũ cho tab đang mở; lỗi copy thì khôi phục file ghi đè/xóa file mới, không dừng API.
- Không có package thì giữ UI hiện có. Drive/OAuth/download/copy lỗi: launcher ghi log rồi vẫn chạy app nếu UI hiện có hợp lệ; chưa có UI hợp lệ thì dừng.
- Cổng 7158/5158 thuộc ứng dụng/source khác: dừng launcher và ghi log, không kill tiến trình đó. Khởi động lỗi/health timeout: không mở trình duyệt.
- Không tự sửa certificate store, credential/config, Git hoặc database. API vẫn thực hiện hành vi theo cấu hình sẵn có khi khởi động, gồm migration nếu cấu hình cho phép.
- Backup và bundle cũ được giữ để tự dọn khi không cần nữa và app đã dừng. Updater trực tiếp không có `-LiveUpdate` vẫn thay toàn bộ build như trước, chỉ dùng khi API đã dừng.

`-PassThru` trả object `{ Status, PackageName, InstalledTimestampUtc }`, với `Status` là `Updated`, `Current`, `NoPackage`; lỗi thực sự vẫn throw.

Log tại `artifacts/spa/runtime/`: `launcher.log` ghi các bước/fallback/lỗi, `update.log` ghi transcript update, `api-<UTC>.stdout.log` và `.stderr.log` ghi API mới do launcher start. API đã chạy từ trước giữ log của cách chạy ban đầu.

## OAuth và tên gói

- Mặc định tìm `api/src/AdminPortal.Api/client_secret.json`, rồi `deploy/iis/client_secret.json`; có thể ghi đè bằng `-ClientSecretFile <path>`. Đường dẫn tương đối tính từ `SourceRoot`.
- Lần đầu mỗi máy mở trình duyệt trên **chính máy đó** để đăng nhập Google, thời hạn 5 phút. Các lần sau dùng refresh token; lỗi token có thể xử lý bằng `-ReAuth`.
- Token upload/download tách riêng tại `deploy/spa/upload.token.json` và `deploy/spa/download.token.json`, được Git bỏ qua. Không cần copy token qua máy app. `-TokenFile <path>` cho phép chọn vị trí khác; giữ file ngoài Git.
- Máy build yêu cầu scope `drive` để ghi vào folder đã có; máy app yêu cầu `drive.readonly`. Cần bật Google Drive API cho OAuth project. Không thay đổi sharing của folder.
- Tên ví dụ: `gv-portal-ui-20261017T093045123Z.zip` = **2026-10-17 09:30:45.123 UTC** (16:30:45.123 Việt Nam).
- Regex mặc định: `\Agv-portal-ui-(?<timestamp>\d{8}T\d{9}Z)\.zip\z`; format `yyyyMMdd'T'HHmmssfff'Z'`.
- Script cập nhật cho ghi đè `-PackageRegex` và `-TimestampFormat`; regex phải khớp toàn bộ tên và có group `timestamp`. Mặc định bỏ qua gói IIS, sidecar và tên/ngày không hợp lệ.
- Các entry script hỗ trợ `-WhatIf`, không chạy build, OAuth, thay UI, chạy API hoặc tạo shortcut. Launcher ẩn vẫn cần trình duyệt cấp quyền Drive lần đầu trên từng máy; dùng updater với `-ReAuth` khi cần đăng nhập lại.

Tài liệu Google dùng khi triển khai: [OAuth Desktop/PKCE](https://developers.google.com/identity/protocols/oauth2/native-app), [files.list và phân trang](https://developers.google.com/workspace/drive/api/reference/rest/v3/files/list), [upload resumable](https://developers.google.com/workspace/drive/api/guides/manage-uploads).

## Kiểm tra offline cho script

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\spa\tests\test-spa-package.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\spa\tests\test-app-launcher.ps1
```

Kiểm tra dùng source/ZIP giả, HTTP/process/browser/shortcut mock trong `artifacts/spa-tests/`. Probe mutex chỉ chạy thêm một PowerShell ẩn với source giả. Không chạy API thật, không mở trình duyệt/tạo shortcut Desktop thật, không build production, không gọi Google hoặc thay ClientApp thực tế. Gate production trong `AGENTS.md` vẫn áp dụng khi agent thực thi build; việc thêm script không tự chạy gate hoặc luồng IIS.
