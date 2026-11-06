# تحویل به عامل Frontend — کاربران، دسترسی‌ها و نشست‌ها (Wave 1 Backend)

- **تاریخ:** 2026-11-06 · **مبنا:** `main@16d84466`؛ PRهای #549 تا #552 در main ادغام شده‌اند؛ #553 این سند را منتشر می‌کند.
- **منبع حقیقت قرارداد:** [`docs/api/USER_ACCESS_MANAGEMENT_API.md`](../api/USER_ACCESS_MANAGEMENT_API.md) و برای HR [`docs/HR_FRONTEND_API_CONTRACT.md`](../HR_FRONTEND_API_CONTRACT.md)؛ این سند فقط نقشهٔ مصرف و مرزهاست.
- **قاعده طلایی:** UI هرگز مرز امنیتی نیست. هر کنترلی که در UI مخفی/نمایش می‌شود، سمت سرور مستقلاً enforce می‌شود؛ DTO و وضعیت‌های خطا را حدس نزن — از سند قرارداد بخوان.

## ۱. مدل احراز هویت و نشست
- ورود فقط فرم Razor (`/Account/Login`) است؛ API با همان Cookie هم‌مبدأ HttpOnly کار می‌کند (D-012)؛ هیچ توکن در localStorage نیست.
- هر نشست یک ردیف `UserSession` دارد (D-069)؛ در هر درخواست سمت سرور: حساب فعال + CredentialStamp + نشست معتبر (ابطال/بی‌کاری ۳۰ دقیقه → 401). کوکی قدیمی یک‌بار رد و کاربر به ورود می‌رود.
- خروج: `POST /api/v1/session/logout` (همیشه POST + توکن anti-forgery). تغییر رمز همهٔ نشست‌ها را ابطال می‌کند.
- UI نشست‌ها (صفحهٔ امنیت حساب): `GET /me/sessions`، `POST /me/sessions/{id}/revoke`، `POST /me/sessions/revoke-others` — بدون IP/UA؛ نمایش «همین دستگاه» با `isCurrent`.

## ۲. زمینهٔ شرکت و دورهٔ مالی
- `companyId` فقط از مسیر؛ سرور فقط عضویت فعال را قبول می‌کند (D-013)؛ انتخاب/تغییر زمینه از `WorkContextEndpoints` (قرارداد موجود، بی‌تغییر).
- `GET /me/permissions` فقط برای منو؛ ساختار پاسخ: `{ isInstallationAdministrator, company: { id, permissionCodes } | null }`.
- HR به FiscalPeriod وابسته نیست؛ دورهٔ مالی فقط برای مسیرهای حسابداری/Trade لازم است.

## ۳. نگاشت صفحه‌ها به API (چک‌لیست)

