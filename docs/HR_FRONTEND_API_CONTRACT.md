# قرارداد API منابع انسانی برای Frontend (S2 — D-072)

- **وضعیت:** `Implemented` — قرارداد مطابق کد واقعی `main` (مبنای `5715df82` + PR سخت‌سازی قرارداد) نوشته شده است؛ هیچ فیلد یا Endpoint حدسی در این سند نیست.
- **Issue مرجع:** #491 (زیرمجموعه #444) · **تاریخ:** 2026-11-05
- **منابع حقیقت کد:** `Server/Api/HrOrganizationalStructureEndpoints.cs`، `Domain/Hr/*`، `Infrastructure/Persistence/Migrations/20261104200301_AddHrOrganizationalStructure.cs`
- **سند طراحی:** `docs/ORGANIZATIONAL_STRUCTURE_DATA_MODEL.md` (بخش ۶) · **نیازهای کلی:** `docs/FRONTEND_API_CONTRACT_NEEDS.md` (بخش ۵)

## ۰. قواعد مشترک همهٔ Endpointهای HR

| موضوع | قرارداد |
|---|---|
| Base Path | `/api/v1/companies/{companyId}/org/*` — `{companyId}` فقط از مسیر؛ هرگز از body/query به‌عنوان مجوز پذیرفته نمی‌شود |
| احراز هویت | Cookie هم‌مبدأ؛ `AccountKind=User` الزامی (حساب‌های غیرکاربر ۴۰۳ می‌گیرند) |
| Anti-forgery | همهٔ متدهای نوشتن (`POST/PUT/DELETE`) توکن لازم دارند؛ `GET /api/v1/antiforgery/token` |
| Rate Limit | سیاست `ApiSensitivePolicy` روی همهٔ Endpointهای HR |
| Cache | همهٔ پاسخ‌های موفق `Cache-Control: no-store` و `Pragma: no-cache` |
| JSON | کلیدها camelCase؛ enumها به‌صورت **عدد** serialize می‌شوند (مثلاً `employmentStatus: 3`)؛ `DateTime` با ISO 8601 و پسوند `Z` (Kind=UTC تضمین‌شده سمت سرور) |
| صفحه‌بندی | `{ items, totalCount, page, pageSize }`؛ `page` با clamp به `1..100000` و `pageSize` با clamp به `1..100` (پیش‌فرض ۲۵)؛ مقدار نامعتبر خطا نیست، clamp می‌شود |
| خطاها | ۴۰۱ ناشناس/حساب غیرکاربر؛ ۴۰۴ غیرعضو یا هدف ناموجود در شرکت (پاسخ یکسان، بدون افشای وجود)؛ ۴۰۳ عضو فعال بدون مجوز؛ ۴۰۹ تعارض وضعیت؛ ۴۰۰ اعتبارسنجی. بدنهٔ خطا: `{ "error": "<پیام فارسی>" }` (۴۰۴ خالی است) |
| Audit | هر عملیات نوشتن یک `SecurityAuditEvent` با `UserId`، `TenantId`، `EventType`، `EntityType`، `EntityId`، `OccurredAtUtc`، `CorrelationId` ثبت می‌کند؛ Payload/IP/DetailsJson هرگز به UI برنمی‌گردد |
| Tenant Scope | همهٔ Queryها با `TenantId == companyId` اثبات‌شده از `CompanyAccessService` (عضویت فعال + کد مجوز، از دیتابیس در هر درخواست — D-013)؛ شناسهٔ دستکاری‌شده همان ۴۰۴ هدف ناموجود را می‌دهد |
| RowVersion | فقط PUTها توکن `rowVersion` را (base64، از Detail GETها) در body می‌گیرند؛ نبود/بدشکلی توکن ۴۰۰ و توکن منقضی ۴۰۹ می‌دهد؛ POST/DELETE به توکن نیاز ندارند |

مقادیر enum (عددی — قرارداد D-072):
- `employmentStatus`: `1=Active`، `2=Suspended`، `3=Terminated`
- `status` (انتساب): `1=Active`، `2=Ended`

---

## ۱. ماتریس کامل API

وضعیت‌ها: `Verified` = کد + تست مستقیم + این سند هماهنگ‌اند.

### ۱.۱ کارکنان (`/org/employees`)

