# Handoff — بستن شکاف‌های S3-0: G3، G6 (SP-1..SP-9)، بسته تصمیم مالک و کارهای بدون Schema

- **Issue:** #397 (S3-0؛ `UNNUMBERED` برای این سند چون Issue جدا ندارد)
- **تاریخ:** 2026-11-24
- **مبنا:** `origin/main@35bd390`؛ همان SHA گزارش مراحل ۰ تا ۲ قبلی؛ بنابراین نتایج P0-0 دوباره اجرا نشدند.
- **ماهیت:** فقط مستندات. هیچ کد، تست، Schema، Migration یا Backfill تغییر نکرد. هیچ تصمیم `Proposed` به `Accepted` نرفت. Excel Import دست‌نخورده و `Deferred` است.
- **PR مرتبط:** ماتریس S3-0d-b در PR #788 است (جدا).

## ۱. مرحلهٔ ۰ و ۱ — تثبیت

| مورد | وضعیت واقعی |
|---|---|
| `origin/main` | `35bd390`؛ تغییری پس از گزارش قبلی نیست؛ PR باز: صفر |
| P0-0 | بدون تکرار. ۵۴ تست Trade (Party/Product HTTP، `TradeProductStoreSqlServerTests`، `TradeMasterDataMigrationSqlServerTests`) روی SQL Server واقعی: ۵۴ موفق، ۰ ناموفق، ۰ Skip. **فقط اثبات جداسازی Company در مدل فعلی است**؛ ادعای Tenant واقعی یا Shared Database نیست. شکاف تازه‌ای پیدا نشد؛ PR صوری ساخته نشد. |

## ۲. مرحلهٔ ۳ — G3 (بررسی مستقل، روی SQL Server واقعی)

PR-D در `main` است (`TenantMigrationSqlServerValidationTests.cs`، `TenantValidationMigrations.cs`، `TenantValidationUnpinnedModels.cs`) و تست‌ها روی **SQL Server واقعی** اجرا می‌شوند، نه Mock/InMemory؛ هدف تست با Guard `D6_*` فقط سرور آزمایشی یک‌بارمصرف را می‌پذیرد و پایگاه تست حتی با شکست حذف می‌شود.

**اجرای امروز** (SQL Server 2022 آزمایشی، `FARANOU_REQUIRE_SQLSERVER=1`، پس از اتمام حذف شد):

| کلاس تست | کل | موفق | ناموفق | Skip | NotRun |
|---|---|---|---|---|---|
| `TenantMigrationSqlServerValidationTests` | 20 | 20 | 0 | 0 | 0 |
| `RenameMigrationSqlServerTests` | 5 | 5 | 0 | 0 | 0 |
| `DatabaseMigrationHostSqlServerTests` | 8 | 8 | 0 | 0 | 0 |
| `LegacyTenantIdEntityContractTests` | 3 | 3 | 0 | 0 | 0 |
| `DatabaseModelTests` | 18 | 18 | 0 | 0 | 0 |
| `TradeMasterDataMigrationSqlServerTests` | 1 | 1 | 0 | 0 | 0 |
| **کل مجموعهٔ `Template.Tests`** | 1623 | 1613 | 0 | 10 (Windows CNG ×8، مجوز سیستم‌فایل ×2) | 0 |

Guardهای Python (`scripts/tests/test_migration_guard_pinned_mappings.py`، `test_migration_guard_rename.py`، `test_migration_guard_mutations.py` با ۳۲ تست، `test_migration_tooling.py`): هر چهار `OK` بعد از نصب `tree-sitter` و `tree-sitter-c-sharp` (همان مرحلهٔ CI). **نکته:** بدون این دو بسته، `test_unmodified_copy_passes_both_guards` با `tree-sitter is not installed` شکست می‌خورد؛ این شکست محیطی است نه نقص مخزن (CI در `.github/workflows/dotnet.yml:142` آن‌ها را نصب می‌کند).

