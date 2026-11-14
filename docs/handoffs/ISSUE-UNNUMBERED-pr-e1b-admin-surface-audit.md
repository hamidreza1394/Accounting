# PR-E1b — ممیزی امنیت صفحات مدیریتی و خروجی اطلاعات

- **مبنا:** `origin/main@a4ffdfffcbfabb8cd5a1e62ab3d9c70f4b7240d7`؛ شاخه `fix/pr-e1b-admin-surface-audit`.
- **تغییر:** `Server/Pages/Admin/FileManager/Index.cshtml.cs` (حبس مسیرها در ریشهٔ `wwwroot`، جداکنندهٔ مستقل از سکو).
- **تست:** `FileManagerPathBoundaryTests` (۱۸ تست)، `SecurityAuditAdministrationSqlServerTests` (+۱: Export توسط غیرمدیر رد می‌شود، Export با Actor بدون نام موفق است).
- **صداقت RED:** کد قدیمی روی Linux به‌خاطر `\` ثابت هرگز عبور از `..` را اجرا نمی‌کرد؛ تست‌های «داخل ریشه کار می‌کند» روی Linux قرمز بودند (۴ شکست) و تست‌های عبور از ریشه محافظ رگرسیون‌اند (نمونهٔ Windows اجرا نشد).
- **بدون:** Schema/Migration/Tenant/Backfill/تغییر نقش Admin سراسری. موارد OD-1 در `docs/SECURITY.md` ثبت شد.
