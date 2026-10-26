# تحویل وظیفه — ISSUE-130-vendor-rtl-managed-select

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/130
- **Branch:** `feat/issue-130-vendor-rtl-managed-select`
- **Base:** `main` at `6bdfe0d2219419050def70a07a1e4892c5b7bd48`
- **Status:** Implemented in branch; awaiting CI. Not Production-ready.

## هدف
استانداردسازی RTL فارسی در Vendor و ساخت فیلد انتخابی مشترک که بعداً بتواند با فهرست‌های مرجع سمت سرور کار کند.

## تغییرات
- حفظ `lang="fa"` و `dir="rtl"` موجود در `Vendor/Vendor.Web/index.html`.
- افزودن قواعد CSS برای تراز RTL و خواص منطقی؛ نام کاربری با `dir="auto"` و رمز عبور با `dir="ltr"`.
- افزودن `Vendor/Vendor.Web/src/components/forms/managed-select.tsx` با پیش‌فرض اولین گزینه معتبر، پشتیبانی از controlled/uncontrolled value، حالت disabled برای فهرست خالی، label/error accessible و دکمه مدیریت گزینه که فقط با مجوز صریح و callback نمایش داده می‌شود.
- افزودن تست‌های کامپوننت و assertion جهت ورودی‌ها در تست ورود.
- به‌روزرسانی Vendor README، PROJECT_STATUS و WORK_LOG.

## مرز معماری
فهرست‌های مرجع قابل مدیریت نیازمند API واقعی و authorization سمت سرورند؛ کامپوننت به‌تنهایی چیزی را ذخیره نمی‌کند. شرکت‌های حسابداری و رابطه‌های مادر/زیرمجموعه متعلق به دیتابیس نصب مشتری‌اند؛ Vendor نباید بدون تصمیم provisioning مستقل، این داده‌ها را مستقیماً در دیتابیس خود بسازد.

## اعتبارسنجی
- تست/Build محلی: اجرا نشد؛ محیط این Agent به GitHub دسترسی شبکه‌ای/clone ندارد.
- Vendor CI: باید پس از ایجاد PR بررسی شود.
- وضعیت: **Implemented in branch / Not yet Verified**.

## گام بعدی
پس از CI، تکمیل bootstrap امن مدیران و RBAC سمت سرور، سپس تثبیت/تأیید مدل مشتری تجاری، نصب، قرارداد و مجوز قبل از ساخت CRUD واقعی.
