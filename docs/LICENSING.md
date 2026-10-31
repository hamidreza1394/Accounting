# معماری لایسنس و فعال‌سازی

> **وضعیت واقعی در main (2026-10-31):** قراردادهای امضاشدهٔ لایسنس و provisioning، Vendor control-plane مرحله‌ای، workflow صدور/ابطال، provisioning v4، مصرف snapshot ابطال آفلاین، state store مشتری و TPM NV Extend adapter پیاده‌سازی شده‌اند. PR #382 مرز DI را طوری سخت کرده که فقط `ICustomerLicenseEnvelopeValidator` قابل تزریق باشد. بااین‌حال **ورود و ذخیرهٔ LicenseEnvelope در نصب مشتری، call-site اجرایی enforcement، سیاست مشترک Authorization و audit لایسنس هنوز NotImplemented هستند**؛ بنابراین هیچ کنترل لایسنس end-to-end در عملیات حسابداری فعال نیست و سامانه Production-ready نیست. Providerهای امضای Production تا provision و پیکربندی معتبر fail-closed باقی می‌مانند. Hardware verification و عملیات واقعی TPM نیز NotVerified هستند.
>
> تصمیم‌ها: D-020 تا D-057 در [`DECISIONS.md`](DECISIONS.md)؛ موارد باز در [`LICENSING_OPEN_DECISIONS.md`](LICENSING_OPEN_DECISIONS.md)؛ مدل داده در [`LICENSING_DATA_MODEL.md`](LICENSING_DATA_MODEL.md)؛ گردش‌کارها در [`LICENSING_WORKFLOWS.md`](LICENSING_WORKFLOWS.md)؛ تهدیدها در [`LICENSING_THREAT_MODEL.md`](LICENSING_THREAT_MODEL.md).


## ۱) وضعیت فعلی (واقعی)

- هیچ کنترل لایسنس در برنامه فعال نیست. `ActivationKeysHandlerMiddleware` کد مرده است و از commit `370fe67` در Pipeline نیست (D-028).
- مدل Tenant و عضویت (`Company`، `UserCompanyMembership`، `CompanyRole`) وجود دارد و پایه کار است (D-009، D-013).
- مجوز عملیاتی در دیتابیس تعریف شده ولی در کد مصرف نمی‌شود (D-025).

## ۲) اصول

1. **سه مفهوم مستقل:** `CustomerAccount` (خریدار)، `Installation` (نمونه نصب)، `Company` (شرکت حسابداری/Tenant) — D-020.
2. **دو استقرار، یک هسته:** ابری و اختصاصی؛ سامانه فروشنده جدا از بسته مشتری — D-021.
3. **حق استفاده، پشتیبانی و به‌روزرسانی مفاهیم جدا:** D-022.
4. **فعال‌سازی آفلاین با درخواست/پاسخ امضاشده:** D-023، قالب و رمزنگاری D-024 (`Proposed`).
5. **لایسنس تجاری ≠ مجوز کاربر:** هر دو سمت سرور لازم‌اند — D-025.
6. **داده هرگز حذف یا قفل کامل نمی‌شود:** حالت محدودشده با خروجی‌گرفتن — D-026.
7. **هیچ ادعای مصونیت مطلق نمی‌شود.** کپی کامل ماشین مجازی در محیط کاملاً آفلاین قابل جلوگیری نیست (D-027).

## ۳) اجزا

| جزء | توضیح | وضعیت |
|---|---|---|
| سامانه مرکزی فروشنده (Vendor Control Plane) | Host، persistence، هویت/مجوز، ممیزی، maker-checker، workflow صدور/ابطال و داشبورد در مراحل مختلف پیاده‌سازی شده‌اند؛ provider امضای Production و sink مستقل ممیزی هنوز مانع Production هستند | `Partially Implemented / NotProductionReady` |
| سرویس صدور Vendor | workflow دو نفره و ذخیرهٔ envelope/sequence پیاده‌سازی شده؛ adapter PKCS#11 و SoftHSM توسعه/آزمون موجود است؛ provider Production فقط با پیکربندی و پذیرش ریسک معتبر قابل فعال‌سازی است | `Partially Implemented / NotProductionReady` |
| `Licensing.Contracts` | قرارداد LicenseEnvelope، اعتبارسنج امضا/زمان، revocation-aware validator و provisioning payload v1-v4 با canonical سازگار | `Implemented`; تست‌های CI مربوط به قرارداد و DI موفق‌اند |
| ماژول لایسنس نصب | ورود فایل، ذخیره، ارزیابی وضعیت، ممیزی، محدودسازی سمت سرور | `NotImplemented` |
| نمایش در React | فقط بازتاب وضعیت از API؛ منبع تصمیم نیست | `NotImplemented` |

