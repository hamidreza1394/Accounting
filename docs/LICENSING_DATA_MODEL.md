# مدل داده لایسنس و تفکیک جداول واقعی از پیشنهادی

> جداول بخش «واقعی» از `Infrastructure/Persistence/Configurations/*` و Migrationهای `main` (commit `1d39dbf`) استخراج شده‌اند. جداول بخش «پیشنهادی» **وجود ندارند** (`NotImplemented`) و تا تأیید کارفرما Migration برایشان نوشته نمی‌شود (D-020..D-025).

## ۱) جداول واقعی مرتبط با Tenant و دسترسی (`Implemented`)

```mermaid
erDiagram
    Companies ||--o{ UserCompanyMemberships : "TenantId"
    Companies ||--o{ CompanyRoles : "TenantId"
    Companies ||--o{ FiscalPeriods : "TenantId"
    CompanyRoles ||--o{ UserCompanyMemberships : "(RoleId, TenantId)"
    CompanyRoles ||--o{ CompanyRolePermissions : "RoleId"
    PermissionDefinitions ||--o{ CompanyRolePermissions : "PermissionId"
    Users ||..o{ UserCompanyMemberships : "UserId (بدون FK)"
    Companies {
        Guid Id
        string Name
        string NationalId
        bool IsActive
    }
    UserCompanyMemberships {
        Guid UserId
        Guid TenantId
        Guid RoleId
        bool IsActive
    }
    CompanyRoles {
        Guid TenantId
        string Code
        string Name
        bool IsActive
    }
    PermissionDefinitions {
        string Code
    }
    FiscalPeriods {
        Guid TenantId
        date StartDate
        date EndDate
    }
```

نکات (مبتنی بر کد):
- خط‌چین `Users → UserCompanyMemberships`: ستون `UserId` **کلید خارجی ندارد** (فقط FK به `Companies` و ترکیبی `(RoleId, TenantId)`).
- `SecurityAuditEvent` جدول ممیزی امنیتی است و در نمودار برای سادگی نیامده است.
- هیچ Global Query Filter وجود ندارد؛ جداسازی با `CompanyAccessService` و فیلتر صریح است (D-013).
- نام دقیق ستون‌ها/جدول‌ها را قبل از استفاده از Migration/Configuration تأیید کنید؛ نمودار خلاصه است.

## ۲) جداول پیشنهادی (`Proposed`، `NotImplemented`)

### الف) دیتابیس سامانه فروشنده (جدا از دیتابیس نصب مشتری)

```mermaid
erDiagram
    CustomerAccount ||--o{ Installation : has
    CustomerAccount ||--o{ Contract : signs
    Contract ||--o{ License : grants
    Installation ||--o{ License : "target of"
    License ||--o{ LicenseModuleEntitlement : includes
    ModuleDefinition ||--o{ LicenseModuleEntitlement : "defines"
    License ||--o{ LicenseLimit : constrains
    License ||--o{ LicenseEvent : audited
    SigningKey ||--o{ License : "signs (kid)"
    Installation ||--o{ ActivationRequest : submits
    ActivationRequest ||--o| License : "answered by"
    CustomerAccount {
        Guid Id
        string LegalName
        string Status
    }
    Installation {
        Guid Id
        string PublicKeyFingerprint
        string Mode
        string Status
    }
    License {
        Guid Id
        string Type
        int Sequence
        date ValidFrom
        date ValidUntil
        date SupportUntil
        date UpdatesUntil
        string Status
    }
    ModuleDefinition {
        string Code
        string Name
    }
    LicenseLimit {
        string LimitCode
        int Value
    }
    SigningKey {
        string Kid
        string PublicKey
        date NotBefore
        date NotAfter
        string Status
    }
```

### ب) دیتابیس نصب مشتری (حداقلی؛ بدون داده تجاری فروشنده)

```mermaid
erDiagram
    InstallationIdentity ||--o{ InstalledLicense : stores
    InstalledLicense ||--o{ LicenseAuditEntry : logs
    InstallationIdentity {
        Guid InstallationId
        string PublicKeyFingerprint
    }
    InstalledLicense {
        Guid LicenseId
        int Sequence
        bytes SignedEnvelope
        date ImportedAt
        string State
    }
    TrustedSigningKey {
        string Kid
        string PublicKey
        date NotBefore
        date NotAfter
    }
```

کلید خصوصی نصب در دیتابیس عادی ذخیره نمی‌شود مگر محافظت‌شده؛ محل ذخیره `NeedsApproval` (D-027). `LicenseAuditEntry` می‌تواند از `SecurityAuditEvent` موجود استفاده کند (تصمیم در ADR آتی).

## ۳) ابهام‌ها و تصمیم‌های لازم

