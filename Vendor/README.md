# سامانهٔ مرکزی فروشندهٔ فرانو

این پوشه میزبان مستقل سامانهٔ فروشنده است و از برنامهٔ حسابداری مشتری جدا نگه داشته می‌شود.

## وضعیت

- **پوستهٔ اولیهٔ Vendor UI در PR #127 ادغام شده است؛ این به معنی آمادگی Production نیست.** شاخهٔ `feat/issue-128-vendor-passkey-mfa-ui` اکنون ورود Passkey/WebAuthn، ثبت Passkey و راه‌اندازی/تأیید TOTP را به endpointهای موجود متصل می‌کند؛ این پیگیری هنوز CI تازه و بازبینی مرورگری لازم دارد. UI مستقل React/TypeScript strict/Vite/Tailwind با lockfile مستقل و assetهای محلی در `Vendor.Host/wwwroot` ساخته می‌شود. صفحات مشتری/نصب/لایسنس هنوز placeholder هستند و هیچ آمار تجاری ساختگی نمایش داده نمی‌شود.

- اسکلت اولیهٔ Host روی `net10.0` ایجاد شده است.
- `/health/live` فقط زنده‌بودن فرایند وب را اعلام می‌کند؛ وضعیت دیتابیس یا جزئیات عملیاتی را افشا نمی‌کند.
- لایهٔ persistence مستقل (`VendorDbContext`، جداول Identity مجزا، `VendorAuditEvents` و Migration اولیه) در PR #102 ادغام شده است؛ CI آن PR هماهنگی مدل/Snapshot را بررسی کرده است. Issue #117 پوشش تکمیلی اجرای Migration روی SQL Server واقعی و ثبت storeهای Identity را اضافه می‌کند. تا سبز شدن CI آن PR، اجرای Migration روی SQL Server در تست اختصاصی `NotVerified` است.
- APIهای MFA/passkey/TOTP و گارد عمومی دسترسی در Host وجود دارند. اجبار تغییر گذرواژهٔ اولیه در PR #160 و زیرساخت RBAC/گارد منبع‌محور در PR #162 ادغام شده‌اند. شاخهٔ Issue #163 اکنون موجودیت‌های VendorCustomer، VendorInstallation، VendorContract و VendorLicense، Migration مستقل Vendor، APIهای فهرست/جزئیات و ایجاد رکوردها، ممیزی تراکنشی عملیات ایجاد و evaluator منبع‌محور مبتنی بر رکورد پایدار را اضافه می‌کند؛ این شاخه هنوز CI نهایی و بازبینی امنیتی لازم دارد. ویرایش/چرخهٔ وضعیت کامل، UI تجاری، آزمون‌های cross-resource روی SQL Server، checkpoint مستقل، maker-checker، صدور امضای لایسنس و KMS/HSM هنوز کامل نیستند.
- این اسکلت برای Production آماده نیست.

## توسعه و آزمون

از ریشهٔ مخزن:

```bash
dotnet restore Vendor.sln
dotnet build Vendor.sln --no-restore --configuration Release
dotnet test Vendor.sln --configuration Release
dotnet run --project Vendor/Vendor.Host/Vendor.Host.csproj
```

پس از اجرا، مسیر `/health/live` باید HTTP 200 برگرداند.

## استقرار ابری و سرور اختصاصی

Host عمداً به ارائه‌دهندهٔ ابری خاصی وابسته نیست. در هر دو محیط:

- تنظیمات عملیاتی از Configuration Providers و متغیرهای محیطی/Secret Store محیط استقرار تأمین شوند.
- TLS باید در Host یا reverse proxy مورد اعتماد و مستندشده برقرار باشد.
- فقط proxyهای شناخته‌شده در صورت نیاز پیکربندی شوند؛ این اسکلت فعلاً forwarded headers را فعال نمی‌کند.
- حساب سرویس با حداقل مجوز اجرا شود و دسترسی شبکه/دیتابیس تا زمان طراحی مرحلهٔ بعدی باز نشود.
- کلید خصوصی امضا در این برنامه/مخزن قرار نمی‌گیرد؛ Production باید در مرحلهٔ مجزا با KMS/HSM و مجوز فقط‌امضا fail-closed شود.
- از انتشار عمومی endpointهای مدیریتی یا health checkهای جزئیات‌دار خودداری شود.

## مرز امنیتی

`Vendor.sln` مستقل است. برنامهٔ مشتری نباید به پروژه‌های Vendor ارجاع دهد؛ تنها قرارداد مشترک مجاز در مراحل بعدی `Licensing.Contracts` است. دیتابیس مشتری و Migrationهای آن خارج از این Solution باقی می‌مانند.


