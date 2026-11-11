# تحویل وظیفه — ISSUE-107-vendor-audit-checkpoints

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/107
- **Agent/مسئول:** ChatGPT
- **تاریخ:** 2026-10-31
- **وضعیت:** قرارداد، versioned event hash، Production fail-closed guard، startup verify و periodic health monitoring پیاده‌سازی شده‌اند؛ sink مستقل هنوز انتخاب نشده است

## هدف و معیارهای پذیرش
- [x] تعریف checkpoint versioned با canonical payload شامل schema/hash version، event count، head hash و timestamp.
- [x] تعریف abstraction مستقل append-only برای خواندن آخرین checkpoint و افزودن checkpoint جدید.
- [x] سرویس قبل از checkpoint، زنجیره را کامل verify می‌کند؛ checkpoint قبلی نامعتبر، جلوتر از دیتابیس یا خارج از زنجیره را رد می‌کند.
- [x] تست‌های واحد canonicalization/tampering و تست Integration SQL Server افزوده شدند.
- [x] اجرای CI واقعی پس از Merge #633: Vendor CI و .NET CI موفق؛ regression چندپردازه‌ای روی Ubuntu/Windows نیز موفق.
- [x] افزودن HashVersion به هر audit event و dispatch نسخه‌ای verifier (در main ادغام شده).
- [ ] انتخاب و پیاده‌سازی store مستقل با credential جدا و immutability/retention.
- [x] Production startup guard و fail-closed startup verification.
- [x] periodic full-chain verification/checkpoint advance و health endpoint برای alert monitoring.
- [x] SQL runtime append-only permission script و migration principal جدا.
- [x] recovery runbook و restore/sink-outage guidance.

## خلاصه تغییرات
- `Vendor/Vendor.Persistence/Auditing/VendorAuditCheckpoint.cs`: مدل canonical checkpoint، hash نسخه‌دار، قرارداد `IVendorAuditCheckpointStore` و سرویس `VendorAuditCheckpointService`.
- `tests/Vendor.Host.Tests/VendorAuditCheckpointTests.cs`: آزمون UTC normalization، genesis consistency، version validation و tampering.
- `tests/Vendor.Host.Tests/VendorAuditCheckpointIntegrationTests.cs`: SQL Server واقعی، پیشروی checkpoint، عدم نوشتن تکراری و رد tampering.
- مستندات SECURITY/TESTING/PROJECT_STATUS/WORK_LOG به‌روزرسانی شدند.

## تصمیم‌های فنی
- checkpoint hash فقط تمامیت payload را کنترل می‌کند؛ برای اعتماد در برابر DBA مقصد باید مستقل و append-only/immutable باشد.
- هیچ provider واقعی یا filesystem/local DB fallback انتخاب/ثبت نشد. Fake store فقط در تست است. Production تا ثبت دقیقاً یک IProductionVendorAuditCheckpointStore متوقف می‌ماند.
- این مرحله به‌تنهایی Runtime را fail-closed نمی‌کند تا وقتی provider و wiring عملیاتی انتخاب و پیاده‌سازی شوند؛ Production readiness ادعا نمی‌شود.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| Unit tests | اجرا نشده محلی | CI لازم است |
| SQL Server integration | اجرا نشده محلی | CI باید SQL Server واقعی فراهم کند |
| Vendor CI / .NET CI | موفق پس از Merge #633 | Vendor CI run `42783806157` و .NET CI run `42783805952` روی SHA `3b103a...` |
| `git diff --check` | اجرا نشده محلی | تغییرات از GitHub API ثبت شده‌اند |

## امنیت، داده و سازگاری
- هیچ secret، credential یا کلید خصوصی اضافه نشده است.
- هیچ Migration یا تغییر دیتابیس مشتری انجام نشده.
- هیچ store پیش‌فرضی به عنوان مقصد مستقل معرفی نشده است.

## ریسک‌ها و کارهای باقی‌مانده
- adapter مستقل هنوز وجود ندارد؛ بنابراین سرویس فعلاً فقط با store قراردادی قابل استفاده است.
- `HashVersion=1` در هر `VendorAuditEvent` ذخیره می‌شود و verifier بر اساس نسخه dispatch می‌کند؛ این بخش در main موجود است.
- انتخاب و پیاده‌سازی sink مستقل، credential جدا، immutability/retention و اجرای واقعی alert notification/restore drill هنوز لازم‌اند.

## شناسه‌های Git
- Branch جدید: `feat/issue-107-production-audit-operations`.
- PR در حال آماده‌سازی است؛ CI نهایی پس از ایجاد PR ثبت خواهد شد.
- PRهای قبلی #211 و PR مربوط به startup guard در main ادغام شده‌اند.


## Recovery runbook addition (2026-10-29)
- Added provider-neutral operational guidance at `docs/runbooks/vendor-audit-checkpoint-recovery.md` covering preflight verification, backup, restore reconciliation, sink outage, SQL permission separation and exit criteria.
- The runbook intentionally does not choose a provider, define fictional credentials or claim Production readiness. Issue #107 remains open until a real independently permissioned sink is selected and an environment restore drill is evidenced.


## 2026-11-11 — شواهد جاری و مرز Production

- مبنای جاری: `main@3b103a5875a9668bf0c07d305a91685db3fb1db7`.
- .NET CI run [42783805952](https://github.com/hamidreza1394/Accounting/actions/runs/42783805952) و Vendor CI run [42783806157](https://github.com/hamidreza1394/Accounting/actions/runs/42783806157) پس از Merge #633 موفق‌اند؛ Vendor CI شامل regression قفل فایل بین‌پردازه‌ای روی Ubuntu و Windows است.
- `VendorAuditEvent.HashVersion` در کد جاری ذخیره می‌شود؛ canonical hash version dispatch و startup/periodic verification از این تغییر مستقل‌اند.
- adapter فایل فقط Development/Test است. هیچ `IProductionVendorAuditCheckpointStore` عملیاتی ثبت نشده و هیچ مقصد cloud/object-store یا filesystem محلی به‌عنوان sink مستقل انتخاب نشده است.
- تا تعیین مقصد مستقل، هویت/credential جدا، سیاست retention/immutability، alerting و اجرای restore/recovery drill، Production باید fail-closed بماند. CI سبز جایگزین failure-domain مستقل نیست.
