# گزارش بررسی ایستای مخزن

- **تاریخ بررسی:** 2026-10-16
- **مبنای بررسی:** ZIP ارسالی `net9-baseline`، نه مخزن آنلاین GitHub.
- **روش:** بررسی ساختار، فایل‌های پروژه، تنظیمات JSON، نسخه‌های پکیج، مسیرهای دارایی فرانت‌اند، الگوهای پرریسک در C# و بازبینی هدفمند ورود، Cookie و Middleware خطا. اسکن کامل‌تر در `docs/CLASS_REVIEW_INVENTORY.csv` ثبت شده است.
- **محدودیت:** در محیط بررسی `dotnet` نصب نیست و DNS/دریافت شبکه نیز در دسترس نبود؛ بنابراین Restore، Build، Test، `libman restore` و تست مرورگر اجرا نشدند. این گزارش تأیید کامپایل یا تأیید امنیتی نهایی نیست.

## دامنه مخزن
- 12 فایل پروژه `.csproj` و یک Solution.
- 199 فایل C#، 76 فایل Razor `.cshtml` و یک فایل تست C# در بسته ورودی.
- 9 کتابخانه در `Server/libman.json` و دارایی‌های vendored در `Server/wwwroot/lib/`.
- 1 تست فعلی در `tests/Template.Tests`؛ این سطح پوشش برای ادعای آمادگی Production کافی نیست.

## اصلاحات اعمال‌شده در این دور
1. `AGENTS.md` به پروتکل شروع/پایان اجباری، قوانین معماری/امنیت و قالب گزارش Agent مجهز شد.
2. قالب‌های GitHub Issue برای وظیفه Agent و Bug Report اضافه شدند.
3. قالب تحویل هر وظیفه در `docs/handoffs/TEMPLATE.md` اضافه شد.
4. اسکریپت `scripts/check_static_assets.py` و گام CI برای جلوگیری از بارگذاری دارایی‌های CDN در صفحات/CSS/JS متعلق به برنامه اضافه شد. فایل‌های vendor داخل `wwwroot/lib` از این اسکن مستثنا هستند؛ باید هنگام ارتقای vendor جداگانه بررسی شوند.
5. Redirect پس از ورود اکنون فقط به URL محلی مجاز می‌رود و Open Redirect از `ReturnUrl` کنترل می‌شود.
6. تنظیمات Cookie احراز هویت موجود به HttpOnly، SameSite=Lax، عمر محدود و Secure در Production مجهز شد.
7. Middleware استثنای قدیمی دیگر Exception را بی‌صدا نمی‌بلعد؛ خطا را با ILogger ثبت و دوباره پرتاب می‌کند تا handler استاندارد پاسخ را مدیریت کند.
8. مستندات Coding Standards و Development با قواعد .NET/C#, EF Core, Razor, امنیت، تست، نسخه‌بندی و دارایی‌های محلی گسترش یافت.

## یافته‌های باقی‌مانده و اولویت‌ها

### P0 — پیش از انتشار عمومی/Production
- **احراز هویت legacy:** `Server/Pages/Account/Login.cshtml.cs` از نام کاربری مرکزی و SHA-256 خام استفاده می‌کند و Claim نقش `Admin` می‌سازد. این مدل برای Production مناسب نیست. پیش از استفاده واقعی، آن را با ASP.NET Core Identity یا `IPasswordHasher<TUser>` و نقش/مجوزهای پایدار جایگزین کن.
- **Secrets/config:** `MasterUsername`، `MasterPassword` و `ActivationKeys` در تنظیمات وجود دارند اما در نسخه ورودی خالی هستند. مقادیر واقعی را فقط در User Secrets/Environment/Secret Store قرار بده. اگر مقادیر قبلی قبلاً به Git push شده‌اند، آن‌ها را افشاشده فرض و Rotate کن.
- **عدم اجرای Build/Test:** این بسته در این محیط compile نشده؛ پیش از شروع قابلیت‌های جدید، روی ویندوز دارای SDK واقعی Restore/Build/Test را اجرا کن و خطاها را رفع کن.
- **پایان پشتیبانی .NET 9:** با وجود هدف فعلی net9.0، پایان پشتیبانی در نوامبر 2026 نزدیک است. تصمیم مهاجرت به .NET 10 LTS را پیش از توسعه محصول/استقرار بگیر.

### P1 — پیش از شروع قابلیت‌های جدی
- **Frontend versions:** `Server/libman.json` به Bootstrap 5.2.2 و Bootstrap Icons 1.9.1 اشاره می‌کند؛ این با هدف Bootstrap 5.3+ منطبق نیست. در این محیط دسترسی شبکه برای دریافت باینری‌ها وجود نداشت؛ بنابراین عمداً فقط شماره نسخه را تغییر ندادم تا فایل‌های محلی و manifest ناسازگار نشوند. در وظیفه جداگانه، Bootstrap را به نسخه پایدار 5.3.x (در زمان بررسی سایت رسمی 5.3.8) و Icons را به نسخه مناسب ارتقا بده، فایل‌های محلی را واقعاً به‌روزرسانی و UI/RTL/آیکون‌ها را تست کن.
- **منطق در PageModelها:** CRUDهای متعدد حاوی `catch (Exception)` و دسترسی مستقیم به Persistence هستند. هر PageModel را در Issueهای کوچک به Application منتقل کن؛ بازنویسی دسته‌جمعی بدون تست پرریسک است.
- **مدیریت Exception:** اکنون مسیر استاندارد `UseExceptionHandler` در Program موجود است. Middleware قدیمی در صورت استفاده ثبت و rethrow می‌کند، اما بهتر است در آینده استفاده مستقیم آن حذف و handler استاندارد تنها نقطه مدیریت پاسخ خطا باشد.
- **Coverage:** فقط تست پایه Hashing وجود دارد؛ برای ورود، مجوزها، CRUD، Validation، Cookie، Redirect و مسیرهای خطا تست اضافه کن.
- **Migration/Database:** پیش از تغییر Schema، وضعیت Migrationها، مدل‌های EF، cascade delete، unique index و روش deployment/rollback را بررسی و مستند کن.
- **نمونه‌های قالب:** صفحات About/Contact/Index هنوز لینک‌ها و اطلاعات صاحب قالب را دارند؛ پیش از انتشار محصول با اطلاعات پروژه جایگزین شوند.
- **OAuth:** کد Google Authentication در Program کامنت شده، در حالی که PackageReference موجود است؛ یا در Issue جداگانه کامل و امن پیاده‌سازی شود یا وابستگی/کد بلااستفاده حذف شود.

