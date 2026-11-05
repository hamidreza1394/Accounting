# تحویل وظیفه — ISSUE-444-system-role-assign-guard

- **Issue:** #444 (مدیریت یکپارچه کاربران، ساختار سازمانی و دسترسی‌ها؛ زیرمجموعهٔ D-071)
- **Agent/مسئول:** Backend Wave-1 mission
- **تاریخ:** 2026-11-05
- **وضعیت:** انجام‌شده (در انتظار CI و Merge با اجازهٔ مالک)

## هدف و معیارهای پذیرش
- [x] «تخصیص گروه دسترسی سیستمی برای همه انواع actor ممنوع می‌ماند» (D-071 تصمیم ۵) — اینک در `AssignMembershipAsync` نیز fail-closed اعمال می‌شود.
- [x] هیچ مسیری نتواند نقش `company-admin` را خارج از گردش تأیید عضویت تخصیص دهد.
- [x] تست منفی برای مدیر نصب و برای مدیر شرکتِ دارای کل کاتالوگ اضافه شد؛ هیچ تستی حذف یا تضعیف نشد.

## خلاصه تغییرات
- ممیزی امنیتی نشان داد هر سه مسیر هم‌سایه (`UpdateRoleAsync`، `TransferMembersAsync` و تأیید درخواست عضویت) تخصیص نقش سیستمی را رد می‌کنند، اما مسیر تک‌عضویِ `POST .../roles/{roleId}/members/{userId}` (`AssignMembershipAsync`) این گارد را نداشت:
  - مدیر نصب می‌توانست مستقیماً نقش سیستمی `company-admin` را به عضو فعال بدهد و گردش bootstrap امن (تأیید عضویت + رد نقش سیستمی) را دور بزند.
  - مدیر شرکتی که تصادفاً کل کاتالوگ مجوز را در سقف تفویض خود داشت، از بررسی زیرمجموعه سقف عبور می‌کرد و می‌توانست مدیر شرکت دوم بسازد (fail-open سهوی، نه عمدی).
- اصلاح: رد نقش سیستمی با `409 Conflict` پیش از بررسی سقف و پیش از هر نوشتنی، منطبق با پیام‌های هم‌سایه (`تخصیص گروه سیستمی مجاز نیست.`)؛ بدون تغییر DTO، Route، Migration یا رفتار موفق مسیرهای مجاز.

## فایل‌های مهم تغییرکرده
- `Server/Api/CompanyRoleAdministrationEndpoints.cs` — گارد `role.IsSystemRole → 409` در `AssignMembershipAsync`.
- `tests/Template.Tests/CompanyRoleAdministrationEndpointTests.cs` — تست مدیر نصب (قرمز→سبز).
- `tests/Template.Tests/CompanyRoleDelegationCeilingEndpointTests.cs` — تست مدیر شرکت با سقف کامل (قرمز→سبز).

## تصمیم‌های فنی
- تصمیم معماری جدیدی گرفته نشد؛ اعمال همان تصمیم مصوب D-071 (بند ۵) و توضیح خودِ کد در `TransferMembersAsync` روی آخرین مسیر باقی‌مانده.
- وضعیت HTTP مطابق هم‌سایه‌ها: `409` با بدنهٔ `{ error }` (نه 403)، چون درخواست از نظر مجوز معتبر است اما انتقال وضعیت مجاز نیست.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `dotnet build Template.sln --no-restore -c Release` | موفق | ۰ خطا |
| `dotnet test ... --filter-class Template.Tests.CompanyRoleAdministrationEndpointTests` | موفق | ۲۱ از ۲۱ (پس از افزودن تست؛ پیش از اصلاح ۱ ناموفق) |
| `dotnet test ... --filter-class Template.Tests.CompanyRoleDelegationCeilingEndpointTests` | موفق | ۱۸ از ۱۸ (پس از اصلاح؛ پیش از آن ۱ ناموفق) |
| `dotnet test Template.sln --no-build -c Release` | موفق (با ۲ استثنای محیطیِ از قبل موجود) | ۱۰۸۵ کل، ۱۰۲۱ موفق، ۶۲ Skip، فقط همان ۲ ناموفق پایهٔ `SafeExceptionRealApplicationTests` (نیاز به SQL Server؛ محیط محلی فاقد Docker) |
| تست‌های SQL Server-gated | اجرا نشده (محیط) | بدون SQL Server واقعی؛ در CI با `FARANOU_REQUIRE_SQLSERVER=1` اجرا می‌شوند |

## امنیت، داده و سازگاری
- امنیت: بستن آخرین مسیر fail-open تخصیص `company-admin`؛ سقف تفویض و dual-mode authorization دست‌نخورده.
- داده: بدون Migration، بدون تغییر Snapshot، بدون backfill.
- سازگاری: رفتار رد (409) additive است؛ هیچ مشتری موجودی که نقش سیستمی تخصیص می‌داد، مسیر مجازی نداشته است.

## ریسک‌ها و کارهای باقی‌مانده
- «غیرفعال‌سازی/لغو عضویت به‌صورت مستقل» و «انقضای درخواست Pending» (چرخه D-065) خارج از دامنهٔ این PR و نیازمند تصمیم محصولی است.
- ثبت Audit برای ردهای 403 مطابق تصمیم ISSUE-498 بی‌صدا می‌ماند (تغییر نیازمند تصمیم مالک).

## شناسه‌های Git
- Branch: `fix/access-system-role-assign`
- Commit: به‌روزرسانی پس از push
- Pull Request: پس از ایجاد تکمیل می‌شود
