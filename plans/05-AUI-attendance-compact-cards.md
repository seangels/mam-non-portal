# Kế hoạch UI điểm danh dạng card nhỏ gọn

## 1. Thông tin kế hoạch

- **Epic:** `AUI` — Attendance UI.
- **Thứ tự:** `05`.
- **Trạng thái:** Bản đề xuất đang chờ review; chưa triển khai source.
- **Ngày lập:** 2026-08-12.
- **Phạm vi chính:** Angular 15/DevExtreme tại trang `/#/attendance`.
- **Phụ thuộc:** [`02-ATT-attendance.md`](02-ATT-attendance.md) và [`04-SCH-student-groups-study-schedule.md`](04-SCH-student-groups-study-schedule.md).

Mẫu hình người dùng cung cấp là định hướng trực quan: card thấp, xếp nhiều cột, tên/mã học sinh ở thanh dọc, trạng thái dạng pill có mũi tên, field phụ chỉ xuất hiện khi cần và vùng ghi chú nằm ngay trong card.

Production build, IIS package và deploy không thuộc plan này; chỉ chạy khi người dùng gọi riêng `$gv-portal-production`.

## 2. Mục tiêu

1. Hiển thị khoảng 8–10 học sinh trong một viewport desktop thông thường; nhóm lớn tiếp tục cuộn dọc đến tối đa 100 học sinh.
2. Giảm chiều cao mỗi card và số thao tác chuột so với cụm radio hiện tại.
3. Giữ nguyên đầy đủ nghiệp vụ `Present`, `AbsentFullDay`, `AbsentHalfDay`, `OneToOneHour`, phép/không phép, buổi sáng/chiều, ghi chú và full-roster save.
4. Không làm mất draft khi tìm kiếm/lọc; không phá dirty guard, conflict recovery, read-only, Saved snapshot hoặc historical recovery.
5. Toàn bộ text hiển thị bằng tiếng Việt và vẫn dùng được bằng bàn phím/screen reader.

## 3. Phạm vi và ngoài phạm vi

### 3.1 Trong phạm vi

- Redesign danh sách card điểm danh hằng ngày ở trạng thái `Missing` và `Saved`.
- Status/conditional controls dạng compact select/pill.
- Thanh định danh dọc chứa tên thường gọi và mã học sinh.
- Grid responsive, cuộn dọc, dirty/invalid/read-only/loading states.
- Sửa giới hạn UI ghi chú từ 200 lên đúng contract API 2.000 ký tự.
- Focused unit tests và kiểm tra trực quan ở các viewport mục tiêu.

### 3.2 Ngoài phạm vi

- Không đổi REST endpoint, DTO, database, enum `AttendanceStatus`, migration hoặc authorization.
- Không autosave, batch API mới, virtual-scroll ở đợt đầu hoặc kéo-thả card.
- Không redesign popup `Khôi phục lịch sử`; chỉ chạy regression để bảo đảm luồng này giữ nguyên.
- Không thay đổi filter panel, summary, toolbar, conflict banner và sticky save ngoài các copy/spacing cần thiết để đồng bộ layout.
- Không sửa global theme hoặc `ui/src/styles.scss`; style đặt trong attendance component.

## 4. Hiện trạng cần giữ

- Missing lấy roster và trạng thái gợi ý từ backend theo lịch học: `FullDay → Present`, `OneToOne → OneToOneHour/60 phút`.
- Missing POST và Saved PUT đều gửi toàn bộ roster; `Unmarked/Chưa điểm danh` không phải giá trị hợp lệ của API.
- `AbsentHalfDay` bắt buộc `halfDayPart = Morning|Afternoon` và `isExcused` boolean.
- `AbsentFullDay` bắt buộc `isExcused`; `Present` và `OneToOneHour` không có các field nghỉ.
- `notes` áp dụng cho mọi status, nullable và tối đa 2.000 ký tự.
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
- Desktop dùng `writing-mode: vertical-rl` kết hợp hướng chữ phù hợp mẫu; DOM và accessible name vẫn theo thứ tự `Tên thường gọi, mã học sinh, họ tên đầy đủ`.
- Phần nội dung có một status pill, tối đa một hàng conditional compact và textarea ghi chú.
- Card mục tiêu rộng 220–260 px, cao khoảng 145–175 px tùy trạng thái; card có lỗi được phép cao hơn thay vì che lỗi.
- Full name hiển thị qua tooltip/title và text chỉ dành cho screen reader; trên mobile thanh định danh chuyển thành header ngang để tránh tên dọc quá dài.

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
| UI-only nếu được duyệt | `Chưa điểm danh` | `Chưa chọn trạng thái điểm danh` | Xám |

Màu chỉ hỗ trợ nhận biết; text đầy đủ luôn tồn tại. Contrast phải đạt WCAG AA và focus ring không bị màu pill che khuất.

### 7.2 Field phụ

