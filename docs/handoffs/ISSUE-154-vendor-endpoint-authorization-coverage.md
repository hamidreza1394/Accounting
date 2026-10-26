# تحویل وظیفه — ISSUE-154-vendor-endpoint-authorization-coverage

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/154
- **تاریخ:** 2026-10-26
- **وضعیت:** آزمون metadata افزوده شده؛ اجرا و CI هنوز تأیید نشده‌اند.

## هدف
اطمینان از اینکه تمام endpointهای /api/ به‌صورت صریح AllowAnonymous یا RequireAuthorization دارند و هیچ مسیر عمومی جدیدی بدون تصمیم ظاهر نمی‌شود.

## تغییرات
- tests/Vendor.Host.Tests/VendorPermissionAuthorizationTests.cs: آزمون integration با WebApplicationFactory و EndpointDataSource اضافه شد.
- آزمون مسیرهای عمومی را به شش مسیر مجاز احراز هویت محدود می‌کند: antiforgery، password، TOTP verify، passkey options، passkey sign-in و logout.
- آزمون دوم سیاست مؤثر تمام endpointهای محافظت‌شده را با AuthorizationPolicy.CombineAsync می‌سازد و وجود VendorAdminAccessRequirement (گارد مدیر فعال/MFA) را تأیید می‌کند.
- تست موجود DefaultAndFallbackPoliciesBothRequireAuthenticatedMfaVendorAdministrator حفظ شده است.

## اعتبارسنجی
- Build/Test اجرا نشده؛ CI PR باید نتیجه بدهد.
- آزمون resource-level authorization، cross-customer IDOR و maker-checker هنوز در محدوده کامل این Issue/مراحل بعدی باقی است.
- هیچ دیتابیس یا Migration حسابداری مشتری تغییر نکرده است.

## Git
- Branch: test/issue-154-vendor-endpoint-authorization-coverage
- Commit: 91441ee9758f931a4624df968c297e21807ee04d
