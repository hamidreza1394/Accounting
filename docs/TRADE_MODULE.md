# ماژول بازرگانی خارجی — معماری و قواعد اجرایی

**مرجع تصمیم:** D-061 و D-062  
**Issue:** #397  
**وضعیت:** D-061/D-062/D-063 Accepted؛ foundation معماری در main ادغام شده است (PR #398، commit 6c09765cefe8742668c34deda1c86b881f970332). قابلیت‌های عملیاتی، seed مجوزها، enforcement کامل entitlement و گردش‌کارها هنوز NotImplemented هستند.

## 1. معماری و استقرار

در فاز اول ماژول در مخزن و Host فعلی Accounting اجرا می‌شود؛ اما یک Bounded Context مستقل است.

    Modules/Trade/
      Trade.Domain/
      Trade.Application/
      Trade.Infrastructure/
    ClientApp/src/features/trade/   (D-081؛ قبلاً modules/trade)
    tests/Template.Tests/

- Domain هیچ ProjectReference به Core یا Server ندارد.
- Application فقط به Trade.Domain وابسته است و قراردادهای Port برای مرزهای بیرونی تعریف می‌کند.
- Infrastructure فقط Trade.Application/Domain را Reference می‌کند؛ مالک TradeDbContext و EF mapping است.
- Server Composition Root و محل adapterهای Host است؛ `TradeAuthorizationAdapter` مجوزهای تخصصی را با CompanyAccessService و WorkContext مشترک ترکیب می‌کند و هیچ منطق دامنه‌ای در Server قرار نمی‌گیرد.
- پروژه‌های UI و API بعد از تثبیت use caseها اضافه می‌شوند؛ UI هیچ مرجع امنیتی نیست.

این یک Modular Monolith است، نه Microservice توزیع‌شده. مسیر استخراج به Microservice در بخش ۹ آمده است.

## 2. دیتابیس و مالکیت داده

- در فاز اول یک SQL Server database فیزیکی مشترک استفاده می‌شود.
- تمام جداول ماژول تحت schema مستقل trade قرار می‌گیرند.
- `TradeDbContext` در پروژه Infrastructure نگهداری می‌شود؛ `TradeDbContextOptionsConfiguration` از SQL Server مشترک، migration assembly خود Trade و history table مستقل `dbo.__TradeMigrationsHistory` استفاده می‌کند. Host فقط در شاخه دارای Connection String معتبر (خارج از Database Setup) آن را با lifetime برابر Scoped ثبت می‌کند. Factory طراحی EF نیز مستقل از boot شدن Server موجود است. این برش فقط ثبت و پیکربندی را پیاده‌سازی می‌کند؛ هنوز Migration یا مسیر deployment عملیاتی Trade ایجاد نشده و وضعیت نهایی به CI نیاز دارد.
- هیچ FK از Trade به جداول تراکنشی حسابداری ایجاد نمی‌شود.
- شناسه شرکت مرجع منطقی است؛ حذف/غیرفعال‌سازی شرکت باید از مسیر هسته و با سیاست lifecycle هماهنگ شود.
- Migration مشتری بدون Issue مستقل، تست SQL Server، rollout/rollback و تأیید اجرا نمی‌شود.

### 2.1 داده پایه Party و Product — وضعیت S2.2

- `Party` و `Product` داده مرجع شرکت‌محورند؛ `TenantId` از قرارداد TradeEntity می‌آید و هیچ `FiscalPeriodId` ندارند.
- `Party` شامل کد، نام حقوقی، نام نمایشی، شناسه حقوقی، کشور، نقش‌های چندگانه و وضعیت فعال است.
- حساب بانکی Party یک رکورد فرزند مستقل است؛ وضعیت Pending/Verified/Rejected و تأییدکننده/زمان تأیید صریح دارد. تغییر مشخصات حساب، تأیید قبلی را باطل و وضعیت را Pending می‌کند. وضعیت حساب به‌تنهایی کافی نیست و use case پرداخت باید فعال بودن Party را نیز کنترل کند.
- `Product` شامل کد/نام، گروه کالا، HS و واحد پایه است. `ProductIdentifier` کد GTIN و IRC را در دو ستون متنی مستقل نگه می‌دارد؛ صفر ابتدایی حذف نمی‌شود و تاریخ اعتبار اجباری است.
- روابط فرزند به والد با کلید مرکب `TenantId + Id` برقرار می‌شوند تا رکورد فرزند نتواند به والد شرکت دیگر متصل شود. کدهای Party/Product و شناسه‌های GTIN/IRC ایندکس یکتای tenant-scoped با فیلتر رکورد حذف‌نشده دارند.
- این مرحله فقط Domain و EF Model را افزوده است؛ هیچ Migration تولید/اجرا نشده و API، seed داده مرجع و فرم UI خارج از این Issue هستند. تا ایجاد Migration و آزمون SQL Server، این مدل برای استقرار عملیاتی آماده محسوب نمی‌شود.

- **API کالا (S2-4، `Implemented`؛ PR #644 با CI سبز ادغام شد، SHA `6fa65e9e`):** `GET/POST /api/v1/trade/products`، `GET/PUT /api/v1/trade/products/{id}` و `GET /api/v1/trade/reference/units` (فقط‌خواندنی، مجوز `trade.form.product.view`). الگو و خطاها همان Party است (404 خالی، 409 `trade_product_code_duplicate`/`trade_concurrency_conflict`، 413 `trade_request_too_large`). POST/PUT علاوه بر `create|update` مجوز `view` می‌خواهند و مستقل از دورهٔ مالی‌اند (پرچم `periodIndependent`؛ گسترش D-084 به Product در D-085 ثبت شده و `Proposed`+`NeedsApproval` است). `hsCode` متن است (حداکثر ۱۶ نویسهٔ لاتین قابل‌چاپ) و `baseUnitCode` باید در واحدهای مرجع باشد. `isActive` برگردانده می‌شود ولی قابل ویرایش نیست و Endpoint حذف/غیرفعال‌سازی نیست (D-083 `Proposed`). `ProductIdentifier` استفاده نمی‌شود و Migration/Schema تغییر نکرده است.

## 3. Tenant و جداسازی شرکت

در این مخزن طبق D-013، Company.Id همان TenantId است. Tenant entity جدید نسازید.

1. تمام داده‌های متعلق به شرکت باید TenantId داشته باشند.
2. TenantId از درخواست کلاینت یا Claim شرکت پذیرفته نمی‌شود؛ سرور آن را از Work Context معتبر و CompanyAccessService تأیید می‌کند.
3. هر query، update، delete، export، attachment و report باید TenantId را صریحاً در scope داشته باشد.
4. Global Query Filter به‌تنهایی کنترل امنیتی نیست و در این مخزن عمداً جایگزین predicate صریح نمی‌شود.
5. تست منفی باید دسترسی به رکورد شرکت دیگر را با شناسه معتبر اما غیرمجاز رد کند؛ بدون افشای وجود داده.

## 4. دوره‌های مالی مشترک با هسته فرانو

- تنها مرجع تعریف، انتخاب و چرخه عمر دوره مالی، موجودیت `Domain.FiscalPeriod` در هسته فرانو است؛ Trade هیچ `OperationalPeriod`، جدول یا سرویس موازی ندارد.
- داده‌های مرجع tenant-scoped مانند Party و Product به‌طور معمول `TenantId` دارند و `FiscalPeriodId` نمی‌گیرند. اسناد و عملیات تراکنشی period-scoped هر دو شناسه `TenantId` و `FiscalPeriodId` را نگه می‌دارند؛ `TenantId` همان `Company.Id` و `FiscalPeriodId` همان `FiscalPeriod.Id` است.
- **قاعدهٔ تاریخی D-063 (برای ImportCase/Proforma/OrderRegistration با D-086 superseded شده است):** قبلاً دورهٔ افتتاح روی ImportCase ثبت و هر سند/عملیات فرزند به دورهٔ انتخاب‌شده متصل می‌شد. طبق D-086، خود ImportCase، Proforma، OrderRegistration و پیگیری بلندمدت پرونده FiscalPeriodId ندارند و با بسته‌شدن دوره از دسترس خارج نمی‌شوند. اسناد مالی واقعی و عملیات حسابداری آینده همچنان FiscalPeriodId دورهٔ ثبت خود را دارند و باید WorkContext، تعلق و وضعیت دوره و مجوز را در سرور اعتبارسنجی کنند.
- Trade.Domain و Trade.Application برای حفظ مرز ماژول به Core Domain/DbContext Reference نمی‌دهند. Host باید پیش از اجرای use case بررسی کند که `FiscalPeriod.TenantId == TenantId` و دوره از طریق `WorkContext` معتبر انتخاب شده است. این ارجاع منطقی است و FK بین schemaها ایجاد نمی‌شود.
- انتخاب شرکت/دوره، فهرست دوره‌های قابل انتخاب و سیاست وضعیت دوره از `WorkContext` و `FiscalPeriodSelectionPolicy` مشترک پیروی می‌کند؛ هیچ دسترسی جداگانه‌ای برای «دوره بازرگانی» تعریف نمی‌شود.
- سیاست مشترک دوره حفظ می‌شود: دوره Open قابل ورود با رعایت مجوز عملیات است؛ دوره Closed برای مشاهده/گزارش فقط خواندنی است و عملیات نوشتن با قواعد مشترک بسته می‌شود (**استثنا، D-084 و D-085 (`Proposed`):** عملیات صریحاً «مستقل از دوره» مثل ایجاد/ویرایش Party و Product که اطلاعات پایهٔ سطح Tenant است؛ بقیهٔ کنترل‌ها حفظ می‌شود)؛ Archived مطابق سیاست هسته قابل انتخاب نیست.
- ورود خودکار به شرکت/دوره قبلی ممنوع است؛ هر نشست جدید باید context معتبر را طبق سیاست هسته تعیین کند.

## 5. مجوز فرم‌ها و عملیات

- مجوزهای بازرگانی به‌صورت کدهای تخصصی در همان کاتالوگ مجوزهای فرانو (`PermissionDefinitions`) تعریف و به نقش شرکت از طریق `CompanyRolePermissions` تخصیص داده می‌شوند؛ سیستم موازی تخصیص نقش یا دوره ایجاد نمی‌شود. **وضعیت فعلی:** API/UI تخصیص `CompanyRolePermissions` در اسکلت فرانو هنوز پیاده‌سازی نشده است (`docs/CORPORATE_GROUPS.md`). مرحله بعد باید یک فرم یکپارچه مدیریت دسترسی نقش شرکت بسازد و بخش Trade را کنار سایر ماژول‌ها در همان فرم نمایش دهد.
- `Server/Infrastructure/Trade/TradeAuthorizationAdapter.cs` هویت را از principal احراز‌شده می‌گیرد و با `WorkContextService` زمینه جاری را دوباره از Cookie محافظت‌شده و دیتابیس اعتبارسنجی می‌کند.
- `CompanyAccessService` مرجع سنجش مجوز نقش در شرکت است. Adapter تعلق `FiscalPeriodId` به شرکت جاری را کنترل می‌کند و سپس مجوز تخصصی عملیات را از سازوکار مشترک می‌سنجد.
- شناسه دوره در اینجا context اجرای مجوز است، نه مجوز مستقل دوره‌ای. سطح دسترسی به وضعیت Open/Closed/Archived از سیاست مشترک `FiscalPeriodSelectionPolicy` می‌آید؛ `TradeAccessIntent.Write` در دوره بسته رد می‌شود (**استثنا، D-084 و D-085 (`Proposed`):** عملیات صریحاً «مستقل از دوره» مثل ایجاد/ویرایش Party و Product که اطلاعات پایهٔ سطح Tenant است؛ بقیهٔ کنترل‌ها حفظ می‌شود).
- طبق D-063، گزارش عملیاتی چندشرکتی از موج اول لازم است؛ مجوز نقش باید در تمام شرکت‌ها وجود داشته باشد و هر جفت `TenantId/FiscalPeriodId` جداگانه اعتبارسنجی شود؛ نتیجه جزئی پذیرفته نیست. این گزارش، گزارش تلفیقی حسابداری نیست.

برای هر فرم/منبع کد مجوز مستقل و پایدار تعریف می‌شود:

- trade.form.<form-code>.view
- trade.form.<form-code>.create
- trade.form.<form-code>.update
- trade.form.<form-code>.approve
- trade.form.<form-code>.delete
- trade.form.<form-code>.export

مجوزهای تخصصی گزارش نیز مستقل‌اند. فهرست فرم‌های واقعی و کدهای نهایی با نیازمندی‌های فرایند واردات در Issueهای دامنه‌ای تکمیل می‌شوند.

**قانون امنیتی:** منو، فرم، دکمه یا فیلد پنهان‌شده به معنی مجازبودن یا غیرمجازبودن درخواست نیست. API و Application use case باید permission را مستقل از UI بررسی کنند. هیچ endpoint فقط به‌خاطر دسترسی به منوی والد قابل استفاده نیست.

## 6. لایسنس و مجوز عملیاتی

- شناسه تجاری ماژول: TRADE_IMPORT.
- entitlement تجاری می‌گوید شرکت حق استفاده از ماژول را دارد.
- permission عملیاتی می‌گوید کاربر مشخص در شرکت/دوره مشخص چه عملیاتی می‌تواند انجام دهد.
- این دو قابل جایگزینی نیستند.
- ترتیب کنترل: نشست معتبر → Company membership و تطبیق TenantId → انتخاب/اعتبارسنجی FiscalPeriod مشترک → entitlement ماژول → permission تخصصی بازرگانی از سیستم مشترک → اجرای use case با رعایت ReadOnly بودن دوره بسته.
- کاتالوگ اولیه مجوزها در `TradePermissionCatalog` تعریف می‌شود و در `PermissionDefinitions` مشترک ذخیره می‌شود؛ هیچ نقش یا Permission store موازی وجود ندارد. Migration `20261031140000_SeedTradePermissionCatalog` فقط ردیف‌های کاتالوگ را درج می‌کند و هیچ `CompanyRolePermission`ای را خودکار تخصیص نمی‌دهد.
- کاتالوگ این برش شامل ۴۶ مجوز برای Party، Product، Purchase Request، Supplier Inquiry، Quotation Comparison، Proforma، Contract، Import Case و دو نوع گزارش است؛ این فهرست، همه فرم‌های ۹۰ صفحه پیشنهاد را پوشش نمی‌دهد و فرم‌های بعدی در Issueهای جدا اضافه می‌شوند.
- S1-a (Issue #403): دروازه `TradeAccessGate` با ترتیب نشست ← عضویت ← WorkContext/دوره ← entitlement ← مجوز، پیاده‌سازی پیش‌فرض `DenyAllTradeModuleEntitlement` (رد در همه محیط‌ها؛ هیچ stub مجازکننده‌ای در Server نیست)، فیلتر `TradeAccessFilter` برای گروه `/api/v1/trade/**` (endpoint بدون متادیتای صریح رد می‌شود) و `GET /api/v1/trade/capabilities` افزوده شده‌اند. تا ادغام #390 و تأمین حالت لایسنس runtime، هر endpoint عملیاتی Trade با `trade_module_unavailable` رد می‌شود. `HasPermissionForAllCompanyPeriodsAsync` entitlement-aware نیست و نباید از هیچ endpoint در دسترس باشد.
- entitlement runtime هنوز Port است؛ باید در S1 به قرارداد واقعی Licensing متصل و پیش از هر قابلیت Trade در Production fail-closed شود. entitlement برای همان Company بررسی می‌شود و از permission کاربر جداست.

## 7. گزارش تک‌شرکتی و چندشرکتی

### گزارش شرکت
- TenantId و دوره باید از context معتبر یا درخواست اعتبارسنجی‌شده گرفته شوند.
- مجوز گزارش همان شرکت و دوره بررسی می‌شود.
- داده‌های گزارش باید query predicate صریح TenantId داشته باشند.

### گزارش چندشرکتی
- scope شرکت/دوره‌ها از درخواست arbitrary کلاینت پذیرفته نمی‌شود؛ ابتدا scope مجاز از منبع معتبر سرور حل و هر جفت شرکت/دوره اعتبارسنجی می‌شود.
- کاربر باید مجوز گزارش تجمیعی را در تمام جفت‌های شرکت/دوره scope داشته باشد.
- empty scope یا partial permission رد می‌شود؛ خروجی ناقص و بی‌اعلام ممنوع است.
- scope، شرکت‌ها، دوره‌ها، کاربر، زمان، فیلترها و شناسه همبستگی audit می‌شوند.
- گزارش عملیاتی Trade فقط داده Trade را جمع می‌زند. گزارش تلفیقی عددی حسابداری مشمول D-034 است و بدون Ledger/Chart-of-Accounts ادعا نمی‌شود.

## 8. اتصال به سیستم مالی

Trade به جداول یا Entityهای حسابداری دسترسی مستقیم ندارد و نمی‌تواند مستقیم سند حسابداری ایجاد/تغییر دهد. اتصال آتی با قراردادهای نسخه‌دار و Integration Events خواهد بود. پیام‌ها باید شناسه همبستگی، نسخه قرارداد، idempotency و audit داشته باشند. Outbox/Inbox فقط زمانی اضافه می‌شود که جریان واقعی نیاز به سازگاری بین دو bounded context داشته باشد.

## 9. مسیر استخراج به Microservice

در صورت نیاز آینده:
1. API و Integration Contracts نسخه‌دار و identity سرویس‌به‌سرویس تعریف شود.
2. schema trade به دیتابیس مستقل منتقل و مالکیت backup/restore/migration جدا شود.
3. Company authorization و license entitlement از adapter درون‌فرایندی به API امن بین‌سرویسی تبدیل شود.
4. Outbox/Inbox و idempotency برای جریان‌های بین‌سرویسی اضافه شود.
5. deployment، health/readiness، observability، secrets، scaling و CI/CD مستقل شوند.
6. تست قرارداد و migration داده قبل از cutover اجرا شود.

استخراج آتی نیازمند تغییرات عملیاتی و داده‌ای است؛ این طراحی coupling را کاهش می‌دهد، اما ادعای «استخراج بدون کار» ندارد.

## 10. موارد ممنوع

- ایجاد Tenant entity موازی یا استفاده از TenantId ارسالی کلاینت.
- Reference به Core Accounting Domain/DbContext از Trade.Domain یا Trade.Application.
- FK به اسناد/جداول تراکنشی حسابداری.
- ایجاد دوره مالی مستقل یا دسترسی موازی به دوره برای Trade.
- اعتماد به مخفی‌کردن فرم/دکمه به‌عنوان Authorization.
- گزارش چندشرکتی با دسترسی جزئی یا scope اعلام‌نشده.
- یکی‌گرفتن entitlement تجاری و permission کاربر.
- افزودن broker، Event Bus، دیتابیس جدا یا Kubernetes بدون Issue و نیاز اثبات‌شده.
- اجرای Migration مشتری بدون تأیید مستقل.


## 11. سیاست‌های اجرایی تأییدشده (D-063؛ با اصلاح دامنهٔ دوره در D-086)
- Voyage/Shipment یک شرکت مالک دارد؛ شرکت‌های دیگر فقط allocation/share line دارند. سهم تخصیص‌یافته مجوز مشاهده اسناد شرکت مالک نیست.
- قاعدهٔ دوره‌ای D-063 برای ImportCase، Proforma و OrderRegistration با D-086 superseded شده است: این رکوردهای تجاری و پیگیری بلندمدت پرونده مستقل از FiscalPeriod هستند. اسناد مالی واقعی آینده همچنان دوره‌محورند و FiscalPeriodId دورهٔ ثبت خود را نگه می‌دارند؛ انتقال خودکار اسناد مالی بین دوره‌ها انجام نمی‌شود.
- گزارش چندشرکتی عملیاتی از موج اول با مجوز all-or-nothing و audit کامل scope عرضه می‌شود؛ گزارش تلفیقی مالی خارج از این دامنه است.
- entitlement مستقل TRADE_IMPORT از S1 و قبل از دسترسی به قابلیت Trade enforce می‌شود؛ Production در نبود/خطای اعتبارسنجی fail-closed است.


## ۱۲. قواعد جاری اجرای S3 — D-086 (2026-11-14)

D-086 مرجع جاری قواعد کسب‌وکاری S3 است و در موارد تعارض، بندهای متعارض D-080 را جایگزین می‌کند. P-1 در D-080 همچنان Accepted است؛ P-4 و P-5 باز هستند.

### ۱۲.۱ پروفایل فعالیت شرکت
- هر شرکت یک پروفایل فعال و نسخه‌دار دارد؛ تغییر آن بر پرونده‌های قبلی اثر بازگشتی ندارد.
- پروفایل فقط گزینه‌های نوع‌دار و مشخص را تنظیم می‌کند: تعداد کالا در پرونده/پیش‌فاکتور، تعداد پیش‌فاکتور/ثبت‌سفارش جاری، تغییر فروشنده و cardinalityهای حمل و ترخیص.
- موتور عمومی تنظیمات، فرم‌ساز یا workflow builder ساخته نمی‌شود.

### ۱۲.۲ پرونده و شماره‌ها
- شماره پرونده سامانه جامع تجارت در محدوده Company یکتا است. طبق D-087، Tenant مرز داده مشتری و Company شرکت عملیاتی داخل آن است؛ یک Tenant می‌تواند چند Company داشته باشد. یکتایی شماره‌ها Company-scoped است، نه Tenant-scoped. همه عملیات باید هر دو مرز را سمت سرور اعتبارسنجی کنند.
- تغییر کالا پیش از ثبت‌سفارش روی همان پرونده مجاز است و تاریخچه شامل کالای قبل/بعد، کاربر، زمان و علت دارد.
- پس از دریافت ثبت‌سفارش، تغییر کالا ممنوع است.
- تعداد کالا در پرونده و پیش‌فاکتور دو تنظیم مستقل‌اند؛ کالاهای مشابه‌نام مانند «گندم» و «گندم دامی» مستقل‌اند.

### ۱۲.۳ پیش‌فاکتور و ثبت‌سفارش
- تعداد پیش‌فاکتور جاری و کالاهای هر پیش‌فاکتور بر اساس پروفایل است؛ در فعالیت فعلی یک پیش‌فاکتور جاری و تک‌کالایی است.
- شماره پیش‌فاکتور در هر شرکت یکتا است؛ تکرار رد می‌شود و شماره متمایز مانند /1 را کاربر وارد می‌کند.
- اصلاح پیش‌فاکتور با Revision جدید و حفظ نسخه قبلی ثبت می‌شود؛ تغییر فروشنده تابع پروفایل است.
- شماره ثبت‌سفارش در هر شرکت یکتا است؛ اصلاح با همان شماره در تاریخچه همان ثبت‌سفارش، شماره تازه در رکورد مستقل. شماره ابطال‌شده دوباره استفاده نمی‌شود.
- تغییر کالا پس از ثبت‌سفارش ممنوع؛ ثبت مجدد پس از ابطال طبق P-1 با پرونده جدید است.

### ۱۲.۴ گردش کار، دوره مالی و فایل
- وضعیت کلی پرونده از وضعیت مجوزها/مراحل فرایند جداست؛ رد درخواست/مجوز به‌تنهایی پرونده را نمی‌بندد.
- پرونده، پیش‌فاکتور، ثبت‌سفارش و هزینه‌های پیگیری بلندمدت مستقل از دوره مالی هستند؛ اسناد مالی واقعی آینده و عملیات حسابداری دوره ثبت مالی خود را خواهند داشت.
- فایل‌ها خارج SQL Server، با نگهداری نامحدود زمانی و کنترل دسترسی مبتنی بر دسترسی پرونده ذخیره می‌شوند؛ PDF، Word، Excel و تصویر پشتیبانی می‌شوند.
- Maker-Checker از سیاست مرکزی و قابل تنظیم برای هر عملیات استفاده می‌کند؛ تأییدکننده باید با ثبت‌کننده متفاوت باشد.

### ۱۲.۵ حمل و ترخیص
- چند بارنامه برای یک پرونده مجاز است؛ در فعالیت فعلی یک بارنامه به چند پرونده متصل نمی‌شود.
- اظهارنامه معمولاً به یک بارنامه مربوط است، اما تجمیع استثنایی چند بارنامه از یک ثبت‌سفارش و کشتی در یک اظهارنامه پشتیبانی می‌شود.
- گمرک مجاز محدود به گمرک‌های مشخص‌شده در ثبت سفارش است.

جزئیات کامل، مدل مفهومی، معیارهای پذیرش و موارد باز در TRADE_S3_ARCHITECTURE.md آمده‌اند. پیش از Migration، مدل موجود Modules/Trade و V3.2 بررسی می‌شود؛ این سند مجوز ساخت جدول تکراری یا تغییر Migration host نیست.
