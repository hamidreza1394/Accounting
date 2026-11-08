- **مبنای کد:** `main` پس از Merge PR #589 (merge commit `65a04c2628a9`)
- **مجوز اجرایی:** مالک محصول، اجرای مرحله دوم و Merge نهایی PRهای همین مأموریت را پس از سبز بودن همهٔ کنترل‌ها مجاز کرده است.
- **این فایل append-only است؛** هر PR بخش خود را به انتهای آن اضافه می‌کند.

## 0. تکمیل مرحله اول — PR #589

- وضعیت پیش از شروع: Open با Base `feat/ui-workspace-navigation-polish`؛ همهٔ PRهای #582 تا #588 مرج شده بودند و شاخهٔ پایه داخل `main` بود.
- Patch و commit ancestry بررسی شد: شاخه head از نظر ancestry شامل نوک `main` بود؛ پس از تغییر Base به `main`، Diff دقیقاً همان تغییرات #589 بود (تست‌های integration workspace، محافظت از جریان تغییر زمینه، مستندات handoff مرحله اول).
- Base به `main` تغییر کرد؛ همهٔ ۱۰ چک CI روی SHA `655820c57fb923e5ca209eb0752a71275570e42b` سبز شد (build-and-test، e2e، client، publish، publish-windows، secret-scan، Semgrep).
- Merge با merge commit در `65a04c2628a9` انجام شد؛ CI روی `main` جدید نیز کاملاً سبز است.

## 1. ممیزی پیش از توسعه (کد `main` در `65a04c2628a9`)

| # | مورد | وضعیت | شاهد |
|---|---|---|---|
| 1 | API مدیریت نقش‌ها (دوحالته D-071) | Implemented | `Server/Api/CompanyRoleAdministrationEndpoints.cs`؛ `CompanyRoleAdministrationEndpointTests`؛ `CompanyRoleDelegationCeilingEndpointTests` |
| 2 | ایجاد/ویرایش/غیرفعال‌سازی نقش | Implemented | `CreateRoleAsync`/`UpdateRoleAsync`؛ 409 نقش سیستمی و عضو فعال؛ کپی نقش NotImplemented |
| 3 | نمایش PermissionTree | Implemented | `ClientApp/src/components/ui/PermissionTree.tsx` + `RoleDetailPage` |
| 4 | جستجو و فیلتر مجوزها | Implemented (جستجوی debounce) | `PermissionTree.tsx` (searchDebounceMs=150)؛ فیلتر مجزا بر اساس زیرسیستم = گروه‌بندی درختی موجود |
| 5 | انتخاب گروهی مجوزها | Implemented | bulk actions گروهی در `PermissionTree.tsx` |
| 6 | مجوزهای مؤثر کاربر | Implemented (API + UI اعضای نقش) | `GetEffectivePermissionsAsync`؛ `EffectivePermissionsEndpointTests`؛ dialog «دسترسی مؤثر» در `RoleMembersSection` |
| 7 | منبع هر مجوز | Partially Implemented | پاسخ فقط `RoleNames[]` + کدها دارد؛ منبع مجوزبه‌مجوز تفکیک نیست → PR مرحله دوم |
| 8 | فهرست اعضای نقش | Implemented؛ جستجو سمت سرور نبود | `GetRoleMembersAsync` (صفحه‌بندی)؛ شکاف جستجو در همین مرحله بسته شد |
| 9 | انتقال اعضا بین نقش‌ها | Implemented | `TransferMembersAsync`؛ اتمی؛ Audit هر عضو؛ سقف ۵۰۰ |
| 10 | پیش‌نمایش اثر تغییر Permission | NotImplemented | هیچ endpoint یا UI preview وجود ندارد → PR مرحله دوم |
| 11 | Audit تغییر Role/Permission | Implemented (موفق)؛ شکست سقف بدون Audit | رویدادهای `CompanyRoleCreated/Updated`، `UserCompanyRoleAssigned`؛ مسیرهای `ForbiddenBeyondCeiling` بدون رکورد Audit → PR مرحله دوم |
| 12 | جلوگیری از حذف/تنزل آخرین مدیر | Implemented | `UserAdministrationEndpoints.cs` گارد «آخرین مدیر سیستم» با تراکنش Serializable؛ مسیر شرکتی: 409 نقش دارای عضو فعال + بستن تغییر نقش خود |
| 13 | تست‌های مثبت/منفی Backend | Implemented | ۵۲ تست نقش/سقف/دسترسی مؤثر + `CompanyRoleMembersSqlServerTests` |
| 14 | جداسازی شرکت‌ها / IDOR | Implemented (تست‌شده) | تست‌های `RejectsRoleOfAnotherCompany...` و معادل‌های سقف |
| 15 | دسترس‌پذیری صفحه مدیریت نقش‌ها | NeedsVerification | axe/صفحه‌خوان اجرا نشده (سند ADMIN_UI.md §11)؛ تست‌های صفحه‌کلید موجود است؛ افزودن وابستگی axe نیازمند تأیید (D-048/D-049) |
| 16 | سازگاری کاتالوگ‌ها با Route/API | Implemented (منو↔روتر) | `menuRouteConsistency.test.tsx`؛ کاتالوگ‌های SUBSYSTEM/MENU_FORM/PERMISSION از `cb2ddd57`؛ تناظر کامل route↔کاتالوگ ماشینی نیست (مستند) |

