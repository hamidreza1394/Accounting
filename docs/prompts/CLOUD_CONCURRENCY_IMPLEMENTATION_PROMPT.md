# پرامپ اجرای ممیزی و اصلاح آمادگی کاربران هم‌زمان ابری

**Issue مرجع:** https://github.com/hamidreza1394/Accounting/issues/49  
**سند معیار:** `docs/CLOUD_CONCURRENCY_READINESS.md`

این دستور را در Claude Code / Codex یا عامل برنامه‌نویسی دیگری، از ریشه مخزن و روی شاخه مستقل اجرا کن. این پرامپ جایگزین قوانین مخزن نیست؛ تمام قواعد `AGENTS.md` لازم‌الاجرا هستند.

---

## نقش و هدف

تو مهندس ارشد .NET، SQL Server، تست عملکرد و امنیت هستی. باید پروژه موجود را مبتنی بر شواهد بررسی کنی و آن را برای آزمون واقعی 100 کاربر مجازی هم‌زمان آماده کنی. هدف «اثبات و بهبود با اندازه‌گیری» است، نه تضمین شفاهی ظرفیت یا افزودن زیرساخت مد روز.

## مرحله صفر — قوانین غیرقابل مذاکره

1. پیش از هر تغییر، `AGENTS.md` را کامل بخوان؛ سپس `docs/INDEX.md`, `docs/PROJECT_STATUS.md`, `docs/ROADMAP.md`, `docs/ARCHITECTURE.md`, `docs/DECISIONS.md`, `docs/WORK_LOG.md`, `docs/REPOSITORY_AUDIT.md` و این سند را بخوان. سپس `docs/CODING_STANDARDS.md`, `docs/SECURITY.md`, `docs/TESTING.md`, `docs/DATABASE.md`, `docs/DEPLOYMENT.md`, `docs/DEVELOPMENT.md` و `docs/AGENT_COORDINATION.md` را بررسی کن.
2. بررسی کن که Issue #49 هنوز همین محدوده را دارد؛ اگر Issue/شاخه/مبنای کد عوض شده، قبل از تغییر اعلام کن.
3. `git status --short`, شاخه، commitهای اخیر، PRهای مرتبط و CI را بررسی کن. هیچ تغییر قبلی کاربر را حذف، reset، stash یا بازنویسی نکن.
4. **مستقیماً روی main کار نکن.** شاخه‌ای اختصاصی از آخرین main بساز یا از شاخه Issue استفاده کن؛ اگر شاخه قدیمی شده، آن را ایمن به‌روز کن بدون از بین بردن تغییرات.
5. معماری فعلی `.NET 10 / C# 14 / EF Core 10 / SQL Server`, Modular Monolith، Razor Pages، API هم‌مبدأ `/api/v1` و React + TypeScript + Vite در `ClientApp` را حفظ کن. نسخه چارچوب یا Major پکیج‌ها را downgrade/upgrade نکن.
6. Domain به Web/Infrastructure وابسته نشود؛ وابستگی‌های فعلی را مطابق `docs/ARCHITECTURE.md` رعایت کن. انتقال کد قدیمی فقط تدریجی، با Issue/تست و در محدوده نیاز باشد.
7. دسترسی شرکت فقط از مسیر `Infrastructure.Tenancy.CompanyAccessService` و عضویت معتبر انجام شود. به TenantId ارسالی کلاینت یا Claim قابل‌تغییر اعتماد نکن. بدون ADR، Global Query Filter یا bypass دسترسی اضافه نکن.
8. Cookie هم‌مبدأ، Anti-forgery، CSP، Rate Limiting، Forwarded Headers مورد اعتماد و سیاست‌های موجود را تضعیف نکن. CORS یا ذخیره توکن در localStorage اضافه نکن. هیچ اسرار یا داده واقعی در Git/log/report قرار نده.
9. هیچ تست/Analyzer/Validation را حذف یا ضعیف نکن تا CI سبز شود. نتیجه تست اجرا‌نشده را موفق اعلام نکن. Merge ممنوع است مگر مالک صریحاً اجازه دهد.
10. از Redis، Kubernetes، microservices، queue، cache یا abstraction جدید فقط به دلیل «مقیاس‌پذیری» استفاده نکن؛ نیاز باید با اندازه‌گیری ثابت و تصمیم ثبت‌شده توجیه شود.

## مرحله 1 — بازبینی شواهد

