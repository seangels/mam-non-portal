# Hướng dẫn deploy GV Portal lên IIS local bằng HTTPS

Tài liệu này dành cho máy Windows 10 đã có IIS và PostgreSQL 17.

## 1. Kết quả sau khi deploy

| Thành phần | IIS site/app pool | Thư mục | URL |
|---|---|---|---|
| API .NET 10 | api-gv-portal.local | C:\inetpub\api-gv-portal.local | https://api-gv-portal.local |
| Angular UI | gv-portal.local | C:\inetpub\gv-portal.local | https://gv-portal.local |

Script chỉ tạo HTTPS binding cổng 443 bằng SNI. Không tạo HTTP binding cổng 80.

Certificate mặc định là self-signed certificate dùng cho local, có SAN cho cả hai hostname. Script import certificate vào Local Computer / Trusted Root nên trình duyệt trên chính máy này không cảnh báo certificate.

## 2. File sử dụng

- deploy\iis\deploy-iis.ps1: build artifact hoặc deploy artifact đã build.
- deploy\iis\build-iis-package.ps1: chỉ dùng trong repository ở máy build để tạo ZIP; máy IIS đích không cần file này.
- deploy\iis\ui.web.config: cấu hình IIS static site và security headers cho Angular.
- ui\src\environments\environment.iis.ts: Angular gọi API qua https://api-gv-portal.local/api/v1.
- artifacts\iis\api: API artifact sau khi build.
- artifacts\iis\ui: UI artifact sau khi build.
- release\gv-portal-iis-*.zip: package hoàn chỉnh để copy sang máy deploy.

Thư mục artifacts và release đã được đưa vào .gitignore.

## 2.1. Luồng khuyến nghị: build máy A, deploy máy B

Trên máy có source code, build package mới:

    Set-ExecutionPolicy -Scope Process Bypass
    .\deploy\iis\build-iis-package.ps1

Nếu artifact vừa được build và đã kiểm tra, chỉ đóng gói lại mà không build lần nữa:

    .\deploy\iis\build-iis-package.ps1 -UseExistingArtifacts

Kết quả gồm hai file:

    release\gv-portal-iis-YYYYMMDD-HHMMSS.zip
    release\gv-portal-iis-YYYYMMDD-HHMMSS.zip.sha256

Package chứa API Release, Angular IIS bundle, deploy script, hướng dẫn và BUILD-INFO.txt. Package không chứa source code, node_modules, PostgreSQL password hoặc JWT signing key.

Copy cả ZIP và file sha256 sang máy IIS. Trên máy đích, kiểm tra checksum trước khi giải nén:

    $zip = "C:\deploy\gv-portal-iis-YYYYMMDD-HHMMSS.zip"
    $expected = ((Get-Content ($zip + ".sha256")) -split "\s+")[0]
    $actual = (Get-FileHash $zip -Algorithm SHA256).Hash
    if ($actual -ne $expected) { throw "Package checksum mismatch" }

Giải nén:

    Expand-Archive $zip -DestinationPath C:\deploy -Force
    Set-Location C:\deploy\gv-portal-iis-YYYYMMDD-HHMMSS
    Get-ChildItem -Recurse | Unblock-File

Từ thư mục vừa giải nén, chạy deploy-iis.ps1 theo mục 6. Không dùng tham số Build trên máy IIS đích. Máy đích không cần source, .NET SDK, Node hoặc npm.

## 3. Điều kiện cần

### Trên máy build

- .NET SDK 10.
- Node.js 18 hoặc 20.
- npm.

Nếu build ngay trên máy IIS thì máy IIS cũng phải có các công cụ trên. Nếu build từ máy khác, chỉ cần chuyển thư mục artifacts\iis sang máy IIS.

### Trên máy IIS

- IIS với Static Content và Default Document.
- .NET 10 Hosting Bundle, gồm ASP.NET Core Runtime và AspNetCoreModuleV2.
- PostgreSQL 17 đang chạy.
- Windows PowerShell 5.1 chạy bằng Run as administrator.

