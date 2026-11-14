# مدل داده و قرارداد پیاده‌سازی — ساختار سازمانی و پرسنلی S2

- Parent: Issue #444
- Design review: Issue #491
- Decision reference: D-064 (Accepted)، D-065 (Accepted)، **D-072 (Accepted)** — مالک در مأموریت 2026-11-05 اجرای این طراحی را صریحاً دستور داد و سند با ادغام PR #542 پذیرفته شد.
- وضعیت: **Implemented (Backend)** — گزینه B با PR پیاده‌سازی (Entityها، Migration زنجیره‌ای `20261104200301_AddHrOrganizationalStructure`، کاتالوگ ۱۲ کد `hr.*`، Endpointهای `/org/*`، Audit و تست‌ها) تحویل شده است؛ UI در ClientApp و گردش‌کارهای عملیاتی تکمیلی در PR بعدی (فاز ۴) ادامه می‌یابند. این سند مرجع قرارداد باقی می‌ماند.

> **ملاحظه D-087:** Entityها و Migrationهای HR فعلی در مدل تاریخی Company=Tenant ساخته شده‌اند و TenantId آن‌ها عملاً CompanyId است. این وضعیت برای کد فعلی معتبر است، اما مدل هدف باید TenantId و CompanyId مستقل را رعایت کند. این سند، مجوز backfill یا Migration خودکار نیست؛ اثر تغییر بر HR باید در ممیزی S3-0 ثبت شود.

## 1. شواهد مدل فعلی

- Domain.User یک هویت ورود در سطح Installation/Identity Realm است و اطلاعات حساب و تماس را نگه می‌دارد.
- Domain.UserCompanyMembership عضویت عملیاتی یک User در یک Company و نقش همان شرکت را نگه می‌دارد؛ هر (UserId, TenantId) یکتاست.
- یک User می‌تواند در چند Company عضویت مستقل داشته باشد.
- هیچ Entity مستقل Employee/Personnel، OrganizationalUnit یا Position در Domain فعلی وجود ندارد.
- CompanyRole/CompanyRolePermissions موتور RBAC شرکتی است؛ D-064 صریحاً سمت و واحد را داده منابع انسانی و مستقل از مجوز تعریف می‌کند.
- مرحله دوم مدیریت دسترسی (مدیر شرکت، سقف تفویض مبتنی بر تصاحب، bootstrap امن، دفترچه هم‌گروه) با D-071 و PR #541 ادغام شده است؛ مدل پیشنهادی این سند روی همان زیرساخت (`CompanyAccessService`، کاتالوگ `PermissionDefinitions`، الگوی مجوزدهی دوحالته) سوار می‌شود و هیچ RBAC موازی نمی‌سازد.

## 2. مسئله طراحی

اتصال مستقیم واحد و سمت به User اشتباه است، چون User سراسری نصب است و ممکن است در چند شرکت سمت‌های متفاوت داشته باشد. اتصال مستقیم به CompanyRole نیز اشتباه است، چون عنوان شغلی نباید مجوز ایجاد کند. اتصال مستقیم به UserCompanyMembership ساده است، اما فقط کارکنانی را مدل می‌کند که حساب ورود و عضویت عملیاتی دارند.

پرسش مالک محصول: آیا فرانو باید کارکنانی را که هنوز حساب ورود ندارند (یا اصلاً نباید وارد سیستم شوند) نیز به‌عنوان پرسنل شرکت ثبت کند؟

## 3. گزینه‌ها

### گزینه A — مدل حداقلی روی Membership

- افزودن OrganizationalUnitId و PositionId اختیاری به UserCompanyMembership.
- ایجاد CompanyOrganizationalUnit و CompanyPosition با TenantId.
- مزیت: تغییر کمتر و اتصال ساده به فرم کاربر.
- محدودیت: کارمند بدون حساب/عضویت عملیاتی قابل ثبت نیست؛ اطلاعات پرسنلی با چرخه دسترسی کاربر گره می‌خورد.

### گزینه B — مدل مستقل HR (پیشنهاد ترجیحی؛ در انتظار تأیید مالک — D-072)

