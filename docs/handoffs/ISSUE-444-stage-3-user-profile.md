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

## ۳. Gap Analysis واقعی پس از PR #596 (ممیزی کد `main` در `f0e89591` — 2026-11-09)

| قابلیت | Backend | تست Backend | تست SQL Server | UI | UI→API | مجوز سرور | Audit | نتیجه |
|---|---|---|---|---|---|---|---|---|
| 3-C اتصال HR↔User | `PUT/DELETE /org/employees/{id}/user` + فیلتر `?userId=` موجود | ✅ | ✅ (CI) | فقط-خواندنی | نمایش بدون عملیات | ✅ (`hr.employees.user-link.manage`؛ بایپس مدیر نصب در `AuthorizeHrScopeAsync`) | ✅ `HrEmployeeUserLinked/Unlinked` | **شکاف UI: اتصال/قطع اتصال** (PR 3-C2) |
| 3-D عضویت‌ها | گردش کامل درخواست/تأیید/رد + صف + تاریخچه | ✅ | ✅ (CI) | تب ۳ فقط عضویت‌های فعال/غیرفعال را نشان می‌دهد؛ صف تأیید و «درخواست‌های من» صفحات مستقل موجودند | ⚠️ وضعیت درخواست‌های «همین کاربر» (Pending/Rejected+دلیل/تأییدکننده) در پرونده نیست | ✅ | ✅ | **شکاف: وضعیت درخواست‌های کاربر در پرونده + فیلتر `userId` روی تاریخچه** (PR 3-D2) |
| 3-E مجوزهای مؤثر | `effective-permissions` با منبع هر مجوز | ✅ | ✅ (CI) | per-company + منبع + جستجو + حالت «مجاز نیست» | ✅ | ✅ (`roles.view` دوحالته) | n/a (خواندنی) | **کامل**؛ گروه‌بندی زیرسیستمی نیازمند نگاشت مجوز→زیرسیستم است که در API وجود ندارد — به‌صورت Proposed در سند ثبت می‌شود، نگاشت موازی ساخته نمی‌شود |
| 3-F نشست‌ها | list/revoke/revoke-all با re-auth + reasonCode | ✅ | ✅ (CI) | تب ۲ فقط-خواندنی بود | ❌ | ✅ (فقط مدیر نصب؛ self-target 400) | ✅ تراکنشی fail-closed | **شکاف UI: ابطال با step-up** (PR 3-F2) |
| 3-G زیرسیستم/لایسنس | کاتالوگ واقعی D-075 | ✅ | ✅ (CI) | کاتالوگ + یادداشت صادقانه | ✅ | ✅ | n/a | **کامل** — هیچ وضعیت فرضی نمایش داده نمی‌شود |
| 3-H1 E2E | — | — | — | spec پرونده کاربر وجود ندارد | — | — | — | **شکاف** (PR 3-H1) |
| 3-H2 axe/Screenshot | — | — | — | صفحات پرونده در سناریوی axe نیستند؛ Screenshot واقعی نیست | — | — | — | **شکاف** (PR 3-H1) |

## ۴. PR 3-F2 — ابطال نشست‌های کاربر دیگر از پرونده (UI مرحله‌به‌مرحلهٔ D-073 §1)

- Backend از قبل کامل بود (§۸.۵ قرارداد API) و دوباره ساخته نشد؛ این PR فقط UI و تست UI است.
- `ConfirmDialog` props اختیاری `children` گرفت (سازگار با عقب) تا فرم step-up داخل همان دیالوگ دسترس‌پذیر موجود رندر شود (focus trap/Escape/بازگشت focus بدون تکرار کد).
- کامپوننت جدید `AdminRevokeSessionDialog`: رمز عبور مدیر (`current-password`، فقط در بدنهٔ درخواست، پاک‌سازی در هر بازشدن) + انتخاب دلیل از کاتالوگ سرور (۴ کد D-073 با برچسب فارسی)؛ خطای اعتبارسنجی و خطای واقعی سرور (403 re-auth، 400 دلیل نامعتبر، 429) داخل دیالوگ با `Alert` (role=alert) نمایش داده می‌شود و مقادیر در خطا حفظ می‌شوند؛ گارد دوارسالی؛ هیچ Token/Secret نمایش داده نمی‌شود.
- تب ۲ پرونده (`ProfileSecurityTab`): ستون «عملیات» با دکمهٔ ابطال هر نشست + دکمهٔ «ابطال همهٔ نشست‌های فعال (n)» + پیام موفقیت در ناحیهٔ zندهٔ `aria-live`؛ پس از هر ابطال موفق، cache تب حذف و فهرست از سرور بازخوانی می‌شود (هیچ ردیف «به‌ظاهر فعال» پس از mutation نمایش داده نمی‌شود).
- کلاینت: `revokeAdminUserSession`، `revokeAllAdminUserSessions` و parser `parseAdminRevocationOutcome` (`revokedCount` allowlist).
- تست‌ها: ۳ سناریوی جدید در `ProfileTabs.test.tsx` — (۱) ابطال تک‌نشست فقط با فرم کامل + assert بدنهٔ POST + بازخوانی فهرست + عدم افشای رمز در متن صفحه، (۲) 403 re-auth: دیالوگ باز می‌ماند، مقادیر حفظ و پیام واقعی سرور نمایش داده می‌شود، (۳) ابطال همه با شمارش سرور. vitest 920/920؛ typecheck؛ build؛ check:offline سبز.
- مستندات: `ADMIN_UI.md` (ردیف صفحهٔ پرونده، جدول کامپوننت‌ها و وضعیت نشست‌ها)، `WORK_LOG`، همین handoff. Migration: هیچ.

