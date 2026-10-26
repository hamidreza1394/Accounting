# تحویل وظیفه — ISSUE-114-default-mfa-policy

- **Issue:** [#114](https://github.com/hamidreza1394/Accounting/issues/114)
- **Branch:** `fix/issue-114-default-mfa-policy`
- **وابستگی:** PR #148 (bootstrap)؛ این شاخه روی آن بنا شده است.
- **تاریخ:** 2026-10-26

## خطای امنیتی
در ASP.NET Core، `FallbackPolicy` برای endpointهای بدون authorization metadata اعمال می‌شود، اما `.RequireAuthorization()` از `DefaultPolicy` استفاده می‌کند. برنامه فقط fallback را با `VendorAdminAccessRequirement` تنظیم کرده بود؛ بنابراین baseline MFA در مسیرهای دارای `.RequireAuthorization()` به‌صورت مرکزی تضمین نمی‌شد.

## اصلاح
- فایل `Vendor/Vendor.Host/Authorization/VendorAuthorizationPolicies.cs` baseline مشترک برای DefaultPolicy و FallbackPolicy می‌سازد.
- `Program.cs` اکنون هر دو policy را از همین تنظیم‌کننده می‌گیرد.
- `tests/Vendor.Host.Tests/VendorAuthorizationPolicyTests.cs` هر دو policy را بررسی می‌کند.

## محدوده و باقی‌مانده
- این تغییر MFA/فعال‌بودن مدیر را برای default authorization تثبیت می‌کند؛ هنوز permission catalog، default-deny بر اساس مجوز، resource/tenant authorization و maker-checker پیاده نشده‌اند.
- هیچ Migration یا دیتابیس مشتری تغییر نکرده است.
- CI این شاخه باید Build/Test و E2E را سبز کند.