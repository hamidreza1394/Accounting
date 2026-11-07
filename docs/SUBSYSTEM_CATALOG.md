# کاتالوگ رسمی زیرسیستم‌های فرانو (Subsystem Catalog)

- **Issue:** [#444](https://github.com/hamidreza1394/Accounting/issues/444) — Stage 2-A مأموریت تکمیلی (2026-11-08)
- **وضعیت:** کاتالوگ استخراج‌شده از کد واقعی مخزن (`cb2ddd57`)؛ مدل مفهومی در [D-075](DECISIONS.md) ثبت شده است (`Proposed`).
- **منبع حقیقت:** کد در Commit مبنا (D-030). این سند فهرست را از حدس نساخته است؛ هر ردیف به شواهد کد ارجاع دارد.

## 1. مدل مفهومی — تفکیک زیرسیستم از منو، فرم و عملیات

طبق D-075، این چهار مفهوم مستقل‌اند و نباید یکی گرفته شود:

| مفهوم | تعریف | محل حقیقت در کد فعلی | نمونه |
|---|---|---|---|
| **زیرسیستم (Subsystem)** | حوزهٔ قابلیت محصولی با مرز، کد پایدار و مالک مشخص؛ واحدی که لایسنس/Entitlement به آن گره می‌خورد | کد زیرسیستم: `LicenseSubsystemCodes` + `CustomerProvisioningSubsystemCatalog`؛ پوشش UI: گروه‌های `AppSidebar` | `TRADE_IMPORT`، `hr` |
| **منو (Menu)** | نقطهٔ ناوبری فارسی در Sidebar؛ می‌تواند چند زیرسیستم را نمایش دهد یا چند منو به یک زیرسیستم برسد | آرایهٔ `navigation` در `AppSidebar.tsx` | گروه «مدیریت سیستم» شامل کاربران/نقش‌ها/امنیت |
| **فرم (Form)** | صفحهٔ واقعی با Route ثبت‌شده در `AppRoutes.tsx`/`hrRoutes.tsx` | جدول مسیرهای روتر + کامپوننت صفحه | `/admin/users/:id` |
| **عملیات (Operation)** | فعل قابل اعطا روی منبع؛ واحد اعطا در `CompanyRolePermissions` | کاتالوگ `PermissionDefinitions` (کد `server-owned`) | `hr.employees.create` |

قواعد ثابت (مطابق D-064/D-065/D-071/D-074):

1. هیچ مجوزی در فرانت‌اند enforce نمی‌شود؛ `can()` فقط hint است و سرور مرجع است.
2. کد زیرسیستم و کد مجوز هر دو server-owned و نسخه‌دارند؛ wildcard یا کد ناشناخته ممنوع (D-052).
3. داشتن Permission به معنی داشتن Entitlement نیست و برعکس (D-074).
4. زیرسیستم‌های مدیریتی هسته (کاربران/دسترسی/امنیت/ساختار سازمانی) فعلاً gate entitlement ندارند؛ هر تغییر آینده باید با ADR جداگانه ثبت شود.
5. `FiscalPeriod` از Core مشترک باقی می‌ماند؛ دورهٔ موازی برای زیرسیستم‌ها ساخته نمی‌شود (ACCESS_CONTROL_ARCHITECTURE.md §1).

## 2. کاتالوگ رسمی

وضعیت‌ها با واژگان استاندارد مخزن (D-030): `Implemented` / `Partial` / `NotImplemented`. «منو» فقط گروه/آیتم مرئی فعلی Sidebar را نشان می‌دهد.

### 2.1 زیرسیستم‌های هسته و مدیریتی (بدون gate entitlement)

| کد پیشنهادی زیرسیستم | نام فارسی | شواهد کد | وضعیت | منو | کدهای مجوز (پیشوند) |
|---|---|---|---|---|---|
| `core` | هسته — داشبورد و زمینهٔ کاری | Route `/`؛ `GET /session`، `GET/POST/DELETE /context`، `GET /companies`، `WorkContextSelector` (D-031) | Implemented | «داشبورد» | — |
| `identity-admin` | مدیریت کاربران نصب | `/admin/users` + `UserAdministrationEndpoints`؛ `Domain/User`؛ D-064 | Implemented (فیلتر/خروجی CSV در ادامه) | «مدیریت کاربران» | `users.*`، `admin.users.*` (hint) |
| `membership` | عضویت شرکت و گردش درخواست | `/company/users`، `/admin/membership-requests`، `/account/membership-requests`؛ `CompanyMembershipRequestEndpoints`؛ D-065 | Implemented (انقضا/لغو خودکار `NotImplemented` طبق ADMIN_UI.md) | «کاربران شرکت جاری»، «درخواست‌های عضویت»، «درخواست‌های عضویت من» | `users.list`، `users.membership.approve` |
| `access-admin` | گروه‌های دسترسی و کاتالوگ مجوز | `/admin/roles`، `/admin/permissions`؛ `CompanyRoleAdministrationEndpoints`؛ `PermissionDefinitions` (۷۳ کد)؛ D-071 | Implemented (سقف تفویض + دوحالته) | «گروه‌های دسترسی»، «کاتالوگ و جزئیات مجوزها» | `roles.*`، `corporate-groups.directory.view` |
| `hr` | ساختار سازمانی و منابع انسانی | `Domain/Hr/*`؛ `/companies/{id}/org/*` (۲۴ endpoint)؛ `features/hr` (۱۰ Route)؛ D-072 | Implemented (پایه؛ گردش‌کارهای تکمیلی فاز بعد) | گروه «منابع انسانی» | `hr.*` (۱۲ کد) |
| `security-audit` | امنیت حساب، نشست و گزارش امنیتی | `/admin/security/audit` + `SecurityAuditAdministrationEndpoints`؛ `/me/sessions*`؛ `/admin/users/{id}/sessions`؛ `UserSession`، `SecurityAuditEvent`؛ D-069/D-073 | Implemented (retention طبق D-070 `NotImplemented`) | «گزارش امنیت و سوابق»؛ «سیاست‌های امنیتی» به‌زودی | `admin.security.*` (hint نصب) |
| `fiscal` | دوره/سال مالی و تخصیص | `Domain/FiscalPeriod`؛ انتخاب زمینه؛ D-065 §1، D-066 (`Proposed`) | Partial (grant سال/دوره `NotImplemented`) | هدر (چیپ فقط-مشاهده) | — |

### 2.2 زیرسیستم‌های عملیاتی مجوزدار (entitlement-دار)

| کد زیرسیستم | نام فارسی | شواهد کد | وضعیت | منو | entitlement |
|---|---|---|---|---|---|
| `TRADE_IMPORT` | بازرگانی خارجی | `Modules/Trade/*`؛ `TradeAccessGate` + `DenyAllTradeModuleEntitlement` (fail-closed)؛ ۴۶ کد `trade.*` seed شده؛ ۴ الگوی نقش | Partial — هستهٔ معماری ادغام‌شده؛ عملیات/entitlement runtime `NotImplemented` (#390) | «بازرگانی خارجی» (به‌زودی) | کد v2 شرکتی؛ فعلاً همیشه deny (D-074) |
| `finance` | حسابداری | فقط placeholder منو؛ هیچ Route/API/Entity | NotImplemented | «حسابداری» (به‌زودی) | کد رزروشدهٔ v1 |
| `treasury` | خزانه‌داری | فقط placeholder منو | NotImplemented | «خزانه‌داری» (به‌زودی) | کد رزروشدهٔ v1 |
| `sales` | فروش | فقط placeholder منو؛ Feature `sales.pos` در کاتالوگ | NotImplemented | «فروش» (به‌زودی) | کد رزروشدهٔ v1 |
| `inventory` | انبار | فقط placeholder منو؛ Feature `inventory.warehouses` در کاتالوگ | NotImplemented | «انبار» (به‌زودی) | کد رزروشدهٔ v1 |

### 2.3 زیرسیستم‌های پشتیبان و بیرونی

| کد پیشنهادی | نام فارسی | شواهد کد | وضعیت | منو | یادداشت |
|---|---|---|---|---|---|
| `corporate-groups` | گروه شرکتی و تلفیق | `Domain/CorporateGroups/*` (Migration `20261022150943`)؛ ۸ کد مجوز؛ فقط `GET /corporate-groups/{id}/directory` | Partial — فقط دفترچهٔ فقط-خواندنی؛ مدیریت گروه/تلفیق `NotImplemented` (D-033 تا تصمیم مرز مشتری) | خارج از منو | گزارش تجمیعی تا وجود دفتر و کدینگ ادعا نمی‌شود |
| `reporting` | گزارش‌ها | فقط placeholder منو؛ ۴ کد `reporting.*` seed شده | NotImplemented | «گزارش‌ها»، «اطلاعات پایه» (به‌زودی) | وابسته به زیرسیستم‌های عملیاتی |
| `licensing` | لایسنس و فعال‌سازی (سمت مشتری) | `/enroll`، `/provisioning/import`؛ `/licensing/import`؛ `Licensing.Contracts`؛ `InstalledLicense`، `InstallationIdentity` | Partial — import/enroll fail-closed؛ اتصال runtime به entitlement `NotImplemented` | خارج از شل (صفحات first-run) | خودِ لایهٔ لایسنس است، نه مصرف‌کننده |
| `vendor` | سامانهٔ مرکزی فروشنده | `Vendor/Vendor.Host` + `Vendor/Vendor.Web` | Partial (D-047: staged) | خارج از محصول مشتری | میزبان مستقل؛ مرز D-041 |
| `infrastructure-operator` | ابزار اپراتور نصب | `/operator/customer-boundaries/*`؛ `AccountKind=Infrastructure` | Implemented | خارج از منو | فقط حساب زیرساخت |
| `support` | راهنما و پشتیبانی | فقط placeholder منو | NotImplemented | «راهنما و پشتیبانی» (به‌زودی) | محتوای اولیه بعداً |

## 3. ارتباط با لایسنس (D-059 / D-074)

- کدهای نصب‌محور v1 (allowlist): `finance`، `sales`، `inventory`، `treasury` — `LicenseSubsystemCodes.IsAllowedInstallationSubsystem`.
- کد شرکتی v2: `TRADE_IMPORT` — `IsAllowedCompanyModule`؛ مصرف توسط `ITradeModuleEntitlement` و `TradeAccessGate`؛ تا آماده‌شدن اعتبارسنجی Envelope (#390) پیاده‌سازی Production `DenyAllTradeModuleEntitlement` و fail-closed می‌ماند.
- نگاشت provisioning: `CustomerProvisioningSubsystemCatalog` فقط کدهای شناخته‌شده را می‌پذیرد و «هرگز مجوز نمی‌سازد» (D-052).
- Featureها: `finance.analytic_account_levels`، `finance.cost_centers`، `finance.budgeting`، `inventory.warehouses`، `sales.pos` — `LicenseFeatureCatalog`.
- زیرسیستم‌های §2.1 عمداً در این جدول نیستند: حق تجاریِ استفاده از آن‌ها هنوز تصمیم محصولی ندارد و دادن entitlement به آن‌ها نیازمند ADR جداگانه است.

## 4. زیرسیستم‌های ناقص — شکاف‌های واقعی

1. **Trade:** seed مجوز کامل است ولی UI عملیاتی و enforcement entitlement runtime وجود ندارد؛ `GET /trade/capabilities` تنها endpoint است.
2. **گروه شرکتی/تلفیق:** فقط دفترچهٔ هم‌گروه؛ مدیریت گروه، مالکیت و تلفیق UI/API ندارد.
3. **تخصیص سال/دوره:** D-066 هنوز `Proposed` است؛ هیچ grant سالی/دوره‌ای در کد نیست.
4. **Retention سوابق امنیتی:** D-070 Accepted ولی `NotImplemented` (Purge/Archive).
5. **انقضا/لغو خودکار عضویت:** lifecycle در مدل هست ولی اجرای خودکار ندارد.
6. **حسابداری/فروش/انبار/خزانه‌داری/گزارش‌ها/اطلاعات پایه/پشتیبانی:** فقط placeholder — هیچ کد عملیاتی.
7. **اتصال entitlement مؤثر به مجوزدهندهٔ عمومی:** فقط الگوی Trade؛ تعمیم طبق D-074 باقی است.

## 5. قواعد نگهداری این کاتالوگ

1. افزودن زیرسیستم جدید = حداقل: کد پایدار server-owned (در صورت entitlement-دار بودن: `LicenseSubsystemCodes` + `CustomerProvisioningSubsystemCatalog`) + کدهای مجوز با پیشوند قراردادی + مسیر مالک در کد + ردیف در همین سند.
2. هیچ UI/Route تازه‌ای نباید بدون ردیف کاتالوگ ساخته شود؛ منو فقط صفحات واقعی را نشان می‌دهد و قابلیت آینده با نشان «به‌زودی» (بدون Route جعلی) ظاهر می‌شود.
3. درخت `PermissionTree` UI (زیرسیستم→منبع→عملیات) باید با بخش «کد مجوز» همین کاتالوگ هم‌خوان بماند؛ هر واگرایی یک باگ مستندات است.
4. تغییر وضعیت‌ها فقط با شواهد کد/CI (D-030) انجام شود؛ تاریخ و PR در هر تغییر ثبت شود.