- آخرین کد و CI واقعی را بررسی کن؛ گزارش‌های قدیمی را به‌عنوان وضعیت جاری فرض نکن.
- مسیرهای درخواست در `Server/Program.cs`, `Server/Api/**`, `Server/Infrastructure/**`, `Infrastructure/Persistence/**`, `ClientApp/**` و تست‌ها را دنبال کن.
- پیکربندی rate limiting، cookie/Data Protection، logging، health checks، exception handling، reverse proxy، SQL connection string/options، DbContext lifetime، query patterns، cancellation، paging، query timeouts و مدیریت خطاهای موقت را پیدا کن.
- جست‌وجوی کد را با بررسی رفتار واقعی کامل کن؛ صرف grep نبود یا وجود قابلیت را ثابت نمی‌کند.
- endpointها را بر اساس قابلیت واقعی موجود فهرست کن. ماژول‌های حسابداری سند/دفتر/گزارش طبق وضعیت پروژه هنوز پیاده‌سازی نشده‌اند؛ endpoint مصنوعی را جایگزین آزمون مالی واقعی نکن.
- یافته‌ها را در سه دسته ثبت کن: (الف) نقص قطعی با شاهد کد/تست، (ب) ریسک نیازمند سنجش runtime، (ج) پیشنهاد اختیاری.

قبل از تغییر کد، خلاصه baseline، گلوگاه‌های احتمالی، محدوده فایل‌ها، ریسک، برنامه آزمون و معیار پذیرش را در Issue/handoff ثبت کن. اگر تصمیمی نیازمند تأیید مالک است، متوقف شو و سؤال روشن مطرح کن.

## مرحله 2 — آزمون بار قابل تکرار

- یک ابزار مناسب را انتخاب کن (برای نمونه k6 یا ابزار هم‌سطح)؛ انتخاب را بر اساس سهولت اجرای محلی/CI، خروجی قابل‌تحلیل، پشتیبانی سناریوهای cookie/CSRF و نگهداری توجیه کن. ابزار را به runtime محصول اضافه نکن مگر ضرورت مشخص باشد.
- سناریوها فقط روی endpointهای واقعی موجود ساخته شوند: ورود/احراز هویت در صورت امکان امن، دریافت context، فهرست‌های موجود، جست‌وجو و مسیرهای خواندنی واقعی. برای هر endpoint ابتدا contract و روش seed را بخوان.
- آزمون نوشتن فقط وقتی ساخته شود که endpoint واقعی وجود داشته باشد و داده آزمون قابل ایزوله‌کردن باشد. ماژول حسابداری پیاده‌نشده را ساخته‌شده وانمود نکن.
- هدف اولیه: ramp-up تدریجی تا 100 VU، سناریوی ترکیبی واقع‌گرایانه با think time، و آزمون جداگانه تا 150/200 VU برای تشخیص نقطه افت. یک soak حداقل 30 دقیقه‌ای برای سنجش رشد منابع تعریف کن؛ در صورت محدودیت محیط، محدودیت را صریح گزارش کن.
- معیار اولیه پیشنهادی برای endpointهای معمول: p95 <= 2s و نرخ خطای غیرمنتظره <1%. این‌ها هدف آزمون هستند، نه SLA تضمین‌شده؛ 401/403/validation/429 مورد انتظار را از خطاهای واقعی تفکیک کن.
- گزارش شامل commit SHA، نسخه ابزار، مشخصات سرور و SQL، تعداد VU، ramp-up، duration، think time، throughput، p50/p95/p99، نرخ خطا، timeout، 429/5xx، CPU/RAM، GC/ThreadPool، SQL waits/latency، pool saturation و deadlockها باشد.
- آزمون فقط در محیط isolated/staging با داده مصنوعی و مجوز صریح اجرا شود. هرگز بار را روی Production بدون تأیید اجرا نکن. سقف منابع/هزینه و شرط توقف تعریف کن.

## مرحله 3 — اصلاح‌های مبتنی بر شواهد

فقط گلوگاه‌هایی را اصلاح کن که با profiling یا آزمون قابل تکرار اثبات شده‌اند. بسته به یافته‌ها، موارد زیر را بررسی کن:
- async/await برای I/O و انتقال CancellationToken؛ حذف sync-over-async.
- جلوگیری از اجرای موازی روی یک DbContext و رعایت طول عمر Scoped.
- projection، AsNoTracking برای خواندن‌های مناسب، paging سمت سرور و حذف N+1/داده اضافی.
- execution plan، ایندکس‌های واقعی مورد نیاز، تراکنش‌ها، deadlock و locking.
- connection pool، command timeout و retry فقط در صورت idempotency و سیاست صحیح؛ retry کور برای نوشتن مالی ممنوع.
- rate limiting و رفتار پشت چند نمونه؛ توجه کن محدودیت فعلی ممکن است per-instance باشد.
- health/readiness checks، graceful shutdown و logging/metrics کم‌هزینه و بدون داده حساس.
- Data Protection keys و state وابسته به نمونه فقط اگر استقرار چندنمونه‌ای هدف واقعی است.
- گزارش‌های سنگین/کارهای طولانی فقط در صورت وجود واقعی و نیاز اندازه‌گیری‌شده به پردازش پس‌زمینه منتقل شوند.

برای هر تغییر performance، نتیجه قبل/بعد و تست رگرسیون ارائه کن. اگر هیچ گلوگاه واقعی در محیط موجود قابل اثبات نیست، کد حدسی اضافه نکن؛ ابزار آزمون و شکاف اندازه‌گیری را تحویل بده.

