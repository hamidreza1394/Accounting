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


## وضعیت بازبینی‌شده روی main — 2026-10-31

این handoff ابتدا قراردادهای مصرف‌کننده را توصیف می‌کرد. پس از ادغام PRهای بعدی، باید بین «قرارداد»، «adapter پایدار» و «اتصال runtime» تفکیک شود:

### اجزای موجود در main
- قرارداد `OfflineRevocationList` و `OfflineRevocationListEnvelope`، serialization canonical و اعتبارسنجی امضا/sequence/expiry در `Licensing.Contracts/OfflineRevocationList.cs`.
- `OfflineRevocationConsumer` و `RevocationAwareLicenseEnvelopeValidator` در Licensing.Contracts؛ وجود adapter به تنهایی تضمین نمی‌کند همه مسیرهای واقعی customer host از آن استفاده کنند.
- persistence ضد rollback و TPM anchor در `Infrastructure/Persistence/TpmAnchoredOfflineRevocationStateStore.cs` و migration مشتری `20261031120000_AddCustomerOfflineRevocationState` در PR #366. این تغییر قبلاً جداگانه ادغام شده است؛ این handoff مجوز تغییرات schema بیشتر در Customer DB نیست.
- trust resolver مستقل revocation در `Server/Infrastructure/Licensing/OfflineRevocationTrustProviders.cs`.
- `IOfflineRevocationSigningProvider` و adapter PKCS#11 مجزا با KeyId اختصاصی در PR #371؛ DI پیش‌فرض همچنان unavailable/fail-closed است. وجود adapter به معنی provision شدن کلید یا HSM واقعی نیست.

### شکاف قطعی در Vendor control plane
- `VendorApprovalEndpoints` برای approval با `TargetStatus == "Revoked"` هنوز `503 license_revocation_unavailable` برمی‌گرداند.
- `VendorLicenseIssuanceService` فقط صدور پاکت license را انجام می‌دهد؛ سرویس متناظر تولید snapshot ابطال وجود ندارد.
- در `VendorDbContext` مدل/DbSet تاریخچه snapshotهای ابطال و sequence پایدار Vendor وجود ندارد.
- endpoint مجاز export snapshot امضاشده و کنترل cache برای artifact وجود ندارد.

### ترتیب الزامی پیاده‌سازی بعدی
1. افزودن مدل immutable مانند `VendorSignedRevocationSnapshot` در Vendor.Persistence، شامل sequence یکتا، KeyId، algorithm، envelope JSON، payload hash و timestamp؛ بدون ذخیره private key.
2. افزودن counter/sequence پایدار Vendor با concurrency control در سطح SQL Server. sequence نباید از timestamp یا شمارش ساده رکوردها استنتاج شود.
3. افزودن سرویس تولید snapshot با `OfflineRevocationListValidator.Serialize` و `IOfflineRevocationSigningProvider`; ترتیب شناسه‌ها ordinal و canonical باشد و bounds قرارداد رعایت شود.
4. در یک تراکنش واحد: بررسی RowVersion و وضعیت license، تأیید maker-checker، ساخت و امضای snapshot، ذخیره artifact، تغییر status و ثبت audit. در صورت شکست signer/audit هیچ state یا sequence commit نشود.
5. افزودن endpoint فقط-خواندنی و مجاز برای export آخرین envelope؛ پاسخ `Cache-Control: no-store` داشته باشد و اطلاعات کلید خصوصی/پیکربندی حساس افشا نکند.
6. تست SQL Server برای sequence monotonic/unique، دو revoke هم‌زمان، rollback در signer/audit failure، approval stale، عدم self-approval و export آخرین artifact.
7. در slice جداگانه، مسیر واقعی customer runtime را audit و `RevocationAwareLicenseEnvelopeValidator` را در تمام مسیرهای صدور/بارگذاری/استفاده license متصل کن؛ اگر snapshot موجود/معتبر نیست یا منقضی شده، fail-closed باقی بماند.

### معیار بستن Issue #214
Issue فقط وقتی قابل بستن است که تولید و انتشار snapshot در Vendor، enforcement در مسیر واقعی Customer runtime، تست‌های SQL Server و CI سبز باشند. تست‌های واقعی سخت‌افزار/دو میزبان و رویه backup/restore باید به‌عنوان معیارهای عملیاتی مستقل ثبت شوند؛ ادغام قرارداد یا اجرای unit test به‌تنهایی کافی نیست.

### محدودیت محیط اجرای این نشست
این بازبینی مستندات را دقیق می‌کند. پیش از تغییر کد و ساخت migration جدید باید Build و تست‌های Vendor اجرا شوند؛ این نشست checkout اجرایی .NET/SQL Server ندارد. هیچ ادعایی درباره اجرای محلی یا موفقیت تست‌های جدید مطرح نشده است.