# نقشه راه توسعه

> **بازبینی 2026-10-24 (فاز صفر لایسنس، مبنا `main`@`8c1e7a3`):** مرحله‌بندی L0..L4 به فاز 8 افزوده شد؛ چک‌باکس‌ها بدون تغییر ماندند چون هیچ کد لایسنس ساخته نشده است. شاهد: `docs/REPOSITORY_AUDIT.md` (ممیزی مجدد فاز صفر).
>
> **بازبینی 2026-10-21 (مبنا `main`@`1d39dbf`):** وضعیت چک‌باکس‌ها با کد و CI اجرای `39585708662` هم‌تراز شد. `[x]` فقط با شاهد (کد/تست/CI) است. ردیف‌های لایسنس/مشتری تجاری (فاز 8) همگی `NotImplemented` هستند. واژگان: `docs/INDEX.md`.

## فاز 0 — تثبیت اسکلت
- [x] ~~هدف‌گذاری پروژه روی .NET 9~~ — منسوخ: هدف رسمی `net10.0` است (D-007؛ `Directory.Build.props`، همه `.csproj`ها، `global.json` و CI).
- [x] مدیریت متمرکز نسخه NuGet
- [x] فعال‌کردن قواعد پایه C# و Nullable
- [x] افزودن تست پایه و CI
- [x] ایجاد مستندات Agent و معماری
- [x] ایجاد مرز پروژه Application و انتقال پروژه persistence به `Infrastructure/Persistence`
- [x] اجرای CI واقعی و رفع خطاهای کامپایل/Restore — شاهد: اجرای CI `39585708662` سبز (۶ Job).

