# Chuyển DevExtreme 19.2.5 và Angular 12.2.17 — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Chuyển UI từ Angular 15.2/DevExtreme 23.2.3 sang Angular 12.2.17/DevExtreme 19.2.5 mà vẫn giữ nguyên chức năng, bố cục, API, bảo mật và quy trình triển khai hiện tại.

**Architecture:** Thực hiện migration tại chỗ theo các checkpoint độc lập: khóa toolchain, thay dependency và workspace Angular, chuyển API DevExtreme, sinh lại theme, rồi kiểm thử hồi quy. Mỗi checkpoint tạo một commit riêng để có thể rollback mà không trộn lỗi Angular, DevExtreme và giao diện.

**Tech Stack:** Node.js 14.21.3, npm 8.19.4, Angular 12.2.17, TypeScript 4.3.5, RxJS 7.4.0, DevExtreme/DevExtreme Angular/ThemeBuilder 19.2.5, Karma/Jasmine.

## Global Constraints

- Không thay đổi bất kỳ file nào trong `api/`, database, migration hoặc REST contract.
- Không đổi hash routing, auth flow, refresh/CSRF behavior hoặc IIS environment.
- Không loại bỏ, ẩn hoặc vô hiệu hóa tính năng chỉ để build/test thành công.
- Không dùng `npm install --force`, `--legacy-peer-deps`, `skipLibCheck`, tắt `strictTemplates` hoặc hạ TypeScript strictness để che lỗi tương thích.
- Chỉ được dùng `--ignore-scripts` đúng một lần khi tạo lock ở `DX19-FE-00`, nhằm tách dependency resolution khỏi theme generation; final `npm ci` bắt buộc chạy đầy đủ lifecycle scripts.
- Mỗi checkpoint phải qua development build và test liên quan trước khi commit.
- Phải smoke-test thủ công toàn bộ màn hình dùng DevExtreme trước khi hoàn tất epic.
- Chấp nhận khác biệt nhỏ về màu sắc, font, spacing và kích thước widget do theme 19.2.5; phải giữ bố cục và luồng thao tác.
- Không chạy production build, IIS build/package hoặc deploy nếu người dùng chưa gọi `$gv-portal-production`.
- Người dùng chịu trách nhiệm chạy NVM, chuyển Node và xóa `ui/node_modules`, `ui/package-lock.json`; agent không tự thực hiện các thao tác này.

---

## 1. Mã phát triển và phụ thuộc

- Epic: `DX19`.
- File này phụ thuộc toàn bộ epic đã triển khai: `BASE`, `ATT`, `TCH`, `SCH`, `AUI`.
- Không thay đổi product requirements; đây là migration công nghệ của riêng frontend.
- Trạng thái ban đầu: `Chờ triển khai`.

## 2. Quyết định đã khóa

| Mã | Quyết định |
|---|---|
| `DX19-DEC-01` | Pin chính xác `devextreme` và `devextreme-angular` ở `19.2.5`. |
| `DX19-DEC-02` | Pin Angular framework/CLI/build tooling ở `12.2.17`; CDK ở `12.2.13`. |
| `DX19-DEC-03` | Pin Node.js `14.21.3` và npm `8.19.4`; không hỗ trợ Node 16/20 cho workspace UI này. |
| `DX19-DEC-04` | Migration tại chỗ, theo checkpoint; không scaffold ứng dụng mới rồi copy source. |
| `DX19-DEC-05` | Giữ chức năng và bố cục; chấp nhận khác biệt hình thức nhỏ của theme 19.2.5. |
| `DX19-DEC-06` | Giữ RxJS 7 để bảo toàn `firstValueFrom` và error factory hiện tại, nhưng pin về `7.4.0`, nằm trong range Angular 12.2 hỗ trợ. |
| `DX19-DEC-07` | Giữ NgModule, strict mode, hash routing, Karma/Jasmine và toàn bộ API DTO/service hiện tại. |
| `DX19-DEC-08` | Không chạy production/IIS trong epic; chỉ development build, unit test và smoke-test dev. |
| `DX19-DEC-09` | Popup 19.2 dùng `closeOnOutsideClick`; không giữ tên option mới `hideOnOutsideClick`. |
| `DX19-DEC-10` | Các namespace type mới như `DxDrawerTypes`, `DxTreeViewTypes`, `DxToolbarTypes` được thay bằng type tương thích nhỏ, không đổi behavior. |
| `DX19-DEC-11` | Theme được sinh lại từ metadata bằng tool 19.2.5; không dùng CSS generated bởi 23.2.3. |
| `DX19-DEC-12` | Nếu một tính năng không thể tái tạo trên 19.2.5, dừng checkpoint và báo blocker; không xóa tính năng hoặc âm thầm đổi UX. |

## 3. Ma trận phiên bản đích

### 3.1 Runtime và framework

