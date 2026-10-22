# تحویل وظیفه — ISSUE-UNNUMBERED-corporate-groups-consolidation

- **Issue:** `UNNUMBERED` (مأموریت مستقیم کارفرما، 2026-10-22؛ Issue گیت‌هاب ساخته نشده)
- **Agent/مسئول:** Claude (Lead؛ پیاده‌سازی مستقیم) + دو بازبین مستقل فقط‌خواندنی
- **تاریخ:** 2026-10-22
- **وضعیت هنگام تهیه گزارش اولیه:** انجام‌شده در محدوده زیرساخت؛ هنوز ادغام‌نشده و بدون PR در آن لحظه. **به‌روزرسانی پس از آن:** PR #53 ادغام شده است؛ Merge commit `1d30436d25b4f1e76b95e76a764afe249fd84f6f`؛ CI اجرا `39826940050` موفق گزارش شده است.

## هدف و معیارهای پذیرش
- [x] مستندات پیش از کد به‌روز و ADRهای D-033..D-035 ثبت شدند.
- [x] هسته (`Company`، `UserCompanyMembership`، `FiscalPeriod`، `CompanyRole`) بازتعریف نشد؛ فقط Navigationها و کلید جایگزین `FiscalPeriods(Id, TenantId)` افزوده شد.
- [x] Migration افزایشی، اجراشده روی SQL Server آزمایشی دارای داده قبلی، با Down/Up.
- [x] قواعد دامنه و مجوزها با تست مثبت/منفی.
- [x] هیچ خروجی عددی مالی ساخته یا ادعا نشد.
- [ ] اجرای CI روی شاخه (اجرا نشده؛ Push انجام نشده).

## خلاصه تغییرات
- **مستندات:** `docs/CORPORATE_GROUPS.md`، `docs/CORPORATE_GROUPS_DATA_MODEL.md` (جدید)؛ D-033..D-035 در `docs/DECISIONS.md`؛ بخش‌های جدید در `ARCHITECTURE.md`، `DATABASE.md`، `database-migrations.md`، `SECURITY.md`، `TESTING.md`، `ROADMAP.md`، `PRODUCT_REQUIREMENTS.md`، `REQUIREMENTS_TRACEABILITY.md`، `REPOSITORY_AUDIT.md`، `PROJECT_STATUS.md`، `WORK_LOG.md`، `INDEX.md`؛ اصلاح محدود `AGENTS.md` (بخش 5.3 و شماره ADR) و `CLAUDE.md`.
- **کد:** `Domain/CorporateGroups/{CorporateGroup,CorporateGroupMember,OwnershipRelationship,Consolidation,CorporateGroupRules}.cs`؛ Navigationهای بدون ستون در `Domain/Company.cs` و `Domain/FiscalPeriod.cs`؛ شش پیکربندی EF جدید و تغییر `FiscalPeriodConfiguration.cs`؛ `DatabaseContext.cs`؛ `CompanyAccessService.HasPermissionAsync/GetCompaniesWithPermissionAsync`؛ `Server/Infrastructure/CorporateGroups/{CorporateGroupService,CorporateGroupPermissions}.cs`؛ ثبت DI در `Server/Program.cs`.
- **Migration:** `20261022150943_AddCorporateGroups` (+ Designer، Snapshot).
- **ابزار:** `scripts/ef_model.py`، `migration_model.py`، `generate_migrations.py`، `verify_migration_model.py` (جدول تغییرات در `database-migrations.md`).
- **تست:** `CorporateGroupRulesTests`، `CorporateGroupServiceTests`، `Integration/CorporateGroupSqlServerTests` (جدید)، `DatabaseModelTests` (فهرست جداول و کلیدها گسترش یافت).

