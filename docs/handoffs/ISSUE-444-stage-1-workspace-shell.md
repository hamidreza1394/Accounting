# تحویل وظیفه — ISSUE-444-stage-1-workspace-shell

- **Issue:** [#444](https://github.com/hamidreza1394/Accounting/issues/444) — مأموریت جامع مرحله اول (پوستهٔ حرفه‌ای فرانو، منوها و Workspace چندتب)
- **Agent/مسئول:** Z.ai (GLM) طبق پرامپ مأموریت 2026-11-07
- **تاریخ:** 2026-11-07 — اصلاح تاریخچهٔ ادغام: 2026-11-08
- **وضعیت:** انجام‌شده و **ادغام‌شده** — هر ۶ PR (#566 تا #571) در 2026-11-07 ادغام شدند؛ انتهای نهایی `main` پس از ادغام: `cb2ddd57bdff88e0be75a5e0e75f76dc25f76d13`. (این سند در 2026-11-08 با تاریخچهٔ واقعی ادغام از GitHub API اصلاح شد — PR 1-A مأموریت تکمیلی #444.)

## هدف و معیارهای پذیرش

- [x] PR 1 — دسترسی مدیر نصب به عملیات ایجاد/ویرایش ساختار پایه HR
- [x] PR 2 — دکمهٔ بازگشت یکنواخت صفحهٔ پروندهٔ کاربر با حفظ فیلترها
- [x] PR 3 — پوستهٔ حرفه‌ای فرانو: Header سرمه‌ای، Sidebar راست‌چین گروهی، Breadcrumb واقعی
- [x] PR 4 — Workspace چندتب ماندگار در حافظه (بدون Storage)
- [x] PR 5 — محافظت فرم‌های Dirty در بستن تب، خروج و تغییر زمینهٔ کاری
- [x] PR 6 — یکپارچه‌سازی نهایی، آزمون‌های یکپارچگی و مستند Handoff

## جدول PRها

| # | عنوان | شاخه | Commit نهایی (Head) | Merge commit | PR | وابستگی (Base) | ادغام (UTC) |
|---|---|---|---|---|---|---|---|
| 1 | fix(hr): allow installation administrator to access HR creation actions | `fix/hr-installation-admin-access` | `17f55b26d04b` | `911959da9dd9` | [#566](https://github.com/hamidreza1394/Accounting/pull/566) | `main` | 2026-11-07 15:40 |
| 2 | fix(users): add consistent back navigation to user details | `fix/user-detail-back-nav` | `8d8b99da47c3` | `3dcce666d01e` | [#567](https://github.com/hamidreza1394/Accounting/pull/567) | `main` | 2026-11-07 15:41 |
| 3 | feat(ui): build professional Farano ERP application shell | `feat/ui-app-shell` | `9ae1aaa6ecae` | `a0b07ac7fb61` | [#568](https://github.com/hamidreza1394/Accounting/pull/568) | `main` (پس از ادغام #566/#567 همگام شد) | 2026-11-07 15:57 |
| 4 | feat(ui): add persistent multi-tab workspace navigation | `feat/ui-workspace-tabs` | `5e460f4d3a05` | `dad4c10c3ee4` | [#569](https://github.com/hamidreza1394/Accounting/pull/569) | `feat/ui-app-shell` (stacked) | 2026-11-07 15:58 |
| 5 | feat(ui): protect dirty forms during workspace navigation | `feat/ui-dirty-form-protection` | `88c24114a551` | `e6f558c3ddd7` | [#570](https://github.com/hamidreza1394/Accounting/pull/570) | `feat/ui-workspace-tabs` (stacked) | 2026-11-07 15:58 |
| 6 | fix(ui): polish workspace navigation and HR administration flows | `fix/ui-final-polish` | `33cddd7d0fcd` | `cb2ddd57bdff` | [#571](https://github.com/hamidreza1394/Accounting/pull/571) | `feat/ui-dirty-form-protection` (stacked) | 2026-11-07 15:59 |

> **یادداشت SHA:** نسخهٔ اولیهٔ این سند برای #568 و #571 به commitهای میانی (`f706c791` و `9b482e10`) اشاره می‌کرد؛ پس از آن، commitهای همگام‌سازی/مستندات بعدی به همان شاخه‌ها اضافه شد. SHAهای جدول بالا، Head واقعیِ هر PR در لحظهٔ ادغام است که از GitHub API استخراج شده است.

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

- وابستگی واقعی PRها به‌صورت **stacked PR** مدیریت شد (۴→۳، ۵→۴، ۶→۵) تا هر PR مستقلاً قابل بازبینی باشد و شاخه‌ها تداخل نگیرند؛ PRهای ۱ و ۲ مستقل از `main`اند. در زمان نگارش سند هیچ PRای Merge نشده بود؛ ادغام نهایی هر شش PR در 2026-11-07 دقیقاً طبق همین ترتیب پیشنهادی انجام شد (جدول «بررسی و آزمون» بالای سند).
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

### وضعیت CI و اصلاح یافتهٔ E2E

- PRهای #566، #567 و #568: **تمام checkها سبز** (`client`، `build-and-test`، `e2e`، `secret-scan`، `Semgrep`، `CodeQL`/`CodeRabbit`، `Analyze`، `publish`).
- PR #569 (دور اول CI): همه سبز به‌جز **e2e: failure** — axe یک نقض critical `aria-required-children` روی `.faranou-tabbar-tabs` گزارش کرد (tablist به فرزند مستقیم `tab` نیاز دارد و دکمهٔ بستن داخل `tab` نقض `nested-interactive` می‌شد).
- **اصلاح انجام‌شده روی همان شاخه** (commit `5e460f4d`): نوار تب از `role=tablist/tab/tabpanel` به الگوی واقعی خودش — یک `nav` از صفحات باز با `aria-current="page"` + دکمهٔ بستن مستقل — بازطراحی شد؛ تست‌ها به ساختار جدید به‌روز شدند؛ اسکن کامل axe روی شل رندرشده **صفر نقض**؛ سپس شاخه‌های #570 و #571 با merge همگام شدند (commits `88c24114` و `9b482e10`؛ ۸۲۴/۸۲۵ تست محلی سبز در هر شاخه).
- **نتیجهٔ نهایی CI (2026-11-07، دور دوم): هر شش PR سبز کامل** — `client`، `build-and-test`، `e2e` (Playwright + axe روی سرور واقعی)، `secret-scan`، `Semgrep scan`/`Semgrep OSS`، `CodeQL`، `Analyze (actions/python/csharp/javascript-typescript)` و `publish`/`publish-scripts`/`publish-windows` همه `success` روی SHA نهایی هر شاخه (CodeRabbit `skipped` طبق تنظیمات مخزن).

### آمار تست واقعی از لاگ CI (جاب `client`، SHA نهایی هر PR)

| PR | Head SHA ادغام‌شده | نتیجهٔ vitest از لاگ CI | وضعیت checkها |
|---|---|---|---|
| #566 | `17f55b26d04b` | ۷۹۲/۷۹۲ (۷۴ فایل تست) | همه سبز |
| #567 | `8d8b99da47c3` | ۷۹۰/۷۹۰ (۷۵ فایل تست) | همه سبز |
| #568 | `9ae1aaa6ecae` | ۸۰۹/۸۰۹ (۷۷ فایل تست) | همه سبز |
| #569 | `5e460f4d3a05` | ۸۱۳/۸۱۳ (۷۸ فایل تست) | همه سبز |
| #570 | `88c24114a551` | ۸۲۴/۸۲۴ (۷۹ فایل تست) | همه سبز |
| #571 | `33cddd7d0fcd` | ۸۲۵/۸۲۵ (۷۹ فایل تست) | همه سبز |

> منبع: لاگ خام جاب `client` در workflow «.NET CI» برای Head SHA هر PR (استخراج‌شده در 2026-11-08 از GitHub Actions API). رشد شمار تست از ۷۹۰ تا ۸۲۵ دقیقاً با افزودن تست‌های هر PR هم‌خوان است.

## امنیت، داده و سازگاری

- هیچ مجوزی در فرانت‌اند enforce نمی‌شود؛ همهٔ hintها صرفاً نمایشی‌اند و سرور مرجع است (D-064).
- مدیر نصب با مدیر شرکت یکی نشده؛ فقط عملیات پایه HR که سرور از قبل مجاز می‌دانست در UI نمایان شد (D-064، D-071، D-072).
- TenantId/FiscalPeriod و جریان انتخاب صریح شرکت/دوره (D-031) دست نخورده؛ تغییر زمینه پس از تأیید، صفحات را با Context جدید بارگذاری می‌کند.
- هیچ Migration یا تغییر Schema؛ هیچ وابستگی npm جدید؛ همهٔ دارایی‌ها محلی و آفلاین (`check:offline` سبز).
- تب‌ها و رجیستری فرم‌ها فقط در حافظه؛ چیزی حساس به web storage نمی‌رود.

## ریسک‌ها و کارهای باقی‌مانده

- پوشش E2E کامل Playwright/axe فقط در CI قابل اجراست (Sandbox فاقد Backend/SQL)؛ با این حال یافتهٔ axe همان دور اول CI (نقض a11y نوار تب) گرفته و اصلاح شد — اثبات ارزش این پوشش. بررسی headless محلی روی خروجی build انجام شد.
- بازگردانی اسکرول بین تب‌ها best-effort است.
- تب‌های باز پس از تغییر شرکت/دوره حفظ می‌شوند و صفحات company-scoped با Context جدید refetch می‌کنند؛ در نسخهٔ بعدی می‌تواند تصمیم محصولی برای بستن/برچسب‌زدن تب‌های وابسته به Context قبلی گرفته شود.
- فرم‌های نقش‌ها و کاتالوگ مجوزها هنوز به رجیستری Dirty متصل نیستند (خارج از محدودهٔ این مرحله).

## ترتیب پیشنهادی Merge

1. #566 (PR 1) و #567 (PR 2) — مستقل، هر زمان آماده بودند.
2. #568 (PR 3) — پوسته.
3. #569 (PR 4) — پس از #568؛ پس از Merge می‌توان base را به `main` برد.
4. #570 (PR 5) — پس از #569.
5. #571 (PR 6) — پس از #570.

### آزمون ادغام متقاطع (انجام‌شده محلی)

- `fix/hr-installation-admin-access` × `feat/ui-app-shell`: **auto-merge بدون تعارض** (هر دو به `main` می‌روند).
- `fix/user-detail-back-nav` × `feat/ui-app-shell`: یک تعارض معلوم در `ClientApp/src/components/admin/users/UserDetailPage.tsx` (هر دو PR بلوک `return` را ویرایش کرده‌اند؛ GitHub Mergeability فعلی هر دو True است چون هنوز یکی Merge نشده).
  - **دستور رفع هنگام ادغام:** نسخهٔ نهایی باید «حذف رندر درون‌صفحه‌ای Breadcrumb و افزودن `usePageBreadcrumb(crumbs)` به بالای کامپوننت (از PR 3)» + «حفظ `<BackToList />` زیر Breadcrumb شل (از PR 2)» باشد؛ شاخهٔ #571 (`fix/ui-final-polish`) دقیقاً همین ترکیب را دارد و می‌توان از آن به‌عنوان مرجع رفع تعارض استفاده کرد.
  - پس از رفع، `npm test` روی فایل‌های `admin/users` اجرا شود (پوشش هر دو PR در آن هست).

## شناسه‌های Git

- انتهای نهایی `main` پس از ادغام هر ۶ PR: `cb2ddd57bdff88e0be75a5e0e75f76dc25f76d13` (Merge commit PR #571)
- جدول کامل شاخه/Commit/Merge commit/PR: بخش «جدول PRها» در ابتدای سند
- آرشیو تصاویر پیش‌نمایش: شاخهٔ `artifacts/stage-1-ui-preview` (لینک‌ها در `artifacts/ui-preview/stage-1/README.md`)

## بازبینی نهایی با اجرای واقعی برنامه (2026-11-07 — مأموریت بازبینی پیش از Merge)

### محیط واقعی اجرا

- SQL Server 2022 واقعی روی Sandbox (نصب بسته رسمی مایکروسافت بدون Docker) + Migration واقعی اسکیما (`dotnet Server.dll --migrate`) + Seed واقعی مخزن (`tests/e2e/seed.mjs`).
- سرور پابلیش‌شده Release از کد هر شاخه، با `ASPNETCORE_ENVIRONMENT=Production`؛ ورود واقعی با حساب مدیر نصب Seed (Cookie + Anti-forgery؛ هیچ امنیتی غیرفعال نشده).
- مرورگر واقعی Playwright Chromium از `tests/e2e` مخزن؛ عرض‌های 360/768/1024/1280/1440.

### نتایج سناریوها (نسخه یکپارچه: سرِ Stack + PR #566)

هر **۱۲ سناریو PASS** (اسکرین‌شات و JSON در `artifacts/ui-preview/stage-1/`، شاخه `artifacts/stage-1-ui-preview`): باز شدن تب، تب جدید بدون بستن قبلی، حفظ جست‌وجو/Query بین تب‌ها، جلوگیری از تب تکراری، دو رکورد = دو تب، بستن تب فعال/غیرفعال با جانشین درست، هشدار Dirty هنگام بستن تب/خروج/تغییر شرکت (لغو = حفظ فرم؛ تأیید = انجام عملیات)، Back/Forward مرورگر، بازگشت به داشبورد پس از بستن آخرین تب، عدم نشت داده Context قبلی پس از تغییر شرکت.

### یافته مهم یکپارچگی (بدون تغییر کد؛ نیازمند رعایت ترتیب Merge)

روی سرِ Stack (568→571) دکمه‌های ایجاد HR برای مدیر نصب نمایش داده نمی‌شوند، چون فیکس PR #566 فقط روی شاخهٔ مستقل خودش (main-محور) است و در Stack نیست. ادغام آزمایشی محلی `fix/ui-final-polish` + `fix/hr-installation-admin-access` **بدون تعارض** بود؛ در نسخهٔ ادغام‌شده، دکمه‌ها ظاهر شدند، همهٔ ۱۲ سناریو PASS شد و کل تست کلاینت **۸۳۴/۸۳۴** سبز شد (typecheck/build/check:offline/static-assets سبز). **نتیجه: #566 باید قبل یا هم‌زمان با Stack ادغام شود؛ ادغام Stack بدون #566 رگرسیون نمایش دکمه‌های HR برای مدیر نصب می‌سازد.** کد هیچ‌کدام از PRها برای این مورد تغییر نکرد (رفع تعارض لازم نیست؛ صرفاً ترتیب Merge).

### یافته‌های طراحی (برای تصمیم مالک — بدون تغییر در این مرحله)

- آیتم فعال Sidebar با **آبی-فیروزه‌ای** هایلایت می‌شود؛ استاندارد مأموریت «انتخاب فعال» را در زمرهٔ تأکیدهای **طلایی** ذکر کرده است. در صورت صلاح مالک، تغییر رنگ pill فعال (در `AppSidebar`) یک تغییر CSS کوچک در قلمرو PR #568 است.
- کاشی‌های آیکون زیرسیستم‌ها چندرنگ (سبز/قرمز/آبی/بنفش/کهربایی) هستند؛ از نظر یکدستی ERP محافظه‌کارانه‌تر قابل ملاحظه است (همین قلمرو).

### تست‌های محلی واقعی این دور بازبینی

| شاخه | typecheck | npm test | build | check:offline | static-assets |
|---|---|---|---|---|---|
| `fix/ui-final-polish` (7b754e72) | ✓ | 825/825 ✓ | ✓ | ✓ | ✓ |
| ادغام محلی Stack + #566 | ✓ | 834/834 ✓ | ✓ | ✓ | ✓ |
| `fix/hr-installation-admin-access` (17f55b26) | ✓ | 792/792 ✓ | ✓ | CI | CI |
| `fix/user-detail-back-nav` (8d8b99da) | ✓ | 790/790 ✓ | ✓ | CI | CI |
| `feat/ui-app-shell` (f706c791) | — | CI | ✓ | CI | CI |
| `feat/ui-workspace-tabs` (5e460f4d) | — | CI | ✓ | CI | CI |
| `feat/ui-dirty-form-protection` (88c24114) | — | CI | ✓ | CI | CI |

Backend هیچ تغییری در ۶ PR نداشت؛ nevertheless بیلد Release سرور ۷ بار (هر شاخه) با موفقیت انجام شد. CI همهٔ ۶ PR در زمان بازبینی کاملاً سبز بود (build-and-test، client، e2e، CodeQL، Semgrep، secret-scan، publish).

### بررسی مرورگری شاخه‌به‌شاخه (علاوه بر نسخه یکپارچه)

- `pr-566`: هر ۳ دکمه ایجاد HR برای مدیر نصب روی شاخه خود PR نمایان (PASS×۳، صفر خطای کنسول).
- `pr-567`: بازگشت در Ready + حفظ `q` فهرست (PASS×۳، صفر خطای کنسول).
- `pr-568`: پوسته بدون نوار تب (طبق تفکیک محدوده PRها؛ PASS).
- `pr-569`: T1..T5 تب‌ها PASS.
- `pr-570`: D1/D2 (هشدار Dirty + بستن فرم تمیز بدون هشدار با بازگشت به داشبورد) PASS.

## وضعیت پس از ادغام (به‌روزرسانی 2026-11-08 — PR 1-A مأموریت تکمیلی #444)

### تاریخچه ادغام واقعی

ادغام هر ۶ PR در 2026-11-07 با همین ترتیب توسط مالک مخزن انجام شد (خلاصه‌ی UTC از GitHub API):

1. **15:40** — #566 (`fix/hr-installation-admin-access`) → `911959da9dd9`
2. **15:41** — #567 (`fix/user-detail-back-nav`) → `3dcce666d01e`
3. **15:57** — #568 (`feat/ui-app-shell`) → `a0b07ac7fb61`
4. **15:58** — #569 (`feat/ui-workspace-tabs`) → `dad4c10c3ee4`
5. **15:58** — #570 (`feat/ui-dirty-form-protection`) → `e6f558c3ddd7`
6. **15:59** — #571 (`fix/ui-final-polish`) → `cb2ddd57bdff`

- انتهای نهایی `main`: `cb2ddd57bdff88e0be75a5e0e75f76dc25f76d13`.
- یافتهٔ «ترتیب Merge» بازبینی پیش از ادغام (#566 باید قبل/هم‌زمان با Stack ادغام شود) رعایت شد و رگرسیون پیش‌بینی‌شده رخ نداد؛ در نسخهٔ نهایی main، ترکیب فیکس #566 + Stack روی همان نسخهٔ یکپارچهٔ آزموده‌شده (`pr-integration`) است که هر ۱۲ سناریوی آن PASS بود.

### وضعیت CI نهایی

- همه checkهای هر ۶ PR روی SHA نهایی: `success` (جدول «آمار تست واقعی از لاگ CI» بالای سند)؛ CodeRabbit `skipped` طبق تنظیمات مخزن.
- هیچ job الزامی Fail یا Pending باقی نمانده است.

### محدودیت‌های باقی‌مانده (بازنویسی‌شده از «ریسک‌ها»، همچنان معتبر پس از ادغام)

- بازگردانی اسکرول بین تب‌ها best-effort است.
- تب‌های باز پس از تغییر شرکت/دوره حفظ می‌شوند و صفحات company-scoped با Context جدید refetch می‌کنند؛ تصمیم محصولی بعدی: بستن/برچسب‌زدن تب‌های وابسته به Context قبلی.
- فرم‌های نقش‌ها و کاتالوگ مجوزها به رجیستری Dirty (PR 5) متصل نیستند — اتصال هر فرم یک خط `useFormGuard` است و در مأموریت تکمیلی #444 بررسی می‌شود.
- ذخیرهٔ تب‌ها پس از refresh عمداً پیاده نشده است (الزام امنیتی: بدون web storage).

### تصمیمات معماری ثبت‌شده در این مرحله

- هیچ ADR جدیدی لازم نشد؛ D-011، D-031، D-048/D-049، D-064، D-071، D-072 و D-074 رعایت شدند.
- وابستگی نهایی PRها: #566 و #567 مستقل از main؛ #568→#569→#570→#571 به‌صورت stacked؛ پس از ادغام همه، هیچ شاخهٔ باقی‌مانده‌ای برای این مرحله وجود ندارد.

## تکمیل مأموریت تکمیلی مرحله اول (2026-11-08)

با ایجاد سه PR زیر، نواقص مرحله اول تکمیل و این Handoff «تکمیل‌شده» اعلام می‌شود:

- PR 1-A — اصلاح همین سند با تاریخچه ادغام واقعی: [#572](https://github.com/hamidreza1394/Accounting/pull/572)
- PR 1-B — انتشار لینک‌های مستقیم تصاویر پیش‌نمایش: [#573](https://github.com/hamidreza1394/Accounting/pull/573)
- PR 1-C — تکمیل تست بصری/Responsive و بستن WARN معلق Tooltip: [#574](https://github.com/hamidreza1394/Accounting/pull/574) (سند: `docs/handoffs/ISSUE-444-stage-1-responsive-checks.md`)

هیچ‌کدام Merge نشده‌اند؛ ادغام فقط با اجازهٔ مالک انجام می‌شود.

## تکمیل نهایی مرحلهٔ اول — چرخهٔ عمر فرم‌ها، گارد تغییر زمینه و جریان محلی HR (2026-11-08، PRهای #582..#589)

این بخش، تکمیل «قابل‌استفاده شدن واقعی» مرحلهٔ اول را ثبت می‌کند (مأموریت مالک محصول 2026-11-08). وضعیت D-076 همچنان `Proposed` است و تغییر نکرده؛ پیاده‌سازی بر اساس تصمیم صریح مالک (رتبهٔ ۳ در D-030) در همین محدوده انجام شده و همهٔ انحراف‌های قبلی مرتبط رفع شده‌اند.

### جدول PRها (هیچ‌کدام Merge نشده‌اند)

| # | عنوان | شاخه | Base | وضعیت |
|---|---|---|---|---|
| 1 | feat(workspace): implement four-state form lifecycle | `feat/workspace-form-lifecycle` | `main` | [#582](https://github.com/hamidreza1394/Accounting/pull/582) — CI سبز کامل |
| 2 | feat(workspace): show tab state and navigate blocked forms | `feat/workspace-tab-status` | شاخهٔ PR-1 (stacked) | [#583](https://github.com/hamidreza1394/Accounting/pull/583) — CI سبز کامل |
| 3 | fix(workspace): block context switch until forms are clean | `fix/workspace-context-guard` | شاخهٔ PR-2 (stacked) | [#584](https://github.com/hamidreza1394/Accounting/pull/584) — e2e رفع شد (اصلاح spec)، دور جدید Pending |
| 4 | fix(workspace): isolate async data by active work context | `fix/workspace-stale-context` | شاخهٔ PR-3 (stacked) | [#585](https://github.com/hamidreza1394/Accounting/pull/585) — همان اصلاح e2e، Pending |
| 5 | feat(hr): keep create and edit flows within parent workspace tab | `feat/hr-local-form-navigation` | شاخهٔ PR-4 (stacked) | [#586](https://github.com/hamidreza1394/Accounting/pull/586) — e2e + publish-windows رفع شد، Pending |
| 6 | feat(ui): polish faranou workspace navigation and navigation feedback | `feat/ui-workspace-navigation-polish` | شاخهٔ PR-5 (stacked) | [#587](https://github.com/hamidreza1394/Accounting/pull/587) — Pending |
| 7 | test(workspace): verify context safety and responsive workspace flows | `test/workspace-stage1-integration` | شاخهٔ PR-6 (stacked) | [#589](https://github.com/hamidreza1394/Accounting/pull/589) |

- Commitهای اصلاحی CI (spec E2E و timeout تست lazy) با cherry-pick به همهٔ شاخه‌های زنجیره push شده‌اند؛ هیچ Force Push انجام نشده است.
- **تست محلی واقعی هر شاخه:** typecheck و `npm test` و `npm run build` و `npm run check:offline` سبز؛ شمارش vitest به‌ترتیب: ۸۶۸ / ۸۷۴ / ۸۸۵ / ۸۹۰ / ۸۹۷ / ۸۹۸ / **۹۰۰**. هیچ تستی حذف یا تضعیف نشده است.
- **نتیجهٔ نهایی CI (2026-11-08):** هر هفت PR روی head نهایی **کاملاً سبز** شدند — client، build-and-test، e2e (Playwright + axe روی سرور واقعی و SQL Server)، publish، publish-windows، publish-scripts، CodeQL، Semgrep و secret-scan. اصلاحات میانی: هم‌ترازی spec E2E با تأیید نهایی تغییر زمینه، timeout تست lazy، و ادغام کار یک اجرای هم‌زمان دیگر (fix عنوان مسیر تب در جریان محلی 79d78002) با merge — بدون Force Push و بدون حذف کار دیگران.
- **اصلاح ممیزی زنجیره (ثبت‌شده):** دو commit محتوای PR-7 به‌اشتباه روی شاخهٔ PR-6 push شده بودند؛ با `git revert` از شاخهٔ PR-6 حذف و با cherry-pick به شاخهٔ PR-7 منتقل شدند (بدون Force Push). بعد از اصلاح: PR-6 فقط محتوای خودش را دارد (vitest ‏۸۹۸/۸۹۸ محلی) و PR-7 محتوای کامل (۹۰۰/۹۰۰)؛ diff هر PR دقیقاً محدودهٔ خودش است.
- **Backend:** هیچ تغییری در Backend/Schema/API در این زنجیره انجام نشده است؛ طبق مأموریت اجرای dotnet test الزامی نبود و در Sandbox اجرا نشده گزارش می‌شود.

### پاسخ‌های موضوعی مأموریت (وضعیت واقعی)

1. **چهار وضعیت فرم‌ها:** Implemented — `lib/formGuard.ts` با ۲۱ تست؛ هر ۵ فرم واقعی متصل‌اند.
2. **مسدودسازی تغییر زمینه در Dirty/Saving/Error:** Implemented — ماشین وضعیت شل + تست‌های `AppShellContextGuard.test.tsx` برای هر سه وضعیت.
3. **نمایش همهٔ تب‌های مشکل‌دار:** Implemented — `ProblemTabsDialog` (نام فارسی جاری تب، بدترین وضعیت، پیام، تعداد فرم‌ها).
4. **کلیک روی نام تب:** Implemented — فعال‌سازی از طریق store مشترک (URL/کوئری/اسکرول/وضعیت فرم حفظ می‌شود؛ فوکوس به h1).
5. **تأیید نهایی پس از Clean:** Implemented — خودکار (effectivePhase) پس از تعیین تکلیف همهٔ فرم‌ها.
6. **بی‌اعتبارسازی cache زمینهٔ قبلی:** Implemented — بستن تب‌های وابسته (Janitor) + unmount state؛ PR-4 گارد نسل برای پاسخ‌های دیررس.
7. **پاسخ دیررس:** Implemented — `staleGuard` + تست سناریوی دقیق مأموریت؛ پاسخ دیررس هرگز state شرکت جدید را عوض نمی‌کند.
8. **ایجاد کارمند در همان تب:** Implemented — `?form=new` روی مسیر فهرست؛ تست یکپارچگی + بررسی تصویری مرورگر واقعی.
9. **انصراف Clean/Dirty:** Implemented — `HrFormExit` (Clean بی‌دیالوگ؛ Dirty/Error دیالوگ؛ Saving غیرفعال).
10. **حفظ فیلترها و جستجو:** Implemented — فهرست mount می‌ماند (hidden)؛ تست + تصویر.
11. **Sidebar/Tooltip/رنگ فعال/آیکون/گروه‌بندی:** Implemented — سکشن‌های فارسی UI-08، ۸ زیرسیستم UI-05، Tooltip پروژه‌ای، pill طلایی.
12. **اعلان نمایشی:** حذف شد (زنگ + نقطهٔ قرمز)؛ قابلیت واقعی اعلان آینده در همین سند ردیابی می‌شود.
13. **Breadcrumb همهٔ صفحات:** fallback مسیر واقعی + اعلام صفحات موجود بررسی شد؛ فرم‌های in-tab مسیر والد را با `to` حفظ می‌کنند.
14. **پیام‌های جدید فارسی:** Implemented — `lib/uiTexts.ts` + متن‌های رجیستری/دیالوگ‌ها.
15. **تصاویر واقعی ۵ اندازه:** انجام شد — مرورگر headless واقعی روی bundle تولیدی با API شبیه‌سازی‌شده؛ ۳۶۰/۷۶۸/۱۰۲۴/۱۲۸۰/۱۴۴۰ + Rail + فرم + دیالوگ گارد؛ بدون خطای صفحه؛ آرشیو در شاخهٔ `artifacts/stage-1-ui-preview`.
16. **تست چندشرکتی/امنیتی:** سطح کلاینت اجرا و سبز شد (تست‌های گارد/جداسازی)؛ تست‌های امنیتی Backend/E2E روی SQL Server فقط در CI قابل اجراست (در Sandbox اجرا نشد — صادقانه گزارش می‌شود).
17. **NotImplemented/Blocked باقی‌مانده:** اتصال Entitlement به عملیات، MFA، بازیابی رمز، هفت تب پروندهٔ کاربر، عکس پرسنلی، زیرسیستم‌های مالی و E2E چندشرکتی روی SQL Server واقعی — همه خارج از محدودهٔ این مرحله و در همین سند از قبل ثبت‌اند.

### رفتارهای کلیدی (خلاصه برای بازبینی)

- بستن تب Dirty → تأیید؛ Saving/Error → رد قطعی با دیالوگ رد (بدون کنش مخرب).
- تغییر زمینه: Dirty/Saving/Error → توقف + فهرست تب‌ها با پرش؛ همه Clean → تأیید نهایی خودکار → صفحهٔ انتخاب روی پوستهٔ inert (تب‌های global حفظ، وابسته‌ها پس از موفقیت بسته)؛ لغو در هر مرحله هیچ چیز را عوض نمی‌کند؛ شکست سرور کاربر را در زمینهٔ قبلی نگه می‌دارد.
- تغییر URL/Back-Forward گارد را دور نمی‌زند (زمینه در Cookie سرور است).
