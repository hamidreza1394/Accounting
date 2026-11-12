# راستی‌آزمایی نهایی طراحی بازرگانی خارجی (TRADE DESIGN FINAL VERIFICATION)

**تاریخ:** ۱۴۰۵/۰۸/۲۲ (۲۰۲۶-۱۱-۱۲)
**هدف این سند:** نه بازنویسی طراحی، بلکه اثبات صحت اصلاحات دور قبل (`TRADE_DESIGN_FINAL_REVIEW.md`) و رفع شکاف‌های باقی‌مانده با شواهد مستقیم از خودِ سند — طبق دستور صریح شما.
**دامنه:** فقط مستندات. **هیچ کد، Migration، Entity، API یا جدول دیتابیس ایجاد یا تغییر نشده.**
**نتیجهٔ این دور:** در حین راستی‌آزمایی، **دو شکاف واقعی** پیدا شد (نه در دور قبل اعلام‌شده) و در `docs/TRADE_DESIGN_V3_2.md` رفع شد (نه حدس زده شد؛ هر دو رفع صرفاً تکمیل فنی قواعد از‌پیش‌موجود بودند). جزئیات در بخش ۳.

---

## ۱. Tenant و Cross-company Security — سه جدول گروه C دوطرفه

### جدول مستند دسترسی

| | `BillOfLadingOrderAllocation` (حوزهٔ ۴) | `CustomsDeclarationBillAllocation` (حوزهٔ ۵) | `ForexVoucherBillCoverage` (حوزهٔ ۷) |
|---|---|---|---|
| **شرکت مالک سند اول (Parentِ قفل‌شونده در کنترل مجموع)** | `BillOfLadingOwnerCompanyId` (= `Voyage.OwnerCompanyId`) | `CustomsDeclarationOwnerCompanyId` | `ForexVoucherOwnerCompanyId` |
| **شرکت مالک سند دوم (طرف مقابل)** | `OrderRegistrationOwnerCompanyId` | `BillOfLadingOwnerCompanyId` | `BillOfLadingOwnerCompanyId` |
| **شرکت ثبت‌کنندهٔ ردیف Allocation (Create/Update/Cancel)** | فقط مالک سند اول | فقط مالک سند اول | فقط مالک سند اول |
| **شرکت‌های مجاز به مشاهده (List/Detail/Report)** | هر دو (OR منطقی) | هر دو (OR منطقی) | هر دو (OR منطقی) |
| **محدودهٔ دقیق داده قابل‌مشاهده برای طرف مقابل** | فقط همین ردیف: `AllocatedTonnage`، `Status`، تاریخ، ارجاع شناسه/شماره طرف مقابل — نه بدنهٔ کامل سند | همان الگو | همان الگو |
| **کنترل در List** | `WHERE OwnerCompanyId=@C OR CounterpartyCompanyId=@C` در Query Handler | همان الگو | همان الگو |
| **کنترل در Detail** | همان شرط Scope؛ در غیر این صورت ۴۰۴ | همان الگو | همان الگو |
| **کنترل در Create** | فقط اگر `@C = BillOfLadingOwnerCompanyId` | فقط اگر `@C = CustomsDeclarationOwnerCompanyId` | فقط اگر `@C = ForexVoucherOwnerCompanyId` |
| **کنترل در Update (اصلاح = Supersede)** | همان شرط Create | همان شرط Create | همان شرط Create |
| **کنترل در Cancel** | همان شرط Create | همان شرط Create | همان شرط Create |
| **کنترل در Report** | همان شرط Scope خواندن (OR) | همان الگو | همان الگو |
| **رفتار در صورت دسترسی غیرمجاز** | `404 NotFound` یکسان برای «ناموجود» و «غیرمجاز» (هم‌راستا با `docs/SECURITY.md`: «شرکت غیرمجاز و ناموجود پاسخ یکسان ۴۰۴ دارند») | همان | همان |

### جلوگیری از IDOR و افشای اطلاعات سند طرف مقابل

