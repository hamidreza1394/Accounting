# تحویل وظیفه — ISSUE-235-dual-host-e2e

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/235
- **PR:** https://github.com/hamidreza1394/Accounting/pull/317
- **Branch:** `feat/issue-235-dual-host-e2e`
- **تاریخ:** 2026-10-30
- **وضعیت:** پیاده‌سازی آزمون انجام شده؛ Vendor CI و .NET CI روی PR در حال اجرا هستند. تا سبز شدن همه بررسی‌ها هیچ نتیجه موفقی برای اجرای این آزمون ادعا نمی‌شود.

## هدف
اثبات یک جریان HTTP واحد میان دو میزبان ASP.NET Core مستقل:
- Vendor Host با دیتابیس Vendor مجزا
- Customer Host با دیتابیس Customer مجزا

این آزمون از endpointهای واقعی هر دو Host استفاده می‌کند و انتقال بسته و acknowledgement را از طریق فایل JSON در یک پوشه موقت انجام می‌دهد.

## سناریوهای پوشش‌داده‌شده در آزمون
1. Migration هر دو دیتابیس آزمایشی و راه‌اندازی دو Host مستقل.
2. ورود دو اپراتور Vendor با Password + TOTP و مجوزهای server-side؛ ورود Infrastructure در Customer نیز با MasterUsername/MasterPassword هش‌شدهٔ runtime و صفحه واقعی Razor Login انجام می‌شود (بدون Test Authentication Handler یا جعل Claim).
3. ساخت درخواست provisioning، maker-checker با دو کاربر متفاوت، امضای بسته و Export.
4. انتقال فایل بسته به Customer؛ رد KeyId ناشناخته و payload دست‌کاری‌شده قبل از هر تغییر داده.
5. دو درخواست هم‌زمان HTTP برای همان بسته؛ انتظار یک apply و یک replay، با فقط یک Company، User و ProvisioningRequestClaim.
6. بررسی وضعیت Enrollment در دیتابیس؛ صفحه واقعی Razor Login با credential معتبرِ آزمایشی قبل از Enrollment از صدور Cookie منع می‌شود و پس از Enrollment ورود موفق و صدور Cookie بررسی می‌شود.
7. تکمیل Enrollment با token یک‌بارمصرف؛ پذیرش ورود پس از آن و رد مصرف مجدد token.
8. replay بسته و بازگشت acknowledgement اصلی بدون صدور مجدد enrollment token.
9. انتقال acknowledgement از طریق فایل؛ رد امضای دست‌کاری‌شده در Vendor، حفظ `PendingDelivery` و تغییر به `Confirmed` فقط پس از acknowledgement معتبر.
10. ارسال مجدد acknowledgement دقیق و اثبات idempotency.

## مرز امنیتی
- کلیدهای ECDSA با `ECDsa.Create(ECCurve.NamedCurves.nistP256)` در زمان تست ساخته می‌شوند و فقط در حافظه تست وجود دارند.
- Providerهای Production تغییر نکرده‌اند و همچنان fail-closed هستند.
- هیچ کلید خصوصی، رمز ثابت، یا credential عملیاتی به repo، log یا artifact اضافه نشده است.
- Trust Store سمت Customer از تنظیمات Customer Host خوانده می‌شود؛ KeyId داخل package فقط lookup hint غیرقابل‌اعتماد است.

## محدودیت مهم آزمون
دو میزبان با دو `WebApplicationFactory` و دو `TestServer` مستقل و دو دیتابیس SQL Server مستقل اجرا می‌شوند، اما در یک test process قرار دارند. انتقال فایل واقعی است؛ این آزمون هنوز دو ماشین، دو OS process، قطع فیزیکی شبکه یا استقرار KMS/HSM واقعی را شبیه‌سازی نمی‌کند. برای ادعای آن سطح از Production E2E باید در staging دو پردازه/ماشین جدا با signer عملیاتی آزمایش شوند.

## CI
Vendor CI اکنون هر دو متغیر زیر را به SQL Server 2022 سرویس‌شده متصل می‌کند:
- `FARANO_VENDOR_TEST_SQLSERVER`
- `FARANOU_TEST_SQLSERVER`

در CI هر دو متغیر Require نیز فعال‌اند تا آزمون به‌صورت Skip سبز نشود.

## باقی‌مانده
- بررسی نتیجه کامل PR #317 روی SHA نهایی.
- رفع هر خطای Build/Test که CI نشان دهد و تکرار اعتبارسنجی.
- پس از سبز شدن، اجرای نهایی SQL Server transaction/replay/concurrency suite و ثبت دقیق نتایج.
- Provider عملیاتی امضای بسته و acknowledgement، KMS/HSM، چرخش کلید و توزیع کلید عمومی همچنان در #236 مسدود به تصمیم و دسترسی است.
