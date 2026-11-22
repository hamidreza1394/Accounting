# طراحی قرارداد دامنهٔ Proforma — مرحلهٔ B مأموریت تکمیل زیرسیستم Trade

- **Issue:** UNNUMBERED (ادامهٔ Issue #397؛ مرحلهٔ B پس از مرحلهٔ A: PR #756، D-116)
- **Agent/مسئول:** Claude (Lead)، فقط Trade؛ Vendor/GLM دست‌نخورده
- **تاریخ:** 2026-11-22
- **Commit مبنا:** `37f8f913` (main پس از Merge #756)
- **وضعیت سند:** مستندات طراحی فقط‌خواندنی. هیچ کد اجرایی، Entity، جدول، DbSet، Migration، Schema، API یا UI تغییر نکرده است. گیت‌های S3-0 بسته‌اند.
- **قاعدهٔ برچسب‌ها (D-030):** `[Accepted]` = تصمیم مصوب موجود در ADR؛ `[طراحی-پیشنهادی]` = پیشنهاد این سند و **غیرمصوب**؛ `[NeedsApproval]` = نیازمند تصمیم مالک؛ `[Blocked]` = وابسته به گیت یا تصمیم باز. هیچ `Proposed` یا `Open` به `Accepted` تبدیل نشده است. نام‌های Entity/جدول در این سند فقط پیشنهاد طراحی‌اند و مجوز ساخت Schema نیستند.

---

## ۱. وضعیت فعلی پیاده‌سازی (B-1، با شواهد کد در `37f8f913`)

### ۱.۱ آنچه واقعاً در کد هست
| بخش | شاهد | نتیجه |
|---|---|---|
| Entityهای پایگاه‌داده Trade | `TradeDbContext`: فقط `Party`، `PartyRoleAssignment`، `PartyBankAccount`، `Product`، `ProductIdentifier`؛ یک Migration (`InitialTradeMasterData`) | هیچ جدول Proforma، ImportCase یا OrderRegistration وجود ندارد. توجه: `PartyBankAccount` و `ProductIdentifier` با `ExcludeFromMigrations()` در دیتابیس جدول ندارند؛ الگوی FK مرکب آن‌ها فقط در مدل EF هست و روی SQL Server سنجیده نشده است |
| Entity دامنهٔ Proforma/Revision/Line/History | جست‌وجوی `proforma` در `*.cs`: فقط `PaymentPlan`، `PaymentPlanCalculator`، یک سند در `TradeDocumentNumber`، توضیح `PaymentMethod` و کاتالوگ مجوز | **هیچ Entity دامنه‌ای وجود ندارد** (`NotImplemented`) |
| `PaymentPlan` / `PaymentPlanCalculator` | `Trade.Application/Proformas/`، تست‌های `TradePaymentPlan*Tests` | تنها قابلیت واقعی Proforma؛ محاسبهٔ خالص و بدون persistence؛ **قابل استفادهٔ مجدد بدون بازنویسی** |
| `TradeDocumentNumber` | `Trade.Domain/Common` (`TryCreate`، `Display`، `Normalized`، سقف ۶۴) | قابل استفاده برای شمارهٔ Proforma؛ قواعد دقیق Unicode/case در S3-0 تثبیت می‌شود |
| `TradeQuantitySnapshot`/واحدها/`TradeRoundingPolicy` | `Trade.Domain/Units` | قابل استفاده برای تناژ و واحد خطوط |
| `Product.HsCode` | `Product.cs`، حداکثر ۱۶ نویسه، اختیاری | HS Code فعلاً ویژگی **کالا** است، نه خط پیش‌فاکتور |
| مجوزها | `TradePermissionCatalog`: `trade.form.proforma.*` با update/approve/delete | کاتالوگ آماده است؛ بدون Endpoint |
| Entitlement | `DenyAllTradeModuleEntitlement` | Fail-Closed؛ دست‌نخورده |

### ۱.۲ کدام موارد فقط مستنداتی‌اند
Proforma، ProformaRevision، ProformaLine، `ProformaPaymentStage`، `ProformaHistoryEvent`، ImportCase، OrderRegistration، Amendment و همهٔ روابط آن‌ها فقط در `TRADE_DESIGN_V3_2.md` §1، D-080، D-086، D-104 و D-116 توصیف شده‌اند.

### ۱.۳ ناسازگاری‌های واقعی یافت‌شده
| # | یافته | شاهد | اثر |
|---|---|---|---|
| F-1 | `TradeEntity` فقط `TenantId` دارد و آن در کد برابر `Company.Id` است؛ `CompanyId` جدا وجود ندارد | `TradeEntity.cs`، `TradeDbContext.cs` | قاعدهٔ «هر Entity جدید هر دو را جدا دارد» (D-087/D-105) با پایهٔ فعلی قابل اجرا نیست. افزودن ستون = Schema/Migration (`Blocked`، گیت G0، D-090 `Proposed`) |
| F-2 | `TradeAccessContext(UserId, TenantId, FiscalPeriodId, AllowsWrites)` و `TradePeriodScope` شناسهٔ دورهٔ مالی را الزامی می‌دانند | `TradeAccessContext.cs`، `TradePeriodScope.cs` | با «استقلال ImportCase/Proforma/OrderRegistration از FiscalPeriod» (D-086 بند ۷) ناسازگار است؛ سرویس‌های Proforma نباید به دورهٔ مالی وابسته شوند. زنجیرهٔ کنترل Entitlement در D-063 بند ۵ نیز «WorkContext و دورهٔ معتبر» را شامل می‌شود و در حل این مانع باید بازبینی شود |
| F-3 | V3.2 §1 هنوز Proforma (1)→(0..n) OrderRegistration و `OrderRegistrationLine` را شرح می‌دهد | `TRADE_DESIGN_V3_2.md` ۵۵–۸۲ | رابطهٔ چندگانه با D-104 منسوخ است (یادداشت دارد)؛ `OrderRegistrationLine` هنوز در هیچ ADR مصوب یا رد نشده. ردیف «شرکت/دوره: `TenantId + FiscalPeriodId`» در V3.2 §1 نیز با D-086 بند ۷ منسوخ است |
| F-4 | D-080 بندهای ۱ تا ۷ (حداکثر یک Proforma جاری با قید یکتای DB، `Superseded`، زنجیرهٔ `SupersedesProformaId`) را D-086 در تعارض جایگزین کرده است؛ D-086 بند ۳.۱ سقف را قابل تنظیم می‌داند | D-086 «مرجع جایگزینی»، بند ۳.۱ | روشن نیست کدام بخش از مدل Replace/`Superseded` پس از D-086 هنوز معتبر است (ابهام N-1) |
| F-5 | `ProformaRevision` در V3.2 نسخهٔ سند است و D-116 تمدید اعتبار را از آن مستثنا می‌کند؛ مدل V3.2 محل `ValidUntil` را مشخص نکرده | V3.2، D-116 | ابهام B-4 (محل تاریخ اعتبار) |

### ۱.۴ قابل استفادهٔ مجدد بدون بازنویسی
`TradeEntity` (الگوی Id/Audit/RowVersion/SoftDelete؛ برای Revision/History با هشدار H-2 در بخش ۲.۵)، `PaymentPlanCalculator` (بدون تغییر)، `TradeDocumentNumber`، `TradeQuantitySnapshot`، الگوی `Outcome` سرویس‌های Party/Product، `TradeAccessGate`، `TradeEntityConfiguration`. هیچ کلاس تکراری نباید ساخته شود.

---

## ۲. مدل مفهومی پیشنهادی `[طراحی-پیشنهادی]` (B-2)

اصل: کمترین Entity ممکن؛ Revision تنها محل نگهداری «محتوای سند»؛ سرتیتر Proforma فقط هویت و وضعیت.

```
ImportCase (خارج از Aggregate؛ فقط ImportCaseId)
   └─1──0..n─ Proforma                         ← ریشهٔ Aggregate
                 ├─1──1..n─ ProformaRevision   ← تغییرناپذیر؛ RevisionNumber = 1، 2، …
                 │              ├─1──1..n─ ProformaLine
                 │              └─1──0..12─ ProformaPaymentStage
                 └─1──0..n─ ProformaHistoryEvent   ← append-only
Proforma ─1──0..1─ OrderRegistration (D-104؛ Aggregate جدا)
```

### ۲.۱ ادغام ProformaItem و ProformaRevisionItem
پیشنهاد: **فقط `ProformaLine` زیر Revision**؛ جدول جدا برای «اقلام سرتیتر» ساخته نمی‌شود. دلیل: دو جدول قلم دادهٔ تکراری و محل انحراف بین «نسخهٔ فعلی» و «نسخهٔ ثبت‌شده» می‌سازند. «اقلام فعلی» = خطوط آخرین Revision. نسخهٔ اولیه = Revision شمارهٔ ۱. هزینه: خواندن اقلام فعلی یک Join به آخرین Revision است (با شاخص ساده، ناچیز).

### ۲.۲ `ProformaHistoryEvent` به‌جای جدول اختصاصی تمدید
پیشنهاد: **یک جدول رویداد** با `EventType` و **ستون‌های نوع‌دار** (`PreviousValidUntil`، `NewValidUntil`، `Reason` و …)، نه Payload آزاد/JSON؛ D-086 از event store عمومی منع می‌کند و فعلاً فقط یک نوع رویداد شناخته‌شده است (YAGNI). جایگزین (جدول `ProformaValidityExtensionHistory` جدا): تایپ‌محورتر ولی با هر رویداد جدید جدول تازه می‌خواهد. هر دو با D-116 سازگارند؛ انتخاب تصمیم فنی است و به‌خاطر اثر Schema `NeedsApproval` است.

### ۲.۳ `ProformaLine` و HS Code
`[Accepted]` (D-116 بند ۱): چند کالا با HS Codeهای متفاوت. `[طراحی-پیشنهادی]`: هر خط `ProductId` و **کپی لحظه‌ای** `ProductCode`، `ProductName`، `HsCode` را نگه دارد تا ویرایش بعدی Product نسخهٔ ثبت‌شده را تغییر ندهد (الزام تغییرناپذیری Revision). شناسهٔ Product بدون FK به Inventory؛ Product در Trade می‌ماند (D-116 بند ۴). سقف تعداد خطوط از پروفایل فعالیت (D-086) می‌آید.

### ۲.۴ `ProformaPaymentStage`
از `PaymentPlan` فعلی تغذیه می‌شود؛ `PaymentPlanCalculator` و الگوریتم تخصیص تغییر نمی‌کنند. محدودیت‌های موجود (حداکثر ۱۲ مرحله، دقت درصد، سقف مبلغ) به‌همان شکل اعمال می‌شود. روش تخصیص و MinorUnits باز است (ابهام A-7، A-8).

### ۲.۵ شناسه‌ها و مالکیت
هر Entity: `Id`، `TenantId`، `CompanyId` (هدف)، Audit و `RowVersion` در ریشه (الگوی `TradeEntity`). فرزندان با FK مرکب شامل مالک ریشه ارجاع می‌دهند (الگوی `ProductIdentifier`). جزئیات و مانع F-1 در بخش ۸.

**هشدار H-2:** `TradeEntity` متدهای عمومی `RecordUpdate` و `SoftDelete` دارد (`TradeEntity.cs:45,58`). برای `ProformaRevision`، `ProformaLine`، `ProformaPaymentStage` و `ProformaHistoryEvent` که باید تغییرناپذیر/append-only باشند، ارث‌بری مستقیم از آن با ادعای «بدون mutator عمومی» (بخش ۵.۳) در تضاد است. در مرحلهٔ C باید یکی انتخاب شود: پایهٔ append-only جدا بدون این دو متد، یا ارث‌بری همراه Guard صریح که هر دو را رد کند. V3.2 (`TRADE_DESIGN_V3_2.md` بند ۲) `IsDeleted` را فقط برای رکوردی می‌داند که هنوز اثر کسب‌وکاری نداشته و Revision/History را صراحتاً استثنا نکرده است. `[طراحی-پیشنهادی]` فقط ریشهٔ Proforma می‌تواند `IsDeleted` داشته باشد و آن هم فقط پیش از هر اثر.

---

## ۳. روابط Entityها (جدول خلاصه) `[طراحی-پیشنهادی]`

| رابطه | کاردینالیتی | مبنا | قید پیشنهادی |
|---|---|---|---|
| ImportCase → Proforma | 1 → 0..n (سقف از پروفایل) | D-086 بند ۳.۱ | `Proforma.ImportCaseId` تغییرناپذیر پس از ثبت (V3.2) |
| Proforma → ProformaRevision | 1 → 1..n | D-086 بند ۳.۵، D-116 | یکتا `(ProformaId, RevisionNumber)`؛ برای FK مرکب یک کلید جایگزین واحد شامل مالک ریشه (پیشنهاد: `(TenantId, CompanyId, ProformaId, Id)`) |
| Revision → ProformaLine | 1 → 1..n | D-116 بند ۱ | یکتا `(RevisionId, LineNumber)` |
| Revision → ProformaPaymentStage | 1 → 0..12 | کد فعلی | یکتا `(RevisionId, StageNumber)` |
| Proforma → ProformaHistoryEvent | 1 → 0..n | D-116 بند ۲ | فقط INSERT |
| Proforma ↔ OrderRegistration | 1 ↔ 0..1 | D-104 `[Accepted]` | یکتا روی `OrderRegistration.ProformaId` |
| OrderRegistration → Revision | n → 1 | V3.2 (پیشنهادی) | FK مرکب شامل `TenantId` و `CompanyId` و `ProformaId` به همان کلید جایگزین Revision؛ `TradeEntityConfiguration` از قبل کلید جایگزین `(TenantId, Id)` دارد، پس باید از چند کلید موازی پرهیز و کلید واحد انتخاب شود |

`OrderRegistration.ImportCaseId` نباید ساخته شود (V3.2: از زنجیرهٔ `ProformaId` به دست می‌آید).

---

## ۴. چرخهٔ عمر Proforma (B-6 و رفتار در وضعیت‌ها)

وضعیت‌های V3.2: `Draft → IssuedToBuyer → UnderRevision → Accepted → Expired/Cancelled` (D-080 `Superseded` را افزوده بود؛ وضعیت آن در N-1 باز است).

| وضعیت | `[Accepted]`/باز | رفتار پیشنهادی |
|---|---|---|
| `Draft` | پیشنهاد D-080 (غیرمصوب): «جاری» حساب می‌شود؛ پس از D-086 باید تأیید شود (N-1) | ویرایش آزاد؟ پیشنهاد: ویرایش روی Revision ۱ پیش از صدور بدون ساخت Revision جدید؛ **این فرض باز است** (ابهام A-3) و با قید «Revision تغییرناپذیر است» (بخش ۵.۳) تعارض دارد: یا Draft تا صدور هنوز Revision حساب نمی‌شود، یا هر ویرایش Revision می‌سازد |
| `IssuedToBuyer` | — | تغییر شرایط تجاری ⇒ Revision جدید؛ تمدید ⇒ رویداد |
| `UnderRevision` | — | همان Proforma؛ Revision تازه در دست تهیه (Draft داخلی) |
| `Accepted` | — | مبنای ثبت‌سفارش |
| `Expired` | **P-4 باز** | از تاریخ اعتبار **مشتق می‌شود**؛ ذخیرهٔ وضعیت خودکار فقط پس از تصمیم |
| `Cancelled` | پایانی؛ پیشنهاد D-080 (غیرمصوب) | با دلیل؛ بدون حذف فیزیکی |

`Expired` بهتر است «مشتق از تاریخ اعتبار و زمان سرور» تعریف شود، نه وضعیت ذخیره‌شده، تا تمدید اعتبار بدون نوشتن وضعیت دیگری Proforma را زنده کند. اثر انقضا بر جاری‌بودن و ایجاد OrderRegistration **تصمیم‌نشده** است (P-4).

---

## ۵. چرخهٔ Revision (B-3)

1. **نسخهٔ اولیه:** ایجاد Proforma ⇒ Revision شمارهٔ ۱ با خطوط و مراحل پرداخت.
2. **نسخهٔ جدید:** هر تغییر «شرایط تجاری» ⇒ Revision با شمارهٔ بعدی؛ کپی صریح خطوط و مراحل با تغییر لازم؛ نسخهٔ قبلی دست نمی‌خورد `[Accepted]` (D-116 بند ۳).
3. **تغییرناپذیری:** Revisionها (و خطوط و مراحل آن‌ها) فقط INSERT می‌شوند. اجرای قید (پیشنهاد، چون Trigger مطلوب مخزن نیست): (الف) دامنه هیچ setter/mutator عمومی ندارد؛ (ب) پورت ذخیره‌سازی فقط `Add` دارد؛ (ج) ردیاب تغییرات EF هنگام `SaveChanges` هر `Modified`/`Deleted` روی این جدول‌ها را رد می‌کند؛ (د) تست Integration روی SQL Server واقعی. **صریح:** این‌ها دیتابیس را در برابر `UPDATE` مستقیم SQL محافظت نمی‌کنند؛ نیاز به قید سطح DB (مجوز/Trigger) تصمیم جدا است.
   **نسبت به V3.2:** `TRADE_DESIGN_V3_2.md` (بند ۲–۴، حدود خطوط ۴۳ تا ۵۰) بررسی `ChangeTracker` برای وضعیت `Deleted`، نبود Trigger در فاز اول و ممنوعیت `ExecuteDelete` را تثبیت کرده و حفاظت سطح DB را «برای این فاز لازم نیست» دانسته است. این سند آن‌ها را **تکرار و گسترش پیشنهادی** می‌دهد: وضعیت `Modified` و `ExecuteUpdate` را هم **فقط برای جدول‌های Revision/Line/Stage/History** ممنوع می‌کند (V3.2 خود برای Cascade کشتی از `ExecuteUpdate` استفاده می‌کند، پس ممنوعیت به همین جدول‌ها محدود است)، و N-6 بازگشایی صریح پرسش حفاظت سطح DB است، نه تکرار.
4. **همزمانی:** یکتایی `(ProformaId, RevisionNumber)` رقابت دو Revision هم‌شماره را در SQL Server می‌بندد. `RowVersion` ریشه فقط وقتی کمک می‌کند که ریشه در همان `SaveChanges` واقعاً تغییر کند (درج صرف فرزند آن را بررسی نمی‌کند)؛ پس باید ریشه صراحتاً به‌روزرسانی شود.
5. **حفظ تاریخچه:** همهٔ Revisionها نگه‌داری می‌شوند؛ حذف فیزیکی ممنوع.
6. **فهرست فیلدهای «شرایط تجاری»:** `NeedsApproval`. D-086 بند ۳.۷ فهرست «فیلدهای نسخهٔ اول» را داده ولی نگفته تغییر کدام‌ها Revision می‌سازد. این سند هیچ فهرستی را قانون اعلام نمی‌کند (ابهام A-3).
7. **تغییر فروشنده:** تابع پروفایل (D-086 بند ۳.۶). محل نگهداری فروشنده (سرتیتر یا Revision) تصمیم‌نشده است (N-9)؛ V3.2 آن را روی سرتیتر گذاشته ولی D-080 بند ۳ (غیرمصوب) تغییر فروشنده را Proforma جدید می‌دانست. این انتخاب تعیین می‌کند تغییر فروشنده Revision بسازد یا Replace؛ اثر بر ثبت‌سفارش زنده تحت P-5.

---

## ۶. چرخهٔ تمدید اعتبار (B-4) `[Accepted]` برای اصل، باز برای جزئیات

### ۶.۱ آنچه مصوب است (D-116 بند ۲، مأموریت ۴.۳)
تمدید صرف تاریخ اعتبار ⇒ فقط یک رویداد append-only؛ Revision جدید نمی‌سازد؛ رویداد شامل تاریخ قبلی، تاریخ جدید، دلیل، کاربر و زمان ثبت؛ `SecurityAuditEvent` جایگزین آن نیست.

### ۶.۲ جریان پیشنهادی `[طراحی-پیشنهادی]`
1. کاربر مجاز و `AllowsWrites`؛ Company از context معتبر.
2. پیش‌شرط‌ها: Proforma پایانی (`Cancelled`) نباشد؛ `NewValidUntil` معتبر باشد. D-086 بند ۳.۸ می‌گوید «تاریخ اعتبار قابل اصلاح است» و محدود به رو به جلو نیست؛ مجاز بودن کاهش تاریخ و نوع رویداد آن در A-2 باز است.
3. در یک تراکنش: درج `ProformaHistoryEvent(ValidityExtended)`؛ و **اگر** پاسخ A-1 «ویژگی جاری روی سرتیتر» باشد، به‌روزرسانی همان مقدار. این گام پیش از پاسخ A-1 قطعی نیست.
4. رقابت هم‌زمان دو تمدید: ریشهٔ Proforma در هر حال (چه مقدار جاری روی سرتیتر باشد چه نه) باید در همان `SaveChanges` صراحتاً به‌روز شود تا `RowVersion` آن بررسی شود (قاعدهٔ بخش ۵.۴).

### ۶.۳ آنچه نباید قطعی شود
- **محل نگهداری تاریخ اعتبار (A-1):** گزینه‌ها: (الف) ستون روی سرتیتر Proforma + رویداد برای هر تغییر؛ (ب) فقط روی Revision؛ (ج) مقدار اولیه روی Revision و مقدار مؤثر = آخرین رویداد. (ب) با تمدید بدون Revision متناقض است مگر Revision تغییر کند (بر خلاف D-116)، پس عملاً الف یا ج. **پیشنهاد فنی (غیرمصوب): الف.**
- رفتار پس از انقضا، امکان OrderRegistration پس از انقضا، اثر بر جاری‌بودن: P-4.

---

## ۷. ارتباط با OrderRegistration (B-5) `[Accepted]` D-104؛ P-5 باز

- یک Proforma = دقیقاً یک OrderRegistration و برعکس. `[Accepted]`
- پیشنهاد داده: `OrderRegistration.ProformaId` (یکتا) و `BasedOnProformaRevisionId` با FK مرکب با همان کلید جایگزین واحد بخش ۳ (شامل `TenantId` و `CompanyId` و `ProformaId`). `TradeEntityConfiguration` برای همهٔ Entityها کلید `(TenantId, Id)` تعریف می‌کند؛ اینکه Revision آن را ارث ببرد یا پایهٔ جدا (H-2) داشته باشد و کلید پیشنهادی جایگزین آن شود، به مرحلهٔ C سپرده شده است. «دقیقاً یک» در دامنه با `0..1` پیش از ثبت‌سفارش مدل می‌شود؛ ایجاد Proforma بدون OrderRegistration مجاز است (D-104: وجود موقت Proforma باز است).
- OrderRegistration به **Revisionی** که مبنای ثبت بوده متصل می‌ماند. این اصل در V3.2 §1 و D-080 بند ۳ (متن «تصمیم پیشنهادی») آمده و پس‌ازآن D-086 (ADR با وضعیت `Accepted`) در بخش «موارد باز» خود قید موقت ثبت کرده است: «تا تصمیم، پیوند Revision ثبت‌سفارش جابه‌جا نمی‌شود». یعنی جابه‌جایی پیوند فعلاً **ممنوع (قید موقت ثبت‌شده در موارد باز D-086؛ این تبدیل Open به Accepted نیست)** است و اصل کلی آن `[طراحی-پیشنهادی]` است؛ P-5 و A-5 به آن وابسته‌اند.
- پس از دریافت شمارهٔ ثبت‌سفارش، تغییر کالا ممنوع است `[Accepted]` (D-086 بند ۴.۶ و ۲.۵).
- **P-5 باز نگه داشته شد:** Replace یا تغییر شماره/فروشنده وقتی OrderRegistration زنده به Revision قبلی متصل است. هیچ رفتاری انتخاب نشد.
- **Revision پس از ثبت‌سفارش (D-104 باز):** اگر مجاز باشد، پیوند به Revision قدیمی می‌ماند یا نه، تصمیم‌نشده است (ابهام A-5).
- `OrderRegistrationAmendment` و رابطهٔ آن در مرحله‌ای بعد از B طراحی می‌شود؛ این سند فقط نقطهٔ اتصال را تعیین می‌کند.

---

## ۸. قواعد Tenant و Company (B-6)

### ۸.۱ آنچه مصوب است
D-087/D-105: `TenantId` و `CompanyId` مستقل‌اند؛ هر Entity جدید هر دو را دارد؛ هر دو فقط از context معتبر سرور می‌آیند؛ برابری فعلی `TenantId = Company.Id` فقط وضعیت Legacy است. D-090 `Proposed` است.

### ۸.۲ قواعد طراحی
- **ورودی:** هیچ DTO ورودی `TenantId`/`CompanyId` ندارد. دسترسی هر Company از `CompanyAccessService` (عضویت فعال + نقش) و در مدل هدف تعلق Company به Tenant از رابطهٔ معتبر DB.
- **یکتایی شمارهٔ Proforma:** در محدودهٔ Company (D-086 بند ۳.۳). کلید: `(TenantId, CompanyId, شمارهٔ نرمال‌شده)` با فیلتر `IsDeleted = 0`. فروشنده Scope را تغییر نمی‌دهد. تکرار رد می‌شود و `/1` را کاربر وارد می‌کند. این **صراحتاً** با فرض D-080 («تنها بین ردیف‌های جاری و بر مبنای فروشنده») متفاوت است و باید پیش از S3-3 با D-086 هم‌تراز شود (ابهام N-1). **هشدار M-5:** فیلتر `IsDeleted = 0` یعنی شمارهٔ رکورد حذف‌نرم‌شده آزاد می‌شود ولی شمارهٔ `Cancelled` آزاد نمی‌شود؛ D-086 بند ۳.۳ دربارهٔ هیچ‌کدام حکمی ندارد (N-10). فیلتر فقط پیشنهاد است.
- **فرزندان:** FK مرکب شامل `TenantId` و `CompanyId` ریشه تا ارجاع بین‌شرکتی در سطح DB رد شود.
- **ارجاع‌ها:** `ImportCaseId`، `PartyId`، `ProductId` باید در همان Tenant و Company باشند؛ بررسی در Application و FK مرکب.

### ۸.۳ مانع F-1 (`Blocked`)
`TradeEntity` فقط `TenantId` دارد. ساخت Entity با `CompanyId` مستقل و persistence آن یعنی ستون/Migration، که تا بسته‌شدن G0 و تأیید D-090 ممنوع است. گزینه‌ها برای مرحلهٔ C:

| گزینه | مزیت | عیب/اثر |
|---|---|---|
| ۱) دامنهٔ خالص با `CompanyId` صریح و بدون هیچ EF | هیچ Schema لمس نمی‌شود؛ مدل هدف از ابتدا درست است | Entityها تا S3-0 persist نمی‌شوند؛ ممکن است شکل نهایی بعد از S3-0 جزئی تغییر کند |
| ۲) صبر تا بسته‌شدن گیت‌ها | بدون ریسک بازکاری | هیچ پیشرفت قابل‌اجرایی تا آن زمان نیست |
| ۳) مشتق `CompanyId` از `TenantId` موجود | سریع | **ممنوع:** برابری فعلی را الگو می‌کند و D-087/D-105 را نقض می‌کند |

