# Theo dõi triển khai Admin Portal

Danh mục kế hoạch: [`plans/README.md`](plans/README.md). Contract nền: [`plans/01-BASE-admin-portal.md`](plans/01-BASE-admin-portal.md).

## Quy ước trạng thái

- `[ ]` Chưa bắt đầu
- `[~]` Đang thực hiện
- `[x]` Hoàn thành và đã kiểm tra
- `[!]` Bị chặn; ghi rõ nguyên nhân và hướng xử lý ngay dưới task

Mỗi agent chỉ cập nhật section mình sở hữu sau từng mốc công việc. Không đánh dấu hoàn thành nếu chưa chạy kiểm tra tương ứng.

## Backend — owner: `backend`

- [x] B1. Đọc plan, kiểm tra .NET SDK và khởi tạo solution trong `api/`
- [x] B2. Tạo cấu trúc Api/Application/Domain/Infrastructure/UnitTests/IntegrationTests/Maintenance
- [x] B3. Cấu hình PostgreSQL, EF Core, entity, mapping, index và migration đầu tiên
- [x] B4. Xây dựng nền tảng chung: configuration, DI, ProblemDetails, logging, OpenAPI, health checks
- [x] B5. Triển khai auth: password hashing, login, refresh rotation, logout, `/me`, auth session và revoke tức thời
- [x] B6. Triển khai authorization cho SuperAdmin/Admin/Teacher và quy tắc bảo vệ tài khoản quản trị
- [x] B7. Triển khai User API: CRUD, đổi mật khẩu, soft delete, pagination/filter/sort
- [x] B8. Triển khai Student API: CRUD, soft delete, tái sử dụng student code, pagination/filter/sort
- [x] B9. Triển khai audit log, SQL retention script và console app maintenance
- [x] B10. Thêm seed/CLI SuperAdmin, Docker Compose và cấu hình local an toàn
- [x] B11. Viết/chạy unit test và integration test
  - Unit test 11/11 pass và integration test PostgreSQL/Testcontainers 7/7 pass từ database sạch; migration/seed, auth/authorization, CSRF/logout/revoke, User và Student API đều đã được thực thi thực tế.
- [x] B12. Hoàn thiện README/OpenAPI, chạy build/test cuối và ghi kết quả

### Backend log

- Khởi tạo: đã giao agent backend; đang đọc yêu cầu và kiểm tra môi trường.
- B1 (2026-08-11): đã đọc toàn bộ plan/tasks; không có `AGENTS.md`; xác nhận .NET SDK 10.0.302 và tạo `api/AdminPortal.slnx` thành công.
- B2 (2026-08-11): đã tạo đủ 7 project Api/Application/Domain/Infrastructure/UnitTests/IntegrationTests/Maintenance, thêm vào solution và thiết lập project references theo dependency direction.
- B3 (2026-08-11): đã thêm entity/mapping PostgreSQL snake_case, query filter soft delete, partial unique index và migration `InitialCreate`; build Infrastructure đạt 0 warning/0 error. Đã pin `Microsoft.OpenApi` 2.11.0 để tránh advisory của dependency mặc định.
- B4 (2026-08-11): đã cấu hình DI/Options/CORS, JSON enum string, ProblemDetails có traceId, exception handler, structured request log, OpenAPI và health live/ready kiểm tra PostgreSQL; build API đạt 0 warning/0 error.
- B5 (2026-08-11): đã triển khai password hash, JWT `sid`, kiểm tra session DB trên mỗi request, lockout, login/refresh rotation/logout/me và rate limit. CSRF cross-site trả token trong response, có `GET /auth/csrf`, đồng thời kiểm tra cookie `XSRF-TOKEN` với header `X-CSRF-TOKEN`; build API đạt 0 warning/0 error.
- B6 (2026-08-11): đã thêm policy PortalManagers và application authorization rule: SuperAdmin quản lý Admin/Teacher, Admin chỉ quản lý Teacher, Teacher bị chặn. Logout nay có thể revoke bằng refresh cookie + CSRF dù bearer đã hết hạn; API build sạch.
- B7 (2026-08-11): đã hoàn tất User CRUD, đổi mật khẩu, soft delete, revoke toàn bộ session khi đổi role/status/password/delete, phân trang/filter/sort whitelist và audit không chứa password; build API đạt 0 warning/0 error.
- B8 (2026-08-11): đã hoàn tất Student CRUD, validation ngày sinh, chuẩn hóa/unique studentCode trên bản ghi active, cho phép tái sử dụng sau soft delete, phân trang/filter/sort whitelist và audit; build API đạt 0 warning/0 error.
- Điều chỉnh contract (2026-08-11): update user/student dùng `PUT` full replacement để nullable field có thể được xóa bằng `null`; `plans/01-BASE-admin-portal.md` đã đồng bộ. Ngày sinh thiếu (`0001-01-01`) cũng bị validation từ chối.
- B9 (2026-08-11): audit đã phủ auth/User/Student; thêm SQL cleanup và console maintenance chạy batch theo retention audit 90 ngày/session 30 ngày; build toàn solution đạt 0 warning/0 error.
- B10 (2026-08-11): thêm migrate-on-startup tùy chọn, seed SuperAdmin từ environment (không hard-code secret), Dockerfile, PostgreSQL/API Compose, `.env.example` và ignore secret/build output; build solution đạt 0 warning/0 error.
- B11 (2026-08-11): 11 unit test pass; 7 integration scenarios đã viết/build (auth 401, role 403, login sai, CSRF/logout không bearer, User PUT/409, Student CRUD/409/404/reuse code, pagination/filter/sort), nhưng runtime test bị chặn do máy không có Docker engine.
- Tiếp tục sau reset (2026-08-11): đã xác nhận source backend, README, OpenAPI sample và 7 project vẫn đầy đủ; đang chạy lại build, 11 unit tests và 7 integration scenarios trên môi trường mới.
- Kiểm tra build sau reset (2026-08-11): .NET SDK 10.0.302 hoạt động; lần restore/build trong sandbox bị chặn kết nối NuGet (`NU1301`, socket tới `api.nuget.org:443`), đang chạy lại với quyền network được phê duyệt.
- Build sau reset (2026-08-11): restore thành công và `dotnet build AdminPortal.slnx` đạt 0 warning/0 error; đang chạy lại unit tests.
- Unit tests sau reset (2026-08-11): `dotnet test tests/AdminPortal.UnitTests --no-build` đạt 11/11; đang kiểm tra Docker engine và chạy 7 integration scenarios.
- Docker check sau reset (2026-08-11): Docker CLI 29.7.2 tồn tại nhưng sandbox bị từ chối đọc Docker config/named pipe; đang kiểm tra lại engine ngoài sandbox theo đúng cơ chế phê duyệt.
- Docker check ngoài sandbox (2026-08-11): truy cập được Docker context `desktop-linux`, nhưng daemon trả `Docker Desktop is unable to start`; đang kiểm tra dịch vụ/khởi động lại Docker Desktop trước khi kết luận integration tests bị chặn.
- Integration tests sau reset (2026-08-11): đã chạy ngoài sandbox và discover đủ 7/7 scenarios; cả 7 dừng tại constructor `ApiFactory` trước khi thực thi vì Docker daemon trả `ServiceUnavailable: Docker Desktop is unable to start` tại `npipe://./pipe/docker_engine`. Source/test assembly build sạch; đang kiểm tra trạng thái WSL/Docker lần cuối.
- OpenAPI/runtime smoke test (2026-08-11): API khởi động thành công ở Development với cấu hình secret tạm qua environment; `/openapi/v1.json` trả OpenAPI 3.1.1, có các path auth/users/students và `/health/live` trả HTTP 200. Đang rà soát source/tài liệu lần cuối để chốt B12.
- Docker blocker cuối (2026-08-11): xác nhận `docker-desktop` WSL distro dừng/khởi tạo không thành công vì thiếu file dữ liệu `...\Docker\wsl\data\ext4.vhdx`; không reset/unregister để tránh rủi ro mất dữ liệu Docker của người dùng. B11 giữ `[!]` cho tới khi Docker Desktop được repair/reinstall.
- B12 (2026-08-11): README và `requests.http` đã mô tả đầy đủ cấu hình, auth/CSRF, endpoint, OpenAPI, health check, build/test và retention. Kiểm tra cuối: solution build 0 warning/0 error; unit test 11/11 pass; API/OpenAPI/liveness smoke test pass. Backend hoàn tất, ngoại trừ runtime integration test bị chặn bởi Docker Desktop như B11.
- Migration baseline review (2026-08-11): phát hiện migration đầu tiên thiếu file Designer và `AdminPortalDbContextModelSnapshot.cs`; đang bổ sung metadata chuẩn EF Core để các migration tương lai diff đúng từ baseline hiện tại.
- Migration baseline hoàn tất (2026-08-11): đã pin local `dotnet-ef` 10.0.10, thêm EF design-time dependency, bổ sung `20260811000000_InitialCreate.Designer.cs` và `AdminPortalDbContextModelSnapshot.cs` sinh trực tiếp từ model runtime; không để lại migration tạm nên lịch sử vẫn chỉ có `InitialCreate`. `dotnet-ef migrations has-pending-model-changes` xác nhận không có model change chưa ghi nhận; build cuối `--no-restore` đạt 0 warning/0 error và unit test vẫn đạt 11/11. README đã bổ sung lệnh restore tool, apply/tạo migration và quy tắc giữ snapshot.
- B11 tiếp tục (2026-08-11): Docker/Testcontainer đã sẵn sàng; lần chạy mới phát hiện API vẫn dùng connection string mặc định do `AddInfrastructure` đọc cấu hình ngay khi đăng ký service. Đang chuyển sang resolve `IConfiguration` lazily trong `AddDbContext`, sau đó sẽ chạy lại đủ 7 scenarios.
- B11 integration run 1 (2026-08-11): PostgreSQL Testcontainer đã tạo, readiness pass, migration `InitialCreate` apply và SuperAdmin seed thành công, xác nhận sửa connection string đúng. 7 scenarios tiếp tục lỗi tại auth vì `AuthenticationExtensions` cũng đã chụp `JwtOptions` rỗng trước test override (`IDX10703`); đang chuyển JWT bearer sang resolve options lazily qua DI.
- B11 integration run 2 (2026-08-11): lazy JWT configuration đã hoạt động (`401` anonymous đúng contract); runtime tiếp tục phát hiện validation attributes của positional record đang đặt trên generated property nên ASP.NET Core 10 từ chối metadata. Đang chuyển toàn bộ request record annotations sang constructor parameter target trước vòng test tiếp theo.
- B11 integration run 3 (2026-08-11): PostgreSQL Testcontainer khởi động từ môi trường sạch, apply migration/seed thành công và đủ 7/7 scenarios pass sau khi sửa record validation metadata. Đang chạy unit test/build/model-snapshot check cuối trước khi chốt B11.
- B11 hoàn tất (2026-08-11): sửa `DbContext` và JWT bearer để resolve cấu hình lazily nên `WebApplicationFactory` dùng đúng Testcontainer/JWT test override; chuyển validation annotations của positional request records sang constructor parameters theo ASP.NET Core 10; thêm matching query filter cho `AuthSession` để đồng bộ soft-delete `User` và loại cảnh báo required-navigation. Kết quả cuối: build 0 warning/0 error, unit 11/11 pass, integration 7/7 pass, EF xác nhận không có pending model changes.

