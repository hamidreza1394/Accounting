# Vendor Amendment Delivery and Acknowledgement Contract (#393)

> **وضعیت سند:** `Proposed` — بخش‌های بدون وابستگی به Schema، اجرای مصوب گزینهٔ A مالک (مأموریت اجرایی 2026-11-24) هستند؛ بخش‌های وابسته به تغییر Schema تا تأیید بستهٔ تصمیم مرجع (`DECISION_PACKAGE_AMENDMENT_DELIVERY_TRACKING.md`) `NeedsApproval` هستند و هیچ کدی برای آن‌ها نوشته نشده است. مبنی سند، قرارداد و سند S-B موجود است؛ هیچ پروتکل امضای دوم و هیچ کانال انتقال جدیدی تعریف نمی‌شود.
> مرجع گردش‌کار: `docs/LICENSING_WORKFLOWS.md` §۱۷.۳/۱۷.۴، تصمیم‌های D-054/D-059/D-060/D-108/D-109 و تحلیل `docs/AMENDMENT_ACK_CONTRACT_ANALYSIS.md`.

## ۱) مبنا، گستره و اصل بازاستفاده

- **گزینهٔ A مصوب است:** بازاستفادهٔ کامل از الگوی ACK امضاشدهٔ provisioning با kind مفهومی `license-amendment`؛ بدون broker، بدون کانال موازی، بدون ACK غیرامضاشده و بدون جدول موازی برای Delivery/ACK.
- **قرارداد ACK مشترک دست‌نخورده می‌ماند:** `CustomerProvisioningAcknowledgement`، `CustomerProvisioningAcknowledgementEnvelope` و `CustomerProvisioningAcknowledgementValidator` در `Licensing.Contracts/CustomerProvisioningAcknowledgement.cs` عیناً استفاده می‌شوند؛ هیچ فیلد یا نسخهٔ Schema جدیدی به ACK اضافه نمی‌شود.
- **امضای Amendment تغییر نمی‌کند:** منطق صدور و امضای فعلی (`VendorLicenseAmendmentIssuanceService` + `VendorLicenseEnvelopeFactory`) و ساختار `LicenseEnvelope`/`LicensePayload` بدون تغییر باقی می‌ماند؛ تحویل هیچ payload جدیدی نمی‌سازد و هیچ sequence جدیدی صادر نمی‌کند.
- **مرز داده:** Vendor هرگز مستقیماً Customer DB را تغییر نمی‌دهد؛ مسیر تنها تبادل فایل/پیام امضاشده است.

## ۲) وضعیت واقعی فعلی (پیش از پیاده‌سازی)

