# تحویل وظیفه — ISSUE-UNNUMBERED-localhost-windows-guide

- **Issue:** UNNUMBERED (پوشش سناریوی A مأموریت مالک 2026-11-19: «Windows Localhost — سیستم شخصی») — مرجع‌ها: D-107، D-060، D-098، #396، #236
- **Agent/مسئول:** GLM
- **تاریخ:** 2026-11-19
- **وضعیت:** سند بازبینی و اصلاح شده است؛ تست واقعی روی ماتریس Windows هنوز انجام نشده است. D-109 تک‌اپراتوره بودن V1 را تصویب کرده، اما کد فعلی هنوز self-approval را رد می‌کند؛ صدور نهایی تا اصلاح Runtime در PR مستقل Blocked است.

## هدف و معیارهای پذیرش
- [x] مسیر اجرای محلی Vendor و Customer و بررسی وضعیت Runtime مستند شده است.
- [x] ماتریس Windows/.NET 10/SQL Server و محدودیت Windows 10 Home/Pro و Windows ARM64 ثبت شده است.
- [ ] صدور License از نصب تازه: پس از پیاده‌سازی D-109 در Runtime؛ کد فعلی هنوز Requester != Reviewer را enforce می‌کند.
- [ ] Import کامل از API: نیازمند قرارداد نشست و CSRF واقعی/تست‌شده؛ فرمان PowerShell ناقص قبلی حذف شده است.
- [x] فرمان‌های Setup و Migration و Build اصلاح شده‌اند؛ مرحله‌های نیازمند نشست معتبر/CSRF و شکاف فعلی Runtime نسبت به D-109 صریحاً معرفی شده‌اند.
- [x] localhost صرفاً آدرس است؛ هیچ فرضی از مالکیت/Tenant از آن ساخته نمی‌شود.
- [x] روش ساخت کلید SoftwareProtected برای License/Provisioning (D-107) بدون TPM/HSM/KMS مستند شده است.
- [x] سیاست تک‌اپراتوره Vendor v1 و self-approval طبق D-109 ثبت شده؛ نیاز به مدیر دوم حذف شده است.
- [x] محدودیت‌های واقعی صادقانه ثبت شده‌اند (Wizard React، reactivation D-099، Gate v5، D-100/101/102).

## خلاصه تغییرات
- راهنمای موجود بازبینی شد: ماتریس OS سازگار با .NET 10 و SQL Server 2022، تفکیک Build/Runtime، CNG Software KSP بدون TPM/HSM/KMS برای License/Provisioning، ترتیب Build → Migration → Bootstrap → Startup، راز Bootstrap، PowerShell 5.1، SQL extraction بدون BOM و شکاف پیاده‌سازی self-approval نسبت به D-109 و محدودیت Import.

## فایل‌های مهم تغییرکرده
- `docs/LOCALHOST_WINDOWS_TEST_GUIDE.md` — سند جدید
- `docs/PROJECT_STATUS.md`، `docs/WORK_LOG.md`، این handoff — هم‌ترازی

## تصمیم‌های فنی
- مسیر فعال‌سازی فعلی کد (import پایدار + provisioning v4) مبنا قرار گرفت؛ هیچ قابلیت حدسی به مستندات افزوده نشد.
- D-109 با تأیید مالک Accepted شد: نسخهٔ اول Vendor تک‌اپراتوره است و همان اپراتور می‌تواند درخواست را ثبت و تأیید کند؛ Maker-Checker دو‌نفره فقط در آینده و با تصمیم مستقل مالک فعال می‌شود. D-109 الزام‌های دو مدیر/عدم self-approval در D-046/D-047/D-107 و بند متناظر D-108 را برای Vendor V1 supersede می‌کند.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| Build/Test در PRهای #720/#721/#722 | ثبت‌شده در همان PRها | این PR فقط مستندات است |
| Build/Test پس از اصلاح مستندات | اجرا نشده | تغییر کد وجود ندارد؛ CI مستندات باید بررسی شود |
| اجرای راهنما روی Windowsهای هدف | اجرا نشده | مالک باید روی هر OS هدف تست کند |
| صدور از نصب تازه طبق D-109 | Blocked در کد فعلی | شرط قدیمی Requester != Reviewer هنوز enforce می‌شود |
| Import License از CLI | Not verified | نیازمند نشست معتبر و قرارداد CSRF تست‌شده است |

## امنیت، داده و سازگاری
- هیچ secret/کلیدی در مخزن نیست؛ متغیرهای محیطی موقت با مقادیر placeholder.
- ریسک D-107 (مدیر محلی) در سند تکرار شده؛ ACL کلید باید محدود به هویت اجراکننده باشد.
- Windows 7/8.1 و Windows 10 Home/Pro در ماتریس پشتیبانی .NET 10 نیستند؛ ادعای پشتیبانی عمومی برای همه نسخه‌های Windows نشده است.
- License/Provisioning central signing بدون TPM/HSM/KMS است؛ D-055/D-057 مسیرهای مستقل باقی مانده‌اند.

## ریسک‌ها و کارهای باقی‌مانده
- اصلاح کد Vendor برای self-approval مطابق D-109 در PR مستقل و افزودن تست‌های Authorization/Audit/concurrency؛ تا آن زمان صدور از نصب تازه Blocked است.
- پس از اجرای واقعی مالک، بازخورد به این سند اضافه شود؛ Wizard React و D-099 کارهای بعدی‌اند.

## شناسه‌های Git
- Branch: `agent/unnumbered-localhost-windows-guide`
- Commit: در تاریخچه Branch `agent/unnumbered-localhost-windows-guide` (PR #723)
- Pull Request: #723 (باز؛ Merge نشده)