| Trạng thái | Control dòng hai | Mapping API |
|---|---|---|
| Có mặt | Không hiện | clear `halfDayPart/isExcused/durationMinutes`; giữ notes |
| Nghỉ cả ngày | `Có phép` / `Không phép` | `isExcused=true|false` |
| Nghỉ 1/2 | Một select gộp 4 lựa chọn | `Sáng/Chiều × Có phép/Không phép` map ngược về `halfDayPart + isExcused` |
| 1-1 | Chip nhỏ `60 phút`, không cần select | `durationMinutes=60` |
| Chưa điểm danh | Không hiện | không bao giờ serialize/gửi API |

Select gộp của nghỉ 1/2 chỉ là lớp trình bày; DTO và `toRecord()` vẫn gửi hai field độc lập đúng ATT contract.

### 7.3 Ghi chú

- Textarea hai dòng, tự giãn tối đa một ngưỡng nhỏ rồi cuộn nội bộ.
- `maxlength=2000`, counter `x/2.000` có thể chỉ hiện khi focus hoặc gần giới hạn để giữ card gọn.
- Ghi chú không bị clear khi đổi status, đúng contract hiện tại.
- Lỗi notes được hiển thị ngay trong card và focus đúng textarea/card đầu tiên có lỗi.

## 8. “Chưa điểm danh” và mô hình draft

`Chưa điểm danh` trong hình không tồn tại trong `AttendanceStatus`. Tuyệt đối không gửi chuỗi này lên API và không thêm enum/database chỉ để phục vụ layout.

Phương án khuyến nghị để bám hình mà vẫn giữ contract:

1. Chỉ dùng `Chưa điểm danh` như review state trong UI của phiếu `Missing`.
2. Mỗi draft giữ riêng:

```ts
reviewState: 'Unmarked' | 'Reviewed';
suggestedStatus: AttendanceStatus;
```

3. Backend status gợi ý được giữ trong `suggestedStatus`; Saved luôn là `Reviewed`.
4. Save bị khóa khi còn card `Unmarked`; validation mở filter và focus card đầu tiên chưa chọn.
5. Có action cấp danh sách `Dùng trạng thái gợi ý cho tất cả` để xác nhận nhanh roster lớn. Action này chỉ sửa draft, không autosave.
6. `toRecord()` chỉ nhận draft đã `Reviewed`, do đó full-roster API không thể nhận status không hợp lệ.
7. Dirty/navigation guard tính cả thay đổi `reviewState`, kể cả khi người dùng xác nhận đúng status gợi ý.

Đây là thay đổi workflow so với hiện tại, nơi Missing có thể lưu ngay toàn bộ trạng thái gợi ý dù `dirty=0`; cần user khóa `AUI-DEC-01` trước implementation.

Nếu không duyệt workflow này, card vẫn dùng layout/pill giống hình nhưng không có lựa chọn `Chưa điểm danh`; Missing tiếp tục hiển thị ngay status gợi ý và cho phép lưu lần đầu với `dirty=0`.

## 9. State, validation và save flow

- Giữ API DTO `AttendanceStatus` nguyên vẹn; tạo UI view model riêng, không mở rộng DTO bằng giá trị giả.
- Chuyển status phải clear/set conditional fields đúng rule hiện tại.
- Combined half-day option có helper pure để encode/decode; không rải string parsing trong template.
- Invalid state đặt border/indicator rõ trên card; filter đang ẩn card lỗi phải được reset trước khi focus.
- Missing save gửi full roster + `expectedSnapshotVersion`; Saved save gửi full roster + sheet `expectedVersion` như hiện tại.
- Missing first-save vẫn enabled khi `dirty=0` nếu phương án không dùng `Unmarked`; nếu dùng `Unmarked`, chỉ enabled sau khi toàn roster được review/bulk-apply.
- Save success dùng response làm source of truth, reset baseline/review state và giữ vị trí scroll hợp lý.
- 409 giữ mọi draft/review state, hiển thị CTA tải bản mới nhất; 403 reload context theo behavior hiện tại.
- Read-only Saved dùng cùng card nhưng select/textarea disabled; không hiển thị affordance khiến người dùng nghĩ có thể sửa.

## 10. Filter, summary và trạng thái trang

- Giữ panel filter collapse/expand mặc định mở.
- Search không dấu trên mã, họ tên, nickname như hiện tại.
- Status filter dùng local card state; nếu `Unmarked` được duyệt thì thêm lựa chọn `Chưa điểm danh` nhưng không đưa vào API enum/model chung.
- Summary vẫn tính trên toàn roster, không chỉ card đang lọc.
- Nếu `Unmarked` được duyệt, thêm ô `Chưa điểm danh` hoặc thay copy tổng hợp để tổng các nhóm trạng thái không gây hiểu nhầm.
- `NoScheduledStudents`, loading, error, conflict và empty-filter states giữ copy tiếng Việt hiện tại.
- Sticky action giữ số thay đổi/chưa review, không che hàng card cuối.

## 11. Accessibility và tương tác

