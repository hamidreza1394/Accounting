# تحویل وظیفه — ISSUE-235-dual-host-e2e-v4

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/235
- **PR مبنا:** https://github.com/hamidreza1394/Accounting/pull/319 (single-license v4)
- **PR این مرحله:** #329 — شاخه test/issue-235-dual-host-e2e-v4-main
- **تاریخ:** 2026-10-30
- **وضعیت:** PR #329 با merge commit `43d28253eb9f0f38ff25d0dfea143aeacdb06871` در main ادغام و CI آن تأیید شده است.

## هدف

اثبات جریان واقعی HTTP میان دو میزبان مستقل ASP.NET Core و دو دیتابیس SQL Server جدا:

- Vendor Host: نگهداری مشتری، نصب، لایسنس و envelope امضاشده؛ ساخت درخواست، maker-checker، امضای بسته و Export.
- Customer Host: دریافت بسته از فایل، اعتبارسنجی امضا و payload، ایجاد شرکت و مدیر اولیه، enrollment یک‌بارمصرف و تولید acknowledgement امضاشده.
- Vendor Host: دریافت acknowledgement و تغییر وضعیت به Confirmed فقط پس از اعتبارسنجی آن.

## تغییرات الزامی v4

1. آزمون یک VendorLicense واقعی در دیتابیس آزمایشی می‌سازد و برای آن یک LicenseEnvelope با کلید ECDSA موقت مستقل امضا می‌کند.
2. Vendor Host کلید عمومی آن را از Licensing:TrustedSigningKeys دریافت می‌کند؛ private key فقط در حافظهٔ تست است.
3. آزمون endpoint انتخاب لایسنس معتبر را صدا می‌زند و LicenseId، Sequence و entitlementهای استخراج‌شده از envelope را بررسی می‌کند.
4. درخواست provisioning فقط licenseId و زیرمجموعهٔ اختیاری subsystem را می‌فرستد؛ sequence و فهرست authoritative entitlement از کلاینت ارسال نمی‌شود.
5. payload خروجی باید schemaVersion=4، licenseId و sequence صحیح داشته باشد.
6. کلید امضای provisioning package و کلید امضای LicenseEnvelope مستقل هستند؛ کلید acknowledgement Customer نیز مستقل است.
7. Customer verifier بستهٔ v4 را می‌پذیرد و جریان apply، replay، enrollment و acknowledgement مانند قبل ادامه می‌یابد.

## پوشش آزمون

- دو WebApplicationFactory / TestServer مستقل و دو دیتابیس SQL Server مستقل.
- ورود Vendor با دو حساب، TOTP و مجوزهای server-side.
- ایجاد license envelope امضاشده با test-only key و بررسی eligible-license API.
- درخواست v4، maker-checker با دو کاربر متفاوت، امضای بسته و Export.
- انتقال واقعی فایل package؛ رد فایل ناقص، KeyId ناشناخته و payload دست‌کاری‌شده.
- دو درخواست هم‌زمان Customer برای همان بسته؛ انتظار یک apply و یک replay، فقط یک Company/User/Claim.
- رد ورود مدیر اولیه پیش از enrollment و پذیرش آن پس از enrollment.
- replay بسته و بازگشت acknowledgement اصلی بدون صدور مجدد enrollment token.
- انتقال acknowledgement از طریق فایل؛ رد acknowledgement دست‌کاری‌شده؛ Confirmed فقط پس از acknowledgement معتبر و idempotent.

## CI

Workflow Vendor CI برای دو دیتابیس مستقل این متغیرها را اجباری می‌کند:
- FARANO_VENDOR_TEST_SQLSERVER
- FARANOU_TEST_SQLSERVER
- FARANO_REQUIRE_VENDOR_SQLSERVER=1
- FARANOU_REQUIRE_SQLSERVER=1

Require guardها مانع سبزشدن تست با Skip خاموش می‌شوند.

## مرز و محدودیت

- این آزمون دو TestServer مستقل و دو دیتابیس جدا را در یک test process اجرا می‌کند؛ هنوز تست دو ماشین/دو OS process یا قطع فیزیکی شبکه نیست.
- کلیدها موقت و فقط در حافظه تست هستند؛ providerهای Production همچنان fail-closed باقی می‌مانند.
- تست، MaxCompanies را enforce نمی‌کند؛ این قابلیت در D-054 صریحاً NotImplemented است.
- تست افزودن ماژول به شرکت موجود یا تجمیع چند لایسنس را پوشش نمی‌دهد؛ هر دو NotImplemented هستند.
- مشتری کاملاً آفلاین نمی‌تواند تعلیق یا Sequence جدیدتر را پس از دریافت بسته کشف کند.

## باقی‌مانده

- اجرای CI روی PR #329 انجام و موفق شد: .NET CI `41052515115` (هر ۷ job موفق)، Vendor CI `41052515292` و Semgrep `41052515389`.
- PR #329 در `2026-10-30T12:19:52Z` ادغام شد؛ PRهای قدیمی #317 و #328 به‌عنوان superseded بسته شده‌اند.
- Production E2E روی دو پردازه/ماشین و KMS/HSM واقعی همچنان نیازمند staging و تصمیم عملیاتی جداگانه است.
- وضعیت signer در main پس از PR #344/#349/#352: adapter PKCS#11 و Windows CNG ACK پیاده‌سازی شده‌اند، اما HSM مرکزی واقعی provision نشده و providerهای Production عمداً fail-closed هستند.
