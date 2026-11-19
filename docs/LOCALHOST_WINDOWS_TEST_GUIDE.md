# راهنمای اجرا و تست کامل فرانو روی Windows شخصی (Localhost)

> **نسخه:** 2026-11-19 | **مبنای کد:** `main@efaaa1c5` + PRهای #720 (Signer D-107)، #721 (دروازهٔ Runtime #396)، #722 (پرداخت D-108)
> این راهنما یک مسیر تست **واقعی و تکرارپذیر** برای Windows شخصی می‌سازد؛ بدون Domain عمومی، بدون سرور ابری، بدون اینترنت دائمی و بدون TPM/HSM.
> `localhost` فقط «آدرس دسترسی» است و هیچ‌گاه شناسهٔ مالکیت/Tenant/Installation تلقی نمی‌شود.
>
> **پیش‌نیاز منطقی:** PR #720 باید در `main` ادغام شده باشد (Provider امضای Windows CNG طبق D-107). تست‌های داخلی مخزن از کلید آزمایشی استفاده می‌کنند که هرگز وارد Runtime/Production نمی‌شود؛ در این راهنما کلید واقعیِ محلیِ خودتان ساخته می‌شود.

---

## ۰) معماری همین تست

| نقش | اجرا روی | پورت | دیتابیس محلی |
|---|---|---|---|
| Vendor Control Plane | همان Windows | `http://localhost:5100` | `FaranoVendor_Local` |
| فرانو (Customer Host) | همان Windows | `http://localhost:5000` | `Farano_Local` |
| SQL Server | همان Windows | پیش‌فرض `localhost` | — |

- Vendor و فرانو هرگز به دیتابیس هم وصل نمی‌شوند (D-047/D-051).
- امضای License/Provisioning با Windows CNG و کلید غیرقابل‌Export انجام می‌شود (`AssuranceLevel=SoftwareProtected` طبق D-107).
- فعال‌سازی با import فایل License امضاشده انجام می‌شود (مسیر فعلی در کد؛ Wizard گرافیکی بعداً). Provisioning v4 به‌صورت آنلاین/آفلاین کامل است؛ صدور v5 فعلاً پشت Gate بستهٔ مصوب است.

---

## ۱) پیش‌نیازها (یک‌بار)

```powershell
# .NET SDK 10 (با global.json هماهنگ)
winget install Microsoft.DotNet.SDK.10

# SQL Server 2022 Developer یا Express + SSMS اختیاری
winget install Microsoft.SQLServer.2022.Developer

# Node.js 24 (فقط برای Build کلاینت React)
winget install OpenJS.NodeJS.LTS

dotnet --version          # باید 10.x باشد
sqlcmd -?                 # ابزار خط فرمان SQL
```

اگر `sqlcmd` نبود: از SSMS یا `Invoke-Sqlcmd` ماژول `SqlServer` استفاده کنید (`Install-Module SqlServer -Scope CurrentUser`).

```powershell
# پوشهٔ کاری تست
mkdir C:\FaranouLocalTest | Out-Null
cd C:\FaranouLocalTest
git clone https://github.com/hamidreza1394/Accounting.git
cd Accounting
```

---

## ۲) ساخت دو دیتابیس محلی

```powershell
sqlcmd -S localhost -E -Q "IF DB_ID('FaranoVendor_Local') IS NULL CREATE DATABASE FaranoVendor_Local;"
sqlcmd -S localhost -E -Q "IF DB_ID('Farano_Local') IS NULL CREATE DATABASE Farano_Local;"
```

اگر از SQL Auth استفاده می‌کنید، `-E` را با `-U sa -P '<رمز>'` جایگزین کنید و همهٔ رشته‌های اتصال پایین را هم‌سان کنید. **هیچ رمزی در فایل داخل مخزن ذخیره نشود.**

---

## ۳) Key Ceremony — کلید امضای Vendor با Windows CNG (D-107)

> کلید خصوصی غیرقابل‌Export، در `Cert:\LocalMachine\My` (MachineKey). این بخش نیاز به PowerShell **ادمین** دارد.

```powershell
# PowerShell (Run as Administrator)
$licenseCert = New-SelfSignedCertificate `
  -Subject "CN=Faranou Vendor License Signing v1" `
  -KeyAlgorithm ECDSA_nistP256 -KeyExportPolicy NonExportable `
  -KeyUsage DigitalSignature -HashAlgorithm SHA256 `
  -NotAfter (Get-Date).AddYears(2) `
  -CertStoreLocation Cert:\LocalMachine\My

