# تحویل وظیفه — SECURITY_AUDIT_UI_COMPLETION

- **Issue:** مرتبط با #487، #501، #502، #507، #512 (زیرمجموعه #444)؛ بدون Issue جدید — `UNNUMBERED`
- **Agent/مسئول:** GLM (Z.ai)
- **تاریخ:** 2026-11-06
- **شاخه:** `feature/security-audit-ui-completion` (از `origin/main` @ `265e449f`)
- **وضعیت:** انجام‌شده — Backend دست‌نخورده؛ فقط اتصال UI به قابلیت‌های موجود

## هدف و معیارهای پذیرش
- [x] ممیزی کد واقعی پیش از تغییر (Backend کامل بود؛ هیچ قابلیتی بازنویسی نشد)
- [x] مرتب‌سازی UI به API موجود متصل شد (`sort=occurredAt|eventType` + `descending`، سمت سرور)
- [x] انواع رویداد از `GET /admin/security/audit-event-types` دریافت می‌شوند (پارسر سخت‌گیر، رویداد ناشناخته حذف نمی‌شود)
- [x] فهرست همهٔ شرکت‌های نصب از `GET /admin/companies` مصرف شد (نه `/companies` عضویت‌های کاربر)
- [x] خروجی CSV از `POST /admin/security/audit-events/export` مصرف شد (بدون تغییر Backend)
- [x] ورود و خروج از رویدادهای واقعی گزارش نمایش داده می‌شوند (نوشته‌شده توسط `AuthenticationService` در `SecurityAuditEvents`)
- [x] نشست‌ها با Audit اشتباه گرفته نشد؛ Endpoint مدیریتی نشست اختراع نشد
- [x] D-070 بدون تصمیم مالک اجرایی نشد؛ هیچ حذف/Purge/Migration ایجاد نشد
- [x] هیچ فایل ممنوعه تغییر نکرده است
- [x] تست‌های Regression موفق (۷۵۴/۷۵۴ کلاینت)
- [x] Handoff مستقل (همین سند)
- [x] هیچ ادعای تست/CI بدون نتیجه واقعی

## وضعیت اولیه — ماتریس ممیزی (کد واقعی main @ `265e449f`)

