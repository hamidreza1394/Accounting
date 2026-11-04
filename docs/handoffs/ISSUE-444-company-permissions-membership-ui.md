# تحویل وظیفه — ISSUE-444-company-permissions-membership-ui

- **Issue:** #444؛ قراردادهای Backend #510 و #511
- **Agent/مسئول:** Arena
- **تاریخ:** 2026-11-04
- **وضعیت:** پیاده‌سازی شده؛ E2E واقعی منتظر CI

## هدف و معیارها
- [x] نمایش منوی کاربران شرکت و صف درخواست فقط با مجوز شرکت جاری از API واقعی؛ بدون فرض مجوز مدیر نصب.
- [x] جستجوی دقیق مقصد، ثبت درخواست، مشاهده وضعیت و تأیید/رد با فهرست نقش‌های قابل تخصیص سرور.
- [x] جلوگیری از استفاده پاسخ دیررس شرکت قبلی؛ خطا، retry و 401.
- [ ] اجرای E2E مثبت واقعی درخواست/تأیید به fixture معتبر CustomerBoundary و approver نیاز دارد.

## تغییرات مهم
- `ClientApp/src/api/{client,types}.ts`: قراردادهای `/me/permissions`، destinations، ثبت، assignable-roles با validation و anti-forgery موجود.
- `ClientApp/src/App.tsx`, `lib/useCompanyPermissions.ts`, `components/AppShell.tsx`: مجوزهای نمایشی، fail-closed در loading/خطا، بررسی companyId و لغو درخواست قدیمی.
- `components/membership/`: ثبت و جستجوی مقصد، و صف تأیید با نقش‌های صفحه‌بندی‌شده و مجاز سرور؛ وضعیت درخواست‌های خود و رد/تأیید موجود حفظ شد.
- تست‌های Client و `tests/e2e/specs/access-ui.spec.mjs`: قرارداد، رفتار، مرز مشتری بدون داده mock در سناریوی E2E.

## تصمیم‌ها و امنیت
`/me/permissions` شرکت را از work context تعیین می‌کند؛ شناسه شرکت از کلاینت به آن ارسال نمی‌شود و قبل از نمایش تطبیق داده می‌شود. `users.list` و `users.membership.approve` کدهای واقعی کاتالوگ‌اند، نه مجوزهای ساختگی؛ authorization همیشه سمت سرور است. نصب-مدیر برای APIهای مدیریتی سراسری همچنان از session سرور استفاده می‌کند و برای مجوز شرکتی bypass نمی‌شود. مقصد و نقش صرفاً از API محدودشده سرور می‌آیند؛ نقش سیستم حتی در پاسخ ناسازگار نشان داده نمی‌شود. Trade همچنان غیرفعال است.

## تست‌ها
| فرمان | نتیجه واقعی |
|---|---|
| `npm ci --no-fund --no-audit` | موفق |
| `npm run typecheck` و `npm run build` | موفق؛ build هشدار فونت محلی داشت |
| `npm test -- --run` | ۵۷ فایل، ۵۴۳ تست موفق پیش از دو تست تکمیلی؛ پس از آن دو فایل مربوط مجدداً با ۲۲ تست موفق |
| `npm run check:offline` و `python3 scripts/check_static_assets.py` | موفق |
| `node --check tests/e2e/specs/access-ui.spec.mjs` و `git diff --check` | موفق |
| E2E واقعی / SQL Server / .NET build | اجرا نشده: dotnet و Docker در محیط موجود نیست؛ نتیجه CI لازم است |

## باقی‌مانده و وابستگی Backend
- seed فعلی مرز مشتری ندارد؛ مثبت ثبت/تأیید/رد، تغییر Tenant و نقش تأییدکننده در E2E واقعی نیازمند fixture یکبارمصرف معتبر است. تست افزوده‌شده فقط مسیر منفی مرز و ورودی نامعتبر را پوشش می‌دهد.
- API گزارش #519 در هنگام بررسی باز و دارای conflict بود؛ پیاده‌سازی یا حل تعارض Backend خارج از این کار است.
- ErrorBoundary، گزارش امنیت، Trade و سناریوهای دیگر E2E برش مستقل‌اند. هیچ API، C#، EF، schema، migration یا مجوز سمت سرور تغییر نکرد.

## شناسه‌های Git
- Branch: `arena/e242088d-accounting`
- Commit / PR: در تاریخچه Git و صفحه PR.
