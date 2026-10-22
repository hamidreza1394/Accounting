@AGENTS.md

## Claude Code — تیم Agent و گردش‌کار

این بخش فقط مخصوص Claude Code است و چیزی از `AGENTS.md` را تکرار نمی‌کند؛ قرارداد کاری، قواعد غیرقابل‌مذاکره، استانداردهای کدنویسی و دستورات مرجع همان فایل (بالا) است و باید کامل رعایت شود. اینجا صرفاً تیم Subagentهای این مخزن و نحوه استفاده از آن‌ها تعریف می‌شود.

### مرجع مستندات (2026-10-21)
نقطه شروع: `docs/INDEX.md`. وضعیت قابلیت‌ها با واژگان D-030 (`Implemented`/`Verified`/`NotImplemented`/...) گفته می‌شود. لایسنس تجاری و زیرسیستم‌ها `NotImplemented` هستند؛ Activation قدیمی کد مرده است (D-028). ADR بعدی D-031 است.

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
| `documentation-engineer` | `.claude/agents/documentation-engineer.md` | `haiku` | نگهداری مستندات و گزارش‌های تحویل | کامل |

دسترسی فقط‌خواندنی `architect`، `security-reviewer` و `code-reviewer` در فیلد `tools` همان Agent اعمال شده (فهرست `Edit`/`Write` در آن نیست)، نه صرفاً یک توصیه در متن؛ این سه Agent از نظر ابزاری قادر به تغییر فایل نیستند مگر خودِ تعریف Agent تغییر کند. برای فایلی که چند Agent «کامل» ممکن است هم‌زمان نیاز به تغییرش داشته باشند (مثلاً یک Migration که هم `database-engineer` و هم `backend-developer` به آن مرتبطند)، طبق `docs/AGENT_COORDINATION.md` قاعده ۳ («از اجرای موازی روی یک working tree مشترک اجتناب کن») عمل کن: یک Agent/شاخه در هر لحظه مالک آن فایل باشد.

### نحوه فراخوانی

- **تشخیص خودکار:** Claude بر اساس `description` هر Agent و وظیفه فعلی، Agent مناسب را انتخاب می‌کند.
- **فراخوانی صریح:** می‌توانید بنویسید «از Agent architect برای بررسی این تغییر استفاده کن» یا با `@architect` (و مشابه برای سایر نام‌ها) مستقیماً همان Agent را صدا بزنید.
- برای تغییرات ساده (مثل اصلاح یک تایپو یا یک تنظیم جزئی)، لازم نیست همه Agentها فعال شوند؛ فقط Agent مرتبط به کار گرفته شود.

### قاعدهٔ الزامی تقسیم کار بین Agentها (دستور کارفرما، 2026-10-22)

وقتی کارفرما پرامپت کاری می‌دهد، Claude **ملزم است** کار را بر اساس نوع آن بین Subagentهای همین مخزن تقسیم کند و هر بخش را به Agentی بسپارد که مدل مناسبش در `.claude/agents/` تعریف شده است (هدف: کیفیت مناسب با مصرف بهینه توکن). Claude نمی‌تواند مدل خودِ جلسه را وسط کار عوض کند؛ تقسیم کار از راه فراخوانی Subagent با مدل تعریف‌شدهٔ همان Agent انجام می‌شود.

