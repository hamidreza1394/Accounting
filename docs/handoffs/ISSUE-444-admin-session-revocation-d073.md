# تحویل وظیفه — ابطال مدیریتی نشست کاربر توسط مدیر نصب (D-073 §1)

- **Issue:** #444 (زیرمجموعه؛ تصمیم مرجع: D-073 بخش ۱ — مأموریت جامع تکمیل مدیریت کاربران/سازمان/دسترسی‌ها)
- **Agent/مسئول:** GLM (Z.ai)
- **تاریخ:** 2026-11-06
- **وضعیت:** انجام‌شده — سه Endpoint Backend + تست‌ها + قرارداد API؛ بدون Migration و بدون تغییر UI
- **مبنای ممیزی:** آخرین `origin/main` @ `129b93b5` (سند ممیزی جامع: `docs/handoffs/ISSUE-444-comprehensive-access-completion-audit.md`)

## هدف و معیارهای پذیرش

- [x] فقط مدیر نصب فعال مجاز است؛ نقش Admin در هر درخواست از دیتابیس بازخوانی می‌شود (`AuthorizeInstallationAdministratorAsync` موجود، بدون Claim قدیمی Cookie)
- [x] مشاهده نشست‌های فعال کاربر هدف با Projection حداقلی Allowlist (بدون IP، بدون User-Agent، بدون Cookie/Token/Secret)
- [x] ابطال یک نشست مشخص و ابطال همهٔ نشست‌های فعال کاربر هدف؛ هر دو Idempotent
- [x] Step-up Authentication با الگوی امن موجود پروژه: تأیید رمز جاری خودِ مدیر با `PasswordHashService.Verify` (همان سازوکار تغییر رمز)؛ هیچ مکانیزم جعلی ساخته نشد
- [x] دلیل ابطال فقط از کاتالوگ کدهای مجاز سرور انتخاب می‌شود (`admin_revoked_suspected_compromise` / `admin_revoked_offboarding` / `admin_revoked_user_request` / `admin_revoked_administrative`)
- [x] Audit با Actor، کاربر هدف، Scope، زمان UTC، شمارش و نتیجه؛ `AdminUserSessionRevoked` و `AdminUserSessionsRevoked`؛ بدون رمز/توکن در Audit یا Log
- [x] ابطال در درخواست بعدی واقعاً به 401 می‌رسد (SessionValidator موجود D-069 — بدون تغییر)
- [x] Anti-forgery، Rate Limit حساس و no-store در همهٔ Endpointها
- [x] IP و User-Agent جدید به UserSession اضافه نشد؛ Projection فقط فراداده حداقلی
- [x] مدیر شرکت و کاربر عادی هیچ اختیاری ندارند؛ هیچ مجوز CompanyRole این قابلیت را نمی‌دهد (D-073 §1)
- [x] ابطال اضطراری همهٔ نشست‌های Installation پیاده‌سازی نشد (خارج از تصمیم)
- [x] بدون Migration جدید، بدون جدول/ساختار موازی (همان `UserSession` دمصوب D-069 استفاده شد)
- [x] Idempotency، Concurrency و رفتار واقعی روی SQL Server با تست SQL-gated پوشش داده شد

## خلاصه تغییرات

| Endpoint | توضیح |
|---|---|
| `GET /api/v1/admin/users/{userId}/sessions` | فهرست نشست‌های فعال کاربر هدف؛ `[ { sessionId, createdAtUtc, lastSeenAtUtc } ]` مرتب بر آخرین استفاده |
| `POST /api/v1/admin/users/{userId}/sessions/{sessionId}/revoke` | ابطال یک نشست؛ بدنه `{ currentPassword, reasonCode }`؛ `204` idempotent؛ `404` یکسان ناموجود/بیگانه؛ ابطال نشست جاری خود مدیر → sign-out |
| `POST /api/v1/admin/users/{userId}/sessions/revoke-all` | ابطال همهٔ نشست‌های فعال هدف؛ `200 { revokedCount }`؛ تکرار → `0` بدون Audit دوم |

- `RevokeAsync`/`RevokeOthersAsync` پارامتر اختیاری `reasonCode` گرفتند (رفتار self-service با پیش‌فرض قبلی دقیقاً بدون تغییر) و `RevokeAllAsync` جدید افزوده شد — هر دو همان نوشتن مجموعه‌ای (ExecuteUpdate) با گارد مالکیت در Prediccate؛ مسیر InMemory تست‌ها حفظ شد.

## فایل‌های مهم تغییرکرده

