# تحویل وظیفه — ISSUE-510-membership-workflow-api

- **Issue:** #510 (زیرمجموعه #444؛ D-013، D-037، D-038، D-065، #496، #498)
- **مسئول:** ChatGPT — Backend/API
- **تاریخ:** 2026-11-03
- **وضعیت:** Implemented؛ اجرای تست‌ها و CI هنوز تأیید نشده‌اند.

## هدف و معیارهای پذیرش
- [x] کشف مقصد فقط با نام دقیق (۳ تا ۱۲۰ نویسه)، صفحه‌بندی حداکثر ۱۰۰ و بدون فهرست کامل.
- [x] دامنهٔ مقصد فقط CustomerBoundary غیرتهی و مشترک با یکی از عضویت‌های فعال کاربر؛ مرز null و مرز متفاوت fail-closed.
- [x] کنترل CustomerBoundary در POST ثبت درخواست نیز سمت Backend تکرار می‌شود.
- [x] نقش‌های قابل واگذاری فقط فعال، غیرسیستمی و زیر سقف تفویض تأییدکننده.
- [x] فهرست تأییدکنندگان فقط کاربران فعال با عضویت/نقش فعال و permission تأیید در همان شرکت.
- [x] سوابق تصمیم با فیلتر وضعیت و صفحه‌بندی؛ تاریخچهٔ درخواست‌های خود کاربر با فیلتر وضعیت.
- [x] RowVersion/transaction/unique pending index موجود در approval/reject حفظ شد.
- [x] تست cross-company، boundary، سقف تفویض، وضعیت‌ها و SQL Server اضافه شد.
- [x] بدون Migration، ClientApp یا E2E.

## خلاصه تغییرات
- قرارداد API پیش از کد در `docs/FRONTEND_API_CONTRACT_NEEDS.md` تکمیل شد.
- `CompanyMembershipRequestEndpoints` توسعه یافت: destinations، assignable-roles، approvers، request history و فیلتر status.
- مسیر ثبت درخواست اکنون از `CompanyAccessService.GetAccessibleCompaniesAsync` برای تعیین عضویت‌های مبدأ استفاده می‌کند؛ شناسهٔ مقصد مستقیم از کلاینت بدون کنترل مرز پذیرفته نمی‌شود.
- زمان‌های EF خوانده‌شده برای تمام responseهای `*Utc` این endpointها صریحاً با Kind=UTC سریال می‌شوند.

## قرارداد API
- `GET /api/v1/me/membership-requests/destinations?search={exactName}&page=1&pageSize=25`
  - search اجباری، exact، پس از Trim بین ۳ تا ۱۲۰ نویسه.
  - پاسخ: `{ items: [{ companyId, name }], totalCount, page, pageSize }`.
- `POST /api/v1/me/membership-requests` همچنان همان body را می‌گیرد؛ CustomerBoundary از عضویت‌های فعال کاربر و مقصد دوباره کنترل می‌شود.
- `GET /api/v1/admin/companies/{companyId}/membership-requests/assignable-roles?page=&pageSize=`
  - فقط نقش‌های فعال و غیرسیستمی که تمام permissionهایشان در سقف تفویض فعال تأییدکننده است.
- `GET /api/v1/admin/companies/{companyId}/membership-requests/approvers?page=&pageSize=`
  - فقط فهرست حداقلی تأییدکنندگان مجاز در همان شرکت.
- `GET /api/v1/admin/companies/{companyId}/membership-requests?status=&page=&pageSize=`
  - status اختیاری: Pending/Approved/Rejected.
- `GET /api/v1/me/membership-requests?status=&page=&pageSize=`
  - فقط درخواست‌های خود کاربر.
- تمام فهرست‌ها pageSize حداکثر ۱۰۰ و no-store؛ endpointهای مدیریتی/کشف مقصد rate-limited؛ عدم مجوز مقصد با 404 پاسخ داده می‌شود.

## تاریخچه و دامنه
- سوابق این API تاریخچهٔ درخواست و تصمیم را پوشش می‌دهد؛ Domain فعلی مدل مستقل تاریخچهٔ تمام تغییرات دستی UserCompanyMembership را ندارد. این PR تاریخچهٔ عضویت غیرموجود را جعل نمی‌کند.
- CustomerBoundary فقط مرز جداسازی است و permission نمی‌دهد؛ تمام مجوزهای عملیاتی همچنان با CompanyAccessService و permission مقصد بررسی می‌شوند. CorporateGroup membership/ownership به‌تنهایی دسترسی ایجاد نمی‌کند.

## فایل‌های مهم تغییرکرده
- `Server/Api/CompanyMembershipRequestEndpoints.cs`
- `tests/Template.Tests/CompanyMembershipRequestEndpointTests.cs`
- `tests/Template.Tests/Integration/CompanyMembershipRequestReadSqlServerTests.cs`
- `docs/FRONTEND_API_CONTRACT_NEEDS.md`, `docs/WORK_LOG.md`, `docs/INDEX.md` و این handoff.

## امنیت، داده و سازگاری
- هیچ Migration یا schema تغییر نکرد.
- درخواست‌کننده فقط در مرز provisionشدهٔ خودش مقصد exact را کشف می‌کند؛ POST همان قاعده را دوباره enforce می‌کند.
- role assignment در GET فقط پیشنهاد UI است؛ POST approve سقف تفویض را مجدداً از DB محاسبه می‌کند و RowVersionهای membership/role/request را کنترل می‌کند.
- endpointهای مدیریتی با CompanyAccessService و permission صریح همان مقصد محافظت می‌شوند.
- Audit ایجاد/تأیید/رد موجود حفظ شده است؛ GETها فقط دادهٔ allowlist را برمی‌گردانند.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| Unit tests عضویت | اجرا نشده | محیط اجرای محلی مخزن در دسترس نیست؛ CI باید اجرا کند. |
| SQL Server read-query integration | اجرا نشده | تست جدید برای SQL Server اضافه شده است؛ اجرای واقعی منتظر CI. |
| SQL Server concurrent approval | اجرا نشده در این مرحله | تست موجود `CompanyMembershipRequestSqlServerTests` حفظ شده و CI باید آن را اجرا کند. |
| `dotnet restore Template.sln` | اجرا نشده | محیط محلی مخزن در دسترس نیست. |
| `dotnet build Template.sln --no-restore --configuration Release` | اجرا نشده | محیط محلی مخزن در دسترس نیست. |
| `dotnet test Template.sln --no-build --configuration Release` | اجرا نشده | محیط محلی مخزن در دسترس نیست. |
| `python3 scripts/check_static_assets.py` | اجرا نشده | محیط محلی مخزن در دسترس نیست. |
| Migration guards / pending model changes | اجرا نشده | Migration جدیدی وجود ندارد. |
| `git diff --check` | اجرا نشده | working tree محلی در دسترس نیست. |

## ریسک‌ها و کارهای باقی‌مانده
- Build و تست‌های جدید باید در CI اجرا شوند؛ تا آن زمان Verified نیست.
- فهرست approvers فعلاً برای تأییدکنندگان مجاز مقصد است، نه کاربران بدون عضویت مقصد.
- اگر مالک تاریخچهٔ مستقل تغییرات membership را لازم بداند، باید lifecycle/audit contract جداگانه طراحی شود؛ این PR جدول جدیدی ایجاد نمی‌کند.

## شناسه‌های Git
- Branch: `feat/510-membership-workflow-api`
- Commit نهایی و PR: پس از ایجاد PR ثبت می‌شوند.
