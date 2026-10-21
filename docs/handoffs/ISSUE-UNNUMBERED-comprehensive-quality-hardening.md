# تحویل وظیفه — ISSUE-UNNUMBERED-comprehensive-quality-hardening

- **Issue:** `UNNUMBERED` (مأموریت مستقیم کاربر، بدون GitHub Issue رسمی)
- **Agent/مسئول:** Claude Code (Sonnet 5)
- **تاریخ:** 2026-10-21
- **وضعیت:** انجام‌شده برای بخش‌های مجازشده در این دور (ممیزی، تکمیل تیم Agent، رفع تناقض نسخه، بررسی قطعی CI، اجرای واقعی تست‌ها، Commit/Push/PR)؛ **ناقص/باز** برای بخش‌های نیازمند تصمیم کاربر یا زیرساخت غیردسترس‌پذیر (جزئیات در «ریسک‌ها و کارهای باقی‌مانده»).

## هدف و معیارهای پذیرش
- [x] تکمیل تیم Subagent به حداقل ۸ نقش بدون حذف/ادغام Agentهای موجود (۹ نقش نهایی شد).
- [x] رفع تناقض نسخه .NET در مستندات (۹→۱۰ همگام با D-007 واقعی).
- [x] بررسی قطعی (نه فقط محلی) شکست CI در Job `publish` با شواهد Run واقعی.
- [x] اجرای واقعی `dotnet test`، `check_static_assets.py`، `dotnet format --verify-no-changes`، و کامل مراحل ClientApp (`typecheck`/`test`/`build`/`check:offline`) با گزارش نتیجه صادقانه.
- [x] به‌روزرسانی `docs/REPOSITORY_AUDIT.md`, `docs/PROJECT_STATUS.md`, `docs/WORK_LOG.md`, `docs/AGENT_START_PROMPT.md`, `docs/prompts/*`, `docs/AGENT_COORDINATION.md`.
- [ ] تست یکپارچه‌سازی روی SQL Server واقعی (`FARANOU_TEST_SQLSERVER`) — **ناممکن در این محیط** (بدون SQL Server/LocalDB).
- [ ] بازبینی رفتاری عمیق خط‌به‌خط امنیت/چندمستاجری/معماری فراتر از آنچه ممیزی‌های ۱۶ و ۲۱ اکتبر پوشش دادند — **باز**، نیازمند دور بعدی یا تصمیم اولویت‌بندی کاربر.
- [ ] تصمیم حذف/تکمیل مسیر Google OAuth (AUD-2026-10-21-02) — **منتظر تصمیم کاربر/architect**.

## خلاصه تغییرات
1. سه Agent جدید اضافه شد: `database-engineer`، `devops-release-engineer`، `code-reviewer` (هر سه با `tools`/`model` طبق قرارداد `CLAUDE.md`)؛ ۶ Agent قبلی بدون تغییر محتوایی باقی ماندند.
2. `CLAUDE.md` برای انعکاس ۹ Agent و گردش‌کار ۸ مرحله‌ای به‌روزرسانی شد.
3. تناقض نسخه .NET 9/10 در سه فایل پرامپت/هماهنگی رفع شد؛ `docs/AGENT_COORDINATION.md` آیتم اولویت مرتبط را «انجام‌شده» علامت زد.
4. شکست CI در Job `publish` روی `main`@`8244bf3` با شواهد واقعی (بازتولید محلی + `gh run rerun`) به‌عنوان Flaky تأیید شد؛ `docs/REPOSITORY_AUDIT.md` (AUD-2026-10-21-01) به‌روزرسانی شد — **هیچ کد تولیدی تغییر نکرد** (شواهدی برای نقص واقعی کد یافت نشد).
5. دستور صحیح `dotnet test --solution Template.sln` برای .NET SDK 10.0.113 کشف و استفاده شد (سینتکس قدیمی `dotnet test Template.sln` در این نسخه رد می‌شود).
6. تمام دستورات مرجع بخش ۴ `AGENTS.md` به‌جز تست SQL-Server-محور واقعاً اجرا شدند (جزئیات در جدول پایین).
7. `docs/PROJECT_STATUS.md` و `docs/WORK_LOG.md` با یادداشت جدید append-only به‌روزرسانی شدند.

