# پیش‌نمایش واقعی رابط کاربری — Stage 1 Issue #444 (پوسته فرانو، منوها و Workspace چندتب)

این پوشه شامل **اسکرین‌شات‌های واقعی مرورگر** از نسخه واقعاً اجرا‌شدهٔ هر شاخه PR است — هیچ تصویر Mockup یا تولیدی وجود ندارد (تفکیک کامل در بخش «تصاویر واقعی در برابر Mockup»).

## محیط اجرا (واقعی، نه شبیه‌سازی)

- **سرور:** پابلیش Release واقعی (`scripts/publish.sh` معادل: `dotnet publish Server/Server.csproj -c Release`) از کد همان شاخه، اجرا با `ASPNETCORE_ENVIRONMENT=Production` روی پورت 5090.
- **دیتابیس:** SQL Server 2022 واقعی (نصب‌شده در Sandbox بدون Docker) + Migration واقعی اسکیمای سرور (`dotnet Server.dll --migrate`) + Seed واقعی حساب‌ها/شرکت‌ها با `tests/e2e/seed.mjs` مخزن.
- **مرورگر:** Chromium واقعی Playwright (نسخه مخزن در `tests/e2e`).
- **ورود:** با حساب آزمایشی واقعی مدیر نصب ساخته‌شده توسط Seed (احراز هویت کامل Cookie + Anti-forgery؛ هیچ امنیتی غیرفعال نشده است).
- **انتخاب شرکت/دوره:** طبق D-031 (صفحه انتخاب سال مالی → شرکت) با حسابی که عضو دو شرکت است.
- **تاریخ اجرا:** 2026-11-07 (آرشیو شاخه‌های PR) و 2026-11-07/08 (تأیید پس از ادغام روی main).

## دسترسی به تصاویر — لینک‌های مستقیم

