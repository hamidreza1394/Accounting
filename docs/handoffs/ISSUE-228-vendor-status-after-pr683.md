# Handoff — Vendor status after PR #683

- **Issueهای مرتبط:** #228، #392، #235، #393، #214، #236، #107
- **تاریخ:** 2026-11-16
- **مبنای کد:** `main@b05c1c58c5ff9830b10fb0034354ab8309da9b0d`
- **نوع کار:** بازبینی GitHub و هم‌ترازی مستندات؛ بدون تغییر کد اجرایی

## هدف و محدوده

وضعیت زندهٔ main، PRهای مرجع، Issueهای Vendor و Actions بررسی شد تا snapshot قدیمی #281/#677 پس از ادغام‌های جدید ادامه پیدا نکند. محدوده فقط Vendor و مستندات Licensing است؛ Customer consumer، `Server/**`، Customer DB/Migration و هر secret خارج از محدوده‌اند.

## شواهد GitHub

- در snapshot اولیهٔ این اجرا فهرست PR باز خالی بود؛ سپس #684 (Customer schema برای #392) و #685 (این همگام‌سازی مستندات) باز شدند. #684 هنوز ادغام نشده؛ test count در head `aeadd602c0e3fc5e0b9569f00fbf9988586964c5` اصلاح شده اما CI جدید در انتظار است و schema approval صریح یافت نشده است.
- PRهای #281، #282، #284 و #677 ادغام شده‌اند.
- زنجیرهٔ جدید Vendor:
  - #679: قرارداد Provisioning v5، merge SHA `47e532258bb514d519fc42a094ecdc8977c18750`
  - #680: تغییرناپذیری CompanyReference در Amendment، merge SHA `e2297b41f5c39fe5d2946e4bfbd21e8ca082de2a`
  - #682 (Closes #681): صدور اولیهٔ LicensePayload v2، merge SHA `ce2094321e881d488f953c2ea3276b1f302d29bc`
  - #683: آماده‌سازی Vendor برای Provisioning v5 پشت Gate خاموش، merge SHA `b05c1c58c5ff9830b10fb0034354ab8309da9b0d`

## CI پس از Merge

