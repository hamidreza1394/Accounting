# معماری مدیریت هویت و دسترسی چندشرکتی فرانو

- Issue: #444
- ADR: D-065 (Accepted, 2026-11-03)
- وضعیت: تصمیم محصول و معماری مصوب؛ پیاده‌سازی runtime هنوز کامل یا Verified نیست.

## 1. اصول و مرزها

- Installation/Identity Realm مرز استقرار و دیتابیس است. شناسه ورود در همان realm یکتا است؛ نصب‌های مستقل می‌توانند username مشابه داشته باشند.
- Company همان مرز Tenant فعلی است و TenantId به Companies.Id اشاره می‌کند. موجودیت Tenant جدید نسازید.
- CorporateGroup فقط ساختار گروهی است؛ عضویت گروه، مالکیت یا دامنه به‌تنهایی مجوز داده ایجاد نمی‌کند.
- یک User در هر نصب یک هویت دارد و می‌تواند چند UserCompanyMembership مستقل داشته باشد.
- FiscalPeriod هسته، منبع واحد دوره مالی تمام زیرسیستم‌هاست. TradeOperationalPeriod یا تقویم موازی ممنوع است.
- CompanyRole، CompanyRolePermission و PermissionDefinition موتور مجوز شرکتی باقی می‌مانند؛ موتور Role/Permission موازی نسازید.
- entitlement تجاری لایسنس از permission عملیاتی کاربر مستقل است و هر دو شرط لازم‌اند.

## 2. چهار تصمیم قطعی D-065

1. تخصیص دسترسی مالی هم در سطح سال مالی و هم در سطح دوره مالی مشخص پشتیبانی می‌شود.
2. مدیر شرکت فقط directory محدود و read-only کاربران شرکت‌های عضو همان CorporateGroup را می‌بیند.
3. هر دو نوع دامنه اختصاصی Company و دامنه CorporateGroup پشتیبانی می‌شوند.
4. تمام عضویت‌های جدید بین‌شرکتی نیازمند تأیید صریح شرکت مقصد هستند.

## 3. تصمیم دسترسی مؤثر

برای هر درخواست، سرور باید تمام شروط مرتبط را ارزیابی کند: هویت معتبر و فعال؛ Company context معتبر؛ عضویت Approved و فعال؛ grant سال/دوره در عملیات مالی؛ entitlement فعال در صورت نیاز؛ permission صریح؛ و DataScope معتبر در صورت وجود.

هر شرط ناموجود، منقضی، مبهم یا غیرقابل اعتبارسنجی به deny منتهی می‌شود. RBAC از CompanyRole و CompanyRolePermission استفاده می‌کند. شرایط شرکت، عضویت، دوره، تاریخ اعتبار و محدوده داده با policyهای سروری ارزیابی می‌شوند. UI، Host، URL، claim بلندعمر و شناسه ارسالی کلاینت مرجع نهایی مجوز نیستند.

## 4. هویت و عضویت

- یک User برای هر Identity Realm؛ عضویت و نقش در هر Company مستقل است.
- تمام عضویت‌های جدید بین‌شرکتی ابتدا PendingApproval هستند.
- درخواست‌کننده نمی‌تواند درخواست خودش را تأیید کند.
- فقط مدیر/نقش دارای مجوز صریح در Company مقصد می‌تواند approve/reject کند.
- تا Approved شدن هیچ داده، دوره، API عملیاتی یا export مقصد در دسترس نیست.
- تأیید عضویت به‌تنهایی role یا grant مالی ایجاد نمی‌کند؛ تخصیص نقش و سال/دوره جداست.
- lifecycle حداقل شامل PendingApproval, Approved, Rejected, Suspended, Revoked و Expired است.
- actor، زمان UTC، reason، correlation و transition باید audit شوند.
- تعلیق یا لغو باید در درخواست بعدی مؤثر شود؛ cache/session authorization باید invalidate شود.

### 4.1 ممیزی S2 — ساختار سازمانی و ارتباط با هویت (2026-11-03)