- CompanyEmployee یک رکورد پرسنلی مستقل و Company-scoped باشد.
- CompanyOrganizationalUnit و CompanyPosition هر دو Company-scoped باشند.
- انتساب Employee به واحد/سمت/مدیر به‌صورت رکورد تاریخچه مستقل (CompanyEmployeeAssignment) با Effective Dating نگه داشته شود — نه به‌صورت فیلد ثابت روی Employee.
- CompanyEmployee بتواند بدون User و بدون UserCompanyMembership وجود داشته باشد.
- در صورت نیاز به ورود، Employee به یک UserCompanyMembership همان Company به‌صورت optional one-to-one متصل شود؛ عضویت همچنان مستقل و نیازمند approval است.
- یک User در دو شرکت می‌تواند دو Employee record مجزا با سمت/واحد متفاوت داشته باشد.
- واحد و سمت هیچ permission یا CompanyRole را خودکار اعطا نمی‌کنند.

## 4. طرح پیاده‌سازی گزینه B (سطح Entity و Constraint)

مطابق قواعد مخزن، Entityها در `Domain` و Configurationها در `Infrastructure/Persistence/Configurations` تعریف می‌شوند؛ همه موجودیت‌ها `RowVersion` و فیلدهای Audit (`CreatedAtUtc`، `CreatedByUserId`، `ModifiedAtUtc`، `ModifiedByUserId`) دارند. الگوی کلید ترکیبی tenant-safe (مشابه `CompanyRole` با `HasPrincipalKey (Id, TenantId)`) برای همه FKهای بین موجودیت‌های HR استفاده می‌شود.

### 4.1 CompanyOrganizationalUnit (واحد سازمانی)

| فیلد | نوع | قاعده |
|---|---|---|
| Id | Guid | PK |
| TenantId | Guid | همان CompanyId؛ FK به Companies با DeleteBehavior.Restrict |
| Code | string | حداکثر ۵۰ کاراکتر؛ trim؛ Unique (TenantId, Code) |
| Name | string | الزامی، حداکثر ۲۰۰ کاراکتر |
| Description | string? | حداکثر ۱۰۰۰ کاراکتر |
| ParentUnitId | Guid? | FK ترکیبی (ParentUnitId, TenantId) به همان جدول؛ Restrict |
| IsActive | bool | پیش‌فرض true |
| ValidFromUtc / ValidToUtc | DateTime? | پنجره اعتبار اختیاری؛ UTC |
| Audit + RowVersion | — | الزامی |

قواعد اعمال‌شده در Application Service (درون تراکنش):
- **جلوگیری از چرخه والد:** هنگام Create/Update، زنجیره ParentUnitId تا ریشه پیمایش می‌شود (سقف عمق ۳۲)؛ رسیدن به خود گره = خطای 409. FK به‌تنهایی cycle را متوقف نمی‌کند.
- والد باید فعال، هم‌شرکت و در پنجره اعتبار باشد.
- **Deactivate/حذف:** واحد دارای فرزند فعال یا انتساب فعال کارمند → 409 (Restrict). حذف فیزیکی فقط برای رکورد بدون هیچ فرزند/انتساب/سابقه؛ در عمل مسیر استاندارد Deactivate است تا تاریخچه حفظ شود.
- غیرفعال‌سازی والد، فرزندان را خودکار غیرفعال نمی‌کند؛ اما ایجاد انتساب جدید به زیردرخت غیرفعال ممنوع است.

### 4.2 CompanyPosition (سمت سازمانی)

| فیلد | نوع | قاعده |
|---|---|---|
| Id | Guid | PK |
| TenantId | Guid | FK به Companies؛ Restrict |
| Code | string | حداکثر ۵۰؛ Unique (TenantId, Code) |
| Name | string | الزامی، حداکثر ۲۰۰ |
| Description | string? | حداکثر ۱۰۰۰ |
| Rank | int? | سطح/رتبه اختیاری؛ بدون معنای مجوز |
| IsActive | bool | پیش‌فرض true |
| Audit + RowVersion | — | الزامی |