| صفحه/قابلیت UI | Endpointها | وضعیت مصرف |
|---|---|---|
| امنیت حساب (پروفایل، رمز، ورودها) | `GET /me/profile`، `POST /me/password`، `GET /me/login-history` | کامل و تست‌شده |
| مدیریت نشست‌های من | `GET /me/sessions`، `POST /me/sessions/{id}/revoke`، `POST /me/sessions/revoke-others` | کامل (D-069؛ PR #552) |
| درخواست عضویت من | `GET/POST /me/membership-requests`، `GET /me/membership-requests/destinations` | کامل |
| صف تأیید مقصد | `GET /admin/companies/{id}/membership-requests/pending`، `/assignable-roles`، `/approvers`، `?status=`، `POST .../approve|reject` | کامل |
| گروه‌های دسترسی شرکت | `GET/POST/PUT /admin/companies/{id}/roles...`، `.../roles/{rid}/members`، `.../members/transfer`، `POST .../members/{uid}` | کامل؛ نقش سیستمی و خود-تخصیص و فراتر از سقف → 403/409 |
| کاتالوگ مجوز | `GET /admin/permission-catalog`، `GET /admin/permission-templates` | کامل؛ عضو بی‌مجوز 404 |
| دفترچه کاربران شرکت | `GET /companies/{id}/users` (`users.list`) | کامل |
| دفترچه هم‌گروه | `GET /corporate-groups/{gid}/directory` (`corporate-groups.directory.view`) | کامل، فقط-خواندنی |
| مدیریت کاربران (مدیر نصب) | `GET/POST /admin/users`، `GET/PUT /admin/users/{id}` | کامل؛ سیاست نام کاربری حالا در API هم enforce می‌شود (PR #551) |
| گزارش امنیتی | `GET /admin/security/audit-events`، `/audit-event-types`، `GET /admin/companies`، `POST .../export` | کامل؛ سقف ۱۰٬۰۰۰ و بازهٔ ۳۱ روز |
| ساختار سازمانی HR | `/api/v1/companies/{companyId}/org/*` | کامل — سند مستقل HR |

## ۴. قراردادهای خطا که UI باید پیاده کند
- 401 → انتقال به ورود. 403 → پیام «دسترسی ندارید» (بدنه `{ error }`). 404 → «یافت نشد» — هرگز «متعلق به دیگری» را تفکیک نکن.
- 409 → پیام فارسی بدنه + تازه‌سازی داده (RowVersion: نقش، عضویت، درخواست عضویت، کارمند/واحد/سمت HR).
- 429 → رعایت `Retry-After`، قفل دکمه، پیام عمومی. 413 → «حجم خروجی بیش از سقف» (CSV).
- 400 anti-forgery → توکن را دوباره بگیر و یک‌بار تلاش کن.

## ۵. قابلیت‌هایی که نباید ساخته شوند (Backend ندارد)
- بازیابی رمز فراموش‌شده / reset رمز توسط مدیر (تصمیم محصولی لازم).
- لغو/تعلیق مستقل عضویت، انقضای درخواست Pending، انتقال عضویت بین دو شرکت.
- ابطال نشست کاربر دیگر توسط مدیر نصب؛ retention/پاک‌سازی Audit (D-070 Proposed).
- هیچ entitlement/لایسنس عملیاتی در UI (جدا است).

## ۶. وضعیت واقعی هر حوزه (بدون اغراق)

| حوزه | وضعیت |
|---|---|
| Self-service، کاربران، نقش‌ها، عضویت، دفترچه‌ها، گزارش امنیتی | پیاده‌سازی‌شده و تست‌شده (Unit/Endpoint؛ SQL Server در CI) |
| نشست‌ها (D-069) | پیاده‌سازی‌شده؛ تست‌های هم‌زمانی SQL Server در CI |
| HR (`/org/*`) | پیاده‌سازی‌شده، hardened (PR #543/#545/#547)؛ UI هنوز ساخته نشده |
| E2E اختصاصی مدیریت دسترسی روی SQL Server | فقط در CI (`membership-workflow.spec.mjs`، `hr-structure.spec.mjs` و بقیه) |

## ۷. وضعیت PRهای این Wave (تا لحظهٔ تحویل)

| PR | عنوان | شاخه | تغییرات |
|---|---|---|---|
| #549 | fix(access): refuse system roles on the single-member assign path | `fix/access-system-role-assign` | ۱ گارد امنیتی + ۲ تست |
| #550 | fix(access): never delegate a role that can approve membership requests | `fix/membership-approve-approver-mint` | ۱ گارد امنیتی + ۱ تست |
| #551 | fix(identity): enforce the username charset policy on the user API | `fix/identity-username-charset` | ۱ اعتبارسنجی + ۵ تست |
| #552 | feat(security): implement the server-side session lifecycle (D-069) | `feat/session-lifecycle-d069` | ویژگی کامل + Migration + ۱۱ تست |
| #553 | docs(api): publish the authoritative user access API contract | `docs/api-user-access-contract` | سند قرارداد |

وضعیت زنده در 2026-11-06:
- PR #549 — ادغام‌شده در main.
- PR #550 — ادغام‌شده در main.
- PR #551 — ادغام‌شده در main.
- PR #552 — ادغام‌شده در main (merge commit `16d8446699b6cbc695e474e9031a4b85c9fb97a5`).
- PR #553 — همین تغییر مستنداتی؛ بازبنیادگذاری‌شده روی main فعلی و بدون تعارض Git.

وابستگی‌های اجرایی قرارداد (#550، #551 و #552) همگی اکنون در main هستند. وضعیت CI مربوط به SHA فعلی PR #553 باید پیش از Merge بررسی شود؛ این handoff وضعیت سبز بودن CI را ادعا نمی‌کند. همه ادغام‌ها فقط با اجازه مالک مخزن انجام می‌شود.
