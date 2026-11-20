
# نقشه اجرایی تکمیل Licensing و Vendor بر اساس D-059

> **وضعیت:** مرجع نیازمندی و ترتیب اجرا؛ تصمیم‌های محصول پذیرفته شده‌اند، اما این سند به‌تنهایی شاهد پیاده‌سازی نیست. وضعیت هر قابلیت باید با کد و CI جاری تطبیق داده شود.

> **توضیح سیاست تأیید (D-109، 2026-11-19):** عبارت‌های تاریخی Maker-Checker در این برنامه به گردش‌کار درخواست/تصمیم اشاره دارند؛ برای Vendor v1 الزام دو کاربر متفاوت یا منع self-approval وجود ندارد. اپراتور مجاز می‌تواند درخواست خود را تأیید/رد کند. Maker-Checker دو‌نفره فقط با تصمیم مستقل مالک در آینده فعال می‌شود.

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
- هیچ private signing key در Git، Customer/Vendor DB، artifact، log یا پاسخ API قرار نگیرد. طبق D-107، امضای مرکزی Vendor برای LicenseEnvelope و Provisioning/Amendment در نسخه اول با Windows CNG/Certificate Store و سطح صریح SoftwareProtected مجاز است؛ TPM/HSM/KMS برای این دو کاربرد اجباری نیست. این معافیت شامل امضای Vendor offline-revocation snapshot نیست؛ آن مسیر طبق D-055 همچنان به HSM واقعی در Production نیاز دارد. کلید SoftwareProtected باید non-exportable، محدود به هویت سرویس، با گردش‌کار درخواست/تصمیم، Audit و fail-closed در خطا باشد؛ طبق D-109 تأیید تک‌اپراتوره در Vendor v1 مجاز است. D-057 مربوط به Customer offline-revocation جداست.
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
- گردش‌کار approval برای عملیات حساس، audit و sequence monotonic؛ self-approval در Vendor v1 طبق D-109 مجاز است.
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
- پیاده‌سازی SoftwareProtected signer طبق D-107 برای LicenseEnvelope و Provisioning/Amendment، independent audit sink و MFA/maker-checker عملیاتی blockers باقی‌مانده‌اند؛ HSM/TPM برای این دو کاربرد Vendor signer اجباری نیست. امضای Vendor offline-revocation snapshot همچنان به HSM واقعی Production طبق D-055 نیاز دارد. physical TPM verification هم برای دو الزام مستقل باقی است: آزمون TPM سخت‌افزاری روی سرور Customer پیش‌نیاز Production ACK طبق D-055 است و TPM 2.0 NV Extend برای anti-rollback ابطال آفلاین مشتری طبق D-057 لازم است.
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
- سفارش و Line تأییدشده، مستقل از وضعیت پرداخت، درخواست تغییر entitlement را به Engine تحویل می‌دهند؛ Commercial خودش LicenseEnvelope امضا نمی‌کند. Payment verification چرخه درخواست/تصمیم و Audit جدا دارد و شرط صدور نیست (D-108). طبق D-109، self-approval توسط اپراتور مجاز در Vendor v1 مجاز است.

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

## وضعیت اجرای Issue #390 (2026-11-03)

- شاخه `feat/issue-390-company-scoped-license-contract-v2` قرارداد payload v2، validator، canonical serializer و pure successor rules را اضافه می‌کند.
- دامنه PR فقط Licensing.Contracts، تست قرارداد و مستندات است؛ Vendor issuance در #393 و Customer persistence/enforcement در #391 باقی می‌مانند.
- Envelope بیرونی و الگوریتم D-045 تغییر نمی‌کنند؛ v1 bytes و allowlist legacy حفظ می‌شوند.
- **وضعیت نهایی:** PR #472 در main ادغام و ۱۵/۱۵ Check Run پس از Merge موفق شده است؛ PR #533 نیز Handoff/verification را ثبت کرده است. قرارداد v2 در main Verified است؛ این وضعیت به معنی وجود CompanyReference mapping در provisioning v4 یا consumer مشتری نیست.



