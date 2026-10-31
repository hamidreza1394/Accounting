

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


- **افزایش پوشش روابط لایسنس:** آزمون اکنون علاوه بر قرارداد نامرتبط، نصب متعلق به مشتری دیگر را نیز با قرارداد معتبر آزمایش می‌کند و انتظار `installation_customer_mismatch` دارد. Commit: `8b35026d709538ba29fa8c88523b3166599477cd`. اجرای CI برای این آخرین تغییر هنوز در انتظار است.

- **اصلاح کنترل RowVersion در بازپخش بدون تغییر:** CI نشان داد ارسال دوباره همان بدنهٔ PUT با RowVersion قدیمی می‌توانست `200 OK` بگیرد، چون EF Core در نبود تغییر واقعی ممکن است دستور UPDATE تولید نکند و بررسی concurrency سمت SQL Server اجرا نشود. در هر شش مسیر ویرایش/تغییر وضعیت، RowVersion ارسالی اکنون پیش از تغییر داده با مقدار فعلی موجودیت مقایسه می‌شود؛ توکن قدیمی به `409 stale_row_version` منجر می‌شود و کنترل concurrency در `SaveChanges` همچنان برای race بعد از خواندن باقی می‌ماند. Commit: `7bc21cf62dc2e1076353f652f724ec6ca4ab678a`. CI مجدد هنوز باید این اصلاح را تأیید کند.


## 2026-10-27 — افزودن آزمون HTTP انتقال وضعیت عملیاتی Vendor
- **دامنه:** ادامه Issue #176 پس از ادغام PR #179 در main با merge commit `b425fd06856da30f187836b4c1b2de547921369a`.
- **تغییر:** در `tests/Vendor.Host.Tests/VendorCustomerMutationHttpIntegrationTests.cs`، آزمون HTTP مشتری اکنون انتقال وضعیت `Active -> Suspended -> Active` را با RowVersion تازهٔ دریافتی از پاسخ هر درخواست بررسی می‌کند و انتقال غیرمجاز `Active -> Draft` را با انتظار HTTP 409 و `invalid_status_transition` می‌آزماید.
- **ایمنی:** آزمون از همان میزبان واقعی، کوکی MFA، مجوزهای production و دیتابیس موقت Vendor موجود در تست استفاده می‌کند؛ دیتابیس حسابداری مشتری تغییر نمی‌کند.
- **اعتبارسنجی:** هنوز اجرا نشده؛ CI روی شاخه `test/vendor-operational-status-http-coverage` باید تأیید کند آزمون SQL Server واقعاً اجرا شده و موفق است. نتیجه را پیشاپیش سبز فرض نمی‌کنیم.
- **باقی‌مانده:** آزمون انتقال وضعیت نصب، شکست Audit و Rollback، رقابت کلید یکتا برای SQL Server 2601/2627 و تأیید کامل CI پس از ادغام PR #179.

- **گسترش تکمیلی:** همان آزمون، انتقال وضعیت نصب `Active -> Suspended` و رد `Suspended -> Draft` را نیز با HTTP واقعی و RowVersion تازه بررسی می‌کند. Commit: `77f3ce8b67660541c9c05d4593a8022b3bc1e3b5`.


## 2026-10-27 — آزمون Rollback در صورت شکست Audit
- **تغییر:** آزمون HTTP واقعی مشتری در `VendorCustomerMutationHttpIntegrationTests` اکنون یک شکست کنترل‌شده در `IVendorAuditWriter` تزریق می‌کند؛ پس از خطای درخواست، مقدار مشتری و تعداد رویدادهای Audit را از SQL Server دوباره می‌خواند و عدم ماندگاری تغییر را بررسی می‌کند.
- **محدودیت ایمنی:** جایگزینی writer فقط در `WebApplicationFactory` آزمون انجام می‌شود؛ ثبت سرویس تولیدی تغییر نکرده و فقط دیتابیس موقت Vendor آزمون استفاده می‌شود.
- **اعتبارسنجی:** در انتظار CI برای شاخه `test/vendor-audit-failure-rollback-http`؛ نتیجه تا پایان اجرای آزمون SQL Server تأییدشده محسوب نمی‌شود.
- **باقی‌مانده:** آزمون قطعی رقابت کلید یکتا در SQL Server برای خطاهای 2601/2627 و بررسی نهایی معیارهای پذیرش Issue #176.


## 2026-10-27 — آزمون قطعی رقابت کلید یکتا در API فروشنده
- **دامنه:** تکمیل بخش باقی‌ماندهٔ Issue #174 و پوشش یکپارچگی Issue #176 برای race واقعی SQL Server.
- **تغییر:** در شاخه `test/vendor-unique-key-race-http`، یک `DbCommandInterceptor` مخصوص تست اضافه شد که دو درخواست احراز هویت‌شدهٔ ایجاد مشتری را دقیقاً در مرز اجرای INSERT همگام می‌کند؛ بنابراین هر دو درخواست از بررسی اولیهٔ تکراری‌بودن عبور می‌کنند و شاخص یکتای SQL Server باید برنده را تعیین کند.
- **انتظار آزمون:** دقیقاً یک پاسخ HTTP 201 و یک پاسخ HTTP 409 با کد `customer_number_exists`؛ فقط یک رکورد مشتری و فقط یک رویداد Audit متناظر باقی بماند.
- **ایمنی:** Interceptor فقط در `WebApplicationFactory` و فقط در بازهٔ آزمون رقابت فعال است. هیچ ثبت سرویس تولیدی، Migration یا دیتابیس حسابداری مشتری تغییر نمی‌کند.
- **اعتبارسنجی:** تا این لحظه CI برای این شاخه اجرا/تأیید نشده است؛ سبز بودن Build و اجرای واقعی تست SQL Server باید پس از CI بررسی شود. اجرای محلی از طریق اتصال GitHub در دسترس نیست.


## 2026-10-27 — سخت‌کردن اثبات آزمون رقابت SQL Server
- **یافتهٔ بازبینی:** پاسخ‌های 201/409 به‌تنهایی اثبات نمی‌کردند که هر دو INSERT به Interceptor رسیده‌اند؛ اگر شرط متن SQL تطبیق نمی‌کرد، بررسی اولیهٔ تکراری‌بودن می‌توانست همان نتایج را تولید کند.
- **اصلاح:** تست اکنون تعداد INSERTهای مشاهده‌شده توسط gate را دقیقاً ۲ می‌سنجد؛ در نتیجه عدم تطبیق شرط SQL باعث شکست تست می‌شود، نه موفقیت کاذب.
- **اعتبارسنجی قبلی:** PR #182 با Vendor CI و .NET CI سبز ادغام شد؛ Vendor CI متغیر `FARANO_REQUIRE_VENDOR_SQLSERVER=1` و صفر تست Skip را گزارش کرد. این شواهد مربوط به نسخهٔ پیش از assertion جدید است.
- **وضعیت اصلاح فعلی:** CI جدید هنوز لازم است تا اجرای SQL Server و assertion جدید را تأیید کند.


## 2026-10-27 — رابط کاربری ثبت و ویرایش مشتری تجاری Vendor
- **Issue:** #184؛ PR: #185.
- **تغییر:** فرم RTL ثبت/ویرایش مشتری تجاری به فهرست Vendor اضافه شد؛ درخواست‌ها از API واقعی استفاده می‌کنند، برای هر نوشتن antiforgery تازه می‌گیرند و در ویرایش RowVersion را ارسال می‌کنند.
- **کنترل دسترسی UI:** `/api/auth/me` مجوزهای مؤثر نشست را در قالب فهرست مرتب و یکتا می‌دهد؛ کنترل‌های نوشتن فقط برای `vendor.customers.manage` نشان داده می‌شوند. این کنترل نمایشی است؛ مجوز واقعی همچنان در سرور و با نقش/مجوز فعلی دیتابیس بررسی می‌شود.
- **تست‌ها:** Vitest برای ایجاد موفق، خطای تکرار، RowVersion قدیمی و نمایش کنترل‌ها بر اساس مجوز؛ تست احراز هویت .NET برای قرارداد permissions.
- **محدودیت:** این فرم فقط رکورد مشتری تجاری Vendor را مدیریت می‌کند؛ شرکت حسابداری، دیتابیس مشتری یا کاربر/رمز اولیه مشتری ایجاد نمی‌شود.
- **اعتبارسنجی:** CI نهایی برای آخرین commit PR #185 هنوز لازم است. وضعیت تا موفقیت Vendor CI و .NET CI، `Implemented / Not yet Verified` باقی می‌ماند.


## 2026-10-27 — اجرای Vendor CI روی push شاخه‌های کاری Vendor
- **یافته:** به‌روزرسانی‌های شاخه از طریق GitHub Contents API به‌صورت رویداد `push` ثبت می‌شدند؛ Vendor CI فقط روی push به `main` و روی رویدادهای PR اجرا می‌شد و برای برخی commitهای نهایی PR check جدید ایجاد نمی‌کرد.
- **اصلاح:** workflow مستقل Vendor اکنون علاوه بر `main`، push شاخه‌های `feat/vendor-*`، `fix/vendor-*`، `test/vendor-*` و `ci/vendor-*` را نیز بررسی می‌کند؛ فیلتر مسیرهای Vendor و قراردادهای مرتبط حفظ شده‌اند، بنابراین تغییرات غیرمرتبط Accounting باعث اجرای این workflow نمی‌شوند.
- **دلیل:** تضمین اجرای واقعی TypeScript/Vitest و تست‌های SQL Server Vendor روی head جاری، نه اتکا به نتیجهٔ commit قدیمی‌تر.
- **اعتبارسنجی:** CI باید روی آخرین head این PR سبز شود؛ نتیجهٔ workflow قبل از تغییر trigger برای آخرین commit ادعا نمی‌شود.


## 2026-10-27 — رابط کاربری مدیریت نصب‌های Vendor
- **Issue:** #186.
- **تغییر:** فرم RTL ثبت/ویرایش نصب به APIهای موجود وصل شد؛ انتخاب مشتری از فهرست واقعی صفحه‌بندی‌شده (100 مورد در هر صفحه)، محیط نصب با `ManagedSelect` و مقدار پیش‌فرض Production، ویرایش metadata با RowVersion و تغییر وضعیت Active/Suspended از مسیر مجزای API اضافه شد.
- **کنترل دسترسی:** دکمه‌های ویرایش/وضعیت به `vendor.installations.manage` وابسته‌اند؛ ثبت نصب علاوه بر آن به `vendor.customers.read` نیاز دارد تا مشتری واقعی انتخاب شود. در نبود CustomersRead، ثبت نصب غیرفعال است. Authorization نهایی سمت سرور باقی می‌ماند.
- **امنیت:** همهٔ عملیات نوشتن توکن antiforgery تازه و RowVersion را استفاده می‌کنند؛ conflictهای stale و انتقال وضعیت نامعتبر پیام فارسی دارند. دیتابیس حسابداری مشتری یا provisioning تغییر نمی‌کند.
- **تست‌ها:** Vitest برای ایجاد، محیط پیش‌فرض، نبود مجوز مشتری، ویرایش با RowVersion و تعارض؛ تست‌های UI برای تغییر وضعیت نصب و stale conflict.
- **اعتبارسنجی:** Vendor CI و .NET CI روی head نهایی باید سبز شوند؛ تا آن زمان `Implemented / Not yet Verified`.


## 2026-10-27 — رابط کاربری ثبت و ویرایش قراردادهای Draft
- **Issue:** #191.
- **تغییر:** فرم RTL ثبت/ویرایش قرارداد به APIهای موجود متصل شد؛ انتخاب مشتری از فهرست صفحه‌بندی‌شده، شماره قرارداد، تاریخ شروع/پایان، کد ارز سه‌حرفی و RowVersion در ویرایش پیاده‌سازی شدند.
- **زمان:** تاریخ‌های UI به نیمه‌شب UTC تبدیل می‌شوند تا timezone مرورگر تاریخ قرارداد را تغییر ندهد؛ ارز پیش‌فرض `IRR` است و در ارسال uppercase می‌شود.
- **کنترل دسترسی و وضعیت:** فرم ایجاد نیازمند `vendor.contracts.manage` و `vendor.customers.read` است. فقط Draft قابل ویرایش است؛ endpoint تغییر وضعیت قرارداد در API موجود نیست، بنابراین UI وضعیت را تغییر نمی‌دهد.
- **تست‌ها:** Vitest برای ایجاد با مشتری واقعی و CSRF، تاریخ UTC، نبود مجوز مشتری، RowVersion stale و نمایش ویرایش فقط برای Draft.
- **اعتبارسنجی:** Vendor CI و .NET CI روی head نهایی باید سبز شوند؛ تا آن زمان `Implemented / Not yet Verified`.


## 2026-10-27 — کمبوباکس ارز در فرم قرارداد Vendor
- **زمینه:** PR #192 پس از اصلاح شرط نمایش ستون عملیات ادغام شد؛ بازبینی تکمیلی نشان داد ارز قرارداد هنوز ورودی آزاد است، در حالی که UI باید گزینه‌های رایج را به‌صورت کمبوباکس ارائه کند.
- **تغییر:** ارزهای رایج USD/EUR/IRR/AED در `ManagedSelect` ارائه می‌شوند. اگر قرارداد قدیمی کد ارز سه‌حرفی دیگری داشته باشد، مقدار ذخیره‌شده به‌عنوان «ارز فعلی ثبت‌شده» باقی می‌ماند تا ویرایش سایر فیلدها آن را ناخواسته تغییر ندهد.
- **تست بازگشت:** تست ایجاد قرارداد اکنون از کمبوباکس ارز استفاده می‌کند؛ تست تازه تأیید می‌کند کد ارز موجود خارج از فهرست رایج انتخاب‌شده و حفظ می‌شود.
- **دامنه:** فقط UI و مستندات Vendor تغییر کرده‌اند؛ API، دیتابیس مشتری و migrationها دست‌نخورده‌اند.
- **اعتبارسنجی:** CI شاخهٔ `feat/vendor-contract-currency-select` پس از تغییر باید سبز شود؛ تا آن زمان این تغییر تأییدشده محسوب نمی‌شود.


## 2026-10-27 — Issue #195: رابط مدیریت metadata لایسنس Vendor
- **هدف:** افزودن ثبت و ویرایش metadata لایسنس‌های Draft در پنل Vendor با endpointهای فعلی، بدون ادعای صدور یا activation رمزنگاری‌شده.
- **UI:** فرم RTL برای مشتری، شماره لایسنس، کد محصول، بازه اعتبار، قرارداد اختیاری و نصب اختیاری اضافه شد. کنترل‌های ایجاد/ویرایش به `vendor.licenses.manage` وابسته‌اند؛ ایجاد علاوه بر آن به `vendor.customers.read` نیاز دارد و ویرایش فقط برای Draft نمایش داده می‌شود.
- **فیلتر مرجع:** endpointهای فهرست نصب/قرارداد/لایسنس پارامتر اختیاری `customerId` می‌پذیرند و فیلتر را سمت سرور اعمال می‌کنند. UI نیز آیتم‌هایی را که `customerId` آن‌ها با مشتری انتخاب‌شده سازگار نیست حذف می‌کند؛ این فقط دفاع تکمیلی است و کنترل مالکیت سمت سرور باقی می‌ماند.
- **امنیت:** mutation با antiforgery token تازه و RowVersion انجام می‌شود؛ در نبود مجوز خواندن فهرست قرارداد/نصب، رابطه موجود در ویرایش حفظ و غیرقابل‌تغییر است. API همچنان کنترل نهایی روابط مشتری، قرارداد و نصب را انجام می‌دهد.
- **تست‌ها:** Vitest برای ایجاد، تاریخ UTC، رابطه‌های اختیاری، نبود مجوز، تعارض RowVersion و ویرایش Draft-only اضافه شد. تست HTTP مبتنی بر SQL Server فیلتر customerId را برای فهرست‌های نصب، قرارداد و لایسنس بررسی می‌کند.
- **محدودیت:** `ProductCode` متن آزاد با سقف 64 نویسه باقی می‌ماند، چون کاتالوگ محصول/زیرسیستم در API موجود نیست. صدور امضاشده، KMS/HSM و activation آفلاین خارج از این Issue هستند.
- **دامنه:** فقط Vendor Host/UI، تست‌های Vendor و مستندات مرتبط تغییر می‌کنند؛ هیچ Migration یا دیتابیس حسابداری مشتری تغییر نکرده است.
- **اعتبارسنجی:** Vendor CI و .NET CI روی head نهایی باید سبز شوند؛ تا آن زمان وضعیت `Implemented / Not yet Verified` است.