### P2 — بهبود تدریجی
- هماهنگ‌سازی نام‌گذاری و formatter کد قدیمی در PRهای مرتبط، نه بازقالب‌بندی بزرگ.
- افزودن تحلیل وابستگی و اسکن آسیب‌پذیری NuGet در CI پس از تثبیت سیاست fail/warn.
- افزودن تست Integration با SQL Server یا محیط آزمون معتبر؛ EF InMemory جایگزین کامل SQL Server نیست.
- بررسی مجوز فایل‌های vendor، حذف وابستگی‌های غیرمصرف‌شده و تعیین فرآیند نگهداری نسخه‌ها.

## یافته‌های اسکن ایستا
- جست‌وجوی اولیه برای `FromSqlRaw`، `ExecuteSqlRaw` و `SqlCommand(` در فایل‌های C# مورد بررسی، موردی پیدا نکرد.
- جست‌وجوی اولیه برای `.Result`، `.Wait()` و `GetAwaiter().GetResult()` در فایل‌های C# مورد بررسی، موردی پیدا نکرد.
- چند `catch (Exception)` در PageModelهای CRUD و Registration وجود دارد؛ بررسی محتوای هر catch و اطمینان از ثبت خطا هنوز باید به‌صورت وظیفه‌ای انجام شود.
- TODOها در Login، Register و Page routing باقی هستند و باید هنگام پیاده‌سازی همان قابلیت تکمیل شوند.
- URLهای بیرونی در صفحات Contact/Index عمدتاً لینک محتوایی هستند؛ آن‌ها را با CDN دارایی اشتباه نگیرید. اسکن جدید فقط hostهای CDN را در attributeهای منبع و CSS بررسی می‌کند.

## فرمان‌های لازم روی دستگاه توسعه
```powershell
dotnet --info
dotnet restore Template.sln
dotnet build Template.sln --no-restore --configuration Release
dotnet test Template.sln --no-build --configuration Release
python scripts/check_static_assets.py
dotnet format Template.sln --verify-no-changes
```

## منابع نسخه‌ای بررسی‌شده
- Bootstrap 5.3 download docs: https://getbootstrap.com/docs/5.3/getting-started/download/
- Bootstrap Icons usage/version: https://icons.getbootstrap.com/usage/
- EF Core 9.0.21 package listing: https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.SqlServer/9.0.21

**تفسیر نتیجه:** این بسته اکنون مستندات و کنترل‌های بیشتری دارد و چند ایراد قابل اثبات اصلاح شده، اما تا رفع موارد P0 و اجرای واقعی Build/Test نباید به‌عنوان نسخه Production-ready تلقی شود.

---

## بررسی 2026-10-21 (GitHub آنلاین، شاخه `main` @ `8244bf3`، مرحله یک ممیزی جامع)

