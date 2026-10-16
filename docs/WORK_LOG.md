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
- **دامنه:** بررسی ایستای ZIP net9-baseline؛ 12 csproj، 199 فایل C#، 76 فایل Razor و 1 فایل تست.
- **اصلاحات:** تکمیل AGENTS.md، افزودن Issue templates و handoff template، کنترل CDN در CI، جلوگیری از Open Redirect پس از ورود، تنظیم امنیت Cookie، اصلاح Middleware قدیمی برای عدم بلعیدن Exception و توسعه مستندات.
- **گزارش تفصیلی:** `docs/REPOSITORY_AUDIT.md` و فهرست `docs/CLASS_REVIEW_INVENTORY.csv`.
- **تست:** `dotnet` در محیط موجود نیست و دانلود شبکه‌ای در دسترس نبود؛ Restore/Build/Test و ارتقای واقعی فایل‌های Bootstrap/Icons اجرا نشدند. هیچ نتیجه‌ای به‌عنوان Build موفق اعلام نمی‌شود.
- **باقی‌مانده:** احراز هویت legacy، ارتقای واقعی assetهای Bootstrap/Icons، تست‌های Integration و تصمیم .NET 10 LTS از اولویت‌های بعدی‌اند.


## 2026-10-16 — Multi-agent coordination documentation
- **نوع کار:** تکمیل مستندات گردش کار Agentها طبق درخواست کاربر؛ بدون تغییر کد اجرایی.
- **تغییرات:** افزوده شد: docs/AGENT_COORDINATION.md، پرامپ اختصاصی Codex در docs/prompts/CODEX_PROMPT.md، پرامپ اختصاصی Copilot در docs/prompts/COPILOT_PROMPT.md، و گزارش docs/handoffs/ISSUE-UNNUMBERED-agent-workflow-hardening.md. فایل‌های AGENTS.md و docs/AGENT_START_PROMPT.md نیز به این گردش کار پیوند داده شدند.
- **گردش کار پیشنهادی:** هر وظیفه یک Issue، شاخه و PR مستقل؛ handoff در Git ثبت شود؛ ادغام فقط پس از بازبینی Maintainer.
- **تست:** Restore/Build/Test و اسکریپت دارایی‌ها اجرا نشده‌اند؛ این تغییر مستنداتی است و اجرای محلی/git diff --check از این مسیر ممکن نبود. هیچ ادعایی درباره موفقیت Build/Test وجود ندارد.
- **وضعیت Git:** تغییرات روی شاخه docs/agent-workflow-hardening ثبت شده‌اند؛ main تغییر نکرده است. PR هنوز ساخته نشده است.
