# تحویل وظیفه — ISSUE-105-vendor-audit-chain

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/105
- **Agent/مسئول:** ChatGPT / Faranou Senior Developer + Architecture/Security/Testing skills
- **تاریخ:** 2026-10-25
- **وضعیت:** کد و مستندات در شاخه اضافه شده‌اند؛ منتظر CI واقعی و بازبینی

## هدف و معیارهای پذیرش
- [x] قرارداد `IVendorAuditWriter` بدون ورودی metadata آزاد اضافه شد.
- [x] Writer با `TimeProvider`، canonical version 1 JSON و SHA-256 رخداد را به `PreviousHash` متصل می‌کند.
- [x] از `sp_getapplock` با مالکیت Transaction و timeout استفاده می‌شود؛ قفل حافظه‌ای یا وابسته به یک process نیست.
- [x] Writer در صورت شکست root/leaf/parent، fork قابل مشاهده، metadata آزاد یا شناسهٔ نامعتبر fail-closed می‌شود.
- [x] Verifier تغییر محتوا، hash غیرمعتبر/تکراری، چند ریشه، والد گمشده، fork و رخدادهای disconnected/cyclic را رد می‌کند.
- [x] تست Integration SQL Server: ۱۶ DbContext نویسندهٔ هم‌زمان، verification و دستکاری عمدی.
- [x] تست واحد: رد metadata آزاد و شناسهٔ شبیه ایمیل.
- [x] اسناد امنیت/تست/وضعیت/Work Log و این handoff به‌روزرسانی شدند.
- [ ] Build/Test واقعی CI روی HEAD این شاخه سبز شود.
- [ ] بازبینی نهایی diff و گزارش تست/CI انجام شود.

## فایل‌های مهم تغییرکرده
- `Vendor/Vendor.Persistence/Auditing/IVendorAuditWriter.cs` — قرارداد append.
- `Vendor/Vendor.Persistence/Auditing/VendorAuditWriter.cs` — قفل تراکنشی SQL Server، اعتبارسنجی ورودی و append.
- `Vendor/Vendor.Persistence/Auditing/VendorAuditChainVerifier.cs` — canonical hash و بررسی زنجیره.
- `tests/Vendor.Host.Tests/VendorAuditChainIntegrationTests.cs` — تست هم‌زمانی و tampering روی DB آزمایشی.
- `tests/Vendor.Host.Tests/VendorAuditChainVerifierTests.cs` — تست رد metadata و شناسهٔ قابل‌شناسایی.
- `Vendor/README.md`, `docs/TESTING.md`, `docs/SECURITY.md`, `docs/DECISIONS.md`, `docs/PROJECT_STATUS.md`, `docs/LICENSING_THREAT_MODEL.md`, `docs/WORK_LOG.md` — مستندات و وضعیت دقیق.

## تصمیم‌های فنی
- برای جلوگیری از fork در چند instance، `sp_getapplock` از نوع Exclusive با `LockOwner=Transaction` استفاده می‌شود. این تصمیم به SQL Server وابسته است؛ روی provider حافظه‌ای یا دیتابیس دیگر، writer اجرا نمی‌شود.
- JSON canonical version 1 بر اساس record ثابت و ترتیب مشخص فیلدها تولید می‌شود؛ زمان UTC با فرمت invariant `O`، شناسه‌ها lowercase و hash به hex lowercase ذخیره می‌شود.
- `SubjectId` فقط GUID opaque و `CorrelationId` فقط GUID یا W3C trace-id است. `MetadataJson` آزاد پذیرفته/تولید نمی‌شود تا PII/credentials به‌سادگی وارد audit نشود.
- Writer هنگام append leaf/current root و parent را کنترل می‌کند؛ اجرای کامل `VendorAuditChainVerifier` یک API جداست. Hash-chain به‌تنهایی محافظ در برابر DBA نیست.
- عمداً `Vendor.Host/Program.cs`, `VendorDbContext`, migration/snapshot و workflow دست‌نخورده مانده‌اند تا با PR #104 (Passkey/MFA) تداخل نداشته باشد.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `dotnet restore Vendor.sln` | اجرا نشده محلی | CI مستقل Vendor باید اجرا کند |
| `dotnet build Vendor.sln --no-restore --configuration Release` | اجرا نشده محلی | SDK/محیط اجرای محلی در دسترس نبود |
| `dotnet test Vendor.sln --configuration Release` | اجرا نشده محلی | CI باید SQL Server واقعی فراهم کند |
| `VendorAuditChainIntegrationTests` | در انتظار CI | ۱۶ writer، verification و tampering روی دیتابیس موقت |
| `VendorAuditChainVerifierTests` | در انتظار CI | بدون دیتابیس؛ no-free-form-metadata و anti-PII identifiers |
| `git diff --check` | اجرا نشده محلی | تغییرات از GitHub Contents API انجام شده‌اند |

## امنیت، داده و سازگاری
- هیچ connection string، secret یا کلید خصوصی اضافه نشده است.
- هیچ endpoint یا wiring عمومی برای نوشتن رخداد ایجاد نشده؛ wiring به Host بعد از تکمیل MFA/bootstrap در PR مستقل خواهد بود.
- هیچ مدل/Schema/Migration مشتری، `Template.sln` یا دیتابیس مشتری تغییر نکرده است.
- گزارش/نتیجهٔ verifier فقط کد شکست غیرحساس برمی‌گرداند و محتوا/metadata را افشا نمی‌کند.

## ریسک‌ها و کارهای باقی‌مانده
- Writer در هر append کل زنجیره را برای کارایی دوباره verify نمی‌کند؛ leaf/root/parent را کنترل می‌کند. اجرای full verifier باید در عملیات حساس یا پایش دوره‌ای فراخوانی شود.
- Hash-chain در همان DB مانع بازنویسی کامل توسط DBA نمی‌شود. SQL application account با حداقل مجوز/بدون UPDATE و DELETE، checkpoint/export به مقصد مستقل، alerting و recovery runbook برای Production لازم‌اند.
- جست‌وجوی leaf از subquery روی کل EventHash/PreviousHash استفاده می‌کند و در جدول بسیار بزرگ می‌تواند کند شود؛ ایندکس‌های مناسب باید در Issue جداگانه پس از هماهنگی با migration در حال توسعهٔ Passkey طراحی/آزمون شوند.
- `Vendor.Host` در این PR به سرویس وصل نمی‌شود؛ هیچ مسیر مدیریتی فعالی ادعا نمی‌شود.
- CI و بررسی دقیق PR #104 برای Passkey/MFA مستقل باقی می‌مانند؛ run `40222148385` خطای `IdentityPasskeyData` بدون primary key گزارش کرده و باید قبل از فعال‌سازی MFA اصلاح شود.

## شناسه‌های Git
- Branch: `security/issue-105-vendor-audit-chain`
- Commit: پس از آخرین تغییر تعیین شود.
- Pull Request: پس از ایجاد ثبت شود.