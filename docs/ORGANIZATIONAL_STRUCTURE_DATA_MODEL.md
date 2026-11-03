# مدل داده پیشنهادی — ساختار سازمانی و پرسنلی S2

- Parent: Issue #444
- Design review: Issue #491
- Decision reference: D-064 (Accepted)
- Status: Proposed; no schema/runtime authorization change is approved by this document.

## 1. شواهد مدل فعلی

- Domain.User یک هویت ورود در سطح Installation/Identity Realm است و اطلاعات حساب و تماس را نگه می‌دارد.
- Domain.UserCompanyMembership عضویت عملیاتی یک User در یک Company و نقش همان شرکت را نگه می‌دارد.
- یک User می‌تواند در چند Company عضویت مستقل داشته باشد.
- هیچ Entity مستقل Employee/Personnel، OrganizationalUnit یا Position در Domain فعلی وجود ندارد.
- ساختار فعلی ماژول Payroll/HR و مالکیت داده پرسنلی را پیاده‌سازی نکرده است.
- CompanyRole/CompanyRolePermissions موتور RBAC شرکتی است؛ D-064 صریحاً سمت و واحد را داده منابع انسانی و مستقل از مجوز تعریف می‌کند.

## 2. مسئله طراحی

اتصال مستقیم واحد و سمت به User اشتباه است، چون User سراسری نصب است و ممکن است در چند شرکت سمت‌های متفاوت داشته باشد. اتصال مستقیم به CompanyRole نیز اشتباه است، چون عنوان شغلی نباید مجوز ایجاد کند. اتصال مستقیم به UserCompanyMembership ساده است، اما فقط کارکنانی را مدل می‌کند که حساب ورود و عضویت عملیاتی دارند.

پرسش مالک محصول: آیا فرانو باید کارکنانی را که هنوز حساب ورود ندارند (یا اصلاً نباید وارد سیستم شوند) نیز به‌عنوان پرسنل شرکت ثبت کند؟

## 3. گزینه‌ها

### گزینه A — مدل حداقلی روی Membership

- افزودن OrganizationalUnitId و PositionId اختیاری به UserCompanyMembership.
- ایجاد CompanyOrganizationalUnit و CompanyPosition با TenantId.
- مزیت: تغییر کمتر و اتصال ساده به فرم کاربر.
- محدودیت: کارمند بدون حساب/عضویت عملیاتی قابل ثبت نیست؛ اطلاعات پرسنلی با چرخه دسترسی کاربر گره می‌خورد.

### گزینه B — مدل مستقل HR (پیشنهاد ترجیحی)

- CompanyEmployee یک رکورد پرسنلی مستقل و Company-scoped باشد.
- CompanyOrganizationalUnit و CompanyPosition هر دو Company-scoped باشند.
- CompanyEmployee به Unit و Position همان Company ارجاع دهد.
- CompanyEmployee بتواند بدون User و بدون UserCompanyMembership وجود داشته باشد.
- در صورت نیاز به ورود، Employee به یک UserCompanyMembership همان Company به‌صورت optional one-to-one متصل شود؛ عضویت همچنان مستقل و نیازمند approval است.
- یک User در دو شرکت می‌تواند دو Employee record مجزا با سمت/واحد متفاوت داشته باشد.
- واحد و سمت هیچ permission یا CompanyRole را خودکار اعطا نمی‌کنند.

## 4. طرح مفهومی گزینه B

### CompanyOrganizationalUnit
- Id, TenantId, Code, Name, ParentUnitId?, IsActive, audit timestamps, RowVersion.
- Unique (TenantId, Code).
- Alternate key (Id, TenantId) برای FKهای tenant-safe.
- ParentUnitId به همان Tenant محدود شود؛ حذف واحد دارای فرزند/کارمند Restrict باشد.
- جلوگیری از چرخه در ساختار درختی در application service با SQL Server transaction؛ FK به‌تنهایی cycle را متوقف نمی‌کند.

### CompanyPosition
- Id, TenantId, Code, Name, Description?, IsActive, audit timestamps, RowVersion.
- Unique (TenantId, Code) و alternate key (Id, TenantId).
- Position به Unit وابسته نباشد؛ یک Position می‌تواند در چند Unit استفاده شود. اگر کسب‌وکار position-per-unit بخواهد، آن رابطه باید جداگانه تصویب شود.

