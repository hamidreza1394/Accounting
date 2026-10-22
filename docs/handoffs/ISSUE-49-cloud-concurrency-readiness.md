# تحویل وظیفه — ISSUE-49-cloud-concurrency-readiness

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/49
- **Agent/مسئول:** ChatGPT — ممیزی مستندات GitHub و آماده‌سازی handoff
- **تاریخ:** 2026-10-22
- **وضعیت:** ممیزی و مستندسازی انجام شد؛ اجرای آزمون بار و اصلاح کد در گام بعدی است.

## هدف و معیارهای پذیرش
- [x] بررسی مستندات معماری، وضعیت، راهبرد تست، استقرار، دستورهای Agent و فایل‌های اصلی مرتبط از مخزن GitHub.
- [x] تفکیک شواهد موجود از گلوگاه‌های نیازمند اندازه‌گیری runtime.
- [x] ثبت اینکه ظرفیت 100 کاربر هنوز تأیید نشده است.
- [x] تهیه سند معیارها و پرامپ اجرایی برای Agent بعدی.
- [x] هماهنگ‌سازی INDEX، PROJECT_STATUS، ROADMAP، ARCHITECTURE، TESTING، DEPLOYMENT، REPOSITORY_AUDIT و WORK_LOG.
- [ ] اجرای Load/Stress/Soak روی محیط staging.
- [ ] اصلاح گلوگاه‌های اثبات‌شده با تست و اندازه‌گیری قبل/بعد.

## خلاصه تغییرات
- اضافه شد `docs/CLOUD_CONCURRENCY_READINESS.md` با یافته‌های ممیزی، معیارهای آزمون، ایمنی اجرا و وضعیت `NotVerified`.
- اضافه شد `docs/prompts/CLOUD_CONCURRENCY_IMPLEMENTATION_PROMPT.md` با دستور مرحله‌ای برای Agent کدنویس.
- مستندات معماری، تست، استقرار، roadmap، audit و وضعیت پروژه با مرجع Issue #49 هماهنگ شدند.
- هیچ کد برنامه، Schema، Migration، package version یا deployment topology در این وظیفه تغییر نکرد.

## فایل‌های مهم تغییرکرده
- `docs/CLOUD_CONCURRENCY_READINESS.md` — سند اصلی ممیزی و معیارهای آمادگی بار.
- `docs/prompts/CLOUD_CONCURRENCY_IMPLEMENTATION_PROMPT.md` — پرامپ کامل اجرا برای Agent بعدی.
- `docs/INDEX.md` — افزودن اسناد جدید به فهرست.
- `docs/ARCHITECTURE.md` — اصول scale-out و منع over-engineering.
- `docs/TESTING.md` — معیارهای Load/Stress/Soak.
- `docs/DEPLOYMENT.md` — الزامات میزبانی و چندنمونه‌ای.
- `docs/ROADMAP.md` — کارهای باقیمانده.
- `docs/REPOSITORY_AUDIT.md` — یافته‌های CC-01 تا CC-08.
- `docs/PROJECT_STATUS.md` — ثبت وضعیت `NotVerified`.
- `docs/WORK_LOG.md` — سابقه این دور.

## تصمیم‌های فنی
- معماری Modular Monolith و .NET 10 / EF Core 10 / SQL Server حفظ می‌شود.
- ابزار load test و تغییرات runtime در گام بعدی انتخاب می‌شوند؛ در این دور ADR جدیدی ایجاد نشده چون هنوز انتخاب معماری یا زیرساخت نهایی لازم نیست.
- اعداد p95 و نرخ خطا صرفاً اهداف اولیه آزمون‌اند، نه SLA یا تضمین عملکرد.
- عدم وجود Redis/Kubernetes/microservices به‌تنهایی نقص اعلام نشده است.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| خواندن فایل‌های GitHub | موفق | کد و مستندات مرتبط در main خوانده شدند؛ محدود به فایل‌های ذکرشده در سند ممیزی |
| جست‌وجوی کد برای load test/k6/NBomber | بدون نتیجه در جست‌وجوی انجام‌شده | این جست‌وجو به‌تنهایی اثبات جامع نبود هر ابزار نیست؛ Agent بعدی باید دوباره بررسی کند |
| `dotnet restore/build/test` | اجرا نشد | این دور ممیزی مستنداتی از راه GitHub connector بود، نه محیط checkout |
| Load/Stress/Soak | اجرا نشد | هنوز محیط staging و ابزار/سناریو تعیین نشده‌اند |
| `git diff --check` | اجرا نشد | در این دور checkout محلی وجود نداشت؛ PR diff باید توسط Agent/CI بررسی شود |

## امنیت، داده و سازگاری
- هیچ کد اجرایی، مجوزدهی، Schema یا Migration تغییر نکرد.
- آزمون آینده فقط در محیط مجاز/ایزوله با داده مصنوعی اجرا شود.
- D-013 و `CompanyAccessService` باید حفظ شوند؛ هیچ bypass tenant اضافه نشود.
- تغییرات مستقیم به main یا merge خودکار انجام نشد.

## ریسک‌ها و کارهای باقی‌مانده
- ظرفیت واقعی، CPU/RAM، SQL latency/waits، connection pool، deadlock و p95/p99 هنوز نامعلوم‌اند.
- تعداد نمونه‌ها، سخت‌افزار/ابر، نسخه SQL Server و مدل بار واقعی هنوز مشخص نیستند.
- ماژول‌های حسابداری اصلی در وضعیت فعلی `NotImplemented` هستند؛ صحت هم‌زمانی ثبت سند قابل ادعا نیست.
- مرحله بعد باید ابزار بار انتخاب کند، baseline را اجرا کند، فقط گلوگاه‌های اثبات‌شده را اصلاح کند و نتایج را به commit و محیط دقیق پیوند دهد.

## شناسه‌های Git
- **Branch:** `docs/issue-49-cloud-concurrency-readiness`
- **Commit:** در زمان نگارش handoff، PR هنوز ایجاد نشده است.
- **Pull Request:** پس از تکمیل مستندات ایجاد می‌شود.
