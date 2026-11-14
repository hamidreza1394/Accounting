# Handoff — PR-E1a: اعتبارسنجی نقش مدیر سیستم در هر درخواست (R-16)

- **Issue:** UNNUMBERED (ریسک R-16؛ شماره Issue جعل نشد)
- **Agent:** Claude Code
- **مبنا:** `origin/main@8ef2196197682c9ba8fa3bb2bd4f35e07855419b` (شامل #662)
- **وضعیت:** اصلاح و تست‌ها Implemented در شاخهٔ PR. هیچ تغییر Schema/Migration/Backfill/دیتابیس، Tenant/Company، نقش‌ها/PermissionTree، Trade یا صفحات مدیریت کاربران انجام نشد.

## علت ریشه‌ای
`Login.cshtml.cs` نام نقش را در Claim Cookie می‌نویسد. ۳۰ صفحهٔ `Server/Pages/Admin/**` با `[Authorize(Roles = Constants.Role.Admin)]` و `GET /api/v1/session` (`user.IsInRole`) فقط همین Claim را می‌خوانند. `SessionValidator.ValidatePrincipalAsync` فعال‌بودن، Password Stamp و Session را هر درخواست بررسی می‌کرد ولی نقش را نه؛ پس نقش لغو‌شده تا انقضای Cookie کار می‌کرد. Endpointهای API (`UserAdministrationEndpoints.IsInstallationAdministratorAsync` و `MePermissionsEndpoints`) نقش را از دیتابیس می‌خوانند؛ یعنی دو منطق متناقض.

## نقطهٔ اصلاح و روش
بهترین نقطه: همان رویداد اعتبارسنجی Cookie (`OnValidatePrincipal` ← `SessionValidator`)، چون برای همهٔ درخواست‌ها (Razor و API) اجرا می‌شود و Query کاربر را از قبل دارد؛ Policy/Middleware/سرویس/Interface جدید لازم نشد.
- Query موجود حساب کاربر `RoleName` نقش فعال (`Role.IsActive`) را هم برمی‌گرداند (بدون Query اضافه).
- `IsSessionValidAsync` (امضای عمومی حفظ شد) به `CheckSessionAsync` سپرده شد که نقش فعلی را هم برمی‌گرداند.
- برای کاربر شرکتی، اگر Claimهای نقش Cookie با نقش فعلی یکی نباشد، `ReplacePrincipal` با همان هویت/Claimها و نقش فعلی (یا بدون نقش) و `ShouldRenew = true` انجام می‌شود. Session لغو نمی‌شود. حساب زیرساخت (Master) دست‌نخورده است.
- Cache اضافه نشد، پس لغو نقش در درخواست بعدی اثر می‌کند.
- خطای دیتابیس: Exception منتشر می‌شود و درخواست پیش نمی‌رود (رفتار قبلی)؛ هیچ نقشی اعطا نمی‌شود.

## تست
- `tests/Template.Tests/Integration/AdminRoleRevalidationHttpTests.cs` (۸ تست) با ورود/Cookie/صفحهٔ واقعی `/Admin/Roles/Index` و `/api/v1/session` و Driver مستقیم `ValidatePrincipalAsync` برای Claim جعلی/قدیمی و خطای دیتابیس.
- RED پیش از اصلاح: ۴ شکست (لغو نقش، تعویض/غیرفعال‌سازی نقش، Claim قدیمی کاربر غیرمدیر، Principal کاربر لغو‌شده) و ۴ موفق (خط پایه). GREEN: ۸/۸.
- نتیجهٔ کامل در گزارش PR.

## اثر بر عملکرد و Sessionها
بدون Query اضافه (یک ستون در Query موجود). فقط وقتی Claim نقش با دیتابیس اختلاف دارد Cookie دوباره صادر می‌شود (`ShouldRenew`). Cookieهای ورود قبلی با نقش درست دست‌نخورده می‌مانند.

## Backlog (غیرمسدودکننده)
- `User.IsInRole` در `_LoginStatus.cshtml` و `ApiEndpoints.GetSession` اکنون با دیتابیس هم‌تراز است؛ یکپارچه‌سازی کامل Razor و API روی یک Policy مرکزی (Authorization Handler) در صورت نیاز PR جداگانه.
- سایر موارد R-13..R-18 و حذف Claim نقش از Cookie در ورود دست‌نخورده ماند.
