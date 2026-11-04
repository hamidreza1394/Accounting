# تحویل وظیفه — ISSUE-491-organizational-structure-workflows

- **Issue:** #491 (گردش‌کارهای عملیاتی S2 — ادامه پیاده‌سازی ساختار سازمانی)
- **Agent/مسئول:** Z.ai (GLM) — از طرف مالک مخزن
- **تاریخ:** 2026-11-05
- **وضعیت:** انجام‌شده (Backend کامل؛ E2E محلی Blocked و به CI موکول؛ UI در ClientApp همچنان کاری جداگانه است)

## هدف و معیارهای پذیرش
- [x] **گردش‌کار ساختار سازمانی:** درخت زیرمجموعه (`GET .../org/units/{id}/subtree`) با شمار انتساب فعال هر گره؛ پیمایش BFS با عمق bounded و visited-set (داده فاسد هم حلقه نمی‌سازد)؛ جابه‌جایی/تغییر والد و گاردهای غیرفعال‌سازی از فاز ۳ پابرجاست.
- [x] **گردش‌کار Employee:** تاریخچه تغییرات (`GET .../org/employees/{id}/history`) از Audit Trail با خروجی Allowlist (EventType/EntityType/EntityId/OccurredAtUtc/ActorUserId/CorrelationId — بدون payload و بدون داده حساس)؛ اتصال اختیاری به عضویت هم‌شرکت از فاز ۳.
- [x] **گردش‌کار Position:** `GET .../org/positions/{id}/employees` — دارندگان فعال سمت در همه واحدها (استفاده مجدد Position مطابق D-072).
- [x] **گردش‌کار Assignment:** `GET .../org/units/{id}/employees` — اعضای فعال واحد؛ **کنترل Effective Dating:** شروع آینده‌نگر رد می‌شود (400) و شروع قبل از `HiredOnUtc` رد می‌شود (400) — قاعده جدید `HrRules.ValidateAssignmentStart` (fail-closed).
- [x] **دسترسی و امنیت:** همه endpointهای جدید دوحالته D-071، Rate Limit حساس، 401/403/404 استاندارد، بدون تغییر در RBAC؛ `hr.*` همچنان فقط از `CompanyRolePermissions` تحت سقف تفویض تخصیص می‌یابد.
- [x] تست‌های Unit/Endpoint (۷ تست جدید) و تکمیل E2E (سناریوی دوم گردش‌کار) — نتیجه واقعی ثبت شد.
- [ ] CI نهایی روی آخرین SHA — با Push انجام می‌شود و در PR ثبت می‌گردد.

## خلاصه تغییرات
1. چهار Endpoint خواندنی جدید در `HrOrganizationalStructureEndpoints` (بخش «operational workflows»): subtree واحدها، اعضای واحد، دارندگان سمت، تاریخچه تغییرات کارمند — همه صفحه‌بندی‌شده/محدود و Tenant-scoped.
2. `HrRules.ValidateAssignmentStart` + اعمال آن در `StartAssignmentCoreAsync`: کنترل آینده‌نگری و تاریخ قبل از استخدام (هر دو 400).
3. DTOهای Allowlist جدید: `HrUnitSubtreeNode`، `HrUnitEmployee(List)Item/Response`، `HrPositionEmployee(List)Item/Response`، `HrEmployeeHistoryItem/Response`.
4. E2E: سناریوی دوم در `tests/e2e/specs/hr-structure.spec.mjs` — درخت چندسطحی + subtree + اتصال Employee به عضویت + رد شروع آینده‌نگر + انتقال اتمی + تاریخچه + شمارنده‌های subtree پس از انتقال.
5. مستندات: `ORGANIZATIONAL_STRUCTURE_DATA_MODEL.md` (بند گردش‌کارهای فاز ۴)، `FRONTEND_API_CONTRACT_NEEDS.md` (نیازهای UI)، `WORK_LOG.md`.

## فایل‌های مهم تغییرکرده
- `Server/Api/HrOrganizationalStructureEndpoints.cs` — ۴ endpoint جدید، قواعد تاریخ، DTOهای جدید.
- `Domain/Hr/HrRules.cs` — `HrRuleViolation.AssignmentStartInFuture/BeforeHire` و `ValidateAssignmentStart`.
- `tests/Template.Tests/HrWorkflowsEndpointTests.cs` (جدید، ۷ تست)، `tests/e2e/specs/hr-structure.spec.mjs` (سناریوی دوم).
- `docs/ORGANIZATIONAL_STRUCTURE_DATA_MODEL.md`، `docs/FRONTEND_API_CONTRACT_NEEDS.md`، `docs/WORK_LOG.md`.

## تصمیم‌های فنی
- **Effective Dating:** چون مدل پذیرفته‌شده حداکثر یک انتساب فعال per Employee دارد (Unique فیلترشده)، شروع آینده‌نگر به‌عنوان انتساب فعال قابل ثبت نیست؛ به‌جای پذیرش خاموش، صریحاً 400 می‌شود تا کلاینت در زمان اجرا ثبت کند. شروع قبل از `HiredOnUtc` نیز ناسازگار تاریخچه اشتغال است و رد می‌شود.
- **تاریخچه تغییرات:** منبع، همان `SecurityAuditEvents` است — هیچ منبع حقیقت دومی ساخته نشد؛ رویدادهای Assignment از طریق شناسه انتساب‌های همان Employee واکشی و ادغام می‌شوند.
- **Subtree:** پیمایش BFS با `visited` + عمق ≤ `HrRules.MaxChainDepth`؛ شمارنده‌ها با یک کوئری GroupBy برای همه گره‌های زیردرخت (بدون N+1).
- UI در ClientApp هنوز ساخته نشده (خارج از ظرفیت این PR)؛ نیازهای UI در `FRONTEND_API_CONTRACT_NEEDS.md` کامل شده است.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `dotnet build Template.sln -c Release` | موفق | 0 Error |
| `dotnet test tests/Template.Tests -c Release` | 943 کل: 879 موفق، 62 Skip (SQL/Windows-gated)، 2 ناموفقِ محیطیِ از قبل موجود در main | ۷ تست جدید فاز ۴ سبز |
| `node --check tests/e2e/specs/hr-structure.spec.mjs` | موفق | سینتکس معتبر |
| E2E (Published Server + SQL Server) | **Blocked محلی** | اجرای واقعی با CI این PR |
| `git diff --check` | موفق | بدون خطا |

## امنیت، داده و سازگاری
- بدون Migration و بدون تغییر Schema؛ فقط endpointهای خواندنی جدید + یک قاعده اعتبارسنجی نوشتن.
- Tenant Isolation: همه کوئری‌ها با `TenantId == companyId` اثبات‌شده؛ تست 404 واحد شرکت دیگر.
- تاریخچه فقط رویدادهای همان Employee همان شرکت را می‌دهد؛ غیرعضو 404 و عضو بی‌مجوز 403.

## ریسک‌ها و کارهای باقی‌مانده
- UI مدیریت ساختار سازمانی در ClientApp (نیازها در `FRONTEND_API_CONTRACT_NEEDS.md` بند ۵ ثبت شد).
- آزمون رقابتی SQL Server برای هم‌زمانی انتساب‌ها به CI موکول است.

## شناسه‌های Git
- Branch: `feature/hr-organizational-structure-workflows`
- Pull Request: `feat(hr): organizational-structure operational workflows` به `feature/hr-organizational-structure-implementation` — **وابسته به PR پیاده‌سازی (Stacked)؛ پس از Merge آن، Base به main منتقل می‌شود.**
