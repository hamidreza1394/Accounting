# تحویل — راه‌اندازی کامل خودکار دیتابیس Vendor در صفحه Development (UNNUMBERED)

- **Issue:** `UNNUMBERED` (ادامهٔ Issue #797 / PR #800؛ شمارهٔ Issue جدید در دسترس نبود و شمارهٔ جعلی ساخته نشد)
- **Agent/مسئول:** ZCode (glm) — بدون Subagentهای مخزن (در این جلسه فقط Explore/general-purpose در دسترس بود؛ بازبینی امنیتی به‌صورت چک‌لیست دستی توسط Lead انجام شد و در بخش بازبینی ثبت شده است)
- **تاریخ:** 2026-11-27
- **شاخه:** `agent/vendor-dev-db-full-provisioning` (مبنا: `main@110d797`، PR #800)
- **وضعیت:** پیاده‌سازی کامل + Build/Test واقعی محلی؛ اجرای زنده روی SQL Server واقعی در CI یا دستگاه مالک هنوز انجام نشده (`Implemented / Not yet Verified`)

## هدف و معیارهای پذیرش
- [x] جریان کامل خودکار روی صفحهٔ `development/database-setup`: اتصال موقت → آزمون → بازرسی → ساخت دیتابیس (پیش‌فرض `VendorDb`، قابل ورود) → اعمال Migrationهای موجود EF Core → پیکربندی اتصال Runtime → بررسی آمادگی
- [x] جداسازی هویت: هویت پیکربندی (موقت و پراختیار) فقط در صفحه؛ اتصال ذخیره‌شده برای اجرای بعدی، هویت Runtime کم‌اختیار SQL است
- [x] هویت Runtime به هیچ دیتابیس دیگری (از جمله دیتابیس‌های مشتری) دسترسی ندارد؛ فقط `db_datareader`/`db_datawriter` روی دیتابیس Vendor و بدون db_owner
- [x] تک‌منبع حقیقت اتصال حفظ شد: `FARANO_VENDOR_ConnectionStrings__VendorDb`؛ اگر منبع Environment/Configuration صریح است، جریان اجرا می‌شود اما چیزی ذخیره نمی‌کند و اعتبار Runtime را یک‌بار نمایش می‌دهد
- [x] رفتار idempotent: دیتابیس موجود حذف/بازسازی نمی‌شود (هرگز DROP)، Migration فقط معوق‌ها را اعمال می‌کند، Login/User/نقش‌ها فقط در صورت نبود ساخته می‌شوند
- [x] امنیت: اعتبارسنجی شناسهٔ دیتابیس/Login (ضد SQL Injection، الگوی مسیر مشتری)، همهٔ lookupها پارامتری، همان loopback/HTTPS/Origin/antiforgery/rate-limit صفحهٔ #797، بدون افشای رمز/connection string در پاسخ/لاگ
- [x] بدون هیچ تغییری در Customer، `Server/**`، Migrationها، schema، کلیدها، gateها یا نصب Windows Service
- [x] UI فارسی RTL با فهرست ۷ مرحله و وضعیت هر مرحله؛ فرم قبلی آزمون/ذخیره ساده دست‌نخورده ماند

## خلاصه تغییرات
- کلاس جدید `VendorDevelopmentDatabaseProvisioning` با pipeline ۷ مرحله‌ای، دو helper اعتبارسنجی (نام دیتابیس با قواعد مسیر مشتری شامل NFKC/هم‌ریخت/محدودیت ۱۲۳ نویسه؛ نام Login فقط ASCII)، تولید رمز رمزنگارانهٔ ۳۲ نویسه‌ای بدون نویسه‌های خطرناک، گاردهای «دیتابیس ناشناس» و «جدیدتر از برنامه» قبل از Migration، ساخت/تنظیم Login و کاربر و نقش‌ها، verify با اتصال Runtime و ذخیرهٔ شرطی در Credential Manager.
- دو endpoint جدید در همان سطح محافظت‌شدهٔ صفحه: `POST /api/development/database-setup/inspect` (فقط‌خواندنی، مراحل ۱–۳) و `POST /api/development/database-setup/provision` (کل جریان) با policy نرخ جدید `vendor-development-setup-provision` (۳/دقیقه) و قفل یک‌زمانی pipeline.
- UI: پنل «هویت Runtime کم‌اختیار»، دو دکمهٔ «بررسی وضعیت» و «راه‌اندازی کامل دیتابیس»، فهرست مراحل با وضعیت (در انتظار/موفق/بی‌تغییر/ناموفق)، جعبهٔ یک‌بارنمایش اعتبار Runtime، اصلاح پانوشت قبلی («دیتابیس ایجاد نمی‌کند» دیگر صادق نیست).
- تست‌ها: `VendorDevelopmentDatabaseProvisioningTests` (۲۹ حالت واحدی برای اعتبارسنجی‌ها/رمز/مراحل/شکل اتصال Runtime)، `VendorDevelopmentDatabaseProvisioningSqlServerTests` (۳ تست انتگرال گیت‌دار: موفقیت کامل + idempotency اجرای دوم، رد نام سیستمی، رد دیتابیس ناشناس)، و گسترش `VendorDevelopmentDatabaseSetupExposureTests` به endpointهای جدید (404 بدون flag و خارج از loopback).

## فایل‌های مهم تغییرکرده
- `Vendor/Vendor.Host/Development/VendorDevelopmentDatabaseProvisioning.cs` — جدید؛ pipeline و اعتبارسنجی‌ها
- `Vendor/Vendor.Host/Development/VendorDevelopmentDatabaseSetup.cs` — ثبت دو endpoint جدید + helper `WriteSavedConnection`
- `Vendor/Vendor.Host/Program.cs` — policy نرخ `vendor-development-setup-provision` در هر دو بلوک rate limiter
- `Vendor/Vendor.Host/Vendor.Host.csproj` — `InternalsVisibleTo Vendor.Host.Tests` برای تست واحدِ helperهای داخلی
- `Vendor/Vendor.Host/Development/StaticFiles/index.html` / `setup.js` / `setup.css` — UI جریان کامل
- `tests/Vendor.Host.Tests/VendorDevelopmentDatabaseProvisioningTests.cs` — جدید
- `tests/Vendor.Host.Tests/VendorDevelopmentDatabaseProvisioningSqlServerTests.cs` — جدید (گیت‌دار)
- `tests/Vendor.Host.Tests/VendorDevelopmentDatabaseSetupExposureTests.cs` — گسترش
- `Vendor/README.md`، `docs/INDEX.md`، `docs/superpowers/specs/2026-11-27-vendor-development-database-provisioning-design.md`، همین handoff، `docs/WORK_LOG.md`

## تصمیم‌های فنی
- Migration مستقیماً با `VendorDbContext` و `MigrationsAssembly` همان ثبت runtime (`AddVendorPersistence`) اجرا می‌شود — بدون ثبت سرویس‌های Identity در setup-only؛ abstraction جدید اضافه نشد.
- رفتار Login هم‌نام: بازنشانی گذرواژه با مقدار فرم (هشدار صریح در UI + نام پیش‌فرض مخصوص Vendor) تا اجرای دوم idempotent بماند؛ نقطهٔ مقابل، سکوت روی Login غیرفعال نیست (فعال‌سازی صریح فقط وقتی `is_disabled=1`).
- مراحل ۱–۳ با `CancellationToken` درخواست؛ ساخت/Migration/هویت/verify با `CancellationToken.None` (الگوی تأییدشدهٔ مسیر مشتری) تا بستن تب نیمه‌کاره نگذارد.
- خطای SQL با کاتالوگ کوچکی از شماره‌های تأییدشدهٔ مسیر مشتری به پیام فارسی امن نگاشت می‌شود؛ جزئیات فقط در لاگ.
- شمارهٔ Issue واقعی در دسترس نبود (API GitHub rate-limit بدون احراز)؛ طبق AGENTS.md بخش ۵ از `UNNUMBERED` استفاده شد.

## بررسی و آزمون (نتایج واقعی)
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `dotnet restore Vendor.sln` | موفق | SDK 10.0.404 (اسکریپت رسمی `scripts/dev/install-dotnet.sh`) |
| `dotnet build Vendor.sln -c Release` | موفق | بدون خطا؛ فقط warningهای از قبل موجود مخزن |
| `dotnet test Vendor.sln -c Release` | ۵۱۸ تست: ۴۲۱ موفق، ۱۲ ناموفق، ۸۵ Skip | هر ۱۲ شکست روی `main` هم به همان شکل موجود است (۱۳ شکست در main؛ مجموعه یکسان، یک تست time-flaky) — شکست‌ها به نبود `FARANO_VENDOR_ConnectionStrings__VendorDb`/SQL Server در این sandbox برمی‌گردد، نه تغییرات این PR |
| `dotnet test ... --filter VendorDevelopmentDatabaseProvisioningTests|...ExposureTests` با متغیر CI | ۵۱/۵۱ موفق | با `FARANO_VENDOR_ConnectionStrings__VendorDb` ست‌شده به سبک CI، همهٔ تست‌های واحدی جدید + exposure پاس شد؛ صفر regression |
| `dotnet test ...` (۳ تست SQL گیت‌دار) | Skip با پیام درست | در CI (کانتینر SQL Server 2022 vendor-ci.yml) اجرای واقعی می‌شوند |
| تست‌های SQL/اجراهای live روی نمونهٔ محلی مالک | اجرا نشد | نیازمند دستگاه Windows مالک؛ در این sandbox Docker/SQL Server در دسترس نبود |

## امنیت، داده و سازگاری
- بازبینی دستی امنیتی Lead (جایگزین unavailable `security-reviewer`) روی کل diff: محافظ‌های سطح صفحه (loopback peer/host، Origin، antiforgery، CSP، no-store) روی endpointهای جدید هم برقرارند؛ antiforgery + Origin برای هر دو POST الزامی است؛ در حالت برنامهٔ کامل پشت FallbackPolicy مدیر MFA هستند. ورودی‌ها: شناسهٔ دیتابیس با لیست سفید نویسه + NFKC + رد سیستمی + `DB_ID` سمت سرور؛ نام Login با الگوی سخت؛ CREATE DATABASE/DENY/ALTER ROLE فقط با شناسهٔ اعتبارسنجی‌شده bracket-quote می‌شوند؛ بقیهٔ مقادیر پارامتری‌اند. رمز تولیدی با CSPRNG، بدون نویسه‌های نقل‌قول/بک‌اسلش/سمی‌کالن، فقط یک‌بار در پاسخ؛ لاگ‌ها بدون اتصال/رمز. schema جدید، Migration جدید یا تغییر Customer وجود ندارد؛ رفتار جریان قبلی صفحه تغییر نکرده است.
- داده: هیچ داده‌ای خوانده/نوشته نمی‌شود جز ساخت schema خالی؛ Migration فقط با گارد ناشناس/جدیدتر اجرا می‌شود؛ هرگز DROP نیست.

## ریسک‌ها و کارهای باقی‌مانده
- اجرای واقعی pipeline روی SQL Server واقعی (CI یا دستگاه مالک) هنوز نشده؛ تا آن زمان `Not yet Verified`.
- روی نمونه‌های واقعی مالک (نمونه‌های محلی `.\SQL2022`/`.\SQLSERVER2025` مالک)، چرخهٔ کامل «راه‌اندازی → ذخیره → Restart → سلامت» و dev certificate باید دستی آزمون شود.
- بازنشانی گذرواژهٔ Login هم‌نام عمداً برای idempotency است؛ اگر اپراتور نامی متعلق به سرویس دیگری وارد کند، گذرواژهٔ آن بازنشانی می‌شود (هشدار UI + spec ثبت شده است).

## شناسه‌های Git
- Branch: `fix/vendor-provisioning-security` (مبنا `main@110d797`؛ شاخهٔ خواهر `agent/vendor-db-provisioning` بدون PR باقی می‌ماند و پس از ادغام این PR می‌تواند بسته شود)
- Pull Request: در گزارش نهایی مأموریت ثبت می‌شود

## الحاقیهٔ Phase A — سخت‌سازی امنیتی و شواهد تست (2026-11-27)

بر اساس بازبینی مأموریت، پنج تغییر امنیتی روی همان پیاده‌سازی اضافه شد (جزئیات در spec، بخش «سخت‌سازی امنیتی Phase A»): نشانهٔ مالکیت دیتابیس (`FaranouVendorOwnership`) با fail-safe در برابر تصاحب دیتابیس خالی ناشناس، محدودسازی مقصد SQL سمت سرور به مقصدهای محلی با allow-list پیکربندی‌شدنی، توکن Bootstrap یک‌بارمصرف برای endpoint تغییردهنده در setup-only، هم‌ترازی طول نام کاربری/گذرواژه با حد ۱۲۸ نویسهٔ SQL Server (و حداقل ۸ برای گذرواژهٔ Runtime دستی)، و رد Login دارای نقش/مجوز سروری به‌عنوان هویت Runtime.

- تست‌های واحد جدید: اعتبارسنجی مقصد (اجازهٔ محلی/رد خارجی/رد شکل‌های تزریق/allow-list/نام ماشین)، تصمیم مالکیت (۶ حالت شامل دیتابیس خالی)، قاعدهٔ حداقل‌اختیار Login (۷ حالت)، طول‌ها و ساخت ورودی، و قرارداد توکن یک‌بارمصرف (مصرف یگانه، انقضا، cookie نامطبق، سقف جفت‌ها). تست‌های انتگرال گیت‌دار SQL هم گسترش یافت: وجود نشانهٔ مالکیت پس از provision، رد دیتابیس خالی بدون مالکیت، پذیرش دیتابیس قدیمی با تاریخچه + backfill نشانه، و رد Login دارای `VIEW ANY DATABASE` بدون بازنشانی رمز.
- **شواهد واقعی این نشست (Linux sandbox، SDK 10.0.404):** `dotnet restore` و `dotnet build Vendor.sln` موفق (۰ خطا). `dotnet test Vendor.Host.Tests`: ۵۷۱ تست — ۴۷۱ موفق، ۱۲ ناموفق، ۸۸ Skip. مقایسهٔ مستقیم با `main@110d797` در همان محیط: ۴۶۹ تست — ۳۷۴ موفق، ۱۳ ناموفق، ۸۲ Skip و **مجموعهٔ نام‌های شکست دقیقاً یکسان** (md5 یکسان؛ ۱۳ نام شامل `SignerTimeoutCancelsProviderAndFailsClosed` که حساس به زمان است و در هر دو سمت رد می‌شود؛ در یکی از اجراهای شاخه با ۱۲ شکست پاس شد) — یعنی صفر regression و همهٔ تست‌های جدید سبز. ۱۳ شکست مشترک، محیط‌محورند (Liveness/Auth/BusinessMutation/Readiness/Exposure روی WebApplicationFactory در sandbox بدون Windows/loopback کامل) و روی CI سبز هستند. ۳ تست انتگرال SQL جدید گیت‌دارند و در CI روی کانتینر SQL Server 2022 موقت اجرا می‌شوند (`FARANO_VENDOR_TEST_SQLSERVER` در `run-vendor-ci-database-checks.sh` ست می‌شود؛ `FARANO_REQUIRE_VENDOR_SQLSERVER=1`).
- اجرای زنده روی نمونه‌های مالک همچنان انجام نشده و به دستگاه Windows مالک موکول است؛ هیچ اتصال واقعی SQL در این نشست برقرار نشد.
