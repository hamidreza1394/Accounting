# تحویل وظیفه — ISSUE-491-organizational-structure-implementation

- **Issue:** #491 (پیاده‌سازی S2 — ساختار سازمانی، سمت‌ها و کارکنان؛ زیرمجموعه #444)
- **Agent/مسئول:** Z.ai (GLM) — از طرف مالک مخزن
- **تاریخ:** 2026-11-05
- **وضعیت:** انجام‌شده (Backend کامل؛ تست‌های SQL Server/E2E محلی Blocked و به CI موکول؛ UI در فاز ۴ ادامه می‌یابد)

## هدف و معیارهای پذیرش
- [x] گزینه B (HR-ready) به‌صورت صریح مبنا قرار گرفت — مالک در مأموریت 2026-11-05 اجرای «طراحی تأییدشده» را دستور داد و سند طراحی با ادغام PR #542 پذیرفته شد؛ D-072 به `Accepted` ارتقا یافت.
- [x] Employee مستقل از User و بدون الزام حساب ورود؛ لینک اختیاری one-to-one هم‌شرکت به UserCompanyMembership که هیچ مجوز/عضویتی نمی‌سازد.
- [x] OrganizationalUnit و Position مستقل، Company-scoped، با جلوگیری از چرخه والد و گاردهای غیرفعال‌سازی.
- [x] انتساب مستقل (CompanyEmployeeAssignment) با Effective Dating، حداکثر یک انتساب فعال، ممنوعیت بازنویسی تاریخچه، جلوگیری از چرخه مدیریتی و انتساب بین‌شرکتی.
- [x] کاتالوگ ۱۲ کد `hr.*` با Migration فقط-داده‌ای idempotent؛ مدیر شرکت خودکار هیچ مجوز HR نمی‌گیرد؛ مجوزدهی دوحالته D-071.
- [x] تست‌های Unit، Endpoint، SQL Server-gated و E2E نوشته شدند؛ نتیجه واقعی ثبت شد.
- [ ] CI نهایی روی آخرین SHA — با Push انجام می‌شود و نتیجه در PR ثبت می‌گردد.

## خلاصه تغییرات
1. **Domain:** چهار Entity جدید در `Domain/Hr/` — `CompanyOrganizationalUnit` (درخت با ParentUnitId و پنجره اعتبار)، `CompanyPosition` (مستقل از Unit با Rank اختیاری)، `CompanyEmployee` (شماره پرسنلی یکتا در شرکت، وضعیت اشتغال، لینک اختیاری به عضویت) و `CompanyEmployeeAssignment` (تاریخچه انتساب با شروع/پایان/مدیر). قواعد خالص در `Domain/Hr/HrRules.cs` (ماشین وضعیت اشتغال، بازه‌ها، چرخه والد با پیمایش bounded، چرخه مدیریتی، هم‌پوشانی انتساب فعال) — همه fail-closed و بدون وابستگی.
2. **Persistence:** چهار Configuration در `Infrastructure/Persistence/Configurations/` با FKهای ترکیبی tenant-safe (الگوی `HasPrincipalKey (Id, TenantId)`)، unique `(TenantId, Code)` / `(TenantId, EmployeeNumber)`، دو Unique فیلترشده (یک انتساب فعال؛ یک Employee به‌ازای هر عضویت)، `RowVersion` روی همه؛ alternate key جدید `(Id, TenantId)` روی `UserCompanyMemberships`.
3. **Migration زنجیره‌ای:** `20261104200301_AddHrOrganizationalStructure` با `dotnet ef migrations add` واقعی (EF Core 10.0.14) تولید و SQLهای بازبینی‌شده (۶ CHECK constraint شامل ماشین وضعیت اشتغال/انتساب و seed ۱۲ کد `hr.*` با Ordering 22001..22012 و Down Cleanup بدون حذف ردیف‌های ارجاع‌دار) به آن اضافه و در `HAND_WRITTEN_SQL` اسکریپت ثبت شد. `generate_migrations.py --check` و `verify_migration_model.py` (۱۵ migration، Up و Down) سبز.
4. **API:** `Server/Api/HrOrganizationalStructureEndpoints.cs` — Employee (List/Detail/Create/Update/Deactivate/Link-User/Unlink/Assignment-Change)، Units (List flat/tree/Detail/Create/Update/Deactivate)، Positions (List/Detail/Create/Update/Deactivate)، Assignments (History با فیلتر اجباری/Start با endActive/End). همه دوحالته (مدیر نصب از دیتابیس؛ عضو فعال + کد `hr.*`)، Anti-forgery روی نوشتن‌ها، تراکنش ReadCommitted، گارد RowVersion، Rate Limit حساس، خروجی Allowlist بدون داده تماس/نقش/مجوز کاربران، خطای 401/403/404/409 مطابق D-071.
5. **Audit:** ۱۳ رویداد `SecurityAuditEvent` — `HrEmployeeCreated/Updated/Terminated/UserLinked/UserUnlinked`، `HrUnitCreated/Updated/Deactivated`، `HrPositionCreated/Updated/Deactivated`، `HrAssignmentStarted/Ended` — با actor، شرکت، شناسه هدف و جزئیات بدون داده حساس.
6. **تست‌ها:** `HrRulesTests` (۱۷ تست قواعد خالص)، `HrOrganizationalStructureEndpointTests` (۲۵ تست endpoint با Pipeline مستقیم: مجوزدهی، 403/404، duplicate، لینک یکتا، خاتمه با پایان انتساب، چرخه واحد/مدیر، انتقال، تاریخچه)، `Integration/HrOrganizationalStructureSqlServerTests` (۵ تست SQL-gated: اجرای Migration + seed، unique شماره پرسنلی، FK ترکیبی بین‌شرکتی، CHECK خاتمه، index فیلترشده انتساب فعال)، `tests/e2e/specs/hr-structure.spec.mjs` (سناریوی کامل Published Server + SQL Server).

