# تحویل وظیفه — ISSUE-728-tenant-dedicated-decisions

- **Issue:** [#728](https://github.com/hamidreza1394/Accounting/issues/728)
- **Agent/مسئول:** ChatGPT
- **تاریخ:** 2026-11-20
- **وضعیت:** PR باز برای بازبینی

## هدف و معیارهای پذیرش
- [x] ممیزی شماره‌ها و محتوای موجود D-087..D-092
- [x] حفظ ADRهای موجود و ثبت تصمیم‌های جدید با شماره‌های آزاد بعدی
- [x] هم‌ترازی DECISIONS، INDEX و WORK_LOG
- [x] ثبت صریح وضعیت پیاده‌سازی و عدم ادعای اجرای قابلیت‌ها
- [ ] اجرای git diff --check و بازبینی نهایی در CI/محیط Git

## خلاصه تغییرات
- D-087 موجود حفظ شد.
- D-088..D-092 به‌دلیل استفاده قبلی، بدون تغییر باقی ماندند.
- D-109..D-114 ثبت شدند: مالکیت FiscalPeriod، Customer Bootstrap Administrator، Database Topology، Database Routing/Migration، Dedicated Endpoint و Deployment/Provisioning Profiles.

## فایل‌های مهم تغییرکرده
- docs/DECISIONS.md — شش تصمیم جدید D-109..D-114.
- docs/INDEX.md — به‌روزرسانی دامنه شماره‌گذاری ADRها و خلاصه تصمیم‌های جدید.
- docs/WORK_LOG.md — ثبت سابقه ممیزی و مستندسازی.
- docs/handoffs/ISSUE-728-tenant-dedicated-decisions.md — این گزارش تحویل.

## تصمیم‌های فنی
- Shared و Dedicated یک مدل منطقی و یک مسیر Provisioning دارند.
- توپولوژی دیتابیس قابل انتخاب است: Tenant، Company، یا Company + FiscalPeriod.
- مقصد دیتابیس فقط از نگاشت مورد اعتماد سمت سرور resolve می‌شود.
- Bootstrap مدیر مشتری مستقل از Vendor و بدون رمز پیش‌فرض مشترک است.
- این تغییر صرفاً مستنداتی است و قابلیت اجرایی را فعال نمی‌کند.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| Build | اجرا نشده | تغییر فقط مستندات است |
| Test | اجرا نشده | تغییر فقط مستندات است |
| git diff --check | اجرا نشده | باید در بازبینی شاخه/CI اجرا شود |
| بررسی شماره ADRها | انجام شد | D-087 موجود؛ D-088..D-092 اشغال؛ D-109..D-114 آزاد بعدی |

## امنیت، داده و سازگاری
- هیچ Schema، Migration، داده، secret، Connection String، API یا تنظیم عملیاتی تغییر نکرد.
- D-089 موجود درباره منبع ایجاد Tenant حفظ شده؛ Bootstrap مدیر اولیه Tenant جدید ایجاد نمی‌کند.
- Host/Domain به‌عنوان مجوز یا مرز Tenant تعریف نشده است.
- threat model و طراحی اجرایی Bootstrap و انتقال دیتابیس پیش از پیاده‌سازی لازم است.

## ریسک‌ها و کارهای باقی‌مانده
- پیاده‌سازی قابلیت‌ها نیازمند Issueهای اجرایی مستقل و بررسی کد/Schema است.
- انتخاب topology برای هر مشتری باید با هزینه Backup/Restore و عملیات سنجیده شود.
- جزئیات Registry/Resolver و Cutover باید قبل از Migration یا جابه‌جایی داده طراحی و آزموده شوند.

## شناسه‌های Git
- Branch: docs/issue-728-tenant-dedicated-decisions
- Commit: `7656ad6c1734c19630beab79f6ca9c477c752741`
- Pull Request: [#729](https://github.com/hamidreza1394/Accounting/pull/729)
