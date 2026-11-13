# تحویل وظیفه — ISSUE-UNNUMBERED-trade-s2-4-product-backend

- **Issue:** `UNNUMBERED`
- **Agent/مسئول:** backend-developer (Claude Code)
- **تاریخ:** 2026-11-13
- **وضعیت:** انجام‌شده (بک‌اند؛ ClientApp خارج از محدوده)

## هدف و معیارهای پذیرش
- [x] `GET/POST /api/v1/trade/products`، `GET/PUT /api/v1/trade/products/{id}` با الگوی Party
- [x] `GET /api/v1/trade/reference/units` فقط‌خواندنی با مجوز `trade.form.product.view`
- [x] POST/PUT نیازمند `view` و مستقل از دورهٔ مالی؛ بدون حذف/غیرفعال‌سازی
- [x] بدون Migration، تغییر Schema، جدول جدید یا اتصال به Inventory
- [x] تست واحد، Integration روی SQL Server و معماری

## خلاصه تغییرات
- لایه Application: `ProductDtos`، `IProductStore`، `ProductService` (اعتبارسنجی، نگاشت، بدون echo مقدار).
- Infrastructure: `ProductStore` با فیلتر صریح `TenantId` و `!IsDeleted`، تبدیل خطای ایندکس یکتا و `RowVersion` به نتیجهٔ دامنه. شناسه‌های کالا Include/ذخیره نمی‌شوند.
- Server: `TradeProductEndpoints`، `/reference/units`، ثبت DI. خواندن بدنهٔ JSON (سقف 16 KB) از `TradePartyEndpoints` به `TradeJsonBody` مشترک منتقل شد (رفتار Party بدون تغییر).

## فایل‌های مهم تغییرکرده
- `Modules/Trade/Trade.Application/Products/{ProductDtos,IProductStore,ProductService}.cs` — جدید
- `Modules/Trade/Trade.Infrastructure/Products/ProductStore.cs` — جدید
- `Modules/Trade/Trade.Infrastructure/Persistence/TradePersistenceServiceCollectionExtensions.cs` — ثبت `IProductStore`
- `Server/Api/Trade/{TradeProductEndpoints,TradeJsonBody}.cs` — جدید؛ `TradePartyEndpoints.cs`، `TradeReferenceEndpoints.cs`، `TradeEndpoints.cs`، `Server/Program.cs` — اتصال
- `tests/Template.Tests/TradeProductServiceTests.cs`، `Integration/TradeProductHttpTests.cs`، `Architecture/TradeProductEndpointArchitectureTests.cs` — جدید؛ `Architecture/TradePartyEndpointArchitectureTests.cs` — آزمون «فقط Party مستقل از دوره است» به Party و Product تعمیم یافت
- `docs/TRADE_MODULE.md`، `docs/SECURITY.md` — به‌روزرسانی حداقلی

## تصمیم‌های فنی
- کد کالا مثل Party در PUT هم دریافت و قابل تغییر است (با بررسی تکراری)، نه فقط در ایجاد.
- `hsCode` به نویسه‌های لاتین قابل‌چاپ محدود شد چون ستون `varchar` است و نویسهٔ غیر ASCII بی‌صدا به `?` تبدیل می‌شد.
- فیلتر `productGroupCode` برابری دقیق (بر اساس collation دیتابیس) است.
- راه‌حل Id در `PartyStore` (تبدیل نقش‌های Modified به Added) برای Product لازم نیست: فرزندی ذخیره نمی‌شود.
- تصمیم باز: D-084 فقط Party را نام می‌برد؛ مستقل‌بودن Product از دوره به ADR جدید (پیشنهاد D-085) و تأیید مالک نیاز دارد.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `dotnet build Template.sln --configuration Release` | موفق | 0 خطا |
| `dotnet test Template.sln --no-build -c Release --filter Trade` روی SQL Server واقعی (`FARANOU_REQUIRE_SQLSERVER=1`) ×3 | موفق | هر سه بار 221 از 221، 0 skip، 0 fail |
| `python3 scripts/check_static_assets.py` | اجرا نشد | تغییر وب‌استاتیک نبود |
| آزمون کامل غیر Trade / ClientApp | اجرا نشد | خارج از محدودهٔ این برش |

## امنیت، داده و سازگاری
- Tenant فقط از Context سروری؛ `tenantId`/`isActive` ارسالی نادیده گرفته می‌شود (آزمون شده). 404 یکسان و خالی برای ناموجود/شرکت دیگر/حذف‌شده. 403 یکسان برای PUT/POST بدون `view`.
- بدون Migration و Schema؛ جداول `ProductIdentifiers` همچنان خارج از Migration.

## ریسک‌ها و کارهای باقی‌مانده
- ADR گسترش D-084 به Product؛ UI کالا (frontend)؛ D-083 (حذف/غیرفعال‌سازی) `Proposed`.
- بازبینی مستقل `code-reviewer`/`security-reviewer` توسط Lead انجام می‌شود.

## شناسه‌های Git
- Branch: feat/trade-s2-4-product-slice
- Commit: ندارد (Commit نشد)
- Pull Request: ندارد
