# وضعیت جاری تأییدشده Vendor — 2026-10-28 (پس از PR #226)

این بخش، وضعیت جاری مبتنی بر کد و CI است؛ بخش‌های قدیمی‌تر این سند سابقهٔ تاریخی‌اند و نباید به‌عنوان وضعیت امروز خوانده شوند.

## قابلیت‌های تأییدشده در main
- **CRUD تجاری:** فرم‌های ثبت/ویرایش مشتری و نصب، ثبت/ویرایش قراردادهای Draft و ثبت/ویرایش metadata لایسنس‌های Draft به API واقعی Vendor متصل‌اند. ویرایش قرارداد/لایسنس پس از خروج از Draft در UI پنهان و در API رد می‌شود.
- **کنترل‌های نوشتن:** مجوزدهی سمت سرور و resource authorization، antiforgery token تازه برای mutation، allow-list ورودی و RowVersion برای ویرایش؛ خطاهای تعارض و ورودی نامعتبر با پاسخ‌های مشخص مدیریت می‌شوند.
- **روابط تجاری:** فهرست‌های قرارداد/نصب/لایسنس امکان فیلتر server-side بر اساس `customerId` دارند. ایجاد/ویرایش لایسنس در API بررسی می‌کند قرارداد و نصب انتخاب‌شده متعلق به همان مشتری باشند؛ این قاعده فقط به UI واگذار نشده است.
- **Audit:** رخدادهای mutation در تراکنش داده نوشته می‌شوند؛ تست SQL Server rollback در صورت شکست audit و تست‌های HTTP احراز هویت/تغییر وضعیت در CI وجود دارند.
- **بهینه‌سازی Audit (Issue #202، PR #203):** ایندکس غیر یکتای PreviousHash و ایندکس یکتای EventHash با Migration مستقل Vendor ادغام شده‌اند. Vendor CI (run 40563642222) و .NET CI (run 40563642237) روی head نهایی PR موفق شدند؛ این بخش Verified است. این بهینه‌سازی جایگزین checkpoint مستقل Audit نیست.
- **نسخه‌بندی و یکتایی زنجیرهٔ ممیزی (Issue #107، PR #212):** `HashVersion=1` برای رویدادهای قدیمی و جدید، رد fail-closed نسخهٔ ناشناخته، preflight دادهٔ ناسازگار و unique index روی `PreviousHash` اضافه شد. Vendor CI روی PR run 40650985169 (77/77 تست) و پس از ادغام run 40651677915 موفق است؛ .NET CI روی PR run 40650985057 موفق شد. مقصد مستقل checkpoint، اجرای مستقیم CLI `dotnet ef migrations add` و کنترل‌های عملیاتی Production هنوز تأیید نشده‌اند.
- **رابط:** Vendor UI مستقل React + TypeScript strict + Vite + Tailwind/shadcn، فارسی RTL و assetهای محلی است. `ManagedSelect` گزینهٔ خالی معتبر مانند «بدون قرارداد» را در کنار گزینه‌های فعال پشتیبانی می‌کند.
- **لایسنس:** PRهای #221–#224 در main ادغام شده‌اند: پاکت امضاشده در دیتابیس Vendor پایدار ذخیره می‌شود؛ صدور پاکت، sequence، وضعیت لایسنس، تصمیم maker-checker و audit در یک تراکنش ثبت می‌شوند. تست SQL Server برای صدور موفق، rollback در شکست audit، تعارض sequence و رقابت صدور وجود دارد. این قابلیت هنوز به معنی آمادگی Production یا فعال‌سازی آفلاین مشتری نیست.
- **امضا و ذخیره‌سازی پاکت (PR #218–#224):** قرارداد ILicenseSigningProvider، ساخت payload canonical، کارخانهٔ envelope، Migration 20261028150000_VendorSignedLicenseEnvelopes و سرویس صدور تراکنشی در main هستند. Provider پیش‌فرض UnavailableLicenseSigningProvider عمداً fail-closed است؛ هیچ کلید خصوصی یا fallback نرم‌افزاری وجود ندارد. امضای Production مبتنی بر KMS/HSM هنوز پیاده‌سازی/پیکربندی نشده است.
- **Issue #214 — گردش‌کار تأیید لایسنس:** PRهای #217–#224 در main ادغام شده‌اند؛ HTTP tests برای antiforgery، RowVersion کهنه، منع self-approval، صدور fail-closed و جداسازی منبع/مشتری وجود دارند. Issue #214 باز است چون signer واقعی Production، پروتکل ابطال آفلاین، runbook و آزمون عملیاتی هنوز تکمیل نشده‌اند.

- **تثبیت E2E ورود (Issue #225، PR #226):** helper ورود Playwright تا پایان navigation فرم POST منتظر می‌ماند، چه ورود موفق باشد و redirect شود چه ورود رد شود و صفحهٔ Login دوباره render شود. .NET CI روی PR head 62fd3e93f64c682a9141ab98d8875f591bcb1468 موفق شد؛ شامل Browser tests با SQL Server، publish Linux/Windows، build/test، client و secret scan.

## شواهد CI
- PR #196 با merge commit `45e843ebe639b0013e9b0bc9d28106eb435e4742` ادغام شد.
- Vendor CI روی head نهایی PR: [40550673063](https://github.com/hamidreza1394/Accounting/actions/runs/40550673063) — موفق؛ شامل تست‌های UI و SQL Server integration، build و publish مستقل Vendor.
- .NET CI روی head نهایی PR: [40550672932](https://github.com/hamidreza1394/Accounting/actions/runs/40550672932) — موفق؛ شامل build/test، گاردهای Migration، E2E، انتشار Linux/Windows و secret scan.
- PR #207 با merge commit `d644ab5d36d00b1ce3763b64f17fd6fe7ef9fbac` ادغام شد؛ Vendor CI [40570240409](https://github.com/hamidreza1394/Accounting/actions/runs/40570240409) و .NET CI [40570240205](https://github.com/hamidreza1394/Accounting/actions/runs/40570240205) روی HEAD نهایی موفق شدند؛ شامل UI/Vitest، asset budget، SQL Server integration، EF snapshot guard، E2E، انتشار Linux/Windows و secret scan.
- شواهد بالا تأیید محدودهٔ تست‌شده‌اند و جایگزین آزمون نفوذ، عملیات Production یا بارسنجی نیستند.

## شکاف‌های باقی‌مانده پیش از Production
- **Bootstrap مدیر اول:** فرمان تعاملی یک‌باره و تست‌های Integration آن در کد موجودند؛ Vendor CI روی HEAD نهایی PR #207 موفق است. اجرای عملیاتی در محیط واقعی، گردش بازیابی و تنظیم Secret Store/Data Protection برای هر استقرار هنوز باید با runbook و دسترسی‌های واقعی تأیید شود.
- **RBAC و resource authorization:** کاتالوگ permission نسخه‌دار، policyهای سمت سرور، مدیریت نقش/تخصیص و `VendorResourceAccessEvaluator` مبتنی بر رکوردهای ذخیره‌شده در کد موجودند؛ تست‌های Vendor در CI سبز هستند. بازبینی مستقل کامل بودن ماتریس مجوزها و جداسازی وظایف، و تأیید عملیاتی حداقل دو مدیر Production هنوز باقی است.
- **Maker-Checker مشتری/نصب:** PR #207 با merge commit `d644ab5d36d00b1ce3763b64f17fd6fe7ef9fbac` ادغام و Verified شد؛ Vendor CI [40570240409](https://github.com/hamidreza1394/Accounting/actions/runs/40570240409) و .NET CI [40570240205](https://github.com/hamidreza1394/Accounting/actions/runs/40570240205) سبز هستند. این پوشش تغییر وضعیت مشتری/نصب است؛ تأیید دو نفره برای صدور/ابطال لایسنس هنوز باقی است.
- **Checkpoint مستقل Audit:** در شاخهٔ Issue #107 قرارداد canonical/versioned و سرویس ایجاد checkpoint پس از verification کامل اضافه شده‌اند، اما هنوز CI-Verified نیستند و هیچ adapter مستقل عملیاتی ثبت نشده است. مقصد immutable با credential جدا، مجوزهای SQL append-only، پایش/retention و runbook همچنان Production blocker هستند؛ hash-chain در همان DB به‌تنهایی در برابر DBA کافی نیست.
- صدور امضاشده با کلید خارج از برنامه (KMS/HSM یا سرویس امضای تأییدشده)، activation آفلاین و چرخهٔ وضعیت لایسنس پیاده‌سازی و آزمون نشده‌اند.
- تست مرورگر/عملیات روی محیط واقعی، backup/restore، بازیابی حادثه و ارزیابی ظرفیت/امنیت Production هنوز باید جداگانه انجام شوند.
- ایجاد شرکت حسابداری، دیتابیس یا اعتبارنامهٔ مشتری از پنل Vendor عمداً انجام نمی‌شود؛ provisioning مشتری نیازمند معماری مستقل و تأییدشده است.

**نتیجه:** CRUD و UI metadata تجاری در محدودهٔ PRهای ادغام‌شده Verified هستند؛ سامانهٔ Vendor هنوز Production-ready اعلام نمی‌شود تا شکاف‌های امنیتی/عملیاتی بالا بسته و تأیید شوند.

---

## Vendor follow-up — Identity SchemaVersion 3 and migration drift (2026-10-26, PR #138)

- **Confirmed root cause:** the SQL Server integration diagnostic compared the runtime design-time model with the migration snapshot and reported five Identity column changes plus a missing passkey UserId index/foreign key. The affected columns are VendorAdminUserTokens.Name, VendorAdminUserTokens.LoginProvider, VendorAdminUsers.PhoneNumber, and both key columns in VendorAdminUserLogins.
- **Migration work in progress:** 20261026084500_VendorAlignIdentitySchemaV3 aligns the model with IdentitySchemaVersions.Version3; its migration target model and VendorDbContextModelSnapshot are updated. Because SQL Server rejects altering columns that participate in primary keys, the migration drops and recreates the affected Identity token/login primary keys around the width changes.
- **CI gate:** PR #137 merged a temporary continue-on-error on the Vendor snapshot check. PR #138 removes that bypass and orders the SQL Server integration test before the strict EF snapshot gate so diagnostic failures remain visible.
- **Verification status:** Not verified. Earlier SQL Server runs exposed the model drift and then the primary-key dependency; the latest migration revision still requires a fresh Vendor CI run, successful dotnet ef migrations has-pending-model-changes, SQL Server integration tests and publish checks. Do not merge PR #138 while any of these fail.
- **Isolation:** no customer accounting DbContext, customer migration or customer database schema was changed.
- **Remaining Production blockers:** secure one-time first-admin bootstrap, forced password-change flow, fine-grained RBAC, maker-checker, audit event wiring and independent checkpoints, and real customer/installation/contract/license APIs plus the signing/KMS/HSM workflow.

---


## Issue #130 — استاندارد RTL فارسی و کمبوباکس قابل مدیریت
- فایل `Vendor/Vendor.Web/index.html` از قبل `lang="fa"` و `dir="rtl"` داشت؛ این مبنا حفظ شد.
- استایل Vendor اکنون جهت/تراز فارسی را صریح می‌کند و برای کنترل‌های جهت‌دار از خواص منطقی CSS استفاده می‌کند. نام کاربری با `dir="auto"` برای متن فارسی/لاتین و رمز عبور و OTP با جهت LTR تعریف می‌شوند تا تایپ فنی به‌هم نریزد.
- کامپوننت `ManagedSelect` برای فرم‌های Vendor اضافه شد: انتخاب اولین گزینه معتبر به‌صورت پیش‌فرض، امکان مقدار کنترل‌شده، label و پیام خطای دسترس‌پذیر، و نمایش «افزودن گزینه» فقط با `canManageOptions` و callback صریح.
- این کامپوننت خودش گزینه‌ها را ذخیره نمی‌کند؛ مجوز باید از API سمت سرور بیاید و callback باید به endpoint واقعی متصل شود. هیچ API یا CRUD تجاری جعلی در این مرحله اضافه نشده است.
- مرز معماری: شرکت‌های حسابداری (از جمله مستقل/مادر/زیرمجموعه) متعلق به دیتابیس نصب مشتری‌اند، نه دیتابیس تجاری Vendor. Vendor مشتری تجاری، نصب و مجوز را مدیریت می‌کند. ایجاد شرکت حسابداری از پنل مرکزی بدون طرح provisioning مستقل و تأییدشده انجام نمی‌شود.
- تست‌های جدید برای جهت RTL، پیش‌فرض اولین گزینه، تغییر انتخاب، پنهان بودن مدیریت گزینه بدون مجوز و پیام خطا اضافه شده‌اند.
- اعتبارسنجی محلی در این محیط اجرا نشد؛ وضعیت تا نتیجه Vendor CI روی PR **Implemented / Not yet Verified** است.

> **Vendor CI follow-up — 2026-10-26:** در PR #129، اجرای run [40280056742](https://github.com/hamidreza1394/Accounting/actions/runs/40280056742) تأیید کرد TypeScript، تست‌های UI، runtime dependency audit، Vite build، بودجهٔ bundle، .NET build و EF CLI snapshot check موفق‌اند؛ اما `dotnet test` در تست احراز هویت به `PendingModelChangesWarning` هنگام Migration runtime برخورد کرد. بررسی Host دو ثبت `AddDbContext<VendorDbContext>` را یافت؛ ثبت تکراری حذف شد تا تنظیم Migration Assembly از `AddVendorPersistence` حفظ شود. این اصلاح هنوز به اجرای مجدد integration tests نیاز دارد؛ وضعیت Verified/سبز نیست. [PR #129](https://github.com/hamidreza1394/Accounting/pull/129).

> **Vendor UI follow-up — Issue #128 (2026-10-26):** پس از ادغام PR #127، بررسی کد نشان داد UI هنوز endpointهای واقعی Passkey/WebAuthn و enrollment مربوط به TOTP را مصرف نمی‌کند. شاخهٔ `feat/issue-128-vendor-passkey-mfa-ui` این جریان‌ها را با WebAuthn بومی، نگهداری راز TOTP فقط در حافظه، پیام‌های خطای فارسی و تست‌های UI اضافه می‌کند. Vendor CI روی main در run [40279335977](https://github.com/hamidreza1394/Accounting/actions/runs/40279335977) شکست خورده بود: تست مسیر SPA با رفتار fallback ناسازگار بود و connection string تست با دیتابیس مورد انتظار authentication test تطبیق نداشت. این دو مورد در شاخه اصلاح شده‌اند؛ تا اجرای CI جدید، تغییرات **Verified نیستند**. هنوز CRUD تجاری و آمادگی Production خارج از این مرحله است.

> **Vendor UI — بهبود عملکرد 2026-10-26:** در شاخهٔ `feat/issue-126-vendor-ui-shell`، Host برای assetهای hashدار Vite در `/assets` کش بلندمدت immutable و برای `index.html` سیاست `no-store` اعمال می‌کند تا هم بارگذاری‌های بعدی سریع‌تر باشند و هم نسخهٔ جدید HTML به فایل‌های قدیمی اشاره نکند. در CI، npm/typecheck/UI tests/audit/build و .NET build موفق بودند؛ تست‌های .NET در run 40277588682 به‌علت متغیر اتصال VendorDb پیکربندی‌نشده شکست خوردند و workflow اصلاح شده است. تا اجرای مجدد CI و آزمون مرورگر/Lighthouse، این تغییر Verified یا اندازه‌گیری‌شده از نظر سرعت محسوب نمی‌شود. [PR #127](https://github.com/hamidreza1394/Accounting/pull/127).

> **Vendor UI — 2026-10-26 (Issue #126, شاخه feat/issue-126-vendor-ui-shell):** اسکلت مستقل Vendor/Vendor.Web شامل React/TypeScript strict/Vite/Tailwind و کامپوننت‌های shadcn، صفحه ورود RTL با password→TOTP متصل به APIهای واقعی، shell مدیریتی و build به Vendor/Vendor.Host/wwwroot ایجاد شده است. Host برای static assets و SPA routeها تغییر یافته و Vendor CI اکنون npm ci/typecheck/test/build و audit وابستگی‌های runtime را اجرا می‌کند. این تغییر هنوز فقط در شاخه است؛ تا CI واقعی سبز نشود Verified نیست و تا Merge در main وضعیت آن Implemented in branch است. صفحات مشتری/نصب/لایسنس placeholder هستند؛ bootstrap مدیر، RBAC و audit checkpoint مستقل همچنان مانع Production هستند. در Vendor CI run 40277399614، npm ci، typecheck، تست‌های UI و build فرانت‌اند موفق شدند؛ Build دات‌نت در commit اولیه به‌علت usingهای مفقود در Program.cs شکست خورد. usingهای لازم در commit 3d317ea206087ae026520a84ada49f61941bf139 اضافه شدند؛ اجرای CI برای commit اصلاحی هنوز در صف است. تست محلی توسط Agent اجرا نشده است.

> **UI component standard — 2026-10-26 (D-048):** shadcn/ui با RTL به‌عنوان استاندارد رابط React پذیرفته شده است. شاخهٔ `feat/shadcn-ui-foundation` پیکربندی پایه و `docs/UI_COMPONENTS.md` را اضافه می‌کند. این مرحله هنوز نصب CLI/dependencies، به‌روزرسانی lockfile و افزودن کامپوننت واقعی را انجام نداده؛ بنابراین shadcn/ui در وضعیت `Partially Implemented` است و تا CI مرحلهٔ نصب سبز نشود نباید به‌عنوان وابستگی قابل استفاده معرفی شود.

# وضعیت پروژه

> **به‌روزرسانی 2026-10-26 — Vendor L2.2 (PR #102 / Issue #117):** D-047 برای استقرار ابری و سرور اختصاصی `Accepted` است. `Vendor.sln`، Host مستقل، `VendorDbContext`، مدل‌های `VendorAdminUser` و `VendorAuditEvent` و Migration اولیه در PR #102 ادغام شده‌اند. CI آن PR (`40220512121` Vendor CI و `40220512124` .NET CI) سبز بود؛ این اجرا بررسی `dotnet ef migrations has-pending-model-changes` را پوشش داد، اما اجرای Migration روی دیتابیس SQL Server اختصاصی در تست integration جداگانه هنوز با Issue #117 اضافه می‌شود. در Issue #117، ثبت DI و Identity stores به ماژول Persistence منتقل و تست‌های مدل، fail-closed و SQL Server integration افزوده شده‌اند. Vendor CI run [40272872248](https://github.com/hamidreza1394/Accounting/actions/runs/40272872248) روی PR #118 سبز شد؛ Restore، Build، model-snapshot check، تست‌ها از جمله Migration واقعی SQL Server و Publish موفق بودند. .NET CI run [40272872260](https://github.com/hamidreza1394/Accounting/actions/runs/40272872260) هنوز در حال اجراست؛ تا پایان آن وضعیت کلی PR نهایی نیست. ورود مدیر، MFA/passkey/TOTP، bootstrap، RBAC، audit writer/verifier/checkpoint، maker-checker، داده‌های تجاری مشتری/قرارداد/لایسنس، امضا و KMS/HSM هنوز کامل نیستند؛ پنل Production-ready نیست. دیتابیس و Migrationهای مشتری تغییر نکرده‌اند.

> **بازبینی مرجع 2026-10-21 (شاخه `docs/architecture-source-of-truth`)** — مبنا: `main` @ `1d39dbf9e0843045e00441bf2778267e9764a3f6`. بخش «وضعیت تأییدشده فعلی» زیر معتبر است؛ بخش‌های بعد از آن تا «هدف» **تاریخی و در موارد مشخص‌شده منسوخ** هستند و برای حفظ سابقه نگه داشته شده‌اند. در تناقض، ترتیب منابع حقیقت D-030 (`docs/INDEX.md`) ملاک است. واژگان وضعیت: `Accepted`, `Implemented`, `Verified`, `Proposed`, `NeedsApproval`, `Deprecated`, `Blocked`, `NotImplemented`.

## وضعیت تأییدشده فعلی (2026-10-21)

### شواهد اجرا
- **CI:** اجرای GitHub Actions شماره `39585708662` روی commit مبنا: هر ۶ Job موفق — Build، ۲۰۴ تست .NET (۰ رد، ۰ Skip) روی SQL Server 2022، گاردهای Migration (`generate_migrations.py --check`، `verify_migration_model.py`، `dotnet ef migrations has-pending-model-changes`)، Job کلاینت، E2E (۱۲ تست)، اسکن آسیب‌پذیری NuGet بدون مورد. این اجرا توسط CI انجام شده است.
- **این بازبینی مستنداتی Build/Test اجرا نکرده است** (`dotnet` در محیط این Agent نصب نیست). ادعاهای «Verified» به اجرای بالا استناد می‌کنند، نه به اجرای محلی.
- اجرای محلی گزارش‌شده در دور قبل (2026-10-21، SDK 10.0.113): ۲۰۴ تست .NET موفق، `check_static_assets.py` PASS، تست‌های کلاینت (۲۴) موفق — طبق `WORK_LOG.md`.

### آنچه `Implemented` است
- Razor Pages + Minimal API `/api/v1` + کلاینت React در `/app`، هدف `net10.0`، EF Core 10، SQL Server.
- مدل چندشرکتی: `Company`، `UserCompanyMembership`، `CompanyRole`، `PermissionDefinition`، `CompanyRolePermission`، `FiscalPeriod`، `SecurityAuditEvent`؛ تعیین دسترسی شرکت فقط با `CompanyAccessService` (D-013).
- **Migrationها وجود دارند:** `InitialCreate`، `AlignSchemaWithModel`، `WidenUserPasswordHash` (مسیر `Infrastructure/Persistence/Migrations`). اظهار قدیمی «Migration ساخته نشده» منسوخ است.
- ورود امن با `PasswordHasher<T>` (ارتقای هش SHA-256 قدیمی هنگام ورود)، Cookie + Anti-forgery، Rate Limiting، CSP/هدرهای امنیتی، اعتبارسنجی مجدد نشست.
- فرایند Publish قطعی و Smoke Test در CI؛ راه‌اندازی اولیه دیتابیس (Issue #22).

### آنچه `NotImplemented` است
- **هیچ ماژول حسابداری** (سند، سرفصل، گزارش) در کد نیست.
- **هیچ سیستم لایسنس/مشتری تجاری/نصب/زیرسیستم** (D-020..D-027؛ `docs/LICENSING.md`).
- **مصرف مجوز عملیاتی:** `PermissionDefinition`/`CompanyRolePermission` در دیتابیس هستند ولی در `Server/` بررسی نمی‌شوند (D-025).
- Global Query Filter (طبق D-013 عمداً وجود ندارد)، OpenAPI.

### وضعیت Activation قدیمی (اصلاح گزاره منسوخ)
`ActivationKeysHandlerMiddleware` **کد مرده است**: `UseActivationKeys()` در `Server/Program.cs` فراخوانی نمی‌شود (از commit `370fe67`، 2026-10-17، Issue #22). گزاره قدیمی «در محیط‌های دیگر فعال است» **منسوخ و نادرست** است. هیچ کنترل لایسنس در برنامه فعال نیست. وجود مشتری Production با Activation قدیمی: **نامشخص**. جزئیات: D-028.

### ریسک‌ها و تصمیم‌های باز
- هویت: چهار دامنه (صفحات `Admin`، Master زیرساختی، نقش شرکتی، مدیر فروشنده) باید طبق D-029 جدا بمانند؛ `UserCompanyMemberships.UserId` بدون FK است.
- دوره ارفاق ۳۰ روزه، فهرست زیرسیستم‌ها، الگوریتم امضا، مسیر گذار Activation قدیمی و کپی ماشین مجازی: `NeedsApproval` (ر.ک. `docs/LICENSING.md` بخش ۶). کپی کامل VM در نصب آفلاین قابل جلوگیری نیست.
- مسیر مرده Google OAuth در صفحه ورود (AUD-2026-10-21-02)؛ نبود فایل `LICENSE` در مخزن؛ ناهمخوانی نام‌ها (Template/TarazAva/Dtat/Faranou)؛ Branch protection مخزن نامشخص؛ شکاف دستور تست `dotnet test Template.sln` در CI با `--solution` لازم در SDK محلی 10.0.113 — ر.ک. `docs/REPOSITORY_AUDIT.md`.

---
**بخش‌های زیر تاریخی‌اند (وضعیت اولیه بازسازی). موارد منسوخ: ذکر «dotnet نصب نیست/Build اجرا نشده»، «Migration نیست»، «پروژه تست موجود نبود»، «Activation فعال است»، و «Google OAuth کامنت شده» (در واقع Handler ثبت نشده است).**

## هدف (تاریخی)
پایه‌سازی پروژه موجود ASP.NET Core Razor Pages برای توسعه مرحله‌ای و قابل بازبینی.

## وضعیت فعلی پس از بازسازی اولیه
- در شاخه `main` هدف‌گذاری به `net10.0` منتقل شده است؛ CI پس از Merge باید سبز شود.
- نسخه NuGetها با Central Package Management در `Directory.Packages.props` متمرکز شده است.
- تحلیلگرهای .NET و تنظیمات Nullable در سطح مخزن فعال شده‌اند.
- پروژه تست اولیه اضافه شده و به Solution افزوده شده است.
- گردش‌کار CI برای Restore، Build و Test اضافه شده است.
- مقادیر MasterPassword و ActivationKeys از فایل‌های تنظیمات حذف شده‌اند؛ مقادیر لازم باید در محیط مناسب پیکربندی شوند.
- حساب مدیر نمونه با اطلاعات تماس و رمز عبور ثابت از EF Core seed حذف شده است؛ ایجاد حساب واقعی باید از مسیر امن مدیریت هویت انجام شود.
- ~~در Development، Middleware فعال‌سازی اجرا نمی‌شود؛ در محیط‌های دیگر فعال است.~~ **منسوخ (2026-10-21):** پس از commit `370fe67` این Middleware به Pipeline متصل نیست (کد مرده)؛ D-028.
- `Program.cs` از محیط استاندارد ASP.NET Core پیروی می‌کند و نبود Connection String را با پیام روشن گزارش می‌دهد.

## محدودیت مهم
در محیط اجرای این بازسازی، `dotnet` نصب نیست؛ بنابراین Restore، Build، Test و بررسی هشدارهای کامپایل اجرا نشده‌اند. فایل‌ها و XML/JSON به‌صورت ایستا بررسی می‌شوند؛ CI باید اولین Build واقعی را انجام دهد.

## ریسک‌های باقی‌مانده
1. پروژه اکنون `.NET 10 LTS` را هدف گرفته است؛ خطاهای Build در CI باید قبل از ادامه توسعه برطرف شوند.
2. احراز هویت فعلی یک راهکار قدیمی با نام کاربری مرکزی و SHA-256 بدون salt است؛ برای تولید مناسب نیست و باید با ASP.NET Core Identity یا PasswordHasher استاندارد جایگزین شود.
3. پروژه `Application` به‌عنوان مرز آماده Use Case ایجاد شده است؛ persistence به `Infrastructure/Persistence` منتقل شده (با حفظ namespace قدیمی `Data`)، اما دسترسی مستقیم به EF Core در PageModelها هنوز وجود دارد و باید مرحله‌ای و همراه تست حذف شود.
4. در مخزن پروژه تست موجود نبود؛ تست اولیه فقط قرارداد تابع Hashing موجود را تثبیت می‌کند و جای تست‌های واقعی را نمی‌گیرد.
5. کتابخانه‌های فرانت‌اند مدیریت‌شده با LibMan هنوز به‌صورت جامع ارتقا داده نشده‌اند تا تغییر Major بدون آزمون UI وارد نشود.
6. صفحات About/Contact/SEO هنوز شامل نام و اطلاعات نمونه صاحب قالب هستند و باید قبل از انتشار با اطلاعات محصول جایگزین شوند.
7. مسیر Google OAuth در صفحه وجود دارد اما ثبت Handler در `Program.cs` کامنت شده است؛ باید فعال یا حذف شود.

## معیار ورود به فاز بعد
- اجرای Restore/Build/Test روی .NET 10 SDK و رفع خطاهای CI.
- بررسی Migration و حفظ داده‌های موجود.
- تعیین مدل احراز هویت، نقش‌ها و مجوزها.
- ثبت Issueهای مهاجرت معماری و افزودن تست برای مسیرهای حیاتی.


## بررسی تکمیلی 2026-10-16
- `docs/REPOSITORY_AUDIT.md` گزارش یافته‌های بررسی ایستا و ریسک‌های باقی‌مانده است.
- `docs/CLASS_REVIEW_INVENTORY.csv` فهرست 199 فایل C# و خروجی اسکن ساده است؛ این فایل به‌تنهایی جای بازبینی رفتاری خط‌به‌خط و تست را نمی‌گیرد.
- Redirect ورود به URL محلی محدود شد؛ Cookie تنظیمات پایه امن‌تری گرفت؛ Middleware قدیمی دیگر Exception را بی‌صدا نمی‌بلعد.
- اسکریپت کنترل دارایی CDN به CI افزوده شد.
- نسخه‌های فعلی Bootstrap (5.2.2) و Bootstrap Icons (1.9.1) هنوز نیاز به ارتقای واقعی فایل‌های محلی دارند؛ این کار به علت نبود شبکه در محیط بررسی انجام نشد.
- Restore/Build/Test هنوز باید روی دستگاه توسعه یا CI اجرا و نتیجه ثبت شود.


## 2026-10-17 — Issue #16: foundation for tenant data model and .NET 10
- Target framework و نسخه SDK در شاخه کاری برای .NET 10 LTS تنظیم شده‌اند؛ تأیید Build واقعی هنوز لازم است.
- مدل‌های Company، UserCompanyMembership، FiscalPeriod، CompanyRole، PermissionDefinition، CompanyRolePermission و SecurityAuditEvent و پیکربندی EF Core آن‌ها اضافه شده‌اند.
- Connection String پیش‌فرض توسعه به ریشه استاندارد `ConnectionStrings:ConnectionString` منتقل شده و از Environment/User Secrets قابل override است.
- **محدودیت:** Login فعلی هنوز Legacy است؛ عضویت جدید هنوز به ASP.NET Core Identity متصل نشده و کنترل کامل Tenant در درخواست‌ها فعال نشده است. Migration رسمی برای دیتابیس‌های موجود و آزمون SQL Server باید جداگانه تکمیل شود.
- **تست:** این تغییر از طریق GitHub API و بدون اجرای SDK در محیط ویرایش اعمال شده است؛ Restore/Build/Test محلی توسط این Agent اجرا نشده‌اند. CI باید نتیجه واقعی را ثبت کند.


## وضعیت پس از Merge مربوط به Issue #16 — 2026-10-17
- PR #17 در `main` ادغام شده است: https://github.com/hamidreza1394/Accounting/pull/17
- Merge commit: `1e0ec4e10799a3a949f33f73659f5b4e656aaff2`.
- مدل پایه چندشرکتی، دوره مالی، عضویت شرکت، نقش/مجوز، رویداد امنیتی، پیکربندی EF Core و تنظیمات قابل تغییر اتصال SQL Server در مخزن ثبت شده‌اند.
- هدف پروژه به .NET 10 / EF Core 10 منتقل شده است.
- **CI سبز نیست/هنوز تأیید نشده:** اجرای CI شاخه Feature با Restore موفق ولی Build ناموفق تمام شد؛ خطاهای مشاهده‌شده مربوط به استفاده از `System.Drawing.Image` در `Server/Pages/Admin/FileManager/PartialViews/_DisplayPictures.cshtml` پس از حذف مرجع `System.Drawing.Common` است. اجرای CI روی Merge commit در زمان ثبت این یادداشت هنوز در حال اجرا بود. وضعیت را از https://github.com/hamidreza1394/Accounting/actions بررسی کن.
- **مهم:** Migration ساخته یا اعمال نشده است؛ `Database.EnsureCreated()` قدیمی باقی مانده؛ Login هنوز Legacy است و ASP.NET Core Identity، tenant resolution، فیلترهای سراسری Tenant، policy authorization و تست‌های cross-tenant تکمیل نشده‌اند.
- این مرحله فقط پایه مدل و پیکربندی است و برای Production آماده نیست.


## 2026-10-17 — Issue #22 first-run database setup (branch in progress)
- پیاده‌سازی اولیه فرم راه‌اندازی دیتابیس، ذخیره محلی محافظت‌شده Connection String، کد یک‌بارمصرف setup، آزمایش اتصال و ایجاد/مهاجرت دیتابیس تازه در شاخه `feature/issue-22-database-first-run-setup` ثبت شده است.
- رشته اتصال صریح از User Secrets/Environment بر تنظیمات محلی مقدم است. رشته اتصال پیش‌فرض از `appsettings.Development.json` حذف شده تا فرم first-run در توسعه قابل استفاده باشد.
- Migration روی دیتابیس دارای جدول‌ها خودکار اجرا نمی‌شود تا دیتابیس قدیمی مبتنی بر `EnsureCreated()` ناخواسته تغییر نکند.
- **وضعیت تأیید:** Restore/Build/Test و اتصال واقعی SQL Server هنوز اجرا/تأیید نشده‌اند. تا بررسی CI و تست SQL Server، این تغییر برای Production تأییدشده نیست.
- گزارش handoff: `docs/handoffs/ISSUE-22-database-first-run-setup.md`.

## 2026-10-19 — Issue #30 تکمیل احراز هویت صفحه ورود
- جریان ورود با DI واقعی، Post/Redirect/Get، پاک‌سازی رمز از `ModelState` تکمیل شد. (اصلاح 2026-10-23: مقصد پیش‌فرض پس از ورود اکنون `/app/` (انتخاب شرکت و دوره مالی) است و حساب Master به `/` می‌رود؛ `/Account/LoginSuccess` فقط Redirect می‌کند.)
- رمز عبور به `PasswordHasher<T>` منتقل شد؛ هش قدیمی SHA-256 پذیرفته و پس از ورود موفق ارتقا می‌یابد.
- Migration `WidenUserPasswordHash` ستون `Users.Password` را به `varchar(255)` بزرگ می‌کند.
- **تأییدشده:** `dotnet restore`, `dotnet build -c Release` (0 error), `dotnet test` (90/90), `scripts/check_static_assets.py`, `git diff --check`.
- **تأییدنشده:** اجرای Migration روی SQL Server واقعی و آزمون End-to-End روی مرورگر در این محیط انجام نشده است.
- گزارش: `docs/handoffs/ISSUE-30-secure-login-authentication.md`.


## 2026-10-20 — ارتقای معماری (شاخه `feature/faranou-modern-architecture`)
- **مبنا:** `main` در `f539a36` (CI سبز).
- **Backend:** سرویس `CompanyAccessService` (تعیین Tenant از عضویت فعال در دیتابیس)، API نسخه 1 در `/api/v1` (نشست، توکن Anti-forgery، خروج، شرکت‌ها، نمای کلی شرکت)، پاسخ 401/403 برای API، `Cache-Control: no-store` و پاسخ خطای عمومی با Log. Claim شناسه کاربر صادر می‌شود و Claimهای `TenantId` حذف شدند.
- **اصلاحات:** داشبورد Razor خطای دیتابیس را دیگر نمی‌بلعد؛ خروج فقط با POST و Anti-forgery؛ ثبت‌نام دیگر موفقیت ساختگی گزارش نمی‌کند؛ منوی مدیریت فقط برای Admin نمایش داده می‌شود (کنترل اصلی همچنان `[Authorize(Roles = Admin)]` است).
- **Frontend:** `ClientApp` با React 19.3، TypeScript 7 (strict)، Vite 8، Tailwind 4؛ داشبورد راست‌چین فارسی در `/app` با داده واقعی (شرکت‌ها و دوره‌های مالی)، حالت خالی/خطا، تاریخ شمسی فقط در نمایش، فونت محلی.
- **تأییدشده:** CI GitHub Actions روی شاخه (Build، همه تست‌های .NET، jsdom، Migration guards، `has-pending-model-changes`، و Job کلاینت) — نتیجه در PR ثبت شده است. محلی: `npm run typecheck`، `npm test` (۱۸ تست)، `npm run build`، `npm run check:offline`، `scripts/check_static_assets.py`، رندر واقعی در Chromium (دسکتاپ/موبایل) با API شبیه‌سازی‌شده.
- **تأییدنشده:** اجرای برنامه روی SQL Server واقعی و آزمون End-to-End ورود → `/app` روی مرورگر با سرور واقعی؛ Build/Test .NET در محیط Agent (NuGet در دسترس نبود) — فقط CI.
- **هنوز وجود ندارد:** هیچ ماژول حسابداری (سند، سرفصل، گزارش)، OpenAPI، Global Query Filter، انتقال صفحه ورود به React.
- گزارش کامل: `docs/handoffs/ISSUE-UNNUMBERED-faranou-modern-architecture.md`.
- **به‌روزرسانی:** PR #38 در `main` ادغام شد (`207d81f`).

## 2026-10-20 — بازبینی امنیتی پس از PR #38 (شاخه `security/post-pr38-hardening`)
- اعتبارسنجی مجدد نشست در هر درخواست، CSP و هدرهای امنیتی، Rate Limiting قابل تنظیم، Exception Handler اختصاصی API، `no-cache` برای index.html کلاینت.
- فرایند انتشار قطعی (`scripts/publish.sh`/`publish.ps1`)، توقف Publish بدون کلاینت، Smoke Test خروجی Publish در CI.
- بررسی آفلاین بازنویسی و با تست پوشش داده شد.
- ۲۷ تست Integration HTTP با `WebApplicationFactory` روی **SQL Server 2022 واقعی در CI**.
- **تأییدشده در CI:** Restore/Build، ۱۶۴ تست .NET (۰ رد، ۰ Skip) با SQL Server، jsdom، گاردهای Migration، `has-pending-model-changes`، اسکن آسیب‌پذیری NuGet (موردی نیست)، Job کلاینت، Publish و Smoke Test.
- **اجرانشده در محیط Agent:** Build/Test .NET (NuGet مسدود)، `publish.ps1` (ویندوز).
- گزارش: `docs/handoffs/ISSUE-UNNUMBERED-post-pr38-security-review.md`.


## 2026-10-20 — پیگیری آمادگی Production (شاخه `security/production-readiness-followup`)
- پایه: `main` @ `2511907` (ادغام PR #39).
- محافظ پوشه خروجی در `publish.sh`/`publish.ps1` با تست؛ اجرای واقعی `publish.ps1` روی Windows در CI.
- Proxy مورد اعتماد، بررسی `AllowedHosts`، دستور تولید اعتبارنامه Master و امکان خاموش‌کردن قالب قدیمی آن.
- حذف inlineهای Razor و اجباری‌شدن CSP آن‌ها (به‌جز PageManager)، Endpoint گزارش CSP (پیش‌فرض خاموش)، اصلاح نام فونت‌ها برای میزبان حساس به حروف.
- E2E مرورگر واقعی ← سرور منتشرشده ← SQL Server 2022.
- **تأییدشده در CI:** ۲۰۴ تست .NET (۰ رد، ۰ Skip) روی SQL Server؛ ۱۲ تست E2E؛ تست‌های محافظ Bash و PowerShell (Linux و Windows)؛ انتشار Linux + Smoke؛ انتشار Windows؛ Job کلاینت؛ اسکن NuGet.
- **نامعین:** دامنه، Proxy و تعداد نمونه Production (پرسش‌های باز در `docs/DEPLOYMENT.md`).
- گزارش: `docs/handoffs/ISSUE-UNNUMBERED-production-readiness-followup.md`.

## 2026-10-21 — ممیزی جامع، تکمیل تیم Agent، تست واقعی (شاخه `fix/comprehensive-quality-hardening`)
- پایه: `main` @ `8244bf3`.
- تیم Subagent به ۹ نقش رسید (۳ مورد جدید: `database-engineer`, `devops-release-engineer`, `code-reviewer`)؛ هیچ Agent قبلی حذف نشد.
- تناقض نسخه .NET 9/10 در `docs/AGENT_START_PROMPT.md`، `docs/prompts/CODEX_PROMPT.md`، `docs/prompts/COPILOT_PROMPT.md` رفع شد (مطابق D-007 واقعی).
- **شکست CI Job `publish` روی `main`@`8244bf3` (Run 39483239598) با شواهد واقعی Flaky تشخیص داده شد، نه Regression:** بازتولید محلی ۶/۶ موفق + `gh run rerun` همان Run با نتیجه `success` در تمام Jobها روی همان commit. جزئیات کامل: `docs/REPOSITORY_AUDIT.md` (AUD-2026-10-21-01).
- **تست‌های واقعی این دور (همگی اجراشده و سبز):**
  - `dotnet test --solution Template.sln --configuration Release` → `total: 204, failed: 0, succeeded: 204, skipped: 0`. (نکته ابزار: سینتکس `--solution` به‌جای مسیر مستقیم Sln در .NET SDK 10.0.113 لازم است.)
  - `python3 scripts/check_static_assets.py` → PASS.
  - `dotnet format Template.sln --verify-no-changes --no-restore` → بدون تغییر لازم.
  - `npm ci && npm run typecheck && npm test -- --run && npm run build && npm run check:offline` در `ClientApp` → همه موفق (۲۴ تست Vitest Pass؛ Build و بررسی آفلاین بدون خطا).
- **ریسک باقی‌مانده بدون تغییر:** مسیر مرده Google OAuth (`AUD-2026-10-21-02`، نیاز به تصمیم حذف/تکمیل)، آسیب‌پذیری Low در `vite` (`AUD-2026-10-21-03`، فقط ابزار توسعه)، عدم امکان تست SQL Server واقعی در این محیط (`FARANOU_TEST_SQLSERVER` اجرا نشد).
- گزارش: `docs/handoffs/ISSUE-UNNUMBERED-comprehensive-quality-hardening.md`.

## انتخاب شرکت و دوره مالی (D-031) — 2026-10-21
- `Implemented` در شاخه `feature/company-fiscal-period-selection` (PR باز، ادغام‌نشده): `/api/v1/context`، Cookie زمینه کاری، صفحه انتخاب، صفحات مسدود، نوار زمینه، کیت UI کوچک.
- `Verified` فقط محلی: ۲۵۳ تست .NET (Provider حافظه‌ای)، ۴۸ تست کلاینت، Build کلاینت و بررسی آفلاین، بررسی بصری با API شبیه‌سازی‌شده. CI و E2E روی SQL Server هنوز اجرا نشده‌اند.
- `Accepted` (D-032، تصمیم کارفرما): Open = ورود عادی، Closed = فقط مشاهده/گزارش، تغییر زمینه حین نشست با ممیزی؛ `Implemented` در شاخه. `NotImplemented`: اِعمال فقط‌خواندنی روی Endpointهای نوشتن آینده.
- `NeedsApproval`: رفتار دوره `Archived` و دوره‌های آینده.
- **بازطراحی صفحه انتخاب (2026-10-23):** `Implemented` در شاخه `feature/work-context-selector-redesign` (PR ادغام‌نشده): جریان اول دوره مالی، بعد شرکت؛ لوگوی رسمی بریده‌شده؛ فونت Vazirmatn (OFL) محلی؛ بدون تغییر API/Schema. `Verified` در این محیط: ۷۶ تست کلاینت، ۴۹۵ از ۴۹۷ تست .NET روی SQL Server واقعی (۲ Skip)، ۲۴ تست E2E؛ بازبینی انسانی نهایی و ابزار دسترس‌پذیری اجرا نشده. جزئیات: `docs/WORK_CONTEXT_SELECTOR_UI.md`.


## آمادگی کاربران هم‌زمان ابری (Issue #49، ممیزی 2026-10-22)

- **وضعیت ظرفیت 100 کاربر:** `NotVerified`. در این دور فقط ممیزی مخزن و مستندات انجام شده؛ آزمون بار روی محیط واقعی اجرا نشده است.
- **شواهد معماری موجود:** Modular Monolith، .NET 10/EF Core 10/SQL Server، Razor Pages + API هم‌مبدأ `/api/v1` + React، Rate Limiting و تنظیمات Reverse Proxy. جزئیات در `docs/CLOUD_CONCURRENCY_READINESS.md`.
- **شکاف‌ها:** آزمون بار قابل تکرار در مخزن یافت نشد؛ تعداد نمونه/مشخصات Production باز است؛ limiter به‌ازای هر نمونه است؛ ماژول‌های حسابداری و آزمون صحت نوشتن مالی هنوز پیاده‌سازی نشده‌اند.
- **محدودیت این ممیزی:** فایل‌های کد و مستندات از GitHub بررسی شدند؛ Build/Test/Load Test در این دور اجرا نشده‌اند. هیچ نتیجه runtime از روی جست‌وجوی کد استنباط نشده است.
- **گام بعدی:** اجرای پرامپ `docs/prompts/CLOUD_CONCURRENCY_IMPLEMENTATION_PROMPT.md` در شاخه مستقل Issue #49، با رعایت کامل `AGENTS.md` و بدون merge خودکار.

## گروه شرکتی، گزارش تجمیعی و تلفیقی (D-033..D-035) — وضعیت پس از ادغام PR #53 (2026-10-22)
- **وضعیت Git:** PR #53 ادغام شده است؛ Merge commit در `main`: `1d30436d25b4f1e76b95e76a764afe249fd84f6f`. [CI](https://github.com/hamidreza1394/Accounting/actions/runs/39826940050): همه Jobهای گزارش‌شده موفق.
- `Implemented`: جداول گروه/عضویت/مالکیت/پروفایل/اجرا/محدوده اجرا، Migration افزایشی `20261022150943_AddCorporateGroups`، قواعد دامنه، `CompanyAccessService.HasPermissionAsync` و `CorporateGroupService`. گزارش محلی قبلی ۳۰۵/۳۰۵ تست SQL Server 2022 در handoff تاریخی ثبت شده است؛ این سطر ادعای اجرای مجدد محلی در این بازبینی نیست.
- `NotImplemented`: API/UI گروه، تخصیص مجوز به نقش‌ها، دفتر/کدینگ، تعدیلات و تطبیق درون‌گروهی؛ `Blocked`: گزارش عددی تا وجود دفتر و کدینگ؛ مرز `CustomerAccount` در ابر مشترک و چرخه کنترلی همچنان `NeedsApproval`.
- Migration روی دیتابیس مشتری/تولید اعمال نشده است.
- گزارش اولیه: `docs/handoffs/ISSUE-UNNUMBERED-corporate-groups-consolidation.md`.

## بازطراحی راه‌اندازی دیتابیس و تنظیمات اتصال (D-036) — 2026-10-23
- **وضعیت Git:** شاخه `feature/database-setup-redesign`؛ Commit/Push/PR انجام نشده و ادغام نشده است. به‌همین دلیل `main` هنوز مسیر قدیمی `TarazAva` را دارد.
- `Implemented` (فقط در شاخه): پوشه تنظیمات `Farano` با سیاست مسیر، رمزنگاری با `ApplicationName=Farano`، مهاجرت فقط‌خواندنی از `TarazAva`، حالت‌های Configured/Setup/Blocked، فرمان `--settings status|migrate`، مراحل و کدهای خطای راه‌اندازی، رد دیتابیس بیگانه، رفع دو علت پیام عمومی «اطلاعات فرم را بررسی کنید»، بازطراحی UI. جزئیات: [`DATABASE_SETUP.md`](DATABASE_SETUP.md).
- **`Verified` محلی توسط Lead (2026-10-23، SDK 10.0.402، SQL Server 2022 واقعی):** build ۰ خطا؛ ۴۸۹ تست، ۴۸۷ موفق، ۰ ناموفق، ۲ Skip (دو تست `chmod` با root)؛ E2E ۱۸ از ۱۸؛ ClientApp ۴۸ از ۴۸؛ `check_static_assets.py` PASS. بدون SQL Server (گزارش Agent پیاده‌ساز، دوباره اجرا نشد): ۴۶۹ موفق/۲۰ Skip. CI هنوز باز نشده.
- `NeedsApproval`: تصمیم D-036 و املای Farano/Faranou.
- **اجرا نشده:** رفتار Windows (DPAPI، Service، IIS، UNC، `HardenWindowsAcl` — فقط کامپایل شده)؛ Login SQL با رمز منقضی/غیرفعال؛ CI؛ دو POST هم‌زمان روی HTTP؛ POST بوت‌استرپ Master روی HTTP.
- **بازبینی:** `code-reviewer` یک دور بدون High؛ `security-reviewer` دو دور، پایان بدون Critical/High/Medium؛ `architect` روی Diff نهایی اجرا نشد.
- **یافته باز:** `AddDataProtection` سراسری تنظیم نشده (AUD-2026-10-23-02). E2E صفحه راه‌اندازی به‌روز و موفق شد (AUD-2026-10-23-01 حل‌شد).
- گزارش: `docs/handoffs/ISSUE-UNNUMBERED-database-setup-redesign.md`.

## مرز مشتری، شرکت پیش‌فرض و برچسب‌های رابطه (D-037..D-040) — 2026-10-23
- **وضعیت Git:** PRهای #65، #67، #68، #69 ادغام شده‌اند (Issue #66 بسته است). PR #68 (`4bcee61`) ستون‌ها و Endpointها را بدون ADR/handoff/`WORK_LOG` ادغام کرد و شرحش می‌گفت Build/Test اجرا نشده. این بازبینی در شاخه `feature/customer-boundary-audit-and-docs` است (Commit/PR در زمان نگارش ثبت نشده).
- `Implemented` (در کد): `Users.DefaultCompanyId` و `POST /api/v1/context/default-company` (D-039)؛ `Companies.CustomerBoundaryId`، `POST .../operator/customer-boundaries/assign` و `GET .../operator/customer-boundaries/diagnostics` (فقط حساب زیرساختی)؛ اعمال مرز در `CorporateGroupService`؛ برچسب‌های `relationshipTypes` در `GET /api/v1/context`؛ پیش‌انتخاب سال/شرکت بدون ورود خودکار (D-038، D-040). تصمیم‌ها: D-037 و D-038 و D-039 و D-040.
- **وضعیت تأیید:** `Verified` در این سند ادعا نمی‌شود. CI اجرا نشده (سقف صورتحساب GitHub؛ Jobها شروع نمی‌شوند). نتیجه اجرای محلی (SQL Server واقعی): ۵۲۴ تست .NET با ۰ شکست و ۲ ردشده، Vitest ۹۴/۹۴، E2E ۲۹/۲۹ — جزئیات در handoff. اجرا نشده: Firefox، Safari، axe، HTTPS پشت Proxy.
- `NotImplemented`: API/UI گروه شرکتی و گزارش گروه؛ نگاشت سمت فروشنده `CustomerAccount` ← (نصب، مرز)؛ رویه اصلاح/ادغام/تفکیک/فروش شرکت؛ تریگر دیتابیس مرز.
- `NeedsApproval`: واژگان برچسب‌ها و نمایش آن برای عضو بدون `corporate-groups.view`؛ رویه اصلاح مرز و تریگر دیتابیس؛ تأیید پسا‌واقع کارفرما برای D-038..D-040 (انحراف پیاده‌سازی: PR #68 پیش از ثبت ادغام شد).
- ریسک‌های باقی‌مانده و شرح: `docs/SECURITY.md` (بخش مرز مشتری) و D-038. گزارش: `docs/handoffs/ISSUE-66-customer-boundary-default-company-audit.md`.

## فاز صفر لایسنس و سامانهٔ مرکزی — 2026-10-24 (فقط مستندات)
- **مبنا:** `main`@`8c1e7a3`؛ شاخهٔ کار `docs/licensing-central-system-phase0`؛ هیچ کد اجرایی، Schema، Migration یا وابستگی تغییر نکرد.
- **شواهد اجرا در همین بررسی (SDK 10.0.402، Linux):** Release Build با ۰ خطا؛ ۴۰۷ تست .NET غیر-Integration موفق (۱۶ ردشده، ۰ ناموفق)؛ ۹۴ تست Vitest و typecheck موفق؛ `check_static_assets.py` PASS. **اجرا نشد:** تست‌های SQL Server/Integration/E2E (Docker در دسترس نیست)، CI، `npm run build`. اعداد ۵۲۴/۲۹ و CI سبز در این سند همچنان نقل از مستندات‌اند.
- **تصمیم‌ها:** D-041 `Accepted` (مشروط به گاردها: همین مخزن با Host/DB/هویت/انتشار مستقل، بدون مخزن جدید)؛ D-042..D-044 `Proposed` + `NeedsApproval`؛ مخزن جدا `Deferred`. فهرست باز: `docs/LICENSING_OPEN_DECISIONS.md`.
- **وضعیت لایسنس:** همچنان `NotImplemented` (هیچ پروژه، جدول، API یا صفحه‌ای ساخته نشد). Activation قدیمی کد مرده است (D-028).
- **یافته‌های ممیزی مجدد:** AUD-2026-10-24-01..09 در `docs/REPOSITORY_AUDIT.md`؛ مهم‌ترین‌ها: نبود مسیر ساخت اولین شرکت/مدیر (01)، نبود گارد جداسازی بستهٔ مشتری و اسکن اسرار (06)، ممیزی قابل‌ویرایش (04)، ریسک حفاظت کلید در Linux (02).
- **اصلاح انحراف مستندات (D-030):** D-031/D-036 در `main` موجودند (یادداشت در `DECISIONS.md`)؛ شمارهٔ ADR بعدی در `AGENTS.md`/`CLAUDE.md` اصلاح شد (D-045).
- **گام بعدی (پس از تأیید مالک):** مرحلهٔ L1 در `docs/ROADMAP.md` فاز 8؛ پیش‌شرط: پاسخ OD-01، OD-02، OD-12.

## وضعیت Vendor بعد از Issue #103 (2026-10-26)
- D-047 باید به‌عنوان معماری پذیرفته‌شده خوانده شود؛ یادداشت تاریخی «پس از تأیید مالک» در بخش‌های قدیمی این فایل به‌روز نشده و دیگر وضعیت جاری نیست.
- شاخهٔ security/issue-103-vendor-auth-mfa-v2 شامل APIهای Identity password→TOTP/passkey، Cookie session، antiforgery، rate limiting و fallback policy فعال/MFA است؛ هنوز در main نیست و CI/بازبینی migration باید موفق شود.
- Migration ثبت‌شدهٔ `20261026045412_VendorAddPasskeySupport` و snapshot به شاخه افزوده شدند؛ workflow دیگر scaffolding موقت ندارد. CI جدید باید نبود drift مدل و اجرای migration را تأیید کند.
- باقی‌مانده‌های اصلی: bootstrap امن نخستین مدیر، اتصال/تکمیل audit chain و checkpoint مستقل، RBAC و maker-checker، مدل مشتری/قرارداد/لایسنس، KMS/HSM، UI پنل و deployment واقعی.
- هیچ schema/migration دیتابیس مشتری تغییر نکرده است.


## Vendor follow-up — ثبت تکراری Identity (2026-10-26)
- کد فعلی پس از merge نشان داد Identity در `AddVendorPersistence` و دوباره در `Vendor.Host/Program.cs` ثبت می‌شود؛ این با معماری مستند «یک محل ثبت persistence/Identity» ناسازگار است.
- شاخهٔ `fix/vendor-identity-registration-single-source` ثبت Identity را به Persistence محدود کرده و SchemaVersion، SignInManager و token providers را در همان ثبت حفظ می‌کند.
- وضعیت: اصلاح پیشنهادی در شاخه، **Not verified** تا سبز شدن Vendor CI و integration tests روی SQL Server. علت ریشه‌ای شکست قبلی هنوز قطعی فرض نمی‌شود.

- CI evidence: Vendor CI run [40285919870](https://github.com/hamidreza1394/Accounting/actions/runs/40285919870) failed only in the runtime auth integration test at `Database.MigrateAsync` with `PendingModelChangesWarning`; frontend checks, .NET build and EF CLI snapshot check passed. PR #132 consolidates Identity registration, but the cause remains **unconfirmed until rerun**.

- **یافتهٔ مهم‌تر دربارهٔ migration:** فایل `20261026045412_VendorAddPasskeySupport.Designer.cs` در `main` غایب بود. شاخهٔ PR #132 اکنون `BuildTargetModel` این migration را اضافه کرده و تست SQL Server، اعمال migration و جدول `VendorAdminUserPasskeys` را صریح بررسی می‌کند. این با شکست runtime `PendingModelChangesWarning` سازگار است؛ تا CI سبز، رفع‌شدن آن تأیید نشده است.


## CI follow-up — SignInManager registration
- Vendor CI run 40286276604 found a compile error (`IdentityBuilder` has no `AddSignInManager` in Persistence project). Follow-up branch moves only SignInManager registration to Vendor.Host while keeping IdentityCore/store registration single-source. Status: Not verified until CI passes.


## Vendor CI follow-up — Issue #134 (2026-10-26)
- Vendor CI run [40287063856](https://github.com/hamidreza1394/Accounting/actions/runs/40287063856) failed during `dotnet build` because `AddDefaultTokenProviders()` is not available from the Persistence class library's current compile-time references. All frontend typecheck/tests/build and asset-budget checks passed in that run; .NET tests were skipped after the build failure.
- Branch `fix/vendor-identity-token-providers` moves only token-provider registration to `Vendor.Host`, retaining IdentityCore/store registration in Persistence and preserving token-provider behavior.
- Status: **Implemented in branch / Not verified** until fresh Vendor CI completes Build, model snapshot check, SQL Server integration tests and publish.


### Issue #134 — CI follow-up 2
- Vendor CI run [40288514593](https://github.com/hamidreza1394/Accounting/actions/runs/40288514593) تأیید کرد خطای قبلی Persistence برطرف شده، اما Build سپس در Host شکست خورد: `AddSignInManager` نیز extension روی `IdentityBuilder` است، نه `IServiceCollection`.
- شاخهٔ PR #135 اصلاح شد تا `AddSignInManager()` و `AddDefaultTokenProviders()` هر دو از طریق یک `IdentityBuilder` در Host ثبت شوند. وضعیت همچنان **Not verified** تا اجرای تازهٔ CI.


## Vendor Issue #195 — مدیریت metadata لایسنس (شاخهٔ در حال اعتبارسنجی)
- **وضعیت:** `Implemented / Not yet Verified` تا موفقیت Vendor CI و .NET CI روی head نهایی.
- فرم ثبت/ویرایش metadata لایسنس Draft به APIهای موجود متصل شده است؛ فهرست‌ها از `customerId` فیلتر می‌شوند تا قرارداد یا نصب مشتری دیگر در انتخاب‌گرها ظاهر نشود.
- تست‌های UI برای ایجاد، روابط اختیاری، تاریخ UTC، مجوز، RowVersion و Draft-only؛ تست HTTP مبتنی بر SQL Server برای فیلتر customerId اضافه شده‌اند.
- **خارج از محدوده:** صدور لایسنس امضاشده، KMS/HSM، activation آفلاین و اعمال لایسنس در برنامه مشتری هنوز پیاده‌سازی نشده‌اند و مانع Production هستند. هیچ migration یا دیتابیس حسابداری مشتری تغییر نکرده است.


## Issue #214 — next signing pipeline slice (2026-10-28)
- PR #219 merged after both Vendor CI and .NET CI succeeded; it adds a validated Vendor-metadata-to-`LicensePayload` mapper and negative tests.
- PR #220 (in progress) adds a provider-backed envelope factory that signs the exact canonical UTF-8 payload bytes and validates result metadata before producing `LicenseEnvelope`. Tests verify the envelope with a temporary test-only ECDSA key and ensure unavailable signer/invalid payloads fail closed.
- Not production-ready: envelope persistence, atomic sequence/status update, approval integration, a real KMS/HSM or remote signer, key rotation/runbook, and operational issuance/revocation tests remain. No customer DB schema changes are permitted.


## Vendor provisioning signer boundary — 2026-10-28 (PR #241)
- PR [#241](https://github.com/hamidreza1394/Accounting/pull/241) merged as `9ff7ae3a1cc9a2a2623335740f8adeb2b536417f`; final PR head was `ca94836f9e46dd4c97b30665d2f1456b63f7300d`.
- Vendor CI run [40745034125](https://github.com/hamidreza1394/Accounting/actions/runs/40745034125) and .NET CI run [40745033978](https://github.com/hamidreza1394/Accounting/actions/runs/40745033978) passed on that head. .NET CI included client/typecheck/tests/build, secret scan, build/test, migration guards, E2E, and Linux/Windows publish.
- Provisioning issuance now injects `IProvisioningSigningProvider` separately from `ILicenseSigningProvider`; default provider remains unavailable/fail-closed. No private key, software fallback, customer-side DB/schema change, or production KMS/HSM adapter was added.
- Vendor onboarding now has the versioned request/approval/export path, but this does **not** mean a customer company/admin was created or that entitlements were activated. A trusted customer-side consumer and signed acknowledgement remain tracked in Issue #235.
- **Production blockers remain:** approved external provisioning/license signer with trusted key IDs and rotation, signature verification before persistence, independent immutable audit checkpoint provider/credentials, operational recovery/alerting, and controlled deployment/backup-restore verification. Vendor must not be described as production-ready until these are addressed and tested.


## وضعیت تأییدشده پس از PRهای #244–#246 — 2026-10-29

- PR #244 با merge commit `96ecc4c49475d04fdaeb889e20f9d6b51354bc8e` ادغام شد؛ اعتبارسنجی بومی فرم درخواست provisioning دوباره فعال است و آزمون رگرسیون وجود دارد. .NET CI روی PR با اجرای [40816378102](https://github.com/hamidreza1394/Accounting/actions/runs/40816378102) موفق شد؛ شامل Build/Test، E2E، publish Linux/Windows و بررسی امنیتی.
- PR #245 با merge commit `381201b478363d5035e2b9d4dd9016ab497d71d8` ادغام شد؛ فرم مشتری Vendor دیگر `noValidate` ندارد و آزمون رگرسیون مربوطه اضافه شده است. .NET CI [40816639908](https://github.com/hamidreza1394/Accounting/actions/runs/40816639908)، Vendor CI [40816639918](https://github.com/hamidreza1394/Accounting/actions/runs/40816639918) و CodeQL [40816647570](https://github.com/hamidreza1394/Accounting/actions/runs/40816647570) موفق شدند.
- PR #246 با merge commit `ecce57cbb5b8c0b058a1cea2643a27a870245b32` ادغام شد؛ اعتبارسنجی بومی فرم‌های قرارداد، نصب و metadata لایسنس فعال است و آزمون‌های رگرسیون اضافه شده‌اند. Vendor CI [40816815620](https://github.com/hamidreza1394/Accounting/actions/runs/40816815620)، .NET CI [40816815563](https://github.com/hamidreza1394/Accounting/actions/runs/40816815563) و CodeQL [40816821910](https://github.com/hamidreza1394/Accounting/actions/runs/40816821910) موفق شدند.
- **محدودهٔ تأیید:** این PRها اعتبارسنجی سمت مرورگر را اصلاح می‌کنند؛ جایگزین اعتبارسنجی سمت سرور نیستند و ایجاد شرکت/مدیر در دیتابیس مشتری را انجام نمی‌دهند. در این PRها هیچ API، schema یا migration مشتری تغییر نکرد.
- **وضعیت پس از ادغام:** اجرای CI روی آخرین `main` با SHA `ecce57cbb5b8c0b058a1cea2643a27a870245b32` آغاز شده است؛ تا زمان تکمیل runهای [40817730196](https://github.com/hamidreza1394/Accounting/actions/runs/40817730196)، [40817728004](https://github.com/hamidreza1394/Accounting/actions/runs/40817728004) و [40817727539](https://github.com/hamidreza1394/Accounting/actions/runs/40817727539)، نتیجهٔ پس از ادغام را موفق اعلام نمی‌کنیم.
- **شکاف‌های مستقل باقی‌مانده:** مصرف‌کنندهٔ قابل‌اعتماد سمت مشتری (#235)، provisioning end-to-end (#228)، signer واقعی KMS/HSM و maker-checker صدور بسته (#236/#214)، و sink مستقل checkpoint ممیزی (#107) هنوز باز هستند. بدون تصمیم/دسترسی زیرساختی، این موارد با کلید نرم‌افزاری یا تأیید جعلی جایگزین نمی‌شوند.