## CI و بستهٔ build
- `.github/workflows/vendor-ci.yml` به تغییرات Vendor/Contracts و تنظیمات build واکنش نشان می‌دهد و Restore/Build/Test مستقل را اجرا می‌کند.
- فقط push به `main` artifact خروجی `dotnet publish` را تولید می‌کند؛ artifact موقت ۷ روز نگه داشته می‌شود. این مرحله Deploy واقعی نیست و هیچ credential یا کلید امضایی استفاده نمی‌کند.
- workflow مستقل Vendor جایگزین گاردهای مشتری نیست؛ انتشار مشتری همچنان توسط `.github/workflows/dotnet.yml` و اسکریپت‌های publish اعتبارسنجی می‌شود.


## اتصال دیتابیس Vendor
- متغیر محیطی: `FARANO_VENDOR_ConnectionStrings__VendorDb` (به یک دیتابیس مستقل Vendor). `AddVendorPersistence` در مرحلهٔ Issue #117، DbContext و storeهای Identity را از همین اتصال ثبت می‌کند؛ هیچ fallback به تنظیم اتصال برنامهٔ مشتری مجاز نیست.
- هیچ connection string پیش‌فرض، seed مدیر، ثبت‌نام عمومی یا endpoint مدیریتی ناشناس وجود ندارد.
- قبل از استفادهٔ واقعی، پیاده‌سازی login/MFA و bootstrap باید تکمیل و کنترل‌های امنیتی/عملیاتی جداگانه تأیید شوند؛ وجود Schema به معنی آماده‌بودن پنل نیست.

## احراز هویت Vendor — Issue #103 (شاخهٔ در حال بازبینی)
- APIهای پایهٔ password→TOTP، passkey sign-in/enrollment، TOTP setup/confirmation، session/me و logout با ASP.NET Core Identity 10 در شاخه پیاده‌سازی شده‌اند؛ تا CI سبز و ادغام، Production-verified نیستند.
- password-only برای حساب فاقد MFA رد می‌شود؛ کوکی موقت TOTP به‌تنهایی مجوز دسترسی به APIهای محافظت‌شده نیست.
- Passkey assertion قبل از صدور نشست بررسی می‌شود؛ مدیر باید فعال باشد و شمارندهٔ credential به‌روزشده ذخیره شود.
- کوکی‌ها host-only با پیشوند __Host-، Secure، HttpOnly، SameSite=Strict و عمر کوتاه‌اند. همهٔ POSTهای auth ضدجعل درخواست دارند و تلاش‌های credential بر اساس IP محدود می‌شوند.
- Production به مسیر پایدار و مستقل Data Protection و دامنهٔ صریح passkey نیاز دارد؛ AllowedHosts wildcard ممنوع است.
- bootstrap نخستین مدیر، UI پنل، RBAC ریزدانه، maker-checker و سرویس checkpoint ممیزی هنوز باقی‌اند.


## Migration ثبت‌شده برای Passkey
- Migration `20261026045412_VendorAddPasskeySupport` و تغییرات `VendorDbContextModelSnapshot` اکنون در source ثبت شده‌اند؛ workflow دیگر Migration تولید نمی‌کند و فقط `dotnet ef migrations has-pending-model-changes` را اجرا می‌کند.
## زنجیرهٔ ممیزی Vendor — Issue #105
- شاخهٔ کاری `security/issue-105-vendor-audit-chain` سرویس `IVendorAuditWriter` و verifier را اضافه می‌کند؛ تا ادغام PR و CI موفق، این قابلیت در وضعیت `NotVerified` است.
- Writer فقط شناسه‌های محدود و متادیتای allow-listed می‌پذیرد؛ `MetadataJson` آزاد عمداً از درخواست حذف شده است. زمان از `TimeProvider` می‌آید و canonical hash نسخه‌دار SHA-256 روی فیلدهای ثابت و `PreviousHash` محاسبه می‌شود.
- append با `sp_getapplock` در تراکنش SQL Server سریال می‌شود تا چند نمونهٔ Host یک شاخهٔ واحد بسازند. verifier ریشهٔ چندگانه، والد گمشده، fork، چرخه، رخداد جداشده و تغییر محتوا را گزارش می‌کند.
- **محدودیت امنیتی:** زنجیره در همان دیتابیس مانع DBA دارای مجوز بازنویسی نمی‌شود. مجوز SQL مخصوص درج، اعتبارسنجی دوره‌ای و checkpoint/export مستقل به مقصدی با مجوز جداگانه همچنان قبل از استفادهٔ Production لازم‌اند.
- هیچ endpointی هنوز این writer را صدا نمی‌زند؛ wiring به Host پس از تکمیل مسیر ورود/MFA و bootstrap، در PR مستقل انجام می‌شود.

## رابط مستقل Vendor — Issue #126

