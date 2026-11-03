# تحویل وظیفه — ISSUE-444-role-members-api

- **Issue:** #444 (مورد ۳ از «کارهای باقی‌مانده»: اعضای نقش و انتقال اعضا)
- **Agent/مسئول:** Claude Code
- **تاریخ:** 2026-11-03
- **وضعیت:** انجام‌شده در Backend؛ UI این API در این PR نیست؛ وضعیت `Verified` پس از CI و SQL Server

## هدف و معیارهای پذیرش
- [x] مدیر نصب بتواند اعضای فعال یک گروه دسترسی شرکتی را صفحه‌بندی‌شده و با حداقل فیلد هویتی ببیند.
- [x] مدیر نصب بتواند همهٔ اعضای فعال یک گروه را اتمی به گروه فعال دیگرِ همان شرکت منتقل کند تا گروه خالی و غیرفعال‌شدنی شود.
- [x] هیچ عضویت جدیدی ساخته یا عضویت غیرفعالی فعال نشود (D-065).
- [x] گروه شرکت دیگر، شرکت نامعتبر، غیر مدیر، توکن Anti-forgery نامعتبر، مقصد غیرفعال و گروه سیستمی رد شوند و هیچ عضوی جابه‌جا نشود.
- [x] هر جابه‌جایی Audit شود.

## خلاصه تغییرات
- `GET /api/v1/admin/companies/{companyId}/roles/{roleId}/members?page&pageSize`: فقط عضویت‌های فعال؛ فیلدها: `userId, username, firstName, lastName, isUserActive, memberSinceUtc`. `pageSize` بین ۱ تا ۱۰۰ و `page` بین ۱ تا ۱۰۰٬۰۰۰ محدود می‌شود. بدون ایمیل، تلفن و hash.
- `POST .../roles/{roleId}/members/transfer` با بدنهٔ `{ "targetRoleId": "<guid>" }`: پاسخ `200 { movedCount }`.
  - `404` شرکت/گروه مبدأ/گروه مقصد ناموجود یا متعلق به شرکت دیگر؛ `400` بدنه یا مقصد نامعتبر یا برابر مبدأ یا Anti-forgery نامعتبر؛ `409` مقصد غیرفعال، گروه سیستمی، یا بیش از ۵۰۰ عضو (هیچ عضوی منتقل نمی‌شود)؛ `403` غیر مدیر نصب.
  - یک `SaveChanges` اتمی؛ برای هر کاربر یک `SecurityAuditEvent` از نوع `UserCompanyRoleAssigned` با `bulkTransfer=true` و `sourceRoleWasActive`.

## فایل‌های مهم تغییرکرده
- `Server/Api/CompanyRoleAdministrationEndpoints.cs` — دو endpoint و DTOها.
- `tests/Template.Tests/CompanyRoleAdministrationEndpointTests.cs` — ۱۳ تست جدید (فهرست، صفحه‌بندی، ایزولاسیون شرکت، غیر مدیر، انتقال، تخلیه و غیرفعال‌سازی، مقصد خارجی/غیرفعال/سیستمی، سقف دسته، Anti-forgery، بدنهٔ خالی).
- `tests/Template.Tests/Integration/CompanyRoleMembersSqlServerTests.cs` — دو تست SQL Server (صفحه‌بندی با نام‌های null/متمایز و انتقال ۵۰۰ نفره).

## تصمیم‌های فنی
- تصمیم معماری جدیدی گرفته نشد. فقط مدیر نصب مجاز است، پس Delegation Ceiling لازم نیست؛ پیش از پشتیبانی از Company Manager باید جدا طراحی شود.
- Audit یک رویداد به ازای هر کاربر است، نه یک رویداد با همهٔ شناسه‌ها: ستون `DetailsJson` حداکثر ۴۰۰۰ نویسه است و فهرست حدود ۱۰۰ شناسه آن را پر می‌کرد (یافتهٔ بازبین امنیت؛ راستی‌آزمایی شد).
- انتقال از/به گروه سیستمی (مثلاً `company-admin`) بسته است (fail-closed، هم‌سو با `UpdateRoleAsync`). اگر مالک محصول انتقال گروهی مدیران شرکت را بخواهد، باید صریحاً تأیید و در سند ثبت شود (`NeedsApproval`).

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| `dotnet build Template.sln -c Release` | موفق، ۰ خطا | |
| `dotnet test` فیلتر `CompanyRole` | موفق: ۲۶ تست، ۰ خطا، ۲ skip | دو skip همان تست‌های SQL Server (محلی SQL Server نبود) |
| تست‌های SQL Server جدید | اجرا نشد محلی | Docker در این محیط نبود؛ باید در CI سبز شوند |
| بازبینی مستقل | انجام شد | `code-reviewer` و `security-reviewer`؛ بدون Critical/High؛ یافته‌های Medium/Low بالا اصلاح یا ثبت شدند |

## امنیت، داده و سازگاری
- بدون Migration و بدون تغییر Schema؛ بدون وابستگی جدید؛ بدون اتکا به `TenantId` کلاینت؛ مجوز از دیتابیس در هر درخواست.
- **ریسک باقی‌مانده (Medium، fail-closed):** `CompanyRole` و `UserCompanyMembership` concurrency token ندارند. اگر هم‌زمان با انتقال، مقصد غیرفعال شود، اعضا روی گروه غیرفعال می‌مانند و دسترسی‌شان بسته می‌شود (دسترسی اضافه ایجاد نمی‌شود). همین شرایط در `UpdateRoleAsync` و `AssignMembershipAsync` هم هست. رفع پایدار: ستون RowVersion روی `CompanyRole` با Migration جدا؛ `NeedsApproval`.
- انتقال از گروه غیرفعال، دسترسی اعضای آن را برمی‌گرداند (مثل تخصیص تک‌نفره)؛ `sourceRoleWasActive` در Audit ثبت می‌شود.

## ریسک‌ها و کارهای باقی‌مانده
- UI برای این دو endpoint (فهرست اعضا در جزئیات گروه و دکمهٔ «انتقال اعضا») ساخته نشده.
- Delegation Ceiling، E2E مثبت با SQL Server واقعی، RowVersion، S2، گزارش امنیت (فهرست کامل در `ISSUE-444-admin-ui-completion.md`).

## شناسه‌های Git
- Branch: `feat/444-role-members-api`
- Pull Request: در ادامه ثبت می‌شود
