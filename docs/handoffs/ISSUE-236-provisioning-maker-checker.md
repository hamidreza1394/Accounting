# تحویل وظیفه — ISSUE-236-provisioning-maker-checker

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/236
- **وضعیت:** maker-checker و fail-closed signer boundary در main ادغام شده‌اند؛ PR #314 برای ثبت امن خطای provider ادغام شده؛ بررسی رمزنگاری خروجی signer در PR مستقل جدید و منتظر CI است

## هدف
افزودن مسیر تأیید دو نفره برای صدور بستهٔ راه‌اندازی مشتری، بدون قراردادن کلید خصوصی در برنامه یا مخزن.

## تغییرات
- افزودن `VendorProvisioningIssuanceService` برای بررسی نسخهٔ درخواست، ارتباط مشتری/نصب، فعال‌بودن مقصد، canonical JSON، hash و عمر بسته پیش از امضا.
- استفاده از `ILicenseSigningProvider` موجود برای امضای دقیق بایت‌های canonical؛ هر نتیجهٔ نامعتبر یا عدم دسترسی به امضاکننده با پاسخ عمومی 503 fail-closed می‌شود.
- تغییر وضعیت `AwaitingSigner` به `PendingDelivery` فقط پس از امضای موفق و ذخیرهٔ KeyId/algorithm/signature در همان تراکنش با تصمیم تأیید.
- افزودن درخواست تأیید با کنترل Anti-forgery، مجوز مشتری/نصب، RowVersion، جلوگیری از تأیید توسط همان درخواست‌دهنده و ثبت ممیزی.
- افزودن مسیر export بستهٔ امضاشده فقط در وضعیت `PendingDelivery` با Anti-forgery، مجوز خواندن مشتری/نصب، هدرهای `no-store` و رویداد ممیزی تراکنشی.
- افزودن دکمهٔ UI برای ارسال درخواست به بررسی دو نفره و دانلود فایل JSON بستهٔ امضاشده برای انتقال آفلاین؛ وضعیت تأیید مشتری همچنان جدا نگه داشته می‌شود.
- رد تأیید، درخواست را با کد عمومی `provisioning_rejected` به `Failed` می‌برد؛ شکست امضاکننده درخواست را `AwaitingSigner` و تأیید را `Pending` نگه می‌دارد تا retry ممکن باشد.
- گسترش آزمون integration SQL Server برای اثبات شکست موقت امضا، retry همان approval، امضای موفق، اعتبارسنجی رمزنگاری بسته با کلید عمومی تست، export با Anti-forgery و ممیزی.

## مرزهای امنیتی و محدودیت‌ها
- امضاکنندهٔ پیش‌فرض `UnavailableLicenseSigningProvider` باقی مانده است؛ این تغییر به‌تنهایی امضای Production را فعال نمی‌کند.
- تا وقتی adapter واقعی KMS/HSM یا سرویس امضای مورد تأیید پیکربندی نشود، درخواست تأیید نمی‌تواند به `PendingDelivery` برسد.
- `PendingDelivery` به معنی تحویل/تأیید مشتری نیست. مصرف‌کنندهٔ مشتری و acknowledgement امضاشده در Issue #235 جداگانه لازم است.
- هیچ تغییری در `Server/**` یا دیتابیس حسابداری مشتری انجام نشده است.

- یک شکست CI نشان داد که تست integration پس از ایجاد intent، شناسهٔ تجاری مشتری را تغییر می‌داد؛ fixture اصلاح شده تا binding امضاشونده ثابت بماند و تست به مسیر واقعی signer-unavailable برسد. کنترل تولیدی که تغییر binding را رد می‌کند حفظ شده است.

## آزمون‌ها
| مورد | وضعیت |
|---|---|
| Vendor CI | در انتظار نتیجهٔ PR |
| .NET CI و تست integration روی SQL Server | در انتظار نتیجهٔ PR |
| E2E و انتشار Linux/Windows | در انتظار نتیجهٔ PR |
| اجرای محلی | در این محیط GitHub-only اجرا نشده |

## فایل‌های مهم
- `Vendor/Vendor.Host/Signing/VendorProvisioningIssuanceService.cs`
- `Vendor/Vendor.Host/Authorization/VendorProvisioningEndpoints.cs`
- `Vendor/Vendor.Host/Authorization/VendorApprovalEndpoints.cs`
- `Vendor/Vendor.Host/Program.cs`
- `tests/Vendor.Host.Tests/VendorCustomerMutationHttpIntegrationTests.cs`

