# تحویل وظیفه — ISSUE-UNNUMBERED-vendor-tenant-derived-view-correction

- **Issue:** `UNNUMBERED` (تصمیم مرجع: ADR **D-103**؛ شمارهٔ جعلی ساخته نشد)
- **Agent/مسئول:** GLM agent (z.ai)
- **تاریخ:** 2026-11-18
- **وضعیت:** انجام‌شده — اصلاح محدودهٔ V1 بر پایهٔ تصمیم مالک
- **شاخهٔ مرجع حفظ‌شده:** `feat/tenant-v1-management` با Commit `7e9abc1c` (دست‌نخورده؛ فقط سابقه)
- **شاخهٔ اصلاحی:** `fix/d103-v1-derived-tenant-view` از `origin/main@89c00641`

## ۱) نتیجهٔ بررسی پیاده‌سازی اولیه
پیاده‌سازی `7e9abc1c` از نظر کیفیت کد (الگوی Antiforgery/RowVersion/Audit/مجوزدهی) درست نوشته شده بود، اما **از نظر محدوده غیرقابل قبول است**: Entity مستقل `VendorTenant`، Migration، ستون `TenantId` روی `VendorInstallations`، APIهای مستقل مدیریت Tenant، کدهای مجوز `vendor.tenants.*`، ResourceType مستقل و Maker-Checker مستقل Tenant — همه فراتر از مجاز V1 (نمایش مشتق‌شده) بودند. بر اساس تصمیم مالک، در شاخهٔ اصلاحی حذف شدند و فقط نمایش مشتق‌شدهٔ فقط-خواندنی باقی مانده است.

## ۲) تغییرات خارج از محدوده که شناسایی و حذف شدند
- Entity مستقل `VendorTenant` و جدول `VendorTenants` و Migration `20261118100153_AddVendorTenantsForD103V1` (فایل + Designer + تغییر Snapshot)
- ستون جدید `TenantId` روی `VendorInstallations` و FK/ایندکس آن و گسترش `InstallationResponse`
- APIهای مستقل `/api/vendor/tenants*` (لیست/جزئیات/ایجاد/مسدودسازی وضعیت) و `PUT .../tenant-binding`
- کدهای مجوز `vendor.tenants.read` و `vendor.tenants.manage` در کاتالوگ و ResourceType مستقل `"tenant"` در `VendorResourceCatalog`/`VendorResourceAccessEvaluator`
- Maker-Checker مستقل Tenant (`resourceType=tenant` در `/api/vendor/approvals/status-change` و تصمیم)
- `TenantKey` ذخیره‌شده/مدیریت‌شونده (سمت سرور `Guid "N"`)
- UI مستقل: بخش `tenants`، منوی جدید، `TenantForm`، `TenantBindingForm` و اکشن‌های وضعیت/اتصال
- تست‌های متصل به قابلیت‌های حذف‌شده
- گزارش وضعیت قبلی در `PROJECT_STATUS`/`WORK_LOG`/`DECISIONS`/`INDEX` و handoff قبلی (در شاخهٔ حفظ‌شده می‌مانند و در شاخهٔ اصلاحی بازنویسی/هم‌راستا شدند)

**اثر حذف Migration:** شاخهٔ `feat/tenant-v1-management` و `7e9abc1c` و bundle/patch قبلی در `download/` حفظ شده‌اند؛ تاریخچه بازنویسی نشده است. Migration هرگز push نشد و روی هیچ دیتابیس واقعی (محلی/CI/تولید) اجرا نشد؛ بنابراین حذف آن از شاخهٔ اصلاحی (که از `main` تازه ساخته شده) هیچ اثر Schema یا داده‌ای ندارد و `has-pending-model-changes` تأیید می‌کند مدلی معلق وجود ندارد.

## ۳) قابلیت‌های مجاز V1 که حفظ/تکمیل شدند
- **نمایش مشتق‌شدهٔ Tenant از CustomerAccount، بدون ذخیره‌سازی:** `VendorTenantView` (record فقط-خواندنی با سه فیلد `DerivedFrom="customer-account"`، `WorkspaceReference=CustomerNumber مشتری`، `Status=وضعیت مشتری`) که در زمان درخواست با `VendorTenantView.From(...)` ساخته و در `CustomerResponse` همهٔ چهار مسیر (لیست/جزئیات/ایجاد/ویرایش) بازگردانده می‌شود؛ هیچ ذخیره‌سازی، کد مجوز، endpoint یا lifecycle مستقلی ندارد.
- **UI فارسی RTL:** ستون فقط-خواندنی «فضای کاری (مشتق از حساب مشتری)» در بخش موجود «مشتریان تجاری» (بدون منو/فرم جدید).