## Frontend — owner: `frontend`

- [x] F1. Đọc API plan và rà soát Angular/DevExtreme template hiện tại trong `ui/`
- [x] F2. Chuẩn hóa environment, API client, DTO/model và xử lý `ProblemDetails`
- [x] F3. Triển khai auth: login, refresh, logout, session restore, interceptor và route guard
- [x] F4. Loại bỏ/ẩn các luồng ngoài phạm vi như đăng ký và quên mật khẩu
- [x] F5. Cập nhật layout/navigation và hiển thị theo role
- [x] F6. Xây dựng màn hình quản lý user: grid, pagination, filter, create/edit/delete, đổi mật khẩu
- [x] F7. Xây dựng màn hình quản lý student: grid, pagination, filter, create/edit/delete
- [x] F8. Bổ sung loading, validation, confirm dialog, notification và xử lý 401/403/409
- [x] F9. Thêm/chỉnh unit test phù hợp
- [x] F10. Hoàn thiện README/config, chạy build/test cuối và ghi kết quả

### Frontend log

- Khởi tạo: đã giao agent frontend; đang đọc yêu cầu và rà soát template.
- F1: Đã rà soát toàn bộ API contract, Angular 15/DevExtreme 23.2 và cấu trúc template; chưa có API client/environment thật, auth hiện là dữ liệu giả, các trang hiện tại là demo.
- F2: Đã thêm environment dev/prod, model tách đúng `UserStatus`/`StudentStatus`, API client có credentials và chuẩn hóa `ProblemDetails`; TypeScript compile đạt.
- F3: Đã nối login/refresh/logout/me, access token trong memory, restore session, refresh đồng thời dùng chung một request, Bearer/CSRF interceptor và guards; TypeScript compile đạt.
- F4: Đã gỡ route, module, link và component đăng ký/quên-đặt lại mật khẩu khỏi ứng dụng; màn hình login chỉ còn luồng được cấp tài khoản; TypeScript compile đạt.
- F5: Đã Việt hóa layout, thay nội dung demo bằng dashboard/profile thật, navigation thay đổi theo role và không hiện chức năng quản trị cho Teacher; TypeScript compile đạt.
- F6: Đã hoàn tất user grid với remote pagination/filter/sort, quyền tạo Admin chỉ cho SuperAdmin, popup create/edit/delete và đổi mật khẩu; Angular development build đạt.
- F7: Đã hoàn tất student grid với remote pagination/filter/sort, popup CRUD, validation ngày sinh và đúng `StudentStatus`; Angular development build đạt.
- F8: Đã thêm load state, khóa form khi lưu, validation/confirm/notify, ánh xạ lỗi field/401/403/409 và chuyển login khi refresh thất bại; TypeScript compile đạt.
- F9: Đã thêm unit test cho ProblemDetails, auth state/token và navigation theo role; `ChromeHeadlessCI` chạy 5/5 test thành công.
- F10: Đã hoàn thiện README/config, đối chiếu contract backend (`PUT` full update, CSRF bootstrap, enum/string và sort whitelist); production build đạt (3.00 MB raw, 556.56 kB ước tính truyền) và 5/5 unit test đạt.
- Kiểm tra lại sau reset (2026-08-11): chạy production build bằng Angular CLI cục bộ với 1 worker, đạt 3.00 MB raw/556.56 kB ước tính truyền; chạy `ChromeHeadlessCI` bằng Chrome profile tạm trong workspace, đạt 5/5 unit test. Không phát hiện lỗi frontend cần sửa.
- Cleanup sau review (2026-08-11): xóa `not-authorized-container.ts` không còn được sử dụng cùng các nhánh cũ `reset-password`, `create-account` và `change-password`; production build đạt, không còn tham chiếu đến các luồng xác thực ngoài phạm vi.

## Tích hợp — owner: `root`

- [x] I1. Điều phối contract API giữa backend và frontend
- [x] I2. Kiểm tra thay đổi không chồng chéo ngoài phạm vi `api/` và `ui/`
- [x] I3. Chạy kiểm tra tổng hợp khả dụng trong môi trường hiện tại
- [x] I4. Review code, cập nhật status cuối và bàn giao

### Integration log

- Khởi tạo: `plans/01-BASE-admin-portal.md` là contract nền; backend sở hữu `api/`, frontend sở hữu `ui/`.
- Contract enum: `UserStatus = Active/Inactive/Locked`, `StudentStatus = Active/Inactive`; JSON enum dạng chuỗi.
- Contract CSRF cross-site: UI không đọc cookie của API; login/refresh trả `csrfToken`, bootstrap qua `GET /api/v1/auth/csrf`, UI lưu token trong memory và gửi header `X-CSRF-TOKEN` cho refresh/logout.
- Contract update resource: dùng `PUT /api/v1/users/{id}` và `PUT /api/v1/students/{id}` với full form để phân biệt rõ việc xóa các field nullable; không dùng PATCH nullable mơ hồ.
- Kiểm tra sau reset: backend build 0 warning/0 error, 11/11 unit test pass, API/OpenAPI/liveness smoke test pass; frontend production build pass và 5/5 unit test pass.
- Review cuối: đã xóa component frontend chết còn chứa luồng đăng ký/reset password; bổ sung EF Core migration Designer/ModelSnapshot và xác nhận không có pending model changes.
- Scope thay đổi đúng `api/`, `ui/` và `tasks.md`; không phát hiện placeholder/TODO hoặc file demo còn sót trong source.
- Docker blocker đã được giải quyết. Integration test trên PostgreSQL Testcontainers chạy từ database sạch đạt 7/7; quá trình chạy đã phát hiện và sửa lazy configuration cho database/JWT, validation metadata ASP.NET Core 10 và matching query filter cho `AuthSession`.

## Thay đổi: Khởi tạo SuperAdmin bằng UI — owner: `root`

- [x] S1. Thiết kế contract setup và loại bỏ seed configuration/runtime seeder
- [x] S2. Triển khai API kiểm tra trạng thái và tạo SuperAdmin đầu tiên an toàn khi database rỗng
- [x] S3. Triển khai màn hình UI setup, app initialization và route guards
- [x] S4. Cập nhật Docker Compose, `.env.example`, plan, README và request samples
- [x] S5. Cập nhật unit/integration tests và chạy lại backend/frontend verification
- [x] S6. Rebuild Docker, kiểm tra setup flow thực tế và chốt bàn giao

### Setup flow log

