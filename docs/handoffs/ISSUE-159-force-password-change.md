# تحویل وظیفه — ISSUE-159-force-password-change

- **Issue:** [#159 — اجبار تغییر گذرواژهٔ اولیه و محدودسازی نشست](https://github.com/hamidreza1394/Accounting/issues/159)
- **Agent/مسئول:** ChatGPT با ابزار GitHub
- **تاریخ:** 2026-10-27
- **وضعیت:** پیاده‌سازی در شاخه؛ CI و بازبینی هنوز لازم است (**Implemented / Not Verified**)

## هدف و معیارهای پذیرش
- [x] تغییر گذرواژه از endpoint هم‌مبدأ با Cookie و anti-forgery انجام شود.
- [x] تا پیش از تغییر گذرواژه، APIهای مدیریتی مسدود باشند؛ وضعیت نشست، دریافت توکن ضدجعل، خروج و endpoint تغییر گذرواژه مجاز بمانند.
- [x] گذرواژه خام در ممیزی ذخیره نشود و رخداد موفقیت ثبت شود.
- [x] رابط فارسی RTL جریان تغییر گذرواژه را ارائه کند.
- [x] آزمون‌های رگرسیون HTTP و رابط افزوده شوند.
- [ ] Vendor CI و .NET CI روی آخرین SHA سبز شوند.
- [ ] بازبینی انسانی و تأیید Maintainer.

## خلاصه تغییرات
- bootstrap اکنون پس از تکمیل MFA حساب مدیر را با `MustChangePassword=true` فعال می‌کند؛ قبل از MFA حساب همچنان غیرفعال است.
- endpoint جدید `POST /api/auth/change-password` با بررسی anti-forgery، بررسی گذرواژه فعلی، حداقل طول ۱۴ نویسه و سیاست Identity؛ خطاها پیام عمومی دارند. rate limit این مسیر از rate limit ورود/TOTP جدا شده تا حملهٔ گذرواژه و نرخ ورود مستقل کنترل شوند.
- پرچم `MustChangePassword` فقط در همان به‌روزرسانی Identity که هش رمز را ذخیره می‌کند پاک می‌شود؛ در خطا پاسخ موفق صادر نمی‌شود.
- middleware در Host تا زمان تغییر موفق گذرواژه دسترسی API را محدود می‌کند.
- صفحهٔ تغییر گذرواژه اولیه در رابط Vendor اضافه شد؛ رازها در Web Storage قرار نمی‌گیرند.
- آزمون Integration با SQL Server و آزمون UI برای مسیر اجباری افزوده شد.

## فایل‌های مهم تغییرکرده
- `Vendor/Vendor.Host/Authentication/VendorPasswordChangeEndpoints.cs` — endpoint تغییر گذرواژه.
- `Vendor/Vendor.Host/Program.cs` — گارد سروری اجباری و ثبت endpoint.
- `Vendor/Vendor.Web/src/App.tsx` — فرم فارسی RTL تغییر گذرواژه.
- `Vendor/Vendor.Web/src/App.test.tsx` — آزمون جریان رابط.
- `tests/Vendor.Host.Tests/VendorAuthenticationSecurityTests.cs` — آزمون منفی/مثبت HTTP و ممیزی.
- `Vendor/README.md`, `docs/SECURITY.md`, `docs/WORK_LOG.md` — مستندات وضعیت و امنیت.
- `docs/handoffs/ISSUE-159-force-password-change.md` — این تحویل.

## تصمیم‌های فنی
- از `UserManager.ChangePasswordAsync` و سیاست موجود ASP.NET Core Identity استفاده شد؛ الگوریتم هش سفارشی یا dependency جدید اضافه نشد.
- گارد سمت سرور است و به مخفی‌کردن UI تکیه نمی‌کند.
- تنها مسیرهای لازم برای دریافت anti-forgery، مشاهده وضعیت نشست، تغییر گذرواژه و خروج در حالت اجباری مجازند.
- تغییر معماری یا Migration برای دیتابیس حسابداری مشتری انجام نشد؛ Migration دیتابیس Vendor نیز در این تغییر لازم نیست.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| Vendor UI typecheck/tests/build | روی اجرای اولیه موفق | CI run 40451859716؛ آزمون UI و build موفق بودند |
| Vendor .NET build/tests | اجرای اولیه: ۱ شکست / ۲۵ موفق | CI run 40451859716؛ شکست به‌علت اشتراک rate limit ورود با دو تلاش تست تغییر رمز بود؛ policy مستقل افزوده شد و اجرای جدید لازم است |
| EF Core snapshot / migration guard | منتظر CI | مدل و Migration تغییر نکرده‌اند |
| Publish و artifact scan | منتظر CI | باید روی آخرین commit اجرا شود |
| بازبینی diff | ناقص تا مشاهده نتیجه CI | منبع از طریق GitHub API تغییر کرده؛ اجرای محلی `git diff --check` در این محیط انجام نشد |

## امنیت، داده و سازگاری
- درخواست تغییر گذرواژه دارای anti-forgery و محدودیت تلاش است.
- هیچ رمز خام یا مقدار محرمانه در رویداد ممیزی قرار نمی‌گیرد.
- رخداد ممیزی پس از تغییر موفق ثبت می‌شود. اگر append ممیزی شکست بخورد، endpoint نباید پاسخ موفق برگرداند؛ بررسی بازیابی/رفتار خطای عملیاتی در بازبینی لازم است.
- هیچ تغییر در دیتابیس مشتری یا Migrationهای حسابداری اعمال نشده است.

## ریسک‌ها و کارهای باقی‌مانده
- اجرای CI روی آخرین commit و رفع تمام خطاهای واقعی.
- تأیید رفتار SecurityStamp و نشست پس از تغییر رمز با آزمون HTTP.
- بازبینی UX در مرورگر و تست‌های دسترس‌پذیری.
- تکمیل RBAC مدیریتی، resource-level authorization/IDOR، maker-checker، مدیریت مشتری/نصب/قرارداد/لایسنس، checkpoint مستقل ممیزی و امضای KMS/HSM همچنان جداگانه باقی است.

## شناسه‌های Git
- **Branch:** `security/issue-159-force-password-change`
- **Commitهای شاخه:** از Issue #159؛ SHA نهایی پس از مستندات ثبت می‌شود.
- **Pull Request:** پس از تکمیل مستندات ایجاد می‌شود.
