# تحویل بازبینی Vendor — 2026-11-15

- **Issue:** #228 (اصلی)؛ وابستگی‌ها و شکاف‌های مرتبط: #214، #236، #235، #107، #393 و #392
- **Agent/مسئول:** ChatGPT
- **تاریخ:** 2026-11-15
- **وضعیت:** ناقص؛ همگام‌سازی مستندات انجام شد، اجرای end-to-end به وابستگی‌های قراردادی/استقرار مسدود است.

## هدف و معیارهای پذیرش
- [x] SHA جاری main و mergeهای مرجع از GitHub تازه خوانده شد.
- [x] وضعیت PRهای مرجع #281، #282، #284 و PRهای مرتبط #649 و #674 بررسی شد.
- [x] Workflowهای PR-triggered در SHAهای نهایی مرتبط و Jobهای آن‌ها بررسی شد.
- [x] Issueهای #228، #214، #236، #235، #107، #393 و #392 بازخوانی شدند.
- [x] قرارداد و کد جاری Provisioning، resolver، export و وضعیت CompanyReference بازبینی شد.
- [x] اجرای post-merge روی #281/#282/#284/#649 و main@53dd2e با Run ID و Job/Test summary تازه تأیید شد؛ جست‌وجوی صریح PRهای باز #675/#676/#677 را برگرداند.
- [ ] Provisioning v2 برای entitlementهای Company-scoped تا تکمیل #392 و مصرف‌کنندهٔ سازگار در #235 عمداً fail-closed می‌ماند.

## خلاصه تغییرات و شواهد

### مبنای Git
- SHA جاری main از آخرین merge PR #674: `53dd2eab9bf19148d80eb8ee01bbc744d8033d3f`.
- PR #674 در `2026-11-15T07:38:26Z` با همین merge commit ادغام شد.
- PR #649 در `2026-11-14T08:17:38Z` ادغام شد؛ merge commit: `017f18eca481b06ff4fc69157ead30fc53121edc`.
- PRهای #281، #282 و #284 ادغام‌شده‌اند:
  - #281: `2ee1edc52904833bc28eead5a679185377363c24`
  - #282: `f4a0462b0972e7686a686eca0febfaee680baecf`
  - #284: `81ed5f64ed86ebbae47a433cea63e3655cb1d24`

### CI روی Headهای PR (نه ادعای post-merge)
- **#281**، head `62472ea1dbf79e72b647e32d0e10cb9ad8558290`: .NET CI run [40842045659](https://github.com/hamidreza1394/Accounting/actions/runs/40842045659)، ۷/۷ Job موفق؛ log: 576/576 موفق، صفر شکست و صفر Skip. Semgrep run [40842045639](https://github.com/hamidreza1394/Accounting/actions/runs/40842045639) موفق.
- **#282**، head `45da243a4395fc2dc999e0718f9d08e73ae0ffab`: .NET CI run [40841923168](https://github.com/hamidreza1394/Accounting/actions/runs/40841923168)، ۷/۷ Job موفق؛ log: 582/582 موفق. Vendor CI run [40841923144](https://github.com/hamidreza1394/Accounting/actions/runs/40841923144) و Semgrep run [40841923109](https://github.com/hamidreza1394/Accounting/actions/runs/40841923109) هرکدام ۱/۱ موفق.
- **#284**، head `3a64ec2edef32507f081d8188eef486be5d0e903`: .NET CI run [40844522287](https://github.com/hamidreza1394/Accounting/actions/runs/40844522287)، ۷/۷ Job موفق؛ log: 582/582 موفق. Vendor CI run [40844522286](https://github.com/hamidreza1394/Accounting/actions/runs/40844522286) و Semgrep run [40844522342](https://github.com/hamidreza1394/Accounting/actions/runs/40844522342) هرکدام ۱/۱ موفق.
- **#649**، head `99a197ecd4bf4777b55e3ad4696be7b4a2a6a75c`: .NET CI run [43573902803](https://github.com/hamidreza1394/Accounting/actions/runs/43573902803)، ۷/۷ Job موفق؛ log: 1360 کل، 1352 موفق، صفر شکست، ۸ Skip. Vendor CI run [43573902807](https://github.com/hamidreza1394/Accounting/actions/runs/43573902807)، ۳/۳ Job موفق؛ Semgrep run [43573902837](https://github.com/hamidreza1394/Accounting/actions/runs/43573902837)، ۱/۱ موفق.
- **#674**، head `3f4498833e3fde0100fe93bc79d0db711c8fdc32`: .NET CI run [43689700875](https://github.com/hamidreza1394/Accounting/actions/runs/43689700875)، ۷/۷ Job موفق؛ log: 1433 کل، 1425 موفق، صفر شکست، ۸ Skip. Semgrep run [43689700877](https://github.com/hamidreza1394/Accounting/actions/runs/43689700877)، ۱/۱ موفق. این PR خارج از مسیر Vendor بود؛ Vendor CI برای آن انتظار نمی‌رفت.

