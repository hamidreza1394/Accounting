# تحویل وظیفه — ISSUE-UNNUMBERED-trade-s2-3-party-backend

- **Issue:** UNNUMBERED (S2-3 در `docs/TRADE_PHASE0_DRAFTS.md`؛ فقط بخش بک‌اند)
- **Agent/مسئول:** backend-developer (Claude Code)
- **تاریخ:** 2026-11-13
- **وضعیت:** Implemented (بک‌اند)؛ `Verified` فقط برای آنچه روی SQL Server واقعی اجرا شده (جدول زیر). ClientApp، منو و ردیف کاتالوگ در این تحویل نیست.

## هدف و معیارهای پذیرش
- [x] فهرست، جزئیات، ایجاد و ویرایش طرف‌حساب در `/api/v1/trade/parties` (بدون حذف/فعال/غیرفعال‌سازی؛ D-083 هنوز Proposed)
- [x] Tenant و کاربر فقط از `TradeAccessContext`؛ ۴۰۴ با بدنهٔ خالی و یکسان برای ناموجود/شرکت دیگر/حذف‌نرم
- [x] Anti-forgery و مجوز سمت سرور از دروازهٔ Trade؛ DTO جدا از Entity؛ `legalIdentifier` فقط در جزئیات
- [x] تست cross-tenant و رقابت روی SQL Server واقعی
- [x] `GET /api/v1/trade/reference/countries` برای فرم (منبع حقیقت یکتا: `TradeReferenceData.Countries`)
- [ ] صفحات React، منو، کاتالوگ — خارج از این تحویل

## خلاصه تغییرات
- لایهٔ Application: `PartyService` (اعتبارسنجی، Diff نقش‌ها، نگاشت)، `IPartyStore` (درگاه باریک، نه Repository عمومی)، DTOها؛ بدون ارجاع به EF (تست معماری).
- لایهٔ Infrastructure: `PartyStore` روی `TradeDbContext` با فیلتر صریح `TenantId` و `!IsDeleted` (از جمله زیرپرس‌وجوی نقش).
- Endpointهای نازک در `TradePartyEndpoints.cs` و `TradeReferenceEndpoints.cs`؛ پاسخ‌ها `Cache-Control: no-store` و `Pragma: no-cache` (فیلتری پیش از دروازه روی گروه Trade).
- خطاها: ۴۰۹ `trade_party_code_duplicate` (پیش‌بررسی + SQL 2601/2627 با نام ایندکس)، ۴۰۹ `trade_concurrency_conflict`، ۴۰۰ اعتبارسنجی با کلیدهای camelCase و بدون بازتاب مقدار.
- اعتبارسنجی: رد نویسه‌های کنترلی و بیدی (U+202A–202E، U+2066–2069، U+200E/F) در code/legalName/displayName/legalIdentifier؛ ZWNJ مجاز؛ بدون allowlist برای Code (تصمیم کسب‌وکار).
- محدودیت اندازهٔ بدنهٔ POST/PUT: ۱۶ کیلوبایت با خواندن دستی بدنه (بخش آزمون).
- مسیر کشورها: ترتیب ordinal بر اساس کد ISO (وابسته به ICU/کالیشن فارسی نیست)؛ کلاینت در صورت نیاز با locale خودش بر اساس `nameFa` مرتب کند.

## فایل‌های مهم تغییرکرده
- `Modules/Trade/Trade.Application/Parties/{PartyDtos,IPartyStore,PartyService}.cs` — جدید
- `Modules/Trade/Trade.Infrastructure/Parties/PartyStore.cs` — جدید
- `Modules/Trade/Trade.Infrastructure/Persistence/TradePersistenceServiceCollectionExtensions.cs` — ثبت `IPartyStore` و `AddLogging()`
- `Server/Api/Trade/{TradePartyEndpoints,TradeReferenceEndpoints,TradeEndpoints}.cs`
- `Server/Program.cs` — ثبت Scoped `PartyService` در همان شاخهٔ `!setupRequired`
- `tests/Template.Tests/{TradePartyServiceTests.cs, Architecture/TradePartyEndpointArchitectureTests.cs, Integration/TradePartyHttpTests.cs, Integration/FaranouApplicationFactory.cs}` — تست‌ها؛ در کارخانهٔ تست، `TradeDbContext.Database.MigrateAsync()` هم اجرا می‌شود (فقط کد تست).

