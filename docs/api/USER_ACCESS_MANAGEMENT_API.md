# قرارداد API مدیریت کاربران و دسترسی‌ها (منبع حقیقت مصرف Frontend)

> به‌روزرسانی 2026-11-08: بخش ۱۰ (کاتالوگ دسترسی Stage 2-D) افزوده شد.

- **وضعیت:** `Implemented` — این سند از روی کد واقعی `main` (Nov 2026) استخراج شده است، نه از طراحی پیشین. هر تغییری در کد باید همین سند را هم به‌روزرسانی کند.
- **Issue مادر:** #444. اسناد مرتبط: `docs/USER_ACCESS_MANAGEMENT.md`، `docs/ACCESS_CONTROL_ARCHITECTURE.md`، `docs/FRONTEND_API_CONTRACT_NEEDS.md`، `docs/HR_FRONTEND_API_CONTRACT.md` (HR)، `docs/DECISIONS.md` (D-064، D-065، D-067، D-069، D-070، D-071، D-072، D-073، D-074).
- **مرز این سند:** کاربران، نقش‌ها و مجوزها، عضویت بین‌شرکتی، دفترچه هم‌گروه، مجوزهای من، گزارش امنیتی و نشست‌ها. قرارداد HR در سند مستقل خودش است و قرارداد زمینهٔ کاری (شرکت/دوره) در `WorkContextEndpoints` جداگانه می‌ماند.

## ۰. قواعد مشترک همهٔ endpointها

