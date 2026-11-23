# تحویل وظیفه — ISSUE-768-vendor-deployment-readiness

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/768
- **Agent/مسئول:** ChatGPT — بررسی و اصلاح مستندات
- **تاریخ:** 2026-11-23
- **مبنای Git:** main@560ff2890be8bbf87795f30dd85f4eaf3cd1f131
- **شاخه:** agent/768-vendor-deployment-readiness
- **وضعیت:** اصلاح تناقض مستنداتی انجام‌شده؛ نقشه استقرار ثبت‌شده؛ اجرای عملیاتی و تغییر کد هنوز انجام نشده است.

## هدف و معیارهای پذیرش
- [x] ایجاد Issue مستقل برای هماهنگی استقرار Local/Remote Vendor و وابستگی‌های Customer.
- [x] بازبینی تصمیم‌های D-055/D-056/D-107 و تفکیک signerهای مرکزی از مسیرهای PKCS#11.
- [x] اصلاح مستند نادرست مبنی بر الزام PKCS#11/HSM برای Gate صدور Provisioning v5.
- [x] ثبت وضعیت واقعی اجزای موجود و مراحل Local Development، Staging و Production.
- [ ] اجرای Vendor در Visual Studio روی دستگاه مالک.
- [ ] اجرای Publish/Install روی Windows Server.
- [ ] آزمون CNG واقعی، Customer activation و ACK بین دو میزبان.
- [ ] تکمیل شواهد عملیاتی #107/#235/#236/#392/#393/#394/#396.

## یافته‌های تأییدشده
1. Vendor.sln در ریشه مخزن و Host در Vendor/Vendor.Host/Vendor.Host.csproj قرار دارند.
2. Provider مرکزی Windows CNG/Certificate Store برای License و Provisioning در کد هست و با Signing:CertificateStore:Enabled opt-in می‌شود؛ Key Ceremony و ACL روی میزبان واقعی هنوز تأیید نشده است.
3. D-107 الزام HSM واقعی را برای signer مرکزی License/Provisioning/Amendment در Vendor V1 حذف کرده و SoftwareProtected را مجاز می‌داند.
4. PKCS#11/D-055 در حوزه‌های مستقلی که D-107 supersede نکرده باقی است؛ نباید به‌عنوان پیش‌نیاز CNG signer مرکزی یا v5 gate مستند شود.
5. Customer verifier، CompanyReference mapping و Apply اتمیک v5 در main وجود دارد؛ بااین‌حال Gate صدور v5 بسته است و CustomerConsumerReady=false باقی می‌ماند.
6. D-110 Bootstrap مستقل TenantOwner/Dedicated/Offline را Accepted ولی NotImplemented ثبت می‌کند؛ Enrollment موجود به‌تنهایی اثبات اجرای کامل D-110 نیست.
7. G2 (Publish allow-list) و G5 (Vendor deployment workflow) هنوز کامل/عملیاتی اثبات نشده‌اند.
8. #235 trust distribution/rotation و آزمون دو میزبان/شبکه واقعی، #236 Key Ceremony، و #107 Production audit sink هنوز باقی‌اند.

## فایل‌های تغییرکرده
- docs/VENDOR_OPERATIONAL_READINESS_CHECKLIST.md — اصلاح شرط امضای v5 و تفکیک D-107 از PKCS#11؛ تصحیح وضعیت CompanyReference mapping در برابر Bootstrap D-110؛ حذف الزام اشتباه حضور فیزیکی دو اپراتور برای CNG SoftwareProtected؛ به‌روزرسانی مبنای سند و لینک راهنمای استقرار.
- docs/VENDOR_LOCAL_REMOTE_DEPLOYMENT_READINESS.md — نقشه استقرار Local/Remote و معیارهای Local runnable، Staging deployable، Customer integrated و Operationally verified.
- Vendor/Vendor.Host/Program.cs — اصلاح comment قدیمی که signer را به KMS/HSM گره می‌زد؛ فقط توضیح کد اصلاح شد و رفتار/ثبت DI تغییر نکرد.
- docs/handoffs/ISSUE-768-vendor-deployment-readiness.md — این گزارش.

## تصمیم‌های فنی
- بازنویسی Vendor یا Microservice کردن آن توصیه/اجرا نشد.
- برای مرحله اول، یک Vendor Host و دو DB مستقل (Vendor/Customer) حفظ می‌شوند.
- CNG SoftwareProtected مسیر امضای مرکزی V1 است؛ HSM/TPM/Dongle برای این مسیر الزام نیست.
- هیچ Gate، Runtime flag، interlock یا fail-closed control تغییر نکرد.
- Customer-side implementation به Issueهای مستقل خودش محدود می‌ماند.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| GitHub main branch lookup | موفق | SHA دقیق 560ff2890be8bbf87795f30dd85f4eaf3cd1f131 |
| بازبینی D-055/D-056/D-107 | انجام شد | متن ADRها با LICENSING.md و SECURITY.md تطبیق داده شد |
| بررسی PRهای باز | انجام شد | #765 (Draft، D-117)، #766 (Vendor docs)، #767 (Trade docs)؛ فایل‌های مشترک آن‌ها دست‌نخورده ماند |
| Build/Test محلی | اجرا نشد | این محیط به working tree و Windows host مالک دسترسی ندارد؛ تغییر Program.cs فقط comment است و رفتار اجرایی تغییر نکرد |
| git diff --check | اجرا نشد | تغییرات از طریق GitHub Contents API اعمال می‌شوند؛ پس از checkout باید در محیط Git اجرا شود |
| Windows CNG ceremony | اجرا نشد | نیازمند Windows host، service identity و حضور/دسترسی مالک |
| دو-host E2E | اجرا نشد | نیازمند دو میزبان و دو دیتابیس SQL Server مستقل |

## امنیت، داده و سازگاری
- هیچ private key، password، token یا connection string خوانده/ثبت/تغییر داده نشد.
- هیچ کد اجرایی، Schema، Migration یا تنظیم فعال‌سازی تغییر نکرد.
- هیچ Gate یا Production configuration باز نشد.
- هیچ دیتابیس Customer یا Vendor واقعی ایجاد/تغییر داده نشد.

## ریسک‌ها و کارهای باقی‌مانده
- Local/Remote deployment هنوز operationally verified نیست.
- D-041/G2 و G5 باید در PRهای اجرایی مستقل تکمیل و با artifact واقعی آزمایش شوند.
- D-112/D-113 پذیرفته‌اند، اما endpoint provisioning/change runtime هنوز باید در کد اثبات شود.
- #235/#236/#107 و #392/#393/#394/#396 باید در Issueهای مالک خودشان تکمیل شوند.
- docs/INDEX.md و docs/WORK_LOG.md عمداً در این PR تغییر داده نشدند، چون PR باز #765 هر دو فایل را تغییر می‌دهد؛ پس از تعیین تکلیف #765، ردیف فهرست و رکورد append-only باید جداگانه همگام شوند. PROJECT_STATUS.md نیز به دلیل PR باز #766 دست‌نخورده ماند.

## شناسه‌های Git
- Branch: agent/768-vendor-deployment-readiness
- Commit: پس از ثبت همه فایل‌ها درج می‌شود.
- Pull Request: پس از بازبینی نهایی و هماهنگی فایل‌های مشترک ایجاد می‌شود.
