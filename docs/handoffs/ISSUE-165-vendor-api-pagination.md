# تحویل Issue #165 — صفحه‌بندی APIهای تجاری Vendor

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/165
- **Agent/مسئول:** ChatGPT / Faranou senior developer workflow
- **تاریخ:** 2026-10-27
- **وضعیت:** پیاده‌سازی اولیه؛ نیازمند CI و بازبینی

## هدف و معیارهای پذیرش
- [x] محدودکردن تعداد نتایج هر درخواست فهرست با page/pageSize.
- [x] افزودن metadata صفحه و تعداد کل نتایج.
- [x] حفظ ترتیب قطعی، projection DTO، cancellation token و permissionهای فعلی.
- [x] افزودن تست واحد برای نرمال‌سازی ورودی و metadata.
- [ ] تأیید Build/Test و CI روی شاخه.

## خلاصه تغییرات
- مشتریان، نصب‌ها، قراردادها و لایسنس‌ها اکنون از page و pageSize پشتیبانی می‌کنند.
- page پیش‌فرض 1، اندازه صفحه پیش‌فرض 25 و حداکثر اندازه صفحه 100 است.
- مقادیر کمتر از 1 به پیش‌فرض برمی‌گردند و pageSize بیش از سقف محدود می‌شود.
- پاسخ فهرست شامل items، page، pageSize، totalCount، totalPages، hasPreviousPage و hasNextPage است.
- پرس‌وجو قبل از materialization با Count/Skip/Take اجرا می‌شود؛ کل مجموعه برای صفحه‌بندی در حافظه بارگیری نمی‌شود.

## فایل‌های مهم تغییرکرده
- `Vendor/Vendor.Host/Authorization/VendorBusinessEndpoints.cs` — صفحه‌بندی bounded برای چهار endpoint فهرست.
- `tests/Vendor.Host.Tests/VendorPageRequestTests.cs` — آزمون‌های ورودی مرزی و metadata.
- `docs/handoffs/ISSUE-165-vendor-api-pagination.md` — این گزارش.
- `docs/WORK_LOG.md` — ثبت تاریخچه کار.

## تصمیم‌های فنی
- سقف pageSize برابر 100 انتخاب شد تا پاسخ‌ها bounded بمانند و امکان صفحه‌بندی برای مصرف‌کننده حفظ شود.
- ترتیب فهرست‌ها بر اساس شناسهٔ تجاری مرتب‌شده موجود حفظ شد؛ صفحه‌بندی روی query دیتابیس انجام می‌شود.
- Schema یا Migration تغییر نکرده است.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| تست واحد جدید | نوشته شد؛ اجرا نشده | منتظر GitHub Actions |
| Vendor build/test | اجرا نشده در محیط گفتگو | CI باید تأیید کند |
| بازبینی diff نهایی | محدود به API Git | تست‌های CI هنوز لازم‌اند |

## امنیت، داده و سازگاری
- هیچ permission policy یا گارد منبع‌محور حذف نشده است.
- این تغییر فقط API فهرست Vendor را تغییر می‌دهد و هیچ دیتابیس/مهاجرت حسابداری مشتری را لمس نمی‌کند.
- مصرف‌کننده‌ها باید از ساختار صفحه‌بندی جدید به‌جای آرایهٔ خام استفاده کنند؛ این یک تغییر قرارداد API است و UI هنوز باید به آن متصل شود.

## ریسک‌ها و کارهای باقی‌مانده
- تأیید CI و مرور API contract.
- اتصال صفحات UI به metadata و کنترل‌های صفحه‌بندی.
- بررسی اینکه هیچ مصرف‌کننده فعلی از فهرست‌های خام استفاده نکرده باشد.
- به‌روزرسانی OpenAPI/نمونه‌های API در صورت وجود قرارداد مستند.

## شناسه‌های Git
- Branch: `feat/issue-165-vendor-api-pagination`
- Commit کد: `e9292e66f449d5941c68177488f5556512f61c0d`
- Commit تست: `1fa01da16f0d3e64df9d0da61677166dbaf13a3d`
- Pull Request: در انتظار ایجاد پس از بازبینی و CI.
