# گزارش کار و تحویل Agentها

> گزارش‌ها را به انتهای فایل اضافه کن؛ سابقه قبلی را حذف یا بازنویسی نکن.

## 2026-10-16 — Baseline review / scaffold
- **نوع کار:** بررسی ایستا و آماده‌سازی اسکلت.
- **یافته‌ها:** همه پروژه‌های اصلی روی `net6.0` بودند؛ NuGetها نسخه‌های قدیمی EF Core/Google Auth داشتند؛ Solution پروژه StateMachine را دربر نمی‌گرفت؛ تست پروژه‌ای وجود نداشت؛ `.editorconfig` چند هشدار Nullable را خاموش می‌کرد؛ appsettings حاوی MasterPassword hash و ActivationKeys بود؛ `Program.cs` محیط را براساس Debugger بازنویسی می‌کرد؛ PageModel ورود دارای نام کاربری/Claims hardcode بود؛ یک مدیر نمونه با اطلاعات تماس و رمز عبور شناخته‌شده نیز در EF seed شده بود.
- **تغییرات:** هدف‌گذاری `net9.0`، ایجاد پروژه Application، انتقال پروژه Data به `Infrastructure/Persistence` (با حفظ namespace فعلی)، Central Package Management با نسخه 9.0.21 برای بسته‌های Microsoft، حذف `System.Drawing.Common` بلااستفاده، تنظیم Build/Analyzer، تست پایه xUnit، CI و مستندات معماری/امنیت/Agent.
- **امنیت:** مقادیر MasterPassword/ActivationKeys حذف شدند؛ Login username به تنظیمات منتقل شد؛ Seed مدیر نمونه حذف شد؛ فعال‌سازی در Development غیرفعال و در محیط‌های غیر Development اجباری باقی ماند.
- **تست:** Build/Test اجرا نشد؛ محیط فعلی `dotnet` ندارد. JSON/XML و ساختار فایل‌ها باید با CI واقعی تأیید شوند.
- **کار باقی‌مانده:** اجرای CI، بررسی خطاهای کامپایل، مهاجرت به .NET 10 LTS، جایگزینی Authentication legacy، جداسازی Application/Infrastructure، افزودن تست‌های واقعی و بررسی به‌روزرسانی frontend.


## 2026-10-16 — Repository audit and agent workflow hardening
- **دامنه:** بررسی ایستای ZIP net9-baseline؛ 12 فایل پروژه `.csproj` و 199 فایل C#.
- **اصلاحات:** تکمیل AGENTS.md، افزودن Issue templates و handoff template، کنترل CDN در CI، جلوگیری از Open Redirect پس از ورود، تنظیم امنیت Cookie، اصلاح Middleware قدیمی و توسعه مستندات.
- **گزارش تفصیلی:** `docs/REPOSITORY_AUDIT.md` و `docs/CLASS_REVIEW_INVENTORY.csv`.
- **تست:** `dotnet` در محیط بررسی موجود نبود؛ Restore/Build/Test و ارتقای واقعی assetها اجرا نشدند.


## 2026-10-16 — Multi-agent coordination documentation
- **نوع کار:** تکمیل مستندات گردش کار Agentها؛ بدون تغییر کد اجرایی.
- **تغییرات:** مستندات هماهنگی، پرامپت‌های Agent، قالب Handoff و Issue workflow اضافه یا تکمیل شدند.
- **وضعیت Git:** تغییرات مستنداتی قبلی روی شاخه مستقل ثبت شدند؛ Merge به main انجام نشده است.


## 2026-10-16 — Issue #7 secure localized login page
- **نوع کار:** بازطراحی محدود صفحه ورود و ثبت ریسک‌های احراز هویت.
- **شاخه:** `agent/UNNUMBERED-secure-login-page`
- **تغییرات:** صفحه ورود دو ستونه واکنش‌گرا با Bootstrap/Bootstrap Icons محلی، رنگ سرمه‌ای/فیروزه‌ای، فرم واقعی Razor Pages، نمایش/مخفی‌سازی رمز عبور، Loading، دسترس‌پذیری پایه، تغییر زبان موجود و حذف لینک‌های غیرفعال OAuth/بازیابی.
- **امنیت:** ReturnUrl محلی و Cookie/Remember Me/Logout موجود حفظ شدند؛ SHA-256 خام و کاربر مرکزی به‌عنوان ریسک P0 باقی ماندند و جایگزینی بدون User Store و طرح مهاجرت اجرا نشد.
- **داده:** هیچ Schema یا Migration تغییر نکرد.
- **اعتبارسنجی:** Build، Test، اسکریپت دارایی‌ها، `git diff --check` و مرورگر در محیط متصل اجرا نشدند؛ باید در CI/محیط دارای SDK اجرا شوند.
- **تحویل:** handoff در `docs/handoffs/ISSUE-UNNUMBERED-secure-login-page.md` ثبت شد. Merge به `main` انجام نشده است.


## 2026-10-16 — Issue #7 CI follow-up
- **یافته CI:** Build موفق بود، اما `dotnet test` در مسیر VSTest با SDK جدیدتر و Microsoft.Testing.Platform شکست خورد.
- **تصمیم:** به‌جای ارتقای پکیج‌ها یا تغییر معماری، `global.json` با SDK سازگار `9.0.100` اضافه شد تا پروژه net9.0 و پکیج‌های موجود روی مسیر پایدار باقی بمانند.
- **UI:** تکرار عنوان در بخش برند حذف شد؛ نام تجاری واقعی در منابع/تنظیمات یافت نشد و نام جدیدی اضافه نشد.
- **JavaScript:** handler عمومی فرم حذف و به `.login-form` محدود شد؛ ارسال با Enter، اعتبارسنجی موجود و Loading حفظ شدند؛ TinyMCE با guard باقی ماند.
- **وضعیت:** اجرای CI جدید روی commit `b54ac4214a6ae54fe297682791445ce465ab0894` در زمان ثبت این گزارش Pending بود؛ نتیجه نهایی هنوز تأیید نشده است.
- **ریسک‌های باز:** SHA-256 خام، نبود Rate Limiting/Account Lockout و نبود Integration Test واقعی همچنان رفع‌نشده هستند.