## تصمیم‌های فنی
- **ممیزی:** فقط ستون‌های `Created*/Updated*` موجودیت؛ بدون `SecurityAuditEvents`.
- **نوشتن در دورهٔ بسته:** در دور اول مسدود بود (۴۰۹ `trade_period_read_only`)؛ در دور چهارم با تأیید مالک برای POST/PUT parties آزاد شد (بخش «تغییرات مجاز دور چهارم»).
- **بدون نرمال‌سازی فارسی** (ي/ی، ك/ک، ارقام).
- **`AddLogging()` در `AddTradePersistence`:** چون `PartyStore` لاگ می‌زند و تست ثبت موجود با Container خالی و `ValidateOnBuild` ساخته می‌شود؛ idempotent است.
- **راه‌حل حالت Added برای نقش‌ها:** نقش افزوده‌شده با `Party.AddRole` کلید Guid سمت کلاینت دارد و EF آن را ردیف موجود (Modified) می‌گیرد؛ نتیجه ۴۰۹ کاذب است (با حذف موقت راه‌حل، تست شکست خورد). `PartyStore.SaveChangesAsync` چنین ردیف‌هایی (RowVersion خالی) را Added می‌کند. پیشنهاد بازبین‌ها: `ValueGeneratedNever()` روی `TradeEntity.Id`؛ این تغییر مدل/Snapshot است و فقط با تأیید مالک و در تغییر جداگانه انجام شود.
- نقش‌ها جایگزینی کامل ولی به‌صورت Diff (نه حذف و درج مجدد) به‌دلیل ایندکس یکتای نقش فعال.
- تعارض ایندکس نقش (دو تغییر هم‌زمان نقش) به ۴۰۹ `trade_concurrency_conflict` نگاشت می‌شود.
- `rowVersion` باید دقیقاً ۸ بایت Base64 باشد.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `dotnet build Template.sln -c Release` | موفق | ۰ خطا (هشدارهای موجود مخزن باقی است) |
| `dotnet test ... --filter "FullyQualifiedName~Trade"` روی SQL Server 2022 واقعی، `FARANOU_REQUIRE_SQLSERVER=1`، ۳ بار | موفق | (پس از دور سوم تغییرات) هر بار ۱۵۹ از ۱۵۹ موفق، ۰ رد، ۰ Skip؛ کد خروج ۰ |
| همان فیلتر بدون SQL Server (InMemory) | موفق | ۱۵۹ کل، ۱۴۶ موفق، ۱۳ Skip (تست‌های دادهٔ SQL)، ۰ رد |
| کل پروژهٔ تست روی SQL Server واقعی | موفق | ۱۱۷۳ کل، ۱۱۶۳ موفق، ۰ رد، ۱۰ Skip (تست‌های Windows CNG و دسترسی فایل) |
| `python3 scripts/check_static_assets.py`، ClientApp | اجرا نشد | خارج از محدودهٔ بک‌اند |
- **محدودیت اندازهٔ بدنه (۱۶KB):** با خواندن دستی بدنه در handler اعمال می‌شود (فراداده `RequestSizeLimit` فقط در Kestrel کار می‌کند و حذف شد). پیش از هر Parse/اعتبارسنجی/سرویس: `Content-Length` بزرگ‌تر از حد یا بدنهٔ chunked بزرگ‌تر از حد ← ۴۱۳ با `detail=trade_request_too_large`. تست‌ها (روی TestServer، هر دو حالت Content-Length و chunked): دقیقاً ۱۶۳۸۴ بایت پذیرفته و اعتبارسنجی می‌شود؛ ۱۶۳۸۵ بایت ۴۱۳؛ بدنهٔ بزرگ و JSON نامعتبر باز ۴۱۳؛ کاربر بدون مجوز ۴۰۳ می‌گیرد (دروازه جلوتر است)؛ ایجاد بزرگ‌تر از حد هیچ ردیفی نمی‌سازد (SQL). **پوشش‌نشده:** اجرای واقعی روی Kestrel (تست با `UseKestrel` ساخته نشد؛ منطق به Stream بدنه وابسته است نه ویژگی میزبان، پس انتظار می‌رود یکسان باشد ولی تست نشده).
- **۴۰۱ ناشناس:** پاسخ ۴۰۱ از میان‌افزار احراز هویت، پیش از فیلتر گروه Trade می‌آید؛ برای افزودن `no-store` بدون دست‌زدن به گروه‌های دیگر API ساده نیست و عمداً تغییر نکرد.

