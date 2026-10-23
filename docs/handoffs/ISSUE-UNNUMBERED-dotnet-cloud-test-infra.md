# تحویل وظیفه — ISSUE-UNNUMBERED-dotnet-cloud-test-infra

- **Issue:** UNNUMBERED (درخواست مستقیم کارفرما: رفع نبودن `dotnet` در Claude Code Cloud و زیرساخت تست واقعی)
- **Agent/مسئول:** Lead (Claude Code)؛ بازبین‌های مستقل: `code-reviewer`، `security-reviewer`
- **تاریخ:** 2026-10-23
- **وضعیت:** انجام‌شده روی شاخهٔ `chore/dotnet-sdk-cloud-test-infra` (از `main` در `19d608e`)؛ PR باز می‌شود، Merge با مسئول مخزن

## هدف و معیارهای پذیرش
- [x] `dotnet` در محیط Cloud واقعاً در دسترس شود (`dotnet --info`، `--list-sdks` ثبت شد)
- [x] Restore و Build Release اجرا شود
- [x] تست‌های xUnit روی SQL Server واقعی اجرا شود (نه SQLite)
- [x] E2E با برنامهٔ واقعی اجرا شود
- [x] CI تکراری ساخته نشود؛ مستندات و دستورالعمل Agent هماهنگ شود
- [ ] تنظیم «Setup script» خود Environment (کش‌شده) — فقط مالک حساب در UI می‌تواند؛ متن آن در `docs/DEVELOPMENT.md` است

## بررسی اولیه (بدون تغییر)
- `global.json`: SDK `10.0.100`، `rollForward: latestFeature`، Runner تست Microsoft.Testing.Platform؛ همهٔ ۱۱ پروژه `net10.0`.
- علت نبودن `dotnet`: تصویر پایهٔ Cloud (Ubuntu 24.04.5، ۸GB RAM، root) این SDK را ندارد؛ مستندات رسمی Anthropic می‌گوید ابزارهای بیرون از فهرست از پیش‌نصب با Setup script نصب می‌شوند. شبکه مانع نبود: `builds.dotnet.microsoft.com`، `api.nuget.org`، `mcr.microsoft.com` و Docker Hub در سطح Trusted مجازند.
- Docker: `docker`/`dockerd` نصب بود ولی daemon بالا نبود؛ با `dockerd` دستی بالا آمد و SQL Server 2022 (۲٫۳۵GB) بدون مشکل کشیده و اجرا شد. **اصلاح گزارش قبلی:** در PR #57 نوشته شده بود SQL Server در محیط قابل اجرا نیست؛ دقیق‌تر: daemon بالا نبود، نه اینکه ممکن نباشد.
- CI موجود (`.github/workflows/dotnet.yml`) از قبل Restore، Build، xUnit با Service Container SQL Server و E2E دارد؛ Workflow جدید ساخته نشد.

## خلاصه تغییرات
- `scripts/dev/install-dotnet.sh`: نسخه را از `global.json` می‌خواند، تازه‌ترین SDK پایدار پذیرفته‌شده را از `releases.json` رسمی برمی‌دارد، SHA-512 را با فراداده رسمی تطبیق می‌دهد، با `--proto =https` و فقط از `builds.dotnet.microsoft.com`، ایدمپوتنت، نشانهٔ `.faranou-complete` برای تشخیص نصب نیمه‌کاره، قفل `flock`، `tar --no-same-owner`.
- `scripts/dev/test-sqlserver.sh up|env|down`: SQL Server 2022 در Docker روی `127.0.0.1:14333`، رمز تصادفی در فایل ۶۰۰ بیرون از مخزن، برچسب مالکیت کانتینر، فقط کانتینر خودش را حذف می‌کند.
- `scripts/dev/cloud-session-start.sh` + Hook `SessionStart` در `.claude/settings.json` (فقط با `CLAUDE_CODE_REMOTE=true`؛ `permissions` دست‌نخورده).
- `.github/workflows/dotnet.yml`: دو گام کوچک در Job موجود `publish-scripts` (lint با `bash -n`/ShellCheck و بررسی `--check`). بقیهٔ CI تغییر نکرد.
- مستندات: `docs/DEVELOPMENT.md` (بخش Claude Code Cloud)، `docs/TESTING.md`، `docs/REPOSITORY_AUDIT.md` (AUD-17 حل‌شد)، `AGENTS.md`، `CLAUDE.md` (بند Hook).
- کد برنامه، احراز هویت، API، Migration و UI تغییر نکرد.

