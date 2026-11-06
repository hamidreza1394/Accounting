# تحویل ادغام — Frontend منابع انسانی (HR Organizational UI)

- **Issue:** UNNUMBERED (مأموریت Frontend منابع انسانی فرانو)
- **Agent/مسئول:** عامل Frontend HR (مستقل از عامل موازیِ مدیریت دسترسی)
- **تاریخ:** 2026-11-06
- **وضعیت:** انجام‌شده — ادغام در پوسته انجام شد (بخش «نتیجهٔ ادغام در پوسته» در انتهای سند؛ شاخهٔ `feature/hr-frontend-integration`)

## هدف و معیارهای پذیرش

- [x] UI کامل واحدهای سازمانی (درخت + فهرست + جزئیات + ایجاد/ویرایش + غیرفعال‌سازی)
- [x] UI کامل سمت‌ها (فهرست + ایجاد/ویرایش + دارندگان سمت + تفکیک از گروه دسترسی)
- [x] UI کامل کارکنان (فهرست/فیلتر، ثبت بدون حساب، پروندهٔ تب‌ها، انتساب/انتقال/پایان، تاریخچه)
- [x] اتصال اختیاری کارمند به عضو فعال شرکت (بدون ساخت حساب خودکار)
- [x] حفظ و ارسال rowVersion و مدیریت صریح 409
- [x] پارس قرارداد واقعی DTOها با خطای سخت روی ناهمخوانی
- [x] RTL کامل، تاریخ شمسی (D-068)، توکن‌های طراحی فرانو، بدون وابستگی جدید
- [x] تست: ۹۲ تست HR + سوییت کامل ۶۳۸ تست سبز؛ typecheck/build/check:offline سبز
- [x] هیچ فایل مالکیت عامل موازی (Claude) تغییر نکرده است (بخش «مرزبندی»)

## خلاصه تغییرات

ميزبانی کامل ماژول HR در `ClientApp/src/features/hr/` (لایه API، تایپ‌ها/پارسرها، اعتبارسنجی، ۷ کامپوننت، ۱۰ صفحه، جدول مسیرها، فیکسچر و تست‌های colocated) به‌همراه دو سند: `ClientApp/src/features/hr/README.md` و همین تحویل.

## فایل‌های مهم تغییرکرده

- جدید — `ClientApp/src/features/hr/**` (کل ماژول؛ فهرست کامل در README همان پوشه)
- جدید — `docs/handoffs/HR_FRONTEND_INTEGRATION.md` (همین سند)
- به‌روزرسانی — `docs/WORK_LOG.md` (افزودن خلاصه در انتهای فایل، بدون بازنویسی سابقه)

## تصمیم‌های فنی

- **بدون تغییر فایل‌های مشترک:** طبق مرزبندی، `AppRoutes.tsx`، `App.tsx`، `AppShell.tsx`، `lib/permissions.ts`، `api/client.ts` و صفحات مدیریت دسترسی تغییر نکردند. `hrRoutes.tsx` جدول مسیرهای آمادهٔ ادغام را صادر می‌کند و `AppRouteDataContext` (فقط import) را مصرف می‌کند.
- **عدم افزودن وابستگی:** درخت سازمانی با لیست تو در تو بومی (نه کتابخانهٔ visualization)، انتخابگرها با input جستجو + select بومی؛ هم‌راستا با D-049/D-050 و قانون آفلاین.
- **کد به‌عنوان منبع حقیقت:** DTOها از `HrOrganizationalStructureEndpoints.cs` استخراج و با `docs/HR_FRONTEND_API_CONTRACT.md` تطبیق داده شدند؛ پارسرها روی هر ناهمخوانی `HrContractError` می‌دهند.
- **معنای null در PUT واحد و کارمند** مستقیماً از کد سرور استخراج و در UI hint شد (null والد = ریشه؛ null validFrom = حفظ؛ null validTo = پاک‌کردن؛ null hiredOn = حفظ).
- تصمیم معماری جدیدی (ADR) گرفته نشد.

## دستورالعمل ادغام (PR بعدی — مالک پوستهٔ مشترک)

1. **مسیرها** — در `ClientApp/src/components/AppRoutes.tsx` پس از `appRoutes`:
   ```ts
   import { hrRoutes } from '../features/hr/hrRoutes';
   export const appRoutes: readonly Route[] = [ ...existingRoutes, ...hrRoutes ];
   ```
   (ترتیب `/new` قبل از `/:id` داخل خود `hrRoutes` حفظ شده است.)
