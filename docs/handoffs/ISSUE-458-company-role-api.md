# تحویل وظیفه — ISSUE-458-company-role-api

- **Issue:** [#458](https://github.com/hamidreza1394/Accounting/issues/458)
- **Agent/مسئول:** ChatGPT
- **تاریخ:** 2026-11-03
- **وضعیت:** پیاده‌سازی اولیه؛ CI و تست محلی هنوز اجرا/تأیید نشده‌اند.

## هدف و معیارهای پذیرش
- [x] انتقال API مدیریت CompanyRole و Permission Catalog از شاخه‌های stacked قبلی به شاخه مستقل مبتنی بر main.
- [x] استفاده مجدد از CompanyRole، CompanyRolePermission و PermissionDefinition؛ بدون مدل یا جدول موازی.
- [x] محدودکردن عملیات به Installation Administrator فعال که در هر درخواست از دیتابیس بررسی می‌شود.
- [x] جلوگیری از ایجاد یا فعال‌سازی مجدد عضویت از endpoint تخصیص نقش؛ عضویت جدید تا پیاده‌سازی approval مقصد fail-closed است.
- [x] ثبت Audit برای ایجاد/ویرایش نقش و تخصیص نقش.
- [x] افزودن تست منفی دسترسی کاربر احراز‌شده فاقد نقش مدیر نصب.
- [ ] اجرای CI روی PR و بررسی تمام checkها.
- [ ] اجرای تست SQL Server در CI.

## خلاصه تغییرات
- APIهای فهرست/ایجاد/ویرایش CompanyRole، کاتالوگ مجوزهای ثبت‌شده، templateهای مشتق‌شده از کاتالوگ و تغییر نقش عضویت فعال به main اضافه شدند.
- مسیرهای API در ApiEndpoints ثبت شدند.
- helper احراز مدیر نصب برای استفاده داخلی endpointهای مرتبط قابل دسترسی شد.
- تست‌های واحد موجود در شاخه stacked منتقل و تست منع دسترسی non-admin اضافه شد.

## فایل‌های مهم تغییرکرده
- `Server/Api/CompanyRoleAdministrationEndpoints.cs` — endpointهای Backend مدیریت نقش و کاتالوگ.
- `Server/Api/ApiEndpoints.cs` — ثبت route extension.
- `Server/Api/UserAdministrationEndpoints.cs` — reuse helper احراز مدیر نصب.
- `tests/Template.Tests/CompanyRoleAdministrationEndpointTests.cs` — تست‌های API و مرزهای امنیتی.
- `docs/handoffs/ISSUE-458-company-role-api.md` — این handoff.
- `docs/WORK_LOG.md` — ثبت افزایشی کار.

## تصمیم‌های فنی
- از مدل‌های موجود استفاده شد؛ AccessGroup/Permission engine یا جدول جدید ساخته نشد.
- API این برش عمداً فقط برای Installation Administrator فعال است. مجوزدهی مدیر شرکت و delegation ceiling به‌عنوان کار جداگانه باقی می‌ماند؛ تا تکمیل آن، دسترسی مدیر شرکت به این endpointها اعطا نمی‌شود.
- AssignMembership فقط نقش عضویت فعال موجود را تغییر می‌دهد و عضویت جدید/غیرفعال را 409 می‌کند تا approval D-065 دور زده نشود.
- هیچ Schema یا Migration تغییر نکرد.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| بازبینی کد و قراردادهای PRهای #454–#456 | انجام شد | تغییرات Backend از Branchهای stacked استخراج شدند؛ UI عمداً منتقل نشد. |
| `dotnet build Template.sln --configuration Release` | اجرا نشده | محیط اجرای فعلی dotnet SDK ندارد و clone محلی به علت عدم دسترسی DNS به GitHub ممکن نشد. |
| `dotnet test Template.sln --configuration Release` | اجرا نشده | منتظر CI PR هستیم؛ نتیجه محلی ادعا نمی‌شود. |
| SQL Server Integration Tests | اجرا نشده | باید در CI با SQL Server واقعی بررسی شود. |
| `git diff --check` | اجرا نشده | تغییرات از طریق Git Data API ساخته می‌شوند؛ checkout محلی در دسترس نیست. |

## امنیت، داده و سازگاری
- تمام endpointها در این برش نقش فعال Admin را از DB بررسی می‌کنند؛ Claim صرفاً AccountKind را محدود می‌کند.
- درخواست‌های تغییردهنده Anti-forgery و sensitive rate limiting دارند.
- شناسه شرکت و نقش با هم در query بررسی می‌شوند.
- Permission code فقط از PermissionDefinitions ثبت‌شده پذیرفته می‌شود.
- پاسخ‌های حساس با no-store علامت‌گذاری می‌شوند.
- هیچ Migration یا تغییر داده مشتری انجام نشده است.
- UI و فایل‌های ClientApp تغییر نکرده‌اند.

## ریسک‌ها و کارهای باقی‌مانده
- Membership lifecycle و تأیید شرکت مقصد هنوز پیاده‌سازی نشده است؛ endpoint تخصیص نقش عمداً این خلأ را دور نمی‌زند.
- مدیر شرکت هنوز از مسیر CompanyRole permission مجوز مدیریت نقش دریافت نمی‌کند؛ این API فعلاً فقط Installation Administrator را می‌پذیرد.
- Fiscal year/period grants، Group directory، domain mapping و membership approval در PRهای مستقل بعدی انجام می‌شوند.
- CI و تست SQL Server هنوز باید بررسی شوند.

## شناسه‌های Git
- Branch: `agent/458-company-role-api`
- Commit: پس از ایجاد commit در Git ثبت می‌شود.
- Pull Request: پس از ایجاد PR ثبت می‌شود.

- تست endpointهای مدیریت کاربران اکنون ProblemDetails JSON و status/title پاسخ 403 را بررسی می‌کند.
