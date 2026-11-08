# رابط مدیریت سیستم (ClientApp) — معماری، استانداردها و شکاف‌ها

- **Issue مرجع:** #444؛ برش‌های UI انجام‌شده در Issues #461–#464 و #475.
- **وضعیت سند:** snapshot کد ادغام‌شده در `main` پس از PR #478 (merge commit `4c84b3938b4438f93db907be0375d0c120202387`). PRهای #470، #471، #473، #474 و #478 ادغام شده‌اند؛ #469 به‌صورت مستقل ادغام نشد و زیرساخت آن در زنجیره PRهای بعدی وارد main شد.
- **حد ادعا:** تست‌های Vitest/TypeScript/Build و QA مرورگر با API mock طبق handoffها و CI PRها گزارش شده‌اند. این شواهد، E2E مدیریت نقش با Backend/SQL Server واقعی یا دسترس‌پذیری axe را اثبات نمی‌کنند. وضعیت پیاده‌سازی UI `Implemented` است؛ اعتبارسنجی یکپارچهٔ واقعی `NeedsVerification` باقی می‌ماند.
- منبع قواعد عمومی: `AGENTS.md` بخش ۲، `docs/UI_COMPONENTS.md` (D-048، D-049) و `docs/UI_LIBRARIES.md`. این سند آن‌ها را تکرار نمی‌کند و فقط الگوهای مدیریت سیستم را ثبت می‌کند.

## ۱) هدف و دامنه

- یکپارچه‌سازی صفحات مدیریت سیستم (کاربران و حساب من) روی یک مجموعه کامپوننت، روتر، قرارداد API و استاندارد فرم/RTL، تا صفحات بعدی (نقش‌ها، عضویت‌ها، دسترسی مالی) دوباره‌کاری نکنند.
- دامنهٔ کد: فقط `ClientApp/src` (React + TypeScript strict + Vite + Tailwind، D-011). بدون وابستگی npm جدید برای این صفحات.
- خارج از دامنه: هر API یا Schema جدید؛ این سند فقط نیازهای Backend را فهرست می‌کند (بخش ۱۰).

| صفحه | مسیر (زیر `/app`) | وضعیت |
|---|---|---|
| داشبورد | `/` | `Implemented` |
| فهرست کاربران | `/admin/users` | `Implemented` |
| ایجاد کاربر | `/admin/users/new` | `Implemented` |
| جزئیات کاربر (تب‌های اطلاعات عمومی، تماس، شرکت‌ها و عضویت‌ها) | `/admin/users/:id` | `Implemented` |
| ویرایش کاربر | `/admin/users/:id/edit` | `Implemented` |
| حساب من (drawer با تب‌های مشخصات، امنیت/رمز، سوابق ورود) | بدون مسیر؛ پنل داخل `AppShell` | `Implemented` |
| نقش‌ها (`/admin/roles`، ایجاد، ویرایش، کپی، مقایسهٔ دو گروه) و کاتالوگ مجوز (`/admin/permissions`) | صفحات lazy با `PermissionTree`؛ قراردادهای API در PR #459 ادغام شده‌اند؛ کپی (prefill از گروه مبدأ با حذف کدهای خارج از کاتالوگ کاربر) و مقایسهٔ دو گروه از 2026-11-08 `Implemented` است (مرحله دوم #444) | `Implemented`؛ تست UI و CI موفق گزارش شده، اما E2E این صفحات با Backend/SQL Server واقعی هنوز اثبات نشده است |
| سوابق امنیتی، سیاست‌ها، ساختار سازمانی | فقط آیتم غیرفعال منو | `NotImplemented` |

## ۲) معماری صفحات مدیریت سیستم

