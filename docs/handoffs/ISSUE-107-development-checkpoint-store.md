# تحویل وظیفه — ISSUE-107-development-checkpoint-store

- Issue: [#107](https://github.com/hamidreza1394/Accounting/issues/107)
- Branch: `feat/issue-107-development-checkpoint-store`
- وضعیت جاری: Development/Test adapter و regression چندپردازه‌ای روی Ubuntu و Windows در Vendor CI پس از Merge #633 تأیید شده‌اند؛ sink مستقل Production همچنان انتخاب/پیاده‌سازی نشده است.

## هدف و محدوده
ساخت adapter محلی برای Development و تست خودکار، مطابق تصمیم 2026-10-30. این adapter مقصد مستقل Production نیست و هرگز نباید Production-ready معرفی شود.

## تغییرات
- `DevelopmentFileVendorAuditCheckpointStore` فایل JSONL append-only را با اعتبارسنجی همه رکوردها می‌خواند.
- sequence باید strictly increasing باشد؛ duplicate/decreasing رد می‌شود.
- رکورد نامعتبر، خالی، خراب یا tampered باعث fail-closed با InvalidDataException می‌شود.
- قفل فایل با FileShare.None برای هماهنگی بین پردازه‌ها و قفل درون‌پردازه‌ای برقرار است؛ تست هشت پردازه مستقل برای sequence تکراری در Vendor CI روی Ubuntu و Windows موفق شده است. این شواهد فقط Development/Test adapter را پوشش می‌دهد و portability روی network filesystem یا Production sink را اثبات نمی‌کند.
- انتظار lock پس از ۱۵ ثانیه با IOException خاتمه می‌یابد تا خطای I/O دائمی باعث انتظار نامحدود نشود؛ آزادسازی semaphore در finally تضمین شده است.
- append با WriteThrough و Flush(true) به دیسک flush می‌شود.
- کلاس فقط `IVendorAuditCheckpointStore` را پیاده‌سازی می‌کند، نه marker مربوط به Production.

## تست‌ها
- append/read آخرین checkpoint.
- رد sequence تکراری و کاهشی.
- تشخیص دستکاری checkpoint.
- رد مسیر نسبی.
- نتیجه واقعی تست: اجرای محلی انجام نشده است؛ regression چندپردازه‌ای در Vendor CI run `42783806157`، jobهای `129719941495` (Ubuntu) و `129719941498` (Windows) موفق است.

## محدودیت امنیتی
- فایل محلی در همان failure domain قرار دارد و در برابر DBA/ادمین میزبان یا rollback کل فایل مقاومتی ادعا نمی‌کند.
- هیچ registration پیش‌فرض، fallback Production، migration یا customer DB change اضافه نشده است.
- Production همچنان به provider مستقل با credential جدا و retention/immutability نیاز دارد.

## کارهای باقیمانده
- [x] CI نهایی و regression قفل بین‌پردازه‌ای روی Ubuntu و Windows پس از Merge #633 تأیید شد.
- [ ] انتخاب و پیاده‌سازی provider مستقل Production، credential separation، retention/immutability، alerting و recovery.
- در صورت نیاز wiring صریح Development از config (بدون Production registration).


## 2026-10-31 — independent-process checkpoint regression
- Added a helper executable and a regression test launching eight independent .NET processes that concurrently append the same sequence to one development JSONL checkpoint journal. Exactly one append must succeed; the other seven must reject the duplicate; the final journal must contain one valid record.
- Added a Windows/Linux matrix to the existing Vendor CI workflow (rather than introducing a workflow that cannot run until merged).
- This verifies cross-process locking for the Development/Test adapter only. It does not create a Production checkpoint sink or satisfy independent failure-domain, external retention/immutability, alerting or recovery requirements.
- At the time this historical note was written, local .NET execution was unavailable and the matrix was pending; the later 2026-11-11 section records the successful Ubuntu/Windows CI evidence.


## 2026-11-11 — تأیید regression چندپردازه‌ای روی main

- مبنای کد: `main@3b103a5875a9668bf0c07d305a91685db3fb1db7`.
- Vendor CI run [42783806157](https://github.com/hamidreza1394/Accounting/actions/runs/42783806157) پس از Merge #633 هر سه Job را با موفقیت تکمیل کرد.
- Job `129719941495` روی Ubuntu و job `129719941498` روی Windows تست `AppendAcrossIndependentProcessesRejectsDuplicateSequenceWithoutCorruptingJournal` را اجرا کردند: هشت پردازه مستقل روی یک journal یک sequence یکسان را append می‌کنند؛ دقیقاً یک append باید موفق و هفت مورد دیگر duplicate را رد کنند.
- هر دو Job موفق و test summary هرکدام `failed: 0` و `skipped: 0` است. اجرای محلی در این نشست انجام نشده است.
- این نتیجه فقط adapter محلی Development/Test را Verified می‌کند. مقصد مستقل Production، credential جدا، immutability/retention خارجی، alerting و recovery drill همچنان NotImplemented/Blocked هستند؛ هیچ filesystem یا provider فرضی به Production متصل نشده است.
