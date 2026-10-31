# سامانهٔ مرکزی فروشندهٔ فرانو

## وضعیت جاری Vendor — 2026-10-31 (Issue #214 PR pending)

- **Offline revocation در شاخهٔ کاری:** مسیر maker-checker برای `TargetStatus=Revoked` اکنون فقط پس از ساخت و verify شدن snapshot canonical، ذخیرهٔ envelope، تغییر License، تصمیم approval و audit در transaction واحد موفق می‌شود. این تغییر هنوز merge/CI-verified نیست.
- **توالی و تاریخچه:** sequence با قفل تراکنشی SQL Server و unique index کنترل می‌شود؛ snapshotها immutable و به approval و license محرک متصل‌اند. export از `POST /api/vendor/offline-revocations/latest/export` با antiforgery، مجوز `LicensesRead`، audit و no-store انجام می‌شود.
- **پیکربندی لازم:** `OfflineRevocation:SnapshotValidityDays` باید صریحاً ۱..۳۶۵ باشد؛ trust set مستقل `OfflineRevocationSigningTrust:Keys` فقط public SPKI دارد. نبود policy/trust یا signer معتبر به fail-closed/503 منجر می‌شود.
- **امضاکننده:** `UnavailableOfflineRevocationSigningProvider` پیش‌فرض باقی است؛ PKCS#11 adapter و KeyId purpose-specific در main وجود دارند، اما هیچ KMS/HSM/credential واقعی در این PR انتخاب یا provision نمی‌شود.
- **مرز:** این PR فقط Vendor control plane و Vendor DB را تغییر می‌دهد. customer-side consumer/enforcement در #235، checkpoint مستقل ممیزی در #107 و provisioning واقعی کلید در #236 همچنان جداگانه‌اند.

> وضعیت CI این شاخه تا پایان اجرا در حال بررسی است؛ تست محلی .NET/SQL Server اجرا نشده است.

---


## Snapshot تاریخی — وضعیت Vendor (2026-10-28، پس از PR #226)

- **UI و CRUD تجاری:** مدیریت مشتری، نصب، قرارداد Draft و metadata لایسنس به API واقعی Vendor متصل است؛ کنترل‌های سمت سرور شامل permission/resource authorization، antiforgery، RowVersion و audit هستند.
- **Maker-checker و صدور لایسنس:** PRهای #217–#224 در main ادغام شده‌اند. پاکت امضاشده، key ID، الگوریتم، هش payload و sequence در دیتابیس Vendor ذخیره می‌شوند؛ وضعیت لایسنس، تصمیم approval و رویدادهای audit در تراکنش واحد ثبت می‌شوند. تست SQL Server برای صدور موفق، rollback در شکست audit، تعارض sequence و رقابت هم‌زمان وجود دارد.
- **Signer تولیدی هنوز موجود نیست:** `UnavailableLicenseSigningProvider` پیش‌فرض fail-closed است. تأیید صدور تا زمان پیکربندی KMS/HSM یا سرویس امضای امن، به‌درستی با 503 متوقف می‌شود. هیچ کلید خصوصی در repo/CI/artifact/customer install قرار نمی‌گیرد.
- **ابطال آفلاین هنوز پیاده‌سازی نشده:** تغییر Status در Vendor به‌تنهایی لایسنس صادرشدهٔ آفلاین را باطل نمی‌کند. پروتکل امضاشدهٔ ابطال و اعتبارسنجی در مصرف‌کنندهٔ لایسنس، مانع جداگانهٔ انتشار است.
- **آزمون E2E و امنیت:** PR #226 مشکل race در navigation آزمون Playwright ورود را رفع کرد؛ .NET CI شامل Browser tests روی SQL Server، publish Linux/Windows، build/test، secret scan و تست‌های کلاینت موفق است.
- **Audit:** زنجیرهٔ hash و کنترل‌های نسخه‌بندی موجودند؛ checkpoint مستقل immutable با credential جدا، runbook، backup/restore و کنترل عملیاتی هنوز Production blocker هستند.
- **مرز داده:** پنل Vendor فقط control-plane تجاری خودش را مدیریت می‌کند. ایجاد شرکت حسابداری، دیتابیس یا اعتبارنامهٔ مشتری از این فرم‌ها انجام نمی‌شود و نیازمند معماری provisioning مستقل است.