## تصمیم‌های فنی
- `Company` = واحد جداسازی داده (`TenantId` = `Companies.Id`) است و موجودیت `Tenant` ساخته نشد (D-033). نگاشت اولیه «Tenant مأموریت = نصب» **اصلاح شد**: مشتری/مستأجر = `CustomerAccount` (D-020، `NotImplemented`) و نصب فقط مرز استقرار/دیتابیس است (D-020/D-021)؛ در ابر مشترک یک نصب چند مشتری دارد.
- گروه یک **شرکت مالک اداری** دارد؛ بدون `TenantId` روی گروه.
- `ConsolidationAdjustment` و `IntercompanyMatch` ساخته نشدند (نبود سرفصل حساب)؛ طرح در مستندات.
- مجوزها با `HasPermissionAsync` و طبق جدول `CORPORATE_GROUPS.md` بخش ۵؛ گزارش گروه مجوز در همه شرکت‌های محدوده می‌خواهد (D-034).
- Migration با `dotnet ef` نوشته شد، چون شبیه‌ساز پایتونی `CreateTable` نمی‌سازد؛ گاردها برای این حالت گسترش یافتند و با آزمون جهش (Mutation) بررسی شدند.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| `dotnet restore Template.sln` / `dotnet build Template.sln -c Release` | موفق، ۰ خطا | پایه پیش از تغییر هم ۰ خطا (۷۶۰ هشدار موجود) |
| `dotnet test --solution Template.sln --no-build -c Release` + SQL Server 2022 | موفق: ۳۰۵/۳۰۵، ۰ Skip | پایه: ۲۵۳/۲۵۳ |
| همان، بدون SQL Server | ۳۰۱ موفق، ۴ Skip | Skipها: تست‌های فقط-SQL Server |
| تست هم‌زمانی SQL Server (۳ اجرای پیاپی) | موفق | پیش از اصلاح، Deadlock بدون نگاشت را آشکار کرد |
| `python3 scripts/generate_migrations.py --repo-root . --check` | موفق | |
| `python3 scripts/verify_migration_model.py --repo-root .` | موفق | آزمون جهش: FK حذف‌شده، AK حذف‌شده، نوع decimal، DropTable/DropUniqueConstraint حذف‌شده، SQL تغییر‌یافته همگی رد شدند |
| `dotnet ef migrations has-pending-model-changes` | موفق | |
| `dotnet ef migrations script WidenUserPasswordHash AddCorporateGroups` | بازبینی شد | فقط CREATE/ALTER ADD/INSERT شرطی؛ تنها «DELETE»ها `ON DELETE NO ACTION` هستند |
| `scripts/apply_migrations_to_testdb.sh` (دیتابیس `TarazAva_scratch_cg`) | موفق | اجرای دوم: بدون تغییر |
| `python3 scripts/check_static_assets.py` | PASS | |
| `git diff --check` | موفق | |
| کلاینت React (`npm ...`) | اجرا نشد | کلاینت تغییری نکرد |
| E2E | اجرا نشد | فقط در CI |

## بازبینی پیش از تحویل (CLAUDE.md بخش ث)
- **سطح:** کار حساس (Schema + Tenant) → بازبینی امنیتی (مدل opus) و بازبینی کیفیت (مدل sonnet)، هر دو مستقل و فقط‌خواندنی. Agentهای سفارشی مخزن در این نشست بارگذاری نشده بودند؛ همان تعریف‌ها (`.claude/agents/*.md`) به Agent عمومی داده شد. `architect` جدا فراخوانی نشد؛ طرح معماری توسط Lead و در ADRها ثبت شد.
- **یافته‌ها:** بدون Critical/High. کیفیت: ۴ Medium (عضویت‌های پیاپی در محدوده، بررسی Down و SQL خام در گارد، نشت SQL دستی به Migration بعدی، قرارداد نتیجه اجرا) و ۵ Low — همه اصلاح یا مستند شدند. امنیت: ۵ Low (کاوش کد گروه، نشت `ControlCycle`، Deadlock، سیل ممیزی، ممیزی شرکت عضو) — ۳ اصلاح، ۲ به‌عنوان ریسک در `SECURITY.md`/D-034.
- **مصرف توکن:** بازبین امنیتی حدود ۱۳۰ هزار، بازبین کیفیت حدود ۱۲۸ هزار توکن (گزارش ابزار)؛ یک رفت‌وبرگشت. تقسیم کار برای بازبینی مفید بود: دو یافته واقعی (عضویت‌های پیاپی، کاوش کد) پیدا شد که تست‌ها نداشتند.
- دور دوم (پس از بازخورد کارفرما): architect (opus) — یافته: نگاشت Tenant=نصب با D-020/D-021 ناسازگار بود؛ اصلاح متنی + تصمیم باز؛ بدون مانع معماری برای PR زیرساختی؛ مصرف توکن حدود ۶۹ هزار.
- دور دوم — security-reviewer (opus)، بازبینی دوباره فقط اصلاحات پس از دور اول: بدون Critical/High/Medium؛ Low-1 (Rollback پس از Deadlock ممکن بود خطای ۵۰۰ بدهد) اصلاح شد؛ Low-2 (نگاشت هر نقض یکتایی به `ConcurrentChange`) پذیرفته شد چون فقط کد خطاست و نشتی ندارد؛ مصرف توکن حدود ۵۹ هزار. documentation-engineer (sonnet) اصلاح متنی D-033 را طبق قرارداد Lead انجام داد (حدود ۶۱ هزار توکن)؛ Diff آن توسط Lead بازبینی شد.
- آزمون نهایی پس از همه اصلاحات: Build ۰ خطا؛ ۳۰۵ از ۳۰۵ روی SQL Server 2022؛ ۳۰۱ + ۴ Skip بدون SQL Server؛ هر دو گارد Migration، `has-pending-model-changes`، `check_static_assets.py` و `git diff --check` موفق.

