# تحویل وظیفه — ISSUE-85-faranou-engineering-skills

- **Issue:** [#85 — Harden and version-control Faranou engineering skills](https://github.com/hamidreza1394/Accounting/issues/85)
- **Agent/مسئول:** ChatGPT
- **تاریخ:** 2026-10-25
- **وضعیت:** تغییرات مستنداتی انجام شد؛ PR پیش‌نویس و منتظر بازبینی

## هدف و معیارهای پذیرش
- [x] همگام‌کردن Skill اصلی با .NET 10 / C# 14 و معماری موجود Razor Pages + React/API.
- [x] افزودن Skillهای تخصصی معماری، امنیت و چندشرکتی، تست، UI/UX فارسی RTL و دامنه حسابداری.
- [x] حفظ قواعد AGENTS.md، ADRها و کنترل تغییرات؛ عدم تغییر کد اجرایی یا وابستگی‌ها.
- [x] معرفی Skillها در فهرست مستندات.
- [ ] بازبینی نهایی PR و تأیید انسانی پیش از ادغام.

## خلاصه تغییرات
- Skill اصلی از راهنمای قدیمی .NET 9 به مبنای مستندشده فعلی .NET 10 / C# 14 به‌روزرسانی و نسخه قابل‌ردیابی آن در مخزن اضافه شد.
- پنج Skill تخصصی اضافه شد تا بازبینی معماری، امنیت/tenant isolation، QA، UI/UX و قواعد دامنه حسابداری را پوشش دهد.
- تمام Skillها موظف‌اند اسناد مرجع را بخوانند، وضعیت تصمیم/پیاده‌سازی/تأیید را تفکیک کنند و تست اجرا‌نشده را موفق گزارش نکنند.
- هیچ کد محصول، Schema، Migration، package manifest، lockfile یا Workflow تغییر نکرده است.

## فایل‌های مهم تغییرکرده
- `.claude/skills/faranou-senior-developer/SKILL.md` — Skill اصلی همگام با معماری فعلی.
- `.claude/skills/faranou-architecture-review/SKILL.md` — تحلیل معماری و ADR.
- `.claude/skills/faranou-security-multitenancy/SKILL.md` — امنیت و جداسازی شرکت‌ها.
- `.claude/skills/faranou-testing-quality/SKILL.md` — راهبرد تست و گزارش CI.
- `.claude/skills/faranou-ui-ux-design/SKILL.md` — طراحی RTL، دسترس‌پذیری و offline assets.
- `.claude/skills/faranou-accounting-domain/SKILL.md` — قواعد دامنه حسابداری و کنترل‌های مالی.
- `docs/INDEX.md` — ارجاع به Skillهای نسخه‌بندی‌شده.
- `CLAUDE.md` — جدول انتخاب Skillها و قواعد استفاده از نسخه‌های مخزن.
- `docs/WORK_LOG.md` — ثبت تحویل.
- `docs/handoffs/ISSUE-85-faranou-engineering-skills.md` — این گزارش.

## تصمیم‌های فنی
- Skillها در `.claude/skills/` نگهداری می‌شوند تا نسخه‌بندی و بازبینی آن‌ها همراه مخزن باشد.
- از افزودن وابستگی یا ابزار اجرایی جدید خودداری شد؛ این Issue فقط بهبود راهنمای توسعه است.
- تصمیم معماری محصول جدیدی گرفته نشد؛ ADRهای پذیرفته‌شده مرجع باقی می‌مانند.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| خواندن GitHub metadata، فایل‌های مرجع و Skill نصب‌شده | انجام شد | بررسی از طریق GitHub connector؛ بدون clone یا تغییر محلی |
| بازخوانی فایل‌های ایجادشده | در انتظار بررسی نهایی PR | پس از ساخت PR لینک و وضعیت نهایی ثبت شود |
| `dotnet build Template.sln --no-restore --configuration Release` | اجرا نشده | تغییر فقط مستنداتی است؛ این محیط build اجرا نکرد |
| `dotnet test Template.sln --no-build --configuration Release` | اجرا نشده | تغییر فقط مستنداتی است؛ تست محصول اجرا نشده |
| تست‌های کلاینت و `check:offline` | اجرا نشده | فایل‌های کلاینت و وابستگی‌ها تغییر نکرده‌اند |

## امنیت، داده و سازگاری
- هیچ راز، کلید، کد اجرایی، تنظیمات CI یا دیتابیس تغییر نکرد.
- Skill امنیت، ریسک اجرای کد Pull Request غیرقابل‌اعتماد روی Self-hosted Runner را یادآوری می‌کند.
- محتوای Skill راهنماست و به‌تنهایی قابلیت اجرایی یا کنترل امنیتی ایجاد نمی‌کند.

## ریسک‌ها و کارهای باقی‌مانده
- تأیید نهایی لینک‌ها و diff در PR.
- Build/test محصول برای تغییر مستنداتی اجرا نشده و ادعای Verified درباره محصول وجود ندارد.
- ادغام به `main` انجام نشده و نیازمند بررسی و تأیید جداگانه است.

## شناسه‌های Git
- Branch: `docs/issue-85-faranou-engineering-skills`
- Commit: در تاریخچه Branch قابل مشاهده است.
- Pull Request: [#86 — docs: version and align Faranou engineering skills](https://github.com/hamidreza1394/Accounting/pull/86) (Draft؛ ادغام نشده).