این پوشه میزبان مستقل سامانهٔ فروشنده است و از برنامهٔ حسابداری مشتری جدا نگه داشته می‌شود.

> بندهای زیر سابقهٔ توسعه‌اند؛ برای وضعیت تأییدشدهٔ فعلی به بخش بالای همین سند رجوع کنید.

## وضعیت

- **پوستهٔ اولیهٔ Vendor UI در PR #127 ادغام شده است؛ این به معنی آمادگی Production نیست.** شاخهٔ `feat/issue-128-vendor-passkey-mfa-ui` اکنون ورود Passkey/WebAuthn، ثبت Passkey و راه‌اندازی/تأیید TOTP را به endpointهای موجود متصل می‌کند؛ این پیگیری هنوز CI تازه و بازبینی مرورگری لازم دارد. UI مستقل React/TypeScript strict/Vite/Tailwind با lockfile مستقل و assetهای محلی در `Vendor.Host/wwwroot` ساخته می‌شود. فهرست‌ها و عملیات مشتری/نصب/قرارداد Draft/metadata لایسنس Draft به API واقعی متصل‌اند؛ ویرایش metadata قرارداد و لایسنس فقط در Draft مجاز است. هیچ آمار تجاری ساختگی نمایش داده نمی‌شود.

- اسکلت اولیهٔ Host روی `net10.0` ایجاد شده است.
- `/health/live` فقط زنده‌بودن فرایند وب را اعلام می‌کند؛ وضعیت دیتابیس یا جزئیات عملیاتی را افشا نمی‌کند.
- لایهٔ persistence مستقل (`VendorDbContext`، جداول Identity مجزا، `VendorAuditEvents` و Migration اولیه) در PR #102 ادغام شده است؛ CI آن PR هماهنگی مدل/Snapshot را بررسی کرده است. Issue #117 پوشش تکمیلی اجرای Migration روی SQL Server واقعی و ثبت storeهای Identity را اضافه می‌کند. تا سبز شدن CI آن PR، اجرای Migration روی SQL Server در تست اختصاصی `NotVerified` است.
- APIهای MFA/passkey/TOTP و گارد عمومی دسترسی در Host وجود دارند. اجبار تغییر گذرواژهٔ اولیه در PR #160 و زیرساخت RBAC/گارد منبع‌محور در PR #162 ادغام شده‌اند. PR #164 ادغام شده است: موجودیت‌های VendorCustomer، VendorInstallation، VendorContract و VendorLicense، Migration مستقل Vendor، APIهای فهرست/جزئیات و ایجاد رکوردها، ممیزی تراکنشی عملیات ایجاد و evaluator منبع‌محور مبتنی بر رکورد پایدار در main قرار دارند. صفحه‌بندی bounded برای endpointهای فهرست در main قرار دارد. Issue #168 لبه‌های offset/ترتیب را اصلاح می‌کند و Issue #169 اتصال فهرست‌های تجاری به UI واقعی را در حال انجام دارد؛ هیچ‌کدام تا CI سبز Verified محسوب نمی‌شوند. Issue #171 شاخهٔ ویرایش امن با RowVersion و انتقال محدود وضعیت مشتری/نصب را پیاده‌سازی می‌کند؛ تا CI سبز و ادغام، این قابلیت Verified نیست. UI تجاری در PR #170 است و CI آن سبز شده اما هنوز Draft باقی مانده (تبدیل به Ready در اتصال GitHub با خطای مجوز رد شد). آزمون‌های کامل cross-resource، checkpoint مستقل، maker-checker، صدور امضای لایسنس و KMS/HSM هنوز کامل نیستند.
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


