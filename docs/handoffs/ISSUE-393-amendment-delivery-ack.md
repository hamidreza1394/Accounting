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

- **Migration `20261128161909_VendorLicenseAmendmentDeliveryAcknowledgement`** (additive): ده ستون nullable جدید (`IssuedSequence`, `DeliveredNonce`, `DeliveredPayloadSha256`, `DeliveredAtUtc`, `AcknowledgementSha256`, `AcknowledgementKeyId`, `AcknowledgementResult`, `AcknowledgementErrorCode`, `CustomerProcessedAtUtc`, `AcknowledgedAtUtc`)، گسترش CHECK Constraint وضعیت با `Delivered`/`Applied` و افزودن `Delivered` به فیلتر ایندکس یکتای «یک amendment فعال برای هر خط خرید». Backfill داده‌های قدیمی و گارد Down طبق بخش «سیاست Backfill و Rollback» زیر. این Migration هنوز منتشر/ادغام نشده است و اصلاح آن در همین شاخه با تصمیم مستند انجام شده است.
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
- Endpoint بدون دیتابیس: ۴۰۱ کلید ناموثق پیش از هر دسترسی دیتابیس، ۴۰۰ پاکت ناقص و ۴۰۱ فراخوانی ناشناس Endpoint تحویل پیش از حل هر پارامتر (از جمله `VendorDbContext`).
- اجرای محلی (پیش از CI): `dotnet test` کامل Vendor.Host.Tests با `ASPNETCORE_ENVIRONMENT=Development` — **۶۷۶ تست، ۵۵۲ موفق، ۰ شکست، ۱۲۴ Skip گیت‌دار** (SQL/SoftHSM/CNG در CI اجرا می‌شوند). چک differ snapshot EF نیز سبز است («No changes have been made to the model since the last migration»). یادداشت Baseline: در محیط بدون `FARANO_VENDOR_ConnectionStrings__VendorDb` تست‌های WebApplicationFactory با ۵۰۰ مواجه می‌شوند (خطای پیکربندی محیط، نه کد)؛ با تنظیم همان env var کل مجموعه سبز است.
- هیچ ادعای موفقیت CI پیش از سبز شدن چک‌های واقعی ثبت نمی‌شود.

## سیاست Backfill و Rollback (افزودهٔ 2026-11-29)

- **مسئله:** ردیف‌های `Issued` قدیمی (صادرشده پیش از این Migration) هیچ `IssuedSequence` ندارند؛ بدون اصلاح، تحویل برای همیشه با `amendment_envelope_missing` شکست می‌خورد و ردیف بن‌بست می‌شود.
- **سیاست Backfill (فقط در `Up`):** یک درخواست `Issued` قدیمی فقط وقتی به `License.LastIssuedSequence` bind می‌شود که «تنها amendment فعالِ همان لایسنس» باشد و پاکت `(LicenseId, LastIssuedSequence)` واقعاً وجود داشته باشد — تنها حالتِ بدون ابهام، چون صدورهای بعدی همان لایسنس `LastIssuedSequence` را بازنویسی می‌کنند و sequence دقیق ردیف قدیمی از هیچ ستونی قابل بازسازی نیست. هر ردیف ابهام‌دار (دو amendment فعال روی یک لایسنس، پاکت ناموجود، `LastIssuedSequence = 0`) عمداً NULL می‌ماند و در تحویل fail-closed با `amendment_envelope_missing` می‌شود؛ اپراتور باید آن را مجدداً صادر کند. هیچ دادهٔ تاریخی حذف یا بازنویسی نمی‌شود.
- **گارد Rollback:** `Down` پیش از هر عملیات تخریبی، اگر ردیف `Delivered` یا `Applied` وجود داشته باشد با `THROW 51000` شکست می‌خورد. دلیل: بازگرداندن چنین ردیفی یا در CHECK Constraint جدید نامعتبر می‌شد یا تاریخ امضاشدهٔ تحویل/ACK را به‌صورت غیرواقعی به `Issued` بازمی‌نوشت؛ هیچ‌کدام قابل قبول نیست و اپراتور پیام روشن می‌گیرد.
- **تست‌های اضافه‌شده (همه گیت‌دار SQL، اجرا در CI):** backfill سه سناریو (تک‌ردیف قابل‌حل → 2؛ دو ردیف ابهام‌دار → هر دو NULL؛ بدون پاکت → NULL) با downgrade واقعی به `20261120135809_VendorLicenseAmendmentRequests` و ارتقای مجدد؛ بلاک شدن Down با ردیف Delivered (کد SQL 51000 و دست‌نخوردن ماندن داده)؛ fail-closed تحویل ردیف بدون sequence؛ ACK با payload hash نادرست (400، بدون تغییر وضعیت و Audit)؛ دو ACK هم‌زمان یکسان (دقیقاً یک پذیرش، invariant نهایی)؛ دو تحویل هم‌زمان (دقیقاً یک مهر تحویل، nonce یکتا، `stale_row_version` برای بازنده).
- دو Warning تحلیلگری که خود PR معرفی کرده بود نیز رفع شد (IDE0011 و CA1859 در فایل تست SQL).

## مرز تغییر

فقط `Vendor/Vendor.Persistence` (entity، DbContext، Migration، snapshot)، `Vendor/Vendor.Host` (قواعد، سرویس تحویل، دو endpoint، ثبت DI، مهر IssuedSequence)، تست‌های Vendor و مستندات. هیچ تغییر در `Server/**`، Customer DB، transport مشتری (#235/#394)، Key Ceremony یا فعال‌سازی `Licensing:RuntimeGate:Enabled` (باید `false` بماند) انجام نشده است. تولید/انتقال ACK سمت مشتری خارج از این PR است.