| Package/tool | Phiên bản đích | Ghi chú |
|---|---:|---|
| Node.js | `14.21.3` | Dùng qua NVM for Windows. |
| npm | `8.19.4` | Tạo `package-lock.json` lockfile v2. |
| `@angular/animations` | `12.2.17` | Pin exact, không dùng caret. |
| `@angular/common` | `12.2.17` | Pin exact. |
| `@angular/compiler` | `12.2.17` | Pin exact. |
| `@angular/core` | `12.2.17` | Pin exact. |
| `@angular/forms` | `12.2.17` | Pin exact. |
| `@angular/platform-browser` | `12.2.17` | Pin exact. |
| `@angular/platform-browser-dynamic` | `12.2.17` | Pin exact. |
| `@angular/router` | `12.2.17` | Giữ hash routing hiện tại. |
| `@angular/cdk` | `12.2.13` | Đồng bộ major 12; chỉ dùng `BreakpointObserver`. |
| `rxjs` | `7.4.0` | Angular 12.2 cho phép `^7.0.0`. |
| `tslib` | `2.3.1` | Thỏa Angular 12 và RxJS 7. |
| `zone.js` | `0.11.4` | Peer range chính xác của Angular 12.2.17. |

### 3.2 DevExtreme

| Package | Phiên bản đích | Ghi chú |
|---|---:|---|
| `devextreme` | `19.2.5` | Pin exact. |
| `devextreme-angular` | `19.2.5` | Pin exact; peer `devextreme ~19.2.5`. |
| `devextreme-themebuilder` | `19.2.5` | Sinh lại CSS/SCSS theme. |
| `devextreme-cli` | `1.6.4` | Pin thay cho `latest`; chạy được trên Node 14. |
| `devextreme-schematics` | `1.6.0` | Bản tương thích Angular 12; override dependency `latest` của wrapper 19.2.5. |

`package.json` phải có override để npm không kéo `devextreme-schematics@latest` trong tương lai:

```json
{
  "overrides": {
    "devextreme-angular@19.2.5": {
      "devextreme-schematics": "1.6.0"
    }
  }
}
```

### 3.3 Build và test

| Package | Phiên bản đích |
|---|---:|
| `@angular-devkit/build-angular` | `12.2.17` |
| `@angular/cli` | `12.2.17` |
| `@angular/compiler-cli` | `12.2.17` |
| `typescript` | `4.3.5` |
| `@types/jasmine` | `3.8.2` |
| `jasmine-core` | `3.8.0` |
| `karma` | `6.3.20` |
| `karma-chrome-launcher` | `3.1.1` |
| `karma-coverage` | `2.0.3` |
| `karma-jasmine` | `4.0.2` |
| `karma-jasmine-html-reporter` | `1.7.0` |

## 4. Các khoảng cách tương thích đã biết

1. Angular 12 dùng `src/polyfills.ts` và test bootstrap `src/test.ts`; cấu hình array polyfills của Angular 15 phải chuyển về file entrypoint.
2. TypeScript phải hạ từ 4.9 xuống 4.3.5; `target/module` ES2022 phải hạ về cấu hình Angular 12 hỗ trợ.
3. DevExtreme 19.2.5 không export các namespace event type mới đang được source sử dụng: `DxDrawerTypes`, `DxTreeViewTypes`, `DxToolbarTypes`.
4. Popup 19.2.5 dùng `closeOnOutsideClick`, trong khi source hiện có nhiều binding `hideOnOutsideClick`.
5. Theme generated hiện tại được tạo bởi ThemeBuilder 23.2.3 và không được tái sử dụng sau khi đổi runtime 19.2.5.
6. `devextreme-angular@19.2.5` khai báo `devextreme-schematics: latest`; nếu không override, một lần cài mới có thể kéo tooling không tương thích.
7. Giao diện sử dụng rộng các widget DataGrid, CustomStore, Drawer, TreeView, Toolbar, Popup, Form, List, Tabs, SelectBox, DateBox, TextBox và dialog/notify; compile pass chưa đủ chứng minh runtime đúng.
8. CSS custom hiện dựa trên class của DevExtreme 23.2.3; phải kiểm tra lại selectors sau khi sinh theme 19.2.5, nhưng chỉ sửa selector thực sự hỏng bố cục.

## 5. Chiến lược checkpoint và rollback

| Checkpoint | Nội dung | Commit đề xuất | Rollback độc lập |
|---|---|---|---|
| `DX19-FE-00` | Toolchain guard + package manifest + lock mới | `[DX19-FE-00] Pin Angular 12 and DevExtreme 19 toolchain` | Có |
| `DX19-FE-01` | Angular 12 workspace/polyfills/test bootstrap | `[DX19-FE-01] Adapt workspace to Angular 12` | Có |
| `DX19-FE-02` | DevExtreme types và widget option compatibility | `[DX19-FE-02] Adapt UI to DevExtreme 19 APIs` | Có |
| `DX19-FE-03` | Theme 19.2.5 và layout CSS | `[DX19-FE-03] Rebuild DevExtreme 19 themes` | Có |
| `DX19-QA-01` | Unit/integration UI regression | `[DX19-QA-01] Verify Angular 12 UI regressions` | Có |
| `DX19-QA-02` | Smoke-test toàn portal + docs/memory/tasks | `[DX19-QA-02] Complete DevExtreme 19 migration` | Có |

