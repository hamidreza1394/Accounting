# تحویل وظیفه — ISSUE-235-provisioning-e2e-tests

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/235
- **Agent/مسئول:** Claude Code
- **تاریخ:** 2026-10-30
- **وضعیت بازبینی‌شده:** E2E خودکار دو میزبان منطقی در PR #329 ادغام و در Vendor CI اجرا شده است؛ اجرای دو ماشین فیزیکی/دو OS process و تست عملیاتی همچنان انجام نشده است.
- **مبنای فعلی:** `main` پس از merge commit `43d28253eb9f0f38ff25d0dfea143aeacdb06871` (PR #329). یادداشت‌های قبلی دربارهٔ ساخته‌نشدن E2E و انتظار CI روی head قدیمی‌اند و با این وضعیت جایگزین می‌شوند.

## هدف و معیارهای پذیرش
- [x] Rollback در شکست در میانهٔ ایجاد شرکت/دوره/نقش/مدیر/عضویت، و اثبات اینکه Retry بعدی گیر نمی‌کند.
- [x] درخواست‌های هم‌زمان تکراری: دقیقاً یک شرکت و یک توکن.
- [x] انقضا، مصرف یک‌باره و ابطال توکن قبلی؛ محدودسازی حدس؛ مصرف هم‌زمان.
- [x] مجوزهای دقیق زیرسیستم‌های خریداری‌شده و جداسازی بین شرکت‌ها.
- [x] Replay، استفادهٔ مجدد متعارض از RequestId/Nonce، بستهٔ دست‌کاری‌شده و نصب بیگانه.
- [x] E2E خودکار دو میزبان منطقی Vendor و Customer با دو TestServer و دو SQL Server database مستقل، انتقال فایل بسته و acknowledgement، Enrollment و Confirmed فقط پس از ACK معتبر (PR #329؛ Vendor CI run `41052515292`: 166/166 موفق، 0 شکست، 0 skipped).
- [ ] اجرای عملیاتی روی دو ماشین/دو OS process مستقل و شبکه واقعی: هنوز اجرا نشده؛ تست #329 در یک process و با دو WebApplicationFactory اجرا می‌شود.

## یافتهٔ مهم (باگ واقعی، رفع شد)
- تست هم‌زمانی نشان داد ۷ از ۸ درخواست هم‌زمان تکراری apply و ۳ از ۴ درخواست هم‌زمان enrollment با **Deadlock Victim** (SQL Server 1205) خطای مدیریت‌نشده می‌دادند (HTTP 500). رکورد تکراری ساخته نمی‌شد، ولی رفتار idempotent نبود.
- رفع: `sp_getapplock` تراکنشی در ابتدای apply/enrollment/reissue؛ درخواست‌های تکراری صف می‌شوند و مسیر replay موجود را می‌روند. Timeout پانزده‌ثانیه‌ای کد قابل‌تکرار `provisioning_apply_busy` / `enrollment_busy` (409) می‌دهد. `provisioning_apply_conflict` هم به 409 نگاشت شد (قبلاً 400 پیش‌فرض بود: تغییر رفتار API کوچک).

## فایل‌های مهم تغییرکرده
- `Server/Infrastructure/Provisioning/CustomerProvisioningApplyService.cs` — قفل و کدها.
- `Server/Api/ApiEndpoints.cs` — نگاشت کدها.
- `tests/Template.Tests/Integration/CustomerProvisioningTransactionalSqlServerTests.cs` — هفت تست جدید.
- `docs/SECURITY.md` — توضیح کنترل هم‌زمانی.

## تصمیم‌های فنی
- قفل برنامه‌ای سراسری (نه به‌ازای RequestId): provisioning و enrollment کم‌تکرار و اپراتوری‌اند؛ قفل سراسری جلوی رقابت‌های بین‌درخواستی (nonce، نام کاربری) را هم می‌گیرد و ساده‌تر است. SQL خام با دلیل مستند و پارامتری.
- ADR جدید ثبت نشد؛ تغییر پیاده‌سازی داخلی بدون اثر بر قرارداد است.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `dotnet build Template.sln -c Release` | موفق | 0 خطا |
| CustomerProvisioningTransactionalSqlServerTests (SQL Server 2022 واقعی) پیش از رفع | 2 ناموفق از 7 | Deadlock در apply و enrollment هم‌زمان |
| همان تست‌ها پس از رفع، سه بار پشت‌سرهم | 7 از 7 موفق در هر سه اجرا | |
| `dotnet test Template.sln -c Release` (با SQL Server) | موفق | 664 کل، 662 موفق، 2 skipped، 0 ناموفق |
| Vendor CI روی PR #329 | موفق | 166 تست، 0 شکست، 0 skipped؛ شامل تست dual-host با دو دیتابیس مستقل و انتقال package/ACK. .NET CI روی همان PR نیز طبق PR summary موفق بود. |
| بازبینی مستقل Subagent | اجرا نشد | Subagent فراخوانی نشد؛ بازبینی Diff توسط نویسنده |
| GitHub secret-scan، run `41021431573` روی SHA قبلی | ناموفق | Gitleaks یک مقدار ثابت password-like فقط در fixture تست را گزارش کرد؛ credential عملیاتی نبود، اما از کد حذف و با مقدار تصادفی runtime جایگزین شد. CI روی commit اصلاحی هنوز اجرا/تأیید نشده است |

## وضعیت E2E دو میزبان پس از PR #329
- PR #329 پروژهٔ تست Vendor را با `extern alias CustomerServer` به هر دو Host متصل می‌کند و دو `WebApplicationFactory` مستقل با دو SQL Server database جدا بالا می‌آورد.
- جریان کامل شامل ایجاد لایسنس و trust test-only، ساخت درخواست، تأیید دو نفره، امضای package، انتقال package از طریق فایل، رد package ناقص/کلید ناشناخته/دست‌کاری‌شده، apply و replay هم‌زمان، Enrollment یک‌بارمصرف، انتقال ACK امضاشده و Confirmed فقط پس از ACK معتبر است.
- Vendor CI run `41052515292` در 2026-10-30: مرحلهٔ `Test Vendor solution` موفق؛ 166 تست، 166 موفق، 0 شکست، 0 skipped. لاگ همان job ایجاد دو دیتابیس مستقل `FaranouVendorProvisioningE2E_*` و `FaranouTests_ProvisioningDualHost_*` را نشان می‌دهد.
- این E2E دو میزبان نرم‌افزاری را در یک process تست می‌کند؛ این معادل دو ماشین فیزیکی، دو OS process مستقل یا شبکهٔ واقعی نیست. تست‌های عملیاتی آن‌ها به Staging موکول‌اند.

## امنیت، داده و سازگاری
- بدون Schema/Migration جدید. قفل فقط رفتار هم‌زمانی را تغییر می‌دهد و هیچ مسیر دسترسی تازه‌ای نمی‌سازد.
- هیچ توکن/رمز در Audit وارد نمی‌شود (در تست انقضا بررسی شد).

## ریسک‌ها و کارهای باقی‌مانده
- اجرای دو ماشین/دو OS process مستقل، شبکه واقعی، سناریوهای قطع/وصل انتقال فایل و عملیات recovery در Staging.
- قفل سراسری یعنی applyهای همزمان صف می‌شوند؛ در حجم بالا (نامحتمل برای این کار اپراتوری) باید بازنگری شود.

## امنیت و اصلاح Secret Scan
- مقدار ثابت password-like فقط در تست integration وجود داشت؛ هیچ رمز Production یا customer credential در اختیار این تست قرار نگرفته بود.
- برای حذف الگوی قابل‌تشخیص و جلوگیری از هرگونه credential ثابت، رمزهای تست در هر اجرای تست با `RandomNumberGenerator.GetBytes(32)` تولید می‌شوند و در log یا audit ثبت نمی‌شوند.
- نتیجهٔ Gitleaks باید فقط بر اساس اجرای CI روی SHA نهایی هر تغییر جدید گزارش شود؛ نتیجهٔ قدیمی run `41021431573` برای SHA قبل از اصلاح، نتیجهٔ فعلی محسوب نمی‌شود.

## شناسه‌های Git
- Branch: `feat/issue-235-provisioning-e2e-tests`
- PR E2E ادغام‌شده: [#329](https://github.com/hamidreza1394/Accounting/pull/329) — merge commit `43d28253eb9f0f38ff25d0dfea143aeacdb06871`.
- CI evidence: [Vendor CI #41052515292](https://github.com/hamidreza1394/Accounting/actions/runs/41052515292).