- Domain شامل User و UserCompanyMembership است، اما Entity مستقل Employee/Personnel، OrganizationalUnit یا Position در مخزن وجود ندارد؛ ماژول Payroll نیز در ساختار فعلی پیدا نشد.
- User هویت نصب است و می‌تواند در چند Company عضویت مستقل داشته باشد. UserCompanyMembership دسترسی عملیاتی و CompanyRole را نگه می‌دارد؛ employment/HR record نیست.
- D-064 می‌گوید سمت و واحد داده منابع انسانی‌اند و به‌خودی‌خود مجوز نمی‌دهند. بنابراین افزودن مستقیم Position/Unit به User سراسری اشتباه است و اتصال آن‌ها به CompanyRole نیز ممنوع است.
- طرح پیشنهادی در docs/ORGANIZATIONAL_STRUCTURE_DATA_MODEL.md دو گزینه دارد: فیلدهای ساده روی membership (فقط کاربران دارای login) یا CompanyEmployee مستقل با لینک اختیاری به membership (پشتیبانی از کارکنان بدون حساب). گزینه دوم برای ERP/Payroll آینده مناسب‌تر است، اما نیازمند تأیید مالک محصول است.
- به‌روزرسانی 2026-11-05 (پیاده‌سازی): مالک اجرای طراحی را صریحاً دستور داد؛ D-072 به `Accepted` ارتقا یافت و گزینه B به‌صورت کامل در Backend پیاده شد — چهار Entity شرکتمحور، Migration زنجیره‌ای `20261104200301_AddHrOrganizationalStructure` (ساختار + CHECK + seed ۱۲ کد `hr.*`)، Endpointهای `/org/*` با مجوزدهی دوحالته D-071، Audit، تست‌های Unit/Endpoint/SQL-gated/E2E. UI در ClientApp در فاز ۴ ادامه می‌یابد.
- مرز امنیتی پابرجاست: Employee/Unit/Position/Assignment و ManagerEmployeeId هیچ مجوزی ایجاد نمی‌کنند؛ RBAC همچنان CompanyRole/CompanyRolePermissions/PermissionDefinitions و CompanyAccessService است.

## 5. تخصیص سال و دوره مالی

| نوع تخصیص | معنا |
|---|---|
| Company-scoped | عملیات اداری غیرمالی که به دوره وابسته نیست |
| Fiscal-year grant | اجازه در سطح سال با policy صریح درباره دوره‌های تحت پوشش |
| Fiscal-period grant | اجازه به یک یا چند FiscalPeriod مشخص |

هر عملیات period-scoped باید دوره را از WorkContext معتبر سمت سرور بگیرد و تعلق دوره به همان Company را کنترل کند. دوره Closed/Archived طبق policy هسته فقط خواندنی یا غیرقابل استفاده است.

دسترسی سال به‌صورت ضمنی دوره جدیدی را که بعداً به سال اضافه می‌شود مجاز نمی‌کند؛ مگر grant صریحاً نوع AllPeriodsInYear داشته باشد و این policy توسط مدیر مجاز انتخاب شده باشد. اگر مدل فعلی سال را به‌صورت entity جداگانه ندارد، YearId جدید نسازید؛ ابتدا مدل FiscalPeriod و قواعد تقویم موجود بررسی شود.

نام FiscalAccessGrant صرفاً نام مفهومی است، نه schema نهایی. ممکن است سال از دوره‌های موجود مشتق شود. چند grant نباید با تکثیر عضویت کاربر در شرکت پیاده‌سازی شوند.

### 5.1 تصمیم نهایی مدل سال/دوره مالی (D-066 Accepted، 2026-11-08)

- مالک محصول گزینه A را تأیید کرد: `FiscalPeriod` موجود نماینده سال مالی شرکت است؛ `FiscalYear` جدید ساخته نمی‌شود.
- برای الزام دسترسی دوره‌ای D-065، `AccountingPeriod` فرزند FiscalPeriod ایجاد می‌شود. دوره‌ها متعلق به همان Tenant و همان سال‌اند و تقویم موازی در ماژول‌ها ساخته نمی‌شود.
- تخصیص نقش از عضویت مستقل است. تخصیص Company-scoped برای عملیات اداری غیرمالی و تخصیص Fiscal-year-scoped برای عملیات مالی/دوره‌ای در یک مدل واحد ثبت می‌شوند.
- تخصیص سالانه دارای policy صریح `ExplicitPeriodsOnly` یا `AllPeriodsInYear` است. اولی فقط دوره‌های انتخاب‌شده را مجاز می‌کند؛ دومی دوره‌های فعلی و آینده همان سال را پوشش می‌دهد.
- جزئیات Entityها، composite FKها، یکتایی، backfill، copy preview، concurrency و تست‌ها در `docs/FISCAL_ACCESS_DATA_MODEL.md` است.
- Issue #488 از نظر معنای دامنه حل شده است؛ runtime/schema هنوز NotImplemented تا PRهای اجرایی و شواهد واقعی SQL Server.

