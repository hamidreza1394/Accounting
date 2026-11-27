# تحویل وظیفه — ISSUE-802-vendor-setup-endpoint-security

- **Issue:** [#802](https://github.com/hamidreza1394/Accounting/issues/802) — بخش PR-4 از مجموعهٔ ۵ PR
- **Agent/مسئول:** GLM (Autonomous Engineering)
- **تاریخ:** 2026-11-27
- **وضعیت:** انجام‌شده (Endpointها + Bootstrap اپراتور)؛ وضعیت کارکرد واقعی مرورگر روی Windows: Not yet Verified

## هدف و معیارهای پذیرش (این PR)
- [x] `POST /inspect` و `POST /provision` + `GET /provision-token` روی همان صفحهٔ setup
- [x] Bootstrap یک‌بارمصرف: جفت token+Cookie سخت‌گیرانه، مصرف یگانه، انقضا ۱۵ دقیقه، TimeProvider تزریقی، hash-only در حافظه، سقف ۱۶ جفت بدون بی‌اعتبار کردن فعال دیگر (بازنویسی قدیمی‌ترین)
- [x] **اصلاح ۳.۲:** صدور Bootstrap به‌تنهایی مجوز نیست؛ همهٔ عملیات تغییردهنده به Challenge کنسولی صادره از خود فرآیند نیاز دارند (فقط loopback، فقط hash در حافظه، مقایسهٔ ثابت-زمان، انقضا ۱۲ ساعت، تغییر با restart، عدم حضور در HTTP)
- [x] Allow-list مقصد روی test/save هم اعمال شد
- [x] Rate limit provision = ۳/دقیقه؛ setup = ۱۰/دقیقه (هر دو میزبان)
- [x] تست واحد Bootstrap (مصرف یگانه/انقضا/سقف) و Challenge (اعتبار/انقضا/restart)
- [ ] مرور بصری مرورگر روی Windows (PR-5)

## امنیت
- Challenge فقط در کنسول فرآیند؛ هیچ Endpoint آن را نمی‌دهد؛ در Log/URL/Storage نمی‌آید.
- Bootstrap token + Cookie سخت‌گیرانه فقط برای provision؛ replay/reuse fail-closed؛ توکن در URL/Log/Storage دائمی نمی‌آید.
- تست/save/inspect/provision همگی پشت loopback+Origin هم‌میزبان+antiforgery+challenge؛ در حالت برنامهٔ کامل پشت سیاست پیش‌فرض Vendor.
- هیچ SQL execution عمومی وجود ندارد؛ Endpointهای جدید فقط orchestration صحنه‌های موجود هستند.

## بررسی و آزمون
| آزمون | نتیجه واقعی |
|---|---|
| `dotnet build Vendor.sln -c Release` | موفق |
| `dotnet test tests/Vendor.Host.Tests -c Release` | همهٔ تست‌های Bootstrap/Challenge سبز؛ ۱۲ failed baseline محیطی بدون تغییر |
| مرورگر واقعی | PR-5 |

## شناسه‌های Git
- Branch: `fix/vendor-provisioning-endpoint-security`
- Pull Request: پس از push ثبت می‌شود