- **روتر:** `lib/router.tsx`، روتر کوچک مبتنی بر History API زیر `/app` (بخش ۹). مسیرها آرایهٔ `Route` در `components/AppRoutes.tsx` هستند (`path`، `element`، `title`، `permission?`). اولین تطابق برنده است؛ `/admin/users/new` باید پیش از `/admin/users/:id` بیاید.
- **جای `RouterProvider`:** داخل `AppShell` (نه `main.tsx`)؛ `AppShell` با props اختیاری `routes` و `canAccess` آن را می‌سازد. `main.tsx` سوئیچ صفحات `/enroll` و `/provisioning/import` را بدون روتر نگه می‌دارد. اگر صفحه‌ای خارج از پوسته مسیر بخواهد، Provider باید بالاتر برود.
- **Lazy:** چهار صفحهٔ کاربران با `React.lazy` و `Suspense` (با `LoadingState`) بارگذاری می‌شوند؛ داشبورد عمداً eager است. `scripts/offline-check.mjs` اکنون chunkهای lazy را درست resolve می‌کند.
- **داده‌های مسیر:** `AppRouteDataContext` (`session`، `context`، `onUnauthenticated`) را `App` یک‌بار می‌دهد. شیء در هر رندر `App` جدید است (memo نشده).
- **رفتار روتر:** `Link` فقط مسیر داخلی تک‌اسلشی را مدیریت می‌کند (`https://`، `//host`، `mailto:` به مرورگر واگذار می‌شود)؛ `navigate` به URL یکسان `replace` می‌کند؛ پس از تغییر `pathname` (نه تغییر فقط query) focus به `h1` می‌رود؛ عنوان تب `<title> | فرانو` است و `useDocumentTitle` آن را برای دادهٔ پویا اصلاح می‌کند؛ `useSearchParams` حالت فهرست را در URL نگه می‌دارد.
- **نگاشت route به permission (راهنمای UI):**

| مسیر | `Route.permission` | تلمیح فعلی `can()` |
|---|---|---|
| `/admin/users` | `admin.users.view` | `session.isAdministrator` |
| `/admin/users/new` | `admin.users.create` | `session.isAdministrator` |
| `/admin/users/:id` | `admin.users.view` | `session.isAdministrator` |
| `/admin/users/:id/edit` | `admin.users.update` | `session.isAdministrator` |
| `/` و حساب من (`account.view`) | بدون / همیشه true | همه کاربران وارد‌شده |

- **سرور مرجع نهایی است (D-064، D-065):** `lib/permissions.ts` فقط برای پنهان یا غیرفعال‌کردن کنترل است. `can()` برای هر `admin.*` فقط `session.isAdministrator` را می‌خواند؛ این تفکیک مدیر سراسری نصب از مجوز شرکتی را بیان نمی‌کند. کاربر غیرمدیر در deep link «دسترسی ندارید» می‌بیند و درخواستی به `/admin/users` نمی‌رود (تست jsdom). مسیر `GET /me/permissions` هنوز پیاده‌سازی نشده است؛ بنابراین `can()` همچنان فقط راهنمای UX مبتنی بر `session.isAdministrator` است و مجوزدهی قطعی فقط در API انجام می‌شود.

## ۳) کامپوننت‌های مشترک

**قاعده:** پیش از ساخت هر کامپوننت جدید، `components/ui/`، `components/admin/users/`، `components/account/` و `components/Feedback.tsx` را جست‌وجو کن؛ فقط اگر موجود نبود و کتابخانهٔ shadcn/Base UI پروژه آن را پوشش نمی‌داد بساز. وابستگی یا کتابخانهٔ UI دوم بدون توجیه، تأیید مالک و ADR ممنوع است (`UI_COMPONENTS.md`).

