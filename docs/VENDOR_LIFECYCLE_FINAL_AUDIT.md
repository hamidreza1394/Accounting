# ممیزی نهایی چرخه Vendor — مرحله پنجم مأموریت (بازبینی 2026-11-21)

این سند وضعیت Issueها، امنیت، هزینه و آمادگی Production را با main و شاخه‌های جاری تطبیق می‌دهد. وجود کد یا سبز بودن CI به‌تنهایی به معنی تکمیل End-to-End یا Production readiness نیست.

**مبنای کد و Git:**
- main: `ac0ec74115ced3988bc609e1e50ba46866b0920f` (مرحله ۵ ادغام شده)؛ بازبینی این سند روی همین main انجام شده است.
- PR #739 (مرحله ۲): ادغام‌شده با روش Rebase (قاعده `required_linear_history` مخزن Merge Commit را ممنوع می‌کند)؛ ۱۱ commit حفظ‌شده و tip ادغام `2d181e0d801a...`.
- PR #740 (مرحله ۳): ادغام‌شده با Squash؛ commit `b5f8c53db240...`.
- PR #741 (مرحله ۴): ادغام‌شده با Squash؛ commit `755aeba20032...` از head `811fbb08`؛ CI روی head نهایی ۱۸/۱۸ Check Run سبز؛ UI مطابق D-107/D-109 هماهنگ و تست‌های رگرسیون ادعاها اضافه شد (Vitest ۸۳/۸۳).
- دامنهٔ پوشش چک‌ها: تست‌های e2e این چک‌لیست‌ها اپ اصلی (Server/ClientApp) را می‌رانند و Vendor.Web را پوشش نمی‌دهند؛ پوشش UI Vendor در این زنجیره Vitest است. همچنین «Push on main/CodeQL» گردش‌کار CodeQL پیش‌فرض GitHub (dynamic/github-code-scanning/codeql) است و فایل workflow جداگانه در مخزن ندارد؛ نبود فایل به معنی نبود تحلیل نیست.
- PR #742 (این مرحله): Merge شده با Squash؛ commit `ac0ec74115ced3988bc609e1e50ba46866b0920f` از head `7a8c1759`؛ شاخه با main (مرحله ۴ ادغام‌شده) همگام شده بود و علاوه بر مستندات، هم‌راستاسازی readiness با وضعیت‌های ذخیره‌شده و تفکیک پرداخت آخرین سفارش را به‌صورت تغییرات مستقل خود داشت؛ ادغام معنایی بدون Force Push انجام شد؛ CI روی head نهایی ۱۸/۱۸ Check Run موفق (فقط CodeRabbit cancelled) و CI post-merge روی main سبز است.

## ممیزی مجدد مرحله ۰ مأموریت تکمیل Vendor — 2026-11-22 (مبنا `main`@`eba5e336`)

این افزودنی بازممیزیِ پس از ادغام PR #743 است؛ هیچ وضعیت بخش‌های ۱ تا ۴ را بازنویسی نمی‌کند و فقط مبنا و شواهد را تازه می‌کند.

