# طراحی راه‌اندازی کامل خودکار دیتابیس Vendor در صفحه Development

- **تاریخ:** 2026-11-27
- **پایه:** ادامهٔ Issue #797 / PR #800 (فرم اتصال `VendorDb` فقط در Development)
- **وضعیت:** پیاده‌سازی‌شده در شاخه؛ تا CI و اجرای واقعی روی SQL Server، `Implemented / Not yet Verified`.

## مسئله

فرم قبلی فقط «اتصال موجود» را آزمون و ذخیره می‌کرد و صریحاً هیچ دیتابیسی نمی‌ساخت و Migration اجرا نمی‌کرد. اپراتور توسعه باید دیتابیس Vendor را دستی در SSMS می‌ساخت و Migration را با ابزار EF اجرا می‌کرد، و بدتر: اگر با هویت مدیریتی (مثل `sa`) اتصال را ذخیره می‌کرد، برنامهٔ Vendor با همان هویت پراختیار همه‌وقت کار می‌کرد.

## تصمیم طراحی

دو endpoint جدید و یک پنل UI به همان صفحهٔ `development/database-setup` اضافه می‌شود و یک جریان ۷ مرحله‌ای اجرا می‌کند:

1. **connection** — اعتبارسنجی ورودی و ساخت اتصال موقت «هویت پیکربندی» (Windows یا SQL Login پراختیار).
2. **test** — آزمون اتصال به خود سرور (کاتالوگ `master`).
3. **inspect** — بازرسی: نسخهٔ سرور، وجود دیتابیس هدف، تعداد جدول‌های کاربر، وجود `__EFMigrationsHistory`، شمارش Migrationهای اعمال‌شده/معوق/ناشناس، وجود Login با نام Runtime. نام دیتابیس سمت سرور با `DB_ID(@name)` هم چک می‌شود (۱ تا ۴ = سیستمی، حتی برای نام‌های هم‌ریخت).
4. **createDatabase** — اگر دیتابیس نبود با `CREATE DATABASE [<نام اعتبارسنجی‌شده>]` ساخته می‌شود (CommandTimeout=۱۲۰s). اگر بود، مرحله «بی‌تغییر» می‌خورد و ادامه می‌دهیم؛ **هرگز DROP یا بازسازی انجام نمی‌شود** و خطای 1801 (ساخت هم‌زمان) خطا نیست.
5. **migrate** — Migrationهای موجود EF Core (`VendorDbContext.Database.MigrateAsync`) با هویت پیکربندی روی دیتابیس هدف. دو گارد ایمنی قبل از هر DDL: دیتابیس با جدول کاربر اما بدون تاریخچهٔ فارنو → «دیتابیس ناشناس» و اجرا نمی‌شود؛ تاریخچه با شناسه‌های ناشناس → «جدیدتر از برنامه» و اجرا نمی‌شود.
6. **runtimeIdentity** — ساخت/تنظیم یک SQL Login کم‌اختیار (پیش‌فرض `FaranouVendorRuntime`) با `CREATE LOGIN`/`ALTER LOGIN` + `CHECK_POLICY=ON`، mapping کاربر فقط در دیتابیس هدف با نقش‌های `db_datareader`/`db_datawriter`، `DENY VIEW ANY DATABASE` برای Login تازه‌ساخته، بدون db_owner و بدون هیچ نقش سروری و بدون هیچ دسترسی به دیتابیس دیگر (پیش‌فرض SQL Server نبودِ دسترسی است). اگر Login هم‌نام از قبل باشد، گذرواژه‌اش با مقدار فرم بازنشانی می‌شود (در UI هشدار صریح دارد). اگر Login غیرفعال باشد فعال می‌شود.
7. **verify** — اتصال با خود اتصال Runtime: `DB_NAME()` = هدف، `SUSER_SNAME()` = Login، `IS_ROLEMEMBER('db_owner')` ≠ 1، و `GetPendingMigrationsAsync` خالی از دید Runtime.

در پایان، **اتصال Runtime کم‌اختیار** (نه هویت پیکربندی) در Windows Credential Manager ذخیره می‌شود (همان کلید `Faranou.Vendor.Development.VendorDb`)، مشمول همان گارد منبع (اگر تنظیم از Environment/Configuration صریح بیاید، چیزی ذخیره نمی‌شود و در پاسخ اعلام می‌شود). اجرای بعدی Vendor همان را می‌خواند؛ منبع واحد حقیقت همچنان `FARANO_VENDOR_ConnectionStrings__VendorDb` است.

