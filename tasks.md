# Theo dõi triển khai Admin Portal

Tài liệu nguồn: [`api/plan.md`](api/plan.md)

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
- Điều chỉnh contract (2026-08-11): update user/student dùng `PUT` full replacement để nullable field có thể được xóa bằng `null`; `api/plan.md` đã đồng bộ. Ngày sinh thiếu (`0001-01-01`) cũng bị validation từ chối.
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

- Khởi tạo: `api/plan.md` là contract nguồn; backend sở hữu `api/`, frontend sở hữu `ui/`.
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
