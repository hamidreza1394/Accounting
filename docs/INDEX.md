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
| [USER_ACCESS_MANAGEMENT.md](USER_ACCESS_MANAGEMENT.md) | طراحی و نقشه اجرایی مدیریت کاربران، سمت‌ها، گروه‌های دسترسی، مجوزهای جزئی و سوابق امنیتی (Issue #444) |
| [SUBSYSTEM_CATALOG.md](SUBSYSTEM_CATALOG.md) | کاتالوگ رسمی زیرسیستم‌های واقعی مخزن با شواهد کد، وضعیت، منو، کدهای مجوز و پیوند entitlement؛ مدل زیرسیستم/منو/فرم/عملیات در D-075 (Stage 2-A، Issue #444) |
| [MENU_FORM_CATALOG.md](MENU_FORM_CATALOG.md) | کاتالوگ درخت منو و همهٔ Routeهای واقعی با Parent/گیت/ترتیب؛ گارد ماشینی سازگاری منو↔روتر (Stage 2-B، Issue #444) |
| [PERMISSION_CATALOG.md](PERMISSION_CATALOG.md) | نگاشت کاربرد هر ۷۳ کد `PermissionDefinitions` به endpoint/فرم، تاکسونومی افعال عملیات، سطح hint کلاینت و قواعد نگهداری کاتالوگ (Stage 2-C، Issue #444) |
| [api/USER_ACCESS_MANAGEMENT_API.md](api/USER_ACCESS_MANAGEMENT_API.md) | قرارداد واقعی API مدیریت کاربران/دسترسی/نشست‌ها برای مصرف Frontend — استخراج‌شده از کد (Issue #444؛ به‌روزرسانی 2026-11-05) |
| [handoffs/USER-ACCESS-FRONTEND-HANDOFF.md](handoffs/USER-ACCESS-FRONTEND-HANDOFF.md) | نقشهٔ مصرف Frontend برای کاربران/دسترسی/نشست‌ها: نگاشت صفحه‌ها به API، خطاها، قابلیت‌های ناموجود و وضعیت PRهای Wave-1 (2026-11-05) |
| [`FRONTEND_API_CONTRACT_NEEDS.md`](FRONTEND_API_CONTRACT_NEEDS.md) | قراردادهای API پیشنهادی مورد نیاز UI مدیریت دسترسی (عضویت، `/me/permissions`، گزارش امنیتی، نشست‌ها، S2) برای Backend (Issue #444) |
| [ORGANIZATIONAL_STRUCTURE_DATA_MODEL.md](ORGANIZATIONAL_STRUCTURE_DATA_MODEL.md) | مرجع S2: طراحی پیاده‌سازی‌شده گزینه B (HR-ready) با D-072 (Accepted) — Entityها، Migration `20261104200301`، کاتالوگ `hr.*` و قرارداد API `/org/*`؛ Backend پیاده و UI در ادامه فاز ۴ |
| [`PROJECT_STATUS.md`](PROJECT_STATUS.md) | وضعیت تأییدشده فعلی |
| [`ROADMAP.md`](ROADMAP.md) | برنامه راه |
| [`ARCHITECTURE.md`](ARCHITECTURE.md) | معماری و جهت وابستگی |
| [`WORK_CONTEXT_SELECTOR_UI.md`](WORK_CONTEXT_SELECTOR_UI.md) | طراحی بصری و رفتار صفحه انتخاب شرکت و دوره مالی (لوگو، فونت، مرور محصولات، اندازه‌گیری)؛ قرارداد تغییر مرکزی شرکت/دوره حین نشست و ۱۲ سناریوی پذیرش (D-076، Proposed) |
| [`DECISIONS.md`](DECISIONS.md) | ADRها D-001..D-077 در DECISIONS.md؛ D-076: قرارداد رسمی UI، چرخهٔ عمر تب‌های Workspace (Clean/Dirty/Saving/Error) و تغییر مرکزی شرکت/دورهٔ مالی (Proposed؛ NeedsApproval)؛ D-075: کاتالوگ زیرسیستم و مدل زیرسیستم/منو/فرم/عملیات (Proposed؛ Stage 2-A)؛ D-064: تفکیک مدیر سراسری نصب از مجوزهای عملیاتی شرکتی (Accepted؛ Issue #444)؛ D-065: معماری مصوب دسترسی چندشرکتی، دامنه و grant سال/دوره (Accepted؛ Issue #444)؛ D-059: entitlement مستقل Company/Module/Feature/Seat، add-on با amendment، bootstrap مدیر Offline و sync دوحالته؛ D-055 و D-056 جداگانه در اسناد اختصاصی Issue #236 (D-055: معماری PKCS#11؛ D-056: SoftHSM برای Development/Staging و Software assurance با پذیرش ریسک صریح در Production؛ مرحلهٔ ۲ با HSM واقعی؛ D-057: TPM 2.0 NV Extend hash-chain برای anti-rollback وضعیت ابطال آفلاین مشتری (Issue #364)؛ D-058: فعال‌سازی پایدار، هویت نصب ویندوز و کنترل server-side ابری (Issue #384)؛ مرز مشتری و برچسب‌های رابطه: D-037..D-040؛ مرز سامانه مرکزی، فعال‌سازی، هویت فروشنده و گذار: D-041..D-044؛ قرارداد لایسنس: D-045؛ امنیت و عملیات فروشنده: D-046؛ معماری اجرایی Vendor: D-047 (Accepted; implementation staged and not Production-ready)؛ shadcn/ui: D-048؛ کتابخانه‌های جدول/نمودار/تاریخ، آفلاین‌بودن دارایی‌ها و قواعد طراحی/سرعت: D-049؛ استفادهٔ مشروط از بخش‌های رایگان KendoReact: D-050 |
| [`CORPORATE_GROUPS.md`](CORPORATE_GROUPS.md) | گروه شرکتی، گزارش تجمیعی/تلفیقی، مجوزها و نقشه راه (D-033..D-035) |
| [`CORPORATE_GROUPS_DATA_MODEL.md`](CORPORATE_GROUPS_DATA_MODEL.md) | جداول، کلیدها و راهبرد Migration گروه شرکتی |
| [`PRODUCT_REQUIREMENTS.md`](PRODUCT_REQUIREMENTS.md) | نیازمندی‌های محصول |
| [`REQUIREMENTS_TRACEABILITY.md`](REQUIREMENTS_TRACEABILITY.md) | ردیابی نیازمندی ← کد/تست |
| [`LICENSING.md`](LICENSING.md) | معماری و وضعیت لایسنس و فعال‌سازی |
| [`LICENSING_IMPLEMENTATION_PLAN.md`](LICENSING_IMPLEMENTATION_PLAN.md) | مرجع اجرایی D-059/D-060 و ترتیب Issues #389–#396 |
| [`handoffs/ISSUE-384-customer-license-import.md`](handoffs/ISSUE-384-customer-license-import.md) | وضعیت واقعی نصب هویت، persistence و import لایسنس مشتری پس از ادغام PRهای #386–#388 و کارهای runtime باقیمانده |
| [`handoffs/ISSUE-228-vendor-provisioning-state.md`](handoffs/ISSUE-228-vendor-provisioning-state.md) | وضعیت Vendor provisioning، binding لایسنس و شکاف CompanyReference در payload v2 |
| [`handoffs/ISSUE-393-vendor-addon-payload-builder.md`](handoffs/ISSUE-393-vendor-addon-payload-builder.md) | تحویل Vendor-only برای candidate افزایش ظرفیت شرکت/ماژول/Feature و تمدید مستقل support/update؛ بدون persistence، approval یا Customer-side changes (Issue #393) |
| [`handoffs/ISSUE-391-runtime-entitlement-enforcement.md`](handoffs/ISSUE-391-runtime-entitlement-enforcement.md) | وضعیت لایه تصمیم Entitlement، پل payload معتبر و blocker نگاشت CompanyReference در Issue #391 |
| [`LICENSING_DATA_MODEL.md`](LICENSING_DATA_MODEL.md) | ERD واقعی و پیشنهادی |
| [`LICENSING_WORKFLOWS.md`](LICENSING_WORKFLOWS.md) | گردش‌کارها |
| [`LICENSING_THREAT_MODEL.md`](LICENSING_THREAT_MODEL.md) | تهدیدها |
| [`LICENSING_OPEN_DECISIONS.md`](LICENSING_OPEN_DECISIONS.md) | تصمیم‌های باز لایسنس و سامانه مرکزی با گزینه‌ها، ریسک و پیشنهاد؛ پرسش‌های اولویت‌بندی‌شده برای مالک محصول (فاز صفر 2026-10-24) |
| [`DATABASE.md`](DATABASE.md), [`database-migrations.md`](database-migrations.md) | دیتابیس و Migration |
| [`DATABASE_SETUP.md`](DATABASE_SETUP.md) | راه‌اندازی اولیه دیتابیس، محل و رمزنگاری تنظیمات اتصال، مهاجرت TarazAva ← Farano، فرمان `--settings` (D-036) |
| [`SECURITY.md`](SECURITY.md) | امنیت؛ شامل الزامات تغییر زمینه و جداسازی چندشرکتی (D-076، Proposed) |
| [`VENDOR_BOOTSTRAP.md`](VENDOR_BOOTSTRAP.md) | راهنمای عملیاتی command-mode برای راه‌اندازی اولین مدیر Vendor، TOTP و بازیابی pending |
| [`runbooks/vendor-audit-checkpoint-recovery.md`](runbooks/vendor-audit-checkpoint-recovery.md) | رویهٔ provider-neutral برای checkpoint مستقل Audit، backup/restore و recovery؛ تا انتخاب مقصد واقعی، Production-ready نیست |
| [`runbooks/ISSUE-236-HSM-TPM-KEY-OPERATIONS.md`](runbooks/ISSUE-236-HSM-TPM-KEY-OPERATIONS.md) | Key Ceremony، کنترل دو نفره، rotation/revocation، Backup/Recovery، TPM مشتری و alerting؛ اجرای Production تا تأمین HSM واقعی Blocked است |
| [`TESTING.md`](TESTING.md) | تست |
| [`DEPLOYMENT.md`](DEPLOYMENT.md), [`DEVELOPMENT.md`](DEVELOPMENT.md) | استقرار و توسعه |
| [`CODING_STANDARDS.md`](CODING_STANDARDS.md) | استانداردها |
| [`UI_COMPONENTS.md`](UI_COMPONENTS.md) | هستهٔ قواعد UI: shadcn/ui، RTL فارسی، دسترس‌پذیری، runtime آفلاین، قانون محلی‌سازی دارایی‌ها و قواعد طراحی/سرعت/ویژوال (D-048، D-049)؛ الزامات رسمی UI-01..UI-15 و قرارداد چرخهٔ عمر تب‌ها (D-076، Proposed) |
| [`ADMIN_UI.md`](ADMIN_UI.md) | وضعیت کد ادغام‌شدهٔ رابط مدیریت سیستم در ClientApp، مسیرها، API client، تست‌ها و شکاف‌های واقعی Backend (Issues #461–#475) |
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
| [`handoffs/ISSUE-444-membership-requests-ui.md`](handoffs/ISSUE-444-membership-requests-ui.md) | تحویل UI گردش درخواست عضویت بین‌شرکتی (درخواست‌های من و صف تأیید) و قرارداد API مورد نیاز از Backend (Issue #444) |
| [`handoffs/ISSUE-444-role-members-api.md`](handoffs/ISSUE-444-role-members-api.md) | تحویل Backend فهرست اعضای گروه دسترسی و انتقال اعضا (Issue #444، مورد ۳)؛ UI و RowVersion باقی‌مانده |
| [`handoffs/ISSUE-444-role-members-ui.md`](handoffs/ISSUE-444-role-members-ui.md) | تحویل UI فهرست اعضای گروه دسترسی و انتقال گروهی اعضا روی API ادغام‌شدهٔ #482 (Issue #444) |
| [`handoffs/ISSUE-485-security-audit-report-api.md`](handoffs/ISSUE-485-security-audit-report-api.md) | API گزارش صفحه‌بندی‌شده Audit امنیتی برای مدیر نصب؛ بدون IP و payload جزئیات |
| [`handoffs/ISSUE-507-security-audit-csv-export.md`](handoffs/ISSUE-507-security-audit-csv-export.md) | خروجی CSV محدود گزارش امنیتی با ثبت Audit، محدودیت حجم و جلوگیری از Formula Injection (Issue #507) |
| [`handoffs/ISSUE-515-csv-leading-control-characters.md`](handoffs/ISSUE-515-csv-leading-control-characters.md) | تکمیل خنثی‌سازی Tab/CR/LF در سلول‌های خروجی CSV (Issue #515) |
| [`handoffs/ISSUE-522-sensitive-identity-read-rate-limit.md`](handoffs/ISSUE-522-sensitive-identity-read-rate-limit.md) | اعمال سیاست نرخ حساس روی چهار endpoint خواندن اطلاعات هویتی (Issue #522) |
| [`handoffs/ISSUE-444-company-manager-delegation-ceiling.md`](handoffs/ISSUE-444-company-manager-delegation-ceiling.md) | مدیر شرکت، سقف تفویض مبتنی بر تصاحب، bootstrap امن تأیید عضویت و دفترچه فقط-خواندنی هم‌گروه (D-071، Issue #444 مرحله ۲) |
| [`handoffs/ISSUE-444-access-hardening-mission2.md`](handoffs/ISSUE-444-access-hardening-mission2.md) | راستی‌آزمایی مرحله اول مدیریت دسترسی‌ها (#538/#536/#481، قرارداد #510/#511، ممیزی #537) و تکمیل پوشش تست رفتاری 429 و مجوزدهی HTTP (Issue #444) |
| [`handoffs/ISSUE-511-me-permissions.md`](handoffs/ISSUE-511-me-permissions.md) | API مجوزهای کاربر جاری بر اساس شرکت فعال زمینه کاری همان نشست (Issue #511) |
| [`handoffs/ISSUE-512-security-audit-report-api.md`](handoffs/ISSUE-512-security-audit-report-api.md) | مرتب‌سازی امن، فهرست نوع رویداد، فهرست شرکت‌ها و قرارداد UTC؛ D-070 نگهداری Proposed (Issue #512) |
| [`handoffs/ISSUE-530-membership-search-loading-reset.md`](handoffs/ISSUE-530-membership-search-loading-reset.md) | رفع گیر کردن وضعیت بارگذاری جستجوی مقصد عضویت پس از پاک کردن query (Issue #530) |
| [`handoffs/ISSUE-510-membership-workflow-api.md`](handoffs/ISSUE-510-membership-workflow-api.md) | کشف مقصد محدود به CustomerBoundary، نقش‌های قابل واگذاری، فهرست تأییدکنندگان و تاریخچه درخواست (Issue #510) |
| [`handoffs/ISSUE-535-membership-workflow-positive-e2e.md`](handoffs/ISSUE-535-membership-workflow-positive-e2e.md) | تست E2E مسیر موفق گردش درخواست عضویت (تأیید، رد، حالت‌های امنیتی) روی مرورگر و SQL Server واقعی؛ فقط تست، بدون تغییر محصول (Issue #535) |
| [`handoffs/ISSUE-498-membership-permission-delegation-ceiling.md`](handoffs/ISSUE-498-membership-permission-delegation-ceiling.md) | enforce سقف واگذاری مجوز هنگام تأیید عضویت بین‌شرکتی؛ بدون Schema/Migration |
| [`handoffs/ISSUE-500-utc-kind-value-converter.md`](handoffs/ISSUE-500-utc-kind-value-converter.md) | EF ValueConverter برای فیلدهای DateTime دارای پسوند Utc؛ حفظ مقدار و عدم تغییر فیلدهای ساعت محلی قدیمی (Issue #500) |
| [`handoffs/ISSUE-496-role-membership-rowversion.md`](handoffs/ISSUE-496-role-membership-rowversion.md) | RowVersion SQL Server برای CompanyRole و UserCompanyMembership؛ مدیریت 409 و تست رقابت نویسنده‌ها (Issue #496) |
| [`handoffs/ISSUE-504-session-lifecycle-design.md`](handoffs/ISSUE-504-session-lifecycle-design.md) | ممیزی نشست فعلی، پیشنهاد UserSession/SessionId، ابطال مستقل و تصمیم‌های retention (D-069, Issue #504) |
| [`handoffs/ISSUE-492-customer-boundary-authz-tests.md`](handoffs/ISSUE-492-customer-boundary-authz-tests.md) | تست امنیتی endpointهای Customer Boundary operator؛ فقط Infrastructure، Anti-forgery و جلوگیری از reassignment |
| [`handoffs/ISSUE-494-company-user-directory-api.md`](handoffs/ISSUE-494-company-user-directory-api.md) | API فهرست کاربران در scope شرکت با مجوز users.list؛ بدون اطلاعات تماس/امنیتی |
| [`TIME_CONTRACT.md`](TIME_CONTRACT.md) | ممیزی و قرارداد زمان: `*Utc` در برابر ساعت محلی قدیمی سرور، قرارداد Frontend و پیشنهاد Backend (D-067) |
| [`handoffs/ISSUE-444-utc-time-contract.md`](handoffs/ISSUE-444-utc-time-contract.md) | تحویل یکپارچه‌سازی قرارداد زمان در ClientApp (Issue #444) |
| [`handoffs/ISSUE-444-jalali-date-picker.md`](handoffs/ISSUE-444-jalali-date-picker.md) | تحویل انتخابگر تاریخ شمسی (`JalaliDateField`) و ADR وابستگی D-068 (Issue #444) |
| [`handoffs/UNNUMBERED-ui-workspace-context-contracts.md`](handoffs/UNNUMBERED-ui-workspace-context-contracts.md) | تحویل مستندسازی قراردادهای UI، چرخهٔ عمر تب و تغییر زمینه (D-076، Proposed؛ بدون تغییر کد) |
| [`handoffs/ISSUE-444-security-audit-ui.md`](handoffs/ISSUE-444-security-audit-ui.md) | تحویل UI گزارش امنیت و سوابق مدیریتی روی API #487 (Issue #444، S7) |
| [`FRONTEND_API_CONTRACT_NEEDS.md`](FRONTEND_API_CONTRACT_NEEDS.md) | قراردادهای API پیشنهادی مورد نیاز UI مدیریت دسترسی (عضویت، `/me/permissions`، گزارش امنیتی، نشست‌ها، S2) برای Backend (Issue #444) |
| [`handoffs/ISSUE-444-ui-e2e-and-a11y.md`](handoffs/ISSUE-444-ui-e2e-and-a11y.md) | تحویل E2E و axe رابط مدیریت دسترسی (Issue #444، مرحلهٔ ۱۱؛ اجرا فقط در CI) |
| [`handoffs/ISSUE-444-company-user-directory-ui.md`](handoffs/ISSUE-444-company-user-directory-ui.md) | تحویل UI دفترچهٔ کاربران شرکت روی API #497 (Issue #444) |
| [`TIME_CONTRACT.md`](TIME_CONTRACT.md) | ممیزی و قرارداد زمان: `*Utc` در برابر ساعت محلی قدیمی سرور، قرارداد Frontend و پیشنهاد Backend (D-067) |
| [`handoffs/ISSUE-444-utc-time-contract.md`](handoffs/ISSUE-444-utc-time-contract.md) | تحویل یکپارچه‌سازی قرارداد زمان در ClientApp (Issue #444) |
| [`handoffs/ISSUE-444-security-audit-complete.md`](handoffs/ISSUE-444-security-audit-complete.md) | تکمیل UI گزارش امنیتی: فیلتر تاریخ شمسی، انتخاب کاربر/شرکت و قراردادهای API باقی‌مانده (Issue #444) |
| [`handoffs/ISSUE-467-login-history-logout-test.md`](handoffs/ISSUE-467-login-history-logout-test.md) | Regression test برای نمایش خروج موفق در سوابق ورود/خروج کاربر |
| [`WORK_LOG.md`](WORK_LOG.md), [`handoffs/`](handoffs/) | سوابق تحویل؛ [تحویل تجمیعی مدیریت کاربران و دسترسی‌ها](handoffs/ISSUE-444-admin-ui-completion.md)؛ [تحویل نقش‌ها و کاتالوگ مجوز](handoffs/ISSUE-475-roles-permission-catalog.md) |


- **D-061..D-063:** ماژول مستقل Trade، استفاده از FiscalPeriod مشترک و سیاست‌های اجرایی مالکیت Voyage، پرونده چنددوره‌ای، گزارش عملیاتی چندشرکتی موج اول و entitlement از S1 در `DECISIONS.md` و `TRADE_MODULE.md` ثبت شده‌اند.
- **Issue #399:** هم‌ترازی S0 و ممیزی foundation بازرگانی.


- **Issue #488 / D-066 Accepted (2026-11-08):** FiscalPeriod موجود = سال مالی؛ AccountingPeriod فرزند = دوره عملیاتی؛ تخصیص نقش Company/year و policy دوره‌ای طبق D-065/D-077. طراحی داده: [`FISCAL_ACCESS_DATA_MODEL.md`](FISCAL_ACCESS_DATA_MODEL.md)؛ handoff: [`handoffs/ISSUE-488-fiscal-grant-model-audit.md`](handoffs/ISSUE-488-fiscal-grant-model-audit.md). Runtime/Migration هنوز NotImplemented.
| [`handoffs/ISSUE-483-role-admin-positive-e2e.md`](handoffs/ISSUE-483-role-admin-positive-e2e.md) | E2E مثبت مدیر نصب برای API نقش‌ها روی سرور منتشرشده و SQL Server واقعی |


- **D-070 Accepted (2026-11-06):** نگهداری Audit با طبقه‌بندی ۱۲/۲۴/۳۶ ماه، Legal Hold و Archive چندلایه Cloud؛ اجرای Purge همچنان NotImplemented تا طراحی اجرایی و بررسی حقوقی.
- **D-073 Accepted (2026-11-06):** MFA، بازیابی/Reset رمز و ابطال مدیریتی نشست؛ پیاده‌سازی Customer هنوز ناقص است.
- **D-074 Accepted (2026-11-06):** ترکیب fail-closed مجوز عملیاتی و Entitlement در همه ماژول‌های مجوزدار؛ اتصال سراسری runtime هنوز کامل نیست.
- **Issue #556:** ثبت و هم‌ترازی ADRهای امنیت حساب، Audit retention و Entitlement؛ handoff: [ISSUE-556-security-decisions.md](handoffs/ISSUE-556-security-decisions.md).
- **Stage 2-A مأموریت تکمیلی #444 (2026-11-08):** کاتالوگ رسمی زیرسیستم‌ها: [SUBSYSTEM_CATALOG.md](SUBSYSTEM_CATALOG.md)؛ ADR پیشنهادی D-075؛ handoff مرحله دوم: [handoffs/ISSUE-444-stage-2.md](handoffs/ISSUE-444-stage-2.md).

- **D-077 Accepted (2026-11-08):** هویت User یک‌باره و پایدار است؛ تخصیص CompanyRole برای هر سال/دوره مستقل و فراخوانی دسترسی از سال دیگر به‌صورت کپی قابل بازبینی است. پیاده‌سازی NotImplemented و DDL تا رفع ابهام D-066 / Issue #488 مسدود است؛ handoff: [`handoffs/ISSUE-444-fiscal-year-user-access.md`](handoffs/ISSUE-444-fiscal-year-user-access.md).
