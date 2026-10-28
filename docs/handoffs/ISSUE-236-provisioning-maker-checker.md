# تحویل وظیفه — ISSUE-236-provisioning-maker-checker

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/236
- **وضعیت:** پیاده‌سازی اولیه در شاخه؛ منتظر CI و بازبینی امنیتی

## هدف
افزودن مسیر تأیید دو نفره برای صدور بستهٔ راه‌اندازی مشتری، بدون قراردادن کلید خصوصی در برنامه یا مخزن.

## تغییرات
- افزودن `VendorProvisioningIssuanceService` برای بررسی نسخهٔ درخواست، ارتباط مشتری/نصب، فعال‌بودن مقصد، canonical JSON، hash و عمر بسته پیش از امضا.
- استفاده از `ILicenseSigningProvider` موجود برای امضای دقیق بایت‌های canonical؛ هر نتیجهٔ نامعتبر یا عدم دسترسی به امضاکننده با پاسخ عمومی 503 fail-closed می‌شود.
- تغییر وضعیت `AwaitingSigner` به `PendingDelivery` فقط پس از امضای موفق و ذخیرهٔ KeyId/algorithm/signature در همان تراکنش با تصمیم تأیید.
- افزودن درخواست تأیید با کنترل Anti-forgery، مجوز مشتری/نصب، RowVersion، جلوگیری از تأیید توسط همان درخواست‌دهنده و ثبت ممیزی.
- افزودن مسیر export بستهٔ امضاشده فقط در وضعیت `PendingDelivery` با Anti-forgery، مجوز خواندن مشتری/نصب، هدرهای `no-store` و رویداد ممیزی تراکنشی.
- افزودن دکمهٔ UI برای ارسال درخواست به بررسی دو نفره و دانلود فایل JSON بستهٔ امضاشده برای انتقال آفلاین؛ وضعیت تأیید مشتری همچنان جدا نگه داشته می‌شود.
- رد تأیید، درخواست را با کد عمومی `provisioning_rejected` به `Failed` می‌برد؛ شکست امضاکننده درخواست را `AwaitingSigner` و تأیید را `Pending` نگه می‌دارد تا retry ممکن باشد.
- گسترش آزمون integration SQL Server برای اثبات شکست موقت امضا، retry همان approval، امضای موفق، اعتبارسنجی رمزنگاری بسته با کلید عمومی تست، export با Anti-forgery و ممیزی.

## مرزهای امنیتی و محدودیت‌ها
- امضاکنندهٔ پیش‌فرض `UnavailableLicenseSigningProvider` باقی مانده است؛ این تغییر به‌تنهایی امضای Production را فعال نمی‌کند.
- تا وقتی adapter واقعی KMS/HSM یا سرویس امضای مورد تأیید پیکربندی نشود، درخواست تأیید نمی‌تواند به `PendingDelivery` برسد.
- `PendingDelivery` به معنی تحویل/تأیید مشتری نیست. مصرف‌کنندهٔ مشتری و acknowledgement امضاشده در Issue #235 جداگانه لازم است.
- هیچ تغییری در `Server/**` یا دیتابیس حسابداری مشتری انجام نشده است.

- یک شکست CI نشان داد که تست integration پس از ایجاد intent، شناسهٔ تجاری مشتری را تغییر می‌داد؛ fixture اصلاح شده تا binding امضاشونده ثابت بماند و تست به مسیر واقعی signer-unavailable برسد. کنترل تولیدی که تغییر binding را رد می‌کند حفظ شده است.

## آزمون‌ها
| مورد | وضعیت |
|---|---|
| Vendor CI | در انتظار نتیجهٔ PR |
| .NET CI و تست integration روی SQL Server | در انتظار نتیجهٔ PR |
| E2E و انتشار Linux/Windows | در انتظار نتیجهٔ PR |
| اجرای محلی | در این محیط GitHub-only اجرا نشده |

## فایل‌های مهم
- `Vendor/Vendor.Host/Signing/VendorProvisioningIssuanceService.cs`
- `Vendor/Vendor.Host/Authorization/VendorProvisioningEndpoints.cs`
- `Vendor/Vendor.Host/Authorization/VendorApprovalEndpoints.cs`
- `Vendor/Vendor.Host/Program.cs`
- `tests/Vendor.Host.Tests/VendorCustomerMutationHttpIntegrationTests.cs`

- **Branch:** feat/issue-236-provisioning-release-check
- **PR:** https://github.com/hamidreza1394/Accounting/pull/240


## تکمیل رفع خطاهای CI — 2026-10-28

- در اجرای Vendor CI شمارهٔ 40740242624، typecheck شکست خورد چون static members کلاس آزمایشی `TestURL`، متدهای استاتیک ارث‌رسیده از `URL` را بدون `override` تعریف کرده بودند. در commit `b1d355add0cc0a54561cc6cd9f8f1324599c4a90` اصلاح شد؛ در اجرای بعدی، typecheck، تست‌های UI، audit وابستگی‌های runtime و build رابط Vendor موفق شدند.
- اجرای Integration روی SQL Server سپس به‌درستی درخواست صدور را با `provisioning_payload_invalid` رد کرد: خود fixture بعد از ایجاد درخواست، کد نصب را تغییر می‌داد؛ سرویس امضا عمداً payload را به هویت نصب فعلی bind می‌کند. این خطای تست بود و نباید validation تولیدی تضعیف می‌شد.
- در commit `d30693f649a50edd2f2a493ab3b05648469174a1` fixture طوری اصلاح شد که کد نصب ثابت بماند و آزمون update همچنان نام و محیط نصب را تغییر دهد. اجرای Vendor CI شمارهٔ 40742478715 روی همین SHA موفق شد: typecheck، UI tests، audit وابستگی‌ها، build، تمام تست‌های Vendor روی SQL Server، EF snapshot guard، publish و بررسی نبود کلید خصوصی همگی موفق شدند.
- اجرای .NET CI روی همین SHA باید تا تکمیل تمام jobها بررسی شود. هر commit مستنداتی بعد از این اجرا نیازمند CI مجدد روی SHA نهایی است.
- اجرای محلی در محیط فعلی ممکن نبود؛ شواهد اعتبارسنجی از GitHub Actions است.