## ۴) قابلیت‌هایی که از قبل در main بودند و دوباره‌کاری نشدند
- مدیریت CustomerAccount و Installation (CRUD موجود `VendorBusinessEndpoints`)
- Retire معادل: تعلیق/فعال‌سازی با Maker-Checker موجود (`Active`↔`Suspended`)
- Reconnect از مسیر Provisioning موجود و ACK امضاشدهٔ m2m (`/{requestId}/acknowledgement`، allow-list #699)
- پنل فارسی RTL موجود Vendor.Web با الگوی کامپوننت‌های موجود

## ۵) فایل‌های اصلاح‌شده (شاخهٔ اصلاحی)
- `Vendor/Vendor.Host/Authorization/VendorTenantView.cs` — جدید؛ نمایش مشتق‌شدهٔ فقط-خواندنی
- `Vendor/Vendor.Host/Authorization/VendorBusinessEndpoints.cs` — گسترش `CustomerResponse` با `TenantView` در چهار مسیر
- `Vendor/Vendor.Web/src/BusinessLists.tsx` — ستون جدید + `kind: 'tenantView'` در `formatValue`
- `Vendor/Vendor.Web/src/App.test.tsx` — پوشش ستون و متن نمایشی
- `tests/Vendor.Host.Tests/VendorTenantViewTests.cs` — Unit: مشتق‌سازی خالص، آینه‌ای بودن وضعیت، نبود شناسه/کلید قابل مدیریت (نگهبان scope)
- `tests/Vendor.Host.Tests/VendorCustomerTenantViewHttpIntegrationTests.cs` — Integration (SQL Server): وجود `tenantView` در پاسخ جزئیات مشتری + **تست نگهبان**: `GET/POST /api/vendor/tenants/` باید `404` باشد
- مستندات: `docs/PROJECT_STATUS.md`، `docs/WORK_LOG.md`، `docs/DECISIONS.md`، `docs/INDEX.md` و همین handoff

## ۶) وضعیت Migration و Schema
- **بدون هیچ Migration جدید و بدون تغییر Snapshot**؛ `dotnet ef migrations has-pending-model-changes` = «No changes».
- هیچ جدول، ستون یا FK جدیدی به Vendor DB اضافه نشد؛ دیتابیس مشتری (`Server/**`) و قراردادهای Canonical و Provisioning/ACK/v5 Gate دست‌نخورده ماندند.

## ۷) تست‌های واقعاً اجراشده (محلی، SDK 10.0.404)
| فرمان | نتیجهٔ واقعی |
|---|---|
| `dotnet build Vendor.sln -c Release` | موفق |
| `dotnet test Vendor.sln --no-build -c Release` | **Passed! ۳۵۸ کل، ۳۰۴ موفق، ۰ ناموفق، ۵۴ Skip** |
| `dotnet ef migrations has-pending-model-changes` | بدون تغییر مدل |
| `npm ci && npm run typecheck && npm test` (Vendor.Web) | **۶۸/۶۸ موفق** |
| `npm run build` + بودجهٔ دارایی | موفق؛ JS gzip ۹۷,۷۷۲ ≤ ۱۰۲,۴۰۰ و CSS ۸,۸۴۰ ≤ ۲۰,۴۸۰ |
| `python3 scripts/check_static_assets.py` | PASS (بدون CDN) |
| `git diff --check` | پاک |

## ۸) تست‌های Skip یا اجرانشده
- **۵۴ تست Skip** همگی نیازمند SQL Server واقعی‌اند (`FARANO_VENDOR_TEST_SQLSERVER` در محیط موجود نیست؛ Docker در دسترس نیست)، از جمله تست Integration این PR (`VendorCustomerTenantViewHttpIntegrationTests`). این تست‌ها فقط روی CI (SQL Server 2022) اجرا می‌شوند و تا سبز شدن CI، هیچ موردی «Verified» اعلام نمی‌شود.
- تست E2E مرورگری برای ستون جدید اضافه نشده است (پوشش Vitest موجود).
- CI این شاخه هنوز اجرا نشده است؛ موفقیت CI ادعا نمی‌شود.

## ۹) ریسک‌ها و موارد باقی‌مانده
- گسترش `CustomerResponse` یک تغییر قرارداد API افزودنی است (فیلد جدید)؛ مصرف‌کننده‌های موجود صرف‌نظر می‌کنند و تست‌های موجود تغییری در رفتار نداشتند.
- جست‌وجوی سمت سرور، خلاصهٔ اطلاعات و تاریخچهٔ CustomerAccount/Installation در main و در هر دو شاخه وجود ندارد — نیازمند اولویت مالک در وظیفه‌ای مستقل.
- `LastSeenAtUtc` ستون موجود است اما **هیچ نویسنده‌ای ندارد**؛ ثبت آن از ACK معتبر نیازمند تغییر handler ماشین‌به‌ماشین Provisioning است که در این مأموریت ممنوع است (بند ۴) — نیازمند تصمیم مالک.
- مفاهیم «Cloud/Dedicated از یک مدل مالکیت واحد» در گیت‌های S3-0 باقی است.

## ۱۰) شناسه‌های Git
- Branch اولیه: `fix/d103-v1-derived-tenant-view` (پایه: `origin/main@89c00641`) — Commitهای `743b0d90` (کد) و `f3b5bf6f` (مستندات)
- **انتقال به مبنای فعلی main (به‌روزرسانی 2026-11-18):** Branch نهایی `fix/d103-v1-derived-tenant-view-rebased` (پایه: `origin/main@b785e0148e1ee6213c0d5e5edb541b7c1dc3d5bb`؛ پس از Merge PR #706)
- Commitهای نهایی: `a3cd7137` (کد؛ cherry-pick از `743b0d90`) و `53d66a87` (مستندات؛ cherry-pick از `f3b5bf6f`) — با `-x` و **بدون هیچ تعارضی**
- Push/PR/Merge انجام نشده؛ طبق محدودیت‌های مأموریت

## ۱۱) آمادگی برای بازبینی و PR
- Diff کوچک (۴ فایل کد/تست + ۵ فایل مستندات)، بدون تغییر Schema، بدون قابلیت تکراری، با تست نگهبان scope — آمادهٔ بازبینی. Push/PR پس از مجوز/توکن دارای دسترسی انجام خواهد شد.

## ۱۲) موارد نیازمند تصمیم مالک
- اولویت و شکل «جست‌وجوی سمت سرور، خلاصه و تاریخچه» برای CustomerAccount/Installation
- مجوز تغییر handler ACK برای ثبت `LastSeenAtUtc` امضاشده (در بند ۴ فعلی ممنوع)
- تکلیف شاخهٔ سابقهٔ `feat/tenant-v1-management` (حفظ/حذف) و تصریح اینکه V1 صرفاً نمایشی است در صورت تمایل به ثبت ADR جداگانه

## ۱۳) پیوست: انتقال به مبنای `main@b785e014` و بررسی LastSeenAtUtc (2026-11-18)
- **حفظ PR #706:** تمام تغییرات مستنداتی #706 (`D-104`، یادداشت تاریخ‌دار D-086، سند Trade و handoff آن) دست‌نخورده باقی است؛ Cherry-pick دو Commit این شاخه روی `b785e014` **بدون تعارض** انجام شد (مناطق تغییر غیرمجاور بودند: DECISIONS.md بخش D-103 و آغاز PROJECT_STATUS در برابر ضمیمهٔ D-104 و بخش Trade).
- **سازگاری D-103/D-104:** D-104 مربوط به کاردینالیتی Proforma↔OrderRegistration در دامنه Trade است و با تصمیم D-103 در دامنه Vendor هم‌پوشانی ندارد؛ هیچ ADR تکراری، شماره تکراری، Migration یا تغییر Schema ایجاد نشد.
- **بررسی LastSeenAtUtc (فقط بررسی، بدون تغییر کد):** در کل Vendor دقیقاً ۶ رخداد غیر-Migration وجود دارد: یک property نال‌پذیر در `VendorBusinessEntities` و ۵ projection فقط-خواندنی در `InstallationResponse`. **هیچ سایت نوشتن (assignment) وجود ندارد** و handler امضاشدهٔ m2m ACK (`POST /{requestId}/acknowledgement`) نیز آن را ثبت نمی‌کند؛ یعنی ACK معتبر در حال حاضر `LastSeenAtUtc` را به‌روز نمی‌کند. این به‌عنوان **Gap مستند** می‌ماند و ثبت آن نیازمند تصمیم مالک و تغییر جداگانهٔ handler Provisioning است (در محدودهٔ این مأموریت ممنوع).
- **اعتبارسنجی مجدد پس از انتقال:** `dotnet test Vendor.sln` = ۳۵۸ کل، ۳۰۴ موفق، ۰ ناموفق، ۵۴ Skip؛ `npm test` = ۶۸/۶۸؛ بودجهٔ دارایی برقرار؛ `check_static_assets.py` PASS؛ `git diff --check` پاک؛ `has-pending-model-changes` بدون تغییر؛ CI هنوز اجرا نشده و موفقیت آن ادعا نمی‌شود.

**وضعیت نهایی: Ready for Review** (بدون نیاز به تصمیم مالک برای ادغام این شاخه؛ موارد بند ۱۲ مستقل‌اند)
