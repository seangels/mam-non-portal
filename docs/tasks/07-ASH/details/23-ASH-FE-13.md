# ASH-FE-13 — Không tự động fill kết quả cuối

## Tóm tắt ngắn

1. ✅ Chỉ tự động fill `PlanGrade`/`PlanNote` từ dữ liệu latest khi tạo/thêm mục đánh giá.
2. ✅ Không tự động fill `FinalGrade` từ `PlanGrade` khi mở màn edit, thêm record mới, serialize dirty state hoặc lưu records.
3. ✅ Không tự động fill `FinalNote`; giá trị rỗng/null phải giữ rỗng cho tới khi người dùng nhập.
4. ✅ UI `Kết quả hiện tại` hiển thị trống khi chưa có `FinalGrade`; không dùng fallback `PlanGrade`.
5. ✅ Không đổi backend, REST contract, Google Sheet/Drive flow, auth/routing/IIS.

## Mục đích

`PlanGrade`/`PlanNote` là dữ liệu kế hoạch, được khởi tạo từ kết quả gần nhất để giáo viên tham khảo và chỉnh kế hoạch. `FinalGrade`/`FinalNote` là kết quả đánh giá thật, nên không được tự động lấy từ kế hoạch. Nếu chưa nhập kết quả, UI và payload lưu phải giữ trống/null.

## Nội dung cần làm

- ✅ Giữ nguyên flow tạo mới: picker gửi `latestGrade`/`latestNote` để backend lưu vào `PlanGrade`/`PlanNote`.
- ✅ Bỏ fallback `FinalGrade = PlanGrade` trong helper khởi tạo records khi load detail.
- ✅ Bỏ fallback `FinalGrade = PlanGrade` trong request full-replace records khi thêm/xóa/lưu.
- ✅ Khi thêm một assessment record mới, set `finalGrade = null`, `finalNote = null`.
- ✅ Trên `dx-select-box` cột `Kết quả hiện tại`, binding value chỉ dùng `finalGrade`, không fallback sang `planGrade`.
- ✅ Highlight khác kế hoạch chỉ áp dụng khi đã có `FinalGrade` và khác `PlanGrade`; trạng thái chưa nhập không bị tô vàng.
- ✅ Cập nhật Jasmine regression test cho rule mới.

## DoD

- ✅ `FinalGrade`/`FinalNote` chưa nhập vẫn trống khi mở/sửa/lưu AssessmentSheet.
- ✅ `PlanGrade`/`PlanNote` vẫn được fill từ latest như hiện tại.
- ✅ Frontend `test:ci` và development build pass.
- ✅ Không chạy production/IIS/deploy.

## Verification 2026-08-28

- ✅ `npm --prefix ui run test:ci` — pass 118/118 ngoài sandbox; trong sandbox fail `EPERM lstat C:\Users\sangn` như baseline cũ.
- ✅ `npm --prefix ui run build -- --configuration development` — pass, hash `75afc3b9148aa5cfc68c`, chỉ warning CommonJS/DevExtreme/html2pdf/canvg quen thuộc.
- ✅ Không chạy backend vì không đổi API/REST contract.
- ✅ Không chạy Google live smoke, production/IIS/deploy.
