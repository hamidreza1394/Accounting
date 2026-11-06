# تحویل وظیفه — ISSUE-UNNUMBERED-access-security-ui-completion

- **Issue:** UNNUMBERED (شمارهٔ Issue واقعی در دسترس نبود)
- **Agent/مسئول:** Claude Code
- **تاریخ:** 2026-11-06
- **وضعیت:** انجام‌شده در سطح ClientApp؛ بازبینی مالک و CI باز است

## هدف و معیارهای پذیرش
- [x] مجوز UI از `/me/permissions` شرکت فعال، نه `session.isAdministrator`؛ fail-closed در loading/error؛ بازخوانی با تغییر شرکت.
- [x] تفکیک مدیر نصب / مدیر شرکت / عضو عادی در منو، مسیرها و صفحات نقش.
- [x] فهرست/ایجاد/ویرایش نقش، افزودن عضو، انتقال، سقف تفویض، نقش سیستمی، 403/409 پوشش داده شد.
- [x] UI نشست‌ها با سه endpoint مرحلهٔ D-069.
- [ ] E2E و تست‌های .NET در این محیط اجرا نشدند.

## خلاصه تغییرات
- `lib/permissions.ts`، `lib/useCompanyPermissions.ts`، `lib/router.tsx`: مدل `AccessSnapshot`، `can()`، بازخوانی و خطای مجوز.
- `AppShell`/`AppRoutes`/`App`: منو و مسیرها بر اساس `can(access, …)`.
- صفحات نقش: `RoleMemberAssign` جدید، حالت فقط‌خواندنی خارج از سقف، 409 با بازخوانی، `onForbidden`.
- `ActiveSessions` جدید در `MyAccountPanel` (تب امنیت حساب).
- `api/client.ts`/`types.ts`: `assignCompanyRoleMember`، `getMySessions`، `revokeMySession`، `revokeMyOtherSessions` فقط بر اساس DTOهای مستند `docs/api/USER_ACCESS_MANAGEMENT_API.md`.

## تصمیم‌های فنی
- تصمیم معماری جدیدی گرفته نشد؛ پیاده‌سازی D-069، D-071، D-012 و D-013 است.
- سقف تفویض سمت کلاینت فقط راهنما است: نقشی که کدی خارج از کاتالوگ (= سقف) مدیر شرکت دارد فقط‌خواندنی نمایش داده می‌شود تا PUT تعویض کامل مجوزها چیزی را بی‌صدا حذف نکند؛ تصمیم نهایی با سرور است.
- «Retry-After» برای 429 پیاده نشد (`ApiError` هدر را نگه نمی‌دارد)؛ فقط پیام فارسی نمایش داده می‌شود.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `npx tsc --noEmit` (ClientApp) | موفق | بدون خطا |
| `npx vitest run` (ClientApp) | موفق | ۶۱ فایل، ۶۳۵ تست (پایه قبل از تغییر: ۵۷ فایل، ۵۴۶ تست) |
| `npm run build` / `npm run check:offline` | موفق | ۳۶ منبع محلی، بدون منبع خارجی |
| `python3 scripts/check_static_assets.py` | موفق | بدون ارجاع CDN |
| `dotnet build/test` | اجرا نشد | `dotnet` در این محیط نصب نیست؛ Backend تغییر نکرد |
| Playwright E2E (`tests/e2e`) | اجرا نشد | نیازمند سرور و SQL Server؛ اسپک‌ها به رشته‌های تغییرکرده ارجاع نمی‌دهند (جستجوی متنی) |

## بازبینی مستقل
- `security-reviewer` و `code-reviewer` (فقط‌خواندنی) روی Diff اجرا شدند. بدون یافتهٔ Critical/High. یافتهٔ Medium (حلقهٔ 403 ↔ بازخوانی مجوز) اصلاح و با تست رگرسیون پوشش داده شد: `refresh()` اکنون نرم است (صفحه unmount نمی‌شود) و `retry()` سخت است. پیشنهاد نشان‌دادن راهنما برای نتایج بیش از ۲۵ مورد اعمال شد.
- بدون اصلاح، آگاهانه: (۱) پاسخ `/me/permissions` با `company: null` یا شرکت دیگر ← `error` و عدم اعطا (fail-closed، طبق الزام)، حتی برای مدیر نصب؛ (۲) شکست بازخوانی هنگام بازگشت به تب ← `error` (fail-closed)؛ (۳) `usePermissions` فعلاً فقط در تست استفاده می‌شود؛ (۴) پنهان‌بودن افزودن عضو برای نقش غیرفعال قاعدهٔ UI است و در مستند API ذکر نشده؛ سرور تصمیم‌گیرنده است.

## امنیت، داده و سازگاری
- هیچ توکن/شناسهٔ نشست نمایش، لاگ یا در localStorage/sessionStorage ذخیره نمی‌شود (تست شده). شناسهٔ نشست فقط در مسیر درخواست revoke می‌رود.
- `TenantId` از کلاینت اعتماد نمی‌شود؛ تطبیق `companyId` پاسخ با شرکت فعال بررسی می‌شود.
- تغییر Schema/Migration/API وجود ندارد. HR دست نخورد.

## ریسک‌ها و کارهای باقی‌مانده
- E2E و تست‌های .NET باید در CI اجرا و بررسی شوند.
- ۴۲۹ بدون احترام به `Retry-After`.
- افزودن عضو به جستجوی دفترچه کاربران شرکت (`users.list`) وابسته است؛ مدیر شرکت بدون این مجوز پیام توضیحی می‌بیند.

## شناسه‌های Git
- Branch: feature/access-security-ui-completion
- Commit: (پس از Commit)
- Pull Request: (پس از ایجاد)
