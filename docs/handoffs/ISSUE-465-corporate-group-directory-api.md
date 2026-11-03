# تحویل وظیفه — ISSUE-465-corporate-group-directory-api

- **Issue:** [#465](https://github.com/hamidreza1394/Accounting/issues/465)
- **Parent:** #444 / D-065
- **مسئول:** ChatGPT
- **تاریخ:** 2026-11-03

## هدف
ارائه API فقط‌خواندنی برای دفترچه محدود کاربران شرکت‌های عضو یک CorporateGroup مطابق D-065.

## رفتار و امنیت
- دسترسی فقط با `corporate-groups.view` در شرکت Owner و از طریق `CompanyAccessService.HasPermissionAsync` بررسی می‌شود.
- گروه غیرفعال، Owner غیرفعال/بدون CustomerBoundary، گروه با عضو خارج از مرز مشتری، گروه ناشناخته یا کاربر فاقد مجوز همگی fail-closed هستند؛ گروه غیرمجاز و ناشناخته هر دو 404 می‌دهند.
- فقط شرکت‌های فعال با عضویت گروهی معتبر در تاریخ UTC جاری وارد محدوده می‌شوند.
- خروجی allowlist فقط شامل UserId، DisplayName، Username، CompanyId، CompanyName و وضعیت مؤثر عضویت است.
- ایمیل، موبایل، رمز/hash، role، permission، login history، session، IP و audit جزئیات شرکت‌های دیگر در DTO وجود ندارند.
- جستجو فقط روی نام و username است؛ pagination سمت سرور و حداکثر pageSize برابر 100 است.
- endpoint هیچ مسیر تغییردهنده یا دسترسی عملیاتی به داده شرکت عضو ایجاد نمی‌کند.

## فایل‌ها
- `Server/Api/CorporateGroupDirectoryEndpoints.cs`
- `Server/Api/ApiEndpoints.cs`
- `tests/Template.Tests/CorporateGroupDirectoryEndpointTests.cs`
- `docs/handoffs/ISSUE-465-corporate-group-directory-api.md`
- `docs/WORK_LOG.md`
- `docs/INDEX.md`

## تست
- تست‌های InMemory برای scope شرکت‌های گروه، عدم افشای فیلدهای حساس، عدم مجوز، گروه ناشناخته، CustomerBoundary متناقض، جستجو، pagination و status عضویت اضافه شده‌اند.
- Build و اجرای تست در محیط فعلی انجام نشده است؛ .NET SDK در دسترس نیست و clone محلی GitHub به علت DNS ناموفق بود. CI باید پیش از Merge بررسی شود.
- SQL Server integration test در این برش اضافه نشده، چون تغییر Schema نداریم؛ تست API روی EF InMemory است.

## محدودیت/تصمیم
- قرارداد فعلی `corporate-groups.view` طبق D-034 در OwnerCompany سنجیده می‌شود؛ API همین قرارداد را reuse می‌کند و permission جدید یا Migration نمی‌سازد.
- این endpoint فقط دفترچه گروهی است و هیچ مجوزی برای مشاهده اطلاعات مالی/عملیاتی شرکت‌های دیگر نمی‌دهد.
- پیاده‌سازی مدیر شرکت عضو (غیر Owner) با مجوز اختصاصی directory نیازمند permission مستقل و تصمیم/seed جداست؛ در این برش ادعای پشتیبانی آن نمی‌شود.

## Git
- Branch: `agent/461-group-directory-api`
- Commit: پس از commit ثبت می‌شود.
- PR: پس از ایجاد ثبت می‌شود.
