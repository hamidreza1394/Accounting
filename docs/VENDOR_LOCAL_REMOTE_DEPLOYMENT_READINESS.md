# آمادگی استقرار محلی و راه دور Vendor فرانو

- **Issue:** [#768](https://github.com/hamidreza1394/Accounting/issues/768)
- **مبنای بررسی:** main@560ff2890be8bbf87795f30dd85f4eaf3cd1f131
- **وضعیت این سند:** نقشه اجرای مرحله‌ای؛ نه Runbook اجراشده و نه گواه Production readiness.
- **هدف:** اجرای مستقل Vendor در Visual Studio روی سیستم توسعه و استقرار آن روی Windows Server، با امکان تغییر endpoint بدون تغییر کد Customer.

## ۱) معماری مورد تأیید

Vendor یک Control Plane مستقل باقی می‌ماند و Customer Installation یک Data Plane مستقل است:

- Vendor مسئول مدیریت مشتری، Installation، سفارش/پرداخت، Provisioning، صدور و مدیریت License و API مرکزی است.
- Customer مسئول اجرای فرانو، نگهداری داده‌های عملیاتی، اعتبارسنجی License، Runtime Gate و اعمال Provisioning است.
- Vendor DB و Customer DB مستقل‌اند. Vendor نباید Connection String دیتابیس عملیاتی Customer را نگهداری کند یا مستقیماً به آن SQL بزند.
- برای V1 یک Vendor Host شامل API و Management UI کافی است؛ ایجاد Microservice، Message Broker یا سرویس ابری جدید بدون نیاز اثبات‌شده مجاز نیست.
- آدرس Vendor API در Customer باید تنظیم محیطی باشد. جابه‌جایی از localhost به IP/DNS نباید هویت Tenant، Installation، Customer یا License را تغییر دهد.

## ۲) وضعیت واقعی اجزای مرتبط در مبنای بررسی

| حوزه | وضعیت در main | پیامد برای استقرار |
|---|---|---|
| Solution مستقل | Vendor.sln در ریشه مخزن؛ Host در Vendor/Vendor.Host/Vendor.Host.csproj | پایه اجرای مستقل وجود دارد؛ اجرای واقعی در Visual Studio هنوز باید روی دستگاه توسعه اثبات شود |
| جداسازی Vendor/Customer | مرز پروژه و دیتابیس مستقل در معماری وجود دارد | در Staging باید دو دیتابیس مستقل با دو Connection String مجزا ساخته شوند |
| CNG مرکزی | Provider ویندوزی CNG/Certificate Store برای License و Provisioning پیاده شده و با Signing:CertificateStore:Enabled opt-in می‌شود | کلید واقعی، ACL هویت سرویس و Key Ceremony روی میزبان هدف هنوز عملیاتی تأیید نشده‌اند |
| PKCS#11 | مسیر مجزای D-055/D-056 برای دامنه‌هایی که D-107 supersede نکرده باقی است | PKCS#11/HSM پیش‌نیاز signer مرکزی License/Provisioning در Vendor V1 نیست |
| Publish guard (D-041/G2) | گارد رد Vendor DLL و الگوهای کلید وجود دارد؛ Allow-list کامل خروجی هنوز اثبات نشده | بسته انتشار مستقل Vendor هنوز معیار پذیرش کامل را ندارد |
| Deployment workflow (D-041/G5) | workflow مسیرمحدود Vendor CI وجود دارد؛ استقرار مقصد واقعی انتخاب/پیکربندی نشده | Staging deploy باید مستقل از CI و بدون signing key در CI طراحی شود |
| Provisioning v5 | Customer verifier، CompanyReference mapping و Apply اتمیک در کد موجود است؛ Gate Vendor همچنان fail-closed و بسته است | وجود Consumer به معنی مجازبودن صدور v5 یا بازکردن Gate نیست |
| Bootstrap | Enrollment موجود است؛ Bootstrap مستقل TenantOwner/Dedicated/Offline طبق D-110 هنوز NotImplemented است | این دو را یکی نگیریم؛ پیش از استفاده از این مسیر باید D-110 تکمیل شود |
| Trust و ACK | پایه verifier/ACK وجود دارد؛ توزیع/چرخش Trust و آزمون دو میزبان روی شبکه واقعی باقی است (#235) | یکپارچگی بین Vendor و Customer هنوز Operationally Verified نیست |
| Runtime Gate | پیاده‌سازی شده ولی Licensing:RuntimeGate:Enabled پیش‌فرض خاموش است | فقط در Staging ایزوله و پس از تکمیل پیش‌نیازها قابل آزمایش کنترل‌شده است |
| Audit Production | قرارداد و startup guard fail-closed موجود؛ sink مستقل Production provision نشده (#107) | Production readiness قابل ادعا نیست |
| Database routing | Routing foundation وجود دارد، اما اتصال کامل به مسیر عملیاتی DbContext در وضعیت بررسی‌شده اثبات نشده | Staging اول با توپولوژی فعال فعلی و یک DB برای هر Installation انجام شود؛ پشتیبانی عملیاتی همه توپولوژی‌ها ادعا نشود |

منابع وضعیت: docs/DECISIONS.md (D-041، D-055 تا D-058، D-107، D-110 تا D-113)، docs/LICENSING.md، docs/SECURITY.md، docs/VENDOR_OPERATIONAL_READINESS_CHECKLIST.md و Issueهای #107/#228/#235/#236/#392/#396.

## ۳) پروفایل‌های استقرار

### A. Local Development

- اجرای Vendor.sln در Visual Studio 2026 نسخه 18.0 یا بالاتر و .NET 10 SDK؛ مسیر Host اجرایی Vendor/Vendor.Host/Vendor.Host.csproj است.
- اجرای مستقیم Host از ریشه مخزن پس از تنظیمات: dotnet run --project Vendor/Vendor.Host/Vendor.Host.csproj.
- برای آماده‌سازی UI مستقل Vendor، دستورات تأییدشده CI عبارت‌اند از: در Vendor/Vendor.Web اجرای npm ci، npm run typecheck، npm test و npm run build؛ سپس از ریشه مخزن اجرای dotnet publish Vendor/Vendor.Host/Vendor.Host.csproj --configuration Release --output <absolute-output-path>. مسیر خروجی باید خارج از سورس و مجزا از Customer publish باشد.
- خروجی Vendor باید حداقل Faranou.Vendor.Host.dll، wwwroot/index.html و bundleهای JS/CSS در wwwroot/assets داشته باشد. CI این موارد را بررسی می‌کند و فایل‌های private-key با پسوندهای حساس یا PEM private-key marker را رد می‌کند؛ این بررسی جایگزین ممیزی کامل artifact و allow-list مستقل نیست.
- اجرای Host منتشرشده از پوشه خروجی: dotnet Faranou.Vendor.Host.dll. فرمان bootstrap-admin برای ساخت اولین اپراتور Vendor یک فرمان تعاملی و یک‌بارمصرف است؛ FARANO_VENDOR_Bootstrap__Secret باید از Secret Store/Environment و حداقل ۳۲ بایت باشد. این فرمان فقط مدیر Vendor را می‌سازد و به Bootstrap مدیر Customer طبق D-110 ارتباطی ندارد.
- Host فقط از Configuration با پیشوند FARANO_VENDOR_ استفاده می‌کند. کلید اتصال Vendor DB برابر ConnectionStrings:VendorDb و نام متغیر محیطی آن FARANO_VENDOR_ConnectionStrings__VendorDb است؛ هیچ مقدار پیش‌فرض یا fallback به Customer DB وجود ندارد.
- در Production، FARANO_VENDOR_DataProtection__KeysDirectory باید به مسیر absolute و پایدار با ACL محدود به هویت سرویس اشاره کند و FARANO_VENDOR_AllowedHosts باید فهرست صریح میزبان‌های مجاز باشد؛ wildcard پذیرفته نیست. تنظیمات و کلیدهای Data Protection بین Staging و Production مشترک نشوند.
- نمونه تنظیم موقت PowerShell (فقط placeholder؛ مقدار واقعی را در محیط خود وارد کنید): $env:FARANO_VENDOR_ConnectionStrings__VendorDb = '<Vendor-only SQL Server connection string>'. مقدار واقعی را در Git یا گزارش ثبت نکنید.
- اجرای Customer در پردازه/پورت مستقل؛ برای آزمون integration حداقل دو process مستقل لازم است.
- استفاده از دو دیتابیس آزمایشی مستقل، مثلاً FaranoVendor_Dev و FaranoCustomer_Dev؛ نام‌ها نمونه‌اند و باید با محیط واقعی هماهنگ شوند.
- برای EF Core نیز همان FARANO_VENDOR_ConnectionStrings__VendorDb باید فقط به Vendor DB اشاره کند. Migrationهای Vendor را با ابزار EF و پس از بازبینی فقط روی دیتابیس توسعه‌ای Vendor اجرا کنید؛ Migration مشتری Issue/مسیر مستقل خودش را دارد.
- در این مرحله از کلید و داده Production استفاده نشود.

### B. Staging روی Windows Server

- Host مستقل Vendor روی Windows Server یا Windows VM اختصاصی اجرا شود.
- Vendor DB و Customer DB از نظر نام، دسترسی، credential و backup جدا باشند.
- Endpoint عمومی فقط با HTTPS و گواهی معتبر استفاده شود؛ IP عمومی به گواهی دارای IP SAN نیاز دارد. Endpoint خصوصی فقط روی LAN/VPN مورد اعتماد با CA معتبر داخلی.
- SQL Server به اینترنت عمومی باز نشود؛ فقط Hostهای مجاز به آن دسترسی داشته باشند.
- Signing key آزمایشی Staging روی همان میزبان/هویت سرویس و در Certificate Store طبق D-107 آماده شود؛ private key فقط non-exportable MachineKey باشد.
- CNG فقط پس از اجرای Key Ceremony و بررسی ACL/Health روی میزبان هدف فعال شود. نبود گواهی/کلید یا خطای Provider باید fail-closed بماند.
- Customer endpoint از تنظیمات قابل تغییر باشد؛ تغییر endpoint نباید به تغییر Tenant/Installation binding منجر شود.
- در ابتدا دو process مستقل و دو دیتابیس SQL Server مستقل روی یک میزبان قابل قبول است؛ اما این نتیجه جای آزمون دو میزبان/دو OS و شبکه واقعی را نمی‌گیرد.

### C. Production

Production فقط پس از بسته‌شدن شواهد زیر قابل ارزیابی است:

- Key Ceremony و ACL سرویس CNG، Trust Store، rotation/overlap، revocation و recovery طبق #236.
- Trust distribution/rotation، consumer readiness و signed ACK در Customer طبق #235.
- Bootstrap اختصاصی TenantOwner طبق D-110/#392.
- Activation/Runtime Gate و online/offline sync طبق #396/#394.
- Delivery/ACK/Applied tracking برای Amendment طبق #393/#394 و تصمیم قراردادی مصوب.
- Production Audit Sink مستقل با identity/failure-domain جدا، retention، alerting و recovery drill طبق #107.
- Publish allow-list کامل، artifact inspection و استقرار تکرارپذیر طبق D-041/G2/G5.
- آزمون واقعی دو میزبان، partition/retry، restore/recovery و سناریوهای منفی امنیتی.

## ۴) قرارداد تنظیمات و Endpoint

اصل الزامی: Customer باید فقط به یک آدرس API قابل پیکربندی متصل شود؛ آدرس نباید در کد یا Build ثابت شود.

- مقدار endpoint در Configuration Provider محیط قرار می‌گیرد؛ secrets و connection strings از User Secrets/Environment/Secret Store خوانده می‌شوند.
- Local، Staging و Production باید configuration و trust material مجزا داشته باشند.
- تغییر Host/IP/DNS صرفاً تغییر محل اتصال است و نباید InstallationId، Tenant identity، LicenseId یا public-key binding را بازتولید کند.
- اعتبارسنجی TLS نباید خاموش شود؛ HTTP عمومی، گواهی self-signed بدون trust توزیع‌شده و اعتماد به Host/X-Forwarded-Host برای Tenant resolution ممنوع است.
- قبل از افزودن کلید تنظیمات یا API جدید، کد فعلی Customer transport در #394 و قرارداد D-112/D-113 باید بررسی شود تا تنظیم تکراری یا پروتکل دوم ساخته نشود.

D-112 و D-113 تصمیم‌های پذیرفته‌شده‌اند، اما قرارداد کامل Provisioning/Endpoint و گردش‌کار تغییر Endpoint هنوز باید در کد اثبات شود.

## ۵) ترتیب اجرای کار

1. **Local host baseline:** بررسی کامل پروژه‌های Vendor.sln، launch settings، config، migrationها و تست‌ها؛ Build/Run/Debug از Visual Studio.
2. **Vendor staging database:** ساخت دیتابیس جدا و اجرای migrationهای Vendor فقط روی آن؛ ثبت مسیر backup/restore.
3. **Customer staging database:** نصب Customer مستقل با DB جدا و topology فعال فعلی؛ عدم استفاده از DB واقعی.
4. **CNG Staging ceremony:** ساخت کلید آزمایشی واقعی روی Windows، ACL سرویس، ثبت public trust material در Customer و تست امضا/اعتبارسنجی.
5. **Signed lifecycle:** ایجاد مشتری/Installation، صدور بسته واقعی Staging، دریافت و اعتبارسنجی Customer، ACK امضاشده و مشاهده وضعیت از پنل Vendor.
6. **Negative tests:** امضای نامعتبر، کلید ناشناخته، Customer/Installation اشتباه، payload دستکاری‌شده، انقضا، replay، ACK تکراری/ناسازگار، signer unavailable، TLS failure و قطع شبکه.
7. **Remote host:** تکرار همان Runbook روی Windows Server دوم یا VM؛ سپس آزمون واقعی بین دو میزبان.
8. **Production review:** فقط پس از شواهد مستقل #107/#235/#236/#392/#393/#394/#396 و تأیید مالک.

## ۶) گاردهای امنیتی غیرقابل مذاکره

- private signing key هرگز وارد Git، CI، customer package، Vendor DB، Customer DB، log یا backup عادی نمی‌شود.
- CI فقط build/test/publish validation انجام می‌دهد؛ امضای License/Provisioning با Production key در CI ممنوع است.
- Staging و Production از نظر DB، keys، trust، endpoint و مشتری‌ها کاملاً جدا هستند.
- هیچ Gate یا fail-closed control برای سبزکردن تست غیرفعال نمی‌شود.
- هیچ Customer migration روی دیتابیس عملیاتی اجرا نمی‌شود؛ Migrationهای Customer فقط از Issue/PR مستقل و با backup/rollback plan.
- Signing:CertificateStore:Enabled opt-in است؛ صرف داشتن configuration به معنی آمادگی کلید یا مجوز فعال‌سازی نیست.
- Signing:Pkcs11:Enabled را با CNG signer مرکزی یکی نکنید؛ D-107 فقط مسیر مرکزی License/Provisioning/Amendment را پوشش می‌دهد.
- لاگ‌ها فقط metadata امن، status و correlation ID را نگه می‌دارند؛ secret، token، private key و payload حساس log نمی‌شوند.

## ۷) محدودیت‌های فعلی و معیار اعلام آمادگی

| سطح | معیار اعلام |
|---|---|
| Local runnable | Vendor از Visual Studio اجرا و دیباگ شده، با DB توسعه‌ای مستقل و بدون secret در repository |
| Staging deployable | Publish مستقل و allow-list معتبر، Host ویندوزی، تنظیمات جدا، CNG ceremony و تست واقعی API |
| Customer integrated | Customer مجوز واقعی امضاشده را با trust مستقل اعتبارسنجی و ACK امضاشده برمی‌گرداند |
| Operationally verified | آزمون دو میزبان، rotation/recovery، قطع شبکه و negative cases با شواهد قابل بازتولید |
| Production ready | علاوه بر موارد بالا، Audit sink مستقل، alerting، backup/restore، امنیت میزبان و تأیید نهایی مالک |

در تاریخ این سند، هیچ‌یک از سطوح Local runnable، Staging deployable، Customer integrated یا Operationally verified به‌عنوان اجرای عملی روی میزبان هدف اثبات نشده‌اند. این سند نقشه راه و معیار پذیرش است و نباید به‌عنوان گزارش اجرای موفق استفاده شود.