### راستی‌آزمایی تکمیلی post-merge و وضعیت جاری — 2026-11-15

- **#281:** merge commit `2ee1edc52904833bc28eead5a679185377363c24`؛ Check Runها ۱۲/۱۲ موفق. .NET CI run [40843230825](https://github.com/hamidreza1394/Accounting/actions/runs/40843230825) با ۷/۷ Job و ۵۸۲/۵۸۲ تست موفق؛ Semgrep [40843230797](https://github.com/hamidreza1394/Accounting/actions/runs/40843230797) و Push on main [40843230369](https://github.com/hamidreza1394/Accounting/actions/runs/40843230369) موفق.
- **#282:** merge commit `f4a0462b0972e7686a686eca0febfaee680baecf`؛ Check Runها ۱۳/۱۳ موفق. .NET CI run [40843073694](https://github.com/hamidreza1394/Accounting/actions/runs/40843073694) با ۷/۷ Job و ۵۸۲/۵۸۲ تست موفق؛ Vendor CI [40843073693](https://github.com/hamidreza1394/Accounting/actions/runs/40843073693)، Semgrep [40843073640](https://github.com/hamidreza1394/Accounting/actions/runs/40843073640) و Push on main [40843073179](https://github.com/hamidreza1394/Accounting/actions/runs/40843073179) موفق.
- **#284:** merge commit `81ed5f64ed86ebbae47a433cea63e3655cb1d24b`؛ Check Runها ۱۳/۱۳ موفق. .NET CI run [40845416352](https://github.com/hamidreza1394/Accounting/actions/runs/40845416352) با ۷/۷ Job و ۵۸۲/۵۸۲ تست موفق؛ Vendor CI [40845416309](https://github.com/hamidreza1394/Accounting/actions/runs/40845416309)، Semgrep [40845416374](https://github.com/hamidreza1394/Accounting/actions/runs/40845416374) و Push on main [40845416241](https://github.com/hamidreza1394/Accounting/actions/runs/40845416241) موفق.
- **#649:** merge commit `017f18eca481b06ff4fc69157ead30fc53121edc`؛ Check Runها ۱۵/۱۵ موفق. .NET CI [43575126688](https://github.com/hamidreza1394/Accounting/actions/runs/43575126688)، ۷/۷ Job، ۱۳۵۲ موفق/۸ Skip/۰ شکست از ۱۳۶۰؛ Vendor CI [43575126726](https://github.com/hamidreza1394/Accounting/actions/runs/43575126726)، ۳/۳ Job؛ Semgrep [43575126727](https://github.com/hamidreza1394/Accounting/actions/runs/43575126727) و Push on main [43575126414](https://github.com/hamidreza1394/Accounting/actions/runs/43575126414) موفق.
- **#674 و main فعلی:** merge commit #674 همان `53dd2eab9bf19148d80eb8ee01bbc744d8033d3f` است؛ Check Runها ۱۲/۱۲ موفق. .NET CI [43690845226](https://github.com/hamidreza1394/Accounting/actions/runs/43690845226)، ۷/۷ Job، ۱۴۲۵ موفق/۸ Skip/۰ شکست از ۱۴۳۳؛ Semgrep [43690845229](https://github.com/hamidreza1394/Accounting/actions/runs/43690845229) و Push on main/CodeQL [43690844923](https://github.com/hamidreza1394/Accounting/actions/runs/43690844923) موفق. Check Runهای main: ۱۲/۱۲ موفق.
- **تداوم کد Vendor:** Compare بین merge commit #649 و main فعلی ۲۴ Commit ahead، صفر behind و ۸۴ فایل تغییرکرده دارد؛ هیچ مسیر Vendor، Vendor.sln، Licensing.Contracts یا تست Vendor تغییر نکرده است. بنابراین Vendor CI پس از #649 روی کد Vendor فعلی معتبر است؛ PR #674 صرفاً تغییرات Tenant/test/docs خارج از Vendor دارد.
- **PRهای باز:** جست‌وجوی صریح `is:open` سه PR را برگرداند: #675 و #676 خارج از Vendor، و #677 مستندات Vendor. هیچ PR باز دیگری در نتیجه جست‌وجوی مخزن مشاهده نشد.
- **۱۰۰ اجرای اخیر Actions:** ۹۲ success، ۷ cancelled، یک in_progress و صفر failure. شکست تازه‌ای در این بازه مشاهده نشد.
- **PR #677:** head فعلی `c9085edf3627a480deda93650a214f8ff4ea5a0e`، mergeable=true؛ ۱۵ Check Run شامل ۱۱ success، یک neutral (CodeRabbit rate limit) و سه in_progress برای publish، publish-windows و e2e. هر چهار Required Status Check سبز هستند، اما Code Owner review ثبت نشده است. Ruleset `main-protection`، Code Owner review را الزام می‌کند؛ CODEOWNERS فقط `@hamidreza1394` را مالک مسیرهای Vendor می‌داند و نویسنده PR نیز همین حساب است. Merge انجام نشد.


