# تحویل وظیفه — ISSUE-UNNUMBERED-database-setup-redesign

- **Issue:** `UNNUMBERED` (شماره واقعی ثبت نشده)
- **Agent/مسئول:** Lead (Claude Code) با `architect`، `backend-developer`، `documentation-engineer` و بازبین‌های مستقل (فهرست در «Agentهای به‌کار‌رفته»)
- **تاریخ:** 2026-10-23
- **شاخه:** `feature/database-setup-redesign`
- **وضعیت:** انجام‌شده در شاخه (`Implemented`) و `Verified` محلی توسط Lead؛ تصمیم D-036 `NeedsApproval`؛ CI باز نشده؛ ادغام‌نشده

## هدف و معیارهای پذیرش
- [x] جریان راه‌اندازی دیتابیس با نام محصول Farano و پیش‌فرض‌های درست (دیتابیس `Farano`، سرور خالی) — در کد و تست (`DatabaseSetupValidationTests`)
- [x] محل نگهداری تنظیمات مستقل از پوشه برنامه و فایل‌های SQL Server، با کلید پیکربندی — `Implemented`؛ تست `DatabaseSettingsLocationTests`
- [x] مهاجرت فقط‌خواندنی از `TarazAva` بدون حذف/تغییر فایل قدیمی — تست بایت‌به‌بایت (`DatabaseSettingsStoreTests`)
- [x] رفع علت پیام عمومی «اطلاعات فرم را بررسی کنید» — `Implemented`؛ تست‌های `DatabaseSetupServiceTests`
- [x] خطاهای قابل‌اقدام با کد، مرحله و شناسه پیگیری؛ بدون نشت راز
- [x] اجرای نهایی یکپارچه Build/Test توسط Lead (بخش «بررسی و آزمون»)
- [ ] تأیید صریح کارفرما برای D-036 و املای Farano/Faranou
- [ ] اجرا/تأیید روی Windows (DPAPI، Service، IIS، UNC، `HardenWindowsAcl`) — اجرا نشد
- [ ] CI روی PR — هنوز باز نشده

## ساختار اولیه (پیش از تغییر، از `HEAD`)
- فقط `DatabaseConnectionSettingsStore` مسیر `LocalApplicationData/TarazAva/Database` را می‌خواند/می‌نوشت (`database-settings.json`، `keys/`، `setup-token.txt`)؛ `ApplicationName=TarazAva`، Purpose `TarazAva.DatabaseConnectionSettings.v1`.
- پیش‌فرض فرم: سرور `.\SQLSERVER2025`، دیتابیس `TarazAvaDb`. Log شروع «Taraz Ava requires first-run database setup…».
- متغیرهای `TARAZAVA_CONNECTIONSTRING` (ابزار طراحی EF) و `TARAZAVA_TEST_CONNECTIONSTRING` (`scripts/apply_migrations_to_testdb.sh`).
- هیچ اسکریپت، CI یا Publish مسیر `TarazAva\Database` را نمی‌خواند (طبق بررسی Agent معمار).

### جدول بررسی مسیر قدیمی
| محل | کاربرد | نوع داده | وابسته به حساب؟ | اثر بر نصب | اقدام |
|---|---|---|---|---|---|
| `…/TarazAva/Database/database-settings.json` | Connection String رمزشده | ciphertext | بله | نصب‌های موجود | فقط‌خواندنی؛ مهاجرت؛ هرگز حذف/تغییر |
| `…/TarazAva/Database/keys/` | حلقه کلید | XML (Windows: DPAPI) | بله | بدون آن رمزگشایی ممکن نیست | فقط‌خواندنی؛ کلید جدید ساخته می‌شود |
| `…/TarazAva/Database/setup-token.txt` | کد یک‌بارمصرف | متن | خیر | موقت | در مسیر جدید ساخته می‌شود |
| `ApplicationName=TarazAva`، Purpose `…v1` | مشتق‌سازی کلید | ثابت کد | — | کپی‌کردن کلید بدون نام کمکی نمی‌کند | `Farano` و `…v2`؛ خواندن قدیمی با نام قدیمی |
| `docs/DATABASE.md`، `docs/SECURITY.md` | مستندات | متن | — | منسوخ | اصلاح شد |
| `TARAZAVA_*` | ابزار EF و اسکریپت آزمون | Connection String | — | ماشین‌های توسعه | `FARANO_*` با fallback به نام قدیمی |
| `TarazAvaDb`، `TarazAva_Test` | پیش‌فرض‌ها | نام | — | فقط توسعه | `Farano`، `Farano_Test` |
| Log «Taraz Ava» | پیام شروع | رشته | — | ندارد | «Farano» |

