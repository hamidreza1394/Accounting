# تحویل بازبینی Vendor — 2026-11-13

**مبنای جاری:** `main@8e58ad6dd9c8943845df6e328577d6816812c2b3`  
**شاخهٔ PR مستنداتی:** `docs/vendor-reconcile-2026-11-13-0839`  
**وضعیت:** مستندات؛ بدون تغییر کد یا Migration.

## ۱) SHA و CI شاخهٔ اصلی

- SHA فعلی از GitHub خوانده شد؛ merge commit PR #639 در 2026-11-13T07:50:14Z است.
- .NET CI post-merge run [43388610156](https://github.com/hamidreza1394/Accounting/actions/runs/43388610156): Build/Test موفق؛ ۱۲۰۹ تست، ۱۲۰۱ موفق، صفر شکست و ۸ Skip. Secret Scan، Client و Publish Scripts موفق‌اند؛ E2E، Linux Publish و Windows Publish در آخرین snapshot هنوز در حال اجرا هستند.
- Semgrep run [43388610161](https://github.com/hamidreza1394/Accounting/actions/runs/43388610161) و Push on main / CodeQL run [43388609329](https://github.com/hamidreza1394/Accounting/actions/runs/43388609329) موفق‌اند.
- Check Runهای main در این snapshot: ۹ موفق، ۳ در حال اجرا، صفر ناموفق. CI post-merge هنوز کامل تأیید نشده است.

## ۲) Mergeهای مرجع

| PR | Merge SHA | Check Run پس از Merge |
|---|---|---:|
| #281 | `2ee1edc52904833bc28eead5a679185377363c24` | ۱۲/۱۲ موفق |
| #282 | `f4a0462b0972e7686a686eca0febfaee680baecf` | ۱۳/۱۳ موفق |
| #284 | `81ed5f64ed86ebbae47a433cea63e3655cb1d24b` | ۱۳/۱۳ موفق |
| #636 | `0c9ecff7b6feca2efbdad8470301fe3c32edcf63` | ۱۲/۱۲ موفق |
| #637 | `1825107437894e941ef51f60802117ba8e057ca4` | ۱۲/۱۲ موفق |
| #639 | `8e58ad6dd9c8943845df6e328577d6816812c2b3` | post-merge CI در حال اجرا |

ترتیب Authorization در Approval مربوط به #284 و اعتبارسنجی Provisioning پیش از Signing در #282 همچنان در main هستند.

## ۳) Vendor CI و تفاوت آن با main

- آخرین Vendor CI کد-محور: run [42783806157](https://github.com/hamidreza1394/Accounting/actions/runs/42783806157)، سه Job موفق، پس از PR #633.
- GitHub Compare بین merge SHA #633 یعنی `3b103a5875a9668bf0c07d305a91685db3fb1db7` و main فعلی: ۴۹ commit جلوتر، صفر عقب‌تر؛ هیچ فایل `Vendor/**` یا `Licensing.Contracts/**` در فهرست تغییرات نیست.
- کد Vendor از اجرای موفق #633 تغییر نکرده است؛ بااین‌حال این run روی SHA فعلی main اجرا نشده و نباید به‌عنوان چنین شاهدی معرفی شود.

## ۴) PRهای باز و وضعیت Actions

- #638 (Trade): ۱۲ Check Run موفق، دو Job در حال اجرا و یک Skip.
- #640 (Trade): E2E ناموفق؛ عملیات HR deactivate پاسخ 429 به‌جای 204 دریافت کرده؛ Windows Publish موفق و CodeRabbit Skip.
- #641 (Trade): ۱۴ Check Run موفق و یک Skip.
- #642: PR مستنداتی Vendor؛ head اولیه روی main قبلی بود و پس از Merge #639 روی SHA جدید main بازپایه می‌شود. CI پس از بازپایه باید روی head تازه اجرا شود.

در ۱۰۰ اجرای اخیر Actions، ۸۲ موفق، ۸ ناموفق، ۷ لغوشده و ۳ در حال اجرا ثبت شد. هفت شکست مربوط به headهای قدیمی #628/D-078 بودند؛ چهار شکست کامپایل (CS1001/CS1002، CS0246، CS1503) و سه اجرای SQL Server test با Error 18456/State 38 و بازنشدن دیتابیس تست. شکست هشتم E2E در #640 و مربوط به HR است. هیچ‌یک Vendor CI نبودند.

Ruleset فعال `main-protection` برای main، contextهای `secret-scan`، `client`، `build-and-test` و `publish-scripts` را required کرده و Code Owner review را لازم می‌داند؛ bypass actor ثبت نشده است.

## ۵) وضعیت شکاف‌های Vendor

### #228 — Provisioning و Purchased Subsystems
- Provisioning schema v4 و انتخاب یک لایسنس امضاشده موجود است.
- LicensePayload v2 entitlementهای Company-scoped دارد؛ schema v4 هنوز CompanyReference را حمل نمی‌کند.
- Resolver با `provisioning_company_reference_mapping_required` fail-closed می‌ماند؛ هیچ Company entitlement به سطح Installation flatten نمی‌شود.
- #392 مالک CompanySetupRecord، binding مات و پایدار CompanyReference → Company محلی و schema review مشتری است.
- افزودن Customer migration بدون review و approval صریح مجاز نیست. قرارداد نسخه‌دار v5 و اتصال آن به consumer هنوز تکمیل نشده است.

### #393 — Commercial Order و Add-on
- Candidate builderهای #527 (maxCompanies)، #558 (Company-scoped Module/Feature/UserSeat) و #565 (Support/Updates مستقل) در main هستند.
- SalesOrder، OrderLine، PaymentRecord و PaymentAllocation runtime وجود ندارند.
- قبل از persistence/endpoint باید نوع مدرک پرداخت، نقش/هویت تأییدکننده و transitionهای PendingVerification/Purchased/Approval تصویب شوند.
- پیشنهاد کم‌هزینهٔ موجود: یک Order و typed OrderLine، ثبت دستی PaymentReference/Amount/Currency و metadata محدود evidence، RowVersion و audit؛ بدون درگاه پرداخت یا Ledger موازی. این پیشنهاد هنوز تصمیم مصوب نیست.

### #214 — Maker-Checker و Revocation
- Vendor-side signed snapshot، trust set مستقل، sequence تراکنشی، immutable history، maker-checker، audit transaction و export در PR #375 ادغام و CI-verified هستند.
- Customer runtime enforcement و anti-rollback/recovery در #235/#364 جداگانه باقی است.

### #236 — Signer
- D-056 پذیرفته شده؛ PKCS#11 و سه purpose مستقل وجود دارند.
- SoftHSM فقط برای CI/test است. Default signer unavailable/fail-closed باقی می‌ماند.
- KMS/HSM واقعی، token، identity و credential عملیاتی provision نشده‌اند؛ provider واقعی حدس زده نمی‌شود.

### #107 — Audit checkpoint
- HashVersion، verifier version dispatch، chain uniqueness، Development/Test file store و regression چندپردازه‌ای Linux/Windows وجود دارند.
- Production sink مستقل، identity/credential جدا، immutability/retention، alerting و recovery drill هنوز انتخاب یا اجرا نشده‌اند.
- فایل محلی یا Vendor SQL جایگزین Production sink مستقل نیست.

### #235 — Customer consumer و ACK
- Package verifier، trust store عمومی، atomic claim/replay، idempotent retry و signed ACK پایه قبلاً پیاده‌سازی شده‌اند.
- trust distribution/rotation، آزمون دو میزبان/دو OS واقعی، offline recovery و backup/restore عملیاتی باقی‌اند.
- این کار فقط در PR مستقل Customer-side انجام می‌شود؛ نباید با PR محدود به Vendor ادغام شود.

## ۶) یافتهٔ امنیتی خارج از محدوده

لاگ Job ‏`build-and-test` در .NET CI main (run [43385807523](https://github.com/hamidreza1394/Accounting/actions/runs/43385807523)) فرمان ساخت SQL Server تستی و آرگومان `sqlcmd -P` را بدون mask نشان می‌دهد. مقدار رمز در این سند بازنشر نشده است. Vendor CI jobهای بررسی‌شده چنین markerهایی نداشتند. اصلاح `.github/workflows/dotnet.yml` خارج از PR Vendor است و در این بازبینی تغییر نکرد؛ باید به‌صورت کار CI مستقل انجام شود.

## ۷) محدودیت و گام بعدی

- هیچ کد اجرایی، Customer `Server/**`، Migration مشتری، consumer، private key، password، password hash یا credential تغییر نکرد.
- تست محلی اجرا نشده است؛ شواهد CI فقط همان Run/SHAهای لینک‌شده‌اند.
- کار #228 منتظر تصمیم و schema review #392؛ #393 منتظر تصویب semantics پرداخت؛ #236 منتظر KMS/HSM واقعی؛ #107 منتظر sink و هویت مستقل است.
- تا رفع موانع، fail-closed حفظ می‌شود و هیچ CompanyReference، Payment Evidence یا provider عملیاتی حدس زده نمی‌شود.
