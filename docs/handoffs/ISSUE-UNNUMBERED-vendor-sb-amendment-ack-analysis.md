# تحویل وظیفه — ISSUE-UNNUMBERED-vendor-sb-amendment-ack-analysis

- **Issue:** `UNNUMBERED`
- **Agent/مسئول:** GLM — Vendor/Licensing
- **تاریخ:** 2026-11-22
- **وضعیت:** انجام‌شده (فقط تحلیل و مستندات؛ کد/قرارداد تغییر نکرد)

## هدف و معیارهای پذیرش
- [x] ثبت رفتار واقعی فعلی Amendment Delivery و وضعیت ارسال/تحویل/ACK/Retry/Failure (صفر بودن آن‌ها برای amendment؛ الگوی مرجع provisioning ACK)
- [x] مقایسهٔ حداقل چهار گزینه با هر شش معیار مأموریت (ACK امضاشده، شناسهٔ یکتا/ضد Replay، bind به نسخه/hash، زمان+هویت، ACK تکراری/نامعتبر، Retry/Idempotency)
- [x] جدول تقسیم مسئولیت Vendor/Consumer و وابستگی دقیق به #235/#394/#392
- [x] پیشنهاد حداقلی بدون broker/زیرساخت جدید + چهار سؤال مشخص مالک
- [x] هیچ ACK فرضی یا غیرامضاشده‌ای وارد مسیر عملیاتی نشد
- [x] تکمیل در اصلاحیهٔ همین PR: بخش ۳.۱ «رفتار در Timeout، پیام تکراری و ACK ناشی از پیام اشتباه» — انتظار ACK با re-delivery امنِ همان بایت‌های پاکت (import idempotent موجود در `CustomerLicenseImportService.cs:155-164`)، ACK تکراری retry-safe، ACK ناسازگار/پیام اشتباه با `Conflict` صریح و bind چهارگانه؛ همه `Proposed` و بدون هیچ کد جدید

## خلاصه تغییرات
- سند جدید `docs/AMENDMENT_ACK_CONTRACT_ANALYSIS.md` با پیشنهاد گزینهٔ A (بازاستفاده از الگوی ACK امضاشدهٔ provisioning با kind جدید) به‌صورت `Proposed`.

## فایل‌های مهم تغییرکرده
- `docs/AMENDMENT_ACK_CONTRACT_ANALYSIS.md` — جدید
- `docs/handoffs/ISSUE-UNNUMBERED-vendor-sb-amendment-ack-analysis.md` — جدید

## تصمیم‌های فنی
- تصمیم قراردادی گرفته نشد؛ Schema/API و تغییر وضعیت‌های درخواست amendment صریحاً «نیازمند تأیید مالک» ثبت شد. اعتبار ACK فقط با امضای مشتری و trust store موجود تعریف می‌شود.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| تطبیق ادعاها با کد (entity statuses، endpoint ACK provisioning، trust store، import service) | انجام شد | همهٔ ارجاع‌ها از `main@58a1ccd4` |
| `dotnet build/test` | اجرا نشده | تغییر کد صفر؛ CI PR گزارش خودش را می‌دهد |
| `git diff --check` | موفق | صفر خطا |

## امنیت، داده و سازگاری
- هیچ تغییر امنیتی/داده؛ سند تصریح می‌کند ACK بدون امضای مشتری هرگز معتبر تلقی نمی‌شود و Schema/API جدید به مجوز مالک وابسته است.