## وضعیت اجرایی Issue #393 — 2026-11-04
- **Implemented / Verified in main:** candidate افزایش installation-scoped `maxCompanies` در PR #527 ادغام شد؛ merge commit `2a62811acdb71bda77288356743be45356685954` دارای ۱۵/۱۵ Check Run موفق پس از Merge است. این builder Payload v1/v2 را در همان نسخه نگه می‌دارد و تمام entitlementهای دیگر را حفظ می‌کند.
- **NotImplemented:** commercial order/payment evidence، persistence درخواست Amendment، endpoint، approval، signing/export و statusهای purchased/delivered/acknowledged. طبق D-108، Verified payment شرط صدور نیست؛ چرخه پرداخت و صدور مستقل و هر دو approval/Audit جدا دارند؛ self-approval طبق D-109 برای Vendor v1 مجاز است.
- **Dependency:** add-onهای Company-scoped برای module/feature/user-seat نیازمند current Payload v2 و CompanyReference قابل اعتماد است. تبدیل خودکار Payload v1 به v2 ممنوع است؛ تا زمانی که مرجع شرکت‌های نصب از مسیر provisioning/signed ACK معتبر در دسترس نباشد، UI نباید Company دلخواه را به‌عنوان مرجع معتبر بپذیرد.
- **محدوده:** این برش هیچ تغییر در Customer `Server/**`، دیتابیس مشتری یا Migration مشتری ندارد و signer واقعی KMS/HSM را انتخاب یا شبیه‌سازی نمی‌کند.


## وضعیت اجرایی Issue #393 — برش دوم Company-scoped add-on (2026-11-06)

- **Implemented / Verified in main:** `VendorLicenseAmendmentPayloadBuilder.TryBuildCompanyEntitlementIncrease` در PR #558 ادغام شد؛ merge commit `8d9740631e715e17b6d280026a049a89a2596f3b` دارای ۱۵/۱۵ Check Run موفق پس از Merge است.
- **قواعد fail-closed:** فقط reference موجود در payload پذیرفته می‌شود؛ v1 به v2 تبدیل نمی‌شود؛ Company جدید ساخته نمی‌شود؛ ماژول/Feature ناشناخته، تکراری، از قبل موجود، Feature بدون ماژول مالک، کاهش یا مقدار برابر، سقف خارج از محدوده و درخواست بدون تغییر رد می‌شود. enum تا تصویب کاتالوگ مقادیر قابل صدور نیست.
- payload candidate همان LicenseId/InstallationId و نسخه v2 را نگه می‌دارد، Sequence را فقط در candidate یک واحد افزایش می‌دهد و maxCompanies، تاریخ‌های support/update و entitlement شرکت‌های دیگر را حفظ می‌کند. اعتبارسنجی امضا و ثبت sequence پایدار همچنان مسئولیت caller/service و مرحله بعدی است.
- **NotImplemented:** Commercial Order/Payment Evidence، persistence درخواست Amendment، endpoint، approval، signer/export و وضعیت‌های Purchased/Delivered/Acknowledged. این PR آن‌ها را تکمیل‌شده معرفی نمی‌کند.
- **مرز:** فقط Vendor Host، تست Vendor و مستندات؛ بدون `Server/**`، دیتابیس/Migration مشتری یا secret. تست محلی در زمان PR اجرا نشد؛ post-merge CI روی merge commit #558 اکنون ۱۵/۱۵ Check Run موفق دارد.


## وضعیت اجرایی Issue #393 — برش سوم: تمدید مستقل پشتیبانی/به‌روزرسانی (2026-11-07)
- **Implemented / Verified in main:** متد `TryBuildSupportAndUpdatePeriodExtension` در PR #565 ادغام شد؛ merge commit `66b60cc48f6454997e260cb1244374114e2bfbd4` دارای ۱۵/۱۵ Check Run موفق پس از Merge است.
- تاریخ `expiresAtUtc` و entitlementهای Company/Installation تغییر نمی‌کنند؛ term license پس از expiry قابل amendment نیست و perpetual license با پایان support/update منقضی نمی‌شود.
- تاریخ null فعلی به‌عنوان مجوز ضمنی تفسیر نمی‌شود؛ این برش فقط دوره‌ای را تمدید می‌کند که قبلاً در payload امضاشده وجود داشته است. اعطای اولیه از null تا روشن‌شدن معنای تجاری آن جدا نگه داشته شده است.
- **NotImplemented:** سفارش/پرداخت، persistence، endpoint، approval، signer/export و وضعیت‌های Purchased/Delivered/Acknowledged. هیچ Migration یا Customer-side change انجام نشده است.


## وضعیت اجرایی Issue #393 — اتصال خرید تأییدشده به Amendment امضاشده (2026-11-20، برش چهارم)

