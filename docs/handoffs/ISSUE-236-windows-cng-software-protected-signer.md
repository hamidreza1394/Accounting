# تحویل وظیفه — ISSUE-236-windows-cng-software-protected-signer

- **Issue:** #236 (Signer) — تصمیم مالک: ADR **D-107** در `docs/DECISIONS.md`
- **Agent/مسئول:** GLM (mأموریت مالک 2026-11-19؛ تکمیل زنجیرهٔ Vendor/Licensing)
- **تاریخ:** 2026-11-19
- **وضعیت:** انجام‌شده در کد و تست cross-platform؛ اجرای تست‌های Windows-only به CI ویندوز و میزبان واقعی موکول است

## هدف و معیارهای پذیرش (از D-107)
- [x] Provider نسخهٔ اول با Windows Certificate Store و CNG؛ فقط عملیات Sign؛ هیچ export/سریال‌سازی کلید خصوصی.
- [x] `AssuranceLevel=SoftwareProtected` صریح در پیکربندی، startup log، health و Audit؛ هیچ ادعای Hardware.
- [x] کلیدهای License و Provisioning جدا (Thumbprint و KeyId مجزا؛ اعتبارسنجی یکتایی)؛ KeyId نسخه‌دار.
- [x] کلید غیرقابل‌Export (CNG export policy بدون AllowPlaintextExport/AllowArchiving) به‌صورت fail-closed اعتبارسنجی می‌شود.
- [x] گواهی ECDSA P-256 معتبر الزامی؛ RSA/P-384/گواهی منقضی/آینده رد می‌شوند.
- [x] هیچ fallback به کلید آزمایشی؛ نبود/خرابی گواهی یا Store → `signing_provider_unavailable`؛ ثبت در Audit و صدور 503 در مسیر صدور.
- [x] Audit هر عملیات امضا (metadata-only)؛ شکست Audit → دورریختن امضا (پابرجای الگوی PKCS#11).
- [x] Audit انتخاب Assurance در Production (`vendor.signing.assurance.software-protected-selected`) + لاگ ریسک مدیر محلی (D-107 بند ۵).
- [x] Offline-revocation عمداً مشمول این Provider نیست (D-055 برای آن پابرجاست)؛ در شاخه CNG همان Provider Unavailable می‌ماند.
- [x] تداخل هم‌زمانی PKCS#11 و CNG در Startup رد می‌شود (fail-fast).
- [ ] اجرای تست‌های واقعی با کلید MachineKey روی Windows Server هدف — نیازمند میزبان ویندوز/Key Ceremony (بخش باقی‌مانده).

## خلاصه تغییرات
- Provider امضای SoftwareProtected طبق D-107 اضافه شد: انتخاب گواهی با Thumbprint از Store مشخص، اعتبارسنجی ساختاری (ECDSA P-256، بازهٔ اعتبار)، الزام CNG non-exportable برای کلید خصوصی و امضای دقیق بایت‌های canonical با SHA-256/P1363 و خروجی ۶۴ بایتی.
- دو Provider هدف‌مند `ILicenseSigningProvider` و `IProvisioningSigningProvider` با Audit مشترک metadata-only (`VendorSigningAuditExecution`)؛ رفتار شکست Audit عین الگوی PKCS#11 است.
- Startup service اعتبارسنجی پیکربندی (fail-fast، شامل رد میزبان غیرویندوزی و رد `CurrentUser` در Production) و Audit Production را انجام می‌دهد.
- Health check برای گواهی‌های هر دو هدف.
- فرمان read-only `dotnet Vendor.Host.dll --signing-certificate export-public` برای Key Ceremony: فقط SPKI عمومی/KeyId/Thumbprint/NotAfter را چاپ می‌کند تا Trust Store پیکربندی شود؛ هیچ مادهٔ خصوصی چاپ نمی‌شود و هیچ effect runtime ندارد.
- ثبت DI opt-in: `Signing:CertificateStore:Enabled` (پیش‌فرض خاموش). در نبود فعال‌سازی، Providerهای Unavailable عین قبل fail-closed می‌مانند. Provider ابطال آفلاین در این شاخه عوض نمی‌شود.
- CI: job جدید `windows-cng-signer` در `vendor-ci.yml` تا تست‌های CNG ویندوز واقعاً روی Windows runner اجرا شوند (هم‌الگوی job ویندوز در `dotnet.yml`).

## فایل‌های مهم تغییرکرده
- `Vendor/Vendor.Host/Signing/WindowsCngSigningOptions.cs` — پیکربندی و اعتبارسنجی (بدون secret).
- `Vendor/Vendor.Host/Signing/WindowsCngCertificateStoreSigningModule.cs` — ماژول امضا و سیاست گواهی (`WindowsCngCertificatePolicy`).
- `Vendor/Vendor.Host/Signing/WindowsCngSigningProviders.cs` — Audit execution، دو Provider هدف‌مند، Health check، Startup assurance.
- `Vendor/Vendor.Host/Signing/WindowsCngSigningCertificateCommand.cs` — فرمان ceremony فقط‌عمومی.
- `Vendor/Vendor.Host/Program.cs` — رد تداخل دو Signer، ثبت شاخهٔ CNG، هوک فرمان ceremony.
- `tests/Vendor.Host.Tests/WindowsCngSigningProviderTests.cs` — ۱۲ تست cross-platform (پیکربندی، Audit، KeyId routing، fail-fast).
- `tests/Vendor.Host.Tests/WindowsCngCertificatePolicyTests.cs` — سیاست گواهی با گواهی درون‌حافظه‌ای (RSA/P-384/منقضی/فقط-عمومی).
- `tests/Vendor.Host.Tests/WindowsCngCertificateStoreSigningTests.cs` — تست‌های Windows-only با کلید CNG واقعی Software KSP غیر-Machine در CurrentUser (نمونهٔ تست؛ Production الزام LocalMachine دارد) + پاک‌سازی کامل.
- `tests/Vendor.Host.Tests/WindowsCngSigningCertificateCommandTests.cs` — کدهای خروج و پیام‌های فرمان.
- `.github/workflows/vendor-ci.yml` — job `windows-cng-signer`.
- `docs/SECURITY.md`، `docs/PROJECT_STATUS.md`، `docs/LICENSING.md`، `docs/WORK_LOG.md` — هم‌ترازی وضعیت.

## تصمیم‌های فنی
- تکرار الگوی PKCS#11 عمداً حفظ شد (Audit wrapper جدا برای CNG) تا فایل حساس PKCS#11 و تست‌های آن دست‌نخورده بماند؛ ادغام آیندهٔ دو Audit wrapper در یک helper می‌تواند در PR تمیز جداگانه انجام شود.
- در تست‌های ویندوز از Software KSP کلید user-scope (نه MachineKey) استفاده شده تا نیاز به elevation نداشته باشد — عین رویهٔ تست‌های CNG مشتری؛ Production همچنان `LocalMachine` را الزام می‌کند و تست‌ها اثبات TPM/ACLS واقعی سرور نیستند.
- گواهی بدون کلید CNG (مثل کلید نرم‌افزاری درون‌حافظه‌ای) به‌عنوان «قابل‌Export» رد می‌شود تا مسیر کلید exportable بسته بماند.
- تصمیم معماری جدیدی گرفته نشد؛ اجرای دقیق D-107 است.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `dotnet build Vendor.sln -c Release` | موفق | ۰ خطا (SDK 10.0.100 محلی؛ Linux) |
| `dotnet test Vendor.sln -c Release` (Linux، بدون SQL Server) | اجرا شد — نتیجه در `PROJECT_STATUS.md` ثبت می‌شود | تست‌های SQL Server skip؛ تست‌های Windows CNG skip |
| `dotnet test ... --filter "FullyQualifiedName~WindowsCng"` | ۲۱ موفق / ۷ skip / ۰ شکست | skipها فقط ویندوزی‌اند و در job جدید CI ویندوز اجرا می‌شوند |
| تست CNG روی Windows runner | اجرا نشده | نتیجه پس از CI این PR ثبت می‌شود؛ تا آن زمان Verified نیست |
| `git diff --check` | موفق/پاک | — |

## امنیت، داده و سازگاری
- هیچ Migration/Schema و هیچ داده‌ای تغییر نکرد؛ Vendor DB و Customer DB دست‌نخورده‌اند.
- هیچ کلید خصوصی/secret در کد، تست، پیکربندی یا Log نیست؛ تست‌ها کلیدهای موقت را در finally حذف می‌کنند.
- DI پیش‌فرض عین قبل fail-closed است؛ فعال‌سازی صریح نیازمند `Signing:CertificateStore:Enabled=true` است و در نبود گواهی معتبر صدور همچنان 503 می‌دهد.
- مرزهای D-055 (HSM برای ابطال آفلاین) و D-057 (TPM مشتری) تغییر نکرده‌اند.

## ریسک‌ها و کارهای باقی‌مانده
- Key Ceremony واقعی (ساخت MachineKey non-exportable در Store سرور، ACL برای هویت سرویس، ثبت SPKI در `Licensing:TrustedSigningKeys` و `ProvisioningSigningTrust:Keys`) عملیاتی و مستند در راهنمای Windows انجام می‌شود؛ تا اجرای واقعی، Verified کلیدواژه‌ای ادعا نمی‌شود.
- تست Windows-only در CI ویندوز باید سبز شود؛ سپس در میزبان ویندوز مالک با کلید واقعی دوباره اجرا شود.
- Rotation/overlap با KeyId v2 و Trust Store پیکربندی‌شده پشتیبانی می‌شود؛ drill عملیاتی rotation بعدی است.

## شناسه‌های Git
- Branch: `agent/236-windows-cng-signer`
- Commit: `d00d6e52`
- Pull Request: #720
