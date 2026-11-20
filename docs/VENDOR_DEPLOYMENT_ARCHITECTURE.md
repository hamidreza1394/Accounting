# معماری استقرار مشتری، Bootstrap و توپولوژی دیتابیس Vendor

> **وضعیت:** تصمیم‌های D-110 تا D-113 در 2026-11-20 توسط مالک پذیرفته شده‌اند؛ پیاده‌سازی آن‌ها در main هنوز NotImplemented است. این سند قرارداد معماری است، نه ادعای قابلیت فعال.

## 1. مدل هویتی مستقل از استقرار

Tenant مرز مالکیت و جداسازی داده مشتری است؛ Company یک شرکت حقوقی/عملیاتی داخل Tenant؛ و FiscalPeriod دوره مالی داخل Company است. این شناسه‌ها مستقل از تعداد دیتابیس، سرور، دامنه یا IP باقی می‌مانند. هر مشتری مستقل Tenant مستقل دارد، اما یک فضای کاری گروهی می‌تواند چند Company را در یک Tenant نگه دارد (D-087/D-103).

```text
CustomerAccount
  └── Tenant
       ├── Company A
       │    ├── FiscalPeriod 1405
       │    └── FiscalPeriod 1406
       └── Company B
            ├── FiscalPeriod 1405
            └── FiscalPeriod 1406
```

## 2. مدیر اولیه مشتری (Bootstrap)

- هر Tenant در Provisioning یک مسیر ایجاد اولین TenantOwner دارد.
- مشتری نام کاربری و رمز خودش را تعیین می‌کند؛ admin فقط می‌تواند پیشنهاد نام باشد. رمز ثابت/مشترک و Seed رمز آزمایشی ممنوع است.
- Bootstrap token امضاشده، یک‌بارمصرف، دارای انقضا و مقید به Tenant و Installation است. برای Dedicated/Offline اعتبارسنجی محلی ممکن است.
- پس از ایجاد اولین مدیر، مسیر Bootstrap بسته می‌شود. حساب اولیه مدیر Vendor نیست و دسترسی سراسری یا خودکار به همه زیرسیستم‌ها ایجاد نمی‌کند.
- مسیر recovery مدیر باید جداگانه، ممیزی‌شده و مقاوم در برابر تصاحب حساب باشد.

## 3. تفکیک Control Plane از Customer Data Plane

- Vendor Control Plane سفارش، entitlement، provisioning metadata و نگاشت نصب را نگه می‌دارد.
- Customer Operational Data (اسناد مالی، اشخاص، کالا، اسناد بازرگانی و ...) در دیتابیس عملیاتی مشتری می‌ماند.
- Dedicated می‌تواند یک دیتابیس metadata/config محلی کوچک داشته باشد؛ نیازی نیست برای هر دوره مالی دیتابیس metadata جدا ساخته شود.
- Connection String و credential دیتابیس عملیاتی در Vendor Control Plane ذخیره نمی‌شود. اسرار در Secret Store مناسب نصب نگهداری و فقط از میزبان برنامه مصرف می‌شوند.

## 4. توپولوژی‌های دیتابیس قابل پشتیبانی

| توپولوژی | محل تفکیک | مثال |
|---|---|---|
| Tenant DB | یک دیتابیس برای کل Tenant | همه شرکت‌ها و دوره‌ها در یک DB |
| Company DB | یک دیتابیس برای هر Company | شرکت A همه سال‌ها در DB-A؛ شرکت B همه سال‌ها در DB-B |
| Tenant + FiscalPeriod DB | یک دیتابیس برای هر دوره مالی Tenant | DB-1405 برای همه شرکت‌ها؛ DB-1406 برای همه شرکت‌ها |
| Company + FiscalPeriod DB | یک دیتابیس برای هر جفت شرکت/دوره | ۲ شرکت × ۲ سال = ۴ DB |

درخواست «هر شرکت دیتابیس مستقل داشته باشد و تمام سال‌های آن شرکت در همان دیتابیس باشند» توپولوژی Company DB است. درخواست «هر شرکت و هر سال مالی دیتابیس مستقل داشته باشند» توپولوژی Company + FiscalPeriod است. اگر مشتری یک دیتابیس مشترک برای تمام شرکت‌های یک Tenant در هر سال مالی بخواهد، توپولوژی Tenant + FiscalPeriod مناسب است.

**پیش‌فرض کم‌هزینه:** Tenant DB. جداسازی بیشتر فقط با نیاز روشن، بررسی ظرفیت/هزینه و انتخاب مشتری در زمان خرید فعال می‌شود.

