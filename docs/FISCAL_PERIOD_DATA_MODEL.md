# مدل داده هدف برای مالکیت Company، دوره مالی و پرونده‌های سنواتی

- **وضعیت:** طراحی منطقی پیشنهادی بر پایه تصمیم‌های مالک در 2026-11-23؛ هنوز Schema/DDL نیست.
- **پیاده‌سازی:** NotImplemented؛ هیچ Entity، DbSet، Configuration، API، Permission یا Migration با این سند ایجاد نمی‌شود.
- **مرجع تصمیم:** D-117؛ همراه با D-087، D-103، D-105، D-111، D-114 و D-115.
- **اصل طراحی:** مالکیت منطقی داده از توپولوژی فیزیکی دیتابیس مستقل است. یک مدل دامنه و قرارداد Application، با مسیرهای دسترسی Single-DB و Multi-DB.

> نام و فیلدهای این سند طراحی منطقی‌اند. پیش از DDL باید موجودیت‌ها، جداول، کلیدها، قیود، ایندکس‌ها و مسیرهای دسترسی واقعی با S3-0 و مدل زیرسیستم تطبیق داده شوند. هیچ فیلدی صرفاً به دلیل حضور در این سند به جدول موجود اضافه نشود.

## 1. واژگان و مرزهای هویتی

| مفهوم | معنی | تغییرپذیری |
|---|---|---|
| TenantId | مرز مالکیت/جداسازی مشتری | پایدار |
| CompanyId | شرکت عملیاتی داخل Tenant | پایدار |
| FiscalPeriodId | دوره مالی متعلق به Company | مستقل از دیتابیس فیزیکی |
| OriginFiscalPeriodId | دوره‌ای که پرونده در آن ایجاد شده | پس از ایجاد پرونده تغییرناپذیر |
| TargetFiscalPeriodId | دوره‌ای که پرونده در فهرست کارهای آن ادامه می‌یابد | برای هر مشارکت جدا |
| Document FiscalPeriodId | دوره ثبت سند مالی/عملیات دوره‌ای | متعلق به همان سند |

قواعد:
- TenantId و CompanyId دو شناسه مستقل‌اند؛ هر Entity جدید Company-owned هر دو را دارد و از context معتبر سمت سرور مقدار می‌گیرد.
- OriginFiscalPeriodId جای FiscalPeriodId عملیات را نمی‌گیرد و مجوز دسترسی نیست.
- CreatedAt زمان ایجاد است، نه جایگزین OriginFiscalPeriodId؛ تاریخ ایجاد به‌تنهایی نمی‌تواند سال مالی مبدأ را با اطمینان تعیین کند.
- شناسه فنی رکورد از شماره نمایشی مستقل است.
- شماره پرونده به‌تنهایی برای Routing یا شناسایی یکتای رکورد کافی نیست.

## 2. ماتریس توپولوژی و مالکیت داده

| توپولوژی | داده Company-level | داده period-scoped | پرونده بلندمدت |
|---|---|---|---|
| Tenant DB | همان DB Tenant با CompanyId | همان DB با FiscalPeriodId | همان DB |
| Company DB | DB همان Company | همان DB با FiscalPeriodId | همان DB |
| Company + FiscalPeriod DB | Assignment پایدار Company-level | DB اختصاصی Company+FiscalPeriod | Assignment پایدار Company-level |
| Tenant + FiscalPeriod DB | تا تصمیم مالک Pending | DB هر دوره Tenant | تا تصمیم مالک Pending |

- Tenant + FiscalPeriod DB همچنان تا تأیید صریح مالک قابل انتخاب نیست.
- در توپولوژی‌های Single-DB، یک رکورد مرجع نگهداری می‌شود و Queryها با CompanyId و در صورت لزوم FiscalPeriodId محدود می‌شوند.
- در Company + FiscalPeriod DB، یک محل پایدار Company-level برای داده‌های بلندمدت و مرجع لازم است. دیتابیس‌های دوره‌ای مالک اسناد و عملیات مالی همان دوره‌اند.
- Registry سمت سرور D-115 تنها مرجع مقصد فیزیکی است. این سند Registry موازی یا جدول فهرست اسناد در Control Plane ایجاد نمی‌کند.
- در Multi-DB هیچ Cross-database FK یا فرض Distributed Transaction وجود ندارد.

