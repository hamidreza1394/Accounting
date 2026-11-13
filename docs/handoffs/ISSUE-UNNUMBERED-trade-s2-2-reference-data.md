# Handoff — Trade S2-2: داده‌های مرجع محلی فقط‌خواندنی (UNNUMBERED)

- **Issue:** UNNUMBERED (شمارهٔ جعلی ساخته نشد). مبنا: D-081 (`Accepted`)، S2-2 در `docs/ROADMAP.md`.
- **خلاصه:** Country، Currency، Unit و Incoterm به‌صورت داده درون‌کد، فقط‌خواندنی و بدون جدول/Migration/API/منو در `Modules/Trade/Trade.Application/ReferenceData`. قرارداد: `CountryRef`، `CurrencyRef` (با `MinorUnits`)، `UnitRef`، `IncotermRef` (با `SeaOnly`) و `TradeReferenceData` (لیست‌های فقط‌خواندنی و `TryFind*` غیرحساس به حروف با trim).
- **محتوا:** ۲۴۹ کشور ISO 3166-1 alpha-2 با نام فارسی؛ ارزهای ISO 4217 (۳۸ مورد با Minor Unit)؛ ۱۳ واحد (`KG, TON, G, M, M2, M3, L, PCS, SET, CARTON, PALLET, CNT20, CNT40`؛ سازگار با `Product.BaseUnitCode`)؛ ۱۱ Incoterm نسخهٔ 2020.
- **فایل‌ها:** `ReferenceData/{ReferenceRecords,TradeReferenceData,CountryData,MiscData}.cs`، `tests/Template.Tests/TradeReferenceDataTests.cs`.
- **تصمیم‌ها:** بدون جدول (D-081)؛ کدهای واحد حروف بزرگ ASCII؛ داده دستی (منبع شبکه مجاز نیست).
- **آزمون واقعی:** Restore و Build Release بدون خطا؛ `dotnet test --filter Trade` = ۹۲ تست، ۹۱ موفق، ۱ Skip، ۰ شکست (پیش از افزودن چند Assert منفی؛ اجرای نهایی در گزارش PR). SQL Server/E2E/ClientApp اجرا نشد (لازم نبود).
- **بازبینی:** `code-reviewer` مستقل؛ بدون یافتهٔ Critical/High؛ موارد Medium/Low (مستندات، Assert منفی، trim، `MaybeNullWhen`) اصلاح شد. نام‌های فارسی را یک فارسی‌زبان مرور کند (نمونه: «چک»، «واتیکان»).
- **ریسک/باقی‌مانده:** دقت داده دستی؛ مصرف در S2-3/S2-4 (Combobox کشور/واحد).
