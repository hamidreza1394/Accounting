@AGENTS.md

## Claude Code — تیم Agent و گردش‌کار

این بخش فقط مخصوص Claude Code است و چیزی از `AGENTS.md` را تکرار نمی‌کند؛ قرارداد کاری، قواعد غیرقابل‌مذاکره، استانداردهای کدنویسی و دستورات مرجع همان فایل (بالا) است و باید کامل رعایت شود. اینجا صرفاً تیم Subagentهای این مخزن و نحوه استفاده از آن‌ها تعریف می‌شود.

### مرجع مستندات (2026-10-21)
نقطه شروع: `docs/INDEX.md`. وضعیت قابلیت‌ها با واژگان D-030 (`Implemented`/`Verified`/`NotImplemented`/...) گفته می‌شود. لایسنس تجاری و زیرسیستم‌ها `NotImplemented` هستند؛ Activation قدیمی کد مرده است (D-028). ADR بعدی D-045 است (اصلاح 2026-10-24؛ پیش‌تر D-036 نوشته شده بود در حالی که `docs/DECISIONS.md` تا D-044 را ثبت کرده است؛ مرجع همیشه عنوان `docs/DECISIONS.md` است). گروه شرکتی و تلفیق: `docs/CORPORATE_GROUPS.md` (D-033..D-035)؛ در این مخزن `Company` همان Tenant است.

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

### قاعدهٔ الزامی تقسیم کار بین Agentها (دستور کارفرما، 2026-10-22؛ بازنگری‌شده)

وقتی کارفرما پرامپت کاری می‌دهد، Claude **ملزم است** کار را بر اساس نوع و اندازهٔ آن بین Subagentهای همین مخزن تقسیم کند. هدف: کمترین ریسک و ناهماهنگی، با مصرف بهینهٔ توکن. Claude نمی‌تواند مدل خودِ جلسه را وسط کار عوض کند؛ تقسیم کار با فراخوانی Subagent انجام می‌شود. Claude نقش **هماهنگ‌کننده (Lead)** را دارد: مالک قرارداد، ترتیب کارها، تلفیق و تأیید نهایی.

#### الف) انتخاب سطح تقسیم (بر اساس اندازهٔ کار)
| اندازه | روش |
|---|---|
| خیلی کوچک (تایپو، یک متن، تنظیم جزئی تک‌فایلی) | مستقیم توسط Lead؛ بدون Subagent (هزینهٔ خواندن دوبارهٔ زمینه) |
| متوسط (یک قابلیت متصل: سرور + کلاینت + تست) | **یک** Agent پیاده‌ساز برای کل برش عمودی (نه سه Agent جدا) + بازبینی مستقل |
| بزرگ یا حساس (Schema، احراز هویت، Multi-Tenancy، چند برش مستقل) | `architect` برای طرح، سپس پیاده‌سازی، سپس بازبینی‌های مستقل |

#### ب) نگاشت نوع کار به Agent و مدل
| نوع کار | Agent | مدل |
|---|---|---|
| معماری/ADR/اثر روی Tenant و لایه‌ها (فقط کار حساس یا مبهم) | `architect` | opus |
| بازبینی امنیتی (فقط تغییر حساس: احراز هویت، Tenant، ورودی کاربر، اسرار) | `security-reviewer` | opus |
| پیاده‌سازی برش عمودی؛ بک‌اند محض | `backend-developer` | sonnet |
| پیاده‌سازی برش عمودی با کلاینت؛ فقط UI | `frontend-developer` | sonnet |
| مدل داده، Migration، Index | `database-engineer` | sonnet |
| تست، Build، Regression | `qa-engineer` | sonnet |
| بازبینی کیفیت Diff | `code-reviewer` | sonnet |
| CI/CD، publish، Smoke Test | `devops-release-engineer` | sonnet |
| مستندات حساس: معماری، امنیت، ADR/تصمیم‌های فنی، قرارداد API، `PROJECT_STATUS`، وضعیت‌ها، handoff | `documentation-engineer` (پیش‌فرض `sonnet`)؛ برای تغییر محتوایی، بازبینی تخصصی Diff: معماری/ADR/API ← `architect`، امنیت ← `security-reviewer` | sonnet (بازبین: opus) |
| مستندات ساده و تکراری (افزودن ردیف WORK_LOG، اصلاح نگارشی) | `documentation-engineer` با `model: haiku` در فراخوانی | haiku |

