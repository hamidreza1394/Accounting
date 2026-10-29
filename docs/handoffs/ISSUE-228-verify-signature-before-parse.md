# تحویل وظیفه — ISSUE-228-verify-signature-before-parse

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/228
- **Agent/مسئول:** ChatGPT
- **تاریخ:** 2026-10-29
- **وضعیت:** پیاده‌سازی در PR؛ منتظر CI و بازبینی

## هدف و معیارهای پذیرش
- [x] امضای ECDSA روی بایت‌های محدودشدهٔ payload پیش از deserialize کردن JSON بررسی شود.
- [x] تست رگرسیون ثابت کند payload نامعتبر با امضای نامعتبر، خطای امضا بدهد و به parser نرسد.
- [ ] CI روی SHA نهایی موفق شود و PR پس از بررسی کامل merge شود.

## خلاصه تغییرات
- ترتیب عملیات در validator قرارداد provisioning تغییر کرد: ابتدا اعتبارسنجی envelope/اندازه و امضای دقیق UTF-8، سپس parse/schema/canonical JSON.
- خطای امضای نامعتبر دیگر به جزئیات parser وابسته نیست؛ خطای کلید عمومی نامعتبر جداگانه و fail-closed باقی می‌ماند.

## فایل‌های مهم تغییرکرده
- `Licensing.Contracts/CustomerProvisioningPackageValidator.cs` — تأیید امضا پیش از parse.
- `tests/Licensing.Contracts.Tests/CustomerProvisioningPackageValidatorTests.cs` — تست ترتیب امنیتی.
- `docs/WORK_LOG.md` — ثبت یافته و وضعیت.

## تصمیم‌های فنی
- با الزام Issue #235 هم‌راستا است: تأیید اصالت بایت‌های دقیق قبل از parse/اعمال وضعیت. همچنان اعتبارسنجی ساختار پس از تأیید امضا انجام می‌شود.
- کلید عمومی باید از trust store مستقل و مورد اعتماد caller برسد؛ KeyId داخل بسته به‌تنهایی اعتماد ایجاد نمی‌کند.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| تست محلی .NET | اجرا نشد | این نشست checkout محلی/SDK در اختیار ندارد. |
| تست رگرسیون | به CI سپرده شد | انتظار: با ترتیب قبلی شکست و پس از اصلاح موفق شود. |
| .NET CI و Vendor CI | در انتظار | Merge فقط پس از موفقیت بررسی‌های الزامی. |

## امنیت، داده و سازگاری
- فقط قرارداد مشترک و تست آن تغییر می‌کند؛ هیچ migration، Server/** یا دیتابیس مشتری تغییر نمی‌کند.
- اندازهٔ payload همچنان قبل از تبدیل به بایت محدود می‌شود؛ محدودیت 64 KiB حفظ شده است.

## ریسک‌ها و کارهای باقی‌مانده
- مصرف‌کنندهٔ مشتری، claim اتمی nonce و acknowledgement امضاشده در Issue #235 هنوز پیاده‌سازی نشده‌اند.
- signer واقعی KMS/HSM در Issue #236 همچنان نیازمند انتخاب و پیکربندی عملیاتی است.

## شناسه‌های Git
- Branch: fix/issue-228-verify-signature-before-parse
- Commit: پس از ساخت
- Pull Request: پس از ساخت