## گزینه‌های بررسی‌شده

- **اجرای Migration با ابزار CLI در اسکریپت جدا:** به صفحه وابستگی بیرونی و ترتیب اجرا می‌افزود و گزارش مرحله‌به‌مرحله نمی‌داد؛ رد شد.
- **ذخیرهٔ اتصال پیکربندی (نه Runtime) مثل فرم قبلی:** هویت پراختیار می‌ماند؛ برخلاف هدف جداسازی هویت؛ رد شد.
- **ساخت ViewModel/Context جدا برای Runtime:** الگوی طراحی‌زمان `VendorDbContextFactory` و ثبت `AddVendorPersistence` از طریق `DbContextOptionsBuilder` مستقیم کافی است؛ abstraction جدید لازم نیست (KISS/YAGNI).

## قرارداد API و تجربهٔ کاربری

- `POST /api/development/database-setup/inspect` (مراحل ۱–۳، فقط‌خواندنی؛ rate limit مشترک `vendor-development-setup`) و `POST /api/development/database-setup/provision` (هر ۷ مرحله + ذخیره؛ policy جدید سخت‌گیرتر `vendor-development-setup-provision` با ۳ درخواست در دقیقه).
- بدنه: `{server, database, authentication, username, password, trustServerCertificate, runtimeUsername?, runtimePassword?}`. پاسخ: `stages: [{key,state,message}]` با ترتیب ثابت، `runtimeUsername`، `generatedPassword` (فقط وقتی سرور ساخته، یک‌بار)، `savedToCredentialManager`، `message`؛ برای inspect: وضعیت دیتابیس و Migrationها.
- UI فارسی RTL: همان فرم اتصال + پنل «هویت Runtime کم‌اختیار» + دکمهٔ «بررسی وضعیت سرور و دیتابیس» و «راه‌اندازی کامل دیتابیس» + فهرست ۷ مرحله با وضعیت (در انتظار/موفق/بی‌تغییر/ناموفق) + جعبهٔ یک‌بارنمایش گذرواژهٔ ساخته‌شده. متن پانوشت قبلی که می‌گفت «دیتابیس ایجاد نمی‌کند» اصلاح شد.
- حالت‌های نسخه‌بندی پاسخ stage: `pending` فقط برای مراحل اجرانشدهٔ یک تلاش ناموفق.

## مرز امنیت

- همان محافظ‌های صفحهٔ #797: فقط Development + Windows، فقط loopback (peer و Host)، فقط HTTPS، Origin دقیق هم‌میزبان، antiforgery، هدرهای امنیتی و CSP؛ در حالت برنامهٔ کامل همهٔ endpointها پشت سیاست پیش‌فرض Vendor (مدیر MFA) می‌مانند.
- شناسهٔ دیتابیس با قواعد تأییدشدهٔ مسیر مشتری (حرف/رقم/`_`/`-`/`.`/فاصلهٔ میانی؛ بدون `\ / : * ? " < > | ; ' [ ]`، بدون نویسهٔ کنترلی/قالب (ZWNJ)، بدون نقطه/فاصلهٔ ابتدا-انتها و `..`، حداکثر ۱۲۳ نویسه، NFKC و نام‌های سیستمی و هم‌ریخت رد) اعتبارسنجی و بعد bracket-quote می‌شود؛ نام Login فقط الگوی سخت ASCII؛ همهٔ lookupها پارامتری‌اند.
- `CancellationToken` درخواست فقط مراحل ۱–۳ را لغو می‌کند؛ CREATE DATABASE/Migration/hویت Runtime/verify با `CancellationToken.None` اجرا می‌شوند تا بستن تب، عملیات نیمه‌کاره نگذارد.
- لاگ فقط نام مراحل/شکست را دارد؛ connection string و رمز هرگز لاگ یا در پیام خطا برنمی‌گردند؛ گذرواژهٔ ساخته‌شده فقط یک‌بار در پاسخ موفق می‌آید.
- `SemaphoreSlim` یک‌زمانی: درخواست موازی دومی فوراً «راه‌اندازی دیگر در حال اجراست» می‌گیرد.
- **هیچ** تغییری به Customer، `Server/**`، Migrationهای مشتری، schema جدید، کلید یا Windows Service اضافه نمی‌شود؛ جریان فقط روی دیتابیس هدف خودش کار می‌کند و به هیچ دیتابیس دیگری وصل نمی‌شود.

## سخت‌سازی امنیتی Phase A (همان روز، بازبینی مأموریت)

