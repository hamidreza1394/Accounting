# تحویل وظیفه — ISSUE-107-checkpoint-runtime-guard

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/107
- **وضعیت:** guard fail-closed و تست‌ها در `main` ادغام شده و CI مربوط به PRها سبز است؛ provider مستقل واقعی هنوز انتخاب/پیکربندی نشده است

## تغییرات
- افزودن marker interface با نام IProductionVendorAuditCheckpointStore؛ fake/test storeها نباید آن را پیاده‌سازی کنند مگر در تست صریح.
- افزودن VendorAuditCheckpointStartupGuard و فراخوانی آن پیش از ساخت Host.
- در Production، نبود ثبت adapter مستقل باعث شکست startup با پیام صریح می‌شود؛ Development و تست‌ها بدون provider اجرا می‌شوند.
- تست‌ها حالت Production بدون store، ثبت فقط store عمومی، Development و ثبت adapter production را پوشش می‌دهند.

## اثر عملیاتی عمدی
- تا زمانی که provider مستقل واقعی با credential جدا و immutability/retention مناسب ثبت نشود، Vendor در محیط Production شروع به کار نخواهد کرد. این رفتار برای جلوگیری از ادعای امنیت ممیزی بدون checkpoint مستقل عمدی است.
- این guard صرفاً ثبت نوع production adapter را کنترل می‌کند؛ adapter واقعی باید خودش اتصال، credential، write/read، retention و availability را بررسی کند. Provider هنوز انتخاب/پیکربندی نشده است.
- هیچ fallback به فایل محلی، Vendor SQL Server یا fake store مجاز نیست.

## فایل‌های تغییرکرده
- Vendor/Vendor.Persistence/Auditing/VendorAuditCheckpoint.cs
- Vendor/Vendor.Host/Authorization/VendorAuditCheckpointStartupGuard.cs
- Vendor/Vendor.Host/Program.cs
- tests/Vendor.Host.Tests/VendorAuditCheckpointStartupGuardTests.cs
- Vendor/README.md، docs/WORK_LOG.md و این handoff

## شواهد CI و وضعیت نهایی این برش
- Guard و تست‌های آن در `main` هستند؛ این handoff دیگر شاخهٔ در انتظار CI نیست.
- اجرای Vendor Production همچنان عمداً تا ثبت provider مستقل واقعی متوقف می‌شود.
- موارد باقی‌مانده در Issue #107 عبارت‌اند از انتخاب و پیکربندی sink مستقل، credential/retention/immutability، جداسازی SQL principal، alerting و recovery runbook و شاهد اجرای مستقیم EF CLI migration در محیط کنترل‌شده.
- شاخهٔ اولیه: `fix/vendor-audit-checkpoint-fail-closed`؛ وضعیت ادغام guard را با معیارهای باقیماندهٔ Issue #107 اشتباه نگیرید.
