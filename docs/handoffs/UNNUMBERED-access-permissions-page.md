# تحویل وظیفه — UNNUMBERED-access-permissions-page

- **Issue:** UNNUMBERED (مأموریت رابط مدیریت سیستم، PR شماره ۴ — صفحهٔ مستقل «مجوزهای دسترسی»)
- **Agent/مسئول:** Claude Code (Lead) + frontend-developer + code-reviewer + security-reviewer
- **تاریخ:** 2026-11-08
- **وضعیت:** انجام‌شده (فقط Frontend؛ بدون Backend/Migration)

## هدف و معیارهای پذیرش
- [x] مسیر `/admin/access` با دو تب «زیرسیستم‌ها» و «دسترسی فرم‌ها» (انتخاب‌گر زیرسیستم داخل تب دوم)
- [x] انتخاب شرکت و کاربر، وضعیت عضویت، نقش‌های فعلی و دسترسی مؤثر (`effective-permissions`)
- [x] تخصیص نقش با ConfirmDialog (`assignCompanyRoleMember`)؛ ایجاد نقش فقط لینک به جریان موجود
- [x] هشدار نقش مشترک با تعداد کاربران (از `permissions/preview`) و تأیید صریح پیش از `PUT`
- [x] فقط مجوزهای واقعی `GET /admin/permission-catalog`؛ بدون مجوز ساختگی؛ نقش سیستمی فقط‌خواندنی
- [x] آیتم منوی «مجوزهای دسترسی» و تعویض `accessManagementPath` فرم کاربر با `/admin/access`
- [ ] فعال‌سازی per-company زیرسیستم و بازیابی تنظیمات قبلی: **NotImplemented** (پرچم سمت سرور وجود ندارد)

## تصمیم‌ها
- «فعال بودن زیرسیستم» از کدهای واقعی نقش مشتق می‌شود (نگاشت نمایشی پیشوند کد ← زیرسیستم در `lib/accessSubsystems.ts`، از `docs/SUBSYSTEM_CATALOG.md`)؛ به سرور فرستاده نمی‌شود. غیرفعال‌سازی کدهای همان زیرسیستم را حذف و کدهای دیگر/ناشناخته را حفظ می‌کند؛ فعال‌سازی دوباره فقط view/list را برمی‌گرداند و صفحه این محدودیت را صریح می‌گوید.
- ذخیره: `preview` → ConfirmDialog → `PUT roles/{id}` (جایگزینی کامل). اگر `preview` رد شود `PUT` ارسال نمی‌شود.
- `companyId` فقط در مسیر URL؛ هرگز در بدنه.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی |
|---|---|
| `npm run typecheck` / `npm run build` / `npm run check:offline` (۶۶ منبع محلی) | موفق |
| `python3 scripts/check_static_assets.py` / `git diff --check` | موفق |
| `npx vitest run` کل (Lead، پیش از اصلاحات کوچک بازبینی) | ۱۰۲۵ تست: ۱۰۱۹ موفق، ۶ ناموفق = همان baseline روی main |
| `vitest` فایل‌های access + AppRouting + shell (پس از اصلاحات) | ۱۰۸ موفق، ۰ ناموفق |
| E2E/Playwright، بازبینی بصری مرورگر، dotnet | **اجرا نشد** (Backend تغییر نکرد) |

## بازبینی
- `code-reviewer`: Critical/High ندارد. اصلاح‌شده: `aria-label` روی `span`، تست مربوط هم‌راستا شد (assertion ضعیف نشد). ثبت‌شده به‌عنوان ریسک: `PUT` نقش، `name`/`isActive` را از فهرست بارگذاری‌شده می‌فرستد و توکن هم‌زمانی سمت کلاینت ندارد (آخرین نوشتن برنده)؛ محافظ تغییرات ذخیره‌نشده فقط برای تغییر شرکت/کاربر/نقش و `beforeunload` است نه ناوبری درون‌برنامه‌ای؛ نگاشت پیشوند ← زیرسیستم دستی است.
- `security-reviewer`: Critical/High/Medium ندارد. Low (سمت سرور، از قبل): سقف تفویض در کاتالوگ/Preview اجتماع همهٔ شرکت‌هاست ولی ذخیره فقط سقف همان شرکت را می‌سنجد (fail-closed با 403؛ فقط راهنمای گمراه‌کننده)؛ حذف کدهای خارج از سقف طبق D-071 مجاز است؛ پیام نادرست «حفظ کدهای خارج از فهرست» اصلاح شد.

## ریسک‌ها و کارهای باقی‌مانده
- Issue جداگانه: محدودکردن Preview/کاتالوگ به سقف همان شرکت.
- توکن هم‌زمانی `PUT` نقش و محافظ ناوبری درون‌برنامه‌ای.

## شناسه‌های Git
- Branch: `feat/access-permissions-page` (روی `feat/users-multi-tab-form`)
- Commit / Pull Request: در گزارش PR
