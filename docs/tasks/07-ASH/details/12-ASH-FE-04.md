# ASH-FE-04 — Build/test:ci mặc định, tài liệu, smoke phần frontend

Owner: `frontend`. Phụ thuộc: `ASH-FE-03`. Trạng thái: xem [`../status.md`](../status.md). Log lịch sử: [`../log.md`](../log.md).

Nguồn: [plan mục 9](../../../plans/07-ASH-assessment-sheet.md#9-test--smoke--phạm-vi-đã-được-người-dùng-giới-hạn).

## Mục đích

Khép lại phần frontend: chạy gate mặc định của repo, đồng bộ tài liệu, và phối hợp backend chạy trọn 10 bước smoke test qua UI thật. **Không xây bộ test UI chuyên sâu (Karma component test chi tiết, ma trận visual/responsive/accessibility) và không test performance** cho epic này — đúng yêu cầu đã chốt.

## Nội dung cụ thể cần làm

- Chạy default verification gate theo `AGENTS.md`: `npm --prefix ui run build -- --configuration development`, `npm --prefix ui run test:ci`.
- Cập nhật tài liệu/handoff: `.agents/frontend/MEMORY.md`, `docs/tasks/**` (dòng liên quan nếu có), `docs/plans/README.md` nếu cần.
- Phối hợp `ASH-QA-01`: chạy trọn 10 bước smoke test golden path qua UI thật (không chỉ qua API như `ASH-BE-05` đã làm riêng phần backend), đặc biệt các bước liên quan UI: 1, 2, 4, 6, 8, 10 trong [`13-ASH-QA-01.md`](13-ASH-QA-01.md).
- Không thêm hạng mục kiểm thử ngoài phạm vi đã giới hạn (không responsive/accessibility matrix, không performance/load).

## Kết quả mong đợi (Definition of Done)

Build development + `test:ci` pass; tài liệu/memory cập nhật; các bước smoke phía UI chạy được thật trên trình duyệt, không giả lập.