## خلاصه تغییرات
- **نام محصول:** ثابت‌ها در `SettingsNames.cs` (`FaranoSettingsNames`، `LegacySettingsNames`). اکنون «Farano»؛ **املای نهایی با «Faranou» (Cookie، API، `FARANOU_*`) ناهمخوان و `NeedsApproval` است**؛ در بقیه مخزن تغییر نام داده نشد.
- **سیاست ذخیره‌سازی:** `Farano:Settings:Directory` (`Farano__Settings__Directory`) وگرنه `LocalApplicationData/Farano/Settings`؛ absolute، بیرون پوشه برنامه، بدون fallback به پوشه برنامه، خطای شروع با نام کلید در نبود `LocalApplicationData`. فایل‌ها: `database-connection.json` (`version 2`)، `keys/`، `backups/` (۱۰ آخر)، `migration.json`، `setup-token.txt`، `.lock`.
- **رمزنگاری و مهاجرت:** `ApplicationName=Farano`، Purpose `Farano.DatabaseConnectionSettings.v2`، حلقه کلید تازه؛ خواننده قدیمی فقط‌خواندنی؛ مهاجرت اتمی با بازخوانی و `migration.json` (SHA-256).
- **حالت‌ها:** Configured / Setup / Blocked؛ ده وضعیت Store (`NotConfigured`، `Loaded`، `LoadedFromLegacy`، `MigratedFromLegacy`، `Conflict`، `Corrupt`، `Undecryptable`، `UnsupportedVersion`، `AccessDenied`، `LegacyUnreadable`)؛ ماتریس کامل در `DATABASE_SETUP.md` بخش ۵.
- **فرمان:** `dotnet Server.dll --settings status` و `--settings migrate [--replace-current|--keep-current]`؛ کد خروج ۰/۱/۲ (`SettingsCommand.cs`).
- **پس از بازبینی‌های مستقل (دور ۲ و ۳):** فهرست مجاز نام دیتابیس (حرف/نشانه/رقم، `_`، `-`، `.`/فاصله میانی؛ رد `\ / : * ? " < > | ; ' [ ]`، نویسه‌های کنترلی/قالب‌بندی از جمله ZWNJ (U+200C، دسته Cf)، نقطه/فاصله در ابتدا یا انتها، `..`؛ حداکثر ۱۲۳ نویسه چون `<name>_log` باید در ۱۲۸ جا شود)، NFKC و فهرست نام سیستمی، و بررسی مرجع سمت سرور `DB_ID(@name)` بین ۱ تا ۴ در `master` و `DB_ID()` > ۴ پس از اتصال؛ کدهای خطای تازه `SystemDatabaseNotAllowed`، `AlreadyConfigured`، `DatabaseNewerThanApplication` (وضعیت دیتابیس `NewerThanApplication`)؛ هشدارهای `WindowsAclNotApplied`، `SetupTokenNotDeleted` و legacy-unreadable؛ دروازه یک‌بارمصرف (`StartupLoad` ثابت؛ `Save` وضعیت را زیر قفل دوباره می‌سنجد)؛ مراحل پس از آزمون اتصال با `CancellationToken.None`؛ مهلت `CREATE DATABASE` ۱۲۰ ثانیه؛ `Save` مقدار `LastLoad` را به‌روز می‌کند؛ کد راه‌اندازی بازتاب داده نمی‌شود؛ پاک‌سازی فقط `.tmp` خود فایل‌ها؛ `chmod` فقط برای پوشه‌های ساخته‌شده؛ مقایسه مسیر واقعی (لینک) و ممنوعیت `WebRoot`؛ `HardenWindowsAcl` (اجرا نشده)؛ فیلدهای SQL فقط در حالت Windows سمت سرور پنهان‌اند و بدون JS قابل استفاده‌اند؛ رفع اشکال UX (blur کلیک ارسال را می‌بلعید؛ با E2E آشکار شد).
- **راه‌اندازی:** مراحل `Ready … Completed/Failed`، نتیجه هر مرحله، شناسه پیگیری ۱۲ hex، طبقه‌بندی خطا (`DatabaseSetupErrorCatalog`)، وضعیت دیتابیس (`Missing/Empty/UpToDate/PartiallyMigrated/ForeignOrLegacy`)، هرگز `DROP`، یک راه‌اندازی در لحظه؛ 1801 خطا نیست؛ رد اتصال Linux (Number 0 با Class ≥ 20) = `ServerUnreachable`.
- **UI:** Razor حفظ شد؛ فقط دارایی محلی، RTL، نمایش/پنهان رمز، خطای هر فیلد، جلوگیری از ارسال مضاعف، JS سازگار با CSP.
- **متغیرها:** `TARAZAVA_CONNECTIONSTRING`→`FARANO_CONNECTIONSTRING`، `TARAZAVA_TEST_CONNECTIONSTRING`→`FARANO_TEST_CONNECTIONSTRING` (نام قدیمی fallback).