- Bắt đầu: thay cơ chế seed bằng `GET /api/v1/setup/status` và `POST /api/v1/setup/super-admin`; UI tự chuyển sang màn setup khi bảng `users` chưa có bất kỳ bản ghi nào.
- S1: đã xóa runtime seeder/options và các biến seed trong appsettings/Compose/.env.example; setup POST dùng PostgreSQL transaction advisory lock để chỉ một request có thể tạo user đầu tiên.
- S2: API setup đã build 0 warning/0 error; đang bổ sung test thực tế với PostgreSQL.
- S3: đã nối màn hình `/setup` vào app initializer và route guards; nếu database rỗng UI chỉ cho phép khởi tạo SuperAdmin, nếu đã có user mới restore session/hiện login.
- S2/S5: integration test PostgreSQL sạch xác nhận status ban đầu, race condition chỉ một SuperAdmin được tạo, status sau setup và login; toàn bộ 8/8 integration test đạt.
- S3/S4: production build UI đạt; README, plan, request samples, Compose và `.env.example` đã chuyển hoàn toàn sang setup flow, không còn cấu hình seed runtime.
- S5: backend build 0 warning/0 error, unit 11/11, integration PostgreSQL 8/8; frontend production build đạt 3.01 MB raw/557.44 kB transfer và unit test 8/8.
- S6 smoke lần 1: Compose giữ nguyên database hiện có, setup status trả `false`, tạo SuperAdmin lần nữa trả `409`, live/ready/OpenAPI đều `200`; phát hiện request logger ghi nhầm `200` cho exception đã map thành `409`, đang điều chỉnh thứ tự middleware và xác minh lại.
- S6 hoàn tất: rebuild container API sau khi sửa middleware; PostgreSQL healthy, API chạy ở `http://localhost:5158`, setup status `false` trên dữ liệu hiện có, POST setup lần hai trả và log đúng `409`, live/ready/OpenAPI đều `200`. Không xóa hoặc reset dữ liệu hiện có.

## Deploy IIS local production — owner: `root`

- [x] D1. Thêm cấu hình Angular IIS production cho `https://api-gv-portal.local`
- [x] D2. Tạo PowerShell build/deploy hai IIS site và app pool riêng
- [x] D3. Tự động cấu hình hosts, HTTPS local certificate và SNI bindings
- [x] D4. Cấu hình PostgreSQL/JWT/CORS an toàn ngoài source code
- [x] D5. Viết hướng dẫn triển khai, rollback và troubleshooting
- [x] D6. Build/test artifact và kiểm tra script trước khi bàn giao

### IIS deploy log

- Bắt đầu: target API `C:\inetpub\api-gv-portal.local`, UI `C:\inetpub\gv-portal.local`; hostname tương ứng dùng HTTPS local port 443.
- D1-D4: đã thêm Angular configuration `iis` và script PowerShell 5.1-compatible; script prompt secret dạng SecureString, sinh `web.config` tại máy deploy, tạo SAN certificate/trust root, hosts entries, SNI bindings và app pools riêng. Đang build artifact để kiểm chứng.
- D5: đã viết hướng dẫn PostgreSQL role/database, Hosting Bundle, build/deploy, certificate có sẵn, first-run setup, backup/rollback và troubleshooting tại `deploy/iis/HUONG-DAN-DEPLOY-IIS.md`.
- D6: PowerShell parser đạt; chạy thật `-Build -PrepareOnly` thành công và không thay đổi IIS; API Release publish đạt, Angular IIS build 3.01 MB/557.73 kB đạt và chứa đúng API HTTPS URL; test chèn 7 environment variables vào published web.config đạt; backend unit 11/11, integration Release PostgreSQL 8/8, frontend 8/8.
- Bàn giao: artifacts có tại `artifacts/iis`; chưa chạy chế độ deploy nên chưa thay đổi `C:\inetpub`, IIS bindings, certificate store hoặc hosts file của máy.
- Lưu ý vận hành: hướng dẫn có bước kiểm tra cổng 5432 và dừng Compose bằng `docker compose stop` (không xóa volume) nếu IIS dùng PostgreSQL 17 cài trực tiếp trên Windows.

## Đóng gói deploy sang máy IIS khác — owner: `root`

- [x] P1. Tạo script build package ZIP không chứa source/secret
- [x] P2. Cập nhật hướng dẫn copy, checksum, extract và deploy trên máy đích
- [x] P3. Sinh package thực tế và kiểm tra nội dung trước bàn giao

### Cross-machine package log

- Bắt đầu: build trên máy hiện tại; máy IIS đích chỉ nhận package publish và chạy deploy, không cần .NET SDK/Node/npm.
- P1/P2: đã thêm `build-iis-package.ps1`, checksum SHA-256 và `BUILD-INFO.txt`; hướng dẫn tách rõ lệnh chạy ở máy source và máy IIS, bao gồm verify checksum và Unblock-File.
- P3 review package lần 1: checksum đúng và không có source/secret; phát hiện Release publish còn mang `appsettings.Development.json` và PDB mặc định, đã cấu hình loại khỏi bản production trước khi đóng gói chính thức.
- P3 hoàn tất: package cuối `release/gv-portal-iis-20260811-102752.zip` (6,811,884 bytes), SHA-256 `93EA758E0DCD542FDE73A8659A9D4BC96E3C5BA51381AA60010255B9056866F1`; kiểm tra 0 source, 0 secret file, 0 PDB, 0 appsettings Development. Đã xóa các package trung gian do quá trình kiểm tra sinh ra; có thể tái tạo bằng package script.

## Persistent backend/frontend agents — owner: `root`

- [x] A1. Tạo root `AGENTS.md` quy định ownership và thứ tự đọc khi mở session mới
- [x] A2. Tạo persistent memory chuyên biệt cho backend
- [x] A3. Tạo persistent memory chuyên biệt cho frontend
- [x] A4. Tạo shared memory và tài liệu cách resume subagents ở chat khác
- [x] A5. Tạo project custom-agent config, review chéo memory và chốt bàn giao

### Persistent agent log

- Bắt đầu: runtime subagent không tồn tại qua chat mới; dùng `AGENTS.md` + `.agents/**/MEMORY.md` làm durable memory trong repository.
- A1/A4: đã tạo entrypoint root, shared memory, quy tắc không lưu secret, phân biệt memory hiện trạng với log chi tiết trong `tasks.md`, và yêu cầu recheck runtime state ở session mới.
- A2/A3: hai subagent đã tạo `api/AGENTS.md`, `ui/AGENTS.md` cùng memory chuyên biệt cho kiến trúc, contract, lệnh kiểm tra, baseline và pitfalls của từng phần.
- A5: đã thêm custom agent project-scoped `backend`/`frontend` trong `.codex/agents`, giới hạn tối đa hai subagent đồng thời trong `.codex/config.toml`, và review chéo ownership/auth/setup/IIS contract. Các thay đổi chỉ là agent config/tài liệu nên không chạy lại product build/test.
- Kiểm tra bàn giao: cả 3 file TOML parse thành công; đủ root/nested `AGENTS.md`, shared/backend/frontend memory; secret-value scan không phát hiện credential hoặc private key trong bộ file agent.

## Epic điểm danh `ATT` — owner: `root`

- [x] `ATT-P-01`. Phân tích gap backend/frontend và yêu cầu nghiệp vụ
- [x] `ATT-P-02`. Tạo plan cross-stack có mã từng đợt tại `plans/02-ATT-attendance.md`
- [x] `ATT-P-03`. `ATT-DEC-01`–`10` đã chốt; chọn full daily snapshot gồm `Present`
- [x] `ATT-P-04`. `ATT-DEC-11` đã chốt; toàn bộ UI chỉ sử dụng tiếng Việt
- [x] `ATT-01`. Nền tảng Teacher/Group/current roster và UI quản trị
- [x] `ATT-02`. Vertical slice đọc Missing/Saved sheet, filter và card list
- [x] `ATT-03`. Vertical slice tạo/cập nhật full daily sheet
- [x] `ATT-04`. UX, edge cases và hardening
- [x] `ATT-05`. Tài liệu, full regression và release IIS

### Attendance implementation status

Backend:

- [x] `ATT-BE-00`. Khóa DTO/schema/authorization/OpenAPI contract
- [x] `ATT-BE-01`. Teacher profile, policy và migration/backfill
- [x] `ATT-BE-02`. StudentGroup, current roster, snapshot version và lifecycle
- [x] `ATT-BE-03`. Attendance context/daily Missing-Saved query
- [x] `ATT-BE-04`. Full-sheet POST/PUT, concurrency, transaction và audit
- [x] `ATT-BE-05`. Historical recovery, candidate lookup và hardening
- [x] `ATT-BE-06`. OpenAPI, README, requests và migration notes

Frontend:

- [x] `ATT-FE-00`. Khóa models/wireflow và dictionary UI tiếng Việt
- [x] `ATT-FE-01`. UI quản trị Teacher policy, group và current roster
- [x] `ATT-FE-02`. Navigation, route, models và attendance service
- [x] `ATT-FE-03`. Filter panel, local search và card list Missing-Saved
- [x] `ATT-FE-04`. Editor, full-roster save, dirty state và sticky action
- [x] `ATT-FE-05`. Historical recovery, error states, responsive và accessibility
- [x] `ATT-FE-06`. Unit test, README và IIS environment verification

QA/Integration:

