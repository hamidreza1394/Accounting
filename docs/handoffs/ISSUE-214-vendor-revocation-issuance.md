# تحویل وظیفه — ISSUE-214-vendor-revocation-issuance

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/214
- **Branch:** `feat/issue-214-revocation-snapshot-factory`
- **تاریخ:** 2026-10-31
- **وضعیت:** PR #375 باز؛ پیاده‌سازی Vendor در همان PR گسترش یافته و منتظر CI نهایی و بازبینی

## هدف و محدوده
تکمیل بخش Vendor control-plane از گردش ابطال آفلاین: approval دو نفره باید تنها پس از تولید، اعتبارسنجی و ذخیرهٔ یک snapshot امضاشده بتواند License را به `Revoked` تغییر دهد. این PR هیچ consumer سمت مشتری، `Server/**` یا migration دیتابیس مشتری را تغییر نمی‌دهد.

## تغییرات
- مدل immutable `VendorSignedRevocationSnapshot` و migration مستقل Vendor با unique sequence، ListId، ApprovalRequestId و TriggeringLicenseId اضافه شد.
- `VendorOfflineRevocationIssuanceService` فهرست کامل licenseهای Revoked را با GUIDهای canonical و ترتیب ordinal می‌سازد، sequence را با `sp_getapplock` تراکنشی serialize می‌کند و payload را با قرارداد مشترک `OfflineRevocationListValidator` canonicalize می‌کند.
- امضای اختصاصی `IOfflineRevocationSigningProvider` فقط با KeyId موجود در trust set مستقل و public key متناظر پذیرفته می‌شود؛ signature، algorithm، format، expiry و sequence دوباره اعتبارسنجی می‌شوند.
- تغییر وضعیت License، approval، snapshot و سه رخداد audit در همان transaction ذخیره می‌شوند. شکست signer، trust، concurrency یا audit اجازهٔ commit نمی‌دهد.
- شاخهٔ approval برای `TargetStatus=Revoked` اکنون سرویس فوق را فراخوانی می‌کند؛ مسیر قبلی 503 عمومی فقط تا زمان آماده شدن artifact بود.
- endpoint `POST /api/vendor/offline-revocations/latest/export` با antiforgery، مجوز `LicensesRead`، audit و `Cache-Control: no-store` آخرین envelope را تحویل می‌دهد.
- trust store revocation از trust storeهای license و provisioning مستقل است. خالی/ناشناخته/retired بودن KeyId fail-closed است.

## پیکربندی صریح
- `OfflineRevocation:SnapshotValidityDays` باید صریحاً بین ۱ تا ۳۶۵ تنظیم شود؛ مقدار صفر/ناموجود به 503 منجر می‌شود و هیچ مدت انقضای تجاری حدس زده نشده است.
- `OfflineRevocationSigningTrust:Keys` فقط شامل KeyId و SPKI عمومی است؛ کلید خصوصی/credential در repo یا artifact قرار نمی‌گیرد.
- signer پیش‌فرض همچنان `UnavailableOfflineRevocationSigningProvider` است. PKCS#11 فقط با پیکربندی موجود و کلید اختصاصی provision‌شده قابل استفاده است؛ این PR هیچ provider یا credential عملیاتی را انتخاب/جعل نمی‌کند.

## تست‌ها
| آزمون | نتیجه در این شاخه |
|---|---|
| SQL Server success: snapshot + license + approval + audit | افزوده شد؛ منتظر Vendor CI |
| SQL Server signer unavailable: عدم تغییر state | افزوده شد؛ منتظر Vendor CI |
| SQL Server audit failure: rollback کامل | افزوده شد؛ منتظر Vendor CI |
| SQL Server concurrent revocations: sequence 1/2 و cumulative list | افزوده شد؛ منتظر Vendor CI |
| trust store خالی/کلید retired | افزوده شد؛ منتظر Vendor CI |
| تست محلی .NET/SQL Server | اجرا نشده؛ checkout/SDK/SQL Server محلی در این نشست در دسترس نیست |

## ریسک‌ها و مرزهای باز
- این PR صدور و export در Vendor را پیاده می‌کند؛ enforcement واقعی فقط پس از اتصال consumer به مسیرهای customer runtime در Issue #235 کامل است و آن کار جدا می‌ماند.
- HSM/KMS واقعی، credential/identity عملیاتی، مراسم کلید و recovery هنوز نیازمند تصمیم/دسترسی است؛ default fail-closed حفظ شده است.
- snapshot validity یک سیاست استقرار اجباری و بدون مقدار پیش‌فرض است.
- Issue #107 هنوز به sink مستقل ممیزی با credential/retention جدا نیاز دارد.

## فایل‌های اصلی
- `Vendor/Vendor.Persistence/Domain/VendorSignedRevocationSnapshot.cs`
- `Vendor/Vendor.Persistence/VendorDbContext.cs`
- `Vendor/Vendor.Persistence/Migrations/20261031083000_VendorSignedRevocationSnapshots*`
- `Vendor/Vendor.Host/Signing/VendorOfflineRevocationIssuanceService.cs`
- `Vendor/Vendor.Host/Authorization/VendorOfflineRevocationEndpoints.cs`
- `Vendor/Vendor.Host/Authorization/VendorApprovalEndpoints.cs`
- `tests/Vendor.Host.Tests/VendorOfflineRevocationIssuanceTests.cs`
- `docs/WORK_LOG.md`, `docs/PROJECT_STATUS.md`, `Vendor/README.md`

## Git
- **PR:** https://github.com/hamidreza1394/Accounting/pull/375
- **Branch:** `feat/issue-214-revocation-snapshot-factory`
- **وضعیت merge:** انجام نشده؛ هیچ بررسی ناتمام/ناموفق merge نخواهد شد.


## CI failure follow-up — 2026-10-31
- .NET CI روی head `6e089f4f017ea1321c42acbd24e099bdd592760e` فقط در Job `secret-scan` شکست خورد؛ Build/Test و مراحل بعدی به علت dependency از آن job اجرا نشدند.
- Job log نشان داد finding دقیقاً همان fingerprint تاریخی commit `0508c553e4cab42f967bc6bcd86e48cefac2ec9a` در fixture تستی است؛ مقدار KeyId عمومی است، نه private key یا credential.
- تغییر literal به `test-kid` به‌تنهایی کافی نبود چون Gitleaks کل تاریخچهٔ commitهای PR را اسکن می‌کند. فقط همان fingerprint در `.gitleaksignore` اضافه شد.
- Vendor CI run `41183238859` هنگام بررسی در حال اجرا بود؛ نتیجهٔ نهایی آن و CI جدید روی commit بعدی هنوز تأیید نشده‌اند. هیچ merge انجام نشده است.
