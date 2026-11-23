# handoff به تیم Customer — چرخهٔ اول لایسنس Staging (UNNUMBERED؛ مرجع‌ها #384/#392/#396/#235/#228)

- **تهیه:** GLM (zgo/glm-5.3-flash) — 2026-11-23 — فقط مستندات؛ هیچ `Server/**`، Migration یا تنظیم Customer تغییر نکرده است.
- **هدف:** روشن‌کردن دقیق اینکه Vendor چه چیزی، از کدام endpoint و با چه قراردادی تحویل می‌دهد و Customer چه چیزهایی را باید جداگانه پیاده/اجرا/تأیید کند تا اولین چرخهٔ صدور→اعمال بسته شود.

## 1) قرارداد تحویل از Vendor (پیاده‌شده؛ PR #781 در انتظار بازبینی مالک)

| مورد | مقدار دقیق |
|---|---|
| مسیر صادرات | `GET /api/vendor/licenses/{licenseId:guid}/envelope/export` (فقط-خواندنی؛ مجوز `vendor.licenses.read` + resource authorization) |
| بدنهٔ پاسخ | دقیقاً `EnvelopeJson` ذخیره‌شده — همان `LicenseEnvelope` (`Faranou.Licensing.Contracts`): `FormatVersion=1, Kid, Algorithm="ECDSA-P256-SHA256", PayloadJson, SignatureBase64` |
| payload | `LicensePayload` v2 شرکت‌محور؛ `CompanyEntitlements` با `CompanyReference` opaque (Guid-D)، `Modules=[TRADE_IMPORT]`، `MaxUsers`؛ فاقد `Subsystems` installation-wide |
| کنترل‌های سمت Vendor | وضعیت Issued، hash ذخیره‌شده، ساختار، Trust Store `Licensing:TrustedSigningKeys`، امضا و binding به Installation؛ شکست امضا/ارزیابی با کد جدا (`license_envelope_signature_invalid` / `license_envelope_not_currently_valid`) |
| آنچه تحویل نمی‌شود | ACK/Applied tracking لایسنس (#393/#394 — تصمیم قرارداد S-B با مالک)، revocation snapshot consumer (#235)، هر مادهٔ خصوصی |

## 2) وضعیت واقعی Customer-side (بر اساس main@62b0c3c6؛ هیچ‌کدام Operationally Verified نیست)

| قابلیت | وضعیت کد | شکاف تا چرخهٔ اول |
|---|---|---|
| اعتبارسنجی پاکت (revocation-aware) + import تراکنشی | در main (`CustomerLicenseImportService`؛ PRهای #386–#388) | اجرای واقعی روی DB مشتری هرگز انجام نشده |
| enrollment هویت نصب | در main (`installation-identity/enroll`) | اجرای واقعی؛ proof و TPM در آزمون سخت‌افزار جدا است |
| reload/اعتبارسنجی مجدد در startup | Implemented برای import؛ runtime بدون cache | تست واقعی Restart با لایسنس واقعی انجام نشده |
| Runtime Gate | در main (PR #721؛ #735 wizard) — پیش‌فرض OFF | روشن‌کردن فقط در Staging با تأیید مالک + مسیر recovery؛ E2E با لایسنس واقعی NotRun |
| نگاشت CompanyReference→Company | Schema در مخزن (`CompanySetupRecords`، #684/#686) | **Migration روی DB واقعی اجرا نشده**؛ بدون آن، entitlement شرکت‌محور قابل اعمال نیست |
| Trust Store لایسنس | `Licensing:TrustedLicenseKeys` (config) | ثبت SPKI واقعی؛ توزیع/rotation عملیاتی در #235 باز است |
| entitlement enforcement call-siteها | `Proposed` (S-D؛ `ENTITLEMENT_ENFORCEMENT_CALLSITE_INVENTORY.md`) | برش E1..E4 نیازمند مأموریت Customer مستقل |

## 3) چیزهایی که Customer باید از Vendor دریافت کند و نباید بسازد

- SPKI عمومی کلید امضا (از خروجی `--signing-certificate export-public`؛ ثبت در `Licensing:TrustedLicenseKeys`).
- فایل `license-envelope.json` از endpoint صادرات — بدون هیچ ویرایش؛ کلید خصوصی هرگز و از هیچ مسیری منتقل نمی‌شود.
- هیچ اتصال مستقیم به Vendor DB، هیچ endpoint یا جدول مشترک جدید — لایسنس فقط از مسیر فایل/قراردادهای نسخه‌دار.

## 4) معیار پذیرش چرخهٔ اول (شواهد الزامی)

1. import موفق پاکت امضاشده روی DB واقعی مشتری + ثبت پایدار (idempotent در تکرار).
2. رد fail-closed: امضای دستکاری‌شده، kid ناشناخته، InstallationId اشتباه، replay، منقضی.
3. `GET /api/v1/licensing/status` فقط از payload امضاشده؛ حالت‌های Valid/Restricted درست نمایش داده شود.
4. Restart سرویس بدون import مجدد؛ Gate روشن در Staging فقط با تأیید مالک.
5. نگاشت CompanyReference ثبت‌شده و Company دوم هیچ دسترسی به entitlement شرکت اول ندارد (تست A/B).
6. Trust Store مستقل از پاکت باقی بماند (پاکت حامل اعتماد خودش نیست).

## 5) مراجع

`docs/runbooks/FIRST-STAGING-LICENSE-CYCLE.md`؛ `docs/handoffs/ISSUE-384-customer-license-import.md`؛ `docs/handoffs/ISSUE-396-license-runtime-gate.md`؛ `docs/ENTITLEMENT_ENFORCEMENT_CALLSITE_INVENTORY.md`؛ `docs/AMENDMENT_ACK_CONTRACT_ANALYSIS.md` (S-B، Proposed).
