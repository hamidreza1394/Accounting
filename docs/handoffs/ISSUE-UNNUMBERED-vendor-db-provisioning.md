# تحویل وظیفه — ISSUE-UNNUMBERED-vendor-db-provisioning

- **Issue:** UNNUMBERED — ابزار ساخت Issue در جلسه در دسترس نبود (خطای مکرر «missing required parameter: title» در API Issue)؛ شماره جعلی ساخته نشد. پس از باز شدن Issue، این سند و PR باید شماره‌دار شوند.
- **Agent/مسئول:** GLM (Lead)
- **تاریخ:** 2026-11-27
- **وضعیت:** انجام‌شده در شاخه؛ تا CI سبز نشده Verified نیست

## هدف و معیارهای پذیرش
- [x] Vendor در Development می‌تواند دیتابیس مستقل خود را وقتی وجود ندارد ایجاد و با مهاجرت‌های موجود مقداردهی کند؛ بدون SSMS (پیاده‌سازی شد؛ اجرای واقعی روی Windows توسعه، آزمون دستی اپراتور است)
- [x] اجرای مکرر Provisioning امن و ایدمپوتنت است؛ دیتابیس موجود بازسازی نمی‌شود (تست SQL idempotency)
- [x] credentialهای مدیریتی در فایل، config، log، پاسخ API یا مرورگر ذخیره نمی‌شوند
- [x] دسترسی Runtime از مجوز Provisioning جدا است و Runtime بدون sysadmin کار می‌کند (تست SQL least-privilege)
- [x] دیتابیس مشتری/بیگانه به‌عنوان هدف Vendor رد می‌شود (تشخیص عمومی بیگانه + تست SQL)
- [x] endpointهای Provisioning برای فراخوان ناشناس/غیرمجاز قابل استفاده نیستند (تست exposure به‌روز شد)
- [x] تست واحد + تست یکپارچگی SQL Server با قرارداد `FARANO_VENDOR_TEST_SQLSERVER`/Skip اضافه شد
- [x] مستندات Vendor (README، ADR D-122، spec، WORK_LOG، INDEX) هم‌راستا شدند

## خلاصه تغییرات
- جریان هفت‌مرحله‌ای راه‌اندازی خودکار روی همان صفحهٔ setup: اتصال مدیریتی → بررسی وضعیت → ایجاد در صورت نیاز → مهاجرت → دسترسی اجرای محدود → آزمون اجرا → ذخیرهٔ اجرا (دو مرحله آخر با endpointهای موجود).
- چهار endpoint جدید `provision/check|create|migrate|runtime` با همان گارد loopback/Origin/antiforgery/rate-limit و سیاست fallback در حالت برنامهٔ کامل.
- تفکیک هویت Provisioning (موقتی، در حافظهٔ درخواست) از Runtime (ذخیره‌شدهٔ Credential Manager)؛ حداقل مجوز اجرا (db_datareader/db_datawriter، بدون sysadmin، ممنوعیت sa).
- ایمنی SQL: اعتبارسنجی سخت‌گیرانهٔ شناسه + QUOTENAME سمت سرور؛ SQL پویا فقط جاهایی که پارامترسازی در گرامر DDL ممکن نیست، با دلیل مستند.
- تشخیص عمومی دیتابیس بیگانه (جدول بدون تاریخچهٔ Vendor یا شناسه‌های مهاجرت ناشناخته) → fail-safe؛ `sp_getapplock` برای هم‌زمانی؛ نگاشت خطای SQL به پیام فارسی امن.

## فایل‌های مهم تغییرکرده
- `Vendor/Vendor.Host/Development/VendorDevelopmentDatabaseProvisioning.cs` — جدید: سرویس چهار مرحله + اعتبارسنجی/quoting/lock/نگاشت خطا
- `Vendor/Vendor.Host/Development/VendorDevelopmentDatabaseSetup.cs` — افزودن ۴ endpoint با همان الگوی گارد موجود
- `Vendor/Vendor.Host/Development/StaticFiles/index.html` — بخش راه‌اندازی خودکار + فهرست ۷ مرحله + به‌روزرسانی footer
- `Vendor/Vendor.Host/Development/StaticFiles/setup.js` — ارکستراسیون مراحل، پر کردن خودکار فرم اجرا پس از ساخت login، اتصال به test/save موجود
- `Vendor/Vendor.Host/Development/StaticFiles/setup.css` — استایل مراحل (pending/running/success/failed/warning) و ریسپانسیو
- `Vendor/Vendor.Host/Vendor.Host.csproj` — `InternalsVisibleTo` برای تست
- `tests/Vendor.Host.Tests/VendorDevelopmentDatabaseProvisioningTests.cs` — جدید: ۵ تست واحد + ۳ تست یکپارچگی SQL
- `tests/Vendor.Host.Tests/VendorDevelopmentDatabaseSetupExposureTests.cs` — افزودن مسیرهای provision به فهرست 404
- `Vendor/README.md`، `docs/DECISIONS.md` (D-122)، `docs/superpowers/specs/2026-11-27-...-design.md`، `docs/WORK_LOG.md`، `docs/INDEX.md`

