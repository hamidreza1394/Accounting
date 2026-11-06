# تحویل وظیفه — هم‌ترازی سیاست رمز عبور با D-073 §5 (حداقل ۱۵)

- **Issue:** #444 (تصمیم مرجع: D-073 بخش ۵ — مأموریت جامع تکمیل مدیریت کاربران/سازمان/دسترسی‌ها)
- **Agent/مسئول:** GLM (Z.ai)
- **تاریخ:** 2026-11-06
- **وضعیت:** انجام‌شده — حداقل طول رمز جدید/تغییریافته از ۸ به **۱۵** و هم‌ترازی همهٔ نقاط اعمال‌کننده؛ بدون Migration
- **مبنای ممیزی:** آخرین `origin/main` @ `129b93b5` (ممیزی جامع: PR #562)

## هدف و معیارهای پذیرش

- [x] حداقل طول رمز جدید یا تغییرکردنه = ۱۵ (`Constants.MinLength.UserPassword`) — فقط در مسیر ایجاد/تغییر رمز؛ **هیچ حساب قدیمی قفل یا مجبور به چرخش نمی‌شود** (Login فقط hash را می‌سنجد)
- [x] حداکثر ۱۲۸ بدون تغییر (`Constants.MaxLength.UserPassword`)
- [x] عبارت عبور و فاصله پذیرفته می‌شود؛ بدون قاعده ترکیبی (تست «correct horse battery staple» اضافه شد)؛ بدون چرخش دوره‌ای اجباری
- [x] Blocklist رمزهای رایج بدون تغییر حفظ شد و در همهٔ مسیرها اعمال می‌شود — **از جمله ثبت‌نام اولین مدیر** که قبلاً فقط طول ۱۲..۱۲۸ را چک می‌کرد
- [x] همهٔ نقاط اعمال‌کننده Backend از یک ثابت واحد تغذیه می‌شوند (CreateUser API، تغییر رمز self-service، Razor Register/Create، Provisioning Enrollment)
- [x] همهٔ نقاط Client (فرم تغییر رمز، فرم ایجاد کاربر، صفحه ثبت‌نام اولین مدیر) هم‌زمان هم‌تراز شدند
- [x] بدون Migration، بدون تغییر Schema، بدون وابستگی جدید

## خلاصه تغییرات

| نقطه | پیش | پس |
|---|---|---|
| `Constants/MinLength.cs → UserPassword` | ۸ | **۱۵** (D-073 §5) |
| `Server/Infrastructure/Provisioning/CustomerProvisioningApplyService.cs` (ثبت‌نام اولین مدیر) | طول ۱۲..۱۲۸ hardcode، بدون blocklist | همان ثابت‌های ۱۵/۱۲۸ + `NotCommonPasswordAttribute` |
| `ClientApp/src/components/account/SecurityTab.tsx` (تغییر رمز شخصی) | minLength = 8 | ۱۵ |
| `ClientApp/src/components/admin/users/userFormValidation.ts` (فرم ایجاد کاربر) | passwordMin = 8 | ۱۵ |
| `ClientApp/src/components/ProvisioningEnrollmentPage.tsx` | «حداقل ۱۲» و minLength=12 | «حداقل ۱۵» و minLength=15 |

- پیام‌های خطای Razor با منابع `Resources.Messages.Validations.MinLength` پارامتریک هستند و خودکار عدد جدید را نشان می‌دهند.
- مستندات: `docs/USER_SELF_SERVICE.md` حداقل ۱۵ + شفاف‌سازی «فقط رمز جدید/تغییریافته؛ حساب قدیمی قفل نمی‌شود». `docs/SECURITY.md` از قبل ۱۵ را مستند داشت (کد به سند رسید).

## فایل‌های مهم تغییرکرده

- `Constants/MinLength.cs` — ثابت ۱۵ با ارجاع D-073 §5
- `Server/Infrastructure/Provisioning/CustomerProvisioningApplyService.cs` — هم‌ترازی enrollment
- سه فایل Client + پنج فایل تست Client/Server — هم‌ترازی و تست مرزی
- `docs/USER_SELF_SERVICE.md`، همین handoff و `docs/WORK_LOG.md`

## تصمیم‌های فنی

- **بدون اختراع سیاست جدید:** فقط حداقل طول تغییر کرد؛ blocklist موجود دست‌نخورده ماند (D-073 §5 دقیقاً همین را می‌خواهد). ردِ کوتاه‌ها روی مسیرهای موجود (`PasswordPolicyRejected` / خطای DataAnnotation / `enrollment_invalid` یکسان) برمی‌گردد — بدون افشای جدید.
- **رفتار مهاجرت:** حساب‌های موجود فقط هنگام ایجاد/تغییر رمز بعدی سیاست جدید را می‌بینند؛ هیچ دورهای اجباری، انقضا یا تغییر CredentialStamp در این PR نیست.
- تصمیم معماری جدیدی گرفته نشد؛ اجرای دقیق D-073 §5 است.

## بررسی و آزمون (نتایج واقعی — 2026-11-06)

| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| `dotnet build Template.sln -c Release` | موفق — ۰ خطا | |
| `dotnet test Template.sln -c Release` (کل) | **۱۰۴۰ موفق، ۲ ناموفق پیش‌موجود، ۶۲ Skip** | دو ناموفق همان `SafeExceptionRealApplicationTests` نیازمند SQL Server (روی main تمیز هم همین است)؛ نسبت به پایه ۱۰۳۸ → ۱۰۴۰ (+۲ تست مرزی جدید) |
| تست‌های مرزی جدید | رمز ۱۴ نویسه → `PasswordPolicyRejected`؛ رمز ۱۵ نویسه با فاصله → `Changed` | در `UserPasswordServiceTests` |
| `PasswordPolicyTests` | بازساختگان: blocklist-only با `passwordpassword` (۱۶ نویسه)؛ کوتاه‌ها (شامل blocklistهای کوتاه) رد می‌شوند | هم‌تراز با حداقل ۱۵ |
| `npx vitest run` (کل ClientApp) | **۷۸۳/۷۸۳ موفق (۷۴ فایل)** | رمزهای معتبر تست‌ها به ≥۱۵ ارتقا یافت؛ هیچ تستی حذف/تضعیف نشد |
| `npm run typecheck` و `npm run build` | موفق | |

## امنیت، داده و سازگاری

- فقط رمزهای جدید/تغییریافته مشمول حداقل ۱۵ هستند؛ Login/Recovery موجود دست‌نخورده؛ هیچ تغییر Schema یا داده‌ای لازم نبود.
- ثبت‌نام اولین مدیر حالا blocklist را هم اعمال می‌کند (قبلاً از دستور D-073 §5 جا مانده بود)؛ کد خطای opaque `enrollment_invalid` بدون تغییر است.
- Rate limit و no-store مسیرهای موجود بدون تغییر.

## ریسک‌ها و کارهای باقی‌مانده

- ریسک UX: کاربران موجود هنگام تغییر رمز بعدی باید رمز ≥۱۵ انتخاب کنند؛ پیام‌های فارسی پارامتریک راهنما هستند.
- شناسه‌های رمزهای افشاشدهٔ واقعی (HaveIBeenPwned) نیازمند تصمیم وابستگی/سرویس است و در D-073 الزام نسخه اول نیست — blocklist محلی فعلی کفایت مصوب دارد.

## شناسه‌های Git

- Branch: `feat/password-policy-d073`
- Pull Request: پس از Push ثبت می‌شود
