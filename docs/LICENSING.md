# معماری لایسنس و فعال‌سازی (طرح هدف)

> **وضعیت قابلیت در کد: `NotImplemented`.** لایسنس، مشتری تجاری، نصب و زیرسیستم هنوز در برنامه پیاده‌سازی نشده‌اند. تصمیم‌های OD-01/02/12 با D-045 و OD-07/09/11 با D-046 پذیرفته شده‌اند؛ D-047 معماری اجرایی Vendor را `Accepted` می‌داند؛ اسکلت Host مستقل در PR #92 ادغام شده و بقیهٔ کنترل‌پلین هنوز در حال پیاده‌سازی مرحله‌ای است. این تصمیم‌ها رفتار اجرایی موجود را تغییر نمی‌دهند. تصمیم‌های پایه: D-020 تا D-029 و D-041 تا D-047 در [`DECISIONS.md`](DECISIONS.md)؛ موارد باز: [`LICENSING_OPEN_DECISIONS.md`](LICENSING_OPEN_DECISIONS.md). مدل داده: [`LICENSING_DATA_MODEL.md`](LICENSING_DATA_MODEL.md). گردش‌کارها: [`LICENSING_WORKFLOWS.md`](LICENSING_WORKFLOWS.md). تهدیدها: [`LICENSING_THREAT_MODEL.md`](LICENSING_THREAT_MODEL.md).

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
| سامانه مرکزی فروشنده (Vendor Control Plane) | Host و پایهٔ persistence/Identity/audit مستقل در حال پیاده‌سازی مرحله‌ای؛ login/MFA، bootstrap، RBAC، مشتری/قرارداد، سرویس ممیزی، maker-checker و صدور/تمدید هنوز پیاده‌سازی کامل نشده‌اند | `Partially Implemented`; schema فقط پس از CI موفق روی migration ثبت‌شده `Verified` است |
| سرویس صدور Vendor | workflow دو نفره، ذخیرهٔ envelope و sequence در تراکنش Vendor پیاده‌سازی شده؛ provider واقعی KMS/HSM هنوز unavailable/fail-closed است | `Partially Implemented / NotProductionReady` |
| `Licensing.Contracts` | قرارداد امضاشدهٔ لایسنس و provisioning؛ payload provisioning v1-v4 با canonical سازگار | `Implemented` در source؛ binding v4 و CI شاخهٔ #235 هنوز `NotVerified` |
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
- `limits.maxCompanies` (نسخهٔ اول); `maxUsers` صریحاً `Deferred` است (D-045)
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