1. **بدون شناسهٔ قابل‌پیش‌بینی/ترتیبی به‌تنهایی کافی برای دسترسی:** هر دسترسی به ردیف (با هر شناسه‌ای که در URL/پارامتر بیاید) همیشه از Scope Check سمت سرور عبور می‌کند؛ هیچ `TenantId`/شناسهٔ شرکت ارسالی کلاینت مبنای تصمیم نیست (طبق D-013 — تکرار صریح قاعدهٔ سراسری مخزن برای این سه جدول در `TRADE_DESIGN_V3_2.md` قواعد مشترک، بند گروه C-۵).
2. **بدون نشت وجود رکورد:** تلاش برای خواندن یک `Id` معتبر که متعلق به شرکت ثالث است (نه هیچ‌کدام از دو ستون مالکیت) همان `404` را می‌گیرد که یک `Id` کاملاً ناموجود می‌گیرد — یک مهاجم نمی‌تواند با آزمایش شناسه‌های پیاپی فهمید کدام شناسه «هست ولی مال من نیست» در برابر «اصلاً نیست».
3. **بدون افشای بدنهٔ سند مبدأ از طریق ردیف پیوند:** فیلدهای برگشتی برای طرف غیرمالک محدود به همان ردیف تخصیص است (`AllocatedTonnage`/`CoveredAmount`، `Status`، تاریخ، شناسه/شماره ارجاعی) — هیچ Join خودکار به بدنهٔ کامل `BillOfLading`/`CustomsDeclaration`/`ForexVoucher`/`OrderRegistration` طرف مقابل در همان Query Handler انجام نمی‌شود؛ اگر طرف غیرمالک بخواهد جزئیات بیشتر، باید مجوز مستقل روی خودِ آن سند داشته باشد (که به‌طور طبیعی ندارد، چون سند دیگری با `TenantId` مستقل است).
4. **بدون تسهیم نوشتن:** چون فقط مالک سند اول (Parentِ قفل‌شونده) اجازهٔ Create/Update/Cancel دارد، طرف مقابل حتی اگر بداند شناسهٔ ردیف را، نمی‌تواند روی آن بنویسد — این هم یک کلاس حمله را می‌بندد (نوشتن غیرمجاز، نه فقط خواندن).
5. **بدون Security Engine جدید:** پیاده‌سازی پیشنهادی همان الگوی تکی `CompanyAccessService` مخزن است، فقط با یک متد Scope اختصاصی کوچک (شرط OR برای خواندن، شرط تکی برای نوشتن) — دقیقاً طبق محدودیت صریح شما.

**نتیجه: Verified.** شواهد: `docs/TRADE_DESIGN_V3_2.md` قواعد مشترک (بند گروه C) و حوزه‌های ۴، ۵، ۷ (بخش‌های «مشخصات Tenant/Security گروه C» و «تفکیک دقیق خواندن از نوشتن»، افزوده‌شده در همین دور).

---

## ۲. Allocation Concurrency — ترتیب دقیق عملیات

برای هر سه جدول (`BillOfLadingOrderAllocation`، `CustomsDeclarationBillAllocation`، `ForexVoucherBillCoverage`) دقیقاً همان ۷ مرحلهٔ درخواستی شما، با مقدار مشخص هر پارامتر:

| مرحله | عملیات | جدول/کلید قفل | جزئیات |
|---|---|---|---|
| ۱ | شروع Transaction | — | `DbContext.Database.BeginTransactionAsync()` با `IsolationLevel.ReadCommitted` (نه `Serializable` کامل — قفل صریح مرحلهٔ ۲ انحصار لازم را بدون نیاز به بالابردن Isolation کل تراکنش تضمین می‌کند) |
| ۲ | قفل Parent | `trade.BillOfLadings`/`trade.CustomsDeclarations`/`trade.ForexVouchers` — کلید: `WHERE Id = @X` (و `TenantId = @CurrentTenantId` برای Scope) | `SELECT Id FROM ... WHERE Id = @X WITH (UPDLOCK, HOLDLOCK)` — قفل فقط روی همین یک ردیف، نه کل جدول |
| ۳ | خواندن Active Allocations | جدول تخصیص (`...Allocation`/`...Coverage`) | `SELECT ... WHERE ParentId = @X AND Status = 'Active'` — در همان تراکنش، بعد از قفل Parent |
| ۴ | محاسبهٔ SUM | همان نتیجهٔ مرحلهٔ ۳ | `SUM(AllocatedTonnage یا CoveredAmount)` |
| ۵ | اعتبارسنجی ظرفیت | — | `SUM + NewAmount <= DeclaredTonnage/VoucherAmount` |
| ۶ | ثبت یا جایگزینی Allocation | جدول تخصیص | اگر اصلاح: ردیف قبلی → `Superseded` + ردیف جدید `Active` با `SupersedesAllocationId`؛ اگر تخصیص جدید: فقط درج `Active` جدید |
| ۷ | Commit | — | `CommitAsync()` — قفل Parent همین‌جا آزاد می‌شود (`HOLDLOCK` تا پایان تراکنش نگه می‌دارد) |

