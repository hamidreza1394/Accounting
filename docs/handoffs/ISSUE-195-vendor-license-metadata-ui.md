# تحویل وظیفه — ISSUE-195-vendor-license-metadata-ui

- **Issue:** [#195](https://github.com/hamidreza1394/Accounting/issues/195)
- **Agent/مسئول:** ChatGPT / مهندسی Vendor
- **تاریخ:** 2026-10-27
- **وضعیت:** Implemented in branch / Not verified

## هدف و معیارهای پذیرش
- [x] فرم RTL برای ثبت لایسنس Draft و ویرایش metadata لایسنس Draft با endpointهای موجود.
- [x] انتخاب مشتری واقعی و فیلتر سروری قرارداد/نصب بر اساس مشتری؛ UI نیز پاسخ‌های ناسازگار را فیلتر می‌کند.
- [x] حفظ RowVersion، دریافت antiforgery token تازه، مدیریت خطاهای 400/403/409/429/5xx و عدم ویرایش پس از خروج از Draft.
- [x] تست‌های Vitest برای ایجاد، تاریخ UTC، روابط اختیاری، مجوز، RowVersion stale و Draft-only.
- [x] تست HTTP روی SQL Server برای فیلتر customerId در فهرست‌های نصب، قرارداد و لایسنس.
- [ ] Vendor CI و .NET CI روی آخرین commit سبز شوند.
- [ ] PR بازبینی و ادغام شود؛ این handoff پس از نتیجه CI به‌روزرسانی خواهد شد.

## خلاصه تغییرات
- فرم LicenseForm برای metadata ایجاد/ویرایش ساخته شده است؛ این فرم صدور فایل امضاشده یا activation انجام نمی‌دهد.
- endpointهای فهرست نصب/قرارداد/لایسنس پارامتر اختیاری `customerId` می‌پذیرند و فیلتر را در SQL اعمال می‌کنند.
- BusinessLists اکنون ایجاد لایسنس و ویرایش فقط برای Draft را ارائه می‌دهد.
- API همچنان مرجع نهایی مالکیت مشتری، اعتبارسنجی و مجوز است.

## فایل‌های مهم تغییرکرده
- `Vendor/Vendor.Web/src/LicenseForm.tsx` — فرم metadata و انتخاب روابط.
- `Vendor/Vendor.Web/src/LicenseForm.test.tsx` — تست‌های رفتار UI.
- `Vendor/Vendor.Web/src/BusinessLists.tsx` — اتصال فرم به فهرست و کنترل‌های مجوز.
- `Vendor/Vendor.Host/Authorization/VendorBusinessEndpoints.cs` — فیلترهای سروری customerId.
- `tests/Vendor.Host.Tests/VendorCustomerMutationHttpIntegrationTests.cs` — تست واقعی SQL Server برای جلوگیری از برگرداندن روابط مشتری دیگر.
- `Vendor/Vendor.Web/src/App.test.tsx` — تست ویرایش فقط Draft و مجوزهای UI.
- `Vendor/README.md`, `docs/WORK_LOG.md` — وضعیت و تصمیم‌های پیگیری.

## تصمیم‌های فنی
- کد محصول آزاد باقی مانده چون API فعلی کاتالوگ محصول ارائه نمی‌کند؛ گزینهٔ ساختگی یا کاتالوگ فرانت‌اند ایجاد نشد.
- قرارداد و نصب اختیاری‌اند. اگر اپراتور مجوز خواندن فهرست مرتبط را ندارد، رابطهٔ موجود در ویرایش حفظ و کنترل آن غیرفعال می‌شود.
- صفحه‌بندی مرتبط‌ها در سرور اعمال می‌شود تا رابط به صفحهٔ اول فهرست سراسری محدود نشود.
- هیچ Migration یا تغییر دیتابیس مشتری انجام نشده است.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| Vendor UI typecheck / Vitest / build | در CI بررسی می‌شود | نتیجهٔ head نهایی هنوز در دست انتظار است |
| Vendor .NET build/test و SQL Server integration | در CI بررسی می‌شود | تست فیلتر مشتری اضافه شده؛ تا CI سبز Verified نیست |
| .NET CI / E2E / Publish Linux و Windows | در CI بررسی می‌شود | نتیجهٔ head نهایی هنوز در دست انتظار است |
| `git diff --check` | اجرا نشده در این رابط | CI و بازبینی diff لازم است |

## امنیت، داده و سازگاری
- همهٔ mutationها از antiforgery token تازه استفاده می‌کنند.
- RowVersion در ویرایش ارسال می‌شود و conflict به پیام قابل‌فهم تبدیل می‌شود.
- سرور همچنان کنترل می‌کند قرارداد و نصب به همان CustomerId تعلق داشته باشند؛ فیلتر UI به‌تنهایی مرز امنیتی محسوب نمی‌شود.
- فقط Vendor API و UI و تست‌های Vendor تغییر می‌کنند؛ کلید خصوصی، صدور رمزنگاری‌شده، دیتابیس حسابداری مشتری و Licensing.Contracts تغییر نمی‌کنند.

## ریسک‌ها و کارهای باقی‌مانده
- صدور رمزنگاری‌شده، KMS/HSM، activation آفلاین، چرخهٔ وضعیت لایسنس و maker-checker خارج از این Issue و هنوز مانع Production هستند.
- کاتالوگ مدیریت‌شدهٔ محصول وجود ندارد؛ به همین دلیل ProductCode متن آزاد محدودشده به 64 نویسه باقی مانده است.
- نتیجهٔ نهایی CI و بازبینی کد قبل از ادغام باید ثبت شود.

## شناسه‌های Git
- Branch: `feat/vendor-license-management-ui`
- Commit: `367dbdebaa5b2f9eba1b68f19acf92f4e2e84e86`.
- Pull Request: [#196](https://github.com/hamidreza1394/Accounting/pull/196) — باز، CI در حال اجرا.
