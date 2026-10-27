# تحویل Issue #167 — اصلاح لبه‌های صفحه‌بندی Vendor

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/167
- **مسئول:** ChatGPT / Faranou senior developer workflow
- **تاریخ:** 2026-10-27
- **وضعیت:** پیاده‌سازی انجام شده؛ منتظر CI و بازبینی نهایی

## هدف و معیارهای پذیرش
- [x] ترتیب قطعی فهرست‌ها حتی وقتی کد/شمارهٔ اصلی تکراری باشد.
- [x] جلوگیری از offset تکراری در شماره صفحه‌های بسیار بزرگ.
- [x] پوشش مرزهای pageSize=1 و pageSize=100 در تست واحد.
- [ ] Build، تست‌ها و Vendor CI روی HEAD نهایی سبز شوند.

## تغییرات
- Vendor/Vendor.Host/Authorization/VendorBusinessEndpoints.cs: افزودن ThenBy(Id) به چهار query؛ محدودکردن Page به محدودهٔ قابل‌نمایش offset؛ Skip checked.
- tests/Vendor.Host.Tests/VendorPageRequestTests.cs: تست offset بزرگ و جلوگیری از overflow هنگام pageSize=1.
- docs/WORK_LOG.md: ثبت تصمیم و وضعیت اعتبارسنجی.

## تصمیم فنی
Skip در EF Core از Int32 استفاده می‌کند؛ بنابراین صفحهٔ ورودی باید با توجه به PageSize محدود شود. clamp کردن صرفِ offset به int.MaxValue باعث می‌شد صفحات بسیار بزرگ مختلف به یک offset یکسان برسند. ترتیب بر اساس Id نیز tie-breaker پایدار فراهم می‌کند.

## اعتبارسنجی
| مورد | نتیجه |
|---|---|
| Build Vendor | اجرا نشده در محیط گفتگو |
| تست Vendor روی SQL Server | اجرا نشده در محیط گفتگو |
| Vendor CI روی HEAD نهایی | هنوز نتیجه‌ای دریافت نشده؛ Verified نیست |
| بررسی تغییر Schema/Migration مشتری | تغییری در این محدوده انجام نشده |

## ریسک و سازگاری
- قرارداد API صفحه‌بندی در Issue #165 تغییر کرده بود؛ این Issue فقط رفتار مرزی آن را اصلاح می‌کند.
- هنوز باید مصرف‌کنندهٔ UI با envelope صفحه‌بندی هماهنگ شود؛ این موضوع خارج از محدودهٔ Issue #167 است.

## Git
- Branch: fix/issue-167-vendor-pagination-edge-cases
- Commit کد: 3a0cdc068857e724cafd753f6abe1f58ebaf73bc
- Commit تست: 8b04193bf2d4a73842c7b196f850ffad8900277f
- Pull Request: پس از بازبینی نهایی ایجاد می‌شود.
