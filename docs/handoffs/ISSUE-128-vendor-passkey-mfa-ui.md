# تحویل وظیفه — ISSUE-128-vendor-passkey-mfa-ui

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/128
- **PR مبنا:** https://github.com/hamidreza1394/Accounting/pull/127 (Merged)
- **Branch:** `feat/issue-128-vendor-passkey-mfa-ui`
- **تاریخ:** 2026-10-26
- **وضعیت:** پیاده‌سازی در شاخه؛ Vendor CI پس از بازشدن PR باید اجرا و بررسی شود. Production-ready نیست.

## دامنه
تکمیل UI برای endpointهای موجود Passkey/WebAuthn و TOTP، اصلاح دو شکست CI روی main، و ثبت وضعیت مستندات. این مرحله CRUD تجاری یا تغییر Schema انجام نمی‌دهد.

## تغییرات شاخه
- `Vendor/Vendor.Web/src/App.tsx`: ورود Passkey با `/api/auth/passkey/options` و `/api/auth/passkey/sign-in`؛ ثبت Passkey با `/api/auth/passkey/creation-options` و `/api/auth/passkey/register`؛ راه‌اندازی/تأیید TOTP با `/api/auth/totp/setup` و `/api/auth/totp/confirm`.
- تبدیل challenge/credential IDs از Base64URL به ArrayBuffer و serialization پاسخ‌های WebAuthn بدون وابستگی runtime خارجی.
- shared key و authenticator URI مربوط به TOTP فقط در state حافظه‌ای React؛ پاک‌سازی پس از تأیید/لغو/خروج؛ درخواست‌ها با `cache: no-store`.
- پیام‌های خطای فارسی برای وضعیت‌های 400/401/403/409/429 و پیام WebAuthn برای مرورگر/اتصال ناسازگار.
- `Vendor/Vendor.Web/src/styles/index.css`: استایل فرم‌های passkey و TOTP، حالت‌های فوکوس و نمایش راز.
- `Vendor/Vendor.Web/src/App.test.tsx`: تست fallback مرورگر و ثبت TOTP، با assert عدم نوشتن در Web Storage.
- `tests/Vendor.Host.Tests/LivenessEndpointTests.cs`: تست deep link کلاینت به‌جای انتظار 404 برای مسیر UI؛ تست مستقل unknown API همچنان 404 را بررسی می‌کند.
- `.github/workflows/vendor-ci.yml`: دیتابیس اتصال Host در تست به `FaranouVendorAuthTests` تنظیم شد تا با تست integration هم‌خوان باشد؛ SQL Server آزمایشی audit همچنان از متغیر مستقل می‌آید.
- `Vendor/Vendor.Host/Program.cs`: ثبت تکراری `AddDbContext<VendorDbContext>` حذف شد تا تنظیم Migration Assembly در persistence module حفظ شود؛ این اصلاح پس از مشاهدهٔ `PendingModelChangesWarning` در تست runtime انجام شد و تا CI مجدد، تأییدنشده است.
- `Vendor/README.md`, `docs/PROJECT_STATUS.md`, `docs/WORK_LOG.md` و handoff قبلی به‌روزرسانی شدند.

## شواهد و وضعیت تست
- Vendor CI در main: run [40279335977](https://github.com/hamidreza1394/Accounting/actions/runs/40279335977) شکست خورد.
- شکست‌های قطعی: تست `UnknownPathReturnsNotFound` با SPA fallback تناقض داشت؛ assertion تست authentication انتظار `FaranouVendorAuthTests` داشت، در حالی که workflow `FaranouVendorTests` تنظیم می‌کرد.
- اصلاح SPA fallback و نام دیتابیس انجام شد. اجرای run 40280056742 نشان داد TypeScript، Vitest، audit، Vite build، بودجهٔ bundle، .NET build و EF snapshot check سبز هستند؛ اما `dotnet test` هنوز به‌علت `PendingModelChangesWarning` در runtime migration شکست خورد. ثبت تکراری DbContext در Host حذف شده است و **CI پس از این اصلاح هنوز سبز تأیید نشده**.
- Build/Test محلی اجرا نشده است.
- هیچ تغییر در ClientApp، Customer DB یا Customer migrations انجام نشده است.

## محدودیت‌های باز
- WebAuthn واقعی باید در HTTPS با authenticator واقعی بررسی شود؛ تست UI شبیه‌سازی‌شده جایگزین آزمون مرورگری نیست.
- UI فعلی نام نمایشی Passkey را ثابت می‌گذارد؛ مدیریت نام/حذف credential نیازمند endpointهای صریح و آزمون‌شده است.
- bootstrap امن، تغییر رمز اجباری، RBAC ریزدانه، اتصال writer/verifier ممیزی به عملیات، checkpoint مستقل، maker-checker، مدل/API تجاری و KMS/HSM هنوز موانع Production هستند.
- هیچ تستی حذف یا ضعیف نشده است.
