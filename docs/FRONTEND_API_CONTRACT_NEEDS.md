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
- جستجو **اجباری و exact** است: پس از Trim طول ۳ تا ۱۲۰ نویسه؛ فهرست کامل/جستجوی prefix یا wildcard ارائه نمی‌شود. این endpoint فقط شرکت‌های فعال با `CustomerBoundaryId` غیرتهی و برابر با مرز حداقل یکی از عضویت‌های فعال کاربر را بررسی می‌کند. شرکت‌هایی که کاربر در آن‌ها عضویت فعال با نقش فعال یا درخواست Pending دارد، حذف می‌شوند. شرکت‌های بیرون از مرز هرگز در پاسخ یا شمارش نیایند؛ مرز null به معنی fail-closed است (D-037/D-038/D-065).
- Rate limit حساس، `no-store`، صفحه‌بندی با سقف ۱۰۰؛ خطا: 401 ناشناس/حساب غیرکاربر؛ 400 جستجوی خالی/کوتاه/بیش از ۱۲۰ نویسه. فهرست خالی خطا نیست.
- `POST /me/membership-requests` همان مرز را مستقلاً سمت سرور دوباره بررسی می‌کند؛ پنهان‌کردن مقصد در UI کنترل امنیتی محسوب نمی‌شود.

### ۱.۲ ثبت درخواست (موجود) — کدهای خطای مورد انتظار UI
`POST /me/membership-requests` → 201 `{ id, status, requestedAtUtc }`؛ 400 بدنه/Anti-forgery نامعتبر؛ 404 شرکت مقصد ناموجود/غیرفعال/خارج از CustomerBoundary (پاسخ یکسان، بدون افشا)؛ 409 عضو فعال یا درخواست Pending دارد. درخواست تا تأیید صریح مقصد هیچ دسترسی عملیاتی ایجاد نمی‌کند.

### ۱.۳ گروه‌های قابل‌تخصیص برای تأییدکننده
`GET /api/v1/admin/companies/{companyId}/membership-requests/assignable-roles`
- پاسخ ۲۰۰: `{ "items": [{ "id": "guid", "code": "string", "name": "string", "isSystemRole": false }], "totalCount": 0, "page": 1, "pageSize": 100 }`.
- پارامترهای `page` و `pageSize`؛ سقف pageSize برابر ۱۰۰.
- مجوز: `users.membership.approve` در همان شرکت (بدون مجوز ← 404). فقط گروه‌های فعال و غیرسیستمی همان شرکت که تمام permissionهایشان زیرمجموعهٔ مجوزهای فعال تأییدکننده در همان شرکت است؛ permission codeها برگردانده نشوند. این فهرست جایگزین کنترل مجدد سقف تفویض در POST approve نیست.

### ۱.۴ فهرست تأییدکنندگان مقصد
`GET /api/v1/admin/companies/{companyId}/membership-requests/approvers`
- پارامترهای `page` و `pageSize`؛ سقف pageSize برابر ۱۰۰. پاسخ ۲۰۰: `{ items: [{ userId, displayName, username }], totalCount, page, pageSize }`.
- مجوز: `users.membership.approve` همان شرکت (بدون مجوز ← 404). فقط کاربران فعال با عضویت و نقش فعال همان شرکت که permission `users.membership.approve` را دارند؛ پاسخ allowlist است و ایمیل/تلفن/نقش/permissionهای دیگر برنمی‌گردد.

### ۱.۵ سوابق درخواست و تصمیم
`GET /api/v1/admin/companies/{companyId}/membership-requests?status=Pending|Approved|Rejected&page=&pageSize=`
- آیتم: `{ id, requesterDisplayName, username, status, requestedAtUtc, decidedAtUtc, decidedByDisplayName, approvedRoleName, decisionReason }`؛ مجوز مانند ۱.۳؛ status اختیاری و فقط همین سه مقدار؛ زمان‌های `*Utc` با پسوند Z. این endpoint تاریخچهٔ درخواست‌ها/تصمیم‌ها را می‌دهد؛ مدل مستقل تاریخچهٔ همهٔ تغییرات عضویت هنوز وجود ندارد و نباید جعل شود.
- درخواست‌کننده سوابق خودش را از `GET /me/membership-requests?status=&page=&pageSize=` می‌بیند؛ فقط درخواست‌های متعلق به خودش.

