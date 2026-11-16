# تحویل وظیفه — ISSUE-696-s1-0-vendor-licensing-docs

- **Issue:** [#696](https://github.com/hamidreza1394/Accounting/issues/696) — S1-0: ممیزی و هم‌ترازی مستندات Vendor/Licensing
- **Agent/مسئول:** GLM (z.ai agent) به دستور مالک محصول
- **تاریخ:** 2026-11-16
- **مبنا:** `main@947901bb586b6fccf2d14e432a666695ffad6e80`
- **وضعیت:** انجام‌شده (فقط مستندات)؛ PR باز و merge نمی‌شود.

## هدف و معیارهای پذیرش
- [x] خواندن کامل `AGENTS.md`، اسناد اصلی و هر شش سند LICENSING*، VENDOR_BOOTSTRAP و handoffهای #107/#214/#228/#236/#392/#393.
- [x] تأیید وضعیت واقعی PRهای #679..#695 از GitHub و ممیزی کد (گیت v5، مصرف‌کنندهٔ مشتری، builderهای Vendor، `DenyAllTradeModuleEntitlement`).
- [x] ثبت تصمیم‌های مالک محصول در شش ADR جدید D-097..D-102 با شماره‌گذاری واقعی (پس از D-096) و تفکیک Accepted از Proposed/NeedsApproval.
- [x] ثبت پرسش‌های باز در OD-20..OD-26 بدون بازکردن دوبارهٔ تصمیم‌های گرفته‌شده.
- [x] رفع تناقض‌های اسناد (AGENT_COORDINATION بند ۰، INDEX، LICENSING.md، WORKFLOWS §۱۲، ROADMAP، PROJECT_STATUS) بدون بازنویسی سابقه.
- [x] به‌روزرسانی LICENSING.md با وضعیت v2/v5/Gate و تفاوت Perpetual/Term/Support/Update.
- [x] گردش‌کارهای S1 (reactivation Online/Offline، Update با تأیید، پایان Support/License، انقضای Cloud، recovery) و تهدیدهای T53..T59 با حفظ محدودیت‌های آفلاین.
- [x] مدل مفهومی S1 در DATA_MODEL با مالکیت داده Vendor DB/Customer DB؛ بدون ساخت Entity/Migration.
- [x] نقشه اجرایی S1 با وابستگی‌های واقعی (#228/#235/#236/#392/#393/#394/#396) و ثبت مالکیت حوزه‌های فنی و قواعد انتخاب عامل اجرایی (بدون شروع کدنویسی موازی).
- [x] گیت v5 باز نشد؛ `CustomerConsumerReady=false` و تنظیم غیرفعال دست‌نخورده ماند.
- [x] WORK_LOG افزایشی + این handoff طبق TEMPLATE؛ سابقهٔ قبلی حذف/بازنویسی نشد.

## خلاصه تغییرات
- `docs/DECISIONS.md` — افزودن D-097..D-102 (تصمیم‌های مالک S1-0؛ همه `Accepted` برای رفتار محصول، `NotImplemented`).
- `docs/LICENSING_OPEN_DECISIONS.md` — OD-20..OD-26 با گزینه‌ها، ریسک و پیشنهاد؛ همه `Proposed`/`NeedsApproval`.
- `docs/LICENSING.md` — header وضعیت (2026-11-16، مبنا `947901bb`)، رفع تناقض داخلی v5، خط جدول `Licensing.Contracts`، بخش «تصمیم‌های S1-0 و تفاوت Perpetual/Term/Support/Update».
- `docs/LICENSING_WORKFLOWS.md` — اصلاح §۱۲ بند ۷/۸ (وضعیت #684/#686/#687) + بخش ۱۶ گردش‌کارهای S1.
- `docs/LICENSING_DATA_MODEL.md` — بخش ۱۱: موجودیت‌های پیشنهادی S1 با مالکیت Vendor-only/Customer-only و قواعد داده.
- `docs/LICENSING_THREAT_MODEL.md` — T53..T59 (reactivation abuse، fingerprint، سرقت کلید، clone VM، replay، Update bypass، اطلاعیه) + حفظ محدودیت‌های آفلاین.
- `docs/LICENSING_IMPLEMENTATION_PLAN.md` — بخش «نقشه اجرایی S1» (وضعیت ورودی، برش‌های S1-a..S1-g، ترتیب، مالکیت حوزه‌های فنی و قواعد انتخاب عامل اجرایی و قرارداد مشترک).
- `docs/ROADMAP.md` — هم‌ترازی چک‌لیست D-059 ([x]/[~]/[ ] با شاهد)، جدول وابستگی‌ها و بخش S1.
- `docs/PROJECT_STATUS.md` — بخش وضعیت جاری جدید (supersede فقط وضعیت #684/#686/#687؛ سابقه حفظ شده).
- `docs/AGENT_COORDINATION.md` — اصلاح بند ۰ (شماره ADR و وضعیت لایسنس)؛ مالکیت فایل/Branch/PR حفظ شد.
- `docs/INDEX.md` — بازهٔ ADRها و لینک handoff جدید.
- `docs/WORK_LOG.md` — ورود افزایشی این مأموریت.

## فایل‌های مهم تغییرکرده
- دوازده فایل بالا؛ هیچ فایل کد، workflow، تنظیم یا Migration تغییر نکرده است.

## تصمیم‌های فنی
- تصمیم‌های مالک عیناً و کامل ثبت شدند؛ موارد مبهم یا ناقص به OD-20..OD-26 با پیشنهاد موکول شد و هیچ موردی خودسرانه Accepted نشد.
- تفاوت سیاست انقضای Cloud (D-098) با Restricted نصب Dedicated/Offline (D-026/D-059) به‌صورت صریح حل تعارض شد؛ هیچ ADR قبلی بازنویسی نشد.
- گردش‌کار reactivation به همان قرارداد نسخه‌دار و هویت D-058/#384 متصل است؛ هیچ سیستم هویت، CRM سنگین، Ticketing یا transport موازی طراحی نشد.
- مالکیت حوزه‌های فنی (Vendor-side/Customer-side) ثبت شده و انتخاب عامل اجرایی هر مأموریت در صلاحیت مالک پروژه است؛ قرارداد مشترک باید پیش از پیاده‌سازی یک‌بار تثبیت شود.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `git diff --check` | موفق | بدون trailing whitespace |
| بازبینی کامل diff | انجام شد | ۱۲ فایل، +305/−20؛ فقط docs/ |
| `dotnet build` / `dotnet test` | اجرا نشده | تغییر اجرایی وجود ندارد؛ طبق AGENTS.md «اجراشده» گزارش نمی‌شود |
| `python3 scripts/check_static_assets.py` | اجرا نشده | بی‌ارتباط با تغییر مستندات |
| بازخوانی API وضعیت PR/Issue و ممیزی کد (`git grep`) | انجام شد | شواهد در متن سندها با SHA ثبت شد |

## امنیت، داده و سازگاری
- هیچ کلید، secret، connection string یا credential در مستندات نیست؛ وضعیت fail-closed signer و Gate v5 بدون تغییر توصیف شده است.
- هیچ Migration/Schema/Customer DB پیشنهاد اجرایی نشده؛ بخش ۱۱ DATA_MODEL صراحتاً Proposed است.
- سازگاری با D-022، D-026، D-045، D-046، D-053، D-057، D-058، D-059، D-060، D-070، D-074، D-087، D-089 بررسی و ارجاع داده شد؛ هیچ LicenseEnvelope/Installation model/CRM/transport موازی معرفی نشد.

## ریسک‌ها و کارهای باقی‌مانده
- بازکردن Gate v5، اجرای Migration مشتری روی دیتابیس واقعی، گسترش allow-list ACK سمت Vendor و سیاست Restore/Clone نیازمند تصمیم/PRهای مستقل مالک‌اند.
- OD-20..OD-26 باز هستند و پیاده‌سازی برش‌های S1 به تأییدهای آن‌ها وابسته است؛ قرارداد مشترک بین‌بخشی (Vendor/Customer) هنوز تثبیت نشده است.
- ~~عبارت منسوخ «شماره بعدی D-087» در بخش 5.1 `AGENTS.md`~~ — در مأموریت تکمیلی همان PR رفع شد (افزودهٔ پایین).

## افزودهٔ مأموریت تکمیلی پیش از Merge (2026-11-16؛ تصمیم مالک پروژه)

پیش از Merge، مالک پروژه دو اصلاح را تعیین کرد که در همین PR اعمال شد:

1. **تفکیک مالکیت فنی از انتخاب عامل اجرایی:** ثبت اولیهٔ «تقسیم GLM/Qwen» اصلاح شد — هیچ زیرسیستم یا بخشی از پروژه منحصراً یا دائماً به GLM، Claude، ChatGPT یا عامل دیگری واگذار نشده است؛ اسناد فقط «مالکیت حوزه فنی و قراردادهای بین‌بخشی» (Vendor-side/Customer-side) را ثبت می‌کنند. انتخاب عامل اجرایی هر مأموریت با مالک پروژه و بر اساس نوع کار، پیچیدگی، کیفیت، توانایی ابزار، هزینه و محدودیت مصرف است؛ GLM فقط عامل اجرایی جاری مأموریت‌های Vendor است و این انحصار آینده نیست. محل‌های ثبت: `LICENSING_IMPLEMENTATION_PLAN.md` (بخش جایگزین‌شده)، `AGENT_COORDINATION.md` (قاعدهٔ جدید ۱۱)، `ROADMAP.md`، `INDEX.md` و همین handoff. «Agent/مسئول» در سربرگ handoffها صرفاً سابقهٔ اجرای همان مأموریت است، نه ادعای مالکیت دائمی.
2. **AGENTS.md §5.1:** عدد ثابت «اکنون D-087» حذف شد؛ شماره ADR بعدی فقط از انتهای واقعی `docs/DECISIONS.md` خوانده می‌شود و این سند عمداً شمارهٔ جاری را ذخیره نمی‌کند. عبارت وضعیت Licensing در §5.2 پیش‌تر اصلاح شده بود و با تغییر جدید تعارض ندارد.

## شناسه‌های Git
- Branch: `agent/696-s1-0-vendor-licensing-docs` (از `main@947901bb`)
- Commit: SHA نهایی از head همان PR خوانده می‌شود.
- Pull Request: مستنداتی؛ وضعیت ادغام و SHA نهایی در PR #697 و تاریخچه Git ثبت می‌شود.

## بازبینی مستقل پیش از ادغام (2026-11-16)

بازبینی CodeRabbit پنج نکتهٔ معتبر را مشخص کرد و در همین PR اصلاح شدند: گزینه‌ها و موازنهٔ شش ADR؛ واحد شمارش کاربران هم‌زمان و مشروط‌بودن شمارش Session به OD-26؛ مرز اعتماد بازیابی کلید گمشده (OD-21)؛ تقدم D-099 بر پیشنهاد OD-06(a) برای تغییر سخت‌افزاری مهم؛ و ثبت واقعی شکست `build-and-test` در SHA ادغام #687. همچنین متن‌های جاری وضعیت merge با تاریخچهٔ PR هماهنگ شدند. هیچ کد اجرایی، Migration یا تنظیم Runtime تغییر نکرد.
