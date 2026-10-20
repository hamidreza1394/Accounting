# نقشه راه توسعه

## فاز 0 — تثبیت اسکلت
- [x] ~~هدف‌گذاری پروژه روی .NET 9~~ — منسوخ: هدف رسمی `net10.0` است (D-007؛ `Directory.Build.props`، همه `.csproj`ها، `global.json` و CI).
- [x] مدیریت متمرکز نسخه NuGet
- [x] فعال‌کردن قواعد پایه C# و Nullable
- [x] افزودن تست پایه و CI
- [x] ایجاد مستندات Agent و معماری
- [x] ایجاد مرز پروژه Application و انتقال پروژه persistence به `Infrastructure/Persistence`
- [ ] اجرای CI واقعی و رفع خطاهای کامپایل/Restore

## فاز 1 — امنیت و هویت
- [ ] جایگزینی احراز هویت سفارشی با ASP.NET Core Identity یا `IPasswordHasher<TUser>`
- [ ] حذف SHA-256 خام برای رمز عبور و افزودن الگوریتم استاندارد salt/work factor
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
- [ ] ایجاد فرایند رسمی EF Core Migrations
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
- [ ] تولید و بازبینی Migration سازگار با دیتابیس فعلی و حذف مرحله‌ای `EnsureCreated`.
- [ ] جایگزینی احراز هویت Legacy با ASP.NET Core Identity و مهاجرت حساب‌ها.
- [ ] پیاده‌سازی انتخاب شرکت/دوره و Tenant resolution سمت سرور.
- [ ] اعمال کنترل دسترسی tenant-aware و تست نفوذ متقاطع شرکت‌ها.


## فاز 3A — راه‌اندازی اولیه دیتابیس (Issue #22)
- [x] فرم Razor Pages برای تنظیم سرور، دیتابیس و احراز هویت Windows/SQL.
- [x] تست اتصال و ذخیره رمزنگاری‌شده Connection String خارج از مخزن.
- [x] ساخت دیتابیس تازه و اعمال Migrationها؛ عدم اعمال خودکار Migration روی schema دارای جدول‌ها.
- [ ] تأیید Build/Test/CI و تست عملی با SQL Server واقعی.
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
- [ ] ماژول‌های حسابداری: سرفصل حساب‌ها، اسناد با `decimal(18,2)`/`decimal(19,4)` و قواعد گردکردن مستند، گزارش‌ها.
