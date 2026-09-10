# ASH-CACHE-PAGE-01 — Tăng kích thước lô cache Assessment

## Tóm tắt ngắn

1. ✅ Form AssessmentSheet tải mỗi trang Assessment với `pageSize=1000` thay vì 100.
2. ✅ Picker Assessment dùng cùng kích thước lô 1000 và vẫn tải đủ mọi trang.
3. ✅ Test tập trung, full frontend suite và build development đều đạt.

## Phạm vi

- Đổi hằng `ASSESSMENT_CACHE_PAGE_SIZE` trong form và picker từ `100` lên `1000`.
- Giữ vòng lặp `totalPages`, cache local, filter, select-all và phân trang hiển thị DataGrid như cũ.
- Không đổi page size của SelectBox học sinh/giáo viên.
- Không đổi backend/API/schema; `GET /api/v1/assessments` đã chấp nhận `pageSize` tối đa 5000.

## Thực hiện

- `assessment-sheets-form.component.ts`: cache loader dùng lô 1000.
- `assessment-picker.component.ts`: cache loader dùng lô 1000.
- `assessment-sheets.component.spec.ts`: cập nhật fixtures/assertions và kiểm tra cả hai loader tiếp tục gọi trang 2 với `pageSize: 1000`.
- `assessment-results.component.spec.ts`: đồng bộ test sticky-bar cũ với production implementation `scrollIntoView` đã có trên HEAD; không đổi source runtime.

## Kiểm chứng

- Focused AssessmentSheet tests: **101/101 pass**.
- Full `npm --prefix ui run test:ci`: **208/208 pass**.
- `npm --prefix ui run build -- --configuration development`: **pass**, hash `27123ece4b665d606a86`.
- `git diff --check`: sạch; chỉ có cảnh báo LF/CRLF quen thuộc.
- Không chạy production/IIS/deploy; browser smoke chưa chạy.