| کامپوننت | مسئولیت | فایل (زیر `ClientApp/src/components`) |
|---|---|---|
| `FormField` + `Input`/`Select`/`Textarea` | برچسب، `required`، راهنما، خطا، اتصال `aria-describedby`/`aria-invalid`؛ فقط یک فرزند | `ui/FormField.tsx` |
| `FormSection` | گروه‌بندی فیلدها با عنوان و توضیح (از `h3` استفاده می‌کند) | `ui/FormSection.tsx` |
| `ConfirmDialog` | تأیید با focus trap، Escape، بازگشت focus، `busy` با `aria-disabled`، `tone='danger'` | `ui/ConfirmDialog.tsx` |
| `Tabs` | WAI-ARIA، جهت فلش‌ها در RTL، تب `disabled` | `ui/Tabs.tsx` |
| `StatusBadge` | وضعیت `active/inactive/pending/rejected/readonly` با آیکون و متن | `ui/StatusBadge.tsx` (بر پایهٔ `ui/Badge.tsx`) |
| `DataTable` | جدول نمایشی و سرور-محور: مرتب‌سازی، اسکلت بارگذاری، حالت خالی/خطا/تلاش دوباره، صفحه‌بندی، `rowHref` | `ui/DataTable.tsx` |
| `Breadcrumb` | مسیر صفحه؛ آخرین مورد بدون `to` | `ui/Breadcrumb.tsx` |
| `AuditTimeline` | رویدادهای زمانی با آیکون + متن، `<time dateTime>` جلالی، IP با `dir=ltr` | `ui/AuditTimeline.tsx` |
| `Alert`، `Badge`، `Button`، `Card` | پایه‌های بصری | `ui/Alert.tsx`، `ui/Badge.tsx`، `ui/button.tsx`، `ui/card.tsx` |
| `LoadingState`، `ErrorState`، `EmptyState` | حالت‌های بارگذاری (`role=status`)، خطا (`role=alert`)، خالی | `Feedback.tsx` |
| `UserForm` | فرم مشترک ایجاد/ویرایش کاربر (`mode='create'|'edit'`) | `admin/users/UserFormFields.tsx` |
| `PasswordInput` (حساب من) | ورودی رمز، `dir=ltr`، نمایش/پنهان با `aria-pressed` | `account/PasswordInput.tsx` |
| `userFlash` | پیام موفقیت یک‌بارمصرف `{userId, kind}` در حافظهٔ ماژول، TTL ۳۰ ثانیه | `admin/users/userFlash.ts` |

**موجود نیستند (`NotImplemented`)** و تا نیاز واقعی و API مربوط ساخته نمی‌شوند: `UserSelector`، `FiscalYearSelector`، `FiscalPeriodSelector` (`PermissionTree` در `ui/PermissionTree.tsx` و انتخاب‌گر شرکت نقش‌ها در `admin/roles/CompanySelect.tsx` اکنون `Implemented`). برای انتخاب شرکت/دوره الگوی موجود `WorkContextSelector` است و نباید موازی‌اش ساخته شود مگر با بازبینی.

**تکرار شناخته‌شده:** `PasswordInput` در `account/` و یک `PasswordInput` محلی در `UserFormFields.tsx` تکراری‌اند و قرار است به یک `ui/PasswordInput` ادغام شوند (بخش ۱۱).

## ۴) قرارداد API client (`api/client.ts`)

- همه چیز هم‌مبدأ و زیر `/api/v1` با Cookie (`credentials: 'same-origin'`). توکن نشست هرگز در JavaScript دیده یا در `localStorage` ذخیره نمی‌شود (D-012).
- **Anti-forgery:** `sendJson(method, path, body?)` با `method` در `POST|PUT|PATCH|DELETE` پیش از هر درخواست تغییردهنده `GET /antiforgery/token` می‌زند و هدر را برمی‌گرداند. توکن عمداً کش نمی‌شود (وابسته به هویت؛ کش بیات پس از ورود/خروج ناامن است). `Content-Type: application/json` فقط وقتی بدنه هست.
- **توابع:** `getAdminUsers`، `getAdminUser`، `createAdminUser` (POST، پاسخ `{id}`)، `updateAdminUser` (PUT، 204)، `getMyProfile`، `getMyLoginHistory`، `changeMyPassword`، `getSession`، `getCompanies`، `getCompanyOverview`، `getWorkContext`، `selectWorkContext`، `setDefaultCompany`، `logout`، `getCompanyRoles`، `createCompanyRole`، `updateCompanyRole`، `getPermissionCatalog` و `getPermissionTemplates`. هر پاسخ با `parse*` در `api/types.ts` اعتبارسنجی می‌شود.
- **`apiPostJson`/`apiGetRaw`:** فقط برای دو صفحهٔ Provisioning (بدون نشست)؛ برای وضعیت ناموفق پرتاب نمی‌کنند. صفحات مدیریت از آن‌ها استفاده نکنند.
- **خطاها:**