### قواعد فنی
- نگاشت سمت سرور بر اساس TenantId و در صورت نیاز CompanyId/FiscalPeriodId انجام می‌شود؛ الگوی precedence باید deterministic باشد.
- زیرسیستم‌ها فقط از DbContext/Factory و routing مورد اعتماد استفاده می‌کنند؛ Connection String یا DatabaseId از کلاینت پذیرفته نمی‌شود.
- از Cross-database FK و فرض Distributed Transaction اجتناب می‌شود.
- Master Data طبق مالکیت D-105 نگهداری می‌شود؛ داده مرجع Company-level نباید بدون دلیل در هر DB سالانه کپی شود. Snapshot یا versioning فقط برای مواردی که نیاز تاریخی/ممیزی دارند تعریف می‌شود.
- عملیات چنددیتابیسی مانند انتقال مانده افتتاحیه، import یا گزارش تجمیعی به orchestration، idempotency، audit و reconciliation نیاز دارد.
- Connection pooling و DbContext pooling باید tenant-safe باشند؛ DbContext دارای tenant/connection state نباید برای Tenant دیگر reuse شود.

## 5. انتقال و تغییر توپولوژی

تغییر توپولوژی با تغییر TenantId/CompanyId/FiscalPeriodId انجام نمی‌شود. روال پیشنهادی:

1. ثبت درخواست مشتری و تأیید مدیر فنی.
2. بررسی نسخه Schema، ظرفیت، سازگاری ماژول‌ها و روابط داده.
3. Backup قابل بازیابی و ثبت checksum/manifest.
4. توقف موقت نوشتن یا اعمال راهبرد کنترل‌شده برای تغییرات هم‌زمان.
5. انتقال داده و بازسازی indexها/constraints.
6. تطبیق شمارش رکوردها، روابط و جمع‌های مالی/مانده‌ها.
7. تغییر اتمیک DatabaseAssignment و smoke test.
8. نگهداری منبع تا پایان پنجره Rollback و سپس آرشیو/پاکسازی طبق سیاست retention.

در این جریان نباید روی تراکنش توزیع‌شده بین چند SQL Server حساب کرد. هر مرحله باید retry-safe و قابل ممیزی باشد.

> **قرارداد عملیاتی Routing و مهاجرت کنترل‌شده در D-115** (`docs/DECISIONS.md`) ثبت شده است: منبع یگانه Routing (Registry سمت سرور)، سیاست Read/Write در حالت Transitioning، مراحل پیش‌شرط Cutover (Final Reconciliation تنها با توقف معتبر Write مبدأ)، Cutover اتمیک، Health Verification پس از Cutover و مرز Rollback (فقط تا پیش از اولین Write روی مقصد). روال این بخش باید با آن سازگار اجرا شود و در تعارض، D-115 مرجع است. توپولوژی «Tenant + FiscalPeriod DB» تا تأیید مالک Pending است و قابل‌انتخاب نیست.

## 6. Endpoint در Dedicated

دامنه اجباری نیست. Endpointهای پشتیبانی‌شده:

- دامنه DNS با HTTPS؛ روش توصیه‌شده برای دسترسی عمومی.
- IP عمومی ثابت با HTTPS و گواهی معتبر دارای IP SAN، مشروط به پشتیبانی CA.
- IP خصوصی یا DNS داخلی روی LAN/VPN با گواهی CA داخلی مورد اعتماد.
- IP عمومی پویا با DNS/DDNS یا VPN امن.

تغییر دامنه/IP فقط Endpoint را تغییر می‌دهد؛ Tenant identity، Installation identity و اعتبار License را تغییر نمی‌دهد. از HTTP عمومی، بازکردن SQL Server به اینترنت، تکیه به Host/X-Forwarded-Host برای تعیین Tenant، و استفاده Production از گواهی self-signed بدون trust distribution اجتناب شود. namespace رزروشده .local برای DNS داخلی استفاده نشود.

## 7. چرخه انتخاب و تغییر پروفایل

- در خرید اولیه: Shared/Dedicated، توپولوژی دیتابیس و Endpoint موردنیاز انتخاب می‌شوند.
- پس از خرید: مشتری درخواست تغییر می‌دهد؛ مدیر فنی مجاز اثر امنیتی، مالی، ظرفیت، Backup/Restore و Downtime را بررسی و تأیید می‌کند.
- Provisioning و runtime از قرارداد منطقی مشترک استفاده می‌کنند؛ تفاوت در پروفایل و زیرساخت است.
- نصب Dedicated/Offline باید بتواند با امضای محلی بسته و بدون تماس دائمی با Vendor راه‌اندازی شود.

## 8. مرز پیاده‌سازی مرحله‌ای

این تصمیم‌ها به معنی ساخت فوری چندین abstraction یا دیتابیس برای همه مشتریان نیستند. فاز اول باید فقط قرارداد و یک مسیر ساده را پیاده کند:
1. Tenant DB به‌عنوان پیش‌فرض؛
2. DatabaseAssignment و routing سمت سرور با قابلیت توسعه؛
3. Bootstrap امن مدیر اولیه؛
4. ثبت DeploymentMode و Endpoint؛
5. انتقال چنددیتابیسی به مرحله بعد، پس از تست عملیاتی و backup/restore.

گزینه‌های Company DB، Tenant+FiscalPeriod DB و Company+FiscalPeriod DB باید در مدل هدف قابل بیان باشند، اما اجرای آن‌ها می‌تواند بر اساس نیاز مشتری و ظرفیت پشتیبانی مرحله‌ای شود.