### شکاف‌های مصوب مرحله دوم (ترتیب اجرا)

1. **PR-A:** جستجوی سمت سرور اعضای نقش (Backend + Client + UI + تست).
2. **PR-B:** منبع مجوزبه‌مجوز در دسترسی مؤثر کاربر.
3. **PR-C:** Audit تلاش ناموفق واگذاری فراتر از سقف.
4. **PR-D:** پیش‌نمایش اثر تغییر Permission نقش (بدون Mutation).
5. **PR-E:** کپی نقش و مقایسهٔ دو نقش (UI با API موجود).

خارج از دامنه (مرحله ۳ به بعد): تب‌های هفت‌گانه پرونده کاربر/HR، عکس پرسنلی، اتصال entitlement به Runtime، MFA/بازیابی رمز، آرشیو/Purge Audit، FiscalYear موازی، ABAC و سرویس Authorization مستقل.

## 2. PR-A — جستجوی سمت سرور اعضای نقش

- **شاخه:** `agent/444-stage2-role-members-search` (از `main` در `65a04c2628a9`)
- **تغییرات:**
  - `Server/Api/CompanyRoleAdministrationEndpoints.cs`: پارامتر `search` اختیاری در `GetRoleMembersAsync`؛ قرارداد همان دایرکتوری کاربران (trim، خالی = بدون فیلتر، >۱۲۰ نویسه → 400)؛ فیلتر روی `Username`/`FirstName`/`LastName` پیش از شمارش و صفحه‌بندی.
  - `tests/Template.Tests/CompanyRoleAdministrationEndpointTests.cs`: `GetRoleMembers_SearchFiltersServerSideByUserFields` — جستجو بر اساس نام کاربری و نام فارسی، عدم افشای عضو غیرفعال، رفتار خالی/طولانی؛ به‌روزرسانی فراخوانی‌ها در `CompanyRoleMembersSqlServerTests`.
  - `ClientApp/src/api/client.ts`: امضای `getCompanyRoleMembers` به query object (`CompanyRoleMemberQuery`) تغییر کرد؛ `search` فقط با مقدار غیرخالی ارسال می‌شود.
  - `ClientApp/src/components/admin/roles/RoleMembersSection.tsx`: فرم `role="search"` با debounce ۳۰۰ms، ریست صفحه به ۱، دکمه پاک‌کردن.
  - تست‌های UI و کلاینت به‌روزرسانی/افزوده شدند؛ `docs/api/USER_ACCESS_MANAGEMENT_API.md` §۳.۵، `docs/ADMIN_UI.md` §۱۰، `docs/USER_ACCESS_MANAGEMENT.md` §۱۴ به‌روزرسانی شد.
- **تست‌های اجراشده (محلی):** `dotnet build Template.sln` بدون خطا؛ ۵۲ تست نقش/سقف/دسترسی مؤثر سبز؛ `npm run typecheck` پاک؛ ۹۰۲ تست Client سبز؛ `npm run build` موفق. تست‌های SQL Server در این محیط اجرا نشدند (Docker در دسترس نیست) و در CI ارزیابی می‌شوند؛ دو تست integration وابسته به SQL Server روی main پایه به همین دلیل در محلی قرمز بودند (ServerUnreachable) و در CI سبز.
- **Migration:** هیچ. فقط قرارداد query.

## 3. PR-B — منبع مجوزبه‌مجوز در دسترسی مؤثر کاربر

- **شاخه:** `agent/444-stage2-effective-source` (stacked روی PR-A؛ هم‌فایل: `CompanyRoleAdministrationEndpoints.cs`، `RoleMembersSection.tsx`، `client.ts`)
- **تغییرات:**
  - `Server/Infrastructure/Tenancy/CompanyAccessService.cs`: متد جدید `GetGrantedPermissionsWithSourcesAsync(userId, companyId)` — هر کد مجوز + نام نقش(های) فعال اعطاکننده؛ از همان `QueryActiveMemberships` استفاده می‌کند تا اجتماع کدها با `GetGrantedPermissionCodesAsync` برابر بماند. رکورد `GrantedPermissionSources(Code, SourceRoleNames)`.
  - `GetEffectivePermissionsAsync`: پاسخ علاوه بر `permissionCodes` (حفظ سازگاری) فیلد `permissions: [{code, sourceRoleNames}]` را برمی‌گرداند؛ منبع نقش غیرفعال هرگز فهرست نمی‌شود.
  - `ClientApp/src/api/types.ts`: type + parser سخت‌گیرانهٔ `EffectivePermissionSource`.
  - `RoleMembersSection.tsx`: دیالوگ «دسترسی مؤثر» اکنون جدول مجوز/منبع دارد (ستون «منبع» با نام نقش‌ها)؛ caption sr-only.
  - تست: `EffectivePermissions_AttributesEveryCodeToItsGrantingActiveRoles` (مجوز مشترک بین دو نقش، نقش غیرفعال هرگز منبع نیست، برابری اجتماع کدها) + به‌روزرسانی تست‌های UI.