## 3. دسته‌بندی داده و محل مرجع

| دسته | مرجع منطقی | رفتار در Single-DB | رفتار در Company + FiscalPeriod DB |
|---|---|---|---|
| Party / مشتری / تأمین‌کننده | Company-owned | یک رکورد مرجع در همان DB | مرجع در Company DB؛ Snapshot موردنیاز در DB سال مقصد |
| Product | فعلاً Trade-owned طبق D-116 | یک رکورد مرجع در همان DB | مرجع در Company DB؛ Snapshot موردنیاز در DB سال مقصد |
| ImportCase، Proforma، Revision، OrderRegistration و تاریخچه تجاری | Company-owned و مستقل از دوره عملیاتی | همان DB | Company DB پایدار |
| پیوست پرونده بلندمدت | مالک پرونده/Company | metadata در همان DB؛ محتوا در File Storage | metadata در Company DB؛ محتوا بدون کپی سالانه |
| سند مالی و گردش دوره‌ای | Company + FiscalPeriod | همان DB با FiscalPeriodId | DB همان Company+FiscalPeriod |
| مانده افتتاحیه | دوره مقصد | همان DB، متعلق به دوره مقصد | DB سال مقصد |
| تصمیم ادامه پرونده در سال جدید | Target FiscalPeriod | همان DB | DB سال مقصد |

### 3.1 Snapshot اطلاعات پایه

در Company + FiscalPeriod DB، «انتقال اطلاعات پایه» به معنی ساخت Snapshot سالانه است، نه انتقال مالکیت و نه Dual-write.

- مرجع اصلی Party/Product در Company DB باقی می‌ماند.
- در آغاز دوره، Snapshot حداقلی و موردنیاز از رکوردهای فعال/مجاز به DB دوره مقصد منتقل می‌شود.
- شناسه پایدار مرجع PartyId/ProductId حفظ می‌شود؛ در صورت نیاز شناسه محلی Snapshot جداست، اما StableId باید بدون ابهام حفظ شود.
- Snapshot شامل SourceVersion/SourceUpdatedAt و SnapshotCreatedAt است تا منشأ و زمان نسخه قابل ردیابی باشد.
- اصلاح Snapshot سال جدید نباید اطلاعات مرجع یا Snapshot سال‌های بسته را بازنویسی کند.
- تغییرات سال جاری فقط در مرجع Company یا Snapshot سال جاری، مطابق مالکیت فیلد و قواعد زیرسیستم، اعمال می‌شوند؛ همگام‌سازی دوطرفه خودکار ممنوع است.
- برای اطلاعاتی که نیاز به تاریخچه کامل دارند، Snapshot سند/عملیات در زمان ایجاد سند ثبت می‌شود؛ Snapshot Master Data جایگزین تاریخچه تجاری نیست.
- Product فعلاً در Trade باقی می‌ماند؛ این سند آن را به Core یا Inventory منتقل نمی‌کند.

### 3.2 داده‌های بلندمدت و پیوست‌ها

- پرونده‌های تجاری و تاریخچه تغییرناپذیر آن‌ها در Company-level owner باقی می‌مانند؛ بستن دوره مالی آن‌ها را قفل نمی‌کند.
- فایل‌ها خارج از SQL Server نگهداری می‌شوند؛ SQL فقط metadata، شناسه Storage و checksum/size/type موردنیاز را نگه می‌دارد.
- انتقال سالانه پرونده یا فایل، کپی کامل ایجاد نمی‌کند.
- حذف دیتابیس مبدأ نباید پرونده بلندمدت را حذف کند؛ آرشیو Company DB باید دسترسی read-only موردنیاز پرونده‌های فعال را حفظ کند.

## 4. مدل منطقی جداول

نام‌ها illustrative هستند؛ پیش از پیاده‌سازی با نام‌گذاری و موجودیت‌های واقعی مخزن تطبیق داده شوند.

### 4.1 Registry و Assignment

**جدول عملیاتی جدیدی در این سند پیشنهاد نمی‌شود.** از Database Registry/Assignment موجود طبق D-111 و D-115 استفاده شود.

