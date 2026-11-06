# ماژول Frontend منابع انسانی (HR) — `ClientApp/src/features/hr/`

- **مأموریت:** پیاده‌سازی Frontend مدیریت سازمانی HR (S2 — D-072) برای فرانو.
- **وضعیت:** پیاده‌سازی کامل و تست‌شده در شاخه `feature/hr-organizational-ui`؛ ثبت مسیرها در پوستهٔ مشترک به PR ادغام بعدی موکول است (مرزبندی توسعهٔ موازی).
- **منبع حقیقت قرارداد:** `docs/HR_FRONTEND_API_CONTRACT.md` (تأییدشده در PR #547) و کد `Server/Api/HrOrganizationalStructureEndpoints.cs`. هیچ Endpoint، فیلد یا رفتاری حدس زده نشده است.

## ۱. نمای معماری

```
features/hr/
├── api/
│   ├── hrTypes.ts        # DTOهای واقعی بک‌اند + پارسرهای سخت‌گیر (HrContractError)
│   ├── hrTypes.test.ts
│   ├── hrClient.ts       # توابع API؛ خواندن با hrGet هم‌مبدأ/no-store، نوشتن با sendJson مشترک (anti-forgery)
│   └── hrClient.test.ts
├── lib/
│   ├── hrLabels.ts       # برچسب‌های فارسی وضعیت‌ها/رویدادها + الگوها و سقف‌های سمت سرور
│   ├── hrValidation.ts   # اعتبارسنجی آینه‌ای قواعد سرور + سازنده‌های بدنهٔ درخواست فیلدبه‌فیلد
│   ├── hrValidation.test.ts
│   └── hrFormat.ts       # نمایش لحظه‌های *Utc با asUtcInstant (قرارداد زمان پروژه)
├── components/
│   ├── HrBadges.tsx          # نشان وضعیت اشتغال/انتساب/فعال (همیشه آیکون + متن)
│   ├── HrSearchSelect.tsx    # انتخابگر جستجودار سرور-محور برای داده‌های بزرگ
│   ├── HrUnitTree.tsx        # درخت سازمانی با aria-expanded و پیوند به جزئیات
│   ├── HrUnitParentPicker.tsx# انتخاب والد با حذف زیردرخت خود واحد (پیشگیری چرخه)
│   ├── HrAssignmentSection.tsx# انتساب جاری + شروع/انتقال اتمی/پایان انتساب
│   ├── HrUserLinkSection.tsx # اتصال اختیاری کارمند به عضو فعال شرکت
│   └── HrHistoryList.tsx     # تاریخچه Audit با Allowlist شش‌فیلدی
├── pages/                # ۱۰ صفحه (جدول پایین)
├── hrRoutes.tsx          # جدول مسیرهای lazy برای ادغام در AppRoutes.tsx
└── test/                 # فیکسچرها + ابزار رندر با روتر واقعی
```

اصل جداسازی: لایه API / DTO / hooks و منطق فرم / کامپوننت نمایش / صفحه. منطق دامنه در UI نیست؛ سرور مرجع است (D-064) و پیام‌های `{ error }` سرور عیناً نمایش داده می‌شوند.

## ۲. صفحات و مسیرها (زیر `/app`)

| مسیر | صفحه | توضیح |
|---|---|---|
| `/hr/org/units` | HrUnitsPage | نمای درخت (`?view=tree` شامل غیرفعال‌ها) و فهرست تخت با جستجو/صفحه‌بندی |
| `/hr/org/units/new` | HrUnitCreatePage | کد/نام/توضیح/والد/بازهٔ اعتبار |
| `/hr/org/units/:unitId` | HrUnitDetailPage | اطلاعات + ویرایش (rowVersion) + زیردرخت با شمار انتساب فعال + کارکنان واحد + غیرفعال‌سازی |
| `/hr/org/positions` | HrPositionsPage | فهرست سمت‌ها؛ تصریح «سمت ≠ گروه دسترسی» |
| `/hr/org/positions/new` | HrPositionCreatePage | کد/نام/رتبهٔ اختیاری |
| `/hr/org/positions/:positionId` | HrPositionDetailPage | اطلاعات + ویرایش + دارندگان سمت + غیرفعال‌سازی |
| `/hr/org/employees` | HrEmployeesPage | جستجوی سمت‌سرور، فیلتر وضعیت اشتغال و واحد |
| `/hr/org/employees/new` | HrEmployeeCreatePage | چهار فیلد DTO واقعی؛ بدون اجبار به حساب کاربری |
| `/hr/org/employees/:employeeId` | HrEmployeeDetailPage | تب‌ها: ساختار سازمانی / حساب کاربری / اطلاعات پایه / تاریخچه رخدادها |
| `/hr/org/employees/:employeeId/edit` | HrEmployeeEditPage | ویرایش نام/وضعیت/تاریخ همکاری با rowVersion |

`/new` همیشه قبل از `/:id` ثبت شده است (ترتیب match روتر). هیچ فایل مشترکی (AppRoutes/AppShell/permissions.ts/…) تغییر نکرده است.

## ۳. نگاشت Endpointها (همه از قرارداد واقعی)

| عملیات | فراخوانی در hrClient | Permission سمت سرور |
|---|---|---|
| فهرست/جزئیات/ثبت/ویرایش/خاتمه کارمند | listHrEmployees، getHrEmployee، createHrEmployee، updateHrEmployee، deactivateHrEmployee | hr.employees.list / view / create / update |
| اتصال و جداسازی حساب | linkHrEmployeeUser، unlinkHrEmployeeUser | hr.employees.user-link.manage |
| انتقال اتمی و تاریخچه Audit | changeHrAssignment، getHrEmployeeHistory | hr.assignments.manage / hr.employees.view |
| واحدها: درخت/تخت/جزئیات/ثبت/ویرایش/غیرفعال‌سازی/زیردرخت/کارکنان | getHrUnitTree، listHrUnits، getHrUnit، createHrUnit، updateHrUnit، deactivateHrUnit، getHrUnitSubtree، listHrUnitEmployees | hr.organizational-units.list / create / update |
| سمت‌ها: فهرست/جزئیات/ثبت/ویرایش/غیرفعال‌سازی/داردگان | listHrPositions، getHrPosition، createHrPosition، updateHrPosition، deactivateHrPosition، listHrPositionEmployees | hr.positions.list / create / update |
| انتساب‌ها: فهرست/شروع/پایان | listHrAssignments، startHrAssignment، endHrAssignment | hr.employees.view / hr.assignments.manage |

### قواعد قراردادی رعایت‌شده در UI
- **rowVersion:** فقط از Detail خوانده و فقط در PUTها ارسال می‌شود؛ نبود/بدشکلی ۴۰۰ و منقضی‌شدن ۴۰۹ می‌دهد. در ۴۰۹، صفحه مجدد Detail را می‌خواند و فرم با توکن تازه باز می‌ماند.
- **معنای null در PUT واحد:** `parentUnitId: null` = ریشه؛ `validFromUtc: null` = حفظ (پاک‌کردن ممکن نیست)؛ `validToUtc: null` = پاک‌شدن. فرم ویرایش این معنای دوگانه را با hintهای فارسی نشان می‌دهد.
- **enumهای عددی:** وضعیت اشتغال ۱/۲/۳ و وضعیت انتساب ۱/۲ (هرگز رشته ارسال نمی‌شود).
- **تاریخ‌ها:** ورودی شمسی (JalaliDateField، D-068) → لحظهٔ UTC با `dayStartUtc`؛ نمایش با `formatHrInstant` (asUtcInstant).
- **قواعد تاریخ انتساب:** شروع آینده‌نگر و شروع قبل از `hiredOnUtc` سمت کلاینت رد می‌شود و پیام سرور نیز نمایش داده می‌شود؛ انتقال اتمی است و تاریخچه بازنویسی نمی‌شود.
- **خطاها:** 401 → UnauthenticatedError مشترک (هدایت به ورود)؛ 403/404 → پنل استاندارد «دسترسی ندارید/یافت نشد»؛ 409 تکراری/وضعیت → پیام فارسی سرور؛ 5xx → پیام عمومی.
- **Tenant Scope:** همهٔ درخواست‌ها زیر `/companies/{companyId}/org/*` با companyId زمینهٔ کاری تأییدشدهٔ سرور (WorkContext)؛ شناسهٔ شرکت هرگز از بدنه/کوئری به‌عنوان مجوز ارسال نمی‌شود.

## ۴. مجوزهای موردنیاز هر عملیات (تلمیح UI از `/me/permissions`)

- مشاهدهٔ واحدها/درخت/کارکنان واحد: `hr.organizational-units.list` · ایجاد: `...create` · ویرایش/غیرفعال‌سازی: `...update`
- مشاهدهٔ سمت‌ها/دارندگان: `hr.positions.list` · ایجاد/ویرایش: `hr.positions.create` / `hr.positions.update`
- فهرست کارکنان: `hr.employees.list` · جزئیات/تاریخچه: `hr.employees.view` · ثبت/ویرایش/خاتمه: `hr.employees.create` / `hr.employees.update`
- اتصال/جداسازی حساب: `hr.employees.user-link.manage` (+ `users.list` شرکت فقط برای جستجوی کاربر — محدودیت بخش ۶)
- شروع/انتقال/پایان انتساب: `hr.assignments.manage`

این کدها فقط دکمه‌ها/منو را پنهان می‌کنند؛ سرور مستقلاً هر درخواست را بررسی می‌کند و صفحات 403/404 را صادقانه نشان می‌دهند.

## ۵. تست‌ها و اجرا

```bash
cd ClientApp
npm ci
npm run typecheck          # سبز
npx vitest run src/features/hr   # ۹۲ تست HR
npx vitest run             # کل سوییت (۶۳۸ تست)
npm run build              # شامل typecheck
npm run check:offline      # بدون هیچ منبع بیرونی
```

پوشش: پارسرهای قرارداد (رد وضعیت/rowVersion نامعتبر)، پارامترهای کوئری دقیق، بدنهٔ DTO فیلدبه‌فیلد، anti-forgery، 401/403/404/409 و 400، حفظ rowVersion و بازخوانی پس از تعارض، معنای null در PUT واحد، جستجوی debounce، صفحه‌بندی، درخت (aria-expanded)، ثبت کارمند بدون حساب، اتصال/جداسازی حساب، شروع/انتقال/پایان انتساب، تاریخچهٔ Allowlist، مسدودی ویرایش کارمند خاتمه‌همکاری.

## ۶. محدودیت‌های شناخته‌شده (بدون ساخت قابلیت غیرواقعی)

1. **جستجوی کاربر برای اتصال حساب:** Backend نقطهٔ پایانی جستجوی کاربر مخصوص HR ندارد؛ انتخابگر از دایرکتوری شرکت (`GET /companies/{id}/users`) با مجوز `users.list` استفاده می‌کند. بدون آن مجوز، صفحه همین محدودیت را می‌گوید و فرم را نمایش نمی‌دهد.
2. **نام مدیر در انتساب:** DTO انتساب فقط `managerEmployeeId` برمی‌گرداند؛ UI شناسه را `dir=ltr` نشان می‌دهد. افزودن نام نیازمند تغییر قرارداد بک‌اند است (خارج از محدودهٔ Frontend).
3. **حذف `validFromUtc` و `hiredOnUtc`** از مسیر PUT ممکن نیست (قرارداد سرور)؛ UI این را hint می‌کند.
4. **کارمند خاتمه‌همکاری** قابل ویرایش/فعال‌سازی مجدد نیست (409/400 سرور)؛ UI فرم را باز نمی‌کند.
5. چانک‌های HR تا لحظهٔ ثبت مسیرها در `AppRoutes.tsx` در باندل lazy ورود نمی‌کنند — طبیعیِ مرزبندی توسعهٔ موازی؛ پس از ادغام، code-split مثل بقیهٔ صفحات اعمال می‌شود.
