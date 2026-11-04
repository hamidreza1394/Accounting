# تحویل وظیفه — ISSUE-444-access-backend-db-audit

- **Issue مادر:** [#444](https://github.com/hamidreza1394/Accounting/issues/444)
- **نوع:** ممیزی مستنداتی Backend/Database مدیریت دسترسی‌ها (مأموریت ۱). بدون تغییر کد، Migration، API یا UI.
- **تاریخ ممیزی:** 2026-11-04
- **Commit مبنای ممیزی:** `main` @ `2a62811acdb71bda77288356743be45356685954` (CI سبز در همین SHA)

## ۱. وضعیت واقعی #479

- PR [#479](https://github.com/hamidreza1394/Accounting/pull/479) — `feat(access): implement destination-approved company membership requests` — **Merge شده** به `main` در 2026-11-03T12:43:21Z (شاخه `agent/460-membership-approval`، 19 فایل، +4857/−1314).
- وضعیت در متن Issue #444 («در حال بررسی») قدیمی است؛ منبع حقیقت طبق D-030 تاریخچه PR ادغام‌شده و کد `main` است.
- بدنه PR #479 عنوان می‌کند «کاربر بدون عضویت هم می‌تواند درخواست دهد»؛ کد فعلی (پس از ISSUE-510) مرز CustomerBoundary را fail-closed اعمال می‌کند و کاربر بدون هیچ عضویت مرزی، مسیر درخواست ندارد. handoff [ISSUE-510](https://github.com/hamidreza1394/Accounting/blob/main/docs/handoffs/ISSUE-510-membership-workflow-api.md) منطق فعلی را مستند کرده؛ توضیح بدنه PR قدیمی است.

## ۲. Entity و Configurationهای بررسی‌شده

- `Domain/CompanyMembershipRequest.cs` — جدول مستقل Pending/Approved/Rejected؛ فقط `DestinationTenantId` به `Company` متصل است؛ `UserCompanyMembership` تنها منبع عضویت عملیاتی باقی مانده (مطابق D-065).
- `Infrastructure/Persistence/Configurations/CompanyMembershipRequestConfiguration.cs`:
  - `RowVersion` با `IsRowVersion()` (SQL Server rowversion).
  - Unique Index فیلترشده `(UserId, DestinationTenantId)` با `Filter = "[Status] = 'Pending'"` — یک درخواست Pending برای هر جفت کاربر/مقصد.
  - FK مقصد با `DeleteBehavior.Restrict`.
  - FK نقش تأییدشده مرکب `(ApprovedRoleId, DestinationTenantId)` به `CompanyRoles(Id, TenantId)` با `HasPrincipalKey` — نقش از شرکت دیگر قابل ارجاع نیست.
- ارتباط با `CompanyRole`، `UserCompanyMembership`، `CustomerBoundaryId` و `SecurityAuditEvents` بررسی و هم‌سو با D-064/D-065 تأیید شد.

## ۳. Migration و Snapshot

- Migration `20261103111108_AddCompanyMembershipRequests`:
  - Check Constraintهای `CK_CompanyMembershipRequests_Status` و `CK_CompanyMembershipRequests_DecisionState` (شامل `DecidedByUserId <> UserId` در حالت Approved).
  - Seed idempotent مجوز `users.membership.approve` بدون grant به هیچ نقشی.
  - `Down`: گارد THROW در صورت وجود تاریخچه درخواست‌ها؛ حذف seed فقط وقتی هیچ `CompanyRolePermission` ارجاع ندارد.
- Snapshot با مدل جاری هم‌خوان است؛ گارد `dotnet ef migrations has-pending-model-changes` در CI روی `main` سبز است.
- تست Integration SQL Server سناریوهای additive upgrade، حفظ عضویت‌های موجود approver، single-winner concurrent approval و down/up (پس از حذف ردیف آزمون) را پوشش می‌دهد.
- هیچ Migration تکراری یا ناقصی یافت نشد؛ هیچ Migration موجودی تغییر نکرد.

## ۴. APIهای بررسی‌شده

`Server/Api/CompanyMembershipRequestEndpoints.cs` — ۹ endpoint:

| Endpoint | AuthN | AuthZ سمت سرور | کنترل‌های دیگر |
|---|---|---|---|
| `GET /me/membership-requests/destinations` | ✅ | مرز CustomerBoundary مشتق از عضویت‌های فعال خود کاربر (CompanyAccessService، نه ورودی کلاینت) | جستجوی نام دقیق ۳..۱۲۰ نویسه (ضد enumeration)، rate limit |
| `POST /me/membership-requests` | ✅ | مقصد باید فعال و در مرز مجاز باشد؛ عضویت فعال/Pending موجود → 409 | Antiforgery، rate limit، catch unique index (2601/2627) |
| `GET /me/membership-requests` | ✅ | فقط درخواست‌های خود کاربر (`UserId == principal`) | صفحه‌بندی محدود |
| `GET /admin/companies/{id}/membership-requests/pending` | ✅ | `HasPermissionAsync(users.membership.approve)` از DB | خروجی حداقلی (allowlist) |
| `GET .../assignable-roles` | ✅ | همان مجوز؛ فقط نقش‌های زیر سقف واگذاری و غیرسیستمی | rate limit |
| `GET .../approvers` | ✅ | همان مجوز | rate limit |
| `GET ...` (تاریخچه) | ✅ | همان مجوز | rate limit |
| `POST .../{requestId}/approve` | ✅ | مجوز دوبار (قبل و داخل تراکنش)، self-approval ممنوع، نقش فعال متعلق به مقصد، delegation ceiling | RowVersion روی درخواست/عضویت/نقش‌های تأییدکننده (no-op update برای lock)، تراکنش ReadCommitted، Antiforgery |
| `POST .../{requestId}/reject` | ✅ | مجوز + status Pending در WHERE | Antiforgery، محدودیت ۵۰۰ نویسه reason |

## ۵. ماتریس تهدیدها و کنترل‌های امنیتی

| سناریو | لایه دفاعی | تست موجود |
|---|---|---|
| کاربر شرکت A شناسه درخواست شرکت B را ارسال کند | `CompanyAccessService.HasPermissionAsync` از DB + فیلتر `DestinationTenantId == companyId` | `ApproveMembership_RequiresDestinationPermissionAndCannotSelfApprove`، `ApproveMembership_DoesNotUsePermissionsGrantedInAnotherCompany` |
| شناسه شرکت خارج از مرز | Boundary فقط از عضویت‌های فعال خود کاربر مشتق می‌شود؛ fail-closed | `GetDestinations_...` (شرکت خارج از مرز)، `RequestMembership_...` خارج از مرز 404 |
| نقش Administrator/سقف بالاتر بدون اختیار | Delegation ceiling (subset مجوزها) + ممنوعیت `IsSystemRole` | ۳ تست ceiling + تست SQL Server نقش restricted |
| تأیید/رد درخواست دیگری | مجوز مقصد + self-approval 403 + `Status == Pending` در خود query | تست self-approval و unauthorized |
| تأیید هم‌زمان دو تأییدکننده | RowVersion + تراکنش + unique filtered index و check constraints | تست SQL Server single-winner |
| ثبت Pending تکراری | بررسی قبل + unique filtered index + catch SqlException | `RequestMembership_RejectsDuplicatePendingAndExistingActiveMembership` |
| تغییر درخواست در فاصله دریافت/تأیید | RowVersion + recheck مجوز درون تراکنش | تست `ConcurrentUpdateInterceptor` روی SQL Server |
| استفاده از دسترسی لغوشده | خواندن مجوز از DB در هر درخواست (D-064/D-013) + no-op RowVersion guard | تست concurrency لغو grant در SQL Server |
| ارسال بدون Antiforgery | `IsRequestValidAsync` قبل از هر نوشتن | `RequestMembership_RequiresAntiforgery` |

- شناسه‌های کلاینت هرگز جایگزین WorkContext/پرینسیپال معتبر نشست نمی‌شوند؛ `UserId` فقط از `ClaimsPrincipal` گرفته می‌شود.
- خروجی‌ها حداقلی و allowlist هستند؛ تست reflection، نبود Email/Phone/Password/Role/Permission در پاسخ pending را تضمین می‌کند.

## ۶. Audit و تراکنش

- رویدادهای `CompanyMembershipRequested/Approved/Rejected` هم‌تراکنش با تغییر داده در همان `SaveChanges` ثبت می‌شوند؛ خطای Audit باعث وضعیت نیمه‌کاره نمی‌شود.
- Actor، زمان UTC، Tenant، EntityType/EntityId، CorrelationId و جزئیات حداقلی ثبت می‌شوند؛ در Reject فقط بولی `ReasonProvided` (نه متن) و رمز/توکن/داده حساس وارد Log نمی‌شوند.
- تست‌های Audit موجود و مؤثرند (assert روی `SecurityAuditEvents` در هر سه مسیر).

## ۷. وضعیت تست‌ها و محدودیت محیط

- تست‌های InMemory موجود: ایجاد موفق، duplicate، حساب/مقصد غیرفعال، antiforgery، pending allowlist، self-approval، تأیید موفق با Audit، سقف واگذاری (مجاز/غیرمجاز/سیستمی/شرکت دیگر)، نقش خارجی/غیرفعال، رد موفق، درخواست‌های خود کاربر، destinations، assignable-roles، approvers، تاریخچه.
- تست‌های SQL Server (فقط با `FARANOU_TEST_SQLSERVER=1`): additive upgrade، حفظ عضویت‌ها، single-winner concurrent approval، رد نقش خارج از سقف بدون mutation، down/up با گارد rollback.
- **محدودیت محیط این ممیزی:** در این نشست `dotnet` SDK و SQL Server در دسترس نبود؛ Build/Test محلی اجرا نشد و هیچ نتیجه‌ای جعل نشد. معیار تأیید، CI روی `main@2a62811` است که سبز است (`.NET CI`، `Push on main`، Semgrep، Vendor CI در 2026-11-04).
- **نکته روش‌شناختی:** تست‌های هم‌زمانی واقعی (RowVersion/تراکنش) فقط با SQL Server معتبرند؛ EF Core InMemory جایگزین Integration Test واقعی SQL Server نیست و فقط برای منطق غیر وابسته به رفتار پایگاه داده مناسب است. در صورت نیاز به پوشش بیشتر، تست Integration مستقل SQL Server اضافه شود، نه تست InMemory.

## ۸. یافته‌ها به تفکیک شدت

| شدت | یافته |
|---|---|
| Critical | — |
| High | — |
| Medium | پوشش مسیر 409 هم‌زمانی تنها در تست SQL Server است که بدون `FARANOU_TEST_SQLSERVER=1` skip می‌شود؛ لایه دفاعی کد و DB صحیح است و طبق بند ۷ اصلاح آن فقط با تست Integration واقعی مجاز است. |
| Low | انحراف مستندات: توضیح بدنه PR #479 درباره کاربر بدون عضویت با منطق فعلی fail-closed (ISSUE-510) هم‌خوان نیست؛ کد و handoff صحیح‌اند. |
| Low | فیلد `IpAddress` در Audit رویدادها ثبت می‌شود؛ retention آن فقط در D-069/D-070 به‌صورت Proposed است. |

## ۹. وضعیت PRهای مرتبط و CI در زمان ممیزی

- `main@2a62811`: `.NET CI` ✅، `Push on main` ✅، Semgrep ✅، Vendor CI ✅.
- PR باز مرتبط: [#536](https://github.com/hamidreza1394/Accounting/pull/536) — E2E مثبت گردش درخواست عضویت (#535)؛ باز، غیر Draft، `mergeable: true`؛ در زمان ممیزی `.NET CI` آن در حال اجرا و E2E هنوز pending بود؛ هیچ نتیجه سبزی برایش ادعا نمی‌شود.
- Branch Protection دقیق این مخزن از API مخزن قابل خواندن نیست؛ طبق AGENTS.md، Merge با تأیید Maintainer و CI سبز الزامی است و این PR نیز merge نمی‌شود.

## ۱۰. موارد باقی‌مانده و پیشنهاد مرحله بعد (ورودی مأموریت ۲)

1. وضعیت واقعی PR #536 و نتیجه CI (build + E2E روی Chromium و SQL Server) پس از اتمام اجرا بررسی شود؛ تا آن زمان Verified نیست.
2. سناریوهای مثبت و منفی تأیید/رد درخواست بر اساس نتایج E2E واقعی #536 تکمیل و نتیجه واقعی CI ثبت شود.
3. Company Manager و Delegation Ceiling کامل (مورد ۲ Issue #444) — enforce فعلی فقط Installation Administrator/سقف مجوزهاست.
4. تست واقعی Tenant Isolation روی SQL Server برای تمام endpointهای حساس چندشرکتی (مورد ۵ Issue #444).
5. ممیزی امنیتی API گزارش دسترسی‌ها/امنیت (PR #519 و handoffهای ISSUE-512/507/515/485).
6. هماهنگی Permission و Entitlement: مرز اتصال مجوز عملیاتی به entitlement تجاری (D-025/D-059) فقط با قرارداد معتبر؛ بدون اعطای مجوز فرضی.
