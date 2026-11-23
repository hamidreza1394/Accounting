# تحویل ممیزی Vendor — 2026-11-23

- **Issue:** `UNNUMBERED` — ممیزی و همگام‌سازی وضعیت Vendor؛ Issue جدید یا شماره ساختگی ایجاد نشده است.
- **مبنای دقیق:** `main@06fdb015ddd113c2b2b5a715310a83759488cfca`.
- **دامنه:** فقط Vendor/Licensing و شکاف‌های مرتبط؛ بدون تغییر کد اجرایی، Schema، Migration، Customer-side consumer یا `Server/**`.

## ۱) شواهد GitHub

- `main` از GitHub API خوانده شد؛ PR #765 پس از بازبینی و 14/14 Check Run موفق با squash commit `06fdb015ddd113c2b2b5a715310a83759488cfca` ادغام شد و D-117 اکنون در main است.
- PR #763 آخرین Merge کد/قابلیت Vendor پیش از این snapshot است. PRهای باز در زمان آخرین بازبینی: #766 (همین PR)، #767 (Trade)، #769 (Vendor deployment readiness) و #770 (Vendor publish guard G2). وضعیت mergeability و CI آن‌ها باید روی HEAD فعلی بررسی شود.
- PRهای #281، #282 و #284 همگی Merged هستند. Check Runهای merge commitها به‌ترتیب 12/12، 13/13 و 13/13 موفق‌اند. برای #282، .NET CI پس از Merge روی همان merge SHA نیز `completed/success` است.
- روی main فعلی 12/12 Check Run موفق‌اند: هفت Job در .NET CI، یک Job در Semgrep و چهار تحلیل CodeQL. Runها:
  - .NET CI: https://github.com/hamidreza1394/Accounting/actions/runs/45060571229
  - Semgrep: https://github.com/hamidreza1394/Accounting/actions/runs/45060571224
  - Push on main / CodeQL: https://github.com/hamidreza1394/Accounting/actions/runs/45060570843
- در 100 اجرای اخیر Actions، 93 مورد success و 7 مورد cancelled و صفر failure دیده شد. این بررسی فقط صفحهٔ اخیر و SHA فعلی را پوشش می‌دهد، نه تمام 10,424 اجرای تاریخی.
- هیچ Job ناموفق جاری روی main وجود ندارد که نیازمند log/root-cause repair باشد؛ بنابراین branch اصلاحی تستی ساخته نشد.

## ۲) PRها و Issueهای مرتبط

- **#281:** وضعیت/Work Log بعد از mergeهای مربوطه در تاریخچه مستند شده است؛ merge commit `2ee1edc52904833bc28eead5a679185377363c24`، 12/12.
- **#282:** اعتبارسنجی payload قبل از signing؛ merge commit `f4a0462b0972e7686a686eca0febfaee680baecf`، 13/13 پس از Merge.
- **#284:** Authorization هدف Approval قبل از بررسی RowVersion/Status؛ merge commit `81ed5f64ed86ebbae47a433cea63e3655cb1d24b`، 13/13. D-109/#724 بعداً self-approval را برای اپراتور مجاز Vendor V1 پذیرفت؛ این تصمیم ترتیب Authorization را تغییر نمی‌دهد.
- **#228:** Vendor provisioning intent، provisioning v4، signing/export، ACK intake و readiness diagnostics موجودند. Gate صدور v5 بسته و default-off است. CompanyReference mapping/bootstrap و Customer migration در #392؛ trust distribution و readiness سمت Customer در #235 هستند.
- **#393:** #722 چرخه Order/Payment/Receivables، #738 UI فروش/پرداخت و #739 Amendment نصب‌محور را به main آورده‌اند. Company-scoped Module/Feature/UserSeat تا #392 مسدود است. Delivery/ACK/Applied tracking اختصاصی Amendment هنوز وجود ندارد.
- **#759:** تحلیل S-B فقط گزینه A را پیشنهاد می‌کند؛ وضعیت `Proposed`. پرسش‌های باز مالک: انتخاب A یا B، جدول مستقل یا state روی درخواست، کلید ACK مشترک یا جدا، و retention/audit. تا تصویب، Schema/API/state جدید اضافه نمی‌شود.
- **#236:** CNG/Certificate Store با MachineKey غیرقابل‌استخراج و `SoftwareProtected` طبق D-107 پیاده‌سازی و Runbook دارد. KMS/HSM خارجی برای signer مرکزی Vendor V1 اجباری نیست. Key Ceremony واقعی، ACL سرویس، Trust Store، rotation/overlap، recovery و آزمون fail-closed روی Windows Server هدف هنوز تأیید نشده‌اند.
- **#214:** کنترل‌های Vendor-side صدور/ابطال، Approval، RowVersion، Audit و rollback اتمیک fail-closed در main هستند. D-109 الزام دو اپراتور متفاوت را برای Vendor V1 حذف کرده است. Customer revocation consumer/anti-rollback در #235/#364 مستقل باقی می‌ماند.
- **#235:** consumer/verifier و ACK پایه در main هستند؛ trust distribution/rotation، durable readiness/audit، retry/error semantics و آزمون Staging دو میزبان/دو OS باقی‌اند. این کار باید PR جداگانه Customer-side باشد.
- **#107:** checkpoint contract، versioned chain، startup guard و Development/Test file store موجودند؛ Production sink مستقل، identity/credential جدا، immutability/retention، alerting و recovery drill provision نشده‌اند.

