# گزارش کار و تحویل Agentها

> گزارش‌ها را به انتهای فایل اضافه کن؛ سابقه قبلی را حذف یا بازنویسی نکن.

## 2026-10-17 — اصلاح ریشه‌ای DI برای DatabaseSchemaMigrator
- **دامنه:** بررسی خطای `Unable to resolve service for type 'Data.DatabaseContext' while attempting to activate 'Data.DatabaseSchemaMigrator'`.
- **علت اثبات‌شده:** `DatabaseContext` فقط در `Server/Program.cs` و فقط در شاخه `!setupRequired` با `AddDbContext` ثبت می‌شد، اما `IDatabaseSchemaMigrator` بدون شرط و در همه حالت‌ها ثبت می‌شد. در first-run که Connection String وجود ندارد، این دو registration هم‌مسیر نبودند و ساخت Migrator به وابستگی Scoped ثبت‌نشده می‌رسید.
- **اصلاح:** ثبت `Data.IDatabaseSchemaMigrator` و `Data.DatabaseSchemaMigrator` به همان شاخه `!setupRequired` منتقل شد؛ ثبت تکراری ایجاد نشد و مسیر Migration/`--migrate` پس از پیکربندی اتصال معتبر حفظ شد.
- **تست بازگشت:** `tests/Template.Tests/DatabaseDependencyInjectionTests.cs` با `ValidateOnBuild` و `ValidateScopes` اضافه شد تا resolve شدن Migrator با `DatabaseContext` ثبت‌شده بررسی شود.
- **مستندات:** handoff جدید `docs/handoffs/ISSUE-UNNUMBERED-database-migrator-di.md` ثبت شد.
- **اعتبارسنجی:** Build/Test محلی اجرا نشد؛ CI جدید باید روی Branch اصلاحی اجرا شود. هیچ SQL Server واقعی یا دیتابیس مشتری لمس نشد.
- **Git:** Branch `fix/database-migrator-di-registration`; آخرین Commit این تغییر در زمان ثبت، `a81b043fe330fad06a94452d6057ab1dcca85bed` است. PR ساخته نشد.

## 2026-10-17 — رفع ریشه‌ای `PendingModelChangesWarning` در `/DatabaseSetup`
- **دامنه:** شکست `MigrateAsync` در `Server/Infrastructure/DatabaseSetup/DatabaseConnectionSetupService.cs:127` با `Microsoft.EntityFrameworkCore.Migrations.PendingModelChangesWarning`.
- **علت اثبات‌شده:** مدل درست بود؛ `DatabaseContextModelSnapshot.cs` اشتباه بود. این Snapshot با `scripts/ef_model.py` + `scripts/generate_migrations.py` تولید شده بود، نه با `dotnet ef migrations add`. آن ابزار `.IsRequired()` بدون آرگومان را نادیده می‌گرفت، پس `PostCategory.Title` در Snapshot nullable نوشته شد در حالی که `PostCategoryConfiguration` آن را `.IsRequired()` کرده است؛ به‌همین دلیل `SqlServerIndexConvention` هم فیلتر `IX_PostCategory_ParentId_Title` را با `[Title] IS NOT NULL` اضافه نگه داشت. `MigrationsModelDiffer` فیلدهای `IsNullable` (ستون) و `Filter` (ایندکس) را مقایسه می‌کند، پس اختلاف واقعی و خطا قطعی بود.
- **اصلاح:** رفع پارس `.IsRequired()` و `[DatabaseGenerated(DatabaseGeneratedOption.None)]` در `scripts/ef_model.py`؛ اصلاح دقت Snapshot در `scripts/generate_migrations.py` (`.IsUnicode(true)` و `ValueGenerated`)؛ افزودن ماژول `scripts/migration_model.py`؛ بازتولید Snapshot؛ و Migration جدید `20261017090000_AlignSchemaWithModel` (DropIndex → UPDATE → AlterColumn → CreateIndex) بدون دست‌زدن به `InitialCreate`.
- **پنهان‌سازی انجام نشد:** `PendingModelChangesWarning` غیرفعال نشد، `ConfigureWarnings` اضافه نشد، `EnsureDeleted`/`EnsureCreated` استفاده نشد.
- **تست بازگشت:** `TheModelSnapshotDescribesTheCurrentModel` با `Database.HasPendingModelChanges()` در `tests/Template.Tests/DatabaseModelTests.cs`؛ به‌همراه گاردهای جدید در `.github/workflows/dotnet.yml` از جمله `dotnet ef migrations has-pending-model-changes`.
- **مستندات:** `docs/handoffs/ISSUE-UNNUMBERED-pending-model-changes.md` و بازنویسی بخش ابزار/بررسی‌های `docs/database-migrations.md`.
- **اعتبارسنجی:** `ef_model.py`، `generate_migrations.py --check`، `verify_migration_model.py` (با tree-sitter) و `check_static_assets.py` همه exit 0؛ آزمون منفی با Snapshot قبلی exit 1 با ۲۷ خطا. `dotnet build`/`dotnet test`/`dotnet ef` **اجرا نشد**: در sandbox فقط `github.com`, `codeload.github.com`, `api.github.com`, `registry.npmjs.org`, `pypi.org`, `files.pythonhosted.org` باز هستند و `builds.dotnet.microsoft.com`/`dot.net`/`api.nuget.org` با `curl: (35) SSL_ERROR_SYSCALL` رد می‌شوند. رفتار EF Core به‌جای اجرا، با خواندن سورس `dotnet/efcore@release/10.0` تأیید شد.
- **Git:** Branch `arena/e5e905ae-accounting`.

## 2026-10-17 — اصلاح گزارش قبلی: علت اصلی `PendingModelChangesWarning` ایندکس‌های ضمنی FK بود
- **دامنه:** همان خطای `PendingModelChangesWarning`؛ گزارش قبلی این بخش ناتمام بود.
- **چه چیزی در گزارش قبلی اشتباه بود:** نوشته شده بود علت، nullable بودن `PostCategory.Title` و فیلتر ایندکس است و بررسی‌های پایتون (`generate_migrations.py --check` و `verify_migration_model.py`) کافی‌اند. بعد از اجرا روی CI، آزمون `TheModelSnapshotDescribesTheCurrentModel` همچنان شکست (`total: 16`, `failed: 1`). آن دو انحراف واقعی بودند ولی **کل علت نبودند**؛ بررسی‌های پایتون هم چون از همان مدل ناقص پایتون استفاده می‌کردند، این اختلاف را نمی‌دیدند.
- **نحوهٔ اندازه‌گیری واقعی:** یک آزمون تشخیصی موقت، `IMigrationsModelDiffer.GetDifferences(...)` را صدا زد و نتیجه را در `/tmp/pending-model-changes.txt` نوشت؛ یک گام موقت در workflow آن را به‌صورت annotation منتشر کرد (لاگ کامل گام‌ها از API قابل دانلود نیست).
- **نتیجهٔ اندازه‌گیری:** دقیقاً **۸ `CreateIndexOperation`** و هیچ اختلاف دیگری: `IX_CompanyRolePermissions_PermissionId`، `IX_LoginLogs_UserId`، `IX_MenuItems_ParentId`، `IX_Pages_CreatorUserId`، `IX_Pages_PageCategoryId`، `IX_Permissions_ApplicationHandlerId`، `IX_UserCompanyMemberships_RoleId_TenantId`، `IX_Users_RoleId`.
- **علت ریشه‌ای:** `scripts/ef_model.py` فقط ایندکس‌های صریح `.HasIndex(...)` را جمع می‌کرد و `ForeignKeyIndexConvention` را پیاده‌سازی نکرده بود. این Convention برای هر foreign key یک ایندکس می‌سازد مگر ستون‌هایش از قبل ستون‌های ابتدایی یک Key یا ایندکس دیگر باشند (`AreIndexedBy`). چون `InitialCreate` هم از همان مدل تولید شده بود، این ایندکس‌ها در دیتابیس هم نبودند و Snapshot با Migration سازگار بود — اختلاف فقط با مدل واقعی ظاهر می‌شد.
- **اصلاح:** افزودن `are_indexed_by()` و حلقهٔ تولید ایندکس‌های ضمنی به `scripts/ef_model.py` (ترجمهٔ مستقیم `ForeignKeyIndexConvention.CreateIndex`/`AreIndexedBy`)؛ بازتولید `DatabaseContextModelSnapshot.cs`، `20261017090000_AlignSchemaWithModel.cs` و `.Designer.cs`. تعداد ایندکس‌ها از ۲۴ به ۳۲ رسید و دقیقاً با ۸ مورد گزارش‌شدهٔ EF Core یکی شد.
- **حذف موقت‌ها:** آزمون تشخیصی `PendingModelChangesDiagnostic.cs` و گام موقت workflow حذف شدند؛ گام دائمی `Publish the test output` نگه داشته شد.
- **اعتبارسنجی:** `ef_model.py` exit 0 (۳۲ ایندکس، ۸ ضمنی، دقیقاً منطبق بر خروجی `MigrationsModelDiffer`)؛ `generate_migrations.py --check` exit 0؛ `verify_migration_model.py` exit 0؛ `check_static_assets.py` exit 0. آزمون منفی: با حذف `b.HasIndex("RoleId")` از Snapshot، `verify_migration_model.py` با exit 1 و ۲ خطا شکست. Build/Test/`has-pending-model-changes` روی GitHub Actions اجرا شدند و سبز شدند: https://github.com/hamidreza1394/Accounting/actions/runs/39039125915 (هر ۱۳ گام success، از جمله `Check for pending model changes` با `dotnet ef` 10.0.13).
- **Git:** Branch `arena/e5e905ae-accounting`؛ PR https://github.com/hamidreza1394/Accounting/pull/25.

## 2026-10-17 — اصلاح شکست CI در PR #27 و افزودن آزمون رفتاری toggle رمز
- **دامنه:** ادامهٔ کار روی PR https://github.com/hamidreza1394/Accounting/pull/27 (شاخهٔ `fix/faranou-login-password-toggle`). آخرین commit `c61ffe8bb3c8c88df996a4a173a48e18602b2bef` در GitHub Actions شکست خورده بود.
- **علت شکست CI:** آزمون `FaranouMarkup_UsesPasswordToggleButtonAndMatchingControlId` در `tests/Template.Tests/LoginPasswordToggleTests.cs` با گزارء `Assert.DoesNotContain("type=\"submit\"", markup[toggleIndex..])` رشتهٔ `type="submit"` را در کل بازماندهٔ مارکاپ پس از toggle جستجو می‌کرد، که شامل دکمهٔ واقعی submit پایین فرم است (`<button type="submit" class="taraz-submit">`). خروجی CI: `total: 52`, `failed: 1`, `succeeded: 51`.
- **اصلاح:** گزارء به‌جای کل بازمانده، فقط **تگ آغازین خود دکمهٔ toggle** را برش می‌دهد (از نزدیک‌ترین `<button` پیش از `data-password-toggle` تا نخستین `>` پس از آن). قرارداد حفظ می‌شود: `type="button"`، `aria-controls="ViewModel_Password"`، بدون `type="submit"`.
- **اعتبارسنجی محلی پیش از push:** پایتون در `/home/z/my-project/scripts/verify_test_logic.py` هر سه گزارء اصلاح‌شده را تأیید کرد.
- **نتیجهٔ CI پس از push commit `e947489`:** سبز — هر دو run `39062817222` و `39062820291` با `conclusion: "success"` تکمیل شد؛ وضعیت PR `MERGEABLE`، `mergeStateStatus: CLEAN`.
- **بررسی نوع آزمون موجود:** هر دو آزمون `FaranouMarkup_*` و `FaranouScript_*` صرفاً **ساختاری** بودند — متن فایل `.cshtml`/`.js` را می‌خوانند و الگوی رشته‌ای می‌زدند. هیچ DOM، هیچ اجرای JS، هیچ کلیک واقعی روی toggle. این بازخورد که «آیا toggle واقعاً رمز را نمایش/پنهان می‌کند» را پوشش نمی‌داد.
- **افزودن آزمون رفتاری:** پروژهٔ جدید `tests/Template.Tests.Js/` با `jsdom@^24` و `node:test` داخلی Node 20. هفت آزمون واقعی DOM-level:
  1) toggle و password در مارکاپ هست؛ 2) وضعت اولیه `password.type == "password"` و `aria-pressed == "false"`؛ 3) کلیک اول `type == "text"` و `aria-pressed == "true"`؛ 4) کلیک دوم بازگشت به `password`؛ 5) سه کلیک به text (toggle ایدمپوتنت)؛ 6) `aria-label` بین عبارت نمایش و پنهان toggle می‌شود؛ 7) `site.js` داخل `.taraz-login-form` دوباره روی toggle فعال نمی‌شود (تطابق `.not('.taraz-login-form [data-password-toggle]')`).
- **شبیه‌سازی خروجی Razor در آزمون:** تابع `buildLoginDocument` عبارات `@(isRightToLeft ? "X" : "Y")` را به شاخهٔ RTL و `asp-for="ViewModel.Password"` را به `id="ViewModel_Password" name="ViewModel.Password"` گسترش می‌دهد — دقیقاً مطابق خروجی ASP.NET Core در زمان درخواست. این شبیه‌سازی در کامنت‌های فایل آزمون توضیح داده شده.
- **CI:** گام‌های `Setup Node 20` و `Behavioral tests (jsdom)` به `.github/workflows/dotnet.yml` افزوده شد. گام `Publish the failure details` برای شامل‌کردن `behavioral-test.log` و `npm-install.log` به‌روزرسانی شد.
- **اعتبارسنجی محلی:** `node --test` ۷ آزمون، ۷ pass، 0 fail (مدت ~۵۵۰ms). `dotnet build`/`dotnet test` در sandbox اجرا نشدند (نبود SDK .NET) — وابسته به CI GitHub Actions برای تأیید نهایی.
- **مستندات:** `docs/handoffs/ISSUE-UNNUMBERED-pr27-ci-fix-and-behavioral-test.md` ساخته شد (قالب `TEMPLATE.md`).
- **Git:** Branch `fix/faranou-login-password-toggle`؛ PR https://github.com/hamidreza1394/Accounting/pull/27؛ commitهای این مرحله `e947489` و commit جدیدتر با آزمون رفتاری. **Merge نشد** — منتظر تأیید مسئول.

## 2026-10-19 — Issue #30: تکمیل و اصلاح احراز هویت صفحه ورود

- **وضعیت ورودی:** `LoginModel` سرویس `AuthenticationService` را با `new` می‌ساخت، `DbContext` تزریق‌شده را در `finally` Dispose می‌کرد، رمز را فقط از `ViewModel` و نه از `ModelState` پاک می‌کرد (امکان بازگرداندن رمز در HTML)، و پس از ورود موفق بدون مقصد موقت به `ReturnUrl`/`Index` می‌رفت. رمز عبور با SHA-256 بدون salt ذخیره می‌شد و ستون `Users.Password` برابر `varchar(44)` بود.
- **جریان ورود:** DI برای `AuthenticationService` (Scoped) و `PasswordHashService` (Singleton) در `Program.cs`؛ PageModel از `BasePageModel` بدون `DbContext`؛ Post/Redirect/Get با پیام موفقیت در TempData؛ `ClearPassword()` حذف هم‌زمان property و کلید `ModelState`؛ `GetLocalReturnUrl()` فقط با `Url.IsLocalUrl`.
- **مقصد موقت:** صفحه جدید `Server/Pages/Account/LoginSuccess.cshtml(.cs)` با `[Authorize]`، فقط پیام «ورود به حساب با موفقیت انجام شد. بخش‌های نرم‌افزار در مراحل بعدی تکمیل خواهند شد.» (کلید جدید `Successes.LoginSucceededPendingFeatures`). `/Index` دست‌نخورده ماند.
- **رمز عبور:** `PasswordHashService` با `PasswordHasher<Domain.User>`؛ پذیرش هش قدیمی به‌صورت `SuccessRehashNeeded` و ارتقای آن پس از ورود موفق؛ `SimulateVerification` برای نام کاربری ناموجود؛ تبدیل هش نامعتبر به `Failed` به‌جای `FormatException`؛ مسیر ایجاد کاربر ادمین نیز به همین سرویس منتقل شد.
- **طرح داده:** `Constants.FixedLength.PasswordHash = 255`، `Domain.User.Password` با `MaxLength(255)`، Migration `20261019071146_WidenUserPasswordHash` (بدون تغییر داده؛ Snapshot فقط ۲ خط تغییر).
- **شکست CI و اصلاح ابزار:** اجرای اول CI در گام «Check the generated migrations» شکست خورد، چون Migration با `dotnet ef` ساخته شده بود و این مخزن تولید فایل‌ها را با `scripts/generate_migrations.py` اجباری می‌کند. سه اصلاح انجام شد: (۱) اسکریپت حالا مهاجرت جاری را با `CURRENT_MIGRATION_*` می‌شناسد و عملیات را از تفاضل مهاجرت قبلی با مدل می‌سازد، نه با بازنویسی `AlignSchemaWithModel`؛ (۲) در `migration_model.py` مقایسه‌ی facetها از truthiness به مقایسه‌ی مقدار تغییر کرد (تغییر خالص طول `varchar(44)`→`varchar(255)` دیده نمی‌شد و Migration خالی تولید می‌شد)؛ (۳) `verify_migration_model.py` مهاجرت جدید را با designer مهاجرت قبلی می‌سنجد.
- **گاردهای Migration پس از اصلاح:** `generate_migrations.py --check` → up to date؛ `verify_migration_model.py` → match؛ `dotnet ef migrations has-pending-model-changes` → no pending changes.
- **تست:** فایل جدید `tests/Template.Tests/LoginAuthenticationTests.cs` با ۳۷ آزمون (جریان PageModel، سرویس هش، پیکربندی). نتیجه واقعی: `dotnet build -c Release` → `0 Error(s)`؛ `dotnet test` → `total: 90`, `failed: 0`, `succeeded: 90`؛ `scripts/check_static_assets.py` → PASS؛ `git diff --check` → پاک.
- **اجرانشده (صادقانه):** Migration روی SQL Server واقعی و آزمون End-to-End روی مرورگر در این محیط ممکن نبود.
- **Git:** Branch `fix/secure-login-authentication`؛ Issue #30؛ PR https://github.com/hamidreza1394/Accounting/pull/31 (commitهای `72f6c44`, `00dcb01`, `82a1f3a`). **CI سبز** — هر دو run `39227188912` و `39227195897` با `pass`؛ `mergeStateStatus: CLEAN`. **Merge نشد** — منتظر تأیید مسئول.

## 2026-10-20 — ارتقای معماری فرانو (UNNUMBERED)
- **پایه:** `main` @ `f539a36`؛ شاخه `feature/faranou-modern-architecture`.
- **Backend:** `CompanyAccessService` (Tenant از عضویت فعال در دیتابیس، هر درخواست)، API v1 در `/api/v1` با Cookie هم‌مبدأ و Anti-forgery، 401/403 برای API، `no-store`، Claim شناسه کاربر و حذف Claimهای `TenantId`.
- **اصلاحات:** خطای داشبورد دیگر بلعیده نمی‌شود؛ خروج فقط POST؛ پیام ساختگی ثبت‌نام حذف شد؛ منوی مدیریت فقط برای Admin.
- **Frontend:** `ClientApp` (React 19.3 / TS 7 strict / Vite 8 / Tailwind 4)، داشبورد RTL در `/app` با داده واقعی و حالت خالی/خطا؛ تاریخ شمسی فقط در نمایش؛ بدون CDN.
- **تست:** CI روی `387e703` سبز (Build + همه تست‌های .NET + گاردهای Migration)؛ محلی: typecheck، ۱۸ تست Vitest، Build، بررسی آفلاین، رندر Chromium. Build/Test .NET در محیط Agent اجرا نشد (NuGet مسدود)؛ SQL Server واقعی و E2E اجرا نشد.
- **ADR:** D-011، D-012، D-013؛ D-001 جزئاً و D-003 کاملاً منسوخ علامت خوردند.
- **گزارش:** `docs/handoffs/ISSUE-UNNUMBERED-faranou-modern-architecture.md`. **Merge نشد.**