- پروژه UI مستقل: Vendor/Vendor.Web؛ dependencyها و package-lock.json مستقل از ClientApp هستند. کامپوننت‌های shadcn/ui به‌صورت source-owned در خود پروژه Vendor نگهداری می‌شوند.
- توسعه محلی: از Vendor/Vendor.Web اجرا کنید: npm ci، سپس npm run dev. برای proxy به Host، مقدار FARANO_VENDOR_SERVER_URL را به آدرس HTTPS واقعی Host تنظیم کنید.
- Build: npm run typecheck، npm test و npm run build. خروجی build مستقیماً به Vendor/Vendor.Host/wwwroot نوشته می‌شود و در publish مستقل Host بسته‌بندی می‌شود.
- هیچ CSS/JS/font/icon از CDN بارگیری نمی‌شود. فعلاً فونت‌های سیستم استفاده می‌شوند تا Host مستقل به دارایی‌های مشتری وابسته نباشد.
- ورود از endpointهای /api/auth/antiforgery، /api/auth/password، /api/auth/totp/verify، /api/auth/me و /api/auth/logout استفاده می‌کند. توکن CSRF فقط در حافظه نگهداری می‌شود؛ credential یا توکن در Web Storage ذخیره نمی‌شود.
- Host فایل‌های استاتیک و SPA routeهای UI را سرو می‌کند، اما مسیرهای ناشناخته /api/* و /health/* به صفحه SPA بازنویسی نمی‌شوند.
- این UI به معنی تکمیل bootstrap، RBAC، maker-checker، audit checkpoint یا آمادگی Production نیست. قبل از فعال‌سازی عملیات تجاری، Issueهای امنیتی وابسته و APIهای واقعی باید تکمیل شوند.


### Vendor UI performance guardrails
- خروجی تولیدی Vite به‌صورت local bundle سرو می‌شود؛ CSS/JS/font/icon از CDN خارجی بارگیری نمی‌شود.
- فایل‌های hashدار `wwwroot/assets` با `Cache-Control: public,max-age=31536000,immutable` کش می‌شوند؛ `index.html` با `no-store` سرو می‌شود تا نسخه‌های جدید فوراً به assetهای hashدار جدید اشاره کنند.
- Vendor CI سقف gzip برای bundle اصلی JavaScript برابر 100 KiB و CSS برابر 20 KiB را کنترل می‌کند. اندازهٔ مشاهده‌شده در build قبلی 90.03 KiB JS و 7.29 KiB CSS بود؛ گارد تازه باید در CI بعدی تأیید شود.
- این اعداد اندازهٔ فایل هستند و جایگزین اندازه‌گیری Lighthouse، Core Web Vitals یا تست شبکه در مرورگر واقعی نیستند.


### پیگیری Issue #128 — احراز هویت و CI
- رابط passkey از WebAuthn بومی مرورگر و endpointهای موجود Host استفاده می‌کند؛ تبدیل Base64URL و serialization در UI انجام می‌شود و اعتبارسنجی نهایی همچنان مسئولیت سرور است.
- ثبت Passkey و راه‌اندازی/تأیید TOTP به نشست MFA معتبر نیاز دارند. کلید مشترک TOTP فقط در state صفحه نگه‌داری می‌شود و پس از تأیید یا لغو پاک می‌شود؛ پاسخ‌های auth/enrollment با `no-store` سرو می‌شوند.
- UI برای مرورگر ناسازگار/اتصال غیرامن پیام روشن می‌دهد؛ هیچ وابستگی runtime خارجی اضافه نشده است.
- Vendor CI در main روی run 40279335977 شکست خورد: تست SPA مسیر مشتری را اشتباهاً 404 انتظار داشت و connection string تست با نام دیتابیس مورد انتظار تست احراز هویت هم‌خوان نبود. Issue #128 هر دو را اصلاح می‌کند بدون حذف یا تضعیف تست.
- وضعیت این تغییرات تا اجرای Vendor CI روی PR #128: **Implemented in branch / Not yet Verified**.


## Issue #130 — استاندارد RTL فارسی و کمبوباکس قابل مدیریت
- فایل `Vendor/Vendor.Web/index.html` از قبل `lang="fa"` و `dir="rtl"` داشت؛ این مبنا حفظ شد.
- استایل Vendor اکنون جهت/تراز فارسی را صریح می‌کند و برای کنترل‌های جهت‌دار از خواص منطقی CSS استفاده می‌کند. نام کاربری با `dir="auto"` برای متن فارسی/لاتین و رمز عبور و OTP با جهت LTR تعریف می‌شوند تا تایپ فنی به‌هم نریزد.
- کامپوننت `ManagedSelect` برای فرم‌های Vendor اضافه شد: انتخاب اولین گزینه معتبر به‌صورت پیش‌فرض، امکان مقدار کنترل‌شده، label و پیام خطای دسترس‌پذیر، و نمایش «افزودن گزینه» فقط با `canManageOptions` و callback صریح.
- این کامپوننت خودش گزینه‌ها را ذخیره نمی‌کند؛ مجوز باید از API سمت سرور بیاید و callback باید به endpoint واقعی متصل شود. هیچ API یا CRUD تجاری جعلی در این مرحله اضافه نشده است.
- مرز معماری: شرکت‌های حسابداری (از جمله مستقل/مادر/زیرمجموعه) متعلق به دیتابیس نصب مشتری‌اند، نه دیتابیس تجاری Vendor. Vendor مشتری تجاری، نصب و مجوز را مدیریت می‌کند. ایجاد شرکت حسابداری از پنل مرکزی بدون طرح provisioning مستقل و تأییدشده انجام نمی‌شود.
- تست‌های جدید برای جهت RTL، پیش‌فرض اولین گزینه، تغییر انتخاب، پنهان بودن مدیریت گزینه بدون مجوز و پیام خطا اضافه شده‌اند.
- اعتبارسنجی محلی در این محیط اجرا نشد؛ وضعیت تا نتیجه Vendor CI روی PR **Implemented / Not yet Verified** است.


## بازبینی پس از ادغام‌ها — ثبت Identity
- در `main` یک ثبت `AddIdentityCore<VendorAdminUser>` در `Vendor.Persistence/DependencyInjection.cs` و ثبت دوم در `Vendor.Host/Program.cs` مشاهده شد. شاخهٔ `fix/vendor-identity-registration-single-source` ثبت تکراری Host را حذف کرده و تنظیمات Identity و providerهای لازم را در Persistence متمرکز می‌کند.
- تا اجرای Vendor CI و integration testهای SQL Server، این اصلاح **Not verified** است؛ Production-ready اعلام نمی‌شود.

- **اصلاح migration Passkey:** بازبینی پس از merge نشان داد فایل Designer migration مربوط به Passkey در `main` وجود نداشت؛ PR #132 آن را اضافه کرده و تست SQL Server را برای اعمال migration و وجود جدول Passkey گسترش می‌دهد. تا Vendor CI سبز نشود، این اصلاح تأییدشده نیست.

## راه‌اندازی امن اولین مدیر Vendor — Issue #113
- دستور `bootstrap-admin` یک حالت command-mode است؛ هیچ endpoint عمومی برای ساخت مدیر اضافه نشده است و در این حالت HTTP Host اجرا نمی‌شود.
- پیش‌نیازها: دیتابیس مستقل Vendor از قبل با Migrationهای Vendor آماده باشد؛ `FARANO_VENDOR_ConnectionStrings__VendorDb` و راز تصادفی یک‌باره `FARANO_VENDOR_Bootstrap__Secret` از Secret Store/Environment تأمین شوند. راز باید حداقل ۳۲ بایت باشد و هرگز به‌عنوان آرگومان command یا در فایل tracked قرار نگیرد.
- فرمان: `dotnet Faranou.Vendor.Host.dll bootstrap-admin` (از پوشهٔ انتشار Vendor). راز bootstrap و گذرواژه با ورودی مخفی دریافت می‌شوند؛ نام کاربری و ایمیل تعاملی هستند. URI حاوی TOTP فقط در کنسول تعاملی نمایش داده می‌شود.
- حساب ابتدا غیرفعال ساخته می‌شود. تنها پس از ثبت کلید authenticator و تأیید کد واقعی TOTP، `TwoFactorEnabled` و `IsEnabled` فعال می‌شوند. pending account به نشست مدیریتی دسترسی ندارد.
- SQL Server application lock عملیات bootstrap را بین چند فرایند سریال می‌کند. اگر فرایند هنگام ثبت TOTP قطع شود، حساب pending باقی می‌ماند؛ اجرای مجدد با همان نام کاربری و گذرواژه می‌تواند enrollment را بازیابی کند. بعد از تکمیل موفق، اجرای مجدد fail-closed است.
- رویدادهای pending-created، TOTP-rejected و completed به زنجیرهٔ ممیزی Vendor افزوده می‌شوند. راز bootstrap، گذرواژه و کد TOTP در audit/log نوشته نمی‌شوند.
- این مرحله به‌تنهایی سامانه را Production-ready نمی‌کند: RBAC ریزدانه، maker-checker، checkpoint مستقل ممیزی، تغییر اجباری گذرواژه (در صورت نیاز)، مدیریت مشتری/قرارداد/لایسنس و KMS/HSM هنوز مسیرهای مستقل دارند. هیچ Migration یا دیتابیس مشتری تغییر نمی‌کند.
