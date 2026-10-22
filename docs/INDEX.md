# فهرست مستندات و ترتیب مطالعه

نقطه شروع همه Agentها و توسعه‌دهندگان. پیش از هر کار [`../AGENTS.md`](../AGENTS.md) را بخوانید.

## ترتیب منابع حقیقت (D-030)
۱) کد در Commit مبنا ← ۲) نتیجه CI/تست ← ۳) تصمیم صریح کارفرما ← ۴) PR ادغام‌شده/تاریخچه ← ۵) مستندات ← ۶) پیشنهاد Agentها. اختلاف کد با تصمیم تأییدشده «انحراف پیاده‌سازی» ثبت می‌شود.

## واژگان وضعیت
`Accepted` تصمیم پذیرفته‌شده؛ `Implemented` در کد هست؛ `Verified` با اجرای تست/CI شاهد دارد؛ `Proposed` پیشنهاد؛ `NeedsApproval` نیازمند تأیید کارفرما؛ `Deprecated` منسوخ؛ `Blocked` مسدود؛ `NotImplemented` در کد نیست.

## فهرست
| سند | کاربرد |
|---|---|
| [`PROJECT_STATUS.md`](PROJECT_STATUS.md) | وضعیت تأییدشده فعلی |
| [`ROADMAP.md`](ROADMAP.md) | برنامه راه |
| [`ARCHITECTURE.md`](ARCHITECTURE.md) | معماری و جهت وابستگی |
| [`DECISIONS.md`](DECISIONS.md) | ADRها D-001..D-030 (بعدی D-031) |
| [`PRODUCT_REQUIREMENTS.md`](PRODUCT_REQUIREMENTS.md) | نیازمندی‌های محصول |
| [`REQUIREMENTS_TRACEABILITY.md`](REQUIREMENTS_TRACEABILITY.md) | ردیابی نیازمندی ← کد/تست |
| [`LICENSING.md`](LICENSING.md) | طرح لایسنس (`NotImplemented`) |
| [`LICENSING_DATA_MODEL.md`](LICENSING_DATA_MODEL.md) | ERD واقعی و پیشنهادی |
| [`LICENSING_WORKFLOWS.md`](LICENSING_WORKFLOWS.md) | گردش‌کارها |
| [`LICENSING_THREAT_MODEL.md`](LICENSING_THREAT_MODEL.md) | تهدیدها |
| [`DATABASE.md`](DATABASE.md), [`database-migrations.md`](database-migrations.md) | دیتابیس و Migration |
| [`SECURITY.md`](SECURITY.md) | امنیت |
| [`TESTING.md`](TESTING.md) | تست |
| [`DEPLOYMENT.md`](DEPLOYMENT.md), [`DEVELOPMENT.md`](DEVELOPMENT.md) | استقرار و توسعه |
| [`CODING_STANDARDS.md`](CODING_STANDARDS.md) | استانداردها |
| [`REPOSITORY_AUDIT.md`](REPOSITORY_AUDIT.md) | ممیزی و ناهماهنگی‌ها |
| [`CLOUD_CONCURRENCY_READINESS.md`](CLOUD_CONCURRENCY_READINESS.md) | ممیزی آمادگی بار ابری؛ ظرفیت ۱۰۰ کاربر فعلاً تأیید نشده |
| [`AGENT_COORDINATION.md`](AGENT_COORDINATION.md), [`AGENT_START_PROMPT.md`](AGENT_START_PROMPT.md), [`prompts/`](prompts/) | هماهنگی Agentها (شامل بخش «تقسیم کار بین Subagentهای Claude Code») |
| [`prompts/CLOUD_CONCURRENCY_IMPLEMENTATION_PROMPT.md`](prompts/CLOUD_CONCURRENCY_IMPLEMENTATION_PROMPT.md) | پرامپ اجرایی Issue #49 برای آزمون بار و اصلاح مبتنی بر شواهد |
| [`WORK_LOG.md`](WORK_LOG.md), [`handoffs/`](handoffs/) | سوابق تحویل |
