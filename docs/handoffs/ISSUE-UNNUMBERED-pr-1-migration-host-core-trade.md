# PR-1 — Migration Host امن برای Core و Trade

- **مبنا:** `origin/main@40e125a57cfe700ef7cf5dd18a8e269963ba1b99`؛ شاخه `feat/pr-1-migration-host-core-trade`.
- **تغییر:** `DatabaseMigrationHost`/`EfContextMigrationRunner`/`TradeMigrationRunner` (Server/Infrastructure/Hosting)، اتصال به `--migrate` در `Program.cs`، پرچم صریح `--include-trade`.
- **تست:** `DatabaseMigrationHostSqlServerTests` (۸). نکتهٔ شفافیت: کد پیش از تست نوشته شد؛ برای جبران، ۴ جهش گاردها را شکستند (جزئیات در `docs/TESTING.md`).
- **باگ‌های یافت‌شده حین تست واقعی:** (۱) قفل روی دیتابیس ناموجود باز نمی‌شد ⇒ fallback به `master`؛ (۲) خطای ورود ۴۰۶۰ در Pool مشترک اجرای EF را مسموم می‌کرد ⇒ اتصال قفل بدون Pool؛ (۳) `CASE WHEN OBJECT_ID ... (SELECT FROM جدول ناموجود)` خطای ۲۰۸ می‌داد ⇒ دو Query.
- **Blocker:** ندارد. **Backlog:** هشدار Pending Trade هنگام شروع برنامه؛ اتصال خرید/فعال‌سازی Trade به `--include-trade`؛ مستندسازی/اجرای Baseline دیتابیس‌های قدیمی (نیازمند تصمیم مالک).
