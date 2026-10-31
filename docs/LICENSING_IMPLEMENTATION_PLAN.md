
# نقشه اجرایی تکمیل Licensing و Vendor بر اساس D-059

> **وضعیت:** مرجع نیازمندی و ترتیب اجرا؛ تصمیم‌های محصول پذیرفته شده‌اند، اما این سند به‌تنهایی شاهد پیاده‌سازی نیست. وضعیت هر قابلیت باید با کد و CI جاری تطبیق داده شود.

## 1. هدف و مرز معماری
هدف، تکمیل سامانه Vendor از یک issuance/control-plane موجود به چرخه کامل تجاری و فنی لایسنس است؛ بدون بازنویسی معماری فعلی و بدون اتصال مستقیم Vendor به دیتابیس Customer.

سه حوزه منطقی در معماری موجود:
1. **Vendor Commercial:** CustomerAccount، قرارداد، سفارش/خرید، پرداخت، تمدید، پشتیبانی و تاریخچه تجاری.
2. **Licensing & Entitlement Engine:** LicenseEnvelope پایه، revision/amendment، شرکت/ماژول/Feature/Seat entitlement، امضا، sequence، ابطال و audit.
3. **Customer Deployment & Sync:** Installation identity، bootstrap مدیر، import پایدار، enforcement سمت سرور، تبادل آنلاین و فایل امضاشده.

اینها مرزبندی مسئولیت‌اند، نه الزام به Microservice یا دیتابیس جدید. Vendor Host/DB و Customer Host/DB مستقل می‌مانند. Cloud می‌تواند روی زیرساخت مشترک چندمستأجری اجرا شود؛ ساخت VM فیزیکی برای هر مشتری الزام نیست.

## 2. واژگان و مدل دامنه
- **CustomerAccount:** خریدار/طرف تجاری.
- **Installation:** یک استقرار مشخص برنامه و مرز عملیاتی/دیتابیس آن.
- **Company:** شرکت حسابداری و Tenant داخل نصب.
- **Base License:** یک LicenseId و LicenseEnvelope پایه برای هر Installation.
- **Revision/Amendment:** تغییر امضاشده و sequence افزایشی برای همان LicenseId/Installation؛ افزونه باعث صدور LicenseId جدید نمی‌شود.
- **Entitlement:** حق تجاری قابل اعمال؛ با مجوز عملیاتی کاربر/Role/Permission یکی نیست.

## 3. ماتریس نیازمندی‌های مصوب

| حوزه | تصمیم قطعی | معیار پذیرش |
|---|---|---|
| License scope | یک Base LicenseEnvelope به‌ازای Installation | یک Current LicenseId؛ amendment همان شناسه را حفظ کند |
| Company limit | maxCompanies در سطح Installation | شمارش/ایجاد اتمیک؛ API و UI تابع تصمیم سرور |
| Module/Feature | مستقل به‌ازای Company | Feature catalog نوع‌دار و قابل توسعه |
| User seats | مستقل به‌ازای Company | عضویت یک کاربر در دو شرکت، دو Seat مصرف کند |
| Seat decrease | block-new | عضویت موجود حفظ؛ عضویت جدید مسدود تا رفع overage |
| Feature limit decrease | no destructive mutation | داده حفظ؛ ایجاد/گسترش ممنوع؛ over-limit قابل مشاهده |
| Term expiry | restricted، بدون grace ضمنی | نوشتن عادی محدود؛ read/export/recovery مجاز؛ داده حفظ |
| Perpetual/support | مستقل | پایان support/updates حق استفاده دائمی را منقضی نکند |
| Add-on | amendment امضاشده با sequence | LicenseId/Installation ثابت؛ replay/downgrade رد |
| Cloud authority | تصمیم نهایی سمت سرور | بدون Vendor round-trip برای هر درخواست حسابداری |
| Offline authority | آخرین entitlement امضاشده پذیرفته‌شده | قطع اینترنت به‌تنهایی license را باطل نکند |
| Sync | online authenticated + signed file exchange | هر دو مسیر دارای binding، signature، sequence و audit |
| Offline admin | username/password محلی | یک‌بارمصرف، اتمیک، Identity hash؛ عدم انتقال رمز به Vendor |
| Company creation | مشروط به maxCompanies | Cloud مرکزی؛ Offline آخرین envelope امضاشده |
| Usage telemetry | در Offline stale تا sync | Vendor زمان آخرین sync را نشان دهد؛ stale به‌عنوان live معرفی نشود |