## 2026-10-20 — بازبینی امنیتی پس از PR #38 (UNNUMBERED)
- **پایه:** `main` @ `207d81f`؛ شاخه `security/post-pr38-hardening`.
- اعتبارسنجی مجدد نشست، CSP/هدرها، Rate Limiting، Exception Handler اختصاصی API، انتشار قطعی همراه کلاینت و Smoke Test، بازنویسی و تست بررسی آفلاین، ۲۷ تست Integration HTTP روی SQL Server 2022 در CI.
- **CI:** ۱۶۴ تست .NET موفق (۰ Skip) روی SQL Server؛ اسکن NuGet بدون مورد؛ Jobهای client و publish موفق. محلی: ۲۴ تست Vitest، Build و بررسی آفلاین، CSP در Chromium.
- **اجرانشده:** Build/Test .NET در محیط Agent، `publish.ps1`، E2E مرورگر با دیتابیس.
- **گزارش:** `docs/handoffs/ISSUE-UNNUMBERED-post-pr38-security-review.md`. **Merge نشد.**

## 2026-10-20 — پیگیری آمادگی Production (UNNUMBERED)
- **پایه:** `main` @ `2511907`؛ شاخه `security/production-readiness-followup` (کار جلسه قبلی Claude بازبینی و تکمیل شد).
- محافظ پوشه خروجی انتشار (Bash/PowerShell با تست، اجرای Windows در CI)، Proxy مورد اعتماد و هشدار `AllowedHosts`، مهاجرت Master، حذف inlineهای Razor و اجباری‌شدن CSP، گزارش CSP امن، اصلاح نام فونت، E2E مرورگر با SQL Server.
- **رفع:** E2E درخواست‌های API لغوشده هنگام ناوبری را به‌اشتباه «منبع ایستای ناموفق» می‌شمرد.
- **CI:** ۲۰۴ تست .NET، ۱۲ تست E2E، تست‌های Bash و PowerShell (Linux و Windows)، انتشار Linux و Windows، همه موفق.
- **گزارش:** `docs/handoffs/ISSUE-UNNUMBERED-production-readiness-followup.md`. **Merge نشد.**

