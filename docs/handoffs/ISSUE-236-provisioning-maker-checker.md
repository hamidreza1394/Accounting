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