Không squash các checkpoint trước khi toàn bộ migration được duyệt. Nếu checkpoint thất bại, revert đúng commit checkpoint; không reset worktree hoặc xóa thay đổi ngoài phạm vi.

---

### Task 1: Khóa toolchain và dependency graph (`DX19-FE-00`)

**Files:**
- Create: `ui/.nvmrc`
- Create: `ui/.npmrc`
- Create: `ui/scripts/check-toolchain.cjs`
- Modify: `ui/package.json`
- Create: `ui/package-lock.json` bằng npm 8.19.4 sau khi người dùng đã xóa lock cũ
- Test: `ui/package.json`, npm dependency tree

**Interfaces:**
- Consumes: Node `14.21.3`, npm `8.19.4` do người dùng đã chuyển bằng NVM.
- Produces: dependency graph tái lập; tất cả task sau chỉ được chạy khi toolchain guard thành công.

- [ ] **Step 1: Xác minh precondition do người dùng thực hiện**

Run from workspace root:

```powershell
node --version
npm --version
Test-Path ui/node_modules
Test-Path ui/package-lock.json
git -c safe.directory=C:/my-works/MamNon/apps/api-portal status --short
```

Expected:

```text
v14.21.3
8.19.4
False
False
```

Nếu Node/npm hoặc hai path không đúng, dừng task; agent không tự xóa hoặc tự chuyển version.

- [ ] **Step 2: Viết toolchain guard trước khi cài package**

`ui/.nvmrc`:

```text
14.21.3
```

`ui/.npmrc`:

```text
engine-strict=true
```

`ui/scripts/check-toolchain.cjs` phải kiểm tra chính xác:

```javascript
const expectedNode = '14.21.3';
const expectedNpm = '8.19.4';
const actualNode = process.versions.node;
const actualNpm = process.env.npm_config_user_agent?.match(/npm\/([^ ]+)/)?.[1] ?? '';

if (actualNode !== expectedNode || actualNpm !== expectedNpm) {
  console.error(`UI yêu cầu Node ${expectedNode} và npm ${expectedNpm}; hiện tại là Node ${actualNode}, npm ${actualNpm || 'không xác định'}.`);
  process.exit(1);
}
```

- [ ] **Step 3: Xác minh guard thất bại khi version bị giả lập sai**

Run:

```powershell
$env:npm_config_user_agent='npm/0.0.0 node/v14.21.3 win32 x64'
node ui/scripts/check-toolchain.cjs
Remove-Item Env:npm_config_user_agent
```

Expected: exit code `1`, thông báo tiếng Việt nêu đúng version yêu cầu.

- [ ] **Step 4: Thay toàn bộ version trong `ui/package.json`**

Yêu cầu:

- Dùng đúng ma trận mục 3, không caret/tilde cho framework, DevExtreme, runtime và test tooling.
- Thêm `engines.node = "14.21.3"`, `engines.npm = "8.19.4"`.
- Thêm `packageManager = "npm@8.19.4"`.
- Thêm `preinstall = "node ./scripts/check-toolchain.cjs"`.
- Giữ nguyên `prestart`, `start`, `setup:https`, `watch`, `test`, `test:ci`, `build-themes`, `postinstall`.
- Thay `devextreme-cli: latest` bằng `1.6.4`.
- Thêm override ở mục 3.2.

- [ ] **Step 5: Resolve dependency và tạo lock mới, chưa chạy theme lifecycle**

Run từ thư mục `ui/` (npm 8.19.4 trên Windows không áp dụng `--prefix` cho Arborist của lệnh `install`, dù đã đọc đúng `ui/.npmrc`):

```powershell
Push-Location ui
npm install --ignore-scripts
Pop-Location
```

Expected: thành công không dùng `--force`/`--legacy-peer-deps`; `ui/package-lock.json` có `lockfileVersion: 2`. Đây là lần duy nhất được bỏ lifecycle scripts; `preinstall` đã được kiểm tra trực tiếp ở Step 3 và `postinstall` sẽ được chứng minh bằng clean `npm ci` tại Task 4.

- [ ] **Step 6: Xác minh dependency tree**

Run:

```powershell
npm --prefix ui ls @angular/core @angular/cli @angular/cdk typescript rxjs zone.js devextreme devextreme-angular devextreme-themebuilder devextreme-cli devextreme-schematics
```

