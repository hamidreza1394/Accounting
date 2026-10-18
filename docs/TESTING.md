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
