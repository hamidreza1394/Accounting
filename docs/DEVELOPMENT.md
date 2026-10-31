# راهنمای توسعه محلی

## رابط React و shadcn/ui

استاندارد طراحی کامپوننت‌های جدید در `ClientApp`، shadcn/ui با RTL فارسی است؛ راهنمای کامل در [`UI_COMPONENTS.md`](UI_COMPONENTS.md) آمده است. `ClientApp/components.json` پیکربندی اولیه را ثبت می‌کند، اما به‌تنهایی وابستگی npm را نصب نمی‌کند. پیش از استفاده از کامپوننت‌ها باید مرحلهٔ نصب رسمی اجرا شود، `package.json` و `package-lock.json` هماهنگ بمانند و `npm ci`, `npm run typecheck`, `npm test`, `npm run build` و `npm run check:offline` سبز شوند. از اجرای `npx shadcn@latest` بدون بازبینی نسخه و diff در شاخهٔ کاری استفاده نکنید.

## پیش‌نیازها
- Visual Studio 2026 (نسخه 18.0 یا بالاتر) با workload مربوط به ASP.NET and web development و .NET 10 SDK نصب‌شده. برای بازکردن و هدف‌گذاری رسمی net10.0، Visual Studio 2022 کافی نیست.
- SQL Server متناسب با Connection String محیط محلی.
- Git؛ Python 3 فقط برای بررسی ممنوعیت CDN در اسکریپت CI.
- برای دریافت/به‌روزرسانی NuGet و LibMan در مرحله آماده‌سازی به اینترنت دسترسی لازم است؛ **اجرای برنامه پس از دریافت وابستگی‌ها نباید برای CSS/JS/فونت/آیکون به اینترنت خارجی نیاز داشته باشد**.

## تنظیم اتصال پایگاه داده
در اجرای اولیه، اگر Connection String از User Secrets، متغیر محیطی یا تنظیمات صریح برنامه ارائه نشده باشد، برنامه به صفحه `/DatabaseSetup` هدایت می‌شود. فرم اتصال SQL Server را آزمایش می‌کند، در صورت نیاز دیتابیس را می‌سازد و برای دیتابیس خالی Migrationهای EF Core را اجرا می‌کند.

- فایل تنظیمات محافظت‌شده (`database-connection.json`) در `LocalApplicationData/Farano/Settings` ذخیره می‌شود (ویندوز: `%LOCALAPPDATA%\\Farano\\Settings`)؛ با کلید `Farano:Settings:Directory` (متغیر `Farano__Settings__Directory`) قابل تغییر است (مسیر absolute، بیرون پوشه برنامه). مسیر دقیق روی صفحه نمایش داده می‌شود. فایل قدیمی `TarazAva` فقط خوانده و مهاجرت می‌شود؛ جزئیات و فرمان `--settings`: [`DATABASE_SETUP.md`](DATABASE_SETUP.md).
- Connection String در فایل به کمک ASP.NET Core Data Protection رمزنگاری می‌شود. کلیدهای Data Protection در پوشه `keys` نگه‌داری می‌شوند و در ویندوز با DPAPI محافظت می‌شوند.
- فایل `setup-token.txt` کد یک‌بارمصرف فرم را نگه می‌دارد. آن را فقط از روی همان سرور بخوانید و در اختیار دیگران قرار ندهید؛ پس از ذخیره موفق تنظیمات حذف می‌شود.
- پس از ذخیره تنظیمات، برنامه را متوقف و دوباره اجرا کنید. برای دیتابیس موجود، Migration خودکار انجام نمی‌شود تا schema قدیمی یا دیتابیس ایجادشده با `EnsureCreated()` ناخواسته تغییر نکند.
- در استقرار، پوشه داده و کلیدهای Data Protection باید با همان هویت سرویس قابل دسترس باشند. جابه‌جایی فایل تنظیمات به دستگاه دیگر بدون انتقال امن کلیدها امکان رمزگشایی را تضمین نمی‌کند.
- برای پیکربندی صریح/مدیریت‌شده، مقادیر `ConnectionStrings:ConnectionString` در User Secrets یا متغیر محیطی `ConnectionStrings__ConnectionString` بر فایل محلی مقدم هستند.

