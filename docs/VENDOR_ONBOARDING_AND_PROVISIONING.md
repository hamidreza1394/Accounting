# ممیزی Onboarding و Provisioning مشتری — مرحله سوم مأموریت Vendor (2026-11-20)

مرجع تصمیم‌ها: `docs/DECISIONS.md` (D-056، D-059، D-064/D-065/D-071، D-073، D-107، D-108، D-109). وضعیت Issueهای مرتبط: #392 باز (Company-scoped Blocked)، #393 در PR #739.

## ۱. جدول ممیزی قابلیت‌ها

| قابلیت | وضعیت واقعی | محل پیاده‌سازی | وابستگی | اقدام لازم |
|---|---|---|---|---|
| Customer Registration | Implemented / Verified | Vendor: `VendorBusinessEndpoints` (CRUD + unique CustomerNumber) | ندارد | عدم تغییر |
| Installation Registration | Implemented / Verified | Vendor: `VendorBusinessEndpoints` (InstallationCode unique، LastSeenAtUtc) | مشتری | عدم تغییر |
| License Activation | Implemented / Verified | Customer: رابط RTL فعال‌سازی (#396، PR #735) | لایسنس امضاشده | عدم تغییر |
| Provisioning Package Creation | Implemented / Verified | Vendor: `POST /api/vendor/provisioning-requests` + `VendorProvisioningRequestRules` (RequestId+Nonce unique، dedupe با `MatchesExisting`) | #392 برای Company-scoped | عدم تغییر |
| Provisioning Approval | Implemented / Verified | Vendor: approval با مجوز دوگانه CustomersManage+InstallationsManage | — | عدم تغییر |
| Signing (fail-closed) | Implemented / Verified | Vendor: `VendorProvisioningIssuanceService`، وضعیت AwaitingSigner→Signed، D-056 | Signer واقعی (#236) | عدم تغییر |
| Package Export / Delivery | Implemented / Verified | Vendor: `POST /{id}/package/export` | ACK | عدم تغییر |
| ACK Verification | Implemented / Verified | Vendor: `POST /{requestId}/acknowledgement` — verify امضای مشتری با trust store، Idempotent retry دقیق، Conflict برای outcome متفاوت، **و در این مرحله: ثبت LastSeenAtUtc نصب در همان تراکنش** | — | انجام شد |
| Customer Apply (Company+Admin) | Implemented / Verified | Customer: `CustomerProvisioningApplyService` — تعارض نام کاربری/ایمیل، سقف MaxCompanies، CompanySetupRecord، ACK امضاشده | #392 | عدم تغییر |
| Initial Administrator (credential) | Implemented / Verified | Customer: رمز تصادفی غیرقابل استفاده + `MustChangePassword` + Enrollment یک‌بارمصرف (hash+انقضا+مصرف در همان تراکنش Serializable+قفل+rate-limit 5/15min+SecurityAudit؛ سیاست رمز D-073) | — | عدم تغییر |
| Initial Administrator (username choice) | Partially Implemented / Needs Owner Decision | Vendor فرم نام کاربری/ایمیل مدیر اولیه را در package ثبت می‌کند؛ مشتری در Enrollment باید همان نام را وارد کند و فقط رمز را خودش انتخاب می‌کند | تغییر payload (v6) + تغییر سمت Customer | تصمیم مالک: آزادسازی انتخاب نام کاربری در Enrollment نیازمند تغییر schema و سمت مشتری است — بدون مجوز انجام نشد |
| Enrollment Reissue (Retry) | Implemented / Verified | Customer: `POST /provisioning/{requestId}/enrollment/reissue` | — | عدم تغییر |
| DB Topology (Tenant) | Implemented / Current | Customer: یک دیتابیس برای هر Installation؛ Company و FiscalPeriod ردیف‌های همان دیتابیس (TenantId=Company.Id) | — | گزینه فعال فعلی |
| DB Topology (per Company) | Not Implemented / Needs Owner Decision | — | Connection Routing معتبر | فقط مستند شد؛ فعال‌سازی بدون تأیید مالک ممنوع |
| DB Topology (Company×FiscalPeriod) | Not Implemented / Blocked | — | تصمیم مالک (صریحاً در مأموریت) | مستند شد؛ پیاده‌سازی ممنوع |
| Runtime Gate | Implemented / Verified | Customer: اجرای Licensing Enforcement روی payload امضاشده | — | عدم تغییر |
| Company-scoped Entitlement | Blocked | Vendor+Customer | **#392 باز** | تا حل کامل mapping، هیچ Binding حدسی ساخته نمی‌شود |

## ۲. تحلیل توپولوژی دیتابیس (فقط بررسی — هیچ فعال‌سازی‌ای انجام نشد)

1. **یک دیتابیس برای کل Tenant (= یک دیتابیس برای هر Installation — وضعیت فعلی):** جداسازی مشتری با Installation-boundary؛ ساده‌ترین مسیر پشتیبان‌گیری/بازیابی و حفظ داده؛ هزینه رشد داده با Company/FiscalPeriod به‌صورت ردیفی کنترل می‌شود. این گزینه فعال است و با D-059 سازگار است.
2. **یک دیتابیس برای هر Company:** جداسازی قوی‌تر برای مشتریان چندشرکتی بزرگ؛ هزینه: Connection Routing مبتنی بر نگاشت معتبر Company→DB، مدیریت Migrationهای N-تایی، پیچیدگی Backup و تراکنش‌های Cross-Company. **نیازمند تصمیم مالک و ADR جدید است.**
3. **یک دیتابیس برای هر ترکیب Company و FiscalPeriod:** بیشترین انزوا برای بستن دوره مالی؛ هزینه: انفجار تعداد DBها، Routing با کلید مرکب، گزارش‌گیری Cross-Period دشوار. **طبق مأموریت بدون تأیید مالک پیاده‌سازی نمی‌شود.**

نتیجه: مدل فعلی حفظ شد؛ Connection String و Credential افشا نمی‌شوند و هیچ نگاشت حدسی Customer/Company/Tenant/Installation/Entitlement ساخته نشده است.

## ۳. تغییر کد این مرحله (حداقل و واقعی)

- **ثبت سلامت اتصال اولیه:** در endpoint ACK وند (`VendorProvisioningEndpoints`)، هنگام پذیرش نخستین ACK امضاشده و معتبرِ مشتری، `VendorInstallation.LastSeenAtUtc` در همان تراکنش Serializable مهر می‌شود؛ Retry دقیقِ همان ACK زودتر برمی‌گردد و timestamp را بازنویسی نمی‌کند. این تنها داده سلامت نصبی است که تا امروز هیچ نویسنده‌ای نداشت. (این تغییر در commit پایه زنجیره `1ee03fb6` روی شاخه مرحله دوم ثبت شده و این شاخه بر مبنای آن است.)
- تست SQL مربوطه اکنون `LastSeenAtUtc != null` را پس از Confirmed بررسی می‌کند.

## ۴. موارد Blocked و ریسک‌ها

- **Blocked:** انتخاب نام کاربری مستقل توسط مشتری (نیازمند تصمیم مالک + تغییر payload/customer)، فعال‌سازی توپولوژی‌های ۲ و ۳، Company-scoped entitlement (#392)، Signer عملیاتی واقعی (#236) و sink مستقل Audit (#107).
- **ریسک:** ACK تنها سیگنال سلامت Vendor-side است؛ وضعیت «پسورد اولیه ست شده» در Vendor دیده نمی‌شود (مرز طراحی: Enrollment یک فرایند Customer-side است). Stage 4 این مرز را در Diagnostics رعایت می‌کند.
