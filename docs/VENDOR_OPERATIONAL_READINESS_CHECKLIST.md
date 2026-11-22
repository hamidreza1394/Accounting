# چک‌لیست آمادگی عملیاتی Vendor — Gateها و پیش‌نیازهای Production — `Proposed`

> **وضعیت سند:** خروجی S-G مأموریت. این چک‌لیست فقط «وضعیت واقعی + پیش‌نیاز + مسئول + شواهد لازم» را ثبت می‌کند؛ **هیچ Gate یا فلگی را باز نمی‌کند و تکمیل مستندات به‌تنهایی مجوز باز کردن هیچ گیت یا ادعای Production readiness نیست.** مبنای واقعیت: `docs/DECISIONS.md` (D-041/D-055/D-057/D-058/D-107)، `docs/LICENSING.md` (هدر وضعیت main@`4c91797e`)، `docs/PROJECT_STATUS.md` snapshot 2026-11-22 و کد main@`58a1ccd4`.

## ۱) گاردهای جداسازی D-041 (G1..G6)

وضعیت ثبت‌شده در پیگیری گاردهای D-041 (بند «پیگیری گاردها، 2026-10-25») — از آن زمان تغییر وضعیت ثبت‌شدهٔ جدیدی در مستندات وجود ندارد:

| گارد | تعریف | وضعیت فعلی | پیش‌نیاز بسته‌شدن | مسئول | شواهد لازم | تست تأیید | مانع باقی‌مانده | قابل انجام در محیط فعلی؟ |
|---|---|---|---|---|---|---|---|---|
| G1 | آزمون وابستگی: گراف `Server` نباید به هیچ پروژهٔ `Vendor/**` برسد؛ `Licensing.Contracts` بدون ProjectReference/link بیرونی | **انجام‌شده** — ادغام PR #96؛ CI run `40214517027` شامل build-and-test و secret-scan موفق | نگهداری تست منفی | نگهدارندهٔ مخزن/CI | تست منفی در CI سبز بماند | افزودن عمدی ارجاع در شاخهٔ موقت باید گارد را بشکند | نیست | بله (تست موجود) |
| G2 | بررسی خروجی Publish: allow-list اسمبلی‌ها؛ رد `Vendor.*` و الگوهای کلید خصوصی در `scripts/publish.sh/.ps1` و Job publish | **ناقص** — گارد فعلی Vendor DLL/کلید را رد می‌کند اما allow-list کامل همهٔ اسمبلی‌ها تکمیل/اثبات نشده | تکمیل allow-list و اثبات با artifact واقعی | GLM (اسکریپت‌ها) + مالک (تأیید فهرست) | خروجی publish واقعی Linux/Windows با فهرست نهایی | تست fixture موجود (`scripts/tests/publish-content-guard.test.sh`) + اجرای publish واقعی | تکمیل فهرست + راستی‌آزمایی artifact | بله (محلی/CI) |
| G3 | اسکن اسرار در CI | **انجام‌شده** — Gitleaks فعال و موفق | نگهداری | مالک مخزن | اجرای سبز در هر PR | خود CI | نیست | بله |
| G4 | CODEOWNERS برای `Vendor/**` و `Licensing.Contracts/**` | **ناقص** — فایل اضافه شده؛ «Require review from Code Owners» قابل فعال‌سازی نیست چون reviewer دوم واجد شرایط وجود ندارد | تنظیم Branch protection با مالک | **مالک مخزن** | screenshot/API تنظیمات protection | یک PR آزمایشی باید بدون review Code Owner merge نشود | نبود reviewer دوم؛ تنظیمات از عامل قابل تغییر نیست | **خیر** — فقط مالک |
| G5 | Workflow استقرار جدا با فیلتر مسیر؛ **امضای لایسنس هرگز در CI** | **ناقص** — vendor-ci.yml مسیر-محدود موجود؛ deploy واقعی انتخاب/پیکربندی نشده | انتخاب محیط استقرار Vendor + key غیر-CI | **مالک** (تصمیم) + GLM (پیاده‌سازی پس از تصمیم) | تنظیمات محیط + artifact جدا | smoke test استقرار | تصمیم استقرار هنوز گرفته نشده | **خیر** — تصمیم مالک |
| G6 | Solution جدا (`Vendor.sln`) | **انجام‌شده** در سطح ساختار؛ معیار D-041 «جداسازی کامل استقرار Production-verified» برقرار نیست | زنجیرهٔ کامل G1..G5 | — | CI سبز `Vendor.sln` (موجود) | تست رفت‌وبرگشت صدور←اعتبارسنجی (سبز) | تابع G2/G4/G5 | بخشاً |

## ۲) Gate دوکلیدی صدور Provisioning v5

