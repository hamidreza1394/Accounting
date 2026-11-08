# Handoff مرحله سوم — پرونده کاربر، اتصال HR، عضویت‌ها، دسترسی مؤثر، امنیت حساب و لایسنس

- **Issue:** #444 (مرحله سوم)
- **مبنای کد:** `main` پس از Merge PR #595 (SHA `a213059dbe21ff1268ec0094aabd5cc750b36142`)، CI کامل سبز در 2026-11-08.
- **مجوز اجرایی:** مالک محصول، اجرای مرحله سوم و Merge PRهای همین مأموریت را پس از سبز بودن همهٔ کنترل‌های الزامی GitHub مجاز کرده است (بدون دور زدن Branch Protection/Review/CI).
- **این فایل append-only است؛** هر PR بخش خود را به انتهای آن اضافه می‌کند.

## ۰. مرحله صفر — بستن موارد باقی‌ماندهٔ مرحله دوم (ممیزی، بدون دوباره‌کاری)

شواهد از CI اجرای `42495890447` روی `main` (SHA `a213059d`؛ همهٔ ۷ job سبز) و اجرای محلی 2026-11-09:

| مورد | وضعیت | شاهد |
|---|---|---|
| 0-A تست واقعی SQL Server | Verified (در CI) | job `build-and-test` با SQL Server 2022 و `FARANOU_REQUIRE_SQLSERVER=1` سبز؛ تست‌های `*SqlServerTests` (اعضا/انتقال نقش، درخواست عضویت، دایرکتوری، مجوزهای مؤثر، نشست مدیر، HR) همان‌جا اجرا می‌شوند. محلی بدون Docker: ۶۴ تست SQL Skip و دو شکست `SafeExceptionRealApplicationTests` صرفاً به‌دلیل نبود provider رابطه‌ای برای راه‌اندازی واقعی `Program.cs` — محدودیت زیرساختی ثبت می‌شود، نه موفقیت ادعایی |
| 0-B دسترس‌پذیری | Verified (در CI) | job `e2e` با `@axe-core/playwright` (WCAG 2.x A/AA شامل contrast) روی صفحات مدیریت + تست‌های صفحه‌کلید؛ محلی: typecheck پاک و vitest ۹۰۸/۹۰۸ سبز |
| 0-C مدیر شرکتی و D-071 | Verified | `CompanyRoleDelegationCeilingEndpointTests` (تخلف سقف + Audit رد)، `CompanyMembershipRequestEndpointTests` (تأیید مقصد، Self-Approval ممنوع، Pending هیچ دسترسی نمی‌سازد — `AssignMembership_DoesNotCreateANewMembershipBeforeDestinationApproval`)، تست‌های IDOR (`RejectsRoleOfAnotherCompany...`) |
| 0-D هم‌زمانی تغییر Role | Verified (در CI) | توکن‌های RowVersion + `RoleAndMembershipWritersReturnConflictWhenAnotherWriterWinsTheRowVersion`، `MigrationPreservesExistingMembershipsAndConcurrentApprovalsHaveOneWinner` و `ApprovalReturnsConflictWhenDelegationPermissionsChangeAfterTheyWereRead` روی SQL Server واقعی |

نتیجه: هیچ نقص مسدودکننده‌ای برای شروع مرحله سوم یافت نشد؛ دوباره‌کاری انجام نشد.

## ۱. PR 3-A — ممیزی و تکمیل API پرونده کاربر

- **شاخه:** `agent/444-stage3-user-profile-apis` (از `main` در `a213059d`)
- **Gap Analysis:** موجود: اطلاعات پایه/عضویت‌ها (`GET /admin/users/{userId}`)، نشست‌های فعال مدیر (D-073)، مجوزهای مؤثر با منبع، سوابق تغییرات مدیریتی (`audit-events?userId=`)، تغییر رمز و self-service. مفقود: (۱) سوابق ورود کاربر هدف برای مدیر نصب، (۲) یافتن پرونده پرسنلی متصل به یک کاربر.
- **تغییرات:**
  - `GET /api/v1/admin/users/{userId}/login-history` — فقط مدیر نصب (بازارزیابی DB)؛ همان قرارداد `/me/login-history` (۵ رویداد احراز هویت، `take` clamp، نزولی، `no-store`، rate limit حساس)؛ ۴۰۴ ناموجود/Empty؛ بدون افشای رویدادهای غیراحراز هویت.
  - `GET /api/v1/companies/{companyId}/org/employees?userId=` — فیلتر اختیاری اتصال کارمند↔عضویت؛ fail-closed (ناشناخته = خالی، Empty = 400).
  - نگهبان معماری: route جدید در `ApiSensitiveReadRateLimitTests`.
- **تست‌ها:** ۳ تست login-history + ۱ تست فیلتر `userId`؛ اجرای محلی: build Release پاک، ۶/۶ + ۳۱/۳۱ + ۱/۱ سبز. SQL Server/E2E به CI واگذار شد (Docker محلی موجود نیست).
- **مستندات:** API §۲.۵، HR contract §۱.۱، WORK_LOG.
- **Migration:** هیچ.

## ۲. PR 3-B — صفحه حرفه‌ای پرونده کاربر (UI) — همراه با PR 3-A در #596

- UI مستقیماً به endpointهای همین شاخه وابسته است؛ به همین دلیل 3-A و 3-B در یک PR ادغام‌شده و توضیحات PR هر دو بخش را ثبت می‌کند.
- `UserDetailPage` به Workspace هفت‌تبی تبدیل شد (سربرگ با آواتار حروف اول، نشان وضعیت حساب/عضویت، نشان مدیر سیستم در صورت نقش Admin قدیمی)؛ تب‌ها: مشخصات فردی و پرسنلی، حساب و امنیت (نشست‌های فعال allowlist D-069)، شرکت‌ها و عضویت‌ها، زیرسیستم‌ها و لایسنس (کاتالوگ واقعی D-075 + یادداشت صادقانه «اتصال لایسنس پیاده‌سازی نشده»)، مجوزهای مؤثر با منبع + جستجو، سوابق ورود/خروج، سوابق تغییرات مدیریتی.
- رفتار تب‌ها: lazy load فقط هنگام فعال‌شدن، cache per-user، بدون درخواست تکراری در بازگشت، بدون فراخوانی تب غیرفعال؛ حالت‌های Loading/Empty/Error/Retry؛ بخش بدون مجوز = پیام صادقانه (403/404)؛ هیچ رمز/hash/token در UI نیست.
- کلاینت: `getAdminUserLoginHistory`، `getAdminUserSessions`، `getAccessCatalogSubsystems` + parserهای سخت‌گیر؛ `HrEmployeeQuery.userId`.
- تست‌ها: بازنویسی `UserDetailPage.test.tsx` + `ProfileTabs.test.tsx` جدید؛ vitest ۹۱۶/۹۱۶، typecheck، build و check:offline سبز.
- محدودیت صادقانه: Screenshot واقعی مرورگر در این محیط ممکن نیست (بدون Docker/SQL Server راه‌اندازی سرور ممکن نیست)؛ افزودن صفحات پرونده به سناریوی axe/Playwright CI در PR 3-H ادامه می‌یابد.