## 2026-10-27 — اصلاح شکست آزمون کمبوباکس در Issue #195
- **شاهد شکست:** Vendor CI run [40549993097](https://github.com/hamidreza1394/Accounting/actions/runs/40549993097) روی commit `7ce4c4125f6f29c452de207d0605f8eb563c3280` با ۱ شکست از ۳۷ تست UI متوقف شد: فرم لایسنس گزینهٔ `CTR-001 — Draft` را نمی‌دید.
- **علت ریشه‌ای:** `ManagedSelect` وجود گزینهٔ فعال را با شرط `firstEnabledValue !== ''` تشخیص می‌داد. گزینهٔ معتبر «بدون قرارداد» مقدار خالی دارد؛ در نتیجه حتی وقتی گزینه‌های دیگر هم وجود داشتند، کنترل کاملاً غیرفعال و فهرست گزینه‌ها حذف می‌شد. متن CSS طولانی در گزارش تست فقط بخشی از DOM بود، نه علت خطا.
- **اصلاح:** تشخیص به `options.some(option => !option.disabled)` تغییر کرد تا مقدار خالیِ مجاز از نبود گزینهٔ فعال تفکیک شود. تست بازگشت برای انتخاب خالیِ اختیاری و گزینهٔ قراردادی اضافه شد.
- **اعتبارسنجی:** CI تازه برای commit `2eb44a82d6d9a81baa8fc9f8ead868c8eb93d697` در صف است؛ تا تکمیل Vendor CI و .NET CI نتیجهٔ اصلاح Verified نیست.


## 2026-10-27 — تکمیل و تأیید Issue #195 / PR #196
- **نتیجه نهایی:** PR #196 با merge commit `45e843ebe639b0013e9b0bc9d28106eb435e4742` ادغام شد.
- **CI:** Vendor CI [40550673063](https://github.com/hamidreza1394/Accounting/actions/runs/40550673063) و .NET CI [40550672932](https://github.com/hamidreza1394/Accounting/actions/runs/40550672932) روی head نهایی PR موفق شدند؛ .NET CI شامل build/test، Migration guards، E2E و publish Linux/Windows بود.
- **اصلاح regression:** `ManagedSelect` اکنون گزینهٔ معتبر با value خالی را به‌اشتباه غیرفعال نمی‌کند؛ تست اختصاصی این رفتار افزوده شد.
- **دامنه:** metadata لایسنس Draft ثبت/ویرایش می‌شود؛ صدور امضاشده، KMS/HSM و activation آفلاین همچنان خارج از این Issue و مانع Production هستند.
- **گام بعدی:** Issue #197 برای هم‌راستا کردن وضعیت مرکزی و مستندات Vendor با شواهد واقعی و فهرست صریح شکاف‌های Production ایجاد شد.


## 2026-10-27 — ثبت استاندارد کتابخانه‌ها، آفلاین‌بودن دارایی‌ها و قواعد طراحی/سرعت UI (D-049)
- **دامنه:** فقط مستندات، شاخهٔ `docs/ui-component-standards`؛ بدون تغییر کد، وابستگی یا Migration. Issue: `UNNUMBERED`.
- **تغییر:** `docs/UI_COMPONENTS.md` سه بخش گرفت (قواعد طراحی/سرعت/ویژوال؛ کتابخانه‌های پذیرفته‌شدهٔ جدول، نمودار و تاریخ؛ قانون محلی‌سازی دارایی‌ها). ADR جدید D-049 ثبت و ارجاع به آن در `AGENTS.md`، `CLAUDE.md`، `docs/INDEX.md` و Skillهای UI/مشترک افزوده شد. شمارهٔ «ADR بعدی» کهنه در `AGENTS.md` و `INDEX.md` به D-050 اصلاح شد.
- **اعتبارسنجی:** `git diff --check` و `scripts/check_static_assets.py` موفق؛ build/test اجرا نشد چون تغییر فقط مستندات است. CI ندیده شده است.
- **باقی‌مانده:** نصب هر کتابخانه کار جدا است؛ ساخت آفلاین `NeedsApproval`؛ سقف بودجهٔ سرعت `Deferred` تا اندازه‌گیری مبنا. جزئیات: `docs/handoffs/ISSUE-UNNUMBERED-ui-component-standards.md`.


## 2026-10-27 — استفادهٔ مشروط از KendoReact Free و مسیر گزارش پیوت (D-050)
- **دامنه:** فقط مستندات، شاخهٔ `docs/ui-component-standards`؛ بدون کد، وابستگی یا Migration. Issue: `UNNUMBERED`.
- **تغییر:** ADR `D-050` بند ممنوعیت KendoReact در D-049 را اصلاح کرد: فقط قابلیت‌های بدون نشان الماس، بدون PivotGrid و قابلیت پولی، یک گرید در هر صفحه، پایلوت پیش از پذیرش. `docs/UI_COMPONENTS.md` بخش KendoReact Free و بخش پیوت (Perspective کاندیدای آزمایشی؛ react-pivottable رهاشده) را گرفت.
- **اعتبارسنجی:** `git diff --check` موفق؛ build/test اجرا نشد؛ CI ندیده شده. اطلاعات مجوز از مستندات رسمی Telerik و منابع ثانویه در 2026-10-27 است و پیش از نصب باید دوباره بررسی شود.
- **باقی‌مانده:** نصب Kendo (در صورت تأیید) کار جدا با پایلوت و اندازه‌گیری باندل است؛ RTL/فارسی Kendo تأیید نشده. جزئیات: `docs/handoffs/ISSUE-UNNUMBERED-ui-component-standards.md`.

- **اصلاح (2026-10-27، همان روز):** عبارت «RTL Kendo تأیید نشد» در D-050 و `docs/UI_COMPONENTS.md` اصلاح شد: RTL عمومی و WCAG 2.2 AA گرید طبق مستندات رسمی Telerik تأیید شد؛ ارقام فارسی و تقویم شمسی همچنان تأیید نشده‌اند. build/test اجرا نشد (فقط مستندات).


## 2026-10-27 — افزودن مهارت ui-ux-pro-max برای کیفیت فرم‌ها
- **دامنه:** مهارت/مستندات، شاخهٔ `docs/ui-ux-pro-max-skill` (روی شاخهٔ PR #199)؛ بدون تغییر کد برنامه، وابستگی runtime یا Migration. Issue: `UNNUMBERED`.
- **تغییر:** کپی خام `.claude/skills/ui-ux-pro-max` از upstream کامیت `28b3b8dee8c976df4fa3e94aed4e092ad43fcb03` (MIT) بدون `scripts/tests/`؛ بند تقدم فرانو در `SKILL.md`؛ مجوز در `third_party/`؛ ثبت منشأ و بررسی در `docs/THIRD_PARTY_SKILLS.md`؛ راهنمای استفاده در `docs/UI_COMPONENTS.md` و ارجاع از `AGENTS.md`، `CLAUDE.md`، `docs/INDEX.md`.
- **عمداً کپی نشد:** CLI، افزونه و مهارت‌های `design`، `brand`، `slides`، `banner-design`، `ui-styling`، `design-system` (API بیرونی با کلید و CDN).
- **اعتبارسنجی:** مقایسهٔ بایتی با upstream، اجرای واقعی `search.py` (ux، shadcn، chart)، `check_static_assets.py` و `git diff --check` موفق؛ build/test اجرا نشد؛ CI ندیده شده.
- **باقی‌مانده:** سنجش فایدهٔ واقعی روی یک فرم؛ در صورت لزوم کاهش حجم داده؛ جزئیات: `docs/handoffs/ISSUE-UNNUMBERED-ui-ux-pro-max-skill.md`.


## 2026-10-27 — بهینه‌سازی lookup زنجیره Audit در Vendor (Issue #202)
- **هدف:** کاهش هزینهٔ جست‌وجوی برگ و والد زنجیره در VendorAuditWriter با ایندکس‌گذاری ستون‌هایی که در predicateهای PreviousHash و EventHash استفاده می‌شوند.
- **تغییر:** افزودن ایندکس IX_VendorAuditEvents_PreviousHash و ایندکس یکتای IX_VendorAuditEvents_EventHash؛ Migration جدید 20261027190000_VendorAuditChainIndexes فقط این دو ایندکس را می‌سازد و در Down حذف می‌کند. Snapshot و target model با مدل جاری هماهنگ شدند.
- **تست‌ها:** آزمون مدل برای یکتایی/وجود ایندکس‌ها و آزمون SQL Server برای Migration و وجود هر دو ایندکس افزوده شد.
- **محدوده:** فقط Vendor و تست‌ها/مستندات Vendor؛ هیچ دیتابیس یا Migration حسابداری مشتری تغییر نکرده است. ایندکس یکتا عمداً درج hash تکراری را در سطح دیتابیس رد می‌کند؛ اگر دادهٔ موجود ناسالم و دارای hash تکراری باشد، Migration باید fail کند تا داده پنهانی نادیده گرفته نشود.
- **اعتبارسنجی:** Build، تست .NET، SQL Server و CI هنوز اجرا/تأیید نشده‌اند در زمان ثبت این یادداشت؛ تغییر از GitHub API ثبت شده و باید از CI واقعی تأیید شود. تا آن زمان وضعیت Implemented / Not yet Verified است.
- **Branch:** fix/vendor-audit-chain-indexes-main; **Issue:** #202.

## 2026-10-27 — ادغام ایندکس‌های زنجیره Audit و شروع Maker-Checker
- **PR #203:** Migration مستقل 20261027190000_VendorAuditChainIndexes، ایندکس PreviousHash و unique EventHash، تست مدل و SQL Server ادغام شدند. CI روی head نهایی موفق بود: Vendor CI run 40563642222 و .NET CI run 40563642237؛ merge commit dfd5d941c6249570f8010176d16187e88f6d6217.
- **Issue #201 / PR #207 / شاخه feat/vendor-maker-checker-clean:** گردش تأیید دو نفره برای تغییر وضعیت مشتری/نصب در حال توسعه است. مدل درخواست تأیید، migration مستقل Vendor، endpointهای submit/review، منع self-approval، RowVersion و UI صف تأیید اضافه شده‌اند؛ تست‌های یکپارچه برای درخواست، تکرار، منع تأیید توسط خود درخواست‌کننده، تأیید مدیر مستقل و stale resource افزوده شده‌اند.
- **وضعیت اعتبارسنجی Maker-Checker:** هنوز CI اجرا نشده؛ هیچ‌یک از قابلیت‌های جدید در این بخش Verified اعلام نمی‌شوند تا Build، تست‌های SQL Server، تست UI، snapshot guard و publish/E2E سبز شوند.
- **محدوده:** فقط Vendor و تست‌ها/مستندات اختصاصی آن تغییر کرده‌اند؛ دیتابیس حسابداری مشتری دست‌نخورده است.

## 2026-10-27 — تأیید نهایی Maker-Checker مشتری/نصب (PR #207)
- **نتیجه:** PR #207 ادغام شد؛ merge commit `d644ab5d36d00b1ce3763b64f17fd6fe7ef9fbac`.
- **Vendor CI:** run [40570240409](https://github.com/hamidreza1394/Accounting/actions/runs/40570240409) موفق؛ TypeScript strict، Vitest، asset budget، build/test Vendor، SQL Server integration، EF snapshot guard و انتشار Vendor.
- **.NET CI:** run [40570240205](https://github.com/hamidreza1394/Accounting/actions/runs/40570240205) موفق؛ build/test، secret scan، E2E و انتشار Linux/Windows.
- **رفتارهای آزموده‌شده:** endpoint مستقیم وضعیت دیگر bypass ندارد؛ درخواست تکراری و انتقال نامعتبر رد می‌شود؛ self-approval رد می‌شود؛ مدیر دوم با MFA می‌تواند درخواست را اعمال کند؛ stale RowVersion درخواست را Stale می‌کند؛ Audit در تراکنش ثبت می‌شود.
- **محدوده:** فقط Vendor، تست‌ها و مستندات؛ دیتابیس حسابداری مشتری تغییر نکرده است. این کار به معنی تکمیل signing/KMS/HSM یا checkpoint مستقل Audit نیست.


## 2026-10-28 — تطبیق شواهد Bootstrap و RBAC پس از PR #207 (Issue #209)
- **هدف:** اصلاح وضعیت‌های تاریخی متناقض در اسناد امنیتی Vendor بر اساس کد فعلی و شواهد CI واقعی.
- **شواهد:** PR #207 ادغام‌شده با merge commit `d644ab5d36d00b1ce3763b64f17fd6fe7ef9fbac`; Vendor CI [40570240409](https://github.com/hamidreza1394/Accounting/actions/runs/40570240409) و .NET CI [40570240205](https://github.com/hamidreza1394/Accounting/actions/runs/40570240205) هر دو موفق؛ PR #208 با commit `9f9b9024eb4581b3e674c155befb57f657dab2b9` ادغام شد.
- **اصلاحات:** وضعیت فعلی PROJECT_STATUS، runbook bootstrap و توصیف evaluator منبع‌محور SECURITY با وضعیت واقعی هماهنگ شدند. از ادعای Production-ready پرهیز شده است؛ اجرای عملیاتی bootstrap، بازبینی نهایی ماتریس RBAC، checkpoint مستقل Audit و KMS/HSM همچنان شکاف‌اند.
- **محدوده:** مستندات Vendor فقط؛ هیچ کد اجرایی، Migration، Schema یا دیتابیس مشتری تغییر نکرد.
- **اعتبارسنجی:** شواهد CI از runهای فوق بررسی شدند؛ تست محلی اجرا نشد چون این تغییر مستنداتی است. CI خود PR باید پیش از ادغام سبز باشد.

## 2026-10-28 — قرارداد checkpoint مستقل زنجیره Audit در Vendor (Issue #107، مرحلهٔ اولیه)
- **هدف:** ایجاد قرارداد canonical/versioned برای checkpoint ممیزی بدون ادعای استقلال مقصد تا انتخاب provider.
- **تغییرات:** `VendorAuditCheckpoint` با SchemaVersion/HashVersion، شمار رخدادها، head hash، timestamp UTC و SHA-256 canonical؛ `IVendorAuditCheckpointStore` با قرارداد append/read-latest؛ `VendorAuditCheckpointService` که قبل از ایجاد checkpoint کل زنجیره را verify می‌کند، checkpoint قبلی را اعتبارسنجی می‌کند و rollback/divergence را رد می‌کند.
- **تست‌ها:** تست‌های واحد canonicalization/version/tampering و تست Integration SQL Server برای advancement، idempotence و tampering اضافه شدند. Fake store فقط در تست وجود دارد؛ هیچ store محلی/DB fallback به برنامه اضافه نشده است.
- **محدوده:** فقط Vendor persistence/tests/docs؛ هیچ Migration، `Template.sln` یا دیتابیس مشتری تغییر نکرد. ستون HashVersion برای هر رخداد، adapter مستقل Production، immutability/retention، SQL append-only permissions و alerting هنوز انجام نشده‌اند؛ Issue #107 باز می‌ماند.
- **اعتبارسنجی:** CI روی HEAD این شاخه هنوز اجرا نشده؛ تست محلی در این محیط در دسترس نیست. تا سبز شدن Vendor CI و .NET CI، وضعیت Implemented / Not Verified است.


## 2026-10-28 — Issue #107: نسخه‌بندی هش و یکتایی زنجیره ممیزی Vendor (در انتظار CI)
- **هدف:** جلوگیری از fork در زنجیره ممیزی، ثبت نسخه الگوریتم هش برای هر رویداد و رد امن نسخه‌های ناشناخته.
- **تغییرات:** `VendorAuditEvent.HashVersion` با backfill پیش‌فرض 1؛ dispatch verifier نسخه 1 و رد fail-closed نسخه ناشناخته؛ Migration مستقل Vendor با preflight برای duplicate parent/root، parent مفقود و رویدادهای جداافتاده؛ تبدیل ایندکس `PreviousHash` به unique و حفظ unique بودن `EventHash`.
- **تست‌ها:** تست واحد نسخه ناشناخته؛ تست SQL Server برای backfill، رد fork/duplicate EventHash و rollback Migration در برابر دو root. اجرای محلی dotnet/SQL Server در این محیط انجام نشده و باید نتیجه CI بررسی شود.
- **محدوده داده:** فقط `VendorDbContext` و Migration مستقل Vendor؛ هیچ تغییر در `Server/**` یا دیتابیس مشتری انجام نشد.
- **مستندات:** handoff در `docs/handoffs/ISSUE-107-vendor-audit-hash-version.md`؛ Issue #107 پس از CI و ادغام باید بر اساس معیارهای باقی‌مانده به‌روزرسانی شود.
- **وضعیت:** شاخه `fix/vendor-audit-hash-version-unique-parent`؛ PR/CI در انتظار.

## 2026-10-28 — تکمیل مرحلهٔ کد Issue #107 و ادغام PR #212
- **نتیجه Git:** PR #212 با commit `4985908d395bb3856aec43d1a22d23555ae7d897` در `main` ادغام شد.
- **Vendor CI:** PR run [40650985169](https://github.com/hamidreza1394/Accounting/actions/runs/40650985169) و post-merge run [40651677915](https://github.com/hamidreza1394/Accounting/actions/runs/40651677915) موفق؛ تست Vendor روی PR: 77 موفق، 0 ناموفق، 0 Skip؛ EF model snapshot guard و انتشار Linux Host موفق.
- **.NET CI روی PR:** [40650985057](https://github.com/hamidreza1394/Accounting/actions/runs/40650985057) موفق؛ Build/Test، migration guards، E2E مرورگر، انتشار Linux و Windows موفق.
- **پیاده‌سازی:** `HashVersion=1`، dispatch verifier، unique `PreviousHash`، preflight رد زنجیرهٔ forked/disconnected و تست‌های SQL Server برای تکرار EventHash، fork و رد Migration روی دو root.
- **محدودیت‌های صریح:** فرمان محلی `dotnet ef migrations add` اجرا نشده؛ فایل Migration/Designer/Snapshot با Vendor CI و `has-pending-model-changes` بررسی شدند. checkpoint مستقل provider واقعی، fail-closed runtime wiring در Production، SQL permission separation، alerting و recovery runbook هنوز ناتمام‌اند. Issue #107 باز می‌ماند.
- **محدوده:** فقط Vendor و تست/مستندات آن؛ هیچ تغییر در `Server/**` یا schema دیتابیس مشتری انجام نشد.


## 2026-10-28 — Issue #214: مرحلهٔ نخست درخواست تأیید صدور/ابطال لایسنس
- **مبنای کار:** PR #216 ادغام شد؛ `main` در شروع این مرحله روی `8e5770f049a875f1c75968d18f7a159691c023b9` بود. شاخهٔ مستقل `feat/vendor-license-approval-workflow` ساخته شد.
- **تغییر:** endpoint درخواست تأیید برای لایسنس با permission `vendor.licenses.issue`، بررسی resource authorization، اعتبارسنجی RowVersion، جلوگیری از درخواست تکراری با index Pending موجود و رخداد ممیزی اضافه شد. endpoint تصمیم Maker-Checker اجازه نمی‌دهد metadata لایسنس به‌تنهایی با تأیید به `Issued`/`Revoked` تبدیل شود؛ تأیید تا آماده‌شدن signer و persistence امن artifact با `503 license_issuance_unavailable` fail-closed است؛ رد درخواست قابل انجام است.
- **تست:** مسیر جدید به تست HTTP برای رد دسترسی ناشناس افزوده شد؛ تست Integration مبتنی بر SQL Server اکنون درخواست صدور، جلوگیری از Pending تکراری، پاسخ 503 هنگام تأیید بدون signer و حفظ وضعیت Draft/Pending را بررسی می‌کند. اجرای CI هنوز در انتظار PR است؛ این تغییر هنوز Verified نیست.
- **امنیت/محدوده:** هیچ کلید خصوصی یا credential اضافه نشد؛ هیچ فایل `Server/**` یا schema دیتابیس مشتری تغییر نکرد. هنوز migration جدیدی برای Vendor ایجاد نشده است.
- **باقی‌مانده:** تست integration برای درخواست صدور/ابطال، تست self-approval/stale version، ذخیرهٔ envelope امضاشده، provider-neutral signer با پیاده‌سازی production مبتنی بر KMS/HSM یا سرویس امضا، ابطال قابل‌اعتبارسنجی، migration/Snapshot افزایشی Vendor، و CI سبز. Issue #214 نباید بسته شود.


## 2026-10-28 — Issue #214: قرارداد provider-neutral امضای لایسنس (در انتظار CI)
- **وضعیت قبلی:** PR #217 با merge commit `a643451fb445ff97bb9069fbd1c5cb8f2984dfe1` ادغام شد؛ Vendor CI run [40677449281](https://github.com/hamidreza1394/Accounting/actions/runs/40677449281) و .NET CI run [40677449226](https://github.com/hamidreza1394/Accounting/actions/runs/40677449226) موفق‌اند. Issue #214 به‌طور خودکار بسته شده بود، با اینکه معیار پذیرش آن تصریح می‌کند تا انتخاب/پیکربندی signer واقعی و آزمون عملیاتی باز بماند؛ بنابراین Issue دوباره باز شد.
- **تغییر:** قرارداد `ILicenseSigningProvider` و نتیجهٔ محدودشدهٔ `LicenseSigningResult` برای امضای بایت‌های دقیق payload canonical افزوده شد. الگوریتم باید از allowlist قرارداد Licensing v1 باشد و امضا باید ۶۴ بایت ECDSA P-256 / IEEE P1363 باشد.
- **تست:** تست-only signer با کلید موقت ECDSA، تأیید امضا با `EcdsaP256Sha256Verifier` و رد payload تغییرکرده اضافه شد؛ همچنین الگوریتم و اندازهٔ امضای نامعتبر رد می‌شوند.
- **امنیت/محدوده:** هیچ provider تولیدی یا fallback نرم‌افزاری، کلید خصوصی، ذخیره‌سازی envelope، Migration یا تغییر `Server/**` اضافه نشد. endpoint صدور همچنان fail-closed است.
- **مستندات:** `docs/PROJECT_STATUS.md`, `Vendor/README.md`, `docs/LICENSING_THREAT_MODEL.md` و handoff اختصاصی به‌روزرسانی شدند.
- **اعتبارسنجی:** تست محلی در این محیط اجرا نشده؛ CI این شاخه باید نتیجهٔ Build/Test، SQL Server integration، EF guard و publish را تأیید کند. تا سبزشدن CI، این slice Verified محسوب نمی‌شود.


## 2026-10-28 — Issue #214: payload builder and envelope factory
- PR #219 adds `VendorLicensePayloadBuilder` to map Vendor metadata into a validated Licensing v1 payload; both Vendor CI and .NET CI passed on head `b8e5e860b8e8f08cffcce404c22d3d3bba71d328` and PR #219 was merged (merge commit `0477c06a0f8a7bcaacf48aa09a0a8e499ad45c11`).
- Next slice adds `VendorLicenseEnvelopeFactory`: canonicalizes payload bytes, calls only the injected signing provider, validates provider output and envelope structure, and returns safe failure codes. Tests use a test-only ephemeral ECDSA key and cover successful verification, provider-unavailable fail-closed behavior, and invalid-payload rejection.
- **Explicit limitations:** factory does not persist the envelope, update license sequence/status, or wire approval decisions to issuance. No production KMS/HSM implementation or private key is added. No customer DB or `Server/**` changes.
- Status: PR opened; must wait for CI and merge only after all required checks pass. Issue #214 remains open.


## 2026-10-28 — ذخیره‌سازی پایدار پاکت امضاشدهٔ لایسنس (شاخهٔ پیگیری پس از PR #220)
- **پیش‌نیاز تأیید شد:** PR #220 با SHA نهایی `2381bce5844b398233f7ef10a5cb85b53e98f547` ادغام شده و Vendor CI [40682375836](https://github.com/hamidreza1394/Accounting/actions/runs/40682375836) و .NET CI [40682375621](https://github.com/hamidreza1394/Accounting/actions/runs/40682375621) موفق‌اند.
- **دامنهٔ این شاخه:** `feat/vendor-signed-envelope-storage`؛ موجودیت `VendorSignedLicenseEnvelope`، DbSet و mapping مستقل Vendor، Migration افزایشی `20261028150000_VendorSignedLicenseEnvelopes`، snapshot/target model EF و تست invariants مدل اضافه شدند.
- **قیود داده:** هر لایسنس در هر sequence فقط یک پاکت دارد (unique index)، sequence باید مثبت باشد، FK به `VendorLicenses` با حذف Restrict است، payload hash در ستون ثابت‌طول ذخیره می‌شود و RowVersion برای concurrency وجود دارد. پاکت JSON کامل ذخیره می‌شود؛ کلید خصوصی یا دیتابیس مشتری به این تغییر وارد نشده است.
- **محدودیت فعلی:** هنوز repository/service تراکنشی برای ثبت پاکت و افزایش `LastIssuedSequence` وجود ندارد؛ endpoint تأیید صدور همچنان fail-closed است. این migration/schema تا اجرای CI و بررسی snapshot، Verified اعلام نمی‌شود.
- **اعتبارسنجی:** در این محیط اجرای محلی .NET/SQL Server انجام نشده؛ PR و CI باید خطاهای کامپایل، model snapshot و migration را بررسی کنند.
## 2026-10-28 — رفع race در E2E ورود و هم‌ترازسازی وضعیت صدور لایسنس
- **Issue #225 / PR #226:** اجرای ناموفق E2E در `tests/e2e/specs/faranou.spec.mjs` نشان داد `page.goto` در navigation بعدی با `net::ERR_ABORTED` شکست می‌خورد؛ شکست از assertion کوکی نبود. اصلاح نهایی helper `signIn()` از `page.waitForNavigation({ waitUntil: 'load' })` همراه با submit استفاده می‌کند تا هم ورود موفق (redirect) و هم ورود ناموفق (render مجدد Login) تا پایان navigation منتظر بمانند. assertions احراز هویت و کوکی دست‌نخورده ماندند.
- **اعتبارسنجی:** .NET CI روی PR head `62fd3e93f64c682a9141ab98d8875f591bcb1468` سبز شد: Browser tests، SQL Server E2E، publish Linux/Windows، build/test، client، publish-scripts و secret scan. runهای [PR CI 40700226645](https://github.com/hamidreza1394/Accounting/actions/runs/40700226645) و [push CI 40700217610](https://github.com/hamidreza1394/Accounting/actions/runs/40700217610).
- **وضعیت لایسنس:** PRهای #221–#224 اکنون در main هستند: ذخیرهٔ envelope و صدور تراکنشی، تست rollback هنگام شکست audit، تعارض sequence و رقابت هم‌زمان. Provider واقعی KMS/HSM و پروتکل ابطال آفلاین همچنان blockers هستند؛ signer پیش‌فرض fail-closed است و Issue #214 باز می‌ماند.
- **محدوده:** فقط آزمون E2E و مستندات Vendor؛ بدون تغییر `Server/**`، schema دیتابیس مشتری یا اسرار.
## 2026-10-28 — Issue #228: versioned customer provisioning contract (PR slice)
- Added CustomerProvisioningPayload and CustomerProvisioningPackage to Licensing.Contracts, with fixed-order canonical JSON and an explicit versioned envelope.
- Added fail-closed validation for the supported schema, customer/installation references, company type, subsystem allow-list, bounded request lifetime, nonce shape, key ID, algorithm, P1363 signature size and ECDSA P-256/SHA-256 signature verification against a caller-resolved trusted public key.
- Added contract tests for valid signatures, modified/non-canonical payloads, expired packages, duplicate/unknown subsystem codes, unknown schema versions and absence of password/secret properties.
- Security boundary: this is a contract/verification slice, not end-to-end provisioning. The consumer must atomically persist/claim nonces to enforce replay protection; Vendor UI/API persistence, customer-side acknowledgement, idempotent provisioning, and one-time credential delivery remain unimplemented. No plaintext password, customer password hash, private key, customer DB schema, or Server/** change was added.
- Validation: local .NET tests could not be executed from this GitHub-only tool session. CI must verify the final PR SHA before merge; do not mark this slice Verified until all required checks pass.
- Handoff: docs/handoffs/ISSUE-228-provisioning-contract-v1.md. Issue #228 remains open.

## 2026-10-28 — Issue #228: provisioning contract CI correction and verification
- The first .NET CI run (40723205619) reported 2 failing new contract tests out of 576. Root cause: the canonical JSON uses camelCase names, while deserialization had no matching JsonNamingPolicy, so valid payloads were parsed with default values and rejected.
- Fixed the deserialization options to use JsonNamingPolicy.CamelCase while retaining case-sensitive property matching; the modified-payload test now reaches signature verification and correctly rejects the altered payload.
- Verification on code SHA 18e684a0c78f675ca35fecdc5b8d2d42c3b10916: .NET CI run 40723732882 passed all 576 tests (0 failed, 0 skipped), plus E2E, Linux/Windows publish, client and secret scan; Vendor CI run 40723732546 passed UI tests/build, Vendor build/test, EF snapshot guard and publish.
- Documentation update is a new commit after the verified code SHA, so the final PR SHA must receive fresh green CI before merge.
- Issue #228 remains open: the contract is not yet wired to Vendor API/UI or a customer-side consumer; atomic nonce claiming/replay protection, idempotent provisioning, status acknowledgement and secure one-time credential delivery remain follow-up work.

## 2026-10-28 — Issue #228: Vendor-side provisioning state and API
- Added Vendor-only VendorProvisioningRequests persistence with a unique request ID and nonce, canonical payload JSON/hash, rowversion, status, and optional signature/delivery/acknowledgement metadata. Migration: 20261028163000_VendorProvisioningRequests. Customer DB and Server/** are unchanged.
- Added a Vendor API that validates company setup and subsystem allow-list, requires antiforgery and server-side customer/installation authorization, checks the persisted installation/customer relationship, writes audit data in the same transaction, and returns the existing request for an identical RequestId/configuration. Conflicting reuse returns 409.
- Requests remain AwaitingSigner. No signed package is issued, no customer administrator/company is claimed created, and no plaintext password/password hash/bootstrap secret is stored. Signing provider, customer-side acknowledgement, UI and full replay-consumer path remain open.
- Added rule tests for valid/invalid configuration and idempotent/conflicting request reuse.
- Migration files were authored in the GitHub-only session rather than generated by local dotnet ef; the PR must pass EF snapshot/pending-model-change guards and SQL Server migration tests before merge.
- Handoff: docs/handoffs/ISSUE-228-vendor-provisioning-state.md.

## 2026-10-28 — Issue #228: Persian RTL provisioning UI
- Added ProvisioningForm with target-installation selection, company name/type, initial administrator username, purchased subsystem selection, first-item defaults, CSRF token handling, RequestId retries, and a status list.
- Connected the customer list action to the form only when customer/install read and manage permissions are present.
- The UI explicitly says that requests are only saved in the Vendor control plane and remain AwaitingSigner; no password is collected and no customer creation is claimed before trusted signing and acknowledgement.
- Added a Vitest/Testing Library regression test for request payload, CSRF header, subsystem defaults, status display and absence of password/passwordHash properties.
- Updated Vendor README and handoff. CI must verify the final PR SHA before merge.

## 2026-10-28 — Issue #228: provisioning HTTP integration regression
- Extended the real SQL Server Vendor mutation integration test with provisioning API coverage: missing antiforgery token, successful creation, idempotent replay, conflicting RequestId reuse, cross-customer installation rejection, single persisted request, single audit event, and absence of password text in persisted payload.
- The request remains AwaitingSigner; no signed package or customer creation is claimed.
- CI must execute the SQL Server integration test on the final PR SHA; local execution was not available in the GitHub-only session.
- Handoff: docs/handoffs/ISSUE-228-provisioning-api-integration.md.

## 2026-10-28 — Issue #236: provisioning maker-checker issuance slice
- Added a Vendor-only issuance service that validates the stored canonical payload/hash, customer/installation binding, target active state, expiry and rowversion before calling the existing fail-closed signing provider.
- Added a two-person approval request endpoint and integrated provisioning decisions with the existing approval transaction. The requester cannot approve their own request; a signer outage returns 503 and leaves the request AwaitingSigner and approval Pending.
- Added an authorized no-store package export route that returns a signed package only after status PendingDelivery.
- Extended the real SQL Server HTTP integration test to cover the approval request and the expected fail-closed response when the default unavailable signer is active.
- No production KMS/HSM adapter was selected or implemented; no customer-side consumer/acknowledgement was implemented. See #235 and #236 and `docs/handoffs/ISSUE-236-provisioning-maker-checker.md`.
- CI must verify the final PR SHA; no local build/test was available in the GitHub-only session.


## 2026-10-28 — Issue #107: fail-closed Production checkpoint readiness (history preservation)
- Preserved the Issue #107 operational requirement that Production startup fails unless an explicit `IProductionVendorAuditCheckpointStore` adapter is registered; a generic or fake store does not satisfy the production gate.
- Vendor cannot start in Production until an independent external checkpoint provider is configured with separate credentials, suitable append-only/immutability retention, SQL principal separation, alerting and recovery. No local-file or same-database fallback is accepted.
- Handoff: docs/handoffs/ISSUE-107-checkpoint-runtime-guard.md.
- Documentation correction during Issue #236 review: the earlier edit had replaced this checkpoint section instead of retaining it. This entry restores the historical requirement; Issue #236 does not supersede it.


## 2026-10-28 — Issue #236: اصلاح خطاهای Vendor CI و تست binding نصب
- رفع خطای TypeScript در `Vendor/Vendor.Web/src/ProvisioningForm.test.tsx`: دو عضو استاتیک `TestURL` باید صریحاً `override` داشته باشند (commit `b1d355add0cc0a54561cc6cd9f8f1324599c4a90`).
- CI بعدی نشان داد آزمون SQL Server پس از ساخت درخواست راه‌اندازی، کد نصب هدف را تغییر می‌داد؛ صدور با `provisioning_payload_invalid` رد شد. رفتار fail-closed درست بود و نباید تضعیف می‌شد.
- اصلاح fixture در `tests/Vendor.Host.Tests/VendorCustomerMutationHttpIntegrationTests.cs`: کد نصب ثابت نگه داشته می‌شود و آزمون update از تغییر نام/محیط برای پوشش RowVersion استفاده می‌کند (commit `d30693f649a50edd2f2a493ab3b05648469174a1`).
- شواهد Vendor CI روی SHA `d30693f649a50edd2f2a493ab3b05648469174a1` سبز: typecheck، تست UI، runtime dependency audit، build UI، build/test Vendor روی SQL Server، EF snapshot guard، publish و گارد نبود کلید خصوصی.
- CI روی SHA نهایی مستندات باید مجدداً تأیید شود؛ .NET CI، E2E و انتشار Linux/Windows تا مشاهدهٔ نتیجهٔ نهایی این commit مستنداتی هنوز Verified محسوب نمی‌شوند.


## 2026-10-28 — Issue #233: جداسازی signer راه‌اندازی مشتری از signer لایسنس
- بازبینی پس از ادغام PR #240 نشان داد سرویس provisioning مستقیماً `ILicenseSigningProvider` را تزریق می‌کرد؛ این با الزام Issue #233 برای کلید provisioning مجزا از کلید لایسنس ناسازگار بود.
- افزودن `IProvisioningSigningProvider` و `UnavailableProvisioningSigningProvider`، ثبت مستقل در DI و تغییر سرویس صدور provisioning به interface جدید. Provider پیش‌فرض همچنان fail-closed است؛ provider واقعی یا کلید نرم‌افزاری اضافه نشده است.
- تست integration اکنون فقط provider provisioning را جایگزین می‌کند و signer لایسنس همچنان unavailable می‌ماند؛ sequence تست با یک شکست موقت و سپس امضای تستی موفق هماهنگ شده است. تست قرارداد برای unavailable/cancellation افزوده شد.
- README و `docs/LICENSING_THREAT_MODEL.md` به‌روزرسانی شدند؛ handoff: `docs/handoffs/ISSUE-233-provisioning-dedicated-signer.md`.
- **CI نهایی این شاخه هنوز اجرا/تأیید نشده است.** Provider تولیدی، trusted key ID/rotation، verify امضا قبل از persistence و runbook عملیاتی همچنان باقی‌اند.


## 2026-10-28 — PR #241 merged and open Vendor blockers reconciled
- PR #241 merged as `9ff7ae3a1cc9a2a2623335740f8adeb2b536417f`; both Vendor CI (`40745034125`) and .NET CI (`40745033978`) were green on the final PR head.
- The provisioning signing interface is now distinct from the license signing interface. Both production signing and independent audit checkpoint adapters remain intentionally unconfigured and fail closed.
- Added status comments to Issues #107, #214, #228, #233 and #236 to distinguish implemented/CI-verified work from deployment-approval blockers. Customer-side consumer/acknowledgement remains in Issues #234/#235 and is outside Vendor-only scope.
- Corrected the Issue #107 handoff so it no longer says the merged startup guard is waiting for CI.


## 2026-10-29 — اعتبارسنجی بومی فرم‌های Vendor و وضعیت onboarding
- **محدوده:** Issue #228؛ اصلاح رگرسیون در فرم درخواست provisioning، فرم مشتری، قرارداد، نصب و metadata لایسنس.
- **یافته:** پنج فرم با `noValidate` اعتبارسنجی constraintهای بومی HTML را دور می‌زدند؛ این ویژگی با `required` و دیگر constraintهای تعریف‌شده سازگار نبود.
- **اصلاحات ادغام‌شده:** PR #244 (provisioning، merge `96ecc4c49475d04fdaeb889e20f9d6b51354bc8e`)، PR #245 (customer، merge `381201b478363d5035e2b9d4dd9016ab497d71d8`) و PR #246 (contract/installation/license، merge `ecce57cbb5b8c0b058a1cea2643a27a870245b32`). برای هر فرم assertion رگرسیون اضافه شد. CIهای مربوطه روی headهای PR شامل تست‌های Vendor، Build/Test، E2E/انتشار و CodeQL موفق شدند.
- **مرز امنیتی:** این اصلاح فقط UX اعتبارسنجی مرورگر است؛ اعتبارسنجی سمت سرور همچنان لازم است. هیچ رمز اولیه یا password hash در Vendor ذخیره نمی‌شود؛ این تغییرها API، دیتابیس مشتری و migration را دست نزدند.
- **مانع بعدی:** provisioning همچنان intent ثبت‌شده در control plane است تا مصرف‌کنندهٔ مشتری بستهٔ امضاشده را تأیید و acknowledgement معتبر برگرداند. signer پیش‌فرض عمداً fail-closed است. Issueهای #228، #235، #236، #214 و #107 را تا ارائهٔ شواهد واقعی امنیتی/عملیاتی نباید بسته اعلام کرد.
- **اعتبارسنجی پس از ادغام:** CI روی `main`@ `ecce57cbb5b8c0b058a1cea2643a27a870245b32` در زمان این ثبت هنوز در حال اجرا بود؛ نتیجهٔ نهایی باید بعداً بررسی شود.


## 2026-10-29 — بازبینی پس از ادغام وابستگی‌های Vendor

- **محدوده:** مستندسازی وضعیت CI و شکاف‌های onboarding/issuance در Issueهای #228، #235، #236، #214 و #107؛ مبنا `main`@ `365b1b89a7370cba91c01de4f2c43219b5bf04ea`.
- **شواهد پس از ادغام:** Vendor CI [40825997070]، .NET CI [40825997059] و Semgrep [40825997067] موفق‌اند؛ تحلیل‌های CodeQL روی commit مبنا نیز موفق‌اند ([40825996663](https://github.com/hamidreza1394/Accounting/actions/runs/40825996663)).
- **تغییرات اخیر وابستگی:** PRهای #268، #269 و #271 در main ادغام شده‌اند. انتشار Windows در .NET CI run 40825997059 هم با موفقیت پایان یافته است.
- **مرز Production:** سبز بودن CI، وجود فرم provisioning یا ذخیرهٔ درخواست، ایجاد واقعی شرکت/مدیر در مشتری را اثبات نمی‌کند. signer عملیاتی KMS/HSM، consumer/ack سمت مشتری و sink مستقل checkpoint همچنان نیازمند پیاده‌سازی/تصمیم استقرار هستند؛ signer فعلی عمداً fail-closed می‌ماند.
- **دامنهٔ تغییر:** فقط مستندات وضعیت و گزارش کار؛ هیچ کد اجرایی، schema، migration یا دیتابیس مشتری تغییر نکرده است.


## 2026-10-29 — Issue #228: جلوگیری از انتخاب نصب غیرفعال در راه‌اندازی مشتری
- **یافته:** فرم provisioning همهٔ نصب‌های برگشتی API را در فهرست نشان می‌داد، در حالی که مسیر صدور بسته فقط نصب فعال را می‌پذیرد؛ انتخاب نصب غیرفعال باعث خطای قابل پیشگیری در ادامهٔ گردش‌کار می‌شد.
- **اصلاح:** فهرست UI به نصب‌های دارای وضعیت `Active` محدود شد؛ انتخاب پیش‌فرض از میان نصب‌های فعال انجام می‌شود. اگر نصب فعالی وجود نداشته باشد، پیام راهنما صریحاً ثبت/فعال‌سازی نصب را درخواست می‌کند.
- **تست رگرسیون:** تست UI نصب غیرفعال در پاسخ API را کنار نصب فعال قرار می‌دهد و بررسی می‌کند نصب غیرفعال قابل انتخاب نیست و اولین نصب فعال پیش‌فرض است.
- **امنیت/داده:** اعتبارسنجی سمت سرور بدون تغییر باقی می‌ماند؛ این اصلاح صرفاً UX و دفاع لایهٔ UI است. بدون تغییر schema، migration، API، `Server/**` یا دیتابیس مشتری.
- **اعتبارسنجی نهایی روی SHA `57d49c145445f13c356f1619571aa571bb8ed223`:** Vendor CI موفق [run 40829071732](https://github.com/hamidreza1394/Accounting/actions/runs/40829071732)، .NET CI با تمام jobها از جمله build-and-test، Linux publish، E2E و Windows publish موفق [run 40829071746](https://github.com/hamidreza1394/Accounting/actions/runs/40829071746)، CodeQL موفق [run 40829067402](https://github.com/hamidreza1394/Accounting/actions/runs/40829067402) و Semgrep موفق [run 40829071675](https://github.com/hamidreza1394/Accounting/actions/runs/40829071675). تست محلی Vitest در این نشست اجرا نشده است؛ آزمون Vendor CI موفق است.
- **ادغام:** PR #273 با روش squash ادغام شد؛ merge commit: `a37b1c32b84d7622dbb7770f38aa852979058ae3`.
- **Handoff:** `docs/handoffs/ISSUE-228-active-installation-filter.md`.


## 2026-10-29 — هم‌ترازسازی وضعیت مدل تهدید Vendor با پیاده‌سازی واقعی
- **یافته:** مقدمهٔ `docs/LICENSING_THREAT_MODEL.md` ادعا می‌کرد هیچ کنترلی پیاده‌سازی نشده است؛ این گزاره با کنترل‌های ادغام‌شدهٔ maker-checker، پاکت امضاشده و تراکنش، مرز signer، audit-chain و guard مربوط به checkpoint تناقض داشت و می‌توانست وضعیت را گمراه‌کننده نشان دهد.
- **اصلاح:** مقدمه اکنون کنترل‌های مرحله‌ای موجود را از موانع Production جدا می‌کند؛ عنوان و متن بخش تاریخی Issue #214 نیز روشن می‌کند که بخشی از موارد بعداً ادغام شده‌اند.
- **محدوده امنیتی:** مستندات فقط؛ هیچ کد اجرایی، تنظیمات signer، کلید، API، schema، migration یا دیتابیس مشتری تغییر نکرد. Production-ready اعلام نشده و موانع KMS/HSM، consumer/ack مشتری، ابطال آفلاین و checkpoint مستقل صریحاً باقی مانده‌اند.
- **اعتبارسنجی:** بررسی متن و diff مستندات در این نشست؛ Build، تست‌ها و CI برای تغییر مستنداتی اجرا نشده‌اند و باید از PR/CI نهایی پیگیری شوند.
- **Branch:** `docs/vendor-threat-model-status-reconciliation`؛ مبنا `df9256bf774e918c6ee540786fcda3490702431f`.


## 2026-10-29 — مشخص‌کردن بخش تاریخی فاز صفر در مدل تهدید
- **یافته:** پس از اصلاح مقدمه در PR #275، عنوان بخش فاز صفر همچنان «Proposed، همگی NotImplemented» می‌گفت و با کنترل‌های بعداً ادغام‌شده تناقض داشت.
- **اصلاح:** عنوان به «سابقهٔ طرح اولیه» تغییر کرد و توضیح اضافه شد که این جدول تاریخی است، نه داشبورد وضعیت جاری؛ برای وضعیت واقعی به شواهد PR/CI و Issueهای باز ارجاع داده می‌شود.
- **محدوده:** مستندات فقط؛ بدون تغییر کد اجرایی، signer، secret، API، schema، migration یا دیتابیس مشتری.
- **اعتبارسنجی:** بررسی محتوای سند و تغییر هدفمند متن؛ CI و تست‌ها پس از ایجاد PR باید بررسی شوند.
- **Branch:** `docs/clarify-threat-model-phase-zero-history`؛ مبنا `4f1ca368c33d5a5692a2ead34c993b5ad6ff2d8d`.


## 2026-10-29 — Issue #228: reject provisioning requests for inactive targets
- Added server-side validation requiring both the Vendor customer and selected installation to have the exact status `Active` before a new provisioning request is persisted.
- Added pure rule tests for active, inactive, suspended, and missing status values. Inactive targets return a generic conflict response; no customer DB access, customer schema change, or credential handling was added.
- Validation limitation: this GitHub-only change was not built or tested locally. Required Vendor CI and integration checks must pass on the PR head before merge.

## 2026-10-29 — سخت‌سازی مجوزدهی provisioning (PR #277–#280)
- **PR #277** `3f5c6be34e17fce8eeca9c40ae9d43eef3355309`: درخواست provisioning فقط برای مشتری و نصب دارای وضعیت دقیق `Active` پذیرفته می‌شود؛ تست‌های وضعیت فعال/غیرفعال/معلق و مقدار تهی اضافه شدند.
- **PR #278** `708eebc3c24d964e57087ab170c0fe20b720dca0`: بررسی وضعیت هدف پس از مجوزدهی منبع منتقل شد تا کاربر غیرمجاز نتواند وضعیت مشتری/نصب را استنباط کند.
- **PR #279** `166289acff15afbec19fc8075476b438270893be`: بررسی وضعیت درخواست و RowVersion در endpoint درخواست تأیید پس از مجوزدهی مشتری و نصب انجام می‌شود.
- **PR #280** `4c247bfab8c10a1fce75ecb06db138a1b522763b`, merge commit `17038ae747aaf991aeea072c57c62c07f957586a`: چهار شاخهٔ شکست مجوزدهی منبع در endpointهای provisioning به `404 Not Found` تغییر کرد تا افشای وجود منابع محدود شود.
- **شواهد CI برای PR #280:** Vendor CI [40839835705]، .NET CI [40839835703]، Semgrep [40839835699] و workflow اختصاصی PR [40839832564] همگی موفق.
- **پس از merge روی main:** Vendor CI [40840817584] و Semgrep [40840817594] موفق‌اند. .NET CI [40840817609] و CodeQL [40840817734] هنوز در حال اجرا بودند هنگام ثبت این گزارش.
- **محدودیت اعتبارسنجی:** تست محلی/ساخت محلی در این نشست اجرا نشده است؛ وضعیت‌ها فقط بر اساس نتایج واقعی GitHub Actions گزارش شده‌اند.
- **باقی‌مانده:** #228 جریان کامل provisioning، #235 مصرف‌کننده و acknowledgement سمت مشتری، #236 signer واقعی KMS/HSM و #107 sink مستقل checkpoint ممیزی هنوز شکاف‌های Production هستند؛ انتخاب provider و اعتبارنامه‌های عملیاتی را نباید جعل کرد.



## 2026-10-29 — جلوگیری از تحویل بستهٔ Provisioning پس از تعلیق هدف (#228)
- **یافته:** وضعیت مشتری و نصب هنگام ایجاد درخواست و امضا کنترل می‌شد، اما ممکن بود پس از امضای بسته و پیش از نخستین export، مشتری یا نصب تعلیق شود و endpoint همچنان بسته را تحویل دهد.
- **اصلاح پیشنهادی در شاخه:** export اکنون داخل تراکنش Serializable وضعیت فعال مشتری و نصب مرتبط را دوباره بررسی می‌کند؛ هدف حذف‌شده/ناسازگار 404 و هدف غیرفعال 409 می‌گیرد. قفل‌های تراکنش از تغییر هم‌زمان وضعیت هدف تا ثبت تحویل/ممیزی جلوگیری می‌کنند.
- **تست افزوده‌شده:** تست Integration موجود ابتدا درخواست را با signer آزمایشی امضا می‌کند، سپس تعلیق مشتری و نصب را از مسیر approval اعمال می‌کند و انتظار دارد export رد شود؛ پس از فعال‌سازی آزمایشی، export موفق و امضای بسته بررسی می‌شود و وضعیت نهایی تعلیق حفظ می‌شود.
- **دامنه داده:** فقط Vendor DB آزمایشی؛ هیچ Server/** یا schema/migration مشتری تغییر نمی‌کند.
- **PR:** #283 (`fix/228-provisioning-export-active-target`، پیاده‌سازی اولیه `fd390cb4af7e6af519305277cd0a73f45a2e6d2e`).
- **اعتبارسنجی:** در این محیط اجرای محلی Build/Test و SQL Server انجام نشده؛ نتیجه باید از GitHub Actions روی PR و سپس main تأیید شود. تا سبزشدن CI، این تغییر Implemented / Not Verified است.


## 2026-10-29 — همگام‌سازی وضعیت Vendor پس از PR #282 و #284
- **مبنای کد:** `main` در `81ed5f64ed86ebbae47a433cea63e3655cb1d24b`.
- **تغییرات ثبت‌شده:** PR #282 اعتبارسنجی قرارداد payload پیش از امضا را اضافه کرد؛ PR #284 ترتیب مجوزدهی در تصمیم تأیید را اصلاح کرد و تست رگرسیون عدم افشای وضعیت منبع غیرقابل‌دسترسی را افزود. هر دو merge شده‌اند.
- **اعتبارسنجی پس از merge:** Vendor CI [40845416309](https://github.com/hamidreza1394/Accounting/actions/runs/40845416309)، .NET CI [40845416352](https://github.com/hamidreza1394/Accounting/actions/runs/40845416352)، Semgrep [40845416374](https://github.com/hamidreza1394/Accounting/actions/runs/40845416374) و Push on main [40845416241](https://github.com/hamidreza1394/Accounting/actions/runs/40845416241) همگی موفق‌اند.
- **شکاف‌های باز:** provisioning واقعی همچنان به consumer و acknowledgement سمت مشتری (#235)، provider امضای Production/KMS-HSM (#236/#214)، و sink مستقل ممیزی (#107) وابسته است. این موارد بدون زیرساخت واقعی تکمیل‌شده اعلام نمی‌شوند.
- **دامنه:** فقط مستندات وضعیت Vendor؛ هیچ کد اجرایی، schema یا دیتابیس مشتری تغییر نکرده است. Build/Test محلی در این نشست اجرا نشده؛ شواهد آزمون از GitHub Actions است.

## 2026-10-29 — تأیید وضعیت PR #283
- بررسی فهرست PRهای بسته‌شدهٔ GitHub تأیید کرد PR #283 با عنوان «block export for inactive provisioning targets» در ساعت 08:45:55Z ادغام شده و merge commit آن `95a098895056a1fe34348fac93091bf2f82780b4` است.
- مستندات وضعیت جاری اصلاح شد تا PR #283 را به‌درستی ادغام‌شده ثبت کند؛ هیچ ادعایی دربارهٔ provisioning end-to-end یا تأیید مشتری افزوده نشد.


## 2026-10-29 — تأیید امضای provisioning پیش از parse (#228)
- **یافته:** `CustomerProvisioningPackageValidator.TryValidateAndVerify` پیش از بررسی امضا، JSON ورودی را deserialize و اعتبارسنجی می‌کرد. اندازه محدود بود و state اعمال نمی‌شد، اما این ترتیب با الزام امنیتی «verify exact bytes before parsing» در Issue #235 هم‌راستا نبود.
- **اصلاح در شاخه:** ابتدا envelope و اندازه بررسی می‌شود، سپس امضای ECDSA P-256/SHA-256 روی بایت‌های دقیق UTF-8 تأیید می‌شود؛ فقط پس از موفقیت امضا JSON parse و schema/canonicalization بررسی می‌شود.
- **تست رگرسیون:** payload غیر JSON با امضای مربوط به payload دیگری باید `provisioning_signature_invalid` بدهد، نه خطای parser.
- **دامنه:** فقط قرارداد مشترک و تست؛ هیچ تغییر در Server/**، دیتابیس مشتری یا migration انجام نشد.
- **اعتبارسنجی:** تست محلی در این نشست اجرا نشده است؛ CI روی PR برای تأیید لازم است. Merge پیش از موفقیت بررسی‌های الزامی مجاز نیست.
- **باقی‌مانده:** Issue #235 مصرف‌کننده و claim اتمی nonce؛ Issue #236 signer عملیاتی KMS/HSM و کلیدهای مورد اعتماد؛ #107 sink مستقل checkpoint ممیزی.

## 2026-10-29 — Consolidated Vendor remaining-work branch

- **Scope:** Issues #228, #235, #236, #107 and #214 on `feat/vendor-remaining-work`, based on `main` at `50d6977d7452b4ee2879c2379140ebbce3e455b9`.
- **Implemented in branch:** confirmed acknowledgement trust-store registration is already on `main` via PR #291 and aligned this branch with the updated `main`; added/updated the deployment handoff; added a provider-neutral signed offline revocation-list contract and negative/rollback tests.
- **Safety boundaries:** no private key; no production KMS/HSM adapter; no customer DB schema or `Server/**` change; no acknowledgement intake endpoint or false `Confirmed` state; no fake independent audit storage.
- **Blockers recorded on Issues:** #236 requires an approved signing provider; #107 requires an independently permissioned checkpoint destination and deployment credentials; #235 customer-side consumer remains a separate reviewed change; #214 customer-side sequence persistence/enforcement remains separate.
- **Tests:** not run locally in this tool session. GitHub Actions must verify the final branch SHA before merge readiness can be claimed.
- **PR:** [#292](https://github.com/hamidreza1394/Accounting/pull/292) is open for the consolidated branch. `main` advanced through merged PR #291 while this work was in progress; the branch now includes `main` as a merge parent and does not overwrite its trust-store registration. CI must pass on the final head SHA.


## 2026-10-29 — Vendor acknowledgement intake (PR #293, merged)

- **Issue / PR:** [#228](https://github.com/hamidreza1394/Accounting/issues/228), [#235](https://github.com/hamidreza1394/Accounting/issues/235), [PR #293](https://github.com/hamidreza1394/Accounting/pull/293).
- **Merged commit:** `2884c61ade381670f754a1b8027c496a6e8eb7b2`.
- **Scope:** Vendor control-plane intake only. No changes to `Server/**` or customer database schema.
- **Implementation:** Added a request-ID-scoped acknowledgement endpoint. It resolves public keys from deployment configuration, verifies the signed canonical acknowledgement and binds request ID, nonce, payload hash, customer reference and installation identity before recording the outcome.
- **State integrity:** Verified `Applied`/`AlreadyApplied` can move a delivered request to `Confirmed`; verified `Rejected` moves it to `Failed`. Exact signed retry is idempotent and audited; a different acknowledgement conflicts. State and audit share a transaction.
- **Persistence:** Added Vendor-only acknowledgement digest/key/result/time columns and migration `20261029130000_VendorProvisioningAcknowledgements`; updated EF mapping, migration target model and snapshot.
- **Tests:** outcome mapping unit tests, unknown-key host test, and SQL Server integration test for confirmation, exact retry, conflicting retry and audit persistence.
- **Verified CI:** Vendor CI [run 40869153676](https://github.com/hamidreza1394/Accounting/actions/runs/40869153676), .NET CI [run 40869153840](https://github.com/hamidreza1394/Accounting/actions/runs/40869153840), and Semgrep [run 40869153686](https://github.com/hamidreza1394/Accounting/actions/runs/40869153686) all succeeded on the merged head. Local .NET commands were not run in this tool session.
- **Remaining:** customer-side provisioning consumer and atomic nonce claim remain separate under #235; D-044/OD-14 conflicts with the later signed-package requirement and is documented on #235. Production signer/KMS remains blocked under #236; independent audit checkpoint destination remains blocked under #107; customer-side offline revocation persistence/enforcement remains separate under #214.

## 2026-10-29 — Vendor acknowledgement status UI (PR #294, merged)

- **Issue / PR:** [#228](https://github.com/hamidreza1394/Accounting/issues/228), [PR #294](https://github.com/hamidreza1394/Accounting/pull/294).
- **Merged commit:** `07dc0a4f1f784dfd2800fb8da12a70f05b67a862`.
- **Scope:** Vendor API response and React request-history UI only; no customer DB changes or secrets.
- **Implementation:** Exposed the persisted acknowledgement result/time in the Vendor provisioning response and displayed them in the request history, distinguishing `Applied`, `AlreadyApplied`, and `Rejected` from the broader provisioning status.
- **Tests:** Added a UI regression test asserting the verified customer outcome and acknowledgement timestamp render.
- **Verified PR CI on implementation head `1a9f8c3f107f713940a4d95c92f6abe597f85f80`:** Vendor CI [40871773941](https://github.com/hamidreza1394/Accounting/actions/runs/40871773941), .NET CI [40871773973](https://github.com/hamidreza1394/Accounting/actions/runs/40871773973), Semgrep [40871774197](https://github.com/hamidreza1394/Accounting/actions/runs/40871774197), and CodeQL [40871768967](https://github.com/hamidreza1394/Accounting/actions/runs/40871768967) passed. Post-merge main CI is tracked separately; no local .NET build was run in this session.
- **Measurements:** Bundle-size before/after and browser LCP/CLS were not measured; no new dependency or external runtime asset was added.
- **Remaining:** UI display does not implement the customer-side consumer, one-time admin enrollment, fiscal-period provisioning inputs or parent-company relationship. These are tracked under #228/#235 and must not be represented as complete.


## 2026-10-29 — Issue #235: provisioning payload schema v2 (contract slice)
- **دامنه:** رفع شکاف قراردادی دورهٔ مالی و رابطهٔ شرکت مادر پیش از پیاده‌سازی consumer سمت مشتری.
- **تغییر:** payload schema v2 فیلدهای عنوان/تاریخ شروع/پایان دورهٔ مالی و ParentCompanyReference را اضافه می‌کند؛ v1 canonical JSON قبلی را بدون فیلدهای جدید حفظ می‌کند.
- **اعتبارسنجی:** v1 و v2 پشتیبانی می‌شوند؛ v2 به بازهٔ معتبر دورهٔ مالی نیاز دارد و فقط شرکت subsidiary می‌تواند/باید مرجع مادر داشته باشد.
- **تست‌ها:** تست امضای v2، حفظ canonical v1، دورهٔ ناقص، تاریخ وارونه و نبود مرجع مادر برای subsidiary افزوده شد.
- **امنیت/داده:** هیچ رمز، password hash، secret، private key، Connection String، Migration یا تغییر Server/** اضافه نشده است. Consumer، replay protection و enrollment مدیر اولیه هنوز پیاده‌سازی نشده‌اند.
- **آزمون واقعی:** dotnet test و git diff --check در این نشست اجرا نشدند؛ checkout محلی متصل به مخزن/SDK در دسترس نبود. نتیجهٔ CI باید قبل از Merge بررسی شود.
- **Branch:** feat/provisioning-payload-v2. PR بعد از ثبت این گزارش ساخته می‌شود.


## 2026-10-29 — Vendor provisioning payload v2 onboarding integration (Issue #228)

- **Baseline:** `main` / `feat/vendor-remaining-work` were aligned by a fast-forward to `341e19a8cb93953dd9be6e9257f9ad504fcf96f6`; no force update or history rewrite.
- **Vendor changes:** the onboarding API now creates schema-v2 payloads with fiscal-period title/start/end and parent-company reference; server validation rejects missing/invalid period ranges and invalid parent-reference rules. Existing request-ID idempotency compares the new fields.
- **UI:** the Persian RTL provisioning form captures the initial fiscal period, conditionally requires the parent reference for subsidiaries, sends no password or password hash, and retains truthful pending/confirmed labels.
- **Tests added/updated:** pure validation tests, idempotency coverage, SQL Server HTTP integration request fixtures and UI tests for schema-v2 metadata and subsidiary parent reference.
- **Architecture:** D-051 records the owner-directed signed-package flow for #228/#235; OD-18 now references that decision. This does not claim that the customer-side consumer exists.
- **Safety:** no `Server/**`, customer DB schema, customer migration, private key, KMS/HSM adapter, or fake audit sink was added in this Vendor-only slice. Production signing remains fail-closed (#236); independent audit destination remains blocked (#107).
- **Verification:** local .NET/npm tests and `git diff --check` were not executable through the GitHub-only session. CI must run on the PR head; no claim of passing tests is made before observing those checks.
- **Remaining:** customer-side consumer, unique nonce/request claim, transaction/recovery, one-time enrollment, signed acknowledgement generation, and offline revocation enforcement remain separate customer-side work under #235/#214.

## 2026-10-29 — Issue #236: bounded wait for provisioning signer

- Added provider-neutral timeout handling around the dedicated `IProvisioningSigningProvider` boundary. A timeout cancels the provider token and returns the generic unavailable result; caller cancellation is preserved.
- Integrated a 15-second bound in `VendorProvisioningIssuanceService`. The service's existing fail-closed transaction behavior remains: no signed package/status/audit success is persisted when the signer is unavailable.
- Added tests for timeout, provider cancellation, caller cancellation, and invalid timeout values.
- No KMS/HSM provider, private key, software-key fallback, customer DB change, or independent audit sink was added.
- Local tests were not run in this GitHub-only session; CI must validate the PR head.

## 2026-10-29 — Issue #107: provider-neutral checkpoint recovery runbook
- Added `docs/runbooks/vendor-audit-checkpoint-recovery.md` with pre-deployment verification, SQL backup plus independent checkpoint preservation, restore reconciliation, sink outage handling, identity separation and Production exit criteria.
- The runbook explicitly forbids local-file/Vendor-DB fallback and never lowers an external checkpoint to match an older restored database.
- No provider-specific adapter, secret, credential, migration or fake Production sink was added. #107 remains blocked on provider selection and operational proof.
- Documentation-only change; no local tests were run in this GitHub-only session.

## 2026-10-29 — Issue #235: customer-side trust verifier and claim schema (WIP)

- Added a customer-side trust store that resolves package KeyId only from independently configured public keys; malformed, oversized, non-P-256 and unknown keys fail closed.
- Added a verifier wrapper that validates the signed canonical package and compares the signed customer/installation references against local deployment settings before any database mutation.
- Added tests for valid signature, unknown/malformed key, customer mismatch, installation mismatch, expiry, payload tampering and malformed local identity settings.
- Added ProvisioningRequestClaims with unique RequestId, nonce digest and enrollment-token digest indexes, rowversion, a composite fiscal-period/company FK and a restricted optional parent-company FK.
- Generated migration 20261029161402_AddCustomerProvisioningClaims and model snapshot using the repository migration verifier. Generation workflow run 40909292917 passed and committed the files to this feature branch.
- PR #302 remains draft. The current changes are not end-to-end provisioning and must not be merged yet.
- Still missing: atomic claim + actual company/fiscal-period/role/membership application in one transaction, enrollment flow with forced password change, signed customer acknowledgement, parent-company external-reference mapping, SQL Server replay/concurrency/rollback tests, and final .NET CI on the current HEAD.
- No plaintext password, password hash, private key, customer DB connection, or signing fallback was added.


## 2026-10-29 — Issue #235: اصلاح RowVersion در مدل EF و Snapshot

- **علت:** CI در run `40913297367` در مرحله Test شکست خورد؛ مدل runtime ویژگی `ProvisioningRequestClaim.RowVersion` را با `.IsRowVersion()` تنظیم می‌کرد اما ابزار مدل سفارشی `byte[]` و metadata rowversion را در Snapshot ثبت نمی‌کرد، که باعث `PendingModelChangesWarning` می‌شد.
- **اصلاح:** استخراج‌کننده مدل، parser Snapshot و مولد Snapshot برای `byte[]` و `rowversion` اصلاح شدند؛ Snapshot و Designer مربوط به Migration موجود به‌روزرسانی شدند. Migration اجرایی و دیتابیس مشتری تغییر نکردند.
- **تست بازگشت:** تست `ProvisioningClaims_RowVersionIsConfiguredAsSqlServerConcurrencyToken` اضافه شد تا CLR type، column type، concurrency token و `ValueGenerated.OnAddOrUpdate` را کنترل کند.
- **اعتبارسنجی:** CI قبلی ناموفق است. در این نشست اجرای محلی .NET و اسکریپت‌های Python ممکن نشد چون شبکه نتوانست `github.com` را resolve کند؛ CI سبز روی آخرین HEAD هنوز تأیید نشده و باید پیش از ادغام بررسی شود.
- **امنیت:** هشدار EF غیرفعال نشد، تستی حذف نشد و هیچ migration روی دیتابیس مشتری اجرا نشد.
- **باقی‌مانده:** نتیجه CI روی آخرین HEAD؛ سپس تکمیل consumer تراکنشی، replay/idempotency، enrollment مدیر اولیه، acknowledgement امضاشده و نگاشت شرکت مادر. PR #302 همچنان Draft است و نباید merge شود.
- **Branch:** `feat/customer-provisioning-consumer`; **PR:** https://github.com/hamidreza1394/Accounting/pull/302.


## 2026-10-29 — رفع شکست گارد Migration در PR #302
- **دامنه:** CI run [40917960102](https://github.com/hamidreza1394/Accounting/actions/runs/40917960102)، برای شاخه `feat/customer-provisioning-consumer`.
- **علت‌های ریشه‌ای:** ۶۱۳ تست .NET و گام بررسی Migrationهای تولیدشده موفق بودند؛ شکست در تست‌های گارد Migration بود. اول، `verify_migration_model.py` نوع CLR آرایه‌ای `byte[]` را در عبارت `table.Column<byte[]>` نمی‌پذیرفت، پس ستون معتبر SQL Server `rowversion` در Migration شناسایی نمی‌شد. دوم، تست جهشی «مهاجرت جدیدتر از ثابت‌ها» از تاریخ `2026-10-24` استفاده می‌کرد که از Migration فعلی `20261029161402` قدیمی‌تر است؛ بنابراین تغییر آزمایشی جدیدتر نبود و تست خطا را تشخیص نمی‌داد.
- **اصلاح:** الگوی parser برای انواع CLR شامل `[]` گسترش یافت؛ تست رگرسیون parse ستون `byte[]/rowversion` اضافه شد؛ تاریخ Migration ساختگی تست جهشی به `2026-10-30` تغییر کرد تا از Migration جاری جدیدتر باشد.
- **اعتبارسنجی CI قبل از اصلاح:** `dotnet build` موفق، `dotnet test` موفق با ۶۱۳/۶۱۳، `generate_migrations.py --check` موفق؛ دو تست گارد شکست خوردند (ستون RowVersion شناسایی نمی‌شد و تست migration جدیدتر fixture منسوخ داشت). تست‌ها بعد از اصلاح باید در اجرای CI جدید تأیید شوند.
- **امنیت/داده:** گاردها خاموش یا ضعیف نشده‌اند؛ Migration اجرایی و دیتابیس مشتری تغییر نکرده‌اند.
- **Git:** شاخه `feat/customer-provisioning-consumer`؛ PR موجود [#302](https://github.com/hamidreza1394/Accounting/pull/302)، هنوز Draft است و نباید Merge شود تا مصرف‌کننده تراکنشی provisioning و آزمون‌های نهایی کامل شوند.


## 2026-10-29 — Issue #235: provisioning payload schema v3 for administrator identity
- **Motivation:** The customer-side `Users.EmailAddress` field is required, while the existing signed provisioning contract carried only `InitialAdminUserName`. Inventing a placeholder email during customer DB provisioning would create a false identity and could collide with login/account recovery semantics.
- **Change:** Added schema v3 with `InitialAdminEmail`, canonicalized only for schema v3. Existing v1/v2 canonical JSON remains unchanged so previously issued signatures are not invalidated.
- **Validation:** Schema v3 requires a bounded syntactically valid email; v1/v2 reject the new field and remain compatible. Vendor-side request validation, idempotency matching, payload issuance, RTL form and UI tests now carry the same email value. The email is metadata, not a credential; no password or token is included in the package.
- **Tests added:** Contract validation for valid/missing/malformed v3 email and v2 compatibility; Vendor validation tests for missing/malformed email; UI submission test checks the email field.
- **Verification:** New CI is required for the latest branch head; local tests were not run in this GitHub-only session.
- **Remaining:** Implement the transactional customer consumer, one-time enrollment delivery and forced password-change enforcement, parent-reference mapping, signed customer acknowledgement generation, replay/concurrency/rollback SQL Server tests, and full E2E. Production customer acknowledgement signing remains fail-closed until a separate trusted signer is configured.
- **Branch/PR:** `feat/provisioning-schema-v3-admin-email-verified`, [PR #304](https://github.com/hamidreza1394/Accounting/pull/304) (Draft). CI must validate the latest head; do not merge until the remaining consumer path is implemented and validated.


## 2026-10-29 — PR #304 CI regression follow-up
- Vendor CI exposed an existing provisioning HTTP integration fixture that omitted `initialAdminEmail`; the request now supplies a valid test email in all three related scenarios.
- .NET CI exposed the customer verifier fixture still generating schema v3 without `InitialAdminEmail`; the fixture now includes `admin@example.com`. The failures were caused by stale test payloads after the schema version bump, not by weakening signature or identity validation.
- The initial CI attempt was on the prior head and failed in those tests. Fresh CI is required on the updated head.


## 2026-10-29 — Customer provisioning: fixed subsystem permission ownership map
- **تصمیم:** پس از تأیید مالک محصول، مسیر نگاشت مجوزها به‌صورت کاتالوگ ثابت در کد انتخاب شد؛ Migration جدید برای افزودن `ModuleCode` در این slice ساخته نمی‌شود.
- **تغییر:** `CustomerProvisioningSubsystemCatalog` در Server فهرست مجاز زیرسیستم‌ها را محدود به finance/sales/inventory/treasury می‌کند و فقط مجوزهای صریح با پیشوند زیرسیستم خریداری‌شده را از فهرست موجود می‌پذیرد. هیچ مجوزی از ورودی package ساخته نمی‌شود؛ wildcard، prefix مبهم، segment خالی و کد ناشناخته رد می‌شوند.
- **تست‌های افزوده‌شده:** `CustomerProvisioningSubsystemCatalogTests` برای allowlist، تطبیق دقیق زیرسیستم، کدهای نامعتبر، حذف تکرار و رفتار خالی fail-closed.
- **محدودیت فعلی:** این تغییر فقط قاعدهٔ ownership را تثبیت می‌کند؛ transactional apply، ثبت مجوزها در role، enrollment یک‌بارمصرف، اجبار تغییر رمز و enforcement مجوزهای role در APIها هنوز تکمیل نشده‌اند. چون repository audit می‌گوید مجوزهای شرکتی در همه APIهای موجود enforce نمی‌شوند، نباید از این تغییر به‌عنوان مجوزدهی Production نتیجه‌گیری کرد.
- **اعتبارسنجی:** کد و تست از طریق GitHub Contents API در شاخه `feat/customer-provisioning-transactional-apply` ثبت شده‌اند؛ اجرای محلی .NET در این محیط انجام نشده است. CI روی PR باید compile/test را تأیید کند.


## 2026-10-29 — Customer provisioning: one-time enrollment token primitive
- **هدف:** ایجاد پایهٔ مستقل برای enrollment امن مدیر اولیه بدون قرار دادن رمز، hash رمز یا secret قابل‌استفادهٔ مجدد در package.
- **تغییر:** `CustomerProvisioningEnrollmentToken` توکن ۲۵۶ بیتی تصادفی با نمایش Base64URL بدون padding تولید می‌کند؛ فقط SHA-256 hex برای ذخیره‌سازی در DB مناسب است. بررسی token از مقایسهٔ constant-time استفاده می‌کند و ورودی‌های malformed را رد می‌کند.
- **تست‌ها:** قالب canonical و طول ثابت، تولید مجدد غیر یکسان، تطبیق/عدم تطبیق digest، رد token/hash نامعتبر، رد encoding غیرcanonical و رد token بدقالب در زمان hash. تست encoding غیرcanonical deterministic است و به تصادف وابسته نیست.
- **محدودیت مهم:** این commit فقط primitive رمزنگاری و تست آن است؛ endpoint مصرف یک‌باره، transaction/RowVersion، انقضا، audit، محدودسازی تلاش و گردش امن تحویل token هنوز پیاده‌سازی نشده‌اند. این primitive نباید به‌تنهایی به‌عنوان enrollment end-to-end یا Verified اعلام شود.
- **اعتبارسنجی:** باید CI روی همین شاخه اجرا شود؛ اجرای محلی .NET در این محیط انجام نشده است.


## 2026-10-30 — D-053: تصمیم‌های قطعی Issue #235
- مالک محصول صریحاً تأیید کرد: ParentCompanyReference همان RequestId بستهٔ مادر با GUID canonical D است؛ resolve فقط از claim به CompanyId محلی؛ اعتبارسنجی Vendor و Customer؛ والدهای مجاز parent/subsidiary؛ فقط ParentCompanyId روی claim و عدم ایجاد روابط گروه/مالکیت؛ شرکت‌های قدیمی خارج از دامنه و بدون Company migration.
- Enrollment: token فقط در پاسخ همان apply پس از commit، hash-only در DB، no-store و بدون log/audit/ack/URL/cache؛ replay فقط acknowledgement؛ reissue مستقل با ابطال token قبلی، audit و مجوز صریح؛ expiry کوتاه، مصرف اتمیک، محدودیت تلاش و اجبار تغییر رمز. در ابر مشترک اپراتور فقط کارکنان میزبان است و هیچ دسترسی ماندگار پس از bootstrap ندارد.
- Production signing فقط provider واقعی Issue #236؛ fail-closed برقرار می‌ماند.
- پیشنهاد نوع والد پذیرفته‌شده در D-053: parent یا subsidiary می‌تواند والد مستقیم باشد؛ independent مجاز نیست تا ساختار چندسطحی پشتیبانی شود.
- وضعیت این ثبت: تصمیم/تهدیدمُدل به‌روزرسانی می‌شود؛ این ثبت به معنی تکمیل کد یا تست نیست.


## 2026-10-30 — Issue #235: enforce Vendor-issued parent references
- **تغییرات:** ParentCompanyReference در قرارداد مشترک و قواعد Vendor فقط GUID canonical با قالب D می‌پذیرد؛ مقایسهٔ GUID case-insensitive است و مقادیر آزاد مانند parent-001 رد می‌شوند.
- **Customer:** resolver جدید فقط از RequestId به ProvisioningRequestClaim و سپس CompanyId می‌رسد؛ claim باید AppliedAtUtc داشته باشد، CustomerReference و InstallationId دقیقاً منطبق باشند، شرکت فعال باشد و CustomerBoundaryId با تنظیمات نصب برابر باشد. تنظیم CustomerBoundaryId خالی در verifier نیز fail-closed رد می‌شود.
- **Vendor:** انتخاب‌گر والد فقط درخواست‌های Confirmed با acknowledgement معتبر Applied/AlreadyApplied در همان Customer و Installation را نشان می‌دهد؛ create و issuance هر دو eligibility را server-side بررسی می‌کنند. این بررسی هیچ اتصال مستقیم به دیتابیس مشتری ندارد.
- **محدودیت:** این slice هنوز apply service را صدا نمی‌زند؛ transactional company/admin/membership/entitlement creation، signed customer acknowledgement، one-time token response/reissue و force-password flow باقی‌اند.
- **تست‌های اضافه‌شده:** unit tests برای GUID canonical و مقایسهٔ حروف؛ SQL Server integration test برای resolver و مرز مشتری. تست‌ها فقط به مخزن افزوده شده‌اند؛ در این محیط اجرا نشده‌اند.
- **اعتبارسنجی:** build، unit tests، SQL Server integration، rollback، concurrent replay و E2E اجرا نشده‌اند. checkout محلی به علت خطای DNS برای github.com در دسترس نبود؛ CI روی PR نهایی لازم است.
- **Git:** branch feat/issue-235-transactional-consumer؛ PR هنوز ساخته نشده؛ هیچ merge انجام نشده است.


## 2026-10-30 — Issue #235: transactional apply and one-time enrollment slice
- **پیاده‌سازی:** CustomerProvisioningApplyService با transaction در سطح Serializable؛ verify بسته قبل از mutation؛ claim یکتا برای RequestId/nonce؛ ایجاد Company، FiscalPeriod، User، CompanyRole، Membership و permissionهای دقیق موجود در D-052؛ ParentCompanyId فقط در claim؛ بدون ساخت گروه یا OwnershipRelationship.
- **Idempotency:** acknowledgement امضاشده در SecurityAuditEvents به‌عنوان outbox/audit در همان transaction ذخیره می‌شود؛ replay با همان request/hash/nonce همان envelope را برمی‌گرداند و token را null می‌کند. برای rejection معتبر نیز envelope ثابت ذخیره و replay می‌شود.
- **Enrollment:** token تصادفی ۲۵۶بیتی، فقط hash در claim؛ انقضای ۳۰ دقیقه؛ token فقط پس از commit در response apply؛ reissue مستقل با reasonCode مجاز، ابطال digest قبلی و audit بدون token؛ مصرف تراکنشی؛ ۵ تلاش ناموفق در ۱۵ دقیقه؛ no-store در endpointها. UI اپراتور در /provisioning/import و فرم مدیر اولیه در /enroll اضافه شدند.
- **محدودیت Production:** ICustomerProvisioningAcknowledgementSigningProvider به‌صورت interface تعریف شده و provider پیش‌فرض unavailable است؛ بنابراین تا اتصال provider امن و مورد تأیید، apply با 503 fail-closed می‌ماند. هیچ کلید خصوصی یا fallback نرم‌افزاری اضافه نشده است.
- **وضعیت تاریخی force-password (2026-10-30):** در زمان ثبت این یادداشت، محدودسازی نشست و اجبار تغییر مجدد رمز در اولین login به‌عنوان معیار باز گزارش شده بود. مالک محصول در 2026-10-30 جریان فعلی Enrollment را صریحاً پذیرفت: مدیر رمز واقعی را در Enrollment تعیین می‌کند و تا پیش از آن login/session کاری مسدود است؛ `MustChangePassword=false` پس از Enrollment عمدی است. این معیار دیگر باز نیست؛ مرجع نهایی D-053 در `docs/DECISIONS.md`.
- **تست:** تست SQL Server برای apply، signed acknowledgement، replay، reissue، ابطال token قبلی، مصرف یک‌بار، hash-only و عدم ایجاد گروه اضافه شده است. این تست‌ها در این نشست اجرا نشده‌اند؛ rollback و concurrent replay نیز اجرا نشده‌اند.
- **CI:** اجرای اول PR #307 شکست کامپایل داشت: namespace مربوط به VendorProvisioningParentReferenceRules در IssuanceService وارد نشده بود. using اصلاح شد. اجرای جدید روی head جاری در زمان ثبت این log: secret-scan موفق؛ build-and-test، client، publish-scripts و Semgrep هنوز queued/in_progress. اجرای نهایی هنوز سبز نیست.

## 2026-10-30 — Issue #235: offline verification of customer acknowledgement signatures
- **شکاف:** نتیجهٔ Provider امضا فقط از نظر شکل بررسی و همراه شرکت ساخته‌شده ذخیره می‌شد؛ امضای معیوب ack غیرقابل‌تأیید دائمی می‌ساخت.
- **تغییر:** Trust Store مستقل `Provisioning:TrustedAcknowledgementKeys` و راستی‌آزمایی آفلاین پیش از ذخیره؛ کدهای `provisioning_acknowledgement_signer_unavailable|signer_key_untrusted|signature_invalid` (503)؛ Exception ارائه‌دهنده گرفته و فقط نوع آن لاگ می‌شود؛ مستندات قرارداد Provider در SECURITY.md.
- **تست:** SQL Server واقعی: پنج Provider خراب در مسیر موفق و Rejected بدون هیچ رکورد؛ شاهد مثبت؛ بررسی جهش؛ کل Template.sln: 657 کل، 655 موفق، 2 skipped، 0 ناموفق. Provider واقعی `Blocked` روی #236. جزئیات: `docs/handoffs/ISSUE-235-ack-signing-provider-contract.md`.

## 2026-10-30 — Issue #235: SQL Server transactional/security tests and a concurrency fix
- **تست:** هفت تست روی SQL Server واقعی: هم‌زمانی apply، rollback و retry، انقضا/یک‌بارمصرف/ابطال توکن، حدس‌زنی و rate limit، مصرف هم‌زمان توکن، مجوزهای دقیق زیرسیستم‌ها و جداسازی شرکت‌ها، replay و استفادهٔ مجدد متعارض.
- **باگ یافته و رفع‌شده:** Deadlock (1205) در درخواست‌های هم‌زمان تکراری apply/enrollment → خطای مدیریت‌نشده. رفع با `sp_getapplock` تراکنشی؛ تست‌ها پیش از رفع شکست خوردند و پس از رفع سه بار پشت‌سرهم موفق بودند. کل Template.sln: 664 کل، 662 موفق، 2 skipped، 0 ناموفق.
- **انجام نشد:** E2E خودکار دو-میزبانه (Vendor + مشتری)؛ دلیل در handoff. جزئیات: `docs/handoffs/ISSUE-235-provisioning-e2e-tests.md`.


## 2026-10-30 — Issue #235: first-login/enrollment guard for the provisioned administrator
- **ممیزی:** مدیر اولیه با رمز تصادفیِ ناشناخته ساخته می‌شود و رمز واقعی را فقط در Enrollment تعیین می‌کند؛ پس «تغییر رمز در اولین ورود» از قبل ساختاری برقرار بود. معیار باز قبلی («محدودسازی نشست») به‌صورت لایهٔ دفاعی بسته شد.
- **تغییر:** `SessionValidator` نشست کاربری را که claim با `MustChangePassword=true` دارد رد می‌کند؛ `AuthenticationService` ورود چنین کاربری را حتی با رمز درست با پاسخ عمومی `InvalidCredentials` رد و رویداد `LoginDeniedEnrollmentPending` را ثبت می‌کند (بدون افزایش شمارندهٔ قفل). بدون Schema/Migration/endpoint جدید.
- **تست:** واحد (نشست و ورود) و SQL Server واقعی در مسیر apply ← reissue ← enrollment؛ اجرای کامل Template.sln: 660 کل، 658 موفق، 2 skipped، 0 ناموفق. بازبینی مستقل Subagent اجرا نشد. جزئیات: `docs/handoffs/ISSUE-235-first-login-enrollment-guard.md`.


## 2026-10-30 — بازآشتی وضعیت Vendor پس از PR #281 و mergeهای بعدی
- **مبنای بازبینی:** `main` در SHA `a5db9e3f3a04ebdb8cafd0f5f02767cdaa3f36f6`.
- **PR #281:** در `2ee1edc52904833bc28eead5a679185377363c24` ادغام شد. snapshot بعدی PR #285 وضعیت پس از #284 را ثبت کرد؛ این ثبت، وضعیت را تا #308 دوباره تطبیق می‌دهد.
- **mergeهای واقعی پس از #281:** #282 `f4a0462b0972e7686a686eca0febfaee680baecf`; #283 `95a098895056a1fe34348fac93091bf2f82780b4`; #284 `81ed5f64ed86ebbae47a433cea63e3655cb1d24b`; #285 `7abb33128c29f8eacbac67285e6acfd898c9af0b`; #286 `c139818e181db7cd37c47d9a82fae6449088663f`; #287 `d74ca15a517ddbb72d8711b55b0379381ecc34dd`; #288 `211b43d0c0792e52276e278faf9cb1e040ca55ca`; #289 `8a81f2d7cda38e08a4b43a1d1ddad1704fbff9e3`; #290 `50d6977d7452b4ee2879c2379140ebbce3e455b9`; #291 `8f12a38ee7bc15634f3aa559176948b1fb66b5f1`; #292 `a3690a567c692292d899bc26f968a093b7ba20cb`; #293 `2884c61ade381670f754a1b8027c496a6e8eb7b2`; #294 `07dc0a4f1f784dfd2800fb8da12a70f05b67a862`; #295 `9d09944af559b68dc331d37816c7db8dad68f5b1`; #296 `d1987fd37c2aeac1039cc6f05ff5f500801d42bf`; #297 `341e19a8cb93953dd9be6e9257f9ad504fcf96f6`; #298 `f9a2adf0ef20ff23e77ffef0cc0075e28e9e9555`; #299 `89ffada5d9f317ddbe9a1e379d23650409c7f3ee`; #300 `0ba6a8862001a838884abfa15394e83d21625f75`; #301 `5cbd224e594dcbb72a7ac04866b7abdd68954c0dd`; #302 `4c259280c40af7037901e9cb6f33c64206dbdaeb`; #304 `56e1f66132bbd0a8a193f27853c7220ebc76fa48`; #305 `769e0b8cce1da1b80e702427cd843999f98898ec`; #306 `ac3d481cf8d8091bd139295a9423aa30e0011fb7`; #307 `1bbcc6f45342111f7582ba0f59418f224b2ba618`; #308 `a5db9e3f3a04ebdb8cafd0f5f02767cdaa3f36f6`. PR #303 بسته شد و ادغام نشد.
- **CI روی main:** .NET CI `41014946317` با تمام jobهای build/test، client، publish Linux/Windows، E2E، secret scan و publish scripts موفق؛ Semgrep `41014946340` و Push on main `41014946368` موفق. Vendor CI آخرین بار روی SHA `1bbcc6f...` با run `41010924327` موفق بود؛ برای SHA فعلی پس از PR #308 اجرای Vendor CI جداگانه وجود ندارد.
- **PR #282 پس از merge:** run `40843073694` بخشی از jobها را موفق و build-and-test/e2e/publishها را cancelled ثبت کرد. rerun job build نیز لغو شد. اجرای کامل .NET CI و Vendor CI روی descendant SHA ادغام PR #284 موفق شد؛ این شاهد تغییرات #282 را در بر می‌گیرد، ولی run کامل روی SHA دقیق #282 نیست.
- **PR #284:** ترتیب مجوزدهی target پیش از بررسی RowVersion/status در endpoint تصمیم approval، با تست رگرسیون، در main است؛ Vendor CI `40845416309`، .NET CI `40845416352`، Semgrep `40845416374` و Push on main `40845416241` موفق‌اند.
- **شکست‌های تاریخی شاخهٔ customer-side #235:** در SHAهای superseded، build به علت shadow شدن `System.Convert` و ناسازگاری نوع آرگومان‌ها شکست خورد؛ یک اجرای client به علت نبودن فایل component در همان commit و اجرای بعدی به علت عدم تطابق متن label تست شکست خورد. اجرای دیگر شش تست verifier را به علت fixture بدون trust/installation settings و خطای `provisioning_consumer_not_configured` شکست داد. این موارد در head نهایی #307 با .NET CI `41010026198`، Vendor CI `41010026058` و Semgrep `41010026110` موفق شدند؛ #308 نیز روی head نهایی با .NET CI `41014003705` و Semgrep `41014003694` موفق شد. این اصلاحات customer-side هستند و در این PR Vendor تغییر داده نمی‌شوند.
- **PRهای باز در زمان بازبینی:** #309، #310 و #311 همگی customer-side Issue #235 هستند؛ در این شاخه دست‌کاری یا merge نمی‌شوند.
- **شکاف تصمیمی #228:** زیرسیستم‌ها در payload provisioning قرار می‌گیرند، اما اتصال قطعی آن‌ها به sequence/payload یک لایسنس صادرشده در Vendor contract موجود نیست؛ تعیین مدل یک/چند لایسنس برای نصب و binding آن باید جداگانه روشن شود.

## 2026-10-30 — Issue #228: محدودسازی کد خطای acknowledgement در Vendor
- **یافته:** قرارداد acknowledgement امضا را بررسی و binding درخواست را تأیید می‌کرد، اما Vendor هر `ErrorCode` متنی تا 128 نویسه را در نتیجهٔ `Rejected` می‌پذیرفت. امضای معتبر اصالت فرستنده را ثابت می‌کند، نه اینکه متن error فاقد PII، stack trace یا diagnostics باشد.
- **اصلاح:** `ProvisioningAcknowledgementStateRules` فقط دو کد ثابت فعلی consumer یعنی `provisioning_parent_reference_invalid` و `provisioning_initial_admin_conflict` را می‌پذیرد. کد ناشناخته fail-closed رد می‌شود و transition/ack metadata/audit commit نمی‌شود.
- **تست رگرسیون:** unit tests برای دو کد مجاز و متن آزاد/کد ناشناخته/PII-like؛ SQL Server integration test برای پاسخ 400 و باقی‌ماندن درخواست در `PendingDelivery` بدون ack digest یا audit.
- **دامنه:** فقط Vendor Host، تست‌های Vendor و مستندات؛ هیچ تغییر در `Server/**`، customer schema یا migration انجام نشد.
- **اعتبارسنجی:** تست محلی اجرا نشده؛ PR و CI لازم است. وضعیت این تغییر `Implemented / Not Verified` تا سبز شدن بررسی‌های الزامی است.
- **Branch / PR:** `fix/vendor-acknowledgement-error-code-allowlist` / [PR #312](https://github.com/hamidreza1394/Accounting/pull/312).


## 2026-10-30 — رفع یافتهٔ Gitleaks در تست provisioning (#235)
- **شاهد شکست:** .NET CI run `41021431573` روی head قبلی PR #309 در job `secret-scan` شکست خورد؛ Gitleaks مقدار ثابت password-like را در fixture تست `CustomerProvisioningTransactionalSqlServerTests.cs` شناسایی کرد. این مقدار فقط دادهٔ آزمایشی بود و هیچ credential عملیاتی/مشتری نبود، اما نگه‌داشتن literal ثابت در repository غیرضروری و خلاف hardening بود.
- **اصلاح:** مقدار ثابت حذف شد؛ رمزهای موردنیاز تست با `RandomNumberGenerator.GetBytes(32)` به‌صورت runtime و per-test تولید می‌شوند. تست همچنان بررسی می‌کند رمز و enrollment token در audit ذخیره نشده‌اند.
- **دامنه:** فقط تست و handoff/work log در شاخهٔ مستقل Issue #235؛ بدون تغییر Vendor، دیتابیس مشتری، migration یا secret configuration.
- **اعتبارسنجی:** Gitleaks روی SHA قبلی ناموفق بود؛ اجرای CI روی commit اصلاحی در انتظار است. هیچ نتیجهٔ سبزی برای SHA جدید ادعا نمی‌شود.


## 2026-10-30 — Issue #235: اتصال provisioning به یک لایسنس امضاشده (D-054)

- **تصمیم مالک محصول:** مدل تک‌لایسنس تأیید شد؛ کلاینت فقط licenseId و subset اختیاری زیرسیستم را می‌فرستد؛ Vendor باید entitlement و sequence را از آخرین envelope امضاشده استخراج کند؛ v4 با licenseId/sequence و سازگاری canonical v1-v3؛ revalidation در صدور و export؛ محدودیت آفلاین مستند؛ MaxCompanies یا atomic enforcement یا NotImplemented؛ افزودن ماژول به شرکت موجود و تجمیع چند لایسنس NotImplemented؛ v3 backfill نمی‌شود.
- **قرارداد:** payload schema به v4 ارتقا یافت و دو فیلد licenseId و sequence اضافه شد. canonical writer برای v1-v3 تغییری نمی‌کند؛ تست byte-for-byte برای canonical v3 افزوده شد؛ validator v1-v4 را می‌پذیرد و binding را در v4 اجباری می‌کند.
- **Vendor:** LicenseSigningTrustStore برای کلید عمومی مستقل Licensing:TrustedSigningKeys؛ VendorProvisioningLicenseResolver برای اعتبارسنجی envelope، امضا، digest، customer/installation، وضعیت، تاریخ و sequence؛ API انتخاب لایسنس معتبر؛ ایجاد request فقط با licenseId و subset اختیاری؛ revalidation در issuance و export؛ idempotency شامل licenseId و sequence.
- **UI:** فرم Provisioning اکنون لایسنس معتبر را انتخاب می‌کند؛ زیرسیستم‌ها از entitlementهای برگردانده‌شده توسط API نمایش داده می‌شوند؛ نبود subset به معنی همهٔ entitlementهای امضاشده است.
- **تست‌ها:** تست canonical v3، v4 schema/signature، idempotency licenseId+sequence، UI selection/subset و SQL Server resolver برای subset، sequence تاریخی و کلید لغوشده اضافه شد. اجرای محلی هنوز انجام نشده و CI تا ایجاد PR باید نتیجه بدهد.
- **Migration:** Migration جدیدی اضافه نشد؛ binding در payload امضاشده ذخیره می‌شود. هیچ تغییر schema در دیتابیس Customer انجام نشده است.
- **محدودیت‌های صریح:** MaxCompanies، افزودن زیرسیستم به شرکت موجود و تجمیع چند لایسنس NotImplemented. درخواست‌های v3 backfill نمی‌شوند و Vendor آن‌ها را دوباره امضا/Export نمی‌کند.
- **کار باقی‌مانده:** اجرای CI؛ رفع خطاهای کامپایل/تست؛ افزودن تست SQL Server برای revalidation در issuance/export؛ هماهنگ‌سازی E2E دو میزبان در PR #317 با API جدید v4 (ساخت لایسنس و انتخاب licenseId)؛ بررسی کلید عمومی واقعی در deployment docs؛ تکمیل handoff و به‌روزرسانی Issue #235.
- **Branch:** feat/issue-235-single-license-v4 — وضعیت تا CI سبز: Implemented in branch / NotVerified؛ هنوز Merge نشده است.


## 2026-10-30 — بازخورد CI و اصلاحات v4

- Vendor CI run 851 روی commit قبلی، به علت تداخل نام range variable در LINQ query مربوط به Export، با CS1931 شکست خورد؛ نام متغیرهای query به targetCustomer/targetInstallation تغییر کرد.
- .NET CI run 2763 روی commit قبلی، یک شکست در تست canonical v3 داشت؛ JsonWriter کاراکترهای + و متن غیر ASCII را escape می‌کند. vector تست به رشتهٔ ASCII ثابت و escape صحیح + تغییر کرد تا آزمون byte-for-byte پایدار بماند.
- در تست SQL Server resolver پوشش رد مشتری/نصب دیگر، وضعیت Suspended، خارج از بازهٔ اعتبار، کلید لغوشده، subset خارج از entitlement، Sequence تاریخی و رد امضای Provisioning با Sequence قدیمی اضافه شد.
- E2E دو میزبان نیز اکنون بعد از اولین Export موفق، Sequence جدیدتر صادر می‌کند؛ Export مجدد بستهٔ قدیمی باید 409 بگیرد، ولی فایل قبلاً Exportشده همچنان در Customer آفلاین قابل اعمال است و acknowledgement معتبر می‌تواند جریان را تکمیل کند.
- اجرای مجدد روی SHA نهایی پس از این اصلاحات هنوز در انتظار CI است؛ شکست‌های runهای قبلی به‌عنوان نتیجهٔ نهایی تلقی نمی‌شوند.


## 2026-10-30 — اصلاح build و همگام‌سازی E2E نسخه ۴
- شکست Vendor CI روی PR #319 و #323 ریشه‌یابی شد: دو fixture JSON در C# به‌اشتباه double-escaped شده بودند و باعث CS1003/CS1056 می‌شدند.
- fixture سمت Vendor در شاخه #319 اصلاح شد؛ همان fixture در تست دو میزبان PR #323 نیز اصلاح شد. اجرای CI جدید روی SHAهای جدید آغاز شده است؛ نتیجه هنوز نهایی نیست.
- توضیح PR #319 و handoff به PR #323 به‌روزرسانی شد؛ PR #317 قدیمی v3 همچنان ممنوع از Merge است.


## 2026-10-30 — همگام‌سازی E2E با اصلاح PR #318
- PR #319 از نظر محتوای Server branch هنوز فاقد fix ادغام‌شدهٔ PR #318 بود، چون شاخه از commit پیش از آن منشعب شده بود؛ نسخهٔ merged فایل ApiEndpoints و تست regression به شاخهٔ #319 منتقل شد.
- شاخهٔ E2E جدید از آخرین HEAD #319 ساخته شد تا تست فایل ناقص در Customer Host نیز پاسخ 400 عمومی را از همان کد ادغام‌شده دریافت کند.


## 2026-10-30 — Issue #214: reconcile offline revocation contract status

- **Finding:** `docs/handoffs/ISSUE-214-offline-revocation-contract.md` still said "Implemented in branch; CI verification pending", although PR #292 had already merged the contract into main.
- **Correction:** handoff now records PR #292 and the successful .NET CI run `40864326688`, Vendor CI run `40864326665`, and Semgrep run `40864326732`.
- **Boundary:** only the signed snapshot contract/validator is implemented. Customer-side durable sequence persistence, installed-license enforcement, backup-restore rollback handling, and production signer are still not implemented/configured.
- **Architecture note:** a database-only high-water mark cannot prove rollback resistance after restoring an older full database backup unless its trust anchor is held outside that backup failure domain.
- **Changes:** handoff and this work-log entry only; no runtime code, schema, migration or key material changed.


## 2026-10-30 — بازبینی نهایی Issue #235 پس از PR #329

- **مبنای بازبینی:** `main` @ `43d28253eb9f0f38ff25d0dfea143aeacdb06871`؛ compare با `main` برابر identical بود.
- **اصلاح وضعیت:** handoff قبلی #235 هنوز consumer را NotImplemented معرفی می‌کرد؛ این وضعیت پس از PRهای #302، #305–#308، #293–#294، #318، #319 و #329 منسوخ شده بود. handoff و بخش جاری PROJECT_STATUS با شواهد merge/CI به‌روز شدند؛ snapshotهای تاریخی دست‌نخورده باقی ماندند.
- **E2E v4:** PR #329 با merge commit بالا ادغام شد. .NET CI run `41052515115` (۷ job)، Vendor CI run `41052515292` و Semgrep run `41052515389` موفق‌اند.
- **شواهد consumer:** PR #307 با .NET CI `41010026198`، Vendor CI `41010026058` و Semgrep `41010026110` موفق؛ PR #308 با .NET CI `41014003705` و Semgrep `41014003694` موفق.
- **دامنه اثبات:** E2E دو TestServer و دو دیتابیس SQL Server مستقل را در یک test process اجرا می‌کند؛ دو ماشین/دو OS process واقعی یا staging شبکه‌ای را اثبات نمی‌کند.
- **وضعیت Production:** signerهای پیش‌فرض برای license/provisioning/ack همچنان unavailable/fail-closed هستند. KMS/HSM و چرخه کلید در #236، revocation آفلاین در #214 و checkpoint مستقل Audit در #107 به انتخاب provider/مقصد و دسترسی عملیاتی نیاز دارند.
- **تست محلی این بازبینی:** اجرا نشد؛ فقط نتایج واقعی GitHub Actions بالا بررسی شدند.
- **تغییرات این PR:** فقط مستندات وضعیت، handoff، work log و هم‌ترازی شماره ADR بعدی؛ هیچ کد runtime، schema یا migration تغییر نکرد.


## 2026-10-30 — Issue #107: Production audit checkpoint startup verification

- Added a Production-only hosted startup probe that verifies the complete Vendor audit chain against the independently permissioned checkpoint store before readiness.
- The probe reads the latest checkpoint and appends only when no checkpoint exists or the chain has advanced; it does not perform a redundant append on an unchanged chain.
- Startup fails closed on provider/read/append/integrity errors; logs contain only exception type. Production requires exactly one marker adapter; generic/test store registrations cannot shadow it.
- Added SQL Server integration coverage for initial checkpoint, idempotent restart, unavailable provider, append failure, Development skip, and ambiguous adapter registrations.
- No provider-specific cloud dependency, key, credential, local-file fallback, same-database fallback, or migration was introduced. External provider selection, failure-domain separation, immutable retention, periodic verification, alerting, and restore drills remain operational gates.
- Local tests were not executed in this GitHub-only workflow; require .NET CI, Vendor CI, and Semgrep on the final PR head before merge.


## 2026-10-30 — Issue #236: مالک تصمیم امضای HSM/PKCS#11 را پذیرفت

- پاسخ مالک ثبت شد: Vendor می‌تواند روی سیستم/سرور اختصاصی یا سرور ابری اجرا شود؛ Azure/AWS دسترسی/مجوز ندارد؛ بودجهٔ HSM فعلاً صفر است.
- D-055 به وضعیت Accepted تغییر کرد. معماری deployment-neutral است و هیچ Azure/AWS adapter ساخته نمی‌شود.
- توسعهٔ adapter PKCS#11 و تست‌های CI با SoftHSM (صرفاً تست) مجاز است؛ Production signing برای بسته و لایسنس تا تأمین HSM واقعی همچنان Blocked و fail-closed می‌ماند. SoftHSM یا software key جایگزین Production نیست.
- ACK هر نصب فقط با TPM/OS secure store غیرقابل‌استخراج فعال می‌شود؛ نبود backend امن به معنی fail-closed است.


## 2026-10-30 — Issue #236: Runbook عملیات HSM/TPM

- Runbook مستقل برای Key Ceremony، کنترل دو نفره، نقش‌های HSM Custodian/Runtime، کلیدهای مجزای package/license و ACK نصب مشتری، CKAهای لازم، PIN/secret handling، rotation overlap، revocation، compromise response، Backup/Recovery، health/alerting و صورت‌جلسه اضافه شد.
- رویهٔ ACK مشتری صریحاً offline و per-installation است؛ TPM/OS secure store باید non-exportable بودن را اثبات کند و در فقدان آن fail-closed می‌ماند.
- Runbook به سازنده یا cloud خاص وابسته نیست و هیچ private-key export یا software fallback را مجاز نمی‌کند.
- وضعیت اجرایی: بودجهٔ HSM صفر و HSM واقعی provision نشده؛ بخش Vendor HSM runbook فعلاً غیرقابل اجراست. SoftHSM صرفاً Testing/CI است.


## 2026-10-30 — Issue #236: هم‌ترازی Security contract برای Customer ACK

- متن قدیمی SECURITY.md که می‌گفت private key در نصب مشتری نگهداری نمی‌شود و انتخاب HSM هنوز NeedsApproval است، با تصمیم پذیرفته‌شده D-055 مغایرت داشت.
- قرارداد اصلاح شد: کلید ACK برای هر نصب جدا و non-exportable در TPM/OS secure store همان نصب می‌ماند؛ امضا محلی/offline است و کلید به Vendor منتقل نمی‌شود.
- Vendor package/license همچنان به PKCS#11 HSM واقعی با کلیدهای مجزا نیاز دارد؛ با بودجه صفر Production signing unavailable/fail-closed می‌ماند. Customer ACK adapter هنوز پیاده‌سازی نشده و default unavailable است.




## 2026-10-30 — Issue #236: adapter امضای PKCS#11 و تست SoftHSM

- افزودن Pkcs11Interop 5.3.0 (Apache-2.0) و adapter پشت interfaceهای موجود `ILicenseSigningProvider` و `IProvisioningSigningProvider`؛ بدون Adapter ابری.
- جداسازی purpose و KeyId نسخه‌دار برای لایسنس و provisioning/package؛ adapter فقط private-key object موجود را پیدا می‌کند و کلید تولید یا export نمی‌کند.
- enforce کردن ECDSA P-256، `CKM_ECDSA` روی SHA-256 digest، امضای IEEE P1363 ثابت ۶۴ بایتی و صفات `SENSITIVE/EXTRACTABLE/ALWAYS_SENSITIVE/NEVER_EXTRACTABLE/SIGN/EC_PARAMS`.
- PKCS#11 opt-in و default disabled؛ فقط محیط اختصاصی Testing، SoftHSM را قبول می‌کند؛ سایر محیط‌ها slot سخت‌افزاری و manufacturer/model allowlist‌شده می‌خواهند. `/health/signing` provider disabled/unavailable را با 503 نشان می‌دهد.
- هر تلاش امضا audit metadata-only با purpose، KeyId نسخه‌دار و نتیجه دارد؛ payload/signature/private key در audit/log ثبت نمی‌شوند. اگر audit append شکست بخورد، signature به caller تحویل نمی‌شود.
- تست واحد قرارداد provider و integration تست SoftHSM برای امضای هر دو key، verification با public key و رد SoftHSM در Production اضافه شد. Vendor CI اکنون SoftHSM را فقط در job تست نصب/initialise می‌کند.
- با بودجهٔ HSM صفر، هیچ تنظیم Production فعال نشده است؛ providerهای Production پیش‌فرض unavailable/fail-closed می‌مانند تا HSM واقعی provision شود. Runbook عملیاتی در PR #341 ادغام شد؛ اجرای واقعی Key Ceremony، rotation/revocation، HA/DR و backup/recovery به تأمین HSM وابسته است. پیاده‌سازی ACK TPM مشتری همچنان باقی است.
- Local build/test اجرا نشد (محیط GitHub-only)؛ نتایج واقعی .NET CI، Vendor CI و Semgrep روی SHA نهایی PR باید بررسی شوند.


## 2026-10-30 — Issue #236: ادغام PKCS#11 adapter و CI نهایی

- PR #344 با merge commit `5a9717ca08ec2009305e480dbf6443683bf2e60f` ادغام شد.
- .NET CI `41078370910`، Vendor CI `41078370887` و Semgrep `41078370883` موفق شدند. Vendor CI شامل تست integration واقعی SoftHSM برای امضای دو کلید و verify با public key بود؛ .NET CI شامل E2E و publish Linux/Windows نیز موفق بود.
- اشکالات کشف‌شده و اصلاح‌شده در CI: ambiguity نام ISession؛ وابستگی Linux loader به libdl؛ جداسازی readiness امضا از liveness؛ و تست محیط Staging بدون تداخل session/module.
- Production signer همچنان به‌صورت پیش‌فرض disabled/unavailable است؛ بودجهٔ HSM صفر و HSM واقعی provision نشده. هیچ Azure/AWS adapter یا software fallback اضافه نشد.
- Customer ACK TPM/OS signer per-installation هنوز پیاده‌سازی نشده و باید بر اساس OS واقعی مشتری (Windows Server یا Linux) در PR مستقل انجام شود.


## 2026-10-30 — Issue #236: Windows-first Customer ACK CNG signer

- مالک مشخص کرد نصب‌های مشتری در مرحلهٔ فعلی Windows نسخه‌های مختلف و Windows Server هستند؛ Linux در مرحلهٔ بعد backend مستقل خواهد داشت.
- Provider ویندوزی با Windows CNG اضافه شد؛ TPM KSP (`Microsoft Platform Crypto Provider`) پیش‌فرض است و OS Software KSP فقط با انتخاب صریح مجاز است. هیچ fallback خودکار TPM→Software وجود ندارد.
- Key Provisioner یک کلید ECDSA P-256 signing-only با `CngExportPolicies.None` می‌سازد و فقط public SPKI را تحویل می‌دهد؛ Sign فقط کلید از قبل provision شده را باز می‌کند و هرگز کلید نمی‌سازد/جایگزین نمی‌کند.
- DI به‌صورت opt-in و Windows-only است؛ در سایر حالت‌ها default unavailable/fail-closed باقی می‌ماند. تست‌ها روی Windows CI با Software KSP صرفاً به‌عنوان fixture اجرا می‌شوند؛ این تست سخت‌افزار TPM واقعی را اثبات نمی‌کند.
- تست‌ها قبل از implementation نوشته شدند و CI Windows برای اجرای مجموعهٔ محدود CNG اضافه شد. اجرای محلی در این محیط انجام نشده؛ نتیجهٔ واقعی باید از CI PR نهایی ثبت شود.
- باقی‌مانده: command امن Key Ceremony/Provisioning و ثبت public key در Trust Store مشتری/Vendor، audit durable metadata-only، health/readiness و تست سخت‌افزار TPM واقعی؛ حداقل نسخه Windows/Server هنوز certify نشده است. Linux در PR آینده.


## 2026-10-30 — Issue #236: تکمیل Windows ACK signer و فرمان Key Ceremony

- نصب‌های مشتری در مرحله فعلی Windows نسخه‌های مختلف و Windows Server هستند؛ Linux به مرحله مستقل بعدی موکول شد.
- Provider ویندوزی با Windows CNG و Key Storage Provider صریح اجرا می‌شود. TPM KSP پیش‌فرض است؛ Software KSP فقط انتخاب صریح OS-protected و بدون fallback خودکار است.
- KeyName و KeyId از InstallationId معتبر و KeyVersion مشتق می‌شوند؛ هر نصب namespace مجزا دارد و rotation با نسخه جدید انجام می‌شود. کلید ECDSA P-256 با CngExportPolicies.None و KeyUsage.Signing ایجاد می‌شود؛ فقط public SPKI بازمی‌گردد.
- فرمان آفلاین `dotnet Server.dll --provisioning-ack-key create` اضافه شد. فرمان به Enabled وابسته نیست و signer runtime را فعال نمی‌کند؛ پس از ثبت public key در هر دو Trust Store، اپراتور می‌تواند فعال‌سازی را جداگانه انجام دهد.
- CI ویندوز تست‌های focused را اجرا می‌کند؛ Software KSP صرفاً fixture تست است و آزمون TPM واقعی روی Windows Server سخت‌افزاری هنوز لازم است.
- PR #347 در حال CI است. Local build/test در این محیط اجرا نشده است. Audit durable metadata-only، trust enrollment/rotation/revocation workflow، readiness و ماتریس نسخه Windows/TPM همچنان باقی‌اند.


## 2026-10-30 — Issue #236: اصلاح فیلتر تست Windows CNG در MTP

- **علت ریشه‌ای:** `Template.Tests.csproj` از xUnit v3 و Microsoft Testing Platform استفاده می‌کند؛ دستور CI از نحو فیلتر VSTest یعنی `--filter FullyQualifiedName~...` استفاده می‌کرد. MTP در xUnit v3 این نحو را پشتیبانی نمی‌کند و در نتیجه `Zero tests ran` با exit code 8 می‌داد.
- **اصلاح:** فیلتر به `--filter-class Template.Tests.Integration.WindowsCngCustomerProvisioningAcknowledgementSigningTests` تغییر کرد تا دقیقاً کلاس Windows CNG اجرا شود؛ هیچ تستی حذف یا Skip نشده است.
- **مستندات:** فرمان صحیح در `docs/TESTING.md` ثبت شد.
- **اعتبارسنجی:** علت با لاگ Windows CI run `41085741694` و مستندات رسمی xUnit v3/Microsoft Testing Platform تأیید شد؛ CI روی شاخهٔ اصلاحی هنوز باید اجرا شود.

## 2026-10-30 — Issue #236: customer ACK trust revocation and rotation overlap

- شکاف: Trust Store مشتری فقط وجود SPKI را بررسی می‌کرد و راه explicit برای revoke کردن KeyId بدون حذف کلید عمومی نداشت.
- تغییر: تنظیم `Provisioning:RevokedAcknowledgementKeys:<keyId>=true` اضافه شد؛ کلید revoked حتی با وجود SPKI در trusted set رد می‌شود. overlap rotation با نگه‌داشتن دو public key و revoke نسخه قدیمی پوشش داده شد.
- تست: unit tests برای key resolution، revoked key، overlap نسخه جدید و مقدار malformed اضافه شدند. مرور اولیه نشان داد `bool.TryParse` نامعتبر را به‌اشتباه مانند «revoked=false» تلقی می‌کرد؛ حالا هر مقدار غیر null که boolean معتبر نباشد fail-closed است. CI باید روی PR نهایی اجرا شود؛ تست محلی اجرا نشده است.
- محدودیت: این revocation مبتنی بر configuration است و در برابر restore کامل backup/config قدیمی به‌تنهایی anti-rollback مقاوم نیست؛ trust anchor مستقل/rollback-resistant هنوز نیازمند طراحی است.


## 2026-10-30 — Issue #236: ادغام revocation کلید ACK و CI نهایی

- PR #352 با merge commit `b6ee9419abf53997e28dec19105645d6ed9f8833` ادغام شد.
- .NET CI run `41090537956` و Semgrep run `41090537922` موفق شدند؛ ۶۸۲ تست .NET شامل ۶۷۸ موفق، ۴ Skip و صفر شکست بود. تست CNG ویندوز، Windows/Linux publish و E2E نیز موفق شدند.
- Trust Store مشتری اکنون `Provisioning:RevokedAcknowledgementKeys:<keyId>=true` را پشتیبانی می‌کند؛ SPKI برای audit باقی می‌ماند، KeyId revoked رد می‌شود، مقدار غیر null نامعتبر fail-closed است و rotation overlap با KeyId جدید فعال تست شده است.
- محدودیت صریح: revocation فعلی در configuration است؛ restore کامل backup/config قدیمی می‌تواند trust قبلی را بازگرداند. anti-rollback مستقل هنوز نیازمند طراحی/تصمیم و anchor خارج از همان backup است.
- PR #351 نیز ادغام شد و فیلتر xUnit v3/MTP در Windows CI به `--filter-class` تغییر کرد؛ تست CNG اکنون واقعاً کشف و اجرا می‌شود.
- باقی‌مانده: enrollment عملیاتی SPKI در Trust Store مشتری و Vendor، durable metadata-only audit/readiness، تست TPM واقعی و ماتریس نسخه‌های Windows/Server؛ Linux backend در مرحلهٔ بعدی.


## 2026-10-30 — Issue #228: هم‌ترازی داشبورد Vendor با وضعیت واقعی Provisioning

- بازبینی main نشان داد داشبورد هنوز متن‌های قدیمی «نسخه اولیه رابط»، «در حال توسعه» و «عملیات نوشتن در مراحل بعدی تکمیل می‌شود» را نمایش می‌دهد؛ درحالی‌که CRUD تجاری، گردش درخواست Provisioning v4، maker-checker، export بسته و intake پاسخ ACK در کد موجودند.
- متن Overview اصلاح شد تا قابلیت‌های پیاده‌سازی‌شده را درست معرفی کند و هم‌زمان محدودیت Production را صریح بگوید: امضای بسته و لایسنس تا تأمین HSM واقعی disabled/fail-closed است.
- تست رگرسیون App.test.tsx به‌روزرسانی شد تا متن جدید وضعیت عملیاتی، توضیح جریان ACK و حذف برچسب قدیمی را بررسی کند.
- هیچ endpoint، schema، migration، secret، private key یا دیتابیس مشتری تغییر نکرد. رمز اولیه در Vendor جمع‌آوری/ذخیره نمی‌شود؛ مدیر مشتری در Enrollment امن رمز خود را تعیین می‌کند.
- handoff: docs/handoffs/ISSUE-228-vendor-onboarding-finalization.md. Issue #228 به‌دلیل HSM واقعی، checkpoint مستقل #107، revocation آفلاین #214 و آزمون میدانی هنوز Production-complete نیست.
- اجرای محلی انجام نشد؛ .NET CI و Vendor CI و Semgrep باید روی PR اجرا شوند.


## 2026-10-30 — Issue #228: ادغام PR #354 و شواهد CI

- PR #354 با merge commit `31f1873ce9f26405beaf773e876bf99155c43fa2` ادغام شد.
- Vendor CI `41095191395` موفق: typecheck، Vitest، build standalone UI، runtime dependency audit، asset budgets، Restore/Build/Test Vendor، EF snapshot guard، SoftHSM tests، publish و بررسی artifact.
- .NET CI `41095191238` موفق: secret scan، client، build/test، migration guards، Linux publish، E2E مرورگر، Windows CNG tests و Windows publish.
- Semgrep `41095191239` و CodeQL `41095184027` موفق شدند.
- Post-merge CI روی main با SHA `31f1873ce9f26405beaf773e876bf99155c43fa2` در زمان ثبت این یادداشت هنوز در حال اجراست: .NET `41096833025`، Vendor `41096832981`، Semgrep `41096832996`.
- PR #354 فقط متن داشبورد و آزمون آن را اصلاح کرد؛ هیچ endpoint، schema، migration، secret، private key یا customer DB تغییر نکرد. Issue #228 به‌دلیل HSM واقعی، checkpoint مستقل #107 و revocation آفلاین #214 Production-complete نیست.


## 2026-10-30 — Issue #228: حذف KPIهای ساختگی داشبورد Vendor

- داشبورد چهار KPI با مقدار «—» و متن «نمای آماری در مرحله بعد» نشان می‌داد؛ این‌ها نه داده واقعی بودند و نه برای وضعیت فعلی کنترل‌پلین مفید.
- آن‌ها با کارت‌های وضعیت قابلیت جایگزین شدند: مدیریت تجاری، Provisioning v4، تأیید دو نفره و وضعیت مسدود بودن امضای Production تا تأمین HSM واقعی.
- تست UI وجود چهار کارت و نبود placeholder آماری را بررسی می‌کند.
- هیچ API یا DB تغییر نکرد؛ کارت‌ها ادعای تعداد رکورد یا آمادگی Production نمی‌کنند.


## 2026-10-30 — Vendor status reconciliation after PR #356

- Main اکنون روی SHA `78afd4dc3ea02a9816e9e72a3d7d24b969a034dd` است؛ PR #356 کارت‌های وضعیت داشبورد را ادغام کرد.
- PR #356 checks روی head موفق بودند: Vendor CI `41097366027`، .NET CI `41097365977` و Semgrep `41097366191`.
- Handoffهای #228 و #235 با وضعیت ادغام PR #356 و #329 هم‌تراز شدند؛ متن قدیمی «PR #329 باز است» حذف شد.
- Post-merge CI main در زمان ثبت: Semgrep `41100115088` موفق؛ .NET CI `41100114950` و Vendor CI `41100114963` هنوز در حال اجرا. موفقیت پس از merge تا تکمیل آن‌ها اعلام نمی‌شود.
- هیچ کد اجرایی، API، schema، migration، secret، private key یا customer DB تغییر نکرد.
- موانع Production همچنان واقعی‌اند: HSM مرکزی provision نشده (بودجه صفر)، sink مستقل Audit هنوز provider/credential ندارد، revocation آفلاین هنوز consumer/enforcement ضد rollback ندارد، و آزمون فیزیکی TPM/دو ماشین و backup/restore انجام نشده است.
- PR #356: https://github.com/hamidreza1394/Accounting/pull/356


## 2026-10-30 — D-056: پذیرش امضای نرم‌افزاری برای Stage 1
- مالک محصول استفاده از همان Provider مبتنی بر PKCS#11 با SoftHSM در Development و Staging را تأیید کرد؛ Production software فقط با پذیرش ریسک صریح و Audit در هر startup مجاز است.
- Stage 1: کد کامل + Staging روی سیستم مالک/سرور ابری + نصب مشتری روی ماشین دوم؛ Stage 2: HSM واقعی، دو میزبان واقعی و بازیابی شواهد پیش‌نیاز برچسب Production کامل.
- این PR فقط تصمیم و اسناد را ثبت می‌کند؛ تغییر کد Assurance، استقرار واقعی، آزمون دو میزبان فیزیکی و بازیابی هنوز انجام نشده‌اند.
- فایل‌ها: D-055، D-056، DECISIONS.md، INDEX.md، WORK_LOG.md.


## 2026-10-30 — Issue #236 / PR #360: رفع خطای Build در Validate options
- CI در Job `build-test-package` سه خطای کامپایل `CS1503` در `tests/Vendor.Host.Tests/Pkcs11SigningProviderTests.cs` نشان داد: متد `Validate(bool isProduction = false)` به‌صورت method group دیگر با `Action` سازگار نبود.
- اصلاح: overload بدون پارامتر `Validate()` اضافه شد که به `Validate(isProduction: false)` واگذار می‌کند؛ overload بولی بدون مقدار پیش‌فرض باقی ماند تا فراخوانی‌های محیط Production همچنان صریح باشند و method-groupهای تست/مصرف‌کنندگان قدیمی سازگار بمانند.
- فایل تغییرکرده: `Vendor/Vendor.Host/Signing/Pkcs11SigningProvider.cs`.
- Commit: `fd9f0e9f9135d498266cc847bb0479a650913121`.
- تست پیش از اصلاح: CI شکست خورد با سه خطای CS1503 در تبدیل method group به `System.Action`.
- تست پس از اصلاح: در انتظار CI روی commit جدید؛ اجرای محلی در این نشست انجام نشده است.
- هیچ migration، schema، customer DB، secret یا کلید خصوصی تغییر نکرد.


## 2026-10-30 — Issue #236 / PR #360: پوشش آزمون startup assurance
- سه تست برای مسیر startup افزوده شد: ثبت رخداد metadata-only در Production با پذیرش ریسک Software، شکست startup در صورت شکست Audit، و عدم نیاز Development/Staging به sink ممیزی Production.
- فایل: `tests/Vendor.Host.Tests/Pkcs11SigningProviderTests.cs`.
- Commitهای تغییر: `be3836209bafd1fb8152b7e1d1a0635702c49a62` و `30da154a30350656d2c7b1f3a84a9ce81a5b8572`.
- وضعیت: تست‌ها به CI سپرده شده‌اند؛ اجرای محلی انجام نشده و تا مشاهدهٔ نتیجهٔ CI، Verified نیستند.
14: offline revocation consumer contract
- قرارداد مصرف‌کنندهٔ سمت مشتری اضافه شد: resolver مستقل کلید ابطال، state store با عملیات atomic compare-and-apply، snapshot digest و sequence high-water mark.
- `OfflineRevocationConsumer` قبل از apply امضا/زمان/ساختار snapshot را با کلید مستقل verify می‌کند؛ replay و race هم‌زمان باید در مرز atomic store رد شوند.
- API خواندن وضعیت ابطال (`IsRevokedAsync`) برای اتصال به مسیر enforcement ارائه شد.
- تست‌ها: اعمال snapshot معتبر، replay، کلید ناشناخته و دو درخواست هم‌زمان برای یک sequence.
- محدودیت صریح: هنوز adapter پایدار مشتری، اتصال به مسیر واقعی LicenseEnvelopeValidator/LicenseEvaluator و مقابله با restore دیتابیس قدیمی پیاده نشده؛ هیچ customer DB migration ایجاد نشده است.
- اجرای محلی .NET در این نشست در دسترس نیست؛ نتیجهٔ CI معیار تأیید است.


### تکمیل fail-closed در بررسی وضعیت ابطال (#214)
- بررسی ابطال دیگر `false` را برای نبود snapshot برنمی‌گرداند؛ نتیجه سه‌حالته `Unavailable/NotRevoked/Revoked` اضافه شد.
- تاریخ انقضای snapshot در state نگهداری می‌شود؛ snapshot غایب یا منقضی‌شده `Unavailable` است و مصرف‌کننده باید fail-closed عمل کند.
- تست‌های نبود snapshot و انقضای snapshot اضافه شدند. CI روی HEAD جدید باید این تغییرات را تأیید کند.


### اتصال قرارداد ابطال به ارزیابی مجوز (#214)
- `RevocationAwareLicenseEnvelopeValidator` اضافه شد: ابتدا امضای لایسنس و InstallationId را با `LicenseEnvelopeValidator` بررسی می‌کند، سپس وضعیت ابطال را می‌سنجد.
- مجوز revoked با `license_revoked` رد می‌شود؛ نبود snapshot یا snapshot منقضی با `offline_revocation_unavailable` fail-closed می‌شود؛ فقط snapshot معتبر و current که شناسه را در فهرست ندارد اجازه عبور می‌دهد.
- سه تست regression برای snapshot غایب، مجوز خارج از فهرست و مجوز باطل‌شده افزوده شد.
- این adapter هنوز به runtime customer host متصل نشده و state store پایدار هم پیاده نشده؛ این PR قرارداد اتصال را آماده می‌کند، نه ادعای enforcement production.


- تست‌های ترکیبی pipeline در همان فایل `tests/Licensing.Contracts.Tests/OfflineRevocationConsumerTests.cs` قرار گرفتند تا قرارداد consumer و validator در یک مجموعه تست هم‌خوان بمانند.



## 2026-10-30 — Issue #107: Development/Test checkpoint journal
- تست‌های اولیه برای append/read monotonic، رد sequence تکراری/کاهشی، تشخیص دستکاری checkpoint و الزام مسیر مطلق اضافه شد.
- پیاده‌سازی `DevelopmentFileVendorAuditCheckpointStore` به‌عنوان adapter محلی append-only با lock بین پردازه‌ای، اعتبارسنجی تمام رکوردها، sequence صعودی و flush روی دیسک اضافه شد.
- این کلاس فقط `IVendorAuditCheckpointStore` را پیاده‌سازی می‌کند و عمداً `IProductionVendorAuditCheckpointStore` نیست؛ هیچ Production fallback یا wiring خودکاری ایجاد نشده است.
- اجرای محلی .NET در این نشست در دسترس نیست؛ تأیید compile/test به CI واگذار شده و تا نتیجه آن ادعای موفقیت تست نمی‌شود.
- فایل‌ها: `Vendor/Vendor.Persistence/Auditing/DevelopmentFileVendorAuditCheckpointStore.cs` و `tests/Vendor.Host.Tests/DevelopmentFileVendorAuditCheckpointStoreTests.cs`.


## 2026-10-31 — Issue #107: harden Development checkpoint lock
- بازبینی PR #361 نشان داد انتظار lock در برابر IOExceptionهای دائمی می‌توانست نامحدود شود و Dispose خطادار می‌توانست semaphore درون‌پردازه را آزاد نکند.
- انتظار lock به ۱۵ ثانیه محدود شد و آزادسازی semaphore در finally تضمین شد.
- تست cancellation هنگام اشغال lock اضافه شد تا آزادشدن ProcessGate پس از لغو انتظار بررسی شود.
- تست واقعی چندپردازه‌ای و تأیید cross-platform هنوز انجام نشده‌اند؛ این محدودیت صریحاً در handoff ثبت شد.
- اجرای محلی .NET ممکن نبود؛ نتیجهٔ نهایی به CI روی آخرین SHA وابسته است.


## 2026-10-31 — Issue #107 operational hardening
- HashVersion=1 per VendorAuditEvent, versioned verifier dispatch and unique PreviousHash/EventHash indexes are already in main (Issue #107/#202).
- Production fail-closed guard and startup verification against exactly one IProductionVendorAuditCheckpointStore are already in main; no provider is selected, so Production intentionally cannot start until an approved adapter is registered.
- Added VendorAuditCheckpointBackgroundService to verify/advance the independent checkpoint every configured interval (default 300s).
- Added VendorAuditCheckpointHealthState and /health/audit-checkpoint; failures and stale verification become Unhealthy, with sanitized status only.
- Added SQLCMD permission script to keep Vendor runtime append-only on dbo.VendorAuditEvents and require a separate migration principal.
- Recovery runbook now covers periodic verification/alerting and SQL permission deployment.
- Provider selection, separate external credentials, immutability/retention, real alert receiver, SQL permission execution and a target-environment restore drill remain open. No Production sink is claimed.


## 2026-10-31 — Issue #236/#214: dedicated offline revocation signing key
- Added IOfflineRevocationSigningProvider and an unavailable fail-closed default; PKCS#11 provider signs only with OfflineRevocationKey and emits purpose-specific metadata-only audit events.
- PKCS#11 configuration now requires three distinct versioned key IDs (license, provisioning/package, offline-revocation); health/readiness verifies all three private-key objects and their required attributes.
- Added unit coverage for purpose separation, duplicate/invalid IDs, fail-closed default, and SoftHSM signing/verification using a third independent non-extractable P-256 key.
- Updated Vendor README, HSM runbook and D-055/D-056 clarification. This does not implement snapshot issuance, persistence, export or customer enforcement; those remain Issue #214 work.
- No real HSM/production key was provisioned; CI is pending after PR creation.


## 2026-10-31 — PR #369 CI syntax correction
- Vendor CI caught over-escaped JSON string literals in the new offline-revocation tests (CS1003/CS1056). Corrected the C# string escaping in the unit and SoftHSM integration tests; no production code or test assertions were weakened.
- Latest branch head triggers a fresh .NET CI, Vendor CI and Semgrep run. The prior Vendor CI failure is superseded by the corrected head; success is not claimed until the new runs complete.

## 2026-10-31 — Issue #364 / D-057: TPM NV Extend customer revocation state
- تأیید مالک محصول برای Migration مستقل customer DB و anti-rollback مبتنی بر TPM دریافت شد.
- معماری از TPM NV Extend با SHA-256 hash-chain استفاده می‌کند؛ generation و digest payload در extension data bind می‌شوند. NV index باید خارج از backup دیتابیس باشد.
- جدول singleton مشتری، migration افزایشی، rowversion، pending/accepted signed envelope و anchor digestهای قبلی/جاری افزوده شدند.
- Store در SQL Server با session-level sp_getapplock عملیات چندپردازه‌ای را serialize می‌کند و پروتکل prepare → TPM NV_Extend → finalize را با recovery crash window اجرا می‌کند.
- هر read، TPM digest، chain hash، signature، payload digest، sequence، expiry و فهرست revoked را دوباره تطبیق می‌دهد؛ backup قدیمی یا tamper به Unavailable منجر می‌شود.
- adapter TPM با Microsoft.TSS برای Windows TBS و Linux local TPM device اضافه شد؛ Linux از StrictLinuxTpmDevice استفاده می‌کند تا simulator fallback کتابخانه فعال نشود. default provider بدون section پیکربندی صریح unavailable است. ابزار create-only provisioning برای Windows/TBS و اسکریپت Linux/tpm2-tools با TCTI محلی اضافه شدند؛ اجرای واقعی، secret delivery/rotation و hardware tests هنوز NotImplemented/NotVerified هستند.
- تست‌های SQL Server اضافه شده‌اند اما اجرای محلی .NET در این نشست ممکن نبود؛ نتیجهٔ CI روی PR معیار بعدی است.


### Issue #364 follow-up — customer validator registration
- ConfigurationLicenseTrustStore به‌صورت مستقل از revocation-list trust set افزوده شد؛ کلیدها فقط ECDSA P-256 SPKI عمومی هستند و retirement flag نامعتبر fail-closed است.
- Server DI اکنون LicenseEnvelopeValidator، OfflineRevocationConsumer و RevocationAwareLicenseEnvelopeValidator را register می‌کند؛ بدون TPM config، revocation provider unavailable می‌ماند.
- تست‌های config برای unknown key، malformed retirement flag و retired key اضافه شدند.
- محدودیت: هنوز باید تمام call-siteهای runtime مشتری به RevocationAwareLicenseEnvelopeValidator منتقل شوند؛ صرف DI registration به‌تنهایی enforcement را اثبات نمی‌کند.


## 2026-10-31 — Issue #364 CI regression fixes
- Aligned the migration Designer generated output and corrected the mutation-test fixture so its synthetic migration is newer than the current migration constants.
- Added SQL Server regression coverage proving a concurrent GetCurrentAsync waits on the existing session application lock while TryApplyAsync is between prepare and TPM Extend; the test asserts only one Extend is issued.
- Re-reviewed the production store: the session lock already protects both GetCurrentAsync and TryApplyAsync. No unnecessary production code change was made.
- Latest CI result is pending; no local .NET/SQL Server or physical TPM execution is claimed.


## 2026-10-31 — PR #366 merged and CI verified
- PR #366 (feat(licensing): add TPM-anchored customer revocation state store) merged into main at head SHA d2005b618528b416c3773ed7632d09a8627685db.
- .NET CI run 41175579282 succeeded across client, secret-scan, build-and-test, publish-scripts, E2E, publish and Windows publish; Vendor CI 41175579288 and Semgrep 41175579255 also succeeded.
- Updated Issue #364 handoff and project status to distinguish merged software implementation from still-open hardware provisioning/verification and production runtime enforcement.
- No physical TPM test, production NV provisioning, secret delivery/rotation or production database migration is claimed.


## 2026-10-31 — Issue #214: canonical offline revocation snapshot factory (PR #375)
- Added Vendor-side `VendorOfflineRevocationSnapshotFactory` to validate bounded inputs, ordinal-sort/deduplicate revoked license IDs, serialize the canonical contract and sign the exact UTF-8 bytes with the dedicated revocation signer.
- Added tests for signature verification, canonicalization, invalid input bounds, unavailable signer and cancellation.
- This is only the factory slice: durable sequence/history, atomic maker-checker revocation + license state + audit, no-store export and customer runtime enforcement remain open. The Vendor default signer remains unavailable/fail-closed; no private key or customer DB migration was added.
- No local .NET SDK/build/test was available. On head `d689db2eb28f31fdc268abf6a2627b23df0274a3`, .NET CI and Vendor CI were in progress and Semgrep had succeeded when recorded; no unverified test result is claimed.


## 2026-10-31 — Issue #214: durable Vendor revocation workflow added to PR #375
- PR #375 expanded from the canonical snapshot factory to the complete Vendor-side issuance slice: immutable `VendorSignedRevocationSnapshots` table/migration, monotonic sequence serialized with transaction-owned `sp_getapplock`, and unique constraints for sequence/list/approval/triggering license.
- Maker-checker approval for `TargetStatus=Revoked` now calls the shared `VendorOfflineRevocationSnapshotFactory`, verifies the returned envelope against an independent public trust set, and atomically persists the snapshot, License status, approval decision and three audit events.
- Added authorized antiforgery POST export at `/api/vendor/offline-revocations/latest/export`; the response is audited and marked no-store/no-cache.
- Snapshot validity is explicit deployment configuration (1..365 days); missing policy, empty/unknown/revoked trust entry, unavailable signer, invalid signature, sequence lock failure or audit failure does not commit a revocation.
- Added SQL Server integration tests for success, signer unavailable, audit rollback and concurrent sequence/cumulative-list behavior, plus trust-store negative tests. The earlier factory unit tests remain in PR #375.
- **Validation:** local .NET/SQL Server tests were not run. Fresh .NET CI, Vendor CI (including EF snapshot/migration and SQL Server tests), and Semgrep on final PR head are required before merge.
- **Boundary:** no customer-side consumer, `Server/**`, customer database schema or production private key/credential is changed. #235/#364, #236 and #107 remain separate work.


## 2026-10-31 — Issue #214: consolidated revocation workflow and CI fixes (PR #375)
- Consolidated Vendor-side revocation factory, independent public trust store, signed snapshot persistence, application-lock global sequence, transactional maker-checker revocation, audit writes and antiforgery-protected no-cache export into PR #375.
- Service verifies the newly signed envelope against the independent trust store before committing license/approval state. Added SQL Server trigger rejecting UPDATE/DELETE on signed snapshot history and an integration assertion for immutability.
- Fixed CI compile error by injecting `ILogger<VendorOfflineRevocationIssuanceService>`; fixed Gitleaks false positive by changing the test-only key ID from a high-entropy-looking label to `test-kid`.
- Previous CI head `0508c553e4cab42f967bc6bcd86e48cefac2ec9a` failed on those two issues; latest head `2a9e227b28d9e54dfc08a4378642abaf4395b533` has fresh CI runs pending/in progress. No green result is claimed yet.
- Duplicate stacked PRs #376/#377 were closed as superseded; immutable-row trigger was ported into #375. Customer runtime enforcement and production signer/trust provisioning remain open.


## 2026-10-31 — Issue #214 / PR #375: suppress only the historical test-KeyId false positive
- **تکرارپذیری شکست:** پس از تغییر مقدار فعلی fixture از شناسهٔ بلند به `test-kid`، Gitleaks همچنان همان finding را گزارش کرد؛ fingerprint به commit قدیمی `0508c553e4cab42f967bc6bcd86e48cefac2ec9a`، فایل تست، rule `generic-api-key` و خط 161 اشاره می‌کند. علت این است که اسکنر کل commit range مربوط به PR را بررسی می‌کند، نه فقط working tree نهایی.
- **اصلاح حداقلی:** فقط fingerprint دقیق تاریخی در `.gitleaksignore` اضافه شد؛ rule، مسیر فایل یا الگوی عمومی مستثنا نشده است. مقدار فعلی fixture همچنان یک KeyId عمومی و غیرمحرمانه است و هیچ private key/credential در مخزن وجود ندارد.
- **اعتبارسنجی:** .NET CI run `41183238913` روی head قبلی `6e089f4f017ea1321c42acbd24e099bdd592760e` به علت همین finding شکست خورد؛ Vendor CI run `41183238859` در زمان ثبت هنوز در حال اجرا بود. اجرای جدید پس از این commit لازم است.
- **محدوده:** فقط اسکن اسرار و مستندات Vendor؛ بدون تغییر منطق runtime، دیتابیس مشتری یا migration مشتری.


## 2026-10-31 — Issue #214 / PR #375: migration syntax regression coverage
- Vendor CI run `41183238859` روی head `6e089f4f017ea1321c42acbd24e099bdd592760e` در کامپایل migration با `CS1519/CS1031/CS8124` شکست خورد؛ علت بسته‌شدن زودهنگام متد `Up` قبل از تعریف trigger بود.
- اصلاح در commit `f8743aa80ff5b2821cc64e6bc254d0396c207f64` انجام شد: `migrationBuilder.Sql` اکنون داخل `Up` است و `Down` trigger را پیش از حذف جدول برمی‌دارد.
- تست SQL Server برای immutability گسترش یافت تا UPDATE و DELETE را رد کند و شمار رکورد snapshot پس از هر دو تلاش یک باقی بماند.
- **اعتبارسنجی:** این تست در CI جدید باید همراه migration/pending-model guard اجرا شود؛ اجرای محلی .NET/SQL Server انجام نشده است.


## 2026-10-31 — independent-process checkpoint regression
- Added a helper executable and a regression test launching eight independent .NET processes that concurrently append the same sequence to one development JSONL checkpoint journal. Exactly one append must succeed; the other seven must reject the duplicate; the final journal must contain one valid record.
- Added a Windows/Linux matrix to the existing Vendor CI workflow (rather than introducing a workflow that cannot run until merged).
- This verifies cross-process locking for the Development/Test adapter only. It does not create a Production checkpoint sink or satisfy independent failure-domain, external retention/immutability, alerting or recovery requirements.
- Local .NET execution was unavailable; the matrix must pass before verification.


## 2026-10-31 — customer validator DI boundary (rebased conflict resolution)
- Reapplied on the current mainline: introduced `ICustomerLicenseEnvelopeValidator`; `RevocationAwareLicenseEnvelopeValidator` implements it; Server DI exposes only the interface and constructs the raw signature/time validator as an internal dependency.
- Added a scoped dual-host DI assertion proving the safe interface resolves while neither raw nor concrete validator is directly registered; unit coverage now targets the interface contract.
- This prevents accidental DI bypass, but is **not end-to-end license enforcement**. Production call-sites, customer envelope storage/import, authorization/audit policy, installation binding and approved OD-03/04 expiry/grace behavior remain unresolved. TPM hardware provisioning and real Windows/Linux verification remain NotVerified.
- Rebuilt this change from current `main` to resolve PR #381's divergence; CI must run on the replacement PR head. No local .NET/SQL Server execution is claimed.


## 2026-10-31 — به‌روزرسانی وضعیت Issue #364 پس از ادغام PR #382

- **PR #382:** تعارض PR #381 با بازسازی تغییر روی main حل شد؛ PR #382 با merge commit `f4027d83d1275204167464928c54ed9a185a68bf` ادغام شد.
- **CI واقعی روی head PR:** .NET CI run `41185957406`، Vendor CI run `41185957429` و Semgrep run `41185957415` همگی conclusion=success دارند.
- **تغییر تأییدشده:** فقط interface امن `ICustomerLicenseEnvelopeValidator` در DI در دسترس است؛ validator خام dependency داخلی pipeline است؛ تست unit و DI dual-host اضافه شده‌اند.
- **مرز صادقانه:** این PR اجرای end-to-end ایجاد نمی‌کند. مسیر import/storage لایسنس مشتری و runtime call-site enforcement همچنان NotImplemented است. OD-03/04 و OD-05 نیز نیازمند تصمیم مالک‌اند.
- **محدودیت محیط:** اجرای محلی .NET/SQL Server، migration روی دیتابیس واقعی و آزمون TPM سخت‌افزاری انجام نشد.
- **مستندات:** وضعیت جاری در PROJECT_STATUS و LICENSING و handoff همین Issue همگام شد؛ snapshotهای تاریخی حفظ شدند.

## 2026-10-31 — تصمیم D-058 و Issue #384 برای فعال‌سازی پایدار مشتری
- مالک محصول تأیید کرد لایسنس هر Installation فقط یک‌بار وارد شود؛ restart/reboot/recycle برنامه یا سرویس نباید درخواست مجدد لایسنس یا قفل ایجاد کند.
- در نصب اختصاصی Windows، نصب مجدد/re-image که OS-installation identity را تغییر دهد reactivation لازم دارد؛ patch/servicing عادی نباید فعال‌سازی را باطل کند.
- در Cloud، مرجع نهایی بررسی license/entitlements سمت سرور است و Client/React فقط وضعیت را نمایش می‌دهد؛ برای هر درخواست حسابداری تماس مجزا با Vendor انجام نمی‌شود.
- Issue #384 ایجاد شد. D-058 و اسناد طرح/تهدید/مدل داده به‌روزرسانی شدند.
- این PR فقط تصمیم و مستندات است؛ هیچ کد اجرایی، schema، Migration یا تست اجرایی اضافه نشده است. OD-08 و OD-06 و OD-03/04/05 همچنان نیازمند تعیین تکلیف‌اند.

## 2026-10-31 — Issue #384: تأیید سیاست provider کلید نصب و شروع پیاده‌سازی
- مالک محصول تأیید کرد TPM 2.0 مسیر ترجیحی و Software CNG + DPAPI فقط با تأیید صریح و Assurance پایین‌تر باشد.
- Issue #384 comment ثبت شد؛ PR کاری از head مستندات #385 ساخته شد.
- تست‌های Windows CNG برای رد software بدون consent، کلید non-exportable، امضای challenge و خواندن مجدد کلید پس از restart افزوده شد.
- پیاده‌سازی اولیه در PR کاری شامل CNG key provisioner، metadata شامل OS MachineGuid digest و public-key fingerprint، و فرمان مستقل `--license-installation-key create` است. فرمان هیچ private key را خروجی نمی‌دهد و کلید را خودکار در startup ایجاد نمی‌کند.
- هنوز تست/build CI برای این PR اجرا نشده؛ persistence دیتابیس و audit consent در PRهای بعدی Issue #384 تکمیل می‌شود. تست TPM واقعی انجام نشده است.

## 2026-10-31 — Issue #384: persistence مشتری و Migration افزایشی
- PR #386 با CI کامل سبز ادغام شد؛ commit `010571779e5bbbef9419f7748aa13d3700bd48ec`.
- در branch مربوط به PR #387 تست‌های مدل ابتدا افزوده شدند؛ سپس entityهای `InstallationIdentity` و `InstalledLicense`، configurationها و DbSetهای Customer Database اضافه شدند.
- Migration افزایشی `20261031130000_AddCustomerLicensePersistence` و designer/snapshot به‌روزرسانی شد؛ generator migration برای migration جدید با hand-written CHECK constraints تنظیم شد.
- مدل تاریخچهٔ کلیدهای نصب را حفظ می‌کند؛ برای هر InstallationId تنها یک identity جاری و یک license جاری مجاز است. کلید خصوصی در دیتابیس نیست؛ SoftwareProtected فقط با consent timestamp/actor قابل ثبت است.
- PR #387 هنوز CI نشده/ادغام نشده؛ هیچ Migration روی دیتابیس واقعی اجرا نشده و Vendor schema تغییر نکرده است.


## 2026-10-31 — Issue #395 / D-059: entitlement architecture and execution roadmap

- ثبت D-059 با تأیید مالک محصول: Base License واحد برای هر Installation؛ Company-scoped Module/Feature/UserSeat؛ maxCompanies در سطح Installation؛ add-on با amendment امضاشده و sequence افزایشی روی همان LicenseId.
- سیاست کاهش Seat برابر block-new است؛ عضویت‌های فعال حفظ می‌شوند. کاهش Feature limit داده را حذف نمی‌کند و ایجاد/گسترش جدید را مسدود می‌کند. انقضای Term به restricted می‌رود، بدون grace ضمنی، با حفظ read/export/recovery.
- bootstrap مدیر اولیه Offline روی Customer Host و محلی است؛ رمز/hash به Vendor، package، log یا telemetry منتقل نمی‌شود.
- sync دوحالته است: online authenticated وقتی اتصال موجود است و signed file exchange همیشه. Cloud server-authoritative است؛ Offline آخرین entitlement امضاشده پذیرفته‌شده را حفظ می‌کند.
- نقشه اجرا: #395 → #390 → #391/#392 → #393 → #394. مستندات مرجع در docs/LICENSING_IMPLEMENTATION_PLAN.md قرار گرفتند.
- این تغییر فقط مستندسازی و ثبت تصمیم است؛ هیچ قابلیت اجرایی، Migration یا Production readiness را ادعا نمی‌کند.


## 2026-10-31 — Issue #384 / PR #388: customer identity enrollment and import status

- PR #387 merged: Customer-only InstallationIdentity/InstalledLicense persistence schema is on main; no real customer database migration was run.
- PR #388 adds audited enrollment of an existing Windows CNG installation key, live OS-marker and proof-of-possession validation, explicit SoftwareProtected consent actor/time, and transactional license import/replace through ICustomerLicenseEnvelopeValidator.
- API endpoints are Infrastructure-only, antiforgery-protected, rate-limited and no-store; InstallationId is read from server configuration. Private key material is never persisted or returned.
- CI initially found 8 server compile errors (framework Convert name collision, protected Entity.Id assignment and type mismatch). Those source errors were corrected. The next CI pass exposed test fixture tuple call sites and another protected Id assignment; these were corrected on the same PR branch. Latest head CI is pending at the time of this log entry.
- D-059 changes the accepted status policy: signed Restricted envelopes are persisted for later restricted-mode enforcement; Grace is rejected because there is no implicit grace period. Runtime call-site enforcement remains a separate implementation slice.
- No local .NET/SQL Server execution, physical TPM verification or independent-host E2E is claimed. Startup reload/revalidation, operational authorization enforcement and Cloud authoritative entitlement resolution remain open.