## فایل‌های مهم تغییرکرده
- `Domain/Hr/*` (جدید)، `Domain/Company.cs`، `Domain/UserCompanyMembership.cs` — Entityها و navigationهای معکوس.
- `Infrastructure/Persistence/Configurations/Company{OrganizationalUnit,Position,Employee,EmployeeAssignment}Configuration.cs` (جدید)، `UserCompanyMembershipConfiguration.cs`، `DatabaseContext.cs`، `Infrastructure/Persistence/Migrations/20261104200301_AddHrOrganizationalStructure.cs` (+Designer، Snapshot)، `scripts/generate_migrations.py`.
- `Server/Api/HrOrganizationalStructureEndpoints.cs` (جدید)، `Server/Api/ApiEndpoints.cs`.
- `tests/TemplateTests`: `HrRulesTests.cs`، `HrOrganizationalStructureEndpointTests.cs`، `Integration/HrOrganizationalStructureSqlServerTests.cs`، `DatabaseModelTests.cs` (افزودن ۴ جدول).
- `tests/e2e/specs/hr-structure.spec.mjs` (جدید).
- مستندات: `DECISIONS.md` (D-072 → Accepted)، `ORGANIZATIONAL_STRUCTURE_DATA_MODEL.md`، `INDEX.md`، `ACCESS_CONTROL_ARCHITECTURE.md`، `USER_ACCESS_MANAGEMENT.md`، `FRONTEND_API_CONTRACT_NEEDS.md`، `WORK_LOG.md`.

