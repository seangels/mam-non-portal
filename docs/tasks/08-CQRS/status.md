# Theo dõi tiến độ — CQRS: Backend Mediator + CQRS

Nguồn: [`../../plans/08-CQRS-backend-mediator-cqrs.md`](../../plans/08-CQRS-backend-mediator-cqrs.md).

## Quy ước trạng thái

- `[ ]` Chưa bắt đầu
- `[~]` Đang thực hiện
- `[x]` Hoàn thành và đã kiểm tra
- `[!]` Bị chặn — xem chi tiết trong task/log

Trong file detail dùng icon:

- `⬜` chưa làm
- `🔄` đang làm
- `✅` đã xong
- `⚠️` bị chặn/rủi ro
- `❓` cần làm rõ

## Tổng quan

| Giai đoạn | Tổng số việc | Chưa bắt đầu | Đang làm | Hoàn thành | Bị chặn |
|---|---:|---:|---:|---:|---:|
| Backend | 7 | 6 | 0 | 1 | 0 |

## Backend — owner: `backend`

| Status | Mã | Việc cần làm | Phụ thuộc |
|---|---|---|---|
| `[x]` | [`CQRS-BE-00`](details/01-CQRS-BE-00.md) | Tạo mediator foundation nội bộ và migrate `Users` controller/use cases sang command/query | `BASE` |
| `[ ]` | `CQRS-BE-01` | Migrate `Setup` và `Auth` sang command/query, giữ cookie/CSRF ở controller | `CQRS-BE-00` |
| `[ ]` | `CQRS-BE-02` | Migrate `Students`, gồm scoped read của Teacher và group assignment | `CQRS-BE-01` |
| `[ ]` | `CQRS-BE-03` | Migrate `Teachers` và `StudentGroups` | `CQRS-BE-02` |
| `[ ]` | `CQRS-BE-04` | Migrate `Attendance` | `CQRS-BE-03` |
| `[ ]` | `CQRS-BE-05` | Migrate `Assessments`, `AssessmentGroups`, `AssessmentSheets`, Google actions/upload PDF | `CQRS-BE-04` |
| `[ ]` | `CQRS-BE-06` | Cleanup service interfaces cũ, docs/memory, full backend gate | `CQRS-BE-05` |
