# D-056 — امضای مرحله‌ای PKCS#11 با سطح اطمینان Software/Hardware

- **وضعیت:** Accepted — تأیید صریح مالک محصول، 2026-10-30.
- **هدف:** نبود HSM نباید تکمیل کد و Staging را متوقف کند؛ برچسب Production کامل نیز نباید زودتر از شواهد سخت‌افزاری و بازیابی صادر شود.

## مرحلهٔ ۱ — کد کامل و Staging تأییدشده
1. یک Provider مبتنی بر PKCS#11 برای هر دو سطح Software و Hardware.
2. SoftHSM برای Development و Staging؛ Vendor روی سیستم مالک و سپس یک سرور ابری؛ نصب مشتری روی ماشین/VM دوم.
3. تغییر از SoftHSM به HSM واقعی فقط با پیکربندی، بدون بازنویسی Provider.
4. AssuranceLevel صریح ('Software'/'Hardware') در startup log، audit و health/readiness.
5. Production با Software فقط وقتی 'AcceptSoftwareKeyRisk=true' صریح باشد و هر startup رخداد Audit ثبت کند؛ در غیر این صورت fail-closed.
6. private key خارج از Git، log، artifact و package؛ کلیدهای package/license/ACK جدا، KeyId نسخه‌دار، overlap rotation، revocation و راهنمای backup/recovery.
7. هیچ KMS cloud adapter بدون تصمیم جداگانه.

## مرحلهٔ ۲ — Production کامل (آینده)
این برچسب فقط پس از تحقق هم‌زمان موارد زیر مجاز است:
- HSM سخت‌افزاری واقعی provision و allowlist شده و آزمون امضای end-to-end موفق باشد.
- آزمون عملیاتی Vendor و Customer روی دو میزبان واقعی و مستقل انجام شده باشد.
- backup/restore و بازیابی شواهد checkpoint ممیزی از failure domain مستقل عملاً اجرا و مستند شده باشد.
- چرخش و ابطال کلیدها، جداسازی دسترسی‌ها، alerting و runbook عملیاتی در محیط هدف تأیید شده باشند.

## مرزبندی وضعیت
- SoftHSM هرگز 'Hardware' یا اثبات HSM تلقی نمی‌شود.
- CI سبز، TestServerهای مجزا یا دو دیتابیس در یک runner معادل دو میزبان واقعی نیستند.
- اگر هر مرحلهٔ استقرار یا تست عملیاتی اجرا نشده باشد، وضعیت آن 'NotVerified'/'NotRun' باقی می‌ماند.
- این تصمیم هیچ مجوزی برای حذف/کاهش کنترل امنیتی یا تغییر دیتابیس مشتری خارج از Issue/PR مستقل نمی‌دهد.

## وابستگی‌ها
- 'Pkcs11Interop' از قبل در 'Directory.Packages.props' با نسخهٔ مرکزی 5.3.0 و PackageReference در Vendor.Host ثبت شده است؛ مجوز بسته باید پیش از هر ارتقا دوباره بررسی شود.
- D-055 معماری اصلی PKCS#11 را تعریف می‌کند؛ این تصمیم دامنهٔ مرحلهٔ ۱ را گسترش می‌دهد.
