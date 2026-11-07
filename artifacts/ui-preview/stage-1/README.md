# پیش‌نمایش واقعی رابط کاربری — Stage 1 Issue #444 (پوسته فرانو، منوها و Workspace چندتب)

این پوشه شامل **اسکرین‌شات‌های واقعی مرورگر** از نسخه واقعاً اجرا‌شدهٔ هر شاخه PR است — هیچ تصویر Mockup یا تولیدی وجود ندارد.

## محیط اجرا (واقعی، نه شبیه‌سازی)

- **سرور:** پابلیش Release واقعی (`scripts/publish.sh` معادل: `dotnet publish Server/Server.csproj -c Release`) از کد همان شاخه، اجرا با `ASPNETCORE_ENVIRONMENT=Production` روی پورت 5090.
- **دیتابیس:** SQL Server 2022 واقعی (نصب‌شده در Sandbox بدون Docker) + Migration واقعی اسکیمای سرور (`dotnet Server.dll --migrate`) + Seed واقعی حساب‌ها/شرکت‌ها با `tests/e2e/seed.mjs` مخزن.
- **مرورگر:** Chromium واقعی Playwright (نسخه مخزن در `tests/e2e`).
- **ورود:** با حساب آزمایشی واقعی مدیر نصب ساخته‌شده توسط Seed (احراز هویت کامل Cookie + Anti-forgery؛ هیچ امنیتی غیرفعال نشده است).
- **انتخاب شرکت/دوره:** طبق D-031 (صفحه انتخاب سال مالی → شرکت) با حسابی که عضو دو شرکت است.
- **تاریخ اجرا:** 2026-11-07.

## جدول تصاویر

| پوشه | شاخه (Branch) | آخرین Commit SHA | محتوا |
|---|---|---|---|
| `pr-566/` | `fix/hr-installation-admin-access` | `17f55b26d04b` | دکمه‌های ایجاد HR برای مدیر نصب روی شاخه خود PR (پوسته پیشین main) |
| `pr-567/` | `fix/user-detail-back-nav` | `8d8b99da47c3` | بازگشت پرونده کاربر + حفظ فیلتر جست‌وجو |
| `pr-568/` | `feat/ui-app-shell` | `f706c791c956` | پوسته فرانو (Header سرمه‌ای، Sidebar راست، Breadcrumb) بدون نوار تب |
| `pr-569/` | `feat/ui-workspace-tabs` | `5e460f4d3a05` | نوار تب‌های کاری + سناریوهای باز/بستن/جلوگیری از تکرار |
| `pr-570/` | `feat/ui-dirty-form-protection` | `88c24114a551` | هشدار فرم تغییرکرده هنگام بستن تب (ConfirmDialog) |
| `pr-571/` | `fix/ui-final-polish` | `7b754e723a89` | مجموعه کامل روی سرِ Stack (یکپارچه‌سازی نهایی) |
| `pr-integration/` | *ادغام محلی* `fix/ui-final-polish` + `fix/hr-installation-admin-access` | `7b754e72` + `17f55b26` (ادغام آزمایشی محلی `6fd427d1`، Push نشده) | اعتبارسنجی ترتیب Merge پیشنهادی: هر ۱۲ سناریو PASS |

نتایج خام سناریوها: `results-pr-*.json` (کنار همین README).

> **نکته مخزن:** پوشهٔ `artifacts/` در `.gitignore` مخزن است (خروجی publish). به همین دلیل آرشیو کامل تصاویر روی شاخهٔ اختصاصی `artifacts/stage-1-ui-preview` (با `git add -f`، بر پایهٔ `main`) نگه‌داری می‌شود و فقط یک زیرمجموعهٔ کلیدی (`pr-integration` و `key-shots/`) در PR #571 قرار گرفته است. هیچ اطلاعات محرمانه‌ای در تصاویر نیست (حساب‌های آزمایشی e2e.test و نام شرکت‌های آزمایشی Seed).

### زیرمجموعه کلیدی در PR #571 (`key-shots/`)

