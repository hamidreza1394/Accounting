# راه‌اندازی اولیه پایگاه داده و نگهداری تنظیمات اتصال (فرانو)

> **وضعیت (2026-10-23):** `Implemented` در شاخه `feature/database-setup-redesign` (ادغام‌نشده؛ Commit/PR ثبت نشده). تصمیم معماری D-036 `NeedsApproval` است. `Verified` در سطح اجرای محلی Lead (SDK 10.0.402، کانتینر SQL Server 2022): ۴۸۹ تست .NET، ۴۸۷ موفق، ۰ ناموفق، ۲ Skip؛ E2E ۱۸ از ۱۸؛ جزئیات بخش ۱۲. CI هنوز باز نشده است. رفتار روی Windows (DPAPI، Windows Service، IIS، UNC و کد `HardenWindowsAcl`) **اجرا نشده** (کد فقط کامپایل شده).
>
> پیش‌تر مسیر تنظیمات در `docs/DATABASE.md` (Issue #22) به‌صورت کوتاه آمده بود؛ جزئیات اکنون اینجاست. تصمیم و گزینه‌های ردشده: D-036 در `docs/DECISIONS.md`.

## ۱. دامنه و نام محصول
- این سند فقط مسیر **first-run** (`/DatabaseSetup`)، فایل تنظیمات اتصال و دستور `--settings` را پوشش می‌دهد. Connection String صریح از Configuration (User Secrets، متغیر محیطی، `appsettings`) همچنان بر فایل محلی مقدم است و در آن حالت Store ساخته نمی‌شود (`Server/Program.cs`).
- در این مسیر نام محصول از «تراز آوا / TarazAva» به «فرانو / **Farano**» تغییر کرد (ثابت‌ها فقط در `Server/Infrastructure/DatabaseSetup/SettingsNames.cs`: `FaranoSettingsNames` و `LegacySettingsNames`).
- **انحراف نام‌گذاری / پرسش باز برای کارفرما (`NeedsApproval`):** در بقیه مخزن برند «**Faranou**» است (Cookie مانند `.Faranou.Authentication`، `MapFaranouApi`، متغیرهای `FARANOU_*`، دیتابیس‌های آزمون `FaranouTests*`). کارفرما در این جریان «Farano» را خواسته است. املای نهایی (Farano یا Faranou) باید تأیید شود؛ در این تغییر چیزی خارج از جریان راه‌اندازی تغییر نام نیافت.

## ۲. ساختار اولیه (پیش از این تغییر)
از `git diff` و کد قبلی (`HEAD`):
- تنها مصرف‌کننده زمان اجرای مسیر قدیمی `DatabaseConnectionSettingsStore` بود: `LocalApplicationData/TarazAva/Database/` شامل `database-settings.json`، `keys/` و `setup-token.txt`.
- Data Protection با `ApplicationName = "TarazAva"` و Purpose `TarazAva.DatabaseConnectionSettings.v1`.
- پیش‌فرض‌های فرم در `HEAD`: سرور `.\SQLSERVER2025` و دیتابیس `TarazAvaDb` (`InputModel` در `DatabaseSetup.cshtml.cs`). اکنون: سرور خالی، دیتابیس `Farano`.

### جدول بررسی مسیر قدیمی
| محل | کاربرد | نوع داده | وابسته به حساب کاربری؟ | اثر بر نصب | اقدام |
|---|---|---|---|---|---|
| `LocalApplicationData/TarazAva/Database/database-settings.json` | Connection String رمزشده | ciphertext Data Protection | بله (کلید با DPAPI در Windows) | نصب‌های موجود فایل را دارند | فقط خواندنی؛ مهاجرت به مسیر جدید؛ هرگز حذف/تغییر نمی‌شود |
| `…/TarazAva/Database/keys/` | حلقه کلید Data Protection | XML (در Windows با DPAPI) | بله | بدون آن فایل قدیمی رمزگشایی نمی‌شود | فقط خواندنی (`ReadOnlyXmlRepository`)؛ کلید جدید در Farano ساخته می‌شود |
| `…/TarazAva/Database/setup-token.txt` | کد یک‌بارمصرف فرم | متن | خیر | موقت | در مسیر جدید ساخته می‌شود |
| `ApplicationName` = `TarazAva` و Purpose `…v1` | مشتق‌سازی کلید رمزنگاری | ثابت کد | — | بخشی از مشتق‌سازی کلید است؛ کپی‌کردن کلیدها بدون همین نام کمکی نمی‌کند | نام جدید `Farano`، Purpose `Farano.DatabaseConnectionSettings.v2`؛ خواندن قدیمی با نام قدیمی |
| `docs/DATABASE.md` و `docs/SECURITY.md` | توضیح مسیر | مستند | — | مستند منسوخ | اصلاح شد |
| متغیرهای `TARAZAVA_CONNECTIONSTRING`، `TARAZAVA_TEST_CONNECTIONSTRING` | ابزار طراحی EF و `scripts/apply_migrations_to_testdb.sh` | Connection String (محرمانه) | — | اسکریپت‌ها/ماشین‌های توسعه | نام جدید `FARANO_*`؛ نام قدیمی هنوز به‌عنوان جایگزین خوانده می‌شود |
| نام دیتابیس‌های پیش‌فرض/نمونه (`TarazAvaDb`، `TarazAva_Test`، پیش‌فرض ابزار طراحی EF) | پیش‌فرض‌ها | نام | — | فقط توسعه | `Farano`، `Farano_Test` |
| متن Log «Taraz Ava requires first-run…» در `Server/Program.cs` | پیام راه‌اندازی | رشته | — | ندارد | «Farano» |

نتیجه جستجو: هیچ اسکریپت، CI یا Publish مسیر `TarazAva\Database` را نمی‌خواند (طبق گزارش بررسی Agent معمار؛ فهرست بالا کامل‌ترین یافته‌ها است).

## ۳. سیاست جدید محل نگهداری
ترتیب (`DatabaseSettingsLocation.Resolve`):
1. کلید `Farano:Settings:Directory` (متغیر محیطی `Farano__Settings__Directory`).
2. وگرنه `LocalApplicationData/Farano/Settings`.

قواعد:
- مسیر سفارشی باید **absolute** باشد و داخل پوشه برنامه (`AppContext.BaseDirectory`) یا ریشه محتوا (`ContentRootPath`) نباشد (با به‌روزرسانی برنامه پاک می‌شود). هر دو خطا با پیام دوزبانه و نام کلید برمی‌گردند.
- مقایسه با پوشه برنامه، ریشه محتوا و `WebRoot` روی **مسیر واقعی** انجام می‌شود (پیوند نمادین/Junction هر جزء موجود دنبال می‌شود؛ `ResolveRealPath`)، پس لینک نمی‌تواند پوشه را به پوشه برنامه یا `wwwroot` ببرد.
- اگر `LocalApplicationData` خالی باشد و کلید هم تعیین نشده باشد، برنامه با خطای شروع که نام کلید را می‌گوید متوقف می‌شود؛ **هرگز به پوشه برنامه برنمی‌گردد.**
- مسیر UNC از نظر `Path.IsPathFullyQualified` معتبر است و **کد برای آن هشدار جدا نمی‌دهد** (پیاده‌سازی نشده)؛ ACL پوشه اشتراکی را اپراتور باید محدود کند (توصیه در بخش ۴).

### فایل‌ها (زیر پوشه تنظیمات)
| فایل/پوشه | محتوا |
|---|---|
| `database-connection.json` | `{ "version": 2, "protectedConnectionString", "savedAtUtc", "origin" }`؛ `origin` = `Setup` یا `MigratedFromTarazAva` |
| `keys/` | حلقه کلید Data Protection جدید (Windows: DPAPI) |
| `backups/` | نسخه‌های پشتیبان `database-connection.<UTC>.json`؛ آخرین ۱۰ نسخه نگه داشته می‌شود |
| `migration.json` | نشانه مهاجرت: `legacyPath`، `legacySha256`، `migratedAtUtc` |
| `setup-token.txt` | کد یک‌بارمصرف فرم؛ پس از ذخیره موفق حذف می‌شود |
| `.lock` | قفل انحصاری نوشتن (انتظار تا ۵ ثانیه) |

- نوشتن اتمی (پاک‌سازی فقط `.tmp`های خودِ همین دو فایل: `database-connection.json.tmp` و `migration.json.tmp`): فایل موقت `.tmp` با `WriteThrough` ← `File.Move(overwrite)` ← بازخوانی و مقایسه؛ در ناهمخوانی وضعیت قبلی بازگردانده می‌شود. فایل موجود (حتی ناخوانا) پیش از جایگزینی به `backups/` کپی می‌شود.
- Unix: مجوز پوشه `700` و فایل‌ها `600` (`File.SetUnixFileMode`)؛ `chmod` پوشه فقط برای پوشه‌هایی است که خود برنامه ساخته (پوشه موجود اپراتور تغییر نمی‌کند). Windows: `HardenWindowsAcl` وراثت را قطع می‌کند و FullControl فقط به کاربر جاری، SYSTEM و Administrators می‌دهد؛ **هرگز استثنا پرتاب نمی‌کند** و شکست آن با هشدار `WindowsAclNotApplied` گزارش می‌شود. **این کد روی Windows اجرا نشده (فقط کامپایل شده).** در Linux حلقه کلید در حالت rest رمز نیست و با مجوز `700` محافظت می‌شود.
- **جدا از فایل‌های SQL Server:** این تنظیمات هیچ ربطی به `.mdf/.ldf` ندارند. برنامه فایل داده را نمی‌سازد، جابه‌جا یا حذف نمی‌کند؛ `CREATE DATABASE` بدون مسیر فایل اجرا می‌شود و SQL Server از پوشه داده خودش استفاده می‌کند.
- پیش‌فرض عمداً `LocalApplicationData` است، نه `ProgramData`: `ProgramData` برای همه کاربران ماشین خواندنی است و راز رمزشده با کلید وابسته به حساب را در مسیر مشترک می‌گذارد؛ تصمیم و دلیل در D-036.

## ۴. راهنمای هر نوع استقرار
اجرا نشده روی Windows؛ این بخش از رفتار کد و مستندات رسمی نتیجه‌گیری شده است، نه آزمون واقعی.

| استقرار | توصیه |
|---|---|
| یک ماشین محلی (توسعه/دسکتاپ) | پیش‌فرض کافی است. فرم راه‌اندازی مسیر دقیق را نشان می‌دهد |
| Windows Service | با `LocalSystem` پوشه `LocalApplicationData` به پروفایل `systemprofile` می‌رود. حساب اختصاصی/مجازی ترجیح دارد و مسیر را صریح با `Farano__Settings__Directory` بدهید. DPAPI وابسته به حساب است؛ تغییر حساب سرویس فایل را `Undecryptable` می‌کند |
| IIS | یا «Load User Profile» را برای Application Pool فعال کنید، یا Connection String را از Configuration/Secret Store بدهید (آنگاه Store ساخته نمی‌شود). هویت App Pool باید روی پوشه تنظیمات Write داشته باشد |
| سرور مشتری با پوشه اشتراکی | UNC پذیرفته می‌شود. ACL محدود (فقط حساب برنامه) وظیفه اپراتور است؛ کلیدهای DPAPI فقط روی همان ماشین و همان حساب قابل رمزگشایی‌اند، پس پوشه اشتراکی با چند ماشین کار نمی‌کند |
| ابر/کانتینر | Connection String را از Configuration بدهید؛ آنگاه فایل محلی هرگز خوانده/ساخته نمی‌شود. وجود فایل محلی پایدار فرض نشود |
| چند نصب زیر یک حساب | برای هر نصب مسیر جدا با `Farano__Settings__Directory` |

**فرمان `--settings`:** با همان حساب سرویس اجرا شود (DPAPI و مجوز پوشه وابسته به حساب‌اند)؛ `status` فایل تنظیمات را نمی‌نویسد، ولی ممکن است با چرخش کلید (key rotation) چیزی در `keys/` بنویسد.

**ریسک/پیگیری:** برنامه تنظیم سراسری `AddDataProtection` ندارد؛ Cookie ورود و Anti-forgery از حلقه کلید پیش‌فرض فریمورک استفاده می‌کنند. این خارج از دامنه این تغییر است و باید جداگانه (به‌ویژه برای چندنمونه، `docs/DEPLOYMENT.md` بخش ۵) بررسی شود.

## ۵. رمزنگاری و مهاجرت از TarazAva
- Data Protection جدید: `ApplicationName = Farano`، Purpose `Farano.DatabaseConnectionSettings.v2`، حلقه کلید تازه. در Windows کلیدها با `ProtectKeysWithDpapi()` (پیش‌فرض: کاربر جاری) محافظت می‌شوند.
- خواندن قدیمی فقط‌خواندنی: حلقه قدیمی با `ReadOnlyXmlRepository` خوانده می‌شود؛ پوشه قدیمی هرگز ساخته/نوشته نمی‌شود و کلید جدیدی که حلقه بخواهد اضافه کند فقط در حافظه می‌ماند.
- مهاجرت: رمزگشایی قدیمی ← محافظت با کلید جدید ← نوشتن اتمی + بازخوانی و تأیید ← `migration.json` با SHA-256 فایل قدیمی. فایل قدیمی هرگز حذف یا تغییر نمی‌کند (تست: مقایسه بایت‌به‌بایت).

### ماتریس رفتار (`DatabaseSettingsStore.Evaluate`، `DatabaseSettingsStatus`)
| وضعیت | شرط | نتیجه | نوشتن |
|---|---|---|---|
| `NotConfigured` | هیچ‌کدام وجود ندارد | حالت Setup | هیچ (Load)؛ Program فقط `setup-token.txt` می‌سازد |
| `Loaded` | فقط جدید معتبر | اتصال با جدید | هیچ |
| `MigratedFromLegacy` | فقط قدیمی معتبر (بار عادی) | مهاجرت خودکار؛ اتصال با مقدار قدیمی | `database-connection.json` (origin `MigratedFromTarazAva`) + `migration.json` |
| `LoadedFromLegacy` | فقط قدیمی معتبر و (فقط‌خواندن، یا پوشه جدید قابل نوشتن نیست) | اتصال با مقدار قدیمی؛ هشدار `LegacyMigrationPending` / `NewFolderNotWritable` | هیچ |
| `Loaded` | هر دو معتبر و برابر | اتصال با جدید | `migration.json` در صورت نبود (هشدار `MarkerNotWritten` اگر نشد) |
| `Loaded` | هر دو معتبر، متفاوت، و `migration.json` با هش فعلی فایل قدیمی برابر | تصمیم قبلی اپراتور؛ جدید | هیچ |
| `Conflict` | هر دو معتبر، متفاوت، بدون نشانه یا هش قدیمی تغییرکرده | **اجرا با جدید** + هشدار `LegacyConflict` در هر شروع | هیچ؛ هرگز بازنویسی نمی‌شود |
| `Loaded` + هشدار `LegacyUnreadable` | جدید معتبر، قدیمی ناخوانا | اتصال با جدید | هیچ |
| `LegacyUnreadable` | فقط قدیمی و خراب/رمزگشایی‌ناپذیر/بدون کلید | حالت Setup با تأیید جایگزینی | هیچ |
| `Corrupt` | جدید: JSON نامعتبر، `version` ناموجود یا کمتر از ۲، فیلد خالی | حالت Setup با تأیید جایگزینی؛ **هرگز به قدیمی برنمی‌گردد** | هیچ |
| `Undecryptable` | جدید: بدون کلید یا رمزگشایی ناموفق | حالت Setup با تأیید جایگزینی | هیچ |
| `UnsupportedVersion` | `version` > ۲ | **Blocked**؛ فرم نمایش داده نمی‌شود | هیچ |
| `AccessDenied` | پوشه/فایل خوانده نمی‌شود، یا `setup-token.txt` قابل نوشتن نیست | **Blocked** | هیچ |

هشدارها (کد ماشینی، `DatabaseSettingsWarnings`): `LegacyUnreadable`، `LegacyConflict`، `LegacyMigrationPending`، `NewFolderNotWritable`، `MarkerNotWritten`، `WindowsAclNotApplied`، `SetupTokenNotDeleted` (تنظیمات ذخیره شد ولی `setup-token.txt` پاک نشد؛ کد دیگر معتبر نیست). پس از ذخیره موفق `LastLoad` به‌روز می‌شود تا صفحه وضعیت کهنه نشان ندهد.

حالت‌ها: **Configured** (دارای Connection String: `Loaded`، `LoadedFromLegacy`، `MigratedFromLegacy`، `Conflict`)، **Setup** (`NotConfigured`، `Corrupt`، `Undecryptable`، `LegacyUnreadable`)، **Blocked** (`AccessDenied`، `UnsupportedVersion`). فایل ناخوانا هرگز صفحه ساده first-run نشان نمی‌دهد. جایگزینی نیازمند کد راه‌اندازی **و** تیک تأیید جایگزینی است و فایل پیشین ابتدا در `backups/` کپی می‌شود.

### فرمان اپراتور
```bash
dotnet Server.dll --settings status
dotnet Server.dll --settings migrate [--replace-current | --keep-current]
```
- `status` فقط‌خواندنی است (پوشه یا فایلی نمی‌سازد) و Connection String را چاپ نمی‌کند.
- `migrate` بدون پرچم، مهاجرت خودکار را اجرا می‌کند؛ در `Conflict` تصمیم می‌خواهد: `--replace-current` (مقدار قدیمی؛ فایل فعلی پشتیبان می‌شود) یا `--keep-current` (فقط نشانه را می‌نویسد). هر دو تکرارپذیرند.
- کد خروج: `0` = انجام شد/قابل استفاده (`Loaded`، `LoadedFromLegacy`، `MigratedFromLegacy`)؛ `1` = شکست یا تنظیمات ناخوانا؛ `2` = نیاز به تصمیم/راه‌اندازی (`Conflict`، `NotConfigured`، استفاده نادرست، هر دو پرچم هم‌زمان). اگر Connection String صریح در Configuration باشد، خروجی خطا و کد `2`.
- رفتار شکست: داده قدیمی حفظ می‌شود، پیام قابل اقدام (کد خطا) چاپ می‌شود، Log بدون راز است و موفقیت فقط پس از ذخیره‌شدن نمایش داده می‌شود.
- **بازگشت (Rollback):** فایل قدیمی دست‌نخورده است و نسخه قدیمی برنامه همچنان آن را می‌خواند. اگر نسخه قدیمی فایل قدیمی را بازنویسی کند، هش عوض می‌شود و رخداد `Conflict` تشخیص داده می‌شود (نه از دست رفتن بی‌صدا).

## ۶. جریان راه‌اندازی (`DatabaseConnectionSetupService`)
مراحل (`DatabaseSetupStage`): `Ready`، `Validating`، `TestingConnection`، `Connected`، `CreatingDatabase`، `RunningMigrations`، `SavingSettings`، `Completed`، `Failed`. هر مرحله نتیجه `Succeeded`/`Skipped`/`Failed` دارد. هر تلاش شناسه پیگیری ۱۲ نویسه hex (۶ بایت تصادفی) می‌گیرد که در پیام کاربر و Log می‌آید. فقط یک راه‌اندازی هم‌زمان اجرا می‌شود (`SemaphoreSlim`؛ دومی `SetupInProgress`). هر مرحله یک ردیف دارد (شکست جایگزین موفقیتِ همان مرحله می‌شود).

- **دروازه یک‌بارمصرف:** درخواست فقط وقتی اجرا می‌شود که کد راه‌اندازی هنوز معتبر باشد و وضعیت فایل‌ها با `StartupLoad` (وضعیت ثابت لحظه شروع) یکی باشد؛ وگرنه `AlreadyConfigured` و فایل معتبرِ ظاهرشده بازنویسی نمی‌شود. `Save` هم زیر قفل وضعیت را دوباره می‌سنجد (`expectedStatus`). کد راه‌اندازی در پاسخ بازتاب داده نمی‌شود.
- **لغو درخواست:** `CancellationToken` درخواست فقط آزمون اتصال را لغو می‌کند؛ ساخت دیتابیس، Migration و ذخیره با `CancellationToken.None` اجرا می‌شوند (بستن تب مرورگر نیمه‌کاره‌شان نمی‌کند). مهلت `CREATE DATABASE` ۱۲۰ ثانیه است.

- اتصال همیشه `Encrypt=Mandatory`، `ConnectTimeout=15`، `PersistSecurityInfo=false`، `ApplicationName=Farano` و ابتدا به `master` است. نام دیتابیس با پارامتر و `QUOTENAME` به SQL می‌رود.
- **فهرست مجاز نام دیتابیس** (چون SQL Server نام فایل‌های `.mdf` و `_log.ldf` را از نام می‌سازد): فقط حرف/نشانه/رقم (Unicode)، `_`، `-`، و `.` یا فاصلهٔ عادی در میانه؛ نویسه‌های `\ / : * ? " < > | ; ' [ ]`، نویسه کنترلی/قالب‌بندی (از جمله ZWNJ، U+200C، که دسته `Cf` است و رد می‌شود)، نقطه/فاصله در ابتدا یا انتها و `..` رد می‌شوند. حداکثر ۱۲۳ نویسه (چون `<name>_log` باید در ۱۲۸ جا شود). نام سیستمی با نرمال‌سازی NFKC و فهرست `master/model/msdb/tempdb` رد می‌شود (نام‌های هم‌ریخت مثل «ｍａｓｔｅｒ» که SQL Server روی دیتابیس سیستمی می‌نشاند؛ یافته F1 بازبینی امنیتی، با آزمایش تأیید شد).
- **بررسی سمت سرور (مرجع نهایی):** `DB_ID(@name)` در `master` اگر بین ۱ تا ۴ باشد ← `SystemDatabaseNotAllowed`؛ و پس از اتصال به دیتابیس `DB_ID()` اگر ≤ ۴ باشد ← همان خطا و هیچ تغییری داده نمی‌شود.
- پیش‌فرض فرم: دیتابیس `Farano`؛ **سرور عمداً خالی** (هر پیش‌فرضی جایی غلط است: نمونه نام‌دار محلی، `localhost` کانتینر، و SQL Browser در Linux فرق دارند؛ مقدار خالی فوراً با پیام روشن رد می‌شود).

### وضعیت دیتابیس موجود (`DatabaseSetupDatabaseState`)
| وضعیت | تعریف (`ClassifyDatabaseState`) | رفتار |
|---|---|---|
| `Missing` | دیتابیس وجود ندارد | با گزینه ساخت: `CREATE DATABASE` و سپس Migration؛ بدون گزینه: خطای `DatabaseMissing` |
| `Empty` | جدول تاریخچه ندارد/تهی است و جدول کاربر ندارد (یا همین الان ساخته شد) | Migration اجرا می‌شود |
| `UpToDate` | همه Migrationهای برنامه اعمال شده‌اند | فقط ذخیره تنظیمات |
| `PartiallyMigrated` | بخشی از Migrationها اعمال شده | بدون تیک «ادامه Migrationهای ناتمام» خطای `PendingMigrations`؛ با تیک Migration |
| `ForeignOrLegacy` | جدول کاربر بدون تاریخچه، یا تاریخچه‌ای که هیچ‌کدام از Migrationهای برنامه در آن نیست | `NotFaranoDatabase`؛ هیچ تغییری نمی‌دهد |
| `NewerThanApplication` | تاریخچه شامل برخی Migrationهای برنامه به‌علاوه شناسه‌های ناشناس (فرانوی جدیدتر) | `DatabaseNewerThanApplication`؛ هیچ تغییری نمی‌دهد؛ برنامه را ارتقا دهید |

`Missing` در عمل یک مقدار قرارداد است و از مسیر کد برگردانده نمی‌شود (نبود دیتابیس با `DatabaseMissing` یا ساخت پوشش داده می‌شود)؛ `DatabaseSetupStage.Failed` هم عمداً به‌عنوان مقدار قرارداد باقی مانده و استفاده نمی‌شود. هرگز `DROP` اجرا نمی‌شود. خطای 1801 (ساخت هم‌زمان توسط دیگری) خطا نیست؛ دیتابیس بازرسی می‌شود.

### طبقه‌بندی خطا (`DatabaseSetupErrorCatalog.Classify`)
| کد | شماره‌های SQL / شرط |
|---|---|
| `LoginFailed` | 18456 |
| `WindowsAuthNotTrusted` | 18452 |
| `LoginDisabledOrExpired` | 18470، 18487، 18488 |
| `CertificateNotTrusted` | `AuthenticationException` یا `SqlException` با «certificate» در پیام |
| `ServerUnreachable` | 53، 2، 10060، 11001، -1؛ همچنین Number = 0 با Class ≥ 20 (رد اتصال TCP روی Linux با SNI مدیریت‌شده) |
| `InstanceNotFound` | 26، 40 |
| `Timeout` | -2، 258، `TimeoutException` |
| `ConnectionClosed` | 233، 10054 |
| `DatabaseNoAccess` | 4060، 916 |
| `PermissionDenied` | 262، 229، 15247 |
| `DatabaseFileError` | 5170، 5133، 5120، 1802 |
| `DiskFull` | 1105، 9002 |
| `MigrationFailed` | هر خطای نامشخص در مرحله `RunningMigrations` |
| `SettingsAccessDenied` / `SettingsIoError` / `SettingsProtectionFailed` | در مرحله `SavingSettings`: `UnauthorizedAccessException` / `IOException` / `CryptographicException` |
| `SystemDatabaseNotAllowed` | `DB_ID` بین ۱ تا ۴ (نام هم‌ریخت با دیتابیس سیستمی) |
| `AlreadyConfigured` | کد راه‌اندازی مصرف شده یا وضعیت فایل‌ها نسبت به شروع عوض شده |
| `DatabaseNewerThanApplication` | وضعیت `NewerThanApplication` |
| `DatabaseMissing`، `PendingMigrations`، `NotFaranoDatabase`، `ValidationFailed`، `SetupInProgress`، `Unknown` | بر اساس منطق جریان بالا |

شماره‌های 18470/18487/18488 فقط با جدول خالص طبقه‌بندی پوشش داده شده‌اند، نه با Login واقعی منقضی/غیرفعال.

**ملاحظه SSRF:** دارنده کد راه‌اندازی می‌تواند سرور/پورت دلخواه را از میزبان آزمایش کند (اتصال خروجی از سمت سرور). برای تلاش‌های حدس کد راه‌اندازی محدودیت نرخ وجود ندارد؛ کد ۱۹۲ بیتی است. صفحه راه‌اندازی در حالت Setup/Blocked مسیرهای پوشه سرور را به هر کسی که به صفحه دسترسی دارد نشان می‌دهد (پیگیری: نمایش فقط پس از کد؛ `NeedsApproval`). صفحه راه‌اندازی را فقط روی شبکه مورد اعتماد در دسترس بگذارید.

### TrustServerCertificate
اتصال همیشه رمزنگاری‌شده است. گزینه «اعتماد به گواهی سرور» فقط **اعتبارسنجی گواهی سرور را خاموش می‌کند** و اتصال را در برابر جعل سرور (MITM) آسیب‌پذیر می‌کند؛ فقط برای SQL Server محلی/آزمایشی با گواهی خودامضا. در تولید گواهی معتبر نصب شود.

## ۷. علت ریشه‌ای پیام عمومی «اطلاعات فرم را بررسی کنید»
دو علت مستقل، هر دو با اجرای واقعی تأیید شده‌اند (طبق گزارش Agent پیاده‌ساز):
1. `UserName`/`Password` از نوع `string` غیرقابل‌null با `Nullable enable` بودند و MVC برایشان `[Required]` ضمنی می‌ساخت (تست متادیتای MVC: `IsRequired=True`). در حالت Windows Authentication فیلدها پنهان و خالی ارسال می‌شدند و فرم همیشه رد می‌شد.
2. فرم Bootstrap حساب Master یک `[BindProperty]` دوم است و فیلدهای `[Required]` آن در **هر** POST فرم دیتابیس `ModelState` را نامعتبر می‌کرد (مشاهده: خطاهای `Username`/`Password`/`PasswordConfirmation`، ۵ پیام)؛ پس حتی SQL Authentication هم رد می‌شد و تنها خروجی همان متن عمومی بود.

اصلاح: دو فیلد nullable شدند؛ Handler فقط کلیدهای `Input.*` را نگه می‌دارد؛ قواعد فیلد یک‌جا در `Validate` است و همه خطاها با هم می‌آیند. یافته‌های جانبی: خطای کد راه‌اندازی زیر کلید `SetupToken` ذخیره می‌شد نه `Input.SetupToken`، پس هرگز نمایش داده نمی‌شد؛ و باگ تلاش مجدد: اگر دیتابیس ساخته می‌شد ولی Migration شکست می‌خورد، تلاش بعد جدول `__EFMigrationsHistory` را می‌دید، دیتابیس را «غیرخالی» فرض می‌کرد، Migration را رد می‌کرد و تنظیمات را ذخیره می‌کرد (Schema نیمه‌کاره). اکنون وضعیت‌های بالا این را جدا می‌کنند. **محدودیت آزمون:** حالت ناتمام با اعمال دستی اولین Migration شبیه‌سازی شده (`PartialMigration_NeedsTheCheckbox_ThenRetryIsSafe`)، نه با شکست تزریق‌شده وسط Migration.

## ۸. رابط کاربری
- Razor حفظ شد؛ نیازی به تغییر فناوری نبود (D-001/D-011). فقط دارایی محلی (`fontiran.css`، `faranou-logo-mark.svg`)، RTL، نمایش/پنهان رمز، خطای هر فیلد، جلوگیری از ارسال مضاعف با متن بارگذاری، و JS سازگار با CSP (بدون inline).
- فیلدهای SQL فقط در حالت Windows در سمت سرور پنهان رندر می‌شوند (`hidden` از `SqlMode`) و JS در حالت Windows آن‌ها را `disabled` می‌کند؛ بدون JS فیلدهای SQL قابل استفاده‌اند (بررسی Agent پیاده‌ساز).
- **E2E (AUD-2026-10-23-01، حل‌شد):** تست «the first-run setup page works under the enforced CSP» در `tests/e2e/specs/faranou.spec.mjs` به نشانه‌گذاری جدید (`#Input_AuthenticationMode`، `[data-sql-only]`، `[data-toggle-password]`، `[data-submit]`، `[data-error-for]`) به‌روزرسانی شد و در اجرای Lead موفق بود (۱۸ از ۱۸). همین E2E یک اشکال واقعی UX را آشکار کرد: اعتبارسنجی هنگام blur کلیک ارسال را می‌بلعید؛ اصلاح شد.
- بررسی دستی Chromium (دسکتاپ ۱۲۸۰ و موبایل ۳۷۵) توسط Lead: بدون خطای Console/CSP، بدون درخواست خارجی، بدون اسکرول افقی، خطای کلاینت هر فیلد، نمایش/پنهان رمز، و POST واقعی به میزبان در دسترس‌نبود ← `ServerUnreachable` با شناسه پیگیری و فهرست مراحل.

## ۹. متغیرهای محیطی
| نام جدید | نام قدیمی (هنوز خوانده می‌شود) | کاربرد |
|---|---|---|
| `FARANO_CONNECTIONSTRING` | `TARAZAVA_CONNECTIONSTRING` | ابزار طراحی EF (`DesignTimeDatabaseContextFactory`) |
| `FARANO_TEST_CONNECTIONSTRING` | `TARAZAVA_TEST_CONNECTIONSTRING` | `scripts/apply_migrations_to_testdb.sh` |
| `Farano__Settings__Directory` | — | مسیر پوشه تنظیمات |

ترتیب خواندن ابزار طراحی: `FARANO_CONNECTIONSTRING` ← `TARAZAVA_CONNECTIONSTRING` ← `ConnectionStrings__ConnectionString` ← پیش‌فرض محلی `(localdb)\mssqllocaldb`، دیتابیس `Farano`. توجه: تست‌های SQL Server همچنان با `FARANOU_TEST_SQLSERVER` (با U) کنترل می‌شوند، نه `FARANO_*`.

## ۱۰. ریسک‌ها و کارهای باقی‌مانده
1. املای نهایی Farano/Faranou: `NeedsApproval` (بخش ۱).
2. رفتار روی Windows (DPAPI، Windows Service، LocalSystem، IIS App Pool، UNC، و `HardenWindowsAcl`) اجرا نشده.
3. `AddDataProtection` سراسری تنظیم نشده (بخش ۴؛ AUD-2026-10-23-02).
4. UNC بدون هشدار در کد؛ فقط توصیه مستند (AUD-2026-10-23-04).
5. `Conflict` هر شروع هشدار می‌دهد؛ تصمیم خودکار وجود ندارد (عمدی). بازگردانی خودکار از `backups/` پیاده‌سازی نشده (عمدی؛ D-036).
6. نمایش مسیر پوشه سرور در صفحه راه‌اندازی به هر کس که به صفحه می‌رسد؛ نبود محدودیت نرخ برای حدس کد (کد ۱۹۲ بیتی)؛ ملاحظه SSRF برای دارنده کد (بخش ۶).
7. Login SQL با رمز منقضی/غیرفعال (18470/18487/18488) فقط با جدول خالص طبقه‌بندی؛ POST هم‌زمان دوگانه روی HTTP و POST بوت‌استرپ Master روی HTTP اجرا نشده (دروازه سرویس تست واحد دارد).
8. CI هنوز باز نشده؛ اندازه PR بزرگ است (در یک PR نگه داشته شد چون تغییر نام کار حلقه کلید/مهاجرت را الزامی می‌کند؛ تقسیم پیشنهاد شده است).

## ۱۱. گزارش تغییرات
`docs/handoffs/ISSUE-UNNUMBERED-database-setup-redesign.md`.

## ۱۲. تست‌ها
فهرست فایل‌ها و قاعده Skip: `docs/TESTING.md` بخش «تست‌های راه‌اندازی پایگاه داده (بازطراحی)».

نتیجه اجرای Lead (2026-10-23، SDK 10.0.402، Ubuntu، root، SQL Server 2022 از `scripts/dev/test-sqlserver.sh`):
- `dotnet build Template.sln -c Release`: ۰ خطا.
- `dotnet test Template.sln --no-build -c Release` با SQL Server واقعی: ۴۸۹ تست، ۴۸۷ موفق، ۰ ناموفق، ۲ Skip (دو تست مبتنی بر `chmod` برای AccessDenied/LegacyUnreadable که با root قابل اجرا نیستند). بدون SQL Server طبق گزارش Agent پیاده‌ساز: ۴۶۹ موفق و ۲۰ Skip (Lead دوباره اجرا نکرد).
- E2E (`publish.sh`، `--migrate`، seed، Playwright): ۱۸ موفق، ۰ ناموفق.
- ClientApp: typecheck، `npm test` (۴۸ از ۴۸)، build و `check:offline` موفق. `scripts/check_static_assets.py` PASS. `git diff --check` تمیز.
- آزمون جهش (Lead): با غیرفعال‌کردن اصلاح `ModelState`، ۴ از ۱۵ تست `DatabaseSetupPageTests` شکست خوردند؛ پس از بازگردانی ۱۵ از ۱۵ موفق.

اجرا نشد: Windows (DPAPI، Service، IIS، UNC، `HardenWindowsAcl`)؛ Login SQL منقضی/غیرفعال؛ CI؛ دو POST هم‌زمان روی HTTP؛ POST بوت‌استرپ Master روی HTTP.
