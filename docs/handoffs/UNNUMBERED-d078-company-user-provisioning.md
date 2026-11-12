# تحویل وظیفه — UNNUMBERED-d078-company-user-provisioning

- **Issue:** `UNNUMBERED` (مأموریت D-078 — PR دوم از سری؛ وابسته به PR اول `feat/d078-users-create-permission` / PR #627)
- **Agent/مسئول:** GLM (Senior .NET Backend)
- **تاریخ:** 2026-11-10
- **وضعیت:** انجام‌شده — هسته D-078 (ایجاد User و عضویت داخلی) پیاده و تست شد

## هدف و معیارهای پذیرش
- [x] مدیر مجاز شرکت با `users.create` بتواند برای شرکت خودش User ایجاد کند و عضویت همان شرکت **بدون تأیید اضافی** فعال شود (سناریو A).
- [x] User موجود دوباره ساخته نشود؛ رمز و پروفایل او تغییر/overwrite نشود؛ فقط Membership ساخته شود (سناریو B).
- [x] عضویت تکراری/race: ایندکس یکتای `(UserId, TenantId)` داور نهایی؛ `DbUpdateException` → 409 کنترل‌شده؛ بدون خطای SQL خام (سناریو C).
- [x] عضویت بدون نقش ممکن شود (Migration حداقلی: `RoleId` nullable)؛ عضو بدون نقش **هیچ** دسترسی عملیاتی ندارد (fail-closed).
- [x] ایجاد Membership هیچ Role/Permission/Entitlement یا درخواست Pending نمی‌سازد.
- [x] مدیر شرکت A نتواند برای شرکت B کاربر ایجاد کند (404 یکسان)؛ مدیر نصب مسیر bootstrap دوحالته دارد (D-071).
- [x] Audit امن (`UserCreated` + `UserMembershipCreated`) بدون رمز/hash؛ transaction اتمیک.
- [x] پاسخ یکنواخت 201 برای ایجاد/استفاده مجدد → مقاوم به User Enumeration.
- [x] گاردهای سه‌گانه Migration + تست‌های خود ابزار سبز.

## خلاصه تغییرات
- **Migration ساختاری `20261110130000_MakeCompanyMembershipRoleOptional`:** `UserCompanyMemberships.RoleId` به nullable تغییر کرد (EF tools؛ SQL Server generator وابستگی FK را خودکار drop/add می‌کند). تنها تغییر schema؛ بدون backfill.
- **Endpoint جدید `POST /api/v1/companies/{companyId}/users`:** مجوزدهی دوحالته (مدیر نصب بازخوانی‌شده از DB یا عضو فعال دارای `users.create`)؛ اعتبارسنجی مشترک با مدیر نصب (`UserCreationValidation`)؛ ایجاد یا استفاده مجدد از حساب؛ عضویت فعال بدون نقش؛ Audit؛ 409 کنترل‌شده برای تکرار/حساب غیرفعال/عضویت غیرفعال.
- **سازگاری‌ها:** دفترچه شرکت به left-join تغییر کرد (عضو بدون نقش با فیلدهای نقش null نمایش داده می‌شود)؛ projection جزئیات مدیر نصب null-safe شد؛ `CompanyAccessService` بدون تغییر رفتار (عضو بدون نقش از همه کوئری‌های دسترسی حذف می‌شود — fail-closed).
- **ClientApp (حداقلی و اجباری به‌دلیل تغییر قرارداد):** type های نقش nullable شدند؛ صفحه «کاربران شرکت» برای عضو بدون نقش «بدون گروه دسترسی» نشان می‌دهد؛ پروفایل/فرم کاربر و `AccessPermissionsPage` با null سازگار شدند. RTL و کامپوننت‌های موجود؛ هیچ کتابخانه/Router جدیدی اضافه نشد.
- **توسعه ابزار ممیزی Migration:** `compute_operations` اکنون تغییر required→optional همان رابطه را به‌عنوان `AlterColumn` ستون می‌پذیرد (تغییر ساختاری همچنان fail-closed)؛ `defaultValue` در `AlterColumn` فقط با مقدار دقیق CLR default پذیرفته می‌شود؛ خطای lookup کلید FK در added-relationships اصلاح شد. هر دو مجموعه تست ابزار (۳۲+۳۱) سبز.

## فایل‌های مهم تغییرکرده
- `Domain/UserCompanyMembership.cs`، `Infrastructure/Persistence/Migrations/20261110130000_*` (+Designer/Snapshot)
- `Server/Api/CompanyUserProvisioningEndpoints.cs` (جدید)، `Server/Api/UserCreationValidation.cs` (جدید)، `Server/Api/UserAdministrationEndpoints.cs`، `Server/Api/CompanyUserDirectoryEndpoints.cs`، `Server/Api/CompanyRoleAdministrationEndpoints.cs`، `Server/Api/ApiEndpoints.cs`، `Server/Infrastructure/Tenancy/CompanyAccessService.cs`
- `ClientApp/src/api/types.ts`، `components/company/CompanyUsersPage.tsx`، `components/admin/users/profile/ProfileTabs.tsx`، `components/admin/users/form/UserFormRolesPanel.tsx`، `components/admin/access/AccessPermissionsPage.tsx`
- `scripts/migration_model.py`، `scripts/verify_migration_model.py`، `scripts/generate_migrations.py`
- `docs/api/USER_ACCESS_MANAGEMENT_API.md` (بخش ۴.۲ + ۴.۱ + ۲.۲)، `docs/PERMISSION_CATALOG.md`
- تست‌ها: `tests/Template.Tests/CompanyUserProvisioningEndpointTests.cs` (۹ تست)، `tests/Template.Tests/Integration/CompanyUserProvisioningSqlServerTests.cs` (۴ تست SQL-gated)

## تصمیم‌های فنی
- **Nullable RoleId به‌جای Role عمومی:** مأموریت صراحتاً Role عمومی/پیش‌فرض را ممنوع کرده و مدل D-077 هنوز پیاده نشده؛ حداقل تغییر سازگار با D-066 (تخصیص نقش مستقل از عضویت) همین است.
- **پاسخ 201 یکنواخت:** تفاوت 200/201 می‌توانست وجود حساب را افشا کند؛ body فقط `userCreated` را می‌گوید (برای مدیر دارای مجوز).
- **عضویت غیرفعال از این مسیر بازفعال نمی‌شود** تا چرخه Suspended/Revoked همچنان فقط با تصمیم مقصد (گردش تأیید) تغییر کند — هم‌راستا با قرارداد endpoint تخصیص نقش.
- **بدون سرویس/abstraction جدید موازی:** منطق مشترک فقط اعتبارسنجی است (دو مصرف‌کننده واقعی)؛ باقی منطق یک‌جا در endpoint جدید است.
- فرم ایجاد کاربر مدیر نصب (`POST /admin/users`) رفتار قبلی خود را حفظ کرد (بدون عضویت) — D-064.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| `dotnet build Template.sln -c Release` | موفق | ۰ خطا |
| `dotnet test Template.sln -c Release` | ۱۱۷۴ کل: ۱۱۰۱ موفق / ۷۱ Skip / ۲ ناموفق | همان ۲ شکست محیطی قدیمی main؛ ۱۳ تست جدید (۹ endpoint + ۴ SQL-gated) |
| `python3 scripts/generate_migrations.py --check` | موفق | |
| `python3 scripts/verify_migration_model.py` | موفق | ۱۸ migration تأیید شد |
| `dotnet ef migrations has-pending-model-changes` | موفق | «No changes have been made to the model.» |
| `python3 scripts/tests/test_migration_tooling.py` | ۳۲/۳۲ موفق | تست‌های خود ابزار |
| `python3 scripts/tests/test_migration_guard_mutations.py` | ۳۱/۳۱ موفق | جهش‌های گارد |
| `npm run typecheck` / `npm test` / `npm run build` / `npm run check:offline` | typecheck ۰ خطا؛ **۱۰۲۵/۱۰۲۵** موفق؛ build و offline سبز | |
| `python3 scripts/check_static_assets.py` | PASS | |
| تست‌های SQL Server این PR | **NotRun محلی** | Docker موجود نیست؛ در CI با `FARANOU_TEST_SQLSERVER` اجرا می‌شوند |

## امنیت، داده و سازگاری
- Tenant isolation: مجوز فقط از DB (`CompanyAccessService`)؛ CompanyId مسیر بدون عضویت/مجوز → 404؛ cross-company غیرممکن.
- عضو تازه بدون نقش: هیچ permission، هیچ accessible-company، هیچ grant سال/دوره — fail-closed با تست.
- هیچ رمز/hash/token در Audit یا پاسخ؛ Audit فقط actor/company/target/نتیجه.
- Migration additive؛ rollback فقط با گارد صریح (Down بدون عضو بدون نقش ممکن است؛ EF `defaultValue` توسط verifier دقیق تأیید می‌شود).
- رفتارهای قبلی (تأیید بین‌شرکتی، سقف تفویض، نقش سیستمی، bootstrap) بدون تغییر — تست‌های موجود همه سبز.

## ریسک‌ها و کارهای باقی‌مانده
- تست‌های SQL Server در CI (محلی NotRun)؛ PRهای بعدی سری: تست‌های سازگاری تخصیص نقش (PR سوم)، سخت‌سازی بین‌شرکتی (PR چهارم)، وضعیت D-078 در مستندات (PR پنجم).
- UI اختصاصی «ایجاد کاربر شرکتی» برای مدیر شرکت در این PR ساخته نشد (قرارداد API آماده است؛ نیازمند تصمیم محصولی صفحه) — مصرف endpoint توسط UI در PR مستقل بعدی.

## شناسه‌های Git
- Branch: `feat/d078-company-user-provisioning` (stacked روی PR #627)
- Commit: (این PR)
- Pull Request: (لینک پس از ایجاد PR)
