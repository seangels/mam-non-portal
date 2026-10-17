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

- Một script liệt kê đầy đủ các trang file trong folder Drive, lọc bằng regex tên gói và đọc timestamp trong tên.
- Chọn timestamp hợp lệ mới nhất, không dùng thời gian upload/sửa Drive để chọn phiên bản.
- Kiểm tra kích thước/checksum tải về, cấu trúc ZIP và `index.html` trước khi thay build đang dùng.
- Giữ bản build cũ để khôi phục khi bước thay thư mục lỗi; không ghi đè cấu hình/credential hoặc source API.
- OAuth đăng nhập lần đầu trên từng máy, lưu token cục bộ được Git bỏ qua; những lần sau dùng refresh token.

## Ranh giới

- Đây là luồng bổ sung cho chạy app từ source; mô hình hai IIS site vẫn là luồng riêng.
- Tạo script không đồng nghĩa đã chạy production build, upload hoặc cập nhật máy app. Gate thực thi production trong `AGENTS.md` vẫn áp dụng cho agent.