- [x] `ATT-QA-01`. Migration/current-assignment/cap/race và CRUD regression
- [x] `ATT-QA-02`. Read scope, role/group/date và tìm kiếm tiếng Việt
- [x] `ATT-QA-03`. Full snapshot/status/batch/concurrency/permission
- [x] `ATT-QA-04`. Historical recovery, stale update, mobile/keyboard và auth regression
- [x] `ATT-QA-05`. Full build/test, database upgrade rehearsal và IIS package

### Attendance planning log

- `ATT-01` bắt đầu: đã kiểm tra working tree sạch tại commit `287a477`; giao song song backend sở hữu `api/` và frontend sở hữu `ui/`, root điều phối contract/trạng thái/integration.
- Preflight QA: .NET SDK `10.0.302`, Node `20.11.0`; Docker Engine `29.7.2` hoạt động và PostgreSQL 17 Compose đang healthy/expose cổng 5432. Lệnh npm trong sandbox bị chặn quyền đọc profile Windows, frontend sẽ chạy build/test với quyền phù hợp khi tới gate.
- `ATT-BE-00`/`ATT-FE-00`: đã khóa contract quản trị Teacher/StudentGroup/Student group fields và query filters. Group metadata, responsible Teacher và Student group có command mutation riêng; frontend đã nhận DTO thống nhất, root đã đồng bộ `plans/02-ATT-attendance.md`.
- `ATT-FE-00` hoàn tất: TypeScript compile sạch; đã thêm DTO/service đầy đủ, dictionary label/error/read-only, utility DateOnly/search không dấu, Angular `vi-VN`, DevExtreme message tiếng Việt và dọn raw role/status/chuỗi tiếng Anh hiện có. UI không dùng raw `ProblemDetails.title/detail`.
- `ATT-BE-00` hoàn tất: DTO/controller/OpenAPI contract cho Teacher, StudentGroup, Student group fields, attendance context/daily/create/update/recovery/candidates và machine-readable ProblemDetails đã khóa, không lệch plan; `dotnet build api/AdminPortal.slnx --no-restore` đạt 0 warning/error.
- `ATT-FE-01` hoàn tất, Angular development build đạt: thêm `/student-groups`, remote group CRUD/filter, phân/gỡ responsible Teacher, roster popup tối đa 100 với search không dấu/gán-gỡ-chuyển Student, Teacher policy grid chỉnh window 1–7 ngày, route/sidebar `Nhóm` và mapping lỗi tiếng Việt.
- `ATT-BE-01` hoàn tất implementation, build 0 warning/error: Teacher profile/list/get/policy 1–7; tạo/reuse profile theo role; lifecycle conflict khi còn group; đổi tên Teacher tăng snapshot version các group trong transaction/row lock; audit và business-timezone persistence nền đã có. Migration/backfill sẽ được chốt cùng model snapshot ở gate backend.
- `ATT-BE-02` hoàn tất, build 0 warning/error: StudentGroup CRUD/filter/sort/paging, assignment Teacher, lifecycle/unique-code/audit; Student group fields và atomic assign/move/unassign với row locks, cap 100, chặn move sau điểm danh hôm nay, snapshot version group cũ/mới và identity invariants.
- `ATT-BE-03` hoàn tất, build 0 warning/error: context/daily phân quyền ba role, business date `Asia/Ho_Chi_Minh`, auto-resolve một Teacher group, Missing preview không ghi DB, Saved immutable snapshot, summary và `canCreate/canEdit/canRecover`/version provenance đúng contract.
- `ATT-BE-04` hoàn tất: first-save full roster/persisted Present, snapshot/unique conflict, full PUT + aggregate version/atomic lock, conditional validation, Admin historical recovery/candidates, Saved response + Location và audit không ghi raw notes. Build 0 warning/error; baseline unit test 11/11 đạt.
- `ATT-FE-02`/`ATT-FE-03` hoàn tất, Angular development build đạt: route/nav `Điểm danh`, context ngày/nhóm theo role và 0/1/n group, card list Missing/Saved, search tiếng Việt không dấu, filter status, summary toàn roster, stale-response guard và loading/error state độc lập. Editor đã vào `ATT-FE-04` để harden/test.
- `ATT-FE-04` hoàn tất, development build đạt: editor đủ bốn trạng thái/conditional fields/notes; Missing dirty=0 vẫn POST full roster, Saved chỉ PUT khi dirty và gửi full roster/version; response reset baseline; sticky count, reveal/focus validation card ẩn, map `records[i]`, xử lý 403/409 giữ draft + CTA đều bằng copy tiếng Việt.
- `ATT-BE-05` hoàn tất: FK RESTRICT, group/sheet row locks và stable multi-group order, optimistic conflict có currentVersion, lifecycle/auth/audit privacy và retention an toàn. EF migration `AddAttendanceFoundation` được sinh chuẩn với constraints/indexes/composite date FK/Teacher backfill; baseline PostgreSQL Testcontainers 8/8 đạt, EF không có pending model changes. Warning query-filter/required historical FK là chủ ý và history query dùng `IgnoreQueryFilters`.
- `ATT-FE-05` hoàn tất: dirty guard phủ date/group/route/beforeunload; Admin/SuperAdmin recovery qua ba candidate APIs với 1–100 Student, acknowledgement/reason và best-known warning; error/empty/loading/read-only tiếng Việt; responsive 2 cột khoảng 8–10 card, mobile 1 cột, sticky/a11y/touch/focus hoàn chỉnh. Production AOT và development build đạt; đang tách style để loại budget warning trước gate cuối.
- `ATT-BE-06` checkpoint: unit 23/23 và PostgreSQL integration 14/14 đã đạt, gồm race first-save và cap 100. Review schema phát hiện `attendance_edit_window_days` scaffold thành `integer` thay vì `smallint`; backend đang sinh lại migration bằng EF rồi mới chạy final gates, chưa đánh dấu QA hoàn tất sớm.
- `ATT-FE-06` checkpoint: ChromeHeadlessCI 20/20 đạt; production build sạch 3,19 MB raw/583,52 kB transfer; IIS build sạch 3,19 MB/583,80 kB và bundle chứa đúng `https://api-gv-portal.local/api/v1`. Chỉ còn warning license/dev-mode từ dependency trong test log; README/agent docs/memory đang được chốt trước final handoff.
- `ATT-BE-06` hoàn tất: README/requests/backend memory đồng bộ; migration cuối `20260811130802_AddAttendanceFoundation` dùng Teacher policy `smallint`, version `integer`, partial roster index và backfill profile. Review cuối sửa check constraint dùng đúng cột `recovery_reason`, chặn gán Student inactive và bổ sung coverage rollback roster/window/recovery authorization. Final backend gate: build 0 warning/error, unit 23/23, PostgreSQL 17 Testcontainers Release 15/15, EF no pending changes. EF CLI còn ba warning query-filter/required historical FK có chủ ý (`RESTRICT` + `IgnoreQueryFilters`) và fresh-DB tests xanh.
- `ATT-FE-06` hoàn tất: ChromeHeadlessCI 21/21 đạt; production AOT sạch 3,20 MB/583,84 kB; IIS AOT sạch và bundle dùng đúng `https://api-gv-portal.local/api/v1`. Recovery ngày quá khứ có entry point cho Admin/SuperAdmin ngay cả khi nhóm inactive/deleted không còn trong context; dictionary UI ánh xạ toàn bộ ProblemCodes sang tiếng Việt; validation tạo/đổi mật khẩu khớp policy API 12–128 ký tự.
- `ATT-QA-01`–`04` hoàn tất: integration phủ migration/current assignment/cap 100/race, role/group/date/window, Missing không ghi DB, full persisted snapshot/PUT rollback/concurrency, historical recovery/authorization/lifecycle/audit privacy; frontend phủ route/navigation, DateOnly, search không dấu, full save, dirty state và recovery entry.
- `ATT-QA-05` hoàn tất: test upgrade tự động migrate `InitialCreate`, chèn Teacher User + Student legacy rồi migrate `AddAttendanceFoundation`, xác nhận Teacher profile policy 7 được backfill và dữ liệu cũ giữ nguyên. Gói IIS cuối `release/gv-portal-iis-20260811-132500.zip` có 103 entries, 6.935.733 bytes, SHA-256 `389E4D5CD4510A377AF41C83A20BA7C7C68C41543B8ED85647D04DFADD07C523`; checksum khớp, có API/UI đúng HTTPS URL và không chứa source/PDB/Development config/secret file. Chế độ package chỉ prepare artifact, không thay đổi IIS máy build.
- `ATT-P-01`: xác nhận hiện trạng chưa có Teacher profile, Group, assignment hoặc attendance; Student CRUD/search hiện tại chưa scope theo giáo viên và chưa search không dấu.
- `ATT-P-02`: initial draft từng dùng exception-only + temporal assignment; thiết kế này đã bị `ATT-DEC-10` thay thế.
- Review chéo backend/frontend đã chuẩn hóa contract theo ngày (`AbsentFullDay`/`AbsentHalfDay`), sheet-level concurrency, Missing/Saved state, historical snapshot và read-only state.
- `ATT-DEC-06` đã chốt: tối đa 100 học sinh/nhóm; màn hình hiển thị rõ khoảng 8–10 card cùng lúc và scroll dọc để xem toàn bộ, không pagination trong v1.
- `ATT-DEC-01`–`05`, `07`–`09` đã chốt: điểm danh theo ngày; half-day bắt buộc Morning/Afternoon; 1-1 cố định 60 phút và loại trừ với vắng; phép/không phép cho các loại vắng; Teacher có edit window 1–7 ngày mặc định 7 do Admin/SuperAdmin cấu hình riêng; có UI nhóm; quản trị bắt buộc chọn một group; attendance data giữ lâu dài và audit 90 ngày.
- `ATT-DEC-10` đã chốt full daily snapshot: `attendance_sheets` + `attendance_records`, lưu cả `Present`; bỏ `effective_from/effective_to` và temporal assignment.
- `ATT-DEC-11` đã chốt: toàn bộ visible text, error/validation/empty/loading state và accessibility text trên UI dùng tiếng Việt; API/code giữ identifier tiếng Anh và frontend ánh xạ tập trung, không hiển thị raw enum/error code.
- `ATT-P-04`: đã bổ sung locale `vi-VN`, yêu cầu Việt hóa chuỗi DevExtreme, mapping role/status/attendance/error code và smoke các route cũ/mới; kiểm tra Markdown fences và `git diff --check` đạt. Không chạy product build/test vì chỉ cập nhật contract/plan.
- Plan đã rewrite: current `responsible_teacher_id` trên group, current `group_id` trên Student, Missing preview không phải lịch sử, Saved sheet snapshot bất biến, first save ghi đủ roster, update dùng aggregate sheet version và full replacement.
- Contract plan dùng `snapshotVersion` bao phủ roster, group/Teacher/Student identity; stale create trả `409 SnapshotChanged`. Ngày quá khứ không thể chứng minh snapshot trả `HistoricalSnapshotUnavailable`; chỉ Admin/SuperAdmin có recovery flow với roster/Teacher rõ ràng, acknowledgement, reason và audit.
- Review cuối khóa rõ state permission: Missing dùng `canCreate`, Saved dùng `canEdit`; first-save vẫn hoạt động khi dirty bằng 0. Historical recovery có group/student/teacher candidate APIs riêng, machine-readable `ProblemDetails.code` và không được bypass luồng chuẩn.
- Sheet lưu provenance lâu dài: `CurrentSnapshot` có `sourceSnapshotVersion`; `HistoricalRecovery` có source version null và recovery reason. Concurrency yêu cầu group-row lock, stable multi-group lock order, aggregate sheet version và transaction rollback toàn batch.
- Backend/frontend review cuối xác nhận không còn blocker plan sau khi chốt recovery là ngoại lệ có kiểm soát cho snapshot không thể tái dựng, group inactive/deleted/không còn responsible Teacher; POST tạo phiếu trả `201 Created` + `Location` + full Saved snapshot.
- Verification tài liệu: Markdown code fences cân bằng, `git diff --check` đạt và scan không còn contract cũ `rosterVersion`/`HistoricalRosterUnavailable`/exception-only ngoài dòng log mô tả thiết kế đã bị thay thế. Không chạy product build/test vì đợt này chỉ thay đổi plan/memory/task tracking.
- Git workflow: người dùng cho phép chủ động tạo local commit theo từng milestone; không bao gồm push/merge/rebase/tag nếu chưa được yêu cầu riêng.
- Epic `ATT` đã được triển khai đầy đủ trên source/schema/API/UI, kiểm tra regression và đóng gói IIS; chưa push hoặc triển khai lên máy IIS đích.

