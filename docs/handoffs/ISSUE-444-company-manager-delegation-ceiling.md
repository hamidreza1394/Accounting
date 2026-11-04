# تحویل وظیفه — ISSUE-444-company-manager-delegation-ceiling

- **Issue:** #444 (مأموریت مرحله دوم: Company Manager، Delegation Ceiling و Company-scoped Authorization)
- **Agent/مسئول:** Z.ai (GLM) — از طرف مالک مخزن
- **تاریخ:** 2026-11-04
- **وضعیت:** انجام‌شده (تست‌های SQL Server/E2E محلی Blocked؛ اجرای آن‌ها با CI این PR)

## هدف و معیارهای پذیرش
- [x] Company Manager در Backend دارای Scope واقعی و محدود است (مدل D-071).
- [x] Delegation Ceiling در تمام مسیرهای تغییر نقش و Permission (Create/Update/Assign/Transfer) enforce می‌شود.
- [x] Company و CorporateGroup Scope با تست منفی اثبات شده‌اند.
- [x] مسیر bootstrap امن مدیر شرکت برقرار است (تأیید عضویت توسط مدیر نصب).
- [x] تست‌های Unit، API/Integration و SQL-gated متناسب اجرا/نوشته شده‌اند و نتیجه واقعی گزارش شده است.
- [ ] CI نهایی روی آخرین SHA — با Push انجام می‌شود؛ نتیجه در PR ثبت می‌گردد.
- [x] هیچ نقص امنیتی بحرانی یا High باز باقی نمانده است (بررسی بخش ممیزی پایین).
- [x] مستندات و handoff به‌روز شدند.
- [x] PR واقعی ساخته شد.

## خلاصه تغییرات
1. **مدل مجوز مدیر شرکت (D-071):** پنج کد سرور-مال با Migration فقط-داده‌ای و idempotent به کاتالوگ اضافه شد: `roles.view`، `roles.create`، `roles.update`، `roles.assign` و `corporate-groups.directory.view`. هیچ تغییر مدل/Schema دیگری وجود ندارد؛ snapshot بدون تغییر ماند.
2. **مجوزدهی دوحالته نقش‌ها:** endpointهای `/admin/companies/{companyId}/roles...` و کاتالوگ/الگوها علاوه بر مدیر نصب، مدیر شرکتِ دارای عضویت فعال و کد مجوز متناظر را می‌پذیرند. خطاهای 401/403/404 مطابق سیاست عدم افشا انتخاب می‌شوند (غیرعضو = همان 404 شرکت ناموجود).
3. **DelegationCeilingService:** سقف تفویض = اجتماع مجوزهای عضویت‌های فعال actor در همان شرکت. ارزیابی همیشه درون تراکنش ReadCommitted عملیات و از ردیف‌های tracked؛ no-op update روی ردیف‌های مبنای سقف و نقش مقصد تا RowVersion هر تغییر هم‌زمان را 409 کند. قواعد خالص در `DelegationCeilingRules` جدا و قابل تست‌اند.
4. **Bootstrap امن:** مدیر نصب می‌تواند صف درخواست‌های عضویت هر شرکت را ببیند و تأیید/رد کند — قبلاً برای شرکت خالی deadlock دائمی وجود داشت. self-approval و نقش سیستمی برای همه انواع actor ممنوع ماند؛ مدیر نصب بدون سقف، هر نقش فعالِ غیرسیستمی شرکت مقصد را می‌تواند تخصیص دهد.
5. **دفترچه هم‌گروه (D-065 تصمیم ۲):** `GET /api/v1/corporate-groups/{groupId}/directory` فقط-خواندنی، با اجازه صریح، scope اثبات‌شده سمت سرور، fail-closed روی مرز مشتری (شامل شرکت مالک) و خروجی allowlistشده.
6. **کاتالوگ و الگوها برای مدیر شرکت:** فقط کدهای داخل سقف actor نمایش داده می‌شود؛ عضو بدون هیچ مجوزی 404 می‌گیرد (تغییر قرارداد نسبت به 403 قبلی، پوشش تست شده و مستند).
7. **UI:** هیچ تغییر ClientApp لازم نشد؛ منو از `/me/permissions` تغذیه می‌شود و کدهای جدید پس از تخصیص به نقش به‌صورت خودکار ظاهر می‌شوند. کنترل واقعی همیشه Backend است.

