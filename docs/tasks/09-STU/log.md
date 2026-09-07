# Log thực hiện — Học sinh

- `STU-STATUS-01` (2026-09-07): bắt đầu thay đổi quy tắc cho phép học sinh đang thuộc nhóm chuyển sang `Inactive`/`Đã nghỉ` mà vẫn giữ nguyên nhóm. Quy tắc xóa học sinh còn nhóm và phân/chuyển nhóm cho học sinh inactive không đổi.
- `STU-STATUS-01` (2026-09-07): hoàn tất API/UI. Full PUT giữ `groupId`, tăng Student version và group snapshot khi status làm đổi active roster; UI bỏ guard gỡ nhóm và đổi nhãn `Inactive` thành `Đã nghỉ`. Bổ sung bảo vệ giới hạn 100 học sinh active khi bật lại trạng thái `Active`. Verification: backend build đạt 0 warning/error, unit 113/113; frontend test 168/168 và development build đạt. Integration không chạy vì Docker daemon không khả dụng; không có migration hoặc deploy.