### چرا قفل روی کدام جدول/کلید و چه مدت باقی می‌ماند

- **کلید قفل = دقیقاً همان `Id` رکورد Parent** (`BillOfLading`/`CustomsDeclaration`/`ForexVoucher`)، نه ردیف‌های تخصیص (که ممکن است صفر باشند — این دقیقاً نقص راهکار قبلی بود).
- **مدت:** از مرحلهٔ ۲ تا پایان مرحلهٔ ۷ (Commit/Rollback) — `HOLDLOCK` قفل را تا پایان تراکنش نگه می‌دارد؛ این معادل `Serializable` **فقط برای همین یک ردیف** است، نه برای کل جدول یا کل تراکنش.
- **چرا درخواست‌های مربوط به Parentهای متفاوت بی‌دلیل مسدود نمی‌شوند:** قفل `WITH (UPDLOCK, HOLDLOCK) WHERE Id = @X` یک قفل **سطح ردیف** (Row-level lock) روی کلید `@X` است، نه قفل جدول (Table lock) و نه قفل صفحه (Page lock) که می‌تواند ردیف‌های مجاور را هم بگیرد. دو تراکنش هم‌زمان که `@X1 ≠ @X2` دارند، دو کلید کاملاً مجزا را قفل می‌کنند و SQL Server هیچ تداخلی بین آن‌ها نمی‌بیند — **فقط** دو درخواست هم‌زمان روی **دقیقاً همان** `@X` یکی پس از دیگری اجرا می‌شوند (دومی تا Commit اولی Block می‌شود، نه Error می‌گیرد).

### چرا این راهکار حتی در نبود Allocation قبلی ایمن است

- رکورد Parent (`BillOfLading`/`CustomsDeclaration`/`ForexVoucher`) **همیشه از قبل وجود دارد** — تخصیص فقط روی یک سند موجود ثبت می‌شود، هرگز روی یک سند که هنوز ساخته نشده. بنابراین مرحلهٔ ۲ (قفل Parent) **همیشه چیزی برای قفل‌کردن پیدا می‌کند**، برخلاف قفل روی ردیف‌های Active که وقتی صفر باشند عملاً هیچ قفلی تولید نمی‌کنند (Phantom Row — دقیقاً نقص TOCTOU شناسایی‌شدهٔ شما).
- چون مرحلهٔ ۳ (خوانش Active) **داخل همان تراکنشی** انجام می‌شود که Parent در آن قفل است، و هر تراکنش رقیب هم باید **اول** همان Parent را قفل کند تا بتواند ردیف تخصیص جدیدی برای همان Parent درج/اصلاح کند، نتیجه این است: در لحظهٔ محاسبهٔ SUM در هر تراکنش، **هیچ تراکنش دیگری نمی‌تواند هم‌زمان در حال درج/اصلاح یک ردیف تخصیص برای همان Parent باشد** — چه صفر ردیف Active وجود داشته باشد، چه بیشتر. این دقیقاً تضمینی است که روی ردیف‌های Active (که ممکن است نباشند) قابل گرفتن نبود.

### کفایت Unique Index

Filtered Unique Index موجود (`(ParentId1, ParentId2) WHERE Status='Active'`) **به‌تنهایی کافی نیست** و این سند صریحاً همین را می‌گوید (حوزهٔ ۴ بند ۵: «نه.»، تکرارشده در حوزه‌های ۵ و ۷) — آن فقط از دو ردیف Active برای همان جفت جلوگیری می‌کند، نه از عبور SUM روی کل Parent از ظرفیت؛ این دو محافظت **مکمل همدیگرند**، نه جایگزین هم: Index برای یکتایی جفت، قفل Parent برای کفایت مجموع.

**نتیجه: Verified.** شواهد: `docs/TRADE_DESIGN_V3_2.md` حوزهٔ ۴ («کنترل مجموع تراکنشی و تحت هم‌زمانی»، بندهای ۱ تا ۶ و «دقت فنی»)، با ارجاع عینی در حوزه‌های ۵ و ۷.