## بازبینی کد و شکاف‌ها

### #228 — Provisioning و Purchased Subsystems
- `CustomerProvisioningPackageValidator.CurrentPayloadSchemaVersion` هنوز ۴ است. Payload v4 فقط `LicenseId` و `Sequence` را به canonical JSON افزوده؛ canonical نسخه‌های v1-v3 ثابت مانده‌اند و CompanyReference در v4 وجود ندارد.
- `VendorProvisioningEndpoints` در export دقیقاً schema v4 را انتظار دارد.
- `VendorProvisioningLicenseResolver` برای LicensePayload v2 با `provisioning_company_reference_mapping_required` متوقف می‌شود؛ از `Limits.Subsystems` فقط برای payload قدیمی v1 استفاده می‌کند و CompanyEntitlements را به سطح Installation تبدیل نمی‌کند.
- فیلد `ParentCompanyReference` در قرارداد v4 در مسیر فعلی به RequestId یک درخواست والدِ Confirmed اشاره می‌کند؛ این فیلد، CompanyReference موجود در LicensePayload v2 نیست.
- #392 هنوز نگاشت معتبر و مات CompanyReference به Company محلی و بازبینی Schema مشتری را مالک است. D-089 پذیرفته شده، اما Tenant mapping و Bootstrap در کد پیاده نشده‌اند. هیچ Customer migration یا `Server/**` در این کار تغییر نکرد.
- افزودن schema v5 بدون مصرف‌کنندهٔ سازگار و بدون تعیین mapping در #392، بستهٔ قابل‌صدور اما غیرقابل‌مصرف می‌سازد؛ بنابراین در این نوبت نسخه یا فیلد حدس زده نشد.

### #393 — خرید تجاری و add-on
- Candidate builderهای #527 (maxCompanies)، #558 (Company-scoped Module/Feature/UserSeat) و #565 (Support/Updates مستقل) در main هستند.
- مدل‌های runtime برای SalesOrder، OrderLine، PaymentRecord و PaymentAllocation وجود ندارند؛ persistence خرید، Payment Evidence و transitionهای Purchased/Delivered/Acknowledged هنوز پیاده نشده‌اند.
- #393 به تصویب نوع مدرک پرداخت، نقش تأییدکننده و transitionهای سفارش/تأیید نیاز دارد. مسیر کم‌هزینهٔ دستی در Issue ثبت شده، اما تصمیم مصوب نیست.

### #214 و #236 — maker-checker و signer
- Vendor-side revocation snapshot، trust set، sequence تراکنشی، immutable history، maker-checker و export در PR #375 موجودند؛ Customer enforcement/anti-rollback در #235/#364 جدا می‌ماند.
- PKCS#11 و SoftHSM برای Development/Test وجود دارند؛ default signer unavailable/fail-closed است. PR #649 ثبت Audit برای انتخاب Hardware Assurance در Startup را افزود.
- هیچ KMS/HSM واقعی، module path عملیاتی، token، identity یا credential فراهم نشده است؛ Software fallback یا provider فرضی اضافه نشد.

### #235 و #107
- Customer consumer/ACK پایه، trust store عمومی، claim/replay و signed ACK در کد وجود دارند؛ trust distribution/rotation، durable readiness/audit، retry/error semantics و آزمون عملیاتی دو میزبان/backup-restore باقی‌اند. این‌ها فقط در PR مستقل Customer-side پیگیری می‌شوند.
- Production startup guard برای checkpoint sink مستقل fail-closed است؛ Development/Test file store و regression چندپردازه‌ای، Production sink محسوب نمی‌شوند. مقصد مستقل، هویت/credential جدا، immutability/retention، alerting و recovery drill هنوز انتخاب/اجرا نشده‌اند.

