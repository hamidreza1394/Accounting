# Handoff — پوسته UI (Sidebar / Workspace Tabs) پس از PR #607

**تاریخ:** 2026-11-09  
**Merge:** PR #607 → `main` (`52eea6a`)

## انجام‌شده

1. **گروه‌های اصلی Sidebar**  
   پس‌زمینه سرمه‌ای متمایز، حاشیه، حالت باز (`aria-expanded` / `.faranou-nav-group--open`) در `premium-dashboard.css`.

2. **ساختار سازمانی تو در تو**  
   «ساختار سازمانی و کارکنان» والد قابل باز/بسته برای کارکنان، واحدهای سازمانی و سمت‌های سازمانی.  
   پیاده‌سازی: `ClientApp/src/components/shell/NestedNavSections.tsx`  
   اتصال: `AppSidebar.tsx`  
   تست: `AppSidebar.test.tsx` (fold/expand بدون به‌هم‌ریختن گروه والد).

3. **Workspace Tabs**  
   تب بزرگ‌تر، حالت فعال واضح‌تر (طلایی/کرم).

4. **Form lifecycle**  
   از قبل با `lib/formGuard` و نشانگر تب (Clean / Dirty / Saving / Error) روی فرم کارمند و فرم‌های متصل موجود بود؛ در این PR تغییر نکرد.

## CI

client، build-and-test، e2e روی #607 سبز بودند.

## خارج از دامنه

- Router جدید / کتابخانه سنگین
- Stage 4
- Screenshot واقعی از UI زنده در محیط agent (Backend مرورگر واقعی در دسترس نبود)