| عملیات | Method + Route | Permission | Request DTO | Response DTO | Pagination | RowVersion | Audit | وضعیت |
|---|---|---|---|---|---|---|---|---|
| فهرست کارکنان | `GET /org/employees` | `hr.employees.list` | Query: `search?` (≤۱۲۰)، `status?` (1/2/3)، `unitId?`، `page?`، `pageSize?` | `HrEmployeeListResponse` | ✔ | — | — | Verified |
| جزئیات کارمند | `GET /org/employees/{employeeId}` | `hr.employees.view` | — | `HrEmployeeDetail` | — | خروجی | — | Verified |
| ثبت کارمند | `POST /org/employees` | `hr.employees.create` | `HrCreateEmployeeRequest` | `201 HrEmployeeCreatedResponse` + `Location` | — | — | `HrEmployeeCreated` | Verified |
| ویرایش کارمند | `PUT /org/employees/{employeeId}` | `hr.employees.update` | `HrUpdateEmployeeRequest` | `204` | — | **الزامی** | `HrEmployeeUpdated` | Verified |
| خاتمه همکاری | `POST /org/employees/{employeeId}/deactivate` | `hr.employees.update` | بدون بدنه | `204` | — | — | `HrEmployeeTerminated` | Verified |
| اتصال به حساب | `PUT /org/employees/{employeeId}/user` | `hr.employees.user-link.manage` | `HrLinkEmployeeUserRequest` | `204` | — | — | `HrEmployeeUserLinked` | Verified |
| جداسازی حساب | `DELETE /org/employees/{employeeId}/user` | `hr.employees.user-link.manage` | بدون بدنه | `204` | — | — | `HrEmployeeUserUnlinked` | Verified |
| تغییر انتساب (انتقال) | `POST /org/employees/{employeeId}/assignment-change` | `hr.assignments.manage` | `HrChangeAssignmentRequest` | `200 HrAssignmentCreatedResponse` | — | — | `HrAssignmentStarted` (+پایان قبلی) | Verified |
| تاریخچه تغییرات | `GET /org/employees/{employeeId}/history` | `hr.employees.view` | Query: `page?`، `pageSize?` | `HrEmployeeHistoryResponse` | ✔ | — | — (خودش خروجی Audit است) | Verified |

### ۱.۲ واحدهای سازمانی (`/org/units`)

| عملیات | Method + Route | Permission | Request DTO | Response DTO | Pagination | RowVersion | Audit | وضعیت |
|---|---|---|---|---|---|---|---|---|
| فهرست تخت | `GET /org/units` (`view` حذف یا `flat`) | `hr.organizational-units.list` | Query: `view?`، `search?` (≤۱۲۰)، `page?`، `pageSize?` | `HrUnitListResponse` — فقط واحدهای فعال | ✔ | — | — | Verified |
| درخت کامل | `GET /org/units?view=tree` | `hr.organizational-units.list` | همان بالا (بدون صفحه‌بندی) | `HrUnitTreeResponse` — شامل واحدهای غیرفعال با فلگ | — | — | — | Verified |
| جزئیات واحد | `GET /org/units/{unitId}` | `hr.organizational-units.list` | — | `HrUnitDetail` | — | خروجی | — | Verified |
| ایجاد واحد | `POST /org/units` | `hr.organizational-units.create` | `HrCreateUnitRequest` | `201 HrUnitCreatedResponse` + `Location` | — | — | `HrUnitCreated` | Verified |
| ویرایش واحد | `PUT /org/units/{unitId}` | `hr.organizational-units.update` | `HrUpdateUnitRequest` | `204` | — | **الزامی** | `HrUnitUpdated` | Verified |
| غیرفعال‌سازی | `POST /org/units/{unitId}/deactivate` | `hr.organizational-units.update` | بدون بدنه | `204` | — | — | `HrUnitDeactivated` | Verified |
| زیردرخت | `GET /org/units/{unitId}/subtree` | `hr.organizational-units.list` | — | `HrUnitSubtreeNode` (بازگشتی) | — | — | — | Verified |
| کارکنان واحد | `GET /org/units/{unitId}/employees` | `hr.organizational-units.list` | Query: `page?`، `pageSize?` | `HrUnitEmployeeListResponse` | ✔ | — | — | Verified |

### ۱.۳ سمت‌ها (`/org/positions`)