برای استفاده از User Secrets:
```powershell
dotnet user-secrets init --project Server/Server.csproj
dotnet user-secrets set "ConnectionStrings:ConnectionString" "Server=localhost;Database=Farano;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=False;" --project Server/Server.csproj
```

ورود مدیریتی فعلی هنوز legacy است و برای Production تأیید نمی‌شود؛ راهنمای `docs/SECURITY.md` را بخوانید. مقادیر `MasterUsername` و `MasterPassword` باید فقط در User Secrets/Environment/Secret Store تنظیم شوند و خالی‌بودن آن‌ها نباید با مقدار پیش‌فرض ناامن دور زده شود. (به‌روزرسانی 2026-10-21: گزینه `ActivationKeys` فعلاً بی‌اثر است چون Middleware به Pipeline متصل نیست — D-028؛ لایسنس آینده در `docs/LICENSING.md`.)

## متغیرهای محیطی تغییرنام‌یافته (2026-10-23)
- `TARAZAVA_CONNECTIONSTRING` ← `FARANO_CONNECTIONSTRING` (ابزار طراحی EF) و `TARAZAVA_TEST_CONNECTIONSTRING` ← `FARANO_TEST_CONNECTIONSTRING` (`scripts/apply_migrations_to_testdb.sh`). نام قدیمی هنوز به‌عنوان جایگزین خوانده می‌شود؛ نام جدید را به‌کار ببرید. دیتابیس پیش‌فرض ابزار طراحی `Farano` است.
- تست‌های SQL Server همچنان با `FARANOU_TEST_SQLSERVER` (املای Faranou) کنترل می‌شوند؛ املای نهایی Farano/Faranou `NeedsApproval` است (`DATABASE_SETUP.md` بخش ۱).

## Restore و اجرا
```powershell
dotnet restore Template.sln
dotnet build Template.sln --configuration Release
dotnet test Template.sln --configuration Release
dotnet run --project Server/Server.csproj
```

## اجرای محلی React در Visual Studio

مسیر `/app/` از خروجی ساخته‌شدهٔ React در `Server/wwwroot/app` سرو می‌شود؛ این پوشه عمداً در Git نادیده گرفته شده است. برای جلوگیری از 404 در اجرای F5:

- در Build عادی پروژهٔ Server از داخل Visual Studio، MSBuild به‌صورت افزایشی ClientApp را Build می‌کند؛ در اولین اجرا، اگر `ClientApp/node_modules` موجود نباشد، `npm ci` اجرا می‌شود. پیش‌نیاز: Node.js و npm در PATH باشند و برای نصب اولیه بسته‌ها دسترسی به رجیستری npm وجود داشته باشد.
- برای اجرای خط فرمان، ابتدا از ریشهٔ مخزن اجرا کنید: `cd ClientApp`, سپس `npm ci` (در اولین بار) و `npm run build`؛ بعد Server را اجرا کنید. گردش انتشار همچنان باید از `scripts/publish.ps1` یا `scripts/publish.sh` استفاده کند.
- خروجی تولیدی را به Git اضافه نکنید. اجرای برنامه پس از Build همچنان محلی است و به CDN خارجی وابسته نیست.
- اگر عمداً فقط Server را Build می‌کنید و نمی‌خواهید ClientApp در Visual Studio ساخته شود، از property `FaranouSkipClientAppBuild=true` استفاده کنید؛ در این حالت باید خروجی React را جداگانه بسازید.

## کتابخانه‌های مرورگر
`Server/libman.json` مرجع نسخه LibMan است و فایل‌های مورد استفاده مرورگر داخل `Server/wwwroot/lib/` نگهداری می‌شوند. پس از تغییر نسخه، فایل‌های محلی را واقعاً به‌روزرسانی و UI را تست کنید:

```powershell
libman restore --config Server/libman.json
python scripts/check_static_assets.py
```

دستور `libman restore` به منبع دانلود وابسته است؛ پس از دریافت فایل‌ها، اجرای برنامه در حالت عادی نباید به CDN نیاز داشته باشد. فایل‌های vendored را بدون بررسی مجوز، نسخه و اثر UI جایگزین نکنید.