**نگاشت پوشش به موارد خواسته‌شده:**

| مورد | تست |
|---|---|
| `sp_rename` و Pin | `D1_PinnedRename_MovesOnlyColumnAndIndexNames_AndKeepsEveryConstraint`، `D1_ThePinnedRenameScripts_ContainNoDropOrCreateOfConstraintsOrIndexes`، `RenameColumn_Up_…`، `TheRenameScripts_ContainNoDropOrCreate…` |
| Up/Down واقعی | `D2_UpThenDown_RestoresTheExactStructureIdentityAndData`، `RenameIndex_Up_AndDown_…` |
| FK مرکب با ستون nullable | `D3_CompositeForeignKeyWithANullableTenantId_…` |
| R1 (Rename بدون Pin) | `D5_R1_TheUnpinnedRename_IsGeneratedAsDropAndAdd…`، `D5_R1_WithoutADependentForeignKey_…` |
| NOT NULL با Guid | `D4_TheManualNotNullMigration_ThrowsWhileNullRowsExist…`، `…SucceedsOnlyAfterTheDataIsComplete…`، `D4_AlterColumnToNotNull_IsRefused…`، `D4_AnEfGeneratedAlterColumnNeverFillsNulls…` |
| شکست Core/Trade | `CoreFailure_TradeIsNeverStarted_…`، `TradeFailure_IsNotSuccess_AndARerunContinuesFromTheFailedStep` |
| اجرای مجدد Migration | `CoreThenTrade_WithExplicitRequest_UsesSeparateHistories_AndRerunRepeatsNothing` |
| همزمانی Migration | `SecondConcurrentRun_IsRefused_AndTheLockIsFreeAfterTheFirstRunEnds`، `LockIsReleased_WhenAStepFails` |
| رفتار Rename و Constraintها | `D1b_…FilteredIndex…`، `D1c_…KeptLegacyConstraintNames…`، `NegativeControl_ADropAndCreate…` |
| ایزوله‌بودن تست از دیتابیس عملیاتی | `D6_ATargetThatIsNotADisposableTestServer_IsRefused…`، `D6_TheTestDatabase_HasADedicatedName_AndIsDroppedEvenWhenTheTestFails` |

**نتیجه: G3 = Verified** برای مجموعهٔ فرض‌های U1..U5 (S3-0g). این **اجازهٔ اجرای Migration نیست** و فقط درستی رفتار SQL Server را برای عملیات‌های پیشنهادی نشان می‌دهد. هیچ شکافی در پوشش یافت نشد؛ تستی اضافه نشد. هیچ Migration عملیاتی یا Migration مشتری اجرا نشد.

## ۳. مرحلهٔ ۴ — G6 و SP-1..SP-9

G6 فقط **فعال‌سازی Tenant دوم در Shared Database** را متوقف می‌کند؛ مانع تست‌ها یا اصلاحات مستقل غیرمخرب نیست. منبع فهرست: `ISSUE-397-s3-0h-…` بخش ۹. شواهد از Source و اجرای امروز.

