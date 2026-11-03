# تحویل وظیفه — ISSUE-444-utc-time-contract

- **Issue:** #444 (ممیزی و یکپارچه‌سازی قرارداد زمان)؛ هماهنگی Backend در Issue #500
- **Agent/مسئول:** Claude Code (Frontend، ممیزی، مستندات، تست)؛ Backend مالک ChatGPT
- **تاریخ:** 2026-11-03
- **وضعیت:** Frontend انجام‌شده؛ اصلاح Backend منتظر تأیید (D-067)

## هدف و معیارها
- [x] ممیزی معنای هر فیلد زمانی با شاهد کد (جدول در `docs/TIME_CONTRACT.md`).
- [x] قرارداد واحد Frontend: `lib/time.ts`، دو reader صریح در `api/types.ts` (`readUtcDateTime`، `readServerLocalDateTime`) و بدون استفادهٔ مستقیم از `readIsoDateTime`.
- [x] جلوگیری از تبدیل دوگانه و از اشتباه گرفتن زمان محلی قدیمی با UTC.
- [x] مرز روز با منطقهٔ زمانی کاربر؛ ورودی نامعتبر بدون استثنا.
- [x] تست: UTC، ایران، آلمان، DST بهار/پاییز، مرز نیمه‌شب، رفت‌وبرگشت JSON، خواندن از SQL Server (مشخصه‌ساز).
- [ ] اصلاح سمت Backend — **منتظر تصمیم/اجرای ChatGPT**.

## یافتهٔ اصلی
فیلدهای قدیمی (`InsertDateTime`، `UpdateDateTime`، `LastLoginDateTime`) با `DateTime.Now` نوشته می‌شوند، نه UTC. مبدل سراسری «بدون Z = UTC» آن‌ها را به‌اندازهٔ offset سرور جابه‌جا می‌کرد؛ بنابراین فقط `*Utc` اصلاح شد.

## تغییرات
- `ClientApp/src/lib/time.ts` (+ تست)، `ClientApp/src/api/types.ts` (طبقه‌بندی هر فیلد)، `ClientApp/src/api/timeContract.test.ts`.
- `tests/Template.Tests/Integration/UtcWireFormatSqlServerTests.cs`: تست مشخصه‌ساز (فقط تست؛ بدون تغییر رفتار) که نشان می‌دهد مقدار ذخیره‌شده با `Kind=Unspecified` و بدون `Z` برمی‌گردد. با اصلاح Backend باید به انتظار `Z` به‌روز شود.
- `docs/TIME_CONTRACT.md`، `docs/DECISIONS.md` (D-067)، `docs/INDEX.md`.

## بررسی و آزمون
| فرمان/آزمون | نتیجه | توضیح |
|---|---|---|
| `npm run typecheck` / `npm test` / `build` / `check:offline` | موفق (۴۷+۱ فایل) | تست‌های جدید: `lib/time.test.ts` (۹)، `api/timeContract.test.ts` (۵) |
| `dotnet build` تست‌ها | موفق | |
| `UtcWireFormatSqlServerTests` | محلی skip (SQL Server نبود) | در CI با SQL Server اجرا می‌شود |
| مرورگر واقعی در منطقه‌های مختلف | **اجرا نشد** | |

## ریسک‌ها و کارهای باقی‌مانده
- فیلدهای قدیمی در صورت تفاوت منطقهٔ مرورگر و سرور، ساعت دیواری سرور را نشان می‌دهند تا Backend آن‌ها را به UTC ببرد.
- PR ‏#499 (گزارش امنیتی) پیش از این کار یک `asUtcInstant` محلی اضافه کرده بود؛ باید پس از Merge این PR به `lib/time` هم‌گام شود (تداخل کوچک در `api/types.ts`).

## شناسه‌های Git
- Branch: `feat/444-utc-time-contract`
- Pull Request: در ادامه ثبت می‌شود
