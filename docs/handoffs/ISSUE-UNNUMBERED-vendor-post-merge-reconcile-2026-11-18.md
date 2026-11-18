# تحویل وظیفه — ISSUE-UNNUMBERED-vendor-post-merge-reconcile-2026-11-18

- **Issue:** [#228 — Provisioning و فعال‌سازی زیرسیستم‌های خریداری‌شده](https://github.com/hamidreza1394/Accounting/issues/228)، با پیگیری #698، #235، #236، #214، #393 و #107
- **Agent/مسئول:** عامل اجرایی Vendor
- **تاریخ:** 2026-11-18
- **وضعیت:** مستندات وضعیت پس از Merge هم‌راستا شد؛ قابلیت ACK در PR #699 ادغام شده و CI مربوط به PR موفق است؛ تأیید CI روی pushهای main پس از Merge در این اجرا در دسترس نبود.

## هدف و معیارهای پذیرش
- [x] SHA واقعی main، PRهای باز، Issueهای مرتبط و وضعیت PRهای مرجع بررسی شوند.
- [x] وضعیت واقعی PR #699 و نتیجه Jobهای CI آن بررسی شود.
- [x] وضعیت Merge و Jobهای CI مرتبط با PRهای #281، #282 و #284 دوباره بررسی شود.
- [x] اسناد جاری Vendor که هنوز #699 را باز/در انتظار CI نشان می‌دادند اصلاح شوند.
- [ ] CI مربوط به push روی main پس از Merge #699/#700/#701 با دسترسی مستقیم به فهرست Workflow Runs تأیید شود.
- [ ] گیت صدور v5 باز نشود؛ این تغییر خارج از این مأموریت و نیازمند آمادگی/تأیید مستقل است.

## خلاصه تغییرات
- PR #699 با merge commit `755150929b211ac3b952a3c3a7f0856624b11f0d` ادغام شده است. چهار کد رد ثابت شرکت‌محور در allow-list محدود ACK سمت Vendor پذیرفته می‌شوند.
- Vendor CI run [44042742801](https://github.com/hamidreza1394/Accounting/actions/runs/44042742801): سه Job (Ubuntu checkpoint regression، Windows checkpoint regression، build-test-package) همگی موفق.
- Semgrep run [44042742931](https://github.com/hamidreza1394/Accounting/actions/runs/44042742931): یک Job موفق.
- .NET CI run [44042743089](https://github.com/hamidreza1394/Accounting/actions/runs/44042743089): هر هفت Job (secret scan، client، build/test، publish scripts، E2E، Windows publish، publish) موفق.
- علت شکست اولیهٔ Vendor CI، تعارض نام متغیرهای `repeatedBody` و `repeated` در تست SQL Server بود؛ تغییر نام به `rejectedRepeatedBody` و `rejectedRepeated` خطای کامپایل را رفع کرد. منطق تولیدی ACK تغییر نکرد.
- PRهای #700 (D-103) و #701 (ممیزی S3-0e) نیز ادغام شده‌اند؛ main اکنون روی SHA `a7693cf476a8ce4dd0095666c9871b69ce3d00e4` است.
- PRهای #281، #282 و #284 همچنان Merge شده‌اند. Jobهای runهای ثبت‌شده در اسناد برای هر سه PR دوباره خوانده شدند و همهٔ Jobهای موجود موفق بودند.
- تنها PR باز فعلی #702 است و به Trade مربوط است؛ در محدودهٔ Vendor نیست.

## فایل‌های مهم تغییرکرده
- `docs/PROJECT_STATUS.md` — snapshot جاری Vendor پس از #699/#700/#701 و موانع باقی‌مانده.
- `docs/WORK_LOG.md` — ثبت append-only این بازبینی و شواهد CI.
- `docs/INDEX.md` — پیوند این handoff.
- `docs/LICENSING.md` — اصلاح وضعیت قدیمی «allow-list ACK باقی‌مانده».
- `docs/LICENSING_WORKFLOWS.md` — ثبت تکمیل allow-list Vendor و باقی‌ماندن Gate دوکلیدی.
- `docs/ROADMAP.md` — هم‌راستایی وضعیت #392 با PR #699.
- `docs/handoffs/ISSUE-UNNUMBERED-vendor-post-merge-reconcile-2026-11-18.md` — این گزارش.

## تصمیم‌های فنی
- تصمیم معماری جدیدی گرفته نشد.
- Gate دوکلیدی صدور Provisioning v5 همچنان بسته است: `CustomerConsumerReady=false` و `Provisioning:CompanyReferenceIssuance:Enabled` پیش‌فرض خاموش.
- `CompanyReference` همچنان از draft اپراتور انتخاب می‌شود؛ مولد خودکار GUID اضافه نشد، چون این تغییر قبلاً به تأیید مالک موکول شده بود.
- `VendorCustomer.CustomerNumber` هنوز پس از Provisioning قابل تغییر است، در حالی که هنگام صدور با `CustomerReference` برابر می‌شود. این ریسک پایداری binding ثبت شد؛ رفتار اصلاح نشد چون سیاست تغییر/اصلاح شناسه نیازمند تصمیم مالک است و قفل‌کردن بی‌قید آن می‌تواند اصلاح اشتباهات ثبت را نیز ناممکن کند.
- خرید add-on در #393 هنوز candidate builder دارد، اما Order/Payment Evidence/persistence/approval تجاری و صدور پایدار amendment وجود ندارد. پیاده‌سازی تا تعیین مدرک پرداخت، نقش تأییدکننده و transitionهای تجاری شروع نشد.
- signer پیش‌فرض Production همچنان unavailable/fail-closed است؛ هیچ KMS/HSM یا credential فرضی انتخاب نشد.
- Production audit checkpoint همچنان بدون sink مستقل واقعی و credential جدا fail-closed است؛ فایل محلی فقط Development/Test است.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| Vendor CI run 44042742801 | موفق، ۳/۳ Job | regression قفل checkpoint روی Ubuntu/Windows و Build/Test/Package |
| Semgrep run 44042742931 | موفق، ۱/۱ Job | اسکن PR #699 |
| .NET CI run 44042743089 | موفق، ۷/۷ Job | شامل Build/Test، E2E و Publishهای Linux/Windows |
| CI مرتبط با #281 | موفق | runهای ثبت‌شده در اسناد: .NET ۷/۷، Semgrep ۱/۱، Push on main چهار تحلیل موفق |
| CI مرتبط با #282 | موفق | .NET ۷/۷، Vendor ۱/۱، Semgrep ۱/۱، Push on main چهار تحلیل موفق |
| CI مرتبط با #284 | موفق | .NET ۷/۷، Vendor ۱/۱، Semgrep ۱/۱، Push on main چهار تحلیل موفق |
| CI push روی main پس از #699/#700/#701 | تأیید نشد | ابزار GitHub در دسترس این اجرا فقط Workflow Runهای PR-triggered را برمی‌گرداند؛ برای merge SHAها فهرست run خالی و Combined Status نیز خالی بود. این به معنی موفق یا ناموفق بودن CI نیست. |
| Build/Test محلی و `git diff --check` | اجرا نشد | این اجرا checkout محلی/ترمینال در اختیار نداشت؛ CI فوق تنها شواهد اجرای تست است. |

## امنیت، داده و سازگاری
- هیچ کد اجرایی، Schema، Migration، Vendor DB یا Customer DB تغییر نکرد.
- هیچ تغییر در `Server/**`، Customer consumer، trust store، signer، کلید خصوصی یا Gate v5 انجام نشد.
- هیچ رمز، hash رمز، connection string، کلید خصوصی یا credential در فایل/Log/Artifact اضافه نشد.
- کدهای ACK همچنان allow-list ثابت هستند؛ متن آزاد، diagnostic، PII و کد ناشناخته پذیرفته نمی‌شوند.

## ریسک‌ها و کارهای باقی‌مانده
- #228: صدور عملیاتی v5 تا بازشدن Gate با تأیید مستقل و تکمیل آمادگی عملیاتی Customer بسته می‌ماند؛ Migration `CompanySetupRecords` در دیتابیس واقعی اجرا نشده است.
- #235: توزیع/چرخش trust key، readiness/audit پایدار، retry/error semantics و آزمون Staging دو میزبان/دو process باقی است؛ Customer-side کار جداگانه است.
- #236/#214: provider/هویت/credential واقعی و key ceremony/rotation/recovery عملیاتی موجود نیست؛ signer Production fail-closed می‌ماند.
- #393: تصمیم تجاری دربارهٔ Payment Evidence، approver و وضعیت‌های خرید لازم است.
- #107: مقصد مستقل checkpoint با failure domain و credential جدا، immutability/retention و recovery drill عملیاتی لازم است.
- ریسک پایداری `CustomerNumber` و سیاست اصلاح آن باید با مالک تعیین شود.
- اجرای post-merge GitHub Actions روی main نیازمند endpoint/دسترسی فهرست Workflow Runs یا تأیید مستقیم Maintainer است.

## شناسه‌های Git
- **Branch:** `docs/vendor-post-merge-reconcile-2026-11-18`
- **Base:** `main@163e0be3a430439b69c13e0c17e8b99e98617fb9` (پس از Merge #702؛ شاخهٔ PR از مبنای قبلی a769 شروع شده و یک commit عقب است)
- **Initial branch head:** `6f730fe8382daa4b337edaf378c47a0d23d86e9d`
- **Pull Request:** [#703](https://github.com/hamidreza1394/Accounting/pull/703)؛ CI روی head نهایی باید دوباره بررسی شود.