## ۵. PR 3-C2 — اتصال/قطع اتصال پرونده پرسنلی در پرونده کاربر (UI)

- Backend اتصال HR↔User از قبل کامل بود (`PUT/DELETE /org/employees/{id}/user` با مجوز `hr.employees.user-link.manage`، Audit `HrEmployeeUserLinked/Unlinked`، قواعد یک‌به‌یک و کارمند خاتمه‌همکاری سمت سرور) و دوباره ساخته نشد؛ این PR فقط UI + تست UI است. **روی PR 3-F2 سوار است (stacked)** چون `ConfirmDialog` props `children` را از همان PR می‌گیرد.
- کامپوننت جدید `HrLinkActions`: (۱) دیالوگ اتصال با جست‌وجوی سرور-سمت کارمندان (`search`، صفحه‌بندی ۲۵تایی) که **فقط کارمندان بدون حساب متصل** را پیشنهاد می‌دهد (`hasLinkedUser=false`؛ اتصال دوبارهٔ کارمند متصل از پرونده کاربر هرگز انجام نمی‌شود و جابه‌جایی لنگر به صفحات مدیریت HR واگذار است)، (۲) دیالوگ قطع اتصال با تأیید (tone danger).
- تب ۱ پرونده: برای هر عضویت فعال، اگر رکورد visible است دکمهٔ «اتصال پرونده پرسنلی» (وقتی متصل نیست) یا «قطع اتصال» (وقتی متصل است)؛ شرکت‌های 403/404 هیچ اکشنی ندارند (fail-closed). پس از هر موفقیت، cache تب حذف و بخش از سرور بازخوانی می‌شود؛ پیام موفقیت در ناحیهٔ zنده.
- خطاها داخل دیالوگ با پیام واقعی سرور (403 مجوز، 409 قواعد یک‌به‌یک/خاتمه‌همکاری، 404) و حفظ مقادیر؛ گارد دوارسالی؛ هیچ داده‌ای در storage ذخیره نمی‌شود.
- تست‌ها: ۴ سناریوی جدید (اتصال کامل با فیلتر «فقط بدون حساب» + assert بدنهٔ PUT + بازخوانی، قطع اتصال با تأیید + DELETE، حفظ دیالوگ با خطای 409 واقعی، نبود هیچ اکشنی برای شرکت غیرقابل‌مشاهده). vitest 924/924؛ typecheck؛ build؛ check:offline سبز.
- مستندات: `ADMIN_UI.md`، `WORK_LOG`، همین handoff. Migration: هیچ.

## ۶. PR 3-D2 — وضعیت درخواست‌های عضویت در پرونده کاربر (Backend خواندنی + UI)

