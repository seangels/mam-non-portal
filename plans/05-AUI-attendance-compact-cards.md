# Kế hoạch điểm danh dạng card nhỏ gọn

## 1. Thông tin kế hoạch

- **Epic:** `AUI` — Attendance UI.
- **Thứ tự:** `05`.
- **Trạng thái:** `AUI-DEC-01`–`08` đã chốt; sẵn sàng triển khai, chưa thay đổi source.
- **Ngày lập:** 2026-08-12.
- **Phạm vi:** .NET 10 REST API, PostgreSQL 17 và Angular 15/DevExtreme tại trang `/#/attendance`.
- **Phụ thuộc:** [`02-ATT-attendance.md`](02-ATT-attendance.md) và [`04-SCH-student-groups-study-schedule.md`](04-SCH-student-groups-study-schedule.md).

Về thứ tự áp dụng contract, plan `AUI` này mở rộng `ATT`/`SCH` và ghi đè đúng hai điểm: thêm persisted status `Unmarked`, đồng thời bỏ yêu cầu `Morning/Afternoon` đối với các thao tác `AbsentHalfDay` mới. Mọi quy tắc khác của `ATT`/`SCH` vẫn giữ nguyên.

Mẫu hình người dùng cung cấp là định hướng trực quan: card thấp, xếp nhiều cột, tên/mã học sinh ở thanh dọc, trạng thái dạng pill có mũi tên, field phụ chỉ xuất hiện khi cần và vùng ghi chú nằm ngay trong card.

Production build, IIS package và deploy không thuộc plan này; chỉ chạy khi người dùng gọi riêng `$gv-portal-production`.

## 2. Mục tiêu

1. Hiển thị khoảng 8–10 học sinh trong một viewport desktop thông thường; nhóm lớn tiếp tục cuộn dọc đến tối đa 100 học sinh.
2. Giảm chiều cao mỗi card và số thao tác chuột so với cụm radio hiện tại.
3. Bổ sung persisted status `Unmarked` (`Chưa điểm danh`); giữ mặc định theo lịch học và cho phép người dùng chủ động đổi một học sinh về `Chưa điểm danh`.
4. Bỏ yêu cầu sáng/chiều khỏi `AbsentHalfDay`; phép/không phép vẫn là dữ liệu có cấu trúc, chi tiết buổi nghỉ nếu cần được nhập trong ghi chú.
5. Không làm mất draft khi tìm kiếm/lọc; không phá dirty guard, conflict recovery, read-only, Saved snapshot hoặc historical recovery.
6. Toàn bộ text hiển thị bằng tiếng Việt và vẫn dùng được bằng bàn phím/screen reader.

## 3. Phạm vi và ngoài phạm vi

### 3.1 Trong phạm vi

- Redesign danh sách card điểm danh hằng ngày ở trạng thái `Missing` và `Saved`.
- Status/conditional controls dạng compact select/pill.
- Thanh định danh dọc chỉ chứa `nickname · studentCode`.
- Grid responsive, cuộn dọc, dirty/invalid/read-only/loading states.
- Giữ giới hạn nhập ghi chú trên UI là 200 ký tự; API vẫn giữ giới hạn 2.000 ký tự để tương thích.
- Mở rộng `AttendanceStatus` với `Unmarked`, summary với `unmarked` và DB check constraint tương ứng.
- Đổi validation/write semantics của `AbsentHalfDay`: `halfDayPart` không còn bắt buộc cho bản ghi mới; `isExcused` vẫn bắt buộc.
- Migration tương thích dữ liệu cũ, giữ `half_day_part` nullable để không xóa thông tin lịch sử đã lưu.
- Focused unit tests và kiểm tra trực quan ở các viewport mục tiêu.

### 3.2 Ngoài phạm vi

- Không đổi URL REST, full-roster, authorization, snapshot/version hoặc retention semantics.
- Không autosave, batch API mới, virtual-scroll ở đợt đầu hoặc kéo-thả card.
- Không redesign popup `Khôi phục lịch sử`; chỉ chạy regression để bảo đảm luồng này giữ nguyên.
- Không thay đổi filter panel, summary, toolbar, conflict banner và sticky save ngoài các copy/spacing cần thiết để đồng bộ layout.
- Không sửa global theme hoặc `ui/src/styles.scss`; style đặt trong attendance component.

