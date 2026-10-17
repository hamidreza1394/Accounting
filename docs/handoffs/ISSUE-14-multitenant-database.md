# تحویل وظیفه — ISSUE-14-multitenant-database

- Issue: https://github.com/hamidreza1394/Accounting/issues/14
- تاریخ: 2026-10-17
- وضعیت: پایه مدل اضافه شد؛ Migration و تست اجرایی باقی است.

## تغییرات
- افزودن مدل‌های SaaS: شرکت، عضویت، دوره مالی، نقش شرکت، مجوز، تخصیص مجوز، سرسند و AuditLog.
- افزودن ApplicationUser، SaaSIdentityContext و ثبت ASP.NET Core Identity UserManager.
- جداسازی schema و Migration History با نام saas.
- تنظیم connection string محلی از appsettings.Development و پشتیبانی override با environment/User Secrets.
- ارتقا TargetFramework، SDK pin، پکیج‌های Microsoft و CI به .NET 10.

## آزمون
- ساختار GitHub و فایل‌های اصلی بررسی شد.
- Restore/Build/Test و تولید Migration: اجرا نشده‌اند؛ محیط این تغییر SDK/CLI اجرایی ندارد.

## ریسک‌ها و ادامه کار
- ابتدا CI باید Restore/Build/Test واقعی را اجرا کند؛ احتمال نیاز به اصلاح EF mapping وجود دارد.
- Migration تولیدی را با EF CLI هم‌نسخه بساز و SQL را بازبینی کن.
- ورود legacy هنوز فعال است؛ Identity registration به معنی مهاجرت کامل login نیست.
- تست‌های integration برای جداسازی tenant، عضویت لغوشده و FK مرکب اضافه شوند.

Branch: `feature/issue-14-multitenant-database`
