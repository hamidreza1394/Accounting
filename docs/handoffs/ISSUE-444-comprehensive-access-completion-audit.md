# ممیزی جامع تکمیل مدیریت کاربران، سازمان و دسترسی‌ها — Issue #444

- **Issue:** #444 (`feat(admin): build unified user, organization and access management`)
- **Agent/مسئول:** GLM (Z.ai) — مأموریت جامع تکمیل مدیریت کاربران، سازمان و دسترسی‌ها
- **تاریخ:** 2026-11-06
- **وضعیت:** ممیزی کامل انجام شد؛ نواقص واقعی شناسایی و برنامه اجرا ثبت شد
- **مبنای ممیزی:** آخرین `origin/main` @ `129b93b5def7443d18e39571f3d7cc48a3010504` (Merge PR #561)

## هدف و معیارهای پذیرش

- [x] وضعیت واقعی `main`، Issue #444 و PRهای مرتبط (#559، #560، #561 و PRهای قبلی) با API واقعی GitHub بررسی شد
- [x] هر قابلیت مأموریت با کد واقعی، تست‌ها و مستندات در یکی از سه وضعیت Completed / Partially Implemented / Not Implemented طبقه‌بندی شد
- [x] هیچ قابلیت تکمیل‌شده‌ای بازنویسی نشد
- [x] نواقص واقعی و ترتیب اجرای مستقل آن‌ها ثبت شد
- [ ] برچسب «Completed» فقط با شاهد کد/تست صادر شده است (این سند) و نواقص باقیمانده در PRهای اجرایی جداگانه رفع می‌شوند

## روش ممیزی

۱) دریافت وضعیت GitHub API: Issue #444 (Open)، تمام PRهای ذکرشده در مأموریت، فهرست PRهای Open (صفر) و Sub-Issueها.
۲) مطالعه اسناد حاکم: `AGENTS.md`، `docs/INDEX.md`، `docs/DECISIONS.md` (D-069، D-070، D-071، D-072، D-073، D-074)، `docs/api/USER_ACCESS_MANAGEMENT_API.md` و handoffهای `HR_FRONTEND_INTEGRATION.md`، `MEMBERSHIP_REQUEST_WORKFLOW_COMPLETION.md`، `SECURITY_AUDIT_UI_COMPLETION.md`.
۳) بررسی کد واقعی Backend (`Server/Api/*`، `Server/Infrastructure/Security/*`، `Domain/`) و تست‌ها (`tests/Template.Tests/*`).
۴) اجرای واقعی Build و تست کامل سولوشن روی main تمیز (جدول پایین).

## نتیجه پایه اجرای واقعی روی main تمیز (`129b93b5`)

| فرمان | نتیجه واقعی |
|---|---|
| `scripts/dev/install-dotnet.sh` | نصب SDK 10.0.403 مطابق `global.json` (SHA-512 تأیید شد) |
| `dotnet restore Template.sln` | موفق |
| `dotnet build Template.sln --no-restore -c Release` | موفق — ۰ خطا (۲۸۶۴ هشدار موجودِ قبلی) |
| `dotnet test Template.sln --no-build -c Release` | **۱۰۳۸ موفق، ۲ ناموفق، ۶۲ Skip** |