- **دروازهٔ شروع مأموریت:** PR #743 در `2026-11-22T06:06:14Z` با Squash ادغام شد و tip `main` اکنون `eba5e3368dda0c2e859fd9f204603f073b065cf0` است. دلتای `ac0ec741..eba5e336` دقیقاً ۵ فایل Markdown است (`LICENSING_WORKFLOWS.md`، `PROJECT_STATUS.md`، `ROADMAP.md`، همین سند و `WORK_LOG.md`)؛ هیچ commit کد، تست، Workflow یا Migration اضافه نشده است، پس همهٔ وضعیت‌های بخش‌های ۱ تا ۴ روی `eba5e336` همچنان معتبرند.
- **GitHub در snapshot 2026-11-22:** هنگام ممیزی اولیه، #744 (Trade) تنها PR باز مشاهده‌شده بود؛ PR #745 حامل همین ممیزی مستنداتی Vendor است و اکنون PR باز Vendor محسوب می‌شود. #744 خارج از دامنه این مأموریت است. Issueهای باز اولویت‌دار این مسیر شامل #228/#236/#214/#235/#107 و وابستگی‌های #392/#393/#364 هستند.
- **گام بعدی طبق اولویت مأموریت:** ابتدا شکاف‌های Vendor-only در #228 و fulfillment/ACK tracking در #393 بررسی شوند؛ CompanyReference mapping/bootstrap در #392 و Customer consumer/trust/ACK در #235 جدا و خارج از PR محدود به Vendor باقی می‌مانند. Key Ceremony عملیاتی #236، revocation آفلاین #214/#364 و sink مستقل #107 تا تأمین زیرساخت/دسترسی واقعی fail-closed یا NotRun می‌مانند.
- **راستی‌آزمایی کد روی `eba5e336` (جستجوی مستقیم؛ بدون تغییر کد):**
  - `EntitlementPolicyEngine` فقط در `Licensing.Contracts` و `tests/Licensing.Contracts.Tests` است و هیچ call-site عملیاتی در `Server/`، `Vendor/` یا `Infrastructure/` ندارد → اتصال Entitlement به مصرف‌کنندگان واقعی همچنان شکاف مرحله ۱ است.
  - تنها پیاده‌سازی `ITradeModuleEntitlement` در `Server/Infrastructure/Trade/DenyAllTradeModuleEntitlement.cs` است که در `Server/Program.cs:284-285` ثبت می‌شود و `Modules/Trade/Trade.Application/Authorization/TradeAccessGate.cs` آن را مصرف می‌کند؛ تست معماری `tests/Template.Tests/Architecture/TradeEndpointGateTests.cs` همین تک‌پیاده‌سازی DenyAll را می‌پوشاند → Trade Fail-Closed باقی است و قرارداد مشترک آن در مالکیت Trade است.
  - `VendorProvisioningCompanyReferenceGate` پیش‌فرض `Disabled` (کلید `Provisioning:CompanyReferenceIssuance:Enabled`) با interlock کدی `CustomerConsumerReady` است؛ فقط PR حامل Verify نسخهٔ v5 سمت مشتری و mapping معتبر CompanyReference مجاز به تغییر آن است → Gate دوکلیدی بسته است.
  - `VendorProvisioningRequest` فیلدهای `DeliveredAtUtc`/`AcknowledgedAtUtc` دارد و ACK امضاشده با تست SQL Server (claim اتمیک، replay و idempotency) پوشش داده شده؛ اما `VendorLicenseAmendmentRequest` فقط وضعیت صدور (`Requested/PendingApproval/Issued/Rejected/Failed`) را دارد و هیچ فیلد تحویل/ACK/اعمال ندارد → ردیابی تحویل و اعمال Amendment شکاف مرحله ۲ است.
  - زنجیره Audit (`VendorAuditWriter`، `VendorAuditChainVerifier`، `IVendorAuditCheckpointStore` و store فایل فقط Development/Test) در کد است؛ طبق مرز D-046، سیاست نگهداری رخدادهای Vendor باید جداگانه در پیکربندی/راهنمای استقرار و Issue اجرایی تثبیت شود. D-046 مدت عددی تعیین نمی‌کند و D-070 فقط `SecurityAuditEvents` و فرادادهٔ `UserSession` در Customer Host را پوشش می‌دهد؛ برای Vendor دورهٔ نگهداری مصوب/تعریف‌شده‌ای وجود ندارد و Retention/Archive پیاده‌سازی نشده است → مرحله ۳.
  - مسیر اعتبارنامه مدیر اولیه مشتری (رمز تصادفی غیرقابل استفاده + `MustChangePassword` + Enrollment یک‌بارمصرف با hash/انقضا/مصرف اتمیک و rate-limit) در کد است؛ اما مکانیزم‌های خاص D-110 — انتخاب نام کاربری توسط مشتری (payload نسخهٔ بعدی؛ نیازمند تصمیم مالک)، توکن Bootstrap امضاشدهٔ یک‌بارمصرف مقید به Tenant/Installation و بازیابی امن پس از مصرف — مطابق وضعیت ADR `NotImplemented` هستند (جستجوی `TenantOwner`/`InitialAdministrator` در کد مشتری نتیجه ندارد).
  - Signer: `WindowsCng*` (Provider/Options/CertificateStore module/فرمان Certificate)، Runbook Key Ceremony (PR #736؛ `Vendor/README.md` و `runbooks/ISSUE-236-CNG-KEY-CEREMONY-ROTATION.md`) و مسیر HSM `Pkcs11SigningProvider` با enablement opt-in برای snapshot ابطال آفلاین (D-055) و سرویس `VendorOfflineRevocation*` در کد هستند؛ Key Ceremony واقعی روی میزبان هدف و ACL/Trust Store/rotation/recovery همچنان Not Verified است.
  - پوشش تست: چهار پروژهٔ تست (`Template.Tests`، `Template.Tests.Js`، `Vendor.Host.Tests`، `Licensing.Contracts.Tests`)؛ چک‌لیست‌های e2e CI اپ اصلی (Server/ClientApp) را می‌رانند و Vendor.Web را پوشش نمی‌دهند (پوشش UI Vendor در این زنجیره Vitest است) → تفکیک و تکمیل تست واقعی Vendor مرحله ۴ است.

## اتصال Entitlement به مصرف‌کنندگان عملیاتی Vendor — مرحله ۱ (2026-11-22، شاخهٔ باز)

این افزودنی وضعیت شکاف مرحله ۱ (بند ممیزی مجدد بالا) را بر پایهٔ شاخهٔ `glm/vendor-stage1-entitlement-consumers` به‌روز می‌کند؛ تا Merge و سبز شدن CI، وضعیت «Implemented / در انتظار CI و بررسی مالک» است، نه Verified:

- `EntitlementPolicyEngine` اکنون در سه نقطهٔ عملیاتی تأییدشدهٔ Vendor مصرف می‌شود: تصمیم subset زیرسیستم‌های v1 در `VendorProvisioningLicenseResolver` (مسیر زندهٔ صدور بسته‌های v1–v4)، انتخاب شرکت و بررسی ماژول در `VendorProvisioningCompanyReferenceRules` (مسیر v2؛ همچنان پشت Gate دوکلیدی بسته)، و Gate سیاستی payload نهایی amendment در `VendorAmendmentEntitlementPolicy` پیش از امضا (ضد-لغو entitlement اعطاشده).
- تست گارد معماری `VendorEntitlementEnforcementGuardTests` مصرف Engine را به همین allow-list محدود و interlock بستهٔ v5 (`CustomerConsumerReady=false`) را تثبیت می‌کند؛ تست موجود `TradeEndpointGateTests.ExactlyOneEntitlementImplementationShipsAndItDeniesAll` نیز بسته‌ماندن Trade را پوشش می‌دهد.
- Feature grant و Seat در سطح payload عمداً با سمانتیک مصرفی Engine ارزیابی نمی‌شوند: feature غیرفعال وضعیت پیش از اعطا است و شمارش عضویت/شرکت واقعی در Vendor وجود ندارد (D-054). جزئیات و نتایج واقعی تست در handoff مرحله ۱ آمده است.
- محدودیت این بخش: تغییر روی شاخهٔ باز است؛ هیچ ادعای CI یا Production readiness بدون شاهد Check Run ثبت نشده است.

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
- **Stage 5 / PR #742:** Merge شده؛ Squash `ac0ec74115ced3988bc609e1e50ba46866b0920f` از head `7a8c1759`؛ CI روی head نهایی ۱۸/۱۸ Check Run موفق و post-merge main سبز است؛ این سند با وضعیت واقعی (SHAها و CI بالا) به‌روز شده است.
- **Production Readiness:** Not Ready؛ موارد جدول و بخش ۴ باز هستند.

هیچ PR باز در این زنجیره بدون تکمیل بررسی‌های الزامی Merge نشده است.
