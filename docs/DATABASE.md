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


## مدل چندمستاجری افزوده‌شده در Issue #16
- `Company`: محدوده Tenant و ریشه داده شرکت.
- `UserCompanyMembership`: اتصال شناسه کاربر مرکزی به شرکت و نقش همان شرکت؛ ترکیب UserId/TenantId یکتا است.
- `FiscalPeriod`: دوره مالی با تاریخ شروع/پایان و وضعیت Open/Closed/Archived.
- `CompanyRole`: نقش مستقل در محدوده شرکت.
- `PermissionDefinition` و `CompanyRolePermission`: کاتالوگ مجوز و تخصیص به نقش.
- `SecurityAuditEvent`: ثبت رویداد امنیتی بدون ذخیره رمز یا توکن.
- نگاشت‌ها در `Infrastructure/Persistence/Configurations` قرار دارند و به‌وسیله `ApplyConfigurationsFromAssembly` کشف می‌شوند.

**محدودیت فعلی:** مدل‌ها به DbContext افزوده شده‌اند، اما هنوز Migration بازبینی‌شده تولید نشده است. برای دیتابیس موجود، افزودن DbSet به‌تنهایی جدول جدید ایجاد نمی‌کند. همچنین `Database.EnsureCreated()` در سازنده DbContext هنوز بدهی فنی قدیمی است و باید در Issue مستقل، همراه با تعیین baseline/migration و مسیر حفظ داده‌ها حذف شود. تا زمان انجام این کار، این تغییر را Migration-ready یا production-ready تلقی نکنید.