## Skill production/IIS — owner: `root`

- [x] `PROD-SKILL-01`. Tạo project skill `$gv-portal-production` bằng skill initializer chuẩn
- [x] `PROD-SKILL-02`. Tách ba mode `build`, `verify`, `deploy` và khóa implicit invocation
- [x] `PROD-SKILL-03`. Chuyển production/IIS ra khỏi default verification trong `AGENTS.md`
- [x] `PROD-SKILL-04`. Validate cấu trúc/frontmatter/UI metadata của skill

### Production skill log

- Skill nằm tại `.codex/skills/gv-portal-production`; `agents/openai.yaml` đặt `policy.allow_implicit_invocation: false`.
- Chỉ khi người dùng gọi `$gv-portal-production` mới chạy production build/package/verify/deploy. Không truyền mode thì mặc định `build`, không thay đổi IIS; chỉ `deploy` rõ ràng mới cho phép sửa IIS, hosts, certificate, `C:\inetpub` và apply migration trên database đích.
- Workflow `build` chạy backend/frontend release gates, tạo ZIP và kiểm tra checksum/nội dung. Workflow `verify` chỉ đọc package hiện có. Workflow `deploy` yêu cầu package đã verify, backup database/artifact, SecureString secrets và kiểm tra HTTPS/IIS sau deploy.
- `quick_validate.py` của skill-creator đạt `Skill is valid`; chỉ cài tạm PyYAML qua `uv` để chạy validator. Không chạy production build, không tạo package mới và không deploy trong đợt tách skill này.

## Epic quản lý thông tin giáo viên `TCH` — owner: `root`

- [x] `TCH-P-01`. Rà soát hiện trạng User/Teacher/Group/Attendance và các bề mặt UI hiện có
- [x] `TCH-P-02`. Lập kế hoạch cross-stack có mã từng đợt tại `plans/03-TCH-teacher-management.md`
- [x] `TCH-P-03`. Đã review và chốt `TCH-DEC-01`–`TCH-DEC-12`
- [x] `TCH-00`. Khóa field ownership, REST/OpenAPI, wireflow và test traceability
- [x] `TCH-01`. Schema/migration/backfill, danh sách và chi tiết giáo viên
- [x] `TCH-02`. Atomic create, full update, validation và concurrency
- [x] `TCH-03`. Password, soft-delete, session và chuyển mutation khỏi User CRUD
- [x] `TCH-04`. Giữ policy điểm danh hiện tại và đồng bộ concurrency/UX nhóm
- [x] `TCH-05`. Audit/privacy, responsive, accessibility và hardening
- [x] `TCH-06`. Regression, tài liệu và bàn giao

### Teacher management planning log

- `TCH-P-01`: backend/frontend rà soát read-only và xác nhận hiện trạng có ba bề mặt rời nhau: account Teacher tại `/users`, policy Teacher trong `/student-groups`, còn `/teachers` mới chỉ có list/detail/policy API.
- `TCH-P-02`: plan đề xuất `User` tiếp tục là nguồn sự thật cho account; `Teacher` chỉ lưu mã và hồ sơ nghề nghiệp. `/teachers` trở thành aggregate API canonical, group assignment vẫn chỉ qua `student-groups` và lịch sử attendance không bị rewrite.
- Plan đề xuất sidebar/route `Giáo viên`, danh sách remote paging/filter/sort, detail, create/edit page, password/delete flow, toàn bộ UI tiếng Việt và optimistic concurrency bằng `expectedVersion`.
- Plan ban đầu liệt kê 12 quyết định về field, mã giáo viên, mutation boundary, policy, search và lifecycle; các quyết định đã được thu hẹp/cập nhật ở các dòng log tiếp theo.
- Đợt này chỉ thay đổi tài liệu plan/tracking/memory; không sửa source sản phẩm và không chạy build, package hoặc deploy production.
- Cập nhật quyết định (2026-08-11): Teacher chỉ thêm `teacher_code`, `note`, `version` trên schema hiện có; mã do người dùng nhập và được phép sửa; `/teachers` là mutation surface canonical; group assignment và policy vẫn ở trang `Nhóm`; không làm field nhân sự, self-service, upload hoặc ngày vào làm; giữ optimistic concurrency và soft-delete/history.
- Trước khi chốt `TCH-DEC-09`, plan đã mô tả cụ thể server-side search không dấu bằng PostgreSQL `unaccent`, tác động remote paging, quyền extension, benchmark và hướng `pg_trgm` nếu dữ liệu lớn.
- `TCH-DEC-09` chốt lần đầu dùng PostgreSQL `unaccent`, sau đó được thay thế theo quyết định mới ngay dưới đây.
- `TCH-DEC-09` cập nhật cuối (2026-08-11): vẫn bắt buộc search không dấu/case-insensitive tại server nhưng normalize/lọc ở tầng .NET API; không cài PostgreSQL extension. API lọc toàn bộ candidate trước khi tính `totalItems`/paging. Quy mô được xác nhận dưới 50 Teacher nên không cần guard/error riêng; vẫn có benchmark/log để phát hiện khi giả định này không còn đúng.

### Teacher management implementation status

Backend:

- [x] `TCH-BE-00`. Khóa DTO/endpoint/authorization/ProblemDetails/OpenAPI contract
- [x] `TCH-BE-01`. Entity/config/migration/backfill/unique/version
- [x] `TCH-BE-02`. List/detail và API-layer search không dấu
- [x] `TCH-BE-03`. Atomic create với mã tự nhập
- [x] `TCH-BE-04`. Full PUT, shared User coordinator và concurrency
- [x] `TCH-BE-05`. Password/delete/session/User CRUD boundary
- [x] `TCH-BE-06`. Policy endpoint dùng expectedVersion chung
- [x] `TCH-BE-07`. Audit/privacy/performance/OpenAPI hardening
- [x] `TCH-BE-08`. Unit/integration/docs/final verification

