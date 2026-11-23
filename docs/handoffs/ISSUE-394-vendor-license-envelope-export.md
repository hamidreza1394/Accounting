# تحویل وظیفه — ISSUE-394-vendor-license-envelope-export

- **Issue:** [#394 — licensing: synchronize online when available and support signed offline file exchange](https://github.com/hamidreza1394/Accounting/issues/394) (برش Vendor-side: مسیر تحویل/خروجی پاکت امضاشده؛ همه‌چیز سمت Customer و جریان online در Issue خودش باقی است)
- **Agent/مسئول:** GLM (zgo/glm-5.3-flash)
- **تاریخ:** 2026-11-23
- **وضعیت:** انجام‌شده در محدوده Vendor؛ در انتظار CI و بازبینی مالک (Merge فقط با تأیید مالک)

## هدف و معیارهای پذیرش
- [x] یک مسیر «مصوب» برای خروج پاکت لایسنس امضاشده از Vendor وجود داشته باشد (پیش از این، فقط Provisioning Package مسیر export داشت و LicenseEnvelope امضاشده هیچ مسیر تحویلی نداشت).
- [x] خروجی دقیقاً همان JSON ذخیره‌شدهٔ پاکت باشد (بدون بازنویسی payload؛ قرارداد import مشتری `CustomerLicenseImportService` همان `LicenseEnvelope` را deserialize می‌کند).
- [x] fail-closed: وضعیت Issued، یکپارچگی `PayloadSha256` ذخیره‌شده، ساختار پاکت و هم‌خوانی KeyId/Algorithm، عضویت KeyId در Trust Store، امضای ECDSA P-256 و binding به Installation همه پیش از خروجی اعتبارسنجی شوند.
- [x] خروجی فقط-خواندنی، بدون تغییر وضعیت لایسنس، با رخداد Audit؛ tracking تحویل/ACK در جریان‌های جدا (#228/#393) ادعا نشود.
- [x] تست Integration HTTP روی SQL Server واقعی برای مسیر موفق و مسیرهای منفی.

## خلاصه تغییرات
- Endpoint جدید `GET /api/vendor/licenses/{licenseId:guid}/envelope/export` در `VendorBusinessEndpoints`:
  - مجوزدهی دو لایه: سیاست `vendor.licenses.read` + Resource authorization روی `license`.
  - ترتیب شکست fail-closed: `404 license_not_found` → `409 license_envelope_not_ready` (وضعیت ≠ Issued یا بدون Installation) → `409 license_envelope_missing` (بدون envelope یا Sequence < 1) → `409 license_envelope_invalid` (JSON خراب، FormatVersion پاکت ناسازگار، KeyId/Algorithm ناهم‌خوان با ردیف ذخیره‌شده، یا عدم تطابق hash ذخیره‌شده) → `503 license_envelope_signing_key_untrusted` (کلید امضا در `Licensing:TrustedSigningKeys` نیست یا ابطال شده) → `409 license_envelope_signature_invalid` یا `409 license_envelope_not_currently_valid` (شکست امضا در برابر شکست ارزیابی زمان/binding تفکیک شد).
  - آخرین Revision (بیشترین Sequence) خروجی می‌شود؛ payload همان بایت‌های امضاشده ذخیره‌شده است — حالت Restricted امضاشده هم عمداً قابل تحویل است چون همان وضعیت امضاشده است که مشتری باید persist کند (D-059 §7)؛ امضای پاکت تغییر نمی‌کند.
  - پاسخ با `Cache-Control: no-store` و `Pragma: no-cache`؛ رخداد `vendor.license.envelope-exported` در تراکنش ثبت می‌شود و شکست Audit کل درخواست را fail-closed می‌کند.
- چهار تست Integration جدید `VendorLicenseEnvelopeExportHttpIntegrationTests` (SQL Server-gated با `FARANO_VENDOR_TEST_SQLSERVER`): موفقیت (برابری بایت‌به‌بایت body با `EnvelopeJson` ذخیره‌شده + ردیف Audit + no-store)، وضعیت Draft/tamper-hash/license ناشناخته، کلید untrusted (503) و امضای دستکاری‌شده (409)، و منع دسترسی بدون `vendor.licenses.read` (403).

## فایل‌های مهم تغییرکرده
- `Vendor/Vendor.Host/Authorization/VendorBusinessEndpoints.cs` — endpoint صادرات + `EnvelopeJsonOptions` + usings.
- `tests/Vendor.Host.Tests/VendorLicenseEnvelopeExportHttpIntegrationTests.cs` — چهار سناریوی مثبت/منفی.
- `docs/handoffs/ISSUE-394-vendor-license-envelope-export.md`، `docs/WORK_LOG.md`، `docs/INDEX.md` — مستندسازی همان PR.

## تصمیم‌های فنی
- **تصمیم معماری جدیدی گرفته نشد.** الگو عیناً از export موجود Provisioning Package (`VendorProvisioningEndpoints` / `VendorProvisioningLicenseResolver`) و اعتبارسنجی `LicenseEnvelopeValidator` کپی الگو شد؛ فرق عمدی: export لایسنس برخلاف resolver، بر اساس وضعیت ارزیابی زمان (Grace/Restricted) رد نمی‌کند — فقط شکست ساختاری/امضایی fail-closed است؛ شکست ارزیابی (`evaluation_invalid`) با کد جداگانه گزارش می‌شود.
- Envelope `FormatVersion` (=۱، نسخهٔ پاکت) از `LicensePayload.CurrentFormatVersion` (=۲، نسخهٔ payload شرکت‌محور) جدا است؛ هر دو طبق قرارداد فعلی بررسی شدند.
- GET بدون Anti-forgery است (فقط-خواندنی؛ قاعدهٔ مخزن برای mutationها). هیچ Schema/Migration/قرارداد مشترک جدیدی اضافه نشد؛ `Licensing.Contracts` دست‌نخورده ماند.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `dotnet build Vendor.sln -c Release` | موفق | SDK 10.0.404 (`global.json`: 10.0.100 rollForward latestFeature) |
| `dotnet test Vendor.sln --no-build -c Release` | ۴۶۷ کل: ۳۷۵ موفق، ۱۰ ناموفق، ۸۲ Skip | مجموعهٔ ۱۰ شکست با baseline تمیز `main@62b0c3c6` (worktree جدا، همان SDK) بایت‌به‌بایت یکسان است (فقط نام‌ها مقایسه شد)؛ این شکست‌های محیطی HTTP/SPA پیش‌موجود‌اند. ۴ تست جدید + ۴ skip قبلی = ۸۲ Skip؛ تست‌های SQL Server این PR فقط در CI اجرا می‌شوند. |
| baseline: `dotnet test` روی `main@62b0c3c6` تمیز | ۴۶۳ کل: ۳۷۵ موفق، ۱۰ ناموفق، ۷۸ Skip | اثبات صفر رگرسیون |
| `python3 scripts/check_static_assets.py` | PASS | بدون تغییر دارایی |
| `git diff --check` | پاک | — |
| تست SQL Server چهار سناریو | اجرا نشده محلی | Docker در sandbox موجود نیست؛ اجرا در CI الزامی است. |

## امنیت، داده و سازگاری
- بدون تغییر Schema، Migration، تنظیم پیش‌فرض یا گیت امنیتی؛ هیچ secret/کلیدی در کد یا تست نیست (کلید تست در حافظهٔ process تست ساخته می‌شود).
- Trust Store مستقل از پاکت است: حتی اگر EnvelopeJson در DB دستکاری شود و امضا معتبر بماند، KeyId باید در `Licensing:TrustedSigningKeys` عضو و غیر-ابطال باشد؛ در غیر این صورت 503.
- پاکت حاوی کلید خصوصی نیست (قرارداد `LicenseEnvelope` فقط SPKI-KeyId/Algorithm/Payload/Signature دارد) و خروجی آن با scanهای موجود (G2/secret-scan) تعارضی ندارد.
- سازگاری با import مشتری: `CustomerLicenseImportService` همان `LicenseEnvelope` را با `JsonSerializerDefaults.Web` (تطبیق case-insensitive) deserialize می‌کند؛ JSON ذخیره‌شدهٔ Vendor (نام‌های PascalCase از `JsonSerializer.Serialize`) سازگار است.

## ریسک‌ها و کارهای باقی‌مانده
- tracking تحویل/ACK/Applied برای لایسنس (برخلاف provisioning) همچنان `NotImplemented` است و عمداً در این PR ادعا نشده (#393/#394؛ تصمیم قرارداد S-B با مالک).
- خروجی پاکت منقضی‌شده با `409 license_envelope_not_currently_valid` رد می‌شود (ارزیابی زمان داخل `LicenseEnvelopeValidator`)؛ اگر محصولاً نیاز به «تحویل پاکت منقضی» باشد، تصمیم مالک لازم است.
- حجم رخداد Audit برای هر export — در V1 تک‌اپراتوره (D-109) ناچیز است.
- هیچ تغییر Customer/`Server/**`/Migration در این PR نیست؛ اجرای واقعی چرخه روی میزبان Windows (Key Ceremony CNG طبق D-107) کار جداگانه عملیاتی است.

## شناسه‌های Git
- Branch: `agent/license-envelope-export` (از `main@62b0c3c6`)
- Commit: `0968375f` (کد+تست) + commit مستندات در همان شاخه
- Pull Request: باز، در انتظار CI و تأیید مالک (Merge نمی‌شود)
