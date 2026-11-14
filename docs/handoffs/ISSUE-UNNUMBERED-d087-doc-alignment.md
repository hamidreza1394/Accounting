# تحویل وظیفه — ISSUE-UNNUMBERED-d087-doc-alignment

- **Issue:** UNNUMBERED (درخواست مستقیم مالک؛ Issue جداگانه ساخته نشد)
- **Agent/مسئول:** ChatGPT
- **تاریخ:** 2026-11-14
- **وضعیت:** انجام‌شده در شاخه؛ منتظر بازبینی و CI در PR

## هدف و معیارهای پذیرش
- [x] حذف دستورهای متناقض با D-087 که Company را Tenant معرفی یا ساخت Tenant را ممنوع می‌کردند.
- [x] ثبت مدل منطقی یکسان Tenant/Company برای Shared Database و Dedicated Database.
- [x] حفظ D-013 به‌عنوان اصل اعتبارسنجی عضویت فعال سمت سرور.
- [x] حفظ CustomerBoundaryId به‌عنوان مرزی مستقل؛ عدم استنتاج Tenant از گروه/مالکیت.
- [x] ثبت تفاوت کد فعلی با مدل هدف و منع backfill/Migration پیش از طرح مصوب.
- [x] ثبت محدودیت یکتایی سراسری User/Email در Identity Realm فعلی، بدون تغییر رفتار هویت.
- [x] ثبت شکاف Migration: فرمان Server --migrate فقط DatabaseSchemaMigrator هسته را اجرا می‌کند و Migrationهای Trade را اجرا نمی‌کند.
- [x] ثبت وضعیت S3-0 بدون ادعای تکمیل ممیزی SQL Server/V3.2.

## خلاصه تغییرات
- قرارداد Agentها و مستندات معماری/امنیت/دیتابیس/Trade با D-087 هم‌راستا شدند.
- Roadmap، S3-0 را به گام‌های قابل بررسی تقسیم کرد؛ ممیزی ایستای اولیه و اصلاح مستندات از ممیزی SQL واقعی جدا شدند.
- مستندات گروه شرکتی، V3.2، HR، Fiscal Access و راهنماهای Agent نیز با مدل هدف هم‌راستا یا صریحاً به‌عنوان طرح تاریخی برچسب‌گذاری شدند.

## فایل‌های مهم تغییرکرده
- `AGENTS.md`، `docs/ARCHITECTURE.md`، `docs/ACCESS_CONTROL_ARCHITECTURE.md`، `docs/SECURITY.md`.
- `docs/DATABASE.md` و `docs/database-migrations.md`، `docs/TRADE_MODULE.md`، `docs/TRADE_DESIGN_V3_2.md` و اسناد مدل HR/Fiscal Access.
- `docs/CORPORATE_GROUPS.md` و `docs/CORPORATE_GROUPS_DATA_MODEL.md`، `docs/AGENT_REFERENCE.md` و دستورالعمل‌های Copilot/Claude.
- `docs/INDEX.md`، `docs/PROJECT_STATUS.md`، `docs/ROADMAP.md`، `docs/TESTING.md`، `docs/REPOSITORY_AUDIT.md` و `docs/WORK_LOG.md`.

## تصمیم‌های فنی
- تصمیم معماری جدیدی گرفته نشد؛ D-087 مرجع تصمیم موجود است.
- Tenant و Company مستقل‌اند؛ Shared و Dedicated از یک مدل منطقی استفاده می‌کنند.
- در کد فعلی Tenant مستقل وجود ندارد و بعضی TenantIdها عملاً Company.Id هستند؛ این وضعیت فقط به‌عنوان legacy ثبت شد.
- Username/Email در سطح Identity Realm یکتا هستند؛ تغییر دامنه یکتایی نیازمند تصمیم جداگانه است.
- CustomerBoundaryId مرزی مستقل باقی می‌ماند و منبع Tenant نیست.
- هیچ تغییر runtime/schema/migration انجام نشد.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| بازبینی متن/تطبیق ارجاعات | انجام شد | فقط مستندات و سورس‌کد از GitHub خوانده شد |
| `git diff --check` | اجرا نشده | local checkout در این محیط در دسترس نبود |
| `dotnet build` / `dotnet test` | اجرا نشده | تغییرات مستنداتی‌اند؛ CI PR باید بررسی شود |
| SQL Server schema/history verification | اجرا نشده | مرحله S3-0c باقی است |
| V3.2 full design audit | اجرا نشده | مرحله S3-0d باقی است |

## امنیت، داده و سازگاری
- هیچ دیتابیس یا Migration اجرا/تغییر نشد.
- هیچ credential یا secret اضافه نشد.
- CustomerBoundaryId، گروه و مالکیت به Tenant تبدیل نشدند.
- شکاف Migrationهای Trade فقط مستند شد؛ مسیر اجرایی تغییر نکرد.

## ریسک‌ها و کارهای باقی‌مانده
- Schema واقعی و Migration History روی SQL Server باید در S3-0c بررسی شود.
- دامنه هویت/Username تکراری بین Tenantها در دیتابیس مشترک تصمیم جداگانه می‌خواهد؛ رفتار فعلی global در Identity Realm حفظ شد.
- migration host/runner برای Core و Trade باید طراحی و تأیید شود؛ هیچ پیاده‌سازی در این PR نیست.
- نگاشت معتبر Company→Tenant و backfill باید با شواهد واقعی و بدون حدس تعیین شود.

## شناسه‌های Git
- Branch: `docs/d087-tenant-company-source-of-truth`
- Commitها: تغییرات اولیه و اصلاحات بازبینی در همین شاخه ثبت شده‌اند؛ SHA نهایی از Head فعلی PR قابل مشاهده است.
- Pull Request: [#650 — هم‌راستاسازی اسناد با D-087](https://github.com/hamidreza1394/Accounting/pull/650) (باز، منتظر CI و بازبینی).
