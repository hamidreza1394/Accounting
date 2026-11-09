# تحویل وظیفه — UNNUMBERED-d078-implementation-status

- **Issue:** `UNNUMBERED` (مأموریت پیاده‌سازی D-078 — PR پنجم/پایانی سری)
- **Agent/مسئول:** GLM (Senior .NET Backend)
- **تاریخ:** 2026-11-10
- **وضعیت:** انجام‌شده — D-078 در سطح Backend `Implemented`؛ اقلام `NotVerified` صریح ثبت شدند

## نقشه سری PRهای D-078

| # | شاخه | محتوا |
|---|---|---|
| 1 | `feat/d078-users-create-permission` | seed فقط-داده `users.create` + تست SQL-gated |
| 2 | `feat/d078-company-user-provisioning` | Migration `20261110130000_MakeCompanyMembershipRoleOptional` + endpoint `POST /companies/{companyId}/users` + سازگاری دفترچه/ClientApp |
| 3 | `test/d078-membership-role-compat` | تست‌های سازگاری `roles.assign` با عضو بدون نقش + عدم دسترسی ضمنی |
| 4 | `test/d078-cross-company-hardening` | ماتریس سخت‌سازی بین‌شرکتی (مالکیت/گروه/self-approval/race/رد) |
| 5 | `docs/d078-implementation-status` | این PR: DECISIONS/USER_ACCESS_MANAGEMENT/ARCHITECTURE/SECURITY/INDEX/PROJECT_STATUS + پاک‌سازی نشانه‌های تعارض WORK_LOG |

PRها stacked هستند (هر PR بر شاخه قبلی)؛ هیچ PRی merge نشده است — تصمیم Merge و ترتیب با مالک است. اگر PR #623 (ثبت تصمیم مستنداتی D-078) هنوز باز است، بخش DECISIONS این سری با آن هم‌راستاست و ترتیب ادغام فقط یک تداخل کوچک متنی ایجاد می‌کند (متن این PR کامل‌تر است).

## وضعیت D-078 (D-030)

| قلم | وضعیت |
|---|---|
| تصمیم محصولی | `Accepted` (PR #623) |
| مجوز `users.create` + seed | `Implemented` |
| ایجاد کاربر + عضویت داخلی فعال (سناریوهای A/B/C) | `Implemented` |
| عضویت بدون نقش + fail-closed | `Implemented` |
| حفظ گردش تأیید بین‌شرکتی D-065 | `Implemented` (بدون تغییر + تست قفل) |
| تست‌های Endpoint (InMemory) | `Implemented`/سبز (۱۶ تست جدید در سه PR) |
| تست‌های SQL Server | در CI اجرا می‌شوند؛ در محیط تهیه این PR `NotRun` (Docker نبود) |
| UI فرم «ایجاد کاربر شرکتی» برای مدیر شرکت | `NotImplemented` (قرارداد API آماده؛ PR مستقل آینده) |
| E2E عملیاتی روی SQL Server/محیط منتشرشده | `NotVerified` |
| اجرای Migration روی دیتابیس مشتری | خارج از دامنه این PRها (نیازمند برنامه استقرار مستقل) |

## خلاصه رفتار نهایی

- مدیر شرکت دارای `users.create` → `POST /api/v1/companies/{companyId}/users` → حساب جدید یا حساب موجود + عضویت همان شرکت **فوراً فعال و بدون نقش**؛ بدون تأیید اضافی؛ بدون هیچ نقش/مجوز/Entitlement/دسترسی مالی ضمنی؛ اتمیک؛ Audit امن؛ پاسخ 201 یکنواخت (ضد User Enumeration)؛ تکرار/race → 409 کنترل‌شده.
- هیچ مدیر شرکت/هلدینگی بدون اختیار مستقل مقصد نمی‌تواند کاربری را به عضویت فعال شرکت دیگری درآورد؛ self-approval و نقش سیستمی ممنوع؛ مسیر تأیید مقصد، سقف تفویض، جداسازی شرکت‌ها و دسترسی سال/دوره کاملاً حفظ شده‌اند.

## فایل‌های مهم این PR
- `docs/DECISIONS.md` (D-078 کامل + شماره ADR بعدی)، `docs/USER_ACCESS_MANAGEMENT.md` §18، `docs/ACCESS_CONTROL_ARCHITECTURE.md` §4.2، `docs/SECURITY.md`، `docs/PROJECT_STATUS.md`، `docs/INDEX.md`، `docs/WORK_LOG.md` (ورود سری + رفع تعارض پیش‌موجود merge #613)

## بررسی و آزمون (کل سری)
- `dotnet build -c Release` سبز در همه PRها؛ `dotnet test` نهایی: ۱۱۱۸ کل — ۱۱۱۳ موفق / ۷۱ Skip (SQL-gated) / ۲ ناموفق (شکست محیطی قدیمی `SafeExceptionRealApplicationTests` در main).
- گاردهای migration (سه‌گانه) + تست‌های خود ابزار (۳۲+۳۱) سبز؛ `dotnet ef migrations has-pending-model-changes` سبز.
- ClientApp: typecheck ۰ خطا؛ vitest ۱۰۲۵/۱۰۲۵؛ build و `check:offline` سبز؛ `check_static_assets.py` PASS.
- `git diff --check` تمیز؛ بدون secret؛ بدون فایل تولیدی.

## ریسک‌ها و کارهای باقی‌مانده
- Merge ترتیبی PRها (stacked) و سپس تغییر base به main توسط مالک؛ اجرای تست‌های SQL Server در CI و بازبینی نتایج آن.
- UI اختصاصی ایجاد کاربر شرکتی و E2E عملیاتی — PRهای مستقل آینده؛ هیچ ادعای Production-readiness برای دیتابیس مشتری.
