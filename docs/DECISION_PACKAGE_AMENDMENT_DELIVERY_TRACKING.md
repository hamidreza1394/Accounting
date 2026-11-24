# بستهٔ تصمیم مالک — تغییر حداقلی Schema برای Amendment Delivery / Signed ACK / Applied Tracking (#393)

> **وضعیت:** `Proposed` + `NeedsApproval`. این سند فقط نیاز و تغییر حداقلی را مستند می‌کند؛ **هیچ Migration ساخته یا اجرا نشده و هیچ کدی نوشته نشده است.** تا تأیید صریح مالک، پیاده‌سازی Stages 3..9 مأموریت اجرایی 2026-11-24 در نقطهٔ توقف این سند متوقف است. قرارداد رفتاری کامل در `docs/VENDOR_AMENDMENT_DELIVERY_ACK_CONTRACT.md` آمده است.

## ۱) زمینه

گزینهٔ A (بازاستفاده از الگوی ACK امضاشدهٔ provisioning روی جدول `VendorLicenseAmendmentRequests`) توسط مالک مصوب شده است. اجرای آن نیازمند ثبت وضعیت‌های `PendingDelivery/Delivered/Applied` و متادیتای تحویل/ACK روی ردیف درخواست است — دقیقاً همان الگویی که ردیف `VendorProvisioningRequest` امروز دارد. این یعنی یک Migration افزایشی صرفاً روی Vendor DB.

## ۲) چرا فیلدهای موجود کافی نیستند (شواهد)

1. **CHECK constraint وضعیت بسته است:** Migration `Vendor/Vendor.Persistence/Migrations/20261120135809_VendorLicenseAmendmentRequests.cs` قید `CK_VendorLicenseAmendmentRequests_Status` را با `[Status] IN (N'Requested', N'PendingApproval', N'Issued', N'Rejected', N'Failed')` تعریف می‌کند. درج `PendingDelivery`/`Delivered`/`Applied` در سطح دیتابیس رد می‌شود؛ پس State Machine مأموریت بدون تغییر این قید غیرممکن است.
2. **هیچ ستون تحویل/ACK وجود ندارد:** entity `Vendor/Vendor.Persistence/Domain/VendorLicenseAmendmentRequest.cs` فقط شناسه‌ها/نوع/وضعیت/`FailureCode`/timestamp/RowVersion دارد؛ در مقابل ردیف الگو `Vendor/Vendor.Persistence/Domain/VendorProvisioningRequest.cs` شامل `Nonce`، `DeliveredAtUtc`، `AcknowledgementSha256`، `AcknowledgementKeyId`، `AcknowledgementResult`، `AcknowledgedAtUtc` است. intake ACK provisioning (`Vendor/Vendor.Host/Authorization/VendorProvisioningEndpoints.cs`) دقیقاً روی همین ستون‌ها idempotency (مقایسهٔ `AcknowledgementSha256`) و conflict را پیاده می‌کند؛ transplant بدون این ستون‌ها ممکن نیست.
3. **پیوند request↔پاکت تحویل‌شده قابل اثبات نیست:** صدور فقط `license.LastIssuedSequence` را می‌نویسد (`Vendor/Vendor.Host/Signing/VendorLicenseAmendmentIssuanceService.cs`) و شناسهٔ sequence را روی درخواست ذخیره نمی‌کند؛ بدون ستون digest/nonce تحویل، نه binding ACK به «همان پاکت تحویل‌شده» قابل اثبات است و نه بازارسالی retry-safe همان بایت‌ها.
4. **Idempotency دقیق ACK نیازمند digest ذخیره‌شده است:** تشخیص «ACK تکراری با digest یکسان ⇒ repeated=true» در برابر «ACK متفاوت ⇒ 409» فقط با مقایسهٔ digest ذخیره‌شده ممکن است؛ جایگزین‌های بدون ستون (استفاده از audit به‌عنوان state یا حدس از timestamp) ناامن و خلاف الگوی مصوب‌اند.
5. **ردِ مشتری و ردِ اپراتوری هم‌نام‌اند:** `Rejected` فعلی ردِ تصمیم صدور است؛ گذار مشتری‌محور پس از تحویل باید با متادیتای ACK متمایز شود تا گزارش‌ها و قاعدهٔ «یک درخواست فعال به‌ازای هر خط» (filtered index روی `[Requested, PendingApproval, Issued]`) بدون ابهام بمانند.

## ۳) تغییر حداقلی پیشنهادی (یک Migration افزایشی، فقط Vendor DB)

### ۳.۱) گسترش CHECK وضعیت

```sql
[Status] IN (N'Requested', N'PendingApproval', N'Issued', N'Rejected', N'Failed',
             N'PendingDelivery', N'Delivered', N'Applied')
```

