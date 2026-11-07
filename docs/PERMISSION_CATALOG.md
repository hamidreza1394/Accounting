# کاتالوگ عملیات و مجوزهای فرانو (Permission & Operation Catalog)

- **Issue:** [#444](https://github.com/hamidreza1394/Accounting/issues/444) — Stage 2-C مأموریت تکمیلی (2026-11-08)
- **منبع حقیقت:** کاتالوگ `PermissionDefinitions` در دیتابیس (seed شده در ۶ Migration؛ دقیقاً **۷۳ کد**)، از کد واقعی `cb2ddd57` استخراج شد. این سند جایگزین یا کپی آن نیست؛ نگاشت کاربرد و تحلیل پوشش عملیات است.
- **مدل:** [D-075](DECISIONS.md) — عملیات = آخرین بخش کد؛ زیرسیستم = اولین بخش؛ نگاشت به زیرسیستم‌ها در [SUBSYSTEM_CATALOG.md](SUBSYSTEM_CATALOG.md).

## 1. فهرست کامل ۷۳ کد و محل اعمال

### users.* — عضویت و دفترچه شرکت (۲ کد)

| کد | نام فارسی (seed) | فعل | محل اعمال واقعی |
|---|---|---|---|
| `users.list` | مشاهده کاربران شرکت | list | `GET /companies/{id}/users` (`CompanyUserDirectoryEndpoints`) |
| `users.membership.approve` | تأیید درخواست عضویت در شرکت | approve | صف و تأیید/رد عضویت (`CompanyMembershipRequestEndpoints`؛ approve و reject هر دو با همین کد) |

### roles.* — گروه‌های دسترسی (۴ کد؛ مجوزدهی دوحالته D-071)

| کد | نام فارسی | فعل | محل اعمال |
|---|---|---|---|
| `roles.view` | مشاهده گروه‌های دسترسی شرکت | view | فهرست نقش‌ها + اعضای نقش (`CompanyRoleAdministrationEndpoints`) |
| `roles.create` | ایجاد گروه دسترسی در شرکت | create | ایجاد نقش |
| `roles.update` | ویرایش گروه دسترسی شرکت | update | ویرایش نقش (نقش سیستمی immutability حفظ می‌شود) |
| `roles.assign` | تخصیص نقش به اعضای شرکت | assign | انتساب عضو + انتقال گروهی اعضا |

### corporate-groups.* / reporting.* / consolidation.* — گروه شرکتی (۹ کد)

| کد | نام فارسی | فعل | محل اعمال |
|---|---|---|---|
| `corporate-groups.view` | مشاهده گروه شرکتی | view | `CorporateGroupService.GetVisibleGroupsAsync` |
| `corporate-groups.manage` | مدیریت گروه شرکتی و پروفایل تلفیق | manage | ایجاد گروه/پروفایل |
| `corporate-groups.members.manage` | مدیریت عضویت شرکت در گروه | manage | افزودن/پایان عضویت |
| `corporate-groups.ownership.manage` | ثبت روابط مالکیت و کنترل | manage | ثبت مالکیت |
| `corporate-groups.directory.view` | مشاهده دفترچه کاربران هم‌گروه | view | `GET /corporate-groups/{id}/directory` |
| `reporting.group-aggregate.view` | اجرای گزارش تجمیعی گروه | view | `StartReportRunAsync` (Aggregate) |
| `reporting.group-consolidated.run` | اجرای گزارش تلفیقی گروه | run | `StartReportRunAsync` (Consolidated؛ لازم در همهٔ شرکت‌های محدوده) |
| `reporting.group.export` | دریافت خروجی گزارش گروه | export | **seeded-only** — تا پیاده‌سازی خروجی گزارش گروه (ثابت `ExportGroupReport` فقط در تست) |
| `consolidation.adjustments.approve` | تأیید تعدیلات تلفیقی | approve | **seeded-only** — تا پیاده‌سازی تعدیلات تلفیق |

### trade.* — بازرگانی خارجی (۴۶ کد)

ثابت‌ها: `Modules/Trade/Trade.Application/Authorization/TradePermissionCodes.cs` + `TradePermissionCatalog.cs` (نام‌ها با seed یکسان). اعمال از مسیر عمومی `TradeAccessFilter → TradeAccessGate → TradeAuthorizationAdapter → CompanyAccessService` انجام می‌شود؛ **هیچ endpoint تولیدی هنوز کد trade را declare نکرده** و تنها `GET /trade/capabilities` کدهای اعطاشده را نمایش می‌دهد (fail-closed، `TradeEndpointGateTests`).

| فرم | کدها و افعال |
|---|---|
| طرف حساب | `trade.form.party.{view,create,update,delete,export}` |
| کالا | `trade.form.product.{view,create,update,delete,export}` |
| درخواست خرید | `trade.form.purchase-request.{view,create,update,approve,delete,export}` |
| استعلام و پیشنهاد تأمین‌کننده | `trade.form.supplier-inquiry.{view,create,update,approve,delete,export}` |
| مقایسه پیشنهادها | `trade.form.quotation-comparison.{view,create,approve,export}` |
| پیش‌فاکتور | `trade.form.proforma.{view,create,update,approve,delete,export}` |
| قرارداد | `trade.form.contract.{view,create,update,approve,delete,export}` |
| پرونده واردات | `trade.form.import-case.{view,create,update,approve,close,export}` |
| گزارش‌ها | `trade.reports.company.view`، `trade.reports.multicompany.view` |

### hr.* — ساختار سازمانی (۱۲ کد؛ همگی enforce)

ثابت‌ها: `HrOrganizationalStructureEndpoints.cs`. مسیرها `/companies/{companyId}/org/*`؛ مجوزدهی دوحالته D-071 (`AuthorizeHrScopeAsync`).

| کد | نام فارسی | فعل | محل اعمال |
|---|---|---|---|
| `hr.employees.list` | فهرست کارکنان شرکت | list | فهرست کارکنان |
| `hr.employees.view` | مشاهده جزئیات کارمند | view | پرونده کارمند + تاریخچه انتساب + فهرست انتساب‌ها |
| `hr.employees.create` | ثبت کارمند جدید | create | ایجاد کارمند |
| `hr.employees.update` | ویرایش و خاتمه همکاری کارمند | update | ویرایش + Deactivate/Terminate |
| `hr.employees.user-link.manage` | اتصال کارمند به حساب کاربری | manage | اتصال/جداکردن User (D-072؛ hint کلاینت ندارد، تعمداً) |
| `hr.organizational-units.list` | مشاهده واحدهای سازمانی | list | فهرست/جزئیات/زیردرخت/کارکنان واحد |
| `hr.organizational-units.create` | ایجاد واحد سازمانی | create | ایجاد واحد |
| `hr.organizational-units.update` | ویرایش و غیرفعال‌سازی واحد سازمانی | update | ویرایش + غیرفعال‌سازی |
| `hr.positions.list` | مشاهده سمت‌های سازمانی | list | فهرست/جزئیات/کارکنان سمت |
| `hr.positions.create` | ایجاد سمت سازمانی | create | ایجاد سمت |
| `hr.positions.update` | ویرایش و غیرفعال‌سازی سمت سازمانی | update | ویرایش + غیرفعال‌سازی |
| `hr.assignments.manage` | مدیریت انتساب سازمانی | manage | آغاز/پایان/تغییر انتساب (تاریخچه بازنویسی نمی‌شود) |

## 2. تاکسونومی افعال عملیات

افعال موجود در کاتالوگ (۱۱): `list`، `view`، `create`، `update`، `delete`، `approve`، `close`، `export`، `manage`، `run`، `assign`.

طبقه‌بندی رزروشده برای آینده Trade (`TradePermissionCodes`): Read = `.view/.export/.read/.list/.download`؛ Write = `.create/.update/.approve/.delete/.submit/.cancel/.post/.complete/.release/.close/.reopen/.issue/.send/.confirm/.reject/.receive/.settle` — پسوند ناشناخته fail-closed است.

### تطبیق با فعل‌های مأموریت

| فعل مأموریت | وضعیت در کاتالوگ | راه واقعی مخزن |
|---|---|---|
| مشاهده | ✓ `view`/`list` | — |
| ایجاد | ✓ `create` | — |
| ویرایش | ✓ `update` | — |
| حذف | ⚠️ فقط در `trade.form.*` (۷ کد، مشروط چرخه عمر) | مسیر استاندارد HR/ساختار: **Deactivate** با کد `.update` و 409 برای رکورد دارای سابقه (D-072)؛ حذف فیزیکی فقط رکورد بدون هیچ وابستگی |
| تأیید | ✓ `approve` | — |
| رد | بدون کد اختصاصی؛ رد عضویت با `users.membership.approve` گیت می‌شود؛ `.reject` فقط suffix آیندهٔ Trade است | — |
| ابطال | بدون کد شرکت‌محور؛ ابطال نشست عملیات مدیریتی نصب است (D-073، خارج از کاتالوگ شرکتی)؛ ابطال لایسنس آفلاین مسیر Vendor مستقل دارد | — |
| چاپ | بدون کد؛ جایگزین فعلی `export` است؛ اگر چاپ به‌عنوان عملیات مستقل لازم شود با نیاز واقعی و Migration فقط-داده اضافه می‌شود | — |
| خروجی | ✓ `export` | — |

## 3. سطح hint کلاینت (نمایشی؛ هرگز enforce)

`ClientApp/src/lib/permissions.ts` با اتحاد ۱۹ hint کار می‌کند؛ `can()` fail-closed است، مدیر نصب همه را می‌بیند و `admin.*` فقط برای مدیر نصب true است. نگاشت به کد سرور:

- `admin.roles.view/create/update/assign` → کدهای هم‌نام `roles.*`؛ `admin.permissions.view` → داشتن هر یک از چهار کد `roles.*`
- `hr.units.view/create/update` → `hr.organizational-units.*`؛ `hr.positions.*` → `hr.positions.*`؛ `hr.employees.view/create/update` → `hr.employees.*` (view → کد `list`)
- `admin.users.*` و `admin.security.view` → بدون کد شرکتی (فقط مدیر نصب)؛ `account.view` → همیشه true برای واردشده

دو صفحهٔ حساس عمداً hint ندارند: `/company/users` (`users.list`) و `/admin/membership-requests` (`users.membership.approve`) — سرور با 403/404 تصمیم می‌گیرد. هیچ hint مرده یا استفادهٔ ناشناخته‌ای وجود ندارد (اتحاد TS + ممیزی grep).

## 4. گروه‌بندی UI درخت مجوز

`lib/permissionTree.ts` فقط از رشتهٔ کد مشتق می‌کند و **هرگز به سرور فرستاده نمی‌شود**: زیرسیستم = بخش اول کد؛ منبع = بخش‌های میانی (≥۳ بخش)؛ عملیات = بخش آخر فقط از میان `view/list/create/update/delete/manage` — پسوند‌های دیگر (approve، run، close، export، assign) بدون نشان عملیات رندر می‌شوند. برچسب زیرسیستم فقط `admin` و `trade` فارسی است؛ بقیه کد خام نمایش داده می‌شوند و بخش «مجوزهای ناشناخته» کدهای خارج از کاتالوگ را حفظ ولی ذخیره را می‌بندد.

## 5. قواعد نگهداری کاتالوگ

1. `PermissionDefinitions` مرجع رسمی است؛ افزودن کد فقط با نیاز واقعی یک فرم/عملیات، Migration فقط-داده idempotent (الگوی D-071/D-072) و ردیف در همین سند انجام می‌شود.
2. **هیچ کد بی‌استفاده یا عمومی بیش‌ازحد اضافه نشود** — دو کد seeded-only فعلی (`reporting.group.export`، `consolidation.adjustments.approve`) از طرح مصوب گروه شرکتی‌اند و تا پیاده‌سازی ویژگی مربوط تعمداً بدون enforce مانده‌اند.
3. هر endpoint تازهٔ Trade باید `RequireTradePermission` declare کند وگرنه fail-closed رد می‌شود؛ افزودن کد تازه = ثابت در `TradePermissionCatalog` + seed + ردیف این سند.
4. واگرایی این سند با seed/کد = باگ مستندات؛ تغییر وضعیت‌ها فقط با شواهد (D-030).

## 6. شکاف‌ها و گام‌های بعدی واقعی

1. **۴۶ کد Trade بدون enforcement تولیدی** — باید هم‌زمان با endpointهای عملیاتی Trade (خارج از Stage 2) declare شوند؛ gate عمومی آماده است.
2. **دو کد seeded-only گروه شرکتی** — با ویژگی خروجی گزارش/تعدیلات تلفیق فعال می‌شوند.
3. **فعل‌های رد/ابطال/چاپ** به‌عنوان کد مستقل فعلاً نیاز واقعی ندارند؛ فقط با فرم واقعی آینده اضافه شوند.
4. عدم وجود hint برای `hr.employees.user-link.manage` و `hr.assignments.manage` تعمداً است (UI این عملیات‌ها با گیت `hr.employees.update` نمایش داده می‌شود؛ تعمیم درخت گیت در فاز بعدی HR).