Expected: root package đúng ma trận; không có `invalid`, `extraneous`, Angular 13+ hoặc DevExtreme 20+.

- [ ] **Step 7: Xác minh ThemeBuilder/CLI binary đã resolve đúng, chưa sinh theme**

Run:

```powershell
npm --prefix ui exec devextreme -- --version
npm --prefix ui ls devextreme-cli devextreme-themebuilder
```

Expected: CLI resolve từ local `node_modules`; tree hiển thị `devextreme-cli@1.6.4` và `devextreme-themebuilder@19.2.5`. Theme chỉ được sinh tại Task 4 để checkpoint dependency không trộn generated CSS.

- [ ] **Step 8: Commit checkpoint**

```powershell
git add ui/.nvmrc ui/.npmrc ui/scripts/check-toolchain.cjs ui/package.json ui/package-lock.json
git commit -m "[DX19-FE-00] Pin Angular 12 and DevExtreme 19 toolchain"
```

---

### Task 2: Chuyển workspace sang Angular 12 (`DX19-FE-01`)

**Files:**
- Create: `ui/src/polyfills.ts`
- Create: `ui/src/test.ts`
- Modify: `ui/angular.json`
- Modify: `ui/tsconfig.json`
- Modify: `ui/tsconfig.app.json`
- Modify: `ui/tsconfig.spec.json`
- Test: Angular compiler, Karma bootstrap

**Interfaces:**
- Consumes: dependency graph từ Task 1.
- Produces: Angular 12 workspace có thể compile/test trước khi xử lý lỗi API DevExtreme còn lại.

- [ ] **Step 1: Tạo polyfill entrypoint Angular 12**

`ui/src/polyfills.ts`:

```typescript
import 'zone.js/dist/zone';
```

- [ ] **Step 2: Tạo Karma test bootstrap Angular 12**

`ui/src/test.ts`:

```typescript
import 'zone.js/dist/zone-testing';
import { getTestBed } from '@angular/core/testing';
import { BrowserDynamicTestingModule, platformBrowserDynamicTesting } from '@angular/platform-browser-dynamic/testing';

declare const require: {
  context(path: string, deep?: boolean, filter?: RegExp): {
    keys(): string[];
    <T>(id: string): T;
  };
};

getTestBed().initTestEnvironment(
  BrowserDynamicTestingModule,
  platformBrowserDynamicTesting(),
  { teardown: { destroyAfterEach: true } }
);

const context = require.context('./', true, /\.spec\.ts$/);
context.keys().forEach(context);
```

- [ ] **Step 3: Chuyển `angular.json` về schema Angular 12**

Yêu cầu:

- Build `polyfills` đổi từ array `['zone.js']` thành `src/polyfills.ts`.
- Test thêm `main: src/test.ts`, `polyfills: src/polyfills.ts`.
- Giữ nguyên output path, assets, styles, HTTPS serve, development/production/IIS environment replacement và budgets.
- Không đổi `HashLocationStrategy`/router config.
- Không chạy production/IIS build.

- [ ] **Step 4: Hạ TypeScript emit target phù hợp Angular 12**

Trong `ui/tsconfig.json`:

```json
{
  "compilerOptions": {
    "target": "ES2017",
    "module": "ES2020",
    "lib": ["ES2020", "dom"]
  }
}
```

Giữ `strict`, `noImplicitOverride`, `noPropertyAccessFromIndexSignature`, `noImplicitReturns`, `strictTemplates` và `useDefineForClassFields: false` nếu compiler 4.3.5 chấp nhận. Chỉ bỏ option khi compiler xác nhận option đó chưa tồn tại; phải ghi lý do trong `tasks.md`, không tắt strictness.

- [ ] **Step 5: Bổ sung entrypoint vào tsconfig**

- `tsconfig.app.json.files`: `src/main.ts`, `src/polyfills.ts`.
- `tsconfig.spec.json.files`: `src/test.ts`, `src/polyfills.ts`.
- Giữ `src/**/*.spec.ts`, `src/**/*.d.ts` trong test include.

- [ ] **Step 6: Chạy Angular compile để thu lỗi tương thích thật**

Run:

```powershell
npm --prefix ui run build -- --configuration development
```

Expected: mọi lỗi còn lại phải được phân loại vào một trong ba nhóm: TypeScript 4.3, Angular template schema, DevExtreme 19 API. Không sửa bằng `any` hàng loạt hoặc nới compiler.

- [ ] **Step 7: Chạy test bootstrap**

Run:

```powershell
npm --prefix ui run test:ci
```

Expected: Karma mở ChromeHeadlessCI. Failure do compile/API widget được chuyển sang Task 3; không xóa spec.

- [ ] **Step 8: Commit checkpoint khi Angular workspace đã hợp lệ**

