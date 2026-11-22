# تحویل وظیفه — ISSUE-UNNUMBERED-trade-product-update-forbidden-chars-tests

- **Issue:** `UNNUMBERED` (Workstream B: یکسان‌سازی اعتبارسنجی Party و Product)
- **Agent/مسئول:** Claude (Trade)
- **تاریخ:** 2026-11-22
- **وضعیت:** انجام‌شده (فقط تست و سند)

## طبقه‌بندی یافته‌ها
**نقص روشن (قبلاً در PR #750):** `PartyService.HasForbiddenCharacters` نویسه‌های `U+061C`, `U+2028`, `U+2029`, `U+FEFF` را رد نمی‌کرد، ولی Product می‌کرد.

**رفتار عمدی/مصوب:** ZWNJ (`U+200C`) در هر دو مجاز است و تغییر نکرد. Create و Update هر دو از یک `Validate` مشترک استفاده می‌کنند.

**شکاف تست (این PR):** Product فقط برای Create تست نویسه‌های ممنوع داشت. تست Update برای code/name و حفظ دادهٔ ذخیره‌شده و ZWNJ اضافه شد.

**نیازمند سیاست مالک (بدون تغییر):**
- `U+200B` (ZWSP) و `U+2060` (word joiner) در `code` پذیرفته می‌شوند؛ کدهای ظاهراً یکسان می‌توانند از یکتایی عبور کنند.
- `code` فقط Trim می‌شود: بدون نرمال‌سازی ارقام فارسی/عربی یا ی/ک عربی، و بدون normalization یونیکد.
- `TradeDocumentNumber` ZWNJ را رد می‌کند ولی Party/Product می‌پذیرند.
هیچ نرمال‌سازی یا ردِ نویسهٔ جدید بدون سیاست مصوب اضافه نشد.

## فایل‌ها
- `tests/Template.Tests/TradeProductServiceTests.cs` — ۷ مورد جدید (۶ InlineData + ۱ Fact)
- این سند

## بررسی و آزمون
| فرمان | نتیجه |
|---|---|
| `dotnet build Template.sln --no-restore -c Release` | موفق، 0 Error |
| `dotnet test --project tests/Template.Tests --no-build -c Release --filter-class "*TradeProductServiceTests*"` | 38 اجرا، 38 موفق، 0 ناموفق |
| بررسی جهش: حذف ۴ نویسه از Product به‌طور موقت | 8 تست ناموفق (آزمون حساس است)؛ کد تولیدی بازگردانده شد |
| تست کامل سوییت | اجرا نشد؛ CI تعیین‌کننده است |
| `git diff --check` | موفق |

کد تولیدی، Schema، Migration، Vendor و Licensing تغییر نکرد.