| SP | وضعیت فعلی (شواهد) | تست موجود / اجراشده | شکاف واقعی | وابسته به Tenant در Core | وابسته به تصمیم مالک |
|---|---|---|---|---|---|
| SP-1 Role.Admin و مرز Platform Admin | ۳۱ صفحهٔ `Pages/Admin/**` با `[Authorize(Roles=Admin)]` + بازبینی نقش از DB؛ `AuthorizeHrScopeAsync` مدیر نصب را مستقیم عبور می‌دهد | `AdminRoleRevalidationHttpTests`، `InstallationAdminAudit*Tests` (اجرا شد، سبز) | تعریف رسمی Platform Admin و محدودکردن آن به اپراتور اعمال نشده؛ تست «Admin از Tenant عبور نکند» ممکن نیست | **بله** | OD-1 (باز) |
| SP-2 ساخت کاربر/نقش/عضویت | `UserCreationValidation` مشترک؛ `CompanyUserProvisioningEndpoints` نقش را به همان شرکت قفل می‌کند؛ `Pages/Admin/Users/Create.cshtml.cs:206` اجازهٔ انتخاب `RoleId` را به مدیر نصب می‌دهد | `CompanyUserProvisioning*Tests` و `*SqlServerTests` (سبز) | نقش سراسری برای کاربر مشتری در Razor ممنوع نشده؛ Username Tenant-scoped (D-118) پیاده نیست | بله | OD-1، D-118 (Accepted، NotImplemented) |
| SP-3 Export و `SecurityAuditAdministrationEndpoints` | تنها Export CSV ممیزی؛ مدیر نصب، Anti-forgery، بازهٔ الزامی، ممیزی؛ فیلتر اختیاری `TenantId == companyId` | `SecurityAuditAdministrationEndpointTests`، `…SqlServerTests` (سبز) | فیلتر Tenant و طبقه‌بندی system/tenant/company (OD-5) تعریف نشده | بله | OD-5 (G4) |
| SP-4 کشف عضویت (`CustomerBoundaryId`) | درخواست عضویت فقط در همان `CustomerBoundaryId` و fail-closed | `CompanyMembershipRequestEndpointTests`، `…SqlServerTests` (سبز) | شرط «Tenant مشترک» ممکن نیست | بله | OD-3 |
| SP-5 `TenantId` استخراج‌شده از سرور در `CompanyAccessService`/`WorkContextService`/`DelegationCeilingService`/`TradeAuthorizationAdapter` | `TenantId` همه جا از context سرور؛ ورودی کلاینت و Claim جعلی رد می‌شود؛ `TradeAccessContext(UserId, TenantId, FiscalPeriodId, AllowsWrites)` | `CompanyAccessTests` (Claim جعلی)، `WorkContextTests`، `TradeAuthorizationAdapterTests`، `TradeAccessHttpTests` (سبز) | زوج `(TenantId, CompanyId)` در `TradeAccessContext` وجود ندارد (N-5a)؛ `TenantId` همان `Company.Id` است | بله | **O-3** (N-5a: داخل T1 یا PR جدا؛ PR جدا حالت میانی نامعتبر می‌سازد) |
| SP-6 HR و Directory | فیلتر Company در هر Handler؛ مدیر نصب عبور می‌کند | تست‌های HR و `HrOrganizationalStructureSqlServerTests` (سبز) | مقایسهٔ Tenant ممکن نیست | بله | — |
| SP-7 Provisioning چندمشتری (OD-14) | `CustomerProvisioningApplyService` تنها مسیر ساخت Company؛ `TenantId` از `Company.Id`؛ نگاشت `CustomerReference→Tenant` نیست | `CustomerProvisioning*SqlServerTests` (سبز) | رد «Tenant ناشناخته» و شناسهٔ نصب پیاده نیست | بله | D-093 (Proposed)، D-092 |
| SP-8 تست‌های منفی نشت cross-Tenant (API، cookie، export، audit، فایل، job) | تست‌های cross-**Company** برای هر چهار مسیر هست؛ Job تجاری/Tenant-level در Server وجود ندارد | اجرای امروز سبز | هیچ تست «Tenant مستقل» نمی‌تواند نوشته شود | بله | — |
| SP-9 یکتایی سراسری `Company.NationalId` | Unique Index فیلتردار روی `Companies.NationalId` (S3-0d-b بند ۱۴.۵) | `DatabaseModelTests` (سبز؛ پوشش این Index به‌تنهایی بازبینی نشد) | تصمیم OD-7 (دامنهٔ یکتایی در Shared) | بله | OD-7 |