| موضوع | وضعیت |
|---|---|
| نگاشت `PermissionDefinition` به `ModuleCode` (ستون یا کاتالوگ ثابت) | `NeedsApproval` (D-025) |
| رابطه `Company` ابری با `CustomerAccount` (ستون ارجاعی یا جدول رابط در دیتابیس ابری) | **تعیین‌تکلیف‌شده با D-038 (2026-10-23):** ستون ارجاعی به `CustomerAccount` ساخته **نمی‌شود**؛ فقط شناسه مرز مات و تصادفی `Companies.CustomerBoundaryId` (هرگز برابر `CustomerAccount.Id`). نگاشت `CustomerAccount` ← (نصب، مرز) فقط در سامانه کنترلی فروشنده است: `NotImplemented` |
| محل ذخیره کلید خصوصی نصب | `NeedsApproval` |
| جایگزینی/اصلاح FK برای `UserCompanyMemberships.UserId` | تصمیم جداگانه؛ بدون اثر بر این طرح |
| لایسنس در سطح نصب یا شرکت (پیش‌فرض: نصب) | `NeedsApproval` |

## ۴) قواعد داده‌ای

- هیچ ستون یا FK از جداول نصب مشتری به جداول فروشنده نیست.
- تاریخچه لایسنس‌ها و رخدادها حذف نمی‌شود؛ تغییر وضعیت با رخداد ثبت می‌شود.
- انقضا یا ابطال هرگز ردیف داده حسابداری را حذف/تغییر نمی‌دهد (D-026).

## ۵) تکمیل فاز صفر — 2026-10-24 (`Proposed`، `NotImplemented`)

> این بخش ERD بخش ۲-الف را **جایگزین** می‌کند (در تعارض نام‌ها `LicenseRevision`/`AuditEvent` به‌جای فیلدهای `License`/`LicenseEvent` بخش ۲-الف ملاک است؛ بخش ۲-ب و جداول واقعی بخش ۱ بدون تغییرند). `License` فیلد `Type` (`Perpetual` | `Term`، D-022 بند ۱) را دارد؛ `LicenseRevision` بازه‌ها و `Sequence` را. هیچ جدول، ستون یا Migration ساخته نشده است. نام‌ها پیشنهادی‌اند و پیش از Migration با مالک محصول نهایی می‌شوند (D-041 بند ۵). نام `Activation` خالی عمداً به کار نرفته تا با سازوکار قدیمی `ActivationKeys` (D-028، کد مرده) اشتباه نشود؛ نام پیشنهادی `ActivationRequest`.

### الف) ERD تکمیلی دیتابیس فروشنده

```mermaid
erDiagram
    CustomerAccount ||--o{ CustomerContact : has
    CustomerAccount ||--o{ Contract : signs
    CustomerAccount ||--o{ Installation : owns
    CustomerAccount ||--o{ CustomerBoundary : "maps (D-038)"
    Installation ||--o{ CustomerBoundary : "hosts"
    Product ||--o{ Subsystem : offers
    Contract ||--o{ SupportContract : "may include"
    Contract ||--o{ License : grants
    Installation ||--o{ License : "target of"
    License ||--o{ LicenseRevision : "versions"
    LicenseRevision ||--o{ LicenseEntitlement : includes
    Subsystem ||--o{ LicenseEntitlement : "defines"
    LicenseRevision ||--o{ LicenseLimit : constrains
    SigningKey ||--o{ LicenseRevision : "signs (kid)"
    Installation ||--o{ ActivationRequest : submits
    ActivationRequest ||--o| LicenseRevision : "answered by"
    Installation ||--o| Installation : "superseded by"
    CentralAdminUser ||--o{ AuditEvent : "acts"
    CentralAdminUser }o--o{ AdminRole : "has"
    LicenseRevision ||--o{ AuditEvent : "subject of"
    CustomerBoundary {
        Guid BoundaryId "opaque, random, never CustomerAccount.Id"
        Guid CustomerAccountId
        Guid InstallationId
    }
    LicenseRevision {
        Guid LicenseId
        int Sequence
        string Kid
        string PayloadSha256
        date ValidFrom
        date ValidUntil
        date SupportUntil
        date UpdatesUntil
        int GraceDays
        string Status
    }
    Installation {
        Guid Id
        string PublicKeyFingerprint
        string Mode "Dedicated | CloudManaged"
        string Status "Pending | Active | Retired | Revoked"
        Guid SupersededById
    }
    SigningKey {
        string Kid
        string Status "Planned | Active | VerifyOnly | Revoked | Expired"
        date NotBefore
        date NotAfter
    }
    AuditEvent {
        Guid Id
        string PrevHash
        string Hash
        string Action
    }
```

### ب) فهرست موجودیت‌ها، ضرورت و مالکیت