- جدید — `Server/Api/AdminUserSessionEndpoints.cs` (سه Endpoint، کاتالوگ دلیل، Step-up، Audit)
- جدید — `tests/Template.Tests/AdminUserSessionEndpointTests.cs` (۱۰ تست رفتاری)
- جدید — `tests/Template.Tests/Integration/AdminUserSessionSqlServerTests.cs` (تست SQL Server-gated: ترجمه ExecuteUpdate، idempotency، 404 بیگانه، Audit)
- ویرایش — `Server/Infrastructure/Security/UserSessionService.cs` (پارامتر `reasonCode` + `RevokeAllAsync`)
- ویرایش — `Server/Api/ApiEndpoints.cs` (ثبت گروه Endpoint)
- ویرایش — `Server/Api/SecurityAuditAdministrationEndpoints.cs` (برچسب فارسی دو رویداد جدید در رجیستری گزارش امنیتی)
- ویرایش — `ClientApp/src/lib/securityEvents.ts` (همگام‌سازی برچسب‌های Fallback کلاینت با سرور؛ هیچ صفحه/منویی اضافه نشد)
- ویرایش — `docs/api/USER_ACCESS_MANAGEMENT_API.md` (بخش ۸.۵ قرارداد مصرف Frontend) و `docs/SECURITY.md` (وضعیت کد D-073 §1)

## تصمیم‌های فنی

- **Re-auth بدون اختراع:** تنها سازوکار امن موجود در پروژه، تأیید رمز جاری در جریان تغییر رمز است؛ همان الگو برای Step-up مدیر استفاده شد (تأیید در برابر Hash ذخیره‌شده؛ پاسخ یکسان 403 برای رمز غلط/خالی بدون افشای دلیل). MFA/TOTP رقب-آپ در Customer موجود نیست و طبق مأموریت جعل نشد؛ افزودن آن به این عملیات به مرحله MFA (D-073 §4) موکول می‌شود.
- **مسیر خود مدیر:** ابطال نشست‌های خود مدیر از مسیر مدیریتی هم ممکن است (D-073 منع نکرده)؛ اگر نشست جاری ابطال شود همان درخواست Cookie را sign-out می‌کند؛ مدیریت نشست شخصی همچنان توصیه و مستندشده مسیر self-service است.
- **Audit `UserId` = مدیر actor** (هم‌الگو با `UserCreated`/`UserUpdated`) و `targetUserId` در DetailsJson؛ `EntityId` برای ابطال تکی شناسه نشست و برای revoke-all شناسه کاربر هدف است.
- تصمیم معماری جدیدی (ADR جدید) گرفته نشد؛ دقیقاً D-073 §1 اجرا شد و D-069 دست‌نخورده ماند.

## بررسی و آزمون (نتایج واقعی — 2026-11-06)

| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| `dotnet build Template.sln -c Release` | موفق — ۰ خطا | هشدارهای موجودِ قبلی (۲۸۶۴) |
| `dotnet test Template.sln --no-build -c Release` (کل) | **۱۰۴۸ موفق، ۲ ناموفق، ۶۳ Skip** | +۱۰ تست جدید نسبت به پایه ۱۰۳۸؛ دو ناموفق همان `SafeExceptionRealApplicationTests` پیش‌موجود نیازمند SQL Server روی main تمیز است |
| `dotnet test --filter FullyQualifiedName~AdminUserSession` | **۱۰ موفق، ۱ Skip** | Skip = تست SQL Server (نیازمند `FARANOU_TEST_SQLSERVER`) |
| `npx vitest run` (ClientApp کل) | **۷۸۳/۷۸۳ موفق (۷۴ فایل)** | برچسب‌های Fallback همگام؛ هیچ تستی حذف/تضعیف نشد |
| `npm run typecheck` | موفق — ۰ خطا | |
| SQL Server واقعی محلی | اجرا نشده | Docker در محیط نیست؛ تست SQL-gated در CI اجرا می‌شود و نتیجه در گزارش PR ثبت می‌شود |
| E2E | اجرا نشده / لازم به تغییر نیست | Endpointهای E2E موجود تغییری نکردند؛ سناریوی E2E صفحه مدیریتی نشست پس از ساخت UI (مرحله Frontend مأموریت) افزوده می‌شود |

## امنیت، داده و سازگاری

- Authorization صددرصد سمت سرور؛ بازخوانی نقش از DB در هر درخواست (تست «غیرفعال‌شدن نقش میان دو درخواست» این را قفل می‌کند).
- مالکیت نشست در Prediccate بروزرسانی است؛ نشست کاربر دیگر با هر شناسه 404 یکسان می‌گیرد (بدون Probing) و Audit بی‌اثر می‌ماند.
- بدون Migration، بدون تغییر Schema، بدون وابستگی جدید، بدون تغییر رفتار self-service موجود.
- کوکی/Token/Secret/IP/UA در پاسخ‌ها و Audit برنمی‌گردند؛ رمز مدیر فقط برای Verify استفاده و هیچ‌جا ذخیره/لاگ نمی‌شود.

## ریسک‌ها و کارهای باقی‌مانده

- نتیجه تست SQL Server-gated و E2E پس از CI این PR ثبت می‌شود.
- صفحه UI «مدیریت نشست سایر کاربران» در مرحله Frontend مأموریت ساخته می‌شود؛ قرارداد مصرف کامل در بخش ۸.۵ `docs/api/USER_ACCESS_MANAGEMENT_API.md` آماده است.
- Step-up مبتنی بر MFA (به‌جای رمز) پس از پیاده‌سازی MFA مرحله D-073 §4 به این عملیات اضافه خواهد شد.

## شناسه‌های Git

- Branch: `feat/admin-session-revocation-d073`
- Pull Request: پس از Push ثبت می‌شود
