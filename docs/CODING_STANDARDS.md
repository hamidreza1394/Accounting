# استانداردهای جامع کدنویسی

## پشته و قراردادها
- Target فعلی `net10.0`، C# 14، ASP.NET Core Razor Pages و EF Core 10 است. برای توسعه رسمی از Visual Studio 2026 نسخه 18.0 یا بالاتر استفاده شود. نسخه پلتفرم فقط با Issue/ADR و آزمون کامل تغییر کند.
- NuGet نسخه‌ها در `Directory.Packages.props` تعریف شوند؛ وابستگی‌های EF Core و providerها هم‌نسخه و سازگار باشند.
- UI از Bootstrap 5.3+ محلی و Bootstrap Icons محلی هدف‌گذاری شده است؛ نسخه فعلی دارایی‌های Bootstrap در مخزن هنوز قدیمی‌تر است و باید در وظیفه ارتقای فرانت‌اند به‌روزرسانی و آزمایش شود. تغییر نسخه در `libman.json` بدون جایگزینی واقعی فایل‌ها مجاز نیست.
- هیچ CDN یا فونت خارجی برای دارایی زمان اجرا استفاده نشود. اسکریپت `scripts/check_static_assets.py` این قاعده را برای کد متعلق به برنامه کنترل می‌کند.

## پارادایم‌ها و اصول طراحی
- **OOP:** برای مدل‌سازی رفتار و هویت.
- **Functional/declarative:** برای توابع خالص، LINQ، تنظیمات و تبدیل‌های قابل پیش‌بینی.
- **Event-driven/concurrent:** فقط وقتی رخداد، جداسازی یا اجرای هم‌زمان نیاز واقعی دارد.
- **SOLID:** مسئولیت متمرکز، توسعه‌پذیری، قراردادهای قابل جایگزینی، Interface کوچک و وابستگی به abstraction در مرز درست.
- **DRY:** تکرار واقعی را حذف کن؛ انتزاع زودهنگام برای کدهای صرفاً شبیه ممنوع.
- **KISS/YAGNI:** راه‌حل ساده متناسب با نیاز فعلی؛ قابلیت آینده بدون نیازمندی نساز.
- **SoC/LoD:** مرز UI، Use Case، Domain و Persistence روشن باشد؛ از دسترسی زنجیره‌ای به جزئیات اشیا پرهیز کن.
- **DDD:** Entity، Value Object، Aggregate، Domain Event و Bounded Context تنها در صورت وجود مفهوم و قاعده واقعی دامنه معرفی شوند.
- **TDD/BDD:** برای منطق مهم و ریسک بالا تست را زود بنویس؛ معیار پذیرش را با زبان قابل فهم کسب‌وکار تعریف کن.

## معماری و وابستگی‌ها
- ساختار موجود چندپروژه‌ای است؛ آن را به‌زور به الگوی دیگری تبدیل نکن. معماری هدف و جهت وابستگی در `docs/ARCHITECTURE.md` تعریف شده است.
- `Domain` به `Server` یا `Infrastructure` وابسته نشود.
- `PageModel` وظیفه دریافت/اعتبارسنجی درخواست و تولید پاسخ را دارد؛ قواعد دامنه و Use Case پیچیده را به Application منتقل کن.
- `Infrastructure/Persistence` مالک EF Core و جزئیات SQL Server است. قراردادهای کاربردی در مرز مناسب تعریف شوند.
- از Repository عمومی برای هر جدول، Mediator، CQRS، Event Bus، Microservices یا لایه اضافی بدون مسئله مشخص استفاده نکن.
- DI: `DbContext` و سرویس‌های وابسته به درخواست عموماً Scoped؛ Singleton فقط برای state مستقل از درخواست و thread-safe. عمر سرویس را با وابستگی‌ها هماهنگ کن.

## C# و کیفیت کد
- `Nullable` و .NET analyzers فعال بمانند؛ هشدار را در منبع حل کن، نه با خاموش‌کردن Rule.
- برای کد جدید، file-scoped namespace، `var` در صورت خوانایی، pattern matching و primary constructor در جایی که وضوح را افزایش می‌دهد مجازند؛ تبدیل کد قدیمی صرفاً برای استفاده از syntax جدید لازم نیست.
- `required`، `record` و immutable model را فقط وقتی قرارداد داده/مقدار اقتضا می‌کند استفاده کن.
- نام‌ها: Type/Public member=`PascalCase`، local/parameter=`camelCase`، interface با `I`، private field با `_camelCase` برای کد جدید.
- ثابت‌های C# عموماً `PascalCase`؛ `snake_case` و `kebab-case` برای قراردادهای مناسب مانند JSON/CSS/URL، نه قاعده عمومی C#.
- PEP 8 برای Python، PSR برای PHP، Airbnb برای JavaScript و JavaDoc برای Java است؛ استاندارد نامرتبط را بر C# تحمیل نکن.
- API عمومی و قراردادهای غیر بدیهی مستندات XML داشته باشند؛ از توضیح خط‌به‌خط بدیهیات اجتناب کن.
- فایل‌های تولیدی `.Designer.cs`، فایل‌های vendor و فایل‌های تولیدشده را دستی ویرایش نکن مگر فرآیند تولید اجازه دهد.