**شکاف قابل اصلاح بدون Schema و بدون تصمیم جدید:** **هیچ شکاف مستقلی پیدا نشد** که بدون Tenant یا تصمیم مالک قابل بستن باشد. `PendingDatabaseMigrationCheckService` و `DatabaseTopologyStartupSnapshot` داده Company را لمس نمی‌کنند. تغییر گستردهٔ Authorization پیشنهاد یا انجام نشد.

## ۴. مرحلهٔ ۷ — کارهای بازرگانی خارجی به سه گروه

هر قابلیت جدا بررسی شد (مراحل ۶ تا ۱۳ یک‌جا Blocked فرض نشدند).

### گروه ۱ — قابل اجرا همین حالا (بدون Tenant، جدول و تصمیم باز)
| کار | دلیل | توضیح |
|---|---|---|
| Domain خالص Proforma/Revision با `CompanyId` صریح و بدون EF/Store | `docs/handoffs/ISSUE-UNNUMBERED-trade-proforma-domain-design.md` گزینهٔ ۱؛ D-116 و D-119/D-120 Accepted؛ همانند #772/#705 | **فقط** قواعد Accepted: Revision بدون mutator، قفل کالا، تمدید با Revision. **خارج از این PR:** P-4 (انقضا) و P-5 (Replace) تا تصمیم مالک؛ فرمول مالی تصمیم‌نشده. پیشنهاد: PR مستقل، با تأیید اینکه N-4 توسط D-120 بسته شده است |
| تست‌های غیرمخرب | — | هیچ شکاف جدیدی در Party/Product یا G3 نیست |

> **اصلاح تاریخ‌دار (2026-11-25؛ ردیف بالا بدون تغییر حفظ شده):** عبارت «تمدید با Revision» در ردیف Domain خالص Proforma با D-116 بند ۲ (`Accepted`) ناسازگار است: تمدید صرف تاریخ اعتبار یک رویداد تاریخچه‌ای مستقل است و Revision جدید **نمی‌سازد**؛ فقط تغییر شرایط تجاری Revision می‌سازد (D-116 بند ۳). کار جدید (پس از پذیرش N-4 گزینهٔ ۱) از D-116 پیروی کند.

### گروه ۲ — قابل طراحی، غیرقابل پیاده‌سازی (طراحی Accepted ولی جدول/Migration می‌خواهد)
Party BankAccount/ProductIdentifier (فعال‌سازی Migration)، ImportCase، ActivityProfile، Proforma/OrderRegistration (D-104 یک‌به‌یک)، Vessel/Voyage/Allocation، BoL، Customs، Permit، Forex، Facility/Guarantee/Cheque، Documents/Insurance، Cost allocation، Reports/Dashboard. وابستگی مشترک: Core Tenant (D-121) + Schema مصوب (D-120). قرارداد API و معیار پذیرش هر یک در اسناد `TRADE_S3_ARCHITECTURE.md` و طراحی Proforma هست؛ در این PR چیزی دوباره‌نویسی یا Entity نمایشی ساخته نشد.

