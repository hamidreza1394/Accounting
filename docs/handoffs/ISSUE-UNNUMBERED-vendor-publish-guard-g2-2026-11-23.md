# تحویل وظیفه — ISSUE-UNNUMBERED-vendor-publish-guard-g2-2026-11-23

- **Issue:** `UNNUMBERED` (شماره جعلی ساخته نشده است؛ مرحلهٔ اجرایی اول مأموریت «ادامه توسعه Vendor پس از ممیزی 2026-11-23» — تکمیل گارد Publish در D-041)
- **Agent/مسئول:** GLM — مالکیت فنی: Vendor، Licensing، Provisioning
- **تاریخ:** 2026-11-23
- **وضعیت:** انجام‌شده (Implemented)؛ تأیید نهایی فهرست Allow-list با مالک و راستی‌آزمایی artifact ویندوزی در CI باقی است

## هدف و معیارهای پذیرش

- [x] بررسی `scripts/publish.sh` و `scripts/publish.ps1` و کتابخانه‌های آن‌ها (`scripts/lib/publish-output.sh`، `scripts/lib/PublishOutput.ps1`)؛ گارد قبلی فقط Block-list بود (`Vendor.*`، `.pfx/.p12/.key`، PEM) و Allow-list الزامی D-041/G2 نداشت.
- [x] بررسی Workflowهای Publish و تست‌های موجود (`publish-scripts`، `publish-windows`، `publish`، `e2e` در `.github/workflows/dotnet.yml`؛ `scripts/tests/publish-content-guard.test.sh`، `scripts/tests/publish-output.test.sh`، `scripts/tests/publish-output.tests.ps1`).
- [x] استخراج فهرست واقعی فایل‌ها/اسمبلی‌ها از خروجی Publish واقعی — بدون حدس: Publish قابل‌حمل Framework-Dependent روی Linux با SDK 10.0.404، مبنا `main@560ff289`؛ ۸۹۰۴ فایل شامل ۴۱ DLL و ۱۳ PDB ریشه، ۱۴ پوشهٔ Satellite، ۱۴ مسیر `runtimes/`، Apphost `Server` و محتوای `wwwroot/`.
- [x] تکمیل Allow-list به‌گونه‌ای که خروجی مجاز صریح باشد و موارد ناشناخته Fail-Closed رد شوند.
- [x] اطمینان از عدم انتشار `Vendor.*`، Private Key/Certificate Private Material و Secret در خروجی Customer (کنترل‌های منفی قبلی دست‌نخورده و اکنون زیرمجموعهٔ گارد نهایی‌اند).
- [x] تست Fixture برای موارد مجاز و غیرمجاز (۸ مورد جدید مثبت/منفی در هر دو پیاده‌سازی bash و PowerShell).
- [x] اجرای Publish واقعی Linux (اجرا شد؛ کامل با کلاینت) و Smoke test سرور منتشرشده. **Publish واقعی Windows: اجرا نشد (NotRun)** — میزبان ویندوز در دسترس نیست؛ معادل آن Job `publish-windows` در CI است که اکنون تست گارد PowerShell را هم اجرا می‌کند.
- [ ] **برای تأیید مالک:** پذیرش نهایی فهرست `scripts/lib/publish-assembly-allowlist.txt`؛ تا آن زمان G2 «Complete» اعلام نمی‌شود.

## خلاصه تغییرات

- فایل دادهٔ جدید `scripts/lib/publish-assembly-allowlist.txt`: منبع واحد حقیقت Allow-list با چهار نوع رکورد (`root:` برای اسمبلی/PDB/Apphost ریشه، `satellite:` برای نام پایهٔ `.resources.dll`، `culture:` برای پوشه‌های Satellite مجاز، `runtime:` برای مسیر دقیق زیر `runtimes/`). همهٔ رکوردها از خروجی واقعی استخراج شده‌اند و `Server.exe` فقط به‌عنوان Apphost ویندوزی پیش‌بینی شده است. مقایسه‌ها Case-Insensitive هستند تا رفتار فایل‌سیستم Windows با Linux یکدست بماند.
- `scripts/lib/publish-output.sh`: تابع `faranou_assert_customer_publish_clean` اکنون پس از کنترل‌های منفی قبلی، Allow-list را هم اعمال می‌کند — هر `.dll/.exe/.so/.dylib` و هر فایل ریشهٔ بدون پسوند (Apphost) باید مجاز باشد؛ زیر `runtimes/` فقط مسیرهای دقیقِ فهرست؛ خارج از `wwwroot/` و `runtimes/` فقط `<culture>/<satellite>.resources.dll` مجاز؛ هر چیز دیگر Fail-Closed رد می‌شود. فایل Allow-list ناخوانا/ناقص خودش خطا می‌دهد (Fail-Closed).
- `scripts/lib/PublishOutput.ps1`: معادل دقیق همان منطق (`Assert-FaranouPublishAllowList`) که انتهای `Assert-FaranouCustomerPublishClean` فراخوانی می‌شود؛ انتشار مشتری Windows با `scripts/publish.ps1` همان گارد را می‌گیرد.
- `scripts/tests/publish-content-guard.test.sh` (+۸ مورد) و فایل جدید `scripts/tests/publish-content-guard.tests.ps1` (۱۲ مورد، mirror تست bash): درخت مجاز مشتق‌از-پروب، اسمبلی غیرمجاز، Satellite در جای اشتباه، فرهنگ ناشناخته، نام پایهٔ ناشناخته، مسیر غیرمنتظرهٔ `runtimes/`، باینری در پوشهٔ ناشناخته و فایل ریشهٔ بدون پسوندِ غیرمجاز.
- `.github/workflows/dotnet.yml`: اجرای تست گارد PowerShell در Jobهای `publish-scripts` (ubuntu) و `publish-windows` تا هر دو پیاده‌سازی در CI پوشش داده شوند.
- پیام‌های مرحلهٔ گارد در `scripts/publish.sh` و `scripts/publish.ps1` به‌روزرسانی شد (ارجاع به allow-list).
- هیچ کد C#، Entity، Migration، Schema، Workflow تجاری یا پیکربندی Runtime تغییر نکرد؛ هیچ Gate یا فلگی باز نشد.

