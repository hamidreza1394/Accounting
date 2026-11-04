# تحویل وظیفه — ISSUE-214-runtime-revocation-enforcement

- Issue: [#214](https://github.com/hamidreza1394/Accounting/issues/214)
- Branch: `fix/214-runtime-evaluator`
- PR: [#526](https://github.com/hamidreza1394/Accounting/pull/526)
- وضعیت: اصلاح ثبت DI و تست رگرسیون آن در این commit اضافه شد؛ CI روی SHA جدید باید تأیید کند.

## شکاف قبلی
- قراردادها، `RevocationAwareLicenseEnvelopeValidator`، `OfflineRevocationConsumer`، state store با TPM و import path از قبل در main بودند.
- پس از import، هیچ سرویس مشترکی envelope ذخیره‌شده را دوباره از مسیر revocation-aware عبور نمی‌داد.

## تغییرات
- `ICustomerLicenseRuntimeEvaluator` / `CustomerLicenseRuntimeEvaluator`
- `CustomerLicenseRuntimeServiceCollectionExtensions.AddCustomerLicenseRuntimeEvaluator()`
- ثبت یک‌خطی در `Server/Program.cs` (اگر هنوز اعمال نشده)
- تست‌های regression (۱۱ سناریو fail-closed)

## کنترل‌های امنیتی
1. هر ارزیابی از `ICustomerLicenseEnvelopeValidator` (revocation-aware) عبور می‌کند؛ DB به‌تنهایی معتبر نیست.
2. Unavailable هرگز NotRevoked نیست (داخل consumer و evaluator).
3. Grace در runtime usable نیست.
4. Log فقط warning عمومی.
5. `LicenseEnvelopeValidator` خام در DI expose نیست.

## خارج از محدوده
- Startup Gate: #396
- Module/Seat/Feature: #390, #391, #401
- بدون Migration / Vendor / تغییر قرارداد Snapshot

## محدودیت محیط
- SDK .NET 10 در محیط agent موجود نبود؛ Build/Test محلی اجرا نشده است.

## علت شکست CI و اصلاح
- اجرای `.NET CI` روی SHA قبلی با `CS1061` در `Server/Program.cs` شکست خورد: متد extension در namespace `Infrastructure.Licensing` تعریف شده بود، اما این فایل namespace را import نمی‌کرد.
- فراخوانی به invocation صریح static تبدیل شد تا وابسته به using/extension lookup نباشد.
- تست رگرسیون جدید ثبت scoped evaluator را در ServiceCollection بررسی می‌کند.
- این تغییر فقط در PR مستقل #526 است؛ به PR Vendor-only یا Issue #235 منتقل نشده است.
- تست محلی در این نشست اجرا نشده؛ نتیجهٔ CI جدید مرجع است.
