# مدل داده دسترسی سال مالی و دوره حسابداری

- Issue: #488 (زیرمجموعه #444)
- ADR: D-065، D-066 و D-077
- وضعیت: طراحی مصوب محصول؛ پیاده‌سازی runtime و schema هنوز NotImplemented
- اصل راهبردی: تغییر افزایشی، حفظ CompanyRole/PermissionDefinitions و عدم اعطای دسترسی ضمنی

## 1. مفاهیم

| مفهوم | منبع حقیقت | معنا |
|---|---|---|
| هویت | `Users` | یک حساب در هر Installation؛ بین سال‌ها تکرار نمی‌شود |
| عضویت | `UserCompanyMemberships` | فقط رابطه User و Company و وضعیت عضویت؛ Pending/Inactive دسترسی نمی‌دهد |
| سال مالی | `FiscalPeriods` | رکورد سال مالی شرکت؛ جدول FiscalYear جدید ساخته نمی‌شود |
| دوره حسابداری | `AccountingPeriods` | ماه/فصل/دوره عملیاتی زیرمجموعه یک FiscalPeriod |
| نقش | `CompanyRoles` | تعریف نقش در سطح شرکت؛ مستقل از سال |
| مجوز نقش | `CompanyRolePermissions` + `PermissionDefinitions` | کاتالوگ واحد و عملیات واقعی |
| تخصیص نقش | `UserCompanyRoleAssignments` | اتصال عضویت به نقش در دامنه شرکت یا سال مالی |
| دوره‌های صریح | `UserCompanyRoleAssignmentPeriods` | AccountingPeriodهای مجاز برای تخصیص سالانه با policy انتخابی |

## 2. جداول پیشنهادی

### AccountingPeriods

- `Id` (PK)، `TenantId`، `FiscalPeriodId`، `Code`، `Title`، `StartDate`، `EndDate`، `Status`، فیلدهای پایه.
- FK ترکیبی `(FiscalPeriodId, TenantId)` به `FiscalPeriods(Id, TenantId)`.
- Unique: `(TenantId, FiscalPeriodId, Code)`.
- CHECK: `StartDate < EndDate`; بازه دوره باید داخل بازه FiscalPeriod باشد (اعتبارسنجی سرویس و constraintهای قابل‌پیاده‌سازی).
- بازه‌های هم‌پوشان در یک سال ممنوع؛ کنترل تراکنشی/Serializable و تست SQL Server. تاریخ‌ها Gregorian DateOnly در DB؛ نمایش جلالی فقط UI.
- Status: Open/Closed/Archived؛ سیاست write/read باید با D-032 هماهنگ باشد.

### UserCompanyRoleAssignments

- `Id` (PK)، `TenantId`، `MembershipId`، `RoleId`، `FiscalPeriodId` nullable، `PeriodAccessMode` nullable، `RowVersion`، `CreatedAtUtc`، `CreatedByUserId` و `Reason` در حد قرارداد Audit.
- `FiscalPeriodId = null`: تخصیص Company-scoped برای عملیات اداری غیرمالی.
- `FiscalPeriodId != null`: تخصیص سالانه؛ باید دقیقاً یک FiscalPeriod از همان Tenant داشته باشد و `PeriodAccessMode` اجباری باشد.
- Mode فقط `ExplicitPeriodsOnly` یا `AllPeriodsInYear`؛ برای Company scope مقدار mode باید null باشد.
- Composite FK: `(MembershipId, TenantId)` به `UserCompanyMemberships(Id, TenantId))، `(RoleId, TenantId)` به `CompanyRoles(Id, TenantId))، و `(FiscalPeriodId, TenantId)` به `FiscalPeriods(Id, TenantId)`.
- Unique filtered index: `(MembershipId, RoleId)` برای Company scope؛ `(MembershipId, FiscalPeriodId, RoleId)` برای سال.
- چند نقش در یک سال مجاز است؛ تخصیص تکراری همان نقش برای همان عضویت/سال مجاز نیست.
- Role باید فعال و متعلق به همان Company باشد؛ Membership باید Approved و فعال باشد. کنترل‌ها در transaction انجام می‌شوند و query نهایی نیز وضعیت جاری را دوباره ارزیابی می‌کند.

### UserCompanyRoleAssignmentPeriods

- `AssignmentId`، `TenantId`، `AccountingPeriodId`؛ کلید مرکب یکتا.
- Composite FK به تخصیص و دوره؛ دوره باید متعلق به همان FiscalPeriod و Tenant تخصیص باشد.
- فقط برای تخصیص سالانه با `ExplicitPeriodsOnly` مجاز است.
- `AllPeriodsInYear` هیچ ردیف انتخابی لازم ندارد و همه دوره‌های فعلی/آینده همان سال را پوشش می‌دهد.
- `ExplicitPeriodsOnly` بدون حداقل یک دوره صریح دسترسی دوره‌ای نمی‌دهد. افزودن دوره جدید، آن را خودکار مجاز نمی‌کند.
- انتخاب دوره‌ای با تغییر mode، حذف فیزیکی نمی‌شود مگر در عملیات صریح و Auditشده.

