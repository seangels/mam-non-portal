# Danh mục task phát triển

Đây là nơi theo dõi trạng thái triển khai từ sau ngày 2026-08-25. Các task mới phải nằm trong `docs/tasks/**`, không ghi thêm vào file `tasks.md` ở root.

## Quy ước

- Mỗi epic có một thư mục riêng: `NN-CODE/`, ví dụ `07-ASH/`.
- Mỗi thư mục task nên có:
  - `status.md`: dashboard trạng thái hiện tại.
  - `log.md`: lịch sử thực hiện theo thời gian.
  - `details/`: mô tả chi tiết từng mã task nếu cần.
- Từ 2026-08-25, mỗi file trong `details/` phải gắn icon trạng thái ngay trước từng mục công việc/kết quả để nhìn nhanh mục nào đã xong:
  - `✅` đã xong
  - `🔄` đang làm
  - `⬜` chưa làm
  - `⚠️` bị chặn/cần quyết định
  Không cần tạo bảng riêng trong file detail; chỉ thêm icon vào từng bullet.
- Khi đổi trạng thái, cập nhật `status.md` và thêm log tương ứng vào `log.md`.
- Memory agent chỉ giữ handoff ngắn gọn; log chi tiết nằm ở đây.

## Task hiện hành

| Thứ tự | Mã | Dashboard | Log |
|---:|---|---|---|
| 00 | `DOC-GOV` | [`00-DOC-GOV/status.md`](00-DOC-GOV/status.md) | [`00-DOC-GOV/log.md`](00-DOC-GOV/log.md) |
| 07 | `ASH` | [`07-ASH/status.md`](07-ASH/status.md) | [`07-ASH/log.md`](07-ASH/log.md) |

## Legacy archive

- File task chung cũ ở root đã được backup tại [`archive/root-tasks-legacy-2026-08-25.md`](archive/root-tasks-legacy-2026-08-25.md).
- Chỉ dùng archive để tra cứu lịch sử cũ khi cần. Không cập nhật archive và không dùng `tasks.md` ở root cho task mới.