| وضعیت/شکل | رفتار کلاینت |
|---|---|
| 401 | `UnauthenticatedError` ← فراخواننده `onUnauthenticated` را صدا می‌زند (انتقال به `buildLoginUrl`) |
| ProblemDetails (`title`، `errors`) | `ApiError.message = title`؛ اگر `errors` هست و `title` همان پیام پیش‌فرض انگلیسی ASP.NET است، پیام فارسی وضعیت |
| `{ error: string }` | `ApiError.message = error` |
| `errors` | `ApiError.fieldErrors` با کلید اول‌حرف‌کوچک (`Email` ← `email`)؛ مقدار تکی یا آرایه پذیرفته می‌شود |
| 5xx | هرگز متن بدنه نمایش داده نمی‌شود؛ پیام ثابت فارسی |
| `detail` | هرگز نمایش داده نمی‌شود |
| 403/404/سایر | پیام فارسی وضعیت اگر بدنه پیام نداشت |

- **PUT/PATCH:** `updateAdminUser` فقط PUT دارد. توکن همزمانی (ETag/rowversion) نیست؛ دو مدیر هم‌زمان: آخرین نوشتن برنده است. بدنهٔ درخواست فیلد‌به‌فیلد ساخته می‌شود (بدون spread).
- **قالب‌های ناسازگار بک‌اند که نیاز به هماهنگی دارند (`Proposed`):**
  - دو شکل خطا (ProblemDetails و `{error}`) هم‌زیستی دارند؛ کلاینت هر دو را می‌فهمد ولی قرارداد واحد سمت سرور تعیین نشده.
  - کلید `errors` گاهی با پیشوند می‌آید (`$.newPassword`، `request.x`)؛ فقط `SecurityTab` پیشوند را می‌زداید. نگاشت باید یکسان و سمت سرور ثابت شود.
  - 409 تکراری نمی‌گوید کدام فیلد (نام کاربری/ایمیل/موبایل) تکراری است.
  - پیام `NotCommonPassword` از «رمز کوتاه» قابل‌تفکیک نیست.
  - صفحه‌بندی فقط در فهرست کاربران است؛ `memberships` در جزئیات کاربر صفحه‌بندی ندارد.
  - `POST /context` و `/context/default-company` از `sendJson` (با توکن) فراخوانی می‌شوند، ولی الزام anti-forgery سمت سرور برای `/context` در این بررسی از کد سرور راستی‌آزمایی نشد (بخش ۱۰).

## ۵) استاندارد فرم‌ها

- **اعتبارسنجی:** قواعد کلاینت فقط راحتی کاربر است و با سرور هم‌خوان نگه داشته می‌شود (`userFormValidation.ts`: نام کاربری ۳ تا ۲۰؛ نام/نام‌خانوادگی تا ۵۰؛ ایمیل الزامی تا ۲۵۴ با قالب `x@y`؛ موبایل اختیاری `09xxxxxxxxx` با ارقام لاتین؛ رمز ۸ تا ۱۲۸ و تکرار برابر). متن‌ها با `normalizeText` (trim و جمع‌کردن فاصلهٔ تکراری، مثل سرور) نرمال می‌شوند؛ رمزها trim نمی‌شوند. چیزی که سرور می‌پذیرد در کلاینت رد نشود. فهرست رمزهای رایج در کلاینت تکرار نمی‌شود. فرم `noValidate` است تا پیام‌های فارسی خودمان نمایش داده شوند.
- **خطای فیلد:** پس از blur یا تلاش برای ارسال؛ با `FormField error`. `fieldErrors` سرور به فیلدهای شناخته‌شده نگاشت و با ویرایش همان فیلد پاک می‌شود؛ کلیدهای ناشناخته و خطاهای عمومی در `Alert`.
- **Focus:** پس از ارسال ناموفق، focus به اولین فیلد نامعتبر (ترتیب DOM) و در نبود آن به `Alert`. پس از تغییر مسیر، focus به `h1`.
- **Dirty guard:** «انصراف» روی فرم تغییرکرده `ConfirmDialog` (پیش‌فرض: ادامهٔ ویرایش) باز می‌کند و `beforeunload` فقط در حالت dirty ثبت می‌شود. dirty بر پایهٔ مقدار نرمال‌شده است. **محدودیت:** کلیک روی لینک‌های داخلی (منو، Breadcrumb) و دکمهٔ Back مرورگر بی‌هشدار ورودی‌ها را رها می‌کند (روتر blocker ندارد) — بخش ۱۱.
- **Double submit:** گارد `useRef` (دو کلیک/Enter پشت‌سرهم) + دکمهٔ `disabled` و `aria-busy` با برچسب «در حال ذخیره…». در حساب من تا پایان درخواست تب‌های دیگر `disabled` می‌مانند. `ConfirmDialog` در حالت `busy` focus را در خود نگه می‌دارد و rejectِ `onConfirm` قفل را آزاد می‌کند.
- **ویرایش:** دکمهٔ ذخیره تا وقتی تغییری نیست غیرفعال است؛ غیرفعال‌کردن کاربر (true به false) تأیید می‌خواهد، فعال‌کردن نه. سرور حساب خود/آخرین مدیر را رد می‌کند و پیام فارسی سرور در `Alert` می‌آید.
- **رمز:** فقط در state محلی فرم و بدنهٔ POST؛ نه در URL/storage/Log. `autocomplete=new-password`، `spellCheck=false`، `autoCapitalize=none`، `autoCorrect=off`، `dir=ltr`، نمایش/پنهان با `aria-pressed`. پس از خطای غیر 401 رمز فعلی پاک می‌شود؛ در حساب من فرم با رفتن به تب دیگر unmount می‌شود. پیام موفقیت فقط `{userId, kind}` دارد (`userFlash`)، هرگز رمز یا داده.

