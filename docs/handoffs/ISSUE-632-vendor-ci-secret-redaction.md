# تحویل وظیفه — ISSUE-632-vendor-ci-secret-redaction

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/632
- **Agent/مسئول:** ChatGPT / GitHub tools
- **تاریخ:** 2026-11-10
- **وضعیت:** Implemented / Not Verified؛ CI و ممیزی log پس از اجرای PR لازم است.

## هدف و معیارهای پذیرش
- [x] حذف password قابل پیش‌بینی SQL Server از YAML و Service container.
- [x] تولید password تصادفی فقط برای همان اجرای Vendor CI و mask کردن آن پیش از استفاده.
- [x] حذف connection stringها از YAML `env:` و ساخت/ثبت mask آن‌ها فقط داخل process تست.
- [x] تضمین cleanup کانتینر SQL Server حتی در شکست stepها.
- [x] افزودن regression guard برای جلوگیری از بازگشت password/connection string به مقادیر YAML.
- [ ] Vendor CI روی PR سبز شود.
- [ ] ممیزی log واقعی تأیید کند password و connection string خام در هیچ step ظاهر نمی‌شوند.

## خلاصه تغییرات
- بررسی Vendor CI run `42651850808` روی SHA `9fb6400569b282d18829073d76588b82f7c3c5d8` نشان داد password آزمایشی SQL Server در Docker create command و بلوک‌های محیط step بدون masking ثبت شده بود. مقدار credential در این handoff عمداً تکرار نمی‌شود.
- SQL Server دیگر به‌عنوان GitHub Actions service با password در YAML تعریف نشده است؛ کانتینر در step مستقل و با password تصادفی همان run راه‌اندازی می‌شود.
- password با workflow command از ابتدای step mask و سپس از طریق `GITHUB_ENV` به stepهای بعد منتقل می‌شود.
- connection stringها در YAML `env:` نیستند؛ در shell زمان اجرا ساخته و پیش از مصرف mask می‌شوند.
- cleanup با `if: always()` انجام می‌شود.
- guard ایستا workflow را از نظر password literal، password/connection string در YAML environment، ترتیب mask/start و cleanup بررسی می‌کند.

## فایل‌های مهم تغییرکرده
- `.github/workflows/vendor-ci.yml` — حذف SQL Server service password و connection stringهای YAML؛ راه‌اندازی/cleanup کنترل‌شده.
- `.github/scripts/verify-vendor-ci-secret-hygiene.sh` — regression guard ساختار workflow.
- این handoff و `docs/WORK_LOG.md`.

## تصمیم‌های فنی
- از secret قابل استفاده مجدد یا GitHub repository secret جدید استفاده نشد؛ password فقط برای همان container/job تصادفی است.
- هیچ تغییر در `.github/workflows/dotnet.yml`، Customer workflow، Production configuration یا قرارداد Licensing انجام نشد.
- هیچ ADR جدیدی لازم نیست؛ این یک hardening محدود زیرساخت تست Vendor است.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| بازبینی log run `42651850808` | یافته تأیید شد | Docker create command و env stepها مقدار password را بدون mask نشان می‌دادند؛ مقدار در گزارش بازنشر نشده است. |
| regression guard | در CI اجرا خواهد شد | هنوز روی GitHub Actions اجرا نشده است. |
| Vendor UI/.NET/EF/SQL Server CI | در انتظار | اجرای محلی در این محیط انجام نشده است. |
| ممیزی log پس از اصلاح | در انتظار | باید پس از run جدید بررسی شود. |
| .NET CI و customer workflow | خارج از محدوده | workflow عمومی مشتری تغییر نکرده است. |

## امنیت، داده و سازگاری
- هیچ password، private key، hash رمز مشتری، connection string کامل یا credential قابل استفاده مجدد به Git/artifact افزوده نشده است.
- password و connection string در runner به‌صورت runtime ساخته و پیش از استفاده mask می‌شوند.
- تغییر فقط زیرساخت تست Vendor است؛ هیچ Customer DB، `Server/**` یا Migration مشتری تغییر نکرده است.
- یافتهٔ مشابه در `.github/workflows/dotnet.yml` خارج از محدودهٔ این Issue است و در این PR اصلاح نمی‌شود.

## ریسک‌ها و کارهای باقی‌مانده
- باید logهای واقعی run جدید بررسی شوند؛ سبزشدن تست به‌تنهایی اثبات عدم نشت log نیست.
- اگر Docker/SQL Server startup روی runner شکست خورد، علت از job log خوانده شود؛ password یا connection string خام برای عیب‌یابی چاپ نشود.
- تغییرات فقط پس از سبزشدن تمام بررسی‌های الزامی و ممیزی log قابل Merge هستند.

## شناسه‌های Git
- Branch: `fix/632-vendor-ci-secret-redaction`
- Commit: در حال تکمیل
- Pull Request: در حال ایجاد
