# Handoff — Issue #728: مستندسازی تصمیم‌های معماری Tenant، Bootstrap و استقرار Dedicated

**تاریخ:** 2026-11-20 | **مبنای بررسی:** `origin/main@6728cff8` (پس از Merge PRهای #722، #730، #725، #731)

## هدف و وضعیت

Issue #728 خواستار ثبت تصمیم‌های معماری مالک درباره Tenant، Bootstrap و استقرار Dedicated بود. سیر تکمیل:

1. **D-109..D-113** با PR #725 (squash commit `77f069e3`) در main ثبت شدند: D-109 سیاست تک‌اپراتوره (Implemented/Verified پس از PR #724)، D-110 Bootstrap امن مدیر اولیه، D-111 توپولوژی چنددیتابیسی و Database Routing، D-112 Endpoint اختصاصی بدون الزام دامنه، D-113 پروفایل Provisioning و مدیریت تغییرات استقرار.
2. **D-114 و D-115** در PR جاری (`docs(architecture): complete fiscal period ownership and database routing ADRs`) ثبت می‌شوند و پس از Merge، Issue #728 به‌طور خودکار بسته می‌شود.

## D-114 — مالکیت FiscalPeriod در محدوده Company

- هر FiscalPeriod متعلق به دقیقاً یک Company است و مالکیت Tenant از رابطه معتبر Company→Tenant resolve می‌شود؛ FiscalPeriod مستقیماً به Tenant تعلق نمی‌گیرد.
- دسترسی و انتخاب دوره مالی فقط با Company معتبر و مجوز کاربر کنترل می‌شود؛ هیچ FiscalPeriod خارج از Company معتبر خود استفاده نمی‌شود.
- سلسله‌مراتب در Shared و Dedicated یکسان و مستقل از محل فیزیکی دیتابیس است؛ در توپولوژی Tenant + FiscalPeriod (گزینه ۳ D-111 — Pending Owner Decision طبق D-115) مالکیت منطقی هر دوره همچنان Company خودش است و Company Scope در همه مسیرها اعمال می‌شود.
- پیاده‌سازی: NotImplemented؛ هیچ Entity، Migration یا Backfill ایجاد نمی‌کند. تکمیل معنای D-087 بدون تغییر D-066.

## D-115 — مسیریابی سمت سرور دیتابیس و مهاجرت کنترل‌شده

- تنها منبع معتبر Routing، Registry سمت سرور است؛ مقصد دیتابیس/Connection String از کلاینت پذیرفته نمی‌شود.
- برای هر Logical Scope فقط یک مقصد authoritative؛ تعارض Assignmentها fail-closed است.
- Registry فقط مرجع مقصد را نگه می‌دارد؛ credential فقط از Secret Store خوانده و در Log/Audit افشا نمی‌شود.
- فرایند انتقال با پیش‌شرطهای ترتیبی Cutover (اعتبارسنجی مقصد، Backup، انتقال اولیه، اعتبارسنجی داده، Transitioning، توقف Write مبدأ، Final Reconciliation) و پس از Cutover: Health Verification و ثبت نتیجه؛ Rollback کنترل‌شده فقط تا پیش از اولین Write روی مقصد. تغییر Routing بدون اعتبارسنجی و تأیید مجاز نیست.
- پیاده‌سازی: NotImplemented؛ فقط قرارداد. پیاده‌سازی آینده در Issue مستقل با تست‌های Routing تعریف‌شده در خود ADR انجام می‌شود.

## ⚠️ نیازمند تأیید مالک — اختلاف توپولوژی D-111

- **متن merge‌شده D-111 (از PR #725)** چهار توپولوژی را ثبت کرده و **گزینه سوم فهرست (Tenant + FiscalPeriod DB: یک دیتابیس برای هر دوره مالی Tenant که داده چند Company را با CompanyId مستقل نگه می‌دارد)** در آن با قید «فقط در صورت نیاز واقعی مشتری انتخاب می‌شود» آمده است؛ در توافق قبلی مالک سه توپولوژی (Tenant، Company، Company+FiscalPeriod = گزینه‌های ۱، ۲ و ۴ فهرست D-111) مطرح بود.
- طبق دستور مالک، این گزینه خودکار حذف یا اجباری نشد. **اثر معماری/اجرایی گزینه Tenant + FiscalPeriod:**
  1. کلید Routing برای آن Scope «Tenant+FiscalPeriod» بدون Company است؛ D-115 وضعیت آن را Pending Owner Decision ثبت می‌کند و Resolver آن را تا تأیید مالک fail-closed رد می‌کند.
  2. جداسازی Company در این حالت کاملاً منطقی (فیلتر CompanyId در همه Queryها) می‌شود، نه فیزیکی؛ ریسک نشت Cross-Company بیشتر از توپولوژی Company DB است و تست‌های نگهبان Cross-Company برای این حالت ضروری‌اند.
  3. داده‌های مرجع مشترک بین Companyهای هم‌دوره در یک DB ممکن است نیاز به کپی/اشتراک کنترل‌شده پیدا کنند (بند ۷ D-111).
  4. Backup/Restore و Restore نقطه‌ای یک Company مستقل از سایر Companyهای همان دوره ممکن نمی‌شود.
- **تصمیم موردنیاز:** تأیید یا حذف گزینه سوم D-111 توسط مالک؛ تا آن زمان، پیاده‌سازی Routing فقط سه توپولوژی توافق‌شده «Tenant DB، Company DB و Company+FiscalPeriod DB» را هدف می‌گیرد و «Tenant + FiscalPeriod DB» در پیکربندی قابل‌ذکر اما Pending و غیرقابل‌انتخاب خواهد بود.

## سابقه PR #729

PR #729 (پیش‌نویس مستقل همین موضوعات) پس از ثبت شواهد تعارض شماره‌گذاری (D-109 آن با سیاست تک‌اپراتوره ادغام‌شده تعارض داشت) بسته شد و محتوای مفید آن اکنون به شماره‌های صحیح (D-114 در این PR) منتقل شده است.

## اقدام بعدی

- پس از Merge این PR، پیاده‌سازی Database Routing طبق D-115 و Bootstrap مدیر اولیه طبق D-110/D-113 در Issueهای مستقل انجام می‌شود.
