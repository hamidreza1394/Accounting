# تحویل وظیفه — ISSUE-201-vendor-maker-checker

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/201
- **Branch:** feat/vendor-maker-checker-clean
- **Pull Request:** https://github.com/hamidreza1394/Accounting/pull/207
- **وضعیت:** Verified و ادغام‌شده در PR #207
- **Merge commit:** `d644ab5d36d00b1ce3763b64f17fd6fe7ef9fbac`

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
- Build/test محلی: اجرا نشده؛ checkout محلی مخزن در این محیط موجود نبود.
- Vendor CI [40570240409](https://github.com/hamidreza1394/Accounting/actions/runs/40570240409): موفق؛ TypeScript strict، Vitest، asset budget، build/test Vendor، SQL Server integration، EF snapshot guard و انتشار Vendor.
- .NET CI [40570240205](https://github.com/hamidreza1394/Accounting/actions/runs/40570240205): موفق؛ client، secret scan، build/test، E2E، انتشار Linux و Windows.
- شواهد بالا روی HEAD نهایی PR #207 اجرا شده‌اند؛ کد merge commit `d644ab5d36d00b1ce3763b64f17fd6fe7ef9fbac` است.

## ریسک‌های باقی‌مانده
- این گردش فعلاً تغییر وضعیت Customer/Installation را پوشش می‌دهد؛ Maker-Checker برای صدور/ابطال امضاشدهٔ لایسنس پس از پیاده‌سازی signing service باید اضافه شود.
- checkpoint مستقل Audit، KMS/HSM یا signing service، activation آفلاین و آزمون Production همچنان blockers جداگانه‌اند.
- صف تأییدها اکنون صفحه‌بندی server-side دارد (۲۵/۵۰/۱۰۰ مورد در هر صفحه)؛ رفتار مرز آخرین صفحه پس از تصمیم نیز باید توسط UI/Vitest و CI نهایی تأیید شود.