**پیشنهاد فنی (غیرمصوب): گزینهٔ ۱.** تصمیم: مالک (`NeedsApproval`).

### ۸.۴ مانع F-2 (`NeedsApproval`)
`TradeAccessContext`/`TradePeriodScope` دورهٔ مالی را الزامی می‌کنند. سرویس‌های ImportCase/Proforma/OrderRegistration باید بدون FiscalPeriod کار کنند (D-086 بند ۷). تغییر یا افزودن نوع context بدون دوره یک تغییر مجوزدهی است و نیاز به تصمیم و تست امنیتی جدا دارد؛ در این مرحله هیچ تغییری داده نشد. اسناد مالی واقعی آینده تابع دورهٔ مالی خود می‌مانند.

---

## ۹. محدودیت‌های معماری

- Modular Monolith: `Trade.Domain` به `Trade.Application`/`Server`/`Infrastructure` وابسته نمی‌شود؛ دامنهٔ Proforma به `PaymentPlan` (Application) وابسته نشود (مرحلهٔ C مدل مرحله‌ها را در Domain مستقل نگه می‌دارد یا ورودی را بدون وابستگی دریافت می‌کند).
- D-111 `Accepted`: معماری باید توپولوژی‌های Tenant DB، Company DB و Company + FiscalPeriod DB را پشتیبانی کند (بند ۲)، و بین دیتابیس‌های عملیاتی Foreign Key مستقیم یا تراکنش اتمیک توزیع‌شده ممنوع است (بند ۸). هر قید یکتایی و FK پیشنهادی این سند فقط **داخل یک دیتابیس** معتبر است؛ فرض «همهٔ دادهٔ یک Company در یک دیتابیس» از D-111 قابل استنتاج نیست (N-7). ارجاع بین دیتابیس‌ها با شناسه پایدار و اعتبارسنجی سمت سرور است. توپولوژی Tenant + FiscalPeriod طبق D-115 بند ۸ تا تأیید صریح مالک `Pending Owner Decision` است و Resolver آن را fail-closed رد می‌کند؛ D-111 بند ۷ (دادهٔ Company-level نباید بی‌دلیل برای هر دوره تکثیر شود) مستقیماً به N-7 مربوط است. وضعیت پیاده‌سازی D-111 `NotImplemented` است.
- بدون `TradeOperationalPeriod` (D-062)، بدون وابستگی جدید به FiscalPeriod، بدون انتقال Product به Inventory، بدون تغییر Entitlement.
- Repository عمومی/MediatR/Event Bus/abstraction جدید بدون نیاز واقعی ممنوع (`AGENTS.md`)؛ پورت‌های باریک شبیه `IPartyStore`.
- `SecurityAuditEvent` تاریخچهٔ تجاری نیست.
- InMemory/SQLite اثبات قیدهای SQL Server نیست؛ یکتایی، هم‌زمانی و تغییرناپذیری فقط با SQL Server واقعی `Verified` می‌شوند.
- بدون Migration، Backfill، Rename یا تغییر معنای `TenantId`.

