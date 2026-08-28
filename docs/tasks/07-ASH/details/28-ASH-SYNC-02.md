# ASH-SYNC-02 — Assessment catalog text giữ newline từ sync tới snapshot

## Tóm tắt ngắn

1. ✅ Đảo lại quyết định "một dòng" ngày 2026-08-28. `Assessment.Name` + `GroupLv1Name` + `GroupLv2Name` + `GroupLv3Name` **phải giữ nguyên xuống dòng** từ khi sync Google Sheets cho tới `AssessmentRecord.AssessmentSnapshot`.
2. ✅ `AssessmentSyncTextNormalizer.NormalizeOptionalName` giờ chỉ `string.IsNullOrWhiteSpace(value) ? null : value.Trim()` — cắt whitespace hai đầu, giữ nguyên xi mọi thứ bên trong (CRLF/LF, space thừa, dòng trống). `NormalizeRequiredName` vẫn trả `string.Empty` khi rỗng.
3. ✅ Các đường ghi khác đã sẵn "trim-only", không cần sửa: `AssessmentSheetRules.BuildReplacementRecord` (copy `assessment.Name`/`GroupLv1Name` verbatim; `NormalizeOptional` = `Trim()`), import Excel (`AssessmentSheetService` dùng `.Trim()`), `AssessmentSnapshotReplacementRules.Apply` (copy verbatim — `ASH-SYNC-01`), `AssessmentService.UpdateGroupAsync` (`request.Name?.Trim()`).
4. ✅ Frontend không cần sửa để **giữ** dữ liệu: `normalizeGroupName` = `value?.trim()`; `normalizeVietnamese` (gộp `\s+`) chỉ dùng làm khoá tra cứu màu/thứ tự nhóm, không dùng để lưu/hiển thị. Snapshot round-trip qua `PUT .../records` giữ nguyên chuỗi.
5. ⬜ Hiển thị newline trên UI (grid/PDF) **ngoài phạm vi** task này: HTML mặc định gộp `\n` thành space khi render; nếu cần xuống dòng thật trong lưới/PDF phải thêm `white-space: pre-wrap` — chờ yêu cầu riêng.
6. ⬜ Lưu ý grouping: nếu cùng một nhóm được gõ khác nhau (có/không newline) giữa các dòng trong Sheet thì sẽ thành 2 nhóm khác nhau ở form bảng đánh giá (so sánh chuỗi chính xác) — rủi ro này vốn đã có với space thừa.

## Phạm vi

- Backend `api/`: `GoogleSheets/AssessmentSyncTextNormalizer.cs` (đổi impl + comment), `tests/AdminPortal.UnitTests/AssessmentSyncTextNormalizerTests.cs` (viết lại), `api/README.md`.
- Không migration/schema/contract/authorization/audit. Không frontend. Không production/IIS/deploy.

## DoD

- ✅ `dotnet build src/AdminPortal.Application/AdminPortal.Application.csproj --no-restore` pass 0/0.
- ✅ `dotnet test tests/AdminPortal.UnitTests --no-restore` pass 102/102 (test khoá hành vi: giữ `\r\n`, giữ tab/space thừa trong chuỗi, chỉ trim hai đầu, rỗng → `null`/`""`).
- ➖ `dotnet build AdminPortal.slnx` chỉ cảnh báo `MSB3026` khi copy DLL vào `AdminPortal.Api/bin` do một tiến trình API dev đang giữ file — không phải lỗi biên dịch.
- ➖ Integration suite: không chạy (luồng `SyncAssessmentsAsync` cần Google live; `FakeGoogleSheetsService` ném `NotImplementedException`).
- ✅ `api/README.md` cập nhật; backend + shared memory ghi rõ đảo quyết định 2026-08-28.
