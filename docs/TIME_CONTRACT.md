# قرارداد زمان (UTC و ساعت محلی سرور)

- **وضعیت:** Frontend `Implemented`؛ اصلاح سمت Backend `Proposed` (D-067، `NeedsApproval`)
- **Issue:** #444 (ممیزی زمان) — هماهنگی Backend در Issue جدا (پایین)
- **تاریخ ممیزی:** 2026-11-03، بر اساس کد `main`

## ۱. یافته‌های ممیزی (شواهد کد)

| گروه | فیلدها | چگونه نوشته می‌شود | معنا | شکل JSON در Production |
|---|---|---|---|---|
| **UTC واقعی** | همهٔ فیلدهای `*Utc`: `OccurredAtUtc` (رویداد امنیتی و سوابق ورود/خروج)، `CreatedAtUtc` (عضویت و گروه شرکتی)، `RequestedAtUtc`، `DecidedAtUtc`، `ImportedAtUtc`، `ProvisionedAtUtc`، … | `DateTime.UtcNow` | UTC | **بدون `Z`** (EF هنگام خواندن از `datetime2` مقدار را `Kind=Unspecified` می‌سازد) |
| **ساعت محلی سرور (قدیمی)** | `Entity.InsertDateTime`، همهٔ `UpdateDateTime`، `User.LastLoginDateTime` | `Dtat.Utility.Now` / `Domain.Seedwork.Utility.Now` = `DateTime.Now` | زمان دیواری سرور، **نه UTC** | بدون `Z`، همان ساعت دیواری |
| تاریخ تقویمی | `StartDate`/`EndDate` دورهٔ مالی | `DateOnly`/تاریخ | بدون زمان و منطقه | `yyyy-MM-dd` |

شواهد: `Domain/SeedWork/Utility.cs` (`DateTime.Now`، `UtcNow` فقط کامنت)، `Dtat/Utility.cs`، `Server/Infrastructure/Security/AuthenticationService.cs:286` (`LastLoginDateTime = Dtat.Utility.Now`) و `:354` (`OccurredAtUtc = DateTime.UtcNow`)؛ نبود هرگونه مبدل UTC/`SpecifyKind` برای مدل‌های EF و `ConfigureHttpJsonOptions`. تست مشخصه‌ساز: `tests/Template.Tests/Integration/UtcWireFormatSqlServerTests.cs` (روی SQL Server در CI).

**نتیجه:** یک مبدل سراسری «زمان بدون Z یعنی UTC» برای گروه دوم **غلط** است و ساعت‌ها جابه‌جا می‌کند. اصلاح باید فیلد به فیلد باشد.

## ۲. قرارداد Frontend (پیاده‌سازی‌شده در این PR)
1. `*Utc` ← UTC. مقدار بدون offset با `asUtcInstant` (`lib/time.ts`) `Z` می‌گیرد؛ مقدار دارای `Z`/offset **دوباره تبدیل نمی‌شود**.
2. فیلدهای قدیمی (`insertDateTime`، `updateDateTime`، `lastLoginDateTime`) همان‌طور که سرور نوشته نمایش داده می‌شوند (`readServerLocalDateTime`)؛ هرگز `Z` نمی‌گیرند. اگر منطقهٔ مرورگر با سرور فرق داشته باشد، این سه فیلد همان ساعت دیواری سرور را نشان می‌دهند (محدودیت شناخته‌شده؛ رفع آن با Backend).
3. نمایش بر اساس منطقهٔ زمانی کاربر (مرورگر). خود مقدار UTC هرگز در کلاینت دوباره‌نویسی نمی‌شود.
4. فیلتر روز: ابتدا و انتهای روز تقویمی در منطقهٔ مرورگر محاسبه و به UTC تبدیل می‌شود (`dayStartUtc`/`dayEndUtc`)؛ ورودی نامعتبر `null` است (نه استثنا).
5. تاریخ‌های صرفاً تقویمی (دورهٔ مالی) هیچ‌وقت UTC فرض نمی‌شوند.
6. هر فیلد زمانیِ جدید باید در جدول بند ۱ طبقه‌بندی و با reader مناسب خوانده شود؛ `readIsoDateTime` مستقیم استفاده نشود.

آزمون‌ها: `lib/time.test.ts` (UTC، ایران، آلمان، DST بهار و پاییز، مرز نیمه‌شب، رفت‌وبرگشت JSON، نمایش در سه منطقه)، `api/timeContract.test.ts` (هر گروه فیلد).

## ۳. قرارداد Backend (پیشنهاد، مسئول: ChatGPT؛ D-067)
1. **UTC واقعی:** ستون‌ها/ویژگی‌های `*Utc` با مبدل مقدار EF (فقط تبدیل در خواندن/نوشتن، **بدون Migration**) `Kind=Utc` بگیرند تا JSON با `Z` برود. سپس تست مشخصه‌ساز بالا باید `Z` را انتظار داشته باشد و `asUtcInstant` بی‌اثر می‌شود (بی‌خطر).
2. **قدیمی:** تبدیل `InsertDateTime`/`UpdateDateTime`/`LastLoginDateTime` به UTC نیازمند تصمیم مالک و **Migration داده** (تعیین منطقهٔ زمانی تاریخی سرور) است؛ تا آن زمان تغییری نکند. تا قبل از تأیید، هیچ مبدل سراسری اجرا نشود.
3. قراردادهای جدید API فقط `DateTimeOffset` یا فیلد `*Utc` با `Z` بدهند.

## اصلاح projection عضویت (Issue #520)

مبدل EF برای ویژگی‌های نام‌گذاری‌شده با پسوند `Utc` پیش‌تر در مدل وجود دارد (Issue #500)، اما خروجی projectionهای عضویت نیز پس از materialization در سه endpoint جزئیات کاربر مدیر نصب، دفترچه کاربران شرکت و اعضای نقش، صریحاً با `DateTime.SpecifyKind(..., DateTimeKind.Utc)` برچسب‌گذاری می‌شود. مقدار tick تغییر نمی‌کند. این اصلاح فقط برای زمان عضویت است؛ فیلدهای قدیمی ساعت محلی و قرارداد دسترسی تغییر نمی‌کنند. تأیید SQL Server منوط به اجرای تست integration است.
