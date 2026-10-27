# مرجع تکمیلی Agentها و Claude Code

این سند بخش‌های **مرجع و توضیحی** `CLAUDE.md` را نگه می‌دارد که در 2026-10-27 **بدون تغییر محتوا** از آن فایل منتقل شدند تا `CLAUDE.md` که در شروع هر جلسه بار می‌شود کوتاه‌تر باشد و توکن ثابت جلسه کمتر مصرف شود. قواعد الزامی (تقسیم کار بین Agentها، دروازهٔ بازبینی، محدودیت‌های مشترک، تقدم Skill) در `CLAUDE.md` و `AGENTS.md` مانده‌اند و در تعارض این سند بر آن‌ها مقدم نیست.

هر وقت به جزئیات تیم Subagent، نحوهٔ فراخوانی، نگاشت گردش‌کار، جدول انتخاب Skill یا فهرست کنترل‌های قطعی نیاز داشتی همین سند را بخوان؛ لازم نیست در هر جلسه خوانده شود.

### مرجع مستندات (2026-10-21)
نقطه شروع: `docs/INDEX.md`. وضعیت قابلیت‌ها با واژگان D-030 (`Implemented`/`Verified`/`NotImplemented`/...) گفته می‌شود. لایسنس تجاری و زیرسیستم‌ها `NotImplemented` هستند؛ Activation قدیمی کد مرده است (D-028). ADR بعدی D-051 است (اصلاح 2026-10-24؛ پیش‌تر D-036 نوشته شده بود در حالی که `docs/DECISIONS.md` تا D-044 را ثبت کرده است؛ مرجع همیشه عنوان `docs/DECISIONS.md` است). گروه شرکتی و تلفیق: `docs/CORPORATE_GROUPS.md` (D-033..D-035)؛ در این مخزن `Company` همان Tenant است.

### وضعیت واقعی تأییدشده (بررسی 2026-10-21)

- `dotnet restore` و `dotnet build Template.sln` روی `.NET SDK 10.0.113` اجرا و **بدون خطا** (۶۸۱ هشدار، همه غیرمسدودکننده) کامل شدند. این تنها بررسی ایستای کد نیست؛ Build واقعی است.
- `git status` در زمان این بررسی: شاخه `main`، هم‌تراز با `origin/main`، بدون تغییر ثبت‌نشده.
- هیچ فایل `CLAUDE.md` یا `.claude/agents/` از قبل وجود نداشت؛ این دو مورد در همین بررسی ایجاد شدند.
- تست‌ها (`dotnet test`) و کلاینت React (`npm test`/`build`) در این بررسی اجرا **نشدند**؛ فقط Restore/Build .NET و Restore/Typecheck کلاینت React تأیید شدند. وضعیت واقعی تست‌ها همان چیزی است که در `docs/PROJECT_STATUS.md` و `docs/WORK_LOG.md` ثبت شده.

### تیم Subagent (`.claude/agents/`)

نُه Subagent تخصصی برای این مخزن تعریف شده‌اند (۶ مورد اول در بررسی اولیه و ۳ مورد آخر در ممیزی ۲۰۲۶-۱۰-۲۱ برای تکمیل حداقل نقش‌های پیشنهادی اضافه شدند؛ هیچ Agent موجودی حذف/ادغام/جایگزین نشد). تعریف یک Subagent به‌تنهایی به معنای اجرای خودکار همه مراحل یا فعال‌شدن یک تیم همیشه‌فعال **نیست**؛ هر Subagent فقط وقتی به کار می‌رود که Claude تشخیص دهد وظیفه با `description` آن تطابق دارد (Delegation خودکار)، یا با نام صریحاً فراخوانی شود. این Agentها قابلیت «Agent Teams» یا اجرای هم‌زمان چند Agent روی یک Working Tree نیستند؛ در نسخه نصب‌شده (Claude Code CLI 2.1.306) چنین قابلیتی جدا از Custom Subagents وجود دارد و در این مرحله فعال نشده است.

| Agent | فایل | مدل | نقش | دسترسی ابزار |
|---|---|---|---|---|
| `architect` | `.claude/agents/architect.md` | `opus` | Lead/Architect: تحلیل معماری، طرح تغییرات بزرگ | فقط‌خواندنی + Bash |
| `backend-developer` | `.claude/agents/backend-developer.md` | `sonnet` | توسعه C#/ASP.NET Core/EF Core | کامل |
| `database-engineer` | `.claude/agents/database-engineer.md` | `sonnet` | مدل EF Core، Migration، Index، صحت داده | کامل |
| `frontend-developer` | `.claude/agents/frontend-developer.md` | `sonnet` | Razor Pages و `ClientApp` (React/Tailwind/RTL) | کامل |
| `qa-engineer` | `.claude/agents/qa-engineer.md` | `sonnet` | تست واحد/یکپارچه/E2E، Build، Regression | کامل |
| `devops-release-engineer` | `.claude/agents/devops-release-engineer.md` | `sonnet` | CI/CD، publish.sh/ps1، Smoke Test، Deployment | کامل |
| `security-reviewer` | `.claude/agents/security-reviewer.md` | `opus` | بازبینی امنیتی Diff، بدون اصلاح فایل | فقط‌خواندنی + Bash |
| `code-reviewer` | `.claude/agents/code-reviewer.md` | `sonnet` | بازبینی مستقل Diff (کیفیت/Regression، غیرامنیتی) | فقط‌خواندنی + Bash |
| `documentation-engineer` | `.claude/agents/documentation-engineer.md` | `sonnet` (پیش‌فرض؛ مستندات ساده با `haiku` در فراخوانی) | نگهداری مستندات و گزارش‌های تحویل | کامل |