---

## ۱۰. تصمیم‌های قطعی (مصوب؛ بدون تغییر در این مرحله)

| تصمیم | مرجع |
|---|---|
| Proforma چندکالایی با HS Codeهای متفاوت؛ سقف از پروفایل | D-116 بند ۱، D-086 بند ۳.۲ |
| تمدید صرف اعتبار = رویداد مستقل، بدون Revision | D-116 بند ۲ |
| تغییر شرایط تجاری = Revision جدید؛ نسخهٔ قبلی تغییرناپذیر | D-116 بند ۳، D-086 بند ۳.۵ |
| Product در Trade می‌ماند | D-116 بند ۴ |
| Proforma ↔ OrderRegistration یک‌به‌یک | D-104 |
| شمارهٔ Proforma در محدودهٔ Company یکتا؛ `/1` فقط ورودی کاربر | D-086 بند ۳.۳–۳.۴ |
| تغییر کالا پس از شمارهٔ ثبت‌سفارش ممنوع | D-086 بند ۴.۶ و ۲.۵ |
| مالکیت Voyage شرکت‌محور | D-063 بند ۱ |
| ImportCase/Proforma/OrderRegistration مستقل از FiscalPeriod؛ `TradeOperationalPeriod` ممنوع | D-086 بند ۷، D-062 |
| `TenantId`/`CompanyId` مستقل؛ Company-owned پیش‌فرض | D-087، D-105 |
| P-1 (ثبت مجدد پس از ابطال با ImportCase جدید) | D-080 `Accepted` |
| P-2: شمارهٔ پروندهٔ ابطال‌شده دوباره استفاده نمی‌شود؛ P-3: Maker-Checker جایگزینی Proforma سیاست قابل تنظیم است | D-080 `Accepted`؛ D-086 بند ۹ |
| Excel Import در این مرحله پیاده‌سازی نمی‌شود؛ D-106 `Proposed` و تأییدنشده است | D-116 (قواعد تأییدشدهٔ قبلی) |