- **Implemented / Verified in PR جاری:** درخواست صریح Amendment (Entity + endpoint + قواعد خالص) از ردیف سفارش دارای پرداخت Verified؛ کاندید همیشه با builderهای رسمی (`TryBuildMaxCompaniesIncrease`/`TryBuildSupportAndUpdatePeriodExtension`) از آخرین پاکت امضاشده و verify‌شده بازسازی و با pipeline امضای موجود به‌صورت sequence+1 روی همان LicenseId صادر می‌شود؛ Delivery بدون تغییر از مسیر موجود (`VendorProvisioningLicenseResolver`) انجام می‌شود؛ preview فقط-خواندنی برای بازبینی اپراتور.
- **کنترل‌های fail-closed:** unique filtered index روی OrderLineId برای وضعیت‌های فعال (ضد تکرار/Replay)؛ verify امضا و hash قبل از هر build؛ sequence stale و binding نامعتبر رد می‌شود؛ شکست Signer → 503 بدون تغییر وضعیت؛ شکست Audit → Rollback کامل تراکنش؛ Rejected اجازه retry اصلاح‌شده می‌دهد؛ تأیید پرداخت خودکار Amendment نمی‌سازد و صدور به پرداخت وابسته نیست (D-108).
- **Blocked:** Company-scoped Module/Feature/UserSeat — نیاز به CompanyReference معتبر #392 دارد؛ builder company-scoped عمداً در جریان جدید expose نشده است. هیچ تغییر در Customer `Server/**` انجام نشد.
- **NotImplemented:** ACK/Applied tracking اختصاصی Amendment (وضعیت Applied همچنان فقط با Customer ACK معتبر ثبت می‌شود — در مسیر provisioning موجود) و Delivery tracking جداگانه برای Amendment؛ در صورت نیاز مالک به Issue مستقل ارجاع می‌شود.