### قرارداد صفحه‌بندی APIهای فهرست (Issue #165)
- endpointهای فهرست مشتری، نصب، قرارداد و لایسنس از `page` و `pageSize` پشتیبانی می‌کنند؛ پیش‌فرض صفحه 1 و اندازه 25 است و اندازه صفحه حداکثر 100 خواهد بود.
- پاسخ صفحه‌بندی شامل `items`, `page`, `pageSize`, `totalCount`, `totalPages`, `hasPreviousPage` و `hasNextPage` است. مصرف‌کننده‌ها باید پاسخ را به‌عنوان envelope صفحه‌بندی بخوانند، نه آرایه خام.
- وضعیت: قرارداد صفحه‌بندی در main موجود است؛ اصلاحات مرزی در PR #168 و اتصال UI در Issue #169 جداگانه در حال بررسی‌اند. این تغییرها Migration دیتابیس مشتری ایجاد نمی‌کنند.


## Audit chain lookup indexes (Issue #202)
- Migration 20261027190000_VendorAuditChainIndexes adds IX_VendorAuditEvents_PreviousHash and unique IX_VendorAuditEvents_EventHash for Vendor-only audit chain lookups. PR #203 merged this migration after the SQL Server integration suite and EF pending-model-change guard passed in Vendor CI and .NET CI.


## متادیتای حق لایسنس — پیگیری تکمیلی

- شاخهٔ `feat/vendor-license-entitlements` فیلدهای `LicenseKind`، `MaxCompanies`، `SubsystemsJson`، `GraceDays` و `LastIssuedSequence` را به مدل مستقل Vendor اضافه می‌کند. Migration فقط جدول `VendorLicenses` در دیتابیس Vendor را تغییر می‌دهد و هیچ جدول یا Migration حسابداری مشتری را لمس نمی‌کند.
- نوع `term` الزاماً تاریخ پایان دارد؛ نوع `perpetual` تاریخ پایان ندارد. این سازگاری در اعتبارسنجی API و Check Constraint دیتابیس اعمال می‌شود. سقف شرکت، بازهٔ مهلت ارفاق و فهرست زیرسیستم‌های مجاز نیز اعتبارسنجی می‌شوند.
- UI فارسی RTL اطلاعات خریداری‌شده را در فهرست و فرم نمایش می‌دهد؛ فرم ویرایش مقدارهای موجود را بازیابی می‌کند و API هم ورودی را مستقل از اعتبارسنجی مرورگر بررسی می‌کند.
- این قابلیت صرفاً **مدیریت metadata و entitlement** است؛ به‌تنهایی لایسنس رمزنگاری‌شده صادر نمی‌کند، امضای دیجیتال نمی‌سازد، فایل فعال‌سازی آفلاین تولید نمی‌کند و به مشتری اجازهٔ اجرا نمی‌دهد. قبل از Production همچنان سرویس امضای KMS/HSM، صدور/ابطال کنترل‌شده، Maker-Checker برای لایسنس، تست فعال‌سازی آفلاین و checkpoint مستقل ممیزی لازم است.
- وضعیت CI باید از runهای جاری شاخه بررسی شود؛ افزودن migration به سورس، جایگزین اجرای واقعی SQL Server و تأیید `dotnet ef migrations has-pending-model-changes` نیست.