---

## ۱۱. تصمیم‌های باز (B-7)

نوع: «ف» = فنی (Agent می‌تواند پیشنهاد دهد و پس از بازبینی بگیرد، ولی اثر Schema/امنیت نیاز به تأیید دارد)، «ک» = کسب‌وکاری/مالکیتی.

| شناسه | مسئله | اثر فنی/کسب‌وکاری | سؤال مشخص از مالک | نوع | پیشنهاد فنی (غیرمصوب) |
|---|---|---|---|---|---|
| **P-4** | رفتار پس از انقضای Proforma | تعیین می‌کند Proforma منقضی «جاری» می‌ماند یا نه، و ثبت‌سفارش روی آن ممکن است یا نه؛ بر شاخص جاری و گزارش‌ها اثر دارد | آیا Proforma منقضی همچنان «جاری» حساب می‌شود؟ آیا می‌توان روی آن OrderRegistration ساخت؟ آیا فقط با تمدید؟ | ک | `Expired` مشتق از تاریخ؛ ثبت‌سفارش جدید پس از انقضا بدون تمدید رد شود |
| **P-5** | Replace/تغییر شماره یا فروشنده وقتی OrderRegistration زنده به Revision قبلی متصل است | خطر بی‌مبنا شدن ثبت‌سفارش یا جابه‌جایی پیوند Revision | وقتی ثبت‌سفارش زنده است: ممنوع، یا مجاز با Amendment، یا مجاز با ابطال ثبت‌سفارش؟ | ک | ممنوع تا ابطال ثبت‌سفارش (هم‌سو با پیشنهاد D-080 بند ۴؛ آن بند مصوب نیست) |
| **A-1** | محل نگهداری تاریخ اعتبار (سرتیتر یا Revision) | تعیین می‌کند تمدید چه ردیفی را به‌روز می‌کند؛ اثر مستقیم بر Schema | آیا تاریخ اعتبار جزو محتوای تغییرناپذیر Revision است یا ویژگی جاری Proforma با تاریخچه؟ | **ک** (D-116 آن را تصمیم‌نشده می‌داند) | سرتیتر + رویداد |
| **A-2** | کاهش یا تغییر دلخواه تاریخ اعتبار (غیر از تمدید) | D-086 بند ۳.۸ می‌گوید تاریخ اعتبار «قابل اصلاح» است (نه فقط تمدید). آیا «اصلاح تاریخ اعتبار» غیر از «تمدید» وجود دارد و نوع رویدادش چیست | آیا کاهش تاریخ اعتبار مجاز است؟ | ک | فقط تمدید رو به جلو؛ سایر تغییرات رویداد جدا |
| **A-3** | فهرست دقیق «شرایط تجاری»، و ویرایش Draft بدون Revision | تعیین می‌کند کدام فیلد Revision می‌سازد؛ مبنای تست‌ها | از فهرست D-086 بند ۳.۷ تغییر کدام فیلدها Revision می‌سازد؟ آیا ویرایش Draft پیش از صدور Revision می‌سازد؟ | ک | تصمیم نشود؛ طراحی دامنه فهرست را پارامتر بگیرد |
| **A-4** | فرمول مبالغ (کل پرونده، محموله، کرایه، فی) | D-086 بند ۳.۹: مستقل‌اند؛ بدون فرمول جمع یا جایگزین نمی‌شوند؛ اعتبارسنجی سرجمع خطوط متوقف است | رابطهٔ مبلغ کل با خطوط و کرایه چیست و کدام‌ها محاسبه‌ای‌اند؟ | ک | هیچ فرمولی اعمال نشود |
| **A-5** | Revision پس از ثبت‌سفارش (D-104) | آیا Revision جدید مجاز است و پیوند ثبت‌سفارش را چه می‌کند | پس از ثبت‌سفارش، تغییر شرایط تجاری (غیر از کالا) چگونه است؟ | ک | ممنوع یا با Amendment |
| **A-6** | تلورانس تناژ | حد مجاز اختلاف تناژ خطوط/محموله با مقدار ثبت‌شده | درصد یا مقدار مجاز اختلاف چقدر است و به کدام سند اعمال می‌شود؟ | ک | بدون پیش‌فرض |
| **A-7** | روش تخصیص `PaymentPlan` (باقی‌مانده به مرحلهٔ آخر یا largest-remainder) | بر جمع مراحل و تست‌های موجود اثر دارد؛ `PaymentPlanCalculator` فعلاً تغییر نمی‌کند | کدام روش تخصیص؟ | ک | بدون تغییر الگوریتم فعلی تا تصمیم |
| **A-8** | `MinorUnits` ریال و ارزهای مجاز | دقت محاسبهٔ مبالغ و ذخیره | ریال با چند رقم اعشار؟ فهرست ارزهای مجاز؟ | ک | بدون پیش‌فرض |
| **N-1** | وضعیت Replace/`Superseded`/`SupersedesProformaId` و قید «یک جاری با ایندکس یکتای فیلترشده» پس از D-086 (که سقف را قابل تنظیم و یکتایی شماره را Company-wide کرده) | مدل Replace و کلید یکتایی شماره به آن وابسته است | کدام بخش مدل جایگزینی D-080 (`Superseded`، زنجیرهٔ جانشین) هنوز معتبر است؟ | ک | پرسش رسمی؛ تا تصمیم، فقط Revision مدل شود |
| **N-2** | `OrderRegistrationLine` در V3.2 §1 | آیا ثبت‌سفارش خط جدا دارد یا از Revision خوانده می‌شود | آیا ثبت‌سفارش خطوط مستقل کالا دارد؟ | ک | تصمیم نشود تا مرحلهٔ OrderRegistration |
| **N-3** | Proforma در پروندهٔ جایگزین P-1 | پرونده جدید با Proforma جدید و شمارهٔ یکتا؟ یا کپی صریح؟ | در پروندهٔ جایگزین، Proforma جدید با شمارهٔ تازه ساخته می‌شود یا کپی؟ | ک | کپی صریح با شمارهٔ جدید کاربر |
| **N-4** | مانع F-1: `CompanyId` جدا در `TradeEntity` | بدون آن Entityهای جدید مدل هدف را ندارند | گزینهٔ ۱ (دامنهٔ خالص با CompanyId صریح) تأیید می‌شود؟ | ک/ف | گزینهٔ ۱ |
| **N-5** | مانع F-2: Context بدون FiscalPeriod | سرویس‌های Proforma نباید به دوره وابسته شوند؛ تغییر مجوزدهی لازم است | تغییر یا افزودن Context مستقل از دوره برای ImportCase/Proforma مجاز است؟ | ک/ف | Context جدا با تست امنیتی |
| **N-6** | سطح اجرای قید تغییرناپذیری Revision در DB | بدون Trigger/مجوز، `UPDATE` مستقیم SQL ممکن است | آیا حفاظت سطح DB (مجوز یا Trigger) لازم است؟ | ف | Guard در دامنه و EF + تست؛ قید DB جدا |
| **N-7** | محل دادهٔ مستقل از دوره در توپولوژی D-111 (Company + FiscalPeriod DB) و FK/یکتایی Company-wide | قید یکتای شمارهٔ Proforma و FK بین Proforma/ImportCase/Product فقط داخل یک دیتابیس کار می‌کند؛ بین دیتابیس‌ها FK ممنوع است (D-111 بند ۸) | دادهٔ Trade (مستقل از FiscalPeriod) در توپولوژی Company + FiscalPeriod در کدام دیتابیس قرار می‌گیرد و یکتایی Company-wide چگونه تضمین می‌شود؟ | ک/ف | پرسش پیش از S3-0 |
| **N-8** | Maker-Checker (P-3، D-086 بند ۹.۳) روی Revision و تمدید | اگر تأیید فعال باشد، عملیات تا تأیید نفر دوم Pending است؛ معلوم نیست Revision یا رویداد Pending کجا نگه داشته می‌شود و مجوز `trade.form.proforma.approve` چه چیزی را تأیید می‌کند | کدام عملیات Proforma (Revision، تمدید، لغو) مشمول Maker-Checker است و حالت Pending چگونه ذخیره شود؟ | ک | تصمیم نشود؛ Pending خارج از Revision ثبت‌شده مدل شود |
| **N-9** | محل نگهداری فروشنده (سرتیتر یا Revision) | تعیین می‌کند تغییر فروشنده Revision بسازد یا Replace؛ بر یکتایی و P-5 اثر دارد | فروشنده جزو محتوای Revision است یا هویت ثابت Proforma؟ | ک | تصمیم نشود تا پاسخ N-1 |
| **N-10** | آزادشدن شمارهٔ Proforma `Cancelled` یا حذف‌نرم‌شده، و معنای مجوز `trade.form.proforma.delete` | تعیین می‌کند ایندکس یکتا چه ردیف‌هایی را شامل شود؛ بر P-1 (ثبت مجدد در پروندهٔ جدید) اثر دارد | آیا شمارهٔ Proforma ابطال‌شده یا حذف‌شده دوباره قابل استفاده است؟ حذف Proforma چه معنایی دارد؟ | ک | شمارهٔ `Cancelled` آزاد نشود؛ حذف فقط پیش از هر اثر |
| **N-11** | سایر ابهام‌های کوچک مخزن | HS Code روی Product اختیاری است (`Product.cs:28`)؛ برنامهٔ پرداخت با صفر مرحله رد می‌شود (`PaymentPlanIssueCode.NoStages`)؛ سقف خطوط باید از نسخهٔ پروفایل ثبت‌شده روی پرونده بیاید (D-086 بند ۱.۳)؛ OD-007 (سطح `ForexCommitment`: Proforma یا OrderRegistration)؛ OD-001 (`ImportCase.OnHold`) فقط از جهت وضعیت پرونده با صفر Proforma جاری مرتبط است | آیا HS Code روی خط الزامی است؟ آیا برنامهٔ پرداخت برای Proforma اجباری است؟ | ک | بدون پیش‌فرض |
| **G0..G6، D-090..D-096** | گیت‌های S3-0، سیاست Migration و Backfill | هر Schema/Migration را مسدود می‌کنند | (تعریف گیت‌ها در `TRADE_S3_READINESS.md` §۳؛ G0 به تأیید S3-0h، D-092 و D-094 وابسته است؛ این سند بازشان نمی‌کند) | ک | — |

