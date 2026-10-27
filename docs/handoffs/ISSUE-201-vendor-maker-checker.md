# تحویل وظیفه — ISSUE-201-vendor-maker-checker

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/201
- **Branch:** feat/vendor-maker-checker-main
- **Pull Request:** https://github.com/hamidreza1394/Accounting/pull/206
- **وضعیت:** پیاده‌سازی اولیه در حال بازبینی؛ CI هنوز اجرا نشده

## محدوده و تصمیم
- فقط Vendor و تست‌ها/مستندات اختصاصی آن تغییر می‌کنند؛ دیتابیس حسابداری مشتری دست‌نخورده می‌ماند.
- انتقال وضعیت مشتری/نصب فقط از طریق درخواست تأیید مجاز است؛ endpointهای قدیمی مستقیم پاسخ 409 با approval_required می‌دهند.
- درخواست‌کننده نمی‌تواند درخواست خودش را تأیید کند؛ تصمیم نهایی فقط در تراکنش واحد همراه Audit ثبت می‌شود.
- اگر RowVersion منبع بین درخواست و تصمیم تغییر کند، درخواست به Stale منتقل می‌شود و تغییر منبع اعمال نمی‌شود.

## تغییرات
- موجودیت VendorApprovalRequest و Migration مستقل 20261027193000_VendorApprovalRequests با RowVersion، وضعیت محدود و ایندکس filtered یکتا برای یک درخواست Pending به‌ازای هر منبع.
- APIهای GET صف، submit درخواست تغییر وضعیت و approve/reject.
- permissionهای vendor.approvals.read/request/review.
- UI فارسی RTL برای ثبت درخواست تغییر وضعیت نصب و صف بررسی/تأیید/رد.
- تست SQL Server برای schema و سناریوهای HTTP: منع مسیر مستقیم، درخواست تکراری، منع self-approval، تأیید توسط مدیر MFA مستقل، اعمال وضعیت همراه Audit و stale resource.
- تست UI برای ارسال درخواست و صف تأیید.

## اعتبارسنجی
- Build محلی و dotnet test محلی: اجرا نشده؛ محیط جاری checkout محلی مخزن ندارد.
- CI: هنوز اجرا نشده؛ تا سبز شدن Vendor CI و .NET CI هیچ‌کدام از این تغییرات Verified نیستند.
- پس از PR باید بررسی شود: strict TypeScript/Vitest, .NET build/test, SQL Server migration integration, pending EF model changes, publish Linux/Windows, E2E, secret scan.

## ریسک‌های باقی‌مانده
- این گردش فعلاً تغییر وضعیت Customer/Installation را پوشش می‌دهد؛ Maker-Checker برای صدور/ابطال امضاشدهٔ لایسنس پس از پیاده‌سازی signing service باید اضافه شود.
- checkpoint مستقل Audit، KMS/HSM یا signing service، activation آفلاین و آزمون Production همچنان blockers جداگانه‌اند.
- فهرست تأییدها فعلاً تا 100 مورد اول را نمایش می‌دهد؛ صفحه‌بندی صف باید در ادامه اضافه/آزموده شود اگر حجم عملیاتی آن را ایجاب کند.