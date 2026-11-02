# تحویل وظیفه — ISSUE-444-user-access-management

- Issue: #444
- Agent: ChatGPT
- تاریخ: 2026-11-02
- وضعیت: S0 تکمیل؛ PR #445 و #447 ادغام شده‌اند؛ PR #449 آماده بازبینی؛ PR #450 API فهرست/جزئیات و PR #451 رابط آن در حال اجرا هستند.

## هدف و معیارهای پذیرش
- [x] API کاربر عادی فقط اطلاعات خودش را برگرداند و رمز خودش را تغییر دهد (PR #447)؛ رابط در PR #449.
- [ ] مدیر مجاز مدیریت کاربران را در محدوده مجاز انجام دهد.
- [ ] سمت‌ها، گروه‌های دسترسی و مجوزهای جزئی با مدل موجود هماهنگ شوند.
- [x] منوی مدیریت سیستم به شش مسئولیت ضروری بازآرایی شد (PR #445).
- [ ] authorization در API و tenant/company scope enforce شود.
- [ ] Login History و Audit Trail تفکیک شوند.

## خلاصه
- Issue #444 ایجاد و شاخه اختصاصی ساخته شد.
- بررسی اولیه Graphify و GitHub روی مدل‌ها، API و AppShell انجام شد.
- سند جامع طراحی و معیارهای پذیرش تهیه شد.
- منوی مدیریت سیستم در AppShell به شش بخش هدف بازآرایی شد؛ «لیست کاربران فعال» حذف و ورود/خروج در گزارش امنیت ادغام شد. آیتم‌ها عمداً تا ایجاد route و API واقعی غیرفعال‌اند.
- تست AppShell برای ساختار جدید به‌روزرسانی شد.
- هنوز Migration، Entity یا API اجرایی تغییر نکرده است.
- PR #445 به‌صورت Draft باز شد. Client CI، .NET Build/Test و Semgrep در runهای PR در حال/در انتظار تکمیل‌اند؛ نتیجه نهایی هنوز دریافت نشده است.

## فایل‌های مهم
- docs/USER_ACCESS_MANAGEMENT.md — طراحی هدف و مراحل.
- docs/handoffs/ISSUE-444-user-access-management.md — این گزارش.
- docs/INDEX.md، docs/WORK_LOG.md و docs/ROADMAP.md — ثبت کار و ارجاع.

## تصمیم فنی
- بازاستفاده از PermissionDefinitions، CompanyRoles و CompanyRolePermissions برای مجوزهای شرکتی.
- Role → Permission → ApplicationHandler قدیمی در runtime به‌عنوان authorization enforce نمی‌شود؛ فعلاً نباید مبنای امنیت API باشد.
- مدیریت سراسری نصب و مدیریت شرکتی دو scope متفاوت‌اند؛ مرز مدیر سراسری/مدیر شرکت پیش از APIهای مدیریت کاربران باید طراحی و تست شود.
- پروفایل/تغییر رمز self-service از APIهای /me استفاده می‌کند؛ UI در PR #449.
- عدم ساخت جدول موازی برای مجوزهای عملیاتی شرکتی؛ CompanyRole/CompanyRolePermissions مرجع فعال‌اند.
- حفظ مرز Company/Tenant و FiscalPeriod مشترک.
- بخشی از customer-side license import/signature/revocation موجود است؛ Trade entitlement هنوز DenyAll است و خارج از S1 مدیریت کاربران باقی می‌ماند.
- D-064 مرز مدیر سراسری نصب و مدیر شرکتی را به‌صورت Accepted تثبیت کرده است.

## آزمون
| آزمون | نتیجه |
|---|---|
| Graphify semantic inspection | اجرا شد |
| GitHub source inspection | اجرا شد |
| Client CI (run 41500624984) | Typecheck، Vitest، Build، offline check و runtime dependency audit موفق |
| .NET CI (run 41500624984) | Restore، Build، Test، migration guards و model snapshot check موفق؛ E2E/Publish هنگام آخرین مشاهده هنوز در حال اجرا بود |
| Semgrep (run 41500625011) | موفق |
| git diff --check | از checkout محلی در دسترس نبود؛ CI migration/static checks موفق بودند |

## ریسک‌ها و کارهای باقی‌مانده
- مدل legacy Users.RoleId/Role و مدل شرکتی UserCompanyMemberships/CompanyRole مستقل‌اند و نباید بدون تصمیم معماری ادغام شوند.
- User هم‌اکنون داده‌های هویتی و تماس را نگه می‌دارد؛ ساخت UserProfile جدا هنوز توجیه نشده است.
- App.tsx routing مستقل ندارد؛ menu items فعلاً placeholder هستند.
- بررسی کامل همه call-siteهای legacy permission و inventory کاتالوگ زیرسیستم‌ها.
- ممیزی کامل API/DI و migration snapshot؛ مسیرهای اصلی بررسی شده‌اند اما call-siteهای کامل باید تکمیل شوند.
- تصمیم route strategy و API contracts.
- بررسی exact permission inventory و Trade permission catalog.
- طراحی و اجرای مرحله‌ای با TDD.

## Git
- PR #445 (مستندات/منو): ادغام‌شده.
- PR #447 (API self-service): ادغام‌شده.
- PR #449 (UI self-service): آماده بازبینی.
- PR #450 (API فهرست/جزئیات کاربران): باز، Draft.
- PR #451 (UI فهرست/جزئیات کاربران): شاخه وابسته به #450، در حال توسعه.