- Mỗi card có accessible name gồm họ tên đầy đủ, nickname và mã học sinh; không bắt screen reader đọc chữ theo chiều dọc.
- Status/conditional select có label riêng cho từng học sinh; không dùng placeholder làm label.
- Keyboard tab order: status → field phụ → ghi chú, sau đó sang card tiếp theo.
- Focus ring rõ; invalid card có message text, không chỉ border đỏ.
- Touch target tối thiểu 44 px trên mobile; desktop compact vẫn bảo đảm select có thể thao tác ổn định ở zoom 200%.
- Tooltip không chứa thông tin duy nhất; full name phải có accessible text dù bị rút gọn trực quan.
- Kiểm tra contrast cho năm nhóm màu ở enabled, hover, focus, disabled và read-only.

## 12. File dự kiến thay đổi

Frontend chính:

- `ui/src/app/pages/attendance/attendance.component.html`
- `ui/src/app/pages/attendance/attendance.component.scss`
- `ui/src/app/pages/attendance/attendance.component.ts`
- `ui/src/app/pages/attendance/attendance.component.spec.ts`
- Có thể thêm helper/view-model nhỏ cạnh attendance component nếu combined half-day/review state làm component khó đọc.

Tài liệu/handoff:

- `ui/README.md`
- `.agents/frontend/MEMORY.md`
- `tasks.md`
- `plans/README.md`

Không dự kiến sửa `api/`, migration, DTO dùng chung hoặc global `styles.scss`.

## 13. Mã đợt triển khai

### Planning

- `AUI-P-01`: phân tích mẫu hình và current attendance UI.
- `AUI-P-02`: khóa semantics `Chưa điểm danh`, card fields, breakpoints và acceptance criteria.

### Frontend

- `AUI-FE-00`: khóa local view model/helper và test traceability; xác nhận không đổi API.
- `AUI-FE-01`: dựng compact card/grid/identity rail/status color tokens.
- `AUI-FE-02`: status pill, permission và combined half-day mapping; notes 2.000 ký tự.
- `AUI-FE-03`: `Unmarked`/bulk suggested flow nếu được duyệt; dirty/filter/summary/save integration.
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
- Bốn lựa chọn nghỉ 1/2 encode/decode chính xác Morning/Afternoon + phép/không phép.
- `OneToOneHour` luôn serialize 60 phút.
- Notes nhận đến 2.000 ký tự; 2.001 bị chặn/map lỗi đúng card.
- Filter/search không làm mất draft; summary tính toàn roster.
- Invalid card bị ẩn được reveal và focus.
- Missing first-save và Saved dirty-only save giữ đúng contract theo quyết định `Unmarked`.
- Nếu có `Unmarked`: không serialize được; save bị chặn; bulk suggestion review toàn roster; dirty guard tính review state.
- Saved/read-only không phát mutation; recovery vẫn manual và default Present.

### Visual/responsive

- 1366 px đạt mục tiêu khoảng 5 card/hàng và 8–10 card/viewport khi phần header ở trạng thái bình thường.
- Không tràn chữ với nickname/mã dài; full name vẫn truy cập được.
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
- Không có `Unmarked` trong request hoặc API DTO.
- Full-roster, version/snapshot, dirty guard, Saved và recovery semantics không regression.
- Notes UI khớp giới hạn API 2.000 ký tự.
- ChromeHeadlessCI và Angular development build pass; chỉ chạy production/IIS khi skill được gọi rõ.
- `git diff --check` sạch; README, task log và frontend memory được cập nhật khi implementation hoàn tất.

## 16. Quyết định cần user khóa

| Mã | Quyết định | Đề xuất |
|---|---|---|
| `AUI-DEC-01` | `Chưa điểm danh` có phải trạng thái review UI-only và chặn save? | Có; không thêm API enum, thêm bulk `Dùng trạng thái gợi ý cho tất cả` |
| `AUI-DEC-02` | Missing ban đầu hiển thị toàn bộ `Chưa điểm danh` hay hiển thị status backend gợi ý? | Để bám hình: `Chưa điểm danh`, nhưng giữ gợi ý trong draft và cho bulk apply |
| `AUI-DEC-03` | Nghỉ 1/2 dùng một select gộp buổi + phép? | Có; 4 lựa chọn, map về hai field API hiện hữu |
| `AUI-DEC-04` | Thanh dọc hiển thị thông tin nào? | `nickname · studentCode`; fullName qua tooltip + accessible text |
| `AUI-DEC-05` | Redesign áp dụng cho recovery? | Không trong v1; chỉ main daily list |
| `AUI-DEC-06` | Giới hạn notes trên UI | Sửa từ 200 lên đúng API 2.000 ký tự |
| `AUI-DEC-07` | Mật độ desktop | Fluid grid, mục tiêu 5 card/hàng ở 1366 px; không hard-code khi thiếu chỗ |
| `AUI-DEC-08` | Màu status | Bám nhóm màu trong hình nhưng điều chỉnh token để đạt contrast/accessibility |

Không bắt đầu `AUI-FE-00` cho đến khi `AUI-DEC-01` và `AUI-DEC-02` được xác nhận, vì hai quyết định này thay đổi trực tiếp first-save workflow. Các quyết định còn lại có thể dùng đề xuất mặc định nếu người dùng không điều chỉnh.