---

## ۳. Vessel Merge — شکاف واقعی پیدا و رفع شد

| محور درخواستی | وضعیت در نسخهٔ قبل از این دور | رفع این دور |
|---|---|---|
| تعیین Canonical Vessel | کامل: `CanonicalVesselId = COALESCE(MergedIntoVesselId, Id)` | بدون تغییر — کافی بود |
| جلوگیری از Self-Merge | کامل: `CHECK (SourceVesselId <> TargetVesselId)` در دیتابیس | بدون تغییر — کافی بود |
| جلوگیری از Circular Merge | کامل: قاعدهٔ «مسطح‌سازی در لحظهٔ اجرا» — `Target` باید خودش Canonical باشد، اعتبارسنجی در Review/Execute، بدون Recursive Query | بدون تغییر — کافی بود |
| کنترل درخواست‌های هم‌زمان | کامل: Filtered Unique Index روی `SourceVesselId WHERE Status IN ('Proposed','Reviewed')` | بدون تغییر — کافی بود |
| نگهداری تاریخچه | کامل: ردیف‌های `VesselMergeRequest` غیرقابل‌حذف + `VesselAuditEvent` به‌ازای هر اجرا | بدون تغییر — کافی بود |
| **بازیابی یا اصلاح Merge اشتباه** | **شکاف واقعی — هیچ مسیری تعریف نشده بود** | **رفع شد این دور:** عملیات جبرانی `MergeReversed` (نه ویرایش/حذف ردیف `Executed`): `SourceVesselId.MergedIntoVesselId` به `NULL` برمی‌گردد + `VesselAuditEvent` جدید با Maker-Checker مستقل (دو کاربر متفاوت از خودِ Reversal)، ارجاع به درخواست اصلی، دلیل اجباری |

### محدودیت صریح باقی‌مانده (نه یک نقص، یک محدودیت مستندشده)

ارجاعات `Voyage.VesselId` که بین زمان Merge و زمان Reversal با `CanonicalVesselId` قدیم (یعنی `TargetVesselId`) محاسبه/گزارش شده‌اند، با Reversal **گذشته‌نگر اصلاح نمی‌شوند** — طراحی کامل این سناریوی نادر (بازسازی گزارش‌های تاریخی بین دو Merge) پیچیدگی غیرضروری برای این فاز است و فقط به‌عنوان یک محدودیت مستند باقی می‌ماند، نه یک مانع پیاده‌سازی. این محدودیت باید به کاربر در لحظهٔ Reversal نمایش داده شود (یک پیام هشدار UI، نه یک قید دیتابیسی).

**نتیجه: Verified with limitation.** پنج محور از شش محور از ابتدا کامل بودند؛ محور ششم (بازیابی/اصلاح) یک شکاف واقعی بود که در همین دور رفع شد، با یک محدودیت صریح و مستند (عدم اصلاح گذشته‌نگر گزارش‌های میانی) که عمداً باز می‌ماند چون رفع کاملش هزینهٔ پیچیدگی نامتناسب با نیاز فعلی دارد. شواهد: `docs/TRADE_DESIGN_V3_2.md` حوزهٔ ۳ (بخش `VesselMergeRequest`، بند «بازیابی/اصلاح یک Merge اشتباه»، افزوده‌شده در دور قبل).

**به‌روزرسانی (دور بعدی — سناریوی زنجیره‌ای A→B و C→A):** قاعدهٔ «ممنوعیت Reversal در وجود Merge وابسته» اضافه شد — `Reversal` یک درخواست اگر درخواست `Executed` دیگری با همان `TargetVesselId` پیشنهادی وجود داشته باشد، صریحاً مسدود می‌شود؛ این دقیقاً همان محدودیت «گذشته‌نگر اصلاح نمی‌شود» بالا را به یک قاعدهٔ قابل‌اجرا و قابل‌بررسی (یک کوئری ساده، بدون Recursive Query) تبدیل می‌کند، نه چیزی جدا از آن. جزئیات کامل در `docs/TRADE_DESIGN_FINAL_GAPS.md`.

---

## ۴. Delete Behavior — جدول موجودیت‌های مهم

