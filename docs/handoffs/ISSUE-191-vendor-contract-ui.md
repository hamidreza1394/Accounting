# Issue #191 — رابط کاربری قراردادهای Draft در Vendor

## هدف
تکمیل عملیات موجود API برای ایجاد قرارداد و ویرایش قراردادهای پیش‌نویس از UI، بدون اختراع گردش‌کار فعال‌سازی قرارداد که در API فعلی وجود ندارد.

## تغییرات
- فرم فارسی RTL با انتخاب مشتری واقعی از API صفحه‌بندی‌شده، شماره قرارداد، تاریخ شروع، تاریخ پایان اختیاری و کد ارز سه‌حرفی.
- تاریخ‌های تقویمی UI به نیمه‌شب UTC تبدیل می‌شوند تا اختلاف timezone مرورگر تاریخ قراردادی را جابه‌جا نکند.
- ارز پیش‌فرض `IRR` است و کد ورودی به uppercase تبدیل می‌شود؛ API اعتبارسنجی نهایی سه حرف ASCII را انجام می‌دهد.
- قرارداد جدید با وضعیت Draft ساخته می‌شود؛ دکمهٔ ویرایش فقط برای Draft نمایش داده می‌شود.
- ویرایش با RowVersion و antiforgery تازه؛ پیام فارسی برای duplicate number، invalid dates/currency، stale RowVersion و خروج قرارداد از Draft.
- نمای Overview و مستندات Vendor به‌روزرسانی می‌شوند.

## تصمیم‌های محافظه‌کارانه
- endpoint فعلی برای تغییر وضعیت قرارداد وجود ندارد؛ UI وضعیت را تغییر نمی‌دهد. افزودن activation workflow نیازمند قواعد تجاری و API/test جداگانه است.
- ایجاد قرارداد در UI نیازمند `vendor.contracts.manage` و `vendor.customers.read` است تا مشتری واقعی انتخاب شود؛ مجوز نهایی سمت سرور است.
- این فرم فقط metadata قرارداد Vendor را تغییر می‌دهد و دیتابیس حسابداری مشتری را لمس نمی‌کند.

## تست و اعتبارسنجی
- Vitest: ایجاد با مشتری واقعی و CSRF، تاریخ‌های UTC، مجوز مشتری، RowVersion stale و کنترل ویرایش Draft-only.
- Vendor CI باید type-check، Vitest، runtime audit، SQL Server integration، EF snapshot و publish را روی head نهایی سبز کند.
- .NET CI باید Build/Test، secret scan، publish scripts، E2E و بسته‌بندی Linux/Windows را سبز کند.
- تا پایان CI روی head نهایی، وضعیت `Implemented / Not yet Verified` باقی می‌ماند.
