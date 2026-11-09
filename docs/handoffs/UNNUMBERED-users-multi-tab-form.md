# تحویل وظیفه — UNNUMBERED-users-multi-tab-form

- **Issue:** UNNUMBERED (مأموریت رابط مدیریت سیستم، PR شماره ۳ — فرم پنج‌تبی ایجاد/ویرایش کاربر)
- **Agent/مسئول:** Claude Code (Lead) + frontend-developer + code-reviewer
- **تاریخ:** 2026-11-08
- **وضعیت:** انجام‌شده (بدون Backend جدید؛ وابسته به PR شمارهٔ ۲)

## هدف و معیارهای پذیرش
- [x] پنج تب: اطلاعات اصلی، اطلاعات تماس، اطلاعات استخدامی، سمت‌ها و نقش‌ها، سوابق
- [x] پیشنهاد شماره پرسنلی از `next-number` شرکت کاری فعال (قابل ویرایش، رزرونشده)
- [x] نشانهٔ خطا روی تب و بازشدن اولین تب دارای خطا؛ حفظ داده بین تب‌ها؛ بدون درخواست تکراری
- [x] بدون Mock، بدون موتور مجوز موازی، بدون وابستگی/دارایی خارجی جدید
- [ ] عکس کاربر، تلفن ثابت: **NotImplemented** (مدل `User` فیلد ندارد؛ عکس پرسنلی متعلق به HR است)
- [ ] تغییر شماره پرسنلی پس از ایجاد: **NeedsApproval** (فقط‌خواندنی نمایش داده می‌شود)

## خلاصه تغییرات
- `UserForm` با تب‌های `ui/Tabs`؛ پنل‌ها در `components/admin/users/form/`.
- ایجاد دو مرحله‌ای (`userCreateFlow.ts`): ابتدا حساب؛ سپس در صورت وجود شماره، `POST /org/employees` بدون اتصال (D-072). پس از ساخت حساب جریان هرگز throw نمی‌کند؛ 409 پیشنهادی تا ۲ بار تجدید می‌شود، 409 شمارهٔ تایپ‌شده هشدار `createdEmployeeDuplicate` می‌دهد، سایر خطاها `createdEmployeeFailed`. در ۴۰۱ صفحه navigate نمی‌کند.
- «نام و نام خانوادگی» در ایجاد الزامی شد (فقط کلاینت؛ سرور nullable می‌پذیرد، در ویرایش اختیاری ماند).
- تب ۴: تخصیص نقش با `assignCompanyRoleMember` پشت ConfirmDialog؛ لینک «مدیریت مجوزهای دسترسی» با ثابت `accessManagementPath` (در PR بعدی عوض می‌شود).

## تصمیم‌ها
- شماره پیشنهادی به شرکتی که برایش گرفته شده وابسته است؛ با تغییر شرکت، شمارهٔ دست‌نخوردهٔ شرکت قبلی جایگزین و شمارهٔ تایپ‌شده حفظ می‌شود (یافتهٔ بازبین).
- `workContext` پیش‌فرض fail-closed؛ hintها فقط UI هستند و سرور تصمیم می‌گیرد.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی |
|---|---|
| `npm run typecheck` | موفق |
| `npx vitest run` کل | ۹۹۰ تست قبل از اصلاحات بازبینی: ۹۸۴ موفق، ۶ ناموفق = دقیقاً شکست‌های baseline روی main |
| `npx vitest run src/components/admin/users` پس از اصلاحات | ۱۶۰ تست: ۱۵۸ موفق، ۲ ناموفق (هر دو از baseline) |
| `npm run build` / `npm run check:offline` | موفق (۶۴ منبع محلی) |
| `python3 scripts/check_static_assets.py` / `git diff --check` | موفق |
| E2E/Playwright، بازبینی بصری مرورگر، dotnet | **اجرا نشد** (Backend تغییر نکرد) |

شکست‌های baseline (زمان/محیط، دست‌نخورده): AuditTimeline تاریخ جلالی، UsersListPage، UserDetailPage memberships، SecurityAuditPage CSV ×2، securityAuditClient CSV.

## بازبینی (دروازهٔ ث)
`code-reviewer` مستقل: Critical/High ندارد. Medium: شمارهٔ کهنه پس از تغییر شرکت (اصلاح + تست)، handoff (این فایل). Low: ۴۰۱ مرحلهٔ دوم (اصلاح + تست)، الزام نام فقط کلاینت (ثبت شد)، `aria-controls` پنل‌های unmounted در `ui/Tabs` (پیشینه، خارج از دامنه).

## ریسک‌ها و کارهای باقی‌مانده
- پروندهٔ پرسنلی بدون اتصال پس از ایجاد باید در تب «اطلاعات استخدامی» وصل شود.
- لینک به صفحهٔ کارمند HR guard تغییرات ذخیره‌نشده ندارد.
- تعویض `accessManagementPath` با صفحهٔ مستقل مجوزها (PR بعدی).

## شناسه‌های Git
- Branch: `feat/users-multi-tab-form` (روی `feat/hr-next-employee-number`)
- Commit / Pull Request: در گزارش PR