| عملیات | Method + Route | Permission | Request DTO | Response DTO | Pagination | RowVersion | Audit | وضعیت |
|---|---|---|---|---|---|---|---|---|
| فهرست سمت‌ها | `GET /org/positions` | `hr.positions.list` | Query: `search?` (≤۱۲۰)، `page?`، `pageSize?` | `HrPositionListResponse` | ✔ | — | — | Verified |
| جزئیات سمت | `GET /org/positions/{positionId}` | `hr.positions.list` | — | `HrPositionDetail` | — | خروجی | — | Verified |
| ایجاد سمت | `POST /org/positions` | `hr.positions.create` | `HrCreatePositionRequest` | `201 HrPositionCreatedResponse` + `Location` | — | — | `HrPositionCreated` | Verified |
| ویرایش سمت | `PUT /org/positions/{positionId}` | `hr.positions.update` | `HrUpdatePositionRequest` | `204` | — | **الزامی** | `HrPositionUpdated` | Verified |
| غیرفعال‌سازی | `POST /org/positions/{positionId}/deactivate` | `hr.positions.update` | بدون بدنه | `204` | — | — | `HrPositionDeactivated` | Verified |
| دارندگان سمت | `GET /org/positions/{positionId}/employees` | `hr.positions.list` | Query: `page?`، `pageSize?` | `HrPositionEmployeeListResponse` | ✔ | — | — | Verified |

### ۱.۴ انتساب‌ها (`/org/assignments`)

| عملیات | Method + Route | Permission | Request DTO | Response DTO | Pagination | RowVersion | Audit | وضعیت |
|---|---|---|---|---|---|---|---|---|
| تاریخچه انتساب‌ها | `GET /org/assignments` | `hr.employees.view` | Query: `employeeId?` یا `unitId?` یا `positionId?` (**حداقل یکی اجباری**)، `page?`، `pageSize?` | `HrAssignmentListResponse` | ✔ | — | — | Verified |
| شروع انتساب | `POST /org/assignments` | `hr.assignments.manage` | `HrStartAssignmentRequest` | `201 HrAssignmentCreatedResponse` + `Location` | — | — | `HrAssignmentStarted` | Verified |
| پایان انتساب | `POST /org/assignments/{assignmentId}/end` | `hr.assignments.manage` | `HrEndAssignmentRequest` (بدنه اختیاری) | `204` | — | — | `HrAssignmentEnded` | Verified |

«دریافت انتساب فعال»: Endpoint جداگانه ندارد و نباید داشته باشد (پرهیز از تکرار)؛ از `GET /org/employees/{id}` (فیلد `activeAssignment`) یا `GET /org/assignments?employeeId=` (سطر `status: 1` و `endOnUtc: null`) به‌دست می‌آید.

---

## ۲. DTOهای واقعی (برابر کد)

### ۲.۱ کارکنان

```csharp
// POST /org/employees — همهٔ فیلدهای متن پرشده اجباری‌اند
HrCreateEmployeeRequest(string? EmployeeNumber, string? FirstName, string? LastName, DateTime? HiredOnUtc)
// JSON: { "employeeNumber": "EMP-101", "firstName": "علی", "lastName": "محمدی", "hiredOnUtc": "2026-01-10T08:30:00Z" }

// PUT /org/employees/{id}
HrUpdateEmployeeRequest(string? FirstName, string? LastName,
    CompanyEmployeeEmploymentStatus EmploymentStatus, DateTime? HiredOnUtc, string? RowVersion)
// - `employmentStatus: 3` (Terminated) با ۴۰۰ رد می‌شود؛ خاتمه فقط از POST deactivate.
// - `hiredOnUtc: null` یعنی «تغییر نکن» (patch)؛ پاک‌کردن تاریخ استخدام از این مسیر ممکن نیست.
// - وضعیت Terminated برگشت‌پذیر نیست (409).

// PUT /org/employees/{id}/user
HrLinkEmployeeUserRequest(Guid UserId)   // { "userId": "<guid>" } — resolve به عضویت فعال همان شرکت

// GET /org/employees — آیتم
HrEmployeeListItem(Guid Id, string EmployeeNumber, string FirstName, string LastName,
    CompanyEmployeeEmploymentStatus EmploymentStatus, DateTime? HiredOnUtc, DateTime? TerminatedOnUtc,
    bool HasLinkedUser, Guid? UnitId = null, string? UnitName = null,
    Guid? PositionId = null, string? PositionName = null)

// GET /org/employees/{id}
HrEmployeeDetail(Guid Id, string EmployeeNumber, string FirstName, string LastName,
    CompanyEmployeeEmploymentStatus EmploymentStatus, DateTime? HiredOnUtc, DateTime? TerminatedOnUtc,
    Guid? UserCompanyMembershipId, Guid? LinkedUserId, DateTime CreatedAtUtc,
    string RowVersion, HrAssignmentItem? ActiveAssignment)
```

