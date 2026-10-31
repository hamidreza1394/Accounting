# تحویل وظیفه — ISSUE-399-trade-s0-alignment

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/399
- **تاریخ:** 2026-10-31
- **وضعیت:** مستندات و ADR روی شاخه کاری ثبت شده؛ CI/PR هنوز انجام نشده است.

## تصمیم‌های تأییدشده مالک محصول
- ADR-B گزینه A: یک Company مالک هر Voyage/Bill of Lading؛ سایر شرکت‌ها فقط allocation/share line می‌گیرند.
- Period policy = selected: پرونده ImportCase می‌تواند از دوره‌های مختلف عبور کند؛ هر سند/عملیات تراکنشی FiscalPeriodId دوره واقعی همان عملیات را نگه می‌دارد. انتقال خودکار اسناد بین دوره‌ها ممنوع است.
- گزارش عملیاتی چندشرکتی از موج اول با all-or-nothing authorization و audit scope.
- entitlement مستقل TRADE_IMPORT از S1 و پیش از استفاده از قابلیت‌های Trade؛ Production fail-closed.

## فایل‌های تغییرکرده در این شاخه
- docs/DECISIONS.md — ثبت D-063.
- docs/TRADE_MODULE.md — اصلاح وضعیت foundation و سیاست‌های دوره، گزارش و لایسنس.
- docs/PROJECT_STATUS.md — اصلاح وضعیت PR #398 و شکاف‌های باقی‌مانده.
- docs/ROADMAP.md — حذف مسیر اجرایی متعارض و ثبت S0 تا S7.
- docs/ARCHITECTURE.md — ثبت سیاست‌های مالکیت، دوره، گزارش و entitlement.
- docs/REQUIREMENTS_TRACEABILITY.md — هم‌ترازی وضعیت foundation و ردیابی D-063.
- docs/INDEX.md — ثبت D-063 و Issue #399.
- docs/WORK_LOG.md — ثبت شواهد و محدودیت‌های S0.

## ممیزی read-only بنیاد در main
- مبنای main: 6c09765cefe8742668c34deda1c86b881f970332؛ PR #398 ادغام شده است.
- Trade.Domain، Trade.Application و Trade.Infrastructure در Template.sln موجودند.
- Server/Program.cs، ITradeAuthorization را به TradeAuthorizationAdapter متصل می‌کند.
- TradeDbContext و TradeDbContextOptionsConfiguration موجودند، اما در Server/Program.cs ثبت AddDbContext<TradeDbContext> دیده نشد.
- Server.csproj به Trade.Application reference دارد؛ Trade.Infrastructure به‌عنوان reference مستقیم Server دیده نشد. باید بررسی شود چرا adapter با وجود این ساختار compile می‌شود و آیا transitive/reference دیگری وجود دارد؛ این نکته پیش از DI persistence باید با build واقعی تعیین تکلیف شود.
- در tree فعلی Modules/Trade هیچ migration، design-time factory یا migration history configuration اختصاصی دیده نشد.
- ITradeModuleEntitlement فقط Port است؛ پیاده‌سازی runtime/DI و enforcement فعلی پیدا نشد.
- TradeAuthorizationAdapter موجود است، اما endpoint/use case تجاری‌ای برای مصرف آن در این foundation وجود ندارد.
- کاتالوگ و seed مجوزهای Trade و فرم تخصیص نقش هنوز پیاده‌سازی نشده‌اند.
- هیچ migration مشتری اجرا نشده است.

## اعتبارسنجی
- بررسی GitHub tree، project files، Program.cs، قراردادهای Trade و اسناد انجام شد.
- Build/Test محلی اجرا نشد؛ دسترسی به working tree و اجرای SDK در این مرحله وجود نداشت.
- CI شاخه این مستندات هنوز اجرا نشده است. هیچ نتیجه Verified ادعا نمی‌شود.

## گام بعدی
پس از تکمیل review این PR، Issue مستقل S1 برای اتصال entitlement واقعی، permission catalog/seed و API gating اجرا شود. سپس S2، ابتدا تثبیت TradeDbContext DI/design-time/migrations و بعد master data forms/API. هیچ migration مشتری بدون تأیید مستقل اجرا نشود.
