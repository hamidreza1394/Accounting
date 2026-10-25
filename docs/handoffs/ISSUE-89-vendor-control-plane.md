# تحویل وظیفه — ISSUE-89-vendor-control-plane

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/89
- **Agent/مسئول:** ChatGPT
- **تاریخ:** 2026-10-25
- **وضعیت:** ناقص؛ طراحی پیشنهادی آمادهٔ بازبینی، پیاده‌سازی هنوز آغاز نشده است.

## هدف و معیارهای پذیرش
- [x] تأیید ادغام PR #84 و ثبت D-046.
- [x] بازبینی ساختار Solution، `Server/Server.csproj`، `Directory.Packages.props`، `global.json` و قرارداد `Licensing.Contracts`.
- [x] تعریف Issue مستقل برای L2: #89.
- [x] ثبت D-047 به‌صورت Proposed و همگام‌سازی اسناد مرتبط.
- [ ] پذیرش معماری D-047 توسط بازبینی انسانی.
- [ ] ساخت Vendor Host/DB/Identity، تست‌ها و Workflow مستقل در PRهای مرحله‌ای پس از بازبینی.

## خلاصه تغییرات
این مرحله فقط طرح معماری و مستندات است. هیچ کد اجرایی، پکیج، endpoint، جدول، migration، کلید یا Secret ایجاد نشده است.

## فایل‌های مهم تغییرکرده
- `docs/DECISIONS.md` — D-047 (Proposed).
- `docs/LICENSING_OPEN_DECISIONS.md` — وضعیت D-046 و مسیر L2.
- `docs/LICENSING.md` — مرجع تصمیم‌های جدید.
- `docs/ROADMAP.md` — Issue #89 و شرط پذیرش معماری.
- `docs/INDEX.md` — شماره ADR بعدی.
- `docs/WORK_LOG.md` — ثبت اقدام و محدودیت‌ها.
- `docs/handoffs/ISSUE-89-vendor-control-plane.md` — این گزارش.

## تصمیم‌های فنی
- D-047 فقط Proposed است، نه Accepted.
- Vendor باید Host، Identity، DbContext، SQL Server database و CI/Publish boundary مستقل داشته باشد؛ تنها قرارداد مشترک `Licensing.Contracts` است.
- MFA برای حساب‌های انسانی، bootstrap محدود و ممیزی‌شده، دو مدیر واجد شرایط پیش از Production و maker-checker سمت سرور.
- Hash-chain به‌تنهایی ضددستکاری کامل نیست؛ نیاز به validation و checkpoint مستقل دارد.
- KMS/HSM Production باید sign-only و fail-closed باشد؛ provider واقعی و recovery runbook هنوز تعیین نشده‌اند.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| بررسی PR #84 | موفق | GitHub تأیید می‌کند merged؛ merge commit `8ba9e7e4add2be183d031fef355a561c4e73fd1f`. |
| بازبینی فایل‌های معماری و قرارداد | انجام شد | GitHub main؛ نه checkout محلی |
| `dotnet build` | اجرا نشده | محیط این وظیفه اجرای build محلی فراهم نکرده است. |
| تست‌های .NET/SQL Server | اجرا نشده | باید با CI واقعی روی PR اجرا شود. |
| بررسی CI روی HEAD نهایی | NotVerified | پس از ساخت PR باید وضعیت jobها بررسی شود. |

## امنیت، داده و سازگاری
- هیچ دیتابیس یا Migration مشتری تغییر نکرده است.
- هیچ کلید خصوصی یا Secret ایجاد/ثبت نشده است.
- سازگاری واقعی WebAuthn/passkey با APIهای .NET 10 باید قبل از انتخاب روش نهایی آزموده شود.
- Hash-chain داخل همان DB، دستکاری توسط DBA را به‌تنهایی ناممکن نمی‌کند.

## ریسک‌ها و کارهای باقی‌مانده
- بازبینی انسانی و پذیرش D-047.
- تعیین KMS/HSM، recovery/break-glass، alerting و مقصد مستقل audit checkpoint.
- پیاده‌سازی مرحله‌ای و اجرای Build/Integration tests/CI واقعی.

## شناسه‌های Git
- Branch: `docs/licensing-l2-vendor-architecture`
- Commit: در زمان نگارش هنوز نهایی نشده است.
- Pull Request: هنوز ایجاد نشده است.