## ۶) استاندارد RTL

- فقط ویژگی‌های منطقی: `ms-`، `me-`، `ps-`، `pe-`، `start-`، `end-`، `text-start`، `border-s/e`؛ `left/right/ml/mr` در کد جدید ممنوع.
- شناسه‌ها و مقدارهای لاتین (نام کاربری، ایمیل، موبایل، IP، شناسه، رمز) با `dir="ltr"` یا `<bdi>`. در `PasswordInput` ریشه `dir=ltr` است تا دکمهٔ نمایش و padding هم‌سو بمانند.
- آیکون‌های جهت‌دار آینه یا متناسب با RTL انتخاب شوند: در کد فعلی `Breadcrumb` از `ChevronLeft` و صفحه‌بندی `DataTable` از `ChevronRight` (قبلی) و `ChevronLeft` (بعدی) استفاده می‌کنند؛ درستی بصری آن‌ها در مرورگر واقعی بررسی نشده.
- تاریخ فقط با `lib/format.ts` (`formatDate`، `formatDateTime`، `notRecorded`)؛ خروجی جلالی با ارقام فارسی مثل `۱۴۰۵/۰۸/۱۱ ۱۱:۳۰`. داده روی سیم ISO می‌ماند. تعریف تکراری ممنوع.
- رنگ تنها معنا نرساند: وضعیت‌ها (`StatusBadge`، `AuditTimeline`) آیکون + متن دارند.
- **چک‌لیست هر صفحهٔ جدید:** (۱) هیچ `left/right` فیزیکی نیست؛ (۲) داده‌های لاتین `ltr` هستند؛ (۳) آیکون جهت‌دار درست است؛ (۴) تاریخ از `format.ts`؛ (۵) مسیر کامل صفحه‌کلید و focus قابل‌دیدن؛ (۶) هر حالت صفحه یک `h1` دارد؛ (۷) بررسی در ۳۶۰، ۷۶۸ و ۱۲۸۰ پیکسل؛ (۸) `npm run check:offline` بدون منبع بیرونی. موارد ۵ و ۷ در کار فعلی **اجرا نشده**.

## ۷) ساختار Navigation

- منوی «مدیریت سیستم» (فقط وقتی `session.isAdministrator`) شامل لینک‌های واقعی «مدیریت کاربران» (`/admin/users`)، «گروه‌های دسترسی» (`/admin/roles`) و «کاتالوگ و جزئیات مجوزها» (`/admin/permissions`) است. سه آیتم «واحدهای سازمانی»، «سمت‌های سازمانی» و «کارکنان» (`/hr/org/*`) با کدهای company `hr.*.list` از `/me/permissions` نمایش داده می‌شوند و فقط «سیاست‌های امنیتی» همچنان `DisabledItem` است.
- **قاعده: لینک یا تب به صفحه/قابلیتی که API آن وجود ندارد ممنوع است.** آیتم تا وقتی مسیر واقعی و API و مجوز سرور نیست غیرفعال می‌ماند. تب‌های نقش‌ها، سال مالی، سوابق ورود و Audit در جزئیات کاربر عمداً ساخته نشده‌اند. فیلدهای بدون پشتوانهٔ API (کد ملی، اجبار تغییر رمز، یادداشت، حذف کاربر، تغییر رمز کاربر دیگر) در فرم‌ها نیامده‌اند.
- پس از ورود یک قابلیت جدید: مسیر در `AppRoutes.tsx`، آیتم منو در `AppShell.tsx`، `Permission` در `lib/permissions.ts`، تست مسیر در `AppRouting.test.tsx`.

