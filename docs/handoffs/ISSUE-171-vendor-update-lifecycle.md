# تحویل Issue #171 — ویرایش امن و چرخهٔ وضعیت Vendor

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/171
- **Agent/مسئول:** ChatGPT / Faranou senior developer workflow
- **تاریخ:** 2026-10-27
- **وضعیت:** پیاده‌سازی اولیه و CI روی HEAD فعلی تأیید شده‌اند؛ آزمون HTTP کامل و پوشش cross-resource/rollback همچنان کار تکمیلی‌اند.

## هدف و معیارهای پذیرش
- [x] DTOهای allow-list برای ویرایش چهار منبع تجاری.
- [x] RowVersion هشت‌بایتی SQL Server به صورت Base64 در پاسخ‌ها و ورودی‌های نوشتن.
- [x] رد token نامعتبر و پاسخ Conflict برای concurrency token قدیمی.
- [x] transition matrix محدود Active↔Suspended برای Customer/Installation.
- [x] قرارداد و لایسنس پس از خروج از Draft قابل ویرایش نیستند.
- [x] Anti-forgery، permission policy، resource evaluator و audit در تراکنش در مسیرهای جدید.
- [x] تست قواعد و metadata concurrency model.
- [x] Vendor CI و .NET CI روی HEAD فعلی این شاخه سبز شدند؛ اجرای .NET CI با شناسه `40467317576` تمام jobها از جمله `publish-windows` را با موفقیت گذراند.
- [ ] تست‌های endpoint HTTP و آزمون cross-resource/rollback واقعی روی SQL Server در کار تکمیلی پوشش داده شوند.

## خلاصه تغییرات
- چهار endpoint \`PUT /api/vendor/{customers|installations|contracts|licenses}/{id}\` با DTOهای صریح و validation.
- \`POST /api/vendor/customers/{id}/status\` و \`POST /api/vendor/installations/{id}/status\` با انتقال وضعیت فقط بین Active و Suspended.
- تمام DTOهای پاسخ دارای \`rowVersion\` Base64 هستند تا UI بتواند concurrency را به سرور بازگرداند.
- خطای rowversion نامعتبر: HTTP 400؛ خطای نسخه قدیمی: HTTP 409.
- از RowVersion موجود استفاده شده؛ هیچ Migration جدیدی ایجاد نشده است.

## فایل‌های مهم تغییرکرده
- \`Vendor/Vendor.Host/Authorization/VendorBusinessEndpoints.cs\` — endpointهای PUT/status و RowVersion در DTO.
- \`Vendor/Vendor.Host/Authorization/VendorBusinessMutationRules.cs\` — قواعد ورودی و transition.
- \`tests/Vendor.Host.Tests/VendorBusinessMutationRulesTests.cs\` — تست واحد قواعد.
- \`tests/Vendor.Host.Tests/VendorPersistenceTests.cs\` — تأیید metadata concurrency برای چهار entity.
- \`Vendor/README.md\` و \`docs/WORK_LOG.md\` — وضعیت و سابقه.

## تصمیم‌های فنی
- فیلدهای CustomerId/مالکیت تغییر نمی‌کنند؛ روابط Contract/Installation لایسنس با رکوردهای ذخیره‌شده اعتبارسنجی می‌شوند.
- Customer و Installation فقط Active↔Suspended را می‌پذیرند.
- Contract و License فقط در Draft قابل ویرایش‌اند. تغییر وضعیت قراردادی یا فعال‌سازی/ابطال License نیازمند تصمیم تجاری، maker-checker و جریان امضای مستقل است و در این PR پیاده‌سازی نمی‌شود.
- هیچ وابستگی runtime یا بسته جدیدی اضافه نشده است.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| VendorBusinessMutationRulesTests | CI موفق | در اجرای CI مربوط به PR #172 |
| VendorPersistenceTests rowversion mapping | CI موفق | metadata مدل بررسی شد |
| Vendor CI | موفق | run `40467317429` |
| .NET CI | موفق | run `40467317576`؛ 569 تست .NET موفق، انتشار Windows/Linux و E2E نیز موفق |
| HTTP integration / cross-resource / rollback | اجرا نشده | پوشش تکمیلی لازم است |

## امنیت، داده و سازگاری
- تمام عملیات نوشتن Anti-forgery و authorization منبع‌محور دارند.
- ممیزی بعد از SaveChanges و پیش از commit در همان تراکنش نوشته می‌شود؛ خطای ممیزی مانع commit می‌شود.
- هیچ Schema/Migration دیتابیس مشتری تغییر نکرده است.
- RowVersion موجود Vendor استفاده می‌شود؛ migration جدیدی ساخته نشده است.

## ریسک‌ها و کارهای باقی‌مانده
- تست‌های HTTP کامل لازم است تا مسیرهای auth/CSRF/resource policy در endpointهای PUT و status با Host واقعی بررسی شوند.
- برای Contract/License انتقال وضعیت و عملیات پرخطر، ماتریس رسمی و maker-checker باید قبل از پیاده‌سازی تعریف شوند.
- License metadata هنوز امضا یا اعتبارسنجی آفلاین نیست.

## شناسه‌های Git
- Branch: \`feat/issue-171-vendor-update-lifecycle\`
- PR: پس از بازبینی کد و CI ایجاد خواهد شد.