```powershell
git add ui/angular.json ui/tsconfig.json ui/tsconfig.app.json ui/tsconfig.spec.json ui/src/polyfills.ts ui/src/test.ts
git commit -m "[DX19-FE-01] Adapt workspace to Angular 12"
```

---

### Task 3: Chuyển API/type DevExtreme 23 sang 19 (`DX19-FE-02`)

**Files:**
- Create: `ui/src/app/core/models/devextreme-legacy.types.ts`
- Modify: `ui/src/app/layouts/side-nav-inner-toolbar/side-nav-inner-toolbar.component.ts`
- Modify: `ui/src/app/layouts/side-nav-outer-toolbar/side-nav-outer-toolbar.component.ts`
- Modify: `ui/src/app/shared/components/side-navigation-menu/side-navigation-menu.component.ts`
- Modify: mọi template có `hideOnOutsideClick` trong `ui/src/app/**/*.html`
- Modify only if compiler/runtime proves required: DevExtreme pages/components under `ui/src/app/`
- Test: focused component/service specs and development compile

**Interfaces:**
- Consumes: Angular 12 compiler error inventory từ Task 2.
- Produces: source compile với public API DevExtreme 19.2.5, không thay đổi UI behavior.

- [ ] **Step 1: Viết compatibility type nhỏ thay namespace mới**

`devextreme-legacy.types.ts` chỉ định nghĩa phần source thực sự dùng:

```typescript
export type DrawerOpenedStateMode = 'overlap' | 'shrink' | 'push';
export type DrawerRevealMode = 'slide' | 'expand';

export interface LegacyPointerEvent {
  preventDefault?(): void;
  stopPropagation?(): void;
}

export interface TreeViewItemClickEvent<TItem = unknown> {
  itemData?: TItem;
  node?: { selected?: boolean };
  event?: LegacyPointerEvent;
}

export interface ToolbarItemClickEvent {
  event?: LegacyPointerEvent;
}
```

Không tạo một file `any` tổng quát. Nếu compiler cho phép import type chính xác từ `devextreme/ui/*` 19.2.5 thì ưu tiên type vendor và thu nhỏ file này.

- [ ] **Step 2: Thay namespace import không tồn tại**

- Bỏ `DxDrawerTypes`, `DxTreeViewTypes`, `DxToolbarTypes` từ `devextreme-angular/ui/*`.
- Dùng type ở Step 1 cho mode và event.
- Giữ nguyên string value `shrink`, `expand` và toàn bộ xử lý navigation/pointer event.

- [ ] **Step 3: Chuyển Popup option**

Thay tất cả:

```html
[hideOnOutsideClick]="..."
```

bằng:

```html
[closeOnOutsideClick]="..."
```

Chuẩn hóa event popup thành `(onShown)`, `(onHiding)` đúng casing wrapper 19.2.5. Giữ điều kiện chặn đóng khi form đang lưu hoặc có dirty draft.

- [ ] **Step 4: Compile-driven audit toàn bộ widget**

Run:

```powershell
npm --prefix ui run build -- --configuration development
```

Với mỗi lỗi template/type:

1. Đối chiếu API documentation 19.2.
2. Dùng option/event tương đương cũ.
3. Thêm hoặc cập nhật spec hành vi trước khi sửa nếu thay đổi có thể ảnh hưởng luồng người dùng.
4. Không xóa binding chỉ để compiler im lặng.

- [ ] **Step 5: Chạy focused specs theo vùng bị sửa**

Run ít nhất:

```powershell
npm --prefix ui run test:ci -- --include="src/app/layouts/**/*.spec.ts" --include="src/app/shared/**/*.spec.ts"
npm --prefix ui run test:ci -- --include="src/app/pages/users/**/*.spec.ts" --include="src/app/pages/teachers/**/*.spec.ts"
npm --prefix ui run test:ci -- --include="src/app/pages/students/**/*.spec.ts" --include="src/app/pages/student-groups/**/*.spec.ts"
npm --prefix ui run test:ci -- --include="src/app/pages/attendance/**/*.spec.ts"
```

Nếu Angular 12 CLI không hỗ trợ nhiều `--include` như trên, chạy từng glob riêng; không bỏ nhóm test.

- [ ] **Step 6: Xác minh không còn API mới đã biết**

Run:

```powershell
rg -n "DxDrawerTypes|DxTreeViewTypes|DxToolbarTypes|hideOnOutsideClick" ui/src
```

Expected: không có kết quả.

- [ ] **Step 7: Commit checkpoint**

```powershell
git add ui/src/app
git commit -m "[DX19-FE-02] Adapt UI to DevExtreme 19 APIs"
```

---

### Task 4: Sinh lại theme và giữ bố cục (`DX19-FE-03`)

