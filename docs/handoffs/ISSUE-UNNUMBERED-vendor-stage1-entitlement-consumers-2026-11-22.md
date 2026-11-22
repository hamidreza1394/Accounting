# تحویل وظیفه — ISSUE-UNNUMBERED-vendor-stage1-entitlement-consumers

- **Issue:** `UNNUMBERED` (مرحله ۱ مأموریت Entitlement Vendor؛ شماره Issue جعلی ساخته نشده است)
- **Agent/مسئول:** GLM — مالکیت Vendor، Licensing، Provisioning
- **تاریخ:** 2026-11-22
- **وضعیت:** انجام‌شده در شاخه (کد + تست سبز محلی؛ Merge و تأیید CI با مالک است)

## هدف و معیارهای پذیرش

- [x] اتصال `EntitlementPolicyEngine` موجود به دو نقطهٔ عملیاتی تأییدشدهٔ Vendor با کمترین تغییر و بدون قرارداد جدید
- [x] رفتار Fail-Closed در همهٔ مسیرهای تصمیم entitlement (کد فرضی/دادهٔ کلاینت هرگز منبع مجوز نیست)
- [x] حفظ کدهای خطای HTTP و رفتار idempotency/conflict موجود (هیچ کد خطای تثبیت‌شده تغییر نکرده است)
- [x] تست‌های جدید برای Gate سیاستی Amendment و گارد معماری ضد-دورزدن؛ تست‌های موجود تضعیف نشده‌اند
- [x] بدون فایل Trade، بدون تغییر `Licensing.Contracts`، بدون Migration/جدول/Workflow جدید، بدون سرویس/Broker/Worker جدید
- [x] `DenyAllTradeModuleEntitlement` دست‌نخورده و Trade همچنان Fail-Closed (تست موجود `ExactlyOneEntitlementImplementationShipsAndItDeniesAll` پابرجاست)
- [x] Gate دوکلیدی صدور v5 همچنان بسته (interlock `CustomerConsumerReady=false` با تست گارد تثبیت شد)
- [ ] تأیید CI مخزن روی head نهایی PR (در انتظار اجرای CI؛ با شاهد واقعی گزارش می‌شود، نه ادعا)

## نقشهٔ Enforcement (ترتیب اعتبارسنجی، وضعیت پس از این تغییر)

| دغدغه | پیاده‌سازی موجود | اتصال در این تغییر |
|---|---|---|
| Signature / Trust / Envelope validity | `LicenseEnvelopeValidator` با `LicenseSigningTrustStore` + `EcdsaP256Sha256Verifier` در هر دو مسیر | بدون تغییر (حفظ کامل) |
| Company (v2) | جستجوی inline در `VendorProvisioningCompanyReferenceRules` | **اتصال:** `EntitlementPolicyEngine.ResolveCompany` روی payload امضاشده |
| Module (v2) | تقاطع inline subset با ماژول‌های شرکت | **اتصال:** `EntitlementPolicyEngine.EvaluateModule` برای هر کد درخواستی |
| Module (v1 legacy) | تقاطع inline subset با `Limits.Subsystems` | **اتصال:** `EntitlementPolicyEngine.EvaluateLegacySubsystem` برای هر کد درخواستی (مسیر عملیاتی زندهٔ v1–v4) |
| Feature | StructuralValidation + قواعد Builder | عمداً بدون اتصال `EvaluateFeature` در سطح payload (نگاه زیر) |
| Seat | StructuralValidation (`MaxUsers` ساختاری) | بدون اتصال — تعداد عضویت انسانی فعال در Vendor وجود ندارد (D-054) |
| Revocation / Amendment sequencing | snapshot ابطال سمت مشتری و `sequence` افزایشی در مسیر صدور | بدون تغییر |
| Amendment (payload نهایی) | StructuralValidation + `LicenseRevisionRules` در Builder | **اتصال جدید:** Gate سیاستی `VendorAmendmentEntitlementPolicy` قبل از امضا |

ترتیب مؤثر Provisioning (مسیر عملیاتی موجود، حفظ‌شده): resolve رکورد لایسنس/مشتری/نصب فعال → وضعیت `Issued` و بازهٔ اعتبار → آخرین envelope (یا sequence تاریخی) با کنترل digest → کلید Trust Store → اعتبارسنجی کامل envelope (امضا/کید/canonical/زمان؛ `Grace`/`Restricted`/`Invalid` رد) → تطابق binding payload با رکورد → **تصمیم entitlement با Engine** → ساخت نتیجه.

