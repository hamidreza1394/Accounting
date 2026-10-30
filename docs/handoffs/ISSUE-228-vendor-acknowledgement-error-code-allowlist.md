# تحویل وظیفه — ISSUE-228-vendor-acknowledgement-error-code-allowlist

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/228
- **Agent/مسئول:** ChatGPT
- **تاریخ:** 2026-10-30
- **وضعیت:** پیاده‌سازی در شاخه؛ منتظر CI و بازبینی

## هدف و معیارهای پذیرش
- [x] نتیجهٔ `Rejected` فقط با کد خطای ثابت و غیرحساس به وضعیت `Failed` تبدیل شود.
- [x] متن آزاد exception، SQL diagnostics، PII و کد ناشناخته پیش از ذخیره در Vendor رد شود.
- [x] تست SQL Server نشان دهد acknowledgement ردشده با کد ناشناخته، درخواست را در `PendingDelivery` نگه می‌دارد و هیچ audit/ack metadata ثبت نمی‌کند.
- [ ] CI الزامی روی SHA نهایی موفق شود.

## خلاصه تغییرات
- allow-list Vendor برای rejection codeها محدود به `provisioning_parent_reference_invalid` و `provisioning_initial_admin_conflict` شد؛ این دو کد با مسیرهای فعلی `StoreRejectedAcknowledgementAsync` در consumer مشتری منطبق‌اند.
- کد ناشناخته حتی اگر با کلید مورد اعتماد امضا شده باشد، از transition rule عبور نمی‌کند و endpoint بدون mutation با 400 پاسخ می‌دهد.
- تست واحد برای کدهای مجاز/غیرمجاز و تست SQL Server برای عدم persistence نتیجهٔ غیرمجاز اضافه شد.

## فایل‌های مهم تغییرکرده
- `Vendor/Vendor.Host/Authorization/ProvisioningAcknowledgementStateRules.cs` — allow-list fail-closed.
- `tests/Vendor.Host.Tests/ProvisioningAcknowledgementStateRulesTests.cs` — آزمون کدهای ثابت و رد متن آزاد/PII.
- `tests/Vendor.Host.Tests/ProvisioningAcknowledgementSqlServerIntegrationTests.cs` — آزمون عدم تغییر وضعیت و عدم ثبت audit برای کد غیرمجاز.
- `docs/SECURITY.md`، `docs/PROJECT_STATUS.md`، `docs/WORK_LOG.md` — ثبت مرز اعتماد و وضعیت واقعی.

## تصمیم‌های فنی
- تصمیم معماری جدیدی گرفته نشد. قرارداد فعلی acknowledgement حفظ شد؛ Vendor علاوه بر امضا و binding، محتوای errorCode را به allow-list محلی محدود می‌کند.
- برای افزودن rejection code جدید باید consumer، قرارداد و Vendor transition rule با تست هماهنگ تغییر کنند؛ ذخیرهٔ پیام exception مجاز نیست.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| تست محلی .NET | اجرا نشد | در این نشست checkout/SDK محلی اجرا نشد. |
| `ProvisioningAcknowledgementStateRulesTests` | در CI | روی PR جدید باید اجرا شود. |
| `ProvisioningAcknowledgementSqlServerIntegrationTests` | در CI | نیازمند SQL Server واقعی در Workflow. |
| .NET CI / Vendor CI / secret scan | در انتظار | Merge فقط پس از موفقیت بررسی‌های الزامی. |

## امنیت، داده و سازگاری
- فقط Vendor Host، تست‌های Vendor و مستندات تغییر می‌کنند؛ هیچ فایل `Server/**`، Migration مشتری یا schema مشتری تغییر نکرده است.
- هیچ secret، کلید خصوصی، رمز، hash رمز یا connection string افزوده نشده است.

## ریسک‌ها و کارهای باقی‌مانده
- اگر consumer در آینده rejection code جدیدی تولید کند، تا افزودن همان کد به allow-list و تست متناظر، Vendor آن acknowledgement را رد می‌کند؛ این رفتار fail-closed و عمدی است.
- این تغییر signer عملیاتی #236 یا consumer signing provider سمت مشتری را فراهم نمی‌کند.

## شناسه‌های Git
- Branch: `fix/vendor-acknowledgement-error-code-allowlist`
- Commit: پس از ساخت
- Pull Request: پس از ساخت
