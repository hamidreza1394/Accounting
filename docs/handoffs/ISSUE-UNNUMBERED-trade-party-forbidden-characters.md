# هم‌ترازی فیلتر نویسه‌های ممنوع Party با Product

- **Issue:** UNNUMBERED (یافتهٔ بازبینی مستقل پایه‌های Trade، Workstream D)
- **مبنا:** `main@5484758722d0a316511806cf3097c5c691875599`

## خلاصه
`PartyService.HasForbiddenCharacters` نویسه‌های `U+061C` (Arabic Letter Mark)، `U+2028`، `U+2029` و `U+FEFF` را رد نمی‌کرد، در حالی که `ProductService` رد می‌کند. این چهار نویسه به فهرست Party افزوده شد. ZWNJ (`U+200C`) همچنان مجاز است. بدون تغییر Schema، API یا قرارداد.

## فایل‌های تغییرکرده
- `Modules/Trade/Trade.Application/Parties/PartyService.cs` — افزودن چهار نویسه به الگوی رد.
- `tests/Template.Tests/TradePartyServiceTests.cs` — چهار `InlineData` به تست Create؛ یک تست Update برای `code` و `legalName` (۶ حالت) که دادهٔ ذخیره‌شده را بدون تغییر می‌خواهد؛ یک تست پذیرش ZWNJ در Update.
- `docs/handoffs/ISSUE-UNNUMBERED-trade-party-forbidden-characters.md` — همین سند.

## تصمیم‌ها
- فقط هم‌ترازی با رفتار موجود Product؛ نویسهٔ نامرئی Format دیگر (`U+200B`، `U+2060`) و نرمال‌سازی کد تغییر نکرد (تصمیم سیاستی مالک).
- `Trim()` نویسه‌های `U+2028/U+2029` را در ابتدا/انتهای متن حذف می‌کند و فقط در وسط متن رد می‌شوند؛ رفتار Product هم همین است و تغییر داده نشد.
- `docs/WORK_LOG.md` عمداً تغییر نکرد (فایل مشترک پرتعارض؛ انحراف آگاهانه از `AGENTS.md` بخش ۵؛ افزودن ردیف پس از تعیین ترتیب ادغام).

## تست‌ها (اجرای واقعی، SDK 10.0.404)
- پیش از اصلاح (Red): `TradePartyServiceTests` — ۳۷ تست، ۴ شکست (چهار حالت جدید).
- پس از اصلاح (Green): `TradePartyServiceTests` — ۴۴ تست، ۴۴ موفق، ۰ شکست.
- کل راه‌حل (`dotnet test Template.sln -c Release`): کل ۱۷۵۱، موفق ۱۶۱۲، شکست ۲، رد (Skip) ۱۳۷.
  - ۲ شکست: `SafeExceptionRealApplicationTests.ApplicationPipeline_DoesNotExposeMiddlewareFailure` (محیط `Development`) با `InvalidOperationException: Relational-specific methods…`. روی `main` بدون این تغییر هم همین ۲ شکست رخ داد (با `git stash` تأیید شد)؛ نامرتبط با این PR و دست‌نخورده.
  - ۱۳۷ رد: تست‌های وابسته به SQL Server که بدون SQL Server «اجرا نشده» حساب می‌شوند، نه پاس.
- `python3 scripts/check_static_assets.py`: موفق.
- `dotnet restore/build -c Release`: ۰ خطا؛ هشدارهای CA موجود از قبل (بدون هشدار جدید ناشی از این تغییر در فایل‌های تغییرکرده).
- اجرا نشد: `npm` (کلاینت تغییری ندارد)، تست‌های SQL Server.

## ریسک‌ها
- متن چسبانده‌شده از Word/PDF حاوی `U+FEFF` یا `U+2028` از این پس برای Party رد می‌شود (همان هدف و هم‌تراز با Product).
- `HasForbiddenCharacters` در دو سرویس کپی دستی است؛ ادغام در helper مشترک موکول است (YAGNI).

## بازبینی
`code-reviewer` فقط‌خواندنی: بدون یافتهٔ Critical/High؛ یافتهٔ Medium (نبود تست Update) و Low (ZWNJ در Update) در همین PR اصلاح شد؛ یافتهٔ Trim/DRY ثبت و تغییر داده نشد.
