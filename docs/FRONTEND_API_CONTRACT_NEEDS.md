# قراردادهای API مورد نیاز رابط کاربری مدیریت دسترسی (Backend ← Frontend)

- **وضعیت:** `Proposed` — پیشنهاد قرارداد از سمت Frontend؛ مسئول پیاده‌سازی: ChatGPT (Backend/API/مدل داده/امنیت)؛ مسئول مصرف: Claude (UI)
- **Issue مادر:** #444 · **تاریخ:** 2026-11-03، بر اساس کد `main`
- **قاعده:** تا وجود API واقعی، UI مربوط ساخته نمی‌شود و هیچ داده/قابلیت ساختگی نمایش داده نمی‌شود (دستور کار). هر مورد پس از Merge شدن API در PR مستقل UI مصرف می‌شود.
- **قواعد مشترک همهٔ endpointها:** احراز هویت Cookie + Anti-forgery برای POST؛ `Cache-Control: no-store`؛ مجوز در سرور و از دیتابیس در هر درخواست (D-013)؛ `companyId` از مسیر و فقط پس از بررسی عضویت فعال؛ خطاها `{ "error": "<پیام فارسی>" }`؛ صفحه‌بندی `{ items, totalCount, page, pageSize }` با `pageSize ≤ 100`؛ زمان‌های `*Utc` با `Z` (Issue #500)؛ بدون IP/توکن/hash/DetailsJson در پاسخ؛ هر تغییر حساس Audit شود.

## ۱. گردش عضویت بین‌شرکتی (مرحلهٔ ۷)
موجود و ادغام‌شده: `GET/POST /me/membership-requests`، `GET /admin/companies/{id}/membership-requests/pending`، `POST …/{requestId}/approve|reject`.

### ۱.۱ کشف شرکت‌های قابل‌درخواست
`GET /api/v1/me/membership-requests/destinations?search=&page=&pageSize=`
- پاسخ ۲۰۰: `{ "items": [{ "companyId": "guid", "name": "string" }], "totalCount": 0, "page": 1, "pageSize": 25 }`
- فقط شرکت‌های **فعال** که طبق D-065 قابل درخواست‌اند (هم‌گروه شرکتی/دامنهٔ مجاز) و کاربر در آن‌ها عضویت فعال یا درخواست Pending ندارد. شرکت‌های بیرون از دامنه هرگز در پاسخ یا شمارش نیایند. `search` حداکثر ۱۲۰ نویسه.
- خطا: 401 ناشناس/حساب غیرکاربر؛ 400 `search` طولانی. فهرست خالی خطا نیست.
- UI: فرم «ثبت درخواست» با جستجو و انتخاب شرکت، سپس `POST /me/membership-requests { destinationCompanyId }`.

### ۱.۲ ثبت درخواست (موجود) — کدهای خطای مورد انتظار UI
`POST /me/membership-requests` → 201 `{ id, status, requestedAtUtc }`؛ 400 شرکت نامعتبر؛ 404 شرکت مقصد ناموجود/خارج از دامنه (پاسخ یکسان، بدون افشا)؛ 409 عضو فعال یا درخواست Pending دارد.

### ۱.۳ گروه‌های قابل‌تخصیص برای تأییدکننده
`GET /api/v1/admin/companies/{companyId}/membership-requests/assignable-roles`
- پاسخ ۲۰۰: `[{ "id": "guid", "code": "string", "name": "string", "isSystemRole": false }]`
- مجوز: همان `users.membership.approve` شرکت (بدون مجوز ← 404 مانند `/pending`). فقط گروه‌های **فعال همان شرکت** و **در سقف تفویض** تأییدکننده (PR #498)؛ مجوزهای داخل گروه برگردانده نشود.
- اکنون تأییدکنندهٔ غیرمدیر نصب نمی‌تواند `GET …/roles` را بخواند و UI پیام «با مدیر نصب هماهنگ کنید» نشان می‌دهد.

### ۱.۴ سوابق تصمیم برای تأییدکننده
`GET /api/v1/admin/companies/{companyId}/membership-requests?status=Pending|Approved|Rejected&page=&pageSize=`
- آیتم: `{ id, requesterDisplayName, username, status, requestedAtUtc, decidedAtUtc, decidedByDisplayName, approvedRoleName, decisionReason }`؛ مجوز مانند ۱.۳. درخواست‌کننده سوابق خودش را از `GET /me/membership-requests` می‌بیند (موجود).

## ۲. مجوزهای کاربر جاری و منو (مرحلهٔ ۹)
`GET /api/v1/me/permissions?companyId=` (شرکت پیش‌فرض: شرکت انتخاب‌شده در زمینهٔ کاری)
- پاسخ ۲۰۰: `{ "isInstallationAdministrator": false, "company": { "id": "guid", "permissionCodes": ["users.list", "users.membership.approve"] } }`
- فقط مجوزهای **همان شرکت** و **همان زیرسیستم** که کاربر واقعاً دارد (مجوز یک زیرسیستم هرگز مجوز زیرسیستم دیگر را القا نکند)؛ شرکت بدون عضویت فعال ← 404؛ `no-store`؛ مجوز نقش غیرفعال/عضویت غیرفعال نیاید.
- UI فقط برای نمایش/مخفی‌کردن منو و کنترل‌ها استفاده می‌کند و مرجع امنیتی نیست؛ بعد از ۴۰۳/۴۰۴ هر endpoint پیام مناسب نشان می‌دهد؛ انقضا ← بارگذاری مجدد مجوز.

## ۳. گزارش امنیتی (Issue #512؛ Backend)
موجود: `GET /admin/security/audit-events` با فیلترهای exact `eventType,userId,companyId,fromUtc,toUtc`. فیلتر `companyId` و بازهٔ `DateTimeOffset` موجودند؛ مقدارهای ورودی به UTC تبدیل می‌شوند.
- **مرتب‌سازی:** `sort=occurredAt|eventType` و `descending=true|false`؛ فقط همین دو مقدار allowlist هستند، مقدار نامعتبر ۴۰۰؛ ترتیب ثانویهٔ پایدار `Id DESC`. هیچ نام ستون یا عبارت SQL از کلاینت وارد query نمی‌شود.
- **فهرست شرکت‌های نصب** (مدیر نصب): `GET /admin/companies?search=&page=&pageSize=` → `{ items:[{ id, name, isActive }], totalCount, page, pageSize }`؛ pageSize حداکثر ۱۰۰.
- **انواع رویداد:** `GET /admin/security/audit-event-types` → `[{ "code": "string", "label": "برچسب فارسی یا کد ناشناخته" }]`. API فهرست کدهای شناخته‌شده و کدهای موجود در سوابق را می‌دهد؛ UI نباید برچسب را از کد بسازد.
- **UTC:** تمام `OccurredAtUtc`های خروجی API با Kind=UTC و پسوند `Z` serialize می‌شوند؛ فیلترها `DateTimeOffset` هستند و مقایسه پس از تبدیل به UTC انجام می‌شود.
- **Export (CSV — Issue #507):** `POST /admin/security/audit-events/export` با بدنه JSON شامل `eventType?`, `userId?`, `companyId?`, `fromUtc`, `toUtc` → `text/csv; charset=utf-8` با BOM. POST اجباری است چون خروجی موفق رویداد `SecurityAuditExported` ثبت می‌کند و GET نباید side effect داشته باشد. فقط مدیر نصب؛ anti-forgery و rate limit؛ هر دو تاریخ اجباری، بازهٔ معکوس یا بیش از ۳۱ روز با ۴۰۰ رد می‌شود؛ ترتیب `OccurredAtUtc DESC, Id DESC`؛ سقف سخت ۱۰٬۰۰۰ ردیف و در صورت بیشتر بودن ۴۱۳. فقط projection ایمن (بدون IP/`DetailsJson`)، UTF-8 BOM، quoting/escaping مطابق RFC 4180 و CRLF؛ سلول‌های فرمولی و کنترل‌کاراکترهای ابتدای سلول با apostrophe خنثی می‌شوند. `SecurityAuditExported` با actor، فیلترهای غیرحساس و تعداد ردیف ثبت می‌شود؛ اگر ثبت Audit شکست بخورد، فایل تحویل داده نمی‌شود.
- **Retention — Proposed / NeedsApproval:** تا تصویب مالک، هیچ API، permission اجرایی، job یا پاک‌سازی Audit پیاده‌سازی نمی‌شود. پیشنهاد تصمیم D-070 در `docs/DECISIONS.md`؛ retention و legal hold باید بر اساس الزام قراردادی/قانونی تعیین شود. UI حق حذف رکورد ندارد.

## ۴. سوابق ورود/خروج و نشست‌ها (مرحلهٔ ۱۰)
- **سوابق ورود کاربر جاری (موجود):** `GET /me/login-history?take=`. برای فیلتر تاریخ و صفحه‌بندی: `?fromUtc=&toUtc=&page=&pageSize=` با همان آیتم و پاسخ `{ items, totalCount, page, pageSize }`.
- **نشست‌ها:** منتظر تأیید D-069 (`Proposed`، PR #505). پس از تأیید و پیاده‌سازی: `GET /me/sessions` → `[{ id, createdAtUtc, lastSeenAtUtc, isCurrent, status: "Active"|"Revoked"|"Idle" }]` (بدون IP/User-Agent)، `POST /me/sessions/{id}/revoke`، `POST /me/sessions/revoke-others`؛ ابطال نشست جاری Cookie را پاک کند؛ نشست کاربر دیگر ← 404. UI هیچ مدت نشست یا خروج را از نبود رویداد استنتاج نمی‌کند.
- ابطال نشست دیگران توسط مدیر نصب: تصمیم جدا و `NeedsApproval`.

## ۵. ساختار سازمانی S2 (مرحلهٔ ۸)
مدل `Proposed` است (`docs/ORGANIZATIONAL_STRUCTURE_DATA_MODEL.md`، PR #493): UI فقط پس از تصویب مدل و آماده‌شدن API. حداقل قرارداد خواندنی مورد نیاز UI: `GET /companies/{companyId}/org/units` (درخت)، `GET /companies/{companyId}/org/positions`، و در دفترچهٔ کاربران `unitName`/`positionName` اختیاری؛ ویرایش با مجوز مجزا. سمت/واحد با `CompanyRole` یکی نیست. همهٔ پرس‌وجوها فقط برای `companyId` مسیر و عضو فعال همان شرکت.

## ۶. مجوزهای گروه برای غیرمدیر نصب
`GET /api/v1/companies/{companyId}/roles/{roleId}` با مجوز صریح (مثلاً `users.roles.view`) فقط برای گروه‌های همان شرکت؛ پاسخ مانند `CompanyRoleResponse`. تا آن زمان مجوزهای گروه فقط برای مدیر نصب دیده می‌شود.