2. **ناوبری** — در `AppShell.tsx` زیر گروه «مدیریت سیستم» (الگوی موجود `companyPermissions.includes('users.list')`):
   - «واحدهای سازمانی» → `/hr/org/units` با شرط `companyPermissions.includes('hr.organizational-units.list')`
   - «سمت‌های سازمانی» → `/hr/org/positions` با شرط `companyPermissions.includes('hr.positions.list')`
   - «کارکنان» → `/hr/org/employees` با شرط `companyPermissions.includes('hr.employees.list')`
   (تلمیح UI فقط؛ سرور مرجع است — D-064.)
3. **(اختیاری، تصمیم مالک)** افزودن کدهای `hr.*` به union `Permission` در `lib/permissions.ts` برای hint مسیرها؛ تا آن زمان مسیرهای HR بدون `permission` hint مانند `/company/users` عمل می‌کنند و سرور تصمیم می‌گیرد.
4. پس از ادغام: `npm ci && npm run typecheck && npx vitest run && npm run build && npm run check:offline` و بازبینی RTL در سه عرض ۳۶۰/۷۶۸/۱۲۸۰.

## بررسی و آزمون (نتایج واقعی)

| فرمان/آزمون | نتیجهٔ واقعی | توضیح |
|---|---|---|
| `npm run typecheck` | موفق | صفر خطا |
| `npx vitest run src/features/hr` | موفق — ۹۲/۹۲ | ۱۱ فایل تست |
| `npx vitest run` (کل) | موفق — ۶۳۸/۶۳۸ | ۶۸ فایل؛ هیچ تستی حذف/تضعیف نشد |
| `npm run build` | موفق | شامل typecheck؛ خروجی در `Server/wwwroot/app` (untracked) |
| `npm run check:offline` | موفق | ۳۶ منبع محلی، بدون منبع بیرونی |
| `dotnet build/test`، SQL Server، E2E | اجرا نشده | خارج از محدودهٔ این PR (فقط Frontend؛ هیچ فایل Backend تغییر نکرده) |
| مرورگر واقعی / اسکرین‌شات | اجرا نشده | محیطsandbox مرورگر ندارد؛ پوشش رفتاری با Testing Library انجام شد و به بازبینی انسانی در سه عرض موکول شد |

## امنیت، داده و سازگاری

- Authorization صددرصد سمت سرور می‌ماند؛ کدهای `hr.*` فقط تلمیح نمایشی‌اند. هیچ عملیات HR مجوز/نقش نمی‌دهد (D-064/D-072).
- Tenant Isolation: همهٔ فراخوانی‌ها با companyId زمینهٔ کاری و فقط در مسیر؛ پاسخ‌های 404/403 بدون افشای وجود.
- بدون ذخیرهٔ دادهٔ حساس در localStorage/گزارش؛ فقط فیلدهای Allowlist تاریخچه نمایش داده می‌شود.
- هیچ تغییر Migration/Backend/قرارداد API وجود ندارد.

## ریسک‌ها و کارهای باقی‌مانده

- ثبت مسیرها/منو (بخش «دستورالعمل ادغام») باید در PR Integration انجام شود؛ تا قبل از آن صفحات HR از URL `/app/hr/...` در اپ build‌شده دیده نمی‌شوند.
- جستجوی کاربر برای اتصال حساب به مجوز `users.list` وابسته است؛ در صورت نیاز، تصمیم بک‌اند برای نقطهٔ پایانی جستجوی مخصوص HR جداگانه است.
- نمایش نام مدیر نیازمند غنی‌سازی DTO انتساب در بک‌اند است (الان فقط شناسه).
- بازبینی بصری انسانی (اسکرین‌شات/مرورگر) و اجرای E2E پس از ادغام مسیرها.

## شناسه‌های Git