## 4. Hiện trạng cần giữ

- Missing lấy roster và trạng thái mặc định từ backend theo lịch học: `FullDay → Present`, `OneToOne → OneToOneHour/60 phút`; không mặc định `Unmarked`.
- Missing POST và Saved PUT đều gửi toàn bộ roster; sau plan này `Unmarked` là persisted `AttendanceStatus` hợp lệ và chỉ xuất hiện khi người dùng chủ động chọn.
- `AbsentHalfDay` không dùng `halfDayPart` cho bản ghi mới, vẫn bắt buộc `isExcused`; chi tiết sáng/chiều nếu có nằm trong `notes`.
- `AbsentFullDay` bắt buộc `isExcused`; `Present` và `OneToOneHour` không có các field nghỉ.
- `notes` áp dụng cho mọi status; API vẫn nullable/tối đa 2.000 ký tự nhưng UI chỉ cho người dùng nhập mới hoặc sửa tối đa 200 ký tự.
- Saved hiển thị snapshot đã lưu, không suy diễn lại từ schedule hiện tại.
- Search/filter chỉ ẩn card; baseline/draft đầy đủ vẫn nằm trong bộ nhớ và save vẫn gửi full roster.
- 403/409 giữ semantics hiện tại; không âm thầm tải đè draft.

## 5. Wireframe card đề xuất

```text
┌──────────┬────────────────────────────────┐
│          │ [ Có mặt                    ▾ ] │
│  Tên     │ [ field phụ theo trạng thái  ▾ ] │
│  thường  │ Ghi chú                        │
│  · Mã HS │ ┌────────────────────────────┐ │
│  (dọc)   │ │                            │ │
│          │ └────────────────────────────┘ │
└──────────┴────────────────────────────────┘
```

- Thanh định danh rộng khoảng 40–48 px, nền trung tính, border phải rõ nhẹ.
- Desktop dùng `writing-mode: vertical-rl` kết hợp hướng chữ phù hợp mẫu; DOM và accessible name chỉ theo thứ tự `nickname, studentCode`.
- Phần nội dung có một status pill, tối đa một hàng conditional compact và textarea ghi chú.
- Card mục tiêu rộng 220–260 px, cao khoảng 145–175 px tùy trạng thái; card có lỗi được phép cao hơn thay vì che lỗi.
- Không đưa `fullName` vào card, tooltip hoặc accessible name. Trên mobile thanh định danh chuyển thành header ngang để tránh nickname/mã dọc quá dài.

## 6. Grid và responsive

| Viewport/container | Bố cục mục tiêu |
|---|---|
| Desktop rộng ≥ 1280 px | 5 card/hàng khi không gian cho phép |
| Desktop nhỏ 1024–1279 px | 4 card/hàng |
| Tablet 700–1023 px | 2–3 card/hàng |
| Mobile < 700 px | 1 card/hàng; identity nằm ngang; control cao tối thiểu 44 px |

Implementation dùng CSS Grid fluid, ví dụ `repeat(auto-fill, minmax(220px, 1fr))`; không hard-code đúng năm cột nếu sidebar hoặc zoom làm card bị bóp. `attendance-list` tiếp tục có giới hạn chiều cao và cuộn dọc ở desktop; mobile dùng page scroll như hiện tại.

Với roster lớn:

- Giữ `trackBy studentId`.
- Ưu tiên native `<select>` được style bằng wrapper/pill thay vì tạo hàng trăm overlay component nặng.
- Không render lại/filter lại dữ liệu từ API khi đổi status.
- Chưa cần virtual scroll vì giới hạn roster là 100; chỉ thêm sau khi đo thấy vấn đề thực tế.

## 7. Mapping control theo nghiệp vụ

### 7.1 Status pill chính

| API status | Nhãn compact | Accessible label | Màu gợi ý |
|---|---|---|---|
| `Present` | `Có mặt` | `Có mặt` | Xanh lá đậm |
| `AbsentFullDay` | `Nghỉ` | `Nghỉ cả ngày` | Đỏ nhạt/đỏ đậm |
| `AbsentHalfDay` | `Nghỉ 1/2` | `Nghỉ một nửa ngày` | Cam nhạt/cam đậm |
| `OneToOneHour` | `1-1` | `Học một kèm một, 60 phút` | Xanh lá nhạt |
| `Unmarked` | `Chưa điểm danh` | `Chưa điểm danh` | Xám |

