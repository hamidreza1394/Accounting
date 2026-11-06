# گزارش ممیزی و تکمیل گردش درخواست عضویت شرکت‌ها

- **Issue:** UNNUMBERED (مأموریت «تکمیل گردش درخواست عضویت شرکت‌ها — Priority 2»)
- **Agent/مسئول:** GLM 5.3
- **تاریخ:** 2026-11-06
- **وضعیت:** انجام‌شده (ممیزی کامل + اصلاح مستندات؛ هیچ نقص اجرایی در کد یافت نشد)
- **مبنای ممیزی:** `main` @ `e1529718bfed250972be8d962d364b69f7c74d25` (Merge PR #557)

## هدف و معیارهای پذیرش

- [x] API کشف شرکت‌های قابل درخواست کامل و امن باشد — `Verified` (از قبل در main پیاده‌شده؛ بخش «ماتریس API»)
- [x] فرم ثبت درخواست عضویت قابل استفاده باشد — `Verified` (`CreateMembershipRequest` در صفحهٔ «درخواست‌های عضویت من»)
- [x] درخواست‌های تکراری کنترل شوند — کنترل دوخطی: بررسی Query + ایندکس یکتای فیلترشده (پاسخ 409)؛ تست endpoint و E2E
- [x] درخواست‌های من و صف تأیید به‌درستی کار کنند — `Verified` (UI + API + تست)
- [x] تأییدکنندهٔ غیرمدیر مجاز بتواند نقش‌های مجاز را دریافت کند — `Verified` (`GET .../assignable-roles` با سقف تفویض؛ اصلاح PR #550)
- [x] تأییدکننده نتواند خارج از سطح اختیار خود نقش تخصیص دهد — سمت سرور با `TrackedDelegationCeiling` و گاردهای RowVersion داخل تراکنش؛ تست واحد و تست SQL Server
- [x] تمام کنترل‌های امنیتی در Backend اعمال شوند — anti-forgery، rate limit، no-store، DTO allowlist، 404 یکسان بدون افشا
- [x] Tenant Isolation اثبات شود — تست‌های مرز مشتری، شرکت بیگانه و دست‌کاری شناسه (endpoint + E2E)
- [x] Audit Event مطابق الگوی پروژه ثبت شود — `CompanyMembershipRequested/Approved/Rejected` در `SecurityAuditEvents`
- [x] تست‌های Backend و Client اجرا شوند — بخش «نتایج اجرای واقعی تست‌ها»
- [△] تست E2E با SQL Server واقعی — در CI main سبز (job `e2e`)؛ در این محیط اجرا نشد (بدون Docker/SQL Server) — بخش «محدودیت‌ها»
- [x] هیچ تغییر غیرضروری در Database — صفر تغییر Schema؛ تأیید `verify_migration_model.py`
- [x] هیچ فایل مشترک ممنوعه تغییر نکرده باشد — فقط ۲ فایل مستندات، خارج از مالکیت عامل موازی
- [x] هیچ تعارض حل‌نشده‌ای در Branch نباشد — بر پایهٔ آخرین main و بدون تماس با فایل‌های PR #559
- [x] Handoff اختصاصی تکمیل شده باشد — همین سند
- [x] PR مستقل و قابل بررسی ایجاد شده باشد — PR فقط-مستندات با شواهد

## وضعیت اولیه قابلیت (یافته ممیزی)

قابلیت گردش درخواست عضویت در `main` **قبلاً تکمیل و تثبیت شده است** و نیازمند بازنویسی یا تکمیل کد نبود. PRهای مبنای ادغام‌شده: #479 (Backend گردش)، #535/#536 (E2E مثبت)، #530 (رفع رفتار جستجو)، #541 (سقف تفویض شرکت‌محور)، #550 (منع واگذاری نقشِ حامل مجوز تأیید)، #553 (انتشار قرارداد مرجع `docs/api/USER_ACCESS_MANAGEMENT_API.md` بخش ۵). هیچ PR بازیِ مرتبط با عضویت وجود نداشت؛ تنها PR هم‌زمان بازی (#559 — ادغام HR Frontend در Shell) به عامل موازی تعلق دارد و مالکیت فایل‌هایش در این کار رعایت شد.

### ماتریس API (استخراج از کد واقعی `Server/Api/CompanyMembershipRequestEndpoints.cs`)

| # | عملیات | Method | Route | مجوز/شرط | وضعیت |
|---|---|---|---|---|---|
| ۱ | کشف شرکت‌های قابل درخواست | GET | `/api/v1/me/membership-requests/destinations?search=&page=&pageSize=` | حساب User فعال؛ `ApiSensitivePolicy` (rate limit)؛ جستجوی exact اجباری ۳..۱۲۰ نویسه؛ فقط شرکت فعال با `CustomerBoundaryId` مشترک؛ حذف عضو فعال و Pending تکراری؛ fail-closed روی مرز null | `Verified` |
| ۲ | درخواست‌های من | GET | `/api/v1/me/membership-requests?status=&page=&pageSize=` | حساب User؛ فیلتر وضعیت Allowlist؛ صفحه‌بندی clamp | `Verified` |
| ۳ | ثبت درخواست | POST | `/api/v1/me/membership-requests` | حساب User + anti-forgery + rate limit؛ بازبینی مرز مشتری سمت سرور (POST با شناسه دستکاری‌شده → 404 یکسان)؛ 409 برای عضو فعال/Pending تکراری (ایندکس یکتا)؛ Audit `CompanyMembershipRequested` | `Verified` |
| ۴ | صف تأیید مقصد | GET | `/api/v1/admin/companies/{companyId}/membership-requests/pending` | مدیر نصب (D-071) یا عضو فعال دارای `users.membership.approve` در همان شرکت؛ غیرعضو/بی‌مجوز → 404 یکسان | `Verified` |
| ۵ | نقش‌های قابل تخصیص | GET | `.../assignable-roles?page=&pageSize=` | همان ۴؛ برای تأییدکنندهٔ شرکتی فقط نقش‌های فعال غیرسیستمیِ داخل سقف تفویض و **بدون** نقش‌های حامل مجوز تأیید | `Verified` |
| ۶ | فهرست تأییدکننده‌ها | GET | `.../approvers` | همان ۴؛ خروجی Allowlist (نام نمایشی/username) | `Verified` |
| ۷ | تاریخچهٔ تصمیم‌ها | GET | `.../membership-requests?status=` | همان ۴؛ UTC صریح؛ صفحه‌بندی | `Verified` |
| ۸ | تأیید | POST | `.../{requestId}/approve` | همان ۴ + anti-forgery؛ تراکنش ReadCommitted؛ بازخوانی مجوز/سقف داخل تراکنش؛ گارد RowVersion روی سقف، نقش و درخواست؛ ممنوعیت self-approval (403)، نقش سیستمی (403)، نقش حامل مجوز تأیید (403)، نقش بیگانه/غیرفعال؛ Audit `CompanyMembershipApproved`؛ هم‌زمانی → 409 | `Verified` |
| ۹ | رد | POST | `.../{requestId}/reject` | همان ۴ + anti-forgery؛ توضیح ≤۵۰۰ نویسه؛ پس از رد، درخواست جدید مجاز؛ Audit `CompanyMembershipRejected` | `Verified` |

منبع قرارداد مصرف Frontend: `docs/api/USER_ACCESS_MANAGEMENT_API.md` بخش ۵ (به‌روز و منطبق بر کد؛ هیچ DTO یا فیلدی حدس زده نشده است). اجزای عمداً ناموجود (انقضای Pending، لغو/تعلیق عضویت، endpoint مستقل انتقال عضویت) همان بخش ۵.۵ قرارداد، «تصمیم محصولی» است و در این مأموریت ساخته نشد.

### امنیت و Tenant Isolation (شواهد)

- هویت درخواست‌کننده فقط از Principal (`GetUserId`) و مجوز فقط از `CompanyAccessService.HasPermissionAsync` روی DB؛ هیچ اعتمادی به `CompanyId`/`UserId` بدنه نیست.
- مرز مشتری (CustomerBoundary) در GET کشف و POST ثبت هر دو سمت سرور بازبینی می‌شود؛ شرکت خارج از مرز، بدون مرز و ناموجود پاسخ 404 یکسان می‌گیرند (بدون افشای وجود).
- 403 برای self-approval و نقش ممنوع؛ 404 برای عدم مجوز صف؛ `no-store` روی همه؛ anti-forgery روی POSTها؛ rate limit روی discovery و ثبت.
- ثبت Audit در همان `SaveChanges` عملیات؛ بدون داده حساس در پیام‌ها.

### تغییرات UI (موجود در main — این PR چیزی به UI اضافه نکرد)

- `/account/membership-requests` — «درخواست‌های عضویت من»: فرم ثبت با جستجوی exact، حالت‌های Loading/Empty/Error، جلوگیری از ارسال چندباره (`busy`)، حفظ نتیجه برای تلاش مجدد، صفحه‌بندی.
- `/admin/membership-requests` — «صف تأیید مقصد»: جدول صف، دیالوگ تأیید با `assignable-roles` و پیام اختصاصی برای تأییدکنندهٔ بدون فهرست، رد با توضیح، مدیریت 403/404/409، انتقال فوکوس پس از تصمیم.
- منو در `AppShell` با گیت `users.membership.approve`؛ مسیرها در `AppRoutes` با lazy loading (هر دو مالکیت مشترک/عامل موازی: دست‌نخورده).

## مشکلات واقعی کشف‌شده و اقدام انجام‌شده

۱. **سطر کهنه در `docs/ADMIN_UI.md` (جدول بخش ۱۰):** می‌گفت فرم «ثبت درخواست عضویت» ساخته نشده، API کشف وجود ندارد و تأییدکنندهٔ غیرمدیر نمی‌تواند گروه‌ها را بخواند — هر سه خلاف کد فعلی main (فرم و API در #510 پیاده و E2E در #536 پوشش داده شد؛ نقش‌های قابل تخصیص در #550 اصلاح شد). طبق D-030 (کد منبع حقیقت است) سطر اصلاح و وضعیت `Verified` با شواهد ثبت شد.
۲. **نبود گزارش تکمیلی اختصاصی این مأموریت:** همین سند ایجاد شد.
۳. **نقص اجرایی در کد Backend/Client/Test:** شناسایی نشد — جزئیات در «نتایج اجرای واقعی تست‌ها».

## قراردادهای API تغییرکرده

هیچ قراردادی تغییر نکرد. هیچ Endpoint، DTO، فیلد، Permission یا Migration جدیدی ایجاد نشد (اصل «قابلیت موجود را دوباره نساز»).

## مجوزهای استفاده‌شده

فقط مجوز موجود `users.membership.approve` (صف تأیید/نقش‌های قابل تخصیص/تأیید/رد) و حساب User احراز‌شده برای مسیرهای `/me/membership-requests*`. Permission جدیدی تعریف نشد.

## تغییرات این PR

- `docs/ADMIN_UI.md` — اصلاح سطر کهنهٔ «عضویت‌ها و جریان تأیید» بر اساس کد و شواهد تست.
- `docs/handoffs/MEMBERSHIP_REQUEST_WORKFLOW_COMPLETION.md` — همین گزارش.

Backend، Client، Schema، Migration و فایل‌های مشترک (`AppRoutes.tsx`، `AppShell.tsx`، `App.tsx`، `lib/permissions.ts`، `useCompanyPermissions`، منو، `features/hr/**`، API client سراسری) **دست نخوردند**.

## نتایج اجرای واقعی تست‌ها (این محیط، بدون تغییر کد)

| آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| `dotnet restore Template.sln` | موفق | SDK 10.0.403 (اسکریپت `scripts/dev/install-dotnet.sh` طبق `global.json`) |
| `dotnet test Template.sln -c Release` | ۱۱۰۲ تست: **۱۰۳۸ موفق، ۶۲ Skip، ۲ ناموفق** | دو ناموفق = `SafeExceptionRealApplicationTests` که محلی بدون SQL Server به Provider غیرrelational می‌خورد؛ وابسته به محیط است و روی main تمیز (درخت بدون تغییر) هم همین رفتار را دارد؛ در CI با `FARANOU_REQUIRE_SQLSERVER=1` سبز است |
| تست‌های کلاس‌های `*CompanyMembershipRequest*` | **۲۴ تست: ۲۱ موفق، ۳ Skip، ۰ ناموفق** | سه Skip = تست‌های SQL Server-gated (`FARANOU_TEST_SQLSERVER` در این محیط تنظیم نیست) |
| `BootstrapMembershipApprovalTests` | **۶/۶ موفق** | bootstrap مدیر نصب |
| `npm ci` + `npm test --run` (کلاینت) | **۷۲ فایل، ۷۲۷/۷۲۷ موفق** | شامل ۲۰ تست مؤلفه‌های membership (۳ + ۴ + ۱۳) |
| `npm run typecheck` | موفق | strict |
| `npm run build` | موفق | Vite |
| `npm run check:offline` | موفق | ۳۶ منبع محلی، بدون CDN |
| `python3 scripts/verify_migration_model.py` | موفق | «Snapshot و هر ۱۶ Migration با مدل EF مطابق‌اند (Up و Down)» — معادل `has-pending-model-changes`؛ نیاز به Migration جدید: **خیر** |

### وضعیت CI روی main (شواهد واقعی، GitHub Actions)

- Run `.NET CI` روی `e1529718` (push به main): **موفق — هر ۷ job**: `client`، `secret-scan`، `publish-scripts`، `build-and-test`، `publish-windows`، `publish`، `e2e` (E2E با SQL Server واقعی سرویس-کانتینر؛ شامل `membership-workflow.spec.mjs`).
- Semgrep و CodeQL/secret-scan: موفق. نتیجه CI این PR پس از باز شدن، از گزارش نهایی جداگانه پیگیری می‌شود و بدون نتیجه واقعی ادعایی ثبت نمی‌شود.

### وضعیت E2E و SQL Server در این محیط

- Docker/SQL Server در این Sandbox در دسترس نیست؛ تست‌های `CompanyMembershipRequestSqlServerTests` (برنده‌ی تأیید هم‌زمان، تعارض RowVersion سقف تفویض) و `CompanyMembershipRequestReadSqlServerTests` اینجا **اجرا نشدند** (Skip خودکار) — اما همین تست‌ها در job `e2e`/`build-and-test` CI با SQL Server واقعی روی `e1529718` سبز بوده‌اند. هیچ تستی حذف، Skip یا تضعیف نشد.

## Migration و Database

هیچ Migration جدیدی لازم نبود و ساخته نشد؛ `verify_migration_model.py` (معادل پروژهٔ `dotnet ef migrations has-pending-model-changes` با اعتبارسنجی سینتکس و Up/Down) تأیید کرد Snapshot و ۱۶ Migration موجود با مدل EF سازگارند. ایندکس یکتای فیلترشدهٔ ضد-درخواست-تکراری از Migration `20261103111108_AddCompanyMembershipRequests` برقرار است.

## تصمیم‌های فنی

۱. **بدون تغییر کد:** ممیزی نشان داد قابلیت کامل است؛ طبق دستور مأموریت، PR خالی/نمایشی کد ساخته نشد و خروجی، ممیزی مبتنی بر شواهد + اصلاح حقیقت مستندات است.
۲. **عدم ویرایش `docs/WORK_LOG.md`:** PR موازی #559 (عامل هماهنگ HR) همین حالا انتهای `WORK_LOG.md` را ویرایش می‌کند؛ برای پرهیز از تعارض در فایل مشترک، طبق بند جایگزین مأموریت، گزارشِ الزامی در همین handoff مستقل ثبت شد. افزودن خلاصه به `WORK_LOG.md` به مرحلهٔ ادغام سپرده می‌شود (یک بلوک تکرارشدنی در بخش «راهنمای ادامه کار»).
۳. **محدودهٔ سطر کهنهٔ `GET /me/permissions` در `ADMIN_UI.md` (خط جدول بخش ۱۰):** این سطر نیز با کد فعلی ناسازگار است (`Implemented` از PR #554) اما مالکیتش با مأموریت مدیریت دسترسی (عامل موازی) است؛ برای پرهیز از تداخل، فقط در «راهنمای ادامه کار» ثبت شد و دستکاری نشد.
۴. تصمیم معماری جدیدی گرفته نشد؛ هیچ ADR جدید لازم نشد.

## ریسک‌ها و محدودیت‌های باقی‌مانده

- axe/صفحه‌خوان برای صفحات membership هنوز اجرا نشده (بدهی QA ثبت‌شده در `ADMIN_UI.md` بخش ۱۱).
- انقضای درخواست Pending، لغو/تعلیق عضویت و انتقال مستقل عضویت: `NotImplemented` با تصمیم محصولی (قرارداد §۵.۵) — نیازمند تصمیم مالک، نه کار Agent.
- دو تست `SafeExceptionRealApplicationTests` در محیط بدون SQL Server ناموفق‌اند (رفتار محیطی main، مرتبط با مأموریت نیست؛ در CI سبز).
- جستجوی شرکت فقط exact است: تصمیم امنیتی مستند (پیشگیری از enumeration)؛ تغییر آن نیازمند تصمیم محصولی است.

## راهنمای ادامه کار

۱. **پس از Merge این PR و PR #559:** یک بلوک خلاصه به انتهای `docs/WORK_LOG.md` اضافه شود؛ نمونهٔ آماده:
   `## 2026-11-06 — ممیزی گردش درخواست عضویت (feature/membership-request-workflow)` با یک خط: «قابلیت روی main Verified شد؛ بدون تغییر کد؛ سطر کهنهٔ ADMIN_UI اصلاح شد؛ گزارش در docs/handoffs/MEMBERSHIP_REQUEST_WORKFLOW_COMPLETION.md».
۲. **برای مأموریت مدیریت دسترسی:** سطر `GET /me/permissions` در جدول بخش ۱۰ `docs/ADMIN_UI.md` هنوز `NotImplemented` ثبت شده و باید به `Implemented` (PR #554) به‌روز شود — خارج از محدودهٔ این PR.
۳. **QA دسترس‌پذیری:** اجرای axe و صفحه‌خوان روی `/account/membership-requests` و `/admin/membership-requests` (بدهی موجود، وابسته به زیرساخت مرورگر).
۴. **Frontend می‌تواند بدون تغییر Backend کار کند:** قرارداد مصرف، `docs/api/USER_ACCESS_MANAGEMENT_API.md` بخش ۵ است؛ همهٔ ۹ Endpoint با DTOهای همان سند Verified شده‌اند.

## شناسه‌های Git

- Branch: `feature/membership-request-workflow` (از `origin/main` @ `e1529718bfed250972be8d962d364b69f7c74d25`)
- Commit: در بخش «شناسه‌های Git» PR ثبت می‌شود
- Pull Request: `feat(access): complete company membership request workflow` (فقط-مستندات؛ Merge فقط با اجازهٔ مالک)
