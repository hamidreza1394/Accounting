# تحویل وظیفه — Issue #393: سازندهٔ Payload برای افزایش سقف شرکت

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/393
- **Agent/مسئول:** ChatGPT
- **تاریخ:** 2026-11-04
- **وضعیت فعلی:** سه candidate builder در main ادغام و post-merge CI آن‌ها تأیید شده است؛ گردش تجاری خرید/پرداخت و صدور هنوز NotImplemented است.

## هدف و معیارهای پذیرش
- [x] ساخت candidate amendment برای افزایش installation-scoped `maxCompanies` روی همان LicenseId و InstallationId.
- [x] حفظ Payload v1 به‌صورت v1 و Payload v2 به‌صورت v2؛ بدون تبدیل ضمنی نسخه یا حذف entitlementهای موجود.
- [x] الزام sequence دقیقاً بزرگ‌تر، سقف جدید بزرگ‌تر، منابع Active/Issued و تطابق metadata با آخرین payload.
- [x] تست‌های واحد برای v1/v2، حفظ حقوق، metadata stale، cross-customer، وضعیت نامعتبر، replay/sequence و عقب‌گرد زمان، انقضا/شروع term و استقلال support/update.
- [x] CI پس از Merge برای PR #527 (۱۵/۱۵)، #558 (۱۵/۱۵) و #565 (۱۵/۱۵) روی merge commitهای واقعی تأیید شد.
- [ ] تکمیل persistence، commercial order/purchase evidence، maker-checker approval و signing/export در PRهای بعدی #393.

## خلاصه تغییرات
- کلاس خالص `VendorLicenseAmendmentPayloadBuilder` افزوده شد. ورودی current payload باید پیش‌تر با trust store مستقل و امضای دقیق envelope اعتبارسنجی شده باشد.
- فقط افزایش سقف شرکت را می‌سازد؛ کاهش، مقدار مساوی، سقف اولیهٔ نامشخص، sequence stale/overflow، customer/installation غیرمنطبق یا غیرفعال و metadata stale رد می‌شوند.
- candidate تمام فیلدهای دیگر را حفظ می‌کند: ProductCode، LicenseId، InstallationId، نوع/تاریخ لایسنس، subsystemهای v1، company entitlements و تاریخ‌های support/update در v2.
- هیچ امضا، وضعیت پایدار، sequence دیتابیس، سفارش/پرداخت یا Approval در این builder تغییر نمی‌کند.

## فایل‌های مهم تغییرکرده
- `Vendor/Vendor.Host/Signing/VendorLicenseAmendmentPayloadBuilder.cs`
- `tests/Vendor.Host.Tests/VendorLicenseAmendmentPayloadBuilderTests.cs`
- `docs/LICENSING_IMPLEMENTATION_PLAN.md`
- `docs/WORK_LOG.md`

## تصمیم‌های فنی
- D-059 رعایت می‌شود: سقف Company در سطح Installation است؛ LicenseId/InstallationId ثابت می‌ماند.
- Payload v1 و v2 جداگانه حفظ می‌شوند. تبدیل v1 به v2 ممنوع است تا زمانی که entitlementهای Company-scoped قبلی با مرجع قابل اعتماد معلوم نباشند.
- Builder هیچ اعتماد جدیدی به ورودی UI نمی‌دهد؛ caller باید signature، hash، trusted key، customer/install binding و آخرین sequence را در transaction مناسب بررسی کند.
- این PR، اجرای تجاری یا امضای Production را کامل نمی‌کند؛ provider واقعی KMS/HSM انتخاب یا جعل نمی‌شود.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `dotnet build` | در CI موفق | Build در `.NET CI` روی SHA قبلی PR موفق شد؛ commit اصلاح analyzer مجدداً باید بررسی شود. |
| `dotnet test tests/Vendor.Host.Tests` | موفق روی SHA قبلی | Vendor.Host.Tests: 214 passed، 0 failed، 0 skipped؛ commit اصلاح analyzer مجدداً CI می‌خواهد. |
| SQL Server Integration | موفق در Vendor CI روی SHA قبلی | مجموعهٔ Vendor.Host.Tests با SQL Server CI اجرا شد؛ 214 passed، 0 failed. |
| `git diff --check` | اجرا نشده | تغییر از طریق GitHub Git API ساخته می‌شود؛ CI و review diff لازم است. |

## امنیت، داده و سازگاری
- فقط کد Vendor Host، تست و مستندات تغییر می‌کند.
- هیچ تغییر در `Server/**`، Customer DB، Migration مشتری، قرارداد Customer یا کلید/secret انجام نمی‌شود.
- builder امضا را تأیید نمی‌کند و نباید با current payload خام فراخوانی شود؛ trust validation مسئولیت service/caller است.

## ریسک‌ها و کارهای باقی‌مانده
- سفارش/پرداخت و purchase evidence هنوز مدل/endpoint ندارد.
- Company-specific module/feature/seat add-ons نیازمند current Payload v2 و CompanyReference معتبر است؛ تبدیل خودکار v1 به v2 مجاز نیست.
- صدور نهایی، maker-checker، تراکنش، sequence uniqueness، export و ACK هنوز در #393/#394/#235 پیاده‌سازی/تأیید می‌شوند.

