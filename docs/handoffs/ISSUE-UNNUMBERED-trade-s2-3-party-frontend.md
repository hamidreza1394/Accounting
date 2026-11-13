# تحویل وظیفه — ISSUE-UNNUMBERED-trade-s2-3-party-frontend

- **Issue:** UNNUMBERED (S2-3 در `docs/TRADE_PHASE0_DRAFTS.md`؛ فقط بخش فرانت‌اند؛ بک‌اند: `ISSUE-UNNUMBERED-trade-s2-3-party-backend.md`)
- **Agent/مسئول:** frontend-developer (Claude Code)
- **تاریخ:** 2026-11-13
- **وضعیت:** `Implemented`. **`Verified` نیست**: تست‌های واحد/یکپارچگی در jsdom اجرا شد، ولی مرورگر واقعی و SQL Server اجرا نشد (بخش «اجرا نشد»).

## هدف و معیارهای پذیرش
- [x] ماژول واقعی طرف‌حساب در `ClientApp/src/features/trade`: `/trade/parties`، `/new`، `/:id`، `/:id/edit` (فارسی، RTL، اجزای مشترک موجود، الگوی HR)
- [x] حالت‌های بارگذاری/خطا/خالی؛ حفظ داده فرم هنگام خطای سرور؛ برچسب/aria/focus/صفحه‌کلید
- [x] `SearchableCombobox` مشترک (D-082) زیر `components/ui` و استفادهٔ آن برای کشور (۲۴۹ گزینه)
- [x] hint مجوز فقط از `GET /trade/capabilities` (fail-closed)؛ نگهبان Route؛ دکمه‌ها فقط با مجوز
- [x] آیتم منوی «طرف‌حساب‌ها» در گروه «بازرگانی خارجی»؛ placeholder «واردات و صادرات» حفظ شد
- [x] بدون حذف/فعال/غیرفعال‌سازی (D-083 `Proposed`)؛ `isActive` فقط نشان فقط‌خواندنی
- [ ] مرورگر واقعی / SQL Server — اجرا نشد

## خلاصه تغییرات
- **API کلاینت:** `tradeClient.ts` (خواندن با GET هم‌مبدأ/no-store؛ نوشتن با `sendJson` مشترک ⇒ هدر Anti-forgery مثل HR) و `tradeTypes.ts` (پارسرهای سخت‌گیر؛ نقش‌ها همان نام‌های enum `PartyRole`). شناسهٔ شرکت/Tenant هرگز ارسال نمی‌شود.
- **مجوز:** `access/TradeAccess.tsx` (`TradeAccessProvider` در `App.tsx` با کلید `company:period`؛ `useTradeAccess`)؛ وضعیت‌ها loading/ready/unavailable/error؛ `can()` برای hintهای نوشتنی دورهٔ `ReadWrite` هم می‌خواهد؛ `has()` برای تمایز «فقط‌خواندنی» از «بدون مجوز». نگهبان Route: `TradeRouteGuard` (`view` ← فهرست/جزئیات، `create` ← `/new`، `update` ← `/edit`).
- **خطاها:** `lib/tradeErrors.ts`: ۴۰۰ ← خطای فیلد (کلید camelCase)؛ ۴۰۹ بر اساس `detail` ← تکراری‌بودن کد (زیر فیلد کد)، تعارض هم‌زمانی (بدون unmount فرم: «دریافت نسخهٔ تازه و حفظ ویرایش‌های من» فقط rowVersion را به‌روز می‌کند و پیام می‌دهد ذخیره، تغییر کاربر دیگر را بازنویسی می‌کند؛ «نمایش مقادیر تازه» با ConfirmDialog؛ خطای خودِ دریافت فقط Alert درون‌صفحه‌ای است)، دورهٔ فقط‌خواندنی، نیاز به زمینهٔ کاری؛ ۴۰۳ `trade_module_unavailable` ← وضعیت دوستانهٔ «ماژول بازرگانی در دسترس نیست»؛ `trade_forbidden` ← «دسترسی ندارید»؛ ۴۰۱ ← مدیریت نشست موجود.
- **فرم مشترک** `components/PartyEditor.tsx`: بخش‌های هویت/نقش‌ها/کشور، ستارهٔ الزامی، اعتبارسنجی آینهٔ سرور (`partyValidation.ts`)، چرخهٔ `useFormGuard`، خروج با `ConfirmDialog`، غیرفعال‌شدن ارسال/فیلدها هنگام ذخیره، فوکوس روی اولین فیلد نامعتبر، پس از موفقیت ← جزئیات با پیام یک‌بارمصرف درون‌حافظه (`partyFlash.ts`) و بروزرسانی فهرست (`partyListRefresh.ts`)؛ تب فرم در Workspace بسته می‌شود (`useLeaveTo`، همان الگوی فرم کاربران).
- **فهرست:** جست‌وجوی debounce (۳۵۰ms) روی کد/نام، فیلتر نقش و وضعیت، صفحه‌بندی سمت‌سرور با انتخاب اندازهٔ صفحه (بدون مرتب‌سازی کلاینتی چون سرور بر اساس کد مرتب می‌کند)، ردیف ← جزئیات، نام کشور از فهرست مرجع (در خطا فقط کد).
- **منو:** `AppSidebar.tsx`: برگ «طرف‌حساب‌ها» ← `/trade/parties` فقط با `status=ready && can('party.view')`؛ برگ «واردات و صادرات» placeholder صادق ماند. هیچ گروهی `openByDefault` ندارد.
- **`api/client.ts` (تغییر افزایشی مشترک):** `ApiError.problemCode` از `detail` فقط وقتی شکل کد دارد (`^[a-z][a-z0-9_]{2,63}$`) و فقط برای ۴xx؛ متن آزاد `detail` هرگز نمایش داده نمی‌شود (قاعدهٔ قبلی حفظ شد).

