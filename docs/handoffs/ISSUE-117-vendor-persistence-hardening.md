# تحویل وظیفه — ISSUE-117-vendor-persistence-hardening

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/117
- **Branch:** `security/issue-117-vendor-persistence-hardening`
- **مبنای کد:** `main` پس از PR #102 (`9b63c546c7a569498f6987137c686c2d7f9fb6ff`)
- **وضعیت:** تغییرات روی شاخه اعمال شده؛ CI این شاخه هنوز باید اجرا و بازبینی شود

## هدف و محدوده
ثبت وابستگی‌های Persistence و Identity در ماژول Vendor مستقل، و افزودن تست واقعی Migration روی SQL Server؛ بدون فعال‌کردن endpointهای حساس.

## تغییرات
- `Vendor/Vendor.Persistence/DependencyInjection.cs`: افزودن `AddVendorPersistence(IConfiguration)`، ثبت DbContext با connection string اختصاصی Vendor، ثبت `IUserStore<VendorAdminUser>` و `IRoleStore<IdentityRole<Guid>>`، تنظیمات پایهٔ password/lockout/email uniqueness.
- `Vendor/Vendor.Host/Program.cs`: حذف ثبت مستقیم EF Core و فراخوانی extension ماژول Persistence؛ `/health/live` همچنان مستقل از DB است.
- `tests/Vendor.Host.Tests/VendorPersistenceTests.cs`: تست مدل/نام جدول‌ها/default امن `IsEnabled=false`، نبود FK از audit به admin، fail-closed، Identity store registration، و Migration واقعی SQL Server.
- `tests/Vendor.Host.Tests/Vendor.Host.Tests.csproj`: وابستگی‌های مستقیم برای Identity EF و SQL Server.
- `.github/workflows/vendor-ci.yml`: هماهنگ‌سازی نام متغیرهای integration test.
- `Vendor/README.md`, `docs/PROJECT_STATUS.md`, `docs/WORK_LOG.md`: وضعیت و مرزهای اعتبارسنجی به‌روز شد.

## امنیت و حفاظت داده
- اتصال فقط از `FARANO_VENDOR_ConnectionStrings__VendorDb` دریافت می‌شود؛ fallback به دیتابیس مشتری وجود ندارد.
- تست Migration فقط loopback SQL Server و catalog پایهٔ `FaranouVendorTests` را می‌پذیرد؛ دیتابیس نام‌تصادفی ایجاد و حذف می‌شود.
- `EnsureDeletedAsync` پس از بستن اتصال اجرا می‌شود.
- هیچ Migration/Schema/کد اجرایی مشتری، مدیر پیش‌فرض، endpoint ورود/ثبت‌نام/bootstrap، secret یا کلید خصوصی افزوده نشده است.
- Identity password/lockout policy به‌تنهایی MFA اجباری را پیاده نمی‌کند؛ MFA باید پیش از انتشار پنل مدیریتی پیاده شود.

## تست‌ها و شواهد
| کنترل | وضعیت |
|---|---|
| CI PR #102، run `40220512121` (Vendor CI) و `40220512124` (.NET CI) | سبز؛ قبل از تغییرات این Issue، شامل model snapshot check |
| Build/Test محلی | اجرا نشده؛ .NET SDK در محیط این Agent در دسترس نیست |
| Vendor CI run `40272872248` | سبز؛ Restore/Build/model snapshot/Test/Migration واقعی SQL Server/Publish موفق |
| تست resolve شدن Identity stores | در همان Vendor CI سبز؛ درون `dotnet test Vendor.sln` اجرا شد |
| `git diff --check` / YAML lint محلی | اجرا نشده |

## باقی‌مانده
- Vendor CI run `40272872248` سبز است. باید `.NET CI` run `40272872260` نیز کامل شود؛ در صورت شکست، علت باید پیش از ادغام اصلاح شود.
- بازبینی SQL migration و `has-pending-model-changes` ضروری است.
- login/MFA/passkey/TOTP، bootstrap نخستین مدیر، RBAC، audit writer/verifier/checkpoint و maker-checker در Issueهای مستقل بعدی هستند.
- هیچ ادعایی دربارهٔ Production readiness وجود ندارد.

## Git
- Commitهای شاخه: تغییرات DI، تست‌ها، workflow و مستندات؛ آخرین commit پس از handoff ثبت می‌شود.
- PR: پس از ایجاد ثبت شود.