| موضوع | قرارداد واقعی |
|---|---|
| احراز هویت | Cookie هم‌مبدأ HttpOnly (`Infrastructure.Security.Utility.AuthenticationScheme`)؛ هیچ توکنی به JS داده نمی‌شود و در localStorage ذخیره نمی‌شود (D-012). |
| اعتبارسنجی مجدد نشست | هر درخواست: حساب فعال + CredentialStamp + ردیف نشست معتبر (D-069). شکست → 401 برای API و redirect برای صفحات. |
| Anti-forgery | همهٔ عملیات تغییردهنده POST/PUT نیاز به هدر `X-XSRF-TOKEN` دارند؛ توکن از `GET /api/v1/antiforgery/token` گرفته می‌شود. توکن نامعتبر → `400` با پیام «درخواست نامعتبر است.». |
| Rate Limit | گروه `/api` سیاست `api` (۳۰۰/دقیقه/کاربر)؛ endpointهای حساس سیاست `api-sensitive` (۳۰/دقیقه/کاربر)؛ صفحه ورود `login` (۲۰/دقیقه/IP). پاسخ 429 با `Retry-After` و `no-store` و بدنهٔ `application/problem+json` فارسی. |
| Cache | پاسخ‌های داده‌محور `Cache-Control: no-store` (و اغلب `Pragma: no-cache`). |
| مجوزدهی | همیشه سمت سرور و از دیتابیس در هر درخواست (D-013): عضویت فعال در شرکت فعال با نقش فعال؛ `companyId` مسیر فقط پس از این بررسی معتبر است. هیچ endpointی به Claim شرکت اعتماد نمی‌کند. |
| کدهای خطا | بدنهٔ خطای استاندارد `{ "error": "<پیام فارسی>" }` یا ProblemDetails فارسی؛ وضعیت‌ها: 400 ورودی نامعتبر، 401 ناشناس/نشست باطل، 403 احرازشده بدون مجوز، 404 «ناموجود = متعلق به دیگری» (بدون افشا)، 409 تعارض وضعیت/RowVersion، 429 نرخ، 413 سقف حجم خروجی، 500 عمومی بدون جزئیات. |
| صفحه‌بندی | پارامترهای `page` و `pageSize` با سقف `pageSize ≤ 100` (مقیاس‌شده با clamp)؛ پاسخ `{ items, totalCount, page, pageSize }` در endpointهای فهرستی جدیدتر؛ برخی فهرست‌های قدیمی فقط آرایه می‌دهند (در جدول مشخص شده). |
| زمان | فیلدهای `*Utc` با Kind=UTC و پسوند `Z` serialize می‌شوند (مبدل EF بند #500). فیلدهای قدیمی `InsertDateTime`/`UpdateDateTime`/`LastLoginDateTime` ساعت محلی سرور قدیمی‌اند (D-067 Proposed) و نباید با فیلدهای UTC مقایسه شوند. |
| DTO | صریح و allowlist؛ هیچ Entityای serialize نمی‌شود؛ رمز/hash/کلید تأیید/DetailsJson/IP در DTOهای عمومی نیست. |
| Audit | عملیات حساس رویداد در `SecurityAuditEvents` با actor، زمان UTC، CorrelationId و IP محدودشده ثبت می‌کنند؛ ردهای 403 عمداً رویداد ندارند (ISSUE-498). |

## ۱. شناخت هویت و مجوزها

### ۱.۱ `GET /api/v1/session`
- مجوز: احراز هویت. پاسخ ۲۰۰: `{ "username", "fullName", "accountKind": "User"|"Infrastructure", "isAdministrator": bool }` (از Claimها؛ `isAdministrator` نمایشی است و مرجع امنیتی نیست).

### ۱.۲ `GET /api/v1/me/profile`
- مجوز: `RequireClaim(AccountKind=User)`؛ UserId فقط از principal.
- پاسخ ۲۰۰ (allowlist): `{ username, firstName, lastName, emailAddress, cellPhoneNumber, isEmailAddressVerified, isCellPhoneNumberVerified, lastLoginDateTime, insertDateTime }`. 401 بدون شناسه، 404 حساب ناموجود.

### ۱.۳ `GET /api/v1/me/permissions`
- مجوز: احراز هویت کاربر. پاسخ ۲۰۰: `{ "isInstallationAdministrator": bool, "company": { "id": "guid", "permissionCodes": [string] } | null }`.
- شرکت فقط از زمینهٔ کاری محافظت‌شدهٔ همین نشست (`WorkContextService`)؛ کدها از نقش فعال عضویت فعال همان شرکت. این API فقط برای نمایش منو است و جایگزین مجوزدهی هر endpoint نیست. `no-store`؛ عضو بدون زمینه → `company: null`؛ حساب Infrastructure فقط پرچم مدیر را می‌دهد.

### ۱.۴ توکن Anti-forgery
`GET /api/v1/antiforgery/token` → `{ "headerName": "X-XSRF-TOKEN", "requestToken": "..." }`؛ rate limit حساس؛ کوکی توکن ست می‌شود.

## ۲. مدیریت کاربران (فقط مدیر نصب)

- **مجوزدهی:** هیچ کد permission ندارد؛ هر درخواست نقش فعال `Admin` را از دیتابیس بازخوانی می‌کند (`IsActive` کاربر و نقش). غیرمدیر 403 می‌گیرد (تست‌شده برای create/update؛ 403 با بدنهٔ `{ error }`).
- **بازیابی/Reset رمز (D-073 Accepted):** endpoint بازیابی رمز فراموش‌شده و Reset آغازشده توسط مدیر هنوز در کد وجود ندارد؛ این سند قرارداد موجود را توصیف می‌کند و تا Issue اجرایی، هیچ API یا UI جدیدی فرض نمی‌شود.

### ۲.۱ `GET /api/v1/admin/users`
- Query: `page`, `pageSize`, `search` (روی نام کاربری/نام/ایمیل/موبایل)، `status=all|active|inactive` (پیش‌فرض `all`)، `sort=created|lastLogin|name|username` (پیش‌فرض `created`) و `descending` (پیش‌فرض `false`). `page` حداقل ۱، `pageSize` بین ۱ تا ۱۰۰ (clamp)؛ `search` پس از Trim حداکثر ۱۲۰ نویسه، ورودی طولانی‌تر یا status/sort ناشناخته → 400.
- پاسخ ۲۰۰: `{ items: [{ id, username, firstName, lastName, emailAddress, cellPhoneNumber, isActive, lastLoginDateTime, insertDateTime, activeCompanyCount }], totalCount, page, pageSize }`؛ فیلتر/جستجو/شمارش سمت سرور.

### ۲.۲ `GET /api/v1/admin/users/{userId}`
- 404 برای ناموجود. پاسخ ۲۰۰: فیلدهای فهرست + `isEmailAddressVerified`, `isCellPhoneNumberVerified`, `updateDateTime`, `legacyRoleName`, `memberships: [{ companyId, companyName, roleCode, roleName, isActive, createdAtUtc }]`.

### ۲.۳ `POST /api/v1/admin/users` (rate limit حساس + anti-forgery)
- بدنه: `{ username, firstName, lastName, emailAddress, cellPhoneNumber?, password, confirmPassword, isActive }`.
- اعتبارسنجی: نام کاربری `^[a-zA-Z0-9._@+-]{3,20}$` (از 2026-11-05 روی API هم اعمال می‌شود)، ایمیل معتبر ≤۲۵۴، موبایل دقیقاً ۱۱ رقم، رمز ۸..۱۲۸ با رد رمزهای رایج، تطابق تأیید. خطا → 400 `{ error: "اطلاعات کاربر یا سیاست رمز عبور معتبر نیست." }`.
- تکراری (نام کاربری case-insensitive/ایمیل/موبایل) → 409؛ Audit `UserCreated`؛ نقش سراسری نمی‌گیرد (`RoleId=null`).

### ۲.۴ `PUT /api/v1/admin/users/{userId}` (rate limit حساس + anti-forgery)
- بدنه: `{ username, firstName, lastName, emailAddress, cellPhoneNumber?, isActive }` — **رمز در این endpoint نیست**.
- همان اعتبارسنجی؛ تغییر ایمیل/موبایل وضعیت تأیید را reset می‌کند؛ Audit `UserUpdated` با `previousIsActive/currentIsActive`؛ غیرفعال‌کردن خود یا آخرین مدیر فعال نصب → 409 (تراکنش Serializable).

## ۳. نقش‌های شرکتی، کاتالوگ و سقف تفویض (D-071)

- **مجوزدهی دوحالته:** مدیر نصب (بازخوانی DB، بدون سقف) یا مدیر شرکت (عضویت فعال + کد مجوز، محدود به سقف تفویض مبتنی بر تصاحب). ناشناس 401؛ بدون هیچ رابطه با شرکت 404؛ عضو بی‌مجوز 403.
- کدهای مجوز: `roles.view`، `roles.create`، `roles.update`، `roles.assign` (seed idempotent در Migration `20261104174711`).
- **قواعد سقف:** مدیر شرکت فقط با مجوزهایی که خودش در همان شرکت دارد نقش می‌سازد/ویرایش می‌کند/تخصیص می‌دهد؛ سقف درون تراکنش از ردیف‌های tracked خوانده می‌شود و گاردهای RowVersion تغییر هم‌زمان را 409 می‌کنند؛ تغییر نقش خودش از مسیر شرکتی 403.
- **نقش سیستمی** (`IsSystemRole`، مانند `company-admin`): ویرایش/تخصیص/انتقال به و از آن برای همهٔ actorها 409 (از 2026-11-05 شامل مسیر تک‌عضوی assign هم می‌شود).

### ۳.۱ `GET /api/v1/admin/companies/{companyId}/roles`
فهرست همهٔ نقش‌های همان شرکت (نه فقط نقش‌های فعال) با کدهای مجوز؛ ۲۰۰ آرایه‌ای از `{ id, companyId, code, name, isSystemRole, isActive, permissionCount, activeMemberCount, permissionCodes: [string] }`. مرتب‌سازی بر اساس نام نقش است.

### ۳.۲ `GET /api/v1/admin/permission-catalog` و `GET /api/v1/admin/permission-templates`
- کاتالوگ برای مدیر نصب کامل است؛ برای مدیر شرکت فقط کدهای سقف خودش؛ عضو بدون هیچ مجوز **404** می‌گیرد (قرارداد D-071). قالب‌ها (templates) با الگوی مشابه.

### ۳.۳ `POST /api/v1/admin/companies/{companyId}/roles` (حساس + anti-forgery)
- بدنه مطابق DTO واقعی: `{ code, name, templateCode?, permissionCodes? }`؛ `code` و `name` در عمل الزامی‌اند (کد با الگوی `^[A-Z0-9][A-Z0-9._-]{1,79}$`، نام حداکثر ۱۲۰ نویسه). `templateCode` و/یا `permissionCodes` برای تعیین مجوزها استفاده می‌شوند؛ `isActive` در درخواست ایجاد وجود ندارد. پاسخ 201: `{ id }` و هدر `Location` به منبع نقش جدید؛ کد تکراری → 409؛ کد/الگو/مجوز نامعتبر → 400؛ فراتر از سقف تفویض → 403؛ Audit `CompanyRoleCreated`.

### ۳.۴ `PUT /api/v1/admin/companies/{companyId}/roles/{roleId}` (حساس + anti-forgery)
- بدنه: `{ name, isActive, permissionCodes }` — تعویض کامل مجموعه مجوزها؛ نقش سیستمی → 409؛ غیرفعال‌سازی نقش دارای عضو فعال → 409 (ابتدا اعضا منتقل شوند)؛ Audit `CompanyRoleUpdated`؛ تغییر هم‌زمان → 409 (RowVersion نقش و عضویت‌ها).

### ۳.۴.۱ `GET /api/v1/admin/companies/{companyId}/roles/{roleId}/permissions/preview` (Stage 2 — 2026-11-08)
- Query: `permissionCodes` تکرارشونده (کل مجموعهٔ کاندید). پاسخ: `{ roleId, roleName, activeMemberCount, currentPermissionCodes[], addedPermissionCodes[], removedPermissionCodes[], sensitiveChangedPermissionCodes[], ceilingViolatedPermissionCodes[] }`.
- **فقط-خواندنی و بدون Mutation/Audit:** هیچ داده‌ای ذخیره نمی‌شود؛ در زمان ذخیره، تمام کنترل‌های امنیتی (سقف D-071 درون تراکنش، RowVersion، anti-forgery) دوباره اجرا می‌شوند. پیش‌نمایش راه دور زدن سقف نیست.
- محاسبه سمت سرور: افزوده/حذف‌شونده = تفاضل مجموعه کاندید با مجوزهای فعلی نقش؛ `activeMemberCount` = اعضای فعال نقش؛ **مجوز حساس** = پسوند `delete`/`approve`/`close` (قاعده server-owned طبق تاکسونومی `PERMISSION_CATALOG.md` §2)؛ `ceilingViolatedPermissionCodes` فقط برای مدیر شرکت گزارش می‌شود (مشاوره‌ای؛ رد قطعی در ذخیره) و برای مدیر نصب خالی است.
- Auth: همان مجوز ویرایش (`roles.update`) دوحالتهٔ D-071 — بدون `roles.update` پیش‌نمایش هم 403 است (کشف سقف/ترکیب اعضا بسته می‌شود). نقش سیستمی → 409؛ کد ناشناخته/تکراری → 400؛ نقش بیگانه/غیرعضو → 404. `no-store`، rate limit.

### ۳.۵ `GET /api/v1/admin/companies/{companyId}/roles/{roleId}/members`
- Query: `page` (پیش‌فرض ۱)، `pageSize` (پیش‌فرض ۲۵، clamp بین ۱ تا ۱۰۰) و `search` اختیاری (جستجوی سمت سرور روی `username`/`firstName`/`lastName`؛ قرارداد همان دایرکتوری کاربران: trim، خالی نادیده، بیش از ۱۲۰ نویسه → 400). پاسخ: `{ items: [{ userId, username, firstName, lastName, isUserActive, memberSinceUtc }], totalCount, page, pageSize }`؛ فقط اعضای فعال آن نقش برگردانده می‌شوند؛ فیلتر جستجو پیش از شمارش و صفحه‌بندی اعمال می‌شود؛ ترتیب نام خانوادگی، نام و سپس شناسه است؛ نقش بیگانه → 404. (حساس rate limit.)

### ۳.۶ `POST /api/v1/admin/companies/{companyId}/roles/{roleId}/members/{userId}` (حساس + anti-forgery)
- تخصیص نقش به عضو فعال موجود؛ عضویت جدید/غیرفعال → 409 «از مسیر تأیید مقصد»؛ نقش سیستمی → 409 «تخصیص گروه سیستمی مجاز نیست.» (اصلاح 2026-11-05)؛ خود-تخصیص شرکتی → 403؛ فراتر از سقف → 403؛ Audit `UserCompanyRoleAssigned`.

### ۳.۷ `POST /api/v1/admin/companies/{companyId}/roles/{roleId}/members/transfer` (حساس + anti-forgery)
- بدنه: `{ targetRoleId }`؛ انتقال گروهی همهٔ اعضای فعال نقش مبدا؛ سقف ۵۰۰ در هر درخواست (بیشتر → 409 بدون انتقال)؛ نقش سیستمی در دو سر → 409؛ Audit `UserCompanyRoleAssigned` برای هر عضو منتقل‌شده.

### ۳.۸ اعضای نقش و انتقال (D-071)
`GET /api/v1/admin/companies/{companyId}/roles/{roleId}/members` و `POST .../members/transfer` برای مدیر نصب و مدیر شرکتی دارای `roles.assign` (با سقف) کار می‌کنند؛ انتقال خود عضوِ مدیر توسط خودش 403.

## ۴. دفترچه کاربران شرکت

### ۴.۱ `GET /api/v1/companies/{companyId}/users` (permission: `users.list`)
- Query: `search` اختیاری (روی username/firstName/lastName، حداکثر ۱۲۰ نویسه)، `membershipActive` اختیاری (فیلتر وضعیت عضویت)، `page` (پیش‌فرض ۱) و `pageSize` (پیش‌فرض ۲۵، clamp بین ۱ تا ۱۰۰). پاسخ: `{ companyId, companyName, items: [{ userId, username, firstName, lastName, userIsActive, membershipIsActive, roleId, roleCode, roleName, roleIsActive, membershipCreatedAtUtc }], totalCount, page, pageSize }`. شامل فیلدهای نقش و وضعیت جداگانهٔ حساب/عضویت است؛ اطلاعات تماس، رمز، نشست و دادهٔ Audit افشا نمی‌شود. غیرعضو 404؛ عضو بی‌مجوز `users.list` → 403؛ (حساس rate limit، no-store).

## ۵. گردش درخواست عضویت بین‌شرکتی (D-065)

- همه با anti-forgery و rate limit حساس. عضویت Pending هیچ دسترسی نمی‌دهد؛ فقط تأییدکنندهٔ شرکت مقصد تصمیم می‌گیرد؛ self-approval برای همه 403 (+ CHECK دیتابیسی)؛ نقش سیستمی برای همه رد می‌شود.

### ۵.۱ `GET /api/v1/me/membership-requests`
درخواست‌های خود کاربر با `status` اختیاری (`Pending|Approved|Rejected`) و صفحه‌بندی. (این خواندن عمداً در سیاست حساس چهار endpoint هویتی نیست — Issue #522؛ تغییر آن نیازمند تصمیم مالک است.)

### ۵.۲ `POST /api/v1/me/membership-requests`
- بدنه مطابق DTO واقعی: `{ destinationCompanyId }`؛ فیلد `reason` پشتیبانی نمی‌شود. مرز CustomerBoundary دوباره سمت سرور بررسی می‌شود؛ 201 `{ id, status: "Pending", requestedAtUtc }`؛ شرکت ناموجود/غیرفعال/خارج از مرز → 404 یکسان؛ عضو فعال یا درخواست Pending تکراری → 409 (ایندکس فیلترشده + رقابت → 409). Audit `CompanyMembershipRequested`.

### ۵.۳ `GET /api/v1/me/membership-requests/destinations?search=`
جستجوی **exact و اجباری** (۳..۱۲۰ نویسه پس از Trim)؛ فقط شرکت‌های فعال داخل مرز مشتری مشترک؛ حذف عضوهای فعال و درخواست‌دهنده‌های Pending؛ fail-closed روی مرز null.

### ۵.۴ صف تأیید مقصد (`/api/v1/admin/companies/{companyId}/membership-requests/...`)
مجوز: مدیر نصب یا عضو فعال دارای `users.membership.approve` همان شرکت (غیرعضو 404، بی‌مجوز 403).
- `GET /pending` — صف شرکت مقصد.
- `GET /assignable-roles` — نقش‌های فعال غیرسیستمی؛ برای تأییدکنندهٔ شرکتی فقط داخل سقف و **بدون نقش‌های حامل مجوز تأیید** (اصلاح 2026-11-05 در POST هم اعمال شد).
- `GET /approvers` — فعالان دارای مجوز تأیید همان شرکت (allowlist).
- `GET ?status=` — تاریخچه تصمیم‌ها (allowlist؛ UTC).
- `POST /{requestId}/approve` — بدنه `{ roleId }`؛ عضویت ایجاد/فعال‌سازی + تصمیم + Audit `CompanyMembershipApproved` در یک تراکنش ReadCommitted با گاردهای RowVersion (تعارض → 409). رد نقش سیستمی و نقش حامل مجوز تأیید → 403 برای تأییدکنندهٔ شرکتی؛ مدیر نصب برای bootstrap اولین مدیر شرکت معاف از سقف است اما نقش سیستمی برای او هم بسته است.
- `POST /{requestId}/reject` — Audit `CompanyMembershipRejected`؛ پس از رد، درخواست جدید ممکن است.

### ۵.۵ چه چیزی عمداً وجود ندارد
لغو/تعلیق مستقل عضویت (چرخهٔ Suspended/Revoked/Expired) و انقضای درخواست Pending هنوز پیاده نشده‌اند — نیازمند تصمیم محصولی؛ انتقال عضویت بین دو شرکت endpoint مستقل ندارد (مسیر: رد و درخواست جدید).

## ۶. دفترچه هم‌گروه (D-065 تصمیم ۲)

### ۶.۱ `GET /api/v1/corporate-groups/{groupId}/directory` (permission: `corporate-groups.directory.view`)
- فقط-خواندنی؛ scope اثبات‌شده: عضو باز همان گروه شرکتی؛ fail-closed روی مرز مشتری ناهمگون (شامل شرکت مالک)؛ خروجی فقط `{ companyId, companyName, userId, firstName, lastName }`؛ بدون username/تماس/نقش/مجوز؛ (حساس rate limit؛ no-store).

## ۷. گزارش امنیتی و Audit (مدیر نصب)

- مجوز همه: `RequireClaim(AccountKind=User)` + بازخوانی DB نقش `Admin`؛ همه حساس rate limit.
- رویدادهای ابطال نشست: `UserSessionRevoked` (ابطال توسط خود کاربر)، `UserSessionsRevokedOthers` — با برچسب فارسی در `audit-event-types`.

### ۷.۱ `GET /api/v1/admin/security/audit-events`
- فیلترهای exact: `eventType`, `userId`, `companyId`, `fromUtc`, `toUtc`؛ مرتب‌سازی allowlist `sort=occurredAt|eventType` و `descending=true|false` (نامعتبر → 400)؛ tie-break پایدار `Id DESC`؛ صفحه‌بندی clamp (page ≤ 100000، pageSize ≤ 100).
- پاسخ: آیتم‌های بدون IP و بدون `DetailsJson`: `{ id, eventType, entityType, entityId, occurredAtUtc, actorUserId, actorDisplayName, companyId, companyName, correlationId }`.
- برچسب‌های فارسی انواع رویداد از `GET /api/v1/admin/security/audit-event-types` (کد ناشناخته خودش برمی‌گردد؛ UI برچسب نمی‌سازد).

### ۷.۲ `GET /api/v1/admin/companies?search=&page=&pageSize=`
فهرست شرکت‌های نصب برای فیلتر گزارش: `{ items: [{ id, name, isActive }], totalCount, page, pageSize }`.

### ۷.۳ `POST /api/v1/admin/security/audit-events/export` (حساس + anti-forgery)
- بدنه: `{ eventType?, userId?, companyId?, fromUtc, toUtc }`؛ هر دو تاریخ اجباری؛ بازهٔ ≤۳۱ روز (بیشتر/معکوس → 400)؛ سقف ۱۰٬۰۰۰ ردیف (بیشتر → 413).
- خروجی `text/csv; charset=utf-8` با BOM، quoting RFC 4180 و CRLF؛ خنثی‌سازی formula injection (`=`,`+`,`-`,`@`,TAB,CR,LF ابتدای سلول) و کنترل‌کاراکترها با apostrophe؛ فیلدها همان projection ایمن گزارش است.
- رویداد `SecurityAuditExported` پیش از تحویل فایل ثبت می‌شود؛ شکست Audit = بدون فایل.

### ۷.۴ Retention (D-070)
`Accepted` (D-070، 2026-11-06) / پیاده‌سازی `NotImplemented` — هیچ endpoint، job یا پاک‌سازی‌ای وجود ندارد و UI حق حذف رکورد ندارد. سیاست ۱۲/۲۴/۳۶ ماه، Archive چندلایه و Legal Hold در ADR ثبت شده‌اند.

## ۸. چرخهٔ عمر نشست‌ها (D-069 — پیاده‌سازی‌شده 2026-11-05)

- هر ورود موفق کاربر یک رکورد `UserSession` می‌سازد؛ Claim `SessionId` کوکی همان شناسه است و در هر درخواست اعتبارسنجی می‌شود (مفقود/ابطال‌شده/بیگانه/idle>30 دقیقه → 401). کوکی‌های قدیمی یک‌بار رد می‌شوند.
- در جدول نشست IP و User-Agent ذخیره نمی‌شود؛ حساب Infrastructure نشست ندارد.

### ۸.۱ `GET /api/v1/me/sessions` (حساس)
- آرایه‌ای از نشست‌های **فعال** کاربر جاری: `[ { "sessionId": "guid", "isCurrent": bool, "createdAtUtc": "...Z", "lastSeenAtUtc": "...Z" } ]`؛ مرتب‌سازی `lastSeenAtUtc DESC, sessionId DESC`؛ فیلد `status` وجود ندارد (فقط active برگردانده می‌شود).

### ۸.۲ `POST /api/v1/me/sessions/{sessionId}/revoke` (حساس + anti-forgery)
- ابطال یک نشست متعلق به خود کاربر؛ `204` در موفقیت و در تلاش تکراری (idempotent، بدون Audit دوم)؛ `404` یکسان برای ناموجود و نشست کاربر دیگر؛ ابطال نشست جاری Cookie را sign-out می‌کند. Audit `UserSessionRevoked` (EntityId=شناسه نشست، دلیل `self_revoked`).

### ۸.۳ `POST /api/v1/me/sessions/revoke-others` (حساس + anti-forgery)
- همهٔ نشست‌های فعال دیگر کاربر (به‌جز نشست جاری) ابطال می‌شوند → `200 { "revokedCount": n }`؛ یک رویداد `UserSessionsRevokedOthers` با شمارش. ابطال نشست کاربر دیگر توسط مدیر نصب طبق D-073 §1 در بخش ۸.۵ همین سند آمده است.

### ۸.۴ ابطال‌های خودکار
- تغییر رمز (`POST /me/password`): همهٔ نشست‌های فعال با دلیل `password_changed` ابطال می‌شوند؛ CredentialStamp هم دقیقه‌بعدی می‌ماند؛ مرورگر جاری بلافاصله sign-out می‌شود؛ Audit `PasswordChanged` با `revokedSessions` در DetailsJson (بدون رمز).
- غیرفعال‌سازی حساب: نشست در اعتبارسنجی بعدی رد می‌شود.
- خروج صریح (`POST /session/logout` API و صفحه Razor): رویداد `LogoutSucceeded` + ابطال نشست جاری + پاک‌سازی Cookie و زمینهٔ کاری.

### ۸.۵ ابطال مدیریتی نشست کاربر دیگر (D-073 §1 — پیاده‌سازی‌شده 2026-11-06)
فقط مدیر نصب فعال (نقش در هر درخواست از دیتابیس بازخوانی می‌شود؛ هیچ مجوز CompanyRole این اختیار را نمی‌دهد) و فقط برای «کاربر دیگر»: درخواست Self-target (`userId` مدیر فعلی) در هر سه Endpoint با `400` رد می‌شود و مدیریت نشست‌های خود مدیر منحصراً از مسیر self-service بخش ۸.۱–۸.۳ انجام می‌شود. هر Mutation دو الزام دارد: **احراز هویت مجدد** (رمز جاری خودِ مدیر در بدنه؛ تأیید سمت سرور با همان سازوکار تغییر رمز) و **کد دلیل از کاتالوگ مجاز**: `admin_revoked_suspected_compromise` / `admin_revoked_offboarding` / `admin_revoked_user_request` / `admin_revoked_administrative`.

| عملیات | Method/Route | بدنه | پاسخ‌ها |
|---|---|---|---|
| فهرست نشست‌های فعال کاربر هدف | `GET /api/v1/admin/users/{userId}/sessions` (حساس) | — | `200 [ { "sessionId": "guid", "createdAtUtc": "...Z", "lastSeenAtUtc": "...Z" } ]` — بدون IP/UA، مرتب‌سازی آخرین استفاده؛ `400` Self-target |
| ابطال یک نشست مشخص | `POST .../sessions/{sessionId}/revoke` (حساس + anti-forgery) | `{ "currentPassword": "...", "reasonCode": "..." }` | `204` (موفق و تکرار idempotent بدون Audit دوم)؛ `403` re-auth ناموفق؛ `400` دلیل نامعتبر/توکن anti-forgery کهنه/Self-target؛ `404` یکسان برای ناموجود و نشست کاربر دیگر |
| ابطال همهٔ نشست‌های فعال کاربر هدف | `POST .../sessions/revoke-all` (حساس + anti-forgery) | `{ "currentPassword": "...", "reasonCode": "..." }` | `200 { "revokedCount": n }`؛ تکرار بدون تغییر `0` و بدون Audit دوم؛ `403`/`400`/`404` مانند بالا |

- Audit: `AdminUserSessionRevoked` (EntityId=شناسه نشست) و `AdminUserSessionsRevoked` (EntityId=شناسه کاربر هدف، `revokedCount` در DetailsJson) — هر دو با `targetUserId` و `reasonCode` و **بدون رمز یا توکن**؛ actor مدیر است. تغییر نشست و درج Audit در **یک تراکنش SQL واحد** commit/rollback می‌شوند؛ شکست درج Audit ابطال را کامل برمی‌گرداند (fail-closed — تست SQL Server `AdminSessionRevocation_AuditFailure_RollsTheRevocationBack`).
- ابطال در درخواست بعدی کاربر واقعاً به 401 می‌رسد (SessionValidator)؛ هیچ Session Cookie/Token/Secret در پاسخ برنمی‌گردد؛ مدیریت نشست خود مدیر منحصراً از مسیر self-service بخش ۸.۱–۸.۳ انجام می‌شود (مسیر مدیریتی درخواست Self-target را با 400 رد می‌کند).
- ابطال اضطراری همهٔ نشست‌های کل Installation خارج از D-073 §1 است و API ندارد.

## ۹. سناریوهای کلیدی Frontend

1. **منو و کنترل‌ها:** از `GET /me/permissions` بخوان؛ هرگز از `isAdministrator` به‌تنهایی تصمیم امنیتی نگیر — سرور دوباره بررسی می‌کند.
2. **هر POST/PUT:** اول `GET /antiforgery/token`، بعد ارسال هدر `X-XSRF-TOKEN`؛ 400 تکراری یعنی توکن کهنه است.
3. **تعارض 409:** پیام فارسی بدنه را نشان بده و داده را تازه کن (RowVersion نقش/عضویت/درخواست/نشست سمت سرور گارد می‌شود).
4. **404 در scope شرکت:** «ناموجود» و «متعلق به دیگری» یکسان‌اند — UI هیچ تفکیکی نشان ندهد.
5. **429:** `Retry-After` را رعایت و فرم را قفل کن؛ پیام عمومی فارسی است.
6. **نشست‌ها:** فهرست، ابطال هر دستگاه، «ابطال بقیه»؛ بعد از تغییر رمز یا خروج، کاربر به صفحه ورود می‌رود.
7. **زمان:** همهٔ `*Utc` را با `Z` نمایش/مقایسه کن؛ فیلدهای legacy (`lastLoginDateTime`، `insertDateTime`) را با UTC قاطی نکن (D-067).


## ۹. تصمیم‌های پذیرفته‌شده اما هنوز پیاده‌سازی‌نشده (D-073)

- Forgot Password، Admin Reset و MFA در D-073 پذیرفته شده‌اند؛ API فعلی همچنان فقط قراردادهای پیاده‌سازی‌شده را توصیف می‌کند.
- تا زمان Issue/PR اجرایی، هیچ مسیر یا DTO فرضی به مصرف‌کننده Frontend معرفی نمی‌شود.
- Reset رمز توسط مدیر شرکت نیازمند مجوز مستقل `users.credentials.reset` است و فقط برای عضو فعال همان Company درخواست user-controlled reset را آغاز می‌کند؛ تغییر credential در سطح حساب Installation اثر می‌گذارد.

## ۱۰. کاتالوگ دسترسی (Stage 2-D — D-075؛ پیاده‌سازی‌شده 2026-11-08)

سه endpoint فقط-خواندنی سطح نصب که کاتالوگ کد-محور زیرسیستم‌ها/منوها/فرم‌ها را می‌دهند (منبع: `Server/Infrastructure/Access/AccessCatalogRegistry.cs`؛ هم‌خوان با `docs/SUBSYSTEM_CATALOG.md` و `docs/MENU_FORM_CATALOG.md`). **عملیات و مجوزها از این endpoints سرو نمی‌شوند** — `GET /admin/permission-catalog` (بخش ۳) مرجع همان کاتالوگ است تا API تکراری ساخته نشود.

| متد و مسیر | پاسخ موفق | Auth |
|---|---|---|
| `GET /api/v1/admin/access/catalog/subsystems` | آرایهٔ `{code,title,entitlementScope,entitlementLinked,entitlementCode?}` — `entitlementScope` یکی از `none\|installation\|company`؛ کد فقط از ثابت‌های `LicenseSubsystemCodes` | فقط مدیر نصب فعال (بازخوانی نقش از DB در هر درخواست، D-064) |
| `GET /api/v1/admin/access/catalog/menus` | آرایهٔ `{label,group?,subsystemCode,status,route?}` — `status` یکی از `active\|coming-soon`؛ `route` فقط برای وضعیت `active` | همان |
| `GET /api/v1/admin/access/catalog/forms` | آرایهٔ `{route,title,subsystemCode,permissionHint?,parent}` — `permissionHint` صرفاً hint نمایشی است (D-064) | همان |

- همهٔ پاسخ‌ها `Cache-Control: no-store` دارند؛ بدون Pagination چون کاتالوگ ثابت و کوچک است (۱۸ زیرسیستم/۲۰ منو/۲۳ فرم).

### ۳.۱ دسترسی‌های مؤثر یک عضو (Stage 2-E — 2026-11-08)

`GET /api/v1/admin/companies/{companyId}/effective-permissions/{userId}` → آرایهٔ `{userId,companyId,roleNames[],permissionCodes[],permissions:[{code,sourceRoleNames[]}]}`.

- معنا: اجتماع کدهای مجوز نقش‌های **فعال** روی عضویت‌های **فعال** همان کاربر در همان شرکت + نام نقش‌ها؛ مرتب‌سازی ordinal.
- **منبع مجوزبه‌مجوز (2026-11-08، مرحله دوم):** `permissions` هر کد را به نقش(های) فعال اعطاکننده‌اش نگاشت می‌کند (`CompanyAccessService.GetGrantedPermissionsWithSourcesAsync`؛ همان محمول‌های `QueryActiveMemberships`، بنابراین اجتماع کدها با `permissionCodes` همیشه برابر است). یک مجوز مشترک بین چند نقش، همهٔ نام‌های منبع را فهرست می‌کند؛ نقش غیرفعال هرگز منبع نیست. `permissionCodes` برای سازگاری حفظ شده است.
- Auth (D-071 دوحالته): مدیر نصب فعال (بازخوانی از DB) یا عضو فعال همان شرکت با `roles.view`؛ عضو بدون مجوز 403؛ کاربر بی‌نسبت به شرکت یا بدون عضویت فعال هدف (شامل حساب ناموجود) **404 بدون افشا**.
- فقط-خواندنی، `no-store`، بدون Audit (مطابق الگوی خواندنی‌های نقش‌ها)؛ تست: `EffectivePermissionsEndpointTests`.
- خطاها: ناشناس 401؛ کاربر واردشدهٔ غیرمدیر نصب 403 (پیام فارسی `فقط مدیر نصب سامانه مجاز به این عملیات است.`). مدیر شرکت برای نقش‌های شرکت خود از endpointهای بخش ۳ استفاده می‌کند و این کاتالوگ به او سرو نمی‌شود.
- تغییر محتوا فقط با به‌روزرسانی Registry + ردیف‌های `SUBSYSTEM_CATALOG.md`/`MENU_FORM_CATALOG.md` انجام می‌شود؛ تست `AccessCatalogEndpointTests` درون‌سازگاری (ارجاع زیرسیستم، یکتایی مسیر، عدم ساخت کد entitlement) را قفل می‌کند.
