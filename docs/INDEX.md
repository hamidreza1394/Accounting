# فهرست مستندات و ترتیب مطالعه

نقطه شروع همه Agentها و توسعه‌دهندگان. پیش از هر کار [`../AGENTS.md`](../AGENTS.md) را بخوانید.

## ترتیب منابع حقیقت (D-030)
۱) کد در Commit مبنا ← ۲) نتیجه CI/تست ← ۳) تصمیم صریح کارفرما ← ۴) PR ادغام‌شده/تاریخچه ← ۵) مستندات ← ۶) پیشنهاد Agentها. اختلاف کد با تصمیم تأییدشده باید به‌عنوان باگ ثبت شود.

## نقشه مطالعه
| سند | نقش |
|---|---|
| [`../AGENTS.md`](../AGENTS.md) | قواعد الزامی Agentها، ممنوعیت‌ها، قالب تحویل |
| [`ARCHITECTURE.md`](ARCHITECTURE.md) | معماری لایه، مرز ماژول‌ها، چندشرکتی |
| [`ACCESS_CONTROL_ARCHITECTURE.md`](ACCESS_CONTROL_ARCHITECTURE.md) | مدل دسترسی، نقش، عضویت، D-064/D-065 |
| [`ADMIN_UI.md`](ADMIN_UI.md) | UI مدیریت سیستم، صفحات و قراردادها |
| [`DECISIONS.md`](DECISIONS.md) | ADRها و تصمیم‌های معماری |
| [`PROJECT_STATUS.md`](PROJECT_STATUS.md) | وضعیت فعلی و کارهای باز |
| [`CODING_STANDARDS.md`](CODING_STANDARDS.md) | استانداردهای کد |
| [`DATABASE.md`](DATABASE.md) / [`DATABASE_SETUP.md`](DATABASE_SETUP.md) | مدل داده و راه‌اندازی |
| [`DEVELOPMENT.md`](DEVELOPMENT.md) | محیط توسعه محلی |
| [`DEPLOYMENT.md`](DEPLOYMENT.md) | استقرار |
| [`PRODUCT_REQUIREMENTS.md`](PRODUCT_REQUIREMENTS.md) | الزامات محصول |
| [`AGENT_COORDINATION.md`](AGENT_COORDINATION.md) / [`AGENT_REFERENCE.md`](AGENT_REFERENCE.md) | هماهنگی و مرجع Agent |
| [`AGENT_START_PROMPT.md`](AGENT_START_PROMPT.md) | پرامپت شروع |
| [`AGENT_SKILL_LIBRARY.md`](AGENT_SKILL_LIBRARY.md) | کتابخانه مهارت |
| [`REPOSITORY_AUDIT.md`](REPOSITORY_AUDIT.md) | ممیزی مخزن |
| [`CORPORATE_GROUPS.md`](CORPORATE_GROUPS.md) / [`CORPORATE_GROUPS_DATA_MODEL.md`](CORPORATE_GROUPS_DATA_MODEL.md) | گروه‌های شرکتی |
| [`ORGANIZATIONAL_STRUCTURE_DATA_MODEL.md`](ORGANIZATIONAL_STRUCTURE_DATA_MODEL.md) | ساختار سازمانی |
| [`LICENSING*.md`](LICENSING.md) | مجوزدهی |
| [`CLOUD_CONCURRENCY_READINESS.md`](CLOUD_CONCURRENCY_READINESS.md) | آمادگی همزمانی ابری |
| [`prompts/CLOUD_CONCURRENCY_IMPLEMENTATION_PROMPT.md`](prompts/CLOUD_CONCURRENCY_IMPLEMENTATION_PROMPT.md) | پرامپ اجرایی Issue #49 برای آزمون بار و اصلاح مبتنی بر شواهد |
| [`../.claude/skills/faranou-senior-developer/SKILL.md`](../.claude/skills/faranou-senior-developer/SKILL.md) | Skill اصلی توسعه فرانو؛ فناوری، امنیت، تست و کنترل تغییرات |
| [`../.claude/skills/faranou-architecture-review/SKILL.md`](../.claude/skills/faranou-architecture-review/SKILL.md), [`../.claude/skills/faranou-security-multitenancy/SKILL.md`](../.claude/skills/faranou-security-multitenancy/SKILL.md), [`../.claude/skills/faranou-testing-quality/SKILL.md`](../.claude/skills/faranou-testing-quality/SKILL.md) | Skillهای بازبینی معماری، امنیت چندشرکتی و کیفیت/تست |
| [`../.claude/skills/faranou-ui-ux-design/SKILL.md`](../.claude/skills/faranou-ui-ux-design/SKILL.md), [`../.claude/skills/faranou-accounting-domain/SKILL.md`](../.claude/skills/faranou-accounting-domain/SKILL.md) | Skillهای طراحی UI فارسی RTL و دامنه حسابداری |
| [`../.claude/skills/dotnet-patterns/SKILL.md`](../.claude/skills/dotnet-patterns/SKILL.md), [`../.claude/skills/csharp-testing/SKILL.md`](../.claude/skills/csharp-testing/SKILL.md), [`../.claude/skills/api-design/SKILL.md`](../.claude/skills/api-design/SKILL.md) | Skillهای انتخابی ECC برای .NET، تست C# و API |
| [`../.claude/skills/react-patterns/SKILL.md`](../.claude/skills/react-patterns/SKILL.md), [`../.claude/skills/react-testing/SKILL.md`](../.claude/skills/react-testing/SKILL.md), [`../.claude/skills/e2e-testing/SKILL.md`](../.claude/skills/e2e-testing/SKILL.md) | Skillهای React، تست کلاینت و Playwright |
| [`../.claude/skills/change-impact/SKILL.md`](../.claude/skills/change-impact/SKILL.md), [`../.claude/skills/faranou-efcore-migrations/SKILL.md`](../.claude/skills/faranou-efcore-migrations/SKILL.md), [`../.claude/skills/faranou-architecture-decisions/SKILL.md`](../.claude/skills/faranou-architecture-decisions/SKILL.md) | تحلیل اثر تغییرات، Migrationهای EF Core و تصمیمات معماری |
| [`THIRD_PARTY_SKILLS.md`](THIRD_PARTY_SKILLS.md), [`../THIRD_PARTY_NOTICES.md`](../THIRD_PARTY_NOTICES.md), [`../third_party/MICROSOFT-TSS-LICENSE.txt`](../third_party/MICROSOFT-TSS-LICENSE.txt), [`../third_party/ECC-LICENSE.txt`](../third_party/ECC-LICENSE.txt), [`../third_party/UI-UX-PRO-MAX-LICENSE.txt`](../third_party/UI-UX-PRO-MAX-LICENSE.txt), [`../.claude/skills/ui-ux-pro-max/SKILL.md`](../.claude/skills/ui-ux-pro-max/SKILL.md) | منشأ، مجوز و قواعد نگهداری Skillها و کاتالوگ UI |

