# تحویل وظیفه — ISSUE-UNNUMBERED-architecture-source-of-truth

- **Issue:** UNNUMBERED
- **Agent/مسئول:** Claude (Sonnet 5.5)
- **تاریخ:** 2026-10-21
- **وضعیت:** انجام‌شده (مستنداتی؛ منتظر بازبینی مالک)

## هدف و معیارهای پذیرش
- [x] ممیزی مستندات در برابر کد، تاریخچه و CI و ثبت انحراف‌ها
- [x] ADRهای جدید از D-020 بدون تغییر شماره‌های قبلی
- [x] مستندات لایسنس/مدل داده/گردش‌کار/تهدید با وضعیت `NotImplemented`
- [x] اصلاح گزاره منسوخ Activation
- [x] هیچ تغییری در کد، Schema، Migration، پیکربندی یا بسته

## خلاصه تغییرات
مرجع مستندات یکپارچه شد؛ وضعیت‌ها با واژگان D-030 برچسب خوردند؛ ناهمخوانی‌ها در `REPOSITORY_AUDIT.md` (AUD-08..20) ثبت شدند.

## فایل‌های مهم تغییرکرده
- جدید: `docs/INDEX.md`، `PRODUCT_REQUIREMENTS.md`، `REQUIREMENTS_TRACEABILITY.md`، `LICENSING*.md` (۴ فایل)، این handoff
- ویرایش: `README.md`، `AGENTS.md`، `CLAUDE.md`، `docs/{PROJECT_STATUS,ROADMAP,ARCHITECTURE,DECISIONS,DATABASE,SECURITY,TESTING,DEPLOYMENT,DEVELOPMENT,REPOSITORY_AUDIT,AGENT_COORDINATION,AGENT_START_PROMPT,WORK_LOG}.md`، `docs/prompts/*`

## تصمیم‌های فنی
D-020..D-030 (D-024 و D-027 `Proposed`؛ D-022/D-025/D-026/D-028 با بخش‌های `NeedsApproval`).

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| `dotnet build` / `dotnet test` | اجرا نشد | dotnet در این محیط نصب نیست |
| شاهد CI | اجرای `39585708662` روی `1d39dbf`: ۶ Job سبز، ۲۰۴ تست، ۱۲ E2E | از `gh api` خوانده شد؛ اجرای این Agent نیست |
| اعتبارسنجی لینک‌های داخلی، تکرار شماره ADR، `git diff --check` | ر.ک. گزارش نهایی PR | اسکریپت موقت |

## امنیت، داده و سازگاری
بدون تغییر رفتاری. ثبت‌شده: Activation کد مرده؛ مجوز عملیاتی بدون مصرف؛ `UserId` بدون FK؛ نبود اسکن اسرار در CI؛ نبود `LICENSE`.

## ریسک‌ها و کارهای باقی‌مانده
تصمیم‌های `NeedsApproval` (LICENSING §۶)؛ PR کد برای حذف کد مرده Activation؛ تصمیم LICENSE و نام‌گذاری؛ بررسی Branch protection (نامشخص).

## شناسه‌های Git
- Branch: `docs/architecture-source-of-truth`
- Commit / Pull Request: در گزارش نهایی
