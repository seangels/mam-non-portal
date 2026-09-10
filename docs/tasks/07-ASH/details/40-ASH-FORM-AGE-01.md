# ASH-FORM-AGE-01 — Hiển thị tuổi học sinh trên form đánh giá

## Tóm tắt ngắn

1. ✅ Summary snapshot `Học sinh` trên form edit hiển thị thêm tuổi hiện tại dạng năm/tháng.
2. ✅ Tái sử dụng helper `calculateAgeText`; không tạo công thức tính tuổi mới.
3. ✅ Có regression test và frontend test/build development đều đạt.

## Phạm vi

- Chỉ thay đổi giao diện form chỉnh sửa AssessmentSheet.
- Tuổi được tính từ `StudentSnapshot.DateOfBirth` tại ngày hiện tại (`new Date()`).
- Chuỗi ví dụ: `S101 · Bé An (An) · 6 tuổi, 9 tháng`.
- Khi snapshot thiếu ngày sinh, giữ fallback `Chưa có thông tin` của helper hiện có.
- Không thay đổi backend, REST contract, database schema, Google Sheet/Drive hoặc luồng PDF.

## Thực hiện

- `assessment-sheets-form.component.ts`: import và gọi `calculateAgeText` trong `buildStudentSummary`.
- `assessment-sheets.component.spec.ts`: thêm regression case tuổi năm/tháng và cập nhật assertion fallback khi thiếu ngày sinh.

## Kiểm chứng

- `npm --prefix ui run test:ci`: **208/208 pass**.
- `npm --prefix ui run build -- --configuration development`: **pass**, hash `65246eafe3ff45524737`.
- `git diff --check`: sạch; chỉ có cảnh báo chuyển LF/CRLF quen thuộc.
- Browser smoke: chưa chạy.