### ۲.۲ واحدها

```csharp
// POST /org/units
HrCreateUnitRequest(string? Code, string? Name, string? Description, Guid? ParentUnitId,
    DateTime? ValidFromUtc, DateTime? ValidToUtc)

// PUT /org/units/{id}
HrUpdateUnitRequest(string? Name, string? Description, Guid? ParentUnitId, bool IsActive,
    DateTime? ValidFromUtc, DateTime? ValidToUtc, string? RowVersion)
// دقت: `validFromUtc: null` = «تغییر نکن» ولی `validToUtc: null` = «پاک شود» (معنای کامل/patch متفاوت)

// آیتم فهرست
HrUnitItem(Guid Id, string Code, string Name, string? Description, Guid? ParentUnitId,
    bool IsActive, DateTime? ValidFromUtc, DateTime? ValidToUtc)

// GET /org/units/{id}
HrUnitDetail(Guid Id, string Code, string Name, string? Description, Guid? ParentUnitId,
    bool IsActive, DateTime? ValidFromUtc, DateTime? ValidToUtc,
    int ActiveChildCount, int ActiveAssignmentCount, string RowVersion)

// درخت: HrUnitTreeNode همان فیلدهای HrUnitItem + IReadOnlyList<HrUnitTreeNode> Children
// زیردرخت: HrUnitSubtreeNode(Guid Id, string Code, string Name, bool IsActive, Guid? ParentUnitId,
//   int ActiveAssignmentCount, IReadOnlyList<HrUnitSubtreeNode> Children, DateTime? ValidFromUtc, DateTime? ValidToUtc)
```

### ۲.۳ سمت‌ها

```csharp
// POST /org/positions
HrCreatePositionRequest(string? Code, string? Name, string? Description, int? Rank)

// PUT /org/positions/{id}
HrUpdatePositionRequest(string? Name, string? Description, int? Rank, bool IsActive, string? RowVersion)

// آیتم فهرست
HrPositionItem(Guid Id, string Code, string Name, string? Description, int? Rank, bool IsActive)

// GET /org/positions/{id}
HrPositionDetail(Guid Id, string Code, string Name, string? Description, int? Rank, bool IsActive,
    int ActiveAssignmentCount, string RowVersion)
```

### ۲.۴ انتساب‌ها

```csharp
// POST /org/assignments
HrStartAssignmentRequest(Guid EmployeeId, Guid? OrganizationalUnitId, Guid? PositionId,
    Guid? ManagerEmployeeId, DateTime StartOnUtc, bool EndActive)

// POST /org/assignments/{id}/end  — بدنه اختیاری؛ null = همین حالا
HrEndAssignmentRequest(DateTime? EndOnUtc)

// POST /org/employees/{id}/assignment-change — فیلد null یعنی «مقدار قبلی حفظ شود»
HrChangeAssignmentRequest(Guid? OrganizationalUnitId, Guid? PositionId, Guid? ManagerEmployeeId)

// آیتم مشترک
HrAssignmentItem(Guid Id, Guid EmployeeId,
    Guid? OrganizationalUnitId, string? UnitCode, string? UnitName,
    Guid? PositionId, string? PositionCode, string? PositionName,
    Guid? ManagerEmployeeId, DateTime StartOnUtc, DateTime? EndOnUtc,
    CompanyEmployeeAssignmentStatus Status)

// آیتم‌های گردش‌کار فاز ۴
HrUnitEmployeeItem(Guid EmployeeId, string EmployeeNumber, string FirstName, string LastName,
    Guid? PositionId, string? PositionName, DateTime StartOnUtc)
HrPositionEmployeeItem(Guid EmployeeId, string EmployeeNumber, string FirstName, string LastName,
    Guid? OrganizationalUnitId, string? OrganizationalUnitName, DateTime StartOnUtc)

// تاریخچه کارمند — فقط Allowlist؛ هیچ Payload/IP/DetailsJson برنمی‌گردد
HrEmployeeHistoryItem(string EventType, string EntityType, string EntityId,
    DateTime OccurredAtUtc, Guid? ActorUserId, string? CorrelationId)
```

