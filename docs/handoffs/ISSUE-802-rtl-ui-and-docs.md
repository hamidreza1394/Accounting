# تحویل وظیفه — ISSUE-802-rtl-ui-and-docs

- **Issue:** [#802](https://github.com/hamidreza1394/Accounting/issues/802) — بخش PR-5 از مجموعهٔ ۵ PR
- **Agent/مسئول:** GLM (Autonomous Engineering)
- **تاریخ:** 2026-11-28
- **وضعیت:** انجام‌شده (رابط فارسی RTL کامل راه‌اندازی + مستندات نهایی)؛ مرور بصری مرورگر روی Windows: در زمان ثبت انجام نشده است — در Vendor CI ماتریس HTTP واقعی سطح setup اجرا می‌شود.

## هدف و معیارهای پذیرش (این PR)
- [x] رابط کاربری فارسی RTL روی مسیر `/development/database-setup` برای جریان کامل Provisioning:
  - ورودی **کد نشست راه‌اندازی** (Challenge کنسولی) با توضیح اینکه کد فقط از کنسول فرآیند قابل دریافت است و با restart تغییر می‌کند.
  - دکمهٔ **بازرسی سرور و دیتابیس** → `POST /api/development/database-setup/inspect` و نمایش خلاصهٔ بازرسی (نسخهٔ سرور، وجود دیتابیس، اعتبار نشانهٔ مالکیت، تاریخچه/باقی‌ماندهٔ Migrationها، وجود Login Runtime، تعداد جدول‌ها).
  - دکمهٔ **راه‌اندازی کامل** → دریافت Bootstrap یک‌بارمصرف (`GET /provision-token`) سپس `POST /api/development/database-setup/provision` و رندر هر هفت مرحله با وضعیت و پیام فارسی.
  - فیلدهای اختیاری **نام کاربری و گذرواژهٔ Runtime** (خالی = ساخت خودکار نام/گذرواژهٔ کم‌اختیار).
  - پنل نتیجهٔ Runtime: نام هویت، گذرواژهٔ تولیدشده «فقط همین یک‌بار»، وضعیت ذخیره در Credential Manager.
  - حفظ جریان‌های قبلی (آزمون اتصال/ذخیره/حذف) با افزودن هدر Challenge به همهٔ عملیات تغییردهنده.
- [x] همهٔ متن‌ها فارسی و چیدمان `dir="rtl"`؛ بدون ایمیج و بدون فونت/کتابخانهٔ خارجی (CSP `default-src 'self'` حفظ شده).
- [x] مستندات: handoff حاضر + WORK_LOG + INDEX.
- [x] تست: ماتریس HTTP واقعی PR-4 (`VendorDevelopmentSetupHttpMatrixTests`) جریان‌های Endpoint را پوشش می‌دهد؛ UI تغییرات رفتاری سمت سرور ندارد و فقط مصرف‌کننده همان قراردادهاست.

## امنیت
- UI هیچ مسیر یا مقادیر محرمانه‌ای در URL نمی‌گذارد؛ گذرواژه‌ها با `autocomplete="new-password"` و هرگز در localStorage.
- کد Challenge و Bootstrap Token فقط در حافظهٔ صفحه می‌مانند و در هر درخواست به‌صورت هدر فرستاده می‌شوند.
- نمایش گذرواژهٔ تولیدشده یک‌بار مصرف است و پس از رفرش صفحه دیگر قابل بازیابی نیست (عمداً).

## بررسی و آزمون
| آزمون | نتیجه واقعی |
|---|---|
| `dotnet build Vendor.sln -c Release` | موفق — منابع Embedded بدون تغییر نام LogicalName |
| `dotnet test tests/Vendor.Host.Tests -c Release` | ماتریس HTTP و واحد setup سبز؛ baseline محیطی بدون تغییر |
| مرورگر واقعی روی Windows | Not yet Verified — سند محدودیت: سندباکس مهندسی Linux است؛ گاردهای سطح Endpoint با ماتریس HTTP واقعی در CI ویندوزی پوشش داده شده‌اند |

## شناسه‌های Git
- Branch: `feat/vendor-provisioning-rtl-ui` (stacked روی `fix/vendor-provisioning-endpoint-security`؛ پس از Merge PR-4 به main retarget می‌شود)
- مستندات: handoff حاضر، WORK_LOG، INDEX

## تکمیل بازبینی CodeRabbit (2026-11-28)
- [x] **پاک‌سازی تأیید ذخیره هنگام راه‌اندازی:** بعد از Provisioning، تأیید قبلی «ذخیرهٔ اتصال» پاک می‌شود تا Save فعال نماند و Credential Manager با Credentialهای ادمین بازنویسی نشود.
- [x] **نگاشت نتیجهٔ Provisioning به وضعیت HTTP:** شکست pipeline اکنون 422 برمی‌گرداند (نه 200)؛ صفحه هم `body.success` را ملاک قرار می‌دهد و پنل Runtime فقط روی موفقیت واقعی نشان داده می‌شود.
- [x] **مدیریت خطای انتقال در بازرسی/راه‌اندازی:** شکست fetch پیام بازیابی نشان می‌دهد؛ برای Provisioning صراحتاً گفته می‌شود نتیجه نامشخص است و قبل از تلاش مجدد باید «بازرسی» اجرا شود (بدون retry خودکار).
- [x] **بازرسی ناموفق به‌عنوان خطا:** ماتریس HTTP با قرارداد 422 هم‌تراز شد (دو آزمون provision).
