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
    ClientApp/src/modules/trade/
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
- TradeDbContext و assembly مهاجرت Trade مستقل‌اند؛ migration history آن نیز باید جدا باشد. وضعیت واقعی فعلی: TradeDbContext موجود است، اما ثبت DI، design-time factory، migrations assembly/history مستقل و مسیر deployment هنوز باید ممیزی/تکمیل شوند؛ وجود DbContext به‌تنهایی اثبات‌کننده جداسازی migration نیست.
- هیچ FK از Trade به جداول تراکنشی حسابداری ایجاد نمی‌شود.
- شناسه شرکت مرجع منطقی است؛ حذف/غیرفعال‌سازی شرکت باید از مسیر هسته و با سیاست lifecycle هماهنگ شود.
- Migration مشتری بدون Issue مستقل، تست SQL Server، rollout/rollback و تأیید اجرا نمی‌شود.

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
- مطابق D-063، پرونده ImportCase می‌تواند چنددوره‌ای باشد: دوره افتتاح روی پرونده ثبت می‌شود، اما هر سند/عملیات فرزند دوره انتخاب‌شده واقعی خود را ثبت می‌کند. انتقال به دوره بعد اسناد قبلی را منتقل یا بازنویسی نمی‌کند؛ هر عملیات جدید باید دوباره WorkContext، تعلق دوره، وضعیت دوره و permission را اعتبارسنجی کند.
- Trade.Domain و Trade.Application برای حفظ مرز ماژول به Core Domain/DbContext Reference نمی‌دهند. Host باید پیش از اجرای use case بررسی کند که `FiscalPeriod.TenantId == TenantId` و دوره از طریق `WorkContext` معتبر انتخاب شده است. این ارجاع منطقی است و FK بین schemaها ایجاد نمی‌شود.
- انتخاب شرکت/دوره، فهرست دوره‌های قابل انتخاب و سیاست وضعیت دوره از `WorkContext` و `FiscalPeriodSelectionPolicy` مشترک پیروی می‌کند؛ هیچ دسترسی جداگانه‌ای برای «دوره بازرگانی» تعریف نمی‌شود.
- سیاست مشترک دوره حفظ می‌شود: دوره Open قابل ورود با رعایت مجوز عملیات است؛ دوره Closed برای مشاهده/گزارش فقط خواندنی است و عملیات نوشتن با قواعد مشترک بسته می‌شود؛ Archived مطابق سیاست هسته قابل انتخاب نیست.
- ورود خودکار به شرکت/دوره قبلی ممنوع است؛ هر نشست جدید باید context معتبر را طبق سیاست هسته تعیین کند.

## 5. مجوز فرم‌ها و عملیات

- مجوزهای بازرگانی به‌صورت کدهای تخصصی در همان کاتالوگ مجوزهای فرانو (`PermissionDefinitions`) تعریف و به نقش شرکت از طریق `CompanyRolePermissions` تخصیص داده می‌شوند؛ سیستم موازی تخصیص نقش یا دوره ایجاد نمی‌شود. **وضعیت فعلی:** API/UI تخصیص `CompanyRolePermissions` در اسکلت فرانو هنوز پیاده‌سازی نشده است (`docs/CORPORATE_GROUPS.md`). مرحله بعد باید یک فرم یکپارچه مدیریت دسترسی نقش شرکت بسازد و بخش Trade را کنار سایر ماژول‌ها در همان فرم نمایش دهد.
- `Server/Infrastructure/Trade/TradeAuthorizationAdapter.cs` هویت را از principal احراز‌شده می‌گیرد و با `WorkContextService` زمینه جاری را دوباره از Cookie محافظت‌شده و دیتابیس اعتبارسنجی می‌کند.
- `CompanyAccessService` مرجع سنجش مجوز نقش در شرکت است. Adapter تعلق `FiscalPeriodId` به شرکت جاری را کنترل می‌کند و سپس مجوز تخصصی عملیات را از سازوکار مشترک می‌سنجد.
- شناسه دوره در اینجا context اجرای مجوز است، نه مجوز مستقل دوره‌ای. سطح دسترسی به وضعیت Open/Closed/Archived از سیاست مشترک `FiscalPeriodSelectionPolicy` می‌آید؛ `TradeAccessIntent.Write` در دوره بسته رد می‌شود.
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


## 11. سیاست‌های اجرایی تأییدشده (D-063)
- Voyage/Shipment یک شرکت مالک دارد؛ شرکت‌های دیگر فقط allocation/share line دارند. سهم تخصیص‌یافته مجوز مشاهده اسناد شرکت مالک نیست.
- ImportCase می‌تواند بین دوره‌ها ادامه یابد؛ هر سند و عملیات تراکنشی FiscalPeriodId دوره انتخاب‌شده همان عملیات را ثبت می‌کند. هیچ انتقال خودکار اسناد بین دوره‌ها انجام نمی‌شود.
- گزارش چندشرکتی عملیاتی از موج اول با مجوز all-or-nothing و audit کامل scope عرضه می‌شود؛ گزارش تلفیقی مالی خارج از این دامنه است.
- entitlement مستقل TRADE_IMPORT از S1 و قبل از دسترسی به قابلیت Trade enforce می‌شود؛ Production در نبود/خطای اعتبارسنجی fail-closed است.
