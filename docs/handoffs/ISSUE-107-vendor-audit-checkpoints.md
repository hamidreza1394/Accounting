# تحویل وظیفه — ISSUE-107-vendor-audit-checkpoints

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/107
- **Agent/مسئول:** ChatGPT
- **تاریخ:** 2026-10-28
- **وضعیت:** مرحلهٔ قرارداد پیاده‌سازی شد؛ CI این شاخه هنوز لازم است

## هدف و معیارهای پذیرش
- [x] تعریف checkpoint versioned با canonical payload شامل schema/hash version، event count، head hash و timestamp.
- [x] تعریف abstraction مستقل append-only برای خواندن آخرین checkpoint و افزودن checkpoint جدید.
- [x] سرویس قبل از checkpoint، زنجیره را کامل verify می‌کند؛ checkpoint قبلی نامعتبر، جلوتر از دیتابیس یا خارج از زنجیره را رد می‌کند.
- [x] تست‌های واحد canonicalization/tampering و تست Integration SQL Server افزوده شدند.
- [ ] اجرای CI واقعی روی HEAD نهایی.
- [ ] افزودن HashVersion به هر audit event و dispatch نسخه‌ای verifier.
- [ ] انتخاب و پیاده‌سازی store مستقل با credential جدا و immutability/retention.
- [ ] SQL runtime principal بدون UPDATE/DELETE، migration principal جدا، alerting و recovery runbook.

## خلاصه تغییرات
- `Vendor/Vendor.Persistence/Auditing/VendorAuditCheckpoint.cs`: مدل canonical checkpoint، hash نسخه‌دار، قرارداد `IVendorAuditCheckpointStore` و سرویس `VendorAuditCheckpointService`.
- `tests/Vendor.Host.Tests/VendorAuditCheckpointTests.cs`: آزمون UTC normalization، genesis consistency، version validation و tampering.
- `tests/Vendor.Host.Tests/VendorAuditCheckpointIntegrationTests.cs`: SQL Server واقعی، پیشروی checkpoint، عدم نوشتن تکراری و رد tampering.
- مستندات SECURITY/TESTING/PROJECT_STATUS/WORK_LOG به‌روزرسانی شدند.

## تصمیم‌های فنی
- checkpoint hash فقط تمامیت payload را کنترل می‌کند؛ برای اعتماد در برابر DBA مقصد باید مستقل و append-only/immutable باشد.
- هیچ provider واقعی یا filesystem/local DB fallback انتخاب/ثبت نشد. Fake store فقط در تست است.
- این مرحله به‌تنهایی Runtime را fail-closed نمی‌کند تا وقتی provider و wiring عملیاتی انتخاب و پیاده‌سازی شوند؛ Production readiness ادعا نمی‌شود.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| Unit tests | اجرا نشده محلی | CI لازم است |
| SQL Server integration | اجرا نشده محلی | CI باید SQL Server واقعی فراهم کند |
| Vendor CI / .NET CI | در انتظار | روی HEAD نهایی PR |
| `git diff --check` | اجرا نشده محلی | تغییرات از GitHub API ثبت شده‌اند |

## امنیت، داده و سازگاری
- هیچ secret، credential یا کلید خصوصی اضافه نشده است.
- هیچ Migration یا تغییر دیتابیس مشتری انجام نشده.
- هیچ store پیش‌فرضی به عنوان مقصد مستقل معرفی نشده است.

## ریسک‌ها و کارهای باقی‌مانده
- adapter مستقل هنوز وجود ندارد؛ بنابراین سرویس فعلاً فقط با store قراردادی قابل استفاده است.
- HashVersion در رکوردهای Audit هنوز ذخیره نمی‌شود.
- SQL permission separation، immutability/retention، alerting و recovery هنوز لازم‌اند.

## شناسه‌های Git
- Branch: `feat/issue-107-audit-checkpoint-contract`
- Implementation commits: `b4dbde9059ac63e86d1b551916658a9395489844`, `2a0787a8969bdf1e6228343b6c5a67ee2421e983`, `4959055880aafaef69fe785bfe5f34d9d2baad89`, `6973e9441d582806d99bdaf9efcb06e410934e78`.
- Pull Request: [#211](https://github.com/hamidreza1394/Accounting/pull/211); use its latest head SHA for the final CI evidence.