## ۲. مجوزهای کاربر جاری و منو (مرحلهٔ ۹)
`GET /api/v1/me/permissions` — بدون پارامتر `companyId`.
- پاسخ ۲۰۰: `{ "isInstallationAdministrator": false, "company": { "id": "guid", "permissionCodes": ["users.list", "users.membership.approve"] } }`؛ اگر زمینهٔ شرکت فعالی برای همین sign-in وجود نداشته باشد، `company: null` برمی‌گردد.
- شرکت فقط از زمینهٔ کاری محافظت‌شدهٔ همین نشست، با `WorkContextService.GetCurrentAsync` تعیین می‌شود؛ شناسهٔ شرکت ارسالی در query/route/body یا claim خوانده نمی‌شود و هیچ شرکت دیگری خودکار انتخاب نمی‌شود.
- مجوزها از نقش فعال عضویت فعال همان شرکت، از طریق `CompanyAccessService` خوانده می‌شوند؛ مجوزهای شرکت دیگر حتی اگر کاربر عضو آن باشد افشا نمی‌شود. `isInstallationAdministrator` از نقش فعال DB در هر درخواست محاسبه می‌شود، نه Claim قدیمی.
- پاسخ `Cache-Control: no-store` و `Pragma: no-cache`؛ بدون cache. کاربر بدون زمینهٔ فعال پاسخ ۲۰۰ با `company: null` می‌گیرد؛ ناشناس یا Claim شناسه نامعتبر ۴۰۱؛ حساب احرازشدهٔ غیرکاربر به‌علت RequireClaim با ۴۰۳ رد می‌شود.
- UI فقط برای نمایش/مخفی‌کردن منو و کنترل‌ها استفاده می‌کند و مرجع امنیتی نیست؛ هر endpoint باید مجوز خودش را سمت سرور بررسی کند.