## امنیت، داده و سازگاری
- Migration فقط افزایشی است؛ روی دیتابیس دارای داده قبلی اجرا و داده حفظ شد؛ Down داده هسته را دست نمی‌زند و ردیف‌های کاتالوگ تخصیص‌یافته را نگه می‌دارد.
- هیچ Endpoint، UI یا خروجی جدید؛ سطح حمله HTTP تغییر نکرد.
- اعمال روی دیتابیس واقعی: **انجام نشده**؛ Production خودکار Migration اجرا نمی‌کند.

## ریسک‌ها و کارهای باقی‌مانده
- اجرای CI پس از Push (نیازمند اجازه کارفرما).
- **تصمیم باز `NeedsApproval` (مانع هر PR آینده برای API/UI گروه؛ نه مانع این PR زیرساختی):** «در نصب ابری مشترک، آیا یک گروه شرکتی یا رابطه مالکیت می‌تواند شرکت‌های چند `CustomerAccount` مختلف را دربر گیرد؟» گزینه‌ها: (۱) ممنوع با پیوند Company↔CustomerAccount، بررسی سمت سرور و محدودکردن بررسی چرخه به همان مشتری؛ (۲) مجاز با اتکا به مجوز هر شرکت (رفتار فعلی) با پذیرش ریسک فراداده/استنتاج؛ (۳) تعویق. توصیه: گزینه ۳ اکنون، گزینه ۱ پیش‌فرض مقصد. جزئیات: D-033 (اصلاح)، `CORPORATE_GROUPS.md` ۶.۱، AUD-2026-10-22-05.
- `NeedsApproval`: قاعده چرخه کنترلی و مجاز بودن مالکیت متقابل اقلیت؛ ریسک افشای `ControlCycle`؛ ادامه یا بازنشستگی شبیه‌ساز پایتونی Migration (AUD-2026-10-22-04).
- `NotImplemented`: API/UI، تخصیص مجوز به نقش، دفتر و کدینگ (پیش‌نیاز گزارش)، تعدیلات، تطبیق درون‌گروهی، ارز، NCI، بین‌نصب (D-035).
- تغییر هشدارهای `dotnet format` در فایل‌های قدیمی (فاصله به‌جای Tab) عمداً اصلاح نشد (بازقالب‌بندی خارج از محدوده).
- `CURRENT_MIGRATION_WRITTEN_BY_EF_TOOLS` برای Migration بعدی که فقط ستون/ایندکس تغییر می‌دهد باید `False` شود.

## شناسه‌های Git
- Branch: `feature/corporate-groups-consolidation`
- Commit پیاده‌سازی: `a01a204fba8df16e8b274ffddb948b86c2a7274f`
- Pull Request: [#53](https://github.com/hamidreza1394/Accounting/pull/53) — ادغام‌شده
- Merge commit در `main`: `1d30436d25b4f1e76b95e76a764afe249fd84f6f`
- CI پس از ادغام: [39826940050](https://github.com/hamidreza1394/Accounting/actions/runs/39826940050) — همه Jobهای گزارش‌شده موفق

## پیگیری ممیزی پس از ادغام (Issue #54)
- اصلاح جداگانه: شناسه `ownerCompanyId` که دسترسی آن رد شده بود، دیگر در `SecurityAuditEvent.TenantId` ثبت نمی‌شود؛ تست رگرسیون برای این رفتار افزوده شده است.
- مستند یکتایی کد با ایندکس واقعی `(OwnerCompanyId, Code)` همسو شد.
- این گزارش اولیه سابقه زمان اجرا را حفظ می‌کند؛ برای وضعیت اصلاحات بعدی، Issue #54 و PR اصلاحی را ببینید.