## Async و دسترسی داده
- I/O دیتابیس، فایل و شبکه تا جای ممکن async؛ `CancellationToken` را در مسیرهای طولانی منتقل کن.
- `.Result`، `.Wait()` و `GetAwaiter().GetResult()` در کد وب ممنوع، مگر استثنای مستند و توجیه‌شده.
- Query خواندنی EF Core معمولاً `AsNoTracking()`؛ projection به ViewModel/DTO را ترجیح بده تا Entity غیرضروری بارگذاری نشود.
- `DbContext` thread-safe نیست؛ آن را بین عملیات موازی به اشتراک نگذار.
- از SQL خام اجتناب کن؛ در صورت ضرورت از API پارامتری استفاده و دلیل را مستند کن.
- Migrationها باید قابل بازبینی، همسو با مدل و دارای برنامه اعمال/بازگشت باشند؛ در Production از تغییر خودکار و بی‌نظارت Schema اجتناب کن.

## تنظیمات، DI و خطا
- از Configuration Providers و Options Pattern استفاده کن؛ مقادیر ثابت عملیاتی و secret در کد نباشند.
- تنظیمات ضروری باید اعتبارسنجی شوند و خطای پیکربندی واضح بدهند؛ مقدار پیش‌فرض ناامن برای دور زدن تنظیمات اضافه نکن.
- Exceptionهای مورد انتظار را در محل مناسب مدیریت کن؛ Exception عمومی را بی‌صدا نبلع. از `ILogger<T>` با structured logging استفاده کن و اطلاعات حساس را ثبت نکن.
- خطاهای Production نباید stack trace، Connection String یا داده حساس به کاربر نشان دهند.

## Razor Pages و Frontend
- از Layout مشترک، Tag Helperها، `asp-for`، `asp-page`، `asp-page-handler` و `asp-validation-for` استفاده کن.
- فرم‌های تغییردهنده داده Anti-forgery داشته باشند؛ Razor Pages معمولاً Anti-forgery را برای POST به‌صورت پیش‌فرض فراهم می‌کند، اما آن را غیرفعال نکن و در تست مسیرهای mutation تأیید کن.
- اعتبارسنجی سمت سرور الزامی است؛ اعتبارسنجی سمت کلاینت مکمل است نه جایگزین.
- خروجی کاربر را امن encode کن؛ برای HTML خام، JavaScript پویا و فایل‌های بارگذاری‌شده بازبینی XSS/Path Traversal لازم است.
- Responsive بودن و دسترس‌پذیری پایه (label، focus، keyboard، contrast، semantic HTML) بررسی شود.
- فونت، آیکون، CSS و JS در زمان اجرا محلی باشند. کتابخانه محلی باید license/version و مسیر مصرف‌کننده مشخص داشته باشد.
- JavaScriptهای جدید به‌صورت ماژولار و بدون global state غیرضروری؛ JSDoc فقط برای قراردادهای پیچیده/عمومی JS.

## Security و validation
- ورودی کاربر با Data Annotations یا validator مناسب و `ModelState.IsValid` سمت سرور بررسی شود.
- برای مجوزدهی، سیاست سرور را ملاک بگیر؛ مخفی‌کردن دکمه مجوز نیست.
- Redirect مقصد کاربر باید local/allow-listed باشد تا Open Redirect ایجاد نشود.
- از ASP.NET Core Identity یا `IPasswordHasher<TUser>` برای رمزها استفاده کن؛ SHA-256 خام بدون salt/work factor برای رمز عبور قابل قبول نیست.
- کوکی احراز هویت: HttpOnly، Secure در محیط Production، SameSite مناسب و عمر محدود.
- ضد CSRF، XSS، SQL Injection، SSRF، Path Traversal، Mass Assignment و افشای خطا/لاگ بررسی شود.
- dependency scan و بررسی مجوز کتابخانه‌ها پیش از Release انجام شود.

## تست و مستندات
- Unit test برای قواعد مستقل؛ Integration test برای EF/SQL/Auth/Middleware؛ E2E فقط برای مسیرهای حیاتی.
- تست رگرسیون برای هر bug؛ تست باید رفتار را بسنجد نه جزئیات خصوصی پیاده‌سازی.
- OpenAPI/Swagger فقط در صورت وجود HTTP API مناسب؛ Razor Pages به‌تنهایی نیازمند Swagger نیست.
- Markdown برای راه‌اندازی، تصمیم‌ها، معماری، وضعیت و گزارش Agent؛ XML docs برای APIهای C#؛ JSDoc برای JavaScript؛ JavaDoc فقط برای Java.
- SVG قالب تصویر است، نه قالب مستندسازی. SVG ورودی غیرقابل اعتماد را بدون اعتبارسنجی/پاک‌سازی وارد DOM نکن.
- CI باید Restore/Build/Test و بررسی دارایی محلی را اجرا کند؛ وضعیت سبز CI جای بازبینی انسانی و تست محیطی را نمی‌گیرد.
