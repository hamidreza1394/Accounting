# تحویل وظیفه — ISSUE-515-csv-leading-control-characters

- **Issue:** #515 (پیگیری امنیتی #507)
- **Agent/مسئول:** ChatGPT — Backend
- **تاریخ:** 2026-11-03
- **وضعیت:** Implemented؛ CI و اجرای تست‌ها هنوز تأیید نشده‌اند.

## هدف و معیارهای پذیرش
- [x] سلول CSV که با Tab، CR یا LF آغاز می‌شود با apostrophe خنثی شود.
- [x] خنثی‌سازی پیشوندهای فرمولی پس از whitespace حفظ شود.
- [x] تست regression برای Tab و CR مستقل اضافه شود.
- [x] قرارداد API، Schema، Migration و UI تغییر نکند.

## خلاصه تغییرات
- CsvCell اکنون کنترل‌کاراکتر ابتدایی Tab/CR/LF را قبل از RFC 4180 quoting خنثی می‌کند.
- تست خروجی CSV دو رویداد با EntityType آغازشونده با Tab و EntityId آغازشونده با CR را بررسی می‌کند و شمارش Audit را مطابق دو ردیف تطبیق می‌دهد.

## فایل‌های مهم تغییرکرده
- `Server/Api/SecurityAuditAdministrationEndpoints.cs` — خنثی‌سازی کنترل‌کاراکتر ابتدایی.
- `tests/Template.Tests/SecurityAuditAdministrationEndpointTests.cs` — regression test برای Tab و CR.
- `docs/WORK_LOG.md`، `docs/INDEX.md` و این handoff — مستندسازی تحویل.

## تصمیم‌های فنی
- بدون تغییر API، داده، مجوز، Migration یا سیاست زمان؛ فقط تکمیل Formula Injection guard مطابق معیار #507.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| تست واحد CSV | اجرا نشده | در این محیط ابزار اجرای محلی مخزن در دسترس نیست؛ CI باید اجرا شود. |
| تست SQL Server | اجرا نشده | فقط GitHub Actions می‌تواند این آزمون را روی SQL Server پیکربندی‌شده اجرا کند. |
| Build و Test کامل .NET | اجرا نشده | منتظر CI روی SHA نهایی PR. |
| `git diff --check` | اجرا نشده | تغییر از GitHub Contents API اعمال شده؛ working tree محلی در دسترس نیست. |

## امنیت، داده و سازگاری
- فقط مقدار متنی خروجی CSV تغییر می‌کند؛ هیچ دادهٔ ذخیره‌شده یا قرارداد API تغییر نمی‌کند.
- هیچ IP یا DetailsJson به CSV اضافه نشده است.

## ریسک‌ها و کارهای باقی‌مانده
- نتیجهٔ واقعی CI و تست SQL Server باید بررسی شود؛ تا آن زمان Verified نیست.
- رفتار Excel در نسخه‌های مختلف با CI قابل اثبات نیست و تست دستی Excel اجرا نشده است.

## شناسه‌های Git
- Branch: `fix/515-csv-leading-control-characters`
- Commit نهایی فعلی: `71eb50a1eefd0c585eeef74147fbc47ae4254362` (سرشاخه PR؛ commitهای قبلی همان شاخه در تاریخچه حفظ شده‌اند).
- Pull Request: https://github.com/hamidreza1394/Accounting/pull/516 (باز؛ Merge نشده).