**Files:**
- Modify if format conversion required: `ui/src/themes/metadata.base.json`
- Modify if format conversion required: `ui/src/themes/metadata.additional.json`
- Regenerate: `ui/src/themes/generated/theme.base.css`
- Regenerate: `ui/src/themes/generated/theme.additional.css`
- Regenerate: `ui/src/themes/generated/variables.base.scss`
- Regenerate: `ui/src/themes/generated/variables.additional.scss`
- Modify only for proven regressions: component SCSS and `ui/src/dx-styles.scss`
- Do not modify for this task: `ui/src/styles.scss` unless a global DevExtreme selector is demonstrably required

**Interfaces:**
- Consumes: working DevExtreme 19 runtime from Task 3.
- Produces: theme assets generated exclusively by 19.2.5 with current light/dark intent.

- [ ] **Step 1: Giữ nguyên theme intent**

- Light/base: `material.orange.light`.
- Dark/additional: `material.orange.dark`.
- Additional swatch vẫn chỉ phục vụ TreeView/navigation như hiện tại.
- Không copy CSS từ theme 23.2.3.

- [ ] **Step 2: Sinh lại assets**

Run:

```powershell
npm --prefix ui run build-themes
```

Expected: bốn file generated được tạo bởi package 19.2.5, không có exception.

- [ ] **Step 3: Development build**

Run:

```powershell
npm --prefix ui run build -- --configuration development
```

Expected: build pass; Angular vẫn load `dx.common.css`, base theme và additional theme theo đúng thứ tự.

- [ ] **Step 4: Chứng minh clean install chạy đầy đủ lifecycle**

Run từ thư mục `ui/` vì giới hạn `--prefix` của npm 8.19.4 trên Windows cũng áp dụng cho Arborist của lệnh `ci`:

```powershell
Push-Location ui
npm ci
Pop-Location
```

Expected: `preinstall` xác nhận đúng Node/npm, `postinstall` chạy `build-themes`, generated output sau `npm ci` không khác output ở Step 2. Không dùng `--ignore-scripts` tại đây hoặc ở các lần cài sau.

- [ ] **Step 5: Visual smoke tập trung layout**

Kiểm tra ở `1366x768` và mobile khoảng `390x844`:

- Drawer/sidebar mở, thu gọn và điều hướng đúng.
- Toolbar/header không tràn.
- Popup không vượt viewport và vẫn chặn đóng trong khi lưu/dirty.
- DataGrid pager, adaptive columns và action buttons đọc được.
- Attendance giữ mục tiêu 5 card/hàng tại content width phù hợp; mobile một cột/ngang cuộn theo AUI.

Chỉ sửa CSS selector/spacing bị hỏng; không cố tái tạo pixel-perfect theme 23.2.3.

- [ ] **Step 6: Commit checkpoint**

```powershell
git add ui/src/themes ui/src/dx-styles.scss ui/src/app
git commit -m "[DX19-FE-03] Rebuild DevExtreme 19 themes"
```

Stage exact files thực sự đổi; không stage toàn bộ `ui/src/app` nếu có thay đổi ngoài scope.

---

### Task 5: Hồi quy tự động toàn UI (`DX19-QA-01`)

**Files:**
- Modify only when a real compatibility expectation changed: existing `ui/src/**/*.spec.ts`
- Modify: `tasks.md`
- Modify: `.agents/frontend/MEMORY.md`

**Interfaces:**
- Consumes: Angular 12 + DevExtreme 19 source và theme đã compile.
- Produces: bằng chứng tự động rằng migration không làm mất behavior.

- [ ] **Step 1: Chạy full ChromeHeadlessCI**

Run:

```powershell
npm --prefix ui run test:ci
```

Expected: toàn bộ số spec hiện hữu và spec bổ sung đều pass; không xóa hoặc `xit`/`fdescribe` test.

- [ ] **Step 2: Chạy development build sạch**

Run:

```powershell
npm --prefix ui run build -- --configuration development
```

Expected: pass không template/type error. Cảnh báo chỉ được chấp nhận sau khi ghi nguyên nhân; DevExtreme license warning không được xem là lỗi migration nhưng vẫn giữ trong known risks.

- [ ] **Step 3: Kiểm tra dependency và source guard**

Run:

```powershell
npm --prefix ui ls --package-lock-only
npm --prefix ui ls @angular/core @angular/cli @angular/cdk typescript rxjs zone.js devextreme devextreme-angular devextreme-themebuilder devextreme-cli devextreme-schematics
rg -n '"(devextreme|devextreme-angular|devextreme-themebuilder)"\s*:\s*"(latest|\^|~)' ui/package.json
rg -n -- "--legacy-peer-deps|skipLibCheck|strictTemplates.*false|hash.*false" ui
```

Expected: logical tree từ lockfile và targeted version tree sạch, không `invalid`/sai version; không version floating hoặc compiler bypass. Với npm 8/Windows, physical-tree scan sau Angular ngcc có thể liệt kê `__ngcc_entry_points__.json` và các package con hoisted của optional Darwin `fsevents` là `extraneous`; phải ghi rõ exact artifact và chứng minh chúng không nằm trong logical lock graph, không được chạy `npm prune` chỉ để che kết quả tạm thời.

