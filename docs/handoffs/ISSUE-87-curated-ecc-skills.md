# تحویل Issue #87 — انتخاب و سازگارسازی Skillهای ECC

## هدف
ارزیابی Skillهای پروژهٔ متن‌باز [Everything Claude Code](https://github.com/affaan-m/ECC) و واردکردن فقط مواردی که با معماری واقعی فرانو سازگارند.

## شواهد و مبنا
- مخزن رسمی ECC بررسی شد: `affaan-m/ECC`.
- درخت منبع بررسی‌شده: `90606c8315ee09cafc303515c5f33024db6b9882`.
- README فعلی ECC نصب Plugin رسمی را از نصب دستی جدا می‌کند و دربارهٔ تکرار نصب‌ها/Hookها هشدار می‌دهد.
- فرانو از قبل Skillهای اختصاصی برای توسعه عمومی، معماری، امنیت چندشرکتی، تست، UI/UX فارسی RTL و دامنه حسابداری دارد.
- Stack واقعی بررسی‌شده: .NET 10/C# 14، EF Core 10/SQL Server، API هم‌مبدأ `/api/v1` با Cookie و anti-forgery، React 19/TypeScript strict/Vite/Tailwind 4، Vitest/React Testing Library و Playwright در `tests/e2e`.

## تغییرات این شاخه
- هفت Skill از ECC وارد و با قواعد فرانو مهار شدند: `dotnet-patterns`, `csharp-testing`, `api-design`, `react-patterns`, `react-testing`, `e2e-testing`, `change-impact`.
- دو Skill مبتنی بر ایده‌های ECC به‌صورت اختصاصی بازنویسی شدند: `faranou-efcore-migrations` و `faranou-architecture-decisions`. اولی به EF Core/SQL Server واقعی و دومی به `docs/DECISIONS.md` موجود متصل است؛ هیچ نظام موازی `docs/adr/` ساخته نشد.
- منبع، SHA مبنا، انتخاب‌ها و محدودیت‌ها در `docs/THIRD_PARTY_SKILLS.md` ثبت و اعلان MIT در `third_party/ECC-LICENSE.txt` نگه‌داری شد.
- `CLAUDE.md` و `docs/INDEX.md` برای کشف Skillهای تازه به‌روزرسانی شدند.

## موارد عمداً نصب/وارد نشدند
- همهٔ Skillهای ECC به‌صورت انبوه وارد نشدند؛ تعداد زیاد می‌تواند زمینه را سنگین و توصیه‌های متعارض ایجاد کند.
- Hookهای اجرایی، MCP serverها، AgentShield، npm packageها، workflowها و dependencyهای جدید اجرا یا نصب نشدند.
- راهنمای PostgreSQL/Prisma/Drizzle از Skill مهاجرت عمومی کپی نشد؛ به‌جای آن، Skill مهاجرت مختص EF Core/SQL Server نوشته شد.
- نصب Plugin سراسری ECC در محیط شخصی Claude Code از طریق این تغییر مخزن انجام نمی‌شود؛ این یک چرخهٔ نصب جداگانه است.

## اعتبارسنجی
- منبع‌ها و فایل‌های پروژه با GitHub API خوانده شدند؛ ساختار و وابستگی‌های موجود بررسی شد.
- Build، تست .NET، تست React و E2E اجرا نشدند؛ تغییرات فقط مستندات/Skill هستند و نتیجهٔ CI روی HEAD جدید باید جداگانه بررسی شود.
- پیش از Merge، Diff، لینک‌ها، frontmatter و نتایج CI باید بازبینی شوند.

## لینک‌ها
- Issue: https://github.com/hamidreza1394/Accounting/issues/87
- Branch: `docs/issue-87-curated-ecc-skills`