## فایل‌های مهم تغییرکرده
کد (از `git status` در زمان تحویل):
- `Server/Infrastructure/DatabaseSetup/DatabaseConnectionSettingsStore.cs`، `DatabaseConnectionSetupService.cs` — بازنویسی Store و سرویس
- جدید: `DatabaseSettingsLoadResult.cs`، `DatabaseSettingsLocation.cs`، `DatabaseSetupErrorCatalog.cs`، `DatabaseSetupModels.cs`، `SettingsCommand.cs`، `SettingsNames.cs`
- `Server/Program.cs` — بارگذاری Store، حالت‌ها، فرمان `--settings`، Log
- `Server/Pages/DatabaseSetup.cshtml`، `DatabaseSetup.cshtml.cs`، `Server/wwwroot/css/database-setup.css`، `Server/wwwroot/js/database-setup.js` — فرم و رفع علت پیام عمومی
- `Infrastructure/Persistence/DesignTimeDatabaseContextFactory.cs`، `scripts/apply_migrations_to_testdb.sh` — نام متغیرها
- `Infrastructure/Persistence/DatabaseContext.cs`، `scripts/ef_model.py` — فقط متن توضیح «Taraz Ava» ← «Farano» (تک‌خطی)
- `tests/e2e/specs/faranou.spec.mjs` — تست صفحه راه‌اندازی به نشانه‌گذاری جدید به‌روز شد
- تست‌ها: `DatabaseSettingsLocationTests.cs`، `DatabaseSettingsStoreTests.cs`، `DatabaseSetupServiceTests.cs`، `DatabaseSetupSqlServerTests.cs`، `DatabaseSetupPageTests.cs`، `DatabaseSetupPageSqlServerTests.cs`، `DatabaseSetupTestSupport.cs` (جدید)؛ `DatabaseSetupValidationTests.cs`، `DatabaseDependencyInjectionTests.cs`، `DatabaseModelTests.cs` (تغییر)

مستندات (این وظیفه): `docs/DATABASE_SETUP.md` (جدید)، `docs/DATABASE.md`، `docs/SECURITY.md`، `docs/DEPLOYMENT.md`، `docs/DEVELOPMENT.md`، `docs/TESTING.md`، `docs/database-migrations.md`، `docs/DECISIONS.md` (D-036 و شماره بعدی D-037)، `docs/INDEX.md`، `docs/PROJECT_STATUS.md`، `docs/REPOSITORY_AUDIT.md` (AUD-2026-10-23-01..04 و توضیح AUD-13)، `docs/WORK_LOG.md`، `docs/AGENT_COORDINATION.md`، `docs/AGENT_START_PROMPT.md`، `docs/prompts/CODEX_PROMPT.md`، `docs/prompts/COPILOT_PROMPT.md` (فقط اصلاح «ADR بعدی D-037»).

## تصمیم‌های فنی
- **D-036** (`NeedsApproval`): محل، رمزنگاری و مهاجرت تنظیمات؛ گزینه‌های ردشده: ProgramData پیش‌فرض، DPAPI سطح ماشین، کپی حلقه کلید، fallback به پوشه برنامه، بازیابی خودکار از `backups/`، حذف فایل قدیمی. بازگشت: فایل قدیمی دست‌نخورده؛ نسخه قدیمی می‌خواندش؛ بازنویسی توسط آن ← `Conflict`.
- سرور پیش‌فرض خالی: هر مقدار پیش‌فرضی جایی غلط است (نمونه نام‌دار محلی، `localhost` کانتینر، SQL Browser در Linux) و خالی فوراً با پیام روشن رد می‌شود.
- پیش‌فرض `LocalApplicationData` نه `ProgramData` (دلیل: D-036).

