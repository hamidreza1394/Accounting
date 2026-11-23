# برگهٔ ثبت شواهد — اولین چرخهٔ لایسنس Staging

- **وضعیت:** آمادهٔ تکمیل؛ تا ثبت اجرای واقعی همهٔ موارد `Not Run` هستند.
- **دامنه:** یک Customer آزمایشی، یک Installation، یک Company، یک لایسنس با entitlement شرکت‌محور `TRADE_IMPORT`.
- **مبنای اجرا:** `docs/runbooks/FIRST-STAGING-LICENSE-CYCLE.md`.
- **محیط مصوب این مرحله:** یک Windows Server، دو پردازه/سرویس مستقل Vendor و Customer و دو دیتابیس SQL Server مستقل. این انتخاب فقط برای Staging اولیه است؛ معماری محصول باید استقرار روی دو Windows Server مستقل را نیز پشتیبانی کند.
- **محدودیت شواهد:** این اجرا استقلال سخت‌افزار/سیستم‌عامل، جداسازی دو میزبان، توزیع/چرخش trust بین میزبان‌ها یا آمادگی Production را اثبات نمی‌کند.

## 1) مشخصات محیط

| فیلد | مقدار ثبت‌شونده |
|---|---|
| شناسه اجرای آزمایش | |
| تاریخ و ساعت شروع/پایان (با منطقه زمانی) | |
| مسئول اجرا / شاهد | |
| Windows Server نام/نسخه/Build | |
| هویت سرویس Vendor (نام حساب؛ بدون رمز) | |
| هویت سرویس Customer (نام حساب؛ بدون رمز) | |
| SQL Server instance | |
| نام دیتابیس Vendor | |
| نام دیتابیس Customer | |
| مسیر داده/کلید Vendor (بدون درج محتوای کلید) | |
| نسخه/commit نرم‌افزار Vendor | |
| نسخه/commit نرم‌افزار Customer | |
| محیط | Staging فقط |

**کنترل جداسازی:** Vendor و Customer باید دو پردازه/سرویس با تنظیمات، هویت سرویس و دسترسی فایل مستقل باشند. دیتابیس‌ها جدا باشند؛ هیچ connection string یا دسترسی مستقیم Customer به Vendor DB و هیچ دسترسی مستقیم Vendor به Customer DB وجود نداشته باشد. این دو سرویس با وجود جداسازی منطقی، همچنان یک میزبان و یک failure domain سخت‌افزاری/سیستم‌عاملی مشترک دارند.

## 2) پیش‌شرط‌های ایمنی — پیش از هر تغییر

| کنترل | نتیجه (Pass/Fail/Not Run) | شاهد/مسیر فایل | توضیح |
|---|---|---|---|
| تأیید کتبی مالک برای شروع اجرای Staging | Not Run | | |
| تأیید میزبان و بازهٔ نگهداری | Not Run | | |
| تأیید وجود Backup معتبر از DBهای هدف و روش Restore | Not Run | | |
| تأیید عدم استفاده از کلید/گواهی Production | Not Run | | |
| تأیید عدم وجود secret در command history، log یا فایل شاهد | Not Run | | |
| تأیید Runtime Gate خاموش است | Not Run | | |
| تأیید `DenyAllTradeModuleEntitlement` بدون تغییر باقی است | Not Run | | |
| تأیید عدم دسترسی متقاطع DB بین Vendor و Customer | Not Run | | |

**توقف اجباری:** این برگه مجوز اجرای Migration، تغییر تنظیمات سرویس، ساخت/حذف کلید، صدور لایسنس یا روشن‌کردن Gate نیست. هر اقدام عملیاتی باید طبق Runbook و پس از تأیید مالک انجام شود. Runtime Gate در این مرحله OFF می‌ماند مگر اینکه مالک جداگانه و صریحاً فعال‌سازی محدود Staging را تأیید کند.

## 3) چرخهٔ Vendor

