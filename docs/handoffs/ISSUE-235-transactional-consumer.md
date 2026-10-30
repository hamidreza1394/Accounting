# تحویل وظیفه — ISSUE-235-transactional-consumer

- **Issue:** [#235](https://github.com/hamidreza1394/Accounting/issues/235)
- **Agent/مسئول:** ChatGPT / GitHub connector
- **تاریخ:** 2026-10-30
- **وضعیت:** تصمیم‌های معماری پذیرفته؛ parent-reference validation به‌صورت جزئی پیاده‌سازی شده؛ transactional consumer هنوز تکمیل نشده است.

## هدف و معیارهای پذیرش
- [ ] verify بسته و binding customer/install قبل از هر mutation.
- [ ] resolve مرجع مادر فقط با RequestId canonical و claim محلی.
- [ ] apply اتمیک Company/FiscalPeriod/Admin/Membership/entitlements/claim.
- [ ] replay هم‌زمان و retry idempotent بدون duplicate side effect.
- [ ] enrollment یک‌باره، reissue مجوزدار، expiry، attempt limit و force password change.
- [ ] acknowledgement امضاشده با نتیجه Applied/AlreadyApplied/Rejected و کد خطای ثابت.
- [ ] تست SQL Server، rollback، replay هم‌زمان و E2E.

## تصمیم‌های فنی
- D-053 در 2026-10-30 با تأیید صریح مالک محصول Accepted.
- ParentCompanyReference = Vendor RequestId؛ parent و subsidiary والد مجاز، independent رد؛ فقط ParentCompanyId ذخیره می‌شود.
- شرکت‌های قدیمی بدون claim رد می‌شوند؛ هیچ Company column یا migration برای مرجع قدیمی در این scope.
- Enrollment token فقط پس از commit در پاسخ اولیه؛ replay فقط ack؛ reissue endpoint مستقل و audit-safe.
- Production signer فقط provider واقعی Issue #236 و fail-closed.

## فایل‌های تغییرکرده در این slice
- docs/DECISIONS.md — D-053
- docs/LICENSING_THREAT_MODEL.md — تهدیدها و کنترل‌های Enrollment/parent reference
- docs/INDEX.md — فهرست ADR
- docs/WORK_LOG.md — سابقه تصمیم
- AGENTS.md — شماره ADR بعدی
- docs/handoffs/ISSUE-235-transactional-consumer.md — این handoff

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| بررسی PR #302 و merge commit | موفق | merge 4c259280c40af7037901e9cb6f33c64206dbdaeb؛ CI .NET/Semgrep/Push on main موفق |
| بررسی PR #306 و merge commit | موفق | merge ac3d481cf8d8091bd139295a9423aa30e0011fb7؛ CI .NET/Semgrep/Push on main موفق |
| dotnet build | اجرا نشده | checkout محلی در این محیط به‌دلیل DNS شبکه ممکن نشد |
| SQL Server integration / rollback | اجرا نشده | consumer هنوز پیاده‌سازی نشده |
| concurrent replay | اجرا نشده | consumer هنوز پیاده‌سازی نشده |

## امنیت، داده و سازگاری
- کد runtime برای validation و resolve مرجع والد تغییر کرده است؛ هنوز apply endpoint/service وجود ندارد.
- هیچ schema یا Migration مشتری تغییر نکرده است و هیچ Company schema جدیدی اضافه نشده است.
- Vendor و Customer همچنان از نظر دیتابیس جدا هستند.

## ریسک‌ها و کارهای باقی‌مانده
- پیاده‌سازی کامل سرویس/endpointهای apply و reissue و transaction boundary.
- بررسی نیاز دقیق schema برای شمارنده تلاش و reissue metadata؛ هر migration افزایشی فقط در #235 و همراه SQL Server tests.
- پیاده‌سازی parent validation در Vendor create/issuance و customer consumer.
- تست واقعی rollback و replay هم‌زمان و تست CI روی head نهایی.
- production signing تا Issue #236 مسدود است.

## شناسه‌های Git
- **Branch:** feat/issue-235-transactional-consumer
- **Commit:** پس از ثبت این مستندات اعلام می‌شود.
- **Pull Request:** هنوز ایجاد نشده است.