## فایل‌های مهم تغییرکرده
- `CLAUDE.md` — جدول و گردش‌کار Agent به ۹ نقش.
- `.claude/agents/database-engineer.md`, `.claude/agents/devops-release-engineer.md`, `.claude/agents/code-reviewer.md` — Agentهای جدید.
- `docs/AGENT_START_PROMPT.md`, `docs/prompts/CODEX_PROMPT.md`, `docs/prompts/COPILOT_PROMPT.md`, `docs/AGENT_COORDINATION.md` — رفع تناقض نسخه .NET.
- `docs/REPOSITORY_AUDIT.md` — به‌روزرسانی AUD-2026-10-21-01 با شواهد Flaky.
- `docs/PROJECT_STATUS.md`, `docs/WORK_LOG.md` — یادداشت append-only این دور.
- `docs/handoffs/ISSUE-UNNUMBERED-comprehensive-quality-hardening.md` — همین فایل.

## تصمیم‌های فنی
- هیچ تصمیم معماری جدیدی (D-0xx) ثبت نشد؛ این دور صرفاً ممیزی/تأیید/تکمیل تیم بود، نه تغییر معماری.
- تصمیم گرفته شد `scripts/smoke-published-app.sh` **تغییر نکند** چون هیچ شواهدی از نقص واقعی اسکریپت یافت نشد؛ تغییر بدون شواهد مغایر قاعدهٔ «تضعیف برای سبزشدن CI» بود.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `dotnet restore Template.sln` | موفق | بدون خطا |
| `dotnet build Template.sln --no-restore --configuration Release` | موفق | ۰ Error (هشدارهای غیرمسدودکننده از قبل) |
| `dotnet test --solution Template.sln --configuration Release` | موفق | `total: 204, failed: 0, succeeded: 204, skipped: 0` (~۱۴s) |
| `python3 scripts/check_static_assets.py` | موفق | PASS — بدون مرجع CDN |
| `dotnet format Template.sln --verify-no-changes --no-restore` | موفق | بدون تغییر لازم؛ فقط هشدار Analyzer |
| `npm ci` (ClientApp) | موفق | ۱۲۲ بسته؛ ۱ آسیب‌پذیری High در `vite`/`launch-editor` (= AUD-2026-10-21-03، فقط ابزار توسعه) |
| `npm run typecheck` | موفق | بدون خطای TS |
| `npm test -- --run` | موفق | ۴ فایل، ۲۴ تست، همه Pass |
| `npm run build` | موفق | Bundle در `Server/wwwroot/app` (gitignored) |
| `npm run check:offline` | موفق | PASS — ۱۲ منبع محلی، بدون مرجع خارجی |
| `gh run rerun 39483239598` (CI واقعی روی `main`) | موفق (Attempt #2) | همه Jobها از جمله `publish` با `conclusion: success` |
| تست یکپارچه‌سازی SQL Server واقعی (`FARANOU_TEST_SQLSERVER`) | **اجرا نشد** | SQL Server/LocalDB در این محیط موجود نیست |

## امنیت، داده و سازگاری
- هیچ تغییر کد تولیدی (Backend/Frontend/Migration) در این دور انجام نشد؛ فقط مستندات، تعریف Agent و تست/بررسی. بدون اثر مستقیم بر امنیت یا داده.
- بررسی امنیتی عمیق‌تر (کد `AuthenticationService`/`SessionValidator`/همهٔ Endpointهای چندمستاجری خط‌به‌خط) در این دور تکرار نشد؛ یافته‌های ممیزی ۱۶ و ۲۱ اکتبر (در `docs/REPOSITORY_AUDIT.md`) هنوز معتبرترین مرجع‌اند.

## ریسک‌ها و کارهای باقی‌مانده
1. مسیر مرده Google OAuth (`AUD-2026-10-21-02`) — نیاز به تصمیم کاربر: حذف کامل یا تکمیل پیکربندی.
2. آسیب‌پذیری Low در `vite` (`AUD-2026-10-21-03`) — ارتقا هنوز انجام نشده (فقط ابزار توسعه، نه Production).
3. تست‌های جداسازی Tenant (`CompanyAccessServiceTests`, `ApiEndpointTests`) روی SQL Server واقعی هنوز تأیید نشده‌اند — نیاز به محیطی با SQL Server/LocalDB.
4. بازبینی عمیق‌تر معماری/امنیت/دیتابیس/Frontend طبق بخش‌های ۵ تا ۸ مأموریت سوم کاربر، فراتر از آنچه در این و دورهای قبلی ممیزی شد، هنوز اجرا نشده است.
5. Build موفق و سبز شدن CI به معنی سلامت کامل پروژه نیست؛ این یادداشت صرفاً وضعیت واقعاً تأییدشده در این دور را ثبت می‌کند.

## شناسه‌های Git
- Branch: `fix/comprehensive-quality-hardening`
- Commit: (پس از ثبت این فایل ایجاد می‌شود — به Commit بعدی مراجعه کنید)
- Pull Request: (پس از Push ایجاد می‌شود)
