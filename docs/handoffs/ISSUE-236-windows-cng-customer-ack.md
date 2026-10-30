# تحویل وظیفه — ISSUE-236-windows-cng-customer-ack

- **Issue:** #236 — Windows-first customer acknowledgement signer
- **Agent/مسئول:** ChatGPT / GitHub integration
- **تاریخ:** 2026-10-30
- **وضعیت:** ناقص — پیاده‌سازی اولیه و تست‌ها در PR؛ آماده Production نیست

## هدف و معیارهای پذیرش
- [x] backend ویندوزی برای امضای ECDSA P-256 پشت interface موجود customer ACK.
- [x] کلید per-installation از قبل provision شده، non-exportable و بدون ساخت/جایگزینی هنگام Sign.
- [x] انتخاب صریح TPM KSP یا OS KSP؛ عدم fallback خودکار.
- [x] امضای بایت‌های canonical دقیق با IEEE P1363 و طول 64 بایت.
- [x] Provider غیرفعال/ناموجود/خراب fail-closed و حفظ provider پیش‌فرض unavailable.
- [x] تست Windows CNG در CI با KSP نرم‌افزاری صرفاً به‌عنوان fixture.
- [ ] اجرای واقعی روی TPM سخت‌افزاری در Windows client و Windows Server.
- [ ] حداقل نسخه‌های Windows/Server و ماتریس firmware/TPM certification.
- [x] فرمان اپراتوری آفلاین برای ایجاد کلید و خروجی public SPKI اضافه شد؛ ثبت public key در Trust Store مشتری و Vendor هنوز باقی است.
- [ ] audit durable metadata-only، health/readiness و runbook جزئی Windows.
- [ ] backend Linux در مرحلهٔ مستقل آینده.

## خلاصه تغییرات
- WindowsCngCustomerProvisioningAcknowledgementSigningProvider به interface فعلی ICustomerProvisioningAcknowledgementSigningProvider اضافه شد.
- WindowsCngCustomerAcknowledgementKeyProvisioner کلید ECDSA P-256 signing-only را با CngExportPolicies.None می‌سازد و فقط public SPKI را تحویل می‌دهد.
- فرمان dotnet Server.dll --provisioning-ack-key create برای اجرای محلی Ceremony اضافه شد؛ ایجاد کلید مستقل از Enabled است و فرمان هیچ‌گاه runtime signer را فعال نمی‌کند.
- TPM پیش‌فرض Microsoft Platform Crypto Provider است؛ Microsoft Software Key Storage Provider فقط با انتخاب صریح استفاده می‌شود و تضمین سخت‌افزاری TPM ندارد.
- DI فقط روی Windows و با Provisioning:CustomerAcknowledgementSigning:Enabled=true provider را فعال می‌کند؛ در سایر حالت‌ها unavailable باقی می‌ماند.
- CI Windows تست‌های محدود CNG را اجرا می‌کند.

## فایل‌های مهم تغییرکرده
- Server/Infrastructure/Provisioning/WindowsCngCustomerProvisioningAcknowledgementSigningProvider.cs
- Server/Infrastructure/Provisioning/WindowsCngCustomerAcknowledgementKeyCommand.cs
- Server/Program.cs
- tests/Template.Tests/Integration/WindowsCngCustomerProvisioningAcknowledgementSigningTests.cs
- .github/workflows/dotnet.yml
- docs/D-055-ISSUE-236-HSM-PKCS11.md
- docs/SECURITY.md
- docs/TESTING.md
- docs/WORK_LOG.md
- docs/runbooks/ISSUE-236-HSM-TPM-KEY-OPERATIONS.md
- docs/PROJECT_STATUS.md