## 4. قواعد امنیتی غیرقابل نقض
- هیچ private signing key در Git، Customer/Vendor DB، artifact، log یا پاسخ API قرار نگیرد. Production signer مستقل KMS/HSM/remote signer و fail-closed است.
- کلید هویت نصب Windows: TPM 2.0 / Microsoft Platform Crypto Provider ترجیحی؛ Software CNG/DPAPI فقط با رضایت صریح و ثبت SoftwareProtected با assurance پایین‌تر. fallback خودکار ممنوع.
- هویت نصب از OS marker و کلید non-exportable/اثبات possession تشکیل می‌شود؛ MachineGuid به‌تنهایی کافی نیست. Restart/reboot/servicing معمولی نباید activation را از بین ببرد؛ OS reinstall/re-image با تغییر OS identity نیازمند reactivation کنترل‌شده است.
- تبادل آنلاین اختیاری است؛ هر عملیات حسابداری عادی به Vendor وابسته نیست. Offline باید مستقل کار کند.
- هر ورودی مشتری، CompanyId، ModuleCode، FeatureCode و count از کلاینت untrusted است؛ مرجع سرور و entitlement معتبر تعیین‌کننده‌اند.
- همه تغییرات entitlement، consent، import، amendment، bootstrap و رد درخواست‌های حساس audit شوند. Production audit sink مستقل هنوز blocker جداگانه است.
- کاهش Seat/Feature/Company هرگز موجب حذف داده یا غیرفعال‌سازی خودکار عضویت موجود نمی‌شود.
- محدودیت محیط آفلاین شفاف است: clone کامل VM/host در محیط کاملاً آفلاین قابل جلوگیری مطلق نیست.

## 5. ترتیب اجرا و وابستگی‌ها

### مرحله صفر — مستندسازی و هم‌ترازی (#395)
ثبت D-059، افزودن این سند به INDEX و هم‌تراز کردن Licensing، data model، workflows و open decisions. وضعیت واقعی کد، تست، قرارداد صرف، UI صرف و NotImplemented از هم جدا بماند.
**خروجی:** مرجع واحد traceable برای توسعه بعدی.

### مرحله یک — قرارداد entitlement نسخه‌دار (#390)
- تعریف schema/version برای company-scoped module/feature/user-seat entitlements.
- حفظ canonicalization و الگوریتم امضای فعلی: ECDSA P-256/SHA-256، IEEE P1363 دقیقاً 64-byte و امضا روی بایت دقیق payload.
- تعریف schema evolution، validation، typed values، default/absence semantics و backward compatibility.
- تعریف amendment: LicenseId و Installation ثابت، sequence strictly increasing، عدم تجمیع چند License.
**معیار پذیرش:** vectorهای ثابت، tamper/unknown version/invalid type، signature exact bytes، compatibility نسخه‌های قبلی و تست monotonic sequence.

### مرحله دو — enforcement سمت Customer (#391)
- یک policy/authorization boundary مرکزی سمت سرور؛ بررسی پراکنده در Controller/React ممنوع.
- ترتیب: authenticated identity → active company membership → entitlement → operational permission → fiscal period.
- seats: company-scoped; block-new; race-safe transaction/concurrency.
- company/module/feature limits: block creates/expansions؛ over-limit read/report؛ no delete.
- term expiry restricted؛ read/export/recovery مجاز؛ no implicit grace.
- Cloud authority و offline local accepted envelope از یک قرارداد policy استفاده کنند.
**معیار پذیرش:** cross-company/tenant، API bypass، client tampering، concurrent create، stale sequence، expired term، over-seat، over-feature و no-data-loss.

### مرحله سه — ساخت شرکت و مدیر اولیه (#392)
- مسیر bootstrap آفلاین روی Customer Host؛ رمز توسط مدیر محلی تعیین می‌شود.
- Password فقط در حافظه درخواست محلی و ASP.NET Core Identity hash؛ عدم ارسال/ذخیره در Vendor یا package/log/telemetry.
- اتمیک و یک‌بارمصرف؛ تا تکمیل bootstrap ورود عادی ممنوع.
- ایجاد Companyهای بعدی با maxCompanies و enforcement سرور؛ Cloud از authority مرکزی، Offline از envelope پذیرفته‌شده.
**معیار پذیرش:** replay token/parallel bootstrap rejected، no default password، no secret leakage، company limit race tests و tenant membership checks.

