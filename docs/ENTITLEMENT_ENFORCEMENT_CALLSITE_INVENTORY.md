# Inventory نقاط مصرف Entitlement در Customer Host و طرح اجرای Enforcement — `Proposed`

> **وضعیت سند:** تحلیل فنی و پیشنهاد اجرا (`Proposed`)؛ هیچ سیاست، رفتار یا فلگ جدیدی فعال نشده است. این سند خروجی S-D مأموریت «ادامهٔ کار Vendor» است و فقط مسیر پیاده‌سازی آینده را آماده می‌کند. تغییر کد تولیدی صفر است. مرجع تصمیم‌ها: `docs/DECISIONS.md` (D-013/D-025/D-041/D-054/D-058/D-059/D-072..D-073)، `docs/LICENSING.md`، `docs/LICENSING_WORKFLOWS.md` §۱۴.

## ۱) وضعیت واقعی اجرای فعلی (سweep کد، main@`58a1ccd4`)

امروز فقط دو لایه اجرا وجود دارد و هر دو «سطح وضعیت لایسنس» را کنترل می‌کنند، نه entitlement شرکت‌محور:

1. **گیت سراسری نصب (opt-in):** `LicenseRuntimeGateMiddleware` در `Server/Program.cs:468` بعد از redirect اولین‌راه‌اندازی و `UseStaticFiles` و **قبل از** UseRouting/Authentication/Authorization ثبت شده است؛ یعنی حتی درخواست‌های anonymous را هم رد می‌کند. فعال‌سازی فقط با `Licensing:RuntimeGate:Enabled=true` است و پیش‌فرض خاموش = عبور کامل. رفتار بر اساس `LicenseRuntimeStateSnapshot` (فعال‌سازی از `ILicenseRuntimeStateService.ResolveAsync` در `LicenseRuntimeStateService.cs:40`؛ ارزیابی کامل امضا/trust/binding/revocation در هر فراخوانی بدون cache در `CustomerLicenseRuntimeEvaluator.cs:53`): `Active` → عبور؛ `Restricted`+Dedicated → فقط GET/HEAD/OPTIONS و نوشتن‌ها با 403 `license_restricted`؛ `Restricted`+Cloud → 403 `license_cloud_expired` (D-098)؛ `Unlicensed` → 503 `license_required`؛ `Invalid` → 503 `license_invalid` (fail-closed). مسیرهای همیشه‌مجاز: `/api/v1/licensing`، `/api/v1/provisioning`، `/DatabaseSetup`، `/error` و خروج نشست `/account/logout` (`LicenseRuntimeGateMiddleware.cs:27-36`). **این گیت هیچ اطلاعی از Company/Subsystem/Seat ندارد.**
2. **کنترل‌های Provisioning-time (تنها مرز شرکت‌محور):** در `CustomerProvisioningApplyService` — resolve payload لایسنس فعلی برای بسته (`:218`؛ کد `provisioning_license_not_installed`)، entitled بودن `CompanyReference` در `CompanyEntitlements` امضاشده (`:224`؛ کد `provisioning_not_entitled_for_company`) و سقف `MaxCompanies` نسبت به شمار کل `Companies` (`:233`؛ کد `provisioning_company_capacity_exceeded`؛ فقط صدور بستهٔ جدید را می‌بندد، هرگز شرکت‌های موجود را). اعطای زیرسیستم‌ها به نقش مدیر اولیه از طریق `CustomerProvisioningSubsystemCatalog` (`:294-306`) ماده‌ای می‌شود؛ **هیچ جدول runtime برای زیرسیستم‌های اعطاشدهٔ هر شرکت وجود ندارد** (`SubsystemsJson` فقط روی `Domain/ProvisioningRequestClaim.cs:15` برای audit ذخیره می‌شود).

نتیجهٔ مهم: outside provisioning، هیچ عملیات حسابداری امروز entitlement شرکت‌محور را نمی‌سنجد و سقف نشست‌های `MaxUsers` (Seat) هیچ‌جا اعمال نمی‌شود. این همان شکاف `#391/#401` است که `docs/LICENSING.md` آن را «Call-site اجرایی enforcement ... هنوز NotImplemented» ثبت کرده است.

## ۲) جدول Inventory کامل نقاط

