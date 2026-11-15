# تحویل وظیفه — ISSUE-392-vendor-provisioning-v5-prepare

- **Issue:** #392 (وابسته: #228، #235، #390، #391، #679، #680، #681/#682)
- **Agent/مسئول:** Claude Code (Lead) + بازبین‌های مستقل فقط‌خواندنی (`code-reviewer`، `security-reviewer`)
- **تاریخ:** 2026-11-15
- **وضعیت:** **ناقص عمدی در سطح محصول** — سمت Vendor آماده و تست‌شده؛ **صدور عملیاتی v5 غیرفعال** و سمت Customer `NotImplemented`.
- **Commit مبنا:** `origin/main@ce20943` (بدون تغییر نسبت به Gap Analysis؛ #679، #680، #682 ادغام شده‌اند و PR بازی نبود)

## هدف و معیارهای پذیرش
- [x] Vendor بتواند `CompanyReference` را فقط از LicensePayload v2 امضاشده و تأییدشده استخراج کند.
- [x] ورودی Browser/API هیچ‌گاه مقدار صادرشده نباشد (فقط انتخاب‌گرِ عضو مجموعهٔ امضاشده).
- [x] v1–v4 بدون تغییر رفتار/بایت.
- [x] صدور v5 پیش از آماده‌شدن Customer عملیاتی نشود (Gate دوکلیدی، پیش‌فرض خاموش).
- [x] تست‌های مرتبط (واقعی) سبز؛ مستندات هم‌سو.
- [ ] Customer بتواند v5 را تأیید، به Company محلی Map و Apply کند — **خارج از این PR** (نیازمند تأیید Schema).

## بررسی سازگاری دو طرف (مرحلهٔ ۲؛ از روی کد `main@ce20943`)
| پرسش | پاسخ با شاهد |
|---|---|
| Vendor مرجع را چگونه می‌گیرد؟ | قبل از این PR: نمی‌گرفت؛ `VendorProvisioningLicenseResolver` برای v2 همیشه `provisioning_company_reference_mapping_required` می‌داد. اکنون (Gate باز) از `CompanyEntitlements` Payload امضاشده با `VendorProvisioningCompanyReferenceRules.Select`. |
| صدور v5 در Vendor امکان‌پذیر بود؟ | خیر: Issuance/Export/IsSameRequest روی `SchemaVersion == 4` قفل و `TryValidatePayload` بدون `allowCompanyReference`. |
| Customer v5 را می‌پذیرد؟ | خیر: `CustomerProvisioningPackageVerifier` → `TryValidateAndVerify` بدون `allowCompanyReference` → `provisioning_payload_invalid` (تست حفاظتی افزوده شد). |
| Customer می‌تواند به Company محلی متصل کند؟ | خیر: `ApplyAsync` فیلد را نمی‌خواند و جدول/ستون Mapping وجود ندارد. |
| Apply بدون Mapping بستهٔ v5 را رد می‌کند؟ | بله، از پیش در لایهٔ اعتبارسنجی (نه Apply)؛ پس هیچ Company بدون Mapping ساخته نمی‌شود. |
| فعال‌کردن v5 بستهٔ غیرقابل‌اعمال می‌سازد؟ | بله اگر Gate باز می‌شد؛ بنابراین Gate دوکلیدی با قفل کدی `CustomerConsumerReady=false` گذاشته شد. |

نتیجه طبق قانون مأموریت: **صدور عملیاتی فعال نشد**؛ فقط آماده‌سازی پشت Gate پیش‌فرض خاموش (الگوی موجود `Signing:Pkcs11:Enabled`، بدون سامانهٔ Feature Flag جدید).

## خلاصه تغییرات
- `VendorProvisioningCompanyReferenceGate` (جدید): کلید پیکربندی `Provisioning:CompanyReferenceIssuance:Enabled` (فقط `true` صریح) **و** قفل کدی `CustomerConsumerReady` (اکنون `false`). `Program.cs` آن را singleton ثبت می‌کند.
- `VendorProvisioningCompanyReferenceRules.Select` (جدید): انتخاب مرجع از Payload v2؛ رد: بدون انتخاب‌گر، انتخاب‌گر ناعضو، مرجع تکراری/نامعتبر در Payload، کد غیرمجاز، Company بدون زیرسیستم قابل‌Provision، زیرمجموعهٔ نامعتبر؛ `TRADE_IMPORT` کپی نمی‌شود.
- `VendorProvisioningLicenseResolver`: شاخهٔ v2 با Gate خاموش مثل قبل مسدود؛ با Gate باز `Select`. overloadهای جدید با انتخاب‌گر؛ v1 با انتخاب‌گر → `provisioning_company_reference_not_applicable`. `BindingResult.CompanyReference` افزوده شد.
- `VendorProvisioningRequestRules`: `CompanyReference` انتخاب‌گر در DTO، اعتبارسنجی فرمت، `BuildPayload` (v4 یا v5 از روی Binding)، `MatchesExisting` آگاه از schema/مرجع.
- `VendorProvisioningLicenseBindingRules`، `VendorProvisioningIssuanceService`، `VendorProvisioningEndpoints`: پذیرش v4|v5، Resolve مجدد با مرجع، `allowCompanyReference` فقط وقتی Gate باز است.

## فایل‌های مهم تغییرکرده
- `Vendor/Vendor.Host/Authorization/VendorProvisioningCompanyReference{Gate,Rules}.cs` (جدید)
- `Vendor/Vendor.Host/Authorization/VendorProvisioning{LicenseResolver,RequestRules,LicenseBindingRules,Endpoints}.cs`، `Vendor/Vendor.Host/Signing/VendorProvisioningIssuanceService.cs`، `Vendor/Vendor.Host/Program.cs`
- تست‌ها: `VendorProvisioningCompanyReference{Gate,Rules}Tests`، `VendorProvisioningV5PayloadRulesTests`، `VendorProvisioningV5IssuanceSqlServerTests` (جدید)؛ helperهای `VendorProvisioningLicenseResolverSqlServerTests` internal شدند؛ `CustomerProvisioningPackageVerifierTests` (تست حفاظتی)
- مستندات: `docs/LICENSING.md`، `docs/PROJECT_STATUS.md`، `docs/WORK_LOG.md`، این فایل

## تصمیم‌های فنی (نیازمند تأیید مالک؛ ADR ثبت نشد)
- هر درخواست Provisioning دقیقاً **یک** Company؛ `Subsystems` v5 = ماژول‌های همان Company (فقط کدهای Provisioning) یا زیرمجموعهٔ درخواستی.
- انتخاب‌گر الزامی است؛ حتی برای لایسنس تک‌شرکتی استنباط نمی‌شود (`provisioning_company_reference_required`).
- ADR/تصمیم جدیدی تأیید یا ثبت نشد؛ D-059/D-060/D-087/D-089 و D-044 دست نخوردند. شکل Mapping، معنای شمارش `maxCompanies`، «مدیر مجاز مشتری»، سیاست Restore/Clone و ترتیب نسبت به Tenant همچنان `NeedsApproval`‌اند.

## بررسی و آزمون (نتایج واقعی؛ SQL Server 2022 موقت Docker، رمز چاپ نشد)
| فرمان/آزمون | اجرا | موفق | ناموفق | Skip |
|---|---|---|---|---|
| RED: Build تست‌ها پیش از پیاده‌سازی | اجرا شد | — | خطای کامپایل (نبود Gate/Rules/overload) | — |
| `VendorProvisioningCompanyReferenceGateTests` | ۱۰ | ۱۰ | ۰ | ۰ |
| `VendorProvisioningCompanyReferenceRulesTests` | ۱۶ | ۱۶ | ۰ | ۰ |
| `VendorProvisioningV5PayloadRulesTests` | ۱۷ | ۱۷ | ۰ | ۰ |
| `VendorProvisioningV5IssuanceSqlServerTests` (SQL واقعی) | ۱۲ | ۱۲ | ۰ | ۰ |
| `VendorProvisioningLicenseResolverSqlServerTests` (SQL واقعی) | ۱۰ | ۱۰ | ۰ | ۰ |
| کل `Vendor.Host.Tests` (SQL واقعی؛ SPA ساخته‌شده) | ۳۵۰ | ۳۴۸ | ۰ | ۲ (SoftHSM؛ Dual-Host که بعداً جدا اجرا شد) |
| `VendorCustomerMutationHttpIntegrationTests` + Dual-Host E2E (هر دو SQL) | ۲ | ۲ | ۰ | ۰ |
| `Licensing.Contracts.Tests` (golden v1–v5) | ۱۶۷ | ۱۶۷ | ۰ | ۰ |
| `Template.Tests` کلاس‌های `CustomerProvisioning*` (SQL واقعی) | ۶۵ | ۶۵ | ۰ | ۰ |
| آزمون جهش (۲ قاعدهٔ حیاتی، سپس بازگردانی) | ۲ جهش | هر دو توسط تست درست گرفته شد | — | — |
| `python3 scripts/check_static_assets.py` | اجرا شد | PASS | — | — |
| `python3 scripts/generate_migrations.py --check` | اجرا شد | up to date | — | — |
| `git diff --check` | اجرا شد | تمیز | — | — |

**اجرا نشد (با دلیل):** `dotnet test Template.sln` کامل و `dotnet build Template.sln` (فقط پروژه‌های Vendor.Host.Tests/Template.Tests/Licensing.Contracts.Tests ساخته و اجرا شدند؛ کل Solution را CI می‌سازد)؛ `verify_migration_model.py` و `dotnet ef has-pending-model-changes` (Migration/مدل تغییر نکرد؛ `tree-sitter` نصب نیست)؛ Vendor.Web typecheck/vitest (فرانت‌اند تغییر نکرد؛ فقط `npm run build` برای تست Liveness)؛ تست E2E Playwright؛ `SoftHsm` (محیط). تست HTTP سطح Endpoint برای مسیر v5 با Gate باز **نوشته نشد** (Gate در هر استقرار خاموش است؛ منطق از طریق Rules/Resolver/Issuance سنجیده شد).

## امنیت، داده و سازگاری
- بدون Migration، جدول/ستون یا تغییر Customer؛ بدون تغییر `Licensing.Contracts`، Envelope، امضا یا Crypto Profile.
- `maxCompanies`/`SubsystemsJson` سطح Installation هرگز مجوز شرکت نمی‌شوند؛ تست دارد.
- بازبینی مستقل: به بخش «بازبینی» پایین مراجعه شود.

## بازبینی
- **سطح:** کار حساس (لایسنس/ورودی اپراتور) → `code-reviewer` و `security-reviewer` مستقل و فقط‌خواندنی. `architect` اجرا نشد (تصمیم معماری/Schema جدیدی نیست؛ موارد NeedsApproval فهرست شد).
- **یافتهٔ Critical/High/Medium:** ندارد. `security-reviewer`: ۳ مورد Low؛ `code-reviewer`: ۵ مورد (Medium فرایندی «نبود handoff/WORK_LOG» با افزودن همین دو برطرف شد).
- **اصلاح‌شده:** وضعیت مستندات به «Implemented — در انتظار CI»؛ جملهٔ متناقض v4/v5 در PROJECT_STATUS؛ توضیح دقیق `bool.TryParse` برای Gate.
- **ثبت‌شده، بدون اصلاح در این PR:** (L-1) `IdPattern` در `StructuralValidation` با `$` یک `\n` پایانی را می‌پذیرد (از پیش در main؛ اثر امروز ناچیز چون انتخاب Ordinal و مقدار از Payload امضاشده است؛ نیازمند Issue/PR جدا روی Contracts با `\z`). (L-2) مرجع‌های متفاوت‌فقط‌درحروف در یک لایسنس مجازند؛ Mapping سمت Customer باید مقایسه/Collation دودویی داشته باشد. `eligible-licenses` با Gate باز لایسنس v2 را نه eligible نه blocked می‌کند (بازنگری در PR فعال‌سازی/UI). نبود تست HTTP برای replay/export v5 با Gate باز/بسته (فقط سطح Rules/Resolver/Issuance).

## ریسک‌ها و کارهای باقی‌مانده
- Customer: تأیید v5، Mapping پایدار `CompanyReference → Company` (نیازمند تأیید Schema)، Apply اتمیک، `maxCompanies`، Bootstrap مدیر، Restore/Clone و Tenant.
- Vendor: UI انتخاب Company در Vendor.Web؛ `eligible-licenses` با Gate باز لایسنس v2 را فهرست نمی‌کند (تا UI انتخاب وجود ندارد)؛ یکتایی «هر مرجع یک Company» سمت Customer اعمال خواهد شد، نه Vendor.
- روشن‌کردن Gate فقط پس از PR مصرف‌کنندهٔ Customer و تغییر هم‌زمان `CustomerConsumerReady` و تست پین آن.

## شناسه‌های Git
- Branch: `feat/issue-392-vendor-provisioning-v5-prepare`
- Commit/Pull Request: پس از ایجاد در PR ثبت می‌شود.
