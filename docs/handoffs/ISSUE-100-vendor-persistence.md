# تحویل وظیفه — ISSUE-100-vendor-persistence

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/100
- **Branch:** `feature/issue-100-vendor-persistence`
- **تاریخ:** 2026-10-26
- **وضعیت:** پیاده‌سازی آماده CI و بازبینی؛ ادغام نشده

## محدوده و تغییرات
- پروژهٔ مستقل `Vendor/Vendor.Persistence` با EF Core SQL Server و ASP.NET Core Identity EF store.
- `VendorDbContext` با schema پیش‌فرض `vendor` و جداول Identity با نام‌های `AdminUsers`, `AdminRoles`, `AdminUserClaims`, `AdminRoleClaims`, `AdminUserLogins`, `AdminUserRoles`, `AdminUserTokens`.
- `VendorAuditEvent` با ActorUserId بدون FK به مدیر، hash fields، correlation/action/target/outcome و indexهای زمانی.
- Migration `20261026090000_InitialVendorPersistence` و `VendorDbContextModelSnapshot`.
- DI فقط با `FARANO_VENDOR_ConnectionStrings__VendorDb`; بدون fallback به تنظیم اتصال مشتری. Host prefix محیطی را به configuration مستقل map می‌کند و در نبود DB همچنان فقط liveness را سرو می‌کند. `AddVendorPersistence` اکنون storeهای واقعی `IUserStore<VendorAdminUser>` و `IRoleStore<IdentityRole<Guid>>` را ثبت می‌کند؛ هیچ endpoint احراز هویت/ثبت‌نامی باز نشده است.
- Integration test با SQL Server واقعی، دیتابیس نام‌تصادفی و اجباری‌شدن متغیر تست در CI؛ تست قبل از ایجاد/حذف دیتابیس، loopback بودن SQL Server و catalog پایهٔ اختصاصی `FaranouVendorTests` را کنترل می‌کند تا Connection String اشتباه نتواند دیتابیس remote/customer را هدف بگیرد.
- به‌روزرسانی D-047، Project Status، Licensing Data Model، Vendor README و Work Log.

## امنیت و مرز داده
- هیچ endpoint ورود/ثبت‌نام/bootstrap/مدیریت اضافه نشده است.
- هیچ مدیر پیش‌فرض، seed یا secret/کلید خصوصی اضافه نشده است.
- هیچ Customer DB schema/migration یا کد اجرایی مشتری تغییر نکرده است.
- Hash fields به‌تنهایی ممیزی tamper-proof ایجاد نمی‌کنند؛ سرویس append-only، validation و checkpoint خارجی در scope بعدی‌اند.

## اعتبارسنجی
- Build/Test محلی اجرا نشده‌اند؛ SDK .NET در محیط ابزار در دسترس نیست. CI روی PR لازم است و باید پس از آخرین commit اجرا شود.
- CI باید build solution، تست مدل، تست fail-closed، اجرای migration روی SQL Server واقعی، صحت schema/table count و `GetAppliedMigrations` را تأیید کند.
- اگر migration یا model snapshot drift دارد، PR تا اصلاح آن نباید ادغام شود.

## موارد خارج از محدوده
- MFA/passkey/TOTP، bootstrap نخستین مدیر، RBAC و maker-checker.
- سرویس واقعی hash-chain/verification/export.
- CRM/قرارداد/کاتالوگ/صدور لایسنس و KMS/HSM.
- هرگونه تغییر دیتابیس مشتری.