## EF Core migrations
در حال حاضر Migrationهای رسمی و فرایند استقرار دیتابیس باید پیش از استفاده محصولی بررسی و تثبیت شوند. Migration را بدون بازبینی مدل، SQL تولیدی، محیط هدف و نسخه پشتیبان روی دیتابیس واقعی اجرا نکنید. دستورهای مرجع پس از تثبیت فرایند:

```powershell
dotnet ef migrations list --project Infrastructure/Persistence/Infrastructure.Persistence.csproj --startup-project Server/Server.csproj
# افزودن migration فقط پس از تغییر مدل و بررسی وضعیت موجود:
# dotnet ef migrations add <DescriptiveName> --project Infrastructure/Persistence/Infrastructure.Persistence.csproj --startup-project Server/Server.csproj
```

## EF Core tooling for Trade

`TradeDbContext` از همان دیتابیس فیزیکی حسابداری استفاده می‌کند، اما schema و migration history مستقل دارد. ابزار EF را با پروژه Infrastructure اجرا کنید؛ Factory طراحی از متغیر `FARANO_TRADE_CONNECTIONSTRING` (اولویت اول)، سپس `FARANO_CONNECTIONSTRING`، `TARAZAVA_CONNECTIONSTRING` یا `ConnectionStrings__ConnectionString` استفاده می‌کند و در نبود آن‌ها فقط یک مقدار LocalDB مخصوص توسعه برمی‌گزیند. برای هر دستور متصل‌شونده به دیتابیس، اتصال آزمایشی/توسعه را صریح تنظیم کنید. هیچ دستور Migration را روی دیتابیس مشتری اجرا نکنید مگر پس از Issue و تأیید جداگانه.

```powershell
dotnet ef migrations list --project Modules/Trade/Trade.Infrastructure/Trade.Infrastructure.csproj --startup-project Server/Server.csproj --context Faranou.Modules.Trade.Infrastructure.Persistence.TradeDbContext
```

## نکته نسخه پلتفرم
هدف فعلی پروژه .NET 10 LTS است. Visual Studio 2026 نسخه 18.0 یا بالاتر برای هدف‌گذاری رسمی net10.0 لازم است. پیش از ادغام تغییرات، Restore/Build/Test در CI و روی دستگاه توسعه باید موفق باشند.


## تنظیم اتصال SQL Server برای محیط‌های مختلف

- مقدار توسعه محلی در `Server/appsettings.json` فقط از Windows Integrated Authentication استفاده می‌کند و رمز دیتابیس در Git ذخیره نمی‌شود.
- برای جایگزینی اتصال در دستگاه توسعه، از User Secrets استفاده کنید.
- برای سرور مشتری یا فضای ابری، متغیر محیطی `ConnectionStrings__ConnectionString` را در تنظیمات سرویس میزبانی تنظیم کنید؛ این مقدار بر appsettings پیش‌فرض اولویت دارد.
- برای SQL Login در محیط عملیاتی، رمز را فقط از Secret Store/Environment امن تزریق کنید؛ آن را در appsettings یا Git commit نکنید.
- تغییر Connection String فقط محل SQL Server را عوض می‌کند؛ قبل از استقرار باید Schema از طریق Migration بازبینی‌شده اعمال شود. Migration و انتقال داده هنوز بخشی از این تغییر نیست.

نمونه متغیر محیطی PowerShell برای نشست فعلی:

```powershell
$env:ConnectionStrings__ConnectionString = "Server=localhost\SQLSERVER2025;Database=Farano;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true;"
```

## .NET 10 و Visual Studio

- مخزن به `.NET 10 LTS` و EF Core 10 ارتقا داده شده است.
- برای توسعه رسمی `net10.0` از Visual Studio 2026 نسخه 18.0 یا بالاتر استفاده کنید. نصب SDK به‌تنهایی پشتیبانی هدف‌گذاری net10.0 را به Visual Studio 2022 اضافه نمی‌کند.
- پس از نصب، از Developer PowerShell اجرا کنید: `dotnet --info`, `dotnet restore Template.sln`, `dotnet build Template.sln --configuration Release`, `dotnet test Template.sln --configuration Release`.


## Claude Code Cloud: SDK و SQL Server آزمایشی (2026-10-23)

