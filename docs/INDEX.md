# فهرست مستندات و ترتیب مطالعه

نقطه شروع همه Agentها و توسعه‌دهندگان. پیش از هر کار [`../AGENTS.md`](../AGENTS.md) را بخوانید.

## ترتیب منابع حقیقت (D-030)
۱) کد در Commit مبنا ← ۲) نتیجه CI/تست ← ۳) تصمیم صریح کارفرما ← ۴) PR ادغام‌شده/تاریخچه ← ۵) مستندات ← ۶) پیشنهاد Agentها. اختلاف کد با تصمیم تأییدشده «انحراف پیاده‌سازی» ثبت می‌شود.

## واژگان وضعیت
`Accepted` تصمیم پذیرفته‌شده؛ `Implemented` در کد هست؛ `Verified` با اجرای تست/CI شاهد دارد؛ `Proposed` پیشنهاد؛ `NeedsApproval` نیازمند تأیید کارفرما؛ `Deprecated` منسوخ؛ `Blocked` مسدود؛ `NotImplemented` در کد نیست. `Deferred` موکول‌شده؛ `Rejected` ردشده با دلیل (افزوده 2026-10-24).

## فهرست

| [AGENT_SKILL_LIBRARY.md](AGENT_SKILL_LIBRARY.md) | کتابخانه مهارت‌های مشترک Agentها و قواعد استفاده از VoltAgent |
| سند | کاربرد |
|---|---|
| [TRADE_MODULE.md](TRADE_MODULE.md) | معماری، امنیت چندشرکتی، استفاده مشترک از دوره‌های مالی، فرم‌ها، گزارش‌ها و مسیر استخراج ماژول بازرگانی (D-061/D-062) |
| [`PROJECT_STATUS.md`](PROJECT_STATUS.md) | وضعیت تأییدشده فعلی |
| [`ROADMAP.md`](ROADMAP.md) | برنامه راه |
| [`ARCHITECTURE.md`](ARCHITECTURE.md) | معماری و جهت وابستگی |
| [`WORK_CONTEXT_SELECTOR_UI.md`](WORK_CONTEXT_SELECTOR_UI.md) | طراحی بصری و رفتار صفحه انتخاب شرکت و دوره مالی (لوگو، فونت، مرور محصولات، اندازه‌گیری) |
| [`DECISIONS.md`](DECISIONS.md) | ADRها D-001..D-062 در DECISIONS.md؛ D-059: entitlement مستقل Company/Module/Feature/Seat، add-on با amendment، bootstrap مدیر Offline و sync دوحالته؛ D-055 و D-056 جداگانه در اسناد اختصاصی Issue #236 (D-055: معماری PKCS#11؛ D-056: SoftHSM برای Development/Staging و Software assurance با پذیرش ریسک صریح در Production؛ مرحلهٔ ۲ با HSM واقعی؛ D-057: TPM 2.0 NV Extend hash-chain برای anti-rollback وضعیت ابطال آفلاین مشتری (Issue #364)؛ D-058: فعال‌سازی پایدار، هویت نصب ویندوز و کنترل server-side ابری (Issue #384)؛ مرز مشتری و برچسب‌های رابطه: D-037..D-040؛ مرز سامانه مرکزی، فعال‌سازی، هویت فروشنده و گذار: D-041..D-044؛ قرارداد لایسنس: D-045؛ امنیت و عملیات فروشنده: D-046؛ معماری اجرایی Vendor: D-047 (Accepted; implementation staged and not Production-ready)؛ shadcn/ui: D-048؛ کتابخانه‌های جدول/نمودار/تاریخ، آفلاین‌بودن دارایی‌ها و قواعد طراحی/سرعت: D-049؛ استفادهٔ مشروط از بخش‌های رایگان KendoReact: D-050 |
| [`CORPORATE_GROUPS.md`](CORPORATE_GROUPS.md) | گروه شرکتی، گزارش تجمیعی/تلفیقی، مجوزها و نقشه راه (D-033..D-035) |
| [`CORPORATE_GROUPS_DATA_MODEL.md`](CORPORATE_GROUPS_DATA_MODEL.md) | جداول، کلیدها و راهبرد Migration گروه شرکتی |
| [`PRODUCT_REQUIREMENTS.md`](PRODUCT_REQUIREMENTS.md) | نیازمندی‌های محصول |
| [`REQUIREMENTS_TRACEABILITY.md`](REQUIREMENTS_TRACEABILITY.md) | ردیابی نیازمندی ← کد/تست |
| [`LICENSING.md`](LICENSING.md) | معماری و وضعیت لایسنس و فعال‌سازی |
| [`LICENSING_IMPLEMENTATION_PLAN.md`](LICENSING_IMPLEMENTATION_PLAN.md) | مرجع اجرایی D-059/D-060 و ترتیب Issues #389–#396 |
| [`LICENSING_DATA_MODEL.md`](LICENSING_DATA_MODEL.md) | ERD واقعی و پیشنهادی |
| [`LICENSING_WORKFLOWS.md`](LICENSING_WORKFLOWS.md) | گردش‌کارها |
| [`LICENSING_THREAT_MODEL.md`](LICENSING_THREAT_MODEL.md) | تهدیدها |
| [`LICENSING_OPEN_DECISIONS.md`](LICENSING_OPEN_DECISIONS.md) | تصمیم‌های باز لایسنس و سامانه مرکزی با گزینه‌ها، ریسک و پیشنهاد؛ پرسش‌های اولویت‌بندی‌شده برای مالک محصول (فاز صفر 2026-10-24) |
| [`DATABASE.md`](DATABASE.md), [`database-migrations.md`](database-migrations.md) | دیتابیس و Migration |
| [`DATABASE_SETUP.md`](DATABASE_SETUP.md) | راه‌اندازی اولیه دیتابیس، محل و رمزنگاری تنظیمات اتصال، مهاجرت TarazAva ← Farano، فرمان `--settings` (D-036) |
| [`SECURITY.md`](SECURITY.md) | امنیت |
| [`VENDOR_BOOTSTRAP.md`](VENDOR_BOOTSTRAP.md) | راهنمای عملیاتی command-mode برای راه‌اندازی اولین مدیر Vendor، TOTP و بازیابی pending |
| [`runbooks/vendor-audit-checkpoint-recovery.md`](runbooks/vendor-audit-checkpoint-recovery.md) | رویهٔ provider-neutral برای checkpoint مستقل Audit، backup/restore و recovery؛ تا انتخاب مقصد واقعی، Production-ready نیست |
| [`runbooks/ISSUE-236-HSM-TPM-KEY-OPERATIONS.md`](runbooks/ISSUE-236-HSM-TPM-KEY-OPERATIONS.md) | Key Ceremony، کنترل دو نفره، rotation/revocation، Backup/Recovery، TPM مشتری و alerting؛ اجرای Production تا تأمین HSM واقعی Blocked است |
| [`TESTING.md`](TESTING.md) | تست |
| [`DEPLOYMENT.md`](DEPLOYMENT.md), [`DEVELOPMENT.md`](DEVELOPMENT.md) | استقرار و توسعه |
| [`CODING_STANDARDS.md`](CODING_STANDARDS.md) | استانداردها |
| [`UI_COMPONENTS.md`](UI_COMPONENTS.md) | هستهٔ قواعد UI: shadcn/ui، RTL فارسی، دسترس‌پذیری، runtime آفلاین، قانون محلی‌سازی دارایی‌ها و قواعد طراحی/سرعت/ویژوال (D-048، D-049) |
| [`UI_LIBRARIES.md`](UI_LIBRARIES.md) | مرجع کتابخانه‌ها: جدول/نمودار/تاریخ، استفادهٔ مشروط از KendoReact Free (D-050)، گزارش پیوت و راهنمای مهارت `ui-ux-pro-max`؛ هنگام انتخاب یا افزودن کتابخانه بخوان |
| [`REPOSITORY_AUDIT.md`](REPOSITORY_AUDIT.md) | ممیزی و ناهماهنگی‌ها |
| [`CLOUD_CONCURRENCY_READINESS.md`](CLOUD_CONCURRENCY_READINESS.md) | ممیزی آمادگی بار ابری؛ ظرفیت ۱۰۰ کاربر فعلاً تأیید نشده |
| [`AGENT_COORDINATION.md`](AGENT_COORDINATION.md), [`AGENT_START_PROMPT.md`](AGENT_START_PROMPT.md), [`prompts/`](prompts/), [`../CLAUDE.md`](../CLAUDE.md) | هماهنگی Agentها (شامل بخش «تقسیم کار بین Subagentهای Claude Code» و «Skill، Plugin و کنترل‌های قطعی»؛ تنظیمات Claude Code در [`../.claude/settings.json`](../.claude/settings.json)) |
| [`AGENT_REFERENCE.md`](AGENT_REFERENCE.md) | مرجع تکمیلی Claude Code منتقل‌شده از `CLAUDE.md` (بدون تغییر محتوا، 2026-10-27): جدول تیم Subagent، نحوهٔ فراخوانی، نگاشت گردش‌کار، جدول انتخاب Skill، کنترل‌های قطعی؛ فقط در صورت نیاز بخوان |
| [`prompts/CLOUD_CONCURRENCY_IMPLEMENTATION_PROMPT.md`](prompts/CLOUD_CONCURRENCY_IMPLEMENTATION_PROMPT.md) | پرامپ اجرایی Issue #49 برای آزمون بار و اصلاح مبتنی بر شواهد |
| [`../.claude/skills/faranou-senior-developer/SKILL.md`](../.claude/skills/faranou-senior-developer/SKILL.md) | Skill اصلی توسعه فرانو؛ فناوری، امنیت، تست و کنترل تغییرات |
| [`../.claude/skills/faranou-architecture-review/SKILL.md`](../.claude/skills/faranou-architecture-review/SKILL.md), [`../.claude/skills/faranou-security-multitenancy/SKILL.md`](../.claude/skills/faranou-security-multitenancy/SKILL.md), [`../.claude/skills/faranou-testing-quality/SKILL.md`](../.claude/skills/faranou-testing-quality/SKILL.md) | Skillهای بازبینی معماری، امنیت چندشرکتی و کیفیت/تست |
| [`../.claude/skills/faranou-ui-ux-design/SKILL.md`](../.claude/skills/faranou-ui-ux-design/SKILL.md), [`../.claude/skills/faranou-accounting-domain/SKILL.md`](../.claude/skills/faranou-accounting-domain/SKILL.md) | Skillهای طراحی UI فارسی RTL و دامنه حسابداری |
| [`../.claude/skills/dotnet-patterns/SKILL.md`](../.claude/skills/dotnet-patterns/SKILL.md), [`../.claude/skills/csharp-testing/SKILL.md`](../.claude/skills/csharp-testing/SKILL.md), [`../.claude/skills/api-design/SKILL.md`](../.claude/skills/api-design/SKILL.md) | Skillهای انتخابی ECC برای .NET، تست C# و API |
| [`../.claude/skills/react-patterns/SKILL.md`](../.claude/skills/react-patterns/SKILL.md), [`../.claude/skills/react-testing/SKILL.md`](../.claude/skills/react-testing/SKILL.md), [`../.claude/skills/e2e-testing/SKILL.md`](../.claude/skills/e2e-testing/SKILL.md) | Skillهای React، تست کلاینت و Playwright |
| [`../.claude/skills/change-impact/SKILL.md`](../.claude/skills/change-impact/SKILL.md), [`../.claude/skills/faranou-efcore-migrations/SKILL.md`](../.claude/skills/faranou-efcore-migrations/SKILL.md), [`../.claude/skills/faranou-architecture-decisions/SKILL.md`](../.claude/skills/faranou-architecture-decisions/SKILL.md) | تحلیل اثر تغییرات، Migrationهای EF Core و تصمیمات معماری |
| [`THIRD_PARTY_SKILLS.md`](THIRD_PARTY_SKILLS.md), [`../THIRD_PARTY_NOTICES.md`](../THIRD_PARTY_NOTICES.md), [`../third_party/MICROSOFT-TSS-LICENSE.txt`](../third_party/MICROSOFT-TSS-LICENSE.txt), [`../third_party/ECC-LICENSE.txt`](../third_party/ECC-LICENSE.txt), [`../third_party/UI-UX-PRO-MAX-LICENSE.txt`](../third_party/UI-UX-PRO-MAX-LICENSE.txt), [`../.claude/skills/ui-ux-pro-max/SKILL.md`](../.claude/skills/ui-ux-pro-max/SKILL.md) | منشأ، مجوز و قواعد نگهداری Skillها و کتابخانه‌های شخص ثالث |
| [`handoffs/ISSUE-214-vendor-revocation-issuance.md`](handoffs/ISSUE-214-vendor-revocation-issuance.md) | تحویل پیاده‌سازی Vendor برای snapshot امضاشده و گردش maker-checker ابطال آفلاین؛ CI شاخه باید پیش از Verified بررسی شود |
| [`WORK_LOG.md`](WORK_LOG.md), [`handoffs/`](handoffs/) | سوابق تحویل |
