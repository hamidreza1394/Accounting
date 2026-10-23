# تحویل وظیفه — ISSUE-UNNUMBERED-claude-code-standing-rules

- **Issue:** UNNUMBERED — طبق تصمیم صریح کارفرما (2026-10-23) فعلاً Issue ساخته نشد؛ این با `AGENTS.md` بخش ۳ («هر وظیفه به Issue متصل») انحراف آگاهانه است و مطابق سابقهٔ مخزن و بخش ۵ با `UNNUMBERED` ثبت می‌شود.
- **Agent/مسئول:** Lead (Claude Code 2.1.309)، بدون فراخوانی Subagent برای نوشتن؛ تغییرات کوچک و صرفاً دستورالعمل/تنظیمات (جدول الف در `CLAUDE.md`)
- **تاریخ:** 2026-10-23
- **وضعیت:** انجام‌شده روی شاخهٔ محلی `docs/claude-code-standing-rules`؛ Commit/Push/PR انجام نشده

## هدف و معیارهای پذیرش
- [x] ثبت قاعدهٔ دائمی تشخیص و فراخوانی Skill/Plugin/Subagent و تفکیک نصب/دسترسی/فراخوانی/اجرا در `CLAUDE.md`
- [x] گزارش‌دهی شفاف: استفاده‌شده، در دسترس ولی استفاده‌نشده، در دسترس نبود، اجرانشده
- [x] کنترل قطعی `permissions` در `.claude/settings.json`، سازگار با ساختار موجود، بدون Hook، با محدودیت‌های مستند
- [x] هماهنگی `documentation-engineer` (مدل و بازبینی تخصصی) با قواعد فعلی
- [x] اصلاح ارجاع منسوخ ADR فقط پس از تأیید با شواهد مخزن
- [x] بدون تغییر کد محصول، CI، Schema یا `docs/DECISIONS.md`
- [ ] اجرای CI روی PR و بازبینی/ادغام توسط Maintainer — انجام نشده

## خلاصه تغییرات
- `CLAUDE.md`: بخش جدید «Skill، Plugin، MCP و کنترل‌های قطعی» (هفت بند)، و هماهنگی ردیف‌های `documentation-engineer` در جدول Agentها، جدول ب و مرحلهٔ ۸ گردش‌کار.
- `.claude/settings.json` (جدید): فقط `permissions` با ۱۷ قاعدهٔ `ask` و ۱۷ قاعدهٔ `deny`؛ بدون `hooks`، `env` یا `defaultMode`.
- `.claude/agents/documentation-engineer.md`: `model: haiku` → `sonnet` و یک قاعدهٔ توقف برای زمانی که با `haiku` روی محتوای حساس فراخوانی شود.
- `docs/AGENT_COORDINATION.md`: اصلاح خط ADR، همسان‌سازی دو ردیف جدول، و ارجاع کوتاه به `CLAUDE.md`.
- `docs/AGENT_START_PROMPT.md`، `docs/prompts/CODEX_PROMPT.md`، `docs/prompts/COPILOT_PROMPT.md`: همان ارجاع منسوخ «ADR بعدی D-031» به D-036.
- `docs/INDEX.md`: ارجاع به بخش جدید و `.claude/settings.json`.
- `docs/WORK_LOG.md`: ورودی جدید در انتها.

## فایل‌های مهم تغییرکرده
- `CLAUDE.md` — قاعدهٔ دائمی Skill/Plugin و کنترل‌های قطعی
- `.claude/settings.json` — کنترل قطعی `permissions` (جدید)
- `.claude/agents/documentation-engineer.md` — مدل پیش‌فرض و قاعدهٔ توقف
- `docs/AGENT_COORDINATION.md` — ارجاع کوتاه، اصلاح D-036، همسان‌سازی جدول
- `docs/AGENT_START_PROMPT.md`، `docs/prompts/CODEX_PROMPT.md`، `docs/prompts/COPILOT_PROMPT.md` — اصلاح ارجاع ADR
- `docs/INDEX.md`، `docs/WORK_LOG.md`، این گزارش