| موجودیت | ضروری؟ | مالک داده | روابط و توضیح |
|---|---|---|---|
| `CustomerAccount` | بله | فروشنده | مشتری تجاری فرانو (خریدار). با شرکت حسابداری مشتری یکی **نیست** (D-020). |
| `CustomerContact` | بله (برای قرارداد و اطلاع‌رسانی) | فروشنده | n:1 به `CustomerAccount`. اطلاعات تماس کمینه؛ حساب ورود نیست و هرگز به نصب مشتری نمی‌رود. |
| `Product` | بله | فروشنده | محصول قابل فروش (مثلاً نرم‌افزار حسابداری فرانو). 1:n به `Subsystem`. |
| `Subsystem` (در ERD قدیم: `ModuleDefinition`) | بله | فروشنده | کد پایدار؛ فهرست نهایی `NeedsApproval` (OD-02، OD-15). |
| `Contract` | بله | فروشنده | n:1 به `CustomerAccount`؛ 1:n به `License`. |
| `SupportContract` | بله (حق پشتیبانی/به‌روزرسانی مستقل از حق استفاده است، D-022) | فروشنده | تاریخ شروع/پایان، دامنه، وضعیت. مقادیر `supportUntil`/`updatesUntil` هنگام صدور از آن به Payload کپی می‌شوند. |
| `License` | بله | فروشنده | هویت پایدار لایسنس (`licenseId`)؛ n:1 به `Contract` و به `Installation` هدف. |
| `LicenseRevision` | بله | فروشنده | هر صدور، تغییر، تمدید یا تعلیق یک نسخهٔ **تغییرناپذیر** با `sequence` یکنواخت، `kid`، هش بایت‌های Payload و بازه‌های اعتبار. نسخه‌ها ویرایش یا حذف نمی‌شوند (D-022 بند ۴). |
| `LicenseEntitlement` (قدیم: `LicenseModuleEntitlement`) | بله | فروشنده | زیرسیستم‌های فعال‌شده در هر نسخه. |
| `LicenseLimit` | بله | فروشنده | سقف‌ها (حداقل `maxCompanies`؛ `maxUsers` `Deferred`). |
| `Installation` | بله | فروشنده (ثبت) + نصب (هویت محلی) | مالک مستقیم (`CustomerAccount`) فقط برای نصب `Dedicated` و تهی‌پذیر است؛ در نصب `CloudManaged` چندمشتری تنها مرجع، `CustomerBoundary` است و دو منبع نباید ناسازگار باشند؛ اثرانگشت کلید عمومی؛ وضعیت؛ پیوند «جایگزین‌شده با» برای اتصال مجدد (OD-06). |
| `ActivationRequest` | بله | فروشنده | درخواست دریافت‌شده (آفلاین یا آنلاین) با `requestId` یک‌بارمصرف و نتیجه؛ n:1 به `Installation`، 0..1 به نسخهٔ لایسنس پاسخ. |
| `CustomerBoundary` | بله (برای ابر مشترک و نگاشت D-038) | فروشنده | نگاشت `BoundaryId` مات ← (`CustomerAccount`، `Installation`) با یکتایی (`CustomerAccountId`، `InstallationId`) طبق D-038 و **تنها مرجع** مالکیت در ابر مشترک؛ تولیدکنندهٔ `BoundaryId` (فروشنده یا اپراتور نصب) `NeedsApproval` است. در دیتابیس نصب فقط `Companies.CustomerBoundaryId` است؛ هیچ FK یا ستون ارجاعی به `CustomerAccount` در نصب ساخته نمی‌شود. |
| `SigningKey` | بله | کلید عمومی: فروشنده + نصب؛ کلید خصوصی: فقط محیط امن فروشنده | چرخهٔ عمر: برنامه‌ریزی‌شده، فعال، فقط-تأیید (کلید بازنشسته که لایسنس‌های قبلی را هنوز تأیید می‌کند)، ابطال‌شده، منقضی. |
| `CentralAdminUser` و `AdminRole` | بله | فروشنده | هویت جدا، MFA، نقش‌ها (D-043). هرگز ردیف `Users` نصب نیست (D-029). |
| `AuditEvent` | بله | فروشنده | Append-only با زنجیرهٔ هش (D-043). |

### پ) جداول سمت نصب مشتری (تکمیل بخش ۲-ب)

