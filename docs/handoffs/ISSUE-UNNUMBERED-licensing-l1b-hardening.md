# تحویل — L1-B Hardening

- **تاریخ:** 2026-10-24
- **شاخه:** `security/licensing-l1b-hardening`
- **مبنا:** `main` @ `75b331a00307a7823cdb9ab5d6567794e9f99536` (Merge PR #73)

## ایرادهای رفع‌شده
1. نبود مسیر واحد اعتبارسنجی → `LicenseEnvelopeValidator`
2. kid بدون trust store → `ITrustedKeyResolver` / `StaticTrustedKeyResolver`
3. Options عمومی و قابل تغییر → private
4. بدون سقف اندازه → `LicenseInputLimits`
5. perpetual با expiry مبهم → رد ساختاری
6. افشای پیام CryptographicException → error codes پایدار
7. ارزیابی دادهٔ امضانشده → pipeline فقط از PayloadJson امضاشده Parse می‌کند

## تست
13 تست در EnvelopeValidatorTests — Passed روی Linux SDK 10.0.402 (اجرا با `dotnet exec` MTP).
Windows: NotVerified.
CI: NotVerified.

## تصمیم‌های باز (بدون تغییر)
OD-01, OD-02, OD-07, OD-11, OD-12, D-024
