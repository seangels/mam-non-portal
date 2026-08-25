# ASH-BE-01 — Domain/EF configuration/migration

Owner: `backend`. Phụ thuộc: `ASH-BE-00`. Trạng thái: xem [`../status.md`](../status.md). **Đã hoàn thành 2026-08-20** — xem [`../log.md`](../log.md) mục Backend log.

Nguồn: [plan mục 5, 10](../../../plans/07-ASH-assessment-sheet.md#5-thay-đổi-domaindata-cần-chốt-kỹ-thuật).

## Mục đích

Wire 4 entity (`AssessmentSheet` — gồm field mới `AssessmentSheetSpreadsheetId`, `AssessmentRecord`, `AssessmentSheetLatest`, `AssessmentRecordLatest`) vào EF Core/PostgreSQL thật — hiện chúng chỉ là file C# chưa từng chạy migration. Đây là bước hạ tầng dữ liệu, chưa có business logic. `AssessmentSheetSpreadsheetId` chỉ là một cột string nullable bình thường trên `AssessmentSheet` — không cần bảng/quan hệ riêng.

## Nội dung cụ thể cần làm

- Thêm `IEntityTypeConfiguration` cho cả 4 entity trong `api/src/AdminPortal.Infrastructure/Persistence/Configurations/` (snake_case, đúng convention hiện có — xem `AssessmentConfiguration.cs`), gồm cột mới `AssessmentSheetSpreadsheetId` trên `AssessmentSheetConfiguration`.
- Đăng ký `DbSet<AssessmentSheet>`, `DbSet<AssessmentRecord>`, `DbSet<AssessmentSheetLatest>`, `DbSet<AssessmentRecordLatest>` vào `DbContext`.
- Cấu hình quan hệ:
  - `AssessmentSheet` 1–nhiều `AssessmentRecord` (khoá ngoại `AssessmentSheetId`).
  - `AssessmentSheetLatest` 1–nhiều `AssessmentRecordLatest` (khoá ngoại `AssessmentSheetLatestId`).
  - Complex type `StudentSnapshot`, `AssessmentSnapshot` dạng `jsonb` trên cả 2 cặp entity, giống cách khai báo `[ComplexType]` hiện có.
- Áp dụng khoá upsert đã chốt ở `ASH-DEC-05`: unique index trên `AssessmentSheetLatest.StudentId`; unique index trên `AssessmentRecordLatest` theo (`AssessmentSheetLatestId`, mục đánh giá). **Đính chính 2026-08-25:** bản trung gian từng thêm `AssessmentRecordLatest.AssessmentCode` vì EF không index được `AssessmentSnapshot.Code` trong JSON; source hiện hành đã thay bằng FK `AssessmentId`/`Assessment` và unique index (`AssessmentSheetLatestId`, `AssessmentId`). Không triển khai mới theo `AssessmentCode`.
- Bỏ `ClosedDate` trên `AssessmentSheetLatest` theo `ASH-DEC-03` (nếu chưa xoá ở `ASH-BE-00`).
- Sinh migration mới bằng `dotnet-ef` theo đúng quy trình ở `api/AGENTS.md` (không sửa tay `AdminPortalDbContextModelSnapshot.cs`), kèm Designer file.
- Chạy `dotnet-ef migrations has-pending-model-changes` để xác nhận không còn model change chưa ghi nhận.

## Kết quả mong đợi (Definition of Done)

Migration mới apply được từ database sạch; `has-pending-model-changes` trả về không còn thay đổi; `dotnet build` toàn solution 0 warning/0 error. Chưa cần service/API — chỉ cần hạ tầng dữ liệu đúng, bao gồm cả cặp bảng chỉ-đọc `AssessmentSheetLatest`/`AssessmentRecordLatest`.

**Đã đạt được:** migration `20260820094414_AddAssessmentSheetManagement` tạo 4 bảng (`assessment_sheets`, `assessment_records`, `assessment_sheet_latests`, `assessment_record_latests`), `has-pending-model-changes` xanh, build `Domain`/`Application`/`Infrastructure`/`Api` 0 warning/0 error, 40/40 unit test hiện có vẫn pass (chưa có test riêng cho entity mới). Phải fix thêm 1 vấn đề analyzer (`CA1861`, `TreatWarningsAsErrors=true`) trong file migration sinh ra — 2 lệnh `CreateIndex` composite-column được EF Core scaffold dạng `new[] { ... }` inline bị flag; đã hoist thành `private static readonly string[]` field ngay trong file migration, không đổi hành vi.

Log lịch sử của task này nằm ở [`../log.md`](../log.md), không lặp lại trong file này.