**علت نبودن `dotnet`:** تصویر پایهٔ جلسه‌های Claude Code Cloud (Ubuntu 24.04) فقط ابزارهای از پیش‌نصب‌شدهٔ خودش را دارد و .NET SDK در آن نیست؛ مستندات رسمی Anthropic صریحاً می‌گوید ابزارهای بیرون از آن فهرست، حتی اگر رجیستری‌شان در فهرست مجاز شبکه باشد، با «Setup script» نصب می‌شوند. دامنه‌های `builds.dotnet.microsoft.com`، `api.nuget.org`، `mcr.microsoft.com` و Docker Hub در سطح شبکهٔ پیش‌فرض (Trusted) مجازند؛ مانع شبکه نیست. Docker نصب است ولی daemon پیش‌فرض بالا نمی‌آید.

**راه‌حل قابل تکرار در مخزن (بدون مسیر شخصی):**
- `scripts/dev/install-dotnet.sh`: نسخهٔ SDK را از `global.json` (`sdk.version` + `rollForward`) می‌خواند، تازه‌ترین SDK پایدار پذیرفته‌شده را از `releases.json` رسمی Microsoft پیدا می‌کند، SHA-512 را با مقدار منتشرشده تطبیق می‌دهد (ناسازگاری = توقف بدون نصب؛ این کنترل «یکپارچگی نسبت به فراداده منتشرشده» است، نه امضای دیجیتال: اعتماد به HTTPS با `builds.dotnet.microsoft.com` است و فقط همان میزبان با https پذیرفته می‌شود) و در `/usr/share/dotnet` (root) یا `~/.dotnet` نصب می‌کند و `dotnet` را به `/usr/local/bin` پیوند می‌دهد. ایدمپوتنت است؛ `--check` فقط نسخهٔ لازم را گزارش می‌کند (کد خروج ۳ = نصب نیست). با `global.json` فعلی (۱۰٫۰٫۱۰۰، `latestFeature`) در 2026-10-23 نسخهٔ `10.0.402` نصب شد؛ همان نسخه‌ای که `actions/setup-dotnet` با `10.0.x` در CI می‌گیرد.
- `scripts/dev/cloud-session-start.sh` + Hook `SessionStart` در `.claude/settings.json`: فقط وقتی `CLAUDE_CODE_REMOTE=true` است اسکریپت بالا را اجرا می‌کند (محلی هیچ کاری نمی‌کند) و هرگز جلسه را خراب نمی‌کند؛ خطا را روی stderr می‌نویسد.
- **سریع‌تر و کش‌شده (اختیاری، دستی توسط مالک حساب):** Setup script پیکربندی خود Environment است و در مخزن ذخیره نمی‌شود. در تنظیمات Environment همین دو خط را بگذارید؛ نصب یک‌بار انجام و در کش (حدود ۷ روز) نگه داشته می‌شود:
  ```bash
  #!/bin/bash
  bash scripts/dev/install-dotnet.sh || true
  ```
  (در آزمایش این محیط نصب سرد حدود ۹ ثانیه طول کشید؛ مهلت ۲۴۰ثانیه‌ای curl زیر حد ۳۰۰ثانیه‌ای Hook و حد تقریبی ۵ دقیقهٔ کش می‌ماند. نصب نیمه‌کاره با فایل نشانهٔ `.faranou-complete` تشخیص داده و در اجرای بعدی ترمیم می‌شود.) بدون این کار Hook همین نصب را در شروع هر جلسه ایدمپوتنت انجام می‌دهد.
- پس از آماده‌سازی: `dotnet --info`، `dotnet --list-sdks`.

