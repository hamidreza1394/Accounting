# Runbook اولین چرخهٔ لایسنس Staging — صدور، تحویل و اعمال (Issue #228/#394/#396/#401؛ D-107)

- **دامنه:** یک Customer آزمایشی، یک Installation، یک Company، یک License با یک entitlement شرکت‌محور `TRADE_IMPORT`، دو دیتابیس مستقل Vendor/Customer. فقط Staging؛ **هیچ ادعای Production readiness ندارد**.
- **وضعیت:** تا اجرای واقعی روی میزبان Windows هدف، همهٔ گام‌ها `Not Run` است. هیچ گیت امنیتی در این Runbook باز نمی‌شود؛ گیت‌هایی که بازکردنشان تصمیم مالک است صریحاً علامت خورده‌اند.
- **پیش‌شرط قطعی Runtime Gate:** پیش از هر اجرای Customer و شروع چرخه، مقدار مؤثر `Licensing:RuntimeGate:Enabled` در پیکربندی همان سرویس Customer باید صریحاً `false` باشد؛ به default یا نبودن متغیر اتکا نکنید. اگر مقدار فعلی `true` است یا منبع مؤثر آن مشخص نیست، چرخه را شروع نکنید. مقدار را در منبع پیکربندی واقعی سرویس (Service/Service Wrapper یا Secret Store) به `false` تغییر دهید و پس از Restart، مقدار مؤثر را از همان منبع پیکربندی/تنظیمات استقرار تأیید و شاهد آن را ثبت کنید. برای اجرای تعاملی، پیش از شروع پردازه در همان PowerShell session مقدار `$env:Licensing__RuntimeGate__Enabled = "false"` را تنظیم کنید. این کار فقط Gate را خاموش نگه می‌دارد و مجوز فعال‌سازی یا اجرای بخش 7 نیست.
- **وضعیت وابستگی:** گام 5 (صادرات پاکت) با PR **#781** (`agent/license-envelope-export`) و commit `b458a09ad918c1d3fa5c38bb909e25a9d6974978` در `main` ادغام شده است؛ endpoint در کد موجود است، اما اجرای واقعی export در Staging هنوز `Not Run` است.
- **مراجع اجباری:** [Runbook Key Ceremony CNG (#236/D-107)](ISSUE-236-CNG-KEY-CEREMONY-ROTATION.md) و [LOCALHOST_WINDOWS_TEST_GUIDE.md](../LOCALHOST_WINDOWS_TEST_GUIDE.md)؛ این سند جایگزین آن‌ها نیست.

## 0) محیط هدف و قواعد ثابت

| مؤلفه | میزبانی | ممنوعیت |
|---|---|---|
| Vendor Host (API + UI) | Windows Server یا ویندوز توسعه (دسترسی Administrator برای CNG MachineKey) | اتصال به Customer DB؛ کلید خصوصی در Git/Log/بستهٔ مشتری |
| Vendor SQL Server DB | نمونهٔ مستقل | اشتراک فایل/نمونه با Customer DB |
| Customer Host (Server.dll) | **آزمایش اول:** پردازه/سرویس دوم روی همان Windows Server؛ **مرحلهٔ بعد:** Windows Server مستقل مشتری | اتصال به Vendor DB؛ دریافت هر مادهٔ خصوصی |
| Customer SQL Server DB | نمونهٔ مستقل | — |

- کل فرآیند بین دو مرز مستقل با **فایل پاکت** انجام می‌شود؛ Vendor هرگز مستقیم به Customer نمی‌نویسد.
- **توپولوژی اجرای اولیه طبق تصمیم مالک:** یک Windows Server با دو پردازه/سرویس Vendor و Customer، هویت سرویس و تنظیمات مستقل و دو دیتابیس جدا. این آزمایش استقلال سخت‌افزار/سیستم‌عامل یا دو میزبان را اثبات نمی‌کند؛ پشتیبانی و آزمون روی دو Windows Server مستقل در مرحلهٔ بعد الزامی است.
- برگهٔ ثبت شواهد و نتایج هر اجرا: [`FIRST-STAGING-LICENSE-CYCLE-EVIDENCE.md`](FIRST-STAGING-LICENSE-CYCLE-EVIDENCE.md).
- پیکربندی Vendor فقط با متغیرهای محیطی `FARANO_VENDOR_*` (User Secrets/Secret Store)؛ هیچ secret در فایل مخزن.

## 1) آماده‌سازی Vendor DB و اجرا

```powershell
# 1.1 — نصب ابزار Migration (فقط یک بار)
dotnet tool install -g dotnet-ef

# 1.2 — ساخت DB Vendor خالی (در SSMS یا sqlcmd): FaranoVendorStaging
# 1.3 — تنظیمات (PowerShell session سرویس؛ در استقرار واقعی از Secret Store/Environment سرویس):
$env:FARANO_VENDOR_ConnectionStrings__VendorDb = "Server=.;Database=FaranoVendorStaging;Trusted_Connection=True;TrustServerCertificate=True"
$env:FARANO_VENDOR_DataProtection__KeysDirectory = "D:\FaranoVendorData\dpkeys"   # Production الزامی؛ جدا و بکاپ‌گیری سرویسی
$env:FARANO_VENDOR_AllowedHosts = "staging-vendor.example.internal"               # بدون wildcard (D-041)
# FARANO_VENDOR_Bootstrap__Secret فقط برای bootstrap اولیه؛ ≥32 بایت؛ پس از bootstrap حذف شود.

# 1.4 — اعمال Migrationهای Vendor
dotnet ef database update --project Vendor/Vendor.Persistence --startup-project Vendor/Vendor.Host

# 1.5 — Vendor را هنوز اجرا نکنید؛ ابتدا §3 را کامل کنید تا Signing و Trust در محیط پردازهٔ Vendor تنظیم شده باشند.
```

- اگر DB خالی بماند یا Connection String نباشد، startup fail-closed می‌شود (رفتار صحیح).

## 2) مدیر اول Vendor

```powershell
dotnet Vendor.Host.dll bootstrap-admin   # تعاملی؛ رمز فقط در terminal؛ در تاریخچهٔ shell ذخیره نشود
```

- ورود از Vendor.Web، ثبت TOTP؛ رمز بار دوم پرسیده نمی‌شود. Bootstrap مستقل TenantOwner (D-110) `NotImplemented` است و **پیش‌نیاز این آزمایش محدود نیست** (مدیر Vendor ≠ مدیر Tenant مشتری).

## 3) کلید امضای Staging (D-107) — خلاصه؛ جزئیات در Runbook #236

1. با Administrator دو گواهی ECDSA P-256 `NonExportable` در `Cert:\LocalMachine\My` بسازید (دستورهای دقیق: Runbook #236 §3؛ Subjectهای مجزا برای License و Provisioning).
2. ACL مادهٔ خصوصی فقط برای هویت سرویس Vendor (§4 همان Runbook) — هویت کاربر تعاملی جدا است.
3. SPKI عمومی هر دو کلید را با هویت سرویس Vendor روی همان میزبان استخراج کنید (فرمان فقط-خواندنی؛ هیچ مادهٔ خصوصی چاپ نمی‌شود):

```powershell
dotnet Vendor.Host.dll --signing-certificate export-public
```

4. Trust Store و فعال‌سازی امضا (متغیرهای §5 Runbook #236). نام‌های دارای خط تیره در PowerShell باید با `${env:...}` نوشته شوند:

```powershell
$env:FARANO_VENDOR_Signing__CertificateStore__Enabled = "true"
$env:FARANO_VENDOR_Signing__CertificateStore__AssuranceLevel = "SoftwareProtected"
$env:FARANO_VENDOR_Signing__CertificateStore__StoreLocation = "LocalMachine"
$env:FARANO_VENDOR_Signing__CertificateStore__License__KeyId = "vendor-license-v1"
$env:FARANO_VENDOR_Signing__CertificateStore__License__Thumbprint = "<THUMBPRINT-LICENSE>"
$env:FARANO_VENDOR_Signing__CertificateStore__Provisioning__KeyId = "vendor-provisioning-v1"
$env:FARANO_VENDOR_Signing__CertificateStore__Provisioning__Thumbprint = "<THUMBPRINT-PROVISIONING>"
${env:FARANO_VENDOR_Licensing__TrustedSigningKeys__vendor-license-v1__PublicKeySubjectPublicKeyInfoBase64} = "<SPKI-License>"
$env:FARANO_VENDOR_ProvisioningSigningTrust__Keys__0__KeyId = "vendor-provisioning-v1"
$env:FARANO_VENDOR_ProvisioningSigningTrust__Keys__0__PublicKeySubjectPublicKeyInfoBase64 = "<SPKI-Provisioning>"
```

5. آزمون‌های پذیرش گام کلید (همه باید ثبت شوند): امضای موفق با هویت سرویس، Verify با SPKI، Restart سرویس و امضای مجدد، رد امضا با حساب غیرمجاز، رد امضا با کلید حذف‌شده (fail-closed 503).
6. **پیش از شروع Vendor:** برای اجرای تعاملی، متغیرهای بالا را در همان PowerShell session تنظیم کنید و سپس در همان session اجرا کنید؛ پردازه فقط محیط زمان شروع را به ارث می‌برد. برای Windows Service یا service wrapper، همین مقادیر را در محیط/Secret Store همان سرویس ثبت کنید و سرویس را پس از تغییر تنظیمات Restart کنید؛ تنظیمات session کاربر به سرویس در حال اجرا منتقل نمی‌شود.

```powershell
dotnet run --project Vendor/Vendor.Host
```

7. **کلید Staging از کلید Production مجزا است** (KeyId/Thumbprint/گواهی جدا).

## 4) تعریف Customer آزمایشی، Installation و لایسنس شرکت‌محور

1. در Vendor.Web: ایجاد Customer (شمارهٔ آزمایشی، مثلاً `STG-001`) → ایجاد Installation (`Environment=Staging`، کد یکتا) → یادداشت `InstallationId`.
2. ایجاد لایسنس شرکت‌محور (v2): فرم لایسنس → `LicenseKind=perpetual` → `MaxCompanies=1` → در بخش شرکت‌ها یک Company با CompanyReference تولیدشدهٔ سرور (Guid) و ماژول `TRADE_IMPORT` و `MaxUsers=1` — CompanyId محلی مشتری هرگز به‌جای CompanyReference استفاده نمی‌شود. (معادل API: `POST /api/vendor/licenses` با آرایهٔ `companies`؛ بدون `subsystems`.)
3. درخواست تأیید و تصمیم تأیید: طبق D-109 همان اپراتور مجاز می‌تواند Maker و Checker باشد (Vendor V1).
4. نتیجهٔ موفق: `Status=Issued`، `LastIssuedSequence=1` و ردیف امضاشده در `VendorSignedLicenseEnvelopes`. هر شکست امضا یا Audit ⇒ هیچ‌چیز ثبت نمی‌شود (تراکنش واحد).

## 5) تحویل پاکت به مشتری (اکنون در main پس از Merge PR #781)

```powershell
# 5.1 — استخراج پاکت (دقیقاً JSON ذخیره‌شده؛ فقط-خواندنی؛ Audit ثبت می‌شود)
Invoke-RestMethod -Method Get -Uri "https://<vendor-host>/api/vendor/licenses/<LICENSE-ID>/envelope/export" `
  -WebSession $vendorSession -OutFile license-envelope.json

# 5.2 — کنترل ساختاری فایل (payloadJson، signatureBase64، kid، algorithm، formatVersion=1)
$env = Get-Content license-envelope.json | ConvertFrom-Json
$env.kid; $env.algorithm   # انتظار: vendor-license-v1 / ECDSA-P256-SHA256
```

- کدهای fail-closed ممکن: `409 license_envelope_not_ready | license_envelope_missing | license_envelope_invalid | license_envelope_signature_invalid | license_envelope_not_currently_valid` و `503 license_envelope_signing_key_untrusted`.
- **هیچ ویرایشی در فایل مجاز نیست**؛ امضا روی بایت‌های payload است و import مشتری همان بایت‌ها را اعتبارسنجی می‌کند.

## 6) سمت Customer — اعتماد، ثبت هویت، import

> گام‌های این بخش Customer-side است و در main موجود است اما **تا امروز روی DB مشتری واقعی اجرا نشده**؛ اجرای واقعی، تصمیم و تأیید مالک می‌خواهد (گیت‌های D-041).

1. **Trust Store لایسنس مشتری** — همان SPKI Vendor، جدا از Trust ACK و revocation:

```powershell
${env:Licensing__TrustedLicenseKeys__vendor-license-v1__PublicKeySubjectPublicKeyInfoBase64} = "<SPKI-License>"
```

2. **Migrationهای مشتری** روی DB واقعی (شامل `20261116070805_AddCompanySetupRecords` برای binding شرکت‌محور #392):

```powershell
dotnet Server.dll --migrate   # یا --migrate --include-trade برای Trade؛ Core اول، شکست Core مانع Trade
```

3. **ثبت هویت نصب** و **import پاکت** (فقط با حساب Infrastructure؛ نه از React):

```powershell
# enrollment یک‌بارمصرف هویت نصب (نقطهٔ پایهٔ TPM/هویت ویندوز):
POST /api/v1/licensing/installation-identity/enroll
# import پاکت امضاشده:
POST /api/v1/licensing/import   # body: envelope JSON + localInstallationId
```

- import فقط پس از پذیرش اعتبارسنج revocation-aware و عبور از وضعیت‌های مجاز (`Valid/ExpiringSoon/Restricted`) ثبت پایدار می‌کند؛ replay و envelope دستکاری‌شده رد می‌شود.
4. **نگاشت CompanyReference** به Company محلی مسیر #392 است؛ import مادهٔ mapping نمی‌سازد و نباید از آن حدس زده شود.

## 7) فعال‌سازی Runtime Gate (خارج از چرخهٔ اول؛ فقط با مجوز جداگانهٔ مالک)

**این بخش را در چرخهٔ اول Staging اجرا نکنید و از آن عبور کنید.** Runtime Gate در تمام چرخهٔ اول خاموش می‌ماند. فعال‌سازی Gate یک رویهٔ مستقل و خارج از دامنهٔ این Runbook است و فقط پس از تصمیم و مجوز کتبی جداگانهٔ مالک، همراه با تأیید آمادگی Recovery، قابل انجام است. وجود دستورات زیر به‌معنی مجوز اجرای آن‌ها نیست.

```powershell
$env:Licensing__RuntimeGate__Enabled = "true"
$env:Licensing__DeploymentMode = "Dedicated"
```

- این دو مقدار باید در **محیط اجرایی خود سرویس Customer** (Service/Service Wrapper یا Secret Store مورد استفادهٔ همان سرویس) ثبت شوند؛ `$env:` در PowerShell فقط session جاری را تغییر می‌دهد و سرویسِ از قبل اجراشده آن را دریافت نمی‌کند. پس از ثبت تنظیمات، سرویس Customer را Restart کنید و سپس وضعیت را بررسی کنید. در اجرای تعاملی، هر دو مقدار را در همان session و پیش از شروع پردازهٔ Customer تنظیم کنید.
- **وضعیت چرخهٔ اول:** مقدار مؤثر `Licensing:RuntimeGate:Enabled` باید پیش از چرخه `false` باشد؛ اگر تنظیم قبلی `true` است، آن را در پیکربندی واقعی سرویس Customer خاموش و پس از Restart تأیید کنید.
- **پیش‌نیاز بازکردن Gate:** آماده‌بودن مسیر recovery و تأیید صریح مالک. `Licensing:RuntimeGate:Enabled` پیش‌فرض OFF باقی می‌ماند؛ تا پیش از تأیید مالک، مقدار را فعال نکنید. بدون لایسنس معتبر، فقط مسیرهای فعال‌سازی/بازیابی باز می‌مانند (503 `license_required` روی بقیهٔ API و صفحهٔ ورود).
- پس از Restart و فقط با مجوز مالک، کنترل وضعیت: `GET /api/v1/licensing/status` — فقط از payload امضاشده.

## 8) Restart و پایداری

1. سرویس Customer را Restart کنید (و در صورت امکان میزبان را).
2. انتظار: ورود عادی بدون import مجدد؛ وضعیت از state پایدار + اعتبارسنجی مجدد runtime می‌آید؛ قطع اینترنت لایسنس Dedicated را باطل نمی‌کند.
3. سرویس Vendor را هم Restart کنید و امضای آزمایشی دوم بگیرید (بخش 3) — امضا بدون تعامل دستی.

## 9) ماتریس منفی (هر مورد با شاهد ثبت شود)

| سناریو | نقطهٔ دفاع | انتظار |
|---|---|---|
| دستکاری PayloadJson در فایل | import مشتری (hash/امضا) | رد fail-closed |
| امضا با کلید دیگر + kid همان | import مشتری | رد (signature_invalid) |
| KeyId ناشناخته/ابطال‌شده در Trust | export Vendor (503) و import مشتری | رد دو طرف |
| InstallationId ناسازگار | binding در Validate هر دو طرف | رد |
| **آزمون زوجی Company A/B** | entitlement resolution مشتری | resolver برای Company A با mapping معتبر و entitlement `TRADE_IMPORT` نتیجهٔ entitled و برای Company B بدون entitlement متناظر نتیجهٔ not-entitled بدهد؛ هر دو نتیجه با هم ثبت شوند. این فقط آزمون entitlement resolution است و مجوز اجرای عملیات Trade نیست؛ `DenyAllTradeModuleEntitlement` بدون تصمیم صریح مالک حفظ می‌شود. آزمون تا تأیید تکمیل mapping موضوع #392 و enforcement موضوع #391 **Not Run / Blocked** است؛ بسته‌شدن Issue #391 به‌تنهایی شاهد اجرای enforcement نیست. پس از فراهم‌شدن هر دو پیش‌نیاز، اجرای آزمون و ثبت نتیجه با تأیید مالک انجام شود. |
| Sequence قدیمی / Replay | state پایدار مشتری + idempotency | رد/بی‌اثر |
| لایسنس منقضی | ارزیابی زمان | `Invalid`؛ import رد |
| پاکت بدون `TRADE_IMPORT` | entitlement gate | Trade باز نمی‌شود؛ `DenyAllTradeModuleEntitlement` دست نمی‌خورد |
| کلید CNG حذف/ACL سلب | Vendor signing | 503 fail-closed + Audit |
| Restart با state خراب | startup guard مشتری | fail-closed |

## 10) ثبت شواهد (الزام)

برای هر گام: تاریخ، دستور، خروجی خلاصه، نام فایل شاهد. تا ثبت واقعی، هیچ گامی «Verified» گزارش نمی‌شود. گام‌های عمداً خارج از این آزمایش: Bootstrap مستقل TenantOwner (D-110)، Amendment ACK/Applied tracking (#393/#394)، sink مستقل Production Audit (#107)، trust distribution/rotation دو میزبان (#235)، و بستن Gate v5 دوکلیدی.

## 11) مراجع

- `docs/runbooks/ISSUE-236-CNG-KEY-CEREMONY-ROTATION.md` (کلید)، `docs/LOCALHOST_WINDOWS_TEST_GUIDE.md` (تست محلی)، `docs/VENDOR_OPERATIONAL_READINESS_CHECKLIST.md` (وضعیت گیت‌ها)، `docs/handoffs/ISSUE-394-vendor-license-envelope-export.md` (قرارداد export)، `docs/handoffs/ISSUE-UNNUMBERED-vendor-to-customer-first-license-cycle-handoff.md` (تقسیم کار Customer)، `docs/handoffs/ISSUE-401-trade-entitlement-handoff.md` (Trade).