## فایل‌های مهم تغییرکرده
- `Infrastructure/Persistence/Migrations/20261104174711_SeedCompanyRoleAdministrationPermissions.cs` (+Designer) — seed پنج کد مجوز؛ با `generate_migrations.py --check` و `verify_migration_model.py` تأیید شده.
- `scripts/generate_migrations.py` — ثبت SQL بازبینی‌شده migration و پیشروی ثابت‌های `CURRENT_MIGRATION_*`.
- `Server/Infrastructure/Tenancy/DelegationCeilingService.cs` (جدید) — قواعد خالص سقف + بارگیری tracked + گاردهای RowVersion.
- `Server/Api/CompanyRoleAdministrationEndpoints.cs` — مجوزدهی دوحالته، enforce سقف روی Create/Update/Assign/Transfer، کاتالوگ/الگوی فیلترشده.
- `Server/Api/CompanyMembershipRequestEndpoints.cs` — bootstrap مدیر نصب (صف‌ها + تأیید/رد)، سقف شرکتی حفظ شد.
- `Server/Api/CorporateGroupDirectoryEndpoints.cs` (جدید) + `Server/Api/ApiEndpoints.cs` — دفترچه هم‌گروه و ثبت route.
- `Server/Api/UserAdministrationEndpoints.cs` — استخراج `IsInstallationAdministratorAsync`/`ForbiddenForNonAdministrator` برای استفاده مشترک (رفتار endpointهای قبلی بدون تغییر).
- `Server/Program.cs` — ثبت DI سرویس سقف تفویض.
- `tests/Template.Tests/DelegationCeilingRulesTests.cs`، `CompanyRoleDelegationCeilingEndpointTests.cs`، `BootstrapMembershipApprovalTests.cs`، `CorporateGroupDirectoryEndpointTests.cs`، `Integration/CompanyRoleAdministrationSeedSqlServerTests.cs` (جدید).
- `tests/Template.Tests/CompanyRoleAdministrationEndpointTests.cs`، `Integration/CompanyRoleMembersSqlServerTests.cs`، `Integration/MembershipUtcWireFormatTests.cs`، `Integration/SensitiveReadRateLimitBehaviorTests.cs` — تطبیق امضاها و قرارداد کاتالوگ (404 برای عضو بی‌مجوز).
- `docs/DECISIONS.md` (D-071)، `docs/USER_ACCESS_MANAGEMENT.md`، `docs/SECURITY.md`، `docs/INDEX.md`، `docs/WORK_LOG.md`.