## فایل‌های مهم تغییرکرده

- `scripts/lib/publish-assembly-allowlist.txt` — جدید؛ دادهٔ Allow-list از شواهد واقعی
- `scripts/lib/publish-output.sh` — اعمال Allow-list در گارد G2 (bash)
- `scripts/lib/PublishOutput.ps1` — اعمال Allow-list در گارد G2 (PowerShell)
- `scripts/tests/publish-content-guard.test.sh` — ۱۱ Fixture جدید
- `scripts/tests/publish-content-guard.tests.ps1` — جدید؛ mirror PowerShell
- `.github/workflows/dotnet.yml` — دو گام تست جدید در `publish-scripts` و `publish-windows`
- `scripts/publish.sh`، `scripts/publish.ps1` — پیام مرحلهٔ گارد
- `docs/DECISIONS.md` — پیگیری گاردهای D-041 (افزودهٔ 2026-11-23)
- `docs/VENDOR_OPERATIONAL_READINESS_CHECKLIST.md` — ردیف G2 به‌روزرسانی شد
- `docs/WORK_LOG.md`، `docs/INDEX.md` — رکورد و ارجاع handoff

## تصمیم‌های فنی

- **باگ از پیش موجود که همین PR پیدا و رفع کرد:** اسکن PEM در `PublishOutput.ps1` زیر `ErrorActionPreference=Stop` بی‌اثر بود — `Write-Error` داخل همان `try/catch` خواندن فایل پرتاب و بلعیده می‌شد و `found` هرگز ست نمی‌شد؛ یعنی مسیر Publish ویندوزی (`publish.ps1` با `EAP=Stop`) تا امروز هرگز محتوای PEM را واقعاً رد نمی‌کرد. این نقص توسط تست Fixture جدید «PEM private key content rejected» و شکست واقعی اولین اجرای CI (`publish-scripts`, job `137594470529`) آشکار شد. اصلاح: Hits ابتدا جمع‌آوری و بعد از حلقه گزارش می‌شوند؛ `catch` فقط فایل‌های ناخوانا را می‌گذارد. رفتار bash از ابتدا درست بود و تغییری نکرد.
- **Allow-list = مجوز، نه الزام:** گارد فقط «هر فایل موجود باید مجاز باشد» را کنترل می‌کند؛ نبود فایل مجاز خطا نیست (سایر چک‌های الزامی مانند `index.html` و باندل‌ها در `publish.sh` باقی‌اند). این دقیقاً مفاد D-041 («هر اسمبلی خارج از آن رد می‌شود») است و Fixtureهای قدیمی را نمی‌شکند.
- **مقایسهٔ Case-Insensitive:** در هر دو پیاده‌سازی (bash با کلید lowercase، PowerShell با `ToLowerInvariant`) تا رفتار فایل‌سیستم حساس Linux و غیرحساس Windows یکدست بماند.
- **`wwwroot/` از Allow-list مستثناست** چون محتوای استاتیک کلاینت است و با `check-offline-build.mjs` و چک‌های فایل الزامی کنترل می‌شود؛ اسکن‌های منفی (`Vendor.*`، `.pfx/.p12/.key`، PEM) همچنان کل درخت را می‌پوشانند.
- **`Server.exe` در فهرست است ولی در خروجی Linux نیست:** Publish قابل‌حمل FDD بدون `-r` روی هر دو OS چیدمان یکسان دارد (درخت `runtimes/` کامل) و تنها تفاوت انتظاری Apphost است؛ راستی‌آزمایی نهایی با Job `publish-windows` در CI انجام می‌شود. اگر خروجی ویندوز فایل دیگری داشته باشد، گارد همان‌جا Fail-Closed رد می‌کند و افزودن رکورد تصمیم مالک است — این خودش رفتار درست گارد است.
- تصمیم معماری جدیدی در سطح محصول گرفته نشد؛ این پیاده‌سازی صرفاً تکمیل گارد مصوب D-041 بند ۴-آیتم G2 و «پیگیری گاردها» است.