- `Rejected` برای هر دو رد (اپراتوری/مشتری) بازاستفاده می‌شود؛ تمایز با lifecycle (پس از `Delivered`) و مقدار `FailureCode` است — بدون وضعیت چهارم، کمترین تغییر.
- فیلتر ایندکس یکتای «یک درخواست فعال به‌ازای هر خط» (`[Status] IN ('Requested','PendingApproval','Issued')`) **تغییر نمی‌کند**: پس از `Delivered`/`Applied`/`Rejected` امکان درخواست اصلاح‌شدهٔ جدید همان خط باز می‌ماند که با قاعدهٔ retry فعلی سازگار است.

### ۳.۲) ستون‌های افزودنی (همگی nullable، بدون backfill)

| ستون | نوع | معنا |
|---|---|---|
| `Nonce` | `varchar(64)` | challenge تحویل؛ base64url سه‌بایتی ۳۲بایتی = ۴۳ نویسه (کوچک‌تر از کران ۲۵۶ validator) |
| `DeliveredAtUtc` | `datetimeoffset NULL` | زمان ثبت تحویل/ارسال فایل |
| `DeliveredEnvelopeSha256` | `varchar(64) NULL` | `PayloadSha256` پاکت دقیقِ تحویل‌شده؛ مرجع binding ACK و بازارسالی همان بایت‌ها |
| `AcknowledgementSha256` | `varchar(64) NULL` | digest کل پاکت ACK پذیرفته‌شده؛ پایهٔ exact-retry و conflict |
| `AcknowledgementKeyId` | `varchar(128) NULL` | KeyId استفاده‌شده در ACK پذیرفته‌شده |
| `AcknowledgementResult` | `varchar(32) NULL` | `Applied`/`AlreadyApplied`/`Rejected` |
| `AcknowledgedAtUtc` | `datetimeoffset NULL` | زمان دریافت ACK نزد Vendor |
| `CustomerProcessedAtUtc` | `datetimeoffset NULL` | `processedAtUtc` اعلامی مشتری در ACK |

- نام‌گذاری و معنا عیناً هم‌الگوی `VendorProvisioningRequest` است تا مرزبندی مفهومی یکسان بماند؛ `FailureCode varchar(64)` فعلی برای کد رد مشتری بازاستفاده می‌شود (همهٔ کدهای allow-list ≤ ۶۴ نویسه‌اند).
- `Down` دقیقاً معکوس `Up` است؛ هیچ داده‌ای تغییر نمی‌کند؛ هیچ جدول موازی ساخته نمی‌شود؛ هیچ تغییر Schema در Customer DB یا `Server/**` وجود ندارد.

## ۴) گزینهٔ رد‌شده

- **جدول موازی `VendorLicenseAmendmentAcknowledgements`:** صریحاً ممنوع در مأموریت؛ join/همگام‌سازی اضافه و خلاف الگوی تک‌ردیفی مصوب.
- **حدس زدن وضعیت از audit یا timestamp:** audit منبع حقیقت state نیست؛ رد شد.
- **افزودن فیلد به `LicensePayload` برای requestId/nonce:** قرارداد امضا منجمد است؛ manifest غیرامضای §۴.۲ قرارداد تحویل، همین نیاز را بدون تغییر payload و بدون ریسک امنیتی برطرف می‌کند.

## ۵) برنامهٔ پیاده‌سازی پس از تأیید (ترتیب)

1. Entity + configuration + Migration افزایشی Vendor + snapshot/target-model و عبور از گاردهای `has-pending-model-changes`/migration-guard.
2. Status constants جدید و قواعد گذار خالص (تست واحد اول).
3. Endpoint آماده‌سازی تحویل و export (الگوی export provisioning/#394) با ثبت nonce/digest و audit تراکنشی.
4. Endpoint intake ACK با ترتیب ۱۲ گامی قرارداد §۶ و `ProvisioningAcknowledgementStateRules`-مانند allow-list.
5. تست‌های واحد + SQL Server + HTTP قراردادی طبق §۱۱ قرارداد.
6. مستندات: WORK_LOG/INDEX/handoff و در صورت تصمیم مالک، ثبت ADR با شمارهٔ بعدی واقعی از انتهای `docs/DECISIONS.md`.

## ۶) گستره و ممنوع‌های این بسته

- تا تأیید: هیچ Migration، ستون، endpoint، provider یا تستِ رفتاریِ وابسته به Schema ساخته نمی‌شود؛ Runtime Gate خاموش می‌ماند؛ هیچ کلید صادر یا ACK واقعی جریان نمی‌یابد.
- پس از تأیید نیز: فقط Vendor DB؛ هیچ Customer DB/Migration؛ هیچ امضای دوم؛ هیچ جدول موازی؛ هیچ secret در log.

## ۷) سؤالات باز (مطابق قرارداد §۱۲)

1. تصویب همین تغییر حداقلی؟
2. کلید ACK amendment = کلید provisioning یا جدا؟ (#235)
3. retention/audit رکوردهای ACK؟
4. مجوز اپراتوری دو اقدام تحویل؟
