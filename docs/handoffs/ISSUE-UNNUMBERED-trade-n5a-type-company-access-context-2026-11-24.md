# تحویل وظیفه — ISSUE-UNNUMBERED-trade-n5a-type-company-access-context

- **Issue:** UNNUMBERED
- **Agent/مسئول:** Claude (Lead) + بازبین امنیتی + بازبین کیفیت (مستقل، فقط‌خواندنی)
- **تاریخ:** 2026-11-24
- **وضعیت:** انجام‌شده (فاز ۱ از N-5a-type؛ بدون مصرف‌کننده)

## هدف و معیارهای پذیرش (تصمیم مالک ۲۰۲۶-۱۱-۲۴)
- [x] نوع جدا `TradeCompanyAccessContext(UserId, TenantId, CompanyId)` و Overload جدا در Gate
- [x] `FiscalPeriodId` در `TradeAccessContext` nullable نشد؛ مسیرهای دوره‌محور دست‌نخورده‌اند
- [x] بررسی Company و Tenant در مسیر جدید اجباری است؛ کنترل‌های D-074 تضعیف نشدند
- [x] Context فقط از `OwnershipScope` (`WorkContext.Ownership`) ساخته می‌شود؛ شناسه‌ها از هم حدس زده نمی‌شوند
- [x] تست منفی Forged/Other-Tenant/Other-Company/Other-User/Other-Request
- [x] بازبینی امنیتی انجام شد
- [x] بدون Schema، Migration یا Backfill

## خلاصه تغییرات
- نوع‌های `TradeCompanyAccessContext` و `TradeCompanyAccessDecision` افزوده شد.
- `ITradeAuthorization`: دو عضو `ResolveCurrentCompanyContextAsync` و `HasCompanyPermissionAsync`.
- `TradeAccessGate.AuthorizeCompanyScopedAsync`: ترتیب Fail-closed هویت ← Context ← Entitlement(با `CompanyId`) ← Permission؛ خطای Entitlement بدون Fallback.
- `TradeAuthorizationAdapter`: کلید خصوصی جدا در `HttpContext.Items` و اتصال با `ReferenceEquals`.
- هیچ فراخواننده‌ای در Server/Modules وجود ندارد (N-5b و مصرف‌کننده در این PR نیست).

## فایل‌های مهم تغییرکرده
- `Modules/Trade/Trade.Application/Authorization/{TradeCompanyAccessContext,TradeCompanyAccessDecision,ITradeAuthorization,TradeAccessGate}.cs`
- `Server/Infrastructure/Trade/TradeAuthorizationAdapter.cs`
- تست‌ها: `TradeCompanyAccessContextTests`، `TradeCompanyAccessGateTests`، افزوده‌های `TradeAuthorizationAdapterTests`، `TradeAccessGateTests` (عضو Fake)، `TradePartyEndpointArchitectureTests` (استخراج `AssertOnlyTheGateCalls` + دو تست اسکن IL)

## تصمیم‌های فنی
- تصمیم معماری جدیدی گرفته نشد؛ اجرای تصمیم مالک (N-5a-type فاز ۱).
- مقدمهٔ Resolve عمداً بین دو مسیر تکرار شد تا مسیر موجود دست نخورد (KISS/ریسک رگرسیون).
- سازندهٔ Context مقدار خالی را رد می‌کند (شکست زودهنگام)؛ `TradeAccessContext` چنین اعتبارسنجی ندارد.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| Build تست‌ها (Release) | موفق | ۰ خطا |
| اجرای گسترده `*Trade*`، `*Architecture*`، `*OwnershipScope*`، `*WorkContext*` | ۵۵۶ اجرا، ۰ ناموفق، ۲۷ Skip | Skipها = تست‌های SQL Server (NotRun) |
| اجرای مجدد پس از اصلاحات بازبینی (`*Architecture*`، `*TradeAuthorizationAdapter*`، `*TradeCompanyAccess*`) | ۸۳ اجرا، ۰ ناموفق، ۰ Skip | |
| `python3 scripts/check_static_assets.py` | موفق | |
| `git diff --check` | تمیز | |
| تست‌های SQL Server / E2E / CI کامل | NotRun | |

## امنیت، داده و سازگاری
- بازبینی امنیتی: Critical/High/Medium ندارد؛ چهار یافتهٔ Low (این به معنی «ایمن است» نیست؛ فقط بازبینی کد، بدون اجرا).
- Tenant تا C4 با رابطهٔ DB سنجیده نمی‌شود (`NotImplemented`)؛ `OwnershipScope.FromLegacyCompany` همچنان Tenant==Company می‌دهد، پس استقلال دو شناسه در سطح Adapter قابل اثبات نیست و فقط در سطح نوع و Gate اثبات شده است. تست «Other-Tenant» صرفاً رد Forgery را می‌سنجد، نه جداسازی Tenant در DB.
- مسیر جدید هنوز به `WorkContextService.GetCurrentAsync` (نیازمند دورهٔ انتخاب‌شده) وابسته است؛ استقلال واقعی Resolve از دوره = N-5b (NeedsApproval).
- بدون Schema/Migration/Backfill/API.

