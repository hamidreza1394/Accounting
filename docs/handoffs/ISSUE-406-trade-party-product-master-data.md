# تحویل وظیفه — Issue #406: Party و Product master data

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/406
- **Agent/مسئول:** ChatGPT
- **تاریخ:** 2026-10-31
- **وضعیت:** Domain و EF model پیاده‌سازی شده؛ منتظر CI و بازبینی نهایی

## منبع نیازمندی
پروپوزال `Faranou-Trade-Import-Proposal-v1.2.docx`، بخش‌های ۷-۸، ۸-۱، ۸-۲-۱ (Party)، ۸-۲-۲ (Product) و ۸-۲-۳۷ (ProductIdentifier).

## معیارهای پذیرش
- [x] TradeEntity با TenantId، audit UTC، RowVersion و Soft Delete.
- [x] Party شرکت‌محور با نقش‌های چندگانه و حساب بانکی فرزند.
- [x] حساب بانکی با وضعیت تأیید صریح و تأییدکننده/زمان.
- [x] Product شرکت‌محور با گروه، HS و واحد پایه.
- [x] GTIN و IRC مستقل و متنی، با تاریخ اعتبار.
- [x] EF mapping فقط در schema trade.
- [x] FKهای فرزند به والد با TenantId مرکب.
- [x] تست‌های Domain و EF metadata اضافه شدند.
- [ ] CI Build/Test سبز.
- [ ] Migration مستقل Trade در Issue جدا، با SQL Server disposable و بررسی rollout/rollback.
- [ ] API و UI در برش‌های جداگانه.

## فایل‌های مهم
- `Modules/Trade/Trade.Domain/Common/TradeEntity.cs`
- `Modules/Trade/Trade.Domain/Parties/*`
- `Modules/Trade/Trade.Domain/Products/*`
- `Modules/Trade/Trade.Infrastructure/Persistence/TradeDbContext.cs`
- `Modules/Trade/Trade.Infrastructure/Persistence/Configurations/*`
- `tests/Template.Tests/TradeMasterDataDomainTests.cs`
- `tests/Template.Tests/TradeMasterDataModelTests.cs`

## تصمیم‌های فنی
- Party/Product بدون FiscalPeriodId هستند.
- TradeEntity با NotMapped از inheritance mapping ناخواسته EF جدا شده است.
- FK مرکب TenantId/Id مرز tenant را در روابط فرزند هم enforce می‌کند.
- هیچ Global Query Filter به‌عنوان مرز امنیتی اضافه نشده است.
- حساب بانکی فقط در حالت Verified از دید حساب بانکی eligible است؛ use case پرداخت باید فعال بودن Party را هم بررسی کند.

## آزمون و داده
- Build/Test محلی: اجرا نشده (dotnet SDK در محیط اجرا موجود نیست).
- CI: منتظر PR و نتیجه workflow.
- Migration: تولید نشده و اجرا نشده.
- دیتابیس مشتری: بدون اتصال و بدون تغییر.

## شناسه Git
- Branch: `feat/issue-406-trade-party-product-master-data`
- Base branch: `feat/issue-404-trade-persistence-registration-v2` (PR #405)
- Pull Request: #408 (بازپایه‌گذاری‌شده روی PR #405).