## Handoffs اخیر (Issue #444 و مرتبط)
| handoff | توضیح |
|---|---|
| [`handoffs/ISSUE-444-admin-ui-completion.md`](handoffs/ISSUE-444-admin-ui-completion.md) | تحویل تجمیعی مدیریت کاربران و دسترسی‌ها |
| [`handoffs/ISSUE-475-roles-permission-catalog.md`](handoffs/ISSUE-475-roles-permission-catalog.md) | تحویل نقش‌ها و کاتالوگ مجوز |
| [`handoffs/ISSUE-444-membership-requests-ui.md`](handoffs/ISSUE-444-membership-requests-ui.md) | تحویل UI گردش درخواست عضویت بین‌شرکتی (درخواست‌های من و صف تأیید) و قرارداد API مورد نیاز از Backend (Issue #444) |
| [`handoffs/ISSUE-444-role-members-api.md`](handoffs/ISSUE-444-role-members-api.md) | تحویل Backend فهرست اعضای گروه دسترسی و انتقال اعضا (Issue #444، مورد ۳)؛ UI و RowVersion باقی‌مانده |
| [`handoffs/ISSUE-444-role-members-ui.md`](handoffs/ISSUE-444-role-members-ui.md) | تحویل UI فهرست اعضای گروه دسترسی و انتقال گروهی اعضا روی API ادغام‌شدهٔ #482 (Issue #444) |
| [`handoffs/ISSUE-485-security-audit-report-api.md`](handoffs/ISSUE-485-security-audit-report-api.md) | API گزارش صفحه‌بندی‌شده Audit امنیتی برای مدیر نصب؛ بدون IP و payload جزئیات |
| [`handoffs/ISSUE-444-security-audit-ui.md`](handoffs/ISSUE-444-security-audit-ui.md) | تحویل UI گزارش امنیت و سوابق مدیریتی روی API #487 (Issue #444، S7) |
| [`handoffs/ISSUE-492-customer-boundary-authz-tests.md`](handoffs/ISSUE-492-customer-boundary-authz-tests.md) | تست امنیتی endpointهای Customer Boundary operator؛ فقط Infrastructure، Anti-forgery و جلوگیری از reassignment |
| [`handoffs/ISSUE-494-company-user-directory-api.md`](handoffs/ISSUE-494-company-user-directory-api.md) | API فهرست کاربران در scope شرکت با مجوز users.list؛ بدون اطلاعات تماس/امنیتی |
| [`handoffs/ISSUE-467-login-history-logout-test.md`](handoffs/ISSUE-467-login-history-logout-test.md) | Regression test برای نمایش خروج موفق در سوابق ورود/خروج کاربر |
| [`WORK_LOG.md`](WORK_LOG.md), [`handoffs/`](handoffs/) | سوابق تحویل؛ [تحویل تجمیعی مدیریت کاربران و دسترسی‌ها](handoffs/ISSUE-444-admin-ui-completion.md)؛ [تحویل نقش‌ها و کاتالوگ مجوز](handoffs/ISSUE-475-roles-permission-catalog.md) |


- **D-061..D-063:** ماژول مستقل Trade، استفاده از FiscalPeriod مشترک و سیاست‌های اجرایی مالکیت Voyage، پرونده چنددوره‌ای، گزارش عملیاتی چندشرکتی موج اول و entitlement از S1 در `DECISIONS.md` و `TRADE_MODULE.md` ثبت شده‌اند.
- **Issue #399:** هم‌ترازی S0 و ممیزی foundation بازرگانی.


- **Issue #488 / D-066 Proposed:** ممیزی مدل سال/دوره مالی و تفکیک grant سال و دوره؛ handoff: [`handoffs/ISSUE-488-fiscal-grant-model-audit.md`](handoffs/ISSUE-488-fiscal-grant-model-audit.md). هیچ Migration تا تأیید معنای FiscalPeriod و AccountingPeriod مجاز نیست.
| [`handoffs/ISSUE-483-role-admin-positive-e2e.md`](handoffs/ISSUE-483-role-admin-positive-e2e.md) | E2E مثبت مدیر نصب برای API نقش‌ها روی سرور منتشرشده و SQL Server واقعی |
