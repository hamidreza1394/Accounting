# تحویل وظیفه — ISSUE-UNNUMBERED-pr27-ci-fix-and-behavioral-test

- **Issue:** PR #27 (`fix/faranou-login-password-toggle`) — ادامهٔ کار روی CI شکست‌خورده
- **Agent/مسئول:** Z.ai CLI Agent
- **تاریخ:** 2026-10-17
- **وضعیت:** انجام‌شده (بدون merge — منتظر تأیید مسئول)

## هدف و معیارهای پذیرش
- [x] بررسی وضعیت نهایی GitHub Actions برای آخرین commit `c61ffe8bb3c8c88df996a4a173a48e18602b2bef`
- [x] اصلاح علت شکست CI و Push دوباره
- [x] تأیید سبزشدن CI
- [x] بررسی نوع تست موجود (ساختاری در برابر رفتاری)
- [x] افزودن تست رفتاری برای نمایش/پنهان‌کردن رمز
- [x] ثبت گزارش در PR بدون merge

## خلاصه تغییرات
- ریشه‌یابی علت شکست CI: گزارء `Assert.DoesNotContain("type=\"submit\"", markup[toggleIndex..])` در `FaranouMarkup_UsesPasswordToggleButtonAndMatchingControlId` رشتهٔ `type="submit"` را در **کل** بازماندهٔ مارکاپ جستجو می‌کرد، که شامل دکمهٔ واقعی Submit پایین فرم است. این گزارء به‌جای محافظت از قرارداد دکمهٔ toggle، در مارکاپ درست آزمون را شکست می‌داد.
- اصلاح: گزارء به‌جای همهٔ بازمانده، فقط **تگ آغازین خود دکمهٔ toggle** را برش می‌دهد (از نزدیک‌ترین `<button` پیش از `data-password-toggle` تا نخستین `>` پس از آن). این قرارداد را بدون برخورد با دکمهٔ submit نگه می‌دارد.
- افزودن پروژهٔ جدید `tests/Template.Tests.Js/` با ۷ آزمون رفتاری واقعی بر پایهٔ jsdom و `node:test`. این آزمون‌ها مارکاپ واقعی `Login.cshtml`، `taraz-login.js` و `site.js` را در jsdom بار می‌کنند، `DOMContentLoaded` را فعال می‌سازند، روی toggle کلیک می‌کنند و تأیید می‌کنند که `password.type` بین `"password"` و `"text"` تغییر می‌کند و `aria-pressed`/`aria-label` هماهنگ می‌مانند. همچنین تأیید می‌کنند که `site.js` (که با `.not('.taraz-login-form [data-password-toggle]')` از toggle فرانو مستثنی شده) داخل فرم فرانو دوباره فعال نمی‌شود.
- افزودن گام جدید به workflow CI برای راه‌اندازی Node 20 و اجرای `npm install` + `node --test` در پوشهٔ آزمون رفتاری. گام «Publish the failure details» نیز به‌روزرسانی شد تا لاگ‌های `behavioral-test.log` و `npm-install.log` را هم شامل شود.

## فایل‌های مهم تغییرکرده
- `tests/Template.Tests/LoginPasswordToggleTests.cs` — اصلاح منطق برش و گزارء نادرست که دکمهٔ submit واقعی را به‌جای toggle شناسایی می‌کرد.
- `tests/Template.Tests.Js/package.json` — فایل جدید؛ تعریف وابستگی `jsdom@^24` و اسکریپت `npm test` با `node --test`.
- `tests/Template.Tests.Js/password-toggle.test.js` — فایل جدید؛ ۷ آزمون رفتاری DOM-level برای toggle رمز فرانو.
- `.github/workflows/dotnet.yml` — افزودن گام‌های `Setup Node` و `Behavioral tests (jsdom)`؛ به‌روزرسانی گام publish failure برای شامل‌کردن لاگ‌های npm و behavioral.
- `docs/handoffs/ISSUE-UNNUMBERED-pr27-ci-fix-and-behavioral-test.md` — این فایل.

