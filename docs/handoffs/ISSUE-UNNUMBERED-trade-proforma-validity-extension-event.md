# تحویل وظیفه — ISSUE-UNNUMBERED-trade-proforma-validity-extension-event

- **Issue:** UNNUMBERED (ادامهٔ #397، مرحلهٔ C-1 طراحی Proforma؛ فقط Trade)
- **Agent/مسئول:** Claude (Lead) + بازبین مستقل `code-reviewer`
- **تاریخ:** 2026-11-24
- **وضعیت:** انجام‌شده (کد و تست)؛ CI و Review انسانی در انتظار مالک

## هدف و معیارهای پذیرش
- [x] تمدید صرف تاریخ اعتبار Proforma به‌صورت رویداد مستقل قابل‌ثبت (D-116 بند ۲): تاریخ قبلی، تاریخ جدید، دلیل، کاربر، زمان ثبت.
- [x] Scope صریح: `TenantId` و `CompanyId` جدا و مستقل؛ برابری Legacy فرض نمی‌شود (D-087، D-116).
- [x] Domain/Application خالص؛ بدون EF، DbContext، Schema، Migration، API، UI، Authorization یا تغییر `TradeAccessContext`.
- [x] موارد تصمیم‌نشده وارد نشدند: A-1 (محل نگهداری تاریخ اعتبار)، A-2 (کاهش تاریخ)، A-3 (فهرست شرایط تجاری)، P-4، P-5، فرمول‌های مالی.

## بررسی وضعیت قبلی (شکاف واقعی)
- #756 (D-116)، #764 (طراحی)، #772 (`ProformaChangeClassifier`)، #705 (`PaymentPlanCalculator`) ادغام شده‌اند. `ProformaChangeClassifier` قاعدهٔ «تغییر شرایط تجاری ⇒ Revision؛ تمدید صرف ⇒ بدون Revision» را پوشش می‌داد و **دست نخورد**.
- **شکاف:** هیچ نوعی برای «رویداد مستقل تمدید» با فیلدهای مصوب و Scope صریح Company وجود نداشت. بقیهٔ Aggregate (`Proforma`، `ProformaRevision`، `ProformaLine`) به تصمیم‌های باز A-1، A-3، N-9، P-4، P-5 وابسته است و عمداً ساخته نشد.

## خلاصه تغییرات
- `ProformaValidityExtendedEvent.TryCreate(...)`: تنها راه ساخت؛ رکورد بدون Setter و بدون سازندهٔ عمومی؛ `Reason` اختیاری و Trim‌شده.
- `ProformaValidityExtensionIssue`: `TenantMissing`، `CompanyMissing`، `ProformaMissing`، `ActorMissing`، `NotAnExtension`، `RecordedAtNotUtc`.
- «تاریخ جدید ≤ قبلی» فقط به‌دلیل «تمدید نبودن» رد می‌شود؛ کاهش تاریخ (A-2) ثبت یا مجاز اعلام نمی‌شود.
- `RecordedAtUtc` باید `DateTimeKind.Utc` باشد (قاعدهٔ مخزن: تاریخ DB میلادی/UTC).

## فایل‌های مهم تغییرکرده
- `Modules/Trade/Trade.Application/Proformas/ProformaValidityExtendedEvent.cs` — نوع جدید (کنار `ProformaChangeClassifier`).
- `tests/Template.Tests/TradeProformaValidityExtensionTests.cs` — ۱۶ تست رفتاری.

## تصمیم‌های فنی
- تصمیم معماری جدیدی گرفته نشد و هیچ ADR/تصمیم `Proposed` به `Accepted` نرفت.
- جای نوع: `Trade.Application/Proformas` طبق الگوی `ProformaChangeClassifier` و `PaymentPlan` (بدون وابستگی به Infrastructure).
- `Reason` بدون سقف طول: سقف و نویسه‌های مجاز تصمیم Persistence/Validation آینده است و اختراع نشد.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| `dotnet restore tests/Template.Tests/Template.Tests.csproj` | موفق | |
| `dotnet build tests/Template.Tests -c Release --no-restore` | موفق، ۰ خطا | هشدارهای قدیمی CA1707/CA1859 بدون تغییر |
| TDD: Build پیش از نوع | ناموفق (CS0246 مورد انتظار) | قرمز تأیید شد |
| `./Template.Tests -class "*TradeProformaValidityExtensionTests"` | ۱۶ اجرا، ۱۶ موفق، ۰ ناموفق، ۰ Skip | |
| بازبینی مستقل `code-reviewer` | بدون یافتهٔ Critical/High؛ ۲ مورد Medium (تست ترکیبی تکراری با تست‌های Classifier و Fixture تک‌مصرف) پذیرفته و اصلاح شد (حذف هر دو) و Build/تست دوباره اجرا شد | بازبین فقط‌خواندنی؛ خودش Build/Test اجرا نکرد |
| Mutation (`<=` → `<`) | تست `TheSameDate_IsNotAnExtension` شکست خورد | سپس برگردانده شد |
| `-class "*Trade*" -class "*Architecture*"` | ۴۸۳ تست، ۴۵۷ موفق، ۰ ناموفق، **۲۶ Skip** | Skipها تست‌های SQL Server (`FARANOU_TEST_SQLSERVER` تنظیم نبود)؛ **NotRun**، نه موفق |
| تست SQL Server/E2E/Vitest | **اجرا نشد** | تغییر Persistence ندارد؛ طبق دستور مأموریت الزام صوری نشد |

## امنیت، داده و سازگاری
- بدون I/O؛ بدون Schema/Migration/Backfill؛ بدون API/UI؛ بدون تغییر Authorization/`TradeAccessContext`/Entitlement (`DenyAllTradeModuleEntitlement` دست‌نخورده).
- این نوع هنوز به هیچ مسیر زمان اجرا وصل نیست؛ هیچ اثر Production ندارد.
- شناسه‌ها باید از context معتبر سرور بیایند؛ نوع فقط مقدار خالی را رد می‌کند و اعتبار Scope را اثبات نمی‌کند.

## ریسک‌ها و کارهای باقی‌مانده
- محل نگهداری تاریخ اعتبار (A-1) و رفتار کاهش (A-2) باید تصمیم مالک شود؛ شکل نهایی رویداد ممکن است پس از S3-0 جزئی تغییر کند (گزینهٔ ۱ طراحی Proforma).
- `Reason` سقف طول ندارد؛ هر لایهٔ ذخیره‌سازی آینده باید طول را در محدودهٔ Schema تعیین کند (بازبین: Low).
- در صورت تصمیم A-2 ممکن است رویداد جداگانه‌ای (مثلاً کوتاه‌سازی) لازم شود؛ این PR آن را تصمیم نمی‌گیرد و فقط «تمدید‌نبودن» را رد می‌کند.
- `Aggregate` کامل Proforma/Revision و Persistence تا بسته‌شدن گیت‌ها و تصمیم‌های A-1، A-3، N-9، P-4، P-5 مسدود است.
- PR #788 و #789 هنوز ادغام نشده‌اند؛ `WORK_LOG.md` و `PROJECT_STATUS.md` هر دو در انتها افزوده می‌شوند و ممکن است هنگام ادغام تعارض متنی ساده (دو افزودن در انتهای فایل) ایجاد کنند.

## شناسه‌های Git
- Branch: `feat/trade-proforma-validity-extension-event`
- Commit / Pull Request: در گزارش نهایی ثبت می‌شود.