| قابلیت | Backend | UI پیش از این PR | اقدام در این PR |
|---|---|---|---|
| نمایش رویدادها | `GET /admin/security/audit-events` (کامل، بدون IP/DetailsJson) | موجود | حفظ |
| صفحه‌بندی | سرور-محور با سقف ۱۰۰ | موجود | حفظ |
| مرتب‌سازی | Allowlist `occurredAt\|eventType` + `descending` (PR #512) | **مصرف نمی‌شد** | اتصال (مرحله ۱) |
| فیلتر نوع رویداد | پارامتر `eventType` | ورودی آزاد با datalist ثابت | اتصال به API انواع رویداد |
| فیلتر تاریخ شمسی | `fromUtc`/`toUtc` | موجود (`JalaliDateField`) | حفظ |
| فیلتر کاربر | `userId` | موجود (`UserPicker`) | حفظ |
| فیلتر شرکت | `companyId` + `GET /admin/companies` | از `GET /companies` (فقط عضویت‌های خود کاربر) | اتصال به Endpoint مدیر نصب |
| فهرست انواع رویداد | `GET /admin/security/audit-event-types` | **مصرف نمی‌شد**؛ لیست ثابت | اتصال با پارسر سخت‌گیر |
| خروجی CSV | `POST /admin/security/audit-events/export` (۳۱ روز، ۱۰٬۰۰۰ ردیف، BOM، ضد Formula Injection، رویداد `SecurityAuditExported`) | **مصرف نمی‌شد** | اتصال کامل UI |
| ورود/خروج سراسری | `LoginSucceeded/Failed/Denied*`، `LogoutSucceeded` توسط `AuthenticationService` ثبت می‌شوند | از طریق گزارش موجود | حفظ + برچسب‌های همگام با سرور |
| نشست‌ها | فقط `/me/sessions` شخصی؛ Endpoint مدیریتی وجود ندارد | صفحه جداگانه شخصی | مرز حفظ شد؛ به‌عنوان نیاز مستقل ذیل D-073 ثبت شد |
| Retention | D-070: تصمیم `Accepted` (تأیید مالک 2026-11-06)، پیاده‌سازی `NotImplemented` | — | فقط وضعیت ثبت شد |

## قابلیت‌های Backend موجود (ممیزی‌شده، بدون تغییر)
`Server/Api/SecurityAuditAdministrationEndpoints.cs` — هر چهار Endpoint، مدیر نصب فقط، Rate-limited (`ApiSensitivePolicy`)، `no-store`، Anti-forgery روی POST، Sort Key با Allowlist، صفحه‌بندی Clamped، Projection بدون IP/DetailsJson، Batch Load بازیگر/شرکت (بدون N+1)، ثبت `SecurityAuditExported`، CSV با UTF-8 BOM و خنثی‌سازی Formula Injection. نقص Backend اثبات‌نشده‌ای یافت نشد؛ این فایل تغییر نکرد.

## APIهای مصرف‌شده در UI (قرارداد واقعی)
1. `GET /api/v1/admin/security/audit-events?eventType=&userId=&companyId=&fromUtc=&toUtc=&sort=occurredAt|eventType&descending=true|false&page=&pageSize=` — مرتب‌سازی اکنون از UI ارسال می‌شود؛ تغییر مرتب‌سازی صفحه را به ۱ برمی‌گرداند؛ پاسخ دیررس درخواست قبلی با AbortController رد می‌شود.
2. `GET /api/v1/admin/security/audit-event-types` → `[{code,label}]` — پیشنهادهای فیلتر؛ کد ناشناخته با برچسب خودش (کد) حفظ می‌شود؛ در خطا، لیست ثابت همگام‌شده جایگزین می‌شود و گزارش قابل استفاده می‌ماند + دکمه تلاش دوباره (Fail-open امنیتی رخ نمی‌دهد؛ فیلتر دستی همچنان کار می‌کند).
3. `GET /api/v1/admin/companies?search=&page=&pageSize=` → `{items:[{id,name,isActive}],totalCount,page,pageSize}` — `CompanyPicker` جدید با جستجوی سمت سرور (الگوی ARIA combobox مثل `UserPicker`)؛ شرکت غیرفعال با نشان «(غیرفعال)» قابل فیلتر می‌ماند؛ انتخاب شرکت از سلول جدول همچنان به‌صورت Chip کار می‌کند.
4. `POST /api/v1/admin/security/audit-events/export` — دکمه «خروجی CSV» با همان فیلترهای اعمال‌شده؛ اعتبارسنجی محلی بازه (الزامی، ≤۳۱ روز) با `exportRangeProblem`؛ Loading و غیرفعال‌شدن دکمه در حین کار؛ مدیریت ۴۰۰/۴۰۳/۴۱۳/۵۰۰؛ دانلود با نام فایل `Content-Disposition` (پارسر امن `fileNameFromDisposition` که `..`، جداکنندهٔ مسیر و کاراکتر کنترلی را رد می‌کند)؛ `URL.revokeObjectURL` پس از دانلود؛ فیلترها در بدنهٔ POST می‌روند، نه در URL یا Log.

## خلاصه تغییرات
- `ClientApp/src/api/types.ts` — `SecurityAuditEventType` + `parseSecurityAuditEventTypes` (هیچ ورودی ناشناخته‌ای حذف نمی‌شود)، `AdminCompanyItem/AdminCompanyPage` + `parseAdminCompanies`.
- `ClientApp/src/api/client.ts` — `SecurityAuditQuery.sort/descending`؛ `getSecurityAuditEventTypes`؛ `AdminCompanyQuery` + `getAdminCompanies`؛ `SecurityAuditExportRequest` + `exportSecurityAuditEventsCsv` + `fileNameFromDisposition`.
- `ClientApp/src/lib/securityEvents.ts` — برچسب‌های ثابت عیناً با رجیstry سرور همگام شدند؛ کدهای واقعی نویستهٔ سرور که در رجیstry سرور برچسب ندارند (`WorkContextSelected`, `WorkContextChanged`, `CorporateGroupCreated`, `CorporateGroupMemberAdded`, `CorporateGroupMemberEnded`) حفظ شدند؛ `UserSessionRevoked`, `UserSessionsRevokedOthers`, `SecurityAuditExported`, `LicenseImported`, `InstallationIdentityEnrolled` افزوده شدند؛ تابع خالص `exportRangeProblem`.
- `ClientApp/src/components/admin/security/CompanyPicker.tsx` (جدید) — انتخاب‌گر شرکت نصب با جستجو و صفحه‌بندی سمت سرور.
- `ClientApp/src/components/admin/security/SecurityAuditPage.tsx` — اتصال مرتب‌سازی (ستون‌های «زمان» و «رویداد»، `aria-sort`، بازگشت به صفحه ۱)، دریافت انواع رویداد (Loading/Error/Retry)، جایگزینی فهرست شرکت عضویتی با `CompanyPicker`، دکمه و وضعیت خروجی CSV.
- تست‌ها: `securityAuditClient.test.ts` (قرارداد مرتب‌سازی، پارسرها، Export، نام فایل)، `CompanyPicker.test.tsx` (جدید)، `SecurityAuditPage.test.tsx` (مرتب‌سازی، انواع رویداد، شرکت نصب، Export، پاسخ دیررس)، `src/test/securityAudit.ts` (fixtureها)، `AppRouting.test.tsx` (فقط یک برچسب همگام با سرور: «ایجاد نقش شرکت»).
- Backend، Migration، وابستگی جدید و فایل‌های ممنوعه: **بدون تغییر**.

## تصمیم‌های فنی
- **برچسب‌ها:** کد و تصمیم معماری مبنا است (D-030)؛ برچسب فارسی سرور منبع حقیقت شد و لیست ثابت کلاینت فقط Fallback خطاست. برچسب‌های متفاوت قبلی کلاینت («ایجاد گروه دسترسی» و…) به نگارش سرور همگام شدند؛ یک Expectation در `AppRouting.test.tsx` نیز همگام شد.
- **انتخاب‌گر شرکت به‌جای Select:** فهرست عضویت‌های کاربر (`GET /companies`) برای گزارش نصب‌محور نادرست است؛ جستجو/صفحه‌بندی سمت سرور طبق مأموریت با الگوی `UserPicker` پیاده شد تا API تکراری ساخته نشود.
- **مرز نشست‌ها:** Endpoint مدیریتی نشست وجود ندارد؛ چیزی اختراع نشد. نیاز «مشاهده نشست همهٔ کاربران» به‌عنوان نیاز مستقل وابسته به D-073 ثبت می‌شود (هیچ استنتاجی از Audit درباره وضعیت نشست انجام نمی‌شود).
- **D-070:** تصمیم `Accepted` و پیاده‌سازی `NotImplemented` است؛ هیچ Purge، Job، Migration یا کنترل UI retention ساخته نشد.
- **Backend بدون نقص اثبات‌شده:** مطابق مأموریت `SecurityAuditAdministrationEndpoints.cs` تغییر نکرد.

## بررسی و آزمون (نتایج واقعی — 2026-11-06)

| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| `npm run typecheck` (ClientApp) | موفق — ۰ خطا | TypeScript strict شامل `exactOptionalPropertyTypes` |
| `npm test` (ClientApp) | موفق — **۷۵۴/۷۵۴** تست در ۷۳ فایل | شامل ۲۳ تست صفحه، ۲۰ تست API/پارسر، ۵ تست CompanyPicker |
| `npm run build` (ClientApp) | موفق — ۹۴۴ms | خروجی در `Server/wwwroot/app` |
| `npm run check:offline` | موفق — ۳۶ منبع محلی، بدون منبع بیرونی | D-048/D-049 |
| `dotnet restore Template.sln` | موفق | SDK 10.0.403 |
| `dotnet build Template.sln --no-restore --configuration Release` | موفق — ۰ خطا (۲۸۶۴ هشدار موجودِ قبلی) | |
| `dotnet test --filter FullyQualifiedName~SecurityAuditAdministration` | **۹ موفق، ۰ ناموفق، ۱ Skip** | Skip: تست SQL Server (نیازمند `FARANOU_TEST_SQLSERVER`) |
| `dotnet test Template.sln --no-build --configuration Release` (کل) | ۱۰۳۸ موفق، ۲ ناموفق، ۶۲ Skip | ۲ ناموفق = `SafeExceptionRealApplicationTests` (راه‌اندازی برنامهٔ واقعی نیازمند SQL Server)؛ **روی main تمیز نیز دقیقاً همین ۲ تست ناموفق است** (با `git stash` اثبات شد) — پیش‌موجود و محیط‌مند، بدون ارتباط با این تغییر |
| `scripts/dev/test-sqlserver.sh up` | اجرا نشده | Docker در محیط اجرا در دسترس نیست |
| `python3 scripts/check_static_assets.py` | اجرا نشده | جایگزین معادل پروژهٔ کلاینت: `npm run check:offline` (موفق) |
| `git diff --check` | موفق | بدون خطای whitespace |
| E2E (`tests/e2e`) | اجرا نشده | سناریوی E2E اختصاصی گزارش امنیتی در مخزن وجود ندارد؛ تغییر API رخ نداده تا تست E2E نیاز به اصلاح داشته باشد |

## امنیت، داده و سازگاری
- Authorization همچنان فقط سمت سرور (`AuthorizeInstallationAdministratorAsync`)؛ UI هیچ کنترل امنیتی جدیدی از خود نساخت و مجوزی را فرض نکرد (پاسخ‌های ۴۰۳ با پیام اختصاصی نمایش داده می‌شوند).
- فیلترهای Export در بدنهٔ POST با توکن Anti-forgery ارسال می‌شوند؛ در URL، Log یا Console ثبت نمی‌شوند.
- هیچ DTO جدیدی فیلد حساسی (IP، DetailsJson، توکن) اضافه نکرد؛ پارسرها فقط Allowlist فیلدها را می‌خوانند.
- رویداد ناشناخته هرگز پنهان نمی‌شود؛ فقط برچسبش کد خام است (رفتار Fail-safe، نه Fail-open).
- بدون Migration، بدون تغییر Schema، بدون وابستگی جدید.

## ریسک‌ها و کارهای باقی‌مانده
- تست Integration SQL Server و ۲ تست برنامهٔ واقعی، محیط SQL Server می‌خواهند؛ در CI (که SQL Server دارد) باید سبز شوند — وضعیت واقعی CI پس از باز شدن PR در گزارش اعلام می‌شود.
- نیاز مستقل (خارج از این PR): Endpoint مدیریتی مشاهده نشست‌های همهٔ کاربران، مشمول تصمیم D-073 (ابطال مدیریتی، مرز داده نشست). تا آن زمان «گزارش امنیتی» فقط رویداد را نشان می‌دهد، نه نشست فعال را.
- D-070: طبقه‌بندی/مدت‌های نگهداری تصویب شده اما هیچ Job آرشیو/Purge پیاده‌سازی نشده؛ هر پیاده‌سازی آینده نیازمند PR اجرایی جدا با مجوز `security.audit.retention.manage` است.
- سناریوهای E2E برای Export و مرتب‌سازی می‌تواند در یک PR آزمون E2E جداگانه افزوده شود (تغییر قرارداد رخ نداده، عجله‌ای نیست).

## شناسه‌های Git
- Branch: `feature/security-audit-ui-completion` (از `origin/main` @ `265e449f`)
- Commit: پس از Push ثبت می‌شود.
- Pull Request: پس از ایجاد ثبت می‌شود؛ Merge فقط با اجازهٔ مالک.