## تصمیم‌های فنی
- **انتخاب jsdom به‌جای Playwright/Selenium:** toggle رمز در `taraz-login.js` با `addEventListener` معمول DOM پیاده‌شده و نه با API مرورگر ویژه؛ jsdom به‌اندازهٔ کافی کامل است تا این قرارداد را واقعی آزمون کند و نصب آن در CI سبک است (~۶۰ بستهٔ npm، کمتر از ۵ ثانیه). Playwright مرورگر کامل نصب می‌کرد (~۳۰۰MB) که برای این نیاز اضافی بود.
- **شبیه‌سازی خروجی Razor:** cshtml شامل `@(... ? "RTL" : "LTR")` و `asp-for="ViewModel.Password"` است که jsdom نمی‌تواند اجرا کند. تابع `buildLoginDocument` این عبارات را به مقدار شاخهٔ RTL (پیش‌فرض صفحهٔ فرانو) و `id="ViewModel_Password"` توسعه می‌دهد، دقیقاً مطابق آن‌که ASP.NET Core در زمان درخواست تولید می‌کند. این شبیه‌سازی در کامنت‌های فایل توضیح داده شده و قابل بازبینی است.
- **نحوهٔ اجرای آزمون:** از `node:test` داخلی Node 20+ استفاده شد تا نیازی به افزودن `mocha`/`jest` نباشد. وابستگی پروژه فقط `jsdom` است.
- **عدم merge:** طبق درخواست کاربر، PR بدون تأیید merge نشد.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `node --test` (در `tests/Template.Tests.Js` محلی) | موفق | ۷ آزمون، ۷ pass، 0 fail (مدت: ~۵۵۰ms) |
| شبیه‌سازی گزارء xUnit با پایتون | موفق | منطق اصلاح‌شده هر سه گزارء را تأیید می‌کند (به اسکریپت `/home/z/my-project/scripts/verify_test_logic.py` رجوع کنید) |
| `dotnet build`/`dotnet test` محلی | اجرا نشده | SDK .NET در sandbox نصب نیست؛ وابست به CI GitHub Actions برای تأیید نهایی. |
| `python3 scripts/check_static_assets.py` | اجرا نشده | در همین PR تغییر داده نشده؛ توسط CI اجرا خواهد شد. |
| CI روی commit `e947489` (فقط اصلاح xUnit) | موفق | هر دو run `39062817222` و `39062820291` با `conclusion: "success"` تکمیل شد؛ وضعیت PR: `MERGEABLE`، `mergeStateStatus: CLEAN`. |
| CI روی commit با آزمون رفتاری جدید | در حال اجرا | نتیجهٔ نهایی در گزارش نهایی اعلام خواهد شد. |

## امنیت، داده و سازگاری
- این تغییر **منطق سمت سرور، احراز هویت، پایگاه‌داده و Migration را لمس نمی‌کند**.
- آزمون رفتاری فقط مارکاپ و اسکریپت سمت کلاینت موجود را می‌خواند؛ هیچ مسیر سمت سرور، API یا پایگاه‌داده‌ای فراخوانی نمی‌کند.
- شبیه‌سازی `id="ViewModel_Password"` در آزمون با قرارداد ASP.NET Core Tag Helper هم‌خوان است و دادهٔ حساس تولید نمی‌کند.
- وابستگی جدید `jsdom@^24` یک بستهٔ شناخته‌شده و پایدار است (شعاع واجد شرایط: صفحهٔ GitHub OpenJS Foundation)؛ license: MIT.

## ریسک‌ها و کارهای باقی‌مانده
- **اجراهای مرورگر واقعی پوشش داده نشد.** آزمون jsdom از مرورگر واقعی (Chrome/Firefox) عبور نمی‌کند؛ مواردی مانند CSS، reflow، یا رویدادهای pointer-level باید دستی یا با Playwright آزموده شوند. مراحل آزمون دستی در کامنت PR آمده است.
- **شبیه‌سازی Razor آزمون از رندر واقعی ASP.NET Core عبور نمی‌کند.** اگر Tag Helper در نسخهٔ آینده `id` متفاوتی تولید کند، آزمون باید به‌روزرسانی شود.
- **تست‌های .NET محلی اجرا نشدند** چون SDK .NET در sandbox موجود نیست؛ وابست به CI GitHub Actions برای تأیید نهایی قطعی.
- **تست‌های E2E با مرورگر واقعی (مثلاً Playwright.NET) در آینده می‌تواند افزوده شود** تا رفتار نهایی در Chrome را پوشش دهد؛ این فعلاً خارج از محدودهٔ PR است.
- **باقی‌ماندهٔ مسئول:** تأیید یا درخواست اصلاح کامنت PR؛ merge پس از تأیید.

## شناسه‌های Git
- Branch: `fix/faranou-login-password-toggle`
- Commitهای این PR روی شاخه:
  - `e947489` — `test(login): scope toggle regression assertion to the toggle's own opening tag` (اصلاح شکست CI)
  - commit جدید (پس از این فایل) — `test(login): add jsdom behavioral tests for password toggle`
- Pull Request: https://github.com/hamidreza1394/Accounting/pull/27