## ۴) محتوای لایسنس (طرح)

نام فیلدها پیشنهادی است و با تأیید ERD نهایی می‌شود:

- `formatVersion`, `kid`, `sequence`, `licenseId`
- `installationId` و اثرانگشت کلید عمومی نصب, `requestId`
- `customerRef` (شناسه ارجاعی؛ بدون داده شخصی غیرضروری)
- `type` = `Perpetual` | `Term`; `validFrom`, `validUntil` (برای Term)
- `supportUntil`, `updatesUntil` (مستقل از حق استفاده)
- `modules[]` با کدهای پایدار نسخهٔ اول: `finance`, `sales`, `inventory`, `treasury` (D-045 `Accepted`; پیاده‌سازی هنوز انجام نشده)
- `limits.maxCompanies` در سطح Installation؛ user seats و Module/Feature entitlement طبق D-059 در سطح Company هستند. D-059 بخش `maxUsers Deferred` از D-045 را supersede می‌کند.
- امضا: ECDSA P-256/SHA-256، IEEE P1363 با طول ثابت 64 بایت؛ اعتبارسنجی روی بایت‌های دقیق Payload (D-045). قالب پاکت/Canonicalization باید در L1 به‌صورت دقیق و نسخه‌دار تعریف شود.
- `issuedAt`

## ۵) ارزیابی در زمان اجرا (طرح)

یک سرویس سمت سرور وضعیت لایسنس را از فایل معتبر محلی (یا در ابر از دیتابیس مرکزی) محاسبه می‌کند: `Valid`, `ExpiringSoon`, `Grace`, `Restricted`, `Missing`, `Invalid`. نتیجه با بررسی عضویت شرکت و مجوز عملیاتی ترکیب می‌شود (D-025). Endpointها یک نقطه مشترک سیاست Authorization دارند؛ بررسی پراکنده ممنوع است.

## ۶) تصمیم‌های بازِ نیازمند تأیید کارفرما (`NeedsApproval`)

موارد OD-01/02/12 با D-045 پذیرفته شده‌اند و از جدول موارد باز حذف شده‌اند. جزئیات و وضعیت هر مورد در [`LICENSING_OPEN_DECISIONS.md`](LICENSING_OPEN_DECISIONS.md) ثبت می‌شود.

| مورد باز | پرسش | وضعیت |
|---|---|---|
| OD-03/04 | مهلت ارفاق و عملیات مجاز در حالت محدود | `NeedsApproval` |
| OD-05 | سیاست ساعت و عقب‌گرد زمان | `NeedsApproval` |
| OD-06/08 | انتقال/بازیابی نصب و حفاظت کلید نصب | `NeedsApproval` |
| OD-07 | نگهداری و چرخش کلید صدور | `Accepted` (D-046); پیاده‌سازی `NotImplemented` |
| OD-11 | MFA و تفکیک وظایف مدیران فروشنده | `Accepted` (D-046); پیاده‌سازی `NotImplemented` |
| OD-09 | ارتباط آنلاین | `Accepted` (D-046); پیاده‌سازی `NotImplemented` |
| OD-10 | گذار نصب‌های موجود | `NeedsApproval` |
| OD-14 | ساخت اولین شرکت و مدیر در نصب تازه | `Proposed` / `NeedsApproval` |
| OD-15 | نگاشت مجوز عملیاتی به زیرسیستم‌ها | `NeedsApproval` |
| OD-17 | پشتیبانی راه دور | `NeedsApproval` |
| OD-13 | نیاز احتمالی به مخزن جدا برای سامانهٔ مرکزی | `Deferred` |

## ۷) مرحله‌بندی پیشنهادی پیاده‌سازی