کلیدهای منطقی Routing:
- Tenant scope: TenantId
- Company scope: TenantId + CompanyId
- Company-period scope: TenantId + CompanyId + FiscalPeriodId
- Tenant-period scope: فقط پس از تصویب توپولوژی مربوط

Registry باید DatabaseId/TargetId را resolve کند؛ Connection String و Secret در Registry ذخیره نمی‌شود. Resolver fail-closed است و Assignment هم‌پوشان یا نامعتبر را رد می‌کند.

### 4.2 FiscalPeriod (کاتالوگ دوره مالی)

مالک منطقی: Company. اگر جدول موجود این اطلاعات را دارد، ستون تکراری نسازید.

فیلدهای منطقی موردنیاز:
- Id (PK)
- TenantId
- CompanyId
- Code/DisplayName (در صورت نیاز دامنه)
- StartDate
- EndDate
- Status (Open، Closing، Closed؛ Archived فقط با lifecycle مستقل)
- ClosedAt، ClosedByUserId (nullable)
- ReopenedAt، ReopenedByUserId و ReopenReason (nullable؛ تاریخچه کامل رویداد در Audit)
- RowVersion
- CreatedAt، CreatedByUserId، UpdatedAt، UpdatedByUserId

قیود:
- FK/Unique مرکب برای تطبیق TenantId و CompanyId.
- StartDate < EndDate.
- عدم هم‌پوشانی دوره‌های یک Company در Application با concurrency control و تست SQL Server enforce شود؛ CHECK ساده برای هم‌پوشانی کافی نیست.
- وضعیت دوره، مجوز و قاعده ثبت مالی را کنترل می‌کند؛ OriginFiscalPeriodId پرونده را تغییر نمی‌دهد.

### 4.3 Company-owned long-lived case root

نمونه Trade: ImportCase، Proforma، OrderRegistration و سایر پرونده‌های مادر که عمرشان از یک FiscalPeriod عبور می‌کند.

فیلدهای مشترک منطقی:
- Id (PK)
- TenantId
- CompanyId
- OriginFiscalPeriodId (Required، immutable)
- BusinessNumber / ExternalNumber (طبق نوع موجودیت و سیاست شماره‌گذاری)
- Status (واژگان و transition تخصصی زیرسیستم)
- CreatedAt، CreatedByUserId
- UpdatedAt، UpdatedByUserId
- RowVersion
- ActivityProfileId/Version (فقط اگر زیرسیستم از پروفایل نسخه‌دار استفاده می‌کند)
- ClosedAt/ClosedByUserId و CancellationReason در صورت نیاز دامنه

قواعد:
- OriginFiscalPeriodId به دوره مبدأ اشاره منطقی دارد؛ در Single-DB یا Company DB که کاتالوگ FiscalPeriod در همان DB است، FK مرکب داخلی ممکن است. اگر مرجع دوره در دیتابیس دیگری باشد، فقط شناسه + اعتبارسنجی سمت سرور.
- این فیلد به معنی period-scoped بودن پرونده نیست و TradeOperationalPeriod ایجاد نمی‌کند.
- برای عملیات مالی مرتبط، سند مالی FiscalPeriodId خودش را دارد.
- Revisionها و HistoryEventها append-only/immutable طبق قواعد زیرسیستم باقی می‌مانند.
- TenantId و CompanyId مستقل و هر دو الزامی‌اند؛ وضعیت Legacy برابر بودن فعلی آن‌ها قرارداد طراحی جدید نیست.

### 4.4 FiscalPeriod participation (فهرست ادامه پرونده در دوره مقصد)

برای هر زیرسیستم جدول تخصصی؛ نمونه نام:
- TradeCaseFiscalPeriodParticipation
- ContractFiscalPeriodParticipation (در آینده، در صورت نیاز)
- ClaimFiscalPeriodParticipation (در آینده، در صورت نیاز)