## تصمیم‌های فنی
- **`ask` به‌جای `deny` برای Commit/Push/Merge و مشابه:** `deny` بین فایل‌های تنظیمات ادغام می‌شود و `allow` محلی نمی‌تواند آن را لغو کند؛ مشارکت‌کنندگان گاهی مشروعاً Push یا `dotnet ef database update` (مثلاً `scripts/apply_migrations_to_testdb.sh`) لازم دارند. `deny` فقط برای عملیات بدون کاربرد مشروع برای Agent ماند: حذف دیتابیس، Push اجباری/حذفی/آینه‌ای، و خواندن/نوشتن فایل‌های اسرار.
- **فایل‌های ردیابی‌شده مسدود نشدند:** `Server/appsettings.json` و `Server/appsettings.Development.json` در Git هستند؛ فقط `appsettings.Local.json` (که `.gitignore` هم آن را کنار می‌گذارد) بسته شد. `.env.example` با `Read(!.env.example)` باز ماند.
- **بدون Hook و بدون تغییر CI:** مطابق دستور کارفرما برای این مرحله.
- **`documentation-engineer` پیش‌فرض `sonnet`:** اشتباه پیش‌فرض ارزان روی مستندات حساس پرهزینه‌تر از توکن اضافه برای مستندات ساده است؛ مستندات ساده با `model: haiku` در فراخوانی (اولویت پارامتر فراخوانی بر فیلد Agent در مستندات رسمی Claude Code تأیید شد). مقادیر `sonnet`/`opus`/`haiku` طبق همان مستندات معتبرند.
- **دامنهٔ اصلاح D-031:** کارفرما فقط `AGENT_COORDINATION.md` را نام برد؛ همان ادعای منسوخ در سه فایل پرامپت Agent هم بود و می‌توانست Agent بعدی را به ثبت ADR با شمارهٔ تکراری بکشاند، پس آن‌ها هم با همان شاهد اصلاح شدند. اگر لازم نیست، قابل برگشت جداگانه است.
- تصمیم معماری یا ADR جدیدی گرفته نشد.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| اعتبار JSON `.claude/settings.json` (پایتون) | موفق | `$schema` و `permissions` فقط؛ بدون قاعدهٔ تکراری یا هم‌پوشان بین `ask` و `deny` |
| `claude doctor` در مخزن | موفق برای تنظیمات | هیچ خطا یا قاعدهٔ ردشده‌ای گزارش نشد؛ دو هشدار نصب مربوط به خود sandbox‌اند |
| آزمون رفتاری `permissions` (جلسهٔ headless، Claude Code 2.1.309، `dontAsk`، دایرکتوری موقت جدا) | موفق برای `deny` | مسدود: `.env`، `Server/.env`، `Server/Nested/.env.production`، `secrets.json`، نوشتن روی `.env.local`، `git push -f`، `-fu`، `+main`، `dotnet ef --project … database drop`. مجاز: `.env.example`، `appsettings.json`، `git push origin feature-fix`، `git push origin HEAD:main` (فقط رد حالت `dontAsk`، نه قاعده) |
| آزمون اثر قواعد `ask` | **انجام نشد/غیرقطعی** | در `dontAsk` هر فرمان غیرمجاز رد می‌شود و حتی شاهد بدون قاعده هم مسدود ماند؛ اثر `ask` فقط بر پایهٔ مستندات رسمی (ترتیب `deny`، `ask`، `allow`) ثبت می‌شود |
| آزمون انتخاب/فراخوانی (جلسهٔ headless فقط‌خواندنی با `Edit`/`Write` ممنوع) | موفق برای Subagent | وظیفهٔ امنیتی، `security-reviewer` را با ابزار `Agent` فراخوانی کرد (۵ فراخوانی ابزار در لاگ). Skill افزونه‌ای فراخوانی نشد و در گزارش خودش «در دسترس ولی استفاده‌نشده» ثبت شد. هیچ فایلی تغییر نکرد |
| بررسی ساختار Markdown (جدول‌ها، لینک‌های نسبی، مسیرهای ارجاع‌شده، ارجاع عنوان بین فایل‌ها، یکسانی ردیف‌های جدول ب) | موفق | ۰ جدول ناهماهنگ؛ ۰ مسیر ناموجود؛ دو ردیف مستندات بین `CLAUDE.md` و `AGENT_COORDINATION.md` یکسان‌اند |
| اعتبار فیلد `model` و `name` در ۹ Agent | موفق | همه `opus`/`sonnet`/`haiku` معتبر و نام با فایل یکی است |
| `git diff --check` | موفق | بدون خطای فاصلهٔ اضافه؛ فایل جدید `.claude/settings.json` بدون فاصلهٔ انتهایی |
| `dotnet restore/build/test`، `check_static_assets.py`، `npm` | اجرا نشده | `dotnet` در این محیط نصب نیست و کد یا دارایی تغییر نکرده است |
| CI (`.github/workflows/dotnet.yml`) | اجرا نشده | PR ساخته نشده |