Kiểm tra ASP.NET Core Module:

    Import-Module WebAdministration
    Get-WebGlobalModule | Where-Object Name -eq AspNetCoreModuleV2

Nếu không có kết quả, cài .NET 10 Hosting Bundle. Nếu Hosting Bundle đã được cài trước IIS, chạy Repair Hosting Bundle sau khi bật IIS rồi restart IIS.

Tài liệu Microsoft:

- [Host ASP.NET Core on IIS](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/?view=aspnetcore-10.0)
- [.NET Hosting Bundle](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/hosting-bundle?view=aspnetcore-10.0)

## 4. Chuẩn bị PostgreSQL 17

API tự apply EF Core migration nhưng không tự tạo database hoặc PostgreSQL role.

Kiểm tra dịch vụ nào đang giữ cổng 5432:

    Get-NetTCPConnection -LocalPort 5432 -State Listen

Docker Compose của project cũng expose PostgreSQL ra cổng 5432. Nếu IIS sẽ dùng PostgreSQL 17 cài trực tiếp trên Windows, dừng container cũ trước để tránh kết nối nhầm database:

    Set-Location .\api
    docker compose stop api postgres

Lệnh stop không xóa volume/dữ liệu Docker. Nếu cần chạy song song, cấu hình PostgreSQL Windows ở cổng khác và truyền đúng PostgresPort cho deploy script.

Mở PowerShell hoặc Command Prompt, chạy psql bằng tài khoản postgres:

    & "C:\Program Files\PostgreSQL\17\bin\psql.exe" -U postgres -d postgres

Tạo role và database riêng. Thay mật khẩu mẫu bằng mật khẩu mạnh:

    CREATE ROLE gv_portal_app
      WITH LOGIN
      PASSWORD 'replace-with-a-strong-database-password';

    CREATE DATABASE gv_portal
      OWNER gv_portal_app
      ENCODING 'UTF8';

    \connect gv_portal
    GRANT ALL ON SCHEMA public TO gv_portal_app;
    \quit

Kiểm tra đăng nhập:

    & "C:\Program Files\PostgreSQL\17\bin\psql.exe" -h localhost -p 5432 -U gv_portal_app -d gv_portal

Không dùng tài khoản postgres superuser cho API.

## 5. Build artifact

Phần này chỉ chạy trên máy có source code. Mở PowerShell tại thư mục repository.

Build sạch nhưng chưa thay đổi IIS:

    Set-ExecutionPolicy -Scope Process Bypass
    .\deploy\iis\deploy-iis.ps1 -Build -PrepareOnly

Lệnh trên chạy:

- dotnet publish Release cho API.
- npm ci.
- Angular build với configuration iis.
- Kiểm tra UI artifact thực sự trỏ tới API HTTPS local.

Nếu ui\node_modules đã được cài đúng theo package-lock.json và muốn bỏ qua npm ci:

    .\deploy\iis\deploy-iis.ps1 -Build -PrepareOnly -SkipNpmInstall

Artifact tạo tại:

    artifacts\iis\api
    artifacts\iis\ui

## 6. Deploy artifact đã build

Mở Windows PowerShell bằng Run as administrator tại thư mục repository hoặc thư mục package đã giải nén.

Đọc PostgreSQL password và JWT signing key bằng SecureString để không ghi plaintext vào command history:

    $pgPassword = Read-Host "PostgreSQL password" -AsSecureString
    $jwtKey = Read-Host "JWT signing key" -AsSecureString

JWT signing key phải ổn định giữa các lần deploy và dài tối thiểu 32 ký tự. Đổi key sẽ làm các access token hiện tại mất hiệu lực.

Deploy artifact:

    .\deploy\iis\deploy-iis.ps1 -PostgresHost localhost -PostgresPort 5432 -PostgresDatabase gv_portal -PostgresUsername gv_portal_app -PostgresPassword $pgPassword -JwtSigningKey $jwtKey