فیلدهای منطقی نمونه Trade:
- Id (PK)
- TenantId
- CompanyId
- TargetFiscalPeriodId (Required)
- SourceCaseId (Required؛ شناسه پایدار پرونده در Company owner)
- OriginFiscalPeriodId (Required؛ برای فهرست و جست‌وجو، نه منبع حقیقت)
- ParticipationStatus (Included / Excluded)
- DecisionByUserId (nullable برای درج خودکار اولیه)
- DecisionAt (nullable برای درج خودکار اولیه)
- DecisionReason (nullable؛ هنگام Excluded اجباری)
- FirstIncludedAt
- CreatedAt، CreatedByUserId
- UpdatedAt، UpdatedByUserId
- RowVersion

کلیدها و قیود:
- Unique: (TenantId, CompanyId, TargetFiscalPeriodId, SourceCaseId).
- FK به TargetFiscalPeriod و Company در همان DB مقصد.
- در Single-DB، FK به پرونده مرجع نیز ممکن است.
- در Multi-DB، FK به SourceCaseId وجود ندارد؛ SourceCaseId با Company owner از طریق Registry سمت سرور resolve و وجود/مالکیت/وضعیت پرونده در سرور اعتبارسنجی می‌شود.
- SourceCaseId به همراه CompanyId و TenantId برای جست‌وجو استفاده می‌شود؛ OriginFiscalPeriodId به‌تنهایی مقصد دیتابیس را تعیین نمی‌کند.
- ParticipationStatus مجوز دسترسی یا وضعیت پرونده نیست.
- مدیر مجاز می‌تواند Included/Excluded را تغییر دهد؛ هر تغییر با دلیل و Audit ثبت شود.
- درج خودکار اولیه فقط برای پرونده‌های صریحاً Open؛ OnHold، Closed، Cancelled و وضعیت‌های نامشخص به‌صورت پیش‌فرض وارد نمی‌شوند.
- اگر پرونده مبدأ بسته/لغو/غیرقابل‌عملیات شود، وضعیت زنده مبدأ بر Participation مقدم است؛ رکورد Participation برای تاریخچه تصمیم حفظ می‌شود.
- اجرای مجدد Rollover نباید تصمیم مدیر را بازنویسی کند؛ فقط رکوردهای جدید را اضافه کند.
- حذف فیزیکی Participation ممنوع؛ حذف از فهرست با Excluded ثبت می‌شود.

**Audit:** از زیرساخت Audit موجود استفاده شود و Actor، Company، TargetFiscalPeriod، SourceCase، old/new status، reason، timestamp و correlation/operation id ثبت شود. جدول تاریخچه موازی فقط اگر ممیزی کد ثابت کند Audit موجود نیاز را پوشش نمی‌دهد، در ADR جداگانه پیشنهاد شود.

### 4.5 Company master-data snapshots در دیتابیس سالانه

جداول Snapshot تخصصی باشند؛ از جدول عمومی EntityType/EntityId اجتناب شود.

نمونه PartyPeriodSnapshot:
- Id (PK محلی)
- TenantId
- CompanyId
- TargetFiscalPeriodId
- StablePartyId (شناسه پایدار Company-level)
- SourceVersion / SourceRowVersion
- SourceUpdatedAt
- SnapshotCreatedAt
- Code و فیلدهای نمایشی/عملیاتی لازم برای دوره (فهرست دقیق از Party فعلی استخراج شود)
- IsActiveForPeriod

نمونه TradeProductPeriodSnapshot:
- Id (PK محلی)
- TenantId
- CompanyId
- TargetFiscalPeriodId
- StableProductId
- SourceVersion / SourceRowVersion
- SourceUpdatedAt
- SnapshotCreatedAt
- Code، Name، HSCode و فقط فیلدهای لازم واقعی Product
- IsActiveForPeriod

قیود:
- Unique: (TenantId, CompanyId, TargetFiscalPeriodId, StablePartyId) و معادل Product.
- هر StableId در هر دوره یک Snapshot معتبر دارد؛ تغییرات بعدی با نسخه/Revision Snapshot یا سیاست صریح همان زیرسیستم انجام می‌شود، نه overwrite بی‌ردپا.
- مقادیر دقیق Party/Product باید از Entityهای واقعی و نیاز مصرف‌کننده استخراج شوند؛ این سند عمداً فیلدهای دامنه‌ای نامعلوم را اختراع نمی‌کند.
- Snapshotها قابل بازسازی‌اند، اما بازسازی پس از شروع استفاده نباید داده‌های اصلاح‌شده در مقصد را بی‌قیدوشرط جایگزین کند؛ اختلاف باید گزارش و تصمیم‌گیری شود.

