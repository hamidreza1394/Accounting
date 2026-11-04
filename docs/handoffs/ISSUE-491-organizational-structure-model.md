# تحویل وظیفه — ISSUE-491-organizational-structure-model

- Issue: #491 (زیرمجموعه #444، S2)
- مسئول: ChatGPT — data model / architecture audit
- وضعیت: پیشنهاد آماده بازبینی؛ نیازمند تصمیم مالک پیش از migration

## یافته‌ها
- User هویت ورود نصب است؛ UserCompanyMembership عضویت عملیاتی و نقش شرکت.
- Domain فاقد Employee، OrganizationalUnit و Position مستقل است.
- User ممکن است در چند شرکت عضویت با سمت‌های متفاوت داشته باشد.
- D-064 مقرر می‌کند سمت و واحد داده HR باشند و مجوز ایجاد نکنند.

## پیشنهاد
دو گزینه مستند شده‌اند:
A. واحد و سمت اختیاری روی UserCompanyMembership؛ ساده اما فقط برای کاربران دارای عضویت.
B. CompanyEmployee مستقل و company-scoped با لینک اختیاری one-to-one به UserCompanyMembership؛ واحد و سمت مستقل، کارمند بدون login مجاز. گزینه B توصیه می‌شود.

## تغییرات
- docs/ORGANIZATIONAL_STRUCTURE_DATA_MODEL.md: شواهد، گزینه‌ها، طرح مفهومی، API boundary، security, migration/backfill/tests.
- docs/ACCESS_CONTROL_ARCHITECTURE.md و docs/USER_ACCESS_MANAGEMENT.md: ثبت وضعیت S2.
- docs/INDEX.md: ارجاع.
- بدون کد Production، API، Entity، Schema، Migration یا ClientApp.

## تأیید موردنیاز
- [ ] گزینه A یا B
- [ ] امکان ثبت Employee بدون User/Login
- [ ] آیا Employee باید یک یا چند عضویت/حساب را بتواند لینک کند؟ پیشنهاد: حداکثر یک membership در همان شرکت.
- [ ] آیا Position فقط Company-scoped باشد یا Position می‌تواند چند Unit را پوشش دهد؟ پیشنهاد: Company-scoped مستقل از Unit.

تا دریافت تصمیم، پیاده‌سازی Schema انجام نمی‌شود؛ هیچ Migration مشتری اجرا نمی‌شود.

---

## به‌روزرسانی 2026-11-05 — نهایی‌سازی طراحی سطح پیاده‌سازی (Z.ai / GLM)

- **وضعیت:** طراحی کامل شد؛ **پیاده‌سازی Schema/API همچنان مسدود است** چون معیار پذیرش Issue («Owner confirms option A or B») هنوز برآورده نشده و هیچ تأیید صریح مالک در Issue، ADR یا مستندات ثبت نشده بود. طبق قاعده «تأیید از سکوت/پیشنهاد قبلی استنباط نمی‌شود»، فقط بخش طراحی و مستندات انجام شد.
- **افزوده‌ها:**
  - `docs/ORGANIZATIONAL_STRUCTURE_DATA_MODEL.md` از «طرح مفهومی» به «طرح پیاده‌سازی» ارتقا یافت: چهار Entity (`CompanyOrganizationalUnit`، `CompanyPosition`، `CompanyEmployee`، `CompanyEmployeeAssignment` با انتساب مستقل و Effective Dating)، کلیدها/Unique Indexها/FKهای ترکیبی tenant-safe، قواعد چرخه والد و چرخه مدیریتی، هم‌پوشانی انتساب فعال با Unique فیلترشده، کاتالوگ ۱۲ کد مجوز `hr.*`، قرارداد کامل API و برنامه تست.
  - ADR **D-072 (Proposed)** در `docs/DECISIONS.md` ثبت شد؛ `INDEX.md`، `ACCESS_CONTROL_ARCHITECTURE.md`، `USER_ACCESS_MANAGEMENT.md` و `FRONTEND_API_CONTRACT_NEEDS.md` هم‌تراز شدند.
- **تغییر نسبت به پیشنهاد قبلی:** انتساب واحد/سمت/مدیر از فیلد ثابت روی `CompanyEmployee` به رکورد تاریخچه مستقل (`CompanyEmployeeAssignment`) منتقل شد تا Effective Dating، عدم بازنویسی تاریخچه و کنترل هم‌پوشانی ممکن شود.
- **منتظر تصمیم مالک (مسدودکننده):** چهار پرسش بند «تأیید موردنیاز» همین سند؛ پس از ثبت پاسخ در Issue #491، ترتیب اجرا: Migration فقط-داده‌ای کاتالوگ `hr.*` → Migration ساختاری additive زنجیره‌ای بعد از `20261104174711` → سرویس/API → تست‌ها (Unit/Integration/SQL Server واقعی/E2E) → UI.
- **مرزبندی:** بدون کد Production، Migration، تغییر مجوز یا ClientApp؛ هیچ داده‌ای تغییر نکرده است.