Frontend:

- [x] `TCH-FE-00`. Khóa models/wireflow/route/permission/dictionary tiếng Việt
- [x] `TCH-FE-01`. Navigation, list/filter/paging
- [x] `TCH-FE-02`. Teacher detail và nhóm phụ trách read-only
- [x] `TCH-FE-03`. Create/edit, mã tự nhập/sửa, validation và dirty guard
- [x] `TCH-FE-04`. Password/delete và chuyển User UI về tài khoản Admin
- [x] `TCH-FE-05`. Giữ policy tại trang Nhóm và đồng bộ version conflict
- [x] `TCH-FE-06`. Responsive/accessibility/error/search hardening
- [x] `TCH-FE-07`. Unit tests/docs/final verification

QA/Integration:

- [x] `TCH-QA-00`. Traceability contract/backend/frontend/test
- [x] `TCH-QA-01`. Migration/read/search/paging
- [x] `TCH-QA-02`. Create/update/code/concurrency/snapshot
- [x] `TCH-QA-03`. Auth/password/delete/session/User boundary
- [x] `TCH-QA-04`. Policy/group/attendance regression
- [x] `TCH-QA-05`. Privacy/a11y/mobile/network/race/performance
- [x] `TCH-QA-06`. Full development build/test, docs và final review

### Teacher management implementation log

- Bắt đầu `TCH-00` (2026-08-11): worktree sạch tại `a973259`; .NET SDK 10.0.302 và Node 20.11.0 sẵn sàng. Giao backend sở hữu `api/`, frontend sở hữu `ui/`; root điều phối contract/task/commit. Không chạy production skill.
- `TCH-BE-00`: khóa `/teachers` CRUD canonical, DTO rút gọn đúng plan, Admin/SuperAdmin authorization và stable ProblemCodes. User CRUD cũ trả `409 TeacherMustBeManagedViaTeachers` khi mutation Teacher; password endpoint vẫn độc lập. Policy route hiện tại nhận thêm `expectedVersion`; search không dấu xử lý tại .NET trước total/paging và không dùng DB extension.
- `TCH-FE-00`: exact backend contract đã align; thêm models/service cho list/detail/create/full PUT/delete/password/policy, group filters, version và dictionary lỗi tiếng Việt; khóa routes/guards/dirty wireflow. Angular development build đạt 10,93 MB, hash `0ce5d75b9be7434b`.
- `TCH-00` hoàn tất: backend/frontend DTO, permission, error và route contract thống nhất; test matrix trong plan bao phủ migration/search/concurrency/lifecycle/UI. Chuyển vertical slice `TCH-01`.
- `TCH-FE-01`: sidebar hiển thị `Giáo viên` cho Admin/SuperAdmin, `Tài khoản quản trị` chỉ SuperAdmin; Teacher list dùng CustomStore remote paging/sort/filter/search với server `totalItems`, debounce 300 ms, reset trang và adaptive grid. Development build tiếp tục đạt.
- `TCH-FE-02`: detail có state loading/retry/403/404/network bằng tiếng Việt; profile/account/policy và nhóm phụ trách đều read-only, CTA dẫn sang trang Nhóm. Edit/password/delete dùng đúng `teacherId`/`userId`/`version`; không tạo mutation group/policy thứ hai.
- `TCH-BE-01`: entity/config và EF migration `20260811150730_AddTeacherManagement` thêm editable `teacher_code` max 50/unique/uppercase check, nullable `note` max 2.000 và concurrency `version` >=1; backfill deterministic `GV-MIG-{UUID}` trước NOT NULL, không sequence/`unaccent`/search column. API build 0 warning/error; EF no pending model changes, chỉ còn warning query-filter lịch sử đã biết.
- `TCH-BE-02`: list/detail project User–Teacher–Group đúng DTO, filter status/group/unassigned, whitelist 8 sort field + id tie-break. Blank search dùng DB paging; search có giá trị materialize candidate sau structured filter, normalize FormD/combining marks/`đ→d`/case/whitespace và phone digits trước exact total/paging. Telemetry không ghi term/PII; API build 0 warning/error.
- `TCH-FE-03`: create/edit page riêng có teacherCode tự nhập/sửa và normalize uppercase, User fields + note, password/confirm chỉ khi tạo, nullable clear, full PUT `expectedVersion`, double-submit và dirty route/beforeunload guard. Validation focus field đầu; conflict 409 giữ draft và có CTA tải bản server.
- `TCH-BE-03`: POST Teacher tạo atomic User role Teacher + profile version 1/policy 7; mã tự nhập normalize uppercase, note nullable, shared password policy, 201 + Location/detail. Duplicate email/code race map stable conflict và rollback toàn transaction; build sạch.
- `TCH-BE-04`: full PUT dùng shared User coordinator và lock Teacher→User→groups sorted; stale trả 409/currentVersion. Code editable, phone/note null-clear, status revoke session; chỉ fullName tăng snapshot các group, toàn bộ update atomic và version +1; build sạch.
- `TCH-BE-05`: DELETE dùng query `expectedVersion`, chặn khi còn group, soft-delete User/revoke sessions nhưng giữ Teacher/code/history và version +1. User list/create/update/delete chuyển sang Admin-account surface; mutation Teacher trả `TeacherMustBeManagedViaTeachers`, password Teacher vẫn độc lập; build sạch.
- `TCH-BE-06`: policy route/UI giữ nguyên; body nhận policy + expectedVersion, dùng chung Teacher concurrency, tăng version +1 và trả full detail. Stale 409/currentVersion; range lỗi có `InvalidAttendanceEditWindow`; không tăng group snapshot.
- `TCH-FE-04`: password dùng đúng `userId`; delete gửi query `expectedVersion` với confirm/copy soft-delete. `/users` chỉ SuperAdmin, cố định role Admin, bỏ mọi khả năng tạo/sửa Teacher và đổi nhãn `Tài khoản quản trị`.
- `TCH-FE-05`: giữ tab policy và group assignment tại `/student-groups`; policy gửi row version, conflict 409 giữ popup và cho tải bản mới. Teacher detail chỉ đọc/deep-link sang policy. Development build đạt, hash `512a70a0ef434a52`.
- `TCH-FE-06`: adaptive grid/mobile forms/cards, labels/ARIA, toàn bộ Teacher errors và 403/404/network/retry bằng tiếng Việt; không local accent filter, conflict giữ draft. ChromeHeadlessCI 34/34 pass (chỉ known DevExtreme W0019/Inferno dependency warning); development build pass hash `512a70a0ef434a52`. FE-07 đang audit docs/memory/final delta, không production build.
- `TCH-FE-07`: hoàn tất docs/memory và bộ test frontend tăng lên 36/36 ChromeHeadlessCI; Angular development build pass 10,93 MB, hash `8b065f3873e100ee`. Diff-check phần `ui/` sạch; không chạy production/IIS/package/deploy.
- `TCH-BE-07`: thêm test search không dấu/literal/exact paging, full PUT/policy/version/group snapshot, lifecycle/session/User boundary, race và validation. Audit Teacher chỉ giữ ID, mã nghiệp vụ, trạng thái/presence/version/changedFields; không lưu raw note, email, họ tên, điện thoại, password hoặc request body.
- `TCH-BE-08`: solution build 0 warning/error; unit 32/32; PostgreSQL 17 Testcontainers Release 20/20, gồm fresh migration và nâng cấp `InitialCreate` → `AddAttendanceFoundation` → `AddTeacherManagement`; EF báo không có model change chưa migrate. README, `requests.http` và backend memory đã đồng bộ.
- `TCH-QA-06`: TCH hoàn tất toàn bộ `TCH-00`–`TCH-06`; frontend development build + 36/36 test và backend gates đều xanh. `git diff --check` sạch; production/IIS build, package và deploy không được gọi trong epic này.

### Teacher/group projection hotfix

- [x] `TCH-HF-01`. Sửa `responsibleTeacherName` bị `null` trên list/detail StudentGroup đã phân công
- [x] `TCH-HF-02`. Rà cùng lỗi projection của group fields trên Student list và bổ sung regression test
- [x] `TCH-HF-03`. Build/test backend, cập nhật memory và commit hotfix

- Điều tra ban đầu (2026-08-11): `StudentGroupService` dùng `.Select(x => Map(x))`; EF chỉ materialize entity cho client-side top-level mapping nên navigation `ResponsibleTeacher.User` và collection `Students` không được đưa vào SQL projection. Hệ quả có thể đồng thời làm `responsibleTeacherName=null` và `studentCount=0` dù FK đã tồn tại.
- Hotfix chuyển StudentGroup list/detail sang EF-translatable projection để lấy đúng tên giáo viên và số học sinh active; đồng thời sửa Student list trả đúng `groupCode/groupName`. Regression PostgreSQL riêng 1/1 và full integration Release 21/21 pass; solution Release build 0 warning/error.