### 4.6 CompanyDocumentNumberSequences

مالک: Company-level DB/Assignment پایدار؛ در Single-DB همان دیتابیس شرکت.

فیلدهای پیشنهادی:
- Id (PK)
- TenantId
- CompanyId
- DocumentType (کد پایدار از کاتالوگ سرور)
- NumberingMode (CompanyContinuous / FiscalPeriodReset)
- FiscalPeriodId (nullable؛ null برای پیوسته، required برای ریست سالانه)
- NextNumber (bigint)
- PrefixTemplate / DisplayFormat (اختیاری؛ فقط قالب نمایش)
- RowVersion
- CreatedAt، UpdatedAt

قواعد:
- Unique filtered index برای (TenantId, CompanyId, DocumentType) وقتی FiscalPeriodId IS NULL.
- Unique filtered index برای (TenantId, CompanyId, DocumentType, FiscalPeriodId) وقتی FiscalPeriodId IS NOT NULL.
- تخصیص شماره باید اتمیک و concurrency-safe باشد؛ SELECT سپس UPDATE بدون قفل/RowVersion کافی نیست.
- شماره تخصیص‌یافته حتی در صورت ابطال/حذف منطقی آزاد نمی‌شود.
- پیش‌فرض CompanyContinuous؛ FiscalPeriodReset فقط با تنظیم صریح شرکت.
- در Company + FiscalPeriod DB شمارنده پیوسته در DB سالانه ذخیره نشود.
- قالب پیشوند سال فقط presentation است و نباید معنای هویت یا Routing داشته باشد.

### 4.7 FiscalPeriodRolloverRuns

جدول کنترل عملیات در دیتابیس مقصد یا Company-level orchestration store متناسب با توپولوژی؛ فقط اگر عملیات rollover چندمرحله‌ای و قابل retry است.

فیلدهای پیشنهادی:
- Id (PK)
- TenantId
- CompanyId
- SourceFiscalPeriodId
- TargetFiscalPeriodId
- OperationType (OpeningBalances / MasterDataSnapshot / CaseParticipation / FullRollover)
- Status (Pending / Running / Completed / CompletedWithWarnings / Failed / Cancelled)
- IdempotencyKey
- RequestedByUserId
- StartedAt، CompletedAt
- SourceVersion/SourceWatermark (در صورت وجود قرارداد قابل اتکا)
- ResultSummary و FailureCode محدود و ساختاریافته
- RowVersion

قواعد:
- Unique: (TenantId, CompanyId, TargetFiscalPeriodId, OperationType, IdempotencyKey).
- هر مرحله retry-safe باشد؛ عملیات تکراری نباید رکورد یا شماره تکراری بسازد.
- عملیات چند DB تراکنش اتمیک توزیع‌شده فرض نمی‌کند؛ هر مرحله ثبت وضعیت و reconciliation دارد.
- Worker/Queue/Broker دائمی صرفاً برای این فرایند ساخته نشود؛ اجرای درخواست‌محور/قابل ادامه کافی است مگر بار واقعی خلاف آن را ثابت کند.

### 4.8 Opening balances

مانده افتتاحیه، پرونده تجاری یا مشارکت پرونده نیست. پیش از ساخت جدول جدید، ساختار فعلی Accounting و سندهای افتتاحیه موجود ممیزی شود.

