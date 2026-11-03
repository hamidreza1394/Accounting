# تحویل وظیفه — ISSUE-444-user-access-management

- Issue: #444
- Agent: ChatGPT
- تاریخ: 2026-11-02
- وضعیت: در حال انجام؛ مستندات و بازآرایی منو در PR #445 باز است؛ S0 ممیزی مدل/امنیت تکمیل‌تر شده، پیاده‌سازی API هنوز آغاز نشده است.

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
- PR #445 به‌صورت Draft باز شد. Client CI، .NET Build/Test و Semgrep در runهای PR در حال/در انتظار تکمیل‌اند؛ نتیجه نهایی هنوز دریافت نشده است.

## فایل‌های مهم
- docs/USER_ACCESS_MANAGEMENT.md — طراحی هدف و مراحل.
- docs/handoffs/ISSUE-444-user-access-management.md — این گزارش.
- docs/INDEX.md، docs/WORK_LOG.md و docs/ROADMAP.md — ثبت کار و ارجاع.

## تصمیم فنی
- بازاستفاده از PermissionDefinitions، CompanyRoles و CompanyRolePermissions برای مجوزهای شرکتی.
- Role → Permission → ApplicationHandler قدیمی در runtime به‌عنوان authorization enforce نمی‌شود؛ فعلاً نباید مبنای امنیت API باشد.
- مدیریت سراسری نصب و مدیریت شرکتی دو scope متفاوت‌اند؛ مرز مدیر سراسری/مدیر شرکت پیش از APIهای مدیریت کاربران باید طراحی و تست شود.
- پروفایل و تغییر رمز Razor فعلی placeholder هستند (OnGet خالی)؛ API self-service وجود ندارد.
- عدم ساخت جدول موازی برای مجوزهای عملیاتی شرکتی؛ CompanyRole/CompanyRolePermissions مرجع فعال‌اند.
- حفظ مرز Company/Tenant و FiscalPeriod مشترک.
- بخشی از customer-side license import/signature/revocation موجود است؛ Trade entitlement هنوز DenyAll است و خارج از S1 مدیریت کاربران باقی می‌ماند.
- D-064 تفکیک مدیر سراسری نصب از مدیر شرکتی را به‌صورت Proposed ثبت کرده است.

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

## S4 — کاتالوگ مجوزها
- صفحه کاتالوگ مجوز در زیرمنوی مدیریت سیستم اضافه شد.
- صفحه فقط PermissionDefinitions ثبت‌شده را نشان می‌دهد و جستجو/فیلتر دارد؛ ساخت کد مجوز از UI مجاز نیست.

## S3 — گروه‌های دسترسی
- CompanyRole و CompanyRolePermissions موجود بازاستفاده می‌شوند؛ جدول موازی AccessGroup ایجاد نمی‌شود.
- کاتالوگ مجوز فعلی فقط شامل مجوزهای ثبت‌شده بازرگانی خارجی است؛ templateهای حسابداری/خزانه‌داری/انبارداری تا inventory و enforcement واقعی ایجاد نمی‌شوند.
- APIهای فهرست گروه، کاتالوگ مجوز، الگوهای Trade، ایجاد/ویرایش گروه و تخصیص membership در PR #454 قرار دارند.

## Git
- Branch: feature/admin-identity-access-management-444
- آخرین Commit شاخه در زمان این handoff: 1743242ccc9bc3de43942ffd26d7a783e837cc2c
- PR: #445 (Draft) — https://github.com/hamidreza1394/Accounting/pull/445
