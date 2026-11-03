# تحویل — Issue #444: E2E واقعی منع دسترسی مدیریت نقش

## هدف
افزودن regression browser test برای اثبات اینکه کاربر عادی نمی‌تواند مدیریت نقش و کاتالوگ مجوز را از طریق UI یا با فراخوانی مستقیم API دور بزند.

## سناریو
1. با کاربر عادی فعال Alice وارد برنامه منتشرشده می‌شود.
2. شرکت و دورهٔ مجاز خود را انتخاب می‌کند.
3. مسیر `/app/admin/roles` را مستقیم باز می‌کند؛ UI باید پیام عدم دسترسی نشان دهد و هیچ درخواست `/api/v1/admin/*` ارسال نکند.
4. UI را دور می‌زند و با Cookie احراز هویت‌شده به `GET /api/v1/admin/permission-catalog` و `GET /api/v1/admin/companies/{companyId}/roles` درخواست می‌فرستد؛ هر دو باید 403 برگردانند.
5. پس از پاک‌کردن Cookie، کاتالوگ باید 401 برگرداند.

## اعتبارسنجی
- تست به harness موجود `tests/e2e` متکی است؛ سرور منتشرشده در Production و SQL Server 2022 واقعی را در CI بالا می‌آورد.
- داده‌ها از seed موجود و دیتابیس disposable با نام `FaranouE2E` می‌آیند؛ تست هیچ داده‌ای را پاک یا mutate نمی‌کند.
- نتیجه CI این PR باید قبل از اعلام Verified بررسی شود؛ تا آن زمان Pending است.
- اجرای محلی در محیط این نشست انجام نشده است.

## دامنه و امنیت
- فقط تست E2E و مستندات تغییر کرده‌اند.
- هیچ production code، API، permission catalog، Schema، Migration یا package تغییر نکرده است.
- این تست مسیر مثبت Installation Administrator یا Delegation Ceiling را پوشش نمی‌دهد؛ این موارد در handoff تجمیعی #444 باقی مانده‌اند.

## فایل‌ها
- `tests/e2e/specs/faranou.spec.mjs`
- `docs/WORK_LOG.md`
- `docs/handoffs/ISSUE-444-role-api-authorization-e2e.md`