## 2026-10-21 — ممیزی جامع، تکمیل تیم Agent، و بررسی قطعی شکست CI (شاخه `fix/comprehensive-quality-hardening`)
- **پایه:** `main` @ `8244bf3` (ادغام PR #44؛ بدون تغییر ثبت‌نشده در ابتدای کار).
- **تیم Subagent تکمیل شد:** سه Agent جدید اضافه شدند — `database-engineer` (sonnet، دسترسی کامل)، `devops-release-engineer` (sonnet، دسترسی کامل)، `code-reviewer` (sonnet، فقط‌خواندنی+Bash) — در کنار ۶ Agent قبلی (`architect`, `backend-developer`, `frontend-developer`, `qa-engineer`, `security-reviewer`, `documentation-engineer`) که هیچ‌کدام حذف/ادغام/جایگزین نشدند. جدول و گردش‌کار استاندارد در `CLAUDE.md` برای ۹ Agent به‌روزرسانی شد.
- **رفع تناقض نسخه .NET در مستندات:** `docs/AGENT_START_PROMPT.md`، `docs/prompts/CODEX_PROMPT.md` و `docs/prompts/COPILOT_PROMPT.md` که هنوز `.NET 9 / C#13 / EF Core 9` را به‌عنوان وضعیت فعلی ذکر می‌کردند، با `.NET 10 LTS / C#14 / EF Core 10` (مطابق D-007 و بررسی واقعی `global.json`/تمام `.csproj`/`Directory.Packages.props`) هماهنگ شدند. `docs/AGENT_COORDINATION.md` نیز در بخش اولویت‌بندی به‌روزرسانی شد (آیتم ۲ به‌عنوان انجام‌شده علامت خورد).
- **بررسی قطعی شکست CI (اولویت اول Mission 3):** Run اصلی CI روی `main`@`8244bf3` (شناسه `39483239598`) در Job `publish` با `FAIL login page renders the anti-forgery token` شکست خورده بود. بررسی کد (`Program.cs` پیکربندی Cookie/Antiforgery، `Login.cshtml` — توکن بدون شرط رندر می‌شود، `Login.cshtml.cs` `OnGet` — بدون دسترسی DB) هیچ علت قطعی کد را نشان نداد. بازتولید محلی با `scripts/publish.sh` + `scripts/smoke-published-app.sh` شش بار اجرا شد و هر ۶ بار موفق بود. **تأیید قطعی با شواهد واقعی CI:** همان Run با `gh run rerun 39483239598` دوباره اجرا شد؛ Attempt #2 با `conclusion: success` در تمام Jobها از جمله `publish` تکمیل شد — روی دقیقاً همان commit، بدون هیچ تغییر کد. نتیجه: شکست Flaky/گذرا بوده، نه Regression کد؛ `scripts/smoke-published-app.sh` بدون شواهد یک نقص واقعی تغییر داده نشد (تغییر بدون شواهد، «تضعیف برای سبزشدن» محسوب می‌شد). `docs/REPOSITORY_AUDIT.md` (شناسه AUD-2026-10-21-01) با این شواهد به‌روزرسانی شد؛ شدت از High به Medium کاهش یافت.
- **اجرای واقعی تست‌ها روی این شاخه (نکته ابزار .NET 10 SDK):** `dotnet test Template.sln --configuration Release` با خطای CLI جدید `Specifying a solution for 'dotnet test' should be via '--solution'` شکست خورد (تغییر رفتار Microsoft.Testing.Platform در SDK 10.0.113)؛ با `dotnet test --solution Template.sln --configuration Release` رفع شد.
  - **نتیجه واقعی `dotnet test`:** `total: 204, failed: 0, succeeded: 204, skipped: 0` (مدت ~۱۴ ثانیه).
  - **نتیجه واقعی `python3 scripts/check_static_assets.py`:** PASS — هیچ مرجع CDN یافت نشد.
  - **نتیجه واقعی `dotnet format Template.sln --verify-no-changes --no-restore`:** بدون تغییر لازم (فقط هشدار Analyzer، بدون خطای فرمت).
  - **نتیجه واقعی کلاینت React (`ClientApp`):** `npm ci` (۱۲۲ بسته، یک آسیب‌پذیری High در `vite`/`launch-editor` — همان مورد ثبت‌شده در AUD-2026-10-21-03، فقط ابزار توسعه)، `npm run typecheck` → بدون خطا، `npm test -- --run` → `4 فایل، 24 تست، همه Pass`، `npm run build` → موفق (bundle در `Server/wwwroot/app`)، `npm run check:offline` → PASS (۱۲ منبع محلی، بدون مرجع خارجی).
- **اجرانشده (صادقانه):** تست‌های یکپارچه‌سازی مبتنی بر SQL Server واقعی (`CompanyAccessServiceTests`, `ApiEndpointTests` با `FARANOU_TEST_SQLSERVER`) — SQL Server/LocalDB در این محیط موجود نیست؛ بازبینی رفتاری خط‌به‌خط کد احراز هویت/چندمستاجری/CSP فراتر از آنچه در ممیزی ۲۰۲۶-۱۰-۱۶ و ۲۰۲۶-۱۰-۲۱ (مرحله یک) انجام شد؛ تصمیم دربارهٔ مسیر مرده Google OAuth (AUD-2026-10-21-02) و ارتقای `vite` (AUD-2026-10-21-03) — هنوز باز و منتظر تصمیم کاربر/اولویت‌بندی بعدی.
- **Git:** Branch `fix/comprehensive-quality-hardening` از `main`@`8244bf3`. Commit/Push/PR پس از این یادداشت انجام می‌شود (طبق اجازهٔ صریح مأموریت سوم).
- **گزارش:** `docs/handoffs/ISSUE-UNNUMBERED-comprehensive-quality-hardening.md`.

## 2026-10-21 — مرجع رسمی معماری و مستندات (UNNUMBERED، شاخه `docs/architecture-source-of-truth`)
- **پایه:** `main` @ `1d39dbf9e0843045e00441bf2778267e9764a3f6`. فقط مستندات تغییر کرد (کد، Schema، Migration، پیکربندی و بسته‌ها دست‌نخورده).
- **جدید:** `docs/INDEX.md`، `PRODUCT_REQUIREMENTS.md`، `REQUIREMENTS_TRACEABILITY.md`، `LICENSING.md`، `LICENSING_DATA_MODEL.md` (ERD واقعی/پیشنهادی)، `LICENSING_WORKFLOWS.md`، `LICENSING_THREAT_MODEL.md`، ADRهای D-020..D-030 (جدول وضعیت D-001..D-030 و توضیح D-009)، handoff.
- **اصلاح ناهمخوانی‌ها:** Activation قدیمی کد مرده است نه فعال (D-028)؛ Migration وجود دارد؛ ROADMAP/TESTING/SECURITY/PROJECT_STATUS هم‌تراز با کد و CI؛ بخش ممیزی جدید AUD-2026-10-21-08..20.
- **شاهد اجرا:** فقط CI `39585708662` روی commit مبنا (۶ Job سبز، ۲۰۴ تست .NET روی SQL Server 2022، ۱۲ E2E). **Build/Test در این محیط اجرا نشد** (dotnet نیست).
- **تصمیم‌های باز (`NeedsApproval`):** دوره ارفاق ۳۰ روزه، فهرست زیرسیستم‌ها، الگوریتم امضا، مسیر گذار Activation قدیمی (وجود مشتری Production: نامشخص)، رابطه Company ابری با مشتری، `LICENSE`.
- **گزارش:** `docs/handoffs/ISSUE-UNNUMBERED-architecture-source-of-truth.md`. Merge نشد.

## 2026-10-21 — انتخاب شرکت و دوره مالی پس از ورود (UNNUMBERED، شاخه `feature/company-fiscal-period-selection`)
- **پایه:** شاخه `docs/architecture-source-of-truth` (`6a731db`، PR #46). Merge نشد.
- **جدید:** `/api/v1/context`، Cookie زمینه کاری وابسته به هر ورود، صفحه انتخاب و مسدود، نوار زمینه، کیت UI کوچک، ADR D-031. بدون تغییر Schema/Migration.
- **شاهد محلی:** ۲۴۵ تست .NET (Provider حافظه‌ای)، ۳۹ تست کلاینت، Build و بررسی آفلاین موفق، بررسی بصری با API شبیه‌سازی‌شده.
- **اجرانشده:** CI، E2E و هر تست روی SQL Server (در محیط موجود نیست).
- **`NeedsApproval`:** «دوره معتبر = Open»؛ دوره‌های بسته/بایگانی/آینده؛ تغییر زمینه حین نشست.
- **گزارش:** `docs/handoffs/ISSUE-UNNUMBERED-company-fiscal-period-selection.md`.

## 2026-10-21 — اعتبار دوره مالی و تغییر زمینه حین نشست (UNNUMBERED، همان شاخه `feature/company-fiscal-period-selection`)
- **تصمیم کارفرما:** ADR D-032. Open=ورود عادی، Closed=فقط مشاهده/گزارش (`accessMode`)، Archived و دوره بیگانه/خارج دسترسی=ممنوع؛ تغییر زمینه حین نشست مجاز با بررسی سرور و ممیزی.
- **کد:** سیاست در `WorkContext.cs`، ممیزی `WorkContextSelected/Changed/SelectionDenied` در `WorkContextService.cs`، فیلد `accessMode` در API؛ کلاینت: نشان «فقط مشاهده»، پیش‌انتخاب بدون ابهام، بازسازی داشبورد با کلید شرکت+دوره؛ اصلاح انتخاب Radio در E2E (کلیک روی Label) و سناریوهای جدید.
- **شاهد محلی:** ۲۵۳ تست .NET (Provider حافظه‌ای)، ۴۸ تست کلاینت، Build/typecheck/offline/static assets موفق. **اجرانشده:** E2E و تست روی SQL Server (فقط CI).
- **باقی:** رفتار `Archived`/آینده `NeedsApproval`؛ اِعمال `AllowsWrites` روی Endpointهای نوشتن آینده.


## 2026-10-22 — ممیزی آمادگی 100+ کاربر هم‌زمان ابری (Issue #49)
- **دامنه:** بررسی مستندات و فایل‌های مرتبط در مخزن GitHub؛ معماری Modular Monolith، .NET 10/EF Core 10/SQL Server، Razor Pages + API `/api/v1` + React، Rate Limiting، Reverse Proxy و CI.
- **نتیجه اصلی:** ظرفیت 100 کاربر `NotVerified` است. در جست‌وجوی انجام‌شده سناریوی بار قابل تکرار در مخزن/CI پیدا نشد؛ تعداد نمونه‌های Production باز است؛ Rate Limiting فعلی per-instance است؛ ماژول‌های حسابداری اصلی و تست رقابت نوشتن مالی هنوز پیاده‌سازی نشده‌اند.
- **مستندات:** افزوده شد `docs/CLOUD_CONCURRENCY_READINESS.md` و `docs/prompts/CLOUD_CONCURRENCY_IMPLEMENTATION_PROMPT.md`; اسناد INDEX، ARCHITECTURE، TESTING، DEPLOYMENT، ROADMAP، REPOSITORY_AUDIT و PROJECT_STATUS هماهنگ شدند. Handoff در `docs/handoffs/ISSUE-49-cloud-concurrency-readiness.md`.
- **تغییر کد/DB:** هیچ کد اجرایی، Schema، Migration، نسخه بسته یا topology استقرار تغییر نکرد.
- **اجرا نشد:** Restore/Build/Test و Load/Stress/Soak؛ این دور فقط ممیزی از طریق مخزن GitHub بود. ظرفیت و رفتار runtime از روی کد ادعا نشده‌اند.
- **گام بعدی:** Agent باید پرامپ اجرایی را طبق `AGENTS.md` در شاخه/PR مستقل اجرا کند؛ هیچ merge خودکار مجاز نیست.

## 2026-10-22 — مستندسازی قاعدهٔ تقسیم کار بین Subagentها (UNNUMBERED، شاخه `docs/agent-delegation-rule`)
- **دامنه:** فقط مستندات؛ کد، Schema، Migration و `docs/DECISIONS.md` تغییر نکرد و ADR جدید ثبت نشد (رویهٔ کاری است، نه تصمیم معماری).
- **تغییر:** بخش «تقسیم کار بین Subagentهای Claude Code» به `docs/AGENT_COORDINATION.md` افزوده شد (سطح تقسیم، نگاشت کار به Agent/مدل، پروتکل ناهماهنگی، اندازه‌گیری، توضیح «چرا همه‌چیز با بالاترین مدل نه»)؛ ارجاع در `docs/INDEX.md`. منبع اصلی همچنان `CLAUDE.md`.
- **وضعیت:** `Accepted` به‌عنوان رویهٔ کاری؛ بر اساس داده در این پروژه اندازه‌گیری نشده است.
- **اجرانشده:** تست/Build لازم نبود (بدون تغییر کد).
- **Git:** PR #50، ادغام نشده. گزارش: `docs/handoffs/ISSUE-UNNUMBERED-agent-delegation-rule.md`.

## 2026-10-22 — دروازهٔ بازبینی پیش از PR و ثبت توکن Agentها (UNNUMBERED، شاخه `docs/agent-pre-pr-gate`)
- **دامنه:** فقط مستندات (`CLAUDE.md`، `docs/AGENT_COORDINATION.md`)؛ کد، Schema و `DECISIONS.md` تغییر نکرد؛ ADR جدید نیست.
- **تغییر:** بند «دروازهٔ بازبینی پیش از PR» (دامنه Diff و نتیجهٔ واقعی تست، عمق به نسبت ریسک، بدون PR تا پایان بازبینی) و افزودن مصرف توکن هر Agent به اندازه‌گیری. وضعیت: `Accepted` به‌عنوان رویهٔ کاری؛ بهینه‌بودن اندازه‌گیری نشده.
- **اجرانشده:** تست/Build لازم نبود. ادغام نشده؛ گزارش: `docs/handoffs/ISSUE-UNNUMBERED-agent-pre-pr-gate.md`.

## 2026-10-22 — زیرساخت گروه شرکتی، گزارش تجمیعی و تلفیقی (UNNUMBERED، شاخه `feature/corporate-groups-consolidation`)
- مستندات پیش از کد: `docs/CORPORATE_GROUPS.md`، `docs/CORPORATE_GROUPS_DATA_MODEL.md`، ADRهای D-033..D-035؛ اصلاح شماره ADR بعدی (D-036) در `CLAUDE.md`/`AGENTS.md`/`INDEX.md`؛ یافته‌ها AUD-2026-10-22-01..04.
- کد: مدل و قواعد در `Domain/CorporateGroups/`، شش پیکربندی EF، کلید جایگزین `FiscalPeriods(Id, TenantId)`، Migration `20261022150943_AddCorporateGroups`، `HasPermissionAsync`، `CorporateGroupService`؛ گسترش گاردهای Migration پایتونی برای Migrationهای افزاینده جدول.
- آزمون واقعی: ۳۰۵ از ۳۰۵ روی SQL Server 2022؛ هر دو گارد Migration و `has-pending-model-changes` سبز؛ `apply_migrations_to_testdb.sh` روی دیتابیس آزمایشی.
- بازبینی مستقل امنیتی و کیفیت: بدون یافته Critical/High؛ یافته‌های Medium/Low اصلاح یا به‌عنوان ریسک ثبت شدند.
- Commit/Push/PR انجام نشد. جزئیات: `docs/handoffs/ISSUE-UNNUMBERED-corporate-groups-consolidation.md`.


## 2026-10-22 — اصلاحات ممیزی پس از ادغام گروه شرکتی (Issue #54)
- PR #53 در `main` ادغام شده است (Merge commit `1d30436d25b4f1e76b95e76a764afe249fd84f6f`); CI اجرا [39826940050](https://github.com/hamidreza1394/Accounting/actions/runs/39826940050) موفق گزارش شده است. این واقعیت جایگزین وضعیت تاریخی ثبت‌شده در گزارش اولیه می‌شود.
- یافته امنیتی: هنگام رد `CorporateGroupCreate`، شناسه شرکت درخواستی بدون اثبات دسترسی در `SecurityAuditEvent.TenantId` نوشته می‌شد؛ اصلاح شد تا شناسه تأییدنشده در فیلد Tenant/Entity ثبت نشود و تست رگرسیون اضافه شد.
- مستند مدل داده با ایندکس واقعی یکتا `(OwnerCompanyId, Code)` هماهنگ شد؛ D-033/D-034 و وضعیت پروژه به‌روزرسانی شدند.
- مرز `CustomerAccount` در ابر مشترک و قاعده چرخه کنترلی همچنان `NeedsApproval` است؛ API/UI گروه، Schema مشتری و Migration جدید اضافه نشد.
- **تست‌های این تغییر:** در این محیط از طریق GitHub API قابل اجرا نبودند؛ CI باید روی PR اجرا شود. هیچ دیتابیس تولیدی لمس یا Migration اجرا نشد.
- PR اصلاحی: [Issue #54](https://github.com/hamidreza1394/Accounting/issues/54)؛ شاخه `fix/issue-54-corporate-groups-audit`.

## 2026-10-23 — قواعد دائمی Claude Code: Skill/Plugin، کنترل‌های قطعی و هماهنگی documentation-engineer (UNNUMBERED، شاخه `docs/claude-code-standing-rules`)
- **دامنه:** فقط فایل‌های دستورالعمل و تنظیمات (`CLAUDE.md`، `.claude/settings.json`، `.claude/agents/documentation-engineer.md`، `docs/AGENT_COORDINATION.md`، `docs/INDEX.md` و سه فایل پرامپت)؛ کد محصول، Schema، CI و `DECISIONS.md` تغییر نکرد؛ ADR جدید نیست؛ Hook اضافه نشد.
- **تغییر:** بخش «Skill، Plugin، MCP و کنترل‌های قطعی» در `CLAUDE.md` (تفکیک چهار وضعیت نصب/در دسترس/فراخوانی/اجرا، نگاشت حوزه به Agent و Skill اختیاری، تقدم قواعد مخزن، قالب گزارش)؛ فایل جدید `.claude/settings.json` فقط با `permissions` (`deny` و `ask`)؛ `documentation-engineer` به پیش‌فرض `sonnet` و جدول ب با بازبینی تخصصی مستندات حساس هماهنگ شد؛ ارجاع منسوخ «ADR بعدی D-031» در `AGENT_COORDINATION.md` و سه فایل پرامپت به D-036 اصلاح شد (شاهد: `DECISIONS.md` و `INDEX.md`).
- **آزمون واقعی** (Claude Code 2.1.309، جلسه headless در دایرکتوری موقت جدا): قواعد `deny` روی `.env`، `Server/.env` تو در تو، `secrets.json`، نوشتن روی `.env.local`، `git push -f/-fu/+main` و `dotnet ef --project … database drop` کار کردند؛ `.env.example`، `appsettings.json` و pushهای بی‌خطر به‌اشتباه رد نشدند. اثر قواعد `ask` به‌صورت تجربی تفکیک‌پذیر نبود و فقط بر پایه مستندات رسمی گزارش شده است. در یک جلسه headless فقط‌خواندنی، وظیفهٔ امنیتی `security-reviewer` را فراخوانی کرد؛ هیچ Skill افزونه‌ای فراخوانی نشد.
- **اجرانشده:** `dotnet build/test` (SDK در این محیط نصب نیست؛ کد تغییر نکرد)، CI روی PR، Hook، Sandbox.
- **Git:** Commit/Push/PR انجام نشد. گزارش: `docs/handoffs/ISSUE-UNNUMBERED-claude-code-standing-rules.md`.

## 2026-10-23 — ارتقای UI/UX صفحه ورود و نشان جدید فرانو (UNNUMBERED، شاخه `ui/login-professional-polish`)
- **تغییر:** نشان SVG محلی `faranou-logo-mark.svg`؛ سربرگ برند، فرم (آیکون رمز، چک‌باکس، spinner، فوکوس)، ستون معرفی و داشبورد نمایشی در `Login.cshtml`، `taraz-login.css` و `taraz-login.js` پالایش شد؛ `LoginSuccess` هم‌سان شد. PageModel، احراز هویت، CSRF، CSP و داده‌های نمایشی تغییر نکرد؛ `faranou-mark.svg` (favicon) و CI دست‌نخورده است.
- **بررسی واقعی:** `npm test` در `tests/Template.Tests.Js` (۷ از ۷)، `check_static_assets.py`، `git diff --check`، ۱۵ بررسی تعاملی Chromium و اسکرول افقی در ۵ عرض روی رندر استاتیک Markup؛ بازبین‌های مستقل `code-reviewer` و `security-reviewer`؛ یک اشکال LTR در `LoginSuccess` یافت و رفع شد.
- **.NET (SDK 10.0.113، بعداً نصب‌شده خارج از مخزن):** restore موفق؛ build Release ۰ خطا؛ `dotnet test --solution Template.sln` ۳۰۶ تست، ۳۰۲ موفق، ۰ ناموفق، ۴ ردشده.
- **اجرانشده:** ۴ تست `CorporateGroupSqlServerTests` (نیاز به `FARANOU_TEST_SQLSERVER`؛ Docker daemon نیست)، E2E با SQL Server، Firefox و Safari؛ باید در CI روی PR دیده شود.
- **Git:** Commit/Push/PR انجام نشد. گزارش: `docs/handoffs/ISSUE-UNNUMBERED-login-professional-ui.md`.

## 2026-10-23 — رفع نبودن `dotnet` در Claude Code Cloud و زیرساخت تست واقعی (UNNUMBERED، شاخه `chore/dotnet-sdk-cloud-test-infra`)
- **علت:** تصویر پایهٔ Cloud SDK .NET ندارد (طبق مستندات رسمی با Setup script نصب می‌شود)؛ شبکه مانع نبود؛ Docker نصب ولی daemon بالا نبود.
- **تغییر:** `scripts/dev/install-dotnet.sh` (SDK از `global.json`، SHA-512 رسمی، ایدمپوتنت)، `scripts/dev/test-sqlserver.sh` (SQL Server 2022 ایزوله در Docker، رمز تصادفی بیرون از Git)، `scripts/dev/cloud-session-start.sh` + Hook `SessionStart` (فقط Cloud)، دو گام lint در Job موجود `publish-scripts`، مستندات و `AGENTS.md`/`CLAUDE.md`؛ AUD-17 (`--solution`) حل شد: علتش باند SDK بود.
- **نتیجهٔ واقعی (SDK 10.0.402):** Release build ۰ خطا (۹۲۱ هشدار)؛ xUnit روی SQL Server واقعی ۳۰۶ از ۳۰۶، ۰ Skip؛ E2E ۱۸ موفق؛ تست JS ۷ از ۷؛ بررسی دارایی‌ها و `git diff --check` موفق.
- **اجرانشده:** Windows/PowerShell، Firefox/Safari، رفتار Runtime Hook، Setup script کش‌شده (مالک حساب در UI).
- **بازبینی:** `code-reviewer` و `security-reviewer` (بدون Critical/High)؛ یافته‌های معتبر اصلاح شد؛ `CODEOWNERS` و سنجاق digest به تصمیم مسئول مخزن واگذار شد.
- **Git:** گزارش: `docs/handoffs/ISSUE-UNNUMBERED-dotnet-cloud-test-infra.md`.

## 2026-10-23 — بازطراحی راه‌اندازی دیتابیس و تنظیمات اتصال (TarazAva → Farano) (UNNUMBERED، شاخه `feature/database-setup-redesign`)
- **علت پیام عمومی «اطلاعات فرم را بررسی کنید» (دو علت، با اجرای واقعی تأیید‌شده توسط Agent پیاده‌ساز):** `[Required]` ضمنی روی `UserName`/`Password` غیرقابل‌null (Windows Authentication همیشه رد می‌شد) و `[Required]` فرم Bootstrap حساب Master که در هر POST فرم دیتابیس `ModelState` را نامعتبر می‌کرد. یافته‌های جانبی: خطای کد راه‌اندازی زیر کلید اشتباه، و باگ تلاش مجدد پس از شکست Migration (Schema نیمه‌کاره).
- **تغییر:** نام محصول در این جریان Farano؛ پوشه تنظیمات `Farano:Settings:Directory` یا `LocalApplicationData/Farano/Settings` (بدون fallback به پوشه برنامه)؛ `ApplicationName=Farano` و Purpose `…v2` با حلقه کلید تازه؛ مهاجرت فقط‌خواندنی و اتمی از `TarazAva` (فایل قدیمی هرگز تغییر/حذف نمی‌شود)؛ حالت‌های Configured/Setup/Blocked؛ فرمان `--settings status|migrate`؛ مراحل، شناسه پیگیری و طبقه‌بندی خطای راه‌اندازی؛ رد دیتابیس بیگانه؛ بازطراحی UI؛ متغیرهای `TARAZAVA_*`→`FARANO_*` با fallback. ADR **D-036** (`NeedsApproval`)؛ شماره بعدی D-037. مستند جدید `docs/DATABASE_SETUP.md`.
- **تغییرات پس از بازبینی‌های مستقل:** فهرست مجاز نام دیتابیس (حداکثر ۱۲۳ نویسه، NFKC، ZWNJ رد) + بررسی `DB_ID` سمت سرور (یافته امنیتی F1: نام هم‌ریخت دیتابیس سیستمی؛ با آزمایش تأیید و اصلاح شد)؛ دروازه یک‌بارمصرف (`AlreadyConfigured`) و وضعیت ثابت شروع؛ مراحل پس از آزمون اتصال با `CancellationToken.None`؛ کدهای خطای تازه `SystemDatabaseNotAllowed`، `AlreadyConfigured`، `DatabaseNewerThanApplication`؛ هشدارهای `WindowsAclNotApplied`، `SetupTokenNotDeleted`؛ مقایسه مسیر واقعی (لینک) و ممنوعیت `WebRoot`؛ `HardenWindowsAcl` (اجرا نشده)؛ رفع اشکال UX (blur کلیک ارسال را می‌بلعید؛ با E2E آشکار شد).
- **نتیجه اجرای Lead (2026-10-23، SDK 10.0.402، SQL Server 2022 واقعی):** build ۰ خطا؛ ۴۸۹ تست، ۴۸۷ موفق، ۰ ناموفق، ۲ Skip (دو تست `chmod` با root)؛ E2E ۱۸ از ۱۸؛ ClientApp ۴۸ از ۴۸؛ `check_static_assets.py` PASS؛ `git diff --check` تمیز؛ آزمون جهش: بدون اصلاح `ModelState` ۴ از ۱۵ تست صفحه شکست می‌خورد. بدون SQL Server (گزارش Agent پیاده‌ساز، دوباره اجرا نشد): ۴۶۹ موفق/۲۰ Skip.
- **بازبینی:** `code-reviewer` یک دور بدون High؛ `security-reviewer` (opus) دو دور، پایان بدون Critical/High/Medium (F1، F2 Medium و N1..N3 Low اصلاح شد)؛ `architect` طرح را داد و روی Diff نهایی دوباره اجرا نشد.
- **اجرانشده:** Windows (DPAPI، Service، IIS، UNC، `HardenWindowsAcl`)، Login SQL منقضی/غیرفعال، CI، دو POST هم‌زمان روی HTTP، POST بوت‌استرپ Master روی HTTP. AUD-2026-10-23-01 (E2E قدیمی) حل شد.
- **باز:** املای Farano/Faranou `NeedsApproval`؛ `AGENTS.md`/`CLAUDE.md` هنوز «ADR بعدی D-036» دارند (نیازمند تأیید صریح مالک برای تغییر).
- **توکن (گزارش ابزار/تخمین):** architect ۷۵٬۱۲۷؛ backend-developer ۲۷۰٬۴۷۶ (۳ دور)؛ documentation-engineer دور اول ۲۳۷٬۳۶۵؛ qa-engineer ۱۳۵٬۱۱۵؛ code-reviewer ۱۸۴٬۴۵۸؛ security-reviewer ۱۲۰٬۸۸۲ + بازبینی دوم ۹۴٬۸۷۷؛ Lead نامشخص. جمع Agentها حدود ۱٫۱ میلیون توکن.
- **Git:** شاخه `feature/database-setup-redesign`، PR #59 (Merge نشده). گزارش: `docs/handoffs/ISSUE-UNNUMBERED-database-setup-redesign.md`.
- **CI (یافتهٔ بعد از باز شدن PR):** Job `publish` در اجرای push شکست خورد (Smoke: «login page renders the anti-forgery token»). علت، شرایط رقابتی موجود در `scripts/smoke-published-app.sh` بود (`curl | grep -q` زیر `pipefail`: `grep -q` با اولین تطابق خارج می‌شود و `curl` با SIGPIPE می‌میرد)، نه تغییر صفحهٔ راه‌اندازی. بازتولید محلی با همان الگو: ۱۱ شکست از ۴۰۰ (حدود ۲٫۷٪)؛ پس از اصلاح (گرفتن بدنه در متغیر): ۰ از ۴۰۰ و Smoke کامل موفق. اصلاح یک‌جایی و کم‌خطر در همین PR آمده؛ اگر مالک نمی‌خواهد، قابل جدا کردن است.

## 2026-10-23 — اصلاح مسیر پس از ورود (انتخاب شرکت و دوره مالی)
- **علت:** `/Account/LoginSuccess` مقصد موقت بدون هدایت بعدی بود و Login بدون `ReturnUrl` به آن می‌رفت؛ کلاینت `/app/` و API زمینه (D-031) از قبل وجود داشتند.
- **تغییر:** ورود ← `/app/` (کاربر شرکتی) یا `/` (حساب Master)؛ `LoginSuccess` فقط Redirect؛ `ReturnUrl` به صفحات ورود نادیده؛ حذف پیام TempData بی‌مصرف.
- **بررسی واقعی:** Build؛ ۴۹۷ تست .NET (۴۹۵ موفق، ۲ Skip، ۰ شکست) روی SQL Server 2022 واقعی؛ ۲۰ تست E2E Chromium (جزئیات و اجرای نهایی در handoff `ISSUE-UNNUMBERED-login-redirect-work-context.md`)؛ بازبینی `code-reviewer` و `security-reviewer` بدون یافتهٔ مهم باز.
- **باز:** سازگاری مسیر مطلق با `PathBase`؛ `AGENTS.md`/`CLAUDE.md` هنوز «ADR بعدی D-036» دارند.
- **توکن:** code-reviewer ≈۴۵٬۰۰۰؛ security-reviewer ≈۴۴٬۰۰۰؛ پیاده‌سازی توسط Lead بدون Subagent (تغییر کوچک و متمرکز؛ تقسیم صرفه نداشت).


## 2026-10-23 — جلوگیری از 404 در `/app/` هنگام اجرای محلی Visual Studio (Issue #61)
- **علت محتمل بر اساس کد:** `Server/wwwroot/app` خروجی تولیدی React است و در `.gitignore` قرار دارد؛ اسکریپت‌های انتشار آن را می‌سازند، اما اجرای مستقیم Server با Visual Studio F5 قبلاً الزام یا ساخت خودکار آن را نداشت.
- **اصلاح پیشنهادی در PR:** افزودن MSBuild target افزایشی فقط برای Buildهای عادی Visual Studio؛ در صورت قدیمی/غایب بودن خروجی، وابستگی‌های npm در صورت نیاز نصب و React ساخته می‌شود. خروجی تولیدی وارد Git نمی‌شود و رفتار CLI/CI تغییر داده نمی‌شود.
- **مستندات:** راهنمای اجرای محلی React در `docs/DEVELOPMENT.md`.
- **اعتبارسنجی:** منتظر CI روی PR است؛ تست محلی Visual Studio/Windows در این محیط قابل اجرا نیست. قبل از ادغام، بررسی کنید target روی Windows و Visual Studio واقعاً اجرا و Buildهای عادی CI را تحت تأثیر قرار ندهد.
- **Git:** Issue #61؛ شاخه `fix/local-react-build-on-server-build`. بدون تغییر مستقیم `main`.

## 2026-10-23 — بازطراحی صفحه انتخاب شرکت و دوره مالی (UNNUMBERED)
- **تغییر:** جریان اول دوره مالی، بعد شرکت؛ لوگوی رسمی بریده‌شده؛ فونت Vazirmatn محلی (OFL)؛ حالت‌های خالی/خطا/بارگذاری؛ بدون تغییر API/Schema.
- **بررسی واقعی:** Build؛ ۴۹۷ تست .NET (۴۹۵ موفق، ۲ Skip) روی SQL Server واقعی؛ ۷۶ تست کلاینت؛ ۲۴ تست E2E؛ آفلاین/static assets موفق. lint و ابزار دسترس‌پذیری اجرا نشد.
- **بازبینی:** `security-reviewer` بدون یافتهٔ مهم؛ `code-reviewer` بدون یافتهٔ مسدودکننده (اصلاح‌ها اعمال و تست‌ها دوباره اجرا شد).
- **باز:** بازبینی انسانی؛ SVG رسمی لوگو؛ «ADR بعدی D-036» در `AGENTS.md`/`CLAUDE.md`.
- **توکن:** frontend-developer ≈۱۱۹٬۰۰۰؛ security-reviewer ≈۶۱٬۰۰۰؛ code-reviewer ≈۶۷٬۰۰۰.
- **جزئیات:** `docs/handoffs/ISSUE-UNNUMBERED-work-context-selector-redesign.md`.

## 2026-10-23 — پیاده‌سازی طرح نهایی صفحه انتخاب شرکت و دوره مالی (UNNUMBERED)
- **تغییر:** هدر سرمه‌ای با خروج بهبودیافته، پس‌زمینهٔ SVG، پنل دوره راست و متمایز، شرکت‌ها ردیفی، جست‌وجوی دوره/شرکت، نوار پایین با دکمهٔ بزرگ، ارتفاع روان؛ بدون تغییر API/Schema.
- **بررسی واقعی:** Build؛ ۴۹۷ تست .NET (۴۹۵ موفق، ۲ Skip) روی SQL Server واقعی؛ ۸۵ تست کلاینت؛ ۲۵ تست E2E. lint/axe اجرا نشد.
- **بازبینی:** security-reviewer بدون یافته؛ code-reviewer بدون مسدودکننده (اصلاح‌ها اعمال شد).
- **باز:** بازبینی انسانی، «کد ملی/شناسه ملی»، برچسب گروه شرکتی تا پیاده‌سازی D-033..35.
- **توکن:** frontend-developer ≈۱۶۱٬۰۰۰؛ code-reviewer ≈۶۲٬۰۰۰؛ security-reviewer ≈۳۷٬۰۰۰.
- **جزئیات:** `docs/handoffs/ISSUE-UNNUMBERED-work-context-selector-final-design.md`.


## 2026-10-23 — پیش‌انتخاب سال مالی و تصمیم مرز مشتری (PR #65، Draft)
- **تصمیم کارفرما:** شرکت پیش‌فرض باید مفهوم جداگانه‌ای برای هر کاربر باشد؛ جدیدترین دوره مالی باز به‌عنوان سال پیش‌فرض انتخاب شود. ممنوعیت رابطه گروهی/مالکیت بین مشتریان مختلف در فضای ابری مشترک طبق D-037 تأیید شد.
- **پیاده‌سازی این برش:** در `ClientApp/src/lib/fiscalYears.ts` جدیدترین سال دارای دوره باز انتخاب می‌شود؛ اگر هیچ دوره بازی وجود نداشته باشد جدیدترین سال قابل ورود فقط‌خواندنی انتخاب می‌شود. در جریان تغییر محیط کاری سال جاریِ معتبر حفظ می‌شود. تست‌های واحد کلاینت برای این رفتار به‌روزرسانی شدند.
- **محدوده عمداً مسدود:** ترجیح ذخیره‌شده شرکت برای هر کاربر و برچسب نوع رابطه شرکت هنوز پیاده نشده‌اند. D-020 می‌گوید CustomerAccount متعلق به سامانه فروشنده است و در کد عملیاتی هنوز نگاشت امن Company→مرز مشتری وجود ندارد. تا طراحی و پیاده‌سازی شناسه مرز عملیاتی و تست‌های منفی متقاطع، انتشار API/UI برای روابط گروهی ممنوع است؛ D-037 این گارد را ثبت می‌کند.
- **اعتبارسنجی:** CI برای commit شاخه گزارش شکست داده است؛ جزئیات Job/گام از API در دسترس نبود و علت شکست هنوز تشخیص داده نشده. تست محلی اجرا نشده است؛ PR در حالت Draft باقی می‌ماند.
- **Git (وضعیت زمان ثبت اولیه):** شاخه `feature/work-context-defaults-company-boundary`؛ PR #65. این گزارش پس از ثبت اولیه قدیمی شد؛ PR #65 در ادامه توسط GitHub ادغام شد و مبنای شاخه جدید `main` است.


## 2026-10-23 — اصلاح به‌خاطر‌سپاری نام کاربری در ورود (PR جدید)
- **رفتار پیاده‌شده:** در صورت فعال بودن «ذخیره نام کاربری و ورود پایدار»، نام کاربری پس از ورود موفق در کوکی اختصاصی HttpOnly با SameSite=Lax و انقضای یک‌ساله ذخیره می‌شود و در بازدید بعدی صفحه ورود پیش‌پر می‌شود. با برداشتن تیک، کوکی در ابتدای ارسال فرم حذف می‌شود. رمز عبور هرگز توسط برنامه ذخیره نمی‌شود.
- **Password Manager:** فیلد رمز از قبل autocomplete="current-password" و نام کاربری autocomplete="username" دارد؛ متن راهنما کاربر را به تأیید prompt مرورگر برای ذخیره امن رمز هدایت می‌کند. ذخیره رمز در localStorage یا کوکی سفارشی عمداً انجام نشده است.
- **تست افزوده‌شده:** E2E برای وجود کوکی نام کاربری، HttpOnly بودن آن، بازگرداندن نام کاربری پس از حذف کوکی نشست و خالی‌ماندن فیلد رمز افزوده شد. این تست هنوز در این محیط اجرا نشده است.
- **محدودهٔ باقیماندهٔ Issue #66:** ترجیح پایدار شرکت پیش‌فرض برای هر کاربر و برچسب نوع شرکت در ردیف‌ها تکمیل نشده‌اند. پیش از برچسب رابطه، نگاشت عملیاتی قابل‌اعتماد Company به مرز مشتری ابری باید طراحی/پیاده‌سازی شود؛ نمایش روابط فعلی بدون این کنترل می‌تواند مرز مشتریان را افشا کند. Issue #66 دوباره باز شده است تا وضعیت ناقص صریح بماند.
- **اعتبارسنجی:** تغییرات از GitHub Contents API روی شاخه مستقل اعمال شده‌اند؛ Build، تست‌های .NET، تست کلاینت و E2E از این محیط اجرا نشده‌اند. CI باید نتیجه واقعی را مشخص کند.
- **Git:** شاخه fix/login-remember-username-work-context؛ تغییرات در main مستقیم اعمال نشده‌اند و PR جدید برای بازبینی ارائه می‌شود.

## 2026-10-23 — بازبینی و اصلاح PR #67 (نام کاربری در ورود)
- PR #67 پیش‌تر Merge شده بود؛ اصلاحات در شاخه `fix/login-remember-username-review`. جزئیات و نتایج واقعی: `docs/handoffs/ISSUE-67-login-remember-username-review.md`.
- قاعده Secure هم‌سو با کوکی احراز هویت، اعتبارسنجی مقدار هنگام خواندن، حذف شرطی کوکی، برچسب و یادداشت صادقانه، ۱۰ تست Integration و ۳ تست E2E جدید.
- بازبینی مستقل امنیت/کیفیت: بدون یافته مسدودکننده. اجرا نشد: Firefox/Safari، Axe، HTTPS پشت Proxy. شکست‌های Vitest/E2E از PR #65 و توقف CI به‌علت صورتحساب: خارج از دامنه و گزارش‌شده.


## 2026-10-23 — ممیزی، سخت‌سازی و مستندسازی مرز مشتری، شرکت پیش‌فرض و برچسب‌های رابطه (Issue #66، شاخه `feature/customer-boundary-audit-and-docs`)
- **زمینه:** PRهای #65، #67، #68، #69 ادغام شده‌اند. PR #68 (`4bcee61`) مرز مشتری، شرکت پیش‌فرض و برچسب‌ها را بدون ADR/handoff/`WORK_LOG` و بدون Build/Test (طبق شرح خودش) ادغام کرد؛ این کار ممیزی، سخت‌سازی و ثبت آن است.
- **تغییر کد:** برچسب `groupMember` و شرط عدم عضویت گروه برای `independent`؛ تفکیک دو سمت `JointControl`؛ Assign در تراکنش Serializable با نگاشت Deadlock/Snapshot به 409؛ Endpoint تشخیصی فقط‌خواندنی اپراتور؛ ممیزی کران‌دار Provision (تعداد + SHA-256 + نام اپراتور) و ثبت ردها؛ تکرار بررسی مرز داخل تراکنش `AddMember`/`RecordOwnership` و محدودکردن بررسی چرخه/درصد/کنترل یگانه به روابط داخل مرز؛ اصلاح تست‌های شکسته از قبل (تست Migration گروه شرکتی، ۶ تست Vitest و ۱ E2E با قاعده منسوخ).
- **تصمیم‌ها:** D-038، D-039، D-040 ثبت و افزوده D-037/توضیح D-033 (متن‌های قبلی بازنویسی نشد). موارد `NeedsApproval`: واژگان برچسب‌ها، نمایش برای عضو بدون `corporate-groups.view`، رویه اصلاح مرز، تریگر دیتابیس.
- **بازبینی:** معماری و امنیتی مستقل: طرح تأیید شد؛ یافته F1 (سرریز ستون ممیزی) اصلاح شد؛ F5 (اشتباه تایپی GUID) باز و پذیرفته. جزئیات در handoff.
- **اعتبارسنجی:** CI اجرا نشده (سقف صورتحساب GitHub). نتیجه اجرای محلی: Build بدون خطا؛ ۵۲۴ تست .NET روی SQL Server واقعی (۵۲۲ موفق، ۰ شکست، ۲ ردشده)؛ Vitest ۹۴/۹۴؛ E2E ۲۹/۲۹. پیش از این کار `main` یک تست .NET، ۶ تست Vitest و یک E2E شکست‌خورده داشت که اصلاح شد (جزئیات در handoff). اجرا نشده: Firefox، Safari، axe، HTTPS پشت Proxy.
- **باز:** `AGENTS.md` و `CLAUDE.md` هنوز «ADR بعدی D-036» می‌گویند (اکنون D-041)؛ تغییر آن‌ها فقط با تأیید صریح کاربر.
- **جزئیات:** `docs/handoffs/ISSUE-66-customer-boundary-default-company-audit.md`.

## 2026-10-24 — فاز صفر لایسنس و سامانهٔ مرکزی مشتریان (شاخه `docs/licensing-central-system-phase0`، فقط مستندات)
- **زمینه:** مأموریت مالک محصول برای معماری سامانهٔ مرکزی مشتریان/لایسنس کنار نرم‌افزار حسابداری. گزارش ممیزی قبلی ورودی اولیه بود و با کد `main`@`8c1e7a3` دوباره اعتبارسنجی شد.
- **انجام‌شده:** ممیزی مجدد (۲۷ ردیف وضعیت + AUD-2026-10-24-01..09)، ADR D-041 (`Accepted` مشروط) و D-042..D-044 (`Proposed`)، سند `LICENSING_OPEN_DECISIONS.md` (۱۷ تصمیم باز)، گسترش مدل داده/گردش‌کار/تهدید، نقشهٔ راه L0..L4، اصلاح شمارهٔ ADR بعدی (D-045) و انحراف مستندات D-031/D-036.
- **اعتبارسنجی واقعی:** Release Build ۰ خطا؛ ۴۰۷ تست .NET غیر-Integration موفق (۱۶ ردشده)؛ ۹۴ تست Vitest و typecheck؛ `check_static_assets.py` PASS؛ آزمایش امضا ECDSA/RSA-PSS روی Linux. **اجرا نشد:** SQL Server/Integration/E2E، CI، `npm run build`، Windows.
- **باز:** پاسخ مالک به OD-01/02/12 (پیش‌شرط L1)؛ گاردهای G1–G3؛ بررسی Branch protection. جزئیات: `docs/handoffs/ISSUE-UNNUMBERED-licensing-phase0-architecture.md`.

## 2026-10-25 — رفع شکست CI (G1/G2) و بازگردانی پوشش تست لایسنس (شاخهٔ محلی `fix/ci-g1-xunit-using-and-g2-printf`، بدون commit)
- **انجام‌شده:** `using Xunit;` در تست G1؛ اصلاح `printf --` در تست G2؛ تست‌های `EvaluatorStateTests` برای پوشش حذف‌شده در PR #74.
- **اعتبارسنجی:** Build و تست‌ها محلی موفق؛ CI/API اجرا نشد (دسترسی). Guard مهاجرت از پیش روی main شکست می‌خورد و رفع نشد.
- **باز:** مولد مهاجرت‌ها از دو مهاجرت اخیر عقب است؛ تصمیم‌های OD-01/02/12. جزئیات: `docs/handoffs/ISSUE-UNNUMBERED-ci-g1-g2-fix-and-licensing-test-coverage.md`.

## 2026-10-25 — رفع شکست Guard مهاجرت پس از PR #77 (شاخهٔ `fix/migration-guard-hasone-generic`)
- **علت:** پارسر `ef_model.py` فرم `HasOne<T>().WithMany()` را نمی‌شناخت و ابزارها دو Migration اخیر را پوشش نمی‌دادند؛ `dotnet ef has-pending-model-changes` روی main سبز بود (بدون اختلاف واقعی مدل).
- **انجام‌شده:** اصلاح پارسر + تست رگرسیون + گام CI؛ پیشبرد مولد/تأییدکننده به `AddCompanyCustomerBoundary`؛ پشتیبانی `AddColumn`؛ کامیت جدا برای ترتیب Snapshot/Designer.
- **جزئیات:** `docs/handoffs/ISSUE-UNNUMBERED-migration-guard-hasone-generic.md`.

## 2026-10-25 — اصلاح PR #78 پس از بازبینی مستقل (شاخهٔ `fix/migration-guard-hasone-generic`)
- **یافته‌ها:** نسخهٔ اول فقط ثابت‌ها را جلو برده بود: SQL مرورشدهٔ `AddCorporateGroups` از کنترل خارج می‌شد، Migrationهای میانی و `Down` سنجیده نمی‌شدند، `defaultValue`/`AddForeignKey` نادیده گرفته می‌شد.
- **اصلاح:** زنجیرهٔ کامل Up/Down از روی پوشه، SQL مرورشده برای همهٔ Migrationها، `AddColumn`/`AddForeignKey` fail-closed، ۳۱ تست جهشی + تست‌های واحد. هیچ Migration/Snapshot/مدل برای سبز شدن CI تغییر نکرد.
- **باز:** AUD-2026-10-25-02 (محدودیت‌های باقی‌مانده). جزئیات: `docs/handoffs/ISSUE-UNNUMBERED-migration-guard-hasone-generic.md`.


## 2026-10-25 — ثبت تصمیم‌های مالک برای هستهٔ قرارداد لایسنس (Issue #80)
- **تصمیم صریح مالک:** OD-01 پذیرفته شد (حق تجاری در سطح `CustomerAccount` و لایسنس فنی برای هر `Installation`); OD-02 پذیرفته شد (`maxCompanies` + فهرست زیرسیستم‌ها، `maxUsers` موکول، سقف نصب در سامانهٔ فروشنده); OD-12 پذیرفته شد (ECDSA P-256/SHA-256، IEEE P1363 با طول ثابت 64 بایت، امضا روی بایت‌های دقیق Payload).
- **انجام‌شده در این تغییر:** ثبت D-045 و همگام‌سازی وضعیت اسناد تصمیم‌های باز، طرح لایسنس و نقشهٔ راه. این تغییر صرفاً مستنداتی است؛ هیچ کد لایسنس، Schema/Migration یا Enforcement اضافه نشده است.
- **اعتبارسنجی:** بازبینی متنی و کنترل پیوندها/وضعیت‌ها؛ Build و تست اجرا نشده‌اند چون تغییر اجرایی ندارد. CI این شاخه پس از ایجاد PR باید بررسی شود.
- **باز:** پیاده‌سازی `Licensing.Contracts`، تعریف دقیق قالب نسخه‌دار و بردارهای آزمون؛ اجرای آزمون‌ها روی Linux و Windows. هیچ ادعایی مبنی بر سازگاری چندسکویی تأییدشده وجود ندارد.
- **جزئیات:** [Issue #80](https://github.com/hamidreza1394/Accounting/issues/80).


## 2026-10-25 — سخت‌سازی قرارداد لایسنس پس از D-045 (Issue #80، PR کد در حال آماده‌سازی)
- **یافتهٔ بازبینی:** `Licensing.Contracts` از قبل وجود داشت؛ کد و تست تکراری ساخته نشد. کامنت‌های OD-02/OD-12 که تصمیم‌ها را هنوز Proposed می‌خواندند، با D-045 همگام شدند.
- **اصلاحات:** allowlist زیرسیستم‌های v1 (`finance`, `sales`, `inventory`, `treasury`)، رد کد ناشناخته و تکراری، آزمون بردار ثابت RFC 6979 با امضای 64 بایتی P1363، آزمون تغییر whitespace برای اثبات امضا روی بایت‌های دقیق Payload، اجرای تست‌های قرارداد در Job ویندوز.
- **اعتبارسنجی:** تغییرات از طریق GitHub API ثبت شده‌اند؛ Build/Test هنوز در این محیط اجرا نشده‌اند. باید CI لینوکس و ویندوز را بگذراند تا ادعای چندسکویی تأیید شود.
- **باز:** بررسی رفتار آزمون در CI؛ اطمینان از سازگاری بردار ثابت روی Windows؛ سپس بازبینی diff مستقل. بدون Schema/Migration/Enforcement.


## 2026-10-25 — تکمیل بردارهای ثابت و کنترل منحنی در قرارداد لایسنس (Issue #80)
- **افزوده شد:** آزمون کامل رشتهٔ canonical JSON، بردار ثابت RFC 6979، آزمون تغییر whitespace، allowlist زیرسیستم، رد تکرار و کد ناشناخته، و کنترل OID منحنی P-256.
- **CI:** تست مستقل قرارداد به Job ویندوز اضافه شد؛ اجرای واقعی هر دو سیستم‌عامل پس از اجرای PR باید تأیید شود.
- **وضعیت:** کد در شاخهٔ `feature/licensing-contracts-d045` ثبت شده؛ هنوز Build/Test محلی اجرا نشده و PR نباید پیش از CI سبز آمادهٔ ادغام اعلام شود.


## 2026-10-25 — پذیرش تصمیم‌های امنیت و عملیات سامانهٔ مرکزی فروشنده (Issue #83، D-046)
- **تصمیم صریح مالک:** سه پیشنهاد OD-07/09/11 تأیید شد؛ مدیریت مرکزی ممکن است چندمدیره باشد و طراحی باید از ابتدا برای چند مدیر آماده شود.
- **D-046:** ریشهٔ اعتماد آفلاین؛ کلیدهای صدور مستقل و قابل چرخش؛ KMS با دسترسی فقط-امضا به‌عنوان گزینهٔ پیش‌فرض Production و HSM به‌عنوان جایگزین معادل؛ provider abstraction؛ بدون کلید نرم‌افزاری Production یا امضا در CI. ارتباط آنلاین مشتری اختیاری و بدون heartbeat اجباری. MFA اجباری (WebAuthn ترجیحی، TOTP جایگزین)، نقش حداقل‌دسترسی و maker-checker برای عملیات پرخطر؛ درخواست‌کننده نمی‌تواند خودش را تأیید کند. پیش از Production حداقل دو مدیر واجد شرایط و مسیر بازیابی کنترل‌شده لازم است.
- **محدوده:** مستندات و تصمیم معماری؛ هیچ کد اجرایی، Schema/Migration، endpoint، کلید واقعی یا Enforcement ایجاد نشده است.
- **اعتبارسنجی:** بازبینی متنی و بررسی هم‌خوانی وضعیت ODها در اسناد؛ Build/Test اجرا نشده‌اند چون تغییر فقط مستنداتی است. CI پس از ایجاد PR باید بررسی شود.
- **ریسک/باقی‌مانده:** انتخاب ارائه‌دهندهٔ KMS، پیاده‌سازی provider، طراحی مدل هویت/نقش/تأیید، ذخیره‌سازی ممیزی و runbook بازیابی در Issueهای اجرایی L2 باقی است. بدون تأیید بیشتر، این جزئیات نباید به کلید/محیط واقعی متصل شوند.
- **جزئیات:** [Issue #83](https://github.com/hamidreza1394/Accounting/issues/83); D-046.


## 2026-10-25 — نسخه‌بندی Skillهای مهندسی فرانو (Issue #85)
- **هدف:** همگام‌کردن راهنمای توسعه با معماری تأییدشده و ایجاد راهنماهای تخصصی قابل‌بازبینی در مخزن.
- **تغییرات:** Skill اصلی برای `.NET 10 / C# 14` و معماری Razor Pages + React/TypeScript/Vite/Tailwind + API هم‌مبدأ همگام شد؛ Skillهای معماری، امنیت و Multi-Tenancy، تست/کیفیت، UI/UX فارسی RTL و دامنه حسابداری اضافه شدند. `CLAUDE.md`، `docs/INDEX.md` و handoff Issue #85 به‌روزرسانی شدند.
- **محدوده:** مستندات و Skillها בלבד؛ هیچ کد اجرایی، Schema/Migration، package manifest، lockfile یا Workflow تغییر نکرد.
- **اعتبارسنجی:** فایل‌های مبنا و Skill نصب‌شده از GitHub/محیط Skill خوانده شدند. Build، تست .NET و تست کلاینت اجرا نشدند؛ این تغییر صرفاً مستنداتی است. بررسی نهایی لینک‌ها و diff در PR باقی است.
- **وضعیت:** روی شاخه `docs/issue-85-faranou-engineering-skills`؛ ادغام نشده و نیازمند بازبینی انسانی است.
- **گزارش تحویل:** `docs/handoffs/ISSUE-85-faranou-engineering-skills.md`.


## 2026-10-25 — انتخاب و سازگارسازی Skillهای ECC (Issue #87)
- **منبع:** مخزن رسمی `affaan-m/ECC`، snapshot درخت `90606c8315ee09cafc303515c5f33024db6b9882`؛ مجوز MIT در `third_party/ECC-LICENSE.txt` حفظ شد.
- **افزوده شد:** Skillهای انتخابی .NET، C# testing، API، React، React testing، Playwright E2E و تحلیل اثر تغییر؛ دو Skill مختص فرانو برای Migrationهای EF Core/SQL Server و ثبت تصمیم در `docs/DECISIONS.md` نوشته شد.
- **کنترل سازگاری:** راهنمای generic دربارهٔ Bearer token، Next.js/RSC، PostgreSQL/Prisma، افزودن پکیج‌های تست، ساخت `docs/adr/` و نصب Hookها بدون بررسی/تأیید مجاز نیست؛ محدودیت‌ها در Skillها و `docs/THIRD_PARTY_SKILLS.md` درج شدند.
- **محدوده:** فقط مستندات و Skillها؛ هیچ کد محصول، Schema/Migration اجرایی، package manifest/lockfile، Workflow، Hook اجرایی، MCP server یا پکیج runtime نصب نشد.
- **اعتبارسنجی:** مخزن ECC، ساختار واقعی تست‌ها و manifestهای فرانو بررسی شدند. Build و تست‌ها اجرا نشدند؛ CI روی HEAD نهایی باید جداگانه بررسی شود.
- **وضعیت:** شاخه `docs/issue-87-curated-ecc-skills`؛ PR برای بازبینی ایجاد می‌شود و ادغام نشده است.
- **گزارش تحویل:** `docs/handoffs/ISSUE-87-curated-ecc-skills.md`.


## 2026-10-25 — شروع L2 سامانه مرکزی فروشنده (Issue #89)
- **پیش‌شرط تأییدشده:** PR #84 ادغام شده است؛ D-046 تصمیم‌های OD-07/09/11 را پذیرفته است. Issue #83 پس از ثبت تصمیم‌ها بسته شد.
- **ممیزی اولیه:** `.NET 10 / EF Core 10` و `Licensing.Contracts` در main بررسی شد. `Template.sln` و `Server` متعلق به برنامه مشتری‌اند؛ Vendor باید Solution/Host/DB/Identity مستقل داشته باشد. وضعیت فعلی پروژه لایسنس همچنان `NotImplemented` است.
- **اقدام:** Issue #89 ساخته شد تا کار به مراحل قابل بازبینی تقسیم شود. این شاخه فقط D-047 پیشنهادی و همگام‌سازی اسناد را آماده می‌کند؛ کد اجرایی، Schema/Migration و کلید واقعی اضافه نمی‌شود.
- **قواعد معماری پیشنهادی:** هویت ASP.NET Core Identity جدا؛ بدون ثبت‌نام عمومی/مدیر پیش‌فرض؛ MFA اجباری؛ bootstrap یک‌بارمصرف و ممیزی‌شده؛ حداقل دو مدیر واجد MFA برای آمادگی Production؛ maker-checker سمت سرور؛ ممیزی hash-chain همراه checkpoint مستقل؛ KMS/HSM با مجوز فقط-امضا و fail-closed در Production.
- **اعتبارسنجی:** Build/Test اجرا نشده‌اند؛ تغییرات این مرحله مستنداتی هستند و باید در PR/CI بازبینی شوند. CI سبز برای HEAD نهایی هنوز تأیید نشده است.
- **ریسک/باقی‌مانده:** D-047 هنوز `Proposed` است. محصول KMS/HSM، runbook بازیابی/break-glass و مقصد مستقل checkpoint نیازمند تعیین محیط/تصمیم جدا هستند. هیچ پیاده‌سازی Production مجاز فرض نشده است.
- **گزارش تحویل:** `docs/handoffs/ISSUE-89-vendor-control-plane.md`; [Issue #89](https://github.com/hamidreza1394/Accounting/issues/89).


## 2026-10-25 — L2.1 اسکلت مستقل Vendor (Issue #91)
- **تصمیم:** کارفرما D-047 را برای محیط‌های ابری و سرور اختصاصی تأیید کرد؛ وضعیت تصمیم در `docs/DECISIONS.md` به `Accepted` تغییر یافت. پیاده‌سازی همچنان `NotImplemented` در سطح قابلیت‌های کامل لایسنس است.
- **تغییرات روی شاخه `feature/vendor-host-skeleton`:** ایجاد `Vendor.sln` مستقل، پروژهٔ `Vendor/Vendor.Host` با target `net10.0`، endpoint حداقلی `/health/live`، تست‌های smoke و `Vendor/README.md` برای استقرار cloud/dedicated-server. افزودن Job مجزا برای Build/Test Vendor در CI.
- **جداسازی:** هیچ ProjectReference به برنامه مشتری، هیچ تغییری در `Template.sln`، هیچ Migration یا تغییری در دیتابیس مشتری و هیچ secret/کلید خصوصی اضافه نشده است.
- **اعتبارسنجی:** این Agent نتوانست Build/Test محلی اجرا کند؛ CI شاخه/PR هنوز باید اجرا و بررسی شود. موفقیت آزمون‌ها تا دریافت نتیجه واقعی گزارش نمی‌شود.
- **ریسک/محدودیت:** Host هنوز احراز هویت، RBAC، دیتابیس، logging عملیاتی یا صدور لایسنس ندارد و Production-ready نیست. این موارد به PRهای جداگانه واگذار شده‌اند.
- **تحویل:** `docs/handoffs/ISSUE-91-vendor-host-skeleton.md`.


## 2026-10-25 — اصلاح شکست اولیهٔ CI برای Vendor.Host.Tests
- **شاهد:** اجرای GitHub Actions شماره `40212637047`، Job `vendor-build-test` در Build شکست خورد؛ خطای xUnit v3 تصریح کرد پروژهٔ تست باید `<OutputType>Exe</OutputType>` داشته باشد. Restore و Build خود Host موفق بودند؛ تست‌ها به‌دلیل شکست Build اجرا نشدند.
- **اصلاح:** `tests/Vendor.Host.Tests/Vendor.Host.Tests.csproj` به‌روزرسانی شد تا خروجی تست executable باشد. این اصلاح از خطای واقعی CI ناشی شد، نه حدس.
- **اعتبارسنجی پس از اصلاح:** CI باید روی HEAD جدید دوباره اجرا شود؛ تا آن زمان نتیجهٔ نهایی `Verified` نیست.


## 2026-10-25 — گارد وابستگی Vendor (Issue #95)
- **مبنای معماری:** D-041 شرط می‌گذارد که گراف وابستگی Server به پروژه‌های Vendor نرسد و `Licensing.Contracts` بدون ProjectReference/لینک منبع بیرونی بماند. بررسی مجدد نشان داد G2 و G3 موجودند، اما G1 هنوز به‌صورت تست رگرسیون وجود نداشت؛ G4/G5 و allow-list کامل G2 نیز حل‌شده فرض نمی‌شوند.
- **پیاده‌سازی در شاخه:** `tests/Template.Tests/Architecture/VendorIsolationTests.cs` با پیمایش XML گراف ProjectReference مستقیم/ترنزیتیو، بررسی قرارداد و fixture منفی افزوده شد. هیچ package جدیدی اضافه نشده است.
- **محدوده داده:** هیچ کد اجرایی مشتری، Schema/Migration یا دیتابیس مشتری تغییر نکرده؛ فقط تست معماری در پروژهٔ تست اضافه شده است.
- **اعتبارسنجی:** Build/Test محلی و `git diff --check` در این محیط اجرا نشده‌اند؛ منتظر CI واقعی روی PR هستیم. تا آن زمان G1 `NotVerified` است.
- **ریسک/باقی‌مانده:** تحلیل XML fail-closed است و ProjectReference دارای property/wildcard را غیرقابل‌اثبات تلقی می‌کند. آزمون CI باید ثابت کند گراف فعلی بدون نقض است. G4/G5 و allow-list کامل Publish خارج از محدوده‌اند.
- **گزارش تحویل:** `docs/handoffs/ISSUE-95-vendor-isolation-g1.md`.


## 2026-10-25 — جداسازی workflow و مالکیت کد Vendor (Issue #98)
- **مبنای تصمیم:** D-041 G4/G5 را برای جداسازی فروشنده ضروری می‌داند. PRهای #92/#94/#96 ادغام شده‌اند؛ گارد G1 در CI run `40214517027` تأیید شد و G2/G3 موجودند، ولی allow-list کامل G2 و تنظیمات Branch protection هنوز تأیید نشده‌اند.
- **تغییرات روی شاخه:** `.github/CODEOWNERS` برای `Vendor/**`, `Vendor.sln`, `tests/Vendor.Host.Tests/**`, `Licensing.Contracts/**` و workflowهای CI؛ `.github/workflows/vendor-ci.yml` با path filters، permissions فقط read، Restore/Build/Test مستقل، publish به مسیر موقت، گارد کلید خصوصی و artifact فقط روی push به `main`. Job تکراری `vendor-build-test` از `.github/workflows/dotnet.yml` حذف شد تا workflow محصول مشتری تکرار غیرضروری نداشته باشد.
- **مرز ایمنی:** در workflow کلید/secret استقرار یا امضای لایسنس وجود ندارد؛ artifact یک بستهٔ build موقت است، نه deploy. هیچ Schema/Migration یا دیتابیس مشتری تغییر نکرد.
- **اعتبارسنجی:** Build/Test محلی و lint YAML در این محیط اجرا نشده‌اند؛ CI روی PR لازم است. به‌عنوان مرجع، run `40214517027` برای PR #96 همهٔ Jobهای گزارش‌شده را موفق داشت. CODEOWNERS تا تنظیم required-code-owner-review در branch protection به‌تنهایی enforce نمی‌شود.
- **باقی‌مانده:** گارد allow-list کامل اسمبلی‌های Publish، تنظیم Branch protection/required review (نیازمند دسترسی تنظیمات مخزن)، و انتخاب محیط استقرار واقعی Vendor باقی است.
- **گزارش تحویل:** `docs/handoffs/ISSUE-98-vendor-ci-codeowners.md`.
## 2026-10-25 — پیگیری L2.1: همگام‌سازی وضعیت Vendor و شواهد CI (Issue #93)
- **یافته:** پس از ادغام PR #92، D-047 در `DECISIONS.md` پذیرفته بود، اما `INDEX.md` و `ROADMAP.md` هنوز `Proposed` می‌گفتند؛ `ARCHITECTURE.md` نیز به‌روز نبود و G2 موجود را نادیده می‌گرفت.
- **اصلاح مستندات:** وضعیت D-047/PR #92/CI run در `INDEX.md`، `ROADMAP.md`، `ARCHITECTURE.md` و `PROJECT_STATUS.md` با دامنهٔ محدود و شواهد واقعی همسو شد. ادعای وجود تمام گاردهای G1–G6 نشده است؛ فقط G2 با اسکریپت و تست مشخص ثبت شد.
- **شاهد CI:** run `40212722972` موفق؛ `vendor-build-test`، `secret-scan`، `client`، `build-and-test`، `publish-scripts`، `e2e`، `publish-windows` و `publish` موفق بودند. run `40212637047` شکست اولیهٔ xUnit را ثبت کرده بود و علت در commit بعدی اصلاح شد.
- **محدوده:** مستندات فقط؛ کد اجرایی، Workflow، Schema/Migration و دیتابیس مشتری تغییر نکرد.
- **اعتبارسنجی محلی:** `git diff --check` و Build/Test محلی در این محیط اجرا نشده‌اند؛ PR/CI باید بررسی شود. لینک‌ها و متن‌ها به‌صورت متنی بررسی شدند.
- **باقی‌مانده:** قبل از گسترش قابلیت‌های مدیریتی، پوشش گاردهای جداسازی باید به‌صورت جداگانه و مستند بازبینی شود. Vendor هنوز Production-ready نیست.
- **گزارش تحویل:** `docs/handoffs/ISSUE-93-vendor-status-reconciliation.md`.

## 2026-10-25 — ممیزی UI صفحهٔ انتخاب شرکت/دوره و سه اصلاح کوچک
- **ممیزی:** مرورگر Playwright با API شبیه‌سازی‌شده در ۳۶۰/۷۶۸/۱۲۸۰؛ بدون سرریز افقی و بدون هدف لمسی کوچک.
- **اصلاح:** عنوان تب بر اساس صفحه، عدم‌شکست بازهٔ تاریخ، preload فونت محلی؛ دو تست جدید.
- **اعتبارسنجی محلی:** typecheck، ۹۶ تست، build، check:offline و check_static_assets موفق؛ .NET اجرا نشد (کد تغییر نکرده).
- **گزارش تحویل:** `docs/handoffs/ISSUE-UNNUMBERED-ui-selection-audit.md`.


## 2026-10-25 — تکمیل L2.2: ثبت Migrationهای Vendor در مخزن (Issue #100، پیگیری پس از PR #101)
- **علت پیگیری:** PR #101 ادغام شد، ولی Migration و Snapshot اولیه صرفاً به‌صورت موقت توسط CI ساخته می‌شدند؛ در نتیجه source قابل اعمال/بازبینی در مخزن نبود. سبز شدن تست همان run به‌تنهایی این خلأ را برطرف نمی‌کرد.
- **اصلاح در شاخهٔ follow-up:** سه فایل تولیدشده توسط `dotnet-ef 10.0.13` ثبت شدند: `Vendor/Vendor.Persistence/Migrations/20261025180755_VendorInitialIdentityAndAudit.cs`, فایل `.Designer.cs` و `VendorDbContextModelSnapshot.cs`. گام scaffolding/آپلود موقت از `vendor-ci.yml` حذف شد و به جایش بررسی دائمی `dotnet ef migrations has-pending-model-changes` اضافه شد.
- **شاهد قبلی:** در run `40219629655`، step تولید EF migration موفق و `Vendor.Host.Tests` روی SQL Server واقعی موفق شد، اما source تولیدشده در آن لحظه artifact/workspace بود، نه commit؛ بنابراین آن run را شاهد نهاییِ migration ثبت‌شده به حساب نمی‌آوریم.
- **محدوده:** دیتابیس مشتری، `Template.sln`، `Server/Server.csproj` و Migrationهای مشتری تغییر نکردند. دیتابیس Integration با پیشوند `FaranouVendorTests_` یکتا ساخته و پس از تست حذف می‌شود.
- **وضعیت اعتبارسنجی این پیگیری:** منتظر CI روی commit حاوی Migrationهای ثبت‌شده هستیم؛ Build/Test محلی اجرا نشده‌اند چون SDK در محیط محلی این Agent موجود نیست. تا نتیجهٔ CI، وضعیت schema `NotVerified` می‌ماند.
- **امنیت/باقی‌مانده:** کلاس `VendorAuditEvent` فقط مدل زنجیرهٔ هش است؛ سرویس append-only، اعتبارسنجی زنجیره و checkpoint مستقل هنوز لازم است. ورود/MFA/RBAC و bootstrap هنوز فعال/قابل دسترس نیستند.
- **گزارش تحویل:** `docs/handoffs/ISSUE-100-vendor-persistence-identity-audit.md`.

## 2026-10-26 — Vendor persistence hardening (Issue #117)
- **مبنای کار:** `main` پس از ادغام PR #102 (`9b63c546c7a569498f6987137c686c2d7f9fb6ff`). PR #116 بسته شد چون بر پایهٔ نسخهٔ قدیمی‌تر بود و مدل Persistence/Migration ناسازگار داشت؛ هیچ تغییری از آن ادغام نشد.
- **تغییرات:** registration دیتابیس و ASP.NET Core Identity EF stores به `Vendor/Vendor.Persistence/DependencyInjection.cs` منتقل شد؛ Host فقط `AddVendorPersistence` را صدا می‌زند. سیاست‌های پایهٔ Identity شامل ایمیل یکتا، رمز ۱۴ کاراکتری با ۴ کاراکتر یکتا، و lockout پس از ۵ تلاش برای ۱۵ دقیقه است. این سیاست‌ها جایگزین MFA اجباری نیستند.
- **تست‌ها:** تست metadata مدل و default غیرفعال بودن مدیر؛ نبود FK از audit به admin؛ fail-closed هنگام نبود اتصال Vendor؛ resolve شدن `IUserStore`/`IRoleStore`; اجرای Migration و assert هشت جدول روی SQL Server واقعی.
- **محافظت destructive test:** تست فقط `localhost`, `127.0.0.1` یا `::1` و catalog پایهٔ `FaranouVendorTests` را می‌پذیرد؛ سپس DB نام‌تصادفی می‌سازد، connection را می‌بندد و فقط همان DB را حذف می‌کند. در CI متغیر test الزامی است و تست نمی‌تواند بی‌صدا skip شود.
- **Workflow:** نام متغیرهای integration test با workflow یکسان شد. CI از SQL Server 2022 استفاده می‌کند؛ اسکن/Publish و مرحلهٔ signing تغییر نکرده‌اند.
- **مرز داده:** هیچ فایل `Server/**`، `Infrastructure/Persistence/**` یا Migration مشتری تغییر نکرده است؛ هیچ endpoint ورود/ثبت‌نام/bootstrap، مدیر seed، secret یا کلید امضا اضافه نشد.
- **اعتبارسنجی:** Build/Test محلی اجرا نشده‌اند. Vendor CI روی PR #118، run `40272872248`، سبز شد: Restore، Build، `dotnet ef migrations has-pending-model-changes`، تست‌های Vendor (از جمله Migration واقعی روی SQL Server)، Publish و گارد خروجی موفق بودند. `.NET CI` run `40272872260` در زمان ثبت هنوز در حال اجرا بود؛ نتیجهٔ کلی PR تا پایان آن نهایی نیست. CI PR #102 پیش از این تغییرات فقط model-snapshot check داشت.
- **باقی‌مانده:** MFA/passkey/TOTP، bootstrap یک‌بارمصرف، RBAC سمت سرور، audit writer/verifier/checkpoint، maker-checker، کنترل‌های deploy و G2 allow-list انتشار همچنان مرحله‌های مستقل هستند.
- **Handoff:** `docs/handoffs/ISSUE-117-vendor-persistence-hardening.md`.


## 2026-10-26 — مبنای shadcn/ui و RTL فارسی
- **درخواست:** پذیرش shadcn/ui در رابط React و مستندسازی در مخزن.
- **بررسی:** `ClientApp` از React 19، TypeScript strict، Vite و Tailwind CSS 4 استفاده می‌کند؛ alias `@/*` و `components.json` از قبل وجود نداشت. Runtime باید بدون CDN بماند.
- **تصمیم:** D-048؛ shadcn/ui به‌صورت source-owned components با RTL و فقط در `ClientApp` استفاده شود. صفحات Razor فعلی خارج از این تغییر هستند.
- **تغییرات این مرحله:** ایجاد `ClientApp/components.json` و `docs/UI_COMPONENTS.md`؛ افزودن ارجاع در README و مستندات توسعه/استانداردها/فهرست/وضعیت پروژه.
- **محدودیت مهم:** این مرحله هنوز CLI، بسته‌های npm، lockfile یا کامپوننت‌های تولیدشده را تغییر نداده است؛ وضعیت صادقانه `Partially Implemented` است. مرحلهٔ بعد باید نصب و lockfile را با npm معتبر به‌روزرسانی و تست‌های CI را اجرا کند.
- **اعتبارسنجی:** مستندات رسمی فعلی shadcn/ui برای Vite، RTL و Tailwind v4 بررسی شدند. Build/Test در این مرحله اجرا نشده است.
- **دیتابیس:** هیچ تغییر دیتابیس یا Migration انجام نشد.

## 2026-10-26 — VENDOR-UI-01: اسکلت مستقل پنل فروشنده (Issue #126)
- **هدف:** شروع مرحله UI بدون وابستگی به UI مشتری و بدون جعل API یا داده مدیریتی.
- **Branch:** feat/issue-126-vendor-ui-shell.
- **تغییرات:** پروژه مستقل Vendor/Vendor.Web با React 19، TypeScript strict، Vite، Tailwind v4، lockfile مستقل و source-owned shadcn Button/Card؛ صفحه ورود فارسی RTL با جریان واقعی password→TOTP و antiforgery؛ shell واکنش‌گرا و داشبورد اولیه با placeholderهای صریح؛ build به Vendor/Vendor.Host/wwwroot؛ Host static-file و SPA fallback امن که مسیرهای ناشناخته API/health را به HTML تبدیل نمی‌کند؛ Vendor CI شامل npm ci/typecheck/test/build و audit وابستگی‌های runtime و بررسی وجود UI bundle در publish.
- **مرزها:** هیچ تغییری در ClientApp، دیتابیس مشتری یا Migrationهای مشتری انجام نشده است. آمار مشتری/نصب/لایسنس جعلی نمایش داده نمی‌شود. هیچ توکن یا رمز در localStorage/sessionStorage ذخیره نمی‌شود.
- **اعتبارسنجی:** Restore/Build/Test و CI از این Agent اجرا نشده‌اند؛ باید نتایج PR را بررسی کرد. وضعیت Implemented in branch، نه Verified و نه Production-ready.
- **باقی‌مانده:** مرور نهایی TypeScript/React و CI؛ آزمون مرورگری روی HTTPS واقعی؛ تکمیل Issue #119 bootstrap، #123 RBAC و audit checkpoint مستقل پیش از عملیات تجاری.

### پیگیری CI برای VENDOR-UI-01 — 2026-10-26
- Vendor CI run 40277399614: npm ci، TypeScript typecheck، تست‌های UI، build فرانت‌اند، npm audit و Restore دات‌نت موفق بودند؛ Build دات‌نت شکست خورد چون Program.cs فاقد usingهای لازم برای Data Protection، EF Core، Identity و Authorization بود.
- اصلاح: افزودن usingهای صریح در Vendor/Vendor.Host/Program.cs، commit 3d317ea206087ae026520a84ada49f61941bf139.
- وضعیت بازبینی: اجرای Vendor CI روی commit اصلاحی 3d317ea در صف GitHub Actions است؛ هنوز نتیجه Build/Test نهایی نداریم. PR #127 همچنان Draft است و Merge نشده است.


## 2026-10-26 — بهبود سرعت بارگذاری Vendor UI و اصلاح پیکربندی CI
- **دامنه:** ادامهٔ UI پنل فروشنده در شاخه `feat/issue-126-vendor-ui-shell`، بدون تغییر ClientApp یا دیتابیس مشتری.
- **بهبود بارگذاری:** فایل‌های خروجی Vite در مسیر `/assets` دارای نام‌های hashدار هستند؛ Host برای این فایل‌ها `Cache-Control: public,max-age=31536000,immutable` می‌فرستد تا بارگذاری مجدد از cache مرورگر انجام شود. `index.html` همچنان `no-store` می‌ماند تا انتشار نسخهٔ جدید به bundle قدیمی قفل نشود. این تغییر از الگوی استاندارد asset fingerprinting/cache استفاده می‌کند؛ اندازه و زمان واقعی لود هنوز با Lighthouse/مرورگر اندازه‌گیری نشده است.
- **یافتهٔ CI:** Vendor CI run 40277588682 نشان داد npm ci، typecheck، تست‌های UI، audit و build فرانت‌اند و نیز .NET build موفق شدند؛ اما ۷ تست Host به دلیل نبود متغیر `FARANO_VENDOR_ConnectionStrings__VendorDb` در مرحلهٔ تست شکست خوردند.
- **اصلاح CI:** متغیر اتصال VendorDb در مرحلهٔ `dotnet test` به همان SQL Server آزمایشی اضافه شد. این تغییر هنوز باید در اجرای جدید CI تأیید شود؛ هیچ تستی را حذف یا دور نزده‌ایم.
- **اعتبارسنجی:** بهبود cache و اصلاح workflow در CI جدید باید تأیید شوند. UI هنوز در مرورگر واقعی/HTTPS و با Lighthouse بررسی نشده است؛ ادعای اندازه‌گیری سرعت یا آمادگی Production مطرح نیست.
- **محدودیت‌ها:** صفحات مشتری/نصب/لایسنس هنوز placeholder هستند؛ bootstrap مدیر، RBAC، اتصال APIهای تجاری، و audit checkpoint مستقل کامل نشده‌اند.
- **مرجع:** PR #127 — https://github.com/hamidreza1394/Accounting/pull/127.

## 2026-10-26 — کنترل بودجهٔ bundle فرانت‌اند Vendor
- **اندازه‌گیری مبنا:** خروجی build ثبت‌شده در Vendor CI run 40277588682 برابر بود با JavaScript خام 278.82 kB / gzip 90.03 kB و CSS خام 30.46 kB / gzip 7.29 kB؛ HTML خام 0.53 kB / gzip 0.37 kB. این‌ها اندازهٔ artifact هستند، نه اندازه‌گیری زمان واقعی بارگذاری.
- **گارد جلوگیری از regression:** به workflow، بررسی سقف gzip برای JavaScript برابر 100 KiB و CSS برابر 20 KiB افزوده شد. اگر رشد وابستگی‌ها یا CSS از این بودجه عبور کند، CI شکست می‌خورد تا قبل از انتشار بررسی شود. اجرای این گارد در CI بعدی باید تأیید شود.
- **کش مرورگر:** assetهای hashدار یک سال immutable و index.html بدون cache باقی می‌ماند.
- **مبنای فنی:** راهنمای رسمی Vite توضیح می‌دهد که build تولیدی، preload و CSS splitting را پشتیبانی می‌کند؛ از code splitting دستی تا وقتی bundle بزرگ یا مسیرهای واقعی ماژولار نشده‌اند پرهیز می‌کنیم تا درخواست‌های اضافی و پیچیدگی غیرضروری ایجاد نشود: https://vite.dev/guide/features و https://vite.dev/guide/performance.html.


## 2026-10-26 — Vendor UI Issue #128: Passkey/TOTP UX و بازیابی CI
- **مبنای کار:** PR #127 در main ادغام شده بود؛ Issue #128 برای پیگیری UX احراز هویت و خطاهای CI ایجاد شد. شاخه: `feat/issue-128-vendor-passkey-mfa-ui`.
- **Passkey:** UI به endpointهای واقعی `/api/auth/passkey/options` و `/api/auth/passkey/sign-in` برای ورود، و `/api/auth/passkey/creation-options` و `/api/auth/passkey/register` برای ثبت کلید عبور متصل شده است. از WebAuthn native API استفاده می‌شود؛ کلید عمومی/خصوصی توسط مرورگر/Authenticator مدیریت می‌شود و verification نهایی سمت سرور است.
- **TOTP:** UI از `/api/auth/totp/setup` و `/api/auth/totp/confirm` برای راه‌اندازی و تأیید استفاده می‌کند. shared key و URI فقط در state حافظه‌ای صفحه نمایش داده می‌شوند و با لغو، تأیید یا خروج پاک می‌شوند؛ Web Storage استفاده نشده است.
- **UX:** پیام‌های خطای API برای 400/401/403/409/429 فارسی و عمومی شده‌اند؛ مرورگر فاقد WebAuthn یا اتصال غیرامن پیام روشن می‌گیرد. CSS فرم‌های امنیتی و تست‌های UI اضافه شده‌اند.
- **CI:** Vendor CI run 40279335977 در main شکست داشت. تست قدیمی `UnknownPathReturnsNotFound` با طراحی SPA fallback تناقض داشت؛ اکنون تست، deep link واقعی UI را بررسی می‌کند و تست موجود unknown API همچنان 404 را بررسی می‌کند. connection string میزبان تست به `FaranouVendorAuthTests` تنظیم شد تا با انتظار integration test مطابقت داشته باشد.
- **اعتبارسنجی:** در زمان ثبت، اجرای Vendor CI برای این شاخه پس از بازشدن PR هنوز انجام نشده/تأیید نشده است. تست‌های جدید UI، TypeScript، build، audit، تست‌های SQL Server و publish باید در GitHub Actions بررسی شوند. Build/Test محلی در این محیط اجرا نشده است.
- **مرزها:** هیچ تغییری در ClientApp، Customer DB یا Migrationهای مشتری نیست. CRUD مشتری/نصب/قرارداد/لایسنس، bootstrap کامل، RBAC، maker-checker، audit checkpoint مستقل و KMS/HSM همچنان مانع آمادگی Production هستند.
- **مراجع:** Issue #128 https://github.com/hamidreza1394/Accounting/issues/128؛ PR #127 merged https://github.com/hamidreza1394/Accounting/pull/127.


### تکمیل‌یادداشت Issue #128 — یافتهٔ CI دوم
- اجرای Vendor CI روی commit `c5aadd0`، پس از سبزشدن تمام گام‌های فرانت‌اند، در `dotnet test` شکست خورد: `PendingModelChangesWarning` هنگام اجرای Migration در تست احراز هویت؛ در همان run، دستور EF CLI برای بررسی pending model changes موفق بود.
- بررسی `Vendor/Vendor.Host/Program.cs` دو ثبت مستقل `AddDbContext<VendorDbContext>` را نشان داد: یک ثبت در `AddVendorPersistence` با Migration Assembly صریح و یک ثبت تکراری در Host بدون آن تنظیم. ثبت تکراری حذف شد تا persistence module تنها منبع پیکربندی DbContext و Migration Assembly باشد. این اصلاح نیازمند اجرای دوبارهٔ integration test است؛ علت به‌عنوان قطعیِ حل‌شده اعلام نمی‌شود تا CI تأیید کند.
- آخرین اصلاح: commit `a2af05b78fe5f8d5237d3b36aa92c786a171b783`. CI جدید هنوز باید بررسی شود.


## Issue #130 — استاندارد RTL فارسی و کمبوباکس قابل مدیریت
- فایل `Vendor/Vendor.Web/index.html` از قبل `lang="fa"` و `dir="rtl"` داشت؛ این مبنا حفظ شد.
- استایل Vendor اکنون جهت/تراز فارسی را صریح می‌کند و برای کنترل‌های جهت‌دار از خواص منطقی CSS استفاده می‌کند. نام کاربری با `dir="auto"` برای متن فارسی/لاتین و رمز عبور و OTP با جهت LTR تعریف می‌شوند تا تایپ فنی به‌هم نریزد.
- کامپوننت `ManagedSelect` برای فرم‌های Vendor اضافه شد: انتخاب اولین گزینه معتبر به‌صورت پیش‌فرض، امکان مقدار کنترل‌شده، label و پیام خطای دسترس‌پذیر، و نمایش «افزودن گزینه» فقط با `canManageOptions` و callback صریح.
- این کامپوننت خودش گزینه‌ها را ذخیره نمی‌کند؛ مجوز باید از API سمت سرور بیاید و callback باید به endpoint واقعی متصل شود. هیچ API یا CRUD تجاری جعلی در این مرحله اضافه نشده است.
- مرز معماری: شرکت‌های حسابداری (از جمله مستقل/مادر/زیرمجموعه) متعلق به دیتابیس نصب مشتری‌اند، نه دیتابیس تجاری Vendor. Vendor مشتری تجاری، نصب و مجوز را مدیریت می‌کند. ایجاد شرکت حسابداری از پنل مرکزی بدون طرح provisioning مستقل و تأییدشده انجام نمی‌شود.
- تست‌های جدید برای جهت RTL، پیش‌فرض اولین گزینه، تغییر انتخاب، پنهان بودن مدیریت گزینه بدون مجوز و پیام خطا اضافه شده‌اند.
- اعتبارسنجی محلی در این محیط اجرا نشد؛ وضعیت تا نتیجه Vendor CI روی PR **Implemented / Not yet Verified** است.


## 2026-10-26 — Vendor Identity registration regression
- بازبینی پس از ادغام‌ها نشان داد `AddVendorPersistence`، `AddIdentityCore<VendorAdminUser>` را ثبت می‌کند و `Vendor.Host/Program.cs` دوباره Identity را با همان DbContext ثبت می‌کرد؛ بنابراین گزارش قدیمی مبنی بر تک‌منبع بودن Identity با کد فعلی `main` هم‌خوان نبود.
- شاخهٔ `fix/vendor-identity-registration-single-source` ثبت Identity را به یک محل در Persistence منتقل می‌کند و تنظیمات SchemaVersion، SignInManager و DefaultTokenProviders را همان‌جا نگه می‌دارد. Host فقط پیکربندی Host/auth cookie را انجام می‌دهد.
- هدف، حذف ثبت تکراری سرویس‌ها و جلوگیری از اختلاف تنظیمات Identity در زمان اجرای Migration/احراز هویت است. این ارتباط علت قطعی شکست قبلی اعلام نمی‌شود تا CI و تست SQL Server آن را تأیید کنند.
- تست/build محلی اجرا نشده؛ منتظر CI روی PR مستقل هستیم.

### CI evidence — Vendor UI fix PR #131
- Vendor CI run [40285919870](https://github.com/hamidreza1394/Accounting/actions/runs/40285919870) completed with failure in `VendorAuthenticationSecurityTests.PasswordOnlyIsDeniedAndTotpChallengeDoesNotIssueApplicationCookie`: runtime `Database.MigrateAsync` throws `PendingModelChangesWarning`. Frontend typecheck/tests/build, asset budget, .NET build and EF CLI snapshot check passed in the same run.
- This reproduces the runtime-only model mismatch and motivates validating the single-source Identity registration in PR #132. The duplicated registration is a concrete inconsistency, but is not declared the proven sole root cause until the new SQL Server integration run passes.

### یافتهٔ قطعی‌تر دربارهٔ PendingModelChangesWarning
- بازبینی مستقیم `main` نشان داد فایل `20261026045412_VendorAddPasskeySupport.Designer.cs` اصلاً وجود نداشت؛ migration فقط `Up/Down` داشت و `BuildTargetModel` تولیدشده را نداشت. این نقص با شکست runtime `Database.MigrateAsync` و موفقیت بررسی snapshot از CLI سازگار است و علت مستقیم محتمل‌تری از ثبت تکراری Identity است.
- همین شاخه اکنون designer را از snapshot جاری ساخته، `BuildModel` را به `BuildTargetModel` تبدیل کرده، metadata را در designer متمرکز کرده و تست SQL Server را برای وجود جدول `VendorAdminUserPasskeys` و اعمال هر دو migration تقویت می‌کند. ثبت Identity نیز جداگانه تک‌منبع شده است.
- تا CI جدید سبز نشود، این علت/اصلاح تأییدشده محسوب نمی‌شود.


## 2026-10-26 — Fix Vendor CI compile failure
- CI run 40286276604 failed at compile time because `IdentityBuilder.AddSignInManager()` is not available to the plain `Microsoft.NET.Sdk` Persistence project with its current references.
- Follow-up branch `fix/vendor-identity-signin-manager-registration` removes that host-layer extension call from Persistence and registers SignInManager in the ASP.NET Core Host after `AddVendorPersistence`; IdentityCore, roles, EF stores and token providers remain registered once in Persistence.
- Must be validated by a fresh Vendor CI run; do not merge if compile or runtime migration tests fail.


## 2026-10-26 — Issue #134: انتقال ثبت Identity token providers به Host
- **علت CI:** Vendor CI run [40287063856](https://github.com/hamidreza1394/Accounting/actions/runs/40287063856) در Build شکست خورد؛ `IdentityBuilder.AddDefaultTokenProviders()` در پروژهٔ کلاس‌لایبرری Persistence با `Microsoft.NET.Sdk` و وابستگی‌های فعلی در دسترس نیست.
- **اصلاح در شاخهٔ مستقل:** زنجیرهٔ Persistence فقط Identity options، roles و EF stores را ثبت می‌کند؛ `AddDefaultTokenProviders()` به Host منتقل شده و روی همان `VendorAdminUser`/سرویس‌ها اعمال می‌شود. ثبت مجدد `AddIdentityCore` انجام نشده است.
- **حفظ امنیت:** token providers حذف نشده‌اند؛ این موضوع برای جریان‌های token/TOTP مهم است. هیچ دیتابیس یا Migration مشتری تغییر نکرده است.
- **اعتبارسنجی:** هنوز Build/Test پس از اصلاح اجرا نشده؛ نتیجهٔ معتبر فقط پس از Vendor CI جدید ثبت می‌شود.
- **Issue/Branch:** [#134](https://github.com/hamidreza1394/Accounting/issues/134)؛ `fix/vendor-identity-token-providers`.


### Issue #134 — نتیجهٔ CI اول و اصلاح دوم
- اجرای Vendor CI [40288514593](https://github.com/hamidreza1394/Accounting/actions/runs/40288514593) ثابت کرد که `AddDefaultTokenProviders()` به Host منتقل شده اما خط قدیمی `builder.Services.AddSignInManager()` هنوز اشتباه است: extension متد روی `IdentityBuilder` قرار دارد، نه `IServiceCollection`.
- اصلاح دوم در همان شاخه، `AddSignInManager()` و `AddDefaultTokenProviders()` را روی یک `IdentityBuilder` در Host ثبت می‌کند. پس از این اصلاح باید CI دوباره اجرا شود؛ هیچ تستی هنوز پس از اصلاح دوم موفق گزارش نشده است.


## 2026-10-26 — Vendor EF Core Identity SchemaVersion 3 migration drift

- **Issue:** follow-up to Vendor authentication integration test after PR #137; tracked in draft PR [#138](https://github.com/hamidreza1394/Accounting/pull/138).
- **Diagnosis:** the SQL Server test compared the runtime design-time model with the latest migration snapshot and exposed five column metadata differences (VendorAdminUserTokens.Name/LoginProvider, VendorAdminUsers.PhoneNumber, and VendorAdminUserLogins.ProviderKey/LoginProvider), plus a missing VendorAdminUserPasskeys.UserId index and foreign key. These differences correspond to the configured IdentitySchemaVersions.Version3.
- **Changes on branch fix/vendor-strict-migration-gate:** restored the strict EF snapshot gate after PR #137's temporary bypass; added 20261026084500_VendorAlignIdentitySchemaV3, its separate designer target model, and the updated snapshot; added explicit drop/recreate of the token/login primary keys because SQL Server rejected altering VendorAdminUserTokens.Name while PK_VendorAdminUserTokens depended on it; corrected the diagnostic test to compare initialized snapshot metadata with the EF design-time model.
- **Observed CI results:** run [40293940211](https://github.com/hamidreza1394/Accounting/actions/runs/40293940211) reported the model-drift operations. Run [40295157534](https://github.com/hamidreza1394/Accounting/actions/runs/40295157534) was pending/in progress before this documentation update; an earlier run failed because the primary key was not yet dropped/recreated. The latest migration revision has not been verified yet.
- **Safety:** no warning suppression, no deleted tests, no changes to customer accounting migrations. PR #138 remains Draft and must not be merged until the SQL Server migration test, strict EF snapshot check, full Vendor tests and publish pass.
- **Next steps:** inspect latest Vendor CI; correct any further migration/model mismatch; then implement the secure first-admin bootstrap and forced password-change flow as a separate small change, followed by RBAC and audit wiring.


## 2026-10-26 — Issue #139: اصلاح آزمون antiforgery در جریان Password → TOTP
- **شواهد اولیه:** Vendor CI run [40345676162](https://github.com/hamidreza1394/Accounting/actions/runs/40345676162) در آزمون `PasswordOnlyIsDeniedAndTotpChallengeDoesNotIssueApplicationCookie` شکست خورد؛ Expected `OK` و Actual `BadRequest`. لاگ علت را «antiforgery token was meant for a different claims-based user» گزارش کرد.
- **اصلاح در شاخه `fix/issue-139-totp-antiforgery-test`:** آزمون بعد از دریافت کوکی challenge مربوط به TOTP توکن antiforgery تازه می‌گیرد؛ بعد از ورود موفق نیز برای هویت Application توکن تازه می‌گیرد. اعتبارسنجی antiforgery و assertion عدم صدور کوکی Application قبل از TOTP حفظ شده‌اند.
- **فایل تغییرکرده:** `tests/Vendor.Host.Tests/VendorAuthenticationSecurityTests.cs`; handoff: `docs/handoffs/ISSUE-139-totp-antiforgery-test.md`.
- **تست‌ها:** هنوز پس از اصلاح اجرا نشده‌اند؛ نتیجه باید از Vendor CI روی PR گرفته شود. در اجرای قبلی، فرانت‌اند و .NET Build موفق بودند اما تست Integration شکست خورد و EF snapshot/Publish اجرا نشدند.
- **امنیت/داده:** هیچ تغییری در منطق runtime، سیاست امنیتی، Schema/Migration یا دیتابیس مشتری اعمال نشده است. این اصلاح فقط آزمون را با چرخه واقعی هویت هماهنگ می‌کند.
- **وضعیت:** Implemented in branch / Not yet Verified. Merge نشود تا آزمون SQL Server، کل Vendor suite، EF snapshot gate و Publish موفق شوند.


## 2026-10-26 — Issue #141: حفظ کوکی antiforgery هنگام تغییر هویت MFA
- **شاهد CI:** Vendor CI run [40346746819](https://github.com/hamidreza1394/Accounting/actions/runs/40346746819) شکست آزمون PasswordOnlyIsDeniedAndTotpChallengeDoesNotIssueApplicationCookie را در helper دریافت توکن ثبت کرد: InvalidOperationException: The given header was not found. این خطا از انتظار قطعی برای وجود هدر Set-Cookie جدید ناشی می‌شد.
- **تحلیل:** ASP.NET Core می‌تواند وقتی کوکی antiforgery قبلاً در درخواست وجود دارد، توکن درخواست تازه متناسب با هویت جاری صادر کند بدون اینکه کوکی را دوباره در Set-Cookie برگرداند. پس از تغییر هویت به TOTP یا Application باید همان کوکی antiforgery نیز صریحاً همراه کوکی هویت ارسال شود؛ صرفاً ساخت توکن جدید کافی نیست.
- **اصلاح در شاخه fix/issue-141-antiforgery-cookie-reuse:** آزمون کوکی antiforgery قبلی را همراه کوکی TOTP/Application به endpoint توکن می‌فرستد؛ helper در صورت نبود Set-Cookie مجاز است همان کوکی ورودی را بازیابی کند، اما اگر هیچ‌کدام موجود نباشد با پیام روشن شکست می‌خورد. منطق Production و کنترل antiforgery دست‌نخورده است.
- **اعتبارسنجی:** اصلاح روی GitHub ثبت شده؛ هنوز Vendor CI پس از این اصلاح اجرا/تأیید نشده است. تا سبز شدن آزمون SQL Server، کل Vendor suite، EF snapshot gate و Publish، Verified یا آماده ادغام محسوب نمی‌شود.
- **ایمنی:** هیچ تغییر در دیتابیس مشتری، Migrationهای مشتری یا منطق احراز هویت Production انجام نشده است.
- **مراجع:** Issue [#141](https://github.com/hamidreza1394/Accounting/issues/141)، PR follow-up پس از آماده شدن branch ایجاد می‌شود.


## 2026-10-26 — Issue #143: اصلاح خطای کامپایل آزمون antiforgery
- **شاهد CI:** Vendor CI run [40349534341](https://github.com/hamidreza1394/Accounting/actions/runs/40349534341) پس از ادغام PR #142 در مرحله Build شکست خورد: متغیر antiforgeryCookie در خطوط جریان TOTP و Application استفاده شده بود اما در دریافت اولیه توکن با discard (`var (token, _)`) دور انداخته شده بود. خطاهای CS0103 و خطاهای زنجیره‌ای CS8130 از همین مورد ناشی می‌شدند.
- **اصلاح:** در شاخه fix/vendor-antiforgery-cookie-variable، کوکی اولیه از tuple نگه داشته می‌شود تا همراه کوکی هویت در درخواست‌های بعدی استفاده شود. منطق Production و کنترل امنیتی تغییری نکرده است.
- **وضعیت:** commit ثبت شده؛ Vendor CI تازه هنوز اجرا/تأیید نشده. تا عبور Build، آزمون SQL Server، کل Vendor suite، EF snapshot gate و Publish، این اصلاح Verified نیست.
- **Issue:** [#143](https://github.com/hamidreza1394/Accounting/issues/143).

## 2026-10-26 — Issue #145: اصلاح فرض نادرست آزمون کوکی پس از TOTP
- **شاهد CI:** Vendor CI run [40352627665](https://github.com/hamidreza1394/Accounting/actions/runs/40352627665) پس از ادغام PR #144 شکست خورد. مراحل UI و Build دات‌نت موفق بودند؛ آزمون PasswordOnlyIsDeniedAndTotpChallengeDoesNotIssueApplicationCookie در استخراج کوکی خطای Sequence contains more than one matching element داد.
- **علت:** Endpoint ابتدا TwoFactorAuthenticatorSignInAsync را اجرا می‌کند و سپس برای افزودن claim amr=otp کوکی Application را با SignInWithClaimsAsync مجدداً صادر می‌کند. در پاسخ ممکن است بیش از یک هدر Set-Cookie با نام کوکی Application دیده شود؛ Single در تست فرض نادرستی درباره تعداد هدرها داشت.
- **اصلاح پیشنهادی در شاخه:** آزمون مقدار نهایی کوکی Application را از هدرهای پاسخ می‌گیرد. هیچ assertion درباره رد ورود password-only، تأیید TOTP، نشست معتبر یا حفاظت antiforgery حذف نشده است.
- **اعتبارسنجی:** هنوز CI پس از این اصلاح اجرا نشده؛ تا موفقیت کل Vendor suite روی SQL Server، strict EF snapshot gate و Publish، وضعیت Verified نیست.
- **محدوده:** فقط آزمون و مستندات؛ بدون تغییر رفتار Production، دیتابیس مشتری یا Migrationهای مشتری.
- **Issue:** [#145](https://github.com/hamidreza1394/Accounting/issues/145).

## 2026-10-26 — Issue #113: پیاده‌سازی اولیه bootstrap امن مدیر Vendor
- **دامنه:** شاخهٔ `feat/issue-113-vendor-bootstrap` بر مبنای شاخهٔ اصلاح CI #146 ساخته شد تا وابستگی CI حل‌شده را حفظ کند.
- **پیاده‌سازی:** فرمان تعاملی `bootstrap-admin` فقط در command mode اجرا می‌شود؛ راز ۳۲ بایتی از `FARANO_VENDOR_Bootstrap__Secret` و ورودی تعاملی دریافت و با hash ثابت‌طول + `FixedTimeEquals` مقایسه می‌شود. راز/گذرواژه در آرگومان CLI قرار نمی‌گیرند.
- **ایمنی حساب:** حساب pending ابتدا غیرفعال ذخیره می‌شود؛ enrollment TOTP تحت SQL Server transaction-scoped application lock انجام می‌شود؛ فقط پس از تأیید کد، MFA و حساب فعال می‌شوند. قطع enrollment حساب را غیرفعال باقی می‌گذارد و قابل بازیابی است.
- **ممیزی:** ثبت scoped `IVendorAuditWriter`/`TimeProvider` افزوده شد؛ رویدادهای رد/شروع/تکمیل در زنجیرهٔ ممیزی نوشته می‌شوند. در صورت خطا یا لغو نیز ثبت audit به‌صورت best-effort انجام می‌شود.
- **صحت هویت:** ایمیل مدیر اولیه `EmailConfirmed=false` می‌ماند؛ bootstrap بدون تأیید واقعی، ادعای تأیید ایمیل را جعل نمی‌کند.
- **تست:** آزمون Integration جدید روی SQL Server واقعی دو تلاش هم‌زمان را اجرا می‌کند و باید فقط یک مدیر فعال و زنجیرهٔ ممیزی معتبر داشته باشد؛ همچنین مقایسهٔ راز تست واحد دارد.
- **اعتبارسنجی:** تغییرات روی branch ثبت شده‌اند؛ Build/Test محلی اجرا نشده است. CI روی PR وابسته باید Build، SQL Server tests، strict EF snapshot gate و publish را تأیید کند. تا نتیجهٔ سبز وضعیت Verified نیست.
- **دامنه داده:** هیچ Migration یا دیتابیس مشتری تغییر نکرده است. Issue #113 باز می‌ماند تا تمام معیارها و CI تأیید شوند.
- **خطای CI و اصلاح DI:** Vendor CI run [40363406842](https://github.com/hamidreza1394/Accounting/actions/runs/40363406842) در آزمون Integration خطا داد: `DataProtectorTokenProvider<VendorAdminUser>` نمی‌توانست `IDataProtectionProvider` را resolve کند. علت این بود که command mode و ServiceCollection تست، Data Protection را ثبت نکرده بودند. ثبت سرویس و رعایت مسیر کلید persistent در Production اضافه شد؛ اجرای تازه لازم است.
- **محافظت از خروجی حساس:** راز bootstrap با ورودی مخفی دریافت می‌شود؛ URI TOTP فقط از مسیر خروجی کنسول تعاملی نوشته می‌شود و در صورت redirect بودن stdout، enrollment fail-closed می‌شود.
- **محافظت از خروجی حساس:** راز bootstrap با ورودی مخفی دریافت می‌شود؛ URI TOTP فقط از مسیر خروجی کنسول تعاملی نوشته می‌شود و در صورت redirect بودن stdout، enrollment fail-closed می‌شود.

## 2026-10-26 — Issue #114: اعمال baseline احراز هویت روی DefaultPolicy
- **یافتهٔ امنیتی:** `FallbackPolicy` قبلی فقط endpointهای بدون metadata را پوشش می‌داد؛ endpointهایی با `.RequireAuthorization()` از `DefaultPolicy` استفاده می‌کنند و ممکن بود گارد `VendorAdminAccessRequirement` را دور بزنند.
- **اصلاح:** `VendorAuthorizationPolicies.Configure` یک baseline مشترک شامل `RequireAuthenticatedUser` و `VendorAdminAccessRequirement` روی هر دو `DefaultPolicy` و `FallbackPolicy` اعمال می‌کند. این اصلاح هنوز جای permissionهای ریزدانه و maker-checker را نمی‌گیرد.
- **تست:** آزمون واحد تضمین می‌کند هر دو policy نیازمند هویت احراز‌شده و گارد MFA/فعال‌بودن مدیر باشند.
- **دامنه:** بدون تغییر Schema/Migration، دیتابیس مشتری یا endpointهای تجاری؛ RBAC، resource authorization و maker-checker همچنان در ادامهٔ Issue #114 لازم‌اند.
- **اعتبارسنجی:** PR در شاخهٔ وابسته به bootstrap ساخته می‌شود؛ CI تازه لازم است و وضعیت تا سبز شدن Not Verified.

## 2026-10-26 — پیگیری اعتبارسنجی bootstrap پس از خطای DI
- **شاهد CI:** [Vendor CI run 40363406842](https://github.com/hamidreza1394/Accounting/actions/runs/40363406842) در مرحله تست Vendor شکست خورد؛ علت مستقیم، ثبت‌نشدن `IDataProtectionProvider` در ServiceCollection آزمون `VendorBootstrapIntegrationTests` بود. Build و تست‌های UI پیش از آن موفق بودند.
- **اصلاح مشاهده‌شده در `main`:** تست اکنون `AddDataProtection().SetApplicationName("Faranou.Vendor.ControlPlane")` را ثبت می‌کند. این با خطای DI مشخص‌شده هم‌راستاست؛ اما نتیجهٔ تازه‌ای که رفع خطا را ثابت کند هنوز از این بررسی به‌دست نیامده است.
- **اقدام:** PR مستنداتی برای اجرای دوبارهٔ Vendor CI و CI کامل ایجاد می‌شود. فقط در صورت سبزشدن تست SQL Server، strict EF snapshot gate و Publish می‌توان bootstrap را Verified دانست.
- **محدوده:** این پیگیری فقط مستندات است؛ هیچ کد Production، Migration مشتری یا داده‌ای تغییر نمی‌کند.


## 2026-10-26 — Issue #150: مبنای RBAC فروشنده (در شاخه؛ در انتظار CI)
- **محدوده:** شاخه `feat/issue-150-vendor-rbac-foundation` بر مبنای `main`@ `6fc04fd368185c6e9935cbd121ed9ccaac1455a6`، کاتالوگ نسخه‌دار مجوزها، policyهای نام‌دار، ارزیابی claimهای نقش Identity و تست‌های ترکیب policy را اضافه می‌کند.
- **طراحی امنیتی:** مجوزها شناسه‌های پایدار سمت سرورند و از نام نقش یا برچسب UI جدا هستند. هر policy مجوزی هم‌زمان گارد موجود مدیر فعال/MFA را الزام می‌کند. شناسه مجوز ناشناخته fail-closed رد می‌شود. مجوزها از claim نوع `vendor:permission:v1` روی نقش‌های Identity خوانده می‌شوند؛ هنوز endpoint یا UI برای مدیریت نقش/مجوز ساخته نشده است.
- **محدوده خارج از این PR:** مجوزدهی منبع‌محور مشتری/نصب/قرارداد/لایسنس و maker-checker هنوز پیاده‌سازی نشده‌اند؛ هیچ Migration یا تغییر دیتابیس مشتری وجود ندارد.
- **اعتبارسنجی:** کد از طریق GitHub API روی شاخه ثبت شد؛ Build/Test محلی در این محیط اجرا نشده‌اند. CI روی PR باید Vendor UI، Build/Test با SQL Server، strict EF snapshot gate، Publish/artifact scan و full repository CI را تأیید کند. وضعیت تا آن زمان **Implemented in branch / Not Verified** است.
- **Handoff:** `docs/handoffs/ISSUE-150-vendor-rbac-foundation.md`.


- **CI بازخورد اولیه PR #153:** Vendor CI run [40380994581](https://github.com/hamidreza1394/Accounting/actions/runs/40380994581) کامپایل را متوقف کرد: `VendorPermissionRequirement.cs` namespace مربوط به `VendorAdminAccessRequirement` را وارد نکرده بود. علت مشخص شد و `using Faranou.Vendor.Host.Authentication;` اضافه شد. این اصلاح باید با اجرای جدید CI تأیید شود؛ نتیجهٔ قبلی شکست خورده و هیچ تست .NET پس از Build اجرا نشده بود.


## 2026-10-26 — اصلاح خطای کامپایل آزمون RBAC فروشنده
- **گزارش CI:** فایل `tests/Vendor.Host.Tests/VendorPermissionAuthorizationTests.cs` با خطای `CS0246` متوقف شد؛ نوع `VendorAdminAccessRequirement` در فضای نام `Faranou.Vendor.Host.Authentication` تعریف شده بود ولی فضای نام در فایل آزمون وارد نشده بود.
- **علت ریشه‌ای:** کمبود `using Faranou.Vendor.Host.Authentication;` در پروژه تست؛ این خطا مربوط به مرجع کامپایل است و نشان‌دهنده نقص در منطق مجوزدهی Production نیست.
- **اصلاح:** فضای نام لازم به فایل آزمون افزوده شد. هیچ آزمونی حذف یا تضعیف نشد؛ هیچ منطق احراز هویت/MFA، دیتابیس یا Migration تغییر نکرد.
- **اعتبارسنجی:** اصلاح روی شاخه `feat/issue-150-vendor-rbac-foundation` ثبت شده است. CI پس از این commit باید نتیجه نهایی را تأیید کند؛ تا آن زمان وضعیت **اصلاح‌شده در شاخه، اما تأییدنشده** است.
- **مستندات:** handoff فارسی `docs/handoffs/ISSUE-150-vendor-rbac-foundation.md` و بخش RBAC در `docs/SECURITY.md` به‌روزرسانی شدند.
- **پیوند پیگیری:** [PR #153](https://github.com/hamidreza1394/Accounting/pull/153).


## 2026-10-26 — Issue #154: پوشش metadata مجوزدهی Vendor
- Issue #154 پس از بسته‌شدن زودهنگام بازگشایی شد؛ علت: آزمون پوشش endpointها و مجوزدهی منبع‌محور هنوز کامل نبود.
- به VendorPermissionAuthorizationTests.cs آزمون integration اضافه شد که endpointهای /api/ را از EndpointDataSource استخراج می‌کند، نبود metadata دسترسی را رد می‌کند و فهرست مسیرهای AllowAnonymous را با فهرست مجاز ثابت مقایسه می‌کند.
- آزمون‌های موجود برای DefaultPolicy/FallbackPolicy و الزام حساب فعال/MFA حفظ شدند.
- **اعتبارسنجی:** آزمون اجرا نشده؛ CI باید اجرا و نتیجه ثبت شود. این تغییر هنوز Verified نیست.
- **باقی‌مانده:** آزمون‌های منفی با احراز هویت واقعی، resource-level authorization/IDOR و maker-checker؛ هیچ Migration مشتری تغییر نکرده است.
- Handoff: docs/handoffs/ISSUE-154-vendor-endpoint-authorization-coverage.md


## تکمیل Issue #154 — آزمون سیاست مؤثر endpointهای محافظت‌شده
- افزوده شد: آزمون می‌سازد سیاست مؤثر هر endpoint محافظت‌شده API را با AuthorizationPolicy.CombineAsync و تأیید می‌کند VendorAdminAccessRequirement (گارد مدیر فعال/MFA) در همه آن‌ها وجود دارد؛ در کنار آزمون metadata دسترسی و فهرست مسیرهای عمومی مجاز.
- Commit: 1708e1d7c4b67a51222690bc756b95e945db2eee.
- وضعیت: تست/Build هنوز منتظر CI هستند؛ Verified اعلام نمی‌شود.


## 2026-10-27 — Issue #159: اجبار تغییر گذرواژهٔ اولیه در Vendor
- **یافته:** `VendorAdminUser.MustChangePassword` در مدل وجود داشت اما تا این تغییر، enforcement عمومی در API pipeline و فرم واقعی تغییر گذرواژه تکمیل نبود؛ فیلد به‌تنهایی مرز امنیتی نیست.
- **پیاده‌سازی در شاخه `security/issue-159-force-password-change`:** endpoint `POST /api/auth/change-password` از Cookie هم‌مبدأ، anti-forgery، `UserManager.ChangePasswordAsync`، سیاست Identity موجود و rate limit استفاده می‌کند. خطاهای اعتبارسنجی و رمز فعلی پاسخ عمومی دارند.
- **گارد سمت سرور:** تا وقتی `MustChangePassword=true` است، مسیرهای API مدیریتی مسدود می‌شوند؛ فقط دریافت anti-forgery، `/api/auth/me`، تغییر گذرواژه و خروج مجاز می‌مانند. UI فارسی RTL فرم اولیه و تأیید دوبارهٔ گذرواژه را نشان می‌دهد.
- **ممیزی:** پس از تغییر موفق، رخداد `admin.password.changed` بدون رمز خام/هش نوشته می‌شود. اگر نوشتن ممیزی شکست بخورد endpoint پاسخ موفق صادر نمی‌کند؛ لازم است این رفتار در CI/بازبینی تأیید شود.
- **تست:** آزمون Integration مسیر رد API قبل از تغییر، رد anti-forgery/رمز فعلی نامعتبر، تغییر موفق، پاک‌شدن پرچم، ثبت audit و بازشدن مجدد endpoint را پوشش می‌دهد. آزمون UI جریان اجباری را اضافه می‌کند.
- **اعتبارسنجی:** اجرای محلی dotnet/npm در این محیط انجام نشد. Vendor CI، .NET CI، SQL Server integration، strict EF snapshot، publish و artifact scan روی آخرین SHA هنوز باید تأیید شوند. وضعیت **Implemented / Not Verified** است.
- **دامنه داده:** هیچ Migration یا دیتابیس حسابداری مشتری تغییر نکرد؛ در این PR Migration Vendor نیز لازم نیست.
- **مستندات:** `Vendor/README.md`، `docs/SECURITY.md` و `docs/handoffs/ISSUE-159-force-password-change.md` به‌روز شدند.

- **اصلاح هم‌راستایی bootstrap (بازبینی تکمیلی):** بررسی مستقیم `VendorBootstrapCommand` نشان داد مسیر فعال‌سازی حساب در نسخه قبلی `MustChangePassword=false` می‌نوشت و بنابراین گارد جدید برای مدیر bootstrapشده فعال نمی‌شد. این مقدار به `true` تغییر کرد و آزمون Integration bootstrap اکنون همین invariant را assert می‌کند؛ حساب قبل از تکمیل TOTP همچنان غیرفعال می‌ماند.

- **CI و اصلاح مبتنی بر شواهد:** Vendor CI run [40451859716](https://github.com/hamidreza1394/Accounting/actions/runs/40451859716) در آزمون HTTP شکست خورد: انتظار `BadRequest` اما `TooManyRequests` دریافت شد، چون تست چند تلاش ورود و سپس تلاش تغییر رمز را با یک policy مشترک پنج‌در-دقیقه اجرا می‌کرد. Build، typecheck، UI tests، audit وابستگی‌ها و Vite build موفق بودند. برای جلوگیری از coupling غیرضروری، policy مستقل `vendor-password-change` با همان حد ۵ تلاش در دقیقه افزوده شد. این اصلاح هنوز باید با Vendor CI جدید تأیید شود.

- **بازبینی تکمیلی ممیزی:** audit writer فقط correlation ID به‌شکل GUID یا W3C trace ID می‌پذیرد؛ `HttpContext.TraceIdentifier` این قالب را تضمین نمی‌کند. endpoint اصلاح شد تا از `Activity.Current.TraceId` یا GUID استفاده کند. همچنین تغییر هش، پاک‌کردن `MustChangePassword` و append زنجیرهٔ ممیزی اکنون در یک تراکنش SQL Server مشترک هستند؛ شکست ممیزی باید تغییر گذرواژه را rollback کند. CI جدید باید این مسیر را تأیید کند.


## 2026-10-27 — Issue #161: مدیریت نقش Vendor و زیرساخت مجوزدهی منبع‌محور
- **وضعیت مبنا:** PR #160 با commit ادغام `c4cc59bd93545e5b0e28f5cc35d76d97f731cccd` ادغام شده است؛ Vendor CI run `40452250226` و .NET CI run `40452250285` روی SHA پیش از ادغام موفق شدند.
- **مدیریت نقش:** شاخه `feat/vendor-rbac-management-resource-authorization` APIهای فهرست/خواندن/ایجاد نقش، به‌روزرسانی مجموعهٔ مجوز و تخصیص نقش به مدیر موجود را اضافه می‌کند. همهٔ مسیرهای مدیریتی نیازمند `vendor.administrators.manage` و گارد MFA هستند.
- **عدم ارتقای امتیاز:** نقش `Vendor.SuperAdmin` تغییرناپذیر است؛ API عادی نمی‌تواند این نقش یا مجوز `vendor.administrators.manage` را اعطا کند. bootstrap تعاملی یک‌باره، نقش SuperAdmin و مجوزهای نسخه‌دار را در تراکنش فعال‌سازی حساب ایجاد/تأیید و به مدیر اولیه تخصیص می‌دهد.
- **ممیزی تراکنشی:** ساخت نقش، تغییر مجوزها، تخصیص نقش و تخصیص SuperAdmin رویداد ممیزی دارند. تغییرات مدیریتی با append ممیزی در یک تراکنش Vendor SQL Server انجام می‌شوند.
- **گارد منبع‌محور:** `VendorResourcePermissionAuthorizationHandler` مجوز عمومی را با بررسی resource-specific ترکیب می‌کند؛ کاتالوگ فقط customer/installation/contract/license و عملیات متناظر را می‌پذیرد. adapter پیش‌فرض `DenyAllVendorResourceAccessEvaluator` است تا فقدان پیاده‌سازی منبع، مجوز ایجاد نکند.
- **محدودیت دامنه:** هنوز entity/API واقعی مشتری، نصب، قرارداد و لایسنس در Vendor وجود ندارد. بنابراین IDOR برای رکوردهای واقعی حل‌شده تلقی نمی‌شود؛ adapter واقعی و آزمون cross-resource باید با مدل دامنه در مرحلهٔ تجاری اضافه شود.
- **تست‌های افزوده:** آزمون عضویت و مجموعهٔ مجوزهای SuperAdmin پس از bootstrap، کاتالوگ منابع، deny-by-default، و الزام سیاست administrators.manage برای تمام endpointهای مدیریتی.
- **اعتبارسنجی:** کد در GitHub ثبت شده است؛ Build/Test محلی اجرا نشده. Vendor CI و .NET CI روی آخرین SHA این شاخه باید موفق شوند؛ تا آن زمان وضعیت `Implemented / Not Verified` است.
- **داده:** هیچ Migration دیتابیس Vendor یا مشتری اضافه نشده و هیچ دیتابیس مشتری تغییر نکرده است.
- **مستندات:** `Vendor/README.md`, `docs/SECURITY.md`, این handoff و `docs/WORK_LOG.md` به‌روز می‌شوند.

- **تکمیل API نقش‌ها:** افزوده شد endpoint خواندن کاتالوگ مجوز نسخه‌دار و لغو تخصیص نقش سفارشی؛ لغو یا تخصیص نقش `Vendor.SuperAdmin` از مسیر API ممنوع است. endpointهای جدید نیز زیر سیاست `administrators.manage` قرار دارند.

- **بازبینی امنیتی CSRF:** چهار endpoint تغییردهندهٔ مدیریت نقش/مجوز اکنون پیش از هر اثر جانبی `IAntiforgery.IsRequestValidAsync` را بررسی می‌کنند؛ از اتکا به `UseAntiforgery` بدون بررسی صریح در endpointهای JSON اجتناب شد.

## 2026-10-27 — مدل دامنه و API اولیهٔ Vendor (Issue #163، شاخهٔ در حال اعتبارسنجی)
- **دامنه:** ایجاد مدل‌های مستقل Vendor برای مشتری، نصب، قرارداد و مجوز محصول؛ هیچ ارجاعی به DbContext یا Migrationهای حسابداری مشتری اضافه نشده است.
- **مدل و ذخیره‌سازی:** جدول‌های VendorCustomers، VendorInstallations، VendorContracts و VendorLicenses، کلیدهای خارجی محدودکنندهٔ حذف، شناسه‌های یکتا و rowversion برای آمادگی کنترل هم‌زمانی اضافه شده‌اند. Migration با شناسهٔ 20261027060000_VendorBusinessDomain و Snapshot به‌روزرسانی شده‌اند؛ تأیید نهایی EF توسط CI هنوز لازم است.
- **API:** فهرست/جزئیات منابع و ایجاد مشتری، نصب، قرارداد و لایسنس زیر /api/vendor/* اضافه شده‌اند. عملیات ایجاد، anti-forgery، اعتبارسنجی سمت سرور، کنترل رابطهٔ والد و ممیزی تراکنشی دارند. مسیرهای تجاری عمداً از /api/admin/* جدا شده‌اند چون آن namespace به مدیریت امتیازات ادمین اختصاص دارد.
- **کنترل دسترسی منبع:** VendorResourceAccessEvaluator رکورد واقعی را از VendorDbContext بارگذاری/بررسی می‌کند؛ شناسهٔ والد در صورت وجود فقط با رابطهٔ ذخیره‌شده تطبیق داده می‌شود. برای لایسنس، هم‌خوانی مشتری قرارداد و نصب نیز بررسی می‌شود.
- **محدودیت‌ها:** این مرحله هنوز API ویرایش/انتقال وضعیت، UI تجاری، تست cross-resource واقعی روی SQL Server و صدور رمزنگاری‌شدهٔ لایسنس را کامل نمی‌کند. وجود metadata لایسنس معادل امضای دیجیتال یا اعتبارسنجی آفلاین نیست. قبل از اعلام تکمیل، CI، Snapshot و آزمون‌های منفی باید تأیید شوند.
- **اعتبارسنجی:** در این محیط Build محلی اجرا نشده است؛ CI روی آخرین SHA شاخه باید مرجع نهایی باشد.
- **مرز داده:** Migration فقط برای دیتابیس مستقل Vendor است؛ هیچ Migration دیتابیس حسابداری مشتری ساخته نشده است.


## 2026-10-27 — Issue #165: صفحه‌بندی bounded برای APIهای تجاری Vendor
- **مبنا:** ادغام PR #164 در main؛ شاخه `feat/issue-165-vendor-api-pagination`.
- **تغییر:** endpointهای فهرست مشتریان، نصب‌ها، قراردادها و لایسنس‌ها از `page` و `pageSize` پشتیبانی می‌کنند؛ مقدار پیش‌فرض 25 و سقف 100 است. پرس‌وجو با `CountAsync`، `Skip` و `Take` در دیتابیس صفحه‌بندی می‌شود و پاسخ metadata صفحه دارد.
- **تست:** `tests/Vendor.Host.Tests/VendorPageRequestTests.cs` برای مقادیر پیش‌فرض/مرزی، جلوگیری از overflow در محاسبه Skip و metadata اضافه شد؛ اجرای تست هنوز به CI وابسته است.
- **داده/امنیت:** هیچ Migration یا تغییر دیتابیس مشتری انجام نشده؛ مجوزهای فعلی endpointها حفظ شده‌اند. تغییر قرارداد پاسخ فهرست باید در UI و مصرف‌کننده‌ها هماهنگ شود.
- **وضعیت اعتبارسنجی:** Build و تست .NET در این محیط اجرا نشده‌اند؛ تا نتیجه CI، وضعیت `Implemented / Not Verified` است.
- **گزارش:** `docs/handoffs/ISSUE-165-vendor-api-pagination.md`.


## 2026-10-27 — Issue #169: اتصال فهرست‌های تجاری Vendor به API
- ناوبری چهار بخش مشتریان، نصب‌ها، قراردادها و لایسنس‌ها به viewهای واقعی فهرست متصل شد؛ هر فهرست از endpoint Vendor با کوکی هم‌مبدأ داده می‌خواند و از envelope صفحه‌بندی استفاده می‌کند.
- UI شامل loading، خطای مجوز/نشست/محدودیت/سرور، empty state، تاریخ فارسی، وضعیت/محیط ترجمه‌شده و صفحه‌بندی 25/50/100 است؛ دادهٔ ساختگی برای فهرست نمایش داده نمی‌شود.
- تست‌های UI برای داده و صفحه‌بندی، 403 و empty state افزوده شدند. اجرای typecheck/test/build و CI هنوز انجام/تأیید نشده؛ وضعیت Implemented / Not Verified.
- محدوده فقط خواندن است؛ فرم‌های ایجاد/ویرایش و چرخه صدور لایسنس خارج از این Issue هستند. هیچ Schema/Migration یا دیتابیس حسابداری مشتری تغییر نکرده است.

- تست تکمیلی Issue #169: endpoint نصب‌ها نیز با پاسخ صفحه‌بندی و ترجمه محیط عملیاتی در UI پوشش داده شد؛ اجرای خودکار تست همچنان منتظر CI است.

- بازبینی نهایی UI: هنگام تازه‌سازی loading/error پاک می‌شود پیش از خالی‌کردن داده تا پیام empty state لحظه‌ای و گمراه‌کننده نمایش داده نشود.

## 2026-10-27 — Issue #171: ویرایش امن و کنترل هم‌زمانی APIهای تجاری Vendor
- **دامنه:** افزودن PUTهای allow-list برای مشتری، نصب، قرارداد و metadata لایسنس؛ پاسخ DTOها RowVersion را به شکل Base64 ارائه می‌کنند و ورودی‌های نوشتن RowVersion هشت‌بایتی SQL Server را الزام می‌کنند.
- **هم‌زمانی:** EF Core از OriginalValue روی concurrency token استفاده می‌کند؛ token نامعتبر پاسخ 400 و نسخهٔ stale پاسخ 409 می‌گیرد. RowVersion موجود استفاده شده و Migration جدیدی ساخته نشده است.
- **اعتبارسنجی و امنیت:** عملیات تغییر Anti-forgery و permission policy دارند؛ دسترسی به رکورد با evaluator منبع‌محور بررسی می‌شود؛ CustomerId تغییرپذیر نیست و رابطهٔ Contract/Installation برای License از دیتابیس بررسی می‌شود. عملیات update و audit در یک تراکنش قرار دارند.
- **وضعیت:** انتقال وضعیت فقط Active↔Suspended برای Customer و Installation مجاز است. Contract و License فقط در Draft ویرایش می‌شوند؛ انتقال وضعیت آن‌ها عمداً تا تعیین matrix پذیرفته‌شده و maker-checker انجام نمی‌شود. Metadata لایسنس به معنی صدور/ابطال رمزنگاری‌شده نیست.
- **تست‌ها:** \`VendorBusinessMutationRulesTests\` برای rowversion/status/environment و \`VendorPersistenceTests.Business_records_use_sql_server_rowversion_concurrency_tokens\` برای metadata مدل اضافه شد. CI مربوط به PR #172 روی commit شاخه موفق شد: Vendor CI run `40467317429` و .NET CI run `40467317576`؛ تمام jobها از جمله Windows Publish و E2E سبز شدند و 569 تست .NET موفق بود. PR #172 با merge commit `8f1f021fd0f8b1c9faf0f24f4a1deef708245643` در main ادغام شده است. تست HTTP end-to-end و آزمون cross-resource/rollback همچنان پوشش تکمیلی‌اند.
- **محدودیت:** تست HTTP end-to-end برای هر چهار PUT، آزمون cross-resource واقعی و آزمون rollback در SQL Server هنوز به پوشش تکمیلی نیاز دارند؛ این PR آن‌ها را به‌عنوان انجام‌شده ادعا نمی‌کند.
- **داده:** هیچ Migration یا تغییر Schema دیتابیس حسابداری مشتری؛ هیچ کلید خصوصی امضا یا API صدور واقعی لایسنس اضافه نشده است.
- **Branch:** \`feat/issue-171-vendor-update-lifecycle\`; **Issue:** #171.


## 2026-10-27 — Issue #174: پاسخ پایدار برای تعارض کلید یکتا در درخواست‌های هم‌زمان Vendor
- **مسئله:** بررسی اولیه با `AnyAsync` به‌تنهایی جلوی رقابت دو درخواست را نمی‌گیرد؛ ایندکس یکتای SQL Server ممکن است یکی را با خطای 2601/2627 رد کند.
- **تغییر:** endpointهای ایجاد و ویرایش مشتری، نصب، قرارداد و لایسنس اکنون فقط خطاهای unique-key شناخته‌شده SQL Server را به HTTP 409 و کد خطای قراردادی همان منبع تبدیل می‌کنند. خطای هم‌زمانی RowVersion همچنان `stale_row_version` است؛ خطاهای اتصال، FK و خطاهای ناشناخته بلعیده نمی‌شوند.
- **تراکنش:** تعارض رخ‌داده پس از شروع تراکنش پیش از ثبت ممیزی برمی‌گردد؛ با خروج از scope تراکنش ناتمام rollback می‌شود. ممیزی برای عملیات ناموفق ثبت نمی‌شود.
- **آزمون:** CI این شاخه باید compile/test/publish را تأیید کند. آزمون اختصاصی رقابت دو درخواست روی SQL Server هنوز باید اضافه شود؛ این تغییر ادعای پوشش race واقعی در تست را ندارد.
- **Issue:** #174؛ **Branch:** `fix/vendor-unique-conflict-races`.


## 2026-10-27 — Vendor business HTTP authorization regression coverage (Issue #176)

- **Scope:** add production-host HTTP regression tests for the authentication boundary on customer, installation, contract, and license collection/read and mutation endpoints.
- **Implementation:** `tests/Vendor.Host.Tests/VendorBusinessMutationAuthorizationIntegrationTests.cs` sends anonymous GET/PUT/status requests through `WebApplicationFactory<Program>` and asserts HTTP 401. This verifies that the real host's authorization middleware protects the business endpoints; it does not replace the production authorization handlers.
- **Explicit limitation:** this is an initial slice of Issue #176, not its full acceptance. It does not yet test authenticated permissions, anti-forgery validation, SQL Server persistence, rowversion conflicts, audit rollback, cross-resource validation, or concurrent unique-key races.
- **Validation:** no local .NET execution was available in this connector-only workflow. CI must validate compilation and execution. Do not treat this commit as full integration coverage or production-readiness evidence.
- **Isolation:** tests use the real Vendor host but anonymous requests should be rejected by authorization before business handlers execute. No database is seeded or mutated by these tests; no customer accounting database is involved.


## 2026-10-27 — Authenticated customer mutation HTTP integration test

- **Scope:** add a narrow SQL Server-backed integration test for customer metadata updates using a unique disposable Vendor database per test.
- **Production path exercised:** ASP.NET Core host, real Identity password + TOTP flow, production role-claim permission handler, MFA administrator requirement, persisted resource authorization, anti-forgery, SQL Server rowversion, and audit writer.
- **Assertions:** missing/invalid anti-forgery token is rejected; malformed RowVersion returns 400; a valid update succeeds; replaying the old RowVersion returns 409; the updated customer remains persisted and exactly one matching `vendor.customer.updated` audit event exists.
- **Isolation:** database name is unique per test and cleanup drops only that generated Vendor test database. The test does not use or access any customer accounting database.
- **Validation status:** CI is required; this connector workflow does not provide a local .NET execution environment. Do not claim the test passed until its workflow completes.
- **Remaining:** equivalent successful/stale/malformed coverage for installation, contract, and license updates; cross-resource mismatch, draft-only lifecycle, operational status transitions, audit-failure rollback, and deterministic SQL Server duplicate-key race coverage.


## 2026-10-27 — اصلاح شکست CI آزمون HTTP ویرایش مشتری Vendor
- **شکست مشاهده‌شده:** Vendor CI run `40489907527`، آزمون `CustomerUpdateEnforcesAntiforgeryAndRowVersionAndAuditsOnlyCommittedWrites` شکست خورد؛ 68 آزمون موفق و 1 آزمون ناموفق بود. assertion روی `invalid_row_version` شکست خورد.
- **علت ریشه‌ای از لاگ:** توکن ضد-CSRF قبل از ورود ناشناس دریافت شده و پس از ورود موفق با TOTP دوباره استفاده می‌شد. ASP.NET Core آن را به دلیل تعلق داشتن به هویت claims متفاوت رد می‌کرد؛ بنابراین درخواست پیش از رسیدن به اعتبارسنجی RowVersion رد می‌شد.
- **اصلاح:** پس از تکمیل MFA، آزمون اکنون توکن ضد-CSRF تازه‌ای از `/api/auth/antiforgery` دریافت می‌کند و همان توکن را برای درخواست‌های تغییر داده استفاده می‌کند. توکن ناشناس فقط برای درخواست‌های ورود باقی می‌ماند.
- **Commit اصلاح:** `86828e36ee2f3d9c473ef7fb8df01fca0dc24b93` روی `test/vendor-customer-mutation-http`.
- **اعتبارسنجی:** اجرای CI پس از اصلاح هنوز تأیید نشده؛ PR #178 در ادامه با merge commit `63681bc4119f347e706620f8541a571df3b3d7ee` ادغام شد، اما workflowهای بعدی لغو شدند و نتیجهٔ سبز برای آزمون اصلاح‌شده در دست نیست.


## 2026-10-27 — گسترش آزمون HTTP برای ویرایش نصب، قرارداد و لایسنس Vendor

- **پایه:** PR #178 با merge commit `63681bc4119f347e706620f8541a571df3b3d7ee` ادغام شده است. اجرای CI پس از اصلاح توکن ضد-CSRF پیش از تکمیل توسط ادغام/لغو workflow متوقف شد؛ بنابراین موفقیت آزمون اصلاح‌شده هنوز اثبات نشده است.
- **تغییر آزمون:** در `VendorCustomerMutationHttpIntegrationTests`، نقش تست علاوه بر مدیریت مشتری، مجوزهای مدیریت نصب، قرارداد و لایسنس را نیز دریافت می‌کند؛ داده‌های مربوطه در دیتابیس موقت Vendor ساخته می‌شوند.
- **نصب و قرارداد:** درخواست PUT واقعی با RowVersion جاری باید موفق شود؛ بازپخش همان RowVersion باید HTTP 409 با `stale_row_version` بدهد.
- **لایسنس:** پیوند قرارداد متعلق به مشتری دیگر باید با `contract_customer_mismatch` رد شود؛ پس از استفاده از قرارداد و نصب متعلق به مشتری صحیح، PUT موفق و بازپخش RowVersion قدیمی با 409 رد می‌شود.
- **ممیزی:** آزمون وجود دقیقاً یک رویداد ممیزی برای هر ویرایش موفق نصب، قرارداد و metadata لایسنس را بررسی می‌کند.
- **ایزوله‌سازی:** فقط دیتابیس موقت Vendor با نام تصادفی ساخته و در پایان حذف می‌شود؛ دیتابیس حسابداری مشتری استفاده نمی‌شود.
- **Commitهای این مرحله:** `bbf37ac1fe8a5c1d0b2f1bb7b668eb10124a56d4` و `f0c2e7a547e556f8d6e3cad7e849503f96c8e17c` روی شاخه `test/vendor-business-mutation-http-coverage`.
- **اعتبارسنجی:** Build/Test محلی در این محیط اجرا نشده؛ منتظر CI واقعی روی شاخه جدید هستیم. آزمون‌های نصب/قرارداد/لایسنس، تا سبز شدن CI، تأییدشده محسوب نمی‌شوند.
