# Handoff — S3-0 PR-3: قراردادهای مالکیت Tenant/Company و تست‌های معماری

- **Issue:** #397 (مبنا: D-087 Accepted)
- **Agent:** Claude Code
- **مبنا:** `origin/main@ad0c44a92ac164fded57601692f3a6dab008df1a` (شامل #661)
- **وضعیت:** قرارداد کد Implemented در شاخهٔ PR؛ مدل Tenant در Schema همچنان NotImplemented. هیچ Entity/جدول/ستون/Migration/Backfill/Schema، Provisioning، Unique Index، رفتار Authentication/Authorization، Trade یا Global Query Filter تغییر نکرد.

## بررسی اولیهٔ کد واقعی (بدون Blocker)
- `CompanyAccess` فقط `CompanyId` دارد و فقط در `CompanyAccessService` ساخته می‌شود؛ `WorkContext` فقط در `WorkContextService` ساخته می‌شود. عضویت با `membership.TenantId == companyId` (Legacy) خوانده می‌شود.
- هیچ قرارداد Tenant مستقلی وجود نداشت. `TradeAccessContext.TenantId`/`TradePeriodScope.TenantId` در Trade عملاً `Company.Id` است. `CustomerBoundaryId` فقط روی `Company` و در مسیرهای گروه/Provisioning استفاده می‌شود.

## تغییرات
- `Server/Infrastructure/Tenancy/OwnershipScope.cs` (جدید): `TenantScope`، `CompanyScope` و `OwnershipScope` با تنها Factoryهای `FromLegacyCompany(CompanyAccess|WorkContext)`؛ بدون تبدیل بین انواع/Guid، بدون Constructor عمومی، رد `Guid.Empty`.
- `WorkContext.cs`: خاصیت محاسباتی `Ownership` (با `[JsonIgnore]`؛ بدون تغییر رفتار).
- `tests/Template.Tests/OwnershipScopeArchitectureTests.cs` (جدید، ۱۰ تست): رفتار واقعی نوع‌ها و Guardهای ساختاری روی سورس (`CustomerBoundaryId` به‌جای Tenant؛ ساخت Scopeها فقط در `OwnershipScope`؛ ساخت `CompanyAccess`/`WorkContext` فقط توسط سرویس‌های خودشان) به‌همراه Fixtureهای منفی.
- `docs/SECURITY.md` بخش جدید؛ این handoff؛ ردیف WORK_LOG.

## تصمیم‌های فنی
- ساده‌ترین قرارداد: دو نوع تفکیک‌شده + نقطهٔ واحد معادلهٔ Legacy؛ Interface تکراری یا لایهٔ انتزاعی جدید ساخته نشد. قرارداد Query Filter به‌صورت مستندات (دو محدودهٔ مستقل، بدون Global Filter) ثبت شد، نه کد بی‌مصرف.
- Trade دست‌نخورده ماند (بدون وابستگی Trade→Core).

## تست
- نتایج واقعی در گزارش PR. Mutation: افزودن `new TenantScope(...)` و انتساب `tenantId = company.CustomerBoundaryId` در یک فایل Server، دو Guard را شکست داد؛ سپس برگردانده شد.

## Backlog (غیرمسدودکننده)
- Tenant واقعی (Entity/ستون/Backfill) و فیلتر Tenant مستقل: مراحل S3-0h بعدی با تصمیم و تأیید جداگانه.
- تغییر نام/نوع `TenantId` در قراردادهای Trade (`TradeAccessContext`, `TradePeriodScope`) و تصمیم Trade Migration Host.
- قرارداد Query Filter کد (Global Filter) فقط پس از طراحی context ورود/Jobها و تست‌های منفی cross-tenant.
- قرارداد Background Job/Export/Raw SQL فقط وقتی ضرورت مشخص شد.
