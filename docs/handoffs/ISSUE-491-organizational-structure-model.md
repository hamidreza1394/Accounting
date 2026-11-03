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
