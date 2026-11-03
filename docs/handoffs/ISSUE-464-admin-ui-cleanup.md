# تحویل وظیفه — ISSUE-464-admin-ui-cleanup

- **Issue:** #464 (PR-A5، پاک‌سازی `ClientApp`؛ روی A3 که شامل A1/A2/A4 است)
- **Agent/مسئول:** frontend-developer (Claude Code)
- **تاریخ:** 2026-11-03
- **وضعیت:** انجام‌شده در حد کلاینت (بازبینی مستقل و بررسی دستی مرورگر هنوز انجام نشده؛ Commit/Push نشده)

## هدف و معیارهای پذیرش
- [x] حذف رنگ hex دلخواه و کلاس‌های پالت خام Tailwind از کامپوننت‌ها (توکن نام‌دار)
- [x] ویژگی‌های فیزیکی RTL به منطقی تبدیل شد (TSX و CSS) و حالت RTL ظاهر یکسان دارد
- [x] ادغام `PasswordInput`، وضعیت دورهٔ مالی در یک ماژول، Alertهای دست‌ساز به `ui/Alert`
- [x] کدسپلیت دو صفحهٔ Provisioning؛ لینک «پرش به محتوای اصلی»؛ `headingLevel` در `FormSection`
- [x] ۳۱۶ تست قبلی سبز ماندند (۳۳۳ تست در مجموع)
- [ ] `MyAccountPanel` تنبل نشد (عمداً؛ دلیل در «تصمیم‌ها»)

## خلاصه تغییرات

### ۱) رنگ‌ها (شمارش با `grep` روی `src/components/**/*.tsx` بدون تست)
| معیار | پیش | پس |
|---|---|---|
| خط‌های دارای hex (`#xxx`) | 42 خط در 6 فایل (WorkContextSelector 18، SelectionBackdrop 11، SelectionFooter 7، ChoiceCard 3، AppShell 2، SelectionLayout 1) | 1 خط: یک کامنت «Issue #442» در `AppShell.tsx` (هیچ رنگ واقعی نیست) |
| خط‌های دارای کلاس پالت خام (slate/rose/emerald/amber/sky/blue/teal/…) | 51 خط (+ چند `teal-*` در Dashboard) | 0 |

- توکن‌های جدید در `@theme` فایل `styles/index.css` (با کامنت): پالت صفحهٔ انتخاب (`navy-header`، `bar-*`، `wash*`، `frost*`، `azure*`، `steel*`، `chip*`، `mint`، `faint`، `line-strong`، `period-end`، `pick-light`، `cta-mid`)، پالت SVG (`ridge`، `ridge-soft`، `ridge-bars`، `glow`)، و متن روی سطح سرمه‌ای (`on-navy`، `on-navy-accent`، `on-navy-ok`، `dot-open`). مقادیر همان مقادیر قبلی‌اند؛ جایی که دو hex فقط چند واحد فرق داشتند به نزدیک‌ترین توکن ادغام شد (تفاوت بصری جزئی، بررسی چشمی در مرورگر انجام نشده).
- نگاشت پالت خام به توکن موجود: `emerald/amber/rose` به `success/warning/danger` (و soft)، `slate` به `cool/line/sub/ink/faint`، `sky/blue` روی سرمه‌ای به `on-navy*`، `blue-100/700` به `period/azure`.
- SVG: `SelectionBackdrop` با کلاس‌های `[stop-color:var(--color-…)]`، `fill-ridge-bars`، `stroke-glow`؛ `SelectionFooter` با `stroke-cta` و `stopColor="white"` (واژهٔ کلیدی برای mask). بدون `style` درون‌خطی در HTML.
- hex باقی‌مانده عمدی: فقط درون خود `styles/index.css` و `premium-dashboard.css` (تعریف توکن‌ها و gradientهای قدیمی کلاس‌های `.faranou-*`)، خارج از محدودهٔ این PR.
- باقی‌مانده عمدی: مقدارهای `rgb()/rgba()` در سایه‌ها (`shadow-[...]`) و شفافیت‌ها (۱۸ مورد در Dashboard، SelectionFooter، SelectionLayout، WorkContextSelector، ChoiceCard)؛ hex نیستند و توکن مشترکی ندارند.