| نوع کار | Agent | مدل |
|---|---|---|
| معماری، ADR، اثر روی Multi-Tenancy/وابستگی لایه‌ها | `architect` | opus |
| بازبینی امنیتی تغییرات حساس (احراز هویت، Tenant، ورودی کاربر، اسرار) | `security-reviewer` | opus |
| پیاده‌سازی Backend (C#/ASP.NET Core/EF Core، `/api/v1`) | `backend-developer` | sonnet |
| مدل داده، Migration، Index، صحت داده | `database-engineer` | sonnet |
| رابط کاربری (Razor و `ClientApp`) | `frontend-developer` | sonnet |
| تست، Build، Regression | `qa-engineer` | sonnet |
| بازبینی کیفیت Diff (غیرامنیتی) | `code-reviewer` | sonnet |
| CI/CD، publish، Smoke Test | `devops-release-engineer` | sonnet |
| مستندات، handoff، `WORK_LOG` | `documentation-engineer` | haiku |

قواعد اجرا:
1. در شروع هر کار، Claude نوع کار را به Agent(ها) نگاشت می‌کند و در یک خط به کارفرما می‌گوید کدام Agent چه بخشی را انجام می‌دهد.
2. استثنا برای صرفه‌جویی توکن: تغییر بسیار کوچک و تک‌فایلی (تایپو، یک متن، یک تنظیم جزئی) مستقیم انجام می‌شود، چون Subagent بدون سابقه شروع می‌کند و هزینهٔ خواندن دوبارهٔ زمینه دارد.
3. مالکیت فایل: هم‌زمان دو Agent «کامل» روی فایل‌های هم‌پوشان کار نمی‌کنند (`docs/AGENT_COORDINATION.md` قاعده ۳)؛ کارهای وابسته پشت‌سرهم و کارهای مستقل می‌توانند هم‌زمان باشند.
4. بازبینی‌کنندگان (`security-reviewer`، `code-reviewer`، `architect`) فقط‌خواندنی‌اند و کد را تغییر نمی‌دهند؛ اصلاح یافته‌ها با Agent پیاده‌ساز انجام می‌شود.
5. خروجی Agentها «گفته» است نه شاهد: Claude نتیجهٔ تست و Build را خودش بررسی یا از خروجی واقعی نقل می‌کند و ادعای اجرا بدون اجرا نمی‌نویسد (`AGENTS.md` بخش ۴ و ۵.۱).
6. هیچ Agent بدون تأیید صریح Commit/Push/Merge نمی‌کند (همان قواعد قبلی).

### گردش‌کار استاندارد یک قابلیت جدید

گردش‌کار کامل (بررسی Issue → طراحی → پیاده‌سازی → بازبینی Diff → تست → بازبینی امنیتی → مستندسازی → گزارش نهایی) دقیقاً همان است که در `AGENTS.md` بخش‌های ۱، ۳، ۴ و ۵ آمده است. نگاشت به Agentها:

1. بررسی درخواست/Issue/Git — توسط Agent در حال انجام کار (هر کدام).
2. بررسی معماری برای تغییرات مهم — `architect`.
3. پیاده‌سازی — `backend-developer`، `database-engineer` (برای Migration/مدل داده)، یا `frontend-developer` بر اساس حوزه.
4. بازبینی Diff و خطاها — `code-reviewer` (کیفیت/Regression عمومی)، یا `architect` برای تغییرات گسترده.
5. اجرای تست‌های مرتبط — `qa-engineer`.
6. بازبینی امنیتی متناسب با ریسک — `security-reviewer` (برای تغییرات حساس: احراز هویت، Multi-Tenancy، ورودی کاربر، اسرار).
7. Build/CI/Publish — `devops-release-engineer` (برای شکست CI یا تغییر Workflow/اسکریپت انتشار).
8. به‌روزرسانی مستندات و گزارش تحویل (`docs/handoffs/ISSUE-<شماره>-...md` و `docs/WORK_LOG.md`) — `documentation-engineer`.

برای تغییرات کوچک، مراحل ۲ و ۶ لازم نیست اجرا شوند. هیچ Agent مجاز به Commit/Push/Merge بدون تأیید صریح نیست؛ این قاعده از `AGENTS.md` بخش ۲ و ۵ است و برای همه Agentها بدون استثنا اعمال می‌شود.

### محدودیت‌های مشترک همه Agentها

- هیچ Agent به GitHub (Push/Merge)، اسرار، Connection String واقعی یا محیط Production بدون نیاز مشخص و تأیید صریح کاربر دسترسی نمی‌گیرد.
- هیچ Migration یا تغییر Schema بدون بررسی اثر و مجوز صریح کاربر اجرا نمی‌شود (مطابق `AGENTS.md` و `docs/DATABASE.md`).
- هیچ کلید API، رمز، Connection String محرمانه یا داده واقعی مشتری در فایل‌ها، کد یا گزارش‌ها قرار نمی‌گیرد.
- مدل هر Agent (`opus`/`sonnet`/`haiku`) طبق مقادیر رسمی پشتیبانی‌شده در فیلد `model` نسخه نصب‌شده Claude Code (2.1.306) است؛ اگر در حساب یا نسخه دیگری مدلی در دسترس نباشد، باید محدودیت گزارش و جایگزین پیشنهاد شود، نه جایگزینی بی‌اطلاع.
