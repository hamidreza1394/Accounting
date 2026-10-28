# تحویل وظیفه — ISSUE-228-provisioning-contract-v1

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/228
- **Agent/مسئول:** ChatGPT
- **تاریخ:** 2026-10-28
- **وضعیت:** slice قراردادی پیاده‌سازی‌شده؛ CI روی کد نهایی سبز است، پس از به‌روزرسانی این گزارش CI نهایی دوباره لازم است

## هدف و معیارهای پذیرش
- [x] تعریف قرارداد نسخه‌دار برای بستهٔ provisioning سمت مشتری.
- [x] امضای ECDSA P-256/SHA-256 روی بایت‌های دقیق JSON canonical و اعتبارسنجی fail-closed.
- [x] تعیین مهلت کوتاه بسته و اعتبارسنجی شناسه درخواست، nonce، نوع شرکت و کدهای زیرسیستم.
- [x] اطمینان از اینکه قرارداد فیلد گذرواژه، هش گذرواژه یا راز bootstrap ندارد.
- [ ] اتصال به API و UI، ذخیره/claim اتمی nonce برای جلوگیری از replay، وضعیت provisioning و تأییدیهٔ سمت مشتری.
- [x] CI روی کد نهایی پیش از مستندسازی سبز شد: .NET CI run 40723732882 و Vendor CI run 40723732546؛ پس از commit مستندات، بررسی نهایی CI لازم است.

## خلاصه تغییرات
- افزودن مدل CustomerProvisioningPayload و CustomerProvisioningPackage با نسخه‌بندی و JSON canonical با ترتیب ثابت.
- افزودن validator برای نسخه، ساختار، فهرست مجاز زیرسیستم‌ها، مهلت حداکثر ۲۴ ساعت، امضای ۶۴ بایتی P1363 و کلید عمومی ECDSA P-256 مورد اعتماد که caller resolve می‌کند.
- افزودن تست‌های موفقیت امضا، دستکاری، انقضا، نسخه ناشناخته، زیرسیستم تکراری/ناشناخته و نبود فیلد رمز/راز.

## فایل‌های مهم تغییرکرده
- Licensing.Contracts/CustomerProvisioningPackage.cs — مدل نسخه‌دار و canonical JSON.
- Licensing.Contracts/CustomerProvisioningPackageValidator.cs — اعتبارسنجی ساختاری و رمزنگاری.
- tests/Licensing.Contracts.Tests/CustomerProvisioningPackageValidatorTests.cs — آزمون‌های regression.
- docs/handoffs/ISSUE-228-provisioning-contract-v1.md — این گزارش.
- docs/WORK_LOG.md — ثبت پیشرفت و محدودیت‌ها.

## تصمیم‌های فنی
- قرارداد مستقل از ASP.NET Core، EF Core و دیتابیس باقی می‌ماند.
- کلید عمومی باید از trust store پیکربندی‌شدهٔ مستقل از بسته resolve شود؛ شناسهٔ کلید داخل بسته به‌تنهایی اعتماد ایجاد نمی‌کند.
- validator امضا و مهلت را بررسی می‌کند، اما replay protection فقط با ذخیره و claim اتمی nonce در مصرف‌کننده ممکن است؛ این slice آن را ادعا نمی‌کند.
- هیچ رمز خام، hash رمز مشتری، private key، migration یا تغییر Server/** اضافه نشده است.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| تست محلی .NET | اجرا نشد | محیط فعلی اتصال GitHub به checkout محلی/SDK دسترسی ندارد؛ CI لازم است. |
| .NET CI روی کد (SHA 18e684a0c78f675ca35fecdc5b8d2d42c3b10916) | موفق | run 40723732882؛ 576/576 آزمون، 0 شکست، E2E، publish Linux/Windows و secret scan همگی موفق. |
| Vendor CI روی کد (SHA 18e684a0c78f675ca35fecdc5b8d2d42c3b10916) | موفق | run 40723732546؛ build/test، EF snapshot guard و publish مستقل Vendor موفق. |

## امنیت، داده و سازگاری
- فقط قرارداد مشترک Licensing.Contracts و تست مربوط تغییر می‌کند؛ Vendor Host هنوز از این قرارداد استفاده نمی‌کند.
- هیچ schema یا دیتابیس مشتری تغییر نکرده است.
- مصرف‌کننده باید nonce را در ذخیره‌سازی پایدار به‌صورت اتمی claim کند و پاسخ provisioning را به‌صورت قابل اعتبارسنجی امضا کند.

## ریسک‌ها و کارهای باقی‌مانده
- پیاده‌سازی هنوز به API، UI، پایگاه دادهٔ Vendor، مصرف‌کنندهٔ مشتری یا جریان تأیید provisioning وصل نشده است.
- اجرای اول .NET CI روی SHA قبلی با دو شکست در تست‌های جدید انجام شد؛ علت، نبود JsonNamingPolicy.CamelCase هنگام deserialize کردن JSON canonical بود. تنظیم نام‌گذاری اصلاح شد و اجرای بعدی 576/576 تست را با موفقیت گذراند.
- پس از تغییر مستندات، CI روی SHA جدید باید دوباره سبز شود؛ تا آن زمان Merge انجام نشود.
- Issue #228 باز می‌ماند.

## شناسه‌های Git
- Branch: feat/issue-228-provisioning-contract-v1
- Commit: c8e9b97c6795ad8d465ce7b946df666d63a3cf34 (آخرین کد در زمان نگارش)
- Pull Request: در حال ایجاد
