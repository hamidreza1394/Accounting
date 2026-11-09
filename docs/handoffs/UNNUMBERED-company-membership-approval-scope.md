# تحویل تصمیم D-078 — دامنه تأیید عضویت داخلی و بین‌شرکتی

- **Issue:** UNNUMBERED؛ Issue اجرایی جداگانه هنوز ایجاد نشده است.
- **تاریخ:** 2026-11-09.
- **وضعیت تصمیم:** Accepted با تأیید صریح مالک محصول.
- **وضعیت کد:** NotVerified؛ این PR فقط مستندات را تغییر می‌دهد.

## مسئله
برخی خلاصه‌های امنیتی، تأیید عضویت را به تمام عضویت‌های جدید تعمیم داده بودند. تصمیم مالک محصول این است که مدیر مجاز هر شرکت کاربران شرکت خودش را بدون تأیید دوباره تعریف کند؛ تأیید مقصد فقط برای عضویت بین‌شرکتی، مانند درخواست هلدینگ برای شرکت زیرمجموعه، لازم است.

## تصمیم
1. مدیر دارای مجوزهای لازم در Company جاری، هنگام ایجاد کاربر برای همان Company، عضویت همان شرکت را خودکار Approved/Active می‌کند.
2. عضویت در Company دیگر، حتی در همان CorporateGroup، PendingApproval است و فقط مدیر مجاز مقصد می‌تواند approve/reject کند.
3. مالکیت هلدینگ یا عضویت گروهی به‌تنهایی اختیار ایجاد/تأیید عضویت در زیرمجموعه نمی‌دهد.
4. self-approval برای درخواست‌های بین‌شرکتی ممنوع می‌ماند؛ مسیر داخلی use case مجاز مستقلی است، نه bypass endpoint approval.
5. عضویت فقط رابطه User/Company است؛ Role، Permission، Entitlement یا دسترسی مالی نمی‌سازد.
6. دسترسی به سال/دوره دیگر در همان Company با تخصیص نقش و grant طبق D-065/D-066/D-077 انجام می‌شود؛ Membership جدید یا تأیید مجدد نمی‌خواهد.
7. یکتایی عضویت، کنترل Company سمت سرور، D-071، Audit، anti-forgery، concurrency و fail-closed حفظ می‌شوند.

## فایل‌های مستندات
- `docs/DECISIONS.md`: افزودن D-078.
- `docs/USER_ACCESS_MANAGEMENT.md`: قواعد workflow.
- `docs/ACCESS_CONTROL_ARCHITECTURE.md`: قواعد scope عضویت.
- `docs/SECURITY.md`: اصلاح خلاصه امنیتی.
- `docs/api/USER_ACCESS_MANAGEMENT_API.md`: تفکیک مسیر داخلی از API بین‌شرکتی، بدون ادعای endpoint جدید.
- `docs/INDEX.md` و `docs/WORK_LOG.md`: ارجاع و سابقه.

## کنترل‌های پیاده‌سازی
- مسیر create-user باید Company جاری را از context معتبر سرور بگیرد و مجوزهای actor را بررسی کند.
- مسیر خودکار داخلی نباید از endpoint approve عبور کند.
- مسیر بین‌شرکتی و منع self-approval دست‌نخورده بمانند.
- در صورت وجود User، حساب/Employee تکراری ساخته نشود.
- User و Membership داخلی از نظر تراکنش/جبران شکست سازگار باشند.
- تست‌های SQL Server/API، same-company auto-approval، cross-company Pending، holding/subsidiary، actor بدون اختیار مقصد، self-approval، duplicate/race و عدم اعطای role/grant را پوشش دهند.
- دسترسی سال/دوره در همان شرکت بدون Membership جدید و با policy صریح grant کنترل شود.

## اعتبارسنجی
فقط مستندات تغییر می‌کنند. Build، تست runtime، SQL Server و E2E اجرا نشده‌اند. پیش از پیاده‌سازی، endpointهای create-user و membership موجود باید با D-078 تطبیق داده شوند؛ هر اختلاف به Issue/PR اجرایی مستقل برود.

## بازبینی
- [x] تصمیم محصولی صریح ثبت شد.
- [x] مرز same-company / cross-company روشن شد.
- [x] تفکیک عضویت از دسترسی سال/دوره ثبت شد.
- [x] وضعیت runtime به‌اشتباه Implemented اعلام نشد.
- [ ] پیاده‌سازی و تست SQL Server/E2E در PR اجرایی بعدی.
