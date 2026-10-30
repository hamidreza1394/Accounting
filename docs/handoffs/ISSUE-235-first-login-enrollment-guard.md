# تحویل وظیفه — ISSUE-235-first-login-enrollment-guard

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/235
- **Agent/مسئول:** Claude Code
- **تاریخ:** 2026-10-30
- **وضعیت:** انجام‌شده (کد و تست محلی)؛ CI روی PR هنوز باید بررسی شود

## هدف و معیارهای پذیرش
- [x] مدیر اولیهٔ provisioning تا پایان Enrollment هیچ نشست معتبر و هیچ ورود موفقی نداشته باشد (سمت سرور، نه UI).
- [x] پس از تکمیل Enrollment محدودیت برداشته شود و ورود با رمز جدید کار کند.
- [x] نشست مبتنی بر رمز قبلی پس از تغییر رمز معتبر نباشد.
- [x] تست منفی روی نشست و ورود؛ تست روی SQL Server واقعی.

## یافتهٔ ممیزی (انحراف از شرح اولیهٔ مرحله)
- کاربر اولیه با رمز **تصادفیِ ناشناخته** ساخته می‌شود (`CreateOpaqueSecret` در `CustomerProvisioningApplyService`) و رمز واقعی را فقط خود مدیر در Enrollment تعیین می‌کند؛ همان لحظه `MustChangePassword=false` می‌شود. بنابراین «رمز اولیه‌ای که باید در اولین ورود تغییر کند» وجود ندارد و ریسک اصلی از قبل بسته بود.
- برای کاربران شرکت در مشتری، endpoint تغییر رمز عادی وجود ندارد (فقط `/provisioning/enrollment/complete`، بدون نشست و با توکن). پس «فقط تغییر رمز و خروج مجاز باشد» در عمل یعنی «تا پایان Enrollment هیچ نشستی وجود نداشته باشد».
- این تغییر لایهٔ دفاعی (defense in depth) است: اگر در آینده مسیری رمزی برای کاربر پیش از Enrollment تعیین کند (بازنشانی توسط مدیر، ایمپورت و…)، آن رمز هم نشست یا ورود نمی‌دهد.

## خلاصه تغییرات
- `SessionValidator`: همان پرس‌وجوی تک‌رفت‌وبرگشتی حساب، علاوه بر فعال‌بودن و مهر اعتبارنامه، وجود `ProvisioningRequestClaim` با `MustChangePassword=true` برای همان کاربر را هم می‌سنجد؛ در صورت وجود نشست رد می‌شود.
- `AuthenticationService.AuthenticateAsync`: پس از تأیید رمز و پیش از ورود، اگر Enrollment در انتظار باشد نتیجهٔ عمومی `InvalidCredentials` برمی‌گردد (افشای وضعیت حساب نمی‌کند). رویداد ممیزی `LoginDeniedEnrollmentPending` ثبت می‌شود (نه `LoginFailed`، تا شمارندهٔ قفل را نیفزاید) و `LoginLog` نوشته نمی‌شود.
- هیچ Schema، Migration، endpoint یا وابستگی جدیدی اضافه نشد. ایندکس موجود `(AdminUserId, MustChangePassword)` پرس‌وجو را پوشش می‌دهد.

## فایل‌های مهم تغییرکرده
- `Server/Infrastructure/Security/SessionValidator.cs` — رد نشست در حالت Enrollment در انتظار.
- `Server/Infrastructure/Security/AuthenticationService.cs` — رد ورود و ثبت ممیزی.
- `tests/Template.Tests/SessionValidatorTests.cs` — تست واحد (در انتظار/تکمیل‌شده/کاربر دیگر).
- `tests/Template.Tests/LoginAuthenticationTests.cs` — تست صفحهٔ ورود (رد با رمز درست در حالت انتظار؛ پذیرش پس از تکمیل).
- `tests/Template.Tests/Integration/CustomerProvisioningClaimsSqlServerTests.cs` — همین رفتار روی SQL Server واقعی در مسیر کامل apply ← reissue ← enrollment.

## تصمیم‌های فنی
- تصمیم معماری جدیدی گرفته نشد. پاسخ عمومی `InvalidCredentials` به‌جای مقدار جدید enum انتخاب شد تا Login Page و قرارداد `LoginOutcome` تغییر نکند و وضعیت حساب فاش نشود.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `dotnet restore` + `dotnet build Template.sln -c Release` | موفق | 0 خطا (هشدارهای موجود قبلی) |
| `dotnet test` SessionValidatorTests / LoginAuthenticationTests | موفق | 7 و 27 تست |
| `dotnet test` CustomerProvisioningClaimsSqlServerTests (SQL Server 2022 واقعی در Docker) | موفق | 3 تست |
| `dotnet test Template.sln -c Release` (با `FARANOU_REQUIRE_SQLSERVER=1`) | موفق | 660 کل، 658 موفق، 2 skipped، 0 ناموفق |
| `python3 scripts/check_static_assets.py` / `npm` کلاینت / E2E Playwright | اجرا نشد | تغییری در کلاینت یا دارایی وب نبود |
| بازبینی مستقل (code-reviewer/security-reviewer) | اجرا نشد | Subagent در این نشست فراخوانی نشد؛ بازبینی Diff توسط خود نویسنده انجام شد |

## امنیت، داده و سازگاری
- تغییر در احراز هویت/نشست: فقط سخت‌تر می‌کند؛ هیچ مسیر دسترسی جدیدی ایجاد نمی‌شود. حساب‌های بدون claim (همهٔ کاربران قدیمی) رفتار قبلی را دارند.
- هزینه: یک `EXISTS` ایندکس‌دار داخل همان پرس‌وجوی نشست؛ رفت‌وبرگشت اضافه ندارد.
- حساب زیرساخت (Master) دست‌نخورده است.

## ریسک‌ها و کارهای باقی‌مانده
- بازنشانی رمز توسط مدیر برای مدیر اولیهٔ در انتظار Enrollment عمداً کار نمی‌کند؛ مسیر درست `reissue` توکن است.
- 2 تست skipped در اجرای کامل بررسی نشد که از پیش وجود داشته‌اند (ریشهٔ skip پیش‌تر در این PR تغییر نکرده).

## شناسه‌های Git
- Branch: `feat/issue-235-first-login-enrollment-guard`
- Commit / Pull Request: پس از ساخت PR در WORK_LOG ثبت می‌شود.
