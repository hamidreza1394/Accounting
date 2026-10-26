# تحویل وظیفه — ISSUE-108-pin-dotnet-sdk

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/108
- **Agent/مسئول:** ChatGPT / Faranou Senior Developer + Testing/Quality
- **تاریخ:** 2026-10-25
- **وضعیت:** تغییر CI اعمال شده؛ منتظر CI واقعی روی PR

## هدف و معیارهای پذیرش
- [x] علت شکست واقعی از لاگ CI تعیین شد: دانلود SDK `10.0.403` و Runtime `10.0.14` با HTTP 409 در `actions/setup-dotnet@v6`.
- [x] چهار step مربوط به .NET در workflow عمومی از `10.0.x` به `10.0.402` پین شدند؛ هیچ package یا target framework تغییر نکرد.
- [x] WORK_LOG و DEVELOPMENT دربارهٔ رخداد و سیاست ارتقای نسخه به‌روزرسانی شدند.
- [ ] CI روی این شاخه Setup/Restore/Build/Test و migration guards را با SDK پین‌شده تأیید کند.

## خلاصه تغییرات
- `.github/workflows/dotnet.yml`: چهار مقدار `dotnet-version` روی `10.0.402` ثابت شدند.
- `docs/WORK_LOG.md`: شواهد خطای HTTP 409، علت و محدودیت اعتبارسنجی ثبت شد.
- `docs/DEVELOPMENT.md`: نسخهٔ فعلی CI و سیاست ارتقای patch مستند شد.

## تصمیم‌های فنی
- انتخاب نسخهٔ دقیق، تکرارپذیری build را بهبود می‌دهد و مسیر wildcard که در لحظه SDK/runtime غیرقابل‌دانلود انتخاب کرده بود دور می‌زند.
- `global.json` و نسخه‌های NuGet/EF Core تغییری نکردند. `global.json` همچنان اجازهٔ roll-forward به SDK سازگار را می‌دهد.
- این pin راه‌حل دائمی برای patchهای آینده نیست؛ ارتقا باید جداگانه و بعد از تأیید در دسترس بودن binary و اجرای کل CI صورت گیرد.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| .NET CI run `40224122273` قبل از اصلاح | شکست | Job `build-and-test` در `Setup .NET` به‌علت HTTP 409 در دریافت SDK/runtime متوقف شد؛ Restore/Build/Test اجرا نشد |
| Vendor CI run `40224122224` | موفق | SDK `10.0.402`: Restore، Build، EF model snapshot check، Test، publish، private-key output check موفق |
| `dotnet restore/build/test` محلی | اجرا نشده | این Agent SDK/محیط build محلی در دسترس ندارد؛ CI باید نتیجه را ارائه کند |
| `git diff --check` | اجرا نشده محلی | تغییرات از GitHub Contents API اعمال شدند |

## امنیت، داده و سازگاری
- هیچ کد runtime، package version، TargetFramework، Schema/Migration یا دیتابیس مشتری تغییر نکرد.
- فقط پیکربندی build CI و مستندات توسعه تغییر کرده‌اند.
- SDK pin باید در چرخهٔ patch security بازبینی شود؛ این PR آن را حذف یا نادیده نمی‌گیرد.

## ریسک‌ها و کارهای باقی‌مانده
- CI روی HEAD این PR باید با نسخهٔ دقیق اجرا شود. اگر upstream هنوز SDK 10.0.402 را ارائه نکند، مشکل دانلود حل نشده است.
- بررسی روزآمد بودن SDK باید در Issue جدا انجام شود؛ ارتقای `global.json` و workflow باید هماهنگ و کامل تست شود.

## شناسه‌های Git
- Branch: `ci/issue-108-pin-dotnet-sdk-402`
- Commit: پس از آخرین تغییر تعیین شود.
- Pull Request: پس از ایجاد ثبت شود.