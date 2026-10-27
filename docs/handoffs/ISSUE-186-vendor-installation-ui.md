# Issue #186 — رابط کاربری مدیریت نصب‌های Vendor

## هدف
افزودن عملیات ثبت، ویرایش metadata و تغییر وضعیت نصب از رابط کاربری Vendor، با استفاده از APIهای موجود و بدون provision کردن شرکت یا دیتابیس حسابداری مشتری.

## تغییرات
- فرم RTL ثبت نصب با انتخاب مشتری از API صفحه‌بندی‌شده؛ گزینه‌های مشتری در صفحات 100تایی قابل پیمایش‌اند.
- انتخاب محیط نصب با `ManagedSelect` و گزینه‌های allow-list شده: Production، Staging و Development؛ مقدار پیش‌فرض Production.
- ویرایش metadata با RowVersion جاری و بدون تغییر CustomerId نصب موجود.
- تغییر وضعیت فقط برای نصب‌های Active/Suspended و از مسیر API واقعی؛ وضعیت‌های دیگر کنترل تغییر وضعیت ندارند.
- هر POST/PUT با antiforgery token تازه و same-origin ارسال می‌شود.
- پیام‌های فارسی برای خطاهای 400/403/409/429/500، شامل `stale_row_version` و `invalid_status_transition`.
- نمایش دکمه‌های نوشتن فقط با permission claimهای مناسب؛ مجوز نهایی سمت سرور است.

## تصمیم‌های محافظه‌کارانه
- ایجاد نصب نیازمند مجوز `vendor.installations.manage` و `vendor.customers.read` در UI است، چون باید مشتری واقعی انتخاب شود؛ در نبود CustomersRead، دکمهٔ ثبت نصب غیرفعال می‌شود. API همچنان مرجع نهایی authorization است.
- این فرم فقط رکورد نصب Vendor را ثبت می‌کند؛ هیچ دیتابیس حسابداری مشتری، سرور یا اعتبارنامه‌ای ایجاد/تغییر نمی‌شود.
- کد نصب، نام نصب و محیط تنها metadata قابل ویرایش هستند؛ انتقال وضعیت جداگانه و با RowVersion انجام می‌شود.

## تست و اعتبارسنجی
- Vitest: ثبت موفق، مشتری واقعی و محیط پیش‌فرض، نبود مجوز مشتری، RowVersion و conflict، تعلیق نصب و conflict در تغییر وضعیت.
- Vendor CI باید type-check، تست UI، تست‌های SQL Server، EF snapshot و publish را روی head نهایی سبز کند.
- .NET CI باید Build/Test، اسکن اسرار، گاردهای انتشار و E2E را سبز کند.
- تا پایان اجرای CI روی آخرین head، وضعیت `Implemented / Not yet Verified` باقی می‌ماند.
