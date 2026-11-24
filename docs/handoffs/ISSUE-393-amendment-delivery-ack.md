# تحویل وظیفه — ISSUE-393-amendment-delivery-ack

- **Issue:** [#393](https://github.com/hamidreza1394/Accounting/issues/393) (برش Amendment Delivery / Signed ACK / Applied Tracking؛ گزینهٔ A مصوب مالک در مأموریت اجرایی 2026-11-24)
- **Agent/مسئول:** GLM (z.ai)
- **تاریخ:** 2026-11-24
- **وضعیت:** قرارداد و بستهٔ تصمیم کامل؛ **پیاده‌سازی در نقطهٔ توقف Schema متوقف است** (محدودیت غیرقابل‌مذاکرهٔ مأموریت: هیچ Migration بدون تأیید صریح مالک)

## هدف و معیارهای پذیرش
- [x] مرحله ۱ — جدول وضعیت واقعی هر قطعهٔ چرخه (صدور/تحویل/ACK/Applied) با مرجع فایل
- [x] مرحله ۲ — ثبت قرارداد اجرایی Delivery و ACK: State Machine، Bindings، امضا/Trust، idempotency، رفتار Timeout/تکراری/ناسازگار، مرز Vendor/Customer و برنامهٔ تست
- [ ] مراحل ۳ تا ۹ (Vendor Delivery، Customer Apply+ACK، Vendor intake، Persistence، تست، مستندات کد، Branch/PR کد) — **مسدود به تأیید تغییر حداقلی Schema**
- [x] توقف الزامی: نیاز واقعی به Migration مستند شد (بستهٔ تصمیم با شواهد file:line) و هیچ Migration/ستون/کد intake ساخته نشد
- [x] هیچ ACK فرضی/غیرامضاشده‌ای وارد مسیر عملیاتی نشد و هیچ کد/قرارداد مشترک تغییر نکرد

## خلاصه تغییرات
- ممیزی واقعی `main@35bd3905`: قرارداد ACK مشترک، intake provisioning، trust store، سرویس صدور amendment، entity/Migration درخواست amendment و مسیر idempotent import مشتری خوانده و تطبیق داده شد.
- **یافتهٔ مسدودکننده:** CHECK constraint `CK_VendorLicenseAmendmentRequests_Status` فقط پنج وضعیت فعلی را می‌پذیرد و ردیف `VendorLicenseAmendmentRequest` هیچ ستونی از الگوی `VendorProvisioningRequest` (Nonce/Delivered/Ack*) ندارد؛ بدون Migration، State Machine `PendingDelivery/Delivered/Applied` و ثبت idempotent ACK غیرقابل پیاده‌سازی است.
- تحویل فقط مستنداتی: قرارداد اجرایی + بستهٔ تصمیم تغییر حداقلی (۸ ستون nullable + گسترش CHECK) + handoff؛ بدون هیچ کد تولیدی.

## فایل‌های مهم تغییرکرده
- `docs/VENDOR_AMENDMENT_DELIVERY_ACK_CONTRACT.md` — جدید؛ قرارداد اجرایی Delivery/ACK
- `docs/DECISION_PACKAGE_AMENDMENT_DELIVERY_TRACKING.md` — جدید؛ بستهٔ تصمیم Schema حداقلی
- `docs/INDEX.md` — سه ردیف ارجاع
- `docs/WORK_LOG.md` — ورودی append-only
- این handoff

## تصمیم‌های فنی
- بازاستفادهٔ بدون تغییر از `CustomerProvisioningAcknowledgement(Envelope/Validator)`، trust store و الگوی intake provisioning؛ کدهای خطای validator با prefix فعلی 1:1 نگاشت می‌شوند (تغییر prefix = تغییر قرارداد مشترک، خارج از محدوده).
- manifest تحویل غیرامضا (requestId+nonce+envelope) به‌جای افزودن فیلد به `LicensePayload`؛ manifest منبع اعتماد نیست و دستکاری آن فقط به شکست binding می‌انجامد.
- بازاستفاده از وضعیت `Rejected` برای رد مشتری با تمایز lifecycle/`FailureCode`؛ تصمیم معماری ADR جدیدی گرفته نشد (ثبت ADR پس از تأیید مالک با شمارهٔ بعدی واقعی از انتهای `docs/DECISIONS.md`).

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| ممیزی تطبیق ادعاها با کد (entityها، migration، endpointها، validator، import) | انجام شد | همهٔ ارجاع‌ها از `main@35bd3905` |
| `dotnet build/test` | اجرا نشده | تغییر کد صفر؛ sandbox بدون checkout/SDK محلی |
| `git diff --check` | اجرا نشده محلی | diff PR باید فقط افزودن اسناد و دو سطر INDEX/WORK_LOG را نشان دهد؛ CI روی head نهایی مرجع است |
| CI (Vendor/.NET/Semgrep) | در انتظار | پس از باز شدن PR روی head نهایی گزارش می‌شود |

## امنیت، داده و سازگاری
- هیچ تغییر Schema/Migration/تنظیم/gate امنیتی؛ هیچ secret/کلیدی در اسناد نیست؛ Runtime Gate خاموش می‌ماند و دست‌نخورده است.
- ACK بدون امضای مشتری و خارج از trust store هرگز معتبر تلقی نمی‌شود؛ نتیجهٔ ACK اول تغییرناپذیر است.
- هیچ تغییر Customer DB/`Server/**`/قرارداد مشترک؛ هیچ ادعای پیاده‌سازی کد ثبت نشده است.

## ریسک‌ها و کارهای باقی‌مانده
- مسیر اجرا کاملاً وابسته به تصویب بستهٔ تصمیم است؛ پس از تصویب، ترتیب پیاده‌سازی در §۵ بستهٔ تصمیم ثبت شده است.
- سؤالات باز: کلید ACK amendment (#235)، retention/audit، مجوز اپراتوری دو اقدام تحویل.

## شناسه‌های Git
- Branch: `feature/issue-393-amendment-delivery-ack` (از `main@35bd3905537023051b9372f01234cc4027590ab7`)
- Commit: پیاده‌سازی مستندات در شاخه؛ SHA نهایی پس از آخرین push ثبت می‌شود
- Pull Request: باز، فقط مستندات؛ Merge فقط با تأیید مالک