## ۸) راهنمای اجرای تست‌های UI

```bash
cd ClientApp
npm ci
npm run typecheck
npm test                 # vitest run
npm run build            # شامل typecheck
npm run check:offline    # اسکن ایستا؛ آدرس ساخته‌شده در زمان اجرا را نمی‌بیند
```

- **الگوی mock:** `src/test/api.ts` تابع `stubApi({ 'METHOD /api/v1/path': {status, body} | handler })` را دارد؛ مسیر ناشناخته 404 می‌دهد تا فراخوانی ناخواسته تست را بشکند. `antiforgeryRoute` و دادهٔ نمونه نیز آنجاست. دادهٔ کاربران مدیریتی در `src/test/adminUsers.ts`. تست‌ها کنار کامپوننت (`*.test.tsx`) و با jsdom.
- نتایج هر برش در handoffهای همان Issue ثبت شده است. برای B1 (#475/#478): typecheck، ۴۰۸ تست، build، check:offline و ۳۶/۳۶ سناریوی Playwright در ۳ اندازه viewport موفق گزارش شده‌اند؛ API مرورگر در آن QA mock بوده است.
- CI نهایی PR #478 در توضیح PR موفق گزارش شده است (build/test، Client، E2E عمومی، secret scan و publish jobs). E2E عمومی CI را نباید با آزمون اختصاصی گردش نقش/مجوز روی SQL Server واقعی یکی دانست.
- **هنوز نیازمند تأیید:** سناریوی end-to-end نقش‌ها/مجوزها روی سرور واقعی و SQL Server؛ اجرای axe/صفحه‌خوان؛ دسترسی مدیر شرکتی و سقف تفویض؛ فهرست اعضای نقش و انتقال اعضا؛ آزمون هم‌زمانی دو ویرایشگر.

## ۹) گزارش تصمیمات طراحی

| تصمیم | دلیل | وضعیت و بازبینی |
|---|---|---|
| روتر داخلی بدون وابستگی | جلوگیری از وابستگی بدون ADR (D-011)؛ نیاز فعلی ساده | `Implemented`؛ اگر layout تو در تو، loader یا blocker لازم شد بازبینی شود |
| `DataTable` بدون TanStack | `UI_LIBRARIES.md` (D-049/D-050) TanStack Table را برای جدول رسمی کرده؛ برای نیاز فعلی (مرتب‌سازی/صفحه‌بندی سرور-محور) پیاده‌سازی سبک کافی بود و وابستگی نیامد | `Implemented`؛ انحراف از جهت کتابخانه‌ای مصوب؛ ADR مستقل ثبت نشده؛ برای ستون‌بندی/virtual/انتخاب سطر باید بازبینی شود |
| `formatDate` واحد در `lib/format.ts` | حذف دو پیاده‌سازی تکراری؛ قالب «۱۴۰۵/۰۸/۱۱» برای یکپارچگی | `Implemented` |
| نگهبان dirty فقط Cancel و `beforeunload` | روتر blocker ندارد | `Implemented` جزئی؛ blocker روتر `NotImplemented` |
| `q` (جست‌وجو) در URL | Back/Refresh/اشتراک‌گذاری؛ می‌تواند ایمیل/موبایل باشد | ریسک پذیرفته‌شده (security-reviewer، سطح Low): صفحه فقط مدیر، `Referrer-Policy: strict-origin-when-cross-origin`؛ اگر ثبت Query String در لاگ درخواست‌ها فعال شد بازبینی شود |
| ضد جعل بدون کش توکن | وابستگی توکن به هویت | `Implemented` |
| پیام موفقیت در حافظهٔ ماژول | بدون storage/URL؛ با رفرش از بین می‌رود | `Implemented` |
| `can()` فقط `isAdministrator` | `GET /me/permissions` نیست | تلمیح موقت؛ سرور مرجع (D-064) |

## ۱۰) APIهای مورد نیاز از Backend

این جدول وضعیت فعلی را بر اساس کد main و PRهای ادغام‌شده ثبت می‌کند؛ موارد `NotImplemented` به معنی قابلیت موجود نبودن است، نه صرفاً نبود UI.

| نیاز | وضعیت | وابستگی / اثر بر UI |
|---|---|---|
| `GET /me/permissions` | `NotImplemented` | جایگزین `can()` مبتنی بر `isAdministrator`؛ پیش‌نیاز تفکیک واقعی مدیر نصب و مجوز شرکتی (D-064) |
| آمار داشبورد مدیریت (تعداد کاربران فعال/غیرفعال، رویداد اخیر) | `NotImplemented` | بدون آن داشبورد مدیریت ساخته نمی‌شود |
| نقش‌ها و کاتالوگ مجوز | API در #459 و UI در #478 ادغام شده‌اند | فهرست همه شرکت‌ها، اعضای نقش/انتقال اعضا (API در #482 و UI در بخش «اعضای این گروه» صفحهٔ جزئیات گروه `Implemented`؛ جستجوی اعضا سمت سرور وجود ندارد)، GET تک‌نقش، جستجوی معکوس و پشتیبانی مدیر شرکتی/Delegation هنوز موجود نیست؛ سقف تفویض باید پیش از فعال‌سازی مدیران غیرسراسری طراحی و enforce شود |
| عضویت‌ها و جریان تأیید (Pending/Approved/Rejected، self-approval ممنوع) | `Verified` روی کد main (e1529718): API کشف «`GET /me/membership-requests/destinations`» (جستجوی exact اجباری ۳..۱۲۰ نویسه، فقط شرکت‌های فعال مرز مشتری مشترک، حذف عضو فعال و درخواست Pending — #510)، فرم «ثبت درخواست عضویت» (`CreateMembershipRequest` در صفحهٔ «درخواست‌های عضویت من») و «`GET .../assignable-roles`» برای تأییدکنندهٔ غیر مدیر نصب (فقط نقش‌های داخل سقف تفویض و بدون مجوز تأیید — PR #550) موجودند؛ UI سه صفحهٔ «درخواست‌های عضویت من» (`/account/membership-requests`)، «صف تأیید مقصد» (`/admin/membership-requests`) و فرم ثبت `Implemented`. تست: ۲۱ تست endpoint + ۶ تست Bootstrap (محلی سبز)، ۳ تست Integration با SQL Server و E2E مثبت/منفی گردش (PR #536) در CI سبز روی main؛ ۲۰ تست کلاینت. **باقی‌مانده:** axe/صفحه‌خوان اجرا نشده؛ انقضای درخواست Pending و لغو/تعلیق عضویت `NotImplemented` (تصمیم محصولی) | D-065؛ قرارداد مرجع: `docs/api/USER_ACCESS_MANAGEMENT_API.md` بخش ۵؛ گزارش ممیزی: `docs/handoffs/MEMBERSHIP_REQUEST_WORKFLOW_COMPLETION.md` |
| دفترچهٔ کاربران شرکت (`/company/users`) | API در #497 (`GET /companies/{id}/users`، مجوز `users.list`) ادغام شده؛ UI `Implemented`: فهرست فقط‌خواندنی شرکت جاری، جستجو، فیلتر وضعیت عضویت، جزئیات، صفحه‌بندی؛ 403/404 با پیام مشخص | واحد/سمت (S2)، مجوزهای دقیق گروه برای غیرمدیر، منو بر اساس مجوز واقعی (`GET /me/permissions`) و ایجاد/ویرایش/مدیریت جداگانه `NotImplemented`؛ ورودی منو فعلاً فقط برای مدیر نصب |
| grantهای سال/دوره مالی (`AllPeriodsInYear`) | `NotImplemented` | D-065؛ `FiscalYearSelector`/`FiscalPeriodSelector` وابسته |
| دایرکتوری read-only هم‌گروه | API در PR #477 ادغام شده؛ UI ساخته نشده (`Blocked`) | API فهرست گروه‌های قابل مشاهده وجود ندارد و UI نمی‌تواند `groupId` را پیدا کند؛ تا افزودن API فهرست گروه‌ها UI ساخته نشود |
| رویدادهای امنیتی/Audit مدیریتی | API در #487 ادغام شده؛ UI «گزارش امنیت و سوابق» (`/admin/security/audit`) `Implemented`: فهرست، صفحه‌بندی، فیلتر نوع رویداد، بازهٔ تاریخ شمسی (`JalaliDateField`، مرز روز در منطقهٔ کاربر)، کاربر (`UserPicker` روی `GET /admin/users?search=`) و شرکت (فقط شرکت‌های عضویت کاربر + کلیک روی جدول). **باقی‌مانده/مسدود به API:** مرتب‌سازی (API پارامتر ندارد)، فهرست همهٔ شرکت‌های نصب، فهرست انواع رویداد، ورود/خروج سراسری، نشست‌ها، export، retention | قرارداد پیشنهادی در `docs/handoffs/ISSUE-444-security-audit-complete.md` |
| یکسان‌سازی قالب خطا و صفحه‌بندی | `Proposed` | بخش ۴؛ حذف کد تطبیق در کلاینت |
| فیلتر شرکت/نقش، شمارهٔ پرسنلی، خروجی فهرست کاربران | `NotImplemented` | UI ساخته نشد |
| توکن همزمانی در PUT کاربر | `NotImplemented` | جلوگیری از last-write-wins |
| تأیید anti-forgery روی `/context` | `NeedsApproval` برای بررسی | کلاینت توکن می‌فرستد؛ الزام سمت سرور باید تأیید شود |

## ۱۱) بدهی فنی باقی‌مانده

1. ادغام دو `PasswordInput` در `ui/PasswordInput`.
2. blocker ناوبری در روتر برای فرم dirty (لینک‌های داخلی و Back).
3. `FormSection` با `h3` است و صفحه `h2` ندارد (پرش heading از h1 به h3).
4. `ConfirmDialog` و drawer حساب من: پس‌زمینه `inert` نمی‌شود، قفل اسکرول ندارند؛ focus trap دستی است؛ Escape در drawer فقط با focus داخل آن کار می‌کند.
5. `DataTable`: `sticky` سربرگ زیر `md` درون ظرف اسکرول افقی نسبت به viewport کار نمی‌کند؛ فقط ستون اول لینک واقعی است (انتخاب متن ردیف دشوار)؛ همپوشانی stretched link بررسی نشده.
6. `Tabs`: `aria-controls` به پنل‌های رندرنشده اشاره می‌کند؛ نوار تب sticky نیست.
7. `router.tsx` از `components/Feedback` import می‌کند (وابستگی lib به components).
8. `AppRouteDataContext` memo نشده؛ اگر `onUnauthenticated` در `AppShell` پایدار نباشد، درخواست‌های پروفایل/سوابق با هر رندر تکرار می‌شوند.
9. پس از ویرایش خود مدیر، نام نمایش‌داده‌شده در پوسته تا بارگذاری دوبارهٔ نشست به‌روز نمی‌شود.
10. یادداشت‌های Low مربوط به `security-reviewer` در #463 ثبت متنی نشده بود؛ Lead باید در handoff آن مورد را تکمیل کند.
11. E2E اختصاصی مدیریت کاربران/نقش‌ها با API واقعی و SQL Server، axe و صفحه‌خوان (بخش ۸).

## مجوز شرکت جاری و درخواست عضویت (#444)
منوی دفترچه و صف بررسی با `GET /api/v1/me/permissions` برای زمینهٔ کاری فعلی نمایش داده می‌شود؛ وضعیت خطا/بارگذاری مجوز شرکتی را fail-closed می‌کند. این منو مرجع authorization نیست. صفحهٔ درخواست‌های من نام دقیق مقصد را از API جستجو و درخواست را با Anti-forgery ثبت می‌کند. صفحهٔ تأیید فقط نقش‌های صفحه‌بندی‌شدهٔ `assignable-roles` را پیشنهاد می‌دهد؛ POST سرور مجوز و سقف تفویض را دوباره بررسی می‌کند. E2E مثبت گردش به fixture مرز مشتری و تأییدکننده نیاز دارد.
