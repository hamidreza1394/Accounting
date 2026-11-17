# تحویل وظیفه — ISSUE-698-vendor-company-provisioning-ack-errors

- **Issue:** [#698 — پذیرش ACKهای رد شرکت‌محور در Vendor](https://github.com/hamidreza1394/Accounting/issues/698)
- **Agent/مسئول:** عامل اجرایی Vendor
- **تاریخ:** 2026-11-17
- **وضعیت:** PR #699 باز است؛ نخستین Vendor CI روی HEAD 5eae14ad به‌علت خطای کامپایل در تست integration شکست خورد. علت ریشه‌ای، نام‌گذاری متغیرهای محلی تکراری بود و اصلاح حداقلی روی همان شاخه ثبت می‌شود؛ تا اجرای مجدد CI روی HEAD اصلاح‌شده، وضعیت همچنان Not Verified است.

## هدف و معیارهای پذیرش
- [x] چهار کد ثابت Rejected که Customer v5 تولید می‌کند در allow-list محدود Vendor ثبت شود.
- [x] تست واحد برای شش کد مجاز قدیمی و جدید افزوده شود.
- [x] تست SQL Server برای پذیرش ACK امضاشده، Failed/FailureCode، metadata/Audit، retry دقیق و conflict اضافه شود.
- [x] کد ناشناخته و diagnostic/PII بدون mutation رد شوند.
- [x] مستندات امنیت، تست، وضعیت، فهرست و Work Log به‌روز شوند.
- [ ] اجرای تست‌های واحد و SQL Server در CI روی HEAD نهایی.
- [ ] تأیید post-merge CI روی main.

## خلاصه تغییرات
- چهار کد provisioning_license_not_installed، provisioning_not_entitled_for_company، provisioning_company_reference_conflict و provisioning_company_capacity_exceeded به دو کد قدیمی allow-list افزوده شدند.
- تست واحد اکنون هر شش کد را بررسی می‌کند.
- تست SQL Server علاوه بر کد آزاد حاوی diagnostic/PII و کد ناشناخته، هر چهار ACK رد شرکت‌محور را امضا و به endpoint Vendor ارسال می‌کند؛ انتقال به Failed، ذخیرهٔ کد دقیق، metadata و Audit، retry دقیق idempotent و conflict برای ACK متفاوت بررسی می‌شود.
- رفتار Applied/AlreadyApplied و دو کد قدیمی تغییر نکرده است.

## فایل‌های مهم تغییرکرده
- Vendor/Vendor.Host/Authorization/ProvisioningAcknowledgementStateRules.cs — allow-list محدود شش کد ثابت.
- tests/Vendor.Host.Tests/ProvisioningAcknowledgementStateRulesTests.cs — پوشش چهار کد جدید.
- tests/Vendor.Host.Tests/ProvisioningAcknowledgementSqlServerIntegrationTests.cs — regression امضاشده روی SQL Server.
- docs/SECURITY.md — فهرست کدهای قابل ذخیره و مرز رد diagnostic/PII.
- docs/LICENSING_WORKFLOWS.md — وضعیت گسترش allow-list Vendor و گیت‌های باقی‌مانده.
- docs/TESTING.md — قرارداد اجرای تست SQL Server در CI.
- docs/PROJECT_STATUS.md — وضعیت دقیق و برچسب Not Verified تا CI.
- docs/WORK_LOG.md — ثبت مأموریت و محدودیت‌های اجرا.
- docs/INDEX.md — پیوند همین handoff.

## تصمیم‌های فنی
- هیچ تصمیم معماری جدیدی گرفته نشد؛ فقط allow-list با خروجی‌های ثابت موجود Customer هم‌تراز شد.
- امضای معتبر به‌تنهایی مجوز ذخیرهٔ هر ErrorCode نیست. کدهای ناشناخته و متن آزاد همچنان fail-closed رد می‌شوند.
- هیچ تغییری در Server/**، Customer DB/Migration، Licensing.Contracts، signer/trust store، schema Vendor یا Gate صدور v5 انجام نشد.
- Gate دوکلیدی v5 و CustomerConsumerReady=false بدون تغییر باقی ماند.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| dotnet build Vendor.sln --no-restore -c Release | اجرا نشده | این اجرا checkout/SDK محلی در اختیار نداشت؛ CI لازم است. |
| dotnet test Vendor.sln -c Release | اجرا نشده | تست واحد و SQL Server فقط پس از اجرای واقعی CI Verified می‌شوند. |
| ProvisioningAcknowledgementStateRulesTests | در انتظار CI | شش کد مجاز و کدهای نامعتبر. |
| ProvisioningAcknowledgementSqlServerIntegrationTests | در انتظار CI | نیازمند FARANO_VENDOR_TEST_SQLSERVER و FARANO_REQUIRE_VENDOR_SQLSERVER=1. |
| git diff --check | اجرا نشده | تغییرات از طریق GitHub Git Data API ثبت می‌شوند؛ جایگزین اجرای محلی نیست. |
| Vendor CI اولیه روی 5eae14ad | ناموفق | Build به‌دلیل CS0136 در خطوط 277 و 280 همین تست متوقف شد: repeatedBody و repeated در scope تودرتو با نام‌های موجود در scope بیرونی دوباره تعریف شده بودند. دو job ویندوز/اوبونتو checkpoint regression نیز قبل از اجرای تست، هنگام build همین پروژه شکست خوردند؛ این شکست، نتیجهٔ خود تست قفل checkpoint نبود. |
| اصلاح regression | ثبت شد؛ CI مجدد در انتظار | متغیرهای retry در شاخهٔ داخلی به rejectedRepeatedBody و rejectedRepeated تغییر نام داده شدند؛ رفتار assertion و منطق ACK تغییر نکرد. نتیجهٔ نهایی فقط پس از CI روی HEAD اصلاح‌شده ثبت می‌شود. |

## امنیت، داده و سازگاری
- فقط کدهای ثابت allow-list شده در FailureCode ذخیره می‌شوند؛ متن exception، SQL diagnostic، PII و کد ناشناخته رد می‌شوند.
- ACK همچنان باید با کلید عمومی مورد اعتماد و bindingهای request/nonce/payload/customer/installation اعتبارسنجی شود.
- retry دقیق idempotent می‌ماند؛ ACK متفاوت برای درخواست نهایی‌شده conflict است.
- هیچ secret، کلید خصوصی، رمز، hash رمز یا credential عملیاتی اضافه نشده است.
- هیچ دیتابیس مشتری یا Migration آن تغییر نکرده است.

## ریسک‌ها و کارهای باقی‌مانده
- تا اجرای موفق تست SQL Server در CI، انتقال state و تراکنش metadata/Audit Not Verified است.
- پس از Merge باید CI روی SHA جدید main دوباره بررسی و نتیجه به این handoff، Project Status و Work Log افزوده شود.
- این تغییر به‌تنهایی customer consumer، فعال‌سازی Gate v5، signer عملیاتی یا Production readiness را اثبات نمی‌کند.

## شناسه‌های Git
- **Branch:** agent/698-vendor-company-provisioning-ack-errors
- **Base:** main@0797bcad754795e071342102c9192b88050f6663
- **HEAD پیش از اصلاح CI:** 5eae14ad8eb3bfb32a92dbf14ffaebec522e8024؛ SHA اصلاح‌شده را از PR #699 و commit بعدی بخوانید.
- **Pull Request:** [#699](https://github.com/hamidreza1394/Accounting/pull/699)؛ CI روی HEAD نهایی pending است.