---

## ۱۲. ریسک‌ها و وابستگی‌ها

| ریسک | احتمال/اثر | کاهش |
|---|---|---|
| ساخت Entity با `CompanyId` پیش از گیت‌ها | اثر بالا: انحراف Schema | گزینهٔ ۱ در ۸.۳؛ بدون EF |
| وابستگی ناخواستهٔ سرویس‌ها به FiscalPeriod از `TradeAccessContext` | اثر بالا: نقض D-086 | N-5؛ بدون استفاده از `FiscalPeriodId` در مرحلهٔ C |
| ادغام ProformaItem/RevisionItem مشکل گزارش «اقلام فعلی» | کم: یک Join | شاخص ساده؛ اندازه‌گیری |
| تغییرناپذیری فقط در Application | متوسط: `UPDATE` مستقیم | N-6؛ تست Integration |
| ابهام N-1 بر کلید یکتایی شمارهٔ Proforma | بالا: محل ایندکس Schema | پرسش پیش از S3-3 |
| تکرار/اختلاف بین V3.2 و D-104/D-116 | متوسط | یادداشت‌های تاریخ‌دار؛ این سند مرجع جاری طراحی است |
| ادعای `Verified` بدون SQL Server واقعی | اثر بالا | هیچ ادعا؛ محلی `NotRun` |
| ارث‌بری Revision/History از `TradeEntity` با `RecordUpdate`/`SoftDelete` عمومی | بالا: نقض تغییرناپذیری | پایهٔ append-only یا Guard صریح در مرحلهٔ C (بخش ۲.۵) |