## فاز 1 — امنیت و هویت
- [~] جایگزینی احراز هویت سفارشی: `PasswordHasher<T>` پذیرفته شد (Issue #30)؛ ASP.NET Core Identity کامل اتخاذ نشده (D-009).
- [x] تولید هش جدید با PBKDF2 (`PasswordHasher<T>`)؛ پذیرش هش قدیمی SHA-256 برای ارتقای تدریجی باقی است (حذف کامل در «فاز 7» منتظر تصمیم).
- [ ] مدل‌سازی کاربران، نقش‌ها و Permissionها؛ جلوگیری از مجوزدهی صرفاً بر اساس Claim ثابت
- [ ] بررسی CSRF، کوکی امن، Session، Lockout، Rate Limiting و خطاهای ورود
- [ ] تصمیم درباره Google OAuth: فعال‌سازی کامل با تنظیمات Secret، یا حذف endpoint/package بلااستفاده
- [ ] تست امنیتی و بازبینی OWASP Top 10

## فاز 2 — معماری
- [ ] افزودن/تثبیت لایه Application برای Use Caseها و قراردادها
- [ ] انتقال تدریجی دسترسی داده به Infrastructure بدون تغییر ناخواسته رفتار
- [ ] کوچک‌کردن PageModelها و انتقال منطق کاربردی به Handler/Serviceهای قابل تست
- [ ] تثبیت مرزهای وابستگی و بررسی جهت ارجاع پروژه‌ها
- [ ] افزودن Unit و Integration Test برای ورود، مجوزها، CRUD و داده

## فاز 3 — پایگاه داده
- [ ] بررسی مدل‌ها، کلیدها، ایندکس‌ها، Cascadeها و محدودیت‌ها
- [x] ایجاد فرایند رسمی EF Core Migrations — سه Migration و گاردهای CI وجود دارند (`docs/DATABASE.md`).
- [ ] تعریف سیاست داده‌های آزمایشی و Seed امن
- [ ] بررسی هم‌زمانی، تراکنش و مدیریت خطاهای SQL

## فاز 4 — کیفیت و تحویل
- [ ] افزودن تست‌های Integration و تست UI مناسب
- [ ] بررسی آسیب‌پذیری NuGet و فرانت‌اند در CI
- [ ] قالب PR، مالکیت کد و محافظت Branch
- [ ] ثبت نسخه‌ها و روش انتشار/بازگشت (rollback)
- [x] ارتقا به .NET 10 LTS (D-007).

## فاز 5 — آماده‌سازی محصول و محتوای قالب
- [ ] جایگزینی نام، عکس، لینک‌ها و اطلاعات تماس صاحب قالب در صفحات About/Contact/SEO با محتوای متعلق به محصول

## فاز 6 — برنامه محصول
پس از تثبیت اسکلت، نیازمندی‌های کسب‌وکار، ماژول‌ها، مدل دامنه، اولویت‌ها و معیار پذیرش در Issueهای مستقل تعریف شوند.


## فاز 1A — مدل چندشرکتی (Issue #16)
- [x] افزودن مدل شرکت/Tenant، عضویت کاربر در شرکت و دوره مالی.
- [x] افزودن نقش‌های سطح شرکت، کاتالوگ مجوزها، اتصال نقش-مجوز و رویدادهای امنیتی.
- [x] اتصال رشته اتصال به Configuration Provider استاندارد.
- [~] Migration تولید و در CI گارد شده است؛ حذف کامل مسیر قدیمی `EnsureCreated` و مسیر مهاجرت دیتابیس‌های قدیمی باید بررسی شود (`docs/database-migrations.md`).
- [ ] جایگزینی احراز هویت Legacy با ASP.NET Core Identity و مهاجرت حساب‌ها.
- [ ] پیاده‌سازی انتخاب شرکت/دوره و Tenant resolution سمت سرور — پیاده‌شده در PR ادغام‌نشده (D-031)؛ تیک پس از ادغام و CI سبز.
- [ ] اعمال کنترل دسترسی tenant-aware و تست نفوذ متقاطع شرکت‌ها.


## فاز 3A — راه‌اندازی اولیه دیتابیس (Issue #22)
- [x] فرم Razor Pages برای تنظیم سرور، دیتابیس و احراز هویت Windows/SQL.
- [x] تست اتصال و ذخیره رمزنگاری‌شده Connection String خارج از مخزن.
- [x] ساخت دیتابیس تازه و اعمال Migrationها؛ عدم اعمال خودکار Migration روی schema دارای جدول‌ها.
- [x] تأیید Build/Test/CI و SQL Server واقعی در CI (۲۰۴ تست روی SQL Server 2022، اجرای `39585708662`).
- [ ] بازبینی امنیت استقرار و کلیدهای Data Protection روی Windows Service/IIS/Linux.


## فاز 7 — مدرن‌سازی معماری و Frontend (2026-10-20)
- [x] سرویس سمت سرور دسترسی به شرکت و حذف Claimهای Tenant از Cookie (D-013).
- [x] API نسخه 1 هم‌مبدأ با Cookie و Anti-forgery (D-012).
- [x] کلاینت React/TypeScript/Vite/Tailwind راست‌چین در `/app` با داشبورد داده واقعی (D-011).
- [x] CI کلاینت (typecheck، تست، Build، بررسی منابع آفلاین، audit).
- [ ] OpenAPI برای `/api/v1` (نیازمند `Microsoft.AspNetCore.OpenApi`).
- [x] تست Integration HTTP با `WebApplicationFactory` و SQL Server 2022 واقعی (Service Container در CI).
- [ ] Global Query Filter Tenant همراه با نخستین ماژول حسابداری.
- [ ] انتقال صفحه ورود به React پس از تست‌های رگرسیون ظاهری (Screenshot) و رفتاری صفحه فعلی.
- [ ] انتقال Layout/منو و سپس صفحات مدیریت؛ حذف صفحات Razor فقط پس از اثبات عدم نیاز.
- [x] Rate Limiting برای API، نقاط حساس نشست و ارسال فرم ورود.
- [x] CSP اجباری برای `/app` و `/api`؛ CSP پایه برای Razor همراه با سیاست سخت‌گیرانه Report-Only.
- [x] اجباری‌کردن CSP سخت‌گیرانه برای Razor پس از انتقال inlineها (D-018؛ `/Admin/PageManager` به‌دلیل TinyMCE هنوز Report-Only).
- [ ] جایگزینی یا پیکربندی TinyMCE برای حذف استثنای Report-Only.
- [x] اعتبارسنجی مجدد نشست در هر درخواست (حساب غیرفعال/رمز تغییرکرده).
- [x] فرایند Publish قطعی همراه کلاینت و Smoke Test خروجی Publish در CI.
- [x] ابزار مهاجرت Master و امکان خاموش‌کردن قالب قدیمی Master (D-019).
- [ ] حذف کامل پذیرش هش SHA-256 قدیمی کاربران طبق برنامه `docs/SECURITY.md` (نیازمند تصمیم).
- [x] پیکربندی ForwardedHeaders فقط برای Proxyهای مورد اعتماد (D-017).
- [ ] تعیین مقادیر واقعی `AllowedHosts` و `ReverseProxy` محیط Production (پرسش‌های باز در `docs/DEPLOYMENT.md`).
- [x] تست E2E مرورگر با سرور منتشرشده و SQL Server واقعی در CI.
- [ ] Rate Limiting و کلیدهای Data Protection مشترک برای اجرای چندنمونه‌ای (در صورت نیاز، با ADR).
- [ ] ماژول‌های حسابداری: سرفصل حساب‌ها، اسناد با `decimal(18,2)`/`decimal(19,4)` و قواعد گردکردن مستند، گزارش‌ها. (`NotImplemented`)

## فاز 8 — مدل تجاری و لایسنس (قابلیت‌ها `NotImplemented`؛ وضعیت تصمیم‌ها جداگانه ثبت می‌شود)
هر مورد Issue و PR مستقل می‌شود؛ مرجع: `docs/LICENSING.md`، D-020..D-029.
- [ ] تأیید ERD و پرسش‌های باز `NeedsApproval` (`docs/LICENSING_DATA_MODEL.md`).
- [ ] `Licensing.Contracts` + بردارهای تست امضا (OD-01/02/12 در D-045 پذیرفته شده‌اند؛ پیاده‌سازی و اعتبارسنجی Linux/Windows هنوز انجام نشده؛ Issue #80).
- [ ] ماژول لایسنس نصب: ورود فایل، ارزیابی وضعیت، ممیزی، حالت محدودشده (D-023، D-026).
- [ ] نقطه مشترک سیاست Authorization: هویت + عضویت شرکت + لایسنس/زیرسیستم + مجوز عملیاتی (D-025).
- [ ] بررسی سقف تعداد شرکت هنگام ایجاد شرکت (D-022).
- [ ] سامانه فروشنده و سرویس صدور (Host/DB/هویت جدا؛ D-021).
- [ ] حذف کد مرده Activation قدیمی و تصمیم گذار برای نصب‌های موجود (D-028، نیازمند تأیید).
- [ ] تصمیم درباره فایل `LICENSE` مخزن و یکسان‌سازی نام محصول (REPOSITORY_AUDIT).

### مرحله‌بندی فاز 8 (فاز صفر 2026-10-24؛ `Proposed`، پیش‌نیاز: تأیید مالک)

هر مرحله یک یا چند PR کوچک و جدا با Issue مستقل است. ترتیب مأموریت: ممیزی و ثبت تصمیم ← هستهٔ لایسنس ← پنل مرکزی ← یکپارچه‌سازی حسابداری ← آزمون نصب آفلاین. تصمیم‌های باز: [`LICENSING_OPEN_DECISIONS.md`](LICENSING_OPEN_DECISIONS.md).

**L0 — ممیزی و ثبت تصمیم معماری (همین PR؛ فقط مستندات)**
- هدف: ممیزی مجدد، D-041..D-044، سند تصمیم‌های باز، گسترش مدل داده/گردش‌کار/تهدید. بدون کد.
- پذیرش: لینک‌ها و ارجاع ADRها معتبر، بدون Secret، بدون تغییر اجرایی؛ تأیید مالک.

**L1 — قراردادهای لایسنس و گاردهای جداسازی (تصمیم‌های OD-01/02/12 پذیرفته‌شده در D-045)**
- هدف و دامنه: پروژهٔ `Licensing.Contracts` (قالب پاکت، درخواست/پاسخ، اعتبارسنجی امضا فقط با کلید عمومی، ماشین حالت `Missing/Valid/ExpiringSoon/Grace/Restricted/Invalid`، `TimeProvider` تزریقی) + گاردهای G1–G3 در D-041. بدون DB، بدون API، بدون UI.
- وابستگی: OD-01/02/12 حل شده‌اند؛ نام پروژه و جزئیات دقیق قالب باید در Issue #80 مشخص و بازبینی شوند.
- فایل‌ها/پروژه‌های احتمالی: `Licensing.Contracts/**`، `tests/Licensing.Contracts.Tests/**`، `.github/workflows/dotnet.yml`، `scripts/publish.sh`/`.ps1`، `Template.sln`.
- زیر-PRهای پیشنهادی: (۱) گاردها و اسکن اسرار؛ (۲) انواع و اعتبارسنج؛ (۳) بردارهای تست روی Linux و Windows.
- آزمون: امضای معتبر/نامعتبر/دستکاری‌شده، `kid` و `formatVersion` ناشناخته، `sequence` قدیمی، `installationId` ناسازگار، مرزهای انقضا/ارفاق/محدود، عقب‌گرد ساعت؛ آزمون منفی گارد G1/G2.
- پذیرش: هیچ کلید خصوصی در مخزن؛ `Licensing.Contracts` به `Server`، EF و ASP.NET وابسته نیست؛ بردارهای تست روی Linux و Windows اجرا و ثبت شده‌اند (الگوریتم نهایی ECDSA P-256/SHA-256، IEEE P1363 64 بایت).
- ریسک امنیتی/مهاجرت: انتخاب الگوریتم؛ نشت کلید آزمایشی در بردارها (فقط کلید آزمایشی بی‌ارزش، با برچسب صریح)؛ **Migration ندارد**.
- مستندات: `LICENSING.md`، `LICENSING_THREAT_MODEL.md`، `TESTING.md`، `DEPLOYMENT.md`، D-045 (انتخاب الگوریتم پذیرفته شده؛ بردارهای آزمون هنوز باید اجرا شوند).

**L2 — سامانهٔ مرکزی فروشنده**
- معماری اجرایی: D-047 `Accepted` با تأیید صریح کارفرما (2026-10-25). PR #92 اسکلت Host مستقل را فراهم کرد؛ PRهای #101/#102 پایهٔ persistence و migration Identity/Audit را اضافه کردند؛ PR #104 schema مربوط به MFA/passkey را اضافه کرد. PR #112 (Issue #103) APIهای ورود MFA/passkey را پیاده‌سازی می‌کند و تا CI سبز/ادغام `NotVerified` است.
- هدف و دامنه: Host، دیتابیس و هویت مستقل؛ مدل `CustomerAccount/Contact/Product/Subsystem/Contract/SupportContract/License/LicenseRevision/Entitlement/Limit/Installation/ActivationRequest/CustomerBoundary/SigningKey/CentralAdminUser/AuditEvent`؛ سرویس امضا؛ صدور و ابطال دستی با فایل؛ ممیزی زنجیره‌ای؛ مدیر مرکزی با MFA. Endpoint آنلاین (D-042) مرحلهٔ بعدی همین فاز.
- وابستگی: قرارداد L1 در PR #82 ادغام شده؛ OD-07/09/11 در D-046 پذیرفته شده‌اند. D-047 مرز Host/DB/هویت/انتشار مستقل، MFA اجباری، bootstrap محدود، RBAC، maker-checker، ممیزی قابل‌کشف و امضای KMS/HSM را الزام می‌کند. وضعیت جاری: persistence Identity/Audit و migration passkey در source هستند؛ PR #112 APIهای password→TOTP/passkey و policyهای Cookie/antiforgery/rate-limit را اضافه می‌کند؛ PR #106 (audit writer/verifier) و PR #109 (SDK pin) باز هستند. Bootstrap امن: Issue #113؛ RBAC/maker-checker: Issue #114؛ UI مستقل RTL: Issue #115. هنوز bootstrap/recovery مدیر اول، RBAC ریزدانه، checkpoint خارجی، مدل تجاری مشتری/قرارداد/لایسنس، KMS/HSM، صدور لایسنس و deployment واقعی کامل نیستند. Maker-Checker تغییر وضعیت مشتری/نصب در PR #207 پیاده‌سازی و با CI تأیید شده؛ تأیید دو نفره برای صدور/ابطال لایسنس هنوز تکمیل نیست. هیچ کلید واقعی در توسعه/CI مجاز نیست.
- فایل‌ها/پروژه‌ها: `Vendor/**` (نام نهایی `NeedsApproval`)، `Vendor.sln`، Workflow استقرار جدا.
- آزمون: Integration روی SQL Server؛ MFA اجباری؛ maker-checker که تأیید درخواست خود را رد کند؛ مجوز نقش سمت سرور؛ ممیزی مقاوم در برابر دست‌کاری و کشف آن؛ صدور→اعتبارسنجی با `Licensing.Contracts`؛ قطع شبکه بدون توقف عملیات آفلاین.
- پذیرش: بستهٔ `Server` هیچ ارجاعی به فروشنده ندارد (G1/G2 سبز)؛ کلید خصوصی خارج از مخزن/CI/DB مشتری؛ کلید Production فقط از KMS/HSM با مجوز امضا؛ حداقل دو مدیر واجد شرایط پیش از Production؛ ارتباط آنلاین اختیاری.
- ریسک: مدیریت کلید و مراسم؛ Migration فقط روی دیتابیس **فروشنده** (دیتابیس مشتری دست‌نخورده).
- مستندات: `LICENSING_DATA_MODEL.md`، `SECURITY.md`، `DEPLOYMENT.md`، راهنمای عملیات صدور/چرخش کلید.

**L3 — یکپارچه‌سازی با نرم‌افزار حسابداری**
- هدف و دامنه: ماژول لایسنس نصب (جداول `InstallationIdentity/InstalledLicense/TrustedSigningKey/LicenseAuditEntry/LicenseTimeHighWater`)، ورود فایل، `EnforcementMode` (`Off/Observe/Enforce`)، نقطهٔ سیاست سمت سرور، سقف `maxCompanies`، حالت محدود، ساعت مؤثر، ویزارد ساخت اولین شرکت/مدیر (D-044)، سپس حذف کد مرده Activation (D-028) در PR جدا.
- وابستگی: L1 (و برای صدور واقعی L2)؛ OD-14، OD-04، OD-03، OD-05، OD-10، OD-15، OD-08.
- فایل‌ها: `Domain`، `Infrastructure/Persistence` (Migration **افزایشی** روی دیتابیس مشتری)، `Server/Api`، `Server/Infrastructure`، `ClientApp` (بازتاب وضعیت)، `tests/Template.Tests`.
- آزمون: ماتریس هویت × عضویت × لایسنس × مجوز × دورهٔ مالی؛ دورزدن مستقیم URL؛ درخواست هم‌زمان؛ `Observe` بدون تغییر رفتار؛ هیچ ردیف داده حذف نمی‌شود؛ ویزارد idempotent.
- ریسک: Migration روی دیتابیس مشتری باید با گاردهای CI و `docs/database-migrations.md` هماهنگ شود؛ ریسک قفل‌کردن مشتری قانونی (کاهش با `Observe` و حالت محدود).
- مستندات: `DATABASE.md`، `database-migrations.md`، `SECURITY.md`، `TESTING.md`، `PROJECT_STATUS.md`، handoff.

**L4 — آزمون نصب آفلاین و عملیات**
- هدف: سناریوی کامل: نصب تازه ← درخواست ← صدور ← ورود ← کار آفلاین ← انقضا/ارفاق/محدود ← تمدید ← بازیابی با و بدون کلید ← اتصال مجدد ← ابطال.
- وابستگی: L1–L3؛ OD-06، OD-08، OD-17.
- آزمون: E2E روی ماشین بدون شبکه (و در Windows)، تغییر ساعت، حذف لایسنس، Backup/Restore.
- پذیرش: هر سناریوی بخش ۸–۱۲ `LICENSING_WORKFLOWS.md` با شاهد؛ محدودیت‌های غیرقابل‌مهار (کپی VM، بازگردانی ماشین) صریح در مستندات قرارداد.
- مستندات: `DEPLOYMENT.md`، `TESTING.md`، راهنمای نصب و بازیابی.

راهنمای نشانه‌ها: `[x]` انجام‌شده با شاهد، `[~]` بخشی، `[ ]` انجام‌نشده.


## فاز 9 — آمادگی ظرفیت ابری و آزمون بار (Issue #49)

- [x] ممیزی اولیه مستندات/کد برای شکاف‌های ظرفیت؛ وضعیت 100 کاربر همچنان `NotVerified`.
- [ ] تعیین محیط staging، مشخصات میزبان و SQL Server، و مدل بار نماینده.
- [ ] انتخاب ابزار load test و افزودن سناریوهای قابل تکرار برای endpointهای موجود.
- [ ] ثبت baseline 100 VU و آزمون افزایش ظرفیت/soak؛ گزارش p50/p95/p99، throughput، خطا و سنجه‌های برنامه/SQL.
- [ ] اصلاح فقط گلوگاه‌های اثبات‌شده و اندازه‌گیری قبل/بعد.
- [ ] آزمون جداسازی tenant تحت بار و در آینده آزمون هم‌زمانی عملیات مالی پس از پیاده‌سازی ماژول‌های حسابداری.
- [ ] تعیین نیاز واقعی به scale-out و ثبت ADR برای هر تغییر معماری؛ بدون افزودن پیش‌دستانه microservices/Redis/Kubernetes.
- [ ] به‌روزرسانی وضعیت به `Verified` فقط با گزارش آزمون واقعی روی commit مشخص.

مرجع: `docs/CLOUD_CONCURRENCY_READINESS.md` و `docs/prompts/CLOUD_CONCURRENCY_IMPLEMENTATION_PROMPT.md`.

## فاز 10 — گروه شرکتی و تلفیق (D-033..D-035)
مرجع: `docs/CORPORATE_GROUPS.md` بخش ۸. وضعیت پس از هر PR با شاهد به‌روز شود.
- [x] مدل گروه، عضویت، مالکیت، پروفایل، سابقه و محدوده اجرا + Migration افزایشی (D-033) — شاخه `feature/corporate-groups-consolidation`، تست محلی روی SQL Server؛ ادغام‌نشده.
- [x] مصرف مجوز عملیاتی و سنجش دسترسی گروه (D-034) — همان شاخه.
- [ ] API و UI مدیریت گروه/عضویت/مالکیت و تخصیص مجوز به نقش‌ها.
- [ ] پیش‌نیاز: سرفصل حساب و سند (FR-AC-02)؛ سپس نگاشت کدینگ و گزارش تجمیعی مانده آزمایشی.
- [ ] تعدیلات تلفیقی، تطبیق درون‌گروهی، حذف معاملات داخلی، تبدیل ارز، دوره‌های ناهم‌زمان، منافع فاقد حق کنترل.
- [ ] گزارش‌گیری بین نصب‌ها با بسته امضاشده (D-035، نیازمند تأیید).


## تکمیل Licensing و Vendor بر اساس D-059 (2026-10-31)

ترتیب مصوب در Issue #389 و سند [LICENSING_IMPLEMENTATION_PLAN.md](LICENSING_IMPLEMENTATION_PLAN.md):

- [ ] **#395 — مستندسازی D-059 و هم‌ترازی ADR/مدل/گردش‌کار/تهدیدها**؛ این PR مستندات را ثبت می‌کند، اما merge و CI لازم است.
- [ ] **#390 — قرارداد نسخه‌دار Company-scoped Module/Feature/UserSeat و amendment**؛ حفظ LicenseId و sequence افزایشی.
- [ ] **#391 — enforcement سمت Customer**؛ seat block-new، feature over-limit، restricted term expiry، بدون حذف داده.
- [ ] **#392 — ایجاد Company و bootstrap مدیر Offline**؛ local-only password، Identity hash، یک‌بارمصرف و اتمیک.
- [ ] **#393 — Vendor add-on sale/issuance**؛ amendment امضاشده برای همان Installation/LicenseId.
- [ ] **#394 — online authenticated sync و signed offline file exchange**؛ stale telemetry واضح.

تست‌های واحد/Integration/CI برای هر PR الزامی‌اند. تست واقعی دو میزبان، TPM فیزیکی و recovery drill در انتهای پیاده‌سازی انجام می‌شوند. این چک‌لیست برنامه است، نه ادعای تکمیل.


## تکمیل تجربه نصب و معماری سه‌حوزه‌ای D-060

- [ ] **Vendor CRM & Commercial:** چرخه مشتری، قرارداد، سفارش، پرداخت، تمدید و پشتیبانی؛ مالک تجاری entitlement است نه signer.
- [ ] **Licensing & Entitlement Engine:** قرارداد نسخه‌دار، ظرفیت‌ها، ماژول‌ها، Featureها، amendment، امضا و ابطال.
- [ ] **Customer Deployment & Sync:** license-first startup، Activation Wizard، مدیر محلی، Cloud API، درخواست/پاسخ فایل آفلاین و ACK.
- [ ] **#396:** دروازه سمت سرور؛ بدون License به Wizard، با License معتبر به Login، Restricted به مسیر read/export/recovery و وضعیت نامعتبر به recovery/fail-closed.
- [ ] **Offline file exchange:** درخواست .request.json و پاسخ .license.json؛ .txt فقط wrapper Base64 بایت‌های دقیق signed package.

این موارد در D-060 Accepted هستند اما پیاده‌سازی و Verified محسوب نمی‌شوند. جزئیات در LICENSING_IMPLEMENTATION_PLAN.md.


## ماژول بازرگانی خارجی (D-061..D-063 / Issues #397 و #399)

مرجع اجرایی به‌روز، بخش «برنامه اجرایی ماژول بازرگانی خارجی — تصمیم‌های S0» در ادامه همین سند است. Foundation در PR #398 ادغام شده؛ دوره عملیاتی مستقل طبق D-062 ممنوع است؛ entitlement از S1 و گزارش عملیاتی چندشرکتی از موج اول طبق D-063 انجام می‌شود. اتصال مالی فقط از قراردادهای نسخه‌دار و بدون FK مستقیم انجام خواهد شد. استخراج Microservice Deferred است.

## برنامه اجرایی ماژول بازرگانی خارجی — تصمیم‌های S0 (D-063 / Issue #399)

### S0 — هم‌ترازی معماری و مستندات
- [ ] ثبت D-063: Voyage با یک شرکت مالک و allocation برای سایر شرکت‌ها؛ ImportCase چنددوره‌ای با اسناد دوره‌محور؛ گزارش عملیاتی چندشرکتی از موج اول؛ entitlement از S1.
- [ ] اصلاح اسناد تاریخی متعارض بدون بازنویسی D-061/D-062.
- [ ] ممیزی DI، design-time factory، migrations assembly/history و ثبت TradeDbContext؛ هیچ Migration مشتری اجرا نشود.
- [ ] تثبیت مدل master data tenant-scoped در برابر اسناد period-scoped و سیاست ادامه پرونده در دوره جدید.
- [ ] ثبت handoff و traceability؛ CI/تست فقط با شاهد واقعی به Verified ارتقا یابد.

### S1 — امنیت دسترسی و فعال‌سازی ماژول
- [ ] اتصال ITradeModuleEntitlement به Licensing runtime و entitlement شرکت‌محور TRADE_IMPORT؛ fail-closed در Production.
- [ ] تعریف PermissionDefinitions با کدهای پایدار فرم/عملیات/گزارش و seed ایدمپوتنت؛ عدم ایجاد role/permission store موازی.
- [ ] API و route gating سمت سرور؛ ترتیب کنترل نشست، membership، WorkContext/FiscalPeriod، entitlement، permission و سپس use case.
- [ ] تست منفی کاربر بدون entitlement، کاربر بدون permission، شرکت/دوره بیگانه، دوره Closed و دستکاری شناسه‌ها.
- [ ] مسیر ورود به Trade و نمایش منو/فرم براساس capabilityهای تأییدشده سرور؛ UI مرجع امنیت نیست.

### S2 — داده‌های پایه و زیرساخت persistence
- [ ] تکمیل TradeDbContext DI، design-time factory، migrations assembly و history table مستقل؛ بدون دیتابیس فیزیکی جدا.
- [ ] طراحی و Migration افزایشی schema trade فقط پس از review، تست SQL Server و rollout/rollback؛ هیچ Migration مشتری بدون تأیید مستقل اجرا نشود.
- [ ] فرم‌ها و APIهای Party، Product و lookupهای مرجع (Country, Port, Currency, Unit, Incoterm, Bank, PermitType) با scope شرکت و مجوز.
- [ ] پیاده‌سازی attachment storage پشت interface قابل استفاده در on-prem/cloud؛ provider نهایی پس از تصمیم جداگانه.

### S3 — درخواست خرید و پرونده واردات
- [ ] Purchase Request → Supplier Inquiry/Quotation → Comparison → Proforma → Contract → ImportCase.
- [ ] هر سند تراکنشی FiscalPeriodId دوره واقعی ثبت را نگه دارد؛ پرونده مادر می‌تواند چنددوره‌ای باشد.
- [ ] workflow/status و audit append-only با قواعد transition سمت سرور.

### S4 — جریان فیزیکی، حمل و گمرک
- [ ] Order Registration، permits، Voyage، Shipment، Bill of Lading، Warehouse Receipt، Customs Declaration، Exit/Delivery.
- [ ] یک Company مالک Voyage/Bill of Lading؛ allocation/share lines برای شرکت‌های دیگر؛ کنترل دسترسی به هر دو سطح.
- [ ] جداسازی مالکیت سند از سهم هزینه/کالا؛ جلوگیری از دسترسی ضمنی cross-tenant.

### S5 — ارز و بانک
- [ ] currency allocation، خرید ارز، حواله/انتقال، منشأ ارز، تعهدات و رفع تعهد.
- [ ] جریان ارز/بانک مستقل از وضعیت فیزیکی کالا و با event/status مستقل.

### S6 — هزینه، claims و گزارش
- [ ] landed cost، هزینه‌های مشترک و سهم‌بندی، claims و محاسبات با دقت decimal.
- [ ] گزارش تک‌شرکتی و عملیاتی چندشرکتی از موج اول؛ all-or-nothing scope، audit و تست عدم نشت.
- [ ] گزارش تلفیقی حسابداری تا پیاده‌سازی Ledger/Chart mapping خارج از محدوده است.

### S7 — مهاجرت داده و پایلوت
- [ ] ابزار import داده‌های اولیه و فایل‌ها با validation و گزارش خطا.
- [ ] آزمون سناریوهای end-to-end، کاربر/شرکت/دوره واقعی و بازیابی؛ پایلوت فقط پس از تکمیل کنترل‌های امنیتی و تأیید جداگانه.

**ترتیب پیاده‌سازی UI:** پس از آماده‌شدن S1/S2، ابتدا Party و Product؛ سپس Purchase Request، مقایسه استعلام/پیشنهاد و Proforma. فرم‌های بعدی به‌صورت vertical slice و نه scaffold یک‌جای همه فرم‌ها ساخته شوند.
