# تحویل وظیفه — ISSUE-710-trade-payment-plan-validation

- Issue مرتبط: #710 (اختصاصی این برش؛ باز تا زمان تکمیل PR). #397 فقط Issue چتر ماژول Trade است و بسته نمی‌شود.
- شاخه: `feat/trade-payment-plan-validation`

## هدف و معیارهای پذیرش
اعتبارسنجی و تقسیم دقیق «طرح پرداخت» پیش‌فاکتور (مراحل پرداخت با روش و درصد/مبلغ) به‌صورت منطق خالص و بدون Schema.
- مجموع درصدها دقیقاً ۱۰۰ یا مجموع مبالغ دقیقاً برابر مبلغ نهایی.
- محاسبات فقط با `decimal` انجام می‌شوند. مراحل غیرآخر با `MidpointRounding.AwayFromZero` تا تعداد اعشار ارز مرجع (`CurrencyRef.MinorUnits`) گرد می‌شوند؛ تست صریح `0.025 → 0.03` در USD وجود دارد. آخرین مرحله باقیماندهٔ واقعی `FinalAmount − مجموع مراحل قبلی` را می‌گیرد؛ اگر صفر یا منفی باشد اعتبارسنجی رد می‌شود.
- خطاها فقط کد enum و شمارهٔ مرحله‌اند و ورودی را بازتاب نمی‌دهند.

## خلاصه تغییرات
- `PaymentMethodRef` و فهرست مرجع `TradeReferenceData.PaymentMethods` (TT، LC، DP، DA، OPEN_ACCOUNT) و `TryFindPaymentMethod`.
- `Trade.Application/Proformas`: `PaymentPlanCalculator.TryBuild`، `PaymentPlan`، `PaymentPlanStage`، `PaymentPlanStageInput`، `PaymentPlanIssue`، `PaymentPlanIssueCode`.
- سقف‌های فنی: حداکثر ۱۲ مرحله، دقت درصد ۴ رقم، `MaxAmount = 1e15`.
- حالت ترکیبی درصد و مبلغ، مرحلهٔ بدون مقدار یا با هر دو مقدار، و مرحلهٔ غیرآخر که پس از گرد‌کردن صفر شود رد می‌شوند.
- `PaymentPlan` و `PaymentPlanStage` سازندهٔ عمومی و `init` ندارند (سازنده `internal`، property فقط‌خواندنی)؛ طرح نامعتبر دستی ساخته نمی‌شود و با `with` تغییر نمی‌کند. تنها تولیدکننده `PaymentPlanCalculator.TryBuild` است. بازیابی طرح ذخیره‌شده در آینده با فراخوانی دوبارهٔ `TryBuild` روی ارز، مبلغ نهایی و ورودی مراحل (درصد یا مبلغ) انجام می‌شود و خواننده باید مبالغ ذخیره‌شده را با مبالغ بازسازی‌شده مقایسه کند؛ نوع Restore جدا ساخته نشد چون هنوز ذخیره‌ای وجود ندارد.
- ورودی `stages` (و فهرست `allowedMethodCodes`) در آغاز `TryBuild` دقیقاً یک بار در کپی خصوصی خوانده می‌شود و اعتبارسنجی و تخصیص فقط روی همان کپی انجام می‌شود؛ تغییر فهرست فراخواننده (حتی هم‌زمان) نتیجه را عوض نمی‌کند. شمارش `Count` یک بار و هر `stages[i]` یک بار خوانده می‌شود (محدودیت: اگر فراخواننده فهرست را حین کپی کوچک کند، indexer استثنا می‌دهد؛ این رد ایمن است نه نتیجهٔ نادرست).
- `PaymentPlan.Stages` اکنون `ImmutableArray<PaymentPlanStage>` است (از `System.Collections.Immutable` چارچوب؛ بدون NuGet جدید)؛ پیش‌تر `ReadOnlyCollection` روی آرایه بود و از مسیر `ICollection.SyncRoot` آرایهٔ قابل‌نوشتن برمی‌گرداند. نوع عمومی `Stages` از `IReadOnlyList` به `ImmutableArray` تغییر کرد.
- `TradeReferenceData.Build` اکنون `ImmutableArray<T>` برمی‌گرداند. `PaymentMethods` و ایندکس `TryFindPaymentMethod` از همان فهرست ساخته می‌شوند و راه تغییر (Cast، `SyncRoot`، آرایه) ندارند. چون `Build` مشترک است، فهرست‌های قدیمی `Countries`، `Currencies`، `Units` و `Incoterms` در `main` هم که همان ایراد را داشتند تغییرناپذیر شدند (نوع عمومی `IReadOnlyList` ثابت ماند؛ تست‌های قبلی بدون تغییر سبزند).
- `allowedMethodCodes == null` یعنی همهٔ روش‌های مرجع مجازند؛ این رفتار با تأیید مالک تثبیت شد. تنظیم محدودیت روش‌ها برای هر Company نیازمند Schema است و به S3 و پروفایل D-086 موکول می‌شود؛ در این PR هیچ جدول یا تنظیم شرکتی ساخته نمی‌شود.
- ۴۴ تست در `tests/Template.Tests/TradePaymentPlanTests.cs`.

