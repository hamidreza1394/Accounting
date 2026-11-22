# Amendment Delivery / ACK Tracking (#393/#394) — تحلیل قرارداد و گزینه‌ها — `Proposed`

> **وضعیت سند:** خروجی S-B مأموریت. تحلیل وضعیت واقعی، مقایسهٔ گزینه‌ها، پیشنهاد حداقلی و سؤالات مالک. **هیچ قرارداد بین‌ماژولی تعریف یا فعال نشده و هیچ ACK فرضی/غیرامضاشده‌ای مسیر عملیاتی ندارد؛** پیاده‌سازی هر بخش به تأیید قرارداد مالک و هماهنگی با #235 وابسته است. مرجع گردش‌کار: `docs/LICENSING_WORKFLOWS.md` §۱۷.۳/۱۷.۴ (وضعیت: `Accepted/NotImplemented`) و تصمیم‌های D-054/D-059/D-060.

## ۱) رفتار فعلی Amendment Delivery (main@`58a1ccd4`)

- **صدور:** `VendorLicenseAmendmentRequest` با وضعیت‌های `Requested/PendingApproval/Issued/Rejected/Failed` و انواع `maxCompanies`/`supportUpdates` (`Vendor/Vendor.Persistence/Domain/VendorLicenseAmendmentRequest.cs:13-20`). صدور = ساخت پاکت امضاشده با Gate سیاستی `VendorAmendmentEntitlementPolicy` (شامل رد fail-closed هر تغییر FormatVersion بدون نگاشت مصوب — PR #753/commit `6490214e`) و persistence اتمیک.
- **تحویل:** هیچ endpoint صادرات/تحویل اختصاصی amendment وجود ندارد؛ تحویل امروز فقط مبادلهٔ دستی فایل طبق §۱۷.۳ است (پاکت امضاشدهٔ همان کلید لایسنس با sequence بزرگ‌تر، bind همان Installation). سمت مشتری پذیرش revision معتبرتر از همان Installation در `CustomerLicenseImportService` پیاده شده است (رد replay/downgrade با کد `license_sequence_replay_or_downgrade`).
- **ثبت ارسال/تحویل/ACK/Retry/Failure:** برای amendment **هیچ‌کدام وجود ندارد** — نه رکورد تحویل، نه وضعیت `Delivered/Acknowledged` روی درخواست، نه intake ACK.
- **الگوی مرجع موجود (provisioning):** intake ACK امضاشدهٔ machine-to-machine در `POST /api/vendor/provisioning-requests/{requestId}/acknowledgement` (`VendorProvisioningEndpoints.cs:38`) با پاکت `CustomerProvisioningAcknowledgementEnvelope` (محدودیت طول AcknowledgementJson، کد `provisioning_acknowledgement_envelope_invalid`)، اعتبارسنجی با trust store امضای مشتری (`ProvisioningAcknowledgementTrustStore` سمت Vendor + `CustomerProvisioningAcknowledgementTrustStore` سمت مشتری)، رفتار idempotent retry/conflict/fail-closed، allow-list چهار کد رد ثابت (#699)، ثبت رکورد در جدول `VendorProvisioningAcknowledgements` (Migration `20261029130000`) و مهر زمان اتصال Installation. تست‌های واحد + SQL Server در CI سبز هستند. **این الگو، پایهٔ طبیعی ACK amendment است.**

## ۲) مقایسهٔ گزینه‌های طراحی قرارداد ACK

معیارهای الزامی مأموریت برای هر گزینه: ACK امضاشدهٔ مصرف‌کننده، شناسهٔ یکتا و ضد Replay، اتصال به نسخه/hash پیام اصلی، زمان و هویت تأییدکننده، رفتار ACK تکراری/نامعتبر، Retry و Idempotency.

| گزینه | شرح | امتیاز | هزینه/ریسک |
|---|---|---|---|
| **A — توسعهٔ الگوی ACK موجود به amendment** (پیشنهاد حداقلی) | همان پاکت امضاشدهٔ مشتری + trust store + validator، با kind جدید (`license-amendment`) که به جای requestId بستهٔ provisioning به شناسهٔ پاکت amendment (envelope id + payload hash + sequence + licenseId) bind است؛ intake جدید یا گروه مشترک با همان قواعد idempotency | بیشترین بازاستفاده؛ یک قرارداد امنیتی اثبات‌شده؛ بدون broker؛ هم‌راستا با §۱۷.۴ («Vendor sequence و request ID را idempotently ثبت می‌کند») | نیازمند endpoint/رکورد ذخیرهٔ ACK amendment (جدول یا وضعیت‌های جدید روی درخواست) → **Schema/API جدید = نیازمند تأیید صریح مالک**؛ وابسته به تکمیل trust model مشتری در #235 |
| B — قرارداد «رسید امضاشدهٔ عمومی» برای همهٔ اسناد (provisioning، amendment، update D-101) | یک قرارداد kind-based واحد | مرز واحد و آینده‌نگر | طراحی بیشتر، تأخیر S-B؛ بدون نیاز فوری اثبات‌شده (YAGNI تا D-101 پیاده شود) |
| C — Polling/وضعیت‌کشی مشتری از Vendor | مشتری وضعیت amendment را می‌پرسد | ساده | اثبات تحویل/پذیرش امضاشده نمی‌دهد؛ با مدل Offline سازگار نیست؛ خلاف §۱۷.۴ |
| D — Message Broker/صف پیام قابل‌اعتماد | زیرساخت توزیع پیام | — | **رد** — زیرساخت جدید بدون ضرورت، خلاف YAGNI و مرز استقرار فعلی |

## ۳) تقسیم مسئولیت (پیشنهادی؛ مشروط به تأیید)

| مسئولیت | مالک | مبنا |
|---|---|---|
| رکورد تحویل (export هر پاکت amendment) و وضعیت‌های `Delivered/Acknowledged` | Vendor | ثبت shipment/ack در درخواست amendment؛ بدون حذف داده در رد |
| صدور ACK امضاشده با کلید acknowledgement مشتری (عدم ارسال کلید خصوصی) | Consumer | همان الگوی provider موجود (`UnavailableCustomerProvisioningAcknowledgementSigningProvider` پیش‌فرض fail-closed) |
| اعتبارسنجی امضا/trust/طول/ساختار و رد fail-closed | Vendor | trust store + validator موجود |
| Idempotency: ثبت idempotent بر (envelopeId، sequence، requestId ACK)؛ ACK تکراری → همان پاسخ قبلی (retry-safe)؛ ACK ناسازگار (hash/sequence متفاوت) → `Conflict` صریح | Vendor | الگوی رفتار retry/conflict موجود provisioning |
| جلوگیری از replay: nonce/زمان در پاکت ACK + bind به hash پیام اصلی؛ پاکت منقضی/تکراری رد | هر دو | §۱۷.۴ و الگوی T49 |
| توزیع/چرخش کلیدهای امضای ACK مشتری و دو میزبان Staging | Consumer side / زیرساخت | **#235** (trust distribution/rotation) |
| freshness (نمایش زمان آخرین sync) | Vendor | §۱۷.۴ |

## ۴) وابستگی‌های دقیق خارج از این سند

1. **#235 — trust distribution/rotation:** قرارداد ACK amendment تا تثبیت مدل توزیع/چرخش کلیدهای امضای مشتری نباید نهایی شود؛ تست دو میزبان Staging پیش‌شرط اعتبارسنجی end-to-end است.
2. **#394 — انتقال فایل:** نسخه‌دار بودن درخواست/پاسخ reactivation و ACK به همان قرارداد انتقال فایل وصل است (D-099 P1).
3. **#392/#228 — v5 gate و mapping مشتری:** amendmentهای شرکت‌محور فقط پس از باز شدن Gate دوکلیدی عملیاتی می‌شوند؛ ACK بدون صدور عملیاتی بی‌مصرف است.
4. **Schema/API:** هر جدول ACK جدید یا تغییر وضعیت‌های `VendorLicenseAmendmentRequest` نیازمند Issue مستقل + ADR/مجوز صریح مالک است (قاعدهٔ مخزن برای Migration).

## ۵) پیشنهاد حداقلی (همهٔ موارد `Proposed`)

- گزینهٔ A: بازاستفادهٔ کامل از الگوی ACK provisioning با kind `license-amendment`؛ بدون broker، بدون کانال موازی، بدون ACK غیرامضاشده.
- ثبت تحویل export و دو وضعیت جدید روی درخواست amendment فقط پس از تأیید (تغییر وضعیت = تصمیم قراردادی، نه صرفاً فنی).
- تست‌های لازم پس از تصویب: واحد (validator/replay/idempotency)، SQL Server در CI، HTTP قراردادی برای ACK تکراری/ناسازگار، رگرسیون fail-closed (ACK نامعتبر هرگز وضعیت را جلو نبرد).

## ۶) سؤالات نیازمند تصمیم مالک

1. گزینهٔ A به‌عنوان مبنا تأیید می‌شود؟ (یا B برای پوشش D-101 آینده؟)
2. ذخیرهٔ ACK amendment: جدول مستقل یا افزودن وضعیت/رکورد به درخواست amendment؟ (اثر Schema/Migration)
3. آیا کلید امضای ACK مشتری همان کلید provisioning ACK می‌ماند یا کلید/دورهٔ جدا؟ (وابسته به #235)
4. سیاست retention و audit رکوردهای ACK amendment چه باشد؟ (هم‌راستای D-070)
