# برنامهٔ تست و آمادگی امنیتی مرحلهٔ عملیاتی بعدی Trade (Workstream E)

- **Issue:** UNNUMBERED (همراه طرح `ISSUE-UNNUMBERED-trade-operational-stage-plan.md`)
- **مبنا:** `main@5484758722d0a316511806cf3097c5c691875599`.
- **وضعیت سند:** فقط مستندات. هیچ تستی نوشته یا اجرا نشده است؛ شمارش «موجود» از خواندن کد تست است (بازبینی `ISSUE-UNNUMBERED-trade-foundations-independent-review.md`). تست‌های SQL Server با `Assert.SkipUnless(SqlServerAvailable())` بدون SQL Server «رد (Skip)» می‌شوند و پاس حساب نمی‌شوند؛ هیچ تست SQL Server در این مأموریت اجرا نشده است.
- **قاعدهٔ گزارش:** اجراشده/پاس/شکست/اجرانشده جدا؛ اجرانشده هرگز پاس گزارش نمی‌شود.

## ۱) ماتریس: موجود در برابر ناموجود

«موجود» = تست در مخزن هست (نه الزاماً اجراشده). «ناموجود» = باید با PR مربوط افزوده شود. ستون «PR» به شمارهٔ PR در طرح مرحله اشاره دارد.

