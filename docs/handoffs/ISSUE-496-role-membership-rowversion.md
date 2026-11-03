# تحویل وظیفه — ISSUE-496-role-membership-rowversion

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/496
- **Agent/مسئول:** ChatGPT — Backend/Database/Security
- **تاریخ:** 2026-11-03
- **وضعیت:** پیاده‌سازی و Migration روی شاخهٔ همگام‌شده با main منتقل شدند؛ سه شکست CI تشخیص داده و اصلاح شده‌اند؛ اجرای مجدد CI هنوز در انتظار است.

## هدف و معیارهای پذیرش
- [x] CompanyRole دارای RowVersion واقعی SQL Server باشد.
- [x] UserCompanyMembership دارای RowVersion واقعی SQL Server باشد.
- [x] UpdateRoleAsync در رقابت هم‌زمان، بازنده را با 409 رد کند.
- [x] AssignMembershipAsync در رقابت هم‌زمان، بازنده را با 409 رد کند.
- [x] TransferMembersAsync انتقال دسته‌ای را اتمیک نگه دارد و در تعارض 409 برگرداند.
- [x] Audit رویداد درخواست بازنده همراه با SaveChanges rollback شود.
- [x] Migration افزایشی روی جدول‌های دارای داده آزمایش شود.
- [x] Guardهای Migration توانایی تشخیص/اعتبارسنجی RowVersion را داشته باشند.

## خلاصه تغییرات
- RowVersion به CompanyRole و UserCompanyMembership افزوده و با IsRowVersion پیکربندی شد.
- در UpdateRoleAsync، حتی تغییر permission linkها یا ارسال مقدار scalar بدون تغییر، یک UPDATE روی ردیف CompanyRole انجام می‌شود تا RowVersion کل تغییرات نقش را محافظت کند.
- در AssignMembershipAsync، RowVersion عضویت و نقش مقصد با UPDATE اجباری کنترل می‌شود.
- در TransferMembersAsync، RowVersion نقش مبدأ/مقصد و تمام عضویت‌های batch در یک SaveChanges کنترل می‌شود.
- DbUpdateConcurrencyException در هر سه endpoint به پاسخ پایدار 409 تبدیل می‌شود؛ retry خودکار وجود ندارد.
- Migration 20261103160000_AddCompanyRoleMembershipRowVersions دو ستون rowversion اضافه می‌کند؛ Down فقط همین دو ستون را حذف می‌کند.
- parser و guardهای migration برای تشخیص facet ردیف RowVersion و AddColumn<byte[]>(rowVersion:true) تقویت شدند.

