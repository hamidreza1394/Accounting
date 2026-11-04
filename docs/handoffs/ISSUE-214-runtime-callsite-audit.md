# ممازی مسیرهای Runtime لایسنس — Issue #214 (Callsite Enforcement)

- Issue: [#214](https://github.com/hamidreza1394/Accounting/issues/214)
- Branch: `fix/214-runtime-callsite-enforcement`
- Base: `fix/214-runtime-evaluator` @ `963fb3e689cb852a3edd720dc57554c529f40d8d` (PR #526)
- تاریخ ممازی: 2026-11-04
- وضعیت: ممازی کامل شد؛ هیچ production callsite برای `ICustomerLicenseRuntimeEvaluator` یافت نشد.

## محدودیت صریح این کار
- کد PR #526 بازنویسی یا تکرار نشد.
- Startup/license-first gate کامل متعلق به [#396](https://github.com/hamidreza1394/Accounting/issues/396) است.
- Module/Seat/Feature entitlement متعلق به [#390](https://github.com/hamidreza1394/Accounting/issues/390) / [#391](https://github.com/hamidreza1394/Accounting/issues/391) / [#401](https://github.com/hamidreza1394/Accounting/issues/401) است.
- Force Push انجام نشد و انجام نخواهد شد.

## جدول مسیرها

| # | مسیر | فایل / متد | منبع License | Validator / تصمیم | نتیجه ممازی |
|---|------|------------|--------------|-------------------|-------------|
| 1 | Import لایسنس | `Server/Api/ApiEndpoints.cs` → `ImportCustomerLicenseAsync` → `CustomerLicenseImportService.ImportAsync` | `request.EnvelopeJson` + `InstallationId` از `CustomerProvisioningSettings` (نه از کلاینت) | `ICustomerLicenseEnvelopeValidator` = `RevocationAwareLicenseEnvelopeValidator` | **امن از نظر revocation** در لحظه import. |
| 2 | Runtime evaluation | `CustomerLicenseRuntimeEvaluator.EvaluateCurrentAsync` | `InstalledLicenses` `State=Current` + `EnvelopeJson` | revocation-aware validator؛ بدون cache | **#526**؛ **بدون caller تولیدی**. |
| 3 | DI registration | `Server/Program.cs` (~247) | — | `AddCustomerLicenseRuntimeEvaluator` | خام validator در DI expose نیست. |
| 4 | Startup / Host | `Server/Program.cs` pipeline | — | بدون license gate | **شکاف** → #396 (T43). |
| 5 | ActivationKeys middleware | `ActivationKeysHandlerMiddleware` | `ApplicationSettings.ActivationKeys` | نه لایسنس امضاشده | **کد مرده**؛ در pipeline نیست. |
| 6 | API عمومی | `Server/Api/*Endpoints.cs` | — | auth/RBAC فقط | **شکاف**: بعد login بدون re-validation لایسنس. |
| 7 | Trade access | `TradeAccessGate` + `DenyAllTradeModuleEntitlement` | همیشه deny | بدون LicenseEnvelope | Fail-closed موقت؛ اتصال به payload = #401/#390. |
| 8 | Refresh / Reload | — | — | — | مسیر اختصاصی یافت نشد. |
| 9 | Background / Cache | Server/Infrastructure/Modules | — | — | Cache نتیجه validation یافت نشد. |
| 10 | Enrollment هویت نصب | `CustomerInstallationIdentityEnrollmentService` | هویت نصب | proof verifier | پیش‌نیاز import. |
| 11 | Provisioning apply | `CustomerProvisioningApplyService` | بسته provisioning | package verifier | جدا از LicenseEnvelope تجاری. |
| 12 | خواندن `InstalledLicenses` | فقط Import + RuntimeEvaluator | DB | — | سرویس دیگری برای usable بودن نمی‌خواند. |
| 13 | Apply snapshot ابطال | `OfflineRevocationConsumer` / TPM state store | snapshot امضاشده | trust + sequence + TPM | زیرساخت هست؛ API مشتری apply در ApiEndpoints دیده نشد. |

## جمع‌بندی شکاف callsite

```
Import ──► RevocationAware validator ──► persist InstalledLicense
                                              │
                                              ▼
                         ICustomerLicenseRuntimeEvaluator  ◄── #526
                                              │
                                              └── caller تولیدی: هیچ
```

پس از import، ابطال آفلاین روی رفتار واقعی سرور اثر نمی‌گذارد مگر callerهای تولیدی evaluator را صدا بزنند.

## اولویت اتصال (بدون مخلوط scope)
1. **این Branch** — ممازی + اتصال‌های حداقلی بدون تداخل با #396/#390
2. **#396** — Startup / license-first gate (سرور، نه فقط React)
3. **#390 / #391 / #401** — Entitlement از payload پس از EvaluateCurrentAsync موفق

## کنترل‌های الزامی در هر callsite
- فقط `ICustomerLicenseRuntimeEvaluator` یا validator revocation-aware
- هرگز اعتماد به ردیف DB به‌تنهایی
- Unavailable ≠ NotRevoked
- Grace در runtime usable نیست
- InstallationId فقط از سرور، نه کلاینت