---

## ۳. نمونه‌های واقعی درخواست/پاسخ

(همهٔ شناسه‌ها نمونه‌نمونهٔ GUID-شکل‌اند؛ ساختار و نام فیلدها عین DTO بک‌اند است.)

### ۳.۱ ثبت کارمند → 201

```http
POST /api/v1/companies/0f3a1d2c-0000-4000-8000-000000000001/org/employees
X-XSRF-TOKEN: <توکن از GET /api/v1/antiforgery/token>
Content-Type: application/json

{ "employeeNumber": "EMP-101", "firstName": "علی", "lastName": "محمدی", "hiredOnUtc": "2026-01-10T08:30:00Z" }
```

```json
{ "id": "8c5b7e2a-1111-4000-8000-00000000abcd" }
```
- هدر `Location: /api/v1/companies/.../org/employees/8c5b...abcd`
- خطاها: `400` شماره/نام نامعتبر یا دورهٔ همکاری نامعتبر؛ `409 { "error": "شماره پرسنلی در این شرکت تکراری است." }`؛ الگوی مجاز شماره پرسنلی: `^[A-Za-z0-9][A-Za-z0-9._-]{0,29}$` (حداکثر ۳۰).

### ۳.۲ فهرست کارکنان → 200

```http
GET /api/v1/companies/0f3a.../org/employees?status=1&unitId=5d2e...&page=1&pageSize=25
```

```json
{
  "items": [
    {
      "id": "8c5b7e2a-1111-4000-8000-00000000abcd",
      "employeeNumber": "EMP-101",
      "firstName": "علی",
      "lastName": "محمدی",
      "employmentStatus": 1,
      "hiredOnUtc": "2026-01-10T08:30:00Z",
      "terminatedOnUtc": null,
      "hasLinkedUser": false,
      "unitId": "5d2e9f10-2222-4000-8000-00000000ef01",
      "unitName": "دفتر مرکزی",
      "positionId": "7a1c3b40-3333-4000-8000-00000000beef",
      "positionName": "حسابدار"
    }
  ],
  "totalCount": 1, "page": 1, "pageSize": 25
}
```
- `unitId/unitName/positionId/positionName` از انتساب **فعال** کارمند پر می‌شوند؛ بدون انتساب فعال `null` هستند. `hasLinkedUser` فقط بولن است (بدون افشای هویت کاربر).
- جستجو روی `firstName/lastName/employeeNumber` (Contains)؛ فیلتر وضعیت فقط `1|2|3`.

### ۳.۳ ویرایش واحد → 204 و 409

```http
PUT /api/v1/companies/0f3a.../org/units/5d2e9f10-...
X-XSRF-TOKEN: <token>
Content-Type: application/json

{ "name": "دفتر مرکزی", "description": null, "parentUnitId": null,
  "isActive": false, "validFromUtc": null, "validToUtc": null,
  "rowVersion": "AAAAAAAAB9I=" }
```
- `204 No Content` در موفقیت.
- `409` اگر واحد فرزند فعال یا انتساب فعال داشته باشد: `{ "error": "ابتدا واحدهای زیرمجموعه فعال را منتقل یا غیرفعال کنید." }` یا `{ "error": "ابتدا انتساب‌های فعال این واحد را پایان دهید." }` — همین گارد روی `POST /deactivate` هم برقرار است.
- `409` با پیام «این واحد هم‌زمان توسط کاربر دیگری تغییر کرده است…» برای توکن منقضی؛ `400` برای نبود/بدشکلی `rowVersion`.
- `409` انتقال واحد به زیرمجموعهٔ خودش: «انتقال واحد باعث چرخه در ساختار سازمانی می‌شود.»

### ۳.۴ شروع انتساب → 201 و 400

```http
POST /api/v1/companies/0f3a.../org/assignments
X-XSRF-TOKEN: <token>
Content-Type: application/json

{ "employeeId": "8c5b...", "organizationalUnitId": "5d2e...",
  "positionId": "7a1c...", "managerEmployeeId": null,
  "startOnUtc": "2026-11-05T07:00:00Z", "endActive": false }
```

