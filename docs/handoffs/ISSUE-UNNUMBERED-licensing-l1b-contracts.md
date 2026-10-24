# تحویل — L1-B Licensing.Contracts

- **تاریخ:** 2026-10-24
- **شاخه:** `feature/licensing-l1b-contracts`
- **مبنا:** `main` @ `8879b149ddb8f1f47814db24e7328e90884f1113` (پس از Merge PR #72 L1-A)

## انجام‌شده
- پروژه `Licensing.Contracts` بدون وابستگی به ASP.NET / EF / Server
- انواع: ActivationRequest، LicensePayload، LicenseEnvelope، LicenseLimits، LicenseStatus
- Canonical JSON پایدار برای امضا
- اعتبارسنجی ساختاری
- `EcdsaP256Sha256Verifier` (**موقت** طبق پیشنهاد OD-12؛ Accepted نیست)
- `LicenseEvaluator` با `TimeProvider`
- ۷ آزمون: round-trip، دستکاری، کلید غلط، انتقال وضعیت، نصب ناسازگار، درخواست نامعتبر، پایداری JSON

## تصمیم‌های باز (تغییر وضعیت نداده‌ایم)
- OD-01، OD-02، OD-12 همچنان Proposed
- D-024 همچنان Proposed

## تست محلی
`dotnet test` روی `tests/Licensing.Contracts.Tests` → **7 passed** (SDK 10.0.402 Linux)

## خارج از محدوده این PR
Vendor Host، DB مرکزی، API صدور، یکپارچه‌سازی Server، کلید تولیدی

## گام بعد
پس از تأیید مالک روی OD-01/02/12: تثبیت الگوریتم، افزودن به Template.sln در صورت نیاز CI، سپس L2 Vendor.
