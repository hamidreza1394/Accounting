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

## اصلاح تکمیلی — قفل Inspect و نتیجهٔ بازرسی کهنه (رفع یافتهٔ CodeRabbit، 2026-11-28)

بازبینی CodeRabbit در PR #810 یک نقص هم‌زمانی واقعی را گزارش کرد و بررسی کد آن را تأیید کرد:

- **علت:** `isBusy()` فقط Mutation ها را می‌دید و هیچ وضعیتی برای Inspection وجود نداشت. در نتیجه تغییر فرم در زمان اجرای `POST /inspect` دکمهٔ Inspect را دوباره فعال می‌کرد و Handler دوم بدون Guard شروع می‌شد؛ پاسخ دیررس درخواست اول می‌توانست نتیجهٔ بازرسی دوم را بازنویسی کند و پاسخ متعلق به ورودی‌های قدیمی نیز بدون کنترل نسخه رندر می‌شد. همچنین `beginSave/beginDelete/beginProvisioning` از Inspection بی‌خبر بودند و Mutation می‌توانست هم‌زمان با یک بازرسی در جریان آغاز شود.
- **اصلاح:** وضعیت `inspectionInFlight` با الگوی موجود token/generation به State Machine اضافه شد: `beginInspection()` فقط وقتی هیچ Test/Mutation/Inspection دیگری در جریان نباشد توکن frozen صادر می‌کند؛ `completeInspection(token, succeeded)` پاسخ کهنه را `stale` می‌کند و قفل را آزاد می‌کند؛ `endInspection()` در `finally` هر مسیر خطا/قطع ارتباط قفل را بدون شرط آزاد می‌کند. `isBusy()` حالا Inspection را هم می‌بیند تا در `syncActionButtons()` دکمهٔ Inspect در تمام مدت درخواست غیرفعال بماند و هیچ Mutation/Test هم‌زمان با بازرسی آغاز نشود؛ Guard خود Handler هم مستقلاً درخواست هم‌زمان را رد می‌کند. تغییر فرم در طول Inspection فقط اعتبار نتیجه (generation) را باطل می‌کند و قفل درخواست را نمی‌شکند.
- **رفع خطای CI:** تست Exposure مسیرهای asset را با انتظار 404 نوشته بود؛ اما مسیرهای فایل‌گونه وقتی Setup surface map نشده باشد هیچ Endpoint ای match نمی‌کنند (`MapFallback` فقط `nonfile` را می‌گیرد) و FallbackPolicy کلی با چالش 401 رد می‌کند. انتظار تست به «401 یا 404 = fail-closed، هرگز ناشناس» اصلاح شد؛ رفتار برنامه تغییر نکرد و این رفتار از قبل روی main هم وجود داشت.

هشت تست Node جدید برای قفل Inspection اضافه شدند (مجموع ۱۴ تست): شروع موفق، رد درخواست دوم، بی‌اثر بودن تغییر فرم روی قفل، عدم شروع Inspection حین Save/Delete/Provisioning و برعکس، امکان Inspect مجدد پس از موفقیت، آزادسازی قفل پس از خطا/Exception و در `finally`، بی‌اثر شدن پاسخ کهنه روی نتیجهٔ جدید و انحصار متقابل Test/Inspection.

اجرای محلی ثبت‌شده پس از اصلاح: Node v24.22.3، چهارده تست، چهارده موفق، صفر شکست و صفر Skip. تست‌های Exposure روی Linux با Connection String آزمایشی: ۲/۲ موفق.

## وضعیت تأییدنشده

در زمان ایجاد این handoff، PR #810 باز است. Build .NET، Vendor CI، ماتریس HTTP ویندوزی، SQL Integration و بررسی نهایی PR هنوز روی HEAD نهایی موفق اعلام نشده‌اند. اجرای مرورگر واقعی و اجرای عملیاتی روی Windows Credential Manager/SQL Server سیستم مالک انجام نشده است.

## مرز تغییر

تغییرات محدود به Vendor Development Setup، تست‌های مربوط و Vendor CI هستند. هیچ `Server/**`، Customer consumer، Customer schema/Migration، کلید خصوصی، رمز، hash رمز یا Connection String واقعی تغییر/ثبت نشده است.