قاعدهٔ یکپارچهٔ سراسری (`docs/TRADE_DESIGN_V3_2.md` قواعد مشترک): **همهٔ FKهای این ماژول پیش‌فرض `Restrict`؛ حذف فیزیکی هیچ‌کجای این ماژول پیش‌بینی نشده؛ «لغو» همیشه با فیلد وضعیت، نه `DELETE`.** جدول زیر این قاعده را روی موجودیت‌های کلیدی هر حوزه تأیید می‌کند (نه قاعدهٔ جدید، اثبات اعمال یکنواخت آن):

| موجودیت | Delete فیزیکی؟ | جایگزین (فیلد وضعیت) | Audit باقی می‌ماند؟ |
|---|---|---|---|
| `ImportCase` | خیر | `Closed`/`Cancelled` | `CaseEvent` |
| `OrderRegistration` | خیر | `Closed`/`Cancelled`؛ اصلاح → `OrderRegistrationAmendment` جدید | `OrderRegistrationAmendment` (تاریخچه‌پذیر) |
| `Voyage` | خیر | گردش‌کار وضعیت خودش (`Completed` و غیره)؛ انتقال مالکیت = رویداد ممیزی‌شده، نه حذف/بازسازی | رویداد ممیزی انتقال مالکیت |
| `Vessel` | خیر (هرگز) | `MergedIntoVesselId` (ادغام) به‌جای حذف؛ `MergeReversed` برای اصلاح | `VesselAuditEvent` |
| `VesselChangeRequest`/`VesselMergeRequest` | خیر | `Rejected`/`Executed` نهایی و تغییرناپذیر | `VesselAuditEvent` |
| `BillOfLadingOrderAllocation`/`CustomsDeclarationBillAllocation`/`ForexVoucherBillCoverage` | خیر (هرگز) | `Superseded`/`Cancelled` + `SupersedesAllocationId` | خودِ ردیف‌های تاریخی (غیرقابل‌حذف) |
| `Permit` | خیر | گردش‌کار `Rejected`/`Expired` | `PermitApproval` |
| `ForexCommitment`/`ForexVoucher` | خیر | گردش‌کار وضعیت؛ حواله با `ForexTransferReversal` اصلاح می‌شود، نه حذف | رویدادهای `CommitmentEvent`/`ForexTransferReversal` |
| `Facility`/`Guarantee`/`Cheque` | خیر | `Defaulted`/`Released`/`Called`/`Bounced`/`Replaced` | `GuaranteeEvent`/`ChequeEvent` |
| `TradeDocument` | خیر | `Rejected`/`Expired`/`Archived` | تغییر `CustodianPartyId` ممیزی‌شده |
| `CostItem`/`CostAllocation` | خیر | `Status=Cancelled`/`Superseded` (افزوده دور بعدی — `TRADE_DESIGN_FINAL_GAPS.md`)؛ اصلاح = ردیف جدید `Active` + قبلی `Superseded` | `TradeEntity` پایه (Created/Updated By/At) + دو ردیف Active/Superseded برای قبل/بعد |

### شرایط اعمال Restrict

`Restrict` فقط وقتی مانع می‌شود که یک **درخواست `DELETE`** واقعی روی رکوردی با فرزند فعال صادر شود — و چون کل طراحی این ماژول اصلاً هیچ مسیر کاربری/Use Case برای `DELETE` روی این موجودیت‌ها ندارد (همهٔ «لغو»ها `UPDATE` فیلد وضعیت هستند)، `Restrict` در عمل هرگز در مسیر عادی اصلاح/لغو/جایگزینی قانونی اسناد فعال نمی‌شود؛ تنها نقشش جلوگیری از یک حذف **تصادفی/نادرست** (مثلاً یک اسکریپت نگهداری دستی اشتباه) است، نه مانعی برای گردش‌کار طراحی‌شده.

**به‌روزرسانی (دور بعدی — تثبیت سه‌لایهٔ کنترل DELETE):** (۱) نبود هیچ Use Case حذف، (۲) `Restrict` روی FK، (۳) بازبینی `ChangeTracker` در `TradeDbContext.SaveChangesAsync` که هر تلاش برای `EntityState.Deleted` روی یک `TradeEntity` را با Exception رد می‌کند (توصیف‌شده برای فاز پیاده‌سازی، کدی در این دور نوشته نشد). تفکیک صریح شد: `IsDeleted` که از پیش در `TradeEntity` Merged وجود دارد فقط برای حذف رکورد **بدون اثر کسب‌وکاری قبلی** است (مثل الگوی فعلی‌اش برای `PartyRoleAssignment`)، نه برای لغو کسب‌وکاری (که همیشه با فیلد وضعیت دامنه‌ای است و در گزارش/تاریخچه باقی می‌ماند). جزئیات کامل در `docs/TRADE_DESIGN_FINAL_GAPS.md`.