برای برش عمودی، Agent پیاده‌ساز همه‌چیز آن قابلیت را (سرور، کلاینت، تست‌های همان قابلیت) انجام می‌دهد تا قرارداد بین لایه‌ها دو دست نشود. اگر کار واقعاً دو تخصص مستقل دارد (مثلاً Migration و UI)، قرارداد پیش‌تر نوشته و ثابت می‌شود (بخش پ).

#### پ) پروتکل جلوگیری از ناهماهنگی
1. **قرارداد اول:** پیش از فراخوانی هر Agent، Lead قرارداد مشترک را می‌نویسد: شکل API (مسیر، بدنه، کدهای خطا)، نام فیلدها، قواعد دامنه، معیارهای پذیرش. Agentها قرارداد را تغییر نمی‌دهند؛ اگر نیاز به تغییر دیدند، برمی‌گردند و گزارش می‌دهند.
2. **ابلاغ کامل:** Agent سابقهٔ گفتگو را نمی‌بیند. Lead در پرامپت می‌نویسد: هدف، قرارداد، فایل‌هایی که Agent **مالک** آن است، فایل‌هایی که نباید دست بزند، معیار پذیرش، فرمان‌های تست، و ارجاع به `AGENTS.md`.
3. **مالکیت فایل:** هر فایل در هر لحظه یک مالک دارد. کارهای وابسته پشت‌سرهم اجرا می‌شوند؛ هم‌زمانی فقط برای کارهای مستقل و بدون فایل مشترک (`docs/AGENT_COORDINATION.md` قاعده ۳).
4. **تلفیق توسط Lead:** پس از هر Agent، Lead Diff را می‌خواند و تطابق با قرارداد را می‌سنجد؛ پس از همهٔ Agentها یک Build و Test کامل یکپارچه اجرا می‌شود، نه فقط تست‌های هر بخش.
5. **بازبین مستقل و فقط‌خواندنی:** `code-reviewer` برای هر تغییر عادی؛ `security-reviewer` و `architect` علاوه بر آن فقط برای تغییر حساس. بازبین‌ها کد را تغییر نمی‌دهند؛ یافته‌ها را Agent پیاده‌ساز اصلاح می‌کند و Lead بازبینی را برای یافتهٔ مهم تکرار می‌کند.
6. **شاهد، نه گفته:** گزارش Agent شاهد نیست. Lead نتیجهٔ Build/Test را از خروجی واقعی می‌گیرد و هرچه اجرا نشده «اجرا نشد» ثبت می‌شود (`AGENTS.md` بخش ۴ و ۵.۱).
7. **یک شاخه، یک مسیر:** همهٔ Agentها روی شاخهٔ همان Issue کار می‌کنند؛ هیچ Agent بدون تأیید صریح Commit/Push/Merge نمی‌کند.

#### ث) دروازهٔ بازبینی پیش از PR (الزامی)
پیش از ایجاد هر PR برای کار تقسیم‌شده، Lead بازبینی نهایی را انجام می‌دهد؛ تا پایان بازبینی PR ساخته نمی‌شود.
1. **دامنه:** فقط Diff نهایی و خروجی واقعی Build/Test؛ کل کار یا زمینه از نو خوانده نمی‌شود.
2. **عمق به نسبت ریسک:** کار ساده و مستندات ساده: خود Lead Diff را می‌خواند. کار عادی: `code-reviewer`. کار حساس (احراز هویت، Tenant، Schema، ورودی کاربر، اسرار): `security-reviewer` و `architect` (opus) هم اضافه می‌شوند.
3. Lead نمی‌تواند مدل جلسه را عوض کند؛ «بازبین قوی» یعنی فراخوانی Agent بازبین با مدل opus، نه تغییر مدل Lead.
4. یافتهٔ مهم → اصلاح توسط Agent پیاده‌ساز → بازبینی دوباره فقط همان بخش.
5. گزارش پایانی (و handoff) باید بنویسد: بازبینی چه سطحی بود و چه یافته‌ای داشت.