| حوزه | موجود [V] | ناموجود | PR |
|---|---|---|---|
| ایزولاسیون Company | تست‌های Party/Product HTTP و Service (`TradePartyHttpTests` ۲۸، `TradeProductHttpTests` ۲۳؛ `TradeMasterDataModelTests`) و تست‌های معماری فهرست legacy `TenantId` (#673/#674) | تست «دو شرکت، شمارهٔ یکسان مجاز» برای Entityهای جدید؛ `404` برای رکورد شرکت دیگر در ImportCase/Proforma/OrderRegistration | PR-3، PR-5 |
| دسترسی بین‌شرکتی (cross-company denial) | `TradeAuthorizationAdapterTests` (۱۸)، `TradeAccessGateTests` (۱۶) | تست رد دسترسی با Company دیگر روی endpointهای جدید؛ تست نشت پیام خطا (ورودی بازتاب نشود) | PR-5 |
| دورهٔ مالی و ReadOnly | `ReadOnlyPeriodDeniesWriteBeforeEntitlementAndPermission` (Gate)؛ تست `PeriodIndependent` در Adapter | تست HTTP «دورهٔ ReadOnly + DenyAll → `409 trade_period_read_only`»؛ تست مستقل از دوره بودن ثبت‌ها (D-086 §7) | PR-5 |
| مجوز و Permission | `TradeEndpointGateTests` (۱۰، معماری)؛ مجوزهای seed‌شده | تست مجوز `order-registration` (کاتالوگ ندارد)؛ تست «Read اضافه لازم است» برای Update ثبت‌ها | PR-5، PR-7 |
| Entitlement Fail-Closed | `DenyAllTradeModuleEntitlementTests` (۲)؛ `ProductionDenyAllEntitlementGets403ModuleUnavailableEvenWithPermission` (`TradeAccessHttpTests.cs:320`)؛ capability در Production `Available:false` | تست جدید برای هر endpoint جدید که زیر DenyAll `403 trade_module_unavailable` می‌دهد؛ **بدون** دور زدن DenyAll در Production | PR-5 |
| تکرار و همزمانی | Party/Product: pre-check + index یکتا + نگاشت 2601/2627 → `409`؛ `RowVersion` | تست رقابتی SQL Server برای شمارهٔ ImportCase/Proforma/OrderRegistration؛ تست رقابتی قفل کالا؛ تست `RowVersion` | PR-4، PR-5، PR-8 |
| اعشار و گرد کردن | `TradePaymentPlanTests` (۳۵)؛ `TradeUnitConversionTests` (۴۵) | **ثبت رفتار بیش‌تخصیص** (`JPY`، `final=5`، ده مرحلهٔ ۱۰٪ → `ResidualStageNotPositive`)؛ انحراف درصد مرحلهٔ آخر؛ لبه‌های `IRR` (۲ رقم) | قبل از wire شدن PaymentPlan (PR-3) |
| یکپارچگی Revision/History | — (دامنه وجود ندارد) | Revision بدون mutator؛ تمدید بدون Revision؛ `NewValidUntil > OldValidUntil`؛ بازسازی طرح پرداخت و مقایسهٔ مبالغ (`TryBuild`)؛ فقط‌افزودنی بودن HistoryEvent | PR-2، PR-3 |
| اسناد نامعتبر/منقضی | `TradeDocumentNumberTests` (۱۷) | `Expired` مشتق از `ValidUntil` (ذخیره نمی‌شود)؛ رفتار شمارهٔ لغوشده؛ ZWNJ در شمارهٔ سند (سیاست مالک)؛ نویسهٔ خارج BMP/homoglyph | PR-2، PR-3 |
| Migration و Rollback | `DatabaseMigrationHostSqlServerTests` (۸)، `TradeMasterDataMigrationSqlServerTests` (۴)؛ #675 (Tenant، G3) | **شکست اتصال برای قفل (کد ۳ دو معنا)**؛ fallback `master` + هم‌زمانی؛ `--migrate` در `setupRequired`؛ Up/Down هر جدول جدید؛ `ChangeTracker` در برابر `Deleted` | PR-4 |
| API integration | الگوی `TradeAccessHttpTests`، `TradePartyHttpTests` | تست‌های HTTP ImportCase/Proforma/OrderRegistration؛ Anti-forgery؛ 415 برای بدنهٔ بدون Content-Type (در فهرست Access دیده نشد) | PR-5 |
| UI و مدیریت خطا | ≈۱۶۴ مورد در ۱۴ فایل زیر `ClientApp/src/features/trade` | فرم‌های ImportCase/Proforma (چند خط، چند HS)، خطای ۴۰۹ (RowVersion)، ۴۰۴، `trade_module_unavailable`؛ `npm` typecheck/test/build/offline | PR-6 |
| ردیابی ممیزی | فقط `CreatedAt/UpdatedAt` در Trade | تست قابل‌ردیابی بودن کاربر/زمان/دلیل در Revision/HistoryEvent/Amendment؛ تست اینکه `SecurityAuditEvent` برای این منظور استفاده نمی‌شود | PR-2، PR-8، PR-9 |

## ۲) شکاف‌های امنیتی/صحت از بازبینی D که تست می‌خواهند

| # | شکاف | نوع | اقدام |
|---|---|---|---|
| ۱ | `PartyService.HasForbiddenCharacters` چهار نویسه (`U+061C`، `U+2028`، `U+2029`، `U+FEFF`) را رد نمی‌کند اما `ProductService` می‌کند | اصلاح کوچک داخل Trade | PR کد جدا + تست‌های متناظر در `TradePartyServiceTests` (در این مأموریت اجرا/پاس نشده تا زمان اجرای واقعی) |
| ۲ | نویسهٔ نامرئی Format (`U+200B`، `U+2060`) در `code` Party/Product یکتایی را دور می‌زند | تأیید مالک | تصمیم، سپس تست |
| ۳ | نرمال‌سازی کد Party/Product (ارقام/ی/ک) | تصمیم سیاستی | تصمیم، سپس تست |
| ۴ | کد خروج ۳ دو معنا؛ `setupRequired`؛ dispose در لغو؛ قفل `master` | خارج از مالکیت Trade (میزبان مشترک) | فقط گزارش؛ تأیید مالک + `security-reviewer` |

## ۳) معیار «اجرا» برای هر PR آینده
- `dotnet restore/build/test` و `python3 scripts/check_static_assets.py` و برای UI: `npm ci && npm run typecheck && npm test && npm run build && npm run check:offline`.
- برای EF/SQL/Auth: تست SQL Server با `scripts/dev/test-sqlserver.sh up` (کانتینر آزمایشی ایزوله؛ پس از کار حذف شود). فقط وقتی «موفق» نوشته می‌شود که واقعاً روی SQL Server اجرا شده باشد؛ رد (Skip) = اجرا نشده.
- Build موفق به معنای صحت امنیتی یا عملکردی کامل نیست.

## ۴) محدودیت‌ها
- در این محیط `dotnet` نصب نیست؛ هیچ Build/Test اجرا نشده است. این سند هیچ ادعای «تست پاس شد» ندارد.
- SQL Server در این مأموریت در دسترس نبود/اجرا نشد.
