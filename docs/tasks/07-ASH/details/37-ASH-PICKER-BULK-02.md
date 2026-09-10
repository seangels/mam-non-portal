# ASH-PICKER-BULK-02 — Kết quả hiện tại và bulk remove trong picker

## Tóm tắt ngắn

1. ✅ Bảng `Danh mục đánh giá đã chọn` thêm cột `Kết quả hiện tại`, lấy latest grade theo học sinh.
2. ✅ Tiêu đề `Kế hoạch` có checkbox `Hiện tại` để ẩn/hiện cột kết quả hiện tại; mặc định hiện.
3. ✅ Picker lọc vòng 1 theo `Trong KH`/`Chưa có trong KH` bằng TagBox toolbar; không hiển thị cột membership.
4. ✅ Checkbox picker cho chọn cả mục đã có; toolbar có nút bỏ nhiều mục khỏi kế hoạch.
5. ✅ Bulk add/remove đều dùng một request full-replace, phân loại đúng lựa chọn hỗn hợp và chỉ clear selection khi thành công.
6. ✅ Bổ sung regression test và chạy frontend test/build development.
7. ✅ Form có checkbox `Hiện danh mục` để ẩn/hiện bảng mục đã chọn mà không thay đổi records.
8. ✅ Toolbar picker có dropdown đa chọn `Trong KH`/`Chưa có trong KH`, độc lập với filter row/header filter vòng 2; chọn cả hai hoặc bỏ hết tương đương không lọc.

## Phạm vi

- Frontend: AssessmentSheet edit records table, assessment picker, request builder và Jasmine tests liên quan.
- Dùng lại `GET /assessments?studentId=...` và `PUT /assessment-sheets/{id}/records`; không đổi backend/REST/schema.
- Không production build, IIS hoặc deploy.

## Điều kiện hoàn thành

- ✅ Latest grade hiển thị ngay khi mở form edit, không phụ thuộc việc người dùng đã mở picker.
- ✅ Checkbox `Hiện tại` có accessible label và toggle đúng header/body column.
- ✅ Picker có TagBox filter vòng 1 cho trạng thái kế hoạch; cột `Trong kế hoạch` được ẩn khỏi lưới.
- ✅ Nút thêm chỉ xử lý lựa chọn chưa có; nút bỏ chỉ xử lý lựa chọn đã có, mỗi nút có count và disabled state đúng.
- ✅ Bulk remove loại đúng records trong một `replaceRecords`; lỗi giữ selection để retry.
- ✅ Frontend test đạt **185/185**; development build đạt hash `0b8b7ea64e37197c22ef`. Browser smoke chưa chạy.