`shell-dashboard-1440x900` · `sidebar-expanded-1440` · `sidebar-collapsed-with-tooltip-1440` · `users-list-1440x900` · `user-detail-with-back-1440x900` · `hr-units-list-1440x900` · `hr-unit-create-1440x900` · `dirty-form-close-dialog-1440` · `dashboard-360x800`

## فهرست تصاویر (نام صفحه × اندازه نمایشگر)

### pr-integration (نسخه یکپارچه نهایی — مرجع بازبینی)
| فایل | صفحه | اندازه |
|---|---|---|
| `shell-dashboard-1440x900.png` | داشبورد — Header، Sidebar، نوار تب، Breadcrumb | 1440×900 |
| `sidebar-expanded-1440.png` / `sidebar-hover-1440.png` | Sidebar باز / حالت Hover | 1440×900 |
| `sidebar-collapsed-with-tooltip-1440.png` | Sidebar جمع‌شده (Tooltip بومی مرورگر با `title` است و در اسکرین‌شات ظاهر نمی‌شود) | 1440×900 |
| `users-list-1440x900.png` | فهرست کاربران (دکمه «ایجاد کاربر») | 1440×900 |
| `users-list-state-preserved-1440.png` | حفظ `q=alice` پس از جابه‌جایی بین تب‌ها | 1440×900 |
| `user-detail-with-back-1440x900.png` | پرونده کاربر — دکمه بازگشت در Ready | 1440×900 |
| `user-detail-two-records-tabs-1440.png` | دو رکورد متفاوت = دو تب مجزا | 1440×900 |
| `hr-units-list-1440x900.png` | واحدهای سازمانی + دکمه «واحد جدید» | 1440×900 |
| `hr-unit-create-1440x900.png` | تب «ایجاد واحد سازمانی» | 1440×900 |
| `hr-positions-list-1440x900.png` | سمت‌های سازمانی | 1440×900 |
| `hr-employees-list-1440x900.png` | کارکنان | 1440×900 |
| `hr-employee-create-1440x900.png` | ثبت کارمند | 1440×900 |
| `dirty-form-close-dialog-1440.png` | ConfirmDialog «بستن تب با تغییرات ذخیره‌نشده» | 1440×900 |
| `dirty-form-logout-dialog-1440.png` | هشدار خروج با فرم Dirty | 1440×900 |
| `work-context-selector-1440.png` | صفحه انتخاب شرکت/دوره (D-031) | 1440×900 |
| `after-company-switch-1440.png` | پس از تغییر زمینه کاری | 1440×900 |
| `dashboard-360x800.png` … `dashboard-1440x900.png` | داشبورد در ۵ اندازه | 360/768/1024/1280/1440 |
| `users-list-360x800.png` … `users-list-1440x900.png` | فهرست کاربران در ۵ اندازه | 360/768/1024/1280/1440 |

پوشه‌های هر PR به همین الگو، فقط صفحات مرتبط با همان PR را دارند.

## خلاصه نتایج سناریوهای واقعی مرورگر

- **نسخه یکپارچه (`pr-integration`)**: هر ۱۲ سناریو **PASS** — باز شدن تب، تب جدید بدون بستن قبلی، حفظ وضعیت/Query، جلوگیری از تب تکراری، دو رکورد = دو تب، بستن فعال/غیرفعال با جانشین درست، هشدار Dirty هنگام بستن تب/خروج/تغییر شرکت (لغو = حفظ فرم)، Back/Forward مرورگر، عدم نشت داده Context قبلی.
- **روی سرِ Stack به‌تنهایی (`pr-571`)**: ۱۱/۱۲ PASS؛ تنها «دکمه ایجاد واحد سازمانی» FAIL است چون فیکس PR #566 روی شاخه‌های 568–571 وجود ندارد (PR مستقل main-محور). پس از ادغام محلی PR #566 با سرِ Stack، همان سناریو **PASS** شد (پوشه `pr-integration`). **نتیجه: ترتیب Merge مهم است — PR #566 باید قبل یا هم‌زمان با Stack ادغام شود.**
- خطاهای Console: **صفر** در همه اجراها (تنها یک `429` در ناوبری سریعِ خودکار، رفتار حفاظتی Rate-limit سرور).