```json
{ "id": "9d6a8f3b-4444-4000-8000-00000000cafe" }
```
- خطاهای تاریخ (400): «انتساب آینده‌نگر پشتیبانی نمی‌شود…» (`startOnUtc > اکنون`)؛ «شروع انتساب نمی‌تواند قبل از تاریخ شروع همکاری باشد.» (`startOnUtc < hiredOnUtc`).
- `409` با انتساب فعال موجود و `endActive: false`؛ `409` چرخه مدیریتی/مدیر غیرفعال/غیرهم‌شرکت؛ `409` واحد غیرفعال یا خارج از پنجرهٔ اعتبار.

### ۳.۵ انتقال کارمند → 200

```http
POST /api/v1/companies/0f3a.../org/employees/8c5b.../assignment-change
X-XSRF-TOKEN: <token>
Content-Type: application/json

{ "organizationalUnitId": "b1b2...", "positionId": null, "managerEmployeeId": null }
```

```json
{ "id": "9d6a8f3b-5555-4000-8000-00000000d001" }
```
- اتمی: پایان انتساب جاری (با `endOnUtc = اکنون`) + انتساب جدید در یک تراکنش؛ فیلد `null` یعنی حفظ مقدار قبلی؛ تاریخچه بازنویسی نمی‌شود.
- `409` اگر کارمند انتساب فعال نداشته باشد یا خاتمه‌همکاری باشد.

### ۳.۶ زیردرخت واحد → 200

```json
{
  "id": "5d2e9f10-...",
  "code": "HQ",
  "name": "دفتر مرکزی",
  "isActive": true,
  "parentUnitId": null,
  "activeAssignmentCount": 1,
  "children": [
    { "id": "b1b2c3d4-...", "code": "SALES", "name": "فروش", "isActive": true,
      "parentUnitId": "5d2e9f10-...", "activeAssignmentCount": 0, "children": [],
      "validFromUtc": null, "validToUtc": null }
  ],
  "validFromUtc": null, "validToUtc": null
}
```
- شمارش انتساب فعال هر گره با یک Query گروهی آمده است (N+1 ندارد)؛ ترتیب فرزندان قطعی (Code سپس Id)؛ پیمایش bounded با سقف عمق ۳۲ و cycle-safe.

### ۳.۷ تاریخچه کارمند → 200

```json
{
  "items": [
    { "eventType": "HrAssignmentStarted", "entityType": "CompanyEmployeeAssignment",
      "entityId": "9d6a8f3b-...", "occurredAtUtc": "2026-11-05T07:00:05.123Z",
      "actorUserId": "e4e5f6a7-...", "correlationId": "00-abc...-01" },
    { "eventType": "HrEmployeeCreated", "entityType": "CompanyEmployee",
      "entityId": "8c5b7e2a-...", "occurredAtUtc": "2026-11-05T06:59:58.100Z",
      "actorUserId": "e4e5f6a7-...", "correlationId": "00-abc...-01" }
  ],
  "totalCount": 2, "page": 1, "pageSize": 25
}
```
- منبع حقیقت: `SecurityAuditEvents` (Employee + Assignmentهای همان کارمند)؛ مرتب‌سازی `occurredAtUtc DESC` با تساوی‌شکن `entityId DESC` (صفحه‌بندی با timestampهای مساوی پایدار است)؛ شامل رویدادهای مجاز زیر است و هیچ چیز دیگر: `HrEmployeeCreated/Updated/Terminated/UserLinked/UserUnlinked`، `HrAssignmentStarted/Ended`.

---

## ۴. رفتارهای خطا و Validation خلاصه‌شده برای UI

| کد | چه زمانی | واکنش پیشنهادی UI |
|---|---|---|
| 401 | بدون نشست معتبر یا حساب غیرکاربر | هدایت به ورود |
| 404 | کاربر عضو فعال شرکت نیست، شرکت غیرفعال است، یا هدف در آن شرکت وجود ندارد | «یافت نشد» — تلاش برای حدس companyId/id اطلاعات جدید نمی‌دهد |
| 403 | عضو فعال بدون کد مجوز لازم | پیام `{error}`؛ منو باید از `/me/permissions` تغذیه شود، این ۴۰۳ فقط تور ایمنی است |
| 400 | اعتبارسنجی ورودی (الگوی شماره/کد، طول، تاریخ‌ها، RowVersion بدشکل) | نمایش `{error}` کنار فیلد |
| 409 | تکراری بودن کد/شماره، تعارض وضعیت (غیرفعال، چرخه، انتساب فعال)، RowVersion منقضی | برای RowVersion: بازخوانی Detail و تلاش دوباره؛ برای بقیه نمایش پیام |

