# تحویل وظیفه — ISSUE-403-trade-access-gate

- **Issue:** [#403](https://github.com/hamidreza1394/Accounting/issues/403) (فرزند #401؛ S1)
- **Agent/مسئول:** Claude (Lead) با Subagentهای `architect`، `backend-developer`، `security-reviewer`، `code-reviewer`
- **تاریخ:** 2026-10-31
- **وضعیت:** ناقص — بخش S1-a انجام شد؛ اتصال به envelope امضاشده Blocked است

## هدف و معیارهای پذیرش
- [x] دروازه با ترتیب نشست ← عضویت ← WorkContext/دوره ← entitlement ← مجوز ← use case.
- [x] پیاده‌سازی پیش‌فرض fail-closed `ITradeModuleEntitlement` در همه محیط‌ها؛ بدون stub مجازکننده در Server.
- [x] فیلتر route برای `/api/v1/trade/**`؛ endpoint بدون متادیتای صریح رد می‌شود.
- [x] `GET /api/v1/trade/capabilities` بدون افشای جزئیات لایسنس/امضا/Tenant.
- [x] تست منفی: بدون عضویت/دوره بیگانه/کوکی دست‌کاری‌شده/دوره بسته/بدون entitlement/بدون مجوز/بدون Anti-forgery.
- [ ] استخراج entitlement `TRADE_IMPORT` از LicenseEnvelope (Blocked: #390 قرارداد entitlement شرکتی؛ #384/#391 حالت لایسنس runtime).
- [x] Seed کاتالوگ مجوز: در PR #402 ادغام شده است (این PR آن را تغییر نمی‌دهد).
- [ ] ثبت DI/Migration `TradeDbContext` (S2).

## خلاصه تغییرات
- `ITradeAuthorization` به `ResolveCurrentContextAsync` و `HasPermissionAsync(TradeAccessContext, …)` شکسته شد؛ context فقط با `ReferenceEquals` همان درخواست (کلید خصوصی `HttpContext.Items`) پذیرفته می‌شود.
- `TradeAccessGate`، `DenyAllTradeModuleEntitlement`، `TradeAccessFilter`، `TradeOperationMetadata`، `GET /capabilities`.
- `CompanyAccessService.GetGrantedPermissionCodesAsync` (همان محمول عضویت فعال؛ پیشوند ثابت `trade.`).
- فیلتر، Anti-forgery را بر اساس Verb HTTP نیز اعمال می‌کند و Verb تغییردهنده با Intent خواندن را رد می‌کند.

## فایل‌های مهم تغییرکرده
- `Modules/Trade/Trade.Application/Authorization/*` (ITradeAuthorization، TradeAccessContext/Decision/Gate/Outcome، TradeCapabilities)
- `Server/Infrastructure/Trade/{TradeAuthorizationAdapter,DenyAllTradeModuleEntitlement}.cs`، `Server/Infrastructure/Tenancy/CompanyAccessService.cs`
- `Server/Api/Trade/*`، `Server/Api/ApiEndpoints.cs`، `Server/Program.cs`
- تست‌ها: `TradeAccessGateTests`، `TradeAuthorizationAdapterTests` (مهاجرت‌شده، بدون تضعیف)، `DenyAllTradeModuleEntitlementTests`، `Architecture/TradeEndpointGateTests`، `Integration/TradeAccessHttpTests`
- مستندات: `TRADE_MODULE.md`، `ROADMAP.md`، `PROJECT_STATUS.md`، `WORK_LOG.md`

## تصمیم‌های فنی
- تصمیم معماری جدیدی گرفته نشد؛ اجرای D-061/D-062/D-063.
- **انحراف محافظه‌کارانه از D-063 بند ۵:** هیچ stub توسعه‌ای مجازکننده در Server نیست؛ تا ادغام #390 ماژول Trade در Development هم بسته است. fake مجازکننده فقط زیر `tests/` است. نیازمند آگاهی/تأیید مالک.
- `HasPermissionForAllCompanyPeriodsAsync` entitlement-aware نیست؛ از هیچ endpoint در دسترس نیست و تست معماری فقط Gate و Adapter را مجاز به وابستگی به `ITradeAuthorization` می‌داند. گزارش چندشرکتی باید در Issue گزارش‌ها entitlement تک‌تک شرکت‌ها را all-or-nothing بسنجد.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `dotnet restore Template.sln` | موفق | SDK 10.0.403 |
| `dotnet build Template.sln --no-restore -c Release` | موفق | 0 خطا؛ هشدارهای قبلی مخزن؛ هشدار جدید در فایل‌های Trade دیده نشد |
| `dotnet test Template.sln --no-build -c Release` | موفق | پس از Rebase روی main (شامل PR #402): 783 کل، 737 موفق، 46 skipped (Windows CNG/SQL Server/مجوز فایل)، 0 ناموفق |
| `git diff --check` | موفق | بدون خروجی |
| تست SQL Server | اجرا نشد | `FARANOU_TEST_SQLSERVER` تنظیم نبود؛ ترجمه `StartsWith` و collation روی SQL Server تأیید نشده |
| CI | اجرا نشد | تا ایجاد PR |

## بازبینی
- `security-reviewer`: بدون یافته Critical/High/Medium؛ 4 مورد Low. L1 (وابستگی مستقیم به port) و L3 (پوشش marker) با تست معماری و L2 (Anti-forgery بر پایه Verb) با اصلاح فیلتر + تست رفع شد؛ L4 (تست SQL Server و افشای وضعیت لایسنس به اعضای همان شرکت) ثبت شد و باز است.
- `code-reviewer`: بدون یافته High؛ ضدجعل بر پایه Verb و کلید خصوصی `Items` اصلاح شد؛ نیت‌های کیفی/سبک (انتقال ثابت‌ها، تست reflection اضافی) باز ماند.
- `architect` طرح را داد؛ بازبینی نهایی architect روی Diff انجام نشد.
- مصرف توکن (گزارش ابزار): architect 77785، backend-developer 122506، security-reviewer 83287، code-reviewer 76930. تقسیم کار صرفه داشت: یافته‌های واقعی امنیتی/تست پیش از PR رفع شد.

## امنیت، داده و سازگاری
- هیچ Migration، Seed یا تغییر دیتابیس نیست؛ هیچ بسته جدیدی اضافه نشد.
- ماژول Trade تا ادغام #390 در همه محیط‌ها با `trade_module_unavailable` بسته است.

## ریسک‌ها و کارهای باقی‌مانده
- #390، #384/#391، resolver مبتنی بر envelope، تست SQL Server و CI، قفل entitlement برای گزارش چندشرکتی، تعارض احتمالی docs با PR #402 (هر دو به `PROJECT_STATUS`/`TRADE_MODULE`/`WORK_LOG` می‌افزایند).