#### ت) اندازه‌گیری
Lead برای هر کار تقسیم‌شده در گزارش پایانی می‌نویسد: کدام Agent چه کرد، **چند توکن مصرف کرد** (عددی که ابزار Agent در گزارش خود می‌دهد؛ اگر نداد «نامشخص»)، چند رفت‌وبرگشت لازم شد، و آیا تقسیم کار صرفه داشت؛ تا قاعده بر اساس تجربهٔ واقعی اصلاح شود.

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

برای تغییرات کوچک، مراحل ۲ و ۶ لازم نیست اجرا شوند. هیچ Agent مجاز به Commit/Push/Merge بدون تأیید صریح نیست؛ این قاعده از `AGENTS.md` بخش ۲ و ۵ است و برای همه Agentها بدون استثنا اعمال می‌شود.

### Skill، Plugin، MCP و کنترل‌های قطعی (قاعدهٔ دائمی هر وظیفه؛ 2026-10-23)

این بخش به تقسیم کار بالا و `AGENTS.md` چیزی نمی‌افزاید جز نحوهٔ تشخیص، فراخوانی و گزارش قابلیت‌ها؛ با هیچ‌کدام تعارض ندارد و در صورت اختلاف آن دو برنده‌اند.

1. **تشخیص و فعال‌سازی:** پس از مطالعهٔ مستندات (`AGENTS.md` بخش ۱) و پیش از شروع، Claude قابلیت‌های مرتبط را از **فهرست Skill و Agent همان جلسه** برمی‌دارد، نه از حافظه یا نام‌های این فایل؛ Skill را با ابزار `Skill` (یا `/نام` صریح) و Subagent را با ابزار `Agent` (یا نام صریح) فعال می‌کند. کار خیلی کوچک (جدول الف) Skill و Agent نمی‌خواهد؛ فراخوانی هم‌زمان همهٔ قابلیت‌ها ممنوع است.
2. **چهار وضعیت جدا:** (۱) نصب/پیکربندی‌شده: روی این دستگاه هست (`claude plugin list`، `.claude/agents/`)؛ (۲) در دسترس: در فهرست همین جلسه آمده؛ (۳) فراخوانی‌شده: ابزار `Skill`/`Agent` واقعاً صدا زده شد؛ (۴) اجراشده با نتیجه: خروجی قابل‌بررسی (دستور، Diff، گزارشی که Lead مستقلاً راستی‌آزمایی کرد) وجود دارد. فقط بالاترین وضعیتِ واقعاً رسیده‌شده گزارش می‌شود؛ «نصب‌شده» هرگز «استفاده‌شده» نیست.
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
   | UI/UX فارسی و RTL | `frontend-developer` | `faranou-ui-ux-design` |
   | تست و کیفیت فرانو | `qa-engineer` | `faranou-testing-quality` |
   | حسابداری، مالی و قواعد داده | `backend-developer` / `database-engineer` | `faranou-accounting-domain` |
   | توسعه عمومی فرانو | Agent حوزهٔ مربوط | `faranou-senior-developer` |
   | پیش از ادعای «تمام شد» | Lead | `superpowers:verification-before-completion` |

   Skillهای اختصاصی فرانو در `.claude/skills/` نسخه‌بندی شده‌اند (Issue #85 / PR #86). وجود فایل در مخزن به‌تنهایی ثابت نمی‌کند که Skill در جلسهٔ جاری در دسترس یا فراخوانی شده است؛ فهرست Skillهای همان جلسه را بررسی کن و فقط در صورت در دسترس بودن آن را فعال کن. در هر تعارض، `AGENTS.md`، این فایل و ADRها مقدم‌اند.
4. **تقدم:** Skill و Plugin ابزار کمکی‌اند و بر `AGENTS.md`، این فایل و ADRها غلبه نمی‌کنند. اگر فرایند یا خروجی Skill تعارض داشت (CDN یا فونت خارجی، Commit/Push/Merge خودکار، ساخت شاخه یا worktree جدا، تغییر معماری یا Schema بدون تأیید)، قاعدهٔ مخزن اجرا و تعارض گزارش می‌شود.
5. **در دسترس نبود:** محدودیت را صریح اعلام کن، جایگزین مناسب (Agent مخزن یا چک‌لیست دستی از `docs/`) را به کار ببر و در گزارش «در دسترس نبود» ثبت کن؛ هرگز ادعای اجرا نکن.
6. **پس از تغییر و گزارش:** تست‌های مرتبط و بازبینی مستقل به نسبت ریسک طبق بخش ث و `AGENTS.md` بخش ۴ اجرا می‌شود. گزارش پایانی (علاوه بر ت و `AGENTS.md` بخش ۵) چهار دستهٔ جدا دارد: **استفاده‌شده** (با شاهد)، **در دسترس ولی استفاده‌نشده** (با دلیل)، **در دسترس نبود**، **اجرانشده** (با دلیل).
7. **کنترل‌های قطعی و محدودیت‌هایشان:** متن این فایل رفتار را هدایت می‌کند و تضمین قطعی نیست. کنترل قطعی فقط CI (`.github/workflows/dotnet.yml`) و `permissions` در `.claude/settings.json` است: `deny` برای خواندن (و در نتیجه نوشتن) `.env*` (جز `.env.example`)، `appsettings.Local.json`، `secrets.json`، `*.pfx`، `*.p12`، مخزن User Secrets، `dotnet ef database drop` و Push اجباری (`--force`، `-f`، `+branch`)، حذفی (`--delete`) و آینه‌ای (`--mirror`)؛ `ask` برای Commit، Push، Merge، Rebase، `reset --hard`، `clean`، `restore`، `checkout --`، `branch -D`، `gh pr create/merge`، `gh release`، `dotnet ef database update`، `dotnet ef migrations remove`، `dotnet user-secrets list` و `rm -r`. محدودیت‌ها: قواعد Bash فقط شکل ظاهری فرمان را می‌سنجند (`/bin/rm`، `bash -c`، `git -C . push`، حذف شاخه با `git push origin :branch` و `sqlcmd` را نمی‌گیرند)؛ `Read deny` برای ابزارهای خواندن و فرمان‌های شناخته‌شده مثل `cat` اعمال می‌شود، نه اسکریپتی که خودش فایل را باز می‌کند؛ این‌ها Sandbox کامل نیستند. تنها Hook مخزن `SessionStart` برای نصب `dotnet` در جلسهٔ Cloud است (`scripts/dev/cloud-session-start.sh`، فقط با `CLAUDE_CODE_REMOTE=true`)؛ Hook بیرون از `permissions` اجرا می‌شود، پس تغییر `scripts/dev/*` و `.claude/*` را مثل پیکربندی اجرایی بازبینی کن. `allow` محلی قاعدهٔ `deny`/`ask` را لغو نمی‌کند؛ تغییر آن‌ها با ویرایش همان فایل و بازبینی PR است. Pluginها روی هر دستگاه جدا نصب می‌شوند و تغییر فایل‌های مخزن فعال‌شدن آن‌ها را تضمین نمی‌کند؛ نسخهٔ Claude Code را با `claude --version` بخوان (عدد 2.1.306 در این فایل مربوط به بررسی 2026-10-21 است).

### محدودیت‌های مشترک همه Agentها

- هیچ Agent به GitHub (Push/Merge)، اسرار، Connection String واقعی یا محیط Production بدون نیاز مشخص و تأیید صریح کاربر دسترسی نمی‌گیرد.
- هیچ Migration یا تغییر Schema بدون بررسی اثر و مجوز صریح کاربر اجرا نمی‌شود (مطابق `AGENTS.md` و `docs/DATABASE.md`).
- هیچ کلید API، رمز، Connection String محرمانه یا داده واقعی مشتری در فایل‌ها، کد یا گزارش‌ها قرار نمی‌گیرد.
- مدل هر Agent (`opus`/`sonnet`/`haiku`) طبق مقادیر رسمی پشتیبانی‌شده در فیلد `model` نسخه نصب‌شده Claude Code (2.1.306) است؛ اگر در حساب یا نسخه دیگری مدلی در دسترس نباشد، باید محدودیت گزارش و جایگزین پیشنهاد شود، نه جایگزینی بی‌اطلاع.
