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
- `PaymentPlan` و `PaymentPlanStage` سازندهٔ عمومی و `init` ندارند (سازنده `internal`، property فقط‌خواندنی)؛ طرح نامعتبر دستی ساخته نمی‌شود و با `with` تغییر نمی‌کند. تنها تولیدکننده `PaymentPlanCalculator.TryBuild` است. بازیابی طرح ذخیره‌شده در آینده با فراخوانی دوبارهٔ `TryBuild` روی ارز، مبلغ نهایی و ورودی مراحل (درصد یا مبلغ) انجام می‌شود و خواننده باید مبالغ ذخیره‌شده را با مبالغ بازسازی‌شده مقایسه کند؛ نوع Restore جدا ساخته نشد چون هنوز ذخیره‌ای وجود ندارد.
- ۴۰ تست در `tests/Template.Tests/TradePaymentPlanTests.cs`.

## فایل‌های مهم تغییرکرده
- `Modules/Trade/Trade.Application/Proformas/PaymentPlan.cs`، `PaymentPlanCalculator.cs` (جدید)
- `Modules/Trade/Trade.Application/ReferenceData/ReferenceRecords.cs`، `MiscData.cs`، `TradeReferenceData.cs`
- `tests/Template.Tests/TradePaymentPlanTests.cs` (جدید)

## تصمیم‌های فنی
- فقط منطق خالص است؛ ذخیرهٔ ماندگار طرح پرداخت (جدول Proforma) به تصمیم Tenant/Company و ADRهای D-086/D-090 وابسته و Blocked است.
- روش‌های مجاز پارامتر ورودی کنترل‌شده است: `null` یعنی همهٔ روش‌های فهرست مرجع، فهرست خالی یعنی هیچ روشی مجاز نیست، و هر روش خارج از فهرست مرجع همیشه رد می‌شود. منبع واقعی «روش‌های مجاز هر Company» هنوز تصمیم ندارد؛ جدول یا تنظیمی ساخته نشد و فهرست هر شرکت حدس زده نشد (مورد باز برای مالک).
- گردکردن: مراحل غیرآخر `MidpointRounding.AwayFromZero` به واحد کوچک ارز؛ آخرین مرحله باقیمانده واقعی (`FinalAmount − مجموع مراحل قبلی`) است؛ مجموع همیشه دقیقاً برابر مبلغ نهایی است. تغییر این سیاست یا قابل‌تنظیم‌کردن آن انجام نشد (تصمیم مالک).
- Overflow: سقف `MaxAmount = 1e15`، حداکثر ۴ رقم اعشار درصد و ۱۲ مرحله؛ ضرب `FinalAmount × Percent` حداکثر 10^17 است؛ با مقیاس مؤثر ≤ 7 ضریب بدون‌مقیاس آن حداکثر حدود 10^24 است که از 2^96 (حدود 7.9×10^28) کمتر است و دقیق می‌ماند. تست برای USD، JPY و KWD با بزرگ‌ترین مبلغ‌ها دارد.
- فرمول جمع ردیف‌ها و مبلغ نهایی پیش‌فاکتور (D-086) هنوز باز است و در این PR نیست.

## بررسی و آزمون
- Build کامل Release: ۰ خطا.
- اجرای کامل با SQL Server آزمایشی (پس از اصلاحات Review؛ 2026-11-18): ۱۶۰۴ اجراشده / ۱۵۹۴ موفق / ۰ ناموفق / ۱۰ ردشده (Windows CNG و chmod-root).
- بازبینی مستقل دوم (`code-reviewer`، فقط‌خواندنی، روی تغییرات اصلاحی): بدون یافتهٔ Critical/High؛ یک تست توتولوژیک بود (مبلغ‌های دستی جایگزین شد) و عبارت Overflow دقیق‌تر شد.
- `check_static_assets.py` موفق؛ `git diff --check` موفق.
- ClientApp: اجرا نشد (تغییر کلاینت ندارد).
- بازبینی مستقل فقط‌خواندنی (`pr-review-toolkit:code-reviewer`): ۲ یافتهٔ مهم؛ مرحلهٔ غیرآخر با مبلغ صفر پس از گرد‌کردن (اصلاح و تست اضافه شد) و تست بدیهیِ عدم بازتاب ورودی (با تست ساختاری جایگزین شد). بازبینی امنیتی/معماری جدا لازم نبود (بدون Schema، ورودی HTTP یا Tenant).

## امنیت، داده و سازگاری
بدون Schema، Migration، Backfill، TenantId/CompanyId، API، UI، I/O یا Log. بدون تماس با Vendor، Licensing و Provisioning.

## ریسک‌ها و کارهای باقی‌مانده
- اتصال به Proforma ماندگار پس از تصمیم Tenant/Company (S3-0h).
- در برش ذخیره‌سازی آینده باید تست رد «مبلغ ذخیره‌شدهٔ ناسازگار با مبلغ بازسازی‌شده» نوشته شود؛ تست فعلی بازیابی فقط رفت‌وبرگشت خروجی خود محاسبه‌گر را می‌سنجد.
- `issues` همان `List` داخلی است (قابل Cast)؛ اثر عملی ندارد و تغییر نکرد.
- تصمیم مالک دربارهٔ روش‌های مجاز و سیاست گرد‌کردن (اکنون AwayFromZero برای مراحل غیرآخر).
- ردیف WORK_LOG عمداً اضافه نشد تا با PR دیگر تداخل نکند.

## شناسه‌های Git
Commit و PR پس از ایجاد در توضیحات PR ثبت می‌شود.
