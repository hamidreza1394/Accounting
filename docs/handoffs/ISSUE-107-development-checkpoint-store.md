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
- قفل فایل با FileShare.None بین پردازه‌ها از append هم‌زمان جلوگیری می‌کند؛ قفل درون پردازه نیز برقرار است.
- append با WriteThrough و Flush(true) به دیسک flush می‌شود.
- کلاس فقط `IVendorAuditCheckpointStore` را پیاده‌سازی می‌کند، نه marker مربوط به Production.

## تست‌ها
- append/read آخرین checkpoint.
- رد sequence تکراری و کاهشی.
- تشخیص دستکاری checkpoint.
- رد مسیر نسبی.
- نتیجه واقعی تست: در انتظار CI؛ اجرای محلی انجام نشده است.

## محدودیت امنیتی
- فایل محلی در همان failure domain قرار دارد و در برابر DBA/ادمین میزبان یا rollback کل فایل مقاومتی ادعا نمی‌کند.
- هیچ registration پیش‌فرض، fallback Production، migration یا customer DB change اضافه نشده است.
- Production همچنان به provider مستقل با credential جدا و retention/immutability نیاز دارد.

## کارهای باقیمانده
- CI و بازبینی کد.
- در صورت نیاز wiring صریح Development از config (بدون Production registration).
- انتخاب و پیاده‌سازی provider مستقل Production، credential separation، retention/immutability، alerting و recovery.
