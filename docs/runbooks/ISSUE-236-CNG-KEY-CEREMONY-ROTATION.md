# Runbook Key Ceremony و Rotation — Windows CNG SoftwareProtected (Issue #236 / D-107)

- **دامنه:** کلیدهای امضای مرکزی Vendor نسخه اول (License و Provisioning) روی Windows Certificate Store با Microsoft Software KSP طبق D-107؛ AssuranceLevel=SoftwareProtected — **هرگز Hardware یا معادل HSM معرفی نشود**.
- **مبنای کد:** `WindowsCngCertificateStoreSigningModule`، `WindowsCngSigningCertificateCommand` (فرمان read-only `--signing-certificate export-public`) و `WindowsCngSigningOptions` در `Vendor/Vendor.Host/Signing/` (Merge PR #720، `70947da5`). مسیر HSM آینده در [ISSUE-236-HSM-TPM-KEY-OPERATIONS.md](ISSUE-236-HSM-TPM-KEY-OPERATIONS.md) باقی است.
- **اصل غیرقابل‌مذاکره (D-107 §2/§6/§7):** کلید خصوصی غیرقابل‌استخراج (ExportPolicy=None، MachineKey در Production)؛ Provider فقط Sign می‌کند و SPKI خصوصی نمی‌دهد؛ نبود کلید یا خطای Store همیشه fail-closed است و هرگز به کلید آزمایشی یا fallback نمی‌رسد؛ امضا در CI انجام نمی‌شود؛ مادهٔ خصوصی در Git، Log، DB، Backup عادی یا بستهٔ مشتری ذخیره نمی‌شود.
- **وضعیت اجرا:** این Runbook هنوز روی میزبان Windows هدف اجرا نشده است؛ تا اجرای واقعی، همهٔ گام‌ها Not Verified است و هیچ «Verified» کلیدواژه‌ای ادعا نمی‌شود (هم‌سو با handoff D-107).

## 1. نقش‌ها و تفکیک مسئولیت

| نقش | مسئولیت | ممنوعیت |
|---|---|---|
| Security Owner (مالک محصول) | تصویب Key Ceremony، rotation، revoke و بازیابی | اجرای عملیاتی روزمره امضا |
| Vendor Operator | اجرای دستورهای مصوب روی سرور، ثبت نتایج در سند جلسه | تغییر Trust Store بدون تأیید مالک؛ امضای Production با کلید آزمایشی |
| شاهد مستقل (نفر دوم) | کنترل خروجی export-public و ثبت Thumbprint/KeyId در صورت جلسه | تأیید عملیات خودش |

برای SoftwareProtected نسخه اول، الزام دو-نفره فیزیکی مانند HSM وجود ندارد؛ **Maker-Checker و MFA و Audit امضا** طبق D-107 §6 در خود نرم‌افزار حفظ می‌شوند و ثبت نتیجه در صورت جلسه جایگزین کنترل سخت‌افزاری نیست.

## 2. پیش‌نیازها

1. Windows Server هدف (نسخه‌ای که Software KSP P-256 را پشتیبانی می‌کند) با دسترسی Administrator محلی؛ میزبان اختصاصی یا VM تحت کنترل مالک (D-107 §5: میزبان اشتراکی با مدیران نامرتبط مجاز نیست).
2. `Vendor.Host` مستقر با همان نسخه‌ای که `70947da5` یا بعد از آن build شده است.
3. هویت سرویس مشخص برای اجرای Vendor (مثلاً `FARANO-VENDOR-SVC`)؛ اجرا با حساب Personal ممنوع.
4. دو KeyId نسخه‌دار جدا با الگوی الگوی KeyId کد (`vendor-license-v1`، `vendor-provisioning-v1`) — KeyId مشترک بین دو هدف در Startup رد می‌شود.
5. Backup سیستمی استاندارد سرور؛ **هیچ بکاپ فایل از مادهٔ خصوصی کلید گرفته نمی‌شود** (CNG non-exportable عملاً بکاپ فایل کلید را ناممکن می‌کند و همین مطلوب است؛ از دست رفتن کلید = سناریوی بازیابی بخش 8).

## 3. ساخت کلیدها — License و Provisioning مجزا

دو کلید مستقل با دو KeyId و دو گواهی (D-107 §3: اشتراک کلید یا KeyId ممنوع). در PowerShell با Administrator:

```powershell
# هر دستور یک گواهی ECDSA P-256 با Software KSP، NonExportable و KeyUsage=DigitalSignature
# می‌سازد و مستقیم در Cert:\LocalMachine\My می‌نویسد (MachineKey طبق D-107).
New-SelfSignedCertificate -Subject "CN=Faranou Vendor License Signing v1" `
  -Provider "Microsoft Software Key Storage Provider" `
  -KeyAlgorithm ECDSA_P256 -KeyLength 256 `
  -KeyExportPolicy NonExportable `
  -KeyUsage DigitalSignature -KeySpec Signature `
  -NotAfter (Get-Date).AddYears(3) `
  -CertStoreLocation Cert:\LocalMachine\My
# این دستور را برای کلید Provisioning با Subject مجزا تکرار کنید:
#   CN=Faranou Vendor Provisioning Signing v1
```

کنترل‌های الزامی پس از ساخت (هر مورد باید برقرار باشد؛ در غیر این صورت گواهی را دور بریزید و دوباره بسازید):

```powershell
$cert = Get-ChildItem Cert:\LocalMachine\My | Where-Object { $_.Subject -like "*License Signing v1*" }
$cert | Select-Object Subject, Thumbprint, NotAfter, HasPrivateKey
$cert.PrivateKey | Format-List *   # باید Provider=Microsoft Software Key Storage Provider و KeyNumber=Signature نشان دهد
(Get-Item ("Cert:\LocalMachine\My\" + $cert.Thumbprint)).Extensions `
  | Where-Object { $_.Oid.FriendlyName -in "Key Usage","Enhanced Key Usage" } | Format-List
# انتظار: Key Usage = Digital Signature؛ بدون KeyEncipherment/DataEncipherment؛
# ECDSA_P256؛ ExportPolicy=NonExportable؛ NotBefore/NotAfter با بازه پذیرفته‌شده کد سازگار.
```

**نسخه اول Production روی CurrentUser ساخته نشود**؛ کد در Production الزام `StoreLocation=LocalMachine` دارد و StoreName فقط `My` است (`WindowsCngSigningOptions.Validate`).

## 4. ACL کلید خصوصی — فقط هویت سرویس

پس از ساخت، فقط `FARANO-VENDOR-SVC` (و در صورت نیاز Administrator برای اضطرار) باید به مادهٔ خصوصی دسترسی داشته باشد:

1. `certlm.msc` → Personal → Certificates → گواهی تازه → All Tasks → **Manage Private Keys**.
2. همه حساب‌های اضافه را حذف کنید؛ فقط این‌ها بمانند: `FARANO-VENDOR-SVC` (Read خاص امضا — از طریق CNG، نه فایل) و در صورت سیاست محلی، `SYSTEM`.
3. بررسی سمت کد: امضای موفق با هویت سرویس و رد امضا با یک حساب غیرمجاز (گام 6). اگر حساب سرویس دسترسی نداشته باشد، Provider fail-closed می‌شود و امضا با خطای نوع-only ثبت می‌شود — این رفتار صحیح است، نه خطای پیکربندی برای دورزدن.

## 5. Key Ceremony — استخراج SPKI عمومی و ثبت Trust Store

1. با هویت سرویس Vendor روی همان میزبان اجرا کنید (فرمان فقط‌خواندنی است؛ هیچ مادهٔ خصوصی چاپ نمی‌شود):

```powershell
dotnet Vendor.Host.dll --signing-certificate export-public
```

2. خروجی حاوی SPKI عمومی Base64، KeyId، Thumbprint و NotAfter برای دو کلید است. شاهد مستقل خروجی را با Thumbprint گام 3 تطبیق دهد و در صورت جلسه ثبت کند.
3. SPKI عمومی هر کلید را در Trust Storeهای میزبان Vendor ثبت کنید (همان کلیدهایی که راهنمای Windows استفاده می‌کند؛ نام دقیق با الگوی پیکربندی فعلی):

```powershell
$env:FARANO_VENDOR_Signing__CertificateStore__Enabled = "true"
$env:FARANO_VENDOR_Signing__CertificateStore__AssuranceLevel = "SoftwareProtected"
$env:FARANO_VENDOR_Signing__CertificateStore__StoreLocation = "LocalMachine"
$env:FARANO_VENDOR_Signing__CertificateStore__License__KeyId = "vendor-license-v1"
$env:FARANO_VENDOR_Signing__CertificateStore__License__Thumbprint = "<THUMBPRINT-LICENSE>"
$env:FARANO_VENDOR_Signing__CertificateStore__Provisioning__KeyId = "vendor-provisioning-v1"
$env:FARANO_VENDOR_Signing__CertificateStore__Provisioning__Thumbprint = "<THUMBPRINT-PROVISIONING>"

# Trust Store امضا — با SPKI عمومی export-public (هرگز مادهٔ خصوصی):
$env:FARANO_VENDOR_Licensing__TrustedSigningKeys__vendor-license-v1__PublicKeySubjectPublicKeyInfoBase64 = "<SPKI-License>"
$env:FARANO_VENDOR_ProvisioningSigningTrust__Keys__0__KeyId = "vendor-provisioning-v1"
$env:FARANO_VENDOR_ProvisioningSigningTrust__Keys__0__PublicKeySubjectPublicKeyInfoBase64 = "<SPKI-Provisioning>"
```

4. کنترل‌های Startup طبق کد: `AssuranceLevel` باید دقیقاً `SoftwareProtected` باشد؛ در Production، StoreLocation=LocalMachine و KeyId دو کلید مجزا الزامی است؛ در صورت تداخل با `Signing:Pkcs11` هر دو هم‌زمان فعال، میزبان fail-fast می‌شود.
5. **سطح اعتبارسنجی و زنجیره گواهی (طبق کد):** `WindowsCngCertificatePolicy` فقط گواهیِ منتخب را کنترل می‌کند — Thumbprint دقیق از پیکربندی، منحنی/اندازه کلید عمومی ECDSA P-256، KeyUsage=DigitalSignature، بازه اعتبار زمانی و سیاست PrivateKey (Software KSP، NonExportable، MachineKey در Production). **اعتبارسنجی Chain/CA انجام نمی‌شود**؛ بنابراین گواهی self-signed با همین مشخصات طبق D-107 قابل قبول است و Trust Store مصرف‌کننده فقط SPKI خام هر KeyId را نگه می‌دارد، نه زنجیره. امضای بسته‌ها با digest SHA-256 و الگوریتم `EcdsaP256Sha256` انجام می‌شود.

## 6. راستی‌آزمایی عملیاتی (چک‌لیست پذیرش)

هر مورد پس از اجرا روی میزبان هدف با تاریخ/نتیجه ثبت شود؛ تا آن زمان Not Verified است:

- [ ] ایجاد کلید License و Provisioning با مشخصات D-107 (P-256، Software KSP، NonExportable، DigitalSignature، LocalMachine)
- [ ] امضای موفق با هویت سرویس (بستهٔ آزمایشی در محیط Staging؛ هرگز با داده Production)
- [ ] Verify بستهٔ امضاشده با Trust Store (مسیر مصرف‌کننده/تست داخلی)
- [ ] Restart سرویس و امضای مجدد موفق بدون هیچ تعامل دستی
- [ ] رد امضا با حساب غیرمجاز پس از حذف ACL حساب سرویس (fail-closed + Audit)
- [ ] رد امضا پس از حذف گواهی (کلید ناموجود → خطای نوع-only؛ هیچ کلید آزمایشی/fallback)
- [ ] `export-public` پس از Rotation برای کلید v2 بدون اختلال کلید v1
- [ ] Audit هر تلاش امضا (موفق/ناموفق) در `SecurityAuditEvents` Vendor

## 7. Rotation با overlap

1. **کلید جدید:** گام 3 با نام/KeyId نسخه بعد (مثلاً `vendor-license-v2`) — کلید v1 را حذف یا ACL را تغییر ندهید.
2. **Trust Store دوگانه:** SPKI v2 را کنار v1 به Trust Storeها اضافه کنید (overlap چند روز تا پایان اعتبار همه بسته‌های صادرشده با v1).
3. **سوییچ امضا:** `Thumbprint` پیکربندی امضا را به v2 تغییر دهید و KeyId را هم‌زمان نسخه‌دار کنید؛ Startup اعتبار الگوی KeyId/Thumbprint را کنترل می‌کند.
4. **بازبینی:** readiness/health سبز، امضای آزمایشی موفق، Audit از امضای اول با v2.
5. **بستن overlap:** پس از اطمینان از نبود بستهٔ نیازمند تأیید با v1، SPKI v1 را از Trust Store حذف کنید؛ گواهی v1 را در Store نگه ندارید (حذف؛ رویداد ثبت شود).
6. **Rollback به کلید قبلی و مرز آن:** تا پیش از گام ۵ (حذف v1 از Store)، بازگشت به v1 ممکن است: `Thumbprint`/`KeyId` پیکربندی امضا را دوباره روی v1 تنظیم و سرویس را restart کنید؛ SPKI v1 هنوز در Trust Store است و Verify امضاهای صادرشده با v2 نیز با overlap ادامه می‌یابد. پس از حذف v1 از Store بازگشت با این روش ممکن نیست (کلید حذف‌شده non-exportable است و از Backup مادهٔ خصوصی مسیر ندارد — بخش 8). **محدودیت امنیتی:** Rollback فقط برای مشکل عملیاتی/فنی کلید جدید مجاز است؛ اگر v2 (یا هر کلید) به افشا مشکوک شود، مسیر درست حذف اعتماد و Rotation اضطراری طبق بخش 8 است، نه بازگشت. هر Rollback با تاریخ، علت و تأیید مالک در Audit ثبت شود.
7. این drill دست‌کم سالانه و همچنین در هر رخداد احتمالی افشا اجرا شود.

## 8. بازیابی از رخداد (کلید حذف/ناموجود/گواهی منقضی)

- رفتار سیستم: fail-closed — صدور 503، readiness ناسالم، Audit؛ **هیچ fallback یا کلید آزمایشی وجود ندارد.**
- اقدام: Key Ceremony جدید طبق بخش 3 با KeyId نسخه بعد؛ Trust Store به‌روزرسانی؛ فعال‌سازی مجدد مجوزها/نصب‌ها طبق D-058 در صورت تغییر هویت امضا؛ بررسی Audit برای امضاهای مشکوک در فاصله رخداد.
- گزارش رخداد با شرح پنجره fail-closed و تصمیم مالک برای هر نصب متأثر ثبت شود.

## 9. مراجع

- D-107 (ADR تصمیم مالک)، PR #720 و handoff `ISSUE-236-windows-cng-software-protected-signer.md`
- `docs/runbooks/ISSUE-236-HSM-TPM-KEY-OPERATIONS.md` (مسیر HSM آینده)
- `docs/LOCALHOST_WINDOWS_TEST_GUIDE.md` (پیکربندی Trust Store و تست محلی)
- D-109 (سیاست تک‌اپراتوره در تأیید عملیات Vendor؛ بر روند Key Ceremony تأثیری ندارد)
