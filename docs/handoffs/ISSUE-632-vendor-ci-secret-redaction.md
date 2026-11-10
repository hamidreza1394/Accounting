# تحویل وظیفه — ISSUE-632-vendor-ci-secret-redaction

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/632
- **Agent/مسئول:** ChatGPT / GitHub tools
- **تاریخ:** 2026-11-10
- **وضعیت:** Implemented / Not Verified؛ ممیزی log اجرای نخست PR #633 یک افشای جزئی از متن connection string در shell source را یافت؛ ساخت مقدار به helper منتقل شده، PINهای SoftHSM نیز per-run و masked شده‌اند و CI/log نهایی هنوز باید تأیید شود.

## هدف و معیارهای پذیرش
- [x] حذف password قابل پیش‌بینی SQL Server از YAML و Service container.
- [x] تولید password تصادفی فقط برای همان اجرای Vendor CI و mask کردن آن پیش از استفاده.
- [x] تولید تصادفی و mask کردن SO PIN و user PIN موقت SoftHSM؛ هیچ PIN ثابت در workflow باقی نماند.
- [x] حذف connection string از YAML و انتقال ساخت runtime به helper مستقل؛ mask کامل هر مقدار پیش از export به process تست/EF.
- [x] تضمین cleanup کانتینر SQL Server حتی در شکست stepها.
- [x] افزودن regression guard برای جلوگیری از بازگشت password/connection string به مقادیر YAML.
- [ ] Vendor CI روی SHA نهایی PR سبز شود.
- [ ] ممیزی log واقعی تأیید کند نه password و نه هیچ fragment از connection string در step log ظاهر نمی‌شود.

## خلاصه تغییرات
- بررسی Vendor CI run `42651850808` روی SHA `9fb6400569b282d18829073d76588b82f7c3c5d8` نشان داد password آزمایشی SQL Server در Docker create command و بلوک‌های محیط step بدون masking ثبت شده بود. مقدار credential در این handoff عمداً تکرار نمی‌شود.
- SQL Server دیگر به‌عنوان GitHub Actions service با password در YAML تعریف نشده است؛ کانتینر در step مستقل و با password تصادفی همان run راه‌اندازی می‌شود.
- password SQL با workflow command از ابتدای step mask و سپس از طریق `GITHUB_ENV` به stepهای بعد منتقل می‌شود.
- SO PIN و user PIN مربوط به SoftHSM نیز در همان run تصادفی تولید و پیش از `softhsm2-util` mask می‌شوند؛ فقط user PIN موردنیاز تست به `GITHUB_ENV` منتقل می‌شود.
- connection stringها در YAML `env:` نیستند؛ در shell زمان اجرا ساخته و پیش از مصرف mask می‌شوند.
- cleanup با `if: always()` انجام می‌شود.
- guard ایستا workflow را از نظر password literal، password/connection string در YAML environment، ترتیب mask/start و cleanup بررسی می‌کند.

## فایل‌های مهم تغییرکرده
- `.github/workflows/vendor-ci.yml` — حذف SQL Server service password و connection stringهای YAML؛ راه‌اندازی/cleanup کنترل‌شده.
- `.github/scripts/verify-vendor-ci-secret-hygiene.sh` — regression guard ساختار workflow و ترتیب mask/export.
- `.github/scripts/run-vendor-ci-database-checks.sh` — ساخت runtime مقادیر اتصال در process بدون echo شدن source در GitHub Actions log.
- `.github/workflows/vendor-ci.yml` — PINهای SoftHSM را نیز تصادفی و per-run می‌سازد؛ مقدار ثابت قبلی حذف شد.
- این handoff و `docs/WORK_LOG.md`.

## تصمیم‌های فنی
- از secret قابل استفاده مجدد یا GitHub repository secret جدید استفاده نشد؛ password فقط برای همان container/job تصادفی است.
- هیچ تغییر در `.github/workflows/dotnet.yml`، Customer workflow، Production configuration یا قرارداد Licensing انجام نشد.
- هیچ ADR جدیدی لازم نیست؛ این یک hardening محدود زیرساخت تست Vendor است.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| بازبینی log run `42651850808` | یافته تأیید شد | Docker create command و env stepها مقدار password را بدون mask نشان می‌دادند؛ مقدار در گزارش بازنشر نشده است. |
| Vendor CI run `42775690727` | موفق | guard، UI، build/test، SQL Server، EF snapshot، publish و دو checkpoint regression موفق؛ package upload مطابق PR بودن Skip شد. |
| ممیزی log run `42775690727` / job `129693604250` | **معیار پذیرش رد شد** | password mask بود، اما بخشی از متن connection string در shell source چاپ‌شده توسط Actions دیده می‌شد؛ علت ریشه‌ای: inline `run:` پیش از اجرای mask در log ثبت می‌شود. |
| Vendor CI روی SHA `9456df27...` | ناموفق در guard | false-positive الگوی grep روی متغیر محلی `password=`؛ تست‌ها به مراحل بعد نرسیدند؛ علت اصلاح شد و باید روی head جدید بازاجرا شود. |
| helper و guard تکمیلی | در commit بعدی PR | ساخت مقدار به helper منتقل و ترتیب mask-before-export به guard افزوده شد؛ CI جدید هنوز در انتظار است. |
| .NET CI / E2E / Windows Publish | در انتظار | اجرای قبلی روی head `56c3...` هنوز کامل نشده بود؛ باید head جدید جداگانه بررسی شود. |
| ممیزی log نهایی | در انتظار | باید پس از اجرای جدید بررسی شود. |
| .NET CI و customer workflow | خارج از محدوده | workflow عمومی مشتری تغییر نکرده است. |

## امنیت، داده و سازگاری
- هیچ password، private key، hash رمز مشتری، connection string کامل یا credential قابل استفاده مجدد به Git/artifact افزوده نشده است.
- password و connection string در runner به‌صورت runtime ساخته می‌شوند؛ helper مقدار کامل را mask می‌کند و سپس export می‌کند. منبع shell helper در log Actions echo نمی‌شود؛ این ادعا تا ممیزی log اجرای head نهایی `NotVerified` است.
- تغییر فقط زیرساخت تست Vendor است؛ هیچ Customer DB، `Server/**` یا Migration مشتری تغییر نکرده است.
- یافتهٔ مشابه در `.github/workflows/dotnet.yml` خارج از محدودهٔ این Issue است و در این PR اصلاح نمی‌شود.

## ریسک‌ها و کارهای باقی‌مانده
- باید logهای واقعی run جدید بررسی شوند؛ سبزشدن تست به‌تنهایی اثبات عدم نشت log نیست.
- اگر Docker/SQL Server startup روی runner شکست خورد، علت از job log خوانده شود؛ password یا connection string خام برای عیب‌یابی چاپ نشود.
- تغییرات فقط پس از سبزشدن تمام بررسی‌های الزامی و ممیزی log قابل Merge هستند.

## شناسه‌های Git
- Branch: `fix/632-vendor-ci-secret-redaction`
- Commit: head نهایی PR #633؛ SHA جاری همیشه از metadata خود PR خوانده شود.
- Pull Request: [#633](https://github.com/hamidreza1394/Accounting/pull/633) — open; CI و ممیزی log روی commit جدید pending
