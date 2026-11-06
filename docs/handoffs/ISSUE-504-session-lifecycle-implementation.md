# تحویل وظیفه — ISSUE-504-session-lifecycle-implementation

- **Issue:** #504 (زیرمجموعه #444، مرحلهٔ S7؛ تصمیم D-069)
- **Agent/مسئول:** Backend Wave-1 mission
- **تاریخ:** 2026-11-05
- **وضعیت:** انجام‌شده (در انتظار CI و Merge با اجازهٔ مالک)

## هدف و معیارهای پذیرش
- [x] یک رکورد `UserSession` به ازای هر ورود موفق کاربر (کاربر Infrastructure مستثنا).
- [x] رد SessionId مفقود/نامعتبر/ابطال‌شده/متعلق به دیگری/خارج از پنجرهٔ بی‌کاری — fail-closed در هر درخواست.
- [x] ابطال مستقل یک نشست بدون اثر بر نشست‌های دیگر؛ `revoke-others`؛ ابطال همه پس از تغییر رمز.
- [x] خروج صریح (API و Razor) نشست جاری را می‌بندد؛ رویدادهای Audit امن؛ بدون IP/UA در جدول نشست.
- [x] هیچ تستی حذف یا تضعیف نشد؛ تست‌های موجود قواعد قبلی (credential stamp، غیرفعال‌سازی حساب، enrollment pending) را همچنان با ردیف نشست می‌آزمایند.

## خلاصه تغییرات
- پیاده‌سازی کامل D-069 طبق دستور صریح مالک در پرامپ Wave-1 (الگوی ارتقای D-072؛ ADR از `Proposed` به `Implemented` ارتقا یافت و تصمیم‌های بازِ آن با همان دستور حل شدند):
  - **مدل:** `Domain/UserSession` (شناسه تصادفی، UserId، CreatedAtUtc/LastSeenAtUtc، RevokedAtUtc/RevokedByUserId/RevocationReasonCode، RowVersion؛ بدون TenantId/IP/UA) + Configuration + Migration افزودنی `20261105094700_AddUserSessions` با `dotnet ef migrations add` واقعی؛ زنجیره ۱۶ Migration با `verify_migration_model.py` تأیید شد.
  - **ورود:** `AuthenticationService.AuthenticateAsync` در مسیر موفق کاربر نشست می‌سازد و `LoginResult.SessionId` را برمی‌گرداند؛ `BuildClaimsPrincipal` همان شناسه را به‌عنوان Claim `SessionId` صادر می‌کند (الگوی GUID بی‌پشتوانه حذف شد؛ `WorkContextCookie` بی‌تغییر کار می‌کند).
  - **اعتبارسنجی:** `SessionValidator.IsSessionValidAsync` پس از بررسی حساب، ردیف نشست را می‌سنجد (موجود، متعلق به همین کاربر، غیرابطال‌شده، LastSeen در ۳۰ دقیقه) و LastSeen را حداکثر هر ۵ دقیقه تازه می‌کند (روی SQL Server با `ExecuteUpdateAsync` مجموعه‌ای؛ روی InMemory تست‌ها با نوشتن tracked).
  - **APIهای self-service:** `GET /api/v1/me/sessions` (فقط فعال‌ها، allowlist)، `POST /api/v1/me/sessions/{sessionId}/revoke` (idempotent؛ 404 یکسان برای ناموجود/بیگانه) و `POST /api/v1/me/sessions/revoke-others` (`{ revokedCount }`)؛ همه anti-forgery + rate limit حساس + no-store؛ ابطال نشست جاری sign-out می‌کند.
  - **تغییر رمز:** `UserPasswordService` همهٔ نشست‌های فعال را با دلیل `password_changed` ابطال می‌کند؛ CredentialStamp حفظ شد.
  - **خروج:** API logout و صفحه Razor logout نشست جاری را ابطال می‌کنند (شکست ابطال مانع خروج نمی‌شود).
  - **Audit:** رویدادهای `UserSessionRevoked` و `UserSessionsRevokedOthers` با برچسب فارسی در گزارش امنیتی مدیر نصب.
- خارج از محدوده (مستند در ADR): نگهداری/پاک‌سازی ردیف‌های revoked (D-070 Proposed) و ابطال نشست کاربر دیگر توسط مدیر نصب (NeedsApproval).