## Epic phân nhóm và lịch học học sinh `SCH` — owner: `root`

- [x] `SCH-P-01`. Rà hiện trạng Student/Group/Attendance API và UI
- [x] `SCH-P-02`. Tạo plan cross-stack có mã đợt tại `plans/04-SCH-student-groups-study-schedule.md`
- [x] `SCH-P-03`. Review và khóa `SCH-DEC-01`–`SCH-DEC-08`
- [x] `SCH-00`. Khóa schema/REST/wireflow/attendance semantics/test traceability
- [x] `SCH-01`. Migration, Student schedule CRUD và optimistic concurrency
- [x] `SCH-02`. Phân/chuyển/gỡ nhóm tại trang Học sinh
- [x] `SCH-03`. Tích hợp scheduled roster vào attendance
- [x] `SCH-04`. Audit, responsive/accessibility và hardening
- [x] `SCH-05`. Full regression, tài liệu và bàn giao

### Student schedule planning log

- `SCH-P-01` (2026-08-11): API đã có nguồn mutation duy nhất `PUT /students/{id}/group`, group cap 100 và snapshot lock; Student UI chưa expose group filter/action. Attendance Missing hiện lấy toàn bộ Student active trong group và chưa biết lịch theo weekday.
- `SCH-P-02`: đề xuất Student có `StudyMode = FullDay|OneToOne`, lịch Thứ Hai–Thứ Bảy và aggregate version. Weekday được lưu bằng bit mask nội bộ nhưng API chỉ expose enum array canonical; group vẫn là command riêng.
- Phương án nghiệp vụ đề xuất: schedule lọc roster theo ngày; FullDay mặc định `Present`, OneToOne mặc định `OneToOneHour`; mode chỉ đặt default, không khóa status; Saved sheet bất biến. Thay đổi schedule của Student trong group tăng group snapshot.
- UI đề xuất thêm group/schedule columns và filters, popup phân/chuyển/gỡ nhóm, section lịch học với mode và sáu checkbox tiếng Việt; cả Student page và Group page dùng cùng endpoint/version.
- Review chéo backend/frontend đã bổ sung semantics exact cho `NoScheduledStudents`, count Missing/Saved, historical recovery, Student concurrency/no-op, migration bump mỗi group một lần, race schedule-vs-first-save, nested validation và UX group picker/attendance tiếng Việt.
- `SCH-P-03` hoàn tất (2026-08-12): yêu cầu `Thực thi plan SCH` được hiểu là chấp thuận `SCH-DEC-01`–`08` theo đề xuất. Backend/frontend bắt đầu song song từ commit sạch `361961c`; production/IIS skill không được gọi.
- `SCH-00` hoàn tất (2026-08-12): backend và frontend đã khóa cùng contract nested `studySchedule`, Student `version/expectedVersion`, group command versioned, `NoScheduledStudents` và enum JSON string; Angular development build checkpoint pass.
- `SCH-BE-01/02` checkpoint: Release build 0 warning/error; EF migration `20260811172348_AddStudentStudySchedule` backfill `FullDay` + mask Thứ Hai–Thứ Bảy + version 1, không tạo DB extension/cột search; CRUD/filter/version và audit privacy đã triển khai.
- `SCH-FE-01/02` checkpoint: Student grid dùng remote filter/paging/server total; form lịch học đủ 6 ngày, nested validation, dirty guard và conflict recovery; Angular development build pass.
- `SCH-BE-04` checkpoint: scheduled roster/default/empty/Saved/recovery semantics đã triển khai; race test phát hiện deadlock thật giữa Student `FOR UPDATE` và AttendanceRecord FK `KEY SHARE`, đã sửa Student lock thành `FOR NO KEY UPDATE`. Full PostgreSQL integration Release sau sửa pass 24/24.
- Frontend final checkpoint: ChromeHeadlessCI 50/50 và development build pass; không chạy production/IIS/package/deploy.
- `SCH-05` hoàn tất (2026-08-12): backend Release build 0 warning/error, unit 38/38, PostgreSQL 17 integration 24/24, EF no pending model changes; frontend test 50/50 và development build pass. README/memory/plan đã đồng bộ; production/IIS vẫn chưa được gọi.

### Student schedule implementation status

Backend:

- [x] `SCH-BE-00`. Khóa enum/schema/DTO/ProblemDetails/OpenAPI contract
- [x] `SCH-BE-01`. Student schedule/version migration và upgrade backfill
- [x] `SCH-BE-02`. Student CRUD/list/filter/concurrency/audit privacy
- [x] `SCH-BE-03`. Group command/delete expectedVersion và lifecycle regression
- [x] `SCH-BE-04`. Scheduled attendance roster/default/empty/recovery integration
- [x] `SCH-BE-05`. Unit/integration/EF/docs/memory/final verification

Frontend:

- [x] `SCH-FE-00`. Khóa explicit DTO/service/error/wireflow contract
- [x] `SCH-FE-01`. Student list group/schedule filters và summary
- [x] `SCH-FE-02`. Schedule create/edit form và validation
- [x] `SCH-FE-03`. Assign/move/unassign group popup dùng version
- [x] `SCH-FE-04`. Attendance scheduled roster/empty/recovery UX
- [x] `SCH-FE-05`. Responsive/a11y/tests/docs/memory/final verification

QA/Integration:

- [x] `SCH-QA-00`. Traceability quyết định → API → UI → test
- [x] `SCH-QA-01`. Fresh/upgrade migration, backfill và DB constraints
- [x] `SCH-QA-02`. Student CRUD/filter/version/group race
- [x] `SCH-QA-03`. Scheduled attendance/default/empty/snapshot/recovery race
- [x] `SCH-QA-04`. Vietnamese/responsive/a11y/error/network regression
- [x] `SCH-QA-05`. Full development build/test, docs và final review

## Epic giao diện điểm danh compact card `AUI` — owner: `root`

- [x] `AUI-P-01`. Phân tích hình tham chiếu và đối chiếu attendance UI/ATT/SCH contract hiện tại
- [x] `AUI-P-02`. Review và khóa `AUI-DEC-01`–`AUI-DEC-08`
- [x] `AUI-BE-00`. Khóa enum/DTO/summary/validation/OpenAPI và compatibility `halfDayPart` legacy
- [x] `AUI-BE-01`. EF migration/check constraint, fresh/upgrade proof
- [x] `AUI-BE-02`. Persisted `Unmarked`, half-day write/preserve semantics và audit
- [x] `AUI-BE-03`. Unit/integration/docs/memory/final gates
- [x] `AUI-FE-00`. Align `Unmarked`/summary/legacy DTO và test traceability
- [x] `AUI-FE-01`. Compact grid/card, identity rail và status pill
- [x] `AUI-FE-02`. Phép/không phép không dùng half-day part, notes UI 200 ký tự và compatibility dữ liệu cũ
- [x] `AUI-FE-03`. Persisted `Unmarked`, defaults/dirty/filter/summary/save integration
- [x] `AUI-FE-04`. Read-only/error/conflict/empty/recovery regression
- [x] `AUI-FE-05`. Responsive/a11y/test/development build/docs/memory
- [x] `AUI-QA-01`. Visual review desktop/tablet/mobile và khả năng mở rộng roster đến giới hạn 100
- [x] `AUI-QA-02`. Keyboard/zoom/contrast/touch target
- [x] `AUI-QA-03`. Missing/Saved/full-roster/dirty/conflict/recovery regression

### Attendance compact-card planning log