- [ ] **Step 4: Kiểm tra diff**

Run:

```powershell
git -c safe.directory=C:/my-works/MamNon/apps/api-portal diff --check
git -c safe.directory=C:/my-works/MamNon/apps/api-portal status --short
```

Expected: không whitespace error; không có thay đổi trong `api/`, `deploy/`, environment API URL hoặc routing contract.

- [ ] **Step 5: Ghi kết quả checkpoint**

Trong `tasks.md` và frontend memory ghi:

- Version thực tế từ `node --version`, `npm --version`, `ng version`.
- Số test pass/fail.
- Development build result.
- Warning còn lại.
- Không chạy production/IIS.

- [ ] **Step 6: Commit checkpoint**

```powershell
git add tasks.md .agents/frontend/MEMORY.md ui/src/app/app-routing.spec.ts ui/src/app/pages/users/users.component.spec.ts ui/src/app/pages/teachers/teachers.component.spec.ts ui/src/app/pages/students/students.component.spec.ts ui/src/app/pages/student-groups/student-groups.component.spec.ts ui/src/app/pages/attendance/attendance.component.spec.ts
git commit -m "[DX19-QA-01] Verify Angular 12 UI regressions"
```

Chỉ giữ các spec thực sự thay đổi trong lệnh stage; không stage generated `dist/` hoặc thay đổi backend.

---

### Task 6: Smoke-test thủ công toàn portal (`DX19-QA-02`)

**Files:**
- Modify: `tasks.md`
- Modify: `.agents/frontend/MEMORY.md`
- Modify: `.agents/shared/MEMORY.md`
- Modify: `ui/AGENTS.md`
- Modify: `ui/README.md`
- Modify: `requirements/07-api-bao-mat-va-van-hanh.md` chỉ nếu tài liệu toolchain hiện ghi version cũ; không đổi requirement nghiệp vụ
- Modify: `plans/README.md` trạng thái epic

**Interfaces:**
- Consumes: API development hiện hữu và UI dev build từ Task 5.
- Produces: handoff hoàn chỉnh, có bằng chứng chức năng DevExtreme runtime.

- [ ] **Step 1: Khởi động development UI bằng toolchain đã khóa**

Run:

```powershell
npm --prefix ui start
```

Expected: `https://localhost:4200`; không đổi certificate script hoặc API base URL.

- [ ] **Step 2: Smoke auth/setup/shell**

Kiểm tra:

- Setup status điều hướng đúng; không biến setup thành đăng ký.
- Login, logout, refresh/session restore và `/me` hoạt động.
- Hash URL vẫn ở dạng `/#/...`.
- Sidebar/header/drawer đúng theo role và responsive.
- Toàn bộ text hiển thị tiếng Việt.

- [ ] **Step 3: Smoke các màn hình quản trị DevExtreme**

Kiểm tra từng màn:

1. `Tài khoản quản trị`: remote grid, filter, paging, create/edit/password/delete.
2. `Giáo viên`: filter/group picker/grid, create/detail/edit/password/delete, conflict behavior.
3. `Nhóm`: tabs, group CRUD, teacher assignment, roster add/move/remove, policy popup.
4. `Học sinh`: remote filters, adaptive grid, schedule form, assignment popup, dirty/conflict flow.

Không bắt buộc thực hiện mutation phá dữ liệu thật; dùng dữ liệu development/test có thể hoàn tác.

- [ ] **Step 4: Smoke điểm danh**

Kiểm tra:

- Date/group/filter/search và Missing/Saved loading.
- 5 trạng thái, permission select, notes, dirty indicator và summary.
- Full-roster save, conflict reload, read-only state và no-scheduled-student state.
- Historical recovery group/teacher/student picker, multi-select List, popup close guard.
- Card layout desktop/mobile vẫn đúng bố cục AUI.

- [ ] **Step 5: Smoke accessibility và browser console**

- Tab/Shift+Tab thao tác được button, select, grid action, popup và recovery list.
- Focus invalid field/card hoạt động.
- Label/ARIA không bị mất do wrapper cũ.
- Console không có runtime exception, unknown option warning hoặc deprecation warning ảnh hưởng behavior.

- [ ] **Step 6: Cập nhật tài liệu và durable memory**

Thay baseline Angular 15.2/DevExtreme 23.2.3 bằng ma trận mới trong tài liệu frontend. Ghi rõ:

- Angular 12 và Node 14 đã EOL, chỉ dùng do ràng buộc DevExtreme 19.2.5.
- Node chỉ cần trên máy phát triển/build; IIS target vẫn nhận static files.
- Production/IIS chưa được kiểm tra trong epic.
- Cách dùng NVM và lệnh kiểm tra version trước `npm ci`.