## ۳. گزارش امنیتی (Issue #512؛ Backend)
موجود: `GET /admin/security/audit-events` با فیلترهای exact `eventType,userId,companyId,fromUtc,toUtc`. فیلتر `companyId` و بازهٔ `DateTimeOffset` موجودند؛ مقدارهای ورودی به UTC تبدیل می‌شوند.
- **مرتب‌سازی:** `sort=occurredAt|eventType` و `descending=true|false`؛ فقط همین دو مقدار allowlist هستند، مقدار نامعتبر ۴۰۰؛ ترتیب ثانویهٔ پایدار `Id DESC`. هیچ نام ستون یا عبارت SQL از کلاینت وارد query نمی‌شود.
- **فهرست شرکت‌های نصب** (مدیر نصب): `GET /admin/companies?search=&page=&pageSize=` → `{ items:[{ id, name, isActive }], totalCount, page, pageSize }`؛ pageSize حداکثر ۱۰۰.
- **انواع رویداد:** `GET /admin/security/audit-event-types` → `[{ "code": "string", "label": "برچسب فارسی یا کد ناشناخته" }]`. API فهرست کدهای شناخته‌شده و کدهای موجود در سوابق را می‌دهد؛ UI نباید برچسب را از کد بسازد.
- **UTC:** تمام `OccurredAtUtc`های خروجی API با Kind=UTC و پسوند `Z` serialize می‌شوند؛ فیلترها `DateTimeOffset` هستند و مقایسه پس از تبدیل به UTC انجام می‌شود.
- **Export (CSV — Issue #507):** `POST /admin/security/audit-events/export` با بدنه JSON شامل `eventType?`, `userId?`, `companyId?`, `fromUtc`, `toUtc` → `text/csv; charset=utf-8` با BOM. POST اجباری است چون خروجی موفق رویداد `SecurityAuditExported` ثبت می‌کند و GET نباید side effect داشته باشد. فقط مدیر نصب؛ anti-forgery و rate limit؛ هر دو تاریخ اجباری، بازهٔ معکوس یا بیش از ۳۱ روز با ۴۰۰ رد می‌شود؛ ترتیب `OccurredAtUtc DESC, Id DESC`؛ سقف سخت ۱۰٬۰۰۰ ردیف و در صورت بیشتر بودن ۴۱۳. فقط projection ایمن (بدون IP/`DetailsJson`)، UTF-8 BOM، quoting/escaping مطابق RFC 4180 و CRLF؛ سلول‌های فرمولی و کنترل‌کاراکترهای ابتدای سلول با apostrophe خنثی می‌شوند. `SecurityAuditExported` با actor، فیلترهای غیرحساس و تعداد ردیف ثبت می‌شود؛ اگر ثبت Audit شکست بخورد، فایل تحویل داده نمی‌شود.
- **Retention — Accepted / NotImplemented (D-070، 2026-11-06):** baseline عادی/حساس/بحرانی ۱۲/۲۴/۳۶ ماه، Legal Hold و Archive چندلایه Cloud مصوب‌اند؛ تا Issue اجرایی هیچ API، permission اجرایی، job یا Purge پیاده‌سازی نمی‌شود. UI حق حذف رکورد ندارد.

## ۴. سوابق ورود/خروج و نشست‌ها (مرحلهٔ ۱۰)
- **سوابق ورود کاربر جاری (موجود):** `GET /me/login-history?take=`. برای فیلتر تاریخ و صفحه‌بندی: `?fromUtc=&toUtc=&page=&pageSize=` با همان آیتم و پاسخ `{ items, totalCount, page, pageSize }`.
- **نشست‌ها (پیاده‌سازی‌شده — 2026-11-05، D-069 `Implemented`):** `GET /api/v1/me/sessions` → آرایه‌ای از نشست‌های **فعال** کاربر جاری با فیلدهای allowlist `{ "sessionId": "guid", "isCurrent": true, "createdAtUtc": "...Z", "lastSeenAtUtc": "...Z" }`؛ مرتب‌سازی `lastSeenAtUtc DESC, sessionId DESC`؛ نشست‌های ابطال‌شده یا خارج از پنجرهٔ بی‌کاری ۳۰ دقیقه فهرست نمی‌شوند و `status` وجود ندارد (فقط active برگردانده می‌شود). بدون IP/User-Agent. `POST /api/v1/me/sessions/{sessionId}/revoke` → `204` (idempotent؛ شناسهٔ ناموجود یا متعلق به کاربر دیگر همان `404`؛ ابطال نشست جاری Cookie را sign-out می‌کند)؛ `POST /api/v1/me/sessions/revoke-others` → `200 { "revokedCount": n }` (نشست جاری حفظ می‌شود). همه سه endpoint با anti-forgery و rate limit حساس و `no-store`؛ رویدادهای Audit `UserSessionRevoked` و `UserSessionsRevokedOthers` در گزارش امنیتی با برچسب فارسی می‌آیند. تغییر رمز همهٔ نشست‌های فعال را ابطال می‌کند و خروج صریح نشست جاری را می‌بندد. UI هیچ مدت نشست یا خروج را از نبود رویداد استنتاج نمی‌کند.
- ابطال نشست دیگران توسط مدیر نصب: `Accepted` در D-073؛ endpoint و UI هنوز `NotImplemented` هستند.

## ۵. ساختار سازمانی S2 (مرحلهٔ ۸)
مدل `Proposed` است (`docs/ORGANIZATIONAL_STRUCTURE_DATA_MODEL.md`) و طراحی سطح پیاده‌سازی آن (Entityها، Constraintها، کاتالوگ مجوز `hr.*` و قرارداد کامل API) به‌همراه ADR پیشنهادی D-072 در همان سند نهایی شده است (2026-11-05). قرارداد پیاده‌سازی شد (D-072 Accepted؛ Backend `Implemented` 2026-11-05): مسیرها عیناً مطابق بخش ۶ سند طراحی روی `/api/v1/companies/{companyId}/org/*` فعال‌اند، مجوزهای `hr.*` در کاتالوگ ثبت شده‌اند و `GET`های جزئیات، `rowVersion` را برای PUTها برمی‌گردانند. UI (فاز ۴) می‌تواند مستقیماً روی همین قرارداد ساخته شود؛ گردش‌کارهای عملیاتی فاز ۴ نیز فعال‌اند: `GET .../org/units/{id}/subtree` (درخت با شمار انتساب)، `GET .../org/units/{id}/employees`، `GET .../org/positions/{id}/employees` و `GET .../org/employees/{id}/history` (تاریخچه تغییرات از Audit) — صفحات پیشنهادی UI: درخت سازمانی با جزئیات گره، فرم کارمند با کارت انتساب فعال و تاریخچه، و فهرست سمت‌ها با دارندگان؛ چکیده خواندنی‌های مورد نیاز UI: `GET /api/v1/companies/{companyId}/org/units?view=tree`، `GET .../org/positions`، `GET .../org/employees` و `GET .../org/assignments?employeeId=` با مجوزهای `hr.organizational-units.list`، `hr.positions.list`، `hr.employees.list` و `hr.employees.view`؛ ویرایش‌ها با مجوزهای مجزای `*.create`/`*.update` و `hr.assignments.manage` سمت سرور enforce می‌شوند. سمت/واحد با `CompanyRole` یکی نیست. همهٔ پرس‌وجوها فقط برای `companyId` مسیر و عضو فعال همان شرکت؛ پاسخ‌ها Allowlist و بدون فیلد حساس کاربر.
- **قرارداد تفصیلی مصرف Frontend:** سند مستقل `docs/HR_FRONTEND_API_CONTRACT.md` (2026-11-05) ماتریس کامل ۲۵ Endpoint، DTOهای ورودی/خروجی واقعی، نمونه‌های JSON منطبق با کد، خطاها، صفحه‌بندی، RowVersion، UTC، Tenant Scope، Audit و قواعد Assignment را پوشش می‌دهد؛ منبع حقیقت قرارداد مصرفی UI همین سند است. در همان به‌روزرسانی، گارد غیرفعال‌سازی واحد/سمت (۴۰۹ با انتساب/فرزند فعال) روی مسیرهای `PUT` نیز یکسان‌سازی شد و خاتمه‌همکاری قبل از تاریخ شروع با ۴۰۰ رد می‌شود.

## ۶. مجوزهای گروه برای غیرمدیر نصب
`GET /api/v1/companies/{companyId}/roles/{roleId}` با مجوز صریح (مثلاً `users.roles.view`) فقط برای گروه‌های همان شرکت؛ پاسخ مانند `CompanyRoleResponse`. تا آن زمان مجوزهای گروه فقط برای مدیر نصب دیده می‌شود.


## ۴.۱ تصمیم‌های D-073 که هنوز API مصرفی ندارند
بازیابی رمز، Reset آغازشده توسط مدیر و MFA از نظر محصول پذیرفته شده‌اند، اما endpoint/DTO واقعی در main وجود ندارد. Frontend نباید مسیر، payload یا موفقیت فرضی بسازد؛ قرارداد API فقط پس از Issue اجرایی و بررسی کد واقعی به‌روزرسانی می‌شود.