- **سازوکار:** `VendorProvisioningCompanyReferenceGate` = تنظیم `Provisioning:CompanyReferenceIssuance:Enabled` (پیش‌فرض خاموش — کلید اول) **و** interlock کدی `CustomerConsumerReady=false` (`VendorProvisioningCompanyReferenceGate.cs:22` — کلید دوم)؛ شرط امضا با PKCS#11/HSM طبق D-055 با `Signing:Pkcs11:Enabled` به‌عنوان پیش‌نیاز اولیه در همان کلاس مستند شده است.
- **وضعیت:** هر دو کلید بسته‌اند؛ هیچ بستهٔ v5 عملیاتی صادر نمی‌شود (LICENSING.md هدر وضعیت).
- **پیش‌نیاز باز شدن (فقط مالک):** تکمیل زنجیرهٔ #392 (mapping/bootstrap و migration مشتری)، trust مشتری و ACK (#235)، تست Staging دو میزبان، و تصمیم صریح مالک. تست interlock موجود (`VendorEntitlementEnforcementGuardTests`) باید با باز شدن گیت هم سبز بماند و هیچ فلگی توسط عامل تغییر نکند.

## ۳) موارد زیرساختی و قراردادی

| مورد | وضعیت فعلی | پیش‌نیاز | مسئول | شواهد لازم | تست تأیید | مانع | قابل انجام در محیط فعلی؟ |
|---|---|---|---|---|---|---|---|
| Customer Migration (#392) | Migration `20261116070805_AddCompanySetupRecords` فقط در مخزن؛ روی DB واقعی مشتری اجرا نشده؛ Mapping/Company bootstrap متعلق به #392 | باز شدن gate دوکلیدی + نسخهٔ پشتیبان + پنجرهٔ نگهداری | **مالک** + DBA | log اجرای migration + snapshot قبل/بعد | تست رفت‌وبرگشت روی staging دیتابیس واقعی | اجرا روی DB واقعی + تصمیم #392 | **خیر** |
| Key Ceremony (#236) | runbookهای CNG (rotation) و HSM/TPM موجود؛ Key Ceremony واقعی، ACL هویت سرویس، rotation/overlap و recovery روی میزبان Production انجام نشده؛ KMS/HSM عملیاتی یا credential وجود ندارد | میزبان Windows Server هدف + دو نفر حاضر + ثبت شواهد | **مالک** (اجرای runbook) | صورت‌جلسهٔ ceremony + اثرانگشت کلید عمومی + audit | health endpoint و تست امضا روی میزبان واقعی | زیرساخت و حضور انسانی | **خیر** |
| #107 — sink ممیزی Production | قرارداد checkpoint و startup guard fail-closed و file store فقط Development/Test؛ sink مستقل Production با failure domain/identity، retention/immutability، alerting و recovery drill provision نشده؛ runbook بازیابی موجود | تصمیم زیرساخت sink + provision آن | **مالک** | پیکربندی sink + تست alerting | drill بازیابی طبق runbook `vendor-audit-checkpoint-recovery.md` | زیرساخت/تصمیم مالک | **خیر** |
| Trust #235 — توزیع/چرخش کلیدهای اعتماد مشتری | توزیع/چرخش trust، سخت‌سازی consumer/ACK مشتری و تست Staging دو میزبان باقی‌اند | قرارداد توزیع (خروجی S-B) + میزبان Staging | GLM (پیاده‌سازی) + **مالک** (تصمیم قرارداد) | تست دو میزبان سبز + runbook چرخش | سناریوی rotation/compromise روی staging | قرارداد + زیرساخت staging | بخشاً (کد ممکن، قرارداد باز) |
| OD-27 — نصب فاقد TPM | `NeedsApproval`؛ D-057 fail-closed باقی است؛ هیچ software anchor معادل TPM معرفی نشود | تصمیم مالک روی گزینه‌های OD-27 | **مالک** | ADR جایگزین در صورت پذیرش | تست anti-rollback در مسیر انتخابی | تصمیم باز | **خیر** (فقط تصمیم) |
| #391/#401 — enforcement عملیاتی | گیت state-only opt-in؛ enforcement شرکت‌محور پیاده نشده (Inventory S-D: PR #757) | تأیید طرح E1..E4 S-D | GLM پس از تأیید | تست‌های HTTP ماتریس state×mode | تست منفی fail-closed | تأیید مالک + برش‌بندی | بله (پس از تأیید) |

## ۴) قاعدهٔ پایانی

باز کردن هر گیت = تصمیم مالک + شواهد اجرا شده + تست تأیید؛ **هیچ آیتم این چک‌لیست با «تکمیل مستندات» بسته نمی‌شود.** این سند هیچ وضعیتAccepted/Verified جدیدی ایجاد نمی‌کند و با هر تغییر واقعی باید از طریق PR مستنداتی به‌روز شود.
