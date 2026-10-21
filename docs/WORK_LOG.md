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
