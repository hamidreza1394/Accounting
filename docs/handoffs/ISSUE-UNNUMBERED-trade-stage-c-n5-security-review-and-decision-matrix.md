# تحویل وظیفه — ISSUE-UNNUMBERED-trade-stage-c-n5-security-review-and-decision-matrix

- **Issue:** UNNUMBERED (ادامهٔ Issue #397؛ مرحلهٔ C پس از PR #764)
- **Agent/مسئول:** Claude (Lead توسعهٔ Trade)؛ فقط Trade؛ Vendor، Licensing و فایل‌های GLM دست‌نخورده
- **تاریخ:** 2026-11-23
- **Commit مبنا:** `560ff2890be8bbf87795f30dd85f4eaf3cd1f131` (`origin/main`، دریافت‌شده در همین جلسه)
- **وضعیت:** C-1 و C-2 انجام‌شده (مستنداتی)؛ C-3 **مسدود** (علت در بخش ۵)
- **قاعدهٔ برچسب‌ها (D-030):** `Accepted` = تصمیم مصوب در ADR یا تصمیم صریح مالک؛ `Proposed` = پیشنهاد این سند؛ `NeedsApproval` = نیازمند تصمیم مالک؛ `Blocked` = وابسته به گیت یا تصمیم باز؛ `NotImplemented` = در کد نیست. هیچ `Proposed` به `Accepted` تبدیل نشده است.

## هدف و معیارهای پذیرش
- [x] ممیزی اولیه: SHA واقعی main، working tree، PRهای باز، شاخه‌ها.
- [x] C-1: بازبینی امنیتی مستقل و فقط‌خواندنی N-5 با ماتریس کنترل‌ها و تست‌های منفی؛ بدون تغییر `TradeAccessContext` و `TradePeriodScope`.
- [x] C-2: ماتریس تصمیم‌های Proforma با پیشنهاد فنی، مزایا، معایب و هزینه.
- [ ] C-3: برش Domain-only — **انجام نشد**؛ دلیل دقیق در بخش ۵ (طبق مأموریت: توقف و گزارش، نه کد صوری).

## ۱. ممیزی اولیه

| مورد | نتیجهٔ واقعی |
|---|---|
| `origin/main` | `560ff289` (docs(vendor): sync WORK_LOG and INDEX ... #763)؛ working tree تمیز |
| PR #764 | merged (طراحی دامنهٔ Proforma) |
| PR #765 | **Draft، ادغام‌نشده** (`docs/fiscal-period-data-ownership-carryover`، ۶ فایل: `DATABASE.md`، `DECISIONS.md`، `FISCAL_PERIOD_DATA_MODEL.md`، `INDEX.md`، `WORK_LOG.md`، handoff). به هیچ‌کدام دست نزدم. محتوای شاخهٔ آن را فقط خواندم |
| PR #766 | باز، غیر Draft: `docs(vendor): reconcile status against main 560ff289`؛ مربوط به Vendor؛ دست نزدم |
| شاخهٔ `feat/trade-s2-4-product-slice` | در Remote وجود ندارد؛ چیزی برای Diff یا Merge نبود |

**وابستگی معماری به #765:** تصمیم‌های ۹ تا ۱۹ مالک (بخش ۳ مأموریت) در این سند «ورودی طراحی» هستند. متن مرجع آن‌ها (D-117 و `FISCAL_PERIOD_DATA_MODEL.md`) تا ادغام #765 در main نیست و هیچ وضعیت `Accepted` برای D-117 ادعا نمی‌شود. هر پیشنهاد این سند که به D-117 وابسته است با «پس از ادغام #765» علامت خورده است.

**تنش مستند که باید ثبت شود:** D-086 §7 «ثبت دورهٔ افتتاح روی ImportCase» را از D-063 supersede کرده است؛ تصمیم ۹ مالک `OriginFiscalPeriodId` را به‌عنوان فیلد تاریخی، اجباری و تغییرناپذیر برمی‌گرداند (بدون `TradeOperationalPeriod` و بدون اثر مجوزی). این دو متناقض نیستند ولی D-117 باید صراحتاً بگوید که فیلد جدید «دورهٔ عملیاتی» نیست. `NeedsApproval` برای ADR شفاف‌ساز، بدون ویرایش بی‌صدای D-086.

## ۲. C-1 — بازبینی امنیتی N-5

### ۲.۱ روش و محدودیت‌ها
- `security-reviewer` مخزن (`.claude/agents/security-reviewer.md`) در فهرست Agentهای این جلسه **در دسترس نبود**. جایگزین: یک Agent مستقل با مدل opus، با همان نقش و چک‌لیست، دستور صریح فقط‌خواندنی و بدون اجرای تست. پس از پایان، `git status` تمیز بود.
- گزارش Agent شاهد نیست؛ ادعاهای کلیدی را Lead در کد راستی‌آزمایی کرد (ردیف «راستی‌آزمایی»).
- `architect` مخزن نیز در جلسه در دسترس نبود؛ بازبینی معماری جداگانه انجام نشد.

### ۲.۲ پاسخ کوتاه
می‌توان برای عملیات پروندهٔ مستقل از دوره یک Context بدون `FiscalPeriodId` داشت، بی‌آنکه هیچ کنترل دیگری ضعیف شود، به این شرط‌ها: نوع Context **جدا** باشد (نه nullable کردن `FiscalPeriodId`)، و فقط شرط دوره برداشته شود. N-5 دو مسئلهٔ جدا است:

- **N-5a (وابستگی نوعی):** سرویس‌ها `FiscalPeriodId` و `AllowsWrites` را در `TradeAccessContext` می‌بینند. رفع آن بی‌خطر و مستقل از تغییر رفتار است. `Proposed`.
- **N-5b (وابستگی در resolve):** Context فقط وقتی ساخته می‌شود که یک دورهٔ قابل‌انتخاب انتخاب شده باشد. رفع آن منبع انتخاب شرکت می‌خواهد و جریان WorkContext را تغییر می‌دهد. `NeedsApproval`.

### ۲.۳ راستی‌آزمایی Lead (شواهد در کد main@560ff289)
| ادعا | شاهد | نتیجه |
|---|---|---|
| Context بدون دورهٔ انتخاب‌شده ساخته نمی‌شود | `WorkContextService.cs` (`requestedFiscalPeriodId == Guid.Empty` ← `PeriodNotFound`؛ دورهٔ غیرقابل‌انتخاب ← `PeriodNotSelectable`)؛ `WorkContextCookie.cs` (`payload.FiscalPeriodId == Guid.Empty` ← null)؛ `WorkContextEndpoints.cs` (`SelectContextRequest(CompanyId, FiscalPeriodId)`) | تأیید شد |
| `PeriodIndependent` فقط بلاک نوشتن در دورهٔ بسته را رد می‌کند | `TradeAccessGate.cs` (محاسبهٔ `periodIndependent` و شرط `PeriodReadOnly`) | تأیید شد |
| `TenantId` از `Company.CompanyId` پر می‌شود و Entitlement/Permission با همین مقدار سنجیده می‌شوند | `TradeAuthorizationAdapter.cs` (ساخت `TradeAccessContext`)، `TradeAccessGate.cs` (`IsEntitledAsync(current.TenantId)`) | تأیید شد (برابری legacy) |
| `HasPermissionForAllCompanyPeriodsAsync` فقط در تعریف و پیاده‌سازی هست و در `Server/` و `Modules/` Callerی ندارد | grep روی `Server` و `Modules` | تأیید شد؛ Entitlement-aware نیست |
| فرم `order-registration` و کد تمدید اعتبار Proforma در کاتالوگ نیست | `TradePermissionCatalog.cs` فقط `proforma` و `import-case` را دارد | تأیید شد |

### ۲.۴ ماتریس عملیات × کنترل
«ال» الزامی، «غ» غیرالزامی، «N.A.» کاربرد ندارد. «NI» = امروز Endpoint/Entity نیست (`NotImplemented`).

| عملیات | Session معتبر | عضویت/Company | Tenant | Permission صریح | Entitlement | FiscalPeriod معتبر و باز | مالکیت داده | وضعیت مجاز دامنه |
|---|---|---|---|---|---|---|---|---|
| مشاهدهٔ پرونده | ال | ال | ال (فعلاً legacy) | ال `trade.form.import-case.view` | ال | N.A.؛ **امروز ناخواسته الزامی** | ال (NI) | غ |
| ایجاد/ویرایش پرونده | ال + Anti-forgery + JSON | ال | ال | ال `create/update` + `view` | ال | N.A.؛ نبود دورهٔ انتخاب‌شده امروز مسدود می‌کند | ال (NI) | ال (NI) |
| ایجاد/اصلاح Proforma | ال | ال | ال | ال `proforma.create/update` + `view` | ال | N.A. (D-086 §7) | ال؛ `ImportCaseId` همان Company | ال (NI) |
| تمدید اعتبار Proforma | ال | ال | ال | ال؛ **کد مشخص ندارد** | ال | N.A. | ال | ال؛ رو به جلو (A-2) و Maker-Checker (N-8) |
| ثبت OrderRegistration | ال | ال | ال | ال؛ **فرم در کاتالوگ نیست** | ال | N.A. | ال | ال؛ P-4، P-5، D-104 |
| عملیات مالی وابسته به دوره | ال | ال | ال | ال (کد خودش) | ال | **ال**: متعلق به همان Company؛ Open برای Write؛ Archived غیرقابل‌ورود | ال | ال |

اصل غیرقابل مذاکره: در همهٔ سطرهای پروندهٔ مستقل از دوره، فقط ستون «FiscalPeriod» برداشته می‌شود.

### ۲.۵ یافته‌ها
| # | شدت | نوع | شرح |
|---|---|---|---|
| F-A | Medium | شکاف طراحی (N-5b) | عملیات «مستقل از دوره» عملاً به دورهٔ انتخاب‌شده وابسته است. شرکت بدون دوره، همهٔ دوره‌ها Archived، یا دورهٔ Archived/حذف‌شده پس از انتخاب ⇒ 409 `trade_context_required` حتی برای Party/Product. Fail-closed است (نشتی ندارد) ولی با D-086 §7.1–7.2 ناسازگار است |
| F-B | Medium | شکاف طراحی (N-5a) | `TradeAccessContext` به همهٔ سرویس‌ها `FiscalPeriodId` می‌دهد. اگر سرویس ImportCase/Proforma آن را برای `OriginFiscalPeriodId` بخواند، دورهٔ دلخواه کاربر در پرونده ثبت می‌شود و پرونده عملاً period-scoped می‌شود. کنترل فعلی فقط تست allowlist برای Party/Product است، نه کنترل نوعی |
| F-C | Low | انحراف نهفته از D-087/D-105 | Context فقط `TenantId` دارد (مقدار `CompanyId`)؛ Entitlement با `IsEnabledForCompanyAsync(tenantId)` صدا می‌شود. با جدا شدن Tenant از Company رفتار نادرست محتمل است (**حدس**، وابسته به Adapter واقعی Entitlement) |
| F-D | Low | شکاف تست | هیچ تستی ثابت نمی‌کند Entitlement با شناسهٔ همان شرکتِ Context ارزیابی می‌شود (Fake ها آرگومان را نادیده می‌گیرند یا همیشه true می‌دهند) |
| F-E | Low | شکاف تست | تست‌های HTTP/IDOR Trade روی InMemory اجرا می‌شوند، نه SQL Server؛ تست Trade-محور برای نشست Revoke/Idle و دورهٔ Archived نیست |
| F-F | Info | کاتالوگ مجوز | فرم `order-registration` و مجوز تمدید اعتبار تعریف نشده‌اند؛ افزودن = تغییر کاتالوگ و seed |
| F-G | Info | طراحی | `HasPermissionForAllCompanyPeriodsAsync` به Entitlement توجه نمی‌کند؛ برای گزارش چندشرکتیِ پرونده‌ها نباید استفاده شود |
| F-H | Info | — | ترتیب Gate (دوره قبل از Entitlement) با D-074 §3 هم‌خوان است؛ فقط وضعیت بسته‌بودن دوره، که کاربر از قبل می‌داند، آشکار می‌شود |

**تعارض ADRها:** تعارض اصولی نیست. D-074 §3.3 مرحلهٔ دوره را «در عملیات period-scoped» قید می‌زند و D-086 §7 دورهٔ این اسناد را از D-063 supersede کرده است؛ ولی متن D-063 §5 قید ندارد. طبق D-030 باید ADR شفاف‌ساز ثبت شود و متن قدیمی بی‌صدا ویرایش نشود.

### ۲.۶ توصیهٔ فنی (`Proposed`، اجرا نشده)
**فاز ۱ — N-5a (پیش از مرحلهٔ C، بدون تغییر رفتار دسترس‌پذیری):**
- نوع جدید `TradeCompanyAccessContext(Guid UserId, Guid TenantId, Guid CompanyId)` بدون `FiscalPeriodId` و `AllowsWrites`.
- در `ITradeAuthorization`: `ResolveCurrentCompanyContextAsync` و `HasCompanyPermissionAsync`؛ در `TradeAccessGate`: overload جدا `AuthorizeCompanyScopedAsync` با ترتیب نشست → Context شرکت → Entitlement(CompanyId) → Permission → AdditionalRead.
- متادیتای جدا (مثلاً `RequireTradeCompanyPermission`) که با `RequireTradePermission` هم‌زمان نمی‌آید.
- منبع resolve در این فاز همان `WorkContextService.GetCurrentAsync` است؛ Adapter فقط دوره را به Application نمی‌دهد.
- `TenantId`/`CompanyId` از `OwnershipScope.FromLegacyCompany` گرفته شوند، نه تکرار برابری legacy.

**نباید تغییر کند:** `TradeAccessContext` و `TradePeriodScope` برای عملیات دوره‌ای؛ `FiscalPeriodId` nullable نشود؛ بلاک `AllowsWrites`؛ اتصال Context به همان درخواست (کلید خصوصی جدا برای نوع جدید)؛ Anti-forgery و content-type؛ `DenyAllTradeModuleEntitlement` و fail-closed؛ پاسخ یکسان 404 برای شناسهٔ شرکت دیگر؛ محدود بودن دسترسی به port به Gate و Adapter.

**چرا کنترلی ضعیف نمی‌شود:** همهٔ شرط‌های D-074 §2 جز «FiscalPeriod در صورت کاربرد» از همان مسیرها می‌گذرند. جدایی نوع‌ها در compile-time تضمین می‌کند عملیات مالی دوره‌ای با Context بدون دوره مجاز نشود، نه با یک پرچم bool.

**فاز ۲ — N-5b (`NeedsApproval`):** (الف) انتخاب «فقط شرکت» در WorkContext (cookie با `FiscalPeriodId` خالی، bound به UserId و SessionId، اعتبارسنجی دوباره در هر درخواست، Anti-forgery و Audit) — سازگارتر با D-062 ولی جریان D-031 را تغییر می‌دهد؛ (ب) شرکت از Route/ورودی هر درخواست با `CompanyAccessService.FindAccessibleCompanyAsync` و 404 یکسان (D-013).

### ۲.۷ تست‌های منفی پیشنهادی (هیچ‌کدام نوشته یا اجرا نشده‌اند)
پوشش «موجود» بر پایهٔ نام تست در کد است؛ تست‌های HTTP روی InMemory اجرا می‌شوند.

| سناریو | پوشش موجود | تست پیشنهادی (لایه) |
|---|---|---|
| کاربر بدون عضویت شرکت مقصد | `RevokedMembershipResolvesNull`، `CookieForForeignCompanyOrPeriodResolvesNull`، `RevokedMembershipAfterSelectionGets409` (برای Context دوره‌دار) | `CompanyContext_RevokedOrInactiveMembershipOrCompanyOrRole_ResolvesNull` (Unit)؛ `CompanyScopedRoute_MembershipRevokedAfterSelection_Gets409AndNoData` (Integration) |
| Permission اشتباه | `MissingPermissionIsForbidden`، `IntentMismatchIsForbiddenWithRealPermissionRules`، `ReadPermissionCannotAuthorizeAWriteIntent`، `EntitledUserWithoutPermissionGets403Forbidden` | `ImportCaseCreate_WithProformaCreateOnly_Is403TradeForbidden` (Integration)؛ `CompanyPermission_FromAnotherCompanyRole_IsNotHonoured` (Unit) |
| Entitlement نامعتبر/خطا/Throw | `DisabledEntitlementIsModuleUnavailable...`، `ThrowingEntitlementIsModuleUnavailable...`، `ProductionDenyAllEntitlementGets403ModuleUnavailableEvenWithPermission` | `Gate_PassesTheContextCompanyIdToEntitlement` (Unit؛ رفع F-D)؛ `Gate_CancelledTokenPropagatesOperationCanceled` (Unit)؛ نسخهٔ همهٔ موارد برای overload شرکت‌محور |
| Session منقضی/Revoke | فقط عمومی (`SessionValidatorTests`، `UserSessionEndpointTests`) و `AnonymousGets401` | `TradeRoute_AfterUserSessionRevoked_Gets401`؛ `TradeRoute_IdleExpiredSession_Gets401` (Integration؛ نسخهٔ SQL Server هم مطلوب) |
| شناسهٔ پروندهٔ شرکت دیگر | فقط Party/Product: `OtherTenantPartyIsInvisible...404` و معادل Product (InMemory) | `ImportCase_GetUpdateOfOtherCompanyCase_Same404NoBodyAndUnchanged_SqlServer`؛ `Proforma_CreateUnderOtherCompanyImportCaseId_Rejected_SqlServer`؛ `ImportCase_SameTenantDifferentCompany_IsIsolated` — **قابل نوشتن نیست** تا گذار D-087 (برابری legacy)؛ باید `NotRun` با دلیل ثبت شود |
| عملیات مالی در دورهٔ بسته | `ReadOnlyPeriodDeniesWriteBeforeEntitlementAndPermission`، `DefaultAndPeriodDependentWritesAreStillBlockedInAClosedPeriod`، `ClosedPeriodWriteGets409ReadOnly` | `CompanyContext_CannotAuthorizePeriodScopedOperation` (Architecture)؛ `ArchivedPeriodSelection_TradeGets409` (Integration) |
| پروندهٔ مستقل از دوره با همهٔ کنترل‌های دیگر | فقط Party/Product با پرچم | `ImportCase_WritableInClosedPeriodWithAllControls` (Integration؛ `FiscalPeriodId` ذخیره نمی‌شود)؛ `ImportCase_NoSelectablePeriod_CurrentBehavior409` (مستندسازی رفتار امروز)؛ پس از فاز ۲ `ImportCase_NoPeriodCompanyContext_AllowedWithAllControls`؛ `ImportCaseService_DoesNotReadFiscalPeriod` (Architecture) |

### ۲.۸ تصمیم‌های لازم برای C-1 (`NeedsApproval`)
1. تأیید فاز ۱ (نوع جدا و overload جدا در Gate).
2. تأیید یا رد فاز ۲ و انتخاب منبع (الف یا ب).
3. ثبت ADR شفاف‌ساز که مرحلهٔ دورهٔ D-063 §5 را «در صورت کاربرد» می‌کند، بدون ویرایش متن قبلی.
4. منبع `OriginFiscalPeriodId` هنگام ایجاد: از دورهٔ WorkContext (در فاز ۱ ممکن است) یا باید چه شود وقتی Context بدون دوره است (فاز ۲)؟ تصمیم ۹ مالک «اجباری» است، پس در Context بدون دوره باید منبع صریحی تعریف شود (مثلاً انتخاب دورهٔ مبدأ توسط سرور بر پایهٔ تاریخ) — **حدس نشده است**.
5. انتقال Party/Product از پرچم `PeriodIndependent` به Context شرکت یا ماندن وضع فعلی (D-085 هنوز `Proposed`).
6. کد مجوز تمدید اعتبار و افزودن فرم `order-registration` به کاتالوگ.
7. scope گزارش چندشرکتی پرونده‌ها (Company-only) و جایگزین Entitlement-aware برای `HasPermissionForAllCompanyPeriodsAsync`.
8. رفتار Trade وقتی همهٔ دوره‌ها Archived هستند (سیاست Archived هنوز `NeedsApproval`).

## ۳. تصمیم‌های قطعی مالک که به‌عنوان ورودی استفاده شد (بدون سؤال مجدد)
ردیف‌های ۱ تا ۱۹ بخش ۳ مأموریت: Proforma چندکالایی با HS Code متفاوت؛ تمدید صرف = رویداد بدون Revision؛ تغییر شرایط تجاری = Revision؛ Product در Trade؛ پرونده مستقل از دورهٔ عملیاتی و بدون `TradeOperationalPeriod`؛ `OriginFiscalPeriodId` اجباری، تاریخی، تغییرناپذیر و بدون اثر مجوزی؛ `FiscalPeriodId` فقط برای اسناد/عملیات واقعاً دوره‌ای؛ پیش‌فرض ورود پرونده‌های Open به فهرست سال جدید و خروج/ورود مجدد توسط مدیر مجاز؛ Audit و عدم بازنویسی تصمیم مدیر در اجرای مجدد؛ محل پایدار Company-level در Company + FiscalPeriod DB؛ Snapshot بدون شناسهٔ دوم و بدون مرجع حقیقت دوم؛ بدون FK و تراکنش اتمیک بین دو دیتابیس؛ Registry فقط برای Routing؛ پرهیز از dual-write. وضعیت مستند: ورودی طراحی؛ متن مرجع در #765 (Draft).

## ۴. C-2 — ماتریس تصمیم‌ها

| موضوع | تصمیم قطعی | تصمیم باز | وابستگی | اثر بر مدل |
|---|---|---|---|---|
| A-1 محل تاریخ اعتبار | تمدید صرف = رویداد، بدون Revision (D-116 بند ۲) | ستون روی سرتیتر + رویداد، یا فقط روی Revision، یا مقدار اولیه روی Revision + مؤثر = آخرین رویداد | A-2، N-8 | `Proforma` یا `ProformaRevision`؛ نیاز به نوشتن ریشه با `RowVersion` در هر تمدید |
| P-4 انقضا | — | آیا Proforma منقضی «جاری» می‌ماند؟ آیا روی آن OrderRegistration ساخته می‌شود؟ | A-1، D-104 | `Expired` مشتق یا ذخیره؛ شاخص «جاری»؛ قاعدهٔ ثبت‌سفارش |
| P-5 Replace با OrderRegistration زنده | تا تصمیم، پیوند Revision ثبت‌سفارش جابه‌جا نمی‌شود (قید موقت D-086) | ممنوع، یا با Amendment، یا با ابطال ثبت‌سفارش | A-5، N-1، N-9 | تغییرناپذیری `BasedOnProformaRevisionId`؛ وضعیت‌های Proforma |
| A-3 فیلدهای سازندهٔ Revision | تغییر شرایط تجاری = Revision (D-116 بند ۳) | فهرست دقیق فیلدها؛ آیا ویرایش Draft پیش از صدور Revision می‌سازد | N-9، A-4 | محتوای Revision؛ تعارض با «Revision تغییرناپذیر» در Draft |
| N-5 | استقلال از دوره بدون حذف سایر کنترل‌ها | بخش ۲.۸ | Security Review (این سند)، تأیید مالک | نوع Context جدا؛ منبع `OriginFiscalPeriodId`؛ Schema را لمس نمی‌کند |
| N-7 ارجاع بین دیتابیس‌ها | محل پایدار Company-level؛ بدون FK و تراکنش اتمیک بین DBها؛ Registry فقط Routing (تصمیم‌های ۱۵، ۱۷، ۱۸) | قرارداد اعتبارسنجی سمت سرور برای ارجاع (وجود، مالکیت، وضعیت) و رفتار هنگام دسترس‌ناپذیری DB مبدأ؛ تضمین یکتایی Company-wide | پس از ادغام #765، D-115 | `ImportCaseId` در Proforma = شناسهٔ پایدار + اعتبارسنجی؛ قید یکتایی فقط در یک DB |
| OD-017 دقت و Rounding | — | دقت مالی/تناژ/درصد؛ MinorUnits ریال؛ فهرست ارزها (A-8) | تصمیم‌های ۱ تا ۳ PaymentPlan | ستون‌های decimal؛ `TradeRoundingPolicy` |
| PaymentPlan (۱) تخصیص باقی‌مانده | — | باقی‌مانده به مرحلهٔ آخر (فعلی) یا largest-remainder با tie-break | ADR جدید؛ PR #751 | مقدار بازسازی‌شدهٔ طرح؛ تست رد مبلغ ناهم‌خوان |
| PaymentPlan (۲) نمایش درصد مؤثر | درصد اسمی در ورودی حفظ می‌شود | فقط مبلغ، یا فیلد محاسباتی `EffectivePercent` | (۱) | فقط خروجی API/UI؛ بدون Schema |
| PaymentPlan (۳) واحد کوچک IRR | — | ۲ (فعلی)، ۰، یا پیکربندی به‌ازای شرکت | OD-017، A-8 | دقت مبالغ ذخیره‌شده |

### ۴.۱ پیشنهاد فنی، مزایا، معایب، هزینه (همگی `Proposed`)

**A-1.** پیشنهاد: ستون روی سرتیتر + رویداد برای هر تغییر.
- مزیت: پرس‌وجوی انقضا و شاخص ساده؛ مقدار جاری بدون Join.
- عیب: دو منبع (ستون و رویداد) که باید در یک تراکنش با `RowVersion` ریشه هم‌گام بمانند.
- هزینه: کم؛ یک ستون و یک نوع رویداد.
- جایگزین (ج): تک‌منبعی و بدون ستون تکراری، ولی «مقدار مؤثر» مشتق است و فیلتر انقضا گران‌تر می‌شود. گزینهٔ (ب) با D-116 در تضاد است.

**P-4.** پیشنهاد: `Expired` مشتق از تاریخ؛ ثبت‌سفارش جدید روی Proforma منقضی بدون تمدید رد شود.
- مزیت: تمدید بدون نوشتن وضعیت دیگر Proforma را زنده می‌کند؛ ساده و قابل‌آزمون.
- عیب: Proforma منقضی هنوز در «جاری» می‌ماند یا نه باید جدا مشخص شود (سقف قابل‌تنظیم پروفایل).
- هزینه: کم.

**P-5.** پیشنهاد: ممنوع تا ابطال ثبت‌سفارش (هم‌سو با قید موقت D-086).
- مزیت: ساده‌ترین، بدون ریسک بی‌مبنا شدن ثبت‌سفارش.
- عیب: فرایند اصلاح فروشنده/شماره پس از ثبت‌سفارش دست‌وپاگیر است.
- هزینه: کم؛ گزینهٔ Amendment هزینهٔ مدل و تست بیشتری دارد (وابسته به N-2).

**A-3.** پیشنهاد: فهرست فیلدها را قانون نکنیم؛ دامنه فهرست را پارامتر بگیرد تا مالک تصمیم بدهد. برای Draft: پیش از صدور، Draft نسخهٔ کاری است و Revision شمرده نمی‌شود (تا با تغییرناپذیری Revision تعارض نکند). هر دو `NeedsApproval`.
- مزیت: هیچ فیلدی بی‌دلیل Revision نمی‌سازد.
- عیب: تا تصمیم، تست‌های Revision نمی‌توانند فهرست واقعی را پوشش دهند.
- هزینه: کم (پارامتر)، ولی تصمیم مالک مسیر بحرانی است.

**N-7.** پیشنهاد: ارجاع بین DBها فقط شناسهٔ پایدار + اعتبارسنجی سمت سرور از طریق Registry؛ اگر DB مبدأ در دسترس نیست، خطای شفاف و بدون وانمود به داده به‌روز (D-117 §9).
- مزیت: با D-111 بند ۸ و D-115 هم‌خوان؛ بدون زیرساخت توزیع‌شده.
- عیب: اعتبارسنجی هر ارجاع یک فراخوانی سمت سرور دارد.
- هزینه: متوسط؛ تست‌های Multi-DB لازم است.

**OD-017.** پیشنهاد: تا تصمیم مالک، هیچ مقدار پیش‌فرض پنهان در Domain قرار نگیرد؛ محدودیت‌های فعلی `PaymentPlanCalculator` تغییر نکند.

## ۵. C-3 — چرا برش Domain-only انجام نشد

مأموریت: اگر پیاده‌سازی بدون حدس ممکن نیست، متوقف شو و علت را گزارش کن. کاندیداها بررسی شد:

| کاندیدا | مانع دقیق |
|---|---|
| `ProformaRevision` + `ProformaLine` + مراحل پرداخت | A-1 و N-9 (جای تاریخ اعتبار و فروشنده)، A-3، A-4 (رابطهٔ مبالغ)، OD-017/A-8 (دقت و MinorUnits)، N-11 (HS Code روی خط) |
| `ProformaHistoryEvent` (تمدید) | A-1، A-2، N-8 |
| سرتیتر `Proforma` و چرخهٔ وضعیت | N-1 (`Superseded`)، P-4، A-3 (Draft)، N-9 |
| `OrderRegistration` | P-5، A-5، N-2 |
| نوع پایهٔ append-only (H-2) یا Value Object مالکیت/Origin بدون مصرف‌کننده | وابسته به تصمیم‌ها نیست، ولی بدون مصرف‌کنندهٔ مصوب «کد بی‌استفاده» است و مأموریت صریحاً آن را ممنوع کرده |
| تغییر `TradeAccessContext`/`TradePeriodScope` | ممنوع تا پایان Security Review و تأیید مالک |

نتیجه: C-3 `Blocked`. هیچ کدی نوشته، هیچ Entity، DbSet، Configuration، Migration، API، DI یا UI تغییر نکرد، و `DenyAllTradeModuleEntitlement` دست‌نخورده است.

## ۶. بررسی و آزمون

| فرمان/آزمون | نتیجهٔ واقعی | توضیح |
|---|---|---|
| `scripts/dev/install-dotnet.sh` | موفق | SDK 10.0.404؛ SHA-512 تأیید شد |
| `dotnet restore Template.sln` | موفق | exit 0 |
| `dotnet build Template.sln --no-restore -c Release` | موفق | ۰ خطا، ۳۸۰۰ هشدار (پایهٔ main؛ تغییر کدی نداشتم) |
| تست‌های Unit/Architecture: `TradeAccessGateTests`، `TradeAuthorizationAdapterTests`، `DenyAllTradeModuleEntitlementTests`، `TradeEndpointGateTests` | اجراشده ۴۶، موفق ۴۶، ناموفق ۰، Skip ۰ | پایهٔ قبل از هر تغییر |
| `TradeAccessHttpTests` | اجراشده ۱۱، موفق ۱۱، ناموفق ۰ | **روی InMemory** (`FaranouApplicationFactory`)، نه SQL Server |
| `git diff --check` | در PR ثبت می‌شود | فقط یک فایل مستندات |
| کل `dotnet test Template.sln` | **اجرا نشد** | هیچ کدی تغییر نکرده؛ فقط پایهٔ مرتبط اجرا شد |
| تست‌های SQL Server | **اجرا نشد** | تغییر کدی نیست و SQL Server آزمایشی بالا نیامد |
| `python3 scripts/check_static_assets.py`، `npm ci/typecheck/test/build/check:offline` | **اجرا نشد** | تغییر دارایی وب یا ClientApp نیست |

## ۷. امنیت، داده و سازگاری
هیچ اثری بر احراز هویت، مجوز، Tenant/Company، Schema، Migration یا داده ندارد. Trade همچنان fail-closed است. همهٔ توصیه‌های بخش ۲.۶ فقط پیشنهادند.

## ۸. ریسک‌ها و کارهای باقی‌مانده
- پاسخ مالک به بخش ۲.۸ و به تصمیم‌های باز بخش ۴ (به‌ویژه A-1، P-4، P-5، A-3، OD-017، PaymentPlan ۱ تا ۳).
- ادغام #765 و ثبت ADR شفاف‌ساز برای D-063 §5.
- بدهی تست: F-D و F-E (Entitlement با شناسهٔ درست؛ SQL Server برای IDOR).
- **ردیف `WORK_LOG.md` و `PROJECT_STATUS.md` اضافه نشد:** این فایل‌های مشترک در PR #765 (و `WORK_LOG` احتمالاً در #766) هم‌زمان در حال تغییرند. طبق مأموریت، به‌جای آن در همین handoff ثبت شد. پس از ادغام #765، ردیف WORK_LOG جدا Append شود.
- گام بعدی دقیق: تصمیم مالک روی ۲.۸ (۱) و (۲)؛ در صورت تأیید فاز ۱ → PR کوچک `TradeCompanyAccessContext` با TDD و بازبینی `security-reviewer` و `architect` (بدون Schema).

## ۹. ابزارها و Agentها (چهار دسته)
- **استفاده‌شده (با شاهد):** Skill `farano-workflow`؛ Agent opus با نقش بازبین امنیتی (گزارش + راستی‌آزمایی Lead)؛ `gh api` (REST)؛ `dotnet` برای build و تست پایه.
- **در دسترس ولی استفاده‌نشده:** Skillهای `faranou-security-multitenancy` و `change-impact` — برای یک سند مستنداتی بدون تغییر کد لازم نشد؛ Agentهای عمومی `pr-review-toolkit:*` — بازبینی مستقل Diff مستنداتی با آن‌ها انجام نشد.
- **در دسترس نبود:** Agentهای مخزن `security-reviewer`، `architect`، `code-reviewer`، `documentation-engineer` در فهرست جلسه نبودند.
- **اجرانشده:** بخش ۶ (کل Suite، SQL Server، کلاینت).
- **اندازه‌گیری Agent:** بازبین امنیتی: حدود ۱۹۲ هزار توکن، ۳۴ فراخوانی ابزار، یک رفت‌وبرگشت؛ تقسیم کار صرفه داشت چون خواندن همهٔ Callerها و تست‌ها را از Lead برداشت و Lead فقط ادعاهای کلیدی را راستی‌آزمایی کرد.

## ۱۰. فایل‌های بررسی‌شده
`AGENTS.md`، `CLAUDE.md`، `docs/DECISIONS.md` (D-062، D-063، D-064، D-074، D-086، D-105، D-111، D-114، D-115، D-116)، `docs/TRADE_OPEN_DECISIONS.md`، `docs/TRADE_S3_ARCHITECTURE.md`، `docs/PERMISSION_CATALOG.md`، `docs/handoffs/ISSUE-UNNUMBERED-trade-proforma-domain-design.md`، `docs/handoffs/ISSUE-UNNUMBERED-trade-payment-plan-invariants-and-decision-proposal.md`، محتوای شاخهٔ #765 (`FISCAL_PERIOD_DATA_MODEL.md`)، و کد Trade (`TradeAccessGate`، `TradeAccessContext`، `TradePeriodScope`، `ITradeAuthorization`، `TradeAuthorizationAdapter`، `DenyAllTradeModuleEntitlement`، `TradeAccessFilter`، `WorkContextService`، `WorkContextCookie`، `WorkContextEndpoints`، `OwnershipScope`، `TradePermissionCatalog`) و تست‌های مرتبط.

> **نکته:** `ROADMAP.md`، `PROJECT_STATUS.md`، `docs/TRADE_MODULE.md`، `DATABASE.md`، `SECURITY.md` و چند handoff فهرست‌شدهٔ مأموریت را کامل نخواندم؛ بخش‌های لازم برای N-5 و Proforma (طبق grep و بخش‌های هدفمند) مطالعه شد.

## ۱۱. شناسه‌های Git
- Branch: `docs/trade-stage-c-n5-security-review`
- Commit مبنا: `560ff289`
- Pull Request: در توضیحات PR ثبت می‌شود.
