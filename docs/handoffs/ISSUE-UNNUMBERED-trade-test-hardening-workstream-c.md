# تحویل وظیفه — ISSUE-UNNUMBERED-trade-test-hardening-workstream-c

- **Issue:** `UNNUMBERED` (Workstream C: تست‌های کم‌حجم و پرارزش برای رفتار موجود)
- **Agent/مسئول:** Claude (Trade)
- **تاریخ:** 2026-11-22
- **وضعیت:** انجام‌شده برای شکاف‌های قابل‌اجرا؛ بقیه یا از قبل پوشش داده شده یا مسدود است

## ۱) وضعیت PRهای مرتبط هنگام شروع
`main@5484758`؛ #749، #750، #751، #752 همه باز و merge‌نشده‌اند. تست‌های Update محصول (۷ مورد) فقط در #752 است و در اینجا تکرار نشد؛ تست‌های Update طرف (Party) فقط در #750 است.

## ۲) جدول پوشش (بازبینی دوبارهٔ ماتریس #749 با کد و تست فعلی)
| حوزه | رفتار موجود | تست موجود | شکاف | اقدام |
|---|---|---|---|---|
| ایزولاسیون Company در HTTP | `OtherTenant{Party,Product}IsInvisible...Empty404` (GET/PUT شرکت دیگر، حذف‌شده، ناموجود → ۴۰۴ یکسان و بدنهٔ خالی، دادهٔ بیگانه بدون تغییر)؛ `Duplicate...FreeInOtherTenants` | کامل (وابسته به SQL Server) | ندارد | بدون تغییر |
| ایزولاسیون در سرویس | سرویس‌ها `context.TenantId` را به store می‌دهند | Product: فقط `Get` شرکت دیگر؛ Party: هیچ | Update شرکت دیگر، کد یکسان در دو شرکت، عدم تغییر پس از تعارض | **P0/P1 — اضافه شد** |
| ReadOnly + DenyAll | `ClosedPeriodWriteGets409ReadOnly` روی `TradeProbeFactory` اجرا می‌شود که entitlement تولیدی DenyAll را نگه می‌دارد؛ پس «ReadOnly + DenyAll ← `409 trade_period_read_only`» همین حالا اثبات شده است. ترتیب Gate هم تست دارد | کامل | سطر «ناموجود» ماتریس #749 اشتباه بود (فقط روی endpoint probe؛ endpoint تولیدی Trade فقط Party/Product/capabilities است) | بدون تغییر؛ اصلاح ماتریس در بند ۶ |
| Party/Product و دورهٔ مالی | PeriodIndependent (master data سطح Tenant)؛ نوشتن در دورهٔ بسته مجاز است ولی مجوز و DenyAll همچنان اعمال می‌شود | `ClosedPeriod{Party,Product}Writes*` (۳ مورد هر کدام) | ندارد | بدون تغییر |
| DenyAll Fail-Closed | `ProductionDenyAll...403`، `PartyRoutesAre403ModuleUnavailable...`، `ProductRoutesAre403...EvenInAClosedPeriod`، capabilities | کامل | ندارد | بدون تغییر |
| Permission | `MissingPermissionGets403...`، `UpdateOrCreateWithoutView...`، `UpdateWithViewAndUpdatePermissionsSucceeds`، `TradeEndpointGateTests` | کامل | ندارد | بدون تغییر |
| قرارداد HTTP | Anti-forgery ۴۰۰، ۴۱۵، ۴۱۳، ۴۰۰ برای بدنهٔ خالی، خطای اعتبارسنجی بدون echo، بدون مسیر حذف/فعال‌سازی | کامل | ندارد | بدون تغییر |
| Atomicity و RowVersion | `Update_*Stale*`، `NoOpStillComparesRowVersion`، تست‌های #750/#752 برای دادهٔ ذخیره‌شده | خوب | عدم تغییر رکورد و token پس از تعارض کد تکراری | **P1 — اضافه شد** |
| همزمانی SQL | `TwoRacingCreatesOfTheSameCode...`، `UpdateEnforcesRowVersion...` (HTTP) و `TradeProductStoreSqlServerTests` | موجود (SQL) | رقابت Update همزمان و Party store مستقل | P2؛ نیازمند SQL Server که اینجا نبود، نوشته نشد |
| نشت پیام استثنا از endpointهای Trade | زیرساخت مشترک `SafeException*Tests` | غیر Trade-specific | تست با store خطادار روی Party/Product | P2؛ نیازمند تزریق store معیوب در DI؛ ثبت شد |

