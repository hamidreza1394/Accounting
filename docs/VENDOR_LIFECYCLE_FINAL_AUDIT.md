# ممیزی نهایی چرخه Vendor — مرحله پنجم مأموریت (بازبینی 2026-11-21)

این سند وضعیت Issueها، امنیت، هزینه و آمادگی Production را با main و شاخه‌های جاری تطبیق می‌دهد. وجود کد یا سبز بودن CI به‌تنهایی به معنی تکمیل End-to-End یا Production readiness نیست.

**مبنای کد و Git:**
- main: `755aeba20032...` (مرحله ۴ ادغام شده)؛ بازبینی این سند روی شاخه Stage 5 پس از همگام‌سازی با همین main انجام شده است.
- PR #739 (مرحله ۲): ادغام‌شده با روش Rebase (قاعده `required_linear_history` مخزن Merge Commit را ممنوع می‌کند)؛ ۱۱ commit حفظ‌شده و tip ادغام `2d181e0d801a...`.
- PR #740 (مرحله ۳): ادغام‌شده با Squash؛ commit `b5f8c53db240...`.
- PR #741 (مرحله ۴): ادغام‌شده با Squash؛ commit `755aeba20032...` از head `811fbb08`؛ CI روی head نهایی ۱۸/۱۸ Check Run سبز؛ UI مطابق D-107/D-109 هماهنگ و تست‌های رگرسیون ادعاها اضافه شد (Vitest ۸۳/۸۳).
- PR #742 (این مرحله): Draft؛ شاخه با main (مرحله ۴ ادغام‌شده) همگام شد و علاوه بر مستندات، هم‌راستاسازی readiness با وضعیت‌های ذخیره‌شده و تفکیک پرداخت آخرین سفارش را به‌صورت تغییرات مستقل خود دارد؛ ادغام معنایی بدون Force Push انجام شد.

## ۱. وضعیت Issueهای مرتبط

