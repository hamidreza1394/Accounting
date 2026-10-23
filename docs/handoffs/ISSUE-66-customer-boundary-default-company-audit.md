# تحویل وظیفه — ISSUE-66-customer-boundary-default-company-audit

- **Issue:** #66 (بسته؛ PRهای #65، #67، #68، #69 ادغام شده‌اند). این PR ممیزی/سخت‌سازی/مستندسازی است.
- **Agent/مسئول:** Lead + Subagentها (`architect`، `security-reviewer`، `documentation-engineer`)؛ این سند توسط `documentation-engineer`
- **تاریخ:** 2026-10-23
- **وضعیت:** ناقص — کد و مستندات آماده بازبینی؛ CI اجرا نشده؛ چند تصمیم `NeedsApproval`

## هدف و معیارهای پذیرش
(بدون تیک مگر با شاهد؛ D-030)
- [ ] مرز مشتری، شرکت پیش‌فرض و برچسب‌ها با ADR (D-038..D-040) ثبت شوند. (مستندات این PR؛ تأیید کارفرما لازم است)
- [ ] هر مسیر نوشتن/خواندن گروه و مالکیت در سرویس سمت سرور به یک مرز Provision‌شده محدود باشد. (`Implemented` در کد؛ `Verified` ادعا نمی‌شود)
- [ ] CI سبز. (**اجرا نشده**: Jobها به‌علت سقف صورتحساب GitHub شروع نمی‌شوند)

## خلاصه تغییرات
**آنچه PR #68 قبلاً رساند (بدون ADR/handoff/WORK_LOG؛ شرح آن می‌گفت Build/Test اجرا نشده):**
- `Users.DefaultCompanyId` (FK به `Companies`، `ON DELETE SET NULL`، ایندکس‌شده؛ Migration `20261023190000_AddUserDefaultCompany`) و `POST /api/v1/context/default-company` (Anti-forgery، نیازمند `FindAccessibleCompanyAsync`، 404 یکسان، `null` پاک می‌کند)؛ `GET /api/v1/context` پیش‌فرض را فقط در صورت دسترس‌پذیری برمی‌گرداند؛ نشان «پیش‌فرض شما»؛ پیش‌فرض فقط پیش‌انتخاب می‌کند.
- `Companies.CustomerBoundaryId` (`Guid?`، ایندکس‌شده، مات؛ Migration `20261023191000_AddCompanyCustomerBoundary`)؛ تنها نویسنده `POST /api/v1/operator/customer-boundaries/assign`.
- اعمال مرز در `CorporateGroupService` و برچسب‌های `relationshipTypes`.

**این کار (ممیزی/سخت‌سازی):**
1. برچسب `groupMember`؛ `independent` مستلزم نبود عضویت فعال گروه.
2. تفکیک `JointControl` به `jointVenturer`/`jointVenture` (پیش‌تر هر دو `jointVenture`).
3. Assign در تراکنش Serializable با Execution Strategy؛ Deadlock/Snapshot (SQL 1205/3960) → 409.
4. Endpoint جدید `GET /api/v1/operator/customer-boundaries/diagnostics` (فقط‌خواندنی، فقط حساب زیرساختی، بدون نام).
5. ممیزی کران‌دار Provision (تعداد + SHA-256 + نام اپراتور) به‌جای فهرست شناسه؛ ثبت ردها با `CustomerBoundaryProvisionRejected`.
6. `CorporateGroupService`: بررسی مرز داخل تراکنش‌های `AddMember`/`RecordOwnership`؛ بررسی چرخه/درصد/کنترل یگانه محدود به روابط داخل مرز نامزد.
7. اصلاح تست‌های شکسته از قبل روی `main` (حذف نشدند).
8. مستندات: D-038..D-040، افزوده D-037، توضیح D-033، `INDEX`، `CORPORATE_GROUPS`، `LICENSING_DATA_MODEL`، `DATABASE`، `SECURITY`، `TESTING`، `PROJECT_STATUS`، `WORK_CONTEXT_SELECTOR_UI`، `WORK_LOG`.

## فایل‌های مهم تغییرکرده
- `Server/Api/CustomerBoundaryEndpoints.cs` — تراکنش Serializable، تشخیصی، ممیزی کران‌دار
- `Server/Api/WorkContextEndpoints.cs` — برچسب‌های `groupMember` و `JointControl`
- `Server/Infrastructure/CorporateGroups/CorporateGroupService.cs` — بررسی مرز داخل تراکنش و محدودکردن قواعد به مرز
- `ClientApp/src/api/types.ts`، `ClientApp/src/components/WorkContextSelector.tsx` — کدها/برچسب‌های جدید
- تست‌ها: `tests/Template.Tests/Integration/WorkContextHttpTests.cs`، `tests/Template.Tests/CorporateGroupServiceTests.cs`، `tests/Template.Tests/Integration/CorporateGroupSqlServerTests.cs`، `ClientApp/src/App.test.tsx`، `ClientApp/src/components/WorkContextSelector.test.tsx`، `tests/e2e/specs/faranou.spec.mjs`
- مستندات: فهرست در بند ۸ بالا و `docs/DECISIONS.md`

