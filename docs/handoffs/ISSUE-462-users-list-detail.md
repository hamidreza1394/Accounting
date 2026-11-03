# تحویل وظیفه — ISSUE-462-users-list-detail

- **Issue:** #462 (PR-A2؛ روی A1 = شاخهٔ `agent/461-admin-ui-foundation`، PR #469)
- **Agent/مسئول:** frontend-developer (Claude Code)
- **تاریخ:** 2026-11-03
- **وضعیت:** انجام‌شده (بازبینی مستقل، بررسی دستی مرورگر و Merge هنوز انجام نشده)

## هدف و معیارهای پذیرش
- [x] روتر A1 به `App`/`AppShell` وصل شد (`/`، `/admin/users`، `/admin/users/:id`)؛ ناوبری واقعی به‌جای `activeAdminSection`
- [x] فهرست کاربران سمت‌سرور: جستجوی debounce، فیلتر وضعیت، مرتب‌سازی، اندازهٔ صفحه، صفحه‌بندی، حالت در URL
- [x] صفحهٔ جزئیات با Breadcrumb و سه تب (فقط فیلدهای موجود API)
- [x] حالت‌های loading/empty/error/403/404/401
- [x] تست‌های قبلی سبز (به‌جز تطبیق‌های ذکرشده)؛ typecheck، build، check:offline موفق

## خلاصه تغییرات
- `lib/router.tsx`: هوک `useSearchParams()` (خواندن query از URL و تغییر آن با push یا replace) همراه تست؛ بدون وابستگی جدید.
- `components/AppRoutes.tsx` (جدید): جدول مسیرها، `AppRouteDataContext` (session، context، `onUnauthenticated`) و صفحات مدیریتی `React.lazy`. داشبورد عمداً eager است (مسیر پیش‌فرض).
- `App.tsx`: مسیرها و `canAccess` (از `can(session, ...)`) را به `AppShell` می‌دهد و `<RouteView />` را رندر می‌کند. عنوان تب داخل پوسته را روتر تعیین می‌کند (`داشبورد | فرانو`)، فقط صفحهٔ انتخاب دوره مالی عنوان قبلی را نگه می‌دارد.
- `AppShell.tsx`: خودش `RouterProvider` را می‌سازد (قرارداد props قبلی حفظ شد، `routes` و `canAccess` اختیاری‌اند)، لینک داشبورد و «مدیریت کاربران» با `Link` و `aria-current` از `useLocation`؛ ظاهر سایدبار، آیتم‌های غیرفعال، `MyAccountPanel` و جریان work-context بدون تغییر. آیتم «مدیریت کاربران» برای `/admin/users/*` فعال می‌ماند.
- `components/admin/users/UsersListPage.tsx`، `UserDetailPage.tsx`، `userDisplay.ts` (جدید)؛ `UserManagementPage.tsx` و تستش حذف شد (کد مرده نماند؛ تست‌هایش به تست‌های جدید منتقل شد).
- `ClientApp/scripts/offline-check.mjs`: اصلاح باگ موجود در پایهٔ حل‌کردن لیست preload ویت (`appUrl` با اسلش پایانی، `dirname` آن والد بود، پس `assets/X.js` به `/assets/X.js` حل می‌شد). تا وقتی chunk تنبل نبود دیده نمی‌شد؛ با اولین chunk تنبل `check:offline` شکست می‌خورد. تست رگرسیون اضافه شد.
- `main.tsx` بدون تغییر ماند (سوئیچ `/enroll` و `/provisioning/import` دست‌نخورده)؛ `RouterProvider` در `AppShell` است، نه در `main.tsx` (نک. انحراف‌ها).

## رفتار لیست
- پارامترهای URL: `q`، `status` (`active|inactive`)، `sort` (`name|username|lastLogin|created`)، `dir=desc`، `page`، `pageSize` (10/20/50). فقط مقادیر غیرپیش‌فرض نوشته می‌شوند و مقدار نامعتبر به پیش‌فرض برمی‌گردد. تایپ جستجو با `replace` و بقیهٔ تغییرها با `push`؛ هر تغییر فیلتر/مرتب‌سازی/اندازهٔ صفحه، صفحه را ۱ می‌کند.
- درخواست‌های منسوخ با `AbortController` و بررسی `signal.aborted` نادیده گرفته می‌شوند؛ هرگز کل کاربران گرفته و سمت کلاینت فیلتر نمی‌شود.
- صفحهٔ بیش از انتها (URL قدیمی) به آخرین صفحه منتقل می‌شود. 403 پیام ثابت بدون «تلاش دوباره»؛ 401 به `onUnauthenticated` موجود.

## تصمیم‌های فنی
- تصمیم معماری جدیدی گرفته نشد؛ ADR جدید لازم نبود.
- کد 400 و 404 هر دو «کاربر پیدا نشد» نمایش داده می‌شوند (شناسهٔ نامعتبر). رفتار واقعی سرور برای شناسهٔ نامعتبر بررسی نشد (Backend دست‌نخورده).
- تب «اطلاعات عمومی» علاوه بر نام/نام کاربری/نقش قدیمی، آخرین ورود و تاریخ ایجاد/ویرایش را (از فیلدهای موجود) نشان می‌دهد. تب نقش‌ها/سال مالی/سوابق ورود/Audit و هرگونه لینک به API ناموجود عمداً نیامده است.