## سیاست هر نوع استقرار
(کامل: `DATABASE_SETUP.md` بخش ۴ و `DEPLOYMENT.md` بخش ۹؛ **روی Windows اجرا نشده**)
- ماشین محلی: پیش‌فرض. Windows Service: `LocalSystem` = `systemprofile`؛ حساب اختصاصی/مجازی ترجیح دارد؛ DPAPI وابسته به حساب. IIS: «Load User Profile» یا Connection String از Configuration؛ هویت App Pool. سرور مشتری با پوشه اشتراکی: UNC پذیرفته می‌شود (کد هشدار نمی‌دهد)، ACL با اپراتور، کلید DPAPI فقط همان ماشین/حساب. ابر/کانتینر: Connection String از Configuration غالب است و Store ساخته نمی‌شود. چند نصب زیر یک حساب: مسیر جدا با کلید.

## مهاجرت و رفتار شکست
- مهاجرت: رمزگشایی قدیمی ← محافظت جدید ← نوشتن اتمی + بازخوانی ← `migration.json`. فایل قدیمی هرگز حذف/تغییر نمی‌شود.
- شکست: داده قدیمی حفظ؛ پیام قابل اقدام؛ Log بدون راز؛ موفقیت فقط پس از ذخیره؛ نوشتن ناموفق وضعیت قبلی را برمی‌گرداند.
- `Conflict`: اجرا با جدید + هشدار هر شروع تا اپراتور با `--settings migrate` تصمیم بگیرد.

## علت ریشه‌ای «اطلاعات فرم را بررسی کنید»
دو علت، هر دو با اجرای واقعی تأیید شده (گزارش Agent پیاده‌ساز):
1. `UserName`/`Password` غیرقابل‌null با `Nullable enable` ← `[Required]` ضمنی (تست متادیتای MVC: `IsRequired=True`)؛ Windows Authentication با فیلدهای پنهان خالی همیشه رد می‌شد.
2. فرم Bootstrap حساب Master یک `[BindProperty]` دوم است و `[Required]` آن در هر POST فرم دیتابیس `ModelState` را نامعتبر می‌کرد (مشاهده: خطاهای `Username`/`Password`/`PasswordConfirmation`، ۵ پیام)؛ حتی SQL Authentication رد می‌شد.

## خطاهای دیگر کشف‌شده
- خطای کد راه‌اندازی زیر کلید `SetupToken` ذخیره می‌شد نه `Input.SetupToken`؛ هرگز نمایش داده نمی‌شد.
- باگ تلاش مجدد: دیتابیس ساخته‌شده با Migration شکست‌خورده در تلاش بعد «غیرخالی» فرض و Migration رد و تنظیمات ذخیره می‌شد (Schema نیمه‌کاره). اکنون با وضعیت‌های دیتابیس جدا می‌شود. (آزمون با اعمال دستی اولین Migration، نه شکست تزریق‌شده.)
- **E2E قدیمی ناسازگار (AUD-2026-10-23-01، حل‌شد):** به `#authMode`/`#sqlUserField` ارجاع می‌داد؛ به نشانه‌گذاری جدید به‌روز شد و ۱۸ از ۱۸ موفق بود. همان E2E اشکال UX «blur کلیک ارسال را می‌بلعد» را آشکار کرد (اصلاح شد).
- **F1 (Medium، امنیتی):** نام هم‌ریخت دیتابیس سیستمی (مثل «ｍａｓｔｅｒ») با آزمایش تأیید شد؛ اصلاح با NFKC + `DB_ID` سمت سرور. F2 (Medium): ACL پوشه سفارشی/کد راه‌اندازی در Windows ← `HardenWindowsAcl`.

## بررسی و آزمون
اجرای Lead روی کد نهایی (2026-10-23، SDK 10.0.402، Ubuntu cloud، root):

| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| `dotnet build Template.sln -c Release` | موفق، ۰ خطا | |
| `dotnet test Template.sln --no-build -c Release` با SQL Server 2022 واقعی (کانتینر `scripts/dev/test-sqlserver.sh`) | ۴۸۹ تست، ۴۸۷ موفق، ۰ ناموفق، ۲ Skip | Skip: دو تست مبتنی بر `chmod` (AccessDenied/LegacyUnreadable) که با root قابل اجرا نیستند |
| همان بدون SQL Server | ۴۶۹ موفق، ۲۰ Skip | گزارش Agent پیاده‌ساز؛ Lead دوباره اجرا نکرد |
| E2E: `publish.sh` ← `--migrate` ← seed ← Playwright | ۱۸ موفق، ۰ ناموفق | تست صفحه راه‌اندازی به نشانه‌گذاری جدید به‌روز شد |
| ClientApp: typecheck، `npm test`، build، `check:offline` | موفق؛ ۴۸ از ۴۸ تست | |
| `python3 scripts/check_static_assets.py` | PASS | |
| `git diff --check` | تمیز | |
| آزمون جهش (Lead) | با غیرفعال‌کردن اصلاح `ModelState` ۴ از ۱۵ تست `DatabaseSetupPageTests` شکست خوردند؛ پس از بازگردانی ۱۵ از ۱۵ | |
| بررسی دستی Chromium (دسکتاپ ۱۲۸۰، موبایل ۳۷۵) | بدون خطای Console/CSP، بدون درخواست خارجی، بدون اسکرول افقی؛ خطای کلاینت هر فیلد؛ نمایش/پنهان رمز؛ POST واقعی به میزبان در دسترس‌نبود ← `ServerUnreachable` + شناسه پیگیری + فهرست مراحل | بدون JS فیلدهای SQL قابل استفاده‌اند (Agent پیاده‌ساز) |
| **اجرا نشد** | Windows (DPAPI، Service، IIS، UNC، `HardenWindowsAcl` — فقط کامپایل)؛ Login SQL منقضی/غیرفعال (18470/18487/18488 فقط با جدول خالص)؛ CI؛ دو POST هم‌زمان روی HTTP (دروازه سرویس تست واحد دارد)؛ POST بوت‌استرپ Master روی HTTP؛ شکست تزریق‌شده وسط Migration | |

## بازبینی مستقل
- `code-reviewer` (sonnet)، یک دور: بدون High. Medium: M1 املا (عمداً حل‌نشده، تصمیم مالک، `NeedsApproval`)، M2 اندازه PR (در یک PR نگه داشته شد چون تغییر نام کار حلقه کلید/مهاجرت را الزامی می‌کند؛ تقسیم پیشنهاد شده)، M3 لغو درخواست حین تغییر، M4 وضعیت کهنه پس از ذخیره، M5 پیام دیتابیس جدیدتر، M6 placeholder مستندات. Low: L1..L10. اصلاح‌شده: M3، M4، M5، L1 (بخشی)، L2، L4..L10.
- `security-reviewer` (opus)، دور ۱: F1 Medium (دور زدن نام سیستمی با نویسه هم‌ریخت، با آزمایش تأیید شد)، F2 Medium (ACL Windows)، F3..F7 Low/Info. دور ۲ (بازبینی مجدد): بدون Critical/High/Medium؛ N1 (نام دیتابیس ← نام فایل؛ فهرست مجاز)، N2 (سخت‌سازی ACL نباید استثنا بپرتاباند)، N3 (وضعیت ثابت شروع + بررسی دوباره در `Save`) Low — همگی در دور ۳ اصلاح شد. اصلاح‌شده: F1..F5، N1..N3، I1، I2.
- **عمداً اصلاح نشد:** M1 (املا)؛ M2؛ F6 (مستندسازی: `--settings` با حساب سرویس؛ `status` ممکن است با چرخش کلید بنویسد)؛ F7 (صفحه مسیر پوشه سرور را در Setup/Blocked به هر کس که به صفحه می‌رسد نشان می‌دهد؛ مستند شد، نمایش فقط پس از کد پیشنهاد است)؛ هشدار UNC در کد (فقط مستند)؛ `DatabaseSetupStage.Failed` و `DatabaseSetupDatabaseState.Missing` به‌عنوان مقدار قرارداد بی‌استفاده ماندند؛ I3/I5 (ملاحظه SSRF برای دارنده کد در `SECURITY.md` مستند شد؛ محدودیت نرخ حدس کد وجود ندارد ولی کد ۱۹۲ بیتی است؛ حلقه کلید Linux در rest رمز نیست و با `700` محافظت می‌شود).
- `architect` طرح را داد و روی Diff نهایی **دوباره اجرا نشد**.