### ۲) RTL منطقی
- `Dashboard`: `-right-16`/`-right-7`/`left-10` به `-start-16`/`-start-7`/`end-10`؛ `SelectionFooter`: `left-0` به `end-0`؛ `AppShell`: `text-right` (×2) به `text-start`؛ `ProvisioningEnrollmentPage`: `text-left` به `text-start` (ورودی `dir=ltr`). `MyAccountPanel`/`account/*` فیزیکی باقی نداشت (فقط کامنت).
- CSS: متغیر `--inline-sign` (۱ در LTR، ‏۱- در `html[dir='rtl']`) در `index.css`. سایدبار موبایل: `right: 0` به `inset-inline-start: 0` و `translateX(calc(var(--inline-sign) * -105%))`؛ hover و glint و سایه‌های جهت‌دار (سایدبار premium/light، موبایل) همه با ضرب در `--inline-sign`. در RTL مقدارهای قبلی دقیقاً بازتولید می‌شوند (۱- × ‏-105% = ‏105% و …)؛ این ریاضی بررسی شد، اما تصویر مرورگر در RTL/LTR گرفته نشد.

### ۳) تکرارها
- (الف) `ui/PasswordInput.tsx` تنها پیاده‌سازی شد (`fieldName` الزامی، `togglePlacement: 'start' | 'end'`، پیش‌فرض `start`)؛ `account/PasswordInput.tsx` حذف شد؛ `SecurityTab` و `UserForm` (با `end`) مهاجرت کردند. سمت دکمه پارامتر ماند چون دو صفحهٔ موجود جای متفاوتی داشتند و تست‌هایشان همان را می‌سنجد.
- (ب) `lib/fiscalStatus.ts`: جدول‌های برچسب (سه عبارت‌بندی موجود و آزمون‌شده: سال/دوره/علت مسدودی)، کلاس نشانهٔ سال، کلاس pill مرور شرکت و نقطهٔ وضعیت فوتر. سه فایل قبلی دیگر نگاشت محلی ندارند.
- (ج) `role="alert"` دست‌ساز: خطای خروج در `AppShell`، `ProvisioningEnrollmentPage`، `CustomerProvisioningImportPage` به `ui/Alert`. نماندند عمداً: `Feedback.ErrorState` (دکمهٔ تلاش مجدد)، کارت «کاربر پیدا نشد» در `UserDetailPage`/`UserEditPage` (کارت خنثی با عنوان، نه پیام خطا)، pillهای وضعیت `CompanyOverviewPanel` (گرد و بدون آیکون/حاشیه؛ `Badge` همان ظاهر را نمی‌دهد).
- (د) `FaranouButton` در برابر `ui/button.tsx`: ۵ محل استفاده در ۳ فایل (SelectionFooter 1، WorkContextSelector 2، BlockedAccess 2). مهاجرت نشد: ظاهر برابر نیست (ارتفاع `min-h-11` و `rounded-[10px]` در برابر `h-8` shadcn) و رفتار متفاوت است (`loading`، الگوی `aria-disabled` که فوکوس را نگه می‌دارد، محافظ کلیک دوم). فقط کلاس‌های slate خام آن به توکن تبدیل شد. پیشنهاد: افزودن `loading`/`aria-disabled` به `button.tsx` در کار جدا و سپس حذف.
- (ه) `ui/icons.tsx` (14 آیکون سفارشی؛ 6 فایل: SelectionFooter، BlockedAccess، WorkContextSelector، ChoiceCard، Alert، Badge) در برابر `lucide-react` (12 فایل). فقط فهرست‌برداری؛ تغییری داده نشد. دلیل ماندن بررسی نشد.
- (و) `Feedback.tsx` (`LoadingState`/`ErrorState`/`EmptyState`) و `BlockedAccess`: هر دو حالت «چیزی ممکن نیست» را نشان می‌دهند ولی `BlockedAccess` صفحهٔ کامل با `SelectionLayout` و `Feedback` بلوک درون‌صفحه است. فقط مستند شد؛ ادغام لازم نیست.

### ۴) کارایی
- `src/Root.tsx` (جدید): انتخاب `/enroll` و `/provisioning/import` با `React.lazy` + `Suspense` (`LoadingState`)؛ `main.tsx` فقط `Root` را رندر می‌کند. رفتار مسیر (سوییچ بر اساس pathname) همان است.
- `MyAccountPanel` تنبل نشد: بدون `ErrorBoundary` در برنامه، شکست بارگذاری chunk در پوستهٔ همیشه‌نمایان کل برنامه را از بین می‌برد و سود (حدود ۲۶ کیلوبایت منبع، اندازه‌گیری نشد) کم است. اگر ErrorBoundary اضافه شود قابل بازبینی است.
- اندازهٔ باندل از `npm run build`:

| | پیش | پس |
|---|---|---|
| index JS | 395.04 kB (121.30 gzip) | 325.42 kB (97.65 gzip؛ پس از ErrorBoundary) |
| index CSS | 105.18 kB (20.00 gzip) | 104.79 kB (19.68 gzip) |
| chunk جدید `button` (Base UI، modulepreload شده در `index.html`) | وجود نداشت | 58.10 kB (21.86 gzip) |
| `ProvisioningEnrollmentPage` | داخل index | 5.03 kB (1.94 gzip) |
| `CustomerProvisioningImportPage` | داخل index | 9.09 kB (3.41 gzip) |

  نتیجه: مسیر عادی برنامه (index + button که preload می‌شود) تقریباً 382.1 kB در برابر 395.04 kB؛ یعنی سود واقعی ورودی حدود 13 kB (حدود 2 kB gzip) است، نه 71 kB؛ بیشتر کاهش index به chunk مشترک `button` رفته است. chunkهای قبلی: userDisplay 0.12، userFlash 0.29، Breadcrumb 0.94، UserCreatePage 1.16، UserEditPage 3.03، DataTable 4.66، UserDetailPage 5.90، UsersListPage 6.52، UserFormFields 11.73 kB. LCP/CLS اندازه‌گیری نشد.

### ۵) دسترس‌پذیری
- `AppShell`: پیوند «پرش به محتوای اصلی» (`sr-only`، با فوکوس دیده می‌شود) اولین عنصر قابل‌فوکوس؛ کلیک/Enter به‌جای تغییر hash، فوکوس را به `<main id="main-content" tabIndex=-1>` می‌برد (روتر مبتنی بر pathname است).
- `FormSection`: prop `headingLevel` (2، 3 یا 4؛ پیش‌فرض 3)؛ فرم کاربر (ایجاد/ویرایش) روی 2 تا از h1 مستقیم به h2 برسد.
- toggle رمز: برچسب ثابت «نمایش …» + `aria-pressed` (برچسب دیگر عوض نمی‌شود).

### تغییرهای واقعی رنگ (ناشی از نگاشت به توکن)
نگاشت به توکن در همه‌جا «همان رنگ» نیست. جدول زیر تغییرهای مرئی ولی کوچک را فهرست می‌کند؛ هگز کلاس‌های Tailwind مقدارهای تقریبی پالت پیش‌فرض Tailwind هستند. همه در انتظار بازبینی بصری‌اند و اجرای QA واقعی مرورگر را سرپرست (lead) جداگانه انجام می‌دهد؛ در این PR مرورگر اجرا نشد.