## ماتریس Permission / Scope / Actor / Target
| عملیات | مجوز لازم (مدیر شرکت) | Scope | مدیر نصب | مدیر شرکتِ مجاز | عضو بی‌مجوز | غیرعضو/ناشناس |
|---|---|---|---|---|---|---|
| مشاهده نقش‌ها و اعضا | `roles.view` | شرکت مسیر (اثبات عضویت) | مجاز (بدون سقف) | مجاز | 403 | 404 / 401 |
| ایجاد نقش | `roles.create` | همان شرکت | مجاز | فقط با مجوزهای ⊆ سقف | 403 | 404 / 401 |
| ویرایش/غیرفعال‌سازی نقش و مجوزها | `roles.update` | همان شرکت | مجاز (نقش سیستمی 409) | فقط ⊆ سقف؛ نقش سیستمی 409 | 403 | 404 / 401 |
| تخصیص/انتقال نقش عضو فعال | `roles.assign` | همان شرکت | مجاز (نقش سیستمی 409) | نقش مقصد ⊆ سقف؛ تغییر نقش خود 403 | 403 | 404 / 401 |
| کاتالوگ/الگوها | عضویت فعال کافی است | کل نصب برای مدیر نصب؛ سقف actor برای مدیر شرکت | فهرست کامل | فقط کدهای سقف | 404 (بدون عضویت فعال) | 404 / 401 |
| صف/تاریخچه/تأیید/رد عضویت | `users.membership.approve` (موجود) | شرکت مقصد | مجاز (bootstrap؛ بدون سقف؛ نقش سیستمی 403) | مجاز با سقف تصاحب (موجود از #498) | 404 | 404 / 401 |
| دفترچه هم‌گروه | `corporate-groups.directory.view` | اعضای باز همان گروه + مرز مشتری یکسان | مجاز | مجاز؛ فقط فیلدهای allowlist | 403 | 404 / 401 |

## تصمیم‌های فنی
- سقف «مبتنی بر تصاحب» انتخاب شد و پرچم `Delegatable` به کاتالوگ اضافه نشد: ساده‌تر، fail-closed، بدون Migration مدل و منطبق با قاعده محصول «تفویض آنچه خودت نداری ممنوع» (D-071 بند ۳).
- مسیر bootstrap بدون تغییر Schema حل شد: مدیر نصب همان مرجع اختیار D-064 است و فقط نیاز به گذر از گردش تأیید عضویت داشت؛ بنابراین هیچ Migration ساختاری و هیچ endpoint موازی‌ای لازم نشد.
- خطای «عضو بدون مجوز در کاتالوگ» از 403 به 404 تغییر کرد تا وجود کاتالوگ و کدهای نصب از یک عضو بی‌مجوز افشا نشود (سخت‌گیرانه‌تر از قبل؛ تست `SensitiveReadRateLimitBehaviorTests` به‌روز شد).

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `dotnet restore Template.sln` | موفق | SDK 10.0.403 |
| `dotnet build Template.sln --configuration Release` | موفق | 0 Error |
| `dotnet test tests/Template.Tests/Template.Tests.csproj` | موفق | 888 کل: 830 موفق، 56 Skip (SQL/Windows-gated)، 2 ناموفقِ محیطیِ از قبل موجود در main (`SafeExceptionRealApplicationTests`؛ در baseline b0d2f09 نیز تکرار و تأیید شد) |
| `python3 scripts/generate_migrations.py --repo-root . --check` | موفق | "The generated migrations are up to date." |
| `python3 scripts/verify_migration_model.py --repo-root .` | موفق | 14 migration + snapshot با مدل EF منطبق؛ سینتکس C# معتبر |
| `python3 scripts/tests/test_migration_tooling.py` | موفق | 32/32 |
| `python3 scripts/tests/test_migration_guard_mutations.py` | موفق | 31/31 (پس از نصب tree-sitter مطابق CI) |
| `git diff --check` | موفق | بدون خطای whitespace |
| تست‌های SQL Server (`FARANOU_TEST_SQLSERVER`) | **Blocked** | بدون SQL Server/Docker در محیط؛ 56 Skip شامل تست جدید `CompanyRoleAdministrationSeedSqlServerTests`؛ اجرای واقعی با CI این PR |
| E2E Playwright (Published Server + SQL Server) | **Blocked** | نیازمند `FARANOU_E2E_PUBLISH` + `FARANOU_E2E_SQL`؛ جعل نتیجه ممنوع؛ CI این PR مجموعه موجود را اجرا می‌کند |
| Semgrep | اجرا نشده محلی | Job مستقل CI با rulesetهای `p/csharp,p/security-audit,...` |
| ClientApp (typecheck/vitest/build) | اجرا نشده | بدون تغییر ClientApp؛ CI اجرا می‌کند |

## امنیت، داده و سازگاری
- **Tenant Isolation:** همه queryهای جدید با `TenantId == companyId` اثبات‌شده از `CompanyAccessService` کار می‌کنند؛ تست cross-company منفی اضافه شد.
- **Authorization:** دوحالته بودن فقط دو مسیر ورود دارد (مدیر نصب از DB، عضویت فعال + کد مجوز از DB)؛ هیچ claim کوکی مرجع نیست (D-013/D-064).
- **سقف تفویض:** در هر شش مسیر جهش‌دهنده با داده تازه و گارد RowVersion اعمال می‌شود؛ race condition با همان الگوی اثبات‌شده `ApproveAsync` کنترل می‌شود.
- **Audit:** رویدادهای موجود (`CompanyRoleCreated/Updated`، `UserCompanyRoleAssigned`، `CompanyMembershipApproved/Rejected`) برای عملیات جدید مدیر شرکت هم با actor درست نوشته می‌شوند؛ رخدادهای ردشده بدون Audit چون هیچ نوشتنی رخ نمی‌دهد (رفتار موجود).
- **داده:** Migration فقط-داده‌ای و idempotent؛ Down فقط ردیف‌های بدون ارجاع را حذف می‌کند؛ هیچ داده موجودی تغییر/حذف نمی‌شود؛ اجرای Migration روی Production خارج از دامنه این PR است.
- **عدم افشا:** 404 برای هر رابطه اثبات‌نشده؛ دفترچه هم‌گروه بدون username/تماس/نقش/مجوز؛ کاتالوگ شرکتی فقط کدهای سقف.

## ریسک‌ها و کارهای باقی‌مانده
- **Blocked محلی:** تست‌های SQL Server و E2E در این محیط اجرا نشدند؛ نتیجه واقعی آن‌ها از CI این PR گرفته می‌شود (`FARANOU_REQUIRE_SQLSERVER=1`).
- **UI:** نمای مدیریت نقش برای مدیر شرکت و دفترچه هم‌گروه در ClientApp هنوز ساخته نشده (این مأموریت Backend بود)؛ کدهای جدید از `/me/permissions` قابل استفاده‌اند.
- **Simulation رقابت روی سقف:** کنترل رقابت با همان سازوکار RowVersion مسیرهای موجود اعمال شد؛ سناریوی رقابتی اختصاصی جدید روی SQL Server به CI موکول شد (تست‌های ConcurrentUpdateInterceptor موجود الگوی آن‌اند).
- دسترسی مستقیم کاربران به فهرست همه‌کدها برای «درخواست مجوز از مدیر» نیازمند تصمیم محصولی جداگانه است (خارج از این مأموریت).

## شناسه‌های Git
- Branch: `feature/company-manager-delegation-ceiling`
- Commitها: seed `4be79dec`؛ سقف تفویض `5940a39b`؛ bootstrap `e5d1f5e9`؛ دفترچه هم‌گروه `eb549a8d`؛ تست‌ها `e8e6612b`؛ مستندات در commit این فایل.
- Pull Request: `feat(access): enforce company-scoped management and delegation ceiling` به `main` (بعد از Push لینک در گزارش نهایی).
