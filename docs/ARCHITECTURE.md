# معماری و مرزبندی مسئولیت‌ها

## وضعیت موجود
- `Server`: برنامه وب Razor Pages، PageModelها، middlewareها، تنظیمات و فایل‌های ایستا.
- `Domain`: مدل‌های دامنه و enumerationها.
- `Infrastructure/Persistence` (نام‌فضای فعلی `Data`): `DbContext`، پیکربندی EF Core و دسترسی داده.
- `ViewModels`: مدل‌های مورد استفاده صفحات/فرم‌ها.
- `Resources`: منابع چندزبانه.
- `Application`: پروژه اسکلت برای Use Caseهای آینده؛ فعلاً جریان‌های موجود به آن منتقل نشده‌اند.
- `Constants`, `Dtat`, `Dtat.Seedwork.Abstractions`, `Dtat.StateMachine*`: کتابخانه‌های کمکی و مشترک.
- `Server/Api`: API نسخه 1 (Minimal API) برای کلاینت React؛ `Server/Infrastructure/Tenancy`: تصمیم سمت سرور دسترسی به شرکت.
- `ClientApp`: کلاینت React + TypeScript + Vite + Tailwind؛ خروجی آن در `Server/wwwroot/app` و مسیر `/app`.

این ساختار هنوز Clean Architecture کامل نیست: `Server` مستقیماً به `Data` و EF Core وابسته است و بخشی از منطق کاربردی در PageModelها قرار دارد.

## معماری هدف
1. **Domain**: موجودیت‌ها، Value Objectها، قوانین دامنه و قراردادهای مستقل؛ بدون وابستگی به Web/EF Core.
2. **Application**: Use Caseها، DTOها، اعتبارسنجی کاربردی، قرارداد Repository/سرویس‌ها و orchestration.
3. **Infrastructure**: پیاده‌سازی قراردادهای داده، EF Core، سرویس‌های بیرونی و persistence.
4. **Web**: Razor Pages، PageModelها، احراز هویت/مجوزدهی وب و ترکیب وابستگی‌ها.
5. **Tests**: Unit Test مستقل و Integration Test برای اجزای متصل به دیتابیس/وب.

## جهت وابستگی هدف
- Web → Application
- Infrastructure → Application و Domain
- Application → Domain
- Domain → هیچ پروژه بیرونی

در فاز مهاجرت، نام‌ها و مسیرهای فعلی حفظ می‌شوند تا جابه‌جایی مکانیکی، رفتار را خراب نکند. پروژه‌های جدید فقط وقتی افزوده شوند که مرز واقعی مسئولیت مشخص باشد.

## پارادایم‌ها
- **OOP**: انتخاب اصلی برای مدل‌سازی موجودیت‌ها و رفتارهای دارای هویت/مسئولیت.
- **Procedural**: مناسب اسکریپت‌ها و جریان‌های ساده؛ نه دلیل برای شکستن هر عملیات به کلاس‌های اضافی.
- **Functional**: توابع خالص، LINQ و داده‌های immutable در جایی که خوانایی و تست‌پذیری را بهتر می‌کند.
- **Event-Driven**: برای رخدادهای واقعی دامنه یا یکپارچه‌سازی؛ از Event Bus بدون نیاز واقعی استفاده نشود.
- **Declarative**: تنظیمات DI، EF Core Fluent API، Routing و Validation.
- **Logic**: قواعد صریح و قابل تست در Domain/Application؛ بدون افزودن چارچوب منطق جداگانه مگر نیاز روشن.
- **Concurrent/Async**: عملیات I/O به‌صورت async با `CancellationToken`؛ از `.Result`/`.Wait()` و اجرای موازی روی یک DbContext اجتناب شود.

## اصول طراحی
- **SOLID**: مسئولیت متمرکز؛ توسعه‌پذیری بدون اصلاح گسترده؛ جانشینی صحیح؛ Interfaceهای کوچک؛ وابستگی به abstraction در مرزهای مناسب.
- **DRY**: فقط تکرار معنادار را حذف کن؛ دو رفتار مشابه اما با قواعد تغییر متفاوت لزوماً نباید ادغام شوند.
- **KISS**: ساده‌ترین راه‌حل قابل نگهداری را انتخاب کن.
- **YAGNI**: قابلیت آینده‌نگر بدون نیازمندی فعلی نساز.
- **SoC**: UI، منطق کاربردی، دامنه و persistence را جدا نگه دار.
- **LoD**: زنجیره‌های طولانی دسترسی به اشیای داخلی را کاهش بده؛ متدهای معنادار و قراردادهای روشن ترجیح دارند.

## DDD (Domain-Driven Design)
- مدل دامنه باید زبان مشترک کسب‌وکار را منعکس کند؛ نام کلاس‌ها و عملیات بر اساس مفهوم دامنه انتخاب شوند.
- Entity، Value Object، Aggregate و Domain Event فقط در صورت وجود مرز و قاعده واقعی دامنه معرفی شوند.
- Aggregate Root مرز سازگاری قواعد را مشخص می‌کند؛ از Repository عمومی برای هر جدول بدون نیاز دامنه اجتناب شود.
- منطق کسب‌وکار در PageModel یا EF Core Configuration قرار نگیرد.
- Bounded Contextها فقط زمانی جدا شوند که واژگان/قواعد یا چرخه تغییر مستقل دارند؛ برای اسکلت اولیه context مصنوعی ایجاد نشود.

