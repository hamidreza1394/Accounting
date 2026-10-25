# تحویل وظیفه — ISSUE-98-vendor-ci-codeowners

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/98
- **Agent/مسئول:** ChatGPT / Faranou Senior Developer + Architecture Review + Security/Multi-Tenancy + Testing/Quality
- **تاریخ:** 2026-10-25
- **وضعیت:** پیاده‌سازی شاخه آماده بازبینی؛ CI روی PR باید تأیید شود

## هدف و معیارهای پذیرش
- [x] `.github/CODEOWNERS` برای Vendor و قرارداد مشترک افزوده شد.
- [x] workflow مستقل مسیر-محدود `.github/workflows/vendor-ci.yml` افزوده شد.
- [x] Vendor Restore/Build/Test از workflow اصلی .NET جدا شد.
- [x] artifact publish فقط روی push به `main` ساخته می‌شود؛ deploy و signing وجود ندارد.
- [x] D-041، DEVELOPMENT، Vendor README و WORK_LOG دربارهٔ وضعیت و محدودیت‌ها به‌روزرسانی شدند.
- [ ] CI واقعی روی HEAD این PR سبز شود.
- [ ] Branch protection و required code-owner review از تنظیمات مخزن بررسی شود؛ ابزار حاضر قابلیت اعمال/تأیید آن را ارائه نکرد.

## خلاصه تغییرات
- `.github/CODEOWNERS`: مالکیت مسیرهای Vendor، Vendor.sln، تست‌های Vendor، Licensing.Contracts و workflowهای CI.
- `.github/workflows/vendor-ci.yml`: trigger محدود به مسیرهای مربوط، permissions فقط read، restore/build/test، publish به مسیر موقت، بررسی مواد کلید خصوصی و artifact فقط از `main`.
- `.github/workflows/dotnet.yml`: حذف job تکراری `vendor-build-test`؛ pipeline برنامه مشتری باقی می‌ماند.
- `docs/DECISIONS.md`: تفکیک وضعیت جاری گاردها از متن تاریخی D-041؛ G4/G5 کامل/Verified ادعا نشده‌اند.
- `Vendor/README.md`, `docs/DEVELOPMENT.md`, `docs/WORK_LOG.md`: توضیح artifact و نبود deploy واقعی، secret یا signing.

## تصمیم‌های فنی
- از workflow مستقل path-filtered برای Vendor استفاده می‌شود تا build/test آن از pipeline مشتری جدا شود.
- Artifact فقط برای `push` به `main` ایجاد می‌شود و retention آن ۷ روز است؛ این یک build artifact است، نه استقرار Production.
- هیچ secret، credential، environment deployment یا کلید خصوصی به workflow داده نشده است؛ امضای لایسنس در GitHub Actions مجاز نیست.
- CODEOWNERS به‌تنهایی required review را اعمال نمی‌کند. Branch protection را باید به‌شکل مستقل فعال و با reviewer معتبر تنظیم کرد.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| CI run `40214517027` برای PR #96 | موفق | همهٔ Jobهای گزارش‌شده از جمله `build-and-test`, `secret-scan`, `vendor-build-test`, E2E و Publish سبز بودند؛ این run قبل از این PR است |
| `dotnet restore Vendor.sln` | اجرا نشده محلی | باید در workflow جدید اجرا شود |
| `dotnet build Vendor.sln --no-restore --configuration Release` | اجرا نشده محلی | باید در workflow جدید اجرا شود |
| `dotnet test Vendor.sln --configuration Release` | اجرا نشده محلی | باید در workflow جدید اجرا شود |
| `git diff --check` | اجرا نشده محلی | تغییرات از GitHub Contents API اعمال شدند |
| YAML lint / workflow validation | اجرا نشده محلی | GitHub Actions باید workflow را parse و اجرا کند |

## امنیت، داده و سازگاری
- no customer application runtime code, schema, migration, or customer DB changes.
- هیچ secret یا کلید خصوصی افزوده نشده است.
- workflow جدید دسترسی `contents: read` دارد و credential انتشار ندارد.
- گارد G2 هنوز allow-list کامل همهٔ اسمبلی‌های customer publish را تضمین نمی‌کند.

## ریسک‌ها و کارهای باقی‌مانده
- CI این PR باید workflow جدید را با path filters تست کند؛ چون workflow جدید خود را در pull_request path filter دارد، تغییر خود فایل نیز trigger می‌شود.
- تنظیمات Branch protection و required CODEOWNERS review از این اتصال قابل اعمال/تأیید نبود؛ CODEOWNERS file به‌تنهایی enforcement نیست.
- deploy واقعی Cloud / Dedicated Server هنوز انجام نشده؛ انتخاب محیط/secret store/runbook به مرحله‌ای جدا نیاز دارد.
- گارد کامل G2 با allow-list اسمبلی‌ها هنوز باز است.

## شناسه‌های Git
- Branch: `security/issue-98-vendor-ci-codeowners`
- Commit: پس از آخرین تغییر تعیین شود.
- Pull Request: پس از ایجاد ثبت شود.