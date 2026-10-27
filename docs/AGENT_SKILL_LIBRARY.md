# کتابخانه مهارت‌های Agent — فرانو و Vendor

این سند نقطهٔ مرجع مشترک برای عامل‌های کدنویسی است. این فهرست به معنی نصب شدن ابزار یا اجرای خودکار همهٔ مهارت‌ها نیست. عامل باید برای هر وظیفه مهارت مرتبط را انتخاب کند، منبع آن را بررسی کند و شواهد اجرای واقعی را گزارش دهد.

## کتابخانهٔ اصلی کشف مهارت‌ها
- VoltAgent Awesome Agent Skills: https://github.com/VoltAgent/awesome-agent-skills
- این مخزن یک فهرست گزینش‌شده است، نه تضمین ممیزی امنیتی همهٔ مهارت‌ها. مهارت‌ها ممکن است تغییر کنند؛ پیش از اتکا به آن‌ها، منبع اصلی و فایل SKILL.md و منابع وابسته را بازبینی کنید.
- دستورالعمل عملیاتی مشترک: .agents/skills/faranou-engineering/SKILL.md (نسخه‌های سازگار برای Claude Code، GitHub Copilot و Cursor نیز نگهداری می‌شوند).

## ماتریس انتخاب مهارت

| دسته | مهارت‌های اولویت‌دار | زمان استفاده |
|---|---|---|
| Backend | openai/aspnet-core | ASP.NET Core 10، Minimal API، Razor Pages، DI، EF Core، Identity |
| امنیت | openai/security-best-practices, openai/security-threat-model, trailofbits/insecure-defaults, trailofbits/static-analysis, trailofbits/differential-review, trailofbits/sharp-edges | هر تغییر حساس، auth, multi-tenancy, licensing, secrets و بررسی diff |
| Frontend | openai/frontend-skill, anthropics/frontend-design, google-labs-code/shadcn-ui, microsoft/frontend-design-review, anthropics/web-artifacts-builder | React, TypeScript, Tailwind, shadcn/ui, RTL, accessibility |
| Performance | cloudflare/web-perf, microsoft/azure-monitor-opentelemetry-ts, netlify/netlify-caching | اندازه‌گیری CWV، telemetry و caching با رعایت جداسازی tenant |
| Testing / API / CI | testmu-ai/xunit-skill, testmu-ai/vitest-skill, testmu-ai/playwright-skill, testmu-ai/api-skill, testmu-ai/cicd-pipeline-skill, openai/gh-fix-ci, openai/gh-address-comments, coderabbitai/code-review, coderabbitai/autofix | تست Backend و Client، E2E، قرارداد API، عیب‌یابی CI و بازبینی PR |
| Database / Deploy | microsoft/azure-resource-manager-sql-dotnet, supabase/postgres-best-practices, openai/vercel-deploy, openai/netlify-deploy, openai/render-deploy | فقط در صورت استفاده واقعی از سرویس مربوط؛ اصول PostgreSQL قبل از اعمال در SQL Server باید تطبیق داده شوند |
| Identity | auth0/auth0-aspnetcore-api, auth0/auth0-react, microsoft/azure-identity-dotnet, microsoft/azure-security-keyvault-secrets-dotnet | فقط وقتی Auth0، Entra ID یا Key Vault بخشی از معماری پذیرفته‌شده باشد |

## قرارداد گزارش‌دهی
در ابتدای گزارش فنی، دسته و مهارت مرتبط را نام ببر. برای هر مورد مشخص کن:
- **منبع خوانده شد:** فایل اصلی/مرجع دقیق بررسی شده است.
- **راهنمایی اعمال شد:** اصول مرتبط در تحلیل یا تغییر لحاظ شده‌اند.
- **اجرا شد:** فرمان/ابزار واقعاً اجرا شده و نتیجه قابل مشاهده دارد.
- **محدودیت:** اگر منبع در دسترس نبود، از دانش عمومی استفاده شد و نباید ادعای خواندن آن منبع کرد.

## اشتراک‌گذاری با عامل‌های مختلف
- Codex / ابزارهای سازگار با AGENTS: AGENTS.md و .agents/skills/
- Claude Code: CLAUDE.md و .claude/skills/
- GitHub Copilot: .github/copilot-instructions.md و .github/skills/
- Cursor: .cursor/rules/ و .cursor/skills/

این فایل‌ها باعث می‌شوند عامل‌هایی که مخزن را می‌خوانند دستورالعمل‌ها را کشف کنند؛ اما نمی‌توان از داخل مخزن تضمین کرد همهٔ مدل‌ها یا سرویس‌های متصل به پروژه آن را خودکار بخوانند. برای ابزارهای خارجی باید دسترسی مخزن و پشتیبانی آن ابزار از دستورالعمل‌های پروژه تنظیم شود. هیچ مهارتی نصب یا اجراشده محسوب نمی‌شود مگر واقعاً انجام نشده باشد.