**نتیجه: Verified.** شواهد: `docs/TRADE_DESIGN_V3_2.md` قواعد مشترک (بند Delete Behavior) + گردش‌کار وضعیت هر حوزه (۱ تا ۱۰) + کد Merged موجود `Modules/Trade/Trade.Domain/Common/TradeEntity.cs`.

---

## ۵. CostAllocation Scope — فهرست دقیق جداول فاز اول

| جدول/موجودیت | فاز اول؟ | وابسته به تصمیم مالی؟ |
|---|---|---|
| `CostType` | بله — ساختار کامل | فهرست دقیق انواع (Seed)، نه Schema |
| `CostItem` | بله — ساختار کامل | خیر |
| `CostAllocation` | بله — ساختار کامل | مبنای پیش‌فرض تسهیم (OD-018) فقط مقدار Application، نه Schema |
| `CostAllocationToOrderRegistrationLine` | بله — ساختار کامل، FK غیر-Nullable | خیر |
| `CostAllocationToImportCase` | بله — ساختار کامل، FK غیر-Nullable | خیر |
| `CostAllocationToVoyageShare` | بله — ساختار کامل، FK غیر-Nullable | خیر |
| `CostAllocationToBillAllocation` | بله — ساختار کامل، FK غیر-Nullable | خیر |

هیچ جدول این حوزه به فاز دوم منتقل نمی‌شود؛ عبارت مبهم «سه یا چهار جدول» حذف و با فهرست قطعی ۴ جدول هدف + ۳ موجودیت پایه جایگزین شد.

**نتیجه: Verified.** شواهد: `docs/TRADE_DESIGN_V3_2.md` حوزهٔ ۱۰ («جدول دقیق فاز/وابستگی»، افزوده‌شده در همین دور).

---

## ۶. کنترل نهایی اسناد — تطبیق سه سند طراحی + گزارش نهایی

| محور | نتیجهٔ تطبیق |
|---|---|
| نام Entityها و FKها | یکدست در هر ۴ سند؛ ارجاعات تاریخی به نام‌های قبلی (`CostAllocationToCompanyShare`، `PermitRequirement`) فقط در بندهای رفع‌تناقض/تاریخچه ظاهر می‌شوند، هیچ‌کجا به‌عنوان نام فعلی ادعا نشده‌اند (بررسی `grep` مستقیم روی هر ۴ فایل) |
| Cardinality | یکدست؛ سه جدول گروه C دوطرفه در هر سه سند با همان مدل Active/Superseded/Cancelled توصیف شده‌اند |
| Tenant Scope | یکدست؛ قرارداد پنج‌بندی گروه C در قواعد مشترک با جزئیات هر حوزه (۳، ۴، ۵، ۷) بدون تناقض |
| وضعیت‌ها (Status) | یکدست؛ گردش‌کار هر حوزه در `TRADE_DESIGN_V3_2.md` با جدول‌های خلاصهٔ `TRADE_DESIGN_FINAL_REVIEW.md` هم‌خوان |
| Delete Behavior | یکدست؛ قاعدهٔ سراسری `Restrict`/بدون حذف فیزیکی در قواعد مشترک، تأییدشده در بخش ۴ همین سند روی موجودیت‌های کلیدی |
| تصمیم‌های باز | یکدست؛ ۲۲ مورد `OD-001` تا `OD-022` بدون شناسهٔ تکراری (بررسی `grep -oE` مستقیم)؛ هیچ موردی بدون مرجع واقعی `Approved` نشده |
| محدودهٔ فاز اول/دوم | یکدست؛ R01–R12 و تمام جداول حوزهٔ ۱۰ = فاز اول؛ R13–R25 = فاز دوم (مشخصات اجرایی هنوز ناقص، صریحاً اعلام‌شده) |