## تصمیم‌های فنی
- D-038: طرح مرز حفظ شد (متناسب؛ هم‌سو با D-020/D-021/D-037؛ بدون کپی `CustomerAccount`؛ `Company` همان Tenant؛ مرز فقط محدود می‌کند، D-013 بدون تغییر). مقدار مرز تصادفی و **هرگز** برابر `CustomerAccount.Id` نیست؛ نگاشت به `CustomerAccount` فقط در سامانه فروشنده (`NotImplemented`). `null` = Fail-closed دائمی؛ بدون Backfill خودکار؛ مرز غیرتهی از راه API تغییر نمی‌کند.
- ماتریس استقرار/انتقال: بازیابی کامل ← مرزها برمی‌گردند؛ انتقال کل مشتری ← مقدار می‌تواند بماند؛ درون‌ریزی یک شرکت به ابر مشترک ← با `null` و Provision مجدد؛ کپی VM آفلاین کنترل‌پذیر نیست (D-027) و مرز کنترل لایسنس نیست؛ نصب اختصاصی ← یک مرز اعلام‌شده توسط Master مشتری و شرکت جدید با `null` (اصطکاک عمدی).
- D-039: شرکت پیش‌فرض فقط پیش‌انتخاب می‌کند. D-040: قاعده پیش‌انتخاب سال/شرکت و جدول برچسب‌ها؛ بند «چیزی پیش‌انتخاب نمی‌شود» در D-031 جایگزین شد (بدون بازنویسی D-031).
- **انحراف پیاده‌سازی:** PR #68 پیش از ثبت ADR ادغام شد؛ کارفرما باید D-038..D-040 را صریحاً تأیید یا اصلاح کند.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `dotnet build Template.sln -c Release` | موفق | ۰ خطا |
| `dotnet test Template.sln` (SQL Server 2022 واقعی در Docker) | موفق | ۵۲۴ کل؛ ۵۲۲ موفق؛ ۰ شکست؛ ۲ ردشده (Skip از پیش موجود) |
| `npm run typecheck` / `npm test` / `npm run build` / `check:offline` (ClientApp) | موفق | Vitest: ۹۴ از ۹۴ در ۷ فایل |
| `scripts/check_static_assets.py`، `git diff --check` | موفق | بدون CDN؛ بدون خطای فضای خالی |
| E2E Playwright (انتشار واقعی + SQL Server) | موفق | ۲۹ از ۲۹ روی کد نهایی |
| پیش از این کار روی `main`: `dotnet test` | **شکست** | یک تست (`CorporateGroupSqlServerTests.TheMigration_UpgradesADatabaseWithData...`: ستون `CustomerBoundaryId` در شمای قدیمی نبود) و ۶ تست Vitest + ۱ E2E با قاعده منسوخ «چیزی پیش‌انتخاب نمی‌شود»؛ در این PR اصلاح شدند (حذف/غیرفعال نشدند) |
| CI (GitHub Actions) | اجرا نشد | Jobها به‌علت سقف صورتحساب GitHub شروع نمی‌شوند |
| Firefox / Safari / axe / HTTPS پشت Proxy | اجرا نشد | — |
| بازبینی معماری و امنیتی مستقل | انجام شد | طرح تأیید؛ F1 اصلاح؛ F5 باز (پایین) |

## امنیت، داده و سازگاری
- دو Migration افزایشی از PR #68؛ اعمال روی دیتابیس مشتری/تولید ادعا نمی‌شود. بدون PII؛ بدون اسرار.
- Endpointهای اپراتور فقط برای حساب زیرساختی (غیر آن 404)، Rate Limit، ممیزی؛ ممیزی کران‌دار (F1: ستون `DetailsJson` ۴۰۰۰ نویسه با بیش از حدود ۱۰۰ شناسه سرریز می‌کرد؛ اصلاح شد).
- API/UI گروه و گزارش گروه وجود ندارد؛ `CorporateGroupService` فقط در DI ثبت است.

## ریسک‌ها و کارهای باقی‌مانده
- اعمال مرز فقط سطح برنامه (SQL مستقیم دور می‌زند) — تریگر اختیاری دیتابیس: `NeedsApproval` (FK ترکیبی و تریگر روی جداول رابطه ردشده).
- اپراتور = حساب Master محلی (راز مشترک، بدون MFA) — انتقال به سامانه فروشنده در آینده.
- F5: اشتباه تایپی GUID می‌تواند دو مشتری را در یک مرز بگذارد — کاهش پیشنهادی `expectNewBoundary`/تعداد مورد انتظار (پیاده‌سازی نشده).
- برچسب‌ها برای عضو بدون `corporate-groups.view` دیده می‌شوند (فقط داخل یک مرز) — `NeedsApproval`.
- اصطلاحات برچسب (طرف‌های `JointControl`، «سرمایه‌پذیر بدون کنترل») — `NeedsApproval` کارشناس حسابداری.
- رویه اصلاح/ادغام/تفکیک/فروش شرکت (باید Serializable و ممیزی‌شده باشد) — `NeedsApproval`، `NotImplemented`.
- تشخیصی همه ردیف‌ها را بارگذاری می‌کند (فقط اپراتور، Rate Limit).
- `AGENTS.md`/`CLAUDE.md` هنوز «ADR بعدی D-036» می‌گویند؛ اکنون D-041 (تغییر با تأیید صریح کاربر).
- **ادامه:** (۱) تأیید کارفرما برای D-038..D-040؛ (۲) اجرای CI پس از رفع سقف صورتحساب و ثبت نتیجه؛ (۴) طراحی سامانه فروشنده و رویه اصلاح مرز؛ (۵) طراحی API/UI گروه فقط پس از تأیید.

## شناسه‌های Git
- Branch: `feature/customer-boundary-audit-and-docs`
- Commit: ثبت نشده در زمان نگارش
- Pull Request: ثبت نشده در زمان نگارش (مرتبط: #65، #67، #68 `4bcee61`، #69؛ Issue #66)
