# تحویل — ISSUE-80-licensing-contracts-d045

- **Issue:** [#80](https://github.com/hamidreza1394/Accounting/issues/80)
- **Agent/مسئول:** ChatGPT
- **تاریخ:** 2026-10-25
- **وضعیت:** تغییرات ثبت شده؛ PR پیش‌نویس و CI در انتظار

## هدف و معیارهای پذیرش
- [x] استفاده از پروژهٔ موجود `Licensing.Contracts`؛ جلوگیری از ساخت پروژهٔ تکراری.
- [x] همگام‌سازی شناسه/کامنت الگوریتم با D-045.
- [x] رد کد زیرسیستم ناشناخته و تکراری در Payload v1.
- [x] آزمون بردار ثابت ECDSA P-256/SHA-256 (RFC 6979) با امضای IEEE P1363 دقیقاً 64 بایت.
- [x] آزمون اینکه تغییر whitespace در Payload باعث رد امضا می‌شود.
- [x] آزمون رشتهٔ کامل JSON canonical و اجرای آزمون قرارداد در Windows CI.
- [ ] CI واقعی سبز روی Linux و Windows؛ فعلاً اجرا نشده/در انتظار.

## خلاصه تغییرات
- کامنت‌های `LicenseAlgorithms`, `LicenseLimits` و `EcdsaP256Sha256Verifier` با تصمیم پذیرفته‌شده هماهنگ شدند.
- `LicenseSubsystemCodes` به‌عنوان allowlist نسخهٔ اول اضافه شد: `finance`, `sales`, `inventory`, `treasury`.
- `StructuralValidation` زیرسیستم ناشناخته و تکراری را fail-closed رد می‌کند.
- اعتبارسنج امضا علاوه بر اندازهٔ 256 بیت، OID منحنی NIST P-256 را نیز بررسی می‌کند؛ صرف اندازهٔ کلید برای تشخیص منحنی کافی نیست.
- تست‌های ثابت/منفی اضافه و Job ویندوز برای اجرای تست‌های قرارداد به Workflow اضافه شد.

## فایل‌های مهم تغییرکرده
- `Licensing.Contracts/LicenseSubsystemCodes.cs` — کدهای پایدار نسخهٔ اول.
- `Licensing.Contracts/StructuralValidation.cs` — اعتبارسنجی کدها و تکرارها.
- `Licensing.Contracts/EcdsaP256Sha256Verifier.cs` — الگوریتم و OID منحنی.
- `Licensing.Contracts/LicenseAlgorithms.cs`, `LicenseLimits.cs` — کامنت‌های تصمیم.
- `tests/Licensing.Contracts.Tests/EnvelopeValidatorTests.cs` — بردار RFC 6979، دستکاری whitespace، JSON کامل و کدهای زیرسیستم.
- `.github/workflows/dotnet.yml` — اجرای تست‌های قرارداد روی Windows.
- `docs/TESTING.md`, `docs/WORK_LOG.md` — راهبرد تست و سابقه.

## تصمیم‌های فنی
- الگوریتم فقط ECDSA P-256/SHA-256 و IEEE P1363 ثابت 64 بایتی است.
- اعتبارسنجی روی بایت‌های UTF-8 متن دقیق Payload انجام می‌شود؛ JSON دوباره‌سریال‌سازی نمی‌شود.
- لیست زیرسیستم‌های نسخهٔ اول allowlist صریح دارد.
- هیچ Schema/Migration، اتصال به Server/API، کلید خصوصی صدور یا Enforcement اضافه نشده است.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| بازبینی فایل‌های موجود و تست‌ها | انجام شد | GitHub API |
| بردار ثابت RFC 6979 با Python cryptography | Verify موفق | فقط بررسی کمکی؛ جایگزین تست .NET/CI نیست |
| `dotnet test tests/Licensing.Contracts.Tests/Licensing.Contracts.Tests.csproj --configuration Release` | اجرا نشده | محیط این گردش‌کار ابزار اجرای dotnet ندارد |
| تست .NET روی Linux در CI | در انتظار | باید از PR بررسی شود |
| تست .NET روی Windows در CI | در انتظار | Workflow تغییر یافته؛ باید از PR بررسی شود |
| `git diff --check` | اجرا نشده | محیط Git محلی در این گردش‌کار در دسترس نیست |

## امنیت، داده و سازگاری
- کلید خصوصی صدور در مخزن یا بستهٔ مشتری قرار نگرفته است.
- الگوریتم/منحنی ثابت است؛ الگوریتم ورودی برای انتخاب verifier استفاده نمی‌شود.
- سازگاری چندسکویی تا پایان CI `NotVerified` می‌ماند.

## ریسک‌ها و کارهای باقی‌مانده
- CI ممکن است تفاوت پشتیبانی crypto روی Windows را آشکار کند؛ باید نتیجهٔ واقعی بررسی شود.
- نیاز به بازبینی مستقل diff و تأیید CI پیش از آماده‌سازی برای ادغام.
- هر تصمیم دربارهٔ سیاست انقضا، مهلت ارفاق، سقف نامحدود شرکت یا سامانهٔ فروشنده خارج از این PR است.

## شناسه‌های Git
- Branch: `feature/licensing-contracts-d045`
- Commit: در سرشاخهٔ PR ثبت می‌شود.
- Pull Request: پس از ایجاد درج می‌شود.
