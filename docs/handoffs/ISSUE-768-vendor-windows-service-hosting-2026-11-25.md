# تحویل وظیفه — ISSUE-768-vendor-windows-service-hosting-2026-11-25

- **Issue:** [#768 — آمادگی استقرار محلی و راه دور Vendor](https://github.com/hamidreza1394/Accounting/issues/768)
- **Agent/مسئول:** ChatGPT — ممیزی و هم‌ترازی مستندات
- **تاریخ:** 2026-11-25
- **وضعیت:** ناقص — Host integration و CI تأیید شده؛ نصب و آزمون عملیاتی سرویس‌ها انجام نشده است.

## هدف و معیارهای پذیرش

- [x] تأیید کد ادغام‌شده برای Native Windows Service lifetime در Vendor و Customer.
- [x] تأیید حفظ مسیر console-only برای فرمان‌های CLI/Key Ceremony.
- [x] تأیید انتقال read-only Customer migration probe به بعد از `ApplicationStarted`.
- [x] تأیید G2 allow-list برای dependencyهای دقیق Windows Service با تست مثبت Bash/PowerShell و حفظ تست منفی.
- [x] تأیید CI پس از Merge #787 و CI روی main جاری.
- [ ] نصب واقعی هر دو سرویس روی Windows Server هدف با هویت‌های مستقل و ACL حداقلی.
- [ ] اجرای HTTP smoke و بررسی تنظیمات/Secret Store/SQL با هویت واقعی سرویس.
- [ ] اجرای چرخهٔ Staging و آزمون مستقل دو میزبان.

## خلاصه تغییرات

- PR #787 در 2026-11-24T07:44:48Z با merge commit `35bd3905537023051b9372f01234cc4027590ab7` ادغام شد.
- `Vendor/Vendor.Host/Program.cs` از `UseWindowsService` با ServiceName برابر `Farano Vendor` استفاده می‌کند.
- `Server/Program.cs` ServiceName برابر `Farano Customer` را ثبت می‌کند؛ فرمان‌های اپراتوری قبل از فعال‌سازی Windows Service lifetime خاتمه می‌یابند.
- `PendingDatabaseMigrationCheckService` بررسی read-only Migrationهای Customer را پس از `ApplicationStarted` اجرا می‌کند؛ Migration را اعمال نمی‌کند.
- G2 allow-list سه dependency دقیق `Microsoft.Extensions.Hosting.WindowsServices.dll`، `System.ServiceProcess.ServiceController.dll` و `runtimes/win/lib/net10.0/System.ServiceProcess.ServiceController.dll` را می‌پذیرد؛ تست‌های منفی ناشناخته حفظ شده‌اند.
- این PR مشترک Host بود؛ هیچ Customer consumer، Customer schema یا Migration دیتابیس مشتری در آن اضافه نشد.

## فایل‌های مهم تغییرکرده در PR #787

- `Vendor/Vendor.Host/Program.cs` — ثبت Windows Service lifetime برای Vendor.
- `Server/Program.cs` — ثبت Windows Service lifetime برای Customer و انتقال probe.
- `Server/Infrastructure/Hosting/PendingDatabaseMigrationCheckService.cs` — اجرای probe پس از شروع Host.
- `scripts/lib/publish-assembly-allowlist.txt` — allow-list محدود G2.
- `scripts/tests/publish-content-guard.test.sh` و `scripts/tests/publish-content-guard.tests.ps1` — fixtureهای مثبت/منفی.
- `tests/Template.Tests/PendingDatabaseMigrationCheckServiceTests.cs` — regression ترتیب ApplicationStarted.

## تصمیم‌های فنی

- تصمیم معماری جدیدی گرفته نشد؛ Windows Service hosting در چارچوب Issue #768 و جداسازی Vendor/Customer انجام شده است.
- `UseWindowsService` فقط lifetime/ServiceName را فراهم می‌کند؛ نصب سرویس، ساخت حساب سرویس، ACL، Secret Store، DataProtection، مجوز SQL و Trust provisioning نیست.
- Migration probe read-only است و پس از ApplicationStarted اجرا می‌شود تا SCM startup را معطل نکند؛ اجرای خودکار Migration اضافه نشده است.
- شکست CI اولیه ناشی از سه فایل خارج از G2 allow-list بود؛ هر سه Job شکست‌خورده همان guard را گزارش کردند. allow-list دقیق اصلاح شد و هیچ guard یا تست منفی تضعیف نشد.

## بررسی و آزمون

| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| PR #787 post-merge check runs | موفق — ۲۰/۲۰ | commit `35bd3905537023051b9372f01234cc4027590ab7`; همهٔ check runهای خوانده‌شده `success` |
| .NET CI پس از Merge #787 | موفق | [Run #45350378180](https://github.com/hamidreza1394/Accounting/actions/runs/45350378180) |
| Vendor CI پس از Merge #787 | موفق | [Run #45350378170](https://github.com/hamidreza1394/Accounting/actions/runs/45350378170) |
| Semgrep پس از Merge #787 | موفق | [Run #45350378311](https://github.com/hamidreza1394/Accounting/actions/runs/45350378311) |
| main فعلی `50a15eae81d4b88657a3a981f59e0ae89f271a91` | موفق — ۱۲/۱۲ | .NET #45567365188، CodeQL #45567365166، Semgrep #45567365152 |
| PR #281 / #282 / #284 پس از Merge | موفق — ۱۲/۱۲، ۱۳/۱۳، ۱۳/۱۳ | Check Runها روی merge SHAهای واقعی |
| اجرای ناموفق قبلی #787 | ناموفق — ۳ Job | Run #45338174154؛ علت ریشه‌ای G2 allow-list و سه dependency Windows Service؛ اصلاح در #787 و CI بعدی سبز |
| `dotnet build` محلی | اجرا نشده | محیط محلی مخزن در این اجرا در دسترس نبود |
| `dotnet test` محلی | اجرا نشده | به CI واقعی پس از Merge اتکا شده است |
| `git diff --check` محلی | اجرا نشده | تغییرات مستنداتی از طریق GitHub ثبت می‌شوند؛ CI PR باید بررسی شود |
| نصب/HTTP smoke روی Windows Server | اجرا نشده | نیازمند میزبان، هویت سرویس و دسترسی انسانی |

## امنیت، داده و سازگاری

- هیچ private key، credential، password hash، connection string یا secret وارد Git، artifact یا log نشده است.
- هیچ Customer consumer، Customer migration/schema یا دادهٔ مشتری تغییر نکرد.
- G2 fail-closed و تست منفی assembly ناشناخته حفظ شده‌اند.
- Vendor DB و Customer DB مستقل باقی می‌مانند؛ دسترسی SQL مستقیم Vendor به Customer اضافه نشده است.
- Runtime Gate و provisioning v5 Gate تغییری نکرده‌اند.

## ریسک‌ها و کارهای باقی‌مانده

- نصب واقعی سرویس‌ها و تعیین هویت‌های مستقل، ACL، environment/Secret Store، DataProtection، AllowedHosts و مجوزهای SQL.
- اجرای CNG Key Ceremony، ACL، Trust Store، rotation/overlap و recovery طبق #236.
- trust distribution/rotation و آزمون Customer consumer/ACK طبق #235.
- Bootstrap D-110 و Customer Migration کنترل‌شده طبق #392؛ v5 gate تا اثبات همهٔ پیش‌نیازها بسته بماند.
- Delivery/ACK/Applied tracking Amendment در #393/#394 تا تصویب قرارداد S-B شروع نشود.
- sink مستقل Production و recovery drill طبق #107.
- اجرای اولیهٔ Staging روی یک میزبان با دو سرویس و دو DB مستقل، سپس آزمون دو میزبان؛ هیچ‌کدام هنوز اجرا نشده‌اند.

## شناسه‌های Git

- **Base:** `50a15eae81d4b88657a3a981f59e0ae89f271a91`
- **Branch:** `docs/vendor-service-hosting-status-2026-11-25`
- **Commit:** در تاریخچهٔ شاخه و PR ثبت می‌شود.
- **Pull Request:** [#793](https://github.com/hamidreza1394/Accounting/pull/793) — docs-only؛ HEAD `0a35ece049038d179677e472a63703898d49eb4d`، ۱۵/۱۵ Check Run تکمیل‌شده (۱۴ `success`، یک `neutral` CodeRabbit، صفر pending/failure)، `mergeable=true` و بدون review thread؛ در زمان ثبت هنوز ادغام نشده بود.