## 3. محاسبه دسترسی مؤثر

1. User فعال و نشست معتبر.
2. Company فعال و عضویت Approved/فعال همان Tenant.
3. Role assignment فعال/معتبر و Role فعال همان Tenant.
4. برای عملیات Company-scoped فقط assignment بدون FiscalPeriodId.
5. برای عملیات سالیانه، assignment همان FiscalPeriod.
6. برای عملیات دوره‌ای: یا mode برابر AllPeriodsInYear باشد، یا AccountingPeriodId در روابط صریح همان assignment وجود داشته باشد.
7. وضعیت سال/دوره و read-only policy D-032 بررسی شود.
8. Entitlement در صورت نیاز مستقل از RBAC ارزیابی شود (D-074).
9. permission دقیق از PermissionDefinitions/CompanyRolePermissions بررسی شود.
10. هر نبود/ابهام/شناسه cross-tenant/permission ناشناخته به deny منتهی شود.

هیچ authorization check دوره‌ای نباید به overload قدیمی Company-only برگردد. WorkContext معتبر سرور مرجع FiscalPeriod و AccountingPeriod است؛ شناسه ارسالی کلاینت به‌تنهایی قابل اعتماد نیست.

## 4. مهاجرت داده

- Expand: ایجاد جداول AccountingPeriods و RoleAssignments و AssignmentPeriods؛ افزودن FK/unique/check/index.
- Backfill: هر RoleId موجود در UserCompanyMembership به یک Company-scoped assignment منتقل شود تا دسترسی اداری فعلی قطع نشود؛ هیچ assignment سالانه یا دوره‌ای به‌صورت خودکار ساخته نشود.
- Validate: شمارش عضویت‌های دارای RoleId در برابر Company assignments؛ کشف duplicate/orphan/role inactive/cross-company.
- Contract: فقط پس از اصلاح همه مصرف‌کنندگان و تست‌ها، RoleId از UserCompanyMembership حذف شود. این حذف در همان PR اول انجام نشود.
- Rollback: قبل از Contract، rollback فقط جداول افزوده را حذف می‌کند؛ پس از Contract نیازمند Migration جبرانی و حفظ داده assignment است.
- Migration روی دیتابیس مشتری اجرا نمی‌شود؛ فقط از مسیر EF Migration و deployment کنترل‌شده.

## 5. فراخوانی دسترسی از سال دیگر

- مبدأ و مقصد باید دو FiscalPeriod فعال/معتبر همان Company باشند.
- فقط assignmentهای سالانه همان User و همان Membership/Company کپی می‌شوند؛ Company-scoped assignments کپی نمی‌شوند.
- Role باید در مقصد فعال و همان Company باشد.
- AllPeriodsInYear بدون لیست دوره کپی می‌شود.
- ExplicitPeriodsOnly با تطبیق `AccountingPeriod.Code` به مقصد نگاشت می‌شود؛ کدهای مفقود/تکراری/غیرفعال در preview به‌عنوان unresolved نشان داده می‌شوند و بدون تصمیم کاربر اعمال نمی‌شوند.
- Preview شامل role، policy، دوره‌های مقصد، موارد حذف/نامعتبر و سقف D-071 است.
- ذخیره در یک transaction؛ unique constraints و RowVersion رقابت را کنترل می‌کنند؛ تکرار درخواست idempotent؛ مبدأ بدون تغییر.
- Audit شامل actor، target user، company، source year، destination year، تعداد نقش‌ها/دوره‌ها، نتیجه و correlation؛ بدون رمز/PII غیرضروری.

## 6. تست‌های اجباری

- Company/Tenant isolation برای هر سه جدول و composite FK.
- عضویت Pending/Inactive، User/Company/Role غیرفعال و عدم assignment → deny.
- AllPeriodsInYear برای دوره فعلی و دوره‌ای که بعداً ساخته می‌شود.
- ExplicitPeriodsOnly فقط برای دوره‌های انتخاب‌شده؛ دوره جدید و انتخاب‌نشده deny.
- Company assignment فقط برای عملیات Company-scoped؛ هرگز جایگزین annual grant نشود.
- multiple roles، duplicate assignment، role inactive، RowVersion conflict.
- Copy same user/company/year; cross-company/source/destination rejection; missing period code preview; idempotent repeat; atomic rollback; audit.
- SQL Server واقعی برای migration، FK/filtered index/transaction/concurrency؛ E2E مسیر create user → membership → role/year assignment → login → allow/deny.

## 7. حدود این طراحی

- هیچ AccessGroup، direct UserPermission، Role-per-year duplicate یا permission catalog دوم ساخته نمی‌شود.
- CompanyRole تعریف شرکت‌محور باقی می‌ماند؛ نقش اختصاصی برای یک سال با ساخت CompanyRole مستقل در همان شرکت انجام می‌شود.
- تغییر Role مشترک روی تمام assignmentهایی که به آن اشاره می‌کنند اثر دارد و UI باید شمار کاربران/سال‌های متاثر را نشان دهد.
- Employee/HR data، password، sessions، login history و audit history در کپی سالانه دخیل نیستند.
