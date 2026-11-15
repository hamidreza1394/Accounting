# Handoff — اجرای D-088: ردیابی مدیر نصب

## هدف
اجرای الزامات ۱ تا ۳ و ۶ D-088 و راستی‌آزمایی الزام ۴ بدون تغییر Schema/Tenant.

## تغییرات
- `Server/Infrastructure/Security/InstallationAdminAudit.cs` (جدید): ثبت دسترسی/تغییر، Diff، گزارش.
- `UserAdministrationEndpoints`، `AdminUserSessionEndpoints`، `SecurityAuditAdministrationEndpoints`، `CompanyMembershipRequestEndpoints`، صفحات Razor کاربران: ثبت مشاهده و تغییر.
- `DatabaseContext`: رد تغییر/حذف `SecurityAuditEvent`.
- مسیر جدید `GET /api/v1/admin/security/installation-admin-activity`.
- امضای Handlerها تغییر نکرد؛ `Reason` فقط پارامتر انتهایی اختیاری Request است.

## تست
`InstallationAdminAuditTests` (۱۵) و `InstallationAdminAuditHttpTests` (۴)؛ آزمون‌های منفی: کاربر غیرمدیر ۴۰۳ و بدون رویداد؛ Update غیرمدیر بی‌اثر؛ بدون رمز در جزئیات؛ تغییر/حذف ممیزی رد می‌شود؛ اسکن منبع با نمونهٔ منفی؛ مدیر نصب بدون عضویت به دایرکتوری شرکت و Trade دسترسی ندارد.
اثبات جهش: حذف محافظ Append-only و حذف ثبت مشاهدهٔ صف عضویت، هرکدام دقیقاً یک آزمون را شکست داد؛ سپس بازگردانده شد. صادقانه: کد پیش از آزمون نوشته شد (نه TDD کامل).

## محدودیت‌ها
محافظت سطح DB نیازمند Schema است؛ دلیل در Razor ندارد و در API اختیاری است؛ استثنای D-071 باید با بند ۴ تطبیق داده شود؛ آزمون خرابی نوشتن ممیزی انجام نشد.