## تصمیمات فنی
- انتخاب «Provisioning در همان صفحهٔ setup با credential مدیریتی صریح» از گزینه‌های CLI جدا/credential مشترک/db_owner؛ ثبت در **D-122**.
- تشخیص دیتابیس بیگانه به‌جای فهرست نام ثابت Customer (عمومی و مقاوم).
- `CREATE LOGIN` با SQL پویا + `QUOTENAME(@pwd, '''')`؛ دلیل: گرامر T-SQL پارامتر پذیر نیست.
- بدون Migration جدید، بدون تغییر Schema، بدون `EnsureCreated`؛ مهاجرت فقط با `IMigrator` روی مهاجرت‌های موجود.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `dotnet restore Vendor.sln` | موفق | SDK 10.0.404 |
| `dotnet build Vendor.sln --no-restore --configuration Release` | موفق | ۰ خطا |
| `dotnet test tests/Vendor.Host.Tests --filter VendorDevelopmentDatabaseProvisioningTests` | ۸ تست: ۵ موفق، ۳ Skip | تست‌های SQL در این محیط اجرا نشدند (نبود Docker/SQL Server) و در CI با SQL Server موقت اجرا می‌شوند |
| `dotnet test Vendor.sln --no-build --configuration Release` (کل) | ۴۷۷ کل: ۳۷۹ موفق، ۱۳ شکست، ۸۵ Skip | **مبنای مقایسه:** روی `main` تمیز همان محیط: ۴۶۹ کل، ۳۷۵ موفق، **۱۲ شکست**، ۸۲ Skip. همهٔ ۱۲ شکست محیطی (WebApplicationFactory حالت کامل بدون connection string روی Linux). اختلاف +۱: `SignerTimeoutCancelsProviderAndFailsClosed` — تایمینگ-فلکی و اثبات‌شده روی main تمیز هم شکست می‌خورد و در اجرای ایزوله ۳/۳ سبز است؛ بی‌ارتباط به این تغییر. |
| تست‌های SQL یکپارچگی جدید | اجرا نشدند محلی؛ در CI اجرا می‌شوند | قرارداد `FARANO_REQUIRE_VENDOR_SQLSERVER=1` در Vendor CI |
| `git diff --check` | پاک | بدون whitespace error |

## امنیت، داده و سازگاری
- هیچ تغییر Schema/Migration، هیچ تغییر در دیتابیس Customer یا `Server/**`، هیچ secret جدید؛ credential مدیریتی فقط در حافظهٔ درخواست؛ logها فقط نوع استثنا/شماره خطای SQL.
- سیاست حداقل مجوز: هویت اجرا بدون sysadmin (رد fail پیش از اعطا)، بدون DDL (تست SQL) و بدون دسترسی cross-database (تست SQL).
- سازگاری: قرارداد اتصال و ذخیرهٔ Credential Manager دست‌نخورده؛ endpointهای قبلی تغییری در رفتار نداشتند.

## ریسک‌ها و کارهای باقی‌مانده
- اجرای واقعی جریان روی SQL Server توسعهٔ واقعی (Windows) به اپراتور موکول است؛ در CI ادعا نمی‌شود.
- شماره‌دار شدن Issue و درج آن در PR/handoff پس از رفع ابزار.
- مرور انسانی نهایی diff و تصمیم Merge با مالک مخزن.

## شناسه‌های Git
- Branch: `agent/vendor-db-provisioning` (از `main@110d797f`)
- Commit: محلی؛ انتشار از طریق API مخزن
- Pull Request: پس از push ایجاد می‌شود (Merge با مالک)
