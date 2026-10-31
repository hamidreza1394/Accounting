# تحویل وظیفه — ISSUE-107-development-checkpoint-store

- Issue: [#107](https://github.com/hamidreza1394/Accounting/issues/107)
- Branch: `feat/issue-107-development-checkpoint-store`
- وضعیت: پیاده‌سازی و تست‌ها ثبت شده‌اند؛ CI در انتظار است.

## هدف و محدوده
ساخت adapter محلی برای Development و تست خودکار، مطابق تصمیم 2026-10-30. این adapter مقصد مستقل Production نیست و هرگز نباید Production-ready معرفی شود.

## تغییرات
- `DevelopmentFileVendorAuditCheckpointStore` فایل JSONL append-only را با اعتبارسنجی همه رکوردها می‌خواند.
- sequence باید strictly increasing باشد؛ duplicate/decreasing رد می‌شود.
- رکورد نامعتبر، خالی، خراب یا tampered باعث fail-closed با InvalidDataException می‌شود.
- قفل فایل با FileShare.None برای هماهنگی بین پردازه‌ها در نظر گرفته شده و قفل درون پردازه نیز برقرار است؛ آزمون واقعی چندپردازه‌ای هنوز اجرا/اضافه نشده و ادعای cross-platform در انتظار CI و تست مستقل است.
- انتظار lock پس از ۱۵ ثانیه با IOException خاتمه می‌یابد تا خطای I/O دائمی باعث انتظار نامحدود نشود؛ آزادسازی semaphore در finally تضمین شده است.
- append با WriteThrough و Flush(true) به دیسک flush می‌شود.
- کلاس فقط `IVendorAuditCheckpointStore` را پیاده‌سازی می‌کند، نه marker مربوط به Production.

## تست‌ها
- append/read آخرین checkpoint.
- رد sequence تکراری و کاهشی.
- تشخیص دستکاری checkpoint.
- رد مسیر نسبی.
- نتیجه واقعی تست: اجرای محلی انجام نشده است؛ CI نهایی باید پس از آخرین commit بررسی شود.

## محدودیت امنیتی
- فایل محلی در همان failure domain قرار دارد و در برابر DBA/ادمین میزبان یا rollback کل فایل مقاومتی ادعا نمی‌کند.
- هیچ registration پیش‌فرض، fallback Production، migration یا customer DB change اضافه نشده است.
- Production همچنان به provider مستقل با credential جدا و retention/immutability نیاز دارد.

## کارهای باقیمانده
- CI نهایی و بازبینی کد پس از آخرین commit.
- تست واقعی قفل بین‌پردازه‌ای روی Windows و Linux؛ در صورت عدم پشتیبانی، محدودکردن ادعای portability.
- در صورت نیاز wiring صریح Development از config (بدون Production registration).
- انتخاب و پیاده‌سازی provider مستقل Production، credential separation، retention/immutability، alerting و recovery.


## 2026-10-31 — independent-process checkpoint regression
- Added a helper executable and a regression test launching eight independent .NET processes that concurrently append the same sequence to one development JSONL checkpoint journal. Exactly one append must succeed; the other seven must reject the duplicate; the final journal must contain one valid record.
- Added a Windows/Linux matrix to the existing Vendor CI workflow (rather than introducing a workflow that cannot run until merged).
- This verifies cross-process locking for the Development/Test adapter only. It does not create a Production checkpoint sink or satisfy independent failure-domain, external retention/immutability, alerting or recovery requirements.
- Local .NET execution was unavailable; the matrix must pass before verification.