## ۳) تست‌های اضافه‌شده (`TradeMasterDataCompanyIsolationServiceTests`، ۶ مورد)
- `Party_UpdateAndGetOfAnotherCompanysRecord_AreNotFoundAndLeaveItUnchanged`
- `Product_UpdateOfAnotherCompanysRecord_IsNotFoundAndLeavesItUnchanged`
- `Party_SameCodeInTwoCompaniesIsAllowedWhileTheSameCompanyStillConflicts`
- `Product_SameCodeInTwoCompaniesIsAllowedWhileTheSameCompanyStillConflicts`
- `Party_UpdateDuplicateCodeConflict_LeavesStoredRecordAndTokenUnchanged`
- `Product_UpdateDuplicateCodeConflict_LeavesStoredRecordAndTokenUnchanged`

store در این تست‌ها حافظه‌ای است و فقط ثابت می‌کند سرویس Company بافت را به هر فراخوانی می‌دهد و رکورد دیگر را لمس نمی‌کند؛ ایزولاسیون واقعی SQL/HTTP همان تست‌های بالاست. فایل‌های `TradePartyServiceTests` و `TradeProductServiceTests` عمداً دست نخوردند تا با #750/#752 تداخل نکنند.

### بررسی جهش (روی کپی محلی، بلافاصله بازگردانده شد)
| جهش در `ProductService` | نتیجه |
|---|---|
| `FindForUpdateAsync(context.TenantId…)` ← `Guid.Empty` | ۲ تست ناموفق (تست شکست می‌خورد) |
| pre-check تکراری در Create بدون Company (`Guid.Empty`) | ۱ تست ناموفق |
جهش روی `PartyService` اجرا نشد (منطق متقارن؛ فقط Product). پس از بازگردانی `git status` فقط فایل تست جدید را نشان می‌دهد.

## ۴) بررسی و آزمون
| فرمان | وضعیت | نتیجه |
|---|---|---|
| `dotnet build Template.sln --no-restore -c Release` | اجرا شد | موفق، 0 Error |
| `dotnet test --project tests/Template.Tests --no-build -c Release --filter-class "*TradeMasterDataCompanyIsolationServiceTests*"` | اجرا شد | 6 اجرا، 6 موفق |
| تست کامل `Template.Tests` (محلی، بدون SQL Server) | اجرا شد | total 1579، موفق 1440، **ناموفق 2**، رد‌شده 137 |
| `python3 scripts/check_static_assets.py` | اجرا شد | PASS |
| `git diff --check` | اجرا شد | موفق |
دو شکست محلی `SafeExceptionRealApplicationTests.ApplicationPipeline_DoesNotExposeMiddlewareFailure` (محیط Development) از قبل روی `main` هم هست و ربطی به این PR ندارد؛ در CI سایر PRها (مثلاً #752) سبز است. ۱۳۷ تست محلی رد شد چون SQL Server محلی نبود؛ آن‌ها **پاس گزارش نمی‌شوند**.

## ۵) SQL Server
- محلی: در دسترس نبود (Docker daemon وجود ندارد؛ `FARANOU_TEST_SQLSERVER` تنظیم نشده) ← اجرا نشد.
- CI (`FARANOU_REQUIRE_SQLSERVER=1` با سرویس `mssql:2022`) برای head #752 (`00e4ca2`): total 1747، ناموفق 0، موفق 1739، رد‌شده 8؛ رد‌شده‌های دیده‌شده همگی `WindowsCng*` است و هیچ تست Trade رد نشده. نتیجهٔ CI برای این شاخه پس از push جدا ثبت می‌شود.

## ۶) اصلاح ماتریس #749
سطر «ReadOnly + DenyAll → 409 (HTTP)» ناموجود نیست؛ پوشش آن `ClosedPeriodWriteGets409ReadOnly` است. سطرهای «ناموجود» مربوط به ImportCase/Proforma/OrderRegistration همچنان آینده/مسدود است و با این PR تکمیل‌شده علامت نخورد.

## ۷) شکاف‌ها و تصمیم‌ها
- P2: تست رقابتی Update همزمان روی SQL Server؛ تست نشت پیام استثنا برای Party/Product با store معیوب.
- مسدود تا پیاده‌سازی عملیاتی: تست‌های ImportCase/Proforma/OrderRegistration، مجوز `order-registration`، شمارهٔ سند رقابتی.
- تصمیم مالک: سیاست ZWSP/WJ، نرمال‌سازی کد، ZWNJ در `TradeDocumentNumber` (ر.ک. handoffهای #750/#752).
- گام بعدی پیشنهادی: بستن S3-0 (G0، G4، G5، G6)، سپس PR-1 طبق #749.

کد تولیدی، Schema، Migration، Vendor و Licensing تغییر نکرد؛ Trade fail-closed مانده است.