ترتیب مؤثر Amendment (مسیر عملیاتی موجود + gate جدید): approval معتبر با RowVersion → payload جاری فقط از آخرین envelope امضاشده (structural + امضا + binding) → Builder قانونی (افزایش سقف/تمدید دوره) → **Gate سیاستی Engine روی payload نهایی (جدید)** → امضا → persistence اتمیک + Audit در تراکنش. نقض سیاست یعنی هیچ امضا/persist رخ نمی‌دهد (`amendment_payload_policy_invalid`).

## خلاصه تغییرات

- `Vendor/Vendor.Host/Signing/VendorAmendmentEntitlementPolicy.cs` (جدید): Gate خالص متولی Engine — جانشین amendment حق لغو هیچ entitlement اعطاشدهٔ payload جاری را ندارد و فقط می‌تواند entitlementهای قابل‌تصمیم و مجازِ Engine را حمل کند؛ کد خطای پایدار جدید `amendment_payload_policy_invalid`. سیم‌کشی در `VendorLicenseAmendmentIssuanceService.IssueApprovedAsync` پیش از `envelopeFactory.CreateAsync`.
- `VendorProvisioningLicenseResolver`: بررسی subset زیرسیستم‌های v1 به `EvaluateLegacySubsystem` متولی Engine شد (همان کد خطا/وضعیت HTTP).
- `VendorProvisioningCompanyReferenceRules`: انتخاب شرکت با `ResolveCompany` و بررسی ماژول هر کد درخواستی با `EvaluateModule` متولی Engine شد (همان کدهای خطا/وضعیت HTTP؛ حلقهٔ دفاعی structural/uniqueness قبل از Engine حفظ شد).
- تست‌ها: `VendorAmendmentEntitlementPolicyTests` (۱۱ تست قرارداد Gate) و `Architecture/VendorEntitlementEnforcementGuardTests` (۳ تست: مصرف Engine فقط در نقاط مجاز با allow-list بستهٔ نام فایل؛ پابرجایی اتصال Engine در نقاط مجاز؛ بسته‌بودن interlock v5).

## فایل‌های مهم تغییرکرده

- `Vendor/Vendor.Host/Signing/VendorAmendmentEntitlementPolicy.cs` — Gate سیاستی جدید (خالص، بدون state)
- `Vendor/Vendor.Host/Signing/VendorLicenseAmendmentIssuanceService.cs` — فراخوانی Gate پیش از امضا (+۶ خط)
- `Vendor/Vendor.Host/Authorization/VendorProvisioningLicenseResolver.cs` — تصمیم v1 با Engine (+۴/−۱)
- `Vendor/Vendor.Host/Authorization/VendorProvisioningCompanyReferenceRules.cs` — تصمیم v2 با Engine (+۱۰/−۳)
- `tests/Vendor.Host.Tests/VendorAmendmentEntitlementPolicyTests.cs` — ۱۱ تست قرارداد Gate
- `tests/Vendor.Host.Tests/Architecture/VendorEntitlementEnforcementGuardTests.cs` — ۳ تست گارد معماری
- `docs/VENDOR_LIFECYCLE_FINAL_AUDIT.md`، `docs/LICENSING_WORKFLOWS.md` §۱۴، `docs/INDEX.md`، `docs/WORK_LOG.md` — هم‌راستاسازی وضعیت (فقط مستندات)

## تصمیم‌های فنی

- **Feature در سطح payload عمداً با `EvaluateFeature` ارزیابی نمی‌شود:** payload امضاشده می‌تواند feature با `Enabled=false` داشته باشد (وضعیت پیش از اعطا که amendment بعدی آن را فعال می‌کند؛ Builder همین را انتظار دارد)؛ سمانتیک اعطای Engine (`feature_disabled`) مربوط به زمان مصرف است نه اعتبار payload. اعتبار ساختاری/catalog/type/ماژول مالک feature قبلاً در `StructuralValidation` و Builder اعمال می‌شود و مخلوط‌کردن سمانتیک‌ها مستلزم اختراع قانون تازه می‌بود.
- **Seat/MaxCompanies در Vendor ارزیابی مصرفی نمی‌شود:** تعداد عضویت فعال و شمارش شرکت‌های واقعی در Customer Host است و طبق D-054 Vendor نباید ادعای enforce این سقف‌ها را داشته باشد؛ تنها محدودهٔ ساختاری در payload اعمال می‌شود.
- **کد خطای جدید فقط یکی** (`amendment_payload_policy_invalid`) برای Gate جدید؛ هیچ کد خطای تثبیت‌شدهٔ دیگری تغییر نکرده است.
- **دلتای Gate نسبت به قواعد موجود:** `StructuralValidation` + `LicenseRevisionRules` در Builder باقی‌اند؛ Gate جدید لایهٔ Engine-محور ضدرود (anti-revoke) است که قبلاً وجود نداشت و آینده را از kinds سیاست‌شکن مصون می‌کند.
- تصمیم معماری/ADR جدیدی ثبت نشد؛ هیچ Proposed به Accepted تبدیل نشد.