## 6. دفترچه محدود شرکت‌های هم‌گروه

Directory فقط کاربران شرکت‌هایی را نشان می‌دهد که عضویت معتبر در همان CorporateGroup دارند. داده پیش‌فرض محدود به نام نمایشی، شناسه/نام کاربری حداقلی، شرکت محل عضویت و وضعیت کلی عضویت است. جستجو و صفحه‌بندی server-side است.

به‌صورت پیش‌فرض ممنوع: رمز و hash، token، MFA secret، recovery code، LoginHistory، session، IP، device fingerprint، جزئیات audit، role و permission داخلی شرکت دیگر. تلفن و ایمیل فقط با مجوز مستقل و ضرورت روشن. Directory هیچ mutation، approval یا مدیریت عضویت در شرکت دیگر را ممکن نمی‌کند و هیچ مجوز عملیاتی ایجاد نمی‌کند.

## 7. دامنه‌های ورود

هر دو نوع دامنه پشتیبانی می‌شوند:

- Company domain به یک Company فعال و verified resolve می‌شود.
- Group domain به یک CorporateGroup فعال و verified resolve می‌شود؛ پس از login فقط شرکت‌هایی نمایش داده می‌شوند که کاربر در آن‌ها عضویت Approved و فعال دارد.
- Custom domain آینده از همان قرارداد verified-domain استفاده می‌کند.

هر Host canonicalized فقط یک مقصد فعال دارد. Host ناشناخته، غیرفعال، تأییدنشده، متداخل یا مبهم fail-closed است. Host فقط LoginContext می‌سازد و authorization نیست. Forwarded headers فقط از proxyهای allowlisted پذیرفته می‌شوند. TLS اجباری و cookie ترجیحاً host-only با Secure و HttpOnly و SameSite مناسب است. دامنه گروه هیچ عضویت یا role جدیدی ایجاد نمی‌کند.

## 8. API و مرز ماژول

قراردادهای کاربردی نسخه‌دار و DTO-based برای این قابلیت‌ها تعریف شوند: ResolveHost، GetMyCompanies، RequestMembership، DecideMembership، GrantYearAccess، GrantPeriodAccess، SearchGroupUsers و AuthorizationDecision.

مسیرهای HTTP دقیق باید با convention موجود مخزن تطبیق داده شوند. قراردادها نباید Entity یا DbContext داخلی را افشا کنند. ماژول مصرف‌کننده به جداول داخلی ماژول دیگر reference مستقیم نمی‌دهد؛ در monolith از application contract و adapter استفاده می‌شود. mutationها anti-forgery، audit و کنترل هم‌زمانی دارند.

## 9. تهدیدها و کنترل‌ها

| تهدید | کنترل |
|---|---|
| Cross-company IDOR | CompanyAccessService، query scoping و تست منفی |
| دسترسی از عضویت گروه | membership صریح و permission مقصد |
| نشت از Pending | بررسی status در تمام query/API/export/job |
| Host spoofing | verified mapping، canonicalization و trusted proxy |
| نشت دوره‌ای | اعتبارسنجی Company + FiscalPeriod + grant |
| دسترسی مانده پس از revoke | revalidation و cache/session invalidation |
| ارتقای دسترسی | منع self-grant، delegation ceiling و maker-checker |
| افشای directory | DTO allowlist و جداسازی API |
| race در approval | rowversion/transaction و unique constraints |
| cache bleed | کلید cache شامل user/company/period/permission version |

## 10. داده و rollout

1. بررسی مدل واقعی Company، FiscalPeriod، UserCompanyMembership، CompanyRole، CorporateGroup و تمام FK/index/query filterها.
2. گزارش orphan و duplicate پیش از افزودن FK یا unique constraint.
3. طراحی lifecycle و audit به‌صورت additive؛ backfill کنترل‌شده.
4. migration به ترتیب expand، backfill، validate و enforce با rollback plan.
5. تست SQL Server روی دیتابیس تازه و ارتقایافته.
6. Domain mapping با unique canonical host و constraint نوع مقصد.
7. هیچ Migration روی دیتابیس مشتری بدون approval مستقل اجرا نشود.

## 11. مسیر استخراج

