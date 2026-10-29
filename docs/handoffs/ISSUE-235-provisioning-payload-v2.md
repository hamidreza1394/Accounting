# تحویل وظیفه — ISSUE-235-provisioning-payload-v2

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/235
- **Agent/مسئول:** ChatGPT
- **تاریخ:** 2026-10-29
- **وضعیت:** بخش قرارداد تکمیل‌شده در PR؛ مصرف‌کنندهٔ مشتری و Migration هنوز باقی است

## هدف و معیارهای پذیرش
- [x] افزودن نسخهٔ قرارداد provisioning که دورهٔ مالی اولیه را صریح حمل کند.
- [x] حمل مرجع شرکت مادر برای شرکت فرعی، بدون اتصال مستقیم Vendor به دیتابیس مشتری.
- [x] حفظ canonical JSON و اعتبار امضای payload نسخهٔ ۱.
- [x] تست منفی برای تاریخ نامعتبر، نبود دورهٔ مالی و شرکت فرعی بدون مرجع مادر.
- [ ] پیاده‌سازی مصرف‌کننده، claim اتمی RequestId/Nonce، تراکنش provisioning و enrollment امن مدیر اولیه.
- [ ] Migration و تست SQL Server سمت مشتری در PR جداگانه.

## خلاصه تغییرات
- CustomerProvisioningPayload فیلدهای نسخه‌دار عنوان/تاریخ شروع/پایان دورهٔ مالی و ParentCompanyReference دارد.
- نسخهٔ ۱ دقیقاً همان فیلدهای canonical قبلی را تولید می‌کند؛ فیلدهای جدید فقط در SchemaVersion=2 به JSON امضاشده افزوده می‌شوند.
- Validator هر دو schema v1 و v2 را می‌پذیرد، اما در v2 دورهٔ مالی معتبر را اجباری می‌کند و ParentCompanyReference را فقط برای subsidiary می‌پذیرد.
- هیچ رمز، password hash، secret، connection string یا private key به payload اضافه نشده است.

## فایل‌های مهم تغییرکرده
- Licensing.Contracts/CustomerProvisioningPackage.cs — فیلدهای نسخهٔ ۲ و canonical serialization سازگار با v1.
- Licensing.Contracts/CustomerProvisioningPackageValidator.cs — قواعد نسخهٔ ۱/۲ و اعتبارسنجی دوره/رابطهٔ والد.
- tests/Licensing.Contracts.Tests/CustomerProvisioningPackageValidatorTests.cs — پوشش v1، امضای v2 و سناریوهای نامعتبر.

## تصمیم‌های فنی
- قالب envelope همچنان نسخهٔ ۱ است؛ نسخهٔ schema payload مستقل است.
- قرارداد v1 حفظ شده چون وضعیت نصب‌های Production از مخزن قابل اثبات نیست؛ فیلد جدیدی به canonical JSON نسخهٔ ۱ افزوده نمی‌شود.
- دورهٔ مالی در v2 لازم است. شرکت subsidiary باید مرجع مادر داشته باشد؛ independent و parent نباید مرجع مادر داشته باشند.
- رمز اولیه از بسته منتقل نمی‌شود؛ مسیر enrollment امن نیازمند طراحی و پیاده‌سازی جداگانه است.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| اجرای محلی dotnet test برای Licensing.Contracts.Tests | اجرا نشده | این نشست checkout محلی متصل به مخزن و SDK .NET ندارد؛ CI روی PR باید اجرا شود. |
| تست‌های جدید | افزوده شد، منتظر CI | شامل حفظ v1، امضای v2، دورهٔ ناقص، تاریخ وارونه و رابطهٔ والد نامعتبر. |
| git diff --check | اجرا نشده | تغییرات از طریق GitHub Contents API ثبت شده‌اند؛ CI و بازبینی نهایی لازم است. |

## امنیت، داده و سازگاری
- این PR فقط قرارداد مشترک و تست‌های واحد آن را تغییر می‌دهد؛ هیچ Server/**، schema مشتری، Migration یا Vendor DB تغییر نکرده است.
- اعتبارسنجی امضا نیازمند trust store مستقل در مصرف‌کننده است؛ این PR مصرف‌کننده یا replay protection را ادعا نمی‌کند.
- payload نباید شامل اطلاعات محرمانه یا اطلاعات تجاری حسابداری مشتری باشد.

## ریسک‌ها و کارهای باقی‌مانده
- تولیدکنندهٔ Vendor باید در PR جداگانه v2 را صادر کند؛ مصرف‌کننده نباید آن را حدس بزند.
- مصرف‌کننده باید RequestId/Nonce را با unique constraint و claim اتمی در همان تراکنش provisioning ذخیره کند.
- پیش از ادغام Migration مشتری، بررسی مدل فعلی، rollback و SQL Server integration tests الزامی است.
- enrollment مدیر اولیه باید با secret تصادفی یک‌بارمصرف و محدود به زمان انجام شود؛ هیچ رمز یا secret قابل‌بازیابی در بسته قرار نگیرد.
- KMS/HSM و مقصد مستقل ممیزی همچنان وابستگی عملیاتی جداگانه‌اند.

## شناسه‌های Git
- Branch: feat/provisioning-payload-v2
- Commit: از روی PR head تعیین شود.
- Pull Request: پس از ثبت این handoff ایجاد می‌شود.