# تحویل وظیفه — ISSUE-802-vendor-runtime-identity

- **Issue:** [#802](https://github.com/hamidreza1394/Accounting/issues/802) — بخش PR-3 از مجموعهٔ ۵ PR (ادامهٔ PR-2: #804)
- **Agent/مسئول:** GLM (Autonomous Engineering)
- **تاریخ:** 2026-11-27
- **وضعیت:** انجام‌شده (ممیزی کامل هویت + ذخیرهٔ فقط Runtime + تست‌ها)؛ وضعیت کارکرد واقعی روی SQL Server مالک: Not yet Verified

## هدف و معیارهای پذیرش (این PR)
- [x] ممیزی کامل سطح سرور: **هر** عضویت نقش سرور (ثابت و نمونه‌ای شامل ##MS_…## 2022) رد می‌شود؛ GRANT غیر CONNECT SQL رد؛ DENY CONNECT SQL رد
- [x] کنترل Existing Login با اثبات Credential پیش از هر grant؛ عدم Reset و عدم فعال‌سازی خودکار (ادامهٔ PR-2)
- [x] تطبیق کاربر/Login از طریق SID و رد هم‌نامی متعلق به Login دیگر (در PR-2 اضافه شد و اینجا با تست پوشش می‌یابد)
- [x] رد دسترسی مدیریتی کاربر هدف (نقش‌های db_owner-کلاس و CONTROL/ALTER/TAKE OWNERSHIP/IMPERSONATE/VIEW DEFINITION)
- [x] رد mapping کاربر در هر دیتابیس دیگر (Cross-database) با فهرست دیتابیس‌ها در پیام
- [x] verify مؤثر: رد db_denydatareader/db_denydatawriter؛ اتصال readiness بدون Pooling
- [x] ذخیرهٔ فقط اتصال Runtime در Credential Manager با گارد منبع (Environment/Configuration ⇒ ذخیره نمی‌شود)؛ هیچ Admin Credential ذخیره یا برگردانده نمی‌شود
- [x] Sanitized Logging: DescribeSqlError عدد-محور و بدون محتوای استثنا؛ تست واحد برای عدم نشت
- [x] تست واحد قواعد خالص + تست SQL گیت‌دار Least Privilege (بدون ساخت DB/Login، بدون DDL، بدون دسترسی بین-دیتابیسی، CRUD سالم)
- [ ] اجرای واقعی روی SQL Server مالک (پس از تحویل، با مجوز عملیاتی جداگانه)

## خلاصه تغییرات
- فایل جدید `VendorDevelopmentRuntimeIdentity.cs`: قواعد خالص هویت Runtime و رد mapping بین-دیتابیسی؛ حذف کلاس تو در تو از pipeline و اتصال آن.
- pipeline: ممیزی رد mapping بین-دیتابیسی بر اساس SID؛ ذخیرهٔ اتصال Runtime پس از verify با `canSaveConnection`؛ `SavedToCredentialManager` در نتیجه؛ `DescribeSqlError` عدد-محور.
- `VendorDevelopmentDatabaseSetup.WriteSavedConnection` برای ذخیرهٔ فقط اتصال Runtime در Credential Target موجود (`Faranou.Vendor.Development.VendorDb`).
- تست‌ها: واحد قواعد/redaction + SQL گیت‌دار Least Privilege و سناریوهای Login غیرفعال و mapping بین-دیتابیسی.

## فایل‌های مهم تغییرکرده
- `Vendor/Vendor.Host/Development/VendorDevelopmentRuntimeIdentity.cs` — جدید
- `Vendor/Vendor.Host/Development/VendorDevelopmentDatabaseProvisioning.cs` — ممیزی بین-دیتابیسی، ذخیره، DescribeSqlError
- `Vendor/Vendor.Host/Development/VendorDevelopmentDatabaseSetup.cs` — WriteSavedConnection
- `tests/Vendor.Host.Tests/VendorDevelopmentRuntimeIdentityTests.cs` — جدید
- `tests/Vendor.Host.Tests/VendorDevelopmentDatabaseProvisioningSqlServerTests.cs` — تست‌های Least Privilege
- `docs/handoffs/ISSUE-802-vendor-runtime-identity.md`، `docs/WORK_LOG.md`، `docs/INDEX.md`

## امنیت، داده و سازگاری
- هیچ Provisioning/Admin Credential ذخیره یا لاگ یا برگردانده نمی‌شود؛ فقط اتصال Runtime کم‌اختیار در Credential Manager کاربر جاری.
- Passphrase هرگز در پیام خطا/لاگ؛ `PersistSecurityInfo=false` در همهٔ اتصال‌ها؛ اثبات با `Pooling=false`.
- بدون تغییر Customer/`Server/**`؛ بدون Migration جدید؛ بدون Endpoint جدید (PR-4).

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| `dotnet build Vendor.sln -c Release` | موفق | بدون error جدید |
| `dotnet test tests/Vendor.Host.Tests -c Release` | total 625 / succeeded 516 / failed 12 / skipped 97 | ۱۲ failed همان baseline محیطی؛ تست‌های جدید سبز |
| تست‌های SQL گیت‌دار | Skip محلی | اجرای واقعی در Vendor CI روی SQL Server 2022 Disposable |

## ریسک‌ها و کارهای باقی‌مانده
- ذخیرهٔ اتصال در Credential Manager فقط روی Windows آزموده می‌شود؛ مسیر غیر-Windows بدون ذخیره گزارش می‌شود.
- Endpointها و Bootstrap اپراتور در PR-4؛ UI در PR-5.

## شناسه‌های Git
- Branch: `fix/vendor-provisioning-runtime-identity`
- Commit/Pull Request: پس از push ثبت می‌شود