- هر ماژول قراردادهای Application مستقل و مالکیت schema/migration روشن دارد.
- هسته مالک Company، User identity و FiscalPeriod است؛ ماژول‌ها فقط از قراردادهای مجاز مصرف می‌کنند.
- استخراج سرویس مستقل نیازمند API contract versioning، service-to-service identity، انتقال مالکیت داده، reconciliation، audit continuity، contract tests، deployment/backup/restore و rollback مستقل است.
- Outbox/Inbox فقط در مرز consistency واقعی اضافه می‌شود؛ microservice یا broker صرفاً برای معماری‌نمایی ساخته نمی‌شود.
- قطع سرویس authorization یا عدم امکان اعتبارسنجی به deny منتهی می‌شود.

## 12. نقشه اجرایی

| Slice | خروجی | معیار پایان |
|---|---|---|
| A | Reconcile PRهای #445..#456 و branch bases | مبنای یکپارچه و بدون تغییر تکراری |
| B | Membership lifecycle و approval | Pending deny و maker-checker |
| C | Fiscal year/period grants | مدل واقعی + تست SQL Server |
| D | Domain mapping و host resolution | spoof/ambiguous-host tests |
| E | Group directory | allowlisted DTO و cross-group tests |
| F | React RTL workflows | API/UI integration tests |
| G | session, audit, cache, concurrency | security regression |
| H | extraction readiness | contract tests و نبود coupling داخلی |

این ADR به معنی جداسازی فوری دیتابیس یا الزام به microservice نیست. ساخت License engine، Migration مشتری و موتور تلفیق مالی خارج از دامنه آن است.


## ۴. تصمیم‌های تکمیلی امنیت حساب و Entitlement (D-073/D-074)

- D-073: مدیر نصب می‌تواند نشست یک کاربر دیگر را فقط در همان Installation، با احراز هویت مجدد، علت اجباری و Audit باطل کند؛ مدیر شرکت اختیار revoke نشست دیگران ندارد. Forgot Password و Admin Reset فقط فرایند user-controlled هستند؛ مدیر رمز را تعیین/مشاهده نمی‌کند. مدیر شرکت برای آغاز Reset به مجوز مستقل `users.credentials.reset` و عضویت فعال همان Company نیاز دارد.
- MFA برای مدیر نصب، مدیران شرکت و حساب‌های حساس اجباری است؛ Passkey/WebAuthn ترجیحی، TOTP جایگزین آفلاین، Recovery Code یک‌بارمصرف؛ SMS/Email OTP عامل دوم نیستند.
- D-074: Permission و Entitlement دو موتور مستقل‌اند و هر دو برای قابلیت‌های لایسنس‌دار لازم‌اند. ترتیب canonical: Authentication/Session → Company membership/context → FiscalPeriod/grant → Entitlement → operational Permission → DataScope/use case.
- هیچ لایسنس یا Feature خریداری‌شده‌ای Permission نمی‌سازد و هیچ Role/Permission لایسنس ایجاد نمی‌کند. Cloud از authority سمت سرور و cache نسخه‌دار/دارای expiry؛ Offline از Envelope امضاشده و sequence/anti-rollback استفاده می‌کند.
- وضعیت پیاده‌سازی جداست: D-073 عمدتاً NotImplemented؛ در D-074 قرارداد v2 و Trade gate موجودند اما runtime Trade هنوز با DenyAll fail-closed است و تعمیم همه ماژول‌ها باقی است.

### 4.2 عضویت بدون نقش و ایجاد کاربر شرکتی (D-078، پیاده‌شده 2026-11-10)

- `UserCompanyMemberships.RoleId` اختیاری است؛ عضویت فعال بدون نقش معتبر است اما **هیچ** دسترسی عملیاتی نمی‌دهد: `CompanyAccessService` فقط عضویت‌های دارای نقش فعال را می‌شناسد (fail-closed) و عضو بدون نقش از زمینه کاری، دفترچه مجوز و همه endpointها خارج می‌ماند؛ در دفترچه شرکت با فیلدهای نقش null نمایش داده می‌شود.
- ایجاد User برای یک شرکت (`POST /companies/{companyId}/users`، مجوز `users.create`) عضویت همان شرکت را مستقیم و فعال می‌کند؛ برای شرکت دیگر — حتی زیرمجموعه یا هم‌گروه — هیچ مسیر مستقیمی وجود ندارد و گردش درخواست/تأیید مقصد (بند 4) تنها راه است. مالکیت هلدینگ، گروه یا دامنه هیچ مجوز تأییدی در مقصد نمی‌سازد.
- تخصیص نقش همچنان فقط از `roles.assign` با سقف D-071 انجام می‌شود؛ تخصیص برای سال‌های مختلف (D-077) به عضویت جدید نیاز ندارد و عضویت هیچ grant مالی نمی‌سازد.
