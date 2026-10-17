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

Trên máy app, dùng đúng tài khoản Windows sẽ chạy shortcut để chuẩn bị development certificate một lần (thao tác thủ công, launcher không tự đổi certificate store):

```powershell
dotnet dev-certs https --trust
dotnet dev-certs https --check --trust
```

Chấp nhận hộp thoại trust certificate của Windows nếu xuất hiện. Kiểm tra lại bằng `Invoke-WebRequest 'https://localhost:7158/health/live' -UseBasicParsing -TimeoutSec 10`; khi trả HTTP 200 thì bấm shortcut lại. Nếu vừa tạo/đổi certificate mới, API đang chạy có thể cần được khởi động lại thủ công để nạp certificate mới; nếu vẫn lỗi, kiểm tra certificate Kestrel thực sự đang dùng. Không dùng `--clean` hoặc bỏ TLS validation để xử lý mặc định. Tham khảo [dotnet dev-certs của Microsoft](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-dev-certs).

Copy `appsettings.Development.json` và OAuth Desktop `client_secret.json` vào `api/src/AdminPortal.Api/`. Tên cấu hình đúng là **`appsettings.Development.json`**. Dùng cùng OAuth Desktop client trên hai máy; đăng nhập tài khoản có quyền folder `packages`.

Package trên máy app được lấy từ **folder Drive đã đồng bộ local**, không tải bằng Google API. Sửa đường dẫn trong `deploy/spa/start-app.config.json` (cạnh `start-app.ps1`) nếu máy dùng ổ/thư mục khác:

```json
{
  "packagesDirectory": "G:\\My Drive\\scripts\\QLMamNonPackages\\packages"
}
```

Đường dẫn tương đối, nếu dùng, tính từ thư mục chứa file config. Có thể chọn file cấu hình khác bằng `-ConfigFile <path>`; đường dẫn config tương đối tính từ `deploy/spa`. Shortcut tạo với `-ConfigFile` sẽ giữ lựa chọn đó. `client_secret.json` trên máy app phục vụ tính năng Google của API; bước copy package local không cần OAuth.

Từ root clone trên máy app, tạo shortcut **một lần**:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\spa\start-app.ps1 -CreateDesktopShortcut
```

Lệnh trên chỉ tạo `GV Portal.lnk` trên Desktop user hiện tại, không chạy app. Shortcut gọi launcher PowerShell ẩn với đường dẫn tuyệt đối có quote, làm việc với source chứa khoảng trắng và không phụ thuộc working directory Desktop. Nếu chuyển clone sang chỗ khác, tạo lại shortcut.

Bấm **GV Portal** để đọc `start-app.config.json`, tìm/copy ZIP UI mới nhất trong folder local đã cấu hình, giữ API đang chạy hoặc **tự chạy API nền nếu chưa có**, chờ `https://localhost:7158/health/live` thành công tối đa 120 giây rồi mở:

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
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\spa\update-ui-from-folder.ps1 -IfNewer -LiveUpdate
```

- Liệt kê ZIP trong folder local từ `start-app.config.json`, chọn **timestamp trong tên**, không dùng thời gian sửa/copy/đồng bộ file. Lưu phiên bản tại `artifacts/spa/installed-ui.json` sau khi cài thành công.
- `-IfNewer` bỏ download/copy khi đã có bản bằng/mới hơn, không downgrade. Thiếu/hỏng trạng thái hoặc UI thiếu asset mà index tham chiếu thì cài lại bản mới nhất.
- Copy ZIP từ folder đồng bộ vào thư mục tạm trong source, đối chiếu size/MD5 với file nguồn rồi kiểm tra ZIP/build trước khi cài. ZIP còn đồng bộ/hỏng sẽ không thay UI đang dùng. Không sửa ZIP nguồn. Không chấp nhận ZIP ngoài `ClientApp/build`, traversal, symlink, tên trùng hoặc timestamp mới nhất trùng.
- `-LiveUpdate` giữ thư mục đang phục vụ, backup file bị thay trong `artifacts/spa/backups/`, copy asset trước và đổi index sau cùng. Giữ bundle cũ cho tab đang mở; lỗi copy thì khôi phục file ghi đè/xóa file mới, không dừng API.
- Không có package thì giữ UI hiện có. Ổ G:/folder chưa sẵn sàng, config lỗi hoặc copy/ZIP lỗi: launcher ghi log rồi vẫn tự chạy/giữ API nếu UI hiện có hợp lệ; chưa có UI hợp lệ thì dừng.
- Cổng 7158/5158 thuộc ứng dụng/source khác: dừng launcher và ghi log, không kill tiến trình đó. Khởi động lỗi/health timeout: không mở trình duyệt.
- Không tự sửa certificate store, credential/config, Git hoặc database. API vẫn thực hiện hành vi theo cấu hình sẵn có khi khởi động, gồm migration nếu cấu hình cho phép.
- Backup và bundle cũ được giữ để tự dọn khi không cần nữa và app đã dừng. Updater trực tiếp không có `-LiveUpdate` vẫn thay toàn bộ build như trước, chỉ dùng khi API đã dừng.

`-PassThru` trả object `{ Status, PackageName, InstalledTimestampUtc }`, với `Status` là `Updated`, `Current`, `NoPackage`; lỗi thực sự vẫn throw.

Log tại `artifacts/spa/runtime/`: `launcher.log` ghi các bước/fallback/lỗi, `update.log` ghi transcript update, `api-<UTC>.stdout.log` và `.stderr.log` ghi API mới do launcher start. API đã chạy từ trước giữ log của cách chạy ban đầu.

Nếu shortcut chưa mở trình duyệt, xem `launcher.log`. Health timeout sẽ ghi `Loi cuoi`: chưa có API mở cổng 7158, HTTP lỗi, hoặc lỗi HTTPS/chứng chỉ thực tế. Có thể chạy hiện cửa sổ để đọc lỗi ngay:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -NoExit -File .\deploy\spa\start-app.ps1
Get-Content .\artifacts\spa\runtime\launcher.log -Tail 30
```

