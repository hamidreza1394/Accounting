# تحویل وظیفه — ISSUE-393-vendor-payment-cycle-d108

- **Issue:** #393 (S1-b: چرخهٔ تجاری و پرداخت) — تصمیم مالک: ADR **D-108** (پذیرفته‌شده 2026-11-19) و مکمل **D-097** (پشتیبانی/Update مستقل)
- **Agent/مسئول:** GLM (مأموریت مالک 2026-11-19)
- **تاریخ:** 2026-11-19
- **وضعیت:** پیاده‌سازی کامل در کد و تست واحد؛ تست چرخهٔ SQL Server به CI (و میزبان ویندوز مالک) موکول است

## هدف و معیارهای پذیرش (از D-108)
- [x] ثبت دستی چند Payment برای هر سفارش با ستون‌های typed (مبلغ decimal(19,4)، ارز، روش، مرجع بیرونی، زمان ادعایی پرداخت).
- [x] وضعیت‌های Unpaid / PartiallyPaid / Paid + Overdue — همه در زمان خواندن محاسبه می‌شوند؛ هیچ ستون وضعیت روی سفارش ذخیره یا ویرایش نمی‌شود.
- [x] فقط Paymentهای Verified در جمع پرداخت‌شده لحاظ می‌شوند؛ Recorded/Rejected بدهی را کم نمی‌کنند.
- [x] تأیید مستقل پرداخت: ResourceType جدید `salesOrderPayment` + permission جدید `vendor.payments.verify` (صدا: `vendor.payments.manage`)؛ self-approval مجاز طبق D-109، RowVersion، تراکنش واحد.
- [x] Audit مستقل هر دو چرخه: `vendor.payment.recorded/approval-requested/verified/rejected` و `vendor.sales-order.created` — جدا از Audit صدور.
- [x] صدور و فعال‌سازی License مستقل از پرداخت: هیچ کدی بین Payment و LicenseIssuance وجود ندارد (هر دو سرویس مستقل‌اند) و برعکس.
- [x] ارز Payment باید با ارز Order برابر باشد (پاسخ `payment_currency_mismatch`).
- [x] گزارش مطالبات `/api/vendor/receivables`: فقط سفارش‌های با بدهی باقی‌مانده، محاسبه در زمان خواندن، صفحه‌بندی‌شده؛ **بدون Scheduler/Background Job/Broker/درگاه/Ledger**.
- [x] سفارش پرداخت‌نشده حذف یا مخفی نمی‌شود؛ سوابق پرداخت‌های ثبت/تأیید/ردشده حفظ می‌شود.
- [x] Overpayment بدهی منفی نمی‌سازد (remaining=0)؛ مبلغ صفر → Paid.
- [ ] تست SQL Server چرخهٔ کامل در CI ویندوز/لینوکس اجرا شود (گیت `FARANO_VENDOR_TEST_SQLSERVER`) — این PR گیت را می‌گذارد؛ نتیجه پس از CI ثبت می‌شود.

## خلاصه تغییرات
- سه Entity جدید در Vendor DB: `VendorSalesOrders` (+Lines) و `VendorPaymentRecords` با CHECK constraintها و ایندکس‌های یکتای `(CustomerId, OrderNumber)`.
- `VendorSalesOrderPaymentRules`: ماشین وضعیت مشتق خالص و تست‌پذیر (12 تست واحد).
- `VendorPaymentVerificationService`: اجرای تصمیم داخل تراکنش approval (هم‌الگوی صدور License)؛ شاخهٔ جدید `salesOrderPayment` در `VendorApprovalEndpoints` با mapping permission.
- کاتالوگ permission نسخه‌دار: `vendor.payments.read|manage|verify` + ثبت در `VendorResourceCatalog` و `VendorResourceAccessEvaluator`.
- endpointها: لیست/جزئیات سفارش با وضعیت مشتق، ایجاد سفارش (اعتبارسنجی جمع خطوط)، ثبت پرداخت، درخواست verify/reject، گزارش مطالبات.
- **بدون تغییر در Customer DB و بدون هرگونه اثر بر صدور/فعال‌سازی/Activation.**

