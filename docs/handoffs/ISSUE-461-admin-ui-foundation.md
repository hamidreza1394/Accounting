# تحویل وظیفه — ISSUE-461-admin-ui-foundation

- **Issue:** #461 (PR-A1)
- **Agent/مسئول:** frontend-developer (Claude Code)
- **تاریخ:** 2026-11-03
- **وضعیت:** انجام‌شده (بازبینی مستقل و Merge هنوز انجام نشده)

## هدف و معیارهای پذیرش
- [x] روتر کوچک History API زیر `/app/` همراه تست
- [x] `Permission` و `can`/`usePermissions` (تلمیح UI) همراه تست
- [x] `sendJson` با PUT/PATCH، `normalizeApiError`، `apiPostJson` برای دو صفحهٔ Provisioning
- [x] کامپوننت‌های مشترک `ui/` با تست رفتاری
- [x] `lib/format.ts` و جایگزینی `formatDate` تکراری
- [x] 120 تست قبلی همچنان سبز؛ typecheck، build و check:offline موفق

## خلاصه تغییرات
- روتر درون‌پروژه‌ای: `RouterProvider`، `RouteView`، `Link`، `useNavigate`، `useLocation`، `useParams`، `matchPath`؛ عنوان تب `<title> | فرانو`؛ NotFound و Forbidden (تلمیح مجوز)؛ مسیرهای lazy با Suspense و `LoadingState`.
- `permissions.ts`: فقط از `session.isAdministrator` برای `admin.*` و همیشه true برای `account.view`؛ سرور مرجع است (D-064).
- `client.ts`: `ApiError.fieldErrors`، `normalizeApiError(status, body)` برای ProblemDetails و `{ error }`، `sendJson` صادرشده با PUT/PATCH، `apiPostJson`/`apiGetRaw` (بدون پرتاب برای وضعیت ناموفق، با همان هدرها و `cache: 'no-store'` قبلی). کامنت جابه‌جاشدهٔ `AdminUserQuery` اصلاح شد.
- دو صفحهٔ Provisioning به‌جای `fetch` خام از این کمک‌ها استفاده می‌کنند؛ پیام‌ها و رفتار بدون تغییر.
- `format.ts`: یک پیاده‌سازی بر پایهٔ `formatPersianDate`؛ `UserManagementPage` و `MyAccountPanel` از `formatDateTime` استفاده می‌کنند.
- `Badge` با لحن `danger` گسترش یافت.

## فایل‌های مهم تغییرکرده
- `ClientApp/src/lib/router.tsx`، `permissions.ts`، `format.ts` (+ تست‌ها) — جدید
- `ClientApp/src/components/ui/{FormField,FormSection,ConfirmDialog,Tabs,StatusBadge,DataTable,Breadcrumb}.tsx` (+ تست‌ها) — جدید
- `ClientApp/src/components/ui/Badge.tsx` — لحن danger
- `ClientApp/src/api/client.ts`، `client.test.ts`
- `ClientApp/src/components/{ProvisioningEnrollmentPage,CustomerProvisioningImportPage,UserManagementPage,MyAccountPanel}.tsx`
- `docs/UI_COMPONENTS.md`، `docs/WORK_LOG.md`، این فایل