Để phân biệt lỗi trình duyệt/SPA với lỗi PowerShell, kiểm tra HTTP trực tiếp trên máy app (không bỏ kiểm tra chứng chỉ):

```powershell
try {
  $r = Invoke-WebRequest 'https://localhost:7158/health/live' -UseBasicParsing -TimeoutSec 10
  $r.StatusCode
  $r.Headers['Content-Type']
  $r.BaseResponse.ResponseUri.AbsoluteUri
} catch { $_.Exception.GetBaseException().Message }
```

## OAuth và tên gói

- Máy nguồn upload vẫn dùng OAuth như trước: tìm `api/src/AdminPortal.Api/client_secret.json`, rồi `deploy/iis/client_secret.json`; có thể ghi đè `-ClientSecretFile`, `-TokenFile` (tương đối từ SourceRoot).
- Lần đầu upload mở trình duyệt cấp quyền Google trên máy nguồn (tối đa 5 phút), sau đó dùng refresh token `deploy/spa/upload.token.json` được Git bỏ qua. `-ReAuth` đăng nhập lại; scope `drive`, cần bật Google Drive API. Không thay đổi sharing của folder.
- Launcher/npm ui:update trên máy app chỉ đọc folder local, không dùng download token/quyền Drive API. Script cũ `update-ui-from-drive.ps1` vẫn giữ để gọi thủ công nếu cần luồng Google API (scope `drive.readonly`, token `download.token.json`); launcher không gọi nó.
- Tên ví dụ: `gv-portal-ui-20261017T093045123Z.zip` = **2026-10-17 09:30:45.123 UTC** (16:30:45.123 Việt Nam).
- Regex mặc định: `\Agv-portal-ui-(?<timestamp>\d{8}T\d{9}Z)\.zip\z`; format `yyyyMMdd'T'HHmmssfff'Z'`.
- Script cập nhật cho ghi đè `-PackageRegex` và `-TimestampFormat`; regex phải khớp toàn bộ tên và có group `timestamp`. Mặc định bỏ qua gói IIS, sidecar và tên/ngày không hợp lệ.
- Các entry script hỗ trợ `-WhatIf`, không chạy build, OAuth, copy package, thay UI, chạy API hoặc tạo shortcut. Google credential-smoke của API vẫn có thể mở bước OAuth cho tính năng Google của app.

Tài liệu Google dùng khi triển khai: [OAuth Desktop/PKCE](https://developers.google.com/identity/protocols/oauth2/native-app), [files.list và phân trang](https://developers.google.com/workspace/drive/api/reference/rest/v3/files/list), [upload resumable](https://developers.google.com/workspace/drive/api/guides/manage-uploads).

## Kiểm tra offline cho script

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\spa\tests\test-spa-package.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\spa\tests\test-app-launcher.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\spa\tests\test-local-ui-package.ps1
```

Kiểm tra dùng source/ZIP giả, HTTP/process/browser/shortcut mock trong `artifacts/spa-tests/`. Probe mutex chỉ chạy thêm một PowerShell ẩn với source giả. Không chạy API thật, không mở trình duyệt/tạo shortcut Desktop thật, không build production, không gọi Google hoặc thay ClientApp thực tế. Gate production trong `AGENTS.md` vẫn áp dụng khi agent thực thi build; việc thêm script không tự chạy gate hoặc luồng IIS.
