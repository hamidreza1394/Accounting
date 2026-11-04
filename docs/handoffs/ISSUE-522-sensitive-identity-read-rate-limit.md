# تحویل وظیفه — ISSUE-522-sensitive-identity-read-rate-limit

- **Issue:** [#522](https://github.com/hamidreza1394/Accounting/issues/522)
- **Agent/مسئول:** ChatGPT
- **تاریخ:** 2026-11-04
- **وضعیت:** پیاده‌سازی‌شده؛ منتظر اعتبارسنجی CI

## هدف و معیارهای پذیرش
- [x] اعمال `ApiSensitivePolicy` علاوه بر `ApiPolicy` روی چهار endpoint خواندن اطلاعات هویتی.
- [x] حفظ authorization، no-store، فیلترها، pagination و DTOهای موجود.
- [x] افزودن تست metadata واقعی route برای هر چهار endpoint و هر دو policy.
- [ ] Build و تست کامل روی CI سبز روی SHA نهایی.

## خلاصه تغییرات
- روی فهرست کاربران مدیر نصب، جزئیات کاربر، directory کاربران شرکت و اعضای نقش، rate limit حساس اضافه شد.
- تست معماری از `EndpointDataSource` استفاده می‌کند و هر چهار route و policyهای عمومی/حساس را بررسی می‌کند.

## فایل‌های مهم تغییرکرده
- `Server/Api/UserAdministrationEndpoints.cs` — دو GET مدیر نصب.
- `Server/Api/CompanyUserDirectoryEndpoints.cs` — GET directory شرکت.
- `Server/Api/CompanyRoleAdministrationEndpoints.cs` — GET اعضای نقش.
- `tests/Template.Tests/Architecture/ApiSensitiveReadRateLimitTests.cs` — تست metadata route.
- `docs/SECURITY.md`, `docs/WORK_LOG.md`, `docs/INDEX.md` — ثبت کنترل امنیتی و handoff.

## تصمیم‌های فنی
- policy حساس روی همان endpoint اضافه شد؛ policy عمومی گروه `/api/v1` حفظ می‌شود. هر دو limiter مستقل‌اند و هر دو محدودیت را اعمال می‌کنند.
- هیچ منطق authorization یا قرارداد پاسخ تغییر نکرد.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| بررسی metadata routeها در کد | انجام شد | چهار route در mapping موجود تأیید شدند |
| تست `ApiSensitiveReadRateLimitTests` | اجرا نشده | نیازمند .NET SDK و اجرای CI |
| `dotnet build Template.sln --no-restore --configuration Release` | اجرا نشده | در این نشست محیط اجرای .NET محلی در دسترس نیست |
| `dotnet test Template.sln --no-build --configuration Release` | اجرا نشده | نتیجه CI باید بررسی شود |
| `git diff --check` | اجرا نشده | تغییر از طریق Git Data API ثبت می‌شود؛ بررسی CI و diff لازم است |

## امنیت، داده و سازگاری
- فقط نرخ خواندن PII/identity سخت‌گیرانه‌تر شد؛ پاسخ‌های 429 از middleware موجود با `Retry-After` و `no-store` تولید می‌شوند.
- schema، Migration، داده، authorization و DTO تغییر نکردند.

## ریسک‌ها و کارهای باقی‌مانده
- CI باید اثبات کند metadata policyها در pipeline واقعی حضور دارند و تست جدید کامپایل و اجرا می‌شود.
- محدودیت rate limiter همچنان per-instance است؛ توزیع‌شده‌کردن آن خارج از دامنه این Issue است.

## شناسه‌های Git
- Branch: `security/issue-522-sensitive-read-rate-limit`
- Commit: پس از ثبت
- Pull Request: پس از ایجاد