## بررسی و آزمون

| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| `bash scripts/tests/publish-content-guard.test.sh` (قبل از پیاده‌سازی) | RED — دقیقاً ۷ مورد منفی جدید شکست خورد (7 passed / 7 failed) | TDD مرحلهٔ RED |
| `bash scripts/tests/publish-content-guard.test.sh` (بعد از پیاده‌سازی) | سبز — 14 passed, 0 failed | TDD مرحلهٔ GREEN |
| `./scripts/tests/publish-content-guard.tests.ps1` — اجرای اول در CI (`publish-scripts`, job `137594470529`) | 13 passed / 1 failed — تنها شکست: Fixture PEM؛ ریشه‌یابی: باگ از پیش موجود اسکن PEM زیر `EAP=Stop` (بند تصمیم‌های فنی) | شاهد تفاوت رفتار bash/PowerShell |
| همان تست پس از اصلاح اسکن PEM | در اجرای بعدی CI گزارش می‌شود | |
| فراخوانی مستقیم گارد روی خروجی واقعی `artifacts/publish-probe` (۸۹۰۴ فایل) | PASS بدون هیچ پیام | خروجی واقعی با Allow-list می‌گذرد |
| تزریق `NotListed.Library.dll` در خروجی واقعی | PASS گارد رد شد — پیام: `file outside the D-041 publish allow-list: NotListed.Library.dll` | رفتار Fail-Closed روی artifact واقعی |
| `scripts/publish.sh artifacts/publish` (کامل: npm ci/typecheck/test/build + dotnet publish + چک‌ها + گارد G2) | موفق — ۱۱۲ فایل تست / ۱۲۸۸ تست کلاینت سبز، Offline check سبز، گارد سبز، خروجی کامل | اجرای واقعی Linux (SDK 10.0.404) |
| `scripts/smoke-published-app.sh artifacts/publish` | موفق — همهٔ مسیرهای HTTP (۲۰۰/۴۰۱) و رندر توکن Anti-forgery | سرور واقعاً از خروجی Publish بالا می‌آید |
| `./scripts/tests/publish-content-guard.tests.ps1` | **NotRun** — pwsh در این محیط نصب نیست | در CI روی ubuntu (`publish-scripts`) و Windows (`publish-windows`) اجرا می‌شود |
| Publish واقعی Windows با `scripts/publish.ps1` | **NotRun** — میزبان Windows در دسترس نیست | Job `publish-windows` در CI معادل آن است؛ ریسک فایل اضافهٔ ویندوزی Fail-Closed رد می‌شود و در آن صورت افزودن رکورد به Allow-list تصمیم مالک است |
| `git diff --check` | پاک | بدون فاصلهٔ سفید خطا |

## امنیت، داده و سازگاری

- هیچ کلید، Secret، Connection String یا دادهٔ واقعی در تغییرات نیست؛ Fixtureها با متن ساختگی (`TEST-FIXTURE-NOT-A-REAL-KEY`) هستند.
- Publish مشتری اکنون Fail-Closed نسبت به هر باینری ناشناخته است؛ اسمبلی `Vendor.*` هرگز به بستهٔ مشتری راه ندارد و این با G1 (تست وابستگی) و مرز `Licensing.Contracts` سازگار است — `Faranou.Licensing.Contracts.dll` عضو مجاز فهرست است (اشتراک مجاز D-041 بند ۳).
- سازگاری Backward: چک‌های منفی قبلی و چیدمان Publish بدون تغییر؛ فقط سطح سخت‌گیری گارد بالاتر رفته است. اگر روزی Package جدید NuGet به وابستگی‌های Server اضافه شود، CI ناموفق می‌شود و افزودن رکورد یک تغییر یک‌خطی بازبینی‌شدنی است.

## ریسک‌ها و کارهای باقی‌مانده

- **تأیید مالک:** فهرست `publish-assembly-allowlist.txt` فقط پیشنهاد مستند به شواهد است؛ پذیرش نهایی آن (و هر تغییر آتی آن) تصمیم مالک است.
- **راستی‌آزمایی ویندوز:** artifact واقعی Windows فقط در CI قابل اثبات است؛ تا آن زمان گارد ویندوزی با تست‌های Fixture و منطق مشترک پوشش داده شده است.
- اگر نسخهٔ SDK/Package آینده Satellite یا فایل نیتیو جدیدی بیاورد، گارد CI را Fail می‌کند؛ به‌روزرسانی آگاهانهٔ فهرست بخشی از نگهداری عادی است.
- G2 همچنان «Complete عملیاتی» اعلام نمی‌شود؛ معیار کامل‌شدن همان ستون شواهد چک‌لیست آمادگی است (تأیید مالک + artifact دو سیستم‌عامل).

## شناسه‌های Git

- Branch: `glm/vendor-publish-guard-g2` (از `main@560ff289`)
- Commit: ثبت پس از Push؛ در PR گزارش می‌شود
- Pull Request: لینک در WORK_LOG و گزارش نهایی مأموریت
