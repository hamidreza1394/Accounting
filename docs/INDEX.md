# فهرست مستندات و ترتیب مطالعه

نقطه شروع همه Agentها و توسعه‌دهندگان. پیش از هر کار [`../AGENTS.md`](../AGENTS.md) را بخوانید.

## ترتیب منابع حقیقت (D-030)
۱) کد در Commit مبنا ← ۲) نتیجه CI/تست ← ۳) تصمیم صریح کارفرما ← ۴) PR ادغام‌شده/تاریخچه ← ۵) مستندات ← ۶) پیشنهاد Agentها. اختلاف کد با تصمیم تأییدشده «انحراف پیاده‌سازی» ثبت می‌شود.

## واژگان وضعیت
`Accepted` تصمیم پذیرفته‌شده؛ `Implemented` در کد هست؛ `Verified` با اجرای تست/CI شاهد دارد؛ `Proposed` پیشنهاد؛ `NeedsApproval` نیازمند تأیید کارفرما؛ `Deprecated` منسوخ؛ `Blocked` مسدود؛ `NotImplemented` در کد نیست. `Deferred` موکول‌شده؛ `Rejected` ردشده با دلیل (افزوده 2026-10-24).

## فهرست
| سند | کاربرد |
|---|---|
| [`PROJECT_STATUS.md`](PROJECT_STATUS.md) | وضعیت تأییدشده فعلی |
| [`ROADMAP.md`](ROADMAP.md) | برنامه راه |
| [`ARCHITECTURE.md`](ARCHITECTURE.md) | معماری و جهت وابستگی |
| [`WORK_CONTEXT_SELECTOR_UI.md`](WORK_CONTEXT_SELECTOR_UI.md) | طراحی بصری و رفتار صفحه انتخاب شرکت و دوره مالی (لوگو، فونت، مرور محصولات، اندازه‌گیری) |
| [`DECISIONS.md`](DECISIONS.md) | ADRها D-001..D-046 (بعدی D-047)؛ مرز مشتری و برچسب‌های رابطه: D-037..D-040؛ مرز سامانه مرکزی، فعال‌سازی، هویت فروشنده و گذار: D-041..D-044؛ قرارداد لایسنس: D-045؛ امنیت و عملیات فروشنده: D-046 |
| [`CORPORATE_GROUPS.md`](CORPORATE_GROUPS.md) | گروه شرکتی، گزارش تجمیعی/تلفیقی، مجوزها و نقشه راه (D-033..D-035) |
| [`CORPORATE_GROUPS_DATA_MODEL.md`](CORPORATE_GROUPS_DATA_MODEL.md) | جداول، کلیدها و راهبرد Migration گروه شرکتی |
| [`PRODUCT_REQUIREMENTS.md`](PRODUCT_REQUIREMENTS.md) | نیازمندی‌های محصول |
| [`REQUIREMENTS_TRACEABILITY.md`](REQUIREMENTS_TRACEABILITY.md) | ردیابی نیازمندی ← کد/تست |
| [`LICENSING.md`](LICENSING.md) | طرح لایسنس (`NotImplemented`) |
| [`LICENSING_DATA_MODEL.md`](LICENSING_DATA_MODEL.md) | ERD واقعی و پیشنهادی |
| [`LICENSING_WORKFLOWS.md`](LICENSING_WORKFLOWS.md) | گردش‌کارها |
| [`LICENSING_THREAT_MODEL.md`](LICENSING_THREAT_MODEL.md) | تهدیدها |
| [`LICENSING_OPEN_DECISIONS.md`](LICENSING_OPEN_DECISIONS.md) | تصمیم‌های باز لایسنس و سامانه مرکزی با گزینه‌ها، ریسک و پیشنهاد؛ پرسش‌های اولویت‌بندی‌شده برای مالک محصول (فاز صفر 2026-10-24) |
| [`DATABASE.md`](DATABASE.md), [`database-migrations.md`](database-migrations.md) | دیتابیس و Migration |
| [`DATABASE_SETUP.md`](DATABASE_SETUP.md) | راه‌اندازی اولیه دیتابیس، محل و رمزنگاری تنظیمات اتصال، مهاجرت TarazAva ← Farano، فرمان `--settings` (D-036) |
| [`SECURITY.md`](SECURITY.md) | امنیت |
| [`TESTING.md`](TESTING.md) | تست |
| [`DEPLOYMENT.md`](DEPLOYMENT.md), [`DEVELOPMENT.md`](DEVELOPMENT.md) | استقرار و توسعه |
| [`CODING_STANDARDS.md`](CODING_STANDARDS.md) | استانداردها |
| [`REPOSITORY_AUDIT.md`](REPOSITORY_AUDIT.md) | ممیزی و ناهماهنگی‌ها |
| [`CLOUD_CONCURRENCY_READINESS.md`](CLOUD_CONCURRENCY_READINESS.md) | ممیزی آمادگی بار ابری؛ ظرفیت ۱۰۰ کاربر فعلاً تأیید نشده |
| [`AGENT_COORDINATION.md`](AGENT_COORDINATION.md), [`AGENT_START_PROMPT.md`](AGENT_START_PROMPT.md), [`prompts/`](prompts/) , [`../CLAUDE.md`](../CLAUDE.md) | هماهنگی Agentها (شامل بخش «تقسیم کار بین Subagentهای Claude Code» و «Skill، Plugin و کنترل‌های قطعی»؛ تنظیمات Claude Code در [`../.claude/settings.json`](../.claude/settings.json)) |
| [`prompts/CLOUD_CONCURRENCY_IMPLEMENTATION_PROMPT.md`](prompts/CLOUD_CONCURRENCY_IMPLEMENTATION_PROMPT.md) | پرامپ اجرایی Issue #49 برای آزمون بار و اصلاح مبتنی بر شواهد |
| [`../.claude/skills/faranou-senior-developer/SKILL.md`](../.claude/skills/faranou-senior-developer/SKILL.md) | Skill اصلی توسعه فرانو؛ فناوری، امنیت، تست و کنترل تغییرات |
| [`../.claude/skills/faranou-architecture-review/SKILL.md`](../.claude/skills/faranou-architecture-review/SKILL.md), [`../.claude/skills/faranou-security-multitenancy/SKILL.md`](../.claude/skills/faranou-security-multitenancy/SKILL.md), [`../.claude/skills/faranou-testing-quality/SKILL.md`](../.claude/skills/faranou-testing-quality/SKILL.md) | Skillهای بازبینی معماری، امنیت چندشرکتی و کیفیت/تست |
| [`../.claude/skills/faranou-ui-ux-design/SKILL.md`](../.claude/skills/faranou-ui-ux-design/SKILL.md), [`../.claude/skills/faranou-accounting-domain/SKILL.md`](../.claude/skills/faranou-accounting-domain/SKILL.md) | Skillهای طراحی UI فارسی RTL و دامنه حسابداری |
| [`WORK_LOG.md`](WORK_LOG.md), [`handoffs/`](handoffs/) | سوابق تحویل |
