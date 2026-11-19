# S1-b / Issue #393 — سند طراحی نهایی سفارش فروش، پرداخت و صدور (نسخهٔ ۲)

- **Issue:** #393 | **Parent:** #389 (D-059) | **مرز معماری:** D-060 (Vendor CRM & Commercial → Licensing Engine)
- **تاریخ:** 2026-11-19 | **مبنا:** `origin/main@6f539679` — شامل Merge PR #714 (نسخهٔ ۱ همین طراحی) و بدون هیچ تغییر کد بعدی
- **جایگزینی:** بخش‌های ۵ (طراحی)، ۶ (گزینه‌ها) و ۷ (گردش‌کار) از `ISSUE-393-s1-b-commercial-order-payment-design-prep.md` با این سند جایگزین می‌شوند؛ بخش‌های ممیزی و شواهد آن سند معتبر می‌مانند.
- **ماهیت:** فقط طراحی برای تصمیم‌گیری مالک. هیچ Entity، Migration، API، UI، Permission عملیاتی، Package یا تغییری در کد ساخته نشده است. همهٔ نام‌ها و مدل‌ها `Proposed` هستند و هر پیاده‌سازی به Issue مستقل + schema approval نیاز دارد.
- **اصلاح‌های v2 نسبت به v1:** جداسازی سه چرخهٔ وضعیت، انتقال مدرک پرداخت از JSON روی Line به Entity مستقل `VendorPayment` روی Order با ستون‌های typed، تفکیک صریح تأیید پرداخت از تأیید صدور روی همان زیرساخت Maker-Checker موجود، حذف `TargetRef/NewValue` عمومی و جایگزینی با ستون‌های typed به‌ازای هر LineType، و تطبیق دقیق ضدتکرار صدور با کد واقعی.
- **اصلاح‌های v2.1 (بازبینی دوم، 2026-11-19 — رفع چهار ابهام بازبینی PR #717، بدون تغییر جهت معماری):** ① `LicenseId` برای **هر چهار** LineType الزامی و پیش از درخواست تأیید صدور روی Line تثبیت می‌شود؛ `InstallationId` فیلد مستقل Line نیست و از License مشتق می‌شود (هر سه builder واقعی زنجیرهٔ License→Installation→Customer را fail-closed اعتبارسنجی می‌کنند و `VendorLicense.InstallationId` در entity **nullable** است) ② سیاست «حداکثر یک صدور باز به‌ازای هر License» با چک اپلیکیشن + پیشنهاد filtered unique index — بدون Queue/Event Bus/Lock ③ معنای `PaymentRecorded/Confirmed` و نسبت صدور با تأیید پرداخت → تصمیم ۱۱ با سه گزینه و پیشنهاد پیش‌فرض فنی (Accepted نشده) ④ یکتایی سراسری `ReferenceNumber` فرض نمی‌شود؛ بدون Namespace معنا ندارد → تصمیم ۱۰ بازنویسی و موکول به schema approval.

---

## ۱. خلاصه وضعیت واقعی Repository

| مورد | وضعیت واقعی (2026-11-19) |
|---|---|
| `main` | `6f539679` — «docs(vendor): prepare commercial order and payment design» = Merge PR #714؛ پیش از آن `98c2edf3` (#713/D-105) |
| PR #714 | **Merged**؛ CI آن ۱۵/۱۵ موفق و `mergeable_state=clean` بود؛ هیچ کامنت بازبینی انسانی (به‌جز اطلاع rate-limit CodeRabbit) ثبت نشده است |
| Issue #393 | باز؛ سه builder (#527/#558/#565) در main Verified؛ Order/Payment/persistence/approval/statusها NotImplemented |
| کد Vendor | از `98c2edf3` به `6f539679` **فقط docs تغییر کرده**؛ همهٔ شواهد کد نسخهٔ ۱ (بخش ۲ handoff قبلی) همچنان برقرار است |
| تصمیم‌ها | D-097..D-105 وضعیت واقعی خود را دارند؛ هیچ Proposed در این سند Accepted تلقی نشده است |

شواهد کلیدی که این طراحی به آن‌ها تکیه می‌کند (کد واقعی، نه مستندات ERD):
- نهادها: `VendorCustomer/VendorInstallation/VendorContract/VendorLicense` (`Vendor/Vendor.Persistence/Domain/VendorBusinessEntities.cs`)، `VendorApprovalRequest`، `VendorSignedLicenseEnvelope`.
- قید یکتای پایگاه‌داده `(LicenseId, Sequence)` و `CheckConstraint Sequence >= 1` و FK Restrict روی `VendorSignedLicenseEnvelopes` (`VendorDbContext.cs:178-194`).
- allow-list `ResourceType` در endpoint تصمیم: فقط `customer|installation|license|provisioning` (`VendorApprovalEndpoints.cs:166`) و در endpoint ثبت درخواست عمومی فقط `customer|installation` (`:62`)؛ approval لایسنس از endpoint اختصاصی `POST /licenses/{id}/approval-request` ساخته می‌شود.
- کاتالوگ permission نسخه‌دار (`VendorPermissionCatalog`، `Version=1`، claim `vendor:permission:v1`) شامل `vendor.licenses.issue` به‌صورت مجزای از `vendor.licenses.manage`.
- تراکنش صدور: `VendorLicenseIssuanceService` (requester≠reviewer، RowVersion approval و resource، signer fail-closed=503 بدون commit، audit درون-تراکنشی، گرفتن SqlException 2601/2627 به‌عنوان `license_sequence_already_issued`).
- سه builder با گارد `license.LastIssuedSequence == currentPayload.Sequence` و `Sequence != int.MaxValue`.
- `VendorLicense.InstallationId` **nullable** است (`Guid?` در `VendorBusinessEntities.cs`) و هر سه امضای builder `installation` و `customer` را می‌گیرند و زنجیره را با `license_installation_mismatch` fail-closed اعتبارسنجی می‌کنند (`VendorLicenseAmendmentPayloadBuilder.cs:13-43`)؛ گارد اجرای stale: `license.RowVersion ↔ approval.ExpectedResourceRowVersion` (`VendorLicenseIssuanceService.cs:50`).

## ۲. معماری پیشنهادی و مرز مسئولیت هر موجودیت

همهٔ موجودیت‌ها فقط در Vendor DB، بدون هیچ FK یا اتصال به دیتابیس مشتری (D-059 بند ۱۵) و بدون هیچ دادهٔ حسابداری مشتری:

| موجودیت | مسئولیت | همسایه‌های ممنوع |
|---|---|---|
| `VendorSalesOrder` | چرخهٔ **تجاری** خرید: ثبت سفارش، ارز، پیوند قرارداد اختیاری | هیچ وضعیت صدور/تحویل روی آن |
| `VendorPayment` | ثبت دستی مدرک پرداخت و وضعیت بررسی آن (یک Order چند Payment) | هیچ محاسبهٔ مالی، تسویه یا Ledger |
| `VendorSalesOrderLine` | قلم خرید typed + چرخهٔ **fulfillment** تا صدور | هیچ دادهٔ پرداخت |
| `VendorApprovalRequest` (موجود) | دو تأیید جدا: بررسی پرداخت و تأیید صدور؛ همان رکورد عمومی با ResourceTypeهای جدید | سیستم approval جدید ساخته نمی‌شود |
| `VendorSignedLicenseEnvelope` (موجود) | خروجی نهایی امضاشده؛ مرجع فنی entitlement | Line فقط به آن **اشاره** می‌کند |
| Audit (`IVendorAuditWriter` موجود) | تاریخچهٔ append-only همهٔ گذارها | جدول event-sourcing جدید لازم نیست |

## ۳. مدل داده پیشنهادی (سطح مفهومی؛ جدول فیلد به‌تفکیک)

### ۳.۱ `VendorSalesOrder`
| فیلد | الزام | مالکیت داده | هدف | اعتبارسنجی | Index/Unique |
|---|---|---|---|---|---|
| `Id` Guid | الزامی | Vendor | PK | — | PK |
| `OrderNumber` string(32) | الزامی | Vendor | شناسهٔ نمایشی/پیگیری | یکتا، فرمت ثابت (پیشنهاد: `SO-yyyy-####`) | **Unique** |
| `CustomerId` Guid | الزامی | Vendor | مرجع مشتری (نه CustomerNumber) | FK به `VendorCustomer.Id`، Restrict | Index |
| `ContractId` Guid? | اختیاری | Vendor | لنگر تجاری قرارداد | FK به `VendorContract.Id` + **سازگاری مشتری** (پیشنهاد: unique `(Id, CustomerId)` روی قرارداد و FK مرکب `(ContractId, CustomerId)`) | Index |
| `CurrencyCode` char(3) | الزامی | Vendor | ارز واحد سفارش | کد ISO 4217؛ مبنای الزام ارز Paymentها | — |
| `TotalAmount` decimal(18,2)? | اختیاری | Vendor | نمایش جمع (فقط اطلاعاتی) | ≥0؛ بدون هیچ محاسبهٔ مالی/تسویه | — |
| `CommercialStatus` string | الزامی | Vendor | چرخهٔ تجاری (بخش ۵) | مجموعهٔ بسته | Index |
| `CreatedByUserId` / `CreatedAtUtc` / `RowVersion` | الزامی | Vendor | ممیزی و هم‌زمانی | — | RowVersion |

### ۳.۲ `VendorPayment` (جدید؛ جایگزین `PaymentEvidenceJson` نسخهٔ ۱)
| فیلد | الزام | مالکیت داده | هدف | اعتبارسنجی | Index/Unique |
|---|---|---|---|---|---|
| `Id` Guid | الزامی | Vendor | PK | — | PK |
| `OrderId` Guid | الزامی | Vendor | پرداخت متعلق به **سفارش** است (یک پرداخت می‌تواند چند ردیف را پوشش دهد) | FK Restrict | Index |
| `Amount` decimal(18,2) | الزامی | Vendor | مبلغ ثبت‌شدهٔ دستی | > 0 | — |
| `CurrencyCode` char(3) | الزامی | Vendor | ارز پرداخت | **باید برابر Currency سفارش** (v1؛ چندارز موکول) | — |
| `Method` string | الزامی | Vendor | کانال ثبت‌شده | مجموعهٔ بستهٔ اولیه: `BankTransfer \| Card \| Cash \| Other` | — |
| `ReferenceNumber` string(128) | الزامی | Vendor | شماره مرجع بیرونی (رسید/تراکنش) | اجباری، غیرخالی؛ **یکتایی سراسری فرض نمی‌شود** — شمارهٔ رسید بین بانک‌ها/ارائه‌دهندگان می‌تواند تکرار شود؛ در v1 فقط Index غیریکتا برای جست‌وجو + هشدار تشخیص تکرار هنگام ثبت (جست‌وجو در همان مشتری/سفارش)؛ در صورت تصمیم به قید unique، Namespace صریح لازم است (تصمیم ۱۰؛ schema approval) | Index (غیریکتا) |
| `PaidAtUtc` DateTimeOffset | الزامی | Vendor | زمان ادعایی پرداخت | ≤ اکنون + تلورانس، ≥ CreatedAt سفارش | — |
| `RecordedAtUtc` / `RecordedByUserId` | الزامی (سیستمی) | Vendor | چه کسی/چه زمانی ثبت کرد | — | — |
| `VerificationStatus` string | الزامی | Vendor | چرخهٔ بررسی پرداخت (بخش ۵) | مجموعهٔ بسته: `Recorded \| Verified \| Rejected` | Index (OrderId) |
| `VerifiedByUserId?` / `VerifiedAtUtc?` / `ReviewComment?` | شرطی | Vendor | فقط با تصمیم تأییدکنندهٔ پرداخت پر می‌شود | در `Verified/Rejected` الزامی، در `Recorded` NULL | — |
| `RowVersion` | الزامی | Vendor | هم‌زمانی + گارد ExpectedResourceRowVersion در approval | — | RowVersion |

- **JSON حذف شد:** همهٔ فیلدها typed و قابل اعتبارسنجی/ایندکس هستند؛ JSON فقط برای توسعهٔ آیندهٔ نسخه‌دار موردی می‌شود، نه برای دادهٔ اصلی.
- **تخصیص پرداخت به ردیف (allocation):** در v1 نیست؛ صدور از مسیر تأیید مجاز می‌شود نه از «تسویهٔ کامل»؛ قاعدهٔ «جمع Verified ≥ Total»، معنای `PaymentRecorded/Confirmed` و گیت صدور نسبت به پرداخت = تصمیم مالک (تصمیم ۱۱).

### ۳.۳ `VendorSalesOrderLine` — typed، بدون TargetRef/NewValue عمومی
| فیلد | الزام | هدف/اعتبارسنجی | Index/Unique |
|---|---|---|---|
| `Id` / `OrderId` / `LineNo` | الزامی | ترتیب ردیف؛ یکتا با `(OrderId, LineNo)` | **Unique `(OrderId, LineNo)`** |
| `LineType` string | الزامی | مجموعهٔ بستهٔ اولیه (جدول ۳.۴) | Index |
| `LicenseId` Guid | الزامی برای **هر چهار نوع** | هدف amendment؛ هر سه builder واقعی روی زنجیرهٔ `License→Installation→Customer` کار می‌کنند و آن را fail-closed اعتبارسنجی می‌کنند (`license_installation_mismatch`؛ امضای `TryBuildMaxCompaniesIncrease` و `TryBuildSupportAndUpdatePeriodExtension` هر دو `installation`+`customer` می‌گیرند). License پیش از درخواست تأیید صدور روی Line **تثبیت** می‌شود و در زمان صدور فقط از `line.LicenseId` خوانده می‌شود — انتخاب آزاد License هدف در لحظهٔ تصمیم وجود ندارد. قواعد: License باید متعلق به `order.CustomerId`، `Issued` و **installation-bound** باشد (`VendorLicense.InstallationId` nullable است؛ License بدون Installation هدف هیچ builder فعلی نیست). تغییر License هدف پس از ثبت درخواست تأیید = لغو درخواست و درخواست جدید (RowVersion Line قفل است) | Index |
| `InstallationId` | **فیلد Line نیست** | از `License.InstallationId` مشتق می‌شود و در سه نقطه هم‌راستایی کنترل می‌شود: ثبت سفارش (سازگاری مشتری)، ساخت درخواست تأیید، و داخل builder در زمان صدور. گزینهٔ جایگزین در صورت نظر مالک: نگه‌داشتن `InstallationId` صریح برای دو نوع Installation-scoped با FK مرکب تساوی — گزینهٔ (ب) تصمیم ۵ | — |
| `CompanyReference` string(64)? | شرطی | فقط `CompanyEntitlementIncrease`؛ **عیناً** از payload v2 امضاشدهٔ آن License؛ در ثبت سفارش فقط اعتبارسنجی شکل، تطبیق با payload در زمان approval توسط builder موجود انجام می‌شود | — |
| `NewMaxCompanies` int? | شرطی | فقط `MaxCompaniesIncrease`؛ > مقدار فعلی (چک نهایی نزد builder) | — |
| `EntitlementKind` string? | شرطی | فقط `CompanyEntitlementIncrease`؛ مجموعهٔ بسته: `Module \| Feature \| UserSeat` | — |
| `EntitlementTargetCode` string(64)? | شرطی | فقط `CompanyEntitlementIncrease`؛ کد ماژول یا کلید Feature یا شناسهٔ Seat؛ باید در کاتالوگ payload مجاز باشد (چک نهایی builder) | — |
| `NewLimit` int? | شرطی | فقط `CompanyEntitlementIncrease`؛ > مقدار فعلی، در بازهٔ مجاز (چک نهایی builder) | — |
| `NewSupportUntilUtc` DateTimeOffset? | شرطی | فقط `SupportExtension`؛ > مقدار فعلی payload؛ در صورت NULL بودن تاریخ فعلی، رد (policy فعلی builder) | — |
| `NewUpdatesUntilUtc` DateTimeOffset? | شرطی | فقط `UpdatesExtension`؛ همان قواعد | — |
| `FulfillmentStatus` string | الزامی | چرخهٔ صدور ردیف (بخش ۵) | Index |
| `ApprovalRequestId` Guid? / `IssuedEnvelopeId` Guid? | شرطی | پیوند 0..1 به approval و 0..1 به envelope صادرشده | پیشنهاد: **filtered unique index روی `IssuedEnvelopeId` WHERE NOT NULL** ⇒ هر ردیف حداکثر یک صدور |
| `RowVersion` | الزامی | گارد ExpectedResourceRowVersion در approval صدور | RowVersion |

### ۳.۴ ماتریس اجبار/ممنوعی ستون‌ها به‌ازای هر LineType (مجموعهٔ بستهٔ اولیه)
| LineType | LicenseId | Installation (مشتق از License؛ فیلد Line ندارد) | CompanyReference | ستون مقدار | Builder مصرفی نهایی |
|---|---|---|---|---|---|
| `MaxCompaniesIncrease` | **الزامی** | License باید installation-bound و Active باشد | ممنوع | `NewMaxCompanies` | `TryBuildMaxCompaniesIncrease` |
| `CompanyEntitlementIncrease` | **الزامی** | همان قاعده؛ `CompanyReference` باید در payload v2 همان License باشد | **الزامی** | `EntitlementKind` + `EntitlementTargetCode` + `NewLimit` | `TryBuildCompanyEntitlementIncrease` |
| `SupportExtension` | **الزامی** | همان قاعده | ممنوع | `NewSupportUntilUtc` | `TryBuildSupportAndUpdatePeriodExtension` (فقط support) |
| `UpdatesExtension` | **الزامی** | همان قاعده | ممنوع | `NewUpdatesUntilUtc` | همان builder (فقط updates) |

- چهار نوع، کل دامنهٔ سه builder را می‌پوشاند؛ **Entity مستقل به‌ازای نوع محصول لازم نیست** (تأیید مجدد). ستون‌های «ممنوع» در هر نوع باید NULL بمانند؛ اجرای این ماتریس: اعتبارسنجی اپلیکیشن الزامی + پیشنهاد Check Constraint به‌ازای هر نوع در schema approval — از جمله `LicenseId IS NOT NULL` برای همهٔ انواع. هم‌راستایی License↔Installation↔Customer با constraint قابل اثبات نیست و برعهدهٔ اعتبارسنجی اپلیکیشن (ثبت و ساخت درخواست) + fail-closed builder در زمان صدور است.
- تفکیک Support/Updates به دو نوع ردیف (به‌جای یک ردیف با دو تاریخ) عمدی است: هر ردیف = یک تغییر هدفمند = یک candidate = یک envelope؛ ممیزی یک‌به‌یک می‌شود و استقلال دو مفهوم (D-022/D-059/D-097) در سطح ردیف دیده می‌شود.

## ۴. نمودار جریان سفارش تا اعمال مجوز

```mermaid
flowchart TD
    A[اپراتور مجاز: ثبت VendorSalesOrder + Lineهای typed] --> B[ثبت دستی VendorPayment<br/>ReferenceNumber/Amount/Currency/PaidAt]
    B --> C{تأیید پرداخت<br/>Maker-Checker: ResourceType=salesOrderPayment<br/>permission: vendor.payments.verify}
    C -- Rejected --> XR[Payment.Rejected<br/>اثر تجاری طبق تصمیم ۱۱]
    A --> D[ثبت ApprovalRequest صدور<br/>ResourceType=salesOrderLine, TargetStatus=Issued<br/>License هدف = line.LicenseId تثبیت‌شده<br/>شرط - فقط یک درخواست صدور باز به‌ازای هر License]
    C -.->|فقط اگر تصمیم ۱۱ گیت پرداخت را انتخاب کند| D
    D --> E{تأیید صدور<br/>Maker-Checker موجود<br/>requester≠reviewer, RowVersion}
    E -- Rejected --> XL[Line.Rejected - terminal]
    E -- Approved --> F[تراکنش اتمیک ۷گامی موجود:<br/>builder → signer fail-closed → Envelope<br/>Sequence+1 → audit → commit]
    F --> G[Line.Issued + IssuedEnvelopeId<br/>unique (LicenseId, Sequence)]
    G --> H[تحویل آنلاین/فایل امضاشده<br/>مسیر #394 - خارج از این طراحی]
    H --> I[ACK معتبر allow-list یا تأیید Cloud<br/>مسیر #394]
    I --> J[Line.Delivered/Applied<br/>فقط پس از ACK/Cloud معتبر]
    F -.->|signer unavailable| XF[503 fail-closed<br/>هیچ commit ناقصی]
```

مرزها: گام‌های A..G در محدودهٔ #393/S1-b است؛ H..J متعلق به #394 است و این طراحی فقط جای وضعیت را رزرو می‌کند. مسیر صدور از پرداخت مستقل است؛ فقط اگر مالک در تصمیم ۱۱ گزینه‌ای را برگزیند که گیت پرداخت را لازم می‌داند، Verified شدن پرداخت پیش‌شرط ثبت درخواست صدور خواهد بود. تأیید پرداخت به‌تنهایی هیچ مجوز صدوری ایجاد نمی‌کند.

## ۵. وضعیت‌ها و انتقال‌های مجاز (پاسخ به «آیا سه ستون مستقل لازم است؟»)

**پاسخ: نه. سه چرخه، سه مکان دارند و یک جدول واحد را آلوده نمی‌کنند:**

| چرخه | محل | مقادیر بسته (Proposed) | نتیجه‌گیری |
|---|---|---|---|
| تجاری (Order) | `VendorSalesOrder.CommercialStatus` | `Open → PaymentRecorded → Confirmed` + terminalهای `Cancelled`، `Rejected`؛ **معنا:** `PaymentRecorded` = وجود حداقل یک رکورد `VendorPayment` (هر وضعیتی)؛ قاعدهٔ رسیدن به `Confirmed` = **تصمیم مالک (تصمیم ۱۱)** — پیشنهاد پیش‌فرض فنی: نخستین Payment `Verified`؛ پرداخت جدید پس از `Confirmed` هم مجاز است و گذار به عقب رخ نمی‌دهد | یک ستون کافی است؛ وضعیت تجاری هرگز با صدور مخلوط نمی‌شود |
| بررسی پرداخت | `VendorPayment.VerificationStatus` | `Recorded → Verified \| Rejected` | چون پرداخت Entity مستقل شد، وضعیتش هم مستقل و طبیعی است |
| Fulfillment (Line) | `VendorSalesOrderLine.FulfillmentStatus` | `Pending → ApprovalRequested → Issued` + terminal `Rejected \| Cancelled`؛ فقط در بازیابی approval کهنه طبق بخش ۷، گذار غیرترمینال `ApprovalRequested → Pending` مجاز است؛ **قاعدهٔ حاکم: در هر لحظه حداکثر یک عملیات صدور باز (`ApprovalRequested`) به‌ازای هر License** — پس از terminal شدن یک Line، درخواست Line بعدیِ هم‌هدف با `LastIssuedSequence` و RowVersion تازه ساخته می‌شود (بخش ۷) | ردیف‌ها مستقل صادر می‌شوند؛ «بسته‌شدن سفارش» مقدار ذخیره‌شده نیست، **مشتق** است (همهٔ ردیف‌ها terminal) — از دو منبع حقیقت جلوگیری می‌کند |
| اعمال Entitlement در مشتری | **فعلاً ستونی ساخته نمی‌شود** | `Delivered/Applied` در PR مربوط به #394 به‌صورت additive به `FulfillmentStatus` اضافه می‌شود | چون ACK معتبر و allow-list هنوز مسیر #394 است؛ ستون خالی امروز = ابهام فردا. ثبت گذارها با audit append-only موجود کافی است |

قواعد انتقال: هیچ گذار معکوسی از terminal مجاز نیست؛ هیچ حذفی رخ نمی‌دهد (append-only ممیزی)؛ گذار فقط از مسیر endpoint مجاز + antiforgery + server-side authorization انجام می‌شود؛ لغو سفارش فقط وقتی مجاز است که هیچ ردیفی `Issued` نشده باشد (ردیف صادرشده فقط با amendment جدید و sequence جدید قابل واگرد است — حذف entitlement در builderهای فعلی وجود ندارد و باید هم نباشد).

## ۶. قواعد امنیتی و Maker-Checker

1. **زیرساخت واحد، دو ResourceType:** `salesOrderPayment` (TargetStatus=Verified) و `salesOrderLine` (TargetStatus=Issued) روی همان `VendorApprovalRequest`. سیستم approval موازی ساخته نمی‌شود؛ هر چهار نقطهٔ dispatch در کد آینده باید گسترش یابد: allow-list ساخت درخواست (`VendorApprovalEndpoints.cs:62`)، نگاشت permission هنگام ساخت (`PermissionFor` در `:66`)، allow-list تصمیم (`:166`) و نگاشت permission هنگام تصمیم (`PermissionFor` در `:189`). `PermissionFor` باید `salesOrderPayment` را به `vendor.payments.verify` و `salesOrderLine` را به `vendor.licenses.issue` نگاشت کند. ساخت درخواست‌ها مثل الگوی لایسنس از **endpoint اختصاصی** انجام می‌شود نه endpoint عمومی status-change. License هدف صدور از `line.LicenseId` تثبیت‌شده خوانده می‌شود (انتخاب آزاد در لحظهٔ تصمیم وجود ندارد) و ساخت درخواست صدور جدید برای License‌ای که درخواست باز دارد رد می‌شود.
2. **جلوگیری از تأیید توسط درخواست‌دهنده:** قاعدهٔ موجود `approval.RequestedByUserId == reviewerUserId ⇒ conflict` در هر دو سرویس آینده الزامی است (همان الگوی `VendorLicenseIssuanceService`).
3. **Permission مستقل:** بررسی پرداخت به permission جدید **پیشنهادی** `vendor.payments.verify` نیاز دارد (هم‌خوان با نام‌گذاری/نسخهٔ `VendorPermissionCatalog` v1)؛ ثبت سفارش/پرداخت با `vendor.orders.manage` پیشنهادی؛ تأیید صدور با `vendor.licenses.issue` **موجود**. تعریف عملیاتی این permissionها در PR پیاده‌سازی بعدی است؛ این سند فقط نام را پیشنهاد می‌دهد.
4. **تفکیک سه مفهوم:** ثبت پرداخت ≠ تأیید پرداخت ≠ تأیید صدور؛ هر کدام actor، endpoint و audit event جدا دارد. این هم‌پوشانی نقش‌ها را از نظر فنی ناممکن نمی‌کند (یک مدیر می‌تواند هر دو permission را داشته باشد)؛ سخت‌گیری بیشتر (ممنوعیت تأیید صدور توسط همان تأییدکنندهٔ پرداختِ همان سفارش) = تصمیم مالک (تصمیم ۲).
5. **fail-closed:** signer ناموجود/نامعتبر ⇒ 503 و **هیچ** commit (approval، sequence، envelope، audit) — الگوی ثابت‌شدهٔ `license-issuance-transaction.md`. `AllowAll`/bypass توسعه/کلید در کد، DB یا artifact ممنوع (قاعدهٔ موجود).
6. **ACK:** ثبت `Delivered/Applied` فقط با ACK معتبر مطابق allow-list ACK (گسترشش PR مستقل) یا تأیید معتبر Cloud؛ بدون آن هیچ وضعیت Applied ثبت نمی‌شود (D-060؛ مأموریت بند ۳.۴).
7. هیچ تغییری در سیاست امنیتی، RowVersion، Audit، Antiforgery یا بازکردن Gate v5 بدون تأیید مالک انجام نمی‌شود؛ این سند هیچ Gateای باز نمی‌کند.

## ۷. کنترل هم‌زمانی و جلوگیری از صدور تکراری (تطبیق با کد واقعی)

| لایه | مکانیزم (شواهد فعلی) | پوشش |
|---|---|---|
| DB | unique `(LicenseId, Sequence)` + `CK Sequence>=1` + FK Restrict (`VendorDbContext.cs:178-194`) | درخواست هم‌زمان دوم با همان Sequence در commit شکست می‌خورد (SqlException 2601/2627 → `license_sequence_already_issued`) — **نگهبان نهایی، مستقل از اپلیکیشن** |
| سیاست یک صدور باز به‌ازای هر License | چک اپلیکیشن در endpoint ساخت درخواست تأیید صدور: License‌ای که Line در `ApprovalRequested` دارد درخواست جدید نمی‌پذیرد (`license_issuance_in_progress`)؛ پیشنهاد نگهبان DB: **filtered unique index `ON VendorSalesOrderLines(LicenseId) WHERE FulfillmentStatus = 'ApprovalRequested'`** (Proposed؛ schema approval) | حداکثر یک عملیات صدور فعال به‌ازای هر License **بدون Queue/Event Bus/Distributed Lock**؛ دو Line هم‌هدف اصلاً هم‌زمان به مرحلهٔ تصمیم نمی‌رسند؛ حتی اگر هر دو عبور کنند، unique `(LicenseId, Sequence)` نگهبان نهایی است |
| Builder | `license.LastIssuedSequence == currentPayload.Sequence` و `Sequence != int.MaxValue` | payload کهنه/stale و overflow رد می‌شود |
| RowVersion | برای درخواست صدور Line، `ExpectedResourceRowVersion` نسخهٔ Line را نگه می‌دارد و فیلد جداگانهٔ پیشنهادی `ExpectedLicenseRowVersion` (افزودهٔ nullable در schema approval؛ الزامی برای `salesOrderLine`) نسخهٔ License را ثبت می‌کند؛ هر دو نسخه پیش از ساخت amendment داخل همان تراکنش با رکورد جاری تطبیق داده می‌شوند و اختلاف هرکدام ⇒ 409 conflict؛ نسخهٔ جدید License هرگز خودکار جایگزین نسخهٔ زمان درخواست نمی‌شود | جلوگیری از اجرای approval کهنه، هم برای تغییر Line و هم برای تغییر License بین درخواست و تصمیم |
| تراکنش | envelope + approval + sequence + audit در **یک** SaveChanges/txn؛ audit درون-تراکنشی | هیچ وضعیت ناقصی در شکست signer/audit باقی نمی‌ماند |
| پیوند Line→Envelope | `IssuedEnvelopeId` 0..1 + filtered unique index (پیشنهاد schema) | هر ردیف حداکثر یک صدور؛ retry روی همان ردیف به‌جای envelope جدید، وضعیت موجود را برمی‌گرداند |
| رفتار Retry و بازیابی approval کهنه | اگر نسخهٔ License یا Line با نسخهٔ ثبت‌شده در approval منطبق نباشد، تصمیم صدور با 409 رد می‌شود و هیچ builder یا signer اجرا نمی‌شود. مسیر بازیابی در همان تراکنش: approval به وضعیت `Stale` (وضعیت موجود در مدل/endpoint فعلی) می‌رود؛ فقط اگر Line هنوز به همان ApprovalRequest متصل و در `ApprovalRequested` باشد و RowVersion ثبت‌شدهٔ Line منطبق باشد، Line به `Pending` برمی‌گردد، پیوند درخواست باز آزاد می‌شود و audit هر دو تغییر ثبت می‌شود. اگر Line نیز تغییر کرده باشد، کل عملیات rollback و conflict می‌شود؛ هیچ وضعیت یا payloadی خودکار جایگزین نمی‌شود و اصلاح/لغو باید از مسیر مجاز و ممیزی‌شده انجام شود. پس از بازیابی موفق، درخواست تازه فقط با خواندن مجدد Line/License و ثبت هر دو RowVersion ساخته می‌شود؛ اتوماتیک-retry ممنوع است | slot یکتای باز آزاد می‌شود؛ درخواست بعدی با نسخه‌های جاری ساخته می‌شود؛ درخواست stale هرگز با payload جدید اجرا نمی‌شود |

نتیجه: بررسی‌های سطح اپلیکیشن لازم‌اند اما **کافی نیستند**؛ امنیت در برابر اجرای هم‌زمان و تکرار، به قید پایگاه‌داده و RowVersion تکیه دارد — همان استراتژی اثبات‌شدهٔ issuance فعلی.

## ۸. وابستگی‌ها و Blockerها

| وابستگی | وضعیت واقعی | اثر بر S1-b |
|---|---|---|
| تصمیم‌های مالک (بخش ۹؛ اکنون ۱۱ مورد) | باز | **پیش‌نیاز صفرم**؛ بدون آن schema approval شکل نمی‌گیرد |
| Schema approval + Migration آینده | ممنوع در این مأموریت | هر PR داده‌ای بعدی به Issue و تأیید مالک نیاز دارد |
| Signer عملیاتی (#236) | مانع Production؛ پیش‌فرض Unavailable | تا آن زمان صدور واقعی fail-closed=503 است؛ مراحل ۱..۳ طراحی به signer وابسته نیستند |
| Gate v5 / `CustomerConsumerReady` | بسته (تصمیم مالک لازم) | `CompanyEntitlementIncrease` پس از ثبت سفارش، در زمان صدور fail-closed رد می‌شود؛ UI باید «غیرقابل‌انتخاب» را از قبل نشان دهد |
| Delivery/ACK (#394) | پیاده‌سازی‌نشده | گام‌های Delivered/Applied تا آن زمان معنا ندارند؛ فقط رزرو وضعیت |
| allow-list ACK | ۴ کد رد ثابت (#699)؛ گسترش = PR مستقل | پیش‌نیاز Applied |
| `CompanyReference` معتبر | فقط از payload v2 امضاشده (#683) | مبنای اعتبارسنجی `CompanyEntitlementIncrease` |

## ۹. تصمیم‌های نیازمند تأیید مالک (فهرست نهایی تصمیم‌گیری)

| # | موضوع | گزینه‌های واقعی | پیشنهاد فنی | دلیل | پیامد امنیتی/داده‌ای | ضروری برای شروع؟ |
|---|---|---|---|---|---|---|
| 1 | ساختار و فیلدهای حداقلی مدرک پرداخت | (الف) JSON روی Line (v1)؛ (ب) Entity مستقل `VendorPayment` روی Order با ستون‌های typed؛ (ج) فیلد واحد روی Order | **(ب)** | پرداخت چندردیفی و چندمرحله‌ای واقعی است؛ ستون typed اعتبارسنجی/ایندکس/ممیزی می‌دهد؛ JSON نه | ReferenceNumber یگانه = ضد ثبت دوبارهٔ رسید؛ بدون آن ریسک پرداخت تکراری | **بله** (PR1) |
| 2 | نقش تأییدکنندهٔ پرداخت و تفکیک اختیارات | (الف) همان نقش مدیریت؛ (ب) permission جدید `vendor.payments.verify` + requester≠reviewer؛ (ج) ب + ممنوعیت هم‌نقشی با تأیید صدور در همان سفارش | **(ب)**؛ (ج) به مالک | زیرساخت موجود فقط گسترش ResourceType می‌خواهد؛ `vendor.licenses.issue` الگوی همان جداسازی است | (ج) سخت‌گیرانه‌تر ولی عملیات روزمره را به دو نفر گره می‌زند | **بله** (PR2) |
| 3 | وضعیت‌های سفارش/پرداخت/صدور و Applied | (الف) یک Status عمومی؛ (ب) سه ستون؛ (ج) دو ستون (Order+Line) + وضعیت روی Payment + Applied موکول به PR #394 | **(ج)** | سه چرخه با نرخ تغییر متفاوت؛ ستون Applied بدون مسیر ACK امروز بی‌معناست | رد گذار معکوس/حذف رکورد؛ audit append-only | **بله** (PR1)؛ Applied فقط پیش از PR تحویل |
| 4 | LineTypeهای اولیه و محدودهٔ هرکدام | ۴ نوع بسته (بخش ۳.۴)؛ یا باز گذاشتن نوع | **۴ نوع بسته + ماتریس NULL + `LicenseId` الزامی برای همهٔ انواع (تثبیت پیش از تأیید صدور)** | یک‌به‌یک با سه builder واقعی که همه زنجیرهٔ License→Installation→Customer را می‌خواهند؛ نوع باز = اعتبارسنجی مبهم | Check Constraint به‌ازای نوع + `LicenseId IS NOT NULL` در schema approval | **بله** (PR1) |
| 5 | اتصال سفارش به Customer و ردیف به Installation/License | (الف) License الزامی در ردیف و Installation مشتق از آن؛ (ب) `InstallationId` صریح برای دو نوع Installation-scoped با FK مرکب تساوی؛ (ج) ردیف بدون License — مرفوض، با builderهای واقعی ناسازگار است | **(الف): Order→Customer الزامی؛ `LicenseId` الزامی در همهٔ ردیف‌ها و تثبیت پیش از تأیید صدور؛ Installation فیلد Line نیست و از License مشتق می‌شود؛ سفارش بدون Installation معتبر است اما License بدون Installation هدف هیچ builder فعلی نیست** | یک خرید واقعی چند Installation را هدف می‌گیرد؛ منبع حقیقت واحد (License خودش Installation دارد و entity آن nullable است، پس دوتایی‌نویسی نمی‌کنیم) | هم‌راستایی زنجیره با اعتبارسنجی اپلیکیشن در ثبت/درخواست + fail-closed builder (`license_installation_mismatch`)؛ گزینهٔ (ب) FK مرکب تساوی می‌خواهد | **بله** (PR1) |
| 6 | سیاست Currency | چند ارز؛ یک ارز در سفارش | **یک ارز در سفارش؛ ارز Payment = ارز سفارش؛ فقط اطلاعاتی، بدون محاسبه/تسویه** | ساده‌ترین مدل معتبر؛ تسویه مالی فعلاً وجود ندارد | هیچ محاسبهٔ مالی از این داده انجام نمی‌شود | **بله** (PR1) |
| 7 | صدور مستقل یا یکپارچه ردیف‌ها | یکجا برای کل سفارش؛ مستقل به‌ازای ردیف | **مستقل به‌ازای ردیف؛ بسته‌شدن سفارش مشتق است** | sequence لایسنس ماهیتاً ترتیبی است؛ یکجا = قفل‌شدن بقیه ردیف‌ها با یک شکست | نگهبان‌های بخش ۷ برای هم‌زمانی دو ردیف | **بله** (PR3/4) |
| 8 | `CustomerId` به‌جای `CustomerNumber` | CustomerNumber؛ CustomerId Guid | **CustomerId (Guid) در همهٔ FKهای جدید؛ CustomerNumber فقط نمایشی/جست‌وجو** | CustomerNumber امروز قابل‌ویرایش است (ریسک ثبت‌شده)؛ Guid پایدار است | تغییر آیندهٔ CustomerNumber هیچ رکوردی را نمی‌شکند | **بله** (PR1) |
| 9 | اجباری/اختیاری بودن `VendorContract` | اجباری؛ اختیاری | **اختیاری + FK مرکب سازگاری مشتری** | doc خود entity: قرارداد پرداخت را imply نمی‌کند؛ اجبار، خرید بدون قرارداد را ناممکن می‌کند | الزام قرارداد فعال = سیاست آیندهٔ مالک | **بله** (PR1) |
| 10 | یکتایی `ReferenceNumber` پرداخت | (الف) بدون قید unique در v1: index جست‌وجو + هشدار تشخیص تکرار هنگام ثبت؛ (ب) unique با Namespace صریح `(Method, ProviderOrBank, ReferenceNumber)` — نیازمند فیلد جدید نام ارائه‌دهنده/بانک | **(الف)**؛ قید unique تا تعیین دامنهٔ namespace فقط Proposed و موکول به schema approval | شمارهٔ رسید بین بانک‌ها/ارائه‌دهندگان می‌تواند تکرار شود؛ یکتایی سراسری فرض فنی نیست و بدون دادهٔ Provider قابل اجرا نیست | در (ب) رسیدهای مشروع با Provider ثبت‌نشده مسدود می‌شوند؛ در (الف) ریسک ثبت دوبارهٔ همان رسید با هشدار انسانی کنترل می‌شود | می‌تواند در schema approval حل شود (موکول‌پذیر) |
| 11 | قاعدهٔ رسیدن به `Confirmed` و نسبت صدور با تأیید پرداخت (بدون allocation/Ledger/درگاه) | (الف) `Confirmed` با نخستین Payment `Verified`؛ جمع Verified فقط نمایش اطلاعاتی؛ صدور Line مستقل از پرداخت (فقط Maker-Checker خودش)؛ (ب) `Confirmed` وقتی جمع Verified ≥ TotalAmount (TotalAmount الزامی می‌شود و رفتار NULL باید تعریف شود)؛ صدور مستقل از پرداخت؛ (ج) `Confirmed` دستی اپراتور مجاز + گیت اختیاری «حداقل یک Verified» برای صدور | **(الف)** — پیشنهاد پیش‌فرض فنی؛ Accepted ثبت نشده | بدون allocation هیچ قاعدهٔ خودکار دقیقی برای کفایت پرداخت وجود ندارد؛ (الف) ساده‌ترین مدل معتبر است و خرید اعتباری/پرداخت مرحله‌ای را نمی‌شکند (چند Payment مجاز است) | در (الف) سفارش با پرداخت جزئی هم Confirmed و قابل صدور می‌شود؛ در (ب) فقط وضعیت تجاری تا تسویهٔ کامل Confirmed نمی‌شود و چون صدور در همین گزینه مستقل از پرداخت است، Confirmed نبودن به‌تنهایی گیت صدور نیست؛ در (ج) بیشترین انعطاف با اتکا به فرآیند انسانی | **بله** — قاعدهٔ Confirmed برای PR2، گیت صدور برای PR3 |

سؤال‌های جزئی‌تر (فرمت OrderNumber، حداکثر طول Note، نام دقیق permissionها) به schema approval/PR مربوطه موکول شد تا فهرست مالک کوتاه بماند.

## ۱۰. برنامه پیاده‌سازی مرحله‌ای با PRهای کوچک (فقط پس از تأیید مالک)

| مرحله | PR | محتوا | وابستگی | تست |
|---|---|---|---|---|
| 0 | Issue/schema-approval | تصویب ۹+۱ تصمیم، نام جدول‌ها/فیلدها، ماتریس NULL، ایندکس‌ها و Check Constraintها | بخش ۹ | بازبینی مالک |
| 1 | PR-۱ | `VendorSalesOrder` + `VendorSalesOrderLine` + `VendorPayment` + DbContext/Migration + endpointهای ثبت (order/line/payment) + permissionهای read/manage | 0 | واحد + یکپارچه SQL (FARANO_VENDOR_TEST_SQLSERVER)؛ اعتبارسنجی ماتریس ۳.۴ |
| 2 | PR-۲ | تأیید پرداخت: ResourceType=`salesOrderPayment` + endpoint اختصاصی ثبت درخواست + گسترش allow-list تصمیم + سرویس Verify + permission `vendor.payments.verify` + قاعدهٔ گذار `Confirmed` طبق تصمیم ۱۱ | 1 | requester≠reviewer؛ RowVersion؛ رد/تأیید + audit؛ گذار تجاری طبق گزینهٔ انتخابی مالک |
| 3 | PR-۳ | درخواست تأیید صدور: ResourceType=`salesOrderLine` + endpoint اختصاصی + اتصال به Maker-Checker موجود + تثبیت License هدف روی درخواست + سیاست «یک درخواست باز به‌ازای هر License» (چک اپلیکیشن + ایندکس فیلترشده پیشنهادی) | 1،2 | گذار ApprovalRequested؛ رد درخواست دوم برای License با درخواست باز (`license_issuance_in_progress`)؛ درخواست stale و conflict؛ گیت صدور-پرداخت در صورت انتخاب مالک |
| 4 | PR-۴ | صدور: سرویس جدید به الگوی `VendorLicenseIssuanceService` (builder→signer→envelope→audit در یک txn) + ثبت `IssuedEnvelopeId` | 3 + signer عملیاتی (#236) برای صدور واقعی | duplicate/replay/downgrade، sequence concurrency، signer fail-closed، cross-customer |
| 5 | PR-۵ | تحویل آنلاین/فایل و ACK معتبر (مسیر #394) | 4 + #394 + گسترش allow-list ACK | duplicate delivery، lost response، import ACK معتبر/نامعتبر |
| 6 | PR-۶ | ثبت Applied پس از ACK/Cloud + additive بودن مقدار جدید به `FulfillmentStatus` | 5 | ACK تکراری/replay، انطباق allow-list، عدم Applied بدون ACK |

هر PR کوچک، تک‌موضوعی و با معیار پذیرش مستقل است؛ هیچ مرحله‌ای پیش از آماده‌شدن وابستگی‌هایش شروع نمی‌شود؛ تا PR-۴ هیچ صدوری (حتی آزمایشی با signer غیرفعال) انجام نمی‌شود.