## تصمیم‌های فنی
- روتر درون‌پروژه‌ای به‌جای وابستگی جدید (D-011)؛ ADR جدید ثبت نشد.
- کش توکن Anti-forgery اضافه نشد: توکن به هویت کاربر وابسته است و کش بیات پس از ورود/خروج ایمن نیست.
- کلیدهای `errors` سرور با حرف اول کوچک نرمال می‌شوند (`Email` به `email`).
- پیام `ApiError` ابتدا `title`، سپس `error` و سپس پیام فارسی وضعیت است؛ `detail` هرگز در پیام کاربر نمی‌آید. اگر `errors` وجود داشته باشد و عنوان همان پیش‌فرض انگلیسی ASP.NET (`One or more validation errors occurred.`) باشد، پیام فارسی وضعیت نمایش داده می‌شود. برای 5xx هرگز متن بدنه نمایش داده نمی‌شود.
- نمایش تاریخ-زمان در دو صفحه از قالب «۱۱ آبان ۱۴۰۵، ۱۱:۳۰» به «۱۴۰۵/۰۸/۱۱ ۱۱:۳۰» تغییر کرد (یکپارچگی با `formatPersianDate`).
- `ConfirmDialog` در حالت busy از `aria-disabled` (نه `disabled`) استفاده می‌کند؛ دکمه‌ها focusپذیر می‌مانند و Tab/Shift+Tab حتی در حالت busy داخل دیالوگ می‌چرخد. Escape با شنونده سطح `document` کار می‌کند (در حالت busy نادیده گرفته می‌شود). رد شدن (reject) یا پرتاب هم‌زمان `onConfirm` بلعیده می‌شود (فراخواننده خطای خود را نشان می‌دهد)، قفل ارسال دوباره آزاد می‌شود و دیالوگ قابل استفاده می‌ماند.
- `Link` فقط مسیر داخلی تک‌اسلشی (`/x`) را مدیریت می‌کند؛ `https://`، `//host` و `mailto:` به مرورگر واگذار می‌شود. `navigate` به URL یکسان با فعلی به‌جای push، replace می‌کند. ترتیب مسیرها مهم است و اولین تطابق برنده است (مثلاً `/x/new` پیش از `/x/:id`).

## بررسی و آزمون (در `ClientApp`)
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `npm run typecheck` | موفق | بدون خطا |
| `npm test` | موفق | 23 فایل، 180 تست (120 قبلی + 60 جدید) |
| `npm run build` | موفق | JS 389.55 kB (119.28 gzip)، CSS 108.68 kB (20.51 gzip)؛ پیش از تغییر JS 389.00 (119.02) و CSS 105.94 (20.09) |
| `npm run check:offline` | موفق | 6 منبع محلی، بدون منبع بیرونی |
| `git diff --check` | موفق | بدون خروجی |
| `dotnet build` / `check_static_assets.py` | اجرا نشد | Razor/Backend تغییر نکرد |
| بررسی دستی مرورگر (۳۶۰/۷۶۸/۱۲۸۰، Network) | اجرا نشد | مرورگر در این محیط اجرا نشد؛ کامپوننت‌ها هنوز در صفحه‌ای استفاده نشده‌اند |

## امنیت، داده و سازگاری
- بدون تغییر Backend، Schema یا Migration. مجوز UI فقط تلمیح است. هیچ توکنی در `localStorage` ذخیره نمی‌شود. وابستگی npm جدید اضافه نشد.

## ریسک‌ها و کارهای باقی‌مانده
- روتر و کامپوننت‌ها هنوز در `App`/`AppShell` وصل نشده‌اند (PR بعدی)؛ تست در مرورگر واقعی انجام نشد.
- `DataTable`: `sticky` سربرگ زیر `md` درون ظرف اسکرول افقی نسبت به viewport عمل نمی‌کند.
- بازبینی مستقل: `code-reviewer` ۰ مورد مسدودکننده و ۳ مورد major یافت؛ هر سه اصلاح شد (focus trap در حالت busy و Escape سطح document؛ مدیریت reject/throw در `onConfirm`؛ لینک‌های غیرداخلی، URL یکسان و ترتیب مسیرها) و `detail` از پیام خطا حذف شد. بازبینی دوباره انجام نشد.
- موارد minor اصلاح‌نشده: ‏`router.tsx` از `components/Feedback` import می‌کند (وابستگی lib به components)؛ همپوشانی لینک روی ردیف `DataTable` (stretched link) نیاز به بررسی در مرورگر واقعی دارد؛ در `Tabs` مقدار `aria-controls` به پنل‌های رندرنشده اشاره می‌کند؛ پس‌زمینهٔ `ConfirmDialog` `inert` نمی‌شود و قفل اسکرول ندارد.

## شناسه‌های Git
- Branch: `agent/461-admin-ui-foundation`
- Commit: ثبت نشده (طبق دستور، Commit انجام نشد)
- Pull Request: ایجاد نشده
