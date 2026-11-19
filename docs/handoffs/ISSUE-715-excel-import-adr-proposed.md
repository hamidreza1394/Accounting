# تحویل وظیفه — ISSUE-715-excel-import-adr-proposed

- **Issue:** #715
- **Agent/مسئول:** documentation-engineer (Claude Code)
- **تاریخ:** 2026-11-19
- **وضعیت:** انجام‌شده (فقط مستندات؛ همهٔ تصمیم‌ها Proposed/NeedsApproval/Deferred)

## هدف و معیارهای پذیرش
- [x] ثبت ۱۵ توافق مالک در ADR جدید به‌صورت Proposed (نه Accepted).
- [x] سند طراحی کامل با شناسه و وضعیت برای هر تصمیم (EXC-1..EXC-17).
- [x] مقایسهٔ کتابخانه با برچسب شواهد.
- [x] فقط D-103 بند ۹، D-105 بند ۸ و D-082 به‌عنوان Accepted ارجاع شده‌اند.
- [x] بدون هیچ تغییر کد/Schema/Migration/Package/csproj/props/ClientApp.
- [x] WORK_LOG فقط افزوده شد.

## خلاصه تغییرات
- ADR جدید D-106 (تصمیم Proposed، پیاده‌سازی NotImplemented) به انتهای `docs/DECISIONS.md`؛ سربرگ فایل (شمارهٔ آخرین ADR و تاریخ بازبینی) اصلاح شد (آخرین ADR قبلی D-105 بود).
- دو سند جدید طراحی و مقایسهٔ کتابخانه.
- ارجاع در `docs/INDEX.md` و ورودی Append-only در `docs/WORK_LOG.md`.

## فایل‌های مهم تغییرکرده
- `docs/DECISIONS.md` — D-106 و اصلاح سربرگ (۲ خط سربرگ تغییر کرد؛ متن هیچ ADR مصوب تغییر نکرد).
- `docs/EXCEL_IMPORT_DESIGN.md` — جدید؛ طراحی کامل، تعارض‌ها، تصمیم‌های باز، PRهای پیشنهادی.
- `docs/EXCEL_IMPORT_LIBRARY_COMPARISON.md` — جدید؛ جدول مقایسه، شواهد Spike، نتیجهٔ پیشنهادی.
- `docs/INDEX.md` — دو ردیف جدید و به‌روزرسانی ردیف DECISIONS.
- `docs/WORK_LOG.md` — ورودی انتهایی (۹ خط افزوده، ۰ خط حذف).
- این handoff.

## تصمیم‌های فنی
- تصمیم معماری `Accepted` جدیدی گرفته نشد. D-106 و EXC-1..EXC-17 `Proposed`/`NeedsApproval`/`Deferred` هستند؛ بقیه موارد مالک/Lead تأیید می‌کنند.
- شمارهٔ D-106 از انتهای `docs/DECISIONS.md` خوانده شد (آخرین: D-105).

## تعارض‌ها و شکاف‌ها با اسناد (حل‌نشده؛ تفصیل در `EXCEL_IMPORT_DESIGN.md` بخش ۱۹)
- `docs/TRADE_DESIGN_V3_2.md` خطوط 633 و 645: موجودیت‌های Trade-محور `ImportBatch/ImportRow/ImportValidationError` و مجوز `trade.form.excelimport.*` در برابر موتور خنثی؛ بدون موجودیت Mapping. خط 643: گردش بدون مرحلهٔ Mapping.
- `docs/TRADE_S3_READINESS.md` خط 57: «Excel Import ... بعد از S3».
- Combobox: `docs/DECISIONS.md` D-082 و `docs/UI_COMPONENTS.md` خط 242 آن را `NotImplemented` می‌نویسند، ولی `ClientApp/src/components/ui/SearchableCombobox.tsx` (+ تست) وجود دارد و در `ClientApp/src/features/trade/components/PartyEditor.tsx` و `ProductEditor.tsx` استفاده شده است. اصلاح نشد (خارج از محدوده)؛ پیشنهاد: Issue مستندسازی جدا.
- D-105: مالکیت Party/Product و اشتراک هنوز تصمیم مستقل می‌خواهد.

