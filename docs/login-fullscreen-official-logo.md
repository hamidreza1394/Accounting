# اصلاح نهایی صفحه ورود تراز آوا

## محدوده
این تغییرات روی شاخه `fix/login-fullscreen-official-logo` انجام شده‌اند و طراحی موجود Login را بازنویسی نکرده‌اند. هدف، حذف Header عمومی از Login، پوشش کامل Viewport، حفظ Layout عمومی سایر صفحات و آماده‌سازی مسیر لوگوی رسمی است.

## تغییرات انجام‌شده
- `Server/Pages/Account/Login.cshtml`
  - استفاده از Layout امنیتی بدون Header و Navigation.
  - انتخاب Layout RTL/LTR بر اساس Culture فعلی.
  - استفاده از منابع موجود برای عنوان صفحه، نام فیلدها، Remember Me و دکمه Login.
  - نگهداری Anti-forgery، مدل فرم و Handlerهای موجود.
- `Server/Pages/Shared/Layouts/Rtl/_Security.cshtml`
  - افزودن کلاس `BodyClass` به Body بدون تغییر Layout عمومی.
- `Server/Pages/Shared/Layouts/Ltr/_Security.cshtml`
  - افزودن کلاس `BodyClass` به Body بدون تغییر Layout عمومی.
- `Server/wwwroot/css/rtl/taraz-login.css`
  - `width: 100%` و `min-height: 100dvh` برای صفحه و Shell.
  - حذف اثر margin پیش‌فرض Body فقط در صفحات Login با `.taraz-login-page`.
  - جلوگیری از overflow افقی و سازگاری LTR برای ورودی‌ها و کنترل رمز عبور.

## لوگو
لوگوی محلی قبلی پروژه در `Server/wwwroot/images/taraz-ava-mark.svg` همچنان استفاده می‌شود. فایل رسمی جدید در این نوبت به‌دلیل محدودیت انتقال مستقیم فایل پیوست به GitHub Connector ثبت نشد؛ بنابراین از جایگزین‌کردن آن با تصویر ساختگی یا URL خارجی خودداری شد. پس از فراهم‌شدن یک file URL قابل‌نوشتن یا Blob URL، باید فایل رسمی در `Server/wwwroot/images/` اضافه و فقط ارجاع‌های Image در Login به آن منتقل شوند.

## چندزبانه
زیرساخت موجود پروژه شامل Culture Cookie، منابع strongly typed و Layoutهای RTL/LTR است. Login اکنون Layout را بر اساس `CurrentUICulture` انتخاب می‌کند و رشته‌های اصلی فرم از `Resources` استفاده می‌کنند. ترجمه‌های توضیحات تبلیغاتی و داشبورد برای فارسی و انگلیسی در View به‌صورت محدود ارائه شده‌اند؛ PageModel و منابع اعتبارسنجی تغییر نکرده‌اند.

## بررسی
- آخرین CI این شاخه در زمان ثبت این سند هنوز pending بود.
- Build/Test محلی و Browser visual review در این محیط اجرا نشده‌اند.
- Merge انجام نشده است.