$provCert = New-SelfSignedCertificate `
  -Subject "CN=Faranou Vendor Provisioning Signing v1" `
  -KeyAlgorithm ECDSA_nistP256 -KeyExportPolicy NonExportable `
  -KeyUsage DigitalSignature -HashAlgorithm SHA256 `
  -NotAfter (Get-Date).AddYears(2) `
  -CertStoreLocation Cert:\LocalMachine\My

"LicenseThumbprint  = $($licenseCert.Thumbprint)"
"ProvisionThumbprint= $($provCert.Thumbprint)"
# این دو مقدار را جایی ثبت کنید؛ در مرحلهٔ ۴ لازم‌اند.
```

> برای محدودکردن استفاده به هویت سرویس (مثلاً حسابی که Vendor را اجرا می‌کند) از `Set-Permission` روی کلید خصوصی یا `certlm.msc → All Tasks → Manage Private Keys` استفاده کنید. ریسک پذیرفته‌شدهٔ D-107 را به خاطر داشته باشید: مدیر محلی می‌تواند بدون استخراج کلید امضا ایجاد کند؛ میزبان باید اختصاصی و محدود باشد.

---

## ۴) اجرای Vendor در حالت محلی

```powershell
cd C:\FaranouLocalTest\Accounting

# (یک‌بار) ساخت کلاینت Vendor
cd Vendor\Vendor.Web
npm ci
npm run build
cd ..\..\

$env:FARANO_VENDOR_ConnectionStrings__VendorDb = "Server=localhost;Database=FaranoVendor_Local;Trusted_Connection=True;TrustServerCertificate=True;"
$env:FARANO_VENDOR_DataProtection__KeysDirectory = "C:\FaranouLocalTest\vendor-dp-keys"

# پیکربندی Signer ویندوز CNG (D-107) — Thumbprintها را از مرحلهٔ ۳ بگذارید
$env:FARANO_VENDOR_Signing__CertificateStore__Enabled = "true"
$env:FARANO_VENDOR_Signing__CertificateStore__AssuranceLevel = "SoftwareProtected"
$env:FARANO_VENDOR_Signing__CertificateStore__StoreLocation = "LocalMachine"
$env:FARANO_VENDOR_Signing__CertificateStore__StoreName = "My"
$env:FARANO_VENDOR_Signing__CertificateStore__LicenseKey__KeyId = "vendor-license-v1"
$env:FARANO_VENDOR_Signing__CertificateStore__LicenseKey__Thumbprint = "<LicenseThumbprint>"
$env:FARANO_VENDOR_Signing__CertificateStore__ProvisioningKey__KeyId = "vendor-provisioning-v1"
$env:FARANO_VENDOR_Signing__CertificateStore__ProvisioningKey__Thumbprint = "<ProvisionThumbprint>"

# (یک‌بار) استخراج کلید عمومی برای Trust Store — فقط SPKI عمومی چاپ می‌شود
dotnet Vendor\Vendor.Host\bin\Debug\net10.0\Faranou.Vendor.Host.dll --signing-certificate export-public
```

خروجی JSON را نگه دارید؛ `publicKeySubjectPublicKeyInfoBase64` هر کلید باید در Trust Store همان هدف ثبت شود:

```powershell
$env:FARANO_VENDOR_Licensing__TrustedSigningKeys__vendor-license-v1__PublicKeySubjectPublicKeyInfoBase64 = "<SPKI-License>"
$env:FARANO_VENDOR_ProvisioningSigningTrust__Keys__vendor-provisioning-v1__PublicKeySubjectPublicKeyInfoBase64 = "<SPKI-Provisioning>"

# اجرای Vendor روی 5100
dotnet run --project Vendor\Vendor.Host -- --urls http://localhost:5100
```

در پنجرهٔ دیگر، مدیر اول را بسازید (تعاملی؛ رمز در این فرمان تایپ می‌شود و در هیچ جا ذخیره نمی‌شود):

```powershell
$env:FARANO_VENDOR_ConnectionStrings__VendorDb = "Server=localhost;Database=FaranoVendor_Local;Trusted_Connection=True;TrustServerCertificate=True;"
dotnet Vendor\Vendor.Host\bin\Debug\net10.0\Faranou.Vendor.Host.dll bootstrap-admin
```

