
# تحویل Issue #395 — مستندسازی D-059 و نقشه اجرایی entitlement

## هدف
ثبت تصمیم‌های پذیرفته‌شده مالک محصول برای مدل entitlement در سطح Company، ظرفیت Seat، Feature، add-on، bootstrap مدیر آفلاین و sync دوحالته؛ و تعیین ترتیب پیاده‌سازی در Vendor/Customer.

## خلاصه
- D-059 به‌عنوان تصمیم پذیرفته‌شده در docs/DECISIONS.md ثبت شد.
- سند docs/LICENSING_IMPLEMENTATION_PLAN.md مرجع اجرایی نیازمندی‌ها، معیارهای پذیرش، مرزبندی و ترتیب #390 تا #394 است.
- سندهای Licensing، data model، workflows و open decisions به D-059 ارجاع داده شدند و تعارض maxUsers/expiry/bootstrap علامت‌گذاری شد.
- این PR صرفاً مستندسازی است؛ هیچ قابلیت runtime، Migration یا تغییر schema را ادعا نمی‌کند.

## تصمیمات کلیدی
- یک Base LicenseEnvelope برای هر Installation؛ amendment امضاشده و sequence افزایشی برای add-onها و همان LicenseId.
- Company-scoped Module/Feature/UserSeat؛ maxCompanies در سطح Installation.
- Seat reduction = block-new؛ موجودی عضویت‌ها حفظ می‌شود.
- Feature limit reduction داده را حذف نمی‌کند؛ ایجاد/گسترش جدید ممنوع و over-limit نمایان می‌شود.
- Term expiry = restricted؛ بدون grace ضمنی؛ read/export/recovery باقی می‌ماند.
- Cloud server-authoritative؛ Offline آخرین entitlement امضاشده پذیرفته‌شده را حفظ می‌کند.
- Sync هم online authenticated و هم signed file exchange.
- مدیر اولیه Offline فقط روی Customer Host تعیین رمز می‌کند؛ رمز/hash به Vendor/package/log/telemetry منتقل نمی‌شود.

## فایل‌های تغییرکرده
- docs/DECISIONS.md
- docs/INDEX.md
- docs/LICENSING.md
- docs/LICENSING_DATA_MODEL.md
- docs/LICENSING_WORKFLOWS.md
- docs/LICENSING_OPEN_DECISIONS.md
- docs/LICENSING_THREAT_MODEL.md
- docs/ROADMAP.md
- docs/REQUIREMENTS_TRACEABILITY.md
- docs/LICENSING_IMPLEMENTATION_PLAN.md
- docs/WORK_LOG.md
- docs/PROJECT_STATUS.md
- docs/handoffs/ISSUE-395-d059-entitlement-roadmap.md

## تست و اعتبارسنجی
- تغییرات مستنداتی هستند؛ Build و تست runtime برای این PR اجرا نشده‌اند.
- بازبینی لینک‌های داخلی، هم‌خوانی D-059 با Issues #389–#395 و عدم ادعای Implemented/Verified لازم است.

## موارد باقی‌مانده
- اجرای #390، سپس #391/#392، سپس #393 و #394 طبق نقشه.
- PR #388 مستقل است و رفع خطای Build آن در PR جداگانه پیگیری می‌شود.
- تست‌های واقعی دو میزبان، TPM فیزیکی و recovery drill در انتهای فاز.

## شناسه‌های Git
- Branch: docs/issue-395-d059-entitlement-roadmap
- Commit: پس از ایجاد PR ثبت می‌شود.
- Pull Request: پس از ایجاد ثبت می‌شود.


## D-060 افزوده‌شده (2026-10-31)
- سه حوزه منطقی: Vendor CRM & Commercial، Licensing & Entitlement Engine، Customer Deployment & Sync.
- License-first startup state machine و Activation Wizard چندشرکتی در Issue #396 ثبت شد.
- Offline exchange: .request.json و .license.json؛ .txt فقط wrapper بایت‌های دقیق signed package؛ private key/password هرگز export نمی‌شود.
- مستندات تکمیلی: DECISIONS.md، LICENSING_IMPLEMENTATION_PLAN.md، LICENSING_WORKFLOWS.md، LICENSING_DATA_MODEL.md، LICENSING_THREAT_MODEL.md، REQUIREMENTS_TRACEABILITY.md، ROADMAP.md، PROJECT_STATUS.md و WORK_LOG.md.
- همه موارد جدید Accepted / NotImplemented هستند؛ PR مستنداتی به معنی تحویل کد یا Production readiness نیست.