Màu chỉ hỗ trợ nhận biết; text đầy đủ luôn tồn tại. Contrast phải đạt WCAG AA và focus ring không bị màu pill che khuất.

### 7.2 Field phụ

| Trạng thái | Control dòng hai | Mapping API |
|---|---|---|
| Có mặt | Không hiện | clear `halfDayPart/isExcused/durationMinutes`; giữ notes |
| Nghỉ cả ngày | `Có phép` / `Không phép` | `isExcused=true|false` |
| Nghỉ 1/2 | `Có phép` / `Không phép` | `halfDayPart=null`, `isExcused=true|false`; sáng/chiều nếu cần ghi trong notes |
| 1-1 | Chip nhỏ `60 phút`, không cần select | `durationMinutes=60` |
| Chưa điểm danh | Không hiện | persisted `status=Unmarked`; mọi conditional field null, notes vẫn được phép |

`AbsentHalfDay` và `AbsentFullDay` có cùng control phép/không phép. Điểm khác nhau chỉ là status; UI không hỏi buổi sáng/chiều.

### 7.3 Ghi chú

- Textarea hai dòng, tự giãn tối đa một ngưỡng nhỏ rồi cuộn nội bộ.
- `maxlength=200`, counter `x/200` có thể chỉ hiện khi focus hoặc gần giới hạn để giữ card gọn.
- Nếu API trả về dữ liệu lịch sử dài hơn 200 ký tự, UI không được tự cắt mất nội dung: giá trị chưa bị người dùng sửa vẫn được round-trip nguyên vẹn trong full-roster PUT; ngay khi người dùng sửa field đó, validation yêu cầu tối đa 200 ký tự.
- Ghi chú không bị clear khi đổi status, đúng contract hiện tại.
- Lỗi notes được hiển thị ngay trong card và focus đúng textarea/card đầu tiên có lỗi.

## 8. Persisted status “Chưa điểm danh”

`AUI-DEC-01` đã chốt `Chưa điểm danh` là trạng thái nghiệp vụ được lưu thật, không phải review state tạm. Wire enum dùng tên `Unmarked` và nhãn UI luôn là `Chưa điểm danh`.

Semantics:

1. Backend Missing vẫn mặc định theo schedule:
   - Student `FullDay` hoặc mode khác `OneToOne` → `Present`.
   - Student `OneToOne` → `OneToOneHour`, 60 phút.
2. Không có học sinh nào tự động thành `Unmarked` khi vừa tải phiếu.
3. Người dùng chủ động chọn `Chưa điểm danh` trên một hoặc nhiều card khi chưa thể kết luận trạng thái thực tế.
4. `Unmarked` được phép trong POST/PUT full roster và được persisted như mọi status khác.
5. Saved sheet đọc lại đúng `Unmarked`; người có quyền sửa có thể đổi nó thành trạng thái khác ở lần PUT sau.
6. `Unmarked` có `halfDayPart=null`, `isExcused=null`, `durationMinutes=null`; notes vẫn nullable, giới hạn UI 200 ký tự và giới hạn API 2.000 ký tự.
7. `Unmarked` không được tính là Có mặt, Vắng hoặc 1-1; summary trả thêm count riêng `unmarked` để tổng các nhóm bằng `rosterTotal`.

Ví dụ Missing của hai học sinh:

```text
Bé An · FullDay   → Có mặt (mặc định)
Bé Vy · OneToOne → 1-1 (mặc định)
```

Nếu giáo viên chưa xác định được Bé An, họ đổi pill của Bé An thành `Chưa điểm danh`; lần lưu đầu sẽ persisted `Unmarked` cho Bé An và `OneToOneHour` cho Bé Vy.

Không cần `reviewState`, `suggestedStatus`, bulk apply hoặc rule chặn save vì còn `Unmarked`. Dirty guard tiếp tục so sánh bản ghi API thực tế như hiện tại.

## 9. Contract, migration, validation và save flow

Contract thay đổi có chủ đích:

- Backend/frontend thêm `AttendanceStatus.Unmarked`.
- `AttendanceSummaryResponse` và `AttendanceSummary` frontend thêm `unmarked: int`.
- `AttendanceRecordRequest.halfDayPart` tạm giữ nullable để tương thích wire/history nhưng được đánh dấu legacy; request mới từ UI luôn gửi null.
- `AttendanceItemResponse.halfDayPart` tạm giữ để đọc dữ liệu lịch sử cũ; UI card mới không yêu cầu hoặc chỉnh field này.
- Validation status-fields mới:
  - `Present`: mọi conditional field null.
  - `AbsentFullDay`: `isExcused` bắt buộc; `halfDayPart/durationMinutes` null.
  - `AbsentHalfDay`: `isExcused` bắt buộc; `durationMinutes` null; `halfDayPart` null cho write mới.
  - `OneToOneHour`: chỉ `durationMinutes=60`.
  - `Unmarked`: mọi conditional field null.
- DB check constraint phải thêm nhánh `Unmarked`. Để giữ record lịch sử, nhánh `AbsentHalfDay` ở DB cho phép `half_day_part` null hoặc giá trị legacy; application command luôn ghi null cho request mới.
- Không drop column `half_day_part` và không rewrite dữ liệu Saved cũ trong migration này. Khi update một sheet có record legacy không đổi status, service phải bảo toàn `halfDayPart` cũ; khi người dùng đổi status của record đó, value được clear.

Save flow:

- Chuyển status phải clear/set conditional fields đúng rule mới và giữ notes.
- Invalid state đặt border/indicator rõ trên card; filter đang ẩn card lỗi phải được reset trước khi focus.
- Missing save gửi full roster + `expectedSnapshotVersion`; Saved save gửi full roster + sheet `expectedVersion` như hiện tại.
- Missing first-save vẫn enabled khi `dirty=0`, vì status ban đầu luôn là default hợp lệ từ backend.
- `Unmarked` là status hợp lệ nên không chặn save; nó chỉ xuất hiện khi người dùng chọn và được tính là dirty nếu khác baseline.
- Save success dùng response làm source of truth, reset baseline và giữ vị trí scroll hợp lý.
- 409 giữ mọi draft, hiển thị CTA tải bản mới nhất; 403 reload context theo behavior hiện tại.
- Read-only Saved dùng cùng card nhưng select/textarea disabled; không hiển thị affordance khiến người dùng nghĩ có thể sửa.

## 10. Filter, summary và trạng thái trang

- Giữ panel filter collapse/expand mặc định mở.
- Search không dấu trên mã, họ tên, nickname như hiện tại.
- Status filter thêm persisted status `Unmarked`, hiển thị nhãn `Chưa điểm danh`.
- Summary vẫn tính trên toàn roster, không chỉ card đang lọc.
- Summary thêm ô/count `Chưa điểm danh`; `rosterTotal = present + absent + oneToOne + unmarked`.
- `NoScheduledStudents`, loading, error, conflict và empty-filter states giữ copy tiếng Việt hiện tại.
- Sticky action giữ số thay đổi chưa lưu, không che hàng card cuối.

## 11. Accessibility và tương tác

- Mỗi card có accessible name chỉ gồm nickname và mã học sinh; không bắt screen reader đọc chữ theo chiều dọc.
- Status/conditional select có label riêng cho từng học sinh; không dùng placeholder làm label.
- Keyboard tab order: status → field phụ → ghi chú, sau đó sang card tiếp theo.
- Focus ring rõ; invalid card có message text, không chỉ border đỏ.
- Touch target tối thiểu 44 px trên mobile; desktop compact vẫn bảo đảm select có thể thao tác ổn định ở zoom 200%.
- Nickname/mã bị rút gọn trực quan phải có accessible text đầy đủ; không bổ sung `fullName` ngoài quyết định `AUI-DEC-04`.
- Kiểm tra contrast cho năm nhóm màu ở enabled, hover, focus, disabled và read-only.

## 12. File dự kiến thay đổi

Backend:

- `api/src/AdminPortal.Domain/Enums/AttendanceStatus.cs`
- `api/src/AdminPortal.Application/Attendance/AttendanceModels.cs`
- `api/src/AdminPortal.Application/Attendance/AttendanceRules.cs`
- `api/src/AdminPortal.Application/Attendance/AttendanceService.cs`
- `api/src/AdminPortal.Infrastructure/Persistence/Configurations/AttendanceRecordConfiguration.cs`
- EF migration + model snapshot được sinh bằng CLI.
- Unit/integration tests attendance và migration upgrade.

