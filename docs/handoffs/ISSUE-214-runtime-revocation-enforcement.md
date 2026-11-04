# تحویل وظیفه — ISSUE-214-runtime-revocation-enforcement

- Issue: [#214](https://github.com/hamidreza1394/Accounting/issues/214)
- Branch: `fix/214-runtime-evaluator`
- وضعیت: نقطهٔ مشترک runtime evaluation افزوده شد؛ CI پس از باز شدن PR مرجع است.

## شکاف قبلی
- قراردادها، `RevocationAwareLicenseEnvelopeValidator`، `OfflineRevocationConsumer`، state store با TPM و import path از قبل در main بودند.
- DI فقط `ICustomerLicenseEnvelopeValidator` را در معرض قرار می‌داد و import از آن استفاده می‌کرد.
- **هیچ سرویس مشترکی برای ارزیابی لایسنس نصب‌شدهٔ جاری وجود نداشت.**

## تغییرات این PR
- `ICustomerLicenseRuntimeEvaluator` / `CustomerLicenseRuntimeEvaluator`
- ثبت DI کنار import service
- تست‌های regression fail-closed

## وابستگی‌های خارج از محدوده
- Startup Gate: #396
- Module entitlement: #390, #391, #401

## محدودیت‌ها
- SDK و TPM در محیط agent موجود نبود؛ Build/Test محلی اجرا نشده
- هیچ Migration / Vendor / تغییر قرارداد Snapshot
