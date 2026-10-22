# ماتریس ردیابی نیازمندی‌ها

> «شاهد» یعنی فایل کد/تست. **وجود فایل تست به‌معنی اجرای موفق نیست.** تنها شاهد اجرا، اجرای CI شماره `39585708662` روی commit مبنا (همه ۶ Job سبز، ۲۰۴ تست .NET روی SQL Server 2022، ۱۲ تست E2E) است. این بازبینی مستنداتی خودش Build/Test اجرا نکرده است. ستون «وضعیت» از واژگان D-030 است.

| نیازمندی | ADR | شاهد کد | شاهد تست | وضعیت |
|---|---|---|---|---|
| FR-ID-01 ورود امن | D-012, D-014 | `Server/Infrastructure/Security/AuthenticationService.cs`, `Server/Pages/Account/Login.cshtml.cs` | `tests/Template.Tests/LoginAuthenticationTests.cs`, `tests/Template.Tests/Integration/HttpSecurityTests.cs`, `SessionValidatorTests.cs` | `Implemented`؛ `Verified` بر اساس CI 39585708662 در حد گزارش آن اجرا |
| FR-ID-03 دسترسی شرکت | D-013 | `Server/Infrastructure/Tenancy/CompanyAccessService.cs` | `CompanyAccessServiceTests`, `ApiEndpointTests` (`tests/Template.Tests/CompanyAccessTests.cs`)، `HttpSecurityTests` | `Implemented` |
| FR-ID-04 مجوز عملیاتی | D-025, D-034 | `CompanyAccessService.HasPermissionAsync`/`GetCompaniesWithPermissionAsync`؛ مصرف فقط در `Server/Infrastructure/CorporateGroups/CorporateGroupService.cs` | `CorporateGroupServiceTests.HasPermission_*` | بخشی `Implemented` |
| FR-GRP-01..05 گروه شرکتی | D-033, D-034 | `Domain/CorporateGroups/*`، `Infrastructure/Persistence/Configurations/{CorporateGroup*,OwnershipRelationship,Consolidation*}Configuration.cs`، Migration `20261022150943_AddCorporateGroups`، `CorporateGroupService` | `CorporateGroupRulesTests`، `CorporateGroupServiceTests`، `Integration/CorporateGroupSqlServerTests`، `DatabaseModelTests` | `Implemented`؛ `Verified` محلی 2026-10-22 روی SQL Server 2022 (CI ثبت نشده) |
| FR-GRP-06..08 | D-034, D-035 | — | — | `Blocked`/`NotImplemented`/`Proposed` |
| FR-ID-05 تفکیک هویت | D-029 | `Login.cshtml.cs` (Claim `AccountKind`) | — | بخشی `Implemented` |
| FR-LIC-01..08, 10 لایسنس | D-020..D-027 | هیچ | هیچ | `NotImplemented` |
| FR-LIC-09 Activation قدیمی | D-028 | `ActivationKeysHandlerMiddleware.cs`, `ExtensionMethods.cs` (بدون فراخوانی در `Program.cs`) | — | کد مرده؛ `Deprecated` |
| NFR-01 بدون CDN | قاعده `AGENTS.md` | `scripts/check_static_assets.py`, `ClientApp` `check:offline` | مرحله CI | `Implemented` |
| NFR-03 .NET 10 | D-007 | `global.json`, `Directory.Build.props` | CI Build | `Implemented` |

## ناهماهنگی‌های ثبت‌شده
- بیان نیازمندی «مصرف مجوز عملیاتی» با D-025 و کد فعلی ناهمخوان است (انحراف پیاده‌سازی از هدف، نه از تصمیم).
- نام کلاس‌های تست با `grep` روی `tests/Template.Tests` در تاریخ 2026-10-21 تأیید شد.
