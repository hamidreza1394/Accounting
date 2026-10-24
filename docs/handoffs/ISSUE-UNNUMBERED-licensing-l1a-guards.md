# تحویل وظیفه — ISSUE-UNNUMBERED-licensing-l1a-guards

- **Issue:** `UNNUMBERED` (مأموریت L1-A — گاردهای G1/G2/G3 طبق D-041)
- **Agent/مسئول:** Grok (مهندس ارشد؛ امنیت زنجیره تأمین)
- **تاریخ:** 2026-10-24
- **وضعیت:** پیاده‌سازی روی شاخه `security/licensing-l1a-guards`؛ در انتظار بازبینی و تأیید؛ Merge انجام نشده

## هدف و معیارهای پذیرش
- [x] G1: آزمون خودکار گراف وابستگی Server بدون Vendor/** + سناریوی منفی با fixture موقت
- [x] G2: کنترل خروجی واقعی Publish برای اسمبلی Vendor و کلید خصوصی؛ اتصال به publish.sh و publish.ps1
- [x] G3: گام Secret Scanning (gitleaks) با SHA کامل commit در workflow
- [x] بدون ساخت Licensing.Contracts یا Vendor Host
- [ ] اجرای کامل `dotnet test` / publish واقعی در این محیط (SDK نصب نشد — محدودیت محیط)
- [ ] شاهد CI سبز (سقف billing احتمالی)

## خلاصه تغییرات
- `scripts/lib/publish-output.sh` + `scripts/lib/PublishOutput.ps1`: تابع G2 `faranou_assert_customer_publish_clean` / `Assert-FaranouCustomerPublishClean`
- `scripts/publish.sh` و `scripts/publish.ps1`: فراخوانی گارد پس از publish
- `scripts/tests/publish-content-guard.test.sh`: تست‌های مثبت/منفی با fixture بی‌خطر
- `tests/Template.Tests/ArchitectureDependencyGuardTests.cs`: G1 (گراف ProjectReference، PackageReference Vendor.*، قوانین Licensing.Contracts در صورت وجود، منفی با temp fixture)
- `.github/workflows/dotnet.yml`: job `secret-scan` با `gitleaks/gitleaks-action@e0c47f4f8be36e29cdc102c57e68cb5cbf0e8d1e` (v3.0.0)
- این handoff و به‌روزرسانی WORK_LOG

## سیاست بسته مشتری (G2)
**ممنوع در خروجی Publish مشتری:**
- هر فایل با نام `Vendor.*.dll|exe|so|dylib|pdb`
- پسوندهای `.pfx`، `.p12`، `.key`
- محتوای `-----BEGIN ... PRIVATE KEY-----` در فایل‌های متنی کوچک‌تر از ۵۱۲KB

**مجاز:** اسمبلی‌های Server/Application/…، گواهی عمومی (`BEGIN CERTIFICATE`)، دارایی‌های کلاینت.

## بررسی و آزمون (واقعی در این نشست)
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| ایجاد شاخه از main@ccc9f74 | موفق | `security/licensing-l1a-guards` |
| منطق G2 روی fixture محلی (clean / Vendor.dll / .pfx / PEM) | clean قبول؛ Vendor و .pfx رد؛ PEM پس از `command grep` قابل تشخیص | محیط sandbox فاقد SDK |
| `scripts/tests/publish-content-guard.test.sh` روی runner | اجرا نشده در این محیط | باید در CI job `publish-scripts` یا محلی با bash اجرا شود |
| `dotnet test` فیلتر ArchitectureDependencyGuardTests | اجرا نشده | SDK 10.0.x در sandbox نصب نشد |
| gitleaks در CI | job اضافه شد؛ اجرای واقعی CI ادعا نمی‌شود | سقف billing احتمالی |
| Build کامل / publish واقعی | اجرا نشده | |

## امنیت
- هیچ کلید خصوصی واقعی ساخته یا commit نشد؛ فقط fixture متنی `TEST-FIXTURE-NOT-A-REAL-KEY`.
- امضای لایسنس در Actions انجام نمی‌شود (D-041 G5).

## ریسک‌ها و کارهای باقی‌مانده
1. نصب SDK و اجرای `dotnet test --filter ArchitectureDependencyGuard` و `scripts/tests/publish-content-guard.test.sh`.
2. افزودن فراخوانی `publish-content-guard.test.sh` به job `publish-scripts` در workflow (در این PR جزئی از فایل workflow اضافه شده؛ در صورت نیاز تکمیل).
3. تأیید مالک برای OD-01/02/12 پیش از L1-B (`Licensing.Contracts`).
4. شاهد CI پس از رفع محدودیت billing.

## شناسه‌های Git
- Branch: `security/licensing-l1a-guards` (مبنا `main`@`ccc9f74caa2cef70f70616ab95875482718c01b4`)
- Commits: روی شاخه (پیام‌های security(licensing-l1a): …)
- Pull Request: در صورت ایجاد موفق در گزارش نهایی

## گام بعدی پیشنهادی (بدون اجرای خودسرانه)
پس از بازبینی و ادغام PR مربوط به L1-A: شروع طراحی و آزمون‌های `Licensing.Contracts` (زیر-PR دوم L1) با رعایت ODهای تأییدشده.