## امنیت، داده و سازگاری
- Connection String در Log/پیام/`ToString`/خروجی `--settings` نیست. `TrustServerCertificate` فقط اعتبارسنجی گواهی را خاموش می‌کند (MITM) و برای محیط محلی/آزمایشی است؛ `Encrypt` همیشه فعال.
- بدون Migration دیتابیس؛ بدون تغییر Schema. هیچ `DROP` اجرا نمی‌شود.
- Windows: `HardenWindowsAcl` (کاربر جاری، SYSTEM، Administrators؛ بدون وراثت؛ بدون پرتاب استثنا) — اجرا نشده، فقط کامپایل شده.
- دور زدن نام دیتابیس سیستمی با نام هم‌ریخت (F1) اصلاح شد. SSRF برای دارنده کد راه‌اندازی و نبود محدودیت نرخ برای حدس کد: پذیرفته‌شده و مستند.
- نصب‌های موجود: فایل `TarazAva` در اولین شروع مهاجرت می‌شود؛ حساب ویندوز باید همان قبلی باشد.

## ریسک‌ها و کارهای باقی‌مانده
1. تأیید D-036 و املای Farano/Faranou (`NeedsApproval`).
2. باز کردن PR و دیدن CI (هنوز باز نشده)؛ تصمیم درباره تقسیم PR (M2).
3. آزمون واقعی Windows (DPAPI، Service، IIS، UNC، `HardenWindowsAcl`) و Login SQL منقضی/غیرفعال.
4. `AddDataProtection` سراسری (AUD-2026-10-23-02).
5. **نیازمند تأیید صریح مالک برای تغییر:** `AGENTS.md` و `CLAUDE.md` هنوز «ADR بعدی D-036» می‌گویند؛ چون D-036 ثبت شد باید D-037 شوند. این دو فایل تغییر نکردند.
6. هشدار UNC در کد وجود ندارد (AUD-2026-10-23-04)؛ نمایش مسیر پوشه سرور در صفحه راه‌اندازی (F7)؛ محدودیت نرخ حدس کد.
7. دو POST هم‌زمان روی HTTP و POST بوت‌استرپ Master روی HTTP اجرا نشد.

## Agentهای به‌کار‌رفته و توکن
اعداد گزارش ابزار Agent یا تخمین‌اند، نه اندازه‌گیری مستقل.

| Agent | کار | توکن |
|---|---|---|
| `architect` | طرح معماری | ۷۵٬۱۲۷ |
| `backend-developer` | پیاده‌سازی و تست (۳ دور) | ۲۷۰٬۴۷۶ (گزارش ابزار) |
| `documentation-engineer` | مستندات، دور اول | ۲۳۷٬۳۶۵ |
| `qa-engineer` | تست و Regression | ۱۳۵٬۱۱۵ |
| `code-reviewer` | بازبینی Diff | ۱۸۴٬۴۵۸ |
| `security-reviewer` | بازبینی امنیتی دور ۱ + بازبینی مجدد | ۱۲۰٬۸۸۲ + ۹۴٬۸۷۷ |
| Lead | هماهنگی، اجرای نهایی | نامشخص (ابزار گزارش نمی‌دهد) |

جمع Agentهای فوق حدود ۱٫۱ میلیون توکن. **ارزیابی تقسیم کار:** برای ایزولاسیون و بازبینی مستقل ارزش داشت (بازبینی‌ها یک دور زدن امنیتی واقعی F1 و یک اشکال وضعیت کهنه را یافتند) ولی هزینه‌اش حدود ۱٫۱ میلیون توکن Agent بود؛ هزینه/فایده فقط برای همین کار ثبت می‌شود، نه حکم کلی.

## شناسه‌های Git
- Branch: `feature/database-setup-redesign`
- Commit: `2018b11` (اصلی) + Commit اصلاح Smoke (در PR)
- Pull Request: #59 (Merge نشده)
- CI: Job `publish` در اجرای push به‌خاطر شرایط رقابتی قدیمی در `scripts/smoke-published-app.sh` شکست خورد (`curl | grep -q` زیر `pipefail`؛ ۱۱ از ۴۰۰ محلی)؛ در همین PR با گرفتن بدنه در متغیر اصلاح شد (۰ از ۴۰۰). این تنها تغییر خارج از جریان راه‌اندازی است و قابل جدا کردن است.
