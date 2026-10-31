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


## ردیابی D-059 — entitlement و sync (2026-10-31)

| نیازمندی | تصمیم/Issue | کد/تست هدف | وضعیت در زمان ثبت |
|---|---|---|---|
| Base License واحد برای Installation و amendment همان LicenseId | D-059 / #390 | Licensing.Contracts schema/version + validator tests | Accepted / NotImplemented |
| Company-scoped Module/Feature/UserSeat | D-059 / #390 | typed entitlement contract + serialization/signature vectors | Accepted / NotImplemented |
| Seat reduction = block-new | D-059 / #391 | central customer policy + concurrency/authorization tests | Accepted / NotImplemented |
| Feature limit reduction بدون حذف داده | D-059 / #391 | policy enforcement + over-limit tests | Accepted / NotImplemented |
| Term expiry = restricted، بدون grace ضمنی | D-059 / #391 | operation policy matrix + read/export/recovery tests | Accepted / NotImplemented |
| ساخت Company با maxCompanies | D-059 / #392 | customer company creation use case + concurrent limit tests | Accepted / NotImplemented |
| Offline local admin bootstrap | D-059 / #392 | one-time bootstrap + Identity + secret-boundary tests | Accepted / NotImplemented |
| Vendor add-on purchase و signed amendment | D-059 / #393 | commercial order + maker-checker + signer + audit tests | Accepted / NotImplemented |
| online/offline dual sync | D-059 / #394 | authenticated API + signed file import/export + replay tests | Accepted / NotImplemented |
| Offline usage freshness | D-059 / #394 | last-sync metadata + stale UI projection tests | Accepted / NotImplemented |

کد و تست هدف تا زمان ادغام PRهای مربوطه به‌عنوان شاهد موجود تلقی نمی‌شوند. مرجع ترتیب و معیار پذیرش: [LICENSING_IMPLEMENTATION_PLAN.md](LICENSING_IMPLEMENTATION_PLAN.md).
