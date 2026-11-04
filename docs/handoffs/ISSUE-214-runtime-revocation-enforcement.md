# تحویل وظیفه — ISSUE-214-runtime-revocation-enforcement

- Issue: [#214](https://github.com/hamidreza1394/Accounting/issues/214)
- Branch: `fix/214-runtime-revocation-enforcement-clean`
- وضعیت: نقطهٔ مشترک runtime evaluation افزوده شد؛ CI پس از باز شدن PR مرجع است.

## شکاف قبلی
- قراردادها، `RevocationAwareLicenseEnvelopeValidator`، `OfflineRevocationConsumer`، state store با TPM و import path از قبل در main بودند.
- DI فقط `ICustomerLicenseEnvelopeValidator` را در معرض قرار می‌داد و import از آن استفاده می‌کرد.
- **هیچ سرویس مشترکی برای ارزیابی لایسنس نصب‌شدهٔ جاری وجود نداشت.** پس از import، تغییر وضعیت ابطال، انقضای snapshot یا شکست TPM روی لایسنس ذخیره‌شده اعمال نمی‌شد.

## تغییرات این PR
- `ICustomerLicenseRuntimeEvaluator` / `CustomerLicenseRuntimeEvaluator` در `Server/Infrastructure/Licensing/`.
- همیشه envelope ذخیره‌شده را دوباره از `ICustomerLicenseEnvelopeValidator` (که فقط به‌صورت revocation-aware ثبت شده) عبور می‌دهد.
- ثبت در DI کنار import service.
- تست‌های regression برای: معتبر+غیرمنقضی، revoked، snapshot غایب/منقضی، signature نامعتبر، trust key ناشناخته، TPM/state failure، license missing، retry قطعی، رد Grace، installation id نامعتبر.

## کنترل‌های امنیتی
1. امضای لایسنس و Installation binding قبل از revocation (داخل validator موجود).
2. `license_revoked` و `offline_revocation_unavailable` کدهای پایدار هستند.
3. Unavailable هرگز NotRevoked تلقی نمی‌شود.
4. خطای TPM / state store / trust → fail-closed.
5. هیچ ثبت DI برای `LicenseEnvelopeValidator` خام وجود ندارد.
6. Log فقط warning عمومی؛ بدون جزئیات crypto/secret.
7. ردیف DB به‌تنهایی منبع اعتبار نیست؛ هر ارزیابی مجدداً validator را صدا می‌زند (بدون cache).

## وابستگی‌های خارج از محدوده این PR
- **Startup Gate / license-first gate:** Issue [#396](https://github.com/hamidreza1394/Accounting/issues/396) (مرتبط [#384](https://github.com/hamidreza1394/Accounting/issues/384)) — اتصال `ICustomerLicenseRuntimeEvaluator` به startup و قبل از Login.
- **Module / seat / feature entitlement:** Issues [#390](https://github.com/hamidreza1394/Accounting/issues/390), [#391](https://github.com/hamidreza1394/Accounting/issues/391) و مسیر Trade [#401](https://github.com/hamidreza1394/Accounting/issues/401) — استخراج entitlement از LicenseEnvelope و gating ماژول‌ها.
- این PR نقطهٔ مشترک اجباری را فراهم می‌کند؛ ادعای تکمیل Startup Gate یا Module Entitlement نمی‌کند.

## محدودیت‌ها (صریح)
- سخت‌افزار واقعی TPM در این محیط در دسترس نبود؛ تست‌ها contract/regression هستند و Hardware assurance محسوب نمی‌شوند.
- Production signing و trust distribution همچنان وابسته به #236 هستند.
- هیچ Migration جدید، تغییر Vendor، تغییر قرارداد Snapshot یا الگوریتم امضا انجام نشده است.
- SDK .NET 10 در محیط agent موجود نبود؛ Build/Test محلی اجرا نشده است.

## فایل‌های تغییرکرده
- `Server/Infrastructure/Licensing/CustomerLicenseRuntimeEvaluator.cs` (جدید)
- `Server/Program.cs` (ثبت DI)
- `tests/Template.Tests/CustomerLicenseRuntimeEvaluatorTests.cs` (جدید)
- `docs/handoffs/ISSUE-214-runtime-revocation-enforcement.md` (این فایل)
- `docs/WORK_LOG.md` (ورودی)

## تست‌های اجراشده در این نشست
- نصب SDK در محیط agent ناموفق بود. هیچ Build/Test محلی ادعا نشده است.
- نتیجه واقعی فقط پس از CI معتبر است.
