# تحویل وظیفه — ISSUE-444-membership-requests-ui

- **Issue:** #444 (مورد «ز»/۶ گزارش تقسیم کار: UI گردش درخواست عضویت بین‌شرکتی)
- **Agent/مسئول:** Claude Code (UI)؛ Backend و API مالک ChatGPT
- **تاریخ:** 2026-11-03
- **وضعیت:** بخش قابل‌انجام با API موجود تکمیل شد؛ «ثبت درخواست جدید» مسدود به API Backend

## هدف و معیارهای پذیرش
- [x] کاربر درخواست‌های عضویت خودش و وضعیت Pending/Approved/Rejected را ببیند (تاریخ، گروه تأییدشده، دلیل رد).
- [x] مدیر مجاز شرکت مقصد صف درخواست‌های در انتظار را ببیند، با انتخاب گروه دسترسی تأیید کند یا با دلیل اختیاری رد کند.
- [x] هیچ دسترسی‌ای قبل از تأیید ساخته یا القا نشود؛ پیام صریح در صفحه؛ تأیید خود ممنوع (پیام سرور نمایش داده می‌شود).
- [x] گروه انتخابی فقط از گروه‌های فعال همان شرکت (از `GET /admin/companies/{id}/roles`)؛ گروه سیستمی با برچسب «سیستمی».
- [ ] فرم ثبت درخواست عضویت — **مسدود** (بخش ۴).

## خلاصه تغییرات
- `api/types.ts` و `api/client.ts`: انواع و parserهای سخت‌گیر و چهار تابع: `getMyMembershipRequests`، `getPendingMembershipRequests`، `approveMembershipRequest`، `rejectMembershipRequest` (anti-forgery، encode شناسه‌ها).
- `components/membership/`: `MyMembershipRequestsPage`، `PendingMembershipRequestsPage`، `MembershipRequestStatusBadge`.
- مسیرها در `AppRoutes.tsx` و دو لینک منو (زیر «مدیریت سیستم»، فقط وقتی `isAdministrator`).
- رفتار کلیدی: 404 صف = «دسترسی ندارید» (سرور وجود صف را فاش نمی‌کند)؛ 409/404 هنگام تصمیم = پیام سرور + بارگذاری مجدد صف؛ درخواست‌کنندهٔ غیرفعال قابل تأیید نیست اما قابل رد است؛ دکمه‌ها `aria-label` با نام متقاضی؛ ConfirmDialog با فوکوس‌تله و جلوگیری از ارسال دوباره.

## فایل‌های مهم
- `ClientApp/src/api/{types,client}.ts`, `ClientApp/src/api/membershipClient.test.ts`
- `ClientApp/src/components/membership/*`, `ClientApp/src/components/{AppRoutes,AppShell}.tsx`, `ClientApp/src/AppRouting.test.tsx`
- `ClientApp/src/test/membership.ts`, `docs/ADMIN_UI.md`

## تصمیم‌های فنی
- تصمیم معماری جدیدی گرفته نشد؛ سمت سرور هیچ تغییری نکرد؛ Entity/API موازی ساخته نشد؛ هیچ داده ساختگی در UI نیست.
- مسیر `/admin/membership-requests` عمداً `permission` ندارد: مجوز تأیید برای هر شرکت جداست و در session نیست؛ سرور تصمیم می‌گیرد (D-064). منوی آن تا آمدن `GET /me/permissions` فقط برای مدیر نصب دیده می‌شود؛ تأییدکنندهٔ غیرمدیر با نشانی مستقیم می‌رسد.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| `npm run typecheck` | موفق | |
| `npm test` (vitest) | موفق: ۴۴ فایل، ۴۳۱ تست | ۲۳ تست جدید (قرارداد، دو صفحه، مسیریابی) |
| `npm run build` و `npm run check:offline` | موفق | بدون منبع خارجی |
| axe / صفحه‌خوان / Playwright | **اجرا نشد** | axe در پروژه نصب نیست؛ QA مرورگر انجام نشد |
| E2E با Backend و SQL Server واقعی | **اجرا نشد** | |

## امنیت، داده و سازگاری
- UI فقط نمایش و فراخوانی API است؛ همهٔ قواعد (مجوز، self-approval، گروه متعلق به شرکت، عضویت فعال) سمت سرور enforce می‌شوند و UI فقط پیام آن‌ها را نشان می‌دهد.
- بدون وابستگی جدید، بدون CDN، بدون Migration.

## ۴) قرارداد API مورد نیاز از Backend (ChatGPT) — بدون آن این دو بخش ناقص می‌مانند
1. **کشف شرکت‌های قابل‌درخواست** (برای فرم ثبت درخواست): مثلاً `GET /api/v1/me/membership-requests/destinations` → `[{ companyId, name }]` فقط برای شرکت‌هایی که طبق D-065 قابل درخواست‌اند (هم‌گروه) و کاربر عضویت فعال یا درخواست Pending در آن ندارد؛ بدون افشای شرکت‌های بیرون از دامنهٔ مجاز. `POST /me/membership-requests { destinationCompanyId }` از قبل هست.
2. **گروه‌های قابل‌تخصیص برای تأییدکننده**: `GET /api/v1/admin/companies/{companyId}/membership-requests/assignable-roles` → `[{ id, code, name, isSystemRole }]` با همان مجوز `users.membership.approve` (بدون گروه‌های غیرفعال و بدون افشای مجوزهای گروه). امروز `GET .../roles` فقط مدیر نصب است و تأییدکنندهٔ شرکتی پیام «نیاز به API» می‌بیند.
3. (پیشنهادی) `GET /me/permissions` برای نمایش درست منو به تأییدکنندهٔ غیرمدیر.

## ریسک‌ها و کارهای باقی‌مانده
- فرم ثبت درخواست (پس از مورد ۱)، تأیید برای تأییدکنندهٔ غیرمدیر (پس از مورد ۲)، E2E مثبت با SQL Server واقعی، axe/صفحه‌خوان.

## شناسه‌های Git
- Branch: `feat/444-membership-requests-ui`
- Pull Request: در ادامه ثبت می‌شود
