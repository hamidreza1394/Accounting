# تحویل وظیفه — Issue #404: Trade persistence host registration

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/404
- **Agent/مسئول:** ChatGPT
- **تاریخ:** 2026-10-31
- **وضعیت:** پیاده‌سازی اولیه ثبت شده؛ منتظر CI و بازبینی نهایی

## هدف و معیارهای پذیرش
- [x] ثبت TradeDbContext در Host فقط وقتی Connection String موجود است.
- [x] lifetime برابر Scoped و استفاده از پیکربندی SQL Server مشترک.
- [x] migration assembly و history table مستقل.
- [x] افزودن EF design-time factory بدون boot کردن Server.
- [x] تست‌های واحد برای lifetime و options.
- [ ] Build و تست .NET در CI سبز؛ هنوز نتیجه‌ای برای head این شاخه دریافت نشده است.
- [ ] بازبینی هم‌زمانی با تغییرات S1 در Program.cs هنگام ادغام.

## خلاصه تغییرات
- Extension ثبت DI در Trade.Infrastructure اضافه شد.
- Server به Trade.Infrastructure ارجاع می‌دهد و ثبت TradeDbContext را فقط داخل شاخه `!setupRequired` انجام می‌دهد.
- Factory طراحی EF با اولویت اتصال اختصاصی Trade و سپس اتصال مشترک اضافه شد.
- تست‌ها عدم اتصال به SQL Server هنگام resolve، lifetime و تنظیمات migration history را پوشش می‌دهند.
- هیچ Entity تجاری، Migration یا تغییر Schema ایجاد نشده است.

## فایل‌های مهم تغییرکرده
- `Modules/Trade/Trade.Infrastructure/Persistence/TradePersistenceServiceCollectionExtensions.cs`
- `Modules/Trade/Trade.Infrastructure/Persistence/TradeDesignTimeDbContextFactory.cs`
- `Server/Server.csproj`
- `Server/Program.cs`
- `tests/Template.Tests/TradePersistenceRegistrationTests.cs`
- `docs/TRADE_MODULE.md`
- `docs/DEVELOPMENT.md`

## تصمیم‌های فنی
- دیتابیس فیزیکی مشترک حفظ شد؛ schema و history مستقل Trade دست‌نخورده باقی ماند.
- Registration در setup mode انجام نمی‌شود تا ساخت DbContext به اتصال ناقص وابسته نشود.
- Connection string پیش‌فرض Factory فقط برای LocalDB توسعه است و runtime را پیکربندی نمی‌کند.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| Build .NET | اجرا نشده | از این محیط Build محلی انجام نشده؛ منتظر CI |
| تست .NET | اجرا نشده | از این محیط تست محلی انجام نشده؛ منتظر CI |
| SQL Server | اجرا نشده | این برش هیچ query یا migration اجرا نمی‌کند |
| Migration روی دیتابیس مشتری | اجرا نشده | خارج از محدوده و ممنوع بدون تأیید مستقل |

## امنیت، داده و سازگاری
- هیچ connection string واقعی یا secret در کد اضافه نشده است.
- Factory فقط provider را پیکربندی می‌کند؛ ساخت migration به معنی اتصال/اجرای آن نیست.
- در حال حاضر هیچ جدول یا داده مشتری تغییر نکرده است.

## ریسک‌ها و کارهای باقی‌مانده
- نتیجه CI و compile API مربوط به `RelationalOptionsExtension` باید بررسی شود.
- در صورت هم‌زمانی S1، تغییرات هر دو شاخه در `Server/Program.cs` باید با merge دقیق ترکیب شوند.
- این PR فقط زیرساخت persistence است؛ مدل Party/Product و سایر داده‌های پایه در S2.2 انجام می‌شوند.

## شناسه‌های Git
- Branch: `feat/issue-404-trade-persistence-registration-v2`
- Commitهای اولیه: `0b008e9`, `9ca353a`, `c179b91`, `faa5bba`, `799344c`
- Pull Request: پس از ثبت مستندات ساخته می‌شود.
