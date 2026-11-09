# تحویل وظیفه — UNNUMBERED-ui-sidebar-system-menus

- **Issue:** UNNUMBERED (مأموریت تکمیل رابط مدیریت سیستم، PR شماره ۱ از ۴)
- **Agent/مسئول:** Claude Code (Lead) + frontend-developer + code-reviewer
- **تاریخ:** 2026-11-08
- **وضعیت:** انجام‌شده (اجرای Playwright/E2E انجام نشد)

## هدف و معیارهای پذیرش
- [x] قاب بیرونی پیوسته و ظریف برای سایدبار؛ Scroll فقط داخل منو
- [x] کادر تمام‌عرض برای گروه‌های اصلی با Radius و فاصلهٔ یکنواخت
- [x] حذف کامل دکمه و حالت «جمع‌کردن منو» (باز/بسته‌شدن گروه‌ها و بخش‌ها حفظ شد)
- [x] آیتم فعال: پس‌زمینهٔ روشن‌تر، نوار طلایی لبهٔ شروع، متن Bold، `aria-current`
- [x] تغییر نام «ساختار سازمانی و کارکنان» به «ساختار سازمانی» با آیکون‌های `Network`، `Building2`، `BriefcaseBusiness`
- [x] Logout در انتهای سایدبار با همان `requestLogout` موجود
- [ ] تأیید بصری در ۷۶۸/۱۰۲۴/۱۴۴۰ و Playwright (اجرا نشد)

## خلاصه تغییرات
- بلوک واحد CSS سایدبار در `premium-dashboard.css`؛ لایه‌های collapsed/rail از `index.css` حذف شد.
- `AppSidebar.tsx`: حذف `collapsed`/`onToggleCollapsed`؛ افزودن `onLogout`/`loggingOut`؛ آیکون زیرمنوها تک‌رنگ.
- `NestedNavSections.tsx`: گروه‌بندی بخش‌ها بر اساس نام بخش هر آیتم، `sectionIcon`، باز شدن خودکار بخش فعال و امکان بستن دستی.
- اصلاح بازبینی مستقل: `sectionIcon` روی همهٔ آیتم‌های هر بخش (مجوز جزئی)، عرض drawer موبایل `min(300px, 86vw)`، حذف padding متعارض قدیمی.

## فایل‌های مهم تغییرکرده
- `ClientApp/src/components/shell/AppSidebar.tsx`، `NestedNavSections.tsx` و تست‌هایشان
- `ClientApp/src/components/AppShell.tsx` (+ تست)
- `ClientApp/src/styles/premium-dashboard.css`، `index.css`، `mobileSidebar.test.ts`
- `tests/e2e/specs/faranou.spec.mjs` — `exact: true` برای دکمهٔ «خروج» تا با «خروج از حساب» تداخل نکند (اجرا نشده)
- `docs/UI_COMPONENTS.md`، `docs/MENU_FORM_CATALOG.md`

## تصمیم‌های فنی
- «کارکنان» (`/hr/org/employees`) صفحهٔ واقعی است و زیر «ساختار سازمانی» ماند؛ مورد خارج از دو زیرمنوی تصریح‌شده است.
- صفحهٔ «مجوزهای دسترسی» در PR3 اضافه می‌شود؛ آیتم‌های «گروه‌های دسترسی» و «کاتالوگ» فعلاً حفظ شده‌اند.
- بقیهٔ آیتم‌های واقعی (کاربران شرکت، درخواست‌های عضویت، گزارش امنیت) حذف نشدند.
- تصمیم معماری جدیدی گرفته نشد؛ وابستگی جدید اضافه نشد.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| `npm run typecheck` | موفق | |
| `npx vitest run` (کل) | ۶ شکست از قبل در `main` (وابسته به منطقه زمانی/محیط)، شکست جدید ندارد | AuditTimeline، UsersListPage، UserDetailPage، SecurityAuditPage (۲)، securityAuditClient |
| تست‌های shell/AppShell/styles | موفق (۸۰/۸۰ پس از اصلاحات بازبینی) | |
| `npm run build` | موفق (پس از اصلاحات بازبینی) | |
| `npm run check:offline`، `check_static_assets.py` | موفق (پس از اصلاحات بازبینی) | |
| Playwright / E2E | اجرا نشد | نیازمند سرور و SQL Server |
| بررسی بصری | فقط ۱۲۸۰px و ۳۶۰px با Chrome headless روی HTML رندرشده | ۷۶۸/۱۰۲۴/۱۴۴۰ اندازه‌گیری نشد |

## امنیت، داده و سازگاری
- فقط UI؛ مجوزدهی سمت سرور و منطق نمایش آیتم‌ها بر اساس مجوز بدون تغییر. بدون Migration.

## ریسک‌ها و کارهای باقی‌مانده
- بدهی CSS قدیمی (`.faranou-sidebar--light` و بلوک‌های `.faranou-nav-active` در `index.css`) باقی است.
- `Tooltip.tsx` پس از حذف Rail مصرف‌کننده ندارد.
- جدول `MENU_FORM_CATALOG.md` از قبل با کد واقعی فاصله داشت و بازنویسی نشد.
- `onLogout` کشوی موبایل را نمی‌بندد.

## شناسه‌های Git
- Branch: `feat/ui-sidebar-system-menus`
- Commit: در گزارش PR
- Pull Request: در گزارش PR