- **مالکیت دیتابیس (Marker):** در زمان ایجاد کنترل‌شده، بلافاصله پس از `CREATE DATABASE` یک Extended Property سطح دیتابیس با نام `FaranouVendorOwnership` روی دیتابیس هدف نوشته می‌شود. Migration فقط روی دیتابیسی اجرا می‌شود که این نشانه را دارد یا تاریخچهٔ Migration اعمال‌شدهٔ فارنو مالکیتش را اثبات می‌کند (دیتابیس‌های قدیمی پیش از نشانه، پس از Migration موفق نشانه را backfill می‌گیرند). **دیتابیس خالیِ موجود بدون نشانه و بدون تاریخچه هرگز تصاحب نمی‌شود** — یک دیتابیس خالی ممکن است برای هدف دیگری (از جمله Customer) ساخته شده باشد؛ پیام رد، انتخاب نام اختصاصی جدید را راهنمایی می‌کند. مسیر «ساخت هم‌زمان» (خطای ۱۸۰۱) هم اگر مالکیت قابل اثبات نباشد رد می‌شود و بازرسی وضعیت مالکیت را در پاسخ inspect برمی‌گرداند (`ownershipMarkerPresent`).
- **محدودسازی مقصد SQL:** نام سرور/نمونه سمت سرور به مقصدهای محلی محدود می‌شود (`localhost`، `127.0.0.1`، `::1`، `.`، `(local)`، `(localdb)`، نام خود ماشین؛ شکل‌های `host`، `host,port`، `host\instance[,port]` و پیشوند `tcp:` پشتیبانی می‌شود و بقیهٔ شکل‌ها/تلاش‌های تزریق رد می‌شوند). هیچ نامی از سیستم مالک در کد hardcode نشده است؛ برای هدف تأییدشدهٔ دیگر (مثلاً Staging)، مالک می‌تواند میزبان را در `FARANO_VENDOR_Development__Provisioning__AllowedSqlHosts` یا کلید پیکربندی `Faranou:Development:Provisioning:AllowedSqlHosts` اضافه کند. همین قاعده روی endpointهای test/save هم اعمال می‌شود تا UX یکدست بماند.
- **Bootstrap یک‌بارمصرف:** در حالت setup-only هیچ هویتی برای احراز وجود ندارد؛ endpoint تغییردهندهٔ `provision` علاوه بر loopback + Origin + antiforgery + rate limit، یک توکن یک‌بارمصرف کوتاه‌عمر (۱۵ دقیقه) می‌طلبد که صفحه محلی از `GET /api/development/database-setup/provision-token` می‌گیرد و با یک Cookie سخت‌گیرانه (HttpOnly، Secure، SameSite=Strict) جفت شده است. جفت‌ها فقط به‌صورت SHA-256 در حافظه نگه داشته می‌شوند (سقف ۱۶ جفت)، در Log/URL/storage پایدار نمی‌آیند و مصرف هر جفت یک‌بار است؛ تلاش مجدد/تکرار fail-closed می‌شود.
- **سازگاری طول‌ها با SQL Server:** نام کاربری و گذرواژهٔ SQL (پیکربندی و Runtime) حداکثر ۱۲۸ نویسه (حد sysname/گذرواژهٔ SQL Login) و گذرواژهٔ Runtime دستی حداقل ۸ نویسه — همه پیش از هر SQL رد می‌شوند تا وسط pipeline به خطای سرور نخورند؛ اندازه پارامترها (NVarChar(128)) با همین حد هم‌تراز است.
- **رد Login دارای مجوز سرور:** اگر Login هم‌نام Runtime از قبل عضو هر نقش ثابت سرور باشد یا هر مجوز سروری غیر از `CONNECT SQL` داشته باشد، به‌عنوان هویت کم‌اختیار رد می‌شود (بدون بازنشانی رمز و بدون ادغام خاموش)؛ پیام رد، تصمیم مدیر سرور را لازم می‌داند.

## مواردی که پس از پیاده‌سازی باید بررسی شوند

Vendor CI سه تست انتگرال گیت‌دار جدید (`VendorDevelopmentDatabaseProvisioningSqlServerTests`) را روی کانتینر SQL Server 2022 واقعی اجرا می‌کند (`FARANO_VENDOR_TEST_SQLSERVER`). اجرای زنده روی نمونه‌های محلی `.\SQL2022`/`.\SQLSERVER2025` مالک و چرخهٔ Restart کامل (ذخیره → اجرای مجدد → سلامت) همچنان بررسی دستی مالک روی دستگاه Windows است.
