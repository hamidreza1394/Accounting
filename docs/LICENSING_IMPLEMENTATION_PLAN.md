
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
- تست واقعی طبق درخواست مالک تا انتهای پیاده‌سازی موکول است؛ unit/integration/CI در هر مرحله الزامی است.

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
