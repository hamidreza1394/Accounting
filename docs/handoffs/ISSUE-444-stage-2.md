# تحویل مرحله دوم — ISSUE-444-stage-2 (معماری زیرسیستم‌ها، منوها و سیستم دسترسی)

- **Issue:** [#444](https://github.com/hamidreza1394/Accounting/issues/444) — Stage 2 مأموریت تکمیلی 2026-11-08
- **وضعیت:** در حال اجرا — این سند با هر PR مرحله دوم به‌روزرسانی می‌شود. ادغام هیچ PRای فقط با اجازهٔ مالک.

## هدف مرحله

ساخت زیرساخت استاندارد و قابل توسعه برای مدیریت دسترسی تمام زیرسیستم‌های فرانو: کاتالوگ رسمی زیرسیستم‌ها، مدل منو/فرم، کاتالوگ عملیات و مجوزها، API کاتالوگ، بهبود گروه‌های دسترسی، UI مدیریت و تست یکپارچگی.

## جدول PRهای مرحله دوم

| بخش | عنوان | شاخه | PR | وضعیت |
|---|---|---|---|---|
| 2-A | docs(access): define subsystem catalog and access architecture | `docs/issue-444-stage2-subsystem-catalog` | [#575](https://github.com/hamidreza1394/Accounting/pull/575) | Open |
| 2-B | feat(access): define menu and form catalog | `feat/issue-444-stage2-menu-form-catalog` | [#576](https://github.com/hamidreza1394/Accounting/pull/576) | Open (stacked روی 2-A) |
| 2-C | feat(access): complete permission definition catalog | `docs/issue-444-stage2-permission-catalog` | ثبت پس از ایجاد | Open (stacked روی 2-B) |

## 2-A — کاتالوگ رسمی زیرسیستم‌ها (این PR)

- کاتالوگ کامل استخراج‌شده از کد واقعی (`cb2ddd57`) در `docs/SUBSYSTEM_CATALOG.md`: ۱۶ زیرسیستم در سه گروه (هسته/مدیریتی، عملیاتی مجوزدار، پشتیبان/بیرونی) با شواهد کد، وضعیت واقعی (D-030)، منوی مرئی، کدهای مجوز و پیوند entitlement.
- تفکیک مفهومی زیرسیستم/منو/فرم/عملیات + قواعد نگهداری کاتالوگ.
- ADR **D-075** (`Proposed`) در `docs/DECISIONS.md`: مدل مفهومی چهارگانه، پیوند entitlement فقط برای زیرسیستم‌های مجوزدار، هم‌خوانی `PermissionTree` با کاتالوگ، بدون موازی‌سازی.
- شکاف‌های واقعی مستند شد (Trade runtime، تلفیق، grant سال/دوره، retention، انقضای عضویت، زیرسیستم‌های placeholder).
- هیچ تغییر کد/Migration/API — فقط مستندات.

### یافته‌های ممیزی (ورودی Stage 2-B..2-H)

- کاتالوگ مجوز: **۷۳ کد** در ۶ Migration (corporate-groups: ۸، trade: ۴۶، membership: ۱، users.list: ۱، roles: ۵، hr: ۱۲)؛ منبع: `PermissionDefinitions`.
- منو و روتر کاملاً هم‌خوان‌اند؛ هیچ Route بدون منو «خراب» نیست؛ ۹ آیتم «به‌زودی» بدون Route جعلی.
- `docs/ADMIN_UI.md` یک سطر کهنه دارد («سوابق امنیتی… فقط آیتم غیرفعال») که با وضعیت فعلی هم‌خوان نیست — در PR مناسب مرحله دوم اصلاح می‌شود.
- مدل منو/فرم فعلی فقط در آرایه‌های UI است؛ مدل‌سازی مجدد در 2-B با حداقل تغییر و بدون جدول جدید بررسی می‌شود.

## 2-B — کاتالوگ منو و فرم

- سند `docs/MENU_FORM_CATALOG.md`: درخت منوی واقعی (۷ گروه + آیتم‌های بالا، ترتیب نمایش، گیت‌ها، ۹ placeholder صادقانه)، کاتالوگ کامل ۲۳ Route با Parent/گیت/یادداشت، قواعد فعال/غیرفعال/پنهان، منع Route جعلی و قواعد تغییر.
- **گارد ماشینی جدید** `menuRouteConsistency.test.tsx` (۶ تست): هر برگ دارای مسیر دقیقاً در `appRoutes`؛ برگ بدون مسیر = placeholder؛ بدون Route تکراری؛ عنوان فارسی همهٔ Routeها؛ منع برچسب انگلیسی داخلی؛ گروه بدون صفحهٔ واقعی هرگز باز-پیش‌فرض نیست.
- تغییر کد: فقط `export` آرایهٔ `navigation` و نوع‌های `NavEntry/NavLeafInfo/NavGroupInfo` از `AppSidebar.tsx` (بدون هیچ تغییر رفتاری) + تست جدید.

## 2-C — کاتالوگ عملیات و مجوزها

- سند `docs/PERMISSION_CATALOG.md`: نگاشت کامل **۷۳ کد** `PermissionDefinitions` به محل اعمال واقعی (endpoint/سرویس)، تاکسونومی ۱۱ فعل موجود + طبقه‌بندی رزروشدهٔ Trade، تطبیق فعل‌های مأموریت (حذف = Deactivate طبق D-072؛ رد/ابطال/چاپ کد ندارند چون فرم واقعی ندارند)، سطح ۱۹ hint کلاینت با نگاشت به کد سرور، قواعد گروه‌بندی UI درخت مجوز و قواعد نگهداری.
- یافته‌های کلیدی: هر ۷۳ کد دارای نام فارسی در seed؛ ۴۶ کد Trade آماده ولی بدون endpoint تولیدی (fail-closed)؛ دو کد seeded-only گروه شرکتی (`reporting.group.export`، `consolidation.adjustments.approve`)؛ هیچ hint مرده/ناشناخته‌ای در کلاینت نیست.
- طبق مأموریت **هیچ کد مجوز تازه‌ای ساخته نشد** (مجوز بی‌استفاده ممنوع)؛ فقط-مستندات.
