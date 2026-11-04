# تحویل وظیفه — ISSUE-444-access-hardening-mission2

- **Issue مادر:** [#444](https://github.com/hamidreza1394/Accounting/issues/444) — تکمیل مرحله اول مدیریت کاربران و دسترسی‌ها (مأموریت ۲)
- **Agent/مسئول:** Z.ai (GLM) با توکن مالک مخزن
- **تاریخ:** 2026-11-04
- **Commit مبنای راستی‌آزمایی:** `main` @ `d06a4b1a` (CI سبز در همین SHA: `.NET CI`، `Push on main`، Semgrep)
- **وضعیت:** انجام‌شده در محدوده امکان محیط؛ اجرای SQL Server/E2E محلی `Blocked` و فقط از CI می‌آید

## هدف و معیارهای پذیرش
- [x] راستی‌آزمایی PRهای ادغام‌شده #538، #536، #481 روی `main` بدون پیاده‌سازی مجدد
- [x] تطبیق قراردادهای #510 و #511 با پیاده‌سازی واقعی Backend
- [x] تطبیق گزارش ممیزی #537 با کد فعلی و ثبت وضعیت هر کنترل
- [x] پوشش شکاف واقعی تست: رفتار واقعی 429 و مجوزدهی HTTP سطح Pipeline برای endpointهای مدیریتی
- [x] بدون Migration، بدون تغییر Schema، بدون تغییر authorization و بدون کار تکراری
- [x] گزارش صادقانه موارد اجرا نشده (بند ۱۵ مأموریت در PR توضیحات آمده است)

## خلاصه تغییرات
- هیچ نقص واقعی امنیتی در محدوده مأموریت یافت نشد؛ همه کنترل‌های حساس روی `main` موجود و درست‌اند (ماتریس پایین).
- دو شکاف «پوشش تست» پر شد (فقط فایل تست جدید؛ بدون تغییر کد اجرایی):
  1. **تست رفتاری Rate Limit حساس** — پیش از این فقط metadata Routeها تست می‌شد؛ اکنون رفتار واقعی Pipeline اثبات می‌شود.
  2. **تست مجوزدهی HTTP سطح Pipeline برای endpointهای مدیریتی** — پیش از این 403/401 این مسیرها فقط با فراخوانی مستقیم متدها (بدون Pipeline) تست می‌شد؛ اکنون 401 ناشناس، 403 عضو عادی (با بدنه بدون افشا) و 200 مدیر نصب از طریق Pipeline واقعی احراز هویت/مجوزدهی/Routing بررسی می‌شود.

## راستی‌آزمایی PRهای ادغام‌شده (روی `main` @ `d06a4b1a`)

### PR #538 — Rate Limit خواندن هویتی حساس: `Verified` در سطح Pipeline
- هر چهار endpoint مأموریت `RequireRateLimiting(ApiSensitivePolicy)` دارند: `UserAdministrationEndpoints.cs` (لیست و جزئیات کاربر)، `CompanyUserDirectoryEndpoints.cs` (کاربران شرکت)، `CompanyRoleAdministrationEndpoints.cs` (اعضای نقش).
- پوشش از چهار endpoint مأموریت فراتر است: endpointهای درخواست عضویت، ممیزی امنیتی و `/admin/companies` نیز `api-sensitive` دارند.
- Policy در Production فعال است: بخش `RateLimiting` در `appsettings.json` پایه است و `appsettings.Production.json` وجود ندارد که آن را خنثی کند؛ پیکربندی از `IOptionsMonitor` در اولین استفاده خوانده می‌شود.
- کلید پارتیشن `api-sensitive|user:{NameIdentifier}` یا `ip:{RemoteIpAddress}` است؛ هیچ داده حساس (نام کاربری، ایمیل، پارامتر Query) در کلید نیست و تغییر Query/Route پارتیشن را عوض نمی‌کند.
- رفتار 429: `RejectionStatusCode=429`، هدر `Retry-After`، `Cache-Control: no-store`، برای API `application/problem+json` با پیام عمومی فارسی — اکنون با تست رفتاری اثبات می‌شود.
- تست‌های جدید: مثبت (پاسخ 200 تا سقف)، منفی (429 + Retry-After + no-store + بدون افشا)، دورزدن (Query/Route/endpoint-hopping همگی رد)، هم‌زمانی/جداوی پارتیشن (کاربر دیگر و endpoint غیرحساس بی‌اثر، ناشناس روی پارتیشن آدرس جدا).
- محدودیت ثبت‌شده از handoff #522: limiter به ازای هر instance است؛ توزیع‌شده‌سازی خارج از دامنه این مرحله است.

### PR #536 — تست E2E گردش درخواست عضویت: ادغام‌شده؛ اجرای محلی `Blocked`
- PR در 2026-11-04T14:54:53Z به `main` ادغام شده است؛ تست `tests/e2e/specs/membership-workflow.spec.mjs` (۵ تست) و تست‌های Backend همراه روی `main` موجودند.
- سناریوهای اجباری مأموریت همگی پوشش داده شده‌اند: موفق کامل (درخواست → Pending → بدون دسترسی → مشاهده تأییدکننده → انتخاب نقش → تأیید → عضویت فعال → Audit → دسترسی واقعی)، خودتأیید 403، تأییدکننده شرکت دیگر 404، لغو عضویت تأییدکننده 404، سقف تفویض دوباره‌محاسبه 403، توکن ضدجعل نشست دیگر 400، Pending تکراری 409، ردشده بدون عضویت، active-member بدون درخواست جدید و خالی بودن صف، و محدودیت نرخ حساس endpointهای عضویت.
- سه سناریوی منفی باقی‌مانده مأموریت در تست‌های Backend پوشش دارند: `ApproveMembership_RejectsInactiveOrForeignCompanyRole` (نقش غیرفعال/نقش شرکت دیگر)، `MigrationPreservesExistingMembershipsAndConcurrentApprovalsHaveOneWinner` (تأیید هم‌زمان — فقط SQL Server)، `ApproveMembership_RejectsSystemRoleEvenWhenItsPermissionsAreDelegable` (نقش سیستمی).
- **اجرای محلی Blocked:** اجرای واقعی E2E به Published Server + SQL Server 2022 + Chromium نیاز دارد؛ در این محیط Docker/SQL Server نیست و جایگزین Mock مجاز نیست (طبق مأموریت و TESTING.md). نتیجه واقعی از job `e2e` روی همین PR می‌آید (CI هم `pull_request` را اجرا می‌کند).

### PR #481 — تست مدیریت نقش: ادغام‌شده؛ پوشش کافی، تکرار نشد
- تست E2E `role-admin.spec.mjs`: CRUD نقش روی SQL Server واقعی، اعتبارسنجی مجدد نقش از دیتابیس (`RoleId=NULL` → 403 کاتالوگ)، 404 برای به‌روزرسانی نقش شرکت دیگر، ضدجعل.
- تست‌های UI موجود (`AppRouting.test.tsx`): غیرمدیر روی deep link های `/app/admin/roles*` و `/app/admin/permissions` صفحه forbidden می‌بیند، آیتم منو ندارد و **هیچ درخواست API ارسال نمی‌کند**؛ `can()` فقط hint نمایشی است و سرور مرجع است (D-064).
- تست‌های Backend: `GetRoles_RejectsAnAuthenticatedNonInstallationAdministrator`، `UpdateRole_RejectsRoleIdOwnedByAnotherCompany`، `TransferMembers_RejectsNonAdministrators...` و بقیه.
- افزوده این PR: تست HTTP سطح Pipeline (`AdministrativeEndpointAuthorizationHttpTests`) که 401 ناشناس، 403 عضو عادی و 200 مدیر نصب را از طریق Pipeline واقعی اثبات می‌کند (موارد ۳، ۴ و ۵ بند ۶ مأموریت در سطح HTTP).

### قرارداد #510 (کشف مقصد و نقش‌های قابل تخصیص): منطبق — `Verified` در سطح قرارداد/تست موجود
- جستجوی اجباری exact با Trim و طول ۳..۱۲۰، صفحه‌بندی با سقف `Math.Clamp(pageSize, 1, 100)` در همه فهرست‌ها، `no-store`، `users.membership.approve` با `CompanyAccessService.HasPermissionAsync` از DB، مرز CustomerBoundary مشتق از عضویت‌های فعال خود کاربر (نه ورودی کلاینت)، نقش‌های فعال غیرسیستمی زیر سقف تفویض، تأییدکنندگان فعال با خروجی حداقلی، تاریخچه با فیلتر وضعیت و UTC — همه در کد موجود است و تست‌های `CompanyMembershipRequestEndpointTests` و E2E آن را پوشش می‌دهند.

### قرارداد #511 (`GET /me/permissions`): منطبق — `Verified` در سطح قرارداد/تست موجود
- بدون پارامتر companyId؛ شرکت فقط از `WorkContextService.GetCurrentAsync` (کوکی محافظت‌شده + اعتبارسنجی مجدد)؛ `isInstallationAdministrator` در هر درخواست از نقش فعال DB؛ مجوزها فقط از CompanyRole فعالِ عضویت فعال همان شرکت؛ `Cache-Control: no-store` و `Pragma: no-cache`؛ `company=null` بدون زمینه معتبر؛ 401 برای هویت نامعتبر — انطباق کامل با قرارداد.
- تست‌های `MePermissionsEndpointTests`: مجوزهای فقط شرکت معتبر، null بدون شرکت، حذف فوری با عضویت/نقش غیرفعال، پرچم مدیر نصب از DB، رد پرینسیپال نامعتبر. تست‌های SQL Server مرتبط در CI اجرا می‌شوند (`MePermissionsSqlServerTests`).

## ماتریس ممیزی #537 در برابر کد فعلی (وضعیت‌های مأموریت بند ۸)

| کنترل | وضعیت | شاهد |
|---|---|---|
| Tenant Isolation | Verified (تست‌های InMemory + SQL Server در CI) | `ApiEndpointTests`، `CompanyAccessServiceTests`، `CompanyMembershipRequestEndpointTests`؛ E2E `access-ui`/`membership-workflow` |
| Company Isolation | Verified (همان بالا) | 404 یکسان شرکت بیگانه/ناموجود؛ `ApproveMembership_DoesNotUsePermissionsGrantedInAnotherCompany` |
| Role Assignment | Verified | `AssignMembership_*` (بدون عضویت پیش از تأیید، رد نقش شرکت دیگر، عدم reactivation) |
| Permission Delegation | Verified | سه تست سقف تفویض + دوباره‌محاسبه در زمان تأیید (`ApprovalReturnsConflictWhenDelegationPermissionsChange...` روی SQL Server) |
| Membership Approval | Verified | گردش کامل E2E + تست‌های Backend + single-winner هم‌زمانی روی SQL Server |
| Audit Logging | Verified (سطح تست موجود) | assert روی `SecurityAuditEvents` در Created/Updated/Approved/Rejected؛ بدون رمز/توکن؛ retention فقط Proposed (D-069/D-070) |
| Anti-forgery | Verified | `RequestMembership_RequiresAntiforgery`، `TransferMembers_RejectsAnInvalidAntiforgeryToken...`، E2E توکن نشست دیگر 400 |
| Sensitive Rate Limiting | Verified (پس از تست جدید این PR) | metadata test + تست رفتاری Pipeline جدید |
| SQL Concurrency | Implemented but not tested محلی / Verified در CI | `FARANOU_TEST_SQLSERVER` لازم؛ محلی Blocked؛ در CI با SQL Server 2022 اجرا می‌شود |
| RowVersion | Implemented but not tested محلی / Verified در CI | `CompanyMembershipRequestConfiguration` + تست single-winner و interceptor در CI |
| Unique Constraints | Implemented (تست DB-محور در CI) | unique filtered index `[Status]='Pending'` + catch 2601/2627؛ رفتار فیزیکی index فقط روی SQL Server معنا دارد |
| Database Check Constraints | Implemented (تست Migration در CI) | `CK_..._Status` و `CK_..._DecisionState` شامل `DecidedByUserId <> UserId` |
| Authorization Policies | Verified (پس از تست جدید این PR در سطح HTTP) | RequireClaim + `AuthorizeInstallationAdministratorAsync` + `HasPermissionAsync`؛ تست Pipeline جدید |

- هیچ مورد `Missing` در این ماتریس یافت نشد؛ هیچ آسیب‌پذیری با مستندسازی بسته نشد.
- یافته Low قبلی ممیزی (ناهمخوانی بدنه PR #479 با منطق فعلی fail-closed) همچنان فقط توضیحِ PR است و تغییر کد نمی‌خواهد.

## فایل‌های مهم تغییرکرده
- `tests/Template.Tests/Integration/SensitiveReadRateLimitBehaviorTests.cs` — جدید؛ دو کلاس تست رفتاری (Rate Limit حساس + مجوزدهی HTTP مدیریتی)
- `docs/SECURITY.md` — ثبت اثبات رفتاری Rate Limit حساس
- `docs/WORK_LOG.md` — پیوست خلاصه این کار
- `docs/INDEX.md` — افزودن این handoff
- `docs/handoffs/ISSUE-444-access-hardening-mission2.md` — همین سند

## تصمیم‌های فنی
- تصمیم معماری جدیدی گرفته نشد و ADR جدید لازم نشد؛ تغییرات فقط «پوشش تست» و مستندات است.
- تست‌ها عمداً روی Provider حافظه‌ای قابل اجرا هستند (Rate Limit و مجوزدهی به رفتار SQL وابسته نیستند) و در CI نیز روی SQL Server واقعی اجرا می‌شوند؛ هیچ تست SQL Server-gated جدیدی ساخته نشد تا Skip محلی تولید شود.
- پارامترهای الزامی `page`/`pageSize`/`descending` در تست‌ها صریح ارسال می‌شوند (رفتار binding واقعی minimal API).

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `dotnet restore Template.sln` + `dotnet build Template.sln --no-restore -c Release` | موفق | ۰ خطا (SDK 10.0.403 نصب‌شده با `scripts/dev/install-dotnet.sh`، SHA-512 تطبیق داده شد) |
| `dotnet test Template.sln --no-build -c Release` (کل Solution، بدون SQL Server) | 969 تست: 911 موفق، 56 Skip، 2 ناموفق | فقط 2 ناموفقِ از قبل موجود: `SafeExceptionRealApplicationTests` (نیاز به SQL Server واقعی در Startup برنامه واقعی؛ روی `main` تمیز هم بدون SQL Server همین رفتار را دارد) — بدون رگرسیون |
| `--filter-class SensitiveReadRateLimitBehaviorTests` | 2 از 2 سبز | 429/Retry-After/no-store/بدون افشا/بدون دورزدن/جداوی پارتیشن |
| `--filter-class AdministrativeEndpointAuthorizationHttpTests` | 1 از 1 سبز | 401 ناشناس / 403 عضو / 200 مدیر نصب از Pipeline واقعی |
| E2E Playwright روی Published Server + SQL Server 2022 | اجرا نشده — `Blocked` محلی | Docker/SQL Server/Chromium منتشرشده در این sandbox نیست؛ جایگزین Mock ممنوع؛ نتیجه واقعی از job `e2e` همین PR |
| Semgrep | اجرای محلی انجام نشد | نتیجه واقعی از workflow `Semgrep` همین PR (روی `main` سبز است) |
| ClientApp (typecheck/test/build) | اجرا نشده | هیچ فایلی از `ClientApp` تغییر نکرد؛ CI مرحله `client` را اجرا می‌کند |
| `git diff --check` | تمیز | بدون فایل تولیدی/راز |

## امنیت، داده و سازگاری
- بدون تغییر در مجوزدهی، API، DTO، Schema، Migration یا داده؛ فقط تست و مستندات.
- تست جدید پارامترهای الزامی Query را صریح می‌فرستد و هیچ رفتار سرور را تغییر نمی‌دهد.
- هیچ راز یا اعتبارنامه‌ای در تست‌ها/مستندات نیست (کاربران آزمایشی با رمز تصادفی هر اجرا).

## ریسک‌ها و کارهای باقی‌مانده
- اجرای واقعی E2E و تست‌های SQL Server فقط از CI این PR اثبات می‌شود؛ تا سبز شدن آن‌ها وضعیت `Verified` کامل به CI وابسته است.
- Company Manager و سقف تفویض شرکتی، فهرست/ابطال نشست مستقل و گزارش امنیتی مدیریتی طبق #444 برای مراحل بعد باقی است (خارج از محدوده این مأموریت).
- محدودیت per-instance بودن Rate Limiter توزیع‌شده نیست (مستند در handoff #522).

## شناسه‌های Git
- Branch: `agent/444-access-hardening-mission2`
- Commit: در PR ثبت می‌شود
- Pull Request: `test(access): complete security and RBAC hardening` به `main`
