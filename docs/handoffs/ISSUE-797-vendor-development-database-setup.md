# تحویل Issue #797 — فرم تنظیم اتصال Vendor در Development

- **تاریخ:** 2026-11-26
- **شاخه:** `agent/797-vendor-dev-db-setup`
- **مبنای شاخه:** ابتدا `5cb7ec7` (شاخهٔ PR #796)؛ پس از Merge شدن #796، روی `main@4a8a028` rebase شد و base PR #800 به `main` تغییر کرد.
- **وضعیت:** پیاده‌سازی و بازبینی شد؛ نتیجهٔ CI روی head نهایی در PR #800 ثبت می‌شود.

## تغییرها

- پروفایل Visual Studio برای `Vendor.Host`، Development و URL محلی HTTPS را مشخص می‌کند و صفحه setup را باز می‌کند.
- وقتی ConnectionStrings:VendorDb پیکربندی نشده و credential ذخیره‌شده نیز وجود ندارد، Host فقط صفحه setup را بالا می‌آورد و به DB، Identity یا audit startup service دسترسی نمی‌گیرد.
- فرم فارسی RTL Windows Integrated و SQL Login را می‌پذیرد، TLS را فعال می‌کند، اتصال را با `DB_NAME()` می‌آزماید و سپس در Windows Credential Manager کاربر فعلی ذخیره می‌کند.
- در شروع بعدی Development، مقدار ذخیره‌شده خوانده می‌شود. مقدار Environment/Configuration اولویت دارد؛ صفحه منبع را نشان می‌دهد و دکمه‌های ذخیره/حذف را غیرفعال می‌کند.
- endpointها loopback-only، antiforgery/Origin-validated و rate-limited هستند؛ رمز و connection string در پاسخ، رابط یا log ثبت نمی‌شوند.
- هیچ دیتابیس یا Migration ساخته/اجرا نمی‌شود؛ Production و Customer دست‌نخورده‌اند.

## بررسی

- منبع کد و جریان آغاز Host از branch پایه بررسی شد.
- checkout محلی در این نشست وجود ندارد؛ `dotnet restore`, `dotnet build`, `dotnet test` و اجرای runtime/SQL انجام نشدند.
- CI تا زمان ایجاد PR اجرا نشده است؛ وضعیت Build/Test **Not yet Verified** است.
- پس از PR، نتیجهٔ Vendor CI، .NET CI، Semgrep و diff/publish guards باید ثبت شود. اجرای واقعی در Visual Studio برای اطمینان از dev certificate و دسترسی SQL به محیط مالک نیاز دارد.

## محدوده

فقط Vendor.Host و اسناد آن. بدون تغییر Customer projects، دیتابیس‌ها، Migrationها، secrets، gateها، نصب سرویس یا عملیات Production.

## بازبینی پیش از Merge (Claude، 2026-11-26)

بازبینی مستقل: `security-reviewer` (opus، فقط‌خواندنی) روی head `f6c1d3e`. یافتهٔ blocking نداشت؛ مسیری برای فعال‌شدن در Production یا بدون flag پیدا نشد، نشت رمز/connection string پیدا نشد و P/Invoke درست ارزیابی شد. یافته‌های غیرمسدودکننده و اصلاح‌ها:

| یافته | شدت | اصلاح |
|---|---|---|
| M1: در حالت برنامهٔ کامل، test/save/delete بدون احراز هویت Vendor بودند؛ هر پروسهٔ محلی غیرمرورگری می‌توانست مقصد DB را عوض کند یا NTLM را به سرور دلخواه بفرستد | Medium | `AllowAnonymous` فقط در حالت setup-only؛ در حالت کامل همهٔ endpointهای setup پشت FallbackPolicy (مدیر MFA) هستند |
| M2: صفحه/دارایی‌ها در حالت کامل پشت FallbackPolicy و API ناشناس بود (ناهمسان) | Medium | با اصلاح M1 سیاست یکسان شد؛ README اصلاح شد |
| L1: نام میزبان فیلتر نمی‌شد (tunnel/rebinding) | Low | peer و Host هر دو باید loopback باشند (`localhost`/`127.0.0.1`/`[::1]`) وگرنه 404 |
| L2: هدرهای no-store روی همهٔ پاسخ‌های برنامهٔ کامل | Low | هدرها فقط روی درخواست‌های setup |
| L3: نبود ضد framing/CSP | Low | `X-Frame-Options: DENY` و CSP `default-src 'self'; frame-ancestors 'none'` روی setup |
| L4: `catch` خالی بدون log (خلاف AGENTS.md بخش ۲) | Low | `catch` محدود به `Win32Exception`/`InvalidOperationException` با `ILogger`، بدون مقدار credential |
| L5: نبود تست | Low | `VendorDevelopmentDatabaseSetupExposureTests` (۲ تست) |

## آزمون‌های اجراشده (Claude، Linux، SDK 10.0.404)

| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| `dotnet build Vendor.sln -c Release` | موفق | 0 خطا |
| `dotnet test Vendor.sln --no-build -c Release` با connection string ساختگی و غیرقابل‌دسترس | موفق | total 469، succeeded 387، skipped 82، failed 0؛ تست‌های SQL Server واقعی skip شدند |
| `VendorDevelopmentDatabaseSetupExposureTests` | موفق | روی Linux مسیر «غیر Windows» و «بدون flag» را می‌سنجد؛ روی Windows همان تست guard loopback را می‌سنجد |

اجرانشده: اجرای واقعی Visual Studio روی Windows، Windows Credential Manager، گواهی HTTPS توسعه و اتصال SQL واقعی؛ این‌ها به سیستم مالک نیاز دارند.


## الحاقیهٔ 2026-11-27 — Merge PR #800 و CI پس از Merge

- Issue #797 با PR [#800](https://github.com/hamidreza1394/Accounting/pull/800) در 2026-11-26T17:41:51Z تکمیل و بسته شد.
- HEAD نهایی PR: `9586e27d92413c371c35ca48b57d2c42094a95f5`؛ merge commit و SHA فعلی main: `110d797fe7a3a40466c7ff3aa520cfa73ff22536`.
- Checkهای نهایی روی HEAD PR موفق‌اند: .NET CI #45867980923، Vendor CI #45867980991، Semgrep #45867980980 و CodeQL #45867977087. اجراهای قبلی لغوشده مربوط به تلاش‌های جایگزین‌شده‌اند؛ روی HEAD نهایی شکست نهایی ثبت نشده است.
- CI پس از Merge روی main برابر ۱۶/۱۶ Check Run موفق است: .NET CI #45869907350 (۷ Job)، Vendor CI #45869907458 (۴ Job)، Semgrep #45869907173 (۱ Job) و Push on main/CodeQL #45869907193 (۴ تحلیل). شامل Build/Test، E2E، Publish Linux/Windows، Secret Scan، Client، CNG signer و checkpoint file-lock regressions است.
- فرم محلی DB setup فقط در Development ویندوز با flag صریح و loopback فعال است؛ setup-only فاقد Persistence/Identity است؛ اتصال پیش از ذخیره آزمایش و در Credential Manager همان کاربر نگهداری می‌شود. اولویت Environment/Configuration حفظ شده؛ ابزار DB نمی‌سازد و Migration اجرا نمی‌کند.
- Issue #797 بسته است؛ Issue #768 برای اجرای واقعی Visual Studio، dev certificate، Windows Credential Manager، اتصال Vendor DB، نصب Windows Service، هویت/ACL و Staging همچنان باز است.
- **محدودیت عملیاتی:** Build/Test محلی این ممیزی اجرا نشد. handoff قبلی نتایج محلی Linux/SDK 10.0.404 را ثبت کرده است (Build موفق؛ Vendor test 469 مورد، 387 موفق، 82 Skip، صفر شکست؛ تست SQL Server واقعی در آن اجرای محلی Skip بود). این شواهد جایگزین اجرای واقعی Windows/SQL روی دستگاه مالک نیست.
- **مرز امنیتی:** هیچ Customer consumer، Customer schema/Migration، `Server/**`، connection string، رمز یا کلید خصوصی تغییر نکرد؛ Provisioning v5 و Runtime Gate باز نشده‌اند.
