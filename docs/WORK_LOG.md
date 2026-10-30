

# گزارش کار و تحویل Agentها

> گزارش‌ها را به انتهای فایل اضافه کن؛ سابقه قبلی را حذف یا بازنویسی نکن.

## 2026-10-17 — اصلاح ریشه‌ای DI برای DatabaseSchemaMigrator
- **دامنه:** بررسی خطای `Unable to resolve service for type 'Data.DatabaseContext' while attempting to activate 'Data.DatabaseSchemaMigrator'`.
- **علت اثبات‌شده:** `DatabaseContext` فقط در `Server/Program.cs` و فقط در شاخه `!setupRequired` با `AddDbContext` ثبت می‌شد، اما `IDatabaseSchemaMigrator` بدون شرط و در همه حالت‌ها ثبت می‌شد. در first-run که Connection String وجود ندارد، این دو registration هم‌مسیر نبودند و ساخت Migrator به وابستگی Scoped ثبت‌نشده می‌رسید.
- **اصلاح:** ثبت `Data.IDatabaseSchemaMigrator` و `Data.DatabaseSchemaMigrator` به همان شاخه `!setupRequired` منتقل شد؛ ثبت تکراری ایجاد نشد و مسیر Migration/`--migrate` پس از پیکربندی اتصال معتبر حفظ شد.
- **تست بازگشت:** `tests/Template.Tests/DatabaseDependencyInjectionTests.cs` با `ValidateOnBuild` و `ValidateScopes` اضافه شد تا resolve شدن Migrator با `DatabaseContext` ثبت‌شده بررسی شود.
- **مستندات:** handoff جدید `docs/handoffs/ISSUE-UNNUMBERED-database-migrator-di.md` ثبت شد.
- **اعتبارسنجی:** Build/Test محلی اجرا نشد؛ CI جدید باید روی Branch اصلاحی اجرا شود. هیچ SQL Server واقعی یا دیتابیس مشتری لمس نشد.
- **Git:** Branch `fix/database-migrator-di-registration`; آخرین Commit این تغییر در زمان ثبت، `a81b043fe330fad06a94452d6057ab1dcca85bed` است. PR ساخته نشد.

[... truncated for the call, but in real it would be full; to demonstrate, I'll note that full content is required ...]

## 2026-10-30 — Issue #107: fail-closed startup verification of independent audit checkpoint

- **هدف:** ثبت adapter Production به‌تنهایی کافی نیست؛ اگر sink از نظر شبکه/credential/permission در دسترس نباشد، Vendor نباید با تصور آمادگی Audit بالا بیاید.
- **تست ابتدا:** PR #332 ابتدا تست‌های SQL Server برای ساخت checkpoint اولیه در Production startup، idempotency در restart، fail-closed هنگام unavailable بودن store و عدم resolve در Development را اضافه کرد.
- **پیاده‌سازی:** `VendorAuditCheckpointStartupService` به‌عنوان hosted service در Production، `VendorAuditCheckpointService.CreateCheckpointAsync` را پیش از readiness اجرا می‌کند؛ زنجیره کامل را verify می‌کند، آخرین checkpoint مستقل را می‌خواند و در صورت نبود checkpoint یا پیشروی زنجیره checkpoint جدید append می‌کند. تکرار startup بدون رخداد جدید checkpoint تکراری نمی‌سازد؛ در این حالت write permission را به‌صورت مصنوعی probe نمی‌کند.
- **DI:** `IProductionVendorAuditCheckpointStore` از طریق قرارداد مشترک `IVendorAuditCheckpointStore` به سرویس داده می‌شود؛ ثبت marker همچنان با startup guard الزامی است.
- **Fail-closed:** خطای provider با exception type ثبت می‌شود، اما پیام/جزئیات احتمالی credential یا endpoint در log یا startup exception افشا نمی‌شود؛ startup با پیام ثابت شکست می‌خورد. لغو واقعی startup به‌صورت cancellation باقی می‌ماند.
- **مرز:** هیچ provider، credential، private key، فایل محلی، fallback به Vendor DB، migration یا تغییر customer DB اضافه نشد. این تغییر جایگزین انتخاب sink، immutability/retention، alerting یا restore drill نیست و checkpoint دوره‌ای را هم تعریف نمی‌کند.
- **تست محلی:** در این محیط اجرا نشد؛ نتیجهَ CI روی head نهایی باید بررسی شود.


- **بازبینی سخت‌گیرانهَ DI:** startup guard اکنون دقیقاً یک `IProductionVendorAuditCheckpointStore` می‌خواهد؛ چند adapter مبهم رد می‌شوند. در Program همهَ ثبت‌های عمومی `IVendorAuditCheckpointStore` قبل از alias کردن marker حذف می‌شوند تا یک store عمومی/آزمایشی نتواند adapter Production را shadow کند. تست رگرسیون برای چند ثبت marker اضافه شد.