| Issue | وضعیت GitHub | وضعیت ممیزی | شواهد و مرز واقعی | اقدام باقی‌مانده |
|---|---|---|---|---|
| #107 — زنجیره Audit و checkpoint مستقل | open | **Partially Implemented** | hash-chain داخل Vendor DB، قرارداد checkpoint، `IVendorAuditCheckpointStore`، startup guard برای Production و file store مخصوص Development/Test وجود دارد. File store محلی Production sink مستقل محسوب نمی‌شود. | انتخاب و provision یک sink با failure domain و identity مستقل، immutability/retention، alerting و recovery drill |
| #214 — صدور/ابطال fail-closed | open | **Vendor-side Implemented / End-to-End Partial** | صدور و snapshot ابطال Vendor، sequence تراکنشی، approval، Audit و rollback در شکست وجود دارد. طبق D-109 همان اپراتور مجاز می‌تواند در V1 درخواست و تصمیم را ثبت کند؛ تضمین دو نفر متفاوت وجود ندارد. Customer-side consumer و runtime enforcement جدا هستند. | تکمیل/اثبات مسیر Customer-side در #235/#364 و آزمون عملیاتی revocation؛ الزامات کلید snapshot ابطال طبق D-055 مستقل از D-107 بررسی شوند |
| #228 — Onboarding و Provisioning | open | **Partially Implemented** | Vendor intent، payload نسخه‌دار، approval، signing، export و ACK intake وجود دارند؛ Customer Apply/Enrollment نیز مسیر پایه دارد. CompanyReference v5 و allow-list خطاهای Company-bound در کد هستند، اما Gate صدور v5 پیش‌فرض بسته است. | تکمیل mapping و bootstrap در #392، اعتماد/آمادگی #235 و آزمون واقعی نصب/تحویل |
| #235 — Customer consumer و ACK | open | **Partially Implemented** | verifier، trust store، claim/replay اتمیک، Apply و ACK امضاشده در main هستند. تست دو TestServer/دو SQL database در یک process، معادل دو میزبان فیزیکی یا دو OS مستقل نیست. | trust distribution/rotation، readiness و audit پایدار، retry/error semantics و آزمون Staging دو میزبان |
| #236 — signer و maker-checker | open | **Code Implemented / Operational Verification Pending** | D-107 برای امضای مرکزی License و Provisioning، Windows CNG/Certificate Store با MachineKey غیرقابل‌استخراج و AssuranceLevel=SoftwareProtected را برای V1 مجاز کرده است؛ Provider و Runbook در PR #720/#736 وجود دارند. HSM/KMS خارجی پیش‌نیاز اجباری این signer مرکزی نیست. | Key Ceremony واقعی روی Windows Server هدف، ACL هویت سرویس، ثبت SPKI در Trust Store، rotation/overlap، recovery و آزمون fail-closed عملیاتی |
| #389 — entitlement شرکت‌محور | open | **Blocked on #392** | مالکیت CompanyReference و mapping باید از مسیر #392 کامل شود؛ هیچ flatten یا reference حدسی مجاز نیست. | تکمیل #392 |
| #390 — قرارداد entitlement شرکت‌محور | closed | **Implemented / CI-Verified** | payload v2 و ساختار entitlement شرکت‌محور در کد و تست‌ها وجود دارد. | مورد کدباقی‌مانده در محدوده این Issue شناسایی نشد |
| #391 — ظرفیت و enforcement entitlement | closed | **Implemented / CI-Verified (scope محدود)** | evaluator و قواعد ظرفیت در مسیرهای مربوط وجود دارند؛ بسته بودن Issue به معنی فعال بودن همه call-siteهای عملیاتی حسابداری نیست. | call-siteهای runtime خارج از این برش باید جداگانه ردیابی شوند |
| #392 — mapping شرکت مجاز و Bootstrap | open | **Partially Implemented / Gate Closed** | قرارداد v5، CompanySetupRecord و مسیر Apply در مخزن وجود دارند؛ Gate دوکلیدی Vendor default-off است. Migration مربوط به Customer فقط در مخزن است و اجرای آن روی DB واقعی مشتری اثبات نشده است. | تصمیم/تأیید فعال‌سازی، mapping معتبر، اجرای Migration در پنجره کنترل‌شده و تست restore/clone |
| #393 — فروش و صدور add-on | open | **Partially Implemented** | Order/Payment/Receivables و Amendment نصب‌محور در PRهای #722/#738/#739 وارد main شده‌اند؛ پرداخت و صدور طبق D-108 مستقل‌اند. Company-scoped Module/Feature/UserSeat تا #392 مسدود است. | ادامه fulfillment/ACK tracking و مسیرهای company-scoped پس از #392؛ بدون درگاه پرداخت یا Ledger موازی |
| #394 — Sync آنلاین/آفلاین | open | **Partially Implemented / Needs Verification** | بسته‌های امضاشده، export/import و Wizard وجود دارند؛ این‌ها اثبات sync دوره‌ای و recovery عملیاتی کامل نیستند. | قرارداد retry/sequence، آزمون قطع‌و‌وصل، reconciliation و backup/restore روی محیط واقعی |
| #396 — Startup Gate و Wizard | open | **Code / CI-Verified; Production Rollout Not Verified** | Runtime Gate و Wizard وجود دارند، اما CompanyReference issuance خاموش است و فعال‌سازی Production اثبات نشده است. | rollout کنترل‌شده پس از تکمیل #392/#235 و تأیید مالک |

## ۲. ممیزی امنیت