### مرحله چهار — فروش و صدور افزونه Vendor (#393)
- ثبت خرید/تغییر ظرفیت شرکت، seat، module و feature.
- ساخت amendment امضاشده برای همان Installation LicenseId.
- maker-checker برای عملیات حساس، audit و sequence monotonic.
- entitlementها به Company مورد نظر bind شوند؛ افزایش ظرفیت برای Cloud و Offline.
**معیار پذیرش:** purchase-to-amendment، scope validation، signer unavailable fail-closed، duplicate/replay/downgrade، audit atomicity و منع cross-customer/company.

### مرحله پنج — همگام‌سازی آنلاین و آفلاین (#394)
- online authenticated sync وقتی ارتباط موجود است.
- export/import فایل امضاشده همیشه در دسترس برای Offline.
- ACK و usage sync با sequence و idempotency؛ وضعیت stale در Vendor با LastSyncedAt روشن.
- قطع شبکه نباید entitlement پذیرفته‌شده را باطل کند.
**معیار پذیرش:** duplicate/reorder/replay، lost response، interrupted import، online/offline parity، stale telemetry و cross-installation rejection.

### مرحله نهایی — آزمون عملیاتی یکپارچه
- دو میزبان مستقل Vendor Host و Customer Host؛ Cloud، Dedicated و Offline.
- خرید اولیه → صدور base license → enrollment/import → ساخت شرکت → add-on → sync/file exchange → expiry/restricted → recovery.
- TPM فیزیکی، software consent path، Windows client/server و بعداً Linux adapter.
- backup/restore، OS reinstall، TPM clear/key loss و recovery drills.
- تست واقعی طبق درخواست مالک تا انتهای پیاده‌سازی موکول است؛ unit/integration/CI در هر مرحله الزامی است. در پایان #396 مسیر startup gate و Activation Wizard پس از آماده‌شدن #384/#390/#392/#394 یکپارچه می‌شود.

## 6. تفکیک وضعیت و ادعای تکمیل
- **Accepted:** تصمیم محصول پذیرفته شده، نه لزوماً کدنویسی.
- **Implemented:** کد وجود دارد.
- **Verified:** تست/CI مرتبط واقعاً موفق شده.
- **ContractOnly:** قرارداد/مدل تعریف شده ولی runtime ندارد.
- **UiOnly:** رابط کاربری بدون enforce سمت سرور.
- **NotImplemented / Blocked / NeedsApproval:** وضعیت‌های مجزا و صریح.

هیچ Issue یا PR فقط با وجود Entity، DTO، migration، mock یا UI تکمیل محسوب نمی‌شود. قابلیت تجاری باید از Vendor purchase/approval تا Customer enforcement و audit قابل ردیابی باشد.

## 7. تصمیمات خارج از دامنه و قیود موجود
- D-057: anti-rollback snapshot ابطال آفلاین با TPM NV Extend؛ D-059 آن را جایگزین نمی‌کند.
- D-058: persistent installation identity و restart-safe activation؛ D-059 آن را جایگزین نمی‌کند.
- D-054 / #228 / #235: provisioning package و ACK؛ این مسیر با LicenseEnvelope/amendment یکی نیست و نباید دوباره پیاده‌سازی شود.
- #214: Vendor signed revocation workflow؛ entitlement amendment جایگزین revocation snapshot نیست.
- OD-06 و recovery در OD-08: انتقال/بازیابی installation و فقدان کلید هنوز نیازمند تصمیم/طرح مجزا است.
- Production signer، independent audit sink، MFA/maker-checker عملیاتی و physical TPM verification blockers جداگانه‌اند.
- این سند مجوز Migration نیست؛ هر Migration باید در Issue خودش با schema review و rollout/rollback plan بررسی شود.

## 8. Issues و پیوندها
- معماری/تصمیم: #389
- قرارداد: #390
- enforcement: #391
- Customer company/bootstrap: #392
- Vendor add-ons: #393
- online/offline sync: #394
- مستندسازی: #395
- Installation identity/import: #384
- Provisioning: #228 و #235
- Issuance/revocation: #214؛ signer/key operations: #236


## 9. معماری سه‌حوزه‌ای Vendor/Customer (D-060)

