# تحویل وظیفه — ISSUE-384-restart-safe-license-activation

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/384
- **Agent/مسئول:** ChatGPT
- **تاریخ:** 2026-10-31
- **وضعیت:** مستندات تصمیم ثبت شد؛ پیاده‌سازی آغاز نشده

## هدف و معیارهای پذیرش
- [x] ثبت تصمیم یک‌بار فعال‌سازی و عدم درخواست مجدد پس از restart.
- [x] ثبت الزام reactivation پس از تغییر OS-installation identity ویندوز.
- [x] ثبت کنترل server-side برای Cloud.
- [ ] طراحی نهایی secure key provider و OS identity طبق OD-08.
- [ ] پیاده‌سازی persistence، Migration، import/replace و enforcement.
- [ ] تست واحد، SQL Server integration و CI.

## خلاصه تغییرات
- D-058 و OD-18 ثبت شدند.
- Issue #384 ایجاد شد.
- رفتار هدف در LICENSING، مدل داده، threat model، وضعیت پروژه و WORK_LOG ثبت شد.

## فایل‌های مهم تغییرکرده
- docs/DECISIONS.md — D-058.
- docs/LICENSING.md — وضعیت و رفتار محصول.
- docs/LICENSING_OPEN_DECISIONS.md — OD-18 و پیوند به OD-08/OD-06.
- docs/LICENSING_DATA_MODEL.md — مدل مفهومی InstallationIdentity/InstalledLicense.
- docs/LICENSING_THREAT_MODEL.md — T31–T34.
- docs/PROJECT_STATUS.md و docs/WORK_LOG.md — وضعیت و تاریخچه.

## تصمیم‌های فنی
- Restart صرفاً reload و cryptographic revalidation است، نه re-import.
- OS reinstall identity change باید reactivation ایجاد کند؛ patch/servicing نباید.
- MachineGuid به‌تنهایی هویت امن نیست؛ secure key binding لازم است.
- Cloud تصمیم را server-side می‌گیرد؛ UI مرجع نیست.
- OD-08 و OD-06 هنوز بازند.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| بررسی اسناد و ADRهای لایسنس | انجام شد | GitHub main files بررسی شدند |
| Build/Test/SQL Server | اجرا نشده | این تغییر فقط مستندات است |
| Migration/TPM hardware | اجرا نشده | پیاده‌سازی هنوز آغاز نشده |

## امنیت، داده و سازگاری
- در این تغییر هیچ schema، Migration، secret یا private key تغییر نکرد.
- مدل نهایی persistence باید با OD-08 و D-057 سازگار باشد.
- D-046 دربارهٔ اختیاری بودن ارتباط آنلاین برای نصب اختصاصی پابرجاست.

## ریسک‌ها و کارهای باقی‌مانده
- انتخاب provider کلید نصب و recovery.
- تعیین marker دقیق OS installation و رفتار false-positive/false-negative.
- پیاده‌سازی InstalledLicense persistence و server-side enforcement.
- تست Windows/Linux و دو میزبان در انتهای پروژه.

## شناسه‌های Git
- Branch: docs/issue-384-restart-safe-license-activation
- Commit: در PR درج می‌شود.
- Pull Request: در انتظار ایجاد.