## فایل‌های مهم تغییرکرده
- `docs/PROJECT_STATUS.md` — همگام‌سازی وضعیت Vendor و شواهد CI با main جدید.
- `docs/WORK_LOG.md` — ثبت این بازبینی و محدودیت شواهد.
- `docs/INDEX.md` — افزودن پیوند همین handoff.
- `docs/handoffs/ISSUE-UNNUMBERED-vendor-reconcile-2026-11-15.md` — این گزارش.

## تصمیم‌های فنی
- تصمیم معماری جدیدی گرفته نشد.
- fail-closed حفظ شد؛ هیچ CompanyReference، schema v5، مدرک پرداخت یا provider عملیاتی حدس زده نشد.
- تغییر فقط مستندات است؛ هیچ کد اجرایی، Customer consumer، `Server/**`، Customer DB یا Migration مشتری تغییر نکرد.

## بررسی و آزمون

| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| GitHub .NET CI روی #649 head | موفق | ۷/۷ Job؛ 1352 موفق، ۰ شکست، ۸ Skip |
| Vendor CI روی #649 head | موفق | ۳/۳ Job |
| Semgrep روی #649 head | موفق | ۱/۱ Job |
| GitHub .NET CI روی #674 head | موفق | ۷/۷ Job؛ 1425 موفق، ۰ شکست، ۸ Skip |
| Semgrep روی #674 head | موفق | ۱/۱ Job |
| CI پس از Merge #281/#282/#284 | موفق | به‌ترتیب ۱۲/۱۲، ۱۳/۱۳ و ۱۳/۱۳ Check؛ .NET هرکدام ۵۸۲/۵۸۲ تست موفق |
| CI پس از Merge #649/#674 و main فعلی | موفق | #649: ۱۵/۱۵؛ #674/main: ۱۲/۱۲؛ Run و Test totals در بخش بالا |
| تست محلی / Build محلی | اجرا نشد | checkout و runner محلی در این اجرا در دسترس نبود |
| PR #677 روی head فعلی | در حال اجرا | چهار Required Check سبز؛ publish، publish-windows و e2e ناتمام؛ Code Owner review غایب |\n| ۱۰۰ اجرای اخیر Actions | بدون شکست | ۹۲ موفق، ۷ لغوشده، ۱ در حال اجرا، ۰ ناموفق |\n| `git diff --check` محلی | اجرا نشد | diff از GitHub بازبینی می‌شود؛ ادعای اجرای محلی نداریم |

## امنیت، داده و سازگاری
- Issue #632 بسته شده است. در logهای #649، الگوهای مشخص `sqlcmd -P/--password`، Connection String حاوی Password/Pwd، PEM private key و tokenهای رایج اسکن شدند؛ هیچ‌یک از این markerها در logهای بررسی‌شده یافت نشد. این بررسی محدود به همان logها و الگوهاست، نه ادعای اسکن جامع تمام Actions.
- هیچ مقدار secret، کلید خصوصی، رمز، hash رمز مشتری، Connection String یا credential قابل‌استفادهٔ مجدد در Git، artifact، log یا این گزارش قرار نگرفت.
- هیچ تغییر در دیتابیس مشتری، Migration مشتری، `Server/**` یا Customer consumer انجام نشد.

## ریسک‌ها و کارهای باقی‌مانده
- #392: تعیین و تأیید mapping CompanyReference و قرارداد/Schema مشتری.
- #235: هماهنگی consumer با قرارداد آینده، trust distribution و آزمون عملیاتی مستقل.
- #393: تصویب مدرک پرداخت، نقش تأییدکننده و state transitionها.
- #236: انتخاب و provision صریح KMS/HSM، credential/identity و Key Ceremony.
- #107: انتخاب sink مستقل و failure domain، credential separation، immutability/retention و recovery.
- وضعیت Vendor Production-ready نیست؛ fail-closed باید تا رفع موانع باقی بماند.

## شناسه‌های Git
- **Branch:** `docs/vendor-reconcile-2026-11-15-main-53dd2eab`
- **Base:** `53dd2eab9bf19148d80eb8ee01bbc744d8033d3f`
- **Commit(s):** handoff `be3906c2d07ae30f77f54f92cbeb8d3e866a5ea4`؛ Project Status `094e9dc223823afb4dd61175fb3bfdbbaf62f55c` و `(PR #677 update)`؛ Work Log `2aaf1167d8503facc2bbc96ae854f5fcfb7c54b7`؛ Index `b31bd2fa343f400cc38dea1c65e8802e0cc692dc`. Head نهایی را از PR بخوانید.
- **Pull Request:** [#677](https://github.com/hamidreza1394/Accounting/pull/677) — باز؛ CI روی head نهایی باید کامل شود؛ Merge انجام نشده.
