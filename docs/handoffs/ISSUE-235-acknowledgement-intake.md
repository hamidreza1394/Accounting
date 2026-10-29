# تحویل وظیفه — ISSUE-235-acknowledgement-intake

- **Issue:** [#235 — trusted provisioning acknowledgement](https://github.com/hamidreza1394/Accounting/issues/235), related [#228](https://github.com/hamidreza1394/Accounting/issues/228)
- **Agent/مسئول:** ChatGPT / GitHub tools
- **تاریخ:** 2026-10-29
- **وضعیت:** `Verified` برای Vendor-side intake؛ PR #293 ادغام شده است. Customer-side consumer همچنان مسدود به تصمیم معماری

## هدف و معیارهای پذیرش
- [x] دریافت acknowledgement فقط از طریق envelope امضاشده و کلید عمومی مورد اعتماد مستقل از envelope.
- [x] تطبیق request ID، nonce، payload hash، customer reference و installation identity پیش از تغییر state.
- [x] جلوگیری از بازنویسی نتیجهٔ نهایی؛ retry دقیق idempotent و پاسخ متفاوت conflict.
- [x] ثبت state و رخداد ممیزی در یک تراکنش Vendor.
- [x] تست SQL Server برای تأیید، retry دقیق، پاسخ متناقض و audit در CI موفق اجرا شد.
- [ ] پیاده‌سازی customer-side consumer در PR جداگانه؛ D-044/OD-14 و نیاز به fiscal period/parent reference/enrollment path باید تعیین‌تکلیف شود.

## خلاصه تغییرات
- مسیر machine-to-machine برای دریافت acknowledgement افزوده شد؛ احراز هویت آن با امضای ECDSA و trust store تنظیم‌شده انجام می‌شود، نه کوکی مدیر Vendor.
- نتیجهٔ معتبر `Applied`/`AlreadyApplied` وضعیت را `Confirmed` می‌کند؛ نتیجهٔ معتبر `Rejected` وضعیت را `Failed` می‌کند.
- ستون‌های persistence و Migration جدید فقط در Vendor DB افزوده شدند.
- retry دقیق ممیزی می‌شود؛ پاسخ متناقض نمی‌تواند نتیجهٔ ثبت‌شده را تغییر دهد.
- endpoint دارای rate limit است و کلید ناشناخته/لغوشده را رد می‌کند.

## فایل‌های مهم تغییرکرده
- `Vendor/Vendor.Host/Authorization/VendorProvisioningEndpoints.cs` — endpoint و تراکنش دریافت acknowledgement
- `Vendor/Vendor.Host/Authorization/ProvisioningAcknowledgementStateRules.cs` — نگاشت نتیجه به state نهایی
- `Vendor/Vendor.Host/Program.cs` — rate limit اختصاصی endpoint
- `Vendor/Vendor.Persistence/Domain/VendorProvisioningRequest.cs` — فیلدهای acknowledgement
- `Vendor/Vendor.Persistence/VendorDbContext.cs` — mapping EF Core
- `Vendor/Vendor.Persistence/Migrations/20261029130000_VendorProvisioningAcknowledgements.cs` — Migration Vendor-only
- `Vendor/Vendor.Persistence/Migrations/20261029130000_VendorProvisioningAcknowledgements.Designer.cs` و `VendorDbContextModelSnapshot.cs` — مدل هدف و snapshot
- `tests/Vendor.Host.Tests/ProvisioningAcknowledgementEndpointTests.cs` — رد کلید ناشناخته
- `tests/Vendor.Host.Tests/ProvisioningAcknowledgementStateRulesTests.cs` — تست‌های mapping state
- `docs/WORK_LOG.md` و `docs/SECURITY.md` — ثبت کار و مرز امنیتی

## تصمیم‌های فنی
- هیچ کلید خصوصی یا اعتبارنامهٔ مشتری به مخزن افزوده نشد.
- هیچ تغییری در `Server/**` یا schema مشتری ایجاد نشد.
- امضای Production همچنان fail-closed است تا تصمیم KMS/HSM در #236 گرفته شود.
- این PR به معنی تکمیل customer-side consumer یا آمادگی Production نیست.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `dotnet build Template.sln --no-restore -c Release` | اجرا نشده | در این نشست محیط اجرای .NET محلی در دسترس نبود |
| `dotnet test Template.sln --no-build -c Release` | اجرا نشده | باید CI روی head نهایی اجرا شود |
| `ProvisioningAcknowledgementSqlServerIntegrationTests` | موفق در CI | Vendor CI run 40869153676؛ local اجرا نشد |
| Vendor CI | موفق | [run 40869153676](https://github.com/hamidreza1394/Accounting/actions/runs/40869153676) |

## امنیت، داده و سازگاری
- تغییر Schema فقط به دیتابیس Vendor محدود است.
- state تغییر نمی‌کند مگر پس از اعتبارسنجی امضای envelope با کلید trust store مستقل.
- state و audit در یک تراکنش ثبت می‌شوند؛ خطای ممیزی باید کل تغییر را rollback کند.
- endpoint عمومی از نظر cookie، با امضای رمزنگاری‌شده و rate limiting محافظت می‌شود؛ قبل از Production باید CI و تست SQL Server تأیید شوند.

## ریسک‌ها و کارهای باقی‌مانده
- Customer-side consumer، claim اتمی nonce/request ID، ایجاد تراکنشی شرکت/مدیر و تولید acknowledgement در #235 هنوز لازم است.
- provider امضای Production و عملیات چرخش/بازیابی کلید در #236 هنوز تصمیم‌گیری/پیاده‌سازی نشده است.
- sink مستقل ممیزی در #107 نیازمند انتخاب مقصد و credential مستقل است.
- revocation آفلاین در #214 به ذخیره‌سازی monotonic سمت مشتری و runbook نیاز دارد.

## شناسه‌های Git
- Branch: `feat/vendor-acknowledgement-intake`
- Commit: head فعلی شاخه PR #293
- Pull Request: [#293](https://github.com/hamidreza1394/Accounting/pull/293) — merged
- Merge commit: `2884c61ade381670f754a1b8027c496a6e8eb7b2`