## فایل‌های تغییرکرده
جدید: `ClientApp/src/components/ui/SearchableCombobox{.tsx,.test.tsx}`، `ClientApp/src/features/trade/**` (api، lib، access، components، pages، test، `tradeRoutes.tsx` و تست‌ها)، `ClientApp/src/TradeIntegration.test.tsx`.
تغییر: `ClientApp/src/App.tsx`، `components/AppRoutes.tsx`، `components/shell/AppSidebar.tsx`، `components/shell/AppSidebar.test.tsx`، `components/shell/menuRouteConsistency.test.tsx`، `api/client.ts`، `api/client.test.ts`، `docs/MENU_FORM_CATALOG.md`، `docs/WORK_LOG.md`.
بدون تغییر: هر فایل بک‌اند، `UserPicker`/`CompanyPicker`، فایل‌های HR.

## تصمیم‌ها و انحراف‌ها
1. **`SearchableCombobox` خانگی (WAI-ARIA 1.2)** روی `<input role=combobox>` + `listbox`، بدون Base UI Combobox و بدون وابستگی جدید: رفتار جست‌وجوی فارسی/لاتین (نرمال‌سازی ي/ك/ارقام)، نمایش «کد — نام»، منبع محلی و `loadOptions` ناهمگام (debounce، لغو، خطا با تلاش مجدد). `LookupDialog` و `QuickCreate` ساخته نشد (مصرف‌کننده‌ای در S2-3 نیست).
2. **hint مجوز Trade در `Route.permission` نیست**: نوع `Permission` و `can()` فقط از snapshot `/me/permissions` می‌آیند و کد Trade ندارند؛ به‌جای آلوده‌کردن آن‌ها، نگهبان اختصاصی فیچر استفاده شد.
3. **Provider در `App.tsx` است نه `AppShell`** تا تست‌های موجود `AppShell` که پاسخ‌های fetch را ترتیبی می‌دهند (مثل `MyAccountPanel.test.tsx`) نشکنند و درخواست اضافی در آن‌ها نباشد.
4. **تغییر `ApiError`** (بخش بالا) لازم بود چون ۴۰۹ها فقط با `detail` از هم جدا می‌شوند. افزایشی و تست‌شده است.
5. **حالت فقط‌خواندنی دوره**: دکمه‌ها/مسیرهای ایجاد و ویرایش وقتی `accessMode=ReadOnly` است پنهان/مسدود می‌شوند (سرور هم ۴۰۹ `trade_period_read_only` می‌دهد).
6. `PartyEditor` از `features/hr/components/HrFormExit` (`useFormExit`) استفاده می‌کند. بهتر است بعداً به کد مشترک منتقل شود؛ در این تحویل فایل HR تغییر نکرد.
7. فهرست/فیلترها در state کامپوننت‌اند (مثل HR) و در URL نیستند؛ تب Workspace فهرست را زنده نگه می‌دارد. دلیل: جست‌وجو هرگز در URL نرود.
8. شناسهٔ حقوقی فقط در جزئیات و فرم‌ها نمایش داده می‌شود؛ هرگز در URL/Query/log/متن خطا (تست‌شده: فهرست، URL پس از ثبت، پیام‌ها).