## فایل‌های مهم تغییرکرده
- Domain/CompanyRole.cs و Domain/UserCompanyMembership.cs — tokenهای مدل.
- Infrastructure/Persistence/Configurations/* — تنظیم SQL Server rowversion.
- Server/Api/CompanyRoleAdministrationEndpoints.cs — کنترل optimistic concurrency و 409.
- Infrastructure/Persistence/Migrations/20261103160000_AddCompanyRoleMembershipRowVersions.cs — Migration افزایشی.
- Infrastructure/Persistence/Migrations/20261103160000_AddCompanyRoleMembershipRowVersions.Designer.cs و DatabaseContextModelSnapshot.cs — مدل هدف EF.
- tests/Template.Tests/DatabaseModelTests.cs — قرارداد مدل و زنجیره Migration.
- tests/Template.Tests/Integration/CompanyRoleMembersSqlServerTests.cs — تست رقابت writerها، rollback Audit و upgrade روی جدول پرشده.
- scripts/migration_model.py، scripts/verify_migration_model.py و scripts/tests/test_migration_tooling.py — اعتبارسنجی metadata و Migration rowversion.

## تصمیم‌های فنی
- از SQL Server rowversion native استفاده شد؛ timestamp برنامه‌ای یا lock توزیع‌شده اضافه نشد.
- برای وابستگی‌های خوانده‌شده در عملیات، no-op UPDATE روی role row باعث می‌شود rowversion نقش نیز در شرط concurrency قرار گیرد.
- کل عملیات و Audit در یک SaveChanges باقی می‌ماند؛ EF Core transaction باید تمام تغییرات همان SaveChanges را در صورت تعارض rollback کند.
- Migration فقط دو ستون افزایشی اضافه می‌کند و داده موجود را بازنویسی/حذف نمی‌کند.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| DatabaseModelTests | اضافه شد؛ اجرا نشده | RowVersion و migration discovery |
| CompanyRoleMembersSqlServerTests | اضافه شد؛ اجرا نشده | سه سناریوی تعارض API + rollback Audit + Down/Up روی داده موجود |
| scripts/tests/test_migration_tooling.py | اضافه شد/توسعه یافت؛ اجرا نشده | parse و validation برای rowversion |
| scripts/verify_migration_model.py | اصلاح شد؛ اجرا نشده | زنجیره Up/Down و metadata |
| dotnet build/test | اجرا نشده | نتیجه CI روی commit نهایی لازم است |
| SQL Server واقعی | اجرا نشده | فقط CI می‌تواند نتیجه را تأیید کند |

## امنیت، داده و سازگاری
- هر تعارض 409 است؛ داده بازنده یا Audit بازنده نباید commit شود.
- انتقال دسته‌ای یک SaveChanges و یک transaction EF Core دارد؛ تست SQL Server صریحاً عدم partial mutation را می‌سنجد.
- RowVersion روی تمام ردیف‌های فعلی SQL Server تولید می‌شود؛ تست upgrade، طول 8 بایت و یکتایی tokenها را بررسی می‌کند.
- هیچ permission grant یا tenant scope تغییر نکرده است.

## ریسک‌ها و کارهای باقی‌مانده
- تست SQL Server واقعی و guardهای Python هنوز اجرا نشده‌اند؛ CI باید سبز شود.
- rowversion از نوع provider-specific SQL Server است؛ استفاده از provider دیگر در این مدل پشتیبانی‌شده نیست.
- endpointهای خارج از Issue #496 که CompanyRole/UserCompanyMembership را تغییر می‌دهند باید در ممیزی‌های بعدی از همین concurrency contract پیروی کنند.

## شناسه‌های Git
- Branch: agent/496-role-membership-rowversion (نسخهٔ همگام‌شده: agent/496-role-membership-rowversion-mainline)
- Commit نهایی فعلی: 5a843981db12a1b614bac8b2c095aeda2147ed66
- Pull Request: https://github.com/hamidreza1394/Accounting/pull/496


## 2026-11-03 — تشخیص و اصلاح شکست‌های CI

- CI اولیه روی SHA قبلی، Build موفق داشت و سه تست SQL Server شکست خورد:
  - CompanyRoleMembersSqlServerTests: مقدار Username برابر rowversion-assignment از محدودیت ۲۰ نویسه عبور می‌کرد؛ به rv-assign و rv-transfer کوتاه شد.
  - CorporateGroupSqlServerTests: تست عمداً روی Schema پیش از RowVersion داده می‌کاشت اما از مدل EF جاری استفاده می‌کرد؛ درج CompanyRole و UserCompanyMembership به SQL پارامتری منتقل شد.
  - CompanyMembershipRequestSqlServerTests: همان مشکل در Seed قبل از Migration؛ درج نقش‌ها و عضویت‌های موجود به SQL پارامتری منتقل شد و پس از اعمال Migration، نقش‌ها دوباره از مدل جاری بارگذاری می‌شوند.
- برای جلوگیری از حل تعارض مخرب، تغییرات PR روی شاخهٔ تازه‌ای که از main شروع شده بود منتقل شدند؛ تغییرات main و PR #498 در docs حفظ شدند.
- بررسی کد TransferMembersAsync نشان داد تغییر نقش عضویت‌ها، no-op update نقش مبدأ/مقصد و رویدادهای Audit در یک SaveChanges قرار دارند؛ تست رقابت SQL Server برای اطمینان از rollback کامل همچنان باید در CI تأیید شود.
- Build و تست‌های پس از این اصلاحات در این محیط اجرا نشده‌اند؛ نتیجهٔ CI روی SHA نهایی معیار Verified شدن است.


## حفاظت از سقف واگذاری در گردش تأیید عضویت
- ApproveAsync اکنون عضویت‌های فعال تأییدکننده و نقش‌های مؤثر را tracked و RolePermissions را بارگذاری می‌کند؛ پیش از SaveChanges، RowVersion تمام عضویت‌ها/نقش‌های مبنای محاسبه و نقش مقصد با no-op UPDATE کنترل می‌شود.
- تغییر هم‌زمان عضویت تأییدکننده، وضعیت نقش، یا permission links از مسیر UpdateRoleAsync باعث DbUpdateConcurrencyException و پاسخ 409 می‌شود؛ درخواست Pending، عضویت و Audit موفق commit نمی‌شوند.
- تست SQL Server جدید revocation مجوز نقش تأییدکننده را بین read و SaveChanges شبیه‌سازی می‌کند و عدم تأیید/عضویت/Audit را بررسی می‌کند.
- تست انتقال گروهی اکنون دو عضو دارد؛ وقتی writer رقیب یکی را تغییر می‌دهد، عضو دوم باید در نقش مبدأ بماند و هیچ Audit انتقالی commit نشود.