## ریسک‌ها و کارهای باقی‌مانده (پیگیری)
- L-1: مسیر Write بدون Allowlist/معادل `AdditionalReadPermissions`؛ پیش از اولین مصرف‌کننده تست اسکن Allowlist اضافه شود (امروز فراخواننده ندارد، تست مثبت‌کنترل‌دار ممکن نیست).
- L-2: پارامتر پورت Entitlement `tenantId` است ولی مقدار `CompanyId` می‌گیرد؛ پیش از Entitlement واقعی (#390) کلید درست در ADR ثبت و نام اصلاح شود. تغییر نام در این PR انجام نشد تا مسیر موجود دست نخورد.
- L-4 (بخشی): اسکن IL فراخوانی روی نوع Concrete Adapter، پارامتر Delegate و `GetRequiredService` را نمی‌گیرد (شکاف از پیش موجود).
- تفاوت معنایی آینده: مسیر قدیم Company را «TenantId» می‌نامد و مسیر جدید Tenant واقعی را؛ پس از T1 جدا می‌شوند.
- Nit از بازبین کیفیت: دو تست کم‌ارزش (`ThePeriodScopedContextsKeepTheirCurrentShape` قفل شکل بدون ارزش رفتاری که در T1 باید اصلاح شود؛ `TheCompanyPathNeverReportsAPeriodDecision`) نگه داشته شد؛ حذف در PR بعدی ممکن است.
- ادعای کامنت «no other code can read or overwrite» برای کلید جدید کمی بیش از واقع است (فقط در برابر کد/ورودی کلاینت).

## شناسه‌های Git
- Branch: `feat/trade-company-access-context`
- Commit / Pull Request: در گزارش نهایی و بدنهٔ PR

## تثبیت پس از بازبینی (ادامهٔ مأموریت، 2026-11-24)

### اعتبار OwnershipScope (بررسی از روی کد)
- `WorkContext` در Production فقط در `WorkContextService.ResolveAsync` ساخته می‌شود، پس از `CompanyAccessService.FindAccessibleCompanyAsync` (عضویت فعال از DB). `GetCurrentAsync` این اعتبارسنجی را در هر درخواست دوباره انجام می‌دهد؛ Cookie فقط شناسهٔ درخواستی را می‌آورد و به کاربر و نشست بسته است.
- **CompanyId**: از عضویت فعال DB اعتبار دارد و `HasCompanyPermissionAsync` هم دوباره عضویت و نقش را از DB می‌سنجد.
- **TenantId**: منبع مستقل ندارد. `WorkContext.Ownership` همان `OwnershipScope.FromLegacyCompany(this)` است، پس مسیر جدید **به‌طور غیرمستقیم** به معادلهٔ Legacy (Tenant=Company) وابسته است. مسیر جدید خودش `FromLegacyCompany` را صدا نمی‌زند و Tenant را از Company حدس نمی‌زند، اما تا C4 مقدار Tenant اعتبار مستقل ندارد و در بررسی مجوز هم به کار نمی‌رود (مجوز و Entitlement با `CompanyId`). هیچ مقدار ساختگی یا اعتبارسنجی ظاهری اضافه نشد.
- `ReferenceEquals`: کنترل هویت درون‌حافظه‌ای در محدودهٔ یک درخواست است (نمونه‌ای که Adapter در همین درخواست ساخته). جفت Tenant/Company را اعتبارسنجی نمی‌کند؛ فقط تضمین می‌کند Context جعلی یا متعلق به درخواست دیگر پذیرفته نشود. `WorkContext` و `CompanyAccess` رکورد عمومی‌اند، پس جلوگیری از ساخت دستی در کد درون‌پردازه با نوع‌ها ممکن نیست؛ تضمین فقط محدود بودن محل ساخت در Production است.
- قابل‌اثبات تا C4 نیست: استقلال Tenant از Company در Adapter، تست «Tenant اشتباه + Company معتبر» به‌عنوان جداسازی واقعی، و تست تفکیک Tenant و Company دو Scope متفاوت (Blocked تا C4).

### وضعیت یافته‌ها
| مورد | وضعیت | اقدام/دلیل |
|---|---|---|
| L-1 Allowlist Write | Deferred | محتوای Allowlist تصمیم دامنه‌ای است (کدام عملیات مستقل از دوره‌اند؛ D-086 بند ۷). تغییر رفتار Legacy نشد. تست دیده‌بان `NoProductionCodeCallsTheCompanyScopedGateYet` افزوده شد تا اولین مصرف‌کننده آگاهانه و با بازبینی امنیتی وارد شود. |
| L-2 نام `tenantId` پورت Entitlement | Deferred | تغییر نام پورت Entitlement (و `DenyAll…`) خارج از مجوز و وابسته به تصمیم کلید Entitlement (D-059/D-060) است. یادداشت صریح در `AuthorizeCompanyScopedAsync` افزوده شد؛ رفتار بدون تغییر. |
| L-3 ادعای «cross-tenant» تست | Fixed (کامنت) / Blocked (اثبات) | کامنت تست اصلاح شد. سنجش `TenantId` با `Ownership.Tenant` تا C4 قابل تفکیک نیست (مقادیر برابرند). |
| L-4 شکاف اسکن IL | Fixed (بخشی) / Deferred (بقیه) | اسکن `ResolveCurrentCompanyContextAsync` و فراخوانی روی نوع Concrete Adapter افزوده شد. فراخوانی از طریق Reflection، DI Factory، پارامتر Delegate و `GetRequiredService` همچنان دیده نمی‌شود (شکاف از پیش موجود، Deferred؛ اسکن جایگزین بازبینی نیست). |
| تست‌های کم‌ارزش | Fixed | حذف `TheCompanyPathNeverReportsAPeriodDecision` (Assertion تهی)، `AReadPermissionCanNotAuthorizeAWriteIntent` (وابسته به منطق Fake؛ تست واقعی در Adapter است) و `Assert.NotEqual` تصادفی. تست قفل شکل `TradeAccessContext/TradePeriodScope` عمداً ماند: دیده‌بان محدودیت مالک (O-3 فقط در T1) است و در T1 باید آگاهانه به‌روز شود. |
| تکرار مقدمهٔ Resolve | Not an issue فعلی | دو بدنهٔ کوتاه؛ استخراج Helper ریسک رگرسیون مسیر موجود دارد (YAGNI تا مسیر سوم). |
