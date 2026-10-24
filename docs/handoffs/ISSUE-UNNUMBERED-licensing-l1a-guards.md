# تحویل وظیفه — ISSUE-UNNUMBERED-licensing-l1a-guards

- **Issue:** `UNNUMBERED` (مأموریت L1-A — گاردهای G1/G2/G3 طبق D-041)
- **Agent/مسئول:** Grok (مهندس ارشد؛ امنیت زنجیره تأمین)
- **تاریخ:** 2026-10-24
- **وضعیت:** کد روی شاخه `security/licensing-l1a-guards`؛ منتظر بازبینی و PR؛ Merge انجام نشده

## هدف و معیارهای پذیرش
- [x] G1: آزمون خودکار گراف وابستگی Server بدون Vendor/** + سناریوی منفی با fixture موقت + نرمال‌سازی مسیر برای Linux
- [x] G2: کنترل خروجی واقعی Publish برای اسمبلی Vendor و کلید خصوصی؛ اتصال به publish.sh و publish.ps1؛ تست مثبت/منفی
- [x] G3: گام Secret Scanning (gitleaks) با SHA کامل commit در workflow
- [x] بدون ساخت Licensing.Contracts یا Vendor Host
- [x] تست‌های مستقل G1/G2 در محیط Agent اجرا و ثبت شد
- [ ] `dotnet test` روی کل Solution (clone خصوصی بدون توکن در Agent ممکن نبود)
- [ ] شاهد CI سبز (سقف billing احتمالی)

## خلاصه تغییرات
- `scripts/lib/publish-output.sh` + `scripts/lib/PublishOutput.ps1`: تابع G2
- `scripts/publish.sh` و `scripts/publish.ps1`: فراخوانی گارد پس از publish (شکست = exit غیرصفر)
- `scripts/tests/publish-content-guard.test.sh`: تست‌های مثبت/منفی G2
- `tests/Template.Tests/ArchitectureDependencyGuardTests.cs`: G1 + path normalize
- `.github/workflows/dotnet.yml`: job `secret-scan` (gitleaks@e0c47f4… v3.0.0) + اجرای تست G2 در publish-scripts
- این handoff

## سیاست بسته مشتری (G2)
**ممنوع:** `Vendor.*.dll|exe|so|dylib|pdb`؛ `.pfx` / `.p12` / `.key`؛ محتوای `-----BEGIN ... PRIVATE KEY-----`
**مجاز:** اسمبلی‌های Server و وابسته‌های مشتری؛ `BEGIN CERTIFICATE`؛ دارایی‌های کلاینت

## بررسی و آزمون (واقعی)
| فرمان/آزمون | نتیجه |
|---|---|
| نصب SDK 10.0.402 (official + SHA-512) | موفق |
| G2 fixture (clean/Vendor/pfx/p12/key/PEM/missing) | PASS (۷/۷ سپس ۴/۴ در تکرار) |
| G1 negative با نرمال‌سازی `\\` → sep | PASS (تشخیص Vendor/Host) |
| `dotnet test` Template.sln | اجرا نشد (بدون clone کامل) |
| CI | تأییدنشده |

## شناسه‌های Git
- Branch: `security/licensing-l1a-guards`
- مبنا: `main`@`ccc9f74caa2cef70f70616ab95875482718c01b4`
- HEAD شاخه: `2f7f90a69f3ade8b0dbe35b9eaac7f9f711c4f25` (و commits قبلی روی همان شاخه)
- Compare: https://github.com/hamidreza1394/Accounting/compare/main...security/licensing-l1a-guards

## گام بعدی
پس از بازبینی و ادغام PR: شروع طراحی `Licensing.Contracts` (L1-B) با رعایت ODهای تأییدشده.