## امنیت، داده و سازگاری
- **بازبینی مستقل (سطح: حساس به‌خاطر اسرار/مجوزها):** `security-reviewer` (opus، فقط‌خواندنی) در جلسهٔ آزمون `.claude/settings.json` را بازبینی کرد. گزارش او شاهد نبود؛ هر یافته مستقلاً بررسی شد:
  - «`Read(!.env.example)` بی‌اثر است»: **نادرست**؛ آزمون واقعی `.env.example` را مجاز نشان داد.
  - «`Server/.env` تو در تو پوشش ندارد» و «Edit/Write روی اسرار بسته نیست»: **نادرست**؛ آزمون واقعی هر دو را مسدود نشان داد.
  - «`git push origin +main`، `-fu`، `dotnet ef --project X database drop` رد نمی‌شوند»: **درست**؛ الگوها اصلاح و دوباره آزموده شدند.
  - «`dotnet user-secrets list` اسرار را چاپ می‌کند»: **درست**؛ `ask` اضافه شد.
  - «`git restore`، `checkout --`، `branch -D`، `rm -r -f` پوشش ندارند»: **درست**؛ `ask` اضافه شد.
  - «اسکریپت پایتون و `sqlcmd DROP` را قاعده نمی‌گیرد»: درست و از قبل مستند؛ نیازمند Sandbox یا Hook.
- **اشکال یافت‌شده در کار خودم:** دو الگوی `git push :*` و `git push * :*` با نحو پسوند `:*` تداخل داشتند (Claude Code هشدار داد) و بی‌اثر بودند؛ حذف شدند. حذف شاخه با `git push origin :branch` فقط از طریق `ask` روی همهٔ pushها پوشش دارد و در `CLAUDE.md` به‌عنوان محدودیت ثبت شد.
- اثر بر داده، Migration، احراز هویت و مجوزهای زمان اجرا: ندارد. هیچ Secret یا داده واقعی درج نشد؛ در آزمون‌ها فقط مقدارهای ساختگی در دایرکتوری موقت بیرون از مخزن به کار رفت.
- **ابزارها:** Skill/Plugin/Subagent واقعاً فراخوانی‌شده برای نوشتن: هیچ. در این محیط ۸ Plugin سطح کاربر نصب و loaded هستند (`engineering`، `design`، `superpowers`، `code-review-plugin`، `code-simplifier`، `cowork-plugin-management`، `typescript-native-lsp`، `serena`) و هیچ MCP پیکربندی نشده؛ چون کار صرفاً دستورالعمل/تنظیمات بود استفاده نشدند. مصرف توکن Subagentها: فقط `security-reviewer` در جلسهٔ آزمون؛ عدد ۲۵٬۵۰۴ توکن را همان جلسه از خروجی ابزار گزارش کرده و مستقلاً راستی‌آزمایی نشده است.

## ریسک‌ها و کارهای باقی‌مانده
- **اصطکاک مشارکت‌کنندگان:** `ask` روی `git commit`/`git push` را `allow` محلی لغو نمی‌کند؛ هر Commit/Push در Claude Code تأیید می‌خواهد. اگر مزاحم بود، فقط همین قواعد از `.claude/settings.json` برداشته شود (با PR).
- `permissions` Sandbox نیست: `/bin/rm`، `bash -c`، `git -C . push`، `sqlcmd`، و اسکریپتی که خودش فایل اسرار را باز کند پوشش ندارند. کنترل واقعی‌تر نیازمند Sandbox یا Hook `PreToolUse` است؛ طبق دستور، Hook اضافه نشد و نیازمند تصمیم کارفرماست.
- Pluginها سطح کاربرند؛ فایل‌های مخزن فعال‌شدن آن‌ها را روی دستگاه‌های دیگر تضمین نمی‌کنند. اگر تیم بخواهد، می‌توان `enabledPlugins`/`extraKnownMarketplaces` را در `.claude/settings.json` ثبت کرد، ولی این به تأیید اعتماد هر عضو وابسته است و اینجا انجام نشد.
- عدد نسخهٔ Claude Code (`2.1.306`) در بخش‌های قدیمی `CLAUDE.md` مربوط به بررسی 2026-10-21 است؛ در این محیط `2.1.309` دیده شد. قاعدهٔ جدید خواندن نسخه با `claude --version` را الزام می‌کند.
- اثر قواعد `ask` به‌صورت تجربی تأیید نشده و آزمون در یک نشست تعاملی (غیر `dontAsk`) باقی است.
- اعتبارسنجی نهایی روی CI و بازبینی Maintainer انجام نشده است. هیچ CI جدید برای اعتبارسنجی فایل‌های Agent/تنظیمات اضافه نشد.

## شناسه‌های Git
- Branch: `docs/claude-code-standing-rules` (محلی، از `main` در commit `55dc302`)
- Commit: ندارد
- Pull Request: ندارد
