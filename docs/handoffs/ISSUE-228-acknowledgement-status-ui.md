# تحویل وظیفه — ISSUE-228-acknowledgement-status-ui

- **Issue:** [#228 — Vendor customer onboarding](https://github.com/hamidreza1394/Accounting/issues/228)
- **Agent/مسئول:** ChatGPT / GitHub tools
- **تاریخ:** 2026-10-29
- **وضعیت:** `Verified` و ادغام‌شده از طریق PR #294.

## هدف و معیارهای پذیرش
- [x] نمایش نتیجهٔ واقعی acknowledgement از API در تاریخچهٔ درخواست Vendor.
- [x] نمایش زمان ثبت پاسخ مشتری جدا از زمان ایجاد درخواست.
- [x] نگاشت روشن برای `Applied`, `AlreadyApplied`, `Rejected`؛ اگر پاسخ هنوز ثبت نشده، نمایش `—`.
- [x] تست UI برای نتیجه و زمان پاسخ.
- [x] Vendor UI typecheck/tests/build و .NET build/test روی implementation head موفق؛ CIهای بعد از ادغام main باید جداگانه بررسی شوند.

## خلاصه تغییرات
- پاسخ API در `ProvisioningRequestResponse` دو فیلد اختیاری `AcknowledgementResult` و `AcknowledgedAtUtc` را برمی‌گرداند.
- UI تاریخچهٔ provisioning دو ستون «نتیجه پاسخ مشتری» و «زمان پاسخ مشتری» اضافه می‌کند.
- هیچ وابستگی npm یا دارایی خارجی جدیدی اضافه نشده است.

## فایل‌های مهم تغییرکرده
- `Vendor/Vendor.Host/Authorization/VendorProvisioningEndpoints.cs` — افزودن نتیجه و زمان acknowledgement به DTO پاسخ.
- `Vendor/Vendor.Web/src/ProvisioningForm.tsx` — نمایش نتیجه/زمان در جدول RTL.
- `Vendor/Vendor.Web/src/ProvisioningForm.test.tsx` — تست نمایش پاسخ و زمان.
- `docs/PROJECT_STATUS.md`, `docs/WORK_LOG.md`, `docs/handoffs/ISSUE-235-acknowledgement-intake.md` — هماهنگ‌سازی وضعیت مستندات.

## تصمیم‌های فنی
- وضعیت کلی درخواست و نتیجهٔ acknowledgement دو مفهوم جدا باقی می‌مانند.
- فیلدها اختیاری‌اند تا درخواست‌های قدیمی یا در انتظار پاسخ با `—` نمایش داده شوند.
- تصمیم معماری جدیدی دربارهٔ customer-side consumer گرفته نشد.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| Vendor UI typecheck/tests/build | موفق | Vendor CI run 40871773941؛ implementation head `1a9f8c3f107f713940a4d95c92f6abe597f85f80` |
| .NET build/test | موفق | .NET CI run 40871773973؛ implementation head `1a9f8c3f107f713940a4d95c92f6abe597f85f80` |
| اندازه باندل / LCP / CLS | اندازه‌گیری نشد | هیچ dependency جدیدی اضافه نشد |

## امنیت، داده و سازگاری
- فقط metadata پاسخ تأییدشده نمایش داده می‌شود؛ acknowledgement JSON یا امضا در UI ذخیره نمی‌شود.
- هیچ schema مشتری یا `Server/**` تغییر نکرد.
- متن نتیجه توسط React به‌صورت متن render می‌شود؛ HTML خام وارد DOM نمی‌شود.

## ریسک‌ها و کارهای باقی‌مانده
- UI نمایش نتیجه را کامل می‌کند اما consumer سمت مشتری را پیاده نمی‌کند.
- D-044/OD-14 با signed package در #228/#235 تعارض دارد؛ این تعارض روی #235 ثبت شده است.
- fiscal period، parent-company reference و enrollment یک‌بارمصرف هنوز نیازمند طراحی/تصمیم هستند.

## شناسه‌های Git
- Branch: `feat/vendor-acknowledgement-status-ui`
- Pull Request: [#294](https://github.com/hamidreza1394/Accounting/pull/294) — merged
- Merge commit: `07dc0a4f1f784dfd2800fb8da12a70f05b67a862`