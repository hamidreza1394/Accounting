# تحویل وظیفه — ISSUE-444-user-access-management

- Issue: #444
- Agent: ChatGPT
- تاریخ: 2026-11-02
- وضعیت: در حال انجام؛ ممیزی اولیه و مستند طراحی آغاز شده، پیاده‌سازی رفتاری هنوز آغاز نشده است.

## هدف و معیارهای پذیرش
- [ ] کاربر عادی فقط اطلاعات خودش را ببیند و رمز خودش را تغییر دهد.
- [ ] مدیر مجاز مدیریت کاربران را در محدوده مجاز انجام دهد.
- [ ] سمت‌ها، گروه‌های دسترسی و مجوزهای جزئی با مدل موجود هماهنگ شوند.
- [ ] منوی مدیریت سیستم فقط قابلیت‌های ضروری را نشان دهد.
- [ ] authorization در API و tenant/company scope enforce شود.
- [ ] Login History و Audit Trail تفکیک شوند.

## خلاصه
- Issue #444 ایجاد و شاخه اختصاصی ساخته شد.
- بررسی اولیه Graphify و GitHub روی مدل‌ها، API و AppShell انجام شد.
- سند جامع طراحی و معیارهای پذیرش تهیه شد.
- منوی مدیریت سیستم در AppShell به شش بخش هدف بازآرایی شد؛ «لیست کاربران فعال» حذف و ورود/خروج در گزارش امنیت ادغام شد. آیتم‌ها عمداً تا ایجاد route و API واقعی غیرفعال‌اند.
- تست AppShell برای ساختار جدید به‌روزرسانی شد.
- هنوز Migration، Entity یا API اجرایی تغییر نکرده است.

## فایل‌های مهم
- docs/USER_ACCESS_MANAGEMENT.md — طراحی هدف و مراحل.
- docs/handoffs/ISSUE-444-user-access-management.md — این گزارش.
- docs/INDEX.md، docs/WORK_LOG.md و docs/ROADMAP.md — ثبت کار و ارجاع.

## تصمیم فنی
- بازاستفاده از PermissionDefinitions، CompanyRoles و CompanyRolePermissions.
- عدم ساخت جدول موازی پیش از S0.
- حفظ مرز Company/Tenant و FiscalPeriod مشترک.
- عدم پیاده‌سازی entitlement لایسنس در این Issue.

## آزمون
| آزمون | نتیجه |
|---|---|
| Graphify semantic inspection | اجرا شد |
| GitHub source inspection | اجرا شد |
| Build/Test | اجرا نشده؛ تغییرات منو و تست نیازمند اجرای کلاینت‌اند |
| git diff --check | اجرا نشده؛ تغییرات از GitHub Contents API ثبت می‌شوند |

## ریسک‌ها و کارهای باقی‌مانده
- مدل legacy Users.RoleId/Role و مدل شرکتی UserCompanyMemberships/CompanyRole مستقل‌اند و نباید بدون تصمیم معماری ادغام شوند.
- User هم‌اکنون داده‌های هویتی و تماس را نگه می‌دارد؛ ساخت UserProfile جدا هنوز توجیه نشده است.
- App.tsx routing مستقل ندارد؛ menu items فعلاً placeholder هستند.
- ممیزی کامل endpointها، DI، mappingها، migrationها و React app flow.
- تصمیم route strategy و API contracts.
- بررسی exact permission inventory و Trade permission catalog.
- طراحی و اجرای مرحله‌ای با TDD.

## Git
- Branch: feature/admin-identity-access-management-444
- Commit: ثبت نشده
- PR: ایجاد نشده
