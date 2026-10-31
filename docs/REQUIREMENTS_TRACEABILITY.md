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


## ردیابی D-060 — سه حوزه و تجربه فعال‌سازی Customer (2026-10-31)

| نیازمندی | تصمیم/Issue | کد/تست هدف | وضعیت |
|---|---|---|---|
| تفکیک Vendor CRM & Commercial | D-060 / #389 | commercial customer/contract/order/payment/support workflows | Accepted / NotImplemented |
| جداسازی Licensing & Entitlement Engine | D-059/D-060 / #390–#391, #393 | versioned contracts, signer, amendments, enforcement | Accepted / Partial foundation |
| Customer Deployment & Sync | D-060 / #384, #392, #394 | installation identity, bootstrap, API, offline packages, ACK | Accepted / Partial foundation |
| License-first startup gate | D-060 / #396 | server-side activation state middleware/endpoint tests | Accepted / NotImplemented |
| Valid license → normal sign-in | D-060 / #396 | server state resolver + login integration tests | Accepted / NotImplemented |
| No license → Activation Wizard | D-060 / #396 | route gate + direct URL/API negative tests | Accepted / NotImplemented |
| Restricted vs Unlicensed vs Invalid states | D-059/D-060 / #391, #396 | state machine and read/export/recovery authorization tests | Accepted / NotImplemented |
| Multi-company setup form with company-scoped entitlements | D-059/D-060 / #390, #392, #396 | local company setup, Identity, membership and no over-posting tests | Accepted / NotImplemented |
| Offline installation request and signed license response | D-060 / #394, #396 | request/response JSON contract, exact-byte, signature, replay tests | Accepted / NotImplemented |
| Optional armored text transport | D-060 / #394, #396 | byte-preserving encode/decode tests | Accepted / NotImplemented |
| Customer ACK/status export | D-059/D-060 / #394 | signed ACK, idempotency, freshness tests | Accepted / NotImplemented |

وضعیت‌های Partial foundation فقط به معنای وجود زیرساخت‌های قبلی است؛ به معنی تکمیل end-to-end نیست. مرجع اجرایی: LICENSING_IMPLEMENTATION_PLAN.md.


## ردیابی D-061 — ماژول بازرگانی خارجی

| نیازمندی | تصمیم/Issue | شاهد کد/تست هدف | وضعیت |
|---|---|---|---|
| ماژول مستقل در Accounting با امکان استخراج آتی | D-061 / #397 | Modules/Trade/* و TradeModuleFoundationTests | Foundation در حال اجرا |
| Company = TenantId و جداسازی داده | D-013/D-061 | ITradeAuthorization + query predicates + cross-company tests | Contract/Foundation؛ runtime enforcement NotImplemented |
| استفاده از دوره مالی مشترک فرانو | D-062 / #397 | WorkContext/FiscalPeriodSelectionPolicy + TradePeriodScope؛ بدون Trade period entity | Contract/Test؛ Host enforcement NotImplemented |
| مجوز تخصصی فرم/عملیات بازرگانی در سازوکار نقش مشترک | D-062 / #397 | TradePermissionCodes و ITradeAuthorization | ContractOnly تا اتصال Host و UI |
| entitlement مستقل TRADE_IMPORT | D-025/D-059/D-061 | TradeModuleIdentity و ITradeModuleEntitlement | ContractOnly؛ licensing runtime NotImplemented |
| گزارش چندشرکتی با مجوز همه شرکت‌ها | D-034/D-061 | ITradeAuthorization.HasPermissionForAllCompaniesAsync | ContractOnly؛ report use case NotImplemented |
| دیتابیس فیزیکی مشترک، schema مستقل trade | D-061 / #397 | TradeDbContext و model test | Foundation؛ Migration ایجاد نشده |
| اتصال آتی به مالی بدون coupling مستقیم | D-061 / #397 | dependency architecture tests | Foundation |
