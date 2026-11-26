# تحویل Issue #797 — فرم تنظیم اتصال Vendor در Development

- **تاریخ:** 2026-11-28
- **شاخه:** `agent/797-vendor-dev-db-setup`
- **مبنای شاخه:** سر commit `5cb7ec7be18e94dbaffb28e26f39c99326079c17` (PR #796)
- **وضعیت:** پیاده‌سازی در شاخه؛ منتظر PR/CI. Merge نشده است.

## تغییرها

- پروفایل Visual Studio برای `Vendor.Host`، Development و URL محلی HTTPS را مشخص می‌کند و صفحه setup را باز می‌کند.
- وقتی ConnectionStrings:VendorDb پیکربندی نشده و credential ذخیره‌شده نیز وجود ندارد، Host فقط صفحه setup را بالا می‌آورد و به DB، Identity یا audit startup service دسترسی نمی‌گیرد.
- فرم فارسی RTL Windows Integrated و SQL Login را می‌پذیرد، TLS را فعال می‌کند، اتصال را با `DB_NAME()` می‌آزماید و سپس در Windows Credential Manager کاربر فعلی ذخیره می‌کند.
- در شروع بعدی Development، مقدار ذخیره‌شده خوانده می‌شود. مقدار Environment/Configuration اولویت دارد؛ صفحه منبع را نشان می‌دهد و دکمه‌های ذخیره/حذف را غیرفعال می‌کند.
- endpointها loopback-only، antiforgery/Origin-validated و rate-limited هستند؛ رمز و connection string در پاسخ، رابط یا log ثبت نمی‌شوند.
- هیچ دیتابیس یا Migration ساخته/اجرا نمی‌شود؛ Production و Customer دست‌نخورده‌اند.

## بررسی

- منبع کد و جریان آغاز Host از branch پایه بررسی شد.
- checkout محلی در این نشست وجود ندارد؛ `dotnet restore`, `dotnet build`, `dotnet test` و اجرای runtime/SQL انجام نشدند.
- CI تا زمان ایجاد PR اجرا نشده است؛ وضعیت Build/Test **Not yet Verified** است.
- پس از PR، نتیجهٔ Vendor CI، .NET CI، Semgrep و diff/publish guards باید ثبت شود. اجرای واقعی در Visual Studio برای اطمینان از dev certificate و دسترسی SQL به محیط مالک نیاز دارد.

## محدوده

فقط Vendor.Host و اسناد آن. بدون تغییر Customer projects، دیتابیس‌ها، Migrationها، secrets، gateها، نصب سرویس یا عملیات Production.