## الگوهای طراحی — فقط در صورت نیاز
- **Singleton**: فقط سرویس stateless و thread-safe؛ نه برای DbContext یا داده وابسته به درخواست.
- **Factory / Abstract Factory**: ساخت شیء پیچیده یا خانواده‌ای از پیاده‌سازی‌ها.
- **Builder**: ساخت تدریجی شیء پیچیده با چند گزینه.
- **Adapter**: سازگارکردن API خارجی با قرارداد داخلی.
- **Decorator**: افزودن رفتار جانبی بدون تغییر پیاده‌سازی اصلی، مثل caching/logging هدفمند.
- **Facade**: ساده‌سازی API چند جزء پیچیده.
- **Proxy**: کنترل دسترسی/بارگذاری/ارتباط؛ با دقت نسبت به رفتار پنهان.
- **Observer**: انتشار رخداد به چند مصرف‌کننده؛ برای رخدادهای دامنه با قرارداد روشن.
- **Strategy**: تعویض الگوریتم/سیاست در زمان اجرا.
- **Command**: نمایش عملیات به‌صورت درخواست مستقل، فقط اگر به صف/تاریخچه/dispatch نیاز باشد.
- **Iterator**: عموماً از abstractionهای استاندارد .NET استفاده شود.
- **State**: رفتارهای متفاوت بر اساس وضعیت‌های معتبر؛ نه جایگزین enum ساده.

## MVC، MVVM، Layered، Clean، Monolith و Microservices
Razor Pages خودش مدل Page/PageModel دارد و نیازی نیست پروژه را به‌زور MVC یا MVVM نام‌گذاری کنیم. تفکیک لایه‌ای و جهت وابستگی از انتخاب برچسب معماری مهم‌تر است. Modular Monolith نقطه شروع است؛ Microservices تنها با دلیل عملیاتی/دامنه‌ای انتخاب می‌شود.


## Frontend و API (2026-10-20)

```
Browser ──(Cookie HttpOnly, same origin)──▶ ASP.NET Core (Server)
  ├─ /Account/Login, /, /Admin/*   Razor Pages (ورود و صفحات موجود — مسیر بازگشت)
  ├─ /app/*                        index.html کلاینت React (wwwroot/app)
  └─ /api/v1/*                     Minimal API ──▶ CompanyAccessService ──▶ DatabaseContext (EF Core / SQL Server)
```

- **جریان ورود:** کلاینت هیچ فرم ورودی ندارد. پاسخ 401 از API کاربر را به `/Account/Login?ReturnUrl=/app/...` می‌برد؛ صفحه ورود فقط `ReturnUrl` محلی را می‌پذیرد.
- **قرارداد API v1** (JSON، camelCase، همه نیازمند ورود، پاسخ‌ها `no-store`):

| Method | مسیر | پاسخ | توضیح |
|---|---|---|---|
| GET | `/api/v1/session` | `{ username, fullName, accountKind, isAdministrator }` | هویت تأییدشده نشست |
| GET | `/api/v1/antiforgery/token` | `{ headerName, requestToken }` | Cookie ضد جعل را صادر می‌کند |
| POST | `/api/v1/session/logout` | 204 / 400 | نیازمند هدر `X-XSRF-TOKEN` |
| GET | `/api/v1/companies` | `[{ id, name, nationalId, roleName }]` | فقط شرکت‌های قابل دسترس؛ حساب زیرساختی: `[]` |
| GET | `/api/v1/companies/{id}/overview` | `{ company, fiscalPeriods: [{ id, title, startDate, endDate, status }] }` / 404 | تاریخ‌ها ISO `yyyy-MM-dd` (میلادی)؛ 404 برای شرکت غیرمجاز یا ناموجود |

- **خطاها:** 401 (نشست نیست/منقضی یا نشست قدیمی بدون شناسه)، 403، 404، 400 (Anti-forgery) و 500 به‌صورت `application/problem+json` با عنوان قابل نمایش؛ جزئیات فنی فقط در Log.
- **مرز مسئولیت:** قواعد دسترسی و داده فقط در Backend. کلاینت پاسخ‌ها را در زمان اجرا با قرارداد بررسی می‌کند (`ClientApp/src/api/types.ts`) و فقط نمایش (از جمله تبدیل تاریخ به شمسی) را انجام می‌دهد.
- **ساختار فعلی Backend:** هنوز Modular Monolith با یک پروژه وب است. منطق جدید دسترسی در یک سرویس کوچک و قابل تست قرار گرفته و Endpointها فقط هماهنگ‌کننده هستند. انتقال آن به پروژه `Application` زمانی انجام می‌شود که Use Case حسابداری واقعی (و قرارداد مستقل از EF) وجود داشته باشد؛ ایجاد Interface/Repository صرفاً برای لایه‌بندی انجام نشد (D-006).
