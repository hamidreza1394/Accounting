# Issue #393 — تصمیم‌های مالک و قرارداد اجرایی S1-b (2026-11-19)

- مراجع تصمیم: D-107 و D-108 در docs/DECISIONS.md.
- وضعیت: تصمیم‌ها Accepted؛ پیاده‌سازی NotImplemented. این handoff مجوز تغییر Customer DB یا بازکردن Gate v5 نیست.
- مبنای طراحی: handoff نسخه ۲.۱ و PR #717؛ این سند فقط تصمیم‌های جدید مالک را به طراحی قبلی اضافه می‌کند.

## ۱) اصلاح تعارض‌های موجود
1. عبارت LICENSING_IMPLEMENTATION_PLAN.md §9.1 که سفارش پرداخت‌شده/تأییدشده را پیش‌شرط handoff به Engine می‌دانست با D-108 superseded است.
2. جمله‌های v2.1 که گیت پرداخت را به تصمیم ۱۱ موکول می‌کردند با تصمیم مالک بسته شدند: هیچ گیت پرداخت برای صدور وجود ندارد.
3. وضعیت‌های سفارش، پرداخت و fulfillment مستقل‌اند؛ Purchased یا PaymentVerified به‌تنهایی به‌معنی Issued/Delivered/Applied نیست.
4. Applied فقط پس از ACK معتبر Customer یا تأیید authoritative Cloud ثبت می‌شود؛ قاعده #393/#394 حفظ می‌شود.

## ۲) قرارداد پرداخت و صدور
- Order دارای مبلغ کل، Currency و تاریخ سررسید اختیاری است.
- چند VendorPayment دستی برای هر Order مجاز است؛ ارز Payment برابر ارز Order است.
- Payment status: Recorded | Verified | Rejected؛ فقط Verified در جمع پرداخت‌شده لحاظ می‌شود.
- Order payment projection در زمان خواندن: Unpaid | PartiallyPaid | Paid؛ با مبلغ باقی‌مانده مثبت و DueAt گذشته، Overdue نیز نمایش داده می‌شود. بدون DueAt، Unpaid/Partial قابل گزارش است اما Overdue نیست.
- Payment Maker-Checker: ResourceType=salesOrderPayment، permission=vendor.payments.verify.
- Issuance Maker-Checker: ResourceType=salesOrderLine، permission=vendor.licenses.issue.
- Requester و reviewer در هر چرخه متفاوت‌اند. Audit هر دو چرخه مستقل است.
- صدور پایه/Amendment، فعال‌سازی آنلاین یا آفلاین، تحویل و ACK به ثبت یا تأیید پرداخت وابسته نیستند.
- Paymentهای ثبت/رد/تأییدشده، اصلاح سررسید و نتیجه صدور تاریخچه قابل ممیزی دارند.

## ۳) گزارش و هشدار مطالبات
- فهرست سفارش‌های Unpaid، Partial، DueSoon و Overdue.
- مبلغ باقی‌مانده = مبلغ کل سفارش − جمع Paymentهای Verified؛ محاسبه decimal و در ارز سفارش.
- هشدارها فقط در داشبورد و گزارش داخلی Vendor؛ در نسخه اول بدون scheduler، BackgroundService، Message Broker، SMS/Email، درگاه پرداخت، Ledger، PaymentAllocation یا CRM/Ticketing جدید.
- عدم پرداخت به‌خودی‌خود License، Perpetual use، Support یا Update را تغییر نمی‌دهد.

## ۴) سیاست امضای Vendor نسخه اول
- D-107: Windows CNG/Certificate Store، MachineKey، private key non-exportable، ACL محدود به سرویس، AssuranceLevel=SoftwareProtected.
- هیچ TPM/HSM/KMS اجباری نیست. برای Cloud نسخه اول Windows VM اختصاصی؛ Linux host تا ارائه Provider OS-native تست‌شده خارج از ماتریس پشتیبانی اولیه است.
- ریسک مدیر محلی/میزبان صریح است؛ SoftwareProtected معادل Hardware/HSM نیست.
- Maker-Checker، MFA، Audit، rotation/overlap، revocation و fail-closed حفظ می‌شوند؛ هیچ fallback خودکار به کلید آزمایشی وجود ندارد.
- کلید ریشه offline و از کلیدهای صدور جداست؛ کلیدهای License/Provisioning/ACK/Revocation با purpose مستقل باقی می‌مانند.

## ۵) مرز TPM در Customer
D-107 فقط Vendor central signing را تغییر می‌دهد. D-057 برای anti-rollback snapshot ابطال آفلاین Customer همچنان TPM NV Extend می‌خواهد. OD-27 باز است؛ تا تصمیم جداگانه، هیچ software counter یا SQL-only state به‌عنوان جایگزین TPM معرفی نمی‌شود. D-058 SoftwareProtected برای Installation identity و D-055 برای Customer ACK مستقل‌اند.

## ۶) برش‌های اجرایی بعدی
1. S1-b1 — Commercial Order/Line persistence: بررسی Entity/DbContext موجود، typed Order و Line، RowVersion، constraints و تست SQL Server.
2. S1-b2 — Payment record/verification: VendorPayment، ثبت دستی، Maker-Checker مستقل، permission و Audit.
3. S1-b3 — Issuance handoff: approval مستقل Line، استفاده از Builder/IssuanceService/Signer موجود، حفظ sequence و تراکنش اتمیک.
4. S1-b4 — Delivery/ACK: استفاده از مسیر #394 و allow-list ACK موجود؛ Applied فقط پس از ACK معتبر/Cloud authoritative.
5. S1-b5 — Receivables projection: queryهای صفحه‌بندی‌شده، داشبورد/گزارش Unpaid/Partial/Overdue و TimeProvider؛ بدون scheduler.
6. S1-b6 — Support/Update lifecycle: دوره‌های مستقل D-097، amendment از همان مسیر صدور و هشدار پشتیبانی.
7. S1-a/S1-c/S1-d integration: فعال‌سازی آنلاین/آفلاین، reactivation و Update با قرارداد مشترک و بدون سیستم موازی.

هر برش یک Issue/branch/PR مستقل دارد. پیش از Migration، VendorDbContext، entityهای موجود، کلیدهای مرکب و تست SQL Server واقعی بررسی می‌شوند. Customer DB و Gate v5 فقط با تصمیم/مجوز مستقل تغییر می‌کنند.

## ۷) معیارهای پذیرش حداقلی
- صدور موفق با صفر Payment، Payment Recorded، Payment Rejected، Payment جزئی و کامل.
- ثبت و تأیید Payment مستقل از approval صدور؛ self-approval در هر دو مسیر رد شود.
- Sequence تکراری، stale RowVersion، replay و signer unavailable fail-closed.
- مجموع Verified payments و وضعیت‌های مشتق‌شده با مرز مبلغ/تاریخ صحیح باشد.
- گزارش مطالبات بدون Job و با query صفحه‌بندی‌شده کار کند.
- Dedicated Perpetual با پایان Support قفل نشود؛ Cloud Term طبق D-098 مسیر expiry/recovery خود را حفظ کند.
- مسیر فایل آفلاین و مسیر آنلاین از همان Envelope، signature، binding و sequence استفاده کنند.

## ۸) محدودیت‌های صریح
- این سند به معنی وجود پیاده‌سازی نیست.
- این سند Gate صدور v5 را باز نمی‌کند و Production signing را تا پیاده‌سازی و تست D-107 فعال نمی‌کند.
- هیچ Customer-side Migration، TPM bypass، HSM/KMS purchase، درگاه پرداخت یا سرویس اعلان پولی مجاز نمی‌شود.