## تغییرات مجاز دور چهارم (تأیید مالک)
- **PUT/POST نیازمند view:** `TradeOperationMetadata` به‌صورت افزایشی `AdditionalReadPermissions` گرفت؛ دروازه پس از مجوز اصلی، مجوزهای Read اضافه را هم می‌سنجد. کاربر فقط‌دارای `update` (یا فقط `create`) پیش از handler ۴۰۳ `trade_forbidden` می‌گیرد؛ پاسخ برای شناسهٔ موجود و ناموجود بایت‌به‌بایت یکسان و بدون هیچ فیلد طرف‌حساب است (تست). **تصمیم POST:** همان قاعده (create ⇒ view) اعمال شد، چون POST هم رکورد را برمی‌گرداند و ۴۰۹ کد تکراری برای کاربر بدون view وجود کدها را لو می‌دهد (oracle).
- **نوشتن Party در دورهٔ بسته مجاز شد (فقط POST/PUT parties):** `TradeAccessOptions.PeriodIndependent` / فراداده `periodIndependent` (پیش‌فرض false برای همهٔ عملیات دیگر و آینده). دروازه و `ITradeAuthorization.HasPermissionAsync` (پارامتر اختیاری `periodIndependent` پس از `cancellationToken`) فقط برای همین عملیات مانع دورهٔ بسته را کنار می‌گذارند؛ هویت، Anti-forgery، زمینهٔ کاری، Tenant، مجوز و entitlement همچنان الزامی‌اند. **یافتهٔ ADR:** D-063 بند ۳ (`docs/DECISIONS.md`) تصریح می‌کند Party/Product بدون `FiscalPeriodId` و در سطح tenant‌اند و D-062 «دورهٔ بسته فقط‌خواندنی است» را برای رکورد/عملیات **دوره‌محور** می‌آورد؛ هیچ ADR صراحتاً دورهٔ‌بستن داده پایه را الزام نمی‌کند. با این حال سه متن عمومی‌تر هنوز می‌گویند «هر Write در دورهٔ بسته رد می‌شود» و باید با تأیید مالک هماهنگ شوند (من نویسنده نیستم): `docs/TRADE_MODULE.md` سطر ۶۹، `docs/SECURITY.md` سطر ۱۷۱ («هر Endpoint نوشتن آینده باید AllowsWrites را بررسی کند»)، و کامنت `ITradeAuthorization`. حالت «فقط‌خواندنی entitlement» وجود ندارد (فقط `IsEnabledForCompanyAsync` بولی است)، پس چیزی برای حفظ نبود.
- **Capabilities:** تغییری نکرد. در دورهٔ بسته هنوز `accessMode="ReadOnly"` و `permissions` شامل کدهای party می‌آید (تست). **قاعدهٔ فرانت باید برای Party عوض شود:** ایجاد/ویرایش را از `available` + وجود `trade.form.party.create|update` **و** `trade.form.party.view` تعیین کند، نه از `accessMode`؛ `accessMode` فقط برای عملیات وابسته به دوره بماند.
- **ممیزی (بررسی، بدون پیاده‌سازی):** هیچ قرارداد موجود ممیزی `SecurityAuditEvents` را برای ایجاد/ویرایش Party **الزامی** نکرده است: `docs/TRADE_MODULE.md` فقط ممیزی گزارش چندشرکتی را می‌خواهد (سطرهای ۱۰۸، ۱۴۳)؛ D-083 فقط رویدادهای چرخهٔ عمر را مطرح و تا تأیید مالک الزام نمی‌داند (`docs/SECURITY.md:376`)؛ `SECURITY.md:370` رویداد جدید را فقط با Issue جدا می‌پذیرد. HR (مثلاً `Server/Api/HrOrganizationalStructureEndpoints.cs:381`) ممیزی می‌کند چون با `DatabaseContext` هسته در یک تراکنش است؛ Trade از `TradeDbContext` جدا استفاده می‌کند و ممیزی اتمیک بین دو Context ارزان نیست. **به تعویق افتاد**؛ تصمیم مالک/Issue جدا.
- **`AccountKind=User` روی گروه `/trade` (بررسی، اعمال نشد):** گروه‌های HR این را دارند (`HrOrganizationalStructureEndpoints.cs:58-61`) و قراردادهای HR/Self-service آن را الزام کرده‌اند (`docs/HR_FRONTEND_API_CONTRACT.md:13`، `USER_SELF_SERVICE.md`) ولی هیچ قرارداد Trade آن را الزام نکرده است. فعلاً حساب زیرساختی احتمالاً پیش از داده در دروازه (نشست/عضویت) رد می‌شود (**تست نشده**). پیشنهاد: Issue جدا با تست.