**SQL Server واقعی (نه SQLite) برای تست‌های یکپارچه و E2E:** `scripts/dev/test-sqlserver.sh up|env|down`. کانتینر `mcr.microsoft.com/mssql/server:2022-latest` (برچسب متحرک، مثل CI) فقط روی `127.0.0.1:14333`، رمز `sa` تصادفی در فایل با مجوز ۶۰۰ بیرون از مخزن؛ اگر daemon بالا نباشد و کاربر root باشد `dockerd` را بالا می‌آورد؛ تست‌های xUnit دیتابیس‌های خودشان (`FaranouTests_CG_*`) را می‌سازند و حذف می‌کنند؛ دیتابیس E2E (`FaranouE2E`) را `--migrate` می‌سازد و تا `down` در همین سرور موقت می‌ماند. «ایزوله» یعنی نمونهٔ جدا و یک‌بارمصرف روی localhost، نه ایزولهٔ شبکه‌ای از کانتینرهای دیگر همان میزبان Docker؛ اسکریپت فقط کانتینری را حذف می‌کند که خودش با برچسب `faranou.test-sqlserver=1` ساخته است. رمز در `docker inspect` و فهرست پردازش‌های همان ماشین دیده می‌شود (رمز یک‌بارمصرف است)؛ خروجی `up`/`env` را حتماً در متغیر بگیرید و چاپ نکنید.

**Hook و مجوزها:** Hook `SessionStart` بیرون از `permissions.ask/deny` اجرا می‌شود و کد مخزن را در شروع جلسه اجرا می‌کند (در Cloud با دسترسی root). برای همین فقط وقتی `CLAUDE_CODE_REMOTE=true` است اسکریپت شروع می‌شود و تغییر `scripts/dev/*` و `.claude/*` باید مثل پیکربندی اجرایی بازبینی شود. پیشنهاد (نیازمند تصمیم مسئول مخزن، در این PR اعمال نشد): فایل `CODEOWNERS` برای `.claude/**` و `scripts/dev/**` و بازبینی اجباری.

```bash
scripts/dev/install-dotnet.sh
out="$(scripts/dev/test-sqlserver.sh up)" && eval "$out"   # FARANOU_TEST_SQLSERVER، FARANOU_REQUIRE_SQLSERVER، FARANOU_E2E_SQL
dotnet restore Template.sln
dotnet build Template.sln --no-restore --configuration Release
dotnet test Template.sln --no-build --configuration Release
# E2E (Node 22.12+؛ در CI Node 24): انتشار در مسیر موقت خارج از مخزن، Migration، Seed، Playwright
export FARANOU_E2E_PUBLISH="$(mktemp -d)/faranou-publish"
scripts/publish.sh "$FARANOU_E2E_PUBLISH"
ConnectionStrings__ConnectionString="$FARANOU_E2E_SQL" dotnet "$FARANOU_E2E_PUBLISH/Server.dll" --migrate
(cd tests/e2e && npm ci && npm run seed && npx playwright test)
scripts/dev/test-sqlserver.sh down
```
هیچ رمز یا Connection String ثابت در Git نیست. این اسکریپت‌ها جایگزین Job های CI نیستند؛ CI همچنان با Service Container همان تست‌ها را روی PR اجرا می‌کند.

## کلاینت React (`ClientApp`)
پیش‌نیاز: Node.js 22.12+ (CI: Node 24 / npm 11).

```powershell
cd ClientApp
npm ci
npm run build      # خروجی در Server/wwwroot/app؛ سپس سرور را اجرا و /app را باز کنید
npm run dev        # توسعه: http://localhost:5173/app/ ؛ /api و /Account به https://localhost:5001 پروکسی می‌شوند
```
- آدرس سرور برای پروکسی: متغیر `FARANOU_SERVER_URL`.
- خروجی Build در Git ثبت نمی‌شود (`.gitignore`)؛ در انتشار ابتدا `npm ci && npm run build` و سپس `dotnet publish` اجرا شود. اگر Build کلاینت انجام نشده باشد، `/app` پاسخ 404 می‌دهد و بقیه برنامه کار می‌کند.
- ورود از صفحه Razor `/Account/Login` انجام می‌شود؛ پس از ورود، `/app` یا پیوند «داشبورد جدید (پیش‌نمایش)» در صفحه اصلی را باز کنید.


## انتشار (Production)
```bash
scripts/publish.sh artifacts/publish          # Linux/macOS/CI
scripts/smoke-published-app.sh artifacts/publish   # اختیاری: آزمون HTTP خروجی
```
```powershell
pwsh scripts/publish.ps1 -Output artifacts\publish   # Windows (همان مراحل)
```
- ترتیب ثابت: `npm ci` ← typecheck/تست کلاینت ← `npm run build` ← بررسی آفلاین ← `dotnet publish` ← بررسی وجود `wwwroot/app/index.html`، JS، CSS، فونت و تصاویر در خروجی.
- `dotnet publish` مستقیم بدون Build کلاینت خطا می‌دهد (D-016).
- **توسعه محلی:** `npm run dev` (Vite + پروکسی به سرور) یا یک‌بار `npm run build` و سپس `dotnet run`؛ **Production:** فقط اسکریپت انتشار.
- تنظیمات جدید قابل تغییر در `appsettings`/Environment: `RateLimiting:*` و `SecurityHeaders:EnforceRazorContentSecurityPolicy`.