## وضعیت اجرایی Issue #393 — رابط کاربری چرخه فروش و پرداخت (2026-11-20)
- **Implemented / Verified in main:** چرخه کامل Backend سفارش تجاری/پرداخت/تأیید مستقل/گزارش مطالبات از PR #722 (squash `c46e200b`) در main است؛ Migration `20261119175924_VendorSalesOrdersAndPayments`، مجوزهای `vendor.payments.read|manage|verify` و تست‌های Unit + SQL Server مربوط موجودند. درخواست امضای مأموریت فعلی، `origin/agent/393-vendor-payment-cycle` (شاخه قدیمی بر پایه main قبل از #722/#724) را بررسی کردیم؛ محتوای Backend آن با #722 در main منطبق است و بازنویسی نشد.
- **Implemented در PR جاری (فقط UI و تست):** صفحه فارسی/RTL «فروش و پرداخت» در Vendor Web (`VendorSales.tsx`) با فهرست سفارش‌ها، ثبت سفارش با ردیف‌ها و کنترل جمع، ثبت پرداخت دستی با مرجع رسید، درخواست تأیید/رد پرداخت با RowVersion، گزارش «مطالبات باز»، نمایش مانده بدهی و اضافه‌پرداخت و وضعیت معوق؛ صفحه «درخواست‌های تأیید» اکنون درخواست‌های `salesOrderPayment` را با برچسب اختصاصی و تصمیم‌گیری با مجوز `vendor.payments.verify` نمایش می‌دهد. تست‌های Vitest برای همه حالت‌ها اضافه شد و یک تست SQL Server برای گاردهای stale RowVersion، تصمیم تکراری، رد مجوز و Rollback شکست Audit اضافه شد.
- **NotImplemented / عمداً خارج از محدوده:** PaymentAllocation چندسفارشه طبق برش مصوب S1-b (بدون Ledger/Allocation) — پرداخت به‌صورت مستقیم به یک Order ثبت می‌شود و جمع پرداخت‌های Verified هیچ‌گاه بدهی را منفی نمی‌کند؛ اتصال سفارش پرداخت‌شده به Candidate Builder و صدور Amendment امضاشده در برش بعدی (#393، Stage 2) پس از تأیید مالک؛ Company-scoped add-onها همچنان به CompanyReference معتبر #392 وابسته و Blocked هستند.
- **محدوده:** فقط Vendor Web، تست Vendor و مستندات؛ بدون `Server/**`، بدون Migration جدید، بدون تغییر Trade و بدون درگاه پرداخت.


## نقشه اجرایی S1 — هم‌ترازشده در مأموریت S1-0 (2026-11-16؛ Issue #696)

این بخش نقشهٔ اجرایی مرحلهٔ S1 را با تصمیم‌های D-097..D-102 و وضعیت واقعی کد در `main@947901bb` هم‌تراز می‌کند. وضعیت‌ها طبق بخش ۶ همین سند تفسیر می‌شوند؛ هیچ آیتمی از این نقشه به‌تنهایی ادعای Implemented/Verified نیست.

### وضعیت ورودی (شواهد کد، 2026-11-16)
- قرارداد: Payload v2 شرکت‌محور در main (#472، صدور اولیه Vendor در #682)؛ Provisioning v5 additive (#679) و انتخاب CompanyReference فقط از payload امضاشده (#683).
- Customer: schema `CompanySetupRecords` (#684، Migration `20261116070805_AddCompanySetupRecords` فقط در مخزن)، پذیرش و Apply اتمیک v5 با ترتیب Entitlement→Duplicate→Capacity (#686)، استخراج `CustomerCompanyEntitlementResolver` (#687).
- Gate صدور v5 در Vendor بسته است (`CustomerConsumerReady=false` + `Provisioning:CompanyReferenceIssuance:Enabled` پیش‌فرض خاموش) و تا تصمیم صریح مالک باز نمی‌شود.
- Maker-checker، صدور/ابطال تراکنشی و snapshot ابطال در main هستند؛ signer Production (#236) و sink مستقل ممیزی (#107) همچنان موانع Production هستند.
- Trade روی `DenyAllTradeModuleEntitlement` fail-closed است؛ اتصال entitlement واقعی در #401 باقی است.

### برش‌های S1 و وابستگی‌های واقعی
| برش | محتوا | Issues | وابستگی واقعی |
|---|---|---|---|
| S1-a — فعال‌سازی end-to-end شرکت‌محور | تصمیم و اجرای دوکلیدی Gate v5 پس از schema review، allow-list ACK سمت Vendor، تست دو میزبان، سیاست Restore/Clone | #392، #228، #235 | کد مشتری (#686) آماده است؛ مانع: تصمیم مالک برای Gate، گسترش allow-list ACK (PR مستقل Vendor)، اجرای Migration روی دیتابیس مشتری با schema approval |
| S1-b — چرخه تجاری، پرداخت و صدور مستقل | Order/Line typed، Payment دستی چندگانه، تأیید مستقل پرداخت و صدور، Amendment امضاشده بدون گیت پرداخت، پیگیری Unpaid/Partial/Overdue و تمدید مستقل Support/Update | D-097، D-108، #393، #228 | Vendor DB فقط؛ بدون درگاه پرداخت، Ledger، Allocation، Job یا Broker؛ signer طبق D-107 |
| S1-c — فعال‌سازی مجدد سخت‌افزاری | گردش‌کار Online/Offline با تأیید مالک، تاریخچهٔ مشخصات، bind چهارگانهٔ پاسخ، audit | D-099، #384، #394 | اتصال به identity موجود #384 (بدون سیستم موازی)؛ OD-20/21/22 برای جزئیات باز‌اند |
| S1-d — Update با تأیید | بررسی بستهٔ امضاشده، تأیید مدیر مشتری، پشتیبان‌گیری، توزیع فایل Offline | D-101، #394 | به #236 برای امضای بسته وابسته است؛ بدون کانال توزیع موازی |
| S1-e — انقضای Cloud و recovery | شاخهٔ Cloud در state machine شروع (مسدودسازی ورود عادی + حفظ مسیر تمدید/بازیابی) | D-098، #396، #384 | به license-first gate #396 وابسته است |
| S1-f — اطلاعیه و CRM سبک | تفکیک سه دسته اطلاعیه، هدف‌گیری، سوابق تماس/خدمات | D-102، #228 | فقط Vendor DB؛ طراحی جزئیات Proposed؛ بدون CRM/Ticketing سنگین |
| S1-g — سقف هم‌زمانی کاربران | فیلد قراردادی نسخه‌دار + شمارش کاربران یکتای هم‌زمان طبق D-100؛ شمارش Session فقط در صورت تأیید صریح OD-26 | D-100، OD-26، #504 | تنها پس از تأیید OD-26 و اتصال runtime (#391/#384) |

### ترتیب پیشنهادی و معیار پایان S1
ترتیب: S1-a (فعال‌سازی زنجیرهٔ موجود) → S1-e (دروازهٔ شروع) → S1-b/S1-f (چرخه تجاری Vendor، پرداخت مستقل و گزارش مطالبات) → S1-c/S1-d (فعال‌سازی مجدد و Update) → S1-g (نیازمند تصمیم OD-26). پایان S1 وقتی است که: زنجیرهٔ v5 با تصمیم صریح مالک فعال و روی دو میزبان تست شده باشد، دروازهٔ شروع سه حالت Unlicensed/Active/Restricted (+Cloud expired) را enforce کند، تمدید Support/Update از سفارش تا amendment و import قابل ردیابی باشد و گردش‌کار reactivation با audit کامل کار کند. تست واقعی TPM فیزیکی برای دو مورد مستقل باقی است: آزمون TPM سخت‌افزاری روی سرور Customer پیش‌نیاز فعال‌سازی Production ACK طبق D-055 است؛ TPM 2.0 NV Extend برای anti-rollback ابطال آفلاین مشتری و recovery drill مربوطه طبق D-057 در فاز نهایی لازم است. معافیت D-107 از TPM/HSM فقط به امضای مرکزی LicenseEnvelope و Provisioning/Amendment در Vendor مربوط است؛ امضای Vendor offline-revocation snapshot همچنان HSM واقعی Production طبق D-055 می‌خواهد.

### مالکیت حوزه‌های فنی و قواعد انتخاب عامل اجرایی (تصمیم مالک پروژه، 2026-11-16؛ جایگزین ثبت قبلی «تقسیم GLM/Qwen»)

مالکیت فنی حوزه‌ها مستقل از انتخاب عامل اجرایی است و به هیچ مدل هوش مصنوعی خاصی گره نمی‌خورد؛ هیچ کدنویسی موازی آغاز نشده است:

- **سمت Vendor (مالکیت حوزه فنی):** CRM سبک Vendor (CustomerAccount، مخاطبان)، رکوردهای Installation، workflowهای تجاری (سفارش/تمدید/add-on)، پشتیبانی و سوابق خدمات، تأیید درخواست‌های فعال‌سازی مجدد سخت‌افزاری (مقایسه، تاریخچه، تأیید/رد، audit)، اطلاعیه‌ها و گزارش‌های فروش/تمدید. دیتابیس: فقط Vendor DB.
- **سمت Customer (مالکیت حوزه فنی):** Installation identity سمت مشتری، جمع‌آوری مشخصات سخت‌افزار، تشخیص هویت OS و تغییرات نسخه‌دار آن، ساخت/امضای درخواست reactivation و import/export بسته‌های امضاشده (درخواست/پاسخ/Update)، validation سمت مشتری برای reactivation و Update. دیتابیس: فقط Customer DB (در کنار کد موجود #384/#684).
- **قاعدهٔ انتساب عامل اجرایی (تصمیم مالک پروژه):** هیچ زیرسیستم یا بخشی از پروژه منحصراً یا دائماً به GLM، Claude، ChatGPT یا ابزار دیگری واگذار نشده است. انتخاب عامل اجرایی هر مأموریت با مالک پروژه و بر اساس نوع کار، پیچیدگی فنی، کیفیت مورد انتظار، توانایی ابزار، هزینه و محدودیت مصرف انجام می‌شود.
- **وضعیت جاری:** GLM در حال حاضر عامل اجرایی مأموریت‌های Vendor است؛ این یک انتخاب جاری برای مأموریت‌های فعلی است، نه انحصار یا تعهد آینده. ممکن است در مراحل بعدی برخی کارهای Vendor یا Customer به Claude، ChatGPT یا ابزار دیگری سپرده شود.
- **هماهنگی چندعاملی:** تقسیم کار میان چند عامل فقط با مرزبندی روشن فایل‌ها، قراردادها و مسئولیت‌ها انجام می‌شود تا ویرایش هم‌زمان و تعارض تغییرات پیش نیاید؛ هیچ عاملی بدون بررسی وضعیت واقعی مخزن و هماهنگی با مالک پروژه، مسئولیت یک بخش را دائمی یا انحصاری تلقی نمی‌کند.
- **قرارداد مشترک:** قالب نسخه‌دار درخواست/پاسخ reactivation و بستهٔ Update فقط یک‌بار و پیش از شروع پیاده‌سازی تثبیت می‌شود (سند قرارداد مشترک با بازبینی دو طرف)؛ پس از تثبیت هیچ طرفی قرارداد را یک‌جانبه تغییر نمی‌دهد.
- **قواعد هماهنگی:** هیچ دو عاملی فایل مشترک را هم‌زمان ویرایش نمی‌کنند (مالکیت فایل طبق `AGENT_COORDINATION.md`)؛ Vendor DB و Customer DB مستقل می‌مانند و هیچ FK یا اتصال مستقیم بین آنها ساخته نمی‌شود (D-059 بند 15).
