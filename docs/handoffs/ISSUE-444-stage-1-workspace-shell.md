# تحویل وظیفه — ISSUE-444-stage-1-workspace-shell

- **Issue:** [#444](https://github.com/hamidreza1394/Accounting/issues/444) — مأموریت جامع مرحله اول (پوستهٔ حرفه‌ای فرانو، منوها و Workspace چندتب)
- **Agent/مسئول:** Z.ai (GLM) طبق پرامپ مأموریت 2026-11-07
- **تاریخ:** 2026-11-07
- **وضعیت:** انجام‌شده — ۶ PR مستقل/stacked ثبت شد؛ هیچ PRای Merge نشده است (ادغام فقط با اجازهٔ صریح مالک).

## هدف و معیارهای پذیرش

- [x] PR 1 — دسترسی مدیر نصب به عملیات ایجاد/ویرایش ساختار پایه HR
- [x] PR 2 — دکمهٔ بازگشت یکنواخت صفحهٔ پروندهٔ کاربر با حفظ فیلترها
- [x] PR 3 — پوستهٔ حرفه‌ای فرانو: Header سرمه‌ای، Sidebar راست‌چین گروهی، Breadcrumb واقعی
- [x] PR 4 — Workspace چندتب ماندگار در حافظه (بدون Storage)
- [x] PR 5 — محافظت فرم‌های Dirty در بستن تب، خروج و تغییر زمینهٔ کاری
- [x] PR 6 — یکپارچه‌سازی نهایی، آزمون‌های یکپارچگی و مستند Handoff

## جدول PRها

| # | عنوان | شاخه | Commit | PR | وابستگی (Base) |
|---|---|---|---|---|---|
| 1 | fix(hr): allow installation administrator to access HR creation actions | `fix/hr-installation-admin-access` | `17f55b26` | [#566](https://github.com/hamidreza1394/Accounting/pull/566) | `main` |
| 2 | fix(users): add consistent back navigation to user details | `fix/user-detail-back-nav` | `8d8b99da` | [#567](https://github.com/hamidreza1394/Accounting/pull/567) | `main` |
| 3 | feat(ui): build professional Farano ERP application shell | `feat/ui-app-shell` | `f706c791` | [#568](https://github.com/hamidreza1394/Accounting/pull/568) | `main` |
| 4 | feat(ui): add persistent multi-tab workspace navigation | `feat/ui-workspace-tabs` | `9b472a45` | [#569](https://github.com/hamidreza1394/Accounting/pull/569) | `feat/ui-app-shell` (stacked) |
| 5 | feat(ui): protect dirty forms during workspace navigation | `feat/ui-dirty-form-protection` | `6203d504` | [#570](https://github.com/hamidreza1394/Accounting/pull/570) | `feat/ui-workspace-tabs` (stacked) |
| 6 | fix(ui): polish workspace navigation and HR administration flows | `fix/ui-final-polish` | همین سند | ثبت‌شده در همین PR | `feat/ui-dirty-form-protection` (stacked) |

## خلاصه تغییرات به تفکیک PR

### PR 1 — دسترسی مدیر نصب به عملیات پایه HR
- ریشهٔ مشکل: سه صفحهٔ فهرست HR و سه صفحهٔ جزئیات، دکمه‌ها را فقط با `codes.includes('hr.*.create|update')` می‌سنجیدند؛ مدیر نصب عضو شرکت نیست و `/me/permissions` برای او flag را با کدهای خالی برمی‌گرداند (D-064). سرور (`AuthorizeHrScopeAsync`) از قبل مدیر نصب را از همهٔ عملیات `hr.*` عبور می‌داد؛ فقط hint فرانت‌اند ناسازگار بود.
- راه‌حل: افزودن hintهای استاندارد `hr.{units,positions,employees}.{create,update}` به `lib/permissions.ts` (نگاشت‌شده به کدهای server-owned) و استفادهٔ شش صفحه از `can(access, …)` — بدون کپی منطق مجوز در صفحات. هیچ تغییری در Backend یا Migration.
- Backend برای این PR تغییر نکرد؛ enforce همچنان سمت سرور است (D-013، D-064، D-072).

### PR 2 — بازگشت پروندهٔ کاربر
- `UserDetailPage` لینک بازگشت را در هر سه حالت Loading/Error/Ready زیر Breadcrumb نشان می‌دهد (آیکون فلش RTL).
- `userListReturn.ts`: صفحهٔ فهرست، کوئری خود را فقط در حافظه به یاد می‌سپارد؛ بازگشت با همان `q`/فیلتر/مرتب‌سازی/صفحه‌بندی انجام می‌شود. بارگذاری تازه به فهرست ساده برمی‌گردد. `UserEditPage` از همان مسیر استفاده می‌کند.

### PR 3 — پوستهٔ اصلی
- `AppShell` ترکیب‌کننده شد: Header (برند/شرکت/دوره/کاربر/خروج) + `shell/AppSidebar.tsx` + `shell/PageBreadcrumb.tsx`.
- منو با گروه‌بندی منطقی فارسی: داشبورد؛ حسابداری و مالی؛ فروش، بازرگانی و انبار؛ گزارش‌ها و اطلاعات پایه؛ منابع انسانی (واقعی، gated با `hr.*.view`)؛ مدیریت سیستم (واقعی، gated)؛ راهنما و پشتیبانی. هیچ نام داخلی انگلیسی (`hr`, `roles`, …) نمایش داده نمی‌شود.
- گروه‌های جمع‌شونده با `aria-expanded`؛ عمیق‌لینک گروه مالک را باز می‌کند؛ نشان «به‌زودی» همیشه دیده می‌شود (بدون مسیر جعلی).
- حالت جمع‌شدهٔ آیکونی فقط دسکتاپ (≥1024px) با `aria-pressed` و نام accessible با `sr-only`.
- Breadcrumb یک‌بار در شل رندر می‌شود؛ صفحات با `usePageBreadcrumb(items)` مسیر واقعی و رکورد را اعلام می‌کنند (۲۰ صفحه مهاجرت یافت)؛ fallback همیشه عنوان مسیر واقعی از جدول مسیرهاست.
- باندل index: ۳۴۳.۶۰KB → ۳۳۵.۱۹KB (gzip ۱۰۱.۷۷ → ۱۰۰.۱۰).

### PR 4 — Workspace چندتب
- مدل تب‌ها در `lib/workspaceTabs.ts` (بدون React/DOM): کلید تب = pathname؛ همان صفحه تب تکراری ندارد؛ رکوردهای متفاوت تب مستقل؛ query string همراه تب ذخیره و هنگام فعال‌سازی بازگردانده می‌شود.
- `shell/WorkspaceTabs.tsx`: نوار تب زیر Header (role=tablist/tab، RTL arrow keys، دکمه بستن با label کامل، دکمه «+») و paneهای همیشه-mount با `TabLocationScope`/`ParamsScope` — صفحهٔ پنهان با کوئری تب دیگر re-render/refetch نمی‌کند.
- Back/Forward با popstate به تب درست می‌رسد؛ refresh فقط تب صفحهٔ جاری را برمی‌گرداند؛ هیچ داده‌ای در web storage نمی‌رود.
- عنوان تب = عنوان مسیر و پس از اعلام صفحه با `useDocumentTitle` نام رکورد (sink جدا شده از router).

### PR 5 — محافظت فرم‌های Dirty
- `lib/formGuard.ts`: رجیستری سبک مشترک؛ `useFormGuard(dirty)` زیر کلید تبِ صفحه (بیرون شل: `single-page`). هیچ محتوای فرمی ذخیره نمی‌شود؛ `beforeunload` قبلی هر فرم حفظ شده.
- بستن تب Dirty → `ConfirmDialog` استاندارد (danger، فوکوس روی انصراف)؛ خروج و تغییر شرکت/دوره مالی در AppShell با `hasAnyDirtyForm()` گارد می‌شوند؛ انصراف هیچ تغییری نمی‌دهد.
- پس از ذخیرهٔ موفق، `markClean()` هشدار کاذب را برمی‌دارد. فرم‌های متصل: کاربر (ایجاد/ویرایش)، واحد سازمانی، سمت (ردیابی Dirty به آن افزوده شد)، کارمند (ایجاد/ویرایش).

### PR 6 — یکپارچه‌سازی نهایی
- عنوان تب مسیر ناشناخته با عنوان NotFound هماهنگ شد («صفحه پیدا نشد»).
- تست یکپارچگی جدید: کلیک واقعی آیتم منوی Sidebar در شل چندتب، تب درست را باز/فعال می‌کند (منو و تب‌ها یک Router دارند).
- این سند Handoff و ورودی WORK_LOG ثبت شد؛ تصاویر قبل/بعد پوسته تهیه شد.

## فایل‌های مهم تغییرکرده

- `ClientApp/src/lib/permissions.ts` — hintهای create/update زیرساخت HR (PR 1)
- `ClientApp/src/features/hr/pages/*.tsx` — اتصال شش صفحه به `can()` (PR 1) و مهاجرت Breadcrumb (PR 3) و اتصال گارد فرم (PR 5)
- `ClientApp/src/components/admin/users/{UserDetailPage,UserEditPage,UsersListPage,userListReturn}.tsx|ts` — بازگشت یکنواخت (PR 2)
- `ClientApp/src/components/shell/{AppSidebar,PageBreadcrumb,WorkspaceTabs}.tsx` — پوسته و workspace (PR 3/4/5)
- `ClientApp/src/lib/{workspaceTabs,formGuard}.ts` — مدل تب‌ها و رجیستری فرم‌های Dirty (PR 4/5)
- `ClientApp/src/lib/router.tsx` — TabLocationScope/ParamsScope و sink عنوان تب؛ Router و وابستگی‌ها بدون تغییر (PR 4)
- `ClientApp/src/components/AppShell.tsx`, `ClientApp/src/App.tsx` — حالت `workspaceTabs` و گاردهای خروج/تغییر زمینه (PR 4/5)
- `ClientApp/src/styles/index.css` — نوار Breadcrumb، Rail جمع‌شده، نوار تب (PR 3/4)
- `docs/UI_COMPONENTS.md` — مستند سه بخش جدید پوسته (PR 3/4/5)

## تصمیم‌های فنی

- وابستگی واقعی PRها به‌صورت **stacked PR** مدیریت شد (۴→۳، ۵→۴، ۶→۵) تا هر PR مستقلاً قابل بازبینی باشد و شاخه‌ها تداخل نگیرند؛ PRهای ۱ و ۲ مستقل از `main`اند. هیچ PRای Merge نشده است.
- معماری Breadcrumb: به‌جای دو منبع حقیقت، صفحات «اعلام» می‌کنند و شل «رندر» می‌کند؛ Provider به‌ازای تب کلید می‌خورد.
- کلید یکتایی تب = pathname؛ query همراه تب (نه کلید) تا فیلترها تب جدید نسازند.
- ذخیرهٔ تب‌ها پس از refresh عمداً پیاده نشد (الزام امنیتی مأموریت: بدون Storage؛ راه سادهٔ قابل پیش‌بینی انتخاب شد).
- فرم‌های نقش‌ها/کاتالوگ مجوزها خارج از محدودهٔ این مرحله ماندند؛ اتصال آن‌ها بعداً یک خط `useFormGuard` است.
- تصمیم معماری جدیدی (ADR جدید) لازم نشد؛ D-011، D-031، D-048/D-049، D-064، D-071، D-072 و D-074 رعایت شدند.

## بررسی و آزمون

| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| `npm ci` | موفق | 429 بسته |
| `npm run typecheck` | موفق | در هر شاخه پیش از commit |
| `npm test` | موفق | main: 790 → نهایی: **824/824** (+۳۴ تست جدید) |
| `npm run build` | موفق | index نهایی: ۳۴۲.۳۵KB / gzip ۱۰۲.۳۸ |
| `npm run check:offline` | موفق | بدون هیچ منبع بیرونی |
| `python3 scripts/check_static_assets.py` | موفق | PASS |
| Backend (`dotnet build/test`) | اجرا نشده | هیچ تغییری در Backend، کاتالوگ مجوز یا Migration در این مرحله انجام نشد؛ طبق مأموریت الزامی نبود |
| مرورگر واقعی (Chromium headless) | انجام شد | تصاویر قبل/بعد پوسته در 1440/768/360 شامل Rail جمع‌شده، کشوی موبایل، نوار تب و بستن تب؛ LCP/CLS اندازه‌گیری نشد (سرور تولیدی در Sandbox در دسترس نبود) |

وضعیت CI هر PR در لحظهٔ ثبت: Pending (طبق سیاست مأموریت منتظر CI نماندیم)؛ وضعیت نهایی در گزارش پایانی مأموریت و کامنت به‌روزرسانی PR 6 ثبت می‌شود.

## امنیت، داده و سازگاری

- هیچ مجوزی در فرانت‌اند enforce نمی‌شود؛ همهٔ hintها صرفاً نمایشی‌اند و سرور مرجع است (D-064).
- مدیر نصب با مدیر شرکت یکی نشده؛ فقط عملیات پایه HR که سرور از قبل مجاز می‌دانست در UI نمایان شد (D-064، D-071، D-072).
- TenantId/FiscalPeriod و جریان انتخاب صریح شرکت/دوره (D-031) دست نخورده؛ تغییر زمینه پس از تأیید، صفحات را با Context جدید بارگذاری می‌کند.
- هیچ Migration یا تغییر Schema؛ هیچ وابستگی npm جدید؛ همهٔ دارایی‌ها محلی و آفلاین (`check:offline` سبز).
- تب‌ها و رجیستری فرم‌ها فقط در حافظه؛ چیزی حساس به web storage نمی‌رود.

## ریسک‌ها و کارهای باقی‌مانده

- پوشش E2E مرورگر (Playwright) و axe در CI برای این مرحله اجرا نشد (محدودیت Sandbox)؛ پوشش jsdom + بررسی دستی headless جایگزین شد.
- بازگردانی اسکرول بین تب‌ها best-effort است.
- تب‌های باز پس از تغییر شرکت/دوره حفظ می‌شوند و صفحات company-scoped با Context جدید refetch می‌کنند؛ در نسخهٔ بعدی می‌تواند تصمیم محصولی برای بستن/برچسب‌زدن تب‌های وابسته به Context قبلی گرفته شود.
- فرم‌های نقش‌ها و کاتالوگ مجوزها هنوز به رجیستری Dirty متصل نیستند (خارج از محدودهٔ این مرحله).

## ترتیب پیشنهادی Merge

1. #566 (PR 1) و #567 (PR 2) — مستقل، هر زمان آماده بودند.
2. #568 (PR 3) — پوسته.
3. #569 (PR 4) — پس از #568؛ پس از Merge می‌توان base را به `main` برد.
4. #570 (PR 5) — پس از #569.
5. PR 6 (همین شاخه) — پس از #570.

## شناسه‌های Git

- Branch: `fix/ui-final-polish`
- Commit: آخرین commit همین شاخه (جدول بالا به‌روزرسانی می‌شود)
- Pull Request: ثبت‌شده پس از Push (لینک در توضیحات PR و WORK_LOG)