### گروه ۳ — Blocked
| قابلیت | علت | تصمیم لازم |
|---|---|---|
| Core Tenant، `Companies.TenantId`، Backfill | G0، G5، D-092، D-094 | بخش ۵.B و ۵.C و ۵.D |
| Migration Trade (Rename فیزیکی + `TenantId` واقعی) | Core Tenant؛ O-1..O-4؛ D-091 | بخش ۵.E |
| `TradeAccessContext` دو‌شناسه‌ای | O-3 (N-5a) | بخش ۵.E |
| Entitlement (#401) | زنجیرهٔ Provisioning/License آماده نیست؛ `DenyAllTradeModuleEntitlement` فعال می‌ماند | — |
| حذف/غیرفعال‌سازی Party/Product | D-083 `NeedsApproval` | تصمیم مالک |
| Proforma: انقضا و Replace | P-4، P-5 | تصمیم مالک |
| فرمول مبلغ، تلورانس، دقت اعشار | تصمیم‌نشده | تصمیم مالک |
| Excel Import | `Deferred` (D-106 `Proposed`) | بعد از تکمیل Trade |

## ۵. مرحلهٔ ۶ — بسته تصمیم مالک (فقط موارد واقعاً باز)

D-089، D-090، D-118، D-119، D-120 و D-121 دوباره مطرح نشدند.

### A) G0 و S3-0h
**پیشنهاد:** طرح S3-0h به‌عنوان **طرح مرجع پیاده‌سازی** تأیید شود؛ جزئیات آن `Proposed` می‌ماند. گیت G0 تنها با تأیید D-092 و D-094 بسته می‌شود.
**سؤال:** آیا S3-0h مرجع پیاده‌سازی باشد؟ (بله/خیر، یا اصلاحات نام‌برده).

### B) D-092 / OD-13 — مرجعیت Manifest
**پیشنهاد (کم‌هزینه و قابل حسابرسی؛ `Proposed`):** Manifest تأییدشدهٔ مالک تنها مرجع نگاشت است.

| ویژگی | پیشنهاد |
|---|---|
| قالب | JSON با نسخهٔ Schema؛ فهرست ردیف‌های `{ companyId, tenantId }`؛ شناسهٔ نصب؛ تاریخ؛ هویت تأییدکننده |
| اعتبارسنجی | هر `Company.Id` موجود دقیقاً یک بار؛ `Company` ناشناخته یا بدون نگاشت ⇒ توقف کامل؛ نگاشت تکراری/مبهم ⇒ توقف |
| بازتخصیص | هر Company با Tenant قبلی دوباره به Tenant دیگر نگاشت نمی‌شود |
| Hash و نسخه | SHA-256 روی بایت‌های Manifest؛ نسخهٔ تأییدشده بایگانی می‌شود |
| Dry-run | پیش‌فرض؛ فقط گزارش اختلاف و شمار؛ بدون نوشتن؛ نتیجه ثبت می‌شود |
| تکرار امن | اجرای مجدد با Manifest یکسان بی‌اثر |
| ممنوع | استنتاج از `CustomerBoundaryId`، CorporateGroup، نام، دامنه یا مالکیت |

**اثر:** نگاشت دستی‌تر و کندتر ولی بدون ریسک نشت دائمی. **سؤال:** قالب و مرجع بودن Manifest تأیید می‌شود؟ (تأیید/اصلاح قالب).

### C) D-094 / OD-17 — سیاست Migration Trade

| گزینه | هزینه/ریسک | نصب بدون Entitlement | اثر شکست Migration |
|---|---|---|---|
| H1 همیشه Trade | Schema خالی برای نصب بدون مجوز | جدول Trade ساخته می‌شود | شکست Trade همه را متوقف می‌کند |
| **H2 opt-in `--migrate --include-trade` (پیشنهادی، در #665 پیاده)** | کم؛ نیاز به رویهٔ عملیاتی | Trade اجرا نمی‌شود (Fail-closed) | Core دست‌نخورده؛ Trade با اجرای مجدد ادامه می‌یابد |
| H3 مشروط به Entitlement | امروز ممکن نیست (`DenyAll`)؛ Licensing↔Migration وابسته می‌شود | — | — |
| H4 فقط ابزار دستی/CI | مستعد خطای انسانی | — | — |

**سؤال:** H2 تا اتصال Entitlement پذیرفته شود؟ (H2 / گزینهٔ دیگر).

### D) G5 — Backup، Restore و Manifest بایگانی‌شده
رویهٔ لازم پیش از هر اجرا روی دیتابیس مشتری: (۱) Backup کامل تأییدشده با `RESTORE VERIFYONLY`؛ (۲) رویهٔ بازیابی مستند و در محیط آزمایشی تمرین‌شده؛ (۳) Manifest تأییدشده و Hash بایگانی‌شده برای هر نصب؛ (۴) مجوز عملیاتی جدا و صریح برای هر نصب. در این مأموریت هیچ Backup/Restore روی دیتابیس مشتری اجرا نشد. **سؤال:** پذیرش این چهار شرط به‌عنوان تعریف G5؟

### E) G4 و N-5a/N-5b

| مورد | ماهیت | وضعیت | سؤال دقیق |
|---|---|---|---|
| OD-11 | تصمیم گرفته‌شده توسط D-090 | بسته | — |
| OD-12 / OD-18 | تصمیم (ترتیب اجرایی؛ جهت Rename توسط D-120 تعیین شد) | D-091 `Proposed` | ترتیب Core→Trade و پنجرهٔ کوتاه تأیید شود؟ |
| OD-4 (`CompanyRole` Company-scoped) | تصمیم | باز | تأیید وضع فعلی؟ |
| OD-5 (`SecurityAuditEvents`: هر دو null = سیستمی) | تصمیم | باز | تأیید؟ |
| OD-10 | با D-120 جایگزین شد (بدون `HasColumnName`) | بسته به D-120 | — |
| O-1 (رساندن نگاشت به Trade) | تصمیم | باز | گزینه‌ها و پیشنهاد در `ISSUE-UNNUMBERED-trade-tenant-company-o1-o4-analysis-2026-11-23.md` |
| O-2 (یکتایی Company-scoped) | تصمیم | پیشنهاد: Company-scoped | تأیید؟ |
| O-3 (N-5a) | تصمیم | باز | داخل T1، یا PR جدا (حالت میانی نامعتبر)؟ |
| O-4 (رکورد بدون نگاشت) | تصمیم | پیشنهاد: توقف کامل + Manifest دستی | تأیید؟ |
| N-5b | تصمیم (`NeedsApproval`) | باز | انتخاب «فقط شرکت» در WorkContext یا Route؟ |

> **یادداشت تکمیلی (2026-11-25، هم‌راستاسازی پس از همگام‌سازی شاخهٔ S0؛ جدول بالا بدون تغییر حفظ شده):** تصمیم‌های صریح مالک در یادداشت وضعیت D-090 (`docs/DECISIONS.md`) و `ISSUE-UNNUMBERED-trade-s0-owner-decisions-2026-11-24.md` ثبت شده‌اند: **O-3 = گزینهٔ A `Accepted`** (فقط داخل PR اتمیک T1؛ مجوز شروع T1 نیست)؛ **N-5a-type فاز اول `Accepted`** (نوع مستقل از دوره؛ بدون تغییر Contextهای موجود)؛ **N-5b همچنان `NeedsApproval`**. سایر ردیف‌ها تغییری نکرده‌اند.

## ۶. دستورات و نتایج (خلاصه)
- SQL Server آزمایشی: `scripts/dev/test-sqlserver.sh up` / `down` (حذف شد؛ رمز چاپ نشد).
- `./Template.Tests -noColor` (کل مجموعه) ← ۱۶۲۳ کل، ۱۶۱۳ موفق، ۰ ناموفق، ۱۰ Skip، ۰ NotRun.
- `-class` برای شش کلاس G3 ← جدول بخش ۲.
- `python3 scripts/tests/test_migration_guard_*.py`، `test_migration_tooling.py` ← OK پس از نصب `tree-sitter`.
- **اجرانشده:** Vitest/ClientApp، E2E، `dotnet build` روی شاخهٔ جدید (فقط مستندات تغییر کرد)، هر Migration روی دیتابیس مشتری.

## ۷. ریسک‌ها و کارهای باقی‌مانده
- S3-0 همچنان بسته نیست؛ Schema/Backfill بدون مجوز صریح انجام نمی‌شود.
- ریسک: پذیرش H2 بدون Entitlement یعنی Trade در نصب‌های تولیدی ساخته نمی‌شود تا مرحلهٔ بعد.
- بعدی: تصمیم مالک روی A تا E؛ سپس Core Tenant (D-121).
