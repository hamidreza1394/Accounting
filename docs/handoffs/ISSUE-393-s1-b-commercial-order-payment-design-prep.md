# S1-b / Issue #393 — آماده‌سازی طراحی سفارش تجاری، مدرک پرداخت و گردش‌کار تا Amendment

- **Issue:** #393 (مبنا: [#393](https://github.com/hamidreza1394/Accounting/issues/393)، برچسب licensing؛ Parent #389/D-059؛ مرز D-060)
- **تاریخ:** 2026-11-19
- **مبنا:** `origin/main@98c2edf3` (پس از Merge #711 و #713/D-105) — SHA ادعایی مأموریت با GitHub تطبیق داده شد و عیناً برقرار بود.
- **ماهیت:** فقط Design/Decision Preparation و مستندات. هیچ Entity، جدول، Migration، API، UI، کد، secret یا تنظیمی ساخته/تغییر نکرد. هیچ تستی اجرا نشد (تغییر اجرایی وجود ندارد) و هیچ سیاست تجاری به‌جای مالک تصمیم گرفته نشد.
- **ادامهٔ:** `ISSUE-393-vendor-addon-payload-builder.md` (2026-11-04، تحویل ChatGPT). این سند آن را بازنویسی نمی‌کند؛ برش بعدی همان Issue را آماده می‌کند.
- **ماهیت تصمیم‌ها:** همهٔ مدل‌ها و وضعیت‌های این سند `Proposed`اند تا مالک محصول تصویب کند؛ هر Entity/Migration بعدی به Issue و schema-approval مستقل نیاز دارد (D-041 بند ۵، D-097، D-102).
- **⚠️ به‌روزرسانی 2026-11-19:** بخش‌های ۵ (طراحی)، ۶ (گزینه‌ها) و ۷ (گردش‌کار) این سند با نسخهٔ ۲ طراحی جایگزین شده‌اند — `ISSUE-393-s1-b-order-payment-design-v2.md` (جداسازی چرخه‌های وضعیت، Entity مستقل `VendorPayment` با ستون‌های typed به‌جای JSON روی Line، تفکیک تأیید پرداخت از تأیید صدور، حذف TargetRef/NewValue عمومی). **پیشنهاد قدیمی بخش ۴، بند ۲ دربارهٔ نگهداری `InstallationId` در سطح Line نیز superseded است:** در طراحی v2.1، `InstallationId` فیلد مستقل Line نیست و از `License.InstallationId` مشتق می‌شود؛ گزینهٔ نگهداری صریح آن فقط به‌عنوان گزینهٔ جایگزین برای تصمیم مالک باقی مانده است. متن بخش ۴ و سایر شواهد تاریخی برای حفظ audit trail بازنویسی نشده‌اند؛ بخش‌های ممیزی و شواهد (۱ تا ۴ و ۸ به بعد) همچنان سابقهٔ تاریخی‌اند و این یادداشت، ارجاع معتبر برای طراحی جاری است.

## ۱. وضعیت فاز صفر (شواهد واقعی)

| مورد | وضعیت واقعی |
|---|---|
| `main` | `98c2edf3` — دقیقاً برابر SHA ادعایی مأموریت؛ آخرین commit: «record D-105 Company-owned default and controlled sharing (#713)» |
| PR #711 (S3-0d-b مأموریت قبلی) | merged در `2026-11-19T05:33:11Z`؛ CI روی HEAD آن ۱۵/۱۵ موفق بود |
| PRهای باز مخزن | صفر |
| Working tree سندباکس | پاک؛ شاخهٔ مأموریت جدید از `origin/main` ساخته شد (بدون reset/stash/force) |

**وضعیت مراحل دیگر S1 (فقط برای منع تداخل؛ از «نقشه اجرایی S1» در `LICENSING_IMPLEMENTATION_PLAN.md`):** S1-a = فعال‌سازی زنجیرهٔ موجود، مانعش تصمیم مالک برای Gate v5 (`CustomerConsumerReady=false` + تنظیم خاموش) و گسترش allow-list ACK است؛ S1-e وابسته به license-first gate (#396/#384)؛ S1-c نیازمند جزئیات OD-20/21/22؛ S1-d نیازمند signer عملیاتی (#236)؛ S1-f جزئیات Proposed (D-102)؛ S1-g منتظر OD-26. هیچ‌کدام در این مأموریت پیاده‌سازی یا بازطراحی نشدند.

## ۲. قابلیت‌های موجود و شواهد (کد واقعی `main@98c2edf3`)

### ۲.۱ نهادهای تجاری/امنیتی Vendor (`Vendor/Vendor.Persistence/Domain/`)
| نهاد | فایل | نکات کلیدی مأموریت |
|---|---|---|
| `VendorCustomer` | `VendorBusinessEntities.cs` | `CustomerNumber` (طبق PROJECT_STATUS هنوز قابل‌ویرایش — ریسک ثبت‌شده، اصلاحش ممنوع در این مأموریت)، `LegalName`، `Status`، RowVersion |
| `VendorInstallation` | همان فایل | `CustomerId`، `InstallationCode`، `Environment`، `Status`، `LastSeenAtUtc`؛ «هیچ‌وقت اتصال SQL به دیتابیس مشتری ندارد» |
| `VendorContract` | همان فایل | `CustomerId`، `ContractNumber`، `CurrencyCode`، بازه؛ doc خود فایل: «پرداخت یا تأیید حقوقی را imply نمی‌کند» ⇒ مبنای سفارش/پرداخت نیست |
| `VendorLicense` | همان فایل | `CustomerId`، `ContractId?`، `InstallationId?`، `LicenseKind` (perpetual/term)، `MaxCompanies?`، `SubsystemsJson`، `CompanyEntitlementsDraftJson` (دُرافت v2؛ پس از صدور به CompanyReference+Descriptor کاهش می‌یابد)، `GraceDays`، **`LastIssuedSequence`**، بازه‌های اعتبار |
| `VendorApprovalRequest` | `VendorApprovalRequest.cs` | Maker-Checker عمومی: `ResourceType`/`ResourceId` (پیش از مصرف، روی رکورد واقعی authorize می‌شود)، `TargetStatus`، **`ExpectedResourceRowVersion`**، `RequestedByUserId`، `Status`، `ReviewedByUserId`/`ReviewedAtUtc`/`ReviewComment` |
| `VendorSignedLicenseEnvelope` | `VendorSignedLicenseEnvelope.cs` | `LicenseId` + `Sequence` + `KeyId`/`Algorithm`/`EnvelopeJson`/`PayloadSha256`؛ unique `(LicenseId, Sequence)` در DB ⇒ آخرین نگهبان صدور تکراری |
| `VendorSignedRevocationSnapshot`، `VendorProvisioningRequest` | همان پوشه | snapshot ابطال و زنجیرهٔ provisioning v4/v5 |

### ۲.۲ سه Candidate Builder (`Vendor/Vendor.Host/Signing/VendorLicenseAmendmentPayloadBuilder.cs`)
| Builder | PR/CI | محافظ‌های fail-closed (از کد) |
|---|---|---|
| `TryBuildMaxCompaniesIncrease` | #527، ۱۵/۱۵ | `license.LastIssuedSequence == currentPayload.Sequence` (باطل‌شدن payload قدیمی رد می‌شود)، `Sequence != int.MaxValue`، فقط افزایش، همان LicenseId/InstallationId، نگه‌داشتن تمام entitlementها و تاریخ‌ها |
| `TryBuildCompanyEntitlementIncrease` | #558، ۱۵/۱۵ | `CompanyReference` فقط از payload v2 **امضاشده و verify‌شده**؛ هیچ Company ساخته نمی‌شود؛ ماژول/Feature ناشناخته، تکراری، از-قبل‌موجود، Feature بی‌ماژول مالک، کاهش/برابر، سقف خارج از بازه رد می‌شود؛ v1→v2 تبدیل ممنوع |
| `TryBuildSupportAndUpdatePeriodExtension` | #565، ۱۵/۱۵ | فقط دوره‌ای که قبلاً در payload امضاشده **موجود** است تمدید می‌شود؛ null به‌معنای مجوز ضمنی نیست؛ `expiresAtUtc` و entitlementهای دیگر دست‌نخورده؛ term منقضی قابل amendment نیست |

تست‌ها: `tests/Vendor.Host.Tests/VendorLicenseAmendmentPayloadBuilderTests.cs` (v1/v2، حفظ حقوق، metadata stale، cross-customer، replay/sequence، عقب‌گرد زمان، استقلال support/update).

### ۲.۳ خط صدور و Maker-Checker موجود (الگویی که amendment باید از آن پیروی کند)
- `VendorLicenseIssuanceService.IssueApprovedAsync` (150 خط): فقط **داخل تراکنش caller**؛ اعتبارسنجی approval (نوع/هدف در allow-list، Pending، `RequestedBy != reviewer`، تطبیق RowVersion approval و resource) → `TryBuild` → `envelopeFactory.CreateAsync` (خطای هر signer ⇒ **503 fail-closed**، بدون commit) → ذخیرهٔ envelope + تنظیم `LastIssuedSequence` + `Approved` کردن approval + audit (`IVendorAuditWriter` درون همان تراکنش) ⇒ شکست هر گام = rollback کامل.
- قرارداد رسمی: `Vendor/docs/license-issuance-transaction.md` (گام‌های ۱..۷، unique `(LicenseId, Sequence)`، RowVersion، signer پیش‌فرض `UnavailableLicenseSigningProvider` بی‌کلید، تست SQL فقط با `FARANO_VENDOR_TEST_SQLSERVER`).
- Endpointهای Maker-Checker: `Vendor/Vendor.Host/Authorization/VendorApprovalEndpoints.cs` — `GET /approvals`، `POST /approvals/status-change` (ثبت درخواست)، `POST /approvals/{id}/decision` (تصمیم)، `POST /licenses/{id}/approval-request`؛ مصرف‌کنندگان فعلی: issuance، provisioning، revocation.
- امضا: `ILicenseSigningProvider` با پیاده‌سازی `Pkcs11SigningProvider` و پیش‌فرض `UnavailableLicenseSigningProvider`؛ Production signer (#236) همچنان مانع ثبت‌شده.
- Frontend: `Vendor.Web` فرم‌های `CustomerForm`/`ContractForm`/`CompanyEntitlementsEditor`/`BusinessLists` دارد؛ هیچ فرم سفارش/پرداخت ندارد (درست است — ایجاد UI از این مأموریت خارج است).

## ۳. شکاف‌های واقعی #393 (تفکیک «موجود» از «غایب»)

**Implemented/Verified (به‌عنوان candidate خالص):** سه builder بالا + قرارداد Payload v2 + صدور اولیهٔ نسخهٔ v2 (#682) + gate دوکلیدی provisioning v5 (#683، بسته) + تراکنش issuance + maker-checker عمومی + snapshot ابطال.

**NotImplemented (فهرست Issue #393 و PROJECT_STATUS 2026-11-18):** Commercial Order و OrderLine؛ ثبت مدرک پرداخت (Payment Evidence)؛ persistence خرید؛ endpoint و UI فروش؛ approval اختصاصی amendment؛ signing/export پایدار به‌ازای خرید؛ وضعیت‌های Purchased/Delivered/Acknowledged؛ ثبت Applied فقط پس از ACK معتبر یا تأیید معتبر Cloud.

**سه شکاف تجاری ثبت‌شده در Issue که تصمیم مالک می‌خواهند:**
1. نوع و حداقل اطلاعات مدرک پرداخت؛
2. نقش و اختیار تأییدکنندهٔ پرداخت؛
3. معنای دقیق وضعیت‌های پرداخت و نقطهٔ ورود به Maker-Checker.

## ۴. پاسخ پرسش‌های مأموریت از کد واقعی (بدون قطعیت‌سازی سیاست)

1. **اتصال به CustomerAccount/Installation:** «CustomerAccount» سمت Vendor در کدِ فعلی همان `VendorCustomer` است (نام `CustomerAccount` فقط در ERD پیشنهادی بخش ۵ `LICENSING_DATA_MODEL.md` آمده و پیاده‌سازی با نام `VendorCustomer` جلو رفته است — این اختلاف نام برای اولین بار اینجا ثبت می‌شود). `VendorLicense.CustomerId` و `VendorLicense.InstallationId` هر دو FK واقعی به `VendorCustomer`/`VendorInstallation` دارند.
2. **سفارش به کدام متصل شود؟** پیشنهاد (تصمیم نهایی با مالک): `VendorSalesOrder.CustomerId` الزامی و `InstallationId` در سطح **Line** (نه Order) — چون یک خرید واقعی می‌تواند چند Installation را هدف بگیرد، اما هر amendment فقط به یک Installation/LicenseId bind می‌شود (سازگار با builderها). Customer-only بودن سفارش، ثبتِ بعدی را به انتخاب Installation در مرحلهٔ صدور موکول می‌کند؛ Installation-only بودن، خرید سطح مشتری (مثل تمدید چندنصب) را حذف می‌کند.
3. **سطح افزایش ظرفیت Company:** `maxCompanies` در payload **سطح Installation** است (D-059؛ doc builder؛ `VendorLicense.MaxCompanies`)؛ خط فروشش باید Installation-target باشد و amendment همان LicenseId را بالا می‌برد.
4. **Module/Feature/UserSeat و CompanyReference معتبر:** طبق قواعد fail-closed `TryBuildCompanyEntitlementIncrease`، مرجع شرکت فقط از payload v2 امضاشده پذیرفته می‌شود و mapping معتبر از مسیر #392 (`CompanySetupRecords` + signed ACK) می‌آید؛ تا زمانی که Gate صدور v5 با تصمیم مالک باز نشود، خط فروشِ این نوع حتی با ثبت موفق سفارش، در مرحلهٔ ساخت candidate به‌طور fail-closed رد می‌شود (`provisioning_company_reference_mapping_required`/قواعد builder). این باید در UI فروش هم «قابل‌انتخاب‌نبودن» را نشان دهد، نه خطای پس از خرید.
5. **تمدید Support/Update:** خط فروش به `VendorLicense` (⇒ LicenseId/InstallationId) bind می‌شود؛ builder فقط دورهٔ موجود را تمدید می‌کند و معنای «null → مجوز» هنوز سیاست مصوب ندارد؛ بنابراین نوع خط «اعطای اولیهٔ support/update از صفر» فعلاً قابل صدور نیست و باید در تصمیم کاتالوگ صریح شود.
6. **چندقلمی:** طراحی پیشنهادی یک `VendorSalesOrder` با چند `VendorSalesOrderLine` typed است؛ هر Line در زمان صدور، دقیقاً یک candidate amendment (Sequence+1) می‌سازد؛ دو Line با یک هدف و همان مقادیر باید در زمان ثبت سفارش رد یا ادغام شوند تا دو بار صدور یک entitlement برای یک خرید ممکن نشود (نگهبان DB + کنترل ثبت).
7. **PaymentReference حداقلی:** متن Issue خودش «ثبت دستی PaymentReference/مبلغ/ارز و metadata محدود» را پیشنهاد کرده و درگاه پرداخت را ممنوع؛ برای نسخهٔ اول کافی است، به‌شرط نسخه‌دار بودن schema مدرک (JsonVersion) و append-only بودن اصلاحات آن.
8. **اتصال وضعیت‌ها به Maker-Checker و سپس Amendment:** الگوی موجود `VendorApprovalRequest` عمومی است (ResourceType/TargetStatus/ExpectedResourceRowVersion)؛ پس از تأیید پرداخت، maker-checker روی **خط سفارش** (نه صرفاً وضعیت متنی) با همین الگو اجرا می‌شود و تصمیم Approved، صدور را در همان تراکنش ۷گامی `license-issuance-transaction.md` به envelope می‌رساند.
9. **رد پرداخت/لغو سفارش:** وضعیت‌های terminal باید از وضعیت‌های قابل‌ادامه جدا بمانند (Rejected/Cancelled/Expired)؛ رویدادها append-only با `IVendorAuditWriter`؛ هیچ برگشتی از terminal و هیچ حذفی رخ ندهد؛ رد پرداخت هرگز entitlement صادرشدهٔ قبلی را تغییر نمی‌دهد (تمدید/افزایش فقط با amendment جدید و sequence جدید قابل واگرد است — قاعدهٔ فعلی حذف entitlement در amendment وجود ندارد و باید بماند).
10. **جلوگیری از صدور دوباره/دوبار تخصیص:** چهار نگهبان موجود هم‌افزا هستند: (الف) unique DB `(LicenseId, Sequence)`؛ (ب) چک `LastIssuedSequence == currentPayload.Sequence` در هر builder؛ (ج) `ExpectedResourceRowVersion`/RowVersion روی approval و رکورد؛ (د) commit واحد تراکنش (envelope+approval+audit). برای سطح سفارش، یک پیوند 0..1 از Line به envelope صادرشده (ResourceType جدید برای approval) باید افزوده شود تا هر Line حداکثر یک بار منجر به صدور شود؛ این نیاز به schema approval مستقل دارد.

## ۵. طراحی حداقلی پیشنهادی (`Proposed` — مالک هنوز تصمیم نگرفته)

**انتخاب طراحی: گزینهٔ A (یک سفارش، خطوط typed) — متن Issue هم همین را «مسیر کم‌هزینهٔ پیشنهادی» می‌داند.**

| موجودیت پیشنهادی | فیلدهای حداقلی (نهایی با schema approval) | رابطه |
|---|---|---|
| `VendorSalesOrder` | Id، OrderNumber، `CustomerId`→VendorCustomer (Restrict)، Status، CurrencyCode، `CreatedByUserId`، `CreatedAtUtc`، RowVersion | 1:n خطوط؛ فقط Vendor DB |
| `VendorSalesOrderLine` | Id، `OrderId`، LineType (کاتالوگ بستهٔ اولیه: `MaxCompaniesIncrease` / `CompanyEntitlementIncrease` / `SupportUpdatesExtension`)، `InstallationId?`→VendorInstallation، `LicenseId?`→VendorLicense، TargetRef (برای نوع Company-scoped: `CompanyReference` عیناً از payload — هرگز نباید مالکیت بسازد یا مقدار آزاد بپذیرد)، Quantity/NewValue (بازه‌دار)، Status، `PaymentEvidenceJson` (نسخه‌دار، محدود)، `ApprovalRequestId?`→VendorApprovalRequest، `IssuedEnvelopeId?`→VendorSignedLicenseEnvelope، RowVersion | هر Line ⇐ حداکثر یک approval ⇐ حداکثر یک envelope |

- **مدرک پرداخت (پیشنهادی برای بحث، نه تصمیم):** `Method`، `ReferenceNumber`، `Amount`، `CurrencyCode`، `EvidenceVersion`، `RecordedAtUtc`، `RecordedByUserId`، حداکثر یک فیلد یادداشت؛ هیچ فایل باینری/پوشهٔ اسنادی در نسخهٔ اول پیشنهاد نمی‌شود؛ نسخه‌دار تا افزودن فیلدهای بعدی بدون migration معنایی.
- **وضعیت‌ها (پیشنهادی، نگاشت به متن Issue):** `Draft → PaymentRecorded (PendingVerification) → Purchased → Issued → Delivered → Acknowledged/Applied` + terminalهای `Rejected/Cancelled/Expired`. نقطهٔ ورود Maker-Checker: تصمیم PendingVerification→Purchased (پرداخت‌شناسی) و Issued فقط پس از approval دومِ صدور با الگوی موجود — تفکیک «تأیید پرداخت» از «تأیید صدور» باید صریحاً تصویب شود (سؤال مالک بند ۳.۳ Issue).
- ** Applied/ACK:** طبق D-060 و مرز #394، Vendor وضعیت Applied را فقط با ACK معتبر (allow-list ردهای ACK، گسترشش در PR مستقل) یا تأیید معتبر Cloud ثبت می‌کند؛ هیچ مسیر مستقیم به Customer DB وجود ندارد (D-059 بند ۱۵).
- **صادرمند شدن خط:** همان گام‌های `VendorLicenseIssuanceService` (approval revalidation → builder → signer fail-closed → envelope + sequence + audit در یک commit). هیچ سرویس صدور موازی ساخته نمی‌شود.
- **آنچه عمداً طراحی نشد:** درگاه پرداخت، Ledger موازی، CRM سنگین، entity به‌ازای نوع محصول، گردش‌کار چندمرحله‌ای اضافه، abstraction جدید (مطابق ممنوعیت‌های مأموریت و YAGNI/D-102 بند ۶).

## ۶. گزینه‌های بررسی‌شده (تصمیم نهایی با مالک)

| گزینه | مزیت | هزینه/ریسک | داوری |
|---|---|---|---|
| **A — تک `SalesOrder` + خطوط typed** | کوتاه‌ترین فاصله تا builderها و approval موجود؛ ممیزی واحد؛ توسعه‌پذیر با LineType | نیازمند schema approval مستقل؛ LineType باید بستهٔ اولیهٔ صریح داشته باشد | **پیشنهاد** |
| B — Entity جدا برای هر نوع محصول | هر نوع مستقل | سه جدول/سه workflow/سه approval ناهمساز؛ با ممنوعیت مأموریت در تضاد؛ فروش ترکیبی سخت | رد (و با روح #393 سازگار نیست) |
| C — سفارش روی `VendorContract` موجود | بدون جدول جدید | doc خود entity می‌گوید «پرداخت را imply نمی‌کند»؛ آمیختن قرارداد حقوقی با خرید واقعی؛ RowVersion/status های قرارداد آلوده می‌شود | رد؛ Contract فقط مرجع اختیاری روی Order |

## ۷. گردش‌کار پیشنهادی سفارش → ACK (سطح طراحی؛ همهٔ گام‌ها server-authorized + antiforgery + no-store مثل الگوی موجود)

1. اپراتور `SalesOrder` با خطوط typed ثبت می‌کند (اعتبارسنجی: نوع خط مجاز، هدف فعال، `CompanyReference` فقط از payload verify‌شده).
2. مدرک پرداخت دستی ثبت می‌شود → وضعیت Line/Order `PaymentRecorded`.
3. تأییدکنندهٔ مجاز (نقش/اختیار = سؤال مالک) مدرک را می‌سنجد → `Purchased` (یا `Rejected`).
4. ثبت `VendorApprovalRequest` برای صدور (Maker-Checker موجود؛ requester ≠ reviewer).
5. تصمیم Approved ⇒ تراکنش ۷گامی موجود: builder → signer (fail-closed ⇒ 503، بدون commit) → envelope با Sequence+1 → audit → commit.
6. تحویل خروجی آنلاین/فایل امضاشده (مسیر #394؛ در این Issue فقط ثبت وضعیت `Delivered`).
7. ACK معتبر یا تأیید Cloud ⇒ `Acknowledged/Applied`؛ در غیر این صورت وضعیت `Delivered` با `LastSyncedAt` روشن می‌ماند (D-098/مرز D-060).

## ۸. موارد نیازمند تصمیم مالک (بدون تکرار تصمیم‌های Accepted)

1. مدرک پرداخت: فیلدهای حداقلی، فرمت (نسخه‌دار JSON پیشنهاد شد)، نگهداری/حذف و اینکه آیا فایل پیوست لازم است.
2. نقش و اختیار تأییدکنندهٔ پرداخت و اینکه آیا «تأیید پرداخت» و «تأیید صدور» دو نقش مستقل‌اند (پیش‌فرض: بله، الگوی requester≠reviewer موجود).
3. معنای دقیق وضعیت‌ها و نقطهٔ ورود Maker-Checker (PendingVerification↔Purchased؛ Delivered↔Acknowledged).
4. کاتالوگ بستهٔ اولیهٔ `LineType` و اینکه «اعطای اولیهٔ support/update از null» در کدام برش وارد شود.
5. سطح اتصال سفارش (Order به Customer + Line به Installation — پیشنهاد بند ۴.۲) و سیاست سفارش بدون Installation.
6. سیاست ارز و مبلغ (ارز Order در برابر ارز Contract؛ نرخ/تبدیل لازم نیست در نسخهٔ اول؟).
7. سیاست سفارش چندخطی ناهمزمان (خطوط مستقل صدور شوند یا کل سفارش یکجا؟).
8. ریسک ثبت‌شدهٔ `CustomerNumber` قابل‌ویرایش (binding provisioning) — تصمیم جدا؛ در این مأموریت تغییر داده نشد.
9. ارتباط اختیاری/الزامی سفارش با `VendorContract` (پیشنهاد: اختیاری).

## ۹. وابستگی‌ها و ریسک‌ها

- **وابستگی واقعی:** schema approval مستقل برای دو Entity و فیلدهای پیوندی (پیش‌نیاز هر PR کد)؛ gate صدور v5 (تصمیم مالک، S1-a) برای نوع Company-scoped؛ signer عملیاتی (#236) برای صدور واقعی؛ gسترش allow-list ACK برای Applied؛ مسیر تحویل #394 برای Delivered.
- **ریسک‌ها:** دوباره‌کاری با ERD پیشنهادی بخش ۵ `LICENSING_DATA_MODEL.md` (نام‌های `CustomerAccount/Contract/SupportContract/LicenseRevision` با پیاده‌سازی واقعی `VendorCustomer/VendorContract/VendorLicense/VendorSignedLicenseEnvelope` جور نیست — هر PR schema باید فقط نام‌های واقعی را مبنا بگیرد یا divergence را صریح ثبت کند)؛ دوبار-تخصیص در فروش چندخطی (نگهبان‌های بند ۴.۱۰)؛ خطای تجاری/فنی LineType؛ سقف `int.MaxValue` برای Sequence (هشدار عملیاتی، نه در کوتاه‌مدت)؛ نبود Production signer ⇒ همهٔ مسیر صدور فعلاً 503 fail-closed است.

## ۱۰. موارد بررسی‌شده و اعتبارسنجی واقعی

- **فایل‌های اصلی خوانده‌شده:** `docs/INDEX.md`، `docs/PROJECT_STATUS.md`، `docs/AGENT_COORDINATION.md`، `docs/DECISIONS.md` (D-097..D-105)، `docs/LICENSING.md`، `docs/LICENSING_IMPLEMENTATION_PLAN.md`، `docs/LICENSING_DATA_MODEL.md`، `docs/LICENSING_OPEN_DECISIONS.md`، `docs/ROADMAP.md`، `docs/SECURITY.md`، `ISSUE-393-vendor-addon-payload-builder.md`، `Vendor/Vendor.Persistence/Domain/*.cs`، `VendorLicenseAmendmentPayloadBuilder.cs`، `VendorLicenseIssuanceService.cs`، `VendorApprovalEndpoints.cs`، `Vendor/docs/license-issuance-transaction.md`، ساختار `Vendor.Web`.
- **تغییرات این PR (فقط docs):** این handoff (جدید) + ورود `WORK_LOG.md`. هیچ فایل کد/تست/Schema دست نخورد.
- **اعتبارسنجی:** `git diff --check` پاک؛ Build/Test اجرا نشد (تغییر اجرایی وجود ندارد — طبق AGENTS.md صادقانه ثبت می‌شود)؛ آخرین وضعیت CI واقعی مرجع: post-merge builderها ۱۵/۱۵ (#527/#558/#565) و PR #711 ۱۵/۱۵. نتیجهٔ CI این PR پس از اجرا گزارش می‌شود و از ادعای موفقیت بدون اجرا خودداری شده است.
- **ممنوعیت‌های رعایت‌شده:** بدون Migration/Schema، بدون تغییر `Server/**`، بدون بازکردن Gate v5، بدون Production signer، بدون تغییر `CustomerNumber`، بدون import داده از Customer DB، بدون ADR/Issue تکراری و بدون بازنویسی WORK_LOG.
