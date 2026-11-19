# راهنمای اجرا و تست فرانو روی نسخه‌های سازگار Windows (Localhost)

> **نسخه:** 2026-11-19 | **مبنای کد:** `main@efaaa1c5` + PRهای #720 (Signer D-107)، #721 (دروازهٔ Runtime #396)، #722 (پرداخت D-108)
> این راهنما مسیر تست محلی را روی Windowsهای سازگار پوشش می‌دهد؛ نه فقط Windows 11. برای امضای License و Provisioning در این سناریو، TPM/HSM/KMS یا سخت‌افزار اختصاصی لازم نیست؛ Windows CNG از Software Key Storage Provider و کلید نرم‌افزاری non-exportable استفاده می‌کند.
> `localhost` فقط «آدرس دسترسی» است و هیچ‌گاه شناسهٔ مالکیت/Tenant/Installation تلقی نمی‌شود.
>
> **ماتریس سازگاری مبنا:**
> - Windows 11 نسخه‌های پشتیبانی‌شده توسط .NET 10، در x64 و ARM64.
> - Windows 10 فقط نسخه‌ها/Editionهایی که .NET 10 پشتیبانی می‌کند (در حال حاضر Enterprise/LTSC؛ Windows 10 Home و Pro در ماتریس پشتیبانی .NET 10 نیستند).
> - Windows Server 2016، 2019، 2022 و 2025 در x64؛ Windows Server Core نیز برای اجرای Host قابل استفاده است.
> - این سناریو SQL Server 2022 را محلی نصب می‌کند؛ SQL Server 2022 به Windows 10 1607+ یا Windows Server 2016+ و پردازنده x64 نیاز دارد. روی Windows ARM64، SQL Server را روی میزبان x64 جداگانه اجرا کنید.
> - Windows 7 و Windows 8.1 برای .NET 10 پشتیبانی نمی‌شوند. ترکیب OS، نسخه .NET و SQL Server باید در زمان استقرار با ماتریس رسمی Microsoft تطبیق داده شود.
>
> **تفکیک Build و Runtime:** Node.js فقط برای Build فایل‌های React لازم است؛ برای اجرای روزمرهٔ برنامه روی سرور لازم نیست. Build را می‌توان روی همان Windows یا در CI انجام داد و فایل‌های استاتیک خروجی را همراه Host منتشر کرد. در Windows Server Core، UI را از مرورگر یک رایانهٔ دیگر باز کنید؛ دستورهای localhost در این راهنما برای اجرای یک‌ماشینه/توسعه هستند.
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

این راهنما با PowerShell 5.1 و PowerShell 7 قابل اجرا نوشته شده است. فرمان‌های نصب زیر نمونه‌اند؛ اگر `winget` روی Windows Server/Core در دسترس نیست، نصب‌کننده‌های رسمی Microsoft را دستی اجرا کنید.

```powershell
# .NET 10 SDK برای Build و EF tooling؛ نسخه با global.json سازگار باشد
winget install Microsoft.DotNet.SDK.10

# SQL Server 2022 Developer یا Express؛ نصب محلی روی x64
winget install Microsoft.SQLServer.2022.Developer

# Node.js فقط برای Build React؛ package.json حداقل 22.12 را می‌خواهد
winget install OpenJS.NodeJS.LTS

dotnet --version          # باید 10.x باشد
node --version            # باید >= 22.12 باشد
npm --version
sqlcmd -?
```

برای اجرای برنامه پس از Build، Node.js و npm لازم نیستند. اگر `sqlcmd` نبود، از SSMS یا ماژول `SqlServer` استفاده کنید (`Install-Module SqlServer -Scope CurrentUser`). برای ابزار Migration مربوط به Vendor نیز EF CLI نسخهٔ 10.0.14 را نصب کنید:

```powershell
dotnet tool install --global dotnet-ef --version 10.0.14
```

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

> کلید خصوصی غیرقابل‌Export در `Cert:\LocalMachine\My` نگهداری می‌شود. Provider صریحاً `Microsoft Software Key Storage Provider` است؛ بنابراین کلید به TPM/HSM یا کارت هوشمند وابسته نیست. این مرحله برای نوشتن در LocalMachine نیاز به PowerShell **ادمین** دارد.

```powershell
# PowerShell (Run as Administrator)
$licenseCert = New-SelfSignedCertificate `
  -Subject "CN=Faranou Vendor License Signing v1" `
  -KeyAlgorithm ECDSA_nistP256 -Provider "Microsoft Software Key Storage Provider" -KeyExportPolicy NonExportable `
  -KeyUsage DigitalSignature -HashAlgorithm SHA256 `
  -NotAfter (Get-Date).AddYears(2) `
  -CertStoreLocation Cert:\LocalMachine\My

$provCert = New-SelfSignedCertificate `
  -Subject "CN=Faranou Vendor Provisioning Signing v1" `
  -KeyAlgorithm ECDSA_nistP256 -Provider "Microsoft Software Key Storage Provider" -KeyExportPolicy NonExportable `
  -KeyUsage DigitalSignature -HashAlgorithm SHA256 `
  -NotAfter (Get-Date).AddYears(2) `
  -CertStoreLocation Cert:\LocalMachine\My

"LicenseThumbprint  = $($licenseCert.Thumbprint)"
"ProvisionThumbprint= $($provCert.Thumbprint)"
# این دو مقدار را جایی ثبت کنید؛ در مرحلهٔ ۴ لازم‌اند.
```

