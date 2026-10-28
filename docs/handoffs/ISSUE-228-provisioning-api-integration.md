# تحویل وظیفه — ISSUE-228-provisioning-api-integration

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/228
- **وضعیت:** آزمون integration افزوده شد؛ منتظر CI

## تغییرات
- گسترش تست HTTP/SQL Server موجود برای provisioning API.
- پوشش درخواست بدون Anti-forgery token، ایجاد موفق درخواست، replay با RequestId یکسان، conflict با همان RequestId و payload متفاوت، رد نصب متعلق به مشتری دیگر، یکتایی ذخیره و ثبت audit.
- بررسی اینکه درخواست در وضعیت AwaitingSigner می‌ماند و payload ذخیره‌شده فیلد رمز ندارد.

## محدودیت‌ها
- آزمون‌ها به SQL Server آزمایشی Vendor در CI وابسته‌اند؛ تست بدون SQL Server واقعی نباید به‌عنوان موفق گزارش شود.
- امضاکنندهٔ تولیدی و مصرف‌کنندهٔ سمت مشتری هنوز موجود نیستند؛ تست این قابلیت‌ها را ادعا نمی‌کند.

## فایل‌های تغییرکرده
- tests/Vendor.Host.Tests/VendorCustomerMutationHttpIntegrationTests.cs
- این handoff و docs/WORK_LOG.md

## آزمون
- اجرای CI روی PR لازم است؛ در انتظار نتیجهٔ واقعی.
- Branch: test/issue-228-provisioning-api-integration
- PR: در حال ایجاد