- دو تست ناموفق = `SafeExceptionRealApplicationTests` (راه‌اندازی برنامهٔ واقعی نیازمند SQL Server)؛ همان نتیجه‌ای که handoffهای قبلی (PR #561) برای main تمیز ثبت کرده‌اند — پیش‌موجود و محیط‌مند، بدون ارتباط با این ممیزی.
- ۶۲ Skip = تستهای SQL Server-gated (`FARANOU_TEST_SQLSERVER`)؛ در CI با SQL Server واقعی اجرا می‌شوند.
- Docker در این محیط در دسترس نیست؛ SQL Server محلی اجرا نشد و هیچ نتیجه‌ای به‌عنوان «SQL Server موفق» ادعا نشد.

## وضعیت PRهای مبنا (GitHub API، 2026-11-06)

| PR | موضوع | وضعیت |
|---|---|---|
| #559 | ادغام مدیریت سازمانی HR در Admin Shell (`AppRoutes`/`AppShell`/`lib/permissions`) | **Merged** (head `2c4571e`) |
| #560 | تکمیل گردش درخواست عضویت (فقط مستندات؛ کد از قبل کامل) | **Merged** (head `2e05b98`) |
| #561 | تکمیل UI گزارش امنیتی (مرتب‌سازی، انواع رویداد، انتخاب شرکت نصب، CSV) | **Merged** (head `e91ea42`) |
| همه PRهای Open مخزن | — | **صفر** (هیچ کار موازی در جریان نیست) |

## طبقه‌بندی قابلیت‌های مأموریت (با شواهد کد)

### مرحله ۱ — مدیریت کاربران و عضویت‌ها: `Completed`
- فهرست/جزئیات/ایجاد/ویرایش کاربر با صفحه‌بندی و جستجوی سمت سرور: `Server/Api/UserAdministrationEndpoints.cs` (فقط مدیر نصب با بازخوانی DB در هر درخواست — `AuthorizeInstallationAdministratorAsync`؛ anti-forgery، rate limit حساس، no-store، Audit `UserCreated`/`UserUpdated`، گارد «آخرین مدیر فعال» با تراکنش Serializable).
- گردش درخواست عضویت (کشف، ثبت، صف، تأیید/رد با سقف تفویض داخل تراکنش): `Server/Api/CompanyMembershipRequestEndpoints.cs` — ماتریس ۹ عملیاتی در handoff PR #560 همه `Verified`.
- جلوگیری از عضویت تکراری: بررسی Query + ایندکس یکتای فیلترشده + 409؛ Tenant Isolation با بازبینی مرز مشتری سمت سرور.
- فعال/غیرفعال‌سازی حساب: `UpdateUserAsync` با گارد خود-غیرفعال‌سازی و آخرین مدیر.
- ارتباط پرسنلی از مدل مصوب HR (D-072) و نه مدل موازی: اتصال اختیاری Employee↔Membership با مجوز `hr.employees.user-link.manage`.
- تست‌ها: `tests/Template.Tests/UserAdministrationEndpointTests.cs`، `CompanyMembershipRequestEndpointTests.cs` و E2E `membership-workflow.spec.mjs` (CI).

### مرحله ۲ — ساختار سازمانی و ارتباط با کاربران: `Completed`
- مدل چهارگانه HR + `HrRules` + ۲۵ Endpoint `/org/*` (PR #543/#545) و UI کامل HR با ادغام در Shell (PR #559: ثبت ۱۰ مسیر در `AppRoutes.tsx`، سه آیتم منو با hint مجوز، `hr.units.view`/`hr.positions.view`/`hr.employees.view`).
- قرارداد مصرف Frontend: `docs/HR_FRONTEND_API_CONTRACT.md`.
- تفکیک هویتی/پرسنلی/دسترسی: User ≠ Employee (D-072)؛ Membership فقط با مجوز مخصوص به Employee متصل می‌شود.

### مرحله ۳ — نقش‌ها و مجوزها: `Completed`
- API نقش‌های شرکتی با سقف تفویض D-071: `Server/Api/CompanyRoleAdministrationEndpoints.cs` (اعضای نقش، انتقال اعضا، بازخوانی سقف داخل تراکنش، گارد RowVersion).
- کاتالوگ مجوز و الگوها؛ دفترچه کاربران شرکت (`users.list`)؛ مجوز مؤثر کاربر جاری (`GET /me/permissions` — Issue #511).
- منع تغییر نقش‌های سیستمی و منع self-approval؛ Audit کامل؛ تست‌های `CompanyRoleAdministrationEndpointTests.cs`، `CompanyRoleDelegationCeilingEndpointTests.cs` و E2E `role-admin.spec.mjs`.

### مرحله ۴ — ابطال مدیریتی نشست کاربر دیگر (D-073 بخش ۱): `Not Implemented` ← **نقص اصلی قابل اجرا**
- شواهد: هیچ Endpoint مدیریتی برای نشست در `Server/Api/` وجود ندارد؛ handoff PR #561 صریحاً «Endpoint مدیریتی نشست وجود ندارد؛ نیاز مستقل ذیل D-073» را ثبت کرده است.
- D-069 (Implemented) زیرساخت کامل را فراهم کرده است: جدول `UserSession` با `RevokedByUserId`/`RevocationReasonCode`/RowVersion، `SessionValidator` (رد در درخواست بعدی)، `UserSessionService` (الیست/ابطال/evoke-others فقط خود کاربر).
- D-073 بخش ۱ اکنون `Accepted` است؛ گپ دقیقاً مشخص و بدون Migration جدید قابل پیاده‌سازی است.
- **اقدام این مأموریت:** PR مستقل `feat(admin): add installation-admin session revocation (D-073 §1)`.

### مرحله ۵ — بازیابی رمز فراموش‌شده (D-073 بخش ۲): `Not Implemented`
- هیچ token store و Endpoint بازیابی وجود ندارد؛ کانال ایمیل ارسالی در Customer app موجود نیست (بدون وابستگی خارجی جدید بدون مجوز).
- مسیر Cloud به زیرساخت ایمیل وابسته است؛ مسیر Offline (challenge یک‌بارمصرف اپراتور-محور) طبق D-073 §2 قابل پیاده‌سازی مستقل است و به تغییر Schema افزایشی (token store) نیاز دارد.
- **اقدام:** PR جداگانه پس از مرحله ۴؛ جعل یا شبیه‌سازی ایمیل ممنوع است.

### مرحله ۶ — بازنشانی رمز به ابتکار مدیر (D-073 بخش ۳): `Not Implemented`
- همان token store مرحله ۵ را می‌خواهد؛ مجوز مستقل `users.credentials.reset` و اعلام اثر سراسری تغییر credential در UI/API لازم است.
- **اقدام:** در همان PR بازیابی رمز (برش ۳ D-073) یا PR بعدی آن.

### مرحله ۷ — MFA کاربران Customer (D-073 بخش ۴): `Not Implemented`
- MFA سامانه Vendor طبق D-046 مستقل و موجود است (`docs/handoffs/ISSUE-103-vendor-auth-mfa.md`، Passkey/TOTP) اما برای Customer app هیچ enrollment/validation عامل دوم وجود ندارد.
- نیازمند چند PR (enrollment TOTP/Passkey، policy server-owned حساس، Recovery Code hash-only، re-auth در تغییر عامل) و بررسی وابستگی‌های جدید — epic مستقل با شواهد D-073 §4.
- **اقدام در این مأموریت:** فقط ثبت وضعیت و نقشه؛ هیچ مکانیزم دست‌ساز یا Checkbox ظاهری ساخته نمی‌شود.

### مرحله ۸ — هم‌ترازی سیاست رمز عبور (D-073 بخش ۵): `Partially Implemented` ← **قابل اصلاح کوچک**
- سازگار: حداکثر ۱۲۸ (`Constants/MaxLength.cs: UserPassword = 128`)، پذیرش هر نویسه Unicode و عبارت عبور/فاصله (بدون قاعده ترکیبی — `Constants/RegularExpression.cs`)، عدم اجبار تغییر دوره‌ای، blocklist رایج (`ViewModels/Validation/NotCommonPasswordAttribute.cs`)، rate limit ورود ناموفق، عدم افشای وجود حساب در Login.
- ناقص: **حداقل طول ۸ است درحالی‌که D-073 حداقل ۱۵ را مصوب کرده** (`Constants/MinLength.cs: UserPassword = 8`).
- رفع ناقص فقط روی مسیر ایجاد/تغییر رمز اثر دارد (Login فقط hash را می‌سنجد؛ حساب‌های قدیمی قفل نمی‌شوند) — دقیقاً مطابق مهاجرت D-073 §5.
- **اقدام این مأموریت:** PR مستقل `feat(security): align password policy with D-073 (min 15)`.

### مرحله ۹ — گزارش‌های امنیتی و تاریخچه کاربر: `Completed`
- گزارش Audit مدیریتی (فیلتر/مرتب‌سازی/صفحه‌بندی/CSV با ضد Formula Injection و Audit `SecurityAuditExported`) + UI کامل (PR #561) + تاریخچه ورود/خروج کاربر (`GET /me/login-history`) + سوابق نشست شخصی (D-069).
- Retention طبق D-070: تصمیم `Accepted`، پیاده‌سازی `NotImplemented` به‌صورت **عمدی** — هیچ Purge/Job تا طراحی اجرایی و بررسی حقوقی مجاز نیست (دستور صریح مأموریت نیز همین است).

### مرحله ۱۰ — یکپارچگی مجوز و Entitlement (D-074): `Partially Implemented` (Track مستقل)
- قرارداد Envelope v2 و موتور policy در `Licensing.Contracts` موجود؛ دروازه Trade به `DenyAllTradeModuleEntitlement` fail-closed متصل است (دقیقاً طبق D-074 بند ۸ تا اتصال امن Envelope).
- اتصال عمومی Customer runtime به Envelope معتبر خواستهٔ Issueهای #390/#391/#401/#403 است و با مراحل D-073 در یک PR مخلوط نمی‌شود.
- **اقدام در این مأموریت:** فقط ممیزی و ثبت وضعیت؛ هیچ License/Entitlement جدیدی طراحی نشد.

## فهرست APIهای موجود مرتبط (استخراج از کد — به‌اختصار)

- نشست شخصی (D-069): `GET /me/sessions`، `POST /me/sessions/{id}/revoke`، `POST /me/sessions/revoke-others` — همه حساس + anti-forgery روی POST.
- کاربران نصب: `GET/POST /admin/users`، `GET/PUT /admin/users/{userId}`.
- نقش‌ها و کاتالوگ: `GET/POST/PUT /admin/companies/{companyId}/roles*`، `permission-catalog`، `permission-templates`.
- عضویت: `GET/POST /me/membership-requests*`، صف تأیید `/admin/companies/{companyId}/membership-requests*`.
- گزارش امنیتی: `GET /admin/security/audit-events`، `audit-event-types`، `GET /admin/companies`، `POST .../export`.
- HR: ۲۵ Endpoint `/companies/{companyId}/org/*` (قرارداد در `docs/HR_FRONTEND_API_CONTRACT.md`).
- مجوز مؤثر: `GET /me/permissions`.

## ترتیب اجرای مستقل باقیمانده (این مأموریت)

| ترتیب | PR | دامنه | وضعیت در این نشست |
|---|---|---|---|
| ۰ | ممیزی و برنامه اجرایی | همین سند + WORK_LOG | تحویل این PR |
| ۴ | ابطال مدیریتی نشست (D-073 §1) | Backend + تست + قرارداد API + برچسب‌های Audit؛ بدون Migration | PR مستقل بعدی |
| ۸ | سیاست رمز (D-073 §5) | حداقل ۱۵ + پیام‌ها + تست‌ها؛ بدون Migration | PR مستقل بعدی |
| ۵–۶ | بازیابی رمز و Reset مدیریتی | token store + Offline challenge (Migration افزایشی) | خارج از ظرفیت این نشست — نقشه ثبت شد |
| ۷ | MFA Customer | epic چند-PR | ثبت وضعیت |
| ۱۰ | Entitlement runtime | Track مستقل D-074 (Issues #390/#391/#401/#403) | ثبت وضعیت |

## ریسک‌ها و کارهای باقی‌مانده

- تستهای SQL Server و E2E فقط در CI قابل اجرا هستند (Docker در این محیط نیست)؛ نتایج CI هر PR پس از باز شدن، واقعی و بدون ادعای بدون شاهد گزارش می‌شود.
- هیچ تغییری در D-070/D-074 بدون PR اجرایی جدا انجام نمی‌شود؛ Purge ممنوع است.
- Merge همه PRها با مالک مخزن است.
