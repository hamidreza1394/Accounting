# تحویل وظیفه — ISSUE-390-company-scoped-entitlement-contract-v2

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/390
- **Agent/مسئول:** ChatGPT
- **تاریخ:** 2026-11-03
- **وضعیت:** پیاده‌سازی در PR؛ منتظر CI

## هدف و معیارهای پذیرش
- [x] حفظ payload v1 و canonical bytes آن.
- [x] افزودن payload v2 با entitlement مستقل Company/Module/Feature/UserSeat.
- [x] افزودن کد دقیق `TRADE_IMPORT` فقط در entitlement شرکت‌محور v2؛ v1 آن را نمی‌پذیرد.
- [x] پشتیبانی تاریخ‌های support/update مستقل از expiry.
- [x] strict unknown-field و canonical JSON برای v2.
- [x] تعریف قواعد pure برای amendment همان LicenseId/Installation و Sequence افزایشی.
- [ ] CI روی SHA نهایی موفق شود و PR ادغام شود.
- [ ] Customer persistence/replay و enforcement در #391؛ Vendor issuance در #393.

## خلاصه تغییرات
- Payload version از Envelope version جدا شد؛ Envelope بیرونی و ECDSA P-256/SHA-256 / IEEE P1363 64-byte بدون تغییر باقی ماند.
- v2 از global subsystem list استفاده نمی‌کند؛ entitlementها در `companyEntitlements[]` scope می‌شوند.
- Feature catalog کدهای ثابت و type ثابت دارد؛ enum در wire schema موجود است، اما هیچ enum key بدون مقادیر محصولی مصوب فعال نشده است.
- Amendment helper امضا را بررسی نمی‌کند و state ذخیره نمی‌کند؛ caller باید هر دو Envelope را پیش از آن validate کند.

## فایل‌های مهم تغییرکرده
- `Licensing.Contracts/LicensePayload.cs` — مدل payload v2 و تاریخ‌های مستقل.
- `Licensing.Contracts/CanonicalJson.cs` — canonical serialize/parse و strict v2.
- `Licensing.Contracts/StructuralValidation.cs` — allowlist و bounds.
- `Licensing.Contracts/LicenseEnvelopeValidator.cs` — canonical enforcement پس از signature verification.
- `Licensing.Contracts/CompanyLicenseEntitlement.cs`, `LicenseFeatureCatalog.cs`, `LicenseRevisionRules.cs` — قراردادهای جدید.
- `tests/Licensing.Contracts.Tests/CompanyEntitlementContractTests.cs` — regression suite.
- مستندات مدل داده، workflow، threat model، traceability، plan و work log به‌روزرسانی شدند.

## تصمیم‌های فنی
- D-045 cryptographic profile و D-054 provisioning v4 حفظ شدند.
- v1 bytes/allowlist legacy حفظ شدند؛ Trade فقط با code دقیق `TRADE_IMPORT` در v2 و scope شرکت شناخته می‌شود.
- absence semantics: در v2 نبود companyEntitlements نامعتبر؛ آرایه خالی یعنی هیچ Company entitlement ندارد؛ نبود module/feature یعنی عدم اعطا؛ maxUsers الزامی و مثبت است؛ maxCompanies=null یعنی سقف Installation در payload مشخص نشده است.
- catalog اولیه:
  - finance.analytic_account_levels → max_count
  - finance.cost_centers → max_count
  - finance.budgeting → enabled
  - inventory.warehouses → max_count
  - sales.pos → enabled
- enum wire type تعریف شده، اما enum feature key تا مشخص‌شدن value allowlist محصولی فعال نیست.
- هیچ تغییر customer-side، `Server/**`، Vendor issuance یا migration انجام نشده است.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| تست محلی .NET | اجرا نشده | این نشست checkout/SDK محلی ندارد. |
| Licensing.Contracts.Tests | در انتظار CI | regressionهای جدید باید اجرا شوند. |
| .NET CI | در انتظار CI | build/test/E2E/publish روی SHA PR. |
| Semgrep / CodeQL | در انتظار CI | بررسی‌های الزامی PR. |
| `git diff --check` | اجرا نشده محلی | commit از Git Data API ساخته می‌شود؛ CI و بازبینی diff لازم‌اند. |

## امنیت، داده و سازگاری
- private key، secret، credential یا داده مشتری اضافه نشده است.
- no Customer DB migration; no `Server/**`; no Vendor-only dependency.
- v2 strict parser و canonical check پس از signature verification اجرا می‌شوند.
- v1 parser از DTO مستقل legacy استفاده می‌کند و تمام فیلدهای ناشناخته/فقط-v2 را حتی با JSON type متفاوت نادیده می‌گیرد؛ canonical output نیز حفظ می‌شود.

## ریسک‌ها و کارهای باقی‌مانده
- این PR قرارداد را تعریف می‌کند؛ runtime persistence/replay و entitlement enforcement در #391 پیاده‌سازی می‌شوند.
- Vendor v2 payload builder/amendment issuance در #393 انجام می‌شود.
- enum feature keys/allowed values نیازمند catalog extension مستقل است.
- feature codeها باید در PR #391 به policy enforcement و در #393 به commercial order mapping متصل شوند.

## شناسه‌های Git
- Branch: `feat/issue-390-company-scoped-license-contract-v2`
- Commit: پس از ساخت
- Pull Request: پس از ساخت