## فایل‌های مهم تغییرکرده
- `Domain/UserSession.cs`، `Domain/User.cs` (ناوبری Sessions)، `Infrastructure/Persistence/Configurations/UserSessionConfiguration.cs`، `Infrastructure/Persistence/DatabaseContext.cs`، `Infrastructure/Persistence/Migrations/20261105094700_AddUserSessions.*`، `DatabaseContextModelSnapshot.cs`
- `Server/Infrastructure/Security/SessionValidator.cs`، `Server/Infrastructure/Security/AuthenticationService.cs`، `Server/Infrastructure/Security/UserPasswordService.cs`، `Server/Infrastructure/Security/UserSessionService.cs` (جدید)
- `Server/Api/ApiEndpoints.cs` (سه endpoint + ابطال در logout)، `Server/Api/SecurityAuditAdministrationEndpoints.cs` (برچسب رویدادها)، `Server/Pages/Account/Login.cshtml.cs`، `Server/Pages/Account/Logout.cshtml.cs`، `Server/Program.cs` (DI)
- تست‌ها: `tests/Template.Tests/SessionValidatorTests.cs` (به‌روزرسانی به قرارداد جدید + ۳ تست D-069)، `tests/Template.Tests/UserSessionEndpointTests.cs` (جدید، ۷ تست)، `tests/Template.Tests/UserPasswordServiceTests.cs` (+۱)، `tests/Template.Tests/Integration/CustomerProvisioningClaimsSqlServerTests.cs` (هماهنگی helper با ردیف نشست)، `tests/Template.Tests/DatabaseModelTests.cs` (جدول جدید در فهرست)
- مستندات: `docs/DECISIONS.md` (D-069)، `docs/FRONTEND_API_CONTRACT_NEEDS.md` (بند ۴)، `docs/USER_ACCESS_MANAGEMENT.md` (§8)، `scripts/generate_migrations.py` (ثابت‌های Migration جاری)

## تصمیم‌های فنی
- هیچ موتور/جدول نشست موازی‌ای ساخته نشد؛ Claim موجود `SessionId` با ردیف واقعی anchor شد تا `WorkContextCookie` بدون تغییر بماند.
- روی SQL Server از `ExecuteUpdate` برای آخرین‌مشاهده/ابطال استفاده شد تا هیچ استثنای concurrency در مسیر cookie validation یا در رقابت دو ابطال رخ ندهد (ابطال ایدمپوتنت است)؛ شاخهٔ tracked فقط برای provider حافظه‌ایِ تست‌هاست.
- پیام 404 برای نشست بیگانه/ناموجود یکسان است (بدون افشای وجود شناسه)؛ ابطال تکراری رویداد Audit دوباره نمی‌نویسد.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `dotnet build Template.sln --no-restore -c Release` | موفق | ۰ خطا |
| `python3 scripts/generate_migrations.py --check` | موفق | «The generated migrations are up to date.» |
| `python3 scripts/verify_migration_model.py` | موفق | ۱۶ Migration، Snapshot و Up/Down منطبق با مدل |
| `dotnet test ... --filter-class Template.Tests.SessionValidatorTests` | موفق | ۱۰ از ۱۰ (شامل ۳ تست D-069 جدید) |
| `dotnet test ... --filter-class Template.Tests.UserSessionEndpointTests` | موفق | ۷ از ۷ (جدید) |
| `dotnet test ... --filter-class Template.Tests.UserPasswordServiceTests` | موفق | ۸ از ۸ (شامل ابطال همهٔ نشست‌ها) |
| `dotnet test Template.sln --no-build -c Release` | موفق (با همان ۲ استثنای محیطی پایه) | ۱۰۹۴ کل، ۱۰۳۰ موفق، ۶۲ Skip؛ ۲ ناموفق = `SafeExceptionRealApplicationTests` پایه (نیاز به SQL Server؛ Docker محلی ناموجود) |
| تست‌های SQL Server-gated و E2E | اجرا نشده (محیط) | در CI با SQL Server 2022 اجرا می‌شوند؛ رفتار concurrency ابطال روی SQL Server آزمون می‌شود |

## امنیت، داده و سازگاری
- امنیت: ابطال مستقل هر دستگاه؛ تغییر رمز/خروج/غیرفعال‌سازی حساب در درخواست بعدی اثر می‌کند؛ fail-closed روی هر ابهام؛ بدون افشای وجود شناسه نشست دیگران؛ Audit بدون داده حساس.
- داده: Migration افزودنی و بدون backfill؛ هیچ ردیف موجودی تغییر نمی‌کند.
- سازگاری: کوکی‌های پیش از استقرار بدون claim نشست یک‌بار رد می‌شوند و کاربر دوباره وارد می‌شود (پیشامد مستند ADR)؛ قراردادهای قبلی API تغییر نکرده‌اند.

## ریسک‌ها و کارهای باقی‌مانده
- Retention ردیف‌های revoked/idle همچنان طبق D-070 مسدود است (بدون پاک‌سازی خودکار).
- ابطال نشست کاربر دیگر توسط مدیر نصب و «metadata دستگاه» غنی‌تر (بدون IP/UA طبق D-069) نیازمند تصمیم مالک است.
- تست هم‌زمانی دو ابطال روی SQL Server واقعی در CI اجرا می‌شود (idempotency با `ExecuteUpdate` predicate-based تضمین شده).

## شناسه‌های Git
- Branch: `feat/session-lifecycle-d069`
- Commit: پس از push به‌روزرسانی می‌شود
- Pull Request: پس از ایجاد تکمیل می‌شود