- Branch: `feature/hr-organizational-ui` (از `main` = `8726fc2a4b00980dc8e0bfd3ded0f3ab7e20c364`)؛ پس از پیشروی main (PR #554)، هم‌ترازی با `main` جدید = `553405d` با commit merge `04dcfe5` انجام شد و هر دو رکورد WORK_LOG حفظ شد.
- Commit اصلی: `c59a9dc1dd548c2f6479dd8223a1cb6323fe47c7`
- Pull Request: [#555 — feat(hr): implement organizational management frontend](https://github.com/hamidreza1394/Accounting/pull/555) — به `main`؛ **merge توسط مالک**
- CI روی commit اصلی: همهٔ Checkها completed بدون failure (شامل `client` و `build-and-test` با SQL Server واقعی)؛ پس از هم‌ترازی با main، CI دوباره اجرا و در گزارش نهایی ثبت می‌شود.


## نتیجهٔ ادغام در پوسته (2026-11-06، شاخهٔ `feature/hr-frontend-integration`)

### مسیرهای ثبت‌شده
`AppRoutes.tsx` اکنون `hrRoutes` را بعد از مسیرهای مشترک به `appRoutes` می‌افزاید (۱۰ مسیر، ترتیب `/new` قبل از `/:id` حفظ شد). `features/hr/**` دست نخورد. هر مسیر یک `permission` hint از خانوادهٔ خودش می‌گیرد (`/hr/org/units*` ← `hr.units.view`، `/hr/org/positions*` ← `hr.positions.view`، `/hr/org/employees*` ← `hr.employees.view`) تا روتر در حالت loading/error صفحه را نشان ندهد و درخواست HR نفرستد (fail-closed).

| مسیر (زیر `/app`) | hint |
|---|---|
| `/hr/org/units`، `/units/new`، `/units/:unitId` | `hr.units.view` |
| `/hr/org/positions`، `/positions/new`، `/positions/:positionId` | `hr.positions.view` |
| `/hr/org/employees`، `/employees/new`، `/employees/:employeeId`، `/employees/:employeeId/edit` | `hr.employees.view` |

### ورودی‌های منوی «مدیریت سیستم»
«واحدهای سازمانی»، «سمت‌های سازمانی» و «کارکنان» با `aria-current` برای مسیرهای تودرتو. آیتم غیرفعال قبلی «ساختار سازمانی و سمت‌ها» (placeholder) حذف شد چون این سه آیتم واقعی جایگزین آن هستند؛ تست‌های `AppShell.test.tsx` که روی آن placeholder بودند به placeholder باقی‌مانده (`سیاست‌های امنیتی`) منتقل شدند (همان ادعاها، بدون حذف تست).

### کدهای مجوز تأییدشده
در `Server/Api/HrOrganizationalStructureEndpoints.cs` وجود دارند: `hr.organizational-units.list`، `hr.positions.list`، `hr.employees.list` (و `...create/update`، `hr.employees.view/create/update/user-link.manage`، `hr.assignments.manage`). برای `Permission` در `lib/permissions.ts` فقط سه hint اضافه شد که به همین سه کد list نگاشت می‌شوند؛ مدیر نصب مثل سرور (`AuthorizeHrScopeAsync`) همه را می‌گیرد. کد جدیدی ساخته نشد.

### رفتار زمینهٔ شرکت
هیچ انتخابگر شرکت یا حافظهٔ جدیدی افزوده نشد. hintها از `access` همان `useCompanyPermissions` می‌آیند: با تغییر شرکت snapshot قبلی فوراً حذف و دوباره دریافت می‌شود، پاسخ دیررس شرکت قبلی نادیده گرفته می‌شود، خطا/پاسخ ناهمخوان هیچ دسترسی نمی‌دهد. `session.isAdministrator` استفاده نشده است.

### نتایج آزمون (واقعی)
| فرمان | نتیجه |
|---|---|
| `npx tsc --noEmit` | موفق |
| `npx vitest run src/HrIntegration.test.tsx` | ۲۲ از ۲۲ موفق (قبل از پیاده‌سازی ۱۸ ناموفق/۴ موفق) |
| `npx vitest run` (کل) | ۷۳ فایل، ۷۵۵ تست؛ همه موفق |
| `npm run build` | موفق |
| `npm run check:offline` | موفق (۵۷ منبع محلی) |
| `python3 scripts/check_static_assets.py` | موفق |
| `dotnet build/test`، Playwright E2E، مرورگر واقعی | اجرا نشد (SDK و SQL Server و مرورگر در محیط نیست؛ Backend تغییر نکرده) |

### محدودیت‌ها
- hint هر خانواده در سطح فهرست است (`*.list`)؛ دسترسی دقیق صفحات جزئیات/ایجاد/ویرایش (مثلاً `hr.employees.view`/`create`) را سرور تعیین می‌کند و صفحات 403 را صادقانه نشان می‌دهند.
- تغییر شرکت در سطح کل برنامه با تست جدا نشده؛ رفتار آن با تست‌های `useCompanyPermissions.test.tsx` (پاک‌شدن فوری، نادیده‌گرفتن پاسخ دیررس) پوشش داده شده است.
- بازبینی بصری RTL در سه عرض و E2E پس از ادغام لازم است.