تصمیم‌های OD-01/02/12 پذیرفته شده‌اند؛ گام L1 قرارداد در PR #82 ادغام شده است. تصمیم‌های مسدودکننده OD-07/09/11 در D-046 پذیرفته شده‌اند؛ گام بعدی طراحی/پیاده‌سازی کنترل‌شده L2 در Issue #83 است. پس از آن، مراحل مستقل: (۱) ماژول نصب با ارزیابی و ممیزی; (۲) نقطه مشترک سیاست Authorization و بررسی سقف شرکت; (۳) حالت محدودشده و خروجی; (۴) سامانه فروشنده و سرویس صدور; (۵) حذف کد مرده Activation (D-028). هیچ‌کدام از قابلیت‌های اجرایی لایسنس در حال حاضر فعال نیستند.


## ۸) اتصال Provisioning به یک لایسنس (D-054، Issue #228/#235)

- هر درخواست جدید باید به یک LicenseId و آخرین Sequence امضاشدهٔ همان لایسنس متصل باشد؛ چندلایسنس/تجمیع entitlement پشتیبانی نمی‌شود.
- ورودی کلاینت فقط licenseId و زیرمجموعهٔ اختیاری زیرسیستم‌ها است. Vendor entitlementهای اصلی را از آخرین LicenseEnvelope معتبر و امضاشده استخراج می‌کند؛ کلاینت نمی‌تواند فهرست entitlement یا sequence را تعیین کند.
- Vendor در زمان ایجاد درخواست، امضای Provisioning و Export دوباره، وضعیت Issued، Customer/Installation، تاریخ اعتبار، کلید عمومی مورد اعتماد، امضای envelope و Sequence آخر را دوباره بررسی می‌کند.
- payload schema v4 شامل licenseId و sequence است. canonical نسخه‌های v1-v3 بدون تغییر باقی می‌ماند و verifier مشتری آن‌ها را می‌پذیرد. درخواست قدیمی v3 backfill یا به لایسنس حدس‌زده متصل نمی‌شود؛ Vendor آن را دوباره امضا/Export نمی‌کند.
- کلید عمومی اعتبارسنجی envelope لایسنس از پیکربندی مستقل Licensing:TrustedSigningKeys می‌آید؛ کلید خصوصی فقط در signer خارجی قرار می‌گیرد. نبود/لغو کلید عمومی به معنی رد fail-closed است.
- **MaxCompanies: NotImplemented.** تا ایجاد شمارش/رزرو اتمیک و مرجع authoritative برای شرکت‌های نصب آفلاین، هیچ enforcement یا تضمینی ادعا نمی‌شود.
- افزودن زیرسیستم به شرکت موجود و تجمیع چند لایسنس نیز NotImplemented است.
- **محدودیت آفلاین:** بسته‌ای که قبلاً Export شده، تعلیق یا Sequence جدیدتر Vendor را در نصب کاملاً آفلاین کشف نمی‌کند. این محدودیت در D-054 پذیرفته و باید در قرارداد/عملیات به‌روشنی اعلام شود.
- وضعیت پیاده‌سازی این دامنه در شاخه feat/issue-235-single-license-v4 است؛ تا CI سبز و Merge، NotVerified باقی می‌ماند.


## 2026-10-31 — customer validator DI boundary (rebased conflict resolution)
- Reapplied on the current mainline: introduced `ICustomerLicenseEnvelopeValidator`; `RevocationAwareLicenseEnvelopeValidator` implements it; Server DI exposes only the interface and constructs the raw signature/time validator as an internal dependency.
- Added a scoped dual-host DI assertion proving the safe interface resolves while neither raw nor concrete validator is directly registered; unit coverage now targets the interface contract.
- This prevents accidental DI bypass, but is **not end-to-end license enforcement**. Production call-sites, customer envelope storage/import, authorization/audit policy, installation binding and approved OD-03/04 expiry/grace behavior remain unresolved. TPM hardware provisioning and real Windows/Linux verification remain NotVerified.
- Rebuilt this change from current `main` to resolve PR #381's divergence; CI must run on the replacement PR head. No local .NET/SQL Server execution is claimed.

