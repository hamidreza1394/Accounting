# تحویل وظیفه — ISSUE-UNNUMBERED-agent-workflow-hardening

- **Issue:** UNNUMBERED — تغییرات مستندسازی بر اساس درخواست کاربر
- **Agent/مسئول:** ChatGPT با GitHub connector
- **تاریخ:** 2026-10-16
- **وضعیت:** ناقص از نظر اعتبارسنجی محلی؛ تغییرات مستندات در شاخه ثبت شده‌اند

## هدف و معیارهای پذیرش
- [x] تعریف پروتکل مشترک هماهنگی Agentها
- [x] تهیه پرامپ‌های جداگانه برای Codex و GitHub Copilot
- [x] ثبت روش handoff و اولویت‌های شناخته‌شده
- [ ] اجرای Build/Test برنامه — خارج از دامنه تغییر مستندات و در این محیط اجرا نشده
- [ ] بازبینی نهایی مستقل و تأیید Maintainer

## خلاصه تغییرات
- فایل راهنمای هماهنگی چند Agent ایجاد شد.
- پرامپ‌های اختصاصی Codex و Copilot افزوده شدند.
- این تغییرات روی شاخه جداگانه انجام شده‌اند؛ `main` تغییر نکرده است.

## فایل‌های مهم تغییرکرده
- `docs/AGENT_COORDINATION.md` — قواعد همکاری، جلوگیری از تداخل و ترتیب اولویت‌ها
- `docs/prompts/CODEX_PROMPT.md` — پرامپ اجرای وظایف برای Codex
- `docs/prompts/COPILOT_PROMPT.md` — پرامپ اجرای وظایف برای Copilot
- `AGENTS.md` — پیوندهای سیاست مخزن و گردش کار
- `docs/AGENT_START_PROMPT.md` — الزام استفاده از پروتکل هماهنگی و پرامپ‌های تخصصی
- `docs/WORK_LOG.md` — ثبت تاریخچه این تغییر
- `docs/handoffs/ISSUE-UNNUMBERED-agent-workflow-hardening.md` — این گزارش

## تصمیم‌های فنی
- Git و مستندات version-controlled منبع حقیقت بین Agentها هستند؛ حافظه گفتگو جایگزین آن نیست.
- هر وظیفه باید شاخه، Issue و PR مستقل داشته باشد و ادغام توسط Maintainer انجام شود.
- برای این تغییر مستنداتی، Issue واقعی در مخزن ایجاد نشده است؛ شناسه جعلی استفاده نشده است.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| بررسی محتویات GitHub و اسناد فعلی | انجام شد | فقط مستندات و metadata مخزن بررسی شدند |
| `dotnet restore Template.sln` | اجرا نشده | این تغییر صرفاً مستنداتی است؛ محیط محلی پروژه در این اجرا در دسترس نیست |
| `dotnet build Template.sln --no-restore --configuration Release` | اجرا نشده | کامپایل تأیید نشده |
| `dotnet test Template.sln --no-build --configuration Release` | اجرا نشده | تست تأیید نشده |
| `python scripts/check_static_assets.py` | اجرا نشده | اجرای محلی اسکریپت انجام نشده |
| `git diff --check` | اجرا نشده | از طریق GitHub Contents API تغییرات ثبت شده‌اند؛ working tree محلی برای اجرای این فرمان در دسترس نیست |

## امنیت، داده و سازگاری
- تغییرات صرفاً مستنداتی هستند؛ کد اجرایی، schema و secrets تغییر داده نشده‌اند.

## ریسک‌ها و کارهای باقی‌مانده
- بازبینی تغییرات از طریق Pull Request لازم است.
- اولویت‌های امنیتی/فنی موجود در Audit همچنان باز هستند و این PR آن‌ها را پیاده‌سازی نمی‌کند.
- اجرای CI واقعی پس از بازشدن PR برای اطمینان از سلامت مخزن توصیه می‌شود.

## شناسه‌های Git
- Branch: `docs/agent-workflow-hardening`
- Commit نهایی شاخه: e27930893bc7d67ac98782091b21331333f12680
- Pull Request: https://github.com/hamidreza1394/Accounting/pull/6 (باز، ادغام‌نشده)
