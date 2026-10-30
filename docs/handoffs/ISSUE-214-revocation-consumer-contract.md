# تحویل وظیفه — ISSUE-214-revocation-consumer-contract

- Issue: [#214](https://github.com/hamidreza1394/Accounting/issues/214)
- Branch: `feat/issue-214-revocation-consumer-contract`
- وضعیت: قرارداد consumer و تست‌ها ثبت شده؛ CI در انتظار است.

## تغییرات
- `IOfflineRevocationKeyResolver`: trust lookup مستقل مخصوص کلیدهای revocation؛ بدون fallback به کلیدهای License/Provisioning.
- `IOfflineRevocationStateStore`: دریافت state و عملیات `TryApplyAsync` که باید compare-and-apply sequence را اتمیک انجام دهد.
- `OfflineRevocationConsumer`: verify امضا/زمان/ساختار، کنترل high-water sequence، محاسبه SHA-256 از payload دقیق امضاشده، اعمال اتمیک snapshot و query برای enforcement.
- `OfflineRevocationApplyResult`: نتیجه با error code پایدار و بدون جزئیات crypto.

## تست‌ها
- snapshot معتبر در state store اعمال و license revoked قابل query می‌شود.
- replay sequence رد می‌شود.
- key ناشناخته بدون تغییر state رد می‌شود.
- دو درخواست هم‌زمان با sequence یکسان دقیقاً یک بار پذیرفته می‌شوند.
- نتیجه واقعی CI: در انتظار؛ اجرای محلی .NET انجام نشده است.

## محدودیت‌ها و امنیت
- این PR قرارداد و orchestration است، نه پیاده‌سازی durable persistence؛ adapter مشتری هنوز لازم است.
- مسیر واقعی ارزیابی لایسنس هنوز به این state وصل نشده؛ تا اتصال، revocation enforcement کامل نیست.
- DB-only high-water mark در برابر restore یک backup قدیمی از همان DB محافظت نمی‌کند؛ trust anchor جدا یا refresh معتبر لازم است.
- هیچ customer schema/migration، secret یا private key تغییر نکرد.
- Production signer و key lifecycle همچنان وابسته به #236 است.


## اصلاح امنیتی تکمیلی
- API بررسی وضعیت به‌جای bool از نتیجه سه‌حالته استفاده می‌کند: `Unavailable`, `NotRevoked`, `Revoked`.
- نبود snapshot یا انقضای آن هرگز معادل «مجوز باطل نشده» نیست؛ وضعیت `Unavailable` است و caller باید fail-closed باشد.
- `ExpiresAtUtc` در state ذخیره می‌شود تا انقضای snapshot پس از پذیرش نیز کنترل شود.
- تست‌های regression برای نبود snapshot و snapshot منقضی‌شده افزوده شدند.


### اتصال قرارداد ابطال به ارزیابی مجوز (#214)
- `RevocationAwareLicenseEnvelopeValidator` اضافه شد: ابتدا امضای لایسنس و InstallationId را با `LicenseEnvelopeValidator` بررسی می‌کند، سپس وضعیت ابطال را می‌سنجد.
- مجوز revoked با `license_revoked` رد می‌شود؛ نبود snapshot یا snapshot منقضی با `offline_revocation_unavailable` fail-closed می‌شود؛ فقط snapshot معتبر و current که شناسه را در فهرست ندارد اجازه عبور می‌دهد.
- سه تست regression برای snapshot غایب، مجوز خارج از فهرست و مجوز باطل‌شده افزوده شد.
- این adapter هنوز به runtime customer host متصل نشده و state store پایدار هم پیاده نشده؛ این PR قرارداد اتصال را آماده می‌کند، نه ادعای enforcement production.


- تست‌های ترکیبی pipeline در همان فایل `tests/Licensing.Contracts.Tests/OfflineRevocationConsumerTests.cs` قرار گرفتند تا قرارداد consumer و validator در یک مجموعه تست هم‌خوان بمانند.