سپس در `http://localhost:5100` با همان مدیر (MFA/TOTP) وارد شوید و از بخش‌های UI: **مشتری تجاری → نصب → قرارداد → لایسنس** را بسازید. لایسنس را `Draft` نگه دارید. برای صدور، درخواست approval و **تأیید با مدیر دومِ متمایز** لازم است (Maker-Checker؛ خودِ درخواست‌کننده نمی‌تواند تأیید کند).

> اگر به‌جای صدور، خطای `license_issuance_unavailable` دیدید یعنی Signer/Trust Store کامل پیکربندی نشده — این همان رفتار fail-closed صحیح است؛ بند ۴ را بازبینی کنید.

---

## ۵) اجرای فرانو روی localhost:5000

```powershell
cd C:\FaranouLocalTest\Accounting

# (یک‌بار) Build کلاینت React
cd ClientApp
npm ci
npm run build
cd ..

$env:ConnectionStrings__ConnectionString = "Server=localhost;Database=Farano_Local;Trusted_Connection=True;TrustServerCertificate=True;"
$env:Farano__Settings__Directory = "C:\FaranouLocalTest\farano-settings"

# Migration صریح (بازبینی‌شده؛ خودکار روی دیتابیس واقعی اجرا نمی‌شود)
dotnet run --project Server -- --migrate

# فعال‌سازی دروازهٔ License (تعیین رفتار Active/Restricted/Unlicensed)
$env:Licensing__RuntimeGate__Enabled = "true"
$env:Licensing__DeploymentMode = "Dedicated"   # در سناریوی Cloud: "Cloud" → رفتار انقضای D-098

dotnet run --project Server -- --urls http://localhost:5000
```

بررسی وضعیت (قبل از هر فعال‌سازی باید Unlicensed باشد):

```powershell
Invoke-RestMethod http://localhost:5000/api/v1/licensing/status | ConvertTo-Json -Depth 6
# انتظار: "state": "unlicensed"
Invoke-WebRequest http://localhost:5000/api/v1/session -SkipHttpErrorCheck
# انتظار: 503 با {"error":"license_required", ...}  ← Unlicensed عملیاتی است
```

---

## ۶) فعال‌سازی: انتقال License امضاشده به فرانو (مسیر فایل/Import)

مسیر فعلی کد همان import پایدار است (D-058): Envelope امضاشده یک‌بار وارد و ذخیره می‌شود؛ Restart نیاز به فعال‌سازی مجدد ندارد.

```powershell
# 1) خواندن آخرین Envelope امضاشده از Vendor DB و ذخیره به‌عنوان فایل
$vendorConn = "Server=localhost;Database=FaranoVendor_Local;Trusted_Connection=True;TrustServerCertificate=True;"
$envelope = Invoke-Sqlcmd -ServerInstance localhost -Database FaranoVendor_Local -Query @"
SELECT TOP 1 e.EnvelopeJson
FROM VendorSignedLicenseEnvelopes e
JOIN VendorLicenses l ON l.Id = e.LicenseId
WHERE l.Status = N'Issued'
ORDER BY e.CreatedAtUtc DESC;
"@ -ConnectionString $vendorConn
$envelope.EnvelopeJson | Out-File C:\FaranouLocalTest\license.json -Encoding utf8
```

> برای import، کاربر باید حساب **Infrastructure** نصب باشد. ساده‌ترین مسیر: بستهٔ Provisioning را از Vendor (فرم Provisioning → Export → فایل JSON) بگیرید و در فرانو `/provisioning/import` اعمال کنید تا مدیر اولیه ساخته شود؛ سپس با همان حساب وارد شوید. (Enrollment یک‌بارمصرف است و رمز فقط در همین مرحله تعیین می‌شود.)

```powershell
# 2) ورود به فرانو و import لایسنس (antiforgery + Authorization Infrastructure)
# در PowerShell: ابتدا token از /api/v1/antiforgery/token و سپس:
$body = @{ envelopeJson = Get-Content C:\FaranouLocalTest\license.json -Raw } | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri http://localhost:5000/api/v1/licensing/import `
  -WebSession $session -Headers @{ "X-XSRF-TOKEN" = $token } -ContentType "application/json" -Body $body