Hoặc build và deploy trong một lệnh:

    .\deploy\iis\deploy-iis.ps1 -Build -PostgresDatabase gv_portal -PostgresUsername gv_portal_app -PostgresPassword $pgPassword -JwtSigningKey $jwtKey

Script sẽ:

1. Dừng hai IIS site/app pool nếu đã tồn tại.
2. Mirror artifact vào đúng hai thư mục C:\inetpub được chỉ định.
3. Ghi cấu hình Production, PostgreSQL, JWT, CORS và AllowedHosts vào web.config tại máy deploy.
4. Tạo hai app pool No Managed Code, 64-bit, ApplicationPoolIdentity.
5. Cấp quyền filesystem đúng cho từng app pool.
6. Tạo hoặc dùng lại certificate local có SAN cho cả hai hostname.
7. Import self-signed certificate vào Trusted Root của Local Computer.
8. Thêm hai hostname vào Windows hosts file, trỏ về 127.0.0.1.
9. Tạo HTTPS SNI binding cổng 443.
10. Khởi động site và kiểm tra API readiness, PostgreSQL, setup status và UI.

Script dùng Robocopy /MIR. File đặt thủ công trong hai thư mục deploy có thể bị xóa ở lần deploy tiếp theo. Thư mục API logs được giữ lại.

## 7. Dùng certificate có sẵn

Nếu đã có certificate từ internal CA, certificate phải:

- Nằm trong Cert:\LocalMachine\My.
- Có private key.
- Có SAN cho api-gv-portal.local và gv-portal.local.
- Chưa hết hạn.

Lấy thumbprint:

    Get-ChildItem Cert:\LocalMachine\My | Select-Object Subject, Thumbprint, NotAfter, HasPrivateKey

Deploy bằng certificate đó:

    .\deploy\iis\deploy-iis.ps1 -CertificateThumbprint "CERTIFICATE_THUMBPRINT" -PostgresPassword $pgPassword -JwtSigningKey $jwtKey