## بررسی و آزمون (در `ClientApp`)
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `npm run typecheck` | موفق | بدون خطا |
| `npm test` | موفق | 25 فایل، 222 تست (A1: 23 فایل، 180 تست) |
| `npm run build` | موفق | index JS 386.32 kB (119.47 gzip)، CSS 107.67 kB (20.41 gzip)؛ chunkهای تنبل: UsersListPage 6.13 kB، UserDetailPage 6.70 kB، chunk مشترک userDisplay 7.94 kB |
| `npm run check:offline` | موفق | 9 منبع محلی، بدون منبع بیرونی (پس از اصلاح اسکریپت؛ پیش از آن شکست خورد) |
| `git diff --check` | موفق | بدون خروجی |
| `dotnet build` / `check_static_assets.py` | اجرا نشد | Razor/Backend تغییر نکرد |
| بررسی دستی مرورگر (۳۶۰/۷۶۸/۱۲۸۰) | اجرا نشد | مرورگر در این محیط اجرا نشد |

## تطبیق تست‌های قبلی (هدف حفظ شد)
- `AppShell.test.tsx`: «مدیریت کاربران» اکنون `link` با `href=/app/admin/users` است (قبلاً `button`).
- `App.test.tsx`: عنوان تب داشبورد `داشبورد | فرانو` است (قبلاً `فرانو — داشبورد`) چون روتر عنوان را می‌گذارد.
- `MyAccountPanel.test.tsx` و `MyAccountPanel.tsx` دست نخوردند؛ همان‌طور که هست سبز است (AppShell بدون Provider بیرونی کار می‌کند).
- تست‌های جدید: `UsersListPage.test.tsx` (21)، `UserDetailPage.test.tsx` (12)، `AppRouting.test.tsx` (5)، تست `useSearchParams`، تست رگرسیون `offline-check`؛ داده‌ساز مشترک `src/test/adminUsers.ts`.

## امنیت، داده و سازگاری
- بدون تغییر Backend/Schema/Migration. مجوز UI فقط تلمیح است (`can`)؛ سرور مرجع است (D-064). کاربر غیرمدیر در deep link صفحهٔ «دسترسی ندارید» می‌بیند و هیچ درخواست `/admin/users` ارسال نمی‌شود (تست شده). رمز/هش هرگز نمایش داده نمی‌شود. چیزی در `localStorage` نگهداری نمی‌شود. وابستگی npm جدید اضافه نشد.

## ریسک‌ها و کارهای باقی‌مانده
- بررسی دستی مرورگر (RTL، stretched link ردیف جدول، focus، موبایل) انجام نشد.
- `RouterProvider` داخل `AppShell` است؛ اگر بعداً صفحات خارج از پوسته (مثلاً صفحهٔ انتخاب دوره) مسیر بخواهند، باید بالاتر برده شود.
- `AppRouteDataContext` در هر رندر `App` شیء جدید می‌گیرد (رندر مجدد صفحهٔ داشبورد با هر رندر App)؛ هزینهٔ آن کم است ولی memo نشده.
- ردیف جدول: فقط ستون اول لینک واقعی است (stretched link A1)؛ انتخاب متن داخل ردیف ممکن است سخت باشد.
- شکاف‌های API: فیلتر شرکت/نقش، شمارهٔ پرسنلی، خروجی فهرست و `GET /me/permissions` وجود ندارد؛ UI برای آن‌ها ساخته نشد. فهرست `memberships` صفحه‌بندی ندارد. ایجاد کاربر در A3.
- بازبینی مستقل: `code-reviewer` ۰ مسدودکننده و ۰ major؛ موارد minor مورد تأیید (debounce پس از back/forward، انتقال focus پس از ناوبری، `text-start` در لینک منو، memo مقدار context) در همین PR اصلاح شد (روتر: focus به h1 پس از تغییر مسیر، `useDocumentTitle`؛ هر حالت صفحهٔ جزئیات h1 دارد).
- یادداشت `security-reviewer` (سطح Low): عبارت جستجو در `?q=` در URL می‌ماند و می‌تواند ایمیل/موبایل باشد. ریسک پذیرفته‌شده است: صفحه فقط برای مدیر است و `Referrer-Policy` برابر `strict-origin-when-cross-origin` است. اگر ثبت Query String در لاگ درخواست‌ها فعال شد، بازبینی شود.

## یادداشت برای هماهنگ‌کننده
- `docs/WORK_LOG.md`: یک ردیف برای #462 (روتر وصل شد، لیست/جزئیات کاربران، اصلاح `offline-check` برای chunk تنبل).
- `docs/UI_COMPONENTS.md` (بخش A1): افزودن `useSearchParams`، الگوی «حالت لیست در URL»، `AppRoutes.tsx` و اینکه `AppShell` مالک `RouterProvider` است؛ ذکر اینکه `offline-check` اکنون chunkهای تنبل را درست حل می‌کند. `docs/TESTING.md` اگر محدودیت‌های `offline-check` را شرح می‌دهد، همین مورد را اضافه کند.

## شناسه‌های Git
- Branch: `agent/462-users-list-detail`
- Commit: ثبت نشده (طبق دستور)
- Pull Request: ایجاد نشده