- **Authentication/Authorization:** endpoint readiness فقط-خواندنی از resource-bound authorization مشتری و مجوز `vendor.customers.read` استفاده می‌کند؛ queryها با CustomerId محدود می‌شوند.
- **Isolation:** Vendor فقط metadata و وضعیت کنترل‌پلین خود را می‌خواند؛ به Customer DB متصل نمی‌شود و هیچ Connection String مشتری در Vendor ذخیره نمی‌شود.
- **Bootstrap/Credential:** رمز پیش‌فرض مشترک وجود ندارد؛ Enrollment یک‌بارمصرف است و secret قابل استفاده مجدد در Vendor یا package نگهداری نمی‌شود.
- **Central Signing/Trust:** D-107 مبنای V1 است: Windows CNG/Certificate Store، MachineKey، private key غیرقابل‌استخراج، purposeهای License و Provisioning جدا، Trust Store مجزا و fail-closed. `SoftwareProtected` هرگز Hardware/HSM معرفی نمی‌شود. امضا در GitHub Actions انجام نمی‌شود.
- **Scope separation:** D-107 فقط signer مرکزی License/Provisioning/Amendment را پوشش می‌دهد. D-055 برای Customer ACK و Vendor offline-revocation snapshot و D-057 برای anti-rollback مشتری مستقل باقی می‌مانند؛ الزامات آن‌ها را نباید با CNG مرکزی یکی دانست.
- **Approval:** طبق D-109، در Vendor V1 self-approval اپراتور مجاز پذیرفته شده است؛ نباید در مستندات ادعای کنترل دو نفر متفاوت شود. Permission، RowVersion و Audit همچنان الزامی‌اند.
- **Concurrency/Idempotency:** RowVersion، unique indexها، ACK exact-retry و transactionهای approval/issuance حفظ می‌شوند.
- **Audit atomicity:** Audit در تراکنش caller نوشته می‌شود و شکست آن باید mutation مربوط را rollback کند؛ checkpoint مستقل Production هنوز جایگزین این زنجیره نیست.
- **Antiforgery:** mutationهای مرورگر محافظت می‌شوند؛ endpointهای readiness فقط GET هستند.

## ۳. هزینه و پیچیدگی

- Readiness Diagnostics فقط-خواندنی است؛ جدول، Worker، Broker، Cache یا سرویس جدیدی برای آن اضافه نشده است.
- Order/Payment و Amendment فقط مدل‌های موردنیاز Vendor را اضافه کرده‌اند؛ Ledger حسابداری یا Payment Gateway موازی وجود ندارد.
- File checkpoint فقط Development/Test است و نباید به‌عنوان sink مستقل Production معرفی شود.
- رشد Audit باید با retention/archive و sink مستقل کنترل شود؛ نگهداری نامحدود همه رکوردها در دیتابیس عملیاتی تصمیم پیش‌فرض نیست.

## ۴. آمادگی Production

**Not Ready.** موانع واقعی:
1. اجرای Key Ceremony و آزمون ACL/Trust/rotation/recovery برای Windows CNG روی میزبان Production هدف؛ HSM/KMS خارجی برای signer مرکزی License/Provisioning شرط D-107 نیست.
2. تکمیل #392 و فعال‌سازی Gate شرکت‌محور فقط پس از تأیید مالک؛ Migration مشتری در محیط واقعی هنوز اجرا/تأیید نشده است.
3. تکمیل trust distribution/rotation و آزمون دو میزبان/دو OS در #235.
4. انتخاب و provision sink مستقل Audit، retention و recovery drill در #107.
5. تکمیل و آزمون واقعی sync/reconciliation/restore در #394.
6. الزامات جداگانه D-055/D-057 برای offline revocation و Customer anti-rollback باید در حوزه خودشان provision و آزمون شوند.

هیچ کلید، credential، provider یا مقصد Production فرضی اضافه نشده است. نبود تنظیم یا provider معتبر همچنان fail-closed است.

## ۵. وضعیت مأموریت و CI

- **Stage 2 / PR #739:** Merge شده؛ Rebase، tip `2d181e0d801a...`، CI ۱۸/۱۸.
- **Stage 3 / PR #740:** Merge شده؛ Squash `b5f8c53db240...`، CI ۱۸/۱۸ روی head قبل از Merge.
- **Stage 4 / PR #741:** Merge شده؛ Squash `755aeba20032...` از head `811fbb08`، CI ۱۸/۱۸ روی head نهایی؛ شامل هماهنگ‌سازی Overview با D-107/D-109، تست‌های رگرسیون ادعاها، رفع race پاسخ‌های readiness و تفکیک پرداخت آخرین سفارش.
- **Stage 5 / PR #742:** Draft؛ این سند با وضعیت واقعی (SHAها و CI بالا) به‌روز شده است؛ CI این PR باید روی head نهایی خودش سبز شود و نتیجه آن جایگزین این جمله می‌شود.
- **Production Readiness:** Not Ready؛ موارد جدول و بخش ۴ باز هستند.

هیچ PR باز در این زنجیره بدون تکمیل بررسی‌های الزامی Merge نشده است.