قواعد:
- Position از Unit و Employee مستقل است؛ یک Position می‌تواند در چند Unit استفاده شود (تصمیم پیشنهادی بند «تأیید موردنیاز» handoff #491). رابطه position-per-unit در صورت نیاز محصولی، تصمیم جداگانه می‌خواهد.
- Deactivate/حذف Position دارای انتساب فعال → 409. سابقه انتساب‌های گذشته حفظ می‌شود.

### 4.3 CompanyEmployee (پرسنل)

| فیلد | نوع | قاعده |
|---|---|---|
| Id | Guid | PK |
| TenantId | Guid | FK به Companies؛ Restrict |
| EmployeeNumber | string | حداکثر ۳۰؛ Unique (TenantId, EmployeeNumber)؛ پس از ایجاد تغییر نکردنی است |
| FirstName / LastName | string | الزامی؛ حداکثر ۱۰۰ هرکدام |
| EmploymentStatus | enum | `Active`، `Suspended`، `Terminated` |
| HiredOnUtc | DateTime? | شروع همکاری |
| TerminatedOnUtc | DateTime? | پایان همکاری؛ فقط با `Terminated` |
| UserCompanyMembershipId | Guid? | لینک اختیاری به عضویت عملیاتی همان شرکت؛ FK ترکیبی (UserCompanyMembershipId, TenantId) با PrincipalKey (Id, TenantId) روی UserCompanyMemberships؛ **Unique فیلترشده روی (UserCompanyMembershipId, TenantId) `WHERE UserCompanyMembershipId IS NOT NULL`** — هر عضویت حداکثر یک Employee |
| Audit + RowVersion | — | الزامی |

قواعد:
- **Employee بدون User و بدون Membership کاملاً مجاز است.** نبود لینک هیچ محدودیتی در ثبت سوابق ایجاد نمی‌کند.
- لینک به Membership: عضویت باید `Approved` و فعال، متعلق به همان Tenant و بدون Employee متصل باشد؛ در غیر این صورت 409. لینک‌کردن هیچ مجوزی نمی‌سازد و membership را تأیید/ایجاد نمی‌کند.
- `Terminated` شدن Employeemembership را revoke یا نشست را ابطال نمی‌کند؛ آن عملیات‌ها فقط از مسیرهای مجاز مدیریت دسترسی انجام می‌شوند (D-064 بند ۲، D-065).
- حذف فیزیکی Employee دارای هر سابقه انتساب یا لینک → 409؛ مسیر استاندارد `Terminated` است تا سوابق و Audit حفظ شود.
- **Data minimization:** در نسخه اول NationalId، تاریخ تولد، آدرس، بانک و اطلاعات حساس ذخیره نمی‌شود؛ افزودن هر فیلد حساس نیازمند تصمیم محصولی، retention و audit جداگانه است.

### 4.4 CompanyEmployeeAssignment (انتساب سازمانی — تاریخچه مستقل)

| فیلد | نوع | قاعده |
|---|---|---|
| Id | Guid | PK |
| TenantId | Guid | FK به Companies؛ Restrict |
| EmployeeId | Guid | FK ترکیبی (EmployeeId, TenantId)؛ Restrict |
| OrganizationalUnitId | Guid? | FK ترکیبی به Unit هم‌شرکت |
| PositionId | Guid? | FK ترکیبی به Position هم‌شرکت |
| ManagerEmployeeId | Guid? | FK ترکیبی به Employee هم‌شرکت؛ خودِ Employee ممنوع |
| StartOnUtc | DateTime | الزامی (تاریخ مؤثر شروع) |
| EndOnUtc | DateTime? | پایان مؤثر؛ null = جاری |
| Status | enum | `Active`، `Ended` |
| Audit + RowVersion | — | الزامی |

Index و قواعد:
- **Unique فیلترشده:** `(TenantId, EmployeeId) WHERE Status = 1 (Active) AND EndOnUtc IS NULL` — هر Employee در هر لحظه حداکثر یک انتساب فعال. SQL Server filtered index؛ در EF با `HasFilter` تعریف می‌شود.
- Index پشتیبان: `(TenantId, OrganizationalUnitId)` و `(TenantId, PositionId)` برای پرس‌وجوی فهرست/درخت.
- **پایان انتساب = رکورد جدید، نه بازنویسی:** تغییر واحد/سمت/مدیر با `End` انتساب جاری و ایجاد انتساب جدید در همان تراکنش انجام می‌شود؛ تاریخچه هرگز بازنویسی نمی‌شود.
- **کنترل هم‌پوشانی:** ایجاد انتساب فعال جدید تنها وقتی مجاز است که انتساب فعال دیگری برای همان Employee وجود نداشته باشد؛ بررسی درون تراکنش با خواندن tracked و گارد RowVersion (الگوی `ApproveAsync`/`DelegationCeilingService`) تا race دو درخواست هم‌زمان با 409 حل شود.
- **جلوگیری از چرخه مدیریتی:** هنگام تعیین/تغییر ManagerEmployeeId، زنجیره مدیران (از طریق انتساب فعال هر مدیر) تا عمق ۳۲ پیمایش می‌شود؛ رسیدن به Employee مقصد = 409.
- واحد، سمت و مدیر باید هم‌شرکت، فعال و در پنجره اعتبار باشند؛ انتساب بین‌شرکتی در لایه سرویس و با FK ترکیبی هر دو مسیر ممنوع است.
- انتساب هیچ مجوزی ایجاد نمی‌کند؛ `ManagerEmployeeId` به‌تنهایی حق تأیید عضویت، مدیریت کاربر یا مشاهده داده مدیر مطلوب را نمی‌دهد (D-064 بند ۶، D-071).

### 4.5 Audit Events (رویدادهای `SecurityAuditEvent`)

`HrUnitCreated`، `HrUnitUpdated`، `HrUnitDeactivated`، `HrPositionCreated`، `HrPositionUpdated`، `HrPositionDeactivated`، `HrEmployeeCreated`، `HrEmployeeUpdated`، `HrEmployeeTerminated`، `HrEmployeeUserLinked`، `HrEmployeeUserUnlinked`، `HrAssignmentStarted`، `HrAssignmentEnded` — هر رویداد شامل actor، TenantId/CompanyId، شناسه هدف، در صورت لزوم مقادیر قبل/بعدِ کلیدی (بدون داده حساس) و correlation. رخدادهای ردشده (deny) مثل مسیرهای فعلی Audit نوشتنی ندارند چون هیچ نوشتنی رخ نمی‌دهد.

## 5. کاتالوگ مجوز پیشنهادی HR (server-owned codes)

ثبت در `PermissionDefinitions` با Migration فقط-داده‌ای و idempotent (همان الگوی D-071) **فقط پس از تأیید این طراحی**. تخصیص به نقش‌ها فقط از مسیر `CompanyRolePermissions` و تحت سقف تفویض مبتنی بر تصاحب (D-071) انجام می‌شود.

| کد | توضیح | نوع عملیات |
|---|---|---|
| `hr.employees.list` | فهرست کارکنان شرکت | خواندن |
| `hr.employees.view` | جزئیات یک کارمند | خواندن |
| `hr.employees.create` | ثبت کارمند جدید | نوشتن |
| `hr.employees.update` | ویرایش/خاتمه/فعال‌سازی کارمند | نوشتن |
| `hr.employees.user-link.manage` | اتصال/جداسازی کارمند به عضویت کاربر | نوشتن |
| `hr.organizational-units.list` | فهرست و درخت واحدها | خواندن |
| `hr.organizational-units.create` | ایجاد واحد | نوشتن |
| `hr.organizational-units.update` | ویرایش/غیرفعال‌سازی واحد | نوشتن |
| `hr.positions.list` | فهرست سمت‌ها | خواندن |
| `hr.positions.create` | ایجاد سمت | نوشتن |
| `hr.positions.update` | ویرایش/غیرفعال‌سازی سمت | نوشتن |
| `hr.assignments.manage` | شروع/پایان/تغییر انتساب (واحد، سمت، مدیر) | نوشتن |

قواعد مجوز:
- Scope همه کدها = شرکت مسیر، اثبات‌شده سمت سرور از `CompanyAccessService` (عضویت فعال + کد مجوز از دیتابیس در هر درخواست؛ بدون اتکا به Claim کوکی — D-013/D-064).
- **مدیر شرکت به‌صورت خودکار هیچ کد HR دریافت نمی‌کند؛** کدها باید صریحاً به CompanyRole تخصیص یابند و تخصیص از سقف تصاحب actor عبور کند.
- مدیر نصب همان الگوی دوحالته D-071 را دارد (بدون سقف، برای نقش‌های غیرسیستمی همان شرکت).
- کدهای HR با مجوزهای Trade/Fiscal/Licensing هم‌پوشان نیستند و از ساختار سازمانی استنتاج نمی‌شوند.
- عدم دسترسی Fail-Closed است؛ نبود عضویت فعال = 404 (عدم افشا)، عضویت بدون مجوز = 403 (الگوی `users.list`).

## 6. قرارداد API (پیشنهادی `/api/v1`، برای پیاده‌سازی پس از تأیید)

خانواده مسیرها با پیشنهاد ثبت‌شده در `docs/FRONTEND_API_CONTRACT_NEEDS.md` (بخش ۵) هم‌خوان است. همه endpointها: `RequireAuthorization` بر اساس `AccountKind`، Authorization سمت سرور، Anti-forgery برای متدهای تغییردهنده، پاسخ JSON با پیمانه `{ items, totalCount, page, pageSize }` برای فهرست‌ها، `no-store` و Rate Limit سیاست حساس برای خواندن‌های حاوی داده پرسنلی.

### Employee
| متد و مسیر | مجوز | بدنه/خروجی | خطاها |
|---|---|---|---|
| `GET /api/v1/companies/{companyId}/org/employees` | `hr.employees.list` | Query: `search?` (≤۱۲۰)، `status?`، `unitId?`، `page` (clamp 1..100000)، `pageSize` (clamp 1..100)؛ خروجی allowlist | 401/403/404 |
| `GET /api/v1/companies/{companyId}/org/employees/{id}` | `hr.employees.view` | خروجی allowlist + انتساب فعال + لینک user اختیاری (بدون username/تماس) | 401/403/404 |
| `POST /api/v1/companies/{companyId}/org/employees` | `hr.employees.create` | بدنه: `employeeNumber`, `firstName`, `lastName`, `hiredOnUtc?` | 400/401/403/404/409 |
| `PUT /api/v1/companies/{companyId}/org/employees/{id}` | `hr.employees.update` | بدنه: `firstName`, `lastName`, `employmentStatus`, `rowVersion` | 400/401/403/404/409 |
| `POST /api/v1/companies/{companyId}/org/employees/{id}/deactivate` | `hr.employees.update` | خاتمه همکاری؛ پایان انتساب فعال در همان تراکنش | 401/403/404/409 |
| `PUT /api/v1/companies/{companyId}/org/employees/{id}/user` | `hr.employees.user-link.manage` | بدنه: `userId` — resolve به عضویت Approved هم‌شرکت | 400/401/403/404/409 |
| `DELETE /api/v1/companies/{companyId}/org/employees/{id}/user` | `hr.employees.user-link.manage` | جداسازی لینک | 401/403/404/409 |

### Organizational Units
| متد و مسیر | مجوز | توضیح |
|---|---|---|
| `GET .../org/units?view=flat|tree&page=&pageSize=` | `hr.organizational-units.list` | درخت فقط از رکوردهای فعالِ قابل‌مشاهده ساخته می‌شود؛ چرخه در خروجی نمی‌تواند رخ دهد |
| `GET .../org/units/{id}` | `hr.organizational-units.list` | جزئیات + شمار فرزند/انتساب فعال |
| `POST .../org/units` | `hr.organizational-units.create` | بدنه: `code`, `name`, `description?`, `parentUnitId?`, `validFromUtc?`, `validToUtc?` |
| `PUT .../org/units/{id}` | `hr.organizational-units.update` | `name`, `description?`, `parentUnitId?`, `isActive`, `validFromUtc?`, `validToUtc?`, `rowVersion` |
| `POST .../org/units/{id}/deactivate` | `hr.organizational-units.update` | ممنوع با فرزند/انتساب فعال → 409 |

### Positions
| متد و مسیر | مجوز | توضیح |
|---|---|---|
| `GET .../org/positions?page=&pageSize=&search?` | `hr.positions.list` | فهرست صفحه‌بندی‌شده |
| `GET .../org/positions/{id}` | `hr.positions.list` | جزئیات |
| `POST .../org/positions` | `hr.positions.create` | `code`, `name`, `description?`, `rank?` |
| `PUT .../org/positions/{id}` | `hr.positions.update` | `name`, `description?`, `rank?`, `isActive`, `rowVersion` |
| `POST .../org/positions/{id}/deactivate` | `hr.positions.update` | ممنوع با انتساب فعال → 409 |

### Assignments
| متد و مسیر | مجوز | توضیح |
|---|---|---|
| `GET .../org/assignments?employeeId=&unitId?=&positionId?=&page=&pageSize=` | `hr.employees.view` | تاریخچه انتساب؛ خروجی allowlist |
| `POST .../org/assignments` | `hr.assignments.manage` | `employeeId`, `organizationalUnitId?`, `positionId?`, `managerEmployeeId?`, `startOnUtc` — انتساب فعال قبلی باید پایان‌یافته باشد یا در همان تراکنش `endActive` شود (body flag)؛ کنترل Effective Dating: شروع آینده‌نگر و شروع قبل از `HiredOnUtc` رد می‌شود (400) |
| `POST .../org/assignments/{id}/end` | `hr.assignments.manage` | `endOnUtc`؛ پایان مؤثر انتساب جاری |
| `POST .../org/employees/{id}/assignment-change` | `hr.assignments.manage` | تغییر واحد/سمت/مدیر = پایان جاری + شروع جدید در یک تراکنش؛ body: `organizationalUnitId?`, `positionId?`, `managerEmployeeId?` |

### گردش‌کارهای عملیاتی (فاز ۴)
| متد و مسیر | مجوز | توضیح |
|---|---|---|
| `GET .../org/units/{id}/subtree` | `hr.organizational-units.list` | زیردرخت واحد با شمار انتساب فعال هر گره؛ پیمایش bounded و cycle-safe؛ ترتیب فرزندان قطعی و پایدار (Code سپس Id، هم‌تراز فهرست واحدها) |
| `GET .../org/units/{id}/employees?page=&pageSize=` | `hr.organizational-units.list` | کارکنان دارای انتساب فعال در واحد با سمت و شروع مؤثر |
| `GET .../org/positions/{id}/employees?page=&pageSize=` | `hr.positions.list` | دارندگان فعال سمت در همه واحدها (استفاده مجدد سمت) |
| `GET .../org/employees/{id}/history?page=&pageSize=` | `hr.employees.view` | تاریخچه تغییرات کارمند از Audit Trail (رویدادهای Employee و Assignmentهای او) با خروجی Allowlist؛ ترتیب نزولی زمان وقوع با تساوی‌شکن قطعی (EntityId) برای پایداری صفحه‌بندی |

قواعد قرارداد:
- DTOها Allowlist و از Entity جدا هستند؛ `TenantId`/`CompanyId` هرگز از بدنه پذیرفته نمی‌شود و از مسیر + `CompanyAccessService` تعیین می‌شود (ضد Mass Assignment و IDOR).
- `rowVersion` در بدنه PUTها الزامی است؛ عدم تطابق → 409 (الگوی موجود).
- خطاها: 401 ناشناس؛ 404 غیرعضو/هدف ناموجود در شرکت (بدون افشای وجود)؛ 403 عضو بدون مجوز یا عمل ممنوع؛ 409 تعارض وضعیت (چرخه، هم‌پوشانی، رکورد غیرفعال، RowVersion)؛ 400 اعتبارسنجی.
- هیچ endpoint عمومی برای «ارتقای خودکار» یا جریان داده HR به RBAC وجود ندارد.

## 7. قواعد امنیتی غیرقابل‌تغییر

1. Company همان Tenant فعلی است؛ Tenant جدید ساخته نشود.
2. User هویت نصب است، Employee رکورد پرسنلی شرکت.
3. CorporateGroup برای ساختار HR داخلی یک شرکت استفاده نشود.
4. Position و OrganizationalUnit فقط اطلاعات سازمانی‌اند؛ نه permission، نه data scope، نه CompanyRole.
5. تغییر سمت/واحد/مدیر هیچ permissionی را خودکار اضافه یا حذف نمی‌کند.
6. UserCompanyMembership فقط با گردش membership/approval موجود مدیریت می‌شود؛ Employee ایجاد membership نمی‌کند و برعکس.
7. Employee، Unit، Position، Manager و Assignment همگی باید در Tenant یکسان باشند.
8. APIها در هر درخواست company context را از سمت سرور و مجوز صریح resolve کنند؛ UI یا شناسه ارسالی مرجع دسترسی نیست.
9. نام/شماره پرسنلی نیازمند data minimization، audit و retention مستقل است؛ NationalId در این مدل پایه اضافه نمی‌شود.
10. خروجی‌های HR فیلدهای حساس کاربر (username، تماس، نقش، مجوز، سوابق ورود) را افشا نمی‌کنند؛ لینک user فقط شناسه‌های allowlist را برمی‌گرداند.
11. Employee بودن یا مدیر سازمانی بودن، هیچ‌گاه معیار Authorization نیست؛ مرجع واحد RBAC همان `CompanyRole`/`CompanyRolePermissions` و `PermissionDefinitions` می‌ماند (D-064/D-065/D-071).

## 8. Migration و Backfill (پس از تأیید)

- Migration additive و فقط پس از تأیید صریح مالک روی گزینه A/B؛ زنجیره روی آخرین Migration (`20261104174711_SeedCompanyRoleAdministrationPermissions`) ساخته می‌شود، نه روی Snapshot قدیمی.
- تولید با `python3 scripts/generate_migrations.py --repo-root .` و راستی‌آزمایی با `python3 scripts/verify_migration_model.py --repo-root .` و `--check`؛ سپس اجرای واقعی روی SQL Server (نه InMemory).
- **هیچ backfill خودکاری از User به Employee انجام نشود؛** همه Userهای فعلی الزاماً پرسنل نیستند و هیچ membership فعلی به Employee تبدیل نمی‌شود مگر با داده معتبر و تصمیم مستقل.
- Preflight پیش از اعمال constraint: اسکریپت بررسی duplicate employee number/unit code/position code و عضویت‌های چندلینک.
- Rollback: جداول جدید با `Down` حذف‌شدنی‌اند تا وقتی خالی‌اند؛ هیچ داده موجودی تغییر/حذف نمی‌شود. اجرای Migration روی دیتابیس مشتری خارج از دامنه هر PR و نیازمند برنامه استقرار مستقل است.

## 9. برنامه تست (پیاده‌سازی پس از تأیید)

### Unit (بدون دیتابیس)
ایجاد Employee و اعتبارسنجی شماره پرسنلی؛ لینک اختیاری user؛ کنترل Company Scope؛ چرخه والد واحد؛ چرخه مدیریتی؛ هم‌پوشانی انتساب فعال؛ پایان انتساب و حفظ تاریخچه؛ عدم صدور خودکار permission/membership.

### Integration/API
CRUD کارکنان/واحدها/سمت‌ها/انتساب‌ها با Pipeline واقعی و Anti-forgery؛ درخت سازمانی؛ FK و Unique Index؛ Tenant Isolation و cross-company negative (شامل دستکاری `companyId` مسیر)؛ 401/403/404 مطابق قرارداد؛ Concurrency (RowVersion → 409)؛ Audit؛ بدون مجوز deny.

### SQL Server (gated با `FARANOU_TEST_SQLSERVER`؛ InMemory کافی نیست)
filtered unique index انتساب فعال؛ composite FK بین‌شرکتی؛ race هم‌زمان دو انتساب فعال (دو تراکنش واقعی)؛ migration idempotency و rollback؛ snapshot هم‌خوان.

### E2E (Published Server + SQL Server؛ `FARANOU_E2E_PUBLISH`/`FARANOU_E2E_SQL`)
ورود کاربر مجاز HR؛ مشاهده/ایجاد/ویرایش کارمند؛ ایجاد واحد و سمت؛ انتساب و تاریخچه؛ تلاش دسترسی به شرکت دیگر؛ تلاش بدون مجوز؛ اطمینان از اینکه مدیر سازمانی بدون کد مجوز HR هیچ عملیات مدیریتی دسترسی ندارد.

## 10. وابستگی‌ها و ترتیب پیاده‌سازی

1. تأیید مالک روی گزینه A/B و چهار پرسش باز (بند 11) — **مسدودکننده Schema/API/کد.**
2. ثبت کاتالوگ مجوز HR (Migration فقط-داده‌ای idempotent).
3. Domain + Configuration + Migration additive زنجیره‌ای.
4. Application Serviceها + Endpointها مطابق قرارداد بخش ۶.
5. تست‌ها (بخش ۹) و به‌روزرسانی مستندات (`ARCHITECTURE`، ERD، `DATABASE`، `SECURITY`، `TESTING`، `FRONTEND_API_CONTRACT_NEEDS`).
6. UI در ClientApp پس از ثابت‌شدن قرارداد؛ منو فقط بر اساس `/me/permissions`.

## 11. تصمیم موردنیاز از مالک (مسدودکننده پیاده‌سازی)

1. تأیید گزینه A یا B (پیشنهاد فنی: B — HR-ready).
2. آیا کارکنان بدون حساب ورود باید قابل ثبت باشند؟ (در B: بله)
3. ارتباط Employee ↔ Membership: حداکثر یک عضویت در همان شرکت؟ (پیشنهاد: بله، one-to-one هم‌شرکت)
4. Position مستقل از Unit و قابل استفاده در چند Unit؟ (پیشنهاد: بله؛ position-per-unit فقط با تصمیم جداگانه)

تا ثبت این تأییدها در Issue #491، هیچ Migration، Entity، API یا کد اجرایی از این طراحی ساخته نمی‌شود؛ این محدودیت عمدی و برای جلوگیری از مدل داده غیرقابل بازگشت است.
