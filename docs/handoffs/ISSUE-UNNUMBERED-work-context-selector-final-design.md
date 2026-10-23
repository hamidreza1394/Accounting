# تحویل وظیفه — ISSUE-UNNUMBERED-work-context-selector-final-design

- **Issue:** `UNNUMBERED`
- **Agent/مسئول:** Claude (Lead) + `frontend-developer`؛ بازبینی: `code-reviewer`، `security-reviewer`
- **تاریخ:** 2026-10-23
- **وضعیت:** انجام‌شده (PR ادغام‌نشده)

## هدف و معیارهای پذیرش
- [x] پیاده‌سازی وفادار طرح نهایی تأییدشده روی صفحهٔ واقعی `/app/` (فقط انتخاب شرکت و دوره)
- [x] دوره‌ها راست و متمایز؛ شرکت‌ها چپ و ردیفی؛ نوار پایین با دکمهٔ بزرگ؛ بدون «فرانو - داشبورد»؛ آیکون خروج زیباتر
- [x] فقط داده و کنترل واقعی؛ بدون برچسب گروه شرکتی ساختگی؛ بدون تغییر API/Schema
- [ ] مقایسهٔ انسانی با مرجع

## خلاصه تغییرات
ظاهر و چیدمان انتخاب‌گر مطابق مرجع: هدر، پس‌زمینهٔ SVG، پنل‌ها، ردیف‌ها، نوار پایین، جست‌وجوی دوره (`matchesYear`)، وضعیت سال (`yearStatus`)، ارتفاع روان. جزئیات و انحراف‌ها: `docs/WORK_CONTEXT_SELECTOR_UI.md` بخش ۹.

## فایل‌های مهم تغییرکرده
- `ClientApp/src/components/{SelectionLayout,WorkContextSelector,SelectionFooter(جدید),Feedback}.tsx`، `ui/{SelectionBackdrop(جدید),ChoiceCard,Button,FaranouLogo,icons}.tsx`، `lib/{fiscalYears,search}.ts`، `styles/index.css` (+ تست‌ها)
- `tests/e2e/specs/faranou.spec.mjs` (عنوان جدید و تست «final design»)
- `docs/{WORK_CONTEXT_SELECTOR_UI,WORK_LOG}.md`

## تصمیم‌های فنی
تصمیم معماری جدیدی نیست. گروه‌بندی سال از `startDate|endDate` ادامه دارد. پیش‌انتخاب بدون تغییر (D-031).

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| `dotnet build Template.sln -c Release` | موفق | 0 خطا، 610 هشدار |
| `dotnet test` روی SQL Server 2022 واقعی | موفق | 497 کل، 495 موفق، 2 Skip، 0 شکست |
| `npm run typecheck` / `npm test` / `npm run build` / `check:offline` | موفق | 85 تست؛ JS 273.6KB (gzip 83.2KB)، CSS 40.5KB |
| `python3 scripts/check_static_assets.py` / `git diff --check` | موفق | |
| E2E Playwright/Chromium روی SQL Server واقعی | موفق | 25 تست |
| lint، axe، صفحه‌خوان، Firefox/Safari، دستگاه فیزیکی | اجرا نشد | اسکریپت/ابزار موجود نیست |

## بازبینی مستقل
- `security-reviewer`: بدون یافته (نکتهٔ جزئی غیرامنیتی: شناسه‌های SVG ثابت‌اند).
- `code-reviewer`: بدون مسدودکننده؛ اصلاح‌شده: متن وضعیت دوره در نوار پایین، truncate سال. باز: «کد ملی» در برابر «شناسه ملی» (مطابق مرجع نگه داشته شد؛ نیاز به تأیید محصول)، اعلام «در حال ورود» به صفحه‌خوان، تست E2E سرریز موبایل.

## امنیت، داده و سازگاری
بدون تغییر سرور/API. انتخاب همچنان با `POST /api/v1/context` و شناسه‌های فهرست سرور اعتبارسنجی می‌شود؛ بدون ذخیرهٔ مرورگری؛ CSP سازگار (SVG درون‌خطی).

## ریسک‌ها و کارهای باقی‌مانده
بازبینی انسانی؛ پس‌زمینه تقریبی است؛ برچسب‌های گروه شرکتی وقتی D-033..35 پیاده شد؛ `AGENTS.md`/`CLAUDE.md` هنوز «ADR بعدی D-036» دارند.

## شناسه‌های Git
- Branch: `feature/work-context-selector-final-design`
- Commit / Pull Request: در PR ثبت می‌شود