وابستگی‌ها: گیت‌های S3-0 (G0..G6)، D-090..D-096 (`Proposed`)، P-4/P-5، تصمیم‌های A-1..A-8 و N-1..N-11.

---

## ۱۳. برنامهٔ پیشنهادی مرحلهٔ C `[طراحی-پیشنهادی]`

مرحلهٔ C **به‌تنهایی شروع نمی‌شود**؛ ورودی آن تأیید مالک روی N-4 و N-5 است.

1. **C-1 (دامنهٔ خالص، بدون EF):** `Proforma`، `ProformaRevision` (سازنده‌ای بدون mutator)، `ProformaLine`، `ProformaPaymentStage`، `ProformaHistoryEvent` با `CompanyId` صریح؛ تست واحد TDD؛ بدون DbSet/Migration/Snapshot. ورودی‌هایی که به پروفایل (سقف خطوط) و ImportCase وابسته‌اند **پارامتر** هستند؛ در کد هیچ Entity پروفایل یا ImportCase وجود ندارد (S3-1/S3-2 اجرا نشده) و C نباید آن‌ها را بسازد. پایه/Guard تغییرناپذیری مطابق H-2 (بخش ۲.۵) در ابتدای C-1 تصمیم‌گیری می‌شود.
2. **C-2 (Application با Fake Store):** `ProformaService` با پورت باریک، Outcome، `RowVersion`، شمارهٔ یکتا، تمدید اعتبار، Revision. تست‌ها با Fake Store و **نه** «تست SQL Server». `PaymentPlanCalculator` بدون تغییر. ImportCase و پروفایل از طریق پورت باریک و Fake در تست تأمین می‌شوند، نه Entity واقعی.
3. **C-3 (OrderRegistration):** فقط پس از تصمیم A-5 و P-5.
4. **ترتیب مستقل از باز بودن:** C-1 و C-2 فقط بدون تصمیم P-4/P-5/A-3/A-6/A-7/A-8 قابل انجام‌اند به شرطی که این‌ها پارامتر یا `NeedsApproval` بمانند. **تمدید اعتبار و فروشنده تا پاسخ A-1 و N-9 از C-1/C-2 بیرون می‌مانند** (یا ValidUntil/فروشنده صراحتاً پارامتر پشت پورت می‌شوند)؛ Maker-Checker (N-8) و A-2 نیز اثر دارند و Pending در C مدل نمی‌شود.
5. **مسدود (`Blocked`):** persistence، Migration، Endpoint، UI، Entitlement — تا G0..G6 و تأیید مالک.