| گام | نتیجه (Pass/Fail/Not Run) | شاهد/مسیر فایل | زمان | توضیح |
|---|---|---|---|---|
| Vendor DB آماده و Migration مجاز اجرا شد | Not Run | | | |
| Bootstrap مدیر Vendor تکمیل شد | Not Run | | | |
| Key Ceremony مربوط به کلید Staging اجرا و ثبت شد | Not Run | | | |
| ACL کلید خصوصی فقط به هویت سرویس Vendor دسترسی می‌دهد | Not Run | | | |
| SPKI عمومی صادر و fingerprint آن ثبت شد | Not Run | | | |
| امضا با هویت سرویس Vendor موفق بود | Not Run | | | |
| امضا با هویت غیرمجاز یا کلید حذف‌شده fail-closed شد | Not Run | | | |
| Customer/Installation/License آزمایشی ایجاد شد | Not Run | | | |
| وضعیت لایسنس Issued و پاکت امضاشده ثبت شد | Not Run | | | |
| Export endpoint پاکت مورد انتظار را بدون تغییر صادر کرد | Not Run | | | |

## 4) انتقال و چرخهٔ Customer

| گام | نتیجه (Pass/Fail/Not Run) | شاهد/مسیر فایل | زمان | توضیح |
|---|---|---|---|---|
| SPKI عمومی Vendor از مسیر مستقل در Trust Store Customer ثبت شد | Not Run | | | |
| Migration مشتری پس از Backup و مجوز مالک اجرا شد | Not Run | | | |
| هویت Installation ثبت شد | Not Run | | | |
| CompanyReference به Company محلی به‌صورت معتبر نگاشت شد | Not Run | | | |
| پاکت امضاشده بدون ویرایش منتقل شد | Not Run | | | |
| Import موفق و ثبت پایدار انجام شد | Not Run | | | |
| Import تکراری idempotent بود | Not Run | | | |
| `GET /api/v1/licensing/status` وضعیت مورد انتظار را برگرداند | Not Run | | | |
| پس از Restart سرویس Customer، نیاز به import مجدد نبود | Not Run | | | |

## 5) آزمون‌های منفی و مرزهای خارج از دامنه

| سناریو | انتظار | نتیجه (Pass/Fail/Not Run) | شاهد/مسیر فایل |
|---|---|---|---|
| دستکاری Payload یا Signature | رد fail-closed | Not Run | |
| KeyId ناشناخته در Trust Store | رد fail-closed | Not Run | |
| Installation ناسازگار | رد fail-closed | Not Run | |
| پاکت منقضی یا replay | رد/بی‌اثر طبق قرارداد | Not Run | |
| حذف کلید یا سلب ACL هویت سرویس Vendor | خطای امن؛ بدون fallback | Not Run | |
| Restart Customer با state خراب | fail-closed | Not Run | |

موارد زیر در این اجرای محدود **اثبات نمی‌شوند** و باید جداگانه برنامه‌ریزی شوند:

- استقلال دو میزبان Windows Server و آزمون شبکه‌ای Vendor↔Customer؛
- توزیع، rotation، overlap و recovery کلیدهای Trust در دو میزبان (#235)؛
- Bootstrap مستقل TenantOwner برای Dedicated/Offline طبق D-110؛
- آزمون زوجی Company A/B و enforcement عملیاتی شرکت‌محور (#391/#392)؛
- ACK/Applied tracking (#393/#394)؛
- sink مستقل Audit و recovery/retention/alerting محیط Production (#107)؛
- فعال‌سازی Runtime Gate یا مجوز اجرای عملیات Trade.

## 6) نتیجه و تأیید

- **نتیجهٔ چرخه:** Not Run
- **موارد Pass:** 
- **موارد Fail:** 
- **موارد Blocked/Not Run و علت:** 
- **اقدام اصلاحی / Issue مرتبط:** 
- **تأیید مسئول اجرا و تاریخ:** 
- **تأیید مالک و تاریخ:** 

## 7) قواعد نگهداری شواهد

- فقط خلاصهٔ خروجی، timestamp، نسخه/commit و مسیر فایل شاهد ثبت شود؛ رمز، private key، connection string، token، SPKI خصوصی یا دادهٔ حساس مشتری وارد این سند/commit نشود.
- فایل‌های شاهد در محل امن و با دسترسی محدود نگهداری شوند؛ این سند فقط شناسه/مسیر امن آن‌ها را ثبت کند.
- وضعیت هر مورد فقط پس از اجرای واقعی و بازبینی شاهد از `Not Run` تغییر کند.
- موفقیت CI یا تست روی یک میزبان، جایگزین اجرای عملیاتی و آزمون دو میزبان نیست.