| موجودیت | محتوا | یادداشت |
|---|---|---|
| `InstallationIdentity` | `InstallationId`، اثرانگشت کلید عمومی | کلید خصوصی در ستون عادی ذخیره نمی‌شود؛ محل: OD-08. |
| `InstalledLicense` | پاکت امضاشده، `Sequence`، تاریخ ورود، وضعیت | فقط آخرین نسخهٔ معتبر + تاریخچه برای ممیزی. |
| `TrustedSigningKey` | `Kid`، کلید عمومی، بازهٔ اعتبار، امضای ریشه | فقط کش «بستهٔ اعتماد»: ردیفی که با کلید ریشهٔ داخل برنامه امضا نشده باشد نادیده گرفته می‌شود (T29)؛ چرخش: OD-07. |
| `LicenseAuditEntry` | رخدادهای ورود/رد/تغییر وضعیت | ممیزی محلی؛ انتخاب بین جدول جدا و `SecurityAuditEvent` در ADR آتی. |
| `LicenseTimeHighWater` | بیشینهٔ زمان مشاهده‌شده | برای «ساعت مؤثر» (OD-05). |
| `CustomerOfflineRevocationState` | آخرین sequence، payload digest، revoked IDs، previous/current TPM anchor digest، generation و envelope امضاشده؛ pending envelope برای recovery | فقط دیتابیس همان نصب مشتری؛ singleton با RowVersion؛ migration مستقل `20261031120000_AddCustomerOfflineRevocationState`. TPM NV Extend hash-chain خارج از backup دیتابیس است (D-057). |
 
### ت) قواعد مالکیت داده

- **داده حسابداری، کاربران و شرکت‌های مشتری:** فقط در دیتابیس نصب؛ هرگز در فروشنده.
- **داده تجاری (مشتری، قرارداد، قیمت، تماس):** فقط در فروشنده؛ هرگز در نصب. در پاکت فقط `customerRef` مات و کمینه است.
- **کلید خصوصی صدور:** فقط محیط امن فروشنده (OD-07). **کلید خصوصی نصب:** فقط همان نصب (OD-08). کلید عمومی هر دو طرف در هر دو سو مجاز است.
- **ارتباط `Company` با `CustomerAccount`:** فقط از مسیر `CustomerBoundary` در فروشنده و `Companies.CustomerBoundaryId` مات در نصب؛ مستقیم هرگز (D-038). یک `CustomerAccount` می‌تواند چند `Installation` و در هر نصب چند `Company` داشته باشد (D-020).
- هیچ ستون یا FK از جداول نصب مشتری به جداول فروشنده نیست (بخش ۴ همین سند).

## ۶) طرح تکمیلی Issue #384 — persistence فعال‌سازی restart-safe (Approved behavior; schema pending review)
مالک محصول رفتار restart-safe و الزام reactivation پس از تغییر OS-installation identity را تأیید کرده است. این بخش هنوز ERD/DDL نهایی یا Migration نیست.

### موجودیت‌های مشتری
- InstallationIdentity: یک ردیف authoritative برای هویت نصب مشتری شامل InstallationId، OS-installation marker digest، public-key fingerprint، KeyId/KeyVersion، ProviderName، AssuranceLevel (`HardwareTpm` یا `SoftwareProtected`)، زمان/فاعل تأیید ریسک نرم‌افزاری (در صورت انتخاب)، وضعیت، LastVerifiedAtUtc و RowVersion. هیچ private key یا secret در جدول ذخیره نمی‌شود. انتخاب SoftwareProtected فقط پس از تأیید صریح و ثبت audit مجاز است.
- InstalledLicense: تاریخچهٔ پاکت‌های امضاشده شامل LicenseId، Sequence، InstallationId، Kid، exact signed envelope bytes، payload digest، زمان import و وضعیت (Current/Superseded/Rejected)؛ قید یکتایی روی (LicenseId, Sequence) و کنترل concurrency برای جلوگیری از downgrade/duplicate.
- وضعیت current باید با قاعدهٔ یکتا/تراکنشی مشخص شود؛ تاریخچه حذف نمی‌شود. هر import ابتدا signature، trusted key، payload، installation binding، sequence، time و revocation را اعتبارسنجی می‌کند؛ سپس ثبت اتمیک انجام می‌شود.
- restart فقط state را load و دوباره verify می‌کند؛ فایل ورودی دوباره درخواست نمی‌شود مگر نصب identity mismatch/خرابی state/recovery کنترل‌شده.
- CustomerOfflineRevocationState از D-057 مستقل باقی می‌ماند؛ InstalledLicense جایگزین revocation state store نیست.

### هویت OS و کلید نصب
- MachineGuid به‌تنهایی نه راز است و نه اثبات سخت‌افزاری؛ فقط marker کمکی است.
- طراحی باید marker نصب ویندوز را با کلید نصب non-exportable و امضای challenge/اثبات possession bind کند. Private key در DB، backup عادی، log یا package قرار نمی‌گیرد.
- نصب مجدد OS با marker جدید، حتی اگر سخت‌افزار ثابت باشد، activation جدید می‌خواهد. patch/update عادی که marker نصب را حفظ کند، activation را حفظ می‌کند.
- schema نهایی و Migration پس از انتخاب provider و روش recovery در OD-08/Issue #384 نهایی می‌شود.