## بررسی و آزمون (نتایج واقعی، SDK 10.0.100، بدون SQL Server/SoftHSM محلی)

| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `dotnet restore Vendor.sln` | موفق | exit 0 |
| `dotnet build Vendor.sln --no-restore -c Release` | موفق | ۰ خطا؛ هشدارهای Analyzer موجود قبلی |
| `dotnet test --solution Vendor.sln --no-build -c Release` | `total: 462, succeeded: 374, failed: 10, skipped: 78` | +۱۴ تست جدید این تغییر همگی سبز؛ **۱۰ خطا دقیقاً همان مجموعهٔ پیش‌موجود روی baseline `main@54847587` پیش از هر تغییر است** (`total: 448, succeeded: 360, failed: 10`) — تست‌های HTTP/SPA با ۵۰۰ محیطی (بدون محیط کامل CI)؛ مرتبط با این تغییر نیستند. ۷۸ Skip: تست‌های SQL Server (`FARANO_VENDOR_TEST_SQLSERVER`) و SoftHSM؛ «Skip» به معنی موفق نیست |
| `dotnet test --project tests/Licensing.Contracts.Tests/Licensing.Contracts.Tests.csproj -c Release` | `total: 167, failed: 0, succeeded: 167, skipped: 0` | قراردادها دست‌نخورده؛ همه سبز |
| `python3 scripts/check_static_assets.py` | PASS | exit 0 |
| `git diff --check` | تمیز | exit 0 |
| `dotnet build Template.sln` / `dotnet test --solution Template.sln` | اجرا شد (نتیجه در PR ثبت می‌شود) | اثبات عدم اثر جانبی روی اپ اصلی |
| CI مخزن | هنوز اجرا نشده | وضعیت واقعی فقط با شاهد Check Run در PR گزارش خواهد شد |

## امنیت، داده و سازگاری

- هیچ کلید، credential یا داده واقعی اضافه نشده؛ هیچ Schema/Migration/جدول جدیدی وجود ندارد؛ دیتابیس مشتری و Trade دست‌نخورده‌اند.
- Fail-Closed تقویت شد: دادهٔ خراب/نامعلوم entitlement در مسیر amendment قبل از امضا رد می‌شود؛ مسیر provisioning همچنان از payload امضاشده تصمیم می‌گیرد و ورودی کلاینت فقط selector/subset narrowing است.
- سیگنال رفتاری اپراتورها تغییر نکرده است (همان کدها/وضعیت‌های HTTP؛ فقط کد جدید `amendment_payload_policy_invalid` برای نقض Gate به‌وجودنیامدهٔ عملی اضافه شد).

## ریسک‌ها و کارهای باقی‌مانده

- ۱۰ تست HTTP محیطی روی این sandbox از قبل ناکام‌اند (قبل و بعد از تغییر، یکسان)؛ تأیید نهایی با CI مخزن روی head نهایی PR انجام می‌شود.
- تست‌های SQL Server amendment/provisioning روی DB واقعی فقط در CI اجرا می‌شوند؛ تا اجرا، وضعیت آنها «NotRun روی SQL» است نه موفق.
- مصرف‌کنندهٔ عملیاتی entitlement سمت مشتری (runtime gate) خارج از مالکیت این PR و متعلق به #391/#401 سمت Customer است.
- Production readiness ادعا نمی‌شود؛ این تغییر دروازهٔ صدور v5 را باز نمی‌کند و فعال‌سازی Production نیازمند مستندات بخش ۴ سند ممیزی است.

## شناسه‌های Git

- Branch: `glm/vendor-stage1-entitlement-consumers` (از `main@5484758722d0a316511806cf3097c5c691875599`)
- Commit (کد): `27e85c57`
- Pull Request: پس از وریفیکیشن نهایی ساخته می‌شود و در همین فایل به‌روز می‌شود.
