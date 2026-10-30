# تحویل وظیفه — ISSUE-235-provisioning-e2e-tests

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/235
- **Agent/مسئول:** Claude Code
- **تاریخ:** 2026-10-30
- **وضعیت:** ناقص عمدی (تست‌های SQL Server و رفع باگ همزمانی انجام شده؛ E2E دو-میزبانه انجام نشده؛ رفع مقدار ثابت password-like در fixture ثبت شده و CI روی head جدید در انتظار است)
- **وابستگی:** این شاخه روی `feat/issue-235-ack-signing-provider-contract` ساخته شده است (سازندهٔ `CustomerProvisioningApplyService` در آن PR تغییر کرد). تا ادغام آن PR، PR این شاخه را با base همان شاخه باز کنید.

## هدف و معیارهای پذیرش
- [x] Rollback در شکست در میانهٔ ایجاد شرکت/دوره/نقش/مدیر/عضویت، و اثبات اینکه Retry بعدی گیر نمی‌کند.
- [x] درخواست‌های هم‌زمان تکراری: دقیقاً یک شرکت و یک توکن.
- [x] انقضا، مصرف یک‌باره و ابطال توکن قبلی؛ محدودسازی حدس؛ مصرف هم‌زمان.
- [x] مجوزهای دقیق زیرسیستم‌های خریداری‌شده و جداسازی بین شرکت‌ها.
- [x] Replay، استفادهٔ مجدد متعارض از RequestId/Nonce، بستهٔ دست‌کاری‌شده و نصب بیگانه.
- [ ] E2E خودکار که هر دو میزبان (Vendor و مشتری) را در یک تست بالا بیاورد: اجرا نشد (توضیح پایین).

## یافتهٔ مهم (باگ واقعی، رفع شد)
- تست هم‌زمانی نشان داد ۷ از ۸ درخواست هم‌زمان تکراری apply و ۳ از ۴ درخواست هم‌زمان enrollment با **Deadlock Victim** (SQL Server 1205) خطای مدیریت‌نشده می‌دادند (HTTP 500). رکورد تکراری ساخته نمی‌شد، ولی رفتار idempotent نبود.
- رفع: `sp_getapplock` تراکنشی در ابتدای apply/enrollment/reissue؛ درخواست‌های تکراری صف می‌شوند و مسیر replay موجود را می‌روند. Timeout پانزده‌ثانیه‌ای کد قابل‌تکرار `provisioning_apply_busy` / `enrollment_busy` (409) می‌دهد. `provisioning_apply_conflict` هم به 409 نگاشت شد (قبلاً 400 پیش‌فرض بود: تغییر رفتار API کوچک).

## فایل‌های مهم تغییرکرده
- `Server/Infrastructure/Provisioning/CustomerProvisioningApplyService.cs` — قفل و کدها.
- `Server/Api/ApiEndpoints.cs` — نگاشت کدها.
- `tests/Template.Tests/Integration/CustomerProvisioningTransactionalSqlServerTests.cs` — هفت تست جدید.
- `docs/SECURITY.md` — توضیح کنترل هم‌زمانی.

## تصمیم‌های فنی
- قفل برنامه‌ای سراسری (نه به‌ازای RequestId): provisioning و enrollment کم‌تکرار و اپراتوری‌اند؛ قفل سراسری جلوی رقابت‌های بین‌درخواستی (nonce، نام کاربری) را هم می‌گیرد و ساده‌تر است. SQL خام با دلیل مستند و پارامتری.
- ADR جدید ثبت نشد؛ تغییر پیاده‌سازی داخلی بدون اثر بر قرارداد است.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `dotnet build Template.sln -c Release` | موفق | 0 خطا |
| CustomerProvisioningTransactionalSqlServerTests (SQL Server 2022 واقعی) پیش از رفع | 2 ناموفق از 7 | Deadlock در apply و enrollment هم‌زمان |
| همان تست‌ها پس از رفع، سه بار پشت‌سرهم | 7 از 7 موفق در هر سه اجرا | |
| `dotnet test Template.sln -c Release` (با SQL Server) | موفق | 664 کل، 662 موفق، 2 skipped، 0 ناموفق |
| Playwright E2E / Vendor.sln / کلاینت | اجرا نشد | تغییری در آن‌ها نبود |
| بازبینی مستقل Subagent | اجرا نشد | Subagent فراخوانی نشد؛ بازبینی Diff توسط نویسنده |
| GitHub secret-scan، run `41021431573` روی SHA قبلی | ناموفق | Gitleaks یک مقدار ثابت password-like فقط در fixture تست را گزارش کرد؛ credential عملیاتی نبود، اما از کد حذف و با مقدار تصادفی runtime جایگزین شد. CI روی commit اصلاحی هنوز اجرا/تأیید نشده است |

## چرا E2E دو-میزبانه ساخته نشد
- هر دو پروژه `public partial class Program` در فضای نام سراسری دارند؛ یک پروژه تست که هر دو را ارجاع دهد به alias بیرونی و پیکربندی جداگانهٔ دو پایگاه داده نیاز دارد.
- مسیر Vendor تا بستهٔ امضاشده (دو مدیر با TOTP، تأیید دو نفره، Signer) در `VendorCustomerMutationHttpIntegrationTests` صدها خط fixture دارد.
- جایگزین موجود: هر نیمه با همان قرارداد مشترک تست شده است: بستهٔ صادرشده از Vendor با `CustomerProvisioningPackageValidator` اعتبارسنجی می‌شود (تست Vendor)، و ack تولیدشدهٔ مشتری با `CustomerProvisioningAcknowledgementValidator` (همان اعتبارسنج endpoint Vendor) و bindingهای Vendor تأیید می‌شود (تست SQL Server مشتری). تست یکپارچهٔ واحد هر دو میزبان نیازمند پروژهٔ تست جدا و تصمیم مالک دربارهٔ هزینهٔ آن است.

## امنیت، داده و سازگاری
- بدون Schema/Migration جدید. قفل فقط رفتار هم‌زمانی را تغییر می‌دهد و هیچ مسیر دسترسی تازه‌ای نمی‌سازد.
- هیچ توکن/رمز در Audit وارد نمی‌شود (در تست انقضا بررسی شد).

## ریسک‌ها و کارهای باقی‌مانده
- E2E دو-میزبانه و Playwright از آپلود ack تا وضعیت `Confirmed`.
- قفل سراسری یعنی applyهای همزمان صف می‌شوند؛ در حجم بالا (نامحتمل برای این کار اپراتوری) باید بازنگری شود.

## امنیت و اصلاح Secret Scan
- مقدار ثابت password-like فقط در تست integration وجود داشت؛ هیچ رمز Production یا customer credential در اختیار این تست قرار نگرفته بود.
- برای حذف الگوی قابل‌تشخیص و جلوگیری از هرگونه credential ثابت، رمزهای تست در هر اجرای تست با `RandomNumberGenerator.GetBytes(32)` تولید می‌شوند و در log یا audit ثبت نمی‌شوند.
- نتیجهٔ Gitleaks پس از این اصلاح باید روی SHA جدید دوباره تأیید شود.

## شناسه‌های Git
- Branch: `feat/issue-235-provisioning-e2e-tests`
- Pull Request: پس از ساخت PR ثبت می‌شود.
