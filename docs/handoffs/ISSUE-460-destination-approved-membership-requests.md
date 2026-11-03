# تحویل وظیفه — ISSUE-460-destination-approved-membership-requests

- **Issue:** [#460](https://github.com/hamidreza1394/Accounting/issues/460)
- **Parent:** #444 / D-064 / D-065
- **Agent/مسئول:** ChatGPT
- **تاریخ:** 2026-11-03
- **وضعیت:** پیاده‌سازی API/مدل؛ Migration نیازمند اجرای dotnet ef در checkout توسعه

## هدف و معیارهای پذیرش
- [x] هر حساب فعال نصب می‌تواند برای شرکت مقصد درخواست دهد؛ UserId فقط از principal گرفته می‌شود و هیچ Source membership لازم نیست.
- [x] نگهداری Pending در جدول مستقل و عدم ایجاد دسترسی عملیاتی تا تأیید مقصد.
- [x] مجوز صریح و seedشده `users.membership.approve` در PermissionDefinitions.
- [x] تأیید/رد فقط توسط عضو فعال مقصد دارای مجوز؛ جلوگیری از self-approval.
- [x] انتخاب نقش فعال متعلق به مقصد؛ بدون اعطای ضمنی مجوز یا grant دوره مالی.
- [x] Anti-forgery، rate limit، audit، کنترل هم‌زمانی و تراکنش.
- [ ] تولید و بازبینی Migration و snapshot.
- [ ] اجرای Build، تست کامل، SQL Server integration و migration guards در CI.
- [ ] بازبینی نهایی امنیتی و رفع یافته‌ها.

## مدل داده
- `CompanyMembershipRequest` جدول مستقلی برای Pending/Approved/Rejected است و فقط DestinationTenantId را به Company متصل می‌کند.
- `UserCompanyMembership` فقط پس از تأیید ایجاد یا فعال می‌شود و همچنان تنها منبع عضویت عملیاتی است.
- درخواست به Company مقصد FK دارد؛ نقش تأییدشده با FK مرکب به `CompanyRoles(Id, TenantId)` محدود می‌شود.
- ایندکس یکتای فیلترشده فقط یک درخواست Pending برای هر جفت کاربر/شرکت مقصد را می‌پذیرد.
- `RowVersion` برای optimistic concurrency در SQL Server استفاده می‌شود.
- Check constraintها وضعیت و سازگاری فیلدهای تصمیم را enforce می‌کنند؛ rollback باید تاریخچه درخواست‌ها را حفظ یا پیش از حذف جدول export کند.
- Down migration فقط permission seed را وقتی حذف می‌کند که هیچ CompanyRolePermission از آن استفاده نکند.

## API
- `POST /api/v1/me/membership-requests`
- `GET /api/v1/me/membership-requests`
- `GET /api/v1/admin/companies/{companyId}/membership-requests/pending`
- `POST /api/v1/admin/companies/{companyId}/membership-requests/{requestId}/approve`
- `POST /api/v1/admin/companies/{companyId}/membership-requests/{requestId}/reject`

## امنیت
- ثبت درخواست فقط به حساب فعال و شرکت مقصد فعال نیاز دارد؛ این مسیر برای کاربری که هنوز هیچ عضویت عملیاتی ندارد نیز قابل استفاده است.
- approver در مقصد از طریق `CompanyAccessService.HasPermissionAsync` بررسی و درون تراکنش دوباره اعتبارسنجی می‌شود.
- self-approval ممنوع؛ role مقصد باید active و متعلق به همان Tenant باشد.
- تأیید، membership و request state و audit را در یک تراکنش ذخیره می‌کند؛ RowVersion و unique index از race جلوگیری می‌کنند. کاربر مقصد باید فعال باشد و نقش انتخابی فعال و متعلق به مقصد باشد.
- Pending در `CompanyAccessService` وارد نشده و در فهرست شرکت‌های قابل دسترسی ظاهر نمی‌شود.
- خروجی requester و approver حداقلی است؛ داده تماس، رمز/hash و جزئیات دسترسی افشا نمی‌شود.
- هیچ ClientApp یا فایل UI تغییر نکرده است.

## تست‌ها
- تست‌های InMemory برای درخواست، duplicate، عدم دسترسی، self-approval، نقش foreign/inactive، approve/reject، audit، anti-forgery، فهرست درخواست‌های خود و فهرست pending مقصد اضافه شده‌اند.
- تست SQL Server برای additive upgrade، حفظ عضویت‌های موجود، single-winner concurrent approval و down/up migration اضافه شده است.
- Migration در این commit هنوز در مخزن نیست. Generator رسمی مخزن به‌درستی ساخت entity/table جدید را رد می‌کند و صراحتاً `dotnet ef migrations add` را لازم می‌داند. فایل‌های EF-generated باید در checkout توسعه تولید و به همین شاخه اضافه شوند؛ هیچ workflow موقت CI باقی نمانده است.

## فایل‌های اصلی
- `Domain/CompanyMembershipRequest.cs`
- `Infrastructure/Persistence/Configurations/CompanyMembershipRequestConfiguration.cs`
- `Infrastructure/Persistence/DatabaseContext.cs`
- `Server/Api/CompanyMembershipRequestEndpoints.cs`
- `Server/Api/ApiEndpoints.cs`
- `scripts/generate_migrations.py`
- `tests/Template.Tests/CompanyMembershipRequestEndpointTests.cs`
- `tests/Template.Tests/Integration/CompanyMembershipRequestSqlServerTests.cs`
- `.github/workflows/dotnet.yml` (bridge موقت برای تولید artifact Migration؛ باید حذف شود)

## آزمون/اعتبارسنجی
- در محیط ChatGPT اجرای محلی .NET و SQL Server در دسترس نیست.
- نتیجهٔ واقعی Build/Test، generator، snapshot، SQL Server concurrency و migration guards فقط پس از تکمیل CI ثبت خواهد شد.
- Migration روی دیتابیس مشتری اجرا نشده و نباید اجرا شود.

## Git
- Branch: `agent/460-membership-approval`
- PR: پس از اجرای مرحلهٔ اول CI ثبت می‌شود.