## Maker-Checker تغییر وضعیت مشتری/نصب — Issue #201
- PR #207 ادغام شده و CI کامل Vendor/.NET سبز است. endpointهای مستقیم قدیمی با `approval_required` پاسخ می‌دهند؛ ثبت درخواست، تصمیم مدیر مستقل، منع self-approval، RowVersion و Audit تراکنشی در API پیاده‌سازی شده‌اند.
- صف تأیید فارسی RTL صفحه‌بندی server-side دارد. این کار فقط تغییر وضعیت مشتری/نصب را پوشش می‌دهد؛ Maker-Checker صدور/ابطال لایسنس تا تکمیل سرویس امضا همچنان باقی است.
- شواهد: [Vendor CI 40570240409](https://github.com/hamidreza1394/Accounting/actions/runs/40570240409)، [.NET CI 40570240205](https://github.com/hamidreza1394/Accounting/actions/runs/40570240205).


### Licensing v1 signing pipeline status
- `VendorLicensePayloadBuilder` validates Vendor license and active-installation metadata before creating a deterministic contract payload.
- `VendorLicenseEnvelopeFactory` (PR #220) passes exact canonical payload bytes to the injected `ILicenseSigningProvider`, checks provider metadata, and constructs a structurally valid envelope. It does not persist or issue the license.
- The production approval endpoint remains fail-closed until durable Vendor-only envelope persistence, transactional sequence/state changes, approval integration and a configured production KMS/HSM or remote signer are implemented and verified. Never ship a private key or add a software-key fallback.

## Customer provisioning status (Issue #228)

The customer setup form records a versioned provisioning request in the separate Vendor database. It collects the company setup, initial administrator username, target installation, and purchased subsystem selection. It does not collect or persist a customer password.

Requests currently remain `AwaitingSigner`. This is deliberate: until a production signing provider and a trusted customer-side consumer are configured, no signed package is delivered and the UI must not claim that a customer company or administrator has been created. The API persists an idempotent request ID, unique nonce, canonical payload/hash, audit event, and status metadata in the Vendor database only.

Production completion still requires a trusted key source outside Git, signing and one-time delivery, an idempotent customer-side consumer with atomic nonce claiming, signed acknowledgement, and recovery/retry tests for cloud, dedicated-server, and offline installations. Those capabilities are not implemented by the current Vendor UI/API slice.

## Provisioning issuance workflow (Issue #236)

Provisioning requests can now be submitted to the existing two-person approval workflow. The reviewer must be a different Vendor administrator from the requester. Approval calls the dedicated `IProvisioningSigningProvider` with the exact canonical payload bytes and stores the signing metadata with the approval decision in one Vendor database transaction. Provisioning and license issuance have separate provider registrations and must use distinct signing keys. The package export endpoint is an antiforgery-protected POST available only for `PendingDelivery` requests. It checks customer and installation read authorization, records an audit event in the same transaction as the first export timestamp, and uses `Cache-Control: no-store`. The UI exposes the two-person approval action and downloads the exported signed JSON package for offline transfer.

The default provider remains `UnavailableLicenseSigningProvider`, so issuance fails closed with a generic 503 unless the opt-in PKCS#11 adapter is explicitly configured against a real hardware token. The adapter does not generate keys or make private key material exportable. With the current zero HSM budget, leave `Signing:Pkcs11:Enabled` unset/false; Production signing stays unavailable. A trusted customer-side consumer and signed acknowledgement are tracked separately in #235.


## Production audit checkpoint readiness (Issue #107)

The Vendor host fails startup in the Production environment unless an explicit `IProductionVendorAuditCheckpointStore` adapter is registered. Before the host becomes ready, a hosted startup probe verifies the complete local audit chain against the independent checkpoint store and appends a new checkpoint when the chain has advanced. A required read/append/provider failure aborts startup with a generic message; provider exception text is not exposed. When the latest checkpoint already covers the current chain, startup verifies the read path without forcing a redundant append; write permission is exercised when an initial or advancing checkpoint must be appended. A hash chain stored only in the same Vendor SQL Server database is not an independent checkpoint.

Until a real provider is selected and configured with separate credentials and appropriate append-only/immutability retention, the Vendor control plane is **not production-ready** and must not be launched with a fake store, local-file fallback, or Vendor-database fallback. Development and tests can run without the provider.


### Dedicated provisioning signer boundary (Issue #233)

Provisioning issuance now depends on `IProvisioningSigningProvider`, registered separately from `ILicenseSigningProvider`. The default `UnavailableProvisioningSigningProvider` fails closed. This interface separation prevents accidental reuse of the license-signing provider/key, but does not itself implement a production signer. A dedicated external KMS/HSM or protected signing service, trusted key ID/rotation configuration, signature verification, recovery runbook, and operational tests remain required before provisioning can be enabled in Production.


## Vendor PKCS#11 signing adapter — Issue #236 (D-055 Accepted)

The Vendor Host now contains an opt-in PKCS#11 ECDSA P-256 adapter behind the existing `ILicenseSigningProvider`, `IProvisioningSigningProvider`, and `IOfflineRevocationSigningProvider` interfaces. License, provisioning/package, and offline-revocation signing use three separate versioned KeyIds and distinct PKCS#11 private-key objects. The adapter signs the SHA-256 digest of the exact canonical payload using `CKM_ECDSA`, requires a 64-byte IEEE P1363 result, and verifies that the private-key object is sensitive, non-extractable, always-sensitive, never-extractable, sign-enabled, and configured for the P-256 curve. It never generates or exports private keys.

Enablement is opt-in through `FARANO_VENDOR_Signing__Pkcs11__Enabled=true`. Configure the module path, token label, explicit assurance level (`Software` or `Hardware`), license KeyId, provisioning KeyId, offline-revocation KeyId, and PIN through deployment environment/secret injection; never place the PIN in tracked settings. Hardware assurance additionally requires the approved token manufacturer/model allowlist. KeyIds must be unique and versioned. The HSM Key Ceremony must create private-key objects with both CKA_LABEL and CKA_ID set to the exact KeyId, then register their public keys in the corresponding Vendor/customer trust stores before issuance is enabled.

Configuration names include `FARANO_VENDOR_Signing__Pkcs11__AssuranceLevel` (`Software` or `Hardware`), `FARANO_VENDOR_Signing__Pkcs11__AcceptSoftwareKeyRisk` (Production only), `FARANO_VENDOR_Signing__Pkcs11__ModulePath`, `...__TokenLabel`, `...__ApprovedTokenManufacturer`, `...__ApprovedTokenModel`, `...__LicenseKey__KeyId`, `...__ProvisioningKey__KeyId`, `...__OfflineRevocationKey__KeyId`, and `...__UserPin`. Supply the PIN only through the deployment secret/environment mechanism; do not add it to `appsettings*.json`, repository variables, or the package.

With `AssuranceLevel=Software`, a software PKCS#11 token (including SoftHSM) is accepted in Development and Staging. Production rejects Software assurance unless `AcceptSoftwareKeyRisk=true` is explicitly set; every such Production startup writes a metadata-only audit event before serving. With `AssuranceLevel=Hardware`, the selected slot must report the hardware-slot flag and its manufacturer/model must exactly match the deployment allowlist. The assurance level is logged at startup and exposed in `/health/signing`. Missing/unknown assurance values fail closed. The default adapter remains disabled/unavailable; do not enable Production signing without completing the approved key ceremony and trust-store configuration. `/health/signing` reports 503 when the provider is disabled or unavailable. Every signing attempt appends a metadata-only Vendor audit action containing purpose, versioned public KeyId, and result; payload bytes and private material are never audited. Audit failure discards the signature and fails closed.

The module serializes token login/session operations because PKCS#11 login state is token-scoped. If the target HSM supports safe parallel sessions, concurrency tuning must be validated against that specific hardware before changing this guard.


## Issue #107 — Production audit checkpoint operations

- Production startup requires exactly one IProductionVendorAuditCheckpointStore; no local file, Vendor SQL or fake fallback is permitted.
- Startup verifies the complete audit chain against the independent checkpoint before the host becomes ready.
- A background service re-verifies/advances the checkpoint every 300 seconds by default. Configure AuditCheckpoint:IntervalSeconds (30–3600) and AuditCheckpoint:MaximumStalenessSeconds (at least two intervals, default 900).
- Production exposes /health/audit-checkpoint with only sanitized Healthy/Unhealthy status. External monitoring must alert on Unhealthy, timeout or non-200.
- scripts/sql/vendor-audit-runtime-permissions.sql configures the runtime principal as append-only on dbo.VendorAuditEvents while keeping migration credentials separate. Run only after DBA review.
- The independent provider, its credential boundary, retention/immutability policy, notification receiver and restore drill remain deployment blockers. Do not claim Production audit readiness until they are selected and evidenced.