## اصلاحات پس از بازبینی مستقل (architect و security-reviewer، 2026-11-19)
اعمال‌شده در `EXCEL_IMPORT_DESIGN.md`، `EXCEL_IMPORT_LIBRARY_COMPARISON.md` و D-106 (هیچ وضعیت Accepted جدیدی؛ هیچ محل ذخیره‌ای پیشنهاد نشد):
- **H-1:** بند ذخیره‌سازی Mapping بازنویسی شد: هیچ ذخیرهٔ ماندگاری (جدول/schema trade/localStorage/sessionStorage/فایل)؛ هر Import از مرحلهٔ پیشنهاد Mapping؛ توافق‌های ۵، ۷، ۸، ۹ تا رفع EXC-8 اجرا نمی‌شوند؛ وابستگی EXC-9 به EXC-8 در جدول EXC صریح شد. (در طراحی، مرحلهٔ پیشنهاد Mapping «گام ۶» جریان است؛ ارجاع «گام ۵» بازبین به همان مرحله تعبیر شد.)
- **M-1:** کلید جست‌وجو فقط (Company سرور، Target Schema+نسخه، امضای Template/Sheet)؛ اثر انگشت سرستون‌ها ویژگی ذخیره‌شده؛ EXC-8/EXC-9 هم‌خوان شدند.
- **M-2:** سه پرسش باز (TenantId+CompanyId طبق D-105 بند ۱؛ دادهٔ کسب‌وکاری یا پیکربندی فنی طبق بند ۷؛ DbContext/schema + منع FK خطوط 996 و 1068 + D-090) افزوده شد.
- **M-3:** EXC-2 نقد شد (ب با توافق ۱۵ ناسازگار؛ الف با شرط‌ها؛ YAGNI؛ استثنای ARCHITECTURE خط 173 فقط ارجاع)؛ محل Parser پرسش باز.
- **M-4/F1/F2:** EXC-12 تکمیل و بخش امنیت Preview→Commit افزوده شد؛ وابستگی D-085 در تعارض ۱۱.
- **M-5/F13-F16:** قاعدهٔ ارقام، Allowlist صفرپر، مبلغ `decimal`، دو پرسش مالک (پرچم رقم کنترل؛ ارقام فارسی).
- **M-6/M-7:** منبع «الزام Sidebar» و منبع/برچسب افزودهٔ Lead در توافق‌های ۴ و ۱۲.
- **M-8/F5/F20:** برچسب Spike، پیوست کد بازتولید (بدون افزودن فایل به مخزن)، برچسب EPPlus، اصلاح ادعای آسیب‌پذیری.
- **F3-F12، F17:** ترتیب کنترل، Rate Limit، سقف‌های حین خواندن، تشخیص نوع بر محتوا، نام فایل/Log/Bidi، فایل موقت/ضدویروس، Formula Injection، پیش‌شرط view، AND بودن مجوز import، اصلاح ارجاع D-084، `date1904` و پنهان‌ها.
- **Low:** L-1، L-2 (خط 50 + AGENTS.md بخش ۵.۳)، L-4 (نام `data-import`)، L-5 (هشدار هر سلول فرمول‌دار)، L-7.

**شواهد بازبین که با فایل واقعی سنجیده شد (درست بود):** `ARCHITECTURE.md` خط 173؛ `DECISIONS.md` خطوط 996 و 1068؛ `TRADE_S3_READINESS.md` خط 50؛ `RateLimitingPolicies.cs` (سیاست‌های `api` و `api-sensitive`)؛ D-074 بندهای ۲ و ۳؛ D-085 (تصمیم Proposed+NeedsApproval، پیاده‌سازی Implemented با PR #644)؛ `TRADE_IMPORT` در `ARCHITECTURE.md` خط 180.
**مورد نادرست یافت نشد.** دو نکتهٔ سنجش‌شده: در `Server/**/*.cs` با جست‌وجوی متنی `ConcurrencyLimiter` و تنظیم سقف بدنه (`MaxRequestBodySize`/`RequestSizeLimit`) یافت نشد (بازبینی کامل پیکربندی انجام نشد).
**اعمال/راستی‌آزمایی نشد:** متن اصلی مأموریت مالک؛ اعداد Spike و NuGet؛ تداخل تراکنش Commit با UseCaseهای موجود؛ تکرار Spike.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `dotnet build ...` / `dotnet test ...` | اجرا نشد | فقط مستندات؛ Build/Test کد اجرا نشد |
| `git diff --check` | موفق | کد خروج 0 (روی تغییرات پیگیری‌شده؛ فایل‌های جدید untracked پیش از stage) |
| `python3 scripts/check_static_assets.py` | موفق | `PASS: no remote CDN asset references found ...`؛ کد خروج 0 |

شواهد Spike و NuGet در سند مقایسه از گزارش Lead نقل شده‌اند (راستی‌آزمایی‌شده توسط Lead)؛ این Agent دوباره اجرا نکرد.

## امنیت، داده و سازگاری
- هیچ اثر روی کد، امنیت، احراز هویت، مجوز، داده یا Migration. هیچ دادهٔ/فایل واقعی مشتری در مستندات نیست؛ مثال‌ها ساختگی‌اند.
- طراحی بر D-103 بند ۹ و D-105 بند ۸ (Scope از Context سرور، نه فایل) استوار است.

## ریسک‌ها و کارهای باقی‌مانده
- تأیید مالک: EXC-2، 5، 8، 12، 13 (اعداد)، 14، 17.
- Persistence Mapping Blocked تا S3-0 و تصمیم مالکیت؛ تست دستی UI تا #401/تصمیم امنیتی Blocked.
- بازبینی تخصصی Diff پیشنهاد می‌شود: `architect` (EXC-2/محل موتور و تعارض‌ها) و `security-reviewer` (EXC-13).
- اعداد حافظه/زمان تک‌اجرایی‌اند؛ Benchmark رسمی `NotRun`. متن مجوز Sylvan.Data.Excel و `OSMFEULA.txt` NPOI خوانده نشده؛ تاریخ انتشار EPPlus نامشخص.
- بازبینی PR/CI: پس از ایجاد PR توسط Lead.

## شناسه‌های Git
- Branch: docs/excel-import-adr-proposed (مبنا main@6f539679)
- Commit: ایجاد نشده (Lead انجام می‌دهد)
- Pull Request: ایجاد نشده
