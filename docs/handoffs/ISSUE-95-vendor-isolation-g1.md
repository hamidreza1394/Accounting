# تحویل وظیفه — ISSUE-95-vendor-isolation-g1

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/95
- **Agent/مسئول:** ChatGPT / Faranou Senior Developer + Architecture Review + Security/Multi-Tenancy + Testing/Quality skills
- **تاریخ:** 2026-10-25
- **وضعیت:** تست G1 و مستندات افزوده شده‌اند؛ منتظر CI و بازبینی

## هدف و معیارهای پذیرش
- [x] تست رگرسیون برای گراف مستقیم/ترنزیتیو `Server` افزوده شد.
- [x] نبود ProjectReference در `Licensing.Contracts` کنترل می‌شود.
- [x] لینک‌های Compile/Content به خارج از پوشهٔ `Licensing.Contracts` رد می‌شوند.
- [x] fixture منفی نشان می‌دهد وابستگی ترنزیتیو به Vendor تشخیص داده می‌شود.
- [ ] CI واقعی و آزمون روی commit نهایی موفق شود.
- [ ] بازبینی نهایی diff و بررسی اینکه هیچ مسیر معتبر پروژه به‌اشتباه رد نمی‌شود.

## خلاصه تغییرات
- `tests/Template.Tests/Architecture/VendorIsolationTests.cs`: تست معماری G1؛ پیمایش XML گراف پروژه، کنترل `Licensing.Contracts` و تست منفی.
- `docs/DECISIONS.md`: وضعیت واقعی G1–G6 و شکاف‌های باقی‌مانده ثبت شد؛ D-041 بی‌صدا به‌عنوان کاملاً محقق‌شده معرفی نشد.
- `docs/TESTING.md`: راهنمای اجرای گارد و حدود آن افزوده شد.
- `docs/WORK_LOG.md`: شواهد، محدودیت‌ها و گام بعدی ثبت شد.

## تصمیم‌های فنی
- به‌جای جست‌وجوی متنی، XML مربوط به `ProjectReference` به‌صورت بازگشتی پیمایش می‌شود؛ مسیرهای دارای property/wildcard که قابل ارزیابی قطعی نیستند، fail-closed گزارش می‌شوند.
- این گارد فقط مرز پروژه‌ای را بررسی می‌کند؛ جایگزین آزمون انتشار G2 یا اسکن اسرار G3 نیست.
- G2 فعلی Vendor.* و مواد کلید خصوصی را رد می‌کند اما allow-list کامل همهٔ اسمبلی‌های Publish تأیید نشده است. G4 (CODEOWNERS/branch protection) و G5 (Workflow استقرار Vendor جدا) نیز باقی‌اند.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `dotnet test Template.sln --configuration Release` | اجرا نشده | در این محیط ابزار اجرای محلی در دسترس نبود؛ CI لازم است |
| تست G1 روی گراف فعلی | در انتظار CI | تست در مسیر `tests/Template.Tests/Architecture/VendorIsolationTests.cs` ثبت شده است |
| fixture منفی با ارجاع ترنزیتیو | در انتظار CI | گراف مصنوعی موقت، بدون build/اجرای Vendor |
| `git diff --check` | اجرا نشده | تغییرات از GitHub Contents API ثبت شده‌اند |
| secret scan | در انتظار CI PR | Job موجود Gitleaks باید روی HEAD این PR موفق باشد |

## امنیت، داده و سازگاری
- هیچ package جدیدی اضافه نشده است.
- هیچ کد اجرایی مشتری، Schema/Migration یا دیتابیس مشتری تغییر نکرده است.
- هیچ secret، credential یا کلید خصوصی اضافه نشده است.
- تست در `Template.sln` است تا با گاردهای معمول مشتری اجرا شود؛ Vendor مستقل می‌ماند.

## ریسک‌ها و کارهای باقی‌مانده
- CI باید نشان دهد همهٔ ProjectReferenceهای فعلی قابل resolve هستند و گراف Server فاقد Vendor است.
- G4/G5 و allow-list کامل G2 خارج از محدودهٔ این Issue هستند و هنوز Verified نیستند.
- D-041 می‌گوید گاردها پیش‌شرط مرز امن‌اند؛ PR #92 قبل از تکمیل همهٔ گاردها ادغام شده بود. تا تکمیل/بررسی گاردهای لازم، نباید انطباق کامل D-041 ادعا شود.

## شناسه‌های Git
- Branch: `security/issue-95-vendor-isolation-g1`
- Commit: پس از آخرین تغییر تعیین شود.
- Pull Request: پس از ایجاد ثبت شود.