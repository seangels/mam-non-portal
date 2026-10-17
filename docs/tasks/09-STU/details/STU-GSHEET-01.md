# STU-GSHEET-01 — Đồng bộ Student sang Google Sheet

## Tóm tắt ngắn

1. ✅ Lưu spreadsheet/tab danh sách học sinh bằng `GoogleSheets:StudentSpreadsheetId` và `GoogleSheets:StudentSheetId` (gid).
2. ✅ Khi tạo Student, tìm theo `studentCode` ở cột B; nếu chưa có thì tạo dòng với khóa B và dữ liệu C/D/H.
3. ✅ Khi sửa Student, chỉ cập nhật Tên (C), Nickname (D), Ngày sinh (H), không ghi đè cột khác.
4. ✅ Bổ sung kiểm thử và xử lý lỗi để không báo thành công giả khi Google Sheets chưa đồng bộ.
5. ✅ Backend build/unit test đạt; migration và EF model đã được kiểm tra.

## Cấu trúc sheet đã xác nhận

- ✅ Spreadsheet ID: `1_Os9ehdhwbSOHYxwcyQwMapknJOCIMIAH_uuQpTzIBM`.
- ✅ Tab: `TT Học sinh`, gid `1543249668`, header ở dòng 1.
- ✅ Khóa dòng: B (`StudentID`) khớp `Student.studentCode`.
- ✅ Dữ liệu được phép ghi: C (`Tên`), D (`Tên ở nhà của Bé`), H (`Ngày sinh (date)`).
- ✅ F (`Ngày sinh`) là công thức tham chiếu H và G (`Tuổi`) là công thức mảng; không được ghi hai cột này.
- ✅ H dùng kiểu ngày với format `dd/MM/yyyy`; adapter phải ghi giá trị ngày không phụ thuộc cách parse chuỗi theo locale.

## Phạm vi và điều kiện biên

- ✅ Spreadsheet ID và gid lấy từ `GoogleSheets:StudentSpreadsheetId`/`StudentSheetId`, không hard-code trong `StudentService`; backend resolve tên tab hiện tại từ gid trước khi dùng A1 range.
- ✅ `appsettings.json` để trống `StudentSpreadsheetId` và dùng gid mặc định 0; giá trị thật chỉ nằm trong ignored local `appsettings.Development.json` hoặc nguồn cấu hình runtime của môi trường đích.
- ✅ Lookup so khớp mã học sinh đã chuẩn hóa, không phân biệt hoa/thường và từ chối mã trùng trong sheet.
- ✅ Create gặp dòng B có sẵn cập nhật C/D/H của đúng dòng thay vì append trùng.
- ✅ Create chưa có mã dùng dòng B trống đầu tiên, chỉ ghi B/C/D/H.
- ✅ Update không tìm thấy khóa sheet bất biến trả lỗi đồng bộ rõ ràng; không tự tạo bản ghi ngoài ý muốn.
- ✅ Không thay đổi REST contract/UI hiện hành; create/update Student vẫn là điểm kích hoạt đồng bộ.
- ✅ Không chạy production build, package hoặc deploy trong task này.

## Verification

- ✅ `dotnet build api/AdminPortal.slnx -c Release --no-restore`: đạt, 0 warning/error.
- ✅ `dotnet test api/tests/AdminPortal.UnitTests -c Release --no-restore`: đạt 138/138.
- ✅ `dotnet tool run dotnet-ef migrations has-pending-model-changes ...`: không còn model change chờ migration.
- ⚠️ Integration runtime: not run (Docker not available); project integration đã compile trong solution build.
- ✅ Test dùng fake, không gọi ghi vào Google Sheet thật.
