# Issue #393 — رهگیری تحویل و ACK امضاشدهٔ Amendment (تصمیم S-B)

**وضعیت:** پیاده‌سازی Vendor-only در PR جاری؛ منتظر CI و بازبینی.
**Issue:** https://github.com/hamidreza1394/Accounting/issues/393
**مبنای شاخه:** `main@0766208fa663c09e745d45abc372d0a5033eb5ea`
**تصمیم مالک:** کامنت S-B در Issue #393 (2026-11-24) — گزینهٔ A، ذخیرهٔ metadata روی همان ردیف درخواست، بدون جدول ACK جدا و بدون پروتکل امضای دوم.

## قرارداد پذیرفته‌شدهٔ مالک

- بازاستفاده از `CustomerProvisioningAcknowledgementEnvelope`، validator canonical و `ProvisioningAcknowledgementTrustStore`؛ بدون broker یا trust store موازی.
- هر ACK به پنجگانهٔ زیر bind است: شناسهٔ درخواست Amendment، nonce مخصوص همان تحویل، SHA-256 payload امضاشدهٔ دقیق، `CustomerReference` و `InstallationCode`. LicenseId و Sequence در خود payload امضاشده bind هستند.
- metadata تحویل/ACK فقط روی ردیف `VendorLicenseAmendmentRequest` ذخیره می‌شود؛ پاکت کامل ACK هرگز persist نمی‌شود.
- فقط اولین ACK پذیرفته می‌شود؛ retry امضاشدهٔ دقیق idempotent است؛ digest متفاوت = Conflict؛ ACK نامعتبر/ناموثق/ناهمسان هرگز وضعیت را جلو نمی‌برد.
- `Delivered` از `Applied` تفکیک شده؛ `Applied` فقط با ACK امضاشدهٔ `Applied`/`AlreadyApplied`؛ ACK امضاشدهٔ `Rejected` با کد مجاز، رد نهایی است؛ شکست انتقال هرگز رد مشتری تلقی نمی‌شود.
- وضعیت و Audit Event در یک تراکنش ثبت می‌شوند.

## پیاده‌سازی

- **Migration `20261128161909_VendorLicenseAmendmentDeliveryAcknowledgement`** (additive): ده ستون nullable جدید (`IssuedSequence`, `DeliveredNonce`, `DeliveredPayloadSha256`, `DeliveredAtUtc`, `AcknowledgementSha256`, `AcknowledgementKeyId`, `AcknowledgementResult`, `AcknowledgementErrorCode`, `CustomerProcessedAtUtc`, `AcknowledgedAtUtc`)، گسترش CHECK Constraint وضعیت با `Delivered`/`Applied` و افزودن `Delivered` به فیلتر ایندکس یکتای «یک amendment فعال برای هر خط خرید». مسیر Down کامل بازگشت‌پذیر است.
- **صادر شدن:** `VendorLicenseAmendmentIssuanceService.IssueApprovedAsync` اکنون `IssuedSequence` را در همان تراکنش صدور مهر می‌کند تا تحویل و هر ACK به پاکت امضاشدهٔ دقیقِ همان درخواست bind شود؛ صدور موازی بعدی همان لایسنس هرگز برای این درخواست تحویل گرفته نمی‌شود.
- **قواعد خالص** `VendorLicenseAmendmentAcknowledgementRules`: گیت تحویل/صدور مجدد/پذیرش ACK، شکل nonce، جدول نتیجهٔ ACK (Applied/AlreadyApplied → `Applied`؛ Rejected با کد مجاز → `Rejected` با `amendment_ack_rejected`) و allowlist بستهٔ کدهای رد مشتری مطابق کدهای واقعی `CustomerLicenseImportService` (fail-closed برای هر کد ناشناخته).
- **سرویس تحویل** `VendorLicenseAmendmentDeliveryService` (در تراکنش caller): اثبات یکپارچگی پاکت ذخیره‌شده (hash payload + KeyId + Algorithm) پیش از هر export، تولید nonce اختصاصی، مهر تحویل اول، و export مجدد idempotent با همان بایت‌ها و همان nonce.
- **Endpointهای جدید:**
  - `POST /api/vendor/license-amendment-requests/{id}/delivery` — اپراتور دارای `LicensesIssue` + antiforgery؛ پاسخ `no-store` با پاکت، nonce و hash payload.
  - `POST /api/vendor/license-amendment-requests/{id:guid}/acknowledgement` — machine-to-machine، `AllowAnonymous` + سیاست rate limit `provisioning-acknowledgement`؛ اعتبارسنجی با trust store و validator موجود؛ اولین ACK برنده؛ retry دقیق idempotent؛ digest متفاوت 409؛ ACK ناهمسان/نامعتبر 400 بدون هیچ تغییر وضعیت؛ رویدادهای Audit `vendor.amendment.acknowledgement-confirmed/-rejected/-retried` در همان تراکنش.
- **Allowlist endpointهای عمومی** در `VendorPermissionAuthorizationTests` با مسیر M2M جدید گسترش یافت.

## Regression و شواهد

- قواعد خالص: ۸ تست جدید (گیت‌های وضعیت، شکل nonce، جدول نتیجه، allowlist رد، blocking Delivered).
- SQL Server گیت‌دار (`FARANO_VENDOR_TEST_SQLSERVER`): ۷ تست — مهر تحویل و export مجدد idempotent با شمارش Audit، رد تحویل در وضعیت‌های دیگر، fail-closed پاکت دستکاری‌شده، چرخهٔ کامل ACK امضاشدهٔ Applied با retry idempotent، رد ACK متعارض و حفظ نتیجهٔ اول، بی‌اثری ACK نامعتبر/ناموثق/ناهمسان روی وضعیت، رد نهایی ACK امضاشدهٔ Rejected، و بلاک‌بودن درخواست دوم برای همان خط خرید در وضعیت Delivered.
- Endpoint بدون دیتابیس: ۴۰۱ کلید ناموثق پیش از هر دسترسی دیتابیس و ۴۰۰ پاکت ناقص.
- اجرای محلی (پیش از CI): `dotnet test` کامل Vendor.Host.Tests با `ASPNETCORE_ENVIRONMENT=Development` — **۶۶۹ تست، ۵۵۱ موفق، ۰ شکست، ۱۱۸ Skip گیت‌دار** (SQL/SoftHSM/CNG در CI اجرا می‌شوند). تست differ snapshot EF نیز در همین اجرا سبز است (Migration با مدل سازگار است).
- هیچ ادعای موفقیت CI پیش از سبز شدن چک‌های واقعی ثبت نمی‌شود.

## مرز تغییر

فقط `Vendor/Vendor.Persistence` (entity، DbContext، Migration، snapshot)، `Vendor/Vendor.Host` (قواعد، سرویس تحویل، دو endpoint، ثبت DI، مهر IssuedSequence)، تست‌های Vendor و مستندات. هیچ تغییر در `Server/**`، Customer DB، transport مشتری (#235/#394)، Key Ceremony یا فعال‌سازی `Licensing:RuntimeGate:Enabled` (باید `false` بماند) انجام نشده است. تولید/انتقال ACK سمت مشتری خارج از این PR است.