دسترسی فقط‌خواندنی `architect`، `security-reviewer` و `code-reviewer` در فیلد `tools` همان Agent اعمال شده (فهرست `Edit`/`Write` در آن نیست)، نه صرفاً یک توصیه در متن؛ این سه Agent از نظر ابزاری قادر به تغییر فایل نیستند مگر خودِ تعریف Agent تغییر کند. برای فایلی که چند Agent «کامل» ممکن است هم‌زمان نیاز به تغییرش داشته باشند (مثلاً یک Migration که هم `database-engineer` و هم `backend-developer` به آن مرتبطند)، طبق `docs/AGENT_COORDINATION.md` قاعده ۳ («از اجرای موازی روی یک working tree مشترک اجتناب کن») عمل کن: یک Agent/شاخه در هر لحظه مالک آن فایل باشد.

### نحوه فراخوانی

- **تشخیص خودکار:** Claude بر اساس `description` هر Agent و وظیفه فعلی، Agent مناسب را انتخاب می‌کند.
- **فراخوانی صریح:** می‌توانید بنویسید «از Agent architect برای بررسی این تغییر استفاده کن» یا با `@architect` (و مشابه برای سایر نام‌ها) مستقیماً همان Agent را صدا بزنید.
- برای تغییرات ساده (مثل اصلاح یک تایپو یا یک تنظیم جزئی)، لازم نیست همه Agentها فعال شوند؛ فقط Agent مرتبط به کار گرفته شود.

### گردش‌کار استاندارد یک قابلیت جدید

گردش‌کار کامل (بررسی Issue → طراحی → پیاده‌سازی → بازبینی Diff → تست → بازبینی امنیتی → مستندسازی → گزارش نهایی) دقیقاً همان است که در `AGENTS.md` بخش‌های ۱، ۳، ۴ و ۵ آمده است. نگاشت به Agentها:

1. بررسی درخواست/Issue/Git — توسط Agent در حال انجام کار (هر کدام).
2. بررسی معماری برای تغییرات مهم — `architect`.
3. پیاده‌سازی — `backend-developer`، `database-engineer` (برای Migration/مدل داده)، یا `frontend-developer` بر اساس حوزه.
4. بازبینی Diff و خطاها — `code-reviewer` (کیفیت/Regression عمومی)، یا `architect` برای تغییرات گسترده.
5. اجرای تست‌های مرتبط — `qa-engineer`.
6. بازبینی امنیتی متناسب با ریسک — `security-reviewer` (برای تغییرات حساس: احراز هویت، Multi-Tenancy، ورودی کاربر، اسرار).
7. Build/CI/Publish — `devops-release-engineer` (برای شکست CI یا تغییر Workflow/اسکریپت انتشار).
8. به‌روزرسانی مستندات و گزارش تحویل (`docs/handoffs/ISSUE-<شماره>-...md` و `docs/WORK_LOG.md`) — `documentation-engineer`؛ مستندات حساس با `sonnet` و بازبینی تخصصی، مستندات ساده با `haiku` (جدول ب).

### انتخاب تخصص: جدول حوزه ← Subagent ← Skill (بند ۳ از «Skill، Plugin، MCP و کنترل‌های قطعی»)