## تصمیم‌های فنی
- SDK مبتنی بر `global.json` نه نسخهٔ ثابت: خروجی `10.0.402` که با CI (`actions/setup-dotnet` با `10.0.x`) هم‌خوان است. مغایرت قبلی `--solution` از باند 10.0.1xx بود (در `10.0.113` لازم، در `10.0.402` هر دو شکل کار می‌کند).
- Hook به‌جای تکیه فقط بر Setup script: Setup script در مخزن ذخیره نمی‌شود؛ Hook قابل بازبینی و بازتولیدپذیر است. برای سرعت، هر دو می‌توانند همزمان باشند.
- SQL Server را Hook خودکار بالا نمی‌آورد (۲GB، فقط برای تست).

## بررسی و آزمون (اجرای نهایی روی کد نهایی؛ SDK 10.0.402، Node 22.22.0، SQL Server 2022 در Docker)
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `scripts/dev/install-dotnet.sh` | موفق | SDK 10.0.402، SHA-512 تطبیق داده شد، حدود ۹ ثانیه؛ اجرای دوم ایدمپوتنت |
| `dotnet --info` / `--list-sdks` / `--list-runtimes` | ثبت شد | 10.0.402؛ NETCore.App و AspNetCore.App 10.0.13 |
| `dotnet restore Template.sln` | موفق | کد خروج ۰ |
| `dotnet build Template.sln --no-restore -c Release --no-incremental` | موفق | ۰ خطا، ۹۲۱ هشدار (روی SDK 10.0.113 همین کد ۹۲۷؛ تفاوت Analyzer نسخه) |
| `dotnet test Template.sln --no-build -c Release` + `FARANOU_TEST_SQLSERVER` + `FARANOU_REQUIRE_SQLSERVER=1` | موفق | total 306، succeeded 306، failed 0، skipped 0 (≈۲۶ ثانیه). شامل ۴ تست SQL Server (`CorporateGroupSqlServerTests`) |
| همان فرمان با `--solution` بدون SQL Server | موفق با Skip | ۳۰۲ موفق، ۴ Skip، ۰ ناموفق |
| E2E: `scripts/publish.sh` ← `Server.dll --migrate` ← `npm run seed` ← `check-servers.sh` ← `npx playwright test` | موفق | 18 passed (≈۴۵ ثانیه)؛ Chromium واقعی، سرور منتشرشده Production، SQL Server واقعی؛ `publish.sh` خود typecheck/تست/Build کلاینت و بررسی آفلاین را اجرا می‌کند |
| `tests/Template.Tests.Js` (`npm test`) | موفق | 7 از 7 |
| `python3 scripts/check_static_assets.py` | موفق | بدون CDN |
| `bash -n scripts/dev/*.sh`؛ `shellcheck -S style` | موفق | بدون یافته |
| مسیرهای اسکریپت‌ها (دستی): نصب از صفر در پوشهٔ موقت، نصب نیمه‌کاره ترمیم می‌شود، پیوند گم‌شده ترمیم می‌شود، آرگومان ناشناخته رد می‌شود، شبکه قطع = Hook با کد ۰ و پیام، کانتینر بیگانه با همان نام حذف نمی‌شود (`up` و `down`)، تغییر پورت کانتینر خودی را جایگزین می‌کند، `down` کانتینر و فایل رمز را پاک می‌کند | موفق | اجرای واقعی |
| `git diff --check` | موفق | کد خروج ۰ |

**اجرا نشد:** `npm test`/`build` مستقل `ClientApp` (فقط درون `publish.sh`)؛ Job های `publish-windows` و PowerShell؛ Firefox/Safari؛ رفتار واقعی Hook `SessionStart` در Runtime Claude Code (فقط خود اسکریپت با متغیرهای شبیه‌سازی‌شده)؛ Setup script کش‌شدهٔ Environment (باید مالک حساب در UI بگذارد).