> دسترسی کلید خصوصی را فقط به حساب Windows اجراکنندهٔ Vendor و `SYSTEM`/مدیران موردنیاز محدود کنید. در Windows دارای Desktop از `certlm.msc → Personal → Certificates → All Tasks → Manage Private Keys` استفاده کنید؛ در Server Core، MMC را از یک سیستم مدیریتی به‌صورت Remote متصل کنید یا از روش مدیریت Certificate Store سازمان استفاده کنید. در تست محلی، Vendor را با همان حسابی اجرا کنید که مجوز استفاده از کلید را دارد. ریسک پذیرفته‌شدهٔ D-107 پابرجاست: مدیر محلی ممکن است بدون استخراج کلید، از آن برای امضا استفاده کند.

---

## ۴) اجرای Vendor در حالت محلی

```powershell
cd C:\FaranouLocalTest\Accounting

# (یک‌بار) Build فایل‌های استاتیک Vendor؛ خروجی مستقیماً در Vendor.Host/wwwroot قرار می‌گیرد
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

# DLL باید پیش از فرمان export-public ساخته شده باشد؛ فرمان Export خودش Build انجام نمی‌دهد
dotnet build Vendor\Vendor.Host\Vendor.Host.csproj --configuration Debug

# استخراج کلید عمومی برای Trust Store — فقط SPKI عمومی چاپ می‌شود
dotnet Vendor\Vendor.Host\bin\Debug\net10.0\Faranou.Vendor.Host.dll --signing-certificate export-public
```

خروجی JSON را نگه دارید؛ `publicKeySubjectPublicKeyInfoBase64` هر کلید باید در Trust Store همان هدف ثبت شود:

```powershell
$env:FARANO_VENDOR_Licensing__TrustedSigningKeys__vendor-license-v1__PublicKeySubjectPublicKeyInfoBase64 = "<SPKI-License>"
$env:FARANO_VENDOR_ProvisioningSigningTrust__Keys__vendor-provisioning-v1__PublicKeySubjectPublicKeyInfoBase64 = "<SPKI-Provisioning>"

# اجرای Vendor روی 5100
dotnet run --project Vendor\Vendor.Host -- --urls http://localhost:5100
```

پیش از ساخت مدیر اول، دیتابیس Vendor باید Migrationهای خودش را دریافت کند. Vendor در Startup به‌صورت خودکار Migration اجرا نمی‌کند؛ این دستور را فقط روی دیتابیس آزمایشی همین راهنما اجرا کنید:

```powershell
$env:FARANO_VENDOR_ConnectionStrings__VendorDb = "Server=localhost;Database=FaranoVendor_Local;Trusted_Connection=True;TrustServerCertificate=True;"
dotnet ef database update --project Vendor\Vendor.Persistence\Vendor.Persistence.csproj --startup-project Vendor\Vendor.Host\Vendor.Host.csproj
```

در پنجرهٔ Vendor، مدیر اول را بسازید. Bootstrap به راز تصادفی یک‌بارهٔ حداقل ۳۲ بایت نیاز دارد؛ مقدار زیر فقط در Environment همان پنجره نگهداری می‌شود:

```powershell
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$secretBytes = New-Object byte[] 32
$rng.GetBytes($secretBytes)
$rng.Dispose()
$env:FARANO_VENDOR_Bootstrap__Secret = [Convert]::ToBase64String($secretBytes)
Remove-Variable secretBytes

dotnet Vendor\Vendor.Host\bin\Debug\net10.0\Faranou.Vendor.Host.dll bootstrap-admin
```

در فرمان تعاملی، راز bootstrap را از مقدار Environment وارد کنید و سپس مشخصات مدیر و گذرواژه را وارد کنید. بعد از اتمام موفق، راز موقت را پاک کنید:

```powershell
Remove-Item Env:FARANO_VENDOR_Bootstrap__Secret
```

سپس در `http://localhost:5100` با مدیر اول و MFA/TOTP وارد شوید و مشتری/نصب/قرارداد/لایسنس را تا وضعیت `Draft` بسازید.