- **مبنا:** مخزن واقعی GitHub (`hamidreza1394/Accounting`)، نه ZIP آفلاین. `dotnet` 10.0.113 و `npm`/Node 22 واقعاً نصب و اجرا شدند.
- **روش:** Restore/Build واقعی .NET، `gh api` برای وضعیت واقعی PRها و CI، بازتولید محلی Smoke Test، اسکن آسیب‌پذیری NuGet/npm، بررسی EF Core pending model changes، grep هدفمند برای Query Filter/OAuth/Exception handling.
- **یافته مهم:** بسیاری از یافته‌های P0 بخش ۲۰۲۶-۱۰-۱۶ (هدف net9.0، نبود Build، عدم استفاده از PasswordHasher) در کامیت‌های بعدی (D-007، PR #30، PR #38، PR #44) واقعاً رفع شده‌اند؛ جزئیات رفع در `docs/DECISIONS.md` و `docs/WORK_LOG.md` ثبت است. این بخش یافته‌های **جدید** یا **هنوز باز** را ثبت می‌کند؛ بخش ۲۰۲۶-۱۰-۱۶ بالا به‌عنوان سابقه تاریخی حفظ شده و حذف نشده است.

### شناسه AUD-2026-10-21-01 — شکست CI روی HEAD واقعی main (قطعاً Flaky؛ با Re-run واقعی تأیید شد)
- **شدت:** Medium (از High به Medium کاهش یافت چون Regression قطعی در کد نبود) | **وضعیت:** Verified — Flaky، نه Regression
- **به‌روزرسانی قطعی (همان روز):** همان Run (`39483239598`) با `gh run rerun 39483239598` دوباره روی **دقیقاً همان Commit** (`8244bf3`) اجرا شد. نتیجه Attempt شماره ۲: `status=completed`, `conclusion=success` برای **تمام** Jobها از جمله `publish` (همانی که بار اول Fail شده بود). این با بازتولید محلی (۶/۶ موفق) هم‌خوانی کامل دارد و اثبات می‌کند این یک رفتار ناپایدار گذرا در Runner بوده، نه یک باگ تکرارشونده در کد. **با این حال**، چون علت ریشه‌ای دقیق (کدام منبع Flaky — رقابت منابع، Timing گرم‌شدن Antiforgery/Data Protection، یا چیز دیگر) شناسایی نشده، این مورد را Closed نمی‌کنیم؛ `scripts/smoke-published-app.sh` هیچ تغییری در این دور نگرفت چون هیچ شاهدی از نقص در خودِ اسکریپت یا کد برنامه پیدا نشد — تغییر دادن آن بدون شاهد، دقیقاً همان «تضعیف برای سبز شدن تست» است که مجاز نیست.
- **محل:** GitHub Actions run `39483239598` (workflow `.NET CI`، commit `8244bf3` = دقیقاً همان Merge Commit فعلی `main` از PR #44)، Job `publish`، Job ID `118838139150`.
- **شواهد:** خروجی `gh api repos/hamidreza1394/Accounting/actions/jobs/118838139150/logs`: `FAIL login page renders the anti-forgery token (expected 'yes', got 'no')` در `scripts/smoke-published-app.sh`؛ کل Workflow Run با `conclusion: failure` ثبت شده، در حالی که Jobهای دیگر همان Run (`build-and-test`, `client`, `e2e`, `publish-windows`, `publish-scripts`) موفق بودند.
- **اثر:** `docs/PROJECT_STATUS.md` و `docs/DECISIONS.md` ادعا می‌کنند CI برای همین Merge «تأییدشده/سبز» است؛ این ادعا برای آخرین Run واقعی این Commit نادرست است.
- **بازتولید محلی:** همان Commit را در این محیط (`dotnet` 10.0.113، بدون SQL Server) با `scripts/publish.sh` سپس `scripts/smoke-published-app.sh` **۶ بار پیاپی** اجرا کردم → **۶ از ۶ بار موفق**، همان چک Anti-forgery هم هر بار passed. بنابراین این یک Regression قطعی قابل بازتولید در کد نیست؛ به احتمال زیاد یک رفتار ناپایدار (Flaky) مختص Runner CI (رقابت منابع/زمان‌بندی گرم‌شدن Data Protection/Antiforgery هنگام اجرای هم‌زمان چند Job) است، اما بدون اجرای دوباره CI واقعی قابل اثبات قطعی نیست.
- **راهکار پیشنهادی:** (۱) CI را دوباره روی `main` اجرا کن (workflow_dispatch یا یک Commit خالی) و نتیجه واقعی را ثبت کن؛ (۲) اگر دوباره fail شد، warm-up صریح (یک درخواست اولیه به `/Account/Login` پیش از شروع چک‌ها) یا بررسی هم‌زمانی Jobهای `publish`/`publish-windows` روی یک Runner مشترک را بررسی کن.
- **تست لازم:** Re-run واقعی CI؛ در صورت تکرار، افزودن Retry منطقی در `scripts/smoke-published-app.sh` یا لاگ بیشتر برای تشخیص علت.
- **وابستگی:** به تنهایی مانع کار دیگری نیست، اما ادعای «CI سبز» در مستندات باید تا تأیید مجدد محتاطانه خوانده شود.

### شناسه AUD-2026-10-21-02 — مسیر Google OAuth کاملاً بدون Handler ثبت‌شده (نه فقط کامنت)
- **شدت:** Medium | **وضعیت:** Open
- **محل:** `Server/Pages/Google/Login.cshtml.cs` (`OnGet` → `HttpContext.ChallengeAsync(GoogleDefaults.AuthenticationScheme, ...)`)، `Server/Pages/Google/Response.cshtml.cs`، `Server/Server.csproj` (وابستگی `Microsoft.AspNetCore.Authentication.Google` هنوز موجود).
- **شواهد:** `grep -n "Google" Server/Program.cs` هیچ نتیجه‌ای برنگرداند — یعنی هیچ `.AddGoogle(...)` یا ثبت Scheme در Pipeline وجود ندارد (نه حتی به‌صورت کامنت، کاملاً غایب). `grep` برای لینک به `/Google/Login` در تمام `.cshtml`ها هم صفر نتیجه داد → این یک Route یتیم (Dead Route) بدون لینک UI است.
- **سناریوی سوءاستفاده/اثر:** درخواست مستقیم به `/Google/Login` باعث `InvalidOperationException` («No authentication handler is registered for the scheme 'Google'») می‌شود. ExceptionHandler استاندارد پروژه این را می‌گیرد و پیام عمومی بدون جزئیات نشان می‌دهد (طبق بررسی `docs/SECURITY.md`)، بنابراین افشای اطلاعات حساس رخ نمی‌دهد؛ ریسک اصلی، کد/وابستگی بلااستفاده و تجربه کاربری ناقص (خطای کنترل‌نشده به‌جای 404) است، نه نشت امنیتی مستقیم.
- **راهکار:** تصمیم معمار/کاربر لازم است: یا این صفحات و وابستگی NuGet مرتبط کامل حذف شوند، یا Google OAuth با Client ID/Secret واقعی (از Secret Store) کامل و امن پیاده‌سازی شود. این دقیقاً همان ریسک باز #7 در `docs/PROJECT_STATUS.md` است؛ تنها تفاوت با توضیح قبلی («کامنت شده») این است که در کد فعلی حتی یک خط کامنت هم از ثبت Handler نیست — کاملاً غایب است.
- **تست لازم:** بعد از تصمیم، یک تست Integration که مسیر `/Google/Login` را یا 404 (پس از حذف) یا یک ریدایرکت صحیح 302 به Google (پس از پیاده‌سازی) تأیید کند.

### شناسه AUD-2026-10-21-03 — آسیب‌پذیری High در `vite` (فقط ابزار توسعه، نه Bundle Production)
- **شدت:** Low (بر خلاف برچسب «High» خود npm، اثر واقعی محدود به ابزار توسعه است) | **وضعیت:** Deferred
- **محل:** `ClientApp/package-lock.json` → `vite@8.3.0-8.3.2` → وابستگی گذرای `launch-editor`.
- **شواهد:** خروجی واقعی `npm audit` در `ClientApp`: سه Advisory (`GHSA-698v-4r3x-3vcv`, `GHSA-r825-w8mm-5pxw`, `GHSA-587h-7wmj-h5pm`) مربوط به «باز کردن فایل در ویرایشگر» از سرور Dev ویت، نه کد Bundle شده Production. `npm audit fix --force` نسخه `vite@8.3.5` (خارج از بازه فعلی `package.json`) را پیشنهاد می‌دهد.
- **اثر:** این قابلیت فقط در حالت توسعه (`vite dev`) فعال است و در Build/Publish واقعی (`npm run build`) اجرا نمی‌شود؛ ریسک مستقیم برای کاربران Production نیست، اما باید برای امنیت محیط توسعه برطرف شود.
- **راهکار:** ارتقای `vite` به نسخه Patched در یک وظیفه مجزا، همراه اجرای کامل `npm run typecheck && npm test && npm run build && npm run check:offline` برای رد تغییر رفتار.
- **تست لازم:** `npm audit` بعد از ارتقا باید صفر یافته بدهد؛ کل مجموعه تست کلاینت باید سبز بماند.

### شناسه AUD-2026-10-21-04 — اسکن آسیب‌پذیری NuGet: بدون یافته (Verified)
- **شدت:** Informational | **وضعیت:** Verified
- **شواهد:** `dotnet list Template.sln package --vulnerable --include-transitive` روی تمام ۱۲ پروژه Solution: «has no vulnerable packages given the current sources» برای همه.
- **نتیجه:** نیاز به اقدام فوری ندارد؛ پیشنهاد می‌شود این دستور به‌صورت دوره‌ای در CI اجرا و Fail/Warn شود (طبق فاز ۴ نقشه راه که هنوز `[ ]` است).

### شناسه AUD-2026-10-21-05 — سازگاری Migration/Model Snapshot: بدون Drift (Verified با محدودیت)
- **شدت:** Informational | **وضعیت:** Verified (محدود)
- **شواهد:** نصب `dotnet-ef` 10.0.0 و اجرای واقعی `dotnet ef migrations has-pending-model-changes` روی `Infrastructure.Persistence` → خروجی: «No changes have been made to the model since the last migration.» فهرست Migrationها (`dotnet ef migrations list`): `InitialCreate`، `AlignSchemaWithModel`، `WidenUserPasswordHash`.
- **محدودیت صادقانه:** در این محیط SQL Server/LocalDB در دسترس نیست («LocalDB is not supported on this platform»)؛ بنابراین وضعیت «کدام Migration واقعاً روی یک دیتابیس Apply شده» تأیید **نشد** — فقط سازگاری فایل Snapshot با مدل فعلی تأیید شد. تست‌های Integration واقعی روی SQL Server (طبق `docs/TESTING.md`) باید جداگانه و با `FARANOU_TEST_SQLSERVER` اجرا شوند.

### شناسه AUD-2026-10-21-06 — عدم وجود EF Core Global Query Filter: مطابق تصمیم مستند D-013 (نه یافته جدید)
- **شدت:** Informational | **وضعیت:** Verified — مطابق مستندات
- **شواهد:** `grep -rn "HasQueryFilter\|IgnoreQueryFilters"` در `Infrastructure`, `Server`, `Domain`, `Application` صفر نتیجه داد.
- **تفسیر:** این دقیقاً با تصمیم صریح D-013 در `docs/DECISIONS.md` («Global Query Filter فعلاً اضافه نشد... با اولین ماژول حسابداری اضافه شود») هم‌خوانی دارد؛ عدم وجود Filter یک ناسازگاری کد/مستند نیست. دفاع فعلی فقط از طریق `CompanyAccessService` در سطح Service/Endpoint است. **تست‌های رفتاری مرتبط (`CompanyAccessServiceTests`, `ApiEndpointTests`) در این بررسی اجرا نشدند** (نیاز به EF InMemory/SQL Server دارند و در دامنه زمانی این مرحله اجرا نشد)؛ لازم است در مرحله اعتبارسنجی بعدی واقعاً اجرا شوند تا این مورد کاملاً Verified شود، نه فقط بر اساس Grep کد.

### شناسه AUD-2026-10-21-07 — بهبود واقعی: عدم وجود `catch (Exception)` عمومی در `Server/Pages`
- **شدت:** Informational (بهبود مثبت) | **وضعیت:** Verified (دامنه محدود)
- **شواهد:** `grep -rc "catch (Exception" Server/Pages` → صفر در تمام فایل‌ها؛ در باسلاین ۲۰۲۶-۱۰-۱۶ چند مورد در PageModelهای CRUD ثبت شده بود.
- **محدودیت:** فقط `Server/Pages` بررسی شد، نه کل مخزن (`Infrastructure`, `Application`, `Dtat*`). این یافته را نباید به کل مخزن تعمیم داد بدون بررسی مجزا.

### یادداشت روش‌شناسی این دور
- هیچ PR باز یا Draft در مخزن وجود ندارد («closed» همگی)؛ آخرین PR ادغام‌شده #44 دقیقاً همان HEAD فعلی `main` است. بنابراین خطر «اصلاح تکراری روی کاری که در شاخه دیگری انجام شده» در این لحظه وجود ندارد — فقط یک شاخه (`main`) در Remote موجود است.
- تست‌های Unit/Integration .NET (`dotnet test`) و تست‌های کلاینت (`npm test`) در این دور اجرا **نشدند** (در دامنه زمانی این مرحله از ممیزی قرار نگرفت)؛ این یک محدودیت صادقانه است، نه ادعای پوشش. باید در مرحله اعتبارسنجی (مرحله چهار) واقعاً اجرا و نتیجه‌شان با کد خروج دقیق ثبت شود.


---

## ممیزی مرجع مستندات — 2026-10-21 (شاخه `docs/architecture-source-of-truth`)
مبنا: `main`@`1d39dbf9e0843045e00441bf2778267e9764a3f6`. روش: مقایسه مستندات با کد، تاریخچه Git، `.github/workflows/dotnet.yml` و اجرای CI شماره `39585708662` (از طریق `gh api` REST). **Build/Test در این ممیزی اجرا نشد** (dotnet در این محیط نیست).

### AUD-2026-10-21-08 — مستند «Activation فعال است» منسوخ بود
- **شدت:** High (مستندات گمراه‌کننده امنیتی) | **وضعیت:** Mitigated در مستندات (کد مرده هنوز در مخزن است)
- **شواهد:** `git show 370fe67^:Server/Program.cs` خطوط ۱۴۹–۱۵۲ `if (!app.Environment.IsDevelopment()) { app.UseActivationKeys(); }` داشت؛ در `main` فعلی `UseActivationKeys` در `Program.cs` وجود ندارد (`grep`). Middleware تنها در `Server/Infrastructure/Middlewares/` باقی است.
- **اثر:** کنترلی در برنامه نیست؛ وجود مشتری Production با Activation قدیمی **نامشخص**. راهکار: D-028 (حذف کد مرده در PR کد جدا با تأیید).

### AUD-2026-10-21-09 — `DATABASE.md`/`PROJECT_STATUS.md`: «Migration نیست»
- **شدت:** Medium | **وضعیت:** اصلاح مستندات | **شواهد:** سه Migration در `Infrastructure/Persistence/Migrations`، گاردهای CI.

### AUD-2026-10-21-10 — ادعاهای منسوخ در `PROJECT_STATUS.md`
- «dotnet نصب نیست/Build اجرا نشده»، «پروژه تست موجود نبود»، «SHA-256 بدون salt» (هش جدید PBKDF2 است)، «Google OAuth کامنت شده» (Handler ثبت نشده؛ AUD-02). **وضعیت:** با بخش «وضعیت تأییدشده» و برچسب «تاریخی/منسوخ» اصلاح شد.

### AUD-2026-10-21-11 — `ROADMAP.md`/`TESTING.md`/`SECURITY.md` منسوخ
- CI «باز»، «Migration باز»، «فقط یک تست پایه»، فهرست «باقی‌مانده» امنیت (CSP/Rate Limiting انجام‌شده). **وضعیت:** اصلاح شد.

### AUD-2026-10-21-12 — نبود فایل `LICENSE`
- **شدت:** Medium (حقوقی) | **وضعیت:** Open — `ls LICENSE*` خالی. تصمیم مالک لازم است؛ فایل ساخته نشد (انتخاب مجوز تصمیم حقوقی است).

### AUD-2026-10-21-13 — نام‌گذاری ناهماهنگ
- `Template.sln`/`Template.Tests`، مسیر `TarazAva` در تنظیمات محلی، `Dtat*` و نام محصول «فرانو». **شدت:** Low | **وضعیت:** Open (بدون تغییر نام در این مأموریت).
- **به‌روزرسانی 2026-10-23:** مسیر `TarazAva` در جریان راه‌اندازی دیتابیس به `Farano` (با مهاجرت فقط‌خواندنی از مسیر قدیمی) تغییر کرد (D-036، `NeedsApproval`، شاخه `feature/database-setup-redesign`، ادغام‌نشده). ناهماهنگی تازه: «Farano» در این جریان در برابر «Faranou» در Cookie، API و `FARANOU_*`؛ املای نهایی باید تأیید شود (`docs/DATABASE_SETUP.md` بخش ۱).

### AUD-2026-10-21-14 — دو مدل مجوز موازی و عدم مصرف مجوز عملیاتی
- نقش `Admin` قدیمی (جدول `Roles`/Claim) در برابر `CompanyRole`/`PermissionDefinition`؛ مجوزهای شرکتی در `Server/` بررسی نمی‌شوند. **شدت:** Medium | **وضعیت:** Open (D-025، D-029).

### AUD-2026-10-21-15 — `UserCompanyMemberships.UserId` بدون FK
- **شدت:** Medium | **وضعیت:** Open — نیازمند تصمیم Schema جداگانه.

### AUD-2026-10-21-16 — برچسب زمانی `InitialCreate` (`20251017120000`) ناهمگون
- **شدت:** Low | **وضعیت:** Open — بدون بازنویسی Migration (قاعده `AGENTS.md`).

### AUD-2026-10-21-17 — ناهمخوانی دستور تست
- CI/`AGENTS.md`: `dotnet test Template.sln --no-build ...`؛ SDK محلی 10.0.113 طبق `WORK_LOG.md` به `--solution` نیاز داشت. **شدت:** Low | **وضعیت:** Resolved (2026-10-23) — علت تفاوت باند SDK بود: `10.0.113` به `--solution` نیاز داشت، `10.0.402` (SDK مورد انتظار `global.json` و CI) هر دو شکل را می‌پذیرد؛ شاهد در `docs/TESTING.md`.

### AUD-2026-10-21-18 — `CLASS_REVIEW_INVENTORY.csv` منسوخ
- ۱۹۹ فایل C# مربوط به ۲۰۲۶-۱۰-۱۶. **شدت:** Low | **وضعیت:** Open (بازتولید نشد).

### AUD-2026-10-21-19 — Issueهای خالی #40–#43 و Branch protection
- Issueهای #40–#43 در API بدون محتوای معنادار؛ تنظیم محافظت شاخه `main` از خارج **نامشخص**. **وضعیت:** Open — نیازمند بررسی مالک.

### AUD-2026-10-21-20 — اسکن اسرار در CI
- در `dotnet.yml` گام اختصاصی اسکن اسرار مشاهده نشد (فقط اسکن NuGet). **شدت:** Low | **وضعیت:** Open — پیشنهاد افزودن در Issue جدا؛ لازم برای محافظت از آینده کلید صدور (D-024).

### مواردی که بررسی شد و ناهمخوانی نداشت
commit مبنا؛ مدل چندشرکتی؛ نبود Global Query Filter (D-013)؛ نبود هر کد لایسنس/مشتری/ماژول؛ خروجی Publish بدون CDN (طبق CI).


## ممیزی تکمیلی آمادگی کاربران هم‌زمان ابری — 2026-10-22 (Issue #49)

**محدوده:** جست‌وجو در کد و مستندات شاخه main شامل `Server/Program.cs`, `Server/Api/**`, `Infrastructure/Persistence/**`, `Directory.Build.props`, `Directory.Packages.props`, CI، راهبرد تست و Deployment. این بررسی آزمون بار اجرا نکرده و ظرفیت را تأیید نمی‌کند.

- **CC-01 — High/Open:** آزمون بار/فشار/soak قابل اجرا در مخزن/CI پیدا نشد. ظرفیت 100 کاربر `NotVerified` است.
- **CC-02 — High/Open:** تعداد نمونه و مشخصات Production در Deployment هنوز پرسش باز است.
- **CC-03 — High/Open:** Rate Limiting فعلی per-instance است؛ رفتار scale-out باید تحلیل شود.
- **CC-04 — High/Open:** ماژول‌های حسابداری اصلی هنوز `NotImplemented` هستند؛ بنابراین صحت نوشتن مالی تحت رقابت قابل ادعا نیست.
- **CC-05 — Medium/Open:** راهنمای سنجه‌های performance/runtime و SQL contention در مستندات بررسی‌شده مشخص نیست؛ ابتدا سنجه‌های میزبان واقعی را شناسایی کن.
- **CC-06 — Medium/Open:** `Application` هنوز اسکلت است و بخشی از PageModelها به Persistence متصل‌اند؛ profiling مسیرهای پرتکرار لازم است، نه refactor گسترده حدسی.
- **CC-07 — Medium/Guardrail:** Global Query Filter عمداً طبق D-013 استفاده نشده؛ هیچ تغییر tenant isolation بدون ADR و تست cross-tenant.
- **CC-08 — Medium/Open:** Data Protection keys، Proxy/TLS و منابع محیط Production باید هنگام تعیین استقرار بررسی شوند.

موارد بالا در `docs/CLOUD_CONCURRENCY_READINESS.md` توضیح داده شده‌اند. نبود Redis/Kubernetes/microservices به‌تنهایی یافته نقص محسوب نمی‌شود؛ اندازه‌گیری runtime برای تعیین گلوگاه CPU، RAM، DB pool، query latency و deadlock ضروری است.

## ممیزی پیش از گروه شرکتی — 2026-10-22 (شاخه `feature/corporate-groups-consolidation`)

**محدوده:** `main`@`ef3feb5`؛ مستندات راهنما، `docs/*.md`، `Domain/`، `Infrastructure/Persistence/**`، `Server/Infrastructure/Tenancy/**`، اسکریپت‌های Migration و CI. Build و تست پایه در همین محیط اجرا شد (نتیجه در handoff).

- **AUD-2026-10-22-01 — Medium/Fixed:** شماره ADR بعدی ناهمخوان بود: `CLAUDE.md`، `AGENTS.md` و `docs/INDEX.md` «D-031» می‌گفتند ولی `docs/DECISIONS.md` D-031 و D-032 را داشت و «D-033» اعلام می‌کرد. پس از ثبت D-033..D-035 همه به «D-036» هم‌سطح شدند.
- **AUD-2026-10-22-02 — Low/Open:** D-031/D-032 و `PROJECT_STATUS.md` قابلیت انتخاب شرکت/دوره را «در شاخه PR ادغام‌نشده» می‌نامند، اما کد آن (`Server/Infrastructure/Tenancy/WorkContext*.cs`، `Server/Api/WorkContextEndpoints.cs`) در `main` وجود دارد. وضعیت باید با شاهد CI روی `main` به‌روز شود؛ در این مأموریت فقط ثبت شد.
- **AUD-2026-10-22-03 — Info:** مأموریت کارفرما «Tenant» را مرز چند شرکت فرض کرده بود؛ در کد `Company` = Tenant است. نگاشت مفاهیم در `docs/CORPORATE_GROUPS.md` بخش ۱ و D-033 ثبت شد؛ موجودیت `Tenant` جدید ساخته نشد.
- **AUD-2026-10-22-04 — Medium/Open:** گاردهای Migration CI (`generate_migrations.py`، `verify_migration_model.py`) شبیه‌ساز پایتونی EF Core‌اند و زیرمجموعه محدودی از Fluent API (بدون `HasPrecision`، `HasCheckConstraint`، `IsRowVersion`، `HasData`) را می‌فهمند؛ این محدودیت روی طراحی Migration جدید اثر داشت (جزئیات در handoff). اکنون SDK واقعی در CI هست؛ ادامه یا بازنشستگی این شبیه‌ساز تصمیم جداگانه است (`NeedsApproval`).
- **AUD-2026-10-22-05 — High/Fixed (wording) + NeedsApproval:** D-033 و مستندات وابسته (`CORPORATE_GROUPS.md` بخش ۱، `ARCHITECTURE.md`، `AGENTS.md` ۵.۳، handoff) «Tenant مأموریت = نصب» را معادل می‌گرفتند؛ بازبینی معماری مستقل نشان داد این با D-020 (`CustomerAccount`، نصب و `Company` سه مفهوم مستقل؛ یک مشتری چند نصب) و D-021 (یک نصب ابری چند مشتری) ناسازگار است و رابطه Company ابری ↔ `CustomerAccount` در جدول پرسش‌های باز `LICENSING_DATA_MODEL.md` هنوز `NeedsApproval` است. بخش «اصلاح پیش از ادغام» به D-033 افزوده شد (متن اولیه حذف نشد) و همه مستندات متأثر اصلاح شدند: مشتری/مستأجر = `CustomerAccount` (`NotImplemented`)، `Company` = واحد جداسازی، نصب = مرز استقرار/دیتابیس. کد و دسترسی به داده مالی تغییری نکرد (هر عملیات مجوز را در تک‌تک شرکت‌ها می‌سنجد). **باز (`NeedsApproval`):** مجاز بودن گروه/مالکیت بین شرکت‌های چند `CustomerAccount` در نصب ابری مشترک؛ پیش‌شرط هر PR آینده برای API/UI گروه، نه مانع PR زیرساختی (`CORPORATE_GROUPS.md` ۶.۱).


## پیگیری ممیزی پس از ادغام PR #53 — 2026-10-22 (Issue #54)
- **AUD-2026-10-22-06 — Medium / اصلاح در شاخه Issue #54:** در `CorporateGroupService.CreateGroupAsync`، مسیر رد مجوز مقدار `ownerCompanyId` درخواستی را به `AuditDeniedAsync` می‌داد؛ این شناسه در آن نقطه هنوز تأیید نشده بود. این با قرارداد خود تابع («فقط شناسه‌های اثبات‌شده») ناسازگار بود و می‌توانست فراداده یک شرکت را به رویداد ممیزی کاربر نامرتبط وصل کند. اصلاح: `TenantId=null` و `EntityId=null` برای این رویداد، همراه تست رگرسیون. تأیید نهایی منوط به CI روی PR است.
- **AUD-2026-10-22-07 — Low / اصلاح مستندات در شاخه Issue #54:** `docs/CORPORATE_GROUPS_DATA_MODEL.md` یکتایی `Code` را در کل نصب توصیف می‌کرد؛ کد EF و Migration در واقع `(OwnerCompanyId, Code)` را یکتا می‌کنند. مستند با پیاده‌سازی هماهنگ شد.
- **وضعیت باز:** رابطه `Company` با `CustomerAccount` و ممنوع/مجاز بودن گروه‌های چندمشتری در ابر مشترک هنوز `NeedsApproval` است. این PR زیرساختی API/UI یا مدل مشتری اضافه نمی‌کند؛ پیش از هر API/UI گروه باید تصمیم گرفته شود. قاعده `ControlCycle` نیز بدون تأیید حسابداری تغییر نمی‌کند.

## ممیزی بازطراحی راه‌اندازی دیتابیس — 2026-10-23 (شاخه `feature/database-setup-redesign`)
- **AUD-2026-10-23-01 — Medium / Resolved (2026-10-23):** تست E2E `the first-run setup page works under the enforced CSP` در `tests/e2e/specs/faranou.spec.mjs` به `#authMode` و `#sqlUserField` ارجاع می‌دهد؛ این شناسه‌ها در `Server/Pages/DatabaseSetup.cshtml` بازطراحی‌شده وجود ندارند (شاهد: جستجوی متنی در صفحه و `database-setup.js` بدون نتیجه؛ `HEAD` آن‌ها را داشت). تست به نشانه‌گذاری جدید به‌روز شد و در اجرای Lead موفق بود (E2E ۱۸ از ۱۸)؛ CI هنوز باز نشده.
- **AUD-2026-10-23-02 — Medium / Open:** `Program` تنظیم سراسری `AddDataProtection` ندارد؛ Cookie ورود و Anti-forgery از حلقه کلید پیش‌فرض استفاده می‌کنند (پیگیری، به‌ویژه چندنمونه؛ `docs/DEPLOYMENT.md` بخش ۵ و ۹).
- **AUD-2026-10-23-03 — Low / NeedsApproval:** املای Farano (جریان راه‌اندازی) با Faranou (بقیه مخزن) ناهمخوان است.
- **AUD-2026-10-23-04 — Low / Open:** مسیر UNC برای پوشه تنظیمات در کد بدون هشدار پذیرفته می‌شود؛ ACL در Windows با `HardenWindowsAcl` (اجرا نشده) محدود می‌شود؛ ACL پوشه اشتراکی UNC با اپراتور است.

## ممیزی مجدد فاز صفر لایسنس و سامانهٔ مرکزی — 2026-10-24 (شاخه `docs/licensing-central-system-phase0`)

**محدوده و روش:** `main`@`8c1e7a3` (پس از ادغام PR #70)، کلون سطحی (عمق ۱) و فقط‌خواندنی برای کد. هدف: اعتبارسنجی مجدد یافته‌های گزارش ممیزی قبلی (که خودش ورودی اولیه بود، نه حقیقت اثبات‌شده) و ثبت تصمیم‌های فاز صفر. هیچ کد اجرایی، Schema، Migration یا وابستگی تغییر نکرد.

**بررسی‌های واقعاً اجراشده (SDK 10.0.402، Linux):**

| بررسی | نتیجه |
|---|---|
| `dotnet restore Template.sln` | موفق |
| `dotnet build Template.sln --configuration Release --no-restore` | موفق، ۰ خطا، ۶۶۵ هشدار. نخستین تلاش به‌دلیل قفل فایل در `Server/obj` شکست خورد که از اجرای هم‌زمان Build پس‌زمینهٔ خودِ این بررسی بود، نه خطای کد؛ تلاش دوم پس از توقف فرایندهای رقیب موفق شد. |
| `dotnet test tests/Template.Tests/Template.Tests.csproj --no-build --configuration Release --filter "FullyQualifiedName!~Integration"` | ۴۲۳ تست: ۴۰۷ موفق، ۰ ناموفق، ۱۶ ردشده (از جمله تست‌های `chmod` که با root معنا ندارند) |
| `cd ClientApp && npm ci && npm run typecheck && npm test` | تمیز؛ ۹۴ از ۹۴ تست Vitest موفق |
| `python3 scripts/check_static_assets.py` | PASS |
| آزمایش امضا (برنامهٔ کوچک خارج از مخزن، .NET 10.0.13، Ubuntu 24.04) | ECDSA P-256: امضا 64 بایت، Verify حدود 0.10 ms؛ RSA-PSS 3072: امضا 384 بایت، Verify حدود 0.06 ms؛ نوع Ed25519 در BCL یافت نشد (جزئیات: `LICENSING_OPEN_DECISIONS.md` OD-12) |

**اجرا نشد / قابل‌بررسی نبود:** تست‌های Integration و E2E روی SQL Server (Docker daemon در این محیط در دسترس نیست)؛ `npm run build`/`check:offline`؛ CI (توکن `gh` معتبر نیست و طبق مستندات سقف صورتحساب GitHub مانع اجرا است)؛ تاریخچهٔ PR/Issue؛ تنظیمات Branch protection؛ Windows Server/Firefox/Safari/axe؛ هرگونه DB واقعی. ادعاهای مستندات دربارهٔ ۵۲۴ تست .NET، E2E ۲۹/۲۹ و CI سبز در این بررسی **تأیید نشدند** و همان‌طور که هستند نقل می‌شوند. از مخزن دور ۴۹ شاخه خوانده شد (`git ls-remote`)؛ شاخه یا کدی برای لایسنس، مشتری تجاری یا سامانهٔ فروشنده وجود ندارد.

### وضعیت هر یافتهٔ گزارش قبلی

وضعیت‌ها: **تأییدشده** (شاهد فعلی)، **اصلاح‌شده** (تأییدشده ولی دقیق‌تر یا ناقص‌تر از گزارش قبلی)، **ردشده/منسوخ**، **ناقص** (نیازمند بررسی بیشتر)، **غیرقابل‌بررسی** (در این محیط).

| # | ادعای گزارش قبلی | وضعیت | شاهد |
|---|---|---|---|
| 1 | Solution با ۱۲ پروژه، همه `net10.0` | تأییدشده | `Template.sln`؛ `Directory.Build.props` خط 3؛ Build موفق |
| 2 | `Application` فقط اسکلت است و `Server` مستقیم به EF وابسته است | تأییدشده | فقط `Application.csproj` و `README.md`؛ `Server/Server.csproj` خطوط 15–22 |
| 3 | React 19.3 + TS 7 strict + Vite 8 + Tailwind 4.3 | تأییدشده | `ClientApp/package.json`؛ typecheck و ۹۴ تست موفق |
| 4 | `Company` = Tenant؛ `CompanyAccessService` تنها نقطهٔ تصمیم دسترسی | تأییدشده | `Server/Infrastructure/Tenancy/CompanyAccessService.cs` خطوط 179–188 (کاربر، عضویت، شرکت، نقش فعال) |
| 5 | مجوز عملیاتی فقط توسط گروه شرکتی مصرف می‌شود | تأییدشده | `HasPermissionAsync`/`GetCompaniesWithPermissionAsync` فقط در `CorporateGroupService`؛ تنها مجوزهای Seedشده هشت مجوز گروه شرکتی‌اند (`20261022150943_AddCorporateGroups`) |
| 6 | ورود: PBKDF2، قفل حساب ۵ شکست در ۱۵ دقیقه | تأییدشده | `AuthenticationService.cs` خطوط 62–65 |
| 7 | MFA وجود ندارد | تأییدشده | جست‌وجوی MFA/TOTP/Authenticator در `Server` و `Domain` بی‌نتیجه (AUD-2026-10-24-05) |
| 8 | حساب Master پیکربندی‌محور و `--create-master-credential` | تأییدشده | `Server/Program.cs` خط 18؛ `InfrastructureMasterBootstrap.cs` (حداقل طول رمز ۱۵) |
| 9 | «هیچ مسیر ایجاد مشتری/شرکت/مدیر اولیه وجود ندارد» | **اصلاح‌شده** | کاربر قدیمی را صفحهٔ `Server/Pages/Admin/Users/Create.cshtml.cs` (خط 194، نقش Admin) می‌سازد؛ ولی هیچ مسیر غیرآزمونی برای `Company`، عضویت، `CompanyRole`، `FiscalPeriod` یا `PermissionDefinition` نیست (AUD-2026-10-24-01) |
| 10 | `AddDataProtection` سراسری نیست | **اصلاح‌شده** | `DatabaseConnectionSettingsStore.cs` یک Provider اختصاصی دارد و `ProtectKeysWithDpapi()` فقط در Windows اعمال می‌شود؛ تنظیم سراسری در `Program` نیست (AUD-2026-10-24-02، AUD-2026-10-23-02) |
| 11 | `ActivationKeysHandlerMiddleware` کد مرده است | تأییدشده | `UseActivationKeys()` تعریف شده (`Server/Infrastructure/Middlewares/ExtensionMethods.cs` خطوط 27–30) ولی در `Program.cs` فراخوانی نمی‌شود؛ کلید = SHA-256 مقدار `Request.Host`؛ در نبود کلید پاسخ 200 با متن `No Activation Key` (AUD-2026-10-24-08) |
| 12 | باقی‌ماندهٔ `ActivationKeys` در تنظیمات | **اصلاح‌شده** | `Server/appsettings.json:44` و `appsettings.Development.json:11` (آرایهٔ خالی)، `ApplicationSettings.cs:46`، و `.env.example:6` فقط به‌صورت **کامنت**؛ گزارش قبلی آن را فعال نشان داده بود |
| 13 | هیچ کد لایسنس/امضای نامتقارن/مشتری تجاری نیست | تأییدشده | جست‌وجوی `ECDsa`/`RSA.`/`SignData`/`VerifyData` در کل مخزن بی‌نتیجه؛ هیچ پروژه یا شاخهٔ لایسنس نیست |
| 14 | D-020..D-029 طرح‌اند و D-021 جداسازی سامانه فروشنده را الزام می‌کند | تأییدشده | `docs/DECISIONS.md`؛ D-021 بند ۲ و گزینهٔ ردشدهٔ (الف) |
| 15 | D-021 دربارهٔ مخزن مشترک/جدا حکمی ندارد | تأییدشده | متن D-021؛ اکنون با D-041 تکمیل شد |
| 16 | انتشار مشتری فقط `Server` را شامل می‌شود | تأییدشده، ولی بدون گارد | `scripts/publish.sh:42`؛ Jobهای `publish` و `publish-windows`؛ هیچ آزمون خودکاری برای نبود کد فروشنده/کلید در خروجی نیست (AUD-2026-10-24-06) |
| 17 | `SecurityAuditEvent` جدول معمولی و قابل‌ویرایش است | تأییدشده | `Domain/SecurityAuditEvent.cs`؛ `SecurityAuditEventConfiguration.cs` (بدون Append-only/زنجیره) (AUD-2026-10-24-04) |
| 18 | منبع زمان یکدست نیست | تأییدشده با شمارش به‌روز | کد غیرآزمونی: `DateTime.UtcNow` ۱۶، `DateTime.Now` ۲، `DateTimeOffset.UtcNow` ۴ (بدون خطوط کامنت‌شده)؛ `TimeProvider` فقط در `Program.cs`، `WorkContextCookie.cs`، `CorporateGroupService.cs` (AUD-2026-10-24-03) |
| 19 | CI شامل Jobهای client، build-and-test، publish-scripts، publish-windows، publish، e2e و بدون اسکن اسرار | تأییدشده | `.github/workflows/dotnet.yml`؛ جست‌وجوی gitleaks/trufflehog بی‌نتیجه؛ **اجرای CI** غیرقابل‌بررسی |
| 20 | D-031/D-036 در مستندات «ادغام‌نشده»اند ولی کدشان در `main` است | تأییدشده | `Server/Infrastructure/Tenancy/WorkContext*.cs`، `DatabaseSetup/SettingsCommand.cs`، `DatabaseSettingsLocation.cs` و فراخوانی در `Program.cs` (AUD-2026-10-24-07) |
| 21 | `AGENTS.md`/`CLAUDE.md` شمارهٔ ADR بعدی را D-036 می‌گویند | تأییدشده؛ در همین PR اصلاح شد | اکنون D-045 |
| 22 | فایل `LICENSE` در مخزن نیست | تأییدشده | AUD-2026-10-21-12 |
| 23 | برچسب زمانی `InitialCreate` (2025) ناهمگون است | تأییدشده | `Infrastructure/Persistence/Migrations/20251017120000_InitialCreate.cs`؛ AUD-2026-10-21-16 |
| 24 | نام‌گذاری ناهمگون (Template/TarazAva/Dtat/Faranou/Farano) | تأییدشده | `Template.sln` و بقیه؛ AUD-2026-10-21-13، AUD-2026-10-23-03 |
| 25 | ۵۲۴ تست .NET، E2E ۲۹/۲۹ و CI سبز | **غیرقابل‌بررسی** | نقل از مستندات؛ در این محیط فقط ۴۰۷ تست غیر-Integration (موفق)، ۹۴ تست Vitest اجرا شد |
| 26 | حساب راه‌اندازی/Master بدون SQL Server و Docker قابل آزمون نیست | **غیرقابل‌بررسی** | Docker daemon در دسترس نیست؛ تست‌های Integration/E2E اجرا نشدند |
| 27 | وضعیت PR/Issue/شاخه‌های GitHub | **ناقص** | فقط شاخه‌های دور خوانده شد (۴۹ شاخه؛ `main`@`8c1e7a3`)؛ PR/Issue و Branch protection قابل‌بررسی نبود (توکن `gh` نامعتبر) |

### یافته‌های جدید این ممیزی

- **AUD-2026-10-24-01 — Medium / Open:** مسیر ساخت اولین `Company`، مدیر شرکت، نقش‌ها و دورهٔ مالی در نصب تازه وجود ندارد؛ نصب فقط با SQL/تست پر می‌شود. شاهد در جدول بالا (ردیف 9). پیشنهاد: D-044، OD-14.
- **AUD-2026-10-24-02 — Medium / Open:** حلقهٔ کلید Data Protection تنظیمات اتصال فقط در Windows با DPAPI محافظت می‌شود؛ در Linux/macOS حفاظت به مجوز فایل‌سیستم محدود است. هر کلید خصوصی نصب (D-027، OD-08) نباید به این وضع تکیه کند و به AUD-2026-10-23-02 وابسته است.
- **AUD-2026-10-24-03 — Low–Medium / Open:** منبع زمان یکدست نیست؛ ارزیابی لایسنس و «ساعت مؤثر» (OD-05) به `TimeProvider` تزریقی نیاز دارد.
- **AUD-2026-10-24-04 — Medium / Open (برای کاربرد فروشنده):** ممیزی قابل‌ویرایش برای صدور/ابطال لایسنس کافی نیست؛ D-043.
- **AUD-2026-10-24-05 — Low–Medium / Open:** حساب Master بدون MFA و بر پایهٔ راز مشترک در پیکربندی است؛ الگوی هویت مدیر فروشنده نیست (D-029، D-043).
- **AUD-2026-10-24-06 — High (پیش‌شرط) / Open:** گارد خودکار جداسازی بستهٔ مشتری از کد فروشنده و گام اسکن اسرار وجود ندارد؛ پیش از هر کد امضا باید G1–G3 در D-041 اجرا شود (مرتبط با AUD-2026-10-21-20).
- **AUD-2026-10-24-07 — Low / Open:** انحراف مستندات (D-030): D-031/D-036 در `DECISIONS.md`/`PROJECT_STATUS.md` «ادغام‌نشده» نوشته شده‌اند ولی کدشان در `main` است؛ در این PR یادداشت‌های «توضیح 2026-10-24» افزوده شد و شمارهٔ ADR بعدی در `AGENTS.md`/`CLAUDE.md` اصلاح شد. ارتقا به `Verified` منوط به شاهد CI است.
- **AUD-2026-10-24-08 — Info / NeedsApproval:** باقی‌ماندهٔ کد مرده Activation (کلاس Middleware، متد افزونه، گزینهٔ `ActivationKeys`، نمونهٔ کامنت‌شده در `.env.example`) تا PR جدا با تأیید حذف نشود (D-028؛ جزئیات D-044 بند ۶).
- **AUD-2026-10-24-09 — Info:** `Server/libman.json` کتابخانه‌هایی با نسخه‌های قدیمی (مثلاً Bootstrap 5.2.2، jQuery 3.6.1، TinyMCE 6.2.0) را نگه می‌دارد؛ آسیب‌پذیری آن‌ها در این ممیزی بررسی نشد و ارتقا طبق قاعدهٔ LibMan در `AGENTS.md` فقط با به‌روزرسانی واقعی فایل‌ها مجاز است.

### تعارض‌ها و ناسازگاری‌ها با تصمیم‌های قطعی
- هیچ تعارضی با تصمیم‌های `Accepted` پیشین یافت نشد. تنها نکتهٔ ساختاری: D-021 مخزن را مشخص نکرده بود که D-041 تکمیل می‌کند. گزینهٔ «ماژول داخل `Server`» همچنان با D-021 ناسازگار و `Rejected` است.