## مرحله 4 — صحت مالی و امنیت tenant

- در endpointهای موجود، تست هم‌زمانی و cross-tenant متناسب اضافه کن.
- هیچ دسترسی مشتری A به داده مشتری B مجاز نیست، حتی تحت بار.
- اگر عملیات مالی در این نسخه پیاده‌سازی نشده، آن را به‌عنوان مانع شفاف ثبت کن و طراحی تست آینده برای تراکنش اتمیک، شماره‌گذاری یکتا، idempotency، تعارض هم‌زمانی و invariantهای مالی ارائه بده؛ عملیات مالی مصنوعی را به محصول وارد نکن.
- در صورت تغییر مدل/Schema، Migrationها، snapshot و `dotnet ef migrations has-pending-model-changes` را بررسی کن. Migration موجود را بازنویسی/حذف نکن.
- لاگ‌ها نباید رمز، Cookie، توکن، connection string، داده شخصی یا داده مالی حساس را افشا کنند.

## مرحله 5 — اجرا و تحویل

اجرا کن، اگر محیط اجازه می‌دهد:
- `dotnet restore Template.sln`
- `dotnet build Template.sln --no-restore --configuration Release`
- `dotnet test Template.sln --configuration Release` (با syntax معتبر برای SDK حاضر؛ اگر SDK به `--solution` نیاز دارد، دستور درست را ثبت کن)
- `python3 scripts/check_static_assets.py`
- در `ClientApp`: `npm ci`, `npm run typecheck`, `npm test`, `npm run build`, `npm run check:offline`
- تست‌های Integration/E2E و آزمون بار روی محیط مجاز.
- `git diff --check` و بازبینی نهایی diff.

هر دستور اجرا‌نشده را با دلیل ثبت کن. CI سبز را برای commit مشخص ارجاع بده. Build/Test معمولی را به‌عنوان اثبات ظرفیت بار گزارش نکن.

## مستندات اجباری

- به‌روزرسانی `docs/CLOUD_CONCURRENCY_READINESS.md` با یافته‌های واقعی، نتایج و وضعیت.
- به‌روزرسانی `docs/REPOSITORY_AUDIT.md` برای هر یافته قطعی جدید و حذف/علامت‌گذاری موارد منسوخ فقط با شاهد.
- به‌روزرسانی `docs/TESTING.md`, `docs/DEPLOYMENT.md`, `docs/ROADMAP.md` و `docs/PROJECT_STATUS.md` فقط در بخش‌های متأثر؛ هیچ تاریخچه‌ای را بی‌صدا پاک نکن.
- ADR با شماره بعدی فقط در صورت نیاز به تصمیم معماری واقعی؛ پیش از ایجاد، شماره فعلی `docs/DECISIONS.md` را دوباره بررسی کن.
- ساخت `docs/handoffs/ISSUE-49-cloud-concurrency-readiness.md` از قالب `docs/handoffs/TEMPLATE.md`.
- افزودن خلاصه به انتهای `docs/WORK_LOG.md`.
- اگر معیار یا ابزار جدیدی اضافه شد، در `docs/INDEX.md` فهرست شود.

## معیار پایان

- آزمون بار قابل تکرار و راه اجرای آن وجود داشته باشد، یا علت دقیق عدم امکان اجرا مستند باشد.
- هیچ ظرفیت 100/200 VU به‌عنوان `Verified` اعلام نشود مگر نتایج واقعی در محیط و commit مشخص وجود داشته باشد.
- گلوگاه‌های اصلاح‌شده با اندازه‌گیری قبل/بعد و تست regression پشتیبانی شوند.
- جداسازی tenant و صحت مسیرهای موجود تحت بار بررسی شوند.
- مستندات و handoff با کد واقعی هماهنگ باشند.
- PR مستقل برای Issue #49 آماده شود، بدون merge خودکار.

## قالب پاسخ نهایی

1. خلاصه یافته‌های تأییدشده در برابر ریسک‌های نیازمند runtime.
2. فایل‌های تغییرکرده و دلیل هر تغییر.
3. ابزار آزمون و نحوه اجرا.
4. نتایج دقیق آزمون‌ها با commit SHA و مشخصات محیط.
5. تست‌های اجرا‌نشده و علت.
6. گلوگاه‌های باقی‌مانده، ریسک‌ها و نیازهای تأیید مالک.
7. Branch، commit و PR.
8. جمله صریح وضعیت ظرفیت: `Verified` فقط با شواهد بار واقعی؛ در غیر این صورت `NotVerified`.

**ممنوع:** ادعای «۱۰۰ کاربر تضمین شد» بدون نتیجه آزمون؛ تغییر معماری یا زیرساخت حدسی؛ حذف تست؛ تغییرات نامرتبط؛ commit مستقیم به main؛ merge بدون اجازه.