این ساختار، مبنای دسته‌بندی تمام Issueها، پروژه‌ها و صفحات UI است. سه بخش در یک Modular Monolith می‌توانند Deploy شوند؛ جداسازی، جداسازی مسئولیت و مرز داده است، نه الزام Microservice.

### 9.1 Vendor CRM & Commercial
- CustomerAccount و مخاطبان/اطلاعات حقوقی و عملیاتی مشتری.
- قرارداد، سفارش، اقلام خرید، پرداخت/تطبیق، تمدید، تعهد پشتیبانی، SLA و تاریخچه تجاری.
- ارتباط پشتیبانی و سوابق تجاری قابل ممیزی؛ بدون حساب کاربری/رمز Customer و بدون داده حسابداری مشتری.
- سفارش پرداخت‌شده/تأییدشده درخواست تغییر entitlement را به Engine تحویل می‌دهد؛ Commercial خودش LicenseEnvelope امضا نمی‌کند.

### 9.2 Licensing & Entitlement Engine
- کاتالوگ محصول، Module، Feature و سیاست‌های ظرفیت.
- Base LicenseEnvelope، revision/amendment، LicenseId ثابت، sequence، signature، revocation و audit.
- اعمال entitlement در دامنه Installation/Company و اعتبارسنجی قراردادهای نسخه‌دار.
- کلید امضا در signer امن مستقل؛ Commercial order و entitlement فنی با شناسه/رابطه auditپذیر مرتبط می‌شوند.

