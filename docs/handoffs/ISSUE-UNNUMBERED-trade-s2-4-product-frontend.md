# تحویل وظیفه — ISSUE-UNNUMBERED-trade-s2-4-product-frontend

- **Issue:** `UNNUMBERED`
- **Agent/مسئول:** frontend-developer (Claude Code)
- **تاریخ:** 2026-11-13
- **وضعیت:** انجام‌شده (کلاینت React؛ بدون اجرای end-to-end روی مرورگر و SQL Server؛ Commit/Push انجام نشد)

## هدف و معیارهای پذیرش
- [x] مسیرهای `/trade/products`، `/new`، `/:id`، `/:id/edit` (فارسی/RTL، بدون CDN) با `/new` پیش از `/:id`
- [x] فهرست: جست‌وجوی سمت‌سرور (کد/نام)، فیلتر `isActive`، صفحه‌بندی؛ سلول کد `whitespace-nowrap`
- [x] جزئیات فقط‌خواندنی؛ فرم ایجاد/ویرایش (کد، نام، گروه کالا، کد تعرفه به‌صورت متن، واحد پایه با `SearchableCombobox` از `/trade/reference/units`)
- [x] اعتبارسنجی کلاینت هم‌تراز سرور (کد 64، نام 200، گروه 64، HS لاتین قابل‌چاپ حداکثر 16، واحد الزامی/معتبر)
- [x] خطاها دادهٔ فرم را نگه می‌دارند: 400 فیلدی، 409 کد تکراری زیر فیلد کد، 409 هم‌زمانی با «حفظ ویرایش‌های من / نمایش مقادیر تازه»، 413/403/500/شبکه
- [x] نگهبان Route بر اساس capabilities (view برای صفحات؛ create/update برای عملیات نوشتن؛ هر دو با view)
- [x] برگ «کالاها» زیر «بازرگانی خارجی» فقط با `available` و `trade.form.product.view`
- [x] بدون حذف/غیرفعال‌سازی (D-083)، بدون Quick Create/LookupDialog، بدون کتابخانهٔ جدید

## خلاصه تغییرات
- صفحات و فرم کالا دقیقاً با الگوی Party: `ProductsPage`، `ProductDetailPage`، `ProductEditPage`، `ProductCreatePage`، `ProductEditor`، هوک `useUnits`.
- تعمیم حداقلی قطعات مشترک (رفتار Party بدون تغییر، تست‌های Party سبز):
  - `tradeErrors.ts`: منطق `mapTradeFailure` به `mapFailure(cause, subject)` منتقل شد؛ `mapTradeFailure` (Party) و `mapProductFailure` (Product) پوسته‌های نازک‌اند. تفاوت‌ها فقط کلیدهای فیلد، کد ProblemDetails کد تکراری (`trade_product_code_duplicate`) و متن‌های تکراری/هم‌زمانی/پیدا‌نشد است.
  - `partyValidation.ts`: `text` به `validateText` صادر شد (و `forbiddenCharactersMessage`) تا کالا دوباره‌نویسی نکند.
  - `tradeFlash.ts` و `listVersion.ts`: کارخانهٔ کوچک مشترک؛ `partyFlash`/`partyListRefresh` همان صادرات قبلی را نگه می‌دارند.
  - `TradeAccess.tsx`: سه hint `product.view/create/update` (write همراه view، مستقل از `accessMode`).
- `AppSidebar.tsx`: برگ «کالاها» (آیکون `Package` از lucide محلی) و گیت `/trade/products`.
- `docs/MENU_FORM_CATALOG.md`: ردیف ۳.۲ و چهار ردیف Route کالا.

## فایل‌های مهم تغییرکرده
- جدید: `ClientApp/src/features/trade/pages/Product{s,Detail,Edit,Create}Page.tsx`، `components/ProductEditor.tsx`، `components/useUnits.ts`، `lib/productValidation.ts`، `lib/productFlash.ts`، `lib/productListRefresh.ts`، `lib/tradeFlash.ts`، `lib/listVersion.ts`
- تغییر: `features/trade/{tradeRoutes.tsx,api/tradeClient.ts,api/tradeTypes.ts,access/TradeAccess.tsx,lib/tradeErrors.ts,lib/tradeLabels.ts,lib/partyValidation.ts,lib/partyFlash.ts,lib/partyListRefresh.ts}`، `components/shell/AppSidebar.tsx`
- تست جدید: `pages/Product{sPage,DetailPage,Forms}.test.tsx`، `productRoutes.test.tsx`، `lib/productValidation.test.ts`، `api/productClient.test.ts`
- تست تکمیل‌شده: `lib/tradeErrors.test.ts`، `access/TradeAccess.test.tsx`، `test/{tradeFixtures.ts,tradeTestUtils.tsx}`، `components/shell/{AppSidebar,menuRouteConsistency}.test.tsx`، `TradeIntegration.test.tsx`
- مستند: `docs/MENU_FORM_CATALOG.md`

