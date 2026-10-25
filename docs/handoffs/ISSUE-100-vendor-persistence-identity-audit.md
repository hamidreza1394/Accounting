# تحویل وظیفه — ISSUE-100-vendor-persistence-identity-audit

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/100
- **Agent/مسئول:** ChatGPT / Faranou Senior Developer + Architecture Review + Security/Multi-Tenancy + Testing/Quality
- **تاریخ:** 2026-10-25
- **وضعیت:** Migration/Snapshot ثبت شده‌اند؛ منتظر CI روی source commit نهایی

## هدف و معیارهای پذیرش
- [x] پروژهٔ مستقل `Vendor/Vendor.Persistence` و `VendorDbContext` وجود دارد.
- [x] ASP.NET Core Identity EF store با نام جدول‌های اختصاصی `VendorAdmin*` تنظیم شده است.
- [x] `VendorAdminUser` به‌صورت deny-by-default با `IsEnabled=false` و `MustChangePassword=true` مدل شده است.
- [x] `VendorAuditEvent` با metadata محدود، PreviousHash/EventHash و بدون FK به مدیر مدل شده است.
- [x] Migration اولیه، `.Designer.cs` و `VendorDbContextModelSnapshot.cs` با EF Core 10.0.13 در source کنترل‌شده ثبت شده‌اند.
- [x] گام موقت scaffolding در CI حذف و بررسی دائمی `has-pending-model-changes` اضافه شد.
- [x] تست Integration که Migration را روی SQL Server واقعی اعمال می‌کند و جدول‌ها را بررسی می‌کند، در CI قبلی با source تولیدشده در workspace موفق بود.
- [ ] CI روی HEAD این follow-up (با Migrationهای commit‌شده) سبز شود و مدل بدون تغییر معوق تأیید شود.

## خلاصه تغییرات
- `Vendor/Vendor.Persistence/Vendor.Persistence.csproj`: پروژهٔ persistence مستقل با EF Core SQL Server، Identity EF store و EF Design.
- `Vendor/Vendor.Persistence/VendorDbContext.cs`: Identity schema با نام‌گذاری Vendor و تنظیمات hash/audit.
- `Vendor/Vendor.Persistence/Identity/VendorAdminUser.cs`: مدل مدیر انسانی مرکزی؛ هیچ مدیر پیش‌فرض یا seed ندارد.
- `Vendor/Vendor.Persistence/Auditing/VendorAuditEvent.cs`: مدل رخداد append record با ActorUserId بدون FK.
- `Vendor/Vendor.Persistence/VendorDbContextFactory.cs`: factory ابزار EF، بدون اتصال پیش‌فرض یا fallback به دیتابیس مشتری.
- `Vendor/Vendor.Persistence/Migrations/20261025180755_VendorInitialIdentityAndAudit.cs`, `.Designer.cs`, `VendorDbContextModelSnapshot.cs`: Migration و metadata تولیدشده با EF.
- `Vendor/Vendor.Host/Program.cs`, `Vendor.Host.csproj`, `Vendor.sln`: تنظیم اتصال با پیشوند `FARANO_VENDOR_` و ثبت پروژه در Solution مستقل.
- `tests/Vendor.Host.Tests/VendorPersistenceIntegrationTests.cs`: تست SQL Server واقعی، نام دیتابیس موقت یکتا و کنترل deny-by-default/audit no-FK.
- `.github/workflows/vendor-ci.yml`: بررسی snapshot به‌جای تولید Migration در زمان CI.
- `docs/DECISIONS.md`, `docs/LICENSING.md`, `docs/DEVELOPMENT.md`, `Vendor/README.md`, `docs/WORK_LOG.md`: وضعیت، تنظیمات، مرزها و ریسک‌ها.