- Gap: گردش درخواست/تأیید/رد و صف مقصد و «درخواست‌های من» از قبل کامل و تست‌شده بودند؛ اما پرونده کاربر هیچ نمایه‌ای از وضعیت درخواست‌های «همین کاربر» (Pending/تأیید/رد + تصمیم‌گیرنده + دلیل رد) نداشت و هیچ API کاربر-محوری برای آن وجود نداشت (تاریخچهٔ موجود company-scoped است و فیلتر درخواست‌کننده ندارد).
- **Backend (کوچک و فقط-خواندنی):** `GET /api/v1/admin/users/{userId}/membership-requests` — فقط مدیر نصب؛ projects همان جدول گردش D-065 (بدون وضعیت/گردش موازی، بدون Mutation/Audit)؛ فیلتر `status` با همان نرمال‌ساز allowlist endpointهای مقصد (`TryNormalizeStatus` به internal تبدیل شد تا تعریف یکی بماند)؛ صفحه‌بندی clamp؛ نزولی `RequestedAtUtc`؛ `no-store`؛ ۴۰۴ ناموجود/Empty. **Migration: هیچ.**
- **UI:** تب ۳ پرونده علاوه بر جدول عضویت‌ها، بخش «درخواست‌های عضویت بین‌شرکتی» دارد: وضعیت فارسی (در انتظار تأیید/تأیید شده/رد شده)، زمان درخواست/تصمیم، تصمیم‌گیرنده، نقش تخصیص‌یافته، دلیل رد؛ فیلتر وضعیت + صفحه‌بندی سرور-سمت؛ پیام شفاف «تصمیم فقط در شرکت مقصد» + لینک به صف تأیید موجود (`/admin/membership-requests`) — گردش تأیید دومی ساخته نشد.
- رفتار تب: fetch تنبل با کلید cache جداگانه per status/page؛ تب غیرفعال هیچ درخواستی نمی‌زند؛ حالت‌های Loading/Empty/Error.
- تست‌ها: Backend ۴ تست جدید (مجوز 403، فقط درخواست‌های کاربر هدف + جزئیات تصمیم + UTC، فیلتر allowlist + 400، 404) + نگهبان rate-limit route؛ Frontend ۳ سناریو (lazy + برچسب‌ها + دلیل رد + لینک صف، فیلتر با درخواست جدید فقط برای فیلتر، حالت خالی). vitest 927/927؛ build/typecheck سبز؛ Backend: build Release پاک، ۲۵ تست هدفمند سبز. SQL Server/E2E به CI.
- مستندات: قرارداد API §۲.۶، ADMIN_UI، WORK_LOG، همین handoff.

## ۷. PR 3-H1 — E2E واقعی پرونده کاربر + axe + اسکرین‌شات‌های واقعی

- سناریوی جدید `tests/e2e/specs/user-profile.spec.mjs` روی سرور Publish شده و SQL Server واقعی (job `e2e` در CI، `FARANOU_E2E_SQL` + سرور Production) — بدون هیچ Mock:
  ۱. باز کردن پرونده کاربر توسط مدیر نصب؛ ۲. مشاهده مشخصات فردی؛ ۳. اتصال و قطع اتصال Employee از طریق endpointهای واقعی HR (جست‌وجو → انتخاب → تأیید → بازخوانی سرور)؛ ۴. ثبت درخواست عضویت توسط کاربر هدف از مرورگر واقعی خودش؛ ۵. مشاهدهٔ Pending در پرونده؛ ۶. تأیید توسط مقصد (مدیر نصب از مسیر رسمی) و مشاهدهٔ «تأیید شده» با نقش؛ ۷. رد درخواست دوم با دلیل و مشاهدهٔ آن؛ ۸. مشاهدهٔ نقش در جدول عضویت‌ها؛ ۹. مجوزهای مؤثر با منبع (users.list ← عضو مقصد)؛ ۱۰. نشست‌های فعال؛ ۱۱. ابطال نشست با step-up واقعی D-073 (رمز مدیر + دلیل) و اثبات واقعی ابطال: فراخوانی بعدی همان مرورگر هدف 401 می‌گیرد؛ ۱۲. مشاهدهٔ Audit (رویداد درخواست عضویت کاربر هدف) و سوابق ورود (ورود موفق).
- axe (WCAG 2.x A/AA) روی تب‌های باز‌شده اجرا می‌شود (شامل کنتراست)؛ هر نقض شکست می‌دهد.
- اسکرین‌شات واقعی مرورگر از هر تب به `artifacts/ui-preview/stage-3-profile/` نوشته و به‌عنوان artifact CI با retention ۳۰ روز آپلود می‌شود؛ هنگام باز بودن دیالوگ step-up هرگز capture گرفته نمی‌شود (هیچ اعتباری در تصویر/trace نمی‌ماند؛ `trace: 'off'` مطابق الگوی role-admin).
- فیکسچر ایزوله `createMembershipFixture` با پاک‌سازی کامل در `finally`؛ حساب‌های اشتراکی seed تغییر نمی‌کنند.
- اجرای محلی ممکن نیست (Docker/SQL Server و publish در این محیط موجود نیست) — به CI واگذار می‌شود و نتیجهٔ واقعی در گزارش نهایی ثبت خواهد شد. این محدودیت صادقانه اعلام می‌شود.