**بررسی پیوندها/ارجاعات داخلی:** هر ۴ فایل (`TRADE_DESIGN_V3_2.md`، `TRADE_DESIGN_RECONCILIATION.md`، `TRADE_OPEN_DECISIONS.md`، `TRADE_DESIGN_FINAL_REVIEW.md`) با نام دقیق و موجود در `docs/` به یکدیگر ارجاع می‌دهند (بررسی مستقیم با `grep` + `ls`)؛ هیچ لینک شکسته پیدا نشد.

**نتیجه: Verified.**

---

## ۷. خروجی Git (واقعی، اجراشده همین لحظه)

خروجی واقعی (اجراشده همین لحظه، بدون ویرایش):

```
$ git status --short --branch
## docs/trade-handoff-status-reconciliation...origin/main [ahead 2]
 M docs/TRADE_DESIGN_RECONCILIATION.md
 M docs/TRADE_DESIGN_V3_2.md
 M docs/TRADE_OPEN_DECISIONS.md
?? docs/TRADE_DESIGN_FINAL_REVIEW.md
?? docs/TRADE_DESIGN_FINAL_VERIFICATION.md

$ git diff --stat
 docs/TRADE_DESIGN_RECONCILIATION.md |  33 ++++-
 docs/TRADE_DESIGN_V3_2.md           | 138 ++++++++++++++----
 docs/TRADE_OPEN_DECISIONS.md        | 279 ++++++++++++++++++++++++++----------
 3 files changed, 343 insertions(+), 107 deletions(-)

$ git diff --check
(بدون خروجی — تمیز، بدون مشکل whitespace)

$ git diff --name-status
M	docs/TRADE_DESIGN_RECONCILIATION.md
M	docs/TRADE_DESIGN_V3_2.md
M	docs/TRADE_OPEN_DECISIONS.md
```

**توضیح `docs/TRADE_OPEN_DECISIONS.md`:** این فایل در همین دور (راستی‌آزمایی) ویرایش نشد — هیچ تصمیم کسب‌وکاری جدیدی ثبت نشد و هر دو شکاف رفع‌شده (بخش ۱ و ۳ بالا) صرفاً تکمیل فنی بودند. تغییرات `M` آن از دور قبل (بازبینی نهایی) باقی مانده و هنوز commit نشده‌اند؛ `git diff --stat` بالا تغییرات تجمعی نسبت به آخرین commit (`1121a2d1`) را نشان می‌دهد، نه فقط تغییرات همین دور.

**فایل‌های Untracked:**
- `docs/TRADE_DESIGN_FINAL_REVIEW.md` (ساخته‌شده در دور قبل، هنوز commit نشده)
- `docs/TRADE_DESIGN_FINAL_VERIFICATION.md` (همین سند، تازه ساخته‌شده)

**Commit/Push:** هیچ Commit یا Push در این دور انجام نشد؛ طبق دستور صریح شما منتظر درخواست شما می‌مانم.

---

## نتیجهٔ نهایی این دور راستی‌آزمایی

هر ۵ موضوع اصلی **Verified** یا **Verified with limitation** شدند؛ هیچ موردی **Not verified** یا **Requires business decision جدید** نماند (تصمیم‌های کسب‌وکاری قبلاً در `TRADE_OPEN_DECISIONS.md` ثبت‌شده‌اند و این دور چیزی به آن‌ها اضافه نکرد). دو شکاف واقعی (تفکیک خواندن/نوشتن گروه C، بازیابی Merge اشتباه) پیدا و با الگوهای از‌پیش‌موجود (نه حدس) رفع شدند.

**طراحی فاز اول از نظر مستندات برای تأیید نهایی آماده است**، با همان محدودیت قبلی بدون تغییر: تصمیم‌های کسب‌وکاری باز (`OD-007`، `OD-013` تا `OD-016`، `OD-018`، `OD-019` و غیره) هنوز منتظر تأیید شما هستند و مانع شروع **اسکلت** فاز اول نیستند، اما مانع تکمیل واقعی حوزه‌های Forex/Facility/Permit/CostAllocation با مقدار واقعی‌اند — دقیقاً همان نتیجهٔ `READY WITH BUSINESS DECISIONS PENDING` که در `TRADE_DESIGN_FINAL_REVIEW.md` اعلام شد، اکنون با شواهد مستقیم این دور **تأیید مجدد و مستقل** شد.

فعلاً فقط مستندات تکمیل شد؛ منتظر تأیید صریح شما برای شروع پیاده‌سازی می‌مانم.
