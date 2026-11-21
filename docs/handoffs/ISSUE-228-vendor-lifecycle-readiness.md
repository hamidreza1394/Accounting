# تحویل وظیفه — ISSUE-228-vendor-lifecycle-readiness

- **Issue:** [#228 — Vendor onboarding and provisioning](https://github.com/hamidreza1394/Accounting/issues/228)
- **Agent/مسئول:** ChatGPT automation
- **تاریخ:** 2026-11-21
- **وضعیت:** اصلاح کد و تست رگرسیون ثبت شد؛ CI روی commit جدید در انتظار اجرا/تأیید است.

## هدف و معیارهای پذیرش
- نمایش وضعیت lifecycle فقط بر اساس stateهای واقعی ذخیره‌شده Vendor.
- عدم نمایش Ready برای نصب تعلیق‌شده، state ناشناخته، ACK در انتظار یا مراحل تحویل انجام‌نشده.
- تفکیک دقیق امضا، تحویل، ACK و Bootstrap.
- حفظ مرز Vendor-only؛ بدون تغییر Customer consumer، `Server/**` یا Migration دیتابیس مشتری.

## خلاصه تغییرات
- مدل snapshot readiness اکنون `DeliveredAtUtc` را از رکورد Provisioning می‌خواند.
- نگاشت stateها با state machine واقعی Vendor هم‌راستا شد: `AwaitingSigner`، `PendingDelivery`، `Confirmed` و `Failed`.
- `AlreadyApplied` مانند `Applied` موفق شناخته می‌شود؛ `Rejected` به `Failed` نگاشت می‌شود.
- نصب `Suspended` دیگر Ready گزارش نمی‌شود؛ وضعیت ناشناخته fail-closed است.
- پیام امضا با D-107 هماهنگ شد: Windows CNG با سطح SoftwareProtected مسیر نسخه اول است؛ HSM/KMS پیش‌نیاز اجباری signer مرکزی Vendor نیست.

## فایل‌های مهم تغییرکرده
- `Vendor/Vendor.Host/Authorization/VendorLifecycleReadinessRules.cs` — اصلاح state mapping.
- `Vendor/Vendor.Host/Authorization/VendorLifecycleReadinessEndpoints.cs` — خواندن `DeliveredAtUtc` از Vendor DB.
- `tests/Vendor.Host.Tests/VendorLifecycleReadinessRulesTests.cs` — تست نصب Active/Suspended/unknown، PendingDelivery قبل/بعد تحویل، AlreadyApplied، Failed و state ناشناخته.
- `docs/WORK_LOG.md` و `docs/INDEX.md` — ثبت تحویل و ارجاع handoff.

## تصمیم‌های فنی
- هیچ تصمیم معماری جدیدی گرفته نشد؛ فقط پیاده‌سازی با state machine موجود و D-107 هم‌راستا شد.
- هیچ Migration لازم نیست؛ `DeliveredAtUtc` از قبل در `VendorProvisioningRequest` وجود دارد.
- `AwaitingSigner` به‌تنهایی اثبات خرابی signer نیست؛ بنابراین Pending است، نه Blocked. شکست signer در این مسیر به‌صورت 503 برمی‌گردد و state را تغییر نمی‌دهد.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| Vendor CI روی HEAD قبلی PR #741 | موفق | Runهای #44792413863، #44792413787 و #44792414065؛ این شواهد مربوط به قبل از اصلاح‌اند |
| تست‌های جدید readiness | در انتظار CI | پس از push روی branch اختصاصی اجرا می‌شوند |
| اجرای محلی .NET | اجرا نشده | در این اجرا محیط local checkout/SDK در دسترس نبود |
| SQL Server endpoint integration | اجرا نشده | تست مستقل query/authorization endpoint هنوز لازم است |

## امنیت، داده و سازگاری
- فقط Vendor DB خوانده می‌شود؛ هیچ Customer DB یا secret در response وارد نمی‌شود.
- Query همچنان read-only و محدود به یک Customer است.
- هیچ schema، Migration، کلید امضا، credential یا تنظیم Production تغییر نکرد.

## ریسک‌ها و کارهای باقی‌مانده
- endpoint readiness هنوز تست HTTP/SQL Server اختصاصی برای query translation و resource authorization ندارد؛ این باید پیش از اعلام تکمیل کامل diagnostics بررسی شود.
- UI race و صفحه‌بندی جست‌وجوی مشتری در برش جداگانه قابل بررسی‌اند؛ در این commit تغییر نکرده‌اند.
- CI روی SHA جدید باید کامل و سبز شود.

## شناسه‌های Git
- Branch: `feat/vendor-lifecycle-readiness`
- Parent: `90d75362af12810f17ba98bd6dc39a776eb75c63`
- Commit: پس از ساخت Git object ثبت می‌شود.
- Pull Request: https://github.com/hamidreza1394/Accounting/pull/741


### اصلاح تکمیلی متن Entitlement
- مرحله Entitlement فقط `Status == Issued` را می‌شمارد و زمان اعتبار envelope/runtime را evaluate نمی‌کند؛ متن به «لایسنس صادرشده ثبت شده» تغییر کرد تا Ready به معنی runtime-active تلقی نشود.
- تست `IssuedLicenseStageDoesNotClaimRuntimeValidity` این مرز معنایی را تثبیت می‌کند.


## اصلاح route registration — 2026-11-21

- **یافتهٔ بازبینی:** extension جدید `MapVendorLifecycleReadinessEndpoints` در `Program.cs` فراخوانی نشده بود؛ بنابراین UI مسیر `GET /api/vendor/customers/{customerId}/readiness` را به fallback API می‌فرستاد و 404 می‌گرفت، هرچند unit/UI tests سبز بودند.
- **اصلاح حداقلی:** `app.MapVendorLifecycleReadinessEndpoints()` در composition root، کنار `MapVendorBusinessEndpoints()` ثبت شد.
- **Regression HTTP:** تست `VendorLifecycleReadinessRouteTests.ReadinessRouteIsMappedAndRequiresAuthentication` با `WebApplicationFactory<Program>` درخواست anonymous می‌فرستد و 401 را الزام می‌کند؛ اگر route دوباره از composition root حذف شود، fallback پاسخ 404 می‌دهد و تست شکست می‌خورد. Hosted serviceها در این تست حذف می‌شوند تا آزمون route به DB یا signer عملیاتی وابسته نباشد.
- **محدودیت پوشش:** این regression فقط registration و challenge احراز هویت را اثبات می‌کند؛ query translation روی SQL Server و resource-level 403/404 برای مشتری مجاز/غیرمجاز هنوز نیازمند integration test جداگانه است.
- **اعتبارسنجی:** کد روی branch به‌روزرسانی شد؛ اجرای محلی انجام نشده و CI روی SHA جدید باید کامل شود.


### تکمیل پوشش HTTP/SQL Server — 2026-11-21

- تست موجود `VendorCustomerMutationHttpIntegrationTests` اکنون پس از ورود MFA و ایجاد Customer، مسیر readiness را از طریق همان Host واقعی و SQL Server موقت فراخوانی می‌کند.
- Assertions: پاسخ 200 برای Customer موجود و مجاز، تطبیق CustomerId، مرحلهٔ `customerRegistration=Ready` و `payment=NotApplicable` در نبود سفارش.
- این تست query translation و projectionهای EF Core برای سفارش/پرداخت/لایسنس/نصب/Provisioning/Approval/Amendment را در SQL Server واقعی CI اجرا می‌کند.
- قرارداد resource authorization فعلی برای شناسه ناشناخته قبل از handler به 403 می‌رسد؛ این باگ 404 نیست و تست‌ها باید همین رفتار را حفظ کنند. پوشش منفی اختصاصی 403 در این برش هنوز اضافه نشده است.


- **Resource-boundary negative test:** همان integration test اکنون برای CustomerId تصادفی/ناشناخته نیز پاسخ 403 را assert می‌کند؛ مطابق evaluator فعلی، وجود Customer قبل از اجرای handler بررسی می‌شود و شناسهٔ ناشناخته نباید به پاسخ 404 قابل‌استفاده برای enumeration تبدیل شود.


## اصلاح دامنه پرداخت در سفارش‌های متعدد — 2026-11-21

- **یافتهٔ بازبینی:** query قبلی تعداد پرداخت‌های تأییدشده/ثبت‌شده/ردشده را روی همه سفارش‌های Customer جمع می‌کرد، اما `ReferenceId` مرحله سفارش به آخرین Order اشاره داشت. در نتیجه پرداخت قدیمی می‌توانست سفارش جدیدِ بدون پرداخت را Ready نشان دهد.
- **اصلاح حداقلی:** query اکنون فقط آخرین Order (با ترتیب قطعی `CreatedAtUtc DESC, Id ASC`) و پرداخت‌های همان Order را می‌خواند. ReferenceId مرحله Payment نیز شماره همان سفارش است.
- **Regression SQL Server:** یک Customer با دو سفارش ساخته می‌شود؛ سفارش قدیمی پرداخت Verified دارد و سفارش جدید بدون پرداخت است. انتظار: سفارش جدید مرجع commercialOrder و payment باشد و Payment آن Pending بماند. این تست رفتار تجمیع اشتباه customer-wide را رد می‌کند.
- **مرز معنایی:** Ready در مرحله Payment یعنی حداقل یک پرداخت برای آخرین سفارش تأیید شده؛ این به‌تنهایی ادعای تسویه کامل مانده نیست و Amendment همچنان مستقل از پرداخت است.


- **بازبینی projection پس از اصلاح query:** نام فیلدهای anonymous projection با snapshot (`LatestOrderNumber` و `LatestOrderAtUtc`) هم‌نام نگه داشته شد تا mapping compile-time دقیق بماند؛ CI روی HEAD جدید باید build و SQL regression را تأیید کند.
