# Danh mục kế hoạch phát triển

Các file được đánh số theo thứ tự phụ thuộc và triển khai. Mã ở tên file là mã epic dùng trong `tasks.md` và commit Git.

| Thứ tự | Mã | Kế hoạch | Trạng thái | Phụ thuộc |
|---:|---|---|---|---|
| 01 | `BASE` | [`01-BASE-admin-portal.md`](01-BASE-admin-portal.md) | Đã triển khai | Không |
| 02 | `ATT` | [`02-ATT-attendance.md`](02-ATT-attendance.md) | Đã triển khai | `BASE` |
| 03 | `TCH` | [`03-TCH-teacher-management.md`](03-TCH-teacher-management.md) | Đã triển khai | `BASE`, `ATT` |
| 04 | `SCH` | [`04-SCH-student-groups-study-schedule.md`](04-SCH-student-groups-study-schedule.md) | Đã triển khai | `BASE`, `ATT` |
| 05 | `AUI` | [`05-AUI-attendance-compact-cards.md`](05-AUI-attendance-compact-cards.md) | Đã chốt — sẵn sàng triển khai | `ATT`, `SCH` |

## Quy ước đặt tên

```text
NN-CODE-ten-tinh-nang.md
```

- `NN`: thứ tự hai chữ số, thể hiện kế hoạch làm trước/làm sau.
- `CODE`: mã epic ổn định để dùng cho task, branch và commit.
- Khi thêm kế hoạch mới, cấp số tiếp theo; không đổi số/mã của kế hoạch đã triển khai.
- Trạng thái chi tiết và execution log vẫn được theo dõi tại [`../tasks.md`](../tasks.md).

Production build, đóng gói và deploy IIS không phải một plan tính năng trong thư mục này; chỉ thực hiện khi người dùng gọi `$gv-portal-production`.
