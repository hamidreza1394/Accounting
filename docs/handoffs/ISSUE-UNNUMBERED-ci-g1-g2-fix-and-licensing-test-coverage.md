# تحویل وظیفه — ISSUE-UNNUMBERED-ci-g1-g2-fix-and-licensing-test-coverage

- **Issue:** UNNUMBERED
- **Agent/مسئول:** Claude Code (مأموریت جامع ادامهٔ توسعه، 2026-10-25)
- **تاریخ:** 2026-10-25
- **وضعیت:** ناقص (دو شکست CI رفع و بازتولید‌شده؛ شکست سوم `migration guards` فقط تشخیص داده شد)

## هدف و معیارهای پذیرش
- [x] ریشهٔ شکست `build-and-test` و `publish-scripts` پیدا و با کمینه‌ترین تغییر رفع شود.
- [x] تست جدید/ترمیم‌شده برای هر رفع؛ هیچ تستی حذف یا تضعیف نشود.
- [ ] Guardهای مهاجرت (`generate_migrations.py --check`، `verify_migration_model.py`) سبز شوند — **انجام نشد** (پایین).

## خلاصه تغییرات
- G1: `using Xunit;` در `ArchitectureDependencyGuardTests.cs` افزوده شد (علت خطای Build).
- G2: دو `printf` در `publish-content-guard.test.sh` با `--` اصلاح شد (آرگومان `-----BEGIN` به‌عنوان گزینه خوانده می‌شد و فیکسچر خالی می‌ماند) + بررسی غیرخالی‌بودن فیکسچر.
- Licensing: `EvaluatorStateTests.cs` پوشش حذف‌شده در PR #74 (ماشین حالت زمان D-026، کلید اشتباه) را بازمی‌گرداند و مرز دقیق زمان، FormatVersion ناشناخته، امضای کوتاه را می‌افزاید.

## فایل‌های مهم تغییرکرده
- `tests/Template.Tests/ArchitectureDependencyGuardTests.cs`
- `scripts/tests/publish-content-guard.test.sh`
- `tests/Licensing.Contracts.Tests/EvaluatorStateTests.cs` (جدید)

## تصمیم‌های فنی
- تصمیم معماری جدیدی گرفته نشد. کد تولیدی لایسنس تغییر نکرد.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| Build Release | موفق | پس از افزودن `using Xunit;` |
| تست‌های .NET روی SQL Server واقعی | موفق (به‌جز ۲ مورد از پیش‌موجود در گزارش اولیه) | شرح در گزارش جلسه |
| تست‌های Licensing.Contracts | ۳۴/۳۴ موفق | با جهش‌آزمایی (mutation) تأیید شد |
| `publish-content-guard.test.sh` | موفق | |
| `generate_migrations.py --check` / `verify_migration_model.py` | **ناموفق (exit≠0)** | شکست از پیش‌موجود روی main، نه ناشی از این تغییرات |
| CI روی GitHub / API / Branch protection | اجرا نشد | دسترسی push-level به مخزن داده نشد |

## امنیت، داده و سازگاری
- Schema/Migration تغییر نکرد. ممیزی الگوی اسرار روی تاریخچه و همهٔ شاخه‌ها: موردی یافت نشد (heuristic، نه تضمین).

## ریسک‌ها و کارهای باقی‌مانده
- **شکست Guard مهاجرت (PR #68):** `ef_model.py` فرم `HasOne<Domain.Company>().WithMany()` را نمی‌فهمد. افزودن پارسر آن را یک گام جلو می‌برد، اما `generate_migrations.py` ثابت‌های `CURRENT_MIGRATION_*` را روی `AddCorporateGroups` دارد و دو مهاجرت بعدی (`AddUserDefaultCompany`، `AddCompanyCustomerBoundary`) با EF tools نوشته شده‌اند؛ خروجی مولد برای Designer/Snapshot آن‌ها در ترتیب ایندکس/FK، ایندکس `Companies.CustomerBoundaryId` و هدر Designer با فایل واقعی فرق دارد. رفع کامل نیازمند بازطراحی مولد است؛ تغییر ناقص اعمال نشد.
- G1: لینک `Compile`/`Import` به Vendor و ارجاع ناموجود نادیده گرفته می‌شود (Bypass)؛ G2 فهرست‌ممنوعه است نه فهرست‌مجاز (انحراف از متن D-041).
- نبود CODEOWNERS (G4)؛ ProductCode/Sequence در Evaluator بررسی نمی‌شود؛ اختلاف `AGENTS.md`/`CLAUDE.md` با شماره ADR بعدی.
- تصمیم‌های مالک: OD-01/02/12، دسترسی push برای PR، سیاست runner خودمیزبان روی مخزن عمومی، ادغام PR #76 (توصیه: ادغام نشود؛ رفع PATH تأییدنشده).

## شناسه‌های Git
- Branch: `fix/ci-g1-xunit-using-and-g2-printf` (محلی)
- Commit: ندارد (بدون تأیید صریح commit نشد)
- Pull Request: ندارد
