# Handoff — سریال‌سازی تست‌های HTTP دروازهٔ Trade (UNNUMBERED)

- **Issue:** UNNUMBERED. تغییر فقط تست؛ هیچ کد Production، Migration یا مستند معماری تغییر نکرد.
- **مشکل:** `TradeAccessHttpTests` و `TradeAccessGrantedHttpTests` هم‌زمان اجرا می‌شدند و هر دو کد مجوز یکسان را با «بخوان‌و‌درج» در `dbo.PermissionDefinitions` پایگاه مشترک SQL Server می‌نوشتند؛ نتیجه: شکست تصادفی با خطای 2601 روی `IX_PermissionDefinitions_Code`.
- **شاهد (واقعی، SQL Server 2022 کانتینری، `FARANOU_REQUIRE_SQLSERVER=1`):** روی `main` بدون هیچ تغییر دیگر، `--filter TradeAccess` در ۴ از ۴ اجرا یک شکست داشت (۲۷ تست؛ تست شکست‌خورده هر بار فرق می‌کرد). پس از اصلاح، `--filter Trade` در ۴ از ۴ اجرا ۷۷ تست، ۰ شکست.
- **اصلاح:** هر دو کلاس در یک Collection (`TradeHttp.CollectionName`) قرار گرفتند تا پشت‌سرهم اجرا شوند؛ فایل: `tests/Template.Tests/Integration/TradeAccessHttpTests.cs`.
- **توجه:** نتیجهٔ «۷۶ موفق» در ممیزی S0-4 پیش از این کشف بود و این رقابت را نمی‌دید؛ اجرای قبلی احتمالاً از شانس سبز شده بود.
- **اجرا نشد:** تست کامل `Template.sln`، ClientApp، E2E (لازم نبود).