# 3) وضعیت پس از فعال‌سازی
Invoke-RestMethod http://localhost:5000/api/v1/licensing/status | ConvertTo-Json -Depth 6
# انتظار: "state": "active" + جزئیات از payload امضاشده (kind، supportUntilUtc، companyEntitlements ...)
```

پس از این مرحله ورود عادی باز می‌شود و دسترسی‌ها بر اساس Company/Module/UserSeat همان payload اعمال می‌شوند.

---

## ۷) سناریوهای انقضا، تمدید، آفلاین و Restricted

| تست | روش کنترل‌شده | رفتار مورد انتظار |
|---|---|---|
| **انقضای Term (Dedicated)** | لایسنس `term` با `ValidUntilUtc` گذشته بسازید → approve → export envelope → import (جایگزین طبق sequence) | status `restricted`؛ GET باز، POST عادی `403 license_restricted`؛ هیچ داده‌ای حذف نمی‌شود |
| **انقضا در Cloud** | همین لایسنس منقضی با `Licensing__DeploymentMode="Cloud"` | عملیات عادی/ورود عادی بسته `403 license_cloud_expired`؛ مسیر تمدید/بازیابی باز (D-098) |
| **تمدید/Amendment** | در Vendor روی همان License تغییرات جدید → approve → Sequence جدید → envelope جدید → import | پذیرش فقط Sequence جدیدتر (`LicenseRevisionRules`)؛ replay/downgrade رد می‌شود |
| **Offline** | همان فایل `license.json` را روی دستگاه دوم/قطع‌شده کپی کنید و import کنید | اعتبارسنجی کاملاً محلی؛ قطع اینترنت به‌تنهایی انقضا نیست |
| **Support/Update مستقل** | `supportUntilUtc`/`updatesUntilUtc` را در لایسنس جدا از `expiresAtUtc` تنظیم کنید | پایان Support نرم‌افزار Perpetual را قفل نمی‌کند (D-097) |
| **Provisioning آفلاین** | Vendor: Provisioning → `package/export`؛ Customer: `/provisioning/import` + apply | ACK امضاشده (CNG) به Vendor برمی‌گردد و وضعیت Confirmed می‌شود |
| **Unlicensed** | دیتابیس `InstalledLicenses` را در یک دیتابیس تمیز آزمایش کنید | فقط `/api/v1/licensing/*` و `/api/v1/provisioning/*` باز؛ بقیه 503 |

نکته: صرف قطع اینترنت هرگز License معتبر را باطل نمی‌کند (D-046/D-059 §9)؛ انقضای Term/پایان Support/ابطالِ امضاشده مسیرهای رسمی‌اند.

---

## ۸) چک‌لیست ۱۲ مرحله‌ای مأموریت

1. نصب پیش‌نیازها — بند ۱
2. آماده‌سازی دیتابیس محلی — بند ۲
3. اجرای Vendor محلی — بند ۴
4. Customer/Installation آزمایشی — بند ۴ (UI Vendor)
5. صدور License معتبر امضاشده — بند ۴ (Maker-Checker + CNG)
6. اجرای فرانو روی localhost:5000 — بند ۵
7. ورود با License معتبر — بند ۶ (import) و سپس Login
8. بررسی وضعیت و Entitlement — `GET /api/v1/licensing/status`
9. تغییر کنترل‌شدهٔ اعتبار — بند ۷ (Term گذشته)
10. تمدید و License جدید — بند ۷ (Sequence جدید)
11. مسیر آفلاین با انتقال فایل — بند ۷
12. رفتار Restricted/Unlicensed — بند ۵ و ۷

---

## ۹) محدودیت‌های صادقانهٔ این نسخه

- **Wizard فعال‌سازی گرافیکی و صفحهٔ وضعیت React هنوز ساخته نشده‌اند**؛ فعال‌سازی با API/فایل است و UI فقط projection وضعیت است.
- **کد فعال‌سازی یک‌بارمصرف Vendor و گردش‌کار reactivation سخت‌افزاری (D-099)** پیاده نشده‌اند؛ بازیابی هویت نصب با `--license-installation-key` و مسیر `installation-identity/enroll` انجام می‌شود.
- **صدور Provisioning v5 (شرکت‌محور) پشت Gate دوکلیدی بستهٔ مصوب است**؛ در این تست بستهٔ v4 استفاده می‌شود.
- Concurrent-session cap (D-100)، Update با تأیید مدیر (D-101) و CRM سبک (D-102) NotImplemented هستند.
- تست این راهنما روی Windows واقعی توسط مالک انجام می‌شود؛ اجرای محلی Agent (Linux) فقط Build/تست‌های خودکار را پوشش داده است.