---

## ۱۴. معیارهای پذیرش مرحلهٔ B

- [x] وضعیت فعلی با کد واقعی `37f8f913` تطبیق داده شد (بخش ۱).
- [x] مدل مفهومی، روابط، چرخهٔ عمر، Revision، تمدید و اتصال OrderRegistration مستند شد.
- [x] تصمیم‌های مصوب (بخش ۱۰) از تصمیم‌های باز (بخش ۱۱) جدا هستند.
- [x] هیچ تصمیم کسب‌وکاری حدس زده نشد؛ هر پیشنهاد `[طراحی-پیشنهادی]` است.
- [x] هیچ گیت S3-0، Schema، Migration، DbSet، Entity اجرایی، API، UI یا Entitlement تغییر نکرد.
- [x] D-087/D-105 دست‌نخورده و D-090 `Proposed` باقی است.
- [x] فایل‌های مشترک #755 تغییر نکردند.
- [ ] بازبینی مستقل معماری و یافته‌های آن (در PR ثبت می‌شود).
- [ ] CI روی Head نهایی (در PR ثبت می‌شود).

## فایل‌های مهم تغییرکرده
- `docs/handoffs/ISSUE-UNNUMBERED-trade-proforma-domain-design.md` — همین سند (تنها تغییر این PR)