اگر مدل فعلی پوشش کافی نداشت، مدل منطقی پیشنهادی:
- FiscalPeriodOpeningBatch: Id، TenantId، CompanyId، TargetFiscalPeriodId، SourceFiscalPeriodId، Status (Provisional / NeedsReview / Finalized)، SourceWatermark/SourceVersion، GeneratedAt، GeneratedByUserId، FinalizedAt، FinalizedByUserId، RowVersion.
- FiscalPeriodOpeningLine: Id، BatchId، AccountId/Dimension references، Debit، Credit، Currency/Quantity fields فقط مطابق مدل واقعی حسابداری، SourceReference، ReconciliationStatus.
- هر بازسازی یک Batch/Revision جدید یا نسخه قابل ممیزی تولید کند؛ سند/خطوط قبلی حذف نشوند.
- بازسازی نباید اسناد عملیاتی سال جدید را بی‌قیدوشرط overwrite کند.
- ثبت جدید در سال مبدأ بعد از Snapshot، Batch مقصد را NeedsReview می‌کند؛ اختلاف و اثر پیش از Finalize نمایش داده شود.
- قواعد دقیق مانده، ارز، ابعاد، موجودی و حساب‌های کنترلی از معماری Accounting استخراج می‌شوند؛ در این سند فرمول حسابداری اختراع نمی‌شود.

## 5. وضعیت پرونده و سیاست پیش‌فرض Carry-forward

- فقط statusهای صریحاً نگاشت‌شده به IsCarryable=true وارد فهرست سال جدید می‌شوند.
- پیش‌فرض عمومی: فقط Open.
- OnHold تا تصمیم مستقل هر زیرسیستم به‌صورت خودکار وارد نمی‌شود.
- Closed و Cancelled وارد نمی‌شوند.
- رد مجوز/درخواست به‌تنهایی به معنی Closed نیست؛ Status کلی پرونده مرجع است.
- هر زیرسیستم مالک mapping وضعیت‌های خود است؛ وضعیت عمومی مشترک یا arbitrary workflow ساخته نمی‌شود.
- فهرست سال جدید یک Read Model/Query تجمیعی از جدول‌های تخصصی است؛ جدول polymorphic عمومی یا Core WorkItem ایجاد نمی‌شود.

## 6. مجوز و امنیت

- مدیریت مشارکت با مجوز مستقل هر زیرسیستم: الگوی <module>.carryover.manage، برای مثال trade.carryover.manage.
- اختیار متعلق به مدیر مجاز همان Company و فقط در Company scope خودش است.
- System Administrator سراسری به‌صورت ضمنی مجوز عملیاتی Companyها ندارد (D-064).
- View و mutation هر دو در سمت سرور با CompanyAccessService و کنترل‌های فعلی عضویت، Entitlement، Permission و Data Scope انجام می‌شوند.
- OriginFiscalPeriodId، SourceCaseId یا وجود Participation هرگز مجوز نیستند.
- تغییر Included/Excluded باید Audit شود و DecisionReason برای Excluded اجباری باشد.
- عملیات پرونده مستقل از دوره می‌تواند Context بدون FiscalPeriodId داشته باشد، اما Session، Membership، Company/Tenant scope، Entitlement، Permission و Data Ownership همگی باقی می‌مانند.
- عملیات مالی/period-scoped به FiscalPeriod معتبر و باز نیاز دارد.

## 7. بستن سال، بازگشایی و ادامه پرونده

- بستن FiscalPeriod و بستن پرونده تجاری دو lifecycle مستقل‌اند.
- بعد از Closed، نوشتن مالی در همان دوره ممنوع است.
- ویرایش داده period-owned قدیمی فقط پس از بازگشایی کنترل‌شده همان FiscalPeriod و ثبت Actor/Reason/Time/Audit مجاز است.
- پرونده Company-owned بلندمدت می‌تواند پس از بسته‌شدن سال مبدأ ادامه یابد، بدون بازگشایی DB سالانه؛ تغییرات تجاری در Company DB ثبت می‌شوند.
- اثر مالی جدید در دوره جاری و DB همان دوره ثبت می‌شود.
- اصلاح داده تاریخی متعلق به سال بسته با ادامه پرونده تجاری اشتباه نشود و از مسیر reopen همان سال عبور کند.

## 8. Numbering

- پیش‌فرض CompanyContinuous در عمر شرکت.
- گزینه FiscalPeriodReset قابل تنظیم برای هر DocumentType/سیاست شرکت.
- در حالت Continuous، sequence در Company-level owner است.
- در حالت Reset، sequence به FiscalPeriodId مقید می‌شود.
- شماره نمایشی به‌تنهایی کلید اصلی یا route نیست؛ شناسه فنی و scope شرکت/نوع سند تعیین‌کننده‌اند.

## 9. Idempotency، concurrency و cross-database