آرشیو کامل تصاویر روی شاخهٔ اختصاصی **[`artifacts/stage-1-ui-preview`](https://github.com/hamidreza1394/Accounting/tree/artifacts/stage-1-ui-preview/artifacts/ui-preview/stage-1)** نگهداری می‌شود؛ آخرین Commit آن: `f25a0e5e` (شامل پوشهٔ تأیید پس از ادغام). زیرمجموعهٔ کلیدی (`key-shots/`) و نتایج نسخهٔ یکپارچه روی `main` هم موجود است.

### جدول پوشه‌ها (شاخه `artifacts/stage-1-ui-preview`)

| پوشه | لینک مستقیم | شاخه PR (Commit Head ادغام‌شده) | محتوا |
|---|---|---|---|
| `pr-566/` | [فهرست تصاویر](https://github.com/hamidreza1394/Accounting/tree/artifacts/stage-1-ui-preview/artifacts/ui-preview/stage-1/pr-566) | `fix/hr-installation-admin-access` (`17f55b26d04b`) | دکمه‌های ایجاد HR برای مدیر نصب روی شاخه خود PR (پوسته پیشین main) |
| `pr-567/` | [فهرست تصاویر](https://github.com/hamidreza1394/Accounting/tree/artifacts/stage-1-ui-preview/artifacts/ui-preview/stage-1/pr-567) | `fix/user-detail-back-nav` (`8d8b99da47c3`) | بازگشت پرونده کاربر + حفظ فیلتر جست‌وجو |
| `pr-568/` | [فهرست تصاویر](https://github.com/hamidreza1394/Accounting/tree/artifacts/stage-1-ui-preview/artifacts/ui-preview/stage-1/pr-568) | `feat/ui-app-shell` (`9ae1aaa6ecae`) | پوسته فرانو (Header سرمه‌ای، Sidebar راست، Breadcrumb) بدون نوار تب |
| `pr-569/` | [فهرست تصاویر](https://github.com/hamidreza1394/Accounting/tree/artifacts/stage-1-ui-preview/artifacts/ui-preview/stage-1/pr-569) | `feat/ui-workspace-tabs` (`5e460f4d3a05`) | نوار تب‌های کاری + سناریوهای باز/بستن/جلوگیری از تکرار |
| `pr-570/` | [فهرست تصاویر](https://github.com/hamidreza1394/Accounting/tree/artifacts/stage-1-ui-preview/artifacts/ui-preview/stage-1/pr-570) | `feat/ui-dirty-form-protection` (`88c24114a551`) | هشدار فرم تغییرکرده هنگام بستن تب (ConfirmDialog) |
| `pr-571/` | [فهرست تصاویر](https://github.com/hamidreza1394/Accounting/tree/artifacts/stage-1-ui-preview/artifacts/ui-preview/stage-1/pr-571) | `fix/ui-final-polish` (`33cddd7d0fcd`) | مجموعه کامل روی سرِ Stack (یکپارچه‌سازی نهایی پیش از ادغام) |
| `pr-integration/` | [فهرست تصاویر](https://github.com/hamidreza1394/Accounting/tree/artifacts/stage-1-ui-preview/artifacts/ui-preview/stage-1/pr-integration) | *ادغام آزمایشی محلی* `7b754e72` + `17f55b26` (`6fd427d1`، Push نشده) | اعتبارسنجی ترتیب Merge پیشنهادی: هر ۱۲ سناریو PASS — **نسخهٔ یکپارچهٔ آزموده‌شدهٔ پیش از ادغام** |
| `post-merge-main/` | [فهرست تصاویر](https://github.com/hamidreza1394/Accounting/tree/artifacts/stage-1-ui-preview/artifacts/ui-preview/stage-1/post-merge-main) | `main` پس از ادغام هر ۶ PR (`cb2ddd57bdff`) | تأیید بصری نسخهٔ نهایی ادغام‌شده — **نسخهٔ یکپارچهٔ نهایی** |

نتایج خام سناریوها (JSON):

- روی `main` این پوشه: [`results-pr-571.json`](results-pr-571.json) و [`results-pr-integration.json`](results-pr-integration.json)
- روی شاخهٔ آرشیو: [`results-pr-566.json`](https://github.com/hamidreza1394/Accounting/blob/artifacts/stage-1-ui-preview/artifacts/ui-preview/stage-1/results-pr-566.json) تا [`results-pr-570.json`](https://github.com/hamidreza1394/Accounting/blob/artifacts/stage-1-ui-preview/artifacts/ui-preview/stage-1/results-pr-570.json) و [`results-post-merge-main.json`](https://github.com/hamidreza1394/Accounting/blob/artifacts/stage-1-ui-preview/artifacts/ui-preview/stage-1/results-post-merge-main.json)

> **نکته مخزن:** پوشهٔ `artifacts/` در `.gitignore` مخزن است (خروجی publish). به همین دلیل آرشیو کامل تصاویر روی شاخهٔ اختصاصی `artifacts/stage-1-ui-preview` (با `git add -f`، بر پایهٔ `main`) نگه‌داری می‌شود و فقط یک زیرمجموعهٔ کلیدی (`pr-integration` و `key-shots/`) در PR #571 روی `main` قرار گرفته است. هیچ اطلاعات محرمانه‌ای در تصاویر نیست (حساب‌های آزمایشی e2e.test و نام شرکت‌های آزمایشی Seed).

### زیرمجموعه کلیدی روی `main` (`key-shots/`)

| تصویر | موضوع |
|---|---|
| [`shell-dashboard-1440x900.png`](https://github.com/hamidreza1394/Accounting/blob/main/artifacts/ui-preview/stage-1/key-shots/shell-dashboard-1440x900.png) | داشبورد کامل با Header سرمه‌ای، Sidebar، نوار تب و Breadcrumb |
| [`sidebar-expanded-1440.png`](https://github.com/hamidreza1394/Accounting/blob/main/artifacts/ui-preview/stage-1/key-shots/sidebar-expanded-1440.png) | Sidebar باز با گروه‌های فارسی |
| [`sidebar-collapsed-with-tooltip-1440.png`](https://github.com/hamidreza1394/Accounting/blob/main/artifacts/ui-preview/stage-1/key-shots/sidebar-collapsed-with-tooltip-1440.png) | Sidebar جمع‌شده (Rail) |
| [`users-list-1440x900.png`](https://github.com/hamidreza1394/Accounting/blob/main/artifacts/ui-preview/stage-1/key-shots/users-list-1440x900.png) | فهرست کاربران با دکمه «ایجاد کاربر» |
| [`user-detail-with-back-1440x900.png`](https://github.com/hamidreza1394/Accounting/blob/main/artifacts/ui-preview/stage-1/key-shots/user-detail-with-back-1440x900.png) | پرونده کاربر با دکمه بازگشت در حالت Ready |
| [`hr-units-list-1440x900.png`](https://github.com/hamidreza1394/Accounting/blob/main/artifacts/ui-preview/stage-1/key-shots/hr-units-list-1440x900.png) | واحدهای سازمانی + دکمه «واحد جدید» (مدیر نصب) |
| [`hr-unit-create-1440x900.png`](https://github.com/hamidreza1394/Accounting/blob/main/artifacts/ui-preview/stage-1/key-shots/hr-unit-create-1440x900.png) | تب «ایجاد واحد سازمانی» |
| [`dirty-form-close-dialog-1440.png`](https://github.com/hamidreza1394/Accounting/blob/main/artifacts/ui-preview/stage-1/key-shots/dirty-form-close-dialog-1440.png) | ConfirmDialog «بستن تب با تغییرات ذخیره‌نشده» |
| [`dashboard-360x800.png`](https://github.com/hamidreza1394/Accounting/blob/main/artifacts/ui-preview/stage-1/key-shots/dashboard-360x800.png) | داشبورد در عرض موبایل 360 |

## فهرست تصاویر نسخهٔ یکپارچهٔ نهایی (`pr-integration` و `post-merge-main`)

این دو پوشه «نسخهٔ یکپارچه» را نشان می‌دهند: `pr-integration` ادغام آزمایشی محلی سرِ Stack + #566 (اعتبارسنجی ترتیب Merge پیش از ادغام) و `post-merge-main` نسخهٔ نهایی ادغام‌شدهٔ main. محتوای تصویری هر دو یکسان است؛ لینک‌های زیر به `pr-integration` هستند و برای `post-merge-main` فقط نام پوشه را در URL جایگزین کنید.

### داشبورد و پوسته

| تصویر | صفحه | اندازه |
|---|---|---|
| [`shell-dashboard-1440x900.png`](https://github.com/hamidreza1394/Accounting/blob/artifacts/stage-1-ui-preview/artifacts/ui-preview/stage-1/pr-integration/shell-dashboard-1440x900.png) | داشبورد — Header، Sidebar، نوار تب، Breadcrumb | 1440×900 |
| [`dashboard-360x800.png`](https://github.com/hamidreza1394/Accounting/blob/artifacts/stage-1-ui-preview/artifacts/ui-preview/stage-1/pr-integration/dashboard-360x800.png) | داشبورد | 360×800 |
| [`dashboard-768x1024.png`](https://github.com/hamidreza1394/Accounting/blob/artifacts/stage-1-ui-preview/artifacts/ui-preview/stage-1/pr-integration/dashboard-768x1024.png) | داشبورد | 768×1024 |
| [`dashboard-1024x768.png`](https://github.com/hamidreza1394/Accounting/blob/artifacts/stage-1-ui-preview/artifacts/ui-preview/stage-1/pr-integration/dashboard-1024x768.png) | داشبورد | 1024×768 |
| [`dashboard-1280x800.png`](https://github.com/hamidreza1394/Accounting/blob/artifacts/stage-1-ui-preview/artifacts/ui-preview/stage-1/pr-integration/dashboard-1280x800.png) | داشبورد | 1280×800 |
| [`dashboard-1440x900.png`](https://github.com/hamidreza1394/Accounting/blob/artifacts/stage-1-ui-preview/artifacts/ui-preview/stage-1/pr-integration/dashboard-1440x900.png) | داشبورد | 1440×900 |

### Sidebar

| تصویر | صفحه | اندازه |
|---|---|---|
| [`sidebar-expanded-1440.png`](https://github.com/hamidreza1394/Accounting/blob/artifacts/stage-1-ui-preview/artifacts/ui-preview/stage-1/pr-integration/sidebar-expanded-1440.png) | Sidebar باز | 1440×900 |
| [`sidebar-hover-1440.png`](https://github.com/hamidreza1394/Accounting/blob/artifacts/stage-1-ui-preview/artifacts/ui-preview/stage-1/pr-integration/sidebar-hover-1440.png) | Sidebar حالت Hover | 1440×900 |
| [`sidebar-collapsed-with-tooltip-1440.png`](https://github.com/hamidreza1394/Accounting/blob/artifacts/stage-1-ui-preview/artifacts/ui-preview/stage-1/pr-integration/sidebar-collapsed-with-tooltip-1440.png) | Sidebar جمع‌شده (Tooltip بومی مرورگر با `title` است و در اسکرین‌شات ظاهر نمی‌شود) | 1440×900 |

### مدیریت کاربران

| تصویر | صفحه | اندازه |
|---|---|---|
| [`users-list-360x800.png`](https://github.com/hamidreza1394/Accounting/blob/artifacts/stage-1-ui-preview/artifacts/ui-preview/stage-1/pr-integration/users-list-360x800.png) | فهرست کاربران | 360×800 |
| [`users-list-768x1024.png`](https://github.com/hamidreza1394/Accounting/blob/artifacts/stage-1-ui-preview/artifacts/ui-preview/stage-1/pr-integration/users-list-768x1024.png) | فهرست کاربران | 768×1024 |
| [`users-list-1024x768.png`](https://github.com/hamidreza1394/Accounting/blob/artifacts/stage-1-ui-preview/artifacts/ui-preview/stage-1/pr-integration/users-list-1024x768.png) | فهرست کاربران | 1024×768 |
| [`users-list-1280x800.png`](https://github.com/hamidreza1394/Accounting/blob/artifacts/stage-1-ui-preview/artifacts/ui-preview/stage-1/pr-integration/users-list-1280x800.png) | فهرست کاربران | 1280×800 |
| [`users-list-1440x900.png`](https://github.com/hamidreza1394/Accounting/blob/artifacts/stage-1-ui-preview/artifacts/ui-preview/stage-1/pr-integration/users-list-1440x900.png) | فهرست کاربران (دکمه «ایجاد کاربر») | 1440×900 |
| [`users-list-state-preserved-1440.png`](https://github.com/hamidreza1394/Accounting/blob/artifacts/stage-1-ui-preview/artifacts/ui-preview/stage-1/pr-integration/users-list-state-preserved-1440.png) | حفظ `q=alice` پس از جابه‌جایی بین تب‌ها | 1440×900 |
| [`user-detail-with-back-1440x900.png`](https://github.com/hamidreza1394/Accounting/blob/artifacts/stage-1-ui-preview/artifacts/ui-preview/stage-1/pr-integration/user-detail-with-back-1440x900.png) | پرونده کاربر — دکمه بازگشت در Ready | 1440×900 |
| [`user-detail-two-records-tabs-1440.png`](https://github.com/hamidreza1394/Accounting/blob/artifacts/stage-1-ui-preview/artifacts/ui-preview/stage-1/pr-integration/user-detail-two-records-tabs-1440.png) | دو رکورد متفاوت = دو تب مجزا | 1440×900 |

### منابع انسانی (HR)

| تصویر | صفحه | اندازه |
|---|---|---|
| [`hr-units-list-1440x900.png`](https://github.com/hamidreza1394/Accounting/blob/artifacts/stage-1-ui-preview/artifacts/ui-preview/stage-1/pr-integration/hr-units-list-1440x900.png) | واحدهای سازمانی + دکمه «واحد جدید» | 1440×900 |
| [`hr-unit-create-1440x900.png`](https://github.com/hamidreza1394/Accounting/blob/artifacts/stage-1-ui-preview/artifacts/ui-preview/stage-1/pr-integration/hr-unit-create-1440x900.png) | تب «ایجاد واحد سازمانی» | 1440×900 |
| [`hr-positions-list-1440x900.png`](https://github.com/hamidreza1394/Accounting/blob/artifacts/stage-1-ui-preview/artifacts/ui-preview/stage-1/pr-integration/hr-positions-list-1440x900.png) | سمت‌های سازمانی | 1440×900 |
| [`hr-employees-list-1440x900.png`](https://github.com/hamidreza1394/Accounting/blob/artifacts/stage-1-ui-preview/artifacts/ui-preview/stage-1/pr-integration/hr-employees-list-1440x900.png) | کارکنان | 1440×900 |
| [`hr-employee-create-1440x900.png`](https://github.com/hamidreza1394/Accounting/blob/artifacts/stage-1-ui-preview/artifacts/ui-preview/stage-1/pr-integration/hr-employee-create-1440x900.png) | ثبت کارمند | 1440×900 |

### فرم‌های Dirty و تغییر زمینه کاری

| تصویر | صفحه | اندازه |
|---|---|---|
| [`dirty-form-close-dialog-1440.png`](https://github.com/hamidreza1394/Accounting/blob/artifacts/stage-1-ui-preview/artifacts/ui-preview/stage-1/pr-integration/dirty-form-close-dialog-1440.png) | ConfirmDialog «بستن تب با تغییرات ذخیره‌نشده» | 1440×900 |
| [`dirty-form-logout-dialog-1440.png`](https://github.com/hamidreza1394/Accounting/blob/artifacts/stage-1-ui-preview/artifacts/ui-preview/stage-1/pr-integration/dirty-form-logout-dialog-1440.png) | هشدار خروج با فرم Dirty | 1440×900 |
| [`work-context-selector-1440.png`](https://github.com/hamidreza1394/Accounting/blob/artifacts/stage-1-ui-preview/artifacts/ui-preview/stage-1/pr-integration/work-context-selector-1440.png) | صفحه انتخاب شرکت/دوره (D-031) | 1440×900 |
| [`after-company-switch-1440.png`](https://github.com/hamidreza1394/Accounting/blob/artifacts/stage-1-ui-preview/artifacts/ui-preview/stage-1/pr-integration/after-company-switch-1440.png) | پس از تغییر زمینه کاری | 1440×900 |

پوشه‌های `pr-566/` تا `pr-571/` به همین الگو، فقط صفحات مرتبط با همان PR را دارند (جدول پوشه‌ها بالای سند).

## تصاویر واقعی در برابر Mockup

- **تمام تصاویر این آرشیو (همهٔ پوشه‌های بالا و `key-shots/`) اسکرین‌شات واقعی Chromium از برنامهٔ واقعاً اجراشده روی SQL Server واقعی و با احراز هویت کامل هستند**؛ هیچ تصویر Mockup، طراحی دستی یا تولیدی هوش مصنوعی در این آرشیو نیست.
- Mockupهای طراحی (بازطراحی صفحه ورود و پیش‌نمایش ثابت داشبورد از کار پیشین) فقط روی شاخهٔ مستقل [`ui/login-dashboard-preview`](https://github.com/hamidreza1394/Accounting/tree/ui/login-dashboard-preview) نگهداری می‌شوند و هیچ‌کدام در مستندات یا آرشیو Stage 1 استفاده یا ادغام نشده‌اند.
- پوشهٔ `pr-integration/stage-1_tmp/` کپی موقتی همان اجرای نسخهٔ یکپارچه است که در آرشیو حفظ شده؛ مرجع بازبینی، پوشهٔ والد `pr-integration/` است.

## محدودیت‌های پوشش تصویری

- سری کامل پنج اندازهٔ (360/768/1024/1280/1440) فقط برای **داشبورد** و **فهرست کاربران** تهیه شده است؛ سایر صفحات در 1440 (و چند مورد 360/768 طبق فهرست‌های بالا) پوشش داده شده‌اند.
- صفحات HR فقط در 1440 پوشش دارند (به‌جز `hr-units-list-360x800` در `pr-566`)؛ فرم‌های ویرایش HR تصویر ندارند.
- صفحات تب‌ها (`pr-569`) در 1440/1280/360 هستند؛ سناریوهای دو رکورد و بستن تب فقط در 1440.
- Tooltip حالت جمع‌شدهٔ Sidebar بومی مرورگر (`title`) است و در اسکرین‌شات ظاهر نمی‌شود؛ مشاهدهٔ آن فقط زنده ممکن است.
- خطاهای Console: صفر در همهٔ اجراها (تنها یک `429` در ناوبری سریع خودکار — رفتار حفاظتی Rate-limit سرور، طبق JSONهای نتایج).
- تصاویر شامل دادهٔ آزمایشی Seed (`e2e.test`) هستند و هیچ داده محرمانه‌ای ندارند؛ به همین دلیل نگهداری داخل مخزن عمومی مجاز تشخیص داده شد.

## خلاصه نتایج سناریوهای واقعی مرورگر

- **نسخه یکپارچه (`pr-integration`)**: هر ۱۲ سناریو **PASS** — باز شدن تب، تب جدید بدون بستن قبلی، حفظ وضعیت/Query، جلوگیری از تب تکراری، دو رکورد = دو تب، بستن فعال/غیرفعال با جانشین درست، هشدار Dirty هنگام بستن تب/خروج/تغییر شرکت (لغو = حفظ فرم)، Back/Forward مرورگر، عدم نشت داده Context قبلی.
- **نسخهٔ نهایی ادغام‌شدهٔ main (`post-merge-main`)**: همان مجموعه سناریوها روی main واقعی تأیید شد (نتایج: `results-post-merge-main.json` روی شاخهٔ آرشیو).
- **روی سرِ Stack به‌تنهایی (`pr-571`)**: ۱۱/۱۲ PASS؛ تنها «دکمه ایجاد واحد سازمانی» FAIL است چون فیکس PR #566 روی شاخه‌های 568–571 وجود ندارد (PR مستقل main-محور). پس از ادغام محلی PR #566 با سرِ Stack، همان سناریو **PASS** شد (پوشه `pr-integration`). **نتیجه: ترتیب Merge مهم است — PR #566 باید قبل یا هم‌زمان با Stack ادغام شود.** (پس از ادغام واقعی، این ترتیب رعایت شد و رگرسیون رخ نداد — به [`docs/handoffs/ISSUE-444-stage-1-workspace-shell.md`](../../../../docs/handoffs/ISSUE-444-stage-1-workspace-shell.md) مراجعه کنید.)
- خطاهای Console: **صفر** در همه اجراها (تنها یک `429` در ناوبری سریعِ خودکار، رفتار حفاظتی Rate-limit سرور).