| # | محل دقیق (file:line) | عملیات محافظت‌شده | اطلاعات Company/Tenant لازم | رفتار فعلی | ریسک bypass | رفتار fail-closed مورد انتظار | تست‌های لازم |
|---|---|---|---|---|---|---|---|
| 1 | `Server/Program.cs:468` + `LicenseRuntimeGateMiddleware.cs` | کل درخواست‌های عملیاتی نصب (بجز مسیرهای همیشه‌مجاز) | Installation-level؛ بدون Company | گیت state-only، opt-in، پیش‌فرض خاموش | با فلگ خاموش، همهٔ عملیات بدون لایسنس معتبر اجرا می‌شود (انتخاب پیکربندی، نه bypass کد) | همان رفتار فعلی؛ هر خطای resolve باید Invalid/503 شود | HTTP testها برای پنج state × دو DeploymentMode؛ تست مسیرهای همیشه‌مجاز |
| 2 | `GET /api/v1/licensing/status` (`ApiEndpoints.cs:787-836`) | projection وضعیت (بدون تصمیم) | — | فقط اطلاع‌رسانی؛ Limits و CompanyEntitlements را نمایش می‌دهد | None (اطلاعاتی) | — | تست no-store و anonymous |
| 3 | `CustomerProvisioningApplyService.cs:218/224/233` | صدور/apply بستهٔ provisioning و ایجاد شرکت | CompanyReference از بستهٔ امضاشده | کنترل entitlement/capacity فقط هنگام apply | اگر فقط اینجا بماند: شرکت‌های موجود پس از ابطال/کاهش سقف بی‌کنترل می‌مانند | خطا → اتمیک rollback بسته (رفتار فعلی) | تست‌های موجود apply + تست کاهش سقف |
| 4 | `TradeAccessFilter.cs:21` روی گروه `/api/v1/trade/**` (`TradeEndpoints.cs:13-22`) با `TradeAccessGate.cs:109-125` و `ITradeModuleEntitlement` (impl فعلی `DenyAllTradeModuleEntitlement.cs:12` — همیشه false) | تمام عملیات Trade | TenantId/CompanyId از work-context معتبر سرور | entitlement **قبل از** مجوز کاربر بررسی می‌شود؛ استثنا → deny؛ `ModuleUnavailable` → 403 `trade_module_unavailable` | — (این seam امن و fail-closed است) | **قرارداد مرجع برای بقیهٔ ماژول‌ها**؛ تغییر ممنوع (مالکیت Claude؛ `DenyAll` باید بماند) | تست‌های موجود Trade؛ فقط خوانده شد |
| 5 | `CustomerCompanyEntitlementResolver.cs:17/24/53/67` (static، بدون interface) | resolve entitlements payload لایسنس فعلی | CompanyReference | تنها مصرف‌کننده: `CustomerProvisioningApplyService` | عدم استفادهٔ runtime از آن یعنی قضاوت entitlement فقط در صدور است | resolve ناموفق → رد provisioning (رفتار فعلی) | تست‌های موجود؛ در طرح E2 مصرف runtime پیشنهاد می‌شود |
| 6 | `Domain/CompanySetupRecord.cs` (نگاشت 1:1 Company↔CompanyReference↔InstallationId) | حلقهٔ اتصال شرکت به entitlement لایسنس | هر دو طرف mapping | فقط در تراکنش apply ساخته می‌شود؛ **هیچ reader زمان اجرا ندارد** | بدون reader، entitlementهای شرکت‌محور payload در عملیات قابل مصرف نیستند | هر ناهماهنگی mapping → deny | تست ناهماهنگی mapping (شرکت بدون رکورد) |
| 7 | `WorkContextService.cs` (سوییچ شرکت/دوره در `/companies/{id}/context`) | انتخاب شرکت فعال کاربر | TenantId+CompanyId از context سرور | هیچ بررسی لایسنس/entitlement ندارد | کاربر به شرکتِ خارج از entitlement لایسنس سوییچ و عملیات می‌کند | سوییچ به شرکتِ بدون entitlement معتبر باید deny شود (D-025) | تست سوییچ به شرکتِ خارج از entitlement |
| 8 | `/companies/{companyId}/users` + `/admin/companies/{id}/membership-requests` (`CompanyUserDirectoryEndpoints.cs:20`، `CompanyUserProvisioningEndpoints.cs:27`، `CompanyMembershipRequestEndpoints.cs:22/33`) | عضویت/افزودن کاربر | CompanyId مسیر + work-context | **`MaxUsers` (Seat) هیچ‌جا اعمال نمی‌شود** | سرریز نشست‌های خریداری‌نشده | افزودن کاربر بالای Seat → رد با کد روشن؛ کاربران موجود دست‌نخورده (روح block-new D-059) | تست مرز Seat و رفتار کاهش سقف |
| 9 | `/admin/companies/{id}/roles/**` (`CompanyRoleAdministrationEndpoints.cs:50`) | مدیریت نقش/مجوز | CompanyId مسیر | فهرست زیرسیستم فقط metadata کاتالوگ است (`:1001`)؛ پیوند به entitlement اعطاشده وجود ندارد | نقش‌سازی برای زیرسیستمِ خارج از entitlement خریداری‌شده | نگهداری مجوزِ خارج از entitlement باید رد یا ممنوع شود | تست نقش با SubsystemCode خارج از entitlement |
| 10 | نواحی بدون هیچ بررسی: `/admin/users/**`، `/admin/security-audit`، `/admin/access/catalog/**`، `/corporate-groups`، `/companies/{id}/org/**` (HR)، `/session`، `/me/**`، Razor Pages | عملیات عمومی نصب | بسته به endpoint | فقط تابع گیت سراسری (وقتی opt-in) | با گیت خاموش: همه‌چیز باز است (رفتار ثبت‌شدهٔ طراحی) | پوشش با گیت سراسری کافی است؛ entitlement شرکت‌محور فقط برای endpointهای Company-scoped لازم است | تست نمونه در E1 |
| 11 | `EntitlementPolicyEngine.cs:71/211` (فقط Vendor مصرف می‌کند: `VendorAmendmentEntitlementPolicy`، `VendorProvisioningCompanyReferenceRules`، `VendorProvisioningLicenseResolver`) | ارزیابی خالص ماژول/subsystem | — | در Server هیچ call-siteای ندارد | — | Engine باید بدون وابستگی EF/ASP.NET بماند (الزام D-041) | تست‌های موجود Contracts |