### 9.3 Customer Deployment & Sync
- Customer installation/setup، identity enrollment، license-first startup gate، local admin bootstrap.
- Cloud API و customer-side authoritative policy، Dedicated/Offline file exchange، import/export، ACK، sync cursor و freshness.
- provisioning v4 و ACK موجود (#228/#235) مصرف/گسترش داده می‌شوند؛ پروتکل موازی ساخته نمی‌شود.

## 10. گردش شروع Customer و فرم فعال‌سازی

### 10.1 State machine سمت سرور

| وضعیت نصب | تجربه کاربر | رفتار امنیتی |
|---|---|---|
| Unlicensed — envelope وجود ندارد | Activation Wizard؛ ورود عادی مسدود | فقط مسیرهای فعال‌سازی، health و پشتیبانی محدود در دسترس |
| Active — امضا، binding، sequence و revocation معتبر | صفحه ورود عادی | احراز هویت و سپس عضویت/entitlement/permission بررسی می‌شود |
| Restricted — envelope معتبر ولی Term منقضی | ورود کنترل‌شده و صفحه حالت محدود | read/export/recovery طبق نقش مجاز؛ عملیات عادی نوشتن مسدود؛ بدون grace |
| InvalidOrUntrusted — امضا/کلید/sequence/revocation نامعتبر یا نامعلوم | صفحه خطای امن/بازیابی | fail-closed؛ هیچ دسترسی کاری اعطا نمی‌شود |
| ReactivationRequired — OS identity یا کلید نصب تغییر کرده/از دست رفته | مسیر recovery/reactivation | انتقال نصب یا recovery فقط با فرایند کنترل‌شده؛ MachineGuid به‌تنهایی کافی نیست |

- بررسی در Startup و درخواست‌های سرور انجام می‌شود؛ UI state فقط projection است.
- Login و Activation routeها از نظر دسترسی تفکیک می‌شوند؛ عدم وجود لایسنس نباید با انقضا/ابطال/خرابی هویت یکی شود.
- بعد از import موفق، سرویس state را reload می‌کند؛ restartهای عادی درخواست activation جدید نمی‌سازند.
- تا زمان تکمیل و تست مسیر جدید، Razor Login موجود حفظ می‌شود و تغییر مسیر سراسری فقط با cutover مجزا انجام می‌شود (AGENTS.md).

### 10.2 Wizard چندشرکتی و entitlement per-company
1. فایل/پاسخ آنلاین امضاشده اعتبارسنجی و در Installation ذخیره می‌شود.
2. Wizard فقط Company referenceهای مجاز را از payload می‌خواند و اطلاعات محلی/حقوقی شرکت (مانند نام شرکت الف و ب) را به هر مرجع متصل می‌کند.
3. زیرسیستم‌های finance/sales/inventory/treasury، Featureهای type-safe، max_countها و UserSeat هر Company از payload پر می‌شوند؛ فرم حق ارتقای entitlement را ندارد.
4. برای هر Company یک مدیر اولیه محلی/عضویت/نقش تعریف می‌شود. هویت User در Customer در سطح نصب یکتا است؛ User می‌تواند membership چند شرکت داشته باشد و در هر شرکت یک Seat مصرف کند. رمز فقط روی Customer Host گرفته و با Identity hash ذخیره می‌شود.
5. اطلاعات رمز، hash، token ورود و secret هرگز به Vendor، فایل‌های انتقال، audit payload یا telemetry راه نمی‌یابد.
6. ایجاد Company بعدی پس از setup همچنان تحت #392 و maxCompanies است؛ import شرکت/دوره مالی موجود از provisioning v4 دوباره‌سازی نمی‌شود.

## 11. Offline activation key exchange و مسیر سریع نصب

### 11.1 چرخه درخواست/پاسخ
1. Customer Host در زمان نصب، کلید هویت Installation را محلی و non-exportable تولید/ثبت می‌کند؛ TPM ترجیحی و SoftwareProtected فقط با consent صریح طبق D-058.
2. Customer از Wizard یک درخواست نسخه‌دار .request.json صادر می‌کند. درخواست شامل InstallationId، public SPKI/key version، assurance level، nonce/request ID، زمان/انقضا، نسخه قرارداد و proof-of-possession است؛ private key، رمز و داده حسابداری ندارد.
3. اپراتور فایل را به Vendor Host منتقل/آپلود می‌کند. Vendor identity و binding را بررسی می‌کند؛ activation code یک‌بارمصرف و bind‌شده به CustomerAccount/Installation را در Vendor Host redeem می‌کند و پس از خرید/تأیید تجاری، LicenseEnvelope یا amendment را از مسیر signer امن صادر می‌کند. کد داخل فایل درخواست نیست.
4. Vendor پاسخ .license.json امضاشده را می‌دهد. Customer آن را از Wizard وارد می‌کند؛ امضا، installation binding، request nonce، sequence، زمان، revocation و policy validate و سپس transactionally ذخیره می‌شود.
5. در صورت نیاز به انتقال متنی، .txt فقط Base64/armored wrapper بایت‌های دقیق فایل canonical است. پس از decode باید همان payload/signature bytes به دست آید؛ متن ویرایش‌شده، signature نامعتبر یا JSON reserialized جایگزین امضای اصلی نمی‌شود.
6. Customer ACK/status را با بسته مجزای امضاشده و request/sequence متناظر صادر می‌کند؛ Vendor پس از import آن را idempotently ثبت می‌کند.

### 11.2 انتقال آنلاین و گزینه‌های کمکی
- Online: همان قراردادها با HTTPS و authentication مبتنی بر Installation identity/client proof؛ انتقال مستقیم entitlement به Customer API مجاز است فقط با authorization و signature validation.
- Offline: Download Request → انتقال فایل → Upload Response؛ انتقال از طریق USB/شبکه داخلی/رسانه مجاز سازمانی، بدون اتصال مستقیم DB.
- QR/deep-link صرفاً برای request ID یا challenge کوچک و کوتاه‌عمر؛ فایل امضاشده منبع کامل entitlement باقی می‌ماند. هیچ private key/password در QR یا URL قرار نمی‌گیرد.
- sync دوطرفه باید request ID، nonce، InstallationId، payload hash، schema version، sequence، idempotency و ACK را کنترل کند؛ stale telemetry با LastSyncedAt مشخص است.

## 12. تقسیم Issueها در سه حوزه

| حوزه | Issues | مالکیت اصلی |
|---|---|---|
| Vendor CRM & Commercial | #228 (onboarding/provisioning metadata)، #393 و Issueهای تجاری بعدی | مشتری تجاری، قرارداد، خرید، پرداخت، تمدید، پشتیبانی و سوابق |
| Licensing & Entitlement Engine | #214، #236، #390، #391 | قرارداد لایسنس، entitlement، امضا، ابطال، sequence و enforcement |
| Customer Deployment & Sync | #235، #384، #392، #394، #396 | نصب، identity، bootstrap، startup gate، API، فایل، sync و ACK |

Issue #396 فقط مسئول startup gate و Activation Wizard است؛ contract schema در #390، سیاست enforcement در #391، Company/admin provisioning در #392، فروش add-on در #393 و transport/reconciliation در #394 باقی می‌ماند.