## امنیت، داده و سازگاری
- به دیتابیس Production، Migration، Secret واقعی و کد برنامه دست زده نشد. رمز `sa` تصادفی و فقط در فایل ۶۰۰ خارج از مخزن؛ هیچ رمز یا Connection String ثابت در Git نیست. کانتینر پس از تست‌ها با `down` پاک شد.
- **بازبینی مستقل (سطح: اسکریپت‌های اجرایی + Hook؛ نه تغییر حساس برنامه):**
  - `code-reviewer` (۵۹٬۲۳۱ توکن، گزارش ابزار): بدون Critical؛ ۲ High/Medium + چند Medium/Low. اصلاح شد: تشخیص نصب نیمه‌کاره، پیوند فقط در نصب تازه، مخفی‌ماندن خطای `up` زیر `eval`، کانتینر بیات/پورت تغییرکرده، `down` که رمز را بدون حذف کانتینر پاک می‌کرد، نبود retry شبکه، آرگومان ناشناخته، ادعاهای نادرست مستندات (`FaranouE2E` را تست حذف نمی‌کند؛ اصلاح شد)، بند Hook در `CLAUDE.md`.
  - `security-reviewer` (۵۲٬۴۵۹ توکن): بدون Critical/High؛ ۲ Medium. اصلاح شد: Hook فقط با `CLAUDE_CODE_REMOTE=true` اسکریپت را شروع می‌کند، نصب نیمه‌کاره، enforce کردن https و میزبان رسمی و قالب hash، `--no-same-owner/--no-same-permissions`، عدم بازنویسی `/usr/local/bin/dotnet` غیرلینک، رمز با متغیر محیطی به‌جای آرگومان، برچسب مالکیت کانتینر، اعتبارسنجی پورت، اصلاح عبارت «ایزوله» و ادعای SHA-512 (یکپارچگی نه امضا).
  - **اصلاح‌نشده، با دلیل:** `CODEOWNERS` (تصمیم حاکمیتی مسئول مخزن، خارج از دامنه)؛ سنجاق digest تصویر SQL Server (مثل CI برچسب متحرک است؛ تغییر جدا)؛ امضای دیجیتال بسته SDK (فقط یکپارچگی نسبت به فراداده)؛ رمز در `docker inspect` (یک‌بارمصرف، فقط localhost)؛ `latestMinor/latestMajor` فقط در همان major.minor (برای `global.json` فعلی بی‌اثر).
  - محدودیت هر دو بازبین: اسکریپت‌ها را اجرا نکردند؛ اجرا را خودم انجام دادم (جدول بالا). رفتار Hook در Runtime را هیچ‌کدام نیازموده‌اند.
- ارزیابی تقسیم کار: دو بازبین ≈ ۱۱۱٬۶۹۰ توکن؛ یافته‌های واقعی زیادی پیدا کردند (از جمله نصب نیمه‌کاره و `down` خطرناک)؛ صرفه داشت.

## ریسک‌ها و کارهای باقی‌مانده
- Hook اجرای کد مخزن در شروع جلسه است (در Cloud با root) و بیرون از `permissions` اجرا می‌شود؛ پیشنهاد `CODEOWNERS` و بازبینی اجباری برای `.claude/**` و `scripts/dev/**`.
- Setup script کش‌شدهٔ Environment را مالک حساب باید در UI تنظیم کند (متن در `docs/DEVELOPMENT.md`).
- مسائل برند از PR قبلی هنوز باز است و در این PR دست نخورد: انطباق دقیق SVG نشان با طرح تأییدشده تأیید نشده (فایل اصلی طراح نبود)؛ favicon (`faranou-mark.svg`) و `ClientApp/src/components/ui/FaranouLogo.tsx` هنوز نشان قدیمی/متفاوت‌اند.
- CI: نتیجهٔ PR در توضیحات/گزارش نهایی ثبت می‌شود.

## شناسه‌های Git
- Branch: `chore/dotnet-sdk-cloud-test-infra`
- Commit / Pull Request: در گزارش نهایی و PR
