# تحویل وظیفه — ISSUE-236-signing-assurance-build-fix

- **Issue:** [#236](https://github.com/hamidreza1394/Accounting/issues/236)
- **Agent/مسئول:** ChatGPT
- **تاریخ:** 2026-10-30
- **وضعیت:** اصلاح ثبت شد؛ تأیید نهایی CI در انتظار است

## هدف و معیارهای پذیرش
- [x] رفع خطای کامپایل CS1503 در تست‌های PKCS#11
- [x] حفظ اعتبارسنجی صریح Production و سازگاری فراخوانی‌های بدون پارامتر
- [ ] تأیید Build/Test روی CI پس از commit اصلاحی

## خلاصه تغییرات
- متد بدون پارامتر `Validate()` به‌عنوان overload سازگار اضافه شد و به `Validate(isProduction: false)` واگذار می‌کند.
- overload `Validate(bool isProduction)` بدون مقدار پیش‌فرض باقی ماند؛ بنابراین تشخیص محیط Production در نقاط فراخوانی عملیاتی صریح است.
- تست‌های قبلی که `options.Validate` را به‌صورت method group به `Assert.Throws` می‌دهند، دوباره با `Action` سازگار می‌شوند.

## فایل‌های مهم تغییرکرده
- `Vendor/Vendor.Host/Signing/Pkcs11SigningProvider.cs` — اصلاح امضای API اعتبارسنجی.
- `docs/WORK_LOG.md` — ثبت علت خطا و شواهد.

## تصمیم‌های فنی
- به‌جای تغییر تست‌ها به lambda، سازگاری API حفظ شد؛ چون مشکل ناشی از تغییر امضای متد عمومی بود و method-group بدون پارامتر یک الگوی معتبر مصرف‌کننده است.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| CI پیش از اصلاح | ناموفق | سه خطای CS1503 در `Pkcs11SigningProviderTests.cs` خطوط 21، 24 و 43 |
| Build/Test پس از اصلاح | در انتظار | باید روی commit جدید CI اجرا شود |
| اجرای محلی .NET | اجرا نشده | محیط محلی متصل به checkout مخزن در این نشست موجود نیست |

## امنیت، داده و سازگاری
- رفتار Production همچنان نیازمند `AcceptSoftwareKeyRisk=true` صریح است.
- هیچ Migration، Schema، دیتابیس مشتری، کلید خصوصی، PIN یا secret تغییر نکرد.
- اعتبارسنجی نامعتبر همچنان fail-closed است.

## ریسک‌ها و کارهای باقی‌مانده
- تا سبز شدن تمام کنترل‌های الزامی CI، PR آماده Merge نیست.
- بررسی سایر خطاهای احتمالی در Jobهای جاری پس از تکمیل آن‌ها ادامه می‌یابد.

## شناسه‌های Git
- Branch: `feat/issue-236-signing-assurance-stage1`
- Commit: `fd9f0e9f9135d498266cc847bb0479a650913121`
- Pull Request: [#360](https://github.com/hamidreza1394/Accounting/pull/360)