## فایل‌های مهم تغییرکرده
- `Vendor/Vendor.Persistence/Domain/VendorSalesEntities.cs` — Entityها
- `Vendor/Vendor.Persistence/VendorDbContext.cs` — کانفیگ + CHECKها
- `Vendor/Vendor.Persistence/Migrations/20261119175924_VendorSalesOrdersAndPayments.*` — Migration جدید Vendor (بازبینی‌شده؛ گیت `has-pending-model-changes` سبز)
- `Vendor/Vendor.Host/Authorization/VendorSalesOrderPaymentRules.cs` — قواعد مشتق
- `Vendor/Vendor.Host/Authorization/VendorPaymentVerificationService.cs` — تأیید تراکنشی
- `Vendor/Vendor.Host/Authorization/VendorSalesEndpoints.cs` — APIهای سفارش/پرداخت/مطالبات
- `Vendor/Vendor.Host/Authorization/VendorApprovalEndpoints.cs` — شاخهٔ salesOrderPayment + service
- `Vendor/Vendor.Host/Authorization/VendorPermissionCatalog.cs`، `VendorResourceAuthorization.cs`، `VendorResourceAccessEvaluator.cs` — permissionهای جدید
- `Vendor/Vendor.Host/Program.cs` — ثبت سرویس و endpoint
- `tests/Vendor.Host.Tests/VendorSalesOrderPaymentRulesTests.cs` (۱۲ تست)، `tests/Vendor.Host.Tests/VendorSalesPaymentSqlServerTests.cs` (۲ تست SQL-گیت)

## تصمیم‌های فنی
- permission تأییدکننده در endpoint تصمیم با `ApprovalsReview` (گیت سراسری) + `PaymentsVerify` (منبع‌محور) ترکیب شده است؛ requester فقط `PaymentsManage`.
- جمع خطوط سفارش باید برابر مبلغ کل باشد؛ LineAmount = Quantity × UnitAmount با گرد کردن ۴ رقم اعشار.
- برخلاف License (که status «Issued» دارد)، سفارش هیچ status ذخیره‌شده‌ای ندارد — دقیقاً طبق D-108 §6.
- هیچ Gateway، Allocation به Lineها، scheduler یا Email/SMS اضافه نشد (ممنوعیت صریح D-108).

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| `dotnet build Vendor.sln -c Release` | موفق — ۰ خطا | SDK 10.0.100، Linux |
| `dotnet ef migrations has-pending-model-changes` | «No changes have been made to the model since the last migration.» | گیت snapshot سبز |
| `dotnet test --filter VendorSalesOrderPaymentRules` | ۱۲ موفق / ۰ شکست | unit |
| `dotnet test Vendor.sln` کامل | نتیجه در PROJECT_STATUS ثبت می‌شود | تست‌های SQL در CI اجرا می‌شوند |
| تست چرخهٔ SQL روی دیتابیس واقعی | اجرا نشده محلی (نیازمند SQL Server) | گیت FARANO_VENDOR_TEST_SQLSERVER در CI |

## امنیت، داده و سازگاری
- Migration فقط Vendor DB؛ هیچ FK یا جدول مشترکی با Customer نیست.
- antiforgery روی همهٔ mutationها؛ مجوزدهی سمت سرور؛ RowVersion و self-approval مجاز طبق D-109؛ Audit فقط metadata.
- هیچ secret یا کلیدی در کد/تست نیست؛ تست‌ها دیتابیس disposable با نام `FaranouVendorPayments_*` می‌سازند و حذف می‌کنند.

## ریسک‌ها و کارهای باقی‌مانده
- نتیجهٔ تست SQL در CI ویندوز/لینوکس پس از باز شدن PR ثبت می‌شود؛ تا آن زمان Verified نیست.
- UI داشبورد Vendor برای سفارش/پرداخت/مطالبات (React) مرحلهٔ بعدی است؛ APIها آماده‌اند.
- amendment تمدید Support/Update (#565 موجود) به این چرخه در PR جداگانه متصل می‌شود؛ صدور amendment همچنان از مسیر Maker-Checker صدور عبور می‌کند.
- سیاست `CustomerNumber` قابل‌ویرایش (یافتهٔ قبلی ممیزی) خارج از دامنه این PR است.

## شناسه‌های Git
- Branch: `agent/393-vendor-payment-cycle`
- Commit: `a7147435`
- Pull Request: #722