## ۳) تغییر این شاخه

- `docs/PROJECT_STATUS.md`: snapshot تازه روی SHA واقعی main، CI، PRهای مرجع و وضعیت #228/#393/#236/#214/#235/#107 افزوده شد؛ snapshotهای تاریخی حذف یا بازنویسی نشدند.
- این handoff: شواهد، مرزهای تغییر و موانع ثبت شد.
- **`docs/WORK_LOG.md` عمداً تغییر نکرد:** PR باز #765 نیز همان فایل append-only را تغییر می‌دهد؛ ویرایش هم‌زمان آن احتمال conflict غیرضروری ایجاد می‌کرد. رکوردهای #281/#282/#284 و S-B تا S-G و Mergeهای #755/#757–#763 از قبل در Work Log موجودند. پس از تعیین تکلیف #765، این ممیزی را می‌توان بدون تعارض به Work Log append کرد.
- `docs/INDEX.md` نیز برای پرهیز از تداخل با PR #765 دست‌نخورده ماند؛ snapshot جدید از `PROJECT_STATUS.md` به این handoff پیوند می‌دهد.

## ۴) آزمون و اعتبارسنجی

| بررسی | نتیجه |
|---|---|
| GitHub main SHA | `560ff2890be8bbf87795f30dd85f4eaf3cd1f131` |
| main Check Runs | 12/12 success |
| PR #281 merge SHA | 12/12 success |
| PR #282 merge SHA | 13/13 success؛ post-merge CI موفق |
| PR #284 merge SHA | 13/13 success |
| PR #765 HEAD | 14/14 success؛ Draft، خارج از دامنه |
| اجرای محلی Build/Test | اجرا نشد؛ این شاخه فقط مستندات است |
| `git diff --check` محلی | اجرا نشد؛ diff از طریق GitHub compare/patch بازبینی شد |

## ۵) امنیت و محدودیت‌ها

- هیچ private key، password، password hash، connection string یا reusable secret خوانده/ثبت/تغییر داده نشد.
- هیچ provider واقعی KMS/HSM، credential یا مقصد Production حدس زده یا provision نشد.
- هیچ Gate مربوط به v5 باز نشد؛ هیچ Customer DB، `Server/**` یا Migration مشتری تغییر نکرد.
- این مستندات Production readiness را ادعا نمی‌کنند.

## ۶) گام بعدی

1. پس از تصمیم مالک درباره قرارداد S-B، یک PR کوچک Vendor-only برای Amendment delivery/ACK tracking طراحی شود؛ تست‌های SQL Server، idempotency/replay/conflict و re-delivery همان بایت‌ها لازم‌اند.
2. CompanyReference mapping و Customer v5 Apply در #684/#686 پیاده شده‌اند؛ Bootstrap مستقل TenantOwner طبق D-110 هنوز NotImplemented است و Migration مشتری هنوز روی DB واقعی اجرا نشده. consumer/trust/ACK مشتری فقط در #235 باقی است.
3. Key Ceremony #236 و Production audit sink #107 فقط با میزبان، identity، مقصد و دسترسی واقعی انجام شوند؛ تا آن زمان fail-closed بمانند.
