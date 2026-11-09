# تحویل وظیفه — ISSUE-UNNUMBERED-users-form-navigation-and-validation

- **Issue:** `UNNUMBERED` (مأموریت مالک: اصلاح `/app/admin/users/new` و `/app/admin/users`)
- **Agent/مسئول:** Claude Code (Lead، پیاده‌سازی مستقیم)
- **تاریخ:** 2026-11-09
- **وضعیت:** انجام‌شده (پیاده‌سازی؛ `Verified` فقط پس از CI همان PR)
- **شاخه:** `fix/users-form-navigation-and-validation`

## هدف و معیارهای پذیرش
- [x] گروه‌های Sidebar (از جمله «مدیریت سیستم») به‌طور پیش‌فرض بسته‌اند و فقط با کلیک باز می‌شوند؛ Route فعال و گروه بازشده مستقل‌اند.
- [x] قاب Sidebar یک حاشیهٔ سرمه‌ای نازک و پیوسته است؛ بدون اسکرول افقی.
- [x] عکس پرسنلی در تب «اطلاعات اصلی» با ذخیرهٔ واقعی (نه نمایشی).
- [x] حداقل رمز ۸ نویسه در کلاینت و سرور؛ مرز ۷/۸/خالی تست شده؛ ویرایش رمز نشان نمی‌دهد.
- [x] ایمیل اختیاری (`NULL`)، با قالب معتبر در صورت وجود؛ Migration مستقل + تست SQL Server.
- [x] پس از ایجاد: ارسال یکتا، پیام موفقیت، رفتن به `/app/admin/users`، تازه‌سازی فهرست، جدیدترین کاربر اول، بازنشانی صفحه و جستجو؛ انصراف/بازگشت دقیقاً به فهرست با محافظ تغییرات؛ بدون تب تکراری.

## خلاصه تغییرات
- **Sidebar:** `AppSidebar`، `NestedNavSections`، هوک `useHeaderHeightVar`، `AppShell` و CSS؛ بدون بازنویسی Sidebar/Workspace.
- **رمز/ایمیل (Backend):** `Constants/MinLength` = 8؛ `User.EmailAddress` تهی‌پذیر با `ChangeEmailAddress(string?)`؛ `UserAdministrationEndpoints` و سه صفحهٔ Razor قدیمی null-safe؛ Migration `MakeUserEmailAddressOptional`.
- **عکس (Backend):** جدول `CompanyEmployeePhotos` (Migration `AddCompanyEmployeePhotos`)، `HrEmployeePhotoEndpoints` (GET/PUT/DELETE)، نوع از امضای فایل، حداکثر ۲ مگابایت، نام فایل ذخیره نمی‌شود، `CompanyAccessService` + `hr.employees.view/update`.
- **فهرست:** پیش‌فرض مرتب‌سازی `created` نزولی (با tiebreak پایدار) در `GetUsersAsync`.
- **Frontend:** `UserPhotoField`، `passwordPolicy`، `employeePhoto`، `useLeaveToList`، `userListRefresh`، `UserListFlashBanner`، تغییر `UserCreatePage`/`UserEditPage`/`UsersListPage`/`WorkspaceTabs` (`useOptionalWorkspaceTabs`)/`client.ts`/`hrClient.ts`.
- **اسکریپت‌های مدل Migration:** `scripts/ef_model.py`، `generate_migrations.py`، `verify_migration_model.py`.
- **مستندات:** ADR D-078، `SECURITY.md`، `HR_FRONTEND_API_CONTRACT.md`، `AGENTS.md` (شمارهٔ ADR بعدی D-079).

## تصمیم‌های فنی
- D-078 (جدید): رمز ۸ (جایگزین عدد D-073)، ایمیل اختیاری، ذخیرهٔ عکس در جدول جدا. CSP بدون تغییر؛ پیش‌نمایش با data URL.
- در حالت ایجاد، عکس فقط همراه ساخت پروندهٔ پرسنلی (نیازمند شماره پرسنلی و `hr.employees.create`) ذخیره می‌شود؛ شکست عکس پس از ایجاد حساب، هشدار روی فهرست است نه شکست ایجاد. در ویرایش، عکس فقط برای کاربر دارای پروندهٔ پرسنلی متصل و `hr.employees.view/update` در دسترس است؛ در غیر این صورت یادداشت صادقانه نمایش داده می‌شود.
- رمز اصلی `DatabaseSetup` (۱۵) عمداً تغییر نکرد.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `dotnet restore/build Template.sln -c Release` | موفق | ۰ خطا |
| `dotnet test Template.sln -c Release` (با SQL Server 2022 واقعی در Docker، `FARANOU_REQUIRE_SQLSERVER=1`) | موفق | ۱۱۸۶ کل، ۱۱۷۶ موفق، ۰ ناموفق، ۱۰ رد‌شده (ویژهٔ Windows/chmod) |
| تست‌های SQL Server جدید `UserEmailAndEmployeePhotoSqlServerTests` | موفق | ۲/۲: چند کاربر بدون ایمیل، یکتایی ایمیل واقعی، یک عکس برای هر کارمند، رد عبور از شرکت دیگر |
| `npx vitest run src/components/admin/users` | موفق | ۱۱ فایل، ۱۸۲ تست |
| `npm test` (کل Vitest) | ۱۰۵۳ موفق، ۳ ناموفق | سه شکست از قبل موجود و نامرتبط: خروجی CSV در `securityAuditClient.test.ts` و `SecurityAuditPage.test.tsx` (×۲)، خطای `object.stream is not a function` در محیط تست؛ با `git stash` روی کد پایه هم تکرار شد |
| `npm run typecheck`، `npm run build`، `npm run check:offline` | موفق | |
| `python3 scripts/check_static_assets.py` | موفق | |
| ESLint | اجرا نشد | مخزن پیکربندی ESLint (flat config) ندارد |
| E2E (Playwright) | اجرا نشد | در این جلسه اجرا نشد؛ CI تعیین‌کننده است |

## امنیت، داده و سازگاری
- دو Migration مستقل و غیرمخرب (Up). **Down ایمیل:** ستون را غیرتهی می‌کند و `NULL` را با `""` پر می‌کند؛ با چند کاربر بدون ایمیل ممکن است به تکراری‌شدن بخورد — پیش از Down داده بازبینی شود.
- عکس: فقط از طریق API با دسترسی شرکت‌محور؛ پاسخ `private, no-store` + `nosniff`؛ هیچ مسیر عمومی/wwwroot؛ نام فایل کاربر ذخیره یا استفاده نمی‌شود.
- هیچ رمز/کلید/Connection String واقعی در Git یا گزارش نیست.

## ریسک‌ها و کارهای باقی‌مانده
- `PROJECT_STATUS.md` (محتوای Vendor-محور) عمداً تغییر نکرد؛ فقط بر وضعیت تأییدشدهٔ CI به‌روز می‌شود.
- E2E و بازبینی بصری در مرورگر در این جلسه اجرا نشد (مرورگر در دسترس نبود).
- نمایش عکس در ویرایش به پروندهٔ پرسنلی متصل وابسته است؛ برای کاربران بدون پروندهٔ پرسنلی عکس قابل ذخیره نیست (یادداشت در UI).

## شناسه‌های Git
- Branch: `fix/users-form-navigation-and-validation`
- Commit: 7e27097، d93a067، d4a6c80، 898cf8a، 638ce09 و کامیت‌های مستندات/تست بعدی
- Pull Request: در گزارش نهایی
