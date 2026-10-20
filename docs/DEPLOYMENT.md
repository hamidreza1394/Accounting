# راهنمای استقرار Production

این سند تنظیماتی را که پیش از استقرار واقعی **باید** برای هر محیط تعیین شوند جمع می‌کند. مقادیر نمونه (`accounting.example.com`، `10.0.0.5`) فقط مثال هستند و تنظیم نهایی محسوب نمی‌شوند؛ پرسش‌های باز در پایان سند آمده‌اند.

## ۱. انتشار
```bash
scripts/publish.sh /srv/faranou/release-2026-10-20        # Linux
scripts/smoke-published-app.sh /srv/faranou/release-2026-10-20   # اختیاری: آزمون HTTP خروجی
```
```powershell
pwsh scripts/publish.ps1 -Output D:\Deploy\Faranou\release-2026-10-20   # Windows
```
- ترتیب ثابت: `npm ci` ← typecheck و تست کلاینت ← `npm run build` ← بررسی آفلاین ← `dotnet publish` (net10.0، Release) ← بررسی خروجی (`wwwroot/app/index.html`، JS، CSS، فونت و تصاویر).
- **محافظ پوشه خروجی:** مسیر پیش از هر حذف به مسیر مطلق و canonical تبدیل می‌شود. اسکریپت این مسیرها را رد می‌کند و چیزی حذف نمی‌کند: ریشه فایل‌سیستم/درایو، ریشه مخزن، پوشه خانه، پوشه جاری یا هر والد آن‌ها، هر مسیر داخل مخزن به‌جز `artifacts/<name>`، پیوند نمادین (یا مسیری زیر آن)، فایل، و پوشه غیرخالی که نشانگر `.faranou-publish-output` انتشار قبلی را ندارد. پیش‌فرض (`artifacts/publish`) همچنان کار می‌کند. Bash و PowerShell قواعد یکسان دارند و هر دو در CI تست می‌شوند (PowerShell روی Windows واقعی و روی Linux).

## ۲. `AllowedHosts`
- مقدار `*` در `appsettings.json` فقط برای توسعه مناسب است. در Production نام میزبان(های) واقعی را تنظیم کنید:
  - متغیر محیطی: `AllowedHosts=accounting.example.com` (چند نام با `;`)
  - یا `appsettings.Production.json`.
- درخواستی با سرآیند `Host` دیگر پاسخ 400 می‌گیرد (تست Integration). اگر `*` بماند، در شروع برنامه هشدار ثبت می‌شود.

## ۳. Reverse Proxy (`ReverseProxy`)
- به‌طور پیش‌فرض **به هیچ فرستنده‌ای اعتماد نمی‌شود**، حتی loopback. `X-Forwarded-For` و `X-Forwarded-Proto` نادیده گرفته می‌شوند.
- فقط آدرس یا شبکه Proxyهای واقعی را وارد کنید:
  ```json
  "ReverseProxy": { "KnownProxies": [ "10.0.0.5" ], "KnownNetworks": [ "10.0.1.0/24" ], "ForwardLimit": 1 }
  ```
  متغیر محیطی معادل: `ReverseProxy__KnownProxies__0=10.0.0.5`.
- مقدار نامعتبر (IP یا CIDR غلط، `ForwardLimit < 1`) برنامه را در شروع متوقف می‌کند تا اعتماد بی‌صدا تغییر نکند.
- `X-Forwarded-Host` هرگز پذیرفته نمی‌شود؛ میزبان فقط با `AllowedHosts` کنترل می‌شود.
- **رفتار پشت Proxy مورد اعتماد** (تست‌های `DeploymentConfigurationTests`):
  - آدرس واقعی کلاینت از `X-Forwarded-For` در Rate Limiting، `LoginLogs` و رویدادهای امنیتی استفاده می‌شود.
  - اگر TLS در Proxy خاتمه یابد و `X-Forwarded-Proto: https` برسد، HTTPS Redirection دوباره هدایت نمی‌کند و HSTS ارسال می‌شود. درخواست HTTP خود Proxy همچنان به HTTPS هدایت می‌شود.
  - سرآیند جعلی از فرستنده نامعتبر هیچ اثری ندارد و پارتیشن Rate Limiting جدید هم باز نمی‌کند.
- اگر Proxy وجود ندارد، این بخش را خالی بگذارید. هشدار شروع برنامه فقط یادآوری است.

## ۴. HTTPS و HSTS
- `UseHttpsRedirection` و `UseHsts` بیرون از Development فعال‌اند. HSTS فقط روی پاسخ HTTPS و نه برای `localhost` ارسال می‌شود.
- Cookie احراز هویت در Production فقط `Secure` است؛ دسترسی کاربران باید از HTTPS باشد (مستقیم یا از Proxy مورد اعتماد).

