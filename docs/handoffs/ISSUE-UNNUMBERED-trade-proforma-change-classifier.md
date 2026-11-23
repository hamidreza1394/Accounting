# قاعدهٔ خالص تشخیص «Revision جدید» در Proforma (Trade)

- **Issue:** UNNUMBERED (ادامهٔ Issue #397، مأموریت ادامهٔ توسعه Trade)
- **شاخه:** `feat/trade-proforma-change-classifier`؛ مبنا `main@43bec93d`
- **وضعیت:** `Implemented` (کد خالص) و `Verified` فقط در سطح تست واحد محلی؛ CI هنوز بررسی نشده است.

## خلاصه
قاعدهٔ اعلام‌شدهٔ مالک (تغییر شرایط تجاری = Revision جدید؛ تمدید صرف اعتبار = رویداد جداگانه بدون Revision) به‌صورت منطق خالص و بدون Schema در `ProformaChangeClassifier` کد شد. ورودی: شرایط تجاری قبلی/پیشنهادی (ارز، Incoterm، خطوط چندکالایی با HS Code و Snapshot مقدار/واحد، طرح پرداخت) و تاریخ اعتبار قبلی/جدید. خروجی: `RequiresNewRevision` و `Validity` (`None`/`Extended`/`Shortened`) به‌صورت مستقل.

## تصمیمات و مرزها
- هیچ Entity، DbSet، جدول، Migration، API، UI، مجوز یا تغییر Core/Tenant/Company ساخته نشد؛ `TenantId` و معنای تاریخی آن دست‌نخورده است.
- محل نگهداری `ValidUntil` (Header یا Revision، ابهام B-4) تصمیم‌گیری نشد؛ طبقه‌بند به آن نیاز ندارد.
- **کوتاه‌کردن تاریخ اعتبار** (`Shortened`) عمداً فقط گزارش می‌شود؛ مجاز بودن و نوع رویداد آن تصمیم مالک است (`NeedsApproval`).
- ترتیب خطوط بخشی از سند است؛ جابه‌جایی خطوط تغییر شرایط تجاری حساب می‌شود (پیش‌فرض محافظه‌کارانه؛ در صورت نظر دیگر مالک قابل تغییر است).
- واحد/ضریب Snapshot مقدار در مقایسه لحاظ می‌شود؛ تغییر `TON` به `KG` با مقدار پایهٔ یکسان هم تغییر است.
- نام کالا (`ProductName`) جزء شرایط تجاری مقایسه می‌شود؛ اگر مالک نام را غیرتجاری بداند، فقط حذف یک فیلد لازم است.

## فایل‌ها
- `Modules/Trade/Trade.Application/Proformas/ProformaChangeClassifier.cs` (جدید)
- `tests/Template.Tests/TradeProformaChangeClassifierTests.cs` (جدید؛ ۱۳ تست)

## نتایج واقعی (محلی)
- اجرا شد و موفق: `dotnet restore`، `dotnet build Template.sln -c Release` (۰ خطا)؛ تست‌های جدید ۱۳/۱۳؛ تست‌های #704/#705 (`TradeUnitConversion*`، `TradePaymentPlan*`) ۱۳۰/۱۳۰.
- اجرا نشد: کل `dotnet test` روی SQL Server، تست‌های ClientApp، E2E، `check_static_assets.py` (تغییر کلاینت/دارایی ندارد).

## ریسک و کارهای باقی‌مانده
- ریسک پایین: کلاس خالص و ناخوانده توسط هیچ مسیر اجرایی است.
- باقی‌مانده: Entityهای Proforma/Revision/History پشت گیت‌های S3-0 (G0، D-090..D-096) مسدودند.