## تصمیم‌های فنی
هیچ تصمیم مصوب جدیدی ثبت نشد؛ همهٔ پیشنهادها `[طراحی-پیشنهادی]` و تصمیم‌های باز در بخش ۱۱ هستند.

## بررسی و آزمون
| فرمان | نتیجه | توضیح |
|---|---|---|
| `git diff --check` | در PR ثبت می‌شود | فقط مستندات |
| `dotnet build/test`، `npm`، `check_static_assets.py` | **اجرا نشد** | هیچ کد یا دارایی وبی تغییر نکرده است |
| SQL Server/E2E | **اجرا نشد** | مرتبط نیست |

## امنیت، داده و سازگاری
هیچ اثری بر احراز هویت، مجوز، Tenant/Company، Schema، Migration یا داده ندارد. `DenyAllTradeModuleEntitlement` دست‌نخورده و Trade Fail-Closed است. موانع F-1/F-2 (CompanyId و Context بدون دوره) فقط ثبت شده‌اند و تغییر امنیتی انجام نشد؛ حل F-2 نیازمند `security-reviewer` است.

## شناسه‌های Git
شاخه: `docs/trade-proforma-domain-design`؛ Commit مبنا: `37f8f913`؛ SHA نهایی و شمارهٔ PR در توضیحات PR ثبت می‌شود.

## کارهای باقی‌مانده
- پاسخ مالک به N-1، N-4، N-5، P-4، P-5 و A-1..A-8.
- ردیف `WORK_LOG.md` و `PROJECT_STATUS.md` پس از تعیین تکلیف #755 جدا Append شود.
