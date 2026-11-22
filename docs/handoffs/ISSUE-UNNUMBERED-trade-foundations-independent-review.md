# بازبینی مستقل پایه‌های موجود Trade (Workstream D)

- **Issue:** UNNUMBERED (ادامهٔ Issue #397 / S3-2 زیرساخت)
- **مبنا:** `main@5484758722d0a316511806cf3097c5c691875599` (کد Trade از `ac0ec741` تغییر نکرده است).
- **وضعیت سند:** فقط مستندات، بازبینی ایستا. هیچ Build/Test اجرا نشد. شمارش تست‌ها از خواندن کد تست است، نه نتیجهٔ اجرا. تست‌های SQL Server با `Assert.SkipUnless(SqlServerAvailable())` محافظت می‌شوند و بدون SQL Server «رد (Skip)» می‌شوند؛ پاس حساب نمی‌شوند.
- **روش:** بازبین `architect` فقط‌خواندنی؛ ادعاهای پرتأثیر او توسط Lead با کد تطبیق داده شد (نویسه‌های ممنوع Party/Product، منطق گرد‌کردن `PaymentPlanCalculator`، دو معنایی کد خروج ۳، نبود `--migrate` در حالت `setupRequired`، مقدار `IRR`).
- **برچسب:** [V] در کد دیده شد؛ [D] فقط مستندات؛ [P] پیشنهاد.

**نتیجهٔ اصلی [V]:** در زمان اجرا تنها پیاده‌سازی ثبت‌شدهٔ entitlement کلاس `DenyAllTradeModuleEntitlement` است (`Server/Program.cs:284-285`) و همیشه `false` برمی‌گرداند. پس endpointهای Party/Product/Reference wired هستند اما در Production با `403 trade_module_unavailable` بسته‌اند؛ این رفتار عمدی و Fail-Closed است، نه نقص.

---

## ۱) جدول خلاصه

| مورد | wired در Runtime؟ | تست‌ها (شمارش از کد) | شکاف اصلی | حکم |
|---|---|---|---|---|
| `TradeDocumentNumber` | خیر (ابزار دامنه) | ۱۷ | ZWNJ رد می‌شود ولی Party/Product می‌پذیرند؛ مصرف‌کننده ندارد | آمادهٔ استفادهٔ بعدی؛ **عملیاتی نیست** |
| تبدیل واحد/Snapshot | خیر (ابزار دامنه) | ۴۵ | عضویت کد واحد در فهرست مرجع چک نمی‌شود (مسئولیت اولین مسیر Application)؛ Schema ندارد | **عملیاتی نیست** |
| `PaymentPlan`/`Calculator` | خیر | ۳۵ | بیش‌تخصیص گرد‌کردن، انحراف درصد مرحلهٔ آخر، `IRR` با ۲ رقم | **عملیاتی نیست**؛ قبل از wire شدن تصمیم می‌خواهد |
| `TradeAccessGate` + `TradeAccessFilter` | بله | ۱۶+۱۸+۱۵+۱۰+۲ | `TenantId` عملاً `CompanyId` است (D-087، خارج از Trade)؛ تست HTTP برای «دورهٔ بسته + DenyAll» ندارد | با شرط آماده |
| Capability endpoint | بله | داخل موارد بالا | — | با شرط آماده |
| Party/Product (API+UI) | بله، ولی در Production در دسترس نیست | سرور ۱۹+۱۶+۲۸+۲۳+۲؛ کلاینت ≈۱۶۴ | فیلتر نویسهٔ Party ناقص؛ نویسهٔ نامرئی در کد؛ نرمال‌سازی کد | با شرط آماده |
| داده‌های مرجع | بله (کشور، واحد) | ۸ | ارز/Incoterm/روش پرداخت endpoint ندارند (عمدی) | با شرط آماده |
| Migration Host Trade (#665) | بله (`--migrate`) | ۸ + ۴ (SQL Server) | کد ۳ دو معنا؛ `--include-trade` بدون `--migrate` ساکت؛ قفل `master` | با شرط آماده |

هیچ موردی production-ready نیست: یا wired نیست، یا پشت `DenyAll` است، یا تست‌های SQL Server آن در این بازبینی اجرا نشده‌اند.

---

## ۲) جزئیات هر مورد

### `TradeDocumentNumber` — `Modules/Trade/Trade.Domain/Common/TradeDocumentNumber.cs`
- پیاده‌سازی [V]: NFKC، ارقام فارسی/عربی→ASCII، ی/ک عربی→فارسی، upper-invariant؛ رد نویسه‌های Control/Format/Mark/Surrogate/PrivateUse؛ کنترل طول قبل و بعد از NFKC؛ برابری روی `Normalized`.
- Runtime: هیچ مصرف‌کننده‌ای بیرون از `tests/Template.Tests/TradeDocumentNumberTests.cs`.
- شکاف: (الف) ZWNJ رد می‌شود (تست در خط ۱۲۳) در حالی که Party/Product آن را می‌پذیرند — سیاست باید تعیین شود. (ب) بدون scope یکتایی تا وقتی مصرف‌کننده نیامده. (ج) تست صریح برای رد همهٔ نویسه‌های خارج BMP و homoglyph ندارد (خود کد homoglyph را خارج از محدوده اعلام کرده).

### تبدیل واحد/Snapshot — `Modules/Trade/Trade.Domain/Units/*`
- پیاده‌سازی [V]: سقف‌های حسابی (Quantity ≤ 1e9، Factor ≤ 1e6، Scale ≤ 6)، `TradeRoundingPolicy` بدون پیش‌فرض ضمنی، `TradeQuantitySnapshot` با `TryRestore` که مقدار را دوباره محاسبه می‌کند، `ProductUnitConversionSet` با واحد پایه صریح.
- Runtime: فقط تست‌ها. `Product.BaseUnitCode` به `ProductUnitConversionSet` وصل نیست و ذخیره‌سازی ندارد.
- شکاف: `TryCreate` کد واحد را با فهرست مرجع نمی‌سنجد (مستند‌شده؛ باید در اولین مسیر Application انجام شود). ایراد حسابی پیدا نشد.

### `PaymentPlan`/`PaymentPlanCalculator` — `Modules/Trade/Trade.Application/Proformas/`
- پیاده‌سازی [V]: حداکثر ۱۲ مرحله؛ درصد با حداکثر ۴ رقم اعشار؛ جمع باید دقیقاً ۱۰۰ باشد؛ مراحل غیرآخر `AwayFromZero` به `MinorUnits` ارز؛ باقی‌مانده به مرحلهٔ آخر؛ ورودی یک‌بار در کپی خصوصی خوانده می‌شود. سرریز ممکن نیست (حداکثر ≈1e20 زیر سقف decimal).
- Runtime: فقط تست‌ها؛ Proforma و Schema وجود ندارد.
- شکاف‌های صحت (تصمیم می‌خواهند، نه quick fix) [V کد، P راه‌حل]:
  1. **رد پلن معتبر به‌خاطر بیش‌تخصیص:** `JPY` (۰ رقم)، `finalAmount=5`، ده مرحلهٔ ۱۰٪ → هر مرحلهٔ غیرآخر `round(0.5)=1`؛ نُه مرحله ۹ می‌شود؛ باقی‌مانده −۴ → `ResidualStageNotPositive`. روش largest-remainder این را حل می‌کند [P].
  2. **انحراف درصد مرحلهٔ آخر:** `Percent` مرحلهٔ آخر همان ورودی است ولی `Amount` باقی‌مانده است (مثلاً USD، `0.05`، چهار مرحلهٔ ۲۵٪: مرحلهٔ آخر ۰٫۰۲ ≈ ۴۰٪ ولی «۲۵٪» گزارش می‌شود).
  3. **`IRR` با MinorUnits=۲** (`ReferenceData/MiscData.cs:8`) مطابق ISO است ولی با عرف عملی ریال (صفر رقم) فرق دارد؛ تصمیم مالک.
- تست غایب: سناریوی بیش‌تخصیص چندمرحله‌ای (رفتار فعلی قفل نشده است).

### `TradeAccessGate` و `TradeAccessFilter`
- ترتیب [V]: هویت ← context ← بستن نوشتن در دورهٔ ReadOnly (`TradeAccessGate.cs:40`) ← entitlement (`:45`) ← مجوز کاربر ← مجوزهای Read اضافه. نگاشت: `PeriodReadOnly`→`409 trade_period_read_only`، `ModuleUnavailable`→`403 trade_module_unavailable` (`TradeAccessFilter.cs:139-140`). این تفکیک در مستندات PR #744 (ادغام‌شده در `main@5a8e7a14`) اصلاح شده بود و در اینجا دوباره با کد تأیید شد.
- `PeriodIndependent` فقط از metadata سمت سرور می‌آید، فقط برای Write اثر دارد و Adapter دوباره آن را اعمال می‌کند؛ مجوزهای Read اضافه همچنان لازم‌اند.
- خطای entitlement بلعیده و به `ModuleUnavailable` تبدیل می‌شود (Fail-Closed).
- Runtime: wired (`Program.cs:286`، `TradeEndpoints.cs:21`)؛ گروه `/api/v1` خود `RequireAuthorization()` دارد.
- شکاف‌ها: `TradeAccessContext.TenantId` در واقع `CompanyId` است (`TradeAuthorizationAdapter.cs:45`)؛ انحراف D-087 و فقط با S3-0 رفع می‌شود. تست HTTP برای «دورهٔ بسته + DenyAll → 409» و تست 415 (بدنهٔ بدون Content-Type) در فهرست Access دیده نشد.

### Capability endpoint — `GET /api/v1/trade/capabilities`
- wired؛ کلاینت (`ClientApp/src/features/trade/access/TradeAccess.tsx`) استفاده می‌کند؛ در Production `Available:false`؛ پاسخ `no-store`؛ فهرست Permissions فقط در حالت Available پر می‌شود. شکافی پیدا نشد.

### Party/Product (API و کلاینت)
- سرور: `Server/Api/Trade/TradePartyEndpoints.cs`، `TradeProductEndpoints.cs`؛ سرویس‌ها و Storeها؛ بدنه ≤ ۱۶KB؛ هر query صریحاً `TenantId` و `!IsDeleted` را اعمال می‌کند؛ کد تکراری با pre-check و index یکتای فیلترشده → `409`؛ `RowVersion` الزامی برای Update.
- عملاً Company-scoped است (`TenantId`=Company.Id، D-087).
- شکاف‌ها [V]:
  1. `PartyService.HasForbiddenCharacters` (`PartyService.cs:319-321`) نویسه‌های `U+061C`، `U+2028`، `U+2029`، `U+FEFF` را رد نمی‌کند، در حالی که `ProductService.cs:260-263` رد می‌کند. (ناسازگاری سیاست؛ **رفع آن کوچک و مستقل است**.)
  2. نویسه‌های Format مانند `U+200B`/`U+2060` در `code` پذیرفته می‌شوند؛ دو کد ظاهراً یکسان می‌تواند ثبت شود. (رفتار ورودی را عوض می‌کند → تأیید مالک.)
  3. کد Party/Product فقط Trim می‌شود؛ ارقام فارسی و ی/ک عربی یکسان‌سازی نمی‌شوند (برخلاف `TradeDocumentNumber`) → تصمیم سیاستی.

### داده‌های مرجع
- wired: `/reference/countries` (مجوز `party.view`)، `/reference/units` (`product.view`)؛ اعتبارسنجی کشور در Party و واحد در Product. ارز/Incoterm/روش پرداخت endpoint ندارند (YAGNI).

### میزبان Migration Trade (#665)
- [V] Core همیشه اول؛ Trade فقط با `--include-trade`؛ `sp_getapplock` (Exclusive، Session، `LockTimeout=0`) روی اتصال جدا؛ جدول `trade` بدون `__TradeMigrationsHistory` → توقف بدون Baseline خودکار؛ کدهای خروج ۰..۴؛ پیام‌ها redact.
- شکاف‌ها:
  1. **کد ۳ دو معنا دارد** [V] (`DatabaseMigrationHost.cs:139-150`): «قفل در اختیار اجرای دیگر است» و «اتصال/گرفتن قفل شکست خورد» هر دو `LockNotAcquired`.
  2. `--migrate` با `setupRequired` بی‌صدا نادیده گرفته می‌شود (`Program.cs:372`: `if (!setupRequired && migrateOnly)`) و وب‌سرور عادی بالا می‌آید؛ `--include-trade` بدون `--migrate` هم ساکت است.
  3. اگر دیتابیس وجود نداشته باشد، قفل روی `master` گرفته می‌شود؛ قفل application به دیتابیس جاری محدود است — ریسک اجرای هم‌زمان در پنجرهٔ ایجاد DB [P: بررسی نشده؛ احتمالاً قفل داخلی EF Core آن را کم می‌کند].
  4. `OperationCanceledException` هنگام گرفتن قفل ممکن است `lockConnection` را dispose نکند.
- این‌ها در `Server/Program.cs` و `Server/Infrastructure/Hosting` (میزبان مشترک) هستند؛ **خارج از مالکیت Trade** و تغییر قرارداد اپراتوری‌اند؛ فقط با تأیید مالک.

---

## ۳) نقص‌های کوچک: فهرست و تفکیک

| # | نقص | مالکیت | اقدام |
|---|---|---|---|
| ۱ | ناسازگاری فیلتر نویسه Party با Product | داخل Trade، بدون تصمیم معماری | اصلاح مستقل با تست (PR کد جدا؛ این سند را تغییر نمی‌دهد) |
| ۲ | نویسهٔ نامرئی Format در `code` | داخل Trade، رفتار ورودی را عوض می‌کند | نیاز به تأیید مالک |
| ۳ | تست رفتار بیش‌تخصیص `PaymentPlanCalculator` | داخل Trade | تست ثبت رفتار فعلی؛ تغییر الگوریتم تصمیم می‌خواهد |
| ۴ | تست HTTP «دورهٔ بسته + DenyAll → 409» | داخل Trade | تست افزوده شود |
| ۵–۸ | کد ۳، `setupRequired`، dispose، قفل `master` | **خارج از Trade** (میزبان مشترک) | فقط گزارش؛ تأیید مالک + بازبینی امنیتی |

نیازمند تصمیم (نه quick fix): روش تخصیص درصدها، MinorUnits ریال، سیاست ZWNJ در `TradeDocumentNumber`، نرمال‌سازی ارقام/ی/ک در کد Party/Product، نگاشت `TenantId`→`CompanyId` (D-087، منتظر S3-0).

---

## ۴) محدودیت‌ها
- Build/Test اجرا نشد (`dotnet` در این محیط نصب نیست و کار فقط‌خواندنی بود)؛ هیچ ادعای «تست پاس شد» وجود ندارد.
- `.cshtml`، دیتابیس واقعی و رفتار ClientApp در Runtime بررسی نشد.
- مصرف توکن Agent: ≈۱۳۹٬۷۵۶ توکن، ۲۳ فراخوانی ابزار (از اعلان تکمیل).
