# تحویل Issue #163 — مدل دامنه و API امن Vendor

## هدف
تبدیل زیرساخت RBAC و سیاست منبع‌محور PR #162 به اولین مسیر واقعی مدیریت داده‌های تجاری در دیتابیس مستقل Vendor.

## تغییرهای شاخه
- مدل‌های VendorCustomer، VendorInstallation، VendorContract و VendorLicense در پروژه Persistence.
- DbSet و پیکربندی EF Core، شامل محدودیت حذف، indexهای یکتا و concurrency token از نوع rowversion.
- Migration با شناسه 20261027060000_VendorBusinessDomain و تغییر Snapshot.
- فهرست و جزئیات منابع در /api/vendor/customers، /api/vendor/installations، /api/vendor/contracts و /api/vendor/licenses.
- ایجاد رکورد برای هر چهار نوع منبع با anti-forgery، اعتبارسنجی، بررسی وابستگی به مشتری و ثبت audit در همان تراکنش.
- evaluator واقعی که وجود رکورد و رابطهٔ مشتریِ قرارداد/نصب/لایسنس را از دیتابیس بررسی می‌کند؛ شناسهٔ والد ارسالی به‌تنهایی مدرک مجوز نیست.
- جداسازی مسیرهای دادهٔ تجاری از /api/admin/* تا تست امنیتی مدیریت ادمین به‌درستی فقط روی عملیات RBAC اعمال شود.

## محدودیت‌های فعلی
- APIهای ویرایش و انتقال وضعیت هنوز پیاده‌سازی نشده‌اند.
- تست integration منفی cross-resource روی SQL Server هنوز اضافه/تأیید نشده است.
- Snapshot باید با dotnet ef migrations has-pending-model-changes در CI تأیید شود.
- این مدل‌ها metadata مدیریت مجوز هستند؛ صدور امضاشده، پروتکل آفلاین، نگهداری کلید در KMS/HSM، checkpoint مستقل و maker-checker خارج از این شاخه‌اند.
- UI تجاری هنوز به APIها وصل نشده است.

## اعتبارسنجی
Build محلی در این محیط اجرا نشده است. PR باید Draft بماند تا Vendor CI و .NET CI روی آخرین SHA موفق شوند و شکست احتمالی migration snapshot اصلاح شود.

## مرز داده
این Migration فقط برای VendorDbContext است. دیتابیس و Migrationهای حسابداری مشتری دست‌نخورده باقی می‌مانند.
