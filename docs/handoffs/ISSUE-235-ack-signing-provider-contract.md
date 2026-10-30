# تحویل وظیفه — ISSUE-235-ack-signing-provider-contract

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/235
- **Agent/مسئول:** Claude Code
- **تاریخ:** 2026-10-30
- **وضعیت:** ناقص عمدی (قرارداد و تست انجام شد؛ Provider واقعی Production مسدود روی #236)

## هدف و معیارهای پذیرش
- [x] Interface مستقل امضا (از قبل در `main`) + قرارداد Provider مستند و آزمون‌شده.
- [x] اعتبارسنجی آفلاین امضا با Trust Store مستقل مشتری پیش از ذخیره‌سازی.
- [x] رد امضای جعلی/بدشکل، کلید ناشناخته، Provider ناموجود و Provider که Exception می‌دهد؛ بدون هیچ رکورد ذخیره‌شده.
- [x] کلید خصوصی در مخزن/نصب مشتری نیست؛ فقط کلید عمومی در پیکربندی.
- [ ] اتصال Provider واقعی KMS/HSM: `Blocked` تا تصمیم و پیکربندی #236.

## خلاصه تغییرات
- شکاف واقعی: `SignAcknowledgementAsync` فقط شکل خروجی Provider را می‌سنجید (طول ۶۴ بایت) و بدون راستی‌آزمایی در همان تراکنش ذخیره می‌کرد. Provider معیوب یا جایگزین‌شده می‌توانست شرکت را بسازد و ack غیرقابل‌تأییدی برای همیشه ذخیره کند (replay همان را برمی‌گرداند و Vendor هرگز تأیید نمی‌کرد).
- حالا امضا در سمت مشتری با `Provisioning:TrustedAcknowledgementKeys:<keyId>` (کلید عمومی ECDSA P-256) و همان اعتبارسنج Vendor راستی‌آزمایی می‌شود. خطاها fail-closed با سه کد مجزا و HTTP 503؛ تراکنش Commit نمی‌شود.
- Exception ارائه‌دهنده گرفته می‌شود؛ فقط نوع Exception لاگ می‌شود.
- رفتار حالت موفق و replay تغییری نکرد.

## فایل‌های مهم تغییرکرده
- `Server/Infrastructure/Provisioning/CustomerProvisioningTrustStore.cs` — استخراج خواندن کلید عمومی (refactor جدا، بدون تغییر رفتار).
- `Server/Infrastructure/Provisioning/CustomerProvisioningAcknowledgementTrustStore.cs` — Trust Store جدید کلیدهای امضای ack.
- `Server/Infrastructure/Provisioning/CustomerProvisioningApplyService.cs` — راستی‌آزمایی آفلاین، کدهای خطا، گرفتن Exception، Logger.
- `Server/Program.cs`, `Server/Api/ApiEndpoints.cs` — ثبت DI و نگاشت کدها به 503.
- `tests/Template.Tests/Integration/CustomerProvisioningClaimsSqlServerTests.cs` — تست جدید روی SQL Server واقعی.
- `docs/SECURITY.md` — قرارداد Provider.

## تصمیم‌های فنی
- Trust Store جداگانه برای کلید ack (نه ترکیب با کلید Vendor) تا کلیدهای دو نقش با هم قاطی نشوند.
- تصمیم معماری جدیدی (ADR) گرفته نشد؛ انتخاب KMS/HSM همچنان `NeedsApproval` در #236 است.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `dotnet build Template.sln -c Release` | موفق | 0 خطا |
| `dotnet test` CustomerProvisioningClaimsSqlServerTests (SQL Server 2022 واقعی) | موفق | 4 تست (۳ موجود + ۱ جدید با ۵ حالت خراب × دو مسیر + شاهد) |
| بررسی جهش (Mutation): غیرفعال‌کردن موقت راستی‌آزمایی | تست جدید ناموفق شد | ثابت می‌کند تست واقعاً شکاف را می‌گیرد؛ کد بازگردانده شد |
| `dotnet test Template.sln -c Release` | موفق | 657 کل، 655 موفق، 2 skipped، 0 ناموفق |
| Vendor.sln / کلاینت / E2E | اجرا نشد | تغییری در آن‌ها نبود |
| بازبینی مستقل Subagent | اجرا نشد | Subagent فراخوانی نشد؛ بازبینی Diff توسط نویسنده |

## امنیت، داده و سازگاری
- تغییر فقط سخت‌گیرانه‌تر است. استقرار بدون `TrustedAcknowledgementKeys` و بدون Provider واقعی مثل قبل fail-closed می‌ماند (503).
- هیچ Schema/Migration و هیچ کلید خصوصی اضافه نشد. کلید عمومی ack باید هم‌زمان در Trust Store Vendor ثبت شود.

## ریسک‌ها و کارهای باقی‌مانده
- Provider واقعی (KMS/HSM)، چرخش کلید و تأیید دو نفره: #236.
- کلید ack فعلی/قبلی هم‌زمان در Trust Store مشتری قابل نگهداری است (چرخش)، ولی رویهٔ عملیاتی چرخش هنوز مستند نشده.

## شناسه‌های Git
- Branch: `feat/issue-235-ack-signing-provider-contract`
- Pull Request: پس از ساخت PR ثبت می‌شود.
