# تحویل وظیفه — ISSUE-488-fiscal-grant-model-audit

- **Issue:** #488 (زیرمجموعه #444)
- **مسئول:** ChatGPT — data model / architecture audit
- **تاریخ:** 2026-11-03
- **وضعیت:** ممیزی و پیشنهاد آماده بازبینی؛ نیازمند تأیید معنای سال/دوره پیش از schema implementation

## شواهد کد
- Domain.FiscalPeriod تنها موجودیت مرتبط است و TenantId, StartDate, EndDate, Title, Status دارد.
- Unique index فعلی: (TenantId, StartDate, EndDate).
- Domain فاقد FiscalYear و AccountingPeriod مستقل است.
- ClientApp/src/lib/fiscalYears.ts رکوردهای FiscalPeriod شرکت‌های مختلف را با StartDate/EndDate برابر در یک سال نمایشی گروه می‌دهد.
- تست fiscalYears.ts عنوان «سال مالی ۱۴۰۵» و بازه یک‌ساله را به‌کار می‌برد.

## مسئله
D-065 هر دو سطح Year grant و Period grant را الزام می‌کند و برای سال دو policy متفاوت ExplicitPeriodsOnly و AllPeriodsInYear می‌خواهد. مدل فعلی دو target پایدار و مجزا ندارد. ساخت دو grant type بدون تعیین معنای دوره داخل سال می‌تواند fail-open یا fail-closed ناخواسته بسازد.

## پیشنهاد D-066
- گزینه ترجیحی: FiscalPeriod موجود نماینده سال مالی باشد؛ FiscalYear table جدید نسازیم.
- در صورت نیاز قطعی کسب‌وکار، AccountingPeriod فرزند برای ماه/فصل/دوره عملیاتی اضافه شود.
- Year grant به FiscalPeriod و policy آن متصل باشد.
- Period grant به AccountingPeriod مشخص متصل شود.
- هر دو grant به UserCompanyMembership تأییدشده و همان Tenant محدود شوند.
- AllPeriodsInYear دوره‌های موجود و آینده را پوشش دهد؛ ExplicitPeriodsOnly دوره جدید را ضمنی مجاز نکند.
- گزینه جایگزین: اگر FiscalPeriod از نظر کسب‌وکار خود دوره عملیاتی است، سال فقط grouping نمایشی باشد؛ باید قواعد period creation و future-period behavior صریح شوند.

## تغییرات
- docs/DECISIONS.md: D-066 Proposed.
- docs/ACCESS_CONTROL_ARCHITECTURE.md: ثبت ممیزی و عدم وجود دو target مستقل.
- docs/USER_ACCESS_MANAGEMENT.md: ثبت blocker پیش از grant implementation.
- docs/INDEX.md: ارجاع handoff.
- هیچ کد Production، Entity، API، Schema، Migration یا ClientApp تغییر نکرد.

## معیارهای ادامه
- [ ] مالک محصول معنا و گزینه A/B را تأیید کند.
- [ ] پس از تأیید، طرح کلیدها، composite FK، یکتایی، lifecycle/revocation، backfill، concurrency، SQL Server tests و rollback در PR مستقل ارائه شود.
- [ ] هیچ grant پیش‌فرضی برای کاربران موجود ایجاد نشود.
- [ ] تست ثابت کند Pending membership و نبود grant هر دو deny می‌شوند.
- [ ] AllPeriodsInYear و ExplicitPeriodsOnly برای دوره‌های آینده به‌طور مستقل تست شوند.

## ریسک
بدون روشن شدن معنای FiscalPeriod و AccountingPeriod، migration زدن برای FiscalAccessGrant توصیه نمی‌شود. این PR عمداً فقط تصمیم و شواهد را ثبت می‌کند.