- هر rollover یک IdempotencyKey پایدار دارد.
- Unique constraints و RowVersion از duplicate/concurrent decisions جلوگیری می‌کنند.
- هر DbContext فقط به یک مقصد و یک Scope متصل است؛ DbContext بین threadها یا دیتابیس‌ها share نمی‌شود.
- Cross-database FK و distributed transaction ممنوع.
- در Multi-DB، ارتباطات با Stable IDs و اعتبارسنجی سمت سرور است.
- اگر DB مبدأ unavailable باشد، فهرست ارجاع می‌تواند وضعیت محدود/خطای شفاف بدهد، اما جزئیات قدیمی را به‌عنوان داده به‌روز وانمود نمی‌کند.
- جست‌وجوی بین چند سال باید fan-out محدود و صفحه‌بندی‌شده باشد؛ scan نامحدود DBهای آرشیوی ممنوع.

## 10. ایندکس‌های منطقی پیشنهادی

ایندکس دقیق باید با Queryهای واقعی و Execution Plan تثبیت شود. نقطه شروع:
- Case root: (TenantId, CompanyId, OriginFiscalPeriodId, Status)؛ Unique روی BusinessNumber طبق DocumentType و NumberingMode.
- Participation: Unique (TenantId, CompanyId, TargetFiscalPeriodId, SourceCaseId)؛ lookup (TenantId, CompanyId, TargetFiscalPeriodId, ParticipationStatus).
- Party/Product snapshot: Unique (TenantId, CompanyId, TargetFiscalPeriodId, StableId)؛ lookup روی Code/Name فقط مطابق جست‌وجوی واقعی.
- Number sequence: unique filtered indexes مطابق §4.6.
- RolloverRuns: Unique idempotency key و index روی Company/TargetPeriod/Status.

از ایندکس‌های تک‌ستونی تکراری، indexهای بسیار عریض یا index بر همه فیلدهای فیلتر بدون بررسی plan خودداری شود.

## 11. ترتیب طراحی و گیت‌های قبل از Schema

1. ممیزی S3-0 و تثبیت معنای TenantId/CompanyId و مالکیت موجودیت‌های فعلی.
2. استخراج موجودیت‌ها/جدول‌های واقعی Party، Product، ImportCase، Proforma، OrderRegistration، فایل‌ها و Accounting opening balances.
3. تعیین DatabaseAssignment واقعی Company-level و Company+FiscalPeriod-level؛ بدون فرض وجود Resolver عملیاتی کامل.
4. تطبیق جدول‌های تخصصی با کاتالوگ Permission/Audit و CompanyAccessService.
5. Security Review برای Context مستقل از دوره و تست‌های منفی حفظ تمام کنترل‌ها.
6. تعریف mapping وضعیت هر زیرسیستم؛ فقط Open در پیش‌فرض عمومی.
7. طراحی Migration افزایشی و Backfill جداگانه؛ داده‌های Legacy با OriginFiscalPeriodId نامعلوم به‌صورت حدسی پر نشوند.
8. بررسی FK/Unique/Index/RowVersion روی SQL Server و تست concurrency/idempotency.
9. تأیید صریح مالک برای Issue اجرایی و سپس TDD/پیاده‌سازی.

## 12. خارج از دامنه و موارد باز

- فعال‌سازی توپولوژی Tenant + FiscalPeriod DB؛ همچنان Pending Owner Decision.
- جزئیات دقیق فیلدهای Party/Product Snapshot تا inventory Entityهای فعلی.
- وضعیت‌های OnHold و وضعیت‌های غیر-Open هر زیرسیستم.
- تعیین فرمول و ابعاد مانده افتتاحیه از اسناد Accounting.
- Permission code نهایی و اتصال آن به Permission Catalog.
- دوره retention و سیاست archive دیتابیس Company owner.
- Migration/Backfill فیلد OriginFiscalPeriodId در داده‌های قدیمی.
- پیاده‌سازی Resolver عملیاتی، cross-db orchestration، Snapshot، Carry-forward، numbering یا opening batch.

این موارد مانع مستندسازی قرارداد منطقی‌اند، اما پیش از Schema یا Runtime باید تعیین تکلیف شوند.
