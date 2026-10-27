# تحویل وظیفه — ISSUE-UNNUMBERED-ui-ux-pro-max-skill

- **Issue:** `UNNUMBERED`
- **Agent/مسئول:** Claude (جلسهٔ Claude Code)
- **تاریخ:** 2026-10-27
- **وضعیت:** انجام‌شده (فقط مهارت/مستندات؛ هیچ کد برنامه، وابستگی runtime یا Migration تغییر نکرد)

## هدف و معیارهای پذیرش
- [x] بخش‌های مفید و سازگار مهارت `ui-ux-pro-max` برای زیباسازی و کیفیت فرم‌ها در مخزن باشد.
- [x] با قواعد پروژه (بدون CDN/فونت/سرویس بیرونی، توکن‌ها، shadcn، RTL) تعارض نداشته باشد.
- [x] Claude و Agentهای دیگر بتوانند از آن استفاده کنند و راه استفاده در مستندات باشد.
- [x] منشأ، مجوز و بررسی انجام‌شده ثبت شود.

## خلاصه تغییرات
- کپی خام پوشهٔ `.claude/skills/ui-ux-pro-max` از `nextlevelbuilder/ui-ux-pro-max-skill` کامیت `28b3b8dee8c976df4fa3e94aed4e092ad43fcb03` (MIT)، بدون `scripts/tests/`.
- بند «Faranou project overrides» در ابتدای `SKILL.md` (تنها تغییر فایل‌های upstream): فقط ux/chart/react و stack های shadcn/react؛ ممنوعیت پالت/فونت/آیکون عمومی، `--persist`، GSAP، CDN و وابستگی جدید.
- `.gitattributes`: معافیت فاصلهٔ انتهای خط فقط برای این مسیر، تا کپی بایت‌به‌بایت بماند.
- `third_party/UI-UX-PRO-MAX-LICENSE.txt` و بخش جدید `docs/THIRD_PARTY_SKILLS.md`.
- بخش راهنمای استفاده در `docs/UI_COMPONENTS.md`؛ ارجاع از `AGENTS.md`، `CLAUDE.md` و `docs/INDEX.md`.
- عمداً کپی نشد: CLI `uipro`، افزونهٔ Claude و مهارت‌های `design`، `brand`، `slides`، `banner-design`، `ui-styling`، `design-system` (کلید API سرویس‌های هوش مصنوعی بیرونی و CDN).

## فایل‌های مهم تغییرکرده
- `.claude/skills/ui-ux-pro-max/**` — مهارت vendored (۴۷ فایل)
- `.gitattributes`، `third_party/UI-UX-PRO-MAX-LICENSE.txt`
- `docs/THIRD_PARTY_SKILLS.md`، `docs/UI_COMPONENTS.md`، `docs/INDEX.md`، `AGENTS.md`، `CLAUDE.md`

## تصمیم‌های فنی
- تصمیم معماری جدیدی ثبت نشد؛ مهارت ابزار کمکی Agent است و مثل مهارت‌های ECC در `docs/THIRD_PARTY_SKILLS.md` ثبت شد (نه وابستگی runtime).
- فقط یک نسخه در `.claude/skills/` (۳٫۳ مگابایت)؛ ابزارهای دیگر با دستور `python3 .claude/skills/ui-ux-pro-max/scripts/search.py` از ریشهٔ مخزن آن را اجرا می‌کنند، نه چهار نسخهٔ جدا.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| مقایسهٔ بایتی با upstream (`diff -rq`، بدون tests) | موفق | فقط `SKILL.md` فرق دارد (بند فرانو) |
| بررسی ایستای کد (grep شبکه/exec/نوشتن) | موفق | مسیر جست‌وجو بدون شبکه؛ نوشتن فقط با `--persist` |
| اجرای `search.py` (ux، `--stack shadcn`، chart) | موفق | هر سه نتیجه دادند؛ پرس‌وجوی chart با «financial report comparison» نمودار شمعی داد (نامناسب) |
| `python3 scripts/check_static_assets.py` | موفق | پیام PASS |
| `git diff --check` (هر کامیت و diff نهایی) | موفق | با معافیت `.gitattributes` برای مسیر مهارت |
| `dotnet build/test`، `npm ci/typecheck/test/build/check:offline` | اجرا نشد | تغییر کد برنامه نیست |
| CI | ندیده شده | منتظر اجرای واقعی |

## امنیت، داده و سازگاری
- کد Python شخص ثالث وارد مخزن شد؛ فقط برای Agent/توسعه‌دهنده است و در build یا runtime برنامه نمی‌آید. بررسی ایستای بالا مربوط به کامیت پین‌شده است و ضمانتی برای نسخه‌های بعدی نیست.
- هیچ کلید، رمز یا Migration اضافه نشد.

## ریسک‌ها و کارهای باقی‌مانده
- حدود ۶۳ هزار خط داده (CSV/JSON) به مخزن اضافه شد؛ اگر حجم مشکل شد می‌توان فایل‌های بی‌استفاده (مثل پشته‌های غیر React، `phosphor-icons-upstream.json`) را حذف کرد، به شرط بررسی اینکه اسکریپت‌ها به آن‌ها نیاز ندارند.
- دقت نتایج جست‌وجو متغیر است (مثال بالا)؛ مهارت پیشنهاد می‌دهد، تصمیم با توکن‌ها و قواعد پروژه است.
- شاخهٔ این PR روی شاخهٔ PR #199 بنا شده؛ تا ادغام #199 پایهٔ این PR همان شاخه است.
- به‌روزرسانی upstream نیازمند بازبینی دوبارهٔ اسکریپت‌ها و داده است (روش در `docs/THIRD_PARTY_SKILLS.md`).
- فایده‌ی واقعی مهارت برای فرانو هنوز اندازه‌گیری نشده؛ پیشنهاد: یک فرم واقعی را با و بدون آن طراحی و مقایسه کنید.

## شناسه‌های Git
- Branch: `docs/ui-ux-pro-max-skill` (روی `docs/ui-component-standards`)
- Commit: `a189c93`، `9aedd11`، `6d23676`، `8acb5d1`، `1c78c86` (کامیت تحویل بعد از همین فایل)
- Pull Request: در گزارش پایانی ثبت می‌شود
