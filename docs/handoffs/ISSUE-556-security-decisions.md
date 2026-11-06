# تحویل وظیفه — ISSUE-556-security-decisions

- **Issue:** [#556](https://github.com/hamidreza1394/Accounting/issues/556)
- **Agent/مسئول:** ChatGPT — مستندسازی معماری و امنیت
- **تاریخ:** 2026-11-06
- **وضعیت:** مستندات روی شاخه مستقل ثبت شد؛ PR برای بازبینی باز می‌شود؛ Merge انجام نشده است.

## هدف و معیارهای پذیرش
- [x] D-070 موجود با همان شماره از Proposed به Accepted ارتقا یابد؛ سابقه حذف یا شماره تکرار نشود.
- [x] سیاست نگهداری ۱۲/۲۴/۳۶ ماه و راهکار Cloud high-volume شامل Hot/Archive، Legal Hold، integrity و restore ثبت شود.
- [x] تصمیم‌های ابطال نشست مدیر نصب، بازیابی رمز، Reset مدیریتی و MFA در ADR جدید ثبت شوند.
- [x] جدایی Permission و Entitlement و ترتیب کنترل مشترک ماژول‌ها در ADR مستقل ثبت شود.
- [x] وضعیت تصمیم از وضعیت پیاده‌سازی جدا بماند.
- [x] شماره‌گذاری بعدی ADR با آخرین شماره واقعی D-072 هماهنگ شود.
- [x] اسناد امنیت، API contract، licensing، access architecture، index و AGENTS هم‌تراز شوند.

## خلاصه تغییرات
- D-070: Accepted؛ baseline retention ۱۲/۲۴/۳۶ ماه و Legal Hold؛ آرشیو ساده فایل‌محور و رمزنگاری‌شده با Job زمان‌بندی‌شده؛ تطبیق تعداد/checksum پیش از حذف batchی از DB؛ بدون دیتابیس دوم، WORM یا deletion ledger اجباری در مرحله اول.
- D-073: Accepted؛ admin session revoke، Cloud/Offline recovery، user-controlled Admin Reset، MFA و سیاست رمز. پیاده‌سازی Customer هنوز عمدتاً NotImplemented است.
- D-074: Accepted؛ Permission و Entitlement مستقل و هر دو لازم؛ ترتیب canonical کنترل و Cloud/Offline authority. قرارداد v2 و Trade gate در main هستند اما runtime Trade هنوز DenyAll است.
- شماره ADR بعدی به D-075 اصلاح شد.

## فایل‌های مهم تغییرکرده
- `docs/DECISIONS.md` — تکمیل D-070 و افزودن D-073/D-074.
- `docs/SECURITY.md` — خلاصه اجرایی سیاست‌های مصوب و وضعیت واقعی.
- `docs/api/USER_ACCESS_MANAGEMENT_API.md` — تفکیک تصمیم پذیرفته‌شده از APIهای هنوز ناموجود.
- `docs/FRONTEND_API_CONTRACT_NEEDS.md` — به‌روزرسانی نیازهای Frontend بدون ساخت قرارداد فرضی.
- `docs/ACCESS_CONTROL_ARCHITECTURE.md` و `docs/LICENSING.md` — هم‌ترازی مرزهای Permission/Entitlement.
- `docs/INDEX.md` — ارجاع ADRها و handoff.
- `AGENTS.md` — اصلاح شماره ADR بعدی.
- `docs/handoffs/ISSUE-556-security-decisions.md` و `docs/WORK_LOG.md` — تحویل و سابقه.

## تصمیم‌های فنی
- هیچ ADR قبلی حذف یا بازشماری نشد؛ D-070 موجود تکمیل شد و D-073/D-074 به‌ترتیب بعد از D-072 اضافه شدند.
- دوره‌های ۱۲/۲۴/۳۶ ماه baseline محصول هستند، نه ادعای قانون یکسان جهانی؛ استثنای کوتاه‌تر نیازمند مبنای قانونی/قراردادی و تأیید مستند است.
- حجم بالا باعث کوتاه‌کردن retention نمی‌شود؛ Job آرشیو ماهانه یا زودتر هنگام نزدیک‌شدن به آستانه ظرفیت، فایل فشرده/رمزنگاری‌شده تولید می‌کند؛ پس از تطبیق تعداد و checksum، رکوردها batchی از DB حذف می‌شوند. شکست انتقال رکورد DB را حفظ می‌کند.
- Entitlement در CompanyAccessService ادغام نمی‌شود؛ RBAC و Licensing مرجع مستقل دارند و Gate آن‌ها را ترکیب می‌کند.
- هیچ API، Schema، Migration، Job، Purge یا رفتار runtime در این Issue ساخته نشده است.

## بررسی و آزمون
| بررسی | نتیجه واقعی | توضیح |
|---|---|---|
| خواندن AGENTS.md و DECISIONS.md | موفق | main @ `553405d4f773683b5a43eb08f12dfa4f3669697a` |
| بررسی کد/اسناد Entitlement و Trade gate | موفق | `ITradeModuleEntitlement`، `TradeAccessGate` و `DenyAllTradeModuleEntitlement` بررسی شدند |
| Build / Unit / Integration / E2E | اجرا نشده | تغییرات مستنداتی‌اند؛ هیچ کدی تغییر نکرده است |
| git diff --check | اجرا نشده | تغییرات از GitHub Contents API ثبت شده‌اند؛ پیش از Merge باید PR diff و Markdown review شود |
| CI | در انتظار PR | نتیجه‌ای به‌عنوان موفق گزارش نمی‌شود |

## امنیت، داده و سازگاری
- هیچ Schema، Migration، API یا job اجرایی تغییر نکرد.
- D-070 تا تصویب طراحی اجرایی و حقوقی هیچ purge مجاز نمی‌کند.
- D-073 هیچ endpoint فرضی را به API contract اضافه نمی‌کند.
- D-074 Trade را باز نمی‌کند؛ `DenyAllTradeModuleEntitlement` تا اتصال واقعی باقی می‌ماند.
- تصمیم‌های باز: provider و SLO آرشیو، Hot Window، حوزه‌های قانونی، Backup retention، جزئیات break-glass و emergency revoke-all.

## ریسک‌ها و کارهای باقی‌مانده
- بررسی حقوقی/قراردادی retention برای هر بازار و مشتری.
- طراحی و پیاده‌سازی Archive pipeline، Legal Hold، purge safe, backup/restore reconciliation.
- پیاده‌سازی D-073 در API/UI و تست‌های امنیتی/SQL Server.
- تکمیل اتصال Customer runtime به Entitlement امضاشده و تعمیم به سایر ماژول‌ها.
- بازبینی کامل متن ADR و API contract در PR.

## شناسه‌های Git
- Branch: `docs/adr-073-074-account-security-entitlement`
- آخرین Commit: در ادامه PR #557 به‌روزرسانی می‌شود.
- Pull Request: [#557](https://github.com/hamidreza1394/Accounting/pull/557) — Open, not merged.
