# تحویل فنی — مدیریت نقش‌ها و مجوزدهی منبع‌محور Vendor (Issue #161)

## وضعیت
- PR: https://github.com/hamidreza1394/Accounting/pull/162
- شاخه: `feat/vendor-rbac-management-resource-authorization`
- وضعیت اعتبارسنجی: `Implemented / Not Verified` تا سبزشدن CI روی آخرین SHA.
- PR #160 مربوط به اجبار تغییر گذرواژه ادغام شده و Vendor CI / .NET CI روی SHA ادغام موفق بوده‌اند.

## تغییرات
- APIهای فهرست و خواندن نقش، ایجاد نقش، به‌روزرسانی مجوزها، خواندن کاتالوگ مجوز و تخصیص/لغو تخصیص نقش به مدیر موجود.
- تمام مسیرهای `/api/admin/*` با `vendor.administrators.manage` و baseline احراز هویت مدیر فعال/MFA محافظت می‌شوند.
- نقش `Vendor.SuperAdmin` در bootstrap یک‌باره ساخته/تأیید می‌شود و کاتالوگ مجوزهای نسخه‌دار به آن متصل می‌شود؛ آزمون Integration عضویت و claimها را بررسی می‌کند.
- API عادی نمی‌تواند SuperAdmin را ایجاد/تغییر/تخصیص/لغو تخصیص کند و نمی‌تواند `vendor.administrators.manage` را به نقش سفارشی بدهد.
- ساخت نقش، تغییر مجوزها، تخصیص و لغو نقش رویدادهای audit دارند و عملیات با append ممیزی در یک تراکنش SQL Server انجام می‌شود.
- کاتالوگ منابع فقط ترکیب‌های معتبر customer/installation/contract/license و permissionهای متناظر را قبول می‌کند.
- `VendorResourcePermissionAuthorizationHandler` ابتدا policy مجوز و MFA را ارزیابی می‌کند، سپس adapter واقعی منبع را صدا می‌زند.
- adapter ثبت‌شده فعلی `DenyAllVendorResourceAccessEvaluator` است؛ نبود adapter واقعی به‌صورت fail-closed رد می‌شود.

## محدودیت‌های صریح
1. هنوز entity و API واقعی مشتری، نصب، قرارداد و لایسنس در Vendor وجود ندارد؛ پس رفع IDOR برای رکوردهای واقعی قابل ادعا نیست.
2. هنگام پیاده‌سازی مدل دامنه باید adapter واقعی از رکورد persisted و رابطهٔ parent/customer استفاده کند؛ هرگز به `CustomerId` ارسالی کاربر اعتماد نشود.
3. آزمون‌های cross-resource باید تلاش با شناسهٔ متعلق به مشتری/منبع دیگر را با 403/404 قابل‌قبول و بدون نشت اطلاعات بررسی کنند.
4. API provisioning/invitation برای ایجاد مدیر جدید و UI مدیریت نقش هنوز بخشی از دامنهٔ بعدی است؛ این PR حساب جدید را از طریق API عمومی ایجاد نمی‌کند.
5. Maker-checker برای تغییرات حساس و checkpoint مستقل زنجیرهٔ ممیزی هنوز باقی است.

## اعتبارسنجی لازم
- Vendor CI: restore/build/test روی SQL Server، publish و بررسی artifact.
- .NET CI: build/test، guardهای migration مشتری و اسکن اسرار.
- آزمون bootstrap باید نقش SuperAdmin، مجوزها و زنجیرهٔ ممیزی را در دیتابیس آزمایشی بررسی کند.
- آزمون endpoint باید نشان دهد همهٔ مسیرهای مدیریتی policy `vendor.administrators.manage` و baseline MFA دارند.
- هیچ Migration دیتابیس مشتری یا Migration جدید Vendor در این تغییر وجود ندارد؛ تغییرات فقط از جداول Identity موجود استفاده می‌کنند.
- هیچ وابستگی جدیدی اضافه نشده است.
