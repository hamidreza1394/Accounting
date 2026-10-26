# فرانو — نرم‌افزار حسابداری (ASP.NET Core Razor Pages + React)

مخزن نرم‌افزار حسابداری «فرانو»: پایه چندشرکتی (Multi-Tenant) روی ASP.NET Core و SQL Server با کلاینت React. **هنوز ماژول حسابداری (سند، سرفصل، گزارش) و سیستم لایسنس تجاری پیاده‌سازی نشده‌اند**؛ وضعیت واقعی در [docs/PROJECT_STATUS.md](docs/PROJECT_STATUS.md) و نقطه شروع مستندات در [docs/INDEX.md](docs/INDEX.md) است. مجوز استفاده (LICENSE) هنوز در مخزن تعیین نشده است (تصمیم مالک مخزن).

## فناوری‌ها
- ASP.NET Core Razor Pages + Minimal API (`/api/v1`) / .NET 10 LTS
- کلاینت React 19 + TypeScript (strict) + Vite + Tailwind CSS v4 در `ClientApp` (مسیر `/app`); استاندارد کامپوننت‌های جدید: shadcn/ui با پیکربندی RTL — راهنما: [ClientApp/README.md](ClientApp/README.md) و [استاندارد UI](docs/UI_COMPONENTS.md)
- SQL Server و EF Core 10
- مدیریت متمرکز نسخه NuGet با `Directory.Packages.props`
- تست پایه xUnit v3 و CI با GitHub Actions

> **محیط توسعه:** برای هدف‌گذاری رسمی `net10.0` از Visual Studio 2026 نسخه 18.0 یا بالاتر استفاده کنید. تنظیمات اتصال دیتابیس و مراحل توسعه در `docs/DEVELOPMENT.md` آمده است.

## شروع سریع
```bash
dotnet restore Template.sln
dotnet build Template.sln --configuration Release
dotnet test Template.sln --configuration Release
cd ClientApp && npm ci && npm run build && cd ..   # کلاینت React (اختیاری؛ بدون آن /app در دسترس نیست)
# انتشار Production (کلاینت + سرور + بررسی خروجی): scripts/publish.sh  یا  pwsh scripts/publish.ps1 — راهنما: docs/DEPLOYMENT.md
dotnet run --project Server/Server.csproj
```

تنظیمات Connection String و ورود را طبق `docs/DEVELOPMENT.md` تنظیم کنید. مقادیر محرمانه نباید در فایل‌های tracked ذخیره شوند.

## مستندات اصلی
- [فهرست و ترتیب مطالعه مستندات](docs/INDEX.md)
- [نیازمندی‌های محصول](docs/PRODUCT_REQUIREMENTS.md) و [ردیابی نیازمندی‌ها](docs/REQUIREMENTS_TRACEABILITY.md)
- [طرح لایسنس (NotImplemented)](docs/LICENSING.md)
- [دستورالعمل Agentها](AGENTS.md)
- [وضعیت پروژه](docs/PROJECT_STATUS.md)
- [نقشه راه](docs/ROADMAP.md)
- [تصمیمات معماری](docs/DECISIONS.md)
- [معماری هدف و وضعیت موجود](docs/ARCHITECTURE.md)
- [استانداردهای کدنویسی](docs/CODING_STANDARDS.md)
- [استاندارد کامپوننت‌های UI و shadcn/ui](docs/UI_COMPONENTS.md)
- [راهبرد تست](docs/TESTING.md)
- [راهنمای پایگاه داده](docs/DATABASE.md)
- [چک‌لیست امنیت](docs/SECURITY.md)
- [راهنمای توسعه محلی](docs/DEVELOPMENT.md)
- [گزارش تحویل Agentها](docs/WORK_LOG.md)
- [گزارش بررسی ایستای مخزن و ریسک‌ها](docs/REPOSITORY_AUDIT.md)
- [فهرست فایل‌های C# و خروجی اسکن ایستا](docs/CLASS_REVIEW_INVENTORY.csv)
- [قالب تحویل هر وظیفه](docs/handoffs/TEMPLATE.md)
- [پرامپ شروع کار Agentها](docs/AGENT_START_PROMPT.md)

## ساختار فعلی
پروژه وب `Server`، لایه `Domain`، اسکلت `Application` و پروژه `Infrastructure/Persistence` (با نام‌فضای قدیمی `Data`) از هم جدا شده‌اند. منطق کاربردی قدیمی هنوز به‌طور کامل از PageModelها منتقل نشده است؛ مهاجرت باقی‌مانده باید در PRهای کوچک و همراه تست انجام شود. جزئیات در `docs/ARCHITECTURE.md` آمده است.