- `AUI-P-01` (2026-08-12): mẫu yêu cầu card khoảng 220–260 px, tên/mã ở thanh dọc, status/conditional dạng pill và textarea ghi chú; mục tiêu 5 card/hàng ở desktop 1366 px, nhiều hơn cuộn dọc.
- `AUI-DEC-01`–`03` chốt ngày 2026-08-12: `Unmarked/Chưa điểm danh` là persisted status do user chủ động chọn; Missing vẫn mặc định Present hoặc OneToOneHour theo schedule; AbsentHalfDay không dùng Morning/Afternoon, chỉ giữ phép/không phép và ghi chi tiết trong notes.
- Plan vì vậy có backend contract/migration delta: thêm enum + summary count + DB check, giữ `half_day_part` nullable cho legacy và không rewrite dữ liệu Saved cũ.
- `AUI` ghi đè riêng quy tắc `ATT` cũ bắt buộc Morning/Afternoon; các contract full-roster, version/snapshot, quyền và recovery còn lại giữ nguyên.
- `AUI-DEC-04`–`07` chốt ngày 2026-08-12: card chỉ hiển thị `nickname · studentCode`; redesign không áp dụng historical recovery trong v1; notes UI tối đa 200 ký tự; grid fluid hướng tới 5 card/hàng tại 1366 px.
- API notes vẫn giữ max 2.000 để tương thích; UI không được cắt giá trị cũ dài hơn 200 khi field chưa bị sửa.
- `AUI-DEC-08` chốt ngày 2026-08-12 theo đề xuất: bám nhóm màu trong hình nhưng điều chỉnh design token để đạt contrast/accessibility ở enabled, hover, focus, disabled và read-only.
- `AUI-P-02` hoàn tất; plan sẵn sàng triển khai từ `AUI-BE-00` + `AUI-FE-00`. Chưa triển khai source; production/IIS skill không được gọi trong giai đoạn plan.
- Bắt đầu implementation ngày 2026-08-12: backend và frontend thực hiện song song theo ownership; root điều phối contract, cập nhật task, review hợp nhất và không chạy production/IIS skill.
- `AUI-BE-00` khóa exact contract: persisted `Unmarked`, summary `unmarked`, Missing defaults giữ nguyên; mọi create mới kể cả recovery dùng AbsentHalfDay với `halfDayPart=null` + bắt buộc phép/không phép; Saved PUT chỉ bảo toàn Morning/Afternoon legacy khi status cũ và mới cùng AbsentHalfDay. API notes vẫn tối đa 2.000, không thêm URL/problem code.
- `AUI-FE-00` hoàn tất: model/dictionary/filter/summary đã align contract; Angular development build pass 10,99 MB, hash `ff00e1b07787d048`. Recovery giữ workflow cũ nhưng mọi half-day write mới gửi null.
- `AUI-BE-01` checkpoint: EF migration `20260811201427_AddAttendanceUnmarkedStatus` chỉ thay check constraint, không rewrite/drop legacy; Release build sạch và unit 40/40, đang chạy PostgreSQL fresh/upgrade integration.
- `AUI-FE-01` hoàn tất: main daily list dùng compact native-select pill, identity rail chỉ nickname/mã, token màu accessible và fluid/mobile grid; development build pass 11,00 MB, hash `59e3e9c0766a4cf7`, không sửa global styles.
- `AUI-FE-02` hoàn tất: main/recovery đều bỏ Morning/Afternoon và serialize `halfDayPart=null`; UI notes giới hạn 200 nhưng giữ nguyên Saved legacy >200 khi chưa sửa, edited >200 bị chặn đúng card. Focused ChromeHeadlessCI 13/13 pass.
- `AUI-FE-03` hoàn tất: UI giữ defaults Present/OneToOneHour từ API, persisted Unmarked với filter/summary/dirty và clear conditional fields; POST/PUT tiếp tục full roster. Attendance specs 13/13 pass.
- `AUI-BE-01/02` hoàn tất: full PostgreSQL 17 Testcontainers 26/26 pass, gồm persisted Unmarked/summary, new half-day null, preserve/clear legacy, audit privacy, Development OpenAPI, fresh DB và migration upgrade. Debug build 0 warning/error, unit 40/40, EF pending-model sạch; chỉ còn ba warning required-navigation đã biết.
- `AUI-BE-03` hoàn tất: README/requests/backend memory đồng bộ, scoped diff-check sạch; không production/publish/IIS. Dev API PID 23360 đã được dừng để giải phóng Debug DLL và chưa khởi động lại.
- `AUI-FE-04` hoàn tất: focused regression 16/16 pass cho Saved read-only, 409 giữ draft + reload, empty schedule và recovery manual/default/half-day-null/notes validation; không còn contract mismatch.
- `AUI-FE-05` hoàn tất: full ChromeHeadlessCI 59/59, focused attendance 16/16, development build 11,00 MB hash cuối `dfa785fd5adcf72a`, scoped diff-check sạch và không sửa global styles. Giữ fix regression một dòng Student group summary (`groupName`) vì full gate ban đầu 58/59 và rerun đạt 59/59. Chỉ còn warning dependency DevExtreme W0019/Inferno đã biết.
- `AUI-QA-01` hoàn tất bằng browser QA trên dữ liệu thật 7 học sinh: viewport 1366 px/content 1.036 px đạt 5 card hàng đầu, card khoảng 194 px và không tràn ngang; 1024 px đạt 4 card fluid; 390 px chuyển một cột, identity rail nằm ngang. Danh sách dùng grid/scroll và `trackBy`, không giới hạn client; invariant nhóm tối đa 100 tiếp tục được backend bảo vệ và regression suite hiện hữu bao phủ.
- `AUI-QA-02` hoàn tất: native status select điều khiển được bằng bàn phím từ `Present` đến `Unmarked` rồi phục hồi draft; mobile control/identity rail cao tối thiểu 44 px; viewport tương đương zoom/responsive không tràn ngang. Tỷ lệ tương phản text/background đo được: Present 6,73:1; Absent 7,18:1; Half-day 8,32:1; One-to-one 8,15:1; Unmarked 8,84:1.
- `AUI-QA-03` hoàn tất qua backend build/unit/integration 40/40 + 26/26, frontend full 59/59 + attendance 16/16 và browser smoke Missing. Các case persisted Unmarked, defaults theo lịch, full-roster POST/PUT, dirty/conflict/read-only/empty/recovery và legacy half-day/note đều có regression coverage; không chạy production/IIS.

## Epic hạ phiên bản Angular 12 / DevExtreme 19 `DX19` — owner: `frontend`

- [x] `DX19-FE-00`. Khóa Node 14.21.3, npm 8.19.4, package graph Angular 12.2.17 / DevExtreme 19.2.5 và sinh lockfile mới
- [x] `DX19-FE-01`. Tương thích Angular 12 workspace, polyfills và test bootstrap
- [x] `DX19-FE-02`. Chuyển API/type DevExtreme 23 sang DevExtreme 19, giữ nguyên hành vi
- [~] `DX19-FE-03`. Sinh lại theme 19.2.5 và giữ bố cục hiện tại
- [ ] `DX19-QA-01`. Hồi quy tự động toàn UI bằng development build và ChromeHeadlessCI
- [ ] `DX19-QA-02`. Smoke-test thủ công toàn bộ màn hình DevExtreme, cập nhật tài liệu và memory

### DX19 execution log

- 2026-08-14: người dùng đã chuyển đúng `node v14.21.3` / `npm 8.19.4`, xóa `ui/node_modules` và `ui/package-lock.json`; precondition đã được xác nhận lại. Chỉ chạy development build/test, không chạy production/IIS nếu chưa gọi `$gv-portal-production`.
- `DX19-FE-00` dependency resolution: npm 8.19.4 trên Windows đọc đúng prefix nhưng `install/ci` vẫn chạy Arborist theo current working directory; plan đã sửa hai lệnh lifecycle để chạy trực tiếp trong `ui`. Graph Node 14 cần pin thêm `browserslist 4.21.9`, `node-releases 2.0.13`, `sass 1.69.7` do registry drift của transitive range.
- `npm audit` sau khi khóa stack EOL báo 112 finding (6 low, 63 moderate, 38 high, 5 critical). Đây là rủi ro đã biết của yêu cầu Angular 12/DevExtreme 19; không chạy `npm audit fix` vì sẽ phá version matrix. Cần đánh giá bảo mật riêng trước production.
- `DX19-FE-00` hoàn tất tại commit `a479996`: lockfile v2, dependency tree/CLI/ThemeBuilder đúng pin; task review không còn finding material. Không sinh theme, không chạy production/IIS.
- Build probe của Task 2 phát hiện override Browserslist ban đầu thiếu API Babel; corrective commit `52248bb` pin `browserslist 4.28.8` + `node-releases 2.0.44`. Runtime/build probe và review xác nhận tương thích Node 14, không còn lỗi Babel.
- `DX19-FE-01` hoàn tất tại commit `9ea2bc7`: Angular 12 bundle generation và Karma bootstrap hoạt động; 36 lỗi còn lại đều thuộc DevExtreme 19 API/type ở Task 3. Reviewer approve, không nới strict hoặc xóa test.
- `DX19-FE-02` hoàn tất qua các commit `baeca11`, `47ae290`, `02402cf`: compile errors 36→0, bootstrap bundled theme đúng lifecycle v19, popup dirty guard tương thích event-cancel đồng bộ và Deferred confirm. Full ChromeHeadlessCI 64/64, development build pass; reviewer cuối approve không còn finding.

## Tổ chức thư mục kế hoạch — owner: `root`

- [x] `PLAN-ORG-01`. Chuyển toàn bộ plan ra thư mục root `plans/`
- [x] `PLAN-ORG-02`. Đánh số thứ tự và gắn mã `BASE`, `ATT`, `TCH` vào tên file
- [x] `PLAN-ORG-03`. Tạo `plans/README.md` làm mục lục thứ tự/phụ thuộc/trạng thái
- [x] `PLAN-ORG-04`. Đồng bộ toàn bộ link trong agent rules, memory, README và task log

### Plan organization log

- Thứ tự đã khóa theo dependency và lịch sử triển khai: `01-BASE` → `02-ATT` → `03-TCH`.
- `BASE` là contract nền; các plan có số lớn hơn chỉ mở rộng hoặc override trong phạm vi epic của mình.
- Không để stub tại đường dẫn cũ trong `api/`; mọi tài liệu/agent phải đọc qua `plans/README.md` và đường dẫn mới.
- Đây là thay đổi tổ chức Markdown/TOML, không thay đổi runtime source; không chạy product build/test hoặc production skill.