- **Branch:** feat/issue-236-provisioning-release-check
- **PR:** https://github.com/hamidreza1394/Accounting/pull/240


## تکمیل رفع خطاهای CI — 2026-10-28

- در اجرای Vendor CI شمارهٔ 40740242624، typecheck شکست خورد چون static members کلاس آزمایشی `TestURL`، متدهای استاتیک ارث‌رسیده از `URL` را بدون `override` تعریف کرده بودند. در commit `b1d355add0cc0a54561cc6cd9f8f1324599c4a90` اصلاح شد؛ در اجرای بعدی، typecheck، تست‌های UI، audit وابستگی‌های runtime و build رابط Vendor موفق شدند.
- اجرای Integration روی SQL Server سپس به‌درستی درخواست صدور را با `provisioning_payload_invalid` رد کرد: خود fixture بعد از ایجاد درخواست، کد نصب را تغییر می‌داد؛ سرویس امضا عمداً payload را به هویت نصب فعلی bind می‌کند. این خطای تست بود و نباید validation تولیدی تضعیف می‌شد.
- در commit `d30693f649a50edd2f2a493ab3b05648469174a1` fixture طوری اصلاح شد که کد نصب ثابت بماند و آزمون update همچنان نام و محیط نصب را تغییر دهد. اجرای Vendor CI شمارهٔ 40742478715 روی همین SHA موفق شد: typecheck، UI tests، audit وابستگی‌ها، build، تمام تست‌های Vendor روی SQL Server، EF snapshot guard، publish و بررسی نبود کلید خصوصی همگی موفق شدند.
- اجرای .NET CI روی همین SHA باید تا تکمیل تمام jobها بررسی شود. هر commit مستنداتی بعد از این اجرا نیازمند CI مجدد روی SHA نهایی است.
- اجرای محلی در محیط فعلی ممکن نبود؛ شواهد اعتبارسنجی از GitHub Actions است.

## 2026-10-30 — ثبت امن شکست provider در Issue #236

- **یافته:** `VendorProvisioningIssuanceService` استثنای provider را بدون logging می‌گرفت و فقط 503 عمومی برمی‌گرداند؛ این با AGENTS.md و Coding Standards در مورد بلعیدن بی‌صدای Exception مغایر بود.
- **اصلاح:** استثنا با `ILogger<VendorProvisioningIssuanceService>` ثبت می‌شود، اما فقط نوع استثنا؛ message/stack/payload/KeyId/signature log نمی‌شود. نتیجهٔ unsuccessful و metadata نامعتبر نیز با warning غیرحساس ثبت می‌شوند.
- **تست regression:** provider تستی در اولین فراخوانی exception با متن تشخیصی ساختگی پرتاب می‌کند؛ HTTP همچنان فقط کد عمومی 503 می‌دهد، نوع exception در log دیده می‌شود و متن exception در log دیده نمی‌شود. تلاش دوم همان approval باید موفق شود.
- **مرز:** فقط Vendor Host، تست integration Vendor و مستندات؛ هیچ تغییر در `Server/**`، دیتابیس مشتری یا Migration انجام نشده است.
- **اعتبارسنجی:** تست محلی اجرا نشده؛ CI روی PR نهایی لازم است.
- **Branch:** `fix/vendor-provisioning-signer-exception-logging`؛ PR پس از ایجاد ثبت می‌شود.