## تصمیم‌های فنی
- تنظیمات محیط از `FARANO_VENDOR_ConnectionStrings__VendorDb` خوانده می‌شوند؛ `AddEnvironmentVariables(prefix: "FARANO_VENDOR_")` پیشوند را حذف و آن را به `ConnectionStrings:VendorDb` تبدیل می‌کند.
- Missing connection string هنگام resolve کردن `VendorDbContext` fail-closed است؛ `/health/live` همچنان liveness-only است و به SQL Server وابسته نیست.
- تست Integration دیتابیس با نام `FaranouVendorTests_<guid>` می‌سازد تا هرگز یک نام پیش‌فرض یا دیتابیس مشتری را هدف نگیرد؛ بعد از تست حذف می‌شود.
- جدول ممیزی عمداً FK به کاربر ندارد تا تاریخچه با حذف مدیر از بین نرود. این به‌تنهایی audit tamper-proof نیست؛ سرویس hash chain، درج محدودشده و checkpoint مستقل مرحلهٔ بعد هستند.
- هیچ login endpoint، MFA endpoint، bootstrap endpoint، RBAC enforcement یا صدور لایسنس در این مرحله افزوده نشده است.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `dotnet restore Vendor.sln` | موفق در CI قبلی | Run `40219629655`; restore پروژه قبل از scaffold موفق شد |
| `dotnet build Vendor.sln --no-restore --configuration Release` | موفق در CI قبلی | Build بدون migration files انجام شد؛ تست بعدی با migration تولیدشده در همان workspace ساخت/اجرا شد |
| EF `migrations add` + `has-pending-model-changes` | موفق در CI قبلی | برای منبع‌سازی Migration؛ فایل‌ها پس از آن در follow-up ثبت شدند |
| `dotnet test Vendor.sln --configuration Release` | موفق در CI قبلی | Migration در workspace تولید شده بود؛ test integration روی SQL Server اجرا شد. این نتیجه جایگزین CI روی commit حاوی Migrationهای ثبت‌شده نیست |
| Migration/Identity schema Integration | موفق در CI قبلی | جداول Identity و Audit ایجاد/بررسی شدند؛ دیتابیس موقت حذف شد |
| CI روی HEAD نهایی این PR | در انتظار | باید restore/build/test، migration drift check، publish و secret check را روی فایل‌های منبع commit‌شده تأیید کند |
| Build/Test محلی | اجرا نشده | .NET SDK در محیط این Agent موجود نبود |

## امنیت، داده و سازگاری
- هیچ تغییری در `Template.sln`, `Server/Server.csproj`, دیتابیس مشتری یا Migrationهای مشتری ایجاد نشده است.
- دیتابیس Vendor از طریق connection string مستقل تنظیم می‌شود؛ هیچ default connection string یا مدیر seedشده وجود ندارد.
- `VendorAuditEvent` فقط مدل/schema است؛ append-only enforcement، chain writer/verifier و external checkpoint بعداً باید اضافه شوند.
- هیچ secret، password واقعی یا private signing key به Git، workflow یا migration اضافه نشده است.

## ریسک‌ها و کارهای باقی‌مانده
- فقط پس از سبزشدن CI روی این commit می‌توان schema را `Verified` دانست.
- ورود مدیریتی هنوز در دسترس نیست؛ MFA اجباری، passkey/WebAuthn یا TOTP، session protection و tests باید در issue/PR مستقل طراحی شوند.
- bootstrap اولین مدیر باید یک‌بارمصرف، محدود، idempotent و ممیزی‌شده باشد؛ تا آن مرحله حساب مدیر ساخته نشود.
- RBAC سمت سرور، maker-checker، audit writer/verifier و checkpoint مستقل باقی‌اند.
- دیتابیس مشتری و Vendor نباید migration assembly، connection string، هویت یا cookie مشترک داشته باشند.

## شناسه‌های Git
- Branch: `fix/vendor-persist-migration-source`
- Commit: پس از آخرین تغییر تعیین شود.
- Pull Request: پس از ایجاد ثبت شود.