## تصمیم‌های فنی
- **مبنا و گیت معماری:** مأموریت 2026-11-05 مالک، تبدیل «طراحی تأییدشده» (PR #542) به پیاده‌سازی را صریحاً دستور داد؛ بنابراین برخلاف مرحله قبل، Migration ساختاری مجاز شد. زنجیره تصمیم در D-072 ثبت شد.
- **انتساب به‌جای فیلد ثابت:** واحد/سمت/مدیر روی `CompanyEmployeeAssignment` مستقل با Effective Dating ذخیره می‌شود؛ انتقال = پایان جاری + شروع جدید در یک تراکنش؛ تاریخچه بازنویسی نمی‌شود (طبق طراحی پذیرفته‌شده).
- **کاتالوگ مجوز:** نام‌های پیشنهادی مأموریت (`hr.employee.*`، `hr.organization.*`) با کاتالوگ طراحی پذیرفته‌شده (`hr.employees.*`، `hr.organizational-units.*`، `hr.positions.*`، `hr.assignments.*`) تطبیق داده شد و همان ۱۲ کد سند طراحی (بخش ۵) ثبت شد؛ deactivate در `*.update` ادغام شد تا کد موازی نسازد.
- **Concurrency:** PUTها توکن `rowVersion` را از Detail GETها دریافت و به OriginalValue اعمال می‌کنند؛ توکن خالی روی SQL Server fail-closed به 409 می‌رود. نوشتن‌های چندردیفی (خاتمه/انتقال/شروع با endActive) درون تراکنش ReadCommitted با no-op guard روی ردیف‌های مبناست (الگوی D-071).
- **مرز RBAC:** هیچ مسیری از HR به مجوز وجود ندارد؛ تخصیص کدهای `hr.*` فقط از `CompanyRolePermissions` و تحت سقف تفویض D-071 انجام می‌شود؛ `ManagerEmployeeId` ورودی Authorization نیست.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `dotnet restore/build Template.sln -c Release` | موفق | 0 Error؛ SDK 10.0.403 |
| `dotnet test tests/Template.Tests -c Release` | 937 کل: 873 موفق، 62 Skip (SQL/Windows-gated)، 2 ناموفقِ محیطیِ از قبل موجود (`SafeExceptionRealApplicationTests`؛ در baseline main هم تکرار می‌شود) | تست‌های جدید: 42 موفق + 5 SQL-gated Skip |
| `python3 scripts/generate_migrations.py --repo-root . --check` | موفق | «The generated migrations are up to date.» |
| `python3 scripts/verify_migration_model.py --repo-root .` | موفق | 15 migration + snapshot با مدل EF منطبق؛ سینتکس C# معتبر |
| `git diff --check` | موفق | بدون خطای whitespace |
| تست‌های SQL Server (`FARANOU_TEST_SQLSERVER`) | **Blocked محلی** | 5 تست جدید `HrOrganizationalStructureSqlServerTests`؛ اجرای واقعی با CI این PR |
| E2E Playwright (Published Server + SQL Server) | **Blocked محلی** | `hr-structure.spec.mjs`؛ اجرای واقعی با CI این PR |
| ClientApp | بدون تغییر | typecheck/build در CI |

## امنیت، داده و سازگاری
- **Tenant Isolation:** همه Queryها با `TenantId == companyId` اثبات‌شده از `CompanyAccessService`؛ FKهای ترکیبی + تست SQL بین‌شرکتی + تست 404 شرکت دیگر.
- **Authorization:** دوحالته D-071؛ نبود عضویت = 404 (عدم افشا)؛ عضو بدون مجوز = 403؛ همه مسیرهای نوشتن Anti-forgery + Rate Limit حساس.
- **داده:** Migration additive؛ هیچ داده موجودی تغییر/حذف نمی‌شود؛ Down فقط جداول جدید و ردیف‌های کاتالوگ بدون ارجاع را حذف می‌کند؛ اجرای Migration روی Production خارج از دامنه PR است.
- **عدم افشا:** خروجی‌های HR بدون username/تماس/نقش/مجوز؛ `rowVersion` فقط توکن هم‌زمانی است.

## ریسک‌ها و کارهای باقی‌مانده
- تست‌های SQL Server و E2E محلی اجرا نشدند؛ نتیجه واقعی از CI این PR گرفته می‌شود (`FARANOU_REQUIRE_SQLSERVER=1`).
- UI مدیریت ساختار سازمانی در ClientApp ساخته نشده (فاز ۴)؛ منو از `/me/permissions` تغذیه می‌شود.
- آزمون رقابتی هم‌زمان دو انتساب روی SQL Server به CI موکول شد (index فیلترشده + تست SQL آن در مجموعه موجود است).
- شروع انتساب گذشته/آینده و مدیریت Unitهای غیرفعال در فاز ۴ تکمیل می‌شود.

## شناسه‌های Git
- Branch: `feature/hr-organizational-structure-implementation`
- Pull Request: `feat(hr): implement company-scoped organizational structure and employees` به `main` (لینک پس از Push).