## بررسی و آزمون (نتایج واقعی از `ClientApp`)
| فرمان | نتیجه |
|---|---|
| `npm ci` | اجرا نشد: `node_modules` موجود بود و وابستگی/lock تغییر نکرد |
| `npm run typecheck` | موفق (۰ خطا) |
| `npm test` (کل سوییت) | ۱۱۷۰ تست: **۱۱۶۷ موفق، ۳ ناموفق**؛ ۱۰۵ فایل: ۱۰۳ موفق، ۲ ناموفق |
| `npm run build` | موفق؛ `index` ۳۷۰٫۸۱ kB (gzip ‏۱۱۰٫۸۱)؛ اندازهٔ **پیش از تغییر اندازه‌گیری نشد**؛ chunkهای Trade: `PartiesPage` ۵٫۲ kB، `PartyDetailPage` ۵٫۲ kB، `PartyEditPage` ۲٫۸ kB، `PartyCreatePage` ۱٫۳ kB (فرم مشترک و combobox در chunk مشترک ‏۱۴٫۸ kB) |
| `npm run check:offline` | موفق: ۷۶ منبع محلی، هیچ منبع بیرونی |
| `python3 scripts/check_static_assets.py` (ریشه) | PASS |
| `git diff --check` | بدون خطا |

- تست‌های جدید: ۱۱۴ تست (combobox ۱۶؛ کلاینت/پارسر ۱۱؛ اعتبارسنجی ۹؛ نگاشت خطا ۶؛ دسترسی/نگهبان ۱۳؛ فهرست ۱۱؛ فرم‌های ایجاد/ویرایش ۲۱؛ جزئیات ۶؛ Routeها/بارگذاری مستقیم ۹؛ یکپارچگی App ۴؛ منو ۶ + سازگاری منو/Route ۱؛ `ApiError.problemCode` ۱).
- **۳ شکست قدیمی (بدون ارتباط، دست‌نخورده):** `securityAuditClient.test.ts` (۱) و `SecurityAuditPage.test.tsx` (۲)؛ `object.stream is not a function` در `Response(Blob)` محیط آزمون. پیش از تغییر هم وجود داشت و اصلاح/پنهان نشد.

## اجرا نشد
- **مرورگر واقعی/Playwright و اجرای برنامه روی SQL Server:** اجرا نشد (مرورگر و Playwright روی این ماشین نبود و سرویس Docker در دسترس نبود). بنابراین تصویر/اسکرین‌شات، سه عرض ۳۶۰/۷۶۸/۱۲۸۰، LCP/CLS و تب Network اندازه‌گیری نشد.
- بازبینی مستقل (`code-reviewer`/`security-reviewer`) توسط این Agent انجام نشد.