## شناسه‌های Git
- Branch: feat/393-vendor-company-capacity-amendment-payload
- Commit: SHA نهایی همیشه از Head همان PR خوانده می‌شود؛ CI فعلی روی هر push مجدداً اجرا می‌شود.
- Pull Request: پس از ساخت


## به‌روزرسانی CI — 2026-11-04
- روی SHA قبلی `50b431e7a791b20353db4a83569bf97aafa110c8`، Vendor CI شامل 214 تست Vendor.Host با 0 شکست و 0 skip موفق شد؛ Linux و Windows checkpoint regression نیز موفق شدند.
- .NET CI روی همان SHA: Build موفق؛ مجموع 923 تست، 915 موفق، 8 skip و 0 شکست. E2E و publish در زمان ثبت این یادداشت هنوز در حال اجرا بودند.
- Semgrep، CodeQL و Secret Scan موفق شدند.
- دو هشدار CA1861 در fixtureهای تست با آرایه‌های readonly مشترک اصلاح شده‌اند؛ این commit جدید نیازمند اجرای مجدد CI است.

- **اصلاح اعتبار زمانی:** amendment فقط در بازهٔ اعتبار حق استفاده ساخته می‌شود؛ grace ضمنی برای term expiry اعمال نمی‌شود. انقضای support/update مانع amendment یک perpetual license نیست.


## بازبینی امنیتی تکمیلی — snapshot مستقل از ورودی‌های mutable
- مدل قرارداد از `IReadOnlyList` استفاده می‌کند، اما این interface مانع پاس‌دادن `List<T>` یا آرایه mutable توسط caller نیست.
- Builder اکنون هنگام ساخت amendment، لیست subsystemهای v1 و تمام لایه‌های collection در company entitlementهای v2 (companies/modules/features) را به snapshotهای مستقل read-only کپی می‌کند.
- تست‌های regression پس از ساخت amendment، مجموعه‌های ورودی را mutate می‌کنند و ثابت می‌کنند payload آماده امضا تغییر نمی‌کند.
- این اصلاح در همان محدودهٔ pure payload builder است؛ هیچ persistence، signer، migration یا customer-side code اضافه نشده است.

## برش دوم — افزایش entitlementهای شرکت موجود (2026-11-06)

- متد `TryBuildCompanyEntitlementIncrease` در Builder اضافه شد؛ فقط Payload v2 معتبر و یک `CompanyReference` که از قبل در همان payload امضاشده وجود دارد پذیرفته می‌شود. v1 به v2 تبدیل نمی‌شود و CompanyReference جدید ساخته نمی‌شود.
- افزودن ماژول‌های allowlist‌شده (از جمله `TRADE_IMPORT`)، افزایش `MaxUsers` و افزودن/ارتقای Featureهای موجود در `LicenseFeatureCatalog` پشتیبانی می‌شود.
- Feature باید نوع کاتالوگ و ماژول مالکِ دارای entitlement داشته باشد؛ `enabled` فقط از false به true و `max_count` فقط به مقدار بزرگ‌تر قابل تغییر است. enum تا زمان وجود مقادیر محصولی تأییدشده پذیرفته نمی‌شود.
- کاهش، تکرار، کد ناشناخته، ماژول از قبل خریداری‌شده، شرکت ناشناخته، درخواست خالی و سقف خارج از محدوده رد می‌شوند. همهٔ entitlementهای شرکت‌های دیگر و فیلدهای سطح Installation حفظ می‌شوند.
- collectionهای ورودی و خروجی snapshot مستقل read-only هستند؛ candidate همچنان pure است و هیچ سفارش/پرداخت، persistence، approval، امضا، export یا sequence پایدار ایجاد نمی‌کند.
- تست‌های Regression برای مسیر موفق چندنوعی، حفظ شرکت دیگر، round-trip canonical، ورودی mutable، رد v1، شرکت/ماژول/Feature نامعتبر، عدم افزایش و no-op اضافه شدند.
- تست محلی اجرا نشده است؛ وضعیت فقط پس از CI روی SHA نهایی قابل `Verified` است.


## برش سوم — تمدید مستقل دوره‌های پشتیبانی و به‌روزرسانی (2026-11-07)