روی SHA فعلی main:
- Vendor CI [43740624610](https://github.com/hamidreza1394/Accounting/actions/runs/43740624610): ۳/۳ Job موفق.
- .NET CI [43740624547](https://github.com/hamidreza1394/Accounting/actions/runs/43740624547): ۷/۷ Job موفق.
- Semgrep [43740624503](https://github.com/hamidreza1394/Accounting/actions/runs/43740624503): موفق.
- Push on main / CodeQL [43740624071](https://github.com/hamidreza1394/Accounting/actions/runs/43740624071): ۴/۴ تحلیل موفق.

Runهای post-merge #281/#282/#284 و #677/#679/#680/#682/#683 نیز در Project Status ثبت شده‌اند؛ همهٔ Workflowهای مرتبط روی merge SHAهای واقعی موفق‌اند.

## یافته‌های مهم

### Approval ordering — #284

کد فعلی `VendorApprovalEndpoints` منبع هدف را resolve می‌کند، سپس Customer/Installation یا منبع مربوط را authorize می‌کند و تنها بعد از آن RowVersion، وضعیت Pending و self-approval را می‌سنجد. تست HTTP/SQL Server برای هدف Provisioning با Customer/Installation ناسازگار و RowVersion کهنه، 404 را بررسی می‌کند. اصلاح و CI پس از Merge تأیید شده‌اند.

### Provisioning — #228/#392/#235

- PR #682، CompanyReference را به‌صورت GUID-D سمت سرور در پیش‌نویس LicensePayload v2 می‌سازد؛ فقط یک ستون nullable در Vendor DB افزوده شده و هیچ Customer migration انجام نشده است.
- PR #683، CompanyReference را فقط از entitlement موجود در LicensePayload v2 امضاشده resolve می‌کند و Payload v5 را آماده می‌سازد.
- Gate دوکلیدی است: تنظیم `Provisioning:CompanyReferenceIssuance:Enabled` به‌صورت پیش‌فرض خاموش و `CustomerConsumerReady=false` در کد. بنابراین v5 عملیاتی صادر نمی‌شود.
- Customer verifier هنوز `allowCompanyReference` را فعال نمی‌کند؛ mapping durable `CompanyReference → CompanyId`، Apply اتمیک، maxCompanies و Bootstrap در #392/#235 باقی است. هیچ CompanyReference از CompanyId، نام، NationalId یا CustomerBoundaryId استنتاج نمی‌شود.

### Purchased Add-ons — #393

Candidate builderها وجود دارند؛ اما Order/OrderLine، Payment Evidence، approver، وضعیت‌های Purchased/Delivered/Acknowledged و صدور amendment پایدار هنوز وجود ندارند. این بخش پیش از کدنویسی نیازمند تصمیم مالک دربارهٔ نوع مدرک، نقش تأییدکننده و state transitions است. درگاه پرداخت یا Ledger موازی لازم نیست.

### Signer و Audit — #236/#107/#214

- Maker-checker و صدور/ابطال تراکنشی Vendor در main هستند؛ #214 باز می‌ماند تا signer و مصرف end-to-end ابطال/Recovery عملیاتی شوند.
- PKCS#11/SoftHSM برای Development/Test موجود است؛ هیچ KMS/HSM واقعی، token، identity یا credential عملیاتی provision نشده است. default signer unavailable/fail-closed باقی می‌ماند.
- قرارداد checkpoint و JSONL Development/Test موجودند؛ sink مستقل Production با identity/credential، failure domain، immutability/retention، alerting و recovery انتخاب نشده است. هیچ فایل محلی یا Vendor DB به‌عنوان sink مستقل معرفی نمی‌شود.

## تغییرات این کار

- `docs/PROJECT_STATUS.md`: SHA جاری، PRهای واقعی و CI پس از Merge، وضعیت #228/#392/#393/#214/#236/#235/#107.
- `docs/WORK_LOG.md`: ثبت شواهد و گام بعدی.
- `docs/LICENSING.md` و `docs/LICENSING_WORKFLOWS.md`: تفکیک v5 قراردادی از پذیرش Customer و صدور عملیاتی.
- `docs/LICENSING_THREAT_MODEL.md`: اصلاح مالکیت mapping و threat مدل selector.
- `docs/ROADMAP.md`: اصلاح وضعیت #228/#392.
- `docs/SECURITY.md`: وضعیت فعلی checkpoint و مرز Development/Test در برابر Production.
- بدون تغییر ADR/DECISIONS؛ تصمیم محصولی جدیدی اتخاذ نشد.

## تست و محدودیت‌ها

- Build/Test محلی اجرا نشد؛ این PR فقط مستندات را تغییر می‌دهد و checkout/runner محلی در این اجرا در دسترس نبود.
- `git diff --check` محلی اجرا نشد؛ متن فایل‌های ویرایش‌شده از نظر trailing whitespace بررسی شد و هیچ موردی در محتوای جدید گزارش نشد.
- شاهد Verified فقط Runهای GitHub Actions لینک‌شده‌اند؛ هیچ ادعای تست محلی یا آزمون عملیاتی دو میزبان مطرح نیست.
- هیچ secret، private key، password، password hash، connection string یا credential به Git/log/artifact اضافه نشد.

## گام بعدی

1. #392/#235: schema review و Customer mapping/verifier/Apply در PR مستقل؛ فقط پس از آن و تست end-to-end می‌توان دربارهٔ بازکردن Gate v5 تصمیم گرفت.
2. #393: پس از تصمیم مالک دربارهٔ Payment Evidence، approver و state transitions، مدل حداقلی Vendor-only.
3. #236: انتخاب و provision signer واقعی و Key Ceremony؛ بدون حدس provider/credential.
4. #107: انتخاب sink مستقل و هویت/retention/recovery واقعی؛ تا آن زمان fail-closed.
