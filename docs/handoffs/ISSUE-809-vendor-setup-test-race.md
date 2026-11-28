# Issue #809 — جلوگیری از فعال‌شدن مجدد Save با پاسخ دیررس آزمون اتصال

**وضعیت:** پیاده‌سازی در PR #810؛ منتظر CI و بازبینی نهایی.  
**Issue:** https://github.com/hamidreza1394/Accounting/issues/809  
**PR:** https://github.com/hamidreza1394/Accounting/pull/810  
**مبنای شاخه:** `main@569622a534fc91cc3d4160e0b26aa8240a7dca80`

## علت ریشه‌ای

در PR #807، هنگام شروع Provisioning مقدار تأیید قبلی Save پاک می‌شد؛ اما درخواست `/api/development/database-setup/test` که پیش‌تر آغاز شده بود می‌توانست پس از شروع Provisioning با پاسخ موفق برگردد و دوباره وضعیت Save را معتبر کند. چون Save و Provisioning از یک Credential Target در Windows Credential Manager استفاده می‌کنند، این ترتیب می‌توانست اتصال setup/admin را پس از Provisioning روی اتصال Runtime کم‌اختیار بنویسد.

همچنین نتیجهٔ آزمونی که با ورودی قدیمی آغاز شده بود، در صورت تغییر فرم هنگام انتظار، نباید برای ورودی جدید معتبر تلقی شود. عملیات Save، Delete و Provisioning نیز نباید به‌صورت هم‌زمان روی همان Credential Target اجرا شوند.

## اصلاح پیاده‌سازی‌شده در PR #810

- منطق وضعیت صفحه به `setup-state.mjs` خالص و مستقل منتقل شد.
- هر آزمون اتصال یک generation token و signature از ورودی همان لحظه دریافت می‌کند.
- تغییر ورودی، شروع Provisioning، Save یا Delete نسل قبلی را باطل می‌کند؛ پاسخ دیررس با نتیجهٔ `stale` نادیده گرفته می‌شود.
- فقط پاسخ موفق برای همان signature جاری می‌تواند Save را فعال کند.
- Save، Delete و Provisioning از طریق state machine سریالی شده‌اند و هم‌پوشانی credential mutation رد می‌شود.
- در طول عملیات تغییردهنده، کنترل‌های فرم غیرفعال می‌شوند.
- ماژول ES به‌صورت Embedded Resource از همان Host و با مسیر هم‌مبدأ ارائه می‌شود؛ CSP و guardهای loopback/setup موجود حفظ شده‌اند.
- تست Exposure مسیرهای CSS، JS و ماژول state را در حالت غیرفعال/غیرمجاز کنترل می‌کند.
- ماتریس HTTP ویندوزی، پاسخ 200 و Content-Type ماژول Embedded را بررسی می‌کند.

## Regression و شواهد

شش تست Node برای موارد زیر اضافه شده‌اند:

1. پاسخ موفق دیررس پس از شروع Provisioning نتواند Save را فعال کند.
2. تغییر ورودی حین درخواست، نتیجهٔ قبلی را بی‌اعتبار کند.
3. Save فقط برای signature دقیقاً آزموده‌شده مجاز باشد.
4. Save و Provisioning هم‌زمان نشوند.
5. Delete و Provisioning هم‌زمان نشوند.
6. آزمون شکست‌خورده یا signature ناسازگار Save را فعال نکند.

اجرای محلی ثبت‌شده: Node v22.16.0، شش تست، شش موفق، صفر شکست و صفر Skip.

Vendor CI باید روی Node 24 syntax check فایل‌های ماژول و شش تست state را اجرا کند. ماتریس HTTP ویندوزی نیز route جدید Embedded را بررسی می‌کند.

## وضعیت تأییدنشده

در زمان ایجاد این handoff، PR #810 باز است. Build .NET، Vendor CI، ماتریس HTTP ویندوزی، SQL Integration و بررسی نهایی PR هنوز روی HEAD نهایی موفق اعلام نشده‌اند. اجرای مرورگر واقعی و اجرای عملیاتی روی Windows Credential Manager/SQL Server سیستم مالک انجام نشده است.

## مرز تغییر

تغییرات محدود به Vendor Development Setup، تست‌های مربوط و Vendor CI هستند. هیچ `Server/**`، Customer consumer، Customer schema/Migration، کلید خصوصی، رمز، hash رمز یا Connection String واقعی تغییر/ثبت نشده است.