## تصمیم‌های فنی
- تصمیم معماری جدیدی گرفته نشد؛ ADR جدید لازم نیست.
- ستون «کد» در فهرست اول است؛ پیوند ردیف (سلول نخست `DataTable`) نام دسترس‌پذیرش کد کالا است.
- فیلتر `productGroupCode` فهرست عمداً در UI نیامد (در محدودهٔ درخواست نبود؛ کلاینت API آن را نمی‌فرستد).
- فقط واحد از مرجع واحدها خوانده می‌شود؛ شکست بارگذاری آن فرم را قابل‌استفاده نگه می‌دارد (کد خام + «تلاش دوباره»، و سرور تصمیم‌گیر است).
- `isActive` و هر فیلد Tenant هرگز در بدنهٔ POST/PUT فرستاده نمی‌شود (ساخت فیلد‌به‌فیلد؛ تست شده).

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `npm run typecheck` (ClientApp) | موفق | کد خروج 0 |
| `npx vitest run` (کل ClientApp) | 1265 موفق، 3 ناموفق از 1268 (111 فایل؛ 2 فایل ناموفق) | سه شکست فقط `securityAuditClient.test.ts` و `SecurityAuditPage.test.tsx` (شناخته‌شده، نامرتبط). یک اجرای میانی کل مجموعه، `AppRouting.test.tsx` را هم یک‌بار ناموفق نشان داد (زیر بار، احتمالاً Timeout)؛ تنهایی 27/27 و در اجرای کامل بعدی سبز شد. |
| `npx vitest run src/features/trade` | 160 موفق از 160 (13 فایل)، پیش از افزودن تست‌های sidebar/integration | شامل تست‌های Party که دست‌نخورده سبز ماندند |
| `npm run build` | موفق | |
| `npm run check:offline` | موفق | 89 منبع محلی، بدون منبع خارجی |
| `python3 scripts/check_static_assets.py` | موفق | بدون ارجاع CDN |
| `dotnet build/test` | اجرا نشد | تغییر Razor/بک‌اند نبود |
| E2E مرورگر / SQL Server | اجرا نشد | فقط تست‌های Vitest با `fetch` ساختگی |

تست‌های جدید کالا: فهرست، جزئیات، فرم‌ها (ایجاد/ویرایش، هم‌زمانی، تکراری، 400/403/413/500/شبکه)، `SearchableCombobox` واحد (کیبورد، جست‌وجو با کد/نام، شکست بارگذاری)، اعتبارسنجی، کلاینت/پارسر، نگهبان Route، sidebar، یکپارچگی Route/منو و Integration پوستهٔ برنامه.

## امنیت، داده و سازگاری
- فقط `/api/v1/trade/*` با Cookie و Anti-forgery (`sendJson`)؛ شناسهٔ شرکت/دوره هرگز ارسال نمی‌شود. hintها فقط نمایشی‌اند و سرور هر درخواست را دوباره می‌سنجد؛ مجوز Party، Product را باز نمی‌کند (تست‌شده).
- CSP: بدون اسکریپت/استایل inline، بدون وابستگی جدید، بدون Razor؛ Migration/Schema/بک‌اند دست نخورد.
- پیام‌های خطا مقدار واردشده را بازتاب نمی‌دهند.

## ریسک‌ها و کارهای باقی‌مانده
- بازبینی دستی RTL/واکنش‌گرایی روی مرورگر واقعی انجام نشد.
- `docs/MENU_FORM_CATALOG.md` به‌روز شد (دستور فهرست «mirror» آن را می‌خواست) هرچند دستور «docs غیر از handoff» ظاهراً متناقض بود؛ در صورت نیاز به‌راحتی برگردانده می‌شود. `docs/PROJECT_STATUS.md` و `docs/TRADE_MODULE.md` عمداً دست نخوردند.
- تکرار ساختاری `ProductEditor` در برابر `PartyEditor` پذیرفته شد (دو فرم با فیلد/متن متفاوت)؛ استخراج پوستهٔ مشترک فرم می‌تواند در PR جدا انجام شود.
- هنوز بازبینی مستقل (`code-reviewer`) انجام نشده است.

## شناسه‌های Git
- Branch: `feat/trade-s2-4-product-slice`
- Commit: ندارد (طبق دستور، Commit/Push نشد)
- Pull Request: ندارد