- [ ] **Step 7: Final verification**

Run:

```powershell
node --version
npm --version
npm --prefix ui run test:ci
npm --prefix ui run build -- --configuration development
npm --prefix ui ls
git -c safe.directory=C:/my-works/MamNon/apps/api-portal diff --check
```

Expected: đúng version; full test/build/dependency tree pass; diff sạch. Không chạy production/IIS.

- [ ] **Step 8: Commit hoàn tất epic**

```powershell
git add plans/README.md tasks.md ui/AGENTS.md ui/README.md .agents/frontend/MEMORY.md .agents/shared/MEMORY.md requirements/07-api-bao-mat-va-van-hanh.md
git commit -m "[DX19-QA-02] Complete DevExtreme 19 migration"
```

Chỉ stage `requirements/07...` nếu nội dung thực sự cần cập nhật.

## 6. Smoke-test acceptance matrix

| Khu vực | Desktop | Mobile | Mutation | Dirty/409 | Paging/search | A11y |
|---|---:|---:|---:|---:|---:|---:|
| Setup/Login/Shell | Bắt buộc | Bắt buộc | Login/logout | N/A | N/A | Bắt buộc |
| Tài khoản quản trị | Bắt buộc | Adaptive | Bắt buộc | Bắt buộc | Bắt buộc | Bắt buộc |
| Giáo viên | Bắt buộc | Adaptive | Bắt buộc | Bắt buộc | Bắt buộc | Bắt buộc |
| Nhóm/chính sách/roster | Bắt buộc | Adaptive | Bắt buộc | Bắt buộc | Bắt buộc | Bắt buộc |
| Học sinh/lịch học | Bắt buộc | Adaptive | Bắt buộc | Bắt buộc | Bắt buộc | Bắt buộc |
| Điểm danh chính | 5 card/hàng mục tiêu | 1 cột/scroll | Bắt buộc | Bắt buộc | Local filter | Bắt buộc |
| Recovery lịch sử | Bắt buộc | Dùng được | Bắt buộc | Bắt buộc | Picker search | Bắt buộc |

## 7. Điều kiện hoàn tất

Epic `DX19` chỉ hoàn tất khi:

- Node/npm và toàn bộ root dependency khớp ma trận mục 3.
- `npm install`/`npm ci` không cần `--force` hoặc `--legacy-peer-deps`.
- Theme được sinh bởi 19.2.5 và được development build sử dụng.
- Không còn import/binding API DevExtreme mới đã biết.
- Full ChromeHeadlessCI pass, không skip test.
- Development build pass với strict compiler/template.
- Smoke-test matrix hoàn tất, không mất tính năng hoặc REST behavior.
- Không có thay đổi trong backend/API contract/hash routing/auth/IIS environment.
- Production/IIS không chạy nếu chưa có invocation `$gv-portal-production`.
- `tasks.md`, plan index, role/shared memory và tài liệu frontend phản ánh đúng baseline mới.

## 8. Rủi ro còn chấp nhận

- Angular 12, Node 14 và DevExtreme 19.2.5 đều đã EOL; không còn bản vá bảo mật chính thức. Đây là rủi ro sản phẩm phải được ghi trong tài liệu vận hành.
- Chrome/Edge mới có thể chạy được nhưng không còn nằm trong ma trận test chính thức của Angular 12/DevExtreme 19.2.5; smoke-test browser thực tế là bắt buộc.
- Theme 19.2.5 có thể khác màu, padding và font so với 23.2.3; chỉ coi là lỗi khi phá bố cục, khả năng đọc hoặc thao tác.
- Việc pin package giúp build tái lập nhưng không loại bỏ rủi ro supply-chain của dependency cũ; lưu lockfile và dùng `npm ci` sau migration.
- Production/IIS compatibility chưa được xác nhận cho đến khi người dùng gọi `$gv-portal-production` ở một lượt riêng.

## 9. Tài liệu tham chiếu chính thức

- [Angular version compatibility](https://angular.dev/reference/versions)
- [DevExtreme supported Angular versions](https://js.devexpress.com/Angular/Documentation/25_1/Guide/Angular_Components/Supported_Versions/)
- [DevExtreme 19.2 migration guide](https://js.devexpress.com/Angular/Documentation/19_2/Guide/Common/Migrate_to_the_New_Version/)
- [DevExtreme 19.2 Angular component configuration](https://js.devexpress.com/Angular/Documentation/19_2/Guide/Angular_Components/Component_Configuration_Syntax/)
- [DevExtreme 19.2 Popup API](https://js.devexpress.com/Angular/Documentation/19_2/ApiReference/UI_Widgets/dxPopup/)
- [DevExtreme 19.2 List configuration](https://js.devexpress.com/Angular/Documentation/19_2/ApiReference/UI_Widgets/dxList/Configuration/)