## فایل‌های مهم تغییرکرده
- `docs/WORK_LOG.md` — ورودی این PR (append)
- `Modules/Trade/Trade.Application/Proformas/PaymentPlan.cs`، `PaymentPlanCalculator.cs` (جدید)
- `Modules/Trade/Trade.Application/ReferenceData/ReferenceRecords.cs`، `MiscData.cs`، `TradeReferenceData.cs`
- `tests/Template.Tests/TradePaymentPlanTests.cs` (جدید)

## تصمیم‌های فنی
- فقط منطق خالص است؛ ذخیرهٔ ماندگار طرح پرداخت (جدول Proforma) به تصمیم Tenant/Company و ADRهای D-086/D-090 وابسته و Blocked است.
- روش‌های مجاز پارامتر ورودی کنترل‌شده است: `null` یعنی همهٔ روش‌های فهرست مرجع، فهرست خالی یعنی هیچ روشی مجاز نیست، و هر روش خارج از فهرست مرجع همیشه رد می‌شود. منبع واقعی «روش‌های مجاز هر Company» هنوز تصمیم ندارد؛ جدول یا تنظیمی ساخته نشد و فهرست هر شرکت حدس زده نشد (مورد باز برای مالک).
- **تصمیم تأییدشدهٔ مالک:** محاسبات `decimal`؛ مراحل غیرآخر `MidpointRounding.AwayFromZero` به `CurrencyRef.MinorUnits`؛ مثال آزمون‌شده `0.05 × 50% = 0.025 → 0.03`؛ آخرین مرحله باقیماندهٔ واقعی (`FinalAmount − مجموع مراحل قبلی`) را می‌گیرد و باقیماندهٔ صفر/منفی با `ResidualStageNotPositive` رد می‌شود.
- Overflow: سقف `MaxAmount = 1e15`، حداکثر ۴ رقم اعشار درصد و ۱۲ مرحله؛ ضرب `FinalAmount × Percent` حداکثر 10^17 است؛ با مقیاس مؤثر ≤ 7 ضریب بدون‌مقیاس آن حداکثر حدود 10^24 است که از 2^96 (حدود 7.9×10^28) کمتر است و دقیق می‌ماند. تست برای USD، JPY و KWD با بزرگ‌ترین مبلغ‌ها دارد.
- **فرمول مبلغ نهایی پیش‌فاکتور (D-086) همچنان باز است** و عمداً در این PR تعیین یا فرض نمی‌شود.

## بررسی و آزمون
- Build کامل Release: ۰ خطا.
- اجرای کامل با SQL Server آزمایشی (پس از اصلاحات تغییرناپذیری و merge با main؛ 2026-11-18): ۱۶۰۸ اجراشده / ۱۵۹۸ موفق / ۰ ناموفق / ۱۰ ردشده (Windows CNG و chmod-root).
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
- تنظیم محدودیت روش‌های پرداخت برای هر Company خارج از این برش است و به S3 و پروفایل D-086 موکول شده است.
- ورودی WORK_LOG در commit جدا افزوده شد (append-only؛ با ورودی PR #704 در انتهای فایل ممکن است تداخل متنی بگیرد و با حفظ هر دو حل می‌شود).
- اتصال آینده به Application باید `allowedMethodCodes` را مطابق سیاست مرحله‌ای S3/D-086 ارسال کند؛ تا پیش از آن `null` عمداً همهٔ روش‌های مرجع را مجاز می‌داند.

## شناسه‌های Git
Commit و PR پس از ایجاد در توضیحات PR ثبت می‌شود.
