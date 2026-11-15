# تحویل وظیفه — ISSUE-681-vendor-initial-v2-company-entitlements

- **Issue:** #681 — vendor: issue initial company-scoped LicensePayload v2 for base licenses
- **Agent/مسئول:** Claude Code (Lead) با Subagentهای `frontend-developer`، `code-reviewer`، `security-reviewer`
- **تاریخ:** 2026-11-15
- **وضعیت:** انجام‌شده (PR ادغام نشده؛ تأیید نهایی با مسئول مخزن)

## هدف و معیارهای پذیرش
- [x] ادمین Vendor چند شرکت (descriptor، Modules، Featureهای typed، MaxUsers) را برای لایسنس پایه تعریف می‌کند.
- [x] `CompanyReference` فقط سمت سرور GUID-D؛ مرجع ناشناخته/لایسنس دیگر/تکراری رد؛ حذف و افزودن مجدد دور نمی‌زند.
- [x] هر تغییر پیش‌نویس تأیید قبلی را باطل می‌کند؛ محتوای امضاشده = محتوای تأییدشده؛ رقابت RowVersion امن.
- [x] Envelope بیرونی بدون تغییر؛ مسیر v1 و `TryNormalize` بدون تغییر؛ تغییرناپذیری PR #680 حفظ شد.
- [x] پس از صدور پیش‌نویس به `{CompanyReference, Descriptor}` کاهش می‌یابد و Amendment از Envelope بازیابی می‌کند (آزمون).

## خلاصه تغییرات
- ستون nullable `VendorLicenses.CompanyEntitlementsDraftJson` (مصوب مالک) با یک Migration افزایشی Vendor؛ NULL = مسیر v1؛ بدون جدول جدید، بدون تغییر Customer DB، بدون Backfill.
- `VendorLicenseCompanyDraft`: نرمال‌سازی، اعتبارسنجی با قرارداد، سقف ۶۴KiB، descriptor، کاهش پس از صدور، نگاشت کد خطا.
- `VendorLicenseEntitlementRules.TryNormalizeCompanyScoped` (هستهٔ مشترک بدون تغییر معنایی v1).
- `VendorLicensePayloadBuilder`: شاخهٔ v2 (Subsystems سطح نصب خالی، `SubsystemsJson` استفاده نمی‌شود، fail-closed).
- `VendorLicenseIssuanceService`: کاهش پیش‌نویس در همان `SaveChanges`/تراکنش صدور.
- API: `companies` در Create/Update/Response؛ `company_entitlements_required` برای بازگشت v2→v1؛ `subsystems` برای لایسنس شرکت‌محور خالی نمایش داده می‌شود.
- Vendor.Web: ویرایشگر شرکت‌ها در فرم لایسنس و ستون فهرست.

## فایل‌های مهم تغییرکرده
- `Vendor/Vendor.Persistence/Domain/VendorBusinessEntities.cs`، `Migrations/20261115153531_VendorLicenseCompanyEntitlementsDraft*.cs`، `VendorDbContextModelSnapshot.cs` — ستون و Migration (Designer/Snapshot دستی هم‌قالب فایل‌های قبلی؛ `has-pending-model-changes` تمیز).
- `Vendor/Vendor.Host/Authorization/VendorLicenseCompanyDraft.cs` (جدید)، `VendorLicenseEntitlementRules.cs`، `VendorBusinessEndpoints.cs`.
- `Vendor/Vendor.Host/Signing/VendorLicensePayloadBuilder.cs`، `VendorLicenseIssuanceService.cs`.
- `Vendor/Vendor.Web/src/CompanyEntitlementsEditor.tsx` (جدید)، `LicenseForm.tsx`، `BusinessLists.tsx`، `LicenseForm.test.tsx`.
- تست‌ها: `VendorLicenseCompanyDraftTests`، `VendorLicenseV2IssuancePayloadTests`، `VendorLicenseCompanyDraftSqlServerTests`، `VendorLicenseEntitlementRulesTests`، بلوک جدید در `VendorCustomerMutationHttpIntegrationTests`.
- `docs/LICENSING.md`، `docs/WORK_LOG.md`.

## تصمیم‌های فنی
- تصمیم معماری جدیدی گرفته نشد؛ فقط تصمیم‌های مالک اجرا شد (ستون، کاهش پس از صدور، Subsystem نسخه‌محور، مرجع سمت سرور).
- پیش‌نویس روی ردیف لایسنس است تا `RowVersion` همان اتصال Maker-Checker را برای محتوای شرکت‌ها هم تأمین کند.
- Check constraint برای ستون اضافه نشد (خارج از تأیید مالک).

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| `dotnet test tests/Vendor.Host.Tests` (روی SQL Server 2022 واقعی در Docker، با SPA ساخته‌شده) | موفق: ۲۹۴ از ۲۹۵ | ۰ ناموفق، ۱ Skip (SoftHSM). شامل تست‌های SQL: Migration، صدور و کاهش، rollback با شکست Audit، تغییر پیش‌نویس پس از تأیید، رقابت ویرایش/صدور، v1 compatibility، Amendment، API |
| `dotnet test tests/Licensing.Contracts.Tests` | موفق: ۱۶۷ از ۱۶۷ | |
| `dotnet build Template.sln -c Release` | موفق | ۰ خطا |
| Vendor.Web: `npm ci`، `typecheck`، `npm test`، `npm run build` | موفق | ۸ فایل/۶۸ تست؛ خروجی `Vendor.Host/wwwroot` پس از آزمون پاک شد |
| `dotnet ef migrations has-pending-model-changes` (Vendor) | موفق | ابزار 10.0.13 |
| `python3 scripts/generate_migrations.py --check` | موفق | Guard برنامهٔ اصلی؛ Vendor را پوشش نمی‌دهد |
| `python3 scripts/check_static_assets.py` | موفق | |
| `python3 scripts/verify_migration_model.py` | اجرا نشد | `tree-sitter` نصب نیست؛ مربوط به Migrationهای برنامهٔ اصلی |
| تست دستی مرورگر، E2E، تست‌های `ClientApp` اصلی | اجرا نشد | خارج از دامنه |
| فرایند RED/GREEN | | برای مدل پیش‌نویس، Builder، کاهش صدور و null/Bidi RED مشاهده شد؛ برای `TryNormalizeCompanyScoped` و بلوک API RED جداگانه مشاهده نشد |

## امنیت، داده و سازگاری
- فقط دیتابیس Vendor؛ ستون nullable افزایشی؛ `Down` فقط ستون را حذف می‌کند (اثر: از بین رفتن پیش‌نویس‌های صادرنشده و descriptorها؛ Envelope دست‌نخورده).
- بازبین امنیتی: بدون Critical/High/Medium؛ L1 (Bidi/نامرئی در descriptor) اصلاح شد؛ I1 (Subsystems قدیمی در پاسخ) اصلاح شد.

## ریسک‌ها و کارهای باقی‌مانده
- ردیف خراب پیش‌نویس (فقط با دست‌کاری DB) فهرست لایسنس‌ها را 500 می‌کند؛ ستون `CHECK ISJSON` ندارد (L2، نیازمند تأیید Schema جداگانه).
- اندازهٔ Payload نهایی هنگام صدور (409) سنجیده می‌شود نه هنگام ذخیره.
- تغییر شناسهٔ شرکت‌ها پس از صدور اولیه، Amendment v2→v2 و اتصال Customer/Provisioning v5 در این Issue نیست.

## شناسه‌های Git
- Branch: `feat/issue-681-initial-v2-company-entitlements`
- Commit / Pull Request: در گزارش نهایی ثبت می‌شود.
