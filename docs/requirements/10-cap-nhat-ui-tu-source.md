# 10 — Cập nhật UI cho API chạy từ source

## Mô hình

- Máy chạy app clone repository, cài .NET SDK 10 và tự cấu hình `appsettings.Development.json`, `client_secret.json`.
- API phục vụ SPA tại cùng host, dùng thư mục `api/src/AdminPortal.Api/ClientApp/build`.
- Máy chạy app không cần Node/npm để nhận bản UI đã build.

## Máy source/build

- Một script build UI production với API base URL tương đối `/api/v1`.
- Copy build vào `ClientApp/build` của source hiện tại, sau đó đóng gói ZIP chỉ chứa `ClientApp/build`.
- Tên ZIP phân biệt gói UI và chứa thời gian UTC đủ để xác định bản mới nhất.
- Upload vào folder Google Drive `packages` đã chỉ định: `1EJK12xqBWBlsVchCeXUlwYOpouzZ_D_B`.
- Gói không chứa source API, cấu hình Development, OAuth credential hoặc token.

## Máy chạy app

- Launcher đọc folder local được Google Drive đồng bộ, mặc định `G:\My Drive\scripts\QLMamNonPackages\packages`; đường dẫn trong `deploy/spa/start-app.config.json`, có thể chỉnh hoặc chọn file khác bằng `-ConfigFile`.
- Script liệt kê ZIP trực tiếp trong folder đó, lọc bằng regex tên gói và đọc timestamp trong tên; không gọi Google API/OAuth để tải package trên máy app.
- Chọn timestamp hợp lệ mới nhất, không dùng thời gian upload/sửa Drive để chọn phiên bản.
- Copy gói vào thư mục tạm trong source; kiểm tra kích thước/checksum với ZIP nguồn, cấu trúc ZIP và `index.html` trước khi thay build đang dùng. Không sửa ZIP nguồn; ZIP đang đồng bộ/hỏng phải giữ UI hiện có.
- Giữ bản build cũ để khôi phục khi bước thay thư mục lỗi; không ghi đè cấu hình/credential hoặc source API.
- Máy nguồn upload dùng OAuth/token cục bộ được Git bỏ qua. Máy app chỉ cần folder đồng bộ sẵn; credential Google của API là phần cấu hình app riêng.

## Ranh giới

- Đây là luồng bổ sung cho chạy app từ source; mô hình hai IIS site vẫn là luồng riêng.
- Tạo script không đồng nghĩa đã chạy production build, upload hoặc cập nhật máy app. Gate thực thi production trong `AGENTS.md` vẫn áp dụng cho agent.

## Launcher một lần bấm và cập nhật khi app đang chạy

- Root npm có `build:spa` (bỏ npm install và upload luôn), `ui:update` (folder local theo config, incremental/live), `app:start` (launcher ẩn); giữ các lệnh hiện có.
- Máy app chỉ cần một entry launcher PowerShell, không cần Node/npm. Có chế độ tạo shortcut `GV Portal` trên Desktop gọi launcher ẩn bằng đường dẫn tuyệt đối.
- Launcher kiểm tra gói mới ở folder local trong `start-app.config.json`; lưu phiên bản sau cài thành công, bỏ copy gói đã có, không downgrade; UI thiếu/hỏng phải được cài lại.
- Cập nhật UI giữ nguyên API đang chạy. Copy asset trước, index sau, giữ bundle cũ cho tab đang mở, backup và rollback khi copy lỗi.
- Folder/ổ đồng bộ chưa sẵn sàng, cấu hình hoặc cập nhật lỗi nhưng UI hiện có hợp lệ thì tiếp tục dùng UI đó và tự chạy API nếu chưa có. Nếu không có UI hợp lệ thì dừng launcher.
- API chưa chạy thì khởi động nền với Development, SPA bật và HTTPS 7158/HTTP 5158. API của source này đang chạy thì giữ nguyên, không restart. Cổng bị ứng dụng/source khác chiếm thì báo log và không can thiệp tiến trình đó.
- Khóa theo source root để tránh hai lượt launcher đồng thời. Chờ HTTPS `/health/live` thành công tối đa 120 giây rồi mở `https://localhost:7158/api/v1/google-sheets/credential-smoke?return=%2F`; không gọi endpoint Google trong health polling.
- Launcher kết thúc sau khi mở trình duyệt, API tiếp tục chạy nền không có cửa sổ console. Lỗi khởi động/health thì không mở trình duyệt. Log launcher/update/API nằm trong artifacts cục bộ.
- Không tự cấu hình certificate store, bí mật, Git hoặc database. Máy đích chuẩn bị trước SDK/database/config/certificate; API khởi động theo cấu hình runtime sẵn có.
