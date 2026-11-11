# تحویل وظیفه — ISSUE-228-vendor-provisioning-state

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/228
- **وضعیت فعلی:** backend/state پایه Vendor در main است؛ PR #546 ادغام و post-merge CI آن تأیید شده است. Provisioning برای LicensePayload v2 تا mapping معتبر CompanyReference در #392 عمداً fail-closed باقی می‌ماند.

## خلاصه تغییرات
- افزودن جدول مستقل VendorProvisioningRequests در دیتابیس Vendor؛ هیچ تغییری در Server/** یا دیتابیس حسابداری مشتری انجام نشده است.
- ذخیرهٔ intent نسخه‌دار به‌صورت canonical JSON، hash، requestId idempotency، nonce یکتا، status، metadata امضا/تحویل/تأیید، failure code و rowversion.
- وضعیت اولیه AwaitingSigner است؛ API ادعا نمی‌کند شرکت یا مدیر مشتری ساخته شده یا بسته‌ای به مشتری تحویل داده شده است.
- افزودن API برای ثبت درخواست provisioning با antiforgery، مجوز سمت سرور، بررسی رابطه مشتری/نصب، ممیزی تراکنشی و رفتار idempotent برای RequestId تکراری.
- افزودن تست‌های واحد برای تنظیمات مجاز/نامعتبر و idempotency.

## فایل‌های تغییرکرده
- Vendor/Vendor.Persistence/Domain/VendorProvisioningRequest.cs
- Vendor/Vendor.Persistence/VendorDbContext.cs
- Vendor/Vendor.Persistence/Migrations/20261028163000_VendorProvisioningRequests.cs
- Vendor/Vendor.Persistence/Migrations/20261028163000_VendorProvisioningRequests.Designer.cs
- Vendor/Vendor.Persistence/Migrations/VendorDbContextModelSnapshot.cs
- Vendor/Vendor.Host/Authorization/VendorProvisioningRequestRules.cs
- Vendor/Vendor.Host/Authorization/VendorProvisioningEndpoints.cs
- Vendor/Vendor.Host/Program.cs
- tests/Vendor.Host.Tests/VendorProvisioningRequestRulesTests.cs
- این handoff و docs/WORK_LOG.md

## مرزهای امنیتی و محدودیت‌ها
- هیچ رمز خام، هش رمز مشتری یا راز bootstrap ذخیره نمی‌شود.
- امضاکنندهٔ عملیاتی/KMS هنوز متصل نشده؛ به همین دلیل وضعیت AwaitingSigner باقی می‌ماند و endpoint فعلی بستهٔ امضاشده را به مشتری ارسال نمی‌کند.
- مصرف‌کننده سمت مشتری، claim اتمی nonce، پاسخ امضاشدهٔ تأیید، retry سمت مشتری و UI وضعیت هنوز باید تکمیل شوند. این‌ها به‌عنوان تکمیل‌شده ادعا نمی‌شوند.
- Migration جدید فقط برای دیتابیس Vendor است؛ دیتابیس مشتری و Server/** بدون تغییرند. Migration guardها و SQL Server integration در CI پس از Merge PR #546 موفق شدند؛ اجرای محلی dotnet ef در این نشست انجام نشد.

## آزمون‌ها
| مورد | وضعیت |
|---|---|
| Build/Test .NET | پس از Merge PR #546: ۱۵/۱۵ Check Run موفق |
| Vendor UI/build/test | پس از Merge PR #546: Vendor build/test/package موفق |
| EF pending-model-changes / migration guard | پس از Merge PR #546 موفق |
| SQL Server integration test migration | در Vendor CI پس از Merge PR #546 موفق |

## ادامه لازم برای Issue #228
- افزودن UI فارسی RTL برای company setup، نام مدیر اولیه و زیرسیستم‌های خریداری‌شده با پیش‌فرض‌های امن.
- نمایش وضعیت‌های metadata saved / awaiting signer / pending delivery / confirmed / failed بدون ادعای provisioning قبل از تأیید مشتری.
- افزودن API/سرویس امضا فقط پس از پیکربندی کلید مورد اعتماد خارج از Git و طراحی جریان تحویل یک‌باره.
- طرح مصرف‌کننده سمت مشتری در Issue/PR جداگانه طبق محدوده Issue #228؛ بدون تغییر Server/** در این Issue.

- **Branch:** feat/issue-228-vendor-provisioning-state
- **PRهای مرتبط:** بنیاد Provisioning در PRهای #319/#329 و اصلاح fail-closed در PR #546 در main ادغام شده‌اند؛ CompanyReference v2 همچنان به #392 وابسته است.


## بازبینی 2026-11-05 — fail-closed برای LicensePayload v2

- **یافتهٔ تأییدشده:** LicensePayload نسخهٔ ۲ طبق StructuralValidation باید limits.subsystems خالی داشته باشد و ماژول‌ها را فقط در companyEntitlements و زیر یک CompanyReference مات حمل کند. اما provisioning schema v4 فقط Subsystems، LicenseId و Sequence را حمل می‌کند و مرجع شرکت را ندارد.
- **ریسک پیش از اصلاح:** resolver از payload.Limits.Subsystems می‌خواند؛ برای v2 فهرست خالی برمی‌گشت و در حالت انتخاب پیش‌فرض، intent غیرقابل‌اعمال با Subsystems خالی در وضعیت AwaitingSigner ذخیره می‌شد. validator بسته آن را هنگام صدور رد می‌کرد.
- **اصلاح Vendor-only:** resolver اکنون LicensePayload v2 را با provisioning_company_reference_mapping_required رد می‌کند و LicensePayload v1 بدون هیچ زیرسیستم را با provisioning_license_has_no_subsystems رد می‌کند. در نتیجه endpoint پیش از ایجاد رکورد ProvisioningRequest متوقف می‌شود؛ entitlementهای Company-scoped هرگز global یا خالی‌سازی‌شده تفسیر نمی‌شوند.
- **Regression:** دو تست SQL Server برای v2 شرکت‌محور و v1 بدون زیرسیستم افزوده شد. تست‌ها در این محیط اجرا نشده‌اند؛ نتیجهٔ نهایی فقط پس از Vendor CI روی SHA نهایی ثبت می‌شود.
- **وابستگی ادامه:** #392 باید نگاشت پایدار و مورداعتماد CompanyReference به CompanyId و قرارداد نسخه‌دار provisioning را هماهنگ کند؛ تغییر consumer/ACK مشتری در #235 جدا می‌ماند. تا تکمیل آن، v2 provisioning عمداً unavailable است.
- **مرزبندی:** بدون تغییر Server/**، migration دیتابیس مشتری، schema مشترک، کلید خصوصی، credential یا secret. هیچ ادعای Provisioning/ACK جدیدی ایجاد نشده است.

## تصحیح وضعیت مسیر Customer و هماهنگی #235

- consumer پایه، claim/replay، apply تراکنشی و Enrollment در PRهای #302 و #305–#308/#329 در main وجود دارند؛ intake و اعتبارسنجی ACK در PRهای #293/#294 نیز پیاده‌سازی شده‌اند. این handoff قدیمی نباید به‌عنوان نبود کامل این کد خوانده شود.
- این PR هیچ تغییر Customer-side ندارد. Issue #235 همچنان برای trust distribution، durable audit/readiness، error/retry semantics و تأیید عملیاتی مستقل باز می‌ماند.
- قرارداد provisioning v4 هنوز CompanyReference ندارد؛ تا هماهنگی #392 و نسخه‌دار شدن transport، LicensePayload v2 در Vendor fail-closed رد می‌شود.


## تأیید پس از Merge و وضعیت جاری — 2026-11-08

- PR #546 در 2026-11-05T07:57:20Z با merge commit c3404da89d97e54977ec993a20479084922ca891 ادغام شد؛ هر ۱۵ Check Run پس از Merge موفق است (Vendor build/test/package، SQL Server integration، checkpoint Ubuntu/Windows، .NET build/test/E2E/publish، Secret Scan، Semgrep و CodeQL).
- main اکنون روی da8ed012074f8439cb836ea4ae190e3ad2fc542e است و این merge commit را در تاریخچه دارد.
- قابلیت موجود: schema v4 فقط یک LicenseId/Sequence امضاشده را bind می‌کند؛ resolver v2 را با provisioning_company_reference_mapping_required رد می‌کند و v1 بدون subsystem را نیز نمی‌پذیرد. هیچ Company entitlement به سطح Installation تبدیل نمی‌شود.
- مانع #392: قرارداد و mapping پایدار CompanyReference → CompanyId محلی هنوز نیازمند طراحی/تأیید schema مشتری است؛ هیچ migration مشتری در این handoff یا PR Vendor انجام نشده است.
- مرز #235: consumer/ACK مشتری، trust distribution و retry/error semantics فقط در PR مستقل customer-side ادامه می‌یابد؛ PR Vendor فقط مرجع و binding را fail-closed نگه می‌دارد.
- تست محلی: در این بازبینی اجرا نشد؛ نتیجه فوق فقط به ۱۵ Check Run واقعی پس از Merge نسبت داده می‌شود.
 
## 2026-11-09 — نمایش صریح blocker لایسنس Company-scoped در Vendor (PR #613)
- **مسئله:** فهرست لایسنس‌های قابل Provisioning، LicensePayload v2 امضاشده و معتبر را که به‌دلیل نبود mapping مورداعتماد CompanyReference قابل استفاده نیست، بی‌صدا حذف می‌کرد؛ UI پیام «لایسنس معتبر پیدا نشد» می‌داد.
- **اصلاح Vendor-only:** `eligible-licenses` یک `blockedItems` محدود دارد؛ فقط خطای دقیق `provisioning_company_reference_mapping_required` به علت عمومی `company_reference_mapping_required` نگاشت می‌شود. خطاهای دیگر، CompanyReference، payload و KeyId به مرورگر منتقل نمی‌شوند.
- **رفتار UI:** لایسنس blocked با شماره/محصول و توضیح روشن دیده می‌شود اما انتخاب‌پذیر نیست؛ دکمه ثبت تا وجود لایسنس eligible غیرفعال می‌ماند. دادهٔ blocker هنگام تغییر نصب، loading یا خطا reset می‌شود.
- **Regression:** Unit test allowlist و Vitest نمایش blocker/عدم انتخاب/عدم ثبت اضافه شد. تست SQL Server موجود برای رد fail-closed v2 حفظ شد.
- **وابستگی:** #392 همچنان مالک binding معتبر CompanyReference به Company محلی و هر Migration احتمالی Customer است. این PR فقط blocker را آشکار می‌کند و mapping را جعل یا bypass نمی‌کند. Consumer/ACK در #235 جدا می‌ماند.
- **مرزبندی:** بدون تغییر `Server/**`، Customer DB، Migration مشتری، `Licensing.Contracts` یا private key/secret.
- **آزمون:** تست محلی اجرا نشده؛ وضعیت CI روی head نهایی PR باید بررسی شود. اجرای قبلی #608 روی SHA قدیمی CodeQL را به‌علت HTTP 504 دانلود bundle fail کرد؛ این failure زیرساختی است و باید روی PR جدید دوباره بررسی شود.
- **وضعیت:** Implemented / Not Verified.

## بازبینی 2026-11-11 — وضعیت پس از PR #633

- **مبنای جاری:** `main@3b103a5875a9668bf0c07d305a91685db3fb1db7` (Merge commit PR #633).
- PR #613 در 2026-11-09 با merge commit `f1db16e70608a572ee71673ce1fc9fc3324d2c6d` و PR #633 در 2026-11-10 با merge commit فوق ادغام شده‌اند.
- CI پس از Merge #633 روی SHA نهایی کامل و موفق است: Vendor CI run `42783806157` (۳/۳ Job)، .NET CI run `42783805952` (۷/۷ Job)، Semgrep run `42783805757` و Push on main run `42783805332`.
- لاگ .NET Build/Test: Build succeeded، صفر خطا؛ ۱۱۹۱ تست، ۱۱۸۳ موفق، صفر ناموفق و ۸ Skip؛ ۳۱۷۸ هشدار Build ثبت شده‌اند. تست محلی در این بازبینی اجرا نشد.
- PRهای #281، #282 و #284 و CI پس از Merge هرکدام مجدداً بررسی شدند؛ به‌ترتیب ۳، ۴ و ۴ Workflow پس از Merge موفق‌اند.
- **شکاف فعال #228:** schema v4 فاقد CompanyReference است. resolver برای LicensePayload v2 با `provisioning_company_reference_mapping_required` fail-closed باقی می‌ماند و هیچ entitlement شرکت به سطح Installation تبدیل نمی‌شود. #392 مالک تصمیم و binding پایدار CompanyReference → Company محلی است؛ هر Customer migration نیازمند بازبینی و تأیید جداگانه است.
- **مرز #235:** consumer/ACK، trust distribution و retry/error hardening در PR مستقل Customer-side می‌ماند؛ این بازبینی هیچ `Server/**` یا Customer DB را تغییر نداده است.
- **گام بعدی:** پس از تعیین تکلیف #392، قرارداد نسخه‌دار provisioning و نمایش Purchased Subsystems در Vendor را در PR محدود و مستقل پیگیری کنید. تا آن زمان v2 عمداً unavailable است.