### CompanyEmployee
- Id, TenantId, EmployeeNumber, FirstName, LastName, OrganizationalUnitId?, PositionId?, ManagerEmployeeId?, UserCompanyMembershipId?, EmploymentStatus, HireDate?, TerminationDate?, audit timestamps, RowVersion.
- Unique (TenantId, EmployeeNumber).
- Composite FKهای (OrganizationalUnitId, TenantId)، (PositionId, TenantId) و (ManagerEmployeeId, TenantId) برای جلوگیری از ارتباط بین شرکت‌ها.
- UserCompanyMembershipId nullable و unique per Tenant؛ برای FK ترکیبی به membership باید کلید جایگزین (Id, TenantId) روی UserCompanyMembership طراحی شود.
- Employee بدون login مجاز است؛ membership بدون Employee نیز می‌تواند باقی بماند (مثلاً حساب فنی/مشاور).
- EmploymentStatus از User.IsActive و UserCompanyMembership.IsActive مستقل است؛ خاتمه همکاری به‌تنهایی session یا membership را revoke نمی‌کند مگر عملیات مجزای مجاز اجرا شود.

## 5. قواعد امنیتی غیرقابل‌تغییر

1. Company همان Tenant فعلی است؛ Tenant جدید ساخته نشود.
2. User هویت نصب است، Employee رکورد پرسنلی شرکت.
3. CorporateGroup برای ساختار HR داخلی یک شرکت استفاده نشود.
4. Position و OrganizationalUnit فقط اطلاعات سازمانی‌اند؛ نه permission، نه data scope، نه CompanyRole.
5. تغییر سمت/واحد هیچ permissionی را خودکار اضافه یا حذف نمی‌کند.
6. UserCompanyMembership فقط با گردش membership/approval موجود مدیریت می‌شود؛ Employee ایجاد membership نمی‌کند.
7. Employee، Unit، Position و Manager همگی باید در Tenant یکسان باشند.
8. APIها در هر درخواست company context را از سمت سرور و مجوز صریح resolve کنند؛ UI یا شناسه ارسالی مرجع دسترسی نیست.
9. نام/شماره پرسنلی/اطلاعات هویتی حساس نیازمند data minimization، audit و retention مستقل است؛ NationalId در این مدل پایه اضافه نمی‌شود.

## 6. API و Permission boundary پیشنهادی

مسیرهای احتمالی، پس از تأیید قرارداد:
- GET/POST /api/v1/admin/companies/{companyId}/organizational-units
- GET/PUT/DELETE /api/v1/admin/companies/{companyId}/organizational-units/{id}
- GET/POST /api/v1/admin/companies/{companyId}/positions
- GET/PUT/DELETE /api/v1/admin/companies/{companyId}/positions/{id}
- GET/POST /api/v1/admin/companies/{companyId}/employees
- GET/PUT /api/v1/admin/companies/{companyId}/employees/{id}

کدهای permission و تفویض باید در کاتالوگ server-side ثبت شوند؛ پیشنهاد نام کد در این سند به معنی ایجاد یا grant شدن آن نیست. دسترسی سمت/واحد نباید از permissionهای user یا role استنتاج شود.

## 7. Migration و backfill

- هیچ backfill خودکاری از User به Employee انجام نشود؛ همه Userهای فعلی الزاماً پرسنل نیستند.
- هیچ membership فعلی به Employee تبدیل نشود مگر با داده معتبر و تصمیم مستقل.
- Migration additive با composite FK، unique indexes، delete restrict و RowVersion.
- SQL Server integration tests: duplicate employee number، cross-tenant unit/position/manager/membership، hierarchy cycle، concurrent code creation، optional membership one-to-one، Employee بدون login.
- rollback plan و script preflight برای duplicate/orphan قبل از اعمال constraint.
- دیتابیس مشتری تا تأیید و برنامه استقرار مستقل دست‌نخورده بماند.

## 8. تصمیم موردنیاز

مالک محصول باید گزینه A یا B را انتخاب کند و تأیید کند آیا کارکنان بدون حساب ورود باید قابل ثبت باشند. پیشنهاد فنی گزینه B است؛ گزینه A فقط وقتی مناسب است که محصول رسماً اعلام کند همه پرسنل موردنیاز حتماً User و Membership دارند.
