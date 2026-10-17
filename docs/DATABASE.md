# راهنمای پایگاه داده

## وضعیت موجود
- `Infrastructure/Persistence/DatabaseContext.cs` شامل DbContext است.
- تنظیمات Entityها در `Infrastructure/Persistence/Configurations` قرار دارند.
- SQL Server و EF Core استفاده می‌شوند.
- استفاده از Lazy Loading Proxies فعال است؛ وابستگی به Lazy Loading باید در فاز دیتابیس ارزیابی شود، چون ممکن است Queryهای پنهان و N+1 ایجاد کند.

## استانداردها
- Mapping صریح برای طول رشته، Required/Optional، Precision، Index، کلید و رفتار حذف.
- از Migrationهای EF Core برای تغییر schema استفاده شود؛ تغییر مستقیم دیتابیس بدون Migration ثبت‌شده ممنوع است.
- Migration تولیدی قبل از استقرار بازبینی شود و در محیط حساس ترجیحاً SQL Script تولید و بررسی شود.
- Queryهای صفحه‌بندی‌شده باید ترتیب پایدار و سقف تعداد رکورد داشته باشند.
- از `AsNoTracking` برای خواندن صرف در صورت مناسب‌بودن استفاده شود.
- DbContext به‌صورت Scoped بماند و بین Threadها به اشتراک گذاشته نشود.
- تراکنش و رفتار Cascade براساس قواعد دامنه و تست مشخص شود.
- Connection String واقعی در فایل tracked قرار نگیرد؛ از `ConnectionStrings__ConnectionString` یا Secret Store استفاده شود.


## SaaS چندشرکتی
مدل پایه جدید در `Domain/SaaS` و `Infrastructure/Persistence/SaaS/SaaSIdentityContext.cs` قرار دارد. جزئیات اتصال محلی/ابر، TenantId، فیلترها و دستورات Migration در [SAAS_DATABASE.md](SAAS_DATABASE.md) آمده است. Migration تولیدی هنوز ساخته نشده است.
