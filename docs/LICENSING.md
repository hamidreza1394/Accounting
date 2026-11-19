# معماری لایسنس و فعال‌سازی

> **وضعیت واقعی در main (بازبینی 2026-11-19؛ `main@4c91797e3f395e1fbc737ce1ff363630f03a6052`):** زنجیرهٔ شرکت‌محور شامل قرارداد Provisioning v5 (#679)، تغییرناپذیری CompanyReference در amendment (#680)، صدور اولیهٔ LicensePayload v2 (#682)، آماده‌سازی gated Vendor (#683) و مصرف‌کنندهٔ Customer با CompanySetupRecords و Apply اتمیک (#684/#686/#687) در main است. PR #699 نیز allow-list ACK Vendor را برای چهار کد رد ثابت شرکت‌محور تکمیل کرده و Merge شده است؛ CI روی HEAD نهایی PR آن سبز بوده: Vendor ۳/۳، Semgrep ۱/۱ و .NET ۷/۷ Job. PR #702 با merge commit `163e0be3a430439b69c13e0c17e8b99e98617fb9` و PR #707 با merge commit `ab4ddef16f965b98c21720c5784a1fe068538878` ادغام شده‌اند؛ برای PR #707، CI روی HEAD نهایی ۱۸/۱۸ Check Run موفق و برای PR #699، CI پس از Merge روی `755150929b211ac3b952a3c3a7f0856624b11f0d` برابر ۱۵/۱۵ Check Run موفق تأیید شده است. این شواهد به‌تنهایی تأیید CI پس از Merge برای همهٔ تغییرات main یا Production readiness نیستند. Gate دوکلیدی صدور v5 همچنان بسته است (`CustomerConsumerReady=false` و تنظیم `Provisioning:CompanyReferenceIssuance:Enabled` پیش‌فرض خاموش)، پس هیچ بستهٔ v5 عملیاتی صادر نمی‌شود. Migration `20261116070805_AddCompanySetupRecords` فقط در مخزن است و روی دیتابیس واقعی مشتری اجرا نشده؛ Bootstrap Dedicated/Offline، سیاست Restore/Clone، trust distribution/rotation و آزمون Staging دو میزبان باقی‌اند. Providerهای Production همچنان unavailable/fail-closed هستند؛ با D-107، SoftwareProtected روی Windows CNG مجاز شده اما Provider عملیاتی/تست‌شده و کلید واقعی هنوز provision نشده است؛ KMS/HSM عملیاتی یا credential هم وجود ندارد. Call-site اجرایی enforcement و Authorization مشترک لایسنس در عملیات حسابداری هنوز NotImplemented است؛ سامانه Production-ready نیست.
>
> تصمیم‌ها: D-020 تا D-108 در [`DECISIONS.md`](DECISIONS.md)؛ موارد باز در [`LICENSING_OPEN_DECISIONS.md`](LICENSING_OPEN_DECISIONS.md)؛ مدل داده در [`LICENSING_DATA_MODEL.md`](LICENSING_DATA_MODEL.md)؛ گردش‌کارها در [`LICENSING_WORKFLOWS.md`](LICENSING_WORKFLOWS.md)؛ تهدیدها در [`LICENSING_THREAT_MODEL.md`](LICENSING_THREAT_MODEL.md)؛ نقشه اجرایی S1 در [`LICENSING_IMPLEMENTATION_PLAN.md`](LICENSING_IMPLEMENTATION_PLAN.md).


## ۱) وضعیت فعلی (واقعی)

- هیچ کنترل لایسنس در برنامه فعال نیست. `ActivationKeysHandlerMiddleware` کد مرده است و از commit `370fe67` در Pipeline نیست (D-028). **(به‌روزرسانی 2026-11-19، Issue #396):** دروازهٔ license-first runtime (`LicenseRuntimeGateMiddleware` + `ILicenseRuntimeStateService` + `GET /api/v1/licensing/status`) طبق D-060 §1-6/D-098 پیاده شده است؛ با `Licensing:RuntimeGate:Enabled` opt-in است و پیش‌فرض خاموش می‌ماند (rollout مرحله‌ای)؛ Active/Restricted/Unlicensed/Invalid سمت سرور اعمال می‌شوند.
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
| `Licensing.Contracts` | قرارداد LicenseEnvelope، اعتبارسنج امضا/زمان، revocation-aware validator و provisioning payload v1-v5؛ v5 additive و برای CompanyReference. سمت Customer، verifier با `allowCompanyReference=true` (PR #686) v5 را می‌پذیرد، اما صدور v5 در Vendor پشت Gate خاموش است | `Implemented`; صدور عملیاتی v5 در Vendor `NotImplemented` (Gate خاموش) |
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
- وضعیت این دامنه در main ادغام شده و CI پس از Merge موفق بوده است؛ شکاف باقی‌مانده، فعال‌سازی عملیاتی پس از signer واقعی (#236) و آزمون عملیاتی دو میزبان است.

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

### وضعیت Issue #384 / PR #388 پس از ادغام (2026-10-31)

- PR #387 ادغام شده و Customer-only persistence schema در main است؛ Migration روی دیتابیس واقعی اجرا نشده است.
- PR #388 با commit `75b0af84d5543a2bfb935d893d8bfc37ee330eef` ادغام شد. Enrollment هویت نصب CNG و import/replace تراکنشی از طریق ICustomerLicenseEnvelopeValidator، endpointهای Infrastructure-only، antiforgery-protected، rate-limited و no-store و InstallationId سمت سرور تحویل شدند. تمام ۷ Job CI موفق بودند.
- طبق D-059، import باید یک envelope امضاشده با status برابر Restricted را نیز persist کند تا Customer بتواند به read/export/recovery محدود شود؛ Grace رد می‌شود چون grace ضمنی پذیرفته نشده است. اتصال این status به runtime policy همچنان در #391/#384 باقی است.
- Startup reload/revalidation، license-first gate، Activation Wizard، enforcement در call-siteهای عملیات حسابداری، Cloud authoritative entitlement resolution و recovery عملیاتی هنوز تکمیل نشده‌اند؛ Issues #391 و #396 و ادامه #384 این موارد را پوشش می‌دهند. تست فیزیکی TPM/دو میزبان اجرا نشده است.

## ۱۱) معماری سه‌حوزه‌ای و تجربه License-first (D-060)

- Vendor CRM & Commercial مالک اطلاعات مشتری تجاری، قرارداد، سفارش، پرداخت، تمدید، پشتیبانی و سوابق تجاری است.
- Licensing & Entitlement Engine مالک قرارداد نسخه‌دار، entitlement، LicenseEnvelope/amendment، امضا، sequence، ابطال و enforcement است.
- Customer Deployment & Sync مالک نصب، identity enrollment، bootstrap محلی، startup gate، Activation Wizard، Cloud API، تبادل فایل Offline، sync و ACK است.
- این سه بخش مرز مسئولیت در Modular Monolith هستند؛ Microservice یا دیتابیس جدا الزامی نیست. Vendor و Customer persistence جدا می‌مانند.
- قبل از Login، Customer Host وضعیت لایسنس را سمت سرور می‌سنجد: بدون envelope به Activation Wizard؛ envelope معتبر به Login؛ Restricted به ورود کنترل‌شده read/export/recovery؛ InvalidOrUntrusted به fail-closed/recovery.
- فرم فعال‌سازی Company referenceهای مجاز را از envelope می‌گیرد و زیرسیستم/Feature/Seat را نمایش می‌دهد؛ مدیر هر شرکت محلی ساخته می‌شود و رمز فقط به Identity hash روی Customer Host تبدیل می‌شود.
- کد فعال‌سازی Vendor یک‌بارمصرف، با مدت اعتبار قابل تنظیم و bind‌شده به CustomerAccount/Installation است؛ فقط مجوز شروع صدور است، نه جایگزین LicenseEnvelope. در Cloud از API احراز هویت‌شده و در Offline توسط اپراتور Vendor روی Vendor Host redeem می‌شود؛ هرگز داخل فایل انتقال/log/telemetry قرار نمی‌گیرد.
- Offline: درخواست نسخه‌دار .request.json با public SPKI و proof-of-possession؛ پاسخ امضاشده .license.json. فایل .txt فقط Base64/armored wrapper همان بایت‌های دقیق است؛ QR صرفاً برای challenge کوچک اختیاری است. هیچ private key/password صادر نمی‌شود.
- جزئیات و معیار پذیرش در [LICENSING_IMPLEMENTATION_PLAN.md](LICENSING_IMPLEMENTATION_PLAN.md)، [D-060](DECISIONS.md) و Issue #396 است. این قابلیت‌ها Accepted / NotImplemented هستند.

## Payload v2 و entitlementهای شرکت‌محور — Issue #390

- Envelope بیرونی همچنان v1 است؛ payload v1 موجود بدون تغییر canonical bytes معتبر می‌ماند.
- payload v2 شامل maxCompanies در سطح Installation و `companyEntitlements[]` برای Module/Feature/UserSeat هر Company است.
- `TRADE_IMPORT` فقط در payload v2 و به‌صورت entitlement شرکت‌محور شناخته می‌شود؛ payloadهای v1 بدون این کد باقی می‌مانند و به‌طور ضمنی Trade را فعال نمی‌کنند.
- catalog اولیه و typed feature values در بخش ۱۰ `LICENSING_DATA_MODEL.md` تعریف شده است.
- `supportUntilUtc` و `updatesUntilUtc` از `expiresAtUtc` مستقل‌اند.
- قرارداد amendment فقط invariants را اعتبارسنجی می‌کند؛ persistence/replay transaction در #391 و Vendor issuance در #393 هستند.
- وضعیت: پیاده‌سازی در PR #390؛ تا تکمیل CI و ادغام، Verified محسوب نمی‌شود.

## ۱۲) ترکیب Entitlement با Permission عملیاتی — D-074 (Accepted، 2026-11-06)

- D-025 و D-059 پابرجا هستند: Entitlement تجاری و Permission عملیاتی دو مرجع مستقل‌اند و هر دو برای عملیات ماژول مجوزدار لازم‌اند.
- ترتیب کنترل در Customer Host طبق D-063/D-074: نشست → عضویت و WorkContext/FiscalPeriod → entitlement Installation/Company/Module/Feature/Capacity → permission تخصصی → DataScope/use case.
- در Cloud، مرجع تصمیم سمت سرور است و cache باید نسخه‌دار، دارای expiry و invalidation باشد؛ هر عملیات حسابداری نباید به Vendor round-trip بزند. در Offline، Envelope امضاشده، sequence و anti-rollback مرجع محلی‌اند.
- **وضعیت واقعی main:** قرارداد v2 و policy engine وجود دارند؛ Trade دارای `ITradeModuleEntitlement` و `TradeAccessGate` است اما DI به `DenyAllTradeModuleEntitlement` متصل است. بنابراین Trade در نبود اتصال runtime بسته می‌ماند؛ این رفتار fail-closed است و نباید با stub مجازکننده جایگزین شود.
- اتصال runtime و گسترش الگو به تمام ماژول‌های لایسنس‌دار در Issueهای اجرایی باقی است؛ D-074 به‌تنهایی مجوز Migration یا ادعای Production enforcement نیست.

## صدور اولیهٔ Payload v2 در Vendor — Issue #681 (Verified در main از طریق PR #682)
- **ورودی:** ادمین Vendor فهرست شرکت‌ها را در Create/Update لایسنس می‌فرستد (`companies`: `descriptor`، `modules`، `maxUsers`، `features`). `companyReference` هرگز از کلاینت پذیرفته نمی‌شود مگر برابر مرجع ذخیره‌شدهٔ همان لایسنس باشد؛ شرکت جدید همیشه GUID-D سمت سرور می‌گیرد؛ مرجع ناشناخته، متعلق به لایسنس دیگر یا تکراری رد می‌شود و حذف و افزودن دوبارهٔ مرجع قدیمی ممکن نیست.
- **ذخیره:** تنها ستون جدید `VendorLicenses.CompanyEntitlementsDraftJson` (`nvarchar(max) NULL`، Migration `20261115153531_VendorLicenseCompanyEntitlementsDraft`). `NULL` یعنی مسیر قدیمی v1 بدون تغییر. سقف اندازهٔ JSON ۶۴ KiB و اعتبارسنجی ساختاری با `StructuralValidation`.
- **تأیید و صدور:** پیش‌نویس روی ردیف لایسنس است؛ هر تغییر آن `RowVersion` را عوض و تأیید قبلی را باطل می‌کند (`approval_resource_stale`). Payload فقط از همان ردیفِ تأییدشده در همان تراکنش ساخته می‌شود. Envelope بیرونی `FormatVersion: 1` و Payload داخلی `FormatVersion: 2` با `Limits.Subsystems` خالی است؛ `SubsystemsJson` قدیمی نه خوانده، نه تغییر و نه وارد Payload v2 می‌شود (در پاسخ API برای لایسنس شرکت‌محور `subsystems` خالی نمایش داده می‌شود).
- **پس از صدور:** در همان تراکنش، پیش‌نویس به `{companyReference, descriptor}` کاهش می‌یابد. `descriptor` فقط برچسب شناسایی اپراتور است و هرگز مبنای مالکیت/Tenant/Company در Customer نیست. Amendment اطلاعات قبلی را از Envelope امضاشده بازیابی می‌کند (با آزمون ثابت شده).
- **Rollback و اثر حذف:** `Down` فقط ستون را حذف می‌کند. حذف ستون پیش‌نویس‌های صادرنشده (و برچسب‌های descriptor لایسنس‌های صادرشده) را از بین می‌برد؛ Envelopeهای امضاشده و لایسنس‌های صادرشده دست‌نخورده می‌مانند ولی لایسنس Draft شرکت‌محور باید دوباره وارد شود. پیش از Rollback از ستون پشتیبان بگیرید.
- **محدودیت‌ها:** ستون `CHECK ISJSON` ندارد؛ ردیف خراب (فقط با دست‌کاری مستقیم DB) فهرست لایسنس‌ها را 500 می‌کند و Builder صدور را fail-closed رد می‌کند. اندازهٔ Payload نهایی (۳۲۷۶۸ نویسه) هنگام صدور سنجیده می‌شود نه هنگام ذخیره. Provisioning v5، Customer Runtime و Issue #393 در این تغییر نیستند.

## آمادگی صدور Provisioning v5 در Vendor — Issue #392 (PR #683؛ Verified در main؛ صدور عملیاتی **غیرفعال**)
- **وضعیت یک‌خطی:** سمت Vendor آماده و تست‌شده است؛ صدور عملیاتی v5 **خاموش** است (Gate دوکلیدی بسته). سمت مشتری، پذیرش و Apply بستهٔ v5 با PRهای #684/#686/#687 پیاده شده است (جزئیات پایین‌تر)؛ اما تا باز شدن Gate هیچ بستهٔ v5 عملیاتی صادر نمی‌شود و زنجیرهٔ end-to-end v5 در Production غیرفعال می‌ماند.
- **منبع مرجع:** `CompanyReference` فقط از `CompanyEntitlements` در LicensePayload v2 **امضاشده و تأییدشده** (Resolver همان اعتبارسنجی امضا/نصب/مشتری/توالی قبلی) خوانده می‌شود. فیلد `companyReference` در `CreateVendorProvisioningRequest` فقط **انتخاب‌گر** است: اگر عضو مجموعهٔ امضاشده نباشد رد می‌شود و مقدار صادرشده همیشه از Payload امضاشده می‌آید، نه از ورودی (`VendorProvisioningCompanyReferenceRules.Select`، `VendorProvisioningRequestRules.BuildPayload`).
- **معنای هر درخواست:** هر درخواست Provisioning دقیقاً یک Company را مشخص می‌کند. `Subsystems` پیلود v5 = ماژول‌های همان Company (فقط کدهای مجاز Provisioning؛ `TRADE_IMPORT` کپی نمی‌شود)، یا زیرمجموعهٔ درخواستی از آن‌ها. `MaxCompanies` و `SubsystemsJson` سطح Installation هرگز به فهرست مجوز شرکت تبدیل نمی‌شوند. مقایسه دقیق و حساس به حروف است و به ترتیب آرایه وابسته نیست.
- **سازگاری:** لایسنس v1 مسیر v4 را بدون هیچ تغییر بایتی در canonical JSON حفظ می‌کند و انتخاب‌گر را رد می‌کند (`provisioning_company_reference_not_applicable`). canonical bytes و رفتار نسخه‌های v1–v4 حفظ شده‌اند؛ v5 به‌صورت additive فیلد اجباری `companyReference` را دارد. Envelope بیرونی، امضا و Crypto Profile دست‌نخورده‌اند.
- **Gate (پیش‌فرض خاموش، دو کلید):** `Provisioning:CompanyReferenceIssuance:Enabled` (فقط مقدار قابل‌پارس `true` به‌صورت `bool.TryParse`؛ حروف بزرگ/کوچک و فاصله پذیرفته می‌شود ولی `1`/`yes` نه) **و** قفل کدی `VendorProvisioningCompanyReferenceGate.CustomerConsumerReady` که اکنون `false` است. تا وقتی PR مصرف‌کنندهٔ Customer (تأیید v5 + Mapping + Apply اتمیک) این قفل را همراه تستش تغییر ندهد، هیچ پیکربندی صدور v5 را روشن نمی‌کند. با Gate خاموش رفتار قبلی حفظ است: لایسنس v2 با `provisioning_company_reference_mapping_required` (و دلیل اپراتوری `company_reference_mapping_required`) مسدود می‌ماند و Payload v5 ذخیره‌شده در Issuance/Export رد می‌شود.

- **Customer (PR مصرف‌کننده، 2026-11-16 — Implemented):** Customer اکنون v5 را می‌پذیرد و Apply می‌کند. `CustomerProvisioningPackageVerifier` با `allowCompanyReference: true` اعتبارسنجی می‌کند (تست پین قبلی به پذیرش v5 و تست تزریق فیلد در JSON v4 به ردّ آن تبدیل شد). جدول Mapping `CompanySetupRecords` با ایندکس یکتای `Latin1_General_BIN2` روی `CompanyReference` ساخته شده است (Migration `20261116070805_AddCompanySetupRecords`)؛ مقایسهٔ مرجع باینری است (پاسخ به یافتهٔ L-2 Vendor). در `ApplyAsync` برای بستهٔ شرکت‌محور، ترتیب بررسی‌ها ثابت است: **Verifier → Claim/Replay → (بررسی‌های v1–v4: والد، مدیر اولیه) → Entitlement → Duplicate → Capacity** و Company + Record در یک `SaveChanges` داخل همان تراکنش `Serializable` + `sp_getapplock` درج می‌شوند. کدهای رد جدید: `provisioning_license_not_installed`، `provisioning_not_entitled_for_company`، `provisioning_company_reference_conflict`، `provisioning_company_capacity_exceeded` — همه به‌صورت ACK امضاشدهٔ `Rejected` ذخیره و دقیقاً بازپخش می‌شوند. Entitlement از آخرین لایسنس `Current` همان `LicenseId` روی همین Installation می‌آید (مرجع‌های Company در amendmentها تغییرناپذیرند)؛ `maxCompanies` کل شرکت‌های نصب را می‌شمارد و فقط ایجاد جدید را مسدود می‌کند (D-059 §2/§6)؛ نبود `MaxCompanies` در پیلود امضاشده یعنی سقف عددی ندارند و همان فهرست entitlement مرز است.
- **تکمیل‌شده در Vendor (Issue #698 / PR #699):** چهار کد رد ثابت `provisioning_license_not_installed`، `provisioning_not_entitled_for_company`، `provisioning_company_reference_conflict` و `provisioning_company_capacity_exceeded` در allow-list محدود ACK پذیرفته می‌شوند. PR #699 با merge commit `755150929b211ac3b952a3c3a7f0856624b11f0d` ادغام شد؛ PR CI روی HEAD نهایی: Vendor ۳/۳، Semgrep ۱/۱ و .NET ۷/۷ Job موفق. post-merge push CI روی main در بازبینی 2026-11-18 تأیید نشده است.
- **وابستگی Vendor باقی‌مانده:** Gate صدور Vendor (`CustomerConsumerReady=false`) همچنان بسته است؛ تنظیم `Provisioning:CompanyReferenceIssuance:Enabled` نیز پیش‌فرض خاموش است. هیچ بستهٔ v5 عملیاتی صادر نمی‌شود تا آمادگی Customer و تأیید مستقل Gate احراز شود.
- **خارج از محدوده (نیازمند تأیید و PR جدا):** سیاست تغییر `CustomerNumber` پس از Provisioning (منبع `CustomerReference`)، رفع انحراف `TenantId = company.Id` (D-087/D-089)، سیاست Restore/Clone (OD-06/#364) و Bootstrap مدیر Dedicated/Offline.
- **Merge/CI:** PR #682 با merge SHA `ce2094321e881d488f953c2ea3276b1f302d29bc` و PR #683 با merge SHA `b05c1c58c5ff9830b10fb0034354ab8309da9b0d` در main هستند. پس از Merge #683، Vendor CI [43740624610](https://github.com/hamidreza1394/Accounting/actions/runs/43740624610)، .NET CI [43740624547](https://github.com/hamidreza1394/Accounting/actions/runs/43740624547)، Semgrep [43740624503](https://github.com/hamidreza1394/Accounting/actions/runs/43740624503) و Push on main [43740624071](https://github.com/hamidreza1394/Accounting/actions/runs/43740624071) همگی موفق‌اند. PR #682 نیز هر چهار Workflow پس از Merge را با موفقیت گذرانده است. سمت مشتری: PR #684 با merge SHA `5f4baf82b66e`، PR #686 با `b0eefa37c192` و PR #687 با `f8a987130b85` ادغام شده‌اند (شاخهٔ `5f4baf82` از مالک برای ادغام schema تأیید شده بود)؛ Migration روی دیتابیس مشتری اجرا نشده است.

## تصمیم‌های S1-0 و تفاوت Perpetual/Term/Support/Update (2026-11-16؛ Issue #696)

مأموریت مستنداتی S1-0، شش ADR جدید D-097..D-102 را در [`DECISIONS.md`](DECISIONS.md) ثبت کرد (همه `Accepted` برای رفتار محصول، پیاده‌سازی `NotImplemented`) و پرسش‌های باز باقی‌مانده را در OD-20..OD-26 [`LICENSING_OPEN_DECISIONS.md`](LICENSING_OPEN_DECISIONS.md) ثبت نمود:

- **D-097 — پشتیبانی و Update مستقل:** مجوز Installation اختصاصی `Perpetual` است؛ پشتیبانی و Update حق‌های مستقل از License و قابل تعریف/تمدید به‌ازای هر مشتری و هر Installation هستند؛ مشتری فاقد پشتیبانی در داشبورد Vendor با وضعیت هشدار متمایز نمایش داده می‌شود و سوابق خدمات/تماس/تمدید در CRM سبک (D-102) ثبت می‌شود.
- **D-098 — انقضای Cloud:** در استقرار Cloud انقضای مجوز ورود عادی کاربران را مسدود می‌کند؛ مسیر تمدید، بازیابی و حفظ/بازیابی داده برای نقش‌های authorized باقی می‌ماند و هیچ دادهٔ حسابداری حذف نمی‌شود. سیاست Restricted با ورود کنترل‌شدهٔ D-026/D-059 برای Dedicated/Offline معتبر می‌ماند؛ تفاوت دو سیاست در همین ADR صریح شده است.
- **D-099 — فعال‌سازی مجدد سخت‌افزاری:** گردش‌کار Online/Offline با تأیید صریح مالک Vendor، مقایسه و تاریخچهٔ مشخصات، bind پاسخ به Installation/درخواست/کلید/revision و audit کامل؛ بدون ساخت سیستم هویت نصب موازی (مرجع: D-058/Issue #384).
- **D-100 — سقف کاربران هم‌زمان:** معنای تجاری Accepted (سقف Installation = کاربران یکتای هم‌زمان، متمایز از UserSeat)؛ مدل اعمال فنی Proposed/NeedsApproval (OD-26) و تبدیل خودکار maxUsers یا شمارش خام Session ممنوع.
- **D-101 — Update با تأیید:** بررسی/اطلاع خودکار، نصب فقط با تأیید مدیر Customer، اعتبارسنجی امضا/hash/نسخه/مقصد، پشتیبان‌گیری پیش از نصب، بستهٔ مشترک امضاشده برای Offline و محدودکردن Update جدید پس از پایان Support بدون غیرفعال‌کردن نرم‌افزار Perpetual.
- **D-102 — اطلاعیه و CRM سبک:** تفکیک اطلاعیهٔ فنی/تجاری/امنیتی، معرفی محصول ≠ Entitlement، هدف‌گیری بر اساس Customer/Installation/محصولات خریداری‌شده، سوابق تماس/خدمات/فروش در CRM سبک و اجتناب از CRM/Ticketing سنگین.

**تفکیک مفاهیم در قرارداد فعلی (تطبیق با کد):** در LicensePayload v2 فیلدهای `type` (`Perpetual`/`Term`)، `expiresAtUtc` (فقط Term)، `supportUntilUtc` و `updatesUntilUtc` مستقل‌اند (قسمت ۱۰ [`LICENSING_DATA_MODEL.md`](LICENSING_DATA_MODEL.md))؛ builder تمدید مستقل Support/Updates در PR #565 در main است و amendmentهای تمدید فقط همین دوره‌ها را تغییر می‌دهند. هیچ enforcement runtime برای انقضا/Support/Update هنوز در کد نیست؛ همهٔ رفتارهای D-097..D-102 تا پیاده‌سازی NotImplemented باقی می‌مانند.



## تصمیم‌های مالک محصول در 2026-11-19 — امضای Vendor و پرداخت مستقل (D-107/D-108)

- D-107 Accepted: برای امضای مرکزی License/Provisioning Vendor در نسخه اول، Windows CNG/Certificate Store با کلید MachineKey غیرقابل‌استخراج و سطح صریح SoftwareProtected در Production مجاز است؛ TPM/HSM/KMS اجباری نیست. ریسک مدیر محلی/میزبان صریح است؛ Maker-Checker، MFA، Audit، rotation و fail-closed حفظ می‌شوند. این تصمیم D-047 بند ۷ و بخش Production از D-046 OD-07/D-056 را فقط برای امضای مرکزی Vendor supersede می‌کند.
- D-108 Accepted: صدور/فعال‌سازی از پرداخت مستقل است. Payment و Issuance دو چرخه مستقل Maker-Checker/Audit هستند. Vendor سفارش‌های Unpaid/Partial/Overdue را با محاسبه زمان خواندن در داشبورد/گزارش نشان می‌دهد؛ بدون درگاه، Ledger، scheduler یا Broker.
- محدودیت مستقل Customer: D-057 برای anti-rollback snapshot ابطال آفلاین Customer همچنان TPM 2.0 می‌خواهد؛ D-107 آن را تغییر نمی‌دهد. اثر این الزام بر مشتری فاقد TPM در OD-27 باز است.
- وضعیت: فقط تصمیم و مستندات؛ هیچ Provider، کلید، Entity، Migration، endpoint یا enforcement با این ADRها به‌تنهایی فعال نشده است.