| قطعه | وضعیت در `main@35bd3905` | مرجع |
|---|---|---|
| صدور Amendment | پیاده‌شده و CI-verified: درخواست/تأیید/صدور تراکنشی با sequence بعدی روی همان LicenseId | `VendorLicenseAmendmentIssuanceService.cs`، `VendorLicenseAmendmentEndpoints.cs`، Migration `20261120135809` |
| پاکت امضاشده | در `VendorSignedLicenseEnvelopes` با `EnvelopeJson` و `PayloadSha256` ذخیره می‌شود؛ پیوند request↔envelope در DB ذخیره نمی‌شود | `VendorSignedLicenseEnvelope.cs`؛ issuance: `db.SignedLicenseEnvelopes.Add(...)` |
| Export عمومی پاکت لایسنس | فقط-خواندنی و بدون ثبت تحویل (برش #394) | `GET /api/vendor/licenses/{licenseId}/envelope/export` (PR #781) |
| ثبت تحویل amendment | `NotImplemented` — هیچ وضعیت/رکورد تحویلی روی درخواست نیست | entity `VendorLicenseAmendmentRequest.cs` |
| Intake ACK amendment | `NotImplemented` — intake ACK فقط برای provisioning وجود دارد | `VendorProvisioningEndpoints.cs` (`POST /api/vendor/provisioning-requests/{requestId}/acknowledgement`) |
| تولید ACK سمت مشتری برای amendment | `NotImplemented` — مشتری فقط برای بستهٔ provisioning ACK امضاشده تولید می‌کند؛ import مجدد پاکت یکسان idempotent و no-op است | `Server/Infrastructure/Licensing/CustomerLicenseImportService.cs` |

## ۳) State Machine درخواست Amendment

زنجیرهٔ موجود تغییر نمی‌کند: `Requested → PendingApproval → Issued` (یا `Rejected`/`Failed` با retry اصلاح‌شده). گسترش پیشنهادی پس از صدور:

```
Issued → PendingDelivery → Delivered → Applied
                                  └──→ Rejected
```

| وضعیت | معنای دقیق (مصوب مأموریت) | گذار مجاز |
|---|---|---|
| `Issued` | فقط صدور رسمی و امضای Amendment؛ هنوز «آماده تحویل» ثبت نشده | → `PendingDelivery` با اقدام اپراتور مجاز |
| `PendingDelivery` | آماده تحویل؛ nonce تحویل تولید و ثبت شده است | → `Delivered` با export |
| `Delivered` | تحویل/ارسال فایل طبق تعریف §۴ ثبت شده است | → `Applied` یا `Rejected` فقط با ACK امضاشدهٔ معتبر |
| `Applied` | ACK معتبر `Applied`/`AlreadyApplied` ثبت شده است | پایانی (بدون بازنویسی با ACK دیگر) |
| `Rejected` | ACK معتبر `Rejected` ثبت شده است؛ `FailureCode` = کد رد مشتری | پایانی؛ اجازهٔ درخواست اصلاح‌شدهٔ جدید برای همان خط مطابق قاعدهٔ retry فعلی |

- انتقال شکست‌خوردهٔ transport/timeout هیچ تغییری در وضعیت ایجاد نمی‌کند (§۷).
- ACK نامعتبر/غیرامضاشده/خارج از trust store هیچ گذاری ایجاد نمی‌کند (fail-closed).
- بازاستفاده از `Rejected` برای رد مشتری عمدی است: ردِ اپراتوریِ صدور و ردِ مشتری در دو پنجرهٔ جدای lifecycle رخ می‌دهند و `FailureCode` آن‌ها متفاوت است (`amendment_rejected` در برابر کد رد مشتری از allow-list §۵.۳).

## ۴) قرارداد تحویل (Vendor Delivery)

### ۴.۱) پیش‌شرط‌ها (fail-closed)
- درخواست در وضعیت `Issued` باشد (برای گام آماده‌سازی) و سپس `PendingDelivery` (برای export).
- پاکت هدف، آخرین envelope امضاشدهٔ همان `LicenseId` باشد و پس از verify کامل بماند: تطابق `PayloadSha256` ذخیره‌شده با hash بایت‌های `PayloadJson`، هم‌خوانی `KeyId`/`Algorithm` پاکت با ردیف ذخیره‌شده، عضویت KeyId در trust store امضای لایسنس (`Licensing:TrustedSigningKeys`) و verify امضای ECDSA P-256 — همان اعتبارسنجی‌های `TryVerifyLatestPayloadAsync` در `VendorLicenseAmendmentIssuanceService.cs`.
- هر شکست verify ⇒ هیچ تغییری در وضعیت/nonce/audit موفقیت؛ فقط رخداد audit رد.

### ۴.۲) محتوای تحویل
- **پاکت:** دقیقاً همان `EnvelopeJson` ذخیره‌شده در `VendorSignedLicenseEnvelopes`؛ بدون بازنویسی payload، بدون امضای مجدد متفاوت، بدون candidate جدید و بدون افزایش sequence.
- **Manifest تحویل (حداقلی و غیرامضا):** چون `LicensePayload` هیچ فیلد requestId/nonce ندارد و قرارداد امضا منجمد است، پاسخ export یک wrapper JSON حداقلی برمی‌گرداند:

```json
{
  "amendmentRequestId": "<Guid D>",
  "nonce": "<base64url 32 bytes>",
  "envelope": { ...LicenseEnvelope دقیقاً همان بایت‌های ذخیره‌شده... }
}
```

- **Manifest منبع اعتماد نیست:** فقط مقدارهایی را که مشتری باید در ACK بازتاب دهد منتقل می‌کند. دستکاری manifest فقط به شکست binding ACK می‌انجامد (§۶ گام ۶) و هیچ آسیب امنیتی ندارد؛ اعتماد فقط از امضای مشتری و مقدارهای ذخیره‌شدهٔ سمت Vendor می‌آید.
- مسیر مصرف مشتری تغییر نمی‌کند: مشتری `envelope` را از manifest استخراج و به `CustomerLicenseImportService.ImportAsync` موجود می‌دهد؛ هیچ تغییر Schema مشتری لازم نیست.

### ۴.۳) Retry و idempotency تحویل
- nonce دقیقاً یک‌بار در گذار `Issued → PendingDelivery` تولید (32 بایت تصادفی، base64url بدون padding، هم‌الگوی nonce provisioning) و بدون rotate دوباره استفاده می‌شود؛ re-export همان manifest و همان بایت‌ها را برمی‌گرداند.
- Retry تحویل هرگز صدور جدید، sequence جدید یا nonce جدید ایجاد نمی‌کند.
- export با `Cache-Control: no-store` و ثبت audit تراکنشی؛ شکست audit کل نوشتن وضعیت را برمی‌گرداند.

## ۵) قرارداد ACK (Signed Customer Acknowledgement)

### ۵.۱) فیلدها و Bindings — بازاستفادهٔ بدون تغییر از قرارداد provisioning

| فیلد ACK | مقدار برای amendment |
|---|---|
| `schemaVersion` | `1` (ثابت قرارداد فعلی) |
| `requestId` | شناسهٔ درخواست amendment (Guid قالب `D`) از manifest |
| `nonce` | nonce تحویل از manifest |
| `payloadSha256` | SHA-256 hex (حروف کوچک) بایت‌های UTF-8 `PayloadJson` پاکت تحویل‌شده — همان معنای provisioning (hash payload، نه کل envelope)؛ باید با `DeliveredEnvelopeSha256` ثبت‌شده روی درخواست برابر باشد |
| `customerReference` | `VendorCustomer.CustomerNumber` مشتری مالک درخواست |
| `installationId` | `VendorInstallation.InstallationCode` نصب همان لایسنس |
| `result` | فقط `Applied` یا `AlreadyApplied` یا `Rejected` |
| `processedAtUtc` | UTC؛ سقف آیندهٔ ۲ دقیقه طبق validator فعلی |
| `errorCode` | فقط برای `Rejected`؛ از allow-list §۵.۳؛ در غیر این صورت null |

### ۵.۲) امضا و Trust
- همان الگوریتم فعلی: ECDSA P-256 / SHA-256، امضای IEEE P1363 با طول ثابت ۶۴ بایت روی بایت‌های دقیق `AcknowledgementJson` canonical.
- `KeyId` فقط lookup hint است؛ کلید عمومی فقط از `ProvisioningAcknowledgementTrustStore` (پیکربندی مستقر) resolve می‌شود؛ KeyId ناشناخته/ابطال‌شده ⇒ رد fail-closed؛ کلید همراه پاکت هرگز مورد اعتماد نیست.
- این کلید همان کلید ACK provisioning می‌ماند یا کلید جداگانه است، تصمیم #235 است (سؤال باز §۱۲).

### ۵.۳) کدهای خطای مجاز برای `Rejected`
- الگو همان allow-list بستهٔ `ProvisioningAcknowledgementStateRules` است: فقط کدهای ثابت شناخته‌شده پذیرفته می‌شوند؛ متن آزاد، diagnostic و PII هرگز ذخیره نمی‌شود.
- واژگان پیشنهادی مجموعهٔ اول، بازاستفادهٔ دقیق کدهای رد واقعی مسیر import مشتری است (بدون اختراع واژگان دوم): `license_sequence_replay_or_downgrade`، `license_validation_failed`، `license_status_policy_unapproved`، `envelope_json_invalid`، `installation_identity_missing`، `installation_id_invalid`. همه ≤ ۶۴ نویسه‌اند تا در `FailureCode varchar(64)` فعلی جا شوند (کران validator ۱۲۸ است؛ allow-list سمت Vendor محدودتر و نهایی است).
- کد ناشناخته ⇒ ACK با 400 رد و هیچ state/metadata/audit موفقی ثبت نمی‌شود.

### ۵.۴) نگاشت نتیجه به وضعیت

| ACK result | وضعیت Vendor | شرط |
|---|---|---|
| `Applied` | `Applied` | همهٔ bindingها برابر مقادیر ذخیره‌شده |
| `AlreadyApplied` | `Applied` | پذیرش مشروط به اثبات مشتری: ردیف installed با همان `LicenseId + Sequence + EnvelopeJson` (شاخهٔ idempotent import)؛ هم‌راستا با رفتار provisioning که `AlreadyApplied` را به تأیید نگاشت می‌کند (Issue #293) |
| `Rejected` + کد مجاز | `Rejected` + `FailureCode=کد` | — |

## ۶) Intake سمت Vendor (پیشنهاد endpoint)

`POST /api/vendor/license-amendment-requests/{requestId}/acknowledgement` — `AllowAnonymous` + rate limiting اختصاصی (هم‌الگوی provisioning؛ احراز هویت با امضا نه با نشست).

ترتیب الزامی گام‌ها (هر تخلف ⇒ رد بدون اثر جانبی):
1. parse دقیق `requestId` با `Guid.TryParseExact("D")`؛
2. خواندن bounded بدنه (اندازهٔ `AcknowledgementJson` ≤ `MaxAcknowledgementJsonChars`)؛
3. resolve کلید از trust store؛ KeyId ناشناخته/ابطال‌شده ⇒ 401؛
4. تراکنش `Serializable` + بارگذاری request، customer، license/installation و تطبیق روابط مالکیت؛
5. verify امضا/canonical/کران‌ها با validator مشترک؛
6. بررسی binding پنج‌گانه: `requestId`، `nonce`، `payloadSha256` (برابر `DeliveredEnvelopeSha256`)، `customerReference`، `installationId` — همه در برابر مقادیر ذخیره‌شدهٔ ردیف، نه مقادیر manifest؛
7. محاسبهٔ digest کل پاکت ACK (`SHA-256(EnvelopeJson)`) و مقایسهٔ زمان-ثابت با `AcknowledgementSha256` ذخیره‌شده: برابر ⇒ پاسخ همان قبلی با `repeated=true` + رخداد `…acknowledgement-retried`؛ متفاوت ⇒ 409 `amendment_acknowledgement_conflict` (نتیجهٔ ACK اول تغییرناپذیر)؛
8. پیش‌شرط وضعیت: `Status == Delivered` و `DeliveredAtUtc` و `DeliveredEnvelopeSha256` پر باشند؛ در غیر این صورت 409 `amendment_acknowledgement_not_delivered`؛
9. نگاشت نتیجه با allow-list (§۵.۳/۵.۴)؛ کد ناشناخته ⇒ 400 بدون mutation؛
10. نوشتن وضعیت/متادیتای ACK با گارد RowVersion؛ تعارض هم‌زمانی ⇒ 409؛
11. audit تراکنشی (`vendor.amendment.acknowledgement-confirmed` / `…rejected`)؛ شکست audit ⇒ rollback کامل؛ و مهر `Installation.LastSeenAtUtc` در همان تراکنش (هم‌الگوی provisioning)؛
12. commit و پاسخ `no-store` شامل وضعیت نهایی و `repeated`.

- کدهای خطای validator (`provisioning_acknowledgement_*`) عیناً بازاستفاده و 1:1 به پاسخ HTTP نگاشت می‌شوند؛ تغییر prefix نیازمند تغییر قرارداد مشترک است و خارج از این قرارداد است (تصمیم بازگشت به عقب صریح).

## ۷) رفتار Timeout، پیام تکراری و ACK ناشی از پیام اشتباه

- **Timeout (ACK نرسید):** رکورد در `Delivered` می‌ماند؛ هیچ تغییر وضعیت بازگشتی/حدسی رخ نمی‌دهد. Vendor می‌تواند همان manifest/همان بایت‌ها را دوباره صادر کند؛ import مجدد پاکت یکسان سمت مشتری امروز idempotent و no-op است (شاخهٔ `AlreadyInstalled` در `CustomerLicenseImportService.cs`)؛ نمایش «در انتظار ACK» فقط گزارشی است.
- **ACK تکراری سازگار:** همان پاسخ قبلی با `repeated=true`؛ هیچ رکورد/گذار جدیدی ساخته نمی‌شود.
- **ACK دوم با digest متفاوت:** `Conflict` (HTTP 409 ترجیحی)؛ نتیجهٔ ACK اول تغییر نمی‌کند.
- **ACK ناشی از پیام اشتباه:** با شکست binding (hash/sequence/nonce متفاوت) یا کد خارج از allow-list fail-closed رد می‌شود؛ هیچ مسیری برای پذیرش ACK برای پیام اشتباه وجود ندارد.

## ۸) مرز Vendor و Customer

- Vendor فقط رکورد تحویل/ACK خودش را در Vendor DB می‌نویسد؛ هیچ دسترسی مستقیم به Customer DB وجود ندارد.
- مشتری: import همان مسیر موجود؛ تولید ACK با provider امضای ACK مشتری (پیش‌فرض fail-closed `UnavailableCustomerProvisioningAcknowledgementSigningProvider`)؛ کلید خصوصی هرگز منتقل نمی‌شود.
- هیچ secret، کلید خصوصی، hash رمز یا دادهٔ حساس در ACK، manifest، log یا audit قرار نمی‌گیرد.

## ۹) Audit و تراکنش

- رخدادها (هم‌خوان با نام‌گذاری موجود): `vendor.amendment.delivery-prepared`، `vendor.amendment.package-exported`، `vendor.amendment.package-export-retried`، `vendor.amendment.acknowledgement-confirmed`، `vendor.amendment.acknowledgement-rejected`، `vendor.amendment.acknowledgement-retried`.
- هر گذار وضعیت و رخداد audit متناظرش در یک تراکنش Vendor DB ثبت می‌شوند؛ شکست audit ⇒ rollback کامل گذار (fail-closed؛ الگوی موجود issuance/ACK provisioning).

## ۱۰) نیازهای Persistence — نقطهٔ توقف مأموریت

اجرای §۳ تا §۹ روی `VendorLicenseAmendmentRequests` فعلی **غیرقابل پیاده‌سازی** است: CHECK constraint وضعیت فقط پنج مقدار فعلی را می‌پذیرد و هیچ ستونی برای nonce/تحویل/ACK وجود ندارد؛ پیوند request↔پاکت تحویل‌شده نیز جایی ذخیره نمی‌شود. شواهد و تغییر حداقلی در بستهٔ تصمیم `docs/DECISION_PACKAGE_AMENDMENT_DELIVERY_TRACKING.md` ثبت شده است و تا تأیید صریح مالک هیچ Migration، ستون یا کد intake ساخته نمی‌شود.

## ۱۱) برنامهٔ تست (برای اجرا پس از تأیید Schema)

- **واحد:** نگاشت نتیجه/allow-list؛ تولید nonce و قالب canonical manifest؛ bindingها.
- **SQL Server (CI):** چرخهٔ کامل Issued→PendingDelivery→Delivered→Applied با ACK واقعاً امضاشدهٔ تست؛ exact retry (repeated=true، بدون رکورد جدید)؛ ACK دوم متفاوت ⇒ 409 و تغییرناپذیری نتیجهٔ اول؛ ACK ناسازگار/کد ناشناخته/کلید غیرمورد اعتماد ⇒ رد fail-closed بدون mutation؛ شکست audit ⇒ rollback.
- **HTTP قراردادی:** anti-patternها (بدون امضا، KeyId ناشناخته، بدنهٔ بزرگ) و `no-store`.
- **رگرسیون:** idempotent بودن re-import مشتری برای همان پاکت؛ صدور sequence صرفاً از مسیر approval.

## ۱۲) سؤالات باز مالک

1. تأیید تغییر حداقلی Schema طبق بستهٔ تصمیم (پیش‌شرط اجرای §۳..§۹).
2. کلید امضای ACK amendment: همان کلید provisioning یا کلید/دورهٔ جدا؟ (وابسته به #235).
3. سیاست retention/audit رکوردهای ACK amendment (هم‌راستا با D-070؛ مرز نگهداری Vendor طبق D-046).
4. تصریح دسترسی اپراتور برای دو اقدام تحویل: فقط `vendor.licenses.manage` یا مجوز مستقل جدید؟