Frontend:

- `ui/src/app/pages/attendance/attendance.component.html`
- `ui/src/app/pages/attendance/attendance.component.scss`
- `ui/src/app/pages/attendance/attendance.component.ts`
- `ui/src/app/pages/attendance/attendance.component.spec.ts`
- Có thể thêm helper/view-model nhỏ cạnh attendance component nếu status mapping làm component khó đọc.

Tài liệu/handoff:

- `ui/README.md`
- `.agents/frontend/MEMORY.md`
- `tasks.md`
- `plans/README.md`

Không dự kiến đổi endpoint URL, auth, Student schedule contract hoặc global `styles.scss`.

## 13. Mã đợt triển khai

### Planning

- `AUI-P-01`: phân tích mẫu hình và current attendance UI.
- `AUI-P-02`: khóa semantics `Chưa điểm danh`, card fields, breakpoints và acceptance criteria.

### Backend

- `AUI-BE-00`: khóa enum/DTO/summary/validation/OpenAPI contract và legacy `halfDayPart` compatibility.
- `AUI-BE-01`: domain/config/EF migration/check constraint, fresh + upgrade proof.
- `AUI-BE-02`: service defaults, persisted Unmarked, half-day write/preserve semantics và audit.
- `AUI-BE-03`: unit/integration/OpenAPI/README/requests/memory/final gates.

### Frontend

- `AUI-FE-00`: align `Unmarked`, summary và nullable legacy `halfDayPart` contract; khóa test traceability.
- `AUI-FE-01`: dựng compact card/grid/identity rail/status color tokens.
- `AUI-FE-02`: status pill, permission mapping không dùng half-day part; notes tối đa 200 ký tự trên UI và bảo toàn giá trị API legacy dài hơn khi chưa sửa.
- `AUI-FE-03`: persisted `Unmarked`, defaults/dirty/filter/summary/save integration.
- `AUI-FE-04`: read-only, loading/error/conflict/empty và recovery regression.
- `AUI-FE-05`: responsive, accessibility, focused tests, development build, docs/memory.

### QA

- `AUI-QA-01`: visual review desktop 1366×768, 1280×720, tablet 1024×768 và mobile 390×844.
- `AUI-QA-02`: keyboard/screen-reader labels, zoom 200%, contrast và touch targets.
- `AUI-QA-03`: Missing/Saved/full-roster/conditional validation/dirty/conflict/recovery regression.

## 14. Test matrix

### Unit/component

- Mọi API status map đúng nhãn/màu; không lộ enum tiếng Anh.
- Status change clear/set đúng conditional fields và giữ notes.
- `AbsentHalfDay` và `AbsentFullDay` đều bắt buộc phép/không phép; UI không hiển thị Morning/Afternoon và luôn gửi `halfDayPart=null` cho write mới.
- Missing mặc định `Present` cho Student không phải OneToOne và `OneToOneHour` cho Student OneToOne; không tự mặc định `Unmarked`.
- Người dùng chọn `Unmarked` được POST/PUT round-trip; mọi conditional field null và notes được giữ.
- `OneToOneHour` luôn serialize 60 phút.
- Notes người dùng nhập/sửa nhận đến 200 ký tự; 201 bị chặn đúng card. Giá trị lịch sử từ API dài hơn 200 không bị cắt và được giữ nguyên nếu field chưa sửa.
- Filter/search không làm mất draft; summary tính toàn roster.
- Invalid card bị ẩn được reveal và focus.
- Missing first-save `dirty=0` vẫn gửi full roster default; Saved chỉ enable save khi dirty.
- Summary/filter/count `Unmarked` chính xác và full-roster request vẫn đủ mọi Student.
- Saved/read-only không phát mutation; recovery vẫn manual và default Present.

### Backend/integration

- JSON enum nhận/trả `Unmarked`; OpenAPI và ProblemDetails không lộ giá trị không hợp lệ.
- DB check constraint chấp nhận `Unmarked` chỉ khi conditional fields null.
- Bản ghi AbsentHalfDay mới có `half_day_part=null`, `is_excused` bắt buộc.
- Upgrade giữ nguyên record legacy có Morning/Afternoon; migration không drop/rewrite dữ liệu lịch sử.
- Full PUT không đổi status của record AbsentHalfDay legacy phải bảo toàn `halfDayPart`; đổi status thì clear.
- Summary GET/POST/PUT/recovery trả `unmarked` đúng và tổng category bằng roster total.
- Full attendance regression, fresh migration, upgrade migration và EF pending-model pass.

