# تحویل D-103 — مرز Tenant و Resolve دامنه

- **Issue:** #397 — مرز معماری بازرگانی خارجی و S3-0
- **تاریخ:** 2026-11-17
- **وضعیت تصمیم:** Accepted با تأیید صریح مالک محصول
- **وضعیت پیاده‌سازی:** NotImplemented؛ این PR فقط مستندات است.
- **مبنای شاخه:** main@755150929b211ac3b952a3c3a7f0856624b11f0d

## 1. تصمیم ثبت‌شده

Tenant مرز CustomerAccount/فضای کاری و جداسازی داده مشتری است. هر مشتری مستقل Tenant جدا دارد؛ یک Tenant می‌تواند چند Company داشته باشد. هر Company شناسه، دفاتر، اسناد، عضویت‌ها، مجوزها و دوره‌های مالی مستقل خود را حفظ می‌کند. دامنه اختصاصی Company و دامنه CorporateGroup هر دو به Tenant و context مربوط resolve می‌شوند. Host فقط lookup key است و authorization نیست.

دامنه مشترک به‌تنهایی دلیل ادغام نیست؛ CustomerAccount/مالکیت معتبر، قرارداد و مرز محرمانگی/عملیات معیارند. CorporateGroup و روابط مالکیت نباید از CustomerAccount عبور کنند. پس از login، Company فقط با CompanyAccessService، عضویت فعال و permission سمت سرور قابل دسترسی است.

Cloud از binding معتبر Vendor provisioning به Registry محلی استفاده می‌کند، نه تماس زنده در هر درخواست. Dedicated از Tenant پیکربندی معتبر Installation استفاده و آن را با DB تطبیق می‌دهد. طبق D-017، X-Forwarded-Host مرجع Tenant نیست. Import شناسه Tenant/Company را از فایل یا ورودی نمی‌پذیرد. Manifest D-092 فقط Backfill یک‌باره است. انتقال Company بین Tenantها نیازمند Migration کنترل‌شده و تاریخچه‌محور است.

## 2. فایل‌های تغییرکرده

- docs/DECISIONS.md — افزودن D-103.
- docs/ACCESS_CONTROL_ARCHITECTURE.md — جایگزینی بخش دامنه‌های ورود با قرارداد Tenant + Company/Group context.
- docs/SECURITY.md — ثبت قواعد امنیتی Resolve دامنه، Import و Host.
- docs/INDEX.md — به‌روزرسانی فهرست ADRها.
- docs/PROJECT_STATUS.md — ثبت وضعیت Accepted/NotImplemented و گام بعدی S3-0.
- docs/WORK_LOG.md — ثبت سابقه این تغییر.
- docs/handoffs/ISSUE-397-d103-tenant-customer-domain.md — این گزارش.

## 3. مواردی که عمداً انجام نشد

- ساخت Tenant Entity/جدول، تغییر Company یا FKها.
- ایجاد/تغییر Migration یا اجرای Backfill.
- ساخت Domain Registry، Middleware یا تغییر Authentication/Authorization runtime.
- تغییر provisioning contract یا تنظیم Gateهای Vendor.
- تغییر CustomerBoundaryId یا CorporateGroup.
- تغییر CompanyAccessService، Import pipeline یا هر کد اجرایی.

## 4. اعتبارسنجی و ریسک

- فایل‌های مبنا از شاخه main خوانده شدند؛ شماره ADR بعدی از انتهای واقعی DECISIONS.md استخراج شد: D-103 پس از D-102.
- تغییرات متنی در PR باید از نظر لینک‌ها، Markdown و عدم تناقض با D-017/D-065/D-087/D-089/D-092 بازبینی شوند.
- Build/Test اجرا نشده؛ تغییر مستنداتی است. CI هرچه در مخزن برای Docs/Markdown تعریف شده اجرا شود.
- ریسک باز: Tenant entity، CustomerReference binding، Registry محلی، migration host و Company→Tenant backfill هنوز پیاده‌سازی/Verified نیستند. اجرای زنده SQL Server و خواندن migration history واقعی همچنان بخشی از S3-0 است.

## 5. گام بعدی

ادامه S3-0 با تکمیل inventory و شواهد read-only SQL Server، سپس طراحی مرحله‌ای منبع معتبر Company→Tenant و rollout. هیچ Migration یا Backfill تا عبور از گیت‌های مصوب اجرا نشود.