- متد `VendorLicenseAmendmentPayloadBuilder.TryBuildSupportAndUpdatePeriodExtension` فقط Payload v2 معتبر و منطبق با همان Customer/Installation/License را می‌پذیرد.
- فقط تاریخ غیر-null موجود را می‌توان تمدید کرد؛ تاریخ جدید باید هم از تاریخ قبلی و هم از زمان صدور amendment جلوتر باشد. تاریخ null به‌صورت ضمنی به entitlement تبدیل نمی‌شود.
- `supportUntilUtc` و `updatesUntilUtc` مستقل و قابل تمدید جداگانه‌اند؛ `expiresAtUtc`، LicenseId، InstallationId، ProductCode، maxCompanies و entitlementهای Company حفظ می‌شوند.
- Payload v1، sequence/metadata stale، منابع غیرفعال، no-op، کاهش/تکرار تاریخ، تاریخ آینده‌نبودن و term license منقضی‌شده رد می‌شوند. perpetual license پس از پایان support می‌تواند همان دورهٔ قبلی را تمدید کند.
- این متد pure candidate builder است؛ هیچ خرید، سفارش، پرداخت، approval، امضا، persistence یا sequence پایدار ایجاد نمی‌کند. caller همچنان باید envelope جاری را با trust store و signature بررسی کند.
- تست‌های Regression افزوده‌شده: تمدید مستقل Support، تمدید مستقل Updates، رد v1 و تاریخ null، رد no-op/عدم افزایش/تاریخ غیرآینده/term منقضی، و تمدید Support منقضی‌شده برای perpetual.
- تست محلی در زمان PR اجرا نشد؛ پس از Merge PR #565، هر ۱۵ Check Run روی merge commit واقعی موفق شد و candidate این برش در main Verified است.


## تأیید نهایی پس از Merge — 2026-11-08

- مبنای فعلی: main@da8ed012074f8439cb836ea4ae190e3ad2fc542e؛ merge commitهای #527، #558 و #565 در تاریخچه main هستند.
- #527: 2a62811acdb71bda77288356743be45356685954 — ۱۵/۱۵ Check Run پس از Merge موفق.
- #558: 8d9740631e715e17b6d280026a049a89a2596f3b — ۱۵/۱۵ Check Run پس از Merge موفق.
- #565: 66b60cc48f6454997e260cb1244374114e2bfbd4 — ۱۵/۱۵ Check Run پس از Merge موفق، شامل Vendor package/tests، .NET build/test، E2E، Linux/Windows publish، Secret Scan، Semgrep و CodeQL.
- وضعیت: candidateهای maxCompanies، Company-scoped Module/Feature/UserSeat و support/update در main Verified هستند. سفارش تجاری، payment evidence، persistence، API، Maker-Checker اختصاصی amendment، صدور پایدار و Purchased/Delivered/Acknowledged همچنان NotImplemented هستند.
- مرزها: هیچ Customer consumer، Server/**، Customer DB migration یا provider واقعی KMS/HSM در این سه PR اضافه نشده است.


## بازبینی 2026-11-12 — تفکیک candidate builder از Commercial Order

- مبنای جاری: main@5cd83ad2355c2ad188b4129eb803c705d4f07f2e.
- سه برش payload builder در main باقی‌اند و شواهد پس از Merge آن‌ها تأیید شده است: PR #527 (افزایش maxCompanies)، PR #558 (افزایش Company-scoped Module/Feature/UserSeat) و PR #565 (تمدید مستقل Support/Updates)، هرکدام با ۱۵/۱۵ Check Run موفق.
- بررسی مستقیم VendorBusinessEntities.cs نشان می‌دهد مدل‌های تجاری runtime فعلی VendorCustomer، VendorInstallation، VendorContract و VendorLicense هستند. VendorDbContext برای Approval، SignedLicenseEnvelope، SignedRevocationSnapshot و ProvisioningRequest نیز DbSet دارد، اما در مدل/DbContext بررسی‌شده SalesOrder، SalesOrderLine، PaymentRecord یا PaymentAllocation وجود ندارد. بنابراین Contract metadata یا License metadata را نباید به‌عنوان مدرک خرید/پرداخت تلقی کرد.
- **NotImplemented:** Commercial Order، OrderLine، Payment Evidence، persistence و API خرید، گردش تأیید تجاری، اتصال سفارش به candidate amendment، صدور پایدار، تحویل و وضعیت‌های Purchased/Delivered/Acknowledged.
- پیش از پیاده‌سازی باید سیاست تجاری روشن باشد: چه مدرکی پرداخت را اثبات می‌کند، چه نقش/هویتی آن را بررسی و تأیید می‌کند، و تفاوت PendingVerification با Purchased چیست. این یادداشت هیچ سیاست پرداختی را تصویب نمی‌کند.
- مسیر کم‌هزینهٔ پیشنهادی پس از تأیید سیاست: Vendor-only SalesOrder و lineهای typed، ثبت manual payment reference/evidence metadata، RowVersion و audit، سپس handoff به maker-checker و signer موجود. درگاه پرداخت، Ledger موازی یا دسترسی مستقیم به Customer DB لازم نیست و نباید حدس زده شود.
- Company-specific add-on همچنان به CompanyReference معتبر در payload v2 وابسته است؛ #392 باید binding مورد اعتماد را تعیین کند. v1 به v2 تبدیل نمی‌شود و CompanyReference جدید ساخته نمی‌شود.
- مرزها حفظ می‌شوند: بدون Server/**، Customer consumer/ACK، Customer DB migration، private key، credential یا provider فرضی. تست محلی اجرا نشد؛ این بازبینی صرفاً بررسی کد و مستندات بود.
