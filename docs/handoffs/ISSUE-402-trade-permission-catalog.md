# تحویل وظیفه — ISSUE-402-trade-permission-catalog

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/402
- **Agent/مسئول:** ChatGPT
- **تاریخ:** 2026-10-31
- **وضعیت:** پیاده‌سازی روی شاخه انجام شده؛ Build/CI و SQL Server verification هنوز تأیید نشده است.

## هدف و معیارهای پذیرش
- [x] تعریف کاتالوگ strongly-typed و پایدار مجوزهای فرم/گزارش Trade در مالکیت ماژول.
- [x] استفاده از PermissionDefinitions و CompanyRolePermissions مشترک؛ بدون ایجاد نقش یا Permission store موازی.
- [x] Migration داده‌ای idempotent که ردیف‌های موجود را بازنویسی نکند و هیچ مجوزی را خودکار به نقش تخصیص ندهد.
- [x] Down فقط ردیف‌های متعلق به seed را که هیچ CompanyRolePermission به آن‌ها ارجاع نمی‌دهد حذف کند.
- [x] تست واحد و تست SQL Server برای کاتالوگ و رفتار Up/Down نوشته شود.
- [ ] اجرای Build، تست‌های .NET و تست SQL Server و موفقیت CI.

## خلاصه تغییرات
- کاتالوگ اولیه شامل ۴۶ مجوز برای Party، Product، Purchase Request، Supplier Inquiry، Quotation Comparison، Proforma، Contract، Import Case و گزارش‌های تک‌شرکتی/چندشرکتی است.
- هر مجوز دارای Code، نام/شرح فارسی و TradeAccessIntent است. کدها ASCII، یکتا و حداکثر ۱۲۰ کاراکترند.
- FormClose به helper کدهای پایدار افزوده شد و در گروه عملیات نوشتن قرار دارد.
- Migration 20261031140000_SeedTradePermissionCatalog ردیف‌ها را با IF NOT EXISTS درج می‌کند. شناسه‌های seed ثابت‌اند و ترتیب ۲۰۰۰۰ به بعد است.
- scripts/generate_migrations.py به Migration جدید، SQL بازبینی‌شده و designer/model chain متصل شد. مدل EF تغییری نکرده است.
- تست SQL Server سناریوی وجود ردیف سفارشی قبلی، عدم بازنویسی، عدم تخصیص خودکار، Down با حفظ مجوز تخصیص‌یافته، حذف ردیف‌های استفاده‌نشده و Up مجدد را پوشش می‌دهد.

## فایل‌های مهم تغییرکرده
- Modules/Trade/Trade.Application/Authorization/TradePermissionCodes.cs
- Modules/Trade/Trade.Application/Authorization/TradePermissionCatalog.cs
- Infrastructure/Persistence/Migrations/20261031140000_SeedTradePermissionCatalog.cs
- Infrastructure/Persistence/Migrations/20261031140000_SeedTradePermissionCatalog.Designer.cs
- scripts/generate_migrations.py
- tests/Template.Tests/TradePermissionCatalogTests.cs
- tests/Template.Tests/Integration/TradePermissionCatalogSqlServerTests.cs
- docs/TRADE_MODULE.md, docs/PROJECT_STATUS.md, docs/REQUIREMENTS_TRACEABILITY.md, docs/WORK_LOG.md, docs/DATABASE.md, docs/database-migrations.md

## تصمیم‌های فنی
- مجوز تجاری TRADE_IMPORT از این کاتالوگ جداست؛ این Issue فقط مجوزهای عملیاتی کاربر را تعریف می‌کند.
- مجوزها در دیتابیس مشترک ذخیره می‌شوند؛ مدیر هر شرکت بعداً باید آن‌ها را صریحاً به نقش‌های همان شرکت تخصیص دهد.
- کاتالوگ فعلی فقط فرم‌های موج اول را پوشش می‌دهد و ادعای پوشش همه فرم‌های پیشنهاد ۹۰ صفحه‌ای ندارد.
- Migration از طریق زنجیره اصلی DatabaseContext است؛ هیچ جدول/موجودیت جدیدی ایجاد نشده است.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| dotnet build Template.sln --no-restore --configuration Release | ناموفق در commit قبلی | CI خطای CS0272 داد؛ تست مقدار Id را مستقیم set کرده بود، در حالی که setter در Entity protected است. در commit 17229 اصلاح شد تا Id از constructor تولید شود؛ اجرای مجدد روی head جدید pending است. |
| تست TradePermissionCatalogTests | اجرا نشده | تست اضافه شده؛ نیازمند اجرای CI یا محیط .NET است. |
| تست TradePermissionCatalogSqlServerTests | اجرا نشده | فقط با FARANOU_TEST_SQLSERVER اجرا می‌شود. |
| python3 scripts/generate_migrations.py --check | اجرا نشده | SQL در registry ثبت شده؛ byte-for-byte generation verification pending است. |
| python3 scripts/verify_migration_model.py | اجرا نشده | Designer از snapshot مدل فعلی کپی شده؛ guard باید اجرا شود. |
| git diff --check | اجرا نشده | diff محلی در این محیط در دسترس نیست. |

## امنیت، داده و سازگاری
- Migration فقط جدول PermissionDefinitions را seed می‌کند؛ هیچ permission grant ایجاد نمی‌کند.
- Code موجود هرگز بازنویسی نمی‌شود؛ نام/شرح سفارشی مدیر حفظ می‌شود.
- Down از حذف ردیفی که در CompanyRolePermissions مصرف شده جلوگیری می‌کند.
- Migration روی هیچ دیتابیس مشتری اجرا نشده است.
- entitlement TRADE_IMPORT هنوز در #401 و وابستگی قرارداد آن در #390/#384 باقی است؛ کاتالوگ به‌تنهایی دسترسی عملیاتی را فعال نمی‌کند.

## ریسک‌ها و کارهای باقی‌مانده
- فهرست ۴۶ مجوز باید در بازبینی با فرم‌ها/عملیات واقعی proposal و استاندارد نام‌گذاری پروژه تأیید شود؛ فرم‌های بعدی به‌صورت Issueهای مستقل افزوده شوند.
- Build/CI و SQL Server integration test باید اجرا شوند؛ تا آن زمان وضعیت Verified نیست.
- UI تخصیص مجوزها به CompanyRole و API gating در Issueهای بعدی لازم است.
- entitlement runtime و fail-closed Production در #401 وابسته به قرارداد نسخه‌دار #390 و مسیر اعتبارسنجی envelope در #384 است.

## شناسه‌های Git
- Branch: feat/issue-402-trade-permission-catalog
- Commit head فعلی: SHA جاری در PR شماره ۴۰۲؛ این handoff با هر بازبینی جدید ثابت نمی‌ماند.
- Pull Request: https://github.com/hamidreza1394/Accounting/pull/402 (Draft، base=main).
