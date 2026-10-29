# دستورالعمل GitHub Copilot و Agentهای رایگان برای فرانو

این فایل برای **GitHub Copilot (حتی نسخه Free)**، Cursor، Claude و هر Agent دیگری که در این مخزن کار می‌کند نوشته شده است.

## قبل از هر تغییر کد (اجباری)

1. `AGENTS.md` را کامل بخوان.
2. `docs/INDEX.md` → سپس `docs/PROJECT_STATUS.md`، `docs/ARCHITECTURE.md`، `docs/DECISIONS.md` و `docs/SECURITY.md` را مرور کن.
3. Issue مرتبط را بخوان و محدوده کار را خلاصه کن.
4. هیچ Migration، Schema یا کلید خصوصی بدون تأیید صریح اضافه نکن.

## قواعد کیفیت و سرعت توسعه

- **PR کوچک و تک‌موضوعی** بنویس. یک قابلیت = یک PR.
- همیشه تست واحد یا Integration مربوط به تغییر را اضافه یا به‌روز کن.
- برای UI جدید در `ClientApp` حتماً `docs/UI_COMPONENTS.md` و استاندارد RTL فارسی را رعایت کن.
- دارایی‌ها (CSS/JS/فونت/آیکون) باید محلی باشند؛ هیچ CDN در runtime مجاز نیست.
- Multi-Tenancy: فقط از `CompanyAccessService` استفاده کن؛ به Claim یا TenantId ارسالی کلاینت اعتماد نکن.
- Anti-forgery + مجوز سمت سرور برای هر mutation اجباری است.
- Exception را بی‌صدا نبلع؛ با `ILogger<T>` لاگ کن ولی secret ننویس.

## کمک به هوش مصنوعی (Free Copilot + CodeRabbit + Semgrep)

- وقتی کد می‌نویسی، نام‌گذاری واضح، کامنت کوتاه روی منطق دامنه و تست‌های منفی (fail-closed) بنویس تا Agentهای بعدی و CodeRabbit راحت‌تر بازبینی کنند.
- در PR description بنویس: Issue شماره، خلاصه تغییر، تست‌های اجرا شده، ریسک‌ها.
- اگر CodeRabbit نصب شده، نظرات آن را جدی بگیر و قبل از Merge اصلاح کن.
- Semgrep و CodeQL (وقتی فعال شد) هشدارهای امنیتی می‌دهند؛ آن‌ها را نادیده نگیر.

## محدوده Vendor vs حسابداری مشتری

- تغییرات Vendor نباید Migration یا Schema دیتابیس حسابداری مشتری را دست بزند.
- کلید خصوصی لایسنس/Provisioning هرگز در Git، config یا لاگ نیاید (fail-closed).

## تحویل کار

- گزارش handoff را طبق `docs/handoffs/TEMPLATE.md` بنویس.
- خلاصه را به `docs/WORK_LOG.md` اضافه کن.
- `git diff --check` و بازبینی نهایی diff را قبل از درخواست Merge انجام بده.

این دستورالعمل با نسخه رایگان Copilot و ابزارهای رایگان (CodeRabbit روی Public، Semgrep، CodeQL) سازگار است و هدفش افزایش سرعت و کیفیت بدون نیاز به پلن پولی است.
