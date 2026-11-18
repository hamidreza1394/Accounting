# تحویل وظیفه — ISSUE-UNNUMBERED-trade-payment-plan-validation

- Issue: ندارد (UNNUMBERED)
- شاخه: `feat/trade-payment-plan-validation`

## هدف و معیارهای پذیرش
اعتبارسنجی و تقسیم دقیق «طرح پرداخت» پیش‌فاکتور (مراحل پرداخت با روش و درصد/مبلغ) به‌صورت منطق خالص و بدون Schema.
- مجموع درصدها دقیقاً ۱۰۰ یا مجموع مبالغ دقیقاً برابر مبلغ نهایی.
- گرد‌کردن طبق ارقام اعشار واحد کوچک ارز؛ باقی‌ماندهٔ گرد‌کردن روی آخرین مرحله.
- خطاها فقط کد enum و شمارهٔ مرحله‌اند و ورودی را بازتاب نمی‌دهند.

## خلاصه تغییرات
- `PaymentMethodRef` و فهرست مرجع `TradeReferenceData.PaymentMethods` (TT، LC، DP، DA، OPEN_ACCOUNT) و `TryFindPaymentMethod`.
- `Trade.Application/Proformas`: `PaymentPlanCalculator.TryBuild`، `PaymentPlan`، `PaymentPlanStage`، `PaymentPlanStageInput`، `PaymentPlanIssue`، `PaymentPlanIssueCode`.
- سقف‌های فنی: حداکثر ۱۲ مرحله، دقت درصد ۴ رقم، `MaxAmount = 1e15`.
- حالت ترکیبی درصد و مبلغ، مرحلهٔ بدون مقدار یا با هر دو مقدار، و مرحلهٔ غیرآخر که پس از گرد‌کردن صفر شود رد می‌شوند.
- ۳۳ تست در `tests/Template.Tests/TradePaymentPlanTests.cs`.

## فایل‌های مهم تغییرکرده
- `Modules/Trade/Trade.Application/Proformas/PaymentPlan.cs`، `PaymentPlanCalculator.cs` (جدید)
- `Modules/Trade/Trade.Application/ReferenceData/ReferenceRecords.cs`، `MiscData.cs`، `TradeReferenceData.cs`
- `tests/Template.Tests/TradePaymentPlanTests.cs` (جدید)

## تصمیم‌های فنی
- فقط منطق خالص است؛ ذخیرهٔ ماندگار طرح پرداخت (جدول Proforma) به تصمیم Tenant/Company و ADRهای D-086/D-090 وابسته و Blocked است.
- روش‌های مجاز (مثلاً بر اساس Incoterm یا قرارداد) پارامتر ورودی است؛ قاعدهٔ تجاری آن تصمیم مالک است.
- فرمول جمع ردیف‌ها و مبلغ نهایی پیش‌فاکتور (D-086) هنوز باز است و در این PR نیست.

## بررسی و آزمون
- Build کامل Release: ۰ خطا.
- اجرای کامل با SQL Server آزمایشی: ۱۵۹۷ اجراشده / ۱۵۸۷ موفق / ۰ ناموفق / ۱۰ ردشده (Windows CNG).
- `check_static_assets.py` موفق؛ `git diff --check` موفق.
- ClientApp: اجرا نشد (تغییر کلاینت ندارد).
- بازبینی مستقل فقط‌خواندنی (`pr-review-toolkit:code-reviewer`): ۲ یافتهٔ مهم؛ مرحلهٔ غیرآخر با مبلغ صفر پس از گرد‌کردن (اصلاح و تست اضافه شد) و تست بدیهیِ عدم بازتاب ورودی (با تست ساختاری جایگزین شد). بازبینی امنیتی/معماری جدا لازم نبود (بدون Schema، ورودی HTTP یا Tenant).

## امنیت، داده و سازگاری
بدون Schema، Migration، Backfill، TenantId/CompanyId، API، UI، I/O یا Log. بدون تماس با Vendor، Licensing و Provisioning.

## ریسک‌ها و کارهای باقی‌مانده
- اتصال به Proforma ماندگار پس از تصمیم Tenant/Company (S3-0h).
- تصمیم مالک دربارهٔ روش‌های مجاز و سیاست گرد‌کردن (اکنون AwayFromZero برای مراحل غیرآخر).
- ردیف WORK_LOG عمداً اضافه نشد تا با PR دیگر تداخل نکند.

## شناسه‌های Git
Commit و PR پس از ایجاد در توضیحات PR ثبت می‌شود.
