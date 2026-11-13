# کاتالوگ منو و فرم فرانو (Menu & Form Catalog)

- **Issue:** [#444](https://github.com/hamidreza1394/Accounting/issues/444) — Stage 2-B مأموریت تکمیلی (2026-11-08)
- **وضعیت:** استخراج‌شده از دو رجیستری واقعی کد: آرایهٔ `navigation` در `ClientApp/src/components/shell/AppSidebar.tsx` (منو) و `appRoutes` در `ClientApp/src/components/AppRoutes.tsx` + `features/hr/hrRoutes.tsx` (فرم‌ها).
- **مدل مفهومی:** [D-075](DECISIONS.md) — زیرسیستم/منو/فرم/عملیات چهار مفهوم مستقل‌اند؛ نگاشت هر آیتم به زیرسیستم در [SUBSYSTEM_CATALOG.md](SUBSYSTEM_CATALOG.md).
- **گارد ماشینی:** `ClientApp/src/components/shell/menuRouteConsistency.test.tsx` رفتارهای حیاتی این کاتالوگ را قفل می‌کند (بخش ۴).

## 1. درخت منو (ترتیب نمایش واقعی از بالا به پایین)

گروه‌های دارای صفحهٔ واقعی پیش‌فرض باز هستند (`openByDefault`)؛ زیرسیستم‌های آینده جمع می‌مانند و فقط نشان «به‌زودی» دارند — بدون هیچ مسیر جعلی.

| # | گروه / آیتم | نوع | مسیر | گیت نمایش (hint؛ سرور مرجع است) | زیرسیستم |
|---|---|---|---|---|---|
| ۱ | داشبورد | آیتم بالا | `/` | همیشه | core |
| ۲ | **حسابداری و مالی** | گروه | — | — | — |
| ۲.۱ | حسابداری | آیتم | به‌زودی | همیشه (غیرفعال) | finance |
| ۲.۲ | خزانه‌داری | آیتم | به‌زودی | همیشه (غیرفعال) | treasury |
| ۳ | **فروش، بازرگانی و انبار** | گروه | — | — | — |
| ۳.۱ | فروش | آیتم | به‌زودی | همیشه (غیرفعال) | sales |
| ۳.۲ | بازرگانی خارجی (در کد: گروه مستقل «بازرگانی خارجی» با یک فرزند placeholder «واردات و صادرات»؛ طبق D-081 آیتم فعال فقط با Route واقعی می‌آید: S2 ← `/trade/parties`، `/trade/products`) | آیتم | به‌زودی | همیشه (غیرفعال) | TRADE_IMPORT |
| ۳.۳ | انبار | آیتم | به‌زودی | همیشه (غیرفعال) | inventory |
| ۴ | **گزارش‌ها و اطلاعات پایه** | گروه | — | — | — |
| ۴.۱ | گزارش‌ها | آیتم | به‌زودی | همیشه (غیرفعال) | reporting |
| ۴.۲ | اطلاعات پایه | آیتم | به‌زودی | همیشه (غیرفعال) | reporting |
| ۵ | **منابع انسانی** (باز) | گروه | — | گروه در نبود همهٔ گیت‌ها پنهان می‌شود | hr |
| ۵.۱ | واحدهای سازمانی | آیتم | `/hr/org/units` | `hr.units.view` | hr |
| ۵.۲ | سمت‌های سازمانی | آیتم | `/hr/org/positions` | `hr.positions.view` | hr |
| ۵.۳ | کارکنان | آیتم | `/hr/org/employees` | `hr.employees.view` | hr |
| ۶ | **مدیریت سیستم** (باز) | گروه | — | — | — |
| ۶.۱ | مدیریت کاربران | آیتم | `/admin/users` | `admin.users.view` | identity-admin |
| ۶.۲ | کاربران شرکت جاری | آیتم | `/company/users` | `users.list` (کد شرکت) | membership |
| ۶.۳ | گروه‌های دسترسی | آیتم | `/admin/roles` | `admin.roles.view` | access-admin |
| ۶.۴ | کاتالوگ و جزئیات مجوزها | آیتم | `/admin/permissions` | `admin.permissions.view` | access-admin |
| ۶.۵ | درخواست‌های عضویت | آیتم | `/admin/membership-requests` | `users.membership.approve` | membership |
| ۶.۶ | درخواست‌های عضویت من | آیتم | `/account/membership-requests` | همیشه | membership |
| ۶.۷ | گزارش امنیت و سوابق | آیتم | `/admin/security/audit` | `admin.security.view` | security-audit |
| ۶.۸ | سیاست‌های امنیتی | آیتم | به‌زودی | همیشه (غیرفعال) | security-audit |
| ۷ | راهنما و پشتیبانی | آیتم بالا | به‌زودی | همیشه (غیرفعال) | support |

صفحات خارج از شل (خارج از این منو): `/enroll` و `/provisioning/import` (صفحات first-run لایسنس) و `/Account/Login` (صفحهٔ Razor ورود).

> یادداشت (PR سایدبار): زیر «مدیریت سیستم» بخش‌های تاشوی «دسترسی و مجوزها»، «ساختار سازمانی» (واحدهای سازمانی، سمت‌های سازمانی، کارکنان؛ نام قبلی «ساختار سازمانی و کارکنان»)، «شرکت‌ها و عضویت‌ها» و «سوابق امنیتی و مدیریتی» وجود دارد؛ ترتیب و آیکون‌ها در `navigation` کد مرجع است و این جدول هنوز ساختار قدیمی را نشان می‌دهد.

> یادداشت (مرحلهٔ صفر Trade، 2026-11-13): مقایسه با `AppSidebar.tsx` نشان می‌دهد گروه‌بندی کد (گروه‌های «حسابداری»، «انبار»، «فروش»، «خزانه‌داری»، «حقوق و دستمزد»، «دارایی ثابت»، «بازرگانی خارجی»، «عمومی»، «مدیریت سیستم») با گروه‌های ۲ تا ۵ این جدول یکی نیست؛ فقط ردیف Trade (۳.۲) در این دور اصلاح شد و هم‌ترازی بقیهٔ جدول به مالک این سند واگذار است.

## 2. کاتالوگ فرم‌ها (همهٔ Routeهای ثبت‌شده)

«Parent» محل ورود کاربر به فرم است: منو یا صفحهٔ والد. همهٔ مسیرها lazy هستند و عنوان فارسی `<title>` را می‌سازند.

| مسیر | عنوان فارسی | زیرسیستم | گیت hint | Parent | یادداشت |
|---|---|---|---|---|---|
| `/` | داشبورد | core | — | منو (داشبورد) | — |
| `/admin/users` | مدیریت کاربران | identity-admin | `admin.users.view` | منو | فهرست |
| `/admin/users/new` | ایجاد کاربر | identity-admin | `admin.users.create` | فرم فهرست کاربران | پیش از `:id` ثبت شده |
| `/admin/users/:id` | جزئیات کاربر | identity-admin | `admin.users.view` | فرم فهرست کاربران | بازگشت فیلترها حفظ می‌شود |
| `/admin/users/:id/edit` | ویرایش کاربر | identity-admin | `admin.users.update` | فرم جزئیات | — |
| `/admin/permissions` | کاتالوگ مجوزها | access-admin | `admin.permissions.view` | منو | درخت زیرسیستم→منبع→عملیات |
| `/admin/roles` | گروه‌های دسترسی | access-admin | `admin.roles.view` | منو | — |
| `/admin/roles/new` | ایجاد گروه دسترسی | access-admin | `admin.roles.create` | فرم گروه‌های دسترسی | — |
| `/admin/roles/:roleId` | جزئیات گروه دسترسی | access-admin | `admin.roles.view` | فرم گروه‌های دسترسی | اعضا و انتقال |
| `/company/users` | کاربران شرکت | membership | `users.list` (سرور) | منو | — |
| `/admin/security/audit` | گزارش امنیت و سوابق | security-audit | `admin.security.view` | منو | فیلتر تاریخ شمسی/کاربر/شرکت |
| `/admin/membership-requests` | درخواست‌های عضویت | membership | `users.membership.approve` (سرور) | منو | صف تأیید |
| `/account/membership-requests` | درخواست‌های عضویت من | membership | همیشه | منو | — |
| `/hr/org/units` | واحدهای سازمانی | hr | `hr.units.view` | منو | — |
| `/hr/org/units/new` | ایجاد واحد سازمانی | hr | `hr.units.view` | فرم واحدها | — |
| `/hr/org/units/:unitId` | جزئیات واحد سازمانی | hr | `hr.units.view` | فرم واحدها | ویرایش درون‌صفحه‌ای + زیردرخت + کارکنان |
| `/hr/org/positions` | سمت‌های سازمانی | hr | `hr.positions.view` | منو | — |
| `/hr/org/positions/new` | ایجاد سمت سازمانی | hr | `hr.positions.view` | فرم سمت‌ها | — |
| `/hr/org/positions/:positionId` | جزئیات سمت | hr | `hr.positions.view` | فرم سمت‌ها | ویرایش درون‌صفحه‌ای |
| `/hr/org/employees` | کارکنان | hr | `hr.employees.view` | منو | — |
| `/hr/org/employees/new` | ثبت کارمند | hr | `hr.employees.view` | فرم کارکنان | — |
| `/hr/org/employees/:employeeId` | پروندهٔ کارمند | hr | `hr.employees.view` | فرم کارکنان | تب‌ها: انتساب، اتصال به حساب، تاریخچه |
| `/hr/org/employees/:employeeId/edit` | ویرایش کارمند | hr | `hr.employees.view` | فرم پروندهٔ کارمند | — |

## 3. قواعد فعال/غیرفعال، ترتیب و Parent

1. **سه حالت ممکن برای هر آیتم منو:** (الف) فعال با مسیر واقعی + گیت hint؛ (ب) «به‌زودی» — بدون مسیر، `aria-disabled` و عنوان توضیحی ثابت؛ (ج) پنهان کامل — گروه/آیتمی که هیچ گیتش برآورده نشود (مثل گروه HR برای کاربر فاقد هر کد `hr.*`).
2. **گیت‌ها فقط hint هستند** (D-064): بارگذاری/خطای `/me/permissions` به fail-closed می‌رسد؛ مدیر نصب همهٔ hintها را می‌بیند؛ enforce همیشه سمت سرور است.
3. **ترتیب نمایش** همان ترتیب آرایهٔ `navigation` است؛ افزودن گروه/آیتم تازه فقط در انتهای گروه مربوط یا جای مصوب مالک محصول.
4. **Parent:** فرم‌های فهرست Parent منویی دارند؛ فرم‌های ایجاد/جزئیات/ویرایش Parentِ فرمِ فهرست خودشان هستند؛ هیچ فرمی Parent جعلی ندارد (دکمهٔ بازگشت همیشه به مسیر واقعی برمی‌گردد).
5. **منع Route جعلی:** هر آیتم منوی دارای `to` باید دقیقاً در `appRoutes` باشد؛ این قید با تست بخش ۴ در CI قفل شده است.
6. **تغییر این کاتالوگ:** هر Route/آیتم تازه باید هم‌زمان ردیف کاتالوگ این سند، ردیف زیرسیستم در [SUBSYSTEM_CATALOG.md](SUBSYSTEM_CATALOG.md) و تست سازگاری سبز داشته باشد.

## 4. گارد ماشینی (`menuRouteConsistency.test.tsx`)

شش قید قفل‌شده: (۱) هر برگِ دارای مسیر دقیقاً در `appRoutes` هست؛ (۲) برگ‌های بدون مسیر placeholder صادقانه‌اند؛ (۳) هیچ دو Route هم‌مسیر نیست (اولین تطبیق برنده است)؛ (۴) هر Route عنوان فارسی واقعی دارد؛ (۵) نام داخلی انگلیسی (`hr`، `roles`، `users`، `reporting`، `trade`، `consolidation`) هرگز برچسب منو نیست؛ (۶) گروه بدون صفحهٔ واقعی هرگز `openByDefault` ندارد.