## سامانهٔ مرکزی Vendor: CI و بستهٔ مستقل
- تغییرات `Vendor/**`، `Vendor.sln`، تست‌های Vendor، `Licensing.Contracts` و تنظیمات SDK/NuGet در workflow مستقل `.github/workflows/vendor-ci.yml` بررسی می‌شوند؛ این کار از workflow کامل محصول مشتری جداست.
- خروجی Publish Vendor از push به `main` به‌شکل GitHub Actions artifact با مهلت نگهداری ۷ روز منتشر می‌شود. این artifact **استقرار Production نیست**؛ هنوز محیط‌های deploy، نام دامنه/TLS، secret store و رویهٔ release باید در مرحلهٔ جدا تعیین شوند.
- Workflow هیچ credential استقرار یا کلید امضایی ندارد و عملیات Sign لایسنس در GitHub Actions انجام نمی‌شود.
- `.github/CODEOWNERS` مسیرهای Vendor و قرارداد مشترک را به مالک فعلی مخزن ارجاع می‌دهد؛ این فایل به‌تنهایی بازبینی اجباری ایجاد نمی‌کند. تنظیم `Require review from Code Owners` و حداقل یک reviewer واجد دسترسی غیر از نویسنده باید در Branch protection بررسی و در تنظیمات مخزن اعمال شود.


## پیکربندی persistence سامانهٔ Vendor
- Host مستقل از متغیر با پیشوند `FARANO_VENDOR_` استفاده می‌کند. کلید اتصال دیتابیس `ConnectionStrings:VendorDb` است و نام متغیر محیطی آن `FARANO_VENDOR_ConnectionStrings__VendorDb` می‌باشد؛ هیچ مقدار پیش‌فرض یا اتصال به دیتابیس مشتری وجود ندارد.
- `/health/live` فقط liveness است و اتصال دیتابیس را بررسی نمی‌کند. هر مسیر/سرویسی که `VendorDbContext` را resolve کند در نبود connection string با خطای تنظیمات عمومی fail-closed می‌شود؛ مقدار connection string نباید در log ثبت شود.
- برای ابزارهای EF Core نیز همین متغیر باید به یک **دیتابیس مخصوص Vendor** اشاره کند. کارخانهٔ طراحی عمداً اتصال پیش‌فرض (local/customer) ندارد.
- در محیط توسعه، پس از تنظیم متغیر امن، فهرست Migrationها را با فرمان زیر بررسی کنید:
  `dotnet ef migrations list --project Vendor/Vendor.Persistence/Vendor.Persistence.csproj --startup-project Vendor/Vendor.Persistence/Vendor.Persistence.csproj --context VendorDbContext`
- پیش از اعمال در محیط Production، SQL script قابل بازبینی تولید و مرحله‌بندی deployment/backup طبق راهنمای عملیات اجرا شود؛ Migrationهای Vendor هیچ‌گاه روی دیتابیس مشتری اجرا نمی‌شوند.


## نسخهٔ SDK در CI
- برای reproducibility، `.github/workflows/dotnet.yml` فعلاً از SDK `10.0.402` استفاده می‌کند؛ این نسخه در workflow مستقل Vendor نصب و آزموده شد، در حالی‌که انتخاب خودکار `10.0.x` در 2026-10-25 به دانلود `10.0.403` و `10.0.14` با HTTP 409 برخورد کرد (Issue #108).
- این pin جایگزین ارتقای امنیتی معمول نیست: هنگام انتشار و دسترس‌پذیری پایدار SDK patch جدید، با Issue/PR جدا، `dotnet-version` و `global.json` را هماهنگ و همهٔ تست‌های CI را اجرا کنید. صرف سبزشدن Setup کافی نیست.