### Visual/responsive

- 1366 px đạt mục tiêu khoảng 5 card/hàng và 8–10 card/viewport khi phần header ở trạng thái bình thường.
- Không tràn chữ với nickname/mã dài; accessible name đọc đủ đúng hai thông tin này.
- Conditional row và validation có thể làm card cao hơn mà không đè card kế bên.
- Mobile dùng identity ngang, một card/hàng, action sticky không che textarea cuối.
- Roster 1, 10 và 100 học sinh đều cuộn/trackBy ổn định.

### Regression

- `NoScheduledStudents` không hiện save CTA.
- Backend-suggested FullDay/OneToOne defaults không bị UI tự suy diễn sai.
- Saved snapshot không re-filter current schedule.
- 403/409, reload, date/group change và beforeunload giữ behavior hiện tại.
- Historical recovery popup không bị CSS card mới ảnh hưởng.

## 15. Definition of Done

- Card chính bám cấu trúc hình tham chiếu và đạt mật độ mục tiêu mà không mất trường nghiệp vụ.
- Tất cả text UI tiếng Việt; màu không phải tín hiệu duy nhất.
- `Unmarked` là persisted API/DB status, không phải UI-only state; Missing default vẫn chỉ `Present` hoặc `OneToOneHour` theo schedule.
- `AbsentHalfDay` mới không dùng `halfDayPart`; dữ liệu legacy được bảo toàn và notes là nơi người dùng ghi chi tiết buổi nếu cần.
- Full-roster, version/snapshot, dirty guard, Saved và recovery semantics không regression.
- Notes UI giới hạn 200 ký tự theo quyết định sản phẩm; API tiếp tục giữ giới hạn 2.000 ký tự để tương thích và UI không cắt dữ liệu cũ.
- ChromeHeadlessCI và Angular development build pass; chỉ chạy production/IIS khi skill được gọi rõ.
- `git diff --check` sạch; README, task log và frontend memory được cập nhật khi implementation hoàn tất.

## 16. Quyết định cần user khóa

| Mã | Quyết định | Đề xuất |
|---|---|---|
| `AUI-DEC-01` | Semantics `Chưa điểm danh` | **Đã chốt:** persisted `AttendanceStatus.Unmarked`; được phép POST/PUT/Saved, không phải review state tạm |
| `AUI-DEC-02` | Default Missing | **Đã chốt:** Student không phải OneToOne mặc định `Present`; OneToOne mặc định `OneToOneHour`; `Unmarked` chỉ do người dùng chủ động chọn |
| `AUI-DEC-03` | Có dùng `halfDayPart` không? | **Đã chốt:** không dùng cho write/UI mới; `AbsentHalfDay` chỉ chọn phép/không phép, chi tiết ghi notes; giữ dữ liệu DB legacy an toàn |
| `AUI-DEC-04` | Thanh dọc hiển thị thông tin nào? | **Đã chốt:** chỉ `nickname · studentCode`; không thêm fullName vào card/tooltip/accessibility text |
| `AUI-DEC-05` | Redesign áp dụng cho recovery? | **Đã chốt:** không trong v1; chỉ main daily list |
| `AUI-DEC-06` | Giới hạn notes trên UI | **Đã chốt:** tối đa 200 ký tự trên UI; API giữ 2.000 để tương thích, không cắt dữ liệu cũ chưa sửa |
| `AUI-DEC-07` | Mật độ desktop | **Đã chốt:** fluid grid, mục tiêu 5 card/hàng ở 1366 px; không hard-code khi thiếu chỗ |
| `AUI-DEC-08` | Màu status | **Đã chốt:** bám nhóm màu trong hình nhưng điều chỉnh design token để đạt contrast/accessibility |

`AUI-DEC-01`–`08` đã được người dùng xác nhận ngày 2026-08-12. Do plan có contract/schema delta, implementation phải bắt đầu bằng `AUI-BE-00` và `AUI-FE-00` khóa cùng wire contract; không được triển khai frontend riêng rồi gửi enum giả.