## تصمیم‌های فنی
- انتخاب KSP صریح است؛ نبود TPM یا کلید TPM باعث خطای عمومی unavailable می‌شود و به Software KSP fallback نمی‌کند.
- Software KSP فقط وقتی استفاده می‌شود که صریحاً انتخاب شده باشد و سیاست امنیتی نصب آن را بپذیرد؛ سطح assurance آن پایین‌تر از TPM است.
- کلیدها با KeyName و KeyId نسخه‌دار ساخته می‌شوند؛ برای rotation نام جدید لازم است و overwrite موجود ممنوع است.
- این PR هیچ DB/Migration، cloud adapter یا Linux backend اضافه نمی‌کند.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| Windows CNG tests | در انتظار CI PR | Hosted Windows runner از Software KSP فقط به‌عنوان fixture استفاده می‌کند |
| dotnet build Template.sln --configuration Release | اجرا نشده | محیط فعلی GitHub-only |
| dotnet test Template.sln --configuration Release | اجرا نشده | CI PR باید نتیجه را تعیین کند |
| TPM hardware acceptance | اجرا نشده | نیازمند Windows host واقعی با TPM و نصب سرویس |

## امنیت، داده و سازگاری
- هیچ private key در Git، configuration، log، artifact یا بسته قرار نمی‌گیرد.
- Provisioner فقط public SPKI را برمی‌گرداند؛ signer payload، signature یا private key را log نمی‌کند.
- Structured log فقط purpose، KeyId عمومی، نتیجه یا نوع exception را ثبت می‌کند.
- KSP software با TPM یکسان نیست و فقط انتخاب صریح دارد؛ هیچ fallback خودکاری وجود ندارد.
- حداقل نسخه‌های Windows/Server هنوز تأیید نشده‌اند؛ CNG/KSP ناموجود باعث fail-closed می‌شود.

## ریسک‌ها و کارهای باقی‌مانده
- فرمان Provisioning کلید محلی اضافه شد؛ ثبت/تطبیق public key در Trust Store مشتری و Vendor و روند rotation/revocation هنوز باقی است.
- Trust Store فعلی از configuration عمومی SPKI استفاده می‌کند؛ فرایند امن و versioned برای ثبت/چرخش/revocation دو سمت باید تکمیل شود.
- Audit فعلی در این adapter structured application logging است؛ durable metadata-only audit record هنوز باید طراحی/پیاده‌سازی شود.
- Hardware TPM، ACL واقعی حساب سرویس و بازیابی نصب باید روی Windows Server واقعی آزموده شوند.
- Linux backend به‌صورت جداگانه در آینده پیاده‌سازی می‌شود.

## شناسه‌های Git
- Branch: feat/customer-ack-windows-cng-signer-rebased
- Commit: current PR head (see GitHub PR #347)
- Pull Request: https://github.com/hamidreza1394/Accounting/pull/347

## وضعیت پس از ادغام Windows CNG و اصلاح فیلتر تست — 2026-10-30

- یادداشت‌های اولیهٔ بالا مربوط به PR #347 و پیش از ادغام هستند؛ وضعیت جاری با این بخش جایگزین می‌شود.
- PR #349 با merge commit `e1150a15235c932e3e762bcd727f641fa059038c` ادغام شد و Provider ویندوزی CNG، KeyName/KeyId مشتق‌شده از InstallationId+KeyVersion و فرمان آفلاین `--provisioning-ack-key create` را به main آورد.
- PR #351 فیلتر قدیمی VSTest را با فیلتر xUnit v3/MTP یعنی `--filter-class Template.Tests.Integration.WindowsCngCustomerProvisioningAcknowledgementSigningTests` جایگزین کرد. .NET CI run `41088538581` و Semgrep run `41088538577` موفق شدند؛ تست CNG واقعاً اجرا شد و موفق بود.
- Hosted Windows CI از Software KSP فقط به‌عنوان fixture استفاده می‌کند؛ این نتیجه TPM سخت‌افزاری یا ماتریس نسخه‌های Windows و Windows Server را تأیید نمی‌کند. Production enablement تا آزمون روی سخت‌افزار TPM واقعی و حساب سرویس واقعی ممنوع است.
- PR #352 در حال افزودن revocation صریح Trust Store مشتری و تست rotation overlap است؛ تا CI نهایی آن را ادغام‌شده/Verified تلقی نکنید.
- باقی‌مانده: enrollment public SPKI در Trust Store مشتری و Vendor با رویهٔ عملیاتی، durable metadata-only audit و readiness، آزمون TPM واقعی/ACL حساب سرویس، و پیاده‌سازی Linux backend در مرحلهٔ بعدی.