Script dùng SNI để hai hostname cùng chia sẻ cổng 443. Tham khảo [New-WebBinding và SNI](https://learn.microsoft.com/en-us/powershell/module/webadministration/new-webbinding).

Self-signed certificate chỉ phù hợp local/test/intranet được kiểm soát. Máy khác sẽ không tự tin cậy certificate này. Muốn truy cập từ máy khác cần DNS phù hợp và certificate do CA mà máy khách tin cậy cấp.

## 8. Khởi tạo SuperAdmin lần đầu

Mở:

    https://gv-portal.local

Nếu database chưa có bất kỳ user nào, UI tự chuyển đến:

    https://gv-portal.local/#/setup

Nhập email, họ tên và mật khẩu mạnh để tạo SuperAdmin đầu tiên. Sau khi database đã có user, setup API bị khóa và yêu cầu tạo lần hai trả 409 Conflict.

Nên thực hiện bước khởi tạo trước khi cho máy khác truy cập hệ thống.

## 9. Kiểm tra sau deploy

    Invoke-RestMethod https://api-gv-portal.local/health/live
    Invoke-RestMethod https://api-gv-portal.local/health/ready
    Invoke-RestMethod https://api-gv-portal.local/api/v1/setup/status

Kiểm tra IIS:

    Import-Module WebAdministration
    Get-Website | Where-Object Name -in @("api-gv-portal.local", "gv-portal.local")
    Get-WebBinding -Name api-gv-portal.local
    Get-WebBinding -Name gv-portal.local

Ở môi trường Production, OpenAPI hiện không được public. Health endpoints và API nghiệp vụ vẫn hoạt động bình thường.

## 10. Secret và file cấu hình

PostgreSQL password và JWT signing key không được ghi vào source code hoặc artifacts. Chúng chỉ được chèn vào:

    C:\inetpub\api-gv-portal.local\web.config

File web.config bị IIS chặn truy cập từ HTTP nhưng Windows Administrator vẫn có thể đọc. Chỉ cấp quyền máy chủ cho người quản trị cần thiết và không copy file này vào Git, email hoặc nơi backup không mã hóa.

Mỗi lần redeploy phải nhập lại cùng JWT signing key. Nên lưu key trong password manager.

## 11. Backup và rollback

Trước khi deploy bản mới:

1. Chạy pg_dump cho database.
2. Lưu lại artifacts\iis của bản đang chạy bằng mã phiên bản hoặc ngày build.
3. Không dùng thư mục C:\inetpub làm nơi lưu artifact gốc.

Ví dụ backup PostgreSQL:

    New-Item C:\backup\gv-portal -ItemType Directory -Force
    & "C:\Program Files\PostgreSQL\17\bin\pg_dump.exe" -h localhost -p 5432 -U gv_portal_app -d gv_portal -F c -f C:\backup\gv-portal\gv_portal_before_deploy.dump

Rollback application:

1. Đưa artifact phiên bản cũ trở lại artifacts\iis\api và artifacts\iis\ui.
2. Chạy lại deploy script với cùng PostgreSQL password và JWT signing key.
3. Kiểm tra health/readiness.

Script không tự rollback database migration. Nếu phiên bản cũ không tương thích schema mới, phải dùng quy trình database restore hoặc migration rollback đã được kiểm thử riêng.

## 12. Troubleshooting

### Lỗi 500.30, 500.31 hoặc thiếu AspNetCoreModuleV2

- Cài/Repair .NET 10 Hosting Bundle.
- Restart IIS hoặc restart máy.
- Kiểm tra Event Viewer, Windows Logs, Application.

### API readiness trả lỗi

- Kiểm tra PostgreSQL service.
- Kiểm tra database gv_portal và role gv_portal_app.
- Kiểm tra pg_hba.conf cho kết nối localhost.
- Kiểm tra password nhập lúc deploy.
- Xem IIS log tại C:\inetpub\logs\LogFiles.

### Browser báo certificate không tin cậy

- Mở certlm.msc.
- Kiểm tra certificate GV Portal local HTTPS trong Personal và Trusted Root Certification Authorities.
- Đóng toàn bộ browser rồi mở lại.
- Chạy ipconfig /flushdns nếu hostname chưa resolve.

PowerShell tạo self-signed certificate bằng API được Microsoft mô tả tại [New-SelfSignedCertificate](https://learn.microsoft.com/en-us/powershell/module/pki/new-selfsignedcertificate).

### UI mở được nhưng gọi API lỗi CORS

- UI phải được mở đúng bằng https://gv-portal.local.
- Không dùng http://localhost hoặc IP để mở UI.
- Kiểm tra deployed API web.config có Security__AllowedOrigins__0 bằng https://gv-portal.local.
- Redeploy bằng script thay vì sửa thủ công rồi quên cấu hình.

### HTTP không truy cập được

Đây là hành vi chủ đích. Script chỉ bind HTTPS cổng 443. Dùng:

    https://gv-portal.local
    https://api-gv-portal.local

### Cần giữ hosts file hoặc bỏ health check tự động

Chỉ dùng khi DNS/certificate đã được quản lý bên ngoài:

    .\deploy\iis\deploy-iis.ps1 -SkipHostsFile -SkipHealthCheck -CertificateThumbprint "CERTIFICATE_THUMBPRINT" -PostgresPassword $pgPassword -JwtSigningKey $jwtKey

## 13. Phạm vi triển khai

Cấu hình này phù hợp cho local production hoặc intranet trên một máy Windows 10. Nếu public Internet, nên chuyển sang Windows Server hoặc hạ tầng server được quản lý, dùng DNS thật, certificate CA thật, backup tự động, monitoring và quy trình rotate secret.