| محل | قبل | بعد (توکن) | تغییر |
|---|---|---|---|
| Dashboard، متن/آیکون سبز | `emerald-500/600/700/800` (#10b981 تا #065f46) | `success` #256b4f | سبزِ تیره‌تر و یکدست |
| Dashboard، پس‌زمینه/حاشیهٔ سبز | `emerald-50/100` (#ecfdf5/#d1fae5) | `success-soft` #e9f4ee، حاشیه `success/20` | کمی خاکستری‌تر |
| Dashboard، هشدار | `amber-100/200/700` (#fef3c7/#fde68a/#b45309) | `warning-soft` #fdf4e3، `gold/40`، `warning` #8a6116 | متن قهوه‌ای‌تر |
| Dashboard، کاشی‌ها | `sky-100/700`، `blue-100/700`، `teal-100/700` | `azure-chip/azure-text`، `period/azure`، `teal-soft/teal-deep` | تن‌ها نزدیک ولی نه برابر |
| Dashboard، خنثی | `slate-100/600/700` (#f1f5f9/#475569/#334155) | `cool` #eef2f5، `sub` #4a5d6e، `ink` #22384b | کمی سرمه‌ای‌تر |
| Dashboard، آیکون‌ها | `blue-600/400` | `azure` #1d4f86، `pick` #63afe3 | تیره‌تر / روشن‌تر |
| Dashboard، `text-amber-200` | #fde68a | `cta-soft` #ffe4b7 | کمی کم‌رنگ‌تر |
| Dashboard، روی سرمه‌ای | `sky-200` #bae6fd، `blue-50` #eff6ff | `on-navy-accent` #bae6fd (برابر)، `on-navy` #e0f2fe | `blue-50` کمی آبی‌تر |
| AppShell، متن روی سرمه‌ای | `blue-100` #dbeafe، `sky-100` #e0f2fe | `on-navy` #e0f2fe | `blue-100` کمی روشن‌تر؛ `sky-100` برابر |
| AppShell، لینک فعال مدیریت کاربران | `bg-sky-500/20`، `ring-sky-300/30` (#0ea5e9، #7dd3fc) | `pick` #63afe3 با همان شفافیت | آبی ملایم‌تر و کم‌اشباع‌تر |
| AppShell، فوکوس آواتار | `ring-amber-300` #fcd34d، offset `#102b4c` | `gold` #d9a24b، `night` #10263d | حلقهٔ طلایی‌تر؛ offset کمی تیره‌تر |
| AppShell، «محیط کاری امن» | `emerald-200` #a7f3d0 | `on-navy-ok` #a7f3d0 | برابر |
| AppShell، خطای خروج | `rose-50/200/800` | `Alert` خطا (`danger-soft`، `danger/30`، `danger`) | قرمز مایل به قهوه‌ای و دارای آیکون |
| FaranouButton ثانویه | حاشیه `slate-400` #94a3b8 | `faint` #7b8ba0 | تیره‌تر |
| FaranouButton غیرفعال | `slate-300/200/600` (#cbd5e1/#e2e8f0/#475569) | `line-strong` #b3bfce، `line` #dfe6ec، `sub` #4a5d6e | حاشیه تیره‌تر، پس‌زمینه روشن‌تر |
| ChoiceCard غیرفعال | حاشیه `slate-200`؛ دایره `slate-300`/`slate-200` | `line`؛ `line-strong`/`line` | حاشیهٔ دایره تیره‌تر |
| ChoiceCard، سایر | `#b3bfce`، `#7b8ba0`، `#d6e3f2`، `#e1e9f3`، hover `#f7fbff` | `line-strong`، `faint`، `frost-deep` #c6d9ef، `frost` #dde6f1، `wash-top` #f7faff | `#d6e3f2` تیره‌تر شد؛ `#e1e9f3` کمی تیره‌تر |
| Badge خنثی | حاشیه `slate-300` #cbd5e1 | `line-strong` #b3bfce | تیره‌تر |
| WorkContextSelector | `#cfe0f3`، `#c6d9ef`؛ `#dde6f1`، `#dbe6f3`، `#e1e9f3`، `#e5edf6`؛ `#1d6aa5`؛ `#e6eef9`؛ `#e9f0f9` | `frost-deep` #c6d9ef؛ `frost` #dde6f1؛ `azure-text` #1f6ea6؛ `period` #e7f0fc؛ `wash-deep` | ادغام نزدیک‌ها؛ تفاوت چند واحد روشنایی. `#e5edf6` اکنون تیره‌تر |
| WorkContextSelector، حالت‌های خالی | `slate-400` #94a3b8، `slate-500` #64748b، `slate-100` | `faint` #7b8ba0، `muted` #64798b، `cool` | حاشیه تیره‌تر |
| SelectionBackdrop | `#f4f8fe` | `wash-top` #f7faff | کمی روشن‌تر |
| SelectionFooter، نقطهٔ وضعیت | `emerald-400` #34d399، `slate-300`، `slate-400` | `dot-open` #34d399 (برابر)، `line-strong` #b3bfce، `faint` #7b8ba0 | بسته: تیره‌تر؛ بایگانی: تیره‌تر |
| SelectionFooter/Layout | `sky-200` | `on-navy-accent` #bae6fd | برابر |

- صفحه‌های انتخاب (WorkContextSelector و BlockedAccess) از `AppShell` استفاده نمی‌کنند، پس «پرش به محتوای اصلی» ندارند.
- هدف پرش (`<main id="main-content">`) عمداً `outline-none` دارد (فوکوس برنامه‌ای)؛ نشانهٔ فوکوس بصری روی `main` نیست.
- `--inline-sign` به `html[dir='rtl']` به‌عنوان منبع واحد جهت وابسته است؛ `div dir="rtl"` خود AppShell باید همسو بماند.

### ErrorBoundary برای chunkهای تنبل (پس از بازبینی)
- `ui/ErrorBoundary.tsx`: پیام فارسی با `role="alert"` و فوکوس، دکمهٔ «تلاش دوباره» و «بارگذاری مجدد صفحه». `Root.tsx` هر دو مسیر Provisioning را با آن می‌پیچد؛ تلاش دوباره یک `lazy` تازه (import جدید) می‌سازد چون `React.lazy` رد شدن import را کش می‌کند. تست: import ردشده، نمایش alert با فوکوس، تلاش دوباره موفق (`Root.test.tsx`). `MyAccountPanel` همچنان تنبل نیست.

### یافته‌های QA مرورگر واقعی (Chromium، ۳۶۰/۷۶۸/۱۲۸۰) و اصلاح‌ها
منبع: `/home/claude/qa/REPORT.md`؛ بازبینی پس از اصلاح با همان harness (کپی در `/home/claude/qa2/harness`، خروجی در `/home/claude/qa2/`).

| یافته | اصلاح | اندازهٔ واقعی پس از اصلاح (۳۶۰) |
|---|---|---|
| دکمه‌های `size="lg"` ۳۶ پیکسل | `ui/button.tsx`: `lg` با `max-sm:min-h-11`؛ دکمه‌های خام `SecurityTab` و دکمه‌های `ErrorBoundary` هم `min-h-11`؛ برگه‌های `Tabs` در موبایل `min-h-11` | لینک «ایجاد کاربر» 44، «ایجاد کاربر/انصراف» فرم 44، «پاک‌کردن/تغییر رمز و خروج» 44، تب‌ها 44. در ۷۶۸ و ۱۲۸۰ دکمه‌های lg همچنان ۳۶ (تغییر ناخواسته نشد) |
| تب سوم «سوابق ورود» بریده | تب‌ها در موبایل `flex-1 basis-0` با padding و متن کوچک‌تر و امکان شکستن خط؛ `TabLabel` وسط‌چین | هر سه تب کامل: عرض هرکدام 106 داخل 0 تا 360 و `scrollWidth ≤ clientWidth` |
| نوار پایین دراور روی پیام اعتبارسنجی | `footer` با `shrink-0 flex-wrap gap-x-4 gap-y-1` (دو جمله در دو خط)، ناحیهٔ محتوا `pb-8`؛ ستون flex و اسکرول محتوا از قبل بود | پایین ناحیهٔ اسکرول 679 = بالای footer 679 (همپوشانی ندارد)؛ با اسکرول به انتها آخرین پیام کاملاً دیده می‌شود |
| سایدبار بستهٔ موبایل با Tab قابل‌دسترسی | CSS: `visibility: hidden` + `transition: transform 180ms ease, visibility 0s linear 180ms`؛ حالت `is-open` فوراً `visible`. دسکتاپ بدون تغییر (فقط در media موبایل) | در ۳۶۰، ۱۴ بار Tab: هیچ عنصری داخل `aside` نیست (قبلاً داشبورد/مدیریت سیستم/مدیریت کاربران بودند)؛ باز: visible؛ بسته‌شدن: تأخیر ۱۸۰ms برای نمایش اسلاید (در `dbg.js`: visible تا حدود 150ms سپس hidden) |
| آیکون جستجو بیرون از ورودی، ورودی تمام‌عرض نیست | `UsersListPage`: ورودی در `div.relative`، آیکون با `start-3` و دکمهٔ پاک‌کردن با `end-0` داخل فیلد؛ `FormField` جایگزین label+hint دستی با `aria-describedby`؛ دکمهٔ پاک‌کردن 44×44 | ورودی 322 از 322 (تمام‌عرض)، ارتفاع 44 در موبایل (40 از 640 به بالا)، آیکون و دکمهٔ پاک‌کردن داخل ورودی |

- تست‌های جدید: `styles/mobileSidebar.test.ts` (قاعدهٔ CSS، چون jsdom استایل را اعمال نمی‌کند؛ از `node:fs` با `@ts-expect-error` چون `@types/node` نصب نیست)، تست ارتفاع `lg` در `shadcn-components.test.tsx`، تست آیکون/پاک‌کردن داخل فیلد در `UsersListPage.test.tsx`.
- نکتهٔ harness: اجرای کامل (80 سناریو) فقط یک ناموفق داشت: `tab-order-users` در ۳۶۰ به‌دلیل «حلقهٔ فوکوس» لینک ردیف جدول. این false negative است: حلقه روی `::after` لینک کشیده است (computed `::after` box-shadow طلایی 2px تأیید شد)؛ پیش‌تر چون سایدبار Tab را مصرف می‌کرد به ردیف‌ها نمی‌رسید.
- harness با `reducedMotion: reduce` اجرا می‌شود؛ پس تأخیر اسلاید در آن دیده نمی‌شود و جدا (بدون reduced-motion) سنجیده شد.

## فایل‌های مهم تغییرکرده
- `ClientApp/src/styles/index.css`، `premium-dashboard.css` — توکن‌ها، `--inline-sign`، transform/سایهٔ منطقی
- `ClientApp/src/components/ui/PasswordInput.tsx` (+ test) — جایگزین دو پیاده‌سازی؛ `account/PasswordInput.tsx` حذف شد
- `ClientApp/src/lib/fiscalStatus.ts` (+ test)
- `ClientApp/src/Root.tsx` (+ test)، `main.tsx`
- `AppShell`، `Dashboard`، `SelectionFooter`، `SelectionLayout`، `WorkContextSelector`، `CompanyOverviewPanel`، `ui/ChoiceCard`، `ui/Badge`، `ui/SelectionBackdrop`، `ui/FaranouButton`، `ui/FormSection`، `UserFormFields`، `SecurityTab`، دو صفحهٔ Provisioning

## تصمیم‌های فنی
- تصمیم معماری جدیدی گرفته نشد (بدون وابستگی، بدون Backend). بستهٔ `cn` رسمی shadcn است و دست نخورد.
- تست `UserCreatePage.test.tsx` (خط مربوط به toggle رمز) تنها تغییر assert موجود است: به‌جای نام دکمهٔ «پنهان‌کردن رمز عبور» اکنون همان نام ثابت «نمایش رمز عبور» با `aria-pressed=true` بررسی می‌شود، چون برچسب ثابت الگوی ARIA درست‌تر است. قدرت assert کم نشده، رفتار آشکارا عوض شده.
- سه عبارت‌بندی وضعیت دوره عمداً ادغام نشد چون تست‌ها هر سه را می‌سنجند (`بایگانی‌شده` در علت مسدودی در برابر `بایگانی`).

## بررسی و آزمون (در `ClientApp`)
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `npm run typecheck` | موفق | بدون خطا |
| `npm test` | موفق | 33 فایل، 338 تست (پیش: 316؛ +1 ErrorBoundary، +3 اصلاح‌های QA)؛ جدید: PasswordInput 5، fiscalStatus 5، Root 3، AppShell 2 (skip link، Alert خروج)، FormSection 1، UserCreatePage 1 (outline h1 به h2) |
| `npm run build` | موفق | اعداد بالا |
| `npm run check:offline` | موفق | 18 منبع محلی، بدون منبع بیرونی |
| `git diff --check` | موفق | بدون خروجی |
| `dotnet build` / `check_static_assets.py` | اجرا نشد | Razor/Backend تغییر نکرد |
| بررسی دستی مرورگر (۳۶۰/۷۶۸/۱۲۸۰، RTL و LTR، focus، skip link، موبایل-سایدبار) | اجرا نشد | مرورگر در این محیط اجرا نشد |

## امنیت، داده و سازگاری
- بدون تغییر Backend/Schema/Migration/احراز هویت. ورودی رمز همچنان فقط در state؛ ویژگی‌های `spellCheck/autoCapitalize/autoCorrect` حفظ شد. بدون `style`/اسکریپت درون‌خطی در Razor؛ بدون CDN.
- شکست بارگذاری chunk صفحات Provisioning اکنون با ErrorBoundary (تلاش دوباره/بارگذاری مجدد) مدیریت می‌شود.

## ریسک‌ها و کارهای باقی‌مانده (بدهی)
- بررسی بصری مرورگر برای ادغام رنگ‌های نزدیک و ریاضی `--inline-sign` در سایدبار موبایل و سایه‌ها.
- تنبل‌کردن `MyAccountPanel` (ErrorBoundary اکنون هست، ولی عمداً انجام نشد).
- ادغام `FaranouButton` در `button.tsx` پس از افزودن `loading`/`aria-disabled`.
- یکسان‌سازی سمت toggle رمز (start در حساب کاربری، end در فرم کاربر) پس از تصمیم طراحی.
- hex قدیمی داخل `.faranou-*` در CSS و سایه‌های `rgb()` هنوز توکن نشده‌اند.
- `icons.tsx` در برابر `lucide-react`: تصمیم نشده.
- chunk مشترک `button` (58 kB) سنگین است؛ بررسی علت (Base UI) و تقسیم بهتر.

## شناسه‌های Git
- Branch: `agent/464-admin-ui-cleanup` (Commit/Push انجام نشد)
- Commit: —
- Pull Request: —
