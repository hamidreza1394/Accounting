# تحویل وظیفه — Issue #393: سازندهٔ Payload برای افزایش سقف شرکت

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/393
- **Agent/مسئول:** ChatGPT
- **تاریخ:** 2026-11-04
- **وضعیت:** پیاده‌سازی محدود؛ CI و بازبینی نهایی لازم است.

## هدف و معیارهای پذیرش
- [x] ساخت candidate amendment برای افزایش installation-scoped `maxCompanies` روی همان LicenseId و InstallationId.
- [x] حفظ Payload v1 به‌صورت v1 و Payload v2 به‌صورت v2؛ بدون تبدیل ضمنی نسخه یا حذف entitlementهای موجود.
- [x] الزام sequence دقیقاً بزرگ‌تر، سقف جدید بزرگ‌تر، منابع Active/Issued و تطابق metadata با آخرین payload.
- [x] تست‌های واحد برای v1/v2، حفظ حقوق، metadata stale، cross-customer، وضعیت نامعتبر، replay/sequence و عقب‌گرد زمان، انقضا/شروع term و استقلال support/update.
- [ ] اجرای CI روی SHA نهایی و بازبینی diff.
- [ ] تکمیل persistence، commercial order/purchase evidence، maker-checker approval و signing/export در PRهای بعدی #393.

## خلاصه تغییرات
- کلاس خالص `VendorLicenseAmendmentPayloadBuilder` افزوده شد. ورودی current payload باید پیش‌تر با trust store مستقل و امضای دقیق envelope اعتبارسنجی شده باشد.
- فقط افزایش سقف شرکت را می‌سازد؛ کاهش، مقدار مساوی، سقف اولیهٔ نامشخص، sequence stale/overflow، customer/installation غیرمنطبق یا غیرفعال و metadata stale رد می‌شوند.
- candidate تمام فیلدهای دیگر را حفظ می‌کند: ProductCode، LicenseId، InstallationId، نوع/تاریخ لایسنس، subsystemهای v1، company entitlements و تاریخ‌های support/update در v2.
- هیچ امضا، وضعیت پایدار، sequence دیتابیس، سفارش/پرداخت یا Approval در این builder تغییر نمی‌کند.

## فایل‌های مهم تغییرکرده
- `Vendor/Vendor.Host/Signing/VendorLicenseAmendmentPayloadBuilder.cs`
- `tests/Vendor.Host.Tests/VendorLicenseAmendmentPayloadBuilderTests.cs`
- `docs/LICENSING_IMPLEMENTATION_PLAN.md`
- `docs/WORK_LOG.md`

## تصمیم‌های فنی
- D-059 رعایت می‌شود: سقف Company در سطح Installation است؛ LicenseId/InstallationId ثابت می‌ماند.
- Payload v1 و v2 جداگانه حفظ می‌شوند. تبدیل v1 به v2 ممنوع است تا زمانی که entitlementهای Company-scoped قبلی با مرجع قابل اعتماد معلوم نباشند.
- Builder هیچ اعتماد جدیدی به ورودی UI نمی‌دهد؛ caller باید signature، hash، trusted key، customer/install binding و آخرین sequence را در transaction مناسب بررسی کند.
- این PR، اجرای تجاری یا امضای Production را کامل نمی‌کند؛ provider واقعی KMS/HSM انتخاب یا جعل نمی‌شود.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `dotnet build` | در CI موفق | Build در `.NET CI` روی SHA قبلی PR موفق شد؛ commit اصلاح analyzer مجدداً باید بررسی شود. |
| `dotnet test tests/Vendor.Host.Tests` | موفق روی SHA قبلی | Vendor.Host.Tests: 214 passed، 0 failed، 0 skipped؛ commit اصلاح analyzer مجدداً CI می‌خواهد. |
| SQL Server Integration | موفق در Vendor CI روی SHA قبلی | مجموعهٔ Vendor.Host.Tests با SQL Server CI اجرا شد؛ 214 passed، 0 failed. |
| `git diff --check` | اجرا نشده | تغییر از طریق GitHub Git API ساخته می‌شود؛ CI و review diff لازم است. |

## امنیت، داده و سازگاری
- فقط کد Vendor Host، تست و مستندات تغییر می‌کند.
- هیچ تغییر در `Server/**`، Customer DB، Migration مشتری، قرارداد Customer یا کلید/secret انجام نمی‌شود.
- builder امضا را تأیید نمی‌کند و نباید با current payload خام فراخوانی شود؛ trust validation مسئولیت service/caller است.

## ریسک‌ها و کارهای باقی‌مانده
- سفارش/پرداخت و purchase evidence هنوز مدل/endpoint ندارد.
- Company-specific module/feature/seat add-ons نیازمند current Payload v2 و CompanyReference معتبر است؛ تبدیل خودکار v1 به v2 مجاز نیست.
- صدور نهایی، maker-checker، تراکنش، sequence uniqueness، export و ACK هنوز در #393/#394/#235 پیاده‌سازی/تأیید می‌شوند.

## شناسه‌های Git
- Branch: feat/393-vendor-company-capacity-amendment-payload
- Commit: SHA نهایی همیشه از Head همان PR خوانده می‌شود؛ CI فعلی روی هر push مجدداً اجرا می‌شود.
- Pull Request: پس از ساخت


## به‌روزرسانی CI — 2026-11-04
- روی SHA قبلی `50b431e7a791b20353db4a83569bf97aafa110c8`، Vendor CI شامل 214 تست Vendor.Host با 0 شکست و 0 skip موفق شد؛ Linux و Windows checkpoint regression نیز موفق شدند.
- .NET CI روی همان SHA: Build موفق؛ مجموع 923 تست، 915 موفق، 8 skip و 0 شکست. E2E و publish در زمان ثبت این یادداشت هنوز در حال اجرا بودند.
- Semgrep، CodeQL و Secret Scan موفق شدند.
- دو هشدار CA1861 در fixtureهای تست با آرایه‌های readonly مشترک اصلاح شده‌اند؛ این commit جدید نیازمند اجرای مجدد CI است.

- **اصلاح اعتبار زمانی:** amendment فقط در بازهٔ اعتبار حق استفاده ساخته می‌شود؛ grace ضمنی برای term expiry اعمال نمی‌شود. انقضای support/update مانع amendment یک perpetual license نیست.
