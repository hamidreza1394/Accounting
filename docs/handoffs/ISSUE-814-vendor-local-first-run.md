# تحویل وظیفه — ISSUE-814-vendor-local-first-run

- **Issue:** [#814](https://github.com/hamidreza1394/Accounting/issues/814)
- **Agent/مسئول:** ChatGPT
- **تاریخ:** 2026-11-28
- **وضعیت:** PR پیش‌نویس #815 باز است؛ CI در حال اجرا/انتظار و اجرای عملیاتی هنوز تأیید نشده است

## هدف و معیارهای پذیرش
- [x] Visual Studio با یک launch URL به ریشه برنامه برود؛ در setup-only ریشه به فرم Setup هدایت شود و در full-host صفحه ورود نمایش داده شود.
- [x] مسیر ساخت اولین مدیر Vendor برای اپراتور قابل کشف باشد، بدون endpoint ثبت‌نام عمومی یا حساب seed.
- [x] فرمان bootstrap از اتصال ذخیره‌شده Windows Credential Manager همان کاربر استفاده کند، مگر اینکه Environment/Configuration صریح وجود داشته باشد.
- [x] پیام‌ها و promptهای کنسولی تغییرکرده ASCII/English باشند.
- [x] فرم setup راهنمای سرور/instance و هویت Runtime داشته باشد و پس از Provisioning گام بعدی را نشان دهد.
- [x] امنیت loopback، console challenge، Origin، antiforgery، rate limiting و هویت Runtime کم‌اختیار حفظ شود.
- [x] راهنمای Visual Studio/PowerShell، وضعیت‌های Setup/Full Host و bootstrap به‌روزرسانی شود.
- [ ] Build، تست‌های .NET/Node و بررسی CI نهایی موفق شوند.
- [ ] اجرای واقعی Windows/Visual Studio/SQL Server و مرورگر پس از دریافت تغییرات روی دستگاه مالک انجام شود.

## خلاصه تغییرات
- launch profile اکنون `/` را باز می‌کند؛ setup-only mode خودش ریشه را به فرم setup هدایت می‌کند و full-host mode صفحه اصلی Vendor را می‌دهد.
- فرمان `bootstrap-admin` در نبود تنظیم صریح اتصال، اتصال Runtime را از Windows Credential Manager کاربر جاری می‌خواند. اگر هیچ اتصالی موجود نباشد، پیام روشن ASCII چاپ و فرمان با exit code 2 متوقف می‌شود.
- promptها و پیام‌های console در Program و bootstrap به انگلیسی ASCII تغییر کرده‌اند.
- صفحه ورود Vendor یک بخش بازشونده برای راهنمای ایجاد اولین مدیر دارد؛ همچنین چون API قبلاً فقط `FindByNameAsync` می‌کرد در حالی که UI «نام کاربری یا ایمیل» می‌گفت، fallback امن `FindByEmailAsync` با Normalized Email اضافه و تست HTTP آن با ایمیل واقعی حساب پوشش داده شد. این راهنما نام کاربری، ایمیل، گذرواژه و TOTP را در ترمینال تعاملی می‌گیرد و هیچ API ثبت‌نام عمومی اضافه نمی‌کند.
- فرم Database Setup راهنمای SQL Server instance، انتخاب دیتابیس، مجوز Provisioning و تفاوت حساب موقت Provisioning با Runtime را واضح‌تر می‌کند. بعد از موفقیت Provisioning، دستور ساخت اولین مدیر را نمایش می‌دهد.
- CSS فرم setup با قواعد محلی و واکنش‌گرا بهبود یافته است؛ UI ورود در Vendor.Web از Tailwind محلی موجود استفاده می‌کند و هیچ CDN/dependency جدیدی اضافه نشده است.
- تست‌های رگرسیون برای launch URL و ASCII-only بودن خطوط کنسولی اضافه شده‌اند؛ تست UI برای نمایش راهنمای bootstrap اضافه شده است.

## فایل‌های مهم تغییرکرده
- `Vendor/Vendor.Host/Properties/launchSettings.json`
- `Vendor/Vendor.Host/Program.cs`
- `Vendor/Vendor.Host/Authentication/VendorAuthenticationEndpoints.cs` — پشتیبانی واقعی از ورود با ایمیل مطابق برچسب UI.
- `Vendor/Vendor.Host/Bootstrap/VendorBootstrapCommand.cs`
- `Vendor/Vendor.Host/Development/StaticFiles/index.html`
- `Vendor/Vendor.Host/Development/StaticFiles/setup.js`
- `Vendor/Vendor.Host/Development/StaticFiles/setup.css`
- `Vendor/Vendor.Web/src/App.tsx`
- `Vendor/Vendor.Web/src/App.test.tsx`
- `tests/Vendor.Host.Tests/VendorLocalStartupConfigurationTests.cs`
- `Vendor/README.md`
- `docs/VENDOR_LOCAL_REMOTE_DEPLOYMENT_READINESS.md`
- `docs/WORK_LOG.md`
- `docs/PROJECT_STATUS.md`
- `docs/INDEX.md`

## تصمیم‌های فنی
- هیچ endpoint HTTP برای bootstrap مدیر اضافه نشد؛ مسیر موجود command-mode تعاملی حفظ شد.
- هیچ نام کاربری، ایمیل، رمز یا راز پیش‌فرض/seed اضافه نشد.
- اتصال Credential Manager فقط fallback است؛ Environment/Configuration صریح بر آن مقدم می‌ماند.
- هیچ Migration یا Schema تغییر نکرده و دیتابیس Customer لمس نشده است.
- Tailwind از pipeline محلی Vendor.Web استفاده می‌کند؛ صفحه embedded Setup به‌دلیل خارج بودن از pipeline Tailwind با CSS محلی خود Host بهبود یافته است.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `dotnet build Vendor.sln -c Release` | اجرا نشده | محیط فعلی به SDK/repository working tree محلی دسترسی اجرایی ندارد؛ CI لازم است |
| `dotnet test Vendor.sln -c Release` | اجرا نشده | CI لازم است |
| `cd Vendor/Vendor.Web && npm ci && npm run typecheck && npm test && npm run build` | اجرا نشده | CI لازم است |
| `node --input-type=module --check < Vendor/Vendor.Host/Development/StaticFiles/setup.js` | اجرا نشده | CI لازم است |
| `node --test tests/vendor-ui/setup-state.test.mjs` | اجرا نشده | CI لازم است |
| `git diff --check` | اجرا نشده | تغییرات از طریق GitHub Contents API ثبت شده‌اند؛ بازبینی diff و CI هنوز لازم است |
| اجرای واقعی Windows Credential Manager/SQL Server/Visual Studio | اجرا نشده | فقط کاربر می‌تواند در دستگاه هدف تأیید کند |

## امنیت، داده و سازگاری
- loopback، console challenge، Origin، antiforgery، rate limit و runtime least-privilege تغییر سیاست نداده‌اند.
- رمز و راز فقط به‌صورت ورودی مخفی/Environment هستند و در Git یا Log اضافه نشده‌اند.
- bootstrap همچنان به TOTP واقعی و فعال‌سازی پس از تأیید وابسته است.
- هیچ Customer DB/Migration یا Vendor schema/Migration تغییر نکرده است.

## ریسک‌ها و کارهای باقی‌مانده
- لازم است CI روی PR بررسی شود؛ در صورت شکست، اصلاح قبل از اعلام آمادگی انجام شود.
- باید اجرای عملیاتی روی Windows و SQL Server واقعی مالک تأیید شود.
- متن‌های موجود کنسول در ابزارهای دیگر Vendor خارج از دو فایل تغییرکرده به‌طور کامل ممیزی نشده‌اند؛ تست جدید Program و bootstrap را پوشش می‌دهد.
- مرورگر واقعی و screenshot پس از Build جدید Vendor.Web هنوز بررسی نشده است.

## شناسه‌های Git
- Branch: `fix/issue-814-vendor-local-first-run`
- Commit: ثبت‌شده در تاریخچه شاخه
- Pull Request: [#815](https://github.com/hamidreza1394/Accounting/pull/815) (Draft)
- Head SHA هنگام ایجاد PR: `9e35a1521c33c14f2d925ce2e30ecc1f09a05345`; آخرین commit کد پس از اصلاح ورود با ایمیل: `1994da6e9b74527d7783089d451a2387c68f222e`