## ۵. چندنمونه‌ای (Scale-out)
برنامه فعلاً برای **یک نمونه** طراحی و تست شده است. پیش از اجرای چند نمونه پشت Load Balancer:
- **Rate Limiting درون‌پردازه‌ای است:** هر نمونه شمارنده خودش را دارد و سقف واقعی تقریباً «سقف × تعداد نمونه‌ها» می‌شود. گزینه‌ها (هر کدام نیازمند ADR):
  1. محدودسازی در لایه Proxy یا Gateway (مثلاً `limit_req` در nginx یا قابلیت WAF) — ساده‌ترین؛
  2. Sticky Session بر اساس IP (دقت محدود)؛
  3. Limiter توزیع‌شده با Store مشترک (مثلاً Redis) — زیرساخت جدید.
  زیرساخت توزیع‌شده‌ای اضافه نشده است.
- **کلیدهای Data Protection باید مشترک باشند،** وگرنه Cookie و توکن Anti-forgery صادرشده در یک نمونه در نمونه دیگر نامعتبر است. با `PersistKeysToFileSystem` روی مسیر مشترک امن یا Store دیگر، همراه با محافظت کلید (مثلاً `ProtectKeysWithCertificate`) تنظیم شود. این تنظیم انجام نشده و برای استقرار تک‌نمونه لازم نیست.

## ۶. حساب زیرساختی (Master) و هش قدیمی
1. روی میزبان برنامه اجرا کنید: `dotnet Server.dll --create-master-credential`. نام کاربری و رمز دو بار پرسیده می‌شود و رمز نمایش داده نمی‌شود. فقط **هش** جدید (PBKDF2/PasswordHasher V3) و کلیدهای تنظیم چاپ می‌شوند. چیزی در Log یا دیسک نوشته نمی‌شود و سرور وب اجرا نمی‌شود.
2. هش را در Secret Store یا متغیر محیطی قرار دهید (`ApplicationSettings__MasterPassword`). آن را در Git یا `appsettings` قرار ندهید.
3. پس از اطمینان از ورود با مقدار جدید، `ApplicationSettings__AllowLegacyMasterPasswordHash=false` را تنظیم کنید. از این لحظه مقدار قدیمی SHA-256، حتی با رمز درست، پذیرفته نمی‌شود.
4. اثر: نشست‌های باز حساب Master در درخواست بعدی پایان می‌یابند (مهر اعتبارنامه عوض می‌شود) و باید دوباره وارد شد.

تا وقتی مقدار پیکربندی‌شده قالب قدیمی دارد، در **همه محیط‌ها** هنگام شروع برنامه هشدار ثبت می‌شود (بدون مقدار). برنامه حذف هش‌های قدیمی کاربران در `docs/SECURITY.md` آمده است.

## ۷. CSP و گزارش نقض
- `SecurityHeaders:EnforceRazorContentSecurityPolicy=true` (پیش‌فرض این نسخه): سیاست سخت‌گیرانه برای صفحات Razor اجباری است. استثنا: `RazorReportOnlyPathPrefixes` (پیش‌فرض `/Admin/PageManager` به‌دلیل TinyMCE).
- `SecurityHeaders:CspReportingEnabled=true` گزارش‌ها را در `POST /csp-report` فعال می‌کند. این Endpoint ناشناس است ولی فقط Media Typeهای گزارش CSP و حداکثر ۸KB را می‌پذیرد، با Rate Limit `CspReport` محدود است، چند فیلد پاک‌سازی‌شده را Log می‌کند و چیزی ذخیره یا بازتاب نمی‌دهد. پیش‌فرض خاموش است. برای فعال‌سازی، حجم Log و هشدارها را در نظر بگیرید.

## ۸. سایر تنظیمات
- `ConnectionStrings__ConnectionString`: از Secret Store یا Environment.
- Migration: `dotnet Server.dll --migrate` فقط پس از پشتیبان‌گیری و بازبینی (`docs/DATABASE.md`).
- `RateLimiting:*`: سقف‌ها به ازای هر نمونه هستند (بخش ۵).

## پرسش‌های باز (نیازمند تصمیم مسئول)
1. نام دامنه نهایی (برای `AllowedHosts`) و اینکه آیا چند نام لازم است.
2. وجود Reverse Proxy یا Load Balancer، آدرس و شبکه آن، و محل خاتمه TLS.
3. تعداد نمونه‌های برنامه (برای Rate Limiting و کلیدهای Data Protection).
4. سیستم‌عامل و میزبان (IIS، Windows Service، Linux/systemd، Container) و محل امن کلیدهای Data Protection.
5. فعال‌سازی گزارش CSP و مقصد Logها.
6. زمان‌بندی کنارگذاشتن هش‌های SHA-256 کاربران (پیشنهاد ۹۰ روز) و Master.