3. **انتخاب تخصص** (Skillها سطح کاربرند و روی هر دستگاه فرق می‌کنند؛ ستون دوم فقط «اگر در دسترس بود»):

   | حوزه | Subagent مخزن | Skill اختیاری |
   |---|---|---|
   | معماری، ADR | `architect` | `engineering:architecture`، `engineering:system-design` |
   | امنیت و دسترسی | `security-reviewer` | `engineering:code-review` |
   | بازبینی Diff/PR | `code-reviewer` | `engineering:code-review`، `code-review:code-review` |
   | UI/UX، فارسی/RTL | `frontend-developer` | `frontend-design:frontend-design`، `design:accessibility-review`، `design:design-critique` |
   | تست و QA | `qa-engineer` | `engineering:testing-strategy`، `superpowers:test-driven-development` |
   | خطایابی | Agent حوزهٔ مربوط | `engineering:debug`، `superpowers:systematic-debugging` |
   | مستندات | `documentation-engineer` | `engineering:documentation` |
   | معماری و ADR فرانو | `architect` | `faranou-architecture-review` |
   | امنیت و جداسازی شرکت‌ها | `security-reviewer` | `faranou-security-multitenancy` |
   | UI/UX فارسی و RTL | `frontend-developer` | `faranou-ui-ux-design`؛ پیش از هر کار UI حتماً `docs/UI_COMPONENTS.md` را بخوان؛ برای فرم‌ها اختیاری: `ui-ux-pro-max` |
   | تست و کیفیت فرانو | `qa-engineer` | `faranou-testing-quality` |
   | حسابداری، مالی و قواعد داده | `backend-developer` / `database-engineer` | `faranou-accounting-domain` |
   | توسعه عمومی .NET/C# | `backend-developer` | `dotnet-patterns` |
   | آزمون C#/.NET | `qa-engineer` | `csharp-testing` |
   | طراحی API هم‌مبدأ | `backend-developer` | `api-design` |
   | React/TypeScript | `frontend-developer` | `react-patterns` |
   | آزمون React/Vitest | `qa-engineer` | `react-testing` |
   | E2E مرورگر | `qa-engineer` | `e2e-testing` |
   | تحلیل اثر قبل/بعد تغییر | `architect` | `change-impact` |
   | Migrationهای EF Core/SQL Server | `database-engineer` | `faranou-efcore-migrations` |
   | ثبت تصمیم معماری در فایل موجود | `architect` | `faranou-architecture-decisions` |
   | توسعه عمومی فرانو | Agent حوزهٔ مربوط | `faranou-senior-developer` |
   | پیش از ادعای «تمام شد» | Lead | `superpowers:verification-before-completion` |

   Skillهای اختصاصی فرانو و Skillهای انتخابی سازگارشده از ECC در `.claude/skills/` نسخه‌بندی شده‌اند (Issue #85 / PR #86 و Issue #87). منشأ، SHA و قواعد سازگارسازی در `docs/THIRD_PARTY_SKILLS.md` ثبت شده‌اند. وجود فایل در مخزن به‌تنهایی ثابت نمی‌کند که Skill در جلسهٔ جاری در دسترس یا فراخوانی شده است؛ فهرست Skillهای همان جلسه را بررسی کن و فقط در صورت در دسترس بودن آن را فعال کن. در هر تعارض، `AGENTS.md`، این فایل و ADRها مقدم‌اند.

### کنترل‌های قطعی و محدودیت‌هایشان (بند ۷ از «Skill، Plugin، MCP و کنترل‌های قطعی»)

7. **کنترل‌های قطعی و محدودیت‌هایشان:** متن این فایل رفتار را هدایت می‌کند و تضمین قطعی نیست. کنترل قطعی فقط CI (`.github/workflows/dotnet.yml`) و `permissions` در `.claude/settings.json` است: `deny` برای خواندن (و در نتیجه نوشتن) `.env*` (جز `.env.example`)، `appsettings.Local.json`، `secrets.json`، `*.pfx`، `*.p12`، مخزن User Secrets، `dotnet ef database drop` و Push اجباری (`--force`، `-f`، `+branch`)، حذفی (`--delete`) و آینه‌ای (`--mirror`)؛ `ask` برای Commit، Push، Merge، Rebase، `reset --hard`، `clean`، `restore`، `checkout --`، `branch -D`، `gh pr create/merge`، `gh release`، `dotnet ef database update`، `dotnet ef migrations remove`، `dotnet user-secrets list` و `rm -r`. محدودیت‌ها: قواعد Bash فقط شکل ظاهری فرمان را می‌سنجند (`/bin/rm`، `bash -c`، `git -C . push`، حذف شاخه با `git push origin :branch` و `sqlcmd` را نمی‌گیرند)؛ `Read deny` برای ابزارهای خواندن و فرمان‌های شناخته‌شده مثل `cat` اعمال می‌شود، نه اسکریپتی که خودش فایل را باز می‌کند؛ این‌ها Sandbox کامل نیستند. تنها Hook مخزن `SessionStart` برای نصب `dotnet` در جلسهٔ Cloud است (`scripts/dev/cloud-session-start.sh`، فقط با `CLAUDE_CODE_REMOTE=true`)؛ Hook بیرون از `permissions` اجرا می‌شود، پس تغییر `scripts/dev/*` و `.claude/*` را مثل پیکربندی اجرایی بازبینی کن. `allow` محلی قاعدهٔ `deny`/`ask` را لغو نمی‌کند؛ تغییر آن‌ها با ویرایش همان فایل و بازبینی PR است. Pluginها روی هر دستگاه جدا نصب می‌شوند و تغییر فایل‌های مخزن فعال‌شدن آن‌ها را تضمین نمی‌کند؛ نسخهٔ Claude Code را با `claude --version` بخوان (عدد 2.1.306 در این فایل مربوط به بررسی 2026-10-21 است).