## یادداشت فنی: ValueGeneratedNever روی `TradeEntity.Id` — به تعویق افتاد، بازبینی مستقل
نقش جدیدِ افزوده‌شده با `Party.AddRole` کلید Guid سمت کلاینت (در سازنده) دارد. EF Core هنگام `DetectChanges` موجودیتی را که با کلید تنظیم‌شده و کلید تولیدی (قرارداد پیش‌فرض Guid) در ناوبری کشف می‌کند، `Modified` فرض می‌کند؛ نتیجه `UPDATE` روی ردیف ناموجود و `DbUpdateConcurrencyException` (۴۰۹ کاذب) است — با حذف موقت راه‌حل، تست `UpdateEnforcesRowVersion...` شکست خورد. `PartyStore.SaveChangesAsync` ردیف‌های `PartyRoleAssignment` با وضعیت Modified و RowVersion خالی را Added می‌کند. راه ریشه‌ای `ValueGeneratedNever()` روی `Id` است، ولی مدل/Snapshot (و احتمالاً Migration) را تغییر می‌دهد و روی همهٔ موجودیت‌های Trade اثر دارد؛ بنابراین به تأیید مالک و بازبینی مستقل (database-engineer/architect) موکول شد. تا آن زمان راه‌حل در `PartyStore` بماند و تست حذف/افزودن مجدد نقش آن را پوشش می‌دهد.

## اصلاحات پس از بازبینی دور چهارم
- **Content-Type:** در `TradeAccessFilter` (پس از بررسی فراداده و پیش از Anti-forgery) هر درخواست تغییردهندهٔ Trade باید `application/json` باشد (charset مجاز)، وگرنه ۴۱۵ با `detail=trade_unsupported_media_type`؛ بدون بدنه و بدون Content-Type همچنان به Anti-forgery می‌رسد (۴۰۰ قبلی)، ولی بدنه بدون Content-Type ۴۱۵ است. تست: form-urlencoded، multipart، text/plain و بدون نوع ← ۴۱۵؛ JSON با charset رد نمی‌شود.
- **تست‌های اضافه:** آداپتور (بستهٔ بسته: پیش‌فرض رد، `periodIndependent` + مجوز مجاز، بدون مجوز/Context جعلی رد، Read بی‌اثر)؛ معماری با IL scan که فقط `TradeAccessGate` را فراخواننده `ITradeAuthorization.HasPermissionAsync` می‌یابد (با کنترل مثبت)؛ بدنهٔ خالی ← ۴۰۰ `trade_invalid_request_body`؛ حد دقیق ۱۶۳۸۴ بایت از قبل پوشش داشت.
- **متن‌ها:** کامنت‌های `ITradeAuthorization`، `WorkContext`، دروازه و `TradeCapabilities.AccessMode` با استثنای «عملیات period-independent (D-084، داده پایهٔ Party)» هماهنگ شد؛ `TradeAccessOptions` اکنون ویژگی‌های `init` ساده دارد. پاسخ Capabilities بدون تغییر.
- نتایج: ۱۶۶ تست Trade روی SQL Server واقعی (۳ بار) و کل پروژه؛ اعداد دقیق در گزارش پایانی Lead.

## امنیت، داده و سازگاری
- بدون Migration/Schema جدید؛ بدون دسترسی به `PartyBankAccounts`.
- `legalIdentifier` هرگز در فهرست، جست‌وجو، Log یا خطا نیست.
- مرور مستقل کد و امنیت: یافتهٔ Critical/High/Medium نداشت؛ موارد Low (L-1 اندازهٔ بدنه، L-2 نویسه‌های کنترلی/بیدی، ۴۰۱ بدون no-store، `.ToList()` پیش از حلقه، تست Anti-forgery ضعیف) در همین تحویل اصلاح یا ثبت شد.

## ریسک‌ها و کارهای باقی‌مانده
- L-3 (update ⇒ view) در دور چهارم با تأیید مالک اعمال شد.
- سیاست `AccountKind=User` روی گروه `/trade` وجود ندارد — Issue جدا.
- Migrationهای Trade در نصب واقعی توسط میزبان اعمال نمی‌شوند (فقط در تست‌ها)؛ مسیر اعمال باید جدا تصمیم‌گیری شود.
- ایجاد طرف‌حساب `updatedAtUtc` غیر Null دارد (`AddRole` → `RecordUpdate`).
- تصمیم `ValueGeneratedNever` (بالا) و D-083 (حذف/غیرفعال‌سازی) باز است.

## شناسه‌های Git
- Branch: `feat/trade-s2-3-party-slice`
- Commit: ثبت نشده
- Pull Request: ندارد
