# تحویل وظیفه — ISSUE-488-fiscal-grant-model-audit

- **Issue:** #488 (زیرمجموعه #444)
- **مسئول:** ChatGPT — data model / architecture audit
- **تاریخ:** 2026-11-03
- **وضعیت:** معنای دامنه با تأیید مالک در 2026-11-08 حل شد؛ طراحی داده ثبت شد؛ پیاده‌سازی هنوز NotImplemented

## شواهد کد
- Domain.FiscalPeriod تنها موجودیت مرتبط است و TenantId, StartDate, EndDate, Title, Status دارد.
- Unique index فعلی: (TenantId, StartDate, EndDate).
- Domain فاقد FiscalYear و AccountingPeriod مستقل است.
- ClientApp/src/lib/fiscalYears.ts رکوردهای FiscalPeriod شرکت‌های مختلف را با StartDate/EndDate برابر در یک سال نمایشی گروه می‌دهد.
- تست fiscalYears.ts عنوان «سال مالی ۱۴۰۵» و بازه یک‌ساله را به‌کار می‌برد.

## مسئله
D-065 هر دو سطح Year grant و Period grant را الزام می‌کند و برای سال دو policy متفاوت ExplicitPeriodsOnly و AllPeriodsInYear می‌خواهد. مدل فعلی دو target پایدار و مجزا ندارد. ساخت دو grant type بدون تعیین معنای دوره داخل سال می‌تواند fail-open یا fail-closed ناخواسته بسازد.

## تصمیم نهایی D-066 (تأیید مالک، 2026-11-08)
- FiscalPeriod موجود نماینده سال مالی است؛ FiscalYear جدید ساخته نمی‌شود.
- AccountingPeriod فرزند FiscalPeriod برای ماه/فصل/دوره عملیاتی ایجاد می‌شود.
- تخصیص نقش از Membership جداست؛ CompanyRole در سطح شرکت باقی می‌ماند.
- تخصیص سالانه به FiscalPeriod متصل و دارای policy صریح ExplicitPeriodsOnly یا AllPeriodsInYear است؛ period selection به AccountingPeriod مشخص متصل می‌شود.
- عضویت Approved/Active شرط لازم است اما grant ایجاد نمی‌کند.
- AllPeriodsInYear دوره‌های فعلی و آینده را پوشش می‌دهد؛ ExplicitPeriodsOnly فقط دوره‌های انتخاب‌شده را مجاز می‌کند.
- مدل دقیق و migration strategy در docs/FISCAL_ACCESS_DATA_MODEL.md ثبت شد.

## تغییرات
- docs/DECISIONS.md: D-066 Proposed.
- docs/ACCESS_CONTROL_ARCHITECTURE.md: ثبت ممیزی و عدم وجود دو target مستقل.
- docs/USER_ACCESS_MANAGEMENT.md: ثبت blocker پیش از grant implementation.
- docs/INDEX.md: ارجاع handoff.
- هیچ کد Production، Entity، API، Schema، Migration یا ClientApp تغییر نکرد.

## معیارهای ادامه
- [x] مالک محصول گزینه A را تأیید کرد.
- [x] طرح کلیدها، composite FK، یکتایی، lifecycle/revocation، backfill، concurrency، SQL Server tests و rollback در docs/FISCAL_ACCESS_DATA_MODEL.md ثبت شد.
- [ ] پیاده‌سازی Entity/API/UI و Migration در PRهای مستقل.
- [ ] تست SQL Server و E2E واقعی، و اثبات deny-by-default.
- [ ] هیچ grant پیش‌فرضی برای کاربران موجود ایجاد نشود.
- [ ] تست ثابت کند Pending membership و نبود grant هر دو deny می‌شوند.
- [ ] AllPeriodsInYear و ExplicitPeriodsOnly برای دوره‌های آینده به‌طور مستقل تست شوند.

## ریسک
ابهام معنای FiscalPeriod رفع شده است؛ اما Migration هنوز باید پس از پیاده‌سازی افزایشی و تست SQL Server در PR اجرایی جداگانه ساخته شود. این سند فقط تصمیم و طراحی داده را مستند می‌کند.