خطاهای مربوط به توکن هم‌زمانی همه یک پیام دارند («…تغییر کرده است؛ اطلاعات را تازه‌سازی کنید») — UI لازم نیست نوع تعارض را تفکیک کند.

## ۵. وابستگی‌های مشترک Frontend

- `GET /api/v1/antiforgery/token` پیش از هر نوشتن؛ ارسال هدر `X-XSRF-TOKEN`.
- `GET /api/v1/me/permissions` فقط برای نمایش منو (کدهای `hr.*` در `permissionCodes`)؛ جایگزین Authorization سمت سرور نیست.
- انتخاب شرکت جاری از زمینهٔ کاری نشست (`WorkContext`)؛ `companyId` در همهٔ مسیرها همان شرکت زمینه است.
- همهٔ ورودی‌های تاریخ باید ISO 8601 UTC با `Z` باشند؛ خروجی‌ها همیشه `Z` دارند.

## ۶. قواعد Assignment که UI باید رعایت کند

1. هر کارمند حداکثر یک انتساب فعال؛ شروع انتساب جدید یا با `endActive: true` در POST /org/assignments یا از مسیر `assignment-change`.
2. شروع آینده‌نگر و شروع قبل از `hiredOnUtc` رد می‌شود (400) — فرم باید تاریخ شروع را «اکنون یا گذشته» بپذیرد.
3. انتقال = پایان جاری + شروع جدید اتمی؛ تاریخچه هرگز ویرایش نمی‌شود.
4. واحد/سمت/مدیر باید فعال و هم‌شرکت باشند؛ واحد باید در پنجرهٔ اعتبار باشد.
5. خاتمه همکاری (deactivate) انتساب فعال را در همان تراکنش پایان می‌دهد و برگشت‌پذیر نیست.
6. واحد/سمت دارای انتساب فعال از هر دو مسیر PUT و deactivate با ۴۰۹ مسدود می‌شود.

## ۷. محدودیت‌های شناخته‌شده (بدون تغییر رفتار)

- `PUT /org/units/{id}`: پاک‌کردن `validFromUtc` ممکن نیست (null = حفظ مقدار) ولی `validToUtc` با null پاک می‌شود؛ به‌روزرسانی باید این معنای دوگانه را در فرم لحاظ کند.
- `PUT /org/employees/{id}`: پاک‌کردن `hiredOnUtc` ممکن نیست؛ `employeeNumber` پس از ایجاد تغییرنکردنی است.
- فعال‌سازی دوبارهٔ یک واحد/سمت غیرفعال از PUT ممکن است (با `isActive: true`)، مشروط به رعایت گاردهای والد/انتساب؛ ولی راه انداختن «کارمند خاتمه‌همکاری» وجود ندارد (409).
- فعال‌شدن دوبارهٔ یک واحد زیر والدِ غیرفعال از مسیر PUT مسدود نمی‌شود؛ ترتیب فعال‌سازی (والد قبل از فرزند) مسئولیت گردش‌کار UI است. (رفع آن نیازمند تصمیم دامنهٔ جداگانه است.)
- جستجوی فهرست‌ها `Contains` است؛ خروجی فهرست تخت واحدها فقط واحدهای فعال است (درخت شامل غیرفعال‌ها هم می‌شود).
- تاریخچهٔ کارمند رویدادهای Audit را خلاصه می‌کند؛ برچسب‌های فارسی قابل‌نمایش در دسترس نیستند و UI باید از `eventType` بسازد.

## ۸. وضعیت نهایی Endpointها

هر ۲۵ Endpoint این قرارداد `Verified` است: کد، تست مستقیم (Endpoint/Unit)، تست E2E روی Published Server + SQL Server و این سند هم‌خوان‌اند. شمار تست‌های HR (برابر کد): `HrRulesTests` ۳۳، `HrOrganizationalStructureEndpointTests` ۳۱، `HrWorkflowsEndpointTests` ۷، `HrOrganizationalStructureSqlServerTests` ۵ (SQL-gated) و `tests/e2e/specs/hr-structure.spec.mjs` (سناریوی کامل Published Server).