## شناسهٔ پیگیری اصلاح observability
- Branch: `fix/vendor-provisioning-signer-exception-logging`
- PR: [#314](https://github.com/hamidreza1394/Accounting/pull/314)
- Head پس از همگام‌سازی main: در CI همین PR بررسی می‌شود.


## تأیید کلید عمومی و امضای خروجی Vendor — 2026-10-30

- **یافته:** چک‌کردن طول signature به‌تنهایی اثبات نمی‌کند provider واقعاً payload را با کلید مورد اعتماد امضا کرده است. امضای ۶۴ بایتی تصادفی می‌توانست به `PendingDelivery` برسد و فقط بعداً در نصب مشتری رد شود.
- **اصلاح:** `ProvisioningPackageSigningTrustStore` با public keyهای مستقل در `ProvisioningSigningTrust:Keys` اضافه شد. قبل از هر تغییر پایدار، امضای ECDSA P-256 روی payload دقیق با کلید فعال trust store تأیید می‌شود. KeyId با قاعدهٔ trust lookup مشتری هم‌تراز شد.
- **تست regression:** در Integration SQL Server، provider ابتدا exception و در تلاش بعد امضای نامعتبر با طول صحیح برمی‌گرداند؛ هر دو باید 503 بدهند و وضعیت/approval قابل retry بماند؛ تلاش بعدی با امضای معتبر موفق می‌شود. تست واحد trust store نیز موارد duplicate/revoked/unknown/invalid key material را پوشش می‌دهد.
- **محدوده:** Vendor Host، تست‌های Vendor و مستندات؛ بدون تغییر customer-side، `Server/**` یا Migration.
- **اعتبارسنجی:** تست محلی اجرا نشده؛ CI روی PR لازم است.
- **Branch:** `fix/vendor-verify-provisioning-signature`
- **PR:** پس از ایجاد ثبت می‌شود.


## Runbook عملیاتی HSM/TPM — 2026-10-30

- Runbook در `docs/runbooks/ISSUE-236-HSM-TPM-KEY-OPERATIONS.md` اضافه شد: نقش‌ها و کنترل دو نفره، Key Ceremony، کلیدهای مجزای package/license/ACK، CKAها، enrollment آفلاین TPM، rotation overlap، revocation، Backup/Recovery، outage و alerting.
- Runbook برند ابری یا سازندهٔ HSM فرض نمی‌کند؛ فرمان‌های اجرایی باید از راهنمای HSM واقعی و پس از تأیید مدل گرفته شوند. هیچ دستور key export یا backup فایل‌محور برای private key مجاز نیست.
- با بودجهٔ HSM صفر و نبود hardware واقعی، Runbook بخش HSM را صریحاً غیرقابل اجرا می‌داند. SoftHSM فقط Testing/CI است؛ ACK مشتری نیز تا backend غیرقابل‌استخراج و enrollment آفلاین فعال نمی‌شود.




## D-055 پذیرفته‌شده و Adapter PKCS#11 — 2026-10-30

- مالک تصمیم معماری را پذیرفته است: Vendor deployment-neutral روی سرور خودمان یا cloud؛ Azure/AWS بدون دسترسی/مجوز و خارج از محدوده؛ بودجهٔ HSM فعلاً صفر.
- D-055 در `docs/D-055-ISSUE-236-HSM-PKCS11.md` وضعیت Accepted دارد. Production package/license signing تا تأمین HSM سخت‌افزاری واقعی Blocked و fail-closed باقی می‌ماند.
- PR جاری adapter opt-in PKCS#11 را پشت interfaceهای موجود برای license و provisioning/package اضافه می‌کند. کلیدها با KeyId نسخه‌دار مجزا انتخاب می‌شوند؛ private key باید در token حساس، non-extractable، always-sensitive و never-extractable باشد. الگوریتم ECDSA P-256 و خروجی ۶۴ بایت IEEE P1363 است.
- SoftHSM فقط در Vendor CI/test استفاده می‌شود؛ slot نرم‌افزاری خارج از محیط اختصاصی `Testing` رد می‌شود؛ در Development/Staging/Production علاوه بر HardwareSlot، manufacturer/model باید دقیقاً در allowlist استقرار ثبت شده باشند. در حال حاضر HSM واقعی provision نشده و `Signing:Pkcs11:Enabled` نباید در Production فعال شود.
- PIN فقط از secret/environment injection می‌آید؛ هیچ private key generation/export یا cloud adapter اضافه نشده است. `/health/signing` سلامت signer را بدون افشای جزئیات حساس نشان می‌دهد و audit metadata-only برای هر تلاش امضا ثبت می‌شود.
- **محدودیت شواهد:** اجرای محلی در محیط GitHub-only ممکن نیست؛ تست واقعی SoftHSM و CI نهایی این PR هنوز باید تکمیل شوند. این adapter، Key Ceremony، key rotation/revocation، provisioning/recovery واقعی HSM، توزیع public trust و ACK TPM سمت مشتری را به‌تنهایی کامل نمی‌کند.


## وضعیت نهایی PR #344 — 2026-10-30

- PR #344 با merge commit `5a9717ca08ec2009305e480dbf6443683bf2e60f` ادغام شد.
- .NET CI run `41078370910`، Vendor CI run `41078370887` (شامل integration test واقعی SoftHSM) و Semgrep run `41078370883` همگی success هستند؛ publish Linux/Windows و E2E نیز در .NET CI موفق شدند.
- Adapter فقط در صورت opt-in و با دو KeyId مجزا برای license و package فعال می‌شود. در محیط غیر از Testing، Hardware Slot و manufacturer/model allowlist دقیق لازم است. `/health/signing` جدا از liveness است.
- با بودجهٔ صفر و نبود HSM واقعی، config فعال نشده است؛ signerهای Production همچنان unavailable و fail-closed هستند. SoftHSM فقط برای CI/test است و fallback Production نیست.
- Customer ACK signer per-installation در TPM/OS secure store هنوز پیاده‌سازی نشده است. برای ادامهٔ کدنویسی آن باید سیستم‌عامل هدف نصب‌های مشتری (Windows Server یا Linux) مشخص شود؛ تا آن زمان default provider unavailable باقی می‌ماند.


## وضعیت پس از Windows CNG و اصلاح تست‌ها — 2026-10-30

- یادداشت‌های قبلی که Customer ACK signer را «پیاده‌سازی‌نشده» می‌دانستند، پیش از PR #349 نوشته شده‌اند و دیگر وضعیت جاری نیستند.
- PR #349 در main ادغام شد: Windows CNG signer per-installation، کلید ECDSA P-256 غیرقابل‌استخراج، KeyName/KeyId نسخه‌دار مشتق‌شده از InstallationId و فرمان آفلاین تولید کلید اضافه شد. TPM KSP پیش‌فرض است؛ Software KSP فقط با انتخاب صریح مجاز است و fallback خودکار وجود ندارد.
- PR #351 فیلتر تست xUnit v3/MTP را اصلاح کرد. .NET CI run `41088538581` و Semgrep run `41088538577` موفق‌اند؛ Windows CNG tests، Windows/Linux publish و E2E اجرا و موفق شدند.
- تست CI از Software KSP استفاده می‌کند و TPM فیزیکی، ACL حساب سرویس Production یا همه نسخه‌های Windows/Windows Server را تأیید نمی‌کند. تا آزمون سخت‌افزاری واقعی، Production enablement ACK signer ممنوع است.
- PR #352 ادغام شد: revocation صریح Customer ACK KeyId، نگهداری SPKI برای audit، fail-closed در برابر مقدار revocation نامعتبر و overlap rotation اضافه شدند. .NET CI `41090537956` و Semgrep `41090537922` موفق؛ ۶۸۲ تست .NET (۶۷۸ موفق، ۴ Skip)، CNG ویندوز، انتشار Windows/Linux و E2E موفق. محدودیت ضد rollback در برابر restore کامل backup همچنان برقرار است.
- باقی‌ماندهٔ #236: HSM واقعی Vendor (بودجه صفر و fail-closed)، ثبت public SPKI در Trust Store مشتری و Vendor با روند امن، durable metadata-only audit/readiness، آزمون TPM واقعی و ACL، backup/recovery عملیاتی و Linux backend در مرحلهٔ آتی.


## کلید مستقل امضای Offline Revocation — 2026-10-31

- Issue #214 به سه purpose مرکزی Vendor نیاز دارد: license، provisioning/package و offline-revocation؛ هرکدام KeyId نسخه‌دار و private-key object مستقل دارند.
- قرارداد IOfflineRevocationSigningProvider و default unavailable/fail-closed اضافه شد. Provider PKCS#11 فقط کلید purpose خودش را می‌پذیرد و همان audit metadata-only را با purpose مستقل ثبت می‌کند.
- Pkcs11SigningOptions اکنون سه KeyId متمایز را اجباری می‌کند؛ module readiness وجود/صفات هر سه کلید را بررسی می‌کند. DI در حالت PKCS#11 غیرفعال همچنان unavailable است.
- SoftHSM integration test اکنون سه کلید مستقل می‌سازد و امضای revocation را با public key متناظر verify می‌کند؛ تست‌ها همچنین fail-closed بودن provider پیش‌فرض و رد KeyId تکراری/نامعتبر را پوشش می‌دهند.
- Runbook، Vendor README و D-055/D-056 clarification به‌روزرسانی شدند. این PR فقط زیرساخت امضا را اضافه می‌کند؛ صدور/ذخیره/توزیع revocation snapshot هنوز در مرحلهٔ بعدی Issue #214 است.
- HSM واقعی provision نشده؛ هیچ Production signing enablement ادعا نمی‌شود. CI نهایی پس از ایجاد PR لازم است.