**محدودیت واقعی Maker-Checker:** کد فعلی `bootstrap-admin` فقط مدیر اول را می‌سازد و API/UI عمومی برای ایجاد مدیر دوم در اختیار این راهنما نیست؛ API مدیریت Role هم API ایجاد کاربر نیست. بنابراین صدور نهایی که نیازمند تأیید مدیر دوم مستقل است، با نصب تازه از صفر در وضعیت فعلی قابل تکمیل نیست. برای دورزدن این محدودیت، مدیر دوم را مستقیم در SQL نسازید و Maker-Checker را غیرفعال نکنید؛ این مسیر تا ارائهٔ گردش‌کار امن ایجاد مدیر دوم، Blocked است.

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
try {
  Invoke-RestMethod http://localhost:5000/api/v1/session -ErrorAction Stop | Out-Null
  throw "انتظار 503 وجود داشت اما درخواست موفق شد."
}
catch {
  $response = $_.Exception.Response
  if ($null -eq $response -or [int]$response.StatusCode -ne 503) { throw }
  $reader = New-Object System.IO.StreamReader($response.GetResponseStream())
  try { $errorBody = $reader.ReadToEnd() } finally { $reader.Dispose() }
  $errorBody
  # انتظار: 503 با error=license_required
}
```

---

## ۶) فعال‌سازی: انتقال License امضاشده به فرانو (مسیر فایل/Import)

مسیر فعلی کد همان import پایدار است (D-058): Envelope امضاشده یک‌بار وارد و ذخیره می‌شود؛ Restart نیاز به فعال‌سازی مجدد ندارد.

```powershell
# 1) خواندن آخرین Envelope امضاشده از Vendor DB و ذخیره به‌عنوان فایل
$vendorConn = "Server=localhost;Database=FaranoVendor_Local;Trusted_Connection=True;TrustServerCertificate=True;"
$envelope = Invoke-Sqlcmd -ConnectionString $vendorConn -Query @"
SELECT TOP 1 e.EnvelopeJson
FROM VendorSignedLicenseEnvelopes e
JOIN VendorLicenses l ON l.Id = e.LicenseId
WHERE l.Status = N'Issued'
ORDER BY e.CreatedAtUtc DESC;
"@
if ($null -eq $envelope -or [string]::IsNullOrWhiteSpace([string]$envelope.EnvelopeJson)) {
  throw "هیچ Envelope صادرشده‌ای پیدا نشد؛ تا زمان تکمیل Maker-Checker، این مرحله قابل انجام نیست."
}
# بدون BOM؛ برای سازگاری PowerShell 5.1 و JSON parser
[System.IO.File]::WriteAllText(
  'C:\FaranouLocalTest\license.json',
  [string]$envelope.EnvelopeJson,
  (New-Object System.Text.UTF8Encoding($false)))
```

> برای import، ابتدا باید Provisioning v4 از مسیر موجود اعمال شود و حساب Infrastructure ایجاد/Enroll شود. صفحهٔ Provisioning مشتری در کد React توکن ضدجعل را از `GET /api/v1/antiforgery/token` می‌گیرد و از Cookie همان مرورگر استفاده می‌کند؛ این صفحه یک API مستقل CLI نیست. این راهنما عمداً `$session` و `$token` تعریف‌نشده را به‌عنوان فرمان PowerShell ارائه نمی‌کند. ورود تعاملی و Import License از API نیازمند Cookie نشست معتبر و مقدار واقعی `requestToken`/`headerName` همان نشست است؛ تا زمانی که یک مسیر UI کامل یا اسکریپت ورود/CSRF تست‌شده ارائه نشده، این بخش را به‌عنوان گام دستیِ نیازمند احراز هویت در نظر بگیرید و از ارسال درخواست با Session خالی خودداری کنید.

```powershell
# فقط پس از تکمیل Provisioning، ورود Infrastructure و داشتن نشست معتبر اجرا شود.
# این قطعه عمداً فرمان import نیست؛ قرارداد دقیق Body/نشست باید از API جاری و با همان Cookie احراز‌شده گرفته شود.
# بررسی وضعیت بدون تغییر داده:
Invoke-RestMethod http://localhost:5000/api/v1/licensing/status | ConvertTo-Json -Depth 6
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
5. ساخت Draft و بررسی Signer — بند ۴؛ صدور نهایی فعلاً Blocked به‌دلیل نبود گردش‌کار ایجاد مدیر دوم
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
- **کد فعال‌سازی یک‌بارمصرف Vendor و گردش‌کار reactivation (D-099)** پیاده نشده‌اند؛ بازیابی هویت نصب با `--license-installation-key` و مسیر `installation-identity/enroll` انجام می‌شود.
- **صدور Provisioning v5 (شرکت‌محور) پشت Gate دوکلیدی بستهٔ مصوب است**؛ در این تست بستهٔ v4 استفاده می‌شود.
- Concurrent-session cap (D-100)، Update با تأیید مدیر (D-101) و CRM سبک (D-102) NotImplemented هستند.
- گردش‌کار امن ایجاد مدیر دوم Vendor در کد فعلی موجود نیست؛ صدور نهایی Maker-Checker در نصب تازه Blocked است و نباید با SQL یا خاموش‌کردن کنترل دور زده شود.
- این راهنما مسیر License/Provisioning مرکزی را بدون TPM/HSM/KMS پوشش می‌دهد؛ امضای Vendor offline-revocation snapshot همچنان طبق D-055 و anti-rollback ابطال آفلاین مشتری طبق D-057 تصمیم/الزام جداگانه دارند و این راهنما آن‌ها را حذف یا دور نمی‌زند.
- تست این راهنما روی ماتریس واقعی Windows هنوز توسط مالک اجرا نشده؛ اجرای محلی Agent (Linux) فقط Build/تست‌های خودکار را پوشش داده است. اجرای CNG فقط روی Windows ممکن است.
