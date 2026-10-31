# تحویل وظیفه — ISSUE-397-trade-module-boundary

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/397
- **Agent/مسئول:** ChatGPT / GitHub engineering workflow
- **تاریخ:** 2026-10-31
- **وضعیت:** Foundation اصلاح‌شده طبق D-062؛ نیازمند CI و ادامه کار

## هدف و معیارهای پذیرش
- [x] ثبت D-061 و ساختار مستقل Trade Domain/Application/Infrastructure.
- [x] تعریف Tenant boundary براساس Company.Id و عدم وابستگی Trade به مدل‌های حسابداری.
- [x] تعریف قراردادهای authorization و entitlement؛ دوره مستقل Trade طبق D-062 حذف شد و از FiscalPeriod مشترک استفاده می‌شود.
- [x] تعریف قرارداد گزارش چندشرکتی all-or-nothing.
- [ ] اتصال adapterهای Host به CompanyAccessService و License entitlement.
- [ ] پیاده‌سازی اعتبارسنجی دوره مالی مشترک و مجوزهای تخصصی فرم/عملیات در API و UI و تست‌های منفی cross-company.
- [ ] پیاده‌سازی گزارش‌های شرکت و چندشرکتی.
- [ ] Migration مستقل Trade پس از review و تأیید جداگانه.

## خلاصه تغییرات
- ایجاد پروژه‌های Trade.Domain، Trade.Application و Trade.Infrastructure.
- ایجاد TradeDbContext با default schema برابر trade؛ بدون DbSet یا جدول دوره مستقل.
- تعریف ITradeAuthorization و ITradeModuleEntitlement به‌عنوان portهای host-provided.
- افزودن تست‌های دامنه، مدل EF و guard جهت وابستگی پروژه‌ها.
- ثبت D-061 و هم‌ترازی مستندات معماری/نقشه راه/ردیابی.

## فایل‌های مهم تغییرکرده
- Modules/Trade/** — foundation ماژول.
- tests/Template.Tests/TradeModuleFoundationTests.cs — تست دامنه، مدل و معماری.
- Template.sln و tests/Template.Tests/Template.Tests.csproj — اتصال پروژه‌ها به build/test.
- docs/DECISIONS.md, docs/TRADE_MODULE.md, docs/ARCHITECTURE.md, docs/ROADMAP.md, docs/INDEX.md, docs/REQUIREMENTS_TRACEABILITY.md, docs/WORK_LOG.md — تصمیم و ردیابی.
- docs/handoffs/ISSUE-397-trade-module-boundary.md — این گزارش.

## تصمیم‌های فنی
- D-062: شرکت/دوره و سیاست انتخاب دوره از WorkContext/FiscalPeriodSelectionPolicy مشترک؛ مجوزهای فرم و عملیات Trade در همان سازوکار PermissionDefinitions/CompanyRolePermissions تعریف می‌شوند.
- رکوردهای Trade دوره‌محور در آینده TenantId و FiscalPeriodId را به‌صورت ارجاع منطقی نگه می‌دارند؛ Host تعلق دوره به شرکت را بررسی می‌کند.
- فاز اول Modular Monolith و یک دیتابیس فیزیکی؛ schema و DbContext مستقل.
- Trade.Domain/Application به Core Accounting وابسته نیست.
- Global Query Filter استفاده نشده؛ predicate صریح TenantId الزامی است.
- هیچ migration یا تغییر schema روی دیتابیس مشتری انجام نشده است.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| dotnet restore Template.sln | اجرا نشده | محیط این مرحله دسترسی اجرای .NET محلی ندارد |
| dotnet build Template.sln --no-restore --configuration Release | اجرا نشده | باید در CI بررسی شود |
| dotnet test Template.sln --no-build --configuration Release | اجرا نشده | باید در CI بررسی شود |
| git diff --check | اجرا نشده | تغییرات از طریق GitHub API ثبت می‌شوند |

## امنیت، داده و سازگاری
- شناسه شرکت در Trade به Company.Id اشاره منطقی دارد؛ FK به جداول حسابداری ایجاد نشده است.
- authorization و entitlement فعلاً قرارداد هستند؛ runtime adapter هنوز پیاده‌سازی نشده.
- هیچ migration ایجاد/اجرا نشده؛ بنابراین دیتابیس مشتری تغییر نکرده است.

## ریسک‌ها و کارهای باقی‌مانده
- اتصال واقعی به Host و اعتبارسنجی membership/permission باید پیش از هر endpoint تجاری تکمیل شود.
- Host adapter هنوز باید تعلق FiscalPeriodId به TenantId و وضعیت انتخاب‌پذیری دوره را بررسی کند؛ مجوزهای تخصصی Trade هنوز در runtime enforce نشده‌اند.
- گزارش چندشرکتی و audit scope هنوز use case اجرایی ندارند.
- migration history جداگانه و tooling طراحی EF برای Trade باید پیش از اولین migration تثبیت شود.
- CI روی PR باید build، unit و model tests را اجرا کند.

## شناسه‌های Git
- Branch: feat/issue-397-trade-module-boundary
- Commit: در PR ثبت می‌شود.
- Pull Request: پس از CI ایجاد/ثبت می‌شود.
