# راهبرد آزمون

## رویکرد
- **TDD**: برای منطق جدید و پرریسک، ابتدا تست شکست‌خورده، سپس پیاده‌سازی و بازآرایی. لازم نیست هر تغییر نمایشی با TDD کامل انجام شود.
- **BDD**: معیارهای پذیرش با زبان قابل فهم کسب‌وکار نوشته شوند؛ در صورت نیاز به سناریوهای Given/When/Then از ابزار مناسب استفاده شود.
- Unit Test برای قوانین Domain/Application بدون دیتابیس یا شبکه.
- Integration Test برای EF Core، SQL Server، احراز هویت، Middleware و مسیرهای HTTP.
- تست‌های end-to-end فقط برای جریان‌های مهم و با محیط قابل تکرار.

## ساختار
`tests/Template.Tests` فعلاً یک تست پایه برای رفتار تابع Hashing قدیمی دارد. این تست فقط نمونه اتصال xUnit و CI است؛ پوشش تجاری معنادار محسوب نمی‌شود. تست‌های بعدی باید با Use Caseهای واقعی و مستقل از جزئیات پیاده‌سازی نوشته شوند.

## فرمان‌ها
```bash
dotnet restore Template.sln
dotnet build Template.sln --no-restore --configuration Release
dotnet test Template.sln --no-build --configuration Release
```

## قاعده گزارش
نتیجه واقعی هر دستور، کد خروج، تست‌های اجراشده/نشده و علت شکست ثبت شود. اجرای موفق Build جایگزین تست عملکردی و بازبینی امنیتی نیست.


## تست‌های راه‌اندازی پایگاه داده (Issue #22)
- `DatabaseSetupValidationTests` ورودی‌های فاقد سرور، روش احراز هویت نامعتبر و SQL Login بدون رمز را بدون اتصال به SQL Server بررسی می‌کند.
- این تست‌ها Integration Test نیستند؛ مسیرهای Windows Authentication، SQL Login معتبر، ایجاد دیتابیس، اعمال Migration، Data Protection و دیتابیس موجود باید روی SQL Server واقعی در محیط ایزوله آزموده شوند.
- تا زمان اجرای CI و تست SQL Server، پیاده‌سازی راه‌اندازی اولیه تأیید نهایی نشده است.

## تست‌های جریان ورود (Issue #30)
- `LoginAuthenticationTests` جریان PageModel را با `DatabaseContext` روی provider حافظه‌ای (`Microsoft.EntityFrameworkCore.InMemory`، فقط آزمون) و یک `IAuthenticationService` ثبت‌کننده اجرا می‌کند: فیلد خالی، نام کاربری ناموجود، رمز اشتباه، حساب غیرفعال، قفل موقت، ورود موفق، Remember Me، ثبت `LoginLog`/`LastLoginDateTime`، ارتقای هش قدیمی، حساب زیرساختی، `ReturnUrl` محلی و خارجی، و خطای زیرساخت.
- صدور کوکی با ثبت فراخوانی `SignInAsync` (scheme و `IsPersistent`) بررسی می‌شود؛ این آزمون جای آزمون HTTP واقعی روی مرورگر را نمی‌گیرد.
- `PasswordHashServiceTests` قالب جدید، salt یکتا، رد رمز اشتباه، پذیرش و درخواست ارتقای هش قدیمی و رد مقدار نامعتبر را پوشش می‌دهد.
- `LoginConfigurationTests` به‌صورت ایستا بررسی می‌کند که مقصد موقت `[Authorize]` دارد و فرم/ماژول حسابداری ندارد، صفحه ورود Anti-forgery و toggle رمز را حفظ کرده، و تنظیمات Cookie/ترتیب Middleware تغییر نکرده است.
- اجرای Migration روی SQL Server واقعی همچنان آزمون‌نشده است.