## ۳) تفکیک نقاط مرکزی کم‌هزینه از نقاط پرهزینه

**گروه A — مرکزی و کم‌هزینه (پیشنهاد اجرای اول):**
- فعال‌سازی گیت سراسری موجود (#1) با تست HTTP کامل: فقط پیکربندی + تست؛ هیچ کد جدیدی نمی‌خواهد.
- یک بررسی مرکزی شرکت‌محور روی endpointهای Company-scoped (#7، #8، #9): همهٔ این مسیرها `CompanyId` را از مسیر/context سرور می‌گیرند؛ یک نقطهٔ مشترک resolve entitlement (خواندن mapping `CompanySetupRecord` + entitlementهای payload از همان snapshot گیت) می‌تواند قبل از handler اعمال شود؛ الگو: همان قرارداد `TradeAccessFilter`/`ITradeModuleEntitlement` بدون دست‌زدن به Trade.

**گروه B — پرهزینه و نیازمند برش‌بندی مستقل:**
- اعمال per-subsystem در تک‌تک عملیات زیرسیستم‌محور حسابداری (صدها endpoint؛ نیاز به نگاشت زیرسیستم↔endpoint و تصمیم دربارهٔ endpointهای مشترک) — مأموریت/ADR مستقل.
- شمارش و سقف نشست هم‌زمان (D-100/OD-26) — جداگانه، وابسته به state مشترک.
- Perpetual/restricted جزئیات (خروجی/پشتیبان‌گیری در Restricted مجاز بماند — D-026).

## ۴) طرح اجرای حداقلی پیشنهادی (همه `Proposed`؛ هیچ‌کدام بدون تأیید مالک شروع نمی‌شود)

- **E1 — فعال‌سازی گیت موجود (کمترین ریسک):** روشن‌کردن opt-in در محیط تست + ماتریس تست HTTP پنج-state × دو DeploymentMode + regression برای مسیرهای همیشه‌مجاز. کد تولیدی جدید ندارد.
- **E2 — seam شرکت‌محور مرکزی:** یک سرویس کوچکِ resolve-only (بدون جدول/Migration جدید؛ منبع داده: `CompanySetupRecord` + entitlementهای همان payload اعتبارسنجی‌شده که snapshot گیت ارائه می‌دهد) که به‌صورت یک filter/دموِن مشترک روی گروه‌های Company-scoped endpoint اعمال می‌شود؛ fail-closed: نبود mapping/نبود entitlement/خطای resolve → 403 با کد روشن. قرارداد کدهای خطا پیش از پیاده‌سازی در Issue/ADR تثبیت شود.
- **E3 — Seat (`MaxUsers`):** پس از E2، روی endpointهای عضویت کاربر (#8) با سیاست block-new (هیچ نشست/کاربر موجودی قطع نشود).
- **E4 — per-subsystem در عملیات:** فقط پس از نگاشت مصوب زیرسیستم↔endpoint؛ برش‌بندی با مأموریت مستقل.

ممنوعیت‌ها: اتصال entitlement به Trade (`DenyAllTradeModuleEntitlement` fail-closed می‌ماند)، ایجاد abstraction/سرویس اضافه، تغییر قرارداد `Licensing.Contracts`، فعال‌سازی هر سیاست بدون ADR/تأیید مالک.

## ۵) ریسک‌ها

- تفاوت معنایی «دسترسی به endpoint» و «مجوز نقش»: enforcement entitlement جایگزین RBAC نیست و هرگز نباید مجوز داخلی را دور بزند (D-025: هر دو لازم‌اند).
- Cost ارزیابی کامل در هر درخواست: امروز بدون cache است و گیت سراسری همین هزینه را دارد؛ هر بهینه‌سازی باید از مسیر S-C (ADR cache) بگذرد، نه به‌صورت cache موازی در این طرح.
- ریسک نگاشت نادرست CompanyId→CompanyReference: هر mismatch باید deny شود (fail-closed)، نه fallback به installation-level.
