# تحویل وظیفه — ISSUE-802-rtl-ui-and-docs

- **Issue:** [#802](https://github.com/hamidreza1394/Accounting/issues/802) — بخش PR-5 از مجموعهٔ ۵ PR
- **Agent/مسئول:** GLM (Autonomous Engineering)
- **تاریخ:** 2026-11-28
- **وضعیت:** پیاده‌سازی رابط فارسی RTL و hardening مسیر Credential انجام شده؛ CI revision جاری در انتظار است. مرور بصری مرورگر روی Windows هنوز انجام نشده است.

## هدف و معیارهای پذیرش (این PR)
- [x] رابط کاربری فارسی RTL روی مسیر `/development/database-setup` برای جریان کامل Provisioning:
  - ورودی **کد نشست راه‌اندازی** (Challenge کنسولی) با توضیح اینکه کد فقط از کنسول فرآیند قابل دریافت است و با restart تغییر می‌کند.
  - دکمهٔ **بازرسی سرور و دیتابیس** → `POST /api/development/database-setup/inspect` و نمایش خلاصهٔ بازرسی (نسخهٔ سرور، وجود دیتابیس، اعتبار نشانهٔ مالکیت، تاریخچه/باقی‌ماندهٔ Migrationها، وجود Login Runtime، تعداد جدول‌ها).
  - دکمهٔ **راه‌اندازی کامل** → دریافت Bootstrap یک‌بارمصرف (`GET /provision-token`) سپس `POST /api/development/database-setup/provision` و رندر هر هفت مرحله با وضعیت و پیام فارسی.
  - فیلدهای اختیاری **نام کاربری و گذرواژهٔ Runtime** (خالی = ساخت خودکار نام/گذرواژهٔ کم‌اختیار).
  - پنل نتیجهٔ Runtime: نام هویت، گذرواژهٔ تولیدشده «فقط همین یک‌بار»، وضعیت ذخیره در Credential Manager.
  - حفظ آزمون اتصال و حذف اتصال Runtime ذخیره‌شده با Challenge؛ مسیر قدیمی ذخیرهٔ مستقیم اتصال مدیریتی بازنشسته شد تا رمز setup نتواند Credential کم‌اختیار Runtime را بازنویسی کند.
- [x] همهٔ متن‌ها فارسی و چیدمان `dir="rtl"`؛ بدون ایمیج و بدون فونت/کتابخانهٔ خارجی (CSP `default-src 'self'` حفظ شده).
- [x] مستندات: handoff حاضر + WORK_LOG + INDEX.
- [x] تست Regression HTTP برای بازنشستگی مسیر Save و تست JSDOM برای پاسخ دیرهنگام آزمون اتصال پس از شروع Provisioning اضافه شد؛ اجرای هر دو روی revision جاری در انتظار CI است.

## امنیت
- UI هیچ مسیر یا مقدار محرمانه‌ای در URL نمی‌گذارد؛ گذرواژه‌ها با `autocomplete="new-password"` و هرگز در localStorage ذخیره نمی‌شوند.
- Endpoint قدیمی `/save` با HTTP 410 بازنشسته شده است؛ فقط pipeline پس از ساخت و Verify هویت Runtime اجازهٔ نوشتن در Credential Manager را دارد.
- پس از موفقیت Provisioning، فیلدهای گذرواژهٔ setup و Runtime از DOM پاک می‌شوند.
- پاسخ‌های دیرهنگام تست/بازرسی با generation token بی‌اعتبار می‌شوند؛ شروع Provisioning همهٔ درخواست‌های خواندنی قبلی را بی‌اثر می‌کند.
- کد Challenge و Bootstrap Token فقط در حافظهٔ صفحه می‌مانند و در هر درخواست به‌صورت هدر فرستاده می‌شوند.
- نمایش گذرواژهٔ تولیدشده یک‌بار مصرف است و پس از رفرش صفحه دیگر قابل بازیابی نیست (عمداً).

## بررسی و آزمون
| آزمون | نتیجه واقعی |
|---|---|
| `dotnet build Vendor.sln -c Release` | موفق در revision قبلی؛ CI revision جاری پس از بازنشستگی Save باید دوباره تأیید شود |
| `dotnet test tests/Vendor.Host.Tests -c Release` | ماتریس HTTP جدید شامل HTTP 410 برای Save قدیمی اضافه شده؛ نتیجهٔ revision جاری در انتظار CI |
| `npm test` در Vendor.Web | تست JSDOM برای پاسخ دیرهنگام آزمون اتصال پس از شروع Provisioning اضافه شده؛ نتیجهٔ revision جاری در انتظار CI |
| مرورگر واقعی روی Windows | Not yet Verified — محیط مهندسی Linux است؛ گاردهای Endpoint با ماتریس HTTP واقعی در CI ویندوزی پوشش داده می‌شوند |

## شناسه‌های Git
- Branch: `feat/vendor-provisioning-rtl-ui`؛ PR #806 پیش‌تر در main ادغام شده و PR #807 اکنون مستقیماً به main هدف‌گذاری شده است.
- مستندات: handoff حاضر، WORK_LOG، INDEX

## تکمیل بازبینی CodeRabbit (2026-11-28)
- [x] **بازنشستگی ذخیرهٔ Credential مدیریتی:** دکمهٔ Save و مسیر persist مستقیم اتصال setup حذف/بازنشسته شدند؛ فقط pipeline می‌تواند Runtime کم‌اختیار را ذخیره کند.
- [x] **نگاشت نتیجهٔ Provisioning به وضعیت HTTP:** شکست pipeline اکنون 422 برمی‌گرداند (نه 200)؛ صفحه هم `body.success` را ملاک قرار می‌دهد و پنل Runtime فقط روی موفقیت واقعی نشان داده می‌شود.
- [x] **مدیریت خطای انتقال در بازرسی/راه‌اندازی:** شکست fetch پیام بازیابی نشان می‌دهد؛ برای Provisioning صراحتاً گفته می‌شود نتیجه نامشخص است و قبل از تلاش مجدد باید «بازرسی» اجرا شود (بدون retry خودکار).
- [x] **بازرسی ناموفق به‌عنوان خطا:** ماتریس HTTP با قرارداد 422 هم‌تراز شد (دو آزمون provision).

- [x] **Race پاسخ دیرهنگام:** generation token پاسخ آزمون/بازرسی قدیمی را پس از شروع Provisioning بی‌اثر می‌کند؛ تست JSDOM ترتیب رویداد را بازتولید می‌کند.