- **تست‌های اجراشده (محلی):** build بدون خطا؛ ۳/۳ تست EffectivePermissions سبز؛ typecheck پاک؛ vitest ۹۰۲/۹۰۲؛ build موفق.
- **Migration:** هیچ.

## 3.5 PR-C — Audit تلاش ناموفق واگذاری خارج از سقف (D-071)

- **شاخه:** `agent/444-stage2-access-audit` (stacked روی PR-B)
- **تغییرات:** هر چهار مسیر `ForbiddenBeyondCeiling` (ایجاد/ویرایش/تخصیص/انتقال گروهی) رویداد `CompanyRoleDelegationDenied` با Actor/Company/عملیات/جزئیات تلاش می‌نویسند؛ ذخیره درون همان تراکنش باز و Commit آن اتمی و fail-closed است (رویداد Audit تنها تغییر در انتظار در این مسیر است). گارد خود-تخصیص عمداً Audit سقف نمی‌گیرد.
- تست: چهار تست beyond-ceiling به‌روزرسانی شدند (دقیقاً یک رویداد رد، هیچ رویداد موفق)؛ اجرای واقعی روی build تازه: ۴۱ سبز، ۵ skip (SQL Server).
- Migration: هیچ. یادداشت: بخش مستنداتی این PR در commit خودش ثبت نشده بود و با یک commit مستنداتی مجزا در شاخهٔ PR-D تکمیل شد.

## 4. PR-D — پیش‌نمایش اثر تغییر مجوزهای نقش (سمت سرور)

- **شاخه:** `agent/444-stage2-role-preview` (stacked روی PR-C)
- **وضعیت پیش از این PR:** دیالوگ «پیش‌نمایش تغییرات گروه» در `RoleDetailPage` سمت کلاینت وجود داشت، اما تعداد اعضای متأثر، مجوزهای حساس و سقف تفویض را نشان نمی‌داد و محاسبهٔ دامنه سمت سرور نداشت.
- **تغییرات:**
  - `GET .../roles/{roleId}/permissions/preview` در `CompanyRoleAdministrationEndpoints` — همان مجوز ویرایش (`roles.update`، دوحالته D-071)؛ خروجی: افزوده/حذف‌شونده، `activeMemberCount`، `sensitiveChangedPermissionCodes` (قاعده server-owned: پسوند delete/approve/close) و `ceilingViolatedPermissionCodes` (گزارشی، نه بازدارنده؛ ذخیره سقف را درون تراکنش دوباره enforce می‌کند).
  - فقط-خواندنی: هیچ ذخیره‌ای، هیچ Audit، `no-store`، rate limit؛ نقش سیستمی 409؛ کد ناشناخته 400؛ نقش بیگانه/غیرعضو 404.
  - `RoleDetailPage`: هنگام باز شدن دیالوگ، پیش‌نمایش سرور گرفته می‌شود؛ نمایش اعضای متأثر، برجسته‌سازی مجوز حساس و هشدار سقف؛ خطای preview جریان ذخیره را نمی‌بندد (preview مشورتی است).
  - تست‌های جدید: `RolePermissionPreviewEndpointTests` (۴ تست) + تست UI دیالوگ.
- **تست محلی:** build بدون خطا؛ ۴/۴ تست preview سبز؛ typecheck پاک؛ vitest ۹۰۳/۹۰۳؛ build موفق.
- **Migration:** هیچ.

## 5. PR-E — کپی گروه دسترسی و مقایسهٔ دو گروه (UI، مستقل)

- **شاخه:** `agent/444-stage2-role-copy-compare` (از `main`؛ فایل‌های مشترک با PRهای دیگر را لمس نمی‌کند)
- **تغییرات:**
  - `RolesListPage`: ستون «کپی» برای هر گروه غیرسیستمی (لینک به `?copyFrom=<roleId>`)؛ بخش «مقایسهٔ دو گروه» با دو انتخاب‌گر و نمایش مجوزهای مشترک و فقط-در-هر-گروه (از همان دادهٔ سرور؛ بدون درخواست اضافی).
  - `RoleCreatePage`: با `copyFrom` فرم از گروه مبدأ پر می‌شود (کد `<CODE>-COPY`، عنوان «کپی …»، مجوزهای مبدأ محدود به کاتالوگ کاربر)؛ کدهای خارج از کاتالوگ/سقف کاربر لحاظ نمی‌شوند و تعدادشان صادقانه گزارش می‌شود؛ ذخیره همیشه از مسیر POST محافظت‌شده با سقف D-071 می‌گذرد.
  - هیچ API و Migration جدید؛ `menuRouteConsistency` بدون تغییر مسیر.
  - تست: ۲ تست کپی/مقایسهٔ فهرست + ۲ تست کپی فرم ایجاد.
- **تست محلی:** typecheck پاک؛ vitest ۹۰۴/۹۰۴؛ build و check:offline و check_static_assets سبز.