## 2026-10-31 — فعال‌سازی پایدار و کنترل Cloud (D-058 / Issue #384)
- مالک محصول تأیید کرد هر LicenseEnvelope برای هر Installation فقط یک‌بار وارد شود؛ Restart/reboot/recycle برنامه یا سرویس نباید باعث درخواست مجدد فایل لایسنس یا قفل شود.
- نصب مشتری باید envelope امضاشده و وضعیت نصب را به‌صورت durable نگه دارد و در startup/نقاط سیاست سرور دوباره اعتبارسنجی کند. وجود ردیف DB به‌تنهایی اعتبار نیست.
- در نصب اختصاصی Windows، OS reinstall/re-image که هویت OS installation را تغییر دهد نیازمند reactivation است؛ Windows servicing/patch و restart نباید فعال‌سازی را بی‌دلیل باطل کنند. MachineGuid به‌تنهایی کافی نیست؛ binding باید با کلید نصب غیرقابل‌export و provider امن ترکیب شود. انتخاب provider در OD-08 با D-058 پذیرفته شده؛ recovery کلید همچنان باز است.
- در Cloud، مرجع نهایی entitlement و license در سرور است؛ React/browser فقط projection هستند. درخواست حسابداری نباید برای هر عملیات یک تماس جداگانه به Vendor داشته باشد.
- D-046 پابرجاست: نصب اختصاصی آفلاین معتبر با قطع اینترنت باطل نمی‌شود. Cloud-hosted server از مرجع server-side خود استفاده می‌کند.
- **وضعیت پیاده‌سازی:** این‌ها تصمیم پذیرفته‌شده و دامنه Issue #384 هستند؛ import/storage، installation binding و enforcement هنوز NotImplemented. هیچ Migration یا کد اجرایی در این ثبت تصمیم انجام نشده است.

## Windows customer installation identity — D-058 / OD-08
- مسیر پیش‌فرض کلید هویت نصب، CNG با TPM 2.0 و Microsoft Platform Crypto Provider است؛ کلید ECDSA P-256، machine-scoped و non-exportable است.
- مسیر جایگزین Microsoft Software Key Storage Provider با حفاظت Windows software key store/DPAPI فقط با تأیید صریح اپراتور در ceremony مجاز است؛ سطح اطمینان `SoftwareProtected` پایین‌تر از `HardwareTpm` ثبت می‌شود و fallback خودکار ممنوع است.
- فرمان provisioning فقط metadata و public SPKI را چاپ می‌کند: `dotnet Server.dll --license-installation-key create`. برای مسیر نرم‌افزاری باید `--confirm-software-provider-risk` نیز صریحاً داده شود و ProviderName در configuration به Software KSP انتخاب شده باشد.
- این فرمان باید با هویت/ACL سازگار با حساب اجرای سرویس اجرا شود. فرمان کلید موجود را overwrite نمی‌کند؛ rotation نیازمند KeyVersion جدید و مسیر کنترل‌شده است.
- کد این PR هنوز persistence و audit consent را در دیتابیس کامل نمی‌کند؛ تا آن زمان خروجی command به‌تنهایی فعال‌سازی License نیست.


## ۱۰) تصمیم پذیرفته‌شده D-059 و برنامه اجرایی

در 2026-10-31 مالک محصول D-059 را تأیید کرد. مدل قبلی که maxUsers را Deferred می‌دانست برای بخش user-seat superseded است: Seatها در سطح Company هستند؛ Module و Feature نیز مستقل و Company-scoped هستند؛ maxCompanies در سطح Installation باقی می‌ماند. برای جزئیات سیاست‌ها، معیارهای پذیرش و ترتیب کار به [LICENSING_IMPLEMENTATION_PLAN.md](LICENSING_IMPLEMENTATION_PLAN.md) مراجعه کنید.

- هر Installation یک Base LicenseEnvelope دارد؛ خرید افزونه با amendment امضاشده و sequence افزایشی برای همان LicenseId انجام می‌شود.
- کاهش Seat از نوع block-new است؛ عضویت‌های فعلی حذف نمی‌شوند. کاهش Feature limit داده را حذف نمی‌کند و فقط ایجاد/گسترش جدید را مسدود می‌کند.
- انقضای Term به restricted می‌رود؛ بدون grace ضمنی؛ read/export/recovery مجاز باقی می‌ماند. پشتیبانی/به‌روزرسانی از حق استفاده دائمی جدا است.
- Cloud تصمیم entitlement را سمت سرور می‌گیرد؛ Offline آخرین entitlement امضاشده پذیرفته‌شده را تا sync حفظ می‌کند. تبادل online authenticated و signed file exchange هر دو پشتیبانی می‌شوند.
- bootstrap مدیر اولیه در Offline محلی است؛ رمز فقط روی Customer Host تعیین و با ASP.NET Core Identity hash می‌شود و به Vendor/package/log/telemetry نمی‌رود.
- ترتیب اجرا: #395 → #390 → #391/#392 → #393 → #394. پذیرش تصمیم به معنی پیاده‌سازی نیست.