## اصلاحات پس از بازبینی مستقل
- M2: تعارض هم‌زمانی دیگر فرم را unmount نمی‌کند (کلید `rowVersion` حذف شد)؛ دو مسیر صریح و تست‌شده (حفظ ویرایش‌ها / نمایش مقادیر تازه با تأیید) و خطای دریافت مجدد فقط Alert درون‌صفحه‌ای.
- M1: در حالت ناهمگام `SearchableCombobox`، تا رسیدن نتیجهٔ query فعلی (حالت loading یا query متفاوت) Enter/کلیک انتخاب نمی‌کند و گزینه‌ها `aria-disabled` می‌شوند (تست رگرسیون).
- L4 (trim در debounce فهرست)، L2 (دو متن props با پیش‌فرض)، L6 (`getJson` از `api/client.ts` export شد و `tradeGet` موازی حذف شد)، نیت «شناسهٔ حقوقی»، و کاهش flaky بودن تست فهرست با prop `searchDelayMs`.

## بدهی فنی و محدودیت‌های شناخته‌شده
- `useFormExit` از `features/hr` ایمپورت می‌شود؛ باید در یک کار جدا به lib مشترک منتقل شود.
- `TradeAccessProvider` برای هر کاربر واردشده یک درخواست اضافی `GET /trade/capabilities` می‌زند.
- Home/End در combobox قابل‌ویرایش (APG) پیاده نشده است.
- در `SearchableCombobox` برای `loadOptions` در وابستگی‌های effect از `eslint-disable react-hooks/exhaustive-deps` استفاده شده است (callerها closure درون‌خطی می‌دهند).

## تغییر قواعد بک‌اند (تصمیم مالک) و همگام‌سازی فرانت
- نوشتن طرف‌حساب (POST/PUT) هم مجوز نوشتن و هم `trade.form.party.view` می‌خواهد؛ UI: دکمه/Route ایجاد فقط با `available && party.create && party.view` و ویرایش با `available && party.update && party.view` (`requiredHints` در `access/TradeAccess.tsx`). «ویرایش بدون مشاهده» دکمه را پنهان و Route را رد می‌کند.
- نوشتن طرف‌حساب در دورهٔ مالی بسته مجاز است (داده مرجع سطح Tenant): قاعدهٔ «دورهٔ قابل‌نوشتن» برای Party حذف شد (`periodBoundHints` عمداً خالی و با توضیح برای عملیات آیندهٔ وابسته به دوره نگه داشته شد)؛ در دورهٔ بسته هیچ بنر/حالت غیرفعالی نیست. پنل «دورهٔ مالی فقط‌خواندنی» و بنر فهرست حذف شد؛ ۴۰۹ `trade_period_read_only` همچنان (دفاعی) با پیام فارسی نگاشت می‌شود.
- کدهای جدید: ۴۱۳ `trade_request_too_large` ← پیام «حجم اطلاعات ارسالی بیش از حد مجاز است…»؛ ۴۰۰ `trade_invalid_request_body` ← پیام عمومی «درخواست قابل پردازش نبود…»؛ ۴۰۳ `trade_forbidden` هنگام ذخیره ← پیام مجوز؛ در هر سه فرم و مقادیر حفظ می‌شود (تست‌شده).

## ریسک‌ها و کارهای باقی‌مانده
- بدون smoke واقعی، ظاهر (RTL، همپوشانی popup کشور، ریسپانسیو) فقط با تست DOM پوشش داده شده است.
- مجوز `update` بدون `view` (L-3 بک‌اند): ویرایش داده را با `view` می‌خواند؛ کاربر چنین نقشی «دسترسی ندارید» می‌بیند.
- هر تغییر مجوز Trade در میانهٔ نشست تا رفرش/تلاش مجدد در hint دیده نمی‌شود؛ سرور همیشه مرجع است و ۴۰۳ را با پنل مناسب نشان می‌دهیم.
- `/trade/products` و سایر آیتم‌ها هنوز نیست؛ `useFormExit` باید به کد مشترک منتقل شود.

## سؤال برای مالک
1. نام‌های فارسی نقش‌ها («کارگزار ارز»، «نمایندهٔ کشتیرانی»، «پشتیبان دام») را تأیید می‌کنید؟ (در مستندات برچسب فارسی تعریف‌شده‌ای یافت نشد؛ در `lib/tradeLabels.ts` است.)
2. آیا `useFormExit` به `lib/` مشترک منتقل